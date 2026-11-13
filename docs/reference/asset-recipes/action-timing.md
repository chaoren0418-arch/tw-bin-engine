# 動作時序與 gfx 選取清冊的來源閉合（T447／#1032）

日期：2026-11-13。執行者：本機 Claude（claude-local）。唯讀查核，沒有改程式。

輸入只有兩個：原客戶端 `D:\lineage開發\0-客戶端`（`Text.idx`／`Text.pak`、`Sprite*.idx`／`Sprite*.pak`）與本機基線 `engine/assets`（junction → `releases/r29/assets`）。沒有讀 SQL、JAR、DB、伺服器目錄。

## 結論

| 項目 | 判定 | 一句話 |
|---|---|---|
| A　`serverActionMs`、`stepMs` | **pass**（有一條已知限制） | `40 ms × list.spr 該動作的 tick 總和`，基線 42 個 profile、210 個值全部相符 |
| A　`castReleaseMs`、`projectileReleaseMs` | **pass** | 第一個帶 `!` 的影格之前，各影格 `floor(ticks×1000/24)` 的總和；95／95 相符 |
| A　objects 的 SQL 欄位（`sourceActions[].sql`） | **pass**（同一條限制） | 59 列的 framecount 全等於 list.spr tick 總和，framerate 全是 24 |
| A　職業 gfx 集合 | **pass** | 產品邏輯不需要「職業 → gfx」表；gfx 一律來自伺服器封包（S87、S76） |
| B　gfx 清冊 | **建議：遇到再準備並快取** | 全量準備要 2.6–4.1 GB 原始 SPR，照現行 JSON 格式約 22–35 GB |

已知限制（交主控）：伺服器實際用來擋加速的是它自己的 `spr_action` 表。本張只能證明「基線記下的那些值」等於客戶端算出來的值。基線以外的 gfx 與姿勢（變身外型、斧／矛／杖等）伺服器表是否也與 list.spr 一致，**沒有資料可以比**，列為推測。細節見「A 的已知限制」。

## A1　產品讀 `serverActionMs` 的位置

全部在 `engine/source`。資料來自 `engine/assets/players/manifest.json`（每個 profile 一組 `walk`／`attack`／`spellDir`／`spellNoDir`）。

| 位置 | 讀哪個鍵 | 用途 |
|---|---|---|
| `PlayerProfiles.cs:14` | 全部 | 載入 manifest，放進 `PlayerProfile.ServerActionMs` |
| `PlayerProfiles.cs:16` | 全部 | 檢查四個鍵都在、值大於 0，否則整份 manifest 拒收 |
| `ActionCadence.cs:50`（`Capture`） | `attack`、`spellDir`、`spellNoDir` | 成為 `ServerBaseMs`。之後算出 `ServerIntervalMs`（加速 ×0.755、緩速 ×2），再進 `WaitMs`（近戰／遠程冷卻）與 `CastWaitMs`（施法冷卻的下限） |
| `Combat.cs:116` | `attack`（經 `Capture`） | 送攻擊封包前的節流：`duration=timing.ServerIntervalMs` |
| `Combat.cs:103` → `Combat.cs:25`（`DurationFor`） | `attack` | 收到攻擊回報後延長 `nextCombatAt`；只有拿不到 `Timing` 時才走這條退路 |
| `ActionCadence.cs:183–184`（`ExtendActionCooldown`） | 經 `WaitMs`、`CastSpeedWaitMs` | 速度狀態改變後重算 `nextCombatAt`、`nextMagicAt` |
| `Magic.cs:31`（`CastMsFor`） | `spellDir`、`spellNoDir` | 與技能 `ReuseMs` 取大值。目前 `engine/source` 內沒有呼叫點 |

三點補充：

1. `serverActionMs` **只用在冷卻與送包節流，不用在動畫**。動畫長度走另一組值：`ActionCadence` 的 `NativeBaseMs`（各影格 `milliseconds` 的總和）與 profile JSON 的 `actionMs`。
2. `serverActionMs.walk` 載入後只被檢查，沒有人用。移動節流用的是 `stepMs`（`NormalHaste.cs:14–15`、`ClientLogin.cs:54`），基線裡兩者都是 640。
3. 同一份 manifest 還有兩個時序欄位：`castReleaseMs`（`Magic.cs:32`）與 `projectileReleaseMs`（`Ranged.cs:5`），用來決定特效與箭矢放出的時間點。這兩個也一併查了。

另外，`objects/*.json` 沒有 `serverActionMs`，但其中 22 個檔有 `sourceActions[].sql`（`[framecount, framerate]`）與由它算出的 `actionMs`。這是同一個 SQL 來源的另一個落點，`ObjectAppearance.Duration`（`ObjectAssets.cs:9`）拿它當怪物與 NPC 的走路、攻擊、施法動畫長度。

## A2　規則與全量比對

### 來源

| 檔 | Text.idx 記錄 | 解碼後 SHA-256 |
|---|---:|---|
| `list.spz`（產品優先讀這個） | 15938 | `e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242` |
| `list.spr` | 15937 | `523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127` |

這兩個 SHA 與 `docs/design/portable-delivery.md` 附錄 D 記的歷史研究輸入（`inner-list.spz`、`decoded-list.spr`）相同。也就是說，雖然 `Text.idx`／`Text.pak` 整檔 SHA 與歷史不同，動作表這兩筆內容沒有變。

解碼方式與產品現有的 `ListSprRuntime`（`ObjectRendering.cs:257`）、`EffectSpriteCatalog`（`MagicRendering.cs:42`）相同：Text 封存 DES-ECB，`list.spz` 內層再一層 AES-CBC（開頭 `S1223`）。`list.spz` 有 12,230 個 gfx。

### 規則

list.spr 的一列動作長這樣：`1.attack(1 6,0.0:0 16.0:3 16.1:3 16.2:4[1569 16.3:6! 16.4:4)`。括號裡逗號之後每一項是 `檔.格:ticks` 加事件記號。

- **R1　伺服器動作間隔**：`serverActionMs[動作] = 40 × Σticks`。
  - `walk` 用動作號 `姿勢+0`，`attack` 用 `姿勢+1`，`spellDir` 用 18，`spellNoDir` 用 19。姿勢就是 profile 的 `weapon`（0 空手、4 單手劍、20 弓、46 匕首、50 雙手劍）。
  - `stepMs = 40 × Σticks(姿勢+0)`。
- **R2　放出時間點**：`castReleaseMs[spellDir／spellNoDir]`、`projectileReleaseMs` ＝ 第一個事件含 `!` 的影格**之前**，各影格 `floor(ticks × 1000 ÷ 24)` 的總和。弓的 `projectileReleaseMs` 用動作 21（`20+1`）。
- **R3　objects 的動畫長度**：原本有 SQL 的動作（走路、攻擊、施法）＝ `40 × Σticks`；原本沒有 SQL 的動作（呼吸、受擊、死亡）＝ `Σticks × 1000 ÷ 24`。這只是照抄基線現況，見下方「R3 的說明」。

「40」這個常數不是客戶端資料。它來自基線自己記下的對照：`objects/1108.json` 等檔寫著 `"sql":[20,24]`、`"durationMs":800`，也就是 framecount × 40（framerate 24 時）。客戶端原生的換算是每 tick `1000÷24 ≈ 41.67 ms`，所以伺服器間隔永遠比原生動畫短一點（基線裡短 29–69 ms）。

### 比對結果

逐 profile、逐動作，全部以 `list.spz` 計算。

| 比對 | 筆數 | 相符 | 相符率 |
|---|---:|---:|---:|
| `players/manifest.json` 的 `serverActionMs`（42 profile × 4 動作） | 168 | 168 | 100% |
| 同上的 `stepMs` | 42 | 42 | 100% |
| `castReleaseMs`（42 × 2） | 84 | 84 | 100% |
| `projectileReleaseMs`（弓的 11 個 profile） | 11 | 11 | 100% |
| 38 個 `players/*.json` 自帶的 `serverActionMs` 與 manifest 一致 | 38 | 38 | 100% |
| `objects/*.json` 有 SQL 的列：framecount ＝ list.spr tick 總和 | 59 | 59 | 100% |
| 同上：`durationMs` ＝ 40 × tick 總和 | 59 | 59 | 100% |
| 同上：framerate ＝ 24 | 59 | 59 | 100% |
| 102 個 profile JSON 的動作序列（方向 0，逐格比 `檔:格`、ticks、事件） | 481 | 481 | 100% |

差異分布：沒有差異，全部是 0。

涵蓋範圍：

- 玩家：13 個 gfx（0、1、37、48、61、734、1186、2786、2796、6650、6658、6661、6671），共 42 個（gfx, 姿勢）組合。
- 有 SQL 紀錄的 NPC 與怪物：22 個 gfx（29、31、32、52、54、95、834、929、938、945、1002、1108、1184、1206、1477、1540、3101、3783、3864、3918、5065、6325）。59 列分別是走路 22、攻擊 19、有向施法 9、無向施法 9。
- 102 個 JSON 的分類：players 38、objects 43、shops 11、魔法與飛行物 9、doors 1。
- `list.spr` 與 `list.spz` 對這 42 個 profile 算出的 tick 完全相同。481 個序列裡，`list.spr` 只缺 1 個動作（`list.spz` 有）。

### `actionMs`（動畫長度，不是 serverActionMs）用了哪條算式

順帶記錄，給 T461 與 SPR 準備器參考。基線的 `actionMs` 並不統一：

| 檔案類別 | 動作 | 算式 | 筆數 |
|---|---|---|---:|
| players | walk | `40 × ticks` | 38 |
| players | 其餘 | `Σ floor(ticks×1000÷24)` | 225 |
| players | die（3 筆例外） | `ticks × 1000 ÷ 24`（不取整） | 3 |
| objects | walk、attack、spellDir、spellNoDir | `40 × ticks`（多數）或 `Σ floor(...)`（少數） | 78／9 |
| objects | breath、damage、die | `ticks × 1000 ÷ 24`（多數） | 85／3 |
| shops、魔法、飛行物 | 全部 | `Σ floor(...)`（1 筆 heal 不取整） | 18／1 |

每一筆都對得上其中一條算式，沒有對不上的。

### R3 的說明

objects 的攻擊與施法**動畫**用 `40 × ticks`，是研究時把伺服器間隔直接拿來當動畫長度的結果，不是原版行為（原版每 tick 是 `1000÷24`）。T461 可以照 R3 重現基線，也可以改成原生換算；後者會讓怪物動作慢 4%，畫面上要另外驗。這不影響「來源能否閉合」的判定。

### A 的已知限制

1. **伺服器才有的欄位**：`spr_action` 的 `framecount` 與 `framerate`。基線記到的 59 列加上 210 個玩家值全部等於客戶端的 tick 總和、framerate 全是 24，但那只是抽樣。伺服器管理者改過其中一列，或某個 gfx 的 framerate 不是 24，客戶端都無從得知。
2. **基線沒有涵蓋的姿勢**：list.spr 為職業 gfx 定義的姿勢比基線多。例如 gfx 0 有 0／4／11／20／24／40／46／50，基線只做了 0／4／20／46／50；黑妖有 54／58／62，龍騎有 11／24／62，幻術師有 11／40／58。R1 可以算出這些姿勢的值，但沒有對照。
3. **變身外型**：`PlayerProfiles` 目前只認 13 個職業 gfx。改用 R1 之後任何 gfx 都算得出值，這是功能擴大，不是等價替換，同樣沒有對照。
4. **加速與緩速的係數**（0.755、2.0，`NormalHaste.cs:6–7`、`ActionCadence.cs:13`）是產品裡既有的常數，註解寫明出自 JAR d287。本張沒有重新查證，也沒有動它。

暫行做法建議：R1 算出的值照用；真機遇到伺服器回退或擋動作時，那是限制 1–3 的訊號，記進 Issue，不要在客戶端加表去補。

### 給 T461 的解析注意事項

- 動作名稱可以有空白（`walk onehandsword`、`spell dir`），只認編號。
- 有 36 列寫成兩個左括號（`66.shop loop((1 16,…`），都是動作 66。
- 一列可以排成多行，也可以整列擠在同一行（`#1108 48 lycanthrope 0.walk(…) 1.attack(…)`）。
- 編號 100 以上是欄位（101 shadow、102 type、105 cloth…），不是動作。
- 基線 manifest 的 `unavailable` 有 15 筆。其中 12 筆寫「missing complete action」，list.spr 確實沒有那個姿勢的動作；另外 3 筆是 gfx 138（男妖精）的 0／4／20，list.spr 有動作，是研究工具在解圖時自己拒收的（`opaque pixel outside frame`），要交 SPR 準備器（T454／T455）處理。

## A3　職業 gfx 集合

讀取點：

| 位置 | 內容 | 來源 |
|---|---|---|
| `PlayerProfiles.cs:24–27`（`Get`、`IsCharacter`、`ForGfx`） | manifest 裡有哪些 gfx | 研究清冊（當初由 JAR 職業常數決定要做哪 14 個） |
| `ObjectAssets.cs:34–35` | 是職業 gfx 但沒有這個姿勢的 profile 時回 null | 同上 |
| `PickupAssets.cs:16–31`（`Supports`） | 寫死的 13 個 gfx 與姿勢 | 綁在撿取 profile 內嵌資源上，屬 T463 |
| `TitleAndEmote.cs:14`（`classForms`） | 16 個 gfx，表情動作用 | 原客戶端 `sub_8CA110`（IDA），不是 JAR |

manifest 的 `className`（`CLASSID_KNIGHT_MALE` 等）產品沒有讀。

能否改用伺服器封包：**可以，而且現在就是這樣**。

- 自己與他人的 gfx 來自 S87（`WorldSession.cs:100`、`WorldObjects.cs:42`）。
- 變身與解除變身來自 S76（`Polymorph.cs:60–67`）。程式註解寫明解除變身是伺服器再送一次基礎 gfx，客戶端不猜職業。
- 產品沒有任何地方做「職業 → gfx」的換算。`PlayerProfiles` 是拿封包給的 gfx 去查表。

所以「職業 gfx 集合」在產品裡的唯一作用，是決定**事先準備了哪些 profile**。這是 B 的問題。改成 B 建議的做法後，這份集合不需要存在：封包給什麼 gfx，就去 list.spr 查那一列。`IsCharacter(gfx)` 可以改成看 list.spr 的 `102.type`，13 個職業 gfx 加 138 全部是 `102.type(5)`。不過 type 5 不只職業（world 清冊的 gfx 945 也是 5），T461 要先確認 `IsCharacter` 真正要分辨的是什麼（目前的效果是：職業 gfx 拿到不支援的姿勢時不退回一般物件外觀）。

## B　gfx 清冊：兩種做法的估算

### 現況

基線七份清冊合計 53 個不同的 gfx，加上魔法與飛行物是 62 個：

| 清冊 | gfx 數 | `102.type` 分布 |
|---|---:|---|
| players | 13（另有 138 不可用） | 5 |
| objects/manifest | 15 | 5、6、10、12 |
| encountered-monsters | 19 | 10 |
| world-appearances | 6 | 0、5、6、10、12 |
| shops | 11 | 5 |
| shadows | 17 | 5、6、12 |
| doors | 1 | 8 |

清冊的「成員」是研究時挑的。清冊的「種類」大致可以從 list.spr 推出來：門有動作 28／29，個人商店是動作 70，陰影是 `101.shadow`（17／17 與 list.spr 相符），衣飾是 `105.cloth`。world-appearances 與 monsters 的分界對不上 `102.type`，那是研究時依「要求哪些動作齊全」分的。

### 客戶端的量

| 項目 | 數量 |
|---|---:|
| list.spz 的 gfx | 12,230（9,666 個不同的 sprite 編號） |
| 其中有動作表 | 6,588 |
| 其中沒有動作表（靜態圖、特效） | 5,642 |
| 動作表引用到的 `.spr` | 63,962 檔，2.61 GB |
| 以標頭檔數計的上限（`<sprite>-0` 到 `<sprite>-(n−1)`） | 142,776 檔，4.14 GB |
| 至少缺一個引用檔的 gfx | 384（以標頭檔數計是 2,021） |
| 17 個 Sprite 封存的 `.spr` 總量（去重後） | 243,284 檔，7.88 GB |
| 跨封存同名的檔 | 3,119，其中 2,580 個大小不同 |

每個有動作表的 gfx 引用的原始位元組：中位數 222 KB，平均 860 KB，90 百分位 2.1 MB，99 百分位 7.0 MB，最大 106.7 MB。平均 34 個檔，最多 320 個。

### 樣本實測

用 Python 讀出原始 SPR 並走完整個結構（調色盤、影格表、區塊、每段 run），只計數不輸出。

| 樣本 | gfx | 檔數 | 原始 | 影格 | 像素 | 冷讀 | 結構解析 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 基線用到的全部（含陰影 sprite） | 62 | 5,499 | 58.7 MB | 39,096 | 2,834 萬 | 14.0 秒 | 1.7–1.9 秒 |
| 隨機抽樣（亂數種子 1032） | 150 | 5,087 | 123.1 MB | 40,565 | 7,119 萬 | 3.5 秒 | 3.4–3.5 秒 |

- 冷讀 14 秒是第一次讀、檔案散在封存各處的結果（約 2.5 ms／檔）。第二次讀 0.07 秒（系統快取）。
- Python 的結構解析約 30–36 MB／秒。
- 基線這 62 個 gfx 現在的 JSON 是 119 檔、497 MB，是原始 SPR 的 8.5 倍，約 17.5 bytes／像素。

### 兩種做法

以下時間是用樣本外推的，C# 的實際速度沒有量（本機執行器不能跑編譯出的程式）。

| | 甲：遇到再準備並快取 | 乙：一次準備 list.spr 全部 |
|---|---|---|
| 要處理的原始資料 | 每個 gfx 中位數 222 KB；目前用到的 62 個合計 58.7 MB | 2.61 GB（只算動作引用）到 4.14 GB（標頭上限） |
| 磁碟，照現行 JSON 格式 | 目前這批 0.5 GB；玩得越廣越多 | 約 22–35 GB，交換新舊目錄時還要一倍暫存 |
| 磁碟，改緊湊二進位（約等於原始大小） | 目前這批約 60 MB | 約 2.6–4.1 GB |
| 首次耗時 | 每個 gfx：冷讀約 0.1 秒（34 檔），解析中位數不到 10 ms，99 百分位約 0.2 秒，最大的一個約 3 秒 | 讀檔與解析約 3–5 分鐘（依檔案位移排序循序讀，推測）；現行 JSON 還要寫 22–35 GB，推測再加 5–10 分鐘 |
| 之後每次啟動 | 讀快取 | 讀快取 |
| 清冊 | 不需要。封包給什麼 gfx 就查 list.spr 那一列 | 不需要，但要為 12,230 個 gfx 全部定出規則 |
| 缺檔與同名衝突 | 遇到才處理，一次一個 gfx | 384 個缺檔 gfx、2,580 個同名不同大小的檔要先有全域政策，否則整批準備失敗或靜默選錯 |
| 沒有動作表的 5,642 個 gfx | 用到的才需要規則 | 全部要先定規則 |
| 風險 | 第一次遇到新 gfx 的那一下要等 | 首次啟動久；絕大部分資料永遠用不到 |

### 建議

**採甲：執行期遇到新 gfx 再準備並快取。** 理由：

1. 乙要處理的量是目前實際用到的 44–70 倍（2.61–4.14 GB 對 58.7 MB），而且照現行 JSON 格式會到 22–35 GB，不適合放在首次啟動。
2. 乙必須先替 384 個缺檔 gfx 與 2,580 個同名不同大小的檔定出全域選檔政策。基線的政策（主包優先、主包沒有時替代包必須唯一）在全量下會有多少 gfx 被拒收，沒有人知道。甲只在真的遇到時處理。
3. 產品已經有甲需要的零件：`ListSprRuntime`、`EffectSpriteCatalog` 都是執行期從客戶端讀 list.spr 並解 SPR；`SelfAppearancePending`、物件的 `AppearancePending` 已經能表示「外觀還沒好」；他人缺圖時有原版的退回 gfx 1120（`PlayerProfiles.MissingSpriteGfx`）。
4. 甲同時解掉 A3：不再需要職業 gfx 集合，也不再需要七份研究清冊。

配套（給 T454、T455、T461）：

- **快取格式不要沿用現行 JSON**。它每像素約 17.5 bytes，是原始 SPR 的 8.5 倍。緊湊二進位或乾脆只在記憶體快取都可以；後者連磁碟格式都省了，而且讀原始 SPR 的量比讀現行 JSON 少得多，建議先評估（兩者的實際載入時間沒有量）。
- **快取鍵**用（封存名、idx 記錄的 offset 與 size、recipe 版本），加上 list.spz 的 SHA。不要用 gfx 編號當唯一鍵，因為多個 gfx 共用同一個 sprite（12,230 個 gfx 只有 9,666 個 sprite）。
- **準備階段只預熱很少的東西**：登入前畫面用得到的，其餘等 S87。自己角色的 gfx 要等 S87 才知道，等待期間沿用現有的「角色外觀正在更新」狀態。
- **解碼放背景執行緒**，完成前沿用現有的 pending 表現，避免最大那幾個 gfx（7 MB 以上，約 1%）卡住畫面。
- **缺檔與同名衝突**維持基線政策，一次只對遇到的那個 gfx 判定，判定結果（用了哪個封存）記進快取。
- 七份清冊在甲之下合併成一條規則：要畫某個動作，就看 list.spr 那一列有沒有那個動作號。門（28／29）、個人商店（70）、陰影（101）、衣飾（105）都是同一張表的不同欄位。

## 怎麼驗證的

- 腳本都在 `scratch/t447/`（不入 git）：`listspr.py`（解碼）、`compare.py`（A2）、`partb.py`（B 的量與樣本）、`catalogs.py`（清冊與 `102.type`）、`unavail.py`（unavailable 與姿勢）、`noact.py`（動作號普查）。
- 只用 Python 3.12 與 pycryptodome 讀檔，沒有寫任何東西到客戶端或 `engine/assets`。
- 沒有執行任何編譯出的程式，沒有建置。
- 本張沒有宣稱任何「缺資源」，所以沒有用 PakViewer 查 pak。讀到的封存是 `Text.idx`／`Text.pak` 與 `Sprite.idx`、`Sprite00–15.idx` 及對應的 `.pak`。

## 沒有驗證的

- 基線以外的 gfx 與姿勢，R1 是否等於伺服器的值（已知限制 1–3）。
- C# 實作的解碼速度、首次遇到新 gfx 時畫面的實際停頓：要等 T454／T455 寫完，由 T470 實跑。
- 乙的時間是外推，沒有實際全量跑過。
- 384 個缺檔 gfx 是真的缺，還是命名規則另有例外（例如共用別的 sprite）：沒有逐個看。
- 5,642 個沒有動作表的 gfx 原版怎麼播：沒有查。

## 順帶看到的（不在本張範圍）

`GroundSpriteReader.cs:18–29` 對 `Sprite03.pak` 到 `Sprite14.pak` 寫的檔案大小，與本機客戶端目前的檔案大小不同（例如 Sprite03：pin 422,961,934，磁碟 422,963,990）。`Sprite.pak`、`Sprite00`–`02`、`Sprite15` 相同。本張沒有算這些 pak 的 SHA，也沒有查原因。這會影響任何要從替代封存讀圖的準備器，請 T449 確認。
