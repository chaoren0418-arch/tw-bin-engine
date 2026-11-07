using P06;
using P24.Audio;
using P24.MapEffectEvents;
namespace P24.HostAudio;
// Accepted r3 transport facade. Calls publish fences synchronously before mapping replies.
public sealed class AudioApiTransport(AudioPlayer player):IEffectAudio {
    public const string ManifestPin="87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe";
    public static IEffectAudio Create(string root){
        if(!AudioPlayer.TryCreate(root,ManifestPin,32,out var player,out var error))throw new InvalidDataException(error);
        return new AudioApiTransport(player!);
    }
    static AudioOwner Owner(EffectOwner owner)=>new(owner.TransportIdentity,owner.Generation);
    static async Task<EffectAudioReply> Map(Task<AudioReply> operation){var reply=await operation.ConfigureAwait(false);return new(reply.Success,reply.Status,reply);}
    public Task<EffectAudioReply> RegisterAsync(EffectOwner owner)=>Map(player.RegisterAsync(Owner(owner)));
    public Task<EffectAudioReply> RetireAsync(EffectOwner owner)=>Map(player.RetireAsync(Owner(owner)));
    public Task<EffectAudioReply> PlayAsync(int id,EffectOwner owner)=>Map(player.PlayAsync(id,Owner(owner)));
    public Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled)=>Map(player.ConfigureAsync(gain,muted,enabled));
    public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget)=>Map(player.ShutdownWithinAsync(budget));
    public Task WhenReleased=>player.WhenReleased;
    public EffectAudioReply LastMaintenance {get{var reply=player.LastMaintenance;return new(reply.Success,reply.Status,reply);}}
}
// T166 (#434)：`^` 需要循環播放（AIL_quick_play 次數 0）與逐音源音量（0..100）。AudioPlayer 目前兩者皆無，
// 由實作此介面的後端提供；Start 回 false 表示未啟動（槽表不變）。
public interface IAmbientLoopAudio {
    bool Start(MapAmbientVoice voice,int volume);
    void SetVolume(MapAmbientVoice voice,int volume);
    void Stop(MapAmbientVoice voice);
}
