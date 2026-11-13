# Issue #1041 報告：T456 地圖原檔 provider 與 maps／cache manifest

日期：2026-11-13。執行者：本機 Claude（claude-local）。分支 `task/1041`，PR #1077。
依據：`docs/design/asset-recipe.md` §1.1、§1.2、§3.3、§4.6、§6、D3；`docs/reference/asset-recipes/maps.md`、`terrain.md`。

## 結論

- 採契約定案的 **provider 直讀**：S32／SEG 從 `TW_CLIENT_ROOT/map/<目錄>/<檔名>` 讀，TIL 與 `Water.lac`／`pier1.lac`／`pier5.lac` 從 `Tile.idx`／`Tile.pak` 依名稱取 entry。沒有複製任何 map／TIL 進 git 或 `engine/assets`；`cache/map`、`cache/tiles` 不產生。
- recipe `maps` 產生 `maps/manifest.json`（`p24-map-provider-v1`）與 `cache/manifest.json`（`p24-original-cache-v1`）。terrain 欄位照任務留給 T458。
- `OriginalMapCache.cs`、`Portals.cs`、`ShipBackdrop.cs` 改走同一個 provider；在現有的 r29 樹（integrity v1）上讀的檔、SHA 檢查與錯誤訊息都不變。
- 本機只編譯，測試交給 CI（依 2026-11-09 本機執行限制）。

## 做了什麼

### 1. provider：`engine/source/MapRawSource.cs`

來源由 `assets/integrity.json` 的 schema 決定，一棵樹只有一種、不互相退回：

| schema | 模式 | 區塊 | TIL | LAC |
|---|---|---|---|---|
| `p06-assets-integrity-v1`、沒有 schema（測試 fixture）、沒有 integrity 檔 | Packaged（現況） | `assets/maps/<blocks[].path>`；cache 模式 `assets/cache/map/0/<name>` | `assets/maps/tiles`（cache：`assets/cache/tiles`） | `assets/maps/ship/<名稱>` |
| `tw-assets-integrity-v2`（T467 的準備器輸出） | Client | `TW_CLIENT_ROOT/<blocks[].path>`（最後一段不分大小寫） | `Tile.pak` 的 `<id>.til` | `Tile.pak` 的同名 entry（不分大小寫，`Pier1.lac` → `pier1.lac`） |

- SHA 照舊：區塊與 TIL 用 manifest 的值，SEG 與 LAC 用程式裡的 pin（沒改）。客戶端在準備後被改動 → `InvalidDataException`，Tile 沒有該 entry → `FileNotFoundException`，客戶端目錄不存在 → `DirectoryNotFoundException`。
- Client 模式下 `cache/manifest.json` 也照 integrity 核對 SHA（Packaged 模式維持原本不核對的行為）。
- `PlainArchive`：Tile 的結構檢查（idx 大小 = 筆數 × 28 ＋ 4、entry 不超出 pak、名稱不重複），記 idx SHA，**不 pin 整包 Tile.pak**（§1.1 第 3 點）。產品既有的 `ClientArchive`／`UiResourcePins.Tile` 沒動（D1 範圍外）。
- client 路徑只接受 `map/<十進位>/<8 個十六進位>.(s32|seg)`；LAC 名稱只接受 `<英數>.lac`。
- `WorldSession.MapClientRoot`（`MapResources.cs`，null = `UiResourcePins.ClientRoot`，即 `TW_CLIENT_ROOT`）傳給 `WorldEntry`、`SceneOpacity`、`Portals` 的讀取點；`OriginalMapCache`／`OriginalMapStore` 建構式多一個可省略的 `clientRoot`。

### 2. 共用掃描：`engine/source/AssetPrep/MapScan.cs`（T458 重用）

- 地圖目錄：十進位名稱（不收前導 0），至少一個 `<8 hex>.s32`（D3）。
- 區塊檔：只收 `<8 hex>.s32`／`.seg`（副檔名不分大小寫）；同一區塊兩種都有用 `.s32`；只有 `.seg` 時只有 map 6 `7fff8000` 列為區塊（錨點），其餘 `.seg` 進 `unsupportedBlocks`。
- bounds：`.s32` 與 `.seg` 名稱一起算，`x0 = (bx − 0x7E00) × 64`，max ＋ 63（`terrain.md`）。
- `verifiedAbsent`／`unsupportedBlocks`：bounds ± 2 區塊（bx 外層），四種副檔名都不存在 → absent，否則 unsupported（`maps.md`）。
- `Parse`：照產品讀取的區段順序做結構走訪，回傳 tile id、portal 數、`noEffectTail`、特效 id；effects tail 之後有多餘資料就拒絕。

### 3. recipe `maps`：`engine/source/AssetPrep/MapsRecipe.cs`

- 項目 `maps` → `maps/manifest.json`：`schema`、`blocks[]`（`mapId`、`name` 小寫、`path` 保留原大小寫、`sha256`、`noEffectTail`，SEG 加 `format: "classic-seg"`）、`maps[]`（`mapId`、`minX/maxX/minY/maxY`、`width`、`height`、`verifiedAbsent`、`unsupportedBlocks[].name`）、`tiles[]`（`tile`、`sha256`、`path` = `tiles/<id>.til`，字串沿用基線）、`missingTileIds`、`unsupportedMaps[]`（`mapId`、`reason`）。
  - 拿掉：`maps[].name`（L4）、`terrain`（T458 補）、`copyTerrainMapId`、`clientMapDirectoryPresent`、`directTerrainFilePresent`、`tiles[].record`、`originalSource`、`unsupportedBlocks[].sources`、`provenance`、`scope`、`nativeVa`、`nativeImageBase`。
  - 一張圖只要有一個區塊解析失敗 → 整張進 `unsupportedMaps`，不列區塊、不列 bounds（D3）。
  - map 6 `7fff8000.seg` 的 SHA 不等於錨點 → recipe 停止（`ContainerMismatch`，容器 `map/6`）。
  - tile 清單 = 所有列入區塊（含 SEG）的 ground、cover、objects tile id 聯集 ＋ 140 − 65535；Tile 沒有的進 `missingTileIds`。
- 項目 `cache` → `cache/manifest.json`：map 0 bx 0x7FFB–0x7FFE × by 0x8001–0x8005 的 20 個區塊（`name`、`sha256`、`size`、`effectCount`、`portalCount`）、這 20 塊用到的 TIL ＋ 140（`tile`、`sha256`、`size`）、`verifiedMissing`（四個固定名稱中四種副檔名都不存在的）。拿掉 `record`、`pixelModes`、`sourceArchives`、`scope`；`terrain` 留給 T458。缺區塊或缺 TIL → `MissingEntry`。
- 格式：indent 2、CRLF、UTF-8 無 BOM；maps manifest 檔尾有換行、cache 沒有（照基線）。比對標準是 S（語義），不要求逐 byte。
- 輸出路徑是 assets 樹的相對路徑（§4.5）。用 T451 的單一 recipe 發布時會落在 `assets/maps/maps/manifest.json`、`assets/maps/cache/manifest.json`；組成一棵樹是 T467。
- Coverage = `Partial`（terrain 缺）。

### 4. 準備器核心（§4.6 分給 T456 的缺口）

- `IAssetRecipeSources`／`AssetPrepSources`：recipe 宣告讀 `Tile`、`MapFiles`；沒宣告的維持只開 17 個 Sprite 容器（inventory 不變）。maps recipe 不開 Sprite 容器，所以不受 D1（Sprite03–14 pin 不符）影響。
- `AssetPrepInput`：Tile 結構檢查（記 idx SHA 與 pak 大小，pak SHA 欄位留空）、`map/` 目錄列舉、讀檔；每個用到的檔記進收據（`Tile/<entry>`、`map/<目錄>/<檔名>`，另外每個目錄記一筆檔名清單的 SHA，加檔或刪檔都會讓續跑重做）。`Verify` 支援這兩種來源，續跑與「已是最新」都能判斷。
- 串流寫入：`AssetRecipeItemContext.Write(path, Action<Stream>)`，manifest 直接寫檔，大小與 SHA 從寫好的檔案算。

### 5. 測試

- `engine/_integration/dev/map-provider-tests/MapProviderChecks.cs`：`[CheckGroup("map-provider")]`，同時掛 CI 已在跑的 `map-continuity`（Order 300）與預設全套。全部用合成 S32／SEG／TIL／LAC 與 `Tile.idx`／`Tile.pak`，寫在 `scratch/issue-1041/run-<時間>/`：
  - manifest 欄位與鍵順序、bounds 推導（含 `.seg`-only 區塊）、verifiedAbsent 52／31 個、unsupportedBlocks（`.seg`、`.s32.gz`）、tiles 與 missingTileIds、unsupportedMaps（effects tail 後多 3 bytes）、排除 `tempseg.s32`、9 字元檔名、非十進位目錄、只有 `.seg` 的目錄。
  - cache manifest 20 列、effectCount／portalCount、tiles、verifiedMissing。
  - 兩個輸出目錄逐 byte 相同；不含絕對路徑；沒有複製 S32／TIL；收據只有 Tile 一個輸入、記錄來源；已是最新；客戶端改動後重做。
  - 拒絕：缺 `map/`、缺 Tile、Tile.idx 結構壞、cache 缺區塊、cache 缺 TIL、map 6 SEG 錨點不符、未知項目。
  - provider：v2 樹直讀與 v1 封裝樹畫出相同的場景（`SourceNodes`、draw、`Snapshot`），map 模式與 cache 模式各一次，再 shift 一次；準備後改區塊／改 TIL → 拒絕（SHA）；缺 TIL entry、缺客戶端目錄 → 拒絕；v1 樹不讀客戶端；v2 的 cache manifest 照 integrity 核 SHA。
  - 入口：v2／v1 兩種樹都從區塊讀到 portal 格；客戶端改動 → `portal source hash mismatch`。
  - 船運背景：三個 LAC 從 Tile.pak（名稱不分大小寫）與 `assets/maps/ship` 取得相同 bytes；pin 不符拒絕且錯誤文字不變；路徑型名稱拒絕。
- `AssetPrepModuleTests`（`--module-tests`）：maps recipe 的發現與項目、bounds、S32 結構走訪（tile id、effects tail、無 tail、多餘資料、截斷）。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`（0 錯誤）、`dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`（0 錯誤）。**沒有在本機執行任何編譯出來的程式**。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4,682 個追蹤檔，0 錯誤）。
- PR #1077 的 CI（run 43427996669）：`dotnet`、`map-continuity`、`npc-offline` 全部 pass。`map-continuity` 的 `--module-tests` 0 失敗（含 asset-prep 的 MapScan 檢查）；DemoChecks `map-continuity` 群組產物 `map-checks.json` 為 `status: pass`、33,460 項，其中本張的 `MP …` 檢查 160 項（含「map 0／cache mode：與封裝樹相同場景」），既有的 portals、minimap、ship、cache 相關群組同一個 run 一起通過。
- **D3 回報（唯讀 Python，照 C# 同一套規則對真客戶端算）**：`scratch/issue-1041/d3_compare.py`（不入 git），只讀 `D:\lineage開發\0-客戶端\map`、`Tile` 沒讀，比對基線 `engine/assets/maps/manifest.json`：

| 項目 | 結果 |
|---|---|
| 收進的地圖 | 715 張 = 基線 713 張 ＋ 4472、12000 |
| 基線 713 張的 bounds | 713／713 相同 |
| 不支援的地圖 | 12004（512 個區塊 effects tail 後有多餘資料；第一個 `7ffc7ff6.s32: trailing 8`） |
| 區塊 | 7,258 列；與基線共有的 7,192 列 sha／path／noEffectTail 全同；新增 66 列（12000）；基線有而不再列的 6 個：`0`、`1005`、`244`、`4`、`7` 的 `tempseg.s32` 與 `4/800F7FF3E.s32` |
| `tiles[]` id（含 missing） | 5,054 個；基線 5,053 個全部包含；多出 **4689**（來自新收的 4472／12000 或 SEG，沒有細分） |
| 不收那 6 個檔對 tiles 的影響 | 沒有（6 個檔用到的 tile id 都已被其他區塊涵蓋） |
| 特效 id 集合 | 498 個（含 4472／12000）；不收那 6 個檔沒有少任何特效 id |

## 未驗證（待驗證）

- C# recipe 在真客戶端上的實跑與對基線的 S 比對（T470）。上表是 Python 依同規則算的，不是 C# 的輸出。
- `tiles[]` 多出的 4689 實際來自哪張圖、Tile 有沒有這個 entry（Python 沒讀 Tile）。
- Client 模式在真機的地圖載入、跨圖、入口、船運背景：要等 T458 補 terrain、T467 產生 v2 樹與 `Assets.VerifyIntegrity` 接受 v2 之後才能跑；交給 Codex／dot 的真機回歸（T474）。
- Client 模式每讀一個 TIL 開一次 `Tile.pak`（共用、唯讀），沒有量效能。
- 本機 Tile.idx／Tile.pak 是否仍等於 `maps.md` 記的版本：本張沒有重算。

## 產出路徑

- 程式：`engine/source/MapRawSource.cs`、`engine/source/AssetPrep/MapScan.cs`、`engine/source/AssetPrep/MapsRecipe.cs`；修改 `AssetPrepCore.cs`、`AssetPrepInput.cs`、`AssetPrepRunner.cs`、`AssetPrepModuleTests.cs`、`OriginalMapCache.cs`、`Portals.cs`、`ShipBackdrop.cs`、`MapResources.cs`、`WorldEntry.cs`、`SceneOpacity.cs`。
- 測試：`engine/_integration/dev/map-provider-tests/MapProviderChecks.cs`。
- 報告：本檔。沒有改 `rebuild-lock.json`、`docs/REPORT.md`。

## 給下一棒的提示

- **T458 terrain**：用 `MapScan.Plan`／`Bounds`／`Parse` 掃描，不要另寫一份。完成後把 `maps[].terrain`（`path`、`sha256`）與 `cache/manifest.json` 的 `terrain`（`path`、`sha256`、`origin [32256,32768]`、`size [512,512]`）補進 `MapsRecipe`（它在 §6 排在 terrain 之後，要讀 terrain 的輸出），`Version` 加一。沒有 terrain 時產品的 `OriginalMapStore` 會在 `GetProperty("terrain")` 失敗，這是預期的。D3 的 `unsupportedMaps`（目前只有 12004）不要產生 terrain。
- **T467 總流程**：`integrity.json` 用 `tw-assets-integrity-v2` 時 `MapRawSource` 自動切成直讀；`maps/manifest.json`、`cache/manifest.json` 要列在 integrity（`OriginalMapStore`／`PortalCatalog` 從那裡取 SHA）。recipe 的輸出路徑已經是 assets 樹的相對路徑。`Assets.VerifyIntegrity` 仍只接受 v1，這張沒動。
- **T457**：特效 id 要掃同一批區塊（`MapScan.Parse` 回傳 `EffectIds`），規則已排除那 6 個檔，集合不受影響。
- 執行期的 `MapClientRoot` 預設是 `UiResourcePins.ClientRoot`（`TW_CLIENT_ROOT`，未設定時是開發機路徑）。可攜版要不要拿掉開發機預設，屬 T468／D1 的範圍。
