# Issue #621 — T260：R2-Q 登入公告顯示（code，第 1 次嘗試）

依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-Q；`docs/reference/packetbox-windows.md` 第 5.3 節（PBW§5.3）；gap-matrix 第 2 節 op 179。

## 做了什麼

| 檔案 | 變更 |
|---|---|
| `engine/source/LoginConnection.cs` | 新增 `LoginNews` 確認閘門（`Parse`／`Acknowledgement`／`Begin`／`Confirm(revision)`／`Reset`）。S48 處理改為：解析公告 → `NewsConfirmationRequired` 為真時**暫停接收迴圈，等使用者確認**，確認後才送 C16（`Pad([16])`），並重設登入階段逾時（等待確認期間不計時）。`Cancel()` 與 `Run` 的 finally 會取消等待中的公告；`Snapshot()` 多一個 `newsPending`。對外：`NewsConfirmationRequired`、`NewsPending`、`NewsRevision`、`ConfirmNews(revision)`。 |
| `engine/source/ClientLogin.cs` | 正常操作（沒有 `demoPort`，或情境 `real-interactive`）設 `NewsConfirmationRequired=true`。`login.Changed` 時 `QueueLoginNews()` 以 `BeginInvoke` 開 WinForms 模態訊息框（標題「公告」、只有「確定」），按下後 `ConfirmNews(顯示時的 revision)`；會話換代或公告已被取代則不送。新增 `LoginNewsText.Display`（斷行統一 CRLF、空公告顯示提示字）。 |
| `engine/source/LoginNewsTests.cs`（新） | `[WorldModuleTest("login-news")]`，31 項，接 `--module-tests`。 |
| `engine/_integration/dev/rebuild-lock.json` | `--update-lock` 重產。 |

設計說明：
- 「通用模態訊息框」沿用引擎現有慣例（`BoardUi`、`CharacterManagementUi`、`ClanUi` 等都用 `MessageBox.Show(this,…)` 當模態確認框）。可改檔案範圍不含 `NativeUi/`、`UiPanelRegistry.cs`，所以沒有做原生繪製的 `sub_8E1E30` 外觀；行為（模態、只有確定、確定後才回 C16）照 PBW§5.3。
- 不存 `Capture\NewsNNNN.txt`（PBW§5.3 建議不必存檔；也符合「不寫專案外」規則）。
- 自動化情境（`LoginTests.Probe`、`--demo-port` 的 real-* 腳本）維持原本「收下自動回 C16」，因為預設 `NewsConfirmationRequired=false`，不會被模態框卡住。
- 用 revision 防誤確認：舊對話框按確定不會確認新會話或新公告。

## 怎麼驗證（已驗證）

- `py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`：`dev-inputs-verified`（306 來源檔）。
- `rebuild.py --build --configfile .github/nuget.ci.config`：`PASS dev`，productDll SHA `cc83bf9b…509467b`。
- `P11.ClientEngine.exe --module-tests`：28 組、1401 項、0 失敗；本組 `login-news` 31 項通過（S48 CP950 解析與錯誤長度／opcode 拒絕、C16 位元組、閘門等待／確認／重複確認／舊 revision／Reset 取消／後到公告取代、`LoginConnection` 預設自動確認與 `newsPending` 快照、顯示文字斷行）。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。
- 與 origin/main 同步：開工時落後 0 個 commit。
- 2026-11-09 同步 origin/main（PR #638 開出後 main 前進；只有 `rebuild-lock.json` 衝突，採 main 版本後 `--update-lock` 重產，332 來源檔）：`rebuild.py --build` PASS（productDll SHA `f62267cb…bcdae797`）；`--module-tests` 44 組、2211 項、0 失敗（`login-news` 31 項通過）；`check-no-game-assets.py` 0 錯誤。本機證據（不進 git）：`scratch/t621/module-tests.json`。

## 未驗證什麼

- **未做真機**：沒有啟動遊戲／伺服器，訊息框實際外觀、P24Lab 3.81 伺服器在使用者延後確認時是否會逾時斷線，都待驗證（需 `dot` 依授權在 P24Lab 實測）。
- 未經 TCP 迴路實跑「S48 → 等確認 → C16 → S178」整段（`Run` 只接受 42061–42063 埠，本任務禁止起伺服器）；迴圈內的等待邏輯只由程式審閱與閘門單元測試涵蓋。
- 確認鈕回呼 `sub_ECA950` 送 C_READ_NEWS 仍是 PBW 的**推定**；本實作沿用既有 C16。
- 外觀是 Windows 標準訊息框，不是原版 XML UI 訊息框。
- CI 結果見 PR。

## 產出路徑

- `engine/source/LoginConnection.cs`、`engine/source/ClientLogin.cs`、`engine/source/LoginNewsTests.cs`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-621.md`；分支 `task/621`

## 給下一棒的提示

- 真機 smoke（dot）：登入 P24Lab 後應先看到「公告」框，角色列表在按「確定」之後才出現；可刻意等 30 秒以上再按，確認伺服器不會斷線。若會斷線，再考慮在等待期間不擋 S178 或加逾時自動確認。
- 若之後要改用原生繪製的訊息框（`docs/design/native-ui.md`），只要把 `ClientLogin.cs` 的 `QueueLoginNews` 換成原生面板，再呼叫 `login.ConfirmNews(revision)` 即可；協定層不用改。
- 網頁登入（WebView2）目前不另外顯示公告；模態框擁有者是主視窗，兩種登入路徑都會被它擋住。
