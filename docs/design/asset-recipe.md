# 素材 recipe 契約與已知限制清單（T452／Issue #1037）

日期：2026-11-13。執行者：本機 Claude（claude-local）。只改文件。

這份文件把 T444–T450 七張來源閉合任務與 T451 準備器核心的結果，收成 M8 後續任務（T453–T475）的實作契約。後續任務照這份做；與個別任務原文衝突時，以這份為準（第 9 節逐張列出改了什麼）。

讀法：

- **定案**：本文件直接決定，後續任務照做。
- **第 7、8 節的裁定（T476／#1072，2026-11-13）已全部定案**：L1、L2 由使用者裁定（L1＝A、L2＝X），其餘由主控（調度器雲端主導對話）裁定；出處是 #1072 的兩則裁定留言。原本的「暫行做法」欄已改成「定案」。
- 規則的細節（演算法、欄位、比對數字）不在這裡重寫，各節指向 `docs/reference/asset-recipes/` 下的配方文件。

來源文件：

| 主題 | 配方文件 | 報告 |
|---|---|---|
| Text 容器、names、templates | [`text.md`](../reference/asset-recipes/text.md) | [#1029](../reports/issue-1029.md)（T444） |
| terrain 與 bounds | [`terrain.md`](../reference/asset-recipes/terrain.md) | [#1030](../reports/issue-1030.md)（T445） |
| skills、magic、EXP 表、ground-catalog | [`rule-tables.md`](../reference/asset-recipes/rule-tables.md) | [#1031](../reports/issue-1031.md)（T446） |
| 動作時序與 gfx 清冊 | [`action-timing.md`](../reference/asset-recipes/action-timing.md) | [#1032](../reports/issue-1032.md)（T447） |
| motion 與混色表 | [`motion-blend.md`](../reference/asset-recipes/motion-blend.md) | [#1033](../reports/issue-1033.md)（T448） |
| SPR 類與 pickup | [`sprites.md`](../reference/asset-recipes/sprites.md) | [#1034](../reports/issue-1034.md)（T449） |
| 地圖、TIL、scene、cache、地圖特效 | [`maps.md`](../reference/asset-recipes/maps.md) | [#1035](../reports/issue-1035.md)（T450） |
| 準備器核心 | `engine/source/AssetPrep/` | [#1036](../reports/issue-1036.md)（T451） |

本文件的數字都引自上列文件，本張沒有重跑任何比對，也沒有讀客戶端、SQL、JAR 或 DB。

## 1. 總原則（定案）

### 1.1 輸入

1. 準備器與 provider 只讀兩樣東西：使用者給的絕對 `TW_CLIENT_ROOT`，以及產品程式裡的規則。不讀開發機路徑、`releases/`、研究目錄、SQL、JAR、BIN、既有 `engine/assets`。
2. 找 entry 一律用**檔名**（不分大小寫），不用任何舊的 File Offset 或 record 序號。Text 與 Tile 兩個容器都已經換過版，舊 offset 全部失效（`text.md` 第 2 節、`maps.md`「原檔切片」）。
3. **不再用整個 pak 的 SHA 當拒絕條件**。本機客戶端的 `Sprite03`–`Sprite14` 在 2026-11-09 被附加了 36 個無關的 `.img`，12 組 idx／pak 的 SHA 與大小都變了，而本文件用到的 SPR 內容沒有變（`sprites.md` §8.1）。T451 核心現在開啟時會驗 17 個 Sprite 容器的 idx SHA 與 pak 大小，在這台機器上會直接拒絕。改成三層：
   - **結構檢查（不過就拒絕）**：idx 大小 = 筆數 × 28 ＋ 4；每筆 offset ＋ size 不超出 pak；同一包內不重名；Text 另照 `TextArchive` 的格式。
   - **錨點 entry（不過就拒絕，並回報是哪一個 entry）**：少數幾筆決定「這是不是支援的客戶端版本」，以解碼後的 SHA-256 寫在程式裡（只有雜湊，沒有內容）。清單見 1.2。
   - **逐 entry 記錄**：每個用到的 entry 把容器名、檔名、offset、size、payload SHA-256 記進收據。這是出處紀錄，也是續跑時判斷「來源有沒有變」的依據，不拿來和預期值比。
4. 主包優先與替代包唯一性照 `sprites.md` §3.3：`Sprite` 有就用 `Sprite`；沒有時在 `Sprite00`–`Sprite15` 找，找到多份而內容不同就列為 unavailable，不挑一個。
5. 動畫表以 `list.spz` 為準（`sprites.md` §2.3）。gfx → sprite 編號只用 `list.spz`，不另外讀 `TW13081901.txt`。
6. 原客戶端一律唯讀。

### 1.2 錨點 entry

| 容器 | entry | 比對的值 | 用途 |
|---|---|---|---|
| Text | `list.spz` | 內層解完的 SHA-256 `e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242` | 全部 SPR 類、地圖特效、動作時序 |
| Text | `list.spr` | 解碼後 `523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127` | 地圖特效（兩份 metadata 必須相同） |
| Text | `desc-c.tbl` | 解碼後 `f06c94f47271fcb01f1d5af63725dfa9a230b5f6be3c0dd1d53cd37628adb7df`（等於產品 `TextArchive.DescSha256`） | `objects/names.json` |
| Text | `spelldesc-c.tbl` | 解碼後 SHA，全值見 `rule-tables.md` 1.2（開頭 `387c4a9d`） | 技能名稱與 MP（T462） |
| Tile | `Water.lac`、`pier1.lac`、`pier5.lac` | `ShipBackdrop.cs` 已寫死的三個 SHA | 船運背景 |
| `map/6` | `7fff8000.seg` | `OriginalMapCache.cs`、`Portals.cs` 已寫死的 SHA（開頭 `aad5ff59`） | 唯一支援的 SEG 區塊 |
| 輸出 | `maps/effects/6016.json` | `ClientEffect6016.cs` 已寫死的整檔 SHA（開頭 `825673b1`） | 6016 音效事件 |

- `*-c.html` 模板**不設錨點**：本機客戶端已有 176 筆與研究當時不同（只差圖片編號），這是正常的版本差異（`text.md` 第 5 節）。
- 錨點不符時：該 recipe 停止，訊息寫出容器與 entry 名稱；不退回任何其他來源。README（T473）的「支援的輸入版本」就是這張表。

### 1.3 輸出

1. 輸出只進 `engine/assets`（經 staging）。執行期**不寫** `engine/assets`：這棵樹只有準備模式一個 writer，integrity 才不會在遊戲中途失效。
2. 輸出檔不含絕對路徑、時間、使用者名稱、機器名稱。基線裡帶絕對路徑的欄位（`originalSource`、`sourcePath`、`source.index`、`source.pak`、`opacity.json` 的 `maps[].path`、`sourceArchives`）一律拿掉或改成容器名稱。
3. 產品不讀的預覽檔與來源紀錄欄位不再產生（各類見第 3 節）。
4. **schema 字串沿用現值**，除非產品會讀的欄位有變。只拿掉產品不讀的欄位不算改 schema；recipe 的 `Version` 記在收據裡。
5. 相同輸入、相同 recipe 版本，在任何目錄產生的輸出、收據與 integrity 逐 byte 相同。
6. 不可以把任何表的內容寫成程式常數、Base64 或壓縮字串放進 assembly。可以放的只有：規則（算式、動作編號）、整數編號清單（要準備哪些 gfx）、雜湊。

### 1.4 比對方式的定義（T470 用）

| 代號 | 意思 |
|---|---|
| **B** 逐 byte | 新檔與 r29 基線檔的 SHA-256 相同 |
| **S** 語義 | 兩邊解析後比欄位值：幀的 `key`／`bounds`／每個 block 的 `a`／`b`／`z`／順序／每個像素的 x、y、RGB555；序列每一步的 `key`／`ticks`／`events`／`milliseconds`；時間欄位；集合與排序。基線有、新檔依本文件拿掉的欄位不比 |
| **P** decoded RGBA | 兩邊的 PNG 解成 RGBA 後逐像素相同，尺寸與 offset 相同 |
| **G** 逐格 | terrain 每一格依位元比，標準見第 7 節 L3 |
| **R** 規則 | 沒有檔案可比：用程式算出的值與基線檔的值全量相等（例如混色表 1,024 格） |

等價證明一律是「對 r29 基線比」，不是拿自己產生的 hash 當證明。

## 2. 總表：19 類素材加 pickup

「v1 檔數」是 r29 基線 `integrity.json`（`p06-assets-integrity-v1`，13,957 檔）的數字。「做法」三種：**準備器**（首次啟動時產生檔案）、**直讀**（執行期由 provider 從 `TW_CLIENT_ROOT` 讀，不產生檔案）、**程式**（改成產品規則，沒有檔案）。

| # | 類別（v1 檔數） | 來源閉合結論 | 做法 | 新輸出與 schema | 比對 | 任務 |
|---:|---|---|---|---|---|---|
| 1 | `cache`（378） | **部分**：373 個原檔 pass；manifest pass；`original-map0.txt` 有 112 格區域位元不同（L3）；三張混色表 pass | 原檔 → 直讀；混色表 → 程式；`manifest.json`、`original-map0.txt` → 準備器 | `cache/manifest.json`（`p24-original-cache-v1`，拿掉 `record`、`pixelModes`、`sourceArchives`、`scope`）、`cache/original-map0.txt` | manifest S；txt G；混色表 R | T456、T458、T459 |
| 2 | `character`（103） | **pass** | 準備器 | `character/blocks.json`（`p06-spr-blocks-v1`）、`character/animation.json`（`p04-character-v1`）。其餘 101 個預覽檔不產生 | S（96 幀、16 筆 mapping） | T454 |
| 3 | `dialogs`（2） | `templates.json` **pass**（規則相同；本機來源有 176＋1 筆版本差異）；`skills.json` **pass**（名稱有客戶端來源，等級是算式） | templates → 準備器；skills → 直讀 Text 的 `spelldesc-c.tbl`，不產生檔案 | `dialogs/templates.json`（格式照 `text.md`「序列化規則」） | templates S，標準見 3.1；skills R（177 筆名稱相同、6 筆譯名不同、等級 183 筆全同） | T453、T462 |
| 4 | `doors`（2） | **pass** | 準備器 | `doors/2346.json`、`doors/manifest.json` | S | T455 |
| 5 | `encountered-monsters.json`（1） | 內容 **pass**；成員是研究時挑的清冊（D5） | 準備器 | `encountered-monsters.json`（欄位照現值） | S | T455 |
| 6 | `ground`（4） | **pass** | 直讀（`GroundSpriteReader`，#190 已有） | 不產生 | P（執行期解出的圖對基線兩張 PNG）＋S（bounds、blocks） | T455 確認讀取點 |
| 7 | `inventory`（20） | **pass**（T451 的切片；真客戶端像素還沒實比） | 直讀（`ItemIconSource`／`WebIconGenerator`，T469 已完成） | 不產生。`inventory` recipe 保留為程式庫切片，不進總流程 | P（20 張基線 PNG 對執行期解碼結果） | 已完成；T470 補比對 |
| 8 | `magic`（1） | **pass**（可以不需要這張表） | 直讀 Text 的 `spelldesc-c.tbl`、`SpellExDesc-c.tbl`，加產品已有的 `SkillCast.Wire` | 不產生 | R（技能 1／3／4／8 的 `name`、`mp`、`action`、`reuseMs`、`target` 與基線相同） | T462 |
| 9 | `maps`（13,304） | 原檔 12,253 個 **pass**；`manifest.json` **部分**（`maps[].name` 沒有客戶端來源，L4）；terrain 713 檔 **部分**（L3）；特效 337 檔 **pass**（154 個不可用 id 是基線既有，L6） | 原檔（S32／SEG、TIL、LAC）→ 直讀；manifest、terrain、effects → 準備器 | `maps/manifest.json`（`p24-map-provider-v1`）、`maps/terrain/<id>.bin`、`maps/effects/<id>.json` 與 `manifest.json`（`p24-map-effects-v1`） | 原檔 B（對客戶端來源）；manifest S；terrain G；effects B（336 檔）＋manifest S | T456、T457、T458 |
| 10 | `motion.json`（1） | **pass**（每欄都有來源；`spawn` 是產品自訂值） | 程式 | 不產生 | R（8 個方向、166 ms、spawn） | T459 |
| 11 | `objects`（54） | **pass**（52 個外觀檔）；`names.json` **pass**（逐 byte 可重現）；成員是清冊（D5） | 準備器 | `objects/<名稱>.json`、`objects/manifest.json`、`objects/names.json` | 外觀 S；names B | T454、T453 |
| 12 | `players`（39） | **pass**（15 筆 unavailable 的原因可重現，L7） | 準備器 | `players/<gfx>-<weapon>.json`、`players/manifest.json`（`p24-player-profiles-v1`，拿掉 `className`） | S | T455 |
| 13 | `scene`（13） | **pass** | 準備器（4 檔）；9 個預覽檔不產生 | `scene/manifest.json`、`draw-commands.json`、`tile-pixels.json`（`p22-dual-map-v1`）、`opacity.json`（`p23-original-opacity-v1`，`maps[]` 拿掉 `path`） | 前三檔 B；opacity S | T457 |
| 14 | `shops`（12） | **pass** | 準備器 | `shops/<gfx>.json`、`shops/manifest.json`（`p24-original-shop-appearance-v1`） | S | T455 |
| 15 | `terrain0.bin`（1） | **pass** | 準備器 | `terrain0.bin` | B | T458 |
| 16 | `world-appearances.json`（1） | 內容 **pass**；成員是清冊（D5） | 準備器 | `world-appearances.json` | S | T455 |
| 17 | `shadows`（18） | **pass** | 準備器（排在 body 之後） | `shadows/<gfx>.json`（`p24-original-shadow-frames-v1`）、`shadows/manifest.json`（`p24-original-shadows-v1`） | S | T455 |
| 18 | `audio-manifest.json`（1） | 七張閉合任務沒有涵蓋音效。#97 附錄 B 實測過兩個 WAV 與基線 SHA 相同；manifest 本身帶絕對路徑與研究證據，不重現 | 程式（兩個 WAV 的路徑、大小、SHA 寫成 pin，做法同 `CombatSfxPins.json`） | 不產生 | R（載入順序、可用性、pin 與現行 manifest 相同） | T460 |
| 19 | `sound`（2） | 同上（`Sound/1340.wav`、`Sound/3332.wav` 與基線 SHA 相同） | 直讀 `TW_CLIENT_ROOT/Sound/<id>.wav` | 不產生 | B（對客戶端來源，用 pin） | T460 |
| 20 | pickup（2 個內嵌 JSON，不在 v1 內） | **pass**（13 groups、312 幀、200,628 pixels 全同） | 準備器 | `pickup/pickup-own-base15-gfx0-weapon0.json`、`pickup/pickup-own-base15-profile-extension.json` | S | T463 |

整個類別判 blocked 的：**沒有**。blocked 的是個別欄位或個別表（第 7 節 L1–L4），以及 B15 例外清單裡兩個不屬於這 19 類的檔案（`OriginalExpTable.cs`、`ground-catalog.json`；裁定見 L1、L2）。

新的 active 樹大約多大：原檔改直讀後，地圖三類從 1.70 GB 降到約 245 MB（`maps.md`「磁碟用量」）；SPR 類照現行 JSON 約 0.5 GB。實際數字由 T470 量。

## 3. 各類輸出細則（定案）

只寫與基線不同、或實作時容易做錯的地方。沒提到的照配方文件。

### 3.1 文字類（recipe `text`，T453）

- `objects/names.json`：照 `text.md` 第 4 節的序列化（逗號加空白、不跳脫非 ASCII），要求 **B**。`System.Text.Json` 的預設輸出做不到，要自己寫出。
- `dialogs/templates.json`：重複鍵規則（內容相同略過、內容不同整個鍵移除）照做。驗收**不比基線 SHA**，標準是：鍵集合 = 基線 8,896 個加 `lyra_y01`；8,720 筆內容相同；176 筆只差 `<img src="#N">` 的編號（清單在 `text-templates-diff-ids.txt`）。
- code page 950 用 `CodePagesEncodingProvider` 加例外式 fallback。
- 不疊加 `LiTo\LTtext`（基線沒有用它；見 D2）。
- `dialogs/skills.json` 與 `magic/catalog.json` 不由這個 recipe 產生（3.6）。

### 3.2 SPR 類（T454、T455、T463）

- 共用解碼照 `sprites.md` §3。三個已知的坑照 §8 處理：空幀判定放寬、動作先切 token 再比編號、動畫表用 `list.spz`。`GroundSpriteReader` 不改。
- 幀結構拿掉 `source_offset`、`block_index`（產品不讀）。各類的來源紀錄欄位（`sourceOffset`、`spzOffset`、`sourceActions[].sql`、`serverSpeedSource`、`className`、`sourcePolicy` 這類）不產生；`entryHash`、`metadataSha256` 保留（是雜湊，續跑與比對用得到）。
- **時間欄位照基線現值的算式**（`action-timing.md` R1–R3、`sprites.md` §3.6），不讀 SQL：
  - `stepMs`、`serverActionMs`、`serverAttackMs`：`40 × ticks 總和`。
  - `castReleaseMs`、`projectileReleaseMs`：第一個帶 `!` 的步之前，各步 `floor(ticks × 1000 ÷ 24)` 的總和。
  - `actionMs`：照 `action-timing.md`「`actionMs` 用了哪條算式」那張表（哪一類、哪個動作用 ×40、floor 或 exact）。這是為了讓 T470 能和基線比；要不要改成原生換算見 D4。
- 規則常數（`effectTileType: 18`、shadows 的 `flags: 5`、doors 的 `stepMs: 167`、magic 的 `stepMs: 1`）寫在 recipe 程式裡（`sprites.md` §6）。
- `character`：只產生 `blocks.json` 與 `animation.json`。`engine/source/Tests.cs:19` 有一個離線測試會開 `character/` 下的 PNG，T454 要一起改成用 blocks 重畫後比對，或改用合成資料。
- `players` 的 15 筆 unavailable 照基線重現（12 筆缺動作、3 筆 gfx 138 的像素落在 bounds 外）。見 L7。
- `shadows`：sprite 編號取 `101.shadow` 的值，不用「body ＋ 1」推算；frame keys 取 body 序列**實際引用**的 key 的聯集。
- `world-234`、`magic-167`、`magic-11722`、`1094` 四個特例照 `sprites.md` §4.3。
- pickup：動作 15，第 0 步的幀來自 body profile 不重複產生；`frames` 順序是方向 0–7、每個方向第 1–3 步（`sprites.md` §5）。
- `ground`：不預先產生。T455 開工時先確認 `assets/ground/*` 還有沒有讀取點，有就改接 `GroundSpriteReader`。

### 3.3 地圖原檔（provider，T456）

- S32／SEG 從 `TW_CLIENT_ROOT/map/<mapId>/<檔名>` 直讀；TIL 與三個 LAC 從 `Tile.idx`／`Tile.pak` 依名稱取 entry。不複製進 `engine/assets`，`cache/map`、`cache/tiles` 也不再另存一份。
- 讀到的每個檔照現在的方式核對 SHA：區塊與 TIL 的 SHA 來自 `maps/manifest.json`，LAC 與 SEG 用程式裡寫死的值。準備完成後客戶端檔案若被改動，載入時明確失敗。
- **地圖 provider 不疊整包 `Tile.pak` 的 pin**（1.1 第 3 點）。`ClientArchive` 給 UI 用的 Tile 與 Sprite 整包 pin 由 T479 改成三層驗證（D1 定案）。
- `maps/manifest.json`：
  - 保留產品會讀的欄位：`blocks[]`（`mapId`、`name`、`path`、`sha256`、`noEffectTail`，SEG 那一列的 `format`）、`maps[]`（`mapId`、bounds、`width`、`height`、`terrain.path`、`terrain.sha256`、`verifiedAbsent`、`unsupportedBlocks[].name`）、`tiles[]`（`tile`、`sha256`、`path`）。
  - 拿掉：`maps[].name`（L4）、`copyTerrainMapId`、`clientMapDirectoryPresent`、`directTerrainFilePresent`、`terrain.sourcePath`／`sourceSha256`、`tiles[].record`、`originalSource`、`unsupportedBlocks[].sources`、`provenance`、`scope`、`nativeVa`、`nativeImageBase`。
  - `path` 保留原檔名大小寫（map 4 有 159 個 `.S32`）。`blocks[].path` 的意思從「assets 內路徑」變成「`TW_CLIENT_ROOT` 內路徑」，字串本身不變（`map/<目錄>/<檔名>`）。
  - bounds 照 `terrain.md`「座標與 bounds」，`.s32` 與 `.seg` 一起算。
  - 地圖與區塊的選取規則見 D3（已定案）。
- 動到的讀取點：`OriginalMapCache.cs`、`Portals.cs`、`ShipBackdrop.cs` 的讀檔根目錄。行為不變。

### 3.4 terrain（recipe `terrain`，T458）

- 713 張**全部**照 `terrain.md` 的規則產生，不因為 L3 的 8 張而跳過任何一張。理由：這 8 張的通行位元（bit0–3）完全相符，差的只有產品不讀的區域位元。
- 三個容易漏的地方照 `terrain.md`：副檔名不分大小寫；只有 `.seg` 的區塊要讀；bounds 內沒有區塊檔的格子當成 attribute 1。最外圈一格整個 byte 歸零。推導時用檔案原值，不用 `OriginalMapCache.cs` 載入時加的 `| 2`。
- `terrain0.bin` 與 `cache/original-map0.txt` 由同一個 recipe 產生。
- `maps/manifest.json` 與 `cache/manifest.json` 的 terrain SHA 寫**新產出檔**的 SHA。

### 3.5 scene 與地圖特效（T457）

- scene 三個執行檔與 336 個特效檔要求 **B**。格式照 `maps.md`：鍵順序固定、沒有空白、非 ASCII 不跳脫；特效檔與特效 manifest 檔尾是 CRLF；scene 三個執行檔沒有任何換行。
- `6016.json` 必須逐 byte 相同，`ClientEffect6016.cs` 的 pin **不改**。C# 做不到逐 byte 時，停下來回報，不要改 pin。
- `opacity.json` 的 schema 字串寫 `p23-original-opacity-v1`（P24 的研究腳本寫的是 `p24-…`，產品會拒絕）。
- 特效 manifest 的 `source` 改成 `{archive, name, size, sha256}`，不含路徑與 record 位置。154 個不可用 id 與原因字串照 `maps.md` 重現。

### 3.6 改成直讀或程式的類別

- **混色表**（T459）：`Renderer.cs:33` 的 screen 表改成啟動時用整數式 `hi + (31 − hi) × lo / 31` 算 1,024 格；不要寫成 `31 − (31−a)(31−b)/31`（有 900 格不同）。multiply、half 在產品裡已經是算式。三個 `cache/*-channel.bin` 不再產生。
- **motion**（T459）：`Assets.cs:53–55` 不讀檔；方向用 `MovementRules.Dx／Dy`，`FrameMs = 166`、`Spawn = [32588, 32928]` 寫成有註解的常數（spawn 是 P06 研究時選的出生點，屬產品自訂值）。`DoorChecks.cs:190` 的測試用 `motion.json` 與 `baseline-pins.json` 的四個 pin 一起處理。
- **skills 與 magic**（T462）：照 `rule-tables.md` 1.5、2.3。讀 `Text.pak` 的 `spelldesc-c.tbl`（不是 `LTtext.pak` 的 `LTSpellDesc-c.tbl`，那份沒有龍騎士與幻術師）。等級用 `((id − 1) >> 3) + 1`。冷卻用原版預設 10 ms。`effect` 欄位沒有讀取者，不用替代。
- **音效**（T460）：1340、3332 由 `TW_CLIENT_ROOT/Sound/<id>.wav` 直讀並驗 pin；`audio-manifest.json` 不再是檔案，載入順序（mp3 → wav）、MP3 不存在時的紀錄、WAV fallback、pin 不符不 fallback 的行為都不變。host 端傳入的 manifest SHA 參數隨之拿掉或改成版本字串，6016 回歸要重跑。
- **inventory**：T469 已改成檔案優先、沒有檔案時即時解碼。可攜模式不會有 `assets/inventory`，一律即時解碼。

## 4. 收據與 integrity（定案）

### 4.1 三個檔

| 檔案 | schema | 內容 |
|---|---|---|
| `engine/assets/integrity.json` | `tw-assets-integrity-v2` | active 樹的檔案清單 |
| `engine/assets/prep-receipt.json` | `tw-asset-prep-set-v1` | 總收據：輸入、各 recipe 的版本與摘要、直讀的類別、已知限制、狀態 |
| `engine/assets/receipts/<recipe-id>.json` | `tw-asset-prep-receipt-v1`（T451 現有格式） | 單一 recipe 的收據：每個項目的來源 entry 與輸出檔 |

三個檔都不含絕對路徑、時間、使用者或機器名稱。會隨機器變的東西（實際的 `TW_CLIENT_ROOT`、開始與結束時間、耗時、磁碟用量）寫到 `engine/runtime/prep-log.json`，不進 `engine/assets`、不算雜湊。

### 4.2 `integrity.json`（v2）

```json
{
  "schema": "tw-assets-integrity-v2",
  "receiptSha256": "<prep-receipt.json 的 SHA-256，小寫十六進位>",
  "files": [
    { "path": "character/animation.json", "size": 0, "sha256": "…" }
  ]
}
```

- `files[]` 的三個鍵與 v1 **完全相同**（`path`、`size`、`sha256`），所以六個只查 `files[].path` 的讀取點不用改。
- 列入 active 樹裡除了 `integrity.json` 與 `prep-receipt.json` 以外的每一個檔（含 `receipts/*.json`）。`path` 用 `/`，依序數（ordinal）排序。
- 直讀的原檔（S32、TIL、LAC、WAV）**不在** integrity 裡；它們的 SHA 在 `maps/manifest.json` 或程式的 pin。

與 `p06-assets-integrity-v1` 的關係：

- v1 是 r29 封存基線的清單（13,957 檔），**不改、不重產**。開發機的 `engine/assets` 仍是指向 `releases/r29/assets` 的 junction，繼續用 v1，準備器不會也不可以寫它。
- v2 是使用者本機產生的清單。檔數少很多（預覽檔、原檔切片、改成直讀或程式的類別都不在）。
- 產品同時接受兩種 schema。兩者不混用：一棵樹只有一種。
- 不可以用改 v1、更新舊 SHA 或重產 lock 的方式宣稱等價。等價由 T470 對基線比對證明。

### 4.3 讀取點怎麼接

| 讀取點 | 現在 | 改法 | 任務 |
|---|---|---|---|
| `Assets.cs:61–73` `VerifyIntegrity` | schema 必須是 v1；`maps/` 開頭略過，其餘逐檔核對存在、大小、SHA | 接受 v1 或 v2。v2 時另外要求 `prep-receipt.json` 存在、SHA 等於 `receiptSha256`、`state` 是 `ready`。`maps/` 略過的規則不變 | T467 |
| `OriginalMapCache.cs:19`、`Portals.cs:27` | 從 integrity 取 `maps/manifest.json` 的 SHA | 不用改 | — |
| `MapEffects.cs:23`、`:161` | 從 integrity 取 `maps/effects/manifest.json`、`objects/magic-<gfx>.json` 的 SHA | 不用改 | — |
| `ObjectAssets.cs:48`、`PlayerProfiles.cs:10`、`PlayerShadows.cs:14` | 從 integrity 取各自檔案的 SHA | 不用改 | — |
| `ClientEffect6016.cs`、`ShipBackdrop.cs`、SEG 的寫死 SHA | 寫死在程式 | 不改（等於錨點） | — |
| `AudioAssets.cs:30–35` | 驗 `audio-manifest.json` 整檔 SHA | 改成程式 pin（3.6） | T460 |
| launcher `Program.cs:57–66` | manifest 必須含 `assets/integrity.json`、schema v1、逐檔核對存在與大小 | `engine/assets` 是 junction → 開發模式，照現在的 v1 檢查；是普通目錄 → 驗 v2 與總收據；不存在或收據不是 `ready` → 未準備 | T468 |

規則：**某個檔從 active 樹拿掉時，拿掉它的那張任務要在同一個 PR 改掉它的讀取點**（那些讀取點多半用 `.Single(path == …)`，找不到會丟例外）。

### 4.4 總收據 `prep-receipt.json`

欄位（鍵名由 T467 定，內容必須有）：

- `schema`、工具版本（沿用 `tw-asset-prep/<n>`）。
- `state`：`ready` 或 `partial`。
- 輸入：17 個 Sprite 容器、Text、Tile 的 idx 大小與 idx SHA-256、pak 大小；1.2 每個錨點的名稱與實際 SHA。整個 pak 的 SHA 不是必要欄位（這次有整檔算過才記）。
- `recipes[]`：`id`、`version`、涵蓋範圍、項目數、輸出檔數與總 bytes、該 recipe 收據檔的 SHA-256。
- `providers[]`：改成直讀或程式的類別（第 2 節 1、3、6、7、8、9、10、18、19 的對應部分），各寫由哪個元件負責、用什麼驗（manifest SHA、程式 pin、或不驗）。
- `knownLimits[]`：本文件第 7 節的編號與這次實際碰到的數量（例如 L3 的地圖清單、L6 的不可用特效數、L7 的 unavailable profile 數）。
- 預熱清單的版本或雜湊（第 5 節）。

`ready` 的條件：總流程裡的每個 recipe 都完成它要求的全部項目，逐檔驗過，integrity 與收據寫回後再讀回比對。**已知限制不擋 `ready`**；manifest 裡的 unavailable 項目是輸出的一部分，也不算失敗。任何一個 recipe 失敗就不替換既有的 active 樹。

### 4.5 目錄與發布

- T451 現況是「recipe id = `engine/assets` 下的一個目錄」，單一 recipe 各自發布。總流程不能這樣做：`objects/` 同時有文字類的 `names.json` 與 SPR 類的外觀檔，`maps/` 同時有 manifest、terrain、effects。
- 定案：**recipe 的輸出路徑一律是 assets 樹的相對路徑**（例如 `text` 寫 `dialogs/templates.json` 與 `objects/names.json`）；recipe id 不必等於目錄名；每個檔只屬於一個 recipe（對照見第 6 節）。
- staging 仍是每個 recipe 一個目錄（`engine/runtime/assets-staging/<recipe-id>/`，各有自己的進行中收據，可以單獨續跑）。全部完成後，總流程把各 recipe 的檔案組成一棵新樹，寫入 `receipts/`、`prep-receipt.json`、`integrity.json`，整棵驗過再替換 `engine/assets`（同磁碟改名，中斷可復原，做法照 T451 的兩次改名）。這一段屬 T467。
- T451 的單一 recipe 發布保留給測試與程式庫使用。現有的 `inventory` recipe 把檔案直接寫在自己的根目錄；它不進總流程，所以不用改。日後若要納入，輸出要改成 `inventory/<icon>.png` 並把 `Version` 加一。
- junction：`engine/assets` 或它的祖先是 reparse point 時準備器拒絕寫入（T451 現況，維持）。開發機因此不會跑準備器，這是要的行為。祖先檢查要不要放寬見 D6。

### 4.6 核心還缺的部分與負責任務

T451 報告列出的缺口，分給第一個用到的任務：

| 缺口 | 負責 |
|---|---|
| Sprite 輸入從「17 個容器整包 pin」改成 1.1 的三層驗證 | T454 |
| Text 來源在續跑時的驗證（現在會被當成不符而重做） | T453 |
| 讀取「先前 recipe 的輸出」並記進收據（shadows 讀 body、清冊讀 objects、pickup 讀 players、maps 讀 terrain） | T455 |
| Tile 容器與 `map/` 散檔的輸入介面；大檔的串流寫入 | T456 |
| 多個 recipe 組成一棵樹的發布、integrity v2、總收據、`ready` | T467 |

這些任務會改到同一批核心檔案（`AssetPrepInput.cs`、`AssetPrepRunner.cs`），同步 main 時自己解衝突。

## 5. gfx 範圍（定案：遇到再準備；首次只預熱）

依 T447 的建議採「**遇到新 gfx 再準備**」，不做一次全量。全量要處理 2.61–4.14 GB 原始 SPR（目前實際用到的 44–70 倍），照現行 JSON 約 22–35 GB，還得先替 384 個缺檔 gfx 與 2,580 個跨封存同名不同大小的檔定全域政策（`action-timing.md` B）。

落實分兩步：

1. **M8 範圍內（T454、T455、T463、T467）**：
   - 每個 SPR 類 recipe 以「項目」為單位（T451 核心已有的概念）：`objects` 的項目是外觀檔名（`<gfx>`、`<gfx>-weapon-<n>`、`magic-<gfx>`、`projectile-<gfx>`、`world-<gfx>`），`players` 是 `<gfx>-<weapon>`，`shops`、`shadows`、`doors` 是 `<gfx>`。任何合法的項目都能單獨準備，不依賴清冊。
   - 首次準備只做一份**預熱清單**：產品目前支援的那批外觀，也就是 r29 基線七份清冊的成員（13 個職業 gfx 的 42 個姿勢、15＋19＋6 個物件與怪物、11 個商店、17 個陰影、1 個門、8 個魔法、1 個飛行物）。清單只有整數編號，寫在程式裡一個檔（建議 `engine/source/AssetPrep/PreheatList.cs`）。
   - 輸出格式沿用現行 JSON，讓既有讀取程式不用改，也讓 T470 能和基線逐項比。
   - 各 manifest（`objects/manifest.json` 的 `gfx`、`players/manifest.json` 的 `profiles`／`unavailable`、兩份清冊檔）由實際準備的結果產生，不是抄清單。
2. **執行期按需（新任務 T477、T478，見第 10 節）**：封包（S87、S76）給了預熱清單以外的 gfx 時，在背景執行緒用同一套 recipe 解碼，完成前沿用現有的 pending 表現（`AppearancePending`、`SelfAppearancePending`、缺圖退回 gfx 1120）。結果只放記憶體，不寫 `engine/assets`（1.3 第 1 點）；要不要另做磁碟快取、用什麼緊湊格式，留給那兩張任務評估，不沿用現行 JSON（每像素約 17.5 bytes，是原始 SPR 的 8.5 倍）。

預熱清單的性質要講清楚：成員當初是研究時挑的（職業 gfx 出自伺服器 JAR 的職業常數，怪物與 NPC 出自當時遇到的與 SQL spawn）。它不是客戶端資料，也不是用來補缺口的資料：gfx 永遠由伺服器封包給，清單只決定「先解哪些」，增刪不影響正確性。是否接受它留在程式裡，見 D5。

職業 gfx 集合：產品不做「職業 → gfx」換算（`action-timing.md` A3）。`PlayerProfiles.IsCharacter` 等讀取點在按需準備完成前仍以已準備的 profile 為準；T461 不需要另建職業表。

## 6. recipe 對照與先後（定案）

| 順序 | recipe id | 擁有的輸出 | 需要先完成 | 任務 |
|---:|---|---|---|---|
| 1 | `text` | `dialogs/templates.json`、`objects/names.json` | — | T453 |
| 1 | `character` | `character/blocks.json`、`character/animation.json` | — | T454 |
| 1 | `objects` | `objects/*.json`（外觀檔與 `manifest.json`，不含 `names.json`） | — | T454 |
| 1 | `doors` | `doors/*` | — | T455 |
| 1 | `shops` | `shops/*` | — | T455 |
| 1 | `terrain` | `maps/terrain/*.bin`、`terrain0.bin`、`cache/original-map0.txt` | — | T458 |
| 1 | `scene` | `scene/manifest.json`、`draw-commands.json`、`tile-pixels.json`、`opacity.json` | — | T457 |
| 1 | `map-effects` | `maps/effects/*` | — | T457 |
| 2 | `players` | `players/*` | `objects`（gfx 0 的 4 個 profile 指向 `objects/0*.json`） | T455 |
| 2 | `appearance-lists` | `encountered-monsters.json`、`world-appearances.json` | `objects`（清冊記外觀檔的 SHA、幀數、長度） | T455 |
| 2 | `maps` | `maps/manifest.json`、`cache/manifest.json` | `terrain`（要寫 terrain 檔的 SHA） | T456；terrain 欄位由 T458 補上 |
| 3 | `shadows` | `shadows/*` | `players`、`objects`（frame keys 取 body 序列引用的 key） | T455 |
| 3 | `pickup` | `pickup/*` | `players`（第 0 步的幀與 42 筆適用清單跟著 players） | T463 |

- 同一順序的 recipe 彼此獨立，可以平行。
- 先後錯誤（要讀的前一個 recipe 還沒完成）時明確失敗，不產生半套輸出。
- `maps` 與 `terrain` 都要掃 `map/` 目錄、算 bounds，共用同一段掃描程式（T456 寫，T458 重用）。T456 合併而 T458 還沒合併的那段時間，`maps/manifest.json` 沒有 terrain 欄位，總流程還不能跑；這是預期的，T467 依賴兩者。
- `map-effects` 要處理的 id 來自所有列入的 `.s32` 的 effects tail，也用同一段掃描；它不讀 `maps` recipe 的輸出。
- 執行期另有一個相依：`MapEffects.cs:158` 會讀 `objects/magic-<gfx>.json`。這是讀取時的事，不影響準備順序。

不是 recipe 的類別（直讀或程式）：`inventory`、`ground`、`dialogs/skills.json`、`magic/catalog.json`、`motion.json`、三張混色表、`sound`、`audio-manifest.json`、全部地圖原檔與 TIL、LAC。

## 7. 已知限制清單

**全部已定案（T476／#1072，2026-11-13）**。L1、L2 是使用者裁定（會改變或保留玩家看得到的行為）；L3–L7 是主控裁定。沒有任何一項用 SQL、JAR、研究輸出或既有 assets 補。

| # | 定案 | 裁定者 | 實作 |
|---|---|---|---|
| L1 | **A**：`OriginalExpTable.cs` 保留，列為 B15 唯一剩下的已知例外 | 使用者 | 不改程式；T464 只把理由寫進 allowlist 說明 |
| L2 | **X**：拿掉可丟棄白名單，能不能丟由伺服器裁定；丟棄前的確認視窗保留 | 使用者 | T464 |
| L3 | T470 比對標準照下文 | 主控 | T458、T470 |
| L4 | 新 manifest 拿掉 `maps[].name` | 主控 | T456（已合併） |
| L5 | 照 R1 算出的值；不在客戶端加表 | 主控 | T461、T474 |
| L6 | 知悉，列已知限制，照樣重現不可用清單 | 主控 | T457 |
| L7 | 照樣重現；gfx 138 放寬等 T470 過了再另開，現在不開 | 主控 | T455 |

### L1　EXP 門檻表（`engine/source/OriginalExpTable.cs`）— blocked

- **缺什麼**：每一級的累計 EXP 門檻（產品現有 101 個值，檔頭註明出自原服 SQL）。原版把表寫死在執行檔的 `.data`；24 個 idx、398 個解出的檔案都沒有；伺服器 S113 只送等級與 EXP 低 32 位。而且客戶端的表與產品的表本來就不同（和 TW16 表 A 差 42 項，和 3.81 快照差 46 項），就算讀得到客戶端的表也還原不出現值。出處：`rule-tables.md` 第 3 節（含查過的 pak 與檔名清單、VA／File Offset）。
- **對遊戲的影響**：只影響 EXP 百分比與「下一級還差多少」的顯示（左狀態框、`ExperienceText`）。等級與 EXP 原值不受影響。
- **選項**：
  - A　保留為已知例外：檔案留在 `b15Exceptions`，B15 的例外清不到 0。
  - B　拿掉表：`DecodeExperience` 不查表，`Percent`、`LevelStart`、`NextLevel` 一律空白；畫面的 EXP% 顯示為空（不可以填假值）。
  - C　使用者自備：準備器接受使用者提供的門檻檔，沒有就走 B。
- **T452 的建議**：B（M8 驗收文字寫的是例外歸零；代價是少一個顯示）。
- **定案（使用者，2026-11-13）：A**。`OriginalExpTable.cs` 保留在 `b15Exceptions`，是 B15 例外清單裡**唯一剩下的一列**；EXP% 與「下一級還差多少」的顯示維持現狀（只對 P24Lab 這類用同一份門檻表的伺服器正確）。因此：
  - M8 驗收文字「9 個已知例外歸零」改成「只剩 `OriginalExpTable.cs` 一個，理由寫在 allowlist 與本節」（`TASKS.md` M8 驗收已同步）。
  - T464 不改這個檔，只在 allowlist 的說明欄寫理由並指向本節；檔案內容與 SHA 不變。
  - T471 不清空 `b15Exceptions`：留這一列，CI 仍以精確路徑加 SHA 鎖定，不能增加、不能改內容；例外機制本身保留。
  - T473 README 的已知限制寫明：EXP% 顯示用的是產品內建門檻表，伺服器的門檻不同時顯示會不準。
  - C（使用者自備門檻檔）不在 M8 範圍，日後要做另開任務。
- **交接**：T464 只寫說明；T471、T473 照上列。
- **實作（T464／#1049）**：`engine/tools/game-assets-allowlist.json` 新增 `b15ExceptionReasons`，這一列的理由指向本節。`check-no-game-assets.py` 現在要求每個 B15 例外都要有理由，執行時逐列印出（`B15-EXCEPTION:`），輸出 JSON 也帶例外數。`OriginalExpTable.cs` 的內容與 SHA 都沒動。

### L2　`ground-catalog.json` 的 7 筆模板列 — 不改行為就是 blocked

- **缺什麼**：背包圖示與地面 gfx 的對應、`wireUseType`、`stackable`、`bless`、`templateId`。這些是伺服器模板與封包的值，客戶端沒有按物品列出的表；7 個 `nameToken` 的名稱倒是查得到（`desc-c.tbl`）。出處：`rule-tables.md` 第 4 節。
- **對遊戲的影響**：這 7 列只被 `GroundCatalog.CanDrop` 當成「可丟棄白名單」。
- **選項**：
  - X　拿掉白名單：`CanDrop` 只留和模板無關的本機條件，能不能丟由伺服器裁定（原版客戶端也沒有物品模板表）。行為變更：任何物品都能送出丟棄封包，誤丟貴重物品的機會變大，伺服器拒絕時要看得到訊息。檔案裡從客戶端量得的 `groundGfx` 與 SPR pin 可以留在程式或交給準備器。
  - Y　維持 7 列，列為已知例外。
- **定案（使用者，2026-11-13）：X**。
  - `GroundCatalog.CanDrop` 拿掉 7 列模板白名單，只留和物品模板無關的本機條件；能不能丟由伺服器裁定。伺服器拒絕時畫面要看得到訊息（沿用既有的伺服器訊息顯示，不另造文字）。
  - **丟棄前的確認視窗保留**，所有物品一律經過確認。
  - `ground-catalog.json` 從 `b15Exceptions` 拿掉。檔案裡從客戶端量得的 `groundGfx` 與 SPR pin 若仍有讀取者，改成程式裡的規則或雜湊、或交給準備器；`wireUseType`、`stackable`、`bless`、`templateId` 這些伺服器模板值**不可以**搬進程式常數（1.3 第 6 點）。
  - 原版送丟棄封包前在本機檢查什麼沒有反組譯過（`rule-tables.md` 4.3），這一點仍是待驗證；T474 真機回歸要包含丟棄一般物品、丟棄後伺服器拒絕兩種情況。
- **交接**：T464 實作；T474 驗。
- **實作（T464／#1049，2026-11-13）**：`ground-catalog.json` 已從 git 與 csproj 移除，`b15Exceptions` 也拿掉這一列。`GroundCatalog.CanDrop` 只留和模板無關的條件：物件 ID 不是 0、數量大於 0、沒有充能數、沒裝備、不是武器或防具、`Bless < 128`、旗標正常。4 筆 SPR pin（gfx 7、36、45、66：entry、idx 筆號、PAK File Offset、大小、payload／RGBA SHA-256，出處 #48）留在 `GroundCatalog.Pinned`，不含任何伺服器模板欄位。原本的 3 筆（gfx 22、42、46）沒有 pin，現在跟其他 gfx 一樣走通用描述。原產品**沒有**「丟到地上」的確認視窗（只有給予與刪除有），所以這張補上：WinForms 的丟棄鈕與 Web 面板的丟棄，送出前都會跳出「確認丟棄物品」（預設按鈕是「否」）。伺服器拒絕時沿用 S71 的既有訊息顯示。詳見 [`issue-1049.md`](../reports/issue-1049.md)。

### L3　terrain 的區域位元：8 張地圖 13,567 格 — 部分 blocked

- **缺什麼**：map 42（13,075 格）、4（304）、0（112）、244（26）、2101（21）、2151（21）、70（7）、440（1）的 bit4–5（16、32）。基線取自伺服器 `server/maps/*.txt`；同一張圖裡同一個 attribute 值對到兩種基線值，只看客戶端 attribute 無法重現。連帶 `cache/original-map0.txt` 有 112 格不同。逐圖清單在 `terrain.md`「不符的地圖」與 `terrain-compare.csv`。
- **對遊戲的影響**：目前沒有。產品只讀 bit0、bit1（與從未出現的 bit7），通行判定 713 張、35,979,264 格完全相同。日後若要顯示安全區或戰鬥區，這 8 張圖會與伺服器不一致。
- **定案（主控）**：713 張全部照規則產生，接受這 8 個 terrain 檔與 `original-map0.txt` 和 r29 基線不同。**T470 的標準**：bounds 與 bit0–3 必須 713 張全部相同；bit4–5 的不符只能落在上列 8 張，格數不超過各自的數字（`original-map0.txt` 不超過 112 格）；bit6–7 必須全同。總收據的 `knownLimits` 記這 8 張。
- **交接**：T458 照做；T470 照標準比。

### L4　`maps/manifest.json` 的 `maps[].name` — blocked（欄位）

- **缺什麼**：地圖名稱，來自 SQL（14 張是 `unknow map`）。客戶端沒有對應來源，T450 沒有找替代。
- **對遊戲的影響**：沒有，產品不讀這一欄。
- **定案（主控）**：新 manifest 拿掉這一欄。日後要顯示地圖名稱時另開查證（候選：Text 的 `MapDesc-c.tbl`、`zone*.tbl`，都還沒解開看過）；M8 不開。
- **交接**：T456 照做（已合併）。

### L5　伺服器的 `spr_action` 無法由客戶端驗證 — 已閉合，但範圍有限

- **缺什麼**：伺服器擋加速用的是它自己的 `spr_action`（framecount、framerate）。基線記下的 59 列加 210 個玩家值全部等於客戶端 `list.spz` 的 tick 總和、framerate 全是 24，但那只涵蓋基線有的 gfx 與姿勢。基線以外的姿勢（斧、矛、杖、雙刀、鋼爪等）、變身外型，以及伺服器管理者改過的列，沒有資料可以對照。出處：`action-timing.md`「A 的已知限制」。
- **對遊戲的影響**：用基線以外的姿勢或變身時，算出的攻擊與施法間隔可能和伺服器不同，表現是伺服器回退或擋動作。
- **定案（主控）**：照 R1 算出的值用。真機遇到回退或擋動作時記進 Issue，不在客戶端加表補。
- **交接**：T461 照做；T474 真機回歸時留意；T473 README 列為使用者看得到的已知限制。

### L6　154 個地圖特效不可用（基線既有）

- **缺什麼**：141 個 id 在 `list.spr`／`list.spz` 都沒有條目，6 個幀尺寸不合法，3 個在多個封存檔內容不同，3 個動作有方向性，1 個沒有動作卻有多幀。另外 `4094.til` 在客戶端不存在（PakViewer 查過 19 個封存檔）。出處：`maps.md`。
- **對遊戲的影響**：這 154 個 id 在 80 張地圖被引用 3,280 次，那些位置不畫特效。r29 基線就是這樣，不是 B15 新增的缺口。
- **定案（主控，知悉）**：recipe 照樣重現不可用清單與原因，列為已知限制。原版怎麼處理沒有 metadata 的特效沒有查；M8 不開查證任務。
- **交接**：T457 照做；T473 README 列入。

### L7　15 筆玩家 profile 不可用（基線既有）

- **缺什麼**：12 筆是 `list.spz` 沒有那個姿勢的完整動作；3 筆是 gfx 138（男妖精）的 0／4／20，`list.spz` 有動作，但 SPR 有不透明像素落在幀 bounds 之外，研究工具因此拒收。
- **對遊戲的影響**：這些 gfx 與姿勢的組合畫不出來，走既有的缺圖退回。
- **定案（主控）**：recipe 重現同樣的 15 筆 unavailable（T470 才比得起來）。gfx 138 是工具的檢查太嚴，不是缺資源；**等 T470 等價驗收通過後**再另開任務放寬（功能擴大，要另外驗畫面），**現在不開**。這張任務由 T475 結案時依 T470 結果開。
- **交接**：T455 照做；T475 開 gfx 138 任務。

### 不屬於素材、順帶列出

- 加速與緩速係數（0.755、2.0，`NormalHaste.cs`、`ActionCadence.cs`）是產品既有常數，註解寫明出自 JAR d287。它們不是素材，七張閉合任務都沒有重新查證。T476 沒有另開查證（M8 不處理）。
- B15 例外清單現況（2026-11-13，`engine/tools/game-assets-allowlist.json`）：7 列。T466 已處理 2 列；剩 pickup 2 列（T463）、EXP 表（L1，定案保留）、ground-catalog（L2，T464 拿掉）、測試向量 3 列（T465）。M8 結束時的目標是 **1 列**（`OriginalExpTable.cs`）。
- T463（#1048）、T465（#1050）、T464（#1049）之後：**1 列**，只剩 `OriginalExpTable.cs`（L1，定案保留），已達 M8 目標。理由寫在 allowlist 的 `b15ExceptionReasons`，`check-no-game-assets.py` 輸出 `"b15Exceptions": 1`。

## 8. 其他決定（主控裁定，已定案）

這些不是 blocked，是做法的選擇。**全部由主控在 T476（#1072，2026-11-13）定案**；「T452 的暫行做法」欄保留原文供對照。

| # | 決定 | T452 的暫行做法 | 定案與實作 |
|---|---|---|---|
| D1 | **產品執行期的 Sprite pin 與本機客戶端不符**。`ClientArchive.cs` 的 `UiResourcePins.Sprites` 與 `GroundSpriteReader.cs` 的 `archivePins` 對 `Sprite03`–`Sprite14` 的 pin 已經和本機客戶端不同（2026-11-09 被附加 36 個 `.img`；誰改的沒有查）。照原始碼，產品現在開這 12 包會被拒絕；實際影響哪些畫面沒有驗證。選項：更新 pin、還原客戶端、或執行期也改成結構檢查加逐 entry | 準備器照 1.1 做（不受影響）。執行期的兩處 pin 本契約不動 | **定案：執行期也改成 1.1 的三層驗證**（結構檢查、錨點 entry、逐 entry 記錄），不更新整包 pin，也**不還原**本機客戶端被附加的 36 個 `.img`。另開 code 任務 **T479**（`b15-runtime-archive-verify`，規劃檔 `docs/plans/issue-1072.json`）。現在實際壞了哪些畫面由 T474 真機驗收時一併確認；T479 不等 T474 |
| D2 | **文字模板的兩件事**。(a) 176 個模板的買賣按鈕圖片編號在本機客戶端是 602／603（基線 312／314）；產品不換這兩個號碼，`Sprite.idx` 有這兩張圖，畫出來對不對沒看過。(b) `LiTo\LTtext` 有 4 個與 Text 同名但內容不同的 html（`inn`、`inn2`、`inn20`、`inn4`），原客戶端會不會優先讀它沒有查 | (a) 照本機來源輸出，T474 真機截圖確認。(b) 不疊加（與基線一致） | **定案**：(a) 照本機來源輸出，T474 截圖確認買賣按鈕。(b) 不疊加 `LiTo\LTtext`，**不另開**反組譯任務 |
| D3 | **地圖選取規則**。基線 713 張是「SQL 有、伺服器有 terrain、客戶端有目錄」的交集，新規則只能看客戶端 | `map/` 下目錄名是十進位數字、且至少有一個檔名為 8 個十六進位字元的 `.s32` 的目錄都收。區塊只收檔名為 8 個十六進位字元的 `.s32`／`.seg`（`tempseg.s32` 5 個與 `800F7FF3E.s32` 不收，產品本來就讀不到）。一張圖只要有任何區塊解析失敗，整張列進 `unsupportedMaps`，不產生 terrain（明確不支援，不是當成全部可走）。結果預期：基線 713 張加 4472、12000 兩張；12004（512 個區塊有多餘資料）不支援；18 只有 `.seg`，不收 | **定案：照左欄規則**。T456 回報不收那 6 個檔之後 `tiles[]` 與特效 id 集合的差異；T470 只對基線有的 713 張比，多出的 2 張只列數字 |
| D4 | **objects 的攻擊與施法動畫長度**。基線用 `40 × ticks`（把伺服器間隔當成動畫長度），原版是每 tick `1000 ÷ 24` | 重現基線（3.2） | **定案：重現基線（`40 × ticks`）**。改回原生換算等 T470 等價驗收通過後再議（怪物動作會慢約 4%，要另外驗畫面）；M8 不改 |
| D5 | **預熱清單留在程式裡**（第 5 節）。成員出自研究時的選擇，只有整數編號 | 留，內容等於 r29 基線七份清冊的成員 | **定案：留在程式裡**（`engine/source/AssetPrep/PreheatList.cs` 或 T455 選的位置），內容等於 r29 基線七份清冊的成員，只有整數編號 |
| D6 | **junction 檢查的範圍**。T451 檢查到磁碟根目錄：使用者把引擎解壓在經由 junction 到達的路徑下會被拒絕 | 維持嚴格。T468 的錯誤訊息要指出是哪一層 | **定案：維持嚴格**。T468 的錯誤訊息要指出是哪一層路徑是 reparse point。放寬只在使用者實際回報時再考慮 |
| D7 | **技能顯示的兩個差異（知悉即可）**。技能名稱改用客戶端表後，6 個名稱會和現在不同（id 30、111、115、119、120、213）；「對象：施法者 → 只對自己」是自主規則，只有 4 筆有基線可比 | 照 `rule-tables.md` | **定案：知悉**，照 `rule-tables.md`（T462 已合併） |
| D8 | **是否做執行期按需準備**（T477、T478） | 兩張任務排在裁定之後 | **定案：做**。T477（#1073）、T478（#1074）保留，依賴本張的前提已成立 |

**主線分母**：T476–T478 計入 M8 主線（主控裁定，410 → 413）；D1 的 T479 也計入（→ 414）。四張都歸在 `PLAN.md` 不確定項 U10（B15 升級）之下。

## 9. 後續任務的範圍修正

與 `docs/plans/issue-1027.json` 原文不同的地方。沒列的照原文。

| 任務 | 修正 |
|---|---|
| T453 文字類 | recipe id `text`。只產生 `templates.json` 與 `names.json`；skills、magic 不在這裡（原 T462 文字的「加進 T453 的文字 recipe」作廢）。補上 Text 來源的續跑驗證（4.6）。驗收標準照 3.1 |
| T454 SPR 共用層、character、objects | 先把 Sprite 輸入驗證改成 1.1 的三層（4.6），否則真客戶端跑不起來。`character` 只產生 2 個 JSON，**不產生 PNG**（原文「產品實際會讀的 PNG 幀」＝ 0 張），`Tests.cs:19` 一起改。`objects` 以項目為單位，`manifest.json` 由準備結果產生。時間欄位照 3.2，不讀 SQL |
| T455 其餘 SPR 類 | recipe：`players`、`shops`、`doors`、`appearance-lists`、`shadows`。加「讀先前 recipe 輸出」的核心介面（4.6）。**`ground` 不產生**，只確認讀取點。`serverActionMs` 沒有 blocked 欄位，照 R1 填。預熱清單檔在這張建立（T454 先放自己那部分也可以） |
| T456 地圖原檔 | **採 provider 直讀，不複製**（3.3）。recipe `maps` 產生兩個 manifest；`cache/map`、`cache/tiles` 不產生。地圖選取照 D3 的定案規則。Tile 與 `map/` 的輸入介面、串流寫入在這張加（4.6）。不疊 Tile.pak 整包 pin |
| T457 scene 與特效 | recipe `scene`（4 檔，不產生 9 個預覽檔）與 `map-effects`。**6016 的 pin 不改**，輸出必須逐 byte 相同（3.5） |
| T458 terrain | **要寫程式**（原文「T445 判 blocked 時不寫程式」不適用：T445 是部分 pass）。713 張全部產生，不跳過 L3 的 8 張（原文「只對相符的地圖產生」作廢）。`maps` recipe 的 terrain 欄位由這張補上。解析失敗的地圖照 D3 列為不支援 |
| T459 motion 與混色 | 兩者都**改成程式**，不由準備器寫檔。沒有 blocked 的表 |
| T460 音效 | **採直讀加程式 pin**，`audio-manifest.json` 不再是檔案（3.6） |
| T461 動作時序 | 沒有 blocked 欄位。規則照 `action-timing.md` R1–R3；objects 動畫長度照 D4 的定案（重現基線）。不建職業 gfx 表（第 5 節）。`serverActionMs.walk` 與 `Magic.CastMsFor` 沒有使用者，可以順手清掉 |
| T462 skills 與 magic | 兩張表都**不產生檔案**，執行期直讀 Text。沒有 blocked 的部分 |
| T463 pickup | recipe `pickup`，排在 `players` 之後。輸出在 `pickup/` |
| T464 EXP 表與 ground-catalog | **L1＝A、L2＝X（使用者裁定，T476）**。`OriginalExpTable.cs` 不動，只把保留理由寫進 allowlist 的說明欄並指向第 7 節 L1。`ground-catalog.json`：照 L2 定案拿掉白名單、保留確認視窗、從 `b15Exceptions` 移除；伺服器模板值不得搬進程式 |
| T465、T466 | 不變（T466 已完成） |
| T467 總流程 | 照第 4 節與第 6 節：多 recipe 一棵樹的發布、integrity v2、總收據、`Assets.VerifyIntegrity` 接受兩種 schema。總流程的 recipe 集合是第 6 節那 13 個；`inventory` 不在內。已知限制不擋 `ready` |
| T468 launcher | 三種狀態照 4.3 最後一列。開發機（junction）行為不變 |
| T469 | 已完成 |
| T470 實跑 | 比對標準照第 2 節的「比對」欄、1.4 與 L3。另外：兩個輸出目錄的 integrity 與收據逐 byte 相同；20 張基線 inventory PNG 與 2 張 ground PNG 對執行期解碼結果比 P；量各 recipe 的耗時、active 樹大小、首次遇到新 gfx 的停頓；回報 D3 多出的地圖與集合差異。本機客戶端的 `Sprite03`–`Sprite14` 與產品 pin 不符是已知狀況（D1，由 T479 處理），不是準備器的失敗 |
| T471 CI 收尾 | **例外數目標是 1**（L1＝A）：`b15Exceptions` 只留 `engine/source/OriginalExpTable.cs`，以精確路徑加 SHA 鎖定；例外機制保留，不移除。腳本輸出與報告明列這一列與理由 |
| T473 README | 「支援的輸入版本」寫 1.2 的錨點；已知限制寫使用者看得到的項目：L1（EXP% 用內建門檻表，伺服器不同時不準）、L2（能不能丟由伺服器決定）、L5、L6、L7 |
| T474 真機 | 回歸項目加：NPC 商店頁的買賣按鈕（D2a）、EXP% 顯示維持現狀（L1）、丟棄一般物品與伺服器拒絕丟棄兩種情況、確認視窗仍出現（L2）、本機客戶端下 `Sprite03`–`Sprite14` 相關畫面（D1，T479 合併前後各看一次為佳） |
| T475 結案 | 第 7、8 節已在 T476 定案；T475 只依 T470、T474 的實跑結果補數字。T470 等價驗收通過時，開 gfx 138 放寬任務（L7） |
| T479 執行期封存驗證（新） | D1 定案：`UiResourcePins`（Sprite、Tile）與 `GroundSpriteReader.archivePins` 的整包 pin 改成 1.1 的三層驗證。範圍見第 10 節 |

## 10. 新增任務

規劃檔 `docs/plans/issue-1037.json`。

| 編號 | id | 內容 | 前置 |
|---|---|---|---|
| T476 | `b15-limits-ruling` | 主控裁定第 7 節 L1–L7 與第 8 節 D1–D8，把結果寫回本文件 | — |
| T477 | `b15-ondemand-appearance-a` | 執行期按需準備：物件、怪物、NPC（`ObjectAssets`） | T476；T454、T455 已合併 |
| T478 | `b15-ondemand-appearance-b` | 執行期按需準備：玩家姿勢、陰影、商店、門、魔法與飛行物 | T477 |
| T479 | `b15-runtime-archive-verify` | 執行期封存驗證改三層（D1），規劃檔 `docs/plans/issue-1072.json` | T476 |

T476–T479 四張都計入 M8 主線分母（T476 裁定，`PLAN.md` 已同步：410 → 414）。

## 11. 已驗證／推測／待驗證

**已驗證（引自七張閉合報告，都是 Python 原型對 r29 基線的全量比對）**：第 2 節每一列的閉合結論；第 7 節 L1–L7 的數字；1.1 第 3 點的 12 組容器現況。本張另外讀原始碼確認的：七個用 integrity 驗檔的讀取點與行號（4.3）、`AssetPrepCore.cs` 的「recipe id = 目錄」註解、`AudioAssets.Load` 驗 manifest SHA 的方式、B15 例外清單現有 7 列。

**推測**：

- 第 4 節的格式能讓六個讀取點完全不用改：依據是它們只用 `files[]` 的 `path`／`size`／`sha256`，沒有逐一試過。
- 新 active 樹的大小（約 245 MB 加 0.5 GB）是從基線檔案大小推的。
- 不收 6 個檔名不合規的 `.s32` 之後，`tiles[]` 與特效 id 集合不變或只變小：沒有算過（D3）。

**待驗證**：

- C# 準備器的全部輸出（T470）。目前所有等價數字都來自 Python 原型。
- C# 能不能做到 `6016.json`、scene 三檔、`names.json` 逐 byte 相同。
- 本機客戶端下產品執行期開 `Sprite03`–`Sprite14` 的實際表現（D1）。
- 602／603 的畫面（D2）；基線以外姿勢的伺服器間隔（L5）。
- 本張沒有建置、沒有執行任何程式。
