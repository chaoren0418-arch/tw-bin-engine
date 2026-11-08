# Issue #519 — T213：M7-M 門自動走近

## 做了什麼
依 `docs/reference/world-objects.md`（WO）第 1、3 節與 gap-matrix 第 7 節 M7-M：

- `engine/source/Doors.cs`（本組唯一可改的程式檔）
  - `LoginConnection.ToggleDoor(id, current, walking)`：
    - 門在 **1 格內（含斜角）** 直接送 C41（行為與原本相同，沿用呼叫端 `current` 閘門）。
    - 門在 1 格外：用 M7-L 的 `ApproachWalk`（`login.Approach.RunAsync`）走到 1 格內，**到達後只送一次 C41**（門的 x、y、id）。原版依據：`sub_F112F0` 模式 3 → `sub_5001C0` 類別 7／8 送 `C_OPEN`（WO §1、§3）。
    - 走近中的閘門：ApproachWalk 的 stamp（連線、世界、校正、外觀、速度、輸入版本）、門仍存在且未被破壞、位置未變、自身未死亡；若點擊時門是選取目標，選取被換掉也會停止。呼叫端 `current`（如 PlayerPointer 的「位置不變」閘門）只在開始時與 1 格內直接送時檢查——走動後位置必然改變，無法沿用。
    - 實際送出改為 `SendDoor`，在 1 格內才放行，送出佇列閘門加上死亡檢查。
  - 新增 `LoginConnection.DoorClickRange = 1`、`DoorNotice`（未送出原因／走近中／已送出）。
  - 新增 `LoginConnection.ObjectClicksBlocked`（= `World.OwnDead`，WO §1 `byte_13CE3FD`）：死亡時 `ToggleDoor` 一開始就擋下，不走也不送。此屬性公開，供其他物件點擊路徑（NPC、拾取等，由各自負責的組）沿用。
- 測試（新檔）：`engine/_integration/dev/doors-tests/DoorApproachChecks.cs`，接進 `demo-tests` 的 `door-approach` 模式（`DemoChecks.csproj` 加一行 Compile、`Program.cs` 加一個 case），CI `build.yml` 加步驟「Door approach contracts T213」。
- `engine/_integration/dev/rebuild-lock.json`：`rebuild.py --update-lock` 重產。

## 怎麼驗證
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` → `dev-inputs-verified`。
- `rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`（product、audio、launcher 皆 0）。
- 新測試 `door-approach`：PASS（T213 本身 34 項）。涵蓋：1 格內斜角直接送；遠處東向走 3 步、斜向走 2 步後各送一次 C41（座標＝門座標）；經 `PlayerPointerController.ActAsync` 點遠處門同樣走近再送；死亡時（近／遠／滑鼠）不走不送且提示「死亡」；走近途中死亡、門被移除（S120）、輸入取消、選取變更、門以同 id 換位置、門被破壞（S158/37）都立即停止、不送、事後不補送；未選取的門不受選取變更影響；無路徑不送並提示；到達後送出佇列中門消失不送。
- 既有測試：`doors`（648）、`pickup`、`map-continuity`、`combat-admission`、`item-status`、`afflictions`、`resurrection`、`item-transfer`、`hypertext`、`buddies` 全部 PASS。
- `engine/tools/check-no-game-assets.py` → pass（errors 0）。
- 全部使用合成封包、記憶體 sender、記憶體移動、合成地形；未啟動遊戲、伺服器或 DB。

## 沒驗證什麼
- 真機：未在 P24Lab 實際點門走近（需 `dot` 依授權執行）。
- 走近路線用引擎既有有界尋路（`PlayerPointerPath`），不是原版尋路器；與原版路徑是否逐步相同未驗證（ApproachWalk 已註明）。
- 原版模式 3「路徑終點等於目標位置時也送」的分支：門格本身通常不可走（關門），本實作只在進入 1 格內時送；終點觸發分支未另外處理。
- 類別 7 與 8 的差異、門類別 vtable 處理 28／29（WO §3 待驗證）未涉及；本組不做畫面。
- 死亡擋點擊只落在門路徑；NPC 對話、攻擊、拾取的死亡阻擋不在本組可改範圍（多數既有路徑已因 `MovementAvailable`／`GroundAvailable` 含 `!OwnDead` 而擋下，但沒有統一提示）。

## 產出路徑
- `engine/source/Doors.cs`
- `engine/_integration/dev/doors-tests/DoorApproachChecks.cs`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`engine/_integration/dev/demo-tests/Program.cs`
- `.github/workflows/build.yml`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-519.md`

## 給下一棒的提示
- `PlayerPointer.ActAsync` 點門時，`Notice` 仍是舊字串（「門操作已送出／未送出」）；更細的原因在 `login.DoorNotice`。要顯示走近中／死亡原因，由擁有 `PlayerPointer.cs` 的組改成讀 `DoorNotice`。
- `ToggleDoor` 新增可選參數 `walking`（預設不看）；UI 若有本機行走動畫狀態（如 `World.Walking`），可由呼叫端傳入讓走近等動畫停下再走下一步。
- 其他物件點擊路徑要對齊 WO §1 死亡阻擋時，可直接用 `LoginConnection.ObjectClicksBlocked`。
- 測試需要支援的自身外觀，因此 `door-approach` 模式自行建立 DoorChecks 同款合成 fixture（`scratch/issue-519/synthetic`）。
