# Issue #1073 報告：T477 執行期按需準備（物件、怪物、NPC 外觀，`ObjectAssets`）

日期：2026-11-13。執行者：本機 Claude（claude-local）。分支 `task/1073`。

## 開工前提（已核對，全部成立）

| 前提 | 結果 | 出處 |
|---|---|---|
| T476 對 D8 的裁定是「做」 | 成立 | `docs/design/asset-recipe.md` 第 8 節 D8「**定案：做**」 |
| T454（#1039）已合併 | 成立（Issue CLOSED，`SprShared.cs`／`SprRecipes.cs` 在 main） | `docs/reports/issue-1039.md` |
| T455（#1040）已合併 | 成立（Issue CLOSED，PR #1079） | `docs/reports/issue-1040.md` |

## 做了什麼

1. **`objects` recipe 抽出共用模型**（`engine/source/AssetPrep/SprRecipes.cs`）
   - 新增 `ObjectsRecipe.CreatureModel` 與 `ObjectsRecipe.Creature(context, entry)`：怪物／NPC 的動作選取、`stepMs`、8 方向序列與用到的 SPR 檔，原本寫在 `Monster()` 裡，現在先產生模型。
   - 準備模式的 `Monster()` 改成「模型 → JSON」，寫出的位元組與改動前相同（同樣的欄位、順序與數值；`SprRecipeChecks` 的兩目錄逐 byte 測試照跑）。
   - 執行期用同一個模型直接建記憶體內的外觀（下面第 3 點），所以兩邊用的是同一套轉換。
   - `ObjectsRecipe.CreatureRule(gfx)`：gfx 0（玩家規則）與 234（clothes 疊圖）不屬於生物規則，執行期不按需產生。
2. **SPR 共用層與核心**（`SprShared.cs`、`AssetPrepRunner.cs`）
   - `AssetRecipeItemContext.ForRuntime(...)`：執行期專用的唯讀 context。讀取方式和準備模式相同：結構檢查、`list.spz` 錨點、主包優先、替代包必須完全相同，每個讀到的 entry 都記錄。差別是沒有 staging 目錄，**任何 `Write` 都會被拒絕**（`InvalidRequest`），所以碰不到 `engine/assets`。
   - `SprDecodeCache`／`SprDecodeKey`：已解碼 SPR 的記憶體快取。鍵是（容器名、idx 的 offset、idx 的 size、recipe id 與版本、解碼後 `list.spz` 的 SHA-256），**不用 gfx 編號**。多個 gfx 共用同一個 sprite 時只解一次。只有執行期 context 會帶這個快取，準備模式照舊每個項目各自解碼。
   - `SprBody.File` 改走 `SprSource.Decode`：沒有快取時行為和錯誤訊息都與原本相同。
3. **記憶體內外觀**（`ObjectAssets.cs`）
   - `ObjectAppearance.FromCreature(model)`：不經 JSON，直接由模型建立。值與讀 `objects/<gfx>.json` 相同：混色 2；`actionMs` 照怪物規則（walk、attack、spellDir、spellNoDir 用 ×40，breath、damage、die 用 exact，即契約 3.2 與 D4）；步不帶 `milliseconds`；`frames` 是讀到的每個檔、每一幀；最後用和讀檔相同的規則檢查。
4. **按需解碼器**（新檔 `engine/source/ObjectOnDemand.cs`，`ObjectAppearanceOnDemand`）
   - `Request(gfx)`：第一次請求只把 gfx 排進佇列並回傳 null，**不在呼叫端執行緒解碼**。只有一個背景 worker（LongRunning 執行緒），一次解一個 gfx；每個 gfx 用 CAS 保證只排隊一次（「同一個 gfx 同時只解一次」）。
   - 狀態有 `None`／`Pending`／`Ready`／`Unavailable` 四種。`Ready` 的外觀會保留，之後每次回傳同一個實例。
   - 不可用時（`list.spz` 沒有 entry、沒有生物動作、缺檔、替代包內容不同、壞檔、步超出幀數、gfx 0／234）記住原因與 `AssetPrepFailure`，**不重試，也不挑一份湊數**（`sprites.md` §3.3）。
   - 客戶端目錄打不開，或 `list.spz` 錨點不符時，這個失敗只記一次，之後所有 gfx 都以同一個原因標為不可用，並寫一行 `UiResourceLog`。
   - 產品實例 `ObjectAppearanceOnDemand.Product` 用 `UiResourcePins.ClientRoot`（也就是 `TW_CLIENT_ROOT`，和其他執行期讀取點相同）與 `AssetPrepPins.Tw1608262200`。
5. **接線**
   - `ObjectAssets.Get(gfx)`：已準備的外觀照舊優先（player profile → encountered-monsters → world-appearances → `objects/manifest.json`）。都沒有、而且不是職業 gfx（`PlayerProfiles.IsCharacter`，玩家留給 T478）時，才交給按需解碼。
   - `ObjectAssets.Pending(gfx)`：在 `ObjectRendering.ComposeObjects` 裡，gfx 解碼中的物件照 `AppearancePending` 處理：放進 `missing`，不畫，也**不換成 gfx 1120**。解完後下一次重算就會畫出來（線上模式每次 `RefreshSceneImage` 都會重組物件）。確定不可用時 `Get` 回傳 null，走既有的缺圖退回（gfx 1120）。
   - 遊戲畫面 `ClientForm.RefreshObjects` 用 `new ObjectAssets(root, ObjectAppearanceOnDemand.Product)`。保留單參數建構子，行為和原本相同（不按需），既有測試不受影響。
6. **離線測試**（新檔 `engine/_integration/dev/asset-prep-tests/OnDemandAppearanceChecks.cs`，群組 `asset-prep-ondemand`，同時掛在 CI 已在跑的 `native-ui-resources`，Order 65，`[CheckGroup]` 自動發現，不改註冊表）。

## 磁碟快取：這次不做（量測留給真機）

- 任務要求「先量再決定」。本機規定不能執行編譯出的程式，所以我**沒有量** C# 的首次解碼時間。
- 可以參考的只有 T447 的 Python 樣本（`action-timing.md`「B」）：每個 gfx 冷讀約 0.1 秒（34 檔）、解析中位數不到 10 ms、99 百分位約 0.2 秒，最大的一個約 3 秒；檔案第二次讀走系統快取，約 0.07 秒。
- 依這些數字，同一次執行內的記憶體快取（已解外觀＋`SprDecodeCache`）應該夠用；重開遊戲後第一次遇到同一個 gfx 會再解一次。
- 決定：**這次不另做 `engine/runtime/` 磁碟快取，也不沿用現行 JSON 格式**。等 T470／T474 在真機量到「首次遇到新 gfx 的停頓」與「重開後的重解成本」，再決定要不要做緊湊二進位快取。快取鍵已經照規定設計好，日後加磁碟層可以直接沿用。

## 怎麼驗證

- **本機編譯**（只建置，沒有執行任何程式）：
  - `dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev`：0 個錯誤。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 個錯誤。
  - restore 用主專案目錄唯讀的 `engine/vendor`，並設了 `DOTNET_CLI_HOME`（worktree 內），同時設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`（#999）。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass。
- **離線測試內容**（交給 GitHub Actions 的 `native-ui-resources` 步驟執行；合成資料，不讀客戶端，不提交幀或像素）：
  1. 先用 `objects` recipe 事先準備 gfx 50、52、53，當作比對基準。
  2. 清單外的 gfx：第一次請求回傳 null，狀態是 `Pending`。worker 被測試閘門擋住時，64 個並行請求都還是 pending，也沒有重複解碼。
  3. 放行後 50、52、53 變成 `Ready`，和事先準備的 JSON 逐項比對：每一幀的 key、bounds、block 的 a／b／z／order、tile type、每個像素；每條序列的每一步；`stepMs`、`actionMs`；`At()` 在 4 個時間點取到的幀。結果相同。
  4. 每個 gfx 只解一次（`Attempts` 是 2）；解碼執行緒不是請求端的執行緒。
  5. 時間欄位符合契約 3.2：stepMs、walk、attack ×40；breath、die exact；沒有 walk 的 53 取 breath 長度取整（1958）。
  6. 快取：50 解 18 個檔，52 的 9 個檔全部命中；鍵含容器、offset、size、recipe 版本與 `list.spz` SHA；主包優先，只在替代包有的檔以該替代包為鍵。
  7. 不可用：60（替代包內容不同）、61（缺檔）、62（壞檔）、63（步超出幀數）、70（沒有生物動作）、9999（`list.spz` 沒有）、234（clothes 疊圖），各自的 `AssetPrepFailure` 與原因字串都對；再請求 5 次也沒有任何新的嘗試。
  8. 客戶端目錄不存在：全部標 `ClientRoot`，快取為 null。`list.spz` 版本不符：錨點拒絕（`ContainerMismatch`）。
  9. 執行期 context 的兩種 `Write` 都被拒絕。
  10. 測試前後：合成客戶端、測試自己準備的引擎目錄（含 `assets/` 與 `runtime/`）、本 repo 的 `engine/assets`（存在時），三者逐檔 SHA 都不變。
- 既有的 objects、NPC、怪物相關測試（`asset-prep-spr`、`native-ui-resources`、`doors`、`pickup` 等）由同一次 CI 跑。
- **CI 結果（PR #1091，run 43451001045／43451001039）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全部 pass。`native-ui-resources` 步驟顯示 `PASS 1488 DEMO checks`；同一時間 main 的同一步驟是 1411，多出的 77 項就是本張新增的 `OnDemandAppearanceChecks`（項數吻合），所以新測試確實在 CI 跑過並通過。其餘既有群組（含 objects、NPC、怪物、doors、pickup 相關）也都通過。

## 未驗證／待驗證（交給 Codex／真機）

- **首次遇到新 gfx 的實際停頓**：背景解碼期間畫面是否順暢，以及從 pending 到出現要多久。需要真機在 P24Lab 讓伺服器送一個預熱清單以外的怪物或 NPC（S87）來觀察。
- 真客戶端上按需解出的外觀是否和 T470 的基線一致：這次只用合成資料證明「按需＝事先準備」，真客戶端的等價由 T470 負責。
- `Sprite03`–`Sprite14` 被附加 36 個 `.img` 之後，按需路徑能不能開這些容器：這裡用的是準備器的三層驗證（不比整包 pin），推測可以，沒有在真機驗過。
- 記憶體用量（長時間遇到很多 gfx 時沒有淘汰機制）：沒有量。

## 推測

- 一次只解一個 gfx 的單一 worker 足夠應付一般遭遇節奏（根據 T447 的中位數）。大型 gfx（約 1%，7 MB 以上）在佇列前面時，後面的 gfx 會晚一點出現。

## 不在範圍（照任務）

- 玩家姿勢、陰影、商店、門、魔法與飛行物（T478）。
- `CombatSfxScene` 自己的 `ObjectAssets` 沒有接按需，所以按需外觀的怪物攻擊／受擊音效要等外觀接上後另外處理。建議放在 T478 一併接上，或另開小任務。
- 預熱清單（`PreheatList.cs`）內容沒有改。`rebuild-lock.json` 沒有改。

## 產出路徑

- `engine/source/ObjectOnDemand.cs`（新）
- `engine/source/ObjectAssets.cs`、`engine/source/ObjectRendering.cs`
- `engine/source/AssetPrep/SprRecipes.cs`、`SprShared.cs`、`AssetPrepRunner.cs`
- `engine/_integration/dev/asset-prep-tests/OnDemandAppearanceChecks.cs`（新）
- `docs/reports/issue-1073.md`（本檔）

## 給下一棒的提示

- **T478**：照同一個模式做。用 `AssetRecipeItemContext.ForRuntime` 加共用的 `SprDecodeCache`（建議直接重用 `ObjectAppearanceOnDemand.Product` 的快取，或把 worker 抽成共用），再從 recipe 抽出模型（例如 players 的 `Player()`、shadows、shops、doors、magic、projectile），並在 `ObjectAssets.Get(gfx, weapon)`、`GetShop`、`GetDoor`、`PlayerProfiles` 的讀取點加 pending 判斷。`SelfAppearancePending` 的接法還沒有做。
- `ObjectAppearanceOnDemand` 的 `beforeDecode` 鉤子只給測試用，產品不要傳。
- 要加磁碟快取時，鍵沿用 `SprDecodeKey`，格式不要用現行 JSON（asset-recipe.md 第 5 節）；寫入位置只能是 `engine/runtime/`。
- T470／T474 量首次停頓時，可以看 `ObjectAppearanceOnDemand.Product.Cache`（`Decodes`／`Hits`）和 `Attempts`。
