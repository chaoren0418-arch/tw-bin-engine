namespace P06;

// 與畫面共用 appearance / ActionCadence / MagicMotion，計時更新時讀取權威實例。
public sealed class CombatSfxScene(string assetsRoot)
{
    readonly ObjectAssets objects = new(assetsRoot);
    readonly Dictionary<int, ObjectAppearance> magic = [];
    readonly Dictionary<(ObjectAppearance, string, int, ActionCadence?, bool, bool), SoundSchedule> schedules = [];
    readonly Dictionary<long, (int Direction, long Release, long Impact)> flights = [];
    readonly HashSet<long> cancelled = [];
    int epoch = -1;
    ObjectAppearance Magic(int gfx)
    {
        if (!magic.TryGetValue(gfx, out var appearance))
            magic[gfx] = appearance = new(Path.Combine(assetsRoot, $"objects/magic-{gfx}.json"));
        return appearance;
    }
    SoundSchedule Schedule(ObjectAppearance appearance, string action, int direction, ActionCadence? timing = null, bool pure = false, bool flight = false)
    {
        var key = (appearance, action, direction, timing, pure, flight);
        if (!schedules.TryGetValue(key, out var schedule))
            schedules[key] = schedule = SoundSchedule.Create(appearance, action, direction, timing, pure, flight);
        return schedule;
    }
    static (int Gfx, int Weapon)? Owner(WorldSession session, uint id)
    {
        if (session.Actor?.ObjectId == id)
            return session.OwnDead || session.SelfAppearancePending ? null : (session.Actor.Gfx, session.Actor.Weapon);
        var other = session.Objects.FirstOrDefault(o => o.ObjectId == id);
        return other == null || other.Dead || other.AppearancePending ? null : (other.Gfx, other.Action);
    }
    void AddAction(List<CombatSoundInstance> result, uint id, int gfx, int weapon, int direction, CombatAnimation? animation)
    {
        if (animation is not { Predicted: false, Action: 1 or 2 } || weapon == 20 && animation.Action == 1) return;
        if (animation.Timing is {} timing && !timing.Matches(gfx, weapon)) return;
        var appearance = objects.Get(gfx, weapon);
        string action = animation.Action == 1 ? "attack" : "damage";
        if (appearance == null || !appearance.Supports(action, direction)) return;
        var schedule = Schedule(appearance, action, direction, animation.Timing);
        result.Add(new($"actor/{id}", animation.Instance, id, gfx, weapon, animation.At,
            animation.At + (long)Math.Ceiling(schedule.Duration), schedule));
    }
    public CombatSoundInstance[] Snapshot(WorldSession session, long now)
    {
        if (epoch != session.WorldEpoch) { flights.Clear(); cancelled.Clear(); epoch = session.WorldEpoch; }
        var result = new List<CombatSoundInstance>();
        if (session.Actor is {} actor && Owner(session, actor.ObjectId) != null)
            AddAction(result, actor.ObjectId, actor.Gfx, actor.Weapon, actor.Direction, session.OwnCombat);
        foreach (var other in session.Objects)
            if (Owner(session, other.ObjectId) != null && NormalSpeed.Supported(other.Speed, other.Brave))
                AddAction(result, other.ObjectId, other.Gfx, other.Action, other.Direction, other.Combat);

        var effects = session.MagicEffects;
        cancelled.RemoveWhere(n => !effects.Any(e => e.Number == n));
        foreach (long number in flights.Keys.Where(n => !effects.Any(e => e.Number == n)).ToArray()) flights.Remove(number);
        foreach (var effect in effects)
        {
            if (cancelled.Contains(effect.Number)) continue;
            var owner = Owner(session, effect.Caster);
            if (owner == null || owner.Value.Gfx != effect.CasterGfx ||
                effect.CasterWeapon is int weapon && owner.Value.Weapon != weapon ||
                effect.Target != 0 && Owner(session, effect.Target) == null)
            {
                cancelled.Add(effect.Number);
                continue;
            }
            int gfx = effect.Gfx, direction = 0;
            long start = effect.At, end;
            string action;
            SoundSchedule schedule;
            if (gfx == 167)
            {
                long release = (effect.ActionAt ?? effect.At) + (effect.Timing?.ReleaseMs ?? MagicRules.ReleaseFor(effect.CasterGfx));
                if (now < release) continue;
                if (!flights.TryGetValue(effect.Number, out var flight))
                {
                    var target = session.Objects.FirstOrDefault(o => o.ObjectId == effect.Target);
                    int x = target?.X ?? effect.TargetX, y = target?.Y ?? effect.TargetY;
                    long impact = release;
                    while (impact < release + 6000 && !MagicMotion.At(effect, x, y, release, impact).Arrived) impact += 60;
                    flight = (MagicMotion.At(effect, x, y, release, release).Direction, release, impact);
                    flights[effect.Number] = flight;
                }
                if (now < flight.Impact)
                {
                    action = "fly"; direction = flight.Direction; start = flight.Release; end = flight.Impact;
                    schedule = Schedule(Magic(167), action, direction, flight: true);
                }
                else
                {
                    gfx = 219; action = "impact"; start = flight.Impact;
                    schedule = Schedule(Magic(219), action, 0, pure: true);
                    end = start + (long)Math.Ceiling(schedule.Duration);
                }
            }
            else
            {
                if (gfx is not (11722 or 221 or 2165)) continue;
                action = gfx == 11722 ? "heal" : "effect";
                schedule = Schedule(Magic(gfx), action, 0, pure: true);
                end = start + (long)Math.Ceiling(schedule.Duration);
            }
            result.Add(new($"magic/{effect.Number}/{action}", effect.Number, effect.Caster,
                owner.Value.Gfx, owner.Value.Weapon, start, end, schedule));
        }
        return result.ToArray();
    }
}
