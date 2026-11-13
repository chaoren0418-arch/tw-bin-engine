# Issue #1148：UIA 工具前景取得改多步驟＋診斷，GDI 截圖改 PrintWindow 優先

**結論：程式修改與靜態檢查完成；實機效果「待驗證」，交重測任務 `f1138-retest`。** 本張沒有執行任何 GUI、記事本或客戶端。

分支 `task/1148`（由 origin/main `dcd9df42` 建立）。

## 做了什麼

1. 從 `origin/task/1138` 原樣帶入：`work/f1118-uia-path/`、`docs/reports/issue-1126.md`、`docs/reports/issue-1126-evidence.json`、`docs/reports/issue-1138.md`（三份報告對來源分支 diff 為空）。草稿 PR #1142 的內容已全部併入本張，合併後可關閉。
2. `UiaGdi.ps1`
   - C# 新增：`AttachThreadInput(uint,uint,bool)`、`BringWindowToTop(IntPtr)`、`PrintWindow(IntPtr,IntPtr,uint)`、`GetWindowRect`、`GetCurrentThreadId`，`SystemParametersInfo` 宣告為 **private**，只透過 `GetForegroundLockTimeout()` 以 `0x2000`（SPI_GETFOREGROUNDLOCKTIMEOUT）、`fWinIni=0` 呼叫；沒有任何 setter。各呼叫有 `…LE` 包裝：呼叫前 `SetLastError(0)`、呼叫後取 `Marshal.GetLastWin32Error()`，記錄的錯誤碼只屬於該次呼叫。
   - `Set-UiaForeground`：最多 3 輪；每輪 `SetForegroundWindow` → 失敗則 `AttachThreadInput(本執行緒, 前景執行緒, true)` → `BringWindowToTop` → `SetForegroundWindow`，**附加呼叫本身與後續步驟都包在 try，`finally` 解除附加**。每步之後每 50 ms 輪詢、最多 1 秒。前景 HWND 為 0 或與本執行緒相同時略過附加並記錄。成功回傳診斷物件；失敗拋錯，訊息含目標 HWND／PID、前景 HWND／PID／ProcessName、每步回傳值／LastError、前景鎖逾時唯讀值；完整診斷另存，可用 `Get-UiaLastForegroundDiagnostic` 取回。
   - `Get-UiaForegroundInfo` 增加 `ThreadId`、`ProcessName`（唯讀 `Get-Process`）。
   - `Save-UiaGdiScreenshot`：先 `PrintWindow(PW_RENDERFULLCONTENT=2)` 畫進 24bpp bitmap（避免 32bpp ARGB 的 alpha=0 造成透明 PNG）；回傳 false 或全黑／全白才走 `Set-UiaForeground`＋`CopyFromScreen`。回傳加 `Method`、`PrintWindowError`、`CopyFromScreenError`、`Foreground`；平均強度、非黑／非白比例、SHA-256 照舊（抽成 `Measure-UiaBitmap`）。兩條路都失敗才拋錯並附兩者原文。
   - `Send-UiaF12` 吞掉 `Set-UiaForeground` 新的回傳值，避免汙染輸出。`Send-UiaText` 的 PID／鍵盤焦點保護未動。
3. `Test-Notepad.ps1`：啟動記事本前記錄 `ForegroundBefore`；`ForegroundDiagnostic` 存 `Set-UiaForeground` 的診斷（失敗時從 `Get-UiaLastForegroundDiagnostic` 取）。ExpectedText（`uia-1138 測試輸入 OK`）、Document/15、TextPattern 讀回、ordinal 比對、「不要儲存」Invoke、PID 身分核對後才強制收尾都不變。
4. README：新增「前景取得（#1148）」一節（步驟、刻意不做的事、仍可能失敗的情境），截圖段改寫為 PrintWindow 優先。
5. （第 2 次嘗試，回應合併前閘門）根目錄 `.gitignore` 的 `work/*/*` 只放行 `.md`／`.json`，兩個 `.ps1` 原本是被 ignore 卻硬加進 git。新增 `work/f1118-uia-path/.gitignore`（內容 `!.gitignore`、`!*.ps1`），只對這個目錄第一層放行 `.ps1`；不改根目錄 `.gitignore`，其他 `work/` 子目錄不受影響。核對：`git check-ignore --no-index` 對 `UiaGdi.ps1`、`Test-Notepad.ps1`、`.gitignore` 都沒有輸出（exit 1，都不再被 ignore）；對照組 `work/other/x.ps1` 仍被 ignore（exit 0）。

## 怎麼驗證（全部靜態）

| 檢查 | 結果 |
|---|---|
| `Parser::ParseFile` 兩個腳本 | 0 個錯誤／0 個錯誤 |
| 內嵌 C# 以 `Add-Type -OutputAssembly scratch/issue-1148/*.dll -OutputType Library` 只編譯（不載入、不執行） | 編譯成功 |
| 簽章人工核對 | `AttachThreadInput(DWORD,DWORD,BOOL)`→`uint,uint,bool`；`BringWindowToTop(HWND)`；`PrintWindow(HWND,HDC,UINT)`；`SystemParametersInfo(UINT,UINT,PVOID,UINT)`→`ref uint`（GET 前景鎖逾時寫入一個 UINT），僅 GET |
| 解除附加在 `finally` | 是（`UiaGdi.ps1` 的 `Set-UiaForeground`，附加呼叫在 try 內，`finally` 依 `$attached` 解除） |
| `git grep -i -E "SPI_SET\|keybd_event\|VK_MENU\|0x12\b\|LockSetForegroundWindow\|Set-ItemProperty\|New-ItemProperty\|HKCU:\|HKLM:\|reg add\|RegSetValue\|passwd" -- work/f1118-uia-path` | 無符合（exit 1） |
| `git diff --check` | 通過 |
| 帶入報告對 `origin/task/1138` diff | 空 |

SHA-256：`UiaGdi.ps1` `CBBFCC7367E511BC5681C4CA6001480B0B9386D08A1DC8C1B70B457E90028138`、`Test-Notepad.ps1` `0098171CE27CF408C86637F0259D6CDF623BBA3E58948B294DCA8CCED0767A2F`（commit 時的版本；若之後有修正以 git 為準）。

## 未驗證什麼（待驗證）

- 背景 shell 下多步驟能否實際取得前景（AttachThreadInput 路徑在前景鎖下的效果）。
- PrintWindow 對記事本的截圖內容、24bpp 色彩正確、圖中可見輸入文字。
- Unicode SendInput 輸入、TextPattern 讀回、「不要儲存」Invoke、成功 exit code 0。
- 客戶端（WinForms）的 PrintWindow 結果；若客戶端用 GPU 繪製可能退回 CopyFromScreen。
- 沒有改引擎、沒有改 `rebuild-lock.json`，不需重建引擎；CI 只看 PR 狀態。

## 產出路徑

- `work/f1118-uia-path/UiaGdi.ps1`、`Test-Notepad.ps1`、`README.md`、`.gitignore`（放行本目錄 `.ps1`）
- `docs/reports/issue-1126.md`、`issue-1126-evidence.json`、`issue-1138.md`（原樣帶入）
- 本報告 `docs/reports/issue-1148.md`
- 編譯檢查用 DLL 在 `scratch/issue-1148/`（不追蹤）

## 給下一棒的提示（f1138-retest）

- 從 main 執行：`powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-Notepad.ps1 -EvidenceDirectory scratch/issue-<號>`。
- 先看 `notepad-result.json` 的 `ForegroundBefore` 與 `ForegroundDiagnostic.Steps`：哪一步成功、`LastError`、`ForegroundLockTimeout`。若前景仍不到，代表有人在前景程式持續輸入或處於鎖定／安全桌面，屬環境條件；**不要**改系統設定或對其他程式送按鍵來硬取前景，應安排無焦點競爭的桌面再測。
- 看 `Screenshot.Method`：預期 `PrintWindow`；PNG 仍須目視確認文字可見。
- 不要移除 `Send-UiaText` 的 PID／鍵盤焦點保護。
- 本張合併後，草稿 PR #1142 可由調度器關閉。
