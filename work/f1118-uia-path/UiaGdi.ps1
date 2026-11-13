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
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
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
function Set-UiaValue { param($Element,[string]$Value) ($Element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).SetValue($Value) }
function Get-UiaValue { param($Element) ($Element.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)).Current.Value }
function Get-UiaWindowInfo {
    param($Window)
    $h=[IntPtr]$Window.Current.NativeWindowHandle
    [pscustomobject]@{Title=$Window.Current.Name;ProcessId=$Window.Current.ProcessId;Handle=$h.ToInt64();Rectangle=$Window.Current.BoundingRectangle.ToString();Minimized=[UiaGdiNative]::IsIconic($h);Dpi=[UiaGdiNative]::GetDpiForWindow($h);ProcessDpiSet=$script:ProcessDpiSet;ProcessDpiError=$script:ProcessDpiError}
}
function Set-UiaForeground {
    param($Window)
    $h=[IntPtr]$Window.Current.NativeWindowHandle
    if([UiaGdiNative]::IsIconic($h)){throw 'Window is minimized; restore deliberately before capture'}
    $null=[UiaGdiNative]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 250
    if([UiaGdiNative]::GetForegroundWindow() -ne $h){throw 'Foreground window does not match target'}
}
function Send-UiaF12 {
    param($Window)
    Set-UiaForeground $Window
    [UiaGdiNative]::Key(0x7B)
}
function Save-UiaGdiScreenshot {
    param([Parameter(Mandatory)]$Window,[Parameter(Mandatory)][string]$Path)
    Set-UiaForeground $Window
    $r=$Window.Current.BoundingRectangle
    $bounds=[Windows.Forms.SystemInformation]::VirtualScreen
    $left=[Math]::Max([int][Math]::Floor($r.Left),$bounds.Left)
    $top=[Math]::Max([int][Math]::Floor($r.Top),$bounds.Top)
    $width=[Math]::Min([int][Math]::Ceiling($r.Right),$bounds.Right)-$left
    $height=[Math]::Min([int][Math]::Ceiling($r.Bottom),$bounds.Bottom)-$top
    if($width -le 0 -or $height -le 0){throw 'Window rectangle is outside the desktop'}
    $bitmap=[Drawing.Bitmap]::new($width,$height)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($left,$top,0,0,$bitmap.Size,[Drawing.CopyPixelOperation]::SourceCopy)
        $bitmap.Save([IO.Path]::GetFullPath($Path),[Drawing.Imaging.ImageFormat]::Png)
        [double]$sum=0;[long]$nonzero=0;[long]$nonwhite=0;[long]$count=0
        for($y=0;$y -lt $height;$y+=4){for($x=0;$x -lt $width;$x+=4){
            $p=$bitmap.GetPixel($x,$y);$v=[int]$p.R+[int]$p.G+[int]$p.B
            $sum+=$v/3.0;$count++;if($v -gt 0){$nonzero++};if($v -lt 765){$nonwhite++}
        }}
        [pscustomobject]@{Path=[IO.Path]::GetFullPath($Path);Width=$width;Height=$height;SampleStride=4;Samples=$count;Mean=$sum/$count;NonzeroRatio=$nonzero/$count;NonwhiteRatio=$nonwhite/$count;Nonblank=($nonzero -gt 0 -and $nonwhite -gt 0);Sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash;Window=(Get-UiaWindowInfo $Window)}
    } finally {$graphics.Dispose();$bitmap.Dispose()}
}
