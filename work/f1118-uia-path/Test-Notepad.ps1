param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$ev=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $ev,(Join-Path $ev 'tmp') | Out-Null
$env:TEMP=Join-Path $ev 'tmp';$env:TMP=$env:TEMP
. "$PSScriptRoot/UiaGdi.ps1"
$owned=$null;$window=$null;$document=$null;$changed=$false;$failure=$null
$result=[ordered]@{ValuePatternPassed=$false;Closed=$false}
try {
    $owned=Start-Process "$env:WINDIR/System32/notepad.exe" -PassThru
    $result.ProcessId=$owned.Id
    $result.StartTime=$owned.StartTime.ToString('o')
    $null=$owned.WaitForInputIdle(5000)
    $window=Get-UiaWindow -ProcessId $owned.Id
    $result.Window=Get-UiaWindowInfo $window
    Get-UiaTree $window | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 "$ev/notepad-tree.json"
    # Windows 10 Notepad's editor is Document/15; the five Edit nodes are status panes.
    $document=Find-UiaControl $window -AutomationId '15' -Type Document
    $result.EditorPatterns=@($document.GetSupportedPatterns() | ForEach-Object {$_.ProgrammaticName})
    $text='Issue 1126 UIA + GDI probe. No file will be saved.'
    Set-UiaValue $document $text
    $changed=$true
    $result.Readback=Get-UiaValue $document
    if($result.Readback -ne $text){throw 'Notepad ValuePattern readback mismatch'}
    $result.ValuePatternPassed=$true
} catch {
    $failure=$_
    $result.Error=$_.Exception.ToString()
} finally {
    # Even if the input gate fails, preserve one read-only GDI diagnostic; no game is launched.
    if($null -ne $window){
        try {$result.Screenshot=Save-UiaGdiScreenshot $window "$ev/notepad.png"} catch {$result.ScreenshotError=$_.Exception.ToString();if(-not $failure){$failure=$_}}
    }
    if($null -ne $owned){
        try {
            if($changed){Set-UiaValue $document ''}
            if($null -ne $window){($window.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern)).Close()}
            $result.Closed=$owned.WaitForExit(3000)
            if(-not $result.Closed){
                # Only the process created above, never a name-wide termination.
                $current=Get-Process -Id $owned.Id -ErrorAction Stop
                if($current.ProcessName -ne 'notepad' -or $current.StartTime.ToString('o') -ne $result.StartTime){throw 'Owned PID identity mismatch'}
                $current.Kill();$result.Closed=$current.WaitForExit(3000)
                $result.ForcedClose=$true
            }
        } catch {$result.CleanupError=$_.Exception.ToString();if(-not $failure){$failure=$_}}
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 "$ev/notepad-result.json"
}
if($failure){throw $failure}
if(-not $result.Screenshot.Nonblank){throw 'GDI diagnostic is uniformly black or white'}
if(-not $result.Closed){throw 'Owned Notepad was not closed'}
$result | ConvertTo-Json -Depth 8
