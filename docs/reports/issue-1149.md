# Issue #1149：記事本前置重測

結論：通過（本輪 ✅ 4／❌ 0／⚠️ 0）。

## 做了什麼／已驗證

2026-11-14 06:55:12–06:55:15（台北時間）在 `task/1149` 執行一次指定記事本測試。測試來源為 `edf77755deb0339f8e90fd267982fa659744d44f`，與當時 fetch 後的 `origin/main` 相同，已包含 #1148／PR #1151 的前景與截圖修正。未修改測試程式。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑（相對本 worktree） |
|---|---|---|---|---|
| S02 | ✅ 通過 | 送出與讀回皆為 `uia-1138 測試輸入 OK`；只正規化換行後 ordinal 相等。目標 HWND=723254／PID=16408；輸入後前景仍相同。 | 下列命令執行一次，檢查 `Readback`、`TextPassed` 與前景診斷。 | `scratch/issue-1149/notepad-result.json`、`notepad-tree.json`、`postflight.json` |
| S02-GDI | ✅ 通過 | `Method=PrintWindow`，1812×1600、`Nonblank=true`；已目視原 PNG，左上可清楚讀到完整輸入字串，標題為 `*未命名 - 記事本`，不是全黑或全白。 | 同一次執行的 finally 截圖；開啟 PNG 目視核對。 | `scratch/issue-1149/notepad.png`、`notepad-result.json` |
| S02-CLOSE | ✅ 通過 | 儲存提示顯示「要儲存對 未命名 的變更嗎?」，Invoke `不要儲存(N)`／`CommandButton_7`；`DontSaveInvoked=true`、`Closed=true`、`ForcedClose=false`，事後 PID=16408 不存在；未產生記事本存檔。 | 同一次執行的 WM_CLOSE → 儲存提示 → 不要儲存；事後唯讀核對 PID 與檔案清單。 | `scratch/issue-1149/notepad-close-tree.json`、`notepad-result.json`、`postflight.json` |
| P01 | ✅ 通過 | JSON `ExitCode=0`，呼叫端立即擷取的子程序 `$LASTEXITCODE=0`；`Errors=[]`、stderr 0 bytes。 | 同一次執行結束後記錄實際退出碼，非只採信 JSON。 | `scratch/issue-1149/execution.json`、`stdout.log`、`stderr.log` |

S02-CLOSE 的無存檔判據：未命名文件的修改提示、同一 PID 的「不要儲存」Invoke 與正常退出形成完整操作證據；worktree 根目錄檔名／長度前後無差異，證據目錄只有測試證據及包裝腳本，無記事本文檔。未進行全磁碟檔案寫入追蹤。

## 怎麼驗證／前置紀錄

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-Notepad.ps1 -EvidenceDirectory scratch/issue-1149
```

由 `scratch/issue-1149/run-once.ps1` 包裝上述單次呼叫，只補上前置唯讀資訊、stdout／stderr 與實際退出碼留存，未另外輸入或切換桌面視窗。

- 開始前 `query session` 的目前工作階段原文：`>console           CHAOREN                   6  Active`；完整輸出另含 `services`／ID 0／Disc。命令退出碼為 1，仍有上述工作階段輸出，按任務要求記錄此程序疑慮，不中止測試。
- 前置時間 `2026-11-14T06:55:12.0731455+08:00`：HWND=132132、PID=4716、ThreadId=17092、ProcessName=`claude`。測試 JSON 的 `ForegroundBefore` 同值。
- 唯讀 `http://localhost:8787/api/state`：lease=`p24lab`，holder=`chaoren0418-arch/tw-bin-engine#1149`，exp=`1794615839.9492235`（`2026-11-14T00:23:59.949Z`），與派工到期時間一致至秒，測試期間有效。
- 首次在外層 shell dot-source 輔助檔遭既有 ExecutionPolicy 拒絕，當時尚未啟動記事本；改由上述程序限定的 `-ExecutionPolicy Bypass` 包裝執行。未修改系統執行原則；記事本測試仍僅執行一次。
- 前景首輪 `SetForegroundWindow=false`、LastError=0、等待逾 1 秒；`AttachThreadInput(attach)=true` → `BringWindowToTop=true`、前景等待 0 ms → `AttachThreadInput(detach)=true`；`Succeeded=true`、Rounds=1，最終前景為目標記事本。
- 編輯器為 Document／ClassName=`Edit`／AutomationId=`15`，支援 ScrollPattern、TextPattern；以 TextPattern 讀回。不觸發停止條件。
- 只啟動本輪記事本 PID=16408，已正常結束。未啟動客戶端、Java、MySQL，未連 42061／43324 或正式服務，未寫 DB、修改系統設定或操作原專案資源。

## 沿用、推測、待驗證

| 項目 | 處理 |
|---|---|
| #1138 P01 靜態部分 | **沿用**：語法解析、INPUT 40 bytes、無帳密、失敗回傳 1；未重測。 |
| #1126 D01／C01 | **沿用**：本輪未執行 WGC 或 C01。 |

推測：無新增推測。待驗證：本張四項無剩餘驗收；記事本結果不代表遊戲實機流程已驗證。

## 產出路徑／給下一棒的提示

報告：`docs/reports/issue-1149.md`。原始證據：本 worktree 的 `scratch/issue-1149/`，未將 PNG、log 或暫存編譯檔加入 git。下文保留 JSON 原文與雜湊供跨環境審查。

由調度器／Claude 審查並接續原先被記事本前置阻擋的任務；本張無需再開相同四項重測，也不需標 `needs:local`。清理 worktree 前須先保留所需 PNG／原始證據；本輪未移除 worktree、junction 或遞迴刪除任何目錄。`docs/REPORT.md` 與 rebuild lock 未修改。

## 原始結果與 SHA-256

以下 `notepad-result.json` 為本輪原文（含完整前景診斷）。

```json
{
    "StartedAt":  "2026-11-14T06:55:12.5251262+08:00",
    "ExpectedText":  "uia-1138 測試輸入 OK",
    "Readback":  "uia-1138 測試輸入 OK",
    "TextPassed":  true,
    "Closed":  true,
    "DontSaveInvoked":  true,
    "ForcedClose":  false,
    "ExitCode":  0,
    "ForegroundBefore":  {
                             "Handle":  132132,
                             "ProcessId":  4716,
                             "ThreadId":  17092,
                             "ProcessName":  "claude"
                         },
    "ProcessId":  16408,
    "StartTime":  "2026-11-14T06:55:12.6655706+08:00",
    "Window":  {
                   "Title":  "未命名 - 記事本",
                   "ProcessId":  16408,
                   "Handle":  723254,
                   "Rectangle":  "1782,112,1812,1600",
                   "Minimized":  false,
                   "Dpi":  192,
                   "ProcessDpiSet":  true,
                   "ProcessDpiError":  0
               },
    "EditorDescription":  "ControlType=ControlType.Document; ClassName=Edit; AutomationId=15; SupportedPatterns=[ScrollPatternIdentifiers.Pattern, TextPatternIdentifiers.Pattern]",
    "EditorPatterns":  [
                           "ScrollPatternIdentifiers.Pattern",
                           "TextPatternIdentifiers.Pattern"
                       ],
    "ForegroundDiagnostic":  {
                                 "TargetHandle":  723254,
                                 "TargetProcessId":  16408,
                                 "TargetThreadId":  9160,
                                 "CallerThreadId":  14264,
                                 "ForegroundBefore":  {
                                                          "Handle":  132132,
                                                          "ProcessId":  4716,
                                                          "ThreadId":  17092,
                                                          "ProcessName":  "claude"
                                                      },
                                 "ForegroundLockTimeout":  {
                                                               "Ok":  true,
                                                               "Milliseconds":  200000,
                                                               "LastError":  0
                                                           },
                                 "Steps":  [
                                               {
                                                   "Round":  1,
                                                   "Step":  "SetForegroundWindow",
                                                   "Returned":  false,
                                                   "LastError":  0,
                                                   "ForegroundAfterMs":  -1
                                               },
                                               {
                                                   "Round":  1,
                                                   "Step":  "AttachThreadInput(attach)",
                                                   "Returned":  true,
                                                   "LastError":  0,
                                                   "FromThread":  14264,
                                                   "ToThread":  17092,
                                                   "Foreground":  {
                                                                      "Handle":  132132,
                                                                      "ProcessId":  4716,
                                                                      "ThreadId":  17092,
                                                                      "ProcessName":  "claude"
                                                                  }
                                               },
                                               {
                                                   "Round":  1,
                                                   "Step":  "BringWindowToTop",
                                                   "Returned":  true,
                                                   "LastError":  0,
                                                   "ForegroundAfterMs":  0
                                               },
                                               {
                                                   "Round":  1,
                                                   "Step":  "AttachThreadInput(detach)",
                                                   "Returned":  true,
                                                   "LastError":  0,
                                                   "FromThread":  14264,
                                                   "ToThread":  17092
                                               }
                                           ],
                                 "Succeeded":  true,
                                 "Rounds":  1,
                                 "ForegroundAtEnd":  {
                                                         "Handle":  723254,
                                                         "ProcessId":  16408,
                                                         "ThreadId":  9160,
                                                         "ProcessName":  "notepad"
                                                     },
                                 "Error":  null
                             },
    "ForegroundAfterInput":  {
                                 "Handle":  723254,
                                 "ProcessId":  16408,
                                 "ThreadId":  9160,
                                 "ProcessName":  "notepad"
                             },
    "ReadPattern":  "TextPattern",
    "NormalizedExpected":  "uia-1138 測試輸入 OK",
    "NormalizedReadback":  "uia-1138 測試輸入 OK",
    "Screenshot":  {
                       "Path":  "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-1149\\scratch\\issue-1149\\notepad.png",
                       "Method":  "PrintWindow",
                       "PrintWindowError":  null,
                       "CopyFromScreenError":  null,
                       "Width":  1812,
                       "Height":  1600,
                       "SampleStride":  4,
                       "Samples":  181200,
                       "Mean":  247.20491905813057,
                       "NonzeroRatio":  0.98296357615894037,
                       "NonwhiteRatio":  0.090805739514348782,
                       "Nonblank":  true,
                       "Sha256":  "8E381001CB08B717F5E4AA539203270A7B92FFBF76670886095FE57AF33B3871",
                       "Foreground":  null,
                       "Window":  {
                                      "Title":  "*未命名 - 記事本",
                                      "ProcessId":  16408,
                                      "Handle":  723254,
                                      "Rectangle":  "1782,112,1812,1600",
                                      "Minimized":  false,
                                      "Dpi":  192,
                                      "ProcessDpiSet":  true,
                                      "ProcessDpiError":  0
                                  }
                   },
    "DontSaveButton":  {
                           "Name":  "不要儲存(N)",
                           "AutomationId":  "CommandButton_7",
                           "ProcessId":  16408
                       },
    "CompletedAt":  "2026-11-14T06:55:15.4709508+08:00",
    "Errors":  [

               ]
}
```

| 檔案 | SHA-256 |
|---|---|
| `work/f1118-uia-path/Test-Notepad.ps1` | `0098171CE27CF408C86637F0259D6CDF623BBA3E58948B294DCA8CCED0767A2F` |
| `work/f1118-uia-path/UiaGdi.ps1` | `CBBFCC7367E511BC5681C4CA6001480B0B9386D08A1DC8C1B70B457E90028138` |
| `scratch/issue-1149/notepad.png` | `8E381001CB08B717F5E4AA539203270A7B92FFBF76670886095FE57AF33B3871` |
| `scratch/issue-1149/notepad-result.json` | `F0695B105FA1A08D3DF17AB9647341A4958629E76BE40E473BFB7AFDF64C6666` |
| `scratch/issue-1149/notepad-tree.json` | `CC7A7C4F9A19B17210C48D7E46F19E4C9C7593818AA156509FE3D6EF212C81D9` |
| `scratch/issue-1149/notepad-close-tree.json` | `46B8F4AE777E3DB9B8DC0DF0BE719E410BE64A6C9F02D100D9E3EACEAAF6D5E1` |
| `scratch/issue-1149/preflight.json` | `5610149A9FBB29205D1F087CF49DCE738B49606A5BA8757E952B9BCFCD2E298A` |
| `scratch/issue-1149/postflight.json` | `D7C2AAD2CF3A35C3C783977B1AEB51FD2065C937B0DB2524C3C48335224F9C09` |
| `scratch/issue-1149/execution.json` | `B88F08CA087801818FF0453CC8D800AC04DDEA975468E3FCAA2AD7C7986FEB83` |
| `scratch/issue-1149/stdout.log` | `7BA9C0486995276B875CA3B72DF5FC7FBBEF56ACB193FB190A3C19303F915DF1` |
| `scratch/issue-1149/stderr.log` | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| `scratch/issue-1149/run-once.ps1` | `9C02034ED1C801914645E82455A01AD72562D9E6CD2E4BA35EAC0E51433747F0` |
