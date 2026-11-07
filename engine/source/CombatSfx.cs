using P24.Audio;
using System.Text.RegularExpressions;

namespace P06;

public sealed record SoundFrame(int Index, double At, int[] Candidates);
public sealed record SoundSchedule(SoundFrame[] Frames, double Duration, bool Loop)
{
    // 依 docs/reference/rea-inventory.md B07（#417，sub_87C890 parser／sub_A3EF40 選擇）：
    // `<`＝模式 0、`[`＝模式 1、`^`＝模式 2；每個聲音 token 依序累積候選，最多存 5 個，第 6 個起讀掉不存；
    // 模式是最後一個 token 的值（parser 為互斥 switch，後寫覆蓋）。模式 0／1 都走 A060F0 單次播放。
    // 模式 2（`^`，A06210 距離／循環路徑）屬 T166，這裡回傳空集合不播放。
    // 值 0 保留在候選裡：選中 0 時略過、不重抽（見 SoundCursor）。
    public const int MaxCandidates = 5;
    static readonly Regex SoundToken = new(@"([<\[\^])(\d+)", RegexOptions.CultureInvariant);
    public static int[] Candidates(string suffix)
    {
        var ids = new List<int>(MaxCandidates);
        char mode = '\0';
        foreach (Match m in SoundToken.Matches(suffix ?? ""))
        {
            mode = m.Groups[1].Value[0];
            if (ids.Count < MaxCandidates)
                ids.Add(int.TryParse(m.Groups[2].Value, out int id) && id <= ushort.MaxValue ? id : 0);
        }
        return mode == '^' ? [] : ids.ToArray();
    }
    // sub_A3EF40：0 或 >5 個略過；1 個取第 0 項；2～5 個 rand() % n；選中 0 回傳 null（略過，不重抽）。
    public static int? Select(int[] candidates, Random random)
    {
        if (candidates.Length is 0 or > MaxCandidates) return null;
        int id = candidates.Length == 1 ? candidates[0] : candidates[random.Next(candidates.Length)];
        return id == 0 ? null : id;
    }

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
            // B07 選擇規則；靜態驗證，待 #52 真機聽音核對。
            if (SoundSchedule.Select(frame.Candidates, random) is int id)
                dispatch(id, frame.Index, cycle);
        }
    }
}

public sealed record CombatSoundInstance(string Key, long Instance, uint Actor, int Gfx, int Weapon,
    long StartedAt, long EndsAt, SoundSchedule Schedule)
{
    public bool DrainOnEnd { get; init; }
    public Func<bool>? StillValid { get; init; }
}
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
        internal readonly List<Task> Pending = [];
    }
    readonly Dictionary<string, Lease> active = [];
    readonly List<(Lease Lease, long Expires)> draining = [];
    readonly Dictionary<string, (long Instance, long Expires)> consumed = [];
    readonly Stack<string> ownerSlots = [];
    public const long SampleCooldownMs = 200;
    readonly Dictionary<int, long> lastPlayed = [];
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
    async Task Finish(Lease lease)
    {
        await Task.WhenAll(lease.Pending).ConfigureAwait(false);
        if (lease.Active) await Observe("finish",lease.Instance.Key,audio.FinishAsync(lease.Owner)).ConfigureAwait(false);
    }
    void CancelDraining((Lease Lease,long Expires) tail)
    {
        tail.Lease.Active=false;
        _=Observe("retire",tail.Lease.Instance.Key,audio.RetireAsync(tail.Lease.Owner));
        ownerSlots.Push(tail.Lease.Owner.Key);draining.Remove(tail);
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
            foreach(var tail in draining.ToArray())CancelDraining(tail);
            consumed.Clear();
            epoch = worldEpoch;
        }
        foreach(var tail in draining.ToArray())
            if(!enabled||now>=tail.Expires||tail.Lease.Instance.StillValid?.Invoke()==false)CancelDraining(tail);
        var current = instances.ToDictionary(i => i.Key);
        foreach (string key in active.Keys.ToArray())
        {
            var old = active[key].Instance;
            if(enabled && old.DrainOnEnd && now>=old.EndsAt && old.StillValid?.Invoke()!=false)
            {
                var finished=active[key];active.Remove(key);draining.Add((finished,now+15000));
                _=Finish(finished); // Owner slot retained until cancellation/cleanup; no reuse cuts off its tail.
            }
            else if (!enabled || !current.TryGetValue(key, out var value) || value.Instance != old.Instance ||
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
            lease.Cursor.Advance(now - instance.StartedAt, (id, frame, cycle) =>
            {
                // B07：A06020 同 sample 200ms 冷卻（now+200）；冷卻中直接略過，不補播。
                if (lastPlayed.TryGetValue(id, out long at) && now - at < SampleCooldownMs)
                {
                    observe(new("play", instance.Key, id, frame, cycle, new(false, "Cooldown", null, 0, [])));
                    return;
                }
                lastPlayed[id] = now;
                lease.Pending.Add(Play(lease, id, frame, cycle));
            });
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
        foreach(var tail in draining.ToArray())CancelDraining(tail);
        return Observe("shutdown", "", audio.ShutdownWithinAsync(TimeSpan.FromMilliseconds(750)));
    }
}
