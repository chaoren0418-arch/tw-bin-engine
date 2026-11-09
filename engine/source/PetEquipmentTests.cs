using System.Reflection;
using System.Text.Json;
namespace P06;

// T250（#611，PBW-E）S250/37 寵物裝備更新的離線測試：只用合成封包（欄位依 3.81 S_PetEquipment 與 S176 類型 11）
// 與記憶體內假傳送端，不開遊戲、網路、視窗或素材。
static class PetEquipmentTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Pet=456,Stranger=790;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Appear(uint id,ushort x,string name,string owner="",byte hp=100)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(owner),[0,hp,0,0,0,255,255]);
    static byte[] Equip(uint pet,byte listNo,sbyte ac,byte data=2)=>P([250,37,data],Le32(pet),[listNo,unchecked((byte)ac)]);
    // S176 類型 11：D(pet) H(n) C(11) n×{D id C type H icon C bless D count C identified S name} C(ac)
    static byte[] Bag(uint pet,sbyte ac,params (uint id,byte identified,string name)[] rows)=>P([176],Le32(pet),Le16((ushort)rows.Length),[11],
        rows.SelectMany(r=>(byte[])[..Le32(r.id),2,..Le16(100),1,..Le32(1),r.identified,..Str(r.name)]).ToArray(),[unchecked((byte)ac)]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    // 有角色時完整 Snapshot() 會讀素材（CI 沒有），所以直接呼叫模組快照與未處理子碼計數（同 PetTargetTests）。
    static JsonElement Snap(WorldSession s)=>JsonSerializer.SerializeToElement(typeof(WorldSession).GetMethod("PetEquipmentSnapshot",F)!.Invoke(s,null));
    static int Unhandled(WorldSession s,byte sub)=>((System.Collections.Concurrent.ConcurrentDictionary<(byte op,byte sub),int>)typeof(WorldSession).GetField("unhandledSubtypes",F)!.GetValue(s)!).GetValueOrDefault(((byte)250,sub));

    static LoginConnection Seed(List<byte[]> log){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":44,\"Charisma\":25,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");c.World.Receive(Appear(Pet,32601,"夥伴","offline",70),"offline");
        c.World.Receive(Appear(Stranger,32603,"他人寵物","someone",70),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }

    [WorldModuleTest("pet-equipment")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S250/37 消化、S176 只觀察；快照與重置；登錄表無錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:37,Method:"WorldSession.ReceivePetEquipment"}),"S250/37 registered");
        t.Check(entries.Any(e=>e is {Kind:"observe",Opcode:176,Subtype:-1,Method:"WorldSession.ObservePetBagOrder"}),"S176 observed (not consumed)");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="petEquipment")&&entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetPetEquipment"),"snapshot／reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. 解碼：3.81 S_PetEquipment 8 byte = C(250) C(37) C(data) D(pet) C(listNo) C(ac)。
        var pkt=Equip(Pet,1,-5);
        t.Equal("FA2502C801000001FB",Convert.ToHexString(pkt)[..18],"3.81 S_PetEquipment bytes");
        t.Equal(((byte)2,Pet,(byte)1,(sbyte)-5),PetEquipmentPackets.Decode(pkt),"decode C D C C; AC signed");
        t.Throws<ProtocolException>(()=>PetEquipmentPackets.Decode(Equip(0,1,0)),"pet id 0 rejected");
        t.Throws<ProtocolException>(()=>PetEquipmentPackets.Decode([250,37,2,0xC8,1,0,0,1]),"truncated rejected");
        t.Throws<ProtocolException>(()=>PetEquipmentPackets.Decode([250,37,2,0xC8,1,0,0,1,5,9,0,0,0,0,0,0]),"extra nonzero bytes rejected");

        // 3. 世界：背包先到，再收到 S250/37 → AC 改用 37 的值、記下該格物品、背包標過時；不送任何封包（不回 C99）。
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        t.Check(w.IsOwnedCompanion(Pet)&&!w.IsOwnedCompanion(Stranger)&&!w.IsOwnedCompanion(Me),"Pets.cs read interface: own companion only");
        w.Receive(Bag(Pet,-2,(1001,1,"牙"),(1002,1,"皮盔")),"offline");
        t.Check(w.PetInventory(Pet) is {Ac:-2,Items.Length:2},"S176 bag still handled by Pets.cs",w.PetInventory(Pet));
        t.Equal((sbyte?)-2,w.PetAc(Pet),"AC from bag before any S250/37");
        t.Check(!w.PetBagStale(Pet),"bag not stale yet");
        long rev=w.PetEquipmentRevision;
        w.Receive(Equip(Pet,1,-6),"offline");
        var e=w.PetEquipmentState(Pet);
        t.Check(e is {Data:2,ListNo:1,Ac:-6,ItemId:1002u,ItemName:"皮盔",KnownData:true},"S250/37 stored with bag slot item",e);
        t.Equal((sbyte?)-6,w.PetAc(Pet),"AC updated from S250/37");
        t.Check(w.PetBagStale(Pet),"bag marked stale (equipped bits not in S250/37)");
        t.Check(w.PetInventory(Pet) is {Ac:-2},"Pets.cs bag unchanged (read-only use)");
        t.Check(w.PetEquipmentRevision>rev,"revision advances");
        t.Equal(0,Unhandled(w,37),"S250/37 not counted unhandled");
        t.Equal(0,log.Count,"no packet sent (no C99 reply, no auto refresh)");
        var snap=Snap(w);var row=snap.GetProperty("pets")[0];
        t.Equal((int)Pet,row.GetProperty("petId").GetInt32(),"snapshot pet id");
        t.Equal(-6,row.GetProperty("ac").GetInt32(),"snapshot ac");
        t.Check(row.GetProperty("bagStale").GetBoolean(),"snapshot bag stale");
        t.Check(snap.GetProperty("reply").GetString()!.StartsWith("none"),"snapshot states no reply");

        // 4. 之後再收到背包：背包較新，AC 改回背包值、不再過時。
        w.Receive(Bag(Pet,-7,(1001,1,"牙"),(1002,3,"皮盔")),"offline");
        t.Equal((sbyte?)-7,w.PetAc(Pet),"newer bag AC wins");
        t.Check(!w.PetBagStale(Pet),"newer bag clears stale");
        t.Check(w.PetEquipmentState(Pet) is {Ac:-6},"last S250/37 kept for reference");

        // 5. 沒有背包、索引超出背包、data 非 2／0x16：照收。
        w.Receive(Equip(Pet,9,3,0x16),"offline");
        t.Check(w.PetEquipmentState(Pet) is {ListNo:9,ItemId:null,ItemName:null,KnownData:true,Ac:3},"listNo beyond bag stored without item",w.PetEquipmentState(Pet));
        w.Receive(Equip(Pet,0,4,7),"offline");
        t.Check(w.PetEquipmentState(Pet) is {Data:7,KnownData:false,ItemId:1001u},"unknown data byte accepted and flagged",w.PetEquipmentState(Pet));

        // 6. 他人寵物、未知物件：消化但忽略並計數。
        w.Receive(Equip(Stranger,0,1),"offline");
        w.Receive(Equip(999,0,1),"offline");
        t.Check(w.PetEquipmentState(Stranger)==null&&w.PetEquipmentState(999)==null,"foreign／unknown pet ignored");
        t.Equal(2,Snap(w).GetProperty("ignored").GetInt32(),"ignored counted");
        t.Equal(0,Unhandled(w,37),"ignored packets still consumed");

        // 7. 背包未知的寵物：AC 直接取 S250/37。
        var log2=new List<byte[]>();using var c2=Seed(log2);var w2=c2.World;
        w2.Receive(Equip(Pet,0,-1),"offline");
        t.Equal((sbyte?)-1,w2.PetAc(Pet),"AC without bag from S250/37");
        t.Check(!w2.PetBagStale(Pet),"no bag → not stale");
        t.Equal((sbyte?)null,w2.PetAc(Stranger),"no data → null");

        // 8. 地圖切換（WorldEpoch）與 Reset 清空；全程不送封包。
        w.Receive(LoginPackets.Pad([206,4,0,0]),"offline");
        t.Check(w.PetEquipmentState(Pet)==null,"map change clears pet equipment");
        w2.Reset();
        t.Check(w2.PetEquipmentState(Pet)==null&&Snap(w2).GetProperty("ignored").GetInt32()==0,"reset clears pet equipment");
        t.Equal(0,log.Count+log2.Count,"nothing sent overall");
    }
}
