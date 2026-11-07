# #447 T173：音效共用層——距離衰減、全域冷卻、S22 模式與音量分類

依據：`docs/reference/sound.md`（T163／#431）第 2.3、2.6、2.7、3、4.1 節（皆標「已驗證」）。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/SoundRules.cs`（新） | 共用層。`SoundRules.DistanceVolume(dx,dy)`：`100 − 14·(nx²+ny²)/20`，整數除法，曼哈頓距離 > 14 夾成 (14,0)，負值為 0。`SoundCooldown.Shared`：每個音效 id 一份、全域共用的 200 ms 冷卻。`S22Mode`／`SoundRules.Mode`：0 單次、1 環境音循環、2 停止，3..255 回 null。`SoundVolumes`：Sound／EnvSound／Voice／Music 4 類各自可調（0..1）；Sound、EnvSound、Voice 三類都是 0 時不播（A06020）。`SoundGate.Admit`：依序檢查類別 → 距離 → 冷卻 → 算出音量倍率。`WorldSession.SoundOffset`：發聲體相對自身角色的格數。`ClientForm.SharedSoundGate`：產品內唯一的判定點。 |
| `engine/source/CombatSfx.cs` | `CombatSfxController` 新增 `Gate`／`Offset`。設定後，每次派送都先過 `SoundGate`；被擋下時記一筆 `gate` 觀測（Cooldown／Inaudible／OutOfRange／CategoriesSilent）。通過時以 `PlayAsync(id, owner, scale)` 送出。`CombatSoundInstance` 新增 `Category`、`CooldownFirstCycleOnly`。沒設 `Gate` 時行為與原本相同。 |
| `engine/source/ClientCombatSfx.cs` | 產品的戰鬥音效控制器接上 `SharedSoundGate` 與 `World.SoundOffset`（只改建立控制器那一行）。 |
| `engine/source/SoundPackets.cs` | S22 的第一個 byte 改當模式，`ServerSound.Repeat` 改名為 `Mode`，新增 `Kind`。**模式 0**：單次；同 id 的單次播放還沒結束時不重播（A05EA0）。**模式 1**：EnvSound 循環，每輪長度是 WAV 長度，冷卻只在第一輪套用；同 id 已在循環就不再啟動；不受 256 分鐘清理影響，持續到收到模式 2、換圖或重設。**模式 2**：移除這個 id 的單次與循環，循環的 owner 立即退役。**未知模式**：忽略並寫 notice。原本 ±100 ms 的幀／S22 首播去重只套用在模式 0。 |
| `engine/audio/IAudioPlayback.cs`、`AudioPlayer.cs`、`AudioCore.cs` | 支援每個 voice 各自的音量倍率：`PlayAsync(id, owner, voiceScale)`，介面有預設實作，舊的測試替身不用改。原生音量 = 主音量 × voice 倍率；改主音量時保留各 voice 的倍率。倍率不在 (0,1] 範圍內時回 `InvalidVoiceScale`。 |
| `engine/audio/P24.Audio.csproj` | 新增 `InternalsVisibleTo SoundRulesChecks`，讓測試能用假 driver 驗證 `AudioCore`。 |
| `engine/_integration/dev/sound-rules-tests/`（新） | 121 項離線檢查，見下節。 |
| `engine/_integration/dev/sound-packets-tests/Program.cs` | 舊的 repeat 斷言改成模式語義（148 項）。 |
| `.github/workflows/build.yml` | `map-continuity`（windows）job 新增一步，執行 sound-rules 與 sound-packets 兩組檢查。sound-packets 原本沒有接上 CI。 |
| `docs/protocol/sound-effects.md` | 用附加段落寫入 T173 的結論，取代原本「repeat＝額外次數」的推測政策。 |
| `engine/_integration/dev/rebuild-lock.json` | 用 `rebuild.py --update-lock` 重新產生。 |

**刻意不實作（文件標「推測」）：**

- Miles 的第三參數 `ext = 64 − 63·(nx+ny)/14`（extra volume）。
- x 座標先 `>>1` 的雙倍精度：`dx`、`dy` 直接以格計算。

## 怎麼驗證

- `rebuild.py --update-lock`，再跑 `rebuild.py --build --configfile .github/nuget.ci.config`：**PASS**。product DLL `230e0d23…`，audio DLL `9967d288…`。
- `sound-rules-tests`：**PASS，121 項**。全部是合成封包和合成位元組，接上產品 DLL 執行，有 socket 守衛。涵蓋：
  - 距離換算表逐點比對，加上 −30..30 窮舉。
  - 冷卻跨角色、跨管線都生效（影格音效擋 S22，S22 也擋影格音效）。
  - S22 `C(mode) H(id)` 的每個模式、截斷與非零 padding。
  - 4 類音量。
  - 控制器送出的倍率。
  - `AudioCore` 的「主音量 × 倍率」，以及改主音量後倍率仍保留。
- `sound-packets-tests`：**PASS，148 項**（改成模式語義後）。
- `combat-sfx-tests`、`effect6016-tests`、`demo-tests`：編譯通過，沒有錯誤。
- `check-no-game-assets.py`：**pass**（errors 0）。
- CI：PR 開出後由 GitHub Actions 執行，結果看 PR 頁面。

## 沒驗證什麼

- **沒有真機聽音**：模式 1 循環的接縫、距離衰減的實際聽感、冷卻的手感都還沒聽過。
- `combat-sfx-tests` 的完整執行：它要讀 `engine/assets` 與客戶端 WAV，在本機超過 9 分鐘沒結束，CPU 幾乎沒動。用 **HEAD（未改動）** 建出的基準 DLL 跑，240 秒後也還沒結束，所以這是**既有狀況，不是這次改動造成的**。這組測試本來就不在 CI 裡。
- 原版每次更新都會用 `A091C0` 重設播放中 sample 的音量。這裡只在派送當下固定倍率，播放中移動位置不會改音量（**已知差距**）。
- 原版音量 0 時仍會靜音播放；這裡不送出無聲的 voice，但冷卻照樣佔用。
- 4 類音量目前只能透過程式 API 調整，還沒有 UI，也還沒有讀寫 `lineage.cfg`：原版類別音量的數值範圍沒有查證，所以用 0..1 正規化。
- 模式 1 重複收包時不再啟動，與模式 3..255 一律忽略：這兩點是產品政策，原版沒有查證，**待驗證**。
- SoundHighlight 壓低、`^` 每個 id 5 個音源槽（T166）、`<` 併入 `[`（T165）：都不在本任務範圍。

## 產出路徑

- 程式：`engine/source/SoundRules.cs`，以及上表列出的修改。
- 測試：`engine/_integration/dev/sound-rules-tests/`。
- 協定附註：`docs/protocol/sound-effects.md` 的末段。
- 分支：`task/447`。

## 給下一棒的提示

- **T166（`^` 環境音）**：呼叫 `SoundGate.Admit(new(id, SoundCategory.Sound, offset, RequireAudible: true))` 就能套用「音量 0 不啟動、不佔冷卻」。5 槽表與 d² 排序要另外做。循環可以參考 S22 模式 1 的做法（`Loop` 加上 `CooldownFirstCycleOnly`）。
- **T165（`<`）**：只要改 `SoundSchedule.Candidates`。派送已經會經過 gate，冷卻與距離會自動套用。
- **介面音效**：用 `SoundGate.Admit(new(id, SoundCategory.Sound))`，`Offset` 填 null，音量就是 100。
- **持續音量更新**：要做到播放中跟著距離改音量，需要在 `IAudioPlayback` 加一個「依 owner／voice 調整倍率」的指令，並在每次 pump 時重算。
- **4 類音量 UI**：要掛在 `PlayerAudioSettings`／web 的 `audioSettings` 指令上，`ClientForm.SoundVolumes` 已經可以直接使用。
