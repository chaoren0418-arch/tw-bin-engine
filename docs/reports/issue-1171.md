# Issue #1171 f1165-uia-tab-selection：Select-UiaTab 改用受控 API，Tab 定位可限定直接子節點

階段 code，claude-local，第 1 次嘗試；分支 `task/1171`。

## 結論

- `Select-UiaTab` 的 Tab 側讀回改為受控 API `pattern.Current.GetSelection()`，不再呼叫不存在的 `GetCurrentSelection()`（#1165 G07／P01 ❌ 的根因）。其餘判定不變。
- `Find-UiaControl` 新增 `-Scope Children|Descendants`（預設 Descendants，既有呼叫不受影響）。
- 全檔其他 pattern 呼叫以反射核對，**沒有發現同類錯誤**。
- 契約 fixture 改成真實 API 形狀，並加防回歸斷言與 `-Scope Children` 檢查。
- 依 claude-local 限制，**沒有執行任何 UIA 腳本或 fixture**；只做 PowerShell 靜態解析（5 個 .ps1 皆 0 錯誤）與唯讀反射。

## 做了什麼

### 1. `work/f1118-uia-path/UiaGdi.ps1`

| 項目 | 修改前 | 修改後 |
|---|---|---|
| `Select-UiaTab` 讀 Tab 選取 | L188 `($Tab.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern)).GetCurrentSelection()` | L192–194（註解兩行＋）`($Tab.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern)).Current.GetSelection()` |
| `Find-UiaControl` 參數 | L119 `param(... [ref]$ReadErrors)` | L119–122：加 `[ValidateSet('Children','Descendants')][string]$Scope='Descendants'` 與註解 |
| `Find-UiaControl` 搜尋 | L122 `FindAll([TreeScope]::Descendants, ...)` | L125–126：依 `$Scope` 選 `TreeScope.Children`／`TreeScope.Descendants` |
| 找不到／多筆錯誤訊息 | L132 | L136：訊息多帶 `scope='...'`，其餘原文不變 |

`Select-UiaTab` 不變的部分：先 `SelectionItemPattern.Select()` 一次；TabItem 讀名稱＋`SelectionItemPattern.Current.IsSelected`；Tab 選取必須恰一筆且 ordinal 同名；兩邊各自 try/catch，失敗時兩邊觀察與例外原文都進錯誤 JSON；不做鍵盤或座標退路。TabItem 仍以 `Find-UiaControl $Tab -Name $Name -Type TabItem`（Descendants）找，靠名稱＋型別唯一。

### 2. 反射成員表（唯讀）

來源：`C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL\UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll`，以 `Assembly.GetType(...).GetMembers(Public|Instance|DeclaredOnly)` 列出（含巢狀 `*Information` 型別），沒有對任何視窗操作。完整輸出在 worktree 的 `scratch/issue-1171/uia-members.json`（不入 git）。

| 型別 | 方法 | 屬性 |
|---|---|---|
| `SelectionPattern` | （無；**沒有 `GetCurrentSelection`**） | `Current`、`Cached` |
| `SelectionPattern+SelectionPatternInformation` | `AutomationElement[] GetSelection()` | `CanSelectMultiple`、`IsSelectionRequired` |
| `SelectionItemPattern` | `Select()`、`AddToSelection()`、`RemoveFromSelection()` | `Current`、`Cached` |
| `SelectionItemPattern+SelectionItemPatternInformation` | — | `IsSelected`、`SelectionContainer` |
| `ExpandCollapsePattern` | `Expand()`、`Collapse()` | `Current`、`Cached` |
| `ExpandCollapsePattern+ExpandCollapsePatternInformation` | — | `ExpandCollapseState` |
| `ValuePattern` | `SetValue(string)` | `Current`、`Cached` |
| `ValuePattern+ValuePatternInformation` | — | `Value`、`IsReadOnly` |
| `InvokePattern` | `Invoke()` | — |
| `TextPattern` | `GetSelection()`、`GetVisibleRanges()`、`RangeFromChild()`、`RangeFromPoint()` | `DocumentRange`、`SupportedTextSelection` |
| `Text.TextPatternRange` | `GetText(int)` 等 | — |
| `RangeValuePattern` | `SetValue(double)` | `Current`、`Cached`（Information：`Value`、`IsReadOnly`、`Maximum`、`Minimum`、`LargeChange`、`SmallChange`） |
| `TogglePattern` | `Toggle()` | `Current`、`Cached`（Information：`ToggleState`） |
| `AutomationElement` | `FindAll`、`GetCurrentPattern`、`TryGetCurrentPattern`、`GetSupportedPatterns` 皆存在 | — |

`work/f1118-uia-path/` 全部 pattern 呼叫核對：

| 位置（修改後） | 呼叫 | 判定 |
|---|---|---|
| UiaGdi.ps1 L158（C#） | `InvokePattern.Invoke()` | ✅ |
| UiaGdi.ps1 L185 | `SelectionItemPattern.Select()` | ✅ |
| UiaGdi.ps1 L189 | `SelectionItemPattern.Current.IsSelected` | ✅ |
| UiaGdi.ps1 L194 | `SelectionPattern.Current.GetSelection()` | ✅（本張修正；原 `GetCurrentSelection()` ❌） |
| UiaGdi.ps1 L205 `Select-UiaItem` | `SelectionItemPattern.Select()` | ✅ |
| UiaGdi.ps1 L206 `Expand-UiaCombo` | `ExpandCollapsePattern.Expand()` | ✅ |
| UiaGdi.ps1 L219 `Set-UiaValue` | `ValuePattern.SetValue(string)` | ✅ |
| UiaGdi.ps1 L221／L226 | `ValuePattern.Current.Value` | ✅ |
| UiaGdi.ps1 L229 | `TextPattern.DocumentRange.GetText(-1)` | ✅ |
| README 範例 | `ExpandCollapsePattern.Collapse()` | ✅ |

RangeValuePattern／TogglePattern 目前沒有使用。`Test-NativeClient.ps1`、`Test-Notepad.ps1`、`Test-UiaHelpers.ps1` 沒有其他 pattern 方法呼叫。

### 3. `work/f1118-uia-path/Test-UiaContracts.ps1`

- fixture `FindAll` 依 TreeScope 分流：Children 只回直接子節點，Descendants 遞迴整棵子樹（原本不論 scope 都回 `Children`）。既有 `read-errors-and-live-sibling` 的元素沒有孫節點，結果不變。
- `tab-selection-dual-readback`：pattern 改為 `{ Current = <Information>; Cached = $null }`，`GetSelection()` 掛在 `Current` 上；計數與失敗開關也移到 `Current`。新增斷言：
  - 受控 `SelectionPattern` 反射沒有 `GetCurrentSelection`，且有 `Current` 與 `SelectionPatternInformation.GetSelection()`（fixture 與真實型別漂移時會失敗）；
  - fixture pattern 不提供 `GetCurrentSelection`，直接呼叫必定拋錯（防回歸）；
  - `Select-UiaTab` 定義內不得含 `GetCurrentSelection`、必須含 `.Current.GetSelection()`。
  - 原本四個拒絕情境（wrong-name／not-selected／multiple／provider-error）與「Select 5 次、讀 5 次」不變。
- 新增 `find-scope-children-outer-tab`：dev → 外層 Tab → TabItem「service」→ 巢狀 Tab。預設與明確 `Descendants` 都 `found 2` 拋錯；`-Scope Children` 只回外層 Tab；`-Scope Subtree` 被 ValidateSet 拒絕。

### 4. `work/f1118-uia-path/README.md`

- 新增「分頁 helper 修正（#1171）」節與 G07 重測步驟（`Find-UiaControl $dev -Type Tab -Scope Children` → `Select-UiaTab` 服務 → 登入／選角）。
- `Find-UiaControl`、`Select-UiaTab` 兩列改寫為實際 API；fixture 說明加上 `-Scope Children`；#1150 舊範例加註。
- #1150／#1165 的已驗證／失敗紀錄原樣保留。

## 怎麼驗證

- 靜態解析：`[System.Management.Automation.Language.Parser]::ParseFile` 對 5 個 .ps1，錯誤數皆 0（不算執行腳本）。
- 唯讀反射：如上表，確認 `GetCurrentSelection` 不在受控 `SelectionPattern`，`GetSelection()` 在 `SelectionPatternInformation`；其他用到的方法／屬性都存在。
- 推送後看 PR CI（CI 不跑這些 PowerShell，只確認引擎建置未受影響；本張未改引擎）。

## 未驗證什麼（待驗證）

- `Test-UiaContracts.ps1` 本身**未執行**（claude-local 不得執行 `Test-*.ps1`）；新斷言與 fixture 是否全綠需由 codex-local／dot 以新 STA 行程跑：
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-UiaContracts.ps1 -EvidenceDirectory scratch/uia-contracts`
- 真機（交 `f1165-retest`）：G07 以 `Find-UiaControl $dev -Type Tab -Scope Children` 取外層 Tab，`Select-UiaTab` 切「服務」→「登入／選角」不再拋例外、讀回 `TabSelection` 恰一筆同名；P01「切分頁」一項變 ✅。
- 真實 WinForms TabControl 的 `SelectionPattern.Current.GetSelection()` 回傳內容（是否恰一個 TabItem、名稱與 TabItem 一致）只有反射型別證據，沒有實機讀值。
- 服務巢狀 Tab 名稱／矩形偏移（#1165 觀察）不在本張範圍。

## 產出路徑

- `work/f1118-uia-path/UiaGdi.ps1`、`Test-UiaContracts.ps1`、`README.md`
- `docs/reports/issue-1171.md`（本檔）
- `scratch/issue-1171/uia-members.json`（不入 git）

## 給下一棒的提示

- 重測先跑 `Test-UiaContracts.ps1`（純 fixture，無 GUI）確認 5 項全綠，再做 G07 真機。
- 取外層 Tab 一定用 `-Scope Children`；每次切頁後重新 `Find-UiaControl`，不要沿用舊 element。
- 若真機 `GetSelection()` 回 0 筆或多筆，錯誤 JSON 會同時帶 TabItem 與 TabSelection 原文，照錯誤判斷，不要加鍵盤或座標退路。
