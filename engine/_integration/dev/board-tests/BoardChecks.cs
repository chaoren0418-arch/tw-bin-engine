using P06;

partial class Checks {
    static byte[] BoardObject(string name="$1385",ushort gfx=1544)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(456u);b.Write(gfx);
        b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,name);Text(b,"");b.Write((byte)0);
        b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] BoardList(uint board=456,params uint[] topics)=>Packet(b=>{
        b.Write((byte)68);b.Write((byte)0);b.Write(board);b.Write(0x7fffffffu);b.Write((ushort)topics.Length);b.Write((ushort)300);
        foreach(uint id in topics){b.Write(id);Text(b,"作者");Text(b,"2026/11/07");Text(b,"文章"+id);}
    });
    static byte[] BoardRead(uint topic=20,string author="offline")=>Packet(b=>{
        b.Write((byte)148);b.Write(topic);Text(b,author);Text(b,"2026/11/07");Text(b,"標題");Text(b,"第一行\r\n第二行 <script>");
    });
    static LoginConnection BoardSeed(){var c=Seed();Feed(c,BoardObject());c.World.SelectTarget(456);return c;}
    static Task<bool> OpenBoard(LoginConnection c)=>c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision);
    static void BoardReject(Action action,string label){try{action();}catch(Exception ex) when(ex is ArgumentException or ProtocolException){Check(true,label);return;}throw new Exception("accepted invalid board packet: "+label);}
    static LoginReader BoardReader(byte[] bytes){var r=new LoginReader(bytes);r.C();return r;}
    [CheckGroup("board",Order=1),CheckGroup("map-continuity",Order=50),CheckGroup(CheckGroups.Default,Order=290)]
    static async Task BoardChecks(){
        // Independent byte vectors: opcode + little endian D fields + NUL terminated CP950 + zero padding.
        Check(Convert.ToHexString(BoardPackets.List(0x01020304))=="0A04030201000000","board C10 D vector");
        Check(Convert.ToHexString(BoardPackets.Back(0x01020304,0x05060708))=="17040302010807060500000000000000","board C23 DD vector");
        Check(Convert.ToHexString(BoardPackets.Read(0x01020304,0x05060708))=="72040302010807060500000000000000","board C114 DD vector");
        Check(Convert.ToHexString(BoardPackets.Delete(0x01020304,0x05060708))=="99040302010807060500000000000000","board C153 DD vector");
        Check(Convert.ToHexString(BoardPackets.Write(0x01020304,"中","文"))=="8D04030201A4A400A4E5000000000000","board C141 DSS CP950 vector");
        BoardReject(()=>BoardPackets.List(0),"board zero object");BoardReject(()=>BoardPackets.Back(1,0),"board zero cursor");
        BoardReject(()=>BoardPackets.Read(1,uint.MaxValue),"board signed topic overflow");BoardReject(()=>BoardPackets.Delete(0,1),"board delete zero object");
        foreach(var text in new[]{"",new string('a',17),"x\0y","😀","a\nb"})BoardReject(()=>BoardPackets.Write(1,text,"內容"),"board invalid title "+text.Length);
        foreach(var text in new[]{"",new string('a',1001),"x\0y","😀"})BoardReject(()=>BoardPackets.Write(1,"標題",text),"board invalid body "+text.Length);
        Check(BoardPackets.Write(1,new string('中',16),new string('文',1000)).Length==2040,"board max character lengths are not byte lengths");
        var parsed=BoardPackets.ParseList(BoardReader(BoardList(456,20,19)));
        Check(parsed.ObjectId==456&&parsed.Fee==300&&parsed.Rows.Length==2&&parsed.Rows[1]==new BoardRow(19,"作者","2026/11/07","文章19"),"board S68 fields and CP950");
        Check(BoardPackets.ParseList(BoardReader(BoardList())).Rows.Length==0,"board S68 empty list");
        var article=BoardPackets.ParseRead(BoardReader(BoardRead()));
        Check(article==new BoardArticle(20,"offline","2026/11/07","標題","第一行\r\n第二行 <script>"),"board S148 all fields plain text");
        foreach(var packet in new[]{BoardList(456,20,19),BoardRead()}){
            for(int length=0;length<packet.Length;length++){
                var cut=packet[..length];BoardReject(()=>{var r=BoardReader(cut);if(packet[0]==68)BoardPackets.ParseList(r);else BoardPackets.ParseRead(r);},"board truncation "+packet[0]+"/"+length);
            }
            var bad=packet.ToArray();bad[^1]=1;BoardReject(()=>{var r=BoardReader(bad);if(packet[0]==68)BoardPackets.ParseList(r);else BoardPackets.ParseRead(r);},"board nonzero padding "+packet[0]);
        }
        foreach(var ids in new[]{new uint[]{1,1},new uint[]{1,2},new uint[]{0},new uint[]{uint.MaxValue},Enumerable.Range(1,9).Reverse().Select(i=>(uint)i).ToArray()})
            BoardReject(()=>BoardPackets.ParseList(BoardReader(BoardList(456,ids))),"board list rejects count/order/identity "+string.Join(',',ids));
        foreach(int offset in new[]{1,6}){var bad=BoardList();bad[offset]^=1;BoardReject(()=>BoardPackets.ParseList(BoardReader(bad)),"board bad header "+offset);}
        BoardReject(()=>BoardPackets.ParseRead(BoardReader(BoardRead(0))),"board invalid read ID");
        using(var c=BoardSeed()){
            foreach(ushort gfx in new ushort[]{1544,1546,1557,1559,2205,2207}){Feed(c,BoardObject(gfx:gfx));Check(BoardPackets.IsBoard(c.World.Target),"board known gfx "+gfx);}
            Feed(c,BoardObject("$33 $1385",1546));Check(BoardPackets.IsBoard(c.World.Target),"board wedding board");
            Feed(c,BoardObject("$1383"));Check(!BoardPackets.IsBoard(c.World.Target),"auction board sharing gfx excluded");
            Feed(c,BoardObject("$13850"));Check(!BoardPackets.IsBoard(c.World.Target),"board name token exact");
            Feed(c,BoardObject(gfx:1110));Check(!BoardPackets.IsBoard(c.World.Target),"board name alone not enough");
        }
        using(var c=BoardSeed()){
            var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
            Feed(c,BoardList(456,20));Feed(c,BoardRead());Check(c.BoardSnapshot().Kind=="closed","unsolicited board replies ignored");
            Check(await OpenBoard(c)&&sent.Count==1&&sent[0].SequenceEqual(BoardPackets.List(456)),"board interaction routes to C10 not C34");
            var beforeMalformed=c.BoardSnapshot();BoardReject(()=>Feed(c,BoardList(456,20,20)),"board malformed list via dispatcher");
            Check(c.BoardSnapshot().Revision==beforeMalformed.Revision&&c.BoardSnapshot().Kind=="list-wait","board malformed list leaves state intact");
            Check(!await c.SendBoard("refresh",c.BoardSnapshot())&&sent.Count==1,"board pending request cannot repeat");
            Feed(c,BoardList(999,20));Check(c.BoardSnapshot().Kind=="list-wait","board wrong object ignored");
            Feed(c,BoardList(456,20,19,18,17,16,15,14,13));var first=c.BoardSnapshot();
            Check(first.Kind=="list"&&first.CanNext&&!first.CanPrevious&&first.Rows.Length==8,"board first page state");
            Feed(c,BoardList(456,99));Check(c.BoardSnapshot().Revision==first.Revision,"board duplicate page ignored");
            Check(!await c.SendBoard("read",first,99),"board unknown article blocked");
            Check(await c.SendBoard("next",first)&&sent[^1].SequenceEqual(BoardPackets.Back(456,13)),"board next cursor is last ID inclusive");
            Feed(c,BoardList(456,20,19));Check(c.BoardSnapshot().Kind=="list-wait","board prior page outside cursor rejected");
            Feed(c,BoardList(456,13,12,11,10,9,8,7,6));var second=c.BoardSnapshot();
            Check(second.CanNext&&second.CanPrevious&&second.Rows[0].Id==13,"board overlapping second page accepted");
            Check(!await c.SendBoard("read",first,20),"board stale page revision blocked");
            Check(await c.SendBoard("next",second)&&sent[^1].SequenceEqual(BoardPackets.Back(456,6)),"board third cursor");
            Feed(c,BoardList(456,6));Check(!c.BoardSnapshot().CanNext,"board inclusive terminal page disables next");
            Check(await c.SendBoard("previous",c.BoardSnapshot())&&sent[^1].SequenceEqual(BoardPackets.Back(456,13)),"board previous uses cursor history");
            Feed(c,BoardList(456,13,12,11,10,9,8,7,6));
            Check(await c.SendBoard("previous",c.BoardSnapshot())&&sent[^1][0]==10,"board return to homepage uses C10");
            Feed(c,BoardList(456,20,19,18,17,16,15,14,13));
            Check(await c.SendBoard("read",c.BoardSnapshot(),20)&&sent[^1].SequenceEqual(BoardPackets.Read(456,20)),"board read C114");
            Feed(c,BoardRead(19));Check(c.BoardSnapshot().Kind=="read-wait","board wrong read topic ignored");
            Feed(c,BoardRead());var read=c.BoardSnapshot();Check(read.Kind=="article"&&read.Article?.Content.Contains("<script>")==true,"board article received literally");
            Check(await c.SendBoard("delete",read,20)&&sent[^1].SequenceEqual(BoardPackets.Delete(456,20)),"board own article delete C153");
            Check(c.BoardSnapshot().Kind=="submitted"&&c.BoardSnapshot().Notice.Contains("尚未確認"),"board delete is not claimed successful");
            Check(!await c.SendBoard("delete",read,20)&&!await c.SendBoard("write",c.BoardSnapshot(),title:"標題",content:"內容"),"board mutation repeat blocked");
            Feed(c,BoardRead());Feed(c,BoardList(456,20));Check(c.BoardSnapshot().Kind=="submitted","unsolicited mutation replies cannot unlock submission");
            Check(await c.SendBoard("refresh",c.BoardSnapshot()),"board manual refresh after delete");Feed(c,BoardList(456,20));
            Check(await c.SendBoard("read",c.BoardSnapshot(),20),"board read another author");Feed(c,BoardRead(author:"其他玩家"));
            Check(!await c.SendBoard("delete",c.BoardSnapshot(),20),"board nonauthor delete blocked");
            Check(!await c.SendBoard("write",c.BoardSnapshot(),title:"😀",content:"內容"),"board unsupported encoding sends nothing");
            Check(await c.SendBoard("write",c.BoardSnapshot(),title:"標題",content:"內容\r\n第二行")&&sent[^1].SequenceEqual(BoardPackets.Write(456,"標題","內容\r\n第二行")),"board write C141");
            Check(!await c.SendBoard("write",c.BoardSnapshot(),title:"標題",content:"內容"),"board write single submission");
            Check(c.CloseBoard(read)&&c.BoardSnapshot().Kind=="closed","board close works even after revision advanced");
            Feed(c,BoardRead());Check(c.BoardSnapshot().Article==null,"board late read after close ignored");
            Check(await OpenBoard(c),"board reopen");Check(!c.CloseBoard(read),"old board window cannot close new window");
            long now=0;Set(c.World,"InteractionClock",(Func<long>)(()=>now));Set(c.World,"boardWait",0L);now=15000;
            Check(c.BoardSnapshot().Kind=="timeout","board timeout allows manual recovery");
            Feed(c,BoardList(456,20));Check(c.BoardSnapshot().Kind=="timeout","late page after timeout ignored");
            Check(await c.SendBoard("refresh",c.BoardSnapshot()),"board manual timeout refresh");Feed(c,BoardList());Check(c.BoardSnapshot().Rows.Length==0&&!c.BoardSnapshot().CanNext,"board empty server state");
        }
        foreach(string invalidation in new[]{"target","remove","map","death","distance","reset","trade","locked","close","moving"}){
            using var c=BoardSeed();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            await OpenBoard(c);Feed(c,BoardList(456,20));var stamp=c.BoardSnapshot();
            var pending=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,v,a)=>{await pending.Task;if(!v())return false;sends++;a();return true;});
            var request=c.SendBoard("write",stamp,title:"測試",content:"內容");
            switch(invalidation){
                case "target":c.World.SelectTarget(null);break;
                case "remove":Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));break;
                case "map":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                case "death":Set(c.World,"Status",c.World.Status! with{Hp=0});break;
                case "distance":Feed(c,Move(456,32620,32940,2));break;
                case "reset":c.Cancel();break;
                case "trade":Set(c.World,"TradeState","invited");break;
                case "locked":Feed(c,LoginPackets.Pad([202,2]));break;
                case "close":c.CloseBoard(stamp);break;
                case "moving":Set(c,"movementSending",true);break;
            }
            pending.SetResult(true);Check(!await request&&sends==0,"board final send fence "+invalidation);
            if(invalidation is not "trade") {Feed(c,BoardRead());Check(c.BoardSnapshot().Article==null,"board invalidated late reply "+invalidation);}
        }
        using(var c=BoardSeed()){
            Sender(c,(_,_,_)=>throw new IOException("offline sender failure"));Check(!await OpenBoard(c)&&c.State==LoginState.Faulted,"board transport failure disconnects without retry");
        }
        using(var c=BoardSeed()){
            Sender(c,(_,_,_)=>throw new Exception("stale pointer reached sender"));Check(!await c.PointerTalk(()=>false),"board stale pointer rejected before send");
        }
        using(var c=BoardSeed()){
            bool current=true;var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(_,v,a)=>{await release.Task;if(!v())return false;sends++;a();return true;});
            var pending=c.PointerTalk(()=>current);current=false;release.SetResult(true);
            Check(!await pending&&sends==0,"board pointer rechecked after sender queue");
        }
    }
}
