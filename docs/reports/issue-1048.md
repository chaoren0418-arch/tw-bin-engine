# Issue #1048 報告：T463 移除已知例外——兩個 pickup 內嵌 JSON 改由準備器產生

日期：2026-11-13。執行者：本機 Claude（claude-local）。分支 `task/1048`，PR #1085。
依據：`docs/reference/asset-recipes/sprites.md` §5；`docs/design/asset-recipe.md` 第 2 節第 20 列、3.2、4.3、6（順序 3）；`docs/reports/issue-102.md` pickup 兩列。

## 結論

- 新 recipe `pickup` 產生 `pickup/pickup-own-base15-gfx0-weapon0.json` 與 `pickup/pickup-own-base15-profile-extension.json`。規則照 `sprites.md` §5，**沒有**把原 JSON 複製進 assets。
- `PickupAssets.cs` 改讀本機 assets 樹（以該樹的 `integrity.json` 核對）；兩個 EmbeddedResource 與 `engine/source/Resources/` 下兩個檔已刪除；`b15Exceptions` 拿掉這兩列。
- `check-no-game-assets.py`：pass，EmbeddedResource 宣告 4 → 2。
- **例外數目**：`b15Exceptions` 由 7 列變成 **5 列**（不是 Issue 寫的「剩 7 個」）。7 是開工前的數字（`asset-recipe.md` 第 383 行：「現況 7 列……剩 pickup 2 列」），扣掉 pickup 兩列就是 5：`OriginalExpTable.cs`、`ground-catalog.json`、兩個 metadata vectors、combat-sfx `expected-events.json`。`binaryExceptions`（19 列）沒動。
- 本機只編譯；測試交給 CI（2026-11-09 本機執行限制）。

## 做了什麼

### 1. recipe `pickup`（`engine/source/AssetPrep/PickupRecipe.cs`）

- 順序 3，`Requires = [objects, players]`，Coverage Partial，Version 1。項目只有一個：`pickup`（一個項目寫兩個檔，基底檔與 extension 的 `requiresResource` SHA 一定出自同一批來源；續跑時不會出現兩檔不配的情況）。
- **適用清單跟著 players 走**（`sprites.md` §5「42 筆 profile 的適用清單跟 players 清冊走」）：讀已發布的 `players/manifest.json` 的 `profiles[]`（gfx、weapon、path、sha256、sprite）。程式裡沒有任何 gfx／weapon 表。真客戶端上 players manifest 是 42 筆可用 profile，正好就是原 extension 的 41 筆加上基底 (0,0)（本機以 Python 只讀比對 r29 基線確認：集合相同、每筆 path 與 SHA 相同）。
- 每個 gfx 一個 group：
  - list.spz 的動作 15，必須 8 方向、恰好 4 步、ticks 為 1, 1, 4, 4（產品 `PickupAssets.ReadBody` 的步序契約）；
  - 第 0 步 `storage: "base-profile"`，第 1–3 步 `"embedded"`；`metadataIndex` 0–3；`ticks`、`events` 照 token；
  - `frames`：方向 0–7，每方向第 1、2、3 步；block 只留 `a, b, z, source_order, pixels`；空幀照 §8.2 保留 bounds、沒有 blocks；
  - SPR 取用照 §3.3（主包優先、替代包唯一）。
- 每筆 profile 讀它的 body profile 檔（`objects/0*.json` 或 `players/*.json`，經 `ReadPrior` 驗收據），核對 SHA 等於 manifest、gfx／sprite 相符，並確認 8 個方向的第 0 步幀都在 body 的 frames 裡。
- 不合規時：該 gfx（動作 15 缺、非 8 方向、ticks 不是 1,1,4,4、內嵌幀重複或與第 0 步重複）或該 profile（body 沒有第 0 步幀）**不收**，原因寫進收據 `skipped:<gfx>-<weapon>`；產品對它不顯示拾取動畫（與其他不支援的姿勢相同）。基底 gfx 0／weapon 0 做不出來 → recipe 停止（Unavailable）；players 沒有 0-0 → Prerequisite。
- 輸出：
  - 基底檔：`schema`（沿用 `pickup-own-base15-gfx0-weapon0-v1`）、`gfx` 0、`weapon` 0、`sprite`（list.spz）、`baseProfilePath` `objects/0.json`、`baseProfileSha256`（players manifest 那一列）、`frameOrder` [1,2,3,0]、`sequences`、`frames`。
  - extension：`schema`（沿用 `pickup-own-base15-profile-extension-v1`）、`frameOrder`、`requiresResource {path, sha256}`、`groups[]`（gfx ≠ 0）、`profiles[]`（除 0-0 以外的全部，依 gfx、weapon 排；`bodySource` gfx 0 為 `r34-base-resource`，其餘 `extension`）。
  - 緊湊 JSON（`SprJson.Write`）；不含路徑、`source_offset`、`block_index`。
- `PreheatList.Items("pickup")` = `["pickup"]`。

### 2. 讀取點（同一個 PR 改掉，`asset-recipe.md` 4.3 規則）

| 檔案 | 改了什麼 |
|---|---|
| `PickupAssets.cs` | 改為 `public`。`Load(assetsRoot)` 從 `integrity.json` 取兩檔的 size／SHA 核對後載入；沒有列入 → `InvalidDataException("Pickup assets not prepared")`。拿掉寫死的檔案 SHA、大小、`baseProfileSha256` 常數、sprite 3225、`Supports` 的 13 個 gfx 表、`13 groups／42 profiles` 的固定數量（改成資料驅動，`Supports` 變成實例方法）。保留：gfx 0／weapon 0 與 `objects/0.json`、8 方向、4 步、metadataIndex、ticks 1,1,4,4、storage、每 group 24 幀、frameOrder [1,2,3,0]、合成 [第 0 步, 1, 2, 3]、執行期以 `PlayerProfiles` 的 path＋SHA 核對 profile。extension 的 `requiresResource` 由 `logicalName` 改成 `path`（見下方「與基線的差異」）。重複的幀／group／profile 改丟 `InvalidDataException`（原本 `Dictionary.Add` 會丟 `ArgumentException`，不在呈現層的 catch 範圍） |
| `ObjectAssets.cs` | 由自己的 assets 根目錄載入，只載一次；失敗也快取（r29 樹沒有 `pickup/*` 時不會每幀重讀 `integrity.json`）。新增 `PickupSupports` |
| `PickupPresentation.cs` | `PickupGuard` 拿掉靜態 `Supports`；render context 先問 `catalog.PickupSupports`，不支援 → `Blocked = "unsupported-profile/speed"`（理由字串不變）；載入失敗照舊落在 `asset-integrity/key` |
| `OfflineClient.csproj` | 拿掉兩個 EmbeddedResource |
| `engine/source/Resources/pickup-own-*.json` | 刪除（`git rm`） |
| `engine/tools/game-assets-allowlist.json` | `b15Exceptions` 拿掉兩列（7 → 5） |
| `engine/tools/check-no-game-assets.py` | `RESOURCES` 拿掉兩列（4 → 2），訊息改成依實際數量 |
| `engine/tools/ResourceChecks/Program.cs` | `P11.ClientEngine.dll` 允許的 ManifestResource 只剩 `GroundCatalog.json`（CI 的 metadata-only 檢查） |

沒有改：`engine/_integration/dev/pe_resource.py`（還寫著舊 pickup 資源名稱與 SHA，但目前沒有任何建置步驟呼叫 `verify_resource`，只列在 `rebuild.py` 的 BUILD_INPUTS；改它會動到 lock 鎖住的建置輸入，留給調度器決定，見「給下一棒的提示」）；r34／r35 封存腳本；`rebuild-lock.json`。

### 3. 與基線的差異（T470 比對時要略過）

- extension 的 `requiresResource`：基線 `{logicalName, sha256}` → 新 `{path, sha256}`；sha256 是新基底檔的 SHA。schema 字串沿用 v1（`asset-recipe.md` 1.3 第 4 點：只有產品讀的欄位意義變了；`logicalName` 指的內嵌資源已不存在）。若主控認為要升 v2，產品與 recipe 各改一個常數即可。
- `baseProfileSha256`：等於新產生的 `objects/0.json`、`players/*.json` 的 SHA，不會等於基線值（基線 JSON 的序列化不同，`sprites.md` §7）。
- 鍵順序與空白不同（基線是 Python `sort_keys`）；S 比對不受影響。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`（0 警告、0 錯誤）；`-o build/client-product-dev` 後 `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`（0 錯誤，3 個既有警告不在本張檔案）。**沒有在本機執行任何編譯出來的程式**。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：`{"status": "pass", "trackedFiles": 4704, "errors": 0, "embeddedResourceDeclarations": 2}`。
- 新測試 `engine/_integration/dev/asset-prep-tests/PickupRecipeChecks.cs`：`[CheckGroup("asset-prep-pickup",Order=1)]`，同時掛 CI 已在跑的 `native-ui-resources`（Order 64）與預設全套。全部合成（list.spz、Text、17 個 Sprite 容器、每個 SPR），寫在 `scratch/issue-1048/run-<時間>/`：
  - 自動發現、Requires、預熱清單；
  - 先後錯誤（沒 objects／players、players 沒有 0-0）→ Prerequisite 且不發布；不合法項目 → InvalidRequest；
  - 兩個目錄（含中文與空白）輸出與收據逐 byte 相同；只寫兩個檔；不含路徑、`source_offset`、`block_index`、`logicalName`；
  - **追加幀的產生**：基底 frames 順序 `88:0 88:1 88:2 89:0 …`（方向 0–7 × 第 1–3 步）；sequences 8 方向 × 4 步，第 0 步 key `d:0`、base-profile，ticks 1,1,4,4，events `<206` 照抄；空幀保留 bounds；extension 只收 gfx 61（第 0 步取 body 的 `8+d:0`），37（ticks 不合）、48（沒有動作 15）、1186（第 0 步幀不在 body）被跳過並記原因；profiles 照 players manifest；替代包（Sprite06）的 SPR 也收；
  - **產品讀取與合成順序**：組一棵附 `integrity.json` 的樹，`PickupAssets.Load` 載入（2 groups、4 profiles，`Supports` 由資料決定）；4 個 profile × 8 方向，`For` 回傳 `[body 的第 0 步幀（同一個物件）, 內嵌 1, 2, 3]`，像素與合成 SPR 相同；錯誤方向、profile 缺或 SHA 不同、不支援姿勢 → null；
  - 拒收：沒有 `pickup/*` 的樹（如 r29 基線）、檔案被改（integrity）、extension 的 requiresResource SHA 不是基底、基底換了 → `InvalidDataException`；
  - 續跑：來源不變 → UpToDate；players 少一個姿勢重產後 pickup 重做、適用清單跟著變；原客戶端目錄整輪不變。
- 既有 pickup 測試（`pickup-tests/PickupChecks.cs` 的 `pickup` 群組、`GroundTests` 的 S158/15）原本就不讀內嵌資源（`PickupRenderContext` 未綁定），本張沒有改它們；`PickupGuard` 不再呼叫靜態表，S158/15 的路徑仍落在 `renderer/assets-unavailable`。CI 結果見下節。

### CI

- PR #1085 第一次 CI（commit `28c80c62`／run 43441800710、43441800850）：`dotnet`、`map-continuity`、`npc-offline` 全部 pass。
  - `map-continuity` 的「Native UI resource layer」步驟（`native-ui-resources` 群組，本張測試掛在這裡）：`PASS 1365 DEMO checks`；同一時間 main（run 43441115618）同一步驟是 1292，多出的 73 項是本張的 `PK` 檢查。該步驟的 JSON 沒有上傳成 artifact，所以逐項標籤只能從數量對照，沒有逐條取回。
  - 同一 run 的 `World module extension tests`（`--module-tests`）、`pickup`、`ground` 相關步驟都 pass；`Check compiled EmbeddedResource names (metadata only)`（`tools/ResourceChecks`）pass，表示產品 DLL 只剩 `GroundCatalog.json` 一個資源。
- 之後同步 main（合併 `ef657a60`，無衝突）、本機重新編譯 0 錯誤、閘門 pass（4,711 個追蹤檔）；同步後的 CI 結果以 PR 最新 commit 為準。

## 未驗證（待驗證）

- **真客戶端 312 幀（13 groups、200,628 pixels）與 42 筆 profile 對基線的比對**：T470。本機不能執行編譯出來的程式；規則正確性的證據是 T449 的 Python 原型（`sprites.md` §5 全量相符）與本張的合成測試。
- **開發機的拾取動畫**：開發機 `engine/assets` 仍是 r29 junction，裡面沒有 `pickup/*`，在 T467 產生 v2 樹之前，`PickupAssets.Load` 會拒收，自己的拾取動畫被抑制（理由 `asset-integrity/key`），拾取本身（C112 送出、轉向、節流）不受影響。真機拾取動畫的驗收要等 T467／T470，之後 T474。
- `pe_resource.py` 沒有改（理由見上）。

## 產出路徑

- 程式：`engine/source/AssetPrep/PickupRecipe.cs`（新增）；修改 `engine/source/PickupAssets.cs`、`ObjectAssets.cs`、`PickupPresentation.cs`、`AssetPrep/PreheatList.cs`、`OfflineClient.csproj`。
- 刪除：`engine/source/Resources/pickup-own-base15-gfx0-weapon0.json`、`pickup-own-base15-profile-extension.json`。
- 閘門：`engine/tools/game-assets-allowlist.json`、`check-no-game-assets.py`、`tools/ResourceChecks/Program.cs`。
- 測試：`engine/_integration/dev/asset-prep-tests/PickupRecipeChecks.cs`。
- 報告：本檔。沒有改 `rebuild-lock.json`、`docs/REPORT.md`；沒有提交任何像素或客戶端資料。

## 給下一棒的提示

- **T467 總流程**：recipe id `pickup`，排在 `players`、`objects` 之後；輸出已是 assets 樹相對路徑 `pickup/…`。**兩檔一定要列進 v2 `integrity.json`**（`PickupAssets.Load` 從那裡取 size／SHA，沒列就拒收）。
- **T470**：S 比對略過 extension 的 `requiresResource`（形狀改了）與兩檔的 `baseProfileSha256`（跟著新 body 檔）；其餘（13 groups、312 幀、104 筆序列、42 筆 profile 的 gfx／weapon／path／sprite／sequenceGroup／bodySource）應全同。若 players 的可用 profile 與基線不同，pickup 會跟著不同，這是預期行為。
- **T471**：`b15Exceptions` 現在 5 列。
- `engine/_integration/dev/pe_resource.py` 的 `LOGICAL`／`RESOURCE_SHA` 指向已移除的資源；目前沒有呼叫者。要刪或改，請在可以重產 lock 的流程裡處理。
