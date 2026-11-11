# Issue #967 報告：T423 Q8-A 聊天列提示（血盟備註、委任）

依據：[`docs/reference/followups-15.md`](../reference/followups-15.md) §2.2、§2.3（T402／#917，PR #964）。分支 `task/967`。

## 做了什麼

### 共用入口（`engine/source/Party.cs`，只加確認入口）
- `ChatLineAsk`：原生面板在**聊天輸入列**發問的入口（原版 `sub_AC98E0`）。接在 R10-F 的 `ChatPanel.LinePrompts` 上：
  - 問的時候把自己的提示放在前面，原來的來源（TradeUi.cs 的交易 140／刪除確認／1028）先存起來；答完（Enter）或 Esc 後還原。
  - 提示是「本機答覆」（`ChatLinePrompt.Answer` 不是 null），答覆直接交給發問的面板，不經聊天送出。
  - 輸入列只有一個提示：新的取代舊的，舊的答覆失效。`Live` 回 false（離開世界、換角色、不再是隊長）時提示自己消失。
  - 沒改 `ChatPanel.cs`／`TradeUi.cs`：原來沒有來源時還原成 null，TradeUi 的 `??=` 下一輪仍接得上。
- `PartyPackets.DelegateConfirmText(name)`：字串 4051。
- `WorldSession.ChatLineAskNotice(text)`：把提示的本機結果訊息印進聊天紀錄（3328 用）。

### ① ModifyMemo → 聊天輸入列 3325（`NativeUi/PledgePanel.cs` 只限備註編輯、`ClanUi.cs` 只限上限）
- 按 ModifyMemo_Button：有聊天窗、接上連線、知道角色名時，在聊天輸入列問 3325（`AskMemo`），不再在血盟窗裡編輯。
- 回答（`AnswerMemo`，對應 `sub_B34270`）：
  - 空白 → 不送；
  - 超過 **60 byte**（Big5，`strlen > 0x3C`）→ 3328 印在聊天欄（`IPledgeWindowSource.MemoNotice` → `WorldPledgeSource` → 聊天紀錄），不送；
  - 其他 → 照舊 `SendMemo` 送 C78 16，畫面等伺服器回送 169。
- 新常數：`MemoPromptMessage=3325`、`MemoTooLongMessage=3328`、`MemoInputLimit=60`；`MemoPromptError` 做 C78 16 字元規則＋60 byte。
  `MemoByteLimit=62` 保留（緩衝區＝S250/169 的固定長度）。
- 沒有聊天窗（`new PledgePanel()`、聊天窗不在）時保留 R7-K 的窗內編輯當後備（上限仍 62，見「未驗證」）。
- `--classic-ui` 的 WinForms 備註框：上限改 60（`MaxLength` 與送出前檢查 `MemoPromptError`），標籤改寫 60。
- 註冊改成 `root=>new PledgePanel(root.Find)`（同檔的 `[UiPanel]`，沒改註冊表）。

### ② 委任 → 聊天輸入列 4051（`NativeUi/PartyPanel.cs` 只限 Delegate）
- 委任鈕：先用 4051（帶隊員名稱）在聊天輸入列問；回答 Y（`YesNoProtocol.Accepts`，同 `sub_A58380`）才送 `"delegate"`（C230 3），送完清掉選取（`sub_956F60(-1)`）。N、空白、Esc 不送，提示收起。
- 回答時用最新的隊伍快照再檢查一次（`DelegateTarget`：自己是隊長、對象有物件 ID、不是自己）。
- `PartyLayout.Interaction.AskDelegate`：PartyPanel 設定；沒設定（`PartyRtPanel`、既有 R7-J 測試）或沒有聊天窗時照舊直接送（後備）。
- 註冊改成 `root=>new PartyPanel(root.SlotGroup(...),root.Find)`；新增 `SelectMember`／`FireDelegate`（等同點格子／按委任鈕，給測試用）。

### 字串來源（PakViewer 查過）
用 `pakviewer-cli pak search <Text.idx> string` 與 `pak extract … string-c.tbl` 查了 6 套客戶端的 `Text.idx`（輸出在 `scratch/t967/<客戶端>/string-c.tbl`，不進 git）：

| 客戶端 | 路徑 | 筆數 | 3325 | 3328 | 4051 |
|---|---|---|---|---|---|
| 本機 | `D:\lineage開發\0-客戶端` | 3,484 | 請輸入目錄內容。(最多60個字/Ese=取消) | 目錄內容太長。(最多60個字) | **沒有** |
| kor25 | `D:\381懶人包kor25(原)` | 4,704 | 請輸入內容。(最多60個字/Ese=取消) | 目錄內容太長。(最多60個字) | 要委任%s 為組隊隊長嗎? |
| pmc | `D:\381懶人包泡麵陳(原)` | 4,704 | 同 kor25 | 同 kor25 | 要委任%s 為組隊隊長嗎? |
| c760 | `D:\760測試\超人760客戶端` | 5,999 | 同本機 | 同本機 | 要委任%s 為組隊隊長嗎? |
| ly | `D:\天堂參考用客戶單\雷雨天堂` | 5,438 | 同 kor25 | 同 kor25 | 要委任%s 為組隊隊長嗎? |
| t89 | `D:\天堂參考用客戶單\89天堂` | 4,550 | 請輸入文字內容. (最大60字節/Ese=取消) | 內容字數超出限制. (最大可輸入60字節) | 是否要把%s任命為隊長？ |

（表的第一行是筆數，第 id 筆在第 id+1 行，與 `ServerStringTable` 相同。）
- 執行時照表；讀不到表（CI）時 3325／3328 用本機表原文。
- 4051 本機表沒有：用 kor25／pmc／c760／ly 共同的「要委任%s 為組隊隊長嗎?」，`%s` 換成隊員名稱。原版用本機表時應是空字串（FU15 推測），這裡改用參考客戶端字面，是引擎的選擇。

## 怎麼驗證
- **編譯**：`dotnet build engine/source/OfflineClient.csproj -c Release`，0 錯誤；警告只有既有的 4 個（HtmlActionTests、MagicUi），改動的檔案沒有新警告。
  本機有一個和本任務無關的環境問題：WebView2 1.0.4258.31 套件根目錄的 `.winmd` 在 Windows 上觸發 NETSDK1130。編譯檢查時用一個放在 `scratch/t967/nowinmd.targets` 的 MSBuild hook（`-p:CustomAfterMicrosoftCommonTargets=…`，改參考 net462 DLL）繞過，**沒有改 repo 的 csproj**。CI（ubuntu 交叉建置）不受影響。
- **離線測試**（新檔 `engine/source/ChatPromptFollowupTests.cs`，`[WorldModuleTest("chat-prompt-followup")]` 自動發現）：
  - ChatLineAsk：放在原來來源前面、本機答覆、答一次、Esc 不答、還原原來來源（含原來是 null）、新的取代舊的、Live false 自己消失。
  - 字串：沒有表時 3325／3328 原文、4051 後備帶名稱；有表時照表（去色碼、`%s`＝名稱）；表沒有 4051 時用後備。
  - 60 byte：30 個中文字可以，31 個（62 byte）／61 個 ASCII → 3328；控制字元照 C78 16 規則擋。
  - ModifyMemo：按鈕不進窗內編輯、3325 上輸入列；空白不送；62 byte 印 3328 不送；60 byte 送一次；Esc 不送；離開世界／換角色提示消失；沒有聊天窗→窗內編輯；沒有來源不問。
  - 委任：先問 4051（帶名稱）不送；N／空白／Esc 不送；Y 送 `delegate Bravo` 並清選取；問完失去隊長→提示消失；不能委任的對象不問；沒有聊天窗直接送；`Interaction` 沒有 AskDelegate 時行為不變。
  - 產品端：`WorldPledgeSource.MemoNotice` 加一行聊天紀錄、不送封包；60 byte 備註照常送 C78 16。
- 既有測試：`PledgeMemoTests`（窗內編輯、62 byte）、`PartyDelegateTests`（Interaction 直接送）、`native-ui-tests/PartyPanelChecks` 走的都是後備路徑，行為沒變。
- **依本機執行限制，沒有在本機執行任何測試程式**；測試結果以 PR 的 CI 為準。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4,558 個追蹤檔，0 錯誤）。

## 未驗證什麼（待驗證）
- **真機再確認**：提示出現後輸入列的樣子、按 Enter／打字開輸入列的行為（沿用 R10-F 的推測）；3328 的字色（`sub_7DC850` 的 palette[24]／[14]，未接，用預設色）。
- 超過 60 byte、空白時，原版提示是否收起：本實作一律收起（推測）。
- 輸入列一開始是空的（推測：`sub_B341F0` 只帶 3325，沒看到帶入舊備註）。
- `sub_51F850(文字, "*")` 的替換沒追（FU15 §2.3），備註照原文送。
- ESC 先給有焦點的血盟窗還是先給輸入列提示：FU15 §2.1 的差異（Q8-C ④）沒處理；血盟窗有焦點時 Esc 可能先關窗，提示留在輸入列。
- 窗內編輯後備（沒有聊天窗時）上限仍是 62 byte：為了讓既有 `PledgeMemoTests` 保持綠燈，沒改 `MemoError`。
- `PartyRtPanel` 的委任鈕（不在可改範圍）仍直接送。

## 產出路徑
- `engine/source/Party.cs`、`engine/source/NativeUi/PledgePanel.cs`、`engine/source/NativeUi/PartyPanel.cs`、`engine/source/ClanUi.cs`
- 新檔 `engine/source/ChatPromptFollowupTests.cs`
- 本報告 `docs/reports/issue-967.md`

## 給下一棒的提示
- 別的面板要在聊天輸入列問（輸入框 3361／577 等 PledgePanel 側欄、4118 刪除確認），可以直接用 `ChatLineAsk.Ask(chat,key,text,answer,live)`；`ChatLineAsk.Chat(root.Find)` 找聊天窗。
- `PartyRtPanel` 要一樣先問 4051：在它的 `Interaction` 設 `AskDelegate`（同 PartyPanel 的寫法）。
- 窗內編輯後備改成 60 byte 時，要一起改 `PledgeMemoTests` 的 62 byte 斷言。
- 追完 `sub_51F5D0` 後，`*` 的替換加在 `PledgePanel.AnswerMemo` 送出前。
- 本機若遇 NETSDK1130（WebView2 winmd），可參考報告中的 scratch hook 只做編譯檢查。
