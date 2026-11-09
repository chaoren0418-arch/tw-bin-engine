# Issue #673 T280：R3-D 聲音收尾

依據：`docs/reference/gap-matrix.md` 12.5 R3-D、SD§2.3（`sound.md`）、FU4§6（`followups-4.md`，T287／#680 Q3-8）。

## 做了什麼

1. **距離音量＋`ext`（SD§2.3 `A3FCBE..A3FD7B` → `A091C0`）**，`SoundPackets.cs`
   - 新增 `EmitterLevel(Volume, Ext)` 與 `SoundAttenuation.Level(dx, dy)`：nx+ny>14 夾成 (14,0)；vol＝100−14·(nx²+ny²)/20（夾 ≥0，與 `SoundRules.DistanceVolume` 共用）；ext＝64−63·(nx+ny)/14（夾 ≥0）。`ExtraVolume`＝ext/128（A06D60）；沒有發聲體時用 A06C30 的預設值 (100, 64)＝0.5。
   - S22 每次更新重算：`ComposePacketSounds` 每次組音效時替每筆播放中的 S22 重算 (vol, ext)，記在 `PacketSoundLevel(n)`、`PacketSoundLevelChanges`，快照 `extensions.packetSoundLevels`；播完就移除。
2. **登入／選角音樂**
   - `ClientSoundScene.ObserveFront(state, gain, muted, now)`：依 `Scene(state)` 呼叫 `ObserveScene`；登入音樂要在進入世界前就出聲，所以這裡也推進 `ProductSound.Pump`（未 Arm 時不做事）。InWorld 不處理（交給 `Observe(WorldSession)`）。
   - `ClientLogin.cs`：ClientForm 主計時器在 Login／None 狀態呼叫（Offline、斷線、拒絕等＝登入畫面，槽 0；EnteringWorld／SwitchingCharacter＝停）。#638 已在 2026-11-09 合併，所以登入畫面這一處本張就一起做了。
   - `CharacterManagementUi.cs`：只加選角畫面（CharacterList／ManagingCharacters，槽 8 循環）的呼叫。
3. **頭目曲（FU4§6，Q3-8 限制已取消）**，`BackgroundMusic.cs`、`ClientSoundScene.cs`、`SoundPackets.cs`
   - `BossAppeared(sprite)`（sub_A50F20 → 8F3990 模式 0）：玩家所在的頭目區域播 `SPAWN`（單次），每筆區域只觸發一次。雙頭目任一隻出現就播，而且全域只播一次。
   - `BossDied(sprite)`（S158 動作 8 → 模式 2）：播 `DEATH`；雙頭目要兩隻都死才播。
   - 選曲（8F31F0／8F15B0）：`SPAWN` 播完時，如果頭目還在場而且沒死（`MusicContext.LiveSprites`，由 `Observe(WorldSession)` 從物件表篩出）就播 `BATTLE`，播完再選到 `BATTLE`；頭目離開或死了就回一般曲。`SPAWN`／`BATTLE`／`DEATH` 播放中因頭目出現或消失而換鍵時，會讓它播完、不淡出。
   - 封包掛點：`[WorldReceive(87, Observe=true)]`（物件出現：不是自己，而且動作 ≠ 8）、`[WorldReceive(158, Observe=true)]`（動作 8，gfx 從物件表取）。都走 M7-A 觀察器，沒改 `WorldSession.cs`／`WorldObjects.cs`。
   - 新增 `MusicKind.BossSpawn`／`BossDeath`（加在 enum 最後）；`RegisterBoss` 保留成「只登記」。`LiveSprites=null`（沒有物件資訊）時行為和舊版一樣，既有 `background-music` 檢查不受影響。
4. **測試**：新檔 `engine/source/SoundFollowupTests.cs`，`[WorldModuleTest("sound-followup")]`，涵蓋距離／ext 表、S22 每次更新重算與快照、登入／選角／進入世界／靜音、頭目 SPAWN→BATTLE→DEATH、雙頭目、BATTLE −1、離開視野，以及 S87／S158 觀察器（含截斷封包）。

## 怎麼驗證

- 已驗證：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`：product／launcher 都建置成功，結果 `PASS dev`（暫時 lock，沒有寫入 `rebuild-lock.json`）。
- 已驗證：`engine/tools/check-no-game-assets.py` 通過（errors 0）。
- 待驗證（交給 CI）：依本機規定沒有執行 `--module-tests` 和 DemoChecks；`sound-followup` 與既有測試是否全綠，以 PR 的 CI 結果為準。

## 未驗證／限制

- **S22 的距離重算目前算出來一定是 (100, 64)**：S22 封包只有 `"ch"`（模式、id），沒有發聲體欄位，原版走全域的 `A05980`／`A08020`，不掛在角色發聲體上（SD§3）。所以每次更新都會重算，但值不會變。這是照原版做的結果（推測），不是沒做完。
- **播放中改音量沒有真的送到正在播的聲音**：戰鬥／S22 走的是 `IAudioPlayback`（`engine/audio`）。這個介面沒有替正在播的聲音改音量（SetVolume）的入口，要接就得改 `engine/audio` 和 `CombatSfx.cs`，不在本張可改的範圍。`ext` 也一樣，只算到 (vol, ext)，還沒交給 Miles 的 extra_volume 對應層。
- 物件出現（S87）只看封包裡的動作 ≠ 8。`sub_EFE890()` 和音樂開關 `sub_F7D5C0(3,0)` 沒有展開（FU4§9），這裡把它們當成「音樂選項開而且沒靜音」。
- 推測：重新出現的雙頭目會清掉自己的死亡旗標。`+44`「已觸發」只在 `ClearBosses` 或重新 Configure 時清掉，和原版一樣是一次性的。
- 推測：打怪觸發的戰鬥音樂（`Attack`，8F3B90）照舊會立即蓋掉 SPAWN／DEATH，原版有沒有擋沒有查。
- 登入畫面音樂會讓 `ProductSound` 在登入畫面就建立串流層（以前是進入世界才建）。離線預覽（Offline 狀態）會被當成登入畫面，播登入音樂。
- 沒做真機聽音：登入／選角／頭目曲實際有沒有出聲，要真機驗收。

## 產出

- `engine/source/SoundPackets.cs`、`ClientSoundScene.cs`、`BackgroundMusic.cs`、`ClientLogin.cs`、`CharacterManagementUi.cs`
- 新檔 `engine/source/SoundFollowupTests.cs`
- 本報告 `docs/reports/issue-673.md`
- 分支 `task/673`

## 給下一棒的提示

- 要讓播放中的戰鬥音效跟著距離改音量：在 `IAudioPlayback` 加一個替正在播的聲音改音量的方法（例如 `SetVoiceScaleAsync(owner, scale)`），讓 `CombatSfxController.Pump` 每次用 `SoundAttenuation.Level(Offset(actor))` 重算並送出；`EmitterLevel.ExtraVolume` 可以拿來當 extra_volume。
- `docs/protocol/music.md` 的「登入畫面的定期呼叫未掛接」、「頭目 sprite 登記沒有封包來源」兩條，以及 gap-matrix 第 5 節背景音樂列、第 1 節 op 91 列，可以在下一輪盤點時改成「已接（#673）」。
- 真機驗收（dot／Codex）：登入畫面出聲（槽 0）→ 選角換曲（槽 8 循環）→ 進入世界換成區域曲；到有 SPRITE／SPAWN 的頭目區，等頭目出現時聽 SPAWN，頭目死亡時聽 DEATH。
