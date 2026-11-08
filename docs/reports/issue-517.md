# Issue #517 T211：M7-K 交易詢問與交易流程

依據：`docs/reference/gap-matrix.md` 第 7 節 M7-K；`windows.md`（WIN）§2.1；gap-matrix 第 1 節 op 85、88、145、167。

## 做了什麼

- **`engine/source/Trade.cs`**
  - 收到 S219／252（交易邀請，對應 TW16 op 85 `S_ASK` 的 252 特例）時，交易模組照舊先檢查並記錄邀請（對象、challenge、世界 epoch），然後依旗標決定怎麼呈現：
    - 旗標為 1：呼叫 M7-J 的 `OfferYesNo(challenge, 252, [對方名稱])`，把邀請放進**聊天輸入列 Y／N 詢問槽**。提示文字是「X 邀請你交易。請在聊天輸入列輸入 Y 接受、N 拒絕後按 Enter。」
    - 旗標以 `TradeAskWindow` 表示，對應 WIN§2.1 的 `byte_136E99D`，預設值是 1。
  - 新增 `TradeYesNoLive(p)`：只有在同一個 challenge、同一個 epoch、同一個對象、而且狀態仍是 `invited` 時，詢問才算有效。交易被重置、取消、收到 S112 結果，或世界改變，詢問都會自動失效。
  - 新增 `LoginConnection.TradeYesNo(accept)`：輸入列的 Y／N 走交易模組原本的 `Trade("accept"/"reject")` 送出路徑。資格檢查不變（接受時對方必須相鄰、自己不能死亡），送出的 C121 也不變。
  - `TradeAdmit`、`ResetTrade` 在離開 `invited` 狀態時呼叫 `YesNoHandled(252)`，避免同一個邀請出現兩條回覆路徑。
- **`engine/source/TradeUi.cs`**
  - 旗標為 1 而且狀態是 `invited` 時，**不顯示交易面板**，也不顯示接受／拒絕鈕。原版要等伺服器送 op 88（S52）之後才開交易窗（`TRANSACTION_WINDOW`）。
  - 旗標為 0 時，維持原本的接受／拒絕按鈕。
  - Web 狀態多一個欄位 `inviteOnInputLine`；`trade.js` 不在可改範圍，所以沒有改。
- **`engine/source/YesNoPrompt.cs`（超出原列範圍，共 3 行）**：這是 M7-J 報告「給下一棒的提示」指定的擴充點。只有這裡改了才能把 252 的回覆交給交易模組。
  - `AnswerYesNo` 加 `case 252` → `TradeYesNo`。
  - `YesNoModuleLive` 加 `252 => TradeYesNoLive(p)`。
  - `Text` 加 252 的提示文字。
- **測試**
  - 新檔 `engine/_integration/dev/trade-tests/TradeYesNoChecks.cs`，已接上 `demo-tests`：
    - `DemoChecks.csproj` 加一行 Compile。
    - `Program.cs` 新增 `case "trade-yes-no"`。
    - 同時掛進 CI 已在跑的 `map-continuity`、既有的 `yes-no`，以及預設全跑。
  - `YesNoPromptTests.cs` 的兩個斷言原本寫的是 M7-J 時「252 不進詢問槽」的行為，已改成「252 由交易放進詢問槽」。
  - `rebuild-lock.json` 已用 `--update-lock` 重新產生。

## 怎麼驗證

- `py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` 的結果是 `dev-inputs-verified`。
- `rebuild.py --build --configfile .github/nuget.ci.config` 的結果是 `PASS dev`，productDll SHA-256 `ad45d39d…553310`。這是同步 origin/main 之後的建置。
- `DemoChecks` 各群組都通過。執行時 `TW_CLIENT_ROOT` 指向空的 scratch 目錄，和 CI 的環境一致（原因見下一節）。

  | 群組 | 結果 |
  |---|---|
  | `trade-yes-no` | PASS 354 |
  | `yes-no` | PASS 452 |
  | `map-continuity` | PASS 33286 |
  | `combat-admission` | PASS 1750 |
  | `duel` | PASS 441 |
  | `resurrection` | PASS 553 |
  | `item-status` | PASS 409 |
  | `afflictions` | PASS 580 |
  | `native-ui-canvas` | PASS 429 |
  | `item-transfer`、`pickup`、`hypertext` | PASS（合併前跑） |

- `npc-tests`（含既有的交易端對端合成測試）：PASS 165。
- `P11.ClientEngine.exe --module-tests`：exit 0。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。
- 新測試涵蓋：
  - 交易的 C121 與共用 Y／N 的封包逐 byte 相同。
  - 輸入 Y／N 時精確送出 C121，狀態變成 `accepting` 或 `cancelled`；回答只能用一次。
  - 輸入 Y 之後，要等 S52 才開窗。
  - 對方不相鄰時不能接受、但可以拒絕。
  - 用模組按鈕接受時，詢問槽會清掉。
  - S112 結果、組隊詢問、第二個邀請、epoch 改變、世界尚未就緒時，詢問都會失效或不會加入。
  - 交易詢問沒有逾時自動回覆。
  - 旗標為 0 時不加入詢問槽，也不會自己編一個自動回覆。

## 沒驗證什麼（含推測）

- **沒有實機測試**：沒有啟動遊戲、伺服器或 DB。按 Enter 送出 Y／N 是沿用 M7-J 的機制，沒有實際按鍵驗證。
- **旗標 `byte_136E99D` 是推測**：設定來源待驗證，預設值 1 是推測的。
- **旗標 0 的行為維持現狀**：原版會自動回覆 C121（opcode 33），但回覆值待驗證。所以旗標為 0 時，引擎**不會自動回覆**，改成保留舊的手動接受／拒絕。目前也沒有任何來源會把旗標設成 0。
- 提示文字是引擎自己寫的，沒有載入原版字串表。
- op 88、145、167（S52、S35、S112）的處理和交易窗內容這次沒有改。交易窗仍是 WinForms 面板，不是原版的 `TRANSACTION_WINDOW` 版面（WIN§1.5）。
- `engine/web/trade.js` 不在可改範圍，所以 Web 版的接受／拒絕鈕仍會出現。後端的 `tradeAccept`／`tradeReject` 命令照常可用。
- **本機與 CI 的環境差異**：本機的 `map-continuity` 如果沒有設定 `TW_CLIENT_ROOT`，`DisplayNoticeChecks` 的「S245 unknown message」會失敗。原因是本機讀得到真客戶端的字串表，訊息 999 會被辨識出來；CI 沒有客戶端資源，所以不受影響。這和本次修改無關。

## 產出路徑

- `engine/source/Trade.cs`、`engine/source/TradeUi.cs`、`engine/source/YesNoPrompt.cs`（3 行）
- `engine/_integration/dev/trade-tests/TradeYesNoChecks.cs`（新）
- `engine/_integration/dev/yes-no-tests/YesNoPromptTests.cs`（2 個斷言）
- `engine/_integration/dev/demo-tests/Program.cs`、`DemoChecks.csproj`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告：`docs/reports/issue-517.md`

## 給下一棒的提示

- **真機驗收**（dot，P24Lab 測試角色）：
  1. 兩個角色相鄰並面對面，由 B 邀請 A。
  2. A 的畫面應該**只有輸入列的提示**，沒有交易面板。
  3. A 輸入 `y` 後按 Enter，應該開啟交易窗（S52），而且沒有送出「y」的聊天訊息。
  4. 再測一次，這次 A 輸入 `n`，B 應該收到拒絕。
- 查到 `byte_136E99D` 的設定來源和自動回覆值之後，在 `Trade.cs` 的 252 分支補上旗標 0 的自動回覆。可以直接送 `TradePackets.Answer(challenge, 值)`，送出路徑請沿用 `TradeYesNo`。
- 改 `engine/web/trade.js` 的人：在 `inviteOnInputLine` 為 true 時，隱藏 Web 的接受／拒絕鈕。
- 交易窗改成原版 `TRANSACTION_WINDOW` 版面，需要另外開任務（WIN§1.5）。
