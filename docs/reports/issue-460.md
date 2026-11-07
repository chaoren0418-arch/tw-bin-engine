# Issue #460 報告：V5 查證——底列行為

階段：resource　執行者：claude-local　分支：`task/460`　日期：2026-11-08

## 做了什麼

依 `docs/design/native-ui.md` 第 6 節 V5，逐項做靜態查證。全程唯讀，沒有啟動遊戲、伺服器或 DB，也沒有改產品程式。

1. **TW16**（`TW1608262200.bin`）：沿用 `scratch/rea/ida/tw16.idb`，查 `Status800`、`StatusMiniMap`、`StatusMacroExtend`、widget 工廠與鍵盤處理。
2. **發現 TW16 的舊底列是死碼。** 消費 `uistatus86-c.xml` 的 `Status800` 建構子沒有任何呼叫者；TW16 改用新版 widget UI，而這套 UI 的 XML 不在本機 `Tile.pak` 中。所以另外對 **TW13**（`TW13081901.unpacked.exe`，SHA 已核對）在 `scratch/rea/tw13/` 跑了一次 IDA 自動分析。TW13 的 `Status800` 是實際在跑的類別，切換與鍵盤行為以它為準。
3. 結果寫進 `docs/reference/ui-layout.md`：
   - 第 2 節新增「底列行為（V5）」，含 (a)(b)(c)(e)
   - 第 5 節新增 (d)，並更新開頭結論與「推測」項
4. 查詢腳本存到 `docs/reference/rea-scripts/v5/`，附 README。

## 結論摘要

| 項目 | 結果 | 狀態 |
|---|---|---|
| (a) 巨集 8 格 | 4 欄 × 2 列，格距 **34×34**。圖示左上＝(657＋34·(k%4), 497＋34·(k/4))；F5–F8 在上列、F9–F12 在下列；選取框與點擊原點＝(655, 495)。另外取得擴充巨集：12 格、4×3，原點 (495, 495) | 已驗證（TW16 `sub_ED33F0`＋TW13 `sub_783A90`／`sub_782740`） |
| (b) F1–F4 | F1–F3 切換巨集分頁 1–3；F4 撿起身邊地面物品（opcode 23）；F5–F12 使用巨集。**Map 是 Ctrl＋M**，與 Map 按鈕（action 13）同一段邏輯。另列 Alt＋F2、Alt＋F3、Ctrl＋F1–F12、Ctrl＋J／N 等 | 已驗證（TW13 `sub_4DEC00`／`sub_4D3910`，TW16 對照） |
| (c) 世界畫面範圍 | tooltip 限制在 800×470 內；聊天窗會往上蓋到 y＝360；輸出表是 800×600。沒找到世界層的裁切或中心常數 | **待驗證**。證據不和「800×600 由底列覆蓋」衝突，C2 維持現有預設 |
| (d) 小地圖內容 | **不是即時地形縮圖。** 重用 `Field_Map`／`Dungern_Map` 的影像繪製，以 `MapSystem` 的 ratio 換算；地圖不在清單內時只畫空框 | 已驗證。影像就是 png 地圖屬高可信推測 |
| (e) (485, 475) 切換 | `Status800` 有 4 個模式：0 小地圖、1 擴充巨集、2 隊伍／寵物、3 `ChatFrameSub`。觸發來源：**Alt＋F3** 循環 0→1→2→3、選項指令 `uitype`、進遊戲時重設成 0、載入設定。點格子本身不會切換 | 已驗證（TW13 `sub_7923E0`／`sub_7927A0`） |

各項的 VA／File Offset 都寫在 `ui-layout.md` 內文。TW16 依區段換算，TW13 是 VA − `0x400000`。

## 怎麼驗證

- 每個結論都至少有一個 Hex-Rays 偽碼或反組譯出處，偽碼輸出在 `scratch/rea/ida/{e*,t13*}/`（未進 git）。
- 座標公式和 XML 數值交叉核對：
  - `MACRO_ICON` 相對座標 (14, 21) 加上 `RIGHT_WINDOW` 原點 (643, 476)
  - 解析者 `xRPos` 分支會加父視窗座標
- 鍵碼編碼（`|0x1000`／`|0x8000`）和 TW16 視窗程序的 `or wParam, 1000h／8000h` 一致。
- action 表的第 8 筆正好是 MACRO_LOCK 切換函式，佐證第 13 筆就是 Map。
- 兩個樣本的 `Status800` 繪製函式用相同的 34 px 公式。
- 兩個 BIN 的 SHA-256 都核對過：TW16 為 `e12949cf…41a2`，TW13 為 `653da514…62a4`。

## 未驗證什麼

- (c) 世界層有沒有畫到 y＝470 以下：沒有找到地圖繪製迴圈的裁切矩形，需要真機目視或再追一輪。
- (d) 「影像成員就是 png 載入函式 `sub_E557D0` 的產物」這一步，寫入點沒有追到。
- 模式 3（`ChatFrameSub`）的實際畫面；Alt＋F2 的 4–7 是否就是輸出縮放；Ctrl＋J／N、Shift＋←→、Alt＋數字的用途。
- `Status800` 設定旗標的來源與預設值（`sub_4B6530` 的呼叫者 `sub_4B8A10`）。
- 兩個分析樣本都不是實際遊玩的 `LiTo.bin`（Themida 加殼），行為是否完全相同無法靜態確認。
- 沒有做任何真機比對。

## 產出路徑

- `docs/reference/ui-layout.md`：第 2 節「底列行為（V5）」、第 5 節「`MINI_MAP_WINDOW` 地圖區的內容」，以及結論 #3、推測、腳本表
- `docs/reference/rea-scripts/v5/`：查詢腳本、執行器與 README
- `docs/reports/issue-460.md`：本報告

## 給下一棒的提示

- **ui-macro-bar：**
  - 直接用 (a) 的公式：圖示 (657＋34c, 497＋34r)，選取框與點擊用 (655＋34c, 495＋34r)
  - 數量文字右對齊在 +27／+22
  - F1–F3 綁分頁，**不要把 F1–F4 當巨集鍵**
  - F4 是撿物
  - 舊的 F1–F4 綁定移轉時，建議移到 F5–F8
- **ui-minimap：**
  - 開關改成 **Ctrl＋M**，不要佔用 F5
  - 底列小地圖內容用 `Field_Map` 同一套 png＋`MapSystem` 換算，不要用地圖快取縮圖
  - 不在 `MapSystem` 清單內的地圖只畫框
- **C2（`BAR_485` 槽位）：**
  - 原版的切換鍵是 **Alt＋F3**，循環 小地圖 → 擴充巨集 → 隊伍／寵物 → 第 4 模式
  - 第 4 模式語義未明，可以先只做前 3 個
  - 原版沒有按鈕切換
- **`docs/design/native-ui.md`：** 裡面的 V5「待驗證」字樣（第 3.1、3.3、5 節）要由維護設計文件的一方改成指向本結果。本任務沒有改它，以免和其他分支衝突。
- **(c) 要定案的話：**
  - 請使用者目視原版：底列透明處或聊天窗縮到最小時，y＞470 是否看得到地面
  - 或開一張查證任務，從 TW13 initGame `sub_4D7E50` 建立的世界視圖往下追地圖繪製的裁切
- **TW13 的 IDB 已在本 worktree 的 `scratch/rea/tw13/`。** worktree 清掉後要重建，照 `rea-scripts/v5/README.md` 跑一次約 3 分鐘。
