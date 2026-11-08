# Issue #533 報告：T226 查證原版——視窗世代與待查視窗

## 做了什麼

- 唯讀分析未加殼的 `TW1608262200.bin`（SHA `e12949cf…41a2`）。用 IDA 7.7＋P01 IDB 的 `scratch/` 副本（SHA `3a0bf206…e82f`，複製前後相同），跑了 8 輪唯讀查詢，地址都標 VA／FO。
- 解 UI XML 到 `scratch/`：本機客戶端 65 個；另外用 `Tile.idx` 搜尋本機其他版本客戶端，從 5 套（kor25、泡麵陳、760、89、雷雨）解出缺檔 XML，只放 `scratch/`。
- 產出 [`docs/reference/windows-2.md`](../reference/windows-2.md)：
  - 背包、魔法、倉庫的視窗世代
  - 血盟窗、變身選擇窗
  - 城堡、盟屋、數量輸入
  - 其他 XML 窗與舊式窗的觸發
  - 缺檔 XML 的來源比對
  - 9 個建議 code 任務（W2-A–I）
- 回填 [`gap-matrix.md`](../reference/gap-matrix.md)：第 1、4 節共 53 列的依據欄、第 8 節七列標已查、依據代號加 WIN2，並重算統計（第 4 節「需先查證」17 → 2）。
- 更新 [`INDEX.md`](../reference/INDEX.md)。
- 腳本與查詢清單放在 `docs/reference/rea-scripts/t226/`。

## 怎麼驗證

- 每個結論都附函式 VA／FO，大多附反編譯行或格式字串。
- 封包欄位逐一對照 `server-protocol.md`。血盟另外讀了伺服器原始碼 `C_Pledge.java`、`S_Pledge.java`（唯讀）。
- 缺檔 XML 用三種方式比對：
  - XML 事件名是否存在於 TW16 BIN（`t226_evbin.py`）
  - 是否命中建構子註冊的事件（`t226_evcmp.py`）
  - 引用的 surf 在本機 `Sprite*.idx` 有沒有（`t226_surfcheck.py`）
- `gap_count.py` 重算統計，與文件第 6 節一致。

## 未驗證什麼

- 全部是靜態分析。`LiTo.bin` 有殼沒查，所以「3.81 世代用舊式窗」是依本機資源推定。
- 待驗證：
  - 熱鍵字元碼對應的實體按鍵
  - `SpellWindow` 何時開啟
  - `WAREHOUSE_WINDOW` 設定的消費類別
  - 王冠圖源
  - 新式變身窗選項的預設值
  - `CSpecialPolymorphUI` 的開啟子碼
  - S136／S185 有一兩欄與 3.81 對不上（code 任務以 3.81 為準）
- 用 `D:\lineage開發` 下各資料夾的檔名找 XML 的背景搜尋，跑滿 10 分鐘逾時被停掉、沒有結果；改用 `Tile.idx` 搜尋取代。
- 沒有啟動遊戲、伺服器或 DB；沒有任何 XML、pak、圖檔進 git。

## 產出路徑

- `docs/reference/windows-2.md`
- `docs/reference/gap-matrix.md`、`docs/reference/INDEX.md`
- `docs/reference/rea-scripts/t226/`（6 個腳本＋`queries.json`）
- 分支 `task/533`

## 給下一棒的提示

- **可以直接開 code（依賴照 windows-2 第 9 節）：**
  - W2-A：伺服器主動送來、物件 id 是自己的 S39 頁（`monlist`、`pledge`）。和 M7-R 同檔，排在 M7-R 之後。
  - W2-B：城堡 `tax`／`withdraw`／`deposit` 頁與城主表（M7-C 擴充）。
  - W2-D：S136 改走 HTML 頁加數字框（M7-E 改寫）。
  - W2-E：聊天列 YES／NO 鈕座標（M7-J）。
  - W2-F：S250 52 料理窗。
  - W2-G：S250 117 血盟倉庫紀錄。
  - W2-C、W2-H、W2-I（盟屋地圖、血盟、舊式背包／魔法／倉庫原生窗）等 T190。
- **仍查不到：**
  - `QuestUI` 的 S250 `S_ClassicQuest` 格式
  - `QuitWindow`／`CNoticeWindow` 的使用點
  - 王冠圖號、`AgitAuctionUI` 的 XML（BIN 表沒有、各客戶端都沒有）
  - `SpellUI` 的視窗 XML
- **需要使用者決定：** 信件、釣魚、重置窗要不要採用其他本機客戶端的 XML 與 surf（第 8 節）。這屬於資源來源決策，未決前不做這三窗的畫面。
- 本機有 IDA 時，`scratch/rea/` 可整個重建：
  - 複製 IDB 到 `scratch/rea/ida/tw16.idb`
  - 把 `docs/reference/rea-scripts` 複製到 `scratch/rea/scripts`
  - `run_ida.ps1 -Drive <空的代號>`；**不要把輸出接到 `Select-Object -First`，否則 `finally` 不會跑、subst 會留著。**
