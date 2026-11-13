# Native UI client smoke up to the login screen (#1145 N01-N04 tool, written in #1154). Windows PowerShell 5.1:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-NativeClient.ps1 -EvidenceDirectory scratch/<task>/native
# Starts engine\<launcher>.cmd --evidence <dir> (no diagnostic / auto-login arguments), waits for the native UI,
# clicks the account box and types 'abc' (no password, no login, no server), sends Ctrl+P, closes with Alt+F4.
# Only processes this script started (PID + start time) are ever terminated. Exit 0 = every check passed.
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$ClientCmd,
    # ID_Input centre in 800x600 logical coordinates; rectangle source: README "Account box" (kor25 LoginUI.xml).
    [double[]]$AccountPoint=@(375,374),
    [int[]]$AccountRect=@(320,364,110,20),
    [ValidateSet('VirtualKey','Unicode')][string]$TextMode='VirtualKey',
    [int]$ActiveTimeoutSeconds=60,
    [switch]$Fill
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$ev=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $ev,(Join-Path $ev 'tmp') | Out-Null
$env:TEMP=Join-Path $ev 'tmp';$env:TMP=$env:TEMP
. "$PSScriptRoot/UiaGdi.ps1"
Set-ClientInputLog (Join-Path $ev 'client-input.jsonl')

$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $ClientCmd){
    # engine\<U+555F U+52D5 U+65B0 U+7248 U+5BA2 U+6236 U+7AEF>.cmd, spelled with code points so this file stays ASCII.
    $name=-join ([char[]](0x555F,0x52D5,0x65B0,0x7248,0x5BA2,0x6236,0x7AEF))
    $ClientCmd=Join-Path $repo "engine\$name.cmd"
}
$ClientCmd=[IO.Path]::GetFullPath($ClientCmd)
$failures=[Collections.Generic.List[string]]::new()
$owned=[ordered]@{}   # pid -> @{Name; StartTime(o)}
$client=$null;$hwnd=[IntPtr]::Zero
$result=[ordered]@{
    StartedAt=[DateTimeOffset]::Now.ToString('o');EvidenceDirectory=$ev;ClientCmd=$ClientCmd
    Arguments=@('--evidence',$ev);AccountPoint=$AccountPoint;AccountRect=$AccountRect;TextMode=$TextMode
    ProcessDpiAwarenessContextSet=$script:ProcessDpiSet;ProcessDpiAwarenessContextError=$script:ProcessDpiError
    N01=[ordered]@{Passed=$false};N02=[ordered]@{Passed=$false};N03=[ordered]@{Passed=$false};N04=[ordered]@{Passed=$false}
    ExitCode=1
}

function Update-OwnedProcesses {
    # Adds descendants of already-owned PIDs created after our start (guards against PID reuse).
    param([datetime]$Since)
    $all=@(Get-CimInstance Win32_Process -Property ProcessId,ParentProcessId,Name,CreationDate)
    $changed=$true
    while($changed){
        $changed=$false
        foreach($p in $all){
            $key=[string]$p.ProcessId
            if($owned.Contains($key)){continue}
            if(-not $owned.Contains([string]$p.ParentProcessId)){continue}
            if($null -eq $p.CreationDate -or $p.CreationDate -lt $Since){continue}
            $owned[$key]=[ordered]@{ProcessId=[int]$p.ProcessId;ParentProcessId=[int]$p.ParentProcessId;Name=$p.Name;StartTime=$p.CreationDate.ToString('o');StartTimeValue=$p.CreationDate}
            $changed=$true
        }
    }
}
function Test-OwnedIdentity {
    param([int]$ProcessId)
    $info=$owned[[string]$ProcessId]
    try {$p=Get-Process -Id $ProcessId -ErrorAction Stop} catch {return $null}
    # CIM CreationDate and Process.StartTime differ in precision; same process = within 1 s.
    if([Math]::Abs(($p.StartTime-$info.StartTimeValue).TotalSeconds) -ge 1){return $null}
    return $p
}
function Get-NativeUiFailureLine {
    $log=Join-Path $ev 'native-ui.log'
    if(-not (Test-Path -LiteralPath $log)){return $null}
    $hit=@(Get-Content -LiteralPath $log -Encoding UTF8 | Where-Object {$_ -like '*native-ui fallback*'})
    if($hit.Count -gt 0){return $hit[0]}
    return $null
}

$verifying=$null;$active=$null
try {
    if(-not (Test-Path -LiteralPath $ClientCmd)){throw "Client launcher not found: $ClientCmd"}
    foreach($stale in 'window-events.json','native-ui.log'){
        if(Test-Path -LiteralPath (Join-Path $ev $stale)){throw "Evidence directory already has $stale; use a new directory so results are not mixed"}
    }
    $result.ForegroundBefore=Get-UiaForegroundInfo
    $captureDirs=@([IO.Path]::GetFullPath((Join-Path $repo 'engine\runtime\Capture')))

    # --- Start (normal entry + --evidence only) ---
    $since=(Get-Date).AddSeconds(-2)
    $starter=Start-Process -FilePath $ClientCmd -ArgumentList @('--evidence',"`"$ev`"") -WorkingDirectory (Split-Path -Parent $ClientCmd) -PassThru
    $owned[[string]$starter.Id]=[ordered]@{ProcessId=$starter.Id;ParentProcessId=0;Name=$starter.ProcessName;StartTime=$starter.StartTime.ToString('o');StartTimeValue=$starter.StartTime}
    $result.StarterProcessId=$starter.Id

    # --- Find the client's main window by PID ---
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while([DateTime]::UtcNow -lt $deadline -and $hwnd -eq [IntPtr]::Zero){
        Update-OwnedProcesses $since
        foreach($k in @($owned.Keys)){
            $o=$owned[$k]
            if($o.Name -ne 'P11.ClientEngine.exe'){continue}
            $p=Test-OwnedIdentity $o.ProcessId
            if($null -eq $p){continue}
            $p.Refresh()
            if($p.MainWindowHandle -ne [IntPtr]::Zero -and [ClientInputNative]::IsWindowVisible($p.MainWindowHandle)){$client=$p;$hwnd=$p.MainWindowHandle}
        }
        if($hwnd -eq [IntPtr]::Zero){Start-Sleep -Milliseconds 250}
    }
    if($hwnd -eq [IntPtr]::Zero){throw "No visible P11.ClientEngine.exe main window among owned processes within 30 s"}
    $result.ClientProcessId=$client.Id;$result.ClientStartTime=$client.StartTime.ToString('o');$result.ClientPath=$client.Path
    $result.Hwnd=$hwnd.ToInt64();$result.WindowTitle=$client.MainWindowTitle
    # <stage>/runtime/Capture with stage = exe dir/../.. (MenuButtonsPanel.CaptureDirectory, WebPanel.cs:15 stageRoot).
    $stageCapture=[IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $client.Path) '..\..\runtime\Capture'))
    if($captureDirs -notcontains $stageCapture){$captureDirs+=$stageCapture}
    $result.CaptureDirectories=$captureDirs

    # --- N01: poll every 0.5 s until the native UI is up (max $ActiveTimeoutSeconds) ---
    # window-events.json is written only in FormClosed (engine/source/Program.cs:117) and native-ui.log has no
    # "active" line (UiClientBridge.cs:46 only adds an in-memory event; :51 logs failures). So "active" is detected
    # visually here: the first frame whose pixels differ from the first captured frame by more than 2 %.
    # It is confirmed after closing from window-events.json (verifying -> active order).
    $poll=[Diagnostics.Stopwatch]::StartNew();$frames=0;$lastCaptureError=$null;$polls=[Collections.Generic.List[object]]::new()
    $first=$null;$firstAt=$null
    try {
        while($poll.Elapsed.TotalSeconds -lt $ActiveTimeoutSeconds){
            $tick=$poll.ElapsedMilliseconds
            if($client.HasExited){throw "Client exited during startup (exit code $($client.ExitCode))"}
            $fail=Get-NativeUiFailureLine
            if($fail){throw "native-ui.log reports a start failure: $fail"}
            $cap=$null
            try {$cap=Get-GdiClientBitmap $hwnd} catch {$lastCaptureError=$_.Exception.Message}
            if($null -ne $cap){
                $frames++
                if($null -eq $first){
                    $first=$cap;$firstAt=[DateTimeOffset]::Now
                    $verifying=Save-GdiClientBitmap $cap (Join-Path $ev 'n01-verifying.png')
                    $polls.Add([ordered]@{Ms=$tick;Frame=$frames;Method=$cap.Method;Baseline=$true})
                } else {
                    $ratio=1.0
                    if($cap.Bitmap.Width -eq $first.Bitmap.Width -and $cap.Bitmap.Height -eq $first.Bitmap.Height){
                        $d=[ClientInputNative]::Diff($first.Bitmap,$cap.Bitmap,48,0,0,0,0)
                        $ratio=$d[0]/[double]($cap.Bitmap.Width*$cap.Bitmap.Height)
                    }
                    $polls.Add([ordered]@{Ms=$tick;Frame=$frames;Method=$cap.Method;ChangedRatio=$ratio})
                    if($ratio -gt 0.02){
                        $activeAt=[DateTimeOffset]::Now
                        $active=Save-GdiClientBitmap $cap (Join-Path $ev 'n01-active.png')
                        $result.N01.VerifyingToActiveMs=[int]($activeAt-$firstAt).TotalMilliseconds
                        $result.N01.ActiveChangedRatio=$ratio
                        $cap.Bitmap.Dispose()
                        break
                    }
                    $cap.Bitmap.Dispose()
                }
            }
            $wait=500-($poll.ElapsedMilliseconds-$tick)
            if($wait -gt 0){Start-Sleep -Milliseconds $wait}
        }
    } finally {if($null -ne $first){$first.Bitmap.Dispose()}}
    $result.N01.Frames=$frames;$result.N01.Polls=$polls;$result.N01.LastCaptureError=$lastCaptureError
    $result.N01.Verifying=$verifying;$result.N01.Active=$active
    if($null -eq $verifying){throw "N01: no client-area capture succeeded in $ActiveTimeoutSeconds s; last error: $lastCaptureError"}
    if($null -eq $active){throw "N01: no visual change from the first frame within $ActiveTimeoutSeconds s (native UI active not observed)"}

    # --- N02: click the account box, type 'abc' (VirtualKey by default), compare before/after inside the box ---
    Start-Sleep -Milliseconds 1000
    $before=Get-GdiClientBitmap $hwnd
    try {
        $result.N02.Before=Save-GdiClientBitmap $before (Join-Path $ev 'n02-before.png')
        $clickError=$null;$textError=$null
        try {$result.N02.Click=Send-ClientClick $hwnd $AccountPoint[0] $AccountPoint[1] -Fill:$Fill} catch {$clickError=$_.Exception.Message}
        $result.N02.ClickSent=($null -eq $clickError);$result.N02.ClickError=$clickError
        if($null -eq $clickError){
            Start-Sleep -Milliseconds 150
            try {Send-ClientText $hwnd 'abc' -Mode $TextMode} catch {$textError=$_.Exception.Message}
        } else {$textError='skipped: click was not sent'}
        $result.N02.TextSent=($null -eq $textError);$result.N02.TextError=$textError
        Start-Sleep -Milliseconds 500
        $after=Get-GdiClientBitmap $hwnd
        try {
            $result.N02.After=Save-GdiClientBitmap $after (Join-Path $ev 'n02-after.png')
            $g=Get-ClientWindowGeometry $hwnd -Fill:$Fill
            $r=Convert-ClientLogicalRect $g $AccountRect
            $result.N02.AccountRectClient=$r
            if($before.Bitmap.Width -ne $after.Bitmap.Width -or $before.Bitmap.Height -ne $after.Bitmap.Height){throw 'N02: client size changed between captures'}
            $d=[ClientInputNative]::Diff($before.Bitmap,$after.Bitmap,48,$r.X,$r.Y,$r.Width,$r.Height)
            $result.N02.Diff=[ordered]@{Threshold=48;ChangedTotal=$d[0];ChangedInsideAccountRect=$d[1];AccountRectArea=$d[2];BoundingBox=@($d[3],$d[4],$d[5],$d[6])}
            # Heuristic only: >= 30 changed pixels per scale^2 inside ID_Input (a caret is ~14 px tall x 1 px).
            # The login background animates (Back_Image png every 200 ms, LoginPanel.cs:51), so changes outside the box are expected.
            $need=[int](30*$g.Scale*$g.Scale)
            $result.N02.RequiredInside=$need
            $result.N02.Passed=($result.N02.ClickSent -and $result.N02.TextSent -and $d[1] -ge $need)
            if(-not $result.N02.Passed){$failures.Add("N02: click=$($result.N02.ClickSent) text=$($result.N02.TextSent) changedInside=$($d[1]) (need $need); click error: $clickError; text error: $textError")}
        } finally {$after.Bitmap.Dispose()}
    } finally {$before.Bitmap.Dispose()}

    # --- N03: Ctrl+P -> new file under <stage>/runtime/Capture ---
    # Static expectation: MenuButtonsPanel.Key returns false outside the world (MenuButtonsPanel.cs:276), so on the
    # login screen no file is expected; the check is still run and reported as observed.
    $beforeFiles=@{}
    foreach($dir in $captureDirs){if(Test-Path -LiteralPath $dir){Get-ChildItem -LiteralPath $dir -File | ForEach-Object {$beforeFiles[$_.FullName]=$_.LastWriteTimeUtc}}}
    $sentAt=[DateTime]::UtcNow
    try {$result.N03.Key=Send-ClientKey $hwnd 'Ctrl+P'} catch {$result.N03.KeyError=$_.Exception.Message}
    $newFiles=@();$deadline=[DateTime]::UtcNow.AddSeconds(3)
    while([DateTime]::UtcNow -lt $deadline -and $newFiles.Count -eq 0){
        Start-Sleep -Milliseconds 250
        $newFiles=@(foreach($dir in $captureDirs){if(Test-Path -LiteralPath $dir){Get-ChildItem -LiteralPath $dir -File | Where-Object {-not $beforeFiles.ContainsKey($_.FullName) -or $beforeFiles[$_.FullName] -ne $_.LastWriteTimeUtc}}})
    }
    $result.N03.KeySentUtc=$sentAt.ToString('o')
    $result.N03.NewFiles=@($newFiles | ForEach-Object {[ordered]@{Path=$_.FullName;LastWriteTime=$_.LastWriteTime.ToString('o');Length=$_.Length;Sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash}})
    $result.N03.ExpectedOnLoginScreen='no file: MenuButtonsPanel.Key ignores hotkeys outside the world (MenuButtonsPanel.cs:276)'
    $result.N03.Passed=($newFiles.Count -gt 0)
    if(-not $result.N03.Passed){$failures.Add("N03: no new file under $($captureDirs -join '; ') within 3 s after Ctrl+P; key error: $($result.N03['KeyError'])")}
} catch {
    $failures.Add($_.Exception.ToString())
    $result.ForegroundAtFailure=Get-UiaForegroundInfo
    $result.ForegroundDiagnostic=Get-UiaLastForegroundDiagnostic
} finally {
    # --- N04: Alt+F4, client exits, window-events.json has closed; clean up only our own processes ---
    try {
        if($null -ne $client -and -not $client.HasExited){
            try {$result.N04.Key=Send-ClientKey $hwnd 'Alt+F4'} catch {$result.N04.KeyError=$_.Exception.Message}
            $result.N04.ExitedAfterAltF4=$client.WaitForExit(10000)
        }
    } catch {$failures.Add($_.Exception.ToString())}
    try {Update-OwnedProcesses $since} catch {}
    $result.OwnedProcesses=@($owned.Values)
    $forced=@()
    foreach($o in @($owned.Values)){
        $p=Test-OwnedIdentity $o.ProcessId
        if($null -eq $p -or $p.HasExited){continue}
        # Leftover from this run only (PID + start time verified); never by name.
        try {$p.Kill();$null=$p.WaitForExit(5000);$forced+=[ordered]@{ProcessId=$o.ProcessId;Name=$o.Name;Exited=$p.HasExited}} catch {$failures.Add("Kill $($o.ProcessId) failed: $($_.Exception.Message)")}
    }
    $result.ForcedClose=$forced
    $events=Join-Path $ev 'window-events.json'
    $kinds=@()
    if(Test-Path -LiteralPath $events){
        try {
            # PS 5.1 ConvertFrom-Json emits a JSON array as one object; assign first so @() does not nest it.
            $parsed=Get-Content -LiteralPath $events -Raw -Encoding UTF8 | ConvertFrom-Json
            $list=@($parsed)
            $states=@($list | Where-Object {$_.PSObject.Properties['kind'] -and $_.kind -eq 'native-ui'} | ForEach-Object {$_.state})
            $kinds=@($list | Where-Object {$_.PSObject.Properties['kind']} | ForEach-Object {$_.kind})
            $result.WindowEvents=[ordered]@{NativeUiStates=$states;Closed=($kinds -contains 'closed');Captures=@($list | Where-Object {$_.PSObject.Properties['kind'] -and $_.kind -eq 'native-ui-capture'})}
            $vi=[array]::IndexOf($states,'verifying');$ai=[array]::IndexOf($states,'active')
            $result.N01.WindowEventsVerifyingThenActive=($vi -ge 0 -and $ai -gt $vi)
        } catch {$failures.Add("window-events.json unreadable: $($_.Exception.Message)")}
    } else {$result.WindowEvents=$null;$result.N01.WindowEventsVerifyingThenActive=$false}
    $result.N04.ClientExited=($null -ne $client -and $client.HasExited)
    $result.N04.WindowEventsClosed=($kinds -contains 'closed')
    $result.N04.ForcedClose=($forced.Count -gt 0)
    $result.N04.RemainingOwned=@($owned.Values | Where-Object {$null -ne (Test-OwnedIdentity $_.ProcessId)} | ForEach-Object {$_.ProcessId})
    try {
        $ownedIds=@($owned.Values | ForEach-Object {[int]$_.ProcessId})
        $listen=@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {@(42061,43324) -contains $_.LocalPort})
        $result.N04.ListenersOnTestPorts=@($listen | ForEach-Object {[ordered]@{Port=$_.LocalPort;ProcessId=$_.OwningProcess;Owned=($ownedIds -contains [int]$_.OwningProcess)}})
        $result.N04.OwnedListeners=@($listen | Where-Object {$ownedIds -contains [int]$_.OwningProcess}).Count
    } catch {$result.N04.ListenerCheckError=$_.Exception.Message;$result.N04.OwnedListeners=-1}
    $result.N04.Passed=($result.N04.ClientExited -and $result.N04.WindowEventsClosed -and -not $result.N04.ForcedClose -and $result.N04.RemainingOwned.Count -eq 0 -and $result.N04.OwnedListeners -eq 0)
    if(-not $result.N04.Passed){$failures.Add("N04: exited=$($result.N04.ClientExited) closedEvent=$($result.N04.WindowEventsClosed) forced=$($result.N04.ForcedClose) remaining=$($result.N04.RemainingOwned -join ',') ownedListeners=$($result.N04.OwnedListeners)")}
    $result.N01.Passed=($null -ne $verifying -and $null -ne $active -and $verifying.Nonblank -and $active.Nonblank -and $verifying.SizeMatchesClient -and $active.SizeMatchesClient -and $result.N01.WindowEventsVerifyingThenActive)
    if(-not $result.N01.Passed -and $null -ne $active){$failures.Add("N01: nonblank/size/window-events check failed (verifying->active in window-events.json: $($result.N01.WindowEventsVerifyingThenActive))")}
    $result.CompletedAt=[DateTimeOffset]::Now.ToString('o')
    $result.Errors=@($failures.ToArray())
    if($failures.Count -eq 0 -and $result.N01.Passed -and $result.N02.Passed -and $result.N03.Passed -and $result.N04.Passed){$result.ExitCode=0}
    $result | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 (Join-Path $ev 'native-result.json')
}
$result | ConvertTo-Json -Depth 8
exit $result.ExitCode
