param()
$ErrorActionPreference='Stop'
$productRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if((Split-Path $productRoot -Leaf) -ne 'engine' -or -not (Test-Path -LiteralPath (Join-Path $productRoot '../project-manifest.json'))){throw 'Build only in canonical mutable engine.'}
& python -B -X utf8 (Join-Path $PSScriptRoot 'rebuild.py')
if($LASTEXITCODE -ne 0){throw 'Accepted r33 source verification refused.'}
$productOutput=Join-Path $productRoot 'build\client-product-r33'
$env:DOTNET_CLI_HOME=Join-Path $productRoot '.dotnet'
$env:NUGET_PACKAGES=Join-Path $productRoot '.nuget'
$env:TEMP=Join-Path $productRoot 'tmp'
$env:TMP=$env:TEMP
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
Push-Location $productRoot
try {
    & dotnet build (Join-Path $productRoot 'source\OfflineClient.csproj') -c Release -o $productOutput --configfile (Join-Path $productRoot 'NuGet.Config') --nologo -p:NuGetAudit=false -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0){throw '新版客戶端建置失敗。'}
    $productFiles=@(Get-ChildItem -LiteralPath $productOutput -Recurse -File)+@(Get-ChildItem -LiteralPath (Join-Path $productRoot 'web') -Recurse -File)+@(Get-Item -LiteralPath (Join-Path $productRoot 'assets\integrity.json'))
    $pins=@($productFiles | Sort-Object FullName | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($productRoot,$_.FullName).Replace('\','/');size=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
    $manifest=[ordered]@{schema='p24-normal-product-v1';entry='build/client-product-r33/P11.ClientEngine.exe';arguments=@('--normal-product');profile='p24-real-jar-d287-client2';port=42061;autoLogin=$false;files=$pins}
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $productRoot 'build\product-r33-manifest.json') -Encoding utf8
    Write-Output '新版客戶端已建置；未啟動視窗或連線。'
} finally {Pop-Location}
