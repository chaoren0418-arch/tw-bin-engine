# Issue #850：T366 R9-F HyperText 排版器（code）

依據：`docs/reference/followups-9.md` 第 10.1 節、第 16.2 節 R9-F、第 16.3 節 M34；`gap-matrix.md` 第 15.11 節。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/HyperTextLayout.cs`（新） | HTML → `HyperTextDocument` 的解析器、行內排版器 `HyperTextLayout.Build`、顏色解碼 `HyperTextColors`、文件登記表 `HyperTextDocuments`、讀 NpcTemplates 原始 HTML 的 `NpcTemplateMarkup` |
| `engine/source/NativeUi/HyperTextPanel.cs` | 對得上文件時本文改用 `RowKind.Rich` 列（行內連結、樣式、圖），高列後接 `RowKind.Spacer`；點擊、繪製、裁切；對不上時維持舊的「連結各占一列」 |
| `engine/source/NpcInteraction.cs` | `RenderHyperText` 渲染模板頁時同時解析成文件；純文字與選項數都和 NpcTemplates 相同才登記（`LastHyperTextDocument`、`HyperTextDocuments`） |
| `engine/source/NativeUi/HyperTextLayoutTests.cs`（新） | 離線測試群組 `hypertext-layout`（11 段） |

支援的標記（FU9§10 已驗證的部分）：
- **行內連結** `<a action|link>`：排在文字流裡，可以跨列；每個 `<a>` 開頭的字色設成常數 `0x00B0FFFF`，`<a>` 之後再開的 `<font fg>` 才蓋過它。選項編號跟 `NpcTemplates.Render` 完全一樣（關閉的 `<a>` 與帶 link／action 的 `<img>` 依出現順序 0,1,2…；巢狀 `<a>` 前一個不算選項）。
- **`<font fg bg face Size>`**：fg／bg 只收十六進位數字（`#`、`0x` 視為無效、不改色），取低 24 位元；face、Size 存成資料。
- **`<img src>`**：`#N` 走 SurfList（312→1109、314→1107、331→601，其他照原號），`pN` 記成 PngList 號；帶 link／action 的圖可以點；存 tooltip。
- **`<var src="#N" textwidth img>`**：取第 N 個資料字串（缺 #0 用對話對象名稱，和 NpcTemplates 一樣）；顯示時把第一個 `^` 換成空白；`textwidth`＝最小寬度（不夠就補空白）；`<var img>` 的值是數字時畫成圖。
- **`<b>/<i>/<u>/<p align>/<br>/<center>`、對齊** left／center／right（justify／scatter 先當左對齊）。

面板怎麼拿到文件：`NpcUi.cs` 建 `HyperTextView` 時只帶純文字（本組不能改那個檔），所以由 `WorldSession` 把文件登記到 `HyperTextDocuments.Shared`（最多 16 份），面板用 `view.Text` 找回來；宿主做過 `$N` 名稱展開時，`ResolveTo` 會從顯示的文字反推每個 `$N` 的值，套進文件後再核對一次。`HyperTextView` 也多了選填參數 `Document`，之後直接傳就好。另外每格都會呼叫 `Show`，所以同一頁（文字、文件、選項 id、登記表版本都沒變）會沿用上次的對照與排版結果。

## 怎麼驗證

- **已驗證（本機）**：`dotnet build engine/source/OfflineClient.csproj -c Release` 編譯通過，0 個錯誤（3 個警告都是原本就有的：MagicUi.cs、HtmlActionTests.cs）。worktree 沒有 `engine/vendor`，所以 restore 改用 scratch 裡一份 NuGet 設定，指向原專案的 `engine/vendor`（只讀）。
- **已驗證（本機）**：`python engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- **已驗證（CI，PR #882，run 42895000785）**：本機規定不執行編譯出來的程式，所以 `--module-tests` 交給 GitHub Actions 跑。`dotnet`、`map-continuity`、`npc-offline` 三個 job 都通過；新群組 `hypertext-layout` 102 項檢查全部通過；既有的 `hypertext-panel`、`hypertext-followup` 等群組沒有失敗。
- 測試內容：①5 份自寫 HTML 的純文字、選項數和 `NpcTemplates.Render` 完全一致；②顏色解析與兩種位元組順序；③樣式堆疊；④選項編號、SurfList 換號、pN；⑤var 的 `^`、textwidth、var img；⑥排版：行內連結跨列、逐字換行、段落與 `<br>`、置中／靠右、圖的列高、缺圖時的處理；⑦`$N` 展開後的對照、登記表上限；⑧面板 Rich 列：點字連結、點圖連結、停用的連結不能點、圖與底色畫在算好的位置；⑨對不上（選項數、id、文字）時退回舊排法；⑩高列捲動時的裁切與點擊；⑪`WorldSession` 登記文件。
- 測試用的 HTML 全部自己寫（格式模仿 `templates.json`），沒有複製遊戲文字或資源。為了決定支援範圍，有唯讀統計 `releases/r29/assets/dialogs/templates.json`（8,896 頁）的標籤用量：`<font fg>` 15,613 個（最常見 `ffffff`、`ffffaf`、`98FB98`）、`<p align>` 8,294 個、`<img>` 3,437 個（`#312 action=buy` 161 個）、`<var>` 4,795 個（`<var img>` 約 695 個）、`bg`／`face`／`size` 14 個、沒有 `textwidth`／`lgap`／`agap`／`pN`。腳本放在 `scratch/survey*.py`，不進 git。

## 未驗證什麼（推測或待真機）

- **M34 對色（真機再確認）**：fg／bg 的位元組順序先當 `0xRRGGBB`，理由是模板作者寫的 `98FB98`（palegreen）、`A2A2A2` 都是 HTML 習慣的 RGB。照這個順序，連結色 `0x00B0FFFF` 會是 **B0FFFF（淡青）**。如果真機是 COLORREF 順序，把 `HyperTextColors.Order` 改成 `Bgr`，連結色就變成 FFFFB0（淡黃）。舊排法的連結色（調色盤 13 黃色）沒動。
- 以下都是推測：`<p>`／`</p>`／`<center>` 當段落（前面有字才換行），`<br>` 一律換行；原始檔裡的 `\r\n` 不算換行；每列開頭的空白去掉；圖和字都靠上對齊，列高取最高的圖；`<b>` 用位移 1 px 重畫一次來模擬粗體；`<i>`、face、Size 只存資料，不換字型；justify／scatter 當左對齊。
- 沒有圖的可點圖改畫成文字標籤（「購買」「出售」或 alt），這是**引擎自訂**的做法，免得功能不見；沒有圖、也不能點的圖就不畫。這次沒有宣稱任何資源缺少，所以沒有跑 PakViewer 查詢；1109／1107／601 等圖執行時從 SurfCatalog 取，取不到就照上面處理。
- 沒有接的部分：PngList（pN）載入、N ≥ 1000 的客戶端狀態變數（顯示和 NpcTemplates 一樣，是「缺少參數」）、`<subHtml>`、`<time>`、`<date>`、`<may>`、img 的 align／valign／highlight、tooltip 的懸停顯示、連結滑鼠移入效果。
- 讀 `NpcTemplates` 的私有欄位 `templates` 用的是反射（既有測試也這樣做）；欄位改名的話，`NpcTemplateMarkup.Available` 會變成 false，面板自動退回舊排法。
- 真機項目（交給 Codex／dot）：開一個有 `<font fg>`、行內連結、`#312` 購買鈕的 NPC 頁，截圖對色（M34）、點行內連結與購買圖、捲動長頁。

## 產出路徑

- `engine/source/NativeUi/HyperTextLayout.cs`、`engine/source/NativeUi/HyperTextLayoutTests.cs`
- `engine/source/NativeUi/HyperTextPanel.cs`、`engine/source/NpcInteraction.cs`
- 本報告 `docs/reports/issue-850.md`；分支 `task/850`

## 給下一棒的提示

- **R9-I（私商查詢頁）**：`img action` 的點擊已經接上，`usershop-c.html` 的 `#0`／`userShop0／1` 會走行內連結的 id，送 C47 那段照 R9-I 的範圍做。
- **建議另開小任務**：改 `NpcUi.cs` 的 `NpcHyperTextView`／`QueryHyperTextView`，直接傳 `Document: w.LastHyperTextDocument`（要先把 NPC 頁和查詢頁的文件分開存）。這樣就可以拿掉以文字對照的登記表。
- M34 對色後只要改 `HyperTextColors.Order`，測試 `Colors` 的「預設 Rgb」也要跟著改。
- WinForms／web 備援面板沒有改，還是純文字加選項按鈕（`--classic-ui` 的入口保留）。
