# Issue #902 — T387：R10-C 好友／封鎖宿主

依據：`docs/reference/gap-matrix.md` 15.12.6（R10-C）與 15.12.5 的 3:C_ADD_BUDDY、4:FriendUI、4:`ExcludeWindow` 三列；FU9 §9.2。

## 做了什麼

| 檔案 | 改動 |
|---|---|
| `engine/source/BuddyUi.cs` | 新增 `LoginFriendWindowHost : IFriendWindowHost`，並在 `InitBuddyUi()` 設定 `FriendPanels.Host`。Friend → `SendBuddy(Delete)`＝C202，送出後在本機移除；Exclude → `SendBuddy(ToggleExclude)`＝C171；MailExclude 一律回 false。送之前會檢查 `CanSend`／`CanToggle`，並先用 `BuddyPackets.Encode` 驗名稱，名稱不合法就回 false、不丟例外。 |
| `engine/source/Buddies.cs` | `SendBuddy` 加一個可省略的參數 `localRemove`（只對 Delete 有效）。`WorldSession.RemoveBuddyLocal`：照 FU9§9.2 sub_B18F10，「本地移除、不送 56」。`BuddyLocalLine`：把 53 印進聊天欄。`BuddyHyperActs`：`addbuddy`、588 與 588 原文。`LoginConnection` 新增三個成員：`OpenAddBuddyInput`、`BuddyInputCanSend`、`AddBuddyFromInput`；最後一個的規則同 `/addbuddy`，名稱不合法印 53，合法就送 C207＋C4。 |
| `engine/source/NpcInteraction.cs` | 只加 `addbuddy` 的分派。NPC 頁（`ClientHyperAction`）與伺服器主動頁（`QueryOption`）遇到 `addbuddy` 改開 588 名稱框，不送 C125。`HtmlNameKind` 加一個 `AddBuddy`，`PromptText` 加 588；`HtmlNameCanSend` 遇到 AddBuddy 框時看好友存取，不看血盟；`SubmitHtmlName` 遇到 AddBuddy 改走 `AddBuddyFromInput`。R7-D 的六個血盟動作沒動。 |
| `engine/source/FriendHostTests.cs`（新） | `[WorldModuleTest("friend-host")]`，自動發現，見下方。 |

- 好友雙擊 → 聊天列 `"名稱 `：#852 已經做在 `FriendPanel.Whisper` 裡，不需要宿主。這次只用產品的清單來源 `LoginFriendListSource` 再測一次。
- 588 原文：用 `docs/reference/rea-scripts/t267/text_tables.py` 唯讀解密 `D:\lineage開發\0-客戶端\Text.pak` 的 `string-c.tbl`（sha256 `4ebbc1a7…2d43e9`，3484 列）讀出 `588 = '在好友清單上增加誰？'`。同一次也核對了 589「在好友清單上刪除誰？」、53、584、1813。執行期照字串表，表不在時用這一句。
- 沒有新增或使用任何圖，所以沒有「缺資源」的宣稱，也沒有查 pak 清單的需要。
- 舊式 `ExcludeWindow`（vtable `0x117B6A0`）照 gap-matrix 的裁定**不另做**：新式 ExcludeUI 接上 Host 後已經能送 C171。
- 遵守的規則：沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`；`--classic-ui` 的 WinForms「好友／封鎖」分頁和過渡開窗鈕照舊；`FriendPanel` 仍實作 `IUiClosablePanel`（本次沒改）。

## 怎麼驗證

- **本機編譯：通過（0 錯誤；本次 4 個檔案沒有警告）。** 本機 restore WebView2 時會出現已知的 NETSDK1130（#677、#746 也遇過）。只為了編譯，我在 `scratch/t902/nowinmd.targets` 用 `-p:CustomAfterMicrosoftCommonTargets` 拿掉 `.winmd`，改參照 `lib_manual/netcoreapp3.0` 的 DLL，所以 WebView2 的錯誤不會蓋掉其他錯誤。repo 檔案沒動。指令（在 `engine/` 下執行）：
  `dotnet build source/OfflineClient.csproj -c Release -o ..\scratch\t902\build -p:CustomAfterMicrosoftCommonTargets=<scratch>\nowinmd.targets`
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4392 個追蹤檔，0 錯誤）。
- **離線測試 `friend-host`：寫好了，但本機依規定不執行。** 由 CI 的 `--module-tests` 跑，結果以 PR 的 CI 為準。內容如下：
  1. 轉接：Friend 乙 → 只送一包 C202（不接 C4），本機名單立刻少乙。Exclude 丁 → C171；等待回覆時不再送，收到 S250/19 後移出。MailExclude、名稱不合法、不在世界時都不送。
  2. 面板 → 宿主 → 封包：FriendUI Delete 先跳 1813；選「否」不送，選「是」送 C202 甲，下一格清單就少了甲。ExcludeUI Delete 直接送 C171。MailExcludeUI Delete 不送。
  3. 自動發現建出的 FriendUI 會讀 `FriendPanels.Host` 並送 C202；測試結束時還原靜態值。
  4. 雙擊乙 → 聊天列出現 `"乙 `，不送包。
  5. NPC 頁的 `addbuddy`、大小寫不同的 ` AddBuddy `：開 588 框，不送 C125，頁版本 +1。框內輸入 ` 丙 ` → 送 C207 丙＋C4。名稱不合法 → 聊天欄印 53、不送。空白名稱、按取消 → 只關框。關掉對話 → 框失效。
  6. 伺服器主動頁的 `addbuddy`：在該頁開 588 框、頁不關，確定後送 C207＋C4。
  7. 有字串表時，588 提示照表顯示；另外核對接線用的型別與方法都存在。
- 既有測試：`html-actions`（六個名稱、578／579／3361）、`friend-window`、`friend-panel`、`chat-command-followup`（`/addbuddy`、`/delbuddy`）的斷言照理不受影響，以 CI 為準。

## 未驗證／推測（真機再確認）

- **開窗鍵（M37）**：原版用哪個鍵或指令開好友窗還沒查到，目前仍只能從開發工具分頁的過渡按鈕開。
- 588 框的畫法：沿用 R7-D 的頁內名稱框（HYPER_TEXT_WINDOW 內，最多 16 個 Big5 位元組），**不是**原版共用的 InputWindow（引擎自訂，同 R7-D）。空白名稱時原版的行為沒查，目前是不送（推測）。
- 好友刪除後本地移除：照 FU9§9.2 的靜態分析。伺服器之後會不會再推一次 S39 buddy 要真機看；如果會推，名單會照伺服器版本覆寫。
- 實際送出時 C202、C171、C207 的伺服器回應，都要到 P24Lab 真機看（#475 流程）。
- `buddy`／`refreshbuddy`（56）、`delbuddy`（589）這三個 HyperAct 不在本任務範圍，沒做。

## 產出路徑

- `engine/source/BuddyUi.cs`、`engine/source/Buddies.cs`、`engine/source/NpcInteraction.cs`（修改）
- `engine/source/FriendHostTests.cs`（新）
- `docs/reports/issue-902.md`（本報告）
- 分支 `task/902`

## 給下一棒的提示

1. HyperAct `delbuddy`（589）和 `buddy`／`refreshbuddy`（C4）可以照本次 `addbuddy` 的模式補：加一個 `HtmlNameKind`，在 `ClientHyperAction`／`QueryOption` 各加一行分派，然後改 `SubmitHtmlName`。
2. 真機（Codex／dot，P24Lab 測試角色）核對清單：
   - 原生好友窗選人 → Delete → 聊天列出現 1813 → 按 Y → 伺服器名單少了這個人，`/buddy` 確認。
   - 封鎖窗 Delete → 解除封鎖。
   - 雙擊好友 → 聊天列出現 `"名稱 `。
   - 有 `addbuddy` 動作的 NPC 頁或伺服器頁 → 588 框 → 輸入名稱 → 好友名單多了這個人。
3. 本機要編譯時，WebView2 的 NETSDK1130 可以照 `scratch/t902/nowinmd.targets` 的作法處理：拿掉 winmd，改參照 `lib_manual` 的 DLL。
