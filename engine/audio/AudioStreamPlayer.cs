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
    static readonly Regex SafeName = new(@"^[A-Za-z0-9_]{1,40}\.(mp3|wav)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    readonly string root;
    readonly IStreamVoiceDriver driver;
    readonly Func<string, bool> exists;
    readonly int capacity;
    readonly bool inline;
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

    AudioStreamPlayer(string soundRoot, IStreamVoiceDriver driver, Func<string, bool> exists, int capacity, bool inline)
    {
        root = Path.GetFullPath(soundRoot); this.driver = driver; this.exists = exists; this.capacity = capacity; this.inline = inline;
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
        return new(Path.Combine(clientRoot, "sound"), new DirectShowStreamDriver(), File.Exists, capacity, false);
    }
    /// <summary>離線測試：命令同步執行，Poll 由測試呼叫。</summary>
    public static AudioStreamPlayer ForTests(string soundRoot, IStreamVoiceDriver driver, Func<string, bool> exists, int capacity = 48)
        => new(soundRoot, driver, exists, capacity, true);

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
        string? path = null;
        foreach (string name in candidates)
        {
            if (!SafeName.IsMatch(name)) continue; // 只接受產品組出的單一檔名，不接受路徑。
            string full = Path.GetFullPath(Path.Combine(root, name));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (exists(full)) { path = full; break; }
        }
        long handle;
        lock (gate)
        {
            if (disposed || path == null || live.Count >= capacity) { rejected++; if (path == null) lastError = "缺檔：" + string.Join("／", candidates); return 0; }
            handle = ++next; started++;
        }
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

[ComImport, Guid("56a868b2-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IMediaPosition
{
    [PreserveSig] int get_Duration(out double length);
    [PreserveSig] int put_CurrentPosition(double time);
}
