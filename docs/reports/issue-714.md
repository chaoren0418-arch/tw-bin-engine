# Issue #714 — T302：R4-K 聲音：播放中隨距離改音量

依據：`docs/reference/gap-matrix.md` 13.5 R4-K（T291／#701）、SD§2.3（`docs/reference/sound.md`，`A3FCBE..A3FD7B` → `A091C0` → `A06D60`）、第 1 節 op 91。

## 做了什麼
- **`engine/audio/IAudioPlayback.cs`**：新增 `SetVoiceLevelAsync(owner, voiceScale, ext)`，用來改正在播放的音量。它會套到 owner 名下所有還在播的 voice，對應 `A091C0` 對發聲體名下每個 sample 呼叫 `A06D60`。這是預設介面方法，預設回 `Unsupported`，所以既有的 `FakeAudio`（combat-sfx／sound-rules／sound-packets 測試）不用改。
- **`engine/audio/AudioCore.cs`**：
  - `Voice.Scale` 改成可變，另加 `Ext` 欄位，預設 64（A06C30）。
  - 新增 `SetLevel`：owner 代數要相符，但收尾中（`Finish`）的 owner 也接受，這樣尾音也會跟著距離變。檢查 scale 在 0..1、ext 在 0..128，再重下 `driver.Gain(主音量 × 新比例)`。
  - 比例 0 只靜音，不停播（`A091C0` 只會把負值夾成 0）。
  - Gain 失敗就釋放該 voice，和 `Configure` 一樣不留在不受控的音量。
  - 之後主音量有變動時，`Configure` 會沿用新的比例。
  - 另加 `Levels(owner)` 給測試讀。
- **`engine/audio/AudioPlayer.cs`**：新增 `SetVoiceLevelAsync`。在 host gate 上同步拒絕已關閉、比例不合法、ext 不合法、代數不符的請求，其餘排進傳輸執行緒呼叫 `core.SetLevel`。
- **`engine/audio/P24.Audio.csproj`**：加 `InternalsVisibleTo P11.ClientEngine`。目的只是讓產品組件裡的模組測試能用假驅動直接測 `AudioCore`／`AudioPlayer.ForTests`。
- **`engine/source/AudioApiTransport.cs`**：新增 `SetVoiceLevelAsync(EffectOwner, …)`，直接轉給 `AudioPlayer`。`IEffectAudio` 不在可改範圍，沒有動它。
- **`engine/source/CombatSfx.cs`**：只改用到 `IAudioPlayback` 的地方。
  - `Lease` 記住最後一次送出的音量。
  - 新增 `ApplyLevels(func)`：替播放中和收尾中的實例算音量，值變了才呼叫 `SetVoiceLevelAsync`。函式回 null 就不更新，對應原版發聲體或自己不在場時 `A3FCBE` 不呼叫 `A091C0` 的情形。比例夾在 0..1，ext 夾在 0..128。觀察記錄的階段名是 `level`。
- **`engine/source/SoundPackets.cs`**：
  - 新增 `WorldSession.VoiceLevel(sound, volumes)`。S22 用每次更新算好的 `PacketSoundLevel`（A091C0 的 vol／ext）。有發聲體的戰鬥影格音效照同一公式即時算：vol＝100−14·(nx²+ny²)/20（夾 0），ext＝64−63·(nx+ny)/14（夾 ≥0），曼哈頓距離 > 14 時夾成 (14,0)。最後的比例＝類別音量·vol/100（A06D60），主音量再由 AudioCore 乘上。
  - `ClientForm.WithPacketSounds` 每次更新都呼叫 `combatSfx.ApplyLevels`，所以 S22（和有位置的戰鬥音效）在播放中會真的改音量；類別音量變動也會套到正在播的 voice。
- **`ProductSoundBackends.cs`**：沒有改。背景音樂、環境音、`^` 的後端本來就有 `SetVolume`。這次缺的是戰鬥／S22 的傳輸層（`AudioPlayer.CreateCombat`），已經在 `engine/audio` 接上。
- **新檔 `engine/source/SoundVolumeUpdateTests.cs`**：`[WorldModuleTest("sound-volume-update")]`，共 5 段：
  - `Core`：改音量會重下 Gain，主音量變動時沿用新比例；拒絕代數不符、比例 >1 或 NaN、ext >128；比例 0 只靜音不停播；收尾中照常套用；Gain 失敗會釋放 voice；沒有 voice 時回 `NoLiveVoice`。
  - `Player`：同步拒絕不合法的請求；經傳輸執行緒套用後是 0.8×0.5；沒實作的後端走介面預設，回 `Unsupported`。
  - `Controller`：值變了才送；回 null 不送；有夾範圍；收尾中的實例也送；停用或關閉後不送。
  - `WorldLevels`：S22 模式 0 套 SoundVolume、模式 1 套 EnvSoundVolume，ext 64；發聲體距離 5／2／>14 格時，(vol, ext) 分別是 (83, 42)、(98, 55)、(0, 1)；發聲體不在場，或非 S22 又沒有發聲體時回 null。
  - `EndToEnd`：S22 循環播放中改了類別音量，下一次更新就把新比例送到播放層。

  測試只用假驅動、合成封包，以及寫在輸出目錄下的自編位元組檔（不是遊戲素材）。

## 怎麼驗證
- **已驗證（本機）**：`dotnet build engine/source/OfflineClient.csproj -c Release` 建置成功，只有原本就有的 `MagicUi.cs` 兩個警告。本機 WebView2 套件快取不完整，會出 NETSDK1130（#607／#675 也遇過，跟這次修改無關）。所以 restore 時用 `scratch/t714/NuGet.Config` 唯讀指向主專案的 `engine/vendor`，並把 `NUGET_PACKAGES` 指到 `scratch/t714/packages`。這兩個都沒進 git。
- **已驗證（本機）**：`py -3 engine/tools/check-no-game-assets.py` → `status: pass`。
- **待驗證（CI）**：依本機規定，我沒有在本機執行任何編譯出來的程式，所以模組測試 `sound-volume-update`、既有單元測試，以及 combat-sfx、sound-rules、sound-packets 這幾組 FakeAudio 的編譯都要等 PR 的 CI 結果。

## 未驗證什麼
- 真機聽感：走近或走遠時音量是否平滑、DirectShow `put_Volume` 換算成 dB 後的聽感。這要交給 Codex／dot 在 P24Lab 實機聽。
- **ext 只走到協定與資料層**：值會一路傳到播放層，記錄在 voice 上，但沒有套用到裝置。原因是 Miles 第三參數 `extra_volume` 的意義在 SD§2.3 標為推測（可能是平衡或額外音量），DirectShow 沒有直接對應的設定。
- S22 封包本身沒有發聲體欄位，所以實際的 S22 值固定是 Self (100, 64)。播放中真正會變的是類別音量。距離變化實際作用在有發聲體的戰鬥影格音效上，公式一樣是 A091C0。

## 產出路徑
- 程式：上面列的 7 個檔案，加上新檔 `engine/source/SoundVolumeUpdateTests.cs`。
- 報告：`docs/reports/issue-714.md`（本檔）。
- 分支：`task/714`。

## 給下一棒的提示
- 如果之後能查證 ext 的意義（例如是平衡），可以在 `AudioCore.SetLevel` 裡把 `voice.Ext` 對應到 `IBasicAudio.put_Balance`。介面已經把 ext 傳進來了，不必再改協定。
- `IEffectAudio`（6016 的效果音）目前還沒有改音量的入口；需要的話，在它的介面加一個預設方法即可，`AudioApiTransport.SetVoiceLevelAsync` 已經寫好了。
- 本機建置環境：WebView2 的 NETSDK1130 照 #607 的做法處理（scratch 裡的 NuGet.Config 指向主專案 vendor，加上獨立的 `NUGET_PACKAGES`，不要帶 `-r win-x64`）。
