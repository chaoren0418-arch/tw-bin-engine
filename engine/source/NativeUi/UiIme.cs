namespace P06.NativeUi;

// f1247-ime-path (#1271): IME composition for the native text inputs (chat line, create-character name, …).
// Pure parts only (no Win32): where the composition / candidate windows go, the one-delivery rule for IME result characters,
// which key messages belong to the IME, and the enter / leave policy of an IME text session. The Win32 side is
// ClientForm.NativeUiIme (engine/source/NativeUiIme.cs). Report: docs/reports/issue-1271.md.

/// <summary>
/// A native panel that can take IME text right now reports its caret (logical 800×600 canvas coordinates: X = caret
/// column, Y = top of the text line, Height = line height). null = this panel is not taking text now.
/// </summary>
public interface IUiImeCaret { Rectangle? ImeCaret { get; } }

/// <summary>Composition window point (CFS_POINT) and candidate exclusion rectangle (CFS_EXCLUDE), in client pixels.</summary>
public readonly record struct UiImePlacement(Point Composition,Rectangle Exclude) {
    /// <summary>
    /// Logical caret → client pixels through the same viewport the canvas is presented with (UiPresenter). The process is
    /// PerMonitorV2 (Program.cs SetHighDpiMode) and AutoScaleMode.None, so ClientSize and the IMM client coordinates are both
    /// physical pixels: the effective DPI enters only through the viewport scale (DPI 96 → 800×600 client, s = 1; DPI 192 →
    /// 1600×1200 client, s = 2). The result is clamped inside the client area (an empty client gives the origin).
    /// </summary>
    public static UiImePlacement Compute(Rectangle caret,UiViewport view,Size client){
        int lx=Math.Clamp(caret.X,0,UiRoot.LogicalWidth-1),ly=Math.Clamp(caret.Y,0,UiRoot.LogicalHeight-1);
        int lh=Math.Max(1,caret.Height);
        int lb=Math.Clamp(caret.Y+lh,ly+1,UiRoot.LogicalHeight);
        var top=view.ToClient(new(lx,ly));
        int bottom=view.OffsetY+(int)MathF.Floor(lb*view.Scale);
        int maxX=Math.Max(0,client.Width-1),maxY=Math.Max(0,client.Height-1);
        int x=Math.Clamp(top.X,0,maxX),y=Math.Clamp(top.Y,0,maxY);
        int b=Math.Clamp(bottom,y,Math.Max(y,client.Height));
        int w=Math.Max(1,(int)MathF.Ceiling(view.Scale));
        return new(new(x,y),new(x,y,Math.Min(w,Math.Max(0,client.Width-x)),b-y));
    }
}

/// <summary>Which key messages the engine must leave to the IME.</summary>
public static class UiImeKeys {
    public const int WM_KEYDOWN=0x0100,WM_KEYUP=0x0101,WM_SYSKEYDOWN=0x0104,WM_SYSKEYUP=0x0105;
    /// <summary>VK_PROCESSKEY: the IME already took this key (ImmProcessKey) and TranslateMessage must turn it into WM_IME_*.</summary>
    public const int VK_PROCESSKEY=0xE5;
    /// <summary>
    /// true = PreProcessMessage must not run ProcessCmdKey for this message, so TranslateMessage (→ ImmTranslateMessage) and
    /// DispatchMessage still happen (Application.LightThreadContext: PreTranslateMessage true skips both). Only VK_PROCESSKEY;
    /// every real key (W, arrows, F1, Enter, …) keeps the existing path.
    /// </summary>
    public static bool PassToIme(int msg,nint wParam)=>
        msg is WM_KEYDOWN or WM_SYSKEYDOWN or WM_KEYUP or WM_SYSKEYUP&&((int)wParam&0xFFFF)==VK_PROCESSKEY;
}

/// <summary>
/// One delivery per IME result character. WM_IME_CHAR is delivered to the native text input directly (and not passed on, so
/// DefWindowProc never turns it into a WM_CHAR). Should a WM_CHAR for the same character still arrive before the next real
/// key press (an IME that also posts WM_CHAR), it is dropped once. A real key press (WM_KEYDOWN other than VK_PROCESSKEY)
/// clears the echo list, because every typed WM_CHAR is translated from its own WM_KEYDOWN, which arrives first.
/// </summary>
public sealed class UiImeCharGate {
    readonly Queue<char> echo=new();
    public int Delivered{get;private set;}
    public int Dropped{get;private set;}
    /// <summary>WM_IME_CHAR: deliver once. Returns whether a text input took it.</summary>
    public bool ImeChar(char c,Func<char,bool> deliver){
        bool used=deliver(c);
        if(used){Delivered++;echo.Enqueue(c);if(echo.Count>64)echo.Dequeue();}
        return used;
    }
    /// <summary>WM_CHAR: true = this is the echo of an IME character already delivered; drop it.</summary>
    public bool IsEcho(char c){
        if(echo.Count==0||echo.Peek()!=c)return false;
        echo.Dequeue();Dropped++;return true;
    }
    /// <summary>WM_KEYDOWN of a real key (not VK_PROCESSKEY).</summary>
    public void KeyDown(int vk){if(vk!=UiImeKeys.VK_PROCESSKEY)echo.Clear();}
    public void Reset()=>echo.Clear();
}

/// <summary>What the Win32 side does when a text input starts or stops taking IME text.</summary>
public enum UiImeAction { None, Associate, RestoreDetached, Close, CancelComposition, RestoreConversion }

/// <summary>
/// Enter / leave policy. Entering a text input: if the focused window has no input context, the default one is associated
/// (ImmAssociateContextEx IACE_DEFAULT); the open status and conversion mode the user had are remembered. Leaving it (back
/// to the game): an unfinished composition is cancelled, the open status and the conversion mode go back to what they were
/// before (a closed or alphanumeric IME stays that way, so it does not take W/A/S/D, arrows, F-keys afterwards), and a
/// context the engine associated is detached again. In game focus nothing is touched (the state stays as it was).
/// 推測 (the TW16 BIN has no ImmAssociateContext / ImmSetOpenStatus import): restoring on leave stands in for the original's
/// separate edit window; sub_8886E0 likewise switches a native-mode IME to conversion 0 before it synthesises a key.
/// </summary>
public sealed class UiImeSession {
    bool active,openedBefore,associated;
    public bool Active=>active;
    public int ConversionBefore{get;private set;}
    public IReadOnlyList<UiImeAction> Enter(bool hasContext,bool open,int conversion){
        if(active)return [];
        active=true;associated=!hasContext;openedBefore=hasContext&&open;ConversionBefore=hasContext?conversion:0;
        return associated?[UiImeAction.Associate]:[];
    }
    public IReadOnlyList<UiImeAction> Leave(bool composing,bool open,int conversion){
        if(!active)return [];
        active=false;
        var list=new List<UiImeAction>();
        if(composing)list.Add(UiImeAction.CancelComposition);
        if(conversion!=ConversionBefore)list.Add(UiImeAction.RestoreConversion);
        if(open&&!openedBefore)list.Add(UiImeAction.Close);
        if(associated)list.Add(UiImeAction.RestoreDetached);
        associated=false;
        return list;
    }
}

/// <summary>The panel whose caret the IME follows: the first usable panel (hit order, focused first) that reports one.</summary>
public static class UiImeTargets {
    public static (IUiPanel Panel,Rectangle Caret)? Find(IEnumerable<IUiPanel> order,Func<IUiPanel,bool> usable){
        foreach(var p in order){
            if(p is not IUiImeCaret c||!usable(p))continue;
            Rectangle? caret;
            try{caret=c.ImeCaret;}catch{continue;}
            if(caret is {} r)return (p,r);
        }
        return null;
    }
}
