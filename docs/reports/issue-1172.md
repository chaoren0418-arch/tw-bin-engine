# Issue #1172：開發工具「服務」頁巢狀分頁 UIA TabItem 錯位／「血盟」不在樹中——比照 #1163 修正

**結論：** 巢狀 TabControl `normalPanels`（AccessibleName「開發工具服務分頁」）裡的分頁全是一般 `TabPage`。它們的 HWND 視窗文字等於標籤文字，符合 #1163 找到的 UIA 代理接手條件（已證實，靜態）。

修法採集中處理：在 `ProductEntry.cs` 建立 `normalPanels` 後呼叫 `CrossThreadTabText.Apply(normalPanels)`，對現有與之後加入的每個一般 TabPage 掛上 `NativeWindow` 子類別。其他執行緒送來的 `WM_GETTEXT`／`WM_GETTEXTLENGTH` 會得到空字串，判定與處理和 `DevToolsPage` 共用同一個 helper。各功能檔都沒改。

產品建置通過。真機 UIA 行為**待驗證**，交 `f1165-retest` N01。

## 1. 靜態核對（觸發條件）

#1163 報告〈3. 代理為什麼會介入〉（`docs/reports/issue-1163.md:43–48`）列出的條件有三項：

- 代理 `WindowsTab` 對 **任何** SysTabControl32 的子 HWND 呼叫 `GetOverrideProviderForHwnd`。
- 用 `WM_GETTEXT` 讀分頁 HWND 文字，和標籤文字相同就接手。
- 文字為空字串時不接手。

| 條件 | 巢狀 `normalPanels` | 判定 |
|---|---|---|
| 父層是 WinForms `TabControl`（SysTabControl32） | `ProductEntry.cs:61` `new TabControl{…AccessibleName="開發工具服務分頁"}`；它本身是外層 `DevToolsPage("服務")` 的內容（`DevToolsForm.cs:155` `devTools.AddPage("服務",normalPanels)`） | 已證實 |
| 分頁是一般 `TabPage`，HWND 文字＝`TabPage.Text`＝標籤文字 | 建立點全為 `new TabPage(title)`：`ProductEntry.cs:66`（背包…交易 8 頁）、`BookmarkUi.cs:109` 記憶座標、`BuddyUi.cs:142` 好友／封鎖、`ClanWar.cs:298` 血盟戰爭、`ClanUi.cs:13` 血盟、`ClientEffect6016.cs:34` 聲音、`DuelUi.cs:16` 決鬥（`:20` 以 `AddRange` 加入）、`Marriage.cs:151` 求婚（同經 `DuelUi.cs:20`）、`ObjectRendering.cs:703` 名稱顯示、`PetUi.cs:16` 寵物、`RingWindows.cs:284` 盟屋拍賣（`:297–298` 動態移除／加回）、`TitleAndEmote.cs:381` 稱號／表情 | 已證實 |
| 沒套到 #1163 修正 | #1163 只把 `DevToolsForm.AddPage` 建的外層頁改成 `DevToolsPage`（`DevToolsForm.cs:24`）；巢狀頁不經過 `AddPage` | 已證實 |
| 加入 `normalPanels` 的途徑只有 `TabPages.Add`／`AddRange` | 全庫 `TabPages.(Add\|Remove\|Insert\|Clear)` 只有上表各點；`MailUi.cs:112` 屬信件自己的 TabControl，不在開發工具內，不改 | 已證實 |
| #1165 的現象出自同一機制 | 巢狀列出 15 個 TabItem，看不到「血盟」，名稱與矩形有偏移（`docs/reports/issue-1165.md:76–96`）。這和 #1163〈2. 名稱錯一格〉〈4. 前面的 TabItem 為什麼不在樹中〉描述的「錯一格、前幾頁不在樹中」同型 | **推測**：沒有逐格反推每個名稱對應的原生索引。巢狀有兩排標籤（Multiline），還需要真機確認 |

## 2. 修正

### `engine/source/DevToolsForm.cs`

- 新增 `static class CrossThreadTabText`（`:74`），內容是從 `DevToolsPage` 抽出的共用 helper：
  - `HidesWindowText(uint)`：`InSendMessageEx != ISMEX_NOSEND(0)`，判定與 #1163 相同。
  - `NeedsSubclass(Control?)`：一般 `TabPage` 才需要掛子類別。`DevToolsPage` 自己已處理，不重複掛；非分頁控制項不掛。
  - `TryHide(ref Message)`：遇到跨執行緒的 `WM_GETTEXT`，寫入空字串並回傳 0；遇到 `WM_GETTEXTLENGTH` 回傳 0；回傳值表示是否已處理。
  - `Apply(TabControl)`（`:91`）：對現有分頁掛子類別，並訂 `ControlAdded`。`TabPages.Add`／`AddRange` 都經過 `Controls.Add`，所以之後加入的頁也會觸發。
  - `Attach(TabPage)`：用 `ConditionalWeakTable` 登記。同一頁只掛一次，所以「盟屋拍賣」移除後再加回不會重複掛。
  - 私有 `PageWindow : NativeWindow`（`:101`）：
    - handle 已建立時立即 `AssignHandle`。
    - 訂 `HandleCreated`，handle 重建時重新掛上；舊 handle 由 NativeWindow 在 `WM_NCDESTROY` 自動釋放，殘留時先 `ReleaseHandle`。
    - 移除分頁時 handle 會留在 parking window、不會銷毀，子類別也跟著保留。
    - `WndProc` 呼叫 `TryHide`。
- `DevToolsPage`（`:57`）改成呼叫同一個 helper，外層行為不變：
  - `HidesWindowText` 轉呼叫 `CrossThreadTabText.HidesWindowText`，所以 `DevToolsTabUiaTests` 不必改。
  - `WndProc` 改用 `TryHide`，邏輯逐行等價。
  - 建構子仍設 `AccessibleName=title`。

### `engine/source/ProductEntry.cs:64`

`normalPanels` 建立後、加入前 8 頁之前呼叫 `CrossThreadTabText.Apply(normalPanels)`。

### 沒改的部分

- 任何分頁的順序、標題、內容、顯示條件。
- 巢狀頁的 `AccessibleName`：沒有比照 `DevToolsPage` 設成標題。任務只要求「不改標題／內容」，設了會改變選取頁 Pane 的 UIA 名稱，所以維持原狀。TabItem 名稱仍取 `TabPage.Text`。
- 各功能檔、`MailUi.cs`、`Program.cs`、`DemoChecks.csproj`、各註冊表、`rebuild-lock.json`。

**為什麼不逐檔改：** 集中做法可行，只要動一處，也涵蓋未來新增的服務分頁，因此沒有走「各建立點改用共用分頁類別」的備案。

## 3. 離線檢查（`engine/source/ServiceTabsUiaTests.cs`，名稱 `service-tabs-uia`）

這組檢查沿用 #1163 的 `DevToolsTabUiaTests` 做法，用 `[WorldModuleTest]` 自動發現，由產品的 `--module-tests` 執行（CI）。

為什麼不用 `[CheckGroup]`：要檢查的 `CrossThreadTabText`／`DevToolsPage` 都是 `internal`，`[WorldModuleTest]` 和它們在同一個組件，不必公開型別，也不必改 DemoChecks。兩者都是自動發現，不改註冊表。

檢查內容如下，全程不建 handle、不開視窗：

1. 對 InSendMessageEx＝0／1／2／4／9，共用判定與 `DevToolsPage.HidesWindowText` 結果一致；本執行緒（0）保留標題。
2. `NeedsSubclass`：一般 TabPage 為 true；`DevToolsPage`、`Panel`、null 為 false。
3. `Apply`：
   - 用 #1165 服務頁的 18 個標題建 TabControl。第 1 頁在 `Apply` 之前加入，其餘之後用 `Add`／`AddRange` 加入（決鬥＋求婚比照 `DuelUi.cs:20`）。
   - 每頁都已掛上子類別。
   - 順序與標題不變。
   - 沒有建立 handle，`AccessibleName` 沒被改。
4. 「盟屋拍賣」加入後已掛上；移除後 `Attach` 回 false（不重複掛）；加回後仍掛著。
5. 把 `DevToolsPage` 加進同一個 TabControl，不會被另外掛子類別。
6. 用 #1163 的代理模型 `DevToolsTabUiaTests.ProxyOverrideIndex` 對照：修正前每一頁都會被代理接手；修正後跨執行緒看到空字串，沒有任何一頁被接手。

**限制：** `PageWindow` 的實際 `AssignHandle`／`WM_GETTEXT` 攔截需要真 HWND，離線檢查只驗到「登記與判定」。實際攔截列為待驗證。

## 4. 怎麼驗證（已做）

- `engine/build-product.ps1 -ConfigFile .github/nuget.ci.config`：product、launcher 的 restore 與 build 都是 0，`PASS dev`。
  - 環境變數：`DOTNET_CLI_HOME` 設在 scratch，`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`，TEMP／TMP 都在 scratch。
  - 使用 temporary lock，沒有寫入 `rebuild-lock.json`。
  - product DLL SHA-256＝`998cc2d0983635a888bfde4f04f83408f2410ee84852acee10d6bf385d82ca95`。
- 依本機限制，**沒有**執行產品、`--module-tests`、DemoChecks，也沒有跑 UIA 腳本。`service-tabs-uia` 與既有的 `devtools-tab-uia` 交給 PR 的 CI 執行。

## 5. 未驗證／待驗證（交 `f1165-retest` N01）

- 服務頁 `Find-UiaControl -Name 開發工具服務分頁 -Type Tab` 下：
  - TabItem 名稱＝畫面分頁標題，矩形＝原生標籤（Multiline 兩排都要核對）。
  - 畫面上可見的「血盟」在樹中。
  - TabItem 不帶 `ScrollItemPattern`，代表樹由 WinForms 原生 provider 提供；判準見 #1163〈1.〉。
- 至少對「聲音」「血盟」兩頁 `Select-UiaTab`，讀回同名，並用 GDI 目視確認一致。
- 外層六個 TabItem 仍與 #1165 表格一致（迴歸檢查）。
- 標籤畫面標題沒有變空（本執行緒讀 Text 不受影響；依 #1163 的推論，待目視）。
- 「盟屋拍賣」動態加入或 handle 重建後仍有效：需要盟屋情境，可列為選測。

## 產出

- `engine/source/DevToolsForm.cs`：共用 helper `CrossThreadTabText`；`DevToolsPage` 改呼叫它。
- `engine/source/ProductEntry.cs`：`normalPanels` 掛上 helper。
- `engine/source/ServiceTabsUiaTests.cs`：新增離線檢查。
- `docs/reports/issue-1172.md`：本報告。
- 分支 `task/1172`。

## 給下一棒的提示

1. 重測請用 PowerShell 5.1 的 `System.Windows.Automation`，與 #1165 相同；代理只在這條路徑介入。若改用 UIA3（COM）用戶端，結果不能拿來對照。
2. 如果重測仍看到巢狀 TabItem 帶 ScrollItem，或名稱仍然錯位，先確認該頁 HWND 的跨執行緒 `WM_GETTEXT` 是否回空字串（例如 Spy++ 或 `GetWindowText` 從另一個行程讀）。若沒有回空，代表子類別沒掛上，查 `PageWindow.Hook` 與 handle 建立時機；若已回空，代表問題不在代理 override，需要另外分析。
3. 之後新增的服務分頁只要加進 `normalPanels` 就自動套用，不必改分頁類別。
