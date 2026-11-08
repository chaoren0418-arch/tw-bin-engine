# #601 T244：gap-matrix 第二輪

分支：`task/601`。日期：2026-11-09。依據 main `8a35d710`（已含 #527）。

## 做了什麼
- 在 `docs/reference/gap-matrix.md` 新增第 11 節「第二輪盤點」。第 1–10 節沒有改動。內容：
  - 11.2：本輪新發現。
  - 11.3：76 列的狀態變更表，每列都寫了任務、剩下的差異，以及 `檔案:行`。
  - 11.4：重算後的統計。
  - 11.5：下一批 code 分組 R2-A–N，共 14 組。
  - 11.6：需先查證的 Q-1–Q-4。
- 新增 `docs/reference/rea-scripts/gap_count_r2.py`：以第 1–5 節的原表為底，套用 11.3 的變更後重算統計。每個變更列都必須唯一對應到原表的一列，而且「原狀態」要和原表相符，否則腳本會報錯。
- `docs/reference/INDEX.md`：更新 gap-matrix 那一列。

**新統計**（290 列；原本 289 列，新增 1 列背景音樂）

| | 已有 | 部分 | 缺 |
|---|---:|---:|---:|
| 本輪 | 67 | 176 | 47 |
| 原本 | 58 | 165 | 66 |

變動：「缺→部分」18 列、「部分→已有」9 列。

## 怎麼驗證
- 找出基準 `003a5a34` 之後合併的 PR，加上 T202 誤標「審查中」、其實早已合併的任務，共約 50 張。逐張讀報告，再用 Grep 抽查 `engine/source`。這部分分成 3 個唯讀子代理平行做，重點結論由我自己 Grep 複核，例如 `PaintAmbience`、`ClientSoundScene.Configure` 在產品程式裡沒有呼叫點，以及 S250 的 `WorldReceive` 註冊。
- 執行 `py -3 -I docs/reference/rea-scripts/gap_count_r2.py docs/reference/gap-matrix.md`：套用 76 列變更，沒有錯誤。
- 執行 `gap_count.py` 重算原表：仍是 58／165／66，第 1–5 節確實沒動到。

## 沒有驗證的部分
- 沒有建置或執行程式，也沒有真機測試。「產品沒有呼叫」是用 Grep 判斷的，透過反射或事件間接呼叫的地方可能漏掉。
- 本輪沒有新的反組譯，所以沒有開 IDB，也沒有用 subst。BIN 地址沿用第 1–5 節。
- 舊第 6 節的子分類（需先查證 3、可直接補 25）和 `gap_count.py` 對原表重算的結果（2／26）差 1 列。這是之前就有的差異，本輪沒有追查。

## 產出
- `docs/reference/gap-matrix.md`（第 11 節）
- `docs/reference/rea-scripts/gap_count_r2.py`
- `docs/reference/INDEX.md`

## 給下一棒的提示
- 本任務是 resource 階段，所以沒有寫 `docs/plans/issue-601.json`。請主控依 11.5 開 code 任務，依 11.6 開 3 張 resource 任務。
- 可以先派、不必等其他任務的組：R2-A（畫面接上日夜、光照、天氣）、R2-B（聲音接上產品）、R2-C、R2-G、R2-I、R2-N。前兩組是「程式已寫好但產品沒接上」，補上後在畫面上和聲音上的差別最明顯。
- 原生 UI 預設是關閉的。要不要改成預設開啟，建議等 #475 真機 smoke 通過後由使用者決定。
- #602（T245）也會改 `gap-matrix.md` 和 `INDEX.md`。後合併的那一方要自己同步：本文第 11 節是新加的段落，衝突應該只會出現在 INDEX 那一列。
