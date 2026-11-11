using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R12-E（T432／#989）board-wait-followup 離線測試：公告板等待收尾（gap-matrix 15.14.5／15.14.6，FU15§2.11）。
//   ① [this+0x40]≠0 期間清單窗滑鼠 sub_7CD080 全部吃掉、不動作：左鍵選取、左鍵雙擊閱讀、右鍵雙擊關窗都不做；
//   ② sub_7CD490：刪文 op 222 之後設 4、送 op 6 補抓清單，收到清單（sub_7CD600）才清 0 → 引擎 "delete-wait"，
//      C153 之後補送目前這頁的 C10／C23，收到這頁 S68 才解除；歷史頁不清掉；逾時同 list-wait。
// 只用合成封包、合成圖面與記憶體內的假傳送端；不開遊戲、不讀客戶端資源、不開窗、不連網路。
static class BoardWaitFollowupTests {
    // ── ① 清單窗滑鼠 ──
    sealed class Host:IBoardPanelHost,IBoardContentHost {
        public BoardView? Board{get;set;}
        public uint? SelectedRow{get;set;}
        public readonly List<string> Calls=[];
        public void Press(string command)=>Calls.Add(command);
        public void SelectRow(uint? id){SelectedRow=id;Calls.Add("select:"+(id?.ToString()??"none"));}
        public void Submit(string title,string content)=>Calls.Add("submit");
        public void Notice(string text)=>Calls.Add("notice:"+text);
    }
    static readonly BoardRow[] Rows=[new(20,"作者甲","2026/11/09","第一篇"),new(19,"作者乙","2026/11/08","第二篇")];
    static BoardView View(string kind,bool canAct)=>new(1,1,1,1,1,456,kind,"",Rows,null,300,canAct,false,false);
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFFFF0000u,w*h).ToArray()));
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at,MouseButtons b,int clicks)=>new(k,at,b,clicks,0,Keys.None);

    [WorldModuleTest("board-wait-mouse")]
    static void Mouse(WorldModuleTestContext t){
        t.Check(BoardPanelSet.IsWaiting(View("delete-wait",false)),"delete-wait (sub_7CD490 → 4) = waiting");
        t.Check(BoardPanelSet.IsWaiting(View("list-wait",false))&&BoardPanelSet.IsWaiting(View("read-wait",false)),"list-wait / read-wait still waiting");
        t.Check(!BoardPanelSet.IsWaiting(View("submitted",false))&&!BoardPanelSet.IsWaiting(View("list",true))&&!BoardPanelSet.IsWaiting(View("timeout",true)),"submitted (write) / list / timeout = not waiting");

        var map=new Dictionary<int,UiSurface>{[656]=Surface(656,238,350),[657]=Surface(657,10,10),[658]=Surface(658,17,17),[660]=Surface(660,23,13),
            [662]=Surface(662,31,26),[664]=Surface(664,31,26),[666]=Surface(666,35,23),[668]=Surface(668,35,22)};
        var host=new Host();var buttons=new BoardPanelSet(host);var set=new BoardContentSet(buttons,host);
        buttons.LoadSurfaces(id=>map.GetValueOrDefault(id));set.LoadSurfaces(id=>map.GetValueOrDefault(id));
        var list=set.List;var cmd=new UiNullCommands();
        var row0=new Point(100,55);var row1=new Point(100,85);var empty=new Point(100,250);var outside=new Point(300,55);
        bool All(params UiMouseEvent[] events){bool eaten=true;foreach(var e in events)eaten&=list.Mouse(e,cmd);return eaten;}

        foreach(var kind in new[]{"list-wait","read-wait","delete-wait"}){
            host.Board=View(kind,false);host.SelectedRow=null;host.Calls.Clear();list.Update(InWorld);
            t.Check(list.Visible&&buttons.Waiting,$"{kind}: list window up and waiting");
            t.Check(All(M(UiMouseKind.Down,row0,MouseButtons.Left,1),M(UiMouseKind.Up,row0,MouseButtons.Left,1)),$"{kind}: left click on a row eaten");
            t.Check(All(M(UiMouseKind.Down,row1,MouseButtons.Left,2)),$"{kind}: left double click eaten");
            t.Check(All(M(UiMouseKind.Down,empty,MouseButtons.Right,2),M(UiMouseKind.Up,empty,MouseButtons.Right,2)),$"{kind}: right double click eaten");
            t.Check(All(M(UiMouseKind.Move,row0,MouseButtons.None,0),M(UiMouseKind.Down,empty,MouseButtons.Middle,1)),$"{kind}: move / other buttons eaten");
            t.Check(host.Calls.Count==0&&host.SelectedRow==null,$"{kind}: nothing selected, read or closed",host.Calls);
            t.Check(!list.Mouse(M(UiMouseKind.Down,outside,MouseButtons.Right,2),cmd)&&host.Calls.Count==0,$"{kind}: outside (0,0,238,350) not taken (sub_7CD080 only inside)");
        }

        // 回應到了：同樣的操作恢復原本行為。
        host.Board=View("list",true);host.Calls.Clear();list.Update(InWorld);
        t.Check(!buttons.Waiting,"list arrived → not waiting");
        list.Mouse(M(UiMouseKind.Down,row0,MouseButtons.Left,1),cmd);
        t.Check(host.SelectedRow==20u&&host.Calls.SequenceEqual(["select:20"]),"after the list: left click selects the row",host.Calls);
        host.Calls.Clear();list.Mouse(M(UiMouseKind.Down,row1,MouseButtons.Left,2),cmd);
        t.Check(host.Calls.SequenceEqual(["select:19","read"]),"after the list: left double click reads",host.Calls);
        host.Calls.Clear();list.Mouse(M(UiMouseKind.Down,empty,MouseButtons.Right,2),cmd);
        t.Check(host.Calls.SequenceEqual(["close"]),"after the list: right double click closes",host.Calls);
        // submitted（發文後）沒有對應的 [this+0x40]：不吃掉（右鍵雙擊仍可關窗）。
        host.Board=View("submitted",false);host.Calls.Clear();list.Update(InWorld);
        list.Mouse(M(UiMouseKind.Down,empty,MouseButtons.Right,2),cmd);
        t.Check(host.Calls.SequenceEqual(["close"]),"submitted (write) is not a wait: right double click still closes",host.Calls);
    }

    // ── ② 刪文後的等待 ──
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,BoardId=456;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    // S87（欄位同 AmountPromptTests／BoardChecks）：自己 123（名字 offline），公告板 456（gfx 1544、$1385，相鄰一格）。
    static byte[] Appear(uint id,ushort x,ushort gfx,string name)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(gfx),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static byte[] Row(uint id)=>[..Le32(id),..Str("作者"),..Str("2026/11/11"),..Str("文章"+id)];
    static byte[] List(params uint[] ids)=>P([68,0],Le32(BoardId),Le32(0x7fffffff),Le16((ushort)ids.Length),Le16(300),ids.SelectMany(Row).ToArray());
    static byte[] Read(uint id,string author="offline")=>P([148],Le32(id),Str(author),Str("2026/11/11"),Str("標題"),Str("內容"));
    static LoginConnection Seed(List<byte[]> sent,Func<byte[],bool> accept){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,0,"offline"),"offline");c.World.Receive(Appear(BoardId,32601,1544,"$1385"),"offline");
        c.World.SelectTarget(BoardId);
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid()||!accept(b))return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Send(LoginConnection c,string command,uint topic=0)=>c.SendBoard(command,c.BoardSnapshot(),topic).GetAwaiter().GetResult();
    static bool Same(byte[] a,byte[] b)=>a.AsSpan().SequenceEqual(b);

    [WorldModuleTest("board-delete-wait")]
    static void DeleteWait(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent,_=>true)){
            t.Check(BoardPackets.IsBoard(c.World.Target),"seeded target is a board");
            c.World.BeginBoard();
            t.Check(Send(c,"refresh")&&Same(sent[^1],BoardPackets.List(BoardId)),"home page C10");
            c.World.Receive(List(20,19,18,17,16,15,14,13),"offline");
            t.Check(c.BoardSnapshot() is {Kind:"list",CanNext:true},"first page");
            t.Check(Send(c,"next")&&Same(sent[^1],BoardPackets.Back(BoardId,13)),"next page C23 cursor 13");
            c.World.Receive(List(13,12,11),"offline");
            t.Check(c.BoardSnapshot() is {Kind:"list",CanPrevious:true},"second page, history kept");
            t.Check(Send(c,"read",12)&&Same(sent[^1],BoardPackets.Read(BoardId,12)),"read own article C114");
            c.World.Receive(Read(12),"offline");
            t.Check(c.BoardSnapshot() is {Kind:"article",Article.Id:12u},"article shown");

            int before=sent.Count;
            t.Check(Send(c,"delete",12),"delete accepted");
            t.Equal(before+2,sent.Count,"delete sends two packets (sub_7CD490: op 222 then op 6)");
            t.Check(Same(sent[^2],BoardPackets.Delete(BoardId,12)),"first C153 (op 222)");
            t.Check(Same(sent[^1],BoardPackets.Back(BoardId,13)),"then the same page again: C23 with this page's cursor 13");
            var w=c.BoardSnapshot();
            t.Check(w.Kind=="delete-wait"&&!w.CanAct&&w.Article==null&&BoardPanelSet.IsWaiting(w),"delete-wait: waiting, cannot act, read window gone",w.Kind);
            t.Check(w.Notice.Contains("尚未確認"),"delete is not claimed successful",w.Notice);
            t.Check(!Send(c,"refresh")&&!Send(c,"next")&&!Send(c,"read",13)&&sent.Count==before+2,"nothing else sends while waiting");
            c.World.Receive(Read(12),"offline");
            t.Equal("delete-wait",c.BoardSnapshot().Kind,"an article reply does not end the wait");
            c.World.Receive(List(20,19),"offline");
            t.Equal("delete-wait",c.BoardSnapshot().Kind,"a page outside the cursor (ids > 13) does not end the wait");
            c.World.Receive(List(13,11),"offline");
            var after=c.BoardSnapshot();
            t.Check(after is {Kind:"list",CanAct:true,CanPrevious:true}&&after.Rows.Select(r=>r.Id).SequenceEqual([13u,11u]),"the page reply ends the wait; rows from the server; history kept");
            t.Check(!BoardPanelSet.IsWaiting(after),"not waiting after the list");
            t.Check(Send(c,"previous")&&Same(sent[^1],BoardPackets.List(BoardId)),"previous still goes back to the home page");
        }

        // 首頁刪文 → 補送 C10。
        sent.Clear();
        using(var c=Seed(sent,_=>true)){
            c.World.BeginBoard();Send(c,"refresh");c.World.Receive(List(20,19),"offline");
            Send(c,"read",20);c.World.Receive(Read(20),"offline");
            t.Check(Send(c,"delete",20)&&Same(sent[^2],BoardPackets.Delete(BoardId,20))&&Same(sent[^1],BoardPackets.List(BoardId)),"home page delete → C153 then C10");
            c.World.Receive(List(19),"offline");
            t.Check(c.BoardSnapshot() is {Kind:"list",CanPrevious:false},"home page reply ends the wait");
            // 他人文章不能刪（沿用既有規則），不進等待。
            Send(c,"read",19);c.World.Receive(Read(19,"其他玩家"),"offline");
            int n=sent.Count;
            t.Check(!Send(c,"delete",19)&&sent.Count==n&&c.BoardSnapshot().Kind=="article","other author's delete blocked, no wait");
            // 發文仍是 submitted（不是 [this+0x40] 等待），清單也解不開。
            Send(c,"refresh");c.World.Receive(List(19),"offline");
            t.Check(c.SendBoard("write",c.BoardSnapshot(),0,"標題","內容").GetAwaiter().GetResult()&&c.BoardSnapshot().Kind=="submitted","write → submitted");
            c.World.Receive(List(19),"offline");
            t.Equal("submitted",c.BoardSnapshot().Kind,"write: unsolicited list still ignored");
        }

        // 補抓清單送不出去 → 停在 delete-wait，逾時後可手動刷新。
        sent.Clear();
        using(var c=Seed(sent,b=>b[0]!=10)){
            long now=1000;c.World.InteractionClock=()=>now;
            // 不經 C10 直接放一頁（假傳送端擋掉所有 C10）。
            c.World.BeginBoard();Set(c.World,"boardKind","list-wait");Set(c.World,"boardWait",now);c.World.Receive(List(20),"offline");
            t.Equal("list",c.BoardSnapshot().Kind,"page in place");
            Send(c,"read",20);c.World.Receive(Read(20),"offline");
            t.Check(Send(c,"delete",20)&&Same(sent[^1],BoardPackets.Delete(BoardId,20)),"delete sent, reload refused by the sender");
            t.Equal("delete-wait",c.BoardSnapshot().Kind,"still waiting for the list");
            now+=15000;
            var timeout=c.BoardSnapshot();
            t.Check(timeout is {Kind:"timeout",CanAct:true}&&!BoardPanelSet.IsWaiting(timeout),"delete-wait times out like list-wait; manual refresh allowed");
            c.World.Receive(List(20),"offline");
            t.Equal("timeout",c.BoardSnapshot().Kind,"late list after the timeout ignored");
        }
    }
}
