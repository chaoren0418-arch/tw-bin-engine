namespace P06;

public sealed record AttackSkillPacket(byte Action, uint Caster, uint Target, ushort DamageWord,
    byte Heading, uint Sequence, ushort Gfx, byte Flight, ushort SourceX, ushort SourceY,
    ushort TargetX, ushort TargetY)
{
    public bool IsArrow => Flight == 127;
    // FU5§3: TW16 S_ATTACK copies the byte after gfx into the effect's `+9` (light flag) for kind 0/1/10.
    // Decoding only accepts 0 for magic (3.81 S_UseAttackSkill writes 0), so S30 magic never lights by flag;
    // kind 2 (Lightning) lights regardless (SkillLight).
    public byte LightFlag => IsArrow ? (byte)0 : Flight;
}

public sealed record RangeSkillTarget(uint ObjectId, ushort HitFlag, uint? DisplayDamage);
public sealed record RangeSkillPacket(byte Action, uint Caster, ushort X, ushort Y, byte Heading,
    uint Sequence, ushort Gfx, byte Type, IReadOnlyList<RangeSkillTarget> Targets)
{
    // FU5§3: S_ATTACK_MANY field 8 (`cdhhcdhccs`, the `c` after gfx) is the effect's `+9`; 3.81 L1SkillUse sends
    // TYPE_DIR = 8 for directional area magic (lights) and TYPE_NODIR = 0 (does not). Kept as MagicEffect.RangeType.
    public byte LightFlag => Type;
}

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
        if (QueueIfCasterBusy(p.Caster, p, null)) return;
        ApplyAttackMagic(p);
    }

    void ApplyAttackMagic(AttackSkillPacket p)
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
        if (QueueIfCasterBusy(p.Caster, null, p)) return;
        ApplyRangeMagic(p);
    }

    void ApplyRangeMagic(RangeSkillPacket p)
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
        // SV#4 (skills-visual.md §2.1, sub_A3D050/sub_A3DC10 -> sub_A40EC0): turn toward the target's
        // (S42: first target's) CURRENT position when that object exists; otherwise keep the packet heading.
        var aim = target is uint t && t != caster ? VisibleTargetPosition(t) : null;
        if (Actor?.ObjectId == caster) Actor = Actor with { Direction = SkillFacing.Toward(Actor.X, Actor.Y, aim, heading) };
        else if (objects.TryGetValue(caster, out var o)) UpdateObject(caster, x => x with { Direction = SkillFacing.Toward(o.X, o.Y, aim, heading) });
        else return;
        AnimateCombat(caster, action, target: target);
    }
}

// R5-C（T308／#735，gap-matrix 14.6；skills-visual.md §2.1、§3 第 1 步）：施法者正在做動作時整包排隊。
// 已驗證（本任務以 Capstone 唯讀反組譯 sub_92FE40／S_ATTACK_MANY，.text FO＝VA − 0x400C00）：0x930759 呼叫
// sub_A3EBF0（施法者忙碌判斷），忙碌時 0x930789 走 sub_A3C710（排入佇列 sub_A40250），否則 0x9307B4 走
// sub_A3DC10（立即掛到施法者並開始動作）。S_ATTACK 同理（sub_A3C5A0）。佇列等目前動作結束才播，所以整包
// （目標清單、特效、動作）都延後，不會打斷目前動作。
// 引擎取捨：
// - 「忙碌」＝施法者目前的 CombatAnimation 有已知長度（Timing，動作 1／18／19 有玩家外型資料時）而且還沒播完；
//   長度不明的動作（受擊 2、沒有外型資料的 NPC）不排隊（推定：不知道何時結束就不擋）。
// - 同一施法者已有排隊中的封包時，新封包一律排在後面，保持收包順序。
// - 施法者離開畫面（找不到物件）時立即播放（§2.1：施法者不在畫面上時特效直接開始）。
// - 最多等 MaxWaitMs，超過就照收包順序直接播（防止長度資料錯誤時永遠卡住；原版沒有這個上限）。
// - 佇列由 Renderer.ComposeMagic 每格推進（UI 執行緒，與收包同一執行緒），也會在下一包到達時推進。
// R7-A（T340／#803，gap-matrix 15.6）：近戰與箭矢的 S30（Combat.cs）也走同一個佇列（S_ATTACK 的 sub_A3C5A0 → sub_A40250）。
// - 只排「其他角色」的近戰／箭矢。自己角色的近戰／箭矢 S30 一律立即套用（引擎取捨，推定）：
//   自己的攻擊已由本地節流（nextCombatAt）控制，而且送出時有預測動作（PredictAction），那一包 S30 就是預測動作的
//   原服確認；排隊會讓確認延後、動作重播，也會讓 ApplyCombatAcknowledgement 拿不到回報。原版是否本地預測沒查。
//   自己的法術 S30／S42 維持 #735 的做法（照排）。
public sealed record QueuedSkillCast(long Number, uint Caster, long ReceivedAt, AttackSkillPacket? Attack, RangeSkillPacket? Range,
    MeleeAttackPacket? Melee = null)
{
    public ushort Gfx => Attack?.Gfx ?? Range?.Gfx ?? 0;
    /// <summary>"melee"／"arrow"／"magic"（S30 法術）／"range"（S42）。</summary>
    public string Kind => Melee != null ? "melee" : Attack is { IsArrow: true } ? "arrow" : Attack != null ? "magic" : "range";
}

public static class SkillCastQueue
{
    public const int MaxWaitMs = 5000, MaxQueued = 16;
    /// <summary>目前動作的結束時刻；null＝沒有在做動作，或動作長度不明（不排隊）。</summary>
    public static long? BusyUntil(CombatAnimation? current, long now) =>
        current is { Timing: { } t } && now < current.At + t.AnimationMs ? current.At + t.AnimationMs : null;
}

public sealed partial class WorldSession
{
    readonly List<QueuedSkillCast> queuedSkillCasts = [];
    long queuedSkillNumber;
    int skillQueueEpoch = -1;
    /// <summary>排隊中的 S30／S42 法術封包（收包順序）。</summary>
    public QueuedSkillCast[] QueuedSkillCasts { get { SkillQueueEpoch(); return [.. queuedSkillCasts]; } }
    /// <summary>本世界累計排過隊的封包數。</summary>
    public int SkillCastsQueued { get; private set; }

    void SkillQueueEpoch()
    {
        if (skillQueueEpoch == WorldEpoch) return;
        queuedSkillCasts.Clear(); skillQueueEpoch = WorldEpoch;
    }

    bool CasterPresent(uint caster) => Actor?.ObjectId == caster || objects.ContainsKey(caster);

    bool QueueIfCasterBusy(uint caster, AttackSkillPacket? attack, RangeSkillPacket? range, MeleeAttackPacket? melee = null)
    {
        long now = ActionClock();
        RunQueuedSkillCasts(now);
        // withCastMotion=false（caster 0）沒有施法者動作，不排隊。
        if (caster == 0 || !CasterPresent(caster)) return false;
        var current = ActionFor(caster);
        // R7-A：自己角色的近戰／箭矢不排隊（見上方說明）。
        if ((melee != null || attack is { IsArrow: true }) && Actor?.ObjectId == caster) return false;
        bool behind = queuedSkillCasts.Any(q => q.Caster == caster);
        if (!behind && SkillCastQueue.BusyUntil(current, now) == null) return false;
        if (queuedSkillCasts.Count >= SkillCastQueue.MaxQueued) ApplyQueued(0);
        var queued = new QueuedSkillCast(++queuedSkillNumber, caster, now, attack, range, melee);
        queuedSkillCasts.Add(queued);
        SkillCastsQueued++; MagicRevision++;
        if (melee != null || attack is { IsArrow: true })
            CombatNotice = $"原服攻擊：{caster} 動作未結束，{(melee != null ? "近戰" : "箭矢")} S30 排隊等目前動作播完";
        else
            MagicNotice = $"原服法術：施法者 {caster} 動作未結束，特效 {queued.Gfx} 排隊等目前動作播完";
        return true;
    }

    void ApplyQueued(int index)
    {
        var q = queuedSkillCasts[index]; queuedSkillCasts.RemoveAt(index);
        if (q.Melee is { } m) ApplyMeleeAttack(m);
        else if (q.Attack is { IsArrow: true } arrow) ApplyArrowAttack(arrow);
        else if (q.Attack is { } a) ApplyAttackMagic(a);
        else ApplyRangeMagic(q.Range!);
    }

    /// <summary>把動作已結束（或已等太久、施法者已離開）的排隊封包依收包順序播出；回傳播出的數量。</summary>
    internal int RunQueuedSkillCasts(long now)
    {
        SkillQueueEpoch();
        int applied = 0;
        for (int i = 0; i < queuedSkillCasts.Count;)
        {
            var q = queuedSkillCasts[i];
            // 同一施法者只看最前面那一包（前面還在等，後面的也要等）。
            if (queuedSkillCasts.Take(i).Any(p => p.Caster == q.Caster)) { i++; continue; }
            bool due = now - q.ReceivedAt >= SkillCastQueue.MaxWaitMs || !CasterPresent(q.Caster)
                || SkillCastQueue.BusyUntil(ActionFor(q.Caster), now) == null;
            if (!due) { i++; continue; }
            ApplyQueued(i); applied++;
            i = 0; // 播出後施法者可能又忙碌（新的動作），從頭重新判斷
        }
        return applied;
    }

    [WorldReset]
    void ResetSkillCastQueue() { queuedSkillCasts.Clear(); SkillCastsQueued = 0; skillQueueEpoch = WorldEpoch; }

    [WorldSnapshot("skillCastQueue")]
    object? SkillCastQueueSnapshot() => new
    {
        queued = QueuedSkillCasts.Select(q => new { number = q.Number, caster = q.Caster, receivedAt = q.ReceivedAt, gfx = q.Gfx, range = q.Range != null, kind = q.Kind }).ToArray(),
        total = SkillCastsQueued,
    };
}

public static class SkillFacing
{
    // Same direction function as flights (F1A340); a target on the caster's own tile keeps the packet heading.
    public static byte Toward(int x, int y, (ushort X, ushort Y)? target, byte packet) =>
        target is { } p ? (byte)ProjectileMotion.Direction(x, y, p.X, p.Y, packet) : packet;
}
