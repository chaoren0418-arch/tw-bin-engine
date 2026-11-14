# f1186-retest helpers. Dot-source after UiaGdi.ps1. Windows PowerShell 5.1.
# No product reflection, packet injection, UIA setters, database access or process discovery.
Set-StrictMode -Version Latest

function Read-SmokeText {
    param([Parameter(Mandatory)][string]$Path)
    $stream=[IO.File]::Open([IO.Path]::GetFullPath($Path),[IO.FileMode]::Open,[IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::UTF8,$true)
        try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}

function Read-SmokeJson {
    # A shared handle prevents reader-induced sharing violations; retry torn/truncated writes separately.
    param([Parameter(Mandatory)][string]$Path,[ValidateRange(1,40)][int]$Attempts=12)
    for($i=0;$i -lt $Attempts;$i++) {
        try {
            $text=Read-SmokeText $Path
            if([string]::IsNullOrWhiteSpace($text)){throw 'Empty JSON during write'}
            $value=ConvertFrom-Json -InputObject $text -ErrorAction Stop
            if($null -eq $value){throw 'Null JSON during write'}
            return $value
        } catch {
            if($i -eq $Attempts-1){throw}
            Start-Sleep -Milliseconds 50
        }
    }
}

function Save-SmokeJson {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)]$Value)
    $full=[IO.Path]::GetFullPath($Path)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full)) | Out-Null
    [IO.File]::WriteAllText($full,($Value | ConvertTo-Json -Depth 60),[Text.UTF8Encoding]::new($false))
}

function Copy-SmokeState {
    param([Parameter(Mandatory)][string]$Source,[Parameter(Mandatory)][string]$Destination)
    if([IO.Path]::GetFullPath($Source) -eq [IO.Path]::GetFullPath($Destination)){throw 'Source and destination must differ'}
    $state=Read-SmokeJson $Source
    Save-SmokeJson $Destination $state
    [pscustomobject]@{Path=[IO.Path]::GetFullPath($Destination);Sha256=(Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash;State=$state}
}

function Get-SmokeTraceMark {
    param([Parameter(Mandatory)][string]$Path)
    # Mark only complete lines. A partial pre-existing row must not count as a new send.
    $text=Read-SmokeText $Path
    [pscustomobject]@{Path=[IO.Path]::GetFullPath($Path);Prefix=$text;Utc=[DateTime]::UtcNow.ToString('o')}
}

function Read-SmokeTraceSince {
    param([Parameter(Mandatory)]$Mark)
    $text=Read-SmokeText $Mark.Path
    if(-not $text.StartsWith($Mark.Prefix,[StringComparison]::Ordinal)){throw 'Trace was truncated/replaced; send status is unknown'}
    $tail=$text.Substring($Mark.Prefix.Length)
    if($Mark.Prefix.Length -gt 0 -and -not $Mark.Prefix.EndsWith("`n")){
        $first=$tail.IndexOf("`n");if($first -lt 0){return};$tail=$tail.Substring($first+1)
    }
    $end=$tail.LastIndexOf("`n");if($end -lt 0){return}
    foreach($line in ($tail.Substring(0,$end) -split "`n")){
        if(-not [string]::IsNullOrWhiteSpace($line)) { ConvertFrom-Json -InputObject $line -ErrorAction Stop }
    }
}

function Measure-SmokeInputInk {
    param([Parameter(Mandatory)]$Bitmap,[Parameter(Mandatory)]$Geometry,[Parameter(Mandatory)][int[]]$InputRect)
    if($InputRect.Count -ne 4 -or $InputRect[2] -lt 20 -or $InputRect[3] -lt 8){throw 'InputRect must cover the full text area (x,y,width,height), excluding its border'}
    $r=Convert-ClientLogicalRect $Geometry $InputRect
    if($r.X -lt 0 -or $r.Y -lt 0 -or $r.X+$r.Width -gt $Bitmap.Width -or $r.Y+$r.Height -gt $Bitmap.Height){throw 'Input rectangle outside capture'}
    $ink=0;$probe=0
    # Native UiText draws white, single-bit glyphs. Omit only the empty caret at the first two logical pixels.
    $skip=[Math]::Ceiling(2*$Geometry.Scale);$probeEnd=[Math]::Ceiling(14*$Geometry.Scale)
    for($y=$r.Y;$y -lt $r.Y+$r.Height;$y++){
        for($x=$r.X+$skip;$x -lt $r.X+$r.Width;$x++){
            $c=$Bitmap.GetPixel($x,$y)
            if($c.R -ge 245 -and $c.G -ge 245 -and $c.B -ge 245){$ink++;if($x -lt $r.X+$probeEnd){$probe++}}
        }
    }
    [pscustomobject]@{WhitePixels=$ink;ProbePixels=$probe;Rect=$r}
}

function Clear-SmokeChat {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$EvidencePrefix,[Parameter(Mandatory)][int[]]$InputRect,[switch]$Fill)
    $state=Read-SmokeJson $StatePath
    if($state.state -ne 'InWorld' -or -not $state.world.renderable){throw 'Chat requires a renderable InWorld client'}
    # The channel tabs overlap the left end of CHAT_BAR; click its centre, not the text origin.
    $null=Send-ClientClick $Hwnd ($InputRect[0]+$InputRect[2]/2) ($InputRect[1]+$InputRect[3]/2) -Fill:$Fill
    # Native ChatInputLine does not implement Ctrl+A/Delete. Esc preserves its draft.
    for($i=0;$i -lt 106;$i++){ $null=Send-ClientKey $Hwnd 'Backspace' }
    Start-Sleep -Milliseconds 180
    $g=Get-ClientWindowGeometry $Hwnd -Fill:$Fill
    $before=Get-GdiClientBitmap $Hwnd
    try {
        $blank=Measure-SmokeInputInk $before.Bitmap $g $InputRect
        $blankCapture=Save-GdiClientBitmap $before ($EvidencePrefix+'-empty.png')
    } finally {$before.Bitmap.Dispose()}
    if($blank.WhitePixels -ne 0){throw "Input not visually empty ($($blank.WhitePixels) white pixels); no command sent"}
    # Positive control: verifies that the bar is open and this rectangle actually contains its input origin.
    Send-ClientText $Hwnd 'W'
    Start-Sleep -Milliseconds 150
    try {
        $probe=Get-GdiClientBitmap $Hwnd
        try {
            $probeInk=Measure-SmokeInputInk $probe.Bitmap $g $InputRect
            $probeCapture=Save-GdiClientBitmap $probe ($EvidencePrefix+'-probe.png')
        } finally {$probe.Bitmap.Dispose()}
    } finally { $null=Send-ClientKey $Hwnd 'Backspace' }
    Start-Sleep -Milliseconds 150
    $after=Get-GdiClientBitmap $Hwnd
    try {
        $afterInk=Measure-SmokeInputInk $after.Bitmap $g $InputRect
        $afterCapture=Save-GdiClientBitmap $after ($EvidencePrefix+'-verified-empty.png')
    } finally {$after.Bitmap.Dispose()}
    if($probeInk.ProbePixels -lt 5*$g.Scale*$g.Scale -or $afterInk.WhitePixels -ne 0){throw 'Empty-line positive control failed; no command sent'}
    $result=[pscustomobject]@{Empty=$true;Method='106 Backspace + GDI white-glyph check + W positive control';Before=$blankCapture;Probe=$probeCapture;After=$afterCapture;BeforeInk=$blank;ProbeInk=$probeInk;AfterInk=$afterInk}
    Save-SmokeJson ($EvidencePrefix+'-clear.json') $result
    $result
}

function Send-SmokeCommand {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$TracePath,[Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][ValidateRange(0,255)][int]$ExpectedOpcode,
        [int[]]$ResponseOpcodes=@(),[ValidateRange(1,30)][int]$TimeoutSeconds=4,
        [Parameter(Mandatory)][string]$EvidencePrefix,[Parameter(Mandatory)][int[]]$InputRect,[switch]$Fill)
    if($Command.Length -eq 0 -or $Command.Length -gt 106 -or $Command -match '[\r\n\x00-\x1f]'){throw 'Command must be one nonempty line within the native 106-character limit'}
    $result=[ordered]@{Command=$Command;ExpectedOpcode=$ExpectedOpcode;ResponseOpcodes=$ResponseOpcodes;SendConfirmed=$false;ResponseObserved=$false;Status='not-attempted';Error=$null}
    try {
        # Require a working trace before touching the draft; TW_PACKET_TRACE must be set before launching the client.
        $null=Get-SmokeTraceMark $TracePath
        $result.Clear=Clear-SmokeChat $Hwnd $StatePath $EvidencePrefix $InputRect -Fill:$Fill
        Send-ClientText $Hwnd $Command
        Start-Sleep -Milliseconds 150
        $result.Typed=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-typed.png')
        $mark=Get-SmokeTraceMark $TracePath
        $result.SubmitUtc=[DateTime]::UtcNow.ToString('o')
        $null=Send-ClientKey $Hwnd 'Enter'
        $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds);$rows=@()
        do {
            Start-Sleep -Milliseconds 100
            $rows=@(Read-SmokeTraceSince $mark)
            $sent=@($rows | Where-Object {$_.dir -eq 'out' -and $_.op -eq $ExpectedOpcode})
            if($sent.Count -gt 0){
                $result.SendConfirmed=$true
                $responses=@($rows | Where-Object {$_.dir -eq 'in' -and $ResponseOpcodes -contains [int]$_.op -and [datetime]$_.utc -ge [datetime]$sent[0].utc})
                $result.ResponseObserved=$responses.Count -gt 0
                if($result.ResponseObserved){break}
            }
        } while([DateTime]::UtcNow -lt $deadline)
        $result.TraceRows=$rows
        $result.Status=if(-not $result.SendConfirmed){'send-not-observed'}elseif($ResponseOpcodes.Count -eq 0){'sent-response-not-specified'}elseif($result.ResponseObserved){'sent-expected-response-observed'}else{'sent-no-expected-response-in-window'}
        $result.After=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-after.png')
        $result.State=Copy-SmokeState $StatePath ($EvidencePrefix+'-state.json')
    } catch { $result.Error=$_.Exception.Message;$result.Status='unknown-or-precondition-failed' }
    Save-SmokeJson ($EvidencePrefix+'-send.json') $result
    [pscustomobject]$result
}

function Convert-SmokeObjectPoint {
    param([Parameter(Mandatory)]$State,[Parameter(Mandatory)][uint32]$ObjectId,
        [ValidateRange(0.35,3)][double]$Zoom=1.5,[double[]]$BodyOffset=@(0,-24))
    if($State.state -ne 'InWorld' -or -not $State.world.renderable){throw 'Object selection requires a renderable world'}
    $a=$State.world.actor;$objects=@($State.world.objects | Where-Object ObjectId -EQ $ObjectId)
    if($null -eq $a -or $objects.Count -ne 1){throw 'Object must occur exactly once in current visible-object state'}
    $o=$objects[0]
    if($o.Dead -or $o.AppearancePending){throw 'Object is dead or its appearance is not ready'}
    if($BodyOffset.Count -ne 2){throw 'BodyOffset must be scene-pixel x,y'}
    # Coordinates.Screen + Camera.ToView; native WorldRect = (0,0,800,600), Follow=true, actor stopped.
    $dx=[double]$o.X-$a.X;$dy=[double]$o.Y-$a.Y
    $x=400+(24*($dx+$dy)+$BodyOffset[0])*$Zoom
    $y=300+(12*($dy-$dx)+$BodyOffset[1])*$Zoom
    if($x -lt 0 -or $x -ge 800 -or $y -lt 0 -or $y -ge 470){throw 'Projected body is outside the uncovered native scene'}
    [pscustomobject]@{ObjectId=$ObjectId;ActorId=$a.ObjectId;ActorX=$a.X;ActorY=$a.Y;ObjectX=$o.X;ObjectY=$o.Y;X=$x;Y=$y;Zoom=$Zoom;BodyOffset=$BodyOffset;Map=$State.world.mapId;Epoch=$State.world.worldEpoch;Revision=$State.revision}
}

function Select-SmokeObject {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][uint32]$ObjectId,[Parameter(Mandatory)][string]$EvidencePrefix,
        [ValidateRange(0.35,3)][double]$Zoom=1.5,[double[]]$BodyOffset=@(0,-24),
        [ValidateRange(1,30)][int]$TimeoutSeconds=5,[switch]$Fill)
    $before=Read-SmokeJson $StatePath
    $point=Convert-SmokeObjectPoint $before $ObjectId -Zoom $Zoom -BodyOffset $BodyOffset
    $result=[ordered]@{Point=$point;Selected=$false;BeforeTarget=$before.world.targetId;AfterTarget=$null;Error=$null}
    try {
        if($before.world.targetId -eq $ObjectId){throw 'Already selected; clear selection first to prove this click'}
        $result.Before=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-before.png')
        $result.Click=Send-ClientClick $Hwnd $point.X $point.Y -Fill:$Fill
        $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 100;$after=Read-SmokeJson $StatePath
            if($after.state -ne 'InWorld' -or $after.world.worldEpoch -ne $point.Epoch -or $after.world.mapId -ne $point.Map){throw 'World changed during selection'}
            $result.AfterTarget=$after.world.targetId
            $result.Selected=($after.revision -gt $point.Revision -and $after.world.targetId -eq $ObjectId)
            if($result.Selected){break}
        } while([DateTime]::UtcNow -lt $deadline)
        $result.After=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-after.png')
        $result.State=Copy-SmokeState $StatePath ($EvidencePrefix+'-state.json')
        if(-not $result.Selected){$result.Error='Click not confirmed by a newer targetId snapshot; no automatic retry'}
    } catch {$result.Error=$_.Exception.Message}
    Save-SmokeJson ($EvidencePrefix+'-select.json') $result
    [pscustomobject]$result
}

function Close-SmokeClient {
    # Pass the actual Process returned by Start-Process (or the owned child with verified creation time).
    param([Parameter(Mandatory)][Diagnostics.Process]$Process,[Parameter(Mandatory)][IntPtr]$Hwnd,
        [Parameter(Mandatory)][string]$EventsPath,[ValidateRange(1,60)][int]$TimeoutSeconds=15)
    $closed=$false;$keyError=$null;$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    if(-not $Process.HasExited){
        if((Get-ClientWindowProcessId $Hwnd) -ne $Process.Id){throw 'Window does not belong to the owned process'}
        try {$null=Send-ClientKey $Hwnd 'Alt+F4'} catch {$keyError=$_.Exception.Message}
    }
    do {
        if(Test-Path -LiteralPath $EventsPath){
            try {$events=Read-SmokeJson $EventsPath;$closed=@($events | Where-Object {$_.PSObject.Properties['kind'] -and $_.kind -eq 'closed'}).Count -gt 0} catch {}
        }
        if($closed -and $Process.HasExited){break}
        Start-Sleep -Milliseconds 100
    } while([DateTime]::UtcNow -lt $deadline)
    # Do not race FormClosed or kill the launcher when its child is still draining. Caller decides on timeout cleanup.
    [pscustomobject]@{ProcessId=$Process.Id;Closed=$closed;Exited=$Process.HasExited;Passed=($closed -and $Process.HasExited);KeyError=$keyError}
}

# #1230: C# source was SmokeLoopback.cs; .gitignore (work/*) only re-includes work/f1118-uia-path/*.ps1, so it is inlined here.
function Import-SmokeLoopback {
    if('SmokeLoopback' -as [type]){return}
    Add-Type -TypeDefinition @'
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
'@
}

function Record-SmokeLoopback {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process,[Parameter(Mandatory)][string]$Path,
        [ValidateRange(1,30)][int]$Seconds=3,
        # Old strict mode: any other active render session rejects, even if its meter is silent.
        [switch]$RequireExclusive)
    if($Process.HasExited){throw 'Owned client has exited'}
    $repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $scratch=[IO.Path]::GetFullPath((Join-Path $repo 'scratch'))+[IO.Path]::DirectorySeparatorChar
    $full=[IO.Path]::GetFullPath($Path)
    if(-not $full.StartsWith($scratch,[StringComparison]::OrdinalIgnoreCase)){throw 'Loopback output must be under this worktree scratch/'}
    # Reject junctions on the output path, including an existing output file.
    for($p=$full;$p -and $p.Length -ge $scratch.TrimEnd('\').Length;$p=[IO.Path]::GetDirectoryName($p)){
        if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Loopback output must not follow a junction/symlink'}
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full)) | Out-Null
    Import-SmokeLoopback
    # other-audible / other-active-unmetered: record as skipped (environment); never close other programs.
    $result=[SmokeLoopback]::Record($Process,$full,$Seconds,[bool]$RequireExclusive)
    Save-SmokeJson ($full+'.json') $result
    $result
}
