using System.Text.Json;
namespace P06;

// This window hosts transitional controls; it is not part of the original game UI.
public sealed class DevToolsForm : Form {
    readonly TabControl pages=new(){Dock=DockStyle.Fill};
    readonly string settingsPath;
    internal Func<Keys,bool,bool>? Command;
    internal Action? HiddenByUser;
    public DevToolsForm(string settingsPath) {
        this.settingsPath=settingsPath;
        Text="開發工具（過渡操作介面） · Ctrl+Shift+F9";
        AutoScaleMode=AutoScaleMode.None;ClientSize=new(800,640);MinimumSize=new(520,400);
        StartPosition=FormStartPosition.CenterParent;KeyPreview=true;
        Controls.Add(pages);
        try {
            if(File.Exists(settingsPath)){
                var bounds=JsonSerializer.Deserialize<Rectangle>(File.ReadAllText(settingsPath));
                if(bounds.Width>=MinimumSize.Width&&bounds.Height>=MinimumSize.Height&&Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(bounds))){StartPosition=FormStartPosition.Manual;Bounds=bounds;}
            }
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException){ }
    }
    public void AddPage(string title,Control content){
        var page=new DevToolsPage(title){BackColor=Color.FromArgb(23,38,41),AutoScroll=true};
        content.Dock=DockStyle.Fill;page.Controls.Add(content);pages.TabPages.Add(page);
    }
    /// <summary>分頁標題，依加入順序（#1163 離線檢查用）。</summary>
    internal string[] PageTitles=>pages.TabPages.Cast<TabPage>().Select(p=>p.Text).ToArray();
    public void SelectPage(Control content){
        if(content.Parent is TabPage page&&page.Parent==pages)pages.SelectedTab=page;
    }
    public void HideTools(){SaveBounds();Hide();HiddenByUser?.Invoke();}
    protected override void OnFormClosing(FormClosingEventArgs e){
        SaveBounds();
        if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideTools();}
        base.OnFormClosing(e);
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)=>
        Command?.Invoke(keyData,((long)msg.LParam&(1L<<30))!=0)==true||base.ProcessCmdKey(ref msg,keyData);
    public void SaveBounds(){
        var bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
        try{Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);File.WriteAllText(settingsPath,JsonSerializer.Serialize(bounds));}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){ }
    }
}

/// <summary>
/// #1163（#1150 G07）：開發工具的分頁。外觀、標題、內容與一般 TabPage 相同，只差在：
/// 其他執行緒（包括其他行程的 UIA 用戶端）用 WM_GETTEXT／WM_GETTEXTLENGTH 讀這個分頁 HWND 的視窗文字時，回空字串；
/// 本執行緒（WinForms 自己讀 Text、畫標籤）照舊拿到標題。
/// 根因（詳見 docs/reports/issue-1163.md）：.NET Framework 的 UIA 用戶端代理 WindowsTab.GetOverrideProviderForHwnd
/// 用分頁 HWND 的視窗文字比對標籤文字，配對到就把選取頁的 Pane 掛到代理的 TabItem(i) 底下；之後的兄弟節點改由代理產生，
/// 只剩 i+1 以後的 TabItem，而且代理 TabItem 的名稱取自 WinForms MSAA childId=i+1，在 .NET 10 會對到 GetChild(i)
/// （GetChild(0) 是選取頁 Pane），名稱比原生標籤早一格。文字比對不到時代理不介入，樹由 WinForms 原生 provider 提供：
/// 選取頁 Pane 之後依序是每一個分頁的 TabItem，名稱＝分頁標題、矩形＝原生標籤、Select 選到同一頁。
/// </summary>
sealed class DevToolsPage:TabPage {
    const int WM_GETTEXT=0x000D,WM_GETTEXTLENGTH=0x000E;
    public DevToolsPage(string title):base(title){AccessibleName=title;}
    /// <summary>InSendMessageEx 的結果非 ISMEX_NOSEND(0)＝訊息由其他執行緒送來，視窗文字不對外公開。</summary>
    internal static bool HidesWindowText(uint inSendMessageEx)=>inSendMessageEx!=0;
    protected override void WndProc(ref Message m){
        if((m.Msg is WM_GETTEXT or WM_GETTEXTLENGTH)&&HidesWindowText(InSendMessageEx(IntPtr.Zero))){
            if(m.Msg==WM_GETTEXT&&m.WParam!=IntPtr.Zero&&m.LParam!=IntPtr.Zero)System.Runtime.InteropServices.Marshal.WriteInt16(m.LParam,0);
            m.Result=IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint InSendMessageEx(IntPtr reserved);
}

public sealed partial class ClientForm {
    DevToolsForm? devTools;
    // T433（#991，classic-ui-audit G1）：屬於場景、不能被搬進開發工具的控制項（例如 WinForms 死亡窗與「死亡選單」鈕）。
    // InitDevTools 會把當時主視窗上的控制項都搬進開發工具分頁；登記在這裡的留在主視窗。
    readonly HashSet<Control> sceneResidentControls=[];
    void KeepOnScene(params Control[] controls){foreach(var c in controls)sceneResidentControls.Add(c);}
    /// <summary>G1：InitDevTools 要搬進開發工具的控制項＝主視窗上除了 HUD 與場景常駐控制項以外的全部（順序不變）。</summary>
    internal static Control[] DevToolsMovedControls(IEnumerable<Control> controls,Control? hud,IReadOnlySet<Control> sceneResident)=>
        controls.Where(c=>c!=hud&&!sceneResident.Contains(c)).ToArray();
    internal static bool IsDevToolsKey(Keys keyData,bool normal)=>
        (keyData&Keys.Modifiers)==(Keys.Control|Keys.Shift)&&
        ((keyData&Keys.KeyCode) is Keys.F9 or Keys.F10 or Keys.F11 or Keys.F12||
        !normal&&(keyData&Keys.KeyCode) is Keys.F2 or Keys.F3 or Keys.F6 or Keys.F7 or Keys.F8);
    /// <summary>
    /// T433（G6、G8）：--classic-ui（或原生 UI 沒有建立）時的選單快捷鍵：Ctrl+Q／Alt+X 統計窗、Ctrl+H 說明、Ctrl+V option1、
    /// Ctrl+C option2、Ctrl+P 截圖（同 MenuButtonsPanel.CommandHotkeys；Ctrl+A 狀態不在這裡，classic 由 HUD「角色」分頁負責）。
    /// 只在正常入口、原生 UI 不在、在世界中、而且焦點在遊戲畫面（不是聊天框或其他輸入框）時接手；原生 UI 在時一律 null，行為不變。
    /// </summary>
    internal static P06.NativeUi.MenuHotkeyCommand? ClassicMenuHotkey(Keys keyData,bool normal,bool nativeActive,bool gameFocused,bool inWorld)=>
        normal&&!nativeActive&&gameFocused&&inWorld&&P06.NativeUi.MenuButtonsPanel.CommandHotkeys.TryGetValue(keyData,out var command)
            &&command!=P06.NativeUi.MenuHotkeyCommand.Status?command:null;
    bool ProcessClassicMenuKey(Keys keyData,bool repeat){
        if(ClassicMenuHotkey(keyData,normalProduct,NativeUiActive,GameInputAllowed&&Focused,login.State==LoginState.InWorld) is not {} command)return false;
        if(repeat)return true;
        switch(command){
            case P06.NativeUi.MenuHotkeyCommand.PlayStats:ToggleClassicPlayStats();break;
            case P06.NativeUi.MenuHotkeyCommand.Help:ToggleMenuLocalPage(P06.NativeUi.MenuLocalPage.Help);break;
            case P06.NativeUi.MenuHotkeyCommand.Option1:ToggleMenuLocalPage(P06.NativeUi.MenuLocalPage.Option1);break;
            case P06.NativeUi.MenuHotkeyCommand.Option2:ToggleMenuLocalPage(P06.NativeUi.MenuLocalPage.Option2);break;
            case P06.NativeUi.MenuHotkeyCommand.Screenshot:CaptureMenuScreen();break;
        }
        events.Add(new{kind="classic-menu-hotkey",command=command.ToString()});
        return true;
    }
    /// <summary>T433（G6）：開發工具「NPC」分頁上的本機 HTML 頁按鈕（說明／選項 option1／血盟 option2）；同一頁再按一次關閉。</summary>
    internal static readonly IReadOnlyList<(string Text,P06.NativeUi.MenuLocalPage Page)> LocalPageButtons=[
        ("說明（Ctrl+H）",P06.NativeUi.MenuLocalPage.Help),("選項 option1（Ctrl+V）",P06.NativeUi.MenuLocalPage.Option1),("血盟 option2（Ctrl+C）",P06.NativeUi.MenuLocalPage.Option2)];
    void AddLocalPageButtons(){
        if(normalPanels?.TabPages.Cast<TabPage>().FirstOrDefault(p=>p.Text=="NPC") is not {} page)return;
        var row=new FlowLayoutPanel{Dock=DockStyle.Top,Height=34,WrapContents=false,AccessibleName="本機說明頁"};
        foreach(var (text,local) in LocalPageButtons){
            var button=new Button{Text=text,AutoSize=true,ForeColor=Color.White,BackColor=Color.FromArgb(42,74,80),FlatStyle=FlatStyle.Flat};
            button.Click+=(_,_)=>ToggleMenuLocalPage(local);
            row.Controls.Add(button);
        }
        page.Controls.Add(row);   // last added Dock=Top control docks first → the row sits above the NPC talk button.
    }
    /// <summary>
    /// T433（G2）：classic（原生 UI 不在）時，進入「不在世界中」的畫面（啟動、登出、斷線、選角）就自動打開開發工具的
    /// 「登入／選角」分頁一次；使用者關掉後，直到下一次離開世界才會再開。自動化情境（demoPort）與原生 UI 都不動。
    /// </summary>
    internal static bool ShouldRevealClassicLogin(bool enabled,bool nativeActive,bool loginScreen,bool wasLoginScreen)=>
        enabled&&!nativeActive&&loginScreen&&!wasLoginScreen;
    bool ProcessDevToolsKey(Keys keyData,bool repeat){
        if(ProcessClassicMenuKey(keyData,repeat))return true;
        if(!IsDevToolsKey(keyData,normalProduct))return false;
        if(repeat||webClosing)return true;
        switch(keyData&Keys.KeyCode){
            case Keys.F9:_=TogglePanel();break;
            case Keys.F10:ReturnGameFocus();break;
            case Keys.F11:_=FocusWebInput();break;
            case Keys.F12:CaptureEvidence("manual");break;
            case Keys.F2:
                if(OnlineMode)break;
                ClearInput("demo-building");buildingBack=!buildingBack;World.Teleport(buildingBack?32590:32589,32930);World.Face(5);Camera.Follow=true;break;
            case Keys.F3:
                if(OnlineMode)break;
                ClearInput("demo-tree");treeBack=!treeBack;World.Teleport(treeBack?32591:32589,treeBack?32938:32941);World.Face(5);Camera.Follow=true;break;
            default:
                if(!OnlineMode){var key=keyData&Keys.KeyCode;NetworkKey(key);Input.Up(key);}break;
        }
        return true;
    }
    void InitDevTools(){
        devTools=new DevToolsForm(Path.Combine(stageRoot,"runtime","dev-tools-window.json")){Owner=this,Command=(key,repeat)=>{
            if(ProcessDevToolsKey(key,repeat))return true;
            int disposition=LoginEnterDisposition(key,normalProduct,loginAccount.Focused||loginPassword.Focused,loginControl?.CanLogin==true,loginConnect.Enabled,repeat);
            if(disposition==2)loginConnect.PerformClick();
            return disposition!=0;
        },HiddenByUser=ReturnGameFocus};
        devTools.Activated+=(_,_)=>LeaveGameFocus("dev-tools");
        devTools.AddPage("網頁面板",webHost);
        if(normalPanels!=null)devTools.AddPage("服務",normalPanels);
        if(playerLoginStrip!=null)devTools.AddPage("登入／選角",playerLoginStrip);
        if(playerToolsStrip!=null)devTools.AddPage("控制",playerToolsStrip);
        devTools.AddPage("狀態列",bar);
        AddLocalPageButtons();
        // Diagnostic-only controls also belong to the tool window, not the scene (T433 G1: scene-resident controls stay).
        foreach(Control control in DevToolsMovedControls(Controls.Cast<Control>(),playerHud,sceneResidentControls))devTools.AddPage(control.AccessibleName??control.GetType().Name,control);
        Activated+=(_,_)=>{if(!webClosing)ReturnGameFocus();};
        FormClosed+=(_,_)=>{devTools.SaveBounds();devTools.Dispose();};
    }
    void ShowDevTools(Control? page=null){
        if(devTools==null||webClosing)return;
        if(page!=null)devTools.SelectPage(page);
        LeaveGameFocus("dev-tools-show");
        if(!devTools.Visible)devTools.Show(this);
        if(devTools.WindowState==FormWindowState.Minimized)devTools.WindowState=FormWindowState.Normal;
        devTools.Activate();
    }
}
