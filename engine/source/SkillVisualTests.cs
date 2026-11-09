using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06;

// T256／R2-H（#617）離線測試：技能表現收尾。由 --module-tests 執行（名稱 skill-visual）。
// 只用合成封包、合成 list.spr 條目（直接放進 EffectSpriteCatalog 快取）與空白影格；
// 不讀原客戶端、不讀 engine/assets、不開遊戲、網路或 DB。
static class SkillVisualTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const int Release=500;
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
    static byte[] Range(ushort gfx,byte type,params uint[] targets)=>Packet(b=>{
        b.Write((byte)42);b.Write((byte)19);b.Write(123u);b.Write((ushort)32600);b.Write((ushort)32940);
        b.Write((byte)6);b.Write(9000001u);b.Write(gfx);b.Write(type);b.Write((ushort)0);b.Write((ushort)targets.Length);
        foreach(uint id in targets){b.Write(id);b.Write((ushort)32);}
    });
    // Brave=1 keeps CaptureCombat away from player profiles (no engine/assets needed).
    static WorldSession Seed(){
        var w=new WorldSession();Set(w,"MapId",(ushort)0);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":200,\"Mp\":80,\"MaxMp\":100}")!);
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"Name\":\"offline\",\"Gfx\":65000,\"X\":32600,\"Y\":32940,\"HpPercent\":50,\"Brave\":1}")!);
        Place(w,456,32603,32942);
        return w;
    }
    static void Place(WorldSession w,uint id,int x,int y){
        var objects=(System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject>)Get(w,"objects")!;
        objects[id]=JsonSerializer.Deserialize<WorldObject>($"{{\"ObjectId\":{id},\"Gfx\":65000,\"X\":{x},\"Y\":{y},\"HpByte\":75,\"Brave\":1}}")!;
    }
    // Caster action captured with a known `!` release (500 ms after the action start).
    static void CasterTiming(WorldSession w){
        var list=(List<MagicEffect>)Get(w,"magicEffects")!;
        for(int i=0;i<list.Count;i++)list[i]=list[i] with{Timing=new ActionCadence(w.Actor!.Gfx,w.Actor.Weapon,list[i].Action,1000,1000,1000,Release,0,0,false),ActionAt=list[i].At};
    }
    // Synthetic list.spr rows, non-directional, two 2-tick frames (83 ms each @24 fps).
    static readonly Dictionary<int,string> Rows=new(){
        [221]="#221 1 legacy shield\n0.effect(0 2,0.0:2! 0.1:2]40100)\n",          // `]` first displayed, `!` displayed last (+83)
        [40200]="#40200 1 range\n0.effect(0 2,0.0:2 0.1:2)\n",
        [40300]="#40300 1 bolt\n0.effect(0 2,0.0:2]40303 0.1:2)\n109.effect(4 40301)\n", // flying class with a `]`
        [40301]="#40301 1 landing\n0.effect(0 2,0.0:2 0.1:2]40302)\n",
    };
    static Renderer Bare(){
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);
        foreach(string name in new[]{"magicFlights","projectileFlights","projectileHits","effectResourceErrors","magicAppearances","projectileFrames"}){
            var f=typeof(Renderer).GetField(name,F)!;f.SetValue(r,Activator.CreateInstance(f.FieldType));
        }
        var catalog=new EffectSpriteCatalog(Path.Combine(AppContext.BaseDirectory,"skill-visual-no-client"));
        var effects=(Dictionary<int,EffectSprite>)Get(catalog,"effects")!;var sprites=(Dictionary<(int,int,int),Frame[]>)Get(catalog,"sprites")!;
        foreach(var (gfx,row) in Rows){var e=EffectSprite.Parse(gfx,row);effects[gfx]=e;sprites[(e.Sprite,0,e.TileType)]=[Blank("0:0"),Blank("0:1")];}
        Set(r,"effectSprites",catalog);Set(r,"magicEpoch",-1);Set(r,"projectileEpoch",-1);
        ((Dictionary<int,ObjectAppearance>)Get(r,"magicAppearances")!)[221]=LegacyAppearance(221);
        ((Dictionary<int,ObjectAppearance>)Get(r,"magicAppearances")!)[189]=LegacyAppearance(189);
        return r;
    }
    static Frame Blank(string key)=>new(key,[0,0,0,0],[]);
    // Stand-in for objects/magic-<gfx>.json (legacy asset): 8 directions × two 83 ms frames, no events.
    static ObjectAppearance LegacyAppearance(int gfx){
        var a=(ObjectAppearance)RuntimeHelpers.GetUninitializedObject(typeof(ObjectAppearance));
        Set(a,"Gfx",gfx);Set(a,"Sprite",gfx);Set(a,"StepMs",1);Set(a,"ActionMs",new Dictionary<string,double>());
        Set(a,"Frames",new Dictionary<string,Frame>{["0:0"]=Blank("0:0"),["0:1"]=Blank("0:1")});
        var seq=new Dictionary<(string,int),AnimationStep[]>();
        foreach(string action in new[]{"effect","heal"})for(int d=0;d<8;d++)seq[(action,d)]=[new("0:0",2,83),new("0:1",2,83)];
        Set(a,"Sequences",seq);return a;
    }
    static void Compose(Renderer r,WorldSession w,long now){
        var draws=new List<Draw>();
        foreach(string m in new[]{"ComposeProjectile","ComposeMagic"})typeof(Renderer).GetMethod(m,F)!.Invoke(r,[w,draws,now]);
    }
    static Dictionary<uint,CombatAnimation> Hits(Renderer r)=>(Dictionary<uint,CombatAnimation>)Get(r,"projectileHits")!;

    [WorldModuleTest("skill-visual")]
    static void Run(WorldModuleTestContext t){
        Facing(t);Legacy(t);Brighten(t);Landing(t);
    }

    // SV#4：施法者轉向目標（S42 第一個目標）的目前位置；沒有目標物件時用封包朝向。
    static void Facing(WorldModuleTestContext t){
        t.Equal((byte)3,SkillFacing.Toward(32600,32940,((ushort)32603,(ushort)32942),6),"faces the target position (F1A340)");
        t.Equal((byte)6,SkillFacing.Toward(32600,32940,null,6),"no target object keeps the packet heading");
        t.Equal((byte)5,SkillFacing.Toward(32600,32940,((ushort)32600,(ushort)32940),5),"target on the same tile keeps the packet heading");

        var w=Seed();w.Receive(Range(40200,0,456,457),"offline");
        t.Equal((byte)3,w.Actor!.Direction,"S42: caster turns toward the FIRST target's current position (packet heading 6)");
        t.Equal(6,w.MagicEffects.Single().Heading,"S42: the effect keeps the packet heading");
        w=Seed();Place(w,456,32597,32938);w.Receive(Range(40200,0,456),"offline");
        t.Equal((byte)7,w.Actor!.Direction,"S42: current position is used, not a cached one");
        w=Seed();w.Receive(Range(40200,0,999,456),"offline");
        t.Equal((byte)6,w.Actor!.Direction,"S42: unknown first target keeps the packet heading");
        w=Seed();w.Receive(Range(40200,0,123,456),"offline");
        t.Equal((byte)6,w.Actor!.Direction,"S42: caster as first target keeps the packet heading");
        w=Seed();w.Receive(Attack(40200,heading:0),"offline");
        t.Equal((byte)3,w.Actor!.Direction,"S30: own caster turns toward the target (packet heading 0)");
        w=Seed();w.Receive(Attack(40200,caster:456,target:123,heading:0),"offline");
        t.Equal((byte)7,w.Objects.Single(o=>o.ObjectId==456).Direction,"S30: other caster turns toward its target (the player)");
        w=Seed();w.Receive(Attack(40200,target:123,heading:4),"offline");
        t.Equal((byte)4,w.Actor!.Direction,"S30: self-target spell keeps the packet heading");
    }

    // 舊素材路徑（ComposeMagic 寫死的 gfx）改走施法者 `!` 時機；受擊在特效自己的 `!` 格；`]` 照預設 Effect 處理。
    static void Legacy(WorldModuleTestContext t){
        var w=Seed();w.Receive(Attack(221),"offline");var e0=w.MagicEffects.Single();
        t.Check(LegacyMagic.Handles(e0)&&!LegacyMagic.Handles(e0 with{RangeType=0})&&!LegacyMagic.Handles(e0 with{Gfx=40200})&&LegacyMagic.Handles(e0 with{Gfx=167}),"legacy gfx list (S30/S55 only; S42 always native)");
        t.Equal(e0.At,LegacyMagic.Start(e0),"no captured caster action (S55 / caster absent): starts at receipt");
        CasterTiming(w);var e=w.MagicEffects.Single();long release=e.At+Release;
        t.Equal(release,LegacyMagic.Start(e),"captured caster action: starts at the caster `!`");
        t.Equal((long?)null,LegacyMagic.Hit(null,release,release-1),"no hit before the start");
        t.Equal((long?)release,LegacyMagic.Hit(null,release,release+400),"without native metadata the caster `!` hits");

        var r=Bare();
        Compose(r,w,release-1);
        t.Check(r.MagicVisuals.Length==0&&Hits(r).Count==0,"legacy 221 waits for the caster `!` (was: drawn and hit on receipt)",r.MagicVisuals);
        Compose(r,w,release);
        t.Check(r.MagicVisuals is [{Gfx:221,X:32603,Y:32942,Phase:"effect"} v]&&v.At==release,"legacy 221 starts at the caster `!`, at the target",r.MagicVisuals);
        t.Check(Hits(r).Count==0,"no hit before the effect's own `!` frame");
        t.Check(r.SubEffects.Live.Any(s=>s.Gfx==40100&&s.X==32603&&s.Y==32942&&s.At==release),"legacy effect `]` spawns a child at its position",r.SubEffects.Live);
        Compose(r,w,release+83);
        t.Check(Hits(r).TryGetValue(456,out var h)&&h.Action==2&&h.At==release+83,"legacy hit on the effect `!` frame",Hits(r));
        Compose(r,w,release+167);
        t.Equal(0,r.MagicVisuals.Length,"legacy effect expires after one cycle");

        w=Seed();w.Receive(Attack(221,caster:0),"offline");r=Bare();long at0=w.MagicEffects.Single().At;
        Compose(r,w,at0);
        t.Check(r.MagicVisuals is [{Gfx:221} v0]&&v0.At==at0,"absent caster: legacy effect starts at once (§2.1)",r.MagicVisuals);

        // 189 has no runtime metadata here (catalog has no list.spr): drawn from the legacy asset, caster `!` hits.
        w=Seed();w.Receive(Attack(189),"offline");CasterTiming(w);r=Bare();release=w.MagicEffects.Single().At+Release;
        Compose(r,w,release-1);t.Check(Hits(r).Count==0&&r.MagicVisuals.Length==0,"legacy without metadata waits for the caster `!`");
        Compose(r,w,release);
        t.Check(r.MagicVisuals is [{Gfx:189}]&&Hits(r).TryGetValue(456,out var h2)&&h2.At==release,"legacy without metadata: drawn at caster `!`, caster `!` hits",new{r.MagicVisuals,hits=Hits(r)});
        t.Check(r.EffectResourceErrors.ContainsKey(189),"missing runtime metadata is diagnosed, not substituted");

        // Busy / mismatched caster appearance: not started (same gate as the native classes).
        w=Seed();w.Receive(Attack(221),"offline");CasterTiming(w);r=Bare();release=w.MagicEffects.Single().At+Release;
        Set(w,"Actor",w.Actor! with{Gfx=65001});
        Compose(r,w,release+10);t.Equal(0,r.MagicVisuals.Length,"caster appearance changed before `!`: legacy effect not started");
    }

    // SV#12：S42 type≠0 的特效存活期間，全域明暗 min(15, L+5)。
    static void Brighten(WorldModuleTestContext t){
        t.Check(MagicBrighten.Apply(3,1)==8&&MagicBrighten.Apply(12,2)==15&&MagicBrighten.Apply(15,1)==15&&MagicBrighten.Apply(7,0)==7,"min(15, L+5) only while active");
        t.Check(MagicBrighten.LocationEffect(2028)&&MagicBrighten.LocationEffect(2056)&&!MagicBrighten.LocationEffect(2027)&&!MagicBrighten.LocationEffect(2057),"S106 gfx 2028–2056 carry the flag");
        foreach(byte type in new byte[]{0,8}){
            var w=Seed();w.Receive(Range(40200,type,456),"offline");CasterTiming(w);var r=Bare();long release=w.MagicEffects.Single().At+Release;
            Compose(r,w,release-1);t.Equal(0,r.MagicBrighteningEffects,"not active before the effect starts "+type);
            Compose(r,w,release);t.Equal(type==8?1:0,r.MagicBrighteningEffects,"active from the effect's first frame "+type);
            t.Equal(type==8?14:9,r.MagicLightLevel(9),"light level while alive "+type);
            Compose(r,w,release+165);t.Equal(type==8?1:0,r.MagicBrighteningEffects,"still active on its last frame "+type);
            Compose(r,w,release+166);t.Equal(0,r.MagicBrighteningEffects,"restored when the last such effect ends "+type);
            t.Equal(9,r.MagicLightLevel(9),"light level restored "+type);
        }
        var w2=Seed();w2.Receive(Range(40200,8,456),"offline");w2.Receive(Range(40200,8,456),"offline");CasterTiming(w2);var r2=Bare();
        Compose(r2,w2,w2.MagicEffects.Max(x=>x.At)+Release);t.Equal(2,r2.MagicBrighteningEffects,"overlapping effects are counted (0x13CE258)");
    }

    // `]` 擴到落點特效；飛行物上的 `]` 待查證（Q-4），只記錄不生成。
    static void Landing(WorldModuleTestContext t){
        var w=Seed();w.Receive(Attack(40300),"offline");CasterTiming(w);var r=Bare();long release=w.MagicEffects.Single().At+Release;
        long now=release;bool flew=false;
        for(;now<release+2000;now+=10){Compose(r,w,now);if(r.MagicVisuals.Any(v=>v.Phase=="fly"))flew=true;if(r.MagicVisuals.Any(v=>v.Phase=="impact"))break;}
        t.Check(flew&&r.MagicVisuals.Any(v=>v is {Gfx:40301,Phase:"impact",X:32603,Y:32942}),"kind 4 flies and places its param gfx at the target",r.MagicVisuals);
        t.Check(r.WithheldSubEffects.Any(x=>x.Gfx==40303&&x.OwnerGfx==40300)&&r.SubEffects.Live.All(s=>s.Gfx!=40303),"flight `]` recorded as data only, never spawned (Q-4)",r.WithheldSubEffects);
        Compose(r,w,now+83);
        t.Check(r.SubEffects.Live.Any(s=>s.Gfx==40302&&s.X==32603&&s.Y==32942),"landing effect `]` spawns a child at the landing position",r.SubEffects.Live);
        int spawned=r.SubEffects.Live.Count(s=>s.Gfx==40302);Compose(r,w,now+120);
        t.Equal(spawned,r.SubEffects.Live.Count(s=>s.Gfx==40302),"each landing `]` frame fires once");
        w.Reset();Compose(r,w,now+130);typeof(Renderer).GetMethod("ComposeSubEffects",F)!.Invoke(r,[w,new List<Draw>(),now+130]);
        t.Check(r.WithheldSubEffects.Count==0&&r.SubEffects.Live.Count==0,"world reset clears withheld data and children");
    }
}
