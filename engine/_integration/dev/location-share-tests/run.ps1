param([string]$ConfigFile='.github/nuget.ci.config')
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location $repoRoot
$taskTemp=Join-Path $repoRoot 'scratch/issue-361/tmp'
New-Item -ItemType Directory -Force $taskTemp | Out-Null
$env:TEMP=$taskTemp
$env:TMP=$taskTemp
$env:DOTNET_CLI_HOME=Join-Path $repoRoot 'scratch/issue-361/build/.dotnet'
$env:NUGET_PACKAGES=Join-Path $repoRoot 'engine/.nuget'
$env:NUGET_SCRATCH=Join-Path $taskTemp 'nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:MSBUILDDISABLENODEREUSE='1'
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile $ConfigFile
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- $repoRoot scratch/issue-361/location-share.json location-share
exit $LASTEXITCODE
