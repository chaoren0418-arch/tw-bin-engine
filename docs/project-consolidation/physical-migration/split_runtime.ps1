$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath('D:\lineage開發\TW1608262200研究')
$journal=Join-Path $PSScriptRoot 'runtime-archives.json'
if(Test-Path -LiteralPath $journal){throw 'Existing receipt; reconcile first'}
$rows=@()
foreach($name in @('runs','runtime','.nuget','.dotnet','tmp')){
 $source=[IO.Path]::GetFullPath((Join-Path $root ('releases\r28\'+$name)))
 $target=[IO.Path]::GetFullPath((Join-Path $root ('evidence\r28-runtime-before-consolidation\'+$name)))
 if($name -in @('.nuget','.dotnet','tmp')){$target=[IO.Path]::GetFullPath((Join-Path $root ('recovery\r28-build-cache\'+$name)))}
 if(-not $source.StartsWith($root+'\') -or -not $target.StartsWith($root+'\')){throw 'Outside project'}
 if(Test-Path -LiteralPath $target){throw 'Target exists'}
 if(-not(Test-Path -LiteralPath $source)){continue}
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force|Out-Null
 $rows+=@{source=$source;target=$target;state='prepared'}
 $rows|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $journal -Encoding UTF8
 Move-Item -LiteralPath $source -Destination $target
 $rows[-1].state='moved'
 $rows|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $journal -Encoding UTF8
 Write-Output ('Archived '+$name)
}
