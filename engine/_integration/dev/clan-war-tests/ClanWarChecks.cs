using P06;

partial class Checks {
    static byte[] WarPacket(byte type,string first,string second)=>Packet(b=>{b.Write((byte)84);b.Write(type);Text(b,first);Text(b,second);});
    static byte[] WarPrompt(ushort message,string clan="乙盟",string? leader="乙主",uint reserved=0,ushort header=0)=>Packet(b=>{
        b.Write((byte)219);b.Write(header);b.Write(reserved);b.Write(message);Text(b,clan);if(leader!=null)Text(b,leader);
    });
    static byte[] WarReply(ushort message,params string[] args)=>Packet(b=>{b.Write((byte)71);b.Write(message);b.Write((byte)args.Length);foreach(var s in args)Text(b,s);});
    static LoginConnection WarSeed(string own="甲盟",string other="乙盟"){
        var c=Seed();Feed(c,ClanUpdatePacket(123,own));Feed(c,ClanUpdatePacket(456,other));return c;
    }
    static void WarAdmit(LoginConnection c,List<byte[]> sent)=>Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
    static void BadWar(LoginConnection c,byte[] packet,string label){
        var before=c.ClanWarSnapshot();bool refused=false;
        try{Feed(c,packet);}catch(ProtocolException){refused=true;}
        var after=c.ClanWarSnapshot();
        Check(refused&&after.Revision==before.Revision&&after.Prompt==before.Prompt&&after.Enemies.SequenceEqual(before.Enemies),"clan war malformed atomic: "+label);
    }
    static async Task ClanWarChecks(){
        void RejectEncode(Action action,string label){bool failed=false;try{action();}catch(ArgumentException){failed=true;}Check(failed,label);}
        // C227: C subtype, S clan name, 8-byte transport padding.
        foreach(var (command,type) in new[]{(ClanWarCommand.Declare,(byte)0),(ClanWarCommand.Surrender,(byte)2),(ClanWarCommand.Cease,(byte)3)}){
            var bytes=ClanWarProtocol.Encode(command,"測試盟");var r=new LoginReader(bytes);
            Check(r.C()==227&&r.C()==type&&r.S()=="測試盟","C227 C subtype S Big5 name "+command);r.Finish();
            Check(bytes.Length%8==0,"C227 padded "+command);
        }
        Check(ClanWarProtocol.Encode(ClanWarCommand.Cease,"ab").SequenceEqual(new byte[]{227,3,(byte)'a',(byte)'b',0,0,0,0}),"C227 exact bytes");
        RejectEncode(()=>ClanWarProtocol.Encode(ClanWarCommand.Declare,""),"C227 empty name");
        RejectEncode(()=>ClanWarProtocol.Encode(ClanWarCommand.Declare," "),"C227 space is the castle path, not a clan");
        RejectEncode(()=>ClanWarProtocol.Encode(ClanWarCommand.Declare,"a b"),"C227 embedded space");
        RejectEncode(()=>ClanWarProtocol.Encode(ClanWarCommand.Declare,"a\0b"),"C227 NUL injection");
        RejectEncode(()=>ClanWarProtocol.Encode(ClanWarCommand.Declare,"😀"),"C227 unmappable name");
        RejectEncode(()=>ClanWarProtocol.Encode(ClanWarCommand.Declare,new string('a',33)),"C227 bounded name");
        RejectEncode(()=>ClanWarProtocol.Encode((ClanWarCommand)1,"ab"),"C227 subtype 1 is not a client command");
        // C121: H0 D0 H message C answer; D is never a request ID.
        foreach(ushort message in new ushort[]{217,221,222})foreach(bool yes in new[]{false,true})
            Check(ClanWarProtocol.Answer(message,yes).SequenceEqual(new byte[]{121,0,0,0,0,0,0,(byte)message,0,(byte)(yes?1:0),0,0,0,0,0,0}),$"C121 H0 D0 H{message} answer {yes}");
        RejectEncode(()=>ClanWarProtocol.Answer(252,true),"C121 refuses non-war message");
        // S84 every documented subtype.
        foreach(byte type in new byte[]{1,2,3,4,6,7,8}){
            var value=ClanWarProtocol.Decode(WarPacket(type,"甲盟","乙盟"));
            Check(value==new ClanWarEvent(type,"甲盟","乙盟"),"S84 C S S layout "+type);
            Check(ClanWarProtocol.Describe(value,"甲盟").Contains("乙盟"),"S84 text names the other clan "+type);
        }
        using(var c=WarSeed()){
            Check(c.World.ClanWarEnemies.Length==0&&!c.World.IsClanWarEnemy("乙盟")&&!c.World.IsClanWarEnemy(""),"no war before S84");
            Feed(c,WarPacket(1,"甲盟","乙盟"));Check(c.World.IsClanWarEnemy("乙盟")&&!c.World.IsClanWarEnemy("甲盟"),"S84/1 own attacker marks defender only");
            Check(c.World.Objects.Single(o=>o.ObjectId==456).Clan=="乙盟"&&c.World.IsClanWarEnemy(c.World.Objects.Single(o=>o.ObjectId==456).Clan),"enemy query matches visible object clan");
            Feed(c,WarPacket(1,"甲盟","乙盟"));Check(c.World.ClanWarEnemies.Length==1,"repeat S84/1 stays one war");
            Check(c.World.IsClanWarEnemy("乙盟".ToUpperInvariant()),"clan compare follows server equalsIgnoreCase");
            Feed(c,LoginPackets.Pad([206,4,0,0]));Check(c.World.IsClanWarEnemy("乙盟"),"map change keeps server war state");
            Feed(c,WarPacket(3,"乙盟","甲盟"));Check(!c.World.IsClanWarEnemy("乙盟"),"S84/3 ends war in either name order");
        }
        foreach(byte end in new byte[]{2,3,4})using(var c=WarSeed()){
            Feed(c,WarPacket(1,"乙盟","甲盟"));Check(c.World.IsClanWarEnemy("乙盟"),"S84/1 own defender marks attacker");
            Feed(c,WarPacket(end,"甲盟","乙盟"));Check(!c.World.IsClanWarEnemy("乙盟")&&c.ClanWarSnapshot().Notice.Contains("乙盟"),"S84 ends war with subtype "+end);
        }
        using(var c=WarSeed()){
            // L1War.requestSimWar surrender order, as both clans see it.
            Feed(c,WarPacket(1,"甲盟","乙盟"));Feed(c,WarPacket(2,"甲盟","乙盟"));Feed(c,WarPacket(4,"乙盟","甲盟"));Feed(c,WarPacket(3,"甲盟","乙盟"));
            Check(c.World.ClanWarEnemies.Length==0&&c.ClanWarSnapshot().Notice.Contains("結束"),"surrender sequence 2/4/3 leaves no war");
        }
        using(var c=WarSeed()){
            Feed(c,WarPacket(8,"甲盟","乙盟"));Check(c.World.IsClanWarEnemy("乙盟")&&c.ClanWarSnapshot().Notice=="目前你的血盟與 乙盟 血盟交戰當中。","S84/8 login state");
            Feed(c,WarPacket(6,"甲盟","丙盟"));Feed(c,WarPacket(7,"甲盟","丙盟"));Check(c.World.ClanWarEnemies.SequenceEqual(new[]{"乙盟"}),"S84/6 and 7 are alliance text, not war state");
            Feed(c,WarPacket(1,"丙盟","丁盟"));Check(c.World.ClanWarEnemies.SequenceEqual(new[]{"乙盟"})&&!c.World.IsClanWarEnemy("丙盟"),"third-party war is not an enemy");
            Feed(c,WarPacket(1,"甲盟","NPC"));Check(!c.World.IsClanWarEnemy("NPC"),"ownerless castle pseudo clan is not an enemy");
            Feed(c,WarPacket(1,"甲盟","甲盟"));Check(!c.World.IsClanWarEnemy("甲盟"),"self war never marks own clan");
            int unknown=c.ClanWarSnapshot().Revision;Feed(c,WarPacket(5,"甲盟","丙盟"));
            Check(c.ClanWarSnapshot().Revision==unknown&&!c.World.IsClanWarEnemy("丙盟"),"unknown S84 subtype changes no state");
            var map=(System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(c.World)!;
            Check(map.GetValueOrDefault((byte)84)==1,"unknown S84 subtype is counted");
            for(int i=0;i<300;i++)Feed(c,WarPacket(1,"甲"+i,"乙"+i));Check(c.World.ClanWarEnemies.SequenceEqual(new[]{"乙盟"}),"third-party S84 broadcasts cannot evict own war");
            for(int i=0;i<300;i++)Feed(c,WarPacket(1,"甲盟","敵"+i));Check(c.World.ClanWarEnemies.Length==256,"own war list is bounded");
        }
        using(var c=WarSeed()){
            Feed(c,WarPacket(1,"甲盟","乙盟"));Feed(c,ClanUpdatePacket(123,""));
            Check(c.World.ClanWarEnemies.Length==0,"leaving clan clears enemy view");
            Feed(c,ClanUpdatePacket(123,"甲盟"));Check(c.World.ClanWarEnemies.Length==0,"rejoining cannot revive an unconfirmed war");
        }
        using(var c=WarSeed()){
            Feed(c,WarPacket(1,"甲盟","乙盟"));c.World.Reset();Check(c.World.ClanWarEnemies.Length==0,"reset clears war");
        }
        using(var c=WarSeed()){
            Feed(c,WarPacket(1,"甲盟","乙盟"));Check(c.World.IsClanWarEnemy("乙盟"),"war before relogin");
            var status=c.World.Status;c.World.Reset();
            Feed(c,Packet(b=>{b.Write((byte)223);b.Write(Convert.FromHexString("03158B7B94F02F"));}));
            Set(c.World,"Status",status);Feed(c,Pack(123));Feed(c,ClanUpdatePacket(123,"甲盟"));
            Check(!c.World.IsClanWarEnemy("乙盟"),"relogin without a query between Reset and EnterGame still clears");
            Feed(c,WarPacket(8,"甲盟","乙盟"));Check(c.World.IsClanWarEnemy("乙盟"),"relogin restores war from S84/8");
        }
        using(var c=WarSeed()){
            foreach(var sample in new[]{WarPacket(1,"甲盟","乙盟"),WarPrompt(217),WarPrompt(221,leader:null)}){
                for(int n=0;n<sample.Length;n++)if(sample[..n].Length>0)BadWar(c,sample[..n],"truncated "+sample[0]+"/"+n);
                var dirty=sample.ToArray();dirty[^1]=255;BadWar(c,dirty,"nonzero padding "+sample[0]);
                BadWar(c,[..sample,..new byte[8]],"extra zero block "+sample[0]);
            }
            BadWar(c,WarPacket(1,"","乙盟"),"S84 empty first");BadWar(c,WarPacket(1,"甲盟"," "),"S84 blank second");BadWar(c,WarPacket(1,"甲\u0001盟","乙盟"),"S84 control name");
            BadWar(c,WarPrompt(217,reserved:7),"S219 requestId path refused");BadWar(c,WarPrompt(217,header:1),"S219 header");
            BadWar(c,WarPrompt(217,""),"S219 empty clan");BadWar(c,WarPrompt(217,leader:""),"S219 217 needs leader");BadWar(c,WarPrompt(221,"",null),"S219 221 empty clan");
            BadWar(c,WarPrompt(221,"乙盟","多餘"),"S219 221 has one string");
        }
        foreach(var (message,leader) in new[]{((ushort)217,"乙主"),((ushort)221,(string?)null),((ushort)222,(string?)null)})foreach(bool yes in new[]{false,true})using(var c=WarSeed()){
            var sent=new List<byte[]>();WarAdmit(c,sent);Feed(c,WarPacket(1,"乙盟","甲盟"));var enemies=c.World.ClanWarEnemies;
            Feed(c,WarPrompt(message,leader:leader));var view=c.ClanWarSnapshot();
            Check(view.Prompt is {} p&&p.Message==message&&p.Clan=="乙盟"&&p.Leader==(leader??"")&&view.CanAnswer&&!view.CanRequest,"S219 prompt "+message);
            Check(view.Notice.Contains("乙盟"),"prompt text names clan "+message);
            Check(await c.AnswerClanWar(view,yes)&&sent.Count==1&&sent[0].SequenceEqual(ClanWarProtocol.Answer(message,yes)),$"answer exact C121 {message} {yes}");
            Check(c.ClanWarSnapshot().Prompt==null&&!await c.AnswerClanWar(view,yes)&&sent.Count==1,"answer only once "+message);
            Check(c.World.ClanWarEnemies.SequenceEqual(enemies),"answer does not predict war state "+message);
        }
        foreach(string mutation in new[]{"replacement","trade","marriage","clan","party","duel","world","death","interaction","clan-change","cancel"})using(var c=WarSeed()){
            Feed(c,WarPrompt(217));var view=c.ClanWarSnapshot();int sends=0;
            Sender(c,(b,v,a)=>{
                switch(mutation){
                    case "replacement":Feed(c,WarPrompt(222,"丙盟",null));break;
                    case "trade":Feed(c,DuelPrompt("trader",252,12));break;
                    case "marriage":Feed(c,DuelPrompt("target",654));break;
                    case "clan":Feed(c,DuelPrompt("target",97));break;
                    case "party":Feed(c,DuelPrompt("target",953,1));break;
                    case "duel":Feed(c,DuelPrompt("target",630));break;
                    case "world":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                    case "death":Set(c.World,"OwnDead",true);break;
                    case "interaction":Set(c.World,"InteractionRevision",c.World.InteractionRevision+1);break;
                    case "clan-change":Feed(c,ClanUpdatePacket(123,"新盟"));break;
                    case "cancel":c.Cancel();break;
                }
                if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);
            });
            Check(!await c.AnswerClanWar(view,true)&&sends==0,"queued war answer invalidated by "+mutation);
        }
        using(var c=WarSeed()){
            Feed(c,DuelPrompt("trader",252,12));Check(c.World.TradeState=="invited","trade invited before war prompt");
            Feed(c,WarPrompt(217));Check(c.World.TradeState=="attention"&&c.ClanWarSnapshot().Prompt!=null,"war prompt expires older trade invitation");
        }
        using(var c=WarSeed()){
            Feed(c,DuelPrompt("邀請者"));Feed(c,WarPrompt(217));Check(c.World.DuelSnapshot.Phase=="expired"&&c.ClanWarSnapshot().Prompt!=null,"war prompt expires older duel invitation");
        }
        using(var c=WarSeed()){
            Feed(c,ClanUpdatePacket(123,""));Feed(c,WarPrompt(217));Check(c.ClanWarSnapshot().Prompt==null,"clanless character cannot answer");
        }
        using(var c=WarSeed()){
            Set(c.World,"OwnDead",true);Feed(c,WarPrompt(217));Check(c.ClanWarSnapshot().Prompt==null,"dead character prompt not kept");
        }
        foreach(var (command,name) in new[]{(ClanWarCommand.Declare,"乙盟"),(ClanWarCommand.Surrender,"乙盟"),(ClanWarCommand.Cease,"乙盟")})using(var c=WarSeed()){
            var sent=new List<byte[]>();WarAdmit(c,sent);var view=c.ClanWarSnapshot();
            Check(view.CanRequest&&await c.SendClanWar(command,view,name)&&sent.Count==1&&sent[0].SequenceEqual(ClanWarProtocol.Encode(command,name)),"memory transport admits "+command);
            Check(c.World.ClanWarEnemies.Length==0&&c.ClanWarSnapshot().Notice.Contains("已送出"),"request does not invent war "+command);
        }
        using(var c=WarSeed()){
            var sent=new List<byte[]>();WarAdmit(c,sent);
            Check(!await c.SendClanWar(ClanWarCommand.Declare,c.ClanWarSnapshot(),"甲盟")&&sent.Count==0&&c.ClanWarSnapshot().Notice.Contains("自己"),"own clan refused with a notice");
            Check(!await c.SendClanWar(ClanWarCommand.Declare,c.ClanWarSnapshot(),"")&&sent.Count==0&&c.ClanWarSnapshot().Notice.Contains("空白"),"blank target refused with a notice");
            Feed(c,WarPrompt(221,leader:null));Check(!await c.SendClanWar(ClanWarCommand.Declare,c.ClanWarSnapshot(),"乙盟")&&sent.Count==0,"pending prompt blocks new request");
        }
        using(var c=WarSeed("")){
            var sent=new List<byte[]>();WarAdmit(c,sent);var view=c.ClanWarSnapshot();
            Check(!view.CanRequest&&!await c.SendClanWar(ClanWarCommand.Declare,view,"乙盟")&&sent.Count==0,"clanless character sends nothing");
        }
        using(var c=WarSeed()){
            var release=new TaskCompletionSource();int sent=0;Sender(c,async(b,v,a)=>{await release.Task;if(!v())return false;sent++;a();return true;});
            var stamp=c.ClanWarSnapshot();var pending=c.SendClanWar(ClanWarCommand.Declare,stamp,"乙盟");
            Check(!await c.SendClanWar(ClanWarCommand.Declare,stamp,"乙盟")&&!c.ClanWarSnapshot().CanRequest,"double submit refused");
            Feed(c,LoginPackets.Pad([206,4,0,0]));release.SetResult();Check(!await pending&&sent==0,"queued request refused after world change");
            Check((int)(typeof(LoginConnection).GetField("clanWarSendingGeneration",F)!.GetValue(c)!)==-1,"sending lease released");
        }
        using(var c=WarSeed()){
            Sender(c,(_,_,_)=>throw new IOException("synthetic failure"));
            Check(!await c.SendClanWar(ClanWarCommand.Declare,c.ClanWarSnapshot(),"乙盟")&&c.State==LoginState.Faulted,"send failure disconnects without retry");
        }
        using(var c=WarSeed()){
            Sender(c,(_,_,_)=>throw new IOException("synthetic failure"));Feed(c,WarPrompt(217));
            Check(!await c.AnswerClanWar(c.ClanWarSnapshot(),true)&&c.State==LoginState.Faulted,"answer failure disconnects without retry");
        }
        // Rejection replies stay on the shared S71 path.
        using(var c=WarSeed()){
            foreach(var (id,args,text) in new[]{((ushort)236,new[]{"乙盟"},"乙盟 血盟拒絕你的宣戰。"),((ushort)237,new[]{"乙盟"},"乙盟 血盟拒絕你的提案。"),((ushort)218,new[]{"乙盟"},"乙盟 血盟君主不在線上。"),((ushort)234,Array.Empty<string>(),"你的血盟已經在戰爭中。"),((ushort)478,Array.Empty<string>(),"只有王子和公主才能宣戰。"),((ushort)232,Array.Empty<string>(),"等級 25 以下的君主無法宣戰。")}){
                Feed(c,WarReply(id,args));Check(c.World.LastServerReply?.Id==id&&ClanWarProtocol.ServerResult(c.World.LastServerReply)==text,"S71 result text "+id);
            }
            Feed(c,WarReply(125));Check(ClanWarProtocol.ServerResult(c.World.LastServerReply)==null,"unrelated S71 is not a war result");
        }
        // Existing S219 routes still reach their modules.
        using(var c=WarSeed()){
            Feed(c,DuelPrompt("trader",252,12));Check(c.World.TradeState=="invited"&&c.World.TradePeerName=="trader","S219/252 still reaches Trade");
        }
        using(var c=WarSeed()){
            Call(c.World,"UpdateObject",456u,(Func<WorldObject,WorldObject>)(o=>o with{Flags=4}));
            Feed(c,DuelPrompt("target",97));Check(c.World.PendingClanInvitation?.Name=="target","S219/97 still reaches Clan");
            Feed(c,WarPrompt(217));Check(c.World.PendingClanInvitation==null&&c.ClanWarSnapshot().Prompt!=null,"war prompt expires older clan invitation");
        }
    }
}
