param([string]$NuGetConfig = 'engine/NuGet.Config')
# 離線 B07 音效選擇檢查（#433）。需先以 rebuild.py --build 產出 engine/build/client-product-dev。
# 不讀遊戲素材、WAV，不開音訊裝置。完整 combat-sfx 套件（需 engine/assets 與客戶端 WAV）不在此腳本。
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location $root
$output = Join-Path $root 'scratch/issue-433'
New-Item -ItemType Directory -Force "$output/tmp", "$output/cli" | Out-Null
$env:TEMP = "$output/tmp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$output/cli"
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_NOLOGO = '1'
$env:NUGET_PACKAGES = "$root/engine/.nuget"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
$project = 'engine/_integration/dev/combat-sfx-tests/CombatSfxChecks.csproj'
& dotnet restore $project --configfile $NuGetConfig
if ($LASTEXITCODE -ne 0) { throw 'combat-sfx restore failed' }
& dotnet run --project $project -c Release --no-restore -p:UseSharedCompilation=false -- . none "$output/selection.json" --selection
if ($LASTEXITCODE -ne 0) { throw 'combat-sfx selection checks failed' }
