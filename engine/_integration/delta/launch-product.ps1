param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$productRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$manifestPath=Join-Path $productRoot 'build\product-r28-manifest.json'
if(-not (Test-Path -LiteralPath $manifestPath)){throw '尚未建置新版。請先執行 build-product.ps1。'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if($manifest.schema -ne 'p24-normal-product-v1' -or $manifest.entry -ne 'build/client-product-r28/P11.ClientEngine.exe' -or $manifest.profile -ne 'p24-real-jar-d287-client2' -or $manifest.port -ne 42061 -or $manifest.autoLogin -ne $false -or @($manifest.arguments).Count -ne 1 -or $manifest.arguments[0] -ne '--normal-product'){throw '新版啟動設定不一致，請重新建置。'}
foreach($pin in $manifest.files){
    $target=[IO.Path]::GetFullPath((Join-Path $productRoot $pin.path))
    if(-not $target.StartsWith($productRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '新版檔案路徑不正確。'}
    if(-not (Test-Path -LiteralPath $target -PathType Leaf)){throw "缺少新版檔案：$($pin.path)"}
    if((Get-Item -LiteralPath $target).Length -ne $pin.size -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $pin.sha256){throw "新版檔案已變更：$($pin.path)，請重新建置。"}
}
$assetRoot=Join-Path $productRoot 'assets'
$assetInventory=Get-Content -LiteralPath (Join-Path $assetRoot 'integrity.json') -Raw | ConvertFrom-Json
$assetFiles=@{}
Get-ChildItem -LiteralPath $assetRoot -Recurse -File | ForEach-Object {$assetFiles[[IO.Path]::GetRelativePath($assetRoot,$_.FullName).Replace('\','/')]=$_.Length}
foreach($asset in $assetInventory.files){if(-not $assetFiles.ContainsKey($asset.path) -or $assetFiles[$asset.path] -ne $asset.size){throw "缺少或大小不符的素材：$($asset.path)"}}
if($CheckOnly){
    [Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false)
    [ordered]@{status='checked-only';executable=(Join-Path $productRoot $manifest.entry);workingDirectory=$productRoot;arguments=@('--normal-product');assets=$assetRoot;web=(Join-Path $productRoot 'web');manifest=$manifestPath;hashedRuntimeAndWebFiles=@($manifest.files).Count;assetFilesCheckedForPresenceAndSize=@($assetInventory.files).Count;assetContentVerification='Pinned integrity manifest; product verifies content when loaded.';profile=$manifest.profile;port=$manifest.port;autoLogin=$false;uiStarted=$false;networkStarted=$false;servicesStarted=$false} | ConvertTo-Json -Depth 5
    return
}
# Explicit interactive launch requested by the user running this entry; no helper/service launch.
Start-Process -FilePath (Join-Path $productRoot $manifest.entry) -ArgumentList '--normal-product' -WorkingDirectory $productRoot
