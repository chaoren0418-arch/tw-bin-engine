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
        KeyPress+=(_,e)=>{if(NativeUiChar(e.KeyChar))e.Handled=true;};   // ui-chat-window (#468)
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
            nativeUiRoot=root;nativeUiInput=new UiInput(root,NativeMacroCommands(new NativeUiCommands(this)));nativeUiCanvas??=new UiCanvas();
            nativeUiState=UiStartState.Active;nativeUiError=null;
            AttachNativeMenuPlayStats(root);
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
        // R5-A (#733): the window stack's 509 rule reads the current map (sub_A57C60, *dword_138BE60).
        nativeUiRoot!.CurrentMap=NativeUiMap();
        nativeUiRoot.Update(NativeUiGameState());
        CheckNativeLoginNotice();
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

    // T352 (#815, R7-M): S48 news in the native Notice_Window (LoginPanel.ShowNotice). The (revision, session) handed over is
    // kept so that, should the panel fault or drop the notice while LoginConnection still waits for that confirmation, the news
    // falls back to the existing message box instead of leaving the login waiting.
    (int Revision,int Session)? nativeLoginNotice;
    /// <summary>The login panel when registered and not faulted; visible = also painted and given input now (UiRoot.Usable).</summary>
    LoginPanel? NativeLoginPanel(bool visible)=>NativeUiActive&&nativeUiRoot?.Find(LoginPanel.PanelId) is LoginPanel p&&!nativeUiRoot.Faulted.Contains(p)&&(!visible||nativeUiRoot.Usable(p))?p:null;
    /// <summary>true = the native Notice_Window shows the news (its Ok confirms); false = the caller shows the message box.</summary>
    bool ShowNativeLoginNotice(string text,int revision,int session){
        // Not required to be visible yet: ShowNotice re-opens a closed login screen, it shows on the next UiRoot.Update.
        if(NativeLoginPanel(false) is not {} panel)return false;
        if(!panel.ShowNotice(text,()=>{nativeLoginNotice=null;ConfirmLoginNews(revision,session);}))return false;
        nativeLoginNotice=(revision,session);
        LeaveGameFocus("login-news");ClearInput("login-news");Invalidate();
        events.Add(new{kind="native-ui-login-news",revision,session});
        return true;
    }
    void CheckNativeLoginNotice(){
        if(nativeLoginNotice is not {} handed)return;
        var (revision,session)=handed;
        if(!login.NewsPending||login.NewsRevision!=revision||login.SessionGeneration!=session){nativeLoginNotice=null;return;}
        // Runs right after UiRoot.Update: the notice must be open in a panel that is painted and gets input (not faulted,
        // not pushed out of the window stack); otherwise the message box takes over.
        if(NativeLoginPanel(true) is {Notice:not null})return;
        nativeLoginNotice=null;
        events.Add(new{kind="native-ui-login-news",revision,session,fallback="message-box"});
        BeginInvoke(()=>ShowLoginNewsBox(revision,session));
    }
    int? NativeUiMap()=>login.State==LoginState.InWorld?login.World.MapId:null;

    IUiGameState NativeUiGameState(){
        var w=login.World;var s=w.Status;bool inWorld=login.State==LoginState.InWorld;
        var basic=new UiStaticGameState(inWorld,inWorld?w.Actor?.Name:null,inWorld?s?.Level:null,inWorld?s?.Hp:null,inWorld?s?.MaxHp:null,inWorld?s?.Mp:null,inWorld?s?.MaxMp:null,Environment.TickCount64);
        // ui-chat-window (#468): UiChatGameState.From walks HpMp → macro → left-status → chat, the other lookups never reach it.
        // ui-party (#471) under chat: PartyStates.StateOf unwraps all four layers.
        // ui-buff-icons (#473) innermost, under party: BuffState.Of unwraps all five layers.
        // ui-red-notice (#474) innermost, under buff: RedNoticeState.Of unwraps all six layers.
        // R3-A (#678) innermost, under red notice: item / spell / warehouse / pledge decorators (LegacyWindowPanel.FindState unwraps all).
        var chat=new UiChatGameState(NativePartyGameState(WithBuffState(WithRedNoticeState(NativeLegacyWindowState(basic,inWorld)))),inWorld?new UiChatSnapshot(login.PlayerChatSnapshot(),gameFocus):UiChatSnapshot.Empty);
        return WithHpMpState(NativeMacroGameState(new UiLeftStatusGameState(chat,inWorld?LeftStatusState.From(w):LeftStatusState.Empty)));   // ui-hpmp #465 outside; #466 macro (MacroBook.StateOf unwraps HpMp only, so it sits right inside); ui-left-status #464 (LeftStatusState.From unwraps both)
    }
    /// <summary>ui-chat-window (#468): typed characters (WM_CHAR, after IME composition) go to the native text input that takes them.</summary>
    bool NativeUiChar(char c){
        if(!NativeUiActive)return false;
        foreach(var p in nativeUiRoot!.HitOrder()){
            if(p is not IUiTextInput t||!nativeUiRoot.Usable(p))continue;
            try{if(t.Char(c)){Invalidate();return true;}}catch(Exception ex){nativeUiRoot.Fault(p,"Char",ex);}
        }
        return false;
    }

    /// <summary>
    /// R3-A (#678): the four legacy windows read the existing models only. Outside the world every snapshot is empty, so the
    /// windows stay closed. The class for the spell level buttons comes from the character list (same source as WithBuffState).
    /// </summary>
    IUiGameState NativeLegacyWindowState(IUiGameState basic,bool inWorld){
        if(!inWorld)return new UiPledgeGameState(new UiWarehouseGameState(new UiSpellWindowGameState(new UiItemWindowGameState(basic,new UiItemWindowData(null)),new UiSpellWindowData(null,null)),null),null);
        var w=login.World;
        int? cls=login.Characters.FirstOrDefault(c=>c.Name==w.Actor?.Name)?.Class;
        IUiGameState s=new UiItemWindowGameState(basic,UiItemWindowData.From(w));
        s=new UiSpellWindowGameState(s,UiSpellWindowData.From(w,cls));
        s=new UiWarehouseGameState(s,UiWarehouseView.From(w));
        return new UiPledgeGameState(s,w.PledgeWindow,w.ClanAttentionNames);
    }

    /// <summary>
    /// R3-A (#678): while the native UI owns the warehouse (window registered and not faulted), the S176 / deposit list opens
    /// WAREHOUSE_WINDOW and the WinForms NPC list (NpcUi.cs) stays out of the way. Native UI off → the WinForms list as before.
    /// </summary>
    internal bool NativeWarehouseActive(WorldSession w)=>NativeUiActive&&w.InteractionKind is "withdraw" or "deposit"
        &&nativeUiRoot?.Find(WarehouseWindowPanel.WindowId) is {} p&&!nativeUiRoot.Faulted.Contains(p);

    /// <summary>Dev-tools page by tab title ("背包", "魔法", "NPC", …); unknown or null → the window as it is.</summary>
    void ShowDevToolsPage(string? title){
        // R4-A (#705): there is no 「信件」 tab; the mail entry is the mail dialog (MailUi.cs), never the bare dev-tools window.
        if(title=="信件"&&RequestMail())return;
        Control? target=null;
        if(title!=null&&normalPanels?.TabPages.Cast<TabPage>().FirstOrDefault(p=>string.Equals(p.Text,title,StringComparison.OrdinalIgnoreCase)) is {} tab){normalPanels.SelectedTab=tab;target=normalPanels;}
        // R4-A (#705): a page hint without a tab (「小地圖」, 「血盟」, 「選項」, …) opens the window as it is; recorded as 待驗證.
        else if(title!=null)events.Add(new{kind="native-ui-menu",page=title,status="待驗證：開發工具沒有這個分頁，只開出視窗本身"});
        ShowDevTools(target);
    }

    // R4-A (#705): menu 5／6 and Ctrl+H／V／C. The Text.pak page (dialogs/templates.json, same key as the original HTML name) is
    // shown as a self query page: HYPER_TEXT_WINDOW when the native UI has it (R4-I; R5-A #733: no dev tools then), otherwise
    // the NPC panel (dev-tools 「NPC」 tab); links and
    // actions stay disabled there (ShowQueryPage, 待驗證). Same page shown again → closed (the original toggles, FU4§1.1).
    // The page replaces a query page already shown (one HYPER_TEXT_WINDOW); an open NPC dialog / shop is not touched.
    string? menuLocalPage;
    /// <summary>
    /// R5-A (#733, gap-matrix 14.2 item 4): dev-tools tab to open with a menu page. The native page window usable (native UI
    /// active, HYPER_TEXT_WINDOW registered and not faulted) → null, nothing extra opens; otherwise the 「NPC」 tab (legacy path).
    /// </summary>
    internal static string? MenuLocalPageDevTools(bool nativePageWindow)=>nativePageWindow?null:"NPC";
    internal bool ToggleMenuLocalPage(MenuLocalPage page){
        if(login.State!=LoginState.InWorld)return false;
        var w=login.World;
        if(w.DialogTemplates==null){events.Add(new{kind="native-ui-menu",page=page.ToString(),status="待驗證：dialogs/templates.json 沒有載入"});return false;}
        int? cls=login.Characters.FirstOrDefault(c=>c.Name==w.Actor?.Name)?.Class;
        string key=MenuButtonsPanel.PageKey(page,cls);
        ClearInput("native-ui-menu-page");
        if(w.QueryVisible&&menuLocalPage==key&&w.QueryHtml==key){w.CloseQueryPage();menuLocalPage=null;}
        else{
            w.ShowQueryPage(w.Actor?.ObjectId??0,key,[],[]);menuLocalPage=key;
            // R5-A (#733): with the native HYPER_TEXT_WINDOW the page shows there (SyncNativeHyperText); dev tools only without it.
            if(MenuLocalPageDevTools(NativeHyperText()!=null) is {} tab)ShowDevToolsPage(tab);
        }
        SyncInteraction();lastWebState="";PublishWebState();
        events.Add(new{kind="native-ui-menu",page=key,shown=w.QueryVisible&&w.QueryHtml==key});
        return true;
    }

    // R4-A (#705): Ctrl+P. Saves the composed 800×600 logical frame (native canvas when active, see CaptureLogicalScene) as PNG
    // under <stage>/runtime/Capture; the game folder is never written. The original's chat line "Saved current screen to %s"
    // is not shown (待驗證); the path goes to the event log.
    internal bool CaptureMenuScreen(){
        try{
            string path=MenuButtonsPanel.CapturePath(stageRoot,DateTime.Now,File.Exists);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using(var bitmap=CaptureLogicalScene())bitmap.Save(path,ImageFormat.Png);
            events.Add(new{kind="native-ui-capture",path});
            return true;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException or ArgumentException){
            events.Add(new{kind="native-ui-capture",error=ex.Message});
            return false;
        }
    }

    // R3-A (#678): menu action 7 / UserPlayStatsUI. The panel's death source (Recovery.cs) stays in charge while dead; the
    // menu adds an "opened by hand" flag on top (TW13 sub_77F490 toggles CUserPlayStatsUI). Stat values are still "—".
    bool menuPlayStats;
    void AttachNativeMenuPlayStats(UiRoot root){
        menuPlayStats=false;
        if(root.Find(UserPlayStatsPanel.PanelId) is UserPlayStatsPanel stats&&stats.Source is not MenuPlayStatsSource)
            stats.Source=new MenuPlayStatsSource(this,stats.Source??new UserPlayStatsSource(this));
    }
    bool ToggleMenuPlayStats(){
        if(nativeUiRoot?.Find(UserPlayStatsPanel.PanelId) is not UserPlayStatsPanel {Source:MenuPlayStatsSource src} stats||nativeUiRoot.Faulted.Contains(stats))return false;
        if(login.State!=LoginState.InWorld)return false;
        if(src.Current!=null)src.Dismiss();else menuPlayStats=true;
        return true;
    }
    sealed class MenuPlayStatsSource(ClientForm form,IUserPlayStatsSource death):IUserPlayStatsSource {
        public UserPlayStatsView? Current=>death.Current??(form.menuPlayStats&&form.login.State==LoginState.InWorld?new(form.login.CanRestart):null);
        public void Restart(){form.menuPlayStats=false;death.Restart();}
        public void Quit(){form.menuPlayStats=false;death.Quit();}
        public void Dismiss(){bool dying=death.Current!=null;form.menuPlayStats=false;if(dying)death.Dismiss();}
    }

    sealed class NativeUiCommands(ClientForm form):IUiChatCommands,IUiPartyCommands,IUiWarehouseCommands,IUiMenuCommands,IUiMenuWindowCommands,IUiLoginCommands {
        // T352 (#815, R7-M): LoginUI Exit_Button → the normal window close (FormClosing / FormClosed: audio shutdown, login.Dispose).
        // BeginInvoke: the click is still being dispatched by UiInput when the form starts closing.
        public void ExitClient(){
            form.ClearInput("native-ui-exit");form.events.Add(new{kind="native-ui-login",action="exit"});
            form.BeginInvoke(()=>{if(!form.IsDisposed)form.Close();});
        }
        // R4-A (#705): menu 1 → mail dialog; 5／6 and Ctrl+H／V／C → local HTML page; Ctrl+P → screenshot.
        public bool OpenMail()=>form.RequestMail();
        public bool ToggleLocalPage(MenuLocalPage page)=>form.ToggleMenuLocalPage(page);
        public bool CaptureScreen()=>form.CaptureMenuScreen();
        // R3-A (#678): the page hint is no longer dropped; it selects the matching dev-tools service tab.
        public void ShowDevTools(string? page=null){form.ClearInput("native-ui-dev-tools");form.ShowDevToolsPage(page);}
        // R3-A (#678): WAREHOUSE_WINDOW OK / exit go through the existing NPC flow (RunInteraction → LoginConnection.Interact:
        // ServicePackets.Warehouse = C_RESULT type 2 deposit / 3 withdraw, same bytes as the WinForms list). Stale revision → nothing.
        public void SubmitWarehouse(int revision,IReadOnlyList<ShopChoice> choices){
            var w=form.login.World;
            if(revision!=w.InteractionRevision||choices.Count==0)return;
            _=form.RunInteraction(new NpcWebCommand("shop",revision,w.WorldEpoch,w.TargetRevision,0,choices.ToArray()));
        }
        public void CancelWarehouse(int revision){
            var w=form.login.World;
            if(revision!=w.InteractionRevision)return;
            _=form.RunInteraction(new NpcWebCommand("close",revision,w.WorldEpoch,w.TargetRevision,0,[]));
        }
        public bool TogglePlayStats()=>form.ToggleMenuPlayStats();
        // ui-party (#471): existing PartyAction (ClearInput + SendParty), mode 0 like the legacy drawer. Implemented here (not as a
        // wrapper) so UiChatGameState.Commands, which only unwraps UiMacroCommands, still finds IUiChatCommands.
        public void PartyAction(string command,PartyView shown,string name)=>_=form.PartyAction(command,shown,name,0);
        public void ReturnGameFocus()=>form.ReturnGameFocus();
        public void Logout(){form.ClearInput("native-ui-logout");form.CancelLogin(form.login.SessionGeneration,false);form.SyncPlayerHud();}
        // ui-chat-window (#468): the same rules and calls as OpenPlayerChat / ExitPlayerChat / ChatSubmitRequested.
        public bool BeginChatInput(){
            var current=form.login.PlayerChatSnapshot();
            if(!PlayerChatFlowRules.CanOpen(form.normalProduct,form.login.State,Form.ActiveForm==form,true,current.CanSend))return false;
            form.ClearInput("chat-open");form.playerHudHeld.Clear();form.LeaveGameFocus("chat-open");return true;
        }
        public void EndChatInput(){form.playerHudHeld.Clear();form.ReturnGameFocus();}
        public async Task<bool> SendChat(string text,PlayerChatView displayed){
            var current=form.login.PlayerChatSnapshot();
            if(current.Generation!=displayed.Generation||current.WorldEpoch!=displayed.WorldEpoch||current.Actor!=displayed.Actor)return false;
            form.ClearInput("player-chat-send");
            return await form.login.SendPlayerChat(text,current.Generation,current.WorldEpoch,current.Actor);
        }
    }
}
