# Issue #445／T171：解出 music.def 與 envsound.def

分支：`task/445`。日期：2026-11-08。範圍：唯讀資源研究，沒有修改產品、原客戶端、封存或 `docs/REPORT.md`。

## 做了什麼

- 定位兩份定義檔於 `D:\lineage開發\0-客戶端\Text.idx`／`Text.pak`。解出 music 28,968 bytes、envsound 76,871 bytes，完整內容只留 scratch。
- 以 TW16 BIN 與 P01 IDB 的 scratch 副本靜態核對 DES 固定格式金鑰、IDX／payload 解密範圍及解析器；沒有執行 BIN、遊戲、伺服器或 DB。
- 在 `sound.md` 第 6 節補上來源 SHA、File Offset、格式、系統槽與實際曲目區別、矩形邊界、群組冷卻及少量範例。修正第 4.7 節把群組誤寫成地圖段的描述。
- 新增可重跑解碼腳本及 IDA 查詢清單。原資源的缺值、拼字、座標缺欄、多值覆寫等均保留原樣，文件說明原版解析行為。

## 怎麼驗證

- 37,498 筆 IDX 名稱與 PAK 範圍檢查通過；兩個目標各一筆。最初要求名稱含 NUL 過嚴，實際存在滿 20 bytes 的其他名稱，已改為有界的 20-byte 欄位解讀。
- IDX 與兩份 payload 解碼後重新 DES 加密，逐 byte 等於原始輸入；envsound 最後 7 bytes 保持原樣。來源 Text.idx、Text.pak 與 TW16 BIN 讀前／讀後完整 SHA 一致。
- 兩個 scratch 輸出目錄逐檔相同；同目錄重跑不改 mtime；3 個非允許輸出位置被拒絕；既有不同內容不被覆寫。驗證紀錄由 `scratch/issue-445/verify.py` 重跑。
- `git diff --cached --check` 通過；`check-no-game-assets.py` 通過（3,669 個追蹤檔、0 錯誤、4 個 EmbeddedResource 宣告）。`git check-ignore` 確認兩份解碼檔均被 scratch 規則排除。
- 所有 IDA 分析只用 scratch 副本。沙箱內 IDA 因登錄檔讀取被拒絕而失敗，取得工具執行核准後完成；沒有更動 IDA 安裝。`8F3B90` 反編譯失敗，改用反組譯核對索引分支；沒有把失敗函式當作偽碼證據。

## 未驗證什麼

- 未做聽音、登入、跨圖、晝夜或頭目實機驗收；沒有跑產品建置，因為未修改產品。
- 目前資源包與 TW16 BIN 的同版配對未證實。缺音檔觀察只檢查 Sound 目錄的檔名，沒有播放或補檔。
- 畸形座標的實際命中結果、候選第三欄聲像效果、`8257D0` 的主音略過條件仍待驗證；資料與解析器的證據層級分開標在 `sound.md`。

## 產出路徑

- `docs/reference/sound.md`：第 6 節及舊節勘誤。
- `docs/reference/rea-scripts/sound_def_extract.py`：只寫本 checkout scratch 的解碼器。
- `docs/reference/rea-scripts/sound_def_targets.txt`：供既有 `ida_dis.py` 使用的靜態查詢清單。
- `docs/reports/issue-445.md`：本報告。
- `scratch/issue-445/decoded/`：兩份完整解碼檔與 manifest；`scratch/issue-445/replay/`：重跑核對；`scratch/rea/`：IDB 副本及靜態分析輸出。全部不進 git。

## 給下一棒的提示

由 Claude 審查、使用者合併 PR，調度器可依此解除 T172／#446 的資料前置。T172 應在執行期唯讀原資源包，保持系統槽／實際曲目、原順序與兩種矩形邊界的區別；環境音先抽群組再檢查該群組冷卻，不是扁平抽所有 id。原檔多值與拼字不能照理想格式推論，異常列如何相容應明示；不得把解碼表或音檔加進 repo。後續聲音驗收交由具授權的實機執行者。
