# Issue #800 報告：T339 gap-matrix 第六輪

階段 resource，第 1 次嘗試。分支 `task/800`。依 main `9b268b30`，加上合併佇列中的 PR #801（R6-F）與 #754。

## 做了什麼
- 在 `docs/reference/gap-matrix.md` 新增第 15 節「第六輪盤點」，舊內容不刪。
  - **15.3 狀態變更**：47 列。
    - 「部分→已有」44 列，依原分類是 K4 17、K2 10、K5 8、K6 6、K3 3。
    - 「缺→部分」3 列：UserObtainUI、StatInfoUI、AgitAuctionUI。
  - **15.5 重新分類**：K3 與 K4 兩類取消，所有非「已有」的列都重判。
    - K4 的 41 列，依預設開啟重判：已有 17、K5 10、K2 14。
    - K3 的 19 列（18 列，加 14.10 移入的 WidgetSlot），依參考客戶端結果重判：已有 4、K5 10、K2 4、K1 1。
  - **15.2**：新發現與更正，另附「程式已寫但產品沒接上」表。
  - **15.6**：第六批 code 分組 R7-A–O，共 15 組。
  - **15.7**：真機檢查清單 M1–M30。
  - **15.8**：需先查證，Q6（#783）與新的 Q7-1–Q7-14。
- 新增 `docs/reference/rea-scripts/gap_count_r6.py`：套用 11–15 節的變更，並檢查 15.5 每一列只歸一類。
- 更新 `docs/reference/INDEX.md` 的 gap-matrix 列。
- PakViewer：為 4:WidgetSlot 查了 11 套客戶端、共 296 個 idx。
  - 本機 `0-客戶端` 的 24 個 idx：全部 0 命中。
  - 381 小草、`D:\380` 下 4 套、凝時、古典、韓版：0 命中。
  - kor25、381 泡麵陳、超人 760 的 `Tile.idx`：`WidgetSlot.xml` 7,139 B、`WidgetSubSlot.xml` 9,144 B。
  - t89：同大小的小寫檔名。
  - ly：另有 `-c` 版；沒有 `-c` 的 `WidgetSlot.xml` 是 8,019 B 的另一版。
  - 查詢清單在本機 `scratch/issue-800/idx/`、`widget_hits.txt`，沒有進 git。

## 新統計
- 第 1–5 節合計 290 列：**已有 155、部分 94、缺 41**（第五輪是 111／135／44）。
- 分類：**K1 52、K2 36、K5 47**。

## M7 能否結案：不能
- 驗收條件要求「缺」與「部分」只剩「需先查證」或「伺服器不支援」。現在 K1＋K2 共 88 列符合；**K5 還有 47 列**。
  - 其中 9 列已有任務：#784、#651、#762、PR #801。
  - 其餘 38 列分在 R7-A–O。
- K2 的 36 列形式上符合條件，但 Q6／Q7 查完後，多數會轉成 code 任務。

## 怎麼驗證
- 逐列判定由 4 個唯讀子代理做：讀報告，再 Grep 產品端呼叫點。15.1 列的 9 個關鍵說法我另外讀檔複核過。
- 統計用 `py -3 -I docs/reference/rea-scripts/gap_count_r6.py docs/reference/gap-matrix.md` 跑過：第 11–15 節依序套用 88／87／85／47／47 列，沒有錯誤，也沒有漏分類。
- 沒有開 IDA，沒有建置或執行程式，也沒有啟動遊戲、伺服器或 DB。

## 未驗證什麼
- 全部「已有」都沒有真機驗收，待真機的項目在 M1–M30。
- 「產品沒有呼叫」是用 Grep 判斷的，透過反射或委派間接呼叫的地方可能漏掉。
- 子代理給的 `檔案:行` 沒有逐一重看。
- 「TW16 依 UI 名稱讀 `WidgetSlot.xml`」是推測：BIN 的 XML 檔名表裡沒有這個檔名。

## 產出路徑
- `docs/reference/gap-matrix.md` 第 15 節
- `docs/reference/rea-scripts/gap_count_r6.py`
- `docs/reference/INDEX.md`
- 本報告

## 給下一棒的提示
- **可以馬上派**：R7-B–K、R7-M、R7-O。
- **要等其他任務**：
  - R7-A 等 PR #801 合併，因為兩邊都改 `MagicRendering.cs`、`SkillLightTests.cs`。
  - R7-N 等 R7-C、R7-I、R7-M 合併。
- **Q7-1–Q7-14 建議合成 1 張 resource 任務**。#783 原本要順便查的 WidgetSlot 資源，本輪已經查完，可以從它的範圍拿掉。
- **PR #801 合併後**，`CCookWindow` 與 5:（109 kind 可改成「已有」，下一輪套用。
- **真機 smoke**（#475、T112）照 15.7 的 M1–M30 做。M1（預設原生 UI 的啟動與核對耗時）優先。
- **建議另開小任務更新的文字**：
  - 14.6 與 FU3§3.1 的「王族 Lv≥50」應為「妖精」；
  - `PLAN.md` 第 90 行「新 UI 預設關閉」。
