# Issue #1074 報告：T478 執行期按需準備（玩家姿勢、陰影、商店、門、魔法與飛行物、pickup）

日期：2026-11-14。執行者：本機 Claude（claude-local）。分支 `task/1074`，PR #1103。

依據：`docs/design/asset-recipe.md` 第 5 節第 2 點；接續 T477（#1073）的背景解碼與記憶體快取。

## 做了什麼

### 1. 共用機制：`RuntimeAppearances`（新檔 `engine/source/RuntimeAppearances.cs`）

- 與 T477 的 `ObjectAppearanceOnDemand` 同一個模式：一個背景 worker（LongRunning），一次做一個項目；每個項目只排隊一次（CAS）；呼叫端只查表，**不在呼叫端執行緒解碼**。狀態 `None／Pending／Ready／Unavailable`；不可用時記住原因與 `AssetPrepFailure`，**不重試、不挑一份湊數**。客戶端打不開或 `list.spz` 錨點不符時只記一次，之後全部同一個原因。
- **直接跑準備器自己的 recipe**：新增 `AssetRecipeItemContext.ForRuntimeMemory`（`AssetPrepRunner.cs`）。讀取方式與準備模式完全相同（結構檢查、`list.spz` 錨點、主包優先、替代包必須相同、每個 entry 記錄來源），差別是沒有目錄：`Write` 的 bytes 只留在記憶體（同樣檢查安全路徑、同一路徑只能寫一次）。產品自己的讀取程式讀一次就丟掉，只保留解析後的結果。**沒有寫 `engine/assets`，也沒有寫 `engine/runtime`**（沒有磁碟快取，理由同 T477）。
  - 這樣做的好處：按需結果與「事先準備同一個項目」是同一份 bytes（測試直接比 SHA 與逐幀內容），不另外維護一套轉換。
  - 這份 JSON 只是過渡（解析完即丟），不是快取格式；第 5 節「快取不沿用現行 JSON」沒有違反。
- 解碼快取：每個 recipe 一個 `SprDecodeCache`，鍵沿用 T477 的 `SprDecodeKey`（容器、idx offset、size、recipe id／版本、`list.spz` SHA）。
- `RuntimeAppearances.Active`：靜態讀取點（`PlayerProfiles`、陰影、魔法、飛行物、封包特效）用的實例。**預設 null＝只用已準備的樹，與 T478 前完全相同**；遊戲視窗（`ClientForm.OnLoad`、`RefreshObjects`）綁上 `RuntimeAppearances.Product`（`TW_CLIENT_ROOT` ＋ `AssetPrepPins.Tw1608262200`）。

### 2. 各讀取點

| 讀取點 | 按需項目（用哪個 recipe） | pending 時 | 不可用時 |
|---|---|---|---|
| `PlayerProfiles`（`Get`／`Pending`／`Own`／`ForGfx`） | `players` 的 `<gfx>-<姿勢>` | 自己：`SelfAppearancePending`（「角色外觀正在更新」，不畫）；他人：不畫（`ObjectAssets.StancePending`） | 照原本規則退回姿勢 0（已準備或按需），再沒有就他人走 gfx 1120、自己顯示「尚未支援此外觀」 |
| `PlayerShadows`（`PlayerShadowCatalog.Resolve`） | `shadows`：sprite 取 `101.shadow`，幀＝這個 body 序列實際引用的 key | 不畫影子 | 不畫影子（與「沒有提供的外觀不畫影子」相同） |
| 個人商店 `ObjectAssets.GetShop` | `shops`（動作 70，八方向） | 不畫（`ShopPending`） | 不畫（原本行為） |
| 門 `ObjectAssets.GetDoor` | `doors`（動作 28／29，不分方向，`stepMs` 167） | 不畫（`DoorPending`） | 不畫（原本行為） |
| 魔法 `MagicRendering.MagicAppearance` | `objects` 的 `magic-<gfx>` | 這一格不畫 | 不畫 |
| 封包特效 `MapEffects.cs` `PacketEffectAssets.Get`（`objects/magic-<gfx>.json`） | 同上；只在沒有準備檔、也沒有可用地圖特效時才按需 | 回 null、**不寫入快取**（下一格再問） | null（原本行為） |
| 飛行物 `ProjectileRendering`（gfx 66 的 `objects/projectile-66.json`） | `objects` 的 `projectile-66` | 不畫箭 | 不畫箭 |
| pickup 追加幀 `ObjectAssets.PickupSupports／PickupFrames` | `PickupRecipe.RuntimeGroup`：該 profile 的 `list.spz` 有沒有可用的動作 15 | 撿取演出這次略過（frames 為 null） | 不支援（`unsupported-profile/speed`） |

已準備的項目一律優先，讀法完全不變；只有已準備的樹沒有的項目才按需。

### 3. `PlayerProfiles` 與 `IsCharacter`（照 `action-timing.md` A3 先讀程式）

- 讀過程式：`IsCharacter` 實際分辨的是「**這個 gfx 用玩家規則畫**」——`ObjectAssets.Get(gfx, 姿勢)` 在職業 gfx 缺某個姿勢時回 null（不退回一般物件外觀），`ObjectAssets.Get(gfx)` 不對它用生物規則按需。它**不是**職業表，所以沒有換成 `102.type`（type 5 也包含 world 的 gfx 945）。
- 定案的做法：`IsCharacter` 維持「已準備的 profile」（預熱清單）。哪些 gfx 會用玩家規則按需：
  1. 已準備過的職業 gfx 的**清單外姿勢**（例如 0 的 11／24／40，或職業 gfx 的 54／58／62）；
  2. **自己角色目前的 gfx**（S87／S76 給的；`WorldSession.AppearanceProfile`／`SelfAppearancePending` 經 `PlayerProfiles.Own` 認領）——涵蓋變身外型。
  其他 gfx（怪物、NPC、他人的變身外型）不轉成玩家規則，照 T477 用生物規則；這保持 A3 說的現行效果。
- 解析規則抽成純函式 `PlayerProfiles.Resolve(prepared, od, onDemandFor, gfx, 姿勢, out pending)`：已準備的完全相同 → 按需同姿勢（ready 用它、pending 回 null）→ 不可用時照原本規則退回姿勢 0（已準備或按需）。不合法的姿勢從不請求。`Active` 為 null 時與原本的 `Get` 逐條相同。
- 時間欄位：`stepMs`、`serverActionMs`（`40 × ticks`，R1）、`castReleaseMs`、`projectileReleaseMs`（第一個 `!` 之前 `floor(ticks×1000÷24)` 的總和，R2）都由 `players` recipe 自己從 `list.spz` 算，`PlayerProfiles.Row` 讀 recipe 記的那一列；外觀再過 `PlayerProfiles.Check`（與讀準備檔相同的檢查，含 `stepMs` 必須等於 walk ticks 的 R1）。產品的冷卻與放出點照 T461 仍由 `ActionTiming` 從外觀的 ticks 算。
- `PlayerProfile` 多一個 `Runtime`（按需外觀；`Path` 是 recipe 的虛擬路徑），`PlayerProfiles.Load` 遇到它直接回傳，不讀檔。

### 4. 其他改動

- `ObjectAppearance`：可由記憶體 JSON 建立（`FromRuntimeJson`），檢查與讀檔相同；`Runtime` 屬性標示「執行期建立」（T477 的 `FromCreature` 也標上）。陰影只對 `Runtime` 的 body 按需，已準備的 body 照舊只用已準備的陰影。
- `ShadowsRecipe.Runtime`、`PickupRecipe.RuntimeGroup`：把既有規則開放給「runtime 已有的 body」，準備模式的輸出不變（`SprRecipeBChecks`、`PickupRecipeChecks` 的逐 byte 測試照跑）。
- `PlayerShadowCatalog.ReadFrames`、`PickupAssets.ReadGroup／Compose`、`RuntimeAppearances.ProjectileFrames`：原本內嵌在讀檔流程的解析抽成可重用的函式，內容不變。
- 改動 `ProjectileRendering` 一處行為：找不到 `projectile-66` 的幀 key 時，原本丟 `KeyNotFoundException`，現在這支箭不畫。

## 怎麼驗證

- **本機編譯**（只建置，沒有執行任何程式）：
  - `dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev`：0 個錯誤。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 個錯誤，新檔沒有警告。
  - restore 用主專案目錄唯讀的 `engine/vendor`（`--source`），`DOTNET_CLI_HOME`／`NUGET_PACKAGES` 在 worktree 的 `scratch/`，同時設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`（#999）。
- **離線測試**（新檔 `engine/_integration/dev/asset-prep-tests/RuntimeAppearanceChecks.cs`，群組 `asset-prep-runtime`，同時掛在 CI 已在跑的 `native-ui-resources`，Order 66；`[CheckGroup]` 自動發現，不改註冊表）。合成資料（17 個 Sprite 容器與 `list.spz`，像素是依編號算出的測試值），寫在 `scratch/issue-1074/run-<時間>/`，不讀客戶端：
  1. 基準：用 `objects`、`shops`、`doors`、`players`、`shadows` recipe 事先準備同一批項目。
  2. **清單外 → pending → 解完**：玩家姿勢 61/20、商店 61、門 2347、魔法 189、飛行物 66 第一次都回 null、狀態 pending（worker 被測試閘門擋住時 48 個並行請求也沒有重複建立）；放行後全部 ready、每個只建一次、在背景執行緒建。
     - 玩家 61/20 與 `players/61-20.json` 逐幀相同；row 的 SHA 等於準備檔（同一份 bytes）；時間照 R1／R2：`stepMs` 160、`serverActionMs` walk 160／attack 600／spellDir 200／spellNoDir 240、`castReleaseMs` 83／83、`projectileReleaseMs` 208。
     - 商店、門、魔法與各自的準備檔逐幀相同；飛行物的幀與 `projectile-66.json` 相同。
     - **陰影**：runtime body 的陰影 key 集合＝body 序列引用的 key（不多不少），每一幀與準備的 `shadows/61.json` 同 key 的幀相同；只讀了 3214（`101.shadow`）的檔。
     - **pickup**：runtime body 61/0 的動作 15 → 24 個追加幀，8 個方向各 4 幀，第 0 步就是 body 的幀（同一個實例）。
  3. **不可用**（每個讀取點至少一組）：姿勢缺動作（61/4、62/24）、gfx 0 的繼承姿勢、商店動作不分方向（70）、沒有動作 70（189）、門的動作有方向（2348）、沒有 28（61）、`list.spz` 沒有的魔法（9998）與飛行物（9997）、body 沒有動作 15 第 0 步的幀（61/20 的 pickup）、沒有 `101.shadow`（63 的陰影）。原因與 `AssetPrepFailure` 都對；再問 5 次沒有任何新嘗試。
  4. **讀取點接線**：`PlayerProfiles.Resolve`（合成的已準備集合，不碰行程共用的 manifest）：已準備姿勢照用、他人未準備的 gfx 不請求、自己認領的變身 gfx 64 pending → ready、職業 gfx 清單外姿勢 50 pending → 不可用時退回已準備的姿勢 0、64 的姿勢 4 不可用時退回按需的姿勢 0、不合法姿勢不請求、沒有按需來源時只看已準備集合。`PlayerShadowCatalog.Resolve`：已準備 body 用已準備陰影且不請求；runtime body pending 時無影子、ready 後拿到 101.shadow 幀。`PacketEffectAssets`：沒有準備檔也沒有地圖特效的 189 按需、pending 不快取；9998 維持 null。
  5. 客戶端目錄不存在：全部 `ClientRoot`。記憶體 context：兩種 `Write` 都只進記憶體，同路徑第二次、`../` 都拒絕。
  6. 測試前後：合成客戶端、測試準備的引擎目錄、接線用的樹、本 repo 的 `engine/assets`（存在時）逐檔 SHA 不變。
- 既有的 players、shadows、shops、doors、magic、pickup 測試（`asset-prep-spr`、`asset-prep-spr-b`、`asset-prep-pickup`、`asset-prep-ondemand`、`native-ui-resources` 等）由同一次 CI 跑。
- **CI 結果（PR #1103，程式 commit 的 run 43460547927／43460547867）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全部 pass。`native-ui-resources` 步驟顯示 `PASS 1546 DEMO checks`；T477 合併後同一步驟是 1488，多出的 58 項正好是本張 `RuntimeAppearanceChecks` 的項數（47 個 Check、不可用迴圈 10 次多算 9 次、2 個 `ApRefused`），所以新測試確實在 CI 跑過並全部通過。既有的 players、shadows、shops、doors、magic、pickup、按需物件（T477）相關群組在同一步驟與其他步驟都通過（沒有 FAIL）。
- **同步 main 之後（merge commit `e150930b`，run 43461571560／43461571582）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 再次全部 pass，`native-ui-resources` 仍是 `PASS 1546 DEMO checks`。同步前本機重新編譯 `OfflineClient` 與 `DemoChecks` 都是 0 個錯誤，`check-no-game-assets.py` pass；`rebuild-lock.json` 與 main 相同。

## 未驗證／待驗證（交給 Codex／真機）

- **伺服器 `spr_action` 對照（契約 L5）**：預熱清單以外的姿勢（例如 11／24／40、54／58／62）與變身外型，`serverActionMs`、`stepMs`、`castReleaseMs`、`projectileReleaseMs` 是由 `list.spz` 照 R1／R2 算出的值，**無法與伺服器的 `spr_action` 對照**。照契約不在客戶端加表補；真機若遇到伺服器回退移動、擋攻擊或施法，記進 Issue，那是 L5 的訊號。
- 真機在 P24Lab：自己換到清單外姿勢、變身（S76）時，「角色外觀正在更新」到出現的時間與畫面；他人清單外姿勢、商店、門、魔法、箭的 pending 期間是否只是短暫不畫；按需陰影與 body 的位置是否對齊（只用合成資料證明「按需＝準備」，真客戶端的等價由 T470 負責）。
- 首次遇到新項目的停頓、長時間記憶體用量（沒有淘汰機制）：沒有量（本機不能執行編譯出的程式）。
- `CombatSfxScene` 自己的 `ObjectAssets` 仍沒有接按需（T477 已記），按需外觀的攻擊／受擊音效沒有處理。

## 推測

- 單一 worker 足夠應付一般遭遇節奏（依 T447 的中位數）；自己變身時若 worker 正在解大型 gfx，外觀會晚一點出現。
- 他人變身成未準備的 gfx 時照 T477 用生物規則；自己變身同一個 gfx 後，這個 gfx 也會被當成玩家規則（`Own` 認領是行程內共用的）。兩者動畫長度規則不同（玩家 vs 怪物的 `actionMs` 規則），影響只在動畫長度，沒有實測。

## 不在範圍

- 預熱清單（`PreheatList.cs`）內容沒有改；`rebuild-lock.json`、`Program.cs`、`DemoChecks.csproj` 沒有改。
- 磁碟快取沒有做（同 T477）。
- 沒有宣稱任何「缺資源」，所以沒有用 PakViewer 查 pak。

## 產出路徑

- `engine/source/RuntimeAppearances.cs`（新）
- `engine/source/PlayerProfiles.cs`、`WorldSession.cs`、`ObjectAssets.cs`、`ObjectRendering.cs`、`PlayerShadows.cs`、`PickupAssets.cs`、`MagicRendering.cs`、`MapEffects.cs`、`ProjectileRendering.cs`、`BuddyUi.cs`
- `engine/source/AssetPrep/AssetPrepRunner.cs`、`SprRecipesB.cs`、`PickupRecipe.cs`
- `engine/_integration/dev/asset-prep-tests/RuntimeAppearanceChecks.cs`（新）
- `docs/reports/issue-1074.md`（本檔）

## 給下一棒的提示

- **T470／T474（真機量測）**：可以看 `RuntimeAppearances.Product` 的 `Attempts`、`State(key)`、`Reason(key)`、`Cache(recipeId)`（`Decodes`／`Hits`）；key 用 `PlayerKey`、`ShadowKey` 等靜態函式產生。
- `ObjectAppearanceOnDemand`（T477）與 `RuntimeAppearances` 是兩個 worker、各自開一次客戶端。日後要合併成一個，可以把 T477 的生物外觀也改成 `RuntimeAppearances` 的一種項目（用 `ObjectsRecipe` 的 `<gfx>` 項目跑記憶體 context），但 T477 的測試有檢查「不經 JSON」與快取命中數，要一起改。
- `RuntimeAppearances.Active` 是靜態的；離線測試要用時記得 `try/finally` 還原（`RuntimeAppearanceChecks` 的做法），並且不要在測試裡碰 `PlayerProfiles.Get` 等靜態入口（它會初始化行程共用的 manifest，`action-timing` 群組靠自己設的 base directory）。
- 要加磁碟快取時，鍵沿用 `SprDecodeKey`，寫入位置只能是 `engine/runtime/`，格式不要用現行 JSON。
