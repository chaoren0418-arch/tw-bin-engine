# Issue #905 報告：T390 R10-F 聊天列確認與提示

依據：`docs/reference/gap-matrix.md` 15.12.6（R10-F）與 15.12 的 1:29、4:ExchangeUI 列；主控裁定 #880 第 1 點（ExchangeUI 的小差異照原版做）。

## 做了什麼

1. **1028 的 Y 接上 `AnswerPrivateShopConfirm`**（1:29）——`PlayerChat.cs`，只改確認分派：
   - `SendPlayerChat` 在刪除確認之後，多一個分支：有待確認的 1028（`PrivateShopConfirmCurrent`）時，這一列就是答覆。戳記（世代／世界／角色）不符時回 false、不答；符合時呼叫 `AnswerPrivateShopConfirm`。Y 會用原來那一包再走一次 `SendPrivateShopBatch`，所有驗證照舊；其他答覆（含空白）不送。回 true 表示這一列已當作答覆處理。
   - 這一改，WinForms 聊天列（`--classic-ui`）與原生聊天列都會走同一個分派。
2. **聊天列提示的共用入口**——`NativeUi/State/ChatState.cs`：
   - 新增 `ChatLinePrompt(Key, Text, Answer?, Cancel?)`。`Answer` 不是 null 的是本機提示（交易 140），答覆直接交給交易窗；`Answer` 是 null 的照常走 `SendChat`，由上面的分派接手（刪除確認、1028）。
   - 新增 `ChatInputLine.ValidFor(prompt)`：有提示時任何一列都能送出，包括空白（140 的「全部」）。
3. **原生 `ChatPanel` 畫 `playerChatNotice` 與提示**——`NativeUi/ChatPanel.cs`：
   - 新增 `LinePrompts`／`Prompt`。輸入列關著時，CHAT_BAR 上畫提示（`PROMPT`）；沒有提示時畫 `PlayerChatView.Notice`（`NOTICE`，也就是 playerChatNotice），只畫得下的開頭。輸入列開著時，提示畫在輸入文字前面；太長時只畫尾端，游標在尾端。
   - 有提示時：Enter 會打開輸入列，同一個 Enter 就答覆；打不帶 Ctrl／Alt 的字母或數字也會打開輸入列，字元照常走 WM_CHAR。答完關閉輸入列。Esc 等於 `Cancel`，不送；輸入列開著時一併關閉。換成新的提示（Key 不同）時清掉草稿。
4. **交易 140 改在聊天輸入列問**（4:ExchangeUI）——`NativeUi/TransactionPanel.cs`：
   - 新增 `AskOnChatLine`、`LinePrompt`、`AnswerPrompt`。`AskOnChatLine=true` 時，140 交給聊天列：文字框不畫 140，數字、Enter、Backspace、Esc 都讓給聊天列（Esc 只取消提示，不送 C86）。答覆照 R9-K 的規則：空白＝全部、1…持有量、超過 16 件印 255。
   - `AskOnChatLine=false`（沒有原生聊天窗，或聊天窗出錯）時，保留 R9-K 的文字框做法當備援，既有 `trade-amount`、`trade-followup` 測試不變。
5. **接線**——`TradeUi.cs`：
   - `AttachTransactionWindow` 把 `ChatPanel.LinePrompts` 接到 `NativeChatLinePrompt`；聊天窗可用（找得到、沒出錯）時設 `AskOnChatLine`。
   - 提示的優先順序：交易 140 → 刪除確認 → 1028（`ChatLinePrompts.Of`，在 `CaptureWorld` 鎖內讀）。刪除確認的 Esc 用 `CancelItemDelete`；1028 的 Esc 用 `AnswerPrivateShopConfirm("")`，也就是不送。
6. **新測試** `ChatLineConfirmTests.cs`（`[WorldModuleTest("chat-line-confirm")]`，自動發現）：
   - 1028 經 `SendPlayerChat` 分派：舊戳記不答；N 和空白都不送，提示收掉；Y 送出原來那一包（C161 位元組相同），送出後狀態為 submitted。
   - `ChatLinePrompts.Of`：1028 的提示文字與 Key；Esc（Cancel）放棄 1028；沒有待答時回 null。
   - `ChatPanel` 的按鍵：沒有提示時空白不送；本機提示一個 Enter 就答（空白）；數字鍵打開輸入列再答；只有字母、數字會打開；Esc 在輸入列關著或開著時都會取消；新提示清掉草稿；送出型提示經 SendChat（Y、空白），送出後關閉，沒送成就不關。
   - `ChatPanel` 的繪製（合成 CENTER_WINDOW 版面與 surf）：notice 畫在 CHAT_BAR 的 (143,586)；提示優先於 notice；長提示只畫開頭；開著時先畫提示、再畫輸入；長提示加輸入時只畫尾端。
   - 交易 140 在聊天列：交易窗讓出數字、Enter、Backspace、Esc；答 3 → C37 ×3；空白 → 全部；9 超過持有量 → 不送，文字框顯示原因；Esc 只取消提示，之後的 Esc 才送 C86；交易結束時提示跟著消失；備援模式維持原行為。

## 怎麼驗證

- **已驗證（本機）**：
  - `dotnet build engine/source/OfflineClient.csproj -c Release` 編譯成功，新改的檔案沒有新警告。
  - 本機 SDK 對 WebView2 winmd 會報 NETSDK1130，這個問題和本次修改無關，#675／#678／#679 也遇過。所以本機建置時暫時用 `scratch/task905/nowinmd.targets`（未入 git，`NUGET_PACKAGES` 指到 worktree 內 `engine/.nuget`）。
  - `python engine/tools/check-no-game-assets.py`：pass（errors 0）。
- **交給 CI**：依本機執行限制，本機不執行任何編譯出來的程式，所以既有單元測試、`--module-tests`（含新的 `chat-line-confirm`）都以 PR 的 GitHub Actions 結果為準。
- **資源**：沒有用到新的圖或字串表，也沒有任何「缺資源」的宣稱，所以本次不必查 PakViewer。1028、140 的字串沿用 #809、#855 用 PakViewer 讀本機 `Text.pak`／`string-c.tbl` 的結果。

## 未驗證什麼（待驗證，交給真機）

- 原版的提示出現時，輸入列是不是自動取得焦點並打開。引擎的推測是：要按 Enter 或打字才打開，同一個 Enter 就答覆。
- 提示的字色、提示與輸入之間的空白、過長時截頭或截尾。
- CHAT_BAR 上平常顯示的 playerChatNotice 是引擎自己的狀態訊息，原版沒有這一行。要不要保留，請真機時對照。
- Esc 在 1028／刪除提示等待中會先被聊天列吃掉（取消提示），之後才輪到全域 Esc 關窗（FU4§1.1 說聊天列在全域分派之前）。這是推測，要真機確認。
- 真機核對：照 gap-matrix 15.12.6 的 M11，原生模式下私商合計 >99999 → 1028 出現在聊天列 → 答 Y 送出、答 N 不送；交易窗加入堆疊 → 140 出現在聊天列 → 輸入數量或 Enter。

## 產出路徑

- 程式：`engine/source/PlayerChat.cs`、`engine/source/NativeUi/ChatPanel.cs`、`engine/source/NativeUi/State/ChatState.cs`、`engine/source/NativeUi/TransactionPanel.cs`、`engine/source/TradeUi.cs`
- 測試：`engine/source/ChatLineConfirmTests.cs`
- 報告：`docs/reports/issue-905.md`
- 分支：`task/905`

## 給下一棒的提示

- `PrivateShopTradeSource.cs` 檔頭（第 21–25 行）和 `AnswerPrivateShopConfirm` 的註解仍寫著「還沒接上」「原生 ChatPanel 不畫 playerChatNotice」。這個檔案不在本任務的可改範圍，下一張碰到它的任務請順手更新。另外，原生模式下 1028 仍會另外寫到狀態列（`Set(State,text)`），現在可以考慮拿掉。
- gap-matrix 的 1:29、4:ExchangeUI 列可以改判為「已有（待真機）」。
- R10-J（`/qbookmark`）與 InputHelp 的 `/duel` 排在本任務之後，都要改 `PlayerChat.cs`。本任務只在 `SendPlayerChat` 開頭的確認分派加了一段（刪除確認之後），不影響指令解析。
- 其他聊天列 Y/N 提示（例如交易邀請 `TradeAskWindow`、YesNoPrompt 系列）如果要畫在原生聊天列，可以在 `ChatLinePrompts.Of` 或 `NativeChatLinePrompt` 再加一個來源，不必改 ChatPanel。
