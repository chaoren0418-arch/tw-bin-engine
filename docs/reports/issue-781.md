# Issue #781 — T335：R6-E 聊天指令與本地確認

依據：`docs/reference/followups-7.md` 第 8 節 R6-E（FU7§1.1、§1.4、§1.5、§6.2）。主控裁定：(1) 移除 `/on 頻道` 與 `/check`；(2) 交易詢問旗標跟 C244 角色設定一起存，用原版位元 `0x40`。

## 做了什麼

### ① 交易詢問旗標（`Trade.cs`、`NpcInteraction.cs`、`ChatChannelToggle.cs`）
- `TradeAskWindow` 不再寫死 true，改讀角色設定：`ChatChannelSettings.AcceptTrade`（TWCC v2 選項位元組）。
  - **位元 `0x40`＝接受交易詢問**（原版 `sub_999B50`：`byte_136E99D = (a1 & 0x40) != 0`）。
  - 另用 **`0x20`＝「記錄過」**：舊的 v2 資料沒有 `0x20`，照原版預設 1 處理，不會因為升級就變成自動拒絕。只有 `0x40` 沒有 `0x20` 的資料視為格式錯誤。`0x08`、`0x80` 仍然拒收（既有測試不變）。
  - 沒切換過時位元組與以前完全相同（`Flags` 仍是 `0x07` 那組）。
- `/accepttrade`（只有英文名，不分大小寫，不帶參數）與 HTML 動作 `accepttrade`（NPC 頁與自己頁）：把旗標取反，**不送 C26／C121**，接著把新值經 C244 存進角色設定。舊版（不可編輯）設定不覆寫，只在本場有效。
  - HyperAct 名稱 `accepttrade` 在 TW16 名稱字串區 FO `0xDA7860`（與 `checkpk` FO `0xDA78A4` 同一區，本次唯讀讀 `P01-分析基準/input/TW1608262200.bin` 確認）。
- 旗標 0 時收到 S219/252：不開詢問，標記 `TradeAutoReject` 並設 `TradeCancelRequired`；連線層收包後原有的觸發點（`LoginConnection.cs:65`，未修改）呼叫 `CancelChangedTrade`，這時送 **一次** `TradePackets.Answer(challenge,false)`（和原版 `chdhcs` 33,0,挑戰值,252,0 相同）。自動拒絕期間不能接受。

### ② 頻道指令改成原版語法（`ChatChannelToggle.cs`、`PlayerChat.cs`）
- `/whisper`、`/悄悄話` ＋ `on`／`off`（不分大小寫）或 `開`／`關` → C26 type 2。
- `/tradechat` ＋ 參數 → type 6。
- `/receive`、`/收到` ＋ 參數 → type 3：3.81 C26 不處理，**不送**，只在聊天列回報。
- 沒有參數或參數不對：不送，回報用法。
- 舊的 `/on 頻道`、`/off 頻道` **已移除**，現在會當一般聊天送出（原版也沒有這種語法）。

### ③ 查詢指令（`PlayerQuery.cs`）
- `/checkpk` → 無欄位的 C51（同 HTML `checkpk`），走聊天列同一個閘門與節流；帶參數不送。
- 加 `/誰`（360）＝`/who`、`/階級`（779）＝`/rank`。
- `/check` **已移除**（含 C125 pc_list → 等提示 → C223 的整段流程）；伺服器送來的 `pc_show` 頁照舊顯示。`CheckBegin`／`CheckTarget` 封包形狀保留給測試向量與寵物 C223 對照。

### ④ 刪除物品的本地確認（`ItemTransfer.cs`、`PlayerChat.cs`）
- 刪除不再開 `MessageBox`，改成聊天輸入列提示，答覆由 `SendPlayerChat` 接手（這一列不當聊天送出）：
  1. 數量 >1：先問數量（4115，本機 `string-c.tbl` 沒有，用引擎文字）。輸入全部的數量才會往下；**輸入比持有量少不送**，提示「3.81 的 C138 只帶物件編號、伺服器整疊刪除」。非數字或超過持有量就取消。
  2. 確認 Y/N：數量 1 用 **3122**（有 `string-c.tbl` 就讀表，`%s`＝名稱，色碼拿掉；沒有表用文件記載的原文）；數量 >1 用 4114（本機表沒有，用引擎文字）。Y 開頭才送（同 `YesNoProtocol.Accepts`），其他取消。
- 新的刪除提示取代舊的；連線世代或世界改變後提示失效；請求只能用一次。
- 給予物品維持原本的 MessageBox（不在本組範圍）。

### 因裁定移除舊語法而連動修改的既有測試（範圍外檔案，只改舊語法的斷言）
- `engine/source/ChatChannelToggleTests.cs` 第 4 段、`engine/source/ChatFormatTests.cs` 第 8–9 段：`/on 頻道` 改成 `/whisper on|off`。
- `engine/_integration/dev/player-query-tests/PlayerQueryChecks.cs`：`/check` 流程改成 `/checkpk`、別名、`/check` 不再是指令。
- `engine/_integration/dev/trade-tests/TradeYesNoChecks.cs`：旗標 0 段改成「自動送一次拒絕」。

### 新測試
- `engine/source/ChatCommandFollowupTests.cs`（`[WorldModuleTest("chat-command-followup")]`，自動發現）：位元組與 TWCC 往返、頻道指令解析與送包、`/accepttrade` 存 C244 與自動拒絕、`/checkpk`／別名／`/check`、刪除提示的數量與 Y/N 流程（用合成字串表或明確「沒有表」）。

沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`LoginConnection.cs`、`rebuild-lock.json`。

## 怎麼驗證
- **編譯**：產品 `OfflineClient.csproj`（含 engine/source 的模組測試）與 `DemoChecks.csproj` 都 **0 錯誤**（只有原本就有的 `MagicUi.cs` 警告）。
  - 本機環境問題：使用者全域 NuGet 快取裡的 `microsoft.web.webview2/1.0.4258.31` 解壓不完整（缺 DLL 與 `build/*.targets`），導致 NETSDK1130。沒有動共用快取；改用 `scratch/issue-781/nuget` 獨立套件目錄還原，並用只放在 `scratch/` 的 `webview2-fix.targets`（`CustomAfterMicrosoftCommonTargets`）補上 WebView2 DLL 參考後編譯。**這不是 repo 的改動**，CI 用乾淨的 nuget.org 還原。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4174 檔、0 錯誤）。
- 依本機限制，**沒有執行任何編譯出來的程式**；模組測試與 DemoChecks 由 CI 執行。
- **CI（PR #798，run 42784013549／42784013232）**：`dotnet`、`map-continuity`（含全部 `[WorldModuleTest]`）、`npc-offline` 全部 pass；新群組 `chat-command-followup` pass（86 checks，0 failures）。

## 未驗證什麼
- 所有離線測試的執行結果（交給 CI）。
- 真機：`/whisper off` 後伺服器是否真的不送密語、`/tradechat`、`/checkpk` 的伺服器回覆（562）、`/accepttrade` 關閉時對方看到的結果、刪除提示在原生聊天列的顯示位置。
- 原版的 `option13` 本機選項頁本引擎沒有；HTML 動作 `accepttrade` 切換後維持目前頁面，不重開。
- 原版頻道指令不帶參數時的行為沒查，本引擎當用法錯誤處理（不送）。
- 哪些物品需要刪除確認（`sub_996B10`，Q6-9）沒追，這裡一律確認。
- 舊 HUD 聊天列送不出空白輸入，所以數量提示的「空白＝全部」只能用輸入全部數量代替。
- Esc 取消刪除提示要接在 `KeyboardNavigation.cs`（不在本組範圍）；目前輸入非 Y 的任何字也會取消。

## 產出路徑
- 程式：`engine/source/Trade.cs`、`ChatChannelToggle.cs`、`PlayerChat.cs`、`PlayerQuery.cs`、`ItemTransfer.cs`、`NpcInteraction.cs`
- 測試：`engine/source/ChatCommandFollowupTests.cs`（新）；連動修改見上
- 報告：`docs/reports/issue-781.md`
- 分支：`task/781`

## 給下一棒的提示
- 交易旗標位元：TWCC v2 最後一個位元組 `0x20|0x40`；其他模組要改這個位元組時用 `ChatChannelSettings ... with{...}`，不要重建，否則會丟掉 `AcceptTrade`。
- 若要做原版選項窗（`option12`／`optiong`／`option13`）：切換交易詢問直接呼叫 `LoginConnection.ToggleAcceptTrade(stamp)`；頻道切換呼叫 `SetChatChannel`。
- Esc 取消刪除：在 `KeyboardNavigation.cs` 的 Esc 分支呼叫 `login.CancelItemDelete()`。
- 刪除提示的文字：`ItemDeleteText`（3122 讀表；4114／4115 本機表沒有，取得完整字串表後只要表裡有就會自動讀表）。
