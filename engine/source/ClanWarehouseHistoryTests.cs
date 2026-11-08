using System.Text.Json;
namespace P06;

// W2-G（T238／#560）血盟倉庫紀錄的離線測試：只用合成封包，不開遊戲、網路或素材。
static class ClanWarehouseHistoryTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] Record(string who,byte type,string item,uint count,uint minutes)=>[..Str(who),type,..Str(item),..Le32(count),..Le32(minutes)];
    static byte[] History(params byte[][] records)=>Raw((uint)records.Length,records);
    static byte[] Raw(uint count,params byte[][] records)=>LoginPackets.Pad([ClanWarehouseHistoryPackets.Opcode,ClanWarehouseHistoryPackets.Subtype,..Le32(count),..records.SelectMany(e=>e)]);
    static JsonElement Snap(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("clanWarehouseHistory");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty(key,out var v)?v.GetInt32():0;
    static int UnhandledSubtype(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).TryGetProperty("unhandledSubtypes",out var u)&&u.TryGetProperty(key,out var v)?v.GetInt32():0;

    [WorldModuleTest("clan-warehouse-history")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S250/117 掛在 Default 登錄表，且登錄表無錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry has no errors",WorldExtensionRegistry.Default.Errors);
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==250&&e.Subtype==117&&e.Method=="WorldSession.ReceiveClanWarehouseHistory"),"S250/117 registered");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="clanWarehouseHistory"),"snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetClanWarehouseHistory"),"reset registered");

        // 2. 初始快照。
        var s=new WorldSession();
        var a=Snap(s);
        t.Equal(JsonValueKind.Null,a.GetProperty("records").ValueKind,"initial records null");
        t.Equal(JsonValueKind.Null,a.GetProperty("notice").ValueKind,"initial notice null");
        t.Equal(0,a.GetProperty("packets").GetInt32(),"initial packet count");

        // 3. 兩筆（中文名、$ 名稱代碼、數量與分鐘高位元），8 byte 對齊；觸發事件、不設訊息。
        ClanWarehouseHistory? ev=null;int evCount=0;s.ClanWarehouseHistoryReceived+=h=>{ev=h;evCount++;};
        var pkt=History(Record("盟主甲",0,"$4 (50)",50,3),Record("Bob",1,"屠龍劍",0xFFFF_FFFF,0x8000_0000));
        t.Equal(0,pkt.Length%8,"synthetic S250/117 padded");
        s.Receive(pkt,"");
        t.Equal(0,Unhandled(s,"0xFA"),"S250/117 consumed");
        t.Equal(0,UnhandledSubtype(s,"0xFA/0x75"),"S250/117 not counted as unhandled subtype");
        t.Equal(1,evCount,"event raised once for non-empty list");
        t.Check(ev is {Empty:false,Records.Count:2},"event carries 2 records");
        a=Snap(s);var r=a.GetProperty("records");
        t.Equal(1,a.GetProperty("packets").GetInt32(),"packet count");
        t.Equal(2,r.GetArrayLength(),"two records");
        t.Equal("盟主甲",r[0].GetProperty("charName").GetString(),"record 0 char name (CP950)");
        t.Equal(0,r[0].GetProperty("type").GetInt32(),"record 0 type raw");
        t.Equal("$4 (50)",r[0].GetProperty("itemName").GetString(),"record 0 item name kept raw");
        t.Equal(50,r[0].GetProperty("count").GetInt32(),"record 0 count");
        t.Equal(3,r[0].GetProperty("minutesAgo").GetInt32(),"record 0 minutes ago");
        t.Equal("Bob",r[1].GetProperty("charName").GetString(),"record 1 char name");
        t.Equal(1,r[1].GetProperty("type").GetInt32(),"record 1 type raw");
        t.Equal("屠龍劍",r[1].GetProperty("itemName").GetString(),"record 1 item name");
        t.Equal(-1,r[1].GetProperty("count").GetInt32(),"count is signed int like server");
        t.Equal(int.MinValue,r[1].GetProperty("minutesAgo").GetInt32(),"minutes is signed int like server");
        t.Equal(JsonValueKind.Null,a.GetProperty("notice").ValueKind,"non-empty list has no notice");
        t.Check(s.ClanWarehouseHistoryNotice==null,"notice property null");

        // 4. 0 筆：不觸發開窗事件，改設訊息 1889（文字走 T228 字串表），不動 LastServerMessage。
        s.Receive(History(),"");
        t.Equal(1,evCount,"empty list does not raise open event");
        a=Snap(s);
        t.Equal(0,a.GetProperty("records").GetArrayLength(),"empty list replaces previous");
        t.Equal(1889,a.GetProperty("notice").GetProperty("id").GetInt32(),"empty list notice id 1889");
        // 文字來自 T228 字串表（讀本機客戶端；CI 無客戶端時為「原服訊息 1889」占位），所以和 Decode 結果比對。
        t.Equal(ServerMessageText.Decode(1889,[]).Text,a.GetProperty("notice").GetProperty("text").GetString(),"1889 text from T228 string table");
        t.Check(s.ClanWarehouseHistory is {Empty:true},"property reports empty");
        t.Check(s.LastServerMessage==null&&s.LastServerReply==null,"empty notice does not touch LastServerMessage");

        // 5. 再收到非空清單：清除訊息。
        s.Receive(History(Record("C",0,"x",1,0)),"");
        t.Equal(2,evCount,"second non-empty list raises event");
        t.Check(s.ClanWarehouseHistoryNotice==null,"non-empty list clears notice");
        t.Equal(3,Snap(s).GetProperty("packets").GetInt32(),"packet count after three packets");

        // 6. 格式錯誤：筆數大於內容、筆數超大、padding 非零、多一塊、子碼錯都丟 ProtocolException，保留原狀態。
        t.Throws<ProtocolException>(()=>s.Receive(Raw(2,Record("x",0,"y",1,1)),""),"count larger than records rejected");
        t.Throws<ProtocolException>(()=>s.Receive(Raw(0xFFFF_FFFF),""),"huge count rejected before allocation");
        var badPad=History(Record("x",0,"y",1,1));badPad[^1]=1;
        t.Throws<ProtocolException>(()=>s.Receive(badPad,""),"nonzero padding rejected");
        t.Throws<ProtocolException>(()=>s.Receive([..History(),0,0,0,0,0,0,0,0],""),"extra block rejected");
        t.Throws<ProtocolException>(()=>ClanWarehouseHistoryPackets.Parse(LoginPackets.Pad([250,52,0,0,0,0])),"wrong subtype rejected");
        t.Equal(1,s.ClanWarehouseHistory!.Records.Count,"malformed packets keep previous state");
        t.Equal("C",s.ClanWarehouseHistory!.Records[0].CharName,"previous record kept");

        // 7. 其他 S250 子碼不被本處理器吃掉。
        s.Receive(LoginPackets.Pad([250,0xEF,0,0]),"");
        t.Check(UnhandledSubtype(s,"0xFA/0xEF")==1||Unhandled(s,"0xFA")>=1,"other S250 subtype still unhandled");

        // 8. Reset 清除。
        s.Reset();
        a=Snap(s);
        t.Equal(JsonValueKind.Null,a.GetProperty("records").ValueKind,"reset clears records");
        t.Equal(JsonValueKind.Null,a.GetProperty("notice").ValueKind,"reset clears notice");
        t.Equal(0,a.GetProperty("packets").GetInt32(),"reset clears count");
        t.Check(s.ClanWarehouseHistory==null&&s.ClanWarehouseHistoryNotice==null,"reset clears properties");
    }
}
