// Windows PowerShell 5.1 Add-Type (C# 5); no NuGet, device/settings changes or microphone access.
// WASAPI shared-mode endpoint loopback. Active render sessions are checked before, during (~20 ms) and after capture.
// Other active sessions are metered (IAudioMeterInformation); capture continues only while they stay at or below
// SilenceThreshold. This is NOT process-isolated capture: the report must disclose the polling/endpoint scope.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

public sealed class SmokeLoopbackSession {
    public string Key, Name, Kind, Verdict;
    public uint Pid;
    public int State, Polls, UnmeteredPolls, LastMeterHResult;
    public double MaxPeak;
    public bool CrossProcess, SystemSounds;
}

public sealed class SmokeLoopbackResult {
    public string Path, StartedUtc, CompletedUtc, EndpointId, Error, RejectReason, RejectDetail;
    public int ProcessId, RequestedSeconds, SampleRate, Channels, BitsPerSample, FormatTag;
    public long Frames, Samples, SilentFrames;
    public int Discontinuities, TimestampErrors, SessionChecks, OwnPolls;
    public double Rms, Peak, CapturedSeconds, Threshold, OwnMaxPeak;
    public bool Completed, HasWaveform, RequireExclusive, WavKept;
    public SmokeLoopbackSession[] OtherSessions = new SmokeLoopbackSession[0];
    public string Scope;
}

public sealed class SmokeLoopbackRejectedException : Exception {
    public readonly string Reason;
    public SmokeLoopbackRejectedException(string reason, string detail) : base(reason + ": " + detail) { Reason = reason; }
}

public static class SmokeLoopback {
    // ~ -80 dBFS on the session peak meter (normalised 0..1). Chosen as "inaudible"; constant, echoed in JSON.
    public const double SilenceThreshold = 1e-4;
    public const string Own = "own", Inactive = "inactive", OtherSilent = "other-active-silent",
        OtherAudible = "other-audible", OtherUnmetered = "other-active-unmetered", OtherExclusive = "other-active-exclusive",
        CaptureHost = "capture-host-ignored", OwnNotActive = "own-not-active";

    static void HR(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    static void Release(object o) { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
    static readonly Guid AudioClientId = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    static readonly Guid CaptureId = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
    static readonly Guid SessionManagerId = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    static readonly Guid MeterId = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"); // IAudioMeterInformation

    /// Pure decision for one session observation. isOwn = PID equals the owned client;
    /// crossProcess (AUDCLNT_S_NO_SINGLE_PROCESS) is never treated as own, even if its PID matches.
    public static string ClassifySession(bool isOwn, int state, double peak, double threshold, bool meterOk,
            bool crossProcess, bool requireExclusive) {
        if (state != 1) return Inactive;                 // AudioSessionStateActive == 1
        if (isOwn && !crossProcess) return Own;
        if (requireExclusive) return OtherExclusive;
        if (!meterOk || Double.IsNaN(peak) || Double.IsInfinity(peak) || peak < 0) return OtherUnmetered;
        return peak <= threshold ? OtherSilent : OtherAudible;
    }

    public static bool IsRejecting(string verdict) {
        return verdict == OtherAudible || verdict == OtherUnmetered || verdict == OtherExclusive;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int PeakFn(IntPtr self, out float peak);

    // QueryInterface the session control for IAudioMeterInformation and call GetPeakValue (vtable slot 3)
    // directly, so no extra RCW shares the control's reference count.
    static bool TryGetPeak(object control, out double peak, out int hr) {
        peak = 0; hr = 0; IntPtr unk = IntPtr.Zero, meter = IntPtr.Zero;
        try {
            unk = Marshal.GetIUnknownForObject(control);
            Guid iid = MeterId; hr = Marshal.QueryInterface(unk, ref iid, out meter);
            if (hr < 0 || meter == IntPtr.Zero) return false;
            IntPtr vtable = Marshal.ReadIntPtr(meter);
            var fn = (PeakFn)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size), typeof(PeakFn));
            float value; hr = fn(meter, out value);
            if (hr < 0 || Single.IsNaN(value) || Single.IsInfinity(value)) return false;
            peak = value; return true;
        } catch (Exception) { if (hr >= 0) hr = -1; return false; }
        finally { if (meter != IntPtr.Zero) Marshal.Release(meter); if (unk != IntPtr.Zero) Marshal.Release(unk); }
    }

    static string ProcessName(uint pid, bool systemSounds) {
        if (systemSounds) return "SystemSounds";
        try { using (var p = Process.GetProcessById((int)pid)) return p.ProcessName; } catch (Exception) { return null; }
    }

    sealed class Tracker {
        public readonly Dictionary<string, SmokeLoopbackSession> Others = new Dictionary<string, SmokeLoopbackSession>();
        public readonly List<SmokeLoopbackSession> Order = new List<SmokeLoopbackSession>();
        public bool RequireExclusive;
    }

    static void CheckSessions(ISessionManager manager, int clientPid, SmokeLoopbackResult result, Tracker tracker) {
        ISessionEnumerator sessions = null;
        bool own = false; int hostPid = Process.GetCurrentProcess().Id;
        try {
            HR(manager.GetSessionEnumerator(out sessions));
            int count; HR(sessions.GetCount(out count));
            for (int i = 0; i < count; i++) {
                ISessionControl2 control = null;
                try {
                    HR(sessions.GetSession(i, out control));
                    int state; HR(control.GetState(out state));
                    if (state != 1) continue;
                    uint pid; int pidHr = control.GetProcessId(out pid); HR(pidHr);
                    bool cross = pidHr != 0; // AUDCLNT_S_NO_SINGLE_PROCESS
                    bool system = control.IsSystemSoundsSession() == 0;
                    double peak; int meterHr; bool meterOk = TryGetPeak(control, out peak, out meterHr);
                    bool isOwn = pid == clientPid;
                    string verdict;
                    if (!cross && !system && pid == hostPid && !isOwn) verdict = CaptureHost;
                    else verdict = ClassifySession(isOwn && !system, state, peak, SilenceThreshold, meterOk, cross || system, tracker.RequireExclusive);
                    if (verdict == Own) {
                        own = true; result.OwnPolls++;
                        if (meterOk && peak > result.OwnMaxPeak) result.OwnMaxPeak = peak;
                        continue;
                    }
                    string key = null;
                    try { string id; if (control.GetSessionInstanceIdentifier(out id) >= 0) key = id; } catch (Exception) { }
                    if (String.IsNullOrEmpty(key)) key = "pid:" + pid + (cross ? ":cross" : "") + (system ? ":system" : "");
                    SmokeLoopbackSession s;
                    if (!tracker.Others.TryGetValue(key, out s)) {
                        s = new SmokeLoopbackSession { Key = key, Pid = pid, CrossProcess = cross, SystemSounds = system,
                            Kind = system ? "system-sounds" : cross ? "cross-process" : "process", Name = ProcessName(pid, system), Verdict = verdict };
                        tracker.Others.Add(key, s); tracker.Order.Add(s);
                    }
                    s.State = state; s.Polls++; s.LastMeterHResult = meterHr;
                    if (!meterOk) s.UnmeteredPolls++;
                    else if (peak > s.MaxPeak) s.MaxPeak = peak;
                    s.Verdict = verdict; // a rejecting verdict throws below, so the last verdict is also the worst
                    if (IsRejecting(verdict)) {
                        result.OtherSessions = tracker.Order.ToArray();
                        throw new SmokeLoopbackRejectedException(verdict, "pid " + pid + " (" + (s.Name ?? "?") + ", " + s.Kind + ")" +
                            (meterOk ? " peak " + peak.ToString("R") + " vs threshold " + SilenceThreshold.ToString("R") : " meter hr 0x" + meterHr.ToString("X8")));
                    }
                } finally { Release(control); }
            }
            result.SessionChecks++;
            result.OtherSessions = tracker.Order.ToArray();
            if (!own) throw new SmokeLoopbackRejectedException(OwnNotActive, "owned client PID " + clientPid + " is not actively rendering on the default multimedia endpoint");
        } finally { Release(sessions); }
    }

    public static SmokeLoopbackResult Record(Process clientProcess, string path, int seconds) {
        return Record(clientProcess, path, seconds, false);
    }

    public static SmokeLoopbackResult Record(Process clientProcess, string path, int seconds, bool requireExclusive) {
        if (seconds < 1 || seconds > 30) throw new ArgumentOutOfRangeException("seconds");
        if (clientProcess.HasExited) throw new InvalidOperationException("Owned client already exited");
        var result = new SmokeLoopbackResult { Path = path, ProcessId = clientProcess.Id, RequestedSeconds = seconds, StartedUtc = DateTime.UtcNow.ToString("o"),
            Threshold = SilenceThreshold, RequireExclusive = requireExclusive,
            Scope = requireExclusive
                ? "Default render/eMultimedia endpoint mix; own PID active and no other active render session at ~20 ms polls (-RequireExclusive); not process-isolated"
                : "Default render/eMultimedia endpoint mix; own PID active; every other active render session (incl. cross-process/system sounds) had session meter peak <= Threshold at every ~20 ms poll; sound between polls is not excluded; not process-isolated" };
        var tracker = new Tracker { RequireExclusive = requireExclusive };
        IDeviceEnumerator enumerator = null; IDevice device = null;
        IAudioClient audio = null; ICaptureClient capture = null; ISessionManager manager = null;
        IntPtr formatPointer = IntPtr.Zero; BinaryWriter writer = null;
        bool started = false, created = false, rejected = false; long dataStart = 0; double squares = 0;
        try {
            enumerator = (IDeviceEnumerator)new DeviceEnumerator();
            HR(enumerator.GetDefaultAudioEndpoint(0, 1, out device)); // eRender, eMultimedia
            string endpoint; HR(device.GetId(out endpoint)); result.EndpointId = endpoint;
            object activated; Guid iid = SessionManagerId;
            HR(device.Activate(ref iid, 23, IntPtr.Zero, out activated)); manager = (ISessionManager)activated;
            CheckSessions(manager, clientProcess.Id, result, tracker);
            iid = AudioClientId; HR(device.Activate(ref iid, 23, IntPtr.Zero, out activated)); audio = (IAudioClient)activated;
            HR(audio.GetMixFormat(out formatPointer));
            int extra = (ushort)Marshal.ReadInt16(formatPointer, 16);
            byte[] format = new byte[18 + extra]; Marshal.Copy(formatPointer, format, 0, format.Length);
            int tag = BitConverter.ToUInt16(format, 0), channels = BitConverter.ToUInt16(format, 2);
            int rate = BitConverter.ToInt32(format, 4), align = BitConverter.ToUInt16(format, 12), bits = BitConverter.ToUInt16(format, 14);
            int sampleTag = tag;
            if (tag == 65534) {
                if (extra < 22) throw new NotSupportedException("Invalid WAVEFORMATEXTENSIBLE");
                byte[] guidBytes = new byte[16]; Array.Copy(format, 24, guidBytes, 0, 16);
                var sub = new Guid(guidBytes);
                if (sub == new Guid("00000001-0000-0010-8000-00aa00389b71")) sampleTag = 1;
                else if (sub == new Guid("00000003-0000-0010-8000-00aa00389b71")) sampleTag = 3;
                else throw new NotSupportedException("Unsupported mix subformat " + sub);
            }
            if (!((sampleTag == 3 && bits == 32) || (sampleTag == 1 && (bits == 16 || bits == 24 || bits == 32))) || channels < 1 || align != channels * (bits / 8))
                throw new NotSupportedException("Unsupported endpoint sample format");
            result.SampleRate = rate; result.Channels = channels; result.BitsPerSample = bits; result.FormatTag = tag;
            Guid session = Guid.Empty;
            HR(audio.Initialize(0, 0x20000, 1000000, 0, formatPointer, ref session)); // shared, LOOPBACK, 100 ms
            iid = CaptureId; HR(audio.GetService(ref iid, out activated)); capture = (ICaptureClient)activated;
            writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)); created = true;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(0);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(format.Length); writer.Write(format);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(0); dataStart = writer.BaseStream.Position;
            HR(audio.Start()); started = true; var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds) {
                if (clientProcess.HasExited) throw new InvalidOperationException("Owned client exited during capture");
                CheckSessions(manager, clientProcess.Id, result, tracker);
                uint next; HR(capture.GetNextPacketSize(out next));
                while (next != 0) {
                    IntPtr data; uint frames, flags; ulong position, qpc;
                    HR(capture.GetBuffer(out data, out frames, out flags, out position, out qpc));
                    try {
                        byte[] bytes = new byte[checked((int)frames * align)];
                        bool silent = (flags & 2) != 0;
                        if (!silent) Marshal.Copy(data, bytes, 0, bytes.Length);
                        else result.SilentFrames += frames;
                        if ((flags & 1) != 0) result.Discontinuities++;
                        if ((flags & 4) != 0) result.TimestampErrors++;
                        for (int j = 0; j < bytes.Length; j += bits / 8) {
                            double sample;
                            if (sampleTag == 3) sample = BitConverter.ToSingle(bytes, j);
                            else if (bits == 16) sample = BitConverter.ToInt16(bytes, j) / 32768.0;
                            else if (bits == 32) sample = BitConverter.ToInt32(bytes, j) / 2147483648.0;
                            else { int v = bytes[j] | bytes[j+1] << 8 | bytes[j+2] << 16; if ((v & 0x800000) != 0) v |= unchecked((int)0xff000000); sample = v / 8388608.0; }
                            if (Double.IsNaN(sample) || Double.IsInfinity(sample)) throw new InvalidDataException("Non-finite audio sample");
                            squares += sample * sample; result.Peak = Math.Max(result.Peak, Math.Abs(sample)); result.Samples++;
                        }
                        writer.Write(bytes); result.Frames += frames;
                    } finally { HR(capture.ReleaseBuffer(frames)); }
                    HR(capture.GetNextPacketSize(out next));
                }
                Thread.Sleep(20);
            }
            CheckSessions(manager, clientProcess.Id, result, tracker);
            result.Completed = true;
        } catch (SmokeLoopbackRejectedException ex) {
            rejected = true; result.RejectReason = ex.Reason; result.Error = ex.GetType().Name + ": " + ex.Message;
            result.RejectDetail = ex.Message;
        } catch (Exception ex) { result.Error = ex.GetType().Name + ": " + ex.Message; }
        finally {
            if (started) { try { HR(audio.Stop()); } catch (Exception ex) { result.Completed = false; result.Error = ex.Message; } }
            if (writer != null) {
                try {
                    long length = writer.BaseStream.Length;
                    writer.BaseStream.Position = 4; writer.Write(checked((int)(length - 8)));
                    writer.BaseStream.Position = dataStart - 4; writer.Write(checked((int)(length - dataStart))); writer.Flush();
                } finally { writer.Dispose(); }
            }
            // A rejected capture may contain other programs' audio: do not keep the WAV (CreateNew guarantees we made it).
            if (rejected && created) { try { File.Delete(path); } catch (Exception ex) { result.Error += "; WAV delete failed: " + ex.Message; } }
            result.WavKept = created && File.Exists(path);
            if (result.OtherSessions.Length != tracker.Order.Count) result.OtherSessions = tracker.Order.ToArray();
            if (formatPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(formatPointer);
            Release(capture); Release(audio); Release(manager); Release(device); Release(enumerator);
            result.Rms = result.Samples > 0 ? Math.Sqrt(squares / result.Samples) : 0;
            result.CapturedSeconds = result.SampleRate > 0 ? result.Frames / (double)result.SampleRate : 0;
            result.HasWaveform = result.Completed && result.Samples > 0 && result.Peak > 0.000001;
            result.CompletedUtc = DateTime.UtcNow.ToString("o");
        }
        return result;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDeviceEnumerator {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioClient {
        [PreserveSig] int Initialize(int mode, uint flags, long duration, long period, IntPtr format, ref Guid session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
        [PreserveSig] int Start(); [PreserveSig] int Stop(); [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }
    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ICaptureClient {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong qpc);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionManager {
        [PreserveSig] int GetAudioSessionControl(ref Guid session, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid session, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out ISessionEnumerator sessions);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionEnumerator {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out ISessionControl2 control);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessionControl2 {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
}
