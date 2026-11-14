param([Parameter(Mandatory)][string]$EvidenceDirectory,[switch]$SkipEvidenceKey)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$ev=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $ev,(Join-Path $ev 'tmp') | Out-Null
$env:TEMP=Join-Path $ev 'tmp';$env:TMP=$env:TEMP
. "$PSScriptRoot/UiaGdi.ps1"
$result=[ordered]@{StartedUtc=[DateTime]::UtcNow.ToString('o');ProcessId=$null;Closed=$false;ForcedClose=$false;Checks=@();Errors=@()}
$owned=$null;$window=$null;$dialog=$null
function Save-Check($name,$body){
    try {$detail=& $body;$result.Checks+= [pscustomobject]@{Name=$name;Passed=$true;Detail=$detail}}
    catch {$result.Checks+= [pscustomobject]@{Name=$name;Passed=$false;Error=$_.Exception.ToString()}}
}
try {
    # Visible Notepad is the only application this test starts. Never open or save a document.
    $owned=Start-Process "$env:WINDIR/System32/notepad.exe" -PassThru
    $result.ProcessId=$owned.Id;$result.StartTime=$owned.StartTime.ToString('o')
    $null=$owned.WaitForInputIdle(5000)
    $window=Get-UiaWindow -ProcessId $owned.Id
    $result.Window=Get-UiaWindowInfo $window
    if(-not $SkipEvidenceKey){ Save-Check 'G04-evidence-key' {
        $log=Join-Path $ev 'evidence-key.jsonl'
        if(Test-Path -LiteralPath $log){throw 'Use a new evidence directory; do not mix input runs'}
        Set-ClientInputLog $log
        $sent=Send-UiaEvidenceKey $window
        $rows=@(Get-Content -Encoding UTF8 $log | ForEach-Object {$_ | ConvertFrom-Json})
        $expected='key-down 0x11|key-down 0x10|key-press 0x7B|key-up 0x10|key-up 0x11'
        if(($rows.Action -join '|') -cne $expected){throw 'Unexpected keyboard sequence'}
        foreach($row in $rows){
            if($row.ProcessId -ne $owned.Id -or $row.ForegroundProcessId -ne $owned.Id -or $row.SendInputReturned -ne $row.InputCount){throw 'SendInput/PID guard mismatch'}
        }
        if($sent.Kind -ne [DateTimeKind]::Utc){throw 'Evidence key did not return UTC'}
        $capture=Save-UiaGdiScreenshot $window "$ev/evidence-key.png"
        [pscustomobject]@{SentUtc=$sent.ToString('o');Sequence=$rows;Screenshot=$capture;NewEvidence=@(Get-UiaNewEvidence $ev $sent).Count}
    }
    }
    Set-ClientInputLog "$ev/dialog-input.jsonl"
    $null=Send-ClientKey ([IntPtr]$window.Current.NativeWindowHandle) 'Ctrl+Shift+S'
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 100
        $fg=Get-UiaForegroundInfo
        if($fg.ProcessId -eq $owned.Id -and $fg.Handle -ne $window.Current.NativeWindowHandle){break}
    } while([DateTime]::UtcNow -lt $deadline)
    if($fg.ProcessId -ne $owned.Id -or $fg.Handle -eq $window.Current.NativeWindowHandle){throw 'Owned Save As dialog did not appear'}
    # A new HWND can be visible before the shell dialog finishes publishing its UIA tree.
    Start-Sleep -Milliseconds 500
    $observed=[Windows.Automation.AutomationElement]::FromHandle([IntPtr]$fg.Handle)
    $title=$observed.Current.Name
    $dialog=Get-UiaWindow -ProcessId $owned.Id -Title $title -IncludeOwned
    Save-Check 'P01-owned-window' {
        $info=Get-UiaWindowInfo $dialog
        if($info.Handle -ne $fg.Handle -or $info.ProcessId -ne $owned.Id){throw 'Owned dialog identity mismatch'}
        Get-UiaTree $dialog | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 "$ev/dialog-tree.json"
        $capture=Save-UiaGdiScreenshot $dialog "$ev/dialog.png"
        $topLevelError=$null
        try {$null=Get-UiaWindow -ProcessId $owned.Id -Title $title} catch {$topLevelError=$_.Exception.ToString()}
        [pscustomobject]@{Window=$info;DesktopOnlyError=$topLevelError;Screenshot=$capture}
    }
    Save-Check 'P01-closing-tree' {
        $null=[UiaGdiNative]::PostMessage([IntPtr]$dialog.Current.NativeWindowHandle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)
        $closing=@(Get-UiaTree $dialog)
        $closing | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 "$ev/closing-tree.json"
        Start-Sleep -Milliseconds 200
        $stale=@(Get-UiaTree $dialog)
        $stale | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 "$ev/stale-tree.json"
        $errors=@($stale | Where-Object {$_.PSObject.Properties['Error']})
        if($errors.Count -eq 0){throw 'Closed dialog did not expose a stale element error; race outcome inconclusive'}
        $live=@(Get-UiaTree $window)
        $live | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 "$ev/after-cancel-tree.json"
        $capture=Save-UiaGdiScreenshot $window "$ev/after-cancel.png"
        [pscustomobject]@{ClosingRows=$closing.Count;StaleErrors=$errors;LiveRows=$live.Count;Screenshot=$capture}
    }
} catch {$result.Errors+=$_.Exception.ToString()}
finally {
    if($null -ne $owned){
        try {
            if(-not $owned.HasExited){
                # Cancel any remaining modal in this PID; never save or send input to another PID.
                $fg=Get-UiaForegroundInfo
                if($fg.ProcessId -eq $owned.Id -and $null -ne $window -and $fg.Handle -ne $window.Current.NativeWindowHandle){
                    $null=Send-ClientKey ([IntPtr]$fg.Handle) 'Escape'
                    Start-Sleep -Milliseconds 200
                }
                $null=$owned.CloseMainWindow()
            }
            $result.Closed=$owned.WaitForExit(3000)
            if(-not $result.Closed){throw 'Owned blank Notepad failed to close'}
        } catch {$result.Errors+=$_.Exception.ToString()}
        finally {
            if(-not $owned.HasExited){
                $current=Get-Process -Id $owned.Id
                if($current.ProcessName -eq 'notepad' -and $current.StartTime.ToString('o') -eq $result.StartTime){
                    $current.Kill();$result.Closed=$current.WaitForExit(3000);$result.ForcedClose=$true
                }
            }
        }
    }
    $result.CompletedUtc=[DateTime]::UtcNow.ToString('o')
    $expectedChecks=if($SkipEvidenceKey){2}else{3}
    $result.ExitCode=if($result.Errors.Count -eq 0 -and @($result.Checks | Where-Object {-not $_.Passed}).Count -eq 0 -and $result.Checks.Count -eq $expectedChecks -and $result.Closed -and -not $result.ForcedClose){0}else{1}
    $result | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 "$ev/helpers-result.json"
}
$result | ConvertTo-Json -Depth 12
exit $result.ExitCode
