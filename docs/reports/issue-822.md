# Issue #822 — T358：R8-C 決鬥指令

依據：`docs/reference/followups-8.md` 第 3 節（Q6-2）、`gap-matrix.md` 第 15.10 節 R8-C。

## 做了什麼
- `engine/source/Duel.cs`
  - 新增 `DuelChatCommand`：照 `PlayerQueryPackets.Parse`（`PlayerQuery.cs:38`）的做法，用第一個空白切出指令字，`/duel`（英文不分大小寫，推測）或 `/決鬥`（字串表 644，寫死）→ `DuelPackets.Invite()`＝C5，沒有欄位。指令字後面的文字不讀（原版 `sub_F54C60` 只送 opcode）。
  - `LoginConnection.DuelChatPacket`：給聊天送出路徑用，回傳 C5、送出提示與 admitted 回呼。不做面對面／狀態檢查（原版不檢查，交給伺服器 `C_Fight` 的 FaceToFace）。
  - `WorldSession.DuelCommandAdmitted`：封包送出後，只有在沒有決鬥流程（idle／ended／rejected／expired／unconfirmed）時才把本機狀態改成 inviting（對手名取面前角色，沒有就空白）；已在決鬥、被邀請（630 詢問槽正等回答）或等待回覆時，封包照送，本機狀態不動。
  - 過時提示（原 `Duel.cs:55`）：「對手顯示『決鬥對手』標記」→「對手可直接攻擊（攻擊游標），名稱不另外標記」（AN#14：原版不畫標記）。初始提示補上 `/duel`、`/決鬥` 入口。
- `engine/source/DuelUi.cs`：開發工具「決鬥」分頁的說明文字補上聊天指令入口；按鈕保留（開發工具入口）。
- `engine/source/PlayerChat.cs`（**超出任務列出的檔案，各 1 行**）：
  - `SendPlayerChat` 的分派鏈加一行 `DuelChatPacket(...)`（放在 `ClientChatCommands` 之後、`PlayerQueryPacket` 之前），沿用聊天列同一個送出閘門、聊天戳記檢查與 1 秒防連點。
  - `Describe` 加一行，`/duel` 顯示「指令 · 決鬥」，不再顯示成一般聊天。
  - 原因：聊天送出沒有自動發現機制，`Duel.cs`／`DuelUi.cs` 都接不到聊天輸入（舊 HUD `PlayerHudChat.cs:93` 與原生 UI `UiClientBridge.cs:309` 都直接呼叫 `SendPlayerChat`），不動 `PlayerChat.cs` 就無法讓指令生效。其他指令沒有改動（FU8§3「不動 PlayerChat.cs 的其他指令」）。
- 新檔 `engine/source/DuelCommandTests.cs`：`[WorldModuleTest("duel-command")]`，自動發現。

## 怎麼驗證
- 編譯：`dotnet restore`（來源用原專案 `engine/vendor`，套件快取放 `scratch/`）＋ `dotnet build source/OfflineClient.csproj -c Release`：0 錯誤；3 個警告都是既有的（`HtmlActionTests.cs:108`、`MagicUi.cs:20`），不是本次改動。
- `engine/tools/check-no-game-assets.py`：pass（errors 0）。
- 離線測試 `duel-command`（依本機限制**未在本機執行**，交給 CI）：
  - 指令字：`/duel`、`/DUEL`、`/決鬥`、帶參數、Tab 分隔都編成 C5；`/duelx`、`/決鬥者`、`duel`、`/ duel` 等不是指令；`Describe` 顯示「指令 · 決鬥」；不進 `ClientChatCommands`／`PlayerQueryPackets`。
  - 聊天閘門：送 C5、提示正確、idle→inviting；等待中再送一次狀態不動；`/duelx` 照一般聊天（C136）；過期戳記、不在世界中都不送。
  - 被邀請（S219/630）時打 `/duel`：C5 照送，邀請狀態與 Revision 不變。
  - 提示文字：初始提示含 `/duel`、`/決鬥`；S250/5 開始決鬥後提示不再提「決鬥對手」標記、改說可直接攻擊。
- CI 結果（PR #842，run 42855601316）：dotnet、map-continuity、npc-offline 全部 pass；`--module-tests` 共 0 失敗，其中 `duel-command`（`P06.DuelCommandTests.Run`）pass，40 項檢查。

## 未驗證什麼
- 真機：在 P24Lab 與測試角色面對面輸入 `/duel`、`/決鬥`，伺服器是否回 630 邀請／91／93 訊息（待驗證，交 Codex）。
- 英文指令不分大小寫是推測（原版比對方式沒查）。
- 原版 `/決鬥` 的中文名執行期讀字串表 644；這裡寫死「決鬥」。
- 攻擊游標圖（BIN PE 游標資源）不在本任務範圍，仍是 `Cursors.Cross`。
- 本任務沒有畫新圖、沒有讀資源，所以不需要 PakViewer 查詢。

## 產出路徑
- `engine/source/Duel.cs`、`engine/source/DuelUi.cs`、`engine/source/PlayerChat.cs`（2 行）、`engine/source/DuelCommandTests.cs`、本報告。
- 分支 `task/822`。

## 給下一棒的提示
- 若審查不接受動 `PlayerChat.cs`，替代做法是做一個聊天指令的自動發現（例如 `[ChatCommand]` attribute），那需要另開任務改 `PlayerChat.cs` 一次，之後指令模組都不用再碰它。
- 聊天列 `InputHelp`（`PlayerChat.cs:24`）尚未列出 `/duel`；可與其他說明文字一併更新。
- 游標圖：`sub_F044C0`（FO `0xB038C0`）選的攻擊游標，見 FU8§3。
