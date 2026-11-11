# Issue #989 — T432：R12-E 公告板等待收尾

依據：`docs/reference/gap-matrix.md` 15.14.5／15.14.6（T427／#980）、`docs/reference/followups-15.md` §2.11。裁定：照原版做。分支 `task/989`。

## 做了什麼

| 項目 | 檔案 | 內容 |
|---|---|---|
| ① 等待期間清單窗滑鼠全部吃掉 | `NativeUi/BoardPanel.cs`（只改 `BoardListContent.Mouse`） | `[this+0x40]≠0`（`BoardPanelSet.Waiting`）時，清單窗 (0,0,238,350) 內的所有滑鼠事件都回 true、不動作：左鍵選取、左鍵雙擊閱讀、**右鍵雙擊關窗**都不做。窗外照舊不收（`sub_7CD080` 只收窗內）。 |
| ② 刪文後的等待 | `Board.cs` | 刪文 C153 送出後不再是 `submitted`，改成新狀態 `delete-wait`（對應原版的值 4）：清掉閱讀中的文章、**補送一次目前這頁的清單請求**（游標 0 → C10，否則 C23 加同一個游標，`BoardDeleteReload`），收到這頁的 S68 才解除；這次重抓不清掉上一頁歷史（`boardPageCommand="reload"`）。等待期間不能再送任何公告板請求（`CanAct=false`、`BoardRequest` 擋掉）；逾時和 list-wait 一樣變成 `timeout`，可以手動刷新。發文（C141）仍是 `submitted`，不受影響。 |
| `IsWaiting` | `NativeUi/BoardPanels.cs`（只改 `IsWaiting`） | 加入 `delete-wait`。 |
| 測試 | 新檔 `BoardWaitFollowupTests.cs` | `board-wait-mouse`、`board-delete-wait` 兩組（`[WorldModuleTest]` 自動發現）。 |

### 原版依據（已驗證，Capstone 靜態反組譯 TW1608262200.bin）

`sub_7CD490`（VA 0x7CD490／FO 0x3CC890）：
- `[+0x40]≠0` 或沒有選取列（`[+0x18]<0`）→ 不做事；作者／權限檢查沒過也不做事；
- 送 op 0xDE（222）：board `[+0xc]`、選取列的 topic；
- 在本機把該列從陣列移除（memmove）、列數 `[+0x14]` 減 1、選取設成 −1；
- 剩下的列不足一頁（`[+0x10]+8 > [+0x14]`）時：`[+0x1c]≠0`（伺服器還有下一段）→ 設 `[+0x40]=4`，送 op 6（board、游標 `[+0x1c]`）補抓清單；`[+0x1c]=0` 只調整頁首，不等待。
- 清 0 的地方是收到清單 `sub_7CD600`（FU15§2.11）。

### 推定／引擎對應

- 引擎**不在本機移除文章**（`docs/protocol/board.md`：刪文沒有 ACK，不推算結果），所以刪文後一律補抓目前這頁；原版只在「不足一頁而且還有下一段」時才補抓、才等待。這是引擎對應，不是原版逐行行為。
- 原版的 op 6 帶的是「下一段」游標；引擎重抓的是同一頁（同一個游標），因為引擎的頁面一律以伺服器回傳為準。

## 範圍外的修改（需要審查注意）

- `engine/_integration/dev/board-tests/BoardChecks.cs` 第 81–84 行：這幾行寫死了舊行為（刪文後 `submitted`、清單不能解除），而 CI 的 `map-continuity` 群組會跑這個檔案（`DemoChecks.csproj` 以 `../*-tests/**/*.cs` 收進來）。不改的話 CI 一定紅，所以只把這 4 行改成新規則（C153 後接 C10、`delete-wait`、文章回包不解除、清單回包解除），其他檢查沒動。

## 怎麼驗證

- 本機：`dotnet build engine/source/OfflineClient.csproj`。本機這台的 WebView2 套件還原有問題（NU1701 → NETSDK1130），跟本次修改無關：用 scratch 裡的 targets 拿掉 `.winmd` 參考後，剩下的錯誤只出在 `WebPanel.cs`、`WebLocalResource.cs`（WebView2 型別），本次改動的檔案沒有編譯錯誤。完整建置以 CI 為準。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4580 檔，0 錯誤）。
- 依本機規定沒有執行任何測試程式；`--module-tests`（含新的兩組）與 DemoChecks `map-continuity`（含 BoardChecks）交給 CI。
- CI（PR #998，run 42969576168）：`dotnet`、`map-continuity`、`npc-offline` 全部 pass；`board-delete-wait`、`board-wait-mouse` 各 29 項檢查，都通過，沒有失敗。

## 未驗證

- 真機：刪文後伺服器回的清單是否就是重抓的那一頁、晚到的清單是否會被游標過濾掉，要在 P24Lab 實測（待驗證，交給 Codex／dot）。
- `--classic-ui`（WinForms `BoardUi`）：沒改程式；它本來就靠 `CanAct` 停用按鈕，`delete-wait` 期間 `CanAct=false`，推定行為正確，沒有實測。
- 清單按鈕窗（658 X、Read、Delete 等，`BoardButtonWindow.Mouse`）不在本任務範圍；原版的按鈕回呼各自檢查 `[+0x40]`，引擎由 host／`BoardRequest` 擋掉送包，但 658 X 在等待期間仍可關窗（原版 `sub_A59AB0` 沒有檢查，推定一致）。

## 產出路徑

- `engine/source/Board.cs`、`engine/source/NativeUi/BoardPanel.cs`、`engine/source/NativeUi/BoardPanels.cs`
- `engine/source/BoardWaitFollowupTests.cs`（新）
- `engine/_integration/dev/board-tests/BoardChecks.cs`（4 行，見上）
- `docs/reports/issue-989.md`

## 給下一棒的提示

- gap-matrix 4.2 `BoardListWindow` 的 K5 兩項（15.14.5）程式端都做了，下一輪盤點可以改回「已有」；「刪文一律重抓」記成引擎取捨。
- 本機 Windows 建置的 WebView2 還原問題（NU1701／NETSDK1130）值得另開 `needs:local` 查：worktree 沒有 `vendor`，用 nuget.org 還原時 WebView2 1.0.4258.31 只解析出 .NETFramework 資產。
