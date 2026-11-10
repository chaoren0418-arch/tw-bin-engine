# Issue #811 T348：R7-I 選單鈕 2／Ctrl+A 與狀態窗

依據：[`docs/reference/gap-matrix.md`](../reference/gap-matrix.md) 第 15.6 節 R7-I 列；FU4§1.1；WIN2§6；#766、#763 報告。

## 做了什麼

1. **鈕 2／Ctrl+A 開原生狀態窗**（`NativeUi/MenuButtonsPanel.cs`）
   - `MenuButtonAction` 加 `Status` 旗標，只有 action 2 有。
   - 鈕 2 與 Ctrl+A（`MenuHotkeyCommand.Status`）會先找 `RenewalStatusPanel`。
   - 狀態窗可用（已登錄、參考 `ReNStatusUIEx.xml` 已載入、沒有停用）時，就切換原生窗。
   - 不可用時，照舊開開發工具的「角色」分頁。`--classic-ui` 沒有原生 root，WinForms 能力點面板不變。
2. **選單熱鍵改走 `root.Toggle`**（同檔，新增 `ToggleWindow`）
   - 鈕 2／3／4／13、Tab／Ctrl+I／Ctrl+S、Ctrl+A，都改成呼叫 `UiRoot.Toggle(panel, open)`。
   - 效果：
     - 在堆疊裡的窗，按一次就關。
     - 被第 5 個窗擠出堆疊的窗，再按一次會回到最上層。
     - 509 規則在開窗之前就判斷。
   - 只有 root 根本沒有這個窗時，才退回面板自己的切換。
   - open 已經執行、但窗沒開成時，不再做其他事。例如 Field_Map 沒有目前地圖，結果和原本相同。
3. **op 43 與 StatInfo**（`NativeUi/StatInfoPanel.cs`）
   - **靜態分析更正**：gap-matrix 寫「op 43 開 StatInfo」，實際是**關閉**。詳見下方「已驗證」。
   - 新增 S64 觀察者 `[WorldReceive(64,Observe=true)] ObserveStatInfoResetHide`：
     - 只有 `CharResetPackets.Parse` 接受的子類 1／2 封包，才發出關窗要求（`StatInfoResetHide.Revision`）。
     - `StatInfoPanel.Update` 看到新要求就關窗。
     - `CharReset.cs` 照常處理 S64，格式錯誤的封包照舊丟出例外。
   - 勾選鈕狀態保留。之後取消勾選再勾起，窗會重新打開（原版這部分未查）。
4. **`StatInfoPanel.ValueProvider` 接線**（`StatInfoPanel.cs`、`BonusStatsUi.cs`）
   - `StatInfoPanel.BonusRows`：依 XML 順序列出 22 列，每列標出所屬能力（STR／DEX／INT／CON／WIS 分 4／5／5／4／4 列，分組是推測）。
   - `BonusValue(name, view)`：從狀態窗的 `RenewalStatusView` 算出該列數值。
   - 產品端在 `SyncNativeStatus` 設定 `info.ValueProvider ??= ClientForm.StatInfoValues(panel)`，畫面重繪時讀狀態窗最新的值。
   - **目前沒有任何一列有已查證的數值來源，所以全部空白**。這些列是「加成值」，引擎現有的總值（MaxHp、MR 等）是不同的數字，不拿來冒充（規則 5）。
5. `NativeUi/RenewalStatusPanel.cs`：只更新檔頭註解，寫明入口與 StatInfo 的關係。程式碼沒改。
6. **新測試** `engine/source/StatusWindowEntryTests.cs`（`[WorldModuleTest("status-window-entry")]`，自動發現），涵蓋：
   - 對應表；
   - 4 種不可用情況都退回「角色」分頁；
   - Ctrl+A／鈕 2 開關原生窗、放開鍵與自動重複不切換；
   - 被擠出堆疊後再按 Ctrl+A 會回到頂端；
   - `ToggleWindow` 的 4 種結果；
   - S64 子類 1／2 關窗，子類 3／4 與格式錯誤的封包不關；
   - 22 列與分組、產品數值供應者全部空白。

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`，也沒改其他測試檔。

## 已驗證

- **op 43 的靜態分析**：用 capstone 讀 `P01-分析基準/input/TW1608262200.bin`（SHA-256 `e12949cf…41a2`，唯讀）。腳本在 `scratch/issue-811/op43.py`、`vt44.py`，不進 git。
  - `sub_94E0E0` 的索引是「子碼 − 1」（`0x94E13A sub eax,1`，跳表 `0x94F030`，位元組表 `0x94F0B4`）。
  - 子類 1（`0x94E164`）在 `0x94E202` 呼叫 `dword_15433B4` vtable+0x44(0)，另一條分支在 `0x94E25B` 也呼叫一次。
  - 子類 2（`0x94E2A3`）在 `0x94E3DA` 呼叫 vtable+0x44(0)。
  - 子類 3（`0x94E3F4`）不碰 StatInfoUI。
  - 建構子 `0xEDCF00` 設的 vtable 是 `0x11E2C54`，+0x44 是 `sub_EDDB80`：參數非 0 走開窗分支，參數 0 走關窗分支（`sub_EE0A50`、`sub_EE0310(1)`）。所以 op 43 是**關閉** StatInfo。
- **StatInfoUI.xml 的 22 列名稱**：取自先前解出的 kor25 檔案（7,371 B，唯讀）。
- **字串 3857–3878**：用 PakViewer CLI 解出 kor25 `Text.idx` 的 `string-c.tbl`（4703 筆，輸出在 `scratch/issue-811/kor25/`），內容都是 `dummyNNNN`。本機表只有 3484 筆，沒有這些字串。
- `check-no-game-assets.py`：pass。

## 未驗證／待驗證

- **本機建置沒有完成**，原因是本機環境：
  - `dotnet build engine/source/OfflineClient.csproj -c Release` 一律失敗，錯誤是 `NETSDK1130 Microsoft.Web.WebView2.Core.winmd`。
  - 原因：本機 NuGet 快取 `%USERPROFILE%\.nuget\packages\microsoft.web.webview2\1.0.4258.31` 缺 DLL 與 `build/*.targets`。改用 worktree 內 `scratch/` 的快取重新還原，`project.assets.json` 記錄的檔案清單還是缺這些檔案。
  - 這和本次改動無關，main 在本機也會遇到。依規定沒有改家目錄。
  - **編譯與測試以 PR 的 CI 結果為準。**
- 依本機規定沒有執行任何測試程式，`status-window-entry` 與既有測試都交給 CI。
- 真機（交給 Codex）：
  - 鈕 2／Ctrl+A 開出參考版面的狀態窗；
  - 勾起 StatusBonus 後 StatInfo 出現在右側；
  - 能力值重置（S64 子類 1）時 StatInfo 關閉。
- 狀態窗在原版的視窗堆疊與 Toggle 行為，是比照其他 Window 層的窗推測的（R5-A 規則），沒有逐一查 CRenewalStatusUI。
- 按 Up 之後是直接送出還是確認後才送（Q7-14）沒查，照現行做法（只做本機選擇，送出仍在 WinForms 面板）。

## 產出路徑

- `engine/source/NativeUi/MenuButtonsPanel.cs`
- `engine/source/NativeUi/StatInfoPanel.cs`
- `engine/source/NativeUi/RenewalStatusPanel.cs`（只改註解）
- `engine/source/BonusStatsUi.cs`
- `engine/source/StatusWindowEntryTests.cs`（新）
- `docs/reports/issue-811.md`

## 給下一棒的提示

- **gap-matrix 15.6 R7-I 列要更正**：「op 43 開 StatInfo」改為「op 43 子類 1／2 關 StatInfo（SetShow(0)）」。
- **新查證項：StatInfo 的數值來源**。
  - TW16 `sub_EE03B0` 用 `sub_7A7590()`（singleton）的 `sub_EBB050(index)` 取值。
  - 這 22 列存在 `+0x23C + index*0x14`（index < 0x16），由 `sub_EB85F0(index)` 計算。
  - 另外，op 43 處理函式在關窗後呼叫 `sub_EBB500(1,0x64)`，看起來是組一個要送出的封包（`sub_C37A50` 寫入器）。
  - 要查清楚的是：這些值由用戶端自己用表格算出，還是要向伺服器要。若要向伺服器要，3.81 伺服器不會回，空白就是正確行為。
  - 查到公式後，只要填 `StatInfoPanel.BonusRows` 每列的 `Value`，接線不用再動。
- R7-N 的 `TextResolver` 統一接線之後，StatInfo 的列名仍會是空白，因為參考字串表也是 dummy。
- R8-D（Ctrl+D 動作窗）改 `MenuButtonsPanel.cs` 時，可直接用 `MenuButtonsPanel.ToggleWindow(root, panel, open, ownToggle)`。
