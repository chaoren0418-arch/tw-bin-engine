# 素材配方：motion.json 與三張混色查表（T448／Issue #1033）

日期：2026-11-13。執行者：本機 Claude（claude-local）。唯讀查核，沒有改程式，沒有提交任何表的內容。

- 基線：主目錄 `engine/assets`（junction → `releases/r29/assets`）。
- 靜態輸入：`D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin`，16,810,496 bytes，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，image base `0x00400000`。只用 pefile＋Capstone 讀檔反組譯，沒有執行、沒有 Unicorn、沒有開 IDA／IDB。
- 位址換算：RVA = VA − `0x00400000`；`.text` 的 File Offset = VA − `0x00400C00`；`.rdata` 的 File Offset = VA − `0x00401400`。
- 腳本與輸出在 `scratch/task-1033/`（不進 git）：`disasm_bin.py`、`gen_tables.py`、`check_motion.py`。

## 結論

| 項目 | 判定 | 比對結果 |
|---|---|---|
| `cache/screen-channel.bin` | **pass** | 算式產生的 2,048 bytes 與基線逐 byte 相同，SHA-256 `9f394f6f…23ad3c` |
| `cache/multiply-channel.bin` | **pass** | 逐 byte 相同，SHA-256 `4e2a67f5…a4d9eca8` |
| `cache/half-channel.bin` | **pass** | 逐 byte 相同，SHA-256 `52f925e7…feac5e5cb` |
| `motion.json` | **pass**（全部欄位可寫成產品規則） | 規則產生的內容 8 個頂層欄位全部相等；以 2 格縮排、CRLF 換行輸出時 2,250 bytes 逐 byte 相同，SHA-256 `050410a8…7d570c7c0b` |

沒有 blocked 項。三張表與 motion.json 都不需要 BIN、IDB 或 Unicorn 就能產生。

## 三張混色表

### 檔案格式

每張 2,048 bytes = 32 × 32 個 little-endian `u16`。第 `a*32+b` 個值是「色版值 a 與 b 混色後的 5-bit 色版值」，a、b、結果都在 0–31。三張表都對稱（`T[a][b] == T[b][a]`，已對基線驗過）。使用方式是 RGB555 三個色版各查一次，再移位組回去（`Renderer.cs:34`）。

### 算式（已驗證：1,024 格全部相符）

| 表 | 算式（整數，除法向下取整） | 原碼的寫法 |
|---|---|---|
| screen | `hi + (31 - hi) * lo / 31`，其中 `hi = max(a,b)`、`lo = min(a,b)` | double：`trunc(((31.0 - hi) / 31.0) * lo + hi)` |
| multiply | `a * b / 31` | `imul`、`cdq`、`idiv 31` |
| half | `(a + b) / 2` | `add`、`cdq`、`sub eax,edx`、`sar eax,1` |

screen 的補充：

- 原碼用 SSE2 double 計算，最後 `cvttsd2si` 截斷。Python 以同樣的 IEEE double 步驟算出的表與基線相同。
- 上表的整數式與 double 式在 1,024 格全部相同（已窮舉）。另外兩個等價寫法也窮舉相符：`(31*(a+b) - a*b) / 31`，以及 `31 - ceil((31-a)*(31-b)/31)`。
- `31 - floor((31-a)*(31-b)/31)` **不相符**（900 格不同），不要用。
- 建議產品用整數式，避免浮點；輸入只有 1,024 種，測試可以窮舉。

### 原碼位置（靜態讀）

產生函式從 VA `0x0097B700` 開始。外層迴圈變數 a 在 `[ebp-0xC]`，內層 b 在 `[ebp-0x10]`，都是 0–31。寫入位置是 `表基址 + (a << 6) + b*2`，也就是第 `a*32+b` 個 `u16`。

| 段落 | VA | RVA | File Offset | 內容 |
|---|---|---|---|---|
| 函式開頭 | `0x0097B700` | `0x0057B700` | `0x0057AB00` | 讀旗標 byte `0x013CC950`，決定移位量 11／6 或 10／5；兩層迴圈 |
| screen | `0x0097B766..0x0097B823` | `0x0057B766..0x0057B823` | `0x0057AB66..0x0057AC23` | 呼叫 max、min，double 運算，`cvttsd2si`（`0x0097B7D2`），寫三張表 |
| multiply | `0x0097B9B3..0x0097BA0F` | `0x0057B9B3..0x0057BA0F` | `0x0057ADB3..0x0057AE0F` | `b*a`、`idiv 31`（`0x0097B9C0`） |
| half | `0x0097BA0F..0x0097BA67` | `0x0057BA0F..0x0057BA67` | `0x0057AE0F..0x0057AE67` | `(b+a)` 有號除以 2（`0x0097BA15..0x0097BA18`） |
| max 輔助函式 | `0x004E7F50` | `0x000E7F50` | `0x000E7350` | 兩個 `int*`，回傳較大者的指標 |
| min 輔助函式 | `0x0064D7F0` | `0x0024D7F0` | `0x0024CBF0` | 兩個 `int*`，回傳較小者的指標 |
| 常數 31.0 | `0x01186FC8` | `0x00D86FC8` | `0x00D85BC8` | 8 bytes `00 00 00 00 00 00 3F 40` |

每種混色寫三張表：未移位的一張，加上左移到高色版與中色版的兩張。基線的三個檔案是**未移位**的那一張：

| 混色 | 未移位（基線檔案的來源） | 左移 `[ebp-0x1C]`（11 或 10） | 左移 `[ebp-0x20]`（6 或 5） |
|---|---|---|---|
| screen | `0x0142F5C0` | `0x0142E5C0` | `0x0142EDC0` |
| multiply | `0x014325C0` | `0x014315C0` | `0x01431DC0` |
| half | `0x01433DC0` | `0x01432DC0` | `0x014335C0` |

- 這些位址都在 `.data` 的未初始化區（`.data` VA `0x0135C000`、虛擬大小 `0x33A284`、檔案內只有 `0x43C00` bytes，表的位址超過 `0x0139FC00`）。**BIN 檔案裡沒有這些表的內容**，只有產生它們的程式碼。
- 旗標 `0x013CC950`（565 或 555 模式）只影響移位版，不影響未移位的三張表。所以基線三個檔與顯示模式無關。
- 同一個函式在 `0x0097B823..0x0097B9B3` 還產生 32 級權重表（`(b*k + (31-k)*a) / 31`），`0x0097BA67` 之後還有相加後封頂 31 的表。這些不在本張範圍，沒有對任何基線比對，只是記錄位置。

### 產品現況

| 位置 | 現況 | 建議（交 T459） |
|---|---|---|
| `Renderer.cs:33–34`（`ScreenBlend`） | 執行期讀 `assets/cache/screen-channel.bin`，只檢查長度 2,048 | 改成啟動時用整數式算出 1,024 格，不再讀檔 |
| `Renderer.cs:35`（`MultiplyBlend`） | 已經是算式 `a*b/31`，不讀檔 | 不必改 |
| `Renderer.cs:31`（`Blend`） | 已經是算式 `(a+b)>>1`，不讀檔 | 不必改 |

`multiply-channel.bin` 與 `half-channel.bin` 產品原始碼裡沒有讀取點（在 `engine/` 搜尋 `channel.bin` 只有 `Renderer.cs:33` 一處讀 screen）。這兩個檔可以直接從交付清單拿掉；screen 改成算式後三個檔都不需要準備器產生。`engine/_integration/baseline-pins.json` 有這三個檔的 pin，拿掉檔案時要一起處理。

## motion.json

### 逐欄來源

產品只在 `Assets.cs:53–55` 讀這個檔，讀的欄位是 `frame_ms`、`spawn`、`directions[].direction`、`world_delta`、`screen_delta`。其他欄位產品不讀。

| 欄位 | 基線值 | 來源 | 產品讀取 | 結論 |
|---|---|---|---|---|
| `schema` | `p06-motion-v1` | 研究腳本寫的標籤 | 否 | 常數，可拿掉 |
| `directions[].direction` | 0–7 | 陣列序號 | 是（檢查等於序號） | 規則 |
| `directions[].world_delta` | 見下表 | 八方位幾何：方向 0 是世界 y−1，之後順時針每 45° 一格 | 是 | **幾何規則**。產品已有同一份：`MovementRules.Dx／Dy`（`OnlineMovement.cs:5`） |
| `directions[].internal_delta` | `[2*dx, dy]` | BIN 靜態建構函式 `0x01146030` 的 16 個 push 立即值（x 以半格為單位，所以是 2 倍） | 否 | 可由 `world_delta` 算出；產品不需要 |
| `directions[].screen_delta` | `[24*(dx+dy), 12*(dy-dx)]` | 等角投影：一格寬 48、高 24 | 是（`Assets.cs:55` 用這條式子檢查） | **幾何規則** |
| `directions[].key` | Home、Up、PageUp、Right、PageDown、Down、End、Left | P06 研究版的鍵盤對應，寫死在 `derive.py` | 否 | 研究時的固定值，可拿掉 |
| `frame_ms` | 166 | `derive.py` 寫死。依據是 list.spr 的 `#3225`（王子）`0.walk(1 4,0.0:4<479 0.1:4 0.2:4<478 0.3:4)`：每格 4 tick，`floor(4*1000/24) = 166` | 是（`Assets.cs:55` 要求等於 166；`World.cs:48` 在非網路動畫時當每格毫秒） | **規則**：`4 tick × 1000 / 24` 取整。只用在離線預覽；連線時用 `NetworkStepMs / 4` |
| `step_ms` | 664 | `4 格 × 166` | 否 | 可由 `frame_ms` 算出；產品不讀 |
| `root_motion` | 一句英文說明 | 研究腳本寫的說明文字 | 否 | 可拿掉 |
| `source_va` | `0x01146030` | 出處標註 | 否 | 可拿掉 |
| `camera_quarter_step_va` | `0x01143F10` | 出處標註 | 否 | 可拿掉 |
| `spawn` | `[32588, 32928]` | **研究時的固定值**：P06 `HANDOFF.md` 記「起點已實際目視確認：主建物西南側道路邊」，寫死在 `derive.py` | 是（`World.cs:32–33`、`Program.cs:136／236`、`Tests.cs`） | 不是原客戶端資料。寫成產品常數（離線預覽起點） |

八方向的值（規則產生的與基線、BIN 立即值三方相同）：

| 方向 | world_delta | BIN `0x01146030`（x×2, y） | screen_delta | BIN `0x01143F10`（×4 後） |
|---:|---|---|---|---|
| 0 | (0, −1) | (0, −1) | (−24, −12) | (−24, −12) |
| 1 | (1, −1) | (2, −1) | (0, −24) | (0, −24) |
| 2 | (1, 0) | (2, 0) | (24, −12) | (24, −12) |
| 3 | (1, 1) | (2, 1) | (48, 0) | (48, 0) |
| 4 | (0, 1) | (0, 1) | (24, 12) | (24, 12) |
| 5 | (−1, 1) | (−2, 1) | (0, 24) | (0, 24) |
| 6 | (−1, 0) | (−2, 0) | (−24, 12) | (−24, 12) |
| 7 | (−1, −1) | (−2, −1) | (−48, 0) | (−48, 0) |

封閉式：`dx = sgn(sin(d×45°))`、`dy = −sgn(cos(d×45°))`（已對 8 個方向驗過）。實作時直接用 `MovementRules.Dx／Dy` 兩個 8 元素陣列即可，這是規則本身，不是抄來的表。

### BIN 佐證位置（靜態讀；產品規則不依賴它）

| 內容 | VA | RVA | File Offset | 說明 |
|---|---|---|---|---|
| 八方向步進表的靜態建構 | `0x01146030..0x011460A5` | `0x00D46030` | `0x00D45430` | 8 組 `push y; push x; mov ecx, 0x01656730 + 8*d; call 0x004F84A0` |
| 攝影機四分之一步表的靜態建構 | `0x01143F10..0x01143F85` | `0x00D43F10` | `0x00D43310` | 8 組，物件在 `0x0153CF40 + 8*d`；值是 `screen_delta / 4` |
| 二元組建構函式 | `0x004F84A0` | `0x000F84A0` | `0x000F78A0` | `this[0] = 第一個參數`、`this[4] = 第二個參數` |

`0x01143F10` 的表剛好是每個方向 `screen_delta` 的四分之一，與「一步分四格」一致。

### 給 T459 的規則

- `Directions`：由 `MovementRules.Dx／Dy` 產生，`Sx = 24*(dx+dy)`、`Sy = 12*(dy-dx)`。
- `FrameMs`：常數 166，註明是 `4 tick × 1000 / 24` 取整（list.spr `#3225` 的 walk）。
- `Spawn`：常數 `[32588, 32928]`，註明是離線預覽的起點、研究時選定，不是原客戶端資料。
- 其餘欄位不必保留。若準備器仍要寫出 `motion.json` 讓舊介面讀，用 2 格縮排、CRLF 換行就能與基線逐 byte 相同。

## 已驗證／推測／待驗證

已驗證：

- 三張表：Python 依算式產生，與基線逐 byte 相同（各 2,048 bytes，SHA 見結論表）。
- screen 的整數式與原碼的 double 式 1,024 格全同。
- 原碼位置、常數 31.0、max／min 輔助函式：靜態反組譯讀出，BIN 的 SHA 與 `derive.py` 鎖定的相同。
- motion.json：規則產生的物件與基線 8 個頂層欄位全等；CRLF 輸出逐 byte 相同。
- 兩張 BIN 表的 16＋16 個立即值與規則相符。
- `frame_ms` 的依據：基線 `character/animation.json` 的 walk 原文與每格 4 tick；96 格對應裡 walk 的 `base_duration_ms` 全是 166。

推測：

- `0x013CC950` 是 565／555 顯示模式旗標。依據只有移位量 11／6 對 10／5，沒有追這個 byte 的寫入點。
- BIN 表的序號 d 就是協定的 heading。依據是產品 `MovementRules` 用同一順序、連線移動已有真機證據（#23、#71），本張沒有追 BIN 裡讀這張表的程式。
- 連線遊玩時角色位置由伺服器封包決定，`spawn` 只影響離線預覽與測試。本張只看了 `World.cs` 與 `Program.cs` 的引用，沒有追完整的登入流程。

待驗證：

- 產品改成算式後的畫面與現況相同：要等 T459 寫完，由 CI 的窮舉測試確認；真機畫面交 T470／T474。
- 本張沒有建置、沒有執行任何編譯出的程式。
