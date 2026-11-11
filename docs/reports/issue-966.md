# #966 T422：查證原版 Q9-2～Q9-5（＋S64 0x48）

## 做了什麼
- 唯讀靜態分析：
  - TW16 BIN 與 P01 IDB 的副本（`scratch/t966/ida/tw16.idb`），跑 IDA 批次查詢 8 輪；
  - 3.81 用 FU12 的 OEP 快照與 #896 的 IDB 副本（`scratch/t966/lito/`），跑 4 輪。
- PakViewer：
  - 本機 24 個 idx 搜尋 `obscene`／`hometown`／`secretary`／`zone`；
  - 參考客戶端 5 套 95 個 idx 搜尋 `zone3`／`obscene`／`WidgetSubSlot`；
  - 解出本機 `Text`、`LTtext`，以及 kor25 的 `zone3-c.xml`、`zone3desc-c.tbl`（只放 scratch）。
- 只讀 grep 3.81 伺服器原始碼裡寫 `S_OPCODE_CHARRESET` 的封包類別。
- 產出：`docs/reference/followups-17.md`；查詢清單與 `varmap.py`／`varmap.tsv` 在 `docs/reference/rea-scripts/t966/`。

## 結論
- **Q9-2**：區域表是全域 `dword_16568A8`，從 `zone3.xml` 讀；`+66`＝`<bookmark id>`。本機 3.81 客戶端沒有 `zone3.xml`，所以不支援，引擎維持用地圖編號。
- **Q9-3**：元件 0–5 是 MainCharInfo、MainChat、MiniMap、WidgetSlot、WidgetSubSlot、MainButton。模式 0／1＝小地圖 ↔ WidgetSubSlot。Q8-B ② 可以開工。
- **Q9-4**：
  - `<date>`＝伺服器時間的月／日，3.81 也有；
  - `<may>`＝條件區塊、`<subHtml>`＝在附屬檢視載入插圖頁，這兩個 3.81 都沒有；
  - `<var>` 998–2064 的對照表在 §3.4，3.81 只有 998、999、1002–1029。
- **Q9-5**：不雅字過濾。`obscene-c.tbl` 裡的每個詞換成字數個 `*`；備註一律過濾。
- **S64 0x48**：3.81 伺服器不送。

## 怎麼驗證
- IDA 反編譯／反組譯（地址都寫成 VA／FO）、PE 區段表重算 FO、資源檔內容、伺服器原始碼 grep。各項在文件裡分「已驗證／推測／待驗證」。

## 未驗證什麼
- 推測的部分：`zone3desc` 用哪張表、書籤篩選細節、`sub_9F8EF0` 的圖號用途、obscene 的排序與大小寫、3.81 的過濾函式、`<subHtml>` 的附屬檢視是哪一格。
- 全部沒有真機驗收；沒有執行任何程式。

## 產出路徑
- `docs/reference/followups-17.md`
- `docs/reference/rea-scripts/t966/`

## 給下一棒的提示
- code 分組（FU17§7）：
  - Q8-B ②：WidgetSlot 模式，已解鎖；
  - Q8-A ① 補充：備註的不雅字過濾；
  - Q9-C：`<date>`／`<time>`／var 998–999；
  - Q9-D：var 1002–1029 裡引擎已有的設定；
  - Q9-A：書籤區域群組，低優先，可省略。
- `WidgetSlotTests.cs:148–149` 現在驗證的是「ModeChange 沒接」，做 Q8-B ② 時要一起改。
- `BookmarkPanel.cs` 的註解「表來源 `sub_F35D90` 待驗證」可以改成 FU17§1 的結論。
