# SPR 類素材的 recipe（T449／#1034）

日期：2026-11-13。範圍：`engine/assets` 的 `character`、`objects`、`players`、`shops`、`shadows`、`doors`、`ground`，以及兩個內嵌的 pickup JSON。

本文件只記錄規則、欄位、數量與雜湊，不含像素或幀資料。Python 原型與比對輸出放在 `scratch/t449/`（不入 git）。

## 1. 結論

| 類別 | 幀、blocks、RGB555 像素、bounds、序列（ticks／events） | 判定 | 不屬於 SPR recipe、要由別張任務決定的欄位 |
|---|---|---|---|
| `character` | 96 幀全部相符；96 張 PNG 解碼後 RGBA 相符 | **pass** | 無 |
| `objects` | 52 個外觀檔、8,549 幀全部相符 | **pass** | 要產生哪些 gfx（T447）；`names.json`（Text，T444）；`effectTileType` 是產品規則常數 |
| `players` | 38 個檔、19,409 幀全部相符；15 筆 unavailable 的原因可重現 | **pass** | 職業 gfx 清冊與 `className`（T447） |
| `shops` | 11 個檔、1,336 幀全部相符 | **pass** | 要產生哪些 gfx（T447） |
| `shadows` | 17 個檔、12,125 幀全部相符；frame keys 與 body 完全一致 | **pass** | 無（要先產生 body） |
| `doors` | 1 個檔、2 幀相符 | **pass** | 無 |
| `ground` | 2 個 JSON 相符；2 張 PNG 解碼後 RGBA 相符 | **pass** | 無 |
| pickup（2 個內嵌 JSON） | 13 groups、312 幀、200,628 pixels 全部相符 | **pass**，可由同一 recipe 產生 | 42 筆 profile 的適用清單（跟 players 清冊走） |

- 比對是**全量**，不是抽樣：上表每一類的每個檔、每一幀都比過，沒有不符。
- 原本列為 SQL 來源的時間欄位（`stepMs`、`serverActionMs`、`serverAttackMs`、怪物的 walk／attack／spell `actionMs`）共 309 個值，全部等於「list.spz 該動作的 ticks 總和 × 40 ms」。本張沒有讀 SQL，這只是對基線值的觀察，規則由 T447 定案（§4.3）。
- 有三件事會影響後面的 C# 實作，見 §8：Sprite03–14 的 pak 與產品 pin 不符、空幀的判定、動作的正規表示式。

## 2. 輸入

### 2.1 容器與本機身份

路徑相對於 `TW_CLIENT_ROOT`（本機 `D:\lineage開發\0-客戶端`）。本輪全檔重算 SHA-256。

| 檔案 | 大小 | SHA-256 | 與產品 pin（`ClientArchive.cs`／`GroundSpriteReader.cs`） |
|---|---:|---|---|
| `Sprite.idx` | 2,585,972 | `327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b` | 相符 |
| `Sprite.pak` | 1,904,012,586 | `4dd4e5348fbad2e69bae9cc388be23d7becebe8423ad9f79f393c1457a69ee62` | 相符 |
| `Sprite00`–`Sprite02`、`Sprite15`（idx、pak） | — | 與 pin 相同 | 相符 |
| `Sprite03`–`Sprite14`（idx、pak） | 見 §8.1 | 見 §8.1 | **不符**（12 組都不符） |
| `Text.idx` | 1,049,948 | `43a342ad3929b541096630165c04794192c48359ecbf116b538eaffc49741f6f` | 產品沒有 pin 整檔 |
| `Text.pak` | 26,267,704 | `9516a372c43b269cf1af7adc0ac00abd4163e1235dcd846e34294849616a37ff` | 同上 |

### 2.2 idx／pak 讀法

- Sprite 類：`idx` = `u32 count` + `count × (u32 offset, 20 bytes 名稱, u32 size)`，明文。名稱以 NUL 結尾，查找不分大小寫。同一包內重名就拒絕（本機 17 包都沒有重名）。
- Text：同樣的 28 bytes 列，但列與 payload 都用 DES-ECB 編碼（`TextArchive.cs`），尾端不足 8 bytes 的部分是明文。

### 2.3 動畫表：`list.spz` 與 `list.spr`

| entry | Text.idx 記錄 | Text.pak File Offset | 大小 | 解碼後 SHA-256 |
|---|---:|---:|---:|---|
| `list.spr` | 15937 | 7,864,336 | 4,067,417 | `523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127`（DES 後） |
| `list.spz` | 15938 | 11,931,753 | 4,864,856 | `e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242`（DES 後再做 AES，含開頭 5 bytes `S1223`） |

- 這兩個解碼後的 SHA 與研究階段留下的 `decoded-list.spr`、`inner-list.spz` 相同（`docs/design/portable-delivery.md` 附錄 D）。所以 #97 擔心的「Text 版本不同」對這兩筆沒有影響：容器整檔 SHA 變了，但這兩筆內容沒變。
- `list.spz` 的解法照產品 `Afflictions.cs` 的 `SpriteTypeTable.Load`：DES 解開後開頭是 `S1223`，其後以 AES-128-CBC 解整數個 16 bytes 區塊，剩下的尾巴與前一個密文區塊做 XOR。
- **兩份表不一樣**：`list.spr` 有 11,297 筆，`list.spz` 有 12,230 筆（多 933 筆，gfx 11297 起）；共同的 11,297 筆裡有 326 筆內容不同。
- **recipe 規則**：以 `list.spz` 為準（產品執行期也是先讀 `list.spz`）。本文涵蓋的 63 個 gfx 中，62 個在兩份表內容相同；gfx 11722 只在 `list.spz`。基線的 `entryHash`、`metadataSha256`、`sourceOffset`、`spzOffset` 用 `list.spz`／`list.spr` 重算全部相符。

### 2.4 一筆 entry 的格式

```
#<gfx> <count>[=<sprite>] <名稱> <動作>... <欄位>...
<動作> = <aid>.<名稱>(<directional> <n>,<base>.<frame>:<ticks><events> ...)
```

- entry 範圍：從行首 `#<gfx>` 到下一個行首 `#<數字>` 之前。
- `<events>` 是 token 尾端的原字串，照抄不解讀（基線出現過 `!`、`>`、`<n`、`[n` 及其組合）。
- 本文用到的欄位：`101.shadow(<sprite>)`、`105.clothes(<n> <sprite>...)`。

## 3. 共用規則

### 3.1 gfx → sprite 編號

`#<gfx> <count>=<sprite>` 有等號就用等號後的數字，沒有就是 gfx 本身。63 個 gfx 全部與基線的 `sprite` 欄位相符。

產品的 `GroundSpriteReader` 另外從 `TW13081901.txt`（30,000 筆）取同一個對應。對這 63 個 gfx，兩個來源的結果相同。recipe 建議只用 `list.spz`，少一個輸入檔。

### 3.2 SPR 檔名與幀 key

- 檔名：`<sprite>-<sid>.spr`。
- 動作 token `<base>.<frame>` 在方向 `d`（0–7）對應 `sid = base + d`（`directional` 為 1）或 `sid = base`（為 0）。
- 幀 key：`"<sid>:<frame>"`。一個輸出檔的 `frames` 是「所有用到的 sid，由小到大，每個 SPR 的全部幀」。

### 3.3 主包優先與替代包唯一性

依 `主管/P24-外觀來源版本選擇-20261026.md`：

1. `Sprite.idx`／`Sprite.pak`（主包）有這個檔名就用主包，不看其他包。
2. 主包沒有時，在 `Sprite00`–`Sprite15` 找。所有找到的同名檔 bytes 必須完全相同才採用；有兩種以上內容就列為 unavailable，不挑一個。
3. 這是本產品的選擇，不代表原客戶端的查找順序。

本輪用到 4,765 個 SPR 檔：

| 情況 | 檔數 | 說明 |
|---|---:|---|
| 取自主包，其他包沒有同名檔 | 3,480 | |
| 取自主包，其他包有同名但**內容不同**的檔 | 340 | 未選用。分布：sprite 3215（160）、3216（152）、3225（5）、3213（4）、3227（4）、2786／3221／3474／3476（各 3）、2796（2）、3217（1） |
| 取自替代包 | 945 | 每個檔都只出現在一個替代包，唯一性自然成立 |

替代包的 945 個檔屬於 10 個 sprite：6650、6651、6658、6659、6661、6662、6671、6672（龍騎士與幻術師的 body 與 shadow，分散在全部 16 包）、6325（Sprite00–12）、11722（Sprite13）。

### 3.4 SPR 解碼

照 `GroundSpriteReader.DecodeCore`，全部小端序：

1. 第 1 byte 是 `0xFF` → 索引色：`u8 調色盤筆數`（0 代表 256）、`筆數 × u16 RGB555`、`u8 幀數`。否則第 1 byte 就是幀數，像素直接存 `u16 RGB555`。
2. 每幀：`i16 left, top, right, bottom`、`u16 u, v`、`i16 tile 數`，接著每個 tile `i8 a, i8 b, u8 z, u16 block_index`。
3. `u32 block 數`、`block 數 × u32 offset`、`u32 展開大小`。
4. 每個 block：`u8 sx, sy, w, lines`；每行 `u8 段數`，每段 `u8 skip, u8 run` 加 `run` 個像素。`skip` 必為偶數，`x += skip/2`。
5. 檢查：block offset 等於實際位置、展開大小相符（每個 block 4、每行 1、每段 2、每像素 2）、檔尾沒有多餘 bytes、RGB555 最高位為 0。
6. tile 的畫面位置：`x = 24 × (b + a − floor(a/2))`、`y = 12 × (b − floor(a/2))`，再加 block 內的像素座標。

空幀見 §8.2。

### 3.5 輸出的幀結構（各類共用）

```json
{"key":"<sid>:<frame>","bounds":[left,top,right,bottom],
 "blocks":[{"a":0,"b":0,"z":0,"source_order":0,"source_offset":0,"block_index":0,"pixels":[[x,y,rgb555]]}]}
```

- `source_order`：tile 在該幀的順序。`source_offset`：tile 記錄在 SPR 檔內的 File Offset。`block_index`：SPR 內的 block 編號。`pixels` 的 x、y 是 block 內座標（0–23）。
- 產品只讀 `key`、`bounds`、`a`、`b`、`z`、`source_order`、`pixels`。`source_offset` 與 `block_index` 沒有程式讀，可以不產生。

### 3.6 序列與時間

- 序列每一步：`key`、`ticks`、`events`；部分類別另有 `milliseconds = ticks × 1000 // 24`（整數除法）。
- 動作長度有三種算法，基線都出現過：
  - **floor**：各步 `milliseconds` 相加。
  - **exact**：`ticks 總和 × 1000 / 24`（浮點數）。
  - **×40**：`ticks 總和 × 40`。基線中這些值原本取自 SQL `spr_action`。
- 放招時間點（`castReleaseMs`、`projectileReleaseMs`）：該動作方向 0 序列中，第一個 `events` 含 `!` 的步之前各步 `milliseconds` 的總和。基線 95 個值全部相符。

## 4. 各類別

### 4.1 `character`（103 檔）

**輸入**：`list.spz` 的 gfx 0；主包 `3225-0.spr` 至 `3225-15.spr`（動作 0 walk、動作 3 breath 兩個動作的 8 個方向）。

**輸出與產品讀取情況**：

| 檔案 | schema／內容 | 產品有沒有讀 |
|---|---|---|
| `blocks.json` | `p06-spr-blocks-v1`；96 幀，每幀 `key`、`bounds`、`anchor`（= `[-left,-top]`）、`source_sha256`、`blocks` | **有**（`Assets.cs:44–45`，讀 `key`、`bounds`、blocks） |
| `animation.json` | `p04-character-v1`；`mapping` 16 筆、`frames` 96 筆、`actions`、`canvas`、`metadata` | **有**（`Assets.cs:46–51`，只讀 `mapping[].action／direction／sequence[].key`） |
| `frames/3225-<sid>-<frame>.png`（96） | 各幀 RGBA | 沒有 |
| `character-entry.txt` | gfx 0 的 entry 原文 | 沒有 |
| `canvas-checks.json` | 畫布聯集與 192 筆不透明像素數 | 沒有 |
| `contact-sheet.png`、`walk-8directions.gif`、`breath-8directions.gif` | 預覽圖 | 沒有 |

「沒有」是指 `engine/` 下沒有任何程式開啟這些檔；它們目前只因為列在 `integrity.json`，啟動時被 `Assets.VerifyIntegrity` 檢查存在與 SHA。

**比對結果**（全量）：

- `blocks.json`：96 幀的 bounds、blocks、anchor、`source_sha256` 全部相符。
- `animation.json`：96 幀的 bounds、寬高、anchor、`header_offset`、`reserved`、`rgba_sha256`、`source_record`、`source_sha256` 相符；16 筆 mapping 的 key、ticks、`base_duration_ms`、events 相符；`actions[].raw` 與 `source_byte_offset`、`metadata` 三個欄位、`canvas` 相符。
- 96 張 PNG 解碼後的 RGBA 與重算結果逐 byte 相同（PNG 檔本身的 bytes 取決於編碼器，沒有比）。
- `character-entry.txt` 與 `list.spz` 的 gfx 0 entry 逐 byte 相同。
- `canvas-checks.json` 的 `canvas` 相符。

**判定：pass。**

**建議**：新的 recipe 只產生 `blocks.json` 與 `animation.json`（而且只需要 `mapping`）。其餘 101 個檔（96 PNG、2 GIF、contact sheet、canvas checks、entry txt）不再產生。GIF 與 contact sheet 沒有比對內容，也不需要。

### 4.2 `players`（39 檔）

**輸入**：`list.spz` 的 gfx entry；每個 (gfx, weapon) 取 7 個動作：

| 標籤 | 動作編號 |
|---|---|
| walk／attack／damage／breath | weapon＋0／＋1／＋2／＋3 |
| die／spellDir／spellNoDir | 8／18／19 |

SPR 依 §3.3 選包。任何一個動作不存在、或 SPR 有不透明像素落在幀 bounds 之外，整個 profile 列為 unavailable。

**輸出 schema**（38 個檔，兩種鍵順序、欄位相同）：

| 欄位 | 來源 |
|---|---|
| `gfx`、`weapon`、`sprite` | 清冊與 §3.1 |
| `entryHash` | entry 原文的 SHA-256 |
| `sequences[]`：`action`、`direction`、`sequence[]`（`key`、`ticks`、`events`、`milliseconds`） | §3.6。walk 的步沒有 `milliseconds` |
| `frames[]` | §3.5 |
| `actionMs` | walk 是 ×40；其餘 6 個是 floor |
| `stepMs`、`serverActionMs`（walk、attack、spellDir、spellNoDir） | ×40（原 SQL） |
| `castReleaseMs`、`projectileReleaseMs`（只有 weapon 20，其餘為 null） | §3.6 |

`manifest.json`（`p24-player-profiles-v1`）：`profiles` 42 筆（其中 gfx 0 的 4 筆指向 `objects/0*.json`，標 `inherited`）、`unavailable` 15 筆。每筆的 `className` 來自伺服器 JAR 的常數名稱，產品沒有讀。

**比對結果**（全量）：38 檔、19,409 幀、13,695,649 pixels。

- 幀與 blocks：全部相符。序列：全部相符。`entryHash`：38／38。
- `actionMs`：228 個值符合 floor；38 個 walk 值符合 ×40。`serverActionMs` 152 個值、`stepMs` 38 個值全部符合 ×40。
- `castReleaseMs`、`projectileReleaseMs`：86 個值全部相符。
- manifest 的 `sprite`、`frames`、檔案 SHA 與各檔一致。
- 15 筆 unavailable 逐筆重跑：12 筆是「缺少動作 `<weapon>`／walk」，3 筆（gfx 138，weapon 0／4／20）是「不透明像素落在 bounds 外」，原因類別與基線相同。

**判定：pass。** (gfx, weapon) 的清冊與 `className` 由 T447 處理。

### 4.3 `objects`（54 檔）

54 檔 = 52 個外觀檔 + `manifest.json`（只有一個 15 筆的 gfx 清單）+ `names.json`（取自 Text 的 `desc-c.tbl`，屬於 T444）。

**輸入**：同 players。動作編號因檔案種類而異：

| 檔案種類 | 檔數 | 動作 |
|---|---:|---|
| `0.json`、`0-weapon-4／20／50.json` | 4 | 同 players 的 7 個動作 |
| 怪物與 NPC（`<gfx>.json`、`world-945.json`） | 38 | walk 0、attack 1、damage 2、breath 3、die 8、spellDir 18、spellNoDir 19 中 entry 有的那幾個。`1094.json` 把動作 8（原名 fly）標成 die |
| `magic-<gfx>.json` | 8 | 動作 0，標籤 fly／impact／heal／effect |
| `projectile-66.json` | 1 | 動作 0（fly） |
| `world-234.json` | 1 | 動作 3（breath），合成幀，見下 |

**全部 schema 欄位盤點**（52 個檔共 12 種鍵組合）：

| 欄位 | 出現檔數 | 來源 |
|---|---:|---|
| `gfx`、`sprite`、`sequences`、`frames` | 52 | 清冊、§3.1、§3.6、§3.5 |
| `stepMs` | 51（`projectile-66` 沒有） | 有 walk 的是 walk ×40（`world-234` 沒有輸出 walk 序列，但一樣取 entry 的 walk ×40）；`85`、`1094` 沒有 walk，取 breath 長度的整數；magic 固定為 1 |
| `actionMs` | 42 | 見下方「時間欄位」 |
| `entryHash` | 18 | entry 原文的 SHA-256 |
| `sourceOffset`、`spzOffset` | 14 | entry 在 `list.spr`、`list.spz` 解碼後內容中的位移 |
| `sourceActions` | 24 | 每個動作的編號、標籤、`metadataTicks`、`durationMs`，以及 `sql`（原 SQL 的 framecount、framerate，或 null） |
| `serverSpeedSource` | 9 | 原 SQL 的 walk framecount、framerate |
| `weapon`、`castReleaseMs` | 4 | gfx 0 的四個檔 |
| `serverAttackMs`、`projectileReleaseMs` | 1（`0-weapon-20`） | ×40、§3.6 |
| `effectTileType`、`eventPolicy` | 5（magic 189／191／197／221／2165） | `effectTileType` 固定 18，是產品的混色選擇，不在 SPR 或 list 裡；`eventPolicy` 是說明文字 |
| `entry` | 1（`projectile-66`） | entry 原文 |
| `sourceComponents`、`sourcePolicy` | 1（`world-234`） | `105.clothes` 的 sprite 清單、說明文字 |
| 序列的 `totalTicks` | 18 | ticks 總和 |
| 步的 `milliseconds` | 12 | §3.6 |
| 幀的 `sourceEmpty`、`sourceHeaderOffset` | 1（`1094`） | 是否空幀、幀表頭在 SPR 內的 File Offset |
| block 的 `sourceSprite`、`sourceSid`、`sourceFrame` | 1（`world-234`） | 這個 block 來自哪個 SPR 的哪一幀 |

產品 `ObjectAppearance` 只讀 `gfx`、`sprite`、`stepMs`、`actionMs`、`effectTileType`、`frames`、`sequences`（步的 `key`、`ticks`、`milliseconds`、`events`）。其餘欄位是來源紀錄。

**特例**：

- `world-234.json`：gfx 234 的 body 是 sprite 200，另有 `105.clothes(6 201 204 206 210 208 211)`。每一幀是 7 個 sprite 同一個 `sid:frame` 的 blocks 依序串接（200 在前，clothes 照原順序），`bounds` 取 7 幀的聯集，`source_order` 重新連號。
- `magic-167.json`：方向 `d` 用 `sid = base + (d mod 4)`。entry 表頭是 `#167 4`，客戶端只有 `167-0` 至 `167-3` 四個檔。基線是寫死 gfx 167；「方向數取表頭的 count」是推測，沒有驗證。
- `magic-11722.json`：entry 只在 `list.spz`；SPR 取自 Sprite13（主包沒有）。
- `1094.json`：`1094-1.spr` 的最後一幀是空幀，見 §8.2。

**時間欄位**：

- `actionMs` 共 182 個值：24 個符合 floor、80 個符合 exact、78 個符合 ×40（ticks 總和是 3 的倍數時 floor 與 exact 相同，這裡歸在 floor）。規律：walk、attack、spellDir、spellNoDir 用 ×40；怪物的 breath、damage、die 用 exact；magic 用 floor。gfx 0 的四個檔三種都有：spell 用 floor，`0-weapon-20` 的 attack、damage、breath、die 用 floor，其餘照前述規律。逐值分類在 `scratch/t449/results.json`。
- `stepMs`、`serverAttackMs` 與上述 ×40 的值，加上 players 的 `serverActionMs`，合計 309 個值，**沒有一個例外**。也就是說，對基線裡有這些欄位的檔案，SQL `spr_action` 的 `framecount × 40 × 24 / framerate` 等於 `list.spz` 的 ticks 總和 × 40（基線 `sourceActions[].sql` 與 `serverSpeedSource` 記的 framerate 都是 24，framecount 都等於 `metadataTicks`）。
- 這是對基線值的觀察，本張沒有開 SQL。能否把「伺服器時間 = ticks × 40」當成通則，由 T447 判定。

**比對結果**（全量）：52 檔、8,549 幀（51 個一般檔 8,501 幀、6,645,446 pixels；`world-234` 48 幀、1,770 個 blocks）。幀、blocks、序列全部相符；`entryHash` 18／18；`sourceOffset`／`spzOffset` 14／14；`castReleaseMs` 等 9 個值相符。

**判定：pass。** 要產生哪些 gfx（`manifest.json` 的 15 個、`encountered-monsters.json` 的 19 個、`world-appearances.json` 的 6 個、magic 與 projectile 的清單）是清冊問題，屬於 T447。

### 4.4 `shops`（12 檔）

**輸入**：`list.spz` 的 gfx entry 中的動作 70（`70.act shop`，必須 `directional = 1`，token 不帶 events）；SPR 依 §3.3。

**輸出 schema**：`gfx`、`sprite`、`sourceAction`（70）、`stepMs` 與 `actionMs.shop`（都是 floor）、`sequences`（步有 `key`、`ticks`、`milliseconds`）、`frames`。`manifest.json`（`p24-original-shop-appearance-v1`）每筆有 `gfx`、`sprite`、`path`、`sha256`、`frames`、`durationMs`、`metadataSha256`。

**比對結果**（全量）：11 檔（gfx 0、37、61、138、734、1186、2786、6650、6658、6661、6671）、1,336 幀、879,740 pixels。幀、序列、時間全部相符；manifest 的 `metadataSha256`、`frames`、`durationMs`、`sprite`、檔案 SHA 全部相符。

**判定：pass。**

### 4.5 `shadows`（18 檔）

**輸入**：

1. 已產生的 body profile（players、objects 的檔）。
2. 該 gfx 的 `101.shadow(<N>)`：N 是 shadow 的 sprite 編號。
3. `<N>-<sid>.spr`，依 §3.3 選包。

**`101.shadow` 與 body frame keys 的關係**：

- shadow sprite 的檔案編號與幀編號和 body **一一對應**：body 畫 `<body sprite>-<sid>.spr` 的第 `frame` 幀時，影子畫 `<N>-<sid>.spr` 的第 `frame` 幀。key 用同一個字串 `"<sid>:<frame>"`。
- 一個 gfx 需要的 keys = 它所有 body profile 的 `sequences` 用到的 key 的聯集（只取序列實際引用的幀，不是 body 的全部幀）。
- 輸出的 `frames` 依 (sid, frame) 由小到大排。17 個檔的 key 集合都與這個聯集完全相同，沒有多也沒有少。
- 17 個 gfx 的 N 都是 body sprite ＋ 1，但 gfx 234 與 695 共用 body 200 與 shadow 225。規則以 `101.shadow` 的值為準，不要用「＋1」推算。
- 產品以 (gfx, bodySprite) 找檔，再用 body 幀的 key 取影子幀（`PlayerShadows.cs`）。

**輸出 schema**：`schema`（`p24-original-shadow-frames-v1`）、`gfx`、`bodySprite`、`shadowSprite`、`flags`（固定 5，產品規則常數）、`frames`。`manifest.json`（`p24-original-shadows-v1`）每筆有 `sourceBodyProfiles`、`actions`、`requiredKeys`、`providedKeys`。

**比對結果**（全量）：17 檔、12,125 幀、2,507,105 pixels。

| gfx | body sprite | shadow sprite | keys | body profile 數 |
|---:|---:|---:|---:|---:|
| 0 | 3225 | 3226 | 1,152 | 5 |
| 1 | 3227 | 3228 | 1,040 | 4 |
| 37 | 3474 | 3475 | 824 | 3 |
| 48 | 3215 | 3216 | 1,232 | 4 |
| 61 | 3213 | 3214 | 1,088 | 4 |
| 98 | 98 | 99 | 109 | 1 |
| 234 | 200 | 225 | 48 | 1 |
| 695 | 200 | 225 | 80 | 1 |
| 734 | 3476 | 3477 | 832 | 3 |
| 792 | 877 | 878 | 80 | 1 |
| 1186 | 3221 | 3222 | 816 | 3 |
| 2786 | 2786 | 2787 | 832 | 3 |
| 2796 | 2796 | 2797 | 880 | 3 |
| 6650 | 6650 | 6651 | 696 | 2 |
| 6658 | 6658 | 6659 | 888 | 3 |
| 6661 | 6661 | 6662 | 880 | 3 |
| 6671 | 6671 | 6672 | 648 | 2 |

幀與 blocks 全部相符；`shadowSprite` 與 `101.shadow` 17／17 相符；`requiredKeys` = `providedKeys` = 實際幀數。

**判定：pass。** 準備器要先產生 body，再產生 shadow。

### 4.6 `doors`（2 檔）

**輸入**：`list.spz` 的 gfx 2346：`28.open(0 1,0.1:4[8)`、`29.close(0 1,0.0:4[9)`；主包 `2346-0.spr`（idx 記錄 24229，File Offset 830,373,336，7,243 bytes，SHA-256 開頭 `41aeb6518179064b`）。

**輸出 schema**：`gfx`、`sprite`、`stepMs`（固定 167，產品規則常數）、`sourceActions`（`[28,29]`）、`nonDirectional`（true）、`sequences`（open、close 各 8 個方向，內容相同）、`frames`（2 幀）。

**比對結果**：2 幀、4,648 pixels、16 筆序列全部相符。

**判定：pass。**

### 4.7 `ground`（4 檔）

**輸入**：主包 `36-0.spr`（idx 記錄 48732，File Offset 971,303,715，244 bytes，SHA-256 開頭 `1734424f17adb848`，1 幀）與 `66-0.spr`（記錄 85892，File Offset 962,888,499，1,056 bytes，SHA-256 開頭 `4795b0d2150d7d07`，3 幀）。兩個都取第 0 幀。

**輸出**：

- `<gfx>.json`：`gfx`、`sprite`、`key`（`"0:0"`）、`bounds`、`blocks`（§3.5 的 block 結構，沒有外層 `frames`）。
- `<gfx>.png`：RGBA。畫布是**不透明像素的最小外框**，不是幀的 `bounds`（gfx 36 的 bounds 是 12×10，PNG 是 11×9）。顏色用 `v × 8 + (v >> 2)` 展開。

**比對結果**：兩個 JSON 的 bounds 與 blocks 相符；兩張 PNG 解碼後的尺寸與 RGBA 逐 byte 相符。

**判定：pass。** 產品已有 `GroundSpriteReader` 在執行期直接讀這兩個 SPR（#190），這一類可以不必預先產生。

## 5. pickup 兩個內嵌 JSON

檔案：`engine/source/Resources/pickup-own-base15-gfx0-weapon0.json`（239,766 bytes，24 幀）與 `pickup-own-base15-profile-extension.json`（2,473,547 bytes，12 groups／288 幀）。

**recipe**：與 §4.2 相同，只是取動作 15（`15.get`，4 步）：

- 每個 group 是一個 gfx。8 個方向各 4 步，`key = "<base + d>:<frame>"`。
- 第 0 步標 `storage: "base-profile"`：這一幀已經在該 gfx 的 body profile 裡，不內嵌。第 1–3 步標 `"embedded"`，共 8 × 3 = 24 幀。
- `frames` 的順序：方向 0–7，每個方向依第 1、2、3 步。`frameOrder` 是 `[1,2,3,0]`。
- block 只留 `a`、`b`、`z`、`source_order`、`pixels`。
- 每步的 `ticks`、`events`、`metadataIndex` 來自 token。

**能否由同一 recipe 產生：可以。** 13 個 group 的 312 幀（200,628 pixels）與 104 筆序列全部相符。42 筆 profile 記的 `baseProfileSha256` 也都等於基線對應檔的 SHA。

| group（gfx） | sprite | `15.get` 動作（list.spz） | 幀 | pixels | 幀比對 | 序列比對 | container |
|---:|---:|---|---:|---:|---|---|---|
| 0（base） | 3225 | `15.get(1 4,0.0:1 88.0:1 88.1:4<206 88.2:4)` | 24 | 17916 | 相符 | 相符 | 主包 Sprite |
| 1（extension） | 3227 | `15.get(1 4,0.0:1 88.0:1 88.1:4<206 88.2:4)` | 24 | 11419 | 相符 | 相符 | 主包 Sprite |
| 37（extension） | 3474 | `15.get(1 4,0.0:1 24.0:1 24.1:4<206 24.2:4)` | 24 | 10922 | 相符 | 相符 | 主包 Sprite |
| 48（extension） | 3215 | `15.get(1 4,0.0:1 24.0:1 24.1:4<206 24.2:4)` | 24 | 13222 | 相符 | 相符 | 主包 Sprite |
| 61（extension） | 3213 | `15.get(1 4,0.0:1 24.0:1 24.1:4<206 24.2:4)` | 24 | 16124 | 相符 | 相符 | 主包 Sprite |
| 734（extension） | 3476 | `15.get(1 4,0.0:1 40.0:1 40.1:4<206 40.2:4)` | 24 | 21732 | 相符 | 相符 | 主包 Sprite |
| 1186（extension） | 3221 | `15.get(1 4,0.0:1 48.0:1 48.1:4<206 48.2:4)` | 24 | 16571 | 相符 | 相符 | 主包 Sprite |
| 2786（extension） | 2786 | `15.get(1 4,8.0:1 64.0:1 64.1:4<206 64.2:4)` | 24 | 16711 | 相符 | 相符 | 主包 Sprite |
| 2796（extension） | 2796 | `15.get(1 4,8.0:1 48.1:1 48.2:4<206 48.3:4)` | 24 | 13390 | 相符 | 相符 | 主包 Sprite |
| 6650（extension） | 6650 | `15.get(1 4,8.0:1 24.0:1 24.1:4<206 24.2:4)` | 24 | 14113 | 相符 | 相符 | 替代包（見下表） |
| 6658（extension） | 6658 | `15.get(1 4,8.0:1 56.0:1 56.1:4<206 56.2:4)` | 24 | 18677 | 相符 | 相符 | 替代包（見下表） |
| 6661（extension） | 6661 | `15.get(1 4,8.0:1 24.0:1 24.1:4<206 24.2:4)` | 24 | 13291 | 相符 | 相符 | 替代包（見下表） |
| 6671（extension） | 6671 | `15.get(1 4,8.0:1 24.0:1 24.1:4<206 24.2:4)` | 24 | 16540 | 相符 | 相符 | 替代包（見下表） |

| gfx | entry | container | idx record | pak File Offset | size | payload SHA-256 | 其他包同名 |
|---:|---|---|---:|---:|---:|---|---|
| 0 | `3225-88.spr` | Sprite | 42729 | 301494636 | 4426 | `f91603374d529ec4961e918bda0cb37fab46e5871134520367789a862544e682` | 無 |
| 0 | `3225-89.spr` | Sprite | 42730 | 301490615 | 4021 | `f60a1f43838f3a305c4554676795c9c5187341ca48909c26a0367d1772ee395f` | 無 |
| 0 | `3225-90.spr` | Sprite | 42732 | 301475450 | 4519 | `ac0d4f7dddb2ddd39322c5d1246c1e86a4465fbbae71f0d026e54a4429172d83` | 無 |
| 0 | `3225-91.spr` | Sprite | 42733 | 301470883 | 4567 | `6428b98762e407ae2704b172c2421033dec752d945620ecde78f0b8a88825b9d` | 無 |
| 0 | `3225-92.spr` | Sprite | 42734 | 301466834 | 4049 | `2d2d2f4da44bbd3ab035df21adc6cb7a9aeffea62a071b393cac7765cff5e9e2` | 無 |
| 0 | `3225-93.spr` | Sprite | 42735 | 301463607 | 3227 | `08a1af48b6e29acf0529cb8e69deba545efdb3b16c6225c3460e3d5b754214c4` | 無 |
| 0 | `3225-94.spr` | Sprite | 42736 | 301459704 | 3903 | `0a619187df5fec11261db2a582eee6b6632da4836157dd94ebce62c4a05c6885` | 無 |
| 0 | `3225-95.spr` | Sprite | 42737 | 301455236 | 4468 | `be83e7f0986c78eb02f0a6c406c1a739db79126e6d6081f5c31304b3a05e2987` | 無 |
| 1 | `3227-88.spr` | Sprite | 43337 | 298399245 | 3383 | `86eec43ac3713996e8a50fc46d35657995277bfe0d5af26c2a504d5d770aaaac` | 無 |
| 1 | `3227-89.spr` | Sprite | 43338 | 298395990 | 3255 | `c48aa019de975b886c63d6baf3966c1765b2402d19c5d935fe3246c828111369` | 無 |
| 1 | `3227-90.spr` | Sprite | 43340 | 298380310 | 3341 | `50e0a5a8853c055f52b051c87a47b2e767442a6f78d4b721d8b410029154b508` | 無 |
| 1 | `3227-91.spr` | Sprite | 43341 | 298376892 | 3418 | `f832a6ceb8c15a52ae97af06233489eb0d6162ae34c06600b4c635550e244aae` | 無 |
| 1 | `3227-92.spr` | Sprite | 43342 | 298373803 | 3089 | `285c3a81c4be6ceecf5a1f77fa8ea6019da63725b76926c012c0938e55be9d06` | 無 |
| 1 | `3227-93.spr` | Sprite | 43343 | 298371104 | 2699 | `118f65e14b4f1e93d2a9d775ba0753dd495f9063318cf174fe8ab3babbbc67ab` | 無 |
| 1 | `3227-94.spr` | Sprite | 43344 | 298368144 | 2960 | `c66b48ef19f3536c58b5eb951537d95540ef3af32d211787f06f4c8e3225b78a` | 無 |
| 1 | `3227-95.spr` | Sprite | 43345 | 298364871 | 3273 | `d93162cb4a4c01a389e53da00af15871e2bc8bbcf01a2bc36fbf80db429c0144` | 無 |
| 37 | `3474-24.spr` | Sprite | 46072 | 296753271 | 3218 | `a881b4beb886eb304f31b8054bfeb1d72c97025475729dff2334796f0a204036` | 無 |
| 37 | `3474-25.spr` | Sprite | 46083 | 296634118 | 2953 | `ef9e15c349306220e6b33946d44e448eeb5ccdd25e827bf055f78732f3520c13` | 無 |
| 37 | `3474-26.spr` | Sprite | 46094 | 296552540 | 3098 | `958af7b365189bc7c312bedf413d6e63de657b208e5e646ab095750a33b17d4e` | 無 |
| 37 | `3474-27.spr` | Sprite | 46099 | 296537769 | 3259 | `c63339b58a529f617907e357112b7d4c0131c1c4d9c4753122d7f5e54d7b1b97` | 無 |
| 37 | `3474-28.spr` | Sprite | 46100 | 296534715 | 3054 | `efc1ab3dede8c0b4430c80b69c9a14cc5e377dfd031efeff4be631e2af63cd13` | 無 |
| 37 | `3474-29.spr` | Sprite | 46101 | 296532163 | 2552 | `781b369ced9fdb78ca695f51131fdda7b454a24fab5062b2c554877fadad78ca` | 無 |
| 37 | `3474-30.spr` | Sprite | 46103 | 296525172 | 2907 | `f99f96fe9936f7d8899edcde6eb035fd3e7a98eb970210a9e7fd7ecaeea2fbfc` | 無 |
| 37 | `3474-31.spr` | Sprite | 46104 | 296521970 | 3202 | `0c156ab25b6e57987086dd1257655adb8e1fc9d6c0cd11d840174d58f5b7370a` | 無 |
| 48 | `3215-24.spr` | Sprite | 40832 | 310502758 | 3852 | `0312b8ce2ddb9cf41c49bb40ae6d108a12066ee6e4cf6daf5690f48b90e9833a` | 有，內容不同（未選用） |
| 48 | `3215-25.spr` | Sprite | 40843 | 310463923 | 3298 | `fc35fb7cde726d14f4b1a20388506bb10cb9992bea03b3aa144474b39f572add` | 有，內容不同（未選用） |
| 48 | `3215-26.spr` | Sprite | 40854 | 310362127 | 3349 | `5f0aeea8f54f98abcb2141bfdde353ece58374119f3647ab66488efdeb17a8ce` | 有，內容不同（未選用） |
| 48 | `3215-27.spr` | Sprite | 40865 | 310204616 | 3531 | `9e9d17672465744cb5bf51ac6408709aae40daf82a5d525aa9714f7777c2a048` | 有，內容不同（未選用） |
| 48 | `3215-28.spr` | Sprite | 40876 | 310147372 | 3199 | `8b9b43750ab0a81efd84135a09cd4fe27c00ad2293765c1f16f0411d747d8572` | 有，內容不同（未選用） |
| 48 | `3215-29.spr` | Sprite | 40887 | 310057708 | 2956 | `7720cef7608a1256e8350439d2550753f85b4f2b2de2984ab928fc65766e9698` | 有，內容不同（未選用） |
| 48 | `3215-30.spr` | Sprite | 40899 | 309884100 | 3250 | `f680c1ca08e4a3a18c65db469222d98a48934f0d1b9801bdeedbec247b9271c2` | 有，內容不同（未選用） |
| 48 | `3215-31.spr` | Sprite | 40910 | 309807676 | 3563 | `44afe00aa0699d85dae53011b06b8a6e974b552f15e5829a5cb77b7ebc47979f` | 有，內容不同（未選用） |
| 61 | `3213-24.spr` | Sprite | 40208 | 314096768 | 3921 | `86cb4df631ea7fae382f1cce7bbc50002476175e3b5f7b44319da99b9d7d42cc` | 無 |
| 61 | `3213-25.spr` | Sprite | 40219 | 313910533 | 4136 | `7e8bf0c572da873f8e22ba5814dcfc213ce973f8fc0b08564b7d7b9255d36328` | 無 |
| 61 | `3213-26.spr` | Sprite | 40230 | 313796253 | 4323 | `9a983a9eb6898a048821807aa66ea65a6cc657e35fc17d622e9b388e8ea4267d` | 無 |
| 61 | `3213-27.spr` | Sprite | 40241 | 313738449 | 4142 | `8a36ad79bd8bcde594535101a7cbf9ffbb60f2cce75352b3c14a375cdb023d46` | 無 |
| 61 | `3213-28.spr` | Sprite | 40252 | 313541547 | 3525 | `0a0f9c4401d94051eb86fd5e057cbcdf54d65d1d8dd82b83992172003dbb0fa2` | 無 |
| 61 | `3213-29.spr` | Sprite | 40263 | 313442398 | 3268 | `91e77e75934e4c6ba544b8277d98322d1b3e0314dec7922713642a490025b243` | 無 |
| 61 | `3213-30.spr` | Sprite | 40275 | 313411895 | 3651 | `170165912d04cf92734eea2b13af2bb99afa3371e9d109da9bc66030968741b6` | 無 |
| 61 | `3213-31.spr` | Sprite | 40286 | 313308161 | 4083 | `eba72c2b12e95919ee07865e864c96bb0fcbac3884e093545faae35e8492e632` | 無 |
| 734 | `3476-40.spr` | Sprite | 46682 | 293073671 | 5242 | `819bf309cd64d5df161c62557cdf158463ec4d2de6f58a7f90cbe09cec8af3e6` | 無 |
| 734 | `3476-41.spr` | Sprite | 46683 | 293069474 | 4197 | `03781256c1f5c4d425ed07d17318192032ec7da6fbd0f22fc5ccbb90c9305f4b` | 無 |
| 734 | `3476-42.spr` | Sprite | 46684 | 293064564 | 4910 | `899e482a37b8e9d79580e240518be6ac5b355bbd6da2f07c81417ff751252aea` | 無 |
| 734 | `3476-43.spr` | Sprite | 46685 | 293059322 | 5242 | `1241b4c35f8175f726efbdbf151c69a88212787bf72fb323c83203d9307a9c7f` | 無 |
| 734 | `3476-44.spr` | Sprite | 46686 | 293054783 | 4539 | `3e426b09d307718f983f7bebcf5b999da4bed95327b991949836a49967ecb747` | 無 |
| 734 | `3476-45.spr` | Sprite | 46687 | 293051066 | 3717 | `cd76c6ddf0b0fe8c2e3ac3c139d731a1f013124d0bc2ebd21e0f95285ed81b21` | 無 |
| 734 | `3476-46.spr` | Sprite | 46688 | 293046332 | 4734 | `2ecf778162ddeaddebe3592be246b1cfb90e08591318db46bc534480281538a0` | 無 |
| 734 | `3476-47.spr` | Sprite | 46689 | 293040663 | 5669 | `1db6f03afd3a1781e02d4eb01f2afc854dec80faf8fcc28b0c64a747cee5ef77` | 無 |
| 1186 | `3221-48.spr` | Sprite | 42077 | 304653810 | 3953 | `f4b627d6d20f17936e9db65f837df0a30753cbf9c625c9a5ed1f155c30b41eed` | 無 |
| 1186 | `3221-49.spr` | Sprite | 42078 | 304649996 | 3814 | `37a9917dc3af0b0abdbb8d62478de01e98a7bc03ca824ada60bcde69df105e22` | 無 |
| 1186 | `3221-50.spr` | Sprite | 42080 | 304635373 | 4480 | `e464b3c29b2ae27ec8f5c27d60c1aa6e5209b4af639602ea753cb9fc05b31649` | 無 |
| 1186 | `3221-51.spr` | Sprite | 42081 | 304630791 | 4582 | `25f8d11f6823c2451c7f3fecb0d3d06afaa91078a40145a41a6040e4d1678e2e` | 無 |
| 1186 | `3221-52.spr` | Sprite | 42082 | 304627163 | 3628 | `7dddbebecae94179292347d00f71b8c5ac06d503588d156014a0b5bf673332f2` | 無 |
| 1186 | `3221-53.spr` | Sprite | 42083 | 304624408 | 2755 | `cc6242601ab39925c5c6fb9146462e119d0efab4559def8decbb6404d5fa85a1` | 無 |
| 1186 | `3221-54.spr` | Sprite | 42084 | 304620527 | 3881 | `c549e76e324e4f90a6ba89a54e06e291d72b9bee2a92f6134c7b7dbabde114ed` | 無 |
| 1186 | `3221-55.spr` | Sprite | 42085 | 304616217 | 4310 | `3f65d8b6df3190ef2637d64d0a48ce4b0198e301e27f7a5c1cecc05639a7f376` | 無 |
| 2786 | `2786-64.spr` | Sprite | 31833 | 241338868 | 4030 | `1f9ebba56de226a229a4c40745a64c065011dc0fec431fe26d747598df816f2b` | 無 |
| 2786 | `2786-65.spr` | Sprite | 31834 | 241334791 | 4077 | `565520fa5b3128502ddb31e1b4a696442c7db32109982194653346967f9929b3` | 無 |
| 2786 | `2786-66.spr` | Sprite | 31835 | 241330562 | 4229 | `61aa3e5ae69389bd49fadcc9c6b83a5396603721bf871a4aa5f58e9f6e0082cc` | 無 |
| 2786 | `2786-67.spr` | Sprite | 31836 | 241326113 | 4449 | `ba64ed6c18bc5de77b40f6eabaaba5a847f4d93634dd1d5b83d4023327be6386` | 無 |
| 2786 | `2786-68.spr` | Sprite | 31837 | 241322198 | 3915 | `a076b9796bb83ae8637bd00c4793c67cf95bf81c3fa673aef30dd481a3d399f0` | 無 |
| 2786 | `2786-69.spr` | Sprite | 31838 | 241318767 | 3431 | `51fa8b8b75d7415f3bcffd889d8145ef6079e46156bf4985389f8efda1516ca4` | 無 |
| 2786 | `2786-70.spr` | Sprite | 31840 | 241314902 | 3865 | `a207de89e048d552e63365b8297399d40b52b894de21161a177aa178b66e7adb` | 無 |
| 2786 | `2786-71.spr` | Sprite | 31841 | 241310788 | 4114 | `8ee07e3619715124d0f6f74a54cfb0d4632932c44d6b698835874d37c07252b4` | 無 |
| 2796 | `2796-48.spr` | Sprite | 32900 | 576452822 | 5989 | `1b63bc799964e8eed0b1fd777963939760ddbb1027409c165cdc52199d4ece1e` | 無 |
| 2796 | `2796-49.spr` | Sprite | 32901 | 576446951 | 5871 | `5f351b1f695750a5afe5c397f65b860d442cafbfa39f2ff0aaa627f421a79524` | 無 |
| 2796 | `2796-50.spr` | Sprite | 32903 | 576431495 | 5607 | `3e1e0dedc201090f7421fd68bb9021b5f97343e087dec4b7b685f4b196acca98` | 無 |
| 2796 | `2796-51.spr` | Sprite | 32904 | 576425577 | 5918 | `eba26369ff5ae2505788387d0395645357f708e56224e6608766c2b6dce0a3e7` | 無 |
| 2796 | `2796-52.spr` | Sprite | 32905 | 576420164 | 5413 | `ef01dd83fd6fc6646552f52b9888d73af8d27effdbf321a537941e8ed4661e80` | 無 |
| 2796 | `2796-53.spr` | Sprite | 32906 | 576415273 | 4891 | `21c00442f1a454178b686a3392f1c4a225de56a927f8dd28d2d1d60c9812367c` | 無 |
| 2796 | `2796-54.spr` | Sprite | 32907 | 576410054 | 5219 | `adf9bc5e84f801db62d0e12f3092b8e32e8f24aa309054fcc742dc2a5ed6afaf` | 無 |
| 2796 | `2796-55.spr` | Sprite | 32908 | 576404302 | 5752 | `8849688a74e0a31080d92ef8ce7896cb0a211289539bf7ff0ef963928d65ba78` | 無 |
| 6650 | `6650-24.spr` | Sprite07 | 9304 | 281561065 | 4904 | `e07665cdcf0b63c704b33f7b47dcba768ef975fd713655aa3eab2379a6ffefd4` | 無 |
| 6650 | `6650-25.spr` | Sprite08 | 9233 | 226465469 | 5314 | `922c3c3aff3cef4545dfa1dc486401fe0b9e08110af6c26b53d7d762fbd982a3` | 無 |
| 6650 | `6650-26.spr` | Sprite09 | 9132 | 218390401 | 5893 | `0c556f955ed78389fc4fd0d122e0b1cf694566e937504dfc74b18bb7daff7075` | 無 |
| 6650 | `6650-27.spr` | Sprite10 | 8945 | 216721406 | 5359 | `359913e53ae873e4eacc4b3e15a99b0f9960f20b6e5f151226a88f8affd866c0` | 無 |
| 6650 | `6650-28.spr` | Sprite11 | 8748 | 195727590 | 4267 | `9708562dc9ea39ee1ccdc780b713b4bf772a008c721825856cc185c3a17954c1` | 無 |
| 6650 | `6650-29.spr` | Sprite12 | 8565 | 197189197 | 3804 | `d8a5bbe87b863a6a5f0c17c7f206de0e3b20c015f1ef4bed3bd937093a24e074` | 無 |
| 6650 | `6650-30.spr` | Sprite04 | 9050 | 268082377 | 4654 | `f61b6adce542ce5b46da93967c0bbe32bc390ee0a72795ddc1f5ceb85ef56f84` | 無 |
| 6650 | `6650-31.spr` | Sprite05 | 9160 | 246194712 | 4903 | `cbfd4c0a7944bf905e2fa3ce1e79ac333a13be2ffa2dcea84a943670b0418c80` | 無 |
| 6658 | `6658-56.spr` | Sprite04 | 9091 | 268527497 | 6776 | `bbcb7411872585da9754f0600de5a8f5243124be5bc62e545677974f62e428bc` | 無 |
| 6658 | `6658-57.spr` | Sprite05 | 9203 | 246626806 | 6129 | `230c3c5187a78ffa63f75ad42eae9467c5773fc40785b96c64e7e488c611e18d` | 無 |
| 6658 | `6658-58.spr` | Sprite06 | 9310 | 267694020 | 6351 | `cd2bb82a931fc0967e21dafadfefe505e668737415df13b19c2fbeeccea5c6ad` | 無 |
| 6658 | `6658-59.spr` | Sprite07 | 9360 | 282037365 | 6748 | `ec034a800ae9908d398dad72c74a459dce4e07845e7b7133c94878823930d096` | 無 |
| 6658 | `6658-60.spr` | Sprite15 | 8404 | 26670965 | 6563 | `e9a5b03f6314e1ac67261717cb8b01a4950bb9c39c33bce6da3b4e58029e3097` | 無 |
| 6658 | `6658-61.spr` | Sprite00 | 8476 | 25935350 | 5354 | `0a3b093da3cf8c320f3afcbe825c14d54aa03906632db5a7edfb1d42bd1d26c4` | 無 |
| 6658 | `6658-62.spr` | Sprite01 | 8586 | 26280183 | 5821 | `a0d7023534e43c7f5602e036bcde5786c9bc7e8f60df1d6f533cec884a6cf450` | 無 |
| 6658 | `6658-63.spr` | Sprite02 | 8738 | 25618255 | 6797 | `fe3e1fa168eea5e1b9195f8a0cbc008e3e2b7f3584d50d3715a5fe9b2d1df026` | 無 |
| 6661 | `6661-24.spr` | Sprite09 | 9243 | 219322080 | 4733 | `768ee9f344ec8a8503e96ed102dc4e7ac4ebb250cd7ccdff833eb67cd173ba55` | 無 |
| 6661 | `6661-25.spr` | Sprite10 | 9064 | 217697124 | 4405 | `34b391dd1d7ac1f8a1ecd1140c2f8869ba3a9e7e2f2654bc6ce24d6f8b6267c2` | 無 |
| 6661 | `6661-26.spr` | Sprite11 | 8873 | 196771132 | 4780 | `e1c0a1b5192e9ae7ccb9b41a23f05a691aedb4874ba94a5fa8a7a4af837ea1ff` | 無 |
| 6661 | `6661-27.spr` | Sprite12 | 8693 | 198211555 | 5272 | `dcd7dc6811de6fe6006159c89c6af36672c185fda7507ce44165a95bce5b1db0` | 無 |
| 6661 | `6661-28.spr` | Sprite13 | 8545 | 207765121 | 4731 | `41c15cb90094e67aa75fd29f7b4bdc70441af748347215adb6399b546c8f8358` | 無 |
| 6661 | `6661-29.spr` | Sprite14 | 8467 | 242017604 | 3919 | `ea98b3edefe5dc17ad8c50b0914f10e860b715825226e78ece6701d2bc42816b` | 無 |
| 6661 | `6661-30.spr` | Sprite06 | 9353 | 268106696 | 4244 | `fb947f967fe10d4b33c0e5e53bfcee1a0290021a2524dbb696a2b501d7ecbf0d` | 無 |
| 6661 | `6661-31.spr` | Sprite07 | 9401 | 282464396 | 4906 | `e668b6096fe01f1449e7a6a14e5174ea5157e46f97d78bff1c6afa16d5a5a04b` | 無 |
| 6671 | `6671-24.spr` | Sprite10 | 9131 | 218332054 | 5786 | `8aa0633cea20dcc827424d7a3e1c9595e75b055b52887b515fa1646c0bfe56f8` | 無 |
| 6671 | `6671-25.spr` | Sprite11 | 8951 | 197552451 | 5932 | `4d237f5d71ef415495e12d3cd3dff46761cf8e48a58029bfe031fa928d40fbb8` | 無 |
| 6671 | `6671-26.spr` | Sprite12 | 8779 | 199028069 | 6408 | `58a93e225b40d930af5fcad06f7e5f82c7a65cdc9a3ff0e16ebe53b8e13b799a` | 無 |
| 6671 | `6671-27.spr` | Sprite13 | 8637 | 208691921 | 6585 | `2ab6e1f0fc0d249a6244115eb7e3c632ca1e8b90e364ecf7afe3e38c81a2664f` | 無 |
| 6671 | `6671-28.spr` | Sprite14 | 8563 | 242908177 | 5196 | `8290b4f1257ced25ce8eebf1c64c2e64e98a6283371113ba42901accaa06bd42` | 無 |
| 6671 | `6671-29.spr` | Sprite15 | 8534 | 21392526 | 4343 | `8260172a69d11de9a5deea9994a52333645aeb8cf31fc9f5312b3bcada2be198` | 無 |
| 6671 | `6671-30.spr` | Sprite07 | 9437 | 282783108 | 5163 | `80cf481caad4d9a418820eade72880c2fc8115279fff7b477abf8b5c27fd6afe` | 無 |
| 6671 | `6671-31.spr` | Sprite08 | 9380 | 227825480 | 5935 | `0f3877b4eb394834b16d8881d7ef28f1a52fb35f90796d9f11eff299807a130b` | 無 |

sprite 3215 的 8 個檔在其他包有同名但內容不同的版本，依 §3.3 取主包，與內嵌資料相符。gfx 6650、6658、6661、6671 的 32 個檔只在替代包，每個檔只出現在一個包。

## 6. 規則常數（不來自客戶端資料）

這些值寫在產生器裡，新的 recipe 要照抄，或改由產品程式自己帶：

| 值 | 用在 | 說明 |
|---|---|---|
| `effectTileType: 18` | `objects/magic-189／191／197／221／2165.json` | 產品的混色模式 |
| `flags: 5` | 全部 `shadows/*.json` | `PlayerShadows.cs` 開頭註明的來源旗標 |
| `stepMs: 167` | `doors/2346.json` | |
| `stepMs: 1` | 全部 `objects/magic-*.json` | |
| 24 ticks／秒、40 ms／tick | 時間換算 | §3.6 |

## 7. 沒有比對、或不在本張範圍的項目

- PNG、GIF 的檔案 bytes（取決於編碼器）。PNG 比的是解碼後的 RGBA；GIF 與 contact sheet 沒有比內容。
- JSON 的檔案 bytes 與各 manifest 裡的 `sha256`：新 recipe 的序列化不同，SHA 會變。本文比的是解析後的值。
- `objects/names.json`（T444）、`world-appearances.json` 與 `encountered-monsters.json` 這兩份清冊（T447）。
- SQL、JAR、DB 都沒有讀。
- 沒有宣稱任何資源缺少，所以沒有用 PakViewer 查 pak。

## 8. 會影響後續實作的發現

### 8.1 Sprite03–14 的 idx／pak 與產品 pin 不符

`ClientArchive.cs` 的 `UiResourcePins.Sprites` 與 `GroundSpriteReader.cs` 的 `archivePins` 對 17 組 Sprite 包各 pin 了 idx SHA、pak SHA 與 pak 大小。本機現在有 12 組不符：

| 包 | pin 的 pak 大小 | 目前大小 | 差 | 多出的 entry（都在舊檔尾之後） |
|---|---:|---:|---:|---|
| Sprite03 | 422,961,934 | 422,963,990 | 2,056 | `22130.img` |
| Sprite04 | 396,502,716 | 396,506,828 | 4,112 | `22131.img`、`22140.img` |
| Sprite05 | 370,792,959 | 370,799,127 | 6,168 | `22132`、`22141`、`22150` |
| Sprite06 | 399,491,486 | 399,499,710 | 8,224 | `22124`、`22133`、`22142`、`22151` |
| Sprite07 | 428,993,498 | 429,001,722 | 8,224 | `22125`、`22134`、`22143`、`22152` |
| Sprite08 | 405,690,494 | 405,698,718 | 8,224 | `22126`、`22135`、`22144`、`22153` |
| Sprite09 | 409,910,541 | 409,918,765 | 8,224 | `22127`、`22136`、`22145`、`22154` |
| Sprite10 | 401,362,413 | 401,370,637 | 8,224 | `22128`、`22137`、`22146`、`22155` |
| Sprite11 | 408,627,491 | 408,635,715 | 8,224 | `22129`、`22138`、`22147`、`22156` |
| Sprite12 | 392,469,796 | 392,475,964 | 6,168 | `22139`、`22148`、`22157` |
| Sprite13 | 404,075,215 | 404,079,327 | 4,112 | `22149`、`22158` |
| Sprite14 | 442,915,587 | 442,917,643 | 2,056 | `22159.img` |

- 12 組的 idx 與 pak 修改時間都是 2026-11-09 11:23（本機時間）。多出來的是 `22124.img` 至 `22159.img` 共 36 個檔，每個 2,056 bytes，接在原檔尾。誰改的、為什麼改，本張沒有查。
- 舊內容沒有被動到的證據：本輪從這 12 包取的 SPR 全部與基線相符。
- 影響：產品開這 12 包時會因為 SHA 不符而拒絕（`ClientArchive.Load`、`GroundSpriteReader.Archive.Find`）。本機沒有執行產品，實際影響哪些畫面沒有驗證。
- 對 recipe 的建議：不要 pin 整個 pak 的 SHA。改成「每個用到的 entry 記檔名、大小、payload SHA-256」，容器只做格式檢查（idx 筆數 × 28 ＋ 4 = 檔案大小、offset ＋ size 不超出 pak、同包不重名）。這樣別人的客戶端多了無關的 entry 也不會被擋。這條交 T451／T452 定案。

目前 12 組的 SHA-256（給 T451 對照）：

| 包 | idx SHA-256 | pak SHA-256 | pak 大小 |
|---|---|---|---:|
| `Sprite03` | `bb26979cf21f7655b3f5d57e2c0d152f8ca7e755ad66f1801a1edb455245cd72` | `3a1d8c976c70ad908f9b9f5de16f65cbcfa1cad47e5de7f84e615e18c8363f8d` | 422963990 |
| `Sprite04` | `7c801f623f68bdca50271dbdddc2fefbcac990e9e9ef60776f48daa73f9cd1f5` | `8f4bbb0c296208c3d6a7ba6fc666d313a25621650d455557e709ebbbabfd14dd` | 396506828 |
| `Sprite05` | `659b4cd84092e9de5b76b0ffa4793b562213b42bcf0a9771eb0e85e6aa5e379b` | `9bb611e401b2f48e4f0a89f7eeb79165d4b182ca082e6c09b36b3a46d9f7f93e` | 370799127 |
| `Sprite06` | `45aa43f0ec1a1480e82c27379bd25b433f960f27160b87bf5e0dfe9f67d831e4` | `4514513774f4e591e1140efc801fe390231f405f4d030c2f0701aad6ff5e01b3` | 399499710 |
| `Sprite07` | `c49468722b1265864cf66f169c09d1318f0decd4817af9f15cd8ebb67f6c98ab` | `7e4e7797e16471ed9bf8adbce6724e21c992a7a860bb10832214d3e9a94663ae` | 429001722 |
| `Sprite08` | `4eb34a830bbae9d3c3ce112d83a2cc3dd96d0312bfa798ed59542f403924c63c` | `4fd3a06362673f2e5a3cb1434777366d32c6edad1a330e400d092ce62fd32e04` | 405698718 |
| `Sprite09` | `cc8b83fe4010ee5a3fa470466a414931bd2e61308972d76c610ada8ea1c04896` | `06b8a0a98bbbf25871957c06e767247d92be49c9745aec4fc8593bbfab91e466` | 409918765 |
| `Sprite10` | `7b4688cf2b01c7889a5e6ef8f06309c52c8aad781537bff92d23ed543e08fb8d` | `bd19155dccfc3a7c7dfc5adafa63ce63d65e6640a4a6d43f363093b3b3f245c2` | 401370637 |
| `Sprite11` | `a190e3e9e54753d89b2ae1209910ad053e6be1d857e895f01631e6da9c7dd88e` | `f2d47ff75c12e25a49ef1018bc74284535fb452770434ba17fc44d7637424a78` | 408635715 |
| `Sprite12` | `2c271ef696788ba1f44acaf0a8fe93e4abae5c88a57d6103e3ccb84c4b319541` | `cf1fa59d39a9c3a4ad4afcd2ef11382c48946f88bd5b29cf1a627f1aee5a5a46` | 392475964 |
| `Sprite13` | `333d0670a4e2fe47c51b62f4f60de2a579b4b7bc5b9f0910bb2a7f79502c671b` | `ce206c743f0f7744bb7153791be27b6fb84e5c34f91b8cb92f2c6a520294c946` | 404079327 |
| `Sprite14` | `51802133f6be2bb7a8e727c58f5da299b9c67c707506e2931ff63b1a4d4633cf` | `55a73c6b6f3315f5e5ea81e7b4d9c7b120f61e41864297f01b4c1f306f65c6d4` | 442917643 |

### 8.2 空幀的判定

`GroundSpriteReader.DecodeCore` 只把表頭 `(192,120,-128,-120,65535,0,0)` 當成空幀。`1094-1.spr` 第 25 幀的表頭是 `(352,240,-288,-240,65535,0,0)`，照產品現在的檢查會整個檔被拒絕。

基線 `objects/1094.json` 把這一幀收成「`bounds` 照抄、`blocks` 為空、`sourceEmpty: true`」。原型改成下面的判定後，52 個外觀檔全部通過：

> tile 數為 0，且 `u = 65535`、`v = 0`、`right < left`、`bottom < top` → 空幀，`bounds` 照原值輸出。

C# 準備器（T454）要用這個判定，不能直接沿用 `GroundSpriteReader` 的條件。

### 8.3 找動作的正規表示式

`(?<![\d.])<aid>\.[^(]*\(` 會錯配：找動作 8 時，會從前一個動作 token 裡的 ` 8.7:4` 開始，一路吃到下一個動作的左括號。原型第一輪就因此把 gfx 0 的 die 配錯。

正確寫法要求動作名稱以英文字母開頭：`(?<![\d.])<aid>\.[A-Za-z][^(]*\((\d+)\s+(\d+),([^)]*)\)`。改用這個寫法後全部相符。C# 最好先把 entry 切成一個個動作再比對編號，不要用單一正規表示式硬找。

## 9. 驗證方法

原型：`scratch/t449/t449lib.py`（讀 idx／pak、Text、list.spz、解 SPR）、`compare.py`（逐檔比對）、`inventory.py`（schema 盤點）、`hash_archives.py`（容器 SHA）。只讀 `TW_CLIENT_ROOT` 與主目錄的 `engine/assets`（junction → `releases/r29/assets`），不寫任何素材。

做法：對基線每個 JSON，用 `list.spz` 與 SPR 重算每一幀的 `bounds` 與每個 block 的全部欄位、每筆序列每一步的全部欄位，與基線解析後的值做相等比較。時間欄位另外分類成 floor、exact、×40 三種算法。

重跑（約 10 分鐘）：

```
python -I -X utf8 scratch/t449/compare.py scratch/t449/results.json
```
