# Issue #1038 — T453 準備器：文字類（b15-prep-text）

2026-11-13，runner：claude-local，分支 `task/1038`。依 `docs/design/asset-recipe.md`（1.1–1.3、3.1、4.6、第 6 節）與 `docs/reference/asset-recipes/text.md`（第 4、5 節）。

## 結論

recipe `text` 已完成：用 `TextArchive` 讀使用者的 `Text.idx`／`Text.pak`，以檔名找 entry，產生 `objects/names.json` 與 `dialogs/templates.json`。離線測試全部用合成的 Text 容器。CI 結果見文末「CI」。

它和 T451 的 `inventory` 一樣是**程式庫**：沒有 `--prepare-assets` 入口，沒有接啟動流程；把各 recipe 組成產品讀的那棵樹是 T467 的工作。

**T444 判 blocked 的部分：沒有。** T444（#1029）的 7 項全部 pass，文字類沒有需要跳過的東西。不屬於這個 recipe 的兩個檔（`dialogs/skills.json`、`magic/catalog.json`）依契約 3.6 改由執行期直讀，歸 T462。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/AssetPrep/TextRecipe.cs`（新） | `[AssetRecipe]` recipe `text`，項目 `names`、`templates` |
| `engine/source/AssetPrep/AssetPrepInput.cs` | Text 輸入：`TextEntries()`、`ReadTextEntry()`；`Verify()` 現在會驗 Text 來源 |
| `engine/source/AssetPrep/AssetPrepRunner.cs` | `AssetRecipeItemContext` 加 `TextEntries()`、`ReadTextEntry()`（讀到的 entry 會記進收據） |
| `engine/source/AssetPrep/TextRecipeModuleTests.cs`（新） | 產品內模組測試 `asset-prep-text`（只在記憶體內） |
| `engine/_integration/dev/asset-prep-tests/TextRecipeChecks.cs`（新） | DemoChecks 群組 `asset-prep-text`（寫檔、續跑、拒絕、既有讀取程式） |

沒有改 `Program.cs`、`DemoChecks.csproj`、任何註冊表、`build.yml`、`rebuild-lock.json`、`docs/REPORT.md`。PR 裡沒有任何模板或名稱：測試用的字串都是測試自編的。

### 行為

- **輸入**：只讀呼叫端給的絕對 `TW_CLIENT_ROOT` 下的 `Text.idx`／`Text.pak`。不 pin 整個容器，不用舊 offset 或 record 序號。
- **支援版本（錨點）**：`desc-c.tbl` 解碼後的 SHA-256（契約 1.2，等於 `TextArchive.DescSha256`）。**兩個項目都先核對它**；不符就停，訊息是「容器 Text 的 desc-c.tbl 與支援版本不符（解碼後 SHA-256 …）」。缺這一筆、或索引裡出現兩筆同名，同樣停並指出 `desc-c.tbl`。模板本身不設錨點（契約 1.2：不同版本的客戶端本來就會不同）。
- **`names`** → `objects/names.json`：code page 950 嚴格解碼，用 `\r\n` 切行，丟掉第一行（宣告筆數），其餘原樣保留（不去空白、不去空行）。序列化自己寫出：`", "` 分隔、非 ASCII 不跳脫、沒有 BOM、沒有換行，只跳脫 `"`、`\` 與控制字元。這是 `text.md` 第 4 節那個與基線逐 byte 相同的格式。
- **`templates`** → `dialogs/templates.json`：照 idx 順序取檔名結尾是 `-c.html`／`-c.htm`（不分大小寫）的 entry；嚴格 code page 950 解碼，無效就跳過該筆；去掉結尾 NUL；鍵＝檔名去掉副檔名與 `-c`，不分大小寫比較，保留第一次出現的寫法。同鍵內容相同略過；內容不同則整個鍵移除，之後同鍵的全部跳過。輸出順序是各鍵第一次出現的順序，用 `Utf8JsonWriter` 預設編碼器寫出（與 `JsonSerializer.Serialize(Dictionary<string,string>)` 的輸出相同：沒有空白、只有 ASCII、`\uXXXX` 大寫）。
- **code page 950**：用 `CodePagesEncodingProvider.Instance.GetEncoding(950, 例外式 fallback)` 直接取得，不依賴系統 code page，也不做全域註冊。
- **收據**：每個讀到的 entry 都記容器、檔名、offset、大小、解碼後 SHA-256。`templates` 項目的來源是錨點加上**每一筆** `*-c.html`／`htm`（含重複、無效、衝突的），因為任何一筆改變都可能改變輸出。metadata 記分類數字：`candidates`、`templates`、`duplicateIdentical`、`invalidEncoding`（與檔名清單）、`conflictKeys`（與鍵清單）、`conflictSkipped`；`names` 項目記 `declared` 與 `names`。T470 可以直接拿這些數字對 `text.md` 第 5 節的表。
- **續跑（契約 4.6 分給本張的缺口）**：`AssetPrepInput.Verify` 以前只認 Sprite 來源，Text 來源一律當成不符。現在 Text 來源以「同名、同 offset、同大小的索引列，解碼後 SHA 相同」驗證。所以中斷後重跑會沿用已完成的項目；來源沒變時重跑回 `UpToDate`；任何一筆 html 變了，`templates` 重做而 `names` 沿用。
- **輸出路徑**：依契約 4.5，路徑是 assets 樹的相對路徑（`objects/names.json`、`dialogs/templates.json`）。T451 的單一 recipe 發布會把它們放在 `engine/assets/text/` 下；組成 `engine/assets/objects/…` 與 `engine/assets/dialogs/…` 要等 T467 的多 recipe 發布。

### 需要知道的取捨

1. **`Coverage` 是 `Complete`**，指的是這個 recipe 擁有的兩個檔它都能產生。只要求一個項目時收據的 status 仍寫 `complete`（`requested` 欄會如實列出只要求了哪一個）。T467 的總流程請用 `TextRecipe.AllItems`。
2. **`TextRecipe(string descSha256)` 這個公開建構式只給測試用**（合成的名稱表不可能符合真實錨點）。自動發現與產品一律用無參數建構式，也就是真實錨點。測試裡有一項專門確認：產品的 recipe 對上合成的 Text 會被拒絕。
3. **分類數字的定義**：先解碼再看衝突，所以衝突鍵的無效 Big5 entry 算在 `invalidEncoding`。歷史產生器的判斷順序沒有讀到原始碼（研究目錄搜尋逾時），`text.md` 的 12／5／416 能不能逐項對上要等 T470；這只影響收據裡的統計，不影響輸出檔。
4. **收據會比較大**：真客戶端的 `templates` 項目有 10,264 筆來源，估計收據約 2–3 MB，整輪寫 3 次。沒有量過實際時間。
5. **`*-c.html` 的鍵是空字串的情況**（檔名剛好是 `-c.html`）照規則收下，沒有特別排除；`text.md` 沒有提到真客戶端有這種檔。

## 怎麼驗證

### 已驗證

- **本機編譯**：`python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile <主專案>/engine/NuGet.Config --output scratch/issue-1038/build` → `PASS dev`（lock 為 temporary，未寫回）。`dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` → 0 個錯誤，新檔沒有警告。本機沒有執行任何編譯出的程式。
- **`engine/tools/check-no-game-assets.py`**：pass（4678 個追蹤檔，0 錯誤）。
- **CI**：見文末。

測試內容（全部是程式合成的 Text 容器與 17 個合成 Sprite 容器）：

| 驗收條件 | 測試 |
|---|---|
| 正常產生 | 兩個引擎目錄（其一含中文與空格）的輸出與收據逐 byte 相同；`names.json` 與預期 bytes 完全相同；`templates.json` 與 `JsonSerializer` 對預期字典的輸出完全相同；收據記錄每筆來源與分類數字、不含路徑 |
| 同名 entry | 同鍵同內容（大小寫不同、結尾多 NUL）略過；同名不同內容整個鍵移除，之後的同名 entry 跳過；三筆同名 entry 在收據裡各有自己的 offset；`desc-c.tbl` 出現兩筆時拒絕 |
| 截斷與越界 | `Text.idx` 少 5 bytes、筆數大於實際列數、`Text.pak` 少 1 byte（最後一筆超出）、entry 的 offset＋size 超出 pak、缺 `Text.pak`、缺 `Text.idx`；每一種都指出容器 `Text`，而且沒有任何東西被發布 |
| 版本不在支援清單 | 產品的 recipe（真實錨點）對上合成 Text：兩個項目都拒絕，訊息含 `desc-c.tbl`、不含開發機路徑；發布之後名稱表換版，拒絕且舊的正式目錄不變 |
| 內容不合法 | 名稱表有被切斷的雙位元組字、第一行不是筆數；模板不是合法 code page 950（跳過該筆） |
| 既有讀取程式接受 | `NpcTemplates` 讀產生的 `templates.json`：不分大小寫的鍵、文字、`action` 選項；被移除與跳過的鍵查不到。`ObjectAssets` 讀產生的 `names.json`：索引＝名稱 ID、空名稱保留、超出範圍的 `$n` 維持原樣 |
| 續跑 | 第一個項目完成後中斷 → 重跑沿用 `names`、重做 `templates`；來源不變時 `UpToDate`；html 改了之後已發布的目錄算過期 |
| 其他 | 不認得的項目（`skills`、`magic`、路徑穿越等）拒絕；只要求一個項目只產生那個檔；整輪前後合成客戶端目錄逐檔 SHA 相同 |

### 推測

- .NET 的 code page 950 表在 CI 的 Windows 與 Linux 上相同（表在 `System.Text.Encoding.CodePages` 內，不取自作業系統）。
- 真客戶端的 `names.json` 會與基線逐 byte 相同：規則照 `text.md` 第 4 節，但那一節的驗證是 Python 原型做的，C# 沒有對真資料跑過。

### 待驗證（交給 T470）

- **真客戶端全量比對**：`names.json` 對基線 B（SHA `40b14f8d…`）；`templates.json` 鍵集合＝基線 8,896＋`lyra_y01`、8,720 筆相同、176 筆只差圖片編號；C# 輸出的 SHA 是否等於 `text.md` 記的 Python 模擬值 `6a7c85e4…`；收據的分類數字對 `text.md` 第 5 節。
- **真客戶端現在跑不起來，原因不在本張**：`AssetPrepInput.Open` 仍會先驗 17 個 Sprite 容器的整包 pin，而本機客戶端的 `Sprite03`–`Sprite14` 已與 pin 不符（契約 1.1 第 3 點、D1）。這要等 T454 把 Sprite 輸入改成三層驗證。文字 recipe 本身不讀 Sprite。
- 真客戶端的耗時與收據大小。
- 602／603 的買賣按鈕畫面（契約 D2a，T474）。

## 產出路徑

- `engine/source/AssetPrep/TextRecipe.cs`、`TextRecipeModuleTests.cs`
- `engine/source/AssetPrep/AssetPrepInput.cs`、`AssetPrepRunner.cs`（修改）
- `engine/_integration/dev/asset-prep-tests/TextRecipeChecks.cs`
- `docs/reports/issue-1038.md`（本檔）
- 本機建置紀錄：`scratch/issue-1038/build/`（不進 git）

## 給下一棒的提示

- **T454**：改 `AssetPrepInput.Open` 的 Sprite 驗證時，Text 這一側（`OpenText`、`TextEntries`、`ReadTextEntry`、`VerifyText`）不需要動。建議順便讓「不讀 Sprite 的 recipe」不必開 Sprite 容器，否則文字 recipe 仍要求 17 個 Sprite 檔都在。兩張都會改 `AssetPrepInput.cs`，同步 main 時自己解衝突。
- **T467**：
  - 用 `TextRecipe.AllItems` 要求兩個項目。
  - 收據的 `inputs` 目前只有 Sprite 容器；總收據要的 Text idx 大小／SHA 與錨點實際 SHA（契約 4.4）還沒有欄位。錨點的實際 SHA 已經在 `text` 收據裡（`desc-c.tbl` 那筆來源的 `sha256`）。
  - 單一 recipe 發布的位置是 `engine/assets/text/{objects,dialogs}/…`，不是產品讀的位置。
- **T462**：`spelldesc-c.tbl` 是 `Single()` 可以找到的單筆，直接用 `context.ReadText(name, sha)` 或執行期的 `TextArchive` 即可，不需要 `TextEntries()`。
- **T470**：`text` 收據 `templates` 項目的 metadata 有 `conflictKeyList` 與 `invalidEncodingEntries`，可以直接對 `text.md` 列的 5 個衝突鍵與 12 筆無效 Big5。
- 其他要讀很多 Text entry 的 recipe：用 `context.TextEntries()` 取索引列，再 `context.ReadTextEntry(row)`。同名列很多，不要用名稱當唯一鍵。
- 測試要在本機重跑請交給 Codex：`DemoChecks … <repo> <out.json> asset-prep-text`。每次用新的 `scratch/issue-1038/run-<時間>/`，不做遞迴刪除，不建 junction。

## CI

（push 後補上）
