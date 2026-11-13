# #1154 f1145-native-input：原生 UI 客戶端 GDI 工作區截圖＋客戶端座標 SendInput 工具（靜態實作）

**結論：工具已寫好，靜態檢查全過；實機效果（N01–N04）全部待驗證。** 本張沒有執行任何 GUI、客戶端、記事本或伺服器。

**本分支含 #1151（#1148）的內容**：開工時 PR #1151 還沒合併，依任務前置在本分支 `git merge origin/task/1148`，沒有改動 #1148 已有函式的行為。**請先合 #1151，再合本張。**

## 做了什麼

只改 `work/f1118-uia-path/` 與本報告（`.gitignore` 沒改，見「怎麼驗證」第 4 點）。

1. `UiaGdi.ps1` 在 #1148 的檔案尾端新增以下內容（前面原有的部分沒動）：
   - 內嵌 C# `ClientInputNative`：`GetClientRect`、`ClientToScreen`、`GetWindowRect`、`SendInput`（x64 `INPUT` 40 bytes）、`WindowFromPoint`／`GetAncestor`、`GetSystemMetrics`、`MapVirtualKey`、`GetDC`／`BitBlt`／`ReleaseDC`。另有 `SendGuarded`：在原生端緊接在 `SendInput` 前核對前景 PID，不符就不送。還有用 `LockBits` 算的 `Stats`／`Diff`。
   - `Get-GdiClientCapture $Hwnd $Path`：第一條路是 PrintWindow(PW_RENDERFULLCONTENT=2) 擷取整個視窗再裁成工作區，呼叫 #1148 的 `UiaGdiNative.PrintWindowLE`；結果全黑／全白或失敗時，才 `Assert-ClientForeground`（內部是 #1148 的 `Set-UiaForeground`）後從螢幕 DC `BitBlt` 同一矩形。回傳寬高、DPI、平均強度、非黑／非白比例、SHA-256、`Method`，以及兩條路各自的錯誤原文。不用 WGC。
   - `Send-ClientClick $Hwnd $X $Y [-Button Left|Right] [-Double] [-Fill]`：800×600 邏輯座標依引擎的 `UiPresenter.Compute` 換算（整數倍率＋置中偏移），寬高比例照實記錄。`ClientToScreen` 後以 `MOUSEEVENTF_MOVE|ABSOLUTE|VIRTUALDESK` 正規化到 0–65535 送出。送出前前景必須屬於客戶端 PID，否則跑一次 `Set-UiaForeground`，仍然不是就拋錯不點；螢幕點被其他視窗蓋住也拒絕。
   - `Send-ClientKey $Hwnd 'Ctrl+P'` 等：送虛擬鍵碼＋scan code，修飾鍵在 finally 放開。
   - `Send-ClientText $Hwnd '<字串>' [-Mode Unicode|VirtualKey]`：`Unicode` 沿用 `KEYEVENTF_UNICODE`；`VirtualKey` 給原生登入框用（見推測 P1）。
   - JSONL：每次點擊／每批按鍵寫一行，記 UTC、HWND、PID、邏輯／工作區／螢幕／正規化座標、縮放、前景 HWND／PID、`SendInput` 回傳值、`GetLastError`。
   - DPI：`SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` 沿用 #1148 檔頭的呼叫（`UiaGdi.ps1:69–70`），失敗只記錄、不中斷。
2. 新增 `Test-NativeClient.ps1 -EvidenceDirectory <dir> [-ClientCmd] [-AccountPoint x,y] [-AccountRect x,y,w,h] [-TextMode] [-ActiveTimeoutSeconds] [-Fill]`：
   - 流程：`engine\啟動新版客戶端.cmd --evidence <dir>` 啟動 → 收自己啟動的程序（依 ParentProcessId，只收建立時間晚於啟動的）→ 找主視窗 → N01 每 0.5 秒擷取 → N02 點帳號欄＋輸入 `abc` → N03 `Ctrl+P` → N04 `Alt+F4`。
   - 結果寫 `native-result.json`；四項都過才回 0。
   - 只強制結束自己啟動、且 PID＋啟動時間吻合的程序。不含帳密，不登入，不啟動伺服器。
3. `README.md` 加「原生 UI 客戶端」一節：擷取方法（PrintWindow／BitBlt／Ctrl+P 內建）、邏輯座標換算、JSONL 欄位、Test-NativeClient 流程、已知限制（引 #1126 D01 的 WGC 錯誤原文）。

## `window-events.json` 落檔時機（結論）

**只在關閉時落檔**，執行中讀不到。

- `engine/source/Program.cs:117`：只有 `FormClosed` 會呼叫 `Json.Save(...,"window-events.json",events)`。
- `engine/source/NativeUi/UiClientBridge.cs:29`、`:46`：`verifying`／`active` 只 `events.Add`，存在記憶體裡。
- `native-ui.log`（`ClientArchive.cs:131` `UiResourceLog`）只在失敗時寫 `native-ui fallback`（`UiClientBridge.cs:51`），active 時沒有任何一行。`ProductEntry.cs` 只做參數與證據目錄核對，不寫事件。

因此「等 active」改用畫面判斷：與第一張擷取的像素差異超過 2% 就視為接手。`native-ui.log` 出現 `native-ui fallback` 時提早失敗。關閉後再讀 `window-events.json`，核對 verifying→active 的順序，作為 N01 的最終條件。README 已寫明依據。

## 帳號欄矩形的出處

- 登入版面來自參考客戶端（預設 kor25，`RefClientXmlSource.cs:23`）`Tile.pak` 的 `LoginUI.xml`。本機客戶端沒有這個檔（`docs/reference/pak-resources.md:110`，T227 已查過所有 pak）。
- 用 PakViewer CLI `pak extract 'D:\381懶人包kor25(原)\Tile.idx' LoginUI.xml` 加上 `xml decrypt` 解出（只讀，輸出在 `scratch/issue-1154-loginui/`，不進 git）。原檔 SHA-256 `C79D636C56750FC5381CDCD18FC9104A9FB1D3EEC9A65C8A0A4FB2783C821A61`，明文 `DFEFDD0DAC096FF9C83840FDF9B865E73EE7E8C577D5F97CD6D27066B2C8D58F`，和 `pak-resources.md:88` 相同。
- `<Window Name="Login_Window" X="0" Y="0">`，`<InputBox Name="ID_Input" X="320" Y="364" Width="110" Height="20" …/>`。依 `LoginPanel.cs:290`（Offset＝X/Y，Local＝Width/Height）與 `:388`（Origin＋Local），邏輯矩形是 **(320, 364, 110, 20)**，`-AccountPoint` 預設取中心 **(375, 374)**，同一矩形用來判斷 N02 的差異。
- 登入窗一顯示，焦點就預設在 `ID_Input`（`LoginPanel.cs:330`；`:359`／`:382` 是帳號已有字時改到密碼欄）。所以點擊沒中也可能輸入成功；`native-result.json` 把 `ClickSent`／`TextSent` 分開記錄，點擊落點要看 JSONL 的座標與 `HitRootHwnd`。

## 怎麼驗證（全部靜態）

1. `[System.Management.Automation.Language.Parser]::ParseFile`：`UiaGdi.ps1`、`Test-NativeClient.ps1`、`Test-Notepad.ps1` 都是 **0 錯誤**。
2. 從 AST 取出兩段內嵌 C#，用 .NET Framework `csc.exe /target:library /platform:x64` 編成 `scratch/issue-1154-static/{UiaGdiNative,ClientInputNative}.dll`，**exit 0**，沒有載入執行。另開一個 PowerShell 行程，以 `ReflectionOnlyLoadFrom`（只讀中繼資料、不執行程式碼）列出簽章，存在 `scratch/issue-1154-static/signatures.txt`：
   - `SendInput(UInt32, INPUT[], Int32) -> UInt32`，SetLastError=True。
   - `INPUT` 是 Sequential `{UInt32 type, INPUTUNION u}`；`INPUTUNION` 是 Explicit `{MOUSEINPUT, KEYBDINPUT}`；`MOUSEINPUT` 是 `{Int32 dx, Int32 dy, UInt32 mouseData, UInt32 dwFlags, UInt32 time, UIntPtr}`；`KEYBDINPUT` 是 `{UInt16 wVk, UInt16 wScan, UInt32 dwFlags, UInt32 time, UIntPtr}`。依 x64 對齊規則算：MOUSEINPUT 32、KEYBDINPUT 24、INPUT＝4＋4（padding）＋32＝**40**。執行時另外把 `Marshal.SizeOf` 記在 JSONL 的 `InputSize`。
   - `PrintWindow(IntPtr, IntPtr, UInt32)`、`BitBlt(IntPtr, Int32×4, IntPtr, Int32, Int32, UInt32)`（gdi32）、`ClientToScreen(IntPtr, POINT&)`、`GetClientRect(IntPtr, RECT&)`、`SetProcessDpiAwarenessContext(IntPtr) -> Boolean`（SetLastError=True）都與 Win32 定義一致。
3. `git grep -i -E 'Windows\.Graphics\.Capture|GraphicsCaptureItem|SPI_SET|LockSetForegroundWindow|Set-ItemProperty|New-ItemProperty|HKCU:|HKLM:|reg add|Registry::' -- work/f1118-uia-path` → **沒有任何結果**。帳密：`git grep -i 'passw|pwd'` 只命中 `Test-NativeClient.ps1:4` 的註解「no password」和 #1148 `UiaGdi.ps1:93` 的 `[PASSWORD]` 遮罩，沒有帳號密碼字串。
4. `git check-ignore -v work/f1118-uia-path/Test-NativeClient.ps1` → exit 1（沒有被排除）。`--no-index` 顯示命中的是 #1148 的 `work/f1118-uia-path/.gitignore:4:!*.ps1`。沿用 #1148 最後的解法（目錄層 `.gitignore`），根目錄 `.gitignore` 沒改。
5. 兩支新增／修改的 `.ps1` 都是純 ASCII（Windows PowerShell 5.1 讀沒有 BOM 的檔案時不會誤判編碼）。啟動檔名的中文以 code point 組字。
6. CI：push 後看 PR 的 build 結果（本張沒有改引擎，CI 不跑這些腳本）。

## 已驗證／推測／待驗證

**已驗證（靜態）**
- 上面第 1–5 點。
- `window-events.json` 只在 `FormClosed` 落檔；執行中沒有 active 的檔案證據（Program.cs:117、UiClientBridge.cs:29／46／51）。
- launcher 啟動 `build/client-product-dev/P11.ClientEngine.exe` 後不等它結束（`engine/_integration/dev/launcher-source/Program.cs:53–58`），所以腳本要往子孫程序找客戶端。
- 帳號欄邏輯矩形 (320, 364, 110, 20)（出處見上）。

**推測（都需要實機確認）**
- **P1 `KEYEVENTF_UNICODE` 可能打不進原生登入框**：登入框有焦點時，`LoginPanel.Key` 會吃掉所有 keydown，字元從按鍵本身換算（`LoginPanel.cs:546–579`，經 `Program.cs:122` 的 `ProcessCmdKey`）。VK_PACKET 換不出字元又被吃掉，就不會產生 WM_CHAR。因此 `Test-NativeClient.ps1` 預設 `-TextMode VirtualKey`。如果實機要驗 Unicode 路徑，加 `-TextMode Unicode` 另跑一次。
- **P2 N03 在登入畫面預期不會產生檔案**：`MenuButtonsPanel.Key` 在世界外回 false（`MenuButtonsPanel.cs:276`），`MenuHotkeyTests.cs:109` 也是這樣測的。所以照 #1145 原樣，N03 在登入畫面會失敗，`Test-NativeClient.ps1` 也就回 1。這是引擎行為，不是工具問題。結果照實記錄（`N03.ExpectedOnLoginScreen`）。
- P3 客戶端是 GDI+ 畫的 WinForms，PrintWindow 應該拿得到畫面；DirectX 那類空白問題推測不會出現。
- P4 單靠畫面判斷 active 的時間差只是上限：第一張可能已經是 active，登入背景動畫（`LoginPanel.cs:51`，每 200 ms 換圖）也會被當成變化。

**待驗證（交 `f1145-retest`）**
- N01–N04 全部的實機結果：截圖非空白且尺寸＝工作區、核對中／接手後的耗時、`abc` 是否可見、Ctrl+P 新檔、Alt+F4 關閉、程序全收、兩埠沒有自己啟動的監聽。
- `Set-UiaForeground` 在客戶端上能不能取得前景（受 Windows 前景鎖限制，見 README「前景取得」）。
- DPI 192（2×）下，工作區實際大小與點擊落點。客戶端預設 800×600 實體像素，k=1；使用者把視窗拉大到 1600×1200 以上時 k=2。

## 產出路徑

- `work/f1118-uia-path/UiaGdi.ps1`（新增「Native UI client」段）
- `work/f1118-uia-path/Test-NativeClient.ps1`（新檔）
- `work/f1118-uia-path/README.md`（新增「原生 UI 客戶端」一節，並更正 #1138 那段「沒有座標點擊」的說明）
- `docs/reports/issue-1154.md`（本檔）
- 不進 git：`scratch/issue-1154-loginui/`（LoginUI.xml 原檔與明文）、`scratch/issue-1154-static/`（抽出的 .cs、編好的 DLL、`signatures.txt`）
- 分支 `task/1154`（含 `origin/task/1148` 的合併）

## 給下一棒的提示

1. 合併順序：先合 #1151（#1148），再合本張。如果 #1151 有改動，本分支 `git merge origin/main` 時，`UiaGdi.ps1` 的衝突取 main 的上半部，保留檔尾「Native UI client」段。
2. `f1145-retest` 跑法：在 worktree 根目錄先建置（`engine/build-product.ps1`），再執行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-NativeClient.ps1 -EvidenceDirectory scratch/<任務>/native`。證據目錄必須在專案內，而且是新的目錄。登入畫面不需要伺服器，**不要**啟動 42061／43324。
3. 判讀時要看：`native-result.json` 的 `N01..N04`、`client-input.jsonl`（`ForegroundProcessId`、`SendInputReturned`、`LastError`、`HitRootHwnd`），以及 `n01-*.png`、`n02-*.png`（人工確認原生登入畫面與 `abc`）。
4. 依 P2，主控可以考慮把 N03 改成「進世界後再驗」，或另開 code 任務決定登入畫面要不要支援 Ctrl+P；本張沒有改引擎。如果 N02 用 VirtualKey 能輸入、Unicode 不能，就證實 P1，Unicode 輸入要不要支援屬於引擎決策，另開任務。
