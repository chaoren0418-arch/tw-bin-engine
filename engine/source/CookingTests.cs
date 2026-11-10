using System.Text.Json;
namespace P06;

// W2-F（T237／#559）料理窗資料模型的離線測試：只用合成封包（依 server-protocol.md S_PacketBoxCooking 欄位順序），不開遊戲、網路或素材。
static class CookingTests {
    static byte[] Cook(byte open,byte value,byte[]? magic=null)=>LoginPackets.Pad([250,52,..(magic??[0xdb,0x31,0xdf,0x02]),open,value]);
    static JsonElement Snap(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("cooking");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty(key,out var v)?v.GetInt32():0;

    [WorldModuleTest("cooking")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S250 子碼 52 由本模組接；快照、重置都在；登錄表沒有錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:52,Method:"WorldSession.ReceiveCookWindow"}),"S250/52 registered to cooking module");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="cooking"),"cooking snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetCooking"),"cooking reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. 解碼：3.81 封包剛好 8 byte，前 4 byte 是 TW16 讀的 D。
        var pkt=Cook(1,0);
        t.Equal("FA34DB31DF020100",Convert.ToHexString(pkt),"3.81 S_PacketBoxCooking(0) bytes");
        t.Equal((CookingProtocol.ServerMagic,(byte)1,(byte)0),CookingProtocol.Decode(pkt),"decode D+C+C");
        t.Equal(1,CookingProtocol.Level(0),"level = value + 1");
        t.Equal(41255,CookingProtocol.BookItemId(0),"value 0 = book item 41255");
        t.Equal((int?)2545,CookingProtocol.LevelSurf(0),"level surf first");
        t.Equal((int?)2555,CookingProtocol.LevelSurf(10),"level surf last");
        t.Equal((int?)null,CookingProtocol.LevelSurf(11),"level surf out of range");

        // 3. 開窗。
        var s=new WorldSession();
        t.Check(!s.CookBook.Open,"initially closed");
        s.Receive(Cook(1,2),"");
        var book=s.CookBook;
        t.Check(book is {Open:true,Value:2,Level:3,Magic:CookingProtocol.ServerMagic},"S250/52 open=1 opens level value+1",book);
        t.Equal(0,Unhandled(s,"0xFA/0x34"),"S250/52 not counted unhandled");
        var snap=Snap(s);
        t.Check(snap.GetProperty("open").GetBoolean(),"snapshot open");
        t.Equal(3,snap.GetProperty("level").GetInt32(),"snapshot level");
        t.Equal(41257,snap.GetProperty("bookItemId").GetInt32(),"snapshot book item id");
        t.Equal(2547,snap.GetProperty("levelSurf").GetInt32(),"snapshot level surf");
        t.Check(snap.GetProperty("magicMatches381").GetBoolean(),"snapshot magic matches 3.81");

        // 4. 已開時再收到不同等級：切換到新等級（PBW§2.3）。
        s.Receive(Cook(1,4),"");
        t.Check(s.CookBook is {Open:true,Level:5},"second open switches level",s.CookBook);
        t.Check(s.CookBook.Revision>book.Revision,"revision advances");

        // 5. 第二欄不是 1：不開窗、不改等級（推定）。
        s.CloseCookBook(s.CookBook.Revision);
        s.Receive(Cook(0,7),"");
        t.Check(s.CookBook is {Open:false,Level:5},"open flag 0 does not open or change level",s.CookBook);
        t.Equal(0,Unhandled(s,"0xFA/0x34"),"flag 0 still consumed");

        // 6. 第一欄不是 3.81 常數：照收（TW16 只用第二、三欄），快照標示不符。
        s.Receive(Cook(1,1,[1,2,3,4]),"");
        t.Check(s.CookBook is {Open:true,Level:2,Magic:0x04030201},"non-381 magic still opens",s.CookBook);
        t.Check(!Snap(s).GetProperty("magicMatches381").GetBoolean(),"snapshot flags magic mismatch");

        // 7. 本機關窗。
        t.Check(s.CloseCookBook(s.CookBook.Revision),"close open window");
        t.Check(!s.CookBook.Open,"closed");
        t.Check(!s.CloseCookBook(s.CookBook.Revision),"close twice is no-op");

        // 8. 長度錯誤。
        t.Throws<ProtocolException>(()=>s.Receive([250,52,0xdb,0x31,0xdf,0x02,0x01],""),"truncated rejected");
        t.Throws<ProtocolException>(()=>s.Receive([250,52,0xdb,0x31,0xdf,0x02,0x01,0x00,0x05,0,0,0,0,0,0,0],""),"extra nonzero bytes rejected");

        // 9. Reset 清空。
        s.Receive(Cook(1,3),"");
        s.Reset();
        t.Check(s.CookBook is {Open:false,Level:0},"Reset closes cook book",s.CookBook);
        t.Check(!Snap(s).GetProperty("open").GetBoolean(),"snapshot closed after Reset");
    }
}
