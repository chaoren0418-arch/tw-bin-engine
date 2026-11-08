# 背景音樂與地圖環境音（T172／#446）

依據：[`docs/reference/sound.md`](../reference/sound.md) §4（T163／#431）與 §6（T171／#445）。本模組沒有 C／S 封包。輸入是使用者原客戶端 `Text.idx`／`Text.pak` 內的 `music.def` 與 `envsound.def`，執行期唯讀載入，不封裝解碼表或音檔。

## 檔案

| 檔案 | 內容 |
|---|---|
| `engine/source/SoundDefinitionSource.cs` | 從 Text 容器取出兩份定義檔（DES-ECB；只轉換完整 8-byte 區塊） |
| `engine/source/MusicDefinition.cs` | `music.def` 的 token parser 與區域選曲（`8F21A0`／`8F31F0`） |
| `engine/source/EnvSoundDefinition.cs` | `envsound.def` 的 parser 與區域選擇（`8287D0`／`825660`） |
| `engine/source/BackgroundMusic.cs` | 登入、選角、區域、戰鬥、頭目音樂的播放政策；後端介面 `IMusicStreamAudio` |
| `engine/source/MapEnvSound.cs` | 主環境音淡入淡出與 ONE_SOUND；後端介面 `IEnvSoundAudio` |
| `engine/source/ClientSoundScene.cs` | 產品掛載點（沒有設定後端時是 no-op） |
| 掛接 | `MapEffects.cs` 的 `ComposeMapEffects`、`Combat.cs` 的 `ReceiveCombat`，各加一行 |

## 已實作的規則（對照 sound.md）

**`music.def`**

- 系統段的 `=N` 依出現順序寫入槽 0..；其餘槽維持 i→i。
- `LOC` 的座標暫存在記錄之間保留，新的位置只重設 map 與 x1。
- 單值欄位遇到多個數字時取最後一個。
- 數值只認首字元是數字的 token。
- 只略過以分號開頭的 token。
- 區域比對採包含右下邊界的矩形，依原順序取第一個符合的記錄。
- 地圖編號 > 15000 時固定播 music23。
- `MUSIC N` 就是 musicN，不查系統槽。

**播放**

- 一般曲目單次播放，並依「開始時間＋1000×INTERVAL」換曲；INTERVAL 是 -1 時，播完立即換。
- 多首候選時，不連續選到同一個候選索引。
- 登入畫面播系統槽 0，單次播放，每秒檢查是否播完。
- 選角畫面播系統槽 8，循環播放。
- 自己參與攻擊時立即切到槽 9，對象是 PC 時切到槽 10；同一首正在播時不重開。
- 戰鬥音樂每秒檢查一次；超過 15 秒沒有戰鬥，就以每 50ms −8 的速度淡出，約 0.8 秒；音量到 0 後以全音量開區域曲，沒有淡入。
- 槽 12／13／15／16／17 的曲目正在播時，不會被打斷。
- 音樂選項關閉時立即停止。

**`envsound.def`**

- 分號起頭的行整行略過。
- 群組以 `[NAME:秒]` 開頭；結尾標記只按名稱前綴比對。
- 候選格式為 `id[:秒[:第三值]]`。
- 遇到 `LOC`／`TIME` 時，提交前一個版本。
- `MAIN_SOUND` 有多個數字時取最後一個。
- `ONE_SOUND` 名稱區分大小寫，找不到的名稱略過。
- `WEATHER`／`ONESOUND` 沒有對應分支。
- 區域是半開矩形；`x1==x2` 代表整張地圖。
- 晝夜判斷用 `AmbienceFrame.Night`。

**環境音播放**

- 主環境音循環播放；換主音時以 40ms 一步做 1 秒淡出、淡入。
- ONE_SOUND 每 999ms 抽一個群組。群組在冷卻中就直接返回，不改抽其他群組。
- 候選不連續選到同一個索引。
- 只有播放成功，才更新群組冷卻（候選秒數）與前次索引。
- 每個 id 套用 200ms 全域冷卻（`SoundCooldown.Shared`）。

## 相容性選擇與推測（明示）

- **CR 當成分隔字元。** 原 token reader 只認空白、tab、LF。本機檔案是 CRLF，所以 CR 也當成分隔；否則會出現 `12\r` 這類 token。對 `atoi` 的結果沒有影響。
- **換區切歌（推測）。** 換到另一筆區域記錄，或頭目出現／離開時，目前曲目若不在新候選中，就以 0.8 秒淡出後切換。原版換區的切換方式未查證。
- **主音交叉淡化（推測）。** 換主音時，舊主音淡出與新主音淡入同時進行。
- **對象類別 5（推測）。** 以 CHARPACK 的 PC 旗標（`Flags & 4`）近似。
- **另一個時間戳未納入。** 離開戰鬥的條件中，另一個時間戳 `dword_153CE6C` 的意義未查證，沒有納入判斷。
- **重試節流（引擎政策）。** 缺檔或後端拒絕時，每秒最多重試一次。這是引擎的政策，不是原版行為。
- **Text 容器名稱比對。** Text 容器的名稱比對不分大小寫；原版查找的大小寫規則未核定。

## 未做

- **沒有串流後端。** 產品沒有串流或循環播放後端。`P24.Audio` 只播 SHA pin 過的取樣（issue-47 政策）；`music*.mp3` 與環境音檔沒有 pin，所以沒有呼叫 `ClientSoundScene.Configure`，產品執行時不會發聲。T166 的 `IAmbientLoopAudio` 也是同樣狀況。是否放寬 pin 政策，改成在執行期從原客戶端 `sound\` 讀未 pin 的檔案，需要另外決定。
- **登入畫面的定期呼叫未掛接。** 規則已經有了（`ObserveScene`），但 ClientForm 的計時器掛點要等後端存在才有意義。
- **頭目 sprite 登記沒有封包來源。** 原版由封包 226 的 `92CB70` 呼叫 `8F3990`。控制器已有 `RegisterBoss`，但沒有掛封包。
- **未實作的事件與情境。** `SPAWN`／`DEATH` 事件曲、封包 108 子類、`Planarization`／`LoginMusic`（本檔沒有）、MIDI 後備、場景音、語音、`equipsound.def`、第三欄聲像、`8257D0` 的略過條件，都沒有實作。
