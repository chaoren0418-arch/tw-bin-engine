# Deterministic contracts with explicitly mocked UI failures; NOT native/IME acceptance evidence.
param([string]$EvidenceDirectory='scratch/issue-1247/contracts')
$ErrorActionPreference='Stop'
$ev=[IO.Path]::GetFullPath($EvidenceDirectory)
$null=New-Item -ItemType Directory -Force $ev,"$ev/tmp"
$env:TEMP="$ev/tmp";$env:TMP=$env:TEMP
. "$PSScriptRoot/UiaGdi.ps1"
. "$PSScriptRoot/SmokeTools.ps1"
. "$PSScriptRoot/SmokeRetest.ps1"
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Passed,$Detail){
    $checks.Add([pscustomobject]@{Name=$Name;Passed=$Passed;Detail=$Detail;Scope='contract/mock, not native acceptance'})
    if(-not $Passed){throw "Failed: $Name"}
}
try {
    Import-SmokeRetestNative
    Check 'dpi-interop-compiles' ($null -ne ('SmokeRetestNative' -as [type])) 'No settings are changed'
    $script:tries=0
    function Clear-SmokeChatAttempt {
        param($Hwnd,$StatePath,$EvidencePrefix,$InputRect,[switch]$Fill)
        $script:tries++
        if($script:tries -lt 3){throw 'Injected focus/positive-control failure'}
        [pscustomobject]@{Empty=$true;Marker='mock success on third try'}
    }
    $r=Clear-SmokeChat ([IntPtr]1) 'unused' "$ev/recovered" @(143,586,338,12) -MaxAttempts 3
    $saved=Read-SmokeJson "$ev/recovered-clear.json"
    Check 'retry-recovers-with-history' ($r.Empty -and $r.Attempts.Count -eq 3 -and -not $r.Attempts[0].Empty -and $r.Attempts[2].Empty -and $saved.Attempts.Count -eq 3) $saved
    function Clear-SmokeChatAttempt {param($Hwnd,$StatePath,$EvidencePrefix,$InputRect,[switch]$Fill);$script:tries++;throw 'Injected permanent failure'}
    $script:tries=0;$script:typed=0;$script:entered=0
    function Send-ClientText {param($Hwnd,$Text);$script:typed++}
    function Send-ClientKey {param($Hwnd,$Keys);if($Keys -eq 'Enter'){$script:entered++}}
    [IO.File]::WriteAllText("$ev/trace.jsonl",'')
    $r=Send-SmokeCommand ([IntPtr]1) 'unused' "$ev/trace.jsonl" '/buddy' 4 -InputRect @(143,586,338,12) -EvidencePrefix "$ev/exhausted" -MaxClearAttempts 2
    $saved=Read-SmokeJson "$ev/exhausted-send.json"
    Check 'exhaustion-never-types-or-submits' ($script:tries -eq 2 -and $script:typed -eq 0 -and $script:entered -eq 0 -and -not $r.SendConfirmed) $r.Status
    Check 'send-json-preserves-failed-attempts' ($saved.Clear.Attempts.Count -eq 2 -and -not $saved.Clear.Empty -and $saved.Clear.Attempts[1].Error -eq 'Injected permanent failure') $saved.Clear
    $script:tries=0
    $r=Send-SmokeCommand ([IntPtr]1) 'unused' "$ev/absent-trace.jsonl" '/buddy' 4 -InputRect @(143,586,338,12) -EvidencePrefix "$ev/missing-trace"
    Check 'trace-required-before-clearing' ($script:tries -eq 0 -and $script:entered -eq 0 -and -not $r.SendConfirmed) $r.Error
    $rejected=$false
    try {$null=Clear-SmokeChat ([IntPtr]1) 'unused' "$ev/invalid" @(143,586,338,12) -MaxAttempts 0}catch{$rejected=$true}
    Check 'retry-limit-validation' $rejected 'Zero retries cannot silently succeed'
}finally{Save-SmokeJson "$ev/results.json" @($checks.ToArray())}
$checks | Format-Table Name,Passed
