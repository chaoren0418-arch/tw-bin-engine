using System.Globalization;
namespace P06.NativeUi;

// T329 (#767, P-D of docs/reference/pak-resources.md §4.2 row 4:FishingUI): the native fishing window Fishing_Window.
// FishingUI.xml is not in 0-客戶端 (PAK§4: all 24 idx checked); it is read at run time from the reference client through
// UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak; SHA-256 logged as "ui-xml-ref").
// Its 8 surfs go through SurfCatalog.Get: local Sprite*.pak .img first, then the T322/#760 reference fallback. Per T320/#758
// the local 6102–6108 exist only as same-numbered Icon-group .tbt files, which a surf never reads, so they come from the
// reference client (kor25 Sprite04–10) or stay undrawn; 5902/5903 have local c.img. Nothing is copied, no substitute picture.
//
// Loading. UiRoot.Build is all-or-nothing, so the registration uses the host layout "uistatus86" (always in the local
// Tile.pak, same as GameRankingPanel / OptionPanel) and Load reads FishingUI.xml itself: no reference client / no XML /
// malformed XML / no window background → Unavailable, the window never opens and the rest of the native UI keeps working.
//
// Opening. Fishing.cs raises FishingWindow.Changed whenever the own fishing state changes (S158 action 71 start, S71
// 1137/1138/1163/403, S119 end, C62 sent). The window opens when fishing becomes active and closes when it ends; the
// event can come from the network thread, so it is queued under a lock and applied in Update (UI thread).
// QuitFishing (R9-P／#860, 已驗證 FU9 §14.3: sub_E659A0 first opens Y/N 1516「你要取消自動釣魚嗎？」, yes → C_THROW, 3.81 C62):
// the click only raises PendingQuit; FishingQuitPromptPanel draws 1516 on the chat input line (推測：輸入列提示與 Y／N／Esc
// 的回答方式沿用 R9-H 好友刪除，真機再確認); Y → FishingWindow.Quit (the legacy HUD's C62 path); the window stays until the
// server confirms the stop (S71 1163 / S119). With --classic-ui no panel exists and the legacy FishingHud button is unchanged.
//
// Layout (照 XML; 推測 where marked): window X/Y from the XML, size = BaseSurf image; Moveable="20" = the top 20 px drag the
// window; Images then Buttons in XML order, coordinates relative to the window. Not drawn (已驗證 FU9 §14.3, reported):
// BaitTemp_Image / BaitCount_Label (the original refreshes the count of bait item 14467; the 3.81 inventory wire carries no
// template id — docs/protocol/fishing.md — so there is no data source), ReelGuage_Image AniGauge (sub_E65510 takes its
// duration from a TW16 auto-fishing message object; 3.81 has only C62/C164/S158/S119/S71 with no duration or state field,
// K1), Tooltip children (string-table tooltips, not wired in this window). FishingNet (已驗證 sub_E65B00) sends no packet:
// it toggles the FishObtainUI window (FishObtainPanel.cs). Its list is the inventory filtered by the fishing desc-id table
// (FishObtainTable.cs, unk_1542864, 已驗證 FU10§1): filled only by S5/S15 tail flag bit 3, which 3.81 never sets → empty list (R10-A #888).
// FrameSurf equals BaseSurf in the reference XML and is not drawn twice.

/// <summary>One surf reference "6102;1" → Id 6102, Flag 1 (flag meaning 待驗證).</summary>
public sealed record FishingSurf(string Attr,int Id,int Flag);

public enum FishingPieceKind { Image, Button }

/// <summary>A drawable element. Offset is relative to the window origin; a null surface = missing surf (not drawn).</summary>
public sealed record FishingPiece(FishingPieceKind Kind,string Path,string? Name,Point Offset,FishingSurf? Base,UiSurface? BaseSurface,
    UiSurface? HighlightSurface,UiSurface? PushSurface,string? MouseEvent) {
    public Rectangle Local=>BaseSurface==null?new(Offset,Size.Empty):new(Offset,new(BaseSurface.Width,BaseSurface.Height));
    public bool Clickable=>Kind==FishingPieceKind.Button&&Local.Width>0&&Local.Height>0;
}

/// <summary>R9-P（#860）：1516 Y/N 詢問；Revision 每次新詢問 +1，回答時用來確認還是同一個詢問。</summary>
public sealed record FishingQuitConfirm(int Revision,string Text);

public static class FishingQuitText {
    /// <summary>String 1516（string-c.tbl，FU9 §14.3）。</summary>
    public const int ConfirmId=1516;
    /// <summary>0-客戶端 string-c.tbl 1516 原文（text_tables.py 讀出）；字串表不在時用。</summary>
    public const string ConfirmFallback="你要取消自動釣魚嗎？";
    public static string Confirm=>ServerMessageText.Table is {} t&&ConfirmId<t.Count
        &&ServerMessageText.DisplayText(t[ConfirmId]).Trim() is {Length:>0} s?s:ConfirmFallback;
}

public sealed class FishingPanel(UiResources? resources=null,Func<bool>? quit=null,Func<FishObtainPanel?>? obtain=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(181)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new FishingPanel(root.Resources,null,()=>root.Find(FishObtainPanel.PanelId) as FishObtainPanel));   // T329 (#767): FishingUI.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="Fishing_Window";
    public const string LayoutName="FishingUI",WindowNode="Window",WindowName="Fishing_Window";
    public const string QuitEvent="QuitFishing",NetEvent="FishingNet";
    /// <summary>Layout handed to UiRoot.Build (always in the local Tile.pak); the real layout is LayoutName, read in Load.</summary>
    public const string HostLayout="uistatus86";

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>Why the window cannot open; null when usable.</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Size Size{get;private set;}
    public int MoveableHeight{get;private set;}
    public Rectangle Bounds=>Size.IsEmpty?Rectangle.Empty:new(Origin,Size);
    public bool Visible=>open;
    public FishingPiece? Background{get;private set;}
    /// <summary>Images then buttons, in XML order (paint order).</summary>
    public IReadOnlyList<FishingPiece> Pieces{get;private set;}=[];
    /// <summary>Surfs named in the XML but not found (not drawn): "surf 6104 (FishingUI/Window/Button[0]/@HighlightSurf)".</summary>
    public IReadOnlyList<string> Missing{get;private set;}=[];
    /// <summary>Elements deliberately not built (no data source / not wired), by path and reason.</summary>
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    /// <summary>Latest own fishing state applied in Update (null before the first event).</summary>
    public FishingWindowEventArgs? State{get;private set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;

    /// <summary>R9-P（#860）：QuitFishing 已按、等 1516 回答（null＝沒有詢問）。</summary>
    public FishingQuitConfirm? PendingQuit{get;private set;}

    readonly Func<bool> quitRoute=quit??FishingWindow.Quit;
    readonly Func<FishObtainPanel?>? obtainRoute=obtain;
    bool open;FishingPiece? pressed,hover;bool pressedInside;bool quitSent;int quitAsk;
    readonly HashSet<string> unhandled=new(StringComparer.Ordinal);
    readonly object gate=new();readonly Queue<FishingWindowEventArgs> pending=new();

    // ---- event wiring (FishingWindow.Changed) ----
    static readonly object currentGate=new();
    static WeakReference<FishingPanel>? current;
    static FishingPanel(){FishingWindow.Changed+=OnChanged;}
    static void OnChanged(object? sender,FishingWindowEventArgs e){
        FishingPanel? panel=null;
        lock(currentGate)current?.TryGetTarget(out panel);
        panel?.Offer(e);
    }
    /// <summary>Makes this panel the one that receives fishing changes (the latest loaded, usable panel wins).</summary>
    internal void Attach(){lock(currentGate)current=new(this);}
    internal static void Detach(){lock(currentGate)current=null;}
    internal static bool IsAttached(FishingPanel panel){lock(currentGate)return current!=null&&current.TryGetTarget(out var p)&&ReferenceEquals(p,panel);}

    /// <summary>Queues a state change for the next Update. false (ignored) when the panel is unusable.</summary>
    public bool Offer(FishingWindowEventArgs e){
        if(!Available)return false;
        lock(gate){pending.Enqueue(e);while(pending.Count>16)pending.Dequeue();}
        return true;
    }

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"fishing-ui 無法載入 {LayoutName}，釣魚窗停用（舊式釣魚鈕照舊）：{ex.Message}");return;}
        try{Load(layout,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"fishing-ui {layout.FileName} 版面不符，釣魚窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"fishing-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Available)Attach();else UiResourceLog.Write($"fishing-ui 停用：{Unavailable}");
    }

    void Clear(){
        open=false;pressed=hover=null;pressedInside=false;quitSent=false;PendingQuit=null;LayoutFile=null;Origin=Point.Empty;Size=Size.Empty;MoveableHeight=0;
        Background=null;Pieces=[];Missing=[];Skipped=[];State=null;Unavailable="not loaded";lock(gate)pending.Clear();
    }

    /// <summary>Offline checks: surfs come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        Clear();
        var window=layout.Root.Child(WindowNode);
        if(window.Attr("Name")!=WindowName)throw new UiLayoutException(layout.FileName,window.Path,$"Window Name 不是 {WindowName}");
        var missing=new List<string>();var skipped=new List<string>();
        UiSurface? Resolve(FishingSurf? r,string path){
            if(r==null)return null;
            var s=surf(r.Id);
            if(s==null)missing.Add($"surf {r.Id} ({path})");
            return s;
        }
        var baseRef=SurfAttr(window,"BaseSurf");
        var baseSurface=Resolve(baseRef,window.Path+"/@BaseSurf");
        if(SurfAttr(window,"FrameSurf") is {} frame&&frame.Id!=baseRef?.Id)skipped.Add(window.Path+"/@FrameSurf（與底圖不同，邊框畫法待驗證）");
        var images=new List<FishingPiece>();var buttons=new List<FishingPiece>();
        foreach(var c in window.Children){
            var at=new Point(c.TryInt("X")??0,c.TryInt("Y")??0);
            switch(c.Name){
                case "Image":{
                    var r=SurfAttr(c,"Surf");
                    if(r==null){skipped.Add(c.Path+"（沒有 Surf）");break;}
                    images.Add(new(FishingPieceKind.Image,c.Path,c.Attr("Name"),at,r,Resolve(r,c.Path),null,null,c.Attr("MouseEvent")));
                    break;
                }
                case "Button":{
                    var b=SurfAttr(c,"BaseSurf");
                    var h=SurfAttr(c,"HighLightSurf")??SurfAttr(c,"HighlightSurf");
                    var p=SurfAttr(c,"PushSurf");
                    buttons.Add(new(FishingPieceKind.Button,c.Path,c.Attr("Name"),at,b,Resolve(b,c.Path+"/@BaseSurf"),
                        Resolve(h,c.Path+(c.Attr("HighLightSurf")!=null?"/@HighLightSurf":"/@HighlightSurf")),Resolve(p,c.Path+"/@PushSurf"),c.Attr("MouseEvent")));
                    break;
                }
                case "Label":skipped.Add(c.Path+"（魚餌數量：原版數物品 14467，3.81 背包封包沒有模板編號，沒有資料來源）");break;   // 已驗證 FU9 §14.3
                case "AniGauge":skipped.Add(c.Path+"（收竿量表：時長來自 TW16 自動釣魚封包，3.81 不送時長與狀態）");break;   // 已驗證 FU9 §14.3（K1）
                default:skipped.Add(c.Path);break;
            }
            foreach(var tip in c.Children)skipped.Add(tip.Path+(tip.Name=="Tooltip"?$"（提示字串 {tip.Attr("String")} 未接）":""));
        }
        LayoutFile=layout.FileName;Origin=new(window.TryInt("X")??0,window.TryInt("Y")??0);MoveableHeight=window.TryInt("Moveable")??0;
        Background=baseRef==null?null:new(FishingPieceKind.Image,window.Path,window.Attr("Name"),Point.Empty,baseRef,baseSurface,null,null,null);
        Size=baseSurface==null?Size.Empty:new(baseSurface.Width,baseSurface.Height);
        Pieces=[..images,..buttons];Missing=missing;Skipped=skipped;
        Unavailable=Size.IsEmpty?$"{layout.FileName}: 視窗底圖 BaseSurf 缺，無法定出視窗範圍（不用替代圖）":null;
    }

    /// <summary>"6102;1" → (6102, 1); "5902" → (5902, 0). Attribute absent → null.</summary>
    public static FishingSurf? SurfAttr(UiNode n,string attr){
        var raw=n.Attr(attr);if(raw==null)return null;
        var parts=raw.Split(';');
        static bool P(string s,out int v)=>int.TryParse(s.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out v);
        if(parts.Length>2||!P(parts[0],out int id)||id<=0)throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是 surf 編號");
        int flag=0;if(parts.Length==2&&!P(parts[1],out flag))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 的旗標不是整數");
        return new(attr,id,flag);
    }

    public void Update(IUiGameState state){
        if(!state.InWorld){if(open)Close();State=null;quitSent=false;PendingQuit=null;lock(gate)pending.Clear();return;}
        while(true){
            FishingWindowEventArgs? e;
            lock(gate){if(!pending.TryDequeue(out e))break;}
            Apply(e);
        }
    }
    void Apply(FishingWindowEventArgs e){
        bool wasActive=State is {Active:true};
        if(State!=null&&State.Actor==e.Actor&&e.Revision<State.Revision)return;   // stale (out of order) change
        State=e;
        if(!e.CancelPending)quitSent=false;
        if(!e.Active||e.CancelPending)PendingQuit=null;   // 釣魚已結束或已送 C62：1516 詢問失效
        if(e.Active&&!wasActive)Open();
        else if(!e.Active&&open)Close();
    }

    /// <summary>Shows the window (fishing or not). false when unavailable.</summary>
    public bool Open(){
        if(!Available)return false;
        open=true;pressed=null;pressedInside=false;
        return true;
    }
    public void Close(){open=false;pressed=hover=null;pressedInside=false;PendingQuit=null;}

    /// <summary>QuitFishing can send: fishing is active, no C62 is pending (server or local).</summary>
    public bool CanQuit=>State is {Active:true,CancelPending:false}&&!quitSent;

    /// <summary>
    /// R9-P（#860）：回答 1516。revision 不是目前的詢問 → false（不動作）。是 → 仍可收竿時走 C62 路徑；否 → 只關詢問。
    /// 回傳 true＝詢問已處理（不論是否送出）。
    /// </summary>
    public bool AnswerQuit(int revision,bool yes){
        if(PendingQuit is not {} q||q.Revision!=revision)return false;
        PendingQuit=null;
        if(!yes||!CanQuit)return true;
        bool sent;
        try{sent=quitRoute();}catch(Exception ex){UiResourceLog.Write("fishing-ui 收竿失敗："+ex.Message);sent=false;}
        if(sent)quitSent=true;
        return true;
    }

    public Rectangle At(FishingPiece p)=>new(Origin.X+p.Local.X,Origin.Y+p.Local.Y,p.Local.Width,p.Local.Height);
    /// <summary>Topmost clickable piece at a logical point (later in paint order = on top).</summary>
    public FishingPiece? ButtonAt(Point location)=>open?Pieces.Where(p=>p.Clickable).LastOrDefault(p=>At(p).Contains(location)):null;

    public void Paint(UiCanvas canvas){
        if(!open)return;
        canvas.DrawSurface(Background?.BaseSurface,Origin.X,Origin.Y);
        foreach(var p in Pieces)canvas.DrawSurface(p.Kind==FishingPieceKind.Button?ButtonSurface(p):p.BaseSurface,Origin.X+p.Offset.X,Origin.Y+p.Offset.Y);
    }

    /// <summary>Push while held or while its C62 is pending (QuitFishing); Highlight while hovered; a missing state image falls back to Base.</summary>
    public UiSurface? ButtonSurface(FishingPiece p){
        bool held=p==pressed&&pressedInside;
        bool waiting=p.MouseEvent==QuitEvent&&(quitSent||State is {CancelPending:true});
        if((held||waiting)&&p.PushSurface!=null)return p.PushSurface;
        if(p==hover&&p.HighlightSurface!=null)return p.HighlightSurface;
        return p.BaseSurface;
    }

    public bool IsDragHandle(Point location)=>open&&Bounds.Contains(location)&&location.Y<Origin.Y+MoveableHeight&&ButtonAt(location)==null;
    public void MoveTo(Point location)=>Origin=location;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Move)hover=ButtonAt(e.Location);
        if(e.Kind==UiMouseKind.Wheel||e.Button!=MouseButtons.Left&&e.Kind!=UiMouseKind.Move)return inside||pressed!=null;
        switch(e.Kind){
            case UiMouseKind.Down:
                pressed=ButtonAt(e.Location);pressedInside=pressed!=null;
                return inside;
            case UiMouseKind.Move:
                if(pressed!=null)pressedInside=At(pressed).Contains(e.Location);
                return inside||pressed!=null;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;pressedInside=false;
                if(was!=null&&At(was).Contains(e.Location))Fire(was);
                return inside||was!=null;
            }
            default:return inside;
        }
    }

    void Fire(FishingPiece p){
        if(p.MouseEvent==QuitEvent){
            if(!CanQuit||PendingQuit!=null)return;
            PendingQuit=new(++quitAsk,FishingQuitText.Confirm);   // sub_E659A0：先問 1516，回答見 AnswerQuit
            return;
        }
        if(p.MouseEvent==NetEvent&&obtainRoute?.Invoke() is {Available:true} w){w.Toggle(Bounds);return;}   // sub_E65B00：不送封包，開關 FishObtainUI
        string key=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(key))UiResourceLog.Write($"fishing-ui {key} 尚未接上（沒有已核實的封包）");
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}

/// <summary>
/// R9-P（#860）：1516 Y/N 提示畫在聊天輸入列（YesNoButtons.Line，同 R9-H FriendDeletePromptPanel）。
/// 推測（真機再確認）：原版 1516 的 Y/N 窗樣式沒查；這裡沿用輸入列提示，Y＝是，N／Esc＝否。
/// 伺服器詢問（YesNoPanel）或好友刪除詢問顯示中時讓位（不畫、不收鍵），詢問保留。聊天輸入列開著時不收 Y／N。滑鼠一律不攔。
/// </summary>
public sealed class FishingQuitPromptPanel(Func<string,IUiPanel?>? find=null):IUiPanel {
    public const string PanelId="R9P_FishingQuitPrompt";
    [UiPanel(197)] internal static UiPanelRegistration Registration=>new(PanelId,FishingPanel.HostLayout,root=>new FishingQuitPromptPanel(root.Find));   // R9-P (#860): string 1516 Y/N on the chat input line

    bool inWorld;
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    FishingPanel? Fishing=>find?.Invoke(FishingPanel.PanelId) as FishingPanel;
    bool Yielding=>find?.Invoke(YesNoPanel.PanelId) is {Visible:true}||find?.Invoke(FriendDeletePromptPanel.PanelId) is {Visible:true};
    bool ChatOpen=>find?.Invoke(ChatPanel.WindowName) is ChatPanel{Input.Open:true};
    public FishingQuitConfirm? Current=>inWorld&&!Yielding?Fishing?.PendingQuit:null;
    public bool Visible=>Current!=null;
    public Rectangle Bounds=>Visible?YesNoButtons.Line:Rectangle.Empty;

    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state)=>inWorld=state.InWorld;
    public void Paint(UiCanvas canvas){
        if(Current is not {} c)return;
        var line=YesNoButtons.Line;var g=canvas.Graphics;var saved=g.Save();
        try{
            g.IntersectClip(line);var size=UiText.Measure(g,c.Text);
            canvas.DrawText(c.Text,line.X,line.Y+Math.Max(0,(line.Height-size.Height)/2),Color.White,Color.Black);
        }finally{g.Restore(saved);}
    }
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(Current is not {} c||!e.Down||e.Repeat||Fishing is not {} f)return false;
        if((e.KeyData&Keys.Modifiers)!=Keys.None)return false;
        if(ChatOpen)return false;   // 聊天列開著：Y／N 是打字、Esc 先關聊天列
        var key=e.KeyData&Keys.KeyCode;
        if(key==Keys.Y)return f.AnswerQuit(c.Revision,true);
        if(key==Keys.N||key==Keys.Escape)return f.AnswerQuit(c.Revision,false);
        return false;
    }
}
