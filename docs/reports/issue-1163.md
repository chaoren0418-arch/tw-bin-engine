# Issue #1163：開發工具分頁的 UIA TabItem 錯一格（#1150 G07）——根因與修正

**結論：根因已在程式碼層級查明，不是分頁建構順序，也不是 .NET 10 WinForms 的 TabItem 本身錯位。** 問題出在兩套元件湊在一起：

- 測試端：PowerShell 5.1 的 `System.Windows.Automation` 會帶入 .NET Framework 4.8 的 UIA 用戶端代理（`UIAutomationClientsideProviders`）。
- 程式端：.NET 10 WinForms 的 TabControl 把「選取頁 Pane」放在第 0 個子節點。

代理用分頁 HWND 的視窗文字配對標籤，接手選取頁之後的兄弟節點。它的 TabItem 名稱取自 MSAA childId `item+1`，在 .NET 10 會對到前一頁的標題。

修正方式：開發工具的分頁改用 `DevToolsPage`（`TabPage` 子類別）。**其他執行緒**用 `WM_GETTEXT` 讀分頁 HWND 文字時回空字串，代理就不會接手，整棵樹由 WinForms 原生 provider 提供。分頁的順序、標題、內容與快捷鍵都沒改。產品建置與 DemoChecks 編譯通過；真機 UIA 行為**待驗證**，交給 `f1150-retest`。

## 證據對照（已證實）

### 1. #1150 樹裡的 TabItem 不是 WinForms 產生的

- 證據裡每個 TabItem 都帶 `ScrollItemPatternIdentifiers.Pattern`（`issue-1150-evidence.json` → `login-keyboard-tree.json`、`g06-dev-tree.json`）。
- .NET 10 WinForms 的 `TabPage.TabAccessibleObject.IsPatternSupported` 只支援 SelectionItem、LegacyIAccessible（反組譯 `System.Windows.Forms.TabPage.cs:144–153`）。基底 `AccessibleObject.IsPatternSupported` 只回報 Invoke（`System.Windows.Forms.AccessibleObject.cs:758–765`）。所以 WinForms 的 TabItem 沒有 ScrollItem。
- 代理的 `WindowsTabItem : ProxyFragment, ISelectionItemProvider, IScrollItemProvider` 兩種都回報（`uiacsp/WindowsTabItem.cs:11`、`:72–83`）。
- 反過來，Tab 元素只有 SelectionPattern。代理的 `WindowsTab` 一定會回報 ScrollPattern（`uiacsp/WindowsTab.cs:131–142`），所以 Tab 本身與第一個子節點 Pane 都來自 WinForms 原生 provider（`TabControl.cs:184–211`：FirstChild＝`SelectedTab.AccessibilityObject`）。

### 2. 名稱錯一格

- 代理 TabItem 的名稱：WinForms 控制項用 `GetAccessibleName(_item + 1)`，也就是 MSAA childId＝原生索引＋1（`uiacsp/WindowsTabItem.cs:26–40`）。
- WinForms 的 IAccessible 把 childId k 轉成 `GetChild(k-1)`（`AccessibleObject.cs:2072–2085`）。
- .NET 10 `TabControlAccessibleObject.GetChild(0)` 是選取頁的 Pane，`GetChild(i)` 是 `TabPages[i-1]`（`TabControl.cs:131–142`）。
- 合起來：代理 TabItem(原生 i) 的名稱＝`TabPages[i-1].Text`。
- 代理 TabItem 的矩形用 `TCM_GETITEMRECT(_item)`（`WindowsTabItem.cs:246–258`），Select 用 `TCM_SETCURFOCUS(_item)`（`:269–284`），兩者都以原生索引 i 為準。
- 所以「名稱 j ↔ 原生 j+1」。這解釋了 Select「登入／選角」卻落在「控制」。

截圖核對（`dev-initial.png`、`login-keyboard.png`，826×711，視窗左上 98,98）：

| 原生標籤 | 螢幕 x 範圍 | UIA 樹上同一矩形的名稱 |
|---|---|---|
| 0 網頁面板 | 116–221 | （不在樹中） |
| 1 服務 | 221–301 | （不在樹中） |
| 2 登入／選角 | 301–433 | （不在樹中） |
| 3 控制 | 433–511 | 「登入／選角」433,160,78×36 |
| 4 狀態列 | 511–595 | 「控制」511,160,84×36 |
| 5 Button | 595–682 | 「狀態列」595,160,90×36 |

開發工具其實有 **6 個**分頁：最後一個是搬進來的診斷控制項，標題 `Button`。「Button」本身也不在樹中。

### 3. 代理為什麼會介入

- `WindowsTab` 實作 `IRawElementProviderHwndOverride`（`WindowsTab.cs:12`）。
- `GetOverrideProviderForHwnd(子 HWND)` 用 `WM_GETTEXT` 讀分頁 HWND 的文字（`uiacsp/Misc.cs:846–855`），跟每個標籤文字比對，相同就回傳 `WindowsTabChildOverrideProxy(hwnd, CreateTabItem(i), i)`（`WindowsTab.cs:285–301`）。
- 文字是**空字串**時直接回傳 null（`:287–291`），代理不介入。
- WinForms 的 TabPage 視窗文字＝`TabPage.Text`＝標籤文字，所以選取頁一定會配對成功。

### 4. 前面的 TabItem 為什麼不在樹中（推測，與證據一致）

這部分是 UIA core 合併 provider 的行為，Windows 元件，未反組譯：

- 選取頁 Pane 被 override 掛到代理 TabItem(i)。之後的兄弟節點由代理的 `WindowsTab.GetNextSibling(child)` 產生，回傳 `CreateTabItem(child._item + 1)`（`WindowsTab.cs:169–185`）。
- 所以樹上只會看到原生 i+1 以後的代理 TabItem。
- 證據吻合：選「控制」(3) 時只看到原生 4、5；選「登入／選角」(2) 時只看到原生 3、4、5。
- WinForms 原生的 TabItem 鏈（`TabPage.cs:264–275` Pane→`TabPages[0]`，`:102–115` 依序 `GetChild(childId+1)`）在這種情況下沒有被走到。

### 5. 已排除的候選原因

- 「TabPage 在 handle 建立後才加入」：`TabControl.ControlCollection.Add` 與 `_tabPages` 一一對應（`TabControl.cs:36–87`、`:1589–1610`），沒有索引偏移。
- 巢狀 TabControl、WebView2 頁：都不影響開發工具這一層的名稱與索引。
- WinForms 原生的 TabItem：名稱＝`_owningTabPage.Text`（`TabPage.cs:48`），矩形＝`accLocation(index+1)`（`:28–42`），Select＝`SelectedTab=_owningTabPage`（`:89–95`），三者一致。

## 修正（`engine/source/DevToolsForm.cs`）

- 新增 `sealed class DevToolsPage : TabPage`：
  - `WndProc` 收到 `WM_GETTEXT`／`WM_GETTEXTLENGTH`，而且 `InSendMessageEx(NULL) != ISMEX_NOSEND`（訊息從其他執行緒或行程送來）時，回傳長度 0、寫入空字串。
  - 本執行緒的讀取不受影響：WinForms 讀 `Text`、畫標籤時是同執行緒直接呼叫，`InSendMessageEx` 回 0。所以標籤顯示的標題不變。
  - 建構時設定 `AccessibleName = 標題`，讓選取頁 Pane 的 UIA 名稱＝分頁標題。原本 Pane 名稱來自鄰近控制項的文字，例如「請重新選取稱號／表情頁面。」。這樣重測可以直接核對「Pane 屬於該分頁」；TabItem 名稱仍取 `TabPage.Text`。
- `DevToolsForm.AddPage` 改建 `DevToolsPage`；新增 `internal PageTitles` 供離線檢查使用。
- 預期效果（**待驗證**）：
  - 代理的 override 比對不到，樹由 WinForms 原生 provider 提供：Tab → 選取頁 Pane（名稱＝分頁標題）→ 6 個 TabItem。
  - 每個 TabItem 的名稱＝分頁標題、矩形＝原生標籤（`accLocation(index+1)`）。
  - `SelectionItemPattern.Select()` 會設 `SelectedTab` 為同名分頁。
- 沒有改的部分：
  - 分頁順序、標題、內容。
  - `ProcessDevToolsKey` 與 Ctrl+Shift+F9／F10／F11／F12。
  - `SelectPage`、`HideTools`。
- 沒有用座標，也沒有改測試腳本。

## 離線檢查（`engine/source/DevToolsTabUiaTests.cs`）

用 `[WorldModuleTest("devtools-tab-uia")]`（產品內模組測試，自動發現；CI 的 `--module-tests` 會跑全部）。沒有另開 `[CheckGroup]`：依 `m6-contract.md`，新群組要改 `build.yml` 才會在 CI 執行；模組測試不用改任何註冊表。

檢查項目：

1. `DevToolsPage.HidesWindowText`：`InSendMessageEx`＝0 時保留標題；1、2、4、9（SEND／NOTIFY／CALLBACK／SEND|REPLIED）時隱藏。
2. `ProxyOverrideIndex`：照 `WindowsTab.GetOverrideProviderForHwnd` 寫的純函式模型。修正前（HWND 文字＝標題），6 頁都會被代理接手；修正後（跨執行緒看到空字串），沒有任何一頁被接手。
3. `AddPage`：6 頁都是 `DevToolsPage`，順序與標題不變，`AccessibleName`＝標題，內容 `Dock=Fill`。
4. `SelectPage(content)` 仍然選到放這個內容的分頁。

「分頁索引 ↔ 名稱 ↔ 矩形」在真機上要靠原生 tab 控制項與 UIA core，無法抽成純函式。純函式只涵蓋「代理是否接手」這個決定點。

## 怎麼驗證

- 反組譯工具：`ilspycmd 11.2.0`，裝在 `scratch/issue-1163/tools`（`--tool-path`，不改 PATH）。輸出在 `scratch/issue-1163/decomp/`（不入 git）。
  - `System.Windows.Forms.dll` 10.0.8（SDK 10.0.300 的執行階段），SHA-256 `12FA3DD5DF239BD0FABB47FA818D6A802F0EE32C4966A71B6513EC6914098854`。
  - `UIAutomationClientsideProviders.dll` 4.8.9316.0（GAC_MSIL），SHA-256 `4CB84AC0236AEC012D7EA280E9693CEECDECEC760B716B0B76C9256BCF17B991`。
  - 上文的行號都是指這兩份反組譯輸出：`decomp/System.Windows.Forms.{TabControl,TabPage,AccessibleObject}.cs`、`decomp/uiacsp/*.cs`。
- 證據：用 Python 解析 `docs/reports/issue-1150-evidence.json` 的四棵樹，並目視 #1150 的 `dev-initial.png`、`login-keyboard.png` 標籤位置。
- 建置：
  - `engine/build-product.ps1` → `PASS dev`，`dev-inputs-temporary-lock`，`writes: false`。
  - `dotnet build DemoChecks.csproj -c Release` 0 錯誤。
  - `rebuild-lock.json` 未改。
- 依本機限制，**沒有執行**產品、模組測試、DemoChecks 或 UIA 腳本。模組測試交給 PR 的 CI。

## 未驗證／待驗證（交 `f1150-retest`）

- G07 照原規格：
  - 找名稱「服務」的 TabItem → Select → 重讀樹，應出現「服務」頁內容，Pane 名稱「服務」。
  - 再找「登入／選角」→ Select → 樹中出現「建立角色」按鈕。
  - 6 個 TabItem 都在樹中，矩形與 GDI 截圖上的標籤一致。
- G05／G06 回歸：截圖外觀、分頁標題不變。
- UIA core 合併 provider 的細節（上面第 4 點）只是推測。如果重測時樹上仍有帶 ScrollItem 的 TabItem，表示代理還從別的路徑介入，請把整棵樹（含 Patterns、Rectangle）留下來。
- 「服務」頁裡的巢狀 `normalPanels` TabControl（`ProductEntry.cs` 建立）沒有改，它的子分頁在 PowerShell 5.1 UIA 下可能有同樣的錯位。本任務限定只改 `DevToolsForm.cs`；G07 只需要讀到「服務」頁內容。

## 產出

- `engine/source/DevToolsForm.cs`（修改）
- `engine/source/DevToolsTabUiaTests.cs`（新增）
- `docs/reports/issue-1163.md`（本報告）
- 分支 `task/1163`

## 給下一棒的提示

1. 重測請用 main 重建（產品 DLL 有改）。工具仍用 PowerShell 5.1 的 `System.Windows.Automation` 即可；修正針對的就是它帶入的代理。
2. 判斷是不是修好了：TabItem 的 Patterns 應該**沒有** ScrollItem，且 Tab 底下 TabItem 共 6 個（網頁面板、服務、登入／選角、控制、狀態列、Button）。
3. 如果重測仍然失敗（例如仍出現帶 ScrollItem 的 TabItem），照停止條件改提設計：在開發工具加一個 UIA 可操作的分頁切換替代方案（例如每頁一個具名按鈕列），並標 `needs:claude`。
4. 巢狀 `normalPanels` 如果也要用 UIA 操作，可以套用同樣的 `DevToolsPage` 手法：讓其他執行緒讀分頁 HWND 文字時拿到空字串。這要另開任務改 `ProductEntry.cs`。
