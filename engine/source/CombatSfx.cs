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
    // T173：交給 SoundGate 的音量類別；S22 環境音循環只在第一輪套用冷卻（原版是原生循環，只啟動一次）。
    public SoundCategory Category { get; init; } = SoundCategory.Sound;
    public bool CooldownFirstCycleOnly { get; init; }
}
public sealed record CombatSoundObservation(string Stage, string Key, int? SoundId, int? Frame, long? Cycle, AudioReply Reply);

// T310（R5-E，SD§2.3 A091C0 → A06D60）：IEffectAudio（6016 地圖特效）改正在播放音量的入口。
// IEffectAudio 本體宣告在 Effect6016Host.cs（不在本任務可改範圍），所以用能力介面＋擴充方法加上入口；
// 沒有這個能力的實作回 Unsupported，呼叫端照舊、不重播。voiceScale／ext 的意義同 IAudioPlayback.SetVoiceLevelAsync。
public interface IEffectAudioLevel
{
    Task<EffectAudioReply> SetVoiceLevelAsync(P24.MapEffectEvents.EffectOwner owner, double voiceScale, int ext);
}
public static class EffectAudioLevelExtensions
{
    public static Task<EffectAudioReply> SetVoiceLevelAsync(this IEffectAudio audio, P24.MapEffectEvents.EffectOwner owner, double voiceScale, int ext)
        => audio is IEffectAudioLevel level ? level.SetVoiceLevelAsync(owner, voiceScale, ext)
            : Task.FromResult(new EffectAudioReply(false, "Unsupported"));
}

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
        internal (double Scale, int Ext)? Level; // T302：最後一次送出的播放中音量。
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
    // T173：設定後，每次派送先經共用規則（類別音量、距離衰減、全域 200ms 冷卻）判定。
    public SoundGate? Gate { get; init; }
    public Func<uint, (int Dx, int Dy)?>? Offset { get; init; }

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
    void Dispatch(Lease lease, int id, int frame, long cycle)
    {
        double scale = 1;
        if (Gate is { } gate)
        {
            var instance = lease.Instance;
            var admission = gate.Admit(new(id, instance.Category, Offset?.Invoke(instance.Actor),
                Cooldown: !instance.CooldownFirstCycleOnly || cycle == 0));
            if (!admission.Dispatch)
            {
                observe(new("gate", instance.Key, id, frame, cycle, new(false, admission.Status, null, 0, [])));
                return;
            }
            scale = admission.Scale;
        }
        lease.Pending.Add(Play(lease, id, frame, cycle, scale));
    }
    async Task Play(Lease lease, int id, int frame, long cycle, double scale)
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
        await Observe("play", lease.Instance.Key, audio.PlayAsync(id, lease.Owner, scale), id, frame, cycle).ConfigureAwait(false);
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
                // T173：有 Gate 時改用 SoundGate 的全域冷卻（SoundCooldown，跨控制器、跨管線），不重複套用。
                if (Gate == null)
                {
                    // B07：A06020 同 sample 200ms 冷卻（now+200）；冷卻中直接略過，不補播。
                    if (lastPlayed.TryGetValue(id, out long at) && now - at < SampleCooldownMs)
                    {
                        observe(new("play", instance.Key, id, frame, cycle, new(false, "Cooldown", null, 0, [])));
                        return;
                    }
                    lastPlayed[id] = now;
                }
                Dispatch(lease, id, frame, cycle);
            });
        }
        if (audio.LastMaintenance != maintenance)
        {
            maintenance = audio.LastMaintenance;
            observe(new("maintenance", "", null, null, null, maintenance));
        }
    }
    // T302（R4-K，SD§2.3 A091C0）：每次更新替播放中（含收尾中）的實例重算音量，值變了才經 IAudioPlayback 改正在播放的音量。
    // level 回 null＝這次不更新（例如發聲體或自身不在場，原版 A3FCBE 也不呼叫 A091C0）。回傳本次送出的筆數。
    public int ApplyLevels(Func<CombatSoundInstance, (double Scale, int Ext)?> level)
    {
        if (closed) return 0;
        int sent = 0;
        foreach (var lease in active.Values.Concat(draining.Select(d => d.Lease)).ToArray())
        {
            if (!lease.Active || level(lease.Instance) is not { } next) continue;
            next = (double.IsFinite(next.Scale) ? Math.Clamp(next.Scale, 0, 1) : 0, Math.Clamp(next.Ext, 0, 128));
            if (lease.Level == next) continue;
            lease.Level = next; sent++;
            _ = Observe("level", lease.Instance.Key, audio.SetVoiceLevelAsync(lease.Owner, next.Scale, next.Ext));
        }
        return sent;
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
