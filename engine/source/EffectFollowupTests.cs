using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
namespace P06;

// T308／R5-C（#735）離線測試：施法者忙碌時整包排隊（sub_A40250）、S42 kind 2 的 `]`（查證結果：照常處理）、
// `.s32` 地圖特效的 `]`、S_GlobalEffect(3) 煙火提亮。由 --module-tests 執行（名稱 effect-followup）。
// 只用合成封包、合成 list.spr 條目（直接放進 EffectSpriteCatalog 快取）、合成地圖特效 JSON 與合成時鐘；
// 不讀原客戶端、不讀 engine/assets、不開遊戲、網路或 DB。
static class EffectFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const int Release=500,Frame=83;
    static void Set(object o,string name,object? value){
        var t=o.GetType();
        if(t.GetProperty(name,F) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
        (t.GetField(name,F)??t.GetField($"<{name}>k__BackingField",F))!.SetValue(o,value);
    }
    static object? Get(object o,string name)=>o.GetType().GetField(name,F)!.GetValue(o);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static byte[] Attack(ushort gfx,uint caster=123,uint target=456,ushort damage=7,byte heading=3)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)18);b.Write(caster);b.Write(target);b.Write(damage);
        b.Write(heading);b.Write(338u);b.Write(gfx);b.Write((byte)0);
        b.Write((ushort)32600);b.Write((ushort)32940);b.Write((ushort)32603);b.Write((ushort)32942);b.Write(0u);b.Write((byte)0);
    });
    static byte[] Range(ushort gfx,byte type,uint caster,params uint[] targets)=>Packet(b=>{
        b.Write((byte)42);b.Write((byte)19);b.Write(caster);b.Write((ushort)32600);b.Write((ushort)32940);
        b.Write((byte)6);b.Write(9000001u);b.Write(gfx);b.Write(type);b.Write((ushort)0);b.Write((ushort)targets.Length);
        foreach(uint id in targets){b.Write(id);b.Write((ushort)32);}
    });
    // 3.81 S_GlobalEffect(trigger)：C(250) C(83) C(trigger)。
    static byte[] Global(byte trigger)=>LoginPackets.Pad([250,83,trigger]);
    static System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject> Objects(WorldSession w)=>(System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject>)Get(w,"objects")!;
    static WorldSession Seed(){
        var w=new WorldSession();Set(w,"MapId",(ushort)0);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":200,\"Mp\":80,\"MaxMp\":100}")!);
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"Name\":\"offline\",\"Gfx\":65000,\"X\":32600,\"Y\":32940,\"HpPercent\":50,\"Brave\":1}")!);
        foreach(uint id in new uint[]{456,457})Objects(w)[id]=JsonSerializer.Deserialize<WorldObject>($"{{\"ObjectId\":{id},\"Gfx\":65000,\"X\":{32603+id-456},\"Y\":32942,\"HpByte\":75,\"Brave\":1}}")!;
        // Night: L = 1 at 00:00, so the +5 boost is visible (6).
        w.Ambience.TimeReceived(0,0);
        return w;
    }
    static void CasterTiming(WorldSession w){
        var list=(List<MagicEffect>)Get(w,"magicEffects")!;
        for(int i=0;i<list.Count;i++)list[i]=list[i] with{Timing=new ActionCadence(w.Actor!.Gfx,w.Actor.Weapon,list[i].Action,1000,1000,1000,Release,0,0,false),ActionAt=list[i].At};
    }
    // A caster action with a known length (the only kind that makes the caster "busy").
    static CombatAnimation Busy(int action,long at,int ms)=>new(action,at,false){Timing=new ActionCadence(65000,0,action,ms,ms,ms,null,0,0,false)};
    static void Busy(WorldSession w,uint id,CombatAnimation a){
        if(id==123)Set(w,"OwnCombat",a);else Objects(w)[id]=Objects(w)[id] with{Combat=a};
    }

    // Synthetic list.spr rows, non-directional, 2-tick frames (83 ms each @24 fps).
    static readonly Dictionary<int,string> Rows=new(){
        [40200]="#40200 1 effect\n0.effect(0 2,0.0:2 0.1:2)\n",
        [40201]="#40201 1 effect2\n0.effect(0 2,0.0:2 0.1:2)\n",
        [40100]="#40100 1 child\n0.effect(0 2,0.0:2 0.1:2)\n",
        // kind 2 with a `]` on the first displayed frame (index 1).
        [41012]="#41012 1 bolt\n0.effect(0 3,0.0:2 0.1:2]40100 0.2:2)\n109.effect(2 -1)\n",
        // A firework gfx whose list.spr row says "flying" (kind 5): S250/83 ignores 109 and builds the default Effect.
        [2042]="#2042 1 cracker\n0.effect(0 2,0.0:2 0.1:2)\n109.effect(5 -1)\n",
    };
    static Renderer Bare(){
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);
        foreach(string name in new[]{"magicFlights","projectileFlights","projectileHits","effectResourceErrors","magicAppearances","projectileFrames"}){
            var f=typeof(Renderer).GetField(name,F)!;f.SetValue(r,Activator.CreateInstance(f.FieldType));
        }
        var catalog=new EffectSpriteCatalog(Path.Combine(AppContext.BaseDirectory,"effect-followup-no-client"));
        var effects=(Dictionary<int,EffectSprite>)Get(catalog,"effects")!;var sprites=(Dictionary<(int,int,int),Frame[]>)Get(catalog,"sprites")!;
        foreach(var (gfx,row) in Rows){
            var e=EffectSprite.Parse(gfx,row);effects[gfx]=e;
            sprites[(e.Sprite,0,e.TileType)]=Enumerable.Range(0,e.Steps.Max(s=>s.Frame)+1).Select(i=>new Frame($"0:{i}",[0,0,0,0],[new Block(0,0,10,0,new(e.TileType,[]))])).ToArray();
        }
        Set(r,"effectSprites",catalog);Set(r,"magicEpoch",-1);Set(r,"projectileEpoch",-1);
        return r;
    }
    static List<Draw> Compose(Renderer r,WorldSession w,long now){
        var draws=new List<Draw>();
        foreach(string m in new[]{"ComposeProjectile","ComposeMagic"})typeof(Renderer).GetMethod(m,F)!.Invoke(r,[w,draws,now]);
        return draws;
    }
    static byte Level(WorldSession w)=>w.Ambience.At(0).Level;

    [WorldModuleTest("effect-followup")]
    static void Run(WorldModuleTestContext t){
        Registration(t);Queue(t);QueueLimits(t);LightningSubEffects(t);MapEffectSubEffects(t);Fireworks(t);
    }

    static void Registration(WorldModuleTestContext t){
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0,"default registry has no errors",reg.Errors);
        t.Check(reg.Entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetSkillCastQueue"),"queue reset registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="snapshot"&&e.Key=="skillCastQueue"),"queue snapshot registered");
        t.Check(SkillCastQueue.BusyUntil(null,0)==null,"no action = not busy");
        t.Check(SkillCastQueue.BusyUntil(new CombatAnimation(18,0,false),10)==null,"unknown length = not busy");
        t.Equal((long?)600,SkillCastQueue.BusyUntil(Busy(18,0,600),599),"busy until At + AnimationMs");
        t.Check(SkillCastQueue.BusyUntil(Busy(18,0,600),600)==null,"finished at At + AnimationMs");
        t.Equal(SkillLight.FireworkFlag,SkillLight.Flag(new MagicEffect(1,0,0,2042,0,0,0,0,0,0,0,0),true),"firework +9 = 1");
        t.Equal((byte)0,SkillLight.Flag(new MagicEffect(1,0,0,2042,0,0,0,0,0,0,0,0),false),"plain location effect +9 = 0");
    }

    // SV§2.1：施法者忙碌（sub_A3EBF0）時整包排進佇列（sub_A40250），等目前動作結束才轉向、播動作、建特效。
    static void Queue(WorldModuleTestContext t){
        long clock=1000;
        var w=Seed();w.ActionClock=()=>clock;var r=Bare();
        Busy(w,123,Busy(1,1000,600));
        w.Receive(Attack(40200),"offline");
        t.Equal(0,w.MagicEffects.Length,"S30 while the caster acts: no effect yet");
        t.Equal(1,w.QueuedSkillCasts.Length,"S30 queued");
        t.Equal(1,w.OwnCombat?.Action??-1,"the current action is not interrupted");
        clock=1599;Compose(r,w,clock);
        t.Equal(0,w.MagicEffects.Length,"still queued one ms before the action ends");
        clock=1600;Compose(r,w,clock);
        t.Check(w.MagicEffects is [{Gfx:40200,At:1600}],"applied by ComposeMagic when the action ends, timed from then",w.MagicEffects);
        t.Equal(18,w.OwnCombat?.Action??-1,"the queued caster action starts");
        t.Equal(0,w.QueuedSkillCasts.Length,"queue empty");

        // Not busy: applied at once (unchanged behavior).
        clock=5000;w.Receive(Attack(40201),"offline");
        t.Check(w.MagicEffects.Any(e=>e.Gfx==40201&&e.At==5000)&&w.QueuedSkillCasts.Length==0,"idle caster: applied immediately",w.MagicEffects);

        // S42 from another caster: the whole packet (target list) waits; receive order is kept.
        clock=10000;var w2=Seed();w2.ActionClock=()=>clock;
        Busy(w2,456,Busy(1,10000,400));
        w2.Receive(Range(40200,0,456,123,457),"offline");w2.Receive(Attack(40201,caster:456,target:123),"offline");
        t.Equal(2,w2.QueuedSkillCasts.Length,"S42 and the following S30 of the same caster both queued");
        t.Check(w2.QueuedSkillCasts.Select(q=>q.Gfx).SequenceEqual(new ushort[]{40200,40201}),"queue keeps receive order");
        // A packet of another (idle) caster is not held back.
        w2.Receive(Attack(40200,caster:123,target:456),"offline");
        t.Check(w2.MagicEffects is [{Caster:123}],"other caster is not queued",w2.MagicEffects);
        clock=10400;t.Equal(2,w2.RunQueuedSkillCasts(clock),"both run once the action ends (new actions have no known length)");
        var applied=w2.MagicEffects.Where(e=>e.Caster==456).ToArray();
        t.Check(applied.Select(e=>e.Gfx).SequenceEqual([40200,40201]),"applied in receive order",applied);
        t.Equal(2,w2.RangeTargets(applied[0].Number).Count,"S42 target list kept with the queued packet");
        t.Equal(2,w2.SkillCastsQueued,"queued total");

        // Caster 0 (withCastMotion=false) never queues.
        var w3=Seed();w3.ActionClock=()=>clock;Busy(w3,123,Busy(1,clock,600));
        w3.Receive(Attack(40200,caster:0),"offline");
        t.Check(w3.MagicEffects.Length==1&&w3.QueuedSkillCasts.Length==0,"caster 0 is never queued");
        // Reset clears the queue.
        w3.Receive(Attack(40200),"offline");t.Equal(1,w3.QueuedSkillCasts.Length,"queued before reset");
        w3.Reset();t.Check(w3.QueuedSkillCasts.Length==0&&w3.SkillCastsQueued==0,"reset clears the queue");
    }

    static void QueueLimits(WorldModuleTestContext t){
        long clock=20000;
        // A wrong / very long length never holds the packet more than MaxWaitMs.
        var w=Seed();w.ActionClock=()=>clock;Busy(w,456,Busy(1,clock,60000));
        w.Receive(Attack(40200,caster:456,target:123),"offline");
        t.Equal(0,w.RunQueuedSkillCasts(clock+SkillCastQueue.MaxWaitMs-1),"held before MaxWaitMs");
        t.Equal(1,w.RunQueuedSkillCasts(clock+SkillCastQueue.MaxWaitMs),"released at MaxWaitMs");
        // The caster leaves the screen: the effect starts at once (§2.1).
        var w2=Seed();w2.ActionClock=()=>clock;Busy(w2,456,Busy(1,clock,600));
        w2.Receive(Attack(40200,caster:456,target:123),"offline");
        Objects(w2).TryRemove(456,out _);
        t.Equal(1,w2.RunQueuedSkillCasts(clock+1),"caster gone: released at once");
        // A full queue releases its oldest entry instead of growing.
        var w3=Seed();w3.ActionClock=()=>clock;Busy(w3,456,Busy(1,clock,600));
        for(int i=0;i<=SkillCastQueue.MaxQueued;i++)w3.Receive(Attack(40200,caster:456,target:123),"offline");
        t.Check(w3.QueuedSkillCasts.Length==SkillCastQueue.MaxQueued&&w3.MagicEffects.Length==1,"queue capped at MaxQueued",new{queued=w3.QueuedSkillCasts.Length,effects=w3.MagicEffects.Length});
    }

    // TWP§3.1：S30 kind 2 是 Lightning（不處理 `]`）；S42 不建 Lightning（sub_92FE40 沒有呼叫 sub_8AB900），kind 2 gfx 走預設類別，`]` 照常處理。
    static void LightningSubEffects(WorldModuleTestContext t){
        var w=Seed();w.Receive(Attack(41012),"offline");CasterTiming(w);var r=Bare();long release=w.MagicEffects.Single().At+Release;
        Compose(r,w,release);Compose(r,w,release+Frame);
        t.Check(r.MagicVisuals.Length==1&&r.SubEffects.Live.All(s=>s.Gfx!=40100),"S30 kind 2 (Lightning): drawn, no `]` child",new{r.MagicVisuals,live=r.SubEffects.Live});
        var w2=Seed();w2.Receive(Range(41012,0,123,456),"offline");CasterTiming(w2);var r2=Bare();long rel2=w2.MagicEffects.Single().At+Release;
        Compose(r2,w2,rel2);
        t.Check(r2.SubEffects.Live.Any(s=>s.Gfx==40100&&s.X==32600&&s.Y==32940&&s.At==rel2),"S42 kind 2 gfx (default class): `]` child at packet x,y",r2.SubEffects.Live);
    }

    static MapEffectAppearance MapAppearance(int id,string events){
        string json=$"{{\"id\":{id},\"flags\":0,\"tickRate\":24,\"frames\":[{{\"index\":0,\"bounds\":[0,0,0,0],\"blocks\":[{{\"a\":0,\"b\":0,\"z\":0,\"order\":0,\"pixels\":[[0,0,1]]}}]}}],"
            +$"\"sequence\":[{{\"frame\":0,\"ticks\":2,\"events\":\"\"}},{{\"frame\":0,\"ticks\":2,\"events\":\"{events}\"}}]}}";
        return new(Encoding.UTF8.GetBytes(json));
    }
    // TWP§3.4：`.s32` 地圖特效是 UserObject，待機動畫的 `]` 經 sub_A4B060 在特效位置建預設 Effect（循環時每輪一次）。
    static void MapEffectSubEffects(WorldModuleTestContext t){
        var w=Seed();var r=Bare();var a=MapAppearance(70001,"]40100");
        var timeline=a.Timeline;
        t.Check(timeline.Loop&&timeline.HasTokens&&timeline.Steps.Length==2&&Math.Abs(timeline.Steps[1].Offset-2000d/24)<1e-9,"map effect timeline: loop, ms offsets",timeline.Steps);
        t.Check(ReferenceEquals(new PacketEffectAppearance(a).Timeline,timeline),"S106 map appearance shares the same timeline");
        var fx=new MapLocalEffect(70001,32600,32940,0,a);
        r.ObserveMapEffectFrames(w,"map-effect:test",fx,0,1000);
        t.Equal(0,r.SubEffects.Live.Count,"first frame has no `]`");
        r.ObserveMapEffectFrames(w,"map-effect:test",fx,100,1100);
        t.Check(r.SubEffects.Live is [{Gfx:40100,X:32600,Y:32940,Direction:0,Source:"map-effect:test"} s]&&s.At==1100-16,"`]` frame spawns the child at the map effect",r.SubEffects.Live);
        r.ObserveMapEffectFrames(w,"map-effect:test",fx,200,1200);
        t.Equal(1,r.SubEffects.Live.Count,"each `]` frame fires once");
        r.ObserveMapEffectFrames(w,"map-effect:test",fx,260,1260);
        t.Equal(2,r.SubEffects.Live.Count(x=>x.Gfx==40100),"looping idle animation: `]` again on the next cycle");
        r.ObserveMapEffectFrames(w,"map-effect:far",new MapLocalEffect(70001,1,1,0,a),100,1100);
        t.Check(r.SubEffects.Live.All(x=>x.Source!="map-effect:far"),"outside the scene: not processed");
        r.ObserveMapEffectFrames(w,"map-effect:6016",new MapLocalEffect(6016,32600,32940,0,MapAppearance(6016,"]40100")),100,1100);
        t.Check(r.SubEffects.Live.All(x=>x.Source!="map-effect:6016"),"6016 (state-driven frames) not processed");
        var plain=MapAppearance(70002,"");
        r.ObserveMapEffectFrames(w,"map-effect:plain",new MapLocalEffect(70002,32600,32940,0,plain),100,1100);
        t.Check(!plain.Timeline.HasTokens&&r.SubEffects.Live.All(x=>x.Source!="map-effect:plain"),"no tokens: nothing");
    }

    // FU5§3.2：S250/83 值 3 的煙火是 `+9`＝1 的預設 Effect：存活期間 L＝min(15, L+5)；不看 list.spr 109 kind。
    static void Fireworks(WorldModuleTestContext t){
        var w=Seed();w.PacketBoxScreenClock=()=>1000;w.PacketBoxScreenRandom=_=>0;
        w.Receive(Global(3),"offline");
        var fx=w.MagicEffects;
        t.Check(fx.Length==3&&fx.All(e=>e.Gfx==2042&&w.IsFireworkEffect(e.Number)),"three fireworks (self + 2 objects), all marked",fx);
        var r=Bare();
        Compose(r,w,999);t.Equal((byte)1,Level(w),"not lit before the first firework");
        Compose(r,w,1020);
        t.Equal(3,r.MagicBrighteningEffects,"every firework holds the counter");
        t.Equal((byte)6,Level(w),"Ambience L + 5 while fireworks play");
        t.Check(r.MagicVisuals.Length==3&&r.MagicVisuals.All(v=>v.Phase=="effect"),"drawn in place (default Effect), not flying despite 109 kind 5",r.MagicVisuals);
        t.Check(r.MagicVisuals.Select(v=>(v.X,v.Y)).OrderBy(p=>p.X).SequenceEqual([(32600,32940),(32603,32942),(32604,32942)]),"at the self / object positions",r.MagicVisuals);
        Compose(r,w,1020+2*Frame+1);t.Equal(0,r.MagicBrighteningEffects,"counter back to 0 after the last one finishes");
        t.Equal((byte)1,Level(w),"L restored");
        // The same gfx as an ordinary effect keeps its 109 class and never lights.
        var w2=Seed();var list=(List<MagicEffect>)Get(w2,"magicEffects")!;
        list.Add(new MagicEffect(99,0,456,2042,18,0,3,32600,32940,32603,32942,1000));
        var r2=Bare();Compose(r2,w2,1010);
        t.Check(r2.MagicVisuals.Any(v=>v.Phase=="fly")&&r2.MagicBrighteningEffects==0,"non-firework 2042 flies (kind 5), no light",r2.MagicVisuals);
        // Reset forgets the marks.
        w.Reset();t.Check(!w.IsFireworkEffect(fx[0].Number),"reset clears firework marks");
    }
}
