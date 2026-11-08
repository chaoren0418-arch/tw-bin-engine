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
        var page=new TabPage(title){BackColor=Color.FromArgb(23,38,41),AutoScroll=true};
        content.Dock=DockStyle.Fill;page.Controls.Add(content);pages.TabPages.Add(page);
    }
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

public sealed partial class ClientForm {
    DevToolsForm? devTools;
    internal static bool IsDevToolsKey(Keys keyData,bool normal)=>
        (keyData&Keys.Modifiers)==(Keys.Control|Keys.Shift)&&
        ((keyData&Keys.KeyCode) is Keys.F9 or Keys.F10 or Keys.F11 or Keys.F12||
        !normal&&(keyData&Keys.KeyCode) is Keys.F2 or Keys.F3 or Keys.F6 or Keys.F7 or Keys.F8);
    bool ProcessDevToolsKey(Keys keyData,bool repeat){
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
        // Diagnostic-only controls also belong to the tool window, not the scene.
        foreach(Control control in Controls.Cast<Control>().Where(c=>c!=playerHud).ToArray())devTools.AddPage(control.AccessibleName??control.GetType().Name,control);
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
