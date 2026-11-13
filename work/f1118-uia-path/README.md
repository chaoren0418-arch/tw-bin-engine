# UIA／GDI 操作工具（#1126／#1138）

記事本前置改為 **UIA 聚焦＋SendInput Unicode 輸入＋TextPattern 讀回**。Windows 10 記事本編輯區是 `ControlType.Document`（Class `Edit`、AutomationId `15`），只提供 `TextPattern`／`ScrollPattern`，不支援 `ValuePattern`。不要把狀態列的五個 `ControlType.Edit` 當編輯區。#1138 實機結果見下方報告；完整客戶端路徑仍待重測。

**#1138 單次實跑尚未通過**：輸入前 `Set-UiaForeground` 回報 `Foreground window does not match target`，未執行 SendInput／讀回，GDI 也被前景檢查擋下。已正常關閉自啟記事本並回傳 1，未修正或重試失敗項目；新的輸入、讀回與「不要儲存」Invoke 路徑仍待實證。交由 `needs:local` 處理前景問題後重測。

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

`Save-UiaGdiScreenshot` 以前景 HWND 核對防止截錯視窗，使用 UIA 實體像素矩形與 `CopyFromScreen`，並裁切至虛擬桌面；最小化或完全在螢幕外會失敗。回傳 PNG SHA-256、尺寸、每隔 4 像素取樣的平均 RGB 強度、非黑／非白比例。`Nonblank` 只是排除全黑／全白，仍須檢視圖片，不能取代功能驗收。GDI 不保證被其他視窗遮住時仍截得到內容。

## 客戶端操作用法（全部待真機驗證）

先確認已有授權且登入至 `CharacterList`。以下是 pattern 包裝方法與使用範例，**不是本輪通過的操作清單**。每次操作後重新找控制項並讀樹；名稱／AutomationId 必須來自當次 UIA 樹，不沿用歷史 element index。重名／找不到控制項會直接拋錯。

WinForms `TextBox`／`NumericUpDown` 的 Edit 子項**預期**提供 ValuePattern，仍待客戶端實機重測確認；若沒有，文字欄位可用 `Send-UiaText`（先依當次觀察準備插入點／選取範圍），能力值可用微調鈕 Invoke。不得把記事本成功推論為客戶端已通過。

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

歷史結果：[issue-1126.md](../../docs/reports/issue-1126.md)（保留原文）。本次結果與下一棒：[issue-1138.md](../../docs/reports/issue-1138.md)。本目錄只放腳本與 README，本次 PNG 留在 `scratch/issue-1138/`。
