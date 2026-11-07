using P06;
using System.Buffers.Binary;

partial class Checks {
    const int EmblemRed=24,EmblemBlack=16; // EmblemPacket bit depths decode to different pixels.
    static readonly int RedPixel=unchecked((int)0xffff0000),BlackPixel=unchecked((int)0xff000000);
    static byte[] EmblemAssignment(uint objectId,uint emblemId)=>Packet(b=>{b.Write((byte)64);b.Write(ClanEmblemProtocol.AssignmentSubtype);b.Write(objectId);b.Write(emblemId);});
    static byte[] ClanPack(uint id,uint clanId,string clan)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(id);b.Write((ushort)(id==123?0:1110));b.Write((byte)0);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);Text(b,id==123?"offline":"target");Text(b,"");b.Write((byte)4);b.Write(clanId);Text(b,clan);Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] EmblemAnnouncement(string clan,uint emblemId)=>Packet(b=>{b.Write((byte)167);Text(b,clan);Text(b,"盟主");b.Write(emblemId);b.Write(123456u);ClanFixed(b,"公告",478);});
    static Task Pump(LoginConnection c)=>(Task)typeof(LoginConnection).GetMethod("PumpWorldAsync",F)!.Invoke(c,[c.SessionGeneration,CancellationToken.None])!;
    static List<byte[]> EmblemSender(LoginConnection c){
        var sent=new List<byte[]>();Sender(c,(b,current,admit)=>{if(!current())return Task.FromResult(false);admit();sent.Add(b);return Task.FromResult(true);});return sent;
    }
    static byte[] C72(uint id){var bytes=new byte[8];bytes[0]=72;BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1),id);return bytes;}
    // Own character in clan 700 "本盟", peer in clan 800 "客盟". Off map 0 the real pump
    // skips ship travel, whose appearance profile needs game assets that CI does not have.
    static LoginConnection EmblemSeed(out long[] clock){
        var c=Seed();var now=new long[]{1000};clock=now;Set(c,"ActionClock",(Func<long>)(()=>now[0]));Set(c.World,"MapId",(ushort)1);
        Feed(c,ClanPack(123,700,"本盟"));Feed(c,ClanPack(456,800,"客盟"));return c;
    }
    static async Task ClanEmblemChecks(){
        var value=ClanEmblemProtocol.DecodeAssignment(LoginPackets.Pad(Convert.FromHexString("403c15ea7a0033b60000")));
        Check(value==new ClanEmblemAssignment(0x7aea15,0xb633),"S64/0x3c C C D objid D emblem matches the server's sample bytes");
        foreach(var bad in new[]{LoginPackets.Pad([64,0x3c,1,0,0,0]),EmblemAssignment(0,5),LoginPackets.Pad([64,0x2a,1,0,0,0,2,0,0,0])}){
            bool failed=false;try{ClanEmblemProtocol.DecodeAssignment(bad);}catch(ProtocolException){failed=true;}Check(failed,"reject malformed S64/0x3c "+Convert.ToHexString(bad));
        }
        await EmblemD287Checks();await EmblemQueryChecks();await EmblemIdentityChecks();await EmblemLifetimeChecks();await EmblemGateChecks();
    }
    // What the d287 JAR actually emits: emblem ID is always zero; pushes are keyed by clan ID.
    static async Task EmblemD287Checks(){
        using var c=EmblemSeed(out _);var sent=EmblemSender(c);
        await Pump(c);Check(sent.Count==0,"S87 clan ID alone never becomes a C72 query");
        Feed(c,EmblemPacket(700));Check(c.World.FindClanEmblem(700)?.Pixels[0]==RedPixel,"login/upload S118 maps through S87 clan ID");
        Check(c.World.FindClanEmblem(800)==null,"own clan's image is not shown on another clan");
        Feed(c,EmblemAnnouncement("本盟",0));Feed(c,EmblemAssignment(123,0));Feed(c,EmblemAssignment(456,0));await Pump(c);
        Check(sent.Count==0&&c.World.FindClanEmblem(700)!=null,"zero emblem ID never sends C72 and keeps the clan-keyed image");
        Feed(c,EmblemAssignment(123,700));Feed(c,EmblemAssignment(123,0));await Pump(c);
        Check(sent.Count==0,"C_Attr's transient clan ID is retracted by the zero that follows it");
        var unhandled=(System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(c.World)!;
        Check(!unhandled.ContainsKey(64),"S64/0x3c is no longer reported as unhandled");
        Feed(c,LoginPackets.Pad([64,0x3c]));Check(unhandled.ContainsKey(64)&&c.World.Ready,"short S64/0x3c stays unhandled and does not drop the connection");
        unhandled.Clear();Feed(c,LoginPackets.Pad([64,0x2a,0,0]));Check(unhandled.ContainsKey(64),"other S64 subtypes keep their existing dispatch");
    }
    static async Task EmblemQueryChecks(){
        foreach(bool own in new[]{true,false}){
            using var c=EmblemSeed(out _);var sent=EmblemSender(c);string label=own?" S167":" S64/0x3c";uint clanId=own?700u:800u;
            Feed(c,own?EmblemAnnouncement("本盟",9001):EmblemAssignment(456,9001));
            await Pump(c);Check(sent.Count==1&&sent[0].SequenceEqual(C72(9001)),"unknown emblem sends exact C72 emblem ID"+label);
            await Pump(c);Feed(c,own?EmblemAnnouncement("本盟",9001):EmblemAssignment(456,9001));await Pump(c);Check(sent.Count==1,"open query is not repeated"+label);
            Feed(c,EmblemPacket(9001));Check(c.World.FindClanEmblem(clanId)?.Id==9001,"C72 reply maps emblem ID back to the clan"+label);
            Check(c.World.FindClanEmblem(9001)==null,"emblem ID is not usable as a clan ID"+label);
            await Pump(c);Feed(c,own?EmblemAnnouncement("本盟",9001):EmblemAssignment(456,9001));await Pump(c);Check(sent.Count==1,"cached emblem is not queried again"+label);
            var copy=c.World.FindClanEmblem(clanId)!;copy.Pixels[0]=0;Check(c.World.FindClanEmblem(clanId)!.Pixels[0]!=0,"mapped emblem snapshot cannot mutate cache"+label);
        }
        using(var c=EmblemSeed(out var clock)){
            var sent=EmblemSender(c);Feed(c,EmblemAssignment(456,9001));await Pump(c);
            clock[0]+=ClanEmblemProtocol.ReplyWindowMs;await Pump(c);Check(sent.Count==1,"server's silent not-found is cached, not retried");
            Feed(c,EmblemPacket(9001));Check(c.World.FindClanEmblem(800)==null&&c.World.FindClanEmblem(9001)!=null,"S118 after the reply window is treated as clan-keyed");
        }
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);Feed(c,EmblemAssignment(456,9001));await Pump(c);Feed(c,LoginPackets.Pad([118,0x29,0x23,0,0,1]));
            Check(c.World.ClanNotice.Contains("無法解析")&&c.World.FindClanEmblem(800)==null,"unsupported C72 reply keeps the name and shows no image");
            await Pump(c);Check(sent.Count==1,"unsupported reply is not re-requested");
        }
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);
            for(uint i=1;i<=ClanEmblemProtocol.MaxQueries+40;i++){Feed(c,EmblemAssignment(456,i));await Pump(c);}
            Check(sent.Count==ClanEmblemProtocol.MaxQueries,"server-supplied IDs cannot grow queries past the session bound");
        }
    }
    // Equal numbers from the two namespaces must stay two different emblems.
    static async Task EmblemIdentityChecks(){
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);Feed(c,EmblemPacket(700,EmblemRed));Feed(c,EmblemAssignment(456,700));await Pump(c);
            Check(sent.Count==1&&sent[0].SequenceEqual(C72(700)),"clan-keyed cache does not answer an equal emblem ID");
            Feed(c,EmblemPacket(700,EmblemBlack));
            Check(c.World.FindClanEmblem(700)?.Pixels[0]==RedPixel&&c.World.FindClanEmblem(800)?.Pixels[0]==BlackPixel,"equal clan ID and emblem ID keep separate images");
            Feed(c,EmblemPacket(800,EmblemRed));Check(c.World.FindClanEmblem(800)?.Pixels[0]==RedPixel,"newer clan-keyed upload replaces an older C72 image");
        }
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);Feed(c,EmblemPacket(800,EmblemBlack));Feed(c,EmblemAssignment(456,9001));await Pump(c);Feed(c,EmblemPacket(9001,EmblemRed));
            Check(c.World.FindClanEmblem(800)?.Pixels[0]==RedPixel,"newer C72 image replaces an older clan-keyed image");
        }
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);Feed(c,EmblemAssignment(456,9001));await Pump(c);Feed(c,EmblemPacket(9001));
            Feed(c,ClanUpdatePacket(456,"別盟"));Check(c.World.FindClanEmblem(800)==null,"S72 clan change retires the old emblem ID");
            Feed(c,ClanPack(456,900,"別盟"));Check(c.World.FindClanEmblem(900)==null,"old emblem ID does not follow the character into its new clan");
            Feed(c,ClanPack(456,800,"客盟"));Feed(c,EmblemAssignment(456,0));Check(c.World.FindClanEmblem(800)==null,"zero S64/0x3c retires the emblem ID");
        }
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);Feed(c,EmblemAssignment(999,9001));Feed(c,ClanPack(456,0,""));Feed(c,EmblemAssignment(456,9002));await Pump(c);
            Check(sent.Count==0,"an emblem ID without a visible clan holder is not queried");
            Feed(c,ClanPack(456,800,"客盟"));Feed(c,EmblemAssignment(456,9003));Feed(c,Packet(b=>{b.Write((byte)120);b.Write(456u);}));await Pump(c);
            Check(sent.Count==0,"a holder that left view is not queried");
        }
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);Feed(c,EmblemAnnouncement("別盟",9001));await Pump(c);Check(sent.Count==0,"S167 of another clan does not bind to the actor");
        }
    }
    static async Task EmblemLifetimeChecks(){
        using var c=EmblemSeed(out _);var sent=EmblemSender(c);
        Feed(c,EmblemPacket(700));Feed(c,EmblemAssignment(456,9001));await Pump(c);Feed(c,EmblemPacket(9001));
        Feed(c,LoginPackets.Pad([206,4,0,0]));Feed(c,ClanPack(123,700,"本盟"));Feed(c,ClanPack(456,800,"客盟"));
        Check(c.World.FindClanEmblem(700)!=null&&c.World.FindClanEmblem(800)!=null,"map change keeps emblems the server will not resend");
        await Pump(c);Check(sent.Count==1,"map change does not repeat a finished query");
        c.World.Reset();Check(c.World.FindClanEmblem(700)==null,"session reset clears the emblem cache");
    }
    static async Task EmblemGateChecks(){
        using(var c=EmblemSeed(out _)){
            var sent=EmblemSender(c);Set(c,"State",LoginState.EnteringWorld);Feed(c,EmblemAssignment(456,9001));await Pump(c);Check(sent.Count==0,"no C72 before the character is in the world");
            Set(c,"State",LoginState.InWorld);await Pump(c);Check(sent.Count==1,"query waits for world entry instead of being lost");
        }
        foreach(string reason in new[]{"retracted","disconnect"})using(var c=EmblemSeed(out _)){
            var release=new TaskCompletionSource();int sends=0;Sender(c,async(b,current,admit)=>{await release.Task;if(!current())return false;sends++;admit();return true;});
            Feed(c,EmblemAssignment(456,9001));var pending=Pump(c);
            if(reason=="retracted")Feed(c,EmblemAssignment(456,0));else c.Cancel();
            release.SetResult();await pending;Check(sends==0,"queued C72 rejected after "+reason);
        }
        using(var c=EmblemSeed(out _)){
            bool first=true;var sent=new List<byte[]>();Sender(c,(b,current,admit)=>{if(first){first=false;return Task.FromResult(false);}if(!current())return Task.FromResult(false);admit();sent.Add(b);return Task.FromResult(true);});
            Feed(c,EmblemAssignment(456,9001));await Pump(c);await Pump(c);Check(sent.Count==1,"a query the gate refused is not counted as asked");
        }
    }
}
