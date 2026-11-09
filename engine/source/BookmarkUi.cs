namespace P06;

public sealed class BookmarkPanel:UserControl {
    readonly ListBox list=new(){Dock=DockStyle.Fill,DisplayMember=nameof(Bookmark.Name),AccessibleName="記憶座標清單"};
    readonly TextBox name=new(){Dock=DockStyle.Top,PlaceholderText="新書籤名稱",AccessibleName="新書籤名稱",MaxLength=64};
    readonly Label detail=new(){Dock=DockStyle.Top,Height=48,ForeColor=Color.White};
    readonly Label notice=new(){Dock=DockStyle.Bottom,Height=72,ForeColor=Color.White};
    readonly Button add=new(){Dock=DockStyle.Top,Height=34,Text="記憶目前位置"};
    readonly Button delete=new(){Dock=DockStyle.Bottom,Height=34,Text="刪除選取書籤"};
    // T276（#667）：選目的地模式（原版 CBookmarkWindowRenewal 的 sub_BDFE90(1) 模式，followups-3.md 第 3.1 節）。
    // 傳送控制戒指＋施放技能 5／69 時開啟：選書籤後按「傳送到此書籤」送一次 C6 HD；「取消」或 ESC 關閉、不送。
    // 這是既有 WinForms 書籤分頁的模式切換（原生 BookmarkWindowUI 窗尚未實作）；版面不是原版。
    readonly Label destination=new(){Dock=DockStyle.Top,Height=40,ForeColor=Color.Gold,Visible=false,Text="傳送控制戒指：請選擇傳送目的地（ESC 取消）"};
    readonly Button teleport=new(){Dock=DockStyle.Bottom,Height=34,Text="傳送到此書籤",Visible=false};
    readonly Button cancelTeleport=new(){Dock=DockStyle.Bottom,Height=34,Text="取消傳送",Visible=false};
    TeleportDestinationRequest? destinationRequest;bool teleportSending;
    BookmarkView? displayed;
    string feedback="";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<BookmarkView,string?,uint?,Task<bool>>? Send {get;set;}
    /// <summary>選目的地模式：送出 (請求 revision, 書籤 id)；回 true＝已送出。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<long,uint,Task<bool>>? Teleport {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<long>? CancelTeleport {get;set;}
    internal bool DestinationMode=>destinationRequest!=null;
    public BookmarkPanel(){
        Dock=DockStyle.Fill;BackColor=Color.FromArgb(23,38,41);Padding=new(8);
        Controls.AddRange([list,detail,add,name,delete,notice,teleport,cancelTeleport,destination]);
        list.SelectedIndexChanged+=(_,_)=>Selection();
        name.TextChanged+=(_,_)=>feedback="";
        add.Click+=async(_,_)=>await Submit(false);
        delete.Click+=async(_,_)=>await Submit(true);
        teleport.Click+=async(_,_)=>await SubmitTeleport();
        cancelTeleport.Click+=(_,_)=>Dismiss();
    }
    /// <summary>每次計時器同步：request＝目前的選目的地請求（null＝關閉模式）。</summary>
    public void UpdateDestination(TeleportDestinationRequest? request){
        if(request?.Revision==destinationRequest?.Revision&&(request==null)==(destinationRequest==null))return;
        destinationRequest=request;bool on=request!=null;
        destination.Visible=teleport.Visible=cancelTeleport.Visible=on;
        if(on){feedback="";if(CanFocus)list.Focus();}
        Selection();
    }
    void Dismiss(){if(destinationRequest is {} q){CancelTeleport?.Invoke(q.Revision);UpdateDestination(null);}}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
        if(keyData==Keys.Escape&&destinationRequest!=null){Dismiss();return true;}   // sub_BDAA10：ESC 關窗
        return base.ProcessCmdKey(ref msg,keyData);
    }
    async Task SubmitTeleport(){
        if(destinationRequest is not {} q||Teleport==null||teleportSending||list.SelectedItem is not Bookmark {DeletePending:false} row)return;
        teleportSending=true;teleport.Enabled=false;
        try{
            bool sent=await Teleport(q.Revision,row.Id);
            if(IsDisposed)return;
            notice.Text=feedback=sent?$"已以書籤「{row.Name}」施放傳送一次；結果以原服為準。":"未送出：技能、書籤或角色狀態不允許，請確認後再選一次。";
        }finally{teleportSending=false;if(!IsDisposed)Selection();}
    }
    public void UpdateView(BookmarkView view){
        var selected=(list.SelectedItem as Bookmark)?.Id;
        bool changed=displayed==null||displayed.Session!=view.Session||displayed.Epoch!=view.Epoch||displayed.Revision!=view.Revision;
        if(displayed?.Session!=view.Session||displayed?.ActorId!=view.ActorId){name.Clear();selected=null;}
        if(changed)feedback="";displayed=view;
        if(changed){list.BeginUpdate();list.Items.Clear();foreach(var row in view.Items)list.Items.Add(row);list.SelectedItem=view.Items.FirstOrDefault(row=>row.Id==selected);list.EndUpdate();}
        add.Enabled=view.CanEdit&&view.Items.Length<view.Capacity;name.Enabled=view.CanEdit;
        notice.Text=$"{view.Items.Length} / {view.Capacity} 筆\n{(feedback.Length>0?feedback:view.Notice)}";Selection();
    }
    void Selection(){
        var row=list.SelectedItem as Bookmark;
        detail.Text=row==null?"選取書籤可查看地圖與座標。":$"地圖 {row.MapId} · 座標 {row.X}, {row.Y}"+(row.DeletePending?"\n刪除已送出，待重新登入確認。":"");
        delete.Enabled=displayed?.CanEdit==true&&row is {DeletePending:false};
        teleport.Enabled=destinationRequest!=null&&!teleportSending&&row is {DeletePending:false};
    }
    async Task Submit(bool remove){
        if(displayed is not {} view||Send==null)return;
        uint? id=remove?(list.SelectedItem as Bookmark)?.Id:null;
        if(remove&&id==null)return;
        if(!remove&&BookmarkPackets.NameError(name.Text) is {} error){notice.Text=feedback=error;return;}
        add.Enabled=delete.Enabled=false;
        bool sent=await Send(view,remove?null:name.Text,id);
        if(IsDisposed)return;
        if(displayed?.Session!=view.Session||displayed?.Epoch!=view.Epoch)return;
        notice.Text=feedback=sent?remove?"刪除已送出，待重新登入確認。":"新增已送出，等待伺服器回覆。":"未送出：清單或角色狀態已改變，請重新選取。";
    }
}

public sealed partial class ClientForm {
    BookmarkPanel? bookmarkPanel;bool bookmarkUiHooked;
    // Form lifecycle provides a local hook without changing shared HUD or MagicUi dispatch.
    // OnLoad, OnCreateControl, OnHandleCreated, OnShown, OnLayout, OnVisibleChanged, SetVisibleCore and others belong to other partial modules.
    // The constructor sets ClientSize, so Resize always runs before Shown is raised; subscribe once here.
    protected override void OnResize(EventArgs e){
        if(!bookmarkUiHooked){bookmarkUiHooked=true;Shown+=(_,_)=>InitBookmarkUi();}
        base.OnResize(e);
    }
    void InitBookmarkUi(){
        if(!normalProduct||normalPanels==null||bookmarkPanel!=null)return;
        bookmarkPanel=new BookmarkPanel();var page=new TabPage("記憶座標");page.Controls.Add(bookmarkPanel);normalPanels.TabPages.Add(page);
        bookmarkPanel.Enter+=(_,_)=>LeaveGameFocus("bookmarks");
        bookmarkPanel.Send=async(view,name,id)=>{
            bool walking=World.Walking;ClearInput("bookmarks");
            bool sent=!walking&&await login.SendBookmark(view,name,id);
            if(!IsDisposed)SyncBookmarks();return sent;
        };
        // T276（#667）：選目的地模式與 674 鈕宿主（RingWindows.cs）。
        bookmarkPanel.Teleport=async(revision,id)=>{
            bool walking=World.Walking;ClearInput("bookmark-teleport");
            bool sent=!walking&&await login.SendTeleportDestination(revision,id);
            if(!IsDisposed)SyncBookmarks();return sent;
        };
        bookmarkPanel.CancelTeleport=revision=>login.CloseTeleportDestination(revision);
        timer.Tick+=BookmarkTick;Disposed+=(_,_)=>timer.Tick-=BookmarkTick;SyncBookmarks();
    }
    void BookmarkTick(object? sender,EventArgs e){SyncBookmarks();SyncRingWindows();}
    void SyncBookmarks(){if(bookmarkPanel is {} panel&&!panel.IsDisposed){var view=login.BookmarkSnapshot();panel.UpdateView(view with{CanEdit=view.CanEdit&&!World.Walking});}}
}
