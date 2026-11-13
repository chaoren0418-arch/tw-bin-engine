# Issue #1126：UIA／GDI 替代路徑

**結論：部分通過（✅ 3／❌ 1／⚠️ 9）；標記 `needs:local`。** 記事本 GDI 截圖成功，WGC 單次診斷仍逾時；新的前置阻塞是記事本編輯區不支援指定的 `ValuePattern.SetValue`。依本張第 1 步「任一步失敗 → 附錯誤原文，停止並標 needs:local」，未啟動客戶端、Java 或 MySQL，驗收 (a)／(b) 均未成立。不是 GDI 擷取故障，也不能據此判定客戶端 UIA 不可用。

2026-11-14 05:12–05:15（台北）執行真機探測；分支 `task/1126`，來源 HEAD `13c66c895c48aef5504b40c3eff14d5c882b5197`。小型原始量測、錯誤、樹與 SHA-256 保存於 [issue-1126-evidence.json](issue-1126-evidence.json)。下表所有 `scratch/issue-1126/` 都在本 worktree `D:\lineage開發\TW1608262200研究\scratch\wt\task-1126` 內。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| S02 記事本 UIA 輸入前置 | ❌ | UIA 找窗、標題及編輯區成功；Document/15 只有 TextPattern、ScrollPattern，ValuePattern 回報 `Unsupported Pattern.`，未寫入文字，無讀回成功證據 | 新開記事本 → UIA 按 PID 找窗 → 找 AutomationId `15`／Document → `GetCurrentPattern(ValuePattern.Pattern)` | `scratch/issue-1126/notepad-tree.json`、`value-pattern-error.txt`；Git 證據 `probe`／`notepadTree` |
| S02-GDI 截圖子項 | ✅ | PNG 1812×1600，可見標題、選單、空白編輯區與狀態列；非全黑／全白 | 同 DPI-aware 行程依 UIA 矩形 `CopyFromScreen` → 統計 → 人工檢視 PNG | `scratch/issue-1126/notepad.png`；Git 證據 `probe.Screenshot` |
| D01 唯讀診斷 | ✅ | WGC 只試一次仍 `FrameArrived timed out`；已記錄顯卡、驅動、螢幕／DPI、session | `sky.list_windows/get_window/activate_window/get_window_state`；CIM、Screen、GetDpiForMonitor、query session | Git 證據 `probe.Diagnostics` |
| G01 登入到 CharacterList | ⚠️ | 依 S02 停止條件未啟動／登入 | 前置通過後才依 #1108 參數登入 p24svc01 | 本報告 S02；`scratch/issue-1126/cleanup.json` |
| G02 主視窗截圖 | ⚠️ | 未執行；主視窗矩形、DPI、最小化未量測 | 用 `Save-UiaGdiScreenshot` 擷取主視窗 | 本報告 S02 |
| G03 主視窗 UIA 樹 | ⚠️ | 未執行 | `Get-UiaTree $main` | 本報告 S02 |
| G04 主視窗操作 | ⚠️ | 未送 F12，無內建證據；本張以 F12 取代原座標點擊 | `Send-UiaF12 $main`，核對新產生 manual PNG | 本報告 S02 |
| G05 開發工具截圖 | ⚠️ | 未執行；視窗矩形、DPI、最小化未量測 | 用 `Save-UiaGdiScreenshot` 擷取開發工具 | 本報告 S02 |
| G06 開發工具 UIA 樹 | ⚠️ | 未執行 | `Get-UiaTree $dev` | 本報告 S02 |
| G07 開發工具操作 | ⚠️ | 未切分頁；本張以 SelectionItemPattern 取代原座標點擊 | 切「服務」再切回「登入／選角」並讀回樹 | 本報告 S02 |
| F01 建角替代路徑 | ⚠️ | 未開建角、未切兩職業、未取消對話框；沒有送出／建角 | 開發工具 Invoke「建立角色」→ ComboBox 展開／選職業並截圖 → 取消 | 本報告 S02 |
| P01 重用腳本 | ⚠️ | 已交付找窗／樹／GDI 方法及其他 pattern 包裝、README；客戶端操作尚未實證，整理後腳本只做語法檢查 | 參考 README；不得把待驗證範例當成功路徑 | `work/f1118-uia-path/UiaGdi.ps1`、`Test-Notepad.ps1`、`README.md` |
| C01 本次收尾 | ✅ | 自啟記事本 PID 4468 已經 WindowPattern.Close 正常結束；未自啟客戶端／Java／MySQL；42061／43324 無監聽 | 核對 PID、名稱、StartTime → 關窗並 WaitForExit → 查指定兩埠 | `scratch/issue-1126/cleanup.json`；Git 證據 `cleanup` |

## 錯誤與量測

記事本標題「未命名 - 記事本」，HWND 16910534，未最小化，DPI 192；UIA 矩形 `(x=1782,y=112,width=1812,height=1600)`。編輯區 `ControlType.Document`、Class `Edit`、AutomationId `15`。初始以 `ControlType.Edit` 查找得到五個節點，實際都是狀態列（`StatusBar.Pane0`–`Pane4`）；改依樹識別真正編輯區後，指定 pattern 的原始錯誤為：

```text
System.Management.Automation.MethodInvocationException: Exception calling "GetCurrentPattern" with "1" argument(s): "Unsupported Pattern." ---> System.InvalidOperationException: Unsupported Pattern.
```

WGC 單次診斷原文：

```text
Error: FrameArrived timed out: timed out waiting on channel
```

GDI 每隔 4 像素取樣，共 181,200 點；平均 RGB 強度 **247.50715967623361／255**，非黑比例 **0.99078366445916111**，非白比例 **0.10749448123620309**。PNG SHA-256：`A7C63C14498910B8803CB3321E92871450CFFA6CEEF698EE08F171783463AB48`。文字輸入沒有成功，截圖中的空編輯區符合實際狀態；畫面非空白不代表輸入測試通過。

顯示卡 NVIDIA GeForce GTX 1080，驅動 `32.0.15.6094`；單螢幕 `\\.\DISPLAY1`，3840×2160，DPI 192×192（200%）。執行器 PID 18568、記事本 PID 4468、Explorer PID 14948 都在 session 6，`query session` 為 console／Active。未重測 #1118 S01 解鎖項，只記錄當次 session。`SetProcessDpiAwarenessContext(-4)` 回傳 true；原始證據同時留下的 LastError 203 是成功後無效的殘值，不可解讀成 DPI 設定失敗，整理版 helper 已只在失敗時回報 LastError。

05:12 首次 lease 查詢回傳持有人 #1124，故只記錄差異、繼續獨立記事本作業；05:14 診斷時已為 #1126，`exp=1794609715.4286966`（2026-11-13T22:41:55.4286966Z），與派工到期時間相符且當時有效。**停止原因是 S02 的明定前置失敗，不是 lease 差異。** 未修改 lease 或調度器。

## 做了什麼／怎麼驗證／未驗證什麼

讀取根目錄 AGENTS.md、docs/REPORT.md、#1121 分流、task/1118 報告，以及 #1108 的指定啟動腳本／UIA 樹（唯讀）。執行 UIA／GDI 記事本探測與一次 WGC 診斷，保存原始資料，整理可重用腳本與 README。PowerShell 啟動時預設 execution policy 拒絕 `-File`，改以行程參數 `-ExecutionPolicy Bypass` 執行，沒有變更持久 execution policy／登錄檔／系統設定。

驗證：實際 PNG 檢視、像素統計與 SHA-256、關窗與埠狀態、PowerShell 語法解析、證據 JSON 解析與表格計數、`git diff --check`。整理腳本不再次重跑已知失敗的前置；沒有變更引擎、dev lock 或 docs/REPORT.md，因此沒有重建引擎。沒有使用 DB、複製遊戲素材、改原專案／封存／正式客戶端、刪目錄或拆 junction。

待驗證：客戶端兩視窗全部項目、完整建角對話框流程、F12、能力值設定；腳本中的 Invoke／SelectionItem／ExpandCollapse／SendInput 包裝尚未在客戶端執行。WGC 的確切底層原因仍未知，未修驅動／工具環境。

## 給下一棒的提示

1. **由本機對話／調度器（needs:local）調整記事本前置契約**：本機記事本確實不提供 ValuePattern；不要原樣重試，也不需因這點修引擎。可考慮允許 UIA 聚焦編輯區、SendInput 輸入、TextPattern 讀回；這是建議，尚未實作或驗證。這項門檻調整後才繼續 G01–G07／F01。
2. GDI 已實證可用；後續不用再次嘗試 WGC。不要把這次停止當成「兩種截圖都失敗」。本輪沒有證明 GDI 對客戶端與獨立建角對話框的結果。
3. [README](../../work/f1118-uia-path/README.md) 已列找窗、分頁、按鈕、職業、能力值與 F12 用法，每次依新 UIA 樹定位。開發工具與建角對話框都有「建立角色」，必須限定根視窗；本張禁止送出。
4. PNG 原始大檔只在本 worktree 的 scratch，Git 留量測摘要與雜湊；清理前由調度器保存需要的證據，本執行器未移除 worktree。

產出：本報告、`docs/reports/issue-1126-evidence.json`、`work/f1118-uia-path/`（僅腳本與 README）。
