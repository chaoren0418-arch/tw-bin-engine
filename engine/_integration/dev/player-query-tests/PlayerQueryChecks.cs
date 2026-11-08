using P06;

partial class Checks {
    static byte[] WhoMessage(string value="勇者 測試者 ($1501) [血盟]",ushort id=166,byte count=1)=>Packet(b=>{
        b.Write((byte)71);b.Write(id);b.Write(count);if(count!=0)Text(b,value);
    });
    static byte[] WhoHtml(uint id=123,string html="y_who",ushort flag=1,ushort count=9)=>Packet(b=>{
        b.Write((byte)39);b.Write(id);Text(b,html);b.Write(flag);b.Write(count);
        foreach(string value in new[]{"42","2","3","4","35","10","20","2026/11/07 10:00","2026/11/08 10:00"}.Take(count))Text(b,value);
    });
    static byte[] QueryHtml(uint id,string html,params string[] args)=>Packet(b=>{
        b.Write((byte)39);b.Write(id);Text(b,html);b.Write((ushort)1);b.Write((ushort)args.Length);foreach(string arg in args)Text(b,arg);
    });
    // Server fills unused rank slots with one space.
    static string[] RankRows(int count,params string[] rows)=>rows.Concat(Enumerable.Repeat(" ",count-rows.Length)).ToArray();
    static string[] HeroRows(params string[] rows)=>RankRows(10,rows).Append("全職業").ToArray();
    static string[] CheckFields(){
        var fields=new string[29];Array.Fill(fields,"");
        new[]{"目標者","52","100 / 120","30 / 40","-20","15","18","12","16","9","10","11","長劍"}.CopyTo(fields,0);
        fields[21]="力量戒指";fields[25]="智慧耳環";fields[28]="鋼鐵臂甲";return fields;
    }
    static void QueryReject(Action action,string label){
        try{action();}catch(Exception ex) when(ex is ArgumentException or ProtocolException){Check(true,label);return;}
        throw new Exception("player query accepted invalid input: "+label);
    }
    static Task<bool> QuerySend(LoginConnection c,string text){var s=c.PlayerChatSnapshot();return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor);}
    static async Task QueryWait(Func<bool> done,string label){for(int i=0;i<400&&!done();i++)await Task.Delay(5);Check(done(),label);}
    static List<byte[]> QuerySender(LoginConnection c){
        var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);lock(sent)sent.Add(b);a();return Task.FromResult(true);});return sent;
    }
    static string QueryChat(LoginConnection c)=>string.Join("\n",c.World.PlayerChatLines.Select(l=>l.Text));
    static async Task PlayerQueryChecks(){
        byte[]? Encode(string input,uint? target=456)=>PlayerQueryPackets.Encode(input,123,target);
        Check(Convert.ToHexString(PlayerQueryPackets.Who(""))=="CE00000000000000","query C206 empty S vector");
        Check(Convert.ToHexString(PlayerQueryPackets.Who("中文"))=="CEA4A4A4E5000000","query C206 CP950 S vector");
        Check(Convert.ToHexString(PlayerQueryPackets.Rank(123))=="7D7B0000006C765F72616E6B696E6700","query C125 self lv_ranking vector");
        Check(Convert.ToHexString(PlayerQueryPackets.CheckBegin(123))=="7D7B00000070635F6C69737400000000","query C125 self pc_list vector");
        var target=PlayerQueryPackets.CheckTarget(123,456);
        Check(target.Length==16&&Convert.ToHexString(target).StartsWith("DF7B00000000C8010000")&&target[10..].All(b=>b==0),"query C223 self/type/target vector");
        Check(Convert.ToHexString(NpcPackets.Action(456,"a")).StartsWith("7DC801000061")&&NpcPackets.Talk(456)[0]==34,"query ranking NPC C34/C125 vectors");
        foreach(string input in new[]{"/who","/WHO","/who   "})
            Check(Encode(input)!.SequenceEqual(PlayerQueryPackets.Who("")),"query slash empty name "+input);
        foreach(string input in new[]{"/who 中文","/Who   中文  "})
            Check(Encode(input)!.SequenceEqual(PlayerQueryPackets.Who("中文")),"query slash named player "+input);
        foreach(string input in new[]{"/rank","/RANK  "})Check(Encode(input)!.SequenceEqual(PlayerQueryPackets.Rank(123)),"query slash rank "+input);
        foreach(string input in new[]{"/check","/Check "})Check(Encode(input)!.SequenceEqual(PlayerQueryPackets.CheckBegin(123)),"query slash check "+input);
        Check(PlayerQueryPackets.Who(new string('中',52)).Length==112,"query local name budget counts characters");
        foreach(string input in new[]{"/who a b","/who a\nb","/who\tname","/who a\0b","/who 😀","/who "+new string('中',53),"/rank 1","/rank 1 2 name","/CHECK 123","/check\n"})
            QueryReject(()=>Encode(input),"query invalid command "+input.Length);
        QueryReject(()=>Encode("/check",null),"query check needs selected target");
        QueryReject(()=>Encode("/check",0),"query check rejects zero target");
        foreach(string name in new[]{" ","a\tb","a\0b","😀",new string('a',53)})QueryReject(()=>PlayerQueryPackets.Who(name),"query encoder validation "+name.Length);
        foreach(string input in new[]{"/whoever","/ranked","/checker","/say /who 中文","一般訊息","!喊話","\"對象 密語"})
            Check(Encode(input)==null,"query unrelated chat unchanged "+input);
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage())=="玩家資訊：勇者 測試者 （正義者） [血盟]","query S71/166 fields and alignment");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage("測試者 ($1502)"))!.Contains("中立者"),"query neutral alignment");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage("測試者 ($1503)"))!.Contains("邪惡者"),"query chaotic alignment");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage("<script>\ntext ($1501)"))=="玩家資訊：<script>text （正義者）","query plain text/control sanitization");
        var html=PlayerQueryPackets.DecodeWhoHtml(WhoHtml())!.Value;
        Check(html.ObjectId==123&&html.Lines.Length==9&&html.Lines[0]=="線上人數：42"&&html.Lines[8]=="重啟時間：2026/11/08 10:00","query S39/y_who nine fields");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage(id:125,count:0))==null,"query other S71 falls through");
        Check(PlayerQueryPackets.DecodeWhoMessage(WhoMessage("一般伺服器文字"))==null,"query generic S71/166 is not player information");
        Check(PlayerQueryPackets.DecodeWhoHtml(WhoHtml(html:"npc",flag:0,count:0))==null,"query other S39 falls through");

        var hero=PlayerQueryPackets.DecodeRankHtml(QueryHtml(123,"y_h_2",HeroRows("甲","乙")))!.Value;
        Check(hero.ObjectId==123&&hero.Lines.SequenceEqual(["排名：英雄風雲榜・全職業","No.1 甲","No.2 乙"]),"query S39/y_h_2 eleven fields");
        Check(PlayerQueryPackets.DecodeRankHtml(QueryHtml(456,"y_k_2",RankRows(10,"甲,5")))!.Value.Lines.SequenceEqual(["排名：殺人榜","No.1 甲（5）"]),"query S39/y_k_2 name,count");
        Check(PlayerQueryPackets.DecodeRankHtml(QueryHtml(456,"y_k_3",RankRows(10,"乙,2")))!.Value.Lines.SequenceEqual(["排名：往生榜","No.1 乙（2）"]),"query S39/y_k_3 name,count");
        Check(PlayerQueryPackets.DecodeRankHtml(QueryHtml(456,"y_w_1",RankRows(10,"富豪")))!.Value.Lines.SequenceEqual(["排名：財富風雲榜","No.1 富豪"]),"query S39/y_w_1 ten fields");
        var clanArgs=new string[20];Array.Fill(clanArgs,"");clanArgs[0]="測試盟(3)";clanArgs[10]="對測試盟(3)宣戰";
        Check(PlayerQueryPackets.DecodeRankHtml(QueryHtml(456,"y_c_1",clanArgs))!.Value.Lines.SequenceEqual(["排名：血盟風雲榜","No.1 測試盟(3)"]),"query S39/y_c_1 keeps rows, drops war labels");
        Check(PlayerQueryPackets.DecodeRankHtml(QueryHtml(123,"y_h_2",HeroRows()))!.Value.Lines[1]=="目前沒有排名資料。","query empty ranking is explicit");
        foreach(var other in new[]{QueryHtml(456,"y_h_1","x"),QueryHtml(456,"y_k_1","無限制"),QueryHtml(456,"y_h_2","too few"),QueryHtml(0,"y_w_1",RankRows(10))})
            Check(PlayerQueryPackets.DecodeRankHtml(other)==null,"query non-ranking or incomplete page left to dialog");
        var check=PlayerQueryPackets.DecodeCheckHtml(QueryHtml(123,"pc_show",CheckFields()))!.Value;
        Check(check.ObjectId==123&&check.Lines.SequenceEqual(["目標：目標者｜等級 52","HP 100 / 120｜MP 30 / 40｜AC -20｜MR 15","力量 18｜敏捷 12｜體質 16｜智力 9｜精神 10｜魅力 11","裝備・武器：長劍","裝備・戒指：力量戒指","裝備・耳環：智慧耳環","裝備・臂甲：鋼鐵臂甲"]),"query S39/pc_show twenty-nine fields");

        foreach(var packet in new[]{WhoMessage(),WhoHtml(),QueryHtml(123,"pc_show",CheckFields()),QueryHtml(123,"y_h_2",HeroRows("甲"))}){
            for(int n=0;n<packet.Length;n++){
                byte[] truncated=packet[..n];
                using var c=Seed();int before=c.World.PlayerChatRevision;
                QueryReject(()=>Feed(c,truncated),"query truncated packet "+packet[0]+"/"+n);
                Check(c.World.PlayerChatRevision==before,"query truncation leaves chat intact");
            }
            var bad=packet.ToArray();bad[^1]=1;
            using(var c=Seed()){QueryReject(()=>Feed(c,bad),"query nonzero padding "+packet[0]);Check(c.World.PlayerChatLines.Length==0,"query padding leaves chat intact");}
            using(var c=Seed()){QueryReject(()=>Feed(c,packet.Concat(new byte[8]).ToArray()),"query extra padding "+packet[0]);Check(c.World.PlayerChatLines.Length==0,"query extra padding leaves chat intact");}
        }
        foreach(var malformed in new[]{WhoHtml(id:0),WhoHtml(flag:0),WhoHtml(count:8),WhoHtml(count:10),QueryHtml(0,"pc_show",CheckFields()),QueryHtml(123,"pc_show","短")}){
            using var c=Seed();int revision=c.World.PlayerChatRevision;
            QueryReject(()=>Feed(c,malformed),"query malformed identity/shape");
            Check(c.World.PlayerChatRevision==revision,"query malformed packet has no partial history");
        }
        using(var c=Seed()){
            Feed(c,WhoMessage());Check(c.World.PlayerChatLines.Length==1&&c.World.LastServerReply==null,"query real S71 dispatcher does not acknowledge inventory");
            Feed(c,WhoMessage(id:125,count:0));Check(c.World.LastServerReply?.Id==125&&c.World.PlayerChatLines.Length==1,"query unrelated inventory response preserved");
            Feed(c,WhoHtml());Check(c.World.PlayerChatLines.Length==1&&c.World.QueryVisible&&c.World.QueryHtml=="y_who"&&c.World.QueryPage!.Text.Contains("重啟時間：2026/11/08 10:00"),"query self HTML shown in hypertext page, not chat");
            int revision=c.World.PlayerChatRevision,queryRevision=c.World.QueryRevision;Feed(c,WhoHtml(id:456));Check(c.World.PlayerChatRevision==revision&&c.World.QueryRevision==queryRevision,"query wrong self ignored");
            Feed(c,QueryHtml(456,"pc_show",CheckFields()));Feed(c,QueryHtml(456,"y_h_2",HeroRows("甲")));Check(c.World.PlayerChatRevision==revision&&c.World.QueryRevision==queryRevision,"query other-object check/rank ignored without dialog");
            Call(c.World,"BeginTalk");int npcRevision=c.World.InteractionRevision;
            Feed(c,WhoMessage());Check(c.World.InteractionRevision==npcRevision&&c.World.LastServerReply?.Id==125,"query S71 preserves pending NPC and inventory state");
            Feed(c,WhoHtml());Check(c.World.InteractionRevision==npcRevision&&c.World.InteractionKind=="waiting","query preserves pending NPC dialog");
            Feed(c,WhoHtml(456,"npc",0,0));Check(c.World.InteractionKind=="dialog","query original NPC HTML still dispatched");
            Set(c.World,"Status",c.World.Status! with{Level=51});
            Feed(c,Packet(b=>{b.Write((byte)39);b.Write(123u);Text(b,"RaiseAttr");}));
            Check(c.World.BonusStatsPhase=="offered","query RaiseAttr special S39 preserved");
            Feed(c,Packet(b=>{b.Write((byte)161);b.Write((byte)0);b.Write(0u);Text(b,"已使用萬能藥:0");}));
            Check(c.World.LastServerMessage=="已使用萬能藥:0","query direct S161 response keeps existing information display");
            for(int n=0;n<120;n++)Feed(c,WhoMessage("玩家"+n+" ($1502)"));
            Check(c.World.PlayerChatLines.Length==100&&c.World.PlayerChatLines.Last().Text.Contains("119"),"query shared chat history bounded");
            c.World.Reset();Check(c.World.PlayerChatLines.Length==0,"query logout clears query history");
            Feed(c,WhoHtml());Feed(c,QueryHtml(123,"y_h_2",HeroRows("甲")));Feed(c,QueryHtml(123,"pc_show",CheckFields()));
            Check(c.World.PlayerChatLines.Length==0&&!c.World.QueryVisible,"query HTML before world ready ignored");
        }
        using(var c=Seed()){
            int before=c.World.PlayerChatRevision;Feed(c,WhoMessage("一般伺服器文字"));
            Check(c.World.PlayerChatRevision==before&&c.World.LastServerReply?.Id==166&&c.World.LastServerMessage!.Contains("一般伺服器文字"),"query generic S71/166 keeps server message route");
            Feed(c,WhoMessage(count:0));Check(c.World.PlayerChatRevision==before&&c.World.LastServerReply?.Id==166,"query S71/166 with other argument count does not fault");
            Call(c.World,"BeginTalk");Feed(c,QueryHtml(789,"y_w_1",RankRows(10,"外來")));Check(c.World.PlayerChatRevision==before,"query rank from another NPC ignored");
            Feed(c,QueryHtml(456,"y_h_1"));Check(c.World.PlayerChatRevision==before&&c.World.InteractionKind=="dialog","query rank menu stays dialog only");
            Feed(c,QueryHtml(456,"y_w_1",RankRows(10,"重複")));Check(c.World.PlayerChatRevision==before&&c.World.InteractionKind=="dialog","query unrequested rank page not mirrored");
            Call(c.World,"AwaitInteraction",false);Feed(c,QueryHtml(456,"y_k_2",RankRows(10,"甲,5")));
            Check(!QueryChat(c).Contains("甲")&&c.World.InteractionKind=="dialog"&&c.World.InteractionPage!.Text.Contains("No.1 甲（5）"),"query rank NPC reply shown in dialog, not chat");
            int revision=c.World.PlayerChatRevision;Call(c.World,"AwaitInteraction",false);Feed(c,QueryHtml(456,"y_h_2","too few"));
            Check(c.World.PlayerChatRevision==revision&&c.World.InteractionKind=="dialog","query incomplete rank page still opens dialog");
        }
        using(var c=Seed()){
            var sent=QuerySender(c);
            Check(await QuerySend(c,"/rank")&&sent.Single().SequenceEqual(PlayerQueryPackets.Rank(123)),"query /rank sends self action through chat transport");
            Check(c.World.PlayerChatLines.Length==0,"query rank send does not fabricate result");
            Feed(c,QueryHtml(123,"y_h_2",HeroRows("甲","乙","丙")));
            Check(c.World.PlayerChatLines.Length==0&&c.World.QueryPage!.Text.Contains("排名：英雄風雲榜・全職業")&&c.World.QueryPage.Text.Contains("No.3 丙"),"query /rank result shown in hypertext page");
            Check(c.World.InteractionKind=="closed","query self rank result does not open NPC dialog");
        }
        using(var c=Seed()){
            var sent=QuerySender(c);c.World.SelectTarget(null);
            Check(!await QuerySend(c,"/check")&&sent.Count==0&&c.PlayerChatSnapshot().Notice.Contains("選取"),"query /check without target sends nothing");
            Check(!await QuerySend(c,"/check 456")&&sent.Count==0,"query /check argument sends nothing");
            Feed(c,WhoMessage(PlayerQueryPackets.CheckPrompt));Check(sent.Count==0&&c.World.LastServerMessage!.Contains(PlayerQueryPackets.CheckPrompt),"query unrequested prompt sends nothing");
        }
        using(var c=Seed()){
            var sent=QuerySender(c);
            Check(await QuerySend(c,"/check")&&sent.Single().SequenceEqual(PlayerQueryPackets.CheckBegin(123)),"query /check enters server select mode first");
            await Task.Delay(30);Check(sent.Count==1,"query target waits for server prompt");
            Feed(c,Packet(b=>{b.Write((byte)236);b.Write(123u);b.Write((byte)0);b.Write((byte)0);b.Write((byte)2);}));Check(sent.Count==1,"query S236 alone sends nothing");
            string? message=c.World.LastServerMessage;Feed(c,WhoMessage(PlayerQueryPackets.CheckPrompt));
            await QueryWait(()=>{lock(sent)return sent.Count==2;},"query prompt releases target");
            Check(sent[1].SequenceEqual(PlayerQueryPackets.CheckTarget(123,456))&&c.World.LastServerMessage==message,"query C223 carries selected target; prompt consumed");
            Check(c.World.PlayerChatLines.Length==0,"query check send does not fabricate result");
            Feed(c,QueryHtml(123,"pc_show",CheckFields()));
            Check(c.World.PlayerChatLines.Length==0&&c.World.QueryPage!.Text.Contains("目標：目標者｜等級 52")&&c.World.QueryPage.Text.Contains("裝備・武器：長劍")&&c.World.InteractionKind=="closed","query pc_show shown in hypertext page");
            Feed(c,WhoMessage("目標不存在。"));Check(c.World.PlayerChatLines.Length==0,"query finished check ignores later failure text");
        }
        foreach(string failure in new[]{"目標不存在。","只能對玩家查詢狀態。","查詢失敗。"}){
            using var c=Seed();var sent=QuerySender(c);await QuerySend(c,"/check");Feed(c,WhoMessage(PlayerQueryPackets.CheckPrompt));
            await QueryWait(()=>{lock(sent)return sent.Count==2;},"query failure case target sent");
            Feed(c,WhoMessage(failure));
            Check(QueryChat(c)=="[查詢] "+failure&&c.World.LastServerMessage!.Contains(failure),"query check failure shown and kept in server message route");
        }
        using(var c=Seed()){
            var sent=QuerySender(c);c.PlayerCheckTimeoutMs=10;await QuerySend(c,"/check");
            await QueryWait(()=>c.PlayerChatSnapshot().Notice.Contains("未收到"),"query missing prompt times out");
            Feed(c,WhoMessage(PlayerQueryPackets.CheckPrompt));await Task.Delay(30);
            Check(sent.Count==1&&c.World.LastServerMessage!.Contains(PlayerQueryPackets.CheckPrompt),"query late prompt after timeout sends nothing");
        }
        using(var c=Seed()){
            var sent=QuerySender(c);await QuerySend(c,"/check");c.World.SelectTarget(null);Feed(c,WhoMessage(PlayerQueryPackets.CheckPrompt));
            await QueryWait(()=>c.PlayerChatSnapshot().Notice.Contains("未完成"),"query target change reported");
            Check(sent.Count==1,"query changed target is not sent");
        }
        using(var c=Seed()){
            var s=c.PlayerChatSnapshot();var sent=QuerySender(c);await QuerySend(c,"/check");c.World.Reset();
            Feed(c,WhoMessage(PlayerQueryPackets.CheckPrompt));await Task.Delay(30);
            Check(sent.Count==1,"query session reset blocks pending target");
        }
        using(var c=Seed()){
            var sent=QuerySender(c);
            Check(await QuerySend(c,"/who 中文")&&sent.Single().SequenceEqual(PlayerQueryPackets.Who("中文")),"query sends C206 through actual chat transport");
            Check(c.World.PlayerChatLines.Length==0,"query send does not fabricate result");
            Check(!await QuerySend(c,"/who")&&!await QuerySend(c,"/rank")&&!await QuerySend(c,"/check")&&sent.Count==1,"query shared double submit throttle");
        }
        foreach(string command in new[]{"/who","/rank","/check"}){
            using var c=Seed();var sent=QuerySender(c);await QuerySend(c,command);
            Check(sent.All(b=>b[0] is not (99 or 63)),"query never sends C99/C63 "+command);
        }
        foreach(string guard in new[]{"session","world","actor","dead","queued-world","disconnected"})
        foreach(string command in new[]{"/who","/rank","/check"}){
            using var c=Seed();var s=c.PlayerChatSnapshot();int sends=0;
            if(guard=="dead")Set(c.World,"Status",c.World.Status! with{Hp=0});
            if(guard=="disconnected")Set(c,"State",LoginState.Faulted);
            Sender(c,(b,v,a)=>{if(guard=="queued-world")Feed(c,LoginPackets.Pad([206,4,0,0]));if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.SendPlayerChat(command,s.Generation+(guard=="session"?1:0),s.WorldEpoch+(guard=="world"?1:0),s.Actor+(guard=="actor"?1u:0))&&sends==0,"query stale/admission guard "+guard+" "+command);
        }
    }
}
