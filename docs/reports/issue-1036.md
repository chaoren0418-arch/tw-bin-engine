# Issue #1036 — T451 準備器核心與 inventory TBT 圖示垂直切片（b15-prep-core）

2026-11-13，runner：claude-local，分支 `task/1036`，PR #1066。依 `docs/design/portable-delivery.md`「本機生成與啟動」。

## 結論

準備器核心與第一個 recipe（`inventory`，TBT→PNG）已完成，離線測試在 CI 全數通過。它目前是**程式庫**：沒有 `--prepare-assets` 入口、沒有接 launcher，收據只會寫 `partial`，不存在 ready 狀態。這是設計文件的要求（完整分類通過前不接正常啟動），不是遺漏。

## 做了什麼

新增 `engine/source/AssetPrep/`（命名空間 `P06.AssetPrep`）：

| 檔案 | 內容 |
|---|---|
| `AssetPrepCore.cs` | `IAssetRecipe`、`[AssetRecipe]`、`AssetRecipeDiscovery`（自動發現）、`AssetPrepException`／`AssetPrepFailure`、`AssetPrepLayout`、`AssetPrepPaths`（路徑規則、junction 檢查、不跟隨連結的刪除） |
| `AssetPrepInput.cs` | 唯讀輸入：絕對 `TW_CLIENT_ROOT`、17 個 Sprite 容器 pin、Text entry pin |
| `AssetPrepRunner.cs` | 收據、staging、單一 writer 鎖、續跑、驗證、發布 |
| `InventoryIconRecipe.cs` | `inventory` recipe 與 `PngWriter` |
| `AssetPrepModuleTests.cs` | 產品內 `[WorldModuleTest("asset-prep")]`（只在記憶體內） |

另有 `engine/_integration/dev/asset-prep-tests/AssetPrepChecks.cs`（DemoChecks 群組），以及 `docs/design/m6-contract.md`「自動發現」表新增一列。沒有改 `Program.cs`、`DemoChecks.csproj`、任何註冊表、`build.yml` 與 `rebuild-lock.json`。

### 行為摘要

- **註冊**：類別標 `[AssetRecipe]` 並實作 `IAssetRecipe` 即被 `AssetRecipeDiscovery.Product` 找到。不合法的註冊（沒實作介面、沒有無參數建構式、id 不合法或重複）不生效並列在 `Errors`，模組測試要求它是空的。
- **輸入**：`TW_CLIENT_ROOT` 必須由呼叫端明確給絕對路徑；沒給、相對路徑、目錄不存在都拒絕，沒有預設值，也不讀 `UiResourcePins.ClientRoot` 的開發機路徑。開啟時每個 Sprite 容器都要有 idx 與 pak、pak 大小符合 pin、idx 的 SHA-256 與結構由 `ClientArchive` 驗；pak 的 SHA-256 由 `ClientArchive` 在第一次讀該容器時驗。Text 走 `TextArchive`，依名稱找 entry 並核對解碼後 SHA-256（不沿用舊 offset）。每一種拒絕都帶容器名稱（`AssetPrepException.Container`）。
- **輸出目錄**：
  - `engine/runtime/assets-staging/<recipe-id>/`：進行中，內含 `prep-receipt.json`（`in-progress`）
  - `engine/runtime/assets-staging/<recipe-id>.lock`：writer 以 `FileShare.None` 持有
  - `engine/runtime/assets-staging/<recipe-id>.previous/`：發布時暫放被換下的舊目錄
  - `engine/assets/<recipe-id>/`：正式目錄，內含 `prep-receipt.json`（`partial` 或 `complete`）
- **收據**（`tw-asset-prep-receipt-v1`）：工具版本 `tw-asset-prep/1`、recipe id 與版本、17 個輸入容器的 idx／pak SHA-256 與 pak 大小（另標記哪些 pak 這次整檔雜湊過）、要求的項目、每個完成項目的來源 entry（容器、名稱、offset、大小、SHA-256）、輸出檔（相對路徑、大小、SHA-256）與 metadata。收據不含路徑與時間，所以相同輸入在任何目錄產生的收據逐 byte 相同。
- **發布**：所有項目完成後，把最終收據寫回、從磁碟讀回比對，再逐檔核對 staging（不得多檔、少檔、大小或 SHA 不符），通過才改名。已有正式目錄時先改名到 `.previous`，新目錄就位後才刪舊的；第二次改名失敗會把舊的放回去。兩次改名之間若程序中斷，下一次執行先把 `.previous` 放回正式位置。
- **續跑**：重跑時先驗輸入容器，再對 staging 收據裡的每個完成項目驗輸出檔（大小、SHA-256）與來源（重新讀 entry 比對 offset、大小、SHA-256）；都相符才沿用，其餘重做。工具版本、recipe 版本或輸入 pin 不同時整批不沿用。收據沒記錄的檔案與空目錄會清掉。
- **已是最新**：正式目錄的收據與本次要求完全相同、檔案與來源都驗過時，回 `UpToDate`，不重寫。
- **junction／symlink**：staging、`.previous`、正式目錄與鎖定檔的路徑，從本身一路到磁碟根目錄，任何一層是 reparse point 就拒絕，而且在建立任何目錄之前就檢查。目錄內部若出現連結，列舉與刪除都會停下來，不跟進去也不刪另一端。
- **路徑穿越**：recipe id 只允許小寫英數與單一連字號；項目只允許短 token；recipe 的輸出路徑只允許英數、`.`、`_`、`-` 的區段，拒絕絕對路徑、磁碟或資料流冒號、`..`、裝置名稱與收據檔名，組合後再確認仍在自己的目錄內。引擎目錄位於原客戶端內也拒絕。
- **`inventory` recipe**：項目是圖示編號（1–65535 的十進位字串）。查找順序用 `BuffIconCatalog.Candidates`（`<n>c.tbt` 再 `<n>.tbt`，先雜湊到的 Sprite 容器再 `Sprite`），解碼用 `TbtImage.Decode`，也就是 `ItemIconSource` 對本機客戶端用的同一條路。輸出 `<icon>.png`，收據 metadata 記 `offsetX`、`offsetY`、`width`、`height`、`rgbaSha256`。`Coverage` 是 `Partial`。
- **PNG**：`PngWriter` 自己寫 8-bit RGBA、不壓縮（deflate stored block），不依賴 GDI+ 或 zlib 版本，同樣的像素在任何機器上得到相同的 bytes。

### 與任務文字不同或需要知道的取捨

1. **沒有直接呼叫 `ItemIconSource`**。`ItemIconSource` 回傳的是 `Bitmap`，拿不到來源 entry 的 bytes 來記 SHA，而且它帶有參考客戶端（kor25）fallback。準備器改用它底下的同一組查找與解碼函式，並且不啟用 fallback：本機客戶端沒有的圖示會報錯並列出找過的位置。測試逐像素比對了兩者的結果與 offset。
2. **offset 記在收據**，沒有另外輸出素材表。之後的消費端要從 `assets/inventory/prep-receipt.json` 讀。
3. **在開發機上會被拒絕**：`engine/assets` 目前是指向 `releases/r29/assets` 的 junction，準備器會拒絕寫入。這正是要保護封存的行為；要在開發機試跑，引擎根目錄必須是沒有 junction 的普通目錄。
4. **祖先檢查到磁碟根目錄**。使用者若把引擎解壓在經由 junction 到達的路徑下會被拒絕。這是照任務文字「任何祖先」實作，是否放寬需要裁定。
5. **替換正式目錄不是單一原子操作**（Windows 沒有目錄交換），是兩次改名加上中斷復原；首次發布則是一次改名。
6. **輸出目前整檔在記憶體**（`context.Write(path,bytes)`），對圖示沒問題，地圖這類大檔的 recipe 需要串流寫入介面。
7. 測試掛在 CI 已有的 `native-ui-resources` 步驟下，所以沒有改 `build.yml`；單獨執行用群組名 `asset-prep`。

## 怎麼驗證

### 已驗證

- **本機編譯**：`python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile <主專案>/engine/NuGet.Config --output scratch/issue-1036/build` → `PASS dev`（lock 為 temporary，未寫回）；`dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` 0 錯誤，新檔沒有警告。本機沒有執行任何編譯出的程式。
- **`check-no-game-assets.py`**：pass（4658 個追蹤檔，0 錯誤）。PR 沒有 PNG、素材表或新的 embedded resource。
- **CI（PR #1066，commit `e772e015`）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全綠。
  - 產品內模組測試 `asset-prep`：`pass: true`，75 項檢查。
  - DemoChecks `native-ui-resources` 步驟：`PASS 829 DEMO checks`；同一步驟在前一個 PR（task/1035）是 596，多出的 233 項是本任務的檢查。
- CI 上通過的驗收項目（全部用程式合成的 idx／pak／TBT／Text）：

| 驗收條件 | 測試內容 |
|---|---|
| 兩個輸出目錄的 RGBA 與 offset 相同 | 兩個引擎目錄（其一含中文與空格）；PNG 以 GDI+ 解回的 RGBA 等於 TBT 解碼結果；PNG 與收據逐 byte 相同；像素、offset、來源與 `ItemIconSource` 一致 |
| 缺檔 | 缺 pak、缺 idx、缺 Text.pak、圖示不在任何容器 |
| 錯 pin | idx SHA、pak SHA、pak 大小；真實版本的 pin 對上合成客戶端（指出 `Sprite.pak`，訊息沒有開發機路徑）；Text entry SHA |
| 截斷 | pak 少 1 byte、idx 少 5 bytes（pin 不變與 pin 跟著改兩種）、TBT 截斷、TBT 多出尾端、Text 索引截斷 |
| 越界 | entry 超過 pak 範圍、entry 超過讀取上限、TBT 一列超出寬度 |
| staging 未完成不發布 | 中途丟例外、中途取消、開始前取消、圖示缺漏、最後驗證不符，正式目錄都不出現或維持原樣 |
| 第二個 writer 被拒 | 第一個 writer 執行中再起一次；鎖被持有時執行；結束後鎖釋放 |
| 中斷重試 | 沿用完好的項目、重做被改壞的、清掉多餘檔案與目錄；結果與未中斷的執行逐 byte 相同；輸入版本不同時不沿用；發布中斷後復原 |
| junction 拒絕 | `engine/assets`、正式目錄、`engine/runtime`、staging 本身、引擎根目錄經 junction 到達、staging 內、正式目錄內、symlink；連結另一端都沒被寫入或刪除 |
| 路徑穿越拒絕 | 15 種輸出路徑、recipe id、項目字串、引擎目錄在客戶端內 |
| 原客戶端唯讀 | 整輪前後客戶端目錄逐檔 SHA 相同 |

### 推測

- symlink 案例在 CI 有實際執行：測試在建立不了 symlink 時會往 stderr 印略過訊息，CI 日誌裡沒有這一行。沒有另外的正面證據。
- 真實客戶端的圖示會得到與現有 `assets/inventory/*.png` 相同的像素：解碼函式相同，但沒有實際比對過。

### 待驗證

- **真實客戶端**：沒有用 `D:\lineage開發\0-客戶端` 跑過（本機不得執行編譯出的程式，CI 沒有客戶端）。首次真跑要整檔雜湊用到的 Sprite pak（單檔約 0.4–1.9 GB），耗時未量。
- **與現有 20 個 inventory PNG 的像素比對**，以及產品實際需要哪些圖示編號（清冊未閉合，所以是 `partial`）。
- **跨程序的 writer 互斥**：測試在同一個程序內開第二個 handle；兩個獨立程序的情況沒有測。
- **磁碟不足**與寫入中途被強制結束程序：只測了例外與取消，沒有真的砍程序或塞滿磁碟。
- **同磁碟檢查**：staging 與正式目錄都在引擎根目錄下，檢查實際上不會失敗，沒有測試能觸發它。
- 非 Windows 的行為（CI 的這組測試只在 windows-latest 跑）。

## 產出路徑

- `engine/source/AssetPrep/`（5 個檔案）
- `engine/_integration/dev/asset-prep-tests/AssetPrepChecks.cs`
- `docs/design/m6-contract.md`（自動發現表一列）
- `docs/reports/issue-1036.md`（本檔）
- 本機建置紀錄：`scratch/issue-1036/build/`（不進 git）

## 給下一棒的提示

- **新增類別**：照 `InventoryIconRecipe.cs` 寫一個 `[AssetRecipe]` 類別。讀取一律走 `AssetRecipeItemContext`（`TryReadSprite`、`ReadText`），來源才會進收據、續跑才驗得到；寫入走 `context.Write`。輸出若在輸入不變時改變，要把 `Version` 加一。
- **Tile 容器與 map／Sound 原檔**還沒有輸入介面，`AssetPrepInput` 只有 Sprite 與 Text。續跑的來源驗證（`AssetPrepInput.Verify`）目前只認 Sprite 來源，Text 來源會被當成不符而重做。
- **接啟動流程**（`--prepare-assets`、launcher、`--check-only` 的未準備狀態）是後續任務；接之前要先決定 `engine/assets` 在開發機上是 junction 的問題（見上面取捨 3），以及祖先檢查的範圍（取捨 4）。
- **消費端改讀本機產出**：`ItemIconSource` 等目前仍在執行期直接讀 pak，沒有改。
- 呼叫方式：`AssetPrepRunner.Product().Run(new AssetPrepRequest(clientRoot, engineRoot, "inventory", icons))`；`AssetPrepInput.ClientRootFromEnvironment()` 讀環境變數，沒設就拒絕。
- 測試要在本機重跑請交給 Codex：`DemoChecks … <repo> <out.json> asset-prep`。測試每次用新的 `scratch/issue-1036/run-<時間>/`，不做遞迴刪除；它會用 `cmd /c mklink /J` 建 junction，結束時逐一移除。
