# Issue #1040 — T455 準備器：players、shops、shadows、doors、ground、encountered-monsters、world-appearances（b15-prep-sprites-b）

2026-11-13，runner：claude-local，分支 `task/1040`，PR #1079。依 `docs/design/asset-recipe.md`（1.3、3.2、4.6、第 5、6 節、第 9 節 T455 列）與 `docs/reference/asset-recipes/sprites.md`（§4.2、§4.4–§4.7、§6）。

## 結論

T454 的 SPR 共用層上新增五個 recipe：`players`、`shops`、`doors`、`appearance-lists`（產生 `encountered-monsters.json` 與 `world-appearances.json`）、`shadows`。另外補上契約 4.6 指定給 T455 的核心缺口「讀先前 recipe 的輸出並記進收據」，以及第 5 節的預熱清單檔。產品與 DemoChecks 本機**編譯通過**（0 錯誤；DemoChecks 只有 3 個既有警告）。依本機執行限制，本機**沒有執行**任何測試，結果以 PR 的 CI 為準（見文末「CI」）。

和 T451／T453／T454 一樣，這些 recipe 是**程式庫**：沒有接啟動流程；把各 recipe 組成產品讀的那棵樹是 T467 的工作。

與任務原文不同的兩點（契約第 9 節優先於任務原文）：

- **`ground` 不產生**（契約 3.2、第 6 節、第 9 節 T455 列）。開工時確認的讀取點：`GroundRendering.cs:4`（`GroundSpriteReader.Read`）與 `WebPanel.cs:74`（`GroundSpriteReader.Image`）都直接讀客戶端；`engine/` 下沒有任何程式開 `assets/ground/*`（`ground/36`、`ground/66` 只出現在 r29–r35 的封存清單與 `engine/web/ground` 的舊 pin 檔）。所以沒有要改接的讀取點，也沒有 ground recipe。
- **gfx 範圍**照契約第 5 節「遇到再準備；首次只預熱」：每個 recipe 以項目為單位，可以任意單獨準備；預熱清單寫在 `PreheatList.cs`，成員等於 r29 基線七份清冊（D5 暫行）。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/AssetPrep/SprRecipesB.cs`（新） | `[AssetRecipe]` `players`、`shops`、`doors`、`appearance-lists`、`shadows`；共用的 `SprManifest`（manifest 列記在收據 metadata、unavailable 處理、讀前一個 recipe 的外觀檔） |
| `engine/source/AssetPrep/PreheatList.cs`（新） | 預熱清單：只有整數編號（57 個玩家姿勢、15＋19＋6 個物件與怪物、11 個商店、17 個陰影、1 個門、8 個魔法、1 個飛行物），`Items(recipeId)` 給出每個 recipe 的請求 |
| `engine/source/AssetPrep/AssetPrepCore.cs` | 新失敗種類 `Prerequisite`（先後順序錯誤）；`IAssetRecipe.Requires`（宣告要讀哪些 recipe 的輸出，預設空） |
| `engine/source/AssetPrep/AssetPrepRunner.cs` | `AssetPriorOutputs`：開工前檢查每個 `Requires` 都已發布（收據為 final），否則什麼都不產生就以 `Prerequisite` 停下；`ReadPrior` 只讀收據有列、大小與 SHA-256 都符合的檔，並記成來源（容器 `assets/<recipe-id>`、entry＝相對路徑）；續跑與「已是最新」時重驗這些來源。`AssetRecipeItemContext` 加 `ReadPrior`、`PriorOutputs`、`Completed`（manifest 項目用本輪已完成項目的收據紀錄組出來） |
| `engine/source/AssetPrep/SprShared.cs` | `SprFile.FirstPixelOutsideBounds`（players 的 unavailable 原因要寫出像素座標）；`PixelsInsideBounds` 改用它，行為不變 |
| `engine/source/PlayerProfiles.cs` | 把讀 `players/manifest.json` 的那段抽成 `PlayerProfiles.LoadManifest(root)`（行為不變；執行期仍是同一個 `Lazy`），讓測試能用產品本身的讀法 |
| `engine/_integration/dev/asset-prep-tests/SprRecipeBChecks.cs`（新） | DemoChecks 群組 `asset-prep-spr-b`（同時掛在 CI 已在跑的 `native-ui-resources`，Order 63） |

沒有改 `GroundSpriteReader`、`ObjectAssets.cs`、`PlayerShadows.cs`、`Program.cs`、`DemoChecks.csproj`、註冊表、`build.yml`、`rebuild-lock.json`、`docs/REPORT.md`。PR 裡沒有任何幀或像素：測試的 SPR 是依編號算出的測試值。

### 先後（契約第 6 節）

| 順序 | recipe | `Requires` | 讀什麼 |
|---:|---|---|---|
| 1 | `shops`、`doors` | — | 只讀客戶端 |
| 2 | `players` | `objects` | gfx 0 的 0／4／20／50 四個姿勢指向 `objects/0.json`、`objects/0-weapon-<n>.json`（標 `inherited`），核對 gfx、weapon、sprite、stepMs、entryHash 後記下它的 SHA 與幀數 |
| 2 | `appearance-lists` | `objects` | 每個成員的 `objects/<gfx>.json`（world 成員在 objects 有產生 `objects/world-<gfx>.json` 時用它） |
| 3 | `shadows` | `objects`、`players` | 該 gfx 的全部 body profile：`objects/<gfx>.json`、`objects/<gfx>-weapon-<n>.json`、`objects/world-<gfx>.json`、`players/<gfx>-<n>.json` |

先後錯誤時（前一個 recipe 沒發布、沒產生需要的檔、或檔案和它的收據不符）一律 `Prerequisite`，訊息寫出缺的是哪個 recipe 或哪個檔，什麼都不發布。前一個 recipe 之後被重跑：內容沒變 → 沿用（`UpToDate`）；變了 → 重做；需要的檔不見了 → `Prerequisite`。

### 輸出（項目 → 檔案）

| recipe | 項目 | 檔案 | 規則 |
|---|---|---|---|
| `players` | `<gfx>-<weapon>` | `players/<gfx>-<weapon>.json` | 七個動作（weapon＋0..3、8、18、19）。`stepMs`、`serverActionMs`（walk、attack、spellDir、spellNoDir）＝ 40 × ticks（R1）；`actionMs`：walk ×40、其他六個 floor；walk 的步沒有 `milliseconds`；`castReleaseMs`、`projectileReleaseMs`（只有 weapon 20）＝ 第一個 `!` 之前的 floor 和；`entryHash`；序列沒有 `totalTicks`（與基線同） |
| | `manifest` | `players/manifest.json` | `p24-player-profiles-v1`：`profiles`（gfx、weapon、path、sha256、stepMs、serverActionMs、projectileReleaseMs、castReleaseMs、sprite、frames，繼承的加 `inherited: true`）、`unavailable`（gfx、weapon、reason）。**不寫 `className`、`selectionPolicy`** |
| `shops` | `<gfx>` | `shops/<gfx>.json` | 動作 70，必須八方向；`stepMs` 與 `actionMs.shop` 都是 floor；`sourceAction: 70`；步有 `key`、`ticks`、`milliseconds`（`events` 只在非空時寫，基線都是空的） |
| | `manifest` | `shops/manifest.json` | `p24-original-shop-appearance-v1`：`sourceAction`、`profiles`（gfx、sprite、path、sha256、frames、durationMs、metadataSha256＝entryHash）、`unavailable`。不寫 `policy` |
| `doors` | `<gfx>` | `doors/<gfx>.json` | 動作 28 open、29 close，必須不分方向；`stepMs: 167`（規則常數）；`sourceActions: [28,29]`、`nonDirectional: true`；open／close 各 8 方向 |
| | `manifest` | `doors/manifest.json` | `p24-original-inn-door-v1`：`profiles`（gfx、sprite、path、sha256）。不寫 `policy` |
| `appearance-lists` | `monster-<gfx>`、`world-<gfx>` | （沒有檔案，只是清冊的一列） | 讀 objects 的外觀檔：sprite、frames、`durationMs`（＝該檔的 `stepMs`）、`metadataSha256`（＝該檔的 `entryHash`）、`supportedActions`（八方向齊全的動作，固定順序 walk、breath、attack、damage、die、spellDir、spellNoDir）。怪物缺 walk／breath／attack／damage／die、場景缺 breath（產品 `ObjectAssets` 的要求）→ unavailable 列 |
| | `encountered-monsters`、`world-appearances` | `encountered-monsters.json`、`world-appearances.json` | `p24-original-encountered-monsters-v1`（`profiles`、`unavailable`）、`p24-original-world-appearances-v1`（`profiles`；`unavailable` 只在非空時寫，基線沒有這個鍵）。不寫 `policy`、`sourceComponents` |
| `shadows` | `<gfx>` | `shadows/<gfx>.json` | `p24-original-shadow-frames-v1`：sprite 取 `101.shadow` 的值（不用 body＋1）；`frames` ＝ 所有 body profile 序列**實際引用**的 key 的聯集，依 (sid, frame) 排序；`flags: 5`（規則常數）。body profile 的 sprite 必須一致 |
| | `manifest` | `shadows/manifest.json` | `p24-original-shadows-v1`：`profiles`（gfx、bodySprite、shadowSprite、path、sha256、sourceBodyProfiles、actions、requiredKeys、providedKeys）、`unavailable` |

unavailable 的處理：規則做不出來（`Unavailable`：缺動作、像素在 bounds 外、替代包內容不同、沒有 `101.shadow`、影子檔幀數不夠、商店動作沒有方向）→ 不寫檔，該項目成為 manifest 的 unavailable 列，**不算失敗**（契約 4.4）。其他失敗（SPR 不在任何容器、檔案損壞、先後錯誤）照 T454 停下且不發布。`doors` 沒有 unavailable 清單，門的規則不符時直接停下。

players 的 unavailable 原因照基線的兩種寫法重現（L7）：`missing complete action <id>/<label>`、`opaque pixel outside frame: x,y versus (l, t, r, b)`。

### serverActionMs 與 T447

T447（`action-timing.md`）與契約 3.2、第 9 節 T455 列：`serverActionMs` **沒有 blocked 欄位**，照 R1 填 `40 × ticks 總和`。所以本張沒有任何「判 blocked、照暫行做法處理」的欄位。下面兩個情況基線沒有出現過，本張選擇不填假值、改列 unavailable：

- spellDir／spellNoDir（或弓的 attack）沒有帶 `!` 的步 → 放招點算不出來 → unavailable（`action <id>/<label> has no release event`）。產品的 `PlayerProfiles` 要求這些值存在，寫 null 會讓整份 manifest 載入失敗。

## 怎麼驗證

- **本機**：`dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev`、`dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` 都成功（0 錯誤）；同步 main（T456、T462 已合併）後重建一次也成功。`python -B -X utf8 engine/tools/check-no-game-assets.py` pass。依規定沒有在本機執行測試。
- **CI**：`native-ui-resources` 群組會跑 `asset-prep-spr-b`。`SprRecipeBChecks` 涵蓋：
  - 自動發現：五個 recipe 各一次；宣告的 `Requires` 符合契約第 6 節。
  - **先後順序錯誤**：players／appearance-lists／shadows 在 objects 之前；players 繼承的 `objects/0.json` 沒被 objects 準備；清冊成員的 objects 檔不存在；shadows 在 players 之前；gfx 沒有任何已發布的 body。每一種都是 `Prerequisite`、訊息寫出缺的 recipe 或檔名、什麼都不發布。
  - 兩個輸出目錄（其一路徑含中文與空白）的輸出與收據逐 byte 相同；請求順序不影響結果；輸出不含路徑、`className`、policy 文字、`source_offset`。
  - **既有讀取程式載入**：把各 recipe 的輸出組成一棵附 `integrity.json` 的 assets 樹後，`PlayerProfiles.LoadManifest`＋`PlayerProfiles.Load`（integrity、stepMs、七個動作 × 8 方向）、`OriginalAppearanceCatalog`（`ObjectAssets` 對 shops、doors、encountered-monsters、world-appearances 的同一組參數）、`PlayerShadowCatalog.Get`（124 個 key 逐一取影子幀）、`ObjectAppearance`。逐幀逐像素與合成 SPR 相同；時間欄位的數值；unavailable 的兩種原因；繼承列；收據記錄了讀到的 objects 檔。
  - 前一個 recipe 被重跑：內容相同 → `UpToDate`；少了需要的檔 → `Prerequisite` 且原本發布的 players 不受影響；改回後又是 `UpToDate`。shadows 中斷後續跑沿用前兩個項目，結果與不中斷時逐 byte 相同。
  - 不合法項目；預熱清單每個 recipe 的項目都被 `Normalize` 接受（objects 53、players 58、shops 12、doors 2、shadows 18、appearance-lists 27）。
  - 整輪下來合成客戶端目錄沒有變動。

## 未驗證什麼（待驗證）

- **真客戶端**：本張沒有讀 `D:\lineage開發\0-客戶端`、沒有跑準備器，也沒有宣稱任何資源缺少（所以沒有用 PakViewer 查 pak）。C# 輸出與 r29 基線的 S 比對（players 38 檔＋15 筆 unavailable、shops 11、shadows 17、doors 1、兩份清冊）留給 **T470**。
- **推測、要 T470 確認的幾點**：
  - 清冊的 `durationMs` 取外觀檔的 `stepMs`。基線 19＋6 列中 24 列有這個值，看起來就是 `stepMs`（例如 85 的 1833 是 breath exact 取整），但 `1094` 在基線沒有 `durationMs`，新檔會有。產品不讀 `durationMs`。
  - 基線前 4 個怪物列沒有 `supportedActions`，新檔一律寫。產品不讀這一欄。
  - players 的 unavailable 原因：像素座標是「依 sid 由小到大、每檔依幀、每幀依 tile 順序」找到的第一個；基線用的掃描順序沒有對過，原因類別相同，座標可能不同。
  - shadows 的 `sourceBodyProfiles` 順序是「objects（plain、weapon 由小到大、world）再 players（weapon 由小到大）」，與基線 gfx 0 那一列相同；其他列只有一個來源 recipe，順序一致。
  - 以預熱清單跑 objects 時，`objects/manifest.json` 會列出全部 plain 項目（15 個物件＋19 個怪物＋4 個場景＝38 個），基線只列 15 個。這是 T454 的 manifest 規則（「本次請求中的 `<gfx>` 項目」）。產品 `ObjectAssets.Get` 先查怪物與場景清冊，多出來的 gfx 不影響畫面，但 T470 的 S 比對會看到差異，要由 T467／T470 決定是否把怪物與場景改成不進 objects manifest。
- `ground`：沒有產生、也沒有改讀取點（本來就直讀）。基線兩張 PNG 對執行期解碼結果的 P 比對是 T470 的項目（契約第 2 節第 6 列）。
- 產品執行期的 Sprite pin（`ClientArchive`、`GroundSpriteReader`）本張不動（D1）。

## CI

PR #1079。

- 第一輪（run 43432168805）：`map-continuity` 失敗在 `SprRecipeBChecks.cs:109`：「不含絕對路徑」的檢查對 `prep-receipt.json` 誤判。收據把 manifest 列存成跳脫過的 JSON 字串（`\"gfx\":…`），裡面本來就有 `:\` 這個字元序列。在這之前的第 0–2 節（自動發現、全部先後順序錯誤案例、兩個目錄逐 byte 相同）都已通過。修正：輸出檔照原檢查，收據改成檢查不含實際的客戶端與引擎路徑（commit `task #1040: 測試的收據路徑檢查改比實際根目錄…`）。recipe 程式沒有改。
- 第二輪（run 43432662605、43432663523，commit 63195850）：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全部 pass。其中「Native UI resource layer」這一步（`native-ui-resources` 群組，含 `asset-prep`、`asset-prep-text`、`asset-prep-spr`、`asset-prep-spr-b`）輸出 `PASS 1242 DEMO checks`；`--module-tests` 也通過。CI log 只印總數，沒有逐項列出 `asset-prep-spr-b` 的項數。

## 產出路徑

- 程式：`engine/source/AssetPrep/SprRecipesB.cs`、`engine/source/AssetPrep/PreheatList.cs`，以及上表各檔的修改。
- 測試：`engine/_integration/dev/asset-prep-tests/SprRecipeBChecks.cs`；執行時寫到 `scratch/issue-1040/run-<時間>/`（不入 git）。
- 報告：本檔。

## 給下一棒的提示

- **T463（pickup）**：宣告 `Requires=>["players"]`（第 0 步的幀來自 body profile），用 `context.PriorOutputs("players")` 與 `ReadPrior` 取 profile；`players/manifest.json` 的列（含 `inherited` 指向 `objects/0*.json`）就是「42 筆適用清單」。注意繼承列的 body 在 objects，`Requires` 要同時列 `objects`。
- **T467（總流程）**：
  - 先後照每個 recipe 的 `Requires` 排就好（拓撲排序）；`AssetPriorOutputs` 目前從 `assets/<recipe-id>/` 讀（單一 recipe 發布的位置）。改成一棵樹之後，要把它換成從總流程的 staging 讀，收據的來源容器名 `assets/<recipe-id>` 可以沿用。
  - 預熱請求用 `PreheatList.Items(recipeId)`。
  - 新的失敗種類 `Prerequisite` 代表順序或前一步的輸出有問題，與客戶端壞掉分開回報。
  - manifest 的 unavailable 列放在收據 metadata 的 `unavailableRow`；L7 的數量可以從 `players` 收據數出來寫進總收據的 `knownLimits`。
- **T477／T478（按需準備）**：每個 recipe 的單一項目都能單獨產生；manifest 列的內容（`manifestRow`）就是執行期要的那一列。
- 若 T470 發現上面「推測」幾點與基線不同，改 `SprRecipesB.cs` 的規則並把該 recipe 的 `Version` 加一。
