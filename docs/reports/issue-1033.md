# Issue #1033 — T448 來源閉合：motion.json 與三張混色查表

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1033`；階段：resource。只新增文件，沒有改程式，沒有提交表的內容。

## 結論

1. **三張混色表全部 pass**：用純整數算式產生的 `screen`、`multiply`、`half` 各 2,048 bytes，與本機基線 `engine/assets/cache/*-channel.bin` 逐 byte 相同。
   - screen：`hi + (31 - hi) * lo / 31`（`hi = max(a,b)`、`lo = min(a,b)`）
   - multiply：`a * b / 31`
   - half：`(a + b) / 2`
2. **motion.json 全部欄位都有來源結論，沒有 blocked**：八方向是幾何規則（產品已有 `MovementRules.Dx／Dy`）；166 ms 是 list.spr `#3225` walk 每格 4 tick 換算（`floor(4×1000/24)`），664 是 4 格的總和；`spawn` 是 P06 研究時目視選定的固定值，不是原客戶端資料。
3. **BIN 檔案裡沒有這三張表**：表位在 `.data` 的未初始化區，BIN 裡只有產生它們的程式碼。所以算式就是唯一來源，產品不需要 BIN、IDB、Unicorn。
4. 產品目前只有 `Renderer.cs:33` 讀 `screen-channel.bin`；multiply 與 half 在 `Renderer.cs` 已經是算式，兩個檔沒有讀取點。
5. 細節（算式、VA／RVA／File Offset、逐欄表）在 `docs/reference/asset-recipes/motion-blend.md`。

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md` 的規則、#1027 報告、`docs/design/portable-delivery.md` 前置 3 與附錄 C。
- 讀研究腳本的算法（沒有執行）：P24 `cache_pixel_modes.py`、P06 `scripts/derive.py`。
- 用 pefile＋Capstone 靜態反組譯未加殼的 `TW1608262200.bin`（SHA-256 `e12949cf…09641a2`）：混色表產生函式 `0x0097B700` 起、max／min 輔助函式、常數 31.0、兩個八方向表的靜態建構函式（`0x01146030`、`0x01143F10`）。
- 在 `scratch/task-1033/` 寫三支 Python 腳本：`disasm_bin.py`（反組譯）、`gen_tables.py`（產生三張表並比對）、`check_motion.py`（motion.json 逐欄比對）。
- 讀產品的讀取點：`Assets.cs:53–55`、`Renderer.cs:31–35`、`World.cs`、`OnlineMovement.cs`。

## 怎麼驗證

- `gen_tables.py`：三張表各自與基線比 bytes 與 SHA-256，結果都是 PASS。screen 另外窮舉確認整數式與原碼的 double 式 1,024 格相同。
- `check_motion.py`：規則產生的物件與基線 8 個頂層欄位全等；以 2 格縮排、CRLF 輸出時 2,250 bytes 逐 byte 相同（SHA `050410a8…7c0b`）。BIN 兩張表的立即值與規則相符。
- BIN 的 SHA 與 `derive.py` 鎖定的值相同。

## 未驗證

- 沒有開 IDA／IDB。任務寫「用 IDA 靜態讀」，實際用的是 pefile＋Capstone 直接反組譯檔案，同樣是靜態、不執行；讀到的指令與位址都列在配方文件裡。
- 沒有建置、沒有執行任何編譯出的程式；產品改接是 T459 的事。
- 旗標 `0x013CC950` 的意義（565／555）是從移位量推測的，沒有追寫入點。
- BIN 表的序號就是協定 heading：依據是產品 `MovementRules` 的順序與既有真機證據，沒有追 BIN 裡讀表的程式。
- 同一函式還產生權重表與相加封頂表，不在範圍內，沒有比對。
- 沒有用 PakViewer 查 pak：本張沒有宣稱任何「缺資源」。

## 產出路徑

- `docs/reference/asset-recipes/motion-blend.md`
- `docs/reports/issue-1033.md`
- `scratch/task-1033/`（腳本與輸出，不進 git）

## 給下一棒的提示

- **T459**：
  - 只需要改 `Renderer.cs:33` 的 `originalScreen`：啟動時用整數式算 1,024 格。`MultiplyBlend`、`Blend` 不必動。測試可以窮舉 1,024 格，並檢查對稱與邊界（`f(0,b)=b`、`f(31,b)=31`）。
  - `Assets.cs:53–55` 改成不讀檔：`Directions` 由 `MovementRules.Dx／Dy` 產生，`FrameMs = 166`、`Spawn = [32588, 32928]` 寫成有註解的常數。
  - `engine/_integration/dev/doors-tests/DoorChecks.cs:190` 會寫一份測試用的 `motion.json`（spawn 是 `[32600, 32940]`）。`Assets` 改成不讀檔後這個測試的起點會變，要一起看。
  - `engine/_integration/baseline-pins.json` 有這四個檔的 pin；檔案從交付清單拿掉時要一起處理。
- screen 的算式不要寫成 `31 - (31-a)*(31-b)/31`（向下取整的版本有 900 格不同）。
- 自己寫的 Python 腳本不要取名 `dis.py`：會蓋掉標準庫的 `dis`，Capstone 匯入會失敗。
