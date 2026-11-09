using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
namespace P06;

// R2-K（T264／#625）離線測試：合成封包、記憶體送出閘門；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
// 涵蓋：C63 階級設定、C18 盟徽上傳（含 S118 回推的 padding）、上傳檔 BMP、S250/173 顯示盟徽旗標與訊息 3383、
// 入盟 97 與盟戰 221／222 進共用詢問槽（217 留在盟戰窗）。
public sealed partial class WorldSession {
    internal void SeedClanFollowupTest(string clan){
        Entered=true;MapId=0;TimeReplies=1;CacheEnabled=false;
        Status=new(ClanFollowupTests.Own,1,0,10,10,10,10,10,10,100u,100,100,100,10,0,0,0,0,0,0,0,0,0);
        Actor=new(32600,32940,ClanFollowupTests.Own,61,0,0,0,0,0,0,ClanFollowupTests.Name,"",0,0,"","",0,100,0,1,0,0,0);
        Actor=Actor with{Clan=clan,ClanId=clan.Length>0?9u:0u};OwnDead=false;
    }
    internal void ClanFollowupRecruiter(uint id){if(objects.TryGetValue(id,out var o))objects[id]=o with{Flags=4};}
}
public sealed partial class LoginConnection {
    internal void SeedClanFollowupTest(string clan,List<byte[]> sent){
        World.SeedClanFollowupTest(clan);State=LoginState.InWorld;
        sendMovementGate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
    }
}

static class ClanFollowupTests {
    public const uint Own=123,Peer=456;
    public const string Name="clan-followup";
    static byte[] Packet(Action<BinaryWriter> write){using var m=new MemoryStream();using(var b=new BinaryWriter(m,Encoding.UTF8,true))write(b);return LoginPackets.Pad(m.ToArray());}
    static void Text(BinaryWriter b,string s){b.Write(LoginPackets.TextEncoding.GetBytes(s));b.Write((byte)0);}
    static byte[] Ask(ushort message,params string[] args)=>Packet(b=>{b.Write((byte)219);b.Write((ushort)0);b.Write(0u);b.Write(message);foreach(var s in args)Text(b,s);});
    // Synthetic S87 for an adjacent player (same layout as YesNoFollowupTests; values are test data).
    static byte[] Player(uint id,ushort x,ushort y,string name)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1);
        b.Write((byte)0);b.Write((byte)4);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);
        Text(b,name);Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");
        b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static LoginConnection World(string clan,List<byte[]> sent){var c=new LoginConnection();c.SeedClanFollowupTest(clan,sent);return c;}
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static JsonElement Snap(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("clanEmblemStatus");
    static int UnhandledSubtype(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).TryGetProperty("unhandledSubtypes",out var u)&&u.TryGetProperty(key,out var v)?v.GetInt32():0;
    // 16x12 24-bit bottom-up BMP, every pixel (r,g,b).
    static byte[] Bmp24(byte red,byte green,byte blue){
        const int stride=48,size=54+stride*12;var d=new byte[size];
        d[0]=(byte)'B';d[1]=(byte)'M';BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(2),size);BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(10),54);
        BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(14),40);BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(18),16);BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(22),12);
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(26),1);BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(28),24);
        for(int i=0;i<16*12;i++){int p=54+(i/16)*stride+(i%16)*3;d[p]=blue;d[p+1]=green;d[p+2]=red;}
        return d;
    }

    [WorldModuleTest("clan-followup")]
    static void Run(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){Rank(t);Upload(t);EmblemStatus(t);JoinSlot(t);WarSlot(t);}
    }

    static void Rank(WorldModuleTestContext t){
        var p=ClanProtocol.EncodeRank(5,"成員");
        t.Check(p.SequenceEqual(LoginPackets.Pad([63,1,5,..LoginPackets.TextEncoding.GetBytes("成員"),0])),"C63 C(1) C(rank) S(name)");
        t.Equal(0,p.Length%8,"C63 padded");
        foreach(byte bad in new byte[]{0,1,11,255})t.Throws<ArgumentException>(()=>ClanProtocol.EncodeRank(bad,"成員"),"rank "+bad+" rejected (server range 2–10)");
        t.Throws<ArgumentException>(()=>ClanProtocol.EncodeRank(2,""),"empty member name rejected");
        t.Throws<ArgumentException>(()=>ClanProtocol.EncodeRank(2,"a b"),"whitespace member name rejected");
        t.Equal("守護騎士",ClanProtocol.RankName(9),"rank 9 name");
        t.Equal("副君主",ClanProtocol.RankName(3),"rank 3 name");
        // The refactored name check keeps the old Create/Ban wording and limits.
        t.Throws<ArgumentException>(()=>ClanProtocol.Encode(ClanCommand.Create,new string('a',17)),"create name over 16 chars");
        t.Check(ClanProtocol.Encode(ClanCommand.Ban,"甲").SequenceEqual(LoginPackets.Pad([69,..LoginPackets.TextEncoding.GetBytes("甲"),0])),"C69 unchanged");

        var sent=new List<byte[]>();using var c=World("測試盟",sent);
        t.Check(Wait(c.SendClanRank(c.ClanSnapshot(),"成員",6))&&sent.Count==1&&sent[0].SequenceEqual(ClanProtocol.EncodeRank(6,"成員")),"SendClanRank sends one C63");
        t.Check(c.ClanSnapshot().Notice.Contains("設定階級請求已送出"),"rank notice");
        t.Check(!Wait(c.SendClanRank(c.ClanSnapshot(),"成員",1))&&sent.Count==1&&c.ClanSnapshot().Notice.Contains("2–10"),"out-of-range rank not sent");
        var none=new List<byte[]>();using var n=World("",none);
        t.Check(!Wait(n.SendClanRank(n.ClanSnapshot(),"成員",6))&&none.Count==0&&n.ClanSnapshot().Notice.Contains("你沒有血盟"),"no clan: C63 not sent");
        var stale=c.ClanSnapshot();c.World.SeedClanFollowupTest("別盟");
        t.Check(!Wait(c.SendClanRank(stale,"成員",6))&&sent.Count==1,"clan changed after the snapshot: not sent");
    }

    static void Upload(WorldModuleTestContext t){
        var pixels=new int[192];pixels[0]=0;pixels[1]=unchecked((int)0xffff0000);pixels[2]=unchecked((int)0xff00ff00);pixels[3]=unchecked((int)0xff0000ff);pixels[4]=0x00ffffff;
        for(int i=5;i<192;i++)pixels[i]=unchecked((int)0xffffffff);
        var p=ClanEmblemProtocol.EncodeUpload(pixels);
        t.Equal(392,p.Length,"C18 = opcode + 384 bytes, padded to 8");
        t.Equal((byte)18,p[0],"C18 opcode");
        ushort At(int i)=>BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(1+i*2));
        t.Equal((ushort)0,At(0),"transparent → 0");
        t.Equal((ushort)0x7C00,At(1),"red → 0x7C00 (R high)");
        t.Equal((ushort)0x03E0,At(2),"green → 0x03E0");
        t.Equal((ushort)0x001F,At(3),"blue → 0x001F");
        t.Equal((ushort)0,At(4),"alpha 0 → transparent");
        t.Equal((ushort)0x7FFF,At(5),"white → 0x7FFF");
        t.Throws<ArgumentException>(()=>ClanEmblemProtocol.EncodeUpload(new int[191]),"wrong pixel count rejected");
        // The server keeps the whole C18 body (incl. padding) and pushes it back as S118 D(id)+body.
        var body=p.AsSpan(1).ToArray();
        var echo=LoginPackets.Pad([118,..BitConverter.GetBytes(77u),..body]);
        var back=ClanProtocol.DecodeEmblem(echo);
        t.Check(back.Id==77&&back.Pixels[0]==0&&back.Pixels[1]==unchecked((int)0xffff0000)&&back.Pixels[5]==unchecked((int)0xffffffff),"S118 echo of an uploaded body decodes (384 bytes + zero padding)");
        var dirty=(byte[])echo.Clone();dirty[5+384]=1;
        t.Throws<ProtocolException>(()=>ClanProtocol.DecodeEmblem(dirty),"non-zero bytes after 384 rejected");
        // Upload file: 16x12 BMP only.
        var file=ClanEmblemProtocol.ReadUploadFile(Bmp24(255,0,0));
        t.Check(file.Length==192&&file.All(x=>x==unchecked((int)0xffff0000)),"16x12 24-bit BMP file → pixels");
        t.Check(ClanEmblemProtocol.EncodeUpload(file).Skip(1).Take(384).SequenceEqual(Enumerable.Range(0,192).SelectMany(_=>new byte[]{0x00,0x7C})),"BMP → RGB555 body");
        t.Throws<ArgumentException>(()=>ClanEmblemProtocol.ReadUploadFile(Bmp24(1,2,3)[..60]),"truncated BMP rejected");
        t.Throws<ArgumentException>(()=>ClanEmblemProtocol.ReadUploadFile(Encoding.ASCII.GetBytes("not a bitmap file at all, definitely more than fifty-four bytes long")),"non-BMP rejected");

        var sent=new List<byte[]>();using var c=World("測試盟",sent);
        t.Check(Wait(c.UploadClanEmblem(c.ClanSnapshot(),file))&&sent.Count==1&&sent[0].SequenceEqual(ClanEmblemProtocol.EncodeUpload(file)),"UploadClanEmblem sends one C18");
        t.Check(c.ClanSnapshot().Notice.Contains("上傳盟徽請求已送出"),"upload notice");
        t.Check(!Wait(c.UploadClanEmblem(c.ClanSnapshot(),new int[3]))&&sent.Count==1,"bad pixels not sent");
    }

    static void EmblemStatus(WorldModuleTestContext t){
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry has no errors",WorldExtensionRegistry.Default.Errors);
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==250&&e.Subtype==173&&e.Method=="WorldSession.ReceiveClanEmblemStatus"),"S250/173 registered");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="clanEmblemStatus"),"snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetClanEmblemStatus"),"reset registered");

        var s=new WorldSession();
        t.Check(s.ClanShowEmblem==null,"flag unknown before S250/173");
        // 3.81 S_PacketBox: C(173) C(1) C(show) D(0).
        s.Receive(LoginPackets.Pad([250,173,1,1,0,0,0,0]),"");
        t.Check(s.ClanShowEmblem==true,"type 1 value 1 → show");
        t.Equal(0,UnhandledSubtype(s,"0xFA/0xAD"),"S250/173 consumed");
        s.Receive(LoginPackets.Pad([250,173,1,0,0,0,0,0]),"");
        t.Check(s.ClanShowEmblem==false,"type 1 value 0 → hide");
        // TW16 reads only C C; the padded 4-byte body is the same bytes.
        s.Receive(LoginPackets.Pad([250,173,1,2]),"");
        t.Check(s.ClanShowEmblem==true,"value ≠ 0 → show (sub_B30990(value≠0))");
        var a=Snap(s);
        t.Equal(3,a.GetProperty("packets").GetInt32(),"packet count");
        t.Check(a.GetProperty("showEmblem").GetBoolean(),"snapshot flag");
        t.Equal(JsonValueKind.Null,a.GetProperty("notice").ValueKind,"no notice yet");

        s.Receive(LoginPackets.Pad([250,173,130,0,0,0,0,0]),"");
        t.Check(s.ClanEmblemStatusNotice is {Id:3383},"type 130 → message 3383");
        t.Equal(ServerMessageText.Decode(3383,[]).Text,Snap(s).GetProperty("notice").GetProperty("text").GetString(),"3383 text via the string table path");
        t.Check(s.LastServerMessage==null&&s.LastServerReply==null,"3383 does not touch LastServerMessage");
        t.Check(s.ClanShowEmblem==true,"type 130 keeps the flag");

        s.Receive(LoginPackets.Pad([250,173,7,1,0,0,0,0]),"");
        t.Check(UnhandledSubtype(s,"0xFA/0xAD")==1||WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty("0xFA",out _),"unknown type counted as unhandled");
        t.Check(s.ClanShowEmblem==true,"unknown type leaves the flag");
        var bad=LoginPackets.Pad([250,173,1,1,0,0,0,0,9]);
        t.Throws<ProtocolException>(()=>s.Receive(bad,""),"non-zero padding rejected");
        t.Throws<ProtocolException>(()=>ClanEmblemStatusPackets.Parse(LoginPackets.Pad([250,171,1,1])),"wrong subtype rejected");

        s.Reset();
        t.Check(s.ClanShowEmblem==null&&s.ClanEmblemStatusNotice==null&&Snap(s).GetProperty("packets").GetInt32()==0,"reset clears");
    }

    static LoginConnection Recruiting(List<byte[]> sent){
        var c=World("測試盟",sent);c.World.Receive(Player(Peer,32601,32940,"target"),"");c.World.ClanFollowupRecruiter(Peer);return c;
    }

    static void JoinSlot(WorldModuleTestContext t){
        foreach(bool accept in new[]{true,false}){
            var sent=new List<byte[]>();using var c=Recruiting(sent);
            c.World.Receive(Ask(97,"target"),"");
            var v=c.YesNoSnapshot();
            t.Check(v.Prompt is {Message:97,RequestId:0,Deadline:null}&&v.Prompt.Arguments.SequenceEqual(["target"]),"S219/97 queued in the shared slot "+accept);
            t.Check(c.World.PendingClanInvitation?.Name=="target","module still admits the invitation "+accept);
            t.Check(Wait(c.AnswerYesNo(v,accept?"y":"n"))&&sent.Count==1&&sent[0].SequenceEqual(ClanProtocol.Answer(accept)),"Y/N answers the same C121 H0 D0 H97 C "+accept);
            t.Check(c.YesNoSnapshot().Prompt==null&&c.World.PendingClanInvitation==null,"slot answer retires the module invitation "+accept);
            t.Check(c.ClanSnapshot().Notice.Contains("輸入列"),"module notice says it was answered on the input line "+accept);
            t.Check(!Wait(c.AnswerClanInvitation(c.ClanSnapshot(),accept))&&sent.Count==1,"no second answer through the module "+accept);
        }
        {
            var sent=new List<byte[]>();using var c=Recruiting(sent);
            c.World.Receive(Ask(97,"target"),"");var view=c.ClanSnapshot();
            t.Check(Wait(c.AnswerClanInvitation(view,true))&&sent.Count==1,"module answer still works");
            t.Check(c.YesNoSnapshot().Prompt==null,"module answer clears the shared slot");
        }
        {
            var sent=new List<byte[]>();using var c=Recruiting(sent);
            c.World.Receive(Ask(97,"target"),"");var v=c.YesNoSnapshot();
            c.World.Receive(Packet(b=>{b.Write((byte)120);b.Write(Peer);}),"");
            t.Check(c.World.PendingClanInvitation==null&&c.YesNoSnapshot().Prompt==null,"applicant leaves: invitation and slot both retired");
            t.Check(!Wait(c.AnswerYesNo(v,"y"))&&sent.Count==0,"stale slot answer not sent");
        }
        {
            var sent=new List<byte[]>();using var c=Recruiting(sent);
            c.World.Receive(Ask(97,"target"),"");c.World.Receive(Ask(1000,"x"),"");
            t.Check(c.World.PendingClanInvitation==null&&c.YesNoSnapshot().Prompt?.Message==1000,"newer S219 replaces the join question");
        }
        {
            var sent=new List<byte[]>();using var c=Recruiting(sent);
            c.World.Receive(Ask(97,"missing"),"");
            t.Check(c.World.PendingClanInvitation==null&&c.YesNoSnapshot().Prompt==null,"unknown applicant: not admitted, not queued");
        }
    }

    static void WarSlot(WorldModuleTestContext t){
        foreach(ushort message in new ushort[]{221,222})foreach(bool accept in new[]{true,false}){
            var sent=new List<byte[]>();using var c=World("甲盟",sent);
            c.World.Receive(Ask(message,"乙盟"),"");
            var v=c.YesNoSnapshot();
            t.Check(v.Prompt is {} p&&p.Message==message&&p.Arguments.SequenceEqual(["乙盟"]),$"S219/{message} queued in the shared slot {accept}");
            t.Check(c.ClanWarSnapshot().Prompt?.Message==message,$"war module keeps prompt {message}");
            t.Check(Wait(c.AnswerYesNo(v,accept))&&sent.Count==1&&sent[0].SequenceEqual(ClanWarProtocol.Answer(message,accept)),$"slot answer = C121 H0 D0 H{message} C {accept}");
            t.Check(c.ClanWarSnapshot().Prompt==null&&c.YesNoSnapshot().Prompt==null,$"slot answer retires war prompt {message} {accept}");
        }
        {
            var sent=new List<byte[]>();using var c=World("甲盟",sent);
            c.World.Receive(Ask(222,"乙盟"),"");
            t.Check(Wait(c.AnswerClanWar(c.ClanWarSnapshot(),false))&&sent.Count==1&&c.YesNoSnapshot().Prompt==null,"war window answer clears the shared slot");
        }
        {
            var sent=new List<byte[]>();using var c=World("甲盟",sent);
            c.World.Receive(Packet(b=>{b.Write((byte)219);b.Write((ushort)0);b.Write(0u);b.Write((ushort)217);Text(b,"乙盟");Text(b,"乙王");}),"");
            t.Check(c.ClanWarSnapshot().Prompt?.Message==217&&c.YesNoSnapshot().Prompt==null,"217 (S_ASK special case) stays in the war window");
            t.Check(!WorldSession.ClanWarUsesSharedSlot(217)&&WorldSession.ClanWarUsesSharedSlot(221)&&WorldSession.ClanWarUsesSharedSlot(222),"slot routing table");
        }
        {
            var sent=new List<byte[]>();using var c=World("",sent);
            c.World.Receive(Ask(221,"乙盟"),"");
            t.Check(c.ClanWarSnapshot().Prompt==null&&c.YesNoSnapshot().Prompt==null,"no clan: 221 not admitted, not queued");
        }
    }
}
