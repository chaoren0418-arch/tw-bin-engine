# S22／S55／S106 封包與生命週期（T105／#192）

目前狀態：模組、renderer、`WorldSession.ReceiveCore` 與 `ClientCombatSfx` 均已接通，已經由正式接收入口與實際 Pump 方法做離線驗證；尚未做原生聽音／畫面驗收。兩處最小掛接依 [#192 主控裁定](https://github.com/chaoren0418-arch/tw-bin-engine/issues/192#issuecomment-6697166058) 實作，詳見 [任務報告](../reports/issue-192.md)。

## 已核對的格式

依 [#132 封包表](../reference/server-protocol.md) 與 [規則 R31](../reference/server-rules.md)。以下長度包含 opcode 及補零，不含 transport header；H／D 為 little-endian unsigned。

| 封包 | 語義欄位 | 語義／補齊長度 |
|---|---|---|
| S22 | C22、C(repeat)、H(soundId) | 4／8 |
| S55 一般 | C55、D(objid)、H(gfxid) | 7／8 |
| S55 帶時間 | C55、D(objid)、H(gfxid)、H(time) | 9／16 |
| S106 | C106、H(x)、H(y)、H(gfxid) | 7／8 |

S55 依完整 payload 長度區分 overload；16-byte 的 `time=0` 仍是合法變體。只接受對應長度和全零 padding，不放寬其他 wrapper。`S_EffectLocation(opid, loc)` 不在此猜測額外 opcode。

唯讀再次核對的來源（沒有複製 Java 本文）：

- `1-伺服器/src/com/lineage/server/serverpackets/S_Sound.java`：SHA-256 `b7cae793e3c64f562fc4655ee6dba153f30ce55ae86ce072078bd73f3e4bd06f`。
- `1-伺服器/src/com/lineage/server/serverpackets/S_SkillSound.java`：SHA-256 `d59df752b4bacbd0158d9962afed424cc9c876d348d1154f633ac0e38b986ccb`。
- `S_EffectLocation.java` 的欄位／SHA 依 #132 索引：`0816db3e29cc8b89da3d45e6114736c1068e61effff8ff8642af37945184d661`。

## 實作政策與資源邊界

- S22 只把 `soundId` 派給既有 `CombatSfxController`／`AudioPlayer`。沿用主音量、靜音、音訊 owner、延遲 registration fence 與換圖退役。每個收包建立獨立實例，重複 pump 不重播；兩個相同封包仍是兩個伺服器事件。
- **去重產品政策、待 M6 smoke**：S22 延遲 100 ms 開始排程，等待相反的收包順序。若同一 WorldEpoch 內的動作／sprite 幀事件只有一個聲音候選，且 soundId 相同、幀排定時間與 S22 收包時間相差不超過 100 ms，保留原幀事件，略過該 S22 的第一次播放；後續 repeat 不變。一個幀事件最多配對一個 S22，決定後不因重複 pump 改變；不合併獨立 S22，不修改原戰鬥排程。保留最近 200 ms、最多 1024 筆幀事件，換圖清空。
- S22 沒有角色或事件編號，上述時間窗是推測，無法證明因果關係：同時發生的獨立同 ID 聲音可能被配對，超過時間窗或多候選幀事件則不配對，可能仍重複。這項限制須在 smoke 觀測，不宣稱跨封包精確去重；gfxId 永不拿來比對 soundId。
- **推測、待 M6 smoke**：repeat 表示額外次數，因此 0 播一次、1 播兩次；以已核定 PCM WAV 的 data bytes／byte rate 排定間隔，不同次播放不在收包時一次全部派送。伺服器序列化器只註記「重複」，無法證明舊客戶端的精確語義。
- 音訊核心既有白名單只有 33 個 WAV。使用它的 embedded pins 核對來源大小／SHA，再讀 RIFF chunk 算時長；實際播放時核心會再次核對。不增加白名單、不複製 WAV。未核定 ID、來源缺失、雜湊不符或無法取得 PCM 時長時記錄「未播放」，不播放其他 ID 代替。這不是所有 soundId 均可播放的宣稱。
- S55／S106 的 gfx 不作 soundId 派送。既有六個短版 S55（11722／221／2165／189／191／197）委派原接收方法，保留既有藥水通知、視覺和 sprite 音效；其他與 timed 變體進入新圖像佇列。因此同一包不會同時進入兩條顯示／播音路徑。
- 圖像讀既有 `objects/magic-<gfx>.json` 或 `maps/effects`，核對 integrity／manifest pins。`MagicSpells.AttachedEffect` 新增 appearance 擴充點，選已存在的 effect／heal／impact／breath 動作，沒有另造圖像。不在 catalog 的 gfx 明示缺素材，不宣稱已顯示。新圖像佇列尚未派送 sprite 幀事件音效；既有短版 S55 的音效維持原流程。
- **推測、待 M6 smoke**：非零 H(time) 以秒為單位，讓圖像循環至期限；省略或 0 為一輪原素材動畫。保留 raw time，不把它當 buff、HP 或 MP。來源序列化器沒有單位註解，必須用真機核對後再固定此政策。
- S106 固定在收包座標；新 S55 逐幀取可見目標座標，目標消失即退役。不為未出現的目標暫存後播。S149、S206（含同圖重送）、Reset 以 WorldEpoch 作廢舊場景；有界佇列各最多 256 筆。

## 離線驗證與真機交接

`engine/_integration/dev/sound-packets-tests/` 執行產品 DLL 的解析、狀態、逐幀與派送程式，以合成 payload／圖像及記憶體音訊檢查。Feed 使用 `WorldSession.Receive`，並在不執行 Form 建構子的宿主直接呼叫 `PumpCombatSfx`，核對掛接、單次播放與退役。這些素材只屬測試，不是遊戲功能證據。禁止連線的 EventListener 會在 socket 活動時使測試失敗。測試須建置到 `scratch/issue-192/tests/bin`，合成 catalog 寫入同任務的 `scratch/issue-192/assets`。

M6 smoke 請 dot 檢查：repeat 0／1 的實際次數；time 0／1／2 的單位與顯示時間；同一事件是否同時收到 S22 及 sprite 音效而重複出聲；S106 的位置和混色；timed S55 跟隨移動及目標離開；播放中換圖／斷線／靜音的退役。先選有核定資源的 ID；若部署使用其他 ID，交資源任務補核定來源。

## T173（#447）附加：S22 改為播放模式、音效共用規則層

依據 `docs/reference/sound.md`（T163／#431）第 2.3、2.6、2.7、3、4.1 節的「已驗證」項目，**取代**上面「repeat 表示額外次數」的推測政策：

- S22 格式不變（`C H`），第一個 byte 是**播放模式**：0 單次（`SoundVolume`，同 id 的單次實例還在播時不重播，對應 `A05EA0`）；1 環境音循環（`EnvSoundVolume`，以 WAV 時長逐輪重送，同 id 已在循環時不重複啟動）；2 停止該 id（移除該 id 的單次與循環，循環 owner 立即退役）；3..255 文件未列，忽略並寫 notice。
- 幀事件／S22 的 ±100 ms 首播去重只用於模式 0，不套用到循環。
- 新檔 `engine/source/SoundRules.cs` 是共用層：`SoundRules.DistanceVolume`（`100 − 14·(nx²+ny²)/20`，曼哈頓距離 > 14 夾成 (14,0)，負值為 0）、`SoundCooldown.Shared`（每 id 一份、跨角色跨管線的 200 ms）、`SoundVolumes`（4 類各自可調，0..1 正規化）、`SoundGate.Admit`（類別檢查 → 距離 → 冷卻 → 倍率）。
- `CombatSfxController` 設了 `Gate` 時每次派送都先過 `SoundGate`，再以 `IAudioPlayback.PlayAsync(id, owner, scale)` 送出；`AudioCore` 以「主音量 × voice 倍率」設定原生音量。產品 `ClientForm` 建立的戰鬥音效控制器已接上 `SharedSoundGate` 與 `WorldSession.SoundOffset`。
- **刻意不實作（文件標推測）**：Miles 第三參數 `ext`（`64 − 63·(nx+ny)/14`）、x 座標先 `>>1` 的雙倍精度。dx、dy 直接以格計。
- **與原版的已知差距**：原版每次更新都以 `A091C0` 重設播放中 sample 的音量；這裡只在派送當下固定倍率，播放中移動不改音量。原版音量 0 時仍會靜音播放；這裡不送出無聲 voice，但冷卻照樣佔用。
- 離線檢查：`engine/_integration/dev/sound-rules-tests/`（CI `map-continuity` job）。