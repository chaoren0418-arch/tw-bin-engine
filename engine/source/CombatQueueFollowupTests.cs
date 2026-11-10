using System.Reflection;
using System.Text.Json;
namespace P06;

// T340／R7-A（#803）離線測試：近戰與箭矢 S30 在施法者忙碌時排隊（SV§2.1、sub_A3C5A0 → sub_A40250）、
// SkillLight 的 kind 2 只在 S30 當 Lightning（#735 第 2 點）、SV#3 選目標模式（sub_E2AB60 → sub_F691C0(…, 15, 179)）。
// 由 --module-tests 執行（名稱 combat-queue-followup）。只用合成封包、記憶體 sender 與合成時鐘；
// 不開遊戲、網路或 DB，不讀客戶端素材與 engine/assets。
static class CombatQueueFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const uint Own=123,Npc=456;
    static void Set(object o,string name,object? value){
        var t=o.GetType();
        if(t.GetProperty(name,F) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
        (t.GetField(name,F)??t.GetField($"<{name}>k__BackingField",F))!.SetValue(o,value);
    }
    static object? Get(object o,string name)=>o.GetType().GetField(name,F)!.GetValue(o);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    // 近戰 S30（24 bytes）：C action、D attacker、D target、H damage、C heading、H x、H y（3.81 送 0）、C flags。
    static byte[] Melee(uint attacker,uint target,ushort damage=5,byte heading=2)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)1);b.Write(attacker);b.Write(target);b.Write(damage);b.Write(heading);
        b.Write((ushort)0);b.Write((ushort)0);b.Write((byte)0);
    });
    // 箭矢 S30（40 bytes）：Flight＝127、sequence 338。
    static byte[] Arrow(uint attacker,uint target,ushort gfx=66)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)1);b.Write(attacker);b.Write(target);b.Write((ushort)4);
        b.Write((byte)2);b.Write(338u);b.Write(gfx);b.Write((byte)127);
        b.Write((ushort)32603);b.Write((ushort)32942);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(0u);b.Write((byte)0);
    });
    static byte[] Magic(ushort gfx,uint caster)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)18);b.Write(caster);b.Write(Own);b.Write((ushort)7);
        b.Write((byte)3);b.Write(338u);b.Write(gfx);b.Write((byte)0);
        b.Write((ushort)32603);b.Write((ushort)32942);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(0u);b.Write((byte)0);
    });
    static System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject> Objects(WorldSession w)=>(System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject>)Get(w,"objects")!;
    static WorldObject Obj(uint id,int x,int y)=>JsonSerializer.Deserialize<WorldObject>($"{{\"ObjectId\":{id},\"Gfx\":65000,\"X\":{x},\"Y\":{y},\"HpByte\":75,\"Brave\":1}}")!;
    static WorldSession Seed(){
        var w=new WorldSession();Set(w,"MapId",(ushort)0);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{Own},\"Hp\":100,\"MaxHp\":200,\"Mp\":80,\"MaxMp\":100}}")!);
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>($"{{\"ObjectId\":{Own},\"Name\":\"offline\",\"Gfx\":65000,\"X\":32600,\"Y\":32940,\"HpPercent\":50,\"Brave\":1}}")!);
        Objects(w)[Npc]=Obj(Npc,32603,32942);
        return w;
    }
    static CombatAnimation Busy(int action,long at,int ms,bool predicted=false)=>new(action,at,predicted){Timing=new ActionCadence(65000,0,action,ms,ms,ms,null,0,0,false)};
    static void Busy(WorldSession w,uint id,CombatAnimation a){if(id==Own)Set(w,"OwnCombat",a);else Objects(w)[id]=Objects(w)[id] with{Combat=a};}
    static T Run<T>(Task<T> t)=>t.GetAwaiter().GetResult();
    // 選中目標之後的一般施法流程會讀動作節奏素材（CI 沒有 engine/assets），這裡只看選目標本身的狀態，不看送出結果。
    static bool Try(Task<bool> t){try{return t.GetAwaiter().GetResult();}catch(Exception){return false;}}

    [WorldModuleTest("combat-queue-followup")]
    static void Run(WorldModuleTestContext t){MeleeQueue(t);ArrowQueue(t);PredictedNotQueued(t);LightClass(t);TargetPick(t);PointerPick(t);}

    // 1. 近戰 S30：施法者忙碌時整包排隊（轉向、動作、目標受擊、回報都延後），動作結束才套用。
    static void MeleeQueue(WorldModuleTestContext t){
        long clock=1000;var w=Seed();w.ActionClock=()=>clock;
        Busy(w,Npc,Busy(1,1000,600));
        var before=w.Objects.Single(o=>o.ObjectId==Npc);
        w.Receive(Melee(Npc,Own),"offline");
        t.Check(w.QueuedSkillCasts is [{Kind:"melee",Caster:Npc}],"melee S30 queued while the attacker acts",w.QueuedSkillCasts);
        t.Check(w.AttackReports==0&&w.LastAttack==null,"no attack report before the queued packet runs");
        t.Check(w.OwnCombat==null,"target hit reaction waits too");
        t.Equal(before.Direction,w.Objects.Single(o=>o.ObjectId==Npc).Direction,"attacker heading unchanged while queued");
        t.Check(ReferenceEquals(before.Combat,w.Objects.Single(o=>o.ObjectId==Npc).Combat),"current action not interrupted");
        t.Equal(0,w.RunQueuedSkillCasts(1599),"still queued one ms before the action ends");
        clock=1600;t.Equal(1,w.RunQueuedSkillCasts(clock),"applied when the action ends");
        t.Check(w.AttackReports==1&&w.LastAttack is {Attacker:Npc,Target:Own,DamageWord:5},"attack report applied",w.LastAttack);
        t.Check(w.LastIncomingAttack?.Number==1,"incoming attack recorded");
        var npc=w.Objects.Single(o=>o.ObjectId==Npc);
        t.Check(npc.Direction==2&&npc.Combat is {Action:1}&&npc.Combat.At==1600,"attacker turns and acts from the release time",npc.Combat);
        t.Check(w.OwnCombat is {Action:2},"target hit reaction applied",w.OwnCombat);
        t.Equal(0,w.QueuedSkillCasts.Length,"queue empty");

        // 不忙碌：立即套用（行為不變）。
        var w2=Seed();w2.ActionClock=()=>clock;w2.Receive(Melee(Npc,Own),"offline");
        t.Check(w2.AttackReports==1&&w2.QueuedSkillCasts.Length==0,"idle attacker: applied at once");
        // 長度不明的動作（受擊 2，沒有 Timing）不擋。
        var w3=Seed();w3.ActionClock=()=>clock;Busy(w3,Npc,new CombatAnimation(2,clock,false));w3.Receive(Melee(Npc,Own),"offline");
        t.Check(w3.AttackReports==1&&w3.QueuedSkillCasts.Length==0,"unknown-length action does not queue");

        // 同一施法者：法術 S30 與之後的近戰 S30 依收包順序排隊；其他攻擊者不受影響。
        clock=5000;var w4=Seed();w4.ActionClock=()=>clock;Objects(w4)[457]=Obj(457,32604,32942);
        Busy(w4,Npc,Busy(18,5000,400));
        w4.Receive(Magic(40200,Npc),"offline");w4.Receive(Melee(Npc,Own),"offline");
        t.Check(w4.QueuedSkillCasts.Select(q=>q.Kind).SequenceEqual(["magic","melee"]),"magic then melee kept in receive order",w4.QueuedSkillCasts);
        w4.Receive(Melee(457,Own),"offline");
        t.Check(w4.LastAttack is {Attacker:457},"other attacker is not held back",w4.LastAttack);
        clock=5400;t.Equal(2,w4.RunQueuedSkillCasts(clock),"both run after the action");
        t.Check(w4.MagicEffects.Any(e=>e.Caster==Npc&&e.Gfx==40200)&&w4.LastAttack is {Attacker:Npc},"magic and melee applied in order",new{w4.LastAttack,w4.MagicEffects});
        // 快照帶 kind。
        var w5=Seed();w5.ActionClock=()=>clock;Busy(w5,Npc,Busy(1,clock,600));w5.Receive(Melee(Npc,Own),"offline");
        var snap=JsonSerializer.SerializeToElement(typeof(WorldSession).GetMethod("SkillCastQueueSnapshot",F)!.Invoke(w5,null));
        t.Equal("melee",snap.GetProperty("queued")[0].GetProperty("kind").GetString(),"snapshot shows the queued kind");
        // 施法者離開畫面：立即套用；重置清空。
        Objects(w5).TryRemove(Npc,out _);t.Equal(1,w5.RunQueuedSkillCasts(clock+1),"attacker gone: released at once");
        var w6=Seed();w6.ActionClock=()=>clock;Busy(w6,Npc,Busy(1,clock,600));w6.Receive(Melee(Npc,Own),"offline");
        w6.Reset();t.Check(w6.QueuedSkillCasts.Length==0,"reset clears the queued melee");
    }

    // 2. 箭矢 S30：同樣排隊，飛行物在動作結束時才建立。
    static void ArrowQueue(WorldModuleTestContext t){
        long clock=2000;var w=Seed();w.ActionClock=()=>clock;
        Busy(w,Npc,Busy(1,2000,500));
        w.Receive(Arrow(Npc,Own),"offline");
        t.Check(w.QueuedSkillCasts is [{Kind:"arrow",Gfx:66}],"arrow S30 queued",w.QueuedSkillCasts);
        t.Check(w.LastProjectile==null&&w.Projectiles.Length==0&&w.AttackReports==0,"no projectile while queued");
        clock=2500;t.Equal(1,w.RunQueuedSkillCasts(clock),"arrow applied when the action ends");
        t.Check(w.LastProjectile is {Attacker:Npc,Target:Own,Gfx:66}&&w.LastProjectile.At==2500,"projectile built at release time",w.LastProjectile);
        t.Check(w.MagicEffects.Length==0,"arrow never becomes a magic effect");
        var w2=Seed();w2.ActionClock=()=>clock;w2.Receive(Arrow(Npc,Own),"offline");
        t.Check(w2.LastProjectile!=null&&w2.QueuedSkillCasts.Length==0,"idle archer: applied at once");
    }

    // 3. 自己角色的近戰／箭矢不排隊（本地節流＋預測動作；引擎取捨）；自己的法術 S30 照 #735 排隊。
    static void PredictedNotQueued(WorldModuleTestContext t){
        long clock=3000;var w=Seed();w.ActionClock=()=>clock;
        Busy(w,Own,Busy(1,3000,600,predicted:true));
        w.Receive(Melee(Own,Npc),"offline");
        t.Check(w.QueuedSkillCasts.Length==0&&w.AttackReports==1&&w.LastAttack is {Attacker:Own},"own predicted attack: confirmation applied at once");
        var w2=Seed();w2.ActionClock=()=>clock;Busy(w2,Own,Busy(1,3000,600));
        w2.Receive(Melee(Own,Npc),"offline");w2.Receive(Melee(Own,Npc),"offline");
        t.Check(w2.QueuedSkillCasts.Length==0&&w2.AttackReports==2,"own melee S30 never queues, even while acting",w2.QueuedSkillCasts);
        var w3=Seed();w3.ActionClock=()=>clock;Busy(w3,Own,Busy(18,3000,1300));
        w3.Receive(Arrow(Own,Npc),"offline");
        t.Check(w3.QueuedSkillCasts.Length==0&&w3.LastProjectile is {Attacker:Own},"own arrow S30 after own magic: applied at once");
        var w4=Seed();w4.ActionClock=()=>clock;Busy(w4,Own,Busy(1,3000,600));
        w4.Receive(Magic(40200,Own),"offline");
        t.Check(w4.QueuedSkillCasts is [{Kind:"magic",Caster:Own}],"own magic S30 still queues (#735)",w4.QueuedSkillCasts);
    }

    // 4. SkillLight：S42（range）不建 Lightning，kind 2 只在 S30 當 Lightning。
    static void LightClass(WorldModuleTestContext t){
        t.Equal(P06.LightClass.Lightning,SkillLight.Class(2,false),"S30 kind 2 = Lightning");
        t.Equal(P06.LightClass.Effect,SkillLight.Class(2,true),"S42 kind 2 = default Effect");
        var two=EffectSprite.Parse(41002,"#41002 1 bolt\n0.effect(0 3,0.0:2 0.1:2 0.2:2)\n109.effect(2 -1)\n");
        t.Equal((long?)null,SkillLight.BeginAfter(two,SkillLight.Class(2,true),0),"S42 type 0 kind 2: never lights");
        t.Equal((long?)0,SkillLight.BeginAfter(two,SkillLight.Class(2,true),8),"S42 type 8 kind 2: lights from the start");
        t.Equal((long?)83,SkillLight.BeginAfter(two,SkillLight.Class(2,false),0),"S30 kind 2: lights from index 2 regardless of +9");
    }

    static LoginConnection Caster(out List<byte[]> sent,params int[] skills){
        var c=new LoginConnection();sent=c.SeedPointerFlow(Own,32600,32940);
        var learned=(HashSet<int>)typeof(WorldSession).GetField("learnedMagic",F)!.GetValue(c.World)!;foreach(int s in skills)learned.Add(s);
        Objects(c.World)[Npc]=Obj(Npc,32605,32942);Objects(c.World)[457]=Obj(457,32620,32940);
        return c;
    }

    // 5. SV#3：沒有目標時進選目標模式（15 格、游標 179 推測），不送 C6。
    static void TargetPick(WorldModuleTestContext t){
        t.Check(SpellTargeting.Range==15&&SpellTargeting.Cursor==179,"sub_F691C0(…, 15, 179)");
        t.Check(SpellTargeting.Within(0,0,15,-15)&&!SpellTargeting.Within(0,0,16,0),"range is 15 tiles (Chebyshev)");
        using var c=Caster(out var sent,4,5);var w=c.World;
        // 沒有技能目錄（CI）時技能 4 是 buff：改成「對他人」，讓它需要目標。
        t.Check(w.SelectMagic(4)&&w.TargetId==null,"skill 4 selected, no target");w.UseSelectedMagicTarget(false);
        t.Check(!Run(c.CastSelectedMagic())&&sent.Count==0,"no target: nothing sent");
        t.Check(w.SpellTargetPicking&&w.SpellTargetPick is {Skill:4,Range:15,Cursor:179},"pick mode entered",w.SpellTargetPick);
        t.Equal(SpellTargeting.PickNotice,w.MagicNotice,"pick notice");
        // 換技能：選目標模式失效。
        w.SelectMagic(5);t.Check(!w.SpellTargetPicking,"another skill ends the pick");
        // 書籤（HD）不進選目標模式。
        t.Check(!Run(c.CastSelectedMagic())&&!w.SpellTargetPicking&&sent.Count==0,"bookmark skill never enters pick mode");
        // 有選取目標：沿用（不進選目標模式）。
        w.SelectMagic(4);w.SelectTarget(Npc);
        t.Check(!c.NeedsSpellTargetPick(),"selected target is reused");
        w.SelectTarget(null);t.Check(c.NeedsSpellTargetPick(),"no target needs a pick");
        // 點選：超出 15 格不接受、不存在的物件不接受，都留在選目標模式。
        Run(c.CastSelectedMagic());
        t.Check(!Run(c.PickSpellTarget(457))&&w.SpellTargetPicking&&w.TargetId==null&&sent.Count==0,"20 tiles away: refused, still picking");
        t.Check(w.MagicNotice.Contains("15 格"),"range refusal notice",w.MagicNotice);
        t.Check(!Run(c.PickSpellTarget(999))&&w.SpellTargetPicking,"unknown object refused");
        // 15 格內：選取並結束選目標模式，走一般施法流程。
        Try(c.PickSpellTarget(Npc));
        t.Check(w.TargetId==Npc&&!w.SpellTargetPicking,"pick selects the target and ends pick mode",new{w.TargetId,w.SpellTargetPick});
        // 世界重置清掉選目標模式。
        w.SelectTarget(null);Run(c.CastSelectedMagic());t.Check(w.SpellTargetPicking,"picking again");
        w.Reset();t.Check(w.SpellTargetPick==null,"reset clears pick mode");
    }

    // 6. PlayerPointer：選目標模式中左鍵物件＝選施法目標（不攻擊）；點地面＝取消、不走路。
    static void PointerPick(WorldModuleTestContext t){
        using var c=Caster(out var sent,4);var w=c.World;var p=new PlayerPointerController(c,()=>true,()=>false,_=>true);
        w.SelectMagic(4);w.UseSelectedMagicTarget(false);Run(c.CastSelectedMagic());t.Check(w.SpellTargetPicking,"pick mode");
        t.Check(!p.MoveTo(new Point(32601,32940))&&!w.SpellTargetPicking&&p.RemainingSteps==0,"ground click cancels the pick, no walking");
        t.Check(sent.Count==0,"ground click sends nothing");
        Run(c.CastSelectedMagic());
        t.Check(p.Select(Npc)&&w.TargetId==Npc&&!w.SpellTargetPicking,"left click on an object picks it",new{w.TargetId,w.SpellTargetPick});
        t.Check(p.LastSpellPick!=null,"pick runs the cast flow");
        t.Check(!sent.Any(b=>b.Length>0&&b[0] is 229 or 123),"no attack (C229／C123) sent while picking");
        // 不在選目標模式：Select 照舊只選取。
        w.SelectTarget(null);var last=p.LastSpellPick;p.Select(Npc);t.Check(!w.SpellTargetPicking&&ReferenceEquals(last,p.LastSpellPick),"outside pick mode Select does not start a cast");
        // ActAsync（攻擊路徑）在選目標模式中改為選施法目標。
        w.SelectTarget(null);Run(c.CastSelectedMagic());t.Check(w.SpellTargetPicking,"pick mode again");
        Try(p.ActAsync(Npc,false));
        t.Check(w.TargetId==Npc&&!w.SpellTargetPicking&&!sent.Any(b=>b.Length>0&&b[0] is 229 or 123),"attack click while picking selects the spell target, no C229",sent.Select(Convert.ToHexString));
    }
}
