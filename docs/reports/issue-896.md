# Issue #896 T384：查證原版——3.81 倉庫數量輸入（1:127）與 3.81 端未決問題

## 做了什麼
- 把 `LiTo.bin`（SHA `ce6ee68a…0292`，Themida）複製到 `scratch/t896/`。靜態脫 Themida 不可行，記憶體傾印需要執行程式（本機禁止），所以改用專案既有的 OEP 快照 `LiTo.unpacked-oep-current.exe`（SHA `4271d92f…ab6e9`，同 `rea-inventory.md`），在 `scratch/` 用 IDA 7.7＋Capstone 分析。
- 找到 3.81 客戶端的封包名稱表（263 筆）、伺服器包分派表、送包函式、字串表物件，記在 FU12§0.4，之後查 3.81 可以直接用。
- 1:127：3.81 倉庫存入／取出是舊式清單窗，每列「▼ 數量 ▲」從 0 開始；▼ 在 0 跳到全部；選取列後可打數字、Backspace、數字鍵盤 ±；沒有聊天列數量提示；只送數量 > 0 的列；取出每種 30 金幣（FU12§1）。
- FU9§17：`ADENA_EXCHANGE_WINDOW`／寶箱鈕——3.81 不讀任何 UI XML，兩者在 3.81 都不存在；個人商店 0xFF＝切兩行，頭上每 5 秒輪流顯示；3.81 交易的堆疊數量走 string 140，與 TW16 相同（FU12§2）。
- 回填 `gap-matrix.md` 15.11：1:127 K2→K5（K2 歸零）、M36／M38 移除。

## 怎麼驗證
- 每個結論都附 VA（快照 FO＝VA − `0x400000`）；字串編號用本機 `string-c.tbl`／`desc-c.tbl` 核對內容（desc 9「揮舞」、117「使用中」、string 140）。
- 封包欄位對照 `S_RetrieveList.java`、`C_Result.java`（唯讀）。
- subst 只用 `Q:`，開始前與結束後 `subst` 都無輸出。

## 未驗證什麼
- 沒有真機；OEP 快照是舊專案做的，無法逐 byte 證明來自這份 `LiTo.bin`。
- 客戶端內部封包編號 ↔ 伺服器編號的對應沒查。
- 「開過商店後清單上限 1000 殘值」、`byte_62C9C9` 的意義、寫死背包窗有沒有寶箱類按鈕：推測或未查（FU12§1.3、§2.2、§4）。

## 產出路徑
- `docs/reference/followups-12.md`（FU12）
- `docs/reference/gap-matrix.md`（15.11 的 1:127 列、K2 計數、M36、M38）
- `docs/reference/rea-scripts/t896/`（`ida_t896.py`、`run_t896.ps1`、`t896_ltstrings.py`）
- 快照副本、IDB、查詢結果在 `scratch/t896/`（不進 git）

## 給下一棒的提示
- code：照 FU12§1.5 改 `engine/source/NativeUi/WarehouseWindowPanel.cs`（列內 ▼▲、預設 0、鍵盤輸入、取出費 30×種類數），排在 R7-H 原生窗之後。
- code：`PrivateShop.cs:116–119` 改成 `訊息1 0xFF 訊息2 0x00`，顯示端照 FU12§2.3 每 5 秒輪流（FU9 R9-J）。
- 3.81 的舊式窗都是寫死在程式裡；要取版面數值，從 FU12§0.4 的分派表與窗建構子查，不要再假設讀 `uiextra86`／`uimagicitem` XML（更正 `windows-2.md`:67、`followups-4.md`:66 的推測）。
