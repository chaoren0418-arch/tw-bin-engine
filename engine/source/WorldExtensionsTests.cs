using System.Text.Json;
namespace P06;

// M7-A（T203／#509）擴充點的離線測試：只用合成封包與測試夾具登錄表，不開遊戲、網路或素材。
static class WorldExtensionsTests {
    // 夾具型別的 static 方法（第一個參數 WorldSession）只進測試登錄表，不會進產品的 Default 登錄表。
    static class Fixture {
        public static readonly List<string> Calls=[];
        public static int Resets;
        [WorldReceive(252)] static bool S252(WorldSession s,byte[] plain){Calls.Add($"S252:{Convert.ToHexString(plain)}");return true;}
        [WorldReceive(175)] static bool S175Declines(WorldSession s,byte[] plain){Calls.Add("S175-declined");return false;}
        [WorldReceive(250,Subtype=0xEE)] static bool S250Exact(WorldSession s,byte[] plain){Calls.Add("S250/EE");return true;}
        [WorldReceive(250)] static bool S250Any(WorldSession s,byte[] plain){Calls.Add($"S250/any:{plain[1]:X2}");return plain[1]!=0xEC;}
        [WorldReceive(49)] static bool S49Throws(WorldSession s,byte[] plain)=>throw new ProtocolException("fixture S49 malformed");
        [WorldReceive(123,Observe=true)] static void ObserveTime(WorldSession s,byte[] plain){Calls.Add($"observe123:serverTimeBefore={s.ServerTime?.ToString()??"null"}");}
        [WorldReceive(250,Subtype=0xEE,Observe=true)] static void ObserveS250(WorldSession s,byte[] plain){Calls.Add("observe250/EE");}
        [WorldReset] static void Reset(WorldSession s){Resets++;}
        [WorldSnapshot("fixture")] static object Snap(WorldSession s)=>new{calls=Calls.Count};
    }
    sealed class BadFixture {
        [WorldReceive(12)] static bool A12(WorldSession s,byte[] plain)=>true;
        [WorldReceive(12)] static bool B12Duplicate(WorldSession s,byte[] plain)=>true;
        [WorldReceive(18)] static void C18WrongReturn(WorldSession s,byte[] plain){}
        [WorldReceive(0,Subtype=300)] static bool D0BadSubtype(WorldSession s,byte[] plain)=>true;
        [WorldReset] static void E_ResetWithArgument(WorldSession s,int x){}
        [WorldSnapshot("same")] static object F1(WorldSession s)=>1;
        [WorldSnapshot("same")] static object F2Duplicate(WorldSession s)=>2;
        [WorldSnapshot("value")] static int G_ValueType(WorldSession s)=>3;
        [WorldReceive(95)] bool H_InstanceOnOtherType(byte[] plain)=>true;
    }
    static class Empty {}

    static JsonElement Snapshot(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s);
    static int Count(JsonElement map,string key)=>map.TryGetProperty(key,out var v)?v.GetInt32():0;

    [WorldModuleTest("world-extensions")]
    static void Run(WorldModuleTestContext t){
        // 1. 產品登錄表：所有模組的註冊都必須合法（後續 M7 各組的新檔由這一項自動把關）。
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry has no invalid registrations",WorldExtensionRegistry.Default.Errors);
        t.Check(ReferenceEquals(new WorldSession().ExtensionEntries,WorldExtensionRegistry.Default.Entries),"new sessions use the default registry");
        t.Check(WorldModuleTests.Discover().Any(x=>x.name=="world-extensions"),"--module-tests discovers [WorldModuleTest] methods");

        // 2. 錯誤的註冊不生效且留下原因；同 key 時名稱排序在前者勝出。
        var bad=WorldExtensionRegistry.FromType(typeof(BadFixture));
        t.Equal(7,bad.Errors.Count,"bad fixture error count");
        t.Check(bad.Errors.Any(e=>e.Contains("B12Duplicate")&&e.Contains("A12")),"duplicate (opcode,subtype) rejected, first by name kept",bad.Errors);
        t.Check(bad.Errors.Any(e=>e.Contains("C18WrongReturn")),"observe-less void handler rejected");
        t.Check(bad.Errors.Any(e=>e.Contains("D0BadSubtype")),"subtype outside -1..255 rejected");
        t.Check(bad.Errors.Any(e=>e.Contains("E_ResetWithArgument")),"reset with parameters rejected");
        t.Check(bad.Errors.Any(e=>e.Contains("F2Duplicate")),"duplicate snapshot key rejected");
        t.Check(bad.Errors.Any(e=>e.Contains("G_ValueType")),"value-type snapshot rejected (no boxing bind)");
        t.Check(bad.Errors.Any(e=>e.Contains("H_InstanceOnOtherType")),"instance method outside WorldSession rejected");
        t.Equal("receive:12:BadFixture.A12|snapshot:same:BadFixture.F1",string.Join("|",bad.Entries.Select(e=>$"{e.Kind}:{(object?)e.Opcode??e.Key}:{e.Method}")),"only valid bad-fixture entries registered");
        var fixture=WorldExtensionRegistry.FromType(typeof(Fixture));
        t.Check(fixture.Errors.Count==0,"fixture registry valid",fixture.Errors);
        t.Equal(9,fixture.Entries.Count,"fixture entry count");

        // 3. 分派：只接主分派沒處理的 opcode；子碼優先；回 false 仍計未處理。
        Fixture.Calls.Clear();Fixture.Resets=0;
        var s=new WorldSession();s.UseExtensions(fixture);
        s.Receive([252,1,2],"");
        t.Equal("S252:FC0102",Fixture.Calls.LastOrDefault(),"S252 reaches module handler with full plain bytes");
        s.Receive([175,9],"");
        t.Equal("S175-declined",Fixture.Calls.LastOrDefault(),"S175 handler called");
        s.Receive([250,0xEE,5],"");
        t.Equal("observe250/EE,S250/EE",string.Join(",",Fixture.Calls.TakeLast(2)),"S250 exact subtype: observer first, then exact handler");
        s.Receive([250,0xED],"");
        t.Equal("S250/any:ED",Fixture.Calls.LastOrDefault(),"S250 other subtype falls back to any-subtype handler");
        s.Receive([250,0xEC],"");
        t.Equal("S250/any:EC",Fixture.Calls.LastOrDefault(),"S250 declined subtype reaches any-subtype handler");
        var snap=Snapshot(s);var unhandled=snap.GetProperty("unhandled");var subs=snap.GetProperty("unhandledSubtypes");
        t.Equal(0,Count(unhandled,"0xFC"),"consumed S252 not counted unhandled");
        t.Equal(1,Count(unhandled,"0xAF"),"declined S175 counted unhandled");
        t.Equal(1,Count(unhandled,"0xFA"),"only the declined S250 counted unhandled");
        t.Equal(1,Count(subs,"0xFA/0xEC"),"declined S250 subtype counted");
        t.Equal(0,Count(subs,"0xFA/0xEE"),"consumed S250 subtype not counted");
        t.Equal(6,snap.GetProperty("extensions").GetProperty("fixture").GetProperty("calls").GetInt32(),"module snapshot published under extensions.<key>");

        // 4. 觀察者：看得到主分派會消化的封包，且不影響原處理（S123 仍設 ServerTime）。
        s.Receive([123,0x10,0x20,0x30,0x40,0,0,0],""); // 8-byte padded like the wire
        t.Equal("observe123:serverTimeBefore=null",Fixture.Calls.LastOrDefault(),"observer runs before core dispatch");
        t.Equal((uint?)0x40302010,s.ServerTime,"core S123 handling unchanged by observer");
        t.Equal(0,Count(Snapshot(s).GetProperty("unhandled"),"0x7B"),"observed S123 not counted unhandled");

        // 5. 模組丟出的協定錯誤照常往外拋（與既有模組一致），且不被記成未處理。
        t.Throws<ProtocolException>(()=>s.Receive([49,0],""),"handler ProtocolException propagates");
        t.Equal(0,Count(Snapshot(s).GetProperty("unhandled"),"0x31"),"throwing handler not counted unhandled");

        // 6. Reset：呼叫模組 reset，清掉未處理計數與子碼計數。
        s.Reset();
        t.Equal(1,Fixture.Resets,"Reset() calls module reset hook once");
        var after=Snapshot(s);
        t.Check(!after.GetProperty("unhandled").EnumerateObject().Any(),"Reset clears unhandled");
        t.Check(!after.GetProperty("unhandledSubtypes").EnumerateObject().Any(),"Reset clears unhandled subtypes");

        // 7. 沒有任何模組時，行為與擴充前相同：全部記成未處理。
        var plain=new WorldSession();plain.UseExtensions(WorldExtensionRegistry.FromType(typeof(Empty)));
        int callsBefore=Fixture.Calls.Count;
        plain.Receive([252,1],"");plain.Receive([250,0xEE],"");plain.Receive([250,0xEE],"");
        var p=Snapshot(plain);
        t.Equal(callsBefore,Fixture.Calls.Count,"other sessions' registries not invoked");
        t.Equal(1,Count(p.GetProperty("unhandled"),"0xFC"),"empty registry: S252 unhandled");
        t.Equal(2,Count(p.GetProperty("unhandled"),"0xFA"),"empty registry: S250 unhandled");
        t.Equal(2,Count(p.GetProperty("unhandledSubtypes"),"0xFA/0xEE"),"empty registry: S250 subtype counted");
        t.Check(!p.GetProperty("extensions").EnumerateObject().Any(),"empty registry: extensions snapshot empty");

        // 8. 已有模組的 S250 子碼仍走原處理（例：S250/41 角色設定），不會落到擴充點。
        Fixture.Calls.Clear();
        var core=new WorldSession();core.UseExtensions(fixture);
        try{core.Receive([250,41],"");}catch(Exception){/* 原模組對截斷封包的處理不在本測試範圍 */}
        t.Check(!Fixture.Calls.Any(c=>c.StartsWith("S250/any",StringComparison.Ordinal)),"core S250/41 not routed to module fallback",Fixture.Calls.ToArray());
    }
}
