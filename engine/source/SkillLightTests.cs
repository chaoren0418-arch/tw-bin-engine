using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06;

// T298／R4-G（#710）離線測試：技能提亮（FU5§3）、舊素材 gfx 照 109 kind、10147／10145 疊層深度、`+40`。
// 由 --module-tests 執行（名稱 skill-light）。只用合成封包、合成 list.spr 條目（直接放進 EffectSpriteCatalog 快取）
// 與合成影格；不讀原客戶端、不讀 engine/assets、不開遊戲、網路或 DB。
static class SkillLightTests {
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
    static byte[] Range(ushort gfx,byte type,params uint[] targets)=>Packet(b=>{
        b.Write((byte)42);b.Write((byte)19);b.Write(123u);b.Write((ushort)32600);b.Write((ushort)32940);
        b.Write((byte)6);b.Write(9000001u);b.Write(gfx);b.Write(type);b.Write((ushort)0);b.Write((ushort)targets.Length);
        foreach(uint id in targets){b.Write(id);b.Write((ushort)32);}
    });
    static WorldSession Seed(){
        var w=new WorldSession();Set(w,"MapId",(ushort)0);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":200,\"Mp\":80,\"MaxMp\":100}")!);
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"Name\":\"offline\",\"Gfx\":65000,\"X\":32600,\"Y\":32940,\"HpPercent\":50,\"Brave\":1}")!);
        var objects=(System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject>)Get(w,"objects")!;
        objects[456]=JsonSerializer.Deserialize<WorldObject>("{\"ObjectId\":456,\"Gfx\":65000,\"X\":32603,\"Y\":32942,\"HpByte\":75,\"Brave\":1}")!;
        // Night: the curve gives L = 1 at 00:00, so the +5 boost is visible (6).
        w.Ambience.TimeReceived(0,0);
        return w;
    }
    static void CasterTiming(WorldSession w){
        var list=(List<MagicEffect>)Get(w,"magicEffects")!;
        for(int i=0;i<list.Count;i++)list[i]=list[i] with{Timing=new ActionCadence(w.Actor!.Gfx,w.Actor.Weapon,list[i].Action,1000,1000,1000,Release,0,0,false),ActionAt=list[i].At};
    }
    // Synthetic list.spr rows, non-directional, 2-tick frames (83 ms each @24 fps).
    static readonly Dictionary<int,string> Rows=new(){
        [40200]="#40200 1 effect\n0.effect(0 2,0.0:2 0.1:2)\n",
        [41001]="#41001 1 single\n0.effect(0 1,0.0:2)\n",
        [41002]="#41002 1 lightning\n0.effect(0 3,0.0:2 0.1:2 0.2:2)\n109.effect(2 -1)\n",
        [41003]="#41003 1 short lightning\n0.effect(0 2,0.0:2 0.1:2)\n109.effect(2 -1)\n",
        [41005]="#41005 1 fly\n0.effect(0 2,0.0:2 0.1:2)\n109.effect(5 -1)\n",
        [191]="#191 1 legacy kind8\n0.effect(0 2,0.0:2 0.1:2)\n109.effect(8 -1)\n",
        [197]="#197 1 legacy kind5\n0.effect(0 2,0.0:2 0.1:2)\n109.effect(5 -1)\n",
        [221]="#221 1 legacy default\n0.effect(0 2,0.0:2 0.1:2)\n",
        [10147]="#10147 1 rise\n0.effect(0 2,0.0:2 0.1:2)\n109.effect(6 -1)\n",
        [10145]="#10145 1 segment\n0.effect(0 3,0.0:2 0.1:2 0.2:2)\n109.effect(7 10147)\n",
        [2030]="#2030 1 location\n0.effect(0 2,0.0:2 0.1:2)\n",
        [2060]="#2060 1 location outside\n0.effect(0 2,0.0:2 0.1:2)\n",
    };
    const int BlockZ=10;
    static Renderer Bare(){
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);
        foreach(string name in new[]{"magicFlights","projectileFlights","projectileHits","effectResourceErrors","magicAppearances","projectileFrames"}){
            var f=typeof(Renderer).GetField(name,F)!;f.SetValue(r,Activator.CreateInstance(f.FieldType));
        }
        var catalog=new EffectSpriteCatalog(Path.Combine(AppContext.BaseDirectory,"skill-light-no-client"));
        var effects=(Dictionary<int,EffectSprite>)Get(catalog,"effects")!;var sprites=(Dictionary<(int,int,int),Frame[]>)Get(catalog,"sprites")!;
        foreach(var (gfx,row) in Rows){
            var e=EffectSprite.Parse(gfx,row);effects[gfx]=e;
            // One block per frame so the drawn Z (layer depth) can be inspected.
            sprites[(e.Sprite,0,e.TileType)]=Enumerable.Range(0,e.Steps.Max(s=>s.Frame)+1).Select(i=>new Frame($"0:{i}",[0,0,0,0],[new Block(0,0,BlockZ,0,new(e.TileType,[]))])).ToArray();
        }
        Set(r,"effectSprites",catalog);Set(r,"magicEpoch",-1);Set(r,"projectileEpoch",-1);
        return r;
    }
    static List<Draw> Compose(Renderer r,WorldSession w,long now){
        var draws=new List<Draw>();
        foreach(string m in new[]{"ComposeProjectile","ComposeMagic"})typeof(Renderer).GetMethod(m,F)!.Invoke(r,[w,draws,now]);
        return draws;
    }
    static Dictionary<uint,CombatAnimation> Hits(Renderer r)=>(Dictionary<uint,CombatAnimation>)Get(r,"projectileHits")!;
    static byte Level(WorldSession w)=>w.Ambience.At(0).Level;

    [WorldModuleTest("skill-light")]
    static void Run(WorldModuleTestContext t){
        Rules(t);RangeEffect(t);Lightning(t);Location(t);LegacyKinds(t);Depth(t);
    }

    static void Rules(WorldModuleTestContext t){
        t.Equal(LightClass.Effect,SkillLight.Class(0,true),"S42 default class = Effect");
        t.Equal(LightClass.Effect,SkillLight.Class(1,false),"S30 kind 1 = Effect");
        t.Equal(LightClass.Lightning,SkillLight.Class(2,false),"S30 kind 2 = Lightning");
        t.Equal(LightClass.Lightning,SkillLight.Class(2,true),"S42 kind 2 = Lightning");
        t.Check(SkillLight.Class(3,false)==LightClass.None&&SkillLight.Class(4,false)==LightClass.None&&SkillLight.Class(5,false)==LightClass.None&&SkillLight.Class(5,true)==LightClass.None,"flying classes never light");
        t.Check(SkillLight.Class(6,false)==LightClass.None&&SkillLight.Class(7,false)==LightClass.None&&SkillLight.Class(8,false)==LightClass.None,"S30 kind 6/7/8 never light");
        t.Equal((byte)1,SkillLight.LocationFlag(2028),"S106 2028 flag");t.Equal((byte)1,SkillLight.LocationFlag(2056),"S106 2056 flag");
        t.Equal((byte)0,SkillLight.LocationFlag(2027),"S106 2027 no flag");t.Equal((byte)0,SkillLight.LocationFlag(2057),"S106 2057 no flag");
        var two=EffectSprite.Parse(40200,Rows[40200]);var one=EffectSprite.Parse(41001,Rows[41001]);var bolt=EffectSprite.Parse(41002,Rows[41002]);var shortBolt=EffectSprite.Parse(41003,Rows[41003]);
        t.Equal((long?)0,SkillLight.BeginAfter(two,LightClass.Effect,8),"Effect +9≠0: begins at index 1 = the start");
        t.Equal((long?)null,SkillLight.BeginAfter(two,LightClass.Effect,0),"Effect +9=0: never");
        t.Equal((long?)null,SkillLight.BeginAfter(one,LightClass.Effect,8),"Effect with 1 frame never reaches index 1");
        t.Equal((long?)Frame,SkillLight.BeginAfter(bolt,LightClass.Lightning,0),"Lightning begins at index 2 (Steps[1] ms), ignores +9");
        t.Equal((long?)null,SkillLight.BeginAfter(shortBolt,LightClass.Lightning,0),"Lightning with 2 frames never reaches index 2");
        t.Check(!SkillLight.Lit(two,LightClass.Effect,8,2*Frame)&&SkillLight.Lit(two,LightClass.Effect,8,2*Frame-1),"ends when it finishes");
        t.Equal((byte)8,new RangeSkillPacket(19,123,0,0,6,1,40200,8,[]).LightFlag,"S42 light flag = type");
        t.Equal((byte)0,new AttackSkillPacket(18,123,456,7,3,338,40200,0,0,0,0,0).LightFlag,"S30 magic light flag = 0");
        // `+40` = list.spr 104 → EffectSprite.TileType (screen 8 → 18).
        t.Equal(18,EffectSprite.Parse(42000,"#42000 1 flag\n0.effect(0 1,0.0:2)\n104.flag(8)\n").TileType,"+40 draw flag comes from list.spr 104");
    }

    // S42 type 8 lights from the first frame to the end; type 0 does not. The level reaches Ambience (min(15, L+5)).
    static void RangeEffect(WorldModuleTestContext t){
        foreach(byte type in new byte[]{0,8}){
            var w=Seed();w.Receive(Range(40200,type,456),"offline");CasterTiming(w);var r=Bare();long release=w.MagicEffects.Single().At+Release;
            t.Equal((byte)1,Level(w),"night L before the effect "+type);
            Compose(r,w,release-1);t.Equal((byte)1,Level(w),"not lit before the caster `!` "+type);
            Compose(r,w,release);t.Equal(type==8?(byte)6:(byte)1,Level(w),"Ambience L while alive "+type);
            Compose(r,w,release+2*Frame-1);t.Equal(type==8?(byte)6:(byte)1,Level(w),"still lit on the last frame "+type);
            Compose(r,w,release+2*Frame);t.Equal((byte)1,Level(w),"L restored when it finishes "+type);
            t.Equal(0,r.MagicBrighteningEffects,"counter back to 0 "+type);
        }
        // Two overlapping effects: one Begin per effect; the boost is applied once and removed after the last.
        var w2=Seed();w2.Receive(Range(40200,8,456),"offline");CasterTiming(w2);var r2=Bare();long first=w2.MagicEffects.Single().At+Release;
        Compose(r2,w2,first);
        var list=(List<MagicEffect>)Get(w2,"magicEffects")!;var copy=list[0] with{Number=list[0].Number+100,At=list[0].At+Frame,ActionAt=list[0].ActionAt+Frame};list.Add(copy);
        Compose(r2,w2,first+Frame);t.Equal(2,r2.MagicBrighteningEffects,"two effects hold the counter");t.Equal((byte)6,Level(w2),"boost applied once (L+5, not L+10)");
        Compose(r2,w2,first+2*Frame);t.Equal(1,r2.MagicBrighteningEffects,"first finished");t.Equal((byte)6,Level(w2),"still boosted while one remains");
        Compose(r2,w2,first+3*Frame);t.Equal((byte)1,Level(w2),"restored after the last");
        // One-frame Effect never lights.
        var w3=Seed();w3.Receive(Range(41001,8,456),"offline");CasterTiming(w3);var r3=Bare();long r3at=w3.MagicEffects.Single().At+Release;
        Compose(r3,w3,r3at);t.Equal(0,r3.MagicBrighteningEffects,"one-frame Effect never lights (index 1 never reached)");
        // S42 kind 5 flying class: no light while flying.
        var w4=Seed();w4.Receive(Range(41005,8,456),"offline");CasterTiming(w4);var r4=Bare();long r4at=w4.MagicEffects.Single().At+Release;
        Compose(r4,w4,r4at+10);t.Check(r4.MagicVisuals.Any(v=>v.Phase=="fly")&&r4.MagicBrighteningEffects==0,"flying class does not light",r4.MagicVisuals);
    }

    // kind 2 Lightning: lights from index 2 regardless of +9 (S30 writes 0), for S30 and S42.
    static void Lightning(WorldModuleTestContext t){
        var w=Seed();w.Receive(Attack(41002),"offline");CasterTiming(w);var r=Bare();long release=w.MagicEffects.Single().At+Release;
        Compose(r,w,release);t.Check(r.MagicVisuals.Length==1&&r.MagicBrighteningEffects==0,"S30 kind 2: drawn, not lit on index 1",r.MagicVisuals);
        Compose(r,w,release+Frame-1);t.Equal((byte)1,Level(w),"not lit before index 2");
        Compose(r,w,release+Frame);t.Equal(1,r.MagicBrighteningEffects,"S30 kind 2 lit from index 2");t.Equal((byte)6,Level(w),"Ambience boosted");
        Compose(r,w,release+3*Frame-1);t.Equal((byte)6,Level(w),"lit until the last frame");
        Compose(r,w,release+3*Frame);t.Equal((byte)1,Level(w),"restored when it finishes");
        var w2=Seed();w2.Receive(Range(41002,0,456),"offline");CasterTiming(w2);var r2=Bare();long rel2=w2.MagicEffects.Single().At+Release;
        Compose(r2,w2,rel2+Frame);t.Equal(1,r2.MagicBrighteningEffects,"S42 type 0 with a kind 2 gfx still lights");
        var w3=Seed();w3.Receive(Attack(41003),"offline");CasterTiming(w3);var r3=Bare();long rel3=w3.MagicEffects.Single().At+Release;
        Compose(r3,w3,rel3+Frame);t.Equal(0,r3.MagicBrighteningEffects,"2-frame Lightning never reaches index 2");
        var w4=Seed();w4.Receive(Attack(40200),"offline");CasterTiming(w4);var r4=Bare();long rel4=w4.MagicEffects.Single().At+Release;
        Compose(r4,w4,rel4);t.Equal(0,r4.MagicBrighteningEffects,"S30 default Effect (+9 = 0) does not light");
    }

    // S106 gfx 2028–2056: Effect with +9 = 1 while it plays once.
    static void Location(WorldModuleTestContext t){
        foreach(ushort gfx in new ushort[]{2030,2060}){
            var w=Seed();_=w.PacketEffects;var list=(List<PacketEffect>)Get(w,"packetEffects")!;
            list.Add(new(1,w.WorldEpoch,new ServerEffect(null,32600,32940,gfx,null),1000,long.MaxValue));
            // An S55 (target) effect with the same gfx never carries the flag.
            list.Add(new(2,w.WorldEpoch,new ServerEffect(456,0,0,gfx,null),1000,long.MaxValue));
            var r=Bare();bool inside=gfx==2030;
            Compose(r,w,999);t.Equal(0,r.MagicBrighteningEffects,"S106 not lit before it starts "+gfx);
            Compose(r,w,1000);t.Equal(inside?1:0,r.MagicBrighteningEffects,"S106 lit only for 2028–2056 (S55 never) "+gfx);
            t.Equal(inside?(byte)6:(byte)1,Level(w),"S106 Ambience L "+gfx);
            Compose(r,w,1000+2*Frame);t.Equal((byte)1,Level(w),"S106 restored after one cycle "+gfx);
        }
    }

    // Legacy gfx path follows the runtime 109 kind: kind 8 draws nothing (caster `!` hits), kind 5 flies, kind 0 stays legacy.
    static void LegacyKinds(WorldModuleTestContext t){
        var probe=new MagicEffect(1,123,456,191,18,7,3,32600,32940,32603,32942,0);
        t.Check(!LegacyMagic.UsesNativeClass(probe,null),"no metadata keeps the legacy path");
        t.Check(!LegacyMagic.UsesNativeClass(probe with{Gfx=221},EffectSprite.Parse(221,Rows[221])),"default kind keeps the legacy path");
        t.Check(!LegacyMagic.UsesNativeClass(probe with{Gfx=167},EffectSprite.Parse(197,Rows[197])),"167 keeps its own flight path");
        t.Check(LegacyMagic.UsesNativeClass(probe,EffectSprite.Parse(191,Rows[191])),"kind 8 goes native");

        var w=Seed();w.Receive(Attack(191),"offline");CasterTiming(w);var r=Bare();long release=w.MagicEffects.Single().At+Release;
        Compose(r,w,release);
        t.Check(r.MagicVisuals.Length==0&&Hits(r).TryGetValue(456,out var h)&&h.At==release,"legacy 191 with kind 8: nothing drawn, caster `!` hits",new{r.MagicVisuals,hits=Hits(r)});
        t.Check(!r.EffectResourceErrors.ContainsKey(191),"no legacy asset was needed");

        w=Seed();w.Receive(Attack(197),"offline");CasterTiming(w);r=Bare();release=w.MagicEffects.Single().At+Release;
        bool flew=false;for(long now=release;now<release+1000;now+=10){Compose(r,w,now);if(r.MagicVisuals.Any(v=>v.Gfx==197&&v.Phase=="fly"))flew=true;}
        t.Check(flew,"legacy 197 with kind 5 flies");
        t.Check(Hits(r).TryGetValue(456,out var h2)&&h2.At>release,"kind 5 hits on arrival, not at the caster `!`",Hits(r));
    }

    // dword_13CCBA4: +29 per EarthRising, 10147 / 10145 draw with it as extra depth; the kind 7 terminal resets it.
    static void Depth(WorldModuleTestContext t){
        t.Check(EarthDepth.Z(10,0)==10&&EarthDepth.Z(10,29)==39&&EarthDepth.Z(250,29)==255,"depth added to Z, capped at 255");
        t.Check(EarthDepth.Stacks(10147)&&EarthDepth.Stacks(10145)&&!EarthDepth.Stacks(10146),"only 10147 / 10145 stack");
        static int[] MagicZ(List<Draw> d)=>d.Where(x=>x.Layer=="magic").Select(x=>x.Z).ToArray();

        var w=Seed();w.Receive(Attack(10147),"offline");w.Receive(Attack(10147),"offline");CasterTiming(w);var r=Bare();long release=w.MagicEffects.Max(e=>e.At)+Release;
        var z=MagicZ(Compose(r,w,release));
        t.Check(z.Length==2&&z.Contains(BlockZ+29)&&z.Contains(BlockZ+58),"stacked S30 kind 6 10147 draw one above another",z);
        t.Equal(58,r.EarthDepthCounter,"two EarthRisings: counter 58");

        w=Seed();w.Receive(Attack(10145),"offline");CasterTiming(w);r=Bare();release=w.MagicEffects.Single().At+Release;
        z=MagicZ(Compose(r,w,release));t.Check(z.Length>0&&z.All(x=>x==BlockZ),"first segment: counter 0, no extra depth",z);
        var plan=r.EruptionPlanFor(w.MagicEffects.Single().Number)!;
        t.Check(plan.Chains,"eruption chains to a terminal EarthRising");
        long terminal=release+plan.TerminalOffset;
        var draws=Compose(r,w,terminal);
        t.Check(r.MagicVisuals.Any(v=>v is {Gfx:10147,Phase:"terminal"}),"terminal EarthRising drawn",r.MagicVisuals);
        t.Check(MagicZ(draws).Contains(BlockZ+29),"terminal 10147 takes counter +29",MagicZ(draws));
        t.Equal(0,r.EarthDepthCounter,"terminal resets the counter to 0");
        Compose(r,w,terminal+10);t.Equal(0,r.EarthDepthCounter,"reset happens once (no repeated +29)");

        w=Seed();w.Receive(Attack(40200),"offline");CasterTiming(w);r=Bare();release=w.MagicEffects.Single().At+Release;
        z=MagicZ(Compose(r,w,release));t.Check(z.Length>0&&z.All(x=>x==BlockZ),"other gfx keep their own Z",z);
    }
}
