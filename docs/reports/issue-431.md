# Issue #431：T163 查證原版——音效播放策略

分支 `task/431`。完整內容見 [docs/reference/sound.md](../reference/sound.md)。

## 結論

- **`<` 與 `[` 的播放完全相同**，都走 `A060F0`，同發聲體可以重疊播放；`^` 走 `A06210`。
- **`127 - 7*round(sqrt(d²))` 不是音量。** 它寫入 `var_DC` 後從未被讀取，是死碼。真正的音量算式在 `A3FD1D..A3FD7B`：`max(0, 100 − 14·(nx²+ny²)/20)`，曼哈頓距離 > 14 時為 0。另有 `ext = 64 − 63·(nx+ny)/14`，語義待驗證。四個常數窗在 TW13 各找到唯一一筆。
- **`^` 循環已由機器碼證實傳 `AIL_quick_play(h, 0)`。**「0 就是無限循環」是 Miles 慣例，屬推測。槽表以音效 id 為索引、全世界共用，最多 5 個音源；滿了只換入較近者，停掉最遠者，再依 d² 排序。
- **200ms 冷卻：** 每個音效 id 一份、全域共用，存在 id 快取物件 `+12`。影格音效、封包音效、UI、環境音都會套用。
- **S22（TW16 case 91，`94C4C0`）的第一個 byte 是模式**：0 單次、1 環境循環、2 停止，不是重複次數。
- **背景音樂：**
  - 由 `music.def` 的地圖區域決定；單次播放，依 `INTERVAL` 換曲，不連續重複同一首。
  - 戰鬥時切到索引 9 或 10，15 秒無戰鬥後以 0.8 秒淡出切回。
  - 另有 `LoginMusic`、`envsound.def` 主環境音（1 秒淡入淡出）與 `ONE_SOUND`。
- **與引擎的差異：14 項**（10 項不一致、4 項部分），1 項一致。

## 做了什麼

1. 把 P01 IDB 複製到 `scratch/rea/ida/tw16.idb`，SHA 與原檔相同。用 IDA 7.7＋Hex-Rays 跑了 9 輪。
2. 從 Miles／WINMM 匯入與音效字串（`music.def`、`envsound.def`、`sound\%d.mp3` 等）往回追呼叫端，共反編譯約 150 個函式。
3. 反組譯 `A3EF40` 全函式、`A06020..A06210`、`A06AC0..A06E40`，用來核對 Hex-Rays 遺漏的傳參與死碼。
4. 用 dispatch 標籤找出封包 case 91（S22）與 108 的處理函式。
5. 用 `xver_snd.py` 在 `TW13081901.unpacked.exe` 找同形狀的常數窗。
6. 派一個唯讀子代理盤點 `engine/source`、`engine/audio` 的音效實作，再抽查 4 處行號。

## 怎麼驗證

- 地址換算沿用 #417 的規則：`.text` 的 File Offset = VA − `0x400C00`。
- 音量、`ext`、曼哈頓距離上限 14、`127−7r` 四個窗，在 TW16 與 TW13 都只各找到 1 筆。
- `var_DC` 的「只寫不讀」是在 `A3EF40..A400D0` 全段反組譯上，以 grep 確認。
- 冷卻與 `quick_play` 參數是在 `A06020` 的組語上確認（`add edx, 0C8h`、`push [ebp+arg_0]`）。
- 引擎行號抽查：`CombatSfx.cs:9-11`、`SoundPackets.cs:39-48`、`Effect6016Definition.cs:14`、`AudioPlayer.cs:48`。

## 未驗證什麼

- 全程靜態，沒有聽音或實機。
- `loop_count=0` 是無限循環：依 Miles 慣例推測。
- `ext`（`AIL_quick_set_volume` 第三參數）的聲學意義未查證。
- `+0x10` 座標是兩倍精度：推測。
- TW13 的 200ms 冷卻沒有比對到相同位元組。
- case 91 等於伺服器 S22：依欄位形狀推測。
- `music.def`、`envsound.def` 在資源包裡的實際內容沒有解出，所以音樂索引 9～23 的實際曲目未知。
- `^` 的停止時機（發聲體銷毀）沒有逐一追呼叫端。

## 產出路徑

- `docs/reference/sound.md`
- `docs/reference/INDEX.md`（T163 列）
- `docs/reference/rea-scripts/`：
  - 新增 `ida_snd.py`、`ida_dis.py`、`xver_snd.py`
  - `run_ida.ps1` 新增 `-Script`、`-Drive` 參數，舊用法不受影響
- `docs/reports/issue-431.md`

## 給下一棒的提示

- **T165：** 直接把 `<` 當成 `[`，並照原版只存 5 個候選、抽到 0 就不播（差異 1、3）。
- **T166：** 要做 4 件事：
  - 循環播放
  - 每個 id 全域 5 槽，以 d² 換入
  - `vol = max(0, 100 − 0.7·d²)` 與曼哈頓距離 > 14 靜音
  - 每個 id 200ms 冷卻
- **S22：** 目前把 `repeat` 當成重複次數。伺服器實務上都送 0，所以結果相同；要對齊原版，應改成模式 0／1／2。這屬於產品變更，要另開任務。
- **背景音樂、環境音：** 引擎完全沒有。開任務前先解資源包裡的 `music.def`、`envsound.def`。
- **IDA 重跑：**
  - `run_ida.ps1` 加 `-Drive W:` 之類的代號，可以避開並行任務。本輪用 Q: 時被外部卸載過一次。
  - 不要對它的輸出用 `Select-Object -First`。本輪踩過一次，`finally` 沒跑到，W: 殘留，手動 `subst W: /D` 清掉。
- `rea-scripts/nat.py`、`xver.py` 內寫死的中文路徑在 git 裡已變亂碼，重跑前要修正。`xver_snd.py` 改成由參數傳路徑。
