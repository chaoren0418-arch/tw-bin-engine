# T318（#752）：用 PakViewer CLI 解出 pak-resources.md 用到的檔案到 scratch/pak-extract/（不進 git），並算 SHA-256。
# 只用 `pak extract`（讀 pak）與 `xml decrypt`（讀 scratch 內的檔、寫 scratch）。不寫任何客戶端目錄。
# 用法（在 repo 根目錄）：powershell -NoProfile -File docs/reference/rea-scripts/pak/pak_extract.ps1
param(
    [string]$Cli = 'D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe',
    [string]$Out = 'scratch\pak-extract'
)
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$main = 'D:\lineage開發\0-客戶端'
$kor25 = 'D:\381懶人包kor25(原)'
$ly = 'D:\天堂參考用客戶單\雷雨天堂'
# 來源代號, 客戶端根目錄, idx 相對路徑, 檔名
$items = @(
    @('main', $main, 'Text.idx', 'string-c.tbl'),
    @('main', $main, 'Text.idx', 'string-c.tbl.bak'),
    @('main', $main, 'Text.idx', 'recipe.tbl'),
    @('main', $main, 'Text.idx', 'monlist-c.html'),
    @('main', $main, 'Text.idx', 'summonlist-c.html'),
    @('main', $main, 'Text.idx', 'helpmail-c.html'),
    @('main', $main, 'Text.idx', 'helpfont-c.html'),
    @('main', $main, 'Text.idx', 'option1-c.html'),
    @('main', $main, 'Text.idx', 'optiong-c.html'),
    @('main', $main, 'Text.idx', 'agit-c.html'),
    @('main', $main, 'Text.idx', 'auction1-c.html'),
    @('main', $main, 'Tile.idx', 'summonList.xml'),
    @('main', $main, 'Tile.idx', 'MailExcludeUI.xml'),
    @('main', $main, 'Tile.idx', 'ReNStatusWinUI-c.xml'),
    @('main', $main, 'LiTo\LTtext.idx', 'LTstring-c.tbl'),
    @('main', $main, 'LiTo\LTtext.idx', 'LTmonlist-c.html'),
    @('kor25', $kor25, 'Text.idx', 'string-c.tbl'),
    @('ly', $ly, 'Text.idx', 'string-c.tbl'),
    @('kor25', $kor25, 'Sprite00.idx', '9183.img'),
    @('kor25', $kor25, 'Sprite01.idx', '9184.img'),
    @('ly', $ly, 'Sprite00.idx', '9183.img'),
    @('ly', $ly, 'Sprite01.idx', '9184.img')
)
foreach ($n in 'LoginUI.xml', 'SelectUI.xml', 'CreateUI.xml', 'ServerName.xml', 'LoadingScene.xml', 'RenewalMailUI.xml',
               'ResetStatUI.xml', 'StatInfoUI.xml', 'ReNStatusUIEx.xml', 'AgitAuctionUI.xml', 'FishingUI.xml',
               'OptionUI.xml', 'GameRankingUI.xml', 'SpellList.xml') {
    $items += , @('kor25', $kor25, 'Tile.idx', $n)
}
$rows = @()
foreach ($it in $items) {
    $dir = Join-Path $Out $it[0]
    New-Item -ItemType Directory -Force $dir | Out-Null
    $dst = Join-Path $dir (($it[2] -replace '[\\.]', '_') + '__' + $it[3])
    & $Cli pak extract (Join-Path $it[1] $it[2]) $it[3] -o $dst | Out-Null
    if (-not (Test-Path $dst)) { $rows += "{0}`t{1}`t{2}`tNOT_FOUND" -f $it[0], $it[2], $it[3]; continue }
    $raw = (Get-FileHash $dst -Algorithm SHA256).Hash.ToLower()
    $plainHash = ''
    if ($it[3] -like '*.xml' -and [IO.File]::ReadAllBytes($dst)[0] -eq 0x58) {   # 'X' 開頭＝加密 XML
        $plain = "$dst.dec.xml"
        & $Cli xml decrypt $dst -o $plain | Out-Null
        if (Test-Path $plain) { $plainHash = (Get-FileHash $plain -Algorithm SHA256).Hash.ToLower() }
    }
    $rows += "{0}`t{1}`t{2}`t{3}`t{4}`t{5}" -f $it[0], $it[2], $it[3], (Get-Item $dst).Length, $raw, $plainHash
}
$rows | Out-File -Encoding utf8 (Join-Path $Out 'HASHES.tsv')
$rows
