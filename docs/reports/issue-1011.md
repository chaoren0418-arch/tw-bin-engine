# Issue #1011 報告：T440 Q9-A 書籤區域群組（參考客戶端 zone3）

依據：[`docs/reference/followups-17.md`](../reference/followups-17.md) §1、§7（T422／#966）。分支 `task/1011`。
主控裁定：TW16 是原版，照 TW16 做；3.81 本機沒有 `zone3.xml`，照 #533 的先例改讀參考客戶端。**這不是 3.81 行為。**

## 做了什麼

### 新檔 `engine/source/NativeUi/ZoneBookmarkGroups.cs`
- `ZoneBookmarkGroups.Parse(xml, zone3desc)`：解析 `zone3(-c).xml` 的 `<zone>`，只留有 `<bookmark id≠0>` 而且有 `<map num>` 的。
  - 每筆記：bookmark id、名稱、地圖編號、`left/top/right/bottom` 矩形。
  - 名稱：有 `<zone3desc>N</zone3desc>` → `zone3desc-c.tbl` 第 N 筆（去色碼）；沒有、超過筆數或是空字串 → `<name>`。
  - 群組＝同一 bookmark id 的**全部**區域；群組名稱＝最後一筆的名稱。依 id 由小到大排。
  - DTD 不處理（`DtdProcessing.Prohibit`），不解析外部實體。
- `ZoneBookmarkGroups.Load(RefClientXmlSource)`：
  - XML 用既有的 `RefClientXmlSource.TryRead("zone3.xml")`（參考客戶端 Tile.pak，先找 `-c`，在記憶體解密）；
  - 表用 `TextArchive.Open(<參考客戶端>)` 讀 `zone3desc-c.tbl`（只在記憶體解碼）；
  - UI 日誌記一行 `zone-bookmark …`：XML 明文／封存內容 SHA-256、Text.pak 第幾筆與表的 SHA-256、組數，並和 kor25 的已知 SHA-256 比對；
  - 任一讀不到（沒有參考客戶端、沒有 zone3、沒有 zone3desc、XML 壞掉、封存打不開）→ 回 null，記原因，不丟例外。
- `ZoneBookmarkGroupSource`：第一次要時在執行緒集區讀一次；讀完前 `Current`＝null，書籤窗先用地圖編號。背景例外會被接住、記日誌。

### `engine/source/NativeUi/BookmarkPanel.cs`（只動區域下拉）
- 註冊（同檔 `[UiPanel(178)]`）改成 `r=>new BookmarkWindowPanel{ZoneGroups=…}`，來源是 `UiResources.Xml.Reference`。沒改註冊表。
- 新屬性 `ZoneGroups`（`Func<ZoneBookmarkGroups?>`）：`Update` 時取一次，拿到就固定。拿到的那一刻篩選歸回「全部」（地圖編號和 bookmark id 不能混用）。
- 有群組表時：
  - `ZoneItems`＝全部＋bookmark id（由小到大），項目和標籤文字（`ZoneLabel`）＝群組名稱；
  - 篩選（`Refresh`）＝群組任一區域符合：地圖相同，而且（沒有矩形，或書籤座標在矩形內）；
  - 書籤變動不會把篩選歸回全部（群組是固定表，沒符合的就是空清單）。
- 沒有群組表時：行為和原本完全一樣（地圖編號、該地圖沒書籤就歸回全部）。`ZoneItemText` 靜態方法保留。
- 檔頭註解：把「表來源 `sub_F35D90` 待驗證」改成 FU17 的結論與本任務做法，刪掉「不做：區域表（Q8）」。
- `--classic-ui` 的 WinForms 書籤分頁沒動。

### 新檔 `engine/source/NativeUi/ZoneBookmarkGroupTests.cs`（`[WorldModuleTest("zone-bookmark-groups")]`，自動發現）
全部用合成資料（假 XML、假 Big5 表、記憶體內 Tile／Text 封存、合成版面與圖片）：
- 解析：排序、id 0／沒有 bookmark／沒有 map 不算、名稱取最後一筆、zone3desc → 名稱（含去色碼、空字串與超過筆數時退回 `<name>`）、SHA-256 記錄、DTD 拒絕、壞 XML。
- 篩選：半開區間邊界（left/top 含、right/bottom 不含）、四個 0＝沒有矩形、同群組多個矩形、多張地圖。
- 讀取：先讀 `zone3-c.xml`；缺 zone3desc、缺 zone3、Text 封存打不開、XML 壞掉、資料夾不存在 → null；背景來源（含例外）。
- 書籤窗：沒有群組表＝地圖編號；有群組表＝下拉與標籤顯示名稱、各群組篩選結果；背景讀完時從地圖篩選切到群組並歸回全部；拿到後不再換回。
- kor25 存在時（本機）多做一次唯讀核對（id 1–19、id 1＝說話之島、id 14＝甘特之家、SHA-256 相同）；CI 沒有參考客戶端就略過。

## 查證（已驗證／推測）

TW16 BIN `P01-分析基準/input/TW1608262200.bin`（SHA-256 `e12949cf…641a2`），`.text` FO＝VA−`0x400C00`。本任務用 Capstone 反組譯（腳本 `scratch/t1011/t1011_disasm*.py`，不進 git）：

| 函式 | VA／FO | 結果 |
|---|---|---|
| `sub_F2F9D0` | `0xF2F9D0`／`0xB2EDD0` | **已驗證**：只呼叫 `sub_F36E50(this+16, id)` 回傳 vector |
| `sub_F36E50` | `0xF36E50`／`0xB36250` | **已驗證**：走訪全部紀錄，`word [rec+0x42]`（＝+66 bookmark id）等於 id 的**每一筆**都 push 進 vector。FU17 的「取出選到的群組」改成已驗證：群組＝同 id 的全部區域 |
| `sub_579BD0` | `0x579BD0`／`0x178FD0` | **已驗證**：矩形的 (left,top) 與 (right,bottom) 都等於 (0,0) → 沒有矩形 |
| `sub_500F80` | `0x500F80`／`0x100380` | **已驗證**：`x≥left && y≥top && x<right && y<bottom`（半開區間） |

- **推測**：`sub_BDC920` 的逐筆迴圈沒有逐行追。引擎取「群組裡任一區域符合就顯示」。
- **推測**：`sub_A6D780(N)`＝`zone3desc-c.tbl` 第 N 筆（同 FU17§1.2）。kor25 的 19 組名稱都合理（見下表），可當旁證。
- **推測（引擎自訂）**：該筆是空字串時用 `<name>`；參考客戶端沒有 zone3desc 表時整個不用（不顯示韓文 `<name>`）。

### 參考客戶端 kor25（`D:\381懶人包kor25(原)`，PakViewer CLI 唯讀，解到 `scratch/t1011/ref/`，不進 git）
- `Tile.idx`（SHA-256 `e205c88f…0a6a`）：`zone3-c.xml` 251,915 B（封存內容 `ad8795b2…f302`，明文 `7064426c…23f2`）、`zone3.xml` 208,650 B。
- `Text.idx`（`dee5fd7f…462c`）：`zone3desc-c.tbl` 12,793 B，解碼後 `7fe1ef1c…a42c`，第一行 802。
- `zone3-c.xml`：1,551 個 `<zone>`，44 個有 `<bookmark>`，id 1–19：

| id | zone3desc | 名稱 | id | zone3desc | 名稱 |
|---|---|---|---|---|---|
| 1 | 0 | 說話之島 | 11 | 231 | 海音 |
| 2 | 214 | 妖魔森林 | 12 | 233 | 歐瑞 |
| 3 | 217 | 古魯丁 | 13 | 235 | 亞丁 |
| 4 | 219 | 風木之城 | 14 | 3 | 甘特之家 |
| 5 | 221 | 妖魔森林 | 15 | 191 | 沉默洞穴 |
| 6 | 223 | 肯特 | 16 | 192 | 布魯迪卡洞穴 |
| 7 | 211 | 銀騎士地區 | 17 | 15 | 哈汀研究所 |
| 8 | 224 | 龍之谷 | 18 | 365 | 希培利亞 |
| 9 | 227 | 威頓 | 19 | 366 | 貝希摩斯 |
| 10 | 229 | 奇岩 | | | |

（id 2 與 id 5 在 kor25 都翻成「妖魔森林」：id 5 的韓文是 요정 숲＝妖精森林，是 kor25 表本身的字，引擎照表顯示。）

### 本機 3.81 客戶端（`D:\lineage開發\0-客戶端`，PakViewer `pak search … zone3`，結果 `scratch/t1011/local_zone3_search.tsv`）
查了 24 個 idx：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile.idx`。
只有 `Text.idx` 的 `zone3-c.tbl`（62,600 B，地圖顏色／alias 表，不是 xml）。**沒有 `zone3.xml`、`zone3-c.xml`、`zone3desc-c.tbl`**，和 FU17§0 一致。

## 怎麼驗證
- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`（restore 來源用原始專案的 `engine/vendor`，有設 `DOTNET_CLI_HOME` 到 `scratch/`、`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`）→ **建置成功，0 個錯誤**；警告只有既有檔案（`HtmlActionTests.cs`、`MagicUi.cs`），新檔沒有警告。
- `python -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "trackedFiles": 4613, "errors": 0}`。
- 單元測試（既有＋本組 `zone-bookmark-groups`）：依本機限制不在本機執行，交給 PR 的 GitHub Actions CI。結果以 PR 上的 CI 為準。
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`；沒有複製任何遊戲或參考客戶端檔案進 repo。

## 未驗證什麼
- 真機畫面：書籤窗下拉的名稱顯示、篩選結果、背景讀完時的切換（**真機再確認**，交給 Codex／dot）。
- `sub_BDC920` 的「任一區域符合」組合方式（推測）。
- `sub_A6D780` 的表名（推測是 zone3desc-c.tbl）。
- kor25 真表核對那一段只在有 kor25 的機器會跑；CI 會略過。
- 其他參考客戶端（pmc、c760、t89、ly）沒有核對；t89 沒有 zone3desc-c.tbl（FU17§0），用它時下拉維持地圖編號。

## 產出路徑
- `engine/source/NativeUi/ZoneBookmarkGroups.cs`（新）
- `engine/source/NativeUi/ZoneBookmarkGroupTests.cs`（新）
- `engine/source/NativeUi/BookmarkPanel.cs`（區域下拉與註冊）
- `docs/reports/issue-1011.md`（本檔）

## 給下一棒的提示
- 想關掉這個功能：`TW_REF_CLIENT` 或 `native-ui.json` 的 `refClient: false` 會一起關掉參考客戶端（XML、圖、字串、區域群組）。
- 真機驗收時看 UI 日誌的 `zone-bookmark …` 那一行：應寫「19 組（1551 個 zone，…，與 kor25 相同）」。
- 若之後追到 `sub_BDC920` 用的是「全部區域都要符合」或只看最後一筆，只要改 `ZoneBookmarkGroup.Matches`，測試裡「同一群組的兩個矩形都算」那一條跟著改。
