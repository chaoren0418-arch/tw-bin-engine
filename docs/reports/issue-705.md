# Issue #705 T293：R4-A 選單鈕、熱鍵與信件入口

依據：`docs/reference/gap-matrix.md` 第 13.5 節 R4-A；`docs/reference/followups-4.md` §1（FU4§1.1 熱鍵 WM_CHAR 表、TW13 `off_8F023C` 選單表）。

## 做了什麼

| 項目 | 原版（FU4§1.1） | 引擎現在 | 檔案 |
|---|---|---|---|
| 選單鈕 1 | `NewMailWindow` | 開 WinForms 信件窗 `MailDialog`（`ClientForm.RequestMail`；等目前的鍵盤／滑鼠處理結束後才開模態窗，重複按不會疊開）。「信件」開發工具分頁不存在的問題一併處理：`ShowDevToolsPage("信件")` 也改開信件窗 | `MailUi.cs`、`NativeUi/MenuButtonsPanel.cs`、`NativeUi/UiClientBridge.cs` |
| 鈕 2、Ctrl+A | `CRenewalStatusUI` | 開發工具「角色」分頁（原生窗缺 `ReNStatusUIEx.xml`，WIN§1.4；暫行） | `NativeUi/MenuButtonsPanel.cs` |
| Ctrl+Q、Alt+X | `CUserPlayStatsUI` | 切換遊玩統計窗（與鈕 7 同一路徑 `TogglePlayStats`） | 同上 |
| 鈕 5、Ctrl+C | HTML `option2`（王族 `option2p`） | `dialogs/templates.json` 的同名頁，以自己的查詢頁顯示在 NPC 面板（開發工具「NPC」分頁）；職業 0（王族）用 `option2p` | `NativeUi/MenuButtonsPanel.cs`（`PageKey`）、`NativeUi/UiClientBridge.cs`（`ToggleMenuLocalPage`） |
| 鈕 6、Ctrl+V | HTML `option1` | 同上，頁名 `option1` | 同上 |
| Ctrl+H | HTML `help` | 同上，頁名 `help` | 同上 |
| 再按一次 | 關閉同一頁 | 同一頁正在顯示時，再按就關閉（`CloseQueryPage`） | 同上 |
| Ctrl+P | 截圖（"Saved current screen to %s"） | 把 800×600 邏輯畫面（原生 UI 啟用時是合成後的畫布，`CaptureLogicalScene`）存成 PNG：`<stage>/runtime/Capture/yyyyMMdd-HHmmss-fff.png`，檔名重複就加 `-2`、`-3`，不覆寫；**不寫遊戲目錄**。路徑記在事件紀錄 | `NativeUi/MenuButtonsPanel.cs`（`CapturePath`）、`NativeUi/UiClientBridge.cs`（`CaptureMenuScreen`） |
| `page` 對不到 | — | `ShowDevToolsPage` 找不到同名分頁時（「小地圖」「血盟」「選項」等退回路徑），在事件紀錄寫 `native-ui-menu`、`status=待驗證：開發工具沒有這個分頁…`；模板沒載入時也記一筆 | `NativeUi/UiClientBridge.cs` |

設計重點：
- 新指令放在新介面 `IUiMenuWindowCommands`（`OpenMail`／`ToggleLocalPage`／`CaptureScreen`），不加進既有的 `IUiMenuCommands`，所以既有測試 `NativeWindowWiringTests` 的假指令物件不用改，也還是通過：沒有新介面時 1／5／6 照舊退回開發工具分頁。
- 新熱鍵放在新表 `MenuButtonsPanel.CommandHotkeys`（7 個鍵）；既有 `Hotkeys`（Tab／Ctrl+I／Ctrl+S，3 個）不變，兩張表沒有重疊。
- 橋接做不到時（不在世界、模板沒載入、還沒有視窗 handle）回 false，選單鈕退回開發工具分頁；Ctrl+H 退回「NPC」分頁；Ctrl+P 什麼都不做。
- 原生 UI 關閉時：信件仍可從舊 HUD 的「信件」鈕開（`MailUi.Attach`，同一個 `OpenMail`，現在多了防止疊開的旗標）。這次沒有新增 `[UiPanel]` 面板，所以不需要 `IUiClosablePanel`。
- 沒動 `ProductEntry.cs`（不需要補「信件」分頁，改成直接開信件窗）；沒改 `rebuild-lock.json`；沒有新封包。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| 產品編譯 `OfflineClient.csproj`（Release，SDK 10.0.300） | 0 錯誤；2 個警告都在既有的 `MagicUi.cs:20`，與本次無關 |
| `python engine/tools/check-no-game-assets.py` | `pass`，errors 0 |
| 新離線測試 `engine/source/MenuHotkeyTests.cs`（`[WorldModuleTest("menu-hotkeys")]`，自動發現） | 本機**沒有執行**（使用者規定本機不跑編譯出的程式）。PR #718 CI（run 42666114808，`--module-tests`）：`menu-hotkeys` pass，41 項檢查，0 失敗 |
| 既有單元測試 | PR #718 CI 三項 job（dotnet、map-continuity、npc-offline）全綠；既有的 `native-window-wiring` pass，47 項檢查，0 失敗 |

測試內容：選單對應表；7 個熱鍵與既有熱鍵不重疊、Ctrl+M／Backspace／單鍵 H／Ctrl+O 不收；`PageKey`（王族 `option2p`）；截圖路徑只在 `runtime/Capture`、重名加序號；每個熱鍵按下只動作一次、放開被吃掉、自動重複不動作；選單 1／5／6／2／7 的分派、都不登出；橋接拒絕時的退回；只有舊介面時的退回；包在 `UiMacroCommands` 裡也找得到；離開世界不收熱鍵；產品橋接類別實作新介面（反射）。

本機編譯備註：worktree 的 `engine/.nuget` 第一次 restore 時 WebView2 套件的 build targets 沒被寫進 `project.assets.json`，出現 NETSDK1130。改用原專案的離線 `vendor`（`scratch/issue-705/NuGet.Config`）、新的 packages 目錄（`--no-cache --force`），並把中繼目錄指到 `scratch/issue-705/obj` 才編過。這是本機環境問題，跟程式碼無關；CI 不受影響。

## 未驗證什麼（待驗證）

- **真機**：沒有啟動客戶端。原生 UI 下按選單鈕 1 會不會真的跳出信件窗、Alt+X 在 WinForms 的 `ProcessCmdKey` 是否收得到（WM_SYSKEYDOWN）、Ctrl+C／Ctrl+V 在原生聊天輸入有焦點時是否先給聊天窗（`UiInput.Key` 是先給有焦點的面板，推測不會被選單搶走），都要交給 Codex／dot 真機確認。
- **HTML 頁的選項**：`option1`／`option2`／`help` 用 `ShowQueryPage` 顯示，頁上的連結與動作是**停用**的（查詢頁的既有規則）。要點連結換頁（例如 `option1` → `option11`）要等 R4-I 的原生 `HYPER_TEXT_WINDOW`，或另開任務給 `NpcInteraction.cs` 開一個「本機頁」入口（本任務不能改那個檔）。
- 本機頁會取代正在顯示的查詢／伺服器頁（同一個 HYPER_TEXT_WINDOW）；進行中的 NPC 對話／商店不受影響。原版是否也這樣取代：推測，待驗證。
- **截圖**：原版的檔名格式、圖檔格式（推測 BMP）與聊天欄的 "Saved current screen to %s" 都沒有查也沒有做；引擎存 PNG，只記事件紀錄。
- 鈕 2／Ctrl+A 只開「角色」分頁，不是原版能力窗（缺 XML）。
- 原版 TW16 的 Ctrl+H／V／C 函式（`sub_8CC330`、`sub_8CC400`、`sub_7F7240`）沒有展開（FU4§9 已列）。

## 產出路徑

- `engine/source/NativeUi/MenuButtonsPanel.cs`：`MenuButtonAction` 加 `Mail`／`Page`；`MenuLocalPage`、`MenuHotkeyCommand`、`IUiMenuWindowCommands`；`CommandHotkeys`、`PageKey`、`CaptureDirectory`、`CapturePath`、`Run`；`Invoke`／`Key` 分派。
- `engine/source/NativeUi/UiClientBridge.cs`：橋接實作 `IUiMenuWindowCommands`；`ToggleMenuLocalPage`、`CaptureMenuScreen`；`ShowDevToolsPage` 的「信件」改道與「沒有分頁」的待驗證紀錄。
- `engine/source/MailUi.cs`：`RequestMail`、防止疊開的旗標。
- `engine/source/MenuHotkeyTests.cs`：新的離線測試（`menu-hotkeys`）。
- `docs/reports/issue-705.md`：本報告。

## 給下一棒的提示

- R4-I（原生 `HYPER_TEXT_WINDOW`）合併後：把 `ClientForm.ToggleMenuLocalPage` 改成交給原生窗，並讓本機頁的 link 可以換頁（現在是停用的）。判斷「同一頁再按就關閉」靠 `menuLocalPage` 和 `QueryHtml` 一起比對，搬移時要保留。
- 若要讓本機頁的連結在 WinForms 下也能點：需要在 `NpcInteraction.cs` 加一個「本機頁」入口（類似 `ShowServerPage`，但 action 不送 C125），屬於別組的檔案。
- 真機核對清單：原生 UI 開啟 → 進世界 → 鈕 1 跳出信件窗（只一個）；鈕 5／Ctrl+C 出現「對話／血盟／同盟／組隊／朋友選項」（王族是「命令選項」）；鈕 6／Ctrl+V 出現「功能選項」；Ctrl+H 出現說明，再按一次關閉；Ctrl+Q、Alt+X 開關統計窗；Ctrl+P 後 `<stage>/runtime/Capture` 多一張 PNG、遊戲目錄沒有新檔；在聊天輸入中按 Ctrl+C／V 不會開選項頁。
