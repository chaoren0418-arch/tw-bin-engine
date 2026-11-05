using P24.Audio;
using System.Text.RegularExpressions;

namespace P06;

public sealed record SoundFrame(int Index, double At, int[] Candidates);
public sealed record SoundSchedule(SoundFrame[] Frames, double Duration, bool Loop)
{
    // 產品政策，推測，待 #52 真機聽音核對。只取 [N，忽略 <、! 等尾綴。
    public static int[] Candidates(string suffix) => Regex.Matches(suffix, @"\[(\d+)")
        .Select(m => int.TryParse(m.Groups[1].Value, out int id) ? id : 0).Where(id => id > 0).ToArray();

    public static SoundSchedule Create(ObjectAppearance appearance, string action, int direction,
        ActionCadence? timing = null, bool pure = false, bool flight = false)
    {
        var sequence = appearance.Sequences[(action, direction)];
        var order = Enumerable.Range(0, sequence.Length).ToArray();
        if (pure || flight) order = order.Skip(1).Append(0).ToArray();
        bool milliseconds = sequence.All(s => s.Milliseconds != null);
        double totalTicks = sequence.Sum(s => s.Ticks), at = 0;
        var frames = new List<SoundFrame>();
        foreach (int index in order)
        {
            var step = sequence[index];
            double duration;
            if (flight) duration = 60; // 沿用 MagicMotion 的既有飛行步進。
            else if (timing?.Slow == true) duration = 2 * (step.Milliseconds ?? step.Ticks * 1000 / 24);
            else if (timing?.Haste == true) duration = ActionCadence.HasteFrameMs(step.Milliseconds ?? step.Ticks * 1000 / 24);
            else duration = milliseconds ? step.Milliseconds!.Value : appearance.Duration(action) * step.Ticks / totalTicks;
            // 零長度初始化列沒有可見幀停留，與 ObjectAppearance.At 一致。
            if (duration > 0) frames.Add(new(index, at, Candidates(step.Events)));
            at += duration;
        }
        return new(frames.ToArray(), at, flight || action == "breath");
    }
}

public sealed class SoundCursor(SoundSchedule schedule, Random random)
{
    long next;
    public void Advance(double elapsed, Action<int, int, long> dispatch)
    {
        if (elapsed < 0 || schedule.Duration <= 0 || schedule.Frames.Length == 0) return;
        while (schedule.Loop || next < schedule.Frames.Length)
        {
            long cycle = next / schedule.Frames.Length;
            var frame = schedule.Frames[next % schedule.Frames.Length];
            if (cycle * schedule.Duration + frame.At > elapsed) break;
            next++;
            // 產品政策，推測，待 #52 真機聽音核對。同幀多候選隨機擇一。
            if (frame.Candidates.Length > 0)
                dispatch(frame.Candidates[random.Next(frame.Candidates.Length)], frame.Index, cycle);
        }
    }
}

public sealed record CombatSoundInstance(string Key, long Instance, uint Actor, int Gfx, int Weapon,
    long StartedAt, long EndsAt, SoundSchedule Schedule);
public sealed record CombatSoundObservation(string Stage, string Key, int? SoundId, int? Frame, long? Cycle, AudioReply Reply);

// 主執行緒推進。非同步完成只讀 lease/control fence，音訊執行緒另有 owner fence。
public sealed class CombatSfxController(IAudioPlayback audio, Random random, Action<CombatSoundObservation> observe)
{
    sealed class Lease(CombatSoundInstance instance, AudioOwner owner, SoundCursor cursor)
    {
        internal readonly CombatSoundInstance Instance = instance;
        internal readonly AudioOwner Owner = owner;
        internal readonly SoundCursor Cursor = cursor;
        internal Task<AudioReply> Registration = null!;
        internal volatile bool Active = true;
    }
    readonly Dictionary<string, Lease> active = [];
    readonly Dictionary<string, (long Instance, long Expires)> consumed = [];
    readonly Stack<string> ownerSlots = [];
    int nextSlot;
    long generation, controlsRevision;
    int epoch = -1;
    AudioControls? controls;
    AudioReply? maintenance;
    bool closed;
    public Task WhenReleased => audio.WhenReleased;

    async Task Observe(string stage, string key, Task<AudioReply> operation, int? id = null, int? frame = null, long? cycle = null)
    {
        AudioReply reply;
        try { reply = await operation.ConfigureAwait(false); }
        catch (Exception ex) { reply = new(false, "TransportFailure", null, 0, [new("Exception", "wav", ex.Message)]); }
        observe(new(stage, key, id, frame, cycle, reply));
    }
    void Retire(string key)
    {
        var lease = active[key];
        lease.Active = false;
        active.Remove(key);
        _ = Observe("retire", key, audio.RetireAsync(lease.Owner));
        ownerSlots.Push(lease.Owner.Key);
    }
    async Task Play(Lease lease, int id, int frame, long cycle)
    {
        long revision = Interlocked.Read(ref controlsRevision);
        AudioReply registered;
        try { registered = await lease.Registration.ConfigureAwait(false); }
        catch (Exception ex)
        {
            observe(new("play", lease.Instance.Key, id, frame, cycle,
                new(false, "RegistrationFailed", null, 0, [new("Exception", "wav", ex.Message)])));
            return;
        }
        if (!registered.Success || !lease.Active || revision != Interlocked.Read(ref controlsRevision))
        {
            observe(new("play", lease.Instance.Key, id, frame, cycle,
                new(false, "CancelledBeforeDispatch", null, 0, [])));
            return;
        }
        await Observe("play", lease.Instance.Key, audio.PlayAsync(id, lease.Owner), id, frame, cycle).ConfigureAwait(false);
    }
    public void SetControls(double gain, bool muted, bool enabled)
    {
        if (closed) return;
        var next = new AudioControls(gain, muted, enabled);
        if (next == controls) return;
        controls = next;
        Interlocked.Increment(ref controlsRevision);
        _ = Observe("controls", "", audio.ConfigureAsync(gain, muted, enabled));
    }
    public void Pump(int worldEpoch, IEnumerable<CombatSoundInstance> instances, long now, bool enabled)
    {
        if (closed) return;
        if (epoch != worldEpoch)
        {
            foreach (string key in active.Keys.ToArray()) Retire(key);
            consumed.Clear();
            epoch = worldEpoch;
        }
        var current = instances.ToDictionary(i => i.Key);
        foreach (string key in active.Keys.ToArray())
        {
            var old = active[key].Instance;
            if (!enabled || !current.TryGetValue(key, out var value) || value.Instance != old.Instance ||
                value.Gfx != old.Gfx || value.Weapon != old.Weapon || now >= old.EndsAt) Retire(key);
        }
        foreach (string key in consumed.Keys.Where(k => !current.ContainsKey(k) && now > consumed[k].Expires).ToArray()) consumed.Remove(key);
        foreach (var instance in current.Values)
        {
            if (!active.TryGetValue(instance.Key, out var lease))
            {
                if (consumed.TryGetValue(instance.Key, out var prior) && prior.Instance >= instance.Instance) continue;
                if (now < instance.StartedAt) continue;
                consumed[instance.Key] = (instance.Instance, instance.EndsAt + 15000);
                if (!enabled || now >= instance.EndsAt) continue;
                string slot = ownerSlots.Count > 0 ? ownerSlots.Pop() : $"combat/owner/{++nextSlot}";
                lease = new(instance, new(slot, ++generation), new(instance.Schedule, random));
                active.Add(instance.Key, lease);
                lease.Registration = audio.RegisterAsync(lease.Owner);
                _ = Observe("register", instance.Key, lease.Registration);
            }
            lease.Cursor.Advance(now - instance.StartedAt, (id, frame, cycle) => _ = Play(lease, id, frame, cycle));
        }
        if (audio.LastMaintenance != maintenance)
        {
            maintenance = audio.LastMaintenance;
            observe(new("maintenance", "", null, null, null, maintenance));
        }
    }
    public Task Shutdown()
    {
        if (closed) return audio.WhenReleased;
        closed = true;
        Interlocked.Increment(ref controlsRevision);
        foreach (string key in active.Keys.ToArray()) Retire(key);
        return Observe("shutdown", "", audio.ShutdownWithinAsync(TimeSpan.FromMilliseconds(750)));
    }
}
