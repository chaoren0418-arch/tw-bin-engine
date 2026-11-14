# Issue #1162 f1150-modal-invoke：「建立角色／刪除角色」延後開 modal＋建角對話框 AccessibleName

- 階段：code（claude-local，第 1 次嘗試）
- 分支：`task/1162`
- 來源：#1150 F01／R07（`docs/reports/issue-1150.md`、`issue-1150-evidence.json` 的 `dialog-errors.json`），分流 #1159。
- 改動檔案：只有 `engine/source/CharacterManagementUi.cs`（模組測試也寫在同一檔，沒有新增檔案）。

## 做了什麼

### 1. Click 只擷取戳記，modal 用 `BeginInvoke` 延後開（解 0x80131505）
- 原本 `createCharacter.Click+=async(_,_)=>await OpenCharacterCreationDialog();`（舊 :47），在 Click 裡同步跑 `dialog.ShowDialog(this)`（舊 :94）；`deleteCharacter.Click`（舊 :48–54）同步跑 `MessageBox.Show`（舊 :52）。
- 現在（`CharacterManagementUi.cs:54–69`）：
  - Click 當下呼叫 `BeginCharacterModal`（:110）：先取防重入閘，再 `ClearInput`、擷取 `login.SessionGeneration`／`login.Revision`（刪角也在 Click 當下擷取角色與提示文字）。
  - 用 `PostCharacterModal`（`BeginInvoke(open)`）把 `ShowDialog`／`MessageBox.Show` 排進訊息佇列，Click handler 立即返回。
  - modal 關閉時先放開閘，再用 `CharacterModalGate.StillCurrent(session, login.SessionGeneration, IsDisposed)` 檢查：表單已關或 session 已換就不送出；有送出時仍帶 Click 當下的 session／revision 戳記給 `login.CreateCharacter`／`login.DeleteCharacter`（它們原有的戳記檢查不變，`CharacterManagement.cs:60–70`）。
- 為什麼能解：WinForms 的 UIA `InvokePattern.Invoke` 會在 UI 執行緒同步執行按鈕的 Click（`PerformClick`）。Click 裡的 `ShowDialog` 會跑巢狀訊息迴圈，要等對話框關掉才返回，所以 Invoke 一直掛著，直到用戶端逾時（`0x80131505`），後面送進同一程序的 UIA 要求也排在它後面。改成 `BeginInvoke` 後，Click 只做排程就返回，Invoke 完成；排進佇列的 modal 在下一輪訊息處理才開，對話框的巢狀訊息迴圈可以正常處理後續的 UIA 要求（`FindAll` 等）。

### 2. 防重入
- 新增 `internal sealed class CharacterModalGate`（:177）：`TryEnter()`／`Exit()`／`Busy`，加上純函式 `StillCurrent`。
- 建角與刪角共用一個閘（`ClientForm.characterModal`）：從 Click 排程開始，到 modal 關閉為止都算占用；這段時間再 Click 一律忽略，不會疊出第二個 modal。`BeginInvoke` 丟例外時會先放開閘再拋出。

### 3. 原生 UI 路徑
- `CharacterScreenHost.CreateFallback`（原 :125，現 :161）改成 `BeginCharacterModal(...)` 後直接呼叫 `OpenCharacterCreationDialog(session, revision)`，**不經 `BeginInvoke`**，所以對話框仍然立即開啟、戳記與 `ClearInput("character-create")` 的時機都和原本相同。唯一差別：已經有建角／刪角 modal 開著時，不會再開第二個（原本也開不了，因為前一個 modal 會擋住原生畫面的輸入）。
- 原生 `Create`／`Delete`（直接送出、不開 modal）沒改。

### 4. 建角對話框 AccessibleName
| 控制項 | AccessibleName |
|---|---|
| 角色名稱 TextBox | 角色名稱 |
| 職業 ComboBox | 職業 |
| 性別 ComboBox | 性別 |
| 六個 NumericUpDown（依序） | 力量／敏捷／體質／精神／魅力／智力 |
| 本機檢查訊息 Label（`notice`） | 本機檢查 |
| 建立鈕 | 建立角色 |
| 取消鈕 | 取消 |

能力值名稱改成共用的 `CharacterCreationDialog.StatNames`，左欄標籤文字不變。驗證規則、職業清單、點數計算都沒改。

### 5. 模組測試
- `[WorldModuleTest("character-modal-gate")]`（`CharacterModalGateTests`，:187），由 CI 的 `P11.ClientEngine.exe --module-tests` 自動發現（`WorldExtensions.cs:135` 掃整個組件），沒改 `Program.cs`、`DemoChecks.csproj` 或註冊表。
- 測試內容：第一次 Click 取得閘、排程或開啟期間第二次 Click 被拒、關閉後可以再開、重複 `Exit` 不會讓兩個 modal 同時通過、`StillCurrent` 的三種情況（同 session／session 已換／表單已關）、六個能力值名稱互不重複且順序正確。
- 選 `[WorldModuleTest]` 不選 `[CheckGroup]` 的原因：被測的型別在主程式組件裡，而 build.yml 的「World module extension tests」步驟會跑所有 `[WorldModuleTest]`，不需要新增 CI 步驟。

## 怎麼驗證
- 本機 `dotnet build engine/source/OfflineClient.csproj -c Release -r win-x64 -p:SelfContained=false --no-incremental`（`DOTNET_CLI_HOME` 設在 `scratch/`，同時設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`）：退出 0、0 錯誤、4 個警告。4 個警告都在 `HtmlActionTests.cs`、`MagicUi.cs`、`MapResources.cs`，本檔 0 警告。
- 依本機限制，**沒有執行**編譯出的程式，模組測試交給 PR 的 CI（`--module-tests`）。
- 沒改 `engine/_integration/dev/rebuild-lock.json`。

## 未驗證（待驗證，交重測 `f1150-retest`）
1. UIA `InvokePattern.Invoke()`「建立角色」在 2 秒內返回、不丟例外。
2. 對話框開啟期間 `Get-UiaTree`／`FindAll(Descendants)` 能讀到「職業」ComboBox、六個能力值 Spinner／Edit（依名稱「力量」…「智力」）與「本機檢查」文字。注意：NumericUpDown 在 UIA 是一個 Spinner 包著 Edit，AccessibleName 會放在外層的 Spinner。
3. R07 七職業切換、能力值設定可以用 GUI 完成；第 6 步「刪除角色」的 Invoke 也能立即返回，確認框可以用 UIA 讀到並按「是／否」。
4. 原生 SelectUI 的「建立角色」備援（CreateFallback）仍然立即開對話框（原生路徑回歸檢查）。
5. 推測但未證實：`BeginInvoke` 排進去的訊息會在 UIA Invoke 的 COM 呼叫返回之後才處理。如果 UIA 伺服端在處理 Invoke 時會順便抽訊息，modal 可能在 Invoke 回傳前就開始；但即使如此，Click 本身已經返回，Invoke 的回應理論上仍然送得出去。這一點要靠真機確認。

## 後續建議（本張不修）
靜態看到其他地方也在事件處理中同步呼叫 `ShowDialog`／`MessageBox.Show`，被 UIA Invoke 觸發時可能有同樣的阻塞問題。以下只是 grep 的位置，還沒逐一確認是不是寫在 Click 裡：
`BoardUi.cs:41–42`、`BuddyUi.cs:128–129`、`CharResetUi.cs:23,26,78`、`ClanUi.cs:43,57,73,84,115,120,121`、`ClanWar.cs:342`、`ClientLogin.cs:84`、`GroundUi.cs:54`、`ItemTransfer.cs:326`、`KickNotice.cs:58`、`MailUi.cs:22,139`。如果之後的 GUI 自動化驗收會用到這些按鈕，建議照本張的作法另開任務處理。

## 產出路徑
- `engine/source/CharacterManagementUi.cs`
- `docs/reports/issue-1162.md`（本檔）

## 給下一棒的提示
- 重測時先 Invoke「建立角色」並計時，再用名稱找 `職業`（ComboBox，用 ExpandCollapse／SelectionItem 切換 7 個職業）、`力量`～`智力`（Spinner 的 RangeValue）、`本機檢查`（Text，內容含「剩餘點數」）。
- 對話框開著時再按一次「建立角色」應該完全沒反應（防重入）。
- 刪角確認框是系統 MessageBox，按鈕名稱是系統語系的「是(Y)／否(N)」。
