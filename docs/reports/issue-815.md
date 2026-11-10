# Issue #815 T352：R7-M 登入畫面收尾（Notice_Window 接 S48、Exit 鈕）

依據：`docs/reference/gap-matrix.md` 第 15.6 節 R7-M（T339／#800）；#764 報告；PAK§4。

## 做了什麼

- **`engine/source/NativeUi/LoginPanel.cs`**
  - **Notice_Window**（參考客戶端 `LoginUI.xml` 第 2 個 Window）：
    - 建法跟 Message_Window 一樣：大小取 BaseSurf（kor25 是 7021），而且一定要有 `Message_Label`。BaseSurf 缺圖或沒有 Label 時，`NoticeWindow=null`，列進 `Skipped`、記一行 log，S48 照舊用訊息框。
    - `ScrollBar` 底下的 `Up_Button`／`Down_Button` 會建出來，位置是 ScrollBar 的 X/Y 加上按鈕的 X/Y。`Move_Button`（拇指）沒做，因為它怎麼移動沒查，列進 `Skipped`。
    - 新增 `ShowNotice(text, confirm)`：
      - 開啟條件：面板可用、Notice_Window 可用、登入狀態在登入畫面會顯示的範圍（`ShowsIn`）。不符合就回 false，呼叫端改用訊息框。
      - 面板如果已經被 `Close()` 收起，會重新顯示。
      - 新公告會取代舊公告，舊公告的 confirm 不會執行。
    - 公告開著時是 Modal：`Bounds` 是整個畫布，登入窗不能點、不能打字、不能登入。F 鍵與 Ctrl／Alt 組合鍵照常交給客戶端。
    - **關閉並確認**：按 `Ok_NoticeWin`、`Skip_NoticeWin`、Enter、Esc，或呼叫 `Close()`，都會關掉公告並執行一次 confirm。按住 Enter 的自動重複不算。
      - Skip 跟 Ok 做一樣的事（**推測**）。
      - 關掉後焦點回到輸入框：已經打了帳號就到密碼框，否則到帳號框。
    - **捲動**：Up／Down 鈕、滾輪、↑↓ 每次捲一行；PageUp／PageDown 每次捲一頁（可見行數）。範圍限制在 `0..NoticeMaxScroll`，`NoticeMaxScroll` 在繪圖時依換行後的行數算出。
    - **文字**：在 `Message_Label` 的寬度內逐字換行，靠上對齊（XML `VAlign="top"`），白色。
    - **繪圖順序**：登入窗（不互動）→ 公告窗（BaseSurf，再畫子元素，子元素照 XML 反向順序）→ 訊息窗。
    - **公告失效**：登入狀態離開 `AwaitList`（取消、被拒、斷線、Faulted、角色清單），或畫面收起時，公告丟掉，confirm 不執行。這時連線已經結束，`LoginConnection` 也不會再等確認。
  - **Exit 鈕**：
    - 新介面 `IUiLoginCommands.ExitClient()`。`Exit_LoginWin` 透過 `LegacyWindowPanel.FindCommands` 找這個指令，會穿過 `UiMacroCommands` 等裝飾層；找到就呼叫，並清掉密碼與焦點。
    - 找不到時（離線測試、舊橋接）照舊只記一次「尚未接上」，所以既有 `login-ui` 測試的預期不變。
  - ChangePass／OTP／Credit 鈕與 Credit_Window 仍然沒接（不在本任務範圍）。
- **`engine/source/ClientLogin.cs`**
  - `QueueLoginNews` 先試原生 Notice_Window（`ShowNativeLoginNotice`）；不能用才走原本的模態訊息框，訊息框程式改成 `ShowLoginNewsBox`，內容不變。
  - 兩邊按確定都走 `ConfirmLoginNews`：先確認還是同一個 session，才 `login.ConfirmNews(revision)`，接著 LoginConnection 送 C16。
- **`engine/source/NativeUi/UiClientBridge.cs`**
  - `ShowNativeLoginNotice`：用 `LoginPanel.ShowNotice` 開公告（面板要已註冊、沒有 Faulted），然後記下交出去的 (revision, session)，並寫 `native-ui-login-news` 事件。
  - **防卡住**：`CheckNativeLoginNotice` 在每次 `UiRoot.Update` 之後檢查。如果連線還在等這則公告，但面板已經 Faulted、被視窗堆疊擠掉，或公告被丟掉，就改用既有訊息框顯示（事件記 `fallback="message-box"`），避免登入一直卡在等確認。
  - `NativeUiCommands` 實作 `IUiLoginCommands.ExitClient`：先 `ClearInput`、寫事件，再用 `BeginInvoke` 呼叫 `form.Close()`。這是一般關視窗的路徑：FormClosing 會關音效，FormClosed 會 `login.Dispose`。
- **新檔 `engine/source/LoginFollowupTests.cs`**：`[WorldModuleTest("login-followup")]`，自動發現。全部用合成資料：同結構的小版面、純色圖、不連網的假 `ILoginSession`。涵蓋：
  - 版面：位置、BaseSurf 大小、Label、ScrollBar 按鈕偏移、拇指沒建、反向繪圖順序。
  - 不能用的情況：BaseSurf 缺、沒有 Label、沒有 Notice_Window、角色清單或世界中、面板沒載入。
  - 公告流程：Modal、Ok／Skip／Enter／Esc／Close 都確認一次；在按鈕外放開、自動重複的 Enter 都不確認；已收起的畫面會重新出現；焦點回復。
  - 捲動：按鈕、滾輪、↑↓、PageUp／PageDown、範圍限制。
  - 失效：狀態變成 Cancelled／Rejected／Faulted／Disconnected／CharacterList 時丟掉公告；新公告取代舊公告。
  - 像素：BaseSurf、Frame、Back、Ok、Skip、Up、Down，以及底下的登入窗；長公告依繪圖算出可捲範圍。
  - Exit：有指令時呼叫；穿過裝飾層也找得到；沒有指令時只記 log；被公告擋住時不觸發；畫面沒顯示時不觸發；Credit 仍然沒接。
  - `LoginNewsText.Display` 的換行在 `Wrap` 後保留。

## 既有入口（`--classic-ui`）

- `--classic-ui`（或沒有開原生 UI）時，`NativeUiActive=false`，`ShowNativeLoginNotice` 回 false，S48 照舊用模態訊息框，按確定才回 C16（R2-Q）。Exit 鈕只在原生畫面，WinForms 不受影響。
- 原生 UI 開著，但以下情況也同樣回到訊息框：沒有參考客戶端、面板停用、Faulted、版面缺 Notice_Window 或缺 7021。
- 自動化情境（`NewsConfirmationRequired=false`）根本不等確認，所以不會開公告。

## 資源

- 沒有宣稱缺任何資源。Notice_Window 用到的 surf 是 7018–7031，都在 #764 逐檔核對的 44 個 surf 之內：kor25 44/44 命中，本機 `0-客戶端` 24 個 idx 全缺。查過的 pak 清單見 `docs/reports/issue-764.md`「資源核對」。執行期照舊從參考客戶端讀，**沒有複製任何檔案進 repo**。
- 版面結構是讀 kor25 `LoginUI.xml` 的解密副本（`scratch/`，由 #629 用 PakViewer 解出，沒有進 git）對照的。

## 怎麼驗證

- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t815/build`，**0 錯誤**，3 個警告都是既有的（`MagicUi.cs`、`HtmlActionTests.cs`）。
  - 本機還原的問題沿用 #764 的處理：把主專案 `engine/source/obj`、`engine/audio/obj` 的 assets／g.props／g.targets 讀出來，改成本 worktree 的路徑後放進 worktree 的 `obj/`。主專案只讀。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`login-followup`、`login-ui` 與其他測試交給 PR 的 GitHub Actions CI，結果見 PR。
- **CI（PR #838）第一輪全綠**：`dotnet`、`map-continuity`（含 `--module-tests`）、`npc-offline` 都通過。`login-followup` 85 項全部通過，既有 `login-ui` 124 項也全部通過，整份 module-tests 的 `failed` 為 0。
- `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj` 都沒改。

## 未驗證什麼（待驗證）

- **原版行為**：PBW§5.3 說 TW16 原版的 S48 是用通用模態訊息框 `sub_8E1E30` 顯示，不是 LoginUI 的 Notice_Window。Notice_Window 出自參考客戶端（kor25）的版面，本任務是照 gap-matrix 15.6 的裁定把它接上。外觀與原版 TW16 不一定相同（**推測**）。
- 以下都是**推測**：Skip＝Ok、Enter／Esc 會確認、每次捲一行、文字白色靠上、公告開著時登入窗照樣畫在底下。
- 沒做：拇指（`Move_Button`）的位置與拖曳、`ScrollWheel_NoticeWin` 的捲動條本體點擊、`;n` 混色、FontSize、音效。
- `Back_Image` 的背景畫法仍然沒查（Q7-14），照舊不畫。
- 真機（交給 Codex／dot，P24Lab 測試角色）：
  1. 原生 UI 開著、kor25 在預設路徑時登入。收到 S48 時，畫面中間應出現公告窗，而不是 Windows 訊息框。按 Ok 後進入角色清單；按 Skip、Enter、Esc 結果也一樣。
  2. 公告很長時，用 ▲▼ 鈕、滾輪、↑↓ 捲動。
  3. 在登入畫面按右下的 Exit，客戶端應正常關閉（音效停、沒有殘留程序）。
  4. 加 `--classic-ui`：S48 仍是 Windows 訊息框。

## 產出路徑

- `engine/source/NativeUi/LoginPanel.cs`
- `engine/source/ClientLogin.cs`
- `engine/source/NativeUi/UiClientBridge.cs`
- `engine/source/LoginFollowupTests.cs`（新）
- 本報告 `docs/reports/issue-815.md`
- 分支 `task/815`

## 給下一棒的提示

- R7-N（參考客戶端字串表）可以接著派；本任務只改了這 3 個檔與新測試檔。
- 要接 ChangePass／OTP／Credit：在 `LoginPanel.Fire` 加分支即可。Credit_Window 要做 TextScroller，讀參考客戶端的 `credits`。
- 真機如果看到公告窗位置或順序不對：繪圖在 `LoginPanel.Paint`，按鈕偏移在 `Window()` 的 `ScrollBar` 分支。
- 要改回「S48 一律用訊息框」：讓 `ShowNativeLoginNotice` 回 false 就行。
