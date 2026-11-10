# Issue #880 報告：T379 gap-matrix 第七輪與 M7 結案判定

階段 resource，第 1 次嘗試，分支 `task/880`。

**依據**：main `1a1762f0`。這時 R6-B、R7-A–O、R8-A–E、R9-A–Q、T378 都已合併。盤點途中 R10-A #888、R10-B #891 也合併了（main `5d8073c3`），所以它們對應的 4 列直接依合併後的程式判定，沒有標「進行中」。

## 做了什麼
- 在 `docs/reference/gap-matrix.md` 新增 **15.12 節**（舊內容不刪）：
  - **15.12.2 狀態變更**：「部分→已有」58 列，其中 2 列（1:2、1:238）是 15.11 的結果。
  - **15.12.4 分類**：取代 15.5，15.10、15.11 的分類變更都已套用。
  - **15.12.5 逐列說明**：K1 以外，每一列「部分」或「缺」都寫出還缺什麼、對應的檔案、能不能轉成 code 任務。
  - **15.12.6 R10 分組**：R10-C–O 共 13 組，另有 Q8 查證、小清理、真機前置。原則同 15.6，寫出目標、可改的檔案、依賴。
  - **15.12.7 真機測法**：M1–M40 每項補一行「怎麼測」，含入口 `檔案:行` 與 log 關鍵字。本輪不執行。
  - **15.12.8**：M7 判定，以及 2 點需要裁定的事。
- 新增 `docs/reference/rea-scripts/gap_count_r7.py`：套用 R2–R7 的變更，並檢查 15.12 的分類每列只歸一類。
- `gap_count_r6.py` 改成讀到 15.12 之前就停，第六輪的數字照舊能重現。
- 更新 `docs/reference/INDEX.md` 的 gap-matrix 列。

## 290 列的去向
| 去向 | 列數 |
|---|---:|
| 已有 | 213（第六輪 155，+58） |
| K1 伺服器不支援 | 54 |
| K2 需先查證 | 3（只有 1:127 要等真機） |
| K5 仍有 code 差異 | 20 |

依狀態算：已有 213、部分 36、缺 41。

## M7 判定：現在還不能結案
擋住結案的有 22 列：
- **K5 20 列**：都能離線寫程式，已分到 R10-C–O。
- **K2 2 列**：4:StatInfoUI、5:`sub_87C890`，都能離線查（Q8-1、Q8-2）。

這些做完以後，非「已有」只剩 K1、1:127（M36）和 `cloth`（M31），就符合結案標準。

## 怎麼驗證
- **逐列核對**：4 個唯讀子代理分組進行，先讀各任務報告的「未做」，再 Grep 產品碼的呼叫點。
- **主控複核**：6 項「產品沒有呼叫者」的說法，我另外 Grep 確認過：`AnswerPrivateShopConfirm`、`PickPartyInviteTarget`、`IUiActionCommands`、`ListSprLayers`、`FriendPanel.Host`、`WarnRatio`。
- **#888、#891 的 4 列**：合併後我自己讀過程式：`HyperTextLayout.cs:53` 是 `Order=Bgr`；`FishObtainTable` 在 `Inventory.cs:65`、`FishObtainPanel.cs:171` 有呼叫。
- **計數**：執行 `py -3 -I docs/reference/rea-scripts/gap_count_r7.py docs/reference/gap-matrix.md`，R2–R7 依序套用 88／87／85／47／47／58 列，沒有錯誤，也沒有漏分類。`gap_count_r6.py` 仍輸出 155／94／41。
- **沒做的事**：沒開 IDA，沒建置或執行任何程式，沒啟動遊戲、伺服器或 DB。
- **PakViewer**：本輪沒有新的「缺資源」宣稱，所以沒有新查。引用的缺圖都是 T318、FU8、FU9、#762 查過的。

## 未驗證什麼
- 所有「已有」都沒有真機驗收；M1–M40 都還沒執行。
- 「沒有產品呼叫者」是用 Grep 判斷的，經由反射或委派的間接呼叫可能漏掉。
- 子代理給的 `檔案:行`，除了上面複核的 6 項，沒有逐一重看。
- 第六輪前就是「已有」的 155 列，沒有逐列重看。
- #888、#891 改到 `Inventory.cs`、`FishingPanel.cs`、`HyperTextPanel.cs`，這幾個檔案的行號可能差幾行。

## 產出路徑
- `docs/reference/gap-matrix.md` 15.12 節
- `docs/reference/rea-scripts/gap_count_r7.py`（新）、`gap_count_r6.py`（只改切段）
- `docs/reference/INDEX.md`
- 本報告

## 給下一棒的提示
- **可以馬上派**：R10-C、D、F、G、H、K、L、M、N、O；另有真機前置：把 `[native-ui]` 的 log 寫進 `--evidence`。不做的話，smoke 只能用 DebugView 看 log。
- **有先後**：
  - R10-E 在 R10-D 之後，而且要先裁定 S48 的顯示方式；
  - R10-I 在 R10-H 之後；
  - R10-J 在 R10-F、R10-I 之後。
- **Q8**：StatInfo 數值、list.spr 的 `altdir`／`stride`，再加 12 個子項，建議開 1 張 resource 任務。
- **需要使用者裁定**：
  1. 1:52、CreateUI、ExchangeUI、SelectUI 的小差異，要做，還是算「引擎取捨」；
  2. S48 要用 TW16 的模態框，還是 kor25 的 `Notice_Window`。
- **真機 smoke**（#475）照 15.12.7 的 M1–M40 做。
- 本任務是 resource 階段，沒有寫 `docs/plans/issue-880.json`。R10 各組請由主控依 15.12.6 開票。
