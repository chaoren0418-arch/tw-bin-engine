using System.Runtime.InteropServices;

namespace P24.Audio;

internal sealed record NativeReply(uint Code, string Text, bool PlatformFailure = false, bool ResourceOwned = false,
    AudioNativeEvent? NativeEvent = null, bool EventFailure = false)
{
    internal bool Ok => Code == 0 && !PlatformFailure && !EventFailure;
    internal bool InvalidFile => Code == 0x8004022f; // VFW_E_INVALID_FILE_FORMAT; never classify arbitrary device failure as format.
}
internal static class DirectShowEvents
{
    internal const int Complete=0x01, UserAbort=0x02, ErrorAbort=0x03, StreamErrorStopped=0x06,
        ErrorStillPlaying=0x08, DeviceLost=0x1f, ClockUnset=0x51;
    internal static NativeReply Classify(int code, nint one, nint two)
    {
        // Copy scalar values before FreeEventParams. Do not dereference or keep string/interface pointers.
        var data = new AudioNativeEvent(code, (long)one, (long)two, null, "Information");
        if (code == Complete) return new(0, "stopped", NativeEvent: data with { Disposition="Complete" });
        if (code is ErrorAbort or StreamErrorStopped or ErrorStillPlaying)
        {
            uint hr = unchecked((uint)(long)one); // LONG_PTR may be sign- or zero-extended; HRESULT is low 32 bits.
            return new(hr, $"DirectShow error event 0x{code:X}; HRESULT=0x{hr:X8}",
                NativeEvent: data with { OriginalHResult=hr, Disposition="ErrorHResult" }, EventFailure:true);
        }
        string? policy = code switch
        {
            UserAbort => "UserAbort",
            DeviceLost when two == 0 => "DeviceRemoved",
            DeviceLost when two == 1 => "DeviceAvailableRequiresRebuild",
            DeviceLost => "UnknownDeviceStateRequiresRebuild",
            ClockUnset => "ClockUnsetConservativeStop",
            _ => null
        };
        // These events have no HRESULT parameter. E_FAIL is an explicit engine failure marker, not a pointer cast.
        return policy == null ? new(0, "information", NativeEvent:data)
            : new(0x80004005, $"DirectShow event 0x{code:X}: {policy}; engine E_FAIL marker (no native HRESULT)",
                NativeEvent:data with { Disposition=policy }, EventFailure:true);
    }
}
internal interface IAudioDriver
{
    NativeReply Open(string path, string format, string voice);
    NativeReply Gain(string voice, double normalized);
    // T310（R5-E）：左右聲像，單位同 IBasicAudio.put_Balance（1/100 dB，負＝右聲道衰減）。沒有這個能力的替身回成功、不動作。
    NativeReply Balance(string voice, int hundredthDecibels) => new(0, "BalanceUnsupported");
    NativeReply Play(string voice);
    NativeReply Status(string voice);
    NativeReply Stop(string voice);
    NativeReply Close(string voice);
}
internal interface IPlaybackGraph
{
    int Open(string path);
    int Volume(int hundredthDecibels);
    int Balance(int hundredthDecibels) => 0;
    int Run();
    NativeReply Status();
    int Stop();
    void Release();
}
internal static class AudioGain
{
    internal static bool Valid(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
    internal static int Decibels(double value)
    {
        if (!Valid(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return value == 0 ? -10000 : (int)Math.Clamp(Math.Round(2000 * Math.Log10(value)), -10000, 0);
    }

    // T310（R5-E）：ext 是 Miles 的 pan，不是額外音量（已驗證，客戶端 Mss32.dll，見 docs/reports/issue-737.md）：
    //   AIL_quick_set_volume(h, vol, ext/128)（RVA 0x1A870）→ 內部 0x051D0 → AIL_set_sample_volume_pan(s, vol, pan)（RVA 0x14F10）
    //   → 0x2A9E0：pan＝0.5 時左右都是 v·0.5^0.3（0.81225）；否則 左＝(1−pan)^0.3·v、右＝pan^0.3·v，v＝vol^(5/3)。
    // 這裡只換成左右比：balance＝20·log10(右/左)（1/100 dB）。中央（ext 64）＝0，不改既有響度；ext 0＝右聲道全關。
    internal const int CenterExt = 64;
    internal static bool ValidExt(int ext) => ext is >= 0 and <= 128;
    internal static (double Left, double Right) PanLevels(int ext)
    {
        if (!ValidExt(ext)) throw new ArgumentOutOfRangeException(nameof(ext));
        double pan = ext / 128d;
        return (Math.Pow(1 - pan, 0.3), Math.Pow(pan, 0.3));
    }
    internal static int Balance(int ext)
    {
        if (ext == CenterExt) return 0;
        var (left, right) = PanLevels(ext);
        if (right <= 0) return -10000;
        if (left <= 0) return 10000;
        return (int)Math.Clamp(Math.Round(2000 * Math.Log10(right / left)), -10000, 10000);
    }

    // R7-L（T351）：Mss32 0x2A9E0 的完整左右振幅，只當資料（真機再決定，M26）。播放路徑仍用 Decibels＋Balance，沒有改響度。
    //   v＝vol^(5/3)（常數 1.6667 在 RVA 0x4BF78）；pan＝0.5 時左右都是 v·0.5^0.3（0.8122522，RVA 0x4BF70）；
    //   否則 左＝(1−pan)^0.3·v、右＝pan^0.3·v（指數 0.3 在 RVA 0x4BF68）。vol、pan 都是 0..1。
    internal const double MilesVolumeExponent = 5d / 3, MilesPanExponent = 0.3;
    internal static readonly double MilesCenter = Math.Pow(0.5, MilesPanExponent);
    internal static (double Left, double Right) MilesLevels(double volume, double pan)
    {
        if (!Valid(volume)) throw new ArgumentOutOfRangeException(nameof(volume));
        if (!Valid(pan)) throw new ArgumentOutOfRangeException(nameof(pan));
        double v = Math.Pow(volume, MilesVolumeExponent);
        if (pan == 0.5) return (v * MilesCenter, v * MilesCenter);
        return (Math.Pow(1 - pan, MilesPanExponent) * v, Math.Pow(pan, MilesPanExponent) * v);
    }
    /// <summary>資料：Miles 在某一聲道的振幅換成 1/100 dB（同 IBasicAudio 單位），供真機聽音比較；0 → -10000。</summary>
    internal static int MilesDecibels(double amplitude)
        => amplitude <= 0 ? -10000 : (int)Math.Clamp(Math.Round(2000 * Math.Log10(amplitude)), -10000, 0);
}
internal sealed class DirectShowDriver(Func<IPlaybackGraph> create) : IAudioDriver
{
    readonly Dictionary<string, IPlaybackGraph> graphs = [];
    internal static NativeReply HResult(int hr) => hr >= 0 ? new(0, "accepted") : new(unchecked((uint)hr), $"HRESULT 0x{hr:X8}");
    static NativeReply Failure(Exception ex) => new(unchecked((uint)ex.HResult), ex.Message,
        ex is PlatformNotSupportedException or DllNotFoundException || ex.HResult == unchecked((int)0x80040154));
    static NativeReply Guard(Func<NativeReply> work) { try { return work(); } catch (Exception ex) { return Failure(ex); } }
    NativeReply With(string voice, Func<IPlaybackGraph, NativeReply> work) => Guard(() =>
        graphs.TryGetValue(voice, out var graph) ? work(graph) : new(0x80070006, "Unknown voice"));
    public NativeReply Open(string path, string format, string voice)
    {
        return Guard(() =>
        {
            if (graphs.ContainsKey(voice)) return new(0x80070057, "Duplicate voice");
            var graph = create(); graphs.Add(voice, graph); // Track before render: failed loading may own partial native resources.
            NativeReply result = Guard(() => HResult(graph.Open(path)));
            return result with { ResourceOwned = true };
        });
    }
    public NativeReply Gain(string voice, double normalized) => With(voice, g => HResult(g.Volume(AudioGain.Decibels(normalized))));
    public NativeReply Balance(string voice, int hundredthDecibels) => With(voice, g => HResult(g.Balance(Math.Clamp(hundredthDecibels, -10000, 10000))));
    public NativeReply Play(string voice) => With(voice, g => HResult(g.Run()));
    public NativeReply Status(string voice) => With(voice, g => g.Status());
    public NativeReply Stop(string voice) => With(voice, g => HResult(g.Stop()));
    public NativeReply Close(string voice) => Guard(() =>
    {
        if (!graphs.TryGetValue(voice, out var graph)) return new(0, "Already released");
        graph.Release(); // If release throws, keep the graph tracked for cleanup retry.
        graphs.Remove(voice); return new(0, "Released");
    });
}

// Instantiated only on the transport MTA thread, after source verification and dispatch gating.
// Each voice owns its own graph and explicitly inserted DirectSound renderer. No system/session-volume API.
internal sealed class WindowsPlaybackGraph : IPlaybackGraph
{
    object? graph, renderer;
    static object Activate(string clsid) => Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(clsid), true)!)!;
    static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    static void ReleaseObject(ref object? value)
    {
        if (value == null) return;
        Marshal.FinalReleaseComObject(value); value = null;
    }
    public int Open(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DirectShow requires Windows");
        graph = Activate("e436ebb3-524f-11ce-9f53-0020af0ba770"); // CLSID_FilterGraph
        renderer = Activate("79376820-07d0-11cf-a24d-0020afd79767"); // CLSID_DSoundRender
        var builder = (IFilterGraph2)graph;
        Check(builder.AddFilter((IBaseFilter)renderer, "P24 isolated voice renderer"));
        IBaseFilter? source = null; IEnumPins? pins = null;
        try
        {
            Check(builder.AddSourceFilter(path, "P24 pinned local audio", out source));
            Check(source.EnumPins(out pins));
            int rendered = 0;
            while (true)
            {
                var next = new IPin[1]; int hr = pins.Next(1, next, out int fetched);
                if (hr < 0) Check(hr);
                if (fetched == 0) break;
                try
                {
                    Check(next[0].QueryDirection(out int direction));
                    if (direction != 1) continue;
                    // AM_RENDEREX_RENDERTOEXISTINGRENDERERS=1: never add WaveOut/other renderers as fallback.
                    Check(builder.RenderEx(next[0], 1, 0)); rendered++;
                }
                finally { Marshal.FinalReleaseComObject(next[0]); }
            }
            return rendered > 0 ? 0 : unchecked((int)0x8004022f);
        }
        finally
        {
            if (pins != null) Marshal.FinalReleaseComObject(pins);
            if (source != null) Marshal.FinalReleaseComObject(source);
        }
    }
    public int Volume(int hundredthDecibels) => ((IBasicAudio)renderer!).put_Volume(hundredthDecibels);
    public int Balance(int hundredthDecibels) => ((IBasicAudio)renderer!).put_Balance(hundredthDecibels);
    public int Run() => ((IMediaControl)graph!).Run();
    public int Stop() => graph == null ? 0 : ((IMediaControl)graph).Stop();
    public NativeReply Status()
    {
        var events = (IMediaEvent)graph!;
        AudioNativeEvent? lastInformation = null;
        for (int i = 0; i < 64; i++) // Bounded drain; no GUI/message callback or infinite wait.
        {
            int hr = events.GetEvent(out int code, out nint one, out nint two, 0);
            if (hr == unchecked((int)0x80004004)) return new(0, "playing", NativeEvent:lastInformation); // E_ABORT means no queued event.
            if (hr < 0) return DirectShowDriver.HResult(hr);
            NativeReply classified;
            int freed = 0; Exception? freeException = null;
            try { classified = DirectShowEvents.Classify(code, one, two); }
            finally
            {
                // Exactly one attempt for every successfully obtained event, including ignored information.
                try { freed = events.FreeEventParams(code, one, two); }
                catch (Exception ex) { freed=ex.HResult; freeException=ex; }
            }
            classified = classified with { NativeEvent=classified.NativeEvent! with { FreeParametersHResult=freed } };
            if (freed < 0 || freeException != null)
            {
                // Preserve primary event HRESULT if present; expose cleanup failure separately, never overwrite it.
                return classified with {
                    Code=classified.Ok ? unchecked((uint)freed) : classified.Code,
                    Text=classified.Text+$"; FreeEventParams failed 0x{freed:X8}"+(freeException == null ? "" : "; "+freeException.Message),
                    EventFailure=true
                };
            }
            if (!classified.Ok || classified.Text == "stopped") return classified;
            lastInformation = classified.NativeEvent;
        }
        return new(0, "playing", NativeEvent:lastInformation);
    }
    public void Release()
    {
        // Stop is attempted separately even if it fails. Release every owned RCW; retain any failure for retry.
        Exception? error = null;
        try { ReleaseObject(ref graph); } catch (Exception ex) { error = ex; }
        try { ReleaseObject(ref renderer); } catch (Exception ex) { error ??= ex; }
        if (error != null) throw error;
    }
}
