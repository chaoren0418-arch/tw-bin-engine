# Issue #1043 — T458 準備器：terrain 與 bounds（從客戶端 S32／SEG）

日期：2026-11-13。執行者：本機 Claude（claude-local）。分支 `task/1043`。

依據：`docs/reference/asset-recipes/terrain.md`（T445，判定**部分 pass**）、`docs/design/asset-recipe.md` §3.4、§6、§7 L3、D3、§9 對 T458 的修訂。

## 結論

- T445 是部分 pass，依 `asset-recipe.md` §9：**要寫程式**，713 張全部照規則產生，不跳過 L3 的 8 張（任務原文「只對相符的地圖產生」已由契約作廢）。
- 新 recipe `terrain` 產生 `maps/terrain/<mapId>.bin`、`terrain0.bin`、`cache/original-map0.txt`。
- recipe `maps` 補上 `maps[].terrain`（`path`、`sha256`）與 `cache/manifest.json` 的 `terrain`（`path`、`sha256`、`origin`、`size`），SHA 是新產出檔的 SHA；不再有 `sourcePath`／`sourceSha256`（來源是客戶端區塊檔，SHA 已記在 `blocks[]` 與收據）。
- 解析不了的地圖（D3）：不產生 terrain 檔，列在 `maps/manifest.json` 的 `unsupportedMaps` 與收據；產品載入時 `OriginalMapStore` 丟 `NotSupportedException`。**沒有任何地方會把不支援的地圖當成全部可走。**
- 本機只編譯（依 2026-11-09 本機執行限制），測試交給 CI。

## 做了什麼

### 1. `engine/source/AssetPrep/TerrainRecipe.cs`（新檔）

- `TerrainRules`（純函式）：
  - `Attributes`：S32 = ground 8192 × u32 → u16 n → cover n × 6 → 8192 × u16；SEG = ground 8192 × u16 → u16 n → cover n × 4 → 8192 × u8。截斷丟 `InvalidDataException`。
  - `Passage`：A = R(x−1,y)、B = R(x,y)、C = L(x,y)、D = L(x,y+1)，只看 bit0；bit0 = !(A∧B)、bit1 = !(C∧D)、bit2 = !A∧!B、bit3 = !C∧!D。
  - `Zone`：`(L & 0xF0) == 0x40` 或 `L & 4` → 16；否則 `L & 8` → 32；否則 0。沒有區塊檔的格子 zone 為 0。
  - `Build`：bounds 內沒有區塊檔的格子 L = R = 1；最外圈整個 byte 為 0；bit6–7 一律 0；用檔案原值（不加 `OriginalMapCache` 載入時的 `| 2`）。
  - `Window`：`terrain0.bin` = x 32576–32639、y 32896–33023（64 × 128）；`original-map0.txt` = x 32256–32767、y 32768–33279（512 × 512），bounds 外為 0。
  - `Text`：十進位、逗號分隔、列尾無逗號、每列（含最後一列）CRLF。
- `MapTerrain`（`maps` 與 `terrain` 兩個 recipe 共用的讀取，§6「共用同一段掃描」）：
  - 用 T456 的 `MapScan.Plan`／`Parse`／`Bounds`，沒有另寫掃描。
  - `plan.Blocks`（.s32 與 map 6 的 SEG 錨點）做完整區段走訪並取 attribute；SEG 錨點不符照舊 `ContainerMismatch`。
  - `plan.SegOnly`（只有 `.seg` 的區塊，T445 實測 47 個）只讀到 attribute 層。
  - 任何一個區塊失敗 → 整張圖不支援（D3），`Terrain` 為 null。
- `TerrainRecipe`（`[AssetRecipe]`，id `terrain`，Version 1，Coverage Complete，只讀 `map/`）：
  - 項目 `maps` → 每張支援的地圖寫 `maps/terrain/<id>.bin`；收據 metadata：`maps`（張數）、`unsupportedMaps`、`zoneLimitMaps`（這次實際產生的 L3 地圖，供 T467 總收據的 `knownLimits`）。
  - 項目 `terrain0`、`original-map0` → map 0；map 0 不存在 → `MissingEntry`，map 0 不支援 → `Unavailable`，不補值。

### 2. `engine/source/AssetPrep/MapsRecipe.cs`

- 改用 `MapTerrain.Plans`／`Read`，`unsupportedMaps` 的判定與 terrain recipe 同一份程式。
- `maps[]` 在 `height` 之後寫 `terrain {path: "terrain/<id>.bin", sha256}`（鍵位置同基線）。
- `cache/manifest.json` 在 `tiles` 之後寫 `terrain {path: "original-map0.txt", sha256, origin: [32256,32768], size: [512,512]}`。
- `Version` 1 → 2；Coverage Partial → Complete（T456 標 Partial 的原因是缺 terrain）。
- 設計取捨：§6 寫 `maps` 排在 `terrain` 之後、讀 terrain 的輸出。核心目前還沒有「讀先前 recipe 輸出」的介面（§4.6 分給 T455，尚未實作），所以 `maps` 用同一段程式在記憶體重算 terrain bytes 取 SHA。兩邊程式與輸入相同，SHA 必然一致；測試也逐張核對。T467 組樹時兩個 recipe 仍可平行跑。

### 3. 測試

- `engine/_integration/dev/map-provider-tests/TerrainRecipeChecks.cs`（新檔，`[CheckGroup("terrain-recipe")]`，同時掛 `map-provider`、`map-continuity`（CI 會跑）與預設群組）。全部用合成 S32／SEG：
  - 每條規則：bit0–3 各種 A／B／C／D 組合、zone 16／32／0x48／0x60／0x20／0x41／0x02／u16 高位元組、bit6–7 不設。
  - 跨區塊鄰格（A 在左邊區塊、D 在下方只有 `.seg` 的區塊）、大寫 `.S32`、`.s32`＋`.seg` 並存時用 `.s32`、bounds 內缺檔格 = 阻擋但鄰格的 bit0 照算（值 1，不是硬塞 0）、最外圈為 0。
  - 地圖邊界：bounds 含只有 `.seg` 的區塊與缺檔區；terrain 長度 = 寬 × 高；`maps` manifest 的 bounds 與 terrain 一致。
  - `terrain0.bin` 為 map 0 的視窗；`original-map0.txt` 格式（512 列 CRLF、512 欄、無尾逗號）與值、bounds 外為 0。
  - 不支援地圖的明確失敗：attribute 層截斷、`.seg`-only 截斷、effects tail 後有多餘資料 → 沒有 terrain 檔、收據與 `maps` manifest 都列為不支援；產品 `OriginalMapCache` 對這三張丟 `NotSupportedException`；map 0 缺 → `MissingEntry`、map 0 壞 → `Unavailable`。
  - 產品讀取點：準備好的樹（integrity v2）由 `OriginalMapStore` 載入 terrain，值與 bounds 正確；terrain 檔被改 → SHA 不符拒絕。
  - 兩個目錄輸出逐 byte 相同、收據無絕對路徑、重跑 up-to-date、客戶端樹不被改動。
- `MapProviderChecks.cs`（T456 的測試）：
  - 合成客戶端的 `map/4/7e027e00.seg` 從 3 個 byte 改成合法 SEG（terrain 要讀 `.seg`-only 區塊；壞的 `.seg` 現在會讓整張圖不支援）。
  - 期望改為：Coverage Complete、Version 2、`maps[]` 與 cache manifest 有 `terrain` 鍵、收據 complete。
  - 第 7 段原本用「全部 3」的假 terrain，改成真的跑 `terrain` recipe，並核對 `maps`／cache manifest 的 SHA 等於 terrain recipe 的檔。
- `AssetPrepModuleTests.cs`（`--module-tests asset-prep`）：terrain recipe 的發現、項目、`Passage`／`Zone` 純函式。

## 怎麼驗證

- 本機編譯（只編譯，未執行）：
  - `dotnet build engine/source/OfflineClient.csproj -c Release -o build/client-product-dev`：建置成功，0 錯誤；警告只有既有的 4 個（`HtmlActionTests.cs`、`MagicUi.cs`、`MapResources.cs`），本張沒有新增。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：建置成功，0 錯誤。
- 測試：交給 PR 的 CI（`map-continuity` 群組含新測試與 map-provider；`--module-tests` 含 asset-prep）。既有移動、施法選目標、approach-walk 測試本張沒有改，由同一輪 CI 一起跑。
- 未改 `engine/_integration/dev/rebuild-lock.json`、`docs/REPORT.md`、`Program.cs`、`DemoChecks.csproj`。

## 未驗證什麼

- **真客戶端 713 張比對沒有跑**（本機不得執行編譯出的程式）。規則逐條照 T445 已驗過的 Python 原型；C# 版在真客戶端對 r29 基線的 G 比對（bounds 與 bit0–3 713 張全同、bit4–5 只在 L3 的 8 張內）屬 T470。
- 真客戶端上會不會出現本張沒想到的不支援地圖：T456 預期只有 12004；本張多了「`.seg`-only 區塊讀不到 attribute 層也不支援」這條，真客戶端的 47 個 `.seg`-only 區塊在 T445 原型全部讀得到，預期不會增加，但沒有用 C# 實跑。
- `maps` recipe 的執行時間：現在每張圖都多算一次 terrain（全部約 3,600 萬格），沒有量。
- 真機（Client 模式載入地圖、移動、跨圖）：要等 T467 產生 v2 樹，交 T474。

## 產出路徑

- `engine/source/AssetPrep/TerrainRecipe.cs`（新）
- `engine/source/AssetPrep/MapsRecipe.cs`
- `engine/source/AssetPrep/AssetPrepModuleTests.cs`
- `engine/_integration/dev/map-provider-tests/TerrainRecipeChecks.cs`（新）
- `engine/_integration/dev/map-provider-tests/MapProviderChecks.cs`
- `docs/reports/issue-1043.md`（本檔）

## 給下一棒的提示

- **T467 總流程**：`terrain` 與 `maps` 彼此不讀對方的輸出，可以平行；但組樹後要確認 `maps/manifest.json` 每個 `terrain.sha256` 等於樹裡的 `maps/terrain/<id>.bin`（理論上必然一致，建議當作組樹後的一致性檢查）。收據 `receipts/terrain.json` 的 `maps` 項目 metadata `zoneLimitMaps` 就是 L3 這次實際碰到的地圖清單，可直接放進 `knownLimits`。`terrain0.bin` 的讀取點（`OnlineMovement.cs`、`Ground.cs`、`ApproachWalk.cs` 的 `MovementRules`）讀的是 assets 根目錄的 `terrain0.bin`，組樹時路徑要放在根目錄。
- **T470 比對**：對 r29 基線比 713 個 `maps/terrain/*.bin`（G：bounds 與 bit0–3 全同；bit4–5 只能在 0、4、42、70、244、440、2101、2151，格數不超過 `terrain.md` 表內數字）、`terrain0.bin`（B）、`original-map0.txt`（G，112 格）。
- **T455 實作「讀先前 recipe 輸出」後**：可以把 `MapsRecipe` 改成讀 terrain recipe 的輸出取 SHA，省掉重算；不改也不影響正確性。
- `MapTerrain.Read` 是 `maps`、`terrain` 共用的唯一讀取點；之後若 `.seg`-only 的規則或 D3 的判定要改，只改這裡。
