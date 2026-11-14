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
    # Honour TreeScope like the managed API: Children = direct children only, Descendants = whole subtree.
    $e | Add-Member ScriptMethod FindAll {
        param($scope,$condition)
        if($scope -eq [Windows.Automation.TreeScope]::Children){return $this.Children}
        foreach($child in $this.Children){$child;$child.FindAll($scope,$condition)}
    }
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
    # Real managed shape (#1165 selection-api.json): SelectionPattern exposes only Current/Cached;
    # the selection is read via SelectionPattern.SelectionPatternInformation.GetSelection().
    # GetCurrentSelection is the COM IUIAutomationSelectionPattern name and must NOT exist here.
    $realPattern=[Windows.Automation.SelectionPattern]
    $realInfo=[Windows.Automation.SelectionPattern+SelectionPatternInformation]
    Assert ($null -eq $realPattern.GetMethod('GetCurrentSelection')) 'Managed SelectionPattern unexpectedly has GetCurrentSelection; fixture shape must be revisited'
    Assert ($null -ne $realPattern.GetProperty('Current') -and $null -ne $realInfo.GetMethod('GetSelection',[type[]]@())) 'Managed SelectionPattern.Current.GetSelection() not found'
    $tabSelection=[pscustomobject]@{Selected=@($item);Reads=0;FailRead=$false;CanSelectMultiple=$false;IsSelectionRequired=$true}
    $tabSelection | Add-Member ScriptMethod GetSelection {
        $this.Reads++
        if($this.FailRead){throw 'fixture selection provider original text'}
        $this.Selected
    }
    $tabPattern=[pscustomobject]@{Current=$tabSelection;Cached=$null}
    $tab | Add-Member NoteProperty Pattern $tabPattern
    $tab | Add-Member ScriptMethod GetCurrentPattern {param($pattern) $this.Pattern}
    # Regression guard: calling GetCurrentSelection on the pattern must fail, exactly like the real type.
    Assert ($null -eq $tabPattern.PSObject.Methods['GetCurrentSelection']) 'Fixture pattern must not provide GetCurrentSelection'
    $legacyError=$null
    try {$null=$tabPattern.GetCurrentSelection()} catch {$legacyError=$_.Exception.Message}
    Assert ($null -ne $legacyError -and $legacyError.Contains('GetCurrentSelection')) 'Pattern-level GetCurrentSelection must fail'
    $helperSource=(Get-Command Select-UiaTab).Definition
    Assert (-not $helperSource.Contains('GetCurrentSelection') -and $helperSource.Contains('.Current.GetSelection()')) 'Select-UiaTab must read SelectionPattern.Current.GetSelection()'
    $accepted=Select-UiaTab $tab 'target'
    $cases=[Collections.Generic.List[object]]::new()
    foreach($scenario in @('wrong-name','not-selected','multiple','provider-error')){
        $itemPattern.Current.IsSelected=$true;$tabSelection.Selected=@($item);$tabSelection.FailRead=$false
        switch($scenario){
            'wrong-name' {$tabSelection.Selected=@(New-FixtureElement 'wrong' 'TabItem')}
            'not-selected' {$itemPattern.Current.IsSelected=$false}
            'multiple' {$tabSelection.Selected=@($item,$item)}
            'provider-error' {$tabSelection.FailRead=$true}
        }
        $failure=$null
        try {$null=Select-UiaTab $tab 'target'} catch {$failure=$_.Exception.ToString()}
        Assert ($null -ne $failure -and $failure.Contains('TabItem') -and $failure.Contains('TabSelection')) "Missing both observations for $scenario"
        if($scenario -eq 'provider-error'){Assert ($failure.Contains('fixture selection provider original text')) 'Provider error text lost'}
        $cases.Add([pscustomobject]@{Scenario=$scenario;Error=$failure})
    }
    Assert ($itemPattern.SelectCalls -eq 5 -and $tabSelection.Reads -eq 5) 'Selection must execute once and read both sides per call'
    [pscustomobject]@{Accepted=$accepted;Rejections=$cases.ToArray();SelectCalls=$itemPattern.SelectCalls;ReadCalls=$tabSelection.Reads;LegacyCallError=$legacyError}
}
Check 'read-error-keeps-node-identity' {
    # #1173 G06: a node with Name=null used to keep only Error, so the broken control could not be named (#1178).
    $nullName=New-FixtureElement 'placeholder' 'Text'
    $nullName.Current.Name=$null;$nullName.Current.AutomationId='sceneStatus';$nullName.Current.ClassName='FixtureParagraph'
    $node=Read-UiaElement $nullName -MaskPassword
    Assert ($null -ne $node.PSObject.Properties['Error']) 'Null Name must still be an error node'
    Assert ($node.Error.Contains("UIA property 'Name' is null")) 'Error must name the failing property'
    Assert ($node.Type -ceq 'ControlType.Text' -and $node.Id -ceq 'sceneStatus' -and $node.Class -ceq 'FixtureParagraph') 'Readable Type/Id/Class must be kept'
    Assert ($node.Rectangle -ceq '0,0,1,1' -and $node.Enabled -eq $true -and $null -eq $node.Name) 'Readable Rectangle/Enabled must be kept; Name stays null'
    # One property throwing (provider text preserved) must not hide the rest either.
    $throwing=New-FixtureElement 'kept name' 'Image'
    $throwing.Current | Add-Member ScriptProperty ClassName {throw 'fixture ClassName provider original text'} -Force
    $partial=Read-UiaElement $throwing
    Assert ($partial.Error.Contains('ClassName') -and $partial.Error.Contains('fixture ClassName provider original text')) 'Throwing property text lost'
    Assert ($partial.Name -ceq 'kept name' -and $partial.Type -ceq 'ControlType.Image' -and $null -eq $partial.Class) 'Other properties must survive a throwing one'
    # The G06 judgement is not relaxed: the tree still counts it, Find still skips it.
    $root=New-FixtureElement 'root' 'Window';$root.Children=@($nullName,(New-FixtureElement 'live' 'Text'))
    $tree=@(Get-UiaTree $root)
    $treeErrors=@($tree | Where-Object {$_.PSObject.Properties['Error']})
    Assert ($tree.Count -eq 3 -and $treeErrors.Count -eq 1 -and $treeErrors[0].Id -ceq 'sceneStatus') 'Tree must report the error node with its identity'
    $readErrors=$null;$skipped=$null
    try {$null=Find-UiaControl $root -AutomationId 'sceneStatus' -ReadErrors ([ref]$readErrors)} catch {$skipped=$_.Exception.Message}
    Assert ($null -ne $skipped -and $skipped.Contains('found 0') -and @($readErrors).Count -eq 1) 'Find must keep error nodes out of matching and list them as read errors'
    [pscustomobject]@{NullName=$node;ThrowingClass=$partial;FindError=$skipped}
}
Check 'tab-select-refuses-disabled' {
    # #1173 N01: in CharacterList the service tabs are Enabled=false; the helper must say so and not Select (#1178).
    $results=[Collections.Generic.List[object]]::new()
    foreach($scenario in @('item-disabled','tab-disabled','tab-enabled-read-error')){
        $item=New-FixtureElement '聲音' 'TabItem'
        $pattern=[pscustomobject]@{Current=[pscustomobject]@{IsSelected=$false};SelectCalls=0}
        $pattern | Add-Member ScriptMethod Select {$this.SelectCalls++}
        $item | Add-Member NoteProperty Pattern $pattern
        $item | Add-Member ScriptMethod GetCurrentPattern {param($p) $this.Pattern}
        $tab=New-FixtureElement '開發工具服務分頁' 'Tab';$tab.Children=@($item)
        $tabPattern=[pscustomobject]@{Current=[pscustomobject]@{Selected=@()}}
        $tabPattern.Current | Add-Member ScriptMethod GetSelection {$this.Selected}
        $tab | Add-Member NoteProperty Pattern $tabPattern
        $tab | Add-Member ScriptMethod GetCurrentPattern {param($p) $this.Pattern}
        switch($scenario){
            'item-disabled' {$item.Current.IsEnabled=$false;$tab.Current.IsEnabled=$false}
            'tab-disabled' {$tab.Current.IsEnabled=$false}
            'tab-enabled-read-error' {$tab.Current | Add-Member ScriptProperty IsEnabled {throw 'fixture IsEnabled provider original text'} -Force}
        }
        $failure=$null
        try {$null=Select-UiaTab $tab '聲音'} catch {$failure=$_.Exception.ToString()}
        Assert ($null -ne $failure -and $failure.Contains('IsEnabled') -and $failure.Contains('TabItemIsEnabled') -and $failure.Contains('TabIsEnabled')) "Missing IsEnabled observation for $scenario"
        Assert ($failure.Contains('RequestedName')) "Missing RequestedName for $scenario"
        Assert ($pattern.SelectCalls -eq 0) "Select must not be called for $scenario"
        if($scenario -eq 'tab-enabled-read-error'){Assert ($failure.Contains('fixture IsEnabled provider original text')) 'IsEnabled provider text lost'}
        $results.Add([pscustomobject]@{Scenario=$scenario;SelectCalls=$pattern.SelectCalls;Error=$failure})
    }
    $results.ToArray()
}
Check 'find-scope-children-outer-tab' {
    # Service page shape (#1165): dev window -> outer Tab -> TabItem -> nested service Tab.
    $inner=New-FixtureElement 'inner tabs' 'Tab'
    $page=New-FixtureElement 'service' 'TabItem';$page.Children=@($inner)
    $outer=New-FixtureElement 'outer tabs' 'Tab';$outer.Children=@($page)
    $dev=New-FixtureElement 'dev' 'Window';$dev.Children=@($outer)
    $ambiguous=$null
    try {$null=Find-UiaControl $dev -Type Tab} catch {$ambiguous=$_.Exception.Message}
    Assert ($null -ne $ambiguous -and $ambiguous.Contains('found 2')) 'Default Descendants scope must still see both Tabs'
    $explicit=$null
    try {$null=Find-UiaControl $dev -Type Tab -Scope Descendants} catch {$explicit=$_.Exception.Message}
    Assert ($null -ne $explicit -and $explicit.Contains('found 2')) 'Explicit Descendants must match the default'
    $found=Find-UiaControl $dev -Type Tab -Scope Children
    Assert ([object]::ReferenceEquals($found,$outer)) 'Children scope must return only the outer Tab'
    $badScope=$null
    try {$null=Find-UiaControl $dev -Type Tab -Scope Subtree} catch {$badScope=$_.Exception.Message}
    Assert ($null -ne $badScope) 'Unknown scope must be rejected'
    [pscustomobject]@{DescendantsError=$ambiguous;ChildrenFound=$found.Current.Name;BadScopeError=$badScope}
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
