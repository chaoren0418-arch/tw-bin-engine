# T446（#1031）：用 PakViewer CLI 解出規則表查核要看的客戶端檔案到 scratch/t1031/（不進 git）。
# 只用 `pak extract`（唯讀開 pak）與 `xml decrypt`（讀寫都在 scratch）。不寫任何客戶端目錄。
# 先跑 docs/reference/rea-scripts/pak/pak_index.ps1 產生 scratch/pak-index/*.list.txt。
# 用法（在 repo 根目錄）：powershell -NoProfile -File docs/reference/rea-scripts/t1031/extract.ps1
param(
    [string]$Client = 'D:\lineage開發\0-客戶端',
    [string]$Cli = 'D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe',
    [string]$Index = 'scratch\pak-index',
    [string]$Out = 'scratch\t1031\all'
)
[Console]::OutputEncoding = [Text.Encoding]::UTF8
# idx 相對路徑, 清單檔, 要解的副檔名, 輸出子目錄
$jobs = @(
    @('Text.idx', 'Text.idx.list.txt', '\.(tbl|def|txt)$', 'Text'),
    @('LiTo\LTtext.idx', 'LiTo_LTtext.idx.list.txt', '\.(tbl|def|txt)$', 'LTtext'),
    @('Tile.idx', 'Tile.idx.list.txt', '\.(xml|enx)$', 'Tile'),
    @('Sprite.idx', 'Sprite.idx.list.txt', '\.(tbl|lst|idx|ico|lmp)$', 'Sprite')
)
$rows = @()
foreach ($j in $jobs) {
    $dir = Join-Path $Out $j[3]
    New-Item -ItemType Directory -Force $dir | Out-Null
    $names = Get-Content (Join-Path $Index $j[1]) -Encoding UTF8 | Where-Object { $_ -match '^\d' } |
        ForEach-Object { ($_.Trim() -split '\s+')[1] } | Where-Object { $_ -match $j[2] } | Sort-Object -Unique
    foreach ($n in $names) {
        $dst = Join-Path $dir $n
        & $Cli pak extract (Join-Path $Client $j[0]) $n -o $dst | Out-Null
        if (-not (Test-Path $dst)) { $rows += "{0}`t{1}`tNOT_FOUND" -f $j[0], $n; continue }
        $plain = ''
        if ($n -like '*.xml' -and [IO.File]::ReadAllBytes($dst)[0] -eq 0x58) {   # 'X' 開頭＝加密 XML
            & $Cli xml decrypt $dst -o "$dst.dec.xml" | Out-Null
            if (Test-Path "$dst.dec.xml") { $plain = (Get-FileHash "$dst.dec.xml" -Algorithm SHA256).Hash.ToLower() }
        }
        $rows += "{0}`t{1}`t{2}`t{3}`t{4}" -f $j[0], $n, (Get-Item $dst).Length, (Get-FileHash $dst -Algorithm SHA256).Hash.ToLower(), $plain
    }
    "{0}: {1} 個檔名" -f $j[0], $names.Count
}
$rows | Out-File -Encoding utf8 (Join-Path $Out 'HASHES.tsv')
