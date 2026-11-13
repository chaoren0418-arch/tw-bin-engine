# 素材配方：地圖、TIL、scene、cache、地圖特效（T450／Issue #1035）

日期：2026-11-13。唯讀查核，沒有改程式，沒有複製任何 map／TIL 進專案。

- 基線：主目錄 `engine/assets`（junction → `releases/r29/assets`），`integrity.json` 的 `maps/` 13,304 檔、`cache/` 378 檔、`scene/` 13 檔，共 1,697,243,532 bytes。
- 輸入：`TW_CLIENT_ROOT` = `D:\lineage開發\0-客戶端`。`Tile.idx` SHA-256 `9291fd80…ad24db`、`Tile.pak` `82e52f47…d60d4a`（與產品 `UiResourcePins.Tile` 相同）。
- 比對方式：Python 原型照下面的規則重新產生，再與基線逐 byte 或逐欄比。
- terrain（`maps/terrain/*.bin`、`terrain0.bin`、`cache/original-map0.txt`）與 bounds 見 `terrain.md`（T445）；三張混色表（`cache/*-channel.bin`）屬 T448，本文只列讀取點。

## 結論

| 子類 | 檔數 | bytes | 性質 | 判定 | 比對數字 |
|---|---:|---:|---|---|---|
| `maps/map/**`（S32／SEG 原檔） | 7,198 | 828,455,751 | 原檔切片 | **pass** | 7,198／7,198 與客戶端 `map/<id>/<檔名>` 逐 byte 相同 |
| `maps/tiles/*.til` | 5,052 | 555,746,825 | Tile.pak entry 切片 | **pass** | 5,052／5,052 與 Tile.pak 的 `<id>.til` 逐 byte 相同 |
| `maps/ship/*.lac` | 3 | 709,197 | Tile.pak entry 切片 | **pass** | 3／3 相同 |
| `cache/map/0/*.s32` | 20 | 2,429,620 | 原檔切片（`maps/map/0` 的子集） | **pass** | 20／20 相同 |
| `cache/tiles/*.til` | 353 | 34,445,943 | Tile.pak entry 切片（`maps/tiles` 的子集） | **pass** | 353／353 相同 |
| `maps/manifest.json` | 1 | 4,954,075 | 衍生 | **部分 pass** | 產品讀的欄位全部可由客戶端重現；`name` 等 SQL 欄位與 `record`、絕對路徑不能或不該重現 |
| `cache/manifest.json` | 1 | 155,912 | 衍生 | **pass**（產品讀的欄位） | `maps` 20／20 列相同；`tiles` 353 列的 `tile/sha256/size` 相同，`record` 已過時 |
| `scene/manifest.json`、`draw-commands.json`、`tile-pixels.json` | 3 | 31,634,828 | 衍生 | **pass** | 3／3 檔逐 byte 相同 |
| `scene/opacity.json` | 1 | 3,331,094 | 衍生 | **pass** | 逐 byte 相同（含本機絕對路徑；拿掉路徑後內容 211 groups、1,825 triggers 全同） |
| `scene` 其餘 9 檔（預覽圖與 P05 中間檔） | 9 | 30,179,943 | 衍生，產品不讀 | **pass**，建議不產生 | `map.json`、`tiles.json`、`region.rgb555` 逐 byte 相同；6 張 PNG 解碼後像素相同 |
| `maps/effects/<id>.json` | 336 | 168,182,124 | 衍生 | **pass** | 336／336 檔逐 byte 相同（含 `6016.json`） |
| `maps/effects/manifest.json` | 1 | 383,121 | 衍生 | **pass**（去掉絕對路徑後） | `effects` 336 列的 id、sha、flags、frames、sequence、來源 SPR SHA、metadata SHA 全同；`unavailable` 154 列的 id 與原因全同 |
| `maps/terrain/*.bin` | 713 | 35,979,264 | 衍生 | 見 `terrain.md` | 705 張整檔相同，8 張只差產品不讀的區域位元 |
| `cache/original-map0.txt` | 1 | 649,691 | 衍生 | 見 `terrain.md` | 112 格區域位元不同 |
| `cache/{screen,multiply,half}-channel.bin` | 3 | 6,144 | 衍生 | 屬 T448 | 本文沒有比 |

沒有任何子類是 blocked。blocked 的只有個別欄位（見「`maps/manifest.json`」一節）。

## 原檔切片（已驗證）

對基線的每個檔先核對 `integrity.json` 的 size 與 SHA（13,304＋378 筆裡這五個子類全部相符），再與來源比 SHA-256。

| 基線路徑 | 來源 | 相同／總數 | 例外 |
|---|---|---:|---|
| `maps/map/<mapId>/<檔名>` | `TW_CLIENT_ROOT/map/<mapId>/<檔名>`（檔名比對不分大小寫） | 7,198／7,198 | 無 |
| `maps/tiles/<id>.til` | `Tile.idx` 以不分大小寫的名稱 `<id>.til` 找到的 entry | 5,052／5,052 | 無 |
| `maps/ship/{Water,Pier1,Pier5}.lac` | `Tile.idx` 的 `Water.lac`、`pier1.lac`、`pier5.lac` | 3／3 | 無 |
| `cache/map/0/<檔名>` | `TW_CLIENT_ROOT/map/0/<檔名>` | 20／20 | 無 |
| `cache/tiles/<id>.til` | 同 `maps/tiles` | 353／353 | 無 |

補充：

- 7,198 個 map 原檔 = 7,197 個 `.s32`（其中 159 個副檔名是大寫 `.S32`，manifest 的 `path` 保留原大小寫）＋ 1 個 `map/6/7fff8000.seg`。
- `cache/` 的 373 個原檔全部與 `maps/` 的同名檔 SHA 相同，是重複的一份。
- `Tile.idx` 是 plain28 格式（u32 筆數；每筆 u32 offset、20 bytes 名稱、u32 長度），5,446 筆、名稱不重複、依小寫名稱排序：`.til` 5,364、`.xml` 65、`.lac` 8、`.spr` 8、`.enx` 1。最大的 TIL 是 167,432 bytes，最大的 map 區塊是 336,072 bytes。
- **Tile.idx／Tile.pak 在封裝之後換過版本**：基線兩個 manifest 記的是 `Tile.idx` `48d3946c…`、`Tile.pak` `c234fb3c…`，現在的客戶端是 `9291fd80…`／`82e52f47…`。5,052 個 TIL 的內容沒有變，但 5,047 個 entry 的 offset 與 record 序號都變了。所以**不能用舊 manifest 的 offset 切片，要用名稱查 idx**；也說明「鎖整個 pak 的 SHA」比「鎖每個 entry 的 SHA」容易因為無關的改版而失效。

## 衍生檔的 recipe

共用的 S32 區段順序（little-endian）：

1. ground：8192 × u32，每個值 `index = v & 0xFF`、`tile = (v >> 8) & 0xFFFF`、`uk = v >> 24`；第 i 個的 `x = i % 128`、`y = i / 128`。
2. cover：`u16 n`，n × 6 bytes（`x:u8, y:u8, index:u8, tile:u16, uk:u8`）。
3. attributes：8192 × u16。
4. groups：`i32 n`；每組 `id:u16, count:u16`，count × 7 bytes（`x:u8, y:u8, z:u8, index:u8, tile:i16, uk:u8`）。
5. opacity triggers：`i32 n`，n × 5 bytes（`x:u8, y:u8, groupOrdinal:u16, type:u8`）。
6. tile refs：`i32 n`，n × i32。
7. portals：`u16 n`；每筆 `len:u8`、名稱 len bytes、`x:u8, y:u8, target:u16, id:i32`。
8. effects tail：`u16 n`（bit15 = 每筆多 4 bytes），`n & 0x7FFF` 筆 `id:u16, x:u16, y:u16`。**有 474 個區塊在 portals 之後就結束，沒有這一段**（manifest 的 `noEffectTail = true`）。

各欄 `offset` 一律是該筆資料在檔案內的 File Offset。

### `maps/manifest.json`（schema `p24-map-provider-v1`）

基線格式：`json.dumps(indent=2, ensure_ascii=False)`，換行 CRLF，檔尾有一個換行，UTF-8 無 BOM。

| 欄位 | 產品是否讀 | 規則 | 比對 |
|---|---|---|---|
| `blocks[]`：`mapId`、`name`（小寫）、`path`（`map/<目錄>/<原檔名>`）、`sha256`、`noEffectTail` | 讀 | 列出每個地圖目錄下副檔名是 `.s32`（不分大小寫）的檔；`noEffectTail` = 檔案在 portals 段之後剛好結束。排序：目錄名當字串、再檔名。 | 7,197／7,197 列的 sha、path、noEffectTail 相同；排序相同 |
| `blocks[]` 最後一列（map 6 的 `7fff8000.seg`）：另有 `format: "classic-seg"`、`nativeVa`、`nativeImageBase`、`originalSource` | 讀 `format`、`sha256`、`path` | 產品只接受這一個 SEG（SHA 寫死 `aad5ff59…101a45`）。`originalSource` 是絕對路徑，新檔要拿掉。 | SHA 相同 |
| `maps[]`：`mapId`、`minX/maxX/minY/maxY`、`width`、`height` | 讀 bounds | 見 `terrain.md`「座標與 bounds」 | 713／713（T445） |
| `maps[].terrain.path`、`.sha256` | 讀 | terrain 檔的相對路徑與產出檔的 SHA | 713 筆與 integrity 相符 |
| `maps[].terrain.sourcePath`、`.sourceSha256` | 不讀 | 伺服器 `maps/<id>.txt` 的絕對路徑與 SHA。**新檔要拿掉。** | — |
| `maps[].verifiedAbsent[]` | 讀 | 對 `bx` 從 `minX/64 + 0x7E00 − 2` 到 `maxX/64 + 0x7E00 + 2`、`by` 同理（bx 在外層）：`<bx:04x><by:04x>` 的 `.s32.gz`、`.s32`、`.seg.gz`、`.seg` 四種檔都不存在就列入。 | 713／713 張相同，共 30,470 個名稱 |
| `maps[].unsupportedBlocks[]`：`name`、`sources[].{path,sha256}` | 讀 `name` | 同一個迴圈裡，沒有 `.s32` 但四種檔有任一存在的列入（實際全是 `.seg`）。`sources[].path` 是絕對路徑，新檔改成相對路徑或拿掉。 | 713／713 張相同，共 46 個（map 3：1、map 4：43、map 10：2） |
| `tiles[]`：`tile`、`sha256`、`path` | 讀 `tile`、`sha256` | 所有列入的 `.s32` 的 ground、cover、objects 用到的 tile id 聯集，加上 140，去掉 65535；在 `Tile.idx` 找得到 `<id>.til` 的依 id 排序列出。 | 5,053 個 id 與基線（5,052＋1 個 missing）完全相同 |
| `tiles[].record` | 不讀 | 舊 Tile.idx 的 record 序號、位置、hex、offset。**5,047／5,052 筆已經與現在的 Tile.idx 不符**，新檔要拿掉或重算。 | 不相同（已過時） |
| `missingTileIds` | 不讀 | 上面的 id 裡 `Tile.idx` 沒有的 | `[4094]`，相同 |
| `maps[].name` | 不讀 | 來自 SQL 的地圖名稱（14 張是 `unknow map`）。 | **blocked**：客戶端沒有對應來源，本張沒有找替代 |
| `maps[].copyTerrainMapId`、`clientMapDirectoryPresent`、`directTerrainFilePresent` | 不讀 | 713 張的值都一樣（−1、true、true），來自 SQL 與伺服器目錄。 | 建議拿掉 |
| `unsupportedMaps`、`provenance`、`scope` | 不讀 | 研究清冊的 SHA 與伺服器 terrain 的檢查結果，含絕對路徑。 | 建議拿掉 |

地圖與區塊的選取範圍與基線有三處不同，**需要主控決定新規則**：

1. 基線的 713 張 = SQL 有紀錄、伺服器有 terrain 檔、客戶端有目錄三者的交集。只看客戶端的話，有 `.s32` 的數字目錄是 716 個，多出 4472（4 個區塊，基線已經收了區塊但沒有列入 `maps`）、12000（66 個）、12004（544 個）。另有目錄 18 只有 1 個 `.seg`。
2. 12004 有 512 個 `.s32` 在 effects tail 之後還有多餘資料，現行解析會拒絕（另外 32 個正常）。
3. 基線收了 6 個檔名不是 8 個十六進位字元的檔（`tempseg.s32` 5 個、`map/4/800F7FF3E.s32` 1 個，共 928,882 bytes）。產品用 `<bx><by>.s32` 找區塊，永遠讀不到它們。

### `cache/manifest.json`（schema `p24-original-cache-v1`）

基線格式：`indent=2`、CRLF、檔尾沒有換行。

| 欄位 | 規則 | 比對 |
|---|---|---|
| `maps[]`：`name`、`sha256`、`size`、`effectCount`、`portalCount` | map 0 的 `bx` 0x7FFB–0x7FFE（外層）× `by` 0x8001–0x8005 共 20 個 `.s32`；`effectCount` 是 effects tail 筆數（全是 0），`portalCount` 是 portals 筆數 | 20／20 列完全相同 |
| `tiles[]`：`tile`、`sha256`、`size` | 這 20 個區塊用到的 tile id 加上 140，依 id 排序 | 353／353 相同 |
| `tiles[].record` | 同上一節，已過時（351／353 不符），產品不讀 | 拿掉 |
| `terrain`：`path`、`sha256`、`origin [32256,32768]`、`size [512,512]` | `sha256` 要寫新產出的 `original-map0.txt` 的 SHA（基線的值是伺服器檔的 SHA） | 見 `terrain.md` |
| `verifiedMissing` | 固定四個名稱 `7e007e00`、`7e017e00`、`7e007e01`、`7e017e01`；條件是 `map/0` 下這四個名稱的四種副檔名都不存在 | 四個名稱的 16 個路徑在現在的客戶端都不存在 |
| `pixelModes[]`：`mode`、`path`、`sha256` | 三張混色表的 SHA（T448） | 基線三個 SHA 與檔案相符 |
| `sourceArchives`、`scope` | 絕對路徑與舊的 Tile SHA，產品不讀 | 拿掉 |

### scene 的三個執行檔（schema `p22-dual-map-v1`）

輸入：`map/0/7ffd8002.s32`（北，編號 0）、`map/0/7ffd8003.s32`（南，編號 1），與它們用到的 120 個 TIL。

`draw-commands.json`（26,960 筆）：

1. 兩個區塊各自依 ground、cover、objects 的順序展開。每筆的欄位順序：`layer`、`source_index`（該層內的序號）、`serial`（= `source_index + (1 − 編號) × 100000`，北區塊加 100000）、`block`、`original_source_index`、然後是該筆原始欄位（ground／cover：`x, y, index, tile, uk, offset`；objects：`x, y, z, index, tile, uk, group, group_ordinal, offset`），其中 `y` 改成 `y + 64 × 編號`。
2. 螢幕位置 `px = 24 × (x − x/2 + y)`、`py = 12 × (y − x/2)`（整數除法）。
3. 以 `(py, px, ground 為 0 其餘為 1, z（沒有就 0）, −serial)` 穩定排序。

`tile-pixels.json`（10,991 筆）：依 tile id 由小到大、同一 tile 內依 index 由小到大，每筆 `{tile, index, type, pixels}`，`pixels` 是 `[x, y, rgb555]`。TIL 格式：`i32 n`、`(n+1) × i32` offset、之後是資料；第 index 塊的第一個 byte 是 type（這兩個區塊只出現 0、1、2、3、6、7、16、17）。

- `type & 2 == 0`（菱形）：24 列，第 y 列有 `2(y+1)`（y < 12）或 `2(23−y)` 個 u16；`type & 1` 時從 x = 0 起，否則從 `24 − 個數` 起。
- `type & 2 != 0`（RLE）：`x0, y0, w, h` 各 1 byte；每列 `u8 段數`，每段 `skip:u8, run:u8`（x 前進 `skip / 2`），接 run 個 u16。

`manifest.json`：`canvas_size = [max(px) + 24, max(py) + 24 + 756]` = `[4608, 2328]`，`draw_count = 26960`，其餘欄位是固定值（`world_origin [32576, 32896]`、`world_inclusive_bounds [32576, 32896, 32639, 33023]`、`pixel_origin [0, 756]`、`terrain_stride 64`、`terrain_rows 128`、`blocks`、五個說明字串）。

三個檔都是 `json.dumps(separators=(',', ':'), ensure_ascii=False)`，沒有換行、沒有檔尾換行。照這個格式輸出，三個檔的 SHA-256 與 `integrity.json` 相同。

注意：P24 目錄下同名的 `dual_block_assets.py` 會寫另一個 schema；現行基線是 P22 那一支的輸出。

### `scene/opacity.json`（schema `p23-original-opacity-v1`）

產品檢查的 schema 字串是 `p23-…`。P24 的 `opacity_sources.py` 現在寫的是 `p24-…`，照它輸出產品會拒絕；本文的規則以基線檔為準。

- 順序：先南（`7ffd8003`，`ybase = 64`，`slot = 2`）再北（`7ffd8002`，`ybase = 0`，`slot = 0`）。
- `maps[]`：`block`、`path`、`sha256`、`slot`、`ybase`、`groups`（群組數）、`triggers`（trigger 數）。`path` 是 S32 的本機絕對路徑，**新檔要拿掉**（產品不讀 `maps`）。
- `groups[]`（211 筆）：`key = "<block>:<ordinal>"`、`block`、`ordinal`、`id`、`offset`、`objects[]`（該群組的 objects 原始欄位加 `globalY = y + ybase`）。
- `triggers[]`（1,825 筆）：原始欄位 `x, y, group_ordinal, type, offset`，加 `block`、`key`、`globalX = x`、`globalY = y + ybase`、`bits = 1 << type`、`coldSlot = slot`、`admittedByColdSlot = x < 256 且 y < (slot 為 0 時 128，否則 64)`（1,796 筆為 true）。`type` 只有 0、1。
- 另有固定字串 `ordering`、`scope`。
- 格式：`indent=2`、`ensure_ascii=False`、CRLF、檔尾沒有換行。在本機用同樣的絕對路徑輸出，SHA 與基線相同。

`opacity_sources.py` 還會讀歷史 BIN 與 IDA 輸出，那些只寫到研究目錄的 `source-manifest.json`，不進 `opacity.json`。

### scene 其餘 9 檔（產品不讀）

`map.json`、`tiles.json`、`region.rgb555`、`region.png`、`ground.png`、`ground-cover.png`、`objects.png`、`overview.png`、`atlas.png` 是 P05 單區塊（`7ffd8002`）的中間檔與預覽圖。產品原始碼沒有任何地方開這 9 個檔，只有 `Assets.VerifyIntegrity` 因為它們列在 `integrity.json` 而檢查存在與 SHA。

- 已驗證可重現：`map.json`、`tiles.json`（7,602 筆）、`region.rgb555` 逐 byte 相同；6 張 PNG 解碼成 RGBA 後與重畫的結果相同。
- PNG 的檔案 bytes 取決於編碼器（用 Pillow 12.2 重新編碼基線的像素可以得到相同 bytes，C# 的編碼器不會）。
- 建議：新 recipe 不產生這 9 檔，從 integrity 拿掉，省 30,179,943 bytes。

### `maps/effects/<id>.json` 與 `manifest.json`（schema `p24-map-effects-v1`）

輸入：17 個 Sprite 封存檔（`Sprite`、`Sprite00`–`Sprite15`）的 `<id>-0.spr`，與 Text 封存檔的 `list.spr`、`list.spz`。

1. **要處理的 id**：所有列入的 `.s32` 的 effects tail 出現過的 id。現在的客戶端是 490 個 id、24,760 筆引用，與基線（336 個可用＋154 個不可用）的集合完全相同。
2. **來源 SPR**：在 17 個 idx 裡用不分大小寫的名稱 `<id>-0.spr` 找。找到多份而內容 SHA 不同 → 不可用（`ambiguous archive content`）。336 個可用的 id 都只在一個封存檔出現（293 個在 `Sprite`，43 個在 `Sprite00`–`15`）。基線 manifest 記的 offset 與 size 對現在的 Sprite 封存檔仍然有效（336／336）。
3. **metadata**：解開 `list.spr`（Text entry 以 DES-ECB 解碼，與產品 `TextArchive` 相同）與 `list.spz`（開頭 `S1223`，再一層 AES-CBC；產品 `ListSprRuntime` 已有實作，見 `action-timing.md`）。取從行首 `#<id>` 加空白起、到下一個行首 `#<數字>` 加空白之前的 bytes。兩份必須相同。找不到 → 不可用（`no original animation metadata`）。
   - 現在的 `list.spr` 解碼後 SHA `523c61ad…b8f127`、`list.spz` 內層 `e8cec038…be6242`，與研究當時記錄的值相同。
4. **序列**：在 metadata 裡找動作 3（regex `(?<![\d.])3\.[A-Za-z][^(]*\((\d+)\s+(\d+),([^)]*)\)`），沒有再找動作 0。第一個數字（方向性）不是 0 → 不可用（`unresolved stationary direction`）。每個 token `a.b:c<事件>`：`a` 必須是 0、`c > 0`，輸出 `{frame: b, ticks: c, events: 事件原文}`；筆數要等於第二個數字。沒有任何動作時 SPR 必須只有 1 幀，序列是 `[{frame: 0, ticks: 1, events: ""}]`，否則不可用（`multiple frames without animation metadata`）。
5. **flags**：`104.attr(n)` 的 n，沒有就是 0；只接受 0、8、128、256（基線：8 有 175 個、0 有 130 個、256 有 19 個、128 有 12 個）。
6. **SPR 解碼**：
   - 第一個 byte 是 255 → 有調色盤：`u8 色數`（0 代表 256）、色數 × u16、`u8 幀數`；否則第一個 byte 就是幀數，像素直接是 u16（336 個裡 301 個有調色盤、35 個沒有）。顏色 bit15 不得為 1。
   - 每幀：`l, t, r, b: i16`、兩個 u16（保留）、`i16 塊數`；每塊 `a:i8, b:i8, mode:u8, index:u16`。幀寬或高不在 1–2048 → 不可用（`invalid frame dimensions`）。
   - 之後 `u32 資料塊數`、資料塊數 × u32 offset、`u32 展開長度`，再接資料塊：`sx, sy, w, lines` 各 1 byte；每列 `u8 段數`，每段 `skip:u8, run:u8`（x 前進 `skip / 2`），接 run 個像素（有調色盤時 1 byte 索引，否則 u16）。
   - 檢查：offset 與實際位置一致、檔尾沒有多餘 bytes、展開長度相符、每個像素落在幀的 bounds 內、每幀至少一個像素。
7. **輸出 `<id>.json`**：鍵的順序 `id`、`flags`、`sequence`、`frames`、`entrySha256`（metadata bytes 的 SHA）、`spriteSha256`、`tickRate`（24）、`eventPolicy`（固定字串）。`frames[]` = `{index, bounds: [l, t, r, b], blocks[]}`；`blocks[]` = `{a, b, z: mode, order: 幀內序號, pixels: [[x, y, rgb555], …]}`。格式：`separators=(',', ':')`、`ensure_ascii=False`，**檔尾是 CRLF**。
8. **`manifest.json`**：`effects[]` 依 id 排序，每列 `id`、`path`、`sha256`（輸出檔）、`flags`、`frames`（幀數）、`source`、`metadataSha256`、`sequence`；`unavailable[]` = `{id, error}`；固定字串 `scope`。基線的 `source.index`、`source.pak` 是絕對路徑，**新檔改成封存檔名稱**（例如 `"archive": "Sprite13"`）；`source.entry` 的 record 位置與 hex 建議拿掉，只留名稱、大小與 SHA。

比對：336／336 個輸出檔的 SHA 與 manifest、`integrity.json` 相同，合計 168,182,124 bytes。154 個不可用的 id 與原因全部相同：

| 原因 | 個數 |
|---|---:|
| `list.spr` 沒有這個 id 的條目 | 141 |
| 幀尺寸不合法 | 6 |
| 多個封存檔內容不同 | 3 |
| 動作有方向性 | 3 |
| 沒有動作卻有多幀 | 1 |

這 154 個 id 在 80 張地圖被引用 3,280 次。這是基線既有的限制，recipe 照樣重現，不是本張新增的缺口。

`6016.json` 是這 336 檔之一（來源 `Sprite13` 的 `6016-0.SPR`，12 幀，序列第 2 步帶事件 `<3332<1340<1340`），輸出 SHA `825673b1…263383`，與產品寫死的 pin 相同。

### 「缺資源」查詢紀錄

本文有兩處說客戶端沒有：`4094.til`，以及 141 個特效 id 在 `list.spr` 沒有條目。

- 用 PakViewer CLI（`pakviewer-cli pak list <idx>`）列出客戶端全部 19 個封存檔的檔名：`Tile`（5,446 筆）、`Text`、`Sprite`、`Sprite00`–`Sprite15`。
- 搜尋結果：沒有 `4094.til`；沒有任何 `.s32`、`.seg`；`.lac` 只在 `Tile`（8 個）。抽查不可用的特效 `1914-0.SPR` 同時在 `Sprite` 與 `Sprite15`，大小不同（694,733 與 696,883），與「內容不同」的判定一致。
- 141 個缺條目的 id 是在解開的 `list.spr` 與 `list.spz` 兩份裡都找不到 `#<id>` 行，沒有另外用 PakViewer 解出文字檔對照。

## 產品讀取點與 SHA 檢查

| 位置 | 讀什麼 | 檢查 | 換成新產生的檔要不要調整 |
|---|---|---|---|
| `Assets.cs` 61–73 `VerifyIntegrity` | `integrity.json` 的每一列 | schema 必須是 `p06-assets-integrity-v1`；`maps/` 開頭的略過；其餘（含 `cache/` 378 檔、`scene/` 13 檔）逐檔核對存在、大小、SHA | 要。integrity 由準備器重產；`cache/map`、`cache/tiles` 與 scene 的 9 個預覽檔若不再產生，要從清單拿掉 |
| `Assets.cs` 33–43 | `scene/manifest.json`、`tile-pixels.json`、`draw-commands.json` | schema `p22-dual-map-v1`、bounds、`pixel_origin`、`draw_count`、排序；沒有個別 SHA | 不用。三個檔可以逐 byte 重現 |
| `SceneOpacity.cs` 7、13–17 | `scene/opacity.json`、`tile-pixels.json`、`draw-commands.json`（路徑是 `AppContext.BaseDirectory/../../assets`） | schema `p23-original-opacity-v1`；沒有 SHA | 不用改程式。產品不讀 `maps[]`，拿掉 `path` 沒有影響 |
| `OriginalMapCache.cs` 19 | `maps/manifest.json` | SHA 取自 `integrity.json` 的 `maps/manifest.json` 那一列；schema `p24-map-provider-v1` | integrity 重產即可。新 manifest 少了幾個欄位不影響（見上表「產品是否讀」） |
| `OriginalMapCache.cs` 23 | `maps/<terrain.path>` | SHA 取自 manifest；長度 = 寬 × 高 | manifest 寫新檔的 SHA 即可 |
| `OriginalMapCache.cs` 36–37 | `maps/<blocks[].path>`，或 cache 模式的 `cache/map/0/<name>.s32` | SHA 取自 manifest；SEG 另外寫死 `aad5ff59…101a45` | 改成 provider 直讀時，這裡的根目錄要從 `assets/maps` 換成 `TW_CLIENT_ROOT`，SHA 檢查照舊 |
| `OriginalMapCache.cs` 52、64 | `tiles/<id>.til` | SHA 取自 manifest 的 `tiles[]` | 改成直讀時換成從 Tile.pak 取 entry，SHA 檢查照舊 |
| `OriginalMapCache.cs` 25–28 | `cache/manifest.json`、`cache/original-map0.txt` | schema `p24-original-cache-v1`；terrain 的 SHA 取自 manifest | manifest 寫新 txt 的 SHA。這條路徑（不帶 mapId）在產品原始碼只有 `CacheTests.cs` 使用，連線時走 `maps/` |
| `Portals.cs` 27–30 | `maps/manifest.json` | 同樣取 integrity 的 SHA；schema 相同 | 同 `OriginalMapCache` |
| `Portals.cs` 49–53 | `maps/<blocks[].path>` | manifest 的 SHA 要等於已載入區塊的 SHA；SEG 寫死同一個值 | 改成直讀時根目錄要換 |
| `MapEffects.cs` 23–24 | `maps/effects/manifest.json` | SHA 取自 integrity；schema `p24-map-effects-v1` | integrity 重產即可；`source` 欄位產品不讀 |
| `MapEffects.cs` 27 | `maps/effects/<id>.json` | SHA 取自 effects manifest | 不用 |
| `MapEffects.cs` 158–165 | `objects/magic-<gfx>.json` | SHA 取自 integrity | 屬 T449 |
| `ShipBackdrop.cs` 55–56 | `maps/ship/{Water,Pier1,Pier5}.lac` | 三個 SHA 寫死在程式裡（`36055e0a…`、`2202bf29…`、`c5e61963…`） | 三個值等於 Tile.pak entry 的 SHA，不用改。改成直讀時只換讀檔來源 |
| `ClientEffect6016.cs` 7、20 | `maps/effects/6016.json` | 整檔 SHA 寫死 `825673b1…263383` | **新檔必須逐 byte 相同（鍵順序、無空白、檔尾 CRLF），否則要改 pin 並重做 6016 回歸。** Python 原型可以做到相同 |
| `Renderer.cs` 33 | `cache/screen-channel.bin` | 只檢查長度 2,048 | 屬 T448 |
| launcher `Program.cs` 57–66 | `assets/integrity.json` | schema 與逐檔存在、大小 | T468 處理 |

測試（`PortalChecks.cs`、`MapContinuityChecks.cs`、`DoorChecks.cs`）自己建 fixture 與 integrity，不讀基線。

## 磁碟用量與時間

磁碟（基線實際大小）：

| 部分 | bytes |
|---|---:|
| 原檔切片（`maps/map`、`maps/tiles`、`maps/ship`、`cache/map`、`cache/tiles`） | 1,421,787,336 |
| 衍生檔（terrain、三個 manifest、effects、scene、cache 的小檔） | 275,456,196 |
| 其中產品不讀的 scene 預覽 9 檔 | 30,179,943 |
| 三類合計 | 1,697,243,532 |

- 全部照基線複製：約 1.70 GB。
- 原檔改成 provider 直讀、不產生 scene 預覽：約 245 MB（少 86%）。

時間（本機、Python 原型、單執行緒，實測）：

| 步驟 | 秒 |
|---|---:|
| 讀取並計算基線與客戶端兩邊 12,626 個原檔的 SHA（各 1.42 GB） | 166 |
| 解析 7,807 個 `.s32` 並算 SHA（manifest 需要） | 55 |
| scene 三個執行檔加 opacity | 約 3 |
| scene 預覽 9 檔 | 約 20 |
| 490 個特效（含 17 個 Sprite idx、解 list.spr／spz、解碼 336 個 SPR、輸出 168 MB） | 36 |

推測：C# 準備器全量（不含 SPR 類與文字類）約 1–2 分鐘；若要複製 1.42 GB 原檔，再加 1–3 分鐘（依磁碟）。T470 實跑後才有準確數字。

## 建議：原檔切片用 provider 直讀，不複製

1. 12,626 個原檔全部與客戶端逐 byte 相同，複製只是把 1.42 GB 再放一份。
2. 產品已經有 `ClientArchive`（`UiResourcePins.Tile` 的 pin 就是現在的 Tile.idx／pak）可以用名稱讀 Tile.pak 的 entry；最大的 TIL 167,432 bytes，遠低於它的 8 MiB 上限。map 區塊是散檔，直接開檔即可。
3. manifest 仍然要由準備器產生（區塊清單、每塊 SHA、`noEffectTail`、`verifiedAbsent`、`unsupportedBlocks`、TIL 清單與 SHA），產品載入時照現在的方式逐檔核對 SHA。
4. `cache/` 的 373 個原檔是 `maps/` 的子集，不必另存；cache 模式只有測試在用，可以共用同一個 provider。
5. 三個 LAC 改從 Tile.pak 讀，寫死的 SHA 不變。

要注意：

- `ClientArchive` 第一次讀會算整個 Tile.pak（593 MB）的 SHA，而且鎖的是單一版本。Tile.pak 已經換過一次版而 TIL 內容沒變（見上）。地圖 provider 建議以 manifest 的逐 entry SHA 為準，不要再疊整包的 pin；這一點交 T456 決定。
- manifest 的 `path` 要保留原檔名大小寫（map 4 有 159 個 `.S32`）。
- 準備完成之後客戶端的檔案若被改動，載入時會因 SHA 不符而明確失敗，不會靜默讀到別的內容。

## 推測

- 基線區塊清單排除 12000、12004 是因為研究清冊以 SQL 的地圖表為準；沒有讀清冊的產生程式確認。
- `tiles` 清單沒有另外加入 SEG 區塊（map 6 的 `7fff8000.seg`）用到的 tile id，基線能運作應該是因為那些 id 已經被其他 `.s32` 涵蓋；沒有逐一核對。
- 141 個沒有 metadata 的特效在原版客戶端怎麼處理（不畫，或用別的規則）沒有查。

## 待驗證

- C# 準備器（T456、T457）照本文實作後，要在真客戶端上重跑同樣的比對（T470）。本文的數字都是 Python 原型的結果。
- C# 的 JSON 輸出要做到與 Python `json.dumps` 逐 byte 相同（鍵順序、無空白、`ensure_ascii=False`、CRLF 的位置）才能維持 `6016.json` 的 pin；本張沒有用 C# 試。
- 改成 provider 直讀後，連線跨圖、船運背景、6016 音效的真機回歸。
- 新規則若收進 4472、12000、12004，這三張圖的 terrain 與載入行為沒有基線可比。
- 154 個不可用特效所在的 80 張地圖，產品載入時的實際表現（`MapEffectCatalog.Get` 會丟 `NotSupportedException`）沒有在本張查。

## 重現方式

腳本在 `scratch/t450/`（不入 git）：

- `raw_compare.py`：原檔切片比對，輸出 `raw_compare.json`。
- `derive.py`：`maps/manifest.json`、`cache/manifest.json`、scene 13 檔，輸出 `derive.json`。
- `effects.py`：特效 490 個 id，輸出 `effects.json`。

執行 `python -I -B -X utf8 scratch/t450/<腳本>`，需要 Pillow 與 PyCryptodome。三支都只讀客戶端與基線，只寫 `scratch/t450/`。腳本不保留時，照本文的規則重寫即可。
