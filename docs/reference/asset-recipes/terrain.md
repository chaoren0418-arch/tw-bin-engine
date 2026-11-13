# 素材配方：terrain 與地圖 bounds（T445／Issue #1030）

日期：2026-11-13。唯讀查核，沒有改程式。逐圖結果見同目錄 `terrain-compare.csv`（713 列）。

## 結論：部分 pass

| 項目 | 判定 | 說明 |
|---|---|---|
| 地圖 bounds（713 張） | **pass** | 由區塊檔名範圍推導，713／713 與 `maps/manifest.json` 相同。 |
| terrain 通行位元（bit0–3） | **pass** | 713 張、35,979,264 格，不符 0 格。產品只讀 bit0、bit1（與 bit7，基線從未出現）。 |
| terrain 區域位元（bit4–5） | **部分 pass** | 705 張整檔逐 byte 相同；8 張共 13,567 格不同（見「不符的地圖」）。 |
| `terrain0.bin` | **pass** | 等於 map 0 的 x 32576–32639、y 32896–33023 裁切，推導結果逐 byte 相同。 |
| `cache/original-map0.txt` | **部分 pass** | 是 map 0 的文字版。格式可重現；內容有 112 格區域位元不同（同 map 0）。 |

也就是：**產品實際用到的資料（bounds 與通行位元）可以只靠客戶端 `map/**/*.s32`、`.seg` 推導**；要與 r29 基線「整檔逐 byte 相同」則有 8 張地圖加 `original-map0.txt` 做不到，差異全部在產品沒有讀的區域位元。

## 推導規則（已驗證）

### 選檔

- 目錄 `map/<mapId>/`，只取檔名主體是 8 個十六進位字元的 `.s32`／`.seg`；**副檔名不分大小寫**（map 4 有 158 個大寫 `.S32`，格式與小寫相同）。
- 同一區塊兩種都有時用 `.s32`（713 張地圖裡有 282 個區塊如此）；只有 `.seg` 時用 `.seg`（47 個區塊：map 3 一個、map 4 四十三個、map 6 一個、map 10 兩個）。
- 不取 `tempseg.s32`、`800F7FF3E.s32`（9 字元）、`.MarketRegion`、`.scc`。

### 座標與 bounds

- 檔名 `XXXXYYYY`：`bx = 0xXXXX`、`by = 0xYYYY`。區塊涵蓋世界座標 `x0 = (bx − 0x7E00) × 64`、`y0 = (by − 0x7E00) × 64` 起的 64 × 64 格。
- `minX = min(x0)`、`minY = min(y0)`、`maxX = max(x0) + 63`、`maxY = max(y0) + 63`，對該地圖所有選到的區塊（`.s32` 與 `.seg` 一起算）取極值。

### attribute 層位置

| 格式 | 區段順序（到 attribute 為止） | attribute |
|---|---|---|
| S32 | ground 8192 × u32 → `u16 n` → cover n × 6 bytes | 8192 × u16（little-endian） |
| SEG | ground 8192 × u16 → `u16 n` → cover n × 4 bytes | 8192 × u8 |

attribute 是 64 列 × 128 欄。世界格 `(x, y)` 在區塊內 `lx = x − x0`、`ly = y − y0`，對應兩個值：

- `L(x,y) = attr[ly × 128 + 2 × lx]`（偶數欄）
- `R(x,y) = attr[ly × 128 + 2 × lx + 1]`（奇數欄）

bounds 內沒有區塊檔的格子，視為 `L = R = 1`（阻擋、無區域），照下面的規則一樣計算，不是直接輸出 0（map 3 與 111 有 26 格缺檔格因此是 2）。

### attribute → terrain byte

令 `b(v) = v & 1`，`A = b(R(x−1,y))`、`B = b(R(x,y))`、`C = b(L(x,y))`、`D = b(L(x,y+1))`：

| terrain 位元 | 規則 |
|---|---|
| bit0（1） | `!(A && B)` |
| bit1（2） | `!(C && D)` |
| bit2（4） | `!A && !B` |
| bit3（8） | `!C && !D` |
| bit4–5 | 看 `L(x,y)`：`(L & 0xF0) == 0x40` 或 `L & 0x04` → 16；否則 `L & 0x08` → 32；否則 0。沒有區塊檔的格子為 0。 |
| bit6–7 | 一律 0（713 份基線沒有任何一格設定） |

最後把地圖最外圈一格（`x == minX`、`x == maxX`、`y == minY`、`y == maxY`）整個 byte 設為 0。

輸出：`maps/terrain/<mapId>.bin` 是 `height × width` bytes，列優先，索引 `(y − minY) × width + (x − minX)`。

### 另外兩個檔

- `terrain0.bin`：map 0 的 terrain 取 x 32576–32639、y 32896–33023，64 欄 × 128 列，8,192 bytes。
- `cache/original-map0.txt`：map 0 的 512 × 512 terrain，每列用 `,` 分隔的十進位數字、列尾沒有逗號、每列以 CRLF 結束（含最後一列）。用這個格式輸出基線 `terrain/0.bin`，與基線 txt 逐 byte 相同。

## 比對結果（已驗證）

- 基線：主目錄 `engine/assets`（junction → `releases/r29/assets`）的 `maps/manifest.json`（SHA-256 `0fe3a61444180c90abdca0cc94c1cfb9ea19adc67812a43f624d806b51f96ee7`）與 `maps/terrain/*.bin` 713 份；每份先核對 manifest 記的 SHA 再比。
- 輸入：`D:\lineage開發\0-客戶端\map`。713 張地圖共用到 7,187 個 `.s32` 與 47 個 `.seg`；其中 7,188 個在 manifest 的 `blocks` 有紀錄，SHA-256 全部相同；其餘 46 個是 manifest 列為 `unsupportedBlocks` 的 `.seg`。
- 逐格比對 713 張、35,979,264 格：
  - 705 張整檔相同（推導結果的 SHA-256 等於基線）。
  - bit0、bit1、bit2、bit3 各自不符 0 格；bit6、bit7 不符 0 格。
  - bit4 不符 13,558 格、bit5 不符 16 格，合計 13,567 格，分布在 8 張地圖。
- 對照組：不用 `.seg`（只讀 `.s32`）時，map 3、4、6、10 共多出約 15 萬格不符；缺檔鄰格當成可走時，只剩 542 張相同。所以「`.seg` 當備援」與「缺檔視為阻擋」兩條都是必要的。
- bounds：713／713 相同。只用 `.s32` 算會有 3 張不同，所以要把 `.seg` 一起算。

### 不符的地圖（全部只差 bit4–5）

| mapId | 名稱（manifest） | 不符格數 | 相符率 | 範圍（x, y） | 內容 |
|---:|---|---:|---:|---|---|
| 42 | unknow map | 13,075 | 87.23% | 32531–32811, 32726–33004 | `L & 0xFE == 0x40` 的格子基線是 0，規則給 16。這張圖另有 585 格 0x40 基線是 16。 |
| 4 | 亞丁大陸 | 304 | 99.989% | 32657–33128, 32297–32943 | 226 格 0x40 基線是 0；78 格 `L & 0xFE == 0x20` 基線是 16（同圖另 118 格 0x20 是 0）。 |
| 0 | 說話之島 | 112 | 99.957% | 32634–32667, 33194–33207 | 客戶端 attribute 沒有任何區域位元，基線有 103 格 16、9 格 32。 |
| 244 | 污濁之地 | 26 | 99.975% | 32947–32957, 32809–32825 | 0x20 的格子基線是 16（同圖另 233 格 0x20 是 0）。 |
| 2101 | 冰之女王的宮殿 | 21 | 99.943% | 32754–32865, 32795–32927 | 零散的單格 0x40 基線是 0（同圖另 114 格是 16）。 |
| 2151 | 冰之女王的宮殿 | 21 | 99.943% | 同上 | 與 2101 相同。 |
| 70 | 遺忘之島 | 7 | 99.997% | 32855–32858, 32965–32970 | `L & 0xFE == 0x48` 基線是 32，規則給 16（同圖另 112,866 格 0x48 是 16）。 |
| 440 | 海賊島 | 1 | 99.9996% | 32316, 32889 | 單格 0x40 基線是 0。 |

同一個 attribute 值在同一張圖裡對到兩種基線值，所以這些格子**無法用任何只看該格 attribute 的規則重現**。

## blocked：伺服器資料有、客戶端沒有的部分

- 上表 8 張地圖共 13,567 格的區域位元（bit4 = 16、bit5 = 32）。基線的 terrain 來自伺服器 `server/maps/*.txt`（manifest 的 `sourcePath`），這些格子的值與本機客戶端 S32 的 attribute 不一致。
- 連帶：`maps/terrain/{0,4,42,70,244,440,2101,2151}.bin` 與 `cache/original-map0.txt` 無法整檔逐 byte 重現。
- 依任務規定，這部分列為已知限制交主控，不找 SQL／JAR 替代。
- 影響：產品不讀 bit4–5（見下表），所以用規則產生的檔案對通行判定沒有差別。若日後要顯示安全區／戰鬥區，這 8 張圖會與伺服器不一致。

「缺資源」查詢紀錄：地圖區塊是 `map/` 下的散檔，不在 pak 內。另用 PakViewer CLI（`pakviewer-cli pak list`）列過客戶端全部 19 個 pak（`Sprite`、`Sprite00`–`Sprite15`、`Text`、`Tile`），檔名沒有 `.s32`、`.seg`、terrain 或地圖文字檔。名稱相關的只有 `Tile.pak` 的 `ExtMapAttr.xml`（只定義 Market Region 與行為限制位元的名稱）、`CollisionZone.xml`（村莊與城堡的矩形碰撞區，範圍與上表不重疊），以及 `Text.pak` 的 `zone*.tbl`（沒有解開內容）；都不是這 13,567 格的來源。

## 產品讀 terrain 的位置與用到的位元

| 檔案 | 位置 | 讀法 | 用到的位元 |
|---|---|---|---|
| `OriginalMapCache.cs` | `OriginalMapStore` 建構式（19–28 行） | 讀 `maps/manifest.json` 的 `minX/minY/maxX/maxY` 與 `terrain.path`，檢查長度 = 寬 × 高；無 mapId 時讀 `cache/original-map0.txt`（512 × 512） | 整個 byte 存起來 |
| `OriginalMapCache.cs` | `Terrain(x,y)`（33 行） | bounds 外回 0 | — |
| `OriginalMapCache.cs` | `OriginalMapCache.Passable`（83 行） | 目的格 `v`：`v != 0`、`(v & 128) == 0`、`(v & 3) != 0`；方向 0 看起點 `a & 2`，方向 2 看 `a & 1`，方向 4 看 `v & 2`，方向 6 看 `v & 1`，斜向看 `v & 3` | bit0、bit1、bit7 |
| `OnlineMovement.cs` | `MovementRules.Passable`（7–14 行） | 讀 `terrain0.bin`，判斷式同上 | bit0、bit1、bit7 |
| `OnlineMovement.cs` | 119 行 | 送出移動前呼叫 `MapCache.Passable`，沒有 cache 時用 `MovementRules.Passable` | 同上 |
| `Ground.cs` | `PickupStep`（72 行） | 同上兩個 `Passable`；71 行只檢查 `terrain0.bin` 是否存在 | 同上 |
| `ApproachWalk.cs` | 49 行 | 同上兩個 `Passable` | 同上 |
| `MagicTargetPreparation.cs` | — | 沒有直接讀 terrain；測試控制器，53、68–69 行的 `Route` 與 56–57 行的 `MovementRules` 間接用到 `MovementRules.Passable` | 同上（間接） |
| `PlayerPointer.cs`（75、133 行）、`CombatVisibilityPlan.cs`（8 行） | — | 同上兩個 `Passable`（不在任務清單，一併列出） | 同上 |

- `v != 0` 與 `(v & 3) != 0` 同時要求，等同只看 bit0–1；bit7 在 713 份基線從未出現。**bit2、bit3、bit4、bit5 產品都沒有讀。**
- `OriginalMapBlock.Attributes`（從 S32 讀進來的 attribute）目前沒有任何讀取者，通行判定完全靠預先產生的 terrain。
- bounds 另由 `Minimap.cs`、`Portals.cs`、`MapResources.cs`、`WorldSession.cs` 以 `Bounds.Contains` 等方式使用。

## 推測

- 8 張地圖的差異應該是伺服器地圖檔與本機客戶端版本不同，或伺服器端另外改過（map 0 的差異區是一塊客戶端完全沒有區域標記的地方；map 42 幾乎整張圖的 0x40 都沒有轉成 16）。沒有讀伺服器檔案，無法確認。
- attribute 的 `0x20` 位元用途不明：713 張圖裡只在 map 4 與 244 出現，多數對應基線 0。`0x60` 只有 13 格（全在 map 244），基線都是 0，所以規則寫成 `(L & 0xF0) == 0x40` 而不是 `L & 0x40`；這一點只有 13 格的證據。
- bit2–3 的意義（多半是遠距攻擊可通過）與 bit4–5 的意義（16 安全區、32 戰鬥區）是依常見伺服器慣例的猜測，本張只驗了數值。

## 待驗證

- C# 準備器（T458）照本規則實作後，要在真客戶端上再跑一次 713 張比對（T470）；本張的比對是 Python 原型做的。
- 不在 713 張基線內的 4 個客戶端地圖目錄（18、4472、12000、12004）沒有基線可比。12004 有 512 個 `.s32` 在已知區段之後還有多餘資料，attribute 層的位置是否相同沒有驗。
- 原版 BIN 自己怎麼從 attribute 判斷通行（是否與這套伺服器轉換規則一致）沒有查；本張只證明「能重現基線」。
- `maps/manifest.json` 其他來自 SQL 的欄位（地圖 `name`、`copyTerrainMapId`）不在本張範圍，交 T450。

## 重現方式

腳本在 `scratch/t445/`（不入 git）：`cmp.py`（解析與規則）、`terrain_compare.py`（713 張比對、bounds、`terrain0.bin`、`original-map0.txt`）。執行 `python -I -B -X utf8 scratch/t445/terrain_compare.py`，只讀客戶端與基線，輸出 CSV 到 `scratch/`。腳本不保留時，照上面「推導規則」重寫即可，規則已完整列出。
