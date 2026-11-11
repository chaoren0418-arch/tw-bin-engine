param([switch]$Native,[string]$AssetsRoot,[string]$ConfigFile='engine/NuGet.Config')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location $root
$output=Join-Path $root 'scratch/issue-145'
foreach($entry in @{'TEMP'='tmp';'TMP'='tmp';'DOTNET_CLI_HOME'='cli';'NUGET_PACKAGES'='packages';'NUGET_HTTP_CACHE_PATH'='http';'NUGET_SCRATCH'='nuget'}.GetEnumerator()) {
    $path=Join-Path $output $entry.Value
    New-Item -ItemType Directory -Force $path | Out-Null
    [Environment]::SetEnvironmentVariable($entry.Key,$path,'Process')
}
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_NOLOGO='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$project=Join-Path $PSScriptRoot 'Effect6016Checks.csproj'
& dotnet restore $project --configfile $ConfigFile
if($LASTEXITCODE -ne 0){throw 'restore failed'}
& dotnet build $project -c Release --no-restore -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'build failed'}
$testArgs=@($output,"$output/result.json")
if($Native){$testArgs+='--native'}
if($AssetsRoot){$testArgs+=@('--assets-root',$AssetsRoot)}
& dotnet "$PSScriptRoot/bin/Release/net10.0-windows/Effect6016Checks.dll" @testArgs
if($LASTEXITCODE -ne 0){throw '6016 checks failed'}
