# T045：攻擊／技能音效與一般地面物品設計

日期：2026-11-05；Issue #42；來源基線：`b573c7be069789d1f4c6c8121e9338a51dc88530`。

## 結論與證據等級

**已驗證（靜態資料）**：原客戶端根目錄 `TW13081901.txt` 含動作／幀／事件資料；技能 1／3／4／8 對應效果中的事件數值分別為 **109／127／97／804**，與現有產品效果 JSON 相符。近戰有依外觀、武器、attack／damage 動作區分的事件集合，不能壓成一個通用音效。上述 WAV 都在原客戶端 `Sound/`，但不在產品 sound 目錄，也不在 AudioCore 的允許範圍。

**已驗證（程式）**：ObjectAppearance 讀取 sequence 時忽略 `events`；Ground 的判斷、renderer 與 Web 圖片路徑各有肉／箭限制。金幣的名稱代碼 `$4` 在現有 `names.json` 解析為「金幣」。原資料中 gfx 7 與 3247 都是金幣相關、type 9 定義。

**推測，禁止直接當成完成的契約**：`[數字` 是與同號音檔關聯的聲音事件候選；type 9 是泛用物品分類的候選依據。本輪沒有證實 `[` 多候選是隨機擇一、全部播放或其他分支，也沒有證實原生 S87 接收路徑如何使用 type 表。

**待驗證**：原生事件選擇／時機／重播規則、S87 → 原生分類表的呼叫鏈、金幣實際 S87 gfx 與完整 envelope、背包可疊加／可丟棄規則及伺服器命名。這是可拆工的部分設計，**不是全部原生語義已核定，也不是兩項產品功能已完成**。後續 code 任務受 resource 任務的驗收閘門限制，未知值保持未知。

## 1. 原始資料與身份

以下檔案皆從 `D:\lineage開發\0-客戶端` 唯讀取得，SHA-256 是本輪整檔重算；沒有複製 PAK、MAP 或 SPR。SHA 表中所有雜湊均指磁碟 bytes，不把換行正規化後的內容冒充原檔。

| 檔名 | bytes | SHA-256 |
|---|---|---|
| TW13081901.txt | 5990880 | `28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8` |
| LiTo.bin | 1941568 | `ce6ee68ada8fd37e6c096b1a27e46cb6888f1179f85defb5007ac60287360292` |
| Text.idx | 1049948 | `43a342ad3929b541096630165c04794192c48359ecbf116b538eaffc49741f6f` |
| Text.pak | 26267704 | `9516a372c43b269cf1af7adc0ac00abd4163e1235dcd846e34294849616a37ff` |
| Sprite.idx | 2585972 | `327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b` |
| Sprite.pak | 1904012586 | `4dd4e5348fbad2e69bae9cc388be23d7becebe8423ad9f79f393c1457a69ee62` |

### 1.1 動作定義解析

`TW13081901.txt` 是文字格式，開頭為 `30000 0 41211`；本輪不賦予這三個數值未證實的語義。資料含 CRCRLF；先保留原始 bytes／雜湊，再在記憶體把 `\r\r\n` 換成 `\r\n`。以行首 `#<十進位 gfx><空白>` 切段，到下一個段首為止。`#0 312=3225 prince` 等 header 的 gfx、sprite alias 與動作編號是不同欄位。

本輪採用的動作形狀為 `action.label(directional tokenCount,sid.frame:ticks[事件...)`，逐 token 解析 `sid.frame:ticks` 並**原樣保留尾綴**。例如 `[1537[1538` 不能變成兩次無條件播放；`!`、`<206`、`109.effect(...)` 也不是可以任意當成聲音的數值。此解析只涵蓋表列動作，不宣稱完整 grammar、繼承／alias、隨機 variant 已完成。

原始文字段首 File Offset（十六進位；不是 BIN RVA）：

| gfx | 段首 File Offset | 標籤／type |
|---|---|---|
| 0 | 0xf | #0 312=3225 prince；type=5 |
| 1 | 0xe69 | #1 312=3227 princess；type=5 |
| 7 | 0x1e04 | #7 1 adena；type=9 |
| 20 | 0x2002 | #20   1       potion blue；type=9 |
| 36 | 0x2ad7 | #36   1       meat；type=9 |
| 41 | 0x39aa | #41   1       potion green；type=9 |
| 43 | 0x3a04 | #43   1       potion orange；type=9 |
| 45 | 0x3a5a | #45   1       potion red；type=9 |
| 66 | 0x6d38 | #66   8       arrow；type=9 |
| 167 | 0xac9b | #167  4       energy bolt；未列 102.type |
| 219 | 0xcf19 | #219  1       take water；未列 102.type |
| 221 | 0xcfe0 | #221  1       shield；未列 102.type |
| 2165 | 0x5444a | #2165 1   holy weapon e；type=0 |
| 3247 | 0x954d2 | #3247 1 new adena；type=9 |
| 11722 | 0x46ab14 | #11722 1 renewal heal eff sc；未列 102.type |

已與 `engine/assets/objects/magic-{11722,221,167,2165,219}.json` 的方向 0 sequence 比較：幀鍵、ticks 與事件尾綴相符；這些 magic JSON 沒有 `entryHash`，不能據此宣稱整段原文一致。`objects/0.json` 亦保留 attack 的 `[1539` 與 damage 的 `[1537[1538`。

版本差異必須保留：原 TXT 的 gfx 0 正規化段 SHA 為 `cbf5b6ba6b2eb33f853d457f1973237390aee8ed8501bab2c53a3a546f31104e`，產品 `entryHash` 為 `c0e63870fee5e86ea3ac68ef66b05198672d7a70e0ed1f19bc4a3ad3f4b0c14c`。與 `work/engine-pickup-asset-source-r1/metadata/entry-0.txt` 的逐行比較只發現 action 50 前導空白不同（兩個空格／tab）；這不等於所有 gfx 或整個發行版相同。歷史 inner-list.spz 的整檔 SHA 不得套用到本輪 TXT。

### 1.2 技能與近戰事件數值

技能 ID → effect gfx 來自既有 `engine/assets/magic/catalog.json` 和 MagicSpells 的讀取契約；本輪只讀取這份既有資料，沒有查伺服器資料。效果 gfx 不是音效 ID。

|技能|名稱|effect gfx／動作|原文字事件位置|資料中可確認的聲音候選 ID|
|---|---|---|---|---|
|1|初級治癒術|11722／0.effect|token index 1：`0.0:3[109`|109|
|3|保護罩|221／0.fire|token index 1：`0.1:2[127`|127|
|4|光箭|167／0.fly|token index 1：`0.0:2[97`|97|
|8|神聖武器|2165／0.animation|token index 1：`0.0:4[804`|804|

index 由 0 起算，是**資料順序**，不是實際開始後必須等待一幀的保證。MagicMotion.PureFrame 現有實作會從 sequence index 1 開始；光箭的 renderer 另走 60ms projectile 路徑，不能照一張固定技能表在按鍵時立即播放。gfx 167 的 `109.effect(4 219)` 指向撞擊效果候選 219；219 的本輪文字與產品 sequence 都沒有 `[` 事件，不能把 219 當成音效號，也不能自行增加命中音。

近戰的具體靜態例子（gfx 0 王子，sprite alias 3225）：

|原動作|事件幀|候選 ID|可宣稱的範圍|
|---|---|---|---|
|1.attack，空手|`16.3:5[1539`|1539|攻擊動畫的事件；未核實與實際揮擊時刻的關係|
|5.attack onehandsword|`120.2:3[248[703[701`|248、703、701|保留順序的候選組，選擇規則待驗證|
|47.attack dagger|`152.3:4[1513[1517`|1513、1517|匕首候選組；不能把 weapon/action 46／47 混為一值|
|2.damage／6.damage onehandsword|`24.0:6[1537[1538`／`128.0:6[1537[1538`|1537、1538|gfx 0 受擊動畫事件，不是所有攻擊命中的統一音效|

gfx 1 的空手 attack 是 246，damage 為 1546／1547（本輪未核對這三個音檔）；這已足以否定全角色共用 1539／1537 的設計。斧／矛／杖／雙手劍等應由實際 adopted profile 再取證，不能用本表作全部武器白名單。目標受擊音應取**目標** appearance 的 damage 事件，不能拿攻擊者的 damage 聲音代替。

### 1.3 音檔可用性

下表全部為 `D:\lineage開發\0-客戶端\Sound\<ID>.wav`。以 RIFF/WAVE reader 唯讀驗格式：PCM、22050 Hz、16 bit；804 為雙聲道，其他為單聲道。同 ID `.mp3` 在該 Sound 根目錄不存在，`engine/assets/sound/<ID>.wav` 也都不存在。未呼叫播放／解碼裝置，因此「音檔存在」不等於可聽驗收。

| ID | bytes | 聲道 | SHA-256 |
|---|---|---|---|
| 97 | 47408 | 1 | `2e8661cc26238e422b3233509ba24ae11b3440d8c1d74666cb5c842e7de6f1a7` |
| 109 | 61046 | 1 | `664c8d8b353e2a03cbc337df7a7d36ce009d0213d33064e43779f4c76471b4d1` |
| 127 | 84854 | 1 | `6ca44e9ea605d14e6d582a3ca6071b0b0f0d1d395df2f24651fcdb1b2061d364` |
| 804 | 335476 | 2 | `a1ccf06439dd3fdf721016b85bb08563a07f16e69ce9ad8d18c2ac07e4d36117` |
| 1539 | 20134 | 1 | `4bdb0af502bff04b83598e1c865365031b960360fe3160cf8a8849b29de8ed4b` |
| 248 | 8356 | 1 | `d2696d07692ba610371d301e3027c2153c6075eb8446476ff4fbcd1f1e101bb5` |
| 703 | 13686 | 1 | `30bac3007049ab0c15a7ccf315445da0a0b9cb990f49a772af0f138b4f127f09` |
| 701 | 17592 | 1 | `6003c0c805fd9dc6b4988be7e31fdd9782f31543ca96955b0816a03698da39f2` |
| 1513 | 10932 | 1 | `ae4145e4c94b514e65b91fcfa4487f149fad01e6251e689c8cc276ce6fcafb3a` |
| 1517 | 13702 | 1 | `7ecf324c4485fc4c43afe531b0e85d9db85dffaaf8cedef9264534ec8e198945` |
| 1537 | 13278 | 1 | `1921c2d75c763d51a18efec28a9e59ae1799b9f1d91e3954e0a76656be64039c` |
| 1538 | 13278 | 1 | `b792497547fdd91fc057575ac37150aa8137cf0aca592f387008dbc6244c7bb6` |

AudioAssets.Load 目前只接受 3332／1340、固定四筆 MP3/WAV 宣告；AudioCore.Play 也對其他 ID 回傳 `IdNotAllowed`。所以即使外部 Sound 有正確檔案，現有 AudioCore **仍不能直接載入**。`engine/assets/audio-manifest.json` 僅包這兩個場景效果 ID；本輪未修改它或 junction。

## 2. 地面物品辨識

### 2.1 gfx、名稱與尚缺的連結

|物品／候選|原 TXT gfx 與 type|目前名稱依據|已驗證／限制|
|---|---|---|---|
|金幣|7 `adena`、3247 `new adena`；均 type 9|names[4]=金幣；RewardIntegration 以 DescriptionId=0、IconId=5、`$4` 前綴找金幣|兩種 metadata 定義存在；**尚無真實金幣 S87 證明採 7 或 3247**|
|肉|36 `meat`、type 9|names[23]=肉；GroundRules 對 `$23`／`$23 (N)` 有完整既有 envelope|既有產品支援；本輪未重做真機|
|箭|66 `arrow`、type 9；另有 109.effect(3 -1)|names[61]=箭；GroundRules 對 `$61`／`$61 (N)` 有完整既有 envelope|既有產品支援；IconId=7 不等於金幣地面 gfx 7|
|治癒藥水|45 `potion red`、type 9 為候選|names[26]=治癒藥水；Inventory 的 desc1342/icon80/use249|名稱與原英文 gfx 標籤的配對尚須伺服器模板證據|
|綠色藥水|41 `potion green`、type 9 為候選|names[234]=綠色藥水；Inventory 的 desc17/icon77/use246|缺實際地面 envelope 與可丟棄／堆疊契約|
|白色藥水|本輪未核定|names[238]=白色藥水；Inventory 的 desc130/icon81/use249|不可由顏色或附近序號猜 gfx|
|傳送回家的卷軸|本輪未核定|names[505]；Inventory 的 desc1338/icon474/use9|缺地面 gfx、堆疊與丟棄規則|

`$4`、`$4 (N)` 是下一棒應核對的**金幣原始名稱格式候選**；本輪沒有金幣丟地封包，不能宣稱 count=1、0、特殊名稱、祝福／裝備後綴都已確認。不要採用合成 InventoryLayoutTest 的欄位作原服證據，也不能把背包圖示 5 或 DescriptionId 0 當作地面 gfx。Name 的 `$數字` 由 ObjectAssets.Name 查 `names.json`，顯示用的中文與封包原字串須分開保存。

其他可疊加物品不能僅由當下 Count>1 判斷：單件可疊加物仍可能 Count=1；物品的可丟棄、堆疊上限、裝備／綁定限制，需要原服資料及封包契約。這些資料本輪沒有查詢，另拆 resource 任務。

### 2.2 原圖可取得範圍

Sprite.idx 為已核對的 plain28：開頭 little-endian uint32 count=92356，長度必須等於 `4+28*count`；每筆 `<I20sI>` 為 PAK offset、NUL 結尾名稱、payload size。先檢查 offset+size 不超 PAK 長度，再唯讀 seek/read，雜湊以下 bounded slice；沒有寫出 SPR。解析器可參照 work/engine-pickup-asset-source-r1/reference/archive_reader.py，不能把它的 extract 寫檔入口用在原客戶端。

| entry | IDX record／File Offset | PAK File Offset | bytes | payload SHA-256 |
|---|---|---|---|---|
| 36-0.spr | 48732／0x14d214 | 0x39e4eb23 | 244 | `1734424f17adb848324f8e6c2aba6ad4bafeb687f990aa59b62426d448899ee8` |
| 41-0.spr | 55893／0x17e150 | 0x39d213f9 | 176 | `296b8bf0c6dafd0f438da13823d3df29b074c603ccb9a69c3267631d8f8e7d9b` |
| 43-0.spr | 59171／0x1947d8 | 0x39d21299 | 176 | `93e5eebaa451d38b89fa272449bcb984b72336bbe5b0ee2b29035e344082d189` |
| 45-0.spr | 62818／0x1ad6bc | 0x39cb54c6 | 174 | `89ccae52b20c83ea26bdb9906f7c2c0bb1dde411bf3e59fd140fd39a01d69ca9` |
| 66-0.spr | 85892／0x24b274 | 0x39648333 | 1056 | `4795b0d2150d7d075d753efc378799b8d471e52d32cdce47eec77fa502bb758c` |
| 7-0.spr | 86493／0x24f430 | 0x395ce3ed | 307 | `65b754770b1915eb070e488ccb010ee2662741a09346f1d1be36fc3faadf2ce2` |

本輪另外定向搜尋主包與 Sprite00..15.idx：**沒有 `3247-0.spr` 這個精確 entry**。這只代表此次檔名查找未命中，不能證明所有可能 alias／容器都無圖，也不能因此推定金幣必定用 gfx 7。未進行新像素解碼、畫面比較或實機呈現。engine/assets/ground 目前只有 36／66 的 JSON/PNG；一般物品還需要合法來源的 render 資料，不是只改辨識函式就能顯示。

### 2.3 S87 的事實與缺口

現行 WorldObjects.cs 的 ReceiveObject 的 S87 envelope 帶有：X、Y、ObjectId、Gfx、Action、Direction、Light、Speed、WireExp、Lawful、Name、Title、Flags、ClanId、Clan、Owner、Rank、HpByte、Brave、Level、Unknown、Tail1、Tail2。沒有獨立的 item template ID／背包 IconId／DescriptionId。現有 GroundRules 將 WireExp 解讀為物品數量，這是特定 envelope 下的分支，不能對所有角色封包套用。

**原客戶端如何區分物品種類：本輪僅找到候選資料機制，尚未完成 native 證明。** TXT 中物品常有 `102.type(9)`、角色 gfx 0 有 `102.type(5)`；建議追查 S87 的 gfx lookup → metadata type 載入 → object factory／滑鼠拾取判斷。不能寫成「原生收到 S87 後一定用 type==9」。既有 work/engine-pickup-renderer-eligibility-source-r1/HANDOFF.md 也明列 text type5 尚未連到原生分類表的 bytes，不能跳過此缺口。

本輪磁碟 LiTo.bin 的 PE ImageBase=VA `0x00400000`，entry RVA=`0x0043B014`，對應 File Offset=`0x0009C014`；其 entry 所在 section 名為 `Themida `。這些只證明磁碟 PE 身份及對映，不代表已取得未包殼的原生分類／音訊函式。既有 work 文件中的 VA 不能直接當成本檔的 File Offset。本輪**不提供未核實的 S87 或音訊函式位址**，沒有啟動、附加或 dump 遊戲。

Text.idx 的 count/長度符合 28-byte 外形，但記錄名稱區不是可直接使用的 plain28 文字；本輪未核定解碼方式，沒有從 Text.pak 猜出技能或物品表。Sprite.pak 的 desc-k.tbl／SpellDesc-k.tbl 雖有資料，但不是已核定的 TW 中文字串與目前技能版本，未採作權威對照。

## 3. 建議實作與驗收閘門

### 3.1 音效

1. 先核定 `[`、`<`、`!` 的原生語義，以及多 ID 選擇、首次進幀、重複／終止／飛行／受擊行為。把每條 binding 的來源 SHA、gfx、action、sequence index、frame key、原事件字串、候選 ID、有依據的 dispatch policy 一起保存；語義未知時拒絕啟用該 binding。
2. 在 ObjectAppearance／動作進度層保留事件資料，以權威接受的 attack／effect 實例推進事件。用 session/world epoch、actor、instance、sequence index 做消費身份；重繪或讀取快照不觸發音效。略過幀、取消、死亡、換圖、斷線、變身與同時施法須有明確規則；不在送 C6/C229 或按鍵時假定成功。
3. 以受來源驗證的 catalog 取代 AudioAssets／AudioCore 的兩個固定 ID 閘門。採用原客戶端唯讀 Sound root、明確的 ID/格式/size/SHA pin 與路徑限制；manifest 放可寫來源區（例如 engine/source/Resources），不改唯讀 assets junction／封存。缺檔、雜湊不符、裝置／解碼失敗必須可觀測，不能悄悄以其他聲音代替。MP3→同 ID WAV fallback、owner generation、靜音、音量及 voice 清理沿用已核定行為。
4. 保留 Effect6016 路徑與診斷，新增 dispatch 來源／ID／實例與成功/抑制/失敗紀錄。`PlaySubmitted` 只代表原生命令接受，實際可聽另由 dot 驗收。

### 3.2 地面物品

建議分開「可辨識／可顯示的地面外觀」與「該背包物件可提出丟棄請求」，不要用一張名稱字串白名單代替兩者。

|資料層|最少欄位|來源與用途|
|---|---|---|
|source identity|版本／SHA、檔名、offset 或表列鍵|可重現與版本拒絕策略|
|appearance|gfx、sprite alias、原 type、ground eligibility、必要 frame keys／資產 pin|辨識／渲染；type 語義須先核定|
|inventory identity|DescriptionId、IconId、UseType、Name token／語法、Bless、Flags、Charges；原服 template ID 僅作離線 provenance|匹配背包，不把離線 template ID 假裝成線上有傳|
|item capability|stackable、drop allowed／bound／equipped 限制、數量上下界、名稱後綴規則|伺服器契約；未證實不放行|
|ground envelope|gfx、Action／Direction 等允許值、Name grammar、WireExp count 解讀及零值規則|辨別非物品／碰撞案例，不把 type9 當全部充分條件|
|runtime instance|ObjectId、X/Y、Count、原始 Name、epoch／selection revision|單次請求、過期選取、權威回包更新|

既有 WinForms `dropCount`／`pickupCount` 已是 NumericUpDown；應讓金幣走此數量輸入並與 Web 對齊，不另做無來源的專用金幣封包。輸入僅接受整數 `1..min(最新持有量,契約上限,int.MaxValue)`；拒絕零、負數、小數、溢位、超額、選取變更與送出前數量下降。整包 Request 現為 16 bytes，C25/C112、LE X/Y/ObjectId/Count，尾端保留既有 padding；只有原服契約支持時才變更。成功只由 S87／背包更新／移除等權威回包判定，不預扣金幣或以 S158/action15 當成財物結算。

GroundRules、GroundRendering、GroundWeb 的 36/66 判斷都要改成同一驗證 catalog，並接通真實物品圖及既有 hit-test／選取；缺少資產要呈現可診斷的未支援狀態，不用肉圖代替金幣。保留既有 900ms 點擊防重、3000ms 待確認、距離、交易／施法／死亡阻擋與 world/selection lease。已裝備、綁定、非物品同名、同 gfx 不同物件、未知 type、畸形名稱、超大數量需作負向案例。

## 4. 後續任務

機器可讀拆工見 `../plans/issue-42.json`：三個 resource（音效原生語義、地面原生分類、dot 原服靜態模板與封包契約），兩個 code（音效派送、地面物品及金幣數量），一個 dot 局部真機 test。code 只能使用前置任務已核定的範圍，資料未齊不得以猜值關閉任務。

dot 補測須含：06 不會移動且可穩定受擊的合法測試目標，記錄目標身份、距離、HP／死亡證據；07 技能 1/3/4/8 的效果與聲音；10 金幣丟 1／拾 1 及 N 數量、取消／超額阻擋，原背包與地面數量閉合；15 未靜音→靜音→取消靜音的 host 新事件與實際聽感分開記錄。沒有目標 HP 差或死亡，不能只用 C229/S30 當近戰傷害通過。不得連正式服務。

## 5. 重現方法與本輪限制

在 scratch 建立下列唯讀 Python 片段，執行 `py -3 -B -X utf8 scratch/issue-42/reproduce.py`。只輸出定向 metadata，不寫出資源。這是定向資料解析方法，不是原生 runtime parser 的替代證明。

```python
from pathlib import Path
import hashlib, re, struct
p = Path(r'D:\lineage開發\0-客戶端')
raw = (p / 'TW13081901.txt').read_bytes()
assert hashlib.sha256(raw).hexdigest() == '28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8'
normalized = raw.replace(b'\r\r\n', b'\r\n')
for gfx in [0, 7, 36, 41, 45, 66, 167, 219, 221, 2165, 3247, 11722]:
    pattern = rb'(?m)^#' + str(gfx).encode() + rb'\s'
    m = re.search(pattern, normalized)
    nxt = re.search(rb'(?m)^#\d+\s', normalized[m.end():])
    block = normalized[m.start():m.end()+nxt.start() if nxt else len(normalized)]
    print(gfx, 'raw File Offset', hex(re.search(pattern, raw).start()))
    for line in block.splitlines():
        if line.startswith(b'#') or b'[' in line or b'102.type' in line:
            print(line.decode('ascii'))
idx = (p / 'Sprite.idx').read_bytes()
count, = struct.unpack_from('<I', idx)
assert len(idx) == 4 + 28*count
with (p / 'Sprite.pak').open('rb') as pak:
    for i in range(count):
        off, name, size = struct.unpack_from('<I20sI', idx, 4+28*i)
        if name.split(b'\0')[0] != b'7-0.spr':
            continue
        assert off + size <= (p / 'Sprite.pak').stat().st_size
        pak.seek(off)
        payload = pak.read(size)
        assert len(payload) == size
        print('7-0.spr', hex(off), size, hashlib.sha256(payload).hexdigest())
```

音檔重算用 hashlib，WAVE 標頭用 Python wave.open 唯讀讀取；JSON 使用 UTF-8 解碼並按表列 gfx/action 對照。一次性腳本與解析數值留在 `scratch/issue-42/`，不作需版本管理的外部依賴；此文件保留必要值與重現方式。沒有執行任何產品 build、遊戲／伺服器／DB、音訊播放或既有原生程式。本輪只做靜態設計核對，不以測試 fixture 冒充原服證據。

## 附錄：本輪使用的產品與歷史資料 SHA-256

這些值是本輪磁碟身份；engine/assets 經既有唯讀 junction 讀取。歷史研究只作限制與資料一致性依據，沒有沿其路徑讀取本任務範圍外的 P24 歷史或封存來源。

| 路徑 | bytes | SHA-256 |
|---|---|---|
| engine/assets/objects/names.json | 361647 | `40b14f8d3d83249d18933f77610fa066efcc9f0c5a19792a7c6514365d96e01c` |
| engine/assets/objects/0.json | 29014453 | `27ab41e8a398eb180ce957d9215b2c9b5cf6bf5343b32f6c101757b8882efdf8` |
| engine/assets/objects/magic-11722.json | 1438386 | `2e96341e889355a555f9b098cdc0645796093ca6fab29306f491f063e3528aad` |
| engine/assets/objects/magic-221.json | 419076 | `549402cf765f7d9d3099befb30d33c20de336d0eeda7773a621583563d4644dd` |
| engine/assets/objects/magic-167.json | 1485999 | `c605c422e7a050f8afcc81cd4aefee539f7a7adb4f18933274be2da25dd1b04b` |
| engine/assets/objects/magic-2165.json | 744998 | `8d22d07fe16a23da6af1e24349083025cda163b11f1078e07f0a98e98c3f4a6b` |
| engine/assets/objects/magic-219.json | 1961439 | `bb045d9bde11340d06d6c3a969fb7621f2d8ebd55f5bec9157aef068dd3babc6` |
| engine/assets/magic/catalog.json | 1626 | `024576e3c96bedc33c63b889d77366f4204b99ceb40c78f3c743ff6e54a44ebf` |
| engine/assets/audio-manifest.json | 3346 | `87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe` |
| engine/source/Ground.cs | 9418 | `6df52d49787795c432309ed0b02deaacc2a582f4927f98b9b186f89fda06a599` |
| engine/source/Inventory.cs | 14236 | `96b10ba12aaea4bd0288bb3b2d41253af27974e8f0e8969ff35fa4b45709d8df` |
| engine/source/WorldObjects.cs | 5869 | `d86aa50b81d36a9353b1d0910cccdbf18affca56e555b3080cb8499cfb22e680` |
| engine/source/ObjectAssets.cs | 7169 | `b09a5849e71b2c282561df96f628f4bd5cca5e5ecb02ad8d61b06cf24e12cd3d` |
| engine/source/MagicRendering.cs | 4867 | `c65cd7e3af2f92d689bb093de1007003efff060798e71a7fe1cad602120f9963` |
| engine/source/GroundRendering.cs | 1566 | `a0a13e18ec6702cdb8f0deb6011586b20667c598c5010b46016b8844ce141e54` |
| engine/source/GroundUi.cs | 3799 | `d1fc05f61dbeef2df739cc01d4b5bddf6e414e07997401f14332c659124f845d` |
| engine/audio/AudioAssets.cs | 3553 | `a35890d65e2f3cfc7922aafea59f500d333a445d6ce8aaeb50960b031e272bb9` |
| engine/audio/AudioCore.cs | 10244 | `53d4aa73be42ee674daceb08751c7c494ebf3c257dd1178257a4caaf94e316c4` |
| work/engine-pickup-renderer-eligibility-source-r1/HANDOFF.md | 9470 | `f2ebf1b62d667dca6940c267e956e803f9bd3213d5a37d995f7ebec6e7b4f962` |
| work/engine-pickup-asset-source-r1/metadata/entry-0.txt | 3675 | `c0e63870fee5e86ea3ac68ef66b05198672d7a70e0ed1f19bc4a3ad3f4b0c14c` |
