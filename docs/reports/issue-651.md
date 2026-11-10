# Issue #651：T273 伺服器踢除時顯示模態訊息框（S_KICK）

## 做了什麼
- 依據 `docs/reference/send-sites-2.md` §9：TW16 S_KICK（op 245，`sub_940960`／FO `0x53FD60`）先收尾，再用通用模態訊息框顯示字串，最後 `PostQuitMessage(0)` 結束程式。
- 3.81 對應的封包是 **S227 S_Disconnect**＝`C(227) H(0x01F4) D(0)`（見 `server-protocol.md`「S_Disconnect」）。第一個資料 byte 是 0xF4，不在 TW16 的原因表內，所以一律顯示**字串 64**。
- 新檔 `engine/source/KickNotice.cs`：
  - `KickNoticeRules`：opcode 227、字串 64。文字取自 T228 字串表 `ServerMessageText.Table[64]`（去掉色碼）；沒有表、表太短或那一筆空白時，用備用字「連線中斷。」。
  - `[WorldReceive(227,Observe=true)] ObserveKick`：M7-A 觀察型處理器。它比 `WorldSession.cs` 的 `case 227` 先收到封包，記下 `WorldSession.Kick`（Revision、原因 byte、字串 id、文字），再交給 `ClientForm`。
  - `[WorldSnapshot("kick")]`：把這筆通知放進快照。
  - `ClientForm.OfferKickNotice`：做法和 `CharResetUi.Offer` 一樣，用 `Application.OpenForms` 找到擁有這個 session 的視窗，再 `BeginInvoke` 開 `MessageBox`（OK）。使用者按下確定後呼叫 `Close()` 結束程式，**不回登入畫面**，和原版一致。
  - 自動化情境（`NewsConfirmationRequired=false`，也就是 demoPort 但不是 real-interactive）不開框、不關視窗，只在 events 記一筆 `server-kick`。和 S48 公告的規則相同。
- **沒有改** `WorldSession.cs`：`case 227` 仍然丟出 `ProtocolException`，LoginConnection 會轉成 Faulted 並 `World.Reset()`。這對應原版開框前的收尾（`sub_A23DE0`／`sub_8C2D10`），所以「先斷線、再開框、按確定後結束程式」的順序和原版一樣。
- 通知刻意**不在 `Reset()` 清掉**：例外發生後會立刻 Reset，框是之後才開。畫面只用 Revision 判斷是不是新的通知。
- 新檔 `engine/source/KickNoticeTests.cs`：`[WorldModuleTest("kick-notice")]`，接上 `--module-tests` 入口。測試內容：
  - 規則常數。
  - 合成 S227 的位元組。
  - 註冊方式：只用觀察型、沒有消化型、有快照 key。
  - 收到後核心仍丟例外，並記下原因 0xF4／字串 64。
  - 快照內容。
  - Reset 後通知還在、第二次踢除 Revision 會變 2。
  - 只有 opcode 的 S227。
  - 沒有表、表太短、那一筆空白時，都用備用字。

## 怎麼驗證（已驗證）
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/t651/nuget.config` → `PASS dev`（暫時 lock、沒有寫入；productDll `9fb15c34…`）。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` → 0 錯誤。3 個警告都是既有的，與本任務無關。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → pass（4322 檔、0 錯誤）。
- 依本機限制，`--module-tests`（含 `kick-notice`）**不在本機執行**，交給 PR 的 CI。

## 未驗證什麼（待驗證）
- 模組測試 `kick-notice` 與既有單元測試要等 CI 結果。
- 真機：在 P24Lab 被伺服器踢除時（例如重複登入、GM 踢人），框是否出現、文字是否是本機 `string-c.tbl` 第 64 筆、按確定後是否結束程式。交給 Codex／dot。
- 原生 UI 沒有另做 native 模態框，用的是 WinForms `MessageBox`（和 S48 公告的備用路徑一樣），外觀和原版通用框不同。
- `CharacterManagement.cs:99`：在切換角色或角色管理途中收到 227，仍走既有的例外路徑，不會開框（那條路徑不經 `WorldSession`，不在本任務的檔案範圍內）。

## 產出路徑
- `engine/source/KickNotice.cs`
- `engine/source/KickNoticeTests.cs`
- `docs/reports/issue-651.md`

## 給下一棒的提示
- gap-matrix 第 245 列（S_KICK）可以改成「已做（T273）：模態框字串 64、確定後結束程式」，等 CI 綠、真機確認後再回填。
- 如果要讓角色管理途中的踢除也開框：在 `CharacterManagement` 的 227 分支前加一個類似的通知點，要另開任務，因為會動到共用檔。
- 如果之後要換成原生通用框（`sub_8E4670`），只要改 `ClientForm.QueueKickNotice` 開框的那一行。
