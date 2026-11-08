using P06.NativeUi;
namespace P06;

// Owned native window: no changes to the shared WebView command dispatcher are required.
public sealed class BoardUi:Form {
    readonly LoginConnection connection;
    readonly ListBox rows=new(){Dock=DockStyle.Fill,IntegralHeight=false};
    readonly TextBox article=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    readonly TextBox title=new(){Dock=DockStyle.Fill,MaxLength=16};
    readonly TextBox content=new(){Dock=DockStyle.Fill,Multiline=true,MaxLength=1000,ScrollBars=ScrollBars.Vertical,AcceptsReturn=true};
    readonly Label notice=new(){Dock=DockStyle.Fill,AutoSize=true};
    readonly Button refresh=new(){Text="刷新首頁",AutoSize=true},previous=new(){Text="上一頁",AutoSize=true},next=new(){Text="下一頁",AutoSize=true},read=new(){Text="閱讀",AutoSize=true},write=new(){Text="發文一次",AutoSize=true},delete=new(){Text="刪除此文",AutoSize=true};
    readonly System.Windows.Forms.Timer timer=new(){Interval=150};
    BoardView displayed;bool busy;string validationNotice="";readonly int openedGeneration,openedEpoch,openedTarget,openedRevision;readonly uint openedBoard;
    public BoardUi(LoginConnection connection){
        this.connection=connection;displayed=connection.BoardSnapshot();openedGeneration=displayed.Generation;
        openedEpoch=displayed.Epoch;openedTarget=displayed.TargetRevision;openedBoard=displayed.ObjectId;openedRevision=displayed.OpenRevision;
        Text="公告板";Size=new(740,680);MinimumSize=new(600,560);StartPosition=FormStartPosition.CenterParent;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Padding=new(10)};
        foreach(var size in new[]{30f,22f,24f,5f,16f,34f,60f})layout.RowStyles.Add(new(size is 30 or 34 or 60?SizeType.Absolute:SizeType.Percent,size));
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};actions.Controls.AddRange([refresh,previous,next,read,delete]);
        layout.Controls.Add(actions,0,0);layout.Controls.Add(rows,0,1);layout.Controls.Add(article,0,2);
        title.PlaceholderText="標題（最多 16 字）";content.PlaceholderText="內容（最多 1000 字）";
        layout.Controls.Add(title,0,3);layout.Controls.Add(content,0,4);layout.Controls.Add(write,0,5);layout.Controls.Add(notice,0,6);Controls.Add(layout);
        refresh.Click+=async(_,_)=>await Run("refresh");previous.Click+=async(_,_)=>await Run("previous");next.Click+=async(_,_)=>await Run("next");
        read.Click+=async(_,_)=>await Run("read");rows.DoubleClick+=async(_,_)=>{if(read.Enabled)await Run("read");};
        write.Click+=async(_,_)=>await ConfirmWrite();delete.Click+=async(_,_)=>await ConfirmDelete();
        timer.Tick+=(_,_)=>Sync();Shown+=(_,_)=>{Sync();timer.Start();};
        FormClosed+=(_,_)=>{timer.Dispose();connection.CloseBoard(displayed);};Sync();
    }
    async Task ConfirmWrite(){if(MessageBox.Show(this,$"送出這篇文章？費用 {displayed.Fee} 金幣。","確認發文",MessageBoxButtons.OKCancel)==DialogResult.OK)await Run("write");}
    async Task ConfirmDelete(){if(MessageBox.Show(this,"刪除目前閱讀的文章？","確認刪文",MessageBoxButtons.OKCancel)==DialogResult.OK)await Run("delete");}
    /// <summary>Snapshot shown by this window (null once closed); read by the native board panels (M7-U).</summary>
    internal BoardView? Displayed=>IsDisposed?null:displayed;
    /// <summary>
    /// Native board button (M7-U) → the same action as this window's button, with the same enabled rule and confirmations.
    /// Read uses the row selected here: the native windows draw no rows (row positions are unverified).
    /// </summary>
    internal async Task Press(string command){
        if(IsDisposed)return;Sync();if(IsDisposed)return;
        switch(command){
            case "close":Close();break;
            case "read" when read.Enabled:await Run("read");break;
            case "write" when write.Enabled:await ConfirmWrite();break;
            case "delete" when delete.Enabled:await ConfirmDelete();break;
            case "previous" when previous.Enabled:await Run("previous");break;
            case "next" when next.Enabled:await Run("next");break;
        }
    }
    async Task Run(string command){
        if(busy)return;var stamp=displayed;uint topic=command=="delete"?stamp.Article?.Id??0:(rows.SelectedItem as RowItem)?.Row.Id??0;
        validationNotice="";busy=true;Sync();if(IsDisposed)return;
        bool sent=await connection.SendBoard(command,stamp,topic,title.Text,content.Text);busy=false;
        if(IsDisposed)return;Sync();if(!sent){validationNotice="未送出：操作已失效，或文字不符合長度／Big5 編碼規則。";Sync();}
    }
    void Sync(){
        var v=connection.BoardSnapshot();
        if(v.Generation!=openedGeneration||v.Epoch!=openedEpoch||v.TargetRevision!=openedTarget||v.OpenRevision!=openedRevision||v.ObjectId!=openedBoard||v.Kind=="closed") {Close();return;}
        bool changed=v.Revision!=displayed.Revision;
        if(changed)validationNotice="";
        if(changed||rows.Items.Count!=v.Rows.Length){uint? selected=(rows.SelectedItem as RowItem)?.Row.Id;rows.Items.Clear();foreach(var row in v.Rows){int i=rows.Items.Add(new RowItem(row));if(row.Id==selected)rows.SelectedIndex=i;}}
        displayed=v;notice.Text=(validationNotice.Length>0?validationNotice:v.Notice)+(connection.World.LastServerMessage is {} message?"\n伺服器："+message:"");
        article.Text=v.Article is {} a?$"{a.Title}\r\n作者：{a.Author}　日期：{a.Date}\r\n\r\n{a.Content}":"";
        bool ready=v.CanAct&&!busy;refresh.Enabled=ready;previous.Enabled=ready&&v.Kind is "list" or "article"&&v.CanPrevious;next.Enabled=ready&&v.Kind is "list" or "article"&&v.CanNext;
        read.Enabled=ready&&v.Kind is "list" or "article"&&v.Rows.Length>0;
        write.Enabled=ready&&v.Kind is "list" or "article";write.Text=$"發文一次（{v.Fee} 金幣）";
        delete.Enabled=ready&&v.Kind=="article"&&v.Article?.Author==connection.World.Actor?.Name;
    }
    sealed record RowItem(BoardRow Row){public override string ToString()=>$"{Row.Id}　{Row.Title}　{Row.Author}　{Row.Date}";}
}

public sealed partial class LoginConnection {
    async Task<bool> OpenBoardUi(Func<bool>? pointerCurrent){
        if(!CanTalk||pointerCurrent?.Invoke()==false)return false;
        World.BeginBoard();var stamp=BoardSnapshot();
        bool sent=await SendBoard("refresh",stamp,pointerCurrent:pointerCurrent);
        if(sent&&stamp.Generation==generation&&World.SameBoardWindow(stamp)){
            // The offline runner has no ClientForm and never constructs/shows a window.
            var owner=Application.OpenForms.OfType<ClientForm>().FirstOrDefault(form=>form.OwnsBoardConnection(this));
            owner?.ShowBoardUi(this);
        }
        return sent;
    }
}
public sealed partial class ClientForm {
    internal bool OwnsBoardConnection(LoginConnection connection)=>ReferenceEquals(login,connection);
    internal void ShowBoardUi(LoginConnection connection){
        ClearInput("board");LeaveGameFocus("board");
        foreach(var old in OwnedForms.OfType<BoardUi>().ToArray())old.Close();
        var ui=new BoardUi(connection);ui.Show(this);
        AttachNativeBoard(ui);
    }

    // M7-U: with the native UI active, the original board windows (verified buttons only) are drawn on the 800×600 canvas and
    // drive the same BoardUi actions. Rows, article text and writing stay in BoardUi (transitional, not original behaviour)
    // until their positions are verified. Switch off / native inactive → nothing changes.
    BoardPanelSet? nativeBoard;readonly BoardPanelHost nativeBoardHost=new();
    void AttachNativeBoard(BoardUi ui){
        nativeBoardHost.Current=ui;
        if(!NativeUiActive||NativeUiRoot is not {Resources:{} resources} root)return;
        try{
            if(nativeBoard==null){
                var set=new BoardPanelSet(nativeBoardHost);set.LoadSurfaces(resources.Surfs);
                foreach(var p in set.Panels)root.Add(p);
                nativeBoard=set;
            }
            root.BringToFront(nativeBoard.List);root.BringToFront(nativeBoard.Read);Invalidate();
        }catch(Exception ex) when(ex is UiResourceException or UiLayoutException or InvalidOperationException){
            UiResourceLog.Write("board native panels skipped: "+ex.Message);
        }
    }
    sealed class BoardPanelHost:IBoardPanelHost {
        public BoardUi? Current;
        public BoardView? Board=>Current?.Displayed;
        public void Press(string command){if(Current is {IsDisposed:false} ui)_=ui.Press(command);}
    }
}
