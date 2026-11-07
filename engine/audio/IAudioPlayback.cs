namespace P24.Audio;

public interface IAudioPlayback
{
    Task<AudioReply> RegisterAsync(AudioOwner owner);
    Task<AudioReply> RetireAsync(AudioOwner owner);
    Task<AudioReply> FinishAsync(AudioOwner owner) => RetireAsync(owner);
    Task<AudioReply> PlayAsync(int fullSoundId, AudioOwner owner);
    // T173: per-voice scale (0..1] from the shared sound rules, multiplied with the master gain.
    // Implementations without per-voice gain fall back to the master-gain play.
    Task<AudioReply> PlayAsync(int fullSoundId, AudioOwner owner, double voiceScale) => PlayAsync(fullSoundId, owner);
    Task<AudioReply> ConfigureAsync(double normalizedGain, bool masterMuted, bool dispatchEnabled);
    Task<AudioReply> ShutdownWithinAsync(TimeSpan hostBudget);
    Task WhenReleased { get; }
    AudioReply LastMaintenance { get; }
}
