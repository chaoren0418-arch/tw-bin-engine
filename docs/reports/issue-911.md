# Issue #911 — T396：R10-L MIDI 換曲

依據：`docs/reference/gap-matrix.md` 15.12.6 R10-L 列、15.12 對應列（5:music.def）；`docs/reports/issue-737.md` §3（8F1C30／8F2FB0／8F3B90／8F3D70 靜態逆向）；`docs/reports/issue-814.md` 第 49、61 行（引擎差異與建議做法）。

## 做了什麼

- `engine/source/BackgroundMusic.cs`
  - `IMusicStreamAudio` 加一個有預設實作的 `Replace(from, to, volume)`：預設＝先 `Stop(from)` 再 `Start(to)`，和原本的 `StopCurrent`＋`Start` 一樣。其他後端（測試記錄器、DemoChecks 的 `MusicRecorder`）不用改，行為也不變。
  - `BackgroundMusic.Start` 換曲時改成呼叫 `audio.Replace(正在播的曲目, 新曲目, 音量)`。`music < 0` 時照舊只停不開。關音樂（A06A10）、`Clear()` 仍走 `StopCurrent` → `Stop`，會真的停掉。
- `engine/source/ProductSoundBackends.cs`
  - `ProductMusicAudio.Replace`：一般模式照舊停舊開新。MIDI 模式照原版處理：
    - 新曲不在 0..8，而且 0.mid 還在播 → 不關也不開，把同一個 handle 改記到新曲目，音量改成新曲的音量（淡出後回到全音量）。
    - 新曲在 0..8 → 關掉 0.mid（8F5510），不開（`MidiSilenced`）。
    - 0.mid 沒在播（沒有舊曲，或串流層已經結束）→ 照 `Start` 開（8F63D0(0)）。
  - 新增診斷計數 `MidiKept`；`productSound` 快照多一欄 `midiKept`。
- `engine/source/MidiSlotTests.cs`（新檔，`[WorldModuleTest("midi-slot")]`，自動發現）：
  - 轉接層：區域 → 槽 9 → 區域都沿用同一個 handle；改到槽 0 時關掉而且不開；之後回區域重開；`Stop` 照舊真的關。
  - 串流層已經結束的 0.mid 不沿用，會重開。
  - 一般模式：停舊開新，槽 0 照播。
  - 介面預設 `Replace` 的呼叫順序和原本相同（stop → start）。
  - 端到端（BackgroundMusic → ProductMusicAudio → AudioStreamPlayer，MIDI 模式，替身 MCI）：打怪（槽 9）、打玩家（槽 10）、15 秒後淡回區域、換到另一筆區域記錄，整段只開一次 0.mid、沒有關掉；淡出後音量回到 0 dB；回登入（槽 0）會關掉；再進世界重開；關音樂會停掉。
- `engine/source/SoundFollowup3Tests.cs`（**不在任務列出的檔案裡，只改一個斷言**）：`MidiEndToEnd` 原本斷言「戰鬥時重開 0.mid」，寫的正是這次要修的引擎差異。改成「不關、不重開、Live＝1」。不改的話，既有測試會因為行為照原版修正而變紅。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`，0 個錯誤；3 個警告都在既有檔案（`HtmlActionTests.cs`、`MagicUi.cs`），和本次修改無關。
  - 本機環境備註（和程式無關，同 #675／#607）：直接 restore 會遇到 WebView2 的 NETSDK1130。改用 `scratch/issue-911/NuGet.Config`（唯讀指向主專案 `engine/vendor`），`NUGET_PACKAGES` 指到 `scratch/issue-911/packages`，restore 之後就正常。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4458 個追蹤檔，0 個錯誤）。
- 單元測試（`--module-tests`，含新的 `midi-slot` 與改過的 `sound-followup-3`）：依本機限制**沒有在本機執行**，交給 PR 的 CI。
- 資源：本任務不讀、也不宣稱缺少任何遊戲資源，所以不需要查 PakViewer。

## 未驗證什麼

- 真機聽音（M26）：需要準備一份沒有 `music*.mp3` 的 sound 測試副本，才會進 MIDI 模式。要確認區域 ↔ 戰鬥換曲時 0.mid 不會從頭播、登入／選角沒有聲音。
- 推測（沒有逐一核對原版）：
  - 頭目 SPAWN／DEATH 在 MIDI 模式下當成非系統槽處理，所以同樣沿用 0.mid。
  - 區域沒有 MUSIC 時（`Start(-1)`），引擎照舊停掉 0.mid。原版 8F1C30(-1) 依 issue-737 的條件（不在 0..8）可能會讓 0.mid 繼續播，這次沒有核對 8F31F0 在這種情況會不會呼叫 8F1C30。
  - 淡出期間仍會調 MIDI 音量（`MciMidiDriver` 在音量 0 時 pause）。原版 8F63D0 不設 MIDI 音量，這段沿用 T310 的推定。
- 響度曲線 `vol^(5/3)` 不在本組（等 M26）。

## 產出路徑

- `engine/source/BackgroundMusic.cs`、`engine/source/ProductSoundBackends.cs`、`engine/source/MidiSlotTests.cs`（新）、`engine/source/SoundFollowup3Tests.cs`（一個斷言）
- 本報告：`docs/reports/issue-911.md`
- 分支：`task/911`

## 給下一棒的提示

- gap-matrix 5:music.def 的 K5 列可以改成「MIDI 非系統槽換曲已照原版沿用（R10-L，#911）；剩下的是響度曲線（M26），以及 `Start(-1)` 在 MIDI 模式下的處理（推測）」。
- 想核對 `Start(-1)` 的話：用 IDA 唯讀看 8F31F0 遇到「區域沒有 MUSIC」時傳給 8F1C30 的參數。
- 其他 `IMusicStreamAudio` 後端如果之後也要共用串流，覆寫 `Replace` 就好，不用改 `BackgroundMusic`。
