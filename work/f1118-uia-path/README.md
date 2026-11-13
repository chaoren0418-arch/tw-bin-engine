# UIA／GDI 操作工具（#1126／#1138）

記事本前置改為 **UIA 聚焦＋SendInput Unicode 輸入＋TextPattern 讀回**。Windows 10 記事本編輯區是 `ControlType.Document`（Class `Edit`、AutomationId `15`），只提供 `TextPattern`／`ScrollPattern`，不支援 `ValuePattern`。不要把狀態列的五個 `ControlType.Edit` 當編輯區。#1138 實機結果見下方報告；記事本 #1149 已通過；客戶端 #1150 部分通過，實證與限制見下方客戶端章節。

**#1138 單次實跑尚未通過**：輸入前 `Set-UiaForeground` 回報 `Foreground window does not match target`，未執行 SendInput／讀回，GDI 也被前景檢查擋下。根因是舊版只呼叫一次 `SetForegroundWindow`，而背景 shell 受 Windows 前景鎖限制（前景是使用者正在用的程式）。**#1148 已改為多步驟取得前景並附診斷、截圖改 PrintWindow 優先**（見下節）；記事本實機效果已由 [#1149](../../docs/reports/issue-1149.md) 驗證通過。

## 前景取得（#1148）

`Set-UiaForeground $Window` 不改任何系統設定，步驟如下，整體最多 3 輪：

1. `SetForegroundWindow($h)`，之後每 50 ms 查 `GetForegroundWindow()`，最多 1 秒；等於目標就結束。
2. 失敗時取目前前景視窗的執行緒（`GetWindowThreadProcessId`），`AttachThreadInput(本執行緒, 前景執行緒, true)` → `BringWindowToTop($h)`（輪詢）→ `SetForegroundWindow($h)`（輪詢）；`AttachThreadInput(..., false)` 一定在 `finally` 解除。沒有前景視窗（HWND 0）或前景就是本執行緒時略過附加，並記入步驟。
3. 成功回傳診斷物件；失敗拋錯，訊息含目標 HWND／PID、當時前景 HWND／PID／ProcessName、每步回傳值與 `GetLastError`、`SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT)` 的唯讀值。拋錯後可用 `Get-UiaLastForegroundDiagnostic` 取得完整步驟（`Test-Notepad.ps1` 會寫進 `ForegroundDiagnostic`）。

刻意不做：改前景鎖逾時、改登錄檔、鎖定前景切換、對其他程式送 ALT 或任何按鍵（會打進使用者正在用的程式）、移除 `Send-UiaText` 的 PID／鍵盤焦點保護。因此下列情境**仍可能失敗**，屬預期，應看診斷而不是硬送輸入：

- 使用者正在另一個程式打字或操作（該程式持續取得最後輸入，系統拒絕讓出前景）。
- 鎖定畫面、UAC 安全桌面、螢幕保護程式（前景 HWND 為 0 或屬於其他桌面）。
- 前景是較高完整性等級（系統管理員）的程式，`AttachThreadInput` 可能被拒（看 `LastError`）。
- 全螢幕獨佔的遊戲或簡報。

`Test-Notepad.ps1` 另在啟動記事本前記錄 `ForegroundBefore`（HWND／PID／ProcessName），方便判斷是否有焦點競爭。

## 執行環境與前置

使用 Windows PowerShell 5.1 的新行程。所有輸出指定至本 worktree 的 `scratch/`；不含帳密、不啟動 P24Lab、不改系統設定。若宿主預設禁止載入腳本，可用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File <腳本>`，此參數只作用於本次行程，沒有呼叫 `Set-ExecutionPolicy` 或改持久設定。

`UiaGdi.ps1` 載入時先呼叫 `SetProcessDpiAwarenessContext(-4)`，再設定目前執行緒 DPI context，接著載入 UIA／Drawing。不要在已有不同 DPI 設定的長駐 GUI 宿主中執行。`Get-UiaWindowInfo` 記錄視窗矩形、最小化、DPI 與行程 DPI 設定結果；成功時 LastError 沒有意義，記作 0。

```powershell
# 在 worktree 根目錄執行，只啟動一個新的記事本。
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-Notepad.ps1 -EvidenceDirectory scratch/issue-1138
```

`Test-Notepad.ps1` 依自啟 PID 找窗與標題，定位 Document/15，送出固定字串 `uia-1138 測試輸入 OK`，讀回後只將 CRLF／CR 正規化為 LF，再以 ordinal 逐字比對；不 trim、不忽略大小寫。結果、原文、前景 HWND／PID、支援 pattern 與截圖統計寫入 `notepad-result.json`。輸入空白或不符即停止輸入、保留證據，不改用第三種方法，交 `needs:local`。

finally 留一張 GDI 圖並關閉自啟記事本；若出現儲存對話框，只在同 PID 的 UIA 樹中唯一識別「不要儲存／不儲存／Don't Save」按鈕後 Invoke，保存 `notepad-close-tree.json`，不呼叫「儲存」、不清空文字規避對話框。確認 PID 結束才通過。失敗時可核對 PID、名稱與啟動時間後強制收尾，但仍回傳非零；成功回傳 0。PNG 必須另外檢視，確認圖中實際看得到指定文字，程式的 `Nonblank` 判定不代表目視驗收。

`Send-UiaText $Element $Text` 先 `SetFocus()`，確認前景 HWND 所屬 PID 與元素相同且元素有鍵盤焦點，再逐個 UTF-16 code unit 送出 `KEYEVENTF_UNICODE` 按下／放開；每字前重新核對前景 PID。不用剪貼簿或 SendKeys；文字會輸入目前插入點／取代目前選取範圍，**不會自動清空既有文字**。`Get-UiaText` 優先讀 `ValuePattern.Current.Value`，不支援時讀 `TextPattern.DocumentRange.GetText(-1)`；兩者均不支援即列出 ControlType／ClassName／pattern 清單並拋錯。`Set-UiaValue` 仍只使用 ValuePattern，不支援時列出相同診斷並拋錯，不自動切換輸入。

## 找窗、樹與截圖（記事本已驗證）

以下片段放在自己的 `scratch/` 腳本，以新 PowerShell 行程執行。視窗 PID 必須來自本張自己啟動的程序記錄；同 PID 有多個頂層視窗時，傳入本輪樹觀察到的精確標題。

```powershell
. ./work/f1118-uia-path/UiaGdi.ps1
$window = Get-UiaWindow -ProcessId $ownedProcessId -Title $observedTitle
Get-UiaWindowInfo $window
Get-UiaTree $window | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 scratch/window-tree.json
Save-UiaGdiScreenshot $window scratch/window.png
```

`Save-UiaGdiScreenshot` **先用 `PrintWindow($h, hdc, PW_RENDERFULLCONTENT=2)`**（#1148）把目標視窗畫進 24bpp bitmap（尺寸取 `GetWindowRect`，含 Win10 隱形邊框），不需要前景、被遮住也能截。`PrintWindow` 回傳 false 或整張全黑／全白時，才改走舊路徑：`Set-UiaForeground` 核對前景後，以 UIA 實體像素矩形 `CopyFromScreen` 並裁切至虛擬桌面。回傳物件含 `Method`（`PrintWindow`／`CopyFromScreen`）、`PrintWindowError`、`CopyFromScreenError`（各自錯誤原文，未走到為 null）、`Foreground`（走 CopyFromScreen 時的前景診斷），以及 PNG SHA-256、尺寸、每隔 4 像素取樣的平均 RGB 強度、非黑／非白比例。兩條路都失敗才拋錯（訊息含兩者原文）。最小化視窗直接失敗。`Nonblank` 只是排除全黑／全白，仍須檢視圖片，不能取代功能驗收；部分 GPU／DirectX 繪製的視窗 PrintWindow 可能畫出空白，屆時會自動退回 CopyFromScreen。

## 客戶端操作實證（#1150，2026-11-14）

完整結果見 [issue-1150.md](../../docs/reports/issue-1150.md) 與 [UIA／量測證據](../../docs/reports/issue-1150-evidence.json)。本輪使用 `--classic-ui --login-demo real-interactive --login-port 42061 --evidence <scratch 路徑>`，登入 p24svc01 至 CharacterList。**只有下表標示成功的方法已驗證；職業／能力值與真正送出仍未驗證。**

| 方法 | 實機結果 |
|---|---|
| 主視窗找窗、讀樹 | ✅ `Get-UiaWindow -ProcessId ... -Title 'P24 診斷客戶端'`、`Get-UiaTree` 成功。主畫布本身沒有可讀的場景文字控制項，樹包含 owned 開發工具視窗。 |
| 開發工具找窗 | ✅ 從主窗 `Find-UiaControl -Name '開發工具（過渡操作介面） · Ctrl+Shift+F9' -Type Window`；❌ 用 `Get-UiaWindow` 頂層列舉會 `found 0`，因它是 owned 子視窗。 |
| GDI 截圖 | ✅ 主窗 1206×871、開發工具 826×711、建角 466×661，皆 `PrintWindow`、DPI 192、未最小化，已目視。 |
| `SelectionItemPattern` 切分頁 | ❌ 對觀察到的「登入／選角」呼叫 Select 後，畫面成為「控制」；新樹找「服務」與「登入／選角」皆 `found 0`。這個方法不能宣稱可用。 |
| 鍵盤進入登入分頁 | ✅ 當畫面位於「控制」時，UIA 聚焦唯一 Tab 控制項、核對 HasKeyboardFocus，再送 Left，出現「登入／選角」及建立／刪除按鈕。只在相同起始狀態使用；它不算 G07 的 SelectionItemPattern 通過。 |
| UIA Invoke「建立角色」 | ⚠️ 確實開啟對話框，但 Invoke 等待後回報 `Operation timed out. (Exception from HRESULT: 0x80131505)`。不要因逾時再次 Invoke；先核對前景與截圖。 |
| 對話框樹、職業切換、能力值設定 | ❌ 對話框的 FindAll／Get-UiaTree 未在開啟期間返回，取消後才釋放；Get-UiaTree 最終報 `The property 'ProgrammaticName' cannot be found on this object. Verify that the property exists.`。未取得 ComboBox／NumericUpDown Edit，因此 ValuePattern、微調 Invoke、Send-UiaText 均未實證。 |
| 取消對話框 | ✅ 核對前景 HWND／PID 為本輪「建立角色」後，送 Escape；回到 CharacterList 3/3，未送出。未驗證取消鈕的 UIA Invoke。 |
| `Send-UiaF12` 單鍵 F12 | ❌ 前景核對與送鍵完成，但沒有新 manual PNG；函式不丟錯不代表擷取成功。 |
| `Ctrl+Shift+F12` | ✅ `Send-ClientKey <主窗 HWND> 'Ctrl+Shift+F12'`，產生三張新的 `manual-client.png`、`manual-screen.png`、`manual-logical.png`，修改時間晚於送鍵。 |

已實證的找窗與截圖：

```powershell
$main = Get-UiaWindow -ProcessId $ownedProcessId -Title 'P24 診斷客戶端'
$dev = Find-UiaControl $main -Name '開發工具（過渡操作介面） · Ctrl+Shift+F9' -Type Window
Get-UiaTree $dev
Save-UiaGdiScreenshot $main scratch/main.png
Save-UiaGdiScreenshot $dev scratch/dev.png

# 只有當最新截圖確認目前是「控制」分頁時，才使用此 Left 範例。
Set-UiaForeground $dev
$tab = Find-UiaControl $dev -Type Tab
$tab.SetFocus()
if (-not $tab.Current.HasKeyboardFocus) { throw 'Tab control has no keyboard focus' }
Send-ClientKey ([IntPtr]$dev.Current.NativeWindowHandle) 'Left'
Get-UiaTree $dev  # 必須確認出現 CharacterList 與建立角色按鈕。

# 已實證的新證據快捷鍵；單獨 F12 在 #1150 沒有輸出。
Set-UiaForeground $main
Send-ClientKey ([IntPtr]$main.Current.NativeWindowHandle) 'Ctrl+Shift+F12'
Get-ChildItem <本張evidence路徑>/manual-*.png | Select Name,LastWriteTimeUtc
```

本輪觀察到 owned 建角對話框可由前景 HWND 取得，不必掃描整個主窗；對話框開啟後掃主窗或對話框子樹可能阻塞。以下只說明 #1150 已完成的截圖／取消，不代表已解決 UIA 樹問題：

```powershell
$fg = Get-UiaForegroundInfo
if ($fg.ProcessId -ne $ownedProcessId) { throw 'Foreground is not the owned client' }
$dialog = [Windows.Automation.AutomationElement]::FromHandle([IntPtr]$fg.Handle)
if ($dialog.Current.Name -ne '建立角色') { throw 'Unexpected dialog' }
Save-UiaGdiScreenshot $dialog scratch/create-dialog.png
# 僅在本張操作要求取消、且再次核對前景 HWND/PID 未改變後使用。
Send-ClientKey ([IntPtr]$fg.Handle) 'Escape'
```

職業清單與能力值仍待可及性問題修復後實證。不要沿用舊 element index、猜 AutomationId、把不支援的 pattern 當成驗證成功，也不要改用座標硬點。F12 類的內建證據只保存主窗，不能替代獨立建角對話框 GDI 圖。

## 原生 UI 客戶端（#1154；給 #1145 N01–N04 用，**全部待真機驗證**）

原生 UI 面板畫在遊戲畫布上，不是 UIA 控制項，所以這一節的函式只吃 HWND，以客戶端 800×600 邏輯座標操作。`UiaGdi.ps1` 載入時已經呼叫 `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)`（只影響本程序；失敗只記在 `ProcessDpiSet`／`ProcessDpiError`，不中斷）。客戶端本身也是 PerMonitorV2、`AutoScaleMode.None`（`engine/source/Program.cs:15`、`:102`），所以兩邊看到的矩形都是實體像素。#1154 只做了靜態檢查（解析、編譯、簽章），以下行為都還沒有在真機跑過。

### 擷取方法

| 方法 | 函式 | 需要前景 | 說明 |
|---|---|---|---|
| PrintWindow | `Get-GdiClientCapture $Hwnd $Path`（第一條路） | 否 | 整個視窗 `PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT=2)` 畫進 `GetWindowRect` 大小的 24bpp bitmap，再依 `ClientToScreen(0,0) − 視窗左上` 裁成工作區（`GetClientRect` 大小）。呼叫的是 #1148 的同一個 `UiaGdiNative.PrintWindowLE`。 |
| BitBlt | 同上（第二條路） | 是 | PrintWindow 回傳 false，或裁出來的整張全黑／全白時才走這條：先 `Assert-ClientForeground`（內部用 #1148 的 `Set-UiaForeground` 多步驟），再從螢幕 DC `BitBlt(SRCCOPY|CAPTUREBLT)` 同一個工作區矩形。 |
| Ctrl+P 內建 | `Send-ClientKey $Hwnd 'Ctrl+P'` | 是 | 引擎把 800×600 邏輯畫面存到 `<stage>/runtime/Capture`（`MenuButtonsPanel.CaptureDirectory`，`MenuButtonsPanel.cs:105`；stage＝exe 目錄上兩層，也就是 `engine/`）。**只在世界內有效**：`MenuButtonsPanel.Key` 在世界外直接回 false（`MenuButtonsPanel.cs:276`），測試 `MenuHotkeyTests.cs:109` 也是這樣寫的。所以在登入畫面按 Ctrl+P，靜態推測**不會**產生檔案。 |

回傳物件包含：`Method`（`PrintWindow`／`BitBlt`）、`PrintWindowError`、`BitBltError`（兩條路各自的錯誤原文，沒走到的是 null）、`Width`／`Height`／`ClientWidth`／`ClientHeight`／`SizeMatchesClient`、`Dpi`（`GetDpiForWindow`）、`Scale`、每個像素都算的平均強度 `Mean`、`NonBlackRatio`／`NonWhiteRatio`、`Nonblank`、PNG 的 `Sha256`、`Foreground`（走 BitBlt 時的前景診斷）。兩條路都失敗才拋錯，訊息含兩者原文。統計與前後差異用內嵌 C# `LockBits` 計算（`ClientInputNative.Stats`／`Diff`），因為 `GetPixel` 太慢，跟不上每 0.5 秒輪詢。最小化的視窗直接失敗。

### 邏輯座標換算

`Get-ClientWindowGeometry` 依照引擎的 `UiPresenter.Compute` 換算（`engine/source/NativeUi/UiPresenter.cs:42–44`，fill=false）：

- `k = max(1, min(floor(工作區寬/800), floor(工作區高/600)))`（整數倍率），畫面置中，偏移 `((寬−800k)/2, (高−600k)/2)`。
- 邏輯點 (X, Y) → 工作區像素 `(偏移X + (X+0.5)·k, 偏移Y + (Y+0.5)·k)` 取整數 → 加上 `ClientToScreen(0,0)` 得到螢幕座標。
- `native-ui.json` 設了 `"fill": true` 時，傳 `-Fill`，改用 `UiPresenter.cs:37–40` 的浮點倍率。
- `RatioX`＝寬/800、`RatioY`＝高/600 照實記錄。兩者不一致，或不是 1／2 時，`RatioNote` 會寫明（例如視窗被拉大，但整數倍率仍然是 1）。點擊依引擎實際的整數倍率和置中偏移換算，不是單純各自乘比例，因為畫面就是這樣畫的。

`Send-ClientClick $Hwnd $X $Y [-Button Left|Right] [-Double] [-Fill]`：先 `Assert-ClientForeground`（前景不屬於客戶端 PID → `Set-UiaForeground` → 仍然不是就拋錯，不點）；再用 `WindowFromPoint`＋`GetAncestor(GA_ROOT)` 確認螢幕點落在客戶端本身，被其他視窗蓋住就拒絕；最後一次 `SendInput` 送出 `MOUSEEVENTF_MOVE|ABSOLUTE|VIRTUALDESK` 移動、按下、放開。正規化公式：`(螢幕X − SM_XVIRTUALSCREEN)·65535/(SM_CXVIRTUALSCREEN−1)`（Y 同理）。

`Send-ClientKey $Hwnd '<鍵或組合>'`：修飾鍵支援 `Ctrl`／`Alt`／`Shift`；按鍵支援 `F1–F24`、`A–Z`、`0–9`、`Enter`、`Esc`、`Tab`、`Space`、`Backspace`、方向鍵、`Home`／`End`、`PageUp`／`PageDown`、`Insert`／`Delete`。每個鍵送虛擬鍵碼＋`MapVirtualKey(VK, MAPVK_VK_TO_VSC)` 的 scan code，導覽鍵加 `KEYEVENTF_EXTENDEDKEY`。修飾鍵先按，**一定在 finally 放開**；放開一樣經過前景檢查，被拒絕就記錄並拋錯，不會送到別的程式。

`Send-ClientText $Hwnd '<字串>' [-Mode Unicode|VirtualKey]`：預設 `Unicode`，和 `Send-UiaText` 一樣逐個 UTF-16 單位送 `KEYEVENTF_UNICODE`。`VirtualKey` 只收 `[A-Za-z0-9 ]`，以虛擬鍵送出（大寫加 Shift）。**登入框要用 `VirtualKey`（推測）**：原生登入框有焦點時，`LoginPanel.Key` 會吃掉每個 keydown，字元是從按鍵本身換算出來的（`LoginPanel.cs:546–579`，經 `Program.cs:122` 的 `ProcessCmdKey`）。`KEYEVENTF_UNICODE` 產生的 VK_PACKET keydown 會被吃掉，換不出字元，也就不會再產生 WM_CHAR。所以 `Test-NativeClient.ps1` 預設 `-TextMode VirtualKey`。

所有送出都經過內嵌 C# 的 `ClientInputNative.SendGuarded`：在原生端緊接在 `SendInput` 之前再核對一次前景 PID，不符就不呼叫 `SendInput`（`LastError` 記 −1）。`Send-ClientText` 每個字元前都會核對。

### JSONL（每次點擊／每批按鍵一行）

預設寫到 `scratch/client-input.jsonl`；`Set-ClientInputLog <路徑>` 可以改，`Test-NativeClient.ps1` 寫在 `<evidence>/client-input.jsonl`。欄位：

- 共通：`Utc`（UTC ISO 8601）、`Action`（`click-left`、`key-down 0x11`、`key-press 0x50`、`key-up 0x11`、`text-virtualkey`…）、`Hwnd`、`ProcessId`、`ForegroundHwnd`、`ForegroundProcessId`、`ForegroundAcquired`（是否需要先取前景）、`InputCount`、`SendInputReturned`、`LastError`（−1 表示前景不符、沒送）、`InputSize`（x64 應為 40）。
- 點擊：`LogicalX`／`LogicalY`、`ClientX`／`ClientY`、`ScreenX`／`ScreenY`、`NormalizedX`／`NormalizedY`、`Scale`、`Mapping`、`RatioX`／`RatioY`／`RatioNote`、`ClientWidth`／`ClientHeight`、`Dpi`、`VirtualDesk`、`HitHwnd`／`HitRootHwnd`；被拒絕時多一個 `Refused`。
- 按鍵：`Keys`、`VirtualKey`、`Modifiers`；文字：`Index`、`CodeUnit`（VirtualKey 模式加 `VirtualKey`、`Shift`）。

### Test-NativeClient.ps1

```powershell
# 在 worktree 根目錄執行；證據目錄必須在專案資料夾內（ProductEntry.CheckEvidenceDirectory），且是新的空目錄。
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-NativeClient.ps1 -EvidenceDirectory scratch/<任務>/native
```

參數：`-ClientCmd`（預設 `engine\啟動新版客戶端.cmd`）、`-AccountPoint x,y`（預設 `375,374`）、`-AccountRect x,y,w,h`（預設 `320,364,110,20`）、`-TextMode VirtualKey|Unicode`、`-ActiveTimeoutSeconds`（預設 60）、`-Fill`。不含帳密，不登入、不送密碼、不啟動伺服器。

1. 以 `<cmd> --evidence <dir>` 啟動，不帶其他參數。launcher 會加上 `--normal-product --evidence <完整路徑>` 啟動 `build/client-product-dev/P11.ClientEngine.exe`，而且**不等它結束**（`engine/_integration/dev/launcher-source/Program.cs:53–58`）。所以腳本依 `Win32_Process` 的 ParentProcessId，從自己啟動的 cmd 往下收子孫，而且只收建立時間晚於啟動的程序。再從中找 `P11.ClientEngine.exe` 的可見主視窗。
2. **N01**：每 0.5 秒呼叫一次 `Get-GdiClientBitmap`。第一張存成 `n01-verifying.png`（核對中）；之後第一張與它像素差異超過 2% 的存成 `n01-active.png`（接手後），兩者的時間差記在 `VerifyingToActiveMs`。最多等 60 秒；`native-ui.log` 出現 `native-ui fallback` 就提早失敗。
   - **為什麼不用 `window-events.json` 等 active**：它只在 `FormClosed` 時寫檔（`engine/source/Program.cs:117`）。執行中，`verifying`／`active` 只加進記憶體的事件清單（`engine/source/NativeUi/UiClientBridge.cs:29`、`:46`）。`native-ui.log` 只記失敗（`UiClientBridge.cs:51` 的 `native-ui fallback`），active 時不寫任何行。所以執行中只能看畫面變化；關閉後再讀 `window-events.json`，確認有 verifying→active 的順序（`WindowEventsVerifyingThenActive`），兩者都成立 N01 才算通過。
   - 限制：如果第一張擷取時就已經是 active，「核對中」那張其實是 active；而登入背景每 200 ms 換一張圖（`LoginPanel.cs:51`），也會被當成「變化」。所以時間差只是上限估計，兩張圖仍然要人工看。
3. **N02**：先擷取 `n02-before.png`，`Send-ClientClick` 帳號欄，`Send-ClientText 'abc'`，再擷取 `n02-after.png`，比對帳號欄矩形內的差異像素。點擊和輸入分開記錄（`ClickSent`／`TextSent`／各自的錯誤）。門檻是矩形內至少 `30·k²` 個像素改變（游標約 1×14 像素）。這只是輔助判斷，要以人工看 after 圖裡有沒有 `abc` 為準。
   - **帳號欄矩形的出處**：登入版面讀的是參考客戶端（預設 kor25，`RefClientXmlSource.cs:23`）`Tile.pak` 裡的 `LoginUI.xml`。本機 `Tile.pak` 沒有這個檔（`docs/reference/pak-resources.md:110`）。#1154 用 PakViewer CLI `pak extract 'D:\381懶人包kor25(原)\Tile.idx' LoginUI.xml` 加上 `xml decrypt` 解出：原檔 SHA-256 `c79d636c…3c821a61`，明文 `dfefdd0d…b2c8d58f`，和 `pak-resources.md:88` 相同；解出的檔案只放 `scratch/`。內容是 `<Window Name="Login_Window" X="0" Y="0">` 和 `<InputBox Name="ID_Input" X="320" Y="364" Width="110" Height="20" …/>`。依 `LoginPanel.cs:290`（InputBox 的 Offset＝X/Y、Local＝Width/Height）與 `LoginPanel.cs:388`（`At`＝Origin＋Local），邏輯矩形是 (320, 364, 110, 20)，中心 (375, 374)。換用別的參考客戶端時要重查。
   - **點擊沒中也可能輸入成功**：登入窗一顯示，焦點就預設在 `ID_Input`（`LoginPanel.cs:330`；帳號已有字時 `:359`／`:382` 才改到密碼欄）。所以「看到 abc」只證明輸入成功，不證明點擊落點正確。點擊落點要看 JSONL 的 `ClientX/Y`、`HitRootHwnd`，必要時另外測一個非預設焦點的位置。
4. **N03**：送 `Ctrl+P`，3 秒內看 `engine/runtime/Capture` 和 `<exe>/../../runtime/Capture` 有沒有新檔，記下檔名、修改時間、SHA-256。依上方靜態結論，在登入畫面**預期沒有檔案**，結果照實記錄。
5. **N04**：送 `Alt+F4`，等客戶端程序結束（10 秒），再讀 `window-events.json` 確認有 `closed`。之後核對所有自己啟動的程序（PID＋啟動時間差 1 秒內），還活著的才強制結束（`ForcedClose` 不是空的就算失敗），最後唯讀查 42061／43324 有沒有屬於自己程序的監聽。

結果寫在 `<dir>/native-result.json`（`N01`–`N04` 各有 `Passed` 和細節、`Errors`、`OwnedProcesses`、`ForcedClose`），四項都通過且沒有錯誤才回 0，否則回 1。

### 已知限制

- **WGC 不可用、不使用**：#1126 D01 在這台機器上單次診斷的原文是 `Error: FrameArrived timed out: timed out waiting on channel`（[issue-1126.md](../../docs/reports/issue-1126.md) 第 31–35 行；GTX 1080、單螢幕 3840×2160、DPI 192）。本目錄的工具只用 PrintWindow／BitBlt／Ctrl+P。
- PrintWindow 對 GPU／DirectX 繪製的視窗可能畫出空白；客戶端是 GDI+ 畫的 WinForms（`UiPresenter.cs:52–56` 用 `Graphics.DrawImage`），classic-ui 已由 #1150 實證 PrintWindow 可用；本節原生登入路徑仍依其專屬任務結果判定。
- 前景鎖：`Send-*` 和 BitBlt 都需要客戶端在前景，限制和上方「前景取得」相同（使用者正在打字、鎖定畫面、UAC、較高完整性的前景程式）。拿不到前景就拋錯，不改用其他方法硬送。
- `Unicode` 模式在原生登入框可能不會產生字元（見上方 `Send-ClientText`）。

歷史結果：[issue-1126.md](../../docs/reports/issue-1126.md)（保留原文）。記事本結果：[issue-1149.md](../../docs/reports/issue-1149.md)；客戶端結果與下一棒：[issue-1150.md](../../docs/reports/issue-1150.md)。本目錄只放腳本與 README，PNG 留在各任務 worktree 的 scratch。
