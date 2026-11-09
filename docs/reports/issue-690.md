# Issue #690 T289：查證原版——名稱旁圖示、S250 27／30／31、光效 +5、物品欄位 +36

階段 resource，第 1 次嘗試。分支 `task/690`。

## 做了什麼

- 唯讀分析 `TW1608262200.bin`（SHA-256 `e12949cf…641a2`），用 IDA 7.7＋P01 IDB 在 `scratch/rea/ida/tw16.idb` 的副本（前後 SHA-256 `3a0bf206…e82f`），跑 14 輪批次（q1–q14），subst 一律用 `W:`，每輪前後檢查沒有被占用、沒有殘留。
- 產出 [`docs/reference/followups-5.md`](../reference/followups-5.md)（FU5），回填 [`gap-matrix.md`](../reference/gap-matrix.md) 新增 12.9 節、12.6 結果說明與 Q3-4 更正；[`INDEX.md`](../reference/INDEX.md) 加一列；腳本與查詢清單放 `docs/reference/rea-scripts/t690/`。
- 結論摘要：
  - **Q3-2**：隊長 surf 8264／隊員 2826（名稱 X＋寬＋2）、主人 surf 773（＋7，另需地圖 ≠ 509）、+181＝icon 5199（3.81 永遠不出現）。等級差外框的「選項」是 `GetACP()` 地區碼，台版（950）永遠不畫。
  - **Q3-7**：27／30／31 都印在聊天欄（淡黃 palette[24]）：27＝784／2125＋階級名；30＝`dssd`，782＋783；31＝77「你覺得舒服多了。」＋下次自己出現時 gfx 831。
  - **Q4-1**：更正 FU4§3——`+12` 是影格號、`sub_8ABC10` 是 `Effect` vtable `+144`。`+9 ≠ 0` 的 `Effect` 播到第 1 格、kind 2 `Lightning` 播到第 2 格就提亮。3.81 會由 S42 type 8、kind 2 gfx、S106 gfx 2028–2056、`S_GlobalEffect(3)` 等觸發。
  - **Q4-2**：`+36`＝背包圖示 gfx；87 在 3.81 是戒指圖示，`$431` 分支不必做。
  - **9183**：本機 `Sprite*.idx`／`Tile.idx` 沒有 `9183.img`（9184、8264、5765 也沒有）。

## 怎麼驗證

- 反編譯＋反組譯交叉看（Hex-Rays 截掉的 `thiscall` 參數以 `push` 立即值為準）；3.81 欄位對 `serverpackets/*.java` 與 `ServerBasePacket` 補零規則；字串用 `t680_strings.py` 從本機 `string-c.tbl` 取；surf 用 `t690_surfcheck.py`；背包圖示用 `t690_invgfx.py` 掃 `380_db.sql`。

## 未驗證什麼

- 全程靜態，沒有真機。隊員圖示 `map<索引, surf>` 的填表者、gfx 831 的實際時機、3.81 S106 的 gfx 有沒有落在 2028–2056、gfx 2001／53／6706 的意義、`+9` 在推進以外的讀者都沒追。
- 沒有建置、沒有執行任何程式；只改文件。

## 產出路徑

- `docs/reference/followups-5.md`
- `docs/reference/gap-matrix.md`（12.6、12.8 後的更正、新增 12.9）
- `docs/reference/INDEX.md`
- `docs/reference/rea-scripts/t690/`（`ida_t690.py`、`queries.txt`、`t690_surfcheck.py`、`t690_invgfx.py`）

## 給下一棒的提示

- 可開 code：`ObjectRendering.cs`（隊伍／主人圖示、等級差外框固定不畫）、`PacketBoxMessages.cs`（27／30／31 印聊天）、R3-H（光效 `LightFlag`）、R3-C（9183 本機缺圖）。各自的建議見 FU5 §1.3、§2.5、§3.4、§5。
- `protocol-gaps.md` 2.2 節 30 列的欄位 `dd` 應為 `dssd`，下一次動到該檔時順手改。
- FU4§3.2「3.81 不會提亮」已撤回，R3-H 若已依 FU4 寫註解要改。
