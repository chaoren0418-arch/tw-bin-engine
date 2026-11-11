# Issue #917 — T402：查證原版 Q8（StatInfo 數值與第七輪子項）

階段 resource，第 1 次嘗試。分支 `task/917`。

## 做了什麼

- 用 IDA 7.7 唯讀分析 `TW1608262200.bin`（照 #417）：IDB 是 P01 的副本，放在 `scratch/t917/ida/`。跑了 23 批查詢，查詢清單在 `docs/reference/rea-scripts/t917/q1`–`q23.txt`。
- 新文件 [`docs/reference/followups-15.md`](../reference/followups-15.md)（FU15）：
  - **Q8-1**：StatInfoUI 22 列的值是伺服器 protobuf 回覆的字串。客戶端送 C157 子 id 484 要求，收擴充封包 0x79 → `sub_EBA270` 填表。`sub_EB85F0` 只算換色。3.81 沒有 protobuf，所以改 **K1**。
  - **Q8-3**：12 個子項都查到依據。能轉 code 的 6 項分成 Q8-A／B／C 三組（FU15§3）。不必改的有 A04A20、`0x15442E4`、StatInfo、區域表；HTML 標記用量歸 Q9。
- 回填 `gap-matrix.md`：
  - §4 StatInfoUI 列；
  - 15.12.2 的 10 列 Q8 子項加 FU15 指標；
  - 15.12.5、15.12.6 的 Q8；
  - 15.13.4 分類：StatInfoUI 從 K7 改 K1，K7 清空；
  - 15.13.5–15.13.7 的計數、R11-G、Q9-2–Q9-5、結案條件 3 與結案摘要（235 已有／55 K1）。
- 其他文件：
  - 更正 `actions-and-names.md` 第 156、309 行：`0x15442E4` 是顯示旗標，不是稱號文字物件。
  - `INDEX.md` 加 FU15。
- 新腳本 `rea-scripts/t917/html_tag_census.py`：輸入是 PakViewer `extract-all` 解出的 Text pak，統計 HTML 標記用量。

## 怎麼驗證

- BIN SHA-256 `e12949cf…641a2`、IDB 副本 `3a0bf206…7e82f`，都重算過，和 FU5–FU14 相同。每次跑 IDA 前都檢查 `V:` 沒被 subst 占用。
- 計數：
  - `py -3 -I docs/reference/rea-scripts/gap_count_r8.py docs/reference/gap-matrix.md` 結果是已有 228／部分 21／缺 41，K1 55、K5 7，沒有錯誤；
  - `gap_count_r6.py`、`gap_count_r7.py` 仍分別得到 155／94／41、213／36／41，歷史數字沒變。
- PakViewer 查遍本機 `0-客戶端` 24 個 idx：`StatInfo` 都是 0 筆。kor25、ly、t89 的 `Tile.idx` 有 `StatInfoUI.xml`。本機與參考客戶端共 5 個 Text pak 都解出來統計（FU15§0、§2.12）。

## 未驗證什麼

- **推測**：
  - protobuf id 0x79 是依登記表的程式順序配對；
  - 委任鈕＝`sub_9568C0`；
  - 書籤下拉的文字來源；
  - 出價鈕按住連續的速度；
  - `sub_51F5D0` 對備註 `*` 的處理。
- **待驗證**：
  - `sub_7058B0` 元件 0–5（WidgetSlot 模式 0／1 各顯示哪個窗）；
  - 書籤窗 72 byte 紀錄的填表者；
  - agsel 交給 `CAgitAuctionUI` 的部分；
  - 3.81 是否送 S64 0x48。
- 只分析 TW16，沒有對 3.81 `LiTo.bin` 快照逐項核對。沒有建置或執行程式，沒有啟動遊戲、伺服器或 DB，也沒有真機驗收。

## 產出路徑

- `docs/reference/followups-15.md`
- `docs/reference/gap-matrix.md`（回填）
- `docs/reference/actions-and-names.md`（更正）
- `docs/reference/INDEX.md`
- `docs/reference/rea-scripts/t917/`（`q1`–`q23.txt`、`html_tag_census.py`）
- `docs/reports/issue-917.md`
- 本機證據（不進 git）：`scratch/t917/ida/o1`–`o23`、`scratch/t917/text/`、`scratch/t917/statinfo_pak_search.tsv`、`scratch/t917/html_census.tsv`

## 給下一棒的提示

- **M7**：結案條件 3 已達成。剩 R11-A–F 與 #922。
- **可以派的 code 任務**（FU15§3）：
  - **Q8-A**：聊天列提示。ModifyMemo 改成 3325／60 byte，委任前問 4051。
  - **Q8-B**：出價 ±1，可以先做；WidgetSlot 模式要等 Q9-3。
  - **Q8-C**：小項。470 圖示、信件結果印聊天欄、公告板等回應、ESC 順序。
  - 三組都不碰 `WorldSession.cs`、`Program.cs`、註冊表，也不碰 lock。
- **R11-G** 多一項：更正 `StatInfoPanel.cs:55–58` 的註解。
- **Q9 新增 2–5**：建議和 Q9-1 合成一張 resource 任務。
- 字串 4050／4051 不在本機 `string-c.tbl`（3,484 筆）裡；做 Q8-A 時要決定用參考字串表，還是照原版顯示空字串。
