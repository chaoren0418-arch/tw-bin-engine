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
    // T302（R4-K，SD§2.3 A091C0 → A06D60）：改正在播放的音量。套到 owner 名下所有還在播的 voice（A091C0 對發聲體名下每個 sample 呼叫 A06D60）；
    // voiceScale＝類別音量·vol/100（0..1，0＝靜音但不停），仍乘主音量。ext 是 Miles AIL_quick_set_volume 第三參數（0..128），
    // 意義屬推測（SD§2.3），只記錄在 voice 上、不套用到裝置。沒有這個能力的實作回 Unsupported（呼叫端照舊、不重播）。
    Task<AudioReply> SetVoiceLevelAsync(AudioOwner owner, double voiceScale, int ext)
        => Task.FromResult(new AudioReply(false, "Unsupported", null, 0, []));
    Task<AudioReply> ConfigureAsync(double normalizedGain, bool masterMuted, bool dispatchEnabled);
    Task<AudioReply> ShutdownWithinAsync(TimeSpan hostBudget);
    Task WhenReleased { get; }
    AudioReply LastMaintenance { get; }
}
