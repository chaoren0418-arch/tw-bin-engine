namespace P06;

public sealed record BoardRow(uint Id,string Author,string Date,string Title);
public sealed record BoardArticle(uint Id,string Author,string Date,string Title,string Content);
public sealed record BoardPage(uint ObjectId,ushort Fee,BoardRow[] Rows);
public sealed record BoardView(int Generation,int Epoch,int TargetRevision,int Revision,int OpenRevision,uint ObjectId,
    string Kind,string Notice,BoardRow[] Rows,BoardArticle? Article,ushort Fee,bool CanAct,bool CanPrevious,bool CanNext);

/// <summary>R9-N: what the read window's 672 / 676 does for the current snapshot.</summary>
public enum BoardArticleStep { None, Read, NextPage, CloseRead }

/// <summary>
/// R9-N (#858, followups-9 §3.2, verified): read-window 672 / 676.
///   672 = previous article (sub_7CDEE0 → sub_7CD910 → sub_7CDBD0): busy → nothing; index ≤ 0 → close the read window;
///         otherwise index − 1 and send C_BoardRead (TW16 op 93 `cdd`(board, topic); 3.81 opcode 114).
///   676 = next article (sub_7CD470 → sub_7CD850 → sub_7CDA70): the next row is loaded → C_BoardRead; otherwise when there
///         is a next page (this[7] ≠ 0, inferred server cursor) C_BoardBack (TW16 op 6 `cdd`(board, cursor); 3.81 opcode 23)
///         loads it first; neither → close the read window.
/// "Index" is the article's row in the current page (rows are newest first, as the server sends them).
/// </summary>
public static class BoardNavigation {
    public static (BoardArticleStep Step,uint Topic) Article(BoardView? v,bool next){
        if(v is not {CanAct:true,Kind:"article",Article:{} a})return (BoardArticleStep.None,0u);
        int i=Array.FindIndex(v.Rows,r=>r.Id==a.Id);
        // An article that is not on the current page has no index: there is nothing to step to.
        if(i<0)return (BoardArticleStep.CloseRead,0u);
        if(!next)return i==0?(BoardArticleStep.CloseRead,0u):(BoardArticleStep.Read,v.Rows[i-1].Id);
        if(i+1<v.Rows.Length)return (BoardArticleStep.Read,v.Rows[i+1].Id);
        return v.CanNext?(BoardArticleStep.NextPage,0u):(BoardArticleStep.CloseRead,0u);
    }
}

public static class BoardPackets {
    static void Identity(uint id){if(id is 0 or >int.MaxValue)throw new ArgumentException("board identity");}
    public static byte[] List(uint board){Identity(board);return LoginPackets.Pad([10,..BitConverter.GetBytes(board)]);}
    static byte[] Pair(byte opcode,uint board,uint topic){Identity(board);Identity(topic);return LoginPackets.Pad([opcode,..BitConverter.GetBytes(board),..BitConverter.GetBytes(topic)]);}
    public static byte[] Back(uint board,uint topic)=>Pair(23,board,topic);
    public static byte[] Read(uint board,uint topic)=>Pair(114,board,topic);
    public static byte[] Delete(uint board,uint topic)=>Pair(153,board,topic);
    public static byte[] Write(uint board,string title,string content){
        Identity(board);ValidateText(title,16,false);ValidateText(content,1000,true);
        return LoginPackets.Pad([141,..BitConverter.GetBytes(board),..LoginPackets.TextEncoding.GetBytes(title),0,..LoginPackets.TextEncoding.GetBytes(content),0]);
    }
    static void ValidateText(string text,int limit,bool multiline){
        if(string.IsNullOrWhiteSpace(text)||text.Length>limit||text.Any(c=>char.IsControl(c)&&!(multiline&&c is '\r' or '\n' or '\t')))
            throw new ArgumentException("標題需為 1–16 字、內容需為 1–1000 字，且不可含控制字元。");
        LoginPackets.TextEncoding.GetByteCount(text);
    }
    public static BoardPage ParseList(LoginReader r){
        if(r.C()!=0)throw new ProtocolException("board prefix");
        uint board=r.D();if(board is 0 or >int.MaxValue||r.D()!=0x7fffffff)throw new ProtocolException("board header");
        int count=r.H();ushort fee=r.H();if(count>8)throw new ProtocolException("board row count");
        var rows=new BoardRow[count];uint previous=uint.MaxValue;
        for(int i=0;i<count;i++){
            uint id=r.D();if(id is 0 or >int.MaxValue||id>=previous)throw new ProtocolException("board topic order");
            rows[i]=new(id,r.S(),r.S(),r.S());previous=id;
        }
        r.Finish();return new(board,fee,rows);
    }
    public static BoardArticle ParseRead(LoginReader r){
        uint id=r.D();if(id is 0 or >int.MaxValue)throw new ProtocolException("board topic identity");
        var article=new BoardArticle(id,r.S(),r.S(),r.S(),r.S());r.Finish();return article;
    }
    // S87 carries no NPC template ID. Gfx alone also matches the auction board ($1383).
    public static bool IsBoard(WorldObject? o)=>o is {Dead:false,AppearancePending:false,ShopActive:false}
        &&(o.Flags&4)==0&&o.Gfx is 1544 or 1546 or 1557 or 1559 or 2205 or 2207
        &&o.Name is "$1385" or "$33 $1385" or "佈告欄" or "公告欄" or "公告板" or "結婚式　揭示板";
}

public sealed partial class WorldSession {
    uint boardId,boardActor,boardTopic,boardCursor,pendingBoardCursor;
    int boardEpoch,boardTargetRevision,boardRevision,boardOpenRevision;
    string boardKind="closed",boardNotice="";
    BoardRow[] boardRows=[];BoardArticle? boardArticle;ushort boardFee=300;
    readonly List<uint> boardHistory=[];
    string boardPageCommand="";long boardWait;
    bool BoardContext=>boardId!=0&&boardEpoch==WorldEpoch&&boardTargetRevision==TargetRevision
        &&boardActor==Actor?.ObjectId&&boardId==TargetId&&NearbyTarget&&BoardPackets.IsBoard(Target);
    internal void CheckBoard(){
        if(boardKind=="closed")return;
        if(!BoardContext){CloseBoard("目標、位置或世界已改變，請重新開啟公告板。");return;}
        if(boardKind is "list-wait" or "read-wait" or "delete-wait"&&InteractionClock()-boardWait>=InteractionTimeoutMs){
            boardKind="timeout";boardRevision++;boardNotice="等待公告板回覆逾時。可手動刷新；不會自動重送。";
        }
    }
    internal void BeginBoard(){
        CloseBoard();boardId=TargetId!.Value;boardActor=Actor!.ObjectId;boardEpoch=WorldEpoch;
        boardTargetRevision=TargetRevision;boardOpenRevision=boardRevision;boardKind="opened";boardNotice="準備讀取公告板。";
    }
    public void CloseBoard(string notice="公告板已關閉。"){
        boardId=boardTopic=boardCursor=pendingBoardCursor=0;boardRows=[];boardArticle=null;
        boardHistory.Clear();boardKind="closed";boardNotice=notice;boardRevision++;
    }
    internal BoardView BoardSnapshot(int generation,bool canAct){
        CheckBoard();return new(generation,WorldEpoch,TargetRevision,boardRevision,boardOpenRevision,boardId,boardKind,boardNotice,
            boardRows.ToArray(),boardArticle,boardFee,canAct&&BoardContext&&boardKind is not ("closed" or "list-wait" or "read-wait" or "delete-wait"),
            boardHistory.Count>0,boardRows.Length==8&&boardRows[^1].Id!=boardCursor);
    }
    internal bool CurrentBoard(BoardView v){CheckBoard();return BoardContext&&v.Epoch==WorldEpoch&&v.TargetRevision==TargetRevision&&v.Revision==boardRevision&&v.ObjectId==boardId;}
    internal bool SameBoardWindow(BoardView v){CheckBoard();return BoardContext&&v.Epoch==WorldEpoch&&v.TargetRevision==TargetRevision&&v.OpenRevision==boardOpenRevision&&v.ObjectId==boardId;}
    internal byte[]? BoardRequest(string command,uint topic,string title,string content){
        if(!BoardContext||boardKind is "closed" or "list-wait" or "read-wait" or "delete-wait")return null;
        return command switch {
            "refresh"=>BoardPackets.List(boardId),
            "next" when boardKind is "list" or "article"&&boardRows.Length==8&&boardRows[^1].Id!=boardCursor=>BoardPackets.Back(boardId,boardRows[^1].Id),
            "previous" when boardKind is "list" or "article"&&boardHistory.Count>0=>boardHistory[^1]==0?BoardPackets.List(boardId):BoardPackets.Back(boardId,boardHistory[^1]),
            "read" when boardKind is "list" or "article"&&boardRows.Any(r=>r.Id==topic)=>BoardPackets.Read(boardId,topic),
            "write" when boardKind is "list" or "article"=>BoardPackets.Write(boardId,title,content),
            "delete" when boardKind=="article"&&boardArticle?.Id==topic&&boardArticle.Author==Actor?.Name=>BoardPackets.Delete(boardId,topic),
            _=>null
        };
    }
    /// <summary>
    /// R12-E（T432／#989）刪文後的等待。原版 sub_7CD490（VA 0x7CD490，FO 0x3CC890，Capstone 靜態反組譯，已驗證）：
    /// [+0x40]≠0 或沒有選取列 → 不做事；送 op 222（0xDE：board [+0xc]、topic），在本機移除該列；剩下的列不足一頁而且
    /// 伺服器還有下一段（[+0x1c]≠0）時設 [+0x40]＝4 並送 op 6（board、cursor [+0x1c]）補抓清單，收到清單（sub_7CD600）才清 0。
    /// 引擎不在本機移除文章（docs/protocol/board.md：刪文沒有 ACK），所以刪文後一律進 "delete-wait"，再補送一次目前這頁的
    /// 清單請求（游標 0 → C10，否則 C23 同一個游標；BoardDeleteReload），收到這頁清單才解除；歷史頁不清掉。
    /// "一律補抓"是引擎對應（原版只有不足一頁且有下一段才抓），標推定。逾時同 list-wait，可手動刷新。
    /// </summary>
    internal byte[]? BoardDeleteReload(){
        CheckBoard();if(!BoardContext||boardKind!="delete-wait")return null;
        return pendingBoardCursor==0?BoardPackets.List(boardId):BoardPackets.Back(boardId,pendingBoardCursor);
    }
    internal void BoardSent(string command,uint topic){
        boardRevision++;boardWait=InteractionClock();boardTopic=topic;
        if(command=="write"){
            boardKind="submitted";boardNotice="請求已送出，尚未確認結果。請手動刷新核對文章與金幣；不會自動重送。";return;
        }
        if(command=="delete"){
            boardPageCommand="reload";pendingBoardCursor=boardCursor;boardArticle=null;boardKind="delete-wait";
            boardNotice="刪文請求已送出，正在重新讀取清單；尚未確認結果，以清單為準，不會自動重送。";return;
        }
        if(command=="read"){boardKind="read-wait";boardNotice="正在讀取文章。";return;}
        boardPageCommand=command;pendingBoardCursor=command=="next"?boardRows[^1].Id:command=="previous"?boardHistory[^1]:0;
        boardKind="list-wait";boardArticle=null;boardNotice="正在讀取公告板清單。";
    }
    void ReceiveBoardList(LoginReader r){
        var page=BoardPackets.ParseList(r);CheckBoard();
        if(!BoardContext||boardKind is not ("list-wait" or "delete-wait")||page.ObjectId!=boardId)return;
        if(pendingBoardCursor!=0&&page.Rows.Any(row=>row.Id>pendingBoardCursor))return;
        if(boardPageCommand=="next")boardHistory.Add(boardCursor);
        else if(boardPageCommand=="previous")boardHistory.RemoveAt(boardHistory.Count-1);
        else if(boardPageCommand!="reload")boardHistory.Clear();   // R12-E：刪文後重抓同一頁，歷史不變
        boardCursor=pendingBoardCursor;boardRows=page.Rows;boardFee=page.Fee;boardKind="list";boardRevision++;
        boardNotice=boardRows.Length==0?"目前沒有文章。":"選擇文章閱讀；發文費用以伺服器為準。";
    }
    void ReceiveBoardRead(LoginReader r){
        var article=BoardPackets.ParseRead(r);CheckBoard();
        if(!BoardContext||boardKind!="read-wait"||article.Id!=boardTopic)return;
        boardArticle=article;boardKind="article";boardRevision++;boardNotice="已收到文章內容。";
    }
}

public sealed partial class LoginConnection {
    public BoardView BoardSnapshot()=>CaptureWorld(()=>World.BoardSnapshot(generation,State==LoginState.InWorld
        &&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy&&!interactionSending));
    public bool CloseBoard(BoardView v)=>CaptureWorld(()=>{
        if(v.Generation!=generation||!World.SameBoardWindow(v))return false;World.CloseBoard();return true;
    });
    public async Task<bool> SendBoard(string command,BoardView v,uint topic=0,string title="",string content="",Func<bool>? pointerCurrent=null){
        bool Valid()=>v.Generation==generation&&State==LoginState.InWorld&&World.CurrentBoard(v)
            &&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy&&(pointerCurrent?.Invoke()??true);
        if(interactionSending||sendMovementGate==null||!CaptureWorld(Valid))return false;
        byte[]? packet;
        try{packet=CaptureWorld(()=>World.BoardRequest(command,topic,title,content));}
        catch(ArgumentException){return false;}
        if(packet==null)return false;
        var sender=sendMovementGate;interactionSending=true;
        try{
            bool sent=await sender(packet,()=>CaptureWorld(Valid),()=>{World.BoardSent(command,topic);Set(State,World.BoardSnapshot(generation,false).Notice);})&&v.Generation==generation;
            // R12-E：刪文後補送清單請求（sub_7CD490 的 op 6），送不出去就停在 delete-wait，逾時後可手動刷新。
            if(sent&&command=="delete"&&CaptureWorld(()=>World.BoardDeleteReload()) is {} reload)
                await sender(reload,()=>CaptureWorld(()=>v.Generation==generation&&State==LoginState.InWorld&&!World.ActionLocked
                    &&!World.TradeBlocksActions&&!MovementBusy&&World.BoardDeleteReload()!=null),()=>{});
            return sent;
        }
        catch(Exception){if(v.Generation==generation){Cancel();Set(LoginState.Faulted,"公告板傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(v.Generation==generation)interactionSending=false;}
    }
}
