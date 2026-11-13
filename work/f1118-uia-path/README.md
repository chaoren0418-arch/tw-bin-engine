# UIA／GDI 操作工具（#1126）

**本輪只有記事本找窗、UIA 樹讀取、GDI 截圖與關窗通過。完整客戶端路徑尚未驗證。** Windows 10 記事本編輯區是 `ControlType.Document`（AutomationId `15`），只提供 `TextPattern`／`ScrollPattern`；`ValuePattern` 回報 `Unsupported Pattern.`，已依任務停止條件停止。不要把狀態列的五個 `ControlType.Edit` 當編輯區，也不要原樣重跑這個已知不支援的輸入方法。

## 執行環境與前置

使用 Windows PowerShell 5.1 的新行程。所有輸出指定至本 worktree 的 `scratch/`；不含帳密、不啟動 P24Lab、不改系統設定。若宿主預設禁止載入腳本，可用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File <腳本>`，此參數只作用於本次行程，沒有呼叫 `Set-ExecutionPolicy` 或改持久設定。

`UiaGdi.ps1` 載入時先呼叫 `SetProcessDpiAwarenessContext(-4)`，再設定目前執行緒 DPI context，接著載入 UIA／Drawing。不要在已有不同 DPI 設定的長駐 GUI 宿主中執行。`Get-UiaWindowInfo` 記錄視窗矩形、最小化、DPI 與行程 DPI 設定結果；成功時 LastError 沒有意義，記作 0。

```powershell
# 在 worktree 根目錄執行；此前置在本機預期因 Unsupported Pattern 失敗。
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-Notepad.ps1 -EvidenceDirectory scratch/issue-1126-recheck
```

`Test-Notepad.ps1` 是將本輪探測整理成含 finally 收尾的重現腳本；整理後只做語法檢查，沒有再次重測已知失敗項目。它只關閉自己啟動的記事本，輸入失敗仍留一張唯讀 GDI 診斷，失敗會回傳非零。重新跑它之前，應先由本機對話調整測試契約，允許編輯區 UIA 聚焦＋SendInput 輸入、TextPattern 讀回；**這條替代輸入方法本輪未實作或驗證**。

## 找窗、樹與截圖（記事本已驗證）

以下片段放在自己的 `scratch/` 腳本，以新 PowerShell 行程執行。視窗 PID 必須來自本張自己啟動的程序記錄；同 PID 有多個頂層視窗時，傳入本輪樹觀察到的精確標題。

```powershell
. ./work/f1118-uia-path/UiaGdi.ps1
$window = Get-UiaWindow -ProcessId $ownedProcessId -Title $observedTitle
Get-UiaWindowInfo $window
Get-UiaTree $window | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 scratch/window-tree.json
Save-UiaGdiScreenshot $window scratch/window.png
```

`Save-UiaGdiScreenshot` 以前景 HWND 核對防止截錯視窗，使用 UIA 實體像素矩形與 `CopyFromScreen`，並裁切至虛擬桌面；最小化或完全在螢幕外會失敗。回傳 PNG SHA-256、尺寸、每隔 4 像素取樣的平均 RGB 強度、非黑／非白比例。`Nonblank` 只是排除全黑／全白，仍須檢視圖片，不能取代功能驗收。GDI 不保證被其他視窗遮住時仍截得到內容。

## 客戶端操作用法（全部待真機驗證）

先確認已有授權且登入至 `CharacterList`。以下是 pattern 包裝方法與使用範例，**不是本輪通過的操作清單**。每次操作後重新找控制項並讀樹；名稱／AutomationId 必須來自當次 UIA 樹，不沿用歷史 element index。重名／找不到控制項會直接拋錯。

```powershell
# 切分頁；dev 是用本次精確視窗標題取回的開發工具視窗。
Select-UiaItem (Find-UiaControl $dev -Name '服務' -Type TabItem)
Get-UiaTree $dev
Select-UiaItem (Find-UiaControl $dev -Name '登入／選角' -Type TabItem)
Get-UiaTree $dev

# 僅從開發工具視窗開啟對話框；不要在對話框內按同名送出按鈕。
Invoke-UiaButton (Find-UiaControl $dev -Name '建立角色' -Type Button)
$dialog = Get-UiaWindow -ProcessId $ownedProcessId -Title '建立角色'
Get-UiaTree $dialog
Save-UiaGdiScreenshot $dialog scratch/create-dialog.png

# 從最新樹取得職業 ComboBox 的唯一 Id；不要將性別 ComboBox 當職業。
$combo = Find-UiaControl $dialog -AutomationId $observedClassComboId -Type ComboBox
Expand-UiaCombo $combo
Get-UiaTree $dialog
# 從實際展開清單取得王族／騎士 ListItem。若清單屬於另一頂層視窗，
# 先依同 PID 的新 UIA 樹找該視窗，不能假設 ListItem 一定位於 dialog。
Select-UiaItem (Find-UiaControl $observedListRoot -Name $observedClassName -Type ListItem)
Get-UiaTree $dialog
Save-UiaGdiScreenshot $dialog scratch/class-selected.png

# 能力值：以最新樹中對應 NumericUpDown 的 Edit 子項呼叫 ValuePattern。
# 若沒有 ValuePattern，改對該微調控制項的向上／向下 Button 呼叫 InvokePattern。
Set-UiaValue $observedStatEdit '12'
Get-UiaValue $observedStatEdit
# 或：Invoke-UiaButton $observedStatIncrementButton

# 結束必須取消，不輸入 Enter、不呼叫對話框的「建立角色」。
Invoke-UiaButton (Find-UiaControl $dialog -Name '取消' -Type Button)
Get-UiaTree $dev

# 主視窗 F12：驗證前景後用 SendInput 送按下／放開。
Send-UiaF12 $main
```

F12 必須在啟動客戶端時提供 `--evidence <本張 scratch 路徑>`；檢查此次新產生的 `manual-client.png`／`manual-screen.png`／`manual-logical.png` 及修改時間。它只保留主視窗證據，無法取代獨立建角對話框截圖。腳本沒有座標點擊實作；本任務允許以 UIA patterns 與 F12 代替。能力值設定也未在本輪執行。

完整結果與待接手項目：[issue-1126.md](../../docs/reports/issue-1126.md)。本目錄只放腳本與 README，PNG 留在 `scratch/issue-1126/`。
