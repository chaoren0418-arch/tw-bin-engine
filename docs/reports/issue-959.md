# #959 T417：R11-C 好友 HyperAct

依據：`docs/reference/gap-matrix.md` 15.13.5（3:C_ADD_BUDDY 列）、15.13.6（R11-C）；FU9§9.2（`docs/reference/followups-9.md` 第 357 行，已驗證）：HyperAct `buddy`／`refreshbuddy` → `c` 56；`addbuddy` → 588；`delbuddy` → 589。

## 做了什麼

| 檔案 | 改動 |
|---|---|
| `engine/source/Buddies.cs` | `BuddyHyperActs` 加 `Buddy`／`RefreshBuddy`／`DelBuddy`、589 常數與原文、`Classify`（去空白、不分大小寫）、`IsInput`／`InputText`／`InputPrompt`。`LoginConnection.BuddyHyperAction(action,query)` 是好友 HyperAct 的分派入口：`addbuddy` → 588 框、`delbuddy` → 589 框、`buddy`／`refreshbuddy` → 只送 C4；其他字串回 null（照舊走 C125）。原本的 `AddBuddyFromInput` 改成 `BuddyFromInput(kind,name)`：588 → C207＋C4，589 → C202＋C4。送包共用 `SendBuddySequence`：第一包放行後才送後面的包，lease／世代檢查和原來一樣。 |
| `engine/source/NpcInteraction.cs`（只加分派） | `QueryOption`（`:160`）與 `ClientHyperAction`（`:181`）的 `IsAddBuddy` 那一行改成呼叫 `BuddyHyperAction`。`HtmlNameKind` 加 `DelBuddy`。`PromptText`、`OpenHtmlNamePrompt` 的提示、`HtmlNameCanSend`、`SubmitHtmlName` 從「只認 AddBuddy」改成「認 588／589」，每處只改一個條件。 |
| `engine/source/BuddyHyperActTests.cs`（新） | `[WorldModuleTest("buddy-hyperact")]`，自動發現，內容見下方。 |

- 589 原文：用 `docs/reference/rea-scripts/t267/text_tables.py`（唯讀）從 `D:\lineage開發\0-客戶端\Text.pak` 的 `string-c.tbl`（sha256 `4ebbc1a7a002a4c1dcdfa73dbf056b0e00648e0789763410c40e5079a22d43e9`，3484 列）讀出：589＝「在好友清單上刪除誰？」。同次也讀了 588「在好友清單上增加誰？」、53「無效的名字。…」來對照。執行時照字串表顯示；字串表不在時用同一句。
- 封包：C4＝`04`；C202＝`CA`＋名稱＋NUL（3.81 `C_DelBuddy` 只讀 `S`，`docs/reference/server-protocol.md`）。這跟 `/delbuddy 名稱`（原版 `cs` 124＋`c` 56）送的一樣。
- 這次沒有新的面板、封包處理或註冊表，所以不涉及資源，也不用 PakViewer 查圖。

## 怎麼驗證

- **本機編譯：通過，0 錯誤；這次改的 3 個檔案沒有警告。** 本機 restore WebView2 會出現已知的 NETSDK1130（#902、#922 也遇過）。只為了編譯，我照 #902 的做法用 `scratch/t959/nowinmd.targets` 拿掉 `.winmd`，repo 沒動。指令（在 `engine/` 下）：`dotnet build source/OfflineClient.csproj -c Release -o ..\scratch\t959\build -p:CustomAfterMicrosoftCommonTargets=<scratch>\nowinmd.targets`。
- **`check-no-game-assets.py`：pass**（tracked 4502、errors 0）。
- **離線測試 `buddy-hyperact`：測試已寫好，本機依規定不執行**，由 CI 的 `--module-tests` 跑，結果看 PR 的 CI。測試內容：
  1. 名稱分派：`buddy`／` RefreshBuddy ` → Query；`DelBuddy` → Delete；`buddylist`、`delbuddy 甲`、空字串、null → 不是好友動作。另外確認 589／588／3361 的編號、589 原文和沒有字串表時的退回文字。
  2. NPC 頁的 `buddy`、`refreshbuddy`、大小寫不同的 `Buddy`：每次只送一包 C4，不送 C125，頁面不關、不開框；收到 S39 buddy 回覆後名單有更新。
  3. NPC 頁的 `delbuddy`：開 589 框，頁版本 +1，不送封包；按確定送 C202 乙＋C4（名稱去頭尾空白）。名稱不合法時印 53、不送；空白名稱不送；取消只關框；同一頁的 `addbuddy` 588 → C207＋C4 跟原來一樣；關掉對話後框失效。
  4. 伺服器主動頁：`buddy`／`refreshbuddy` → C4，頁不關；`delbuddy` → 589 框（Query）→ C202＋C4。
  5. 不是本組的動作（`buddylist`）照舊送 C125。
  6. 沒有送出閘門時不能送，框保留；傳送中（`TeleportPending`）時 `buddy` 不送，也不會落到 C125。
  7. 有字串表時，589／588 的提示照表顯示。
- 既有的 `friend-host`（588）、`html-actions`、`buddy-page`、`chat-command-followup` 測試的斷言照理不受影響，以 CI 為準。

## 未驗證什麼

- **CI 結果**：push 後看 PR 的 CI。
- **推測／引擎自訂（真機再確認）**：
  - 589 框的名稱不合法時印 53、不送，這是比照 588（`sub_F43160`）的做法；原版 `delbuddy` 輸入框有沒有做名稱檢查沒查。
  - 送出後頁面不關，跟 588 一樣；原版什麼時候關窗沒查。
  - 589 送出後不在本機先移除名單（`/delbuddy` 也是這樣）；只有 FriendUI Delete 會先在本機移除（FU9§9.2 `sub_B18F10`）。
  - HyperAct `buddy` 送出時，如果名單現在不能存取（不在世界、傳送中、上一包還沒送完），就什麼都不做，也不送 C125。原版在這些情況的行為沒查。
- **真機**：`docs/testing/smoke-m7.md` SM34 寫「頁上的 `buddy`／`delbuddy` 現在仍送 C125，R11-C 前不算不通過」。本 PR 合併後，SM34 應改成：`buddy`／`refreshbuddy` → C4 並開 buddy 頁；`delbuddy` → 589 框 → C202＋C4。我沒有改 smoke 文件（不在可改範圍）。

## 產出路徑

- `engine/source/Buddies.cs`、`engine/source/NpcInteraction.cs`、`engine/source/BuddyHyperActTests.cs`
- 本報告 `docs/reports/issue-959.md`
- 分支 `task/959`

## 給下一棒的提示

1. gap-matrix 的 3:C_ADD_BUDDY(138) 在 CI 綠、PR 合併後可以改成「已有」（K5 少 1 列）。`NpcInteraction.cs:160`、`:181` 現在呼叫的是 `BuddyHyperAction`（在 `Buddies.cs`）。
2. 請更新 smoke-m7 SM34 的預期（見「未驗證什麼」最後一點）。
3. 舊式封鎖輸入框 584（FU9 推測是 HyperAct `exclude`，名稱 > 12 字印 435，否則送 `cs` 7）沒做；3.81 沒有 C7 封鎖，要做的話得先裁定對應到 C171。
4. 本機編譯遇到 WebView2 NETSDK1130 時，可以用 `scratch/t959/nowinmd.targets`。它參照的是 `scratch/wt/task-922/scratch/t922/packages` 裡的 DLL；使用者快取的 `lib_manual` 只有 xml。
