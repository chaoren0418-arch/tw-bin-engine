param([string]$ConfigFile)
# dev 建置入口（Windows）。等同 python rebuild.py --build；可用 -ConfigFile 指定 NuGet 設定（CI 用 .github/nuget.ci.config）。
$ErrorActionPreference='Stop'
$engine=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if((Split-Path $engine -Leaf) -ne 'engine' -or -not (Test-Path -LiteralPath (Join-Path $engine '../project-manifest.json'))){throw 'Run from <repo>/engine/_integration/dev.'}
$argv=@('-B','-X','utf8',(Join-Path $PSScriptRoot 'rebuild.py'),'--build')
if($ConfigFile){$argv+=@('--configfile',$ConfigFile)}
& python @argv
if($LASTEXITCODE -ne 0){throw 'dev build refused.'}
