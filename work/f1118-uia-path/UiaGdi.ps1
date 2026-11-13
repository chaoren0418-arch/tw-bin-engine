# Windows PowerShell 5.1; dot-source in a fresh powershell.exe -NoProfile -STA process.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not ('UiaGdiNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class UiaGdiNative {
 [DllImport("user32.dll", SetLastError=true)] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool BringWindowToTop(IntPtr hwnd);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
 [DllImport("user32.dll", SetLastError=true)] static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 [DllImport("kernel32.dll")] static extern void SetLastError(uint code);
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 // Each wrapper clears LastError first, so the recorded code belongs to this call only.
 public static bool SetForegroundWindowLE(IntPtr hwnd, out int error) { SetLastError(0); bool r=SetForegroundWindow(hwnd); error=Marshal.GetLastWin32Error(); return r; }
 public static bool BringWindowToTopLE(IntPtr hwnd, out int error) { SetLastError(0); bool r=BringWindowToTop(hwnd); error=Marshal.GetLastWin32Error(); return r; }
 public static bool AttachThreadInputLE(uint idAttach, uint idAttachTo, bool attach, out int error) { SetLastError(0); bool r=AttachThreadInput(idAttach,idAttachTo,attach); error=Marshal.GetLastWin32Error(); return r; }
 public static bool PrintWindowLE(IntPtr hwnd, IntPtr hdc, uint flags, out int error) { SetLastError(0); bool r=PrintWindow(hwnd,hdc,flags); error=Marshal.GetLastWin32Error(); return r; }
 // Read-only: only SPI_GETFOREGROUNDLOCKTIMEOUT (0x2000) with fWinIni=0; no setter is exposed.
 public static bool GetForegroundLockTimeout(out uint milliseconds, out int error) {
  uint v=0; SetLastError(0); bool r=SystemParametersInfo(0x2000,0,ref v,0); error=Marshal.GetLastWin32Error(); milliseconds=v; return r;
 }
 [DllImport("user32.dll", SetLastError=true)] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
 [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT point, uint flags);
 [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
 [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public UNION data; }
 [StructLayout(LayoutKind.Explicit)] public struct UNION {
  [FieldOffset(0)] public KEYBDINPUT key;
  [FieldOffset(0)] public MOUSEINPUT mouse;
 }
 [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
 [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx,dy; public uint mouseData,flags,time; public UIntPtr extra; }
 [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
 public static void Key(ushort vk) {
  INPUT[] a = new INPUT[2]; a[0].type=a[1].type=1; a[0].data.key.vk=a[1].data.key.vk=vk; a[1].data.key.flags=2;
  if(SendInput(2,a,Marshal.SizeOf(typeof(INPUT)))!=2) throw new System.ComponentModel.Win32Exception();
 }
 public static void UnicodeText(string text, uint expectedProcessId) {
  foreach(char c in text) {
   uint foregroundProcessId;
   IntPtr hwnd = GetForegroundWindow();
   GetWindowThreadProcessId(hwnd, out foregroundProcessId);
   if(foregroundProcessId != expectedProcessId)
    throw new InvalidOperationException("Foreground PID changed before SendInput: HWND=" + hwnd + ", PID=" + foregroundProcessId);
   INPUT[] a = new INPUT[2];
   a[0].type=a[1].type=1;
   a[0].data.key.scan=a[1].data.key.scan=c;
   a[0].data.key.flags=4; // KEYEVENTF_UNICODE
   a[1].data.key.flags=4|2; // KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
   if(SendInput(2,a,Marshal.SizeOf(typeof(INPUT)))!=2)
    throw new System.ComponentModel.Win32Exception();
  }
 }
}
'@
}
$script:ProcessDpiSet = [UiaGdiNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$script:ProcessDpiError = if($script:ProcessDpiSet){0}else{[Runtime.InteropServices.Marshal]::GetLastWin32Error()}
$null = [UiaGdiNative]::SetThreadDpiAwarenessContext([IntPtr](-4))
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing,System.Windows.Forms

function Get-UiaWindow {
    param([Parameter(Mandatory)][int]$ProcessId, [string]$Title)
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
    $found = @([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$condition) | Where-Object { -not $Title -or $_.Current.Name -eq $Title })
    if ($found.Count -ne 1) { throw "Expected one window for PID $ProcessId title '$Title'; found $($found.Count)" }
    return $found[0]
}
function Find-UiaControl {
    param([Parameter(Mandatory)]$Root,[string]$Name,[string]$AutomationId,[string]$Type)
    $all = $Root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
    $found = @($all | Where-Object { (-not $Name -or $_.Current.Name -eq $Name) -and (-not $AutomationId -or $_.Current.AutomationId -eq $AutomationId) -and (-not $Type -or $_.Current.ControlType.ProgrammaticName -eq "ControlType.$Type") })
    if ($found.Count -ne 1) { throw "Expected one control name='$Name' id='$AutomationId' type='$Type'; found $($found.Count)" }
    return $found[0]
}
function Get-UiaTree {
    param([Parameter(Mandatory)]$Root)
    $all = @($Root) + @($Root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition))
    foreach ($element in $all) {
        $c = $element.Current
        [pscustomobject]@{ Name= $(if($c.IsPassword){'[PASSWORD]'}else{$c.Name}); Type=$c.ControlType.ProgrammaticName; Id=$c.AutomationId; Class=$c.ClassName; Enabled=$c.IsEnabled; Offscreen=$c.IsOffscreen; Rectangle=$c.BoundingRectangle.ToString(); Patterns=@($element.GetSupportedPatterns() | ForEach-Object {$_.ProgrammaticName}) }
    }
}
function Invoke-UiaButton { param($Element) ($Element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke() }
function Select-UiaItem { param($Element) ($Element.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select() }
function Expand-UiaCombo { param($Element) ($Element.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand() }
function Get-UiaPatternDescription {
    param([Parameter(Mandatory)]$Element)
    $c=$Element.Current
    $patterns=@($Element.GetSupportedPatterns() | ForEach-Object {$_.ProgrammaticName})
    "ControlType=$($c.ControlType.ProgrammaticName); ClassName=$($c.ClassName); AutomationId=$($c.AutomationId); SupportedPatterns=[$($patterns -join ', ')]"
}
function Set-UiaValue {
    param([Parameter(Mandatory)]$Element,[string]$Value)
    $pattern=$null
    if(-not $Element.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)){
        throw "ValuePattern is unsupported; $(Get-UiaPatternDescription $Element)"
    }
    $pattern.SetValue($Value)
}
function Get-UiaValue { param($Element) ($Element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).Current.Value }
function Get-UiaText {
    param([Parameter(Mandatory)]$Element)
    $pattern=$null
    if($Element.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)){
        return $pattern.Current.Value
    }
    if($Element.TryGetCurrentPattern([Windows.Automation.TextPattern]::Pattern,[ref]$pattern)){
        return $pattern.DocumentRange.GetText(-1)
    }
    throw "Neither ValuePattern nor TextPattern is supported; $(Get-UiaPatternDescription $Element)"
}
function Get-UiaForegroundInfo {
    $h=[UiaGdiNative]::GetForegroundWindow()
    [uint32]$foregroundProcessId=0
    $threadId=if($h -eq [IntPtr]::Zero){[uint32]0}else{[UiaGdiNative]::GetWindowThreadProcessId($h,[ref]$foregroundProcessId)}
    $name=$null
    if($foregroundProcessId -ne 0){
        try {$name=(Get-Process -Id $foregroundProcessId -ErrorAction Stop).ProcessName} catch {$name="<unavailable: $($_.Exception.Message)>"}
    }
    [pscustomobject]@{Handle=$h.ToInt64();ProcessId=$foregroundProcessId;ThreadId=$threadId;ProcessName=$name}
}
function Send-UiaText {
    param([Parameter(Mandatory)]$Element,[Parameter(Mandatory)][AllowEmptyString()][string]$Text)
    $Element.SetFocus()
    $foreground=Get-UiaForegroundInfo
    if($foreground.ProcessId -ne $Element.Current.ProcessId){
        throw "Foreground PID differs from target; HWND=$($foreground.Handle); PID=$($foreground.ProcessId); $(Get-UiaPatternDescription $Element)"
    }
    if(-not $Element.Current.HasKeyboardFocus){throw 'Target element does not have keyboard focus'}
    [UiaGdiNative]::UnicodeText($Text,[uint32]$Element.Current.ProcessId)
}
function Get-UiaWindowInfo {
    param($Window)
    $h=[IntPtr]$Window.Current.NativeWindowHandle
    [pscustomobject]@{Title=$Window.Current.Name;ProcessId=$Window.Current.ProcessId;Handle=$h.ToInt64();Rectangle=$Window.Current.BoundingRectangle.ToString();Minimized=[UiaGdiNative]::IsIconic($h);Dpi=[UiaGdiNative]::GetDpiForWindow($h);ProcessDpiSet=$script:ProcessDpiSet;ProcessDpiError=$script:ProcessDpiError}
}
$script:UiaLastForegroundDiagnostic=$null
function Get-UiaLastForegroundDiagnostic { $script:UiaLastForegroundDiagnostic }
function Wait-UiaForeground {
    # Poll GetForegroundWindow every 50 ms for at most 1 s; returns elapsed ms or -1.
    param([IntPtr]$Handle)
    $sw=[Diagnostics.Stopwatch]::StartNew()
    while($true){
        if([UiaGdiNative]::GetForegroundWindow() -eq $Handle){return [int]$sw.ElapsedMilliseconds}
        if($sw.ElapsedMilliseconds -ge 1000){return -1}
        Start-Sleep -Milliseconds 50
    }
}
function Set-UiaForeground {
    # Multi-step foreground acquisition without changing any system setting:
    #   1) SetForegroundWindow; 2) AttachThreadInput(self, foreground thread) + BringWindowToTop
    #   + SetForegroundWindow, always detached in finally. Up to 3 rounds, each step polled <= 1 s.
    # Never changes system parameters or the registry, never locks foreground changes,
    # never sends synthetic keys to other programs.
    param($Window)
    $h=[IntPtr]$Window.Current.NativeWindowHandle
    [uint32]$targetProcessId=0
    $targetThreadId=[UiaGdiNative]::GetWindowThreadProcessId($h,[ref]$targetProcessId)
    [uint32]$lockTimeout=0;[int]$lockError=0
    $lockOk=[UiaGdiNative]::GetForegroundLockTimeout([ref]$lockTimeout,[ref]$lockError)
    $steps=[Collections.Generic.List[object]]::new()
    $diag=[ordered]@{
        TargetHandle=$h.ToInt64();TargetProcessId=$targetProcessId;TargetThreadId=$targetThreadId
        CallerThreadId=[UiaGdiNative]::GetCurrentThreadId()
        ForegroundBefore=Get-UiaForegroundInfo
        ForegroundLockTimeout=[ordered]@{Ok=$lockOk;Milliseconds=$lockTimeout;LastError=$lockError}
        Steps=$steps;Succeeded=$false;Rounds=0;ForegroundAtEnd=$null;Error=$null
    }
    $script:UiaLastForegroundDiagnostic=$diag
    if([UiaGdiNative]::IsIconic($h)){
        $diag.Error='Window is minimized; restore deliberately before capture'
        throw $diag.Error
    }
    $addStep={
        param($round,$name,$ret,$err,$extra)
        $wait=Wait-UiaForeground $h
        $step=[ordered]@{Round=$round;Step=$name;Returned=$ret;LastError=$err;ForegroundAfterMs=$wait}
        if($null -ne $extra){foreach($k in $extra.Keys){$step[$k]=$extra[$k]}}
        $steps.Add([pscustomobject]$step)
        return ($wait -ge 0)
    }
    for($round=1;$round -le 3 -and -not $diag.Succeeded;$round++){
        $diag.Rounds=$round
        [int]$e=0
        $r=[UiaGdiNative]::SetForegroundWindowLE($h,[ref]$e)
        if(& $addStep $round 'SetForegroundWindow' $r $e $null){$diag.Succeeded=$true;break}

        $fg=Get-UiaForegroundInfo
        $self=[UiaGdiNative]::GetCurrentThreadId()
        if($fg.Handle -eq 0 -or $fg.ThreadId -eq 0 -or $fg.ThreadId -eq $self){
            # Nothing to attach to (no foreground window, e.g. secure desktop/lock screen) or already same thread.
            $steps.Add([pscustomobject][ordered]@{Round=$round;Step='AttachThreadInput';Skipped="foreground HWND=$($fg.Handle) thread=$($fg.ThreadId) self=$self";Foreground=$fg})
            continue
        }
        [int]$ae=0
        $attached=$false
        try {
            $attached=[UiaGdiNative]::AttachThreadInputLE($self,$fg.ThreadId,$true,[ref]$ae)
            $steps.Add([pscustomobject][ordered]@{Round=$round;Step='AttachThreadInput(attach)';Returned=$attached;LastError=$ae;FromThread=$self;ToThread=$fg.ThreadId;Foreground=$fg})
            [int]$be=0
            $br=[UiaGdiNative]::BringWindowToTopLE($h,[ref]$be)
            if(& $addStep $round 'BringWindowToTop' $br $be $null){$diag.Succeeded=$true}
            if(-not $diag.Succeeded){
                [int]$se=0
                $sr=[UiaGdiNative]::SetForegroundWindowLE($h,[ref]$se)
                if(& $addStep $round 'SetForegroundWindow(attached)' $sr $se $null){$diag.Succeeded=$true}
            }
        } finally {
            if($attached){
                [int]$de=0
                $detached=[UiaGdiNative]::AttachThreadInputLE($self,$fg.ThreadId,$false,[ref]$de)
                $steps.Add([pscustomobject][ordered]@{Round=$round;Step='AttachThreadInput(detach)';Returned=$detached;LastError=$de;FromThread=$self;ToThread=$fg.ThreadId})
            }
        }
    }
    $diag.ForegroundAtEnd=Get-UiaForegroundInfo
    if(-not $diag.Succeeded){
        $fg=$diag.ForegroundAtEnd
        $summary=@($steps | ForEach-Object {
            if($_.PSObject.Properties['Skipped']){"r$($_.Round) $($_.Step)=skipped"}else{"r$($_.Round) $($_.Step)=$($_.Returned)/LE$($_.LastError)"}
        }) -join '; '
        $diag.Error="Foreground window does not match target after $($diag.Rounds) round(s): target HWND=$($h.ToInt64()) PID=$targetProcessId; foreground HWND=$($fg.Handle) PID=$($fg.ProcessId) Name=$($fg.ProcessName); ForegroundLockTimeout=$lockTimeout ms (ok=$lockOk, LE$lockError); steps: $summary"
        throw $diag.Error
    }
    return [pscustomobject]$diag
}
function Send-UiaF12 {
    param($Window)
    $null=Set-UiaForeground $Window
    [UiaGdiNative]::Key(0x7B)
}
function Measure-UiaBitmap {
    param([Parameter(Mandatory)][Drawing.Bitmap]$Bitmap)
    [double]$sum=0;[long]$nonzero=0;[long]$nonwhite=0;[long]$count=0
    for($y=0;$y -lt $Bitmap.Height;$y+=4){for($x=0;$x -lt $Bitmap.Width;$x+=4){
        $p=$Bitmap.GetPixel($x,$y);$v=[int]$p.R+[int]$p.G+[int]$p.B
        $sum+=$v/3.0;$count++;if($v -gt 0){$nonzero++};if($v -lt 765){$nonwhite++}
    }}
    [pscustomobject]@{SampleStride=4;Samples=$count;Mean=$sum/$count;NonzeroRatio=$nonzero/$count;NonwhiteRatio=$nonwhite/$count;Nonblank=($nonzero -gt 0 -and $nonwhite -gt 0)}
}
function Save-UiaGdiScreenshot {
    # PrintWindow(PW_RENDERFULLCONTENT=2) first: does not need foreground and works when covered.
    # Only if PrintWindow returns false or the image is uniformly black/white, fall back to
    # Set-UiaForeground + CopyFromScreen. Both paths' error texts are kept in the result.
    param([Parameter(Mandatory)]$Window,[Parameter(Mandatory)][string]$Path)
    $full=[IO.Path]::GetFullPath($Path)
    $h=[IntPtr]$Window.Current.NativeWindowHandle
    if([UiaGdiNative]::IsIconic($h)){throw 'Window is minimized; restore deliberately before capture'}
    $printError=$null;$copyError=$null;$foreground=$null
    $method=$null;$stats=$null;$width=0;$height=0

    $rect=New-Object UiaGdiNative+RECT
    if(-not [UiaGdiNative]::GetWindowRect($h,[ref]$rect)){
        $printError="GetWindowRect failed; LastError=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    } elseif(($rect.Right-$rect.Left) -le 0 -or ($rect.Bottom-$rect.Top) -le 0){
        $printError="GetWindowRect returned empty rectangle $($rect.Left),$($rect.Top),$($rect.Right),$($rect.Bottom)"
    } else {
        $pw=$rect.Right-$rect.Left;$ph=$rect.Bottom-$rect.Top
        # 24bpp avoids GDI leaving alpha=0 (transparent PNG) in a 32bpp ARGB target.
        $bitmap=[Drawing.Bitmap]::new($pw,$ph,[Drawing.Imaging.PixelFormat]::Format24bppRgb)
        try {
            $graphics=[Drawing.Graphics]::FromImage($bitmap)
            [int]$pe=0;$ok=$false
            try {
                $hdc=$graphics.GetHdc()
                try {$ok=[UiaGdiNative]::PrintWindowLE($h,$hdc,2,[ref]$pe)} finally {$graphics.ReleaseHdc($hdc)}
            } finally {$graphics.Dispose()}
            if(-not $ok){
                $printError="PrintWindow(PW_RENDERFULLCONTENT) returned false; LastError=$pe"
            } else {
                $s=Measure-UiaBitmap $bitmap
                if(-not $s.Nonblank){
                    $printError="PrintWindow image is uniformly black or white (Mean=$($s.Mean), NonzeroRatio=$($s.NonzeroRatio), NonwhiteRatio=$($s.NonwhiteRatio))"
                } else {
                    $bitmap.Save($full,[Drawing.Imaging.ImageFormat]::Png)
                    $method='PrintWindow';$stats=$s;$width=$pw;$height=$ph
                }
            }
        } catch {
            if($null -eq $printError){$printError=$_.Exception.ToString()}
        } finally {$bitmap.Dispose()}
    }

    if($null -eq $method){
        try {
            $foreground=Set-UiaForeground $Window
            $r=$Window.Current.BoundingRectangle
            $bounds=[Windows.Forms.SystemInformation]::VirtualScreen
            $left=[Math]::Max([int][Math]::Floor($r.Left),$bounds.Left)
            $top=[Math]::Max([int][Math]::Floor($r.Top),$bounds.Top)
            $cw=[Math]::Min([int][Math]::Ceiling($r.Right),$bounds.Right)-$left
            $ch=[Math]::Min([int][Math]::Ceiling($r.Bottom),$bounds.Bottom)-$top
            if($cw -le 0 -or $ch -le 0){throw 'Window rectangle is outside the desktop'}
            $bitmap=[Drawing.Bitmap]::new($cw,$ch)
            $graphics=[Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.CopyFromScreen($left,$top,0,0,$bitmap.Size,[Drawing.CopyPixelOperation]::SourceCopy)
                $bitmap.Save($full,[Drawing.Imaging.ImageFormat]::Png)
                $stats=Measure-UiaBitmap $bitmap
                $method='CopyFromScreen';$width=$cw;$height=$ch
            } finally {$graphics.Dispose();$bitmap.Dispose()}
        } catch {
            $copyError=$_.Exception.ToString()
            if($null -eq $foreground){$foreground=Get-UiaLastForegroundDiagnostic}
        }
    }
    if($null -eq $method){
        throw "Screenshot failed on both paths. PrintWindow: $printError | CopyFromScreen: $copyError"
    }
    [pscustomobject]@{Path=$full;Method=$method;PrintWindowError=$printError;CopyFromScreenError=$copyError;Width=$width;Height=$height;SampleStride=$stats.SampleStride;Samples=$stats.Samples;Mean=$stats.Mean;NonzeroRatio=$stats.NonzeroRatio;NonwhiteRatio=$stats.NonwhiteRatio;Nonblank=$stats.Nonblank;Sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $full).Hash;Foreground=$foreground;Window=(Get-UiaWindowInfo $Window)}
}
