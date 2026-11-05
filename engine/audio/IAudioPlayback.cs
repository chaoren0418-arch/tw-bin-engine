namespace P24.Audio;

public interface IAudioPlayback
{
    Task<AudioReply> RegisterAsync(AudioOwner owner);
    Task<AudioReply> RetireAsync(AudioOwner owner);
    Task<AudioReply> PlayAsync(int fullSoundId, AudioOwner owner);
    Task<AudioReply> ConfigureAsync(double normalizedGain, bool masterMuted, bool dispatchEnabled);
    Task<AudioReply> ShutdownWithinAsync(TimeSpan hostBudget);
    Task WhenReleased { get; }
    AudioReply LastMaintenance { get; }
}
