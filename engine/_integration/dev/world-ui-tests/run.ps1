$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
Set-Location $root
$output = Join-Path $root 'scratch/issue-130'
New-Item -ItemType Directory -Force $output, "$output/tmp", "$output/cli" | Out-Null
$env:TEMP = "$output/tmp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$output/cli"
$env:NUGET_PACKAGES = "$root/engine/.nuget"
$env:NUGET_SCRATCH = "$output/tmp/nuget"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
foreach ($entry in @(
    @('world-ui-tests/WorldUiChecks.csproj', @($root, "$output/world-ui.json")),
    @('demo-tests/DemoChecks.csproj', @($root, "$output/demo.json")),
    @('web-resource-tests/WebResourceChecks.csproj', @("$output/web-fixture", "$output/web-resource.json"))
)) {
    $project = "engine/_integration/dev/$($entry[0])"
    & dotnet restore $project --configfile engine/NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw "restore failed: $project" }
    & dotnet run --project $project -c Release --no-restore -p:UseSharedCompilation=false -- @($entry[1])
    if ($LASTEXITCODE -ne 0) { throw "checks failed: $project" }
}
& node engine/_integration/dev/demo-tests/web-checks.cjs $root "$output/web-demo.json"
if ($LASTEXITCODE -ne 0) { throw 'Web demo checks failed' }
Write-Output "PASS T073 offline checks; evidence: $output"
