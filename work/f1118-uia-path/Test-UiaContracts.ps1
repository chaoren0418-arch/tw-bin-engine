param([Parameter(Mandatory)][string]$EvidenceDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$ev=[IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force $ev,(Join-Path $ev 'tmp') | Out-Null
$env:TEMP=Join-Path $ev 'tmp';$env:TMP=$env:TEMP
. "$PSScriptRoot/UiaGdi.ps1"
# Explicit unit fixtures only. These are NOT client or Notepad UIA evidence.
$checks=[Collections.Generic.List[object]]::new()
function Check($name,$body){
    try {$detail=& $body;$checks.Add([pscustomobject]@{Name=$name;Passed=$true;Detail=$detail})}
    catch {$checks.Add([pscustomobject]@{Name=$name;Passed=$false;Error=$_.Exception.ToString()})}
}
function Assert($condition,$message){if(-not $condition){throw $message}}
function New-FixtureElement($name,$type='Button'){
    $e=[pscustomobject]@{Current=[pscustomobject]@{Name=$name;ControlType=[pscustomobject]@{ProgrammaticName="ControlType.$type"};AutomationId='fixture';ClassName='Fixture';BoundingRectangle='0,0,1,1';IsPassword=$false;IsEnabled=$true;IsOffscreen=$false};Children=@()}
    $e | Add-Member ScriptMethod GetSupportedPatterns { @() }
    $e | Add-Member ScriptMethod FindAll { param($scope,$condition) $this.Children }
    $e
}
Check 'read-errors-and-live-sibling' {
    $good=New-FixtureElement 'target'
    $bad=New-FixtureElement 'vanished'
    $bad | Add-Member ScriptMethod GetSupportedPatterns {throw [Windows.Automation.ElementNotAvailableException]::new('fixture original vanished text')} -Force
    $nullType=New-FixtureElement 'null type';$nullType.Current.ControlType=$null
    $root=New-FixtureElement 'root' 'Window';$root.Children=@($bad,$nullType,$good)
    $tree=@(Get-UiaTree $root)
    $treeErrors=@($tree | Where-Object {$_.PSObject.Properties['Error']})
    Assert ($tree.Count -eq 4 -and $treeErrors.Count -eq 2) 'Tree must retain live siblings and error records'
    Assert ($treeErrors[0].Error.Contains('fixture original vanished text')) 'Original exception lost'
    $readErrors=$null;$found=Find-UiaControl $root -Name 'target' -Type Button -ReadErrors ([ref]$readErrors)
    Assert ([object]::ReferenceEquals($found,$good) -and $readErrors.Count -eq 2) 'Find must return the live element and retain errors'
    $missing=$null
    try {$null=Find-UiaControl $root -Name 'absent'} catch {$missing=$_.Exception.ToString()}
    Assert ($null -ne $missing -and $missing.Contains('fixture original vanished text')) 'Missing target error lost original provider text'
    # Mask tree output only: lookup must still use the accessible name (never the value).
    $good.Current.IsPassword=$true
    $masked=Read-UiaElement $good -MaskPassword
    Assert ($masked.Name -ceq '[PASSWORD]') 'Password tree name must remain masked'
    $named=Find-UiaControl $root -Name 'target' -Type Button
    Assert ([object]::ReferenceEquals($named,$good)) 'Tree redaction must not change control lookup'
    [pscustomobject]@{Tree=$tree;FindErrors=$readErrors;Missing=$missing}
}
Check 'tab-selection-dual-readback' {
    $item=New-FixtureElement 'target' 'TabItem'
    $itemPattern=[pscustomobject]@{Current=[pscustomobject]@{IsSelected=$true};SelectCalls=0}
    $itemPattern | Add-Member ScriptMethod Select {$this.SelectCalls++}
    $item | Add-Member NoteProperty Pattern $itemPattern
    $item | Add-Member ScriptMethod GetCurrentPattern {param($pattern) $this.Pattern}
    $tab=New-FixtureElement 'tabs' 'Tab';$tab.Children=@($item)
    $tabPattern=[pscustomobject]@{Selected=@($item);Reads=0;FailRead=$false}
    $tabPattern | Add-Member ScriptMethod GetCurrentSelection {
        $this.Reads++
        if($this.FailRead){throw 'fixture selection provider original text'}
        $this.Selected
    }
    $tab | Add-Member NoteProperty Pattern $tabPattern
    $tab | Add-Member ScriptMethod GetCurrentPattern {param($pattern) $this.Pattern}
    $accepted=Select-UiaTab $tab 'target'
    $cases=[Collections.Generic.List[object]]::new()
    foreach($scenario in @('wrong-name','not-selected','multiple','provider-error')){
        $itemPattern.Current.IsSelected=$true;$tabPattern.Selected=@($item);$tabPattern.FailRead=$false
        switch($scenario){
            'wrong-name' {$tabPattern.Selected=@(New-FixtureElement 'wrong' 'TabItem')}
            'not-selected' {$itemPattern.Current.IsSelected=$false}
            'multiple' {$tabPattern.Selected=@($item,$item)}
            'provider-error' {$tabPattern.FailRead=$true}
        }
        $failure=$null
        try {$null=Select-UiaTab $tab 'target'} catch {$failure=$_.Exception.ToString()}
        Assert ($null -ne $failure -and $failure.Contains('TabItem') -and $failure.Contains('TabSelection')) "Missing both observations for $scenario"
        if($scenario -eq 'provider-error'){Assert ($failure.Contains('fixture selection provider original text')) 'Provider error text lost'}
        $cases.Add([pscustomobject]@{Scenario=$scenario;Error=$failure})
    }
    Assert ($itemPattern.SelectCalls -eq 5 -and $tabPattern.Reads -eq 5) 'Selection must execute once and read both sides per call'
    [pscustomobject]@{Accepted=$accepted;Rejections=$cases.ToArray();SelectCalls=$itemPattern.SelectCalls;ReadCalls=$tabPattern.Reads}
}
Check 'invoke-deadline-no-retry' {
    Add-Type -TypeDefinition @'
using System;
using System.Threading;
public static class UiaDeadlineFixture {
 public static int Calls;
 public static bool Completed;
 public static Action Blocking { get { return () => { Interlocked.Increment(ref Calls); Thread.Sleep(350); Completed=true; }; } }
}
'@
    # Exercise the same private MTA/deadline implementation without opening a fake GUI.
    $run=[UiaBoundedInvoke].GetMethod('Run',[Reflection.BindingFlags]'Static,NonPublic')
    $watch=[Diagnostics.Stopwatch]::StartNew();$errorText=$null
    try {$null=$run.Invoke($null,@([UiaDeadlineFixture]::Blocking,[int]25))} catch {$errorText=$_.Exception.ToString()}
    $elapsed=$watch.ElapsedMilliseconds
    Assert ($null -ne $errorText -and $errorText.Contains('TimeoutException')) 'Blocked worker did not time out'
    Assert ($elapsed -lt 300) 'Caller waited for the blocked action'
    Start-Sleep -Milliseconds 400
    Assert ([UiaDeadlineFixture]::Calls -eq 1 -and [UiaDeadlineFixture]::Completed) 'Action must not retry or be aborted'
    [pscustomobject]@{TimeoutMs=25;ElapsedMs=$elapsed;Calls=[UiaDeadlineFixture]::Calls;CompletedAfterTimeout=[UiaDeadlineFixture]::Completed;Error=$errorText}
}
Check 'evidence-time-filter' {
    # Files are filter fixtures (plain text), never presented as screenshots.
    $dir=Join-Path $ev 'filter-fixtures';New-Item -ItemType Directory -Force $dir | Out-Null
    $since=[DateTime]::UtcNow
    foreach($name in @('manual-old.png','manual-equal.png','manual-new.png','other.png')){
        [IO.File]::WriteAllText((Join-Path $dir $name),'UNIT FIXTURE, NOT AN IMAGE')
    }
    [IO.File]::SetLastWriteTimeUtc((Join-Path $dir 'manual-old.png'),$since.AddSeconds(-1))
    [IO.File]::SetLastWriteTimeUtc((Join-Path $dir 'manual-equal.png'),$since)
    [IO.File]::SetLastWriteTimeUtc((Join-Path $dir 'manual-new.png'),$since.AddSeconds(1))
    [IO.File]::SetLastWriteTimeUtc((Join-Path $dir 'other.png'),$since.AddSeconds(1))
    $found=@(Get-UiaNewEvidence $dir $since)
    Assert ($found.Count -eq 1 -and $found[0].Name -ceq 'manual-new.png') 'Filter must require pattern AND strictly newer UTC'
    [pscustomobject]@{SinceUtc=$since.ToString('o');Matched=@($found.Name);FixtureOnly=$true}
}
$result=[pscustomobject]@{Kind='unit fixtures, not native UI verification';Checks=$checks.ToArray();ExitCode=$(if(@($checks | Where-Object {-not $_.Passed}).Count -eq 0){0}else{1})}
$result | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 "$ev/contracts-result.json"
$result | ConvertTo-Json -Depth 12
exit $result.ExitCode
