using System.Drawing.Imaging;
using P06.NativeUi;
namespace P06;

// C2 (Issue #463): connects the native UI to ClientForm behind the nativeUi switch (docs/design/native-ui.md §1.1, §3).
// Switch off (default) → nothing here runs and the legacy HUD path is unchanged.
// Switch on → the client verifies the pinned client resources in the background (several GB of SHA-256). Until that
// finishes the legacy HUD stays in charge; on success the native canvas takes over (legacy HUD hidden and deaf, because
// PlayerHudActive is false); on any failure the legacy HUD stays and one red line says why. Legacy HUD controls are still
// constructed so the fallback needs no rebuild of the dev-tools pages; while native is active they never show or get keys.
public sealed partial class ClientForm {
    UiOptions nativeUiOptions=UiOptions.Off;
    UiStartState nativeUiState=UiStartState.Off;
    UiRoot? nativeUiRoot;UiInput? nativeUiInput;UiCanvas? nativeUiCanvas;
    string? nativeUiError;
    internal UiStartState NativeUiState=>nativeUiState;
    internal string? NativeUiError=>nativeUiError;
    internal UiRoot? NativeUiRoot=>nativeUiRoot;
    internal bool NativeUiActive=>nativeUiState==UiStartState.Active;

    void InitNativeUi(){
        nativeUiOptions=UiOptions.Resolve(UiOptions.CommandLine,Path.Combine(stageRoot,"runtime",UiOptions.SettingsFileName));
        if(!nativeUiOptions.Enabled)return;
        // §3.2: the client area is at least 800×600 and starts at 800×600 while the switch is on.
        MinimumSize=SizeFromClientSize(new(UiRoot.LogicalWidth,UiRoot.LogicalHeight));ClientSize=new(UiRoot.LogicalWidth,UiRoot.LogicalHeight);
        nativeUiState=UiStartState.Verifying;events.Add(new{kind="native-ui",state="verifying",source=nativeUiOptions.Source,fill=nativeUiOptions.Fill});
        var ui=SynchronizationContext.Current;
        Task.Run(()=>UiRoot.Build(UiResources.Shared,UiPanelRegistry.Panels)).ContinueWith(t=>{
            void Apply(){if(!IsDisposed)ApplyNativeUiStart(t.IsCompletedSuccessfully?t.Result:null,t.Exception?.GetBaseException());}
            if(ui!=null)ui.Post(_=>Apply(),null);else if(IsHandleCreated)BeginInvoke(Apply);else Apply();
        },TaskScheduler.Default);
    }

    /// <summary>Result of the background start: a built root (→ Active) or the error (→ Failed, legacy HUD + one red line).</summary>
    internal void ApplyNativeUiStart(UiRoot? root,Exception? error){
        if(nativeUiState!=UiStartState.Verifying)return;
        if(root!=null&&error==null){
            nativeUiRoot=root;nativeUiInput=new UiInput(root,new NativeUiCommands(this));nativeUiCanvas??=new UiCanvas();
            nativeUiState=UiStartState.Active;nativeUiError=null;
            ClearInput("native-ui-active");
            events.Add(new{kind="native-ui",state="active",panels=root.Panels.Select(p=>p.Id).ToArray()});
        }else{
            nativeUiState=UiStartState.Failed;
            nativeUiError=error is UiResourceException?UiRoot.PinMismatchMessage:"原生 UI 無法啟動，已使用舊介面："+error?.Message;
            UiResourceLog.Write("native-ui fallback: "+error);
            events.Add(new{kind="native-ui",state="failed",error=error?.ToString()});
        }
        SyncPlayerHud();Invalidate();
    }

    UiViewport NativeUiViewport()=>UiPresenter.Compute(ClientSize,nativeUiOptions.Fill);

    /// <summary>OnPaint branch: compose the logical canvas and present it. false = native inactive, paint the legacy way.</summary>
    bool PaintNativeUi(PaintEventArgs e){
        if(!NativeUiActive)return false;
        var view=NativeUiViewport();nativeUiInput!.Viewport=view;
        ComposeNativeUi(nativeUiCanvas!);
        UiPresenter.Present(e.Graphics,nativeUiCanvas!.Bitmap,ClientSize,view);
        return true;
    }
    void ComposeNativeUi(UiCanvas canvas){
        nativeUiRoot!.Update(NativeUiGameState());
        nativeUiRoot.Compose(canvas,PaintNativeWorld);
    }
    // World layer: the existing scene painter with viewport = WorldRect, so Camera.View = WorldRect.Size (§3.2).
    // Native mode skips base.OnPaint, so the legacy liquor handler never runs; the same row warp is applied here, to the
    // world only (the original warps the world frame, not the UI).
    void PaintNativeWorld(Graphics g,Rectangle world){
        int level=OnlineMode&&login.World.Renderable?login.World.LiquorLevel:0;
        if(level==0){PaintScene(new PaintEventArgs(g,world),world);return;}
        using var frame=new Bitmap(world.Width,world.Height,PixelFormat.Format32bppArgb);
        using(var fg=Graphics.FromImage(frame)){fg.Clear(BackColor);fg.TranslateTransform(-world.X,-world.Y);PaintScene(new PaintEventArgs(fg,world),world);}
        int phase=login.World.LiquorPhase;float zoom=Math.Max(Camera.Zoom,.01f);
        WarpLiquorFrame(frame,row=>(int)Math.Round(TimedBuffRules.ScanlineOffset(level,phase,(int)(row/zoom))*zoom));
        g.DrawImage(frame,world.Location);
    }
    /// <summary>Evidence: the composed 800×600 canvas (before scaling); null when native is not active.</summary>
    Bitmap? CaptureNativeLogical(){
        if(!NativeUiActive)return null;
        using var canvas=new UiCanvas();ComposeNativeUi(canvas);
        return canvas.Bitmap.Clone(UiRoot.Canvas,PixelFormat.Format32bppPArgb);
    }
    /// <summary>Legacy paint path: one red line when the switch is on but native start failed (§2.1).</summary>
    void PaintNativeUiNotice(Graphics g){
        if(nativeUiState!=UiStartState.Failed||nativeUiError==null)return;
        g.ResetClip();g.ResetTransform();
        var size=g.MeasureString(nativeUiError,Font);
        using var back=new SolidBrush(Color.FromArgb(200,0,0,0));
        g.FillRectangle(back,0,0,size.Width+16,size.Height+8);
        using var red=new SolidBrush(Color.FromArgb(255,90,80));
        g.DrawString(nativeUiError,Font,red,8,4);
    }

    /// <summary>
    /// Mouse branch. Inactive → e unchanged. Active → null when the event is consumed (panel) or dropped (black border);
    /// otherwise a copy of e in logical coordinates for the existing world flow (Camera viewport is WorldRect).
    /// </summary>
    MouseEventArgs? NativeUiMouse(MouseEventArgs e,UiMouseKind kind){
        if(!NativeUiActive)return e;
        nativeUiInput!.Viewport=NativeUiViewport();
        var r=nativeUiInput.Mouse(kind,e.Location,e.Button,e.Clicks,e.Delta,ModifierKeys);
        if(r.Route==UiRoute.Panel)Invalidate();
        return r.Route==UiRoute.World&&r.Logical is Point p?new MouseEventArgs(e.Button,e.Clicks,p.X,p.Y,e.Delta):null;
    }
    /// <summary>Key branch: true = a native panel used the key (the game and the legacy HUD must not see it).</summary>
    bool NativeUiKey(Keys keyData,bool down,bool repeat){
        if(!NativeUiActive)return false;
        bool used=nativeUiInput!.Key(new UiKeyEvent(keyData,down,repeat));
        if(used)Invalidate();
        return used;
    }
    void ResetNativeUiInput()=>nativeUiInput?.Reset();

    IUiGameState NativeUiGameState(){
        var w=login.World;var s=w.Status;bool inWorld=login.State==LoginState.InWorld;
        var basic=new UiStaticGameState(inWorld,inWorld?w.Actor?.Name:null,inWorld?s?.Level:null,inWorld?s?.Hp:null,inWorld?s?.MaxHp:null,inWorld?s?.Mp:null,inWorld?s?.MaxMp:null,Environment.TickCount64);
        return WithHpMpState(new UiLeftStatusGameState(basic,inWorld?LeftStatusState.From(w):LeftStatusState.Empty));   // ui-left-status (#464) inside, ui-hpmp #465 over-head targets outside (State/HpMpState.cs)
    }

    sealed class NativeUiCommands(ClientForm form):IUiCommands {
        public void ShowDevTools(string? page=null){form.ClearInput("native-ui-dev-tools");form.ShowDevTools();}
        public void ReturnGameFocus()=>form.ReturnGameFocus();
        public void Logout(){form.ClearInput("native-ui-logout");form.CancelLogin(form.login.SessionGeneration,false);form.SyncPlayerHud();}
    }
}
