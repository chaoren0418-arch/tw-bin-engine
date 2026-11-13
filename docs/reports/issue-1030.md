# Issue #1030 — T445 來源閉合：terrain／bounds 能否只從客戶端 S32／SEG 推導

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1030`；階段：resource。只新增文件，沒有改程式。

## 結論：部分 pass

- **bounds：pass。** 由區塊檔名範圍推導，713／713 與 `maps/manifest.json` 相同。
- **terrain 通行位元（bit0–3）：pass。** 713 張、35,979,264 格，不符 0 格。產品只讀 bit0、bit1（與從未出現的 bit7）。
- **terrain 區域位元（bit4–5）：部分 pass。** 705 張整檔逐 byte 相同；8 張（map 42、4、0、244、2101、2151、70、440）共 13,567 格不同，其中 map 42 佔 13,075 格。
- **`terrain0.bin`：pass**（逐 byte 相同）。**`cache/original-map0.txt`：部分 pass**（格式可重現，112 格區域位元不同）。
- blocked 的部分：上述 13,567 格的區域位元，伺服器資料有、客戶端 attribute 沒有。列為已知限制交主控，沒有找 SQL／JAR 替代。

規則、逐圖不符清單、產品用到的位元表都在 `docs/reference/asset-recipes/terrain.md`。

## 做了什麼

1. 讀 `CLAUDE.md`、`docs/REPORT.md`、`docs/reports/issue-1027.md`、`docs/design/portable-delivery.md`、`docs/protocol/portals.md`、`OriginalMapCache.cs`。
2. 用 Python（`scratch/t445/`）讀客戶端 `map/<mapId>/*.s32`、`.seg` 的 attribute 層，先在 map 0 上用資料找出每個 terrain 位元取決於哪些 attribute，再擴到 713 張逐格比對，依不符的型態修正規則（外圈歸零、缺檔視為阻擋、`.seg` 備援、副檔名大小寫、區域位元優先序）。
3. bounds 由區塊檔名範圍推導，與 manifest 逐圖比對。
4. 核對 `terrain0.bin` 與 `cache/original-map0.txt` 跟 map 0 terrain 的關係。
5. 讀原始碼列出產品讀 terrain 的位置與位元。
6. 用 PakViewer CLI 列出客戶端全部 19 個 pak 的檔名，確認沒有其他地圖或地形來源。

## 怎麼驗證

- 基線 713 份 `maps/terrain/*.bin` 每份先核對 manifest 記的 SHA-256，再與推導結果逐格比對；結果寫成 `terrain-compare.csv`（每張圖的格數、不符格數、通行位元不符、區域位元不符、相符率、bounds 是否相同、整檔 SHA 是否相同）。
- 用到的客戶端區塊 7,234 個（7,187 `.s32`、47 `.seg`），其中 7,188 個在 manifest 有 SHA 紀錄，全部相同。
- 對照組：不用 `.seg`、或把缺檔鄰格當可走，相符張數都明顯下降（702、542），確認兩條規則必要。
- 等價證明是「對既有基線逐格比對」，不是自產 hash。

## 未驗證什麼

- 沒有建置、沒有執行任何編譯出的程式；沒有讀伺服器 `server/maps`、SQL、JAR、DB。
- `cache/original-map0.txt` 是本機基線素材，它的 SHA 等於 manifest 記的伺服器 `0.txt` 的 `sourceSha256`（內容就是那份文字檔的副本）；本張只把它當基線讀。
- 8 張地圖不符的原因（伺服器版本不同或另外改過）只是推測。
- C# 實作與真客戶端全量重跑（T458、T470）。
- 客戶端另有 4 個地圖目錄（18、4472、12000、12004）不在 713 張基線內，沒有基線可比。
- 原版 BIN 自己的通行判定是否等同這套規則，沒有查。
- CI 結果以 PR 為準。

## 需要主控決定

1. **8 張地圖的區域位元差異怎麼處理。** 建議：準備器照規則產生，接受這 8 個檔與 r29 基線不同（產品不讀 bit4–5，通行結果相同），在 T452 的已知限制清單記一筆。另一個選項是把 T458 判成 blocked，但那會放棄已經完全相符的通行資料。
2. `cache/original-map0.txt` 連帶有 112 格不同，處理方式同上。
3. T470 的比對標準建議改成：bounds 與 bit0–3 必須 713 張全部相同；bit4–5 允許本報告列出的 8 張、13,567 格。

## 產出路徑

- `docs/reference/asset-recipes/terrain.md`（結論、規則、不符清單、位元表）
- `docs/reference/asset-recipes/terrain-compare.csv`（713 列逐圖結果）
- `docs/reports/issue-1030.md`
- 腳本與 pak 檔名清單在 `scratch/t445/`（不入 git，worktree 清掉後就沒有；規則已完整寫進 terrain.md）

## 給下一棒的提示

- T458（terrain 準備器）可以開工：選檔、座標、attribute 位置與位元規則照 terrain.md 實作即可，不需要伺服器資料。
- 三個容易漏的地方：副檔名不分大小寫（map 4 有 158 個 `.S32`）；只有 `.seg` 的區塊要讀（8-bit attribute）；bounds 內沒有區塊檔的格子當成 attribute 1，而不是直接輸出 0（缺檔格本身在南邊那格可走時 bit1 會是 1，map 3 與 111 共 26 格如此）。
- 最外圈一格整個 byte 歸零，這是基線的行為，不是 attribute 的內容。
- 產品現在的 SEG 載入會把入口格的 attribute 設上 `| 2`（`OriginalMapCache.cs` 46 行），那是記憶體內的標記，推導 terrain 時要用檔案原值。
- `OriginalMapBlock.Attributes` 目前沒有讀取者。若日後想改成執行期直接從 attribute 算通行（不產生 terrain 檔），規則相同，但要處理跨區塊的鄰格（`R(x−1,y)`、`L(x,y+1)`）。
- PakViewer CLI 的 `pak extract` 不加 `-o` 會寫到目前目錄，記得指定到 `scratch/`。
