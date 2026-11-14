# Issue #1178 f1173-g06-web-tree：網頁面板文字值不同才寫；UiaGdi 保留壞節點身分、Select-UiaTab 先查 IsEnabled

階段 code／claude-local。來源：#1173 報告（`task/1173` 分支 `cf5470ff`，尚未併入 main）、分流 [#1176](issue-1176.md)。

## 結論

- **G06（主要）**：靜態分析支持「`panel.js` 一直重寫 `sceneStatus`」是最可能的根因，而且找到 #1176 沒寫到的關鍵證據：主程式在**選角時每 50 ms 無條件送一次 `scene`（`available=false`）**，而 `state` 訊息有簽章去重。所以整個網頁面板只有 scene 路徑上的文字會在畫面不變時持續重寫，這和「只有 `sceneStatus` 位置壞掉」一致。沒有找到指向 WebView2 本身的證據，因此**沒有觸發停止條件**，照任務修改 `panel.js`。
- helper 與 fixture 照任務第 2–5 點完成。
- 本張**沒有執行任何程式或 UIA 腳本**；真機效果全部待 `f1173-retest` 驗證。

## 做了什麼（修改前後行號）

### 1. `engine/web/panel.js`

| 項目 | 修改前 | 修改後 |
|---|---|---|
| 新增 `textChanged(current,value)`（純函式）、`setText(id,value)`（值不同才寫 `textContent`） | — | L2–5（含兩行註解） |
| `magicVitals`、`magicNotice` | L89 `…textContent=…` | L93 `setText('magicVitals',…)`、`setText('magicNotice',…)` |
| `sceneStatus`（未進入世界） | L97 | L101 `setText('sceneStatus','尚未進入世界')` |
| `sceneStatus`（場景解碼完成） | L107 | L111 |
| `incomingAttack` | L112 | L116 |
| `missingObjects` | L113 | L117 |

文字內容、運算式、順序、顯示條件、事件與訊息格式都沒有改；只把「`el.textContent=x`」換成「`x` 和目前值不同才寫」。用 `function` 宣告，會被提升，在第一個訊息處理前就可用；`engine/web/` 其他腳本沒有同名的全域名稱（grep 確認）。

`engine/_integration/delta/web/panel.js` 是封存的 delta 副本（專案 CLAUDE.md：`delta/` 有 SHA 封存不改），**沒有改**。

state 路徑（L13–39 的 `experienceResult`、`worldResult` 等）沒有改：這些只在 `PublishWebState` 簽章變動時才送（見下方根因對照），畫面不變時不會重寫，不屬於任務列的「每次都重寫」範圍。

### 2. `work/f1118-uia-path/UiaGdi.ps1`

| 函式 | 修改前 | 修改後 |
|---|---|---|
| `Read-UiaElement` | L102–117：整段 try，任一屬性 null 或拋錯 → 整個節點只剩 `{Error}` | L102–146：先取 `Current`，再逐一讀 `Name`／`ControlType(.ProgrammaticName)`／`AutomationId`／`ClassName`／`BoundingRectangle`（null 算錯），以及 `IsEnabled`／`IsOffscreen`、`IsPassword`（遮罩用）、`GetSupportedPatterns()`，每個都有自己的 try。輸出欄位與順序和以前一樣（Name、Type、Id、Class、Enabled、Offscreen、Rectangle、Patterns），讀不到的欄位是 `$null`；有任何失敗就加 `Error`，內容例如 `UIA property 'Name' is null`，拋錯時為 `UIA property 'ClassName' failed: <例外原文>`，多個錯誤用 ` | ` 串接 |
| `Find-UiaControl` | L118–138 | L147–167，**程式沒改**：仍以 `PSObject.Properties['Error']` 判斷，有 Error 的節點列入 read errors、不參與比對 |
| `Get-UiaTree` | L139–150 | 程式沒改：有 Error 的節點仍計入 `UiaReadErrors`，**G06 判定沒有放寬** |
| `Select-UiaTab` | L182–204：直接 `Select()` | L211–243：`Find-UiaControl` 之後、`Select()` 之前（L214–223）讀 `$item.Current.IsEnabled` 與 `$Tab.Current.IsEnabled`（各自 try）。任一不是 `$true`（含讀取失敗）就丟出 `Select-UiaTab refused: TabItem or Tab is not enabled (IsEnabled); Select was not called: {"RequestedName":…,"TabItemIsEnabled":…,"TabIsEnabled":…,"TabItemIsEnabledError":…,"TabIsEnabledError":…}`，不 Select、沒有鍵盤或座標退路。L224 起的 Select 與雙向讀回判定不變 |

遮罩行為保留：只有 `-MaskPassword` 且 `IsPassword=true` 時 Name 變成 `[PASSWORD]`；如果讀 `IsPassword` 失敗，Name 改成 `$null` 並記錯（不洩漏可能是密碼欄的名稱）。

### 3. `work/f1118-uia-path/Test-UiaContracts.ps1`

新增兩個 fixture（L98、L123）：

- `read-error-keeps-node-identity`：(a) Name=null 的節點，輸出同時有 `Error`（含 `UIA property 'Name' is null`）和 Type=`ControlType.Text`、Id=`sceneStatus`、Class、Rectangle、Enabled；(b) 只有 `ClassName` 拋錯時保留 provider 原文，其他屬性照讀；(c) `Get-UiaTree` 仍把它算成 1 個錯誤，且錯誤節點的 Id 是 `sceneStatus`；`Find-UiaControl -AutomationId sceneStatus` 找到 0 個，並把它列入 read errors。
- `tab-select-refuses-disabled`：TabItem 與 Tab 都停用、只有 Tab 停用、Tab 的 `IsEnabled` 讀取拋錯，三種情況都丟出含 `IsEnabled`、`TabItemIsEnabled`、`TabIsEnabled`、`RequestedName` 的錯誤，**Select 呼叫 0 次**；讀取拋錯時保留 provider 原文。
  - TabItem 的 `IsEnabled` 讀取拋錯不另做 fixture：`Read-UiaElement` 會把它當錯誤節點，`Find-UiaControl` 在 `Select-UiaTab` 前就會丟出「found 0」並附原文，和改版前一樣。

既有 fixture 不受影響：`New-FixtureElement` 預設 `IsEnabled=$true`，所以 `tab-selection-dual-readback` 的 Select 次數斷言（5 次）不變；`read-errors-and-live-sibling` 仍是 4 個節點、2 個錯誤，原文仍保留。檔案因為有中文而加上 UTF-8 BOM（Windows PowerShell 5.1 需要；`UiaGdi.ps1` 原本就有 BOM）。

### 4. `work/f1118-uia-path/README.md`

新增「網頁面板 Name=null 與停用分頁（#1178）」一節：記錄 #1173 G06／N01 的失敗原文與證據位置、**前提**（「開發工具服務分頁」只在 InWorld 啟用，`ProductEntry.cs:73`；CharacterList 時 TabItem 全部 `Enabled=false`，切頁必定失敗）、本張修正內容與重測方式。`Get-UiaTree`、`Select-UiaTab` 表格列與 fixture 說明各加一句；既有回寫紀錄都保留。

## G06 根因對照

**已證實（靜態）**

- 壞節點位置是 `sceneStatus`：#1176 已核對 `g06-dev-tree.json`（零起算第 54 個是 Error，前後是 `sceneImage` 和 `incomingAttack`，整棵樹沒有 `sceneStatus`），對照 `engine/web/index.html:16` 的順序。
- `sceneStatus` 在選角時每秒約被重寫 20 次：
  - `engine/source/Program.cs:171` 每個更新週期呼叫 `PublishWebState();PublishSceneWeb();`。
  - `engine/source/OpacityWeb.cs:9` 只做 50 ms 節流；`:10` 在 `!login.World.Renderable`（選角時成立）時**每次**都 `PostWebMessageAsJson({kind:"scene",available=false,…,sequence=++…})`，沒有去重。
  - 修改前 `panel.js:97` 收到後一律執行 `sceneStatus.textContent='尚未進入世界'`。依 DOM 規格，設定 `textContent` 即使值相同也會移除舊文字節點、插入新的 Text 節點。
- 對照組：`engine/source/WebPanel.cs:142` 的 `PublishWebState` 會組簽章，`if(signature==lastWebState)return;`，畫面不變時不送 `state`。所以 L13–39 的其他文字欄位在選角時不會重寫。#1173 的 183 個節點裡只有 `sceneStatus` 一個 Error，和這個差異一致。
- 舊 helper 的缺口：舊版 `Read-UiaElement`（L105–116）一遇到 null 就只留 Error，所以 #1173 無法直接指出是哪個控制項。

**推測（未證實）**

- Chromium 每次 DOM 變動都會重建這段文字的無障礙節點，UIA 用戶端在 `FindAll` 之後讀 `Current.Name` 時，拿到的可能是已失效或還在重建的節點，結果回傳 null，而不是丟出 `ElementNotAvailableException`。這是 WebView2／Chromium 的內部行為，本機不能執行程式，無法證實。
- 因此「值不同才寫」能讓這個節點在選角時保持穩定，但如果 null Name 其實是 Chromium 對 `<p>` 段落節點的固有行為（和重寫無關），重測仍會看到 Error。這時新版 helper 會在 Error 節點上留下 Type／Id／Class，可以直接確認是不是 `sceneStatus`。

**停止條件判斷**：沒有找到證據顯示 null 與 `panel.js` 重寫無關（例如出現在其他不重寫的靜態節點、或在 InWorld／登入頁也出現）；#1165 的登入頁和 #1173 的三棵服務頁樹都沒有 Error。因此照任務修改網頁，沒有標 `needs:claude`。

## 怎麼驗證（本機，只做靜態檢查）

- `[System.Management.Automation.Language.Parser]::ParseFile`：`UiaGdi.ps1`、`Test-UiaContracts.ps1` 都是 0 個解析錯誤。
- `node --check engine/web/panel.js`（只做語法解析，不執行）：exit 0。
- `git diff --check`：只有 `panel.js` 新增行被標成行尾空白，原因是這個檔案在 index 裡原本就是 CRLF（`git ls-files --eol` 顯示 `i/crlf w/crlf`），不是新增的空白。
- 確認 CI 會用到 `panel.js` 的 DOM 替身：`engine/_integration/dev/demo-tests/web-checks.cjs`、`ground-tests/web-checks.cjs` 的替身元素是帶 `textContent` 屬性的普通物件，`getElementById(id)` 和 `querySelector('#'+id)` 回傳同一個物件，所以 `setText` 和直接賦值的結果相同。
- 沒有改 `engine/_integration/dev/rebuild-lock.json`、`Program.cs`、`DemoChecks.csproj` 或任何註冊表。

## 未驗證什麼

- **沒有執行** `Test-UiaContracts.ps1`（claude-local 不得執行 `Test-*.ps1`）。新舊 7 個 fixture 是否全部通過，要由 codex-local 或 dot 用新的 STA 行程執行：
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-UiaContracts.ps1 -EvidenceDirectory scratch/uia-contracts`
- **沒有執行** `web-checks.cjs`（屬於測試程式）。CI 的 `build.yml` 只做建置，沒有執行 Node 替身測試和 PowerShell fixture，所以 CI 綠燈只代表建置通過。
- 沒有在本機 `dotnet build`／`build-product.ps1`：本張只改 `panel.js`（網頁資源，不參與 C# 編譯）和 `work/` 腳本，建置交給 PR 的 CI。
- `textChanged` 是純函式，但驗收範圍不允許改 `engine/_integration/dev/*/web-checks.cjs`，所以**沒有為它新增 Node 檢查**；重測或後續任務可以在 demo web-checks 加上「同值第二次呼叫不寫入」的斷言。
- 真機（交 `f1173-retest`）：
  1. G06：選角狀態下開發工具預設網頁面板，`Get-UiaTree` 連讀 3 次，每次 0 個 Error；如果還有 Error，記下該節點的 `Type`／`Id`／`Class`／`Error`。
  2. N01：先進入世界再做「開發工具服務分頁」聲音／血盟 `Select-UiaTab`；若在選角時做，預期得到 `Select-UiaTab refused ... IsEnabled`，Select 次數為 0。
  3. 附帶回歸：G07 外層切頁（`Find-UiaControl $dev -Type Tab -Scope Children`）仍然通過，因為外層 Tab 沒有停用，新的 IsEnabled 檢查不應擋住它。

## 產出路徑

- `engine/web/panel.js`
- `work/f1118-uia-path/UiaGdi.ps1`、`work/f1118-uia-path/Test-UiaContracts.ps1`、`work/f1118-uia-path/README.md`
- `docs/reports/issue-1178.md`（本檔）
- 分支 `task/1178`

## 給下一棒的提示

- 重測前先跑一次 `Test-UiaContracts.ps1`（純 fixture，不開 GUI），確認 7 項全部通過，再做真機 G06／N01。
- G06 判定沒有放寬：只要樹裡還有任何帶 `Error` 的節點就是 ❌；新版只是讓 Error 節點多帶 Type／Id／Class 方便定位。
- 一次性包裝腳本現在可以安全地對整棵樹用 `.Type` 篩選（Error 節點也有 `Type` 屬性，讀不到時是 `$null`），不會再像 #1173 那樣因為缺屬性而中斷。
- 如果修正後 G06 仍在 `sceneStatus` 出現 null Name，表示根因不是重寫，而是 Chromium 對這個段落節點的行為。這時應改查 `index.html` 的語意標記（例如是否要加 `aria-label` 或 role），另開任務，不要放寬 G06 判定。
