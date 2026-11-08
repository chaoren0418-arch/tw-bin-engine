# #504 T202：差距盤點——原版功能對照引擎（gap-matrix，M7）

**做了什麼**
- 以 `TW1608262200.bin`（SHA `e12949cf…41a2`）為準，靜態盤點原版功能並逐項對到 `engine/source`，產出 [`docs/reference/gap-matrix.md`](../reference/gap-matrix.md)：
  - 收包三組 dispatch（`sub_947530`）：第一組 114 個處理區塊、第二組（登入期）17 個、第三組 protobuf 1 個；名稱取自 `sub_9515E0` 名稱表。每列記處理函式 VA／FO、推定的 3.81 對應、引擎檔案、狀態、依據。
  - 送包 `sub_951F80` 名稱表 139 個 `C_*`，依功能合成列。
  - UI：IDA 反編譯 `sub_B56A40`（XML 視窗工廠，80 個 UI 名；`MatchMakingUI` 不建立物件），用 vtable→RTTI 解出類別名與建構子地址；`sub_8C7C80`（initGame）建立的舊式視窗以 RTTI 列出。
  - 物件、技能、道具、地圖事件、繪製的處理函式（引用 REA、SV、WO、AM、UI 已查證的地址）。
- 統計 287 列：**已有 55、部分 164、缺 68**（缺之中：需先查證 27、伺服器不支援／不建議 21、可直接補 20）。
- 第 7 節：M7 code 任務分組 **22 組**（M7-A–X），每組列目標、可改檔案（同一既有檔案只在一組）、依賴、引用；共用入口 `WorldSession.cs`／`Program.cs` 只給 M7-A。另列已排定任務（T124、T135、T151、T166、T168–T179、T190–T200）保留的檔案。
- 第 8 節：需先查證清單 17 項。
- 新增可重跑腳本：`rea-scripts/gap_inventory.py`、`ui_classes.py`、`gap_count.py`；更新 `docs/reference/INDEX.md`。

**怎麼驗證**
- 地址：`.text` FO = VA − `0x400C00`、`.data` FO = VA − `0x401800`，由 `nat.py` 區段表算出；與既有文件的值（`sub_947530`、`sub_94E0E0`、`sub_93F5E0`、`HpMpWindow` RTTI 等）逐一相符。BIN 由 `nat.py` 檢查 SHA。
- IDB：P01 `TW1608262200.idb` 複製到 worktree 的 `scratch/rea/ida/tw16.idb`，複製後 SHA 相同（`3a0bf206…e82f`）；subst 用 `W:`，執行前確認未被占用，`run_ida.ps1` 結束時解除。原檔未開啟、未修改。
- 統計由 `gap_count.py` 從文件重算，文件內的表格就是它的輸出。
- 引擎對照：以原始碼搜尋 receive 分派（`case N`、`opcode==N`）與送包組包點；每列的引擎檔案都實際存在。

**未驗證什麼**
- 「3.81 對應」多數依封包名稱推定，只有依據欄引用 reference 的列查過欄位。
- 送包只到名稱表，沒有逐一找送出點；以變數組出的 opcode 可能漏判。
- 審查中的 T1xx 分支沒有比對；合併後部分「部分」會變「已有」。
- 沒有建置、沒有啟動遊戲、伺服器或 DB。

**產出路徑**
- `docs/reference/gap-matrix.md`、`docs/reference/INDEX.md`
- `docs/reference/rea-scripts/gap_inventory.py`、`ui_classes.py`、`gap_count.py`
- 中間結果（不進 git）：`scratch/t202/`、`scratch/rea/ida/dec1/`

**給下一棒的提示**
- 主控可直接依第 7 節開單：先派 M7-A、M7-J、M7-L、M7-O、M7-P、M7-R（彼此不衝突、不等別人）；M7-A 合併後再派 M7-B／C／D／E／G／H。M7-Q 要使用者先裁定；M7-S–W 等 T190（原生 UI 畫布）；M7-X 等 T151。
- 第 8 節的查證項建議合成 2–3 張 resource：(1) S_EXTENDED 子碼表＋未知收送包；(2) 3.81 世代用哪套視窗（背包、魔法、倉庫、血盟、變身）；(3) 城堡／盟屋／數量輸入的畫面。
- 重跑：`py -3 -I docs/reference/rea-scripts/gap_inventory.py scratch/t202/inventory.json`；改了 gap-matrix 後用 `gap_count.py` 重算第 6 節。
