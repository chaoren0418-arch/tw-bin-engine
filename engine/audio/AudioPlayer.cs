using System.Collections.Concurrent;

namespace P24.Audio;

/// <summary>受 pin catalog 約束的播放層，套用產品音量與靜音政策。</summary>
public sealed class AudioPlayer : IAsyncDisposable, IAudioPlayback
{
    readonly AudioCore core;
    readonly IAudioSessionLifetime? session;
    readonly BlockingCollection<Action> queue = new();
    readonly object gate = new();
    readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool accepting = true;
    sealed record DispatchState(AudioControls Controls, long Revision);
    DispatchState dispatch = new(new(1, false, false), 0);
    sealed record OwnerFence(long Generation, bool Active);
    // Published under the short host gate; read lock-free by the native worker.
    // Reference identity prevents a retired/replaced generation from being revived by queued work.
    readonly ConcurrentDictionary<string, OwnerFence> ownerFences = new(StringComparer.Ordinal);
    public AudioControls Controls => Volatile.Read(ref dispatch).Controls;
    Task<AudioReply>? shutdown;
    AudioReply maintenance = new(true, "NotStarted", null, 0, []);
    public AudioReply LastMaintenance => Volatile.Read(ref maintenance);
    public Task WhenReleased => released.Task;

    AudioPlayer(AudioCore core, IAudioSessionLifetime? session = null)
    {
        this.core = core;this.session=session;
        var thread = new Thread(Work) { IsBackground = true, Name = "P24 audio transport" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start(); // No device/DLL access until an approved Play reaches native open.
    }
    public static bool TryCreate(string assetsRoot, string trustedManifestSha256, int maxConcurrentVoices,
        out AudioPlayer? player, out string error)
    {
        player = null; error = "";
        if (maxConcurrentVoices is < 1 or > 1024) { error = "Explicit resource capacity must be 1..1024"; return false; }
        try
        {
            var assets = AudioAssets.Load(assetsRoot, trustedManifestSha256);
            player = new(new AudioCore(assets, new DirectShowDriver(() => new WindowsPlaybackGraph()), maxConcurrentVoices));
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or FormatException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        { error = ex.Message; return false; }
    }
    internal static AudioPlayer ForTests(AudioCore core, IAudioSessionLifetime? session=null) => new(core,session);
    public static AudioPlayer CreateCombat(string clientRoot, int capacity = 32)
    {
        if (capacity is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(capacity));
        return new(new AudioCore(AudioAssets.LoadCombat(clientRoot), new DirectShowDriver(() => new WindowsPlaybackGraph()), capacity),new SilentAudioSession());
    }
    Task<AudioReply> Enqueue(Func<AudioReply> action)
    {
        lock (gate)
        {
            if (!accepting) return Task.FromResult(new AudioReply(false, "Disposed", null, LastMaintenance.RemainingVoices, []));
            return Queue(action);
        }
    }
    Task<AudioReply> Queue(Func<AudioReply> action)
    {
        var result = new TaskCompletionSource<AudioReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(() =>
        {
            try { result.SetResult(action()); }
            catch (Exception ex) { result.SetResult(new(false, "TransportFailure", null, core.Count, [new("Exception", "", ex.Message)])); }
        });
        return result.Task;
    }
    Task<AudioReply> Rejected(string status) => Task.FromResult(new AudioReply(false, status, null, LastMaintenance.RemainingVoices, []));
    bool OwnerStillCurrent(string key, OwnerFence expected) => expected.Active
        && ownerFences.TryGetValue(key, out var current) && ReferenceEquals(current, expected);
    public Task<AudioReply> RegisterAsync(AudioOwner owner)
    {
        lock (gate)
        {
            if (!accepting) return Rejected("Disposed");
            if (string.IsNullOrWhiteSpace(owner.Key) || owner.Generation < 0) return Rejected("InvalidOwner");
            ownerFences.TryGetValue(owner.Key, out var previous);
            if (previous != null && (owner.Generation < previous.Generation || owner.Generation == previous.Generation && !previous.Active))
                return Rejected("StaleGeneration");
            // Duplicate active registration preserves its fence and does not cancel valid pending plays.
            var next = previous?.Generation == owner.Generation ? previous : new OwnerFence(owner.Generation, true);
            ownerFences[owner.Key] = next;
            return Queue(() =>
            {
                // Always execute the ordered cleanup transition: a newer registration immediately
                // retired/superseded before execution must still release older live generations.
                var result = core.Register(owner);
                return OwnerStillCurrent(owner.Key, next) ? result : core.Reply(false, "StaleGeneration", result.Events.ToList());
            });
        }
    }
    public Task<AudioReply> ConfigureAsync(double normalizedGain, bool masterMuted, bool dispatchEnabled)
    {
        lock (gate)
        {
            if (!accepting) return Task.FromResult(new AudioReply(false, "Disposed", null, LastMaintenance.RemainingVoices, []));
            if (!AudioGain.Valid(normalizedGain)) return Task.FromResult(new AudioReply(false, "InvalidGain", null, LastMaintenance.RemainingVoices, []));
            var next = new DispatchState(new(normalizedGain, masterMuted, dispatchEnabled), dispatch.Revision + 1);
            Volatile.Write(ref dispatch, next); // Immediately invalidate queued/in-flight not-yet-run plays, even across mute -> unmute.
            return Queue(() => {
                var reply=core.Configure(next.Controls);
                // Lifetime is independent of mute/gain and individual voice reclamation.
                if(next.Controls.DispatchEnabled && session!=null && !session.IsOpen)session.Start();
                return reply;
            });
        }
    }
    public Task<AudioReply> PlayAsync(int fullSoundId, AudioOwner owner) => PlayAsync(fullSoundId, owner, 1);
    public Task<AudioReply> PlayAsync(int fullSoundId, AudioOwner owner, double voiceScale)
    {
        lock (gate)
        {
            var requested = dispatch;
            if (!accepting || !requested.Controls.CanDispatch)
                return Task.FromResult(new AudioReply(false, accepting ? "DispatchSuppressed" : "Disposed", null, LastMaintenance.RemainingVoices, []));
            if (owner.Key == null || !ownerFences.TryGetValue(owner.Key, out var ownerFence) || !ownerFence.Active || ownerFence.Generation != owner.Generation)
                return Rejected("StaleOrUnknownOwner");
            return Queue(() => core.Play(fullSoundId, owner, () =>
                ReferenceEquals(Volatile.Read(ref dispatch), requested) && OwnerStillCurrent(owner.Key, ownerFence), voiceScale));
        }
    }
    public Task<AudioReply> FinishAsync(AudioOwner owner)
    {
        lock(gate)
        {
            if(!accepting)return Rejected("Disposed");
            if(owner.Key==null||!ownerFences.TryGetValue(owner.Key,out var fence)||!fence.Active||fence.Generation!=owner.Generation)
                return Rejected("StaleOrUnknownOwner");
            ownerFences[owner.Key]=new(owner.Generation,false);
            return Queue(()=>core.Finish(owner));
        }
    }
    // T302 (R4-K): live volume update. Same generation is enough (active or draining); the worker re-checks in AudioCore.
    public Task<AudioReply> SetVoiceLevelAsync(AudioOwner owner, double voiceScale, int ext)
    {
        lock (gate)
        {
            if (!accepting) return Rejected("Disposed");
            if (!AudioGain.Valid(voiceScale)) return Rejected("InvalidVoiceScale");
            if (!AudioGain.ValidExt(ext)) return Rejected("InvalidExt");
            if (owner.Key == null || !ownerFences.TryGetValue(owner.Key, out var fence) || fence.Generation != owner.Generation)
                return Rejected("StaleOrUnknownOwner");
            return Queue(() => core.SetLevel(owner, voiceScale, ext));
        }
    }
    public Task<AudioReply> StopAsync(AudioOwner owner, Guid playback) => Enqueue(() => core.Stop(owner, playback));
    public Task<AudioReply> RetireAsync(AudioOwner owner)
    {
        lock (gate)
        {
            if (!accepting) return Rejected("Disposed");
            if (owner.Key == null || !ownerFences.TryGetValue(owner.Key, out var current) || current.Generation != owner.Generation)
                return Rejected("StaleOrUnknownOwner");
            // Fence immediately, before cleanup enters the worker queue. Never hold this gate during native I/O.
            if (current.Active) ownerFences[owner.Key] = new(owner.Generation, false);
            return Queue(() => core.Retire(owner));
        }
    }
    public Task<AudioReply> ShutdownAsync()
    {
        lock (gate)
        {
            if (shutdown != null) return shutdown;
            accepting = false;
            Volatile.Write(ref dispatch, new(dispatch.Controls with { DispatchEnabled = false }, dispatch.Revision + 1));
            shutdown = Queue(() => { var r = core.Shutdown(); Volatile.Write(ref maintenance, r); return r; });
            return shutdown;
        }
    }
    void Work()
    {
        // One-second maintenance is resource reclamation only, not a sound/6016 trigger policy.
        // Poll even under continuous command traffic so completed voices cannot accumulate indefinitely.
        long next = Environment.TickCount64 + 1000;
        while (true)
        {
            if (queue.TryTake(out var action, (int)Math.Clamp(next - Environment.TickCount64, 0, 1000))) action();
            if (core.Closing) {
                try { session?.Stop(); }
                catch(Exception ex) { Volatile.Write(ref maintenance,new(false,"SessionCleanupPending",null,core.Count,[new("SessionStop","pcm",ex.Message)])); }
                if(core.Count==0 && !(session?.IsOpen??false))break;
            }
            if (Environment.TickCount64 >= next)
            {
                try { Volatile.Write(ref maintenance, core.Poll()); }
                catch (Exception ex) { Volatile.Write(ref maintenance, new(false, "MaintenanceFailure", null, core.Count, [new("Exception", "", ex.Message)])); }
                next = Environment.TickCount64 + 1000;
            }
        }
        queue.Dispose();
        released.TrySetResult();
    }
    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
        // A close failure remains owned and retried, never silently reported as released.
        // Host can observe ShutdownAsync/LastMaintenance and bound its own shutdown wait.
        await WhenReleased.ConfigureAwait(false);
    }
    public async Task<AudioReply> ShutdownWithinAsync(TimeSpan hostBudget)
    {
        if (hostBudget <= TimeSpan.Zero || hostBudget.TotalMilliseconds > int.MaxValue)
            return new(false, "InvalidShutdownBudget", null, LastMaintenance.RemainingVoices, []);
        using var deadline = new CancellationTokenSource(hostBudget);
        try
        {
            var first = await ShutdownAsync().WaitAsync(deadline.Token).ConfigureAwait(false);
            await WhenReleased.WaitAsync(deadline.Token).ConfigureAwait(false);
            return new(true, "Released", null, 0, first.Events);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        { return new(false, "CleanupPendingOrNativeCallBlocked", null, LastMaintenance.RemainingVoices, LastMaintenance.Events); }
    }
}
