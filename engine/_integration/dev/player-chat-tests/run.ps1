param([string]$Output='scratch/issue-193/chat-checks.json')
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location $repoRoot
$outputPath=[IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
if(-not $outputPath.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    throw 'Test output must remain inside this worktree.'
}
$taskTemp=Join-Path $repoRoot 'scratch/issue-193/tmp'
New-Item -ItemType Directory -Force $taskTemp,(Split-Path $outputPath) | Out-Null
$env:TEMP=$taskTemp
$env:TMP=$taskTemp
$env:DOTNET_CLI_HOME=Join-Path $repoRoot 'scratch/issue-193/dotnet'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_NOLOGO='1'
$env:NUGET_PACKAGES=Join-Path $repoRoot 'engine/.nuget'
$env:NUGET_SCRATCH=Join-Path $taskTemp 'nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'
$env:MSBUILDDISABLENODEREUSE='1'
dotnet restore "$PSScriptRoot/PlayerChatChecks.csproj" --configfile .github/nuget.ci.config
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
dotnet run --project "$PSScriptRoot/PlayerChatChecks.csproj" -c Release --no-restore -p:UseSharedCompilation=false -- $repoRoot $outputPath
exit $LASTEXITCODE
