using P06;

partial class Checks {
    static byte[] WhoMessage(string value="勇者 測試者 ($1501) [血盟]",ushort id=166,byte count=1)=>Packet(b=>{
        b.Write((byte)71);b.Write(id);b.Write(count);if(count!=0)Text(b,value);
    });
    static byte[] WhoHtml(uint id=123,string html="y_who",ushort flag=1,ushort count=9)=>Packet(b=>{
        b.Write((byte)39);b.Write(id);Text(b,html);b.Write(flag);b.Write(count);
        foreach(string value in new[]{"42","2","3","4","35","10","20","2026/11/07 10:00","2026/11/08 10:00"}.Take(count))Text(b,value);
    });
    static void QueryReject(Action action,string label){
        try{action();}catch(Exception ex) when(ex is ArgumentException or ProtocolException){Check(true,label);return;}
        throw new Exception("player query accepted invalid input: "+label);
    }
    static Task<bool> QuerySend(LoginConnection c,string text){var s=c.PlayerChatSnapshot();return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor);}
    static async Task PlayerQueryChecks(){
        Check(Convert.ToHexString(PlayerQueryPackets.Who(""))=="CE00000000000000","query C206 empty S vector");
        Check(Convert.ToHexString(PlayerQueryPackets.Who("中文"))=="CEA4A4A4E5000000","query C206 CP950 S vector");
        foreach(string input in new[]{"/who","/WHO","/who   "})
            Check(PlayerChatProtocol.Encode(input).SequenceEqual(PlayerQueryPackets.Who("")),"query slash empty name "+input);
        foreach(string input in new[]{"/who 中文","/Who   中文  "})
            Check(PlayerChatProtocol.Encode(input).SequenceEqual(PlayerQueryPackets.Who("中文")),"query slash named player "+input);
        Check(PlayerQueryPackets.Who(new string('中',52)).Length==112,"query local name budget counts characters");
        foreach(string input in new[]{"/who a b","/who a\nb","/who\tname","/who a\0b","/who 😀","/who "+new string('中',53),"/rank","/rank 1 2 name","/CHECK 123"})
            QueryReject(()=>PlayerChatProtocol.Encode(input),"query invalid/unsupported command "+input.Length);
        foreach(string name in new[]{" ","a\tb","a\0b","😀",new string('a',53)})QueryReject(()=>PlayerQueryPackets.Who(name),"query encoder validation "+name.Length);
        foreach(string input in new[]{"/whoever","/ranked","/checker","/say /who 中文","一般訊息","!喊話","\"對象 密語"})
            Check(PlayerChatProtocol.Encode(input).SequenceEqual(PlayerChatProtocol.Encode(PlayerChatProtocol.ParseInput(input))),"query unrelated chat unchanged "+input);
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage())=="玩家資訊：勇者 測試者 （正義者） [血盟]","query S71/166 fields and alignment");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage("測試者 ($1502)"))!.Contains("中立者"),"query neutral alignment");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage("測試者 ($1503)"))!.Contains("邪惡者"),"query chaotic alignment");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage("<script>\ntext"))=="玩家資訊：<script>text","query plain text/control sanitization");
        var html=PlayerQueryPackets.DecodeWhoHtml(WhoHtml())!.Value;
        Check(html.ObjectId==123&&html.Lines.Length==9&&html.Lines[0]=="線上人數：42"&&html.Lines[8]=="重啟時間：2026/11/08 10:00","query S39/y_who nine fields");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage(id:125,count:0))==null,"query other S71 falls through");
        Check(PlayerQueryPackets.DecodeWhoHtml(WhoHtml(html:"npc",flag:0,count:0))==null,"query other S39 falls through");
        foreach(var packet in new[]{WhoMessage(),WhoHtml()}){
            for(int n=0;n<packet.Length;n++){
                byte[] truncated=packet[..n];
                using var c=Seed();int before=c.World.PlayerChatRevision;
                QueryReject(()=>Feed(c,truncated),"query truncated packet "+packet[0]+"/"+n);
                Check(c.World.PlayerChatRevision==before,"query truncation leaves chat intact");
            }
            var bad=packet.ToArray();bad[^1]=1;
            QueryReject(()=>{if(packet[0]==71)PlayerQueryPackets.DecodeWhoMessage(bad);else PlayerQueryPackets.DecodeWhoHtml(bad);},"query nonzero padding "+packet[0]);
            QueryReject(()=>{var extra=packet.Concat(new byte[8]).ToArray();if(packet[0]==71)PlayerQueryPackets.DecodeWhoMessage(extra);else PlayerQueryPackets.DecodeWhoHtml(extra);},"query extra padding "+packet[0]);
        }
        foreach(var malformed in new[]{WhoMessage(count:0),WhoMessage(count:2),WhoHtml(id:0),WhoHtml(flag:0),WhoHtml(count:8),WhoHtml(count:10)}){
            using var c=Seed();int revision=c.World.PlayerChatRevision;
            QueryReject(()=>Feed(c,malformed),"query malformed identity/shape");
            Check(c.World.PlayerChatRevision==revision,"query malformed packet has no partial history");
        }
        using(var c=Seed()){
            Feed(c,WhoMessage());Check(c.World.PlayerChatLines.Length==1&&c.World.LastServerReply==null,"query real S71 dispatcher does not acknowledge inventory");
            Feed(c,WhoMessage(id:125,count:0));Check(c.World.LastServerReply?.Id==125&&c.World.PlayerChatLines.Length==1,"query unrelated inventory response preserved");
            Feed(c,WhoHtml());Check(c.World.PlayerChatLines.Length==10&&c.World.PlayerChatLines.Last().Text.Contains("重啟時間"),"query self HTML visible without NPC selection");
            int revision=c.World.PlayerChatRevision;Feed(c,WhoHtml(id:456));Check(c.World.PlayerChatRevision==revision,"query wrong self ignored");
            Call(c.World,"BeginTalk");int npcRevision=c.World.InteractionRevision;
            Feed(c,WhoMessage());Check(c.World.InteractionRevision==npcRevision&&c.World.LastServerReply?.Id==125,"query S71 preserves pending NPC and inventory state");
            Feed(c,WhoHtml());Check(c.World.InteractionRevision==npcRevision&&c.World.InteractionKind=="waiting","query preserves pending NPC dialog");
            Feed(c,WhoHtml(456,"npc",0,0));Check(c.World.InteractionKind=="dialog","query original NPC HTML still dispatched");
            Set(c.World,"Status",c.World.Status! with{Level=51});
            Feed(c,Packet(b=>{b.Write((byte)39);b.Write(123u);Text(b,"RaiseAttr");}));
            Check(c.World.BonusStatsPhase=="offered","query RaiseAttr special S39 preserved");
            Feed(c,Packet(b=>{b.Write((byte)161);b.Write((byte)0);b.Write(0u);Text(b,"已使用萬能藥:0");}));
            Check(c.World.LastServerMessage=="已使用萬能藥:0","query direct S161 response keeps existing information display");
            for(int n=0;n<120;n++)Feed(c,WhoMessage("玩家"+n));
            Check(c.World.PlayerChatLines.Length==100&&c.World.PlayerChatLines.Last().Text.Contains("119"),"query shared chat history bounded");
            c.World.Reset();Check(c.World.PlayerChatLines.Length==0,"query logout clears query history");
            Feed(c,WhoHtml());Check(c.World.PlayerChatLines.Length==0,"query HTML before world ready ignored");
        }
        using(var c=Seed()){
            var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
            foreach(string text in new[]{"/check 456","/rank 1 2 name"}){
                bool result=await QuerySend(c,text);string notice=c.PlayerChatSnapshot().Notice;
                Check(!result&&sent.Count==0&&(notice.Contains("不是")||notice.Contains("不支援")),"query C99/C63 not sent");
            }
            Check(await QuerySend(c,"/who 中文")&&sent.Single().SequenceEqual(PlayerQueryPackets.Who("中文")),"query sends C206 through actual chat transport");
            Check(c.World.PlayerChatLines.Length==0,"query send does not fabricate result");
            Check(!await QuerySend(c,"/who")&&sent.Count==1,"query shared double submit throttle");
        }
        foreach(string guard in new[]{"session","world","actor","dead","queued-world","disconnected"}){
            using var c=Seed();var s=c.PlayerChatSnapshot();int sends=0;
            if(guard=="dead")Set(c.World,"Status",c.World.Status! with{Hp=0});
            if(guard=="disconnected")Set(c,"State",LoginState.Faulted);
            Sender(c,(b,v,a)=>{if(guard=="queued-world")Feed(c,LoginPackets.Pad([206,4,0,0]));if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.SendPlayerChat("/who",s.Generation+(guard=="session"?1:0),s.WorldEpoch+(guard=="world"?1:0),s.Actor+(guard=="actor"?1u:0))&&sends==0,"query stale/admission guard "+guard);
        }
    }
}
