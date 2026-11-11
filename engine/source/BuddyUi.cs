using P06.NativeUi;
namespace P06;

public sealed class BuddyPanel:UserControl {
    readonly LoginConnection connection;
    readonly Func<FriendListKind,bool>? toggleNative;
    readonly ListBox friends=new(){Dock=DockStyle.Fill,AccessibleName="好友與在線狀態"};
    readonly ListBox excluded=new(){Dock=DockStyle.Fill,AccessibleName="封鎖名單"};
    readonly ListBox mailExcluded=new(){Dock=DockStyle.Fill,AccessibleName="信件封鎖名單"};
    readonly TextBox name=new(){Dock=DockStyle.Fill,MaxLength=52,AccessibleName="名單角色名稱"};
    readonly Label notice=new(){Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=Color.White};
    readonly Button query=new(){Text="查詢好友",AutoSize=true};
    readonly Button add=new(){Text="新增好友",AutoSize=true};
    readonly Button remove=new(){Text="刪除好友",AutoSize=true};
    readonly Button toggle=new(){Text="封鎖／解除",AutoSize=true};
    readonly Button mailToggle=new(){Text="信件封鎖／解除",AutoSize=true};
    readonly Button[] nativeButtons;
    readonly Func<string,bool>? confirmDelete;
    BuddyView? displayed;bool updating;
    /// <param name="toggleNative">Opens/closes the native FriendUI／ExcludeUI／MailExcludeUI window; false = native UI not active.</param>
    /// <param name="confirmDelete">T434（#992，G11）：「刪除好友」送出前問 1813；回 false＝不送。null＝不問（舊行為）。</param>
    public BuddyPanel(LoginConnection connection,Func<FriendListKind,bool>? toggleNative=null,Func<string,bool>? confirmDelete=null){
        this.connection=connection;this.toggleNative=toggleNative;this.confirmDelete=confirmDelete;Dock=DockStyle.Fill;AccessibleName="好友與封鎖名單";
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=6,Padding=new(6)};
        for(int i=0;i<3;i++)layout.ColumnStyles.Add(new(SizeType.Percent,100f/3));
        foreach(var style in new[]{new RowStyle(SizeType.Absolute,26),new RowStyle(SizeType.Percent,100),new RowStyle(SizeType.Absolute,26),new RowStyle(SizeType.Absolute,30),new RowStyle(SizeType.Absolute,104),new RowStyle(SizeType.Absolute,90)})layout.RowStyles.Add(style);
        layout.Controls.Add(new Label{Text="好友（本次查詢狀態）",Dock=DockStyle.Fill,ForeColor=Color.White},0,0);
        layout.Controls.Add(new Label{Text="已封鎖",Dock=DockStyle.Fill,ForeColor=Color.White},1,0);
        layout.Controls.Add(new Label{Text="信件封鎖（僅本機）",Dock=DockStyle.Fill,ForeColor=Color.White},2,0);
        layout.Controls.Add(friends,0,1);layout.Controls.Add(excluded,1,1);layout.Controls.Add(mailExcluded,2,1);
        var label=new Label{Text="輸入名稱，或從名單選取角色",Dock=DockStyle.Fill,ForeColor=Color.White};layout.Controls.Add(label,0,2);layout.SetColumnSpan(label,3);
        layout.Controls.Add(name,0,3);layout.SetColumnSpan(name,3);
        nativeButtons=FriendWindowSpec.All.Select(s=>{
            var b=new Button{Text=s.Kind switch{FriendListKind.Friend=>"原生好友窗",FriendListKind.Exclude=>"原生封鎖窗",_=>"原生信件封鎖窗"},AutoSize=true,Tag=s.Kind};
            b.Click+=(_,_)=>{if(this.toggleNative?.Invoke(s.Kind)!=true)notice.Text="原生 UI 未啟用；請以 --native-ui 啟動後再開原生視窗。";};return b;
        }).ToArray();
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};actions.Controls.AddRange([query,add,remove,toggle,mailToggle,..nativeButtons]);layout.Controls.Add(actions,0,4);layout.SetColumnSpan(actions,3);
        layout.Controls.Add(notice,0,5);layout.SetColumnSpan(notice,3);Controls.Add(layout);
        query.Click+=async(_,_)=>await Submit(BuddyAction.Query);add.Click+=async(_,_)=>await Submit(BuddyAction.Add);
        remove.Click+=async(_,_)=>await Submit(BuddyAction.Delete);toggle.Click+=async(_,_)=>await Submit(BuddyAction.ToggleExclude);
        mailToggle.Click+=(_,_)=>ToggleMail();
        friends.SelectedIndexChanged+=(_,_)=>{if(!updating&&displayed is {} v&&friends.SelectedIndex>=0)name.Text=v.Friends[friends.SelectedIndex].Name;};
        excluded.SelectedIndexChanged+=(_,_)=>{if(!updating&&excluded.SelectedItem is string value)name.Text=value;};
        mailExcluded.SelectedIndexChanged+=(_,_)=>{if(!updating&&mailExcluded.SelectedItem is string value)name.Text=value;};
        name.TextChanged+=(_,_)=>UpdateButtons();VisibleChanged+=(_,_)=>RefreshState();
        connection.Changed+=RefreshState;RefreshState();
    }
    public void RefreshState(){
        if(IsDisposed)return;if(InvokeRequired){if(IsHandleCreated)BeginInvoke((Action)RefreshState);return;}
        var next=connection.BuddySnapshot();updating=true;
        try{
            if(displayed==null||displayed.Generation!=next.Generation||displayed.Actor!=next.Actor)name.Clear();
            if(displayed==null||displayed.Revision!=next.Revision||displayed.Generation!=next.Generation){
                friends.Items.Clear();friends.Items.AddRange(next.Friends.Select(f=>$"{f.Name}（{(f.Online?"在線":"離線")}）").ToArray());
                excluded.Items.Clear();excluded.Items.AddRange(next.Excluded);
                mailExcluded.Items.Clear();mailExcluded.Items.AddRange(next.MailExcluded);
            }
            displayed=next;notice.Text=(next.Loaded?(next.Stale?"好友清單待重新確認。\n":""):"尚未取得好友清單。\n")+next.Notice;UpdateButtons();
        }finally{updating=false;}
    }
    void UpdateButtons(){
        query.Enabled=displayed?.CanSend==true;bool named=!string.IsNullOrWhiteSpace(name.Text);
        add.Enabled=remove.Enabled=query.Enabled&&named;toggle.Enabled=displayed?.CanToggle==true&&named;
        toggle.Text=displayed?.Excluded.Contains(name.Text,StringComparer.OrdinalIgnoreCase)==true?"解除封鎖":"封鎖角色";
        mailToggle.Enabled=displayed?.Actor is >0&&named;
        mailToggle.Text=displayed?.MailExcluded.Contains(name.Text,StringComparer.OrdinalIgnoreCase)==true?"解除信件封鎖":"信件封鎖";
    }
    void ToggleMail(){
        if(displayed is not {} view)return;
        try{if(!connection.ToggleMailExclude(name.Text,view))notice.Text="目前無法變更信件封鎖（不在世界中或名單已更新），請再試一次。";}
        catch(ArgumentException ex){notice.Text=ex.Message;UpdateButtons();return;}
        if(!IsDisposed)RefreshState();
    }
    async Task Submit(BuddyAction action){
        if(displayed is not {} view)return;
        if(action==BuddyAction.Delete&&!ClassicConfirm.Allows(confirmDelete,FriendPanels.DeleteConfirmText))return;
        query.Enabled=add.Enabled=remove.Enabled=toggle.Enabled=false;
        try{await connection.SendBuddy(action,action==BuddyAction.Query?"":name.Text,view);}
        catch(ArgumentException ex){notice.Text=ex.Message;UpdateButtons();return;}
        if(!IsDisposed)RefreshState();
    }
    protected override void Dispose(bool disposing){if(disposing)connection.Changed-=RefreshState;base.Dispose(disposing);}
}

/// <summary>M7-V (#529): read-only adapter from the buddy model to the native FriendUI／ExcludeUI／MailExcludeUI windows.</summary>
public sealed class LoginFriendListSource(LoginConnection connection):IFriendListSource {
    public FriendListData Read(FriendListKind kind){
        var v=connection.BuddySnapshot();
        return new(kind switch{
            FriendListKind.Friend=>v.Friends.Select(f=>new FriendListRow(f.Name,f.Online)).ToArray(),
            FriendListKind.Exclude=>v.Excluded.Select(n=>new FriendListRow(n)).ToArray(),
            _=>v.MailExcluded.Select(n=>new FriendListRow(n)).ToArray(),
        });
    }
}

/// <summary>
/// R10-C（T387／#902）：原生 FriendUI／ExcludeUI 清單列 Delete 的送出端（FriendPanels.Host）。
/// Friend（1813 回答「是」之後）→ C202，送出後本地移除、不另查詢（FU9§9.2 sub_B18F10）；Exclude → C171（3.81 切換；名字在名單上＝解除）。
/// MailExclude 面板不會呼叫（3.81 不支援）。只做前置檢查並開始送出；結果以原服回覆為準。
/// </summary>
public sealed class LoginFriendWindowHost(LoginConnection connection):IFriendWindowHost {
    public bool Remove(FriendListKind kind,string name){
        if(kind==FriendListKind.MailExclude)return false;
        var view=connection.BuddySnapshot();
        if(kind==FriendListKind.Friend?!view.CanSend:!view.CanToggle)return false;
        var action=kind==FriendListKind.Friend?BuddyAction.Delete:BuddyAction.ToggleExclude;
        try{BuddyPackets.Encode(action,name);}catch(ArgumentException){return false;}
        var task=connection.SendBuddy(action,name,view,localRemove:kind==FriendListKind.Friend);
        if(task.IsCompleted)return task.Exception==null&&task.Result;
        _=task.ContinueWith(t=>_=t.Exception,TaskContinuationOptions.OnlyOnFaulted);   // SendBuddy 自己處理送出例外；這裡只避免未觀察的例外
        return true;
    }
}

/// <summary>
/// T434（#992，classic-ui-audit G11）：classic（WinForms）路徑上的 Y/N 確認——好友刪除 1813、收竿 1516。原生 UI 用聊天輸入列提示
/// （FriendDeletePromptPanel／FishingQuitPromptPanel）；classic 沒有原生畫布，所以用 WinForms MessageBox（是＝送出，否＝不送）。
/// 文字照字串表（FriendPanels.DeleteConfirmText／FishingQuitText.Confirm），與原生同一份。離線測試以 Prompt 換掉對話框。
/// </summary>
public static class ClassicConfirm {
    public const string Title="確認";
    /// <summary>測試用：不為 null 時取代 MessageBox（回 true＝是）。</summary>
    public static Func<string,bool>? Prompt {get;set;}
    /// <summary>問一次；Prompt 有設定就用它，否則開 MessageBox（owner 為 null 時不指定擁有者）。</summary>
    public static bool Ask(IWin32Window? owner,string text){
        if(Prompt is {} p)return p(text);
        return (owner==null?MessageBox.Show(text,Title,MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)
            :MessageBox.Show(owner,text,Title,MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2))==DialogResult.Yes;
    }
    /// <summary>confirm 為 null＝不問（放行）；否則照它的回答。</summary>
    public static bool Allows(Func<string,bool>? confirm,string text)=>confirm==null||confirm(text);
}

public sealed partial class ClientForm {
    protected override void OnLoad(EventArgs e){base.OnLoad(e);InitBuddyUi();}
    void InitBuddyUi(){
        FriendPanels.Source=new LoginFriendListSource(login);
        FriendPanels.Host=new LoginFriendWindowHost(login);   // R10-C（#902）：Delete → 1813 → C202／C171
        if(normalPanels==null)return;
        var page=new TabPage("好友／封鎖"){BackColor=Color.FromArgb(23,38,41)};
        // T434（#992，G11）：classic 的「刪除好友」照原生 FriendUI 先問 1813；原生 UI 開著時這顆過渡鈕照舊不問（原生窗自己會問）。
        page.Controls.Add(new BuddyPanel(login,ToggleNativeFriendWindow,text=>NativeUiActive||ClassicConfirm.Ask(this,text)));normalPanels.TabPages.Add(page);
    }
    /// <summary>
    /// Transitional opener. 已驗證：TW16 無開窗鍵（FU13§3.1）——原版沒有任何鍵或選單單獨打開 FriendUI；名單入口是 `/buddy`／HyperAct `buddy`，
    /// 回覆 S39 buddy 開 buddy-c.html 頁（Buddies.cs BuddyPage，R10-V #926）。這個 WinForms 鈕只是看原生三窗的過渡入口。false = native UI not active.
    /// </summary>
    internal bool ToggleNativeFriendWindow(FriendListKind kind){
        if(!NativeUiActive||NativeUiRoot?.Find(FriendWindowSpec.For(kind).Id) is not FriendPanel panel)return false;
        panel.Toggle();if(panel.Visible)NativeUiRoot.BringToFront(panel);
        Invalidate();return true;
    }
}
