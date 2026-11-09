using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
namespace P06;

// T269（#643）離線測試：技能飛行 kind 6／7、`]` 的範圍、SpellList.xml 冷卻來源。由 --module-tests 執行（名稱 projectile-kind）。
// 依據 docs/reference/trade-window-and-projectiles.md §3、followups-3.md §1。
// 只用合成封包、合成 list.spr 條目（直接放進 EffectSpriteCatalog 快取）、空白影格與合成 Tile.idx／XML；
// 不讀原客戶端、不讀 engine/assets、不開遊戲、網路或 DB。
static class ProjectileKindTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const int Release=500;
    static void Set(object o,string name,object? value){
        var t=o.GetType();
        if(t.GetProperty(name,F) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
        (t.GetField(name,F)??t.GetField($"<{name}>k__BackingField",F))!.SetValue(o,value);
    }
    static object? Get(object o,string name)=>o.GetType().GetField(name,F)!.GetValue(o);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    // S30: caster 123 at (32600,32940), target 456 at (32603,32942) — Chebyshev distance 3.
    static byte[] Attack(ushort gfx,uint target=456,ushort damage=7)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)18);b.Write(123u);b.Write(target);b.Write(damage);
        b.Write((byte)3);b.Write(338u);b.Write(gfx);b.Write((byte)0);
        b.Write((ushort)32600);b.Write((ushort)32940);b.Write((ushort)32603);b.Write((ushort)32942);b.Write(0u);b.Write((byte)0);
    });
    static byte[] Range(ushort gfx,params uint[] targets)=>Packet(b=>{
        b.Write((byte)42);b.Write((byte)19);b.Write(123u);b.Write((ushort)32600);b.Write((ushort)32940);
        b.Write((byte)6);b.Write(9000001u);b.Write(gfx);b.Write((byte)0);b.Write((ushort)0);b.Write((ushort)targets.Length);
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
    static void CasterTiming(WorldSession w){
        var list=(List<MagicEffect>)Get(w,"magicEffects")!;
        for(int i=0;i<list.Count;i++)list[i]=list[i] with{Timing=new ActionCadence(w.Actor!.Gfx,w.Actor.Weapon,list[i].Action,1000,1000,1000,Release,0,0,false),ActionAt=list[i].At};
    }
    // Synthetic list.spr rows, non-directional, 2-tick frames (83 ms each @24 fps).
    static readonly Dictionary<int,string> Rows=new(){
        // kind 7 segment: 3 frames, `!` on its last frame (segments must still never hit), param 42006.
        [42007]="#42007 1 eruption\n0.effect(0 3,0.0:2 0.1:2 0.2:2!)\n109.effect(7 42006)\n",
        // EarthRising param: both frames `!` (two hits), plus a `]` that must not spawn.
        [42006]="#42006 1 rising\n0.effect(0 2,0.0:2! 0.1:2]42099!)\n",
        // kind 7 whose param has no `!` (like 4728→4730): no hit at all.
        [42008]="#42008 1 eruption nohit\n0.effect(0 3,0.0:2 0.1:2 0.2:2)\n109.effect(7 42009)\n",
        [42009]="#42009 1 boom\n0.effect(0 2,0.0:2 0.1:2)\n",
        // two-step segment gfx (sub_81C730(…, 2)).
        [7374]="#7374 1 two step\n0.effect(0 3,0.0:2 0.1:2 0.2:2)\n109.effect(7 42006)\n",
        // two-frame kind 7: the cursor never reaches index 2.
        [42013]="#42013 1 short eruption\n0.effect(0 2,0.0:2 0.1:2)\n109.effect(7 42006)\n",
        // kind 6 / kind 2 / default kind 0, each with `!` and `]42099`.
        [42010]="#42010 1 earth rising\n0.effect(0 2,0.0:2! 0.1:2]42099!)\n109.effect(6 -1)\n",
        [42011]="#42011 1 lightning\n0.effect(0 2,0.0:2! 0.1:2]42099)\n109.effect(2 -1)\n",
        [42012]="#42012 1 default\n0.effect(0 2,0.0:2! 0.1:2]42099)\n",
        [42099]="#42099 1 child\n0.effect(0 2,0.0:2 0.1:2)\n",
    };
    static Renderer Bare(){
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);
        foreach(string name in new[]{"magicFlights","projectileFlights","projectileHits","effectResourceErrors","magicAppearances","projectileFrames"}){
            var f=typeof(Renderer).GetField(name,F)!;f.SetValue(r,Activator.CreateInstance(f.FieldType));
        }
        var catalog=new EffectSpriteCatalog(Path.Combine(AppContext.BaseDirectory,"projectile-kind-no-client"));
        var effects=(Dictionary<int,EffectSprite>)Get(catalog,"effects")!;var sprites=(Dictionary<(int,int,int),Frame[]>)Get(catalog,"sprites")!;
        foreach(var (gfx,row) in Rows){var e=EffectSprite.Parse(gfx,row);effects[gfx]=e;sprites[(e.Sprite,0,e.TileType)]=[Blank("0:0"),Blank("0:1"),Blank("0:2")];}
        Set(r,"effectSprites",catalog);Set(r,"magicEpoch",-1);Set(r,"projectileEpoch",-1);
        return r;
    }
    static Frame Blank(string key)=>new(key,[0,0,0,0],[]);
    static void Compose(Renderer r,WorldSession w,long now){
        var draws=new List<Draw>();
        foreach(string m in new[]{"ComposeProjectile","ComposeMagic"})typeof(Renderer).GetMethod(m,F)!.Invoke(r,[w,draws,now]);
    }
    static Dictionary<uint,CombatAnimation> Hits(Renderer r)=>(Dictionary<uint,CombatAnimation>)Get(r,"projectileHits")!;
    static (WorldSession W,Renderer R,long Release) Cast(ushort gfx,uint target=456){
        var w=Seed();w.Receive(Attack(gfx,target),"offline");CasterTiming(w);return (w,Bare(),w.MagicEffects.Single().At+Release);
    }

    [WorldModuleTest("projectile-kind")]
    static void Run(WorldModuleTestContext t){
        Plan(t);Eruption(t);EarthRising(t);SubEffectScope(t);SpellList(t);
    }

    // EruptionMotion.Plan：段數＝切比雪夫距離，每段在「第三格」時間生下一段，兩步 gfx，4 方向。
    static void Plan(WorldModuleTestContext t){
        var seg=EffectSprite.Parse(42007,Rows[42007]);
        t.Equal((long?)83,EruptionMotion.NextSegmentAfter(seg),"next segment when the cursor reaches index 2 (Steps[1] ms after start)");
        var p=EruptionMotion.Plan(32600,32940,32603,32942,3,seg);
        t.Check(p.Chains&&p.Segments.Select(s=>(s.X,s.Y,s.Offset)).SequenceEqual(new[]{(32600,32940,0L),(32601,32941,83L),(32602,32941,166L)}),"three segments from the caster tile, one step each, 83 ms apart",p.Segments);
        t.Equal(249L,p.TerminalOffset,"terminal EarthRising when the next step reaches the end tile");
        t.Equal(3,p.Direction,"direction 3 kept (0–3)");
        t.Equal(3,EruptionMotion.Plan(32603,32942,32600,32940,0,seg).Direction,"computed direction 7 (>3) minus 4 → 3");
        var two=EruptionMotion.Plan(32600,32940,32603,32942,3,EffectSprite.Parse(7374,Rows[7374]));
        t.Check(two.Chains&&two.Segments.Length==3&&two.Segments[1] is {X:32602,Y:32941}&&two.Segments[2] is {X:32604,Y:32943}&&two.TerminalOffset==249,"gfx 7374: two steps per segment, remaining still drops by one",two.Segments);
        var zero=EruptionMotion.Plan(32600,32940,32600,32940,3,seg);
        t.Check(zero.Chains&&zero.Segments.Length==1&&zero.TerminalOffset==83,"zero distance: one segment, terminal at its third frame",zero);
        var shortPlan=EruptionMotion.Plan(32600,32940,32603,32942,3,EffectSprite.Parse(42013,Rows[42013]));
        t.Check(!shortPlan.Chains&&shortPlan.Segments.Length==1&&shortPlan.TerminalOffset==-1,"two-frame segment never chains",shortPlan);
        t.Check(!MagicClass.CasterHits(7,false)&&MagicClass.CasterHits(8,false),"S30 kind 7 no longer hits on the caster `!`; kind 8 still does");
    }

    // S30 kind 7：施法者 `!` 格不受擊，段不受擊，終點 EarthRising 的每個 `!` 格受擊。
    static void Eruption(WorldModuleTestContext t){
        var (w,r,release)=Cast(42007);
        Compose(r,w,release);
        t.Check(Hits(r).Count==0,"caster `!` does not hit (was: CasterHits kind 7)",Hits(r));
        t.Check(r.MagicVisuals is [{Gfx:42007,X:32600,Y:32940,Phase:"segment"}],"first segment at the caster tile",r.MagicVisuals);
        t.Equal(0,r.UndrawnMagicEffects.Length,"kind 7 is drawn, not withheld");
        Compose(r,w,release+166);
        t.Check(r.MagicVisuals.Count(v=>v.Phase=="segment")==3&&Hits(r).Count==0,"segments overlap (each plays its whole animation) and never hit, even with `!`",r.MagicVisuals);
        // Target moves before the terminal starts: EarthRising re-aims at its current position (81CEB0).
        Place(w,456,32605,32945);
        Compose(r,w,release+248);t.Equal(0,Hits(r).Count,"no hit before the terminal object");
        Compose(r,w,release+249);
        t.Check(r.MagicVisuals.Any(v=>v is {Gfx:42006,Phase:"terminal",X:32605,Y:32945}),"terminal EarthRising of the param gfx at the target's current position",r.MagicVisuals);
        t.Check(Hits(r).TryGetValue(456,out var h)&&h.At==release+249,"hit = last segment build time (+249) + first `!` frame",Hits(r));
        Compose(r,w,release+332);
        t.Check(Hits(r).TryGetValue(456,out var h2)&&h2.At==release+332,"every `!` frame of the terminal object hits again",Hits(r));
        t.Check(r.SubEffects.Live.All(s=>s.Gfx!=42099),"terminal EarthRising does not process `]`",r.SubEffects.Live);
        t.Check(r.EruptionPlanFor(w.MagicEffects.Single().Number) is {Segments.Length:3},"plan exposed as data");

        (w,r,release)=Cast(42008);
        for(long now=release;now<release+800;now+=10)Compose(r,w,now);
        t.Equal(0,Hits(r).Count,"param without `!` (4728→4730 case): no hit");

        (w,r,release)=Cast(42013);
        for(long now=release;now<release+800;now+=10)Compose(r,w,now);
        t.Check(Hits(r).Count==0&&r.UndrawnMagicEffects is [{Kind:7}],"two-frame segment: no chain, recorded",r.UndrawnMagicEffects);

        (w,r,release)=Cast(42007,target:0);long number=w.MagicEffects.Single().Number;
        for(long now=release;now<release+800;now+=10)Compose(r,w,now);
        t.Check(Hits(r).Count==0&&r.EruptionPlanFor(number) is {Segments.Length:3},"no target: packet destination, no hit");

        w.Reset();Compose(r,w,release+900);
        t.Equal((EruptionPlan?)null,r.EruptionPlanFor(number),"world reset clears plans");
    }

    // S30 kind 6：目標目前位置播一次，每個 `!` 格都受擊。
    static void EarthRising(WorldModuleTestContext t){
        var (w,r,release)=Cast(42010);
        Place(w,456,32604,32943);
        Compose(r,w,release);
        t.Check(r.MagicVisuals is [{Gfx:42010,X:32604,Y:32943}]&&Hits(r).TryGetValue(456,out var h)&&h.At==release,"kind 6 at the target's current position, first `!` hits",new{r.MagicVisuals,hits=Hits(r)});
        Compose(r,w,release+83);
        t.Check(Hits(r).TryGetValue(456,out var h2)&&h2.At==release+83,"second `!` hits again",Hits(r));
        Compose(r,w,release+166);t.Equal(0,r.MagicVisuals.Length,"plays once");
    }

    // `]` 只在預設 Effect（與角色等）處理；kind 2／6／7 與飛行物不處理。
    static void SubEffectScope(WorldModuleTestContext t){
        foreach(var (gfx,spawns,what) in new[]{((ushort)42010,false,"kind 6"),((ushort)42011,false,"kind 2"),((ushort)42012,true,"default kind 0")}){
            var (w,r,release)=Cast(gfx);
            Compose(r,w,release);Compose(r,w,release+90);
            t.Equal(spawns,r.SubEffects.Live.Any(s=>s.Gfx==42099),$"{what}: `]` spawns = {spawns}");
        }
        // S42 non-flying classes are the default Effect class: `]` still processed even for a kind 6 gfx.
        var w2=Seed();w2.Receive(Range(42010,456),"offline");CasterTiming(w2);var r2=Bare();long rel2=w2.MagicEffects.Single().At+Release;
        Compose(r2,w2,rel2);Compose(r2,w2,rel2+90);
        t.Check(r2.SubEffects.Live.Any(s=>s.Gfx==42099),"S42 default class keeps `]`",r2.SubEffects.Live);
    }

    // SpellList.xml：id+1＝3.81 技能編號；delay 預設 10；direction 對不上引擎動作時不套用；檔案缺漏時沿用現值。
    static void SpellList(WorldModuleTestContext t){
        const string xml="<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<spells>\r\n\t<!-- x -->\r\n\t<catalysttype id=\"0\" nameid=\"166\"/>\r\n"
            +"\t<spell id=\"0\" name=\"\xAA\xEC\" delay=\"10\"/>\r\n"
            +"\t<spell id=\"3\" name=\"a\"/>\r\n"
            +"\t<spell id=\"86\" name=\"b\" delay=\"8000\" direction=\"true\"/>\r\n"
            +"\t<spell id=\"87\" delay='3000' dir=\"false\"></spell>\r\n"
            +"\t<pspell id=\"50\" delay=\"500\"/>\r\n"
            +"\t<spell id=\"60\" delay=\"1\"/><spell id=\"60\" delay=\"2\"/>\r\n</spells>";
        var table=SpellListCooldowns.Parse(xml);
        t.Check(table.Entries[1] is {DelayMs:10,Direction:false}&&table.Entries[4] is {DelayMs:10,Direction:false},"id 0 → skill 1; missing delay defaults to 10 ms, direction false",table.Entries);
        t.Check(table.Entries[87] is {DelayMs:8000,Direction:true}&&table.Entries[88] is {DelayMs:3000,Direction:false}&&table.Entries[51].DelayMs==500,"delay/direction/dir, single quotes, <pspell>",table.Entries);
        t.Check(!table.Entries.ContainsKey(61)&&table.Ambiguous.Contains(61),"duplicate id is not applied");
        t.Equal(8000,SpellListCooldowns.ReuseMs(87,18,0,table),"listed, direction true ↔ action 18: XML delay");
        t.Equal(10,SpellListCooldowns.ReuseMs(1,19,0,table),"listed, direction false ↔ action 19: XML delay (default 10)");
        t.Equal(77,SpellListCooldowns.ReuseMs(87,19,77,table),"direction disagrees with the engine action: mapping not trusted, fallback");
        t.Equal(77,SpellListCooldowns.ReuseMs(61,19,77,table),"ambiguous: fallback");
        t.Equal(77,SpellListCooldowns.ReuseMs(200,19,77,table),"unlisted: fallback");
        t.Equal(1000+10,(new ActionCadence(1,0,19,0,1000,1000,null,SpellListCooldowns.ReuseMs(1,19,0,table),0,false)).CastWaitMs,"cast wait = action frames + delay (sub_A3BC30 + +56)");

        // Synthetic Tile.idx/pak: plain and encrypted payloads; "-c" first (FileManager order).
        byte[] body=Encoding.Latin1.GetBytes(xml);
        var (idx,pak)=Archive(("SpellList.xml",Encoding.ASCII.GetBytes("<spells><spell id=\"0\" delay=\"5\"/></spells>")),("other.xml",[1,2,3]),("SpellList-c.xml",body));
        var loaded=SpellListCooldowns.FromArchive("synthetic",idx,new MemoryStream(pak));
        t.Check(loaded?.Source is {Entry:"SpellList-c.xml",KnownKor25:false} s&&s.XmlSha256==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(body)).ToLowerInvariant()&&loaded.Entries[87].DelayMs==8000,"reads SpellList-c.xml first and records its SHA-256",loaded?.Source);
        var enc=Encrypt(body);
        t.Equal(xml,Encoding.Latin1.GetString(NativeUi.UiXmlSource.Decrypt(enc)),"test encryptor round-trips");
        (idx,pak)=Archive(("SpellList.xml",enc));
        loaded=SpellListCooldowns.FromArchive("synthetic",idx,new MemoryStream(pak));
        t.Check(loaded?.Entries[87].DelayMs==8000&&loaded.Source!.Entry=="SpellList.xml","encrypted entry decrypted in memory",loaded?.Source);
        (idx,pak)=Archive(("other.xml",[1]));
        t.Check(SpellListCooldowns.FromArchive("synthetic",idx,new MemoryStream(pak))==null,"no SpellList entry: null (current cooldown kept)");
        t.Check(SpellListCooldowns.Load(Path.Combine(AppContext.BaseDirectory,"projectile-kind-no-client"))==null,"missing client directory: null");
        bool threw=false;try{SpellListCooldowns.FromArchive("synthetic",[1,0,0,0],new MemoryStream());}catch(InvalidDataException){threw=true;}
        t.Check(threw,"malformed idx rejected");
    }
    static (byte[] Idx,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var idx=new MemoryStream();using var pak=new MemoryStream();using var w=new BinaryWriter(idx);
        w.Write((uint)files.Length);
        foreach(var (name,data) in files){
            w.Write((uint)pak.Position);var raw=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(raw,0);w.Write(raw);w.Write((uint)data.Length);pak.Write(data);
        }
        w.Flush();return (idx.ToArray(),pak.ToArray());
    }
    // Inverse of UiXmlSource.Decrypt: "<"+3 bytes → "X?xm"-style header is NOT required by the format, only blob[0..4]=="X?xm".
    static byte[] Encrypt(byte[] plain){
        // plain = "<?xm" + body; encrypted = "X?xm" + CBC(body) with the tail XORed with the running IV.
        if(plain.Length<4||plain[0]!='<'||plain[1]!='?'||plain[2]!='x'||plain[3]!='m')throw new ArgumentException("needs <?xm");
        byte[] key=[0xDC,0x84,0x01,0x21,0x2A,0x40,0x20,0x0A,0xDD,0x25,0xB9,0xA7,0x0D,0xB9,0xC9,0x4E];
        byte[] iv=[0x3E,0x09,0x78,0xAA,0xC4,0xD5,0x30,0x63,0x30,0x0C,0x5F,0x9A,0x80,0x7F,0x22,0x46];
        using var aes=System.Security.Cryptography.Aes.Create();aes.Key=key;
        var output=new byte[plain.Length];output[0]=(byte)'X';output[1]=plain[1];output[2]=plain[2];output[3]=plain[3];
        int bodyLength=plain.Length-4,full=bodyLength/16*16;var block=new byte[16];var cipher=new byte[16];
        for(int i=0;i<full;i+=16){
            for(int k=0;k<16;k++)block[k]=(byte)(plain[4+i+k]^iv[k]);
            aes.EncryptEcb(block,cipher,System.Security.Cryptography.PaddingMode.None);
            cipher.CopyTo(output,4+i);cipher.CopyTo(iv,0);
        }
        for(int k=0;k<bodyLength-full;k++)output[4+full+k]=(byte)(plain[4+full+k]^iv[k]);
        return output;
    }
}
