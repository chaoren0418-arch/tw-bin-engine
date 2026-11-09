using System.Text.Json;
using P06.NativeUi;
namespace P06;

// T265（#626，R2-L）狀態圖示資料收尾的離線測試：只用依 3.81 伺服器寫法合成的封包，不開遊戲、網路或素材。
// 依據：PacketBoxStatus.cs 檔頭的原版對照（TW16 sub_9357E0 case 59／75／82／180）、AbilityStatus.cs 的 S36 對照、
// ui-layout.md 第 4 節（effect id）、effects-2.md 第 3 節（水中呼吸 effect 11）。
static class StatusIconFeedTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Box(byte sub,byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    static byte[] Dk(byte level)=>Box(75,[level]);
    static byte[] Leaves(byte value)=>Box(82,[value,0,0,0]);
    static byte[] NoneTime(byte on,uint effect,uint text)=>Box(180,[on,..Le32(effect),..Le32(text),..Le16(0)]);
    static byte[] Eva(uint id,int seconds)=>LoginPackets.Pad([126,..Le32(id),..Le16((ushort)seconds)]);
    static byte[] SpMr(int sp,int mr){var b=new byte[9];b[0]=37;BitConverter.TryWriteBytes(b.AsSpan(1),sp);BitConverter.TryWriteBytes(b.AsSpan(5),mr);return LoginPackets.Pad(b);}
    static JsonElement Box(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("packetBoxStatus");
    static WorldSession Own(uint id,Func<long> clock){
        var s=new WorldSession();s.MagicStatusClock=clock;
        typeof(WorldSession).GetProperty("Status")!.SetValue(s,new OwnStatus(id,1,0,10,11,12,13,14,15,100,100,100,100,0,0,0,50,0,0,0,0,0,0));
        return s;
    }
    static string Icons(WorldSession s)=>string.Join(",",s.PacketBoxStatusIcons.Select(i=>$"{i.Effect}/{i.DisplayMode}/{i.Value?.ToString()??"-"}/{i.IconOverride?.ToString()??"-"}"));

    [WorldModuleTest("status-icon-feed")]
    static void Run(WorldModuleTestContext t){
        // 1. 原版常數（PacketBoxStatus.cs 檔頭）。
        t.Equal(57,PacketBoxStatusRules.WaterLifeEffect,"59 removes effect 57 WATER_LIFE_S");
        t.Equal(170,PacketBoxStatusRules.WaterLifeSkill,"engine key of effect 57 is skill 170");
        t.Equal(135,PacketBoxStatusRules.DkEffect,"75 uses effect 135");
        t.Equal(3229,PacketBoxStatusRules.DkIcon(1),"75 level 1 icon 0xC9B+3-1");
        t.Equal(3228,PacketBoxStatusRules.DkIcon(2),"75 level 2 icon");
        t.Equal(3227,PacketBoxStatusRules.DkIcon(3),"75 level 3 icon");
        t.Equal((int?)null,PacketBoxStatusRules.LeavesEffectFor(0),"82 value 0 removes");
        t.Equal((int?)175,PacketBoxStatusRules.LeavesEffectFor(1),"82 value 1 → effect 175");
        t.Equal((int?)175,PacketBoxStatusRules.LeavesEffectFor(200),"82 value 200 → effect 175");
        t.Equal((int?)575,PacketBoxStatusRules.LeavesEffectFor(201),"82 value > 200 → effect 575");
        t.Check(PacketBoxStatusRules.LeavesRemoved.SequenceEqual([175,518,574,575,576]),"82 removal list");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry has no errors",WorldExtensionRegistry.Default.Errors);

        long now=2_000_000;var s=Own(77,()=>now);
        t.Equal(0,s.PacketBoxStatusIcons.Length,"no B2 icons initially");

        // 2. 75 龍騎士弱點曝光：階段 >0 → effect 135、模式 0、預設秒數、icon 改寫；0 → 移除。
        int rev=s.MagicRevision;
        s.Receive(Dk(2),"");
        var dk=s.PacketBoxStatusIcons.Single();
        t.Check(dk is {Effect:135,DisplayMode:0,Value:null,DefaultDuration:true,IconOverride:3228,Origin:"dk:2"},"75 level 2 icon",dk);
        t.Check(s.MagicRevision>rev,"75 bumps MagicRevision for redraw");
        s.Receive(Dk(3),"");t.Equal("135/0/-/3227",Icons(s),"75 level 3 replaces level 2");
        s.Receive(Dk(0),"");t.Equal("",Icons(s),"75 level 0 removes effect 135");
        t.Equal(0,Box(s).GetProperty("dkLevel").GetInt32(),"75 level 0 still recorded as value");

        // 3. 82 殷海薩的祝福：值 → effect 175、模式 1（百分比）；>200 原版用 575（不在表）→ 不列；0 → 移除。
        s.Receive(Leaves(37),"");t.Equal("175/1/37/-",Icons(s),"82 value 37 → effect 175 percent");
        s.Receive(Leaves(200),"");t.Equal("175/1/200/-",Icons(s),"82 value 200 → effect 175");
        s.Receive(Leaves(201),"");t.Equal("",Icons(s),"82 value 201 → effect 575, not created");
        s.Receive(Leaves(0),"");t.Equal("",Icons(s),"82 value 0 removes");

        // 4. 180 無時間圖示：第一個 D 是 effect id；無期限；字串 id 照收不影響；≥540 不建立；0 關閉。
        s.Receive(NoneTime(1,396,3432),"");s.Receive(NoneTime(1,78,0),"");s.Receive(NoneTime(1,600,1),"");
        t.Equal("78/0/-1/-,396/0/-1/-",Icons(s),"180 effects sorted, no limit, effect ≥ 540 skipped");
        t.Equal((uint)396,s.NoneTimeIcons.Single(i=>i.IconId==396).Effect,"180 IconId is the effect id");
        t.Equal(3,s.NoneTimeIcons.Length,"180 raw set keeps every received id");
        s.Receive(NoneTime(0,396,0),"");t.Equal("78/0/-1/-",Icons(s),"180 off removes effect");

        // 5. 三種一起：順序 75、82、180；快照 statusIcons 同內容。
        s.Receive(Dk(1),"");s.Receive(Leaves(5),"");
        t.Equal("135/0/-/3229,175/1/5/-,78/0/-1/-",Icons(s),"combined order 75, 82, 180");
        var snap=Box(s).GetProperty("statusIcons");
        t.Equal(3,snap.GetArrayLength(),"snapshot statusIcons count");
        t.Equal(135,snap[0].GetProperty("effect").GetInt32(),"snapshot effect 135");
        t.Equal(3229,snap[0].GetProperty("iconOverride").GetInt32(),"snapshot icon override");
        t.Check(snap[0].GetProperty("defaultDuration").GetBoolean(),"snapshot default duration");
        t.Equal(1,snap[1].GetProperty("mode").GetInt32(),"snapshot leaves percent mode");
        t.Equal(5,snap[1].GetProperty("value").GetInt32(),"snapshot leaves value");
        t.Equal(-1,snap[2].GetProperty("value").GetInt32(),"snapshot none-time no limit");

        // 6. WinForms 狀態列文字：沒有倒數的狀態也列出，文字取代秒數；Id 不與技能鍵相撞。
        var hud=StatusIconFeed.Read(s);
        t.Equal("弱點曝光 · 第 1 階|殷海薩的祝福 · 5%|狀態 78 · 無期限",string.Join("|",hud.Select(h=>h.Detail)),"HUD text for 75/82/180");
        t.Check(hud.Select(h=>h.Id).SequenceEqual([-1_000_135,-1_000_175,-1_000_078]),"HUD ids are -(1e6+effect)",hud.Select(h=>h.Id));
        t.Equal("料理 · 約3 秒",new StatusIconEntry(1,"料理",3,false,true).Detail,"timed entries keep the seconds text");

        // 7. 59：只移除技能 170（＝effect 57），不論來源；其他狀態與 B2 圖示不動。
        s.Receive(LoginPackets.Pad([250,154,..Le16(64),..Le16(529),..Le16(0),..Le16(0)]),"");   // 250/154 代理 key 529 → 技能 26
        s.Receive(Eva(77,600),"");
        ForceBuff(s,PacketBoxStatusRules.WaterLifeSkill,64,"native-weapon");
        int before=s.MagicBuffs.Length;
        s.Receive(Box(59,[..Le16(0)]),"");
        t.Check(s.MagicBuffs.All(b=>b.Skill!=PacketBoxStatusRules.WaterLifeSkill),"59 removed skill 170");
        t.Equal(before-1,s.MagicBuffs.Length,"59 removed only one buff");
        t.Equal("135/0/-/3229,175/1/5/-,78/0/-1/-",Icons(s),"59 keeps B2 icons");
        s.Receive(Box(59,[..Le16(0)]),"");t.Equal(before-1,s.MagicBuffs.Length,"59 without effect 57 changes nothing");

        // 8. 水中呼吸 effect 11 經 #473 的 BuffState 對照出現（effects-2.md 第 3 節）。
        var buffs=BuffState.From(s,null,BuffTooltipText.None);
        var breath=buffs.Entries.SingleOrDefault(e=>e.Effect==11);
        t.Check(breath is {Seconds:600,Expired:false,Origin:"bless-of-eva:-3126"},"S126 → BuffState effect 11 with seconds",breath);
        t.Check(buffs.Unmapped.All(u=>!u.Origin.StartsWith("bless-of-eva",StringComparison.Ordinal)),"water breath never unmapped");
        s.Receive(Eva(77,0),"");
        t.Check(BuffState.From(s,null,BuffTooltipText.None).Entries.All(e=>e.Effect!=11),"S126 seconds 0 removes effect 11 from BuffState");

        // 9. S37 SP／MR 接到左狀態框（#598 LeftStatusState）；S36 不進左狀態框。
        t.Check(LeftStatusState.From(s) is {Sp:null,Mr:null},"no S37 → SP/MR blank");
        s.Receive(SpMr(12,45),"");
        var left=LeftStatusState.From(s);
        t.Check(left is {Sp:12,Mr:45},"S37 → left status SP 12 / MR 45",left);
        t.Equal("12",left.SpText,"left SP text");
        t.Equal("45%",left.MrText,"left MR text");
        s.Receive(LoginPackets.Pad([37,50,100]),"");t.Check(LeftStatusState.From(s) is {Sp:50,Mr:100},"S37 byte form → left status");
        int buffCount=s.MagicBuffs.Length;string b2=Icons(s);
        s.Receive(LoginPackets.Pad([36,1,1]),"");
        t.Check(LeftStatusState.From(s) is {Sp:50,Mr:100},"S36 does not change left status");
        t.Check(s.MagicBuffs.Length==buffCount&&Icons(s)==b2,"S36 adds no status icon");

        // 10. S36 type 對照：原版只留 type < 6；名稱取自 3.81 L1EquipmentSlot。
        t.Equal("傳送控制戒指",AbilityRules.Name(1),"type 1 name");
        t.Equal("變形控制戒指",AbilityRules.Name(2),"type 2 name");
        t.Equal((string?)null,AbilityRules.Name(3),"type 3 unnamed");
        t.Check(AbilityRules.KeptByOriginal(5)&&!AbilityRules.KeptByOriginal(6)&&!AbilityRules.KeptByOriginal(255),"original keeps type 0–5 only");
        s.Receive(LoginPackets.Pad([36,7,1]),"");
        var flags=WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("ability").GetProperty("flags");
        t.Equal("傳送控制戒指",flags[0].GetProperty("name").GetString(),"snapshot flag name");
        t.Check(flags[0].GetProperty("keptByOriginal").GetBoolean()&&!flags[1].GetProperty("keptByOriginal").GetBoolean(),"snapshot keptByOriginal");

        // 11. 格式錯誤不改圖示；Reset 清空。
        string icons=Icons(s);
        t.Throws<ProtocolException>(()=>s.Receive(Box(82,[9,1,0,0]),""),"82 nonzero filler rejected");
        t.Throws<ProtocolException>(()=>s.Receive(NoneTime(2,79,0),""),"180 switch 2 rejected");
        t.Equal(icons,Icons(s),"malformed packets keep icons");
        s.Reset();
        t.Equal(0,s.PacketBoxStatusIcons.Length,"Reset clears B2 icons");
        t.Equal(0,Box(s).GetProperty("statusIcons").GetArrayLength(),"Reset clears snapshot statusIcons");
    }

    // 3.81 不送水之元氣的加圖示封包；用反射放一筆技能 170 的計時，驗證 59 只刪這個鍵。
    static void ForceBuff(WorldSession s,int skill,int seconds,string source){
        var m=typeof(WorldSession).GetMethod("ReceiveBuff",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        m.Invoke(s,[skill,seconds,source,false,null,false]);
    }
}
