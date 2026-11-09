# Issue #613 T252：R2-B 聲音接上產品

依據：`docs/reference/gap-matrix.md` 11.5 節 R2-B；`docs/reference/sound.md`（SD#4–#14）。分支 `task/613`。

## 做了什麼

- **串流播放層（`engine/audio/AudioStreamPlayer.cs`，新檔）**：`AudioStreamPlayer` 實作新介面 `IAudioStream`。每個 voice 用一個 DirectShow graph，接法跟戰鬥音效一樣（DSoundRender，`RenderEx` 只接到已有的 renderer），不碰系統音量或工作階段音量。
  - UI 執行緒只做兩件事：檢查檔案在不在、把命令排進佇列。開檔、調音量、每 250 ms 輪詢都在同一條 MTA 執行緒上跑。
  - 循環音播完後用 `IMediaPosition.put_CurrentPosition(0)` 倒回開頭再播，對應 AIL 播放次數 0。
  - 單次播放的音播完會自動關閉 graph，`Finished` 回 true。
  - 只接受 `sound\` 底下的單一檔名（只允許英數字加 `.mp3`／`.wav`），帶路徑或會跳出 `sound\` 的名稱一律拒絕。最多同時 48 個 voice，滿了就拒播，不搶已在播的。
  - 原生動作抽成 `IStreamVoiceDriver`，測試可以換成替身；`ForTests` 模式下命令同步執行。
- **產品後端（`engine/source/ProductSoundBackends.cs`，新檔）**：
  - `ProductMusicAudio : IMusicStreamAudio`：播 `sound\musicN.mp3`（管線 B）。
  - `ProductEnvSoundAudio : IEnvSoundAudio`：先找 `sound\N.mp3`，沒有再找 `sound\N.wav`；主環境音循環，ONE_SOUND 單次。
  - `ProductAmbientLoopAudio : IAmbientLoopAudio`（`^`）：最終音量＝距離音量/100 × EnvSoundVolume × 主音量（A06D60）。距離音量在播放中改變時（A091C0），以及類別音量或主音量改變時，都直接改正在播的循環的音量，不重播。
  - `ProductSound`：負責「進入世界時安裝」。
- **掛接**（都在「可修改的檔案」範圍內，沒改 `Program.cs`／`WorldSession.cs`）：
  - `SoundRules.cs`：`ClientForm` 建構時（欄位初始化）呼叫 `ProductSound.Arm()`；`ClientForm.SoundVolumes` 改成指向 `ClientSoundScene.Volumes`，所以戰鬥音效、S22、背景音樂、環境音共用同一份 4 類音量。
  - `ClientSoundScene.cs`：`Observe` 開頭呼叫 `ProductSound.Pump(ready)`。第一次在世界中組畫面時做三件事：建立串流層、設定 `MapAmbientSound.Backend`、在背景讀 `Text.idx`／`Text.pak` 的 music.def／envsound.def。讀完的下一幀呼叫 `ClientSoundScene.Configure`。新增 `ClientSoundScene.Volumes`。
  - `MapAmbientSound.cs`：每幀先 `SetScale(EnvSoundVolume × 主音量)`；靜音或不可繪製時是 0，已在播的循環降成無聲但不停止。`Audible` 加上 A06020 的條件：音效、環境、語音三類都是 0 時不啟動新的。
  - `AudioApiTransport.cs`：`IAmbientLoopAudio` 加預設方法 `SetScale`，所以舊的替身不用改。
- 新增 `[WorldSnapshot("productSound")]`，內容是狀態、根目錄、各類 live 數、串流統計，供真機驗收時看。
- 開關：`TW_CLIENT_ROOT`（預設 `D:\lineage開發\0-客戶端`，跟戰鬥音效一樣）；`TW_PRODUCT_SOUND=0` 可以停用。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock`：通過。`rebuild.py --build`：PASS dev（2026-11-09 再次同步 origin/main 到 `1523e74e`，lock 衝突取 main 版後重產），productDll `2aab04b1…1af5`、audioDll `09898a58…7e6a`。唯讀核驗：dev-inputs-verified。
- `--module-tests`：同步 main 後 38 組全部通過，共 1778 項檢查。本組新增 3 組：
  - `product-sound-stream`（15 項）：缺檔、拒絕路徑、mp3→wav 後備、dB 換算、播放中改音量、循環倒帶、單次播完、Stop、錯誤、開檔失敗、容量上限、Dispose。
  - `product-sound-adapters`（17 項）：三個轉接器的檔名、循環旗標、音量傳遞；`^` 的距離、類別、靜音音量。
  - `product-sound-install`（8 項）：沒 Arm 時不做任何事；Arm 了但還沒進世界時不建立；進世界後才建立並設好 Backend；定義檔讀完才 Configure；只安裝一次；Disarm 會還原。
- 既有檢查都通過：DemoChecks `background-music`（434）、`map-ambient`（372）、`combat-admission`（1750）；SoundRulesChecks 130；SoundPacketChecks 148；CombatSfx selection 46。
- `check-no-game-assets.py`：pass（3942 個檔、0 錯）。
- DemoChecks `map-continuity` 在本機失敗於 `notice S245 unknown message stays labeled`。原因是本機有客戶端 `string-c.tbl`（#546），訊息 999 會被認出來；CI 沒有客戶端，所以不受影響。這跟本任務無關，本任務沒碰相關檔案。

## 未驗證／沒做

- **沒有真機驗收**：沒啟動遊戲，沒聽到實際聲音。DirectShow 的循環倒帶（`IMediaPosition`）、MP3 解碼、多個 graph 同時播放，都只有替身測試，**待驗證**。
- 沒做：
  - 登入／選角音樂（SD#12）：`ClientSoundScene.ObserveScene` 仍沒有呼叫點，接上要改登入流程的檔案，不在本組範圍。
  - MIDI 後備（管線 C）：缺檔時每秒重試。
  - 場景音與語音（SD#14）。
  - 頭目曲 226。
  - Miles 的 `ext`／聲像。
- S22 模式 1（op 91 環境音循環）的「播放中隨距離更新音量」要改 `SoundPackets.cs`，不在本組可改的檔案內。本組做的是 `^` 那一條：MapAmbientSound 每次進幀算出 A091C0 距離音量後，現在真的會改到正在播的 voice。
- 4 類音量目前沒有 UI 可以調，預設都是 1，只有主音量／靜音滑桿有效。

## 產出路徑

- `engine/audio/AudioStreamPlayer.cs`
- `engine/source/ProductSoundBackends.cs`、`engine/source/ProductSoundTests.cs`
- `engine/source/ClientSoundScene.cs`、`MapAmbientSound.cs`、`AudioApiTransport.cs`、`SoundRules.cs`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告

## 給下一棒的提示

- 真機 smoke（dot／P24Lab）要確認的項目：
  - 進入世界約 1 秒內，`Snapshot().extensions.productSound.status` 變成「已接上（…音樂有、環境音有、^ 有）」。
  - 在有 music.def 區域的地圖聽得到背景音樂；15 秒沒戰鬥後淡回區域音樂。
  - 靠近有 `^` 的地圖特效時音量變大、遠離時變小，靜音時無聲。
  - `stream.failed` 沒有持續增加。
- 如果 DirectShow 倒帶不可靠，循環音會被記成「循環重播失敗」，`failed` 會增加；可以改成在 `Status` 回 ended 時 Stop→Run 再試。
- 登入音樂要在 `LoginConnection`／`ClientLogin` 狀態改變時呼叫 `ClientSoundScene.ObserveScene`，並先 `ProductSound.Pump(true)`，或另外拆一個 Pump 給登入用。
- worktree 為了建置與 DemoChecks，建了 `engine/assets`、`engine/vendor` 兩個唯讀 junction，指向主專案的同名目錄，已在提交後用 `rmdir`（不加 /s）拆掉。
