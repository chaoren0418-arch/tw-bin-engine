using P06;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

// Explicitly synthetic protocol tests. No LoginConnection, network, game, form or database.
static partial class AttackSkillChecks
{
    static readonly List<string> Passed = [];
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Passed.Add(label); }
    static void Set(object obj, string name, object value) => obj.GetType().GetProperty(name)!.SetValue(obj, value);
    static byte[] Packet(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream(); using var b = new BinaryWriter(stream);
        write(b); return LoginPackets.Pad(stream.ToArray());
    }
    static byte[] Attack(byte action = 18, ushort gfx = 167, byte flight = 0, uint sequence = 338,
        uint caster = 123, uint target = 456, ushort damage = 65535) => Packet(b =>
    {
        b.Write((byte)30); b.Write(action); b.Write(caster); b.Write(target); b.Write(damage);
        b.Write((byte)3); b.Write(sequence); b.Write(gfx); b.Write(flight);
        b.Write((ushort)32600); b.Write((ushort)32940); b.Write((ushort)32603); b.Write((ushort)32942);
        b.Write(0u); b.Write((byte)0);
    });
    static byte[] Range((uint Id, ushort Hit, uint Damage)[] targets, bool extended = false, byte type = 0) => Packet(b =>
    {
        b.Write((byte)42); b.Write((byte)19); b.Write(123u); b.Write((ushort)32600); b.Write((ushort)32940);
        b.Write((byte)6); b.Write(9000001u); b.Write((ushort)221); b.Write(type); b.Write((ushort)0);
        b.Write((ushort)targets.Length);
        foreach (var t in targets)
        {
            b.Write(t.Id); b.Write(t.Hit);
            if (extended && t.Id != 0) { b.Write(0xD14A6E3Fu); b.Write(t.Damage); }
        }
    });
    static WorldSession Seed()
    {
        var w = new WorldSession(); Set(w,"MapId",(ushort)0); Set(w,"CacheEnabled",false);
        Set(w, "Status", JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":200,\"Mp\":80,\"MaxMp\":100}")!);
        Set(w, "Actor", JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"Name\":\"offline\",\"Gfx\":65000,\"X\":32600,\"Y\":32940,\"HpPercent\":50}")!);
        var objects = (System.Collections.Concurrent.ConcurrentDictionary<uint, WorldObject>)typeof(WorldSession)
            .GetField("objects", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(w)!;
        foreach (uint id in new uint[] { 456, 457 })
            objects[id] = JsonSerializer.Deserialize<WorldObject>($"{{\"ObjectId\":{id},\"Gfx\":65000,\"X\":32603,\"Y\":32942,\"HpByte\":75}}")!;
        return w;
    }
    static void Reject(byte[] bytes, string label)
    {
        var w = Seed(); var actor = w.Actor; var objects = w.Objects; var status = w.Status;
        bool rejected = false;
        try { w.Receive(bytes, "offline"); } catch (ProtocolException) { rejected = true; }
        Check(rejected, label + " rejected");
        Check(w.Actor == actor && w.Objects.SequenceEqual(objects) && w.Status == status && w.OwnCombat == null &&
            w.MagicEffectCount == 0 && w.AttackReports == 0 && w.LastRangeSkill == null, label + " atomic");
    }
    static void MutateReject(byte[] source, int offset, byte value, string label)
    { var bytes = (byte[])source.Clone(); bytes[offset] = value; Reject(bytes, label); }
    static void Fixture()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../assets"));
        Directory.CreateDirectory(Path.Combine(root, "players"));
        byte[] manifest = "{\"schema\":\"p24-player-profiles-v1\",\"profiles\":[]}"u8.ToArray();
        File.WriteAllBytes(Path.Combine(root, "players/manifest.json"), manifest);
        File.WriteAllText(Path.Combine(root, "integrity.json"), JsonSerializer.Serialize(new { files = new[] {
            new { path = "players/manifest.json", size = manifest.Length, sha256 = Convert.ToHexString(SHA256.HashData(manifest)) } } }));
    }
    public static void Run(string output, Action<bool,string> report)
    {
        Passed.Clear();
        string prior=AppContext.BaseDirectory;
        string root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(output)!,"attack-skill-fixture"));
        Directory.CreateDirectory(root);
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(root,"build/checks")+Path.DirectorySeparatorChar);
        try {
        Fixture();
        foreach (byte action in new byte[] { 1, 18, 19, 21, 255 })
        foreach (ushort gfx in new ushort[] { 66, 167, 221, 3860, 2716, 65535 })
        foreach (byte flight in new byte[] { 0, 127 })
        {
            var bytes = Attack(action, gfx, flight); var p = AttackSkillPackets.DecodeAttack(bytes);
            Check(p.Action == action && p.Gfx == gfx && p.IsArrow == (flight == 127) && p.Sequence == 338 &&
                p.DamageWord == 65535 && p.TargetX == 32603 && p.SourceY == 32940, $"decode S30 {action}/{gfx}/{flight}");
            var w = Seed(); w.Receive(bytes, "offline");
            Check(w.Actor!.Direction == 3 && w.OwnCombat?.Action == action, "S30 actor action/heading");
            Check(flight == 0 ? w.MagicEffects is [{ Gfx: var g, Action: var a }] && g == gfx && a == action && w.AttackReports == 0
                : w.LastProjectile?.Gfx == gfx && w.LastAttack?.Action == action && w.MagicEffectCount == 0 && w.AttackReports == 1,
                "S30 format dispatch (including sequence 338 magic)");
            Check(w.Status!.Hp == 100 && w.Objects[0].HpPercent == 75, "S30 word never deducts HP");
            if (flight == 127) Check(w.Projectiles.Single().Gfx == gfx, "all projectile gfx enter compositor queue");
        }
        foreach (uint caster in new uint[] { 0, 999 })
        {
            var w = Seed(); w.Receive(Attack(caster: caster, target: 0), "offline");
            Check(w.OwnCombat == null && w.UnknownObjectUpdates == 0 && w.MagicEffects.Single().Caster == caster &&
                w.MagicEffects.Single().TargetX == 32603, "no motion/unknown caster and suppressed target keep coordinates");
        }
        foreach (bool extended in new[] { false, true })
        foreach (byte type in new byte[] { 0, 8 })
        {
            (uint, ushort, uint)[] targets = [(456,32,70000), (0,32,0), (123,0,0), (457,0,int.MaxValue), (999,32,1)];
            var bytes = Range(targets, extended, type); var p = AttackSkillPackets.DecodeRange(bytes);
            Check(p.Targets.Count == 5 && p.Type == type && p.Heading == 6 && p.Sequence == 9000001 &&
                p.Targets[0].DisplayDamage == (extended ? 70000u : null) && p.Targets[1].DisplayDamage == null &&
                p.Targets[3].DisplayDamage == (extended ? (uint)int.MaxValue : null), "S42 typed fields and unsigned display damage");
            var w = Seed(); w.Receive(bytes, "offline");
            // SV#4 (#617): the caster turns toward the first target (456 at 32603,32942 -> 3), not packet heading 6.
            Check(w.LastRangeSkill!.Targets.SequenceEqual(p.Targets) && w.MagicEffects.Length == 1 && w.RangeTargets(w.MagicEffects[0].Number).Count == 5 && w.Actor!.Direction == 3 &&
                w.OwnCombat?.Action == 19 && w.Objects[0].Combat == null && w.Objects[1].Combat == null,
                "S42 one effect carrying the list, caster motion, no hit reaction on receipt (skills-visual §3)");
            Check(w.Status!.Hp == 100 && w.Actor!.HpPercent == 50 && w.Objects[0].HpPercent == 75, "S42 flag/damage never deducts HP");
            w.Receive(Packet(b => { b.Write((byte)225); b.Write(90u); b.Write(200u); }), "offline");
            foreach (uint id in new uint[] { 123, 456 }) w.Receive(Packet(b => { b.Write((byte)237); b.Write(id); b.Write((byte)45); }), "offline");
            w.Receive(bytes, "offline");
            Check(w.Status.Hp == 90 && w.Actor!.HpPercent == 45 && w.Objects[0].HpPercent == 45, "authoritative S225/S237 survive repeated range packet");
            w.Receive(LoginPackets.Pad([206, 0, 0, 0]), "offline");
            Check(w.LastRangeSkill == null && w.MagicEffects.Length == 0, "map clears range state/effects");
        }
        foreach (bool extended in new[] { false, true })
        foreach (var targets in new (uint, ushort, uint)[][] { [], [(0,32,0)], [(0,0,0),(456,32,0)], [(456,32,0),(0,0,0)] })
        {
            var w = Seed(); w.Receive(Range(targets, extended), "offline");
            Check(w.LastRangeSkill!.Targets.Count == targets.Length, "S42 empty/zero/extension boundary");
            w.Reset(); Check(w.LastRangeSkill == null, "reset clears range state");
        }
        var collision = AttackSkillPackets.DecodeRange(Range([(456,32,0), (0xD14A6E3F,0,0), (457,32,0)]));
        Check(collision.Targets[1].ObjectId == 0xD14A6E3F && collision.Targets.All(t => t.DisplayDamage == null), "extension magic may be legal target ID");
        var incoming = Seed(); incoming.Receive(Attack(21,66,127,caster:456,target:123), "offline");
        Check(incoming.LastIncomingAttack?.Action == 21 && incoming.Objects[0].Combat?.Action == 21 &&
            incoming.OwnCombat == null && incoming.Status!.Hp == 100, "incoming arrow retains action, defers visual impact and HP");
        foreach (var bytes in new[] { Attack(), Attack(21,2716,127), Range([(456,32,2),(0,0,0),(457,32,90000)], true), Range([(456,32,0),(457,0,0)]) })
        {
            for (int n = 0; n < bytes.Length; n++)
            {
                if (bytes[0] == 30 && n == 24)
                {
                    var w = Seed(); w.Receive(bytes[..n], "offline");
                    Check(w.AttackReports == 0 && w.MagicEffectCount == 0 && w.OwnCombat == null,
                        "unsupported 24-byte layout has no state mutation");
                    continue;
                }
                Reject(bytes[..n], $"truncated S{bytes[0]} length {n}");
            }
            // The three-target extended vector is exactly 56 bytes, without padding.
            if (bytes.Length != 56) MutateReject(bytes, bytes.Length - 1, 1, "nonzero padding");
        }
        MutateReject(Attack(), 12, 8, "S30 heading");
        MutateReject(Attack(), 19, 6, "unknown flight");
        MutateReject(Attack(flight: 127), 13, 0, "arrow marker");
        MutateReject(Attack(), 28, 1, "S30 reserved");
        MutateReject(Attack(), 32, 1, "S30 tail");
        var range = Range([(456,32,1),(457,0,0)], true);
        foreach (var bad in new[] { (10,(byte)8), (17,(byte)6), (18,(byte)1), (20,(byte)255), (26,(byte)1), (28,(byte)0), (35,(byte)128), (42,(byte)0) })
            MutateReject(range, bad.Item1, bad.Item2, "S42 malformed offset " + bad.Item1);
        // Extended first target followed by a standard second target is not a legal serializer layout.
        var mixed = range[..42].Concat(range[50..]).ToArray(); Reject(mixed, "mixed extensions");
        var melee = Packet(b => { b.Write((byte)30); b.Write((byte)18); b.Write(123u); b.Write(456u); b.Write((ushort)10); b.Write((byte)2); b.Write(0u); b.Write((byte)0); });
        var m = Seed(); m.Receive(melee, "offline"); Check(m.AttackReports == 1 && m.MagicEffectCount == 0 && m.Objects[0].Combat?.Action == 2, "24-byte melee action18 not stolen by magic");
        Rendering(root);
        SkillEffectTiming(root);
        foreach(string label in Passed)report(true,label);
        }finally{AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",prior);}
    }
}
