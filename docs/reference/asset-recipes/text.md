# 素材 recipe：Text 容器與文字類素材（T444／#1029）

日期：2026-11-13。來源閉合查核，唯讀。對象是使用者原客戶端 `D:\lineage開發\0-客戶端` 的 `Text.idx`／`Text.pak`，比對基線是主目錄 `engine/assets`（junction → `releases/r29/assets`）的 `objects/names.json` 與 `dialogs/templates.json`。

本文只記錄檔名、record、File Offset、大小、SHA-256、筆數與 ID，沒有任何解出的文字或表。解析腳本只放在 `scratch/t444/`，不進 git。

## 結論

**7 項全部 pass，沒有 blocked。** 只用原客戶端的 `Text.idx`／`Text.pak` 加上標準演算法（DES、AES、code page 950），就能重建 `names.json`、`templates.json`，並讀到 `list.spr`／`list.spz`。不需要 PakTools 的 `Map1.bin`–`Map5.bin`、研究 DLL、SQL 或 JAR。

有一點要注意：本機 Text 與基線當時用的 Text 是不同版本，`templates.json` 有 177 筆內容差異（176 筆改值、1 筆新增）。這是來源版本不同，演算法沒有問題，也沒有缺資源。細節見第 5 項。

| # | 項目 | 判定 | 一句話 |
|---|---|---|---|
| 1 | Text.idx／pak 容器格式 | pass | 產品 `TextArchive.cs` 的格式可解出全部 37,498 筆，與 PakViewer 清單逐筆相同 |
| 2 | 與歷史 pin 的版本差異 | pass（已查清） | 多 1 筆、3 筆大小不同、176 個 html 內容不同；舊 File Offset 不能沿用 |
| 3 | `list.spr`、`list.spz` | pass | 解碼後 SHA 與歷史紀錄相同；`list.spz` 內層是 AES-128-CBC |
| 4 | `desc-c.tbl` → `names.json` | pass | 重建結果與基線逐 byte 相同 |
| 5 | `*-c.html`／`htm` → `templates.json` | pass（規則）；內容有 177 筆版本差異 | 鍵集合＝基線＋1；8,720 筆相同，176 筆只差兩個圖片編號 |
| 6 | PakTools `Map1`–`Map5.bin` | pass | 就是標準 DES 的五張表，金鑰與 `TextArchive` 相同 |
| 7 | html 與 spz 的內層編碼 | pass | html 沒有內層編碼（直接是 Big5）；spz 有一層 AES，產品已實作 |

## 1. 容器格式（pass）

本機檔案：

| 檔案 | 大小 | SHA-256 |
|---|---:|---|
| `Text.idx` | 1,049,948 | `43a342ad3929b541096630165c04794192c48359ecbf116b538eaffc49741f6f` |
| `Text.pak` | 26,267,704 | `9516a372c43b269cf1af7adc0ac00abd4163e1235dcd846e34294849616a37ff` |

兩個 SHA 與設計文件附錄 B 相同，與附錄 C 的歷史 pin 不同。

格式照 `engine/source/TextArchive.cs`：

- idx：`u32 count`，後面 `count` 列，每列 28 bytes（`u32 offset`、20 bytes 以 NUL 補滿的 ASCII 檔名、`u32 size`）。列的部分用 DES-ECB 解碼，`count` 不加密。
- pak：每筆 payload 各自用 DES-ECB 解碼，尾端不足 8 bytes 的部分是明文。
- 金鑰 `7E214023255E243C`（格式常數）。

查核結果：

- 解出 37,498 筆，解碼後索引（不含 count）SHA-256 `182198089053f54852e4a66347b45d91c02a10359ee99ac2e714947dcf4a211b`。
- 檔名全部是可列印 ASCII，照不分大小寫的 ASCII 順序排列。
- 各筆的範圍剛好鋪滿整個 pak（0 到 26,267,704），沒有重疊也沒有空隙。
- 有 110 個檔名重複出現（不分大小寫），所以找單一檔案要用 `TextArchive.Single()`，模板要照第 5 項的重複規則處理。
- 有 3 個檔名含空白（record 2333、37096、37151，都是 `…-c .html`），不符合 `-c.html` 結尾，不算模板。
- 用 PakViewer CLI `pak list` 列同一個 idx，37,498 筆的 record、檔名、offset、大小與 Python 解出的逐筆相同。

## 2. 與歷史 pin 的版本差異（pass，已查清）

歷史索引取自研究目錄的 `text-index-decoded.bin`（37,497 筆，SHA-256 `c08d6b3a…05957410`，與 `text-provenance.json` 記錄的 `decodedIndexSha256` 相同）。只拿來比對，沒有用它補資料。

| 差異 | 內容 |
|---|---|
| 筆數 | 歷史 37,497，本機 37,498 |
| 本機多出 | `lyra_y01-c.html`（record 16606，417 bytes） |
| 歷史多出 | 無 |
| 大小不同 | `music.def`、`string-c.tbl`、`zone3-c.tbl` |
| 大小相同、內容不同 | 176 個 `*-c.html`（見第 5 項） |
| File Offset | 同名檔案裡 14,352 個 offset 相同，14,009 個不同；從 record 3144 開始出現不同 |

三個關鍵檔沒有變：`list.spr`、`list.spz`、`desc-c.tbl` 的 record、File Offset、大小與解碼後的 SHA 都與歷史紀錄相同（見第 3、4 項）。

給準備器的規則：

- 不要 pin 整個 `Text.idx`／`Text.pak` 的 SHA，也不要用舊的 File Offset。一律解索引後用檔名查。
- 要 pin 就 pin 單筆解碼後的 SHA（產品的 `TextArchive.DescSha256` 已經是這種做法）。

## 3. `list.spr`、`list.spz`（pass）

| 檔名 | record | File Offset | 大小 | packed SHA-256 | decoded SHA-256（DES 之後） |
|---|---:|---:|---:|---|---|
| `list.spr` | 15937 | 7,864,336 | 4,067,417 | `69faa6bdb817279d6b20d132c7744be387ccecd9843ac82cf454e363932d33c4` | `523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127` |
| `list.spz` | 15938 | 11,931,753 | 4,864,856 | `ffd64d13d4834664446375d072cdab4c0bf7ba9a13078bf597cbf90eda64f5c2` | `8469c9d5d9cd3105789a0a7169c6270c7b029697d7fcd814a444d2b42b9fc38a` |

- `list.spr` 的 decoded SHA 與歷史 `decoded-list.spr` 相同（設計附錄 D）。內容是純 ASCII，有 11,297 個 `#gfx` 條目。
- `list.spz` 經 DES 後開頭是 `S1223`，後面還有一層 AES（演算法見第 7 項）。解完的 SHA-256 是 `e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242`，與歷史 `inner-list.spz` 相同。內容是 `S1223` 加純 ASCII，有 12,230 個 `#gfx` 條目。
- 兩個檔內容不同（條目數不同）。產品現有三處讀取（`MagicRendering.cs`、`ObjectRendering.cs`、`Afflictions.cs`）都是先找 `list.spz`，沒有才用 `list.spr`。準備器照這個順序。

## 4. `desc-c.tbl` → `objects/names.json`（pass，逐 byte 相同）

| 檔名 | record | File Offset | 大小 | packed SHA-256 | decoded SHA-256 |
|---|---:|---:|---:|---|---|
| `desc-c.tbl` | 4201 | 1,649,737 | 249,300 | `0570773730a7df2cabdf7f3d283d706c77f8b48159e1dc40b845911847371343` | `f06c94f47271fcb01f1d5af63725dfa9a230b5f6be3c0dd1d53cd37628adb7df` |

四個值都與歷史 `names-source.json` 相同，decoded SHA 也與產品的 `TextArchive.DescSha256` 相同。

重建規則：

1. DES 解碼，用 code page 950 解成文字（這個檔 Windows 950 與 Python `cp950` 的結果相同）。
2. 用 `\r\n` 切行。第一行是宣告筆數 `17001`，丟掉；其餘 16,945 行就是名稱陣列。最後一行後面沒有 `\r\n`。不去空白、不去空行（有 19 行是空字串，要保留，否則 ID 會位移）。
3. 陣列索引就是名稱 ID：`names[i]` 對應 `$i`。宣告 17,001 筆但實際只有 16,945 筆，超出的 ID 維持原樣的 `$數字`（產品 `ObjectAssets.Name` 已經這樣處理）。

序列化規則（`names.json`）：

- JSON 字串陣列，UTF-8，沒有 BOM，結尾沒有換行。
- 元素之間用逗號加一個空白（`", "`），開頭 `[`、結尾 `]` 內側沒有空白。
- 非 ASCII 字元直接寫出，不轉成 `\uXXXX`。
- 只跳脫 `"`（寫成 `\"`）與 `\`（寫成 `\\`）；這個表裡沒有其他控制字元。
- 這就是 Python `json.dumps(names, ensure_ascii=False)` 的預設輸出。C# 的 `System.Text.Json` 預設輸出不同（沒有空白、會跳脫非 ASCII），要逐 byte 相同得自己寫出。

驗證：照上面規則產生的結果 361,647 bytes，SHA-256 `40b14f8d3d83249d18933f77610fa066efcc9f0c5a19792a7c6514365d96e01c`，與基線 `names.json` 逐 byte 相同。設計文件寫的「names.json 最後序列化步驟來源不明」到此解決。

## 5. `*-c.html`／`*-c.htm` → `dialogs/templates.json`（規則 pass；內容有 177 筆版本差異）

### 重建規則

照歷史產生器（研究目錄 `npc-commerce-r1/decoder/Program.cs`）的做法，把其中的 `PakTools.Decode` 換成 DES：

1. 照 idx 順序走過每一筆，只取檔名結尾是 `-c.html` 或 `-c.htm` 的（不分大小寫）。本機有 10,263 筆。
2. DES 解碼，用 code page 950 嚴格解成文字（遇到無效位元組就整筆跳過），去掉結尾的 NUL。
3. 鍵＝檔名去掉副檔名，再去掉最後 2 個字元（`-c`）。鍵的比較不分大小寫，保留第一次出現的大小寫。
4. 重複鍵的處理：
   - 內容與已收的相同：略過，不影響結果。
   - 內容不同：把這個鍵整個移除並記為衝突鍵，之後同鍵的全部跳過。
5. 輸出順序＝各鍵第一次出現的 idx 順序。

code page 950 要用 Windows／.NET 的對照表（`CodePagesEncodingProvider` 的 950，例外式 fallback）。Python 內建的 `cp950` 不一樣：它會多拒絕 3 個檔（影響鍵 `aras12`、`egggs`、`sonsletter`），並把 5 個字碼對到不同的 Unicode 字元（.NET 對到 U+F6FD、U+F757、U+F77C、U+F785、U+F78D）。

### 本機結果

| 分類 | 筆數 |
|---|---:|
| 收進模板（首次出現） | 8,902 |
| 重複且內容相同 | 928 |
| 無效 Big5，跳過 | 12 |
| 衝突（觸發的那一筆） | 5 |
| 衝突鍵的後續筆，跳過 | 416 |
| 合計 | 10,263 |

8,902 筆裡有 5 個鍵後來因衝突被移除，最後是 **8,897 個模板**。

- 無效 Big5 的 12 筆：`bs_m4`、`duihuan1`、`foni2`、`maptbox0`、`mellisa3`、`morphern2`、`oreni6`、`qtxt2`、`ratsyu1`、`ratsyu2`、`test_qusest01`（2 筆），都是 `-c.html`。
- 衝突的 5 個鍵：`marbinquest10`、`orcfbukascroll`、`orcfkamescroll`、`orcfnoascroll`、`talentclass_1`。
- 跳過的 433 筆（檔名、decoded SHA、原因）與歷史 `text-provenance.json` 的 `skipped` 清單完全相同。

每一筆的 record、File Offset、大小、packed SHA、decoded SHA 與分類在 [`text-entries.tsv`](text-entries.tsv)（10,266 列：3 個關鍵檔加 10,263 個 html）。最後一欄是分類：`key`、`ok`（首次出現並收下；其中 5 個衝突鍵的首筆後來被移除）、`dup-identical`、`skip:invalid-big5`、`skip:conflict`、`skip:ambiguous`。

### 與基線比對（基線 8,896 個模板）

| 比對 | 筆數 |
|---|---:|
| 鍵相同、內容相同 | 8,720 |
| 鍵相同、內容不同 | 176 |
| 只有基線有 | 0 |
| 只有本機有 | 1（`lyra_y01`） |
| 兩邊共有鍵的順序 | 相同 |

176 筆不同的原因只有一種：`<img src="#N">` 的圖片編號。基線是 312 與 314，本機是 602 與 603（312→602 共 162 處，314→603 共 108 處）。把本機這兩個編號換回去之後，176 筆全部與基線相同。也就是說，本機 Text.pak 裡這 176 個 html 被改過圖片編號，其他內容沒有動。176 個鍵的清單在 [`text-templates-diff-ids.txt`](text-templates-diff-ids.txt)。

所以 8,896 個基線模板全部對得上，差異都來自來源檔版本。

602／603 對產品的影響（第 2 次嘗試補查，只讀原始碼與 PakViewer 清單）：

- 產品的圖號換號在 `engine/source/NativeUi/HyperTextLayout.cs:136`：`SurfList` 只把 312→1109、314→1107、331→601，其他號碼照原號（該行註解標為推測）。所以改用本機來源後，這 176 頁的買賣按鈕會直接畫 surf 602／603，不再畫 1109／1107。
- `Sprite.idx` 有 `602.img`（record 85156）、`603.img`（record 85161），也有 `312.img`、`314.img`、`1107.img`、`1109.img`；圖檔本身不缺。
- 沒有圖時的文字標籤（「購買」／「出售」）是照 `action` 判斷，不是照圖號（同檔 183 行），不受影響。
- 602／603 畫出來是不是買賣按鈕的樣子、原客戶端對這兩個號碼有沒有另外換號，沒有查，屬於待驗證。

### 序列化規則（`templates.json`）

- 單一 JSON 物件，鍵是模板名，值是模板字串。沒有縮排、沒有空白、結尾沒有換行。
- 整個檔案只有 ASCII，沒有 BOM。
- 這是 C# `JsonSerializer.Serialize(Dictionary<string,string>)` 的預設輸出，跳脫規則如下：
  - `\` 寫成 `\\`；倒退、tab、換行、換頁、歸位寫成 `\b`、`\t`、`\n`、`\f`、`\r`。
  - `"`、`&`、`'`、`+`、`<`、`>`、`` ` `` 寫成 `\uXXXX`（例如 `<` 是 `\u003C`）。
  - 其他控制字元、U+007F 與所有非 ASCII 字元寫成 `\uXXXX`，十六進位用大寫。
- 產品讀取時用不分大小寫的字典（`NpcTemplates.cs`）。

驗證：

- 用 Python 照這套規則把基線的內容重新序列化，結果與基線檔逐 byte 相同（9,116,328 bytes，SHA-256 `3ebb7902…0983d7`）。所以序列化規則是對的。
- 本機內容照同樣規則序列化是 9,117,479 bytes、8,897 個鍵，SHA-256 `6a7c85e44fa0e6dbddc9526096849edd4cfdb5bc4dd9b265ab8ed8110a357aa8`。這個值與基線不同是預期的（177 筆版本差異）。它是 Python 模擬的結果，C# 實際輸出要等 T470 實跑確認。

## 6. PakTools 的 `Map1.bin`–`Map5.bin`（pass）

`PakTools.cs` 的結構是標準 DES：`Map1` 做初始置換，跑 16 輪（每輪用 `Map5` 的 6 bytes、`Map4` 查 S-box、`Map3` 做 P 置換），左右對調後用 `Map2` 做最終置換。解碼時輪金鑰從第 15 輪倒著用。

用 FIPS 46-3 的標準常數加上金鑰 `7E214023255E243C` 自己算出五張表，與 PakViewer 原始碼裡的五個檔（`D:\lineage開發\PakViewer-main\src\Lin.Helper.Core\Resources\`）逐 byte 相同：

| 檔案 | 大小 | SHA-256 | 內容 |
|---|---:|---|---|
| `Map1.bin` | 2,048 | `d870074914025e855aa5985a2d6778f1e277036bf9c9f03dec61f3c496fec35c` | 初始置換 IP，照每個 byte 的高、低 4 bits 查表（8 × 32 × 8 bytes） |
| `Map2.bin` | 2,048 | `73f5d95c401726b2c92ec96a696ba15f0e5a5c6dd9ac6beb3736a81772a11531` | 最終置換 IP⁻¹，排法同上 |
| `Map3.bin` | 4,096 | `bcab55cd4867abb157b8c81f750c335d85aebb168568f6ba9d5f4d56df018be2` | P 置換，照 byte 查表（4 × 256 × 4 bytes） |
| `Map4.bin` | 16,384 | `76568b54cd6f40dedcb19d19d2c74e48d79ce549f1b2c7375fe172baccb0cf4e` | S-box 兩兩合併：12 bits 輸入查出 1 byte（4 × 4,096） |
| `Map5.bin` | 96 | `4467ac1dbd0cd2bc9af20c9859bf81108c34965151320c635ac54d64621fad30` | 該金鑰的 16 把輪金鑰，各 48 bits |

另外把 `PakTools.DecodeBlock` 照原文移植成 Python（用磁碟上的五張表），對 10,000 個區塊（Text.idx 前 3,000 個、Text.pak 隨機 5,000 個、亂數 2,000 個）與 DES-ECB 解密比對，全部相同。

結論：五張表不含任何額外資料，就是 DES 常數與金鑰排程的預先計算。`TextArchive` 用 .NET 內建的 DES 加同一把金鑰，結果等價。準備器不需要這五個檔，設計文件前置 6 解除。

## 7. 內層編碼（pass）

- **html**：DES 解碼後直接是 Big5 文字，沒有壓縮、沒有第二層加密。10,263 筆裡 10,110 筆的第一個 byte 是 `<`，其餘開頭是引號、換行、中文字等一般文字。12 筆不是合法 Big5（第 5 項已列），照歷史做法跳過。
- **`desc-c.tbl`**：DES 解碼後直接是 Big5 文字。
- **`list.spr`**：DES 解碼後直接是 ASCII 文字。
- **`list.spz`**：DES 解碼後還有一層。依據是產品現有程式（`MagicRendering.cs` 的 `EffectSpriteCatalog.LoadMetadata`），本輪用 Python 照做，結果 SHA 與歷史紀錄相同：
  1. 前 5 bytes 是明文標記 `S1223`，不動。
  2. 第 5 byte 起，取 16 的整數倍長度，用 AES-128-CBC 解密，不補位。金鑰 `DC8401212A40200ADD25B9A70DB9C94E`，IV `3E0978AAC4D53063300C5F9A807F2246`（與 UI XML 用的是同一組格式常數）。
  3. 剩下不足 16 bytes 的尾巴：每個 byte 與 16 bytes 之前的密文 byte 做 XOR（沒有完整區塊時與 IV 對應位置 XOR）。

## 查過的 pak

本文沒有宣稱任何缺資源。仍用 PakViewer CLI（`pakviewer-cli.exe pak list`，唯讀）把 `0-客戶端` 的 24 個 idx 全部列過，找 `list.spr`、`list.spz`、`desc-c.tbl` 與 `*-c.html`／`*-c.htm`：

| idx | 檔數 | 命中 |
|---|---:|---|
| `Text.idx` | 37,498 | 3 個關鍵檔加 10,263 個 html |
| `Tile.idx` | 5,446 | 0 |
| `Sprite.idx` | 92,356 | 0 |
| `Sprite00.idx`–`Sprite15.idx` | 10,184–10,972 | 0 |
| `LiTo\LTtext.idx` | 353 | 116 個 `*-c.html`；沒有 `list.spr`、`list.spz`、`desc-c.tbl` |
| `LiTo\LTsurf.idx`、`LTsprite.idx`、`LTtile.idx`、`LTicon.idx` | 1,623／469／1,210／227 | 0 |

`launcher\TW13081901.pak` 沒有 idx，PakViewer 打不開（與 `docs/reference/pak-resources.md` 相同）。

`LiTo\LTtext` 不在本張任務範圍，基線也沒有用到它，這裡只記觀察：

- 116 個 `*-c.html` 裡，112 個是 Text 沒有的檔名。
- 4 個與 Text 同名但內容不同：`inn-c.html`、`inn2-c.html`、`inn20-c.html`、`inn4-c.html`。
- 另有 `LTlist.spr`、`LTdesc-c.tbl` 等 LT 專用表。

## 給 T453（文字類準備器）的要點

1. 讀取用現有的 `TextArchive`；`list.spz` 內層用現有的 AES 流程。不需要新演算法。
2. 用檔名找 entry，不用舊 offset，不 pin 整個容器的 SHA。
3. `names.json`：照第 4 項，可以做到與基線逐 byte 相同（要自己寫出 `", "` 分隔、不跳脫非 ASCII 的格式）。
4. `templates.json`：照第 5 項。用本機客戶端產生的結果會與基線差 177 筆，驗收要用「鍵集合＝基線＋`lyra_y01`、176 筆只差圖片編號」來判斷，不要求與基線 SHA 相同。
5. code page 950 用 .NET 的 `CodePagesEncodingProvider`，例外式 fallback。
