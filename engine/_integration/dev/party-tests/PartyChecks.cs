using P06;
using System.Text.Json;

partial class Checks {
    // Every synthetic server packet enters the product's WorldSession.Receive via Feed.
    static byte[] PartyRowPacket(byte sub,uint id=456,string name="隊員",byte hp=75)=>Packet(b=>{b.Write((byte)250);b.Write(sub);if(sub==104)b.Write((byte)1);b.Write(id);Text(b,name);b.Write(hp);b.Write(4u);b.Write((ushort)32601);b.Write((ushort)32940);});
    static byte[] PartyYesNo(ushort message=953,string name="邀請者",uint challenge=77)=>Packet(b=>{b.Write((byte)219);b.Write((ushort)0);b.Write(challenge);b.Write(message);Text(b,name);});
    static byte[] PartyListPacket(string leader="offline",string names="offline 隊員 ",uint owner=123)=>Packet(b=>{b.Write((byte)39);b.Write(owner);Text(b,"party");b.Write((ushort)1);b.Write((ushort)2);Text(b,leader);Text(b,names);});
    static byte[] PartyMessage(ushort code,params string[] args)=>Packet(b=>{b.Write((byte)71);b.Write(code);b.Write((byte)args.Length);foreach(string s in args)Text(b,s);});
    static byte[] PartyHp(uint id,byte hp)=>Packet(b=>{b.Write((byte)237);b.Write(id);b.Write(hp);});
    static void PartyBad(LoginConnection c,byte[] bytes,string label){
        string before=JsonSerializer.Serialize(c.PartySnapshot());bool rejected=false;
        try{Feed(c,bytes);}catch(ProtocolException){rejected=true;}
        Check(rejected,label+" rejected");Check(before==JsonSerializer.Serialize(c.PartySnapshot()),label+" atomic");
    }
    static async Task PartyChecks(){
        foreach(byte mode in new byte[]{0,1})Check(PartyPackets.Invite(0x12345678,mode).SequenceEqual(new byte[]{230,mode,0x78,0x56,0x34,0x12,0,0}),"party C230 mode+target "+mode);
        Check(PartyPackets.Leave().SequenceEqual(new byte[]{33,0,0,0,0,0,0,0}),"party C33 empty");
        Check(PartyPackets.List().SequenceEqual(new byte[]{43,0,0,0,0,0,0,0}),"party C43 empty");
        Check(PartyPackets.Kick("隊員").SequenceEqual(Packet(b=>{b.Write((byte)255);Text(b,"隊員");})),"party C255 Big5 NUL name");
        foreach(bool yes in new[]{false,true})Check(PartyPackets.Answer(0x12345678,yes).SequenceEqual(new byte[]{121,0,0,0x78,0x56,0x34,0x12,0xB9,3,(byte)(yes?1:0),0,0,0,0,0,0}),"party C121 953 envelope "+yes);
        foreach(string name in new[]{"","bad name","a\0b",new string('a',53)}){bool rejected=false;try{PartyPackets.Kick(name);}catch(ArgumentException){rejected=true;}Check(rejected,"party unsafe kick name");}
        using(var c=Seed()){
            Feed(c,PartyRowPacket(105));var v=c.PartySnapshot();Check(v.Members.Single() is {Name:"隊員",HpPercent:75,MapId:4}&&v.CanKick,"party 105 leader member+HP");
            Feed(c,PartyRowPacket(104,789,"新隊員",100));Check(c.PartySnapshot().Members.Length==2,"party 104 merges existing members");
            Feed(c,PartyRowPacket(104,123,"offline",100));Check(c.PartySnapshot().Members.Length==2,"party erroneous self row ignored");
            Feed(c,PartyHp(456,0));Check(c.PartySnapshot().Members.Single(m=>m.ObjectId==456).HpPercent==0,"party S237 zero is dead not removed");
            Check(c.World.Target?.HpByte==0,"party S237 preserves combat target HP dispatch");
            Feed(c,PartyHp(999,55));Check(c.PartySnapshot().Members.Length==2,"party unknown HPMeter not membership");
            Feed(c,Packet(b=>{b.Write((byte)250);b.Write((byte)110);b.Write((byte)1);b.Write(456u);b.Write(7u);b.Write((ushort)12);b.Write((ushort)13);}));
            Check(c.PartySnapshot().Members.Single(m=>m.ObjectId==456) is {MapId:7,X:12,Y:13,HpPercent:0},"party 110 location preserves HP");
            Feed(c,Packet(b=>{b.Write((byte)250);b.Write((byte)106);b.Write(456u);Text(b,"隊員");}));Check(c.PartySnapshot().Leader=="隊員"&&!c.PartySnapshot().CanKick,"party 106 leader authority");
            Feed(c,PartyHp(789,255));Check(c.PartySnapshot().Members.Length==1,"party S237 FF removes member");
            Feed(c,PartyListPacket("隊員","offline 隊員 遠方 "));Check(c.PartySnapshot().Members.Single(m=>m.Name=="遠方").HpPercent==null,"party S39 name only HP unknown");
            Feed(c,PartyListPacket());Check(c.PartySnapshot().Members.Length==1&&c.PartySnapshot().CanKick,"party S39 authoritative removes old name");
            var stale=c.PartySnapshot();Feed(c,PartyListPacket(owner:999));Check(c.PartySnapshot().Revision==stale.Revision,"party foreign list ignored");
            PartyBad(c,PartyHp(456,101),"party invalid HP");
            var tail=PartyHp(456,80);tail[^1]=1;PartyBad(c,tail,"party nonzero padding");
            PartyBad(c,PartyRowPacket(105)[..^1],"party truncated packet");
            PartyBad(c,PartyListPacket(names:"offline 隊員 隊員 "),"party duplicate names");
            PartyBad(c,LoginPackets.Pad([250,104,8]),"party excessive count");
            PartyBad(c,PartyRowPacket(105,id:0),"party zero id");
            Feed(c,PartyMessage(420,"隊員"));Check(c.PartySnapshot().Members.Length==0&&c.World.LastServerReply?.Id==420,"party departure + existing message dispatch");
            foreach(ushort code in new ushort[]{418,419,425}){Feed(c,PartyRowPacket(105));Feed(c,PartyMessage(code));Check(c.PartySnapshot().Members.Length==0&&c.PartySnapshot().Leader=="","party clear message "+code);}
        }
        using(var c=MarriageSeed()){
            // S219 is one server-side slot: neither prompt may stay answerable after the other arrives.
            Feed(c,MarriageInvite());Check(c.MarriageSnapshot() is {Phase:"invited",CanAnswer:true},"party shared Yes-No marriage invited");
            Feed(c,PartyYesNo());Check(c.PartySnapshot().Invitation!=null&&c.MarriageSnapshot() is {Phase:"expired",CanAnswer:false},"party S219/953 expires older marriage invitation");
            Feed(c,MarriageInvite());Check(c.PartySnapshot().Invitation==null&&c.MarriageSnapshot().Phase=="invited","party S219/654 expires older party invitation");
        }
        using(var c=Seed()){
            // Shared opcodes must still reach their original consumers with an untouched reader.
            Feed(c,PartyRowPacket(105));string before=JsonSerializer.Serialize(c.PartySnapshot());
            Feed(c,LoginPackets.Pad([250,99]));
            var unhandled=(System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(c.World)!;
            Check(unhandled.GetValueOrDefault((byte)250)==1,"party unrelated S250 retains unhandled dispatch");
            Feed(c,LoginPackets.Pad([250,34,60,0]));
            Check(c.World.MagicBuffs.Any(b=>b.Source=="packetbox34"&&b.Seconds==60),"party unrelated S250 retains magic status dispatch");
            Call(c.World,"BeginTalk");
            Feed(c,Packet(b=>{b.Write((byte)39);b.Write(456u);Text(b,"test-party-fallback");b.Write((ushort)0);b.Write((ushort)0);}));
            Check(c.World.InteractionKind=="dialog"&&c.World.InteractionPage!=null,"party unrelated S39 retains NPC dialog dispatch");
            c.World.CloseInteraction();
            Feed(c,PartyMessage(125));Check(c.World.LastServerReply?.Id==125,"party unrelated S71 retains server reply dispatch");
            Check(before==JsonSerializer.Serialize(c.PartySnapshot()),"party unrelated shared opcodes preserve roster and revision");
            for(ushort code=414;code<=426;code++){
                Feed(c,PartyMessage(code,code is 420 or 424?["隊員"]:[]));
                Check(c.World.LastServerReply?.Id==code,"party S71 still reaches original server reply handler "+code);
            }
        }
        using(var c=Seed()){
            Feed(c,PartyRowPacket(105));Feed(c,PartyYesNo());var old=c.PartySnapshot();
            // Reuse the same WorldSession and character ID. Deliberately do not read a snapshot
            // while offline: the real receive path must perform the cleanup before EnterGame.
            c.World.Reset();
            Feed(c,Convert.FromHexString("DF03158B7B94F02F"));
            var status=RewardTests.Status(1,0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(1),123);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(16),100);Feed(c,status);
            Feed(c,MapPacket(0));Feed(c,Pack(123));c.World.ProbeSent=true;Feed(c,TimePacket(5400));
            var fresh=c.PartySnapshot();
            Check(c.World.Ready&&fresh.Owner==old.Owner,"party same character completes real re-entry packets");
            Check(fresh.Members.Length==0&&fresh.Leader==""&&fresh.Invitation==null&&fresh.Revision>old.Revision,"party re-entry clears roster leader and invitation without offline snapshot");
            int sends=0;Sender(c,(b,current,admitted)=>{if(!current())return Task.FromResult(false);sends++;admitted();return Task.FromResult(true);});
            Check(!await c.SendParty("accept",old)&&!await c.SendParty("kick",old,"隊員")&&sends==0,"party pre-login operations cannot replay after re-entry");
            Feed(c,PartyHp(456,80));Check(c.PartySnapshot().Members.Length==0,"party old HP cannot recreate previous login roster");
            Feed(c,PartyRowPacket(105,789,"重登隊員",60));
            Check(c.PartySnapshot().Members.Single() is {ObjectId:789,Name:"重登隊員",HpPercent:60}&&c.PartySnapshot().CanKick,"party re-entry accepts fresh server roster");
        }
        using(var c=Seed()){
            int sends=0;byte[]? sent=null;Sender(c,(b,current,admitted)=>{if(!current())return Task.FromResult(false);sends++;sent=b;admitted();return Task.FromResult(true);});
            Feed(c,PartyYesNo());var v=c.PartySnapshot();Check(v.Invitation is {Name:"邀請者",Challenge:77},"party 953 invitation");
            Check(await c.SendParty("accept",v)&&sends==1&&sent!.SequenceEqual(PartyPackets.Answer(77,true)),"party accept sends one C121");
            Check(c.PartySnapshot().Members.Length==0&&!await c.SendParty("accept",v),"party no optimistic roster / replay");
            Feed(c,PartyYesNo());Check(await c.SendParty("reject",c.PartySnapshot())&&sends==2&&sent!.SequenceEqual(PartyPackets.Answer(77,false)),"party reject C121");
            Feed(c,PartyRowPacket(105));v=c.PartySnapshot();Check(await c.SendParty("kick",v,"隊員")&&sent!.SequenceEqual(PartyPackets.Kick("隊員"))&&c.PartySnapshot().Members.Length==1,"party kick waits for server");
            Feed(c,PartyMessage(420,"隊員"));Check(c.PartySnapshot().Members.Length==0&&c.World.LastServerReply?.Id==420,"party kick reply updates roster via Receive");
            Feed(c,PartyRowPacket(105));
            Check(await c.SendParty("leave",c.PartySnapshot())&&sent![0]==33&&c.PartySnapshot().Members.Length==1,"party leave waits for server");
            Feed(c,PartyMessage(418));Check(c.PartySnapshot().Members.Length==0&&c.PartySnapshot().Leader==""&&c.World.LastServerReply?.Id==418,"party leader leave reply clears roster via Receive");
            Feed(c,PartyRowPacket(105));
            Check(await c.SendParty("list",c.PartySnapshot())&&sent![0]==43,"party list request");
            Feed(c,PartyYesNo());Feed(c,PartyYesNo(252,"target"));Check(c.PartySnapshot().Invitation==null,"party other Yes-No invalidates old prompt");
            Check(c.World.TradeState=="invited"&&c.World.TradePeerName=="target","party unrelated Yes-No retains trade dispatch");
            Call(c.World,"ResetTrade","test");Feed(c,PartyYesNo());Set(c.World,"WorldEpoch",c.World.WorldEpoch+1);Check(c.PartySnapshot().Invitation==null&&c.PartySnapshot().Members.Length==1,"party map transition invalidates prompt preserves roster");
            long now=0;Set(c.World,"PartyClock",(Func<long>)(()=>now));Feed(c,PartyYesNo());now=30000;Check(c.PartySnapshot().Invitation==null,"party invitation expires");
            c.Cancel();Check(c.PartySnapshot().Members.Length==0,"party cancel clears through snapshot");
        }
        using(var c=Seed()){
            // Replace synthetic target's flag with a player flag; no live actors or socket involved.
            byte[] actor=Pack(456,32601);var reader=new LoginReader(actor);reader.C();reader.H();reader.H();reader.D();reader.H();for(int i=0;i<4;i++)reader.C();reader.D();reader.H();reader.S();reader.S();
            actor[actor.Length-reader.Remaining]=4;Feed(c,actor);c.World.SelectTarget(456);
            Sender(c,(b,current,admitted)=>{Check(b.SequenceEqual(PartyPackets.Invite(456,1)),"party selected target wire");if(!current())return Task.FromResult(false);admitted();return Task.FromResult(true);});
            Check(await c.SendParty("invite",c.PartySnapshot(),target:456,mode:1),"party invite selected player");
            var pending=new TaskCompletionSource<bool>();int sends=0;Sender(c,async(b,current,admitted)=>{await pending.Task;if(!current())return false;sends++;admitted();return true;});
            var send=c.SendParty("list",c.PartySnapshot());Check(!await c.SendParty("list",c.PartySnapshot()),"party concurrent request blocked");
            c.Cancel();pending.SetResult(true);Check(!await send&&sends==0,"party generation gate blocks queued send after cancel");
        }
    }
}
