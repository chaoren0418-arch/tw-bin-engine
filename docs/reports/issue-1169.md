# Issue #1169：#1165 實機驗收失敗項目分流

**結論**：#1165 的 ✅ 5／❌ 2 中，兩個 ❌（G07、P01）是**同一個根因**，出在測試工具 `work/f1118-uia-path/UiaGdi.ps1` 的 `Select-UiaTab`，不是引擎缺陷。這兩項合成一張程式修正任務。#1165 另外觀察到服務頁巢狀分頁的 UIA 錯位（不在該輪驗收項內），判斷與 #1163 修過的外層分頁是同一類引擎缺陷，所以另開一張修正任務。最後一張重測任務只重測 G07、P01，加上新增的 N01，並附帶回歸 G05、G06。

本張只改文件與規劃檔，沒有改程式，也沒有執行任何程式。

## 來源
- #1165 RESULT 留言（failed，codex-local）。
- `docs/reports/issue-1165.md`、`docs/reports/issue-1165-evidence.json`、`work/f1118-uia-path/README.md`：#1165 的結果只推到 `task/1165`（`89771fa9`），還沒進 main。本張用 `git checkout origin/task/1165 -- <三個檔>` 原樣帶進來，讓後續任務可以從 main 引用。內容沒有改。
- 靜態核對：`work/f1118-uia-path/UiaGdi.ps1:178–198`、`Test-UiaContracts.ps1` 的 `tab-selection-dual-readback`、`engine/source/DevToolsForm.cs`（#1163 `f2f7d36a`）、`engine/source/ProductEntry.cs:61–64`，以及各個 `normalPanels.TabPages.Add` 建立點（grep）。

## 1. 已通過（✅）、之後不重測

| 項目 | #1165 結果 | 之後怎麼處理 |
|---|---|---|
| G04 證據快捷鍵 Ctrl+Shift+F12 | ✅ | 沿用。兩張修正任務都不改主視窗擷取和證據鍵 |
| G05 開發工具 GDI 截圖 | ✅ | **附帶回歸**。原因：`f1165-service-tabs-uia` 會改 `ProductEntry.cs`／`DevToolsForm.cs`，影響開發工具視窗 |
| G06 開發工具 UIA 讀樹 | ✅ | **附帶回歸**，原因同上（巢狀分頁的 UIA 樹會變） |
| F01 建立角色 Invoke／對話框讀樹 | ✅ | 沿用。修正任務不改 `CharacterManagementUi.cs` |
| #1108 R07 七職業 GUI 建角＋一次送出＋GUI 刪除 | ✅ | 沿用。重測任務**不建角、不刪角、不寫 DB** |
| G01 登入前置 | ✅（前置） | 重測時只當前置執行 |

其他沿用項目（#1150 G02／G03、#1149 S02 系列、#1118 S01、#1126 D01／C01、#1108 B01、R05、R08–R15、C01、C02、V01）照 #1165 報告的沿用表處理。

## 2. 失敗項目對應

| 項目 | 分類 | 根因（已驗證／推測） | 對應 |
|---|---|---|---|
| G07 ❌ | (a) 程式缺陷（測試工具） | **已驗證**：`UiaGdi.ps1:188` 呼叫 `SelectionPattern.GetCurrentSelection()`，受控 `System.Windows.Automation.SelectionPattern` 沒有這個方法，正確寫法是 `.Current.GetSelection()`（#1165 用反射核對過，證據 `selection-api.json`）。契約測試的 fixture 把這個不存在的方法直接掛在 pattern 上，所以沒抓到。外層分頁實際上切對了，#1163 的引擎修正有效 | `f1165-uia-tab-selection` |
| P01 ❌ | (a) 同一根因 | 七種用法中只有「切分頁」失敗，原因同 G07。另外有一個定位問題：服務頁同時看得到外層 Tab 和巢狀 Tab，`Find-UiaControl -Type Tab` 會找到 2 個，README 沒寫怎麼處理。這兩點一起在這張修（加 `-Scope Children`） | `f1165-uia-tab-selection` |
| 觀察：服務頁巢狀分頁 TabItem 名稱和矩形錯位、畫面上看得到的「血盟」不在樹中（#1165 報告「G07 原文…」一節） | (a) 引擎缺陷（**推測**） | `normalPanels`（`ProductEntry.cs:61`）的分頁都是一般 `TabPage`，沒有套用 #1163 的 `DevToolsPage` 處理（跨執行緒 WM_GETTEXT 回空字串）。現象和 #1150 G07 外層錯一格相同 | `f1165-service-tabs-uia`（P2）；由重測 N01 驗證 |

- (b) 測試環境／資料問題：**無**。#1165 的環境準備、備份、收尾都完成了，原持久 DB 雜湊沒變。
- (c) 已知限制（不開任務）：
  - S98 reason=2、S6 result=5 是推論。metadata 沒保存 payload，R07 已經用 GUI 和 DB 讀回證實，不影響判定。
  - PrintWindow 不會畫出 ComboBox 的獨立 popup。這是 GDI 擷取的限制，#1165 已改用 UIA ListItem、DB 交叉證明。
  - WGC 不可用（#1126 D01），不再試。

## 3. 開出的任務（`docs/plans/issue-1169.json`）

| id | stage／runner | 依賴 | 範圍 |
|---|---|---|---|
| `f1165-uia-tab-selection` | code／claude-local，P1 | — | 只改 `work/f1118-uia-path/`：修 `Select-UiaTab` 的讀回 API；全檔核對 pattern 成員；`Find-UiaControl -Scope Children`；fixture 改成真實 API 形狀並加防回歸檢查；README 同步 |
| `f1165-service-tabs-uia` | code／claude-local，P2 | — | `ProductEntry.cs`（＋從 `DevToolsForm.cs` 抽共用 helper）：讓 `normalPanels` 的分頁比照 #1163 處理，建議用 `ControlAdded`＋`NativeWindow` 集中做，不逐檔改 |
| `f1165-retest` | test／codex-local，runner_lock、real_machine、milestone_smoke | 上面兩張 | **只重測 G07、P01（只測切分頁）、N01（新增），附帶回歸 G05、G06**，G01 當前置；其餘沿用。不建角、不刪角、不寫 DB |

## 4. 其他依賴 #1165 的排隊任務
用 `gh issue list --state open` 掃過所有開啟中的 Issue 內文，`depends_on` 含 1165 的只有本張 #1169 自己，其他沒有。所以沒有需要關閉或改 `depends_on` 的任務。

## 怎麼驗證
- `docs/plans/issue-1169.json` 由 `scratch/i1169-mkplan.py` 產生（scratch 不進 git），已用 `json.load` 讀回：3 項，`depends_on` 指向正確的 id。
- 根因行號是用 grep 和 Read 靜態核對的；`GetCurrentSelection` 不存在這件事引用 #1165 的反射證據，本張沒有另外執行任何程式。

## 未驗證
- 巢狀分頁錯位的成因是**推測**，要等 `f1165-service-tabs-uia` 靜態核對，再由重測 N01 實證。
- helper 修正後的真機行為，交給 `f1165-retest` 驗證。

## 產出
- `docs/plans/issue-1169.json`
- `docs/reports/issue-1169.md`
- 從 `task/1165` 原樣帶進來：`docs/reports/issue-1165.md`、`docs/reports/issue-1165-evidence.json`、`work/f1118-uia-path/README.md`

## 給下一棒的提示
- `f1165-uia-tab-selection`：修正只有一行，重點是讓 fixture 符合真實 API 形狀，避免同類錯誤再漏掉；可以用 PowerShell 反射唯讀列出成員，但不要對任何視窗跑 UIA。
- `f1165-service-tabs-uia`：先讀 `docs/reports/issue-1163.md` 的根因段落。`RingWindows.cs:282–298` 會動態移除、加回分頁，handle 重建時要重新掛 hook。
- `f1165-retest`：helper 和引擎兩張都要合進 main 後，用 main 重建；本張已把 #1165 的 README 回寫帶進 main。
