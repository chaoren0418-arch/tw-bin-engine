# Issue #532 — T225：查證原版協定空白（resource，第 1 次）

## 做了什麼

- 唯讀分析 `TW1608262200.bin`（SHA `e12949cf…41a2`）：P01 IDB 複製到 `scratch/rea/ida/tw16.idb`（前後雜湊相同 `3a0bf206…e82f`），經 subst `Q:`（事先確認未占用、每次跑完解除）以 IDA 7.7＋Hex-Rays 反編譯約 60 個函式，並列出全部 847 個送包呼叫點。
- 唯讀讀 3.81 伺服器原始碼（`D:\lineage開發\1-伺服器\src`）與引擎 `engine/source`；在記憶體解 `Text.pak` 的 `string-c.tbl` 只取少數字串當證據。
- 產出 [`docs/reference/protocol-gaps.md`](../reference/protocol-gaps.md)；回填 [`gap-matrix.md`](../reference/gap-matrix.md) 第 1–3、5、6、8 節（狀態、依據、統計）；更新 [`INDEX.md`](../reference/INDEX.md)；新增可重跑腳本 `docs/reference/rea-scripts/ida_t225_sends.py`、`t225_cases.py`、`t225_s250.py`、`t225_cmp.py`、`t225_table.py`、`t225_strings.py`。

## 主要結論

1. **S_EXTENDED（19／39／109）不是 S250 的對應**：讀 H message id 交 protobuf 分派器（125 個 id，商城／製作／競技場等）。3.81 伺服器沒有 protobuf → 不建議補。
2. **S250 的真正對應是 S_EVENT（op 108，`sub_9357E0`）**：158 個 case，子碼與 3.81 `S_PacketBox` 一致。伺服器會送 62 個，引擎處理 23 個，缺 39 個（分 B1 訊息／B2 圖示／B3 視窗）。
3. **S_MESSAGE_CODE** 取 `Text.pak` 的 `string-<語系>.tbl`（CP950＝`string-c.tbl`，本機 3484 筆），id ≥ 筆數整包丟棄；引擎只硬寫 4 個 id → 建議載入整張表。
4. **版本檢查**：3.81 不要求 C14／S139（C14 屬 TRANSPORT）。S139 的功能旗標 `0x77d82` 決定原版拾取節流 600 ms、且不開連續攻擊。
5. 未知收包：S_BLINK＝S149 傳送（已有，原版移動中延後回確認）、S_HIT_RATIO＝S237（已有）、S_CLONE＝S76 第二型（已有）、S_SHOW_MAP≈S187（缺）、S_NOTICE 3.81 無。
6. 未知送包：10 個找到送出點與欄位（C_ONOFF＝C26 聊天開關，引擎缺）；**C_ALT_ATTACK、C_BOOK、C_SAVE 在 847 個呼叫點都沒有**。
7. S87 同身份入口：TW16 物件建立封包是 S_PUT_OBJECT（186）→`sub_9262D0`→`A51920`；同 id 原地覆寫、移動進度歸零，與引擎整筆取代大致相同。

## 怎麼驗證

- 每個結論都附 VA／File Offset，FO 由 `va2off.py` 從 PE 區段表計算。
- S250 三方比對可由 `t225_s250.py`＋`t225_cases.py`＋`t225_table.py` 重跑；送包呼叫點由 `ida_t225_sends.py` 重跑。
- `gap_count.py` 重算 gap-matrix 統計：289 列，已有 58、部分 165、缺 66，第 1–3 節「需先查證」歸零。

## 未驗證什麼

- 全部是靜態分析，沒有實機（沒有啟動遊戲、伺服器或 DB）。
- 「推定」的 3.81 對應沒有實機對包；S_EVENT 中交給其他函式讀欄位的子碼沒有展開，欄位寬度沒有逐一和 3.81 寫法核對。
- C_ALT_ATTACK／C_BOOK／C_SAVE 是否經 `sub_9FBC00` 以外的送出函式；protobuf id ≥315 的名稱。
- 本機 `string-c.tbl` 與 TW16 同捆版本是否相同（TW16 引用到 ≥3484 的 id）。

## 產出路徑

- `docs/reference/protocol-gaps.md`（新）
- `docs/reference/gap-matrix.md`、`docs/reference/INDEX.md`（更新）
- `docs/reference/rea-scripts/{ida_t225_sends,t225_cases,t225_s250,t225_cmp,t225_table,t225_strings}.py`（新）
- 分支 `task/532`

## 給下一棒的提示

- 可開的 code 任務（細節見 protocol-gaps.md 第 8 節）：
  - **A（高）** S_MESSAGE_CODE 載入整張 `string-c.tbl`：`ServerMessageText.cs`、`ServerStatusMessages.cs`，把 `MagicRendering.cs` 的 Text 封存讀取抽成共用。執行期唯讀，不可把表複製進專案。
  - **B1（中，依賴 A）** S250 訊息類子碼 0–4、12、15、23、27、30、31、79。
  - **B2（中）** S250 圖示類子碼 53、56、59、60、75、82、87、114、132、153、159、160、180（`MagicStatus.cs`）。
  - **C（中）** C26 聊天頻道開關（3.81 處理 type 255／0／2／6）。
  - **D（低）** S187 HouseMap 地圖視窗（等原生 UI）。
  - **E（低）** 送 C14、解析 S139，保存語系與功能旗標。
- B3（14 個視窗類子碼）要先逐窗查版面，本輪不開。
- 實作 S250 子碼時以 `server-protocol.md` 的 3.81 寫法為準，部分子碼 TW16 讀的欄位寬度不同。
- IDB 副本與中間輸出在 worktree 的 `scratch/rea/`、`scratch/t225/`，不進 git。
