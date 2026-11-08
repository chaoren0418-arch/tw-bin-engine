using System.Reflection;
using System.Text.Json;
namespace P06;

// T243（#568）水中呼吸狀態圖示資料的離線測試：只用合成 S126 封包，不開遊戲、網路或素材。
// 依據 docs/reference/effects-2.md 第 3 節：原版 S_BREATH 只呼叫 sub_F05A90(秒, 11, 0, -1, 1)。
static class BreathStatusTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Eva(uint id,int seconds)=>LoginPackets.Pad([126,..Le32(id),..Le16((ushort)seconds)]);
    static JsonElement Icons(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("statusEffects");
    static WorldSession Own(uint id,Func<long> clock){
        var s=new WorldSession();s.MagicStatusClock=clock;
        typeof(WorldSession).GetProperty("Status")!.SetValue(s,new OwnStatus(id,1,0,10,11,12,13,14,15,100,100,100,100,0,0,0,50,0,0,0,0,0,0));
        return s;
    }

    [WorldModuleTest("breath-status")]
    static void Run(WorldModuleTestContext t){
        // 1. 對照：只有水中呼吸對到 effect 11、顯示模式 0。
        t.Equal(11,TimedBuffRules.WaterBreathEffect,"water breath is effectlist2 effect 11 (WATER_BREATH_S)");
        t.Equal(0,TimedBuffRules.WaterBreathDisplayMode,"display mode 0 (seconds tooltip)");
        t.Equal((int?)11,TimedBuffRules.StatusEffect(TimedBuffRules.WaterBreath),"WaterBreath buff maps to effect 11");
        t.Equal((int?)null,TimedBuffRules.StatusEffect(42),"unverified buffs have no effect id here");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry has no errors",WorldExtensionRegistry.Default.Errors);
        t.Check(WorldExtensionRegistry.Default.Entries.Any(e=>e.Kind=="snapshot"&&e.Key=="statusEffects"&&e.Method=="WorldSession.StatusEffectSnapshot"),"statusEffects snapshot registered");

        // 2. S126 加入 effect 11，剩餘秒數沿用 T153 計時。
        long now=50_000;var s=Own(123,()=>now);
        t.Equal(0,s.StatusEffectIcons.Length,"no icon before S126");
        int revision=s.MagicRevision;
        s.Receive(Eva(123,1800),"");
        var icon=s.StatusEffectIcons.SingleOrDefault();
        t.Check(icon is {Effect:11,DisplayMode:0,Skill:TimedBuffRules.WaterBreath,Name:"水中呼吸",Seconds:1800},"S126 adds effect 11 with 1800 s",icon);
        t.Check(s.MagicRevision>revision,"S126 bumps revision for redraw");
        var j=Icons(s);
        t.Equal(1,j.GetArrayLength(),"snapshot has one status effect");
        t.Equal(11,j[0].GetProperty("effect").GetInt32(),"snapshot effect id");
        t.Equal(1800,j[0].GetProperty("seconds").GetInt32(),"snapshot seconds");
        now+=1000;
        t.Equal(1799,s.StatusEffectIcons.Single().Seconds,"remaining seconds count down with the T153 timer");
        t.Equal(s.MagicBuffs.Single(b=>b.Skill==TimedBuffRules.WaterBreath).Remaining(now),s.StatusEffectIcons.Single().Seconds,"icon seconds equal the buff timer");

        // 3. 再收 S126 刷新秒數，仍只有一個圖示。
        s.Receive(Eva(123,3600),"");
        t.Equal(1,s.StatusEffectIcons.Length,"refresh keeps a single effect 11");
        t.Equal(3600,s.StatusEffectIcons.Single().Seconds,"refresh resets seconds");

        // 4. 秒 = 0 移除（sub_F05A90：秒 0 且已存在時移除）。
        s.Receive(Eva(123,0),"");
        t.Equal(0,s.StatusEffectIcons.Length,"seconds 0 removes effect 11");
        t.Equal(0,Icons(s).GetArrayLength(),"snapshot empty after removal");

        // 5. 時間到自然消失。
        s.Receive(Eva(123,2),"");now+=1999;
        t.Equal(1,s.StatusEffectIcons.Single().Seconds,"one second left");
        now+=1;
        t.Equal(0,s.StatusEffectIcons.Length,"expired effect 11 disappears");

        // 6. 其他物件：沿用 T153（3.81 只送給本人），不影響圖示。
        s.Receive(Eva(456,60),"");
        t.Equal(0,s.StatusEffectIcons.Length,"other object's S126 ignored");

        // 7. 其他 buff 不出現在 effect 清單（沒有查證過的 effect id）。
        s.Receive(LoginPackets.Pad([216,..Le16(30),5]),"");
        t.Check(s.MagicBuffs.Length==1&&s.StatusEffectIcons.Length==0,"shield buff kept in MagicBuffs but not mapped to an effect");

        // 8. Reset 清掉。
        s.Receive(Eva(123,60),"");s.Reset();
        t.Equal(0,s.StatusEffectIcons.Length,"Reset clears effect 11");

        // 9. 截斷封包整個拒絕，不留圖示。
        var fresh=Own(123,()=>now);
        t.Throws<ProtocolException>(()=>fresh.Receive([126,123,0,0,0,60],""),"truncated S126 rejected");
        t.Equal(0,fresh.StatusEffectIcons.Length,"rejected S126 adds no icon");
    }
}
