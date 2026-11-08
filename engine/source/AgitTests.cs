using System.Text.Json;
namespace P06;

// M7-D（T206／#512）盟屋清單與地圖的離線測試：只用合成封包，不開遊戲、網路或素材。
static class AgitTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] Entry(uint id,string name,ushort area,byte month,byte day,uint price)=>[..Le32(id),..Str(name),..Le16(area),month,day,..Le32(price)];
    static byte[] HouseList(uint board,params byte[][] entries)=>LoginPackets.Pad([AgitPackets.HouseListOpcode,..Le32(board),..Le16((ushort)entries.Length),..entries.SelectMany(e=>e)]);
    static byte[] HouseMap(uint obj,uint number)=>LoginPackets.Pad([AgitPackets.HouseMapOpcode,..Le32(obj),..Le32(number)]);
    static JsonElement Agit(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("agit");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty(key,out var v)?v.GetInt32():0;

    [WorldModuleTest("agit")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S156、S187 掛在 Default 登錄表，且登錄表無錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry has no errors",WorldExtensionRegistry.Default.Errors);
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==156&&e.Subtype==-1&&e.Method=="WorldSession.ReceiveAgitHouseList"),"S156 registered");
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==187&&e.Subtype==-1&&e.Method=="WorldSession.ReceiveAgitHouseMap"),"S187 registered");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="agit"),"agit snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetAgit"),"agit reset registered");

        // 2. 初始快照。
        var s=new WorldSession();
        var a=Agit(s);
        t.Equal(JsonValueKind.Null,a.GetProperty("houseList").ValueKind,"initial houseList null");
        t.Equal(JsonValueKind.Null,a.GetProperty("houseMap").ValueKind,"initial houseMap null");
        t.Equal(0,a.GetProperty("houseListPackets").GetInt32(),"initial list count");

        // 3. S156：兩筆（含中文名稱、價格高位元），8 byte 對齊。
        AgitHouseList? evList=null;s.AgitHouseListReceived+=l=>evList=l;
        var list=HouseList(0x0102_0304,
            Entry(0x40001,"奇岩盟屋1",78,12,25,2_000_000),
            Entry(262_145+69,"House B",120,1,3,0xFFFF_FFFF));
        t.Equal(0,list.Length%8,"synthetic S156 padded");
        s.Receive(list,"");
        t.Equal(0,Unhandled(s,"0x9C"),"S156 consumed");
        t.Check(evList is {Houses.Count:2},"S156 event raised with 2 houses");
        a=Agit(s);var hl=a.GetProperty("houseList");
        t.Equal(1,a.GetProperty("houseListPackets").GetInt32(),"list packet count");
        t.Equal(0x0102_0304u,hl.GetProperty("boardId").GetUInt32(),"board id");
        var h0=hl.GetProperty("houses")[0];var h1=hl.GetProperty("houses")[1];
        t.Equal(0x40001u,h0.GetProperty("houseId").GetUInt32(),"house 0 id");
        t.Equal("奇岩盟屋1",h0.GetProperty("name").GetString(),"house 0 name (CP950)");
        t.Equal(78,h0.GetProperty("area").GetInt32(),"house 0 area");
        t.Equal(12,h0.GetProperty("month").GetInt32(),"house 0 month");
        t.Equal(25,h0.GetProperty("day").GetInt32(),"house 0 day");
        t.Equal(2_000_000,h0.GetProperty("price").GetInt32(),"house 0 price");
        t.Equal("House B",h1.GetProperty("name").GetString(),"house 1 name");
        t.Equal(-1,h1.GetProperty("price").GetInt32(),"price is signed int like server (int) cast");

        // 4. S156 零筆：取代前一份清單。
        s.Receive(HouseList(7),"");
        hl=Agit(s).GetProperty("houseList");
        t.Equal(7u,hl.GetProperty("boardId").GetUInt32(),"empty list board id");
        t.Equal(0,hl.GetProperty("houses").GetArrayLength(),"empty list replaces previous");

        // 5. S187：0x40001 → 區域 4、編號 1。
        AgitHouseMap? evMap=null;s.AgitHouseMapReceived+=m=>evMap=m;
        s.Receive(HouseMap(0x1234_5678,0x40001),"");
        t.Equal(0,Unhandled(s,"0xBB"),"S187 consumed");
        t.Check(evMap is {Region:4,Number:1},"S187 event raised");
        var hm=Agit(s).GetProperty("houseMap");
        t.Equal(0x1234_5678u,hm.GetProperty("objectId").GetUInt32(),"map object id");
        t.Equal(0x40001u,hm.GetProperty("houseNumber").GetUInt32(),"map raw house number");
        t.Equal(4,hm.GetProperty("region").GetInt32(),"region = high 16 bits");
        t.Equal(1,hm.GetProperty("number").GetInt32(),"number = low 16 bits");
        t.Check(hm.GetProperty("inOriginalRange").GetBoolean(),"0x40001 in original range");
        t.Check(!new AgitHouseMap(0,0x90001).InOriginalRange,"region 9 out of range");
        t.Check(!new AgitHouseMap(0,0x10000).InOriginalRange,"number 0 out of range");
        t.Check(!new AgitHouseMap(0,0x10047).InOriginalRange,"number 71 out of range");
        t.Check(new AgitHouseMap(0,0x80046).InOriginalRange,"region 8 number 70 in range");

        // 6. 格式錯誤：截斷、padding 非零、長度不對都丟 ProtocolException。
        t.Throws<ProtocolException>(()=>s.Receive([AgitPackets.HouseMapOpcode,1,2,3],""),"truncated S187 rejected");
        var badPad=HouseMap(1,2);badPad[^1]=1;
        t.Throws<ProtocolException>(()=>s.Receive(badPad,""),"nonzero padding S187 rejected");
        t.Throws<ProtocolException>(()=>s.Receive([..HouseMap(1,2),0,0,0,0,0,0,0,0],""),"extra block S187 rejected");
        var truncList=LoginPackets.Pad([AgitPackets.HouseListOpcode,..Le32(1),..Le16(2),..Entry(1,"x",1,1,1,1)]);
        t.Throws<ProtocolException>(()=>s.Receive(truncList,""),"S156 count larger than entries rejected");
        t.Throws<ProtocolException>(()=>AgitPackets.ParseHouseMap(HouseList(1)),"wrong opcode rejected");
        t.Equal(0x1234_5678u,Agit(s).GetProperty("houseMap").GetProperty("objectId").GetUInt32(),"malformed packets keep previous state");

        // 7. Reset 清除。
        s.Reset();
        a=Agit(s);
        t.Equal(JsonValueKind.Null,a.GetProperty("houseList").ValueKind,"reset clears list");
        t.Equal(JsonValueKind.Null,a.GetProperty("houseMap").ValueKind,"reset clears map");
        t.Equal(0,a.GetProperty("houseMapPackets").GetInt32(),"reset clears counts");
        t.Check(s.AgitHouseList==null&&s.AgitHouseMap==null,"reset clears properties");
    }
}
