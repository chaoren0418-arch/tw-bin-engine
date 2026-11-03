$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$plan=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dedupe-plan.json') -Raw -Encoding UTF8|ConvertFrom-Json
$e=$plan.entries|Where-Object {$_.source -like '*\mysql\bin\mysql.exe'}|Select-Object -First 1
if($null -eq $e){throw 'No representative binary'}
$hold=Join-Path $PSScriptRoot 'rollback-link-hold'
foreach($path in @($e.source,$e.quarantine,$hold)){if(-not [IO.Path]::GetFullPath($path).StartsWith($root+'\')){throw 'Outside root'}}
if(Test-Path -LiteralPath $hold){throw 'Probe hold exists; reconcile'}
Move-Item -LiteralPath $e.source -Destination $hold
Move-Item -LiteralPath $e.quarantine -Destination $e.source
$restored=(Get-FileHash -LiteralPath $e.source -Algorithm SHA256).Hash.ToLowerInvariant()
if($restored -ne $e.sha256){throw 'Restored original mismatch; do not proceed'}
Move-Item -LiteralPath $e.source -Destination $e.quarantine
Move-Item -LiteralPath $hold -Destination $e.source
$reapplied=(Get-FileHash -LiteralPath $e.source -Algorithm SHA256).Hash.ToLowerInvariant()
if($reapplied -ne $e.sha256){throw 'Reapplied hardlink mismatch'}
@{utc=[DateTime]::UtcNow.ToString('o');source=$e.source;quarantine=$e.quarantine;restoredOriginalSha256=$restored;reappliedSha256=$reapplied;result='original-restored-and-hardlink-reapplied';permanentDelete=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'rollback-probe.json') -Encoding UTF8
Write-Output 'Recoverable dedupe rollback and reapply passed; no file deleted'
