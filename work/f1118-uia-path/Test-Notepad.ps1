param([Parameter(Mandatory)][string]$EvidenceDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$ev=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $ev,(Join-Path $ev 'tmp') | Out-Null
$env:TEMP=Join-Path $ev 'tmp';$env:TMP=$env:TEMP
. "$PSScriptRoot/UiaGdi.ps1"
$owned=$null;$window=$null;$document=$null
$failures=[Collections.Generic.List[string]]::new()
$result=[ordered]@{
    StartedAt=[DateTimeOffset]::Now.ToString('o')
    ExpectedText='uia-1138 測試輸入 OK'
    Readback=$null
    TextPassed=$false
    Closed=$false
    DontSaveInvoked=$false
    ForcedClose=$false
    ExitCode=1
}
try {
    # This task explicitly needs a visible Notepad window for input and screenshot verification.
    $owned=Start-Process "$env:WINDIR/System32/notepad.exe" -PassThru
    $result.ProcessId=$owned.Id
    $result.StartTime=$owned.StartTime.ToString('o')
    $null=$owned.WaitForInputIdle(5000)
    $window=Get-UiaWindow -ProcessId $owned.Id
    $result.Window=Get-UiaWindowInfo $window
    Get-UiaTree $window | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 "$ev/notepad-tree.json"
    # Windows 10 Notepad's editor is Document/15; the five Edit nodes are status panes.
    $document=Find-UiaControl $window -AutomationId '15' -Type Document
    $result.EditorDescription=Get-UiaPatternDescription $document
    $result.EditorPatterns=@($document.GetSupportedPatterns() | ForEach-Object {$_.ProgrammaticName})
    Set-UiaForeground $window
    Send-UiaText $document $result.ExpectedText
    $result.ForegroundAfterInput=Get-UiaForegroundInfo
    # Allow queued input to be processed; never retry input or switch methods on mismatch.
    Start-Sleep -Milliseconds 300
    $result.Readback=Get-UiaText $document
    $result.ReadPattern=if($result.EditorPatterns -contains 'ValuePatternIdentifiers.Pattern'){'ValuePattern'}else{'TextPattern'}
    $result.NormalizedExpected=$result.ExpectedText.Replace("`r`n","`n").Replace("`r","`n")
    $result.NormalizedReadback=$result.Readback.Replace("`r`n","`n").Replace("`r","`n")
    $result.TextPassed=[string]::Equals($result.NormalizedExpected,$result.NormalizedReadback,[StringComparison]::Ordinal)
    if(-not $result.TextPassed){throw 'needs:local: Notepad readback is empty or differs from input; no alternate input attempted'}
} catch {
    $failures.Add($_.Exception.ToString())
    $result.ForegroundAtFailure=Get-UiaForegroundInfo
    if($null -ne $document){
        try {$result.EditorDescription=Get-UiaPatternDescription $document} catch {$result.EditorDiagnosticError=$_.Exception.ToString()}
    }
} finally {
    # Preserve one GDI image even when the input gate fails; no game is launched.
    if($null -ne $window){
        try {
            $result.Screenshot=Save-UiaGdiScreenshot $window "$ev/notepad.png"
            if(-not $result.Screenshot.Nonblank){throw 'GDI screenshot is uniformly black or white'}
        } catch {$failures.Add($_.Exception.ToString())}
    }
    if($null -ne $owned){
        try {
            if(-not $owned.HasExited){
                if($null -eq $window){$window=Get-UiaWindow -ProcessId $owned.Id}
                # Post a close request without blocking the UIA caller on a modal save prompt.
                $h=[IntPtr]$window.Current.NativeWindowHandle
                if(-not [UiaGdiNative]::PostMessage($h,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)){
                    throw 'Could not request WM_CLOSE for owned Notepad'
                }
                $deadline=[DateTime]::UtcNow.AddSeconds(5)
                do {
                    if($owned.WaitForExit(100)){break}
                    $pidCondition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$owned.Id)
                    $roots=@([Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$pidCondition))
                    $buttons=@(foreach($root in $roots){
                        $root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) | Where-Object {
                            $_.Current.ProcessId -eq $owned.Id -and
                            $_.Current.ControlType -eq [Windows.Automation.ControlType]::Button -and
                            ($_.Current.Name -replace '&','') -match '^(不要儲存|不儲存|Don''t Save)(\([A-Za-z]\))?$'
                        }
                    })
                    if($buttons.Count -gt 1){throw 'Multiple Dont Save buttons found; refusing ambiguous close'}
                    if($buttons.Count -eq 1){
                        @($roots | ForEach-Object {Get-UiaTree $_}) | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 "$ev/notepad-close-tree.json"
                        $result.DontSaveButton=[ordered]@{Name=$buttons[0].Current.Name;AutomationId=$buttons[0].Current.AutomationId;ProcessId=$buttons[0].Current.ProcessId}
                        Invoke-UiaButton $buttons[0]
                        $result.DontSaveInvoked=$true
                        break
                    }
                } while([DateTime]::UtcNow -lt $deadline)
            }
            $result.Closed=$owned.WaitForExit(3000)
            if(-not $result.Closed){throw 'Owned Notepad did not exit after the no-save close request'}
            if($result.TextPassed -and -not $result.DontSaveInvoked){throw 'Input passed but no Dont Save invocation was observed'}
        } catch {$failures.Add($_.Exception.ToString())}
        finally {
            if(-not $owned.HasExited){
                try {
                    # Failure-only cleanup: verify the exact process we started; never terminate by name.
                    $current=Get-Process -Id $owned.Id -ErrorAction Stop
                    if($current.ProcessName -ne 'notepad' -or $current.StartTime.ToString('o') -ne $result.StartTime){throw 'Owned PID identity mismatch'}
                    $current.Kill()
                    $result.ForcedClose=$true
                    $result.Closed=$current.WaitForExit(3000)
                    $failures.Add('Forced cleanup was needed; no-save UIA close did not pass')
                } catch {$failures.Add($_.Exception.ToString())}
            }
        }
    }
    $result.CompletedAt=[DateTimeOffset]::Now.ToString('o')
    $result.Errors=@($failures.ToArray())
    if($failures.Count -eq 0 -and $result.TextPassed -and $result.Closed){$result.ExitCode=0}
    $result | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 "$ev/notepad-result.json"
}
$result | ConvertTo-Json -Depth 8
exit $result.ExitCode
