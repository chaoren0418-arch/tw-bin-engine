using System.Text.Json;
namespace P06;

// M7-B（T204／#510）離線測試：只用依 server-protocol.md〈S_Ability〉〈S_SPMR〉組出的合成封包，不開遊戲、網路或素材。
static class AbilityStatusTests {
    static byte[] Pad(params byte[] body){var p=new byte[(body.Length+7)/8*8];body.CopyTo(p,0);return p;}
    static byte[] Ability(byte type,byte equipped)=>Pad(36,type,equipped);
    static byte[] SpMrDword(int sp,int mr){var b=new byte[9];b[0]=37;BitConverter.TryWriteBytes(b.AsSpan(1),sp);BitConverter.TryWriteBytes(b.AsSpan(5),mr);return Pad(b);}
    static JsonElement Ext(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("ability");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandled").TryGetProperty(key,out var v)?v.GetInt32():0;

    [WorldModuleTest("ability-status")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：S36、S37 消化型處理器、reset、快照 key 都在產品登錄表。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==36&&e.Subtype==-1&&e.Method.EndsWith(".ReceiveAbility",StringComparison.Ordinal)),"S36 handler registered");
        t.Check(entries.Any(e=>e.Kind=="receive"&&e.Opcode==37&&e.Subtype==-1&&e.Method.EndsWith(".ReceiveSpMr",StringComparison.Ordinal)),"S37 handler registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method.EndsWith(".ResetAbilityStatus",StringComparison.Ordinal)),"reset registered");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="ability"),"snapshot key 'ability' registered");

        // 2. 初始：沒有旗標、沒有 SP／MR。
        var s=new WorldSession();
        t.Equal(0,s.AbilityFlags.Length,"no flags initially");
        t.Check(s.SpMr==null,"no SP/MR initially");
        var e0=Ext(s);
        t.Equal(0,e0.GetProperty("flags").GetArrayLength(),"snapshot flags empty");
        t.Equal(JsonValueKind.Null,e0.GetProperty("spMr").ValueKind,"snapshot spMr null");

        // 3. S36：C(type) C(equipped?1:0)，補到 8 bytes；依 type 覆寫，按 type 排序。
        s.Receive(Ability(3,1),"");
        s.Receive(Ability(1,1),"");
        s.Receive(Ability(3,0),"");
        t.Equal(0,Unhandled(s,"0x24"),"S36 consumed (not unhandled)");
        t.Equal("1:True:1,3:False:2",string.Join(",",s.AbilityFlags.Select(f=>$"{f.Type}:{f.Equipped}:{f.Updates}")),"flags overwritten per type, sorted");
        t.Equal((bool?)true,s.AbilityEquipped(1),"type 1 equipped");
        t.Equal((bool?)false,s.AbilityEquipped(3),"type 3 unequipped after second packet");
        t.Equal((bool?)null,s.AbilityEquipped(2),"unknown type is null");
        s.Receive(Ability(255,1),"");
        t.Equal((bool?)true,s.AbilityEquipped(255),"type byte read unsigned (255)");
        var e1=Ext(s);
        t.Equal(4,e1.GetProperty("updates").GetInt32(),"snapshot total updates");
        t.Equal("1,255",string.Join(",",e1.GetProperty("active").EnumerateArray().Select(x=>x.GetInt32())),"snapshot active types");
        t.Equal(3,e1.GetProperty("flags").GetArrayLength(),"snapshot flag count");

        // 4. S36 格式錯誤：equipped 非 0／1、長度不對、補位非 0 → ProtocolException，且狀態不變。
        t.Throws<ProtocolException>(()=>s.Receive(Ability(5,2),""),"S36 equipped=2 rejected");
        t.Throws<ProtocolException>(()=>s.Receive([36,5],""),"S36 truncated rejected");
        t.Throws<ProtocolException>(()=>s.Receive([36,5,1,0,0,0,0,0,0,0,0,0,0,0,0,0],""),"S36 overlong rejected");
        t.Throws<ProtocolException>(()=>s.Receive([36,5,1,0,0,0,0,9],""),"S36 nonzero padding rejected");
        t.Equal((bool?)null,s.AbilityEquipped(5),"malformed S36 did not mutate state");

        // 5. S37 ROClient 形式：D(sp) D(mr)，9 bytes 補到 16；有號整數。
        s.Receive(SpMrDword(12,45),"");
        t.Equal(0,Unhandled(s,"0x25"),"S37 consumed (not unhandled)");
        t.Equal(new SpMrStatus(12,45,"dword",1),s.SpMr,"S37 dword form");
        s.Receive(SpMrDword(-3,300),"");
        t.Equal(new SpMrStatus(-3,300,"dword",2),s.SpMr,"S37 negative SP and MR above 255 kept");
        // 無參數建構子：C(50) C(100)，3 bytes 補到 8。
        s.Receive(Pad(37,50,100),"");
        t.Equal(new SpMrStatus(50,100,"byte",3),s.SpMr,"S37 no-arg byte form");
        var sp=Ext(s).GetProperty("spMr");
        t.Equal(50,sp.GetProperty("sp").GetInt32(),"snapshot sp");
        t.Equal(100,sp.GetProperty("mr").GetInt32(),"snapshot mr");
        t.Equal("byte",sp.GetProperty("form").GetString(),"snapshot form");

        // 6. S37 格式錯誤：長度不對、補位非 0 → ProtocolException，狀態不變。
        t.Throws<ProtocolException>(()=>s.Receive([37,1,0,0,0,2,0,0,0,0,0,0],""),"S37 12-byte length rejected");
        t.Throws<ProtocolException>(()=>s.Receive([37,1],""),"S37 truncated rejected");
        var bad=SpMrDword(1,2);bad[15]=1;
        t.Throws<ProtocolException>(()=>s.Receive(bad,""),"S37 nonzero padding rejected");
        t.Equal(new SpMrStatus(50,100,"byte",3),s.SpMr,"malformed S37 did not mutate state");

        // 7. Reset 清空旗標與 SP／MR。
        s.Reset();
        t.Equal(0,s.AbilityFlags.Length,"Reset clears flags");
        t.Check(s.SpMr==null,"Reset clears SP/MR");
        var e2=Ext(s);
        t.Equal(0,e2.GetProperty("updates").GetInt32(),"Reset clears update count");
        t.Equal(JsonValueKind.Null,e2.GetProperty("spMr").ValueKind,"Reset clears snapshot spMr");

        // 8. 其他 session 不共用狀態。
        var other=new WorldSession();
        s.Receive(Ability(7,1),"");
        t.Equal(0,other.AbilityFlags.Length,"sessions do not share ability state");
    }
}
