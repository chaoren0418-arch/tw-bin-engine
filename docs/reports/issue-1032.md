# Issue #1032 — T447 來源閉合：動作時序與 gfx 選取清冊

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1032`；階段：resource。只新增文件，沒有改程式。

完整內容在 [`docs/reference/asset-recipes/action-timing.md`](../reference/asset-recipes/action-timing.md)。

## 結論

- **A：pass，有一條已知限制。**
  - `serverActionMs` 與 `stepMs` ＝ `40 ms × list.spr 該動作的 tick 總和`。基線 42 個玩家 profile 的 210 個值全部相符。
  - `castReleaseMs`、`projectileReleaseMs` ＝ 第一個 `!` 影格之前各影格 `floor(ticks×1000/24)` 的總和。95／95 相符。
  - objects 裡記下的 SQL 值（22 個 gfx、59 列）：framecount 全等於 list.spr 的 tick 總和，framerate 全是 24。
  - 職業 gfx 集合：產品不做「職業 → gfx」換算，gfx 一律來自 S87 與 S76。這份集合只決定事先準備了哪些 profile，改成 B 的做法後不需要存在。
- **A 的已知限制（交主控）**：伺服器擋加速用的是它自己的 `spr_action`（framecount、framerate）。基線以外的 gfx 與姿勢（變身外型、斧／矛／杖等）沒有資料可以對照，R1 對它們是推測。
- **B：建議「遇到新 gfx 再準備並快取」。**
  - 一次準備全部要處理 2.61–4.14 GB 原始 SPR（63,962–142,776 檔），是目前實際用到的 44–70 倍；照現行 JSON 格式約 22–35 GB。
  - 全量還得先替 384 個缺檔 gfx、2,580 個跨封存同名但大小不同的檔定全域政策。
  - 遇到再準備：每個 gfx 中位數 222 KB、約 34 檔，冷讀約 0.1 秒；最大的 1% 在 7 MB 以上，要放背景執行緒。

## 做了什麼

1. 讀 `CLAUDE.md`、`docs/REPORT.md`、`docs/reports/issue-1027.md`、`docs/design/portable-delivery.md`。
2. A1：在 `engine/source` 找出讀 `serverActionMs` 的 7 個位置，分清楚冷卻、送包節流與動畫各用哪個值。
3. A2：用 Python 解出客戶端的 `list.spz`／`list.spr`，照規則算出每個 profile、每個動作的值，與基線 `players/manifest.json`、102 個 profile JSON 逐筆比對。
4. A3：列出職業 gfx 的 4 個讀取點，確認 gfx 的封包來源。
5. B：統計 17 個 Sprite 封存與 list.spz 的引用量；對基線 62 個 gfx 與隨機 150 個 gfx 實際讀檔並走完 SPR 結構，量時間與像素數，外推兩種做法。

## 怎麼驗證

- A2 的比對是全量，不是抽樣：基線裡每一個帶時序欄位的 profile 都比了。結果見報告的比對表，沒有任何不符。
- 另外逐格比了 481 個動作序列（`檔:格`、ticks、事件），確認基線 JSON 與現在客戶端的 list.spz 是同一份 metadata。
- `list.spz`、`list.spr` 解碼後的 SHA 與 #97 附錄 D 的歷史研究輸入相同。
- B 的檔數與位元組數是從 idx 直接算的精確值；時間與 JSON 體積是樣本外推。

## 未驗證

- 基線以外的 gfx 與姿勢，規則是否等於伺服器的值。
- C# 的解碼速度與首次遇到新 gfx 的畫面停頓（本機不能跑編譯出的程式），交 T470。
- 「一次準備全部」的時間沒有實跑。
- 384 個缺檔 gfx 與 5,642 個沒有動作表的 gfx 沒有逐個看。
- 沒有建置；CI 結果以 PR 為準。
- 沒有宣稱缺資源，所以沒有用 PakViewer。

## 產出路徑

- `docs/reference/asset-recipes/action-timing.md`
- `docs/reports/issue-1032.md`
- 腳本與輸出在 `scratch/t447/`（不入 git，依任務規定）。

## 給下一棒的提示

- **T461**：規則與解析注意事項在報告的「R1–R3」與「給 T461 的解析注意事項」。`serverActionMs.walk` 與 `Magic.CastMsFor` 目前沒有人用，可以順手確認要不要留。objects 的攻擊與施法動畫現在用 `40 × ticks`（伺服器間隔），不是原生的 `1000÷24`；要重現基線還是改回原生，請先決定。
- **T454／T455（SPR 準備器）**：建議做成「遇到才解」，快取格式不要沿用現行 JSON。gfx 138（男妖精）在研究工具裡因 `opaque pixel outside frame` 被拒收，list.spr 其實有動作，要處理這個邊界情況。
- **T449**：`GroundSpriteReader.cs` 對 `Sprite03`–`Sprite14.pak` 寫的檔案大小與本機客戶端目前的大小不同（`Sprite`、`00`–`02`、`15` 相同）。本張沒有查原因。
- **T452**：已知限制清單請收「伺服器 `spr_action` 無法由客戶端驗證」這一條。
- 重跑比對：照報告「規則」一節重寫腳本即可，解碼方式與 `docs/reference/rea-scripts/t783/listspr_fields.py` 相同。
