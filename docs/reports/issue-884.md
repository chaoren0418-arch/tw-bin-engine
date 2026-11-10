# Issue #884 T380：查證原版——FishObtainUI 釣魚物品表

## 做了什麼
- 唯讀分析 `TW1608262200.bin`（SHA `e12949cf…41a2`），IDA 7.7＋Hex-Rays 在 `scratch/t884/ida/tw16.idb`（P01 IDB 副本，SHA 前後相同 `3a0bf206…e82f`）跑 6 輪批次，查詢清單放 `docs/reference/rea-scripts/t884/q1–q6.txt`。
- 對照 3.81 伺服器原始碼 `S_InvList`／`S_AddItem`／`S_ServerVersion`，並用 `javap` 核對部署 JAR（d287）位元碼。
- PakViewer CLI 列出本機 24 個 idx＋參考客戶端 5 套各 19 個 idx（共 119 個），搜尋釣魚相關檔名。
- 寫 `docs/reference/followups-10.md` §1；回填 `gap-matrix.md` 4:FishingUI 列與「需要裁定 2」；`INDEX.md` 加一列。

## 結論
- `unk_1542864` 是 int 向量，**只由背包封包填**（S5 S_InvList＝TW16 op 216、S15 S_AddItem＝op 30），條件是封包尾段延伸區塊 +10 的旗標 bit 3；存的是物品 desc id（objid 後的 `H`，`sub_C021B0`＝item+0x26）。不是讀檔、不是資源。
- 3.81 有送 desc id，比對欄位不缺；但 3.81 尾段固定 `C(10) H0 D0 D0`，旗標偏移 +10 不在資料內 → **表恆空、清單恆空**。
- 判定：**可以照原版做**（結果是空清單）；建議 code 任務 R10-A（`Inventory.cs` 尾段解析、`FishObtainPanel.cs` 清單與捲軸、註解改已驗證），低優先。

## 怎麼驗證
- 反編譯／反組譯（推參順序對到格式字串欄位）；`.rdata` 遮罩直接讀 BIN 位元組核對（FO `0xD81F20`、`0xD4F7C8` 都是 `08 00…`）；每輪前後 `subst` 無殘留。

## 未驗證什麼
- 原版 TW16 客戶端接 3.81 時開 FishObtainUI 是否真的是空清單、空清單時捲軸是否顯示（需真機）。
- `sub_92D8B0` 內呼叫 `sub_E65030` 的另一分支條件沒細追（不影響結論，條件同為 bit 3）。

## 產出路徑
- `docs/reference/followups-10.md`、`docs/reference/gap-matrix.md`、`docs/reference/INDEX.md`、`docs/reference/rea-scripts/t884/`、本檔。
- IDA 輸出與 pak 清單在 `scratch/t884/`（不進 git）。

## 給下一棒的提示
- R10-A 若派：3.81 尾段是「長度 1 byte＋內容」，目前 `Inventory.cs:57` 固定讀 11 byte；旗標判斷要看內容第 10 byte（從 0 算）`& 8`，長度 ≤ 10 時一律 false。
- 只想對齊畫面：改註解＋畫空清單與捲軸即可，行為與原版相同。
- FU9§6.1「結束釣魚時清空」要更正：表只在世界重設清；清單在自己動作 71（S158 C71）時重填。
