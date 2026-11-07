# Issue #459 報告：V4 查證——UI 框架層細節

階段：resource　執行者：claude-local　分支：`task/459`　日期：2026-11-08

## 做了什麼

依 `docs/design/native-ui.md` 第 6 節 V4，逐項做靜態查證。全程唯讀，沒有啟動遊戲、伺服器或 DB，也沒有改產品程式。

1. **TW16**（`TW1608262200.bin`，SHA `e12949cf…41a2`）：把 P01 IDB 複製到 `scratch/rea/ida/tw16.idb`（SHA `3a0bf206…7e82f`，已核對），用 IDA 7.7＋Hex-Rays 跑了 19 輪唯讀查詢。
2. **TW13**（`TW13081901.unpacked.exe`）：TW16 的舊底列是死碼（V5 已確認），所以按鈕狀態改在 TW13 查，共跑 10 輪。
3. **資料核對**：寫了兩支唯讀腳本，讀本機 `Sprite*.idx/pak` 與解密後的 `Tile.pak` XML：
   - 檢查雜湊規則、重名、旗標分布
   - 統計 `<n>c.img`
   - 統計 XML 的位元組編碼
4. 結果併入 `docs/reference/ui-layout.md`：
   - 第 1 節新增「框架層細節（V4）」
   - 開頭結論 #1 的 `-c` 敘述改為已驗證
   - 腳本表加上 `v4/`
5. 查詢腳本放在 `docs/reference/rea-scripts/v4/`，附 README。

## 結論摘要

| 項目 | 結果 | 狀態 |
|---|---|---|
| (a) `.img` 旗標 | 旗標只有 0／1。旗標≠0 時色鍵（像素＝透明色就不畫）；旗標＝0 時不透明。沒有半透明或加色。依據：`Surf::Draw` `sub_E539E0`（FO `0xA52DE0`）、色鍵 blit `sub_97CA60` | 已驗證 |
| (b) `a~b` 狀態 | 解析時只 `atoi` 取 `a`。狀態依按鈕類別：一般＝`a`、按下＝`a+1`；3 張圖的滑過＝`a+2`；2 張圖（含選單按鈕、`LEVEL_BTN`）的滑過＝`a+1`。依據：TW13 `LImageButton3Stage`（`sub_50CA20`）與 `Status800` 建構（`sub_77F6B0`） | 已驗證（TW13）；TW16 推測相同 |
| (c) XML 編碼 | BIN 不做 CP950 轉碼。版面 XML 用 expat，沒有宣告時預設 UTF-8；另一條 tinyxml2 路徑不轉碼。本機 65 檔：62 個純 ASCII，3 個 UTF-8，0 個 CP950 | 已驗證；expat 的辨識為推測 |
| (d) 缺 `-c` | 會退回無後綴檔。在封存檔層 `FileManager::Read`（`sub_52D6D0`／FO `0x12CAD0`）先找 `-c` 再找原名；`sub_B3D390` 自己組的 `"%s-%c.xml"` 是死值 | 已驗證 |
| (e) Sprite 重名 | 先找 `Sprite{byte 和 % 16}`，再找 `Sprite.pak`。53 個重名全部以 `SpriteNN` 為準。169,105 筆 `SpriteNN` 全部符合這條雜湊規則 | 已驗證 |
| (e′) **新發現** | `.img` 會先找台灣版 `<n>c.img`（`LocalFileNonDashFormatter`）。共 2,243 張，其中 1,500 張在 UI 範圍 2000–5800；外觀明顯不同（例：`3176c` 是金框，`3176` 是灰框加 F5–F12 字樣） | 已驗證 |
| (f) 字型 | `CreateFontA(12, …, 400, CHINESEBIG5_CHARSET, …, "MingLiU")`，用 GDI `ExtTextOutA`、背景透明；描邊是先在上下左右偏移 1 px 畫描邊色，再畫前景色 | 已驗證 |

各項的 VA／File Offset 都寫在 `ui-layout.md` 內文。

## 怎麼驗證

- **IDA 證據：** 每個結論至少有一個 Hex-Rays 偽碼或反組譯出處，輸出在 `scratch/rea/ida/{q01–q19,t13a–t13j}/`（未進 git）。
- **兩個樣本互相對照：**
  - TW13 的 `sub_759A10` 與 TW16 的 `sub_E539E0` 色鍵邏輯逐行相同。
  - TW13 的 `CUIStatusXML` 欄位位置，用解析者 `sub_5FE870` 的反組譯取得；`LEVEL_BTN` 在 `+0x58`，和 `Status800` 建構時讀取的位置一致。
- **資料核對：**
  - `img_check.py` 證實所有 `SpriteNN` 條目都符合 `NN == sum(name)%16`，53 個重名的 `SpriteNN` 那份都在可讀到的位置。
  - 旗標值只有 0 和 1。
  - `img_csuffix.py` 統計出 c 版圖的數量與差異。
- **目視：** 本機把 `3176`／`3176c`、選單按鈕、`LEVEL_BTN` 等轉成 PNG 檢查。PNG 只在 `scratch/rea/render/`，沒有進 git。
- **XML 編碼：** 對 65 個解密後的 XML 逐一檢查：是否純 ASCII、能否以 UTF-8 解碼、能否以 CP950 解碼。
- **雜湊：** 兩個 BIN 的 SHA-256 都核對過；TW16 IDB 副本的 SHA 也核對過。

## 未驗證什麼

- `LEVEL_BTN` 第 3 張圖 5728 的用途；`BALOG_BTN`／`ADENA_BTN`（`a~a+2`）用哪種按鈕類別。
- `TAB_BUTTON`（每 3 張一組）的狀態對應。
- TW16 的按鈕繪製：只追了 TW13，推定兩版相同。
- `sub_542370` 這條不看旗標的色鍵路徑，有哪些 UI 元件在用。
- 在 c 版與原版尺寸不同的 585 張中，有沒有 UI 版面用到的。
- MingLiU 12 在原版機器上是否呈現點陣字形（需要截圖）。
- 3 個 UTF-8 XML 中的非 ASCII 字，畫到畫面前有沒有再轉成 CP950。
- 兩個分析樣本都不是實際遊玩的 `LiTo.bin`（Themida 加殼）。沒有做任何真機比對。

## 產出路徑

- `docs/reference/ui-layout.md`：
  - 第 1 節「框架層細節（V4，Issue #459）」
  - 開頭結論 #1
  - 「可重跑腳本」表
- `docs/reference/rea-scripts/v4/`：IDA 查詢（`tw16_q01`–`q19`、`tw13_t13a`–`t13j`）、`img_check.py`、`img_csuffix.py`、README
- `docs/reports/issue-459.md`：本報告

## 給下一棒的提示

- **C1（框架，`SurfCatalog`）最重要：**
  - 對每個檔名 `F`，依序找：
    1. `Fc.img`，在 `Sprite{h}`
    2. `Fc.img`，在 `Sprite.pak`
    3. `F.img`，在 `Sprite{h}`
    4. `F.img`，在 `Sprite.pak`
  - `h = sum(檔名 bytes) % 16`，檔名包含 `.img`。
  - 依這個順序就能取代設計文件中「重名且內容不同就拒絕」的暫定規則。
  - **沒有找 c 版，台灣版 UI 會畫成錯的圖。**
- **C1（`.img` 透明）：** 旗標≠0 → 像素＝透明色時 alpha＝0；旗標＝0 → 整張不透明，忽略透明色欄。現行設計「一律當色鍵」需要修正。
- **C1（XML）：**
  - 沒有宣告時用 UTF-8，不要用 CP950（現有資料兩者結果相同）。
  - `-c`→原名的退回順序與原版相同，維持即可。
- **C1（`UiText`）：** `MingLiU`、高度 12（cell height，不是 9 pt）、`FW_NORMAL`、`CHINESEBIG5_CHARSET`；描邊是上下左右偏移 1 px 畫描邊色，再畫前景色。
- **ui-menu-buttons：** 選單按鈕（3177、3179…）是 2 張一組：一般＝`a`，滑過與按下＝`a+1`。3 張一組的按鈕：一般＝`a`、按下＝`a+1`、滑過＝`a+2`。
- **設計文件：** `docs/design/native-ui.md` 第 2.2、2.3、3.2 節中標為「待驗證（V4）」的列，可以依本文件改寫。本任務範圍是 `ui-layout.md`，所以沒有改設計文件，建議由 Claude（雲端）在審查時一併更新。
- **既有證據：** 第 2–6 節以前抽查的 PNG 尺寸以 `<n>.img` 為準，c 版尺寸可能不同，畫面元素要以 c 版重新核對。
