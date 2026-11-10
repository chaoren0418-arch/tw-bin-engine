using P06.NativeUi;
namespace P06;

// R5-I（T314／#741）：原生 UI 關閉時的 QUIT_WINDOW 暫行窗（WinForms）。gap-matrix 13.6 建議、14.6 R5-I。
// 開窗點與原生窗相同：死亡逾時送 C122 後 LoginConnection.OpenQuitWindow（Recovery.cs；PBW§4，唯一已驗證的開窗點；
//   FU3§5.1：ESC／選單都不開，所以這裡也不加其他入口）。內容讀 LoginConnection.QuitWindowSnapshot。
// 三個鈕走與原生窗同一組動作（Recovery.cs 的 QuitWindowRestart／QuitWindowQuit／QuitWindowClose，依 QuitWindowRules）：
//   重新開始＝死亡 C177／存活 C7（選角）／逾時斷線後回登入；離開遊戲＝在世界中 C122 後關程式、斷線後直接關程式；取消＝只關窗。
// 顯示條件：沒有可用的原生 QUIT_WINDOW（原生 UI 關閉、面板沒註冊或已故障）且窗開著、且不是使用者關掉的那一次。
// 本窗是引擎的暫行畫面，不是原版外觀（原版底圖 1951 等只在原生 UI 下畫）。

/// <summary>暫行窗的文字與顯示條件（離線測試可直接驗）。</summary>
public static class QuitFallbackView {
    public const string Title="離開遊戲";
    public const string RestartText="重新開始",QuitText="離開遊戲",CancelText="取消";
    public const string Footnote="暫行視窗（原生介面關閉時使用）。";
    /// <summary>連線狀態的說明列：逾時斷線後重新開始＝回登入畫面（C177 已無法送出）。</summary>
    public static string Hint(QuitWindowView v)=>!v.InWorld?"連線已關閉：「重新開始」回到登入畫面，「離開遊戲」關閉程式。"
        :v.Dead?"角色已死亡：「重新開始」送出死亡後重新開始。":"「重新開始」回到選角畫面。";
    public static string Body(QuitWindowView v)=>(v.Message.Length>0?v.Message+"\n":"")+Hint(v);
    /// <summary>沒有可用的原生 QUIT_WINDOW、窗開著、且不是使用者關掉的那一次時才顯示。</summary>
    public static bool Wanted(bool nativeQuitPanel,QuitWindowView? view,int dismissedRevision)=>
        !nativeQuitPanel&&view!=null&&view.Revision!=dismissedRevision;
}

public sealed partial class ClientForm {
    QuitFallbackWindow? quitFallback;

    /// <summary>原生 QUIT_WINDOW 可用＝原生 UI 開啟、面板已註冊且沒有故障（與 AttachNativeRecovery 接 Source 的條件相同）。</summary>
    bool NativeQuitWindowActive=>NativeUiActive&&nativeUiRoot is {} root&&root.Find(QuitWindowPanel.PanelId) is QuitWindowPanel p&&!root.Faulted.Contains(p);

    /// <summary>由 SyncRecovery（死亡計時 timer，UI 執行緒）每 tick 呼叫。</summary>
    void SyncQuitFallback(){
        var view=login.QuitWindowSnapshot();
        var window=quitFallback is {IsDisposed:false} w?w:null;
        if(!QuitFallbackView.Wanted(NativeQuitWindowActive,view,window?.DismissedRevision??-1)){
            if(window is {Visible:true})window.Hide();
            return;
        }
        if(window==null){
            quitFallback=window=new QuitFallbackWindow();
            window.Restart=QuitWindowRestart;
            window.Quit=QuitWindowQuit;
            window.Dismissed=QuitWindowClose;
            FormClosed+=(_,_)=>quitFallback?.Dispose();
        }
        window.SetView(view!);
        if(!window.Visible)window.Show(this);
    }
}

/// <summary>QUIT_WINDOW 暫行窗：不搶遊戲焦點；標題列 X 與「取消」相同（只關窗）。</summary>
public sealed class QuitFallbackWindow:Form {
    static readonly Color Back=Color.FromArgb(40,32,32);
    readonly Label message=new(){Dock=DockStyle.Top,Height=64,ForeColor=Color.White,Padding=new(8,8,8,0)};
    readonly Label footnote=new(){Dock=DockStyle.Bottom,Height=22,Text=QuitFallbackView.Footnote,ForeColor=Color.Silver,Padding=new(8,2,8,2)};
    readonly Button restart=new(){Text=QuitFallbackView.RestartText,Size=new(118,26)};
    readonly Button quit=new(){Text=QuitFallbackView.QuitText,Size=new(118,26)};
    readonly Button cancel=new(){Text=QuitFallbackView.CancelText,Size=new(118,26)};

    /// <summary>目前顯示的 QUIT_WINDOW revision（-1＝還沒有）。</summary>
    public int Revision{get;private set;}=-1;
    /// <summary>使用者關掉的 revision；同一次的窗不再自動跳出。</summary>
    public int DismissedRevision{get;private set;}=-1;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? Restart{get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? Quit{get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? Dismissed{get;set;}

    public QuitFallbackWindow(){
        Text=QuitFallbackView.Title;
        FormBorderStyle=FormBorderStyle.FixedToolWindow;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MaximizeBox=false;MinimizeBox=false;
        ClientSize=new(300,190);BackColor=Back;ForeColor=Color.White;
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(88,4,0,0)};
        foreach(var b in new[]{restart,quit,cancel}){b.ForeColor=Color.Black;b.BackColor=SystemColors.Control;b.Margin=new(0,2,0,2);}
        buttons.Controls.AddRange([restart,quit,cancel]);
        Controls.Add(buttons);Controls.Add(message);Controls.Add(footnote);
        restart.Click+=(_,_)=>ClickRestart();
        quit.Click+=(_,_)=>ClickQuit();
        cancel.Click+=(_,_)=>Dismiss();
        KeyPreview=true;
        KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){e.Handled=true;Dismiss();}};   // 與原生窗相同：Esc＝取消
    }
    protected override bool ShowWithoutActivation=>true;

    /// <summary>標題列 X：不釋放視窗，只隱藏並關掉這一次的 QUIT_WINDOW（主視窗關閉時才真的關）。</summary>
    protected override void OnFormClosing(FormClosingEventArgs e){
        if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Dismiss();return;}
        base.OnFormClosing(e);
    }

    internal void Dismiss(){
        if(Revision>=0){DismissedRevision=Revision;Dismissed?.Invoke();}
        Hide();
    }

    /// <summary>以目前的 QUIT_WINDOW 內容更新（每 tick 呼叫；內容沒變時不重設文字）。</summary>
    public void SetView(QuitWindowView view){
        Revision=view.Revision;
        string body=QuitFallbackView.Body(view);
        if(message.Text!=body)message.Text=body;
    }
    internal string MessageText=>message.Text;
    // 按鈕處理（測試直接呼叫；PerformClick 在視窗沒顯示時不會觸發）。
    internal void ClickRestart(){if(Revision>=0)Restart?.Invoke();}
    internal void ClickQuit(){if(Revision>=0)Quit?.Invoke();}
    internal string[] ButtonTexts=>[restart.Text,quit.Text,cancel.Text];
}
