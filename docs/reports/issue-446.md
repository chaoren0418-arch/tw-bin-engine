# Issue #446 報告：T172 背景音樂、戰鬥音樂、登入音樂與地圖環境音

分支：`task/446`。階段：code，第 1 次嘗試。2026-11-08。

## 做了什麼

依 `docs/reference/sound.md` §4、§6（T163／T171）實作以下內容，模組說明見 [`docs/protocol/music.md`](../protocol/music.md)：

- **`SoundDefinitionSource`**
  - 執行期以唯讀方式開啟使用者原客戶端的 `Text.idx`／`Text.pak`。
  - 用 DES-ECB 取出 `music.def` 與 `envsound.def`，不寫檔，也不封裝解碼內容。
- **`MusicDefinition`**
  - 系統槽依出現順序寫入。
  - 區域記錄的 token parser 保留原檔的各種陷阱：空白 INTERVAL 是 -1、單值欄位取最後一個值、`LOC` 殘留的座標、註解 token。
  - 區域比對採包含右下邊界的矩形，依原順序取第一個符合的記錄。
  - 地圖編號 > 15000 時播 music23。
- **`EnvSoundDefinition`**
  - 解析群組、候選、`LOC`／`TIME` 版本提交、`MAIN_SOUND`（取最後一個值）、`ONE_SOUND` 群組引用。
  - 區域是半開矩形，`x1==x2` 代表整張地圖。
- **`BackgroundMusic`**
  - 登入畫面播系統槽 0，也就是 music82。
  - 選角畫面循環播系統槽 8。
  - 區域曲單次播放，依「開始時間＋INTERVAL」換曲，多首候選時不連續重複。
  - 戰鬥時立即切到槽 9／10；15 秒沒有戰鬥後，以 0.8 秒淡出（每 50ms −8）切回區域曲。
  - 另含頭目 `BATTLE` 曲、保護槽、停用即停止。
- **`MapEnvSound`**
  - 主環境音循環播放，以 40ms 一步做 1 秒淡入淡出。
  - ONE_SOUND 每 999ms 抽一個群組；群組冷卻、不連續重複同一個索引、只有成功播放才更新冷卻，並套用 200ms 全域 id 冷卻。
  - 依遊戲時間切換晝夜。
- **`ClientSoundScene`**：產品掛載點。在 `MapEffects.ComposeMapEffects` 與 `Combat.ReceiveCombat` 各加一行。沒有設定後端時是 no-op。
- **測試**：`engine/_integration/dev/background-music-tests/`，接上 `demo-tests` 的 `background-music` 群組，也接進 `map-continuity`（CI）與預設群組。
- **dev lock**：已用 `rebuild.py --update-lock` 重產。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` 後 `rebuild.py --build` | PASS（product、launcher 建置退出 0） |
| `DemoChecks … background-music` | PASS 434；其中本模組 122 項，含 6 項本機實際客戶端唯讀核對 |
| `DemoChecks … map-continuity`（CI 群組） | PASS 33060 |
| `engine/tools/check-no-game-assets.py` | pass，errors 0 |
| CI | 見 PR |

本機實際客戶端核對（唯讀，只在本機執行，CI 會略過）：

- 系統槽與 T171 的表一致：0→82、8→5、9→6、10→7、18→120。
- 區域選曲：說話之島村莊 30、說話之島整張地圖 12、地監 63、妖精村 13、新古魯丁地監候選 64／65／66。
- `envsound.def`：98 個群組；map 68 日／夜主音是 2000／2001；map 2005 主音是 2333。

測試用的定義檔是合成的，系統段的名稱與數值都是自編的，沒有複製原檔內容。

另外，本 worktree 原本沒有 `engine/vendor`。比照 #88／#91，複製了唯一的離線套件 `microsoft.web.webview2…nupkg`；該目錄已在 gitignore 中，沒有加入 git。`scratch/issue-446/decoded/` 是 T171 腳本的解碼輸出，只供閱讀格式，沒有加入 git。

## 沒驗證什麼

- **產品不會發聲。** 產品沒有串流或循環播放後端，`ClientSoundScene.Configure` 沒有被呼叫。原因是 `P24.Audio` 只播 SHA pin 過的取樣，而 music／環境音檔沒有 pin。真機上聽不到這些音樂與環境音，本任務沒有宣稱已經能播放。
- **沒有任何真機或聲音驗證。** 沒有啟動遊戲、伺服器或 DB，也沒有播放任何聲音。
- **推測項目未查證。** 換區切歌方式、主音交叉淡化、以 PC 旗標近似「對象類別 5」，都是推測，見 `docs/protocol/music.md`。
- **以下沒有實作：**
  - 頭目 sprite 的封包 226 來源。
  - `SPAWN`／`DEATH` 事件曲、封包 108 子類。
  - MIDI 後備、場景音、語音。
  - 第三欄聲像。
  - 登入畫面的計時器掛接。
- **缺檔的實際表現。** 部分引用的檔案在本機 `Sound/` 不存在，例如 `music166.mp3`、`2333.*`。控制器遇到缺檔時每秒最多重試一次，但實際表現未驗證。

## 產出路徑

- `engine/source/{SoundDefinitionSource,MusicDefinition,EnvSoundDefinition,BackgroundMusic,MapEnvSound,ClientSoundScene}.cs`
- `engine/source/Combat.cs`、`engine/source/MapEffects.cs`：各加一行掛接
- `engine/_integration/dev/background-music-tests/{BackgroundMusicChecks.cs,README.md}`
- `engine/_integration/dev/demo-tests/{DemoChecks.csproj,Program.cs}`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/music.md`、本報告

## 給下一棒的提示

1. **先決定 pin 政策。** 需要一張「串流／循環播放後端」任務，同時服務 T172 的 `IMusicStreamAudio`／`IEnvSoundAudio` 與 T166 的 `IAmbientLoopAudio`。開工前要先裁定：是否允許在執行期從原客戶端 `sound\` 讀沒有 pin 的 `music*.mp3`／`N.mp3`／`N.wav`。這屬於政策決定，建議交給 Claude 或使用者。
2. **接上後端。** 後端完成後，在 ClientForm 啟動時呼叫 `ClientSoundScene.Configure(clientRoot, music, env, SoundVolumes)`，並在登入畫面的計時器呼叫 `ObserveScene(ClientSoundScene.Scene(login.State), gain, muted, now)`。
3. **補頭目登記。** 封包 226（`92CB70` → `8F3990`）的格式要先查證，再呼叫 `BackgroundMusic.RegisterBoss`。
4. **真機驗收集中在 M6／M7 smoke。** 要聽：登入曲、說話之島村莊／野外換曲、戰鬥曲切換與 15 秒後淡回、map 68 的晝夜環境音。
