# Issue #845 — T361：R9-A 書籤窗原版操作

依據：`docs/reference/followups-9.md` §1（Q7-1）、§16.2 R9-A；`gap-matrix.md` §15.11。分支 `task/845`。

## 做了什麼

`engine/source/NativeUi/BookmarkPanel.cs`（原生書籤窗 `BookmarkWindowPanel`）照 FU9§1 改成原版操作：

| 項目 | 原本 | 現在 | 依據 |
|---|---|---|---|
| 右鍵刪除 | 右鍵列＝選取＋846 | **拿掉**（右鍵不選取、不刪） | FU9§1.1-1：`sub_BDE250` 只處理左鍵 |
| Delete | 單筆 | **多選**：Ctrl 切換、Shift 範圍，最多 12 筆；846「是」後依畫面位置**由大到小**一筆一個 C3 | FU9§1.1-1（修飾鍵對應是推測） |
| Insert | 窗內命名新書籤（引擎自訂） | **加進快速移動清單**（`QuickMove_List` (23,62)，5 列，選取圖 4345，標題 4314）；最多 5 筆，第 6 筆顯示 2828；本次登入有效（換連線或換角色清空，3.81 不能保存）；清單上按 Delete 只移出、不送封包 | FU9§1.1-5、§1.2 |
| 窗內新增 | Insert 命名 | 拿掉（原版窗內不能新增；入口是聊天指令，R9-C） | FU9§1.1-2 |
| 排序 | `Ordinal` 含色碼 | 先去掉 `\f?`／`\a?`／`\d?`，再以 CP950 位元組比較（strcmp），同名依 id；**關窗時歸 0** | FU9§1.1-3 |
| 區域第 0 項 | XML Caption「ALL」 | 字串 **878「全部」**；其餘項目維持地圖編號（`sub_F35D90` 區域表未查） | FU9§1.1-4 |
| 確認鈕（一般模式） | 關窗 | **不關窗**、不動作（原版存檔，3.81 沒有東西可存；永遠不 dirty，所以不顯示 2847／3106） | FU9§1.1-6 |
| 選目的地雙擊 | 沒有 | 雙擊記憶清單或快速移動清單的列＝以它傳送（同確認鈕）；焦點在快速移動清單時，確認鈕也用它的選取 | FU9§1.1-5、6 |
| 隨機傳送鈕 | 不畫 | `RandomTeleport_Button` (129,42) 只在選目的地模式畫，按下送 `Teleport(請求, 0)`；**只有宿主宣告 `AcceptsRandomTeleport`＝true 才畫**（見下方「未完成」） | FU9§1.1、§1.2 |

其他：
- `IBookmarkWindowHost` 加預設成員 `AcceptsRandomTeleport=>false`；`IBookmarkEditHost` 加預設方法 `DeleteMany`（逐筆呼叫 `Delete`）。既有宿主（`RingWindows.cs`）不用改。
- `BookmarkUi.cs`：`BookmarkEditHost.DeleteMany` 依序送 C3——`SendBookmark` 要求 view 是最新 revision，所以每送完一筆重取快照；連線／角色換了、角色在走或任何一筆被拒就停。WinForms 分頁（`--classic-ui` 入口）不變，只更新註解。
- 字串原文（本機 `0-客戶端\Text.pak` 的 `string-c.tbl`，用 `docs/reference/rea-scripts/t680/t680_strings.py` 唯讀查詢，表 SHA-256 `4ebbc1a7a002a4c1dcdfa73dbf056b0e00648e0789763410c40e5079a22d43e9`）：878「全部」、2828「快速移動目錄最多只能登錄5個為止。」、2847「座標記憶結束」、3106「只能在全部的項目中才可儲存記憶。」、487「* 隨機座標 *」、846「要刪除所選擇的項目？（y/N）」。執行期讀 `ServerMessageText.Table`；表不存在時用前述原文。
- 版面節點（`QuickMove_List`、`RandomTeleport_Button`）對照本機先前解出的 `BookmarkUI.xml`（`scratch/task448-uixml/`，沒有進 git）。程式在執行期讀 Tile.pak，缺節點或缺圖只是不畫那一件。本任務沒有宣稱任何資源「沒有」。

## 測試

- 新檔 `engine/source/BookmarkWindowOriginalTests.cs`（群組 `bookmark-window-original`，自動發現）：字串、沒有右鍵刪除、Ctrl／Shift 多選與 12 筆上限、846 後的送出順序、預設 `DeleteMany`、選目的地只能單選、快速移動清單（加入、重複、5 筆上限＋2828、移出、書籤刪掉後消失、同角色保留、換角色或連線清空、版面沒有清單時不動作）、去色碼排序與關窗歸 0、878、確認鈕不關窗、雙擊傳送（記憶清單、快速移動清單、送出中不送）、隨機傳送鈕（模式、宿主、版面三個條件與 id 0）。
- **已調整的既有測試**（行為改了，舊斷言必然失敗；不在原列的可改檔案內，但不改的話「既有測試綠」做不到）：
  - `BookmarkPanelTests.cs`：「一般模式確認鈕關窗」改成「不關窗」，下一步先手動關窗。
  - `BookmarkPanelFollowupTests.cs`：右鍵刪除改成「右鍵不選取、不刪」；Insert 命名那一整段改成「Insert 不命名、不送 C165」；區域標籤從「ALL」改成「全部」；拿掉「關窗清掉命名」。

## 怎麼驗證

- 已驗證：`dotnet build engine/source/OfflineClient.csproj -c Release -r win-x64 -p:EnableWindowsTargeting=true -p:SelfContained=false`（同 CI 參數，NuGet 快取放 `scratch/t845/`）**0 錯誤**，改到的檔案沒有新警告。
- 已驗證：`py -3 engine/tools/check-no-game-assets.py` → `pass`。
- 待驗證：離線測試（`bookmark-window-original`、`bookmark-window-panel`、`bookmark-window-followup` 及全部既有群組）依本機規定**沒有在本機執行**，交給 PR 的 CI（`--module-tests`）。
- 註：本機 `rebuild.py --build` 在 product-restore 失敗（離線 vendor 缺套件，和本任務無關），所以改用上面的 CI 式建置。

## 未驗證／未完成

- **隨機傳送實際送不出去**：`AbilityStatus.TeleportDestinationTarget` 拒絕 `bookmarkId==0`，`RingWindows.cs` 的 `BookmarkWindowHost` 也沒宣告 `AcceptsRandomTeleport`，所以正式執行時這顆鈕**不會畫**（規則 5）。這兩個檔案不在本任務範圍。要開通需要：`TeleportDestinationTarget` 接受 id 0（地圖用什麼值要先查 3.81 `TELEPORT.java:36–58` 對 C6 HD 的讀法）、`BookmarkWindowHost` 加 `public bool AcceptsRandomTeleport=>true`。
- 區域下拉的區域名稱表（`sub_F35D90`）未查，非 0 項仍是地圖編號。
- 多選的修飾鍵（Shift＝範圍、Ctrl＝切換）是推測；快速移動清單上 Delete 不經 846 是推測。
- 從記憶清單拖到快速移動清單、`qbookmark`（收到新書籤自動加入）沒做。
- 2828 的畫法（原版畫在哪裡）沒追，畫在調色盤那一列（引擎自訂）。
- 真機：多選刪除連送數個 C3 的伺服器回應、雙擊傳送、快速移動清單的手感，需要實機確認（交給 Codex／dot）。

## 產出路徑

- `engine/source/NativeUi/BookmarkPanel.cs`、`engine/source/BookmarkUi.cs`
- `engine/source/BookmarkWindowOriginalTests.cs`（新）
- `engine/source/BookmarkPanelTests.cs`、`engine/source/BookmarkPanelFollowupTests.cs`（配合行為調整）
- 本報告 `docs/reports/issue-845.md`

## 給下一棒的提示

- 開隨機傳送：改 `AbilityStatus.cs:108`（`bookmarkId==0` 的分支）與 `RingWindows.cs` 的 `BookmarkWindowHost`（加 `AcceptsRandomTeleport=>true`）；面板不用再改，測試第 8 段已涵蓋面板端。
- R9-C（聊天指令 `bookmark`／`qbookmark`）：新增仍走 `IBookmarkEditHost.Add`；`qbookmark` 要把新書籤加進快速移動清單，可在面板加一個 internal 入口呼叫 `AddQuick` 的同一套規則（5 筆上限＋2828）。
- 區域表查到後，只要改 `ZoneItems`／`ZoneItemText` 與篩選條件（目前用 `MapId`）。
