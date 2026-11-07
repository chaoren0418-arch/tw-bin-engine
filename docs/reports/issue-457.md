# Issue #457 V2 查證：原版紅字（畫面中央系統訊息）

階段 resource，runner claude-local，第 1 次嘗試，分支 `task/457`。日期 2026-11-08。

## 結論

TW16（`TW1608262200.bin`）畫面中央有兩套系統訊息：

1. **紅字區**：由 `S_WARNING_CODE`（TW16 opcode 45，處理函式 `sub_94FB00`，VA `0x94FB00`／FO `0x54EF00`）觸發。讀取格式 `hc`＋字串，與伺服器 `S_BlueMessage`（`S_OPCODE_REDMESSAGE` 105）及引擎 S105 相同。
   - **字型與顏色：** MingLiU 12 px，字色調色盤[3] **#FF0000**，1 px 黑外框（調色盤[0]），**沒有底色**。
   - **位置：** 5 個固定格，x＝400 − 6·byte數/2，y＝144＋12k。**一般紅字固定在第 4 格，y＝192**。
   - **時長：** 一般紅字 **15 秒**（`push 3A98h`，VA `0x94FCEA`／FO `0x54F0EA`）。編號 1829／554／553 有專用格，不會自動消失，直到世界重設。
   - **行數：** 一般紅字**只有 1 行**，新的覆蓋舊的。訊息不會折行。
2. **中央訊息佇列**（`sub_F04180`→`sub_F18F40`）：給 `S_MESSAGE` 類型 26／28 與 `S_MESSAGE_CODE` 的部分編號用。
   - 多半是綠字（調色盤[2]），少數紅字。
   - 每筆自帶時長，多半 5 秒。
   - 最多 14 筆，最新的在 y＝121，往上每行 15 px；800×600 下最多看得到 9 行。

和引擎的差異：引擎是黑底、視窗 1/3 高、5 秒，所以 `ui-layout.md` 第 7 節 #17 由「無法判定」改為「不一致」。

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md`、`docs/design/native-ui.md` 第 6 節 V2，以及 `ui-layout.md` 既有方法（T161／V1／V3／V5）。
- 把 P01 IDB 與 BIN 複製到 `scratch/rea/` 並核對 SHA-256，用 IDA 7.7＋Hex-Rays 跑唯讀查詢（`r01`–`r07`）：
  - 從封包分派表找出處理函式
  - 追到存放陣列 `byte_1544388`、主畫面繪製 `sub_F0F0C0`、文字函式 `sub_841770`、計時器 `sub_A231C0`
  - 另外追了中央佇列 `sub_F18F40`／`sub_F188A0`／`sub_F19680`
- 用 `nat.py` 重新換算 File Offset，讀出調色盤 0–3（`0x0000`／`0x001F`／`0x03E0`／`0x7C00`）。
- 從本機 `Text.pak` 的 `string-c.tbl` 讀出 553／554／1829／1006／1017／551／552／637 的原文，只輸出到 `scratch/`；文件中只引用這幾句。
- 更新的文件：
  - `docs/reference/ui-layout.md`：第 2 節新增「紅字與畫面中央系統訊息（V2）」，含 VA／FO 表、繪製虛擬碼、推測、待驗證、引擎差異；並同步「待驗證」、第 7 節 #17、合計、結論第 5 點、給 T164 的提示、腳本表
  - `docs/reference/INDEX.md`：同一列的狀態說明
- 新增 `docs/reference/rea-scripts/v2/`：查詢腳本、執行器、`fo.py`、`tbl.py`、README。

## 怎麼驗證

- IDB／BIN SHA-256 與 `ui-layout.md` 開頭一致（`3a0bf206…`、`e12949cf…`）。
- 關鍵常數都用反組譯核對過，不只看偽碼：
  - 紅字迴圈：`add ecx, 90h`（`0xF1020B`）、`imul eax, 84h`、`add ecx, 190h`、`imul eax, 6`，調色盤[3]／[0]
  - 第 4 格：`push 3A98h`（`0x94FCEA`）
  - 編號比較：`cmp 3EEh`→`3F9h`、`725h`／`22Ah`／`229h`
- `byte_1544388` 的 xref 全部列出，只有 `sub_94FB00`、`sub_94F100`、`sub_F044A0`、`sub_F067B0`、`sub_F12610`、`sub_F0A580`、`sub_F0F0C0`。第 3 格（`0x1544514`）沒有任何直接 xref。
- 封包格式由 `sub_94FB00` 的 `hc`＋字串讀法，對照 `server-protocol.md` 的 `S_BlueMessage` 與 `DisplayNotices.cs` 的 `ReceiveRedMessage`。
- `tbl.py` 重跑輸出與文件中的引文相同。

## 未驗證什麼

- 實際遊玩的 `LiTo.bin`（Themida 加殼）沒有靜態確認，結果以 TW16 為準。
- 沒有真機截圖核對 y＝192、15 秒與外框。
- 推測項（詳見 `ui-layout.md` V2「推測」）：
  - `dword_153C998` 是否為訊息表筆數
  - 連續紅字時，舊計時器會不會提早清掉新訊息：只確認插入時不去重，派送端未追
  - 超過 131 byte 的訊息會不會溢位到下一格
  - `sub_F067B0`／`sub_F12610` 的用途
- 中央佇列中讀 `sssh` 的那個分支由哪個訊息編號觸發，本輪未追。
- 3.81 伺服器哪些地方會送 S105，本輪未統計。

## 產出路徑

- `docs/reference/ui-layout.md`（第 2 節 V2；第 7 節 #17）
- `docs/reference/INDEX.md`
- `docs/reference/rea-scripts/v2/`
- `docs/reports/issue-457.md`（本檔）

## 給下一棒的提示

- **ui-red-notice（`RedNoticePanel.cs`）** 依 V2 實作：
  - 繪製：字色 #FF0000，1 px 黑外框，**去掉黑底**；x＝400 − 6·byte數/2（CP950 byte 數），一般紅字 y＝192
  - 時長 15 秒
  - 字中 `\fN` 換成調色盤[N]、`\<` 回到起始色（目前範本已去掉 `\f`，伺服器帶 `\f` 時要支援）
  - 是否另做 1829／553／554 三個不消失的專用格（y＝144／168／156），請 Claude 決定
- **中央綠字佇列** 引擎沒有。目前收到 `S_MESSAGE` 類型 26／28 或上列 `S_MESSAGE_CODE` 編號時，原版會在畫面中央顯示綠字。要不要納入 ui-red-notice 或另開任務，由 Claude 規劃。
- 真機驗收（ui-native-smoke）時，請在 P24Lab 送一則 S105，核對 y＝192 與 15 秒。
