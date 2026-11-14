# Issue #1195 — f1188-map-seg：只有 .seg 的地圖區塊改依 manifest SHA 載入

- 階段：code（claude-local，第 1 次嘗試）
- 分支：`task/1195`
- 來源：#1188 M01 ⚠️（`docs/reports/issue-1188.md`「額外觀察」）、分流 `docs/reports/issue-1192.md`

## 結論

- **已驗證（靜態）**：原 BIN 的 SEG 載入分支沒有地圖或檔名檢查，所有 `.seg` 用同一個版面；客戶端 46 個只有 `.seg` 的區塊（map 3：1、map 4：43、map 10：2）**全部**能用這個版面嚴格讀到檔尾，用到的 tile id 都在 `Tile.pak` 和 r29 `tiles[]`（SHA 相符），沒有一塊有 effects 尾段。沒有觸發停止條件。
- **已改**：產品（`OriginalMapStore`、`PortalCatalog`）不再只接受 map 6 錨點這一個 SEG，改成依 manifest 的 SHA-256 讀任何 `classic-seg` 區塊；r29 舊 manifest 裡帶 `sources[].sha256` 的 `.seg` `unsupportedBlocks` 列，從 `TW_CLIENT_ROOT/map/<id>/<name>.seg` 讀並核 SHA。準備器（`MapScan`／`MapsRecipe` v3）把 `.seg`-only 區塊列進 `blocks[]`（`format: "classic-seg"`）。
- **待驗證（交 f1188-retest）**：資源法師進世界後，detail 不再出現「SEG／壓縮格式」，`MapResourceState` 不是 `unavailable`，GDI 畫面看得到地圖。本機不得執行編譯產物，CI 結果見 PR。

## 1. 原 BIN 的 SEG 載入路徑（已驗證，靜態）

來源檔：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，ImageBase `0x400000`（`.text` VA `0x401000`／FO `0x400`；`.rdata` VA `0x114B000`／FO `0xD49C00`）。反編譯參考：`P24-原2x2地圖快取與跨區載入/reference/cache/decompiled/0xf0b570.txt`（只供參考）；以下位址用 pefile＋Capstone 重新對過（`scratch/i1195/bin_seg.py` → `scratch/i1195/bin-seg.txt`）。

| 項目 | VA | RVA | File Offset | 說明 |
|---|---|---|---|---|
| `sub_F0B570`（區塊載入） | `0xF0B570` | `0xB0B570` | `0xB0A970` | r29 manifest 錨點的 `nativeVa` |
| 字串 `map\%d\%04x%04x.seg` | `0x1180E24` | `0xD80E24` | `0xD7FA24` | |
| 字串 `map\%d\%04x%04x.s32` | `0x1180E38` | `0xD80E38` | `0xD7FA38` | |
| SEG ground `movzx eax, word ptr [edx]` | `0xF0CADA` | `0xB0CADA` | `0xB0BEDA` | ground 8192 × **u16** 直接存成 tile 值（`tile=v>>8`、`index=v&255`），沒有 S32 的 `-1` 特例 |
| SEG cover 筆數 `movzx ecx, word ptr [eax]` | `0xF0CB0D` | `0xB0CB0D` | `0xB0BF0D` | u16，之後跳 `4 × n` |
| SEG attribute `movzx cx, byte ptr [eax]` | `0xF0CBC3` | `0xB0CBC3` | `0xB0BFC3` | 8192 × **u8** |
| SEG group 成員 205／205 判斷 | `0xF0CDBD`、`0xF0CDD9` | `0xB0CDBD`、`0xB0CDD9` | `0xB0C1BD`、`0xB0C1D9` | 佔位跳 3 bytes；否則 `z:u8`、`v:u16`（`0xF0CE58`） |
| SEG refs 筆數／內容 `movzx … byte ptr` | `0xF0D029`、`0xF0D06B` | `0xB0D029`、`0xB0D06B` | `0xB0C429`、`0xB0C46B` | **u8** 筆數、每筆 u8，逐筆交給 `sub_87EE20` |
| SEG portal 筆數 `movzx edx, word ptr [ecx]` | `0xF0D10C` | `0xB0D10C` | `0xB0C50C` | 只有 `ptr < end` 時才讀；每筆 `attr |= 2` |
| SEG effects 字 `movzx edx, word ptr [ebp-0x378]`／`cmp edx, 0x8000` | `0xF0D71E`／`0xF0D725` | `0xB0D71E`／`0xB0D725` | `0xB0CB1E`／`0xB0CB25` | 只有 `ptr < end` 時才讀；bit15 = 每筆多 4 bytes；之後每筆 `id,x,y[,u32]`（`lea edx,[ecx+ecx-0x8000]` 在 `0xF0D91B`） |

讀碼結論：

- **檔案優先順序**（已驗證）：先 `<s32>.gz`、`<s32>`，兩者都讀不到才 `<seg>.gz`、`<seg>`；都沒有就畫 tile 140 的 187／188（= 產品的 verified-absent）。產品維持「有 `.s32` 就用 `.s32`」，與原版一致。
- **SEG 分支只看檔案存在**（已驗證）：開檔之前只有 `sprintf`，沒有地圖 ID、區塊名或內容的白名單，所以版面對所有 `.seg` 都相同，不是只對錨點成立。
- **與 S32 的差異**（已驗證）：ground u16、cover 4 bytes、attribute u8、group id 讀了但不存、成員 5 bytes、refs `u8` 筆數＋`u8`，portal／effects 只在還有資料時讀。triggers、portal 記錄與 effects 尾段的格式與 S32 相同。
- **effects 尾段**（已驗證格式；語意不同）：格式同 S32，但 SEG 分支的 id → 光源半徑表只有 `1884/1885→4`、`1892–1897→7`、`1992/1993→11`、`1886→14`、`1901–1987→5`，與產品 `MapEffectLightTable`（S32 表）不同。客戶端沒有任何 `.seg`-only 區塊有這一段，所以**維持拒絕**，但錯誤訊息改帶區塊名與值。
- **SEG cover 的另一個差異**（已驗證，未實作，不影響繪製）：SEG 分支在 cover 不是 type 2 時，除了覆蓋 ground tile，還把該格 attribute 清成 0（`*(_WORD*)(v128+4)=0`，S32 分支不清）。產品的 `OriginalMapBlock.Attributes` 只在載入時記錄、繪製與移動都不讀它（地形走 `maps[].terrain`），所以這次不改；列為已知差異。

## 2. 46 塊逐塊嚴格讀取（已驗證）

腳本：`scratch/i1195/seg_scan.py`（只讀 `D:\lineage開發\0-客戶端\map`、`Tile.idx`／`Tile.pak` 與 `releases/r29/assets/maps`），輸出 `scratch/i1195/seg-scan.json`、表格 `scratch/i1195/table.md`。規則與 `OriginalMapStore.Load` 的 SEG 分支相同，另外：

- 每塊必須剛好讀到檔尾；cover、portal 座標必須 `x<128、y<64`；trigger 的 group 序號必須小於 group 數。
- 每個用到的 tile id 都要在 `Tile.idx`；index 小於該 TIL 的筆數；每個 (tile, index) 用產品同樣的 TIL 解碼規則（type、run、尾端對齊）跑一次。
- tile id 也要在 r29 `maps/manifest.json` 的 `tiles[]`，而且 `Tile.pak` 的內容與 `releases/r29/assets/maps/tiles/<id>.til` 的 SHA 都等於 manifest 的值（`--assets engine/assets` 的封裝模式就是讀這裡）。
- 46 塊的 SHA 都等於 r29 `unsupportedBlocks[].sources[].sha256`。
- **額外一致性**：47 塊（含錨點）的 refs 清單都**剛好等於**該塊用到的 tile id 集合（原 BIN 把 refs 逐筆交給 `sub_87EE20` 預載 tile），表示 u8 refs 的解讀正確。

結果：46／46 嚴格讀完、tile 全部可解；portal 段都存在但 0 筆；全部沒有 effects 尾段（`noEffectTail`）。

| # | map | 區塊 | 大小 (bytes) | SHA-256 | tile id 數 | cover | groups | triggers | refs | portal 段 | effects | 讀取結果 | r29 SHA 相符 |
|---:|---:|---|---:|---|---:|---:|---:|---:|---:|---|---|---|---|
| 1 | 3 | `80008002` | 47,834 | `81cb0fa8419df099f203c2af379433d2078cdf74a62475e66b2b49e5e9adc58a` | 4 | 0 | 99 | 751 | 4 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 2 | 4 | `7ffc7ffc` | 24,688 | `00683d3e0126dbd377da4960897e7c447ba8151535f007f7a49480a3bc962e3b` | 18 | 0 | 4 | 0 | 18 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 3 | 4 | `7ffc8008` | 43,233 | `e1012016dc6f29b5a834637820e399fdad3e409e5a4b3c3767b90ff666ee7489` | 46 | 5 | 182 | 68 | 46 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 4 | 4 | `7ffc8009` | 31,322 | `ad9d7cca49036037062bc92b4a97019a3faf3f58bcc9fd6d5708438dbf27c8e0` | 34 | 0 | 76 | 1 | 34 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 5 | 4 | `7ffd7ff8` | 128,422 | `a792a9345d2b64f8dbcc58e05a7052e9b761187386dac5b2bf1d10fc3a7c1d0d` | 28 | 0 | 105 | 4303 | 28 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 6 | 4 | `7ffd7ff9` | 123,628 | `adbda03e09892a2d2fbb53b6a64eaffb554de7515efbba695693c65844cfa942` | 27 | 0 | 108 | 4032 | 27 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 7 | 4 | `7ffd7ffa` | 126,518 | `cc470fca31759c1d6a2389c2d54ac3336231b28c4f947eb017bfbd0a7f3e867c` | 28 | 0 | 114 | 4147 | 28 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 8 | 4 | `7ffd7ffb` | 114,708 | `6d84746920daf894875fbfd699c3895464b3fa76f07436b02eb5c05f2c9ea39a` | 29 | 0 | 105 | 3688 | 29 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 9 | 4 | `7ffd7ffc` | 49,010 | `4746b2ca976a41ee5caffe455520c1923b3f972ddaad58430e613e0138b5a247` | 27 | 299 | 57 | 887 | 27 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 10 | 4 | `7ffd8004` | 72,272 | `4941f13691430b6f698b3bceea7beb5d6935d7c0f1cc916dd0816df065fa1f8a` | 25 | 1221 | 221 | 1533 | 25 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 11 | 4 | `7ffe7ff8` | 126,198 | `486bdd2a1b8fd5d34e194dd02262c669b693857300afe38db5d9d474aa60ec42` | 43 | 103 | 161 | 4185 | 43 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 12 | 4 | `7ffe7ff9` | 134,335 | `ca5fcdfaf37b71a314695c44e08f50ee19f5336d19d9cd46c6412f747d2d7b83` | 36 | 0 | 175 | 4469 | 36 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 13 | 4 | `7ffe7ffa` | 125,513 | `b261b3c8394421f57ac2161e59298474dac793204dd8143769fad0bbfc65e360` | 32 | 0 | 133 | 4091 | 32 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 14 | 4 | `7ffe7ffc` | 51,271 | `7b010a2816a17314c3ac7e2668c333619bccaf97372e3da87e0182bb65cb328a` | 32 | 363 | 77 | 918 | 32 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 15 | 4 | `7fff7ff8` | 137,128 | `0b85763b2233493eee9cac7a4b61029f4823bb860a1325ac7862f1648beff3aa` | 47 | 19 | 344 | 4417 | 47 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 16 | 4 | `7fff7ff9` | 196,017 | `8f315c0690d457c6483502b85aee8069f3585d74722d28cbfcaf67f4e405e533` | 18 | 0 | 725 | 7500 | 18 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 17 | 4 | `7fff7ffc` | 49,924 | `f231b1215591a6a5fb172cea7d238d89faf0af03a5c53af0b6c39f95777c0f92` | 29 | 298 | 51 | 939 | 29 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 18 | 4 | `7fff7ffd` | 56,440 | `a2406075c9bc1498b945fc69f4ff1e64a5c5126f084b8f8031e573f59b83791e` | 35 | 775 | 129 | 877 | 35 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 19 | 4 | `7fff7ffe` | 36,622 | `43169d34d03cbcc0d22c643c4e14c8d508d7a6baef5720988d2d877b5bc81875` | 24 | 409 | 47 | 267 | 24 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 20 | 4 | `80007ffc` | 62,521 | `fe1273326d8e95251cd054681cdac1a041ac3e4be58039878724b3b346c6112c` | 29 | 412 | 95 | 1385 | 29 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 21 | 4 | `80007ffe` | 34,644 | `4e4ab7914f472efe0d099d8d80e62234860c34a45d154185ecdadef0d80a4739` | 18 | 506 | 47 | 184 | 18 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 22 | 4 | `80008000` | 107,961 | `8dbfff4c4a381c7837d597c41381be2a93cb9229e8b3848cfcfe2d3bb5c86b28` | 26 | 1245 | 194 | 3003 | 26 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 23 | 4 | `80008001` | 86,968 | `8d785fa1025740354f3f0f51d8f7708c398b8a66a5fdbbbce7dcfb0ea7f640d4` | 38 | 1155 | 319 | 1948 | 38 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 24 | 4 | `80017ffe` | 46,433 | `62c2e22f704789c97436d317fe90b0fa83e8fecb92a52e22186cfaa56035e69a` | 16 | 649 | 68 | 476 | 16 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 25 | 4 | `80027ffc` | 67,717 | `35a09f06156e740c781467a18fded93b0377dd63e1b3a75d0530b5a6c77de411` | 27 | 349 | 65 | 1642 | 27 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 26 | 4 | `80027ffd` | 117,015 | `cdec4a9ed3b38b39c385f5eb27e68cdd9fb01924151170b8072c47dc84e5a85c` | 29 | 1163 | 215 | 3334 | 29 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 27 | 4 | `80027ffe` | 94,476 | `efcb1b1667f33381d56d05ee982af2d9ae25079a5334835939afe83049bfdd71` | 31 | 975 | 119 | 2509 | 31 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 28 | 4 | `80028000` | 97,648 | `461450eb7584416ebc628516158ac5a98081d616dabf526417ec3588014d119c` | 22 | 1301 | 392 | 2387 | 22 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 29 | 4 | `80028001` | 80,389 | `c2c748babb19d2d3f6a29688a756aa6a34c5b647afc95adff80217abb4e1d668` | 32 | 1166 | 276 | 1733 | 32 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 30 | 4 | `80028002` | 64,022 | `3a8dfcd6f339917d4314241d5b9f602fb755579fc49744b4cbe01a3db0828202` | 33 | 853 | 162 | 1183 | 33 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 31 | 4 | `80028003` | 101,433 | `4a6386c48b66d988ceb56c902804a48118881031081cb524f8f12e2d1e4a8f9e` | 22 | 1222 | 336 | 2624 | 22 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 32 | 4 | `80037ffd` | 56,370 | `744c9cb259005dcf6b92ca2e8a0885f92bce5a0226798a09b76b75fbe2dc05dd` | 27 | 458 | 93 | 1130 | 27 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 33 | 4 | `80037ffe` | 49,886 | `cc550077f2a5f9af1d881574cccef2a0946381642d389c4d73ad8dcca4d35ec0` | 30 | 287 | 41 | 951 | 30 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 34 | 4 | `80037fff` | 77,133 | `0e386826ebcdc5633c855a660c4ddc8fb624264cce8682e4dfff8256fa2b11b6` | 31 | 728 | 169 | 1836 | 31 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 35 | 4 | `80038000` | 75,404 | `109cd286c548e5cc545abd221d920e8dfdecf7ddfc1fc7245b7af0e8b20425ba` | 35 | 758 | 142 | 1779 | 35 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 36 | 4 | `80038001` | 48,162 | `2017d67ddb446334394f1599a74019a639bfeb1ab245d6800f93ebb81d19a788` | 32 | 501 | 123 | 722 | 32 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 37 | 4 | `80038002` | 55,366 | `a6f1e04dbbe83b0c55d1fbe32874e2f9bc65676152a0259db0093fc936301c57` | 29 | 562 | 155 | 988 | 29 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 38 | 4 | `80038003` | 49,901 | `a17a2476e37dc0e6a0c16ded2f753e79d9adab21facd94707990073b5cb57d77` | 31 | 448 | 136 | 812 | 31 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 39 | 4 | `80048001` | 90,398 | `e2dd9928bfcdf46874de18e875543c7378e66170594a1828937a12daf360ba12` | 32 | 1066 | 287 | 2262 | 32 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 40 | 4 | `80048002` | 96,623 | `c332d211320a2066b3623deaa56db037be9bb8d969ec91992454c58852409907` | 27 | 0 | 78 | 2930 | 27 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 41 | 4 | `80048003` | 96,798 | `7727acf1dfeecbdfa09561a6edc7ce197b0360fb679de37de638c29cd1650436` | 38 | 0 | 79 | 2924 | 38 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 42 | 4 | `80058002` | 111,104 | `c20e8d0d8fd5ff8612b638bd65ba23e2572616b5cf3dae25cb899c028c15a3b5` | 30 | 367 | 108 | 3558 | 30 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 43 | 4 | `80058003` | 109,322 | `b3052b69b4426ff1c4e87d9744575ed390925c90bc0e755b0c78fb39e12760bb` | 24 | 0 | 76 | 3564 | 24 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 44 | 4 | `80058004` | 58,255 | `2b247c17e5e997118351cce7484e408b427d9692be2c85ed99d37c6bc1a721dd` | 33 | 0 | 42 | 1332 | 33 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 45 | 10 | `80007fff` | 84,645 | `06c296dc0b0d03e436933f579fe772fa0d0b3c261faccb99cb2d85ce79ef4085` | 7 | 382 | 249 | 1949 | 7 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |
| 46 | 10 | `80008000` | 69,677 | `1ff7c377905eb48653ec4320e505fa03cd673ca1e1831cdd7669cdba6774872b` | 9 | 520 | 191 | 1411 | 9 | 有（0 筆） | 無尾段 | 嚴格讀完、tile 全部可解 | 是 |

錨點對照：map 6 `7fff8000`（37,949 bytes，`aad5ff598f9daeb6574fcef8fdaa1b3b2963d6c0d60d8840057ef48d39101a45`）用同一支腳本也嚴格讀完：tile id 20 個、portal 8 筆、無 effects 尾段。

#1188 的 `80048001`：上表第 39 列，SHA 與 Issue 記錄的 `E2DD9928…DAF360BA12` 相同。

effects 尾段非零的區塊：**無**（46 塊都在 portal 段之後結束）。

## 3. retest 實際走的 manifest 與程式路徑

客戶端以 `--assets engine/assets` 啟動；`engine/assets` 是指向 `releases/r29/assets` 的唯讀 junction，`integrity.json` schema 是 `p06-assets-integrity-v1` → `MapRawSource` 為 **Packaged** 模式。**retest 用的就是 r29 的 `maps/manifest.json`（不改、不重產）**，路徑如下：

1. `SceneOpacity.cs:117` 或 `WorldEntry.cs` 的非同步載入 → `new OriginalMapCache(MapAssetsRoot, x, y, map, …, MapClientRoot)`；`MapClientRoot` 目前從未指派（null）。
2. `OriginalMapStore` 建構子（`OriginalMapCache.cs:23`）：讀 map 4 的 `unsupportedBlocks`；每列交給 `SegBlocks.LegacySha256`（`OriginalMapCache.cs` 新增的 `SegBlocks`）。r29 的 46 列都恰有一個 source、檔名是 `<name>.seg`、在 `map\<mapId>\` 下 → 放進 `legacySeg`（名稱 → SHA），**不再**放進 `unsupportedBlocks`。
3. `OriginalMapStore.Load`（`OriginalMapCache.cs:42–45`）：`blocks[]` 沒有這個名稱、`legacySeg` 有 → `SegBlocks.ReadLegacy(clientRoot, mapId, name, sha)`：根目錄 = `MapClientRoot` → null 時用 `UiResourcePins.ClientRoot`（環境變數 `TW_CLIENT_ROOT`，未設時 `D:\lineage開發\0-客戶端`），讀 `map\4\80048001.seg`（檔名不分大小寫）並核 r29 manifest 記的 SHA；不符就 `InvalidDataException`（不會靜默讀到別的內容）。
4. 依 SEG 版面解析；tile 從 `engine/assets/maps/tiles/<id>.til` 讀並核 r29 `tiles[]` 的 SHA（第 2 節已確認 46 塊用到的 id 全部在）。
5. 入口：`PortalCatalog`（`Portals.cs`）同樣從 r29 manifest 的 `unsupportedBlocks` 建 `legacySeg`，`ReadBlock` 走 `SegBlocks.ReadLegacy`。46 塊都是 0 個 portal。

新的準備器路徑（integrity v2、`MapsRecipe` v3 產的 manifest）則是 `blocks[]` 的 `classic-seg` 列，經 `MapRawSource.Block` 從 `TW_CLIENT_ROOT` 讀；兩條路徑解析結果相同（離線檢查 (c) 比對）。

**沒有**修改 `releases/`、`engine/assets` 目標、客戶端檔案，也沒有把 `.seg` 複製進專案。

## 4. 程式修改（修改前 → 修改後行號）

| 檔案 | 修改前（origin/main） | 修改後 | 內容 |
|---|---|---|---|
| `engine/source/OriginalMapCache.cs` | 15–16 | 15–19 | 新欄位 `legacySeg`、`clientRoot` |
| 同上 | 21（`unsupportedBlocks` 全收） | 23 | 帶 `.seg` sources 的列改進 `legacySeg` |
| 同上 | 37–39（`MapId!=6‖name!="7fff8000"‖SHA≠aad5…` → `Unverified SEG profile`） | 40–45 | 先 `blocks[]`，再 `legacySeg`；SEG 只要求 manifest SHA，錨點 pin 改由 `SegBlocks.CheckAnchor` 檢查 |
| 同上 | 48（portal 段必讀） | 54–55 | SEG 在 refs 後已到檔尾時沒有 portal 段 |
| 同上 | 49–50（`SEG effects need separate native profile`） | 56–58 | SEG 到檔尾時 effects = 0；非 0 拒絕訊息帶 `地圖/區塊` 與 `0x值` |
| 同上 | 63 | 71 | 區塊 `Sha256` 用實際核過的 SHA |
| 同上 | — | 79–116 | 新增 `SegBlocks`（`LegacySha256`、`ReadLegacy`、`CheckAnchor`），註解附原 BIN 位址 |
| `engine/source/Portals.cs` | 22–33 | 22–40 | 建構時讀 legacy `.seg` 列（`maps` 缺欄位時略過） |
| 同上 | 50–55（寫死錨點） | 55–64 | 同 `OriginalMapStore`：manifest SHA＋錨點 pin＋legacy 讀檔 |
| 同上 | 65–66 | 74–76 | SEG 沒有 portal 段時回傳空集合 |
| `engine/source/AssetPrep/MapScan.cs` | 12、74 | 12–17、77 | `.seg`-only 一律進 `Blocks`（`SegOnly` 永遠空） |
| 同上 | 112 | 115 | `Parse` 對 SEG：refs 後到檔尾 → 0 個 portal、無尾段 |
| `engine/source/AssetPrep/MapsRecipe.cs` | 32（Version 2） | 34（Version **3**） | 註解說明 `unsupportedBlocks` 只剩 `.gz` 名稱 |
| `engine/source/AssetPrep/TerrainRecipe.cs` | 132（任何非錨點 SEG → ContainerMismatch） | 132–133 | 只對 map 6 `7fff8000` 檢查 pin |
| 同上 | 187（Version 1） | 188（Version **2**） | `.seg`-only 改完整走訪（讀不了的 `.seg` 讓整張圖不支援，同 `.s32`） |

`TerrainRecipe` 的輸出值不變（`.seg`-only 的 attribute 層原本就有用）；版本加一是因為「讀不了的 `.seg`」的處理從「只看 attribute 層」變成「整塊要讀得完」。

## 5. 離線檢查（`[CheckGroup]` 自動發現，沒改 `Program.cs`、`DemoChecks.csproj` 與註冊表）

- 新檔 `engine/_integration/dev/map-provider-tests/SegBlockChecks.cs`：群組 `map-provider`（Order 3）、`map-continuity`（Order 302）、預設群組。全部用合成 SEG（`MpSeg`，不含任何真實 `.seg`），寫在 `scratch/issue-1195/run-<時間>/`。
  - (a) integrity v2＋`blocks[]` classic-seg：非錨點 SEG 依 manifest SHA 載入（ground u16、cover、205 佔位、u8 attribute、portal `|=2`、TIL 解碼）；refs 後就結束的 SEG 也能載入；SEG effects 尾段仍拒絕且訊息帶 `4/7e027e01`、`0x0001`。
  - (b) 客戶端 `.seg` 被改動 → `Original cache source hash` 拒絕。
  - (c) integrity v1＋r29 式 `unsupportedBlocks`＋`sources[].sha256`：從客戶端根目錄讀，結果與 (a) 逐欄相同；SHA 不符、沒有 sources、source 是 `.s32.gz`、source 在別張地圖目錄、客戶端根目錄不存在、`.seg` 缺檔，各自明確拒絕。
  - (d) `MapScan.Plan` 把 `.seg`-only 列進 `Blocks`（同名 `.s32` 優先）；`maps` recipe 產生 `format: "classic-seg"`、`sha256`、`path`、`noEffectTail` 的區塊列，`unsupportedBlocks` 只剩 `.s32.gz`。
  - (e) map 6 `7fff8000` 的 SHA 不是 pin → `Unverified SEG profile`（舊訊息不變）；其他地圖／名稱不受 pin 限制。
  - (f) 同一棵樹的 S32 區塊載入結果不變；`.s32.gz` 區塊仍是「SEG／壓縮格式」拒絕。
  - 入口：兩種樹的 `PortalCatalog.ReadBlock` 都從 SEG 讀到 portal 格；沒有 portal 段 → 空；改過的 `.seg` → `portal source hash mismatch`。
- 既有檔調整：`MapProviderChecks.cs`（fixture 的 `map/4/7e027e00.seg` 改成完整合成 SEG；預期改為 `blocks[]` 多一列 classic-seg、`unsupportedBlocks` 只剩 `7e007e01`、recipe 版本 3）、`TerrainRecipeChecks.cs`（`.seg`-only fixture 改成完整 SEG `TrSegFull`，terrain 期望值不變）。
- 本機只做了編譯：`dotnet build engine/source/OfflineClient.csproj`（0 錯誤）與 `dotnet build …/DemoChecks.csproj`（0 錯誤）。**檢查本身沒有在本機執行**（規定不得執行編譯產物），以 PR 的 CI（`map-continuity` 步驟會跑 `map-provider` 這幾組）為準。

## 已驗證／推測／待驗證

**已驗證**

- 原 BIN SEG 分支版面與檔案優先順序（第 1 節，位址以 pefile＋Capstone 對過）。
- 46 塊逐塊嚴格讀完、tile 全部可解、SHA 與 r29 manifest 相同、全部沒有 effects 尾段（第 2 節）。
- 產品與 DemoChecks 可編譯。

**推測**

- 主大陸 map 4 的 43 塊載入後，畫面應與原版一致；但 SEG cover 會把 attribute 清 0 的差異（第 1 節）若日後有程式改讀 `OriginalMapBlock.Attributes`，需要補上。
- SEG effects 尾段若要支援，要另做 SEG 版光源表（客戶端目前沒有這種檔，所以沒做）。

**待驗證（交 `f1188-retest`）**

- 資源法師（p24svc01）進世界後：detail 不再出現「SEG／壓縮格式」；`MapResourceState` 不是 `unavailable`；GDI 畫面看得到地圖（主大陸中心一帶）。
- 在 map 4 這 43 塊之間移動（2×2 快取換區）沒有新的錯誤。
- CI 綠燈（含 `map-continuity`、`map-provider` 與新增的 SEG 檢查）。

## 產出路徑

- 程式：`engine/source/OriginalMapCache.cs`、`engine/source/Portals.cs`、`engine/source/AssetPrep/{MapScan,MapsRecipe,TerrainRecipe}.cs`
- 測試：`engine/_integration/dev/map-provider-tests/{SegBlockChecks,MapProviderChecks,TerrainRecipeChecks}.cs`
- 文件：`docs/reference/asset-recipes/maps.md`（新增「SEG 區段順序」、更新 `blocks[]`／`unsupportedBlocks[]`／`tiles[]`／產品讀取點列）、本報告
- 本機暫存（不入 git）：`scratch/i1195/seg_scan.py`、`seg-scan.json`、`table.md`、`bin_seg.py`、`bin-seg.txt`

## 給下一棒的提示

- retest 不需要重產 assets：`--assets engine/assets`（r29）就會走 legacy 路徑，只要 `TW_CLIENT_ROOT`（或預設 `D:\lineage開發\0-客戶端`）的 `map\4\*.seg` 與 r29 記錄的 SHA 相同（2026-11-14 已核對 46／46 相同）。
- 若 retest 仍看到「SEG／壓縮格式」，先確認跑的是含本 PR 的 dev 建置（不是 r33 正式入口）。若看到 `Original cache source hash: TW_CLIENT_ROOT/map/4/…seg`，表示客戶端根目錄不是預期的那一份。
- `docs/reference/asset-recipes/terrain.md` 的「選檔」段仍寫 `.seg`-only 只讀 attribute 層；行為上 attribute 值不變，但「讀不了的 `.seg` 讓整張圖不支援」這點沒有同步到該文件（本張範圍外），可在下一次 terrain 文件更新時補。
