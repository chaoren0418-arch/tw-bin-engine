# Issue #814 報告：T351／R7-L 聲音收尾 3

依據：`docs/reference/gap-matrix.md` 第 15.6 節 R7-L；SD§2（`sound.md`）；`docs/reports/issue-737.md`（Mss32 pan、MIDI 靜態逆向）、`docs/reports/issue-778.md`（`<`／`[` 單次音效）。
分支：`task/814`。

## 做了什麼

### ① 單次音效播放中依距離改音量（A091C0）
- `MapAmbientSound.cs`
  - `IMapOneShotAudio` 加兩個有預設實作的成員：`SetVolume(voice, volume)`（預設不動作）、`Finished(voice)`（預設 `true`）。只實作 `Play` 的舊後端照舊可以用，模型也不會追蹤它播出的聲音（等同 R6-A）。
  - 每個發聲體多一份 `OneShots` 清單：`PlayOnce` 播出後，後端回報還沒播完就記下來。
  - `Enter` 的 A091C0 步驟：先剔除已經播完的聲音，距離音量有變時，`^` 循環和 `<`／`[` 單次音效一起改（走遠只降到 0，不停止）。順序照原版「先播、再調」：新的一聲仍用上一輪的音量開播。
  - 後端撤下或換掉時，舊的 voice 不再追蹤。物件移除時也不再追蹤，但**不主動停**（推測，見待驗證）。
  - 診斷用 `OneShotsLive`。
- `ProductSoundBackends.cs` 的 `ProductOneShotAudio`：改成記錄 voice → (handle, 距離音量)。實作 `SetVolume`（最終音量＝距離音量/100 × SoundVolume × 主音量）和 `Finished`。`SetScale` 變動時，還在播的單次音效也跟著改（同 `^` 的 A06D60 做法）。

### ② MIDI 模式遇到 0..8 號系統槽時停播
- `BackgroundMusic.cs`：`MusicTrack` 加 `Slot`（選填，預設 -1，舊的建構呼叫不受影響）。登入帶槽 0、選角帶槽 8、戰鬥帶槽 9／10；區域選曲、大地圖、頭目曲帶 -1。
- `engine/audio/AudioStreamPlayer.cs`：`IAudioStream.MidiFallback`（預設 `false`，`AudioStreamPlayer` 原有的屬性直接實作它）；`AudioStreamPlayer.SilencesMidi(slot)`＝0..8。
- `ProductMusicAudio.Start`：在 MIDI 模式下遇到 0..8 號槽就不開新的，回 false（計入 `MidiSilenced`）。`BackgroundMusic.Start` 一定先跑 `StopCurrent`，所以正在播的 0.mid 會被關掉，效果等於原版的 8F5510（MCI_CLOSE）。其他槽照舊由串流層換成 0.mid 循環。
- 快照 `productSound` 多了 `midi`、`midiSilenced` 兩欄。

### ③ 響度曲線 `vol^(5/3)` 與中央係數 0.8122：只做資料，不改播放
- `engine/audio/DirectShowDriver.cs` 的 `AudioGain` 加了 `MilesLevels(vol, pan)`、`MilesCenter`（0.5^0.3）、`MilesDecibels`，常數照 issue-737 的 Mss32 位址（RVA `0x4BF78`／`0x4BF70`／`0x4BF68`）。
- **播放路徑沒有換**，`AudioGain.Decibels` 照舊。依 gap-matrix 15.6 的規則，這一項要等真機聽音（M26）再決定，所以不做會改變聽感的部分。

### 測試
新檔 `engine/source/SoundFollowup3Tests.cs`，標記 `[WorldModuleTest("sound-followup-3")]`，靠自動發現註冊。內容：
- 單次音效跟著距離改音量：上一輪音量開播、走遠降到 0 不停止、播完的不再調、後端撤下或物件移除後不再追蹤、`[` 同樣適用、舊式後端不被追蹤。
- `ProductOneShotAudio` 的改音量、類別音量與播完後的行為。
- 經過真的 `AudioStreamPlayer`（替身驅動）：音量真的送到驅動；Poll 看到 ended 之後只調新的那一聲。
- `MusicTrack.Slot` 的來源：登入 0、選角 8、區域 -1、戰鬥 9／10。
- MIDI 模式下 0、8 號槽不開，區域和戰鬥照開；一般模式不受影響。
- 端到端（BackgroundMusic → ProductMusicAudio → AudioStreamPlayer，MIDI 模式＋替身 MCI）：世界開 0.mid → 回登入時關掉、不重開 → 每秒重試仍然不播 → 選角也不播 → 再進世界重開 0.mid → 戰鬥仍是 0.mid。
- Miles 曲線資料：0.8122522、`v＝vol^(5/3)`、偏一邊的公式、dB 換算、範圍檢查；也確認播放路徑的 `Decibels` 沒有變。

只用合成的 music.def 文字、合成地圖特效 JSON 和替身。不讀客戶端、不讀 `engine/assets`、不開音訊裝置。

## 怎麼驗證
- `py -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "errors": 0}`。
- 編譯：`dotnet restore`（NuGet 設定指向主專案 `engine/vendor`，檔案在 `scratch/issue-814/nuget.config`）之後跑 `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore`。這台機器有 #761／#811 記錄過的 WebView2 環境問題（NETSDK1130）。我在 scratch 掛了拿掉 winmd 的 hook，之後剩下的 15 個錯誤**全部在 `WebPanel.cs`／`WebLocalResource.cs`**（WebView2 型別）。本次改的 6 個檔案沒有任何診斷。完整編譯綠燈以 CI 為準。
- `rebuild.py --build` 也遇到同一個 NETSDK1130（環境問題），沒有寫入 lock。
- 依本機規定，**沒有在本機執行任何測試程式**。`--module-tests`（`sound-followup-3`，以及既有的 `map-effect-followup`、`sound-followup`、`sound-followup-2`、`product-sound-*`）交給 PR 的 GitHub Actions CI。

## 未驗證什麼（待驗證）
- 真機聽音（M26）：地圖 `<`／`[` 單次音效走遠時是否跟原版一樣變小聲；MIDI 模式要準備一份沒有 `music*.mp3` 的 sound 測試副本才能驗，登入／選角應該沒有聲音。
- 推測：物件移除（A04A20／A04BF0）時原版會不會也停掉該物件名下的單次 sample，還沒查。這次維持 R6-A 的做法：不停，只是不再追蹤。
- 推測：頭目 SPAWN／DEATH（8F3990）在 MIDI 模式下的處理，沒有逐一核對；這次當成非系統槽（播 0.mid）。
- 引擎差異：MIDI 模式下，非系統槽之間換曲（例如區域 → 戰鬥）會關掉 0.mid 再重開，所以會從頭播。原版的「MIDI 沒在播才開」不會重頭播。要照原版的話，需要讓 `ProductMusicAudio` 在 MIDI 模式下共用同一個 handle。
- 響度曲線：要不要把 `vol^(5/3)`／0.8122 搬進播放路徑，等真機比較後再決定（資料已備好：`AudioGain.MilesLevels`）。

## 產出路徑
- `engine/source/MapAmbientSound.cs`、`engine/source/ProductSoundBackends.cs`、`engine/source/BackgroundMusic.cs`
- `engine/audio/AudioStreamPlayer.cs`、`engine/audio/DirectShowDriver.cs`
- 新檔 `engine/source/SoundFollowup3Tests.cs`
- 本報告 `docs/reports/issue-814.md`
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`。

## 給下一棒的提示
1. M26 真機聽音之後，如果決定採用 Miles 曲線：把 `AudioStreamPlayer`／`AudioCore` 的 `AudioGain.Decibels(volume)` 換成 `MilesDecibels(MilesLevels(volume, pan) 較大的那一邊)`，再加 Balance。中央時整體大約 −1.81 dB。
2. 要讓 MIDI 換曲時不從頭播：`ProductMusicAudio` 在 `stream.MidiFallback` 時保留同一個 0.mid handle，只有系統槽或音樂關閉時才 Stop（要能分辨 `BackgroundMusic.StopCurrent` 的原因）。
3. 查 A04A20 是否也停單次 sample。如果會停，要在 `MapAmbientSound.Release` 對 `OneShots` 呼叫後端停播（`IMapOneShotAudio` 再加一個 `Stop` 預設成員）。
