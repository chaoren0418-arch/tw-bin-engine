# Issue #985 報告：T428 R12-A 自己的動作 71 與層半透明

依據：`docs/reference/gap-matrix.md` 15.14.5（5:`sub_87C890` 列）、15.14.6 R12-A；`docs/reference/followups-14.md` §3.2、§3.3、§3.6。

## 做了什麼

### ① 自己的 S158 71（釣魚）
- 新檔 `engine/source/OwnActionState.cs`：
  - `[WorldReceive(158, Observe=true)] ObserveOwnAction`：自己的 S158 記成 `OwnActionView(Actor, Epoch, Action, At, X, Y)`（`At`＝`ActionClock()`，X／Y＝收到時自己的格座標）。觀察器先於主分派執行，不吃封包；`WorldSession.cs` 沒改。
  - `[WorldReceive(119, Observe=true)]`：自己的 S119（伺服器收竿時送外觀更新，同 `Fishing.cs` 的結束條件）清掉狀態。
  - `[WorldReset]` 清掉；`[WorldSnapshot("own-action")]` 快照。
  - `OwnFishingSince()`：動作＝71、同一個世界與角色、沒死、格座標沒變、沒有更新的自己戰鬥動作時，回開始時間。
  - `Renderer.OwnFishingFrame`／`OwnFishingDraws`：用既有的 `FishingFrame`（list.spr 動作 71，本體 `<別名>-<304＋朝向>.spr`）取當格，本體＋`ClothDraws`（gfx 0 的 cloth 6262 釣魚線，同一個 sid:frame）。
- `engine/source/ObjectRendering.cs`（只動自己的分支）：戰鬥影格之後加 `else if(OwnFishingFrame(...))`：移除自己原本的身體／影子，改畫釣魚本體＋cloth＋影子，`OwnCombatVisual` 的動作記成 `fishing`。戰鬥與拾取影格照舊優先。讀不到動作 71 或子檔時照舊畫姿勢（同他人的 AN§3.4 退回）。

### ② 隱身半透明的層旗標例外（FU14§3.2／§3.6：TW16 `sub_A463C0`、3.81 `sub_4E7000`）
- `engine/source/Invisibility.cs`：`InvisibilityOpacity` 對附加層（cloth：圖層 `"actor"`、序號在 `ClothSerialBase`～1100000 之間，新增 `IsAttachedLayer`）判斷：層旗標（`Renderer.InitialFlags`，來源是 cloth 自己的 `104.attr`）有 8 或 0x80 時傳回原值，不加半透明旗標 1。本體與影子不變；隊伍外的隱身他人仍整組不畫。

### 測試
- 新檔 `engine/source/OwnFishingLayerTests.cs`（`[WorldModuleTest("own-fishing-layer")]`，自動發現；由 CI 的 `--module-tests` 執行）。合成 list.spr／影格／封包，不讀客戶端或 `engine/assets`：
  - 觀察器自動註冊（158、119）、他人的 71 不算、自己的 71 記下；舊的戰鬥不影響、新的戰鬥結束；下一個 S158 動作、移動、自己的 S119、Reset 都會結束；截斷封包不記；快照。
  - 釣魚當格＝3225-(304＋朝向)、依 ticks 前進、走路／死亡／外型沒有動作 71 時回 null；本體 4 塊＋6262 2 塊、ObjectId 0、位置正確；缺 6262 子檔時附加層整組不畫。
  - 附加層序號範圍；自己隱身時本體、影子、層旗標 0／0x100 半透明，層旗標 8／0x80 不半透明；隊伍外他人整組不畫；隊伍成員同自己；解除隱身後不動。

## 怎麼驗證
- `engine/tools/check-no-game-assets.py`：pass（4575 個追蹤檔，0 錯誤）。
- **本機編譯沒有成功，原因和本次修改無關**：`dotnet build engine/source/OfflineClient.csproj`、`rebuild.py --build --configfile .github/nuget.ci.config`、CI 交叉建置參數＋全新 `--packages` 都停在 NETSDK1130（WebView2 `Core.winmd`），和 #910／#914 記錄的本機環境問題相同。編譯與測試以 PR 的 CI 為準。
- 依本機執行限制，沒有執行任何編譯出來的程式（含 `--module-tests`）。

## 未驗證什麼（待驗證）
- CI 編譯、既有測試與 `own-fishing-layer` 結果：看 PR 的 CI。
- 真機外觀（M31）：自己釣魚時換釣魚姿勢、看得到釣魚線；自己隱身時釣魚線不半透明（screen 照畫）。
- **推測**（引擎規則，原版是 +action 被蓋掉）：釣魚的結束條件（下一個 S158、格座標改變、更新的戰鬥、自己的 S119、死亡）；轉向不結束；動作 71 播完重頭循環（沿用 #922 的推測）。
- 自己的釣魚沒有呼叫 `ObserveOwnerFrames`（動作 71 沒有已知的 `]` 子特效）；他人分支照舊。
- 本體自己的 104.attr 有 8／0x80 時是否也不半透明：FU14 只寫逐層放（`sub_A463C0`），本體照舊加 `|1`，沒有改。
- 沒有新的資源宣稱「沒有」，所以沒有另外跑 PakViewer（6262／3225 子檔已由 #922 查過）。

## 產出路徑
- `engine/source/OwnActionState.cs`（新）
- `engine/source/OwnFishingLayerTests.cs`（新）
- `engine/source/ObjectRendering.cs`（自己的分支）
- `engine/source/Invisibility.cs`
- `docs/reports/issue-985.md`
- 分支 `task/985`；沒有改 `rebuild-lock.json`、`WorldSession.cs`、`Program.cs`、`DemoChecks.csproj`。

## 給下一棒的提示
- 下一輪 gap-matrix 盤點：5:`sub_87C890` 列的①②都已接上，可以改回「已有」，外觀等 M31 smoke。
- `smoke-m7.md` 可以加：P24Lab 測試角色釣魚 → 自己換姿勢、有釣魚線；隱身（GM）釣魚 → 身體半透明、釣魚線不半透明。
- CI 若 `own-fishing-layer` 失敗，先看 `OwnFishingDraw` 的朝向（S87 第 2 個 byte）與 `Appear` 欄位，再看主分派對合成 S119 的行為（測試已吞掉主分派的例外）。
