namespace P24.MapEffectEvents;

public readonly record struct EffectKey(int MapId, string BlockKey, int RecordOrdinal);
public readonly record struct EffectOwner(Guid ControllerId, EffectKey Key, long Generation)
{
    // AudioOwner.Identity uses this, AudioOwner.Generation uses Generation. No hash collisions.
    public string TransportIdentity => $"6016/{ControllerId:N}/{Key.MapId}/{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Key.BlockKey))}/{Key.RecordOrdinal}";
}
public readonly record struct EffectPlacement(int RecordOrdinal, int Gfx, int X, int Y);
/// <param name="Lifetime">The actual OriginalMapBlock object; retained remaps must reuse its reference.</param>
public sealed record BlockSnapshot(string BlockKey, object Lifetime, IReadOnlyList<EffectPlacement> Effects);
public sealed record LifecycleDelta(IReadOnlyList<EffectOwner> Created, IReadOnlyList<EffectOwner> Retired);
public readonly record struct EventGate(bool Enabled, bool Available, bool Audible)
{
    public static EventGate Open => new(true, true, true);
}
public sealed record EffectCandidate(EffectOwner Owner, int X, int Y, long AbsoluteStep, long LoopOrdinal,
    int SequenceIndex, int Frame, int CandidateSlot, int SoundId);
/// <summary>Opaque single-use capability issued by this controller, never constructible by a caller.</summary>
public sealed class EventRequest
{
    public EffectCandidate Candidate { get; }
    internal EventRequest(EffectCandidate candidate) => Candidate = candidate;
}
public enum EventDisposition { Pending, Muted, Unavailable, Inaudible, PolicyFailure }
public sealed record EventDecision(EffectCandidate? Candidate, EventDisposition Disposition,
    EventRequest? Request, string? Error = null);
public enum DispatchStatus { Submitted, StaleOrConsumed, Muted, Unavailable, Inaudible, Cooldown, CallbackFailure }
public sealed record DispatchReply(DispatchStatus Status, string? Error = null);
public sealed record EffectFrameState(EffectOwner Owner, int X, int Y, long StartedAtMs,
    long AbsoluteStep, long LoopOrdinal, int SequenceIndex, int Frame);

/// <summary>
/// Owned-engine policy: 24Hz, coalesced catch-up, generation invalidation and shared200ms cooldown.
/// No audio, P06 types, native imports, tasks, timers, file access or paint side effects.
/// Public operations serialize internally. Callbacks must be short and must not mutate this controller.
/// </summary>
public sealed class Effect6016Controller
{
    private sealed class State(EffectOwner owner, object lifetime, EffectPlacement placement, long startedAt)
    {
        public EffectOwner Owner = owner;
        public object Lifetime = lifetime;
        public EffectPlacement Placement = placement;
        public long StartedAt = startedAt;
        public long Step;
        public EventRequest? Pending;
    }
    private readonly object sync = new();
    private readonly Dictionary<EffectKey, State> states = [];
    private readonly Dictionary<int, long> lastSubmission = [];
    private readonly Func<int> chooseSlot;
    private long nextGeneration, lastNow;
    private bool inCallback;
    private long regressions;
    public Guid InstanceId { get; } = Guid.NewGuid();
    public string MetadataSha256 { get; }
    public long ClockRegressions { get { lock (sync) return regressions; } }

    public Effect6016Controller(Effect6016Definition definition, Func<int>? chooseSlot = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        MetadataSha256 = definition.MetadataSha256;
        this.chooseSlot = chooseSlot ?? (() => Random.Shared.Next(3));
    }
    private void NotInCallback()
    {
        if (inCallback) throw new InvalidOperationException("Controller mutation inside a policy/dispatch callback is not allowed.");
    }
    private long Observe(long nowMs)
    {
        if (nowMs < 0) throw new ArgumentOutOfRangeException(nameof(nowMs));
        if (nowMs < lastNow) { regressions++; return lastNow; }
        return lastNow = nowMs;
    }
    private T Callback<T>(Func<T> callback)
    {
        inCallback = true;
        try { return callback(); }
        finally { inCallback = false; }
    }

    /// <summary>
    /// Reconcile an atomic, fully published cache snapshot. No events occur here.
    /// Null map requires an empty snapshot and retires all actors. Cooldown/generation survive map changes.
    /// </summary>
    public LifecycleDelta Synchronize(int? mapId, IReadOnlyList<BlockSnapshot> blocks, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        lock (sync)
        {
            NotInCallback();
            if (nowMs < 0 || mapId is < 0 || (mapId is null && blocks.Count != 0))
                throw new ArgumentException("Invalid map/time snapshot.");
            var wanted = new Dictionary<EffectKey, (object Lifetime, EffectPlacement Placement)>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var block in blocks)
            {
                ArgumentNullException.ThrowIfNull(block);
                if (string.IsNullOrWhiteSpace(block.BlockKey) || !names.Add(block.BlockKey))
                    throw new ArgumentException("Empty/duplicate block key.");
                ArgumentNullException.ThrowIfNull(block.Lifetime);
                ArgumentNullException.ThrowIfNull(block.Effects);
                var ordinals = new HashSet<int>();
                foreach (var placement in block.Effects)
                {
                    if (placement.RecordOrdinal < 0 || !ordinals.Add(placement.RecordOrdinal))
                        throw new ArgumentException("Invalid/duplicate original record ordinal.");
                    if (placement.Gfx == Effect6016Definition.EffectId)
                        wanted.Add(new(mapId!.Value, block.BlockKey, placement.RecordOrdinal), (block.Lifetime, placement));
                }
            }
            int additions = wanted.Count(x => !states.TryGetValue(x.Key, out var old) || !ReferenceEquals(old.Lifetime, x.Value.Lifetime));
            if (nextGeneration > long.MaxValue - additions) throw new InvalidOperationException("Generation space exhausted.");
            long now = Observe(nowMs);
            var retired = new List<EffectOwner>();
            foreach (var entry in states.ToArray())
                if (!wanted.TryGetValue(entry.Key, out var next) || !ReferenceEquals(entry.Value.Lifetime, next.Lifetime))
                {
                    entry.Value.Pending = null;
                    retired.Add(entry.Value.Owner);
                    states.Remove(entry.Key);
                }
            var created = new List<EffectOwner>();
            foreach (var entry in wanted.OrderBy(x => x.Key.BlockKey, StringComparer.Ordinal).ThenBy(x => x.Key.RecordOrdinal))
            {
                if (states.TryGetValue(entry.Key, out var retained)) retained.Placement = entry.Value.Placement;
                else
                {
                    var owner = new EffectOwner(InstanceId, entry.Key, ++nextGeneration);
                    states.Add(entry.Key, new(owner, entry.Value.Lifetime, entry.Value.Placement, now));
                    created.Add(owner);
                }
            }
            return new(created.AsReadOnly(), retired.AsReadOnly());
        }
    }

    public LifecycleDelta Clear(long nowMs) => Synchronize(null, Array.Empty<BlockSnapshot>(), nowMs);
    public bool IsCurrent(EffectOwner owner)
    {
        lock (sync) return states.TryGetValue(owner.Key, out var state) && state.Owner == owner;
    }
    /// <summary>Pure snapshot for draw; repeated calls cannot move the clock/cursor or select a sound.</summary>
    public IReadOnlyList<EffectFrameState> Frames()
    {
        lock (sync) return states.Values.OrderBy(s => s.Owner.Key.BlockKey, StringComparer.Ordinal)
            .ThenBy(s => s.Owner.Key.RecordOrdinal).Select(s => new EffectFrameState(s.Owner,
                s.Placement.X, s.Placement.Y, s.StartedAt, s.Step, s.Step / 12, (int)(s.Step % 12),
                Effect6016Definition.FrameAtStep(s.Step))).ToArray();
    }
    private static EventDisposition? Suppression(EventGate gate) =>
        !gate.Enabled ? EventDisposition.Muted : !gate.Available ? EventDisposition.Unavailable :
        !gate.Audible ? EventDisposition.Inaudible : null;

    /// <summary>
    /// Advance every active actor once from one host update pump, never from paint.
    /// Each actor produces at most one latest event transition, in stable block/ordinal order.
    /// Gate/selector errors are observable, consume that transition and do not stop other actors.
    /// </summary>
    public IReadOnlyList<EventDecision> Advance(long nowMs, Func<EffectCandidate, EventGate> gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        lock (sync)
        {
            NotInCallback();
            long now = Observe(nowMs);
            var decisions = new List<EventDecision>();
            foreach (var state in states.Values.OrderBy(s => s.Owner.Key.BlockKey, StringComparer.Ordinal).ThenBy(s => s.Owner.Key.RecordOrdinal))
            {
                long step = Effect6016Definition.StepAt(state.StartedAt, now);
                if (step <= state.Step) continue;
                long previous = state.Step;
                state.Step = step;
                state.Pending = null; // Older asynchronous request expired at the next sequence transition.
                long eventStep = (step - 1) / 12 * 12 + 1;
                if (eventStep <= previous) continue;
                EffectCandidate? candidate = null;
                try
                {
                    int slot = Callback(chooseSlot);
                    if (slot is < 0 or > 2) throw new InvalidOperationException("Slot selector must return0,1,2.");
                    candidate = new(state.Owner, state.Placement.X, state.Placement.Y, eventStep,
                        eventStep / 12, 1, 0, slot, Effect6016Definition.CandidateSlots[slot]);
                    var denied = Suppression(Callback(() => gate(candidate)));
                    if (denied is { } disposition) decisions.Add(new(candidate, disposition, null));
                    else
                    {
                        state.Pending = new(candidate);
                        decisions.Add(new(candidate, EventDisposition.Pending, state.Pending));
                    }
                }
                catch (Exception ex) { decisions.Add(new(candidate, EventDisposition.PolicyFailure, null, ex.Message)); }
            }
            return decisions.AsReadOnly();
        }
    }

    /// <summary>
    /// Call only after asynchronous owner registration is ready, immediately before enqueueing Play.
    /// Rechecks current generation, one-use capability, current gates and per-ID shared cooldown.
    /// submit must synchronously enqueue (not await) the transport operation and report async failures separately.
    /// Cooldown reserves submission time, including a throwing submit: failed audio is not retried/backfilled.
    /// </summary>
    public DispatchReply TryDispatch(EventRequest request, long nowMs, Func<EffectCandidate, EventGate> gate,
        Action<EffectCandidate> submit)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(gate); ArgumentNullException.ThrowIfNull(submit);
        lock (sync)
        {
            NotInCallback();
            long now = Observe(nowMs);
            var candidate = request.Candidate;
            if (!states.TryGetValue(candidate.Owner.Key, out var state) || state.Owner != candidate.Owner ||
                !ReferenceEquals(state.Pending, request)) return new(DispatchStatus.StaleOrConsumed);
            state.Pending = null; // Consume before gate/callback: never retry this event after a denial/failure.
            // Async completion may run before the next update pump; still enforce the real clock boundary.
            if (Effect6016Definition.StepAt(state.StartedAt, now) > state.Step)
                return new(DispatchStatus.StaleOrConsumed);
            try
            {
                var denied = Suppression(Callback(() => gate(candidate)));
                if (denied is { } disposition) return new(disposition switch
                {
                    EventDisposition.Muted => DispatchStatus.Muted,
                    EventDisposition.Unavailable => DispatchStatus.Unavailable,
                    _ => DispatchStatus.Inaudible
                });
                if (lastSubmission.TryGetValue(candidate.SoundId, out long at) && now - at < Effect6016Definition.CooldownMs)
                    return new(DispatchStatus.Cooldown);
                lastSubmission[candidate.SoundId] = now;
                Callback(() => { submit(candidate); return true; });
                return new(DispatchStatus.Submitted);
            }
            catch (Exception ex) { return new(DispatchStatus.CallbackFailure, ex.Message); }
        }
    }
}
