using P06.NativeUi;
namespace P06;

// R3-C（T279／#672）：血盟倉庫紀錄的畫面，以及料理窗原生面板的接線（共用同一個 timer tick）。
// 原版收到非空 S250/117 時開 sub_A559D0 的視窗（WIN2§7）。T288（#689）：原生 UI 開著時開原生窗
// NativeUi/ClanWarehouseHistoryPanel.cs（FU4§5 版面、字串 1880–1889）；原生 UI 沒開時仍用 WinForms 暫行清單。
// 0 筆由 WorldSession 顯示訊息 1889，不開窗。
// 掛點：ClientForm 的 OnLoad／OnShown／OnActivated／OnHandleCreated 等已被其他模組使用，本模組用 OnClientSizeChanged
// （建構子設定 ClientSize 時必定觸發）只做一次初始化；實際工作都在 timer tick（UI 執行緒）。

public sealed partial class ClientForm {
    bool cookAndHistoryHooked;
    ClanWarehouseHistoryInbox? clanWarehouseHistoryInbox;
    ClanWarehouseHistoryWindow? clanWarehouseHistoryWindow;

    protected override void OnClientSizeChanged(EventArgs e){
        InitCookAndHistoryUi();
        base.OnClientSizeChanged(e);
    }
    void InitCookAndHistoryUi(){
        if(cookAndHistoryHooked)return;
        cookAndHistoryHooked=true;
        clanWarehouseHistoryInbox=new(login.World);
        timer.Tick+=SyncCookAndHistory;
        FormClosed+=(_,_)=>{timer.Tick-=SyncCookAndHistory;clanWarehouseHistoryInbox?.Dispose();clanWarehouseHistoryWindow?.Dispose();};
    }
    void SyncCookAndHistory(object? sender,EventArgs e){
        AttachNativeCookBook();
        if(login.State!=LoginState.InWorld){
            clanWarehouseHistoryInbox?.Clear();
            if(clanWarehouseHistoryWindow is {IsDisposed:false,Visible:true} open)open.Hide();
            return;
        }
        if(clanWarehouseHistoryInbox?.Take() is not {} history)return;
        // T436（#994，G16）：面板已註冊且沒有故障才交給原生窗（比照 QuitFallbackUi）；故障時改開 WinForms 暫行清單。
        if(NativePanelActive(ClanWarehouseHistoryPanel.PanelId)&&NativeUiRoot?.Find(ClanWarehouseHistoryPanel.PanelId) is ClanWarehouseHistoryPanel panel){
            if(clanWarehouseHistoryWindow is {IsDisposed:false,Visible:true} legacy)legacy.Hide();
            panel.Show(new(ClanWarehouseHistoryText.Lines(history,ResolveNpcText),ClanWarehouseHistoryText.EmptyText));
            NativeUiRoot?.BringToFront(panel);
            return;
        }
        if(clanWarehouseHistoryWindow is not {IsDisposed:false} window)clanWarehouseHistoryWindow=window=new ClanWarehouseHistoryWindow();
        window.SetRows(ClanWarehouseHistoryView.Rows(history,ResolveNpcText));
        if(!window.Visible)window.Show(this);
        else window.BringToFront();
    }
}

/// <summary>暫行清單窗：只列原服送來的紀錄；不搶遊戲焦點（ShowWithoutActivation）。</summary>
public sealed class ClanWarehouseHistoryWindow:Form {
    readonly ListView list=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,GridLines=true,HeaderStyle=ColumnHeaderStyle.Nonclickable,
        BackColor=Color.FromArgb(23,38,41),ForeColor=Color.White};
    readonly Label footnote=new(){Dock=DockStyle.Bottom,Height=36,Text=ClanWarehouseHistoryView.Footnote,ForeColor=Color.Silver,Padding=new(6,4,6,4)};

    public ClanWarehouseHistoryWindow(){
        Text=ClanWarehouseHistoryView.Title;
        FormBorderStyle=FormBorderStyle.SizableToolWindow;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        ClientSize=new(520,320);BackColor=Color.FromArgb(23,38,41);ForeColor=Color.White;
        int[] widths=[110,70,170,70,90];
        for(int i=0;i<ClanWarehouseHistoryView.Columns.Length;i++)list.Columns.Add(ClanWarehouseHistoryView.Columns[i],widths[i]);
        Controls.Add(list);Controls.Add(footnote);
    }
    protected override bool ShowWithoutActivation=>true;

    /// <summary>以整份清單取代目前內容（原版每次收到 S250/117 都是整份）。</summary>
    public void SetRows(IReadOnlyList<ClanWarehouseHistoryRow> rows){
        list.BeginUpdate();
        try{
            list.Items.Clear();
            foreach(var r in rows)list.Items.Add(new ListViewItem([r.CharName,r.Type,r.ItemName,r.Count,r.When]));
        }finally{list.EndUpdate();}
        Text=$"{ClanWarehouseHistoryView.Title}（{rows.Count} 筆）";
    }
    internal int RowCount=>list.Items.Count;
    internal string Cell(int row,int column)=>list.Items[row].SubItems[column].Text;
}
