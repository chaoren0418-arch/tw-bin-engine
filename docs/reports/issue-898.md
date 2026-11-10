# Issue #898 報告：T385 查證原版——真機項目 M35／M37／M39／M40 的靜態部分

## 做了什麼
- 用 IDA 7.7＋P01 IDB 副本（`scratch/t898/`，唯讀批次 31 輪）查 TW16 BIN（SHA-256 `e12949cf…09641a2`），寫成 [`docs/reference/followups-13.md`](../reference/followups-13.md)（FU13）。
- 回填 [`gap-matrix.md`](../reference/gap-matrix.md) 15.11 的 M35、M37、M39、M40；[`INDEX.md`](../reference/INDEX.md) 加一列；查詢清單與腳本放 `docs/reference/rea-scripts/t898/`。
- 結論：
  - **M35**：進入選目標模式不換游標，下一次滑鼠在世界上移動時換成索引 8（瞄準框），技能依射程 8／19／20，結束還原 3。
  - **M37**：TW16 沒有單獨開 `FriendUI` 的熱鍵或選單；`/buddy`、HyperAct `buddy` 只送 `c` 56；`/exclude` 固定傳空字串（已確認）。3.81 推測是 S39 `buddy` → 本機 `buddy-c.html`。
  - **M39**：`DisableOverSurf="13"` 不是 surf，是半透明色塊（palette[0]、強度 13）；停用格常駐畫、直接點擊不觸發事件、提示照常。
  - **M40**：登入 `Back_Image` 是 png 1825–1842 動畫（每 > 200 ms 換一張），本機 24 個 idx 沒有，kor25／ly 有（SHA-256 列在 FU13§5.2）。
- main 對照列了 D1–D12（`Magic.cs`、`FriendPanel.cs`、`Buddies.cs`、`SummonPanel.cs`、`LoginPanel.cs`），每項附建議修正。

## 怎麼驗證
- 每個結論都附 VA／FO（FO 用 pefile 從節區表重算），反編譯輸出在 `scratch/t898/ida/o1`–`o31`（不進 git）。
- 游標外觀：pefile 唯讀讀 BIN 的 `RT_GROUP_CURSOR`，轉圖在 `scratch/t898/cursors.png`。
- 資源：PakViewer `pak list` 列本機 24 個 idx 與 kor25／t89／ly 各 18 個 idx；`pak extract` 解出 18 張 png 與 `buddy-c.html` 到 `scratch/` 算 SHA-256，沒有複製進 git。
- 沒有改產品程式；沒有執行任何編譯出來的程式；沒有啟動遊戲、伺服器或 DB。

## 未驗證什麼
- 3.81 `LiTo.bin`（有殼）的游標表、S39 `buddy` 的畫法、`/exclude 名稱` 的行為。
- `FriendUI` 根節點 `Toggle`（`this[133]`）的讀取者；`CommandAddExclude`／`CommandDeleteExclude` 綁的聊天字。
- palette[0] 的實際顏色與色塊強度刻度（推測 0–31）；`sub_EE8D40` 是每幀呼叫（依 vtable 位置推測）。

## 產出路徑
- `docs/reference/followups-13.md`
- `docs/reference/gap-matrix.md`（15.11 M35／M37／M39／M40）
- `docs/reference/INDEX.md`
- `docs/reference/rea-scripts/t898/`

## 給下一棒的提示
- code 任務可直接派：**M39**（`SummonPanel.cs` D6–D9）、**M40**（`LoginPanel.cs` D11／D12，png 找法照 `OptionPanel.cs:23`）、**M35**（`Magic.cs` 與世界滑鼠處理 D1；游標資源要先決定用 BIN 內嵌游標還是系統游標，D2）。
- M37 的 D4（S39 `buddy` 開 `buddy-c.html`）等 3.81 對照再做；可以先用 FU12 的 3.81 OEP 快照查 S39 分派是否特別處理 `buddy`，再決定要不要真機。
- 真機核對清單在 FU13§6，四項都已縮小成「只需對照」，M37 另需記 3.81 的 `/buddy` 畫面與 `/exclude 名稱` 是否送出。
