using P06;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// T216 / #522 (M7-P): skill effect start/hit timing and 109 classes, skills-visual.md §2–§4.
// Invented list.spz metadata and one-pixel sprites only; never game assets, game, network or DB.
static partial class AttackSkillChecks
{
    const int Release = 500;
    static void SaveMetadata(string client, string metadata)
    {
        var clear = Encoding.ASCII.GetBytes(metadata); var packed = (byte[])clear.Clone();
        using (var aes = Aes.Create())
        {
            aes.Key = Convert.FromHexString("DC8401212A40200ADD25B9A70DB9C94E"); var iv = Convert.FromHexString("3E0978AAC4D53063300C5F9A807F2246"); int n = (clear.Length - 5) / 16 * 16;
            aes.EncryptCbc(clear.AsSpan(5, n), iv, PaddingMode.None).CopyTo(packed, 5);
            for (int i = 5 + n; i < clear.Length; i++) packed[i] = (byte)(clear[i] ^ packed[i - 16]);
        }
        Archive(client, "Text", new() { ["list.spz"] = packed }, true);
    }
    // Caster action captured with a known `!` release (500 ms after the action start).
    static void CasterTiming(WorldSession w)
    {
        var list = (List<MagicEffect>)typeof(WorldSession).GetField("magicEffects", Private)!.GetValue(w)!;
        for (int i = 0; i < list.Count; i++)
            list[i] = list[i] with { Timing = new ActionCadence(w.Actor!.Gfx, w.Actor.Weapon, list[i].Action, 1000, 1000, 1000, Release, 0, 0, false), ActionAt = list[i].At };
    }
    static Dictionary<uint, CombatAnimation> Hits(Renderer r) => (Dictionary<uint, CombatAnimation>)typeof(Renderer).GetField("projectileHits", Private)!.GetValue(r)!;
    static void Place(WorldSession w, uint id, int x, int y)
    {
        var objects = (System.Collections.Concurrent.ConcurrentDictionary<uint, WorldObject>)typeof(WorldSession).GetField("objects", Private)!.GetValue(w)!;
        objects[id] = JsonSerializer.Deserialize<WorldObject>($"{{\"ObjectId\":{id},\"Gfx\":65000,\"X\":{x},\"Y\":{y},\"HpByte\":75}}")!;
    }
    static byte[] RangeGfx((uint, ushort, uint)[] targets, ushort gfx, byte type = 0)
    { var p = Range(targets, false, type); BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(15), gfx); return p; }

    static void SkillEffectTiming(string root)
    {
        string client = Path.Combine(root, "skill-timing-client"); Directory.CreateDirectory(client);
        // 0.0:1! is displayed LAST (cursor advances first), i.e. 83 ms (2 ticks @24) after start.
        const string hitLast = "0.effect(1 2,0.0:1! 0.1:2)", noHit = "0.effect(1 2,0.0:1 0.1:2)";
        var gfx = new (int Gfx, string Action, string Extra)[] {
            (41000, hitLast, ""), (41001, noHit, ""), (41003, noHit, " 109.effect(3 41006)"),
            (41005, noHit, " 109.effect(5 41006)"), (41006, noHit, ""), (41007, hitLast, " 109.effect(7 41006)"),
            (41008, hitLast, " 109.effect(8 -1)"), (41009, hitLast, " 109.effect(6 -1)"), (41010, hitLast, " 109.effect(2 -1)") };
        SaveMetadata(client, "S12235 0 0\n" + string.Concat(gfx.Select(g => $"#{g.Gfx} 8 synthetic\n{g.Action}{g.Extra}\n")));
        var sprites = new Dictionary<string, byte[]>();
        foreach (var g in gfx) for (int d = 0; d < 8; d++) sprites[$"{g.Gfx}-{d}.spr"] = Sprite((ushort)(1000 + (g.Gfx - 41000) * 32 + d * 2));
        Archive(client, "Sprite", sprites);

        // `!` flag parsing and the native display order of hit frames.
        var catalog = new EffectSpriteCatalog(client);
        var hit = catalog.Get(41000);
        Check(hit.Steps[0].Hit && !hit.Steps[1].Hit && hit.HitOffsets().SequenceEqual(new long[] { 83 }), "list.spr `!` frame flag parsed, hit after the advanced first frame");
        Check(catalog.Get(41001).HitOffsets().Length == 0 && EffectSprite.Parse(1, "#1 8 x 0.effect(1 2,0.0:1<97! 0.1:2[12)").Steps[0].Hit, "`!` combined with sound marks; no `!` means no hit frame");
        Check(hit.LatestHit(1000, 1082) == null && hit.LatestHit(1000, 1083) == 1083 && hit.LatestHit(1000, 9999) == 1083, "latest hit frame time");
        Check(catalog.Get(41005).Kind == 5 && catalog.Get(41005).Impact == 41006 && catalog.Get(41007).Kind == 7 && catalog.Get(41008).Kind == 8, "109 kinds 5/7/8 parsed");
        Check(MagicClass.Flies(5, true) && MagicClass.Flies(5, false) && !MagicClass.Flies(3, true) && !MagicClass.Flies(4, true) && MagicClass.Flies(3, false), "flying classes: S30 3/4/5, S42 only 5");
        Check(MagicClass.CasterHits(8, false) && !MagicClass.CasterHits(8, true) && !MagicClass.CasterHits(7, false) && !MagicClass.CasterHits(0, false), "S30 kind 8 builds no effect object; S42 kind 8 uses the default class; kind 7 hits from its terminal EarthRising (T269)");

        // SV#5: S30 default class starts at the caster `!`, is placed at the target, hits on its own `!`.
        var w = Seed(); var r = BareRenderer(client); w.Receive(Attack(gfx: 41000, damage: 7), "offline"); CasterTiming(w);
        var e = w.MagicEffects.Single(); long release = e.At + Release;
        Compose(r, w, release - 1); Check(r.MagicVisuals.Length == 0 && Hits(r).Count == 0, "S30 effect waits for the caster `!` frame");
        var draws = Compose(r, w, release);
        Check(r.MagicVisuals is [{ Gfx: 41000, X: 32603, Y: 32942, Phase: "effect" } v] && v.At == release && draws.Count == 1, "S30 default effect starts at caster `!`, at the target position");
        Check(Hits(r).Count == 0, "no hit before the effect's own `!` frame");
        Compose(r, w, release + 83); Check(Hits(r).TryGetValue(456, out var h) && h.Action == 2 && h.At == release + 83, "S30 hit on the effect `!` frame");
        Check(w.Objects.Single(o => o.ObjectId == 456).Combat == null, "S30 receipt never animates the target directly");

        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41001, damage: 7), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        foreach (long t in new long[] { 0, 41, 83, 124, 500 }) Compose(r, w, release + t);
        Check(Hits(r).Count == 0, "effect sprite without `!` frames never triggers a hit (§3)");

        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41000, caster: 0, damage: 7), "offline"); long at0 = w.MagicEffects[0].At;
        Compose(r, w, at0); Check(r.MagicVisuals.Single().At == at0, "absent caster: effect starts immediately (§2.1)");

        // SV#6/#7: one S42 effect at the packet x,y; only the first target reacts (original defect kept).
        foreach (byte type in new byte[] { 0, 8 })
        {
            w = Seed(); r = BareRenderer(client);
            w.Receive(RangeGfx([(456, 32, 0), (457, 32, 0), (123, 0, 0)], 41000, type), "offline"); CasterTiming(w);
            Check(w.MagicEffects.Length == 1 && w.RangeTargets(w.MagicEffects[0].Number).Count == 3, "S42 attaches the whole list to ONE effect " + type);
            Check(w.Objects.All(o => o.Combat == null), "S42 receipt no longer plays hit reactions " + type);
            release = w.MagicEffects[0].At + Release;
            Compose(r, w, release - 1); Check(r.MagicVisuals.Length == 0, "S42 effect waits for caster `!` " + type);
            draws = Compose(r, w, release);
            Check(draws.Count == 1 && r.MagicVisuals is [{ X: 32600, Y: 32940, Gfx: 41000 }], "S42 default class: single effect at packet x,y " + type);
            Compose(r, w, release + 83);
            Check(Hits(r).Keys.SequenceEqual(new uint[] { 456 }) && Hits(r)[456].At == release + 83, "S42 default class: only the FIRST target is hit, on the effect `!` (original defect, §2.4) " + type);
        }
        w = Seed(); r = BareRenderer(client); w.Receive(RangeGfx([(456, 0, 0), (457, 32, 0)], 41000), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        Compose(r, w, release + 83); Check(Hits(r).Keys.SequenceEqual(new uint[] { 456 }), "S42 defect: a later nonzero entry still hits entry 0 (imul eax,0)");
        w = Seed(); r = BareRenderer(client); w.Receive(RangeGfx([(456, 0, 0), (457, 0, 0)], 41000), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        Compose(r, w, release + 83); Check(Hits(r).Count == 0, "S42 all-zero damage list hits nobody");
        w = Seed(); r = BareRenderer(client); w.Receive(RangeGfx([], 41000), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        draws = Compose(r, w, release); Check(draws.Count == 1 && Hits(r).Count == 0, "S42 empty list still shows its one effect");
        w = Seed(); r = BareRenderer(client); w.Receive(RangeGfx([(456, 32, 0), (457, 32, 0)], 41008), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        draws = Compose(r, w, release); Compose(r, w, release + 83);
        Check(draws.Count == 1 && Hits(r).Keys.SequenceEqual(new uint[] { 456 }), "S42 kind 8 uses the default class");

        // No effect object (missing resource): caster `!` hits every nonzero entry (sub_A4B060 uses entry i).
        w = Seed(); r = BareRenderer(client); w.Receive(RangeGfx([(456, 32, 0), (457, 32, 0), (123, 0, 0)], 65000), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        Compose(r, w, release - 1); Check(Hits(r).Count == 0, "no-effect S42 waits for caster `!`");
        draws = Compose(r, w, release);
        Check(draws.Count == 0 && Hits(r).Keys.Order().SequenceEqual(new uint[] { 456, 457 }) && Hits(r).Values.All(x => x.At == release) && r.EffectResourceErrors.ContainsKey(65000), "no-effect S42: caster `!` hits each damaged target");

        // SV#8 kind 5: flies at 60 ms from the caster to the first target; on arrival every damaged
        // target is hit and every visible target gets the param gfx.
        w = Seed(); Place(w, 457, 32606, 32945); r = BareRenderer(client);
        w.Receive(RangeGfx([(456, 32, 0), (457, 32, 0), (999, 32, 0), (123, 0, 0)], 41005), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        Compose(r, w, release); Check(r.MagicVisuals is [{ Phase: "fly", Gfx: 41005, X: 32601 }] && Hits(r).Count == 0, "kind 5 starts flying at caster `!` from packet x,y");
        long impact = release; while (impact < release + 2000) { Compose(r, w, impact); if (r.MagicVisuals.All(x => x.Phase != "fly")) break; impact += 10; }
        Check(impact > release && (impact - release) % 60 < 10 && r.MagicVisuals.All(x => x.Gfx == 41006 && x.Phase == "impact"), "kind 5 arrives on a 60 ms step and switches to the param gfx");
        Check(r.MagicVisuals.Select(x => (x.X, x.Y)).Order().SequenceEqual(new[] { (32600, 32940), (32603, 32942), (32606, 32945) }), "kind 5 param gfx at every visible target (invisible target has no inferred position)");
        Check(Hits(r).Keys.Order().SequenceEqual(new uint[] { 456, 457, 999 }) && Hits(r).Values.All(x => x.At == Hits(r)[456].At), "kind 5 hits every damaged target at arrival");
        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41005, damage: 7), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        for (long t = release; t < release + 1000; t += 10) Compose(r, w, t);
        Check(Hits(r).ContainsKey(456), "S30 kind 5 flies and hits its target");

        // kind 6: in place at the target's CURRENT position when started.
        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41009, damage: 7), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        Place(w, 456, 32604, 32943); Compose(r, w, release); Compose(r, w, release + 83);
        Check(r.MagicVisuals is [{ X: 32604, Y: 32943, Phase: "effect" }] && Hits(r)[456].At == release + 83, "kind 6 in place at target current position, hit on `!`");
        // kind 2: same as default (sub_8ACB20 hits entry 0 on `!`).
        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41010, damage: 7), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        Compose(r, w, release + 83); Check(r.MagicVisuals is [{ X: 32603 }] && Hits(r)[456].At == release + 83, "kind 2 placed at target, hit on `!`");

        // kind 8 (S30): no effect object, nothing drawn; caster `!` hits the target.
        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41008, damage: 7), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        Compose(r, w, release - 1); Check(Hits(r).Count == 0, "S30 kind 8 waits for caster `!`");
        draws = Compose(r, w, release); Check(draws.Count == 0 && r.MagicVisuals.Length == 0 && Hits(r)[456].At == release, "S30 kind 8 draws nothing, caster `!` hits");
        // kind 7 (S30, T269): 41007 has only two frames, so the segment cursor never reaches index 2: one segment, no chain, no hit.
        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41007, damage: 7), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        draws = Compose(r, w, release);
        Check(r.MagicVisuals is [{ Phase: "segment", X: 32600, Y: 32940 }] && r.UndrawnMagicEffects is [{ Kind: 7, EndGfx: 41006 } u] && u.Path.Length == 1 && Hits(r).Count == 0, "S30 kind 7 two-frame segment: drawn once at the caster, no chain, caster `!` does not hit");
        Check(MagicMotion.EruptionPath(32600, 32940, 32600, 32940).SequenceEqual(new[] { (32600, 32940) }), "kind 7 zero-length path");

        // SV#10: kind 3 never draws its 109 param, both as S30 magic and as an arrow.
        w = Seed(); r = BareRenderer(client); w.Receive(Attack(gfx: 41003, damage: 7), "offline"); CasterTiming(w); release = w.MagicEffects[0].At + Release;
        bool flew = false, landed = false;
        for (long t = release; t < release + 1000; t += 10) { draws = Compose(r, w, t); flew |= r.MagicVisuals.Any(x => x.Phase == "fly"); landed |= r.MagicVisuals.Any(x => x.Gfx == 41006) || draws.Count > 0 && r.MagicVisuals.Length == 0; }
        Check(flew && !landed && Hits(r).ContainsKey(456), "kind 3 magic flies and hits without param landing sprite");
        w = Seed(); r = BareRenderer(client); w.Receive(Attack(21, 41003, 127, damage: 7), "offline"); long shot = w.Projectiles.Single().At;
        bool arrowFlew = false, arrowLanded = false;
        for (long t = shot; t < shot + 1000; t += 10) { draws = Compose(r, w, t); if (r.ProjectileVisuals.Length > 0) arrowFlew = true; else if (arrowFlew && draws.Count > 0) arrowLanded = true; }
        Check(arrowFlew && !arrowLanded, "kind 3 arrow (e.g. 2349) draws no param (219) landing");
    }
}
