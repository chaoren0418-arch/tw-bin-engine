param([string]$NuGetConfig = 'engine/NuGet.Config')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location $root
$output = Join-Path $root 'scratch/issue-267'
New-Item -ItemType Directory -Force "$output/tmp", "$output/cli" | Out-Null
$env:TEMP = "$output/tmp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$output/cli"
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_NOLOGO = '1'
$env:NUGET_PACKAGES = "$root/engine/.nuget"
$env:NUGET_SCRATCH = "$output/tmp/nuget"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
$project = 'engine/_integration/dev/mail-tests/MailChecks.csproj'
& dotnet restore $project --configfile $NuGetConfig
if ($LASTEXITCODE -ne 0) { throw 'mail restore failed' }
& dotnet run --project $project -c Release --no-restore -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'mail checks failed' }
