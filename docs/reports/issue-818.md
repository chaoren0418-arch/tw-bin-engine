# Issue #818 報告：T355 查證原版——Q7-1 到 Q7-14

階段 resource，第 1 次嘗試。分支 `task/818`。

## 做了什麼

- 用 IDA 7.7 唯讀批次分析 `TW1608262200.bin`（SHA-256 `e12949cf…1a2`），IDB 是 P01 IDB 在 `scratch/` 的副本（`3a0bf206…e82f`）。
  - 14 項分成 4 組平行查，每組一份 IDB 副本、一個 subst 代號（X:／Y:／V:／T:），合計 39 輪。
  - 第一次派出的 4 組在 23:42 左右中斷（沒有產出草稿）。我解除殘留的 V:，把 C 組的 IDB 換回乾淨副本，再派第二次，接著已有的批次往下查。
- 用 PakViewer CLI 唯讀查過 `0-客戶端` 全部 24 個 idx，以及 kor25、t89、ly 的 `Tile`、`Text`、`Sprite*.idx` 和 pmc、c760 的 `Tile.idx`。解出的檔案只放 `scratch/`。
- 對照 3.81 伺服器原始碼（唯讀）與引擎 `engine/source`（唯讀）。
- 結果寫在 [`docs/reference/followups-9.md`](../reference/followups-9.md)（FU9）：每項寫「原版怎麼做／3.81 是否支援／對應要改哪個檔案／去向」，並附建議的 code 分組。
- `gap-matrix.md` 新增 **15.11 節**，內容有：
  - 28 列 Q7 的分類變更；
  - K2 36 列查證後的去向統計；
  - R9-A–Q 的分組順序；
  - 3 項需要裁定的事；
  - 更正與新的真機項目 M34–M40。
- `INDEX.md` 加 FU9 一行；查詢清單放在 `docs/reference/rea-scripts/t818/{a,b,c,d}/q*.txt`。

**K2 36 列的去向**

| 去向 | 列數 | 列 |
|---|---:|---|
| 已有 | 3 | 1:2、1:238、4:UserObtainUI |
| K1 | 2 | 1:132、4:MapUI |
| K5 | 30 | Q7 的 22 列＋Q6 的 8 列（FU8） |
| 仍 K2 | 1 | 1:127：3.81 倉庫的數量輸入，只能靠真機或脫殼的 `LiTo.bin` 確認 |

套用後的分類：K1 54 列、K2 1 列、K5 77 列。

## 怎麼驗證

- 每項結論都附 VA／FO、函式與字串編號，依據是反編譯、反組譯、資源檔或原始碼；並分「已驗證」「推測」「待驗證」。
- 我讀了 4 組的草稿，並抽查了下面幾項，都和草稿一致：
  - `MapR.java:35–36`
  - `C_CreateParty.java` 的 type
  - `SpellTradeSource.cs:15`、`:50`、`:60`
  - `BookmarkPanel.cs:399–402`、`:464–465`
  - `FieldMapPanel.cs:10`、`:59–61`
  - `TransactionPanel.cs:293`、`:298`
- 跑了 `py -3 -I docs/reference/rea-scripts/gap_count_r6.py docs/reference/gap-matrix.md`：能正常執行，計數和 15.4／15.5 相同。這是預期結果，因為 15.5 的表沒有改。
- subst：最後檢查沒有任何代號殘留，也沒有 idat 程序。
- CI 結果以 PR 頁面為準。

## 未驗證什麼

- 3.81 `LiTo.bin` 有殼，下面這幾項查不到，只能真機確認：
  - 倉庫的數量輸入（M36）；
  - `ADENA_EXCHANGE_WINDOW` 誰開；
  - 寶箱鈕的作用；
  - 0xFF 分隔怎麼顯示（M38）；
  - `/exclude 名稱` 是否讀名稱（M37）。
- 其他待驗證項目：
  - HyperText `<font fg>` 的位元組順序與連結色（M34）；
  - 選目標模式下的游標（M35）；
  - 好友窗的開窗鍵；
  - 召喚窗停用格能不能點（M39）；
  - 登入 `Back_Image`（M40）；
  - `Character_Ani` 的動畫表；
  - FishObtainUI 的物品表；
  - 書籤區域表的來源；
  - 舊式存點窗的存檔位置。
- 子代理寫的其餘 `檔案:行` 與地址，我沒有逐一重看（FU9§17）。
- 沒有建置，沒有執行任何程式，也沒有啟動遊戲、伺服器或 DB。

## 產出路徑

- `docs/reference/followups-9.md`（新）
- `docs/reference/gap-matrix.md`（新增 15.11）
- `docs/reference/INDEX.md`（加 1 行）
- `docs/reference/rea-scripts/t818/`（4 組的查詢清單）
- `docs/reports/issue-818.md`（本檔）

## 給下一棒的提示

- **可以馬上派的 code 任務**：
  - R9-D：S55 不跟隨特效；
  - R9-G：學魔法圖示；
  - R9-K：交易數量；
  - R9-L：背包操作。
- **有先後順序的組**：其他組和 R7／R8 共用檔案，順序見 gap-matrix 15.11。
  - R9-B、R9-H、R9-J、R9-M 排在 R7-H 之後；
  - R9-E → R9-F → R9-I 排在 R7-D 之後。
- **併入既有組**：
  - R7-A：`Magic.cs` 的 179 改成字串提示「請選擇對象。」；
  - R7-I：狀態窗 Up＝預覽＋Y/N，「是」才送；
  - R7-C：`byte_13A931C`＝`EnableElfTelControl`，初值 0。
- **需要使用者裁定**：
  1. 選項窗的入口：鈕 6／Ctrl+V 維持 HTML `option1`，還是改開原生窗；
  2. FishObtainUI 的清單內容；
  3. 交易窗的金幣框要不要留。
- **下一輪盤點**：把 15.10、15.11 一起套進 15.5。套用後 K2 只剩 1:127。
- **文件更正**：`protocol-gaps.md:26`、`:172`、`:228` 的 op 132 要改成「3.81 不送」（15.11 已列，本輪沒有改那個檔）。
