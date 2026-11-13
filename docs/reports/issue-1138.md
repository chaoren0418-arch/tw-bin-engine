# Issue #1138：記事本 UIA 輸入前置工具與單次實測

**結論：部分通過（✅ 1／❌ 2／⚠️ 1）；needs:local。** 已帶入 #1126 工具並實作指定輸入／讀回方法；本次在輸入前的前景檢查失敗，沒有執行 SendInput，也沒有讀回或 PNG。自啟記事本已正常退出，腳本回傳 1。依「失敗項目不要自己修程式」規則保留失敗，不重試、不換第三種方法。

2026-11-14 05:58:51–05:58:52（台北），worktree `D:\lineage開發\TW1608262200研究\scratch\wt\task-1138`，分支 `task/1138`。起始 main：`47ac3ef06d5cb82f19838840d6c69d80adcd410b`；工具與歷史報告來源 `origin/task/1126`：`a241ead7bf94480c6a835333aad4ec57695fbb56`。下列 scratch 路徑均相對此 worktree。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| S02 輸入／讀回 | ❌ | 成功依 PID 找到標題與 Document/15；`Set-UiaForeground` 回報前景不符，Send-UiaText／Get-UiaText 尚未執行，Readback 是 null（不是讀到空字串） | 執行下列單次指令，找窗後進入輸入前景檢查 | `scratch/issue-1138/notepad-result.json`、`notepad-tree.json`；下方原始量測 |
| S02-GDI 附帶回歸 | ❌ | finally 嘗試截圖，同樣被前景檢查拒絕，未產生 PNG，平均強度／比例／SHA／目視均無結果；#1126 舊圖通過仍保留，但不算本次通過 | 同次執行的 finally 呼叫 Save-UiaGdiScreenshot | `scratch/issue-1138/notepad-result.json` 的第二筆 Errors |
| S02-CLOSE 本次收尾 | ✅ | 自啟 PID 13732 正常結束，Closed=true、ForcedClose=false；未輸入、未存檔，沒有儲存對話框；有修改時的「不要儲存」Invoke 尚未驗證 | 同次 finally 送 WM_CLOSE、WaitForExit，再查原 PID 已不存在 | `scratch/issue-1138/notepad-result.json`、`static-checks.json` |
| P01 可重用腳本 | ⚠️ | 新方法、診斷與 README 已交付；兩腳本語法解析與 C# helper 編譯通過、INPUT 結構 40 bytes、無帳密、失敗回傳 1 已實證；成功回傳 0、輸入讀回與儲存對話框路徑尚未實證 | 語法 ParseFile、以 scratch 暫存載入 helper、檢查單次執行 exit code 與 git diff | `work/f1118-uia-path/`、`scratch/issue-1138/exit-code.txt`、`static-checks.json` |

重現指令（本張只執行一次，不應未處理前景原因就原樣重跑）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-Notepad.ps1 -EvidenceDirectory scratch/issue-1138
```

## 原始觀察與證據

- 記事本：PID `13732`、啟動時間 `2026-11-14T05:58:51.5241023+08:00`，標題「未命名 - 記事本」，HWND `7079834`，矩形 `1782,112,1812,1600`，DPI `192`，未最小化，ProcessDpiSet=true。
- 失敗時前景：HWND `132132`、PID `4716`；事後唯讀查詢其 ProcessName=`claude`、SessionId=`6`。沒有操作該程序。只能證明前景沒有切到目標；無法據此判定是作業系統焦點限制還是同桌面活動造成，根因待查。
- 編輯區：`ControlType.Document`、ClassName=`Edit`、AutomationId=`15`，支援 `ScrollPatternIdentifiers.Pattern`、`TextPatternIdentifiers.Pattern`，沒有 ValuePattern。
- 預定輸入：`uia-1138 測試輸入 OK`；`Readback=null`、`TextPassed=false`、`DontSaveInvoked=false`。本次失敗發生在呼叫 Send-UiaText 之前，不能據此判定 SendInput 或 TextPattern 不可用。
- 兩筆 Errors 原文相同：`System.Management.Automation.RuntimeException: Foreground window does not match target`，分別來自輸入前的 Set-UiaForeground 與 finally 截圖的 Set-UiaForeground。
- 證據目錄頂層只有 JSON／exit code；未提供記事本存檔路徑、未呼叫儲存。沒有 PNG 或儲存對話框樹可檢視。

SHA-256：

| 檔案 | SHA-256 |
|---|---|
| `work/f1118-uia-path/UiaGdi.ps1` | `7A251AE661A706BD126B8D32B361FA9BF13D4559201BCCBB98D8A72DE08B29D9` |
| `work/f1118-uia-path/Test-Notepad.ps1` | `8B40F84C33434A2F7DD8088290622F4DDD812240FDD40857680E4718A46B73CE` |
| `scratch/issue-1138/notepad-result.json` | `D8AB0525BD3F08C6EAAFBB16E5E7BE83BCC9B9351049D27C4E1EBE011B993B76` |
| `scratch/issue-1138/notepad-tree.json` | `CC7A7C4F9A19B17210C48D7E46F19E4C9C7593818AA156509FE3D6EF212C81D9` |

## 做了什麼／怎麼驗證／未驗證什麼

帶入 `work/f1118-uia-path/` 與兩份 #1126 歷史報告，後者對來源分支 diff 為空。新增 Send-UiaText（SetFocus、鍵盤焦點與前景 PID 檢查、逐 UTF-16 code unit 的 Unicode SendInput）、Get-UiaText（ValuePattern 優先，否則 TextPattern）、Set-UiaValue 的不支援 pattern 明確診斷。Test-Notepad 改為固定 ASCII＋中文字串、只正規化換行的 ordinal 比對、GDI 統計、同 PID「不要儲存」Invoke 與確實退出檢查；失敗仍收尾與回傳非零。README 說明插入點行為及尚未通過的實測，沒有把程式碼存在當成功驗收。

已驗證：單次實機執行、失敗 exit code 1、PID 退出、兩腳本 PowerShell 語法、內嵌 C# 編譯、無帳密檢查（唯一 password 字樣是既有 UIA 密碼欄位遮罩）、歷史報告內容一致、git diff --check。沒有改引擎，不需重建引擎。失敗後只更新文件、做唯讀／靜態核對，未修測試程式或重跑 GUI。

未驗證：成功 exit code 0、實際 Unicode 輸入及 TextPattern 讀回、文字截圖目視、有修改內容時「不要儲存」Invoke；客戶端 WinForms TextBox／NumericUpDown 的 ValuePattern、F12、分頁與建角仍待後續。WGC 與 #1126 其他已通過診斷沿用，不重測。未啟動客戶端、Java、MySQL，未連遊戲／DB 埠、未改 DB／系統設定／持久 execution policy／服務；未寫原專案、封存或正式客戶端，未刪目錄／拆 junction。

05:56 的 lease 唯讀查詢：holder=`chaoren0418-arch/tw-bin-engine#1138`，exp=`1794612360.1278307`，即 `2026-11-13T23:26:00.1278307Z`，與派工到期秒數相符且當時有效；未修改 lease。

## 產出與給下一棒的提示

產出為本報告、`work/f1118-uia-path/`，以及原文帶入的 `docs/reports/issue-1126.md`／`issue-1126-evidence.json`。原始證據在 `scratch/issue-1138/`，本報告保存關鍵原文與雜湊；調度器清理 worktree 前請保留需要的原始 JSON。

交給本機對話／調度器 `needs:local`：先釐清此次前景仍停在 PID 4716 的原因，安排無焦點競爭的桌面或修正前景取得流程，再只重測 S02、S02-GDI 與 P01 未驗證路徑。不要移除 PID／焦點保護來硬送文字，也不要把此次未送出輸入記成讀回不符。通過前不要放行後續客戶端重測；#1126 D01 與歷史通過項目不重做。本張正常收尾已完成。
