# Issue #627（T266）：查證原版——TRADE_WINDOW 各模式版面與技能飛行（Q-1＋Q-4）

分支 `task/627`。完整內容見 [docs/reference/trade-window-and-projectiles.md](../reference/trade-window-and-projectiles.md)（TWP）。

## 結論

**Q-1 `TRADE_WINDOW`**

- `TradeWindow` 的開窗函式 `sub_A59780` 只有 5 個呼叫點：

  | 模式 | 用途 | 3.81 對應 |
  |---|---|---|
  | 6 | 修理 | S83→C20，每個選取的列各送一包 |
  | 7 | 學魔法 | S41→C39、S13→C191 |
  | 15、16 | 傭兵、弓兵 | 伺服器不支援 |

- NPC 商店與私商在 TW16 改走 `CNpcShop`。不過 `TradeWindow` 仍保留模式 0／1／13／14 的完整程式，送出的 op 89 和 3.81 C161 `C_Result` 逐欄相符，所以 R2-E 的商店可以直接用模式 0（買）和 1（賣）。
- **版面（TWP§1.3–1.5）：**
  - 視窗＝框 1984，239×317。
  - 清單 7 列、列距 36、列原點 (19, 38)；名稱在 (38, 3)；數量與「＋」「－」（1985／1987）在 y=19。
  - 頂部在 (19, 23) 畫 TOTAL 1991，在 (75, 22) 畫「合計/持有」。
  - 左鈕依模式換圖：BUY 1992、SELL 1998、OK 1996。右鈕是 CANCEL 1994。
  - 按鈕只有兩態：平常 n，按下 n+1。
  - 買的單筆上限依單價：9999／999／20／1。
- **空清單時開的頁：**
  - 修理：點的 NPC 外型 727 → `touma3`，否則 → `gayle5`。
  - 學魔法：NPC 外型 237／2820／其他 → `GerengEv3`／`Sedia1`／`Siriss1`。
  - 以物品學魔法：一律 `HorunEv3`。
  - 這 6 個頁名在 LiTo（3.81）快照中也都有。
- **`LoadingWindowUI`** 是 TW16 的 XML 窗（`LoadingScene.xml`，本機缺）。3.81 快照沒有任何 `Loading`／`.xml` 字串，所以 3.81 世代沒有這個窗，**不建議補**。

**Q-4 技能飛行與 `]`**

- RTTI 解出類別名：kind 2＝`Lightning`、3＝`Arrow`、4＝`EnergyBolt`、5＝`FireBall`、6＝`EarthRising`、7＝`Eruption`。
- **kind 6：** 在目標目前位置播一次。每個 `!` 格都受擊。
- **kind 7：** 每一段在原地播一次，播到第三格時生下一段（7374／10145 一次跨兩格），只用 4 個方向。到終點時用參數 gfx 建一個 `EarthRising`。**受擊只在終點物件的 `!` 格**，段本身不受擊。4728→4730 沒有 `!`，所以沒有受擊動作。
- **`]`：**
  - **有處理：** `UserObject`（角色、NPC、門、`.s32` 地圖特效，都走 `sub_A4B060`／`sub_A4BA20`）與預設 `Effect`（含落點圖、子特效）。
  - **不處理：** 飛行物（kind 3／4／5）、kind 2／6／7、`EffectLoop`。

## 做了什麼

1. 把 P01 的 IDB 複製到 worktree 的 `scratch/rea/ida/tw16.idb`，複製後 SHA 相同（`3a0bf206…e82f`）。用 IDA 7.7 唯讀批次查詢 13 輪。新腳本 `ida_t266.py` 支援 `dec`／`xdec`／`xlist`／`vt`。
2. 讀 `TradeWindow` 的建構、開窗、繪製、列繪製、數量、滑鼠、送出各函式，以及 5 個開窗的收包處理函式。
3. 解 `Tile.pak` 的 `uiextra86.xml`，確認第 5 段＝`TRADE_WINDOW`；讀 31 個 surf 的尺寸。
4. 讀 kind 6／7 的建構、啟動、每格處理，以及 Arrow／EnergyBolt／FireBall／Lightning／EffectLoop／Effect 的每格處理。用 vtable 第 19 格讀出 15 個類別的 RTTI 名稱。
5. 追 `.s32` 地圖特效與門的建立和動畫路徑：`A4E5D0`→`A51920`→`A504A0`（UserObject）→`A44500`→佇列 226→`A4BF00`→`A467A0`→`A4B060`。
6. list.spr 普查：kind 6 有 12 個 gfx、kind 7 有 23 個，逐一記錄 `!`／`]` 與參數。
7. 在 LiTo OEP 快照（`4271d92f…`）搜尋字串，做 3.81 的交叉核對。
8. 對照伺服器封包（SP 的 S83、C20、C161、C39／C191）與引擎（`TradeWindowPanel.cs`、`MagicRendering.cs`、`SubEffects.cs`）。
9. 產出 TWP，並更新：
   - `INDEX.md`：新增一列。
   - `gap-matrix.md`：依據代號 TWP、11.5 的 R2-E／R2-H 依賴、11.6 的 Q-1／Q-4 標成已查、新增 11.8 回填 9 列，並附 Q-3 的附帶結果。

## 怎麼驗證

- 送出格式與 3.81 逐欄對照：
  - op 89 `hcdch`＋8n 對 C161 `D C C C`＋n×`DD`
  - op 228 對 C20 `DD`
  - op 123／210 `H`＋n×`D` 對 C39／C191
  - S83 `D(200) H n×(D C)` 對 `dh`＋n×`dc`
- XML 段索引有兩處吻合：`sub_7D7650(5)` 取第 5 段＝`TRADE_WINDOW`；元件 5–8 在列繪製與合計中的用途，和 XML 的 surf 名稱（UP／DOWN／ADENA／TOTAL）一致。
- kind 7 的終點物件是 EarthRising：`sub_81C9D0` 直接呼叫 `sub_81C340`，再由 `t266_vt_owner.py` 解出 vtable `0x1179844`＝`EarthRising`。
- `gap_count_r2.py` 重算：套用 88 列，統計仍是 67／175／48（本輪狀態都不變）。
- 每輪 IDA 結束後，`subst` 只剩別的任務的對映，`T:` 已解除。沒有改產品程式，只改 `docs/`。

## 未驗證什麼

- 全程靜態，沒有實機。3.81 只比對字串；LiTo 快照可能不完整，「3.81 沒有 XML／讀取畫面」可信度中高。
- 賣出確認清單 `sub_A2C660`（`this+48`）的來源、修理列的數量上限 `+296`、`sub_A2A580` 何時使用、`LoadingWindowUI` 的名稱來源，本輪都沒查。
- 商店走 `TRADE_WINDOW` 是推論：依保留的模式碼與送出格式，TW16 本身走 CNpcShop。
- 與 [`windows-2.md`](../reference/windows-2.md) 的說法有出入：該文把本機 `uiextra86.xml` 稱為「3.81 世代設定」，但 LiTo 快照沒有任何 `.xml` 字串。本輪只在 TWP§1.8 註明，沒有改 windows-2。

## 執行中的意外（已處理）

第 2 輪 IDA 我照舊用 `W:`。當時 `W:` 已被 task-629 掛載，而舊的 `run_ida.ps1` 沒有先檢查就執行，結束時的 `subst W: /D` 解除了 task-629 的對映：

- 我的 IDA 開對方 IDB 時得到 “Permission denied”，沒有讀寫對方的資料庫；對方的 `scratch/rea/ida/` 下多了一個 `q2/ida.log`（436 bytes，沒有刪除）。
- 我立刻重新掛回 `W:`，指向 task-629 的 `scratch\rea`，之後一律改用 `T:`。
- 新增的 `rea-scripts/t266/run_ida.ps1` 會在掛載前檢查代號；被占用時以 3 結束，不進入會解除對映的 finally。
- task-629 那一輪的 IDA 可能因此中斷（對方目錄留有未打包的 `tw16.id0`／`.id1`），重跑即可。

## 產出路徑

- `docs/reference/trade-window-and-projectiles.md`（新）
- `docs/reference/INDEX.md`、`docs/reference/gap-matrix.md`（更新）
- `docs/reference/rea-scripts/t266/`：`ida_t266.py`、`run_ida.ps1`、`queries.txt`、`t266_vt_owner.py`、`t266_xver_strings.py`、`t266_kind67.py`
- `docs/reports/issue-627.md`
- `scratch/rea/`：IDB 副本、偽碼、解出的 XML，不進 git

## 給下一棒的提示

- **R2-E**（等 R2-F 合併）：照 TWP§1.9 做六項（版面、按鈕、多選數量、商店模式 0／1＋C161、學魔法模式 7、空清單頁）。修理要記住「點的 NPC 外型是否為 727」。賣出確認在 `sub_A2C660` 查到之前先不做。
- **R2-H**：照 TWP§3.6 做。
  - kind 7 實作逐段推進，拿掉 `CasterHits` 的 kind 7。
  - `]` 加到 `.s32` 地圖特效、門、落點圖；從 kind 2／6 拿掉。
  - 測試：129→123（受擊時間），以及 4728→4730（沒有受擊）。
- **gap-matrix 4.1 `LoadingWindowUI`：** 建議調度器把這一列標成「不建議補」。本輪沒有改狀態，以免和統計規則衝突。
- **Q-3：** `C_EXCHANGEABLE_SPELL` 的送出點已由本輪查到（TWP§1.6），Q-3 任務可以略過這一項。
- **重跑 IDA：** 請用 `rea-scripts/t266/run_ida.ps1`，它會檢查 subst 代號。
