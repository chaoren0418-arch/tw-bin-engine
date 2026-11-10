# Issue #778 報告：T332／R6-A 封包特效與地圖單次音效

依據：`docs/reference/followups-7.md` 第 8 節 R6-A；SV§2.5（`skills-visual.md:116`）；`sound.md` §2.1、§2.4、§2.6、§2.7；REA B04（`rea-inventory.md:114–117、171–172`）。

## 做了什麼

### ① S55 用對象收包時的朝向（`MapEffects.cs`）
- `PacketEffect` 加 `Direction`、`Follows`、`Origin`（init 屬性，`PacketBoxMessages.cs` 既有建構不受影響）。
- `ReceiveEffectPacketCore`：S55 收包當下記下對象（自己或其他物件）的朝向與位置（新 `PacketEffectDirection`）。
- `PacketEffectAppearance`：`At(elapsed, direction)`、`TimelineFor(direction)`。用 magic 素材時依朝向取序列；素材沒有該方向時退回方向 0（等於舊行為）；`.s32` 地圖素材沒有方向，任何方向都一樣。
- `PacketEffectVisual` 加 `Direction`（預設 0）；`ComposePacketEffects` 的 `]` 子特效改用這個方向（原本固定 0）。S106 仍是方向 0。

### ② 只有 7 個 gfx 跟著對象（`MapEffects.cs`）
- 新 `PacketEffectRules.Following`＝{6985, 7179, 8150, 8152, 8981, 7834, 7836}。
- `PacketEffectFrames`：這 7 個 gfx 每格重讀對象位置；其他 S55 畫在收包位置。
- 對象離開可見世界時特效照舊結束（`SoundPackets.cs` 的 `PrunePacketMedia` 不在可改範圍，沒動）。

### ③ 地圖特效 `<`／`[` 單次音效（`MapAmbientSound.cs`、`ProductSoundBackends.cs`）
- 新介面 `IMapOneShotAudio`、紀錄 `MapOneShotVoice`（放在 `MapAmbientSound.cs`）。
- `Enter`：模式 0（`<`）、1（`[`）跟 `^` 一樣選候選（最多 5 個、1 個取第 0 個、抽到 0 略過不重抽），之後走 `PlayOnce`（A060F0）：
  - 三類音量都是 0 → 不播、不佔冷卻（A06020）；
  - 每個 id 全域 200ms 冷卻，和 `^`、戰鬥音效同一份 `SoundCooldown`；
  - 音量用發聲體**上一輪**的距離音量（A060F0 在 A091C0 之前）；
  - 沒有槽表、沒有「音量 0 不啟動」前提，可重疊；音量 0 時冷卻照佔，但不送出無聲 voice（同 `SoundGate` 的做法）。
- `Sources` 改用新的 `HasSound`（`<`／`[`／`^` 任一），6016 仍排除（由 Effect6016Controller 負責）。`HasAmbient` 語意不變。
- 產品接線：`ProductOneShotAudio`（單次、先 `N.mp3` 再 `N.wav`、最終音量＝距離音量/100 × 類別音量 × 主音量）；`ProductSound.Pump` 設 `MapAmbientSound.OneShotBackend`，`Disarm` 還原，snapshot 多 `oneShot`。

### 測試
- 新檔 `engine/source/MapEffectFollowupTests.cs`（`[WorldModuleTest("map-effect-followup")]`，自動發現）：跟隨清單、S55 朝向／收包位置／跟隨、S106 方向 0、`<`／`[` 進幀播一次、上一輪音量、共用冷卻（含與 `^` 共用）、音量 0 佔冷卻、靜音不佔冷卻、沒後端無作用、候選 0／多候選／無自身角色、產品後端檔名與音量。
- **改了一個範圍外的既有測試（必要）**：`engine/_integration/dev/sound-packets-tests/Program.cs` 的「S55 follows current target position」原本用 gfx 1036；依 ②，1036 不再跟隨，所以改用 6985（同一列只改 gfx）。

## 怎麼驗證
- `dotnet build engine/source/OfflineClient.csproj -c Release -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false`（輸出到 `scratch/issue-778/build`）：**建置成功，0 錯誤**。
- `py -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`、errors 0。
- 依本機規定**沒有在本機執行任何測試程式**；`--module-tests`（含 `map-effect-followup`）與 sound-packets／map-ambient 測試交給 PR 的 GitHub Actions CI。

## 未驗證什麼（待驗證）
- CI 結果（push 後看 PR）。
- 真機：S55 方向在實際有方向的 magic 素材上是否正確；7 個跟隨 gfx 的實際觀感；地圖上 `<`／`[` 音效的音量與頻率。需要開遊戲，交給 Codex／dot。
- **推測**：單次音效的類別音量用 `SoundVolume`（`sound.md` §4.1 只說 A06C30 對有 `+5` 旗標的 sample 用 `EnvSoundVolume`，A060F0 新建的 sample 沒看到設這個旗標）。
- **引擎取捨**：單次音效播放中不再跟著距離改音量（原版 A091C0 會改發聲體名下還在播的 sample）；不跟隨的 S55 在對象離開時仍隨對象結束（原版是否繼續留在原地沒查）。

## 產出路徑
- `engine/source/MapEffects.cs`、`engine/source/MapAmbientSound.cs`、`engine/source/ProductSoundBackends.cs`
- `engine/source/MapEffectFollowupTests.cs`（新）
- `engine/_integration/dev/sound-packets-tests/Program.cs`（一列）
- 本報告 `docs/reports/issue-778.md`
- 分支 `task/778`；沒有改 `rebuild-lock.json`、`WorldSession.cs`、`Program.cs`、`DemoChecks.csproj`、各註冊表。

## 給下一棒的提示
- 若要讓單次音效播放中跟著距離改音量，`IMapOneShotAudio.Play` 需要回傳 handle，模型要在 `Enter` 的音量更新段一起 `SetVolume`（參考 `^` 的 `emitter.Voices`）。
- 類別（`SoundVolume` 或 `EnvSoundVolume`）可查 A05410／A06C30 是否對 A060F0 的 sample 設 `+5`。
- 「不跟隨的 S55 在對象消失後是否繼續播」若要照原版，要改 `SoundPackets.cs` 的 `PrunePacketMedia`（本任務範圍外）。
