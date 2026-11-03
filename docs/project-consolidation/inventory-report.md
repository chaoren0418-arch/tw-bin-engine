# 16 路徑實體整併結果

更新：2026-10-28T18:20:31.138849+00:00。16 個來源原本均為實體資料夾，現在均已同磁碟搬入專案根，原 16 路徑已不存在；並非僅建立索引或換名。沒有刪除獨有檔案。

| 原路徑 | 現行根內位置 | 遷移前檔數 | 遷移前邏輯 bytes |
|---|---|---:|---:|
| `D:\P10Lab-TW13081901` | `history\labs\P10Lab-TW13081901` | 2,509 | 497,113,008 |
| `D:\P12Lab-ClientEngine` | `history\labs\P12Lab-ClientEngine` | 1,759 | 452,173,355 |
| `D:\P13Lab-ClientEngine` | `history\labs\P13Lab-ClientEngine` | 1,759 | 452,176,519 |
| `D:\P14Lab-ClientEngine` | `history\labs\P14Lab-ClientEngine` | 1,760 | 452,174,325 |
| `D:\P15Lab-ClientEngine` | `history\labs\P15Lab-ClientEngine` | 1,761 | 452,195,383 |
| `D:\P16Lab-ClientEngine` | `history\labs\P16Lab-ClientEngine` | 1,765 | 452,197,180 |
| `D:\P17Lab-ClientEngine` | `history\labs\P17Lab-ClientEngine` | 1,758 | 451,864,083 |
| `D:\P18Lab-ClientEngine` | `history\labs\P18Lab-ClientEngine` | 1,761 | 451,874,771 |
| `D:\P19Lab-ClientEngine` | `history\labs\P19Lab-ClientEngine` | 1,759 | 451,875,691 |
| `D:\P20Lab-ClientEngine` | `history\labs\P20Lab-ClientEngine` | 1,759 | 451,876,946 |
| `D:\P21Lab-ClientEngine` | `history\labs\P21Lab-ClientEngine` | 1,903 | 711,189,150 |
| `D:\P22Lab-ClientEngine` | `history\labs\P22Lab-ClientEngine` | 1,926 | 711,738,172 |
| `D:\P23Lab-ClientEngine` | `history\labs\P23Lab-ClientEngine` | 1,898 | 712,621,525 |
| `D:\P24Lab-ClientEngine` | `environments\p24` | 1,846 | 723,189,805 |
| `D:\P24Lab-6016-Export-r1` | `history\research\6016-export-r1` | 161 | 256,245,805 |
| `D:\TW1608262200_research` | `history\research\ascii` | 845 | 2,292,409,415 |

總計 26,929 檔、9,972,915,133 bytes。這是遷移前內容量，不是整個專案目前實體佔用，也不將硬連結重複計入磁碟節省。

## 分類與去重

- 現用環境：environments/p24，保留實際 DB、設定及 IDA；不與舊階段 DB 合併。
- 歷史環境：history/labs 的 13 個 Lab；DB 狀態、差異設定與 IDA 保留。
- 研究來源：6016 匯出與 ASCII 研究各保留完整來源、IDB、腳本及證據。
- 不可變且內容相同的 MySQL 工具、原 JAR、原地形、原 SQL：570 組，10,868 個部署路徑改用硬連結；原件 2,957,933,947 bytes 置於 recovery/duplicate-quarantine。修改這些檔案前必須建立獨立新版本，禁止原地改寫共享本文。
- 初次靜態盤點共 600 組跨根重複；其餘 30 組涉及設定／證據或用途不宜合併，保留。初次雜湊範圍內有 1,124 個獨有檔，不代表未納入靜態掃描的 DB 也已做語意去重。
- 可重建資料：r28 的 .nuget、.dotnet、tmp 已移入 recovery/r28-build-cache；未永久刪除。engine 新建產物在 engine/build。
- 必要原始資產：封存 assets 留在 releases/r28，engine/assets 為不可寫入的基準 junction；原 BIN、正式客戶端等外部來源保持唯讀，不納入垃圾清理。

原件仍在隔離區，因此本輪去重回收空間為 0 bytes；磁碟餘量前後為 6,782,588,211,200 → 6,781,455,458,304 bytes，差值 -1,132,752,896 bytes。差值包含備份、重建與全磁碟其他活動，不能當成去重收益。

## 證據與限制

所有 26,929 個外部原檔已全 SHA 核對；28 個設定檔以遷移前備份 SHA 及授權變更後 SHA 配對。r28 的 14,364 個 seal 檔案全本文核對；其完整搬移清單中另有 1,295 個易變日誌／快取只比大小、mtime 與搬移收據，不宣稱本文雜湊證明。

早期搬移收據把 Windows DirEntry 的零 file ID 當成身分證明，此說法已撤回；以 [更正](physical-migration/verification-correction.json) 及 [最終驗證](physical-migration/final-verification.json) 為準。舊收據保留供稽核。

[機器可讀對照](path-map.json) · [CSV](path-map.csv) · [結果](result.json)
