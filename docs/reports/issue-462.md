# Issue #462 — C3 開發工具獨立視窗

## 做了什麼

- 新增非模態、`Owner=ClientForm` 的 `DevToolsForm`，承載網頁面板、服務分頁、登入／選角、控制與狀態列。位置及大小存於 `engine/runtime/dev-tools-window.json`；關閉工具視窗只隱藏，保留 WebView2 與輸入內容，主視窗關閉時才釋放。
- `Ctrl+Shift+F9` 開關開發工具、`Ctrl+Shift+F10` 回遊戲、`Ctrl+Shift+F11` 聚焦網頁輸入、`Ctrl+Shift+F12` 擷取證據。診斷用 F2／F3／F6／F7／F8 同樣需要 Ctrl+Shift，且不在 normalProduct 啟用。單獨 F1–F12 不再執行開發功能，既有遊戲 F5–F12 巨集保留。
- 焦點移入工具視窗沿用 `LeaveGameFocus`，返回主視窗沿用 `ReturnGameFocus`，清除移動與巨集按住狀態。登入分頁保留 Enter 的既有重複鍵／可登入檢查。
- 主視窗場景、HUD 寬度與滑鼠命中測試移除工具面板尺寸依賴。服務通知改為選取工具視窗中的既有服務分頁。
- `CaptureEvidence` 除原有 client／screen／state 證據外，另存 `*-logical.png`：獨立 800×600 場景畫布，不縮放主視窗截圖、不含開發工具，完成後還原攝影機 viewport。C2 尚未整合，這張圖目前是世界場景；後續原生 HUD 畫布合成須接到同一擷取入口。
- WebView2 的來源限制、資源攔截、host objects／DevTools／瀏覽器快捷鍵設定、權限拒絕、下載取消與新視窗阻擋全部保留。

**登入、選角、背包、魔法、NPC、交易、個人商店、能力點等現有操作，暫時由開發工具提供。這是功能過渡措施，不是原版行為；本任務不代表原版 UI 完成。**

必要的範圍補充：`PlayerSceneBounds` 和 HUD 尺寸實際定義於 `PlayerHudIntegration.cs`，因此最小修改該檔。`ClientNetwork.cs` 只更新其中既有 demo 測試呼叫，`WebTests.cs` 同步測試快捷鍵；不改網路協定。

## 怎麼驗證（已驗證）

全程在 `task/462` worktree 執行，未啟動產品入口、未登入遊戲或連接遊戲／DB 服務。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過；222 個來源、231 個鎖定輸入（第 1 次；同步 main 後見下方） |
| `rebuild.py --build --output scratch/issue-462/build` | product、audio、launcher 成功；0 錯誤，保留既有 MagicUi.cs 兩項 nullable 警告 |
| world-ui-tests | 278 項通過，含新增工具視窗與快捷鍵測試 |
| demo-tests 預設組 | 9,295 項通過 |
| demo-tests：map-continuity | 28,203 項通過 |
| combat-admission／item-status／afflictions | 1,686／409／479 項通過 |
| polymorph-cadence／buddies／item-transfer／doors | 380／496／400／648 項通過 |
| demo-tests/web-checks.cjs | 18 項通過 |
| web-resource-tests | 真正 WebView2 載入合成 PNG，bytes／MIME／decode／404／cache 全通過 |
| web-resource-tests `--product-page` | 實際 engine/web HTML／DOM／CSS／命令／圖示／關閉重開通過 |
| `engine/tools/check-no-game-assets.py` | 通過，errors 0 |

WebView2 測試在沙箱內逾時，改於正常本機權限執行後通過；測試專用瀏覽器資料及輸出仍在本 worktree 的 `scratch/issue-462/`。

新增測試不執行 ClientForm 建構子，只初始化基底 Form 及測試所需欄位；使用透明的獨立工具視窗，沒有產品 timer、音效或連線。涵蓋：所有 F 鍵與修飾鍵組合、工具 owner／控制項父層、開關與縮放時場景矩形不變、場景座標不被異窗控制項攔截、焦點清理、X 關閉後可重用、normalProduct 單獨 F 鍵無開發副作用、800×600 擷取與攝影機還原、視窗位置大小保存。

執行完整舊測試時同步修正兩個測試問題：

1. world-ui-tests 的舊 S76 斷言仍預期外觀 pending／目標清空，與既有 PolymorphChecks 的現行契約不同；改驗證立即套用外觀、保留目標身分及 TargetRevision 遞增，確保舊排隊動作失效。
2. demo-tests 是無訊息迴圈的非同步 console harness；建立 HUD 控制項會自動安裝 WinFormsSynchronizationContext，使後續 sender continuation 卡住。僅在測試入口停用 AutoInstall，完整預設組與 CI 群組隨後通過。

最終產品 DLL SHA-256：`5597354ee5c082026e9a5097f41fbdcca24f48093c3d4d2f838fc25ff3f2acf1`。來源 SHA-256 以重產的 `engine/_integration/dev/rebuild-lock.json` 為準。

### 第 2 次嘗試：修正 PR #501 CI 失敗（2026-11-08，runner claude-local）

- 原因：PR CI 在「與 main 合併後」的樹上跑 `rebuild.py --build`，當時 main 已新增來源檔（#258／#447／#480／#481 等：`Bookmarks.cs`、`BookmarkUi.cs`、`SoundRules.cs`…），本分支的 lock 只釘 222 個來源，結果 `REFUSED: sourceCount 222 != pinned source files 223`。不是本任務程式碼錯誤；確認沒有未入 git 的來源檔被計入。
- 處理：`git merge origin/main`，`rebuild-lock.json` 衝突取 main 版本後跑 `rebuild.py --update-lock` 重產（225 個來源、234 個鎖定輸入），未手動拼 hash；`docs/REPORT.md` 用 main 版本。
- 同步後重驗（皆在本 worktree，`scratch/issue-462/`）：

| 檢查 | 結果 |
|---|---|
| `rebuild.py`（唯讀核驗） | dev-inputs-verified，225／234 |
| `rebuild.py --build --output scratch/issue-462/build` | PASS；product DLL `7ef65425fb83d7aa01eef7aa48ae2c73694737ec582f19e211594e7e6a437c97` |
| world-ui-tests | 278 項通過 |
| demo-tests 預設組／map-continuity | 14,045／32,953 項通過（含 main 新增的檢查） |
| combat-admission／item-status／afflictions／polymorph-cadence／buddies／item-transfer／doors | 1,686／409／580／380／496／400／648 項通過 |
| web-checks.cjs | 18 項通過 |
| web-resource-tests（legacy 與 `--product-page`） | 皆通過 |

上表的 DLL SHA 取代上一段「最終產品 DLL SHA-256」。

## 未驗證什麼／推測

- 未做原生實機登入、遊戲操作、真實鍵盤跨 WebView2 視窗操作、不同 DPI／多螢幕、最小化及退出期間的端到端驗收；離線測試不能替代這些驗收。
- C2 尚未提供原生 UiRoot／UiPresenter；目前邏輯截圖為世界場景，未包含未完成的原生 HUD。
- 不以合成封包或測試畫布聲稱原版外觀／行為已驗收；本次不新增這類推測結論。

## 產出路徑

- `engine/source/DevToolsForm.cs`、`WebPanel.cs`、`ProductEntry.cs`、`Program.cs`、`PlayerHudIntegration.cs`。
- 相關測試：`engine/_integration/dev/world-ui-tests/DevToolsChecks.cs`、`Program.cs`、`engine/_integration/dev/demo-tests/Program.cs`，以及產品內 `ClientNetwork.cs`／`WebTests.cs` 的 demo 呼叫。
- `engine/_integration/dev/rebuild-lock.json`、本報告；未修改 `docs/REPORT.md`。
- 本機證據（不入 git）：`scratch/issue-462/` 下 `build/`、`world-ui.json`、`demo.json`、`demo-*.json`、`web-resource.json`、`product-page.json`、`web-demo.json`、測試 log 與 `logical-regression.png`。環境腳本 `run-tests.ps1`／`run-groups.ps1`／`run-product-page.ps1` 亦在該目錄。
- 本機素材僅建立唯讀使用的 `engine/assets` junction 指向原專案 `releases/r29/assets`；未複製 pak／map／sprite、未寫入封存，未移除任何 worktree／junction。

## 給下一棒的提示

1. Claude 審查 C3 後，由 C2 執行器接續原生 800×600 畫布；將 `CaptureLogicalScene` 接到 UiRoot 合成結果，避免另建一套擷取邏輯。
2. dot 依既有 P24Lab 授權驗證：登入／選角入口、背包及 NPC／交易分頁自動開啟、Ctrl+Shift+F9–F12、WebView2 編輯時切回遊戲、按住移動後切窗停止、F5–F12 巨集、關閉工具不改場景矩形、退出產品無殘留 WebView2。
3. 視窗大小與位置可在關閉工具後重開確認。遇到錯誤的儲存位置，修正本 worktree／部署副本的 runtime 設定；不要碰封存或正式客戶端。
