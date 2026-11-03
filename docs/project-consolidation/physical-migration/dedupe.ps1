$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$root=[IO.Path]::GetFullPath('D:\lineage開發\TW1608262200研究')
$plan=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dedupe-plan.json') -Raw -Encoding UTF8|ConvertFrom-Json
$journal=Join-Path $PSScriptRoot 'dedupe-journal.jsonl'
if(Test-Path -LiteralPath $journal){throw 'Existing journal: reconcile first'}
$count=0
foreach($entry in $plan.entries){
 $source=(Get-Item -LiteralPath $entry.source -Force).FullName
 $canonical=(Get-Item -LiteralPath $entry.canonical -Force).FullName
 $quarantine=[IO.Path]::GetFullPath($entry.quarantine)
 foreach($path in @($source,$canonical,$quarantine)){if(-not $path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside project'}}
 if($source -eq $canonical -or (Test-Path -LiteralPath $quarantine)){throw 'Invalid target'}
 if((Get-Item -LiteralPath $source -Force).Length -ne $entry.bytes){throw 'Source changed'}
 New-Item -ItemType Directory -Path (Split-Path -Parent $quarantine) -Force|Out-Null
 @{state='prepared';source=$source;canonical=$canonical;quarantine=$quarantine;sha256=$entry.sha256}|ConvertTo-Json -Compress|Add-Content -LiteralPath $journal -Encoding UTF8
 Move-Item -LiteralPath $source -Destination $quarantine
 New-Item -ItemType HardLink -Path $source -Target $canonical|Out-Null
 @{state='linked';source=$source;canonical=$canonical;quarantine=$quarantine;sha256=$entry.sha256}|ConvertTo-Json -Compress|Add-Content -LiteralPath $journal -Encoding UTF8
 $count++
 if($count%1000 -eq 0){Write-Output ('Recoverable dedupe '+$count)}
}
Write-Output ('DONE '+$count+' originals retained in quarantine; no bytes claimed reclaimed')
