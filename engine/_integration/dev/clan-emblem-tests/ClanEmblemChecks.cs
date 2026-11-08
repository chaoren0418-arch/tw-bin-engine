using P06;
using System.Buffers.Binary;

// T135 (#316): emblem ID mapping on top of the #440 cache and paint-time C72. Everything goes through
// World.Receive and the real paint mount LoginConnection.ClanEmblemFor.
partial class Checks {
    const int EmblemRed=24,EmblemBlack=16; // EmblemPacket bit depths decode to different pixels.
    static readonly int RedPixel=unchecked((int)0xffff0000),BlackPixel=unchecked((int)0xff000000);
    static byte[] EmblemAssignment(uint objectId,uint emblemId)=>Packet(b=>{b.Write((byte)64);b.Write(ClanEmblemProtocol.AssignmentSubtype);b.Write(objectId);b.Write(emblemId);});
    static byte[] ClanPack(uint id,uint clanId,string clan)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(id);b.Write((ushort)(id==123?0:1110));b.Write((byte)0);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);Text(b,id==123?"offline":"target");Text(b,"");b.Write((byte)4);b.Write(clanId);Text(b,clan);Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] EmblemAnnouncement(string clan,uint emblemId)=>Packet(b=>{b.Write((byte)167);Text(b,clan);Text(b,"盟主");b.Write(emblemId);b.Write(123456u);ClanFixed(b,"公告",478);});
    static List<byte[]> EmblemSender(LoginConnection c){
        var sent=new List<byte[]>();Sender(c,(b,current,admit)=>{if(!current())return Task.FromResult(false);admit();sent.Add(b);return Task.FromResult(true);});return sent;
    }
    static byte[] C72(uint id){var bytes=new byte[8];bytes[0]=72;BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1),id);return bytes;}
    // Own character 123 in clan 700 "本盟", peer 456 in clan 800 "客盟".
    static LoginConnection EmblemSeed(){
        var c=Seed();Set(c.World,"MapId",(ushort)1);Feed(c,ClanPack(123,700,"本盟"));Feed(c,ClanPack(456,800,"客盟"));return c;
    }
    static async Task ClanEmblemChecks(){
        string? oldDisk=ClanEmblemCache.DiskPath;ClanEmblemCache.DiskPath=null;
        try{
            var value=ClanEmblemProtocol.DecodeAssignment(LoginPackets.Pad(Convert.FromHexString("403c15ea7a0033b60000")));
            Check(value==new ClanEmblemAssignment(0x7aea15,0xb633),"S64/0x3c C C D objid D emblem matches the server's sample bytes");
            foreach(var bad in new[]{LoginPackets.Pad([64,0x3c,1,0,0,0]),EmblemAssignment(0,5),LoginPackets.Pad([64,0x2a,1,0,0,0,2,0,0,0])}){
                bool failed=false;try{ClanEmblemProtocol.DecodeAssignment(bad);}catch(ProtocolException){failed=true;}Check(failed,"reject malformed S64/0x3c "+Convert.ToHexString(bad));
            }
            EmblemD287Checks();EmblemQueryChecks();EmblemIdentityChecks();EmblemLifetimeChecks();
        }finally{ClanEmblemCache.DiskPath=oldDisk;}
        await Task.CompletedTask;
    }
    // What the d287 JAR emits: emblem ID is always zero, pushes are keyed by clan ID.
    static void EmblemD287Checks(){
        using var c=EmblemSeed();var sent=EmblemSender(c);
        Feed(c,EmblemPacket(700));Check(c.ClanEmblemFor(123,700)?.Pixels[0]==RedPixel&&sent.Count==0,"login/upload S118 shows through the S87 clan ID without C72");
        Check(c.ClanEmblemFor(456,800)==null&&sent.Count==1&&sent[0].SequenceEqual(C72(800)),"no published emblem ID falls back to the original clan-ID C72 (#440)");
        Feed(c,EmblemAnnouncement("本盟",0));Feed(c,EmblemAssignment(123,0));Feed(c,EmblemAssignment(456,0));
        Check(c.World.PublishedClanEmblemId(123)==0&&c.World.PublishedClanEmblemId(456)==0,"zero emblem ID publishes nothing");
        Check(c.ClanEmblemFor(123,700)?.Pixels[0]==RedPixel&&sent.Count==1,"zero emblem ID keeps the clan-keyed image and never sends C72 0");
        Feed(c,EmblemAssignment(123,700));Feed(c,EmblemAssignment(123,0));Check(c.World.PublishedClanEmblemId(123)==0,"C_Attr's transient clan ID is retracted by the zero that follows it");
        var unhandled=(System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(c.World)!;
        Check(!unhandled.ContainsKey(64),"S64/0x3c is no longer reported as unhandled");
        Feed(c,LoginPackets.Pad([64,0x3c]));Check(unhandled.ContainsKey(64)&&c.World.Ready,"short S64/0x3c stays unhandled and does not drop the connection");
        unhandled.Clear();Feed(c,LoginPackets.Pad([64,0x2a,0,0]));Check(unhandled.ContainsKey(64),"other S64 subtypes keep their existing dispatch");
    }
    static void EmblemQueryChecks(){
        foreach(bool own in new[]{true,false}){
            using var c=EmblemSeed();var sent=EmblemSender(c);string label=own?" S167":" S64/0x3c";uint holder=own?123u:456u,clanId=own?700u:800u;
            Feed(c,own?EmblemAnnouncement("本盟",9001):EmblemAssignment(456,9001));
            Check(c.World.PublishedClanEmblemId(holder)==9001,"emblem ID is bound to its holder"+label);
            Check(c.ClanEmblemFor(holder,clanId)==null&&sent.Count==1&&sent[0].SequenceEqual(C72(9001)),"unknown emblem sends exact C72 emblem ID, not the clan ID"+label);
            Check(c.ClanEmblemFor(holder,clanId)==null&&sent.Count==1,"pending query is not repeated"+label);
            Feed(c,EmblemPacket(9001));Check(c.ClanEmblemFor(holder,clanId)?.Id==9001&&sent.Count==1,"C72 reply is shown for the holder and not queried again"+label);
            var copy=c.ClanEmblemFor(holder,clanId)!;copy.Pixels[0]=0;Check(c.ClanEmblemFor(holder,clanId)!.Pixels[0]!=0,"mapped emblem snapshot cannot mutate cache"+label);
        }
        using(var c=EmblemSeed()){
            var sent=EmblemSender(c);Feed(c,EmblemAssignment(456,9001));c.ClanEmblemFor(456,800);c.ClanEmblemFor(456,800);
            Check(sent.Count==1,"server's silent not-found is not retried");
            Feed(c,LoginPackets.Pad([118,0x29,0x23,0,0,1]));
            Check(c.World.ClanNotice.Contains("無法解析")&&c.ClanEmblemFor(456,800)==null&&sent.Count==1,"unsupported C72 reply keeps the name, shows no image, is not re-requested");
        }
    }
    // Clan IDs and emblem IDs are separate number spaces.
    static void EmblemIdentityChecks(){
        using(var c=EmblemSeed()){
            var sent=EmblemSender(c);Feed(c,EmblemPacket(800,EmblemBlack));Feed(c,EmblemAssignment(456,9001));
            Check(c.ClanEmblemFor(456,800)?.Pixels[0]==BlackPixel&&sent.Count==1&&sent[0].SequenceEqual(C72(9001)),"clan-keyed push still shows while the published emblem ID is being asked for");
            Feed(c,EmblemPacket(9001,EmblemRed));Check(c.ClanEmblemFor(456,800)?.Pixels[0]==RedPixel,"published emblem ID's image takes over once received");
            Check(c.World.PublishedClanEmblemId(123)==0&&c.ClanEmblemFor(123,700)==null,"one character's emblem ID does not leak to another clan");
        }
        using(var c=EmblemSeed()){
            var sent=EmblemSender(c);Feed(c,EmblemAssignment(456,9001));
            Feed(c,ClanUpdatePacket(456,"別盟"));Check(c.World.PublishedClanEmblemId(456)==0,"S72 clan change retires the old emblem ID");
            Feed(c,ClanPack(456,900,"別盟"));Check(c.World.PublishedClanEmblemId(456)==0,"old emblem ID does not follow the character into its new clan");
            c.ClanEmblemFor(456,900);Check(sent.Count==1&&sent[0].SequenceEqual(C72(900)),"after the clan change the new clan is asked for, not the old emblem ID");
        }
        using(var c=EmblemSeed()){
            Feed(c,EmblemAssignment(999,9001));Feed(c,ClanPack(456,0,""));Feed(c,EmblemAssignment(456,9002));
            Check(c.World.PublishedClanEmblemId(999)==0&&c.World.PublishedClanEmblemId(456)==0,"an emblem ID without a visible clan holder is not recorded");
            Feed(c,EmblemAnnouncement("別盟",9003));Check(c.World.PublishedClanEmblemId(123)==0,"S167 of another clan does not bind to the actor");
        }
        using(var c=EmblemSeed()){
            for(uint i=1;i<=ClanEmblemProtocol.MaxAssignments+40;i++){Feed(c,ClanPack(10000+i,800,"客盟"));Feed(c,EmblemAssignment(10000+i,i));}
            var published=(System.Collections.IDictionary)typeof(WorldSession).GetField("publishedClanEmblems",F)!.GetValue(c.World)!;
            Check(published.Count==ClanEmblemProtocol.MaxAssignments,"server-supplied assignments are bounded");
        }
    }
    static void EmblemLifetimeChecks(){
        using var c=EmblemSeed();var sent=EmblemSender(c);
        Feed(c,EmblemAssignment(456,9001));c.ClanEmblemFor(456,800);Feed(c,EmblemPacket(9001));
        Feed(c,LoginPackets.Pad([206,4,0,0]));Feed(c,ClanPack(456,800,"客盟"));
        Check(c.ClanEmblemFor(456,800)?.Id==9001&&sent.Count==1,"map change keeps the mapping the server will not resend");
        var published=(System.Collections.IDictionary)typeof(WorldSession).GetField("publishedClanEmblems",F)!.GetValue(c.World)!;
        c.World.Reset();Check(published.Count==0,"session reset clears the emblem ID mapping");
        Check(c.World.FindClanEmblem(9001)!=null,"session reset keeps the #440 image cache");
    }
}
