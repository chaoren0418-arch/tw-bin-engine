# Issue #520 報告：T214 M7-N 幽靈本機限制

日期：2026-11-08。階段：code（第 1 次嘗試）。分支：`task/520`。
依據：`docs/reference/gap-matrix.md` 第 7 節 M7-N；`docs/reference/world-objects.md`（WO）第 4 節。前置 M7-L（#518）已合併。

## 做了什麼

| WO §4 對照表 | 原版（已驗證，除另註） | 改動前 | 改動後 |
|---|---|---|---|
| 3 離開入口 | HyperText 動作 `exitghost`（**推測**是 NPC 對話連結，HTML 在未解開的 `Text.pak`） | HUD 常駐「離開幽靈」鈕 | **拿掉按鈕**。`LoginConnection.ExitGhost` 與 C173 封包保留，給之後的 `exitghost` 入口用 |
| 4 本機限制：道具／技能快捷 | `byte_13CE51E!=0` 時快捷函式開頭就返回 | 不擋 | 幽靈中 HUD 快照標為 `Busy`：F5–F12 快捷槽（鍵盤與點擊）及抽屜的「使用一次」都不送；M（施法）、U（用道具）鍵擋下並在 HUD 顯示原因 |
| 4 本機限制：自動攻擊 | 自動攻擊／移動相關計時函式開頭就返回 | 不擋 | G 鍵（單次攻擊）與 Web 面板的 `attack` 指令擋下，不送 C229／C123 |
| 4 本機限制：F 鍵瞬移 | `teleport_F%d.txt`（`sub_8BA150`） | 引擎**沒有這個功能** | 無需處理（線上模式 F 鍵沒有動作） |
| 4 本機限制：拾取 | 同上 | — | M7-L（#518）已在 `Ground.cs` 擋，本張不動 |
| 4 門、NPC、移動 | 幽靈時仍可點 | — | 不擋，交原服判定 |
| 5 幽靈中自身外觀變更 | `sub_A43B50` 對自己直接略過 | 照收 | **只做判斷** `WorldSession.GhostSkipsSelfAppearance(id)`；呼叫點在 `Polymorph.cs`（S76）／`Recovery.cs`（S85），不在本張可改範圍，**尚未接上**（見下方） |

檔案：
- `engine/source/GhostMode.cs`：移除按鈕與 `ClientForm.ExitGhostFromHud`；`GhostModeHud` 改成無狀態的 `Apply`（幽靈且 stamp 相符時 `Busy=true`、狀態列「幽靈模式 · 快捷與攻擊停用」）；新增 `LoginConnection.GhostLocalBlocked`（InWorld 且自身 S87 有 `0x80`，只看當前狀態、不留本機旗標）、`GhostLocalLimits.Notice`、`WorldSession.GhostSkipsSelfAppearance`；提示文字改為「本機停用快捷、攻擊與拾取；門、NPC 與移動交原服判定」。
- `engine/source/OnlineInput.cs`：M／U／G 鍵與 Web `attack` 的幽靈閘門（在 `ClearInput` 與送出之前返回）。
- 測試（新檔）：`engine/_integration/dev/ghost-limits-tests/GhostLimitsChecks.cs`，接進 `DemoChecks.csproj`（一行 Compile）與 `Program.cs`：新增 `ghost-limits` 模式，並加入 `ghost-mode`、`map-continuity`（CI 會跑）與預設入口。沒有改 `build.yml`。
- `engine/_integration/dev/rebuild-lock.json`：`rebuild.py --update-lock` 重產。

## 怎麼驗證（已驗證）

- 建置：合併 `origin/main`（`8f83f4d`）後 `rebuild.py --update-lock`、`rebuild.py --build` → `PASS dev`。worktree 沒有 `engine/vendor`，建置期間暫時以 junction 唯讀指向原專案 vendor，結束後已用 `cmd /c rmdir` 拆除。
- 本組離線測試 `DemoChecks … ghost-limits` → PASS（新增 35 項）：幽靈中 M／U／G 不送封包、HUD 顯示原因、清掉按住的鍵；Web 攻擊幽靈中不送且同步返回；非幽靈不受限；原服 S87 清 `0x80` 後立即解除、重新進入再套用、取消連線後解除；HUD 快照幽靈時 `Busy`、非幽靈／無資料／stamp 過期時原樣返回；`GhostModeHud` 無狀態、`ExitGhostFromHud` 已移除；真的 `PlayerHudView.ActivateSlot` 在幽靈快照下不觸發、不忙時會觸發（對照）；自身外觀略過判斷只對幽靈中的自己成立。
- 既有測試：`ghost-mode` 721、`map-continuity` 33018、`combat-admission`、`item-status`、`afflictions`、`resurrection`、`polymorph-cadence`、`buddies`、`item-transfer`、`doors`、`pickup`、`hypertext` 全綠；`--module-tests` pass（1 組 39 項）；`check-no-game-assets.py` pass（errors 0）。
- 途中發現：新測試裡原本的 `await Task.Yield()` 在 `map-continuity` 中會卡住（前面的測試建立 WinForms 控制項後，執行緒有 `WindowsFormsSynchronizationContext`，續行排進沒人處理的訊息迴圈）。已拿掉，並改為斷言幽靈路徑同步完成。拿掉新呼叫的基準版同時確認通過（32983 項）。

## 沒驗證什麼

- **非幽靈的對照送出**（鍵盤 G／Web 攻擊實際送 C229）沒有在本組測試：需讀 `engine/assets` 外觀表，CI 沒有素材；鍵盤非幽靈路徑還需要完整 Form（`ClearInput`）。改以閘門旗標與 HUD `ActivateSlot` 對照代替。幽靈中鍵盤移動照常同理未測（閘門只套 M／U／G，移動不經過）。
- **滑鼠點怪攻擊**（`PlayerPointer.cs` 的 `PointerAttack`，單次攻擊）沒擋：檔案不在本張範圍。原版點擊（`sub_5001C0`）在幽靈＝2 時放行，攻擊由自動攻擊計時擋；引擎對應需改 `PlayerPointer.cs`。
- 抽屜的「使用一次」與舊版 `InventoryUi`／`MagicUi` 按鈕、Web 面板的 `use`／`cast`：HUD「使用一次」因 `Busy` 一併停用（比原版可能更嚴；原版道具視窗直接使用在幽靈中是否被擋**未查證**）；舊版 `InventoryUi.cs`／`MagicUi.cs`／`WebPanel.cs` 的按鈕不在範圍，沒擋。
- 自身外觀略過**未接上**（見上表 #5）。
- 真機：未啟動遊戲、伺服器或 DB。HUD 排版（按鈕列少一顆）沒有看畫面。

## 產出路徑

- `engine/source/GhostMode.cs`、`engine/source/OnlineInput.cs`
- `engine/_integration/dev/ghost-limits-tests/GhostLimitsChecks.cs`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`engine/_integration/dev/demo-tests/Program.cs`、`engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-520.md`

## 給下一棒的提示

- **自身外觀略過**：在 `Polymorph.cs` `ReceivePolymorph` 自身分支開頭、`Recovery.cs` `ReceiveRecovery` 自身分支（換 gfx 前）呼叫 `if(GhostSkipsSelfAppearance(id))`，略過 `Actor with{Gfx=…}` 與 `AppearanceRevision++`。要決定其餘副作用（取消動作、清死亡旗標）是否一併略過——WO 只說外觀變更「對自己無效」，建議只略過 gfx／weapon 那一段，其他照舊。需要由擁有這兩個檔案的組別接。
- **`exitghost` 入口**：`NpcInteraction.cs` 已有 HyperAct 名稱表註解（`checkpk` 同表）。拿到含 `exitghost` 連結的 HTML 證據後，比照 `checkpk` 在 HyperText 動作分派中呼叫 `login.ExitGhost(login.GhostModeSnapshot())`；`ExitGhost` 已有 15 秒防重送與 stale fence。
- **點怪攻擊**：`PlayerPointerController.ActAsync` 的攻擊分支（非 talk、非門）可加 `login.GhostLocalBlocked` 檢查；門與交談不要擋。
- `docs/protocol/ghost-mode.md` 第 28 行與 `ghost-mode-tests/README.md` 仍描述 HUD「離開幽靈」按鈕，屬文件更新，本張未改（不在可改檔案）。
- DemoChecks 新測試避免 `await Task.Yield()`／真的非同步等待：`map-continuity` 中執行緒有 WinForms 同步內容，會卡住。
