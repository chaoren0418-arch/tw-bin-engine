using System.Collections.Concurrent;

namespace P24.Audio;

/// <summary>Two-ID transport with self-owned gain/mute policy; no original Miles distance/pan equivalence.</summary>
public sealed class AudioPlayer : IAsyncDisposable
{
    readonly AudioCore core;
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

    AudioPlayer(AudioCore core)
    {
        this.core = core;
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
    internal static AudioPlayer ForTests(AudioCore core) => new(core);
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
            return Queue(() => core.Configure(next.Controls));
        }
    }
    public Task<AudioReply> PlayAsync(int fullSoundId, AudioOwner owner)
    {
        lock (gate)
        {
            var requested = dispatch;
            if (!accepting || !requested.Controls.CanDispatch)
                return Task.FromResult(new AudioReply(false, accepting ? "DispatchSuppressed" : "Disposed", null, LastMaintenance.RemainingVoices, []));
            if (owner.Key == null || !ownerFences.TryGetValue(owner.Key, out var ownerFence) || !ownerFence.Active || ownerFence.Generation != owner.Generation)
                return Rejected("StaleOrUnknownOwner");
            return Queue(() => core.Play(fullSoundId, owner, () =>
                ReferenceEquals(Volatile.Read(ref dispatch), requested) && OwnerStillCurrent(owner.Key, ownerFence)));
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
            if (core.Closing && core.Count == 0) break;
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
