# T172 離線檢查：背景音樂、戰鬥音樂、登入音樂與 `envsound.def` 環境音

`BackgroundMusicChecks.cs` 經 `demo-tests/DemoChecks.csproj` 編譯，直接引用本次產品 DLL。測試不啟動產品入口、視窗、網路、伺服器或 DB，也不播放聲音：後端改用記錄器，實作 `IMusicStreamAudio`／`IEnvSoundAudio`。

先依專案入口更新 dev lock、建置產品，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/background-music.json background-music
```

`map-continuity` 群組（CI 會跑）和預設群組也會呼叫本模組。

本模組沒有 C／S 封包。輸入格式是兩份定義檔的文字，以及 Text.idx／Text.pak 容器，測試全部用合成資料：

- **`music.def`**：涵蓋以下情況。
  - 系統段依出現順序寫入槽；`;` token 和沒有 `=` 的行會略過；21 以後的槽維持預設 i→i。
  - CRLF。
  - 空白 `INTERVAL` 維持 -1。
  - `BATTLE` 多值時取最後一個。
  - `\` 串接位置、跨行串接、結尾多一個 `\`。
  - `map x y` 留下前一組的 x2／y2。
  - 註解內的數字。
  - `atoi` 前綴解析。
  - 包含右下邊界的矩形。
  - 同一張地圖先比對特定區域，最後才用整張地圖的設定。
  - 地圖編號 > 15000。
- **`envsound.def`**：涵蓋以下情況。
  - 群組預設秒數。
  - `id:秒:聲像`。
  - 用前綴比對的結尾標記。
  - 分號起頭的行。
  - DAY／NIGHT 版本的提交方式。
  - `MAIN_SOUND` 取最後一個值。
  - `ONE_SOUND` 名稱區分大小寫，未定義的名稱略過。
  - `WEATHER`／`ONESOUND` 沒有對應分支。
  - 只有四個整數的位置段。
  - 半開矩形，以及 `x1==x2` 代表整張地圖。
- **Text 容器**：涵蓋以下情況。
  - DES-ECB 只轉換完整區塊，不足 8 bytes 的尾端原樣保留。
  - 名稱剛好 20 bytes、沒有 NUL。
  - 名稱比對不分大小寫。
  - 重複的目標、範圍超出 pak、不是 plain28 格式、索引截斷、檔案不存在。
- **背景音樂控制器**：涵蓋以下情況。
  - 登入曲：系統槽 0，每秒檢查是否播完再重播。
  - 選角曲：系統槽 8，循環播放。
  - 區域選曲，單次播放。
  - 依「開始時間＋INTERVAL」換曲；`-1` 表示播完立即換。
  - 多首候選不連續重複。
  - 戰鬥曲：一般用槽 9，對 PC 用槽 10；切換立即生效；同一首正在播時不重開。
  - 15 秒沒有戰鬥後，以每 50ms −8 淡出 0.8 秒，再以全音量播區域曲。
  - 戰鬥中曲子播完就保持安靜。
  - 戰鬥音樂選項關閉時的行為。
  - 換區時淡出。
  - 地圖編號 > 15000 時播 music23。
  - 頭目 sprite 與雙頭目的 `BATTLE` 曲。
  - 停用與重新啟用。
  - 保護槽的曲目不會被打斷。
  - 缺檔時每秒最多重試一次。
  - 音量類別。
- **環境音控制器**：涵蓋以下情況。
  - 主環境音每 40ms 一步，1 秒淡入。
  - 換主音時交叉淡化。
  - 離開區域時淡出。
  - 跨區時主音 id 相同就不重開。
  - ONE_SOUND 約每 999ms 抽一次。
  - 群組冷卻時直接返回，不改抽其他群組。
  - 不連續選到同一個候選索引。
  - 播放失敗時不更新冷卻。
  - 每個 id 的 200ms 全域冷卻。
  - 停用時立即停止。
  - 主音缺檔時重試。
- **產品掛載點**：沒有後端時是 no-op；登入狀態對應到音樂情境。

`BackgroundMusicRealClientChecks` 只在本機有 `D:\lineage開發\0-客戶端\Text.idx` 時執行，以唯讀方式解析使用者的原客戶端，核對 T171 的已知值。CI 沒有遊戲素材，會略過這部分，不計入檢查數。
