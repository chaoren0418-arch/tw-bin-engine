namespace P06;

public sealed record AttackSkillPacket(byte Action, uint Caster, uint Target, ushort DamageWord,
    byte Heading, uint Sequence, ushort Gfx, byte Flight, ushort SourceX, ushort SourceY,
    ushort TargetX, ushort TargetY)
{
    public bool IsArrow => Flight == 127;
}

public sealed record RangeSkillTarget(uint ObjectId, ushort HitFlag, uint? DisplayDamage);
public sealed record RangeSkillPacket(byte Action, uint Caster, ushort X, ushort Y, byte Heading,
    uint Sequence, ushort Gfx, byte Type, IReadOnlyList<RangeSkillTarget> Targets);

// S_UseAttackSkill / S_UseArrowSkill / S_RangeSkill; see docs/reference/server-protocol.md.
// Decode completely before applying state. Damage words/flags are NOT absolute HP values.
public static class AttackSkillPackets
{
    const uint DamageExtensionMagic = 0xD14A6E3F;

    public static AttackSkillPacket DecodeAttack(byte[] plain)
    {
        if (plain.Length != 40) throw new ProtocolException("attack skill envelope length");
        var r = new LoginReader(plain);
        if (r.C() != 30) throw new ProtocolException("attack skill opcode");
        var p = new AttackSkillPacket(r.C(), r.D(), r.D(), r.H(), r.C(), r.D(), r.H(),
            r.C(), r.H(), r.H(), r.H(), r.H());
        uint reserved = r.D(); byte tail = r.C(); r.Finish();
        if (p.Heading > 7 || reserved != 0 || tail != 0 ||
            p.Flight is not (0 or 127) || p.IsArrow && p.Sequence != 338)
            throw new ProtocolException("invalid attack skill envelope");
        return p;
    }

    public static RangeSkillPacket DecodeRange(byte[] plain)
    {
        if (plain.Length < 24) throw new ProtocolException("range skill envelope length");
        var r = new LoginReader(plain);
        if (r.C() != 42) throw new ProtocolException("range skill opcode");
        byte action = r.C(); uint caster = r.D(); ushort x = r.H(), y = r.H();
        byte heading = r.C(); uint sequence = r.D(); ushort gfx = r.H(); byte type = r.C();
        ushort reserved = r.H(), count = r.H();
        if (heading > 7 || type is not (0 or 8) || reserved != 0 || count > r.Remaining / 6)
            throw new ProtocolException("invalid range skill header/count");

        // includeDamage is a packet-wide server option, but target 0 never has an extension.
        // Try both complete layouts, including padding. Merely peeking for the magic number
        // could consume a legal next target ID (or accept a partially extended packet).
        RangeSkillTarget[] targets;
        try { targets = ReadTargets(plain, count, extended: false); }
        catch (ProtocolException) { targets = ReadTargets(plain, count, extended: true); }
        return new(action, caster, x, y, heading, sequence, gfx, type, Array.AsReadOnly(targets));
    }

    static RangeSkillTarget[] ReadTargets(byte[] plain, int count, bool extended)
    {
        var r = new LoginReader(plain);
        for (int i = 0; i < 22; i++) r.C();
        var targets = new RangeSkillTarget[count];
        for (int i = 0; i < count; i++)
        {
            uint id = r.D(); ushort hit = r.H(); uint? damage = null;
            if (hit is not (0 or 32)) throw new ProtocolException("invalid range hit flag");
            if (extended && id != 0)
            {
                if (r.D() != DamageExtensionMagic) throw new ProtocolException("range damage extension marker");
                damage = r.D();
                if (damage > int.MaxValue) throw new ProtocolException("negative range display damage");
            }
            targets[i] = new(id, hit, damage);
        }
        r.Finish();
        return targets;
    }
}

public sealed partial class WorldSession
{
    RangeSkillPacket? lastRangeSkill;
    int rangeSkillEpoch = -1;
    public RangeSkillPacket? LastRangeSkill => rangeSkillEpoch == WorldEpoch ? lastRangeSkill : null;

    void ReceiveAttackMagic(AttackSkillPacket p)
    {
        ApplySkillCaster(p.Caster, p.Action, p.Heading, p.Target);
        var animation = ActionFor(p.Caster);
        AddMagicEffect(new(++MagicEffectCount, p.Caster, p.Target, p.Gfx, p.Action, p.DamageWord,
            p.Heading, p.SourceX, p.SourceY, p.TargetX, p.TargetY, ActionClock(), p.Sequence)
            { Timing = animation?.Timing, ActionAt = animation?.At });
        MagicNotice = $"原服法術回報：特效 {p.Gfx}、動作 {p.Action}；傷害欄 {p.DamageWord}，HP 依原服回包更新";
    }

    // S42 target lists keyed by MagicEffect.Number. The renderer selects the 109 class and
    // therefore who is hit and where landings go (docs/reference/skills-visual.md §2.2–§3).
    readonly System.Collections.Concurrent.ConcurrentDictionary<long, RangeSkillTarget[]> rangeTargets = new();
    public IReadOnlyList<RangeSkillTarget> RangeTargets(long number) =>
        rangeTargets.TryGetValue(number, out var t) ? t : Array.Empty<RangeSkillTarget>();
    // Current position of a visible S42 target. S42 has no target coordinates: an invisible
    // or suppressed target never gets an inferred location.
    public (ushort X, ushort Y)? VisibleTargetPosition(uint id)
    {
        if (id == 0) return null;
        if (Actor?.ObjectId == id) return (Actor.X, Actor.Y);
        var other = Objects.FirstOrDefault(o => o.ObjectId == id);
        return other == null ? null : (other.X, other.Y);
    }

    void ReceiveRangeMagic(RangeSkillPacket p)
    {
        ApplySkillCaster(p.Caster, p.Action, p.Heading, p.Targets.FirstOrDefault()?.ObjectId);
        var animation = ActionFor(p.Caster);
        long at = ActionClock();
        // Original sub_A3DC10 attaches the whole list to ONE effect object, started at the
        // caster's `!` frame and placed at packet x,y (the caster). No hit reaction on receipt:
        // the effect (or the caster `!` frame when there is no effect) triggers it (SV#6/#7).
        var first = p.Targets.FirstOrDefault();
        uint firstId = first?.ObjectId ?? 0;
        var aim = VisibleTargetPosition(firstId) ?? (p.X, p.Y);
        AddMagicEffect(new(++MagicEffectCount, p.Caster, firstId, p.Gfx, p.Action, first?.HitFlag ?? 0,
            p.Heading, p.X, p.Y, aim.X, aim.Y, at, p.Sequence)
            { Timing = animation?.Timing, ActionAt = animation?.At, RangeType = p.Type });
        rangeTargets[MagicEffectCount] = p.Targets.ToArray();
        var live = magicEffects.Select(e => e.Number).ToHashSet();
        foreach (long stale in rangeTargets.Keys.Where(n => !live.Contains(n)).ToArray()) rangeTargets.TryRemove(stale, out _);
        lastRangeSkill = p; rangeSkillEpoch = WorldEpoch;
        MagicRevision++;
        MagicNotice = $"原服範圍法術：特效 {p.Gfx}、動作 {p.Action}、目標 {p.Targets.Count}；HP 依原服回包更新";
    }

    void ApplySkillCaster(uint caster, int action, byte heading, uint? target)
    {
        // withCastMotion=false intentionally serializes caster=0.
        if (caster == 0) return;
        if (Actor?.ObjectId == caster) Actor = Actor with { Direction = heading };
        else if (objects.ContainsKey(caster)) UpdateObject(caster, o => o with { Direction = heading });
        else return;
        AnimateCombat(caster, action, target: target);
    }
}
