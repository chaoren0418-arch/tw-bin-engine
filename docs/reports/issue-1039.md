# Issue #1039 — T454 準備器：SPR 共用層與 character、objects（b15-prep-sprites-a）

2026-11-13，runner：claude-local，分支 `task/1039`。依 `docs/design/asset-recipe.md`（1.1–1.3、3.2、4.6、第 5、6、9 節）與 `docs/reference/asset-recipes/sprites.md`（§3、§4.1、§4.3、§6、§8）。

## 結論

SPR 共用層與兩個 recipe（`character`、`objects`）已寫好，產品與 DemoChecks 本機**編譯通過**（0 錯誤；DemoChecks 只有 3 個既有警告）。離線測試用合成的 list.spz、Text 與 17 個 Sprite 容器。依本機執行限制，本機**沒有執行**任何測試，結果以 PR 的 CI 為準（見文末「CI」）。

和 T451／T453 一樣，這兩個 recipe 是**程式庫**：沒有接啟動流程；把各 recipe 組成產品讀的那棵樹是 T467 的工作。

契約 4.6 指定給 T454 的核心缺口「Sprite 輸入從 17 個容器整包 pin 改成三層驗證」也一併完成（本機客戶端 Sprite03–14 被附加過 entry，不改的話真客戶端跑不起來）。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/AssetPrep/AssetPrepInput.cs` | Sprite 輸入改成三層驗證（結構檢查／錨點 entry／逐 entry 記錄），不再 pin 整包；新增 `PrepArchive`（idx 結構檢查：筆數×28＋4＝檔案大小、entry 不越界、同包不重名）；`AssetPrepPins` 改成只有 list.spz 錨點；`ReadSpriteList()`（Text/list.spz，DES＋AES 解碼後核對錨點，一輪只解一次）；`Verify()` 能驗 list.spz 來源。合併 main 時保留 T453 的 `TextEntries`／`ReadTextEntry`／`VerifyText` |
| `engine/source/AssetPrep/AssetPrepCore.cs` | `AssetPrepFailure.Unavailable`（客戶端有資料但規則做不出來，例如缺動作、替代包內容不同）；`IAssetRecipe.DependsOnRequest`（manifest 這類由請求本身決定的項目，請求不同時不沿用） |
| `engine/source/AssetPrep/AssetPrepRunner.cs` | 收據的輸入改記 idx 大小／idx SHA／pak 大小（工具版本 `tw-asset-prep/2`）；`AssetRecipeItemContext.ReadSpriteList()`、`Requested`；同一個來源只記一次 |
| `engine/source/AssetPrep/SprShared.cs`（新） | 共用層：`SpriteList`（list.spz 解碼、entry 範圍、一個 gfx 兩筆就拒絕）、`SprEntry`（gfx→sprite、先切成動作再依編號找、欄位如 `101.shadow`／`105.clothes`）、`SprFile`（SPR 解碼與檢查，空幀用 §8.2 放寬的判定）、`SprSource`（主包優先、替代包唯一性）、`SprBody`、`SprTiming`（×40／floor／exact／放招點）、`SprJson`（固定鍵順序的輸出） |
| `engine/source/AssetPrep/SprRecipes.cs`（新） | `[AssetRecipe]` `character`、`objects`；`SprCreature`（玩家／怪物動作與時間規則，T455 的 players 可直接沿用） |
| `engine/source/Assets.cs` | 把讀 `character/blocks.json`／`animation.json` 的那段抽成 `Assets.LoadCharacter`（行為不變），讓測試能用產品本身的讀法 |
| `engine/source/Tests.cs` | 契約 3.2：原本開 `character/frames/*.png` 的檢查，改成「獨立計算每個像素位置 vs `Renderer.Blit`」，不再需要 PNG |
| `engine/_integration/dev/asset-prep-tests/SprRecipeChecks.cs`（新） | DemoChecks 群組 `asset-prep-spr`（同時掛在 CI 已在跑的 `native-ui-resources`，Order 62） |
| `engine/_integration/dev/asset-prep-tests/AssetPrepChecks.cs` | 配合三層驗證：拿掉整包 pin 的拒絕案例，改測結構（idx 截斷、entry 越界、同包重名、pak 截斷）與「多了無關 entry 仍接受」 |

沒有改 `GroundSpriteReader`（pin 與測試原樣）、`Program.cs`、`DemoChecks.csproj`、註冊表、`build.yml`、`rebuild-lock.json`、`docs/REPORT.md`。PR 裡沒有任何幀或像素：測試的 SPR 是依編號算出的測試值。

### 輸出（項目 → 檔案）

輸出路徑是 assets 樹的相對路徑（契約 4.5）；單一 recipe 發布時落在 `assets/<recipe-id>/` 之下。

| recipe | 項目 | 檔案 | 規則 |
|---|---|---|---|
| `character` | `character` | `character/blocks.json`（`p06-spr-blocks-v1`）、`character/animation.json`（`p04-character-v1`） | gfx 0 的動作 0（walk）與 3（breath），八方向。blocks：每幀 `key`、`bounds`、`anchor`、`source_sha256`、`blocks`。animation：`character`、`metadata`（list.spz 與 entry 的 SHA）、`mapping` 16 筆（`key`、`subid`、`frame`、`ticks`、`base_duration_ms`、`events` 數字陣列）。**不產生 PNG 與預覽**（契約第 9 節：PNG＝0 張） |
| `objects` | `<gfx>` | `objects/<gfx>.json` | gfx 0 用玩家規則（weapon 0）；其他用怪物規則 |
| | `<gfx>-weapon-<n>` | `objects/<gfx>-weapon-<n>.json` | 玩家規則，七個動作缺一就拒絕 |
| | `world-<gfx>` | `objects/world-<gfx>.json` | 怪物規則；`world-234` 是 body＋`105.clothes` 疊圖 |
| | `magic-<gfx>` | `objects/magic-<gfx>.json` | 動作 0；標籤 167 fly、219 impact、11722 heal、其他 effect；`stepMs` 1；floor；189／191／197／221／2165 加 `effectTileType: 18`；167 的方向用 `base + (d mod 4)` |
| | `projectile-<gfx>` | `objects/projectile-<gfx>.json` | 動作 0 fly；`frames` 給 `ProjectileRendering` |
| | `manifest` | `objects/manifest.json` | `{"gfx":[本次請求中的 <gfx> 項目]}`，由準備結果產生 |

時間欄位（契約 3.2、sprites.md §4.3，D4 暫行＝重現基線）：

- 玩家規則：`stepMs`＝walk ×40；`actionMs`：walk ×40；attack ×40（弓 weapon 20 為 floor）；damage／breath／die exact（弓為 floor）；spellDir／spellNoDir floor。`castReleaseMs`（兩個 spell）、弓的 `serverAttackMs`（×40）與 `projectileReleaseMs`（attack 的放招點）。
- 怪物規則：entry 有的那幾個動作（0 walk、1 attack、2 damage、3 breath、8 die、18 spellDir、19 spellNoDir；動作 8 依編號標 die）；walk／attack／spell ×40，breath／damage／die exact；`stepMs`＝walk ×40，沒有 walk 時取 breath exact 的整數。
- 步的 `milliseconds` 只出現在該動作長度是 floor 的地方（與基線的出現位置一致）。
- 不產生的來源紀錄欄位：`sourceOffset`、`spzOffset`、`sourceActions`、`serverSpeedSource`、`entry`、`sourceComponents`、`sourcePolicy`、`eventPolicy`、`source_offset`、`block_index`、`sourceEmpty`、`sourceHeaderOffset`、block 的 `sourceSprite`／`sourceSid`／`sourceFrame`。保留 `entryHash`。

拒絕（都不發布，訊息指出 gfx／檔名／容器）：替代包同名不同內容（`Unavailable`）、SPR 不在任何容器（`MissingEntry`）、SPR 截斷或有多餘 bytes 等（`CorruptEntry`）、步的幀號超出 SPR 幀數（`CorruptEntry`）、缺動作或 gfx 不在 list.spz（`Unavailable`）、list.spz 解碼後 SHA 不符（`ContainerMismatch`，容器 Text）、Text 沒有 list.spz（`MissingEntry`；不退回 list.spr）。

## 怎麼驗證

- **本機**：`dotnet build engine/source/OfflineClient.csproj -c Release`、`dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` 都成功（0 錯誤）；`python -B -X utf8 engine/tools/check-no-game-assets.py` pass。依規定沒有在本機執行測試。
- **CI**（`native-ui-resources` 群組會跑 `asset-prep`、`asset-prep-text`、`asset-prep-spr`；`--module-tests` 跑 `asset-prep`）。`SprRecipeChecks` 涵蓋：
  - 共用層：list.spz 編碼→解碼→解析、gfx→sprite、動作 8 不被 breath 的 `8.0:6` token 誤配（§8.3）、名稱含空白、欄位、×40／floor／exact／放招點、一個 gfx 兩筆拒絕、未知格式拒絕；每個合成 SPR（含索引色調色盤）解碼後的幀、bounds、tile、RGB555 像素與編碼前相同；非標準空幀 `(352,240,-288,-240)` 接受並保留 bounds；截斷、多餘 bytes 拒絕。
  - 兩個輸出目錄（其一路徑含中文與空白）的輸出與收據逐 byte 相同；請求順序不影響結果。
  - 既有讀取程式載入：`Assets.LoadCharacter`（96 幀、16 序列）、`ObjectAppearance`（玩家、怪物、magic、world）、`ProjectileRendering` 的讀法（projectile）；逐幀逐像素與合成 SPR 相同；時間欄位的數值。
  - 主包優先（替代包有不同內容的同名檔不被讀）、只在替代包的檔、兩個替代包內容相同可接受；收據記錄 list.spz（解碼後 SHA）與每個 SPR entry。
  - 拒絕案例（上一節）與不合法項目名稱。
  - 續跑：中斷後重跑沿用 list.spz／SPR 來源驗得過的項目；manifest 在請求不同時重做；結果與不中斷時逐 byte 相同。
  - 整輪下來合成客戶端目錄沒有變動。

## 未驗證什麼（待驗證）

- **真客戶端**：本張沒有讀 `D:\lineage開發\0-客戶端`、沒有跑準備器。C# 輸出與 r29 基線的 S 比對（character 96 幀、objects 52 檔）留給 **T470**。
- **推測、要 T470 確認的幾點**：
  - `entryHash`／`entry_sha256` 的範圍：本實作是「行首 `#gfx` 到下一個行首 `#數字` 之前」（含結尾換行）。基線 `entry_range [16, 3691]` 是否同一個切法沒有對過。
  - `world-234` 的 `actionMs.breath` 用怪物規則的 exact；基線值 2000 也可能是 ×40（取決於 ticks 總和是 48 還是 50）。
  - `objects` 的 `<gfx>` 項目一律用「entry 有的生物動作」。基線 `945.json` 只有 walk／breath，`world-945.json` 卻有 walk／breath／damage／die；若 list.spz 的 gfx 945 有 damage／die，新的 `945.json` 會多兩個動作（產品讀到多的序列不受影響，但 S 比對會有差）。
  - 基線部分檔案沒有 `actionMs`（`serverSpeedSource` 那批）、部分序列沒有 `totalTicks`；新檔一律寫出。產品讀 `actionMs` 時算出的長度與沒有它時相同（walk＝stepMs、其他＝exact），這點是讀程式碼推的，沒有實機看過。
  - `magic-167` 的「mod 4」與其他特例只照基線寫死，沒有一般化（sprites.md §4.3 也標為推測）。
- 產品執行期的 Sprite pin（`ClientArchive`、`GroundSpriteReader`）本張不動（契約 D1）。
- `Tests.cs` 改過的檢查只在使用 r29 素材的整合流程跑，本機沒有執行。

## CI

PR #1076，commit `task #1039: T454 …` 的 run 43427036608：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全部 pass。其中「Native UI resource layer」這一步（`native-ui-resources` 群組，含 `asset-prep`、`asset-prep-text`、`asset-prep-spr`）輸出 `PASS 1136 DEMO checks`；`--module-tests`（含 `asset-prep`）也通過。CI 的 log 不會逐項列出檢查名稱，所以只看得到總數，沒有分別列出 `asset-prep-spr` 的項數。

## 產出路徑

- 程式：`engine/source/AssetPrep/SprShared.cs`、`engine/source/AssetPrep/SprRecipes.cs`，以及上表各檔的修改。
- 測試：`engine/_integration/dev/asset-prep-tests/SprRecipeChecks.cs`；執行時寫到 `scratch/issue-1039/run-<時間>/`（不入 git）。
- 報告：本檔。

## 給下一棒的提示

- **T455（players、shops、doors、shadows、appearance-lists）**：玩家 profile 直接用 `SprCreature.PlayerActions`／`PlayerRule`／`Sequences`（players 的 walk 步沒有 `milliseconds`、其他 6 個 floor，見 sprites.md §4.2，規則與 objects 的 gfx 0 不同，要另寫一個 rule 函式）；「不透明像素在 bounds 外」用 `SprFile.PixelsInsideBounds`。shadows 的 sprite 用 `SprEntry.Field(101)`，不要用 body＋1。`SprBody.File(sid)` 會把讀到的 entry 記進收據。
- 預熱清單（契約第 5 節）還沒建；`objects` 的項目可以任意單獨準備，`manifest` 依請求產生。
- 收據格式變了（`tw-asset-prep/2`：輸入改記 `indexSize`／`indexSha256`／`pakSize`，沒有整包 pak SHA）；T467 的總收據照這個欄位寫。
- 新的失敗種類 `Unavailable`：T467 在組總收據時，可以把它和「容器壞掉」分開回報。
- 若 T470 發現上面「推測」幾點與基線不同，改 `SprRecipes.cs` 的規則並把 `Version` 加一。
