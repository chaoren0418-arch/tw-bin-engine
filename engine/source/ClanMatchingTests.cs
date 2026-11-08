using System.Text.Json;
namespace P06;

// M7-H（T209／#515）血盟推薦模組的離線測試：只用合成封包（依 server-protocol.md 欄位順序），不開遊戲、網路或素材。
static class ClanMatchingTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] S(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Row(uint id,string name,string leader,uint level,byte type,bool house,bool castle,string msg,uint emblem)=>
        [..Le32(id),..Str(name),..Str(leader),..Le32(level),type,(byte)(house?1:0),(byte)(castle?1:0),0,..Str(msg),..Le32(emblem)];
    static JsonElement Snap(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("clanMatching");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty(key,out var v)?v.GetInt32():0;
    static string LastLine(WorldSession s)=>s.PlayerChatLines.LastOrDefault()?.Text??"";
    static string Hex(byte[] b)=>Convert.ToHexString(b);

    [WorldModuleTest("clan-matching")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S0 由本模組接；快照、Reset 已註冊；登錄表沒有錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==0&&e.Subtype==-1&&e.Method=="WorldSession.ReceiveClanMatching"),"S0 registered to clan matching module");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="clanMatching"),"clanMatching snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetClanMatching"),"clan matching reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. C76／C78 編碼（長度補到 8 的倍數）。
        t.Equal("4C02000000000000",Hex(ClanMatchingProtocol.ListRecommended().Packet),"C76 2 = C(76) C(2)");
        t.Equal("4C03000000000000",Hex(ClanMatchingProtocol.ListApplied().Packet),"C76 3");
        t.Equal("4C04000000000000",Hex(ClanMatchingProtocol.Detail().Packet),"C76 4");
        t.Equal("4C01000000000000",Hex(ClanMatchingProtocol.Unregister().Packet),"C76 1");
        t.Equal("4C00024142000000",Hex(ClanMatchingProtocol.Register(2,"AB").Packet),"C76 0 = C(0) C(clanType) S(typeMessage)");
        t.Equal("4C0005A4A400"+"0000",Hex(ClanMatchingProtocol.Register(5,"中").Packet),"C76 0 message encoded CP950");
        t.Equal("4C05785634120000",Hex(ClanMatchingProtocol.Apply(0x12345678).Packet),"C76 5 = C(5) D(clanId)");
        t.Equal("4C06EFCDAB000100",Hex(ClanMatchingProtocol.Answer(0x00ABCDEF,1).Packet),"C76 6 = C(6) D(objId) C(type)");
        t.Equal("4C06EFCDAB000300",Hex(ClanMatchingProtocol.Answer(0x00ABCDEF,3).Packet),"C76 6 type 3");
        t.Equal("4E0F48690000"+"0000",Hex(ClanMatchingProtocol.Announce("Hi")),"C78 15 = C(15) S(announce)");
        t.Equal("4E0F000000000000",Hex(ClanMatchingProtocol.Announce("")),"C78 15 empty announce allowed");
        t.Equal("4E10783100000000",Hex(ClanMatchingProtocol.Notes("x1")),"C78 16 = C(16) S(notes)");
        t.Throws<ArgumentException>(()=>ClanMatchingProtocol.Announce(new string('a',161)),"announce over 160 chars rejected");
        t.Equal(168,ClanMatchingProtocol.Announce(new string('a',160)).Length,"announce 160 chars accepted (2+160+1 padded to 168)");
        t.Throws<ArgumentException>(()=>ClanMatchingProtocol.Announce("😀"),"announce surrogate rejected (server rule)");
        t.Throws<ArgumentException>(()=>ClanMatchingProtocol.Notes("a\nb"),"notes control char rejected");
        t.Throws<ArgumentException>(()=>ClanMatchingProtocol.Apply(0),"apply clan 0 rejected (server drops)");
        t.Throws<ArgumentException>(()=>ClanMatchingProtocol.Answer(1,0),"answer type 0 rejected");
        t.Throws<ArgumentException>(()=>ClanMatchingProtocol.Answer(1,4),"answer type 4 rejected");
        t.Throws<ArgumentException>(()=>ClanMatchingProtocol.Answer(0,1),"answer objId 0 rejected");

        // 3. S0 type 2：推薦清單。
        var s=new WorldSession();
        s.Receive(S([0,2,0,2],Row(101,"Alpha","Lead1",30,1,true,false,"hunt",7),Row(102,"Beta","Lead2",0,2,false,true,"pvp",0)),"");
        var rec=s.ClanRecommended!;
        t.Equal(2,rec.Length,"S0 type 2 row count");
        t.Check(rec[0] is {ClanId:101,ClanName:"Alpha",Leader:"Lead1",LoginLevel:30,TypeId:1,HasHouse:true,HasCastle:false,TypeMessage:"hunt",EmblemId:7},"S0 type 2 row 0 fields",rec[0]);
        t.Check(rec[1] is {ClanId:102,HasHouse:false,HasCastle:true,TypeMessage:"pvp"},"S0 type 2 row 1 fields",rec[1]);
        t.Equal("[血盟推薦] 收到推薦血盟清單，共 2 個。",LastLine(s),"S0 type 2 notice");
        t.Equal(0,Unhandled(s,"0x00"),"S0 not counted unhandled");
        s.Receive(S([0,2,0,0]),"");
        t.Equal(0,s.ClanRecommended!.Length,"S0 type 2 empty list");
        t.Throws<ProtocolException>(()=>s.Receive(S([0,2,0,1],Le32(101)),""),"S0 type 2 truncated row rejected");

        // 4. S0 type 3：自己的申請清單（clanId 重複一次）。
        s.Receive(S([0,3,0,1],Le32(101),[0],Row(101,"Alpha","Lead1",30,1,false,false,"hunt",0)),"");
        t.Check(s.ClanApplied is [{ClanId:101,ClanName:"Alpha"}],"S0 type 3 row",s.ClanApplied);
        t.Throws<ProtocolException>(()=>s.Receive(S([0,3,0,1],Le32(999),[0],Row(101,"A","L",0,0,false,false,"",0)),""),"S0 type 3 key mismatch rejected");

        // 5. S0 type 4：自己血盟的推薦內容。
        s.Receive(S([0,4,0x82]),"");
        t.Check(s.ClanMatchingDetail is {Registered:false,Applicants.Length:0},"S0 type 4 0x82 = not registered");
        t.Equal("[血盟推薦] 本血盟目前沒有登錄推薦。",LastLine(s),"type 4 not registered notice");
        s.Receive(S([0,4,0,3],Str("join us"),[2],Le32(0x500),[0,1],Str("Ann"),[2],Le16(unchecked((ushort)-500)),Le16(52),Le32(0x501),[0,0],Str("Bob"),[3],Le16(100),Le16(10)),"");
        var d=s.ClanMatchingDetail!;
        t.Check(d is {Registered:true,TypeId:3,TypeMessage:"join us"},"S0 type 4 header",d);
        t.Equal(2,d.Applicants.Length,"S0 type 4 applicant count");
        t.Check(d.Applicants[0] is {ObjectId:0x500,Online:true,Name:"Ann",ClassType:2,Lawful:-500,Level:52},"applicant 0 fields",d.Applicants[0]);
        t.Check(d.Applicants[1] is {ObjectId:0x501,Online:false,ClassType:3,Lawful:100,Level:10},"applicant 1 fields",d.Applicants[1]);
        t.Equal("[血盟推薦] 本血盟的推薦已登錄，申請者 2 人。",LastLine(s),"type 4 registered notice");
        s.Receive(S([0,4,0,1],Str(""),[0]),"");
        t.Check(s.ClanMatchingDetail is {Registered:true,TypeId:1,Applicants.Length:0},"S0 type 4 no applicants (apply==null form)");
        t.Throws<ProtocolException>(()=>s.Receive(S([0,4,7]),""),"S0 type 4 bad flag rejected");

        // 6. S0 type 5／6：操作結果。
        s.Receive(S([0,2,0,2],Row(101,"Alpha","L",0,0,false,false,"",0),Row(102,"Beta","L",0,0,false,false,"",0)),"");
        s.Receive(S([0,5,0],Le32(101),[0]),"");
        t.Check(s.ClanMatchingResult is {Type:5,ActionType:0,RecordId:101,AcceptType:0,Success:true},"S0 type 5 result",s.ClanMatchingResult);
        t.Equal("[血盟推薦] 已送出加入 Alpha 的申請。",LastLine(s),"type 5 notice uses clan name from list");
        t.Check(s.ClanRecommended is [{ClanId:102}],"applied clan removed from recommended list (server excludes it next time)",s.ClanRecommended);
        s.Receive(S([0,4,0,0],Str("m"),[2],Le32(0x500),[0,1],Str("Ann"),[0],Le16(0),Le16(1),Le32(0x501),[0,1],Str("Bob"),[0],Le16(0),Le16(1)),"");
        s.Receive(S([0,6,0],Le32(0x500),[1]),"");
        t.Equal("[血盟推薦] 已接受 Ann 加入血盟。",LastLine(s),"type 6 accept notice");
        t.Check(s.ClanMatchingDetail!.Applicants is [{ObjectId:0x501}],"accepted applicant removed");
        s.Receive(S([0,6,ClanMatchingProtocol.Failed],Le32(0x501),[2]),"");
        t.Check(s.ClanMatchingResult is {Success:false,AcceptType:2},"type 6 failure (131)");
        t.Equal("[血盟推薦] 無法移除 Bob 的申請。",LastLine(s),"type 6 remove failure notice");
        t.Equal(1,s.ClanMatchingDetail!.Applicants.Length,"failed removal keeps applicant");
        s.Receive(S([0,6,0],Le32(0x999),[3]),"");
        t.Equal("[血盟推薦] 已移除 #2457 的申請。",LastLine(s),"unknown applicant shown by id");

        // 7. S0 type 0／1：登錄、撤下。
        s.Receive(S([0,0,0]),"");
        t.Equal("[血盟推薦] 血盟推薦已登錄。",LastLine(s),"type 0 posted");
        t.Equal((ClanMatchingDetail?)null,s.ClanMatchingDetail,"posted clears stale detail");
        s.Receive(S([0,1,0]),"");
        t.Equal("[血盟推薦] 血盟推薦已撤下。",LastLine(s),"type 1 deleted");
        t.Check(s.ClanMatchingDetail is {Registered:false},"deleted marks detail not registered");

        // 8. 未知 type 計入 unhandled、不丟例外。
        s.Receive(S([0,9,0]),"");
        t.Equal(1,Unhandled(s,"0x00"),"S0 unknown type counted unhandled");

        // 9. 快照與 Reset。
        var snap=Snap(s);
        t.Equal(1,snap.GetProperty("recommended").GetArrayLength(),"snapshot recommended");
        t.Check(snap.GetProperty("lastResult").GetProperty("success").GetBoolean(),"snapshot last result");
        s.Reset();
        var cleared=Snap(s);
        t.Equal(JsonValueKind.Null,cleared.GetProperty("recommended").ValueKind,"Reset clears recommended");
        t.Equal(JsonValueKind.Null,cleared.GetProperty("detail").ValueKind,"Reset clears detail");
        t.Equal(JsonValueKind.Null,cleared.GetProperty("lastResult").ValueKind,"Reset clears last result");
        t.Check(!s.ClanMatchingAllowed(ClanMatchingAction.ListRecommended),"no actor -> nothing allowed");
    }
}
