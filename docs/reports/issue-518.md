# Issue #518 報告：T212 M7-L 拾取對齊原版

日期：2026-11-08。階段：code（第 1 次嘗試）。分支：`task/518`。
依據：`docs/reference/gap-matrix.md` 第 7 節 M7-L；`docs/reference/world-objects.md`（WO）第 1–2 節；`docs/reference/rea-inventory.md` B07〈點擊流程〉。

## 做了什麼

| WO §2 對照表 | 原版（已驗證） | 改動前 | 改動後 |
|---|---|---|---|
| 1 距離 | 1 格（含斜角），超過就先走過去再撿 | 3 格內才送，不走 | 點物品時距離超過 1 格就**自動走近**（新檔 `ApproachWalk.cs`），到 1 格內才送；低階送出閘門也改成 1 格 |
| 2 數量 | 單個，或「全部拾取」開著時送整疊；否則跳數量提示（空白＝整疊，0 或超過不送） | 文字框預設 1 | `GroundRules.DirectPickupCount`／`TryPromptCount`；新增 `WorldSession.PickupAll`（全部拾取選項）與 `PickupPrompt`（數量提示） |
| 3 鍵盤拾取 | `Action_Loot`：1 格內第一個物品、整疊、不問、不走 | 選取物品＋文字框數量 | P 鍵改呼叫 `LoginConnection.LootNearby()` |
| 4 節流 | 986 ms，旗標 `0x10000` 時 600 ms；未過就丟棄、不排隊 | 900 ms | `GroundRules.PickupThrottle`；`GroundClickGuardMs`＝986；加速時 600 ms |
| 5 轉向 | 送出前就在本機轉向物品 | 收到 S158/15 才轉 | 送出被接受的那一刻呼叫 `WorldSession.FacePickupTarget`（腳下物品不轉） |
| 幽靈 | `byte_13CE51E` 不為 0 時不拾取 | 沒擋 | `LoginConnection.PickupAvailable`；點擊、鍵盤、明確數量三條路徑都擋 |

檔案：
- `engine/source/ApproachWalk.cs`（新）：`ApproachRules`（WO §1 的距離判定 `Within`、走到距離內就停的 `Plan`、方向換算）與 `ApproachWalk`（一次只走一個目標；本機輸入取消、位置校正、世界／連線／外觀／速度變更、目標消失都會停止，不會重送）。`LoginConnection.Approach` 是 M7-M（門）要用的入口：`Approach.RunAsync(目標座標, 距離, 目標仍有效, 到達後動作)`。
- `engine/source/Ground.cs`：上表的規則、`PickupGround`（點物品流程）、`AnswerPickupPrompt`／`CancelPickupPrompt`、`LootNearby`、幽靈與 1 格閘門、986／600 ms 節流、送出前轉向；快照加 `pickupAll`、`pickupPrompt`。
- `engine/source/GroundUi.cs`：拾取鈕改成「拾取選取物品（自動走近）」；新增「全部拾取」勾選框；數量文字框只在提示開著時啟用，Enter 送出、Esc 取消；Web 面板的拾取在提示開著時當作回答提示，否則走 `PickupGround`；P 鍵（`NativeGroundQuantity(true)`）改成 `LootNearby`。新增 `PickupGroundClicked(id)` 給滑鼠點擊用（見下方「未完成」）。
- `PickupPresentation.cs`：不需修改（S158/15 收到時才擷取 owner，本機先轉向不影響）。
- 測試（新檔）：`engine/_integration/dev/pickup-tests/PickupChecks.cs`，接進 `demo-tests` 的 `pickup` 模式（`DemoChecks.csproj` 加一行 Compile、`Program.cs` 加一個 case），CI `build.yml` 加一個步驟「Pickup alignment contracts T212」。
- `engine/_integration/dev/rebuild-lock.json`：`rebuild.py --update-lock` 重產。

## 怎麼驗證（已驗證）

- 建置：`rebuild.py --update-lock` 後 `rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`（product、audio、launcher 都 0）。本機 `engine/NuGet.Config` 的離線 vendor 在 worktree 中沒有，所以用 CI 的 nuget 設定 restore。
- 本組離線測試：`DemoChecks … pickup` → `PASS`。內容：1 格判定含斜角；986／600 ms；整疊與提示規則（空白＝整疊、0／超過不送、提示期間數量變了不送舊快照）；1 格內單個直接送，送前轉向（東、南、西北、西南）；節流未過不送、不會事後補送；全部拾取開時多個也直接送；幽靈擋三條路徑；鍵盤拾取只撿 1 格內第一個、整疊、不走；遠處物品走 3 步到 1 格內再送一次；走近後多個仍跳提示；走近中途輸入取消、物品移除、取消選取、變幽靈都會停止且不送；沒有路徑時不送；明確數量路徑距離門檻改成 1 格。
- 既有測試：demo-tests 的 CI 模式 `map-continuity`、`combat-admission`、`item-status`、`native-ui-resources`、`afflictions`、`resurrection`、`polymorph-cadence`、`buddies`、`item-transfer`、`doors` 全綠；另跑 `slow-actions`、`ghost-mode` 綠；`npc-tests` 綠（165）；`ground-tests`（以唯讀方式指向原專案 `engine` 與 `D:\lineage開發\0-客戶端`）綠（596 assertions／13 組，含 3000 ms 未確認鎖定、986 ms 間隔）。
- `engine/tools/check-no-game-assets.py` → pass（errors 0）。
- CI：見 PR。

## 沒驗證什麼／限制

- **沒有真機**：沒啟動遊戲、伺服器或 DB。實際走近手感、伺服器是否接受走近後的 C112，要等 smoke。
- `demo-tests combat-loop` 在本 worktree 失敗，原因是 worktree 沒有 `engine/assets`（`integrity.json` 找不到），與本次修改無關，也不在 CI。
- **推測的部分**：旗標 `0x10000` 被當成「加速」（WO §2 標推測），程式用「自身速度＝1（加速）」對應 600 ms；若查證不是加速，只要改 `LoginConnection.PickupFastFlag`。
- **沒做的原版行為**：
  - 牆阻擋（WO 表第 6 項，`sub_F12490` 相鄰格旗標 bit 0 語義是推測）：沒做。
  - 原版「不等回報」：引擎仍保留「送出後等 S158/15，最長 3 秒」的鎖（既有測試依賴、也用於確認轉向），所以連續拾取比原版多一段等待。
  - 數量提示的原版視窗（`sub_4FD2F0`、字串表 121）版面沒查證，暫用地面面板的文字框＋提示文字，不宣稱是原版畫面。
  - 鍵盤拾取的掃描順序：原版是「可見物件順序」，引擎用 ObjectId 排序（待驗證）。
  - `C_GET` 最後一個 byte（`sub_9E4680`）：伺服器不讀，維持原本 C112 格式。
- 舊的 `--ground-regression`（`engine/source/GroundTests.cs`，不在 CI）有一段把物品放在 2 格外仍期待送出，距離改成 1 格後那段會失敗；該檔不在本任務可改範圍。

## 未完成（需要其他檔案）

- **滑鼠雙擊地上物品**仍走 `PlayerPointerController.PickupAsync` → `PointerPickup(1)`（數量固定 1、不走近），因為 `PlayerPointer.cs`／`PlayerPointerUi.cs` 不在本任務範圍。要對齊原版，只需把 `PlayerPointerUi.cs` 第 19 行的 `Pointer.PickupAsync(target)` 改成 `PickupGroundClicked(target)`（`GroundUi.cs` 已提供），並同步調整 `slow-actions` 中 `pointer-pickup` 的預期（目前預期送 1 個）。原版是單擊物件即互動（`sub_F112F0`），是否改單擊也一併決定。
- 「全部拾取」在原版是動作列 `Action_LootAll`／HyperText `HyperActGetAll`／選項頁 `option13`；引擎目前只有地面面板的勾選框。

## 產出路徑

- `engine/source/ApproachWalk.cs`、`engine/source/Ground.cs`、`engine/source/GroundUi.cs`
- `engine/_integration/dev/pickup-tests/PickupChecks.cs`（`demo-tests` 的 `pickup` 模式）
- `.github/workflows/build.yml`（新增 CI 步驟）、`engine/_integration/dev/demo-tests/{DemoChecks.csproj,Program.cs}`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-518.md`

## 給下一棒的提示

- **M7-M（門自動走近，`Doors.cs`）**：直接用 `login.Approach.RunAsync(new Point(門.X,門.Y), 1, ()=>門仍存在, ()=>login.ToggleDoor(...))`。距離判定請用 `ApproachRules.Within`。WO §1 提到類別 14（gfx 10445–10447）門檻是 12 格、類別 6／12／15／18 依武器射程，`RunAsync` 的 `range` 參數已經可以傳。
- **M7-N（幽靈，`GhostMode.cs`、`OnlineInput.cs`）**：拾取的幽靈阻擋已在 `LoginConnection.PickupAvailable`／`SendGround` 完成，不用再處理。P 鍵在 `OnlineInput.cs` 仍呼叫 `NativeGroundQuantity(true)`，現在等於 `LootNearby()`。
- 測試要換掉 movement 或地形時，`ApproachWalk` 建構子有 `move`／`terrain`／`available`／`wait` 參數（只給離線測試用），用反射設 `LoginConnection.Approach`。
- S158/15 需要角色外觀 profile（assets），離線測試要釋放「等待回報」可以呼叫 `WorldSession.InvalidatePickupRequest`（見 `PickupChecks.cs`）。
