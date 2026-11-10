# Issue #742 報告：T315 查證原版 Q5-1–Q5-5，並複核 K6 的 19 列

階段 resource，第 1 次嘗試。分支 `task/742`。

## 做了什麼

- 用 IDA 7.7 唯讀批次查 `TW1608262200.bin`，共 19 輪（q1–q19）。IDB 是 P01 IDB 在 `scratch/rea/ida/` 的副本，SHA-256 `3a0bf206…7e82f`；BIN 的 SHA-256 是 `e12949cf…41a2`。
- Q5-1–Q5-5 全部有結論，寫在 [`docs/reference/followups-7.md`](../reference/followups-7.md)（FU7）第 1–5 節。
  - **Q5-1**：
    - 交易旗標 `byte_136E99D` 是「接受交易詢問」選項，預設 1；旗標 0 時自動回覆拒絕。
    - C11 中間 byte＝S136 的 `c` 欄，對 3.81 一定是 0，和引擎相同。
    - C_BOOK／C_SAVE 原版從不送：底層送出函式只有 2 個呼叫者。
    - 頻道開關的語法是 `/whisper`、`/悄悄話`、`/receive`、`/tradechat` 加 on/off（或 開/關），不是 `/on 頻道`。
    - 刪除物品的確認是聊天列 Y/N，字串 3122；堆疊物品先問數量。
  - **Q5-2**：TW16 的舊式背包、魔法窗用自己的 surf，不讀 3.81 XML 的分頁鈕。職業技能在 TW16 是獨立的鈕 1。3.81 的分類從 TW16 讀不出來。
  - **Q5-3**：料理窗編號 ≥48 的食譜整筆排除、等級 L 顯示前 8×L 筆。清單是 4 欄圖示格加提示框。量表是製作倒數條，不是「等級／11」。
  - **Q5-4**：6 個舊素材 gfx 都是 kind 0，167 是 kind 4，現行分路正確。10147／10145 的疊層深度是每格推進時讀當下的全域值，和引擎的推測相反。
  - **Q5-5**：`BookmarkMemoryWindowUI` 由 S_EVENT **142** 開，不是 141。141 是書籤上限 +10。3.81 這兩個都不送。
- K6 的 19 列逐列複核（FU7§6）。由 1 個唯讀子代理整理；Q6-3 的指令字串我用本輪的 IDA 結果直接查掉，並抽查了幾處 `檔案:行`。
- 在 `gap-matrix.md` 新增 **14.10 節**：Q5 結論、分類變更表、K2／K6 統計、更正。14.5 的表沒有改，所以 `gap_count_r5.py` 的計數不變。
- `INDEX.md` 加 FU7 一列。腳本與查詢放在 `docs/reference/rea-scripts/t742/`。

## K6 複核後的去向

| 去向 | 列數 | 列 |
|---|---:|---|
| 已有（待真機） | 5 | 2:107、3:C_VERSION(122)、3:C_ACTION(50)、5:`sub_A51920`、5:（表情選單 |
| K2 | 5 | 1:181、3:C_DUEL(118)、4:BehaviourUI、5:`sub_87C890`、5:`sub_8E66D0` |
| K5 | 7 | 1:36、1:86、1:29、3:C_WHO(174)、5:`sub_F0E9B0`、5:`sub_5001C0`、5:`byte_13CE51E` |
| K3 | 1 | 4:WidgetSlot |
| K1 | 1 | 4:PopUpUI |

Q5 影響的 8 列 K2 改歸：
- 已有 3 列：C_DIALOG、C_BOOKMARK、C_SAVEIO；
- K5 4 列：C_ASK_XCHG、C_ONOFF、C_DROP、109 kind；
- K1 1 列：`BookmarkMemoryWindowUI`。

K2 由 18 列變成 15 列。

## 怎麼驗證

- 每一個結論都附 VA／FO，以及反編譯或反組譯的出處。
  - 字串用本機 `string-c.tbl`（3484 筆）在記憶體解碼查；
  - 109 kind 用本機 `list.spz` 查；
  - 3.81 端讀伺服器原始碼 `S_PacketBox.java`。
- 每輪 IDA 前後都跑 `subst`：
  - 開始時沒有任何代號被占用；
  - 第 2、3 輪因為我在指令後接了 `Select -First 1`，`W:` 殘留（指向本 worktree 的 `scratch/rea`，沒有 idat 在跑），已手動 `subst W: /D`；
  - 最後 `subst` 沒有輸出。
- `py -3 -I docs/reference/rea-scripts/gap_count_r5.py docs/reference/gap-matrix.md`：計數不變（111／135／44；K1–K6＝48／18／18／41／35／19），沒有錯誤。
- 只改文件，沒有改產品程式。CI 看 PR 結果。

## 未驗證什麼

- Q5-2 的 3.81 分頁分類（背包任務分頁、倉庫四分頁、職業技能鈕）：TW16 不讀那些鈕，`LiTo.bin` 有殼。要真機看 3.81 客戶端。
- 字串 4114、4115、6392、6394 本機沒有。「3.81 不送 6392」是推測。
- 刪除確認的物品條件（`sub_996B10`）、料理清單的欄距／列距與點擊事件：沒有逐一追，列為 Q6-9。
- K6 表的「原版」欄引用既有文件，大部分 `檔案:行` 由子代理整理，我只抽查。
- 「已有」都還沒有真機看過。原生 UI 預設開啟（#751）之後還沒做 smoke（T201／#475）。

## 產出路徑

- `docs/reference/followups-7.md`（新）
- `docs/reference/gap-matrix.md`（新增 14.10 節）
- `docs/reference/INDEX.md`（加 1 列）
- `docs/reference/rea-scripts/t742/`：`ida_t742.py`、`q1.txt`–`q19.txt`
- `docs/reports/issue-742.md`（本檔）

## 給下一棒的提示

- **code 任務**（FU7§8）：
  - R6-C 私商 `TRADE_WINDOW` 13／14、R6-D 鍵盤拾取與幽靈快捷、R6-E 聊天指令與本地確認，三組可以馬上派；
  - R6-A 封包特效與地圖單次音效、R6-B 自己的名稱位置、R6-F 料理窗與地裂深度，範圍小，可以合併。
- **resource 任務**：Q6-1、Q6-2、Q6-4–Q6-9 合成 1 張（Q6-3 已完成）。`SlotWidgetUI.xml` 交給 P-A（#757）的參考客戶端 XML 來源順便查。
- **下一輪盤點**：
  - #751 已把原生 UI 改成預設開啟，14.1 的判定規則與 K4 的 41 列要重判。
  - 把 14.10 的分類變更套到 14.5，再更新 `gap_count_r5.py`（或寫 r6 版）。
- **IDA 小提醒**：跑 `run_ida.ps1` 時不要接 `Select -First`，否則 `finally` 不會跑，subst 會殘留。輸出改用 `| Out-Null`。
