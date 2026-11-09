using P06.NativeUi;
namespace P06;

// R5-D（T309／#736）離線測試：合成封包、記憶體送出閘門、假時鐘；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
// 涵蓋：S_BLINK 等該步走完才回 C52（PG§5）；表情 hold／節流的 1234 ms 保留與速度係數（AN§1）；妖精 Lv ≥ 50 的書籤傳送分支
// （FU3§3.1，職業碼依 FU6§2）；召喚暫行清單的怪物名稱；S241 沒有本地時間上限。
public sealed partial class WorldSession {
    internal void SeedProtocolFollowupTest(ushort gfx=61,byte level=50,ushort map=4){
        Entered=true;MapId=map;TimeReplies=1;CacheEnabled=false;
        Status=new(ProtocolFollowupTests.Own,level,0,10,10,10,10,10,10,100,100,100,100,10,0,0,0,0,0,0,0,0,0);
        Actor=new(32600,32940,ProtocolFollowupTests.Own,gfx,0,0,0,0,0,0,ProtocolFollowupTests.Name,"",0,0,"","",0,100,0,level,0,0,0);
        OwnDead=false;
    }
    internal void SetTeleportStepForProtocolTest(int? ms)=>TeleportStepMs=ms;
    internal void LearnForProtocolTest(int skill){learnedMagic.Add(skill);SelectMagic(skill);}
}
public sealed partial class LoginConnection {
    internal void SeedProtocolFollowupTest(List<byte[]> sent,ushort gfx=61,byte level=50,ushort map=4){
        World.SeedProtocolFollowupTest(gfx,level,map);State=LoginState.InWorld;
        sendMovementGate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
    }
    internal void SetLastMovementForProtocolTest(long at)=>lastMovementAt=at;
    internal Task AcknowledgeTeleportForProtocolTest()=>AcknowledgeTeleport();
    internal bool TryOpenRingWindowForProtocolTest()=>TryOpenRingWindow();
    internal void AddCharacterForProtocolTest(string name,byte cls)=>characters.Add(new(name,"",cls,0,0,100,100,10,50,10,10,10,10,10,10,0,0,0));
}

static class ProtocolFollowupTests {
    public const uint Own=0x7360;
    public const string Name="r5d-test";
    static LoginConnection Seed(List<byte[]> sent,ushort gfx=61,byte level=50,ushort map=4){var c=new LoginConnection();c.SeedProtocolFollowupTest(sent,gfx,level,map);return c;}
    static byte[] Blink(ushort marker=1,uint portal=2)=>LoginPackets.Pad([149,(byte)marker,(byte)(marker>>8),..BitConverter.GetBytes(portal)]);
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();

    [WorldModuleTest("protocol-followup")]
    static void Run(WorldModuleTestContext t){Blinks(t);EmoteHold(t);EmoteThrottle(t);MoveLock(t);ElfTeleport(t);SummonNames(t);}

    // PG§5: sub_931B90 defers C_BLINK while the own step is in progress (sub_A43510), sub_A48590 sends it when the step ends.
    static void Blinks(WorldModuleTestContext t){
        {   // No step in progress → C52 at once, without waiting.
            var sent=new List<byte[]>();var c=Seed(sent);long now=10000;c.MovementClock=()=>now;int waited=-1;c.TeleportStepWait=ms=>{waited=ms;return Task.CompletedTask;};
            c.World.Receive(Blink(),"offline");
            t.Check(c.World.TeleportAckPending&&c.World.TeleportStepMs==null,"S149 without a predicted step records no step",new{c.World.TeleportState,c.World.TeleportStepMs});
            c.AcknowledgeTeleportForProtocolTest().GetAwaiter().GetResult();
            t.Check(waited==-1&&sent.Count==1&&sent[0][0]==52&&c.World.TeleportState=="awaiting-world","idle: C52 sent immediately",new{waited,sent.Count,c.World.TeleportState});
        }
        {   // Step in progress → no C52 until the step ends.
            var sent=new List<byte[]>();var c=Seed(sent);long now=10000;c.MovementClock=()=>now;
            var gate=new TaskCompletionSource();int waited=-1;c.TeleportStepWait=ms=>{waited=ms;return gate.Task;};
            c.World.Receive(Blink(),"offline");c.World.SetTeleportStepForProtocolTest(640);c.SetLastMovementForProtocolTest(now-100);
            t.Equal(now+540,c.TeleportStepEndsAt,"step end = last C29 + step animation");
            var pending=c.AcknowledgeTeleportForProtocolTest();
            t.Check(!pending.IsCompleted&&waited==540&&sent.Count==0,"walking: C52 deferred for the rest of the step",new{waited,sent.Count});
            t.Check(c.World.TeleportAckPending&&c.World.TeleportNotice.Contains("還沒走完"),"walking: notice says the ack waits for the step",c.World.TeleportNotice);
            c.AcknowledgeTeleportForProtocolTest().GetAwaiter().GetResult();
            t.Check(sent.Count==0,"no second C52 while one is deferred");
            now+=540;gate.SetResult();pending.GetAwaiter().GetResult();
            t.Check(sent.Count==1&&sent[0][0]==52&&c.World.TeleportState=="awaiting-world","step end: one C52",new{sent.Count,c.World.TeleportState});
        }
        {   // Step already over → immediate.
            var sent=new List<byte[]>();var c=Seed(sent);long now=10000;c.MovementClock=()=>now;int waited=-1;c.TeleportStepWait=ms=>{waited=ms;return Task.CompletedTask;};
            c.World.Receive(Blink(),"offline");c.World.SetTeleportStepForProtocolTest(640);c.SetLastMovementForProtocolTest(now-640);
            c.AcknowledgeTeleportForProtocolTest().GetAwaiter().GetResult();
            t.Check(waited==-1&&sent.Count==1,"step that just ended does not defer",new{waited,sent.Count});
        }
        {   // World reset during the wait → the deferred C52 is dropped.
            var sent=new List<byte[]>();var c=Seed(sent);long now=10000;c.MovementClock=()=>now;
            var gate=new TaskCompletionSource();c.TeleportStepWait=_=>gate.Task;
            c.World.Receive(Blink(),"offline");c.World.SetTeleportStepForProtocolTest(640);c.SetLastMovementForProtocolTest(now);
            var pending=c.AcknowledgeTeleportForProtocolTest();c.World.Reset();gate.SetResult();pending.GetAwaiter().GetResult();
            t.Check(sent.Count==0&&!c.World.TeleportAckPending,"stale deferred C52 not sent",new{sent.Count,c.World.TeleportState});
        }
        {   // Reset clears the recorded step.
            var w=new WorldSession();w.SetTeleportStepForProtocolTest(640);w.Reset();
            t.Equal((int?)null,w.TeleportStepMs,"Reset clears the step length");
        }
    }

    // AN§1 + BIN 0x4FBABC–0x4FBAF4: hold=1 → byte_153CE6A for 1234 ms; movement input held off meanwhile.
    static void EmoteHold(WorldModuleTestContext t){
        t.Equal(1234,TitleAndEmotePackets.HoldMs,"hold timer 0x4D2");
        var sent=new List<byte[]>();var c=Seed(sent,gfx:6080);long now=1000;c.ActionClock=()=>now;
        var plan=TitleAndEmotePackets.Plan(6080,1,true);
        t.Check(plan.Delivery==EmoteDelivery.Immediate&&plan.Hold,"horse helm menu item 1 is immediate with hold");
        t.Check(!c.World.EmoteHeld&&!c.World.MoveLocked,"no hold before the emote");
        var stamp=c.CaptureTitleEmote();
        t.Check(Wait(c.SendEmote(stamp,1))&&sent.Count==1&&sent[0][0]==120&&sent[0][1]==68,"hold emote sends C120 68",new{sent.Count});
        t.Check(c.World.EmoteHeld&&c.World.MoveLocked&&!c.World.ServerMoveLocked&&!c.MovementAvailable,"hold: movement held off, not a server lock");
        now=1000+1233;t.Check(c.World.EmoteHeld,"still held at 1233 ms");
        now=1000+1234;t.Check(!c.World.EmoteHeld&&!c.World.MoveLocked,"released at 1234 ms");
        // Class forms send without hold.
        var sent2=new List<byte[]>();var d=Seed(sent2,gfx:61);d.ActionClock=()=>now;
        t.Check(Wait(d.SendEmote(d.CaptureTitleEmote(),1))&&sent2.Count==1&&!d.World.EmoteHeld,"class form: no hold");
        // A new world drops the hold.
        var sent3=new List<byte[]>();var e=Seed(sent3,gfx:6080);e.ActionClock=()=>now;Wait(e.SendEmote(e.CaptureTitleEmote(),1));
        e.World.Reset();t.Check(!e.World.EmoteHeld,"Reset drops the hold");
    }

    // sub_4FAAF0 (0x4FAB18–0x4FAC36): interval with speed factors, strict "> interval", and the 1234 ms hold even when not sent.
    static void EmoteThrottle(WorldModuleTestContext t){
        var sent=new List<byte[]>();var c=Seed(sent,gfx:3225);long now=10000;c.ActionClock=()=>now;var stamp=c.CaptureTitleEmote();
        t.Equal(2500,c.EmoteThrottleMs(),"normal speed: 2500 ms");
        t.Check(Wait(c.SendEmote(stamp,1))&&sent.Count==1&&c.World.ActiveEmotes(now).Length==0,"throttled emote sent, no local play");
        t.Check(c.World.EmoteHeld,"throttled send also sets the hold");
        now=11234;t.Check(!c.World.EmoteHeld,"hold over after 1234 ms");
        now=12000;t.Check(!Wait(c.SendEmote(stamp,2))&&sent.Count==1,"inside the interval: not sent");
        t.Check(c.World.EmoteHeld,"refused attempt still sets the hold (0x4FAC18)");
        now=12500;t.Check(!Wait(c.SendEmote(stamp,2))&&sent.Count==1,"exactly 2500 ms: not sent (jbe)");
        now=12501;t.Check(Wait(c.SendEmote(stamp,2))&&sent.Count==2&&sent[1][1]==69,"2501 ms: sent");
        now=13233;t.Check(c.World.EmoteHeld,"throttled re-arm does not extend a running hold");
        now=13234;t.Check(!c.World.EmoteHeld,"first timer clears the hold");
        // Speed state factors (0x14EBF88 = Speed, 0x14EC00C = Brave).
        c.World.SeedProtocolFollowupTest(3225);
        var w=c.World;
        typeof(WorldSession).GetProperty("Actor")!.SetValue(w,w.Actor! with{Speed=1});t.Equal(1875,c.EmoteThrottleMs(),"haste ×3/4");
        typeof(WorldSession).GetProperty("Actor")!.SetValue(w,w.Actor! with{Speed=2});t.Equal(5000,c.EmoteThrottleMs(),"slow ×2");
        typeof(WorldSession).GetProperty("Actor")!.SetValue(w,w.Actor! with{Speed=1,Brave=1});t.Equal(1406,c.EmoteThrottleMs(),"haste + brave ×3/4 ×3/4");
        typeof(WorldSession).GetProperty("Actor")!.SetValue(w,w.Actor! with{Speed=0,Brave=5});t.Equal(937,c.EmoteThrottleMs(),"brave 5 ×3/8");
        // Haste shortens the actual gate.
        var sent2=new List<byte[]>();var h=Seed(sent2,gfx:3225);long hnow=50000;h.ActionClock=()=>hnow;var hs=h.CaptureTitleEmote();
        typeof(WorldSession).GetProperty("Actor")!.SetValue(h.World,h.World.Actor! with{Speed=1});
        Wait(h.SendEmote(hs,1));hnow+=1876;
        t.Check(Wait(h.SendEmote(hs,2))&&sent2.Count==2,"haste: second throttled emote after 1876 ms",new{sent2.Count});
    }

    // R5-D: S241 has no engine time limit any more.
    static void MoveLock(WorldModuleTestContext t){
        t.Check(typeof(WorldSession).GetField("MoveLockTimeoutMs")==null&&typeof(WorldSession).GetField("MoveLockClock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)==null,"5-second limit removed");
        var sent=new List<byte[]>();var c=Seed(sent);
        c.World.Receive(LoginPackets.Pad([241,0]),"offline");
        t.Check(c.World.ServerMoveLocked&&c.World.MoveLocked&&!c.MovementAvailable,"S241 locks");
        c.World.Receive(LoginPackets.Pad([241,0]),"offline");t.Check(c.World.ServerMoveLocked,"repeated S241 keeps the lock");
        c.World.Reset();t.Check(!c.World.ServerMoveLocked,"Reset releases");
    }

    // sub_E08A20 0xE08AF2–0xE08B20: class 2 (elf, FU6§2) and level ≥ 50 and byte_13A931C → destination window, no C6.
    static void ElfTeleport(WorldModuleTestContext t){
        t.Check(ElfBookmarkTeleportRules.Allows(2,50,true),"elf Lv 50 with option");
        t.Check(!ElfBookmarkTeleportRules.Allows(0,50,true),"royal (class 0) is not this branch");
        t.Check(!ElfBookmarkTeleportRules.Allows(2,49,true),"Lv 49 not allowed (0xE08B0E cmp 0x32)");
        t.Check(!ElfBookmarkTeleportRules.Allows(2,50,false),"option off");
        t.Check(!ElfBookmarkTeleportRules.Allows(null,50,true)&&!ElfBookmarkTeleportRules.Allows(2,null,true),"unknown class or level");
        {
            var sent=new List<byte[]>();var c=Seed(sent,map:5);c.World.LearnForProtocolTest(5);c.AddCharacterForProtocolTest(Name,2);
            t.Check(!c.TryOpenRingWindowForProtocolTest()&&c.World.TeleportDestination==null,"option off by default: no window");
            c.World.ElfBookmarkTeleportOption=true;
            t.Check(c.TryOpenRingWindowForProtocolTest()&&c.World.TeleportDestination is {Skill:5},"elf Lv 50 + option: destination window, no map check (0xE08B22)");
            long rev=c.World.TeleportDestination!.Revision;
            t.Check(c.TryOpenRingWindowForProtocolTest()&&c.World.TeleportDestination!.Revision==rev,"open window keeps its request");
            t.Check(sent.Count==0,"opening the window sends nothing");
        }
        {
            var sent=new List<byte[]>();var c=Seed(sent);c.World.LearnForProtocolTest(5);c.AddCharacterForProtocolTest(Name,0);c.World.ElfBookmarkTeleportOption=true;
            t.Check(!c.TryOpenRingWindowForProtocolTest(),"royal with option: normal cast path");
        }
        {
            var sent=new List<byte[]>();var c=Seed(sent,level:49);c.World.LearnForProtocolTest(5);c.AddCharacterForProtocolTest(Name,2);c.World.ElfBookmarkTeleportOption=true;
            t.Check(!c.TryOpenRingWindowForProtocolTest(),"elf Lv 49: normal cast path");
        }
        {
            var sent=new List<byte[]>();var c=Seed(sent);c.World.LearnForProtocolTest(1);c.AddCharacterForProtocolTest(Name,2);c.World.ElfBookmarkTeleportOption=true;
            t.Check(!c.TryOpenRingWindowForProtocolTest(),"non-teleport skill unaffected");
        }
    }

    // summonList.xml desc → desc-c.tbl line "name^(charisma table)".
    static void SummonNames(WorldModuleTestContext t){
        var m=new SummonMonster(1,28,"7",13683,4961,8);
        string Resolve(string s)=>s=="$13683"?"測試怪^(魅力 8/1, 10/2)":s=="$13684"?"無說明怪":s;
        t.Check(SummonFallback.Describe(m,Resolve) is {Name:"測試怪",Detail:"(魅力 8/1, 10/2)"},"name and charisma split at ^");
        t.Equal("測試怪 · 需等級 28 · (魅力 8/1, 10/2)",SummonFallback.Caption(m,Resolve),"caption with name");
        var plain=m with{Desc=13684};
        t.Equal("無說明怪 · 需等級 28 · minCha 8",SummonFallback.Caption(plain,Resolve),"name without ^ keeps minCha");
        var unknown=m with{Desc=99999};
        t.Equal(SummonFallback.Caption(unknown),SummonFallback.Caption(unknown,Resolve),"unresolved desc falls back to the summon number");
        t.Check(SummonFallback.Describe(m with{Desc=0},Resolve)==null&&SummonFallback.Describe(m,null)==null,"desc 0 or no table → no name");
        t.Check(SummonFallback.Describe(m,_=>"^x")==null,"empty name rejected");
    }
}
