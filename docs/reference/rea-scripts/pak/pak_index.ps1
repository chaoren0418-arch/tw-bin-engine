# T318（#752）：用 PakViewer CLI 列出客戶端所有 .idx／.pak 的檔案清單（唯讀）。
# 只呼叫 pakviewer-cli 的 `pak list`／`pak info`（只讀 FileAccess.Read），輸出寫到 scratch/pak-index/（不進 git）。
# 用法（在 repo 根目錄）：
#   powershell -NoProfile -File docs/reference/rea-scripts/pak/pak_index.ps1
#   powershell -NoProfile -File docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt   # 另列參考客戶端 Tile.idx／Text.idx
param(
    [string]$Client = 'D:\lineage開發\0-客戶端',
    [string]$Cli = 'D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe',
    [string]$Out = 'scratch\pak-index',
    [switch]$WithAlt
)
[Console]::OutputEncoding = [Text.Encoding]::UTF8
New-Item -ItemType Directory -Force $Out | Out-Null
$summary = @()
foreach ($i in Get-ChildItem $Client -Recurse -Filter *.idx) {
    $rel = $i.FullName.Substring($Client.Length + 1) -replace '\\', '_'
    $list = & $Cli pak list $i.FullName
    $list | Out-File -Encoding utf8 (Join-Path $Out "$rel.list.txt")
    $info = & $Cli pak info $i.FullName
    $info | Out-File -Encoding utf8 (Join-Path $Out "$rel.info.txt")
    $enc = ($info | Select-String 'Encryption') -replace '\s+', ' '
    $summary += "{0}`t{1}`t{2}`t{3}" -f $rel, $i.Length, $enc, ($list | Select-Object -Last 1)
}
# 沒有 .idx 的 .pak（PakViewer 需要 idx 才能開）
foreach ($p in Get-ChildItem $Client -Recurse -Filter *.pak) {
    if (-not (Test-Path ([IO.Path]::ChangeExtension($p.FullName, '.idx')))) {
        $summary += "{0}`t{1}`t(no idx; PakViewer cannot open)" -f $p.FullName.Substring($Client.Length + 1), $p.Length
    }
}
$summary | Out-File -Encoding utf8 (Join-Path $Out 'SUMMARY.txt')
$summary

if ($WithAlt) {
    $alts = [ordered]@{ kor25 = 'D:\381懶人包kor25(原)'; t89 = 'D:\天堂參考用客戶單\89天堂'; ly = 'D:\天堂參考用客戶單\雷雨天堂' }
    $altOut = Join-Path $Out 'alt'
    New-Item -ItemType Directory -Force $altOut | Out-Null
    foreach ($k in $alts.Keys) {
        $names = @('Tile.idx', 'Text.idx') + (Get-ChildItem $alts[$k] -Filter 'Sprite*.idx' | ForEach-Object Name)
        foreach ($n in $names) {
            $p = Join-Path $alts[$k] $n
            if (Test-Path $p) { & $Cli pak list $p | Out-File -Encoding utf8 (Join-Path $altOut "$k.$n.list.txt") }
        }
    }
}
