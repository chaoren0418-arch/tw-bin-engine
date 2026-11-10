# Issue #741 — T314：R5-I `QUIT_WINDOW` 備援

依據：`docs/reference/gap-matrix.md` 第 14.6 節 R5-I、第 13.6 節建議；PBW§4／§5；第 1 節 op 89；第 3 節 C_DEAD_RESTART；第 4.2 節 `QuitWindow`。

## 做了什麼
- **新檔 `engine/source/QuitFallbackUi.cs`**：原生 UI 關閉時的 `QUIT_WINDOW` WinForms 暫行窗。
  - `QuitFallbackView`：顯示條件 `Wanted(沒有可用原生面板, 目前窗內容, 使用者關掉的 revision)`、標題與三鈕文字（重新開始／離開遊戲／取消）、說明列（斷線後／死亡中／存活三種）。
  - `ClientForm.SyncQuitFallback`：讀 `LoginConnection.QuitWindowSnapshot()`；「沒有可用的原生 QUIT_WINDOW」＝原生 UI 關閉，或面板沒註冊、已故障（`NativeQuitWindowActive`）。開窗點不變：死亡逾時送 C122 後的 `OpenQuitWindow`（PBW§4 唯一已驗證的開窗點；FU3§5.1 ESC／選單不開，所以沒有加其他入口）。
  - `QuitFallbackWindow`：不搶焦點（`ShowWithoutActivation`）；標題列 X、取消鈕、Esc 都＝只關窗（與原生 sub_A59AD0 相同，不送封包）；同一次開窗被關掉後不會再自動跳出，下一次開窗（新 revision）才再出現。
- **`engine/source/Recovery.cs`**：把原生 `QuitWindowSource` 的三個動作搬成 `ClientForm.QuitWindowRestart／QuitWindowQuit／QuitWindowClose`，原生面板與暫行窗共用同一套（依 `QuitWindowRules`：死亡 C177、存活 C7 選角、戰鬥中訊息 130／131、在世界中 C122 後關程式、斷線後重新開始回登入、斷線後離開直接關程式）；行為與原本完全相同。`SyncRecovery` 每 tick 多呼叫一次 `SyncQuitFallback`。
- **新檔 `engine/source/QuitFallbackTests.cs`**（`[WorldModuleTest("quit-fallback")]`，自動發現）：接線（反射）、顯示條件與文字、暫行窗 revision／取消／按鈕轉呼叫、以假時鐘＋假送出閘門跑「死亡→逾時送一次 C122→暫行窗應出現（原生可用時不出現）→取消關窗且不送封包」。
- 沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`、`NativeUi/QuitWindowPanel.cs`。

## 怎麼驗證
- 已驗證：`py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile ../.github/nuget.ci.config`（worktree 沒有 `vendor`，改用 CI 的 NuGet 設定）→ product／launcher restore、build 皆 0，`PASS dev`（lock 為暫時 lock，未寫入）。
- 已驗證：`engine/tools/check-no-game-assets.py` → `status: pass, errors: 0`。
- 待驗證（依本機限制不在本機執行）：`--module-tests` 的 `quit-fallback` 與既有單元測試 → 看 PR 的 GitHub Actions CI。

## 未驗證什麼
- 真機：死亡後等 501,264 ms 逾時，暫行窗是否出現、三個鈕的實際行為（需開遊戲，交給 Codex／dot 在 P24Lab 測試角色）。
- 暫行窗是引擎畫面，不是原版外觀（原版底圖 1951 等只在原生 UI 下畫）。
- 原生 `QuitWindowPanel` 仍未實作 `IUiClosablePanel`（不在本張可改檔案內）。

## 產出路徑
- `engine/source/QuitFallbackUi.cs`、`engine/source/QuitFallbackTests.cs`、`engine/source/Recovery.cs`、本報告。
- 分支 `task/741`。

## 給下一棒的提示
- gap-matrix 第 4.2 節 `QuitWindow`、第 1 節 op 89 可在 CI 綠後更新：原生 UI 關閉時已有 WinForms 備援（`QuitFallbackUi.cs`）；13.6／14.6「仍是代價」清單可移除 `QUIT_WINDOW`。
- 若要讓原生 `QuitWindowPanel` 實作 `IUiClosablePanel`，另開小任務（只動 `NativeUi/QuitWindowPanel.cs`）。
- 真機驗收步驟：P24Lab 測試角色死亡 → 不按重新開始等逾時 → 確認狀態列「斷線。」與「離開遊戲」暫行窗；分別試取消（窗關、無封包）、重新開始（回登入）、離開遊戲（關程式）。
