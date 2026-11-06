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

    void ReceiveRangeMagic(RangeSkillPacket p)
    {
        ApplySkillCaster(p.Caster, p.Action, p.Heading, p.Targets.FirstOrDefault()?.ObjectId);
        var animation = ActionFor(p.Caster);
        long at = ActionClock();
        foreach (var target in p.Targets)
        {
            // S42 has no target coordinates. Never infer an invisible/suppressed target location.
            var other = Objects.FirstOrDefault(o => o.ObjectId == target.ObjectId);
            bool own = Actor?.ObjectId == target.ObjectId;
            if (own || other != null)
            {
                ushort x = own ? Actor!.X : other!.X, y = own ? Actor!.Y : other!.Y;
                AddMagicEffect(new(++MagicEffectCount, p.Caster, target.ObjectId, p.Gfx, p.Action,
                    target.HitFlag, p.Heading, p.X, p.Y, x, y, at, p.Sequence)
                    { Timing = animation?.Timing, ActionAt = animation?.At });
                if (target.HitFlag != 0) AnimateCombat(target.ObjectId, 2);
            }
        }
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
