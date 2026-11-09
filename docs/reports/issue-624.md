# Issue #624 — T263：R2-J Yes/No 收尾

依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-J；WIN§2.1、WO 第 6 節、WIN2§5.3。分支 `task/624`。

## 做了什麼

| 目標 | 做法 | 檔案 |
|---|---|---|
| 復活 322 移進共用詢問槽 | `delegated` 拿掉 322，改列入 `ModuleOwned`。`ObserveResurrectionPrompt` 先判斷是否接受（死亡、`Ready`、還沒要求重新開始），共用槽才排入。回答走 `AnswerResurrection`（C121 `H0 D0 H322 C`，封包不變）。模組自己回覆時用 `YesNoHandled` 清掉共用槽。S85、世界重建或新的 S219 都會讓詢問失效。復活面板只剩 321 使用 | `YesNoPrompt.cs`、`Resurrection.cs` |
| Esc 送拒絕 | WO§6：Esc（27）跟 Enter 走同一條 `sub_A58380` 路徑。條件：有詢問、本視窗（遊戲畫面或聊天列）有焦點、不是自動重複，就送拒絕（C121 C0）並吃掉按鍵；其他情況維持原本的 Esc 行為。掛在 `ProcessCmdKey` 的 `ProcessLoginKeyboardEnter` 那一格，排在聊天 Esc 之前。原生畫布另由 `YesNoPanel.Key` 處理 | `KeyboardNavigation.cs` |
| 拿掉組隊 30 秒本地逾時 | `PartyInvitation.Expires` 與 `PartyClock` 都刪掉，邀請只會因世界更新或死亡失效，或被新的 S219 取代 | `Party.cs` |
| 拿掉求婚 15 秒本地逾時 | 收到的求婚（`invited`）不再套 `InteractionTimeoutMs`。發起方與接受方「等待伺服器結果」的 15 秒保留（那是引擎的等待上限，不是 Yes/No） | `Marriage.cs` |
| 拿掉 PartyUi／DuelUi 的回覆鈕 | 拿掉組隊面板的「接受／拒絕」與決鬥頁的「接受決鬥／拒絕決鬥」。收到決鬥邀請也不再自動切到決鬥頁 | `PartyUi.cs`、`DuelUi.cs`、`Duel.cs`（提示文字） |
| 提示文字改讀字串表 | WO§6：S_ASK 的訊息 id 就是 `string-c.tbl` 的 id。`YesNoProtocol.TableText` 用 #546 的 `ServerMessageText.Format` 帶入 S219 參數，再拿掉色碼。讀不到表、id 超出筆數或該列空白時，退回引擎文字（`FallbackText`，322 也有）。句尾的 Y／N 操作說明是引擎加的 | `YesNoPrompt.cs` |
| 補發起求婚的入口 | 新增 `LoginConnection.ProposeMarriage()`（走原本有保護的 `SendMarriage("propose")`），並在決鬥頁旁邊加一個「求婚」過渡頁（按鈕「向面前角色求婚」，只在婚禮區、面對相鄰角色時能按）。原版的入口是 NPC HTML 動作 `HyperActPropose`，但那個掛點在 `NpcInteraction.cs`（R2-F 的檔案），所以先用過渡頁，**標明是引擎自訂入口** | `Marriage.cs`、`DuelUi.cs`（只負責掛上這一頁） |
| YES／NO 鈕改成 `IUiPanel` | 新增 `NativeUi/YesNoPanel.cs`：Float 層，範圍是 YES (361,567)＋NO (401,567)。左鍵點 YES／NO 就回覆，兩鈕之間的空隙也吃掉點擊，Esc 送拒絕。沒有 XML（InputWindow 是寫死的舊式窗），做法同 HpMpPanel。用 `IYesNoSource` 接共用槽，回覆前會用 revision 確認還是同一個詢問。原生 UI 開啟時隱藏 WinForms 鈕；舊 HUD，或面板 fault 時，照舊用 WinForms 鈕 | `NativeUi/YesNoPanel.cs`、`UiPanelRegistry.cs`（一行）、`YesNoPrompt.cs` |

**範圍外的改動（必要，請審查）**：下列 4 個舊的 dev 測試檔不在「可修改的檔案」清單裡，但它們的斷言寫死了本任務要拿掉的行為。不改的話，本機會失敗（字串表讀得到時）或 CI 會失敗。每個檔案只改了斷言：

- `party-tests/PartyChecks.cs`：「30 秒後邀請失效」改成「沒有本地逾時」。
- `yes-no-tests/YesNoPromptTests.cs`：同上。另外，原本比對寫死文字（`Contains("甲方")`、`Contains("邀請者")`）的地方，改成比對參數，以及 `YesNoProtocol.Text(...)` 的結果。本機有原版字串表時，1000 號的原文沒有 `%0`，舊斷言會失敗。322 那條的註解與名稱也一併更新。
- `marriage-tests/MarriageChecks.cs`：失效原因清單拿掉 `timeout`。
- `trade-tests/TradeYesNoChecks.cs`：`Contains("邀請你交易")` 改成比對參數與 `Text(252,…)`。原因同上：252 的提示文字現在讀字串表。

## 怎麼驗證（已驗證）

- 編譯：`dotnet build source/OfflineClient.csproj -c Release` 成功（只有原本就有的 nullable 警告）；`rebuild.py --update-lock` → `dev-inputs-verified`（310 個來源）；`rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`。同步 main（`df2cd370`，#561）後重產 lock（311 個來源），重新建置得 productDll `30ad39bd…`。
- 模組測試：`P11.ClientEngine.exe --module-tests` → 同步後 29 個測試全過，共 1518 個檢查。新的 `yes-no-followup` 有 66 個檢查，涵蓋：322 排入與回答、321 留在面板、活著時不排入、S85／新 S219 讓詢問失效、Esc 判斷表、組隊與求婚沒有逾時（求婚過了 4×15 秒仍可回答）、PartyUi／DuelUi 沒有回覆鈕、求婚入口（婚禮區外不送，區內送一次 C50）、`YesNoPanel` 的命中、Esc、重複鍵與隱藏狀態、合成字串表的格式化與退回。
- DemoChecks：
  - 本機（讀得到原版字串表）：`yes-no` 452、`marriage` 462、`resurrection` 553、`duel` 441、`combat-admission` 1750，全過。
  - 模擬 CI（`TW_CLIENT_ROOT` 指到不存在的目錄，讀不到字串表）：`map-continuity` 33313、`resurrection` 553、`combat-admission` 1750，全過。
- `check-no-game-assets.py` → pass。

## 未驗證什麼

- **沒有真機**：沒有啟動遊戲、伺服器或 DB。322 實際提示文字、Esc 手感、原生 YES／NO 的畫面位置都沒有看過。
- 本機（讀得到字串表）跑 `map-continuity`，會在 `DisplayNoticeChecks` 的「notice S245 unknown message stays labeled」失敗。那是 S245 鑑定文字依賴字串表，與本任務無關；**推測**在 main 本機也一樣失敗，沒有另外建 main 對照。模擬 CI 的環境下全過。
- 鈕的大小 32×14 與文字鈕的畫法都是推定（原始來源沒讀到）。面板不畫提示文字：原版把文字放在輸入列 (192,581) 280×12，與引擎提示列是否同一列待驗證。
- 原生 UI 開啟、且聊天面板（`ChatPanel`）有焦點時，`UiInput` 會先把 Esc 交給有焦點的面板，結果是關掉聊天輸入，不會送拒絕。這種情況要按第二次 Esc，或用 N＋Enter。要修，需要改 `UiInput` 或 `ChatPanel`（不在本任務範圍）。
- 321 仍用復活面板。WO§6 說 321 也會進 `sub_4FC710`，但 WIN§2.1 把它列為特例，任務也只要求 322，所以沒有動。

## 產出路徑

- 程式：`engine/source/YesNoPrompt.cs`、`Resurrection.cs`、`Party.cs`、`PartyUi.cs`、`Duel.cs`、`DuelUi.cs`、`Marriage.cs`、`KeyboardNavigation.cs`、`NativeUi/YesNoPanel.cs`（新）、`NativeUi/UiPanelRegistry.cs`（一行）
- 測試：`engine/source/YesNoFollowupTests.cs`（新，`[WorldModuleTest("yes-no-followup")]`）；4 個 dev 測試的斷言更新（見上）
- lock：`engine/_integration/dev/rebuild-lock.json`
- 本報告：`docs/reports/issue-624.md`

## 給下一棒的提示

- **R2-K（血盟）**：把 97／221／222 從 `YesNoProtocol.delegated` 移到 `ModuleOwned`，照 322 的做法：模組接受後才 `OfferYesNo`，`YesNoModuleLive` 加一個 case，`AnswerYesNo` 轉給模組的送出函式，模組送出後呼叫 `YesNoHandled`。
- **R2-F（NPC 對話）**：HTML 動作 `propose`（`HyperActPropose`，動作字串是推測）可以直接呼叫 `LoginConnection.ProposeMarriage()`。接上後，請刪掉 `Marriage.cs` 的「求婚」過渡頁，以及 `DuelUi.cs` 掛上這一頁的那行。
- 321 是否也改走共用槽，需要先查 `S_ASK` 特例分支裡 321 的處理，看是不是只差解析方式。
- 原生 UI 下「聊天面板有焦點時按 Esc」的問題，需要動 `UiInput`／`ChatPanel` 的任務來處理。
