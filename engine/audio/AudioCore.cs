namespace P24.Audio;

public readonly record struct AudioOwner(string Key, long Generation);
// Parameter values are numeric snapshots, NOT retained/dereferenceable native pointers.
public sealed record AudioNativeEvent(int Code, long Parameter1, long Parameter2,
    uint? OriginalHResult, string Disposition, int? FreeParametersHResult = null);
public sealed record AudioEvent(string Stage, string Format, string Detail, uint NativeCode = 0)
{
    public AudioNativeEvent? NativeEvent { get; init; }
}
public sealed record AudioReply(bool Success, string Status, Guid? Playback, int RemainingVoices, AudioEvent[] Events);
public sealed record AudioControls(double Gain, bool MasterMuted, bool DispatchEnabled)
{
    public bool CanDispatch => DispatchEnabled && !MasterMuted;
}

// Accessed only on AudioPlayer's one command thread. Tests use the same core with an explicit command double.
internal sealed class AudioCore(AudioAssets assets, IAudioDriver driver, int capacity)
{
    sealed class Voice(Guid id, AudioOwner owner, string alias, string format, FileStream lease, double scale = 1)
    {
        // T173 per-voice rule volume set at dispatch; T302 (A091C0) may update it while playing.
        internal double Scale = scale;
        // T302 recorded ext; T310 (R5-E) applies it as Miles pan (AIL_quick_set_volume 3rd arg -> AIL_set_sample_volume_pan).
        // A06C30 default 0.5 = ext 64 = centre, which is also the renderer's default balance 0, so a new voice needs no call.
        internal int Ext = AudioGain.CenterExt;
        internal int AppliedExt = AudioGain.CenterExt;
        internal readonly Guid Id = id;
        internal readonly AudioOwner Owner = owner;
        internal readonly string Alias = alias, Format = format;
        internal readonly FileStream Lease = lease;
        internal bool Closing;
    }
    readonly Dictionary<string, (long Generation, bool Active)> owners = new(StringComparer.Ordinal);
    readonly Dictionary<Guid, Voice> voices = [];
    // T310: like the emitter's +12/+16 fields written by A091C0, the last SetLevel per owner generation is kept and
    // applied to voices that start later (A060F0 -> A06C30 volume, A06AE0 ext). Cleared on Register/Retire.
    readonly Dictionary<string, (long Generation, double Scale, int Ext)> ownerLevels = new(StringComparer.Ordinal);
    AudioControls controls = new(1, false, false);
    bool controlsFault;
    internal bool Closing { get; private set; }
    internal int Count => voices.Count;
    internal AudioReply Reply(bool ok, string status, List<AudioEvent>? events = null, Guid? id = null)
        => new(ok, status, id, voices.Count, events?.ToArray() ?? []);
    bool Current(AudioOwner owner) => owner.Key != null && owners.TryGetValue(owner.Key, out var v) && v.Active && v.Generation == owner.Generation;
    static AudioEvent Native(string stage, string ext, NativeReply r) => new(r.PlatformFailure ? "PlatformFailure:" + stage : stage, ext, r.Text, r.Code) { NativeEvent = r.NativeEvent };

    internal AudioReply Register(AudioOwner owner)
    {
        if (Closing) return Reply(false, "Disposed");
        if (string.IsNullOrWhiteSpace(owner.Key) || owner.Generation < 0) return Reply(false, "InvalidOwner");
        if (Current(owner)) return voices.Values.Any(v => v.Owner.Key == owner.Key && v.Closing)
            ? Reply(false, "OwnerCleanupPending") : Reply(true, "AlreadyRegistered");
        if (owners.TryGetValue(owner.Key, out var old) && owner.Generation <= old.Generation) return Reply(false, "StaleGeneration");
        var events = new List<AudioEvent>();
        foreach (var v in voices.Values.Where(v => v.Owner.Key == owner.Key).ToArray()) Release(v, events);
        owners[owner.Key] = (owner.Generation, true);
        ownerLevels.Remove(owner.Key);
        return Reply(!voices.Values.Any(v => v.Owner.Key == owner.Key), events.Count == 0 ? "Registered" : "RegisteredWithCleanup", events);
    }

    internal AudioReply Configure(AudioControls next)
    {
        if (Closing) return Reply(false, "Disposed");
        if (!AudioGain.Valid(next.Gain)) return Reply(false, "InvalidGain");
        controls = next; // Logical gate closes even if native cleanup fails.
        var events = new List<AudioEvent>();
        bool failed = false;
        foreach (var voice in voices.Values.ToArray())
        {
            if (voice.Closing) { Release(voice, events); continue; }
            var gain = driver.Gain(voice.Alias, next.CanDispatch ? next.Gain * voice.Scale : 0);
            if (!gain.Ok) { failed = true; events.Add(Native("GainFailed", voice.Format, gain)); }
            if (!next.CanDispatch || !gain.Ok) Release(voice, events);
        }
        if (failed) foreach (var voice in voices.Values.ToArray()) Release(voice, events);
        controlsFault = failed || voices.Values.Any(v => v.Closing);
        return Reply(!controlsFault && events.Count == 0, controlsFault ? "ControlsFailed" : next.CanDispatch ? "ControlsApplied" : "DispatchSuppressed", events);
    }

    internal AudioReply Play(int soundId, AudioOwner owner, Func<bool>? stillAllowed = null, double scale = 1)
    {
        if (Closing) return Reply(false, "Disposed");
        if (!AudioGain.Valid(scale) || scale == 0) return Reply(false, "InvalidVoiceScale");
        if (!assets.Allows(soundId)) return Reply(false, "IdNotAllowed"); // full int; never narrowed to byte
        if (!Current(owner)) return Reply(false, "StaleOrUnknownOwner");
        if (!controls.CanDispatch || stillAllowed?.Invoke() == false) return Reply(false, "DispatchSuppressed");
        if (controlsFault) return Reply(false, "ControlsFailed");
        if (voices.Count >= capacity) return Reply(false, "VoiceCapacityReached"); // no stealing/restart/coalescing
        if (voices.Values.Any(v => v.Closing)) return Reply(false, "CleanupPending");
        var events = new List<AudioEvent>();
        string[] formats = assets.Formats(soundId);
        foreach (string ext in formats)
        {
            if (stillAllowed?.Invoke() == false) return Reply(false, "DispatchSuppressed", events);
            var acquired = assets.Acquire(soundId, ext);
            if (acquired.Error is { } error)
            {
                events.Add(error);
                if (ext == "mp3" && error.Stage is "SourceUnavailable" or "FileMissing" or "FileAccessFailure") continue;
                return Reply(false, error.Stage, events);
            }
            var id = Guid.NewGuid();
            if (stillAllowed?.Invoke() == false)
            { acquired.Lease!.Dispose(); return Reply(false, "DispatchSuppressed", events); }
            var voice = new Voice(id, owner, "p24a" + id.ToString("N"), ext, acquired.Lease!, scale);
            if (ownerLevels.TryGetValue(owner.Key, out var stored) && stored.Generation == owner.Generation)
            { voice.Scale = stored.Scale; voice.Ext = stored.Ext; }
            NativeReply opened = driver.Open(acquired.Path, ext, voice.Alias);
            if (opened.Ok || opened.ResourceOwned) voices.Add(id, voice);
            if (!opened.Ok)
            {
                events.Add(Native("OpenFailed", ext, opened));
                if (opened.ResourceOwned) Release(voice, events); else voice.Lease.Dispose();
                if (voices.ContainsKey(id)) return Reply(false, "CleanupPending", events, id);
                // Match the proven load-failure fallback; retain actual open-stage code, never call it 'missing'.
                if (ext == "mp3" && !opened.PlatformFailure) continue;
                return Reply(false, "OpenFailed", events);
            }
            // Apply volume before Run. Failure never permits an uncontrolled/full-volume start.
            if (stillAllowed?.Invoke() == false) { Release(voice, events); return Reply(false, "DispatchSuppressed", events); }
            NativeReply gain = driver.Gain(voice.Alias, controls.Gain * voice.Scale);
            if (!gain.Ok)
            {
                events.Add(Native("GainFailed", ext, gain)); Release(voice, events);
                return Reply(false, "GainFailed", events, voices.ContainsKey(id) ? id : null);
            }
            if (voice.Ext != voice.AppliedExt)
            {
                // Pan never raises the loudest channel; a failure is reported and retried by the next SetLevel.
                NativeReply pan = driver.Balance(voice.Alias, AudioGain.Balance(voice.Ext));
                if (pan.Ok) voice.AppliedExt = voice.Ext; else events.Add(Native("PanFailed", ext, pan));
            }
            if (stillAllowed?.Invoke() == false) { Release(voice, events); return Reply(false, "DispatchSuppressed", events); }
            NativeReply played = driver.Play(voice.Alias);
            if (played.Ok)
            {
                events.Add(new("PlaySubmitted", ext, "Native command accepted; not proof of audible output"));
                return Reply(true, ext == "wav" ? (formats.Contains("mp3") ? "StartedWavFallback" : "StartedWav") : "StartedMp3", events, id);
            }
            events.Add(Native(played.InvalidFile ? "DecodeFormatFailed" : "PlayFailed", ext, played));
            Release(voice, events);
            if (voices.ContainsKey(id)) return Reply(false, "CleanupPending", events, id);
            // An explicit invalid-file/format error may surface at play. Device/busy/unknown play errors never retry as WAV.
            if (ext == "mp3" && played.InvalidFile) continue;
            return Reply(false, "PlayFailed", events);
        }
        return Reply(false, "NoPlayableAsset", events);
    }

    // T302 (R4-K, SD§2.3 A091C0 -> A06D60): re-apply the volume of every live voice under this owner.
    // Generation must match, but the owner may be draining (Finish) so its submitted tail still follows distance.
    // A failed gain never leaves a voice at an uncontrolled volume: it is released, like Configure.
    internal AudioReply SetLevel(AudioOwner owner, double scale, int ext)
    {
        if (Closing) return Reply(false, "Disposed");
        if (!AudioGain.Valid(scale)) return Reply(false, "InvalidVoiceScale");
        if (!AudioGain.ValidExt(ext)) return Reply(false, "InvalidExt");
        if (owner.Key == null || !owners.TryGetValue(owner.Key, out var known) || known.Generation != owner.Generation)
            return Reply(false, "StaleOrUnknownOwner");
        ownerLevels[owner.Key] = (owner.Generation, scale, ext);
        var events = new List<AudioEvent>();
        int applied = 0; bool failed = false, panFailed = false;
        foreach (var voice in voices.Values.Where(v => v.Owner == owner && !v.Closing).ToArray())
        {
            voice.Scale = scale; voice.Ext = ext;
            NativeReply gain = driver.Gain(voice.Alias, controls.CanDispatch && !controlsFault ? controls.Gain * scale : 0);
            if (!gain.Ok) { failed = true; events.Add(Native("GainFailed", voice.Format, gain)); Release(voice, events); continue; }
            // T310: pan only changes the left/right ratio, never the loudest channel, so a pan failure keeps the voice
            // (gain is already controlled) and is retried on the next update because AppliedExt stays unchanged.
            if (voice.AppliedExt != ext)
            {
                NativeReply pan = driver.Balance(voice.Alias, AudioGain.Balance(ext));
                if (pan.Ok) voice.AppliedExt = ext;
                else { panFailed = true; events.Add(Native("PanFailed", voice.Format, pan)); }
            }
            applied++;
        }
        if (applied > 0) events.Add(new("LevelApplied", "", $"voices={applied}; scale={scale:0.####}; ext={ext} (pan {ext}/128, balance {AudioGain.Balance(ext)})"));
        return Reply(!failed && !panFailed, failed ? "GainFailed" : panFailed ? "PanFailed" : applied == 0 ? "NoLiveVoice" : "LevelApplied", events);
    }
    internal (double Scale, int Ext)[] Levels(AudioOwner owner) =>
        voices.Values.Where(v => v.Owner == owner && !v.Closing).Select(v => (v.Scale, v.Ext)).ToArray();
    internal int[] AppliedPans(AudioOwner owner) =>
        voices.Values.Where(v => v.Owner == owner && !v.Closing).Select(v => v.AppliedExt).ToArray();

    void Release(Voice voice, List<AudioEvent> events)
    {
        voice.Closing = true;
        NativeReply stopped = driver.Stop(voice.Alias);
        if (!stopped.Ok) events.Add(Native("StopFailed", voice.Format, stopped));
        // Always attempt close even if stop fails; only forget the native resource after successful close.
        NativeReply closed = driver.Close(voice.Alias);
        if (!closed.Ok) { events.Add(Native("CloseFailed", voice.Format, closed)); return; }
        voices.Remove(voice.Id);
        voice.Lease.Dispose();
    }

    internal AudioReply Stop(AudioOwner owner, Guid id)
    {
        if (!voices.TryGetValue(id, out var voice)) return Reply(true, "AlreadyStopped");
        if (voice.Owner != owner) return Reply(false, "WrongOwner");
        var events = new List<AudioEvent>(); Release(voice, events);
        return Reply(!voices.ContainsKey(id), voices.ContainsKey(id) ? "CleanupPending" : "Stopped", events);
    }
    internal AudioReply Retire(AudioOwner owner)
    {
        if (owner.Key == null || !owners.TryGetValue(owner.Key, out var known) || known.Generation != owner.Generation)
            return Reply(false, "StaleOrUnknownOwner");
        owners[owner.Key] = (owner.Generation, false);
        ownerLevels.Remove(owner.Key);
        var events = new List<AudioEvent>();
        foreach (var v in voices.Values.Where(v => v.Owner == owner).ToArray()) Release(v, events);
        return Reply(!voices.Values.Any(v => v.Owner == owner), "OwnerRetired", events);
    }
    internal AudioReply Finish(AudioOwner owner)
    {
        if (!Current(owner)) return Reply(false, "StaleOrUnknownOwner");
        // Natural animation completion closes dispatch, but lets submitted WAVs reach EC_COMPLETE.
        owners[owner.Key] = (owner.Generation, false);
        return Reply(true, "OwnerDraining");
    }
    internal AudioReply Poll()
    {
        var events = new List<AudioEvent>();
        foreach (var v in voices.Values.ToArray())
        {
            if (v.Closing) { Release(v, events); continue; }
            NativeReply status = driver.Status(v.Alias);
            if (!status.Ok) { events.Add(Native("StatusFailed", v.Format, status)); Release(v, events); }
            else if (status.Text.Equals("stopped", StringComparison.OrdinalIgnoreCase)) Release(v, events);
        }
        return Reply(events.Count == 0, "Maintenance", events);
    }
    internal AudioReply Shutdown()
    {
        Closing = true;
        var events = new List<AudioEvent>();
        foreach (var v in voices.Values.ToArray()) Release(v, events);
        return Reply(voices.Count == 0, voices.Count == 0 ? "Released" : "CleanupPending", events);
    }
}
