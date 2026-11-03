param([Parameter(Mandatory=$true)][ValidateSet('r28','external')][string]$Batch)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$projectRoot=[IO.Path]::GetFullPath('D:\lineage開發\TW1608262200研究')
$receiptRoot=$PSScriptRoot
$plan=@()
if($Batch -eq 'r28'){
 $plan+=@{source=(Join-Path $projectRoot 'P24-原2x2地圖快取與跨區載入\reference\development\release-integration-r12\delivery');target=(Join-Path $projectRoot 'releases\r28');snapshot='before-r28'}
}else{
 $names=@('P10Lab-TW13081901')+@(12..23|ForEach-Object{'P'+$_+'Lab-ClientEngine'})
 foreach($name in $names){$plan+=@{source=('D:\'+$name);target=(Join-Path $projectRoot ('history\labs\'+$name));snapshot=('before-'+$name)}}
 $plan+=@{source='D:\P24Lab-ClientEngine';target=(Join-Path $projectRoot 'environments\p24');snapshot='before-P24Lab-ClientEngine'}
 $plan+=@{source='D:\P24Lab-6016-Export-r1';target=(Join-Path $projectRoot 'history\research\6016-export-r1');snapshot='before-P24Lab-6016-Export-r1'}
 $plan+=@{source='D:\TW1608262200_research';target=(Join-Path $projectRoot 'history\research\ascii');snapshot='before-TW1608262200_research'}
}
$log=Join-Path $receiptRoot ('moves-'+$Batch+'.jsonl')
if(Test-Path -LiteralPath $log){throw 'Existing journal: reconcile before repeating.'}
foreach($item in $plan){
 $src=(Get-Item -LiteralPath $item.source -Force).FullName
 $dst=[IO.Path]::GetFullPath($item.target)
 if($src -ne [IO.Path]::GetFullPath($item.source)){throw 'Source canonical mismatch'}
 if(-not $dst.StartsWith($projectRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Target outside project'}
 if([IO.Path]::GetPathRoot($src) -ne [IO.Path]::GetPathRoot($dst)){throw 'Not same-volume move'}
 if((Get-Item -LiteralPath $src -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refuse reparse source'}
 if(Test-Path -LiteralPath $dst){throw 'Target already exists'}
 $snap=Get-Content -LiteralPath (Join-Path $receiptRoot ($item.snapshot+'.json')) -Raw -Encoding UTF8|ConvertFrom-Json
 if($snap.root -ne $src){throw 'Snapshot source mismatch'}
 $pin=(Get-FileHash -LiteralPath $snap.snapshot -Algorithm SHA256).Hash.ToLowerInvariant()
 if($pin -ne $snap.snapshot_sha256){throw 'Snapshot checksum mismatch'}
 New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force|Out-Null
 @{utc=[DateTime]::UtcNow.ToString('o');state='prepared';source=$src;target=$dst;snapshot=$item.snapshot}|ConvertTo-Json -Compress|Add-Content -LiteralPath $log -Encoding UTF8
 Move-Item -LiteralPath $src -Destination $dst
 if((Test-Path -LiteralPath $src) -or -not(Test-Path -LiteralPath $dst -PathType Container)){throw 'Uncertain move outcome; inspect journal'}
 @{utc=[DateTime]::UtcNow.ToString('o');state='moved';source=$src;target=$dst;snapshot=$item.snapshot}|ConvertTo-Json -Compress|Add-Content -LiteralPath $log -Encoding UTF8
 Write-Output ('MOVED '+$src+' -> '+$dst)
}
