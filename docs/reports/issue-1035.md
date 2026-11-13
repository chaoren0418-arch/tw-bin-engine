# Issue #1035 — T450 來源閉合：地圖、TIL、scene、cache、地圖特效的 recipe

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1035`；階段：resource。只新增文件，沒有改程式，沒有複製任何 map／TIL 進專案。

## 結論

1. **原檔切片 12,626 個全部 pass**：7,198 個 map 原檔、5,052＋353 個 TIL、3 個 LAC、20 個 map0 S32，與客戶端 `map/**` 或 Tile.pak 的 entry 逐 byte 相同，例外 0 個。
2. **衍生檔都能只靠客戶端重現**：
   - scene 的 `manifest.json`、`draw-commands.json`、`tile-pixels.json`、`opacity.json`、`map.json`、`tiles.json`、`region.rgb555` 共 7 檔逐 byte 相同；6 張 PNG 解碼後像素相同。
   - 地圖特效 336 檔（含 `6016.json`）逐 byte 相同；154 個不可用的 id 與原因全部相同。
   - `maps/manifest.json`、`cache/manifest.json` 裡產品會讀的欄位全部相同。
3. **blocked 的只有欄位，沒有整個子類**：`maps/manifest.json` 的 `maps[].name`（SQL 的地圖名稱）在客戶端沒有來源；產品不讀這一欄，建議新 schema 拿掉。
4. **建議原檔用 provider 直讀，不複製**：三類合計 1.70 GB，其中 1.42 GB 是與客戶端相同的原檔；直讀後只需要約 245 MB 的衍生檔。
5. 完整規則、輸出 schema、比對數字與讀取點清單在 `docs/reference/asset-recipes/maps.md`。

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md`、#1027 報告、`docs/design/portable-delivery.md`、`terrain.md`（T445）。
- 讀研究腳本的算法（沒有執行）：P05 `build.py`／`mapdecode.py`／`archive.py`、P22 `dual_block_assets.py`、P24 `cache_assets.py`／`cache_missing_sources.py`／`opacity_sources.py`、`maps-r1/build_maps.py`、`map-effects-r1/build_effects.py`／`effect_sprite.py`、`ship-route-implementation-r1/package_seg.py`／`package_lac.py`。
- 在 `scratch/t450/` 寫三支 Python 腳本，重新實作這些規則，只讀客戶端與基線，與 r29 基線比對。
- 讀產品的 7 個讀取點加 `Renderer.cs`，整理每一處的 SHA 檢查與要不要調整。
- 用 PakViewer CLI 列出客戶端全部 19 個封存檔的檔名，查 `4094.til`、`.s32`、`.seg`、`.lac`。
- 與 `origin/main` 同步一次（合併 #1032 的文件，無衝突）。

## 已驗證

- 原檔切片：見 `maps.md`「原檔切片」的表。基線每個檔先核對 `integrity.json` 的 size 與 SHA，再與來源比 SHA-256。
- `maps/manifest.json`：7,197 個 `.s32` 的 sha、path、`noEffectTail`（474 個為 true）、排序；5,053 個 tile id；`missingTileIds = [4094]`；713 張的 `verifiedAbsent`（共 30,470 個名稱）與 `unsupportedBlocks`（共 46 個）全部相同。
- `cache/manifest.json`：`maps` 20 列完全相同；`tiles` 353 列的 id、sha、size 相同；四個 `verifiedMissing` 名稱的 16 個路徑在客戶端都不存在。
- scene 與特效：見結論第 2 點。
- **Tile.idx／Tile.pak 在封裝之後換過版本**：基線 manifest 記的 SHA（`48d3946c…`／`c234fb3c…`）與現在的客戶端（`9291fd80…`／`82e52f47…`，等於產品的 pin）不同；TIL 內容沒有變，但 5,047／5,052 筆的 offset 與 record 序號變了。manifest 的 `tiles[].record` 已經過時，產品不讀。
- 現在的 `list.spr`、`list.spz` 解開後的 SHA 與研究當時記錄的相同（與 T447 的結果一致）。
- 特效來源 SPR：336 個 id 都只在 17 個 Sprite 封存檔的其中一個出現，基線記的 offset 與 size 仍然有效。
- `ClientEffect6016.cs` 寫死的 pin `825673b1…` 等於 `6016.json` 整檔的 SHA，Python 原型的輸出與它相同。
- `ShipBackdrop.cs` 寫死的三個 SHA 等於 Tile.pak 的 `Water.lac`、`pier1.lac`、`pier5.lac`。
- `SceneOpacity.cs` 檢查的 schema 是 `p23-original-opacity-v1`，基線檔也是；P24 的 `opacity_sources.py` 現在寫的是 `p24-…`。
- scene 有 9 個檔產品原始碼沒有任何地方讀，只因為列在 `integrity.json` 而被檢查。
- PakViewer：19 個封存檔都沒有 `4094.til`、`.s32`、`.seg`；`.lac` 只在 `Tile`（8 個）。

## 推測

- 基線排除 12000、12004 兩個地圖目錄，是因為研究清冊以 SQL 地圖表為準。
- SEG 區塊用到的 tile id 已被其他 `.s32` 涵蓋，所以 `tiles` 清單沒有另外加。
- C# 準備器全量（地圖類）約 1–2 分鐘；複製原檔的話再加 1–3 分鐘。這是從 Python 原型的時間推的。

## 待驗證（本張沒做）

- 沒有建置、沒有執行任何編譯出的程式；沒有讀 SQL、JAR、DB、伺服器目錄。
- C# 的 JSON 輸出能不能與 Python 逐 byte 相同（`6016.json` 的 pin 需要）：沒有試。
- provider 直讀之後的真機回歸（跨圖、船運背景、6016 音效）。
- 141 個在 `list.spr` 沒有條目的特效 id：只在本張解開的 `list.spr`／`list.spz` 裡查過，沒有另外用 PakViewer 解出文字檔對照。
- 154 個不可用特效所在的 80 張地圖，產品載入時的實際表現。
- 三張混色表（T448）與 terrain 的 8 張差異（T445）不在本張範圍。
- 本張只改文件。CI 結果以 PR 為準，push 當下尚未取得。

## 需要主控決定

1. **地圖選取規則**：只看客戶端會比基線多 3 張地圖（4472、12000、12004），而 12004 有 512 個區塊現行解析會拒絕。要「有 `.s32` 的數字目錄全收」，還是另訂範圍？
2. **6 個檔名不合規的 `.s32`**（`tempseg.s32` 5 個、`800F7FF3E.s32` 1 個）：基線有收，產品讀不到。建議新規則不收。
3. **`maps[].name`**：客戶端沒有來源，產品不讀。建議新 schema 拿掉；若日後要顯示地圖名稱，另查 `MapDesc-c.tbl`。
4. **scene 的 9 個預覽檔**：建議不產生，並從 integrity 拿掉。
5. **原檔 provider 直讀**（建議採用）會動到 `OriginalMapCache.cs`、`Portals.cs`、`ShipBackdrop.cs` 的讀檔來源，屬 T456 的範圍。
6. **地圖 provider 用逐 entry SHA，不疊整包 Tile.pak 的 pin**：理由見 `maps.md`「建議」。

## 產出路徑

- `docs/reference/asset-recipes/maps.md`
- `docs/reports/issue-1035.md`
- 腳本與輸出（不入 git）：`scratch/t450/`（`raw_compare.py`、`derive.py`、`effects.py`、`common.py` 與三個結果 JSON、`pv/` 的 19 份檔名清單）

## 給下一棒的提示

- **T456（地圖原檔）**：用名稱查 idx，不要用舊 manifest 的 offset。`path` 保留原檔名大小寫（map 4 有 159 個 `.S32`）。`noEffectTail` 的判定是「portals 段之後剛好到檔尾」。
- **T457（scene 與特效）**：輸出格式要逐 byte 對齊才能保住 `6016.json` 的 pin——鍵順序固定、沒有空白、非 ASCII 不跳脫、特效檔與特效 manifest 檔尾是 CRLF、scene 三個執行檔沒有任何換行。`opacity.json` 的 schema 字串要寫 `p23-original-opacity-v1`。
- 研究腳本用 Windows 文字模式寫檔，所以 `indent=2` 的 JSON 全部是 CRLF；`maps/manifest.json` 檔尾有換行，`cache/manifest.json` 與 `opacity.json` 沒有。
- 特效的不可用清單要照同一套檢查產生（原因字串見 `maps.md`），否則 manifest 的 `unavailable` 會與基線不同。
- PakViewer CLI 的 `pak list` 要給 **`.idx`** 路徑。給 `.pak` 會把整個 pak 當索引解析，輸出數百 MB 的亂碼而且跑不完（本張踩過，已中止並刪掉輸出）。
- Python 腳本用 `-I` 執行時不會把腳本目錄加進 `sys.path`，同目錄的模組要自己 `sys.path.insert`。
