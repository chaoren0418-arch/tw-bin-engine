namespace P06;

public sealed class BuddyPanel:UserControl {
    readonly LoginConnection connection;
    readonly ListBox friends=new(){Dock=DockStyle.Fill,AccessibleName="好友與在線狀態"};
    readonly ListBox excluded=new(){Dock=DockStyle.Fill,AccessibleName="封鎖名單"};
    readonly TextBox name=new(){Dock=DockStyle.Fill,MaxLength=52,AccessibleName="名單角色名稱"};
    readonly Label notice=new(){Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=Color.White};
    readonly Button query=new(){Text="查詢好友",AutoSize=true};
    readonly Button add=new(){Text="新增好友",AutoSize=true};
    readonly Button remove=new(){Text="刪除好友",AutoSize=true};
    readonly Button toggle=new(){Text="封鎖／解除",AutoSize=true};
    BuddyView? displayed;bool updating;
    public BuddyPanel(LoginConnection connection){
        this.connection=connection;Dock=DockStyle.Fill;AccessibleName="好友與封鎖名單";
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=6,Padding=new(6)};
        layout.ColumnStyles.Add(new(SizeType.Percent,50));layout.ColumnStyles.Add(new(SizeType.Percent,50));
        foreach(var style in new[]{new RowStyle(SizeType.Absolute,26),new RowStyle(SizeType.Percent,100),new RowStyle(SizeType.Absolute,26),new RowStyle(SizeType.Absolute,30),new RowStyle(SizeType.Absolute,72),new RowStyle(SizeType.Absolute,90)})layout.RowStyles.Add(style);
        layout.Controls.Add(new Label{Text="好友（本次查詢狀態）",Dock=DockStyle.Fill,ForeColor=Color.White},0,0);
        layout.Controls.Add(new Label{Text="已封鎖",Dock=DockStyle.Fill,ForeColor=Color.White},1,0);
        layout.Controls.Add(friends,0,1);layout.Controls.Add(excluded,1,1);
        var label=new Label{Text="輸入名稱，或從名單選取角色",Dock=DockStyle.Fill,ForeColor=Color.White};layout.Controls.Add(label,0,2);layout.SetColumnSpan(label,2);
        layout.Controls.Add(name,0,3);layout.SetColumnSpan(name,2);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};actions.Controls.AddRange([query,add,remove,toggle]);layout.Controls.Add(actions,0,4);layout.SetColumnSpan(actions,2);
        layout.Controls.Add(notice,0,5);layout.SetColumnSpan(notice,2);Controls.Add(layout);
        query.Click+=async(_,_)=>await Submit(BuddyAction.Query);add.Click+=async(_,_)=>await Submit(BuddyAction.Add);
        remove.Click+=async(_,_)=>await Submit(BuddyAction.Delete);toggle.Click+=async(_,_)=>await Submit(BuddyAction.ToggleExclude);
        friends.SelectedIndexChanged+=(_,_)=>{if(!updating&&displayed is {} v&&friends.SelectedIndex>=0)name.Text=v.Friends[friends.SelectedIndex].Name;};
        excluded.SelectedIndexChanged+=(_,_)=>{if(!updating&&excluded.SelectedItem is string value)name.Text=value;};
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
            }
            displayed=next;notice.Text=(next.Loaded?(next.Stale?"好友清單待重新確認。\n":""):"尚未取得好友清單。\n")+next.Notice;UpdateButtons();
        }finally{updating=false;}
    }
    void UpdateButtons(){
        query.Enabled=displayed?.CanSend==true;bool named=!string.IsNullOrWhiteSpace(name.Text);
        add.Enabled=remove.Enabled=query.Enabled&&named;toggle.Enabled=displayed?.CanToggle==true&&named;
        toggle.Text=displayed?.Excluded.Contains(name.Text,StringComparer.OrdinalIgnoreCase)==true?"解除封鎖":"封鎖角色";
    }
    async Task Submit(BuddyAction action){
        if(displayed is not {} view)return;
        query.Enabled=add.Enabled=remove.Enabled=toggle.Enabled=false;
        try{await connection.SendBuddy(action,action==BuddyAction.Query?"":name.Text,view);}
        catch(ArgumentException ex){notice.Text=ex.Message;UpdateButtons();return;}
        if(!IsDisposed)RefreshState();
    }
    protected override void Dispose(bool disposing){if(disposing)connection.Changed-=RefreshState;base.Dispose(disposing);}
}

public sealed partial class ClientForm {
    protected override void OnLoad(EventArgs e){base.OnLoad(e);InitBuddyUi();}
    void InitBuddyUi(){
        if(normalPanels==null)return;
        var page=new TabPage("好友／封鎖"){BackColor=Color.FromArgb(23,38,41)};
        page.Controls.Add(new BuddyPanel(login));normalPanels.TabPages.Add(page);
    }
}
