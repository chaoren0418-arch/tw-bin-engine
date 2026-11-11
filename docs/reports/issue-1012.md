# Issue #1012 — T441：gap-matrix 第十輪與 M7 結案判定（resource，第 1 次）

依據：gap-matrix 15.14（T427／#980）、FU17（#966）、`classic-ui-audit.md`（#953）。基準 main `f59d6d69`。

## 結論
- **290 列的去向**：已有 **234**、部分 15、缺 41；K1 **55**、K2 0、K5 **1**、K7 0（第九輪是 230／19／41，K1 55、K5 5、K7 1）。
- 「部分→已有」5 列：5:`sub_87C890`（#985）、4:AgitAuctionUI（#986）、4:OptionUI（#987）、1:99（#988）、4.2 `BoardListWindow`（#989）。4:WidgetSlot 從 K7 預期變成實際「已有」（#969）。
- 「已有→部分」1 列：**1:144**。#1009／#1010 把 HTML 變數做完了，但 FU17§3.4 依據的本機選項頁在引擎點不進去：
  - 選單開的 `option1` 的 link 全部停用（`NpcInteraction.cs:86`）；
  - 頁上有查證依據的客戶端動作（accepttrade、getall、bubbleon、showMargetMsg）不執行；
  - 1023／1024 在產品沒有值（`HyperTextOptionVars.HpMp` 沒接上）。
- **M7 判定：還不能結案**。15.14.7 的三個條件都達成了，但 1:144 是新的 K5，能離線補，分成 R13-A（gap-matrix 15.15.6）。R13-A 合併（或使用者把 ①② 裁定為取捨）後就是 235／55，可以結案；結案摘要已定稿在 15.15.7。
- `smoke-m7.md`：classic 盤點那一項和「SM57 選項窗」撞號，改成 **SM59**。最終是 **59 項（🔴 48、🟢 11）**，舊的「58 項」少算一項。

## 做了什麼
- `docs/reference/gap-matrix.md` 新增 **15.15**（15.15.1–15.15.9）：方法、狀態變更表、統計、分類、K5 逐列、R13-A、M7 判定與結案摘要定稿、smoke 變動、限制。
- 新腳本 `docs/reference/rea-scripts/gap_count_r10.py`：預設算第十輪；`--round 9` 讀到 15.15 之前就停、分類讀 15.14，重現第九輪的數字。`gap_count_r9.py` 也改成在 15.15 前停（做法和 #980 改 r8 一樣）。
- `docs/testing/smoke-m7.md`：改號 SM57→SM59、合計改成 59，新增 **§15**：
  - R12 合併後，5 個已知差異改成正向檢查；
  - SM15 改成實際切換，SM54、SM59 照 CU-1–4 改預期；
  - SM24、SM33、SM38 補步驟；
  - R13-A 合併後才測的步驟另外列出。

## 怎麼驗證
- `py -3 -I docs/reference/rea-scripts/gap_count_r10.py docs/reference/gap-matrix.md` → 234／15／41，K1 55、K5 1，列數 290，exit 0。
- 加 `--round 9` → 含預期 230／19／41、不算預期 229／20／41，K1 55、K5 5、K7 1，和 15.14.3 相同。`gap_count_r9.py`、`gap_count_r8.py` 的結果也沒變（r8 仍是 228／21／41）。
- 逐列讀了 #969、#985–#989、#1008–#1011、#991–#994 的報告，也到產品碼核對呼叫點（`檔案:行` 列在 15.15.1）。
- PakViewer CLI 唯讀查本機 `0-客戶端\Text.idx`：`pak search Text.idx option` 找到 76 筆。解出 `option1-c`、`option11-c`、`option12-c`、`option13-c`、`optiong-c.html` 到 `scratch/t1012/pak/`（不進 git），確認 `option1` 用 link 連到 option11–13、`option11` 用到 `#1023`。本輪沒有宣稱缺資源。
- 沒有開 IDA，沒有建置或執行任何程式，沒有啟動遊戲、伺服器或 DB，沒改產品程式。

## 未驗證什麼
- 「沒做」「產品沒有呼叫」是用 Grep 判斷的，經由反射或委派間接呼叫的地方可能漏掉。
- 原版本機頁上，沒有客戶端處理函式的動作會不會送封包，沒有查。R13-A 先推測為「不送」。
- 第九輪以前就是「已有」的列，沒有逐列重看。
- 「已有」都只是靜態核對，全部沒有真機驗收。

## 產出路徑
- `docs/reference/gap-matrix.md`（15.15）
- `docs/reference/rea-scripts/gap_count_r10.py`（新）、`gap_count_r9.py`（一行）
- `docs/testing/smoke-m7.md`（改號、§15）
- `docs/reports/issue-1012.md`（本檔）
- 分支 `task/1012`

## 給下一棒的提示
- **調度器**：照 15.15.6 開 R13-A（code）。可改的檔案只有 `NpcInteraction.cs`（本機頁模式）、`NativeUi/UiClientBridge.cs`（`ToggleMenuLocalPage` 和 `HpMp` 那一行），加兩個新檔。
  - 先問使用者 15.15.7 的裁定：要做 ①②③，還是只做 ③（一行）、把 ①② 當成取捨。
- **R13-A 合併後**：跑 `gap_count_r10.py` 時，1:144 那一列要另外加一段（15.16），或由下一輪腳本處理。照 15.15.7 的摘要更新 PLAN／TASKS，M7 就能結案。
- **真機**：照 `smoke-m7.md` §15 跑 59 項。R13-A 的步驟在 §15.3。
