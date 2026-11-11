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
        // R7-F: a hidden window (native board) never gets a handle, so Close() only disposes it (no FormClosed).
        FormClosed+=(_,_)=>Finish();Disposed+=(_,_)=>Finish();Sync();
    }
    bool finished;
    void Finish(){if(finished)return;finished=true;timer.Dispose();connection.CloseBoard(displayed);}
    /// <summary>
    /// R7-F (#808): with the native board up, this window is never shown; it only keeps the snapshot and the actions
    /// (same rules, confirmations and checks) for the native panels. Owned by the client form so a new board closes it.
    /// </summary>
    internal void RunHidden(Form owner){Owner=owner;Sync();if(!IsDisposed)timer.Start();}
    /// <summary>Confirmation dialogs belong to the client form while this window is hidden.</summary>
    IWin32Window DialogOwner=>Visible||Owner==null?this:Owner;
    async Task ConfirmWrite(){if(MessageBox.Show(DialogOwner,$"送出這篇文章？費用 {displayed.Fee} 金幣。","確認發文",MessageBoxButtons.OKCancel)==DialogResult.OK)await Run("write");}
    async Task ConfirmDelete(){if(MessageBox.Show(DialogOwner,"刪除目前閱讀的文章？","確認刪文",MessageBoxButtons.OKCancel)==DialogResult.OK)await Run("delete");}
    /// <summary>Snapshot shown by this window (null once closed); read by the native board panels (M7-U).</summary>
    internal BoardView? Displayed=>IsDisposed?null:displayed;
    /// <summary>
    /// Native board button (M7-U) → the same action as this window's button, with the same enabled rule and confirmations.
    /// Read uses the row selected here; the native list (R5-G) changes this selection through SelectRow.
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
            case "article-previous":await Navigate(false);break;
            case "article-next":await Navigate(true);break;
        }
    }
    /// <summary>
    /// R9-N (#858): read-window 672 / 676 (BoardNavigation, followups-9 §3.2). Busy → nothing. The neighbour row becomes the
    /// selection and is read with the same read action; past the last row the next page is loaded first (C_BoardBack).
    /// Closing the read window is local to the native panels (BoardPanelSet.ShowList), so nothing is sent here.
    /// </summary>
    async Task Navigate(bool forward){
        if(busy)return;
        var (step,topic)=BoardNavigation.Article(displayed,forward);
        switch(step){
            case BoardArticleStep.Read when read.Enabled:SelectRow(topic);await Run("read",topic);break;
            case BoardArticleStep.NextPage when next.Enabled:await Run("next");break;
        }
    }
    /// <summary>R5-G: row selected in this window (the native list draws and changes the same selection).</summary>
    internal uint? SelectedRow=>IsDisposed?null:(rows.SelectedItem as RowItem)?.Row.Id;
    internal void SelectRow(uint? id){
        if(IsDisposed)return;
        if(id==null){rows.ClearSelected();return;}
        for(int i=0;i<rows.Items.Count;i++)if(rows.Items[i] is RowItem r&&r.Row.Id==id){rows.SelectedIndex=i;return;}
    }
    /// <summary>R5-G: native write window (686) → the same write action (same confirmation, length and Big5 checks).</summary>
    internal async Task Submit(string titleText,string contentText){
        if(IsDisposed)return;Sync();if(IsDisposed)return;
        title.Text=titleText;content.Text=contentText;
        if(write.Enabled)await ConfirmWrite();
    }
    /// <summary>R5-G: text the original prints as a system message (e.g. string 544); shown in this window's notice line.</summary>
    internal void Notice(string text){
        if(IsDisposed)return;validationNotice=text;
        // R7-F: hidden behind the native board → the notice line is not visible; the original prints it as a system line.
        if(!Visible)connection.World.AddBoardChatNotice(text,null);
        Sync();
    }
    /// <summary>R7-F: gold in the inventory (sub_7CDE00 compares it with the fee); null while the inventory is not loaded.</summary>
    internal long? Gold=>IsDisposed?null:ShopTrade.Holding(connection.World);
    /// <summary>R7-F: sub_7DC850 chat line (189 when short of gold).</summary>
    internal void ChatNotice(string text,int color){if(!IsDisposed)connection.World.AddBoardChatNotice(text,color);}
    /// <summary>R5-G: write is possible now (same rule as this window's write button).</summary>
    internal bool CanWrite{get{if(IsDisposed)return false;Sync();return !IsDisposed&&write.Enabled;}}
    async Task Run(string command,uint? explicitTopic=null){
        if(busy)return;var stamp=displayed;uint topic=explicitTopic??(command=="delete"?stamp.Article?.Id??0:(rows.SelectedItem as RowItem)?.Row.Id??0);
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
        ClearInput("board");
        foreach(var old in OwnedForms.OfType<BoardUi>().ToArray())old.Close();
        var ui=new BoardUi(connection);
        // R7-F (#808): native board attached → the WinForms window is not shown (it would duplicate the native windows);
        // it stays hidden as the owner of the actions. --classic-ui / native unavailable → the WinForms board as before.
        if(AttachNativeBoard(ui)){ui.RunHidden(this);return;}
        LeaveGameFocus("board");ui.Show(this);
    }

    // M7-U: with the native UI active, the original board windows (verified buttons) are drawn on the 800×600 canvas and
    // drive the same BoardUi actions. R5-G (#739): backgrounds, list rows, article text and the write window are drawn
    // natively too (NativeUi/BoardPanel.cs, disassembled layout). R7-F (#808): BoardUi is then kept hidden as the owner of
    // the actions; it is the whole board only when the native UI is off. Switch off / native inactive → nothing changes.
    BoardPanelSet? nativeBoard;BoardContentSet? nativeBoardContent;readonly BoardPanelHost nativeBoardHost=new();
    /// <summary>True when the native board panels are attached (and not faulted) for this board window.</summary>
    bool AttachNativeBoard(BoardUi ui){
        nativeBoardHost.Current=ui;
        if(!NativeUiActive||NativeUiRoot is not {Resources:{} resources} root)return false;
        try{
            if(nativeBoard==null){
                var set=new BoardPanelSet(nativeBoardHost);set.LoadSurfaces(resources.Surfs);
                var content=new BoardContentSet(set,nativeBoardHost);content.LoadSurfaces(resources.Surfs);
                // List/read content sit on the bar layer (below every window, so the button windows paint over them);
                // the write window is a window above the list buttons.
                root.Add(content.List);root.Add(content.Read);
                foreach(var p in set.Panels)root.Add(p);
                root.Add(content.Write);
                nativeBoard=set;nativeBoardContent=content;nativeBoardHost.Content=content;
            }
            nativeBoardContent?.Write.Close();
            root.BringToFront(nativeBoard.List);root.BringToFront(nativeBoard.Read);
            if(nativeBoardContent!=null)root.BringToFront(nativeBoardContent.Write);
            Invalidate();
            // A faulted list/read panel would leave no visible board → fall back to the WinForms window.
            return nativeBoardContent is {} c&&new IUiPanel[]{nativeBoard.List,nativeBoard.Read,c.List,c.Read}.All(p=>!root.Faulted.Contains(p));
        }catch(Exception ex) when(ex is UiResourceException or UiLayoutException or InvalidOperationException){
            UiResourceLog.Write("board native panels skipped: "+ex.Message);
            return false;
        }
    }
    sealed class BoardPanelHost:IBoardPanelHost,IBoardContentHost {
        public BoardUi? Current;
        public BoardContentSet? Content;
        public BoardView? Board=>Current?.Displayed;
        public void Press(string command){
            if(Current is not {IsDisposed:false} ui)return;
            // T426 (#970, FU15§2.11): while the list / read reply is pending ([this+0x40]≠0) row reads and 662 Write do nothing.
            if(command is "read" or "write"&&BoardPanelSet.IsWaiting(ui.Displayed))return;
            // Original 662 (sub_7CDE00) opens the write window; the text is sent from its 686 button.
            if(command=="write"&&Content!=null){if(ui.CanWrite)Content.OpenWrite();return;}
            _=ui.Press(command);
        }
        public uint? SelectedRow=>Current is {IsDisposed:false} ui?ui.SelectedRow:null;
        /// <summary>T426 (#970, FU15§2.11): sub_7CD080 swallows list clicks while waiting → the selection does not change.</summary>
        public void SelectRow(uint? id){if(Current is {IsDisposed:false} ui&&!BoardPanelSet.IsWaiting(ui.Displayed))ui.SelectRow(id);}
        public void Submit(string title,string content){if(Current is {IsDisposed:false} ui)_=ui.Submit(title,content);}
        public void Notice(string text){Current?.Notice(text);}
        public long? Gold=>Current is {IsDisposed:false} ui?ui.Gold:null;
        public void ChatNotice(string text,int color){if(Current is {IsDisposed:false} ui)ui.ChatNotice(text,color);}
    }
}

public sealed partial class WorldSession {
    /// <summary>R7-F (#808): board system lines printed by the client itself (189 from sub_7CDE00; 544 while BoardUi is hidden).</summary>
    internal void AddBoardChatNotice(string text,int? color){
        if(text.Length==0)return;
        playerChatLines.Enqueue(new(++playerChatSequence,0,text){ColorIndex=color});
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
}
