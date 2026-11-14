# Issue #1164：UIA 測試工具修正（#1150 G04／P01）

**結論：通過（本張工具驗收 ✅ 10／❌ 0／⚠️ 0）。** 只啟動記事本；客戶端上的 G04／G07／F01／P01／R07 實證仍交 `f1150-retest`，本報告不改寫 #1150 的失敗結果。

2026-11-14 08:35–08:38（台北）完成記事本實機測試，隨後完成單元與靜態驗證。分支 `task/1164`，作業目錄 `D:\lineage開發\TW1608262200研究\scratch\wt\task-1164`。開始時 fetch 後與 `origin/main` 相同；`p24lab` lease 持有人核對為本張，期限為 `2026-11-14T02:02:38Z`。沒有啟動客戶端、Java、MySQL，沒有連線 42061／43324，也沒有修改 DB、登錄檔、系統設定或持久 execution policy。

## 逐項結果

表內路徑相對本 worktree 的 `scratch/issue-1164/`；精簡原始結果、錯誤、來源與圖片 SHA-256 收錄於 [issue-1164-evidence.json](issue-1164-evidence.json)。PNG 留在本機，已目視確認記事本文字、另存新檔與取消後空白主窗。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| G04-K | ✅ 通過 | 新函式回傳 UTC；5 筆 JSONL 共 6 個 SendInput 事件：Ctrl down、Shift down、F12 down/up、Shift up、Ctrl up；每筆 PID／前景 PID 相同、送出數等於要求數。舊名靜態確認轉呼叫新函式 | 自啟空白記事本 → `Send-UiaEvidenceKey` → 查原生送鍵紀錄／截圖；本項不要求記事本產生 manual PNG | `helpers/helpers-result.json`、`helpers/evidence-key.jsonl`、`helpers/evidence-key.png` |
| G04-F | ✅ 通過 | 嚴格晚於 UTC 且名稱符合者才列出；舊檔、同時間、其他名稱均排除 | `Test-UiaContracts.ps1` 的時間篩選 fixture；純單元測試，不是假冒截圖 | `contracts/contracts-result.json` 的 `evidence-time-filter` |
| P01-W | ✅ 通過 | PID 19124 的「另存新檔」HWND 13895864 可由 IncludeOwned 唯一取得；頂層舊模式 found 0 | 空白記事本 Ctrl+Shift+S → 觀察前景標題 → 同 PID／精確標題 `Get-UiaWindow -IncludeOwned` | `owned/helpers-result.json`、`owned/dialog-tree.json`、`owned/dialog.png` |
| P01-R | ✅ 通過 | 關閉瞬間得到 4 個可讀節點＋1 個 Error，整次讀樹不拋錯；關閉後保留 null Name 錯誤；主窗仍讀到 27 節點。fixture 的 ElementNotAvailableException 原文、null ControlType 與可讀 sibling 均保留，Find 可回傳唯一可讀元素 | 對話框 Post WM_CLOSE 後立即 Get-UiaTree，200 ms 後讀失效 root；另跑 Find／Tree fixture | `owned/closing-tree.json`、`owned/stale-tree.json`、`owned/after-cancel-tree.json`、`owned/after-cancel.png`、`contracts/contracts-result.json` |
| P01-I | ✅ 通過 | 記事本「不要儲存」Invoke 正常返回；同一 MTA 等待核心以 25 ms deadline 測得 29 ms 返回 TimeoutException，350 ms 工作只執行一次且可在逾時後完成；預設 5 秒與提醒文字靜態核對 | 既有記事本回歸；單元測試以反射測實際等待核心，不啟動假 GUI、不模擬客戶端成功 | `baseline/notepad-result.json`、`baseline/notepad-close-tree.json`、`contracts/contracts-result.json` 的 `invoke-deadline-no-retry` |
| P01-T | ✅ 通過 | 雙方名稱一致且 IsSelected=true 才通過；錯名稱、未選取、多選、provider 錯誤均拒絕並附兩側觀察／原文；每次只 Select 一次，無退路 | `Select-UiaTab` 單元 fixture；本機 Windows 10 記事本沒有分頁，本項不是遊戲分頁實機驗證 | `contracts/contracts-result.json` 的 `tab-selection-dual-readback` |
| P01-D | ✅ 通過 | README G04 改用新函式、註明單鍵 F12 不是證據鍵，列出所有新增用法；#1150 成功／失敗表原樣保留 | 檢查 README diff 與新用法表 | `work/f1118-uia-path/README.md`（repo 相對路徑） |
| N01 | ✅ 通過 | 原 `Test-Notepad.ps1` 退出 0；精確讀回 `uia-1138 測試輸入 OK`，目視文字可讀，UIA 不儲存關閉 | 使用既有腳本、新 PowerShell 5.1／STA 執行 | `baseline/notepad-result.json`、`baseline/notepad.png` |
| Q01 | ✅ 通過 | 5 個 PowerShell 腳本解析零錯誤；git diff --check 通過，修改限指定工具與本張報告 | Parser.ParseFile 掃描工具目錄；檢查 git diff | Git 證據 `Parse`；`scratch/issue-1164/verify.ps1`（repo 相對路徑） |
| Q02 | ✅ 通過 | 自啟記事本 PID 15020、20312、19124 均正常退出，ForcedClose=false；對話框取消、沒有存檔 | 各腳本 finally 關閉並 WaitForExit；末次依記錄 PID 檢查均不存在 | Git 證據 `ProcessCleanup` |

開發驗證初次在對話框剛出現時，owned descendants 列舉遇到 `ElementNotAvailableException`；錯誤完整保留在 `InitialOwnedDiscoveryError`。工具補上 Window 型別條件與逐候選錯誤紀錄，測試等待對話框發佈 UIA 樹後，只補驗未完成的 P01-W／P01-R，沒有重送已通過的證據鍵或重跑 N01。未修改引擎程式。

## 做了什麼

- `UiaGdi.ps1`：Ctrl+Shift+F12／UTC 與新檔篩選、相容舊名、owned Window 查找與唯一性、逐元素容錯／原文保留、預設 5 秒背景 MTA Invoke、Select-UiaTab 雙向核對。加入 UTF-8 BOM，確保 Windows PowerShell 5.1 正確解析中文提醒。
- `README.md`：同步 G04 與函式用法，明確區分歷史客戶端實證、本輪記事本實證及單元 fixture。
- `Test-UiaHelpers.ps1`／`Test-UiaContracts.ps1`：可重現本輪工具驗證，暫存與證據均由參數指定到 worktree scratch；腳本不含帳密。
- 本報告與 `issue-1164-evidence.json`：逐項結果及來源 SHA-256。大型 PNG／完整樹留在上述 scratch 路徑，沒有新增時間戳狀態檔。

## 怎麼驗證

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-Notepad.ps1 -EvidenceDirectory scratch/issue-1164/baseline
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-UiaHelpers.ps1 -EvidenceDirectory scratch/issue-1164/helpers
# 只補驗對話框兩項；沿用上一行已通過的組合鍵結果。
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-UiaHelpers.ps1 -EvidenceDirectory scratch/issue-1164/owned -SkipEvidenceKey
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-UiaContracts.ps1 -EvidenceDirectory scratch/issue-1164/contracts
```

前兩個原生測試分別留下退出 0 與開發中列舉例外；補驗對話框退出 0，單元測試退出 0，原生測試結果未覆蓋。`-ExecutionPolicy Bypass` 僅限該次程序；Add-Type 暫存使用 worktree 下的 TEMP／TMP。未設定 DOTNET_CLI_HOME、未建置或執行引擎。

## 未驗證與給下一棒的提示

1. `f1150-retest` 在引擎另兩張 code 任務合併後，以 `Send-UiaEvidenceKey`＋`Get-UiaNewEvidence` 重測 G04，逐張看三張新 manual PNG。記事本送鍵成功不代表客戶端擷取成功。
2. 開發工具用 `Get-UiaWindow -ProcessId ... -Title ... -IncludeOwned`；G07 使用 `Select-UiaTab $tab '登入／選角'`，若兩邊讀回不同，保留錯誤及 GDI 圖，不改用鍵盤／座標補成通過。
3. F01 使用預設 5 秒 Invoke；任何逾時都可能已開窗，先核對前景／截圖，**不要重送**。本張只驗證等待核心與記事本正常 Invoke；未製造客戶端 provider 卡住場景。
4. Get-UiaTree 的容錯處理失效／null／已返回的例外，**不限制永不返回的 FindAll 呼叫**；由引擎 modal 修正與後續客戶端實證處理 #1150 的阻塞。Invoke 逾時只停止等待，不撤銷已送出的 UI 動作。
5. 調度器／Claude 審查此分支與 CI，再由既定合併流程處理；不需使用者手動準備工具。worktree 與本機 PNG 保留，未拆 junction 或清理目錄。
