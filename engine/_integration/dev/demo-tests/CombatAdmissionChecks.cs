using P06;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

partial class Checks
{
    [CheckGroup("combat-admission",Order=1)]
    static async Task CombatAdmission()
    {
        CombatAdmissionFixture();
        foreach (byte weapon in new byte[] { 0, 4, 46, 50 })
        foreach (bool pointer in new[] { false, true })
        foreach (int distance in new[] { 1, 2, 3, 10, 19 })
        {
            using var c = Seed();
            Feed(c, Weapon(weapon));
            Feed(c, Pack(456, (ushort)(32600 + distance), (ushort)(32940 + distance)));
            int sends = 0;
            Sender(c, (bytes, valid, admit) =>
            {
                if (!valid()) return Task.FromResult(false);
                Check(AcceptsMelee(bytes, c.World.Actor!, c.World.Target!), "server contract accepts emitted C229");
                Check(bytes.SequenceEqual(new byte[] { 229, 200, 1, 0, 0,
                    (byte)((32600 + distance) & 255), (byte)((32600 + distance) >> 8),
                    (byte)((32940 + distance) & 255), (byte)((32940 + distance) >> 8), 0, 0, 0, 0, 0, 0, 0 }),
                    "C229 exact target/coordinates/padding");
                sends++;
                admit();
                return Task.FromResult(true);
            });
            var controller = new PlayerPointerController(c, () => true, () => false, _ => true);
            bool sent = pointer ? await controller.ActAsync(456, false) : await c.Attack();
            Check(sent == (distance <= 2) && sends == (sent ? 1 : 0), $"melee boundary weapon={weapon} pointer={pointer} distance={distance}");
            if (!sent)
            {
                Check(c.World.OwnCombat == null && !c.CombatBusy && c.World.CombatNotice.Contains("2 格內"), "range refusal has feedback and no predicted action/cooldown");
                // Moving closer after refusal must immediately allow an explicit retry.
                Feed(c, Pack(456, 32601, 32940));
                Sender(c, (bytes, valid, admit) => { if (!valid()) return Task.FromResult(false); admit(); return Task.FromResult(true); });
                Check(await c.Attack(), "fresh nearby retry accepted");
            }
        }
        foreach (int distance in new[] { 1, 10, 11, 19 })
        {
            using var c = Seed(); LearnBolt(c);
            Feed(c, Pack(456, (ushort)(32600 + distance), (ushort)(32940 - distance)));
            int sends = 0;
            Sender(c, (bytes, valid, admit) =>
            {
                if (!valid()) return Task.FromResult(false);
                Check(AcceptsBolt(bytes, c.World.Actor!, c.World.Target!), "server contract accepts emitted C6");
                sends++; admit(); return Task.FromResult(true);
            });
            var before = c.World.Status;
            bool sent = await c.CastSelectedMagic();
            Check(sent == (distance <= 10) && sends == (sent ? 1 : 0), "bolt range boundary " + distance);
            Check(c.World.Status == before, "no predicted HP/MP deduction");
            if (!sent) Check(c.World.OwnCombat == null && !c.MagicBusy && c.World.MagicNotice.Contains("10 格內"), "bolt range refusal has feedback and no cooldown");
        }
        foreach (bool magic in new[] { false, true })
        foreach (string change in new[] { "actor", "target", "remove", "death", "selection", "map", "disconnect" })
        {
            using var c = Seed(); if (magic) LearnBolt(c);
            var release = new TaskCompletionSource<bool>(); int sends = 0;
            Sender(c, async (bytes, valid, admit) =>
            {
                await release.Task;
                if (!valid()) return false;
                sends++; admit(); return true;
            });
            var pending = magic ? c.CastSelectedMagic() : c.Attack();
            Check(!pending.IsCompleted, "request queued before race " + change);
            switch (change)
            {
                // Preserve correction/selection revisions to exercise the actual coordinate fences.
                case "actor": Call(c.World, "Predict", 32620, 32940, 2, true); break;
                case "target": Feed(c, Move(456, 32620, 32940, 2)); break;
                case "remove": Feed(c, LoginPackets.Pad([120, ..BitConverter.GetBytes(456u)])); break;
                case "death": Feed(c, Death(456)); break;
                case "selection": c.World.SelectTarget(null); break;
                case "map": Feed(c, MapPacket(1)); break;
                case "disconnect": c.Cancel(); break;
            }
            release.SetResult(true);
            Check(!await pending && sends == 0, "stale queued combat sends zero packets " + magic + "/" + change);
        }
        using (var c = Seed())
        {
            byte[] valid = CombatRules.Attack(c.World.Target!);
            Check(AcceptsMelee(valid, c.World.Actor!, c.World.Target!), "known C229 accepted by independent contract model");
            var wrong = valid.ToArray(); wrong[1]++;
            Check(!AcceptsMelee(wrong, c.World.Actor!, c.World.Target!), "wrong object id rejected by contract model");
            var zero = valid.ToArray(); zero[5] = zero[6] = 0;
            Check(!AcceptsMelee(zero, c.World.Actor!, c.World.Target!), "zero coordinate rejected by contract model");
            Check(!AcceptsMelee(valid[..8], c.World.Actor!, c.World.Target!), "truncated C229 rejected by contract model");
            Check(!AcceptsMelee(valid, c.World.Actor! with { X = 32620 }, c.World.Target!), "unchanged packet cannot bypass server actor geometry");
            Sender(c, (bytes, check, admit) => { if (!check()) return Task.FromResult(false); admit(); return Task.FromResult(true); });
            Check(await c.Attack() && !await c.Attack(), "accepted attack keeps shared cooldown");
            Feed(c, AttackReply());
            Check(c.World.LastAttack is { Target: 456, DamageWord: 10 }, "S30 target/damage parsed by product");
            Check(c.World.Status!.Hp == 100, "S30 does not invent own HP loss");
        }
        Check(CombatRules.InRange(32600, 32940, 32610, 32940, 20), "bow keeps its separate admission behavior");
        Check(MagicRules.InRange(1, 32600, 32940, 32619, 32940), "heal keeps its separate target rules");
        await KillAndLoot(); await LearnAndCast(); await Ammunition();
    }

    static void LearnBolt(LoginConnection c)
    {
        var masks = new byte[32]; masks[0] = 164; masks[1] = 32; masks[2] = 8;
        Feed(c, masks);
        Check(c.World.SelectMagic(4) && c.World.SelectMagicTarget(456), "select learned bolt and target");
    }

    // Independent, deliberately partial contract model derived from server source.
    // World identity, geometry and wire fields are tested. RNG, LOS, DB and live server state are outside this model.
    static bool AcceptsMelee(byte[] bytes, OwnActor actor, WorldObject target)
    {
        if (bytes.Length < 9 || bytes[0] != 229) return false;
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1));
        int x = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(5));
        int y = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(7));
        return id == target.ObjectId && !target.Dead && x != 0 && y != 0
            && Math.Max(Math.Abs(actor.X - target.X), Math.Abs(actor.Y - target.Y)) <= 1 + 1;
    }

    static bool AcceptsBolt(byte[] bytes, OwnActor actor, WorldObject target)
    {
        if (bytes.Length < 11 || bytes[0] != 6 || (bytes[1] << 3) + bytes[2] + 1 != 4) return false;
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(3)) == target.ObjectId
            && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(7)) == target.X
            && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(9)) == target.Y
            && !target.Dead && Math.Max(Math.Abs(actor.X - target.X), Math.Abs(actor.Y - target.Y)) <= 10;
    }

    [CheckGroup("cast-cadence",Order=1),CheckGroup("duel",Order=1)]
    static void CombatAdmissionFixture()
    {
        string root = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../..")), "scratch", "issue-173", "synthetic-combat");
        string assets = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(assets, "players"));
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", Path.Combine(root, "build", "offline-checks") + Path.DirectorySeparatorChar);
        // Authored timing metadata only, no game pixels/maps or copied server source.
        var actions = new { walk = 640, attack = 640, spellDir = 1320, spellNoDir = 1480 };
        var profile = JsonSerializer.SerializeToUtf8Bytes(new
        {
            actionMs = actions,
            sequences = new[] { "attack", "spellDir", "spellNoDir" }.Select(action => new
            {
                action, direction = 0, sequence = new[] { new { milliseconds = 640, events = "!" } }
            })
        });
        File.WriteAllBytes(Path.Combine(assets, "players/test-only.json"), profile);
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "p24-player-profiles-v1",
            profiles = new[] { 0, 4, 20, 46, 50 }.Select(weapon => new
            {
                gfx = 0, weapon, path = "players/test-only.json", sha256 = Convert.ToHexString(SHA256.HashData(profile)),
                stepMs = 640, serverActionMs = actions, castReleaseMs = new { spellDir = 320 }, projectileReleaseMs = 320
            })
        });
        File.WriteAllBytes(Path.Combine(assets, "players/manifest.json"), manifest);
        File.WriteAllBytes(Path.Combine(assets, "integrity.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            files = new[] { new { path = "players/manifest.json", size = manifest.Length, sha256 = Convert.ToHexString(SHA256.HashData(manifest)) } }
        }));
    }
}
