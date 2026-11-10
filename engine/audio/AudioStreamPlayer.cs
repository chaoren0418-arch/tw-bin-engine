using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace P24.Audio;

// T252（#613）：背景音樂、envsound.def 環境音與 `^` 循環音的播放層。
// 原版：音樂走 mss32 串流（AIL_open_stream，sound\musicN.mp3），環境音與 `^` 走 AIL_quick_play（次數 0＝循環），
// 檔案查找先 sound\N.mp3 再 sound\N.wav（docs/reference/sound.md §1、§2.5、§4.7）。
// 這裡每個 voice 一個 DirectShow graph（與 AudioCore 相同的 DSoundRender 隔離方式），不碰系統或工作階段音量。
// 呼叫端（UI 執行緒）只做檔案存在檢查與排入命令；開檔、調音量、輪詢都在單一 MTA 執行緒。

/// <summary>產品端看到的串流介面。handle 為 0 表示沒有啟動。volume 是最終比例 0..1。</summary>
public interface IAudioStream
{
    long Start(IReadOnlyList<string> candidates, bool loop, double volume);
    void SetVolume(long handle, double volume);
    void Stop(long handle);
    /// <summary>單次播放已自然播完、開檔失敗或已停止時回 true。</summary>
    bool Finished(long handle);
    /// <summary>R7-L（T351）：true＝音樂改走 MIDI（sound 目錄沒有 music*.mp3）。替身預設 false。</summary>
    bool MidiFallback => false;
}

/// <summary>原生播放的最小動作；測試以替身取代。每個方法回 true 表示成功。</summary>
public interface IStreamVoiceDriver
{
    bool Open(string path, string voice);
    bool Volume(string voice, int hundredthDecibels);
    bool Run(string voice);
    /// <summary>回到開頭（循環用）。</summary>
    bool Rewind(string voice);
    /// <summary>"playing"、"ended" 或 "failed"。</summary>
    string Status(string voice);
    void Close(string voice);
}

public sealed record AudioStreamStats(int Live, long Started, long Rejected, long Looped, long Ended, long Failed, string LastError);

public sealed class AudioStreamPlayer : IAudioStream, IDisposable
{
    public const int PollMs = 250;
    static readonly Regex SafeName = new(@"^[A-Za-z0-9_]{1,40}\.(mp3|wav|mid)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    static readonly Regex MusicName = new(@"^music\d+\.mp3$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    // T310（R5-E，SD§1 管線 C，已驗證 TW1608262200.bin）：sound\music*.mp3 一個都沒有時 byte_13D3F88=1，音樂改走 MCI。
    // 8F1C30／8F2FB0／8F3B90／8F3D70 在這個模式下都只呼叫 8F63D0(0)：sprintf("%ssound\%d.mid", 根目錄, 0)，
    // mciSendCommand(MCI_OPEN 0x803, 0x3202, 裝置型別 0x20B 序列器) → MCI_PLAY(0x806, MCI_NOTIFY)；
    // 8F6340 收到 MCI_NOTIFY_SUCCESSFUL 就 MCI_PLAY(MCI_NOTIFY|MCI_FROM, from 0) 重播，所以固定是 0.mid 無限循環，不設音量。
    public const string MidiFallbackFile = "0.mid";
    readonly string root;
    readonly IStreamVoiceDriver driver;
    readonly Func<string, bool> exists;
    readonly int capacity;
    readonly bool inline;
    long midiHandle; // MIDI 只有一個 MCI 裝置；新的音樂要求取代舊的。
    readonly BlockingCollection<Action>? queue;
    readonly object gate = new();
    sealed class Voice(long handle, string path, bool loop)
    {
        public readonly long Handle = handle; public readonly string Path = path, Alias = "p24s" + handle; public readonly bool Loop = loop;
        public bool Open;
    }
    readonly Dictionary<long, Voice> voices = [];          // 只在命令執行緒使用
    readonly ConcurrentDictionary<long, byte> live = new(); // 已排入、尚未結束的 handle（UI 執行緒可讀）
    long next, started, rejected, looped, ended, failed; string lastError = "";
    bool disposed;

    /// <summary>true＝sound 目錄裡沒有任何 music*.mp3，音樂要求改播 sound\0.mid（循環）。</summary>
    public bool MidiFallback { get; }

    AudioStreamPlayer(string soundRoot, IStreamVoiceDriver driver, Func<string, bool> exists, int capacity, bool inline, bool midiFallback)
    {
        root = Path.GetFullPath(soundRoot); this.driver = driver; this.exists = exists; this.capacity = capacity; this.inline = inline;
        MidiFallback = midiFallback;
        if (inline) return;
        queue = new();
        var thread = new Thread(Work) { IsBackground = true, Name = "P24 audio stream" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }
    /// <summary>產品：clientRoot 下的 sound 目錄（不分大小寫，Windows）。不複製、不改任何檔案。</summary>
    public static AudioStreamPlayer Create(string clientRoot, int capacity = 48)
    {
        if (capacity is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(capacity));
        string sound = Path.Combine(clientRoot, "sound");
        return new(sound, new RoutingStreamDriver(new DirectShowStreamDriver(), new MciMidiDriver()), File.Exists, capacity, false, NeedsMidi(sound));
    }
    /// <summary>離線測試：命令同步執行，Poll 由測試呼叫。</summary>
    public static AudioStreamPlayer ForTests(string soundRoot, IStreamVoiceDriver driver, Func<string, bool> exists, int capacity = 48, bool midiFallback = false)
        => new(soundRoot, driver, exists, capacity, true, midiFallback);
    /// <summary>原版的判斷：目錄存在而且一個 music*.mp3 都沒有（不分大小寫）。目錄不存在時什麼都播不了，不切 MIDI。</summary>
    public static bool NeedsMidi(string soundDirectory)
    {
        try { return Directory.Exists(soundDirectory) && !Directory.EnumerateFiles(soundDirectory, "*.mp3").Any(f => MusicName.IsMatch(Path.GetFileName(f))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    /// <summary>MIDI 模式下，音樂要求（含任何 musicN.mp3 候選）一律換成 0.mid 循環；其他要求不變。</summary>
    /// <summary>
    /// R7-L（T351，已驗證靜態，見 docs/reports/issue-737.md）：MIDI 模式下，8F1C30／8F2FB0／8F3B90／8F3D70 的曲目參數
    /// 是 0..8 號系統槽（登入 0、選角 8 等）時，MIDI 在播就 8F5510（MCI_CLOSE），不改播 0.mid；其他參數（區域 -1、戰鬥 9／10）才播 0.mid。
    /// </summary>
    public const int MidiSilentSlots = 9;
    public static bool SilencesMidi(int systemSlot) => systemSlot is >= 0 and < MidiSilentSlots;
    public static (IReadOnlyList<string> Candidates, bool Loop, bool Midi) Route(IReadOnlyList<string> candidates, bool loop, bool midiFallback)
        => midiFallback && candidates.Any(c => MusicName.IsMatch(c)) ? ([MidiFallbackFile], true, true) : (candidates, loop, false);

    public AudioStreamStats Stats { get { lock (gate) return new(live.Count, started, rejected, looped, ended, failed, lastError); } }
    public int Live => live.Count;

    void Post(Action action)
    {
        if (inline) { action(); return; }
        try { queue!.Add(action); } catch (InvalidOperationException) { }
    }

    public long Start(IReadOnlyList<string> candidates, bool loop, double volume)
    {
        if (!AudioGain.Valid(volume)) volume = 0;
        bool midi;
        (candidates, loop, midi) = Route(candidates, loop, MidiFallback);
        string? path = null;
        foreach (string name in candidates)
        {
            if (!SafeName.IsMatch(name)) continue; // 只接受產品組出的單一檔名，不接受路徑。
            string full = Path.GetFullPath(Path.Combine(root, name));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (exists(full)) { path = full; break; }
        }
        long handle, previousMidi = 0;
        lock (gate)
        {
            if (disposed || path == null || live.Count >= capacity) { rejected++; if (path == null) lastError = "缺檔：" + string.Join("／", candidates); return 0; }
            handle = ++next; started++;
            if (midi) { previousMidi = midiHandle; midiHandle = handle; }
        }
        if (previousMidi != 0) Stop(previousMidi); // 單一 MCI 裝置：先關舊的，新的才開得起來。
        live[handle] = 0;
        int db = AudioGain.Decibels(volume);
        Post(() =>
        {
            var voice = new Voice(handle, path, loop);
            if (!live.ContainsKey(handle)) return; // 排隊期間已 Stop。
            voices[handle] = voice;
            if (!driver.Open(path, voice.Alias) || !driver.Volume(voice.Alias, db) || !driver.Run(voice.Alias)) { Fail(voice, "開檔或播放失敗：" + Path.GetFileName(path)); return; }
            voice.Open = true;
        });
        return handle;
    }
    public void SetVolume(long handle, double volume)
    {
        if (handle == 0 || !live.ContainsKey(handle)) return;
        int db = AudioGain.Decibels(AudioGain.Valid(volume) ? volume : 0);
        Post(() => { if (voices.TryGetValue(handle, out var v) && v.Open && !driver.Volume(v.Alias, db)) Fail(v, "調整音量失敗"); });
    }
    public void Stop(long handle)
    {
        if (handle == 0 || !live.TryRemove(handle, out _)) return;
        Post(() => { if (voices.Remove(handle, out var v)) driver.Close(v.Alias); });
    }
    public bool Finished(long handle) => handle == 0 || !live.ContainsKey(handle);

    void Fail(Voice v, string error)
    {
        voices.Remove(v.Handle); live.TryRemove(v.Handle, out _);
        try { driver.Close(v.Alias); } catch { }
        lock (gate) { failed++; lastError = error; }
    }
    /// <summary>命令執行緒每 250ms 一次；只有 ForTests 模式可由外部呼叫。循環音播完就倒回開頭再播（AIL 次數 0）。</summary>
    public void Poll() { if (inline) PollCore(); }
    void PollCore()
    {
        foreach (var v in voices.Values.ToArray())
        {
            if (!v.Open) continue;
            string status = driver.Status(v.Alias);
            if (status == "playing") continue;
            if (status == "ended" && v.Loop)
            {
                if (driver.Rewind(v.Alias) && driver.Run(v.Alias)) { lock (gate) looped++; continue; }
                Fail(v, "循環重播失敗"); continue;
            }
            if (status == "ended") { voices.Remove(v.Handle); live.TryRemove(v.Handle, out _); driver.Close(v.Alias); lock (gate) ended++; continue; }
            Fail(v, "播放狀態錯誤");
        }
    }
    void Work()
    {
        long due = Environment.TickCount64 + PollMs;
        while (true)
        {
            Action? action;
            try { if (!queue!.TryTake(out action, (int)Math.Clamp(due - Environment.TickCount64, 0, PollMs))) action = null; }
            catch (InvalidOperationException) { break; }
            try { action?.Invoke(); } catch (Exception ex) { lock (gate) lastError = ex.Message; }
            if (Environment.TickCount64 >= due)
            {
                try { PollCore(); } catch (Exception ex) { lock (gate) lastError = ex.Message; }
                due = Environment.TickCount64 + PollMs;
            }
            if (queue!.IsCompleted) break;
        }
        foreach (var v in voices.Values.ToArray()) { try { driver.Close(v.Alias); } catch { } }
        voices.Clear();
    }
    /// <summary>停止全部並結束命令執行緒。</summary>
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; }
        foreach (long h in live.Keys.ToArray()) Stop(h);
        if (inline) { foreach (var v in voices.Values.ToArray()) driver.Close(v.Alias); voices.Clear(); return; }
        queue!.CompleteAdding();
    }
}

// 與 WindowsPlaybackGraph 相同的 graph 建法（DSoundRender、RenderEx 只連到既有 renderer），另加 IMediaPosition 倒帶。
internal sealed class DirectShowStreamDriver : IStreamVoiceDriver
{
    readonly Dictionary<string, (object Graph, object Renderer)> graphs = [];
    static object Activate(string clsid) => Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(clsid), true)!)!;
    public bool Open(string path, string voice)
    {
        if (!OperatingSystem.IsWindows() || graphs.ContainsKey(voice)) return false;
        object? graph = null, renderer = null; IBaseFilter? source = null; IEnumPins? pins = null;
        try
        {
            graph = Activate("e436ebb3-524f-11ce-9f53-0020af0ba770");    // CLSID_FilterGraph
            renderer = Activate("79376820-07d0-11cf-a24d-0020afd79767"); // CLSID_DSoundRender
            graphs[voice] = (graph, renderer);
            var builder = (IFilterGraph2)graph;
            if (builder.AddFilter((IBaseFilter)renderer, "P24 stream renderer") < 0) return false;
            if (builder.AddSourceFilter(path, "P24 stream source", out source) < 0) return false;
            if (source.EnumPins(out pins) < 0) return false;
            int rendered = 0;
            while (true)
            {
                var one = new IPin[1];
                if (pins.Next(1, one, out int fetched) < 0 || fetched == 0) break;
                try { if (one[0].QueryDirection(out int dir) >= 0 && dir == 1 && builder.RenderEx(one[0], 1, 0) >= 0) rendered++; }
                finally { Marshal.FinalReleaseComObject(one[0]); }
            }
            return rendered > 0;
        }
        catch { return false; }
        finally
        {
            if (pins != null) Marshal.FinalReleaseComObject(pins);
            if (source != null) Marshal.FinalReleaseComObject(source);
        }
    }
    bool With(string voice, Func<(object Graph, object Renderer), int> work)
    {
        if (!graphs.TryGetValue(voice, out var g)) return false;
        try { return work(g) >= 0; } catch { return false; }
    }
    public bool Volume(string voice, int db) => With(voice, g => ((IBasicAudio)g.Renderer).put_Volume(db));
    public bool Run(string voice) => With(voice, g => ((IMediaControl)g.Graph).Run());
    public bool Rewind(string voice) => With(voice, g => ((IMediaPosition)g.Graph).put_CurrentPosition(0));
    public string Status(string voice)
    {
        if (!graphs.TryGetValue(voice, out var g)) return "failed";
        try
        {
            var events = (IMediaEvent)g.Graph;
            for (int i = 0; i < 64; i++)
            {
                int hr = events.GetEvent(out int code, out nint one, out nint two, 0);
                if (hr == unchecked((int)0x80004004)) return "playing"; // E_ABORT：沒有事件
                if (hr < 0) return "failed";
                events.FreeEventParams(code, one, two);
                if (code == DirectShowEvents.Complete) return "ended";
                if (code is DirectShowEvents.ErrorAbort or DirectShowEvents.StreamErrorStopped or DirectShowEvents.UserAbort or DirectShowEvents.DeviceLost) return "failed";
            }
            return "playing";
        }
        catch { return "failed"; }
    }
    public void Close(string voice)
    {
        if (!graphs.Remove(voice, out var g)) return;
        try { ((IMediaControl)g.Graph).Stop(); } catch { }
        try { Marshal.FinalReleaseComObject(g.Graph); } catch { }
        try { Marshal.FinalReleaseComObject(g.Renderer); } catch { }
    }
}

/// <summary>T310：依副檔名分流，.mid 走 MCI 序列器（原版 8F63D0 的 mciSendCommand 路徑），其他走 DirectShow。</summary>
public sealed class RoutingStreamDriver(IStreamVoiceDriver stream, IStreamVoiceDriver midi) : IStreamVoiceDriver
{
    readonly Dictionary<string, IStreamVoiceDriver> owners = [];
    IStreamVoiceDriver For(string voice) => owners.TryGetValue(voice, out var d) ? d : stream;
    public bool Open(string path, string voice)
    {
        var d = Path.GetExtension(path).Equals(".mid", StringComparison.OrdinalIgnoreCase) ? midi : stream;
        owners[voice] = d;
        return d.Open(path, voice);
    }
    public bool Volume(string voice, int hundredthDecibels) => For(voice).Volume(voice, hundredthDecibels);
    public bool Run(string voice) => For(voice).Run(voice);
    public bool Rewind(string voice) => For(voice).Rewind(voice);
    public string Status(string voice) => For(voice).Status(voice);
    public void Close(string voice) { var d = For(voice); owners.Remove(voice); d.Close(voice); }
}

/// <summary>
/// T310：MCI 序列器。原版用 mciSendCommand＋MCI_NOTIFY 視窗訊息循環；這裡在串流命令執行緒用字串命令、
/// 由 250ms 輪詢看 mode 是否 stopped（播完）再 seek 回開頭重播，效果等同 8F6340 的 MCI_PLAY from 0。
/// 原版不調 MIDI 音量（8F63D0 沒有 MCI_SETAUDIO），所以 Volume 只區分「靜音」與「有聲」：
/// 音量 0 時暫停（推定：避免靜音時 MIDI 仍以裝置音量發聲），恢復音量時接著播。
/// </summary>
internal sealed class MciMidiDriver : IStreamVoiceDriver
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
    static extern int MciSendString(string command, System.Text.StringBuilder? result, int length, nint callback);
    // started＝已真的送過 play；靜音中要求的 Run 先記下，恢復音量時才 play。
    readonly HashSet<string> open = [], muted = [], wanted = [], started = [];
    static bool Send(string command, System.Text.StringBuilder? result = null)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try { return MciSendString(command, result, result?.Capacity ?? 0, 0) == 0; }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }
    // voice 是 AudioStreamPlayer 產生的 "p24s<數字>"；路徑已限制在 sound 目錄、檔名只含英數底線，不含引號。
    public bool Open(string path, string voice)
    {
        if (open.Contains(voice) || path.Contains('"')) return false;
        if (!Send($"open \"{path}\" type sequencer alias {voice}")) return false;
        open.Add(voice); return true;
    }
    public bool Volume(string voice, int hundredthDecibels)
    {
        if (!open.Contains(voice)) return false;
        bool silent = hundredthDecibels <= -10000;
        if (silent == muted.Contains(voice)) return true;
        if (silent) { muted.Add(voice); return !started.Contains(voice) || Send($"pause {voice}"); }
        muted.Remove(voice);
        if (started.Contains(voice)) return Send($"resume {voice}");
        return !wanted.Contains(voice) || Play(voice);
    }
    bool Play(string voice) { if (!Send($"play {voice}")) return false; started.Add(voice); return true; }
    public bool Run(string voice)
    {
        if (!open.Contains(voice)) return false;
        wanted.Add(voice);
        if (muted.Contains(voice)) return true;
        return Play(voice);
    }
    public bool Rewind(string voice) => open.Contains(voice) && Send($"seek {voice} to start");
    public string Status(string voice)
    {
        if (!open.Contains(voice)) return "failed";
        if (muted.Contains(voice)) return "playing"; // 暫停中：不算播完，不重播。
        var mode = new System.Text.StringBuilder(32);
        if (!Send($"status {voice} mode", mode)) return "failed";
        return mode.ToString().Trim().ToLowerInvariant() switch { "playing" or "seeking" => "playing", "stopped" => "ended", _ => "failed" };
    }
    public void Close(string voice)
    {
        muted.Remove(voice); wanted.Remove(voice); started.Remove(voice);
        if (open.Remove(voice)) Send($"close {voice}");
    }
}

[ComImport, Guid("56a868b2-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IMediaPosition
{
    [PreserveSig] int get_Duration(out double length);
    [PreserveSig] int put_CurrentPosition(double time);
}
