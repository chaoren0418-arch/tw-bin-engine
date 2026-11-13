using P06;
using P24.Audio;
using P24.MapEffectEvents;
namespace P24.HostAudio;
// Accepted r3 transport facade. Calls publish fences synchronously before mapping replies.
public sealed class AudioApiTransport(AudioPlayer player):IEffectAudio,IEffectAudioLevel {
    // T460（#1045）：原本是 assets/audio-manifest.json 的整檔 SHA；聲音改由 TW_CLIENT_ROOT/Sound 直讀＋程式內 pin 後，改成版本字串。
    // 與 P24.Audio 的 AudioPlayer.Effect6016Contract 不同時拒絕建立（6016 記 create Unavailable）。
    public const string AudioContract="p24-audio-assets-v3-client-sound";
    // T310（R5-E）：產品同一時間只有一個 6016 主機（ClientEffect6016.Ensure6016 等上一個 Closed 才建新的）。
    // Effect6016Host 不公開它的 transport，所以由產品工廠記下最近一個尚未關閉的 transport，給每次更新改音量（ClientForm.Apply6016Levels）使用。
    static IEffectAudio? current;
    public static IEffectAudio? Current=>Volatile.Read(ref current);
    /// <summary><paramref name="clientRoot"/>：原客戶端根目錄（TW_CLIENT_ROOT），唯讀。</summary>
    public static IEffectAudio Create(string clientRoot){
        if(!AudioPlayer.TryCreate(clientRoot,AudioContract,32,out var player,out var error))throw new InvalidDataException(error);
        var transport=new AudioApiTransport(player!);Volatile.Write(ref current,transport);return transport;
    }
    static AudioOwner Owner(EffectOwner owner)=>new(owner.TransportIdentity,owner.Generation);
    static async Task<EffectAudioReply> Map(Task<AudioReply> operation){var reply=await operation.ConfigureAwait(false);return new(reply.Success,reply.Status,reply);}
    public Task<EffectAudioReply> RegisterAsync(EffectOwner owner)=>Map(player.RegisterAsync(Owner(owner)));
    public Task<EffectAudioReply> RetireAsync(EffectOwner owner)=>Map(player.RetireAsync(Owner(owner)));
    public Task<EffectAudioReply> PlayAsync(int id,EffectOwner owner)=>Map(player.PlayAsync(id,Owner(owner)));
    // T302（R4-K）：播放中改音量（A091C0 → A06D60），直通 AudioPlayer.SetVoiceLevelAsync。
    public Task<EffectAudioReply> SetVoiceLevelAsync(EffectOwner owner,double voiceScale,int ext)=>Map(player.SetVoiceLevelAsync(Owner(owner),voiceScale,ext));
    public Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled)=>Map(player.ConfigureAsync(gain,muted,enabled));
    public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget){
        Interlocked.CompareExchange(ref current,null,this); // 先撤下，關閉中的 transport 不再收改音量。
        return Map(player.ShutdownWithinAsync(budget));
    }
    public Task WhenReleased=>player.WhenReleased;
    public EffectAudioReply LastMaintenance {get{var reply=player.LastMaintenance;return new(reply.Success,reply.Status,reply);}}
}
// T166 (#434)：`^` 需要循環播放（AIL_quick_play 次數 0）與逐音源音量（0..100）。AudioPlayer 目前兩者皆無，
// 由實作此介面的後端提供；Start 回 false 表示未啟動（槽表不變）。
public interface IAmbientLoopAudio {
    bool Start(MapAmbientVoice voice,int volume);
    void SetVolume(MapAmbientVoice voice,int volume);
    void Stop(MapAmbientVoice voice);
    // T252（#613）：EnvSoundVolume 類別音量 × 主音量（0..1）。後端把它乘到每個 voice 的距離音量上（A06D60），變動時立即套到已在播的循環。
    void SetScale(double scale){}
}
