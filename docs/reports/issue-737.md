# Issue #737：T310 R5-E 聲音收尾

依據：`docs/reference/gap-matrix.md` 第 14.6 節 R5-E；SD§2.3（`docs/reference/sound.md`）；第 1 節 op 91；第 5 節 music.def。
分支：`task/737`。

## 做了什麼

### 1. `ext` 真的套到裝置（`AudioCore.cs:24` 那一列）

**已驗證（靜態，客戶端 `D:\lineage開發\0-客戶端\Mss32.dll`，ImageBase `0x21100000`，只讀）**：`ext` 不是額外音量，而是 **pan**。

| 位置（RVA） | 內容 |
|---|---|
| `_AIL_quick_set_volume@12`（`0x1A870`） | 把 `(vol, ext)` 寫進 HAUDIO 的 `+0x1C`／`+0x20`，呼叫內部 `0x051D0` |
| `0x051D0` | 正在播（`quick_status==3`）而且是取樣類型時，呼叫 `_AIL_set_sample_volume_pan@12(sample, +0x1C, +0x20)`（`0x14F10`）；記錄字串是 `AIL_set_sample_volume_pan(0x%lX,%f,%f)` |
| `0x2A9E0`（set_volume_pan 的實作） | `v = vol^(5/3)`（常數 1.6667 在 `0x4BF78`）；`pan == 0.5` 時左右都是 `v·0.8122522`（＝0.5^0.3，常數在 `0x4BF70`）；否則 左＝`(1−pan)^0.3·v`、右＝`pan^0.3·v`（指數 0.3 在 `0x4BF68`） |

所以 SD§2.3 的 `ext = 64 − 63·(nx+ny)/14` 是 pan：自己身上 0.5（中央），愈遠愈小（愈偏左）。sound.md 第 113 行的「推測：extra_volume、不是 pan」要更正（本任務不改 sound.md，見給下一棒的提示）。

程式：
- `engine/audio/DirectShowDriver.cs`：`AudioGain.PanLevels(ext)`（上面的公式）、`AudioGain.Balance(ext)`＝`20·log10(右/左)`（1/100 dB，夾 ±10000；ext 64＝0）；`IAudioDriver.Balance`、`IPlaybackGraph.Balance`（預設不動作，替身不用改）；`WindowsPlaybackGraph.Balance` → `IBasicAudio.put_Balance`。
- `engine/audio/DirectShowInterop.cs`：`IBasicAudio` 補上 control.h 的 `get_Volume`、`put_Balance`、`get_Balance`（順序照 SDK）。
- `engine/audio/AudioCore.cs`：
  - `SetLevel`：ext 跟上次套用的不同時才下 `Balance`；失敗回 `PanFailed`、**保留 voice**（pan 不會讓最大聲的聲道變大，Gain 已受控），下一次更新重試。
  - 每個 owner 代數保存最後一次的 `(scale, ext)`（對應 A091C0 寫發聲體 `+12`／`+16`）；之後開播的 voice 直接用它，Play 之前先下 Balance（對應 A060F0 → A06C30、A06AE0）。Register／Retire 清掉。
- `AudioPlayer.cs`、`IAudioPlayback.cs`：改用 `AudioGain.ValidExt`、更新註解。
- `SoundPackets.cs`：`EmitterLevel.Pan`（`ExtraVolume` 保留，等同 Pan）。

**刻意的近似**：DirectShow 只有整體音量＋左右平衡，所以只換成左右「比」；中央時不乘 0.8122，偏一邊時較大聲那一邊不加大。響度曲線 `vol^(5/3)` 也沒有改（現有 `AudioGain.Decibels` 照舊），這兩點會影響絕對響度，列在「待驗證」。

### 2. `IEffectAudio` 改音量入口，接上 `SetVoiceLevelAsync(EffectOwner,…)`

`IEffectAudio` 本體宣告在 `Effect6016Host.cs`（不在可改範圍），所以：
- `CombatSfx.cs`：能力介面 `IEffectAudioLevel` ＋ 擴充方法 `IEffectAudio.SetVoiceLevelAsync(owner, scale, ext)`；沒有能力的實作回 `Unsupported`。
- `AudioApiTransport.cs`：實作 `IEffectAudioLevel`；產品工廠 `Create` 記下目前的 transport（`AudioApiTransport.Current`），`ShutdownWithinAsync` 時撤下。原因：`Effect6016Host` 不公開它的 transport，產品同一時間只有一個 6016 主機。
- `SoundPackets.cs`：
  - `WorldSession.EffectVoiceLevel(EffectFrameState, SoundVolumes)`：6016 發聲體＝特效所在格，照 SD§2.3 算 `(vol, ext)`，類別 SoundVolume；自身不在場回 null。
  - `EffectVoiceLevels`：值變才送、null 不送、夾範圍、消失的 owner 忘掉、換 transport 重來。
  - `ClientForm.Apply6016Levels`：在 `WithPacketSounds`（每次更新、`ApplyLevels` 之後）呼叫，所以 `SetVoiceLevelAsync(EffectOwner,…)` 現在有產品呼叫者。

### 3. music.def 的 MIDI 與聲像

**MIDI（已驗證，靜態，`TW1608262200.bin`；`.text` File Offset＝VA − `0x400C00`）**：
- `8F1C30`／`8F2FB0`／`8F3B90`／`8F3D70`：在 MIDI 模式（`byte_13D3F88`）下，曲目參數不在 0..8 且 MIDI 沒在播 → `8F63D0(0)`；參數在 0..8 且 MIDI 在播 → `8F5510`（MCI_CLOSE）。
- `8F63D0`（FO `0x4F57D0`）：`sprintf("%ssound\%d.mid", 根目錄, 0)` → `mciSendCommandA(0, MCI_OPEN 0x803, 0x3202, 型別 0x20B 序列器)` → `MCI_PLAY 0x806, MCI_NOTIFY`；`byte_13D3F89=1`。不設 MIDI 音量。
- `8F6340`：收到 `MCI_NOTIFY_SUCCESSFUL` → `MCI_PLAY(MCI_NOTIFY|MCI_FROM, from 0)`，等於無限循環；其他通知 → 關閉。
- 本機客戶端 `sound\` 有 `0.mid`，也有 161 個 `music*`，所以正常情況不會進 MIDI 模式。

程式（`engine/audio/AudioStreamPlayer.cs`）：
- `NeedsMidi(sound 目錄)`：目錄存在而且沒有任何 `music*.mp3`（不分大小寫）。`Create` 用它決定 `MidiFallback`。
- `Route`：MIDI 模式下，候選含 `musicN.mp3` 的要求一律換成 `0.mid`、循環；環境音等其他要求不變。新的 MIDI 要求先關掉舊的（單一 MCI 裝置）。
- `RoutingStreamDriver`：`.mid` 走 `MciMidiDriver`，其他走原本的 DirectShow。
- `MciMidiDriver`：`mciSendStringW` 的 `open … type sequencer`／`play`／`seek to start`／`status mode`／`close`；250ms 輪詢看到 `stopped` 就倒回重播（效果等同 8F6340）。音量 0 時 `pause`、恢復時 `resume`（**推定**：避免靜音時 MIDI 照裝置音量出聲；原版這段由呼叫端的類別音量判斷）。

**聲像（已驗證）**：music.def 沒有聲像欄位。TW16 開音樂串流後的 5 個 `AIL_set_sample_volume_pan` 呼叫點（`8F159C`、`8F1BE1`、`8F2DE8`、`8F2E1E`、`8F313C`）pan 都是常數 `0x115FFD0`＝**0.5**（File Offset `0xD5EBD0`），也就是中央。引擎串流不下 Balance，DirectShow 預設 balance 0＝中央，已經相同，不用改程式；測試確認 0.5 → balance 0。envsound.def 候選的第三欄 `panning` 依 sound.md 第 428 行，播放路徑不讀，這次沒動。

## 怎麼驗證

- 建置：`py -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile ../.github/nuget.ci.config` → `PASS dev`（暫時 lock，不寫入；**沒有改 `rebuild-lock.json`**）。worktree 沒有離線 `vendor`，所以用 CI 的 NuGet 設定。
- `py -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "errors": 0}`。
- 新離線測試 `engine/source/SoundFollowup2Tests.cs`（`[WorldModuleTest("sound-followup-2")]`，自動發現）：pan 公式（含與 Mss32 常數 0.8122522 對照）、AudioCore 下 Balance 的時機／失敗／重試、保存的 owner 音量套到新 voice、經 `IEffectAudio` 入口走真的 `AudioApiTransport`→`AudioPlayer`→`AudioCore`、`EffectVoiceLevels` 送出規則、`EffectVoiceLevel` 距離公式、MIDI 路由與 `NeedsMidi`、串流層 MIDI 循環與取代、音樂 pan。只用假驅動、自編位元組檔與假檔案清單。
- 依本機限制，**沒有在本機執行任何測試程式**；`--module-tests`（含既有 `sound-volume-update`、`sound-followup`、`product-sound-*`）交給 PR 的 GitHub Actions CI。
- 靜態逆向只用 pefile／Capstone 讀 `Mss32.dll` 與 `TW1608262200.bin`，沒有寫入客戶端或 BIN。

## 未驗證什麼（待驗證，交給 Codex／真機）

- 真機聽音：遠處的影格音效與 6016 是否真的偏左、偏移量是否接近原版；`put_Balance` 在 DSoundRender 上的實際效果。
- 絕對響度：中央 0.8122 係數與 `vol^(5/3)` 曲線沒有搬過來。
- MIDI：本機客戶端有 music mp3，不會進 MIDI 模式；`MciMidiDriver` 沒有在真機上開過（需要把 sound 目錄換成沒有 music*.mp3 的測試副本才能驗）。MCI 從背景 MTA 執行緒呼叫是否穩定也待驗證。
- MIDI 模式下，原版遇到 0..8 號系統槽（登入／選角）會**停止** MIDI；串流層看不到槽號，這裡仍會播 0.mid（偏差，需要改 `BackgroundMusic.cs` 或 `ProductSoundBackends.cs` 傳出槽號）。
- 6016 的發聲體座標用 `EffectFrameState.X/Y`，原版 x 會先 `>>1`（sound.md 2.3）；引擎沿用 T280／T302 的做法直接以格計。

## 產出路徑

- `engine/audio/AudioCore.cs`、`AudioPlayer.cs`、`AudioStreamPlayer.cs`、`DirectShowDriver.cs`、`DirectShowInterop.cs`、`IAudioPlayback.cs`
- `engine/source/AudioApiTransport.cs`、`CombatSfx.cs`（只加 IEffectAudio 入口）、`SoundPackets.cs`
- 新檔 `engine/source/SoundFollowup2Tests.cs`
- 本報告 `docs/reports/issue-737.md`

## 給下一棒的提示

1. `docs/reference/sound.md` 第 113 行與 `docs/protocol/sound-effects.md` 第 50 行要改成「已驗證：ext 是 pan」，附上本報告第 1 節的 Mss32 位址。gap-matrix 第 1 節 op 91、`EmitterLevel.ExtraVolume`、`SetVoiceLevelAsync(EffectOwner,…)` 這幾列可以改判。
2. 如果之後能改 `Effect6016Host.cs`，可以把 `IEffectAudioLevel` 併進 `IEffectAudio`（預設實作回 Unsupported），再讓主機自己送音量，就可以拿掉 `AudioApiTransport.Current`。
3. MIDI 模式要照原版處理 0..8 槽的停止，需要讓 `IMusicStreamAudio` 帶出槽號（`BackgroundMusic.cs`／`ProductSoundBackends.cs`）。
4. `vol^(5/3)` 響度曲線與中央 0.8122 係數要不要搬，等真機聽音後再決定。
