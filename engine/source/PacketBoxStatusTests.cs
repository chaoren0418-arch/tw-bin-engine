using System.Text.Json;
namespace P06;

// T230（#548）S250 狀態圖示類子碼的離線測試：只用依 3.81 伺服器寫法合成的封包，不開遊戲、網路或素材。
static class PacketBoxStatusTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] Box(byte sub,byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    // S_PacketBoxCooking(pc,type,time)：能力 6 個 C、H(食物)、C(類型)、C(圖示種類)、H(秒)、C(0)。
    static byte[] Cooking(byte type,ushort seconds,byte kind=0x26,ushort food=750)=>Box(53,[18,12,14,16,15,9,..Le16(food),type,kind,..Le16(seconds),0]);
    static byte[] Protection(uint model,uint on)=>Box(114,[..Le32(model),..Le32(on)]);
    static byte[] NoneTime(byte on,uint icon,uint text)=>Box(180,[on,..Le32(icon),..Le32(text),..Le16(0)]);
    static byte[] MapTimer(params (uint order,string name,uint minutes)[] maps)=>Box(159,[..Le32((uint)maps.Length),..maps.SelectMany(m=>(byte[])[..Le32(m.order),..Str(m.name),..Le32(m.minutes)])]);
    static JsonElement State(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("packetBoxStatus");
    static JsonElement Unhandled(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandledSubtypes");
    static MagicBuff? Buff(WorldSession s,int key)=>s.MagicBuffs.FirstOrDefault(b=>b.Skill==key);

    [WorldModuleTest("packetbox-status")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：13 個子碼都掛在 Default 登錄表、同一個處理器；登錄表無錯誤。
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0,"default registry has no errors",reg.Errors);
        foreach(byte sub in PacketBoxStatusRules.Subtypes)
            t.Check(reg.Entries.Any(e=>e.Kind=="receive"&&e.Opcode==250&&e.Subtype==sub&&e.Method=="WorldSession.ReceivePacketBoxStatus"),$"S250/{sub} registered");
        t.Equal(13,PacketBoxStatusRules.Subtypes.Length,"13 B2 subtypes");
        t.Check(reg.Entries.Any(e=>e.Kind=="snapshot"&&e.Key=="packetBoxStatus"),"snapshot registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetPacketBoxStatus"),"reset registered");

        long now=1_000_000;
        var s=new WorldSession();s.MagicStatusClock=()=>now;
        var changed=new List<int>();s.PacketBoxStatusChanged+=changed.Add;
        var st=State(s);
        t.Equal(JsonValueKind.Null,st.GetProperty("karma").ValueKind,"initial karma null");
        t.Equal(0,st.GetProperty("protections").GetArrayLength(),"initial protections empty");

        // 2. 53 料理：計時圖示＋能力／食物資料；時間 0 移除。
        s.Receive(Cooking(3,1800),"");
        t.Check(Buff(s,-53003) is {Seconds:1800,Source:"cooking",Name:"料理 3"},"cooking buff");
        var c=State(s).GetProperty("cooking");
        t.Equal(3,c.GetProperty("type").GetInt32(),"cooking type");
        t.Equal(18,c.GetProperty("str").GetInt32(),"cooking str");
        t.Equal(9,c.GetProperty("cha").GetInt32(),"cooking cha");
        t.Equal(750,c.GetProperty("food").GetInt32(),"cooking food");
        s.Receive(Cooking(7,600,kind:0x24),"");t.Check(Buff(s,-53007)?.Seconds==600,"cooking type 7 uses icon kind 0x24");
        s.Receive(Box(53,[0,0,0,0,0,0,..Le16(0),54,0x2A,..Le16(300),0]),"");t.Check(Buff(s,-53054)?.Seconds==300,"cooking type 54 uses icon kind 0x2A");
        s.Receive(Cooking(3,0),"");t.Check(Buff(s,-53003)==null&&Buff(s,-53007)!=null,"cooking stop removes only that type");

        // 3. 56 魔法娃娃：H(秒)。
        s.Receive(Box(56,[..Le16(1800)]),"");t.Check(Buff(s,PacketBoxStatusRules.DollBuff) is {Seconds:1800,Name:"魔法娃娃"},"doll buff");
        now+=1000;t.Equal(1799,Buff(s,PacketBoxStatusRules.DollBuff)!.Remaining(now),"doll countdown");
        s.Receive(Box(56,[..Le16(0)]),"");t.Check(Buff(s,PacketBoxStatusRules.DollBuff)==null,"doll removed");

        // 4. 60 三段加速：C(秒>>2) C(8)，4 秒單位、約略。
        s.Receive(Box(60,[150,8]),"");t.Check(Buff(s,PacketBoxStatusRules.CakeBuff) is {Seconds:600,Approximate:true,Name:"三段加速"},"cake four-second units");

        // 5. 153 地圖限時：D(秒)。
        s.Receive(Box(153,[..Le32(3600)]),"");t.Check(Buff(s,PacketBoxStatusRules.MapLimitBuff) is {Seconds:3600,Name:"地圖限時"},"map limit timer");
        s.Receive(Box(153,[..Le32(0)]),"");t.Check(Buff(s,PacketBoxStatusRules.MapLimitBuff)==null,"map limit timer cleared");

        // 6. 59 DEL_ICON：移除水之元氣（技能 170），其他狀態不動。
        s.Receive(Box(59,[..Le16(0)]),"");
        t.Equal(1,State(s).GetProperty("delIcons").GetInt32(),"del icon counted");
        t.Check(Buff(s,PacketBoxStatusRules.CakeBuff)!=null,"del icon keeps unrelated buffs");
        t.Equal("水之元氣",MagicStatusCatalog.Name(170),"water life name");

        // 7. 數值類：75、82、87（有號）、132、160。
        s.Receive(Box(75,[2]),"");s.Receive(Box(82,[37,0,0,0]),"");s.Receive(Box(87,[..Le32(unchecked((uint)-12345))]),"");
        s.Receive(Box(132,[15]),"");s.Receive(Box(160,[15]),"");
        st=State(s);
        t.Equal(2,st.GetProperty("dkLevel").GetInt32(),"dk level");
        t.Equal(37,st.GetProperty("leaves").GetInt32(),"leaves value");
        t.Equal(-12345,st.GetProperty("karma").GetInt32(),"karma signed");
        t.Equal(15,st.GetProperty("evasion").GetInt32(),"evasion");
        t.Equal(15,st.GetProperty("weaponRange").GetInt32(),"weapon range (-1 written as 15)");
        t.Check(s.Karma==-12345&&s.PacketBoxDkLevel==2&&s.EvasionRate==15&&s.WeaponRange==15&&s.LeavesValue==37,"public properties");

        // 8. 114 保護圖示：開／關集合，未知種類只計數。
        s.Receive(Protection(6,1),"");s.Receive(Protection(0,1),"");s.Receive(Protection(3,1),"");s.Receive(Protection(3,0),"");s.Receive(Protection(9,1),"");
        t.Check(s.Protections.SequenceEqual([PacketBoxProtection.JusticeL1,PacketBoxProtection.Encounter]),"protection set",s.Protections);
        t.Equal(1,State(s).GetProperty("unknownProtections").GetInt32(),"unknown protection counted");

        // 9. 180 無時間圖示：以圖示 id 為鍵，0 關閉。
        s.Receive(NoneTime(1,4523,0xD67),"");s.Receive(NoneTime(1,100,7),"");s.Receive(NoneTime(1,4523,3432),"");
        t.Check(s.NoneTimeIcons.SequenceEqual([new PacketBoxNoneTimeIcon(100,7),new PacketBoxNoneTimeIcon(4523,3432)]),"none-time icons keyed and refreshed",s.NoneTimeIcons);
        s.Receive(NoneTime(0,100,0),"");t.Check(s.NoneTimeIcons.Length==1&&s.NoneTimeIcons[0].IconId==4523,"none-time icon off");

        // 10. 159 限時地圖清單（含 CP950 名稱）；新清單取代舊清單。
        s.Receive(MapTimer((1,"古魯丁地監",120),(2,"Ivory Tower",0)),"");
        var maps=State(s).GetProperty("mapTimes");
        t.Equal(2,maps.GetArrayLength(),"map timer count");
        t.Equal("古魯丁地監",maps[0].GetProperty("mapName").GetString(),"map timer CP950 name");
        t.Equal(120,maps[0].GetProperty("minutes").GetInt32(),"map timer minutes");
        s.Receive(MapTimer(),"");t.Equal(0,State(s).GetProperty("mapTimes").GetArrayLength(),"empty map timer replaces list");

        // 11. 全部被消化：沒有留下未處理子碼；事件依序觸發。
        var unhandled=Unhandled(s);
        foreach(byte sub in PacketBoxStatusRules.Subtypes)t.Check(!unhandled.TryGetProperty($"0xFA/0x{sub:X2}",out _),$"S250/{sub} consumed");
        t.Check(changed.Contains(53)&&changed.Contains(180)&&changed.Contains(159)&&changed.Count==26,"change events",changed);
        t.Equal(2,State(s).GetProperty("packets").GetProperty("153").GetInt32(),"per-subtype packet count");

        // 12. 既有 MagicStatus 子碼仍由原模組處理，不被本模組搶走。
        s.Receive(Box(34,[..Le16(60)]),"");t.Check(s.MagicBuffs.Any(b=>b.Source=="packetbox34"),"existing S250/34 retained");

        // 13. 格式錯誤：丟 ProtocolException 且不改狀態。
        var before=s.MagicBuffs;string snap=State(s).GetRawText();
        void Reject(byte[] p,string label){t.Throws<ProtocolException>(()=>s.Receive(p,""),label);}
        Reject([250,56,1],"truncated doll");
        Reject(Box(60,[10,7]),"cake type not 8");
        Reject(Box(59,[..Le16(1)]),"del icon nonzero");
        Reject(Box(82,[1,0,1,0]),"leaves nonzero filler");
        Reject(Protection(1,2),"protection switch 2");
        Reject(NoneTime(2,1,1),"none-time switch 2");
        Reject(Cooking(3,60,kind:0x24),"cooking icon kind mismatch");
        Reject(Box(53,[1,2,3,4,5,6,..Le16(0),3,0x26,..Le16(60),1]),"cooking nonzero tail");
        Reject(Box(153,[..Le32(0x8000_0000)]),"negative map limit");
        Reject(Box(159,[..Le32(1000)]),"map timer count exceeds packet");
        Reject(Box(159,[..Le32(2),..Le32(1),..Str("x"),..Le32(5)]),"map timer truncated entry");
        var badPad=Box(87,[..Le32(1)]);badPad[^1]=1;Reject(badPad,"nonzero padding");
        Reject([..Box(132,[1]),0,0,0,0,0,0,0,0],"extra block");
        t.Check(before.SequenceEqual(s.MagicBuffs),"malformed packets keep buffs");
        t.Equal(snap,State(s).GetRawText(),"malformed packets keep state");

        // 14. Reset 清除。
        s.Reset();st=State(s);
        t.Equal(JsonValueKind.Null,st.GetProperty("karma").ValueKind,"reset karma");
        t.Equal(JsonValueKind.Null,st.GetProperty("mapTimes").ValueKind,"reset map times");
        t.Equal(JsonValueKind.Null,st.GetProperty("cooking").ValueKind,"reset cooking");
        t.Check(s.Protections.Length==0&&s.NoneTimeIcons.Length==0&&s.MagicBuffs.Length==0,"reset clears sets and buffs");
        t.Equal(0,st.GetProperty("delIcons").GetInt32(),"reset counters");
    }
}
