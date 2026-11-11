# Issue #952 — T412：gap-matrix 第八輪與 M7 結案判定

階段：resource，第 1 次嘗試；分支 `task/952`；依 main `2ee155ab`。

## 結論
- **M7 還不能結案。** 擋住的有 7 列 K5（都能離線做）、1 列 K7（4:StatInfoUI，等 #917），以及 5:`sub_87C890`（要等 #922 合併）。
- 290 列：**已有 228**（其中 1 列是 #922 的預期結果）、**K1 54**、**K2 0**、**K5 7**、**K7 進行中 1**。
- 和第七輪（213／K1 54／K2 3／K5 20）相比：「部分→已有」15 列。

## 做了什麼
- `docs/reference/gap-matrix.md` 新增 **15.13**，內容有：
  - 方法；
  - 15 列的狀態變更表；
  - 統計、分類（取代 15.12.4）；
  - K5／K7 逐列說明（還缺什麼、能不能分組）；
  - R11-A–G 分組與 Q9-1；
  - M7 判定，附結案條件與結案摘要草稿；
  - 真機變動與限制。
- 三張還在跑的任務照目標算進預期：
  - Q8 #917：4:StatInfoUI 標 K7 進行中。它是唯讀查證，查完只會轉 K5 或 K1。
  - R10-R #922：5:`sub_87C890` 記「已有（進行中 #922）」。
  - T411 #948：盤點中已合併（PR #951），不對應任何列。
- 新增計數腳本 `docs/reference/rea-scripts/gap_count_r8.py`。`gap_count_r7.py` 改成讀到 15.13 前就停，仍能重現第七輪的數字。
- `docs/testing/smoke-m7.md`：
  - 新增 SM57 選項窗、SM58 stride／altdir（#922 合併後測），合計 58 項：🔴 47、🟢 11；
  - SM30、SM52 的預期改成依 #922 是否合併判斷；
  - §13 替 7 個既有項目補了步驟。
  - 沒動 §0.3 和 §11 K-1／K-2（PR #951 已改成一輪；本分支已同步 main `c73f8100`，無衝突）。

## 怎麼驗證
- 已驗證（靜態）：第七輪的 20 列 K5 和 3 列 K2，由 2 個唯讀子代理對照任務報告與產品碼逐列核對。仍判 K5 的 7 項「沒做」，我自己用 Grep／讀檔複核過：
  - `ActionWindowPanel.cs:133–138`；
  - `NpcInteraction.cs` 只分派 `addbuddy`；
  - `DuelUi.cs:45–51`；
  - `ObjectRendering.cs:287–290`；
  - `RingWindows.cs:221–224`；
  - #913 報告第 144–145 行。
- 已驗證：`py -3 -I docs/reference/rea-scripts/gap_count_r8.py docs/reference/gap-matrix.md` 輸出 228／21／41、K1 54／K5 7／K7 1，和 15.13.3、15.13.4 一致。`gap_count_r7.py` 仍輸出 213／36／41。
- 沒有開 IDA、沒有建置或執行程式、沒有啟動遊戲／伺服器／DB。本輪沒有新的「缺資源」宣稱，所以沒有跑 PakViewer。

## 未驗證什麼
- 第七輪前已是「已有」的 213 列沒有逐列重看。
- 子代理給的 `檔案:行`，除了上面列出的幾處，我沒有逐一重看。
- 5:`sub_87C890` 的「已有」是預期；#922 的實作若和 FU14 不同，要重判。
- 全部「已有」都沒有真機驗收。

## 產出路徑
- `docs/reference/gap-matrix.md` §15.13
- `docs/reference/rea-scripts/gap_count_r8.py`（新）、`gap_count_r7.py`（加一行截斷）
- `docs/testing/smoke-m7.md`（§5、§9、§12 小改，新增 §13）
- `docs/reports/issue-952.md`（本檔）

## 給下一棒的提示
1. **要先裁定**（gap-matrix 15.13.7）：
   - OptionUI 三條音量、SelectUI 選空格、CreateUI 職業鈕雙擊，要不要算「引擎取捨」。算的話阻擋列少 3 列，R11-D、R11-F 不用開。
   - PersonalShopUI 的頭上對話框要不要排進 M7。
2. 可以馬上派：R11-A（組隊選目標游標）、R11-C（好友 HyperAct）、R11-D、R11-G（註解小清理）、Q9-1（頭上對話框版面，resource）；R11-B 的 ①②④。
3. R11-B ③ 排在 R11-A 之後；R11-E 排在 Q9-1 與 #922 之後（`ObjectRendering.cs`）。
4. #917 完成後，依結果開 R11-H（StatInfo 數值）或把 4:StatInfoUI 改 K1；然後重跑 `gap_count_r8.py`，再判一次 M7。
5. 子代理另外發現的過時註解已收進 R11-G：`PrivateShopTradeSource.cs:23`、`:25`、`:132`，`TitleAndEmote.cs:117`。`ActionWindowPanel.cs:125`、`:136–138` 收進 R11-B。
