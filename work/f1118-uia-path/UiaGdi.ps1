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
    param([Parameter(Mandatory)][int]$ProcessId, [string]$Title, [switch]$IncludeOwned)
    $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
    $windowCondition=[Windows.Automation.AndCondition]::new($condition,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Window))
    $script:UiaReadErrors=@()
    $roots = @([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$windowCondition))
    $candidates = @($roots)
    if($IncludeOwned){
        foreach($root in $roots){
            try {$candidates += @($root.FindAll([Windows.Automation.TreeScope]::Descendants,$windowCondition))}
            catch {$script:UiaReadErrors+= [pscustomobject]@{Error=$_.Exception.ToString()}}
        }
    }
    $seen=@{}
    $found = @(foreach($element in $candidates){
        try {
            $c=$element.Current
            if($c.ProcessId -ne $ProcessId -or $c.ControlType -ne [Windows.Automation.ControlType]::Window){continue}
            if($PSBoundParameters.ContainsKey('Title') -and -not [string]::Equals($c.Name,$Title,[StringComparison]::Ordinal)){continue}
            $identity=$element.GetRuntimeId() -join ','
            if(-not $seen.ContainsKey($identity)){$seen[$identity]=$true;$element}
        } catch {$script:UiaReadErrors+= [pscustomobject]@{Error=$_.Exception.ToString()}}
    })
    if ($found.Count -ne 1) { throw "Expected one window for PID $ProcessId title '$Title'; found $($found.Count); read errors: $(@($script:UiaReadErrors | ForEach-Object {$_.Error}) -join ' | ')" }
    return $found[0]
}
$script:UiaReadErrors=@()
function Get-UiaReadErrors { $script:UiaReadErrors }
function Read-UiaElement {
    # A disappearing element is evidence, not a reason to discard the other nodes.
    param($Element,[switch]$MaskPassword)
    try {
        $c=$Element.Current
        foreach($property in @('Name','ControlType','AutomationId','ClassName','BoundingRectangle')){
            if($null -eq $c.$property){throw "UIA property '$property' is null"}
        }
        if($null -eq $c.ControlType.ProgrammaticName){throw "UIA ControlType.ProgrammaticName is null"}
        $patterns=@(foreach($pattern in $Element.GetSupportedPatterns()){
            if($null -eq $pattern -or $null -eq $pattern.ProgrammaticName){throw 'UIA pattern ProgrammaticName is null'}
            $pattern.ProgrammaticName
        })
        [pscustomobject]@{Name=$(if($MaskPassword -and $c.IsPassword){'[PASSWORD]'}else{$c.Name});Type=$c.ControlType.ProgrammaticName;Id=$c.AutomationId;Class=$c.ClassName;Enabled=$c.IsEnabled;Offscreen=$c.IsOffscreen;Rectangle=$c.BoundingRectangle.ToString();Patterns=$patterns}
    } catch { [pscustomobject]@{Error=$_.Exception.ToString()} }
}
function Find-UiaControl {
    param([Parameter(Mandatory)]$Root,[string]$Name,[string]$AutomationId,[string]$Type,[ref]$ReadErrors)
    $script:UiaReadErrors=@()
    $all=@()
    try {$all=@($Root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition))}
    catch {$script:UiaReadErrors+= [pscustomobject]@{Error=$_.Exception.ToString()}}
    $found = @(foreach($element in $all){
        $c=Read-UiaElement $element
        if($c.PSObject.Properties['Error']){$script:UiaReadErrors+=$c;continue}
        if((-not $Name -or [string]::Equals($c.Name,$Name,[StringComparison]::Ordinal)) -and
           (-not $AutomationId -or [string]::Equals($c.Id,$AutomationId,[StringComparison]::Ordinal)) -and
           (-not $Type -or $c.Type -ceq "ControlType.$Type")){$element}
    })
    if($null -ne $ReadErrors){$ReadErrors.Value=@($script:UiaReadErrors)}
    if ($found.Count -ne 1) { throw "Expected one control name='$Name' id='$AutomationId' type='$Type'; found $($found.Count); read errors: $(@($script:UiaReadErrors | ForEach-Object {$_.Error}) -join ' | ')" }
    return $found[0]
}
function Get-UiaTree {
    param([Parameter(Mandatory)]$Root)
    $script:UiaReadErrors=@()
    $all=@($Root)
    try {$all+=@($Root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition))}
    catch {$errorNode=[pscustomobject]@{Error=$_.Exception.ToString()};$script:UiaReadErrors+=$errorNode;$errorNode}
    foreach ($element in $all) {
        $node=Read-UiaElement $element -MaskPassword
        if($node.PSObject.Properties['Error']){$script:UiaReadErrors+=$node}
        $node
    }
}
if(-not ('UiaBoundedInvoke' -as [type])){
    Add-Type -ReferencedAssemblies UIAutomationClient,UIAutomationTypes -TypeDefinition @'
using System;
using System.Threading;
using System.Windows.Automation;
public static class UiaBoundedInvoke {
 public static void Invoke(AutomationElement element, int milliseconds) {
  Run(() => ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke(), milliseconds);
 }
 static void Run(Action action, int milliseconds) {
  Exception error = null;
  // Background MTA: a blocked provider must not block the caller or process exit.
  // No Abort, synchronous Dispose/Stop, or automatic retry after the deadline.
  Thread worker = new Thread(() => {
   try { action(); }
   catch(Exception ex) { error = ex; }
  });
  worker.IsBackground = true;
  worker.SetApartmentState(ApartmentState.MTA);
  worker.Start();
  if(!worker.Join(milliseconds)) throw new TimeoutException("UIA Invoke exceeded " + milliseconds + " ms");
  if(error != null) throw new InvalidOperationException("UIA Invoke provider error: " + error.ToString(), error);
 }
}
'@
}
function Invoke-UiaButton {
    param([Parameter(Mandatory)]$Element,[ValidateRange(0.001,300)][double]$TimeoutSeconds=5)
    try {[UiaBoundedInvoke]::Invoke($Element,[int]($TimeoutSeconds*1000))}
    catch {throw "Invoke-UiaButton failed (TimeoutSeconds=$TimeoutSeconds): $($_.Exception.ToString())`n動作可能已生效，先截圖核對，不要重送"}
}
function Select-UiaTab {
    param([Parameter(Mandatory)]$Tab,[Parameter(Mandatory)][string]$Name)
    $item=Find-UiaControl $Tab -Name $Name -Type TabItem
    ($item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    # Read both sides independently, even if one side fails, preserving provider text.
    $itemState=$null;$tabState=$null;$itemError=$null;$tabError=$null
    try {
        $itemState=[pscustomobject]@{Name=$item.Current.Name;IsSelected=($item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected}
    } catch {$itemError=$_.Exception.ToString()}
    try {
        $selection=($Tab.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern)).GetCurrentSelection()
        $tabState=@($selection | ForEach-Object {$_.Current.Name})
    } catch {$tabError=$_.Exception.ToString()}
    $observation=[pscustomobject]@{RequestedName=$Name;TabItem=$itemState;TabSelection=$tabState;TabItemError=$itemError;TabSelectionError=$tabError}
    if($null -ne $itemError -or $null -ne $tabError -or -not $itemState.IsSelected -or
       -not [string]::Equals($itemState.Name,$Name,[StringComparison]::Ordinal) -or $tabState.Count -ne 1 -or
       -not [string]::Equals($tabState[0],$Name,[StringComparison]::Ordinal)){
        throw "Select-UiaTab verification failed: $($observation | ConvertTo-Json -Depth 5 -Compress)"
    }
    $observation
}
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
function Send-UiaEvidenceKey {
    param([Parameter(Mandatory)]$Window)
    $null=Set-UiaForeground $Window
    $foreground=Get-UiaForegroundInfo
    if($foreground.ProcessId -ne $Window.Current.ProcessId){throw 'Foreground PID differs from evidence target'}
    # Timestamp precedes injection, so evidence written during SendInput is included.
    $sentUtc=[DateTime]::UtcNow
    $null=Send-ClientKey ([IntPtr]$Window.Current.NativeWindowHandle) 'Ctrl+Shift+F12'
    return $sentUtc
}
function Get-UiaNewEvidence {
    param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][DateTime]$SinceUtc)
    Get-ChildItem -LiteralPath $Directory -Filter 'manual-*.png' -File -ErrorAction Stop |
        Where-Object {$_.LastWriteTimeUtc -gt $SinceUtc.ToUniversalTime()} | Sort-Object Name
}
function Send-UiaF12 {
    # 舊名只為相容保留；現在送 Ctrl+Shift+F12，不再送單鍵 F12。
    param([Parameter(Mandatory)]$Window)
    Send-UiaEvidenceKey $Window
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

# ---------------------------------------------------------------------------
# Native UI client (#1154): client-area GDI capture and client-coordinate SendInput.
# The game canvas is not UIA; these work on a raw HWND and an 800x600 logical frame.
# Process DPI awareness is already set to PER_MONITOR_AWARE_V2 (-4) at the top of this file
# (failure only recorded in $script:ProcessDpiSet/ProcessDpiError). The client is PMv2 as well
# (engine/source/Program.cs:15 SetHighDpiMode(PerMonitorV2), :102 AutoScaleMode.None), so every
# rectangle below is in physical pixels in both processes. No WGC; no system setting is changed.
# ---------------------------------------------------------------------------
$script:ClientInputNativeSource=@'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class ClientInputNative {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx,dy; public uint mouseData,dwFlags,time; public UIntPtr dwExtraInfo; }
 [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk,wScan; public uint dwFlags,time; public UIntPtr dwExtraInfo; }
 [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
 // x64: type(4) + padding(4) + union(32; MOUSEINPUT 4*5 + padding 4 + pointer 8) = 40 bytes.
 [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }
 [DllImport("user32.dll", SetLastError=true)] public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
 [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
 [DllImport("gdi32.dll", SetLastError=true)] static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
 [DllImport("kernel32.dll")] static extern void SetLastError(uint code);
 public const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
 public static int InputSize { get { return Marshal.SizeOf(typeof(INPUT)); } }

 public static uint ForegroundProcessId(out IntPtr hwnd) { uint pid; hwnd = GetForegroundWindow(); GetWindowThreadProcessId(hwnd, out pid); return pid; }

 // Foreground owner is checked in native code right before SendInput (no PowerShell gap in between).
 // Returns SendInput's count and error = GetLastError of that call (cleared first). When the foreground window
 // does not belong to expectedPid nothing is sent: returns 0 with error = -1.
 public static uint SendGuarded(INPUT[] inputs, uint expectedPid, out int error, out long foregroundHwnd, out uint foregroundPid) {
  IntPtr fg; foregroundPid = ForegroundProcessId(out fg); foregroundHwnd = fg.ToInt64();
  if (foregroundPid != expectedPid) { error = -1; return 0; }
  SetLastError(0); uint n = SendInput((uint)inputs.Length, inputs, InputSize); error = Marshal.GetLastWin32Error(); return n;
 }
 public static INPUT Mouse(int dx, int dy, uint flags) { INPUT i = new INPUT(); i.type = 0; i.u.mi.dx = dx; i.u.mi.dy = dy; i.u.mi.dwFlags = flags; return i; }
 public static INPUT Key(ushort vk, ushort scan, uint flags) { INPUT i = new INPUT(); i.type = 1; i.u.ki.wVk = vk; i.u.ki.wScan = scan; i.u.ki.dwFlags = flags; return i; }

 public static bool BitBltScreenLE(IntPtr hdcDest, int x, int y, int w, int h, out int error) {
  IntPtr screen = GetDC(IntPtr.Zero);
  if (screen == IntPtr.Zero) { error = Marshal.GetLastWin32Error(); return false; }
  try { SetLastError(0); bool r = BitBlt(hdcDest, 0, 0, w, h, screen, x, y, SRCCOPY | CAPTUREBLT); error = Marshal.GetLastWin32Error(); return r; }
  finally { ReleaseDC(IntPtr.Zero, screen); }
 }

 // Every-pixel statistics via LockBits (GetPixel is too slow for 0.5 s polling). {pixels, mean, nonBlackRatio, nonWhiteRatio}.
 public static double[] Stats(Bitmap b) {
  Rectangle r = new Rectangle(0, 0, b.Width, b.Height);
  BitmapData d = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
  try {
   byte[] row = new byte[d.Stride]; double sum = 0; long nonBlack = 0, nonWhite = 0, n = (long)b.Width * b.Height;
   for (int y = 0; y < b.Height; y++) {
    Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), row, 0, d.Stride);
    for (int x = 0; x < b.Width; x++) { int v = row[x*3] + row[x*3+1] + row[x*3+2]; sum += v / 3.0; if (v > 0) nonBlack++; if (v < 765) nonWhite++; }
   }
   return new double[] { n, n == 0 ? 0 : sum / n, n == 0 ? 0 : (double)nonBlack / n, n == 0 ? 0 : (double)nonWhite / n };
  } finally { b.UnlockBits(d); }
 }

 // Pixels with |dR|+|dG|+|dB| > threshold. Returns {changedTotal, changedInside, insideArea, bboxLeft, bboxTop, bboxRight, bboxBottom}
 // (bbox = -1 when nothing changed). Inside rectangle = (ix, iy, iw, ih) in bitmap pixels. Both bitmaps must have equal size.
 public static long[] Diff(Bitmap a, Bitmap b, int threshold, int ix, int iy, int iw, int ih) {
  if (a.Width != b.Width || a.Height != b.Height) throw new ArgumentException("bitmap sizes differ");
  Rectangle r = new Rectangle(0, 0, a.Width, a.Height);
  BitmapData da = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
  try {
   BitmapData db = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
   try {
    byte[] ra = new byte[da.Stride], rb = new byte[db.Stride];
    long total = 0, inside = 0; int l = int.MaxValue, t = int.MaxValue, rr = -1, bb = -1;
    for (int y = 0; y < a.Height; y++) {
     Marshal.Copy(new IntPtr(da.Scan0.ToInt64() + (long)y * da.Stride), ra, 0, da.Stride);
     Marshal.Copy(new IntPtr(db.Scan0.ToInt64() + (long)y * db.Stride), rb, 0, db.Stride);
     for (int x = 0; x < a.Width; x++) {
      int k = x * 3; int dv = Math.Abs(ra[k]-rb[k]) + Math.Abs(ra[k+1]-rb[k+1]) + Math.Abs(ra[k+2]-rb[k+2]);
      if (dv <= threshold) continue;
      total++; if (x < l) l = x; if (y < t) t = y; if (x > rr) rr = x; if (y > bb) bb = y;
      if (x >= ix && x < ix + iw && y >= iy && y < iy + ih) inside++;
     }
    }
    return new long[] { total, inside, (long)iw * ih, total == 0 ? -1 : l, total == 0 ? -1 : t, rr, bb };
   } finally { b.UnlockBits(db); }
  } finally { a.UnlockBits(da); }
 }
}
'@
if (-not ('ClientInputNative' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition $script:ClientInputNativeSource
}

function Get-ClientWindowGeometry {
    # Client area in screen pixels (GetClientRect + ClientToScreen) and the engine's 800x600 viewport.
    # Default mapping follows UiPresenter.Compute with fill=false (engine/source/NativeUi/UiPresenter.cs:42-44):
    #   k = max(1, min(floor(w/800), floor(h/600))), viewport offset = ((w-800k)/2, (h-600k)/2).
    # -Fill (native-ui.json "fill": true) follows UiPresenter.cs:37-40 (float scale, rounded size).
    # RatioX/RatioY (= w/800, h/600) are recorded as-is; RatioNote is set when they differ or are not 1x/2x.
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[switch]$Fill)
    if(-not [ClientInputNative]::IsWindow($Hwnd)){throw "HWND $($Hwnd.ToInt64()) is not a window"}
    $cr=New-Object ClientInputNative+RECT
    if(-not [ClientInputNative]::GetClientRect($Hwnd,[ref]$cr)){throw "GetClientRect failed; LastError=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())"}
    $pt=New-Object ClientInputNative+POINT
    if(-not [ClientInputNative]::ClientToScreen($Hwnd,[ref]$pt)){throw "ClientToScreen failed; LastError=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())"}
    $wr=New-Object ClientInputNative+RECT
    $null=[ClientInputNative]::GetWindowRect($Hwnd,[ref]$wr)
    [uint32]$owner=0;$null=[ClientInputNative]::GetWindowThreadProcessId($Hwnd,[ref]$owner)
    $w=$cr.Right-$cr.Left;$h=$cr.Bottom-$cr.Top
    if($Fill){
        $s=[Math]::Min($w/800.0,$h/600.0);if(-not ($s -gt 0)){$s=1.0}
        $dw=[int][Math]::Round(800*$s,[MidpointRounding]::ToEven);$dh=[int][Math]::Round(600*$s,[MidpointRounding]::ToEven)
        $scale=[double]$s;$mode='fill'
    } else {
        $k=[Math]::Max(1,[Math]::Min([Math]::Floor($w/800),[Math]::Floor($h/600)))
        $dw=[int](800*$k);$dh=[int](600*$k);$scale=[double]$k;$mode='integer'
    }
    $ox=[int][Math]::Truncate(($w-$dw)/2);$oy=[int][Math]::Truncate(($h-$dh)/2)
    $ratioX=$w/800.0;$ratioY=$h/600.0
    $note=$null
    if($ratioX -ne $ratioY -or @(1.0,2.0) -notcontains $ratioX){$note="client ${w}x${h}: width/800=$ratioX height/600=$ratioY (not a uniform 1x/2x); clicks use $mode scale $scale with viewport offset ($ox,$oy)"}
    [pscustomobject][ordered]@{
        Hwnd=$Hwnd.ToInt64();ProcessId=$owner;Dpi=[ClientInputNative]::GetDpiForWindow($Hwnd)
        ClientWidth=$w;ClientHeight=$h;ClientScreenLeft=$pt.X;ClientScreenTop=$pt.Y
        WindowRect=@($wr.Left,$wr.Top,$wr.Right,$wr.Bottom)
        Mapping=$mode;Scale=$scale;ViewportLeft=$ox;ViewportTop=$oy;ViewportWidth=$dw;ViewportHeight=$dh
        RatioX=$ratioX;RatioY=$ratioY;RatioNote=$note
    }
}
function Convert-ClientLogicalToScreen {
    # 800x600 logical point -> client pixel -> screen pixel (centre of the scaled logical pixel).
    param([Parameter(Mandatory)]$Geometry,[Parameter(Mandatory)][double]$X,[Parameter(Mandatory)][double]$Y)
    $cx=[int][Math]::Floor($Geometry.ViewportLeft+($X+0.5)*$Geometry.Scale)
    $cy=[int][Math]::Floor($Geometry.ViewportTop+($Y+0.5)*$Geometry.Scale)
    [pscustomobject]@{ClientX=$cx;ClientY=$cy;ScreenX=$Geometry.ClientScreenLeft+$cx;ScreenY=$Geometry.ClientScreenTop+$cy}
}
function Convert-ClientLogicalRect {
    # 800x600 logical rectangle [x,y,w,h] -> client pixel rectangle (for capture diffs).
    param([Parameter(Mandatory)]$Geometry,[Parameter(Mandatory)][int[]]$Rect)
    $s=$Geometry.Scale
    [pscustomobject]@{X=[int][Math]::Floor($Geometry.ViewportLeft+$Rect[0]*$s);Y=[int][Math]::Floor($Geometry.ViewportTop+$Rect[1]*$s);Width=[int][Math]::Ceiling($Rect[2]*$s);Height=[int][Math]::Ceiling($Rect[3]*$s)}
}

# One JSONL line per click / key batch. Default path is the worktree's scratch/; Test-NativeClient.ps1 points it
# at <evidence>/client-input.jsonl.
$script:ClientInputLogPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../scratch/client-input.jsonl'))
function Set-ClientInputLog { param([Parameter(Mandatory)][string]$Path) $script:ClientInputLogPath=[IO.Path]::GetFullPath($Path) }
function Write-ClientInputLog {
    param([Parameter(Mandatory)][Collections.IDictionary]$Entry)
    $dir=Split-Path -Parent $script:ClientInputLogPath
    if(-not (Test-Path -LiteralPath $dir)){New-Item -ItemType Directory -Force $dir | Out-Null}
    $line=([pscustomobject]$Entry | ConvertTo-Json -Depth 6 -Compress)
    [IO.File]::AppendAllText($script:ClientInputLogPath,$line+"`n",[Text.UTF8Encoding]::new($false))
}

function Get-ClientWindowProcessId { param([IntPtr]$Hwnd) [uint32]$p=0;$null=[ClientInputNative]::GetWindowThreadProcessId($Hwnd,[ref]$p);$p }
function Assert-ClientForeground {
    # Foreground must belong to the client's PID. If not, run #1148's multi-step Set-UiaForeground once and re-check;
    # still not -> throw (callers send nothing). Returns what happened.
    param([Parameter(Mandatory)][IntPtr]$Hwnd)
    $clientPid=Get-ClientWindowProcessId $Hwnd
    if($clientPid -eq 0){throw "HWND $($Hwnd.ToInt64()) has no owning process"}
    $fg=Get-UiaForegroundInfo
    if($fg.ProcessId -eq $clientPid){return [pscustomobject]@{Acquired=$false;Foreground=$fg;Diagnostic=$null}}
    $element=[Windows.Automation.AutomationElement]::FromHandle($Hwnd)
    $diag=$null
    try {$diag=Set-UiaForeground $element} catch {
        throw "Client is not foreground and Set-UiaForeground failed; nothing was sent. $($_.Exception.Message)"
    }
    $fg=Get-UiaForegroundInfo
    if($fg.ProcessId -ne $clientPid){throw "Foreground PID $($fg.ProcessId) (HWND $($fg.Handle)) is not client PID $clientPid after Set-UiaForeground; nothing was sent"}
    [pscustomobject]@{Acquired=$true;Foreground=$fg;Diagnostic=$diag}
}

function Send-ClientInputBatch {
    # Internal: one guarded SendInput call + one JSONL line. Throws when refused or partially sent.
    param([IntPtr]$Hwnd,[object[]]$Inputs,[Collections.IDictionary]$Entry)
    $clientPid=Get-ClientWindowProcessId $Hwnd
    $array=[ClientInputNative+INPUT[]]::new($Inputs.Count)
    for($i=0;$i -lt $Inputs.Count;$i++){$array[$i]=$Inputs[$i]}
    [int]$err=0;[long]$fgh=0;[uint32]$fgp=0
    $n=[ClientInputNative]::SendGuarded($array,$clientPid,[ref]$err,[ref]$fgh,[ref]$fgp)
    $Entry['Utc']=[DateTime]::UtcNow.ToString('o');$Entry['Hwnd']=$Hwnd.ToInt64();$Entry['ProcessId']=$clientPid
    $Entry['ForegroundHwnd']=$fgh;$Entry['ForegroundProcessId']=$fgp
    $Entry['InputCount']=$array.Length;$Entry['SendInputReturned']=$n;$Entry['LastError']=$err;$Entry['InputSize']=[ClientInputNative]::InputSize
    Write-ClientInputLog $Entry
    if($err -eq -1){throw "Foreground PID $fgp (HWND $fgh) is not client PID $clientPid; SendInput not called ($($Entry['Action']))"}
    if($n -ne $array.Length){throw "SendInput sent $n of $($array.Length) ($($Entry['Action'])); LastError=$err"}
}
function Copy-ClientLogEntry { param([Collections.IDictionary]$Source,[string]$Action) $e=[ordered]@{};foreach($k in $Source.Keys){$e[$k]=$Source[$k]};$e['Action']=$Action;$e }

function Send-ClientClick {
    # X/Y are 800x600 logical client coordinates. One SendInput batch: MOUSEEVENTF_MOVE|ABSOLUTE|VIRTUALDESK
    # (normalised 0-65535 over the virtual desktop), then button down/up at the same point.
    # Refuses (throws, sends nothing) when the client is not foreground after Set-UiaForeground, or when the target
    # screen point is covered by another top-level window.
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][double]$X,[Parameter(Mandatory)][double]$Y,
          [ValidateSet('Left','Right')][string]$Button='Left',[switch]$Double,[switch]$Fill)
    if($X -lt 0 -or $X -ge 800 -or $Y -lt 0 -or $Y -ge 600){throw "Logical point ($X,$Y) is outside 800x600"}
    $fgCheck=Assert-ClientForeground $Hwnd
    $g=Get-ClientWindowGeometry $Hwnd -Fill:$Fill
    $p=Convert-ClientLogicalToScreen $g $X $Y
    $sp=New-Object ClientInputNative+POINT;$sp.X=$p.ScreenX;$sp.Y=$p.ScreenY
    $hit=[ClientInputNative]::WindowFromPoint($sp);$hitRoot=[ClientInputNative]::GetAncestor($hit,2)   # GA_ROOT
    $vx=[ClientInputNative]::GetSystemMetrics(76);$vy=[ClientInputNative]::GetSystemMetrics(77)          # SM_X/YVIRTUALSCREEN
    $vw=[ClientInputNative]::GetSystemMetrics(78);$vh=[ClientInputNative]::GetSystemMetrics(79)          # SM_CX/CYVIRTUALSCREEN
    $nx=[int][Math]::Round(($p.ScreenX-$vx)*65535.0/[Math]::Max(1,$vw-1));$ny=[int][Math]::Round(($p.ScreenY-$vy)*65535.0/[Math]::Max(1,$vh-1))
    $action="click-$($Button.ToLowerInvariant())"
    if($Double){$action+='-double'}
    $entry=[ordered]@{
        Action=$action;LogicalX=$X;LogicalY=$Y
        ClientX=$p.ClientX;ClientY=$p.ClientY;ScreenX=$p.ScreenX;ScreenY=$p.ScreenY;NormalizedX=$nx;NormalizedY=$ny
        Scale=$g.Scale;Mapping=$g.Mapping;RatioX=$g.RatioX;RatioY=$g.RatioY;RatioNote=$g.RatioNote
        ClientWidth=$g.ClientWidth;ClientHeight=$g.ClientHeight;Dpi=$g.Dpi
        VirtualDesk=@($vx,$vy,$vw,$vh);HitHwnd=$hit.ToInt64();HitRootHwnd=$hitRoot.ToInt64();ForegroundAcquired=$fgCheck.Acquired
    }
    if($hitRoot -ne $Hwnd){
        $entry['Refused']="screen point hits root HWND $($hitRoot.ToInt64()), not the client"
        $entry['Utc']=[DateTime]::UtcNow.ToString('o');$entry['Hwnd']=$Hwnd.ToInt64();Write-ClientInputLog $entry
        throw "Refusing click: screen point ($($p.ScreenX),$($p.ScreenY)) hits HWND $($hitRoot.ToInt64()), not client $($Hwnd.ToInt64())"
    }
    $absolute=0x0001 -bor 0x8000 -bor 0x4000   # MOUSEEVENTF_MOVE | ABSOLUTE | VIRTUALDESK
    $down=if($Button -eq 'Left'){0x0002}else{0x0008}
    $up=if($Button -eq 'Left'){0x0004}else{0x0010}
    Send-ClientInputBatch $Hwnd @([ClientInputNative]::Mouse($nx,$ny,$absolute),[ClientInputNative]::Mouse($nx,$ny,($absolute -bor $down)),[ClientInputNative]::Mouse($nx,$ny,($absolute -bor $up))) $entry
    if($Double){
        Start-Sleep -Milliseconds 60
        Send-ClientInputBatch $Hwnd @([ClientInputNative]::Mouse($nx,$ny,($absolute -bor $down)),[ClientInputNative]::Mouse($nx,$ny,($absolute -bor $up))) (Copy-ClientLogEntry $entry "$action-second")
    }
    [pscustomobject]$entry
}

$script:ClientKeyNames=@{
    'ENTER'=0x0D;'RETURN'=0x0D;'ESC'=0x1B;'ESCAPE'=0x1B;'TAB'=0x09;'SPACE'=0x20;'BACKSPACE'=0x08;'BACK'=0x08
    'LEFT'=0x25;'UP'=0x26;'RIGHT'=0x27;'DOWN'=0x28;'HOME'=0x24;'END'=0x23;'PAGEUP'=0x21;'PAGEDOWN'=0x22;'INSERT'=0x2D;'DELETE'=0x2E;'DEL'=0x2E
}
$script:ClientExtendedKeys=@(0x21,0x22,0x23,0x24,0x25,0x26,0x27,0x28,0x2D,0x2E)
$script:ClientModifierKeys=@{'CTRL'=0x11;'CONTROL'=0x11;'ALT'=0x12;'SHIFT'=0x10}
function ConvertTo-ClientVirtualKey {
    param([Parameter(Mandatory)][string]$Name)
    $u=$Name.Trim().ToUpperInvariant()
    if($script:ClientKeyNames.ContainsKey($u)){return [uint16]$script:ClientKeyNames[$u]}
    if($u -match '^F([1-9]|1[0-9]|2[0-4])$'){return [uint16](0x6F+[int]$Matches[1])}
    if($u -match '^[A-Z0-9]$'){return [uint16][int][char]$u}
    throw "Unknown key name '$Name'"
}
function New-ClientKeyInput {
    # Virtual-key code + scan code (MapVirtualKey MAPVK_VK_TO_VSC); KEYEVENTF_EXTENDEDKEY for the navigation block.
    param([uint16]$Vk,[switch]$Up)
    $scan=[uint16][ClientInputNative]::MapVirtualKey($Vk,0)
    $flags=0
    if($script:ClientExtendedKeys -contains [int]$Vk){$flags=$flags -bor 0x0001}
    if($Up){$flags=$flags -bor 0x0002}
    [ClientInputNative]::Key($Vk,$scan,[uint32]$flags)
}
function Send-ClientKey {
    # '<key>' or '<Mod>+...+<key>': modifiers Ctrl/Alt/Shift; keys F1-F24, A-Z, 0-9, Enter, Esc, Tab, Space, Backspace,
    # arrows, Home/End, PageUp/PageDown, Insert/Delete. Modifiers go down first and are always released in finally.
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$Keys)
    $parts=@($Keys -split '\+' | ForEach-Object {$_.Trim()} | Where-Object {$_})
    if($parts.Count -lt 1){throw "Empty key specification"}
    $mods=@()
    for($i=0;$i -lt $parts.Count-1;$i++){
        $mu=$parts[$i].ToUpperInvariant()
        if(-not $script:ClientModifierKeys.ContainsKey($mu)){throw "Unknown modifier '$($parts[$i])' in '$Keys'"}
        $mods+=[uint16]$script:ClientModifierKeys[$mu]
    }
    $vk=ConvertTo-ClientVirtualKey $parts[-1]
    $fgCheck=Assert-ClientForeground $Hwnd
    $pressed=[Collections.Generic.List[uint16]]::new()
    $base=[ordered]@{Action='';Keys=$Keys;VirtualKey=('0x{0:X2}' -f $vk);Modifiers=@($mods | ForEach-Object {'0x{0:X2}' -f $_});ForegroundAcquired=$fgCheck.Acquired}
    $releaseErrors=[Collections.Generic.List[string]]::new()
    try {
        foreach($m in $mods){
            Send-ClientInputBatch $Hwnd @(New-ClientKeyInput $m) (Copy-ClientLogEntry $base ('key-down 0x{0:X2}' -f $m))
            $pressed.Add($m)
        }
        Send-ClientInputBatch $Hwnd @((New-ClientKeyInput $vk),(New-ClientKeyInput $vk -Up)) (Copy-ClientLogEntry $base ('key-press 0x{0:X2}' -f $vk))
    } finally {
        # Release every modifier this call pressed (reverse order). The release still goes through the foreground
        # guard; a refused release is logged in the JSONL and reported, never sent to another program.
        for($i=$pressed.Count-1;$i -ge 0;$i--){
            $m=$pressed[$i]
            try {Send-ClientInputBatch $Hwnd @(New-ClientKeyInput $m -Up) (Copy-ClientLogEntry $base ('key-up 0x{0:X2}' -f $m))} catch {$releaseErrors.Add($_.Exception.Message)}
        }
    }
    if($releaseErrors.Count -gt 0){throw "Modifier release refused: $($releaseErrors -join ' | ')"}
    $base.Action='key';[pscustomobject]$base
}
function Send-ClientText {
    # -Mode Unicode (default): KEYEVENTF_UNICODE down/up per UTF-16 unit (as Send-UiaText), target = game window.
    # -Mode VirtualKey: [A-Za-z0-9 ] as virtual keys (Shift for capitals). The native login input boxes consume every
    #   keydown and type from the key itself (engine/source/NativeUi/LoginPanel.cs:546-579, Program.cs:122), so the
    #   VK_PACKET keydown of the Unicode mode is likely consumed there without a character (inferred, not observed).
    # The foreground PID is checked before every character (inside SendGuarded). Existing text is not cleared.
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][AllowEmptyString()][string]$Text,[ValidateSet('Unicode','VirtualKey')][string]$Mode='Unicode')
    if($Mode -eq 'VirtualKey' -and $Text -notmatch '^[A-Za-z0-9 ]*$'){throw "VirtualKey mode only types [A-Za-z0-9 ]"}
    $fgCheck=Assert-ClientForeground $Hwnd
    $index=0
    foreach($c in $Text.ToCharArray()){
        $entry=[ordered]@{Action="text-$($Mode.ToLowerInvariant())";Index=$index;CodeUnit=('U+{0:X4}' -f [int]$c);ForegroundAcquired=$fgCheck.Acquired}
        if($Mode -eq 'Unicode'){
            Send-ClientInputBatch $Hwnd @([ClientInputNative]::Key(0,[uint16]$c,0x0004),[ClientInputNative]::Key(0,[uint16]$c,0x0006)) $entry
        } else {
            $vk=[uint16][int][char]([string]$c).ToUpperInvariant();$shift=[char]::IsUpper($c)
            $inputs=@()
            if($shift){$inputs+=New-ClientKeyInput 0x10}
            $inputs+=New-ClientKeyInput $vk
            $inputs+=New-ClientKeyInput $vk -Up
            if($shift){$inputs+=New-ClientKeyInput 0x10 -Up}
            $entry['VirtualKey']=('0x{0:X2}' -f $vk);$entry['Shift']=$shift
            Send-ClientInputBatch $Hwnd $inputs $entry
        }
        $index++
    }
}

function Get-GdiClientBitmap {
    # Internal: hashtable {Bitmap (24bpp, caller disposes), Method, Stats, PrintWindowError, BitBltError, Geometry, Foreground}.
    # 1) PrintWindow(PW_RENDERFULLCONTENT=2) of the whole window into a GetWindowRect-sized bitmap (no foreground needed,
    #    works when covered; same call as Save-UiaGdiScreenshot via UiaGdiNative.PrintWindowLE), cropped to the client
    #    rectangle (ClientToScreen origin - window origin).
    # 2) Only if it returns false or the client crop is uniformly black/white: Assert-ClientForeground (#1148
    #    Set-UiaForeground) and BitBlt the same rectangle from the screen DC.
    param([Parameter(Mandatory)][IntPtr]$Hwnd)
    if([UiaGdiNative]::IsIconic($Hwnd)){throw 'Window is minimized; restore deliberately before capture'}
    $g=Get-ClientWindowGeometry $Hwnd
    if($g.ClientWidth -le 0 -or $g.ClientHeight -le 0){throw "Empty client area $($g.ClientWidth)x$($g.ClientHeight)"}
    $printError=$null;$bltError=$null;$foreground=$null;$result=$null
    $wr=$g.WindowRect;$ww=$wr[2]-$wr[0];$wh=$wr[3]-$wr[1]
    $ox=$g.ClientScreenLeft-$wr[0];$oy=$g.ClientScreenTop-$wr[1]
    if($ww -le 0 -or $wh -le 0 -or $ox -lt 0 -or $oy -lt 0 -or ($ox+$g.ClientWidth) -gt $ww -or ($oy+$g.ClientHeight) -gt $wh){
        $printError="Window rect $($wr -join ',') does not contain the client rect at ($ox,$oy) $($g.ClientWidth)x$($g.ClientHeight)"
    } else {
        $full=[Drawing.Bitmap]::new($ww,$wh,[Drawing.Imaging.PixelFormat]::Format24bppRgb)
        try {
            $graphics=[Drawing.Graphics]::FromImage($full);[int]$pe=0;$ok=$false
            try {
                $hdc=$graphics.GetHdc()
                try {$ok=[UiaGdiNative]::PrintWindowLE($Hwnd,$hdc,2,[ref]$pe)} finally {$graphics.ReleaseHdc($hdc)}
            } finally {$graphics.Dispose()}
            if(-not $ok){$printError="PrintWindow(PW_RENDERFULLCONTENT) returned false; LastError=$pe"}
            else {
                $crop=$full.Clone([Drawing.Rectangle]::new($ox,$oy,$g.ClientWidth,$g.ClientHeight),[Drawing.Imaging.PixelFormat]::Format24bppRgb)
                $s=[ClientInputNative]::Stats($crop)
                if($s[2] -eq 0 -or $s[3] -eq 0){
                    $crop.Dispose()
                    $printError="PrintWindow client image is uniformly black or white (Mean=$($s[1]), NonBlackRatio=$($s[2]), NonWhiteRatio=$($s[3]))"
                } else {$result=@{Bitmap=$crop;Method='PrintWindow';Stats=$s}}
            }
        } catch {if($null -eq $printError){$printError=$_.Exception.ToString()}} finally {$full.Dispose()}
    }
    if($null -eq $result){
        $bmp=$null
        try {
            $foreground=Assert-ClientForeground $Hwnd
            $g=Get-ClientWindowGeometry $Hwnd
            $bmp=[Drawing.Bitmap]::new($g.ClientWidth,$g.ClientHeight,[Drawing.Imaging.PixelFormat]::Format24bppRgb)
            $graphics=[Drawing.Graphics]::FromImage($bmp);[int]$be=0;$ok=$false
            try {
                $hdc=$graphics.GetHdc()
                try {$ok=[ClientInputNative]::BitBltScreenLE($hdc,$g.ClientScreenLeft,$g.ClientScreenTop,$g.ClientWidth,$g.ClientHeight,[ref]$be)} finally {$graphics.ReleaseHdc($hdc)}
            } finally {$graphics.Dispose()}
            if(-not $ok){throw "BitBlt(screen DC, SRCCOPY|CAPTUREBLT) returned false; LastError=$be"}
            $s=[ClientInputNative]::Stats($bmp)
            if($s[2] -eq 0 -or $s[3] -eq 0){$bltError="BitBlt client image is uniformly black or white (Mean=$($s[1]), NonBlackRatio=$($s[2]), NonWhiteRatio=$($s[3]))"}
            $result=@{Bitmap=$bmp;Method='BitBlt';Stats=$s};$bmp=$null
        } catch {
            $bltError=$_.Exception.ToString()
            if($null -eq $foreground){$foreground=Get-UiaLastForegroundDiagnostic}
        } finally {if($null -ne $bmp){$bmp.Dispose()}}
    }
    if($null -eq $result){throw "Client capture failed on both paths. PrintWindow: $printError | BitBlt: $bltError"}
    $result.PrintWindowError=$printError;$result.BitBltError=$bltError;$result.Geometry=$g;$result.Foreground=$foreground
    $result
}
function Save-GdiClientBitmap {
    # Internal: save a Get-GdiClientBitmap result as PNG and return the summary object.
    param([Parameter(Mandatory)]$Capture,[Parameter(Mandatory)][string]$Path)
    $full=[IO.Path]::GetFullPath($Path)
    $Capture.Bitmap.Save($full,[Drawing.Imaging.ImageFormat]::Png)
    $s=$Capture.Stats;$g=$Capture.Geometry
    [pscustomobject][ordered]@{
        Path=$full;Method=$Capture.Method;PrintWindowError=$Capture.PrintWindowError;BitBltError=$Capture.BitBltError
        Width=$Capture.Bitmap.Width;Height=$Capture.Bitmap.Height;ClientWidth=$g.ClientWidth;ClientHeight=$g.ClientHeight
        SizeMatchesClient=($Capture.Bitmap.Width -eq $g.ClientWidth -and $Capture.Bitmap.Height -eq $g.ClientHeight)
        Dpi=$g.Dpi;Scale=$g.Scale;RatioNote=$g.RatioNote;ClientScreenLeft=$g.ClientScreenLeft;ClientScreenTop=$g.ClientScreenTop
        Pixels=[long]$s[0];Mean=$s[1];NonBlackRatio=$s[2];NonWhiteRatio=$s[3];Nonblank=($s[2] -gt 0 -and $s[3] -gt 0)
        Sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $full).Hash;Foreground=$Capture.Foreground;Hwnd=$g.Hwnd;ProcessId=$g.ProcessId
    }
}
function Get-GdiClientCapture {
    # Client-area GDI screenshot of a raw HWND (no UIA tree, no WGC). Method order: see Get-GdiClientBitmap.
    # Returns size, DPI, mean intensity, non-black / non-white ratios, SHA-256, Method and both paths' error texts.
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$Path)
    $c=Get-GdiClientBitmap $Hwnd
    try {Save-GdiClientBitmap $c $Path} finally {$c.Bitmap.Dispose()}
}
