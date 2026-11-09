# Issue #628 T267：查證原版——圖源與字串（Q-2）

## 做了什麼

- 唯讀靜態分析 `TW1608262200.bin`（IDA 7.7＋P01 IDB 的 scratch 副本，19 輪批次），並在記憶體中讀本機 `Sprite.pak`／`Text.pak`（不複製）。
- 產出 [`docs/reference/sprites-and-strings.md`](../reference/sprites-and-strings.md)，每項分「原版怎麼做／3.81 是否支援／R2 組要改什麼」：
  1. **城主王冠**：圖源是 `Sprite.pak` 的 `crown.raw`（封存類別 4＝`Surf`，是 `Sprite.pak` 的檢視），12 格、每格 16×12、RGB555、0 透明，SHA-256 `0e6dc26b…14a7db`。位置 +17／−(40 或 50)−12，`sub_501260`＝gfx 6080／6094；四張偏移表是人物走路內插表（名稱、光照圖也共用），數值已列出。
  2. **盟屋地圖**：`aAvadentelui` 是 IDA 誤命名，真正的表是 `dword_135DA34` = {689, 741, 741, 801, 1482}（區域 4–8，區域 1 同 8）；標記位置 350 筆由靜態初始化 `sub_113F600` 寫入，有資料的筆數 45／11／0／67／6，全部列表；標記閃爍 800／200 ms。
  3. **死亡統計值**：客戶端以「進入世界後第一個 S_STATUS」的基準快照相減（經驗 `%.4f%%`、金幣差、新物品清單筆數）；殺怪數只來自 S250 子碼 61，3.81 不送，恆為 0；地監時間走 S250 子碼 159，與 3.81 `S_PacketBoxMapTimer` 格式相同。
  4. **字串**：1784、1785、2139、3123–3127、3131、1587、3344 都在 `string-c.tbl`（`dword_153C998`）；**mapDesc 查的是 `MapDesc-c.tbl`**（`CMapSystemXML` 自己的表），不是 `string-c.tbl`。
- 更新 `INDEX.md`（新增一列）；回填 `gap-matrix.md` 第 11 節：11.3 的 op 252、C_CHANNEL、`UserPlayStatsUI` 三列依據，11.5 的 R2-D、R2-I、R2-M 目標與依賴（Q-2 解除），11.6 的 Q-2 標為已查、Q-3 加一項（TW16 op 10／9），建議順序的 R2-M。
- 新腳本放 `docs/reference/rea-scripts/t267/`（IDA 資料引用、呼叫點前文、盟屋位置解析、王冠表、`crown.raw` 檢查、字串表對照、q1–q19 查詢清單）。

## 怎麼驗證

- BIN SHA-256 由 `nat.py` 核對；IDB 複製前後 SHA-256 相同；`subst` 開始前與每輪結束後都無輸出（用 `Q:`）。
- 本機資源以 SHA-256 識別（`Sprite.idx`、`Text.idx`、`crown.raw`、`string-c.tbl`、`MapDesc-c.tbl`、`desc-c.tbl`），見文件第 0、1、4 節。
- 盟屋 surf 與王冠／統計窗用到的 surf 在本機 `Sprite*.idx` 都有 `.img`。
- `gap_count_r2.py` 重算統計不變（67／175／48）。
- `rea-scripts/t267/` 的四支 Python 腳本都重跑過，輸出與文件一致。

## 未驗證什麼

- `sub_A42C30`（他人王冠可見條件）、`sub_543810`（盟屋地圖 674 鈕）、新物品清單的加入／移除規則、`sub_88EA30` 格式沒有反編譯。
- 盟屋區域與 3.81 盟屋編號的對照是推測（`house` 表在 DB，依規定不查）。
- TW16 op 10 子碼 9 在 3.81 的對應沒查。
- 沒有建置、沒有執行、沒有真機驗收。

## 產出路徑

- `docs/reference/sprites-and-strings.md`
- `docs/reference/INDEX.md`、`docs/reference/gap-matrix.md`（第 11 節）
- `docs/reference/rea-scripts/t267/`
- 分支 `task/628`

## 給下一棒的提示

- R2-I、R2-M、#550 的 Q-2 依賴都解除，可以直接派；R2-D 原本只依賴 Q-2，現在也沒有依賴。
- R2-M 最容易出錯的地方：**不要拿 mapDesc 去查 `string-c.tbl`**（同編號是不相干的訊息），要另讀 `Text.pak` 的 `MapDesc-c.tbl`。
- R2-I 畫王冠時，走路內插要和人物身體用同一個位移；如果引擎沒有逐格內插，建議另開任務把四張表做成共用資料。
- R2-D 的殺怪數在 3.81 恆為 0，不要自己數擊殺；地監時間（S250／159）可交給 R2-D 或 R2-L，二選一避免重複。
- 待驗證的 TW16 op 10／9 已加到 Q-3。
