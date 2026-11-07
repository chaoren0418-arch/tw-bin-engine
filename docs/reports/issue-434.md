# Issue #434 — T166：地圖事件 `^` 環境音（code，第 1 次）

分支：`task/434`

## 做了什麼

依 `docs/reference/sound.md` §2（T163／#431）與 `ambience-and-map-events.md` §5（T162／#430）實作 `^` 的規則模型：

| 檔案 | 改動 |
|---|---|
| `engine/source/MapAmbientSound.cs`（新檔） | token 解析、進幀偵測、槽表、冷卻、距離音量、物件移除時停止；產品掛載入口 `MapAmbientSound.Observe` |
| `engine/source/MapEffects.cs` | `Renderer.ComposeMapEffects` 加一行掛載點 `MapAmbientSound.Observe(session,now)` |
| `engine/source/AudioApiTransport.cs` | 加一個介面 `IAmbientLoopAudio`（`Start`／`SetVolume`／`Stop`），因為 `AudioPlayer` 沒有循環播放，也沒有逐音源音量 |
| `engine/_integration/dev/map-ambient-tests/` | 離線測試與 README；接進 `demo-tests` 的 `map-ambient`、`map-continuity`、預設群組 |
| `engine/_integration/dev/rebuild-lock.json` | `--update-lock` 重產 |

實作的規則：

- **觸發**：地圖特效物件進到新影格，而且該格模式是 `^` 時觸發。候選數 1 取第一個，2～5 用亂數抽，抽到 0 就略過，不重抽。自己的角色不存在時不觸發。依 24 tick/s 計算絕對進幀序號；畫面停頓時最多補一個循環。
- **槽表**：以音效 id 為索引，整張地圖共用，每個 id 最多 5 個音源，依 `d²` 由近到遠穩定排序。已滿時只接受**比最遠者更近**的音源，等距也拒絕；換入時停掉最遠的音源。同一物件已有同 id 在循環時不重播。
- **啟動前提**：物件上一輪的音量為 0 時不啟動。主音量為 0 或靜音時不啟動（對應 A06020 的類別檢查）。每個 id 有一份 200 ms 全域冷卻，啟動回報失敗時不改槽表。
- **音量**：`100 − 14·(dx²+dy²)/20`，負值當 0；`|dx|+|dy| > 14` 時為 0。每次進幀先處理播放，再更新音量；音量有變才呼叫 `SetVolume`。走遠只把音量降到 0，**不停止**。
- **停止**：物件不在本輪清單時停止，並從槽表移除、後面的往前補。造成物件消失的情況包括換圖、區塊被逐出、世界 epoch 改變、`Renderable` 為假。同一鍵換了外觀或位置時也停止。
- **不使用** `127 − 7*round(sqrt(d²))`。
- 6016 不走本模組，仍由 `Effect6016Controller` 處理。

## 怎麼驗證

- `rebuild.py --update-lock` 之後跑 `rebuild.py --build`：`PASS dev`，productDll `ea1f7d51…e9c8e9`。
- `DemoChecks … map-ambient`：`PASS 321 DEMO checks`。其中 65 項是本模組的檢查，其餘是每次都會跑的 Party／Pets／Polymorph。
- `DemoChecks … map-continuity`（CI 跑的群組，已含本模組）：`PASS 23019 DEMO checks`。
- `engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。
- CI：push 後由 PR 觸發，結果見 PR。

## 沒驗證什麼

- **沒有實際出聲。** `AudioPlayer`（`engine/audio`）沒有循環播放（loop 0），也沒有逐音源音量，而 `engine/audio` 不在本任務可改的範圍內。所以 `MapAmbientSound.Backend` 預設是 `null`，產品掛載點在沒有後端時**不建立任何狀態，也不出聲**。規則已經完整，接上後端才會生效。我沒有用單次播放冒充循環，因為那樣會在任何距離都以全音量播放，與原版不符。
- 沒有做真機聽音：循環是否無縫、5 個上限是否聽得出來。
- **座標尺度是推測**：引擎的地圖特效與角色座標都以格為單位，視為等於原版的 `x>>1`。
- **新物件的初始音量是推測**：原版 group 的 `+12` 初值未查到，本實作用目前距離算出的音量代替「上一輪」的值。
- **一格同時出現多種符號時（例如 `<1^2`），模式取最後一個符號**。這屬推測，實際資料只出現單一種符號。
- **200 ms 冷卻只在本模組內共用。** 原版的冷卻跨戰鬥、封包、UI 管線共用，引擎目前各管線各自去重。
- 預設群組（不帶群組參數）在本機於 `Movement123` → `MovementRules.Passable` 拋例外。這發生在本模組之前，是地圖通行資料讀取的問題，與本改動無關；CI 沒有跑預設群組。
- 沒有新增 C／S 封包，`^` 的觸發來源是地圖特效影格。

## 產出路徑

- `engine/source/MapAmbientSound.cs`
- `engine/source/MapEffects.cs`（1 行）
- `engine/source/AudioApiTransport.cs`（介面）
- `engine/_integration/dev/map-ambient-tests/MapAmbientChecks.cs`、`README.md`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`Program.cs`（接上測試入口）
- `engine/_integration/dev/rebuild-lock.json`

## 給下一棒的提示

1. **建議開一張 code 任務：音訊層循環後端**，可修改 `engine/audio`。
   - 內容：在 `AudioCore`／`AudioPlayer` 加循環播放（loop 0）與逐 voice 音量（0..100，乘上主音量），再寫一個 `IAmbientLoopAudio` 的實作。
   - 生效方式：在 `ClientForm` 設定 `MapAmbientSound.Backend`，並在關閉時停止所有音源。
   - 音效檔：`^` 的 id（例如 3219、3254）可能不在 `AudioAssets.LoadCombat` 釘選的 33 個 wav 裡。依 sound.md §3，要先查 `sound\%d.mp3`，找不到再查 `.wav`。
2. 接上後端後，由 dot 在 P24Lab 做聽音 smoke。可以站在有 `^` 特效的地圖，例如 1036、1859～1866 這些特效所在的地圖，檢查：有聲音、走遠音量變小、走回恢復、換圖停止。
3. 第二參數 `ext = 64 − 63·(|dx|+|dy|)/14` 沒有實作，因為它的聲學意義（pan 或額外音量）待驗證。
