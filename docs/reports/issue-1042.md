# Issue #1042 報告：T457 準備器 scene 與 maps/effects（含 6016）

日期：2026-11-13。執行者：本機 Claude（claude-local）。分支 `task/1042`，PR #1080。
依據：`docs/design/asset-recipe.md` §1.2、§1.3、§3.5、§4.3、§4.5、§6、§9（T457）；`docs/reference/asset-recipes/maps.md`「scene 的三個執行檔」「scene/opacity.json」「maps/effects」。

## 結論

- 兩個 recipe 寫完：`scene`（4 檔）與 `map-effects`（每個可用 id 一檔，加上 manifest）。schema 字串維持 `p22-dual-map-v1`、`p23-original-opacity-v1`、`p24-map-effects-v1`。
- **6016 的 pin 不改**（§3.5）。產品 pin 與準備器的輸出錨點現在是同一個常數 `Effect6016Definition.AcceptedMetadataSha256`（值仍是 `825673b1…263383`）；產出的 `6016.json` 整檔 SHA 不等於它時 recipe 停止，不改 pin。
- 讀取程式：`Assets.cs` 只把 scene 載入抽成 `Assets.LoadScene`（行為不變，方便測試）；`SceneOpacity.cs`、`MapEffects.cs` 不用改。理由見下方「讀取點」。
- 本機只編譯，測試交給 CI（2026-11-09 本機執行限制）。

## 做了什麼

### 1. recipe `scene`（`engine/source/AssetPrep/SceneRecipe.cs`）

- 來源：`Tile` ＋ `map/`（`IAssetRecipeSources`）。項目只有一個：`scene`。Coverage = Complete。
- 輸入：`map/0/7ffd8002.s32`（北，編號 0）、`7ffd8003.s32`（南，編號 1），檔名不分大小寫；它們用到的 TIL 從 Tile.pak 依名稱取。
- `scene/draw-commands.json`：兩個區塊各自依 ground、cover、objects 展開；欄位順序 `layer, source_index, serial, block, original_source_index`，再接該筆原始欄位（ground／cover：`x, y, index, tile, uk, offset`；objects：`x, y, z, index, tile, uk, group, group_ordinal, offset`），`y + 64 × 編號`、`serial = source_index + (1 − 編號) × 100000`；以 `(py, px, ground 0／其餘 1, z, −serial)` 穩定排序。objects 的 tile 照基線用有號 i16。
- `scene/tile-pixels.json`：用到的 (tile, index)，tile 由小到大、index 由小到大；菱形（`type & 2 == 0`）與 RLE 兩種解法照 `maps.md`。塊的結尾取 offset 表中下一個較大的值，解完必須剛好用完。
- `scene/manifest.json`：`canvas_size = [max(px)+24, max(py)+24+756]`、`draw_count`，其餘是固定值與五個說明字串（與基線相同）。
- 三個檔都是緊湊 JSON（無空白、無換行、無檔尾換行）。
- `scene/opacity.json`：先南（ybase 64、slot 2）再北（ybase 0、slot 0）；`maps[]` 只留 `block, sha256, slot, ybase, groups, triggers`（**拿掉 `path`**）；`groups[]`、`triggers[]` 欄位與 `admittedByColdSlot` 規則照 `maps.md`；`ordering`、`scope` 兩個固定字串照基線。indent 2、CRLF、檔尾沒有換行。
- **不產生** 9 個預覽／P05 檔（`map.json`、`tiles.json`、`region.rgb555`、6 張 PNG 含 `atlas.png`）：契約 §2 第 13 列與 §9 T457 定案「4 檔」，產品沒有讀取點。任務原文列了 atlas，依契約（衝突時以契約為準）不做。
- 拒絕：缺區塊、缺 TIL → `MissingEntry`；區塊截斷、沒有 effects tail、結尾多餘資料、TIL 塊壞 → `CorruptEntry`。

### 2. recipe `map-effects`（`engine/source/AssetPrep/MapEffectsRecipe.cs`）

- 來源：17 個 Sprite 容器 ＋ `map/`；Text 的 `list.spr`、`list.spz`。項目只有一個：`effects`。Coverage = Complete。
- id 集合：所有列入的 `.s32` 的 effects tail。選取與 `maps` recipe 同一套（`MapScan.Plan`／`Parse`，D3）：檔名不合規的 `.s32` 不讀、一張圖只要有一個區塊解析失敗整張不算、`.seg` 只參與解析檢查不提供 id。依 #1041 的 Python 對照，真客戶端是 498 個 id（基線 490 個加上 4472、12000 兩張圖帶進來的 8 個）。
- 每個 id：在 17 個容器找 `<id>-0.spr`（不分大小寫，**全部 17 個都看**，內容不同 → `ambiguous archive content`）；`list.spr` 與 `list.spz` 各切一段 metadata，必須相同；動作 3、沒有再找動作 0；`104.attr(n)`；SPR 解碼用比 `SprFile` 嚴格的 `MapEffectSpr`（不收空幀、像素必須在 bounds 內、每幀至少一個像素）。不可用時原因字串與基線相同（`no sprite candidate`、`ambiguous archive content`、`no original animation metadata`、`list.spr/SPZ disagree`、`unresolved stationary direction`、`multiple frames without animation metadata`、`invalid frame dimensions`、`opaque pixel outside frame`、空字串……）。
- `maps/effects/<id>.json`：鍵順序 `id, flags, sequence, frames, entrySha256, spriteSha256, tickRate, eventPolicy`；緊湊 JSON，檔尾 CRLF。
- `maps/effects/manifest.json`：`schema, effects[], unavailable[], scope`；`effects[]` 每列 `id, path, sha256, flags, frames, source, metadataSha256, sequence`，**`source` 改成 `{archive, name, size, sha256}`**（容器名稱與 idx 裡的 entry 名稱，不含路徑與 record 位置）。檔尾 CRLF。
- 6016 錨點：產出的 `6016.json` 與 `AssetPrepPins.Effect6016Sha256`（= 產品 pin）不同 → `ContainerMismatch`，訊息寫出輸出檔、來源容器與 entry；6016 不可用或沒有任何區塊引用 → `MissingEntry`。兩者都不發布。`list.spr` 錨點（`523c61ad…`）不符 → `ContainerMismatch`（容器 Text）。

### 3. 核心與讀取點

| 檔案 | 改了什麼 |
|---|---|
| `AssetPrepInput.cs` | `AssetPrepPins` 加 `ListSprSha256`、`Effect6016Sha256`（產品值寫在 `Tw1608262200`）。`Open` 允許 Sprite 與 Tile／map 同時開（原本拒絕混用；map-effects 需要 Sprite＋map） |
| `AssetPrepRunner.cs` | context 加 `TryReadSpriteSource`（帶來源紀錄，manifest 要 entry 名稱與大小）、`ReadListSpr`、`Pins` |
| `SprShared.cs` | `SpriteList.Decoded`（解完的 list.spz，切 metadata 用） |
| `Effect6016Definition.cs` | `AcceptedMetadataSha256` 常數（值不變） |
| `ClientEffect6016.cs` | `Effect6016Pin` 改指向上面的常數（值不變，行為不變） |
| `Assets.cs` | scene 載入抽成 `public static LoadScene(root, list)`，建構式照舊呼叫；檢查與錯誤訊息不變 |

讀取點（§4.3）：

- `Assets.cs` 的 scene 三檔沒有個別 SHA，由 `VerifyIntegrity` 依 integrity 核對；新檔列進 integrity 是 T467。`VerifyIntegrity` 接受 v2 也是 T467，本張沒動。
- `SceneOpacity.cs`：讀 `opacity.json`、`tile-pixels.json`、`draw-commands.json`，沒有 SHA；產品不讀 `maps[]`，拿掉 `path` 不影響。不用改。
- `MapEffects.cs`：manifest 的 SHA 取自 integrity 的 `maps/effects/manifest.json` 那一列，每個特效檔的 SHA 取自 manifest；`source` 欄位產品不讀。不用改。
- `ClientEffect6016.cs`：整檔 pin，值不改（契約 §3.5「pin 不改」）。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`（0 警告、0 錯誤，增量）、`dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`（0 錯誤）。**沒有在本機執行任何編譯出來的程式**。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4,691 個追蹤檔，0 錯誤）。
- 新測試 `engine/_integration/dev/asset-prep-tests/SceneEffectsRecipeChecks.cs`：`[CheckGroup("asset-prep-scene")]`，同時掛 CI 已在跑的 `map-continuity`（Order 310）與預設全套。全部合成資料，寫在 `scratch/issue-1042/run-<時間>/`：
  - scene：兩個輸出目錄逐 byte 相同；只有 4 個產品檔；不含絕對路徑；manifest **全文**逐 byte 等於期望字串（固定字串取自基線格式）；draw-commands 開頭兩筆的確切 bytes、鍵順序、serial／y 偏移、ground 的 `index／uk／offset`、cover 的 File Offset；tile-pixels 的 (tile, index, type) 清單、菱形與 RLE 像素；opacity 的格式（indent 2、CRLF、檔尾無換行、南先北後）、`maps[]` 無 path、groups 的 `globalY`、triggers 的 `bits`／`admittedByColdSlot`；**產品 `Assets.LoadScene` 與 `OpacityCatalog` 載入輸出**；已是最新；缺南區塊、缺 TIL、截斷、沒有 tail、多餘資料、未知項目都拒絕。
  - map-effects：metadata 切段規則（`#70` 不是 `#7`、`#7x` 不結束 entry）；SPR 拒絕原因字串；兩個輸出目錄逐 byte 相同；**5 個特效檔（含 6016）的整檔 bytes 等於測試端用字串自己拼出的 Python `json.dumps` 格式**（不經 Utf8JsonWriter）；manifest 鍵、`source` 四欄、6016 來源 `Sprite13`、替代包相同內容可收、10 個不可用 id 與原因；tempseg 與解析失敗的地圖不提供 id；**產品 `MapEffectCatalog`（integrity 取 manifest SHA）與 `Effect6016Definition.FromR27Metadata` 載入輸出**，合成 6016 被真 pin 拒絕（pin 沒有放寬）；6016 pin 不符 → 停止並指出 `Sprite13` 與 `6016-0.SPR`；沒有 6016 → 拒絕；list.spr／list.spz 錨點不符、缺 list.spr 錨點、未知項目都拒絕；區塊改動後續跑重做。
- PR #1080 的 CI：見文末「CI 結果」。

## 未驗證（待驗證）

- **C# 輸出在真客戶端上對 r29 基線的比對**（scene 三檔 B、opacity S、336 個特效檔 B 含 `6016.json`、manifest S、154 個不可用原因）：T470。本機規定不能執行編譯出來的程式；本張的逐 byte 證據只有合成資料（格式）與 T450 的 Python 原型（規則）。若 T470 發現 6016 不同，照契約停下回報，不改 pin。
- 新收的 8 個特效 id（來自 4472、12000）各自可不可用：沒有算。
- `Assets.VerifyIntegrity` 接受 v2、把 scene／effects 列進 v2 integrity：T467。
- 真機（地圖特效、6016 音效、場景透明）：T474。

## 產出路徑

- 程式：`engine/source/AssetPrep/SceneRecipe.cs`、`engine/source/AssetPrep/MapEffectsRecipe.cs`；修改 `AssetPrepInput.cs`、`AssetPrepRunner.cs`、`SprShared.cs`、`Assets.cs`、`ClientEffect6016.cs`、`Effect6016Definition.cs`。
- 測試：`engine/_integration/dev/asset-prep-tests/SceneEffectsRecipeChecks.cs`。
- 報告：本檔。沒有改 `rebuild-lock.json`、`docs/REPORT.md`；沒有提交任何像素或客戶端資料。

## 給下一棒的提示

- **T467 總流程**：recipe id 是 `scene` 與 `map-effects`，輸出路徑已是 assets 樹的相對路徑（`scene/*`、`maps/effects/*`）；單一 recipe 發布時落在 `assets/scene/scene/…`、`assets/map-effects/maps/effects/…`。`maps/effects/manifest.json` 一定要列進 integrity（`MapEffectCatalog` 從那裡取 SHA）。總收據的 `knownLimits` L6 可以直接用這個 recipe 收據的 `unavailable` 與 `unavailable:<原因>` metadata。
- **T470**：比對時 `opacity.json` 少了 `maps[].path`、特效 manifest 的 `source` 換了形狀（S 比對要略過這兩處）；特效 id 會多 8 個（D3）。`6016.json` 要求 B，C# 輸出若不同，查 JSON 格式（鍵順序、無空白、CRLF、`<` 不跳脫）而不是改 pin。
- `AssetPrepInput.Open` 現在允許任何 Sprite／Tile／map 組合；`sources == 0` 或未知旗標仍拒絕。
- 6016 錨點在 `AssetPrepPins.Effect6016Sha256`；只有合成測試會設成 null（沒有 6016 的資料）。
