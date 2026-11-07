namespace P06;

public sealed class BookmarkPanel:UserControl {
    readonly ListBox list=new(){Dock=DockStyle.Fill,DisplayMember=nameof(Bookmark.Name),AccessibleName="記憶座標清單"};
    readonly TextBox name=new(){Dock=DockStyle.Top,PlaceholderText="新書籤名稱",AccessibleName="新書籤名稱",MaxLength=64};
    readonly Label detail=new(){Dock=DockStyle.Top,Height=48,ForeColor=Color.White};
    readonly Label notice=new(){Dock=DockStyle.Bottom,Height=72,ForeColor=Color.White};
    readonly Button add=new(){Dock=DockStyle.Top,Height=34,Text="記憶目前位置"};
    readonly Button delete=new(){Dock=DockStyle.Bottom,Height=34,Text="刪除選取書籤"};
    BookmarkView? displayed;
    string feedback="";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<BookmarkView,string?,uint?,Task<bool>>? Send {get;set;}
    public BookmarkPanel(){
        Dock=DockStyle.Fill;BackColor=Color.FromArgb(23,38,41);Padding=new(8);
        Controls.AddRange([list,detail,add,name,delete,notice]);
        list.SelectedIndexChanged+=(_,_)=>Selection();
        name.TextChanged+=(_,_)=>feedback="";
        add.Click+=async(_,_)=>await Submit(false);
        delete.Click+=async(_,_)=>await Submit(true);
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
    BookmarkPanel? bookmarkPanel;
    // Form lifecycle provides a local hook without changing shared HUD or MagicUi dispatch.
    // OnLoad belongs to BuddyUi and OnShown to LocationShare; Form raises OnLoad from inside this override.
    protected override void OnCreateControl(){
        base.OnCreateControl();
        if(!normalProduct||normalPanels==null||bookmarkPanel!=null)return;
        bookmarkPanel=new BookmarkPanel();var page=new TabPage("記憶座標");page.Controls.Add(bookmarkPanel);normalPanels.TabPages.Add(page);
        bookmarkPanel.Enter+=(_,_)=>LeaveGameFocus("bookmarks");
        bookmarkPanel.Send=async(view,name,id)=>{
            bool walking=World.Walking;ClearInput("bookmarks");
            bool sent=!walking&&await login.SendBookmark(view,name,id);
            if(!IsDisposed)SyncBookmarks();return sent;
        };
        timer.Tick+=BookmarkTick;Disposed+=(_,_)=>timer.Tick-=BookmarkTick;SyncBookmarks();
    }
    void BookmarkTick(object? sender,EventArgs e)=>SyncBookmarks();
    void SyncBookmarks(){if(bookmarkPanel is {} panel&&!panel.IsDisposed){var view=login.BookmarkSnapshot();panel.UpdateView(view with{CanEdit=view.CanEdit&&!World.Walking});}}
}
