# Issue #456 報告：V1 查證原版 HP／MP 浮動窗與頭上條

## 結論

- **原版有兩套 HP／MP 顯示。**
  - **浮動窗 `HpMpWindow`：** 可拖曳，預設在 (135, 465)，橫排、長、一般粗細。外框 surf 2355 尺寸 378×48；HP 條 2365（141×20）位於 (9, 11)；MP 條 2358（136×20）位於 (183, 11)。另有直排、3 種長度、thick 外觀、遊戲時鐘和低血紅框。
  - **頭上條：** 底圖 930、HP 931、MP 932，都是 44×5；每條畫 4 px 高，MP 在 HP 下方 4 px。其他物件的條在 sprite bbox 中心 −24、上緣 −12。
- **預設值：** 讀自 BIN 內明文的 `ClientConfig.proto` descriptor。
  - `showCharacterHPMP=true`
  - `FloatingHPMP{show=true, status=Status2, shape=1, thickness=false, showRecovery=true, showPercent=true}`
- **更正 T161 的推測：** `sub_919F30` 是選項視窗的 XML 綁定函式，不是 protobuf 解析者。protobuf 在 `sub_59DD30` 讀進全域變數。
- **Hex-Rays 會把 `sub_E538D0` 的 surf 參數截成 1 byte。** 頭上條的偽碼寫 162／163／164，反組譯才是 930／931／932。本輪所有直接常數的 surf 都已用反組譯核對。
- **引擎差異：** `ui-layout.md` 第 7 節 #4 從「無法判定」改為「不一致」，合計變成不一致 12、部分一致 2、無法判定 3。

## 做了什麼

- **準備分析環境：**
  - 把 P01 的 `TW1608262200.idb` 複製到 worktree 的 `scratch/rea/ida/tw16.idb`，SHA `3a0bf206…7e82f`，與 #429 基準相同。
  - 核對 `TW1608262200.bin`，SHA `e12949cf…641a2`，相同。
  - 用 IDA 7.7＋Hex-Rays 唯讀查詢 13 輪（q1–q13），subst 磁碟用 `Q:`，執行前確認沒有被占用。
- **從設定端往繪製端追：**
  - 字串與 xref：`ShowCharacterHPMP`、`HPMPBarThick`、`HPMPBarRecovery`、`HPMPBarPercent`。
  - 選項視窗：`sub_919F30`／`sub_919170` → 4 個切換處理函式 → `sub_9184B0` 寫回全域。
  - protobuf：解出 `ClientConfig.proto` descriptor（VA `0x11570D8`）。由 `sub_59DD30`、`sub_59E920`、`sub_5AA660` 對出每個欄位存在哪個全域變數。
  - RTTI → vtable → 建構子：`HpMpWindow`、`MiniHpMpBarManager`／`Draw`／`BlinkDraw`、`HyperAct*HPMP*`。
  - 浮動窗版面 `sub_ED9030`：用反組譯確認每個座標寫進哪個成員（時鐘 +0xC、MP +0x14、HP +0x1C、外框 +0x24……）。
  - 繪製：`sub_ED2680`、`sub_ED56D0`、`sub_ED6330`、`sub_EDB8E0`、`sub_8E7290`、`sub_8E77F0`、`sub_8E4D40`、`sub_8E4DE0`。
- **讀圖檔：**
  - 從本機 `Sprite*.pak` 讀出約 120 個 surf 的 `.img` 尺寸。
  - 把關鍵圖轉成 PNG，只放 `scratch/rea/render/`，目視確認顏色。
  - 從 BIN 讀出調色盤[0／7／9／14／33／53]、警示門檻 0.40、兩張時鐘表。
- **更新文件：**
  - `docs/reference/ui-layout.md`：第 2 節新增「HP／MP 浮動窗與頭上條（V1）」，含已驗證、推測、待驗證，每個地址都附 VA／File Offset。另外更新底列表、結論 5、第 7 節 #4 與合計、給後續任務的提示、腳本表。
  - `docs/reference/INDEX.md`：更新 T161 那一列的狀態。
- **保存可重跑腳本：** 放在 `docs/reference/rea-scripts/`，名稱以 `hpmp_` 開頭。

## 怎麼驗證

- **地址：** File Offset 依 PE 區段表換算：.text VA−0x400C00、.rdata VA−0x401400、.data VA−0x401800。用腳本逐一算過，無初值的變數標成 .bss。
- **設定與全域變數的對應，用三種方式交叉確認：**
  - 載入函式讀哪個 getter。
  - getter 讀哪個偏移。
  - merge 函式的 `case` 編號。
  - 另外對照全域初值（shape=1、status=2、兩個顯示旗標=1）和「全部重設」寫入的值。
- **surf 編號：**
  - 成員變數裡的編號看 `mov [reg+off], imm` 指令。
  - 直接常數的編號看 `push imm`，偽碼裡被截斷的 162／163／164／201、128–131、232／233 都已改正。
  - 尺寸讀自 `.img` 標頭，並確認 8＋寬×高×2＝檔案大小。
- **版面合理性：**
  - 各 status 下，HP、MP 條與按鈕的位置都落在外框寬度內。
  - 低血紅框圖剛好包住 HP 條的區域。
  - 頭上條的圖確實是 44×5：底圖是米框白底，HP 紅、MP 藍。
- **唯讀：** 結束時 `git status` 只有本任務的文件與腳本，沒有任何遊戲資源或渲染圖進 git。

## 未驗證什麼

- `FloatingHPMP.show`（`byte_138BE7D`）如何控制浮動窗顯示：沒找到它呼叫顯示／隱藏的地方。另外「全部重設」寫 0，與 descriptor 預設 true 不同。實際預設是否顯示要真機看。
- 時鐘圖的螢幕繪製點、自己頭上條 x 公式中 −80 的基準、800×600 下的 UI 原點偏移（推測為 0）。
- HP 條灰／綠變體的語義（推測綠是中毒）、回復光效條件中的 56／66／112、`byte_1542AA1 == 7`（推測是戰士）。
- 本機缺檔的 surf：閃爍 8393、按鈕與圖 5866–5868、徽章 5760–5763／5864／5865。
- 沒有啟動遊戲做真機比對。

## 產出路徑

- `docs/reference/ui-layout.md`：第 2 節 V1 子節，以及第 7 節 #4
- `docs/reference/INDEX.md`
- `docs/reference/rea-scripts/`：
  - `hpmp_ida_query.py`、`hpmp_x_rtti.py`、`hpmp_x_range.py`、`hpmp_x_dis.py`、`hpmp_x_asm.py`、`hpmp_queries.json`
  - `hpmp_pb_descriptor.py`、`hpmp_bin_values.py`、`hpmp_img_all.py`
- 本報告 `docs/reports/issue-456.md`
- 未進 git，只在本機 `scratch/rea/`：IDB 副本、IDA 輸出、反編譯檔、渲染圖

## 給下一棒的提示

- **ui-hpmp（code）：**
  - 照 `ui-layout.md` 第 2 節 V1 的兩張版面表實作。預設是 shape=1、status=2、thickness=false，位置 (135, 465)。
  - 外框寬會讓 x 被夾在 799 以內，拖曳時 y 上限 550。
  - 兩顆小按鈕分別切換排法、循環長度。
  - 設定沿用 protobuf 欄位語義（`ClientConfig` #10、#11）。
  - 數值只取伺服器值。百分比在 status 0 不顯示。
- **頭上條：**
  - 其他物件一律畫；自己受 `showCharacterHPMP` 控制。
  - 寬 44，填充寬＝44×百分比/100；HP 在 y，MP 在 y＋4。
- **圖檔：** 都可以在執行期從 `Sprite*.pak` 讀 `<surf>.img`，SurfList 中這些 id 都是 id==file，不必把資源複製進專案。
- **做 IDA 查證時：** 偽碼中任何 `sub_E538D0(小數字)` 都要回反組譯核對，避免同樣的截斷錯誤。T160–T163 若有引用直接常數的 surf 編號，建議抽查。
- **真機驗收時請順便確認：**
  1. 新角色進入遊戲時浮動窗是否預設顯示，對應 `FloatingHPMP.show`。
  2. HP 條灰色變體在什麼狀態下出現。
