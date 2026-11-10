using P24.Audio;
using P24.HostAudio;

namespace P06;

// T252（#613，gap-matrix 11.5 R2-B）：背景音樂、envsound.def 環境音、`^` 循環音的產品後端。
// 三個轉接器共用一個 P24.Audio.AudioStreamPlayer（engine/audio，DirectShow／DSoundRender，與戰鬥音效同一套傳輸方式）。
// 檔名依 docs/reference/sound.md：音樂 sound\musicN.mp3（§1 管線 B）；環境音與 `^` 先 sound\N.mp3 再 sound\N.wav（§4.7、表 8）。
// 原版找不到音樂 mp3 時改走 MIDI（§1 管線 C），這裡沒做：缺檔時 Start 回 false，由 BackgroundMusic 每秒重試。

/// <summary>IMusicStreamAudio：volume 已是「淡出 × MusicVolume × 主音量」。</summary>
public sealed class ProductMusicAudio(IAudioStream stream) : IMusicStreamAudio
{
    readonly Dictionary<long, long> handles = [];
    public static string[] Files(int music) => [$"music{music}.mp3"];
    public bool Start(MusicTrack track, double volume)
    {
        long h = stream.Start(Files(track.Music), track.Loop, Math.Clamp(volume, 0, 1));
        if (h == 0) return false;
        handles[track.Number] = h; return true;
    }
    public void SetVolume(MusicTrack track, double volume) { if (handles.TryGetValue(track.Number, out long h)) stream.SetVolume(h, Math.Clamp(volume, 0, 1)); }
    public void Stop(MusicTrack track) { if (handles.Remove(track.Number, out long h)) stream.Stop(h); }
    public bool Finished(MusicTrack track) => !handles.TryGetValue(track.Number, out long h) || stream.Finished(h);
    public int Live => handles.Count;
}

/// <summary>IEnvSoundAudio：volume 已是「淡入淡出 × EnvSoundVolume × 主音量」。單次音效播完由串流層自行釋放。</summary>
public sealed class ProductEnvSoundAudio(IAudioStream stream) : IEnvSoundAudio
{
    readonly Dictionary<long, long> handles = [];
    public static string[] Files(int id) => [$"{id}.mp3", $"{id}.wav"];
    public bool Start(EnvSoundVoice voice, double volume)
    {
        foreach (var done in handles.Where(p => stream.Finished(p.Value)).Select(p => p.Key).ToArray()) handles.Remove(done);
        long h = stream.Start(Files(voice.SoundId), voice.Loop, Math.Clamp(volume, 0, 1));
        if (h == 0) return false;
        handles[voice.Number] = h; return true;
    }
    public void SetVolume(EnvSoundVoice voice, double volume) { if (handles.TryGetValue(voice.Number, out long h)) stream.SetVolume(h, Math.Clamp(volume, 0, 1)); }
    public void Stop(EnvSoundVoice voice) { if (handles.Remove(voice.Number, out long h)) stream.Stop(h); }
    public int Live => handles.Count;
}

/// <summary>
/// IAmbientLoopAudio（`^`）：MapAmbientSound 給 0..100 的距離音量（A091C0）；最終音量＝距離音量/100 × EnvSoundVolume × 主音量（A06D60）。
/// 類別音量或主音量變動時，已在播的循環立即改音量（不重播）。
/// </summary>
public sealed class ProductAmbientLoopAudio(IAudioStream stream) : IAmbientLoopAudio
{
    readonly Dictionary<long, (long Handle, int Volume)> voices = [];
    double scale = 1;
    public double Scale => scale;
    public int Live => voices.Count;
    public static double Final(int distanceVolume, double scale) => double.IsFinite(scale) ? Math.Clamp(distanceVolume, 0, 100) / 100d * Math.Clamp(scale, 0, 1) : 0;
    public bool Start(MapAmbientVoice voice, int volume)
    {
        long h = stream.Start(ProductEnvSoundAudio.Files(voice.SoundId), true, Final(volume, scale));
        if (h == 0) return false;
        voices[voice.Number] = (h, volume); return true;
    }
    public void SetVolume(MapAmbientVoice voice, int volume)
    {
        if (!voices.TryGetValue(voice.Number, out var v)) return;
        voices[voice.Number] = (v.Handle, volume);
        stream.SetVolume(v.Handle, Final(volume, scale));
    }
    public void Stop(MapAmbientVoice voice) { if (voices.Remove(voice.Number, out var v)) stream.Stop(v.Handle); }
    public void SetScale(double value)
    {
        value = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
        if (value == scale) return;
        scale = value;
        foreach (var v in voices.Values) stream.SetVolume(v.Handle, Final(v.Volume, scale));
    }
}

/// <summary>
/// R6-A（T332）：地圖特效 `<`／`[` 的單次音效（A060F0）。檔名同 `^`（先 sound\N.mp3 再 sound\N.wav）；
/// 最終音量＝距離音量/100 × SoundVolume × 主音量（A06D60；類別是推測，見 MapAmbientSound.OneShotScale）。
/// 單次播放，播完由串流層釋放；這裡只記 handle 以便計數、Disarm 時一起被串流層清掉。
/// </summary>
public sealed class ProductOneShotAudio(IAudioStream stream) : IMapOneShotAudio
{
    readonly List<long> handles = [];
    double scale = 1;
    public double Scale => scale;
    public int Live { get { Prune(); return handles.Count; } }
    void Prune() => handles.RemoveAll(stream.Finished);
    public bool Play(MapOneShotVoice voice, int volume)
    {
        Prune();
        double final = ProductAmbientLoopAudio.Final(volume, scale);
        if (final <= 0) return false;
        long h = stream.Start(ProductEnvSoundAudio.Files(voice.SoundId), false, final);
        if (h == 0) return false;
        handles.Add(h); return true;
    }
    public void SetScale(double value) => scale = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}

/// <summary>進入世界時安裝三個後端（ClientSoundScene.Configure＋MapAmbientSound.Backend）。只有產品 ClientForm 會 Arm。</summary>
public static class ProductSound
{
    public const string ClientRootVariable = "TW_CLIENT_ROOT", DisableVariable = "TW_PRODUCT_SOUND";
    public const string DefaultClientRoot = @"D:\lineage開發\0-客戶端";
    static bool armed;
    static Task<SoundDefinitions>? loading;
    static string clientRoot = "";
    static Func<string, IAudioStream> factory = root => AudioStreamPlayer.Create(root);
    static Func<string, SoundDefinitions> load = SoundDefinitionSource.Load;
    public static IAudioStream? Stream { get; private set; }
    public static ProductMusicAudio? Music { get; private set; }
    public static ProductEnvSoundAudio? Env { get; private set; }
    public static ProductAmbientLoopAudio? Ambient { get; private set; }
    public static ProductOneShotAudio? OneShot { get; private set; }
    public static string Status { get; private set; } = "未啟用";
    public static bool Armed => armed;

    /// <summary>ClientForm 建構時呼叫（SoundRules.cs）。測試與 --module-tests 不會呼叫，所以不會開音訊裝置。</summary>
    public static bool Arm()
    {
        if (Environment.GetEnvironmentVariable(DisableVariable) == "0") { Status = $"已停用（{DisableVariable}=0）"; return false; }
        armed = true; Status = "等待進入世界"; return true;
    }
    /// <summary>測試用：換掉串流與定義檔來源，並 Arm。</summary>
    internal static void ArmForTests(Func<string, IAudioStream> streamFactory, Func<string, SoundDefinitions> definitions)
    { Disarm(); factory = streamFactory; load = definitions; armed = true; Status = "等待進入世界"; }
    internal static void Disarm()
    {
        armed = false; loading = null;
        ClientSoundScene.Reset();
        if (ReferenceEquals(MapAmbientSound.Backend, Ambient)) MapAmbientSound.Backend = null;
        if (ReferenceEquals(MapAmbientSound.OneShotBackend, OneShot)) MapAmbientSound.OneShotBackend = null;
        (Stream as IDisposable)?.Dispose();
        Stream = null; Music = null; Env = null; Ambient = null; OneShot = null; Status = "未啟用";
        factory = root => AudioStreamPlayer.Create(root); load = SoundDefinitionSource.Load;
    }

    /// <summary>由 ClientSoundScene.Observe 每次組畫面呼叫；ready＝已在世界中可繪製。定義檔在背景讀，讀完才 Configure。</summary>
    internal static void Pump(bool ready)
    {
        if (!armed || Stream != null && loading == null) return;
        if (loading == null)
        {
            if (!ready) return;
            clientRoot = Environment.GetEnvironmentVariable(ClientRootVariable) ?? DefaultClientRoot;
            try { Stream = factory(clientRoot); }
            catch (Exception ex) { armed = false; Status = "串流層無法建立：" + ex.Message; return; }
            Music = new(Stream); Env = new(Stream); Ambient = new(Stream); OneShot = new(Stream);
            MapAmbientSound.Backend = Ambient; // `^` 不需要定義檔，立即可用。
            MapAmbientSound.OneShotBackend = OneShot; // R6-A：`<`／`[` 單次音效，同樣不需要定義檔。
            string root = clientRoot; var read = load;
            loading = Task.Run(() => read(root));
            Status = "讀取 music.def／envsound.def";
            return;
        }
        if (!loading.IsCompleted) return;
        var defs = loading.IsCompletedSuccessfully ? loading.Result : new SoundDefinitions(null, null, "定義檔讀取失敗：" + loading.Exception?.GetBaseException().Message);
        loading = null;
        ClientSoundScene.Configure(defs, Music, Env);
        Status = $"已接上（{defs.Status}；音樂{(ClientSoundScene.Music != null ? "有" : "無")}、環境音{(ClientSoundScene.EnvSound != null ? "有" : "無")}、^ 有）";
    }
    public static object Snapshot() => new
    {
        status = Status, armed, clientRoot,
        music = Music?.Live ?? 0, env = Env?.Live ?? 0, ambient = Ambient?.Live ?? 0, ambientScale = Ambient?.Scale, oneShot = OneShot?.Live ?? 0,
        stream = (Stream as AudioStreamPlayer)?.Stats,
    };
}

public sealed partial class WorldSession
{
    [WorldSnapshot("productSound")]
    object? ProductSoundSnapshot() => ProductSound.Armed || ProductSound.Stream != null ? ProductSound.Snapshot() : null;
}
