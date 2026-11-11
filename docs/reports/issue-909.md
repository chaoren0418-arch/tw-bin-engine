# Issue #909 — T394：R10-J 書籤窗收尾（code，第 1 次）

依據：`docs/reference/gap-matrix.md` 15.12.6 的 R10-J 列、15.12.5 的 4:BookmarkWindowUI 列；FU9§1.1 第 5 項。分支 `task/909`。

## 做了什麼

| 項目 | 改動 | 依據（已驗證／推測） |
|---|---|---|
| ① 隨機傳送鈕（id 0） | `AbilityStatus.TeleportDestinationTarget` 接受書籤 id 0，送 C6 `HD(地圖 0, 書籤 0)`；`RingWindows.cs` 的 `BookmarkWindowHost` 加 `AcceptsRandomTeleport=>true`。原生書籤窗在選目的地模式會畫 `RandomTeleport_Button`（面板端 R9-A 已做好） | **已驗證（3.81 原始碼，唯讀）**：`C_UseSkill.java` 對技能 5／69 `readH()` 讀了地圖就丟、`targetId=readD()`；`L1SkillUse` 把它設成 `_bookmarkId`；`TELEPORT.java`／`MASS_TELEPORT.java` 的 `CharBookTable.getBookMark(pc,id)` 查不到時，地圖可傳送就 `randomLocation(200)`，否則回 276。原版假書籤是名稱 487、座標 0、id 0（FU9§1.1）。地圖欄送 0＝照原版假書籤 |
| ② `/qbookmark` 加進快速移動清單 | `PlayerChat.cs`：qbookmark 的 C165 放行時呼叫新的 `ClientForm.OfferQuickBookmark(world, session, actor)`，排進 UI 執行緒後呼叫原生書籤窗的 `RequestQuickOnNextBookmark`。`BookmarkPanel.cs`：旗標設著時，同一連線、同一角色下一次收到單筆書籤（S92）就把新出現的書籤 id 加進快速移動清單，旗標清掉。提示文字拿掉「尚未自動加入」 | **已驗證**（FU9§1.1）：`sub_502720` 設 `this+486＝1`，收到新書籤時加進快速移動清單。**推測**：已滿時顯示 2828（同 Insert）；換連線或換角色就清掉旗標；伺服器拒絕（214，不回 S92）時旗標保留到下一筆新書籤（原版沒看到逾時）；整份清單（S64）不會用掉旗標 |
| ③ 從記憶清單拖到快速移動清單 | `BookmarkPanel.cs`：在記憶清單的列上按左鍵、在 `QuickMove_List` 範圍（120×75）放開＝加入，規則和 Insert 一樣（不重複、最多 5 筆、第 6 筆 2828、已送刪除的不加、846 確認中不拖）。Insert 與 qbookmark 共用新的 `AddQuickId` | **已驗證**（FU9§1.1 第 5 項）：拖曳是加入方式之一。**推測**：放開的位置不影響順序，一律加在最後；兩種模式都可以拖；拖曳時跟著游標的那一列（`CloningMemoryPlace_List`）不畫 |

- 新檔 `engine/source/BookmarkFollowupTests.cs`（自動發現，3 組）：`bookmark-followup-random`、`bookmark-followup-qbookmark`、`bookmark-followup-drag`。全部是合成封包、合成 XML 和單色 surf，不讀客戶端素材。
- 只改了任務列出的檔案：`NativeUi/BookmarkPanel.cs`、`AbilityStatus.cs`（只改 `TeleportDestinationTarget`）、`RingWindows.cs`（只加 `AcceptsRandomTeleport`）、`PlayerChat.cs`（只動 qbookmark：`BookmarkChatCommand` 的 qbookmark 分支與 `OfferQuickBookmark`）。沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。
- `--classic-ui`：沒有原生書籤窗，`OfferQuickBookmark` 什麼都不做，`/qbookmark` 照舊只送 C165；WinForms 書籤分頁沒有快速移動清單，也沒有隨機傳送（行為不變）。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release -r win-x64 -p:SelfContained=false -p:RestoreSources=<主專案 engine/vendor>`，0 錯誤；3 個警告都是既有的（`MagicUi.cs:20`、`HtmlActionTests.cs:108`），不在本次改的檔案裡。（worktree 裡沒有 `engine/vendor`，所以從主專案的離線 vendor 唯讀 restore。）
- `py -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。
- 依本機執行限制，**沒有在本機跑任何測試程式**；單元測試與本組離線測試交給 PR 的 GitHub Actions CI。
- 3.81 伺服器原始碼（`D:\lineage開發\1-伺服器\src`，唯讀）：讀了 `skillmode/TELEPORT.java`、`skillmode/MASS_TELEPORT.java`、`C_UseSkill.java`（技能 5／69 分支）、`L1SkillUse.java`（`_bookmarkId`）。
- 資源：本任務沒有宣稱任何資源「沒有」；版面照舊在執行期從 Tile.pak 讀 `BookmarkUI.xml`，沒有複製任何遊戲資源。

## 未驗證什麼（待驗證）

- 真機：P24Lab 戴傳送控制戒指施放 5／69 → 書籤窗出現隨機傳送鈕 → 按下後伺服器隨機傳送（限制地圖應回 276）；集體傳送 69 的血盟成員 748 詢問。
- 真機：`/qbookmark 名稱` → S92 開窗，新書籤出現在快速移動清單。
- 真機：滑鼠從記憶清單拖到快速移動清單（`UiRoot` 是否在放開時把 Up 交給同一個面板；本機只用離線事件測過）。
- 旗標的競態：`OfferQuickBookmark` 用 `BeginInvoke` 排進 UI 執行緒；理論上要在伺服器回 S92 之前執行（一個 RTT 內），沒有實測。
- 上面標「推測」的行為（2828、清旗標的時機、拖曳的放開位置與模式）沒有對 BIN 逐條查證。

## 產出路徑

- `engine/source/NativeUi/BookmarkPanel.cs`、`engine/source/AbilityStatus.cs`、`engine/source/RingWindows.cs`、`engine/source/PlayerChat.cs`
- `engine/source/BookmarkFollowupTests.cs`（新）
- `docs/reports/issue-909.md`（本檔）

## 給下一棒的提示

- 4:BookmarkWindowUI 剩下：區域表（`sub_F35D90`，Q8）、13 色調色盤與改名類（3.81 不支援）、`CloningMemoryPlace_List` 的拖曳影像。要做拖曳影像，先查 `sub_BDF0B0`（QuickMoveList）與 `CloningMemoryPlace_List` 在 `BookmarkUI.xml` 的節點，再決定放開位置是否影響順序。
- 隨機傳送只在原生書籤窗；WinForms 分頁（`BookmarkUi.cs`）沒有對應的按鈕。
- 15.12.6 的「`InputHelp` 補 `/duel`」排在 R10-F、R10-J 之後，現在可以開工（`PlayerChat.cs:24`）。
- 真機 smoke 時照上面的「待驗證」三條核對；log 關鍵字見 15.12.7。
