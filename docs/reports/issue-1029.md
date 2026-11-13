# Issue #1029 — T444 來源閉合：Text 容器版本差異與文字類素材

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1029`；階段：resource。只新增文件，沒有改程式。

## 結論

1. **7 項全部 pass，沒有 blocked。** 只用使用者原客戶端的 `Text.idx`／`Text.pak`，加上產品已有的 `TextArchive`（DES）與 `list.spz` 的 AES 流程，就能重建 `objects/names.json`、`dialogs/templates.json`，並讀到 `list.spr`／`list.spz`。逐項結果在 [`docs/reference/asset-recipes/text.md`](../reference/asset-recipes/text.md)。
2. **`names.json` 重建結果與基線逐 byte 相同**（SHA-256 `40b14f8d…5d96e01c`）。序列化規則已定出。
3. **`templates.json` 的規則對，但本機來源與基線當時的來源是不同版本**：鍵集合是基線 8,896 個加 1 個新的（`lyra_y01`）；8,720 筆內容相同，176 筆只差 `<img src="#N">` 的圖片編號（基線 312／314，本機 602／603）。把這兩個編號換回去後 176 筆全部與基線相同。這不是缺資源，也不是演算法問題。
4. **PakTools 的 `Map1.bin`–`Map5.bin` 就是標準 DES 的五張表**（初始置換、最終置換、P 置換、S-box、金鑰 `7E214023255E243C` 的輪金鑰）。自己從標準常數算出來的表與那五個檔逐 byte 相同。準備器不需要這五個檔，設計文件的前置 6 解除。
5. **html 沒有內層編碼**（DES 之後直接是 Big5）。`list.spz` 內層是 AES-128-CBC，產品已經實作。
6. 設計文件的前置 4（Text 版本差異）也解除：差異已經查清，三個關鍵檔（`list.spr`、`list.spz`、`desc-c.tbl`）沒有變。

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md`、`docs/reports/issue-1027.md`、`docs/design/portable-delivery.md`、`engine/source/TextArchive.cs`，以及研究目錄的歷史解碼器原始碼（`PakTools.cs`、兩個 `Program.cs`）與來源紀錄（只讀，用來對照）。
- 在 `scratch/t444/` 寫 Python 腳本（不進 git），只讀客戶端：
  - `text_probe.py`：解索引、列出目標 entry、重建 names 與 templates 並和基線比對。
  - `tpl_probe.py`：比較 Windows code page 950 與 Python `cp950` 兩種解碼，並和歷史 `text-provenance.json` 的清單對照。
  - `des_maps.py`：從標準 DES 常數推出五張表，與 `Map1`–`Map5.bin` 比對；另把 `PakTools.DecodeBlock` 移植後與 DES 逐區塊比對。
  - `pv_cross.py`：把 PakViewer 的清單與 Python 解出的索引逐筆比對，並看 `LiTo\LTtext` 的重疊。
- 用 PakViewer CLI `pak list` 列出 `0-客戶端` 全部 24 個 idx（輸出在 `scratch/t444/pv/`）。
- 寫 `docs/reference/asset-recipes/text.md`，附 `text-entries.tsv`（10,266 列的 record／File Offset／大小／SHA）與 `text-templates-diff-ids.txt`（176 個差異鍵）。

## 已驗證

都是本輪實際跑出來的結果，數字出處是 `scratch/t444/out/result.json`、`tpl-result.json` 與腳本輸出。

- **容器**：`Text.idx` 1,049,948 bytes、`Text.pak` 26,267,704 bytes，SHA 與設計附錄 B 相同。照 `TextArchive.cs` 的格式解出 37,498 筆，範圍剛好鋪滿整個 pak。PakViewer 列出的 37,498 筆（record、檔名、offset、大小）與 Python 解出的逐筆相同。
- **版本差異**（對照歷史解碼索引 37,497 筆）：本機多 `lyra_y01-c.html` 一筆；`music.def`、`string-c.tbl`、`zone3-c.tbl` 大小不同；14,009 個同名檔的 File Offset 不同。舊 offset 不能沿用。
- **關鍵檔**：
  - `list.spr`（record 15937）解碼 SHA `523c61ad…e8b9f127`，與歷史相同。
  - `list.spz`（record 15938）內層解完 SHA `e8cec038…bfbe6242`，與歷史相同。
  - `desc-c.tbl`（record 4201）packed 與 decoded SHA 都與歷史相同，也與產品 `TextArchive.DescSha256` 相同。
- **names**：`\r\n` 切行、丟掉第一行（宣告 17001）、得 16,945 筆；用 Python `json.dumps(ensure_ascii=False)` 的格式寫出，與基線逐 byte 相同。
- **templates**：
  - 10,263 個 `-c.html`／`-c.htm` 候選，最後 8,897 個模板。
  - 跳過的 433 筆（12 筆無效 Big5、5 筆衝突、416 筆衝突鍵後續）與歷史 `skipped` 清單的檔名、SHA、原因完全相同。
  - 與基線：8,720 筆相同、176 筆不同、基線獨有 0、本機獨有 1；共有鍵的順序相同。
  - 176 筆不同全部能用「602→312、603→314」還原成基線內容（162 處加 108 處）。
  - 序列化規則：用 Python 模擬 `System.Text.Json` 預設跳脫，把基線內容重新序列化後與基線檔逐 byte 相同。
- **code page 950**：Windows 950（`MultiByteToWideChar`，嚴格模式）的結果與 .NET 產生的基線一致；Python 內建 `cp950` 會多拒絕 3 個檔、5 個字碼對到不同字元。
- **Map1–Map5**：五個檔與自己推導的 DES 表逐 byte 相同；移植的 `DecodeBlock` 在 10,000 個區塊上與 DES-ECB 解密相同。
- **內層編碼**：html 與 `desc-c.tbl` 是 Big5，`list.spr` 是 ASCII，都沒有第二層；`list.spz` 是 `S1223` 加 AES-128-CBC，尾端不足 16 bytes 用前一段密文 XOR。
- **提交內容檢查**：`python -B -X utf8 engine/tools/check-no-game-assets.py` 在加入新檔後通過（結果見下方「怎麼驗證」）。

## 推測

- **176 個 html 的圖片編號是本機客戶端後來被改過的**（買、賣按鈕的圖從 312／314 換成 602／603）。依據只有「大小相同、只差這兩個編號、歷史 SHA 不同」，沒有查是誰在什麼時候改的。
- **.NET 的 code page 950 與 Windows API 的 950 在這批資料上結果相同**。依據是 8,896 個基線模板全部對得上、跳過清單也相同；但本輪沒有直接執行 .NET 來確認（本機不能執行自己寫的程式）。
- 歷史解碼器用的 `Lin.Helper.Core` DLL 內嵌的五張表，與 PakViewer 原始碼目錄裡的五個檔是同一份。本輪比對的是後者；前者只從輸出相同間接確認。

## 待驗證（本張沒做）

- **C# 準備器的實際輸出**：`templates.json` 用本機客戶端產生時預期是 9,117,479 bytes、SHA-256 `6a7c85e4…0a357aa8`（Python 模擬值）。要等 T453 寫好、T470 實跑才能確認。
- **產品要不要處理 602／603**：基線模板用 312／314，本機客戶端用 602／603。第 2 次嘗試補查了產品原始碼：`HyperTextLayout.SurfList`（`engine/source/NativeUi/HyperTextLayout.cs:136`）只換 312→1109、314→1107、331→601，602／603 照原號畫；`Sprite.idx` 有 `602.img`、`603.img`，圖不缺；沒有圖時的「購買」／「出售」標籤照 `action` 判斷，不受影響。還沒查的是：602／603 畫出來是不是買賣按鈕、原客戶端對這兩個號碼有沒有另外換號。這要看圖或實機，交 T453 與之後的實機驗收。
- **`LiTo\LTtext` 是否要疊加**：它有 116 個 `*-c.html`，其中 4 個（`inn`、`inn2`、`inn20`、`inn4`）與 Text 同名但內容不同。基線沒有用到它，原客戶端執行時會不會優先讀它，本張沒有查。
- 沒有建置、沒有執行任何編譯出的程式；沒有讀 SQL／JAR／DB；沒有改客戶端、封存或主目錄。
- CI 結果以 PR 為準，push 當下尚未取得。

## 第 2 次嘗試做了什麼

- 第 1 次的結果被調度器標成 failed，留言與 log 都沒有寫原因。當時分支落後 main 14 個 commit，也沒有 PR。
- 本次把 `origin/main` 合併進 `task/1029`（沒有衝突，沒有動 `docs/REPORT.md` 與 lock），重跑 `check-no-game-assets.py` 通過。
- 補查 602／603 在產品裡的處理（見上一節第 2 點與 `text.md` 第 5 項）。其餘結論沒有變，解析腳本沒有重跑。

## 怎麼驗證

在 worktree 根目錄執行（輸出只寫 `scratch/t444/`）：

```
python -I -X utf8 scratch/t444/text_probe.py <客戶端> <主目錄>/engine/assets scratch/t444/out <研究目錄>/reference/object-assets/text-index-decoded.bin
python -I -X utf8 scratch/t444/tpl_probe.py  <客戶端> <主目錄>/engine/assets scratch/t444/out <研究目錄>/reference/development/npc-commerce-r1/text-provenance.json
python -I -X utf8 scratch/t444/des_maps.py   D:\lineage開發\PakViewer-main\src\Lin.Helper.Core\Resources <客戶端>
pakviewer-cli.exe pak list <客戶端>\<每個 idx>
python -I -X utf8 scratch/t444/pv_cross.py   <客戶端> <主目錄>/engine/assets scratch/t444/pv
python -B -X utf8 engine/tools/check-no-game-assets.py
```

腳本依任務規定只放 `scratch/`，worktree 清掉後就沒有了。演算法與規則都寫在 `text.md`，照著可以重寫。

## 產出路徑

- `docs/reference/asset-recipes/text.md`：逐項 pass／blocked、演算法、序列化規則、查過的 pak 清單。
- `docs/reference/asset-recipes/text-entries.tsv`：3 個關鍵檔加 10,263 個 html 的 record、File Offset、大小、packed SHA、decoded SHA、分類。
- `docs/reference/asset-recipes/text-templates-diff-ids.txt`：與基線內容不同的 176 個模板鍵。
- `docs/reports/issue-1029.md`：本報告。

## 給下一棒的提示

- **T453（文字類準備器）**：不需要新的解碼演算法。讀取用 `TextArchive`，`list.spz` 用現有的 AES 流程（目前在三個檔各寫了一份，可以順手收成一個共用函式）。
- 找 entry 一律用檔名，不要用舊 File Offset，也不要 pin 整個 `Text.idx`／`Text.pak` 的 SHA；使用者的客戶端會被改動，本機這份就已經和研究當時不同。
- `names.json` 可以要求與基線逐 byte 相同，但 `System.Text.Json` 的預設輸出做不到，要自己寫出（分隔是逗號加空白、不跳脫非 ASCII）。如果 T452 的契約改成只要求語義相同，就可以直接用預設序列化。
- `templates.json` 的驗收不要比基線 SHA。用「鍵集合＝基線＋`lyra_y01`、176 筆只差圖片編號、其餘相同」判斷，清單在 `text-templates-diff-ids.txt`。
- 模板的重複鍵規則（內容相同就略過、內容不同就整個鍵移除）要照做，否則筆數會對不上；`orcfbukascroll`、`orcfnoascroll` 各有兩百多筆同名檔。
- code page 950 用 `CodePagesEncodingProvider`、例外式 fallback。寫測試時不要用 Python `cp950` 當對照。
- 602／603 與 `LiTo\LTtext` 兩件事需要有人決定（見待驗證），建議 T452 收進已知限制清單或交主控裁定。
