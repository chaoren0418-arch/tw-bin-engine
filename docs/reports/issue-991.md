# Issue #991 — T433：CU-1 classic 入口可見（G1、G2、G6、G7、G8）

依據：`docs/reference/classic-ui-audit.md` §3、§4（T413／#953）。分支 `task/991`。

## 做了什麼

| 項目 | 檔案 | 修正 |
|---|---|---|
| **G1** 死亡窗被搬進開發工具 | `DevToolsForm.cs`、`Recovery.cs` | 新增「場景常駐控制項」登記（`KeepOnScene`／`DevToolsMovedControls`）；`InitRecovery` 把 `recoveryPanel`、`deathReopenButton` 登記為場景常駐，`InitDevTools` 不再把它們搬進開發工具。「死亡選單」鈕的可見條件補上 `!native`（比照死亡窗），死亡窗本身顯示時也不重複顯示。 |
| **G2** 登入列沒有可見入口 | `ClientLogin.cs`、`DevToolsForm.cs` | 正常入口、非自動化（`demoPort==null`）、原生 UI 不在時，每次進入「不在世界中」的畫面（啟動、登出、斷線、選角）自動打開開發工具「登入／選角」分頁一次（邊緣觸發；使用者關掉後要等下次離開世界才再開）。`Shown` 之後才啟用，避免在 `InitNativeUi` 決定前誤判。 |
| **G6** 本機 HTML 頁在 classic 沒有入口 | `DevToolsForm.cs`、`NativeUi/MenuButtonsPanel.cs`（只改註解） | 開發工具「NPC」分頁頂端加三顆鈕：說明（help）、選項 option1、血盟 option2／option2p，呼叫既有 `ToggleMenuLocalPage`（同頁再按＝關閉）。classic 下 Ctrl+H／V／C 也接上（見 G8）。更正 `MenuButtonsPanel.cs:17–18` 與現況不符的註解。 |
| **G7** S39 查詢頁不會自動跳出 | `PlayerHudServices.cs` | `PlayerHudServiceTracker.Observe` 加選用參數 `queryRevision`／`queryActive`（預設值＝舊行為）；伺服器主動頁（`QueryVisible && QueryActionable`：排名、/buddy、伺服器頁）新版本出現時跳「NPC」分頁。選單本機頁（`QueryActionable=false`）不算，避免和 `ToggleMenuLocalPage` 自己開分頁重複。交易、個人商店的優先順序不變。 |
| **G8** 統計窗手動開與 Ctrl+P 只有原生 | `DevToolsForm.cs`、`Recovery.cs` | classic 快捷鍵分支 `ClassicMenuHotkey`／`ProcessClassicMenuKey`：沿用 `MenuButtonsPanel.CommandHotkeys` 同一張表（Ctrl+Q／Alt+X 統計窗、Ctrl+H／V／C 本機頁、Ctrl+P 截圖；Ctrl+A 不接，classic 由 HUD「角色」）。只在正常入口、原生 UI 不在、在世界中、焦點在遊戲畫面（`GameInputAllowed && Focused`，聊天框／輸入框的 Ctrl+C／V 不受影響）時接手。統計窗切換 `ToggleClassicPlayStats` 比照原生 `MenuPlayStatsSource`：沒顯示→手動開（WinForms 死亡／統計窗，提示「遊戲統計（手動開啟）」）；顯示中→關，死亡窗開著就 Dismiss；手動開不送 C63（原生同樣只在死亡窗由關變開時送）。截圖沿用 `CaptureMenuScreen`（原生不在時 `CaptureLogicalScene` 畫場景）。 |

原生 UI 預設行為：所有新分支都以 `!NativeUiActive`（或 `AttachNativeRecovery()==false`）為前提；`PlayerHudServiceTracker` 只在 `PlayerHudActive`（本身要求 `!NativeUiActive`）時呼叫。原生模式下唯一可見差異：死亡窗／死亡選單鈕從開發工具的兩個雜項分頁移回主視窗，但在原生面板可用時一律隱藏。原生 UserPlayStats 面板故障時，WinForms 死亡窗現在出現在場景上（原本在開發工具裡），這是設計好的退路。

沒有改 `Program.cs`、`WorldSession.cs`、`UiPanelRegistry.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。沒有新增面板。

## 怎麼驗證

- 新增離線測試 `engine/source/ClassicEntryTests.cs`（`[WorldModuleTest("classic-entry")]`，CI 的 `--module-tests` 自動發現並執行）：G1 控制項清單與可見條件、G2 邊緣觸發、G6 按鈕涵蓋三個 `MenuLocalPage`、G8 快捷鍵真值表（含原生在時、焦點在輸入框、不在世界、診斷入口都不接）與統計窗切換、G7 追蹤器（新頁跳、同頁不重跳、換角色重算、舊呼叫不變、交易優先）。
- 本機：`dotnet build engine/source/OfflineClient.csproj -c Release -r win-x64 -p:SelfContained=false`（CI 的 nuget 設定）→ **0 錯誤**，3 個警告都是既有檔案（`MagicUi.cs`、`HtmlActionTests.cs`）。
- `python -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4585 個追蹤檔，0 錯誤）。
- 依本機執行限制，**沒有在本機執行任何編譯出的程式**；module-tests 與其他單元測試交給 PR 的 GitHub Actions CI，結果以 PR 頁面為準。

## 未驗證什麼（待驗證，交真機／Codex）

- G1：classic（`--classic-ui`）真機死亡時，場景中央出現 WinForms 死亡窗；按「取消」後出現「死亡選單」鈕；重新開始可用（SM54，smoke-m7 §10）。
- G2：classic 啟動、登出、斷線時自動跳出開發工具「登入／選角」分頁，而且焦點移動不影響登入 Enter 鍵。
- G6：NPC 分頁三顆鈕在 classic／原生都能開關 help／option1／option2（原生時頁面進 HYPER_TEXT_WINDOW）。
- G7：排名、/buddy、伺服器頁在 classic 自動跳 NPC 分頁（推測，需真機 S39）。
- G8：classic 下 Ctrl+Q／Alt+X 開關統計窗、Ctrl+P 存檔到 `<stage>/runtime/Capture`；Alt+X 是否被系統選單吃掉需真機確認。
- 統計窗手動開時的數值顯示「—」或既有基準值，與原生一致；原版手動開是否送 C63 未查證（沿用原生做法：不送）。
- 本任務沒有宣稱任何資源「沒有」，所以沒有跑 PakViewer 查詢。

## 產出路徑

- `engine/source/DevToolsForm.cs`、`Recovery.cs`、`ClientLogin.cs`、`PlayerHudServices.cs`、`NativeUi/MenuButtonsPanel.cs`（註解）
- `engine/source/ClassicEntryTests.cs`（新）
- `docs/reports/issue-991.md`（本檔）

## 給下一棒的提示

- CU-3（G12–G15）也會動 `DevToolsForm.cs`／`NpcUi.cs` 一帶，請在本 PR 合併後再做。
- 之後新增「必須留在場景上」的 WinForms 控制項（在 `InitDevTools` 之前加入主視窗的），請呼叫 `KeepOnScene(...)`，否則會被搬進開發工具。
- G2 的自動開啟只在 `normalProduct && demoPort==null`；G18（原生 Active 但 LoginUI 不可用）屬 CU-4，可以重用 `ShouldRevealClassicLogin` 的邊緣觸發寫法。
- classic 快捷鍵分支掛在 `ProcessDevToolsKey` 開頭（它在 `ProcessCmdKey` 最先執行）；若之後原生以外還要接 Ctrl+A，記得避開 HUD 的按鍵。
