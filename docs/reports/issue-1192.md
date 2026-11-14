# Issue #1192：分流 #1188 實機驗收的失敗項目 → 修正任務＋重測

**結論**：#1188 指定範圍內的 G01、N01（4 個子步驟）與收尾**全部 ✅**，這些不重測。需要分流的有兩項：

| 編號 | 狀態 | 內容 | 分類 | 對應 |
|---|---|---|---|---|
| M01 | ⚠️ | 資源法師進世界後 detail 為「地圖 4 資源無法載入：原始區塊只有尚未支援的 SEG／壓縮格式: 4/80048001」，地圖整張不顯示 | (a) 程式缺陷 | `f1188-map-seg`（code，claude-local） |
| E01 | ❌ | #1188 把 6 張 PNG 提交到 `docs/reports/issue-1188-images/`，PR #1191 的 CI `dotnet` 在「Reject tracked game assets」失敗，結果進不了 main | (b) 測試證據放置問題 | `f1188-evidence-png`（resource，codex-local） |
| 重測 | — | 只重測 M01，附帶回歸 N01-2 | — | `f1188-retest`（test，codex-local，depends_on 上面兩張） |

沒有列為已知限制的項目。

## 做了什麼

1. 讀 #1188 的 `=== RESULT ===`（結論「通過」）與 `task/1188` 分支 `b25ae7b5` 的 `docs/reports/issue-1188.md`，以及之後調度器留言（PR #1191 CI 紅燈 → 退回 → 開本分流）。
2. 讀 PR #1191 失敗的 CI log（run 43561728233，job `dotnet`）：6 行 `REFUSED: docs/reports/issue-1188-images/*.png: PNG magic`，`errors: 6`。對照 `engine/tools/check-no-game-assets.py:99`：PNG 只允許在 `docs/demo/`、`docs/native-baseline/`；`work/f1118-uia-path/README.md` 的範例也把截圖存在 `scratch/`。main 上也沒有任何 `docs/reports/*images*` 先例。
3. 追 M01 的訊息來源（靜態讀碼）：`engine/source/OriginalMapCache.cs:37` 對 manifest 的 `unsupportedBlocks` 丟 `NotSupportedException` → `engine/source/MapResources.cs:16` `MapFailed` 把 `MapCache` 設成 null、`MapResourceState="unavailable"`。即地圖 4 整張不顯示，不只是提示。
4. 唯讀列出客戶端 `D:\lineage開發\0-客戶端\map\{3,4,10}`：只有 `.seg`、沒有 `.s32` 的區塊 map 4 有 43 塊、map 3 有 1 塊、map 10 有 2 塊，共 46 塊，與 `docs/reference/asset-recipes/maps.md:78` 記錄的 46 個 unsupportedBlocks 相同。`map/4/80048001.seg` 90,398 bytes，SHA-256 `E2DD9928…F360BA12`，與 r29 `maps/manifest.json` 的 `unsupportedBlocks[80048001].sources[].sha256` 相同。這些都是 `.seg`，沒有 `.gz`（訊息中的「壓縮」不適用）。
5. 確認程式只接受一個 SEG（map 6 `7fff8000`，SHA 寫死在 `OriginalMapCache.cs:38`、`Portals.cs:53`、`AssetPrep/MapScan.cs:30–32`），`MapScan.cs:12` 把其他只有 `.seg` 的區塊放進 `unsupportedBlocks`。
6. 查 repo 其他排隊中的 Issue（#1186、#1185、#1060、#1059、#1020、#753、#644、#26）的 `depends_on`：**沒有任何一張依賴 #1188**，所以不需要關閉或改依賴。
7. 寫 `docs/plans/issue-1192.json`（3 張）與本報告。

## 已通過、不重測的項目（#1188 ✅）

G01、N01-1、N01-2、N01-3、N01-4a、N01-4b、收尾；以及 #1188 已沿用的 #1179 G05–G07、#1173 P01、#1165 G04／F01、#1108 R07、#1150 G02／G03、#1149 S02／S02-GDI／S02-CLOSE、#1118 S01、#1126 D01／C01、#1108 B01、R05、R08–R15、C01、C02、V01。

**唯一附帶回歸：N01-2**（進世界 InWorld、entered=true、decodeFailure=null）。原因：`f1188-map-seg` 會改 `OriginalMapCache.cs`／`Portals.cs`，進世界時一定會跑這段地圖載入。N01-1／N01-3／N01-4 是 UIA 與服務頁 Tab，兩張修正任務都不碰，不回歸。

## 每個失敗項目對應

### M01 ⚠️ → `f1188-map-seg`（stage: code，runner: claude-local）
- 根因（已驗證）：只有 `.seg` 的區塊被準備器列為 unsupported，產品載入時直接拒絕；地圖 4（主大陸）中心一帶 43 塊都是這種。
- 任務要求先用原 BIN（IDA／Capstone，只讀）與 python 逐塊嚴格讀完 46 塊，證明 SEG 版面與 tile 編碼可一般化，再放寬產品與準備器；舊的 r29 manifest 唯讀，執行路徑也要能載入。無法證明格式時停下、標 `needs:claude`，不猜。
- 既有研究 `主管/P24-完整地圖來源清冊-20261026/seg-core-comparison.json` 只證明 330 個 SEG 版面可讀，沒證明語意，已在任務內註明只供參考。

### E01 ❌ → `f1188-evidence-png`（stage: resource，runner: codex-local）
- 不是產品缺陷：證據圖放錯位置。任務把 6 張 PNG 移出 git（留 scratch、報告記 SHA-256），同步 main，讓 PR #1191 綠燈合併；不改閘門規則。
- 重測任務也已寫明「證據圖存 scratch、不提交 PNG」，避免重蹈覆轍。

### 重測 → `f1188-retest`（stage: test，milestone_smoke: true）
- `depends_on`: `f1188-map-seg`、`f1188-evidence-png`。
- 只重測：**M01**、**N01-2（回歸）**；G01 只當前置。其餘照 #1188 結果沿用。

## 怎麼驗證
- `docs/plans/issue-1192.json` 以 python `json.load` 驗證格式，3 項皆含 id／title／stage／priority／text／depends_on，依賴 id 都存在。
- 所有程式行號、檔案大小、SHA-256 都是本次直接讀檔或 `Get-FileHash` 取得。

## 未驗證／推測
- 推測：一般 SEG 與錨點 SEG 版面相同（只有既有研究的嚴格讀取結果支持，未經原 BIN 證實）——交 `f1188-map-seg` 第 1 步證明。
- 未驗證：地圖 4 載入後是否還有其他 S32 區塊、TIL 或 effects 問題；資源法師的確切座標（只從區塊名推得在 x 33024–33087、y 32832–32895 一帶的鄰近區塊）。
- 未執行任何程式、未啟動遊戲／伺服器／DB。

## 產出
- `docs/plans/issue-1192.json`
- `docs/reports/issue-1192.md`（本檔）

## 給下一棒的提示
- `f1188-evidence-png` 與 `f1188-map-seg` 互不依賴，可平行；前者只動 `task/1188` 分支，不需實機。
- 其他 test 任務若也把 PNG 放進 `docs/reports/`，會遇到同一個 CI 閘門；建議調度器在 test 任務模板加註「證據圖放 scratch」。
- 目前還有多張 test PR 長期 open（#1142、#1140、#1131、#1115、#1101、#1089、#754），本張未逐一查原因。
