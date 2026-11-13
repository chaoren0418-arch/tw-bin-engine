# T461（#1046）去伺服器依賴：動作時序與職業 gfx 集合

日期：2026-11-13。執行者：本機 Claude（claude-local）。分支：`task/1046`。

依據：`docs/reference/asset-recipes/action-timing.md`（T447）R1／R2；`docs/design/asset-recipe.md` 第 5 節（職業 gfx 集合）、L5（spr_action 無法由客戶端驗證，定案：照 R1 用、不加表）、第 9 節 T461 列。沒有讀 SQL、JAR、DB。

## 結論

產品的冷卻、送包節流、施法與放箭的放出點，全部改成由 **list.spr 動作 metadata（外觀檔各影格的 `ticks` 與 `!` 事件）** 照 R1／R2 計算；`players/manifest.json` 的 `serverActionMs`、`castReleaseMs`、`projectileReleaseMs` 產品不再讀，也不再要求存在（只保留成準備器的紀錄，給 T470 對照）。職業 gfx 集合依設計第 5 節不建表：gfx 一律來自伺服器封包，`IsCharacter`／`ForGfx` 看的是已準備的 profile。

用 r29 基線實測：42 個 profile 以新來源算出的值與基線 **336／336 相符**（serverActionMs 168、stepMs 42、castReleaseMs 84、projectileReleaseMs 42 筆含 31 筆 null）；`objects/0.json` 的施法放出點 916、`objects/0-weapon-20.json` 的放箭點 791 也相同。所以對現有資源是等價替換。

## 做了什麼

### 新增 `engine/source/ActionTiming.cs`

- `ActionStep(Ticks, Events)`：list.spr 一格的時序 metadata。
- R1 `ServerMs(steps)＝40 × Σticks`；空動作、總和 0、負 ticks 丟 `InvalidDataException`（不填假值）。
- R2 `ReleaseMs(steps)`：第一個含 `!` 的影格之前，各格 `floor(ticks×1000/24)` 的總和；沒有 `!` 回 null。
- `ActionNumber`（walk＝姿勢+0、attack＝姿勢+1、18、19）。
- `Steps(json, action)`：讀外觀 JSON 方向 0 的影格；**缺 `ticks` 就拒收**，不從 `milliseconds` 反推。
- `StepsOf(path, sha, action)`：依路徑＋SHA-256 快取 profile 檔方向 0 的影格（只留 ticks／事件，不留像素）。
- `ServerMs(profile, action)`、`CastReleaseMs(profile)`、`ProjectileReleaseMs(profile)`（只有姿勢 20 的弓 profile 有）。

### 讀取點的改動（T447 A1 表逐項）

| 讀取點 | 原本 | 現在 |
|---|---|---|
| `ActionCadence.Capture`（近戰／遠程冷卻、施法冷卻下限、`Combat.cs:116` 送包節流、`ExtendActionCooldown`） | `profile.ServerActionMs[...]` | R1：同一段已讀的 sequence 的 ticks |
| `CombatRules.DurationFor`（攻擊回報的退路） | `ServerActionMs["attack"]` | 新的 `ActionCadence.ServerMs(gfx, weapon, 1)`（R1） |
| `MagicRules.CastMsFor` | `ServerActionMs[spellDir/NoDir]` | R1（action 18／19） |
| `MagicRules.CastMs(id)` | 寫死 1320／1480 | `CastMsFor(id, 0, 0)`（基線算出同樣 1320／1480） |
| `MagicRules.ReleaseMs`（gfx 0 退路） | `objects/0.json` 的 `castReleaseMs` 欄位 | R2：同檔 spellDir 的 ticks |
| `MagicRules.ReleaseFor(gfx)` | manifest `castReleaseMs.spellDir` | R2：施法者 profile 的 spellDir；沒有 `!`、沒有 profile 或 profile 讀不了 → 退回 gfx 0 的值（原本未知 gfx 就是這樣退） |
| `RangedRules.ReleaseMs` | `0-weapon-20.json` 的 `projectileReleaseMs` 欄位 | R2：同檔 attack 的 ticks |
| `RangedRules.ReleaseFor(gfx)` | manifest `projectileReleaseMs` | R2：只限真的弓 profile（`Get` 退回空手時回 null，因為空手攻擊也有 `!`，那不是放箭點） |
| `PlayerProfiles.LoadManifest` | 要求四個 serverActionMs 鍵、castReleaseMs.spellDir、弓要有 projectileReleaseMs | 三欄改為可缺、不檢查；`serverActionMs.walk` 原本就沒人用 |
| `PlayerProfiles.Load` | 只比外觀 `stepMs` 與 manifest | 另外核對 `stepMs ＝ R1(walk 方向 0 的 ticks)` |

`stepMs` 仍由 manifest 先給（移動節流在登入時就要，不想為它先解整個外觀檔），但它本來就是準備器照 R1 算的值，而且外觀載入時會再與 walk ticks 核對。

### 職業 gfx 集合（T447 A3）

不建職業表，照設計第 5 節：`IsCharacter`／`ForGfx`／`ObjectAssets.cs:34–35` 照舊看已準備的 profile；gfx 來自 S87／S76。只在 `PlayerProfiles.cs` 補註解說明。按需準備（預熱清單以外的 gfx）是 T477／T478 的範圍。`PickupAssets.Supports`（T463）、`TitleAndEmote.classForms`（IDA 出處）不在本張範圍，沒動。

### 測試

- 新增 `[CheckGroup("action-timing")]`：`engine/_integration/dev/demo-tests/ActionTimingChecks.cs`，用合成 metadata（只有 ticks 與事件記號）：
  - R1／R2 純規則：tick 總和、0 tick 首格、`!` 在首格、沒有 `!`、其他事件不算、負／空／零長度拒收、動作號、缺 ticks 拒收。
  - 合成 assets 樹：manifest **不帶** serverActionMs 等欄位，另一個 profile 故意帶錯值 1，證明產品不讀。逐一檢查 `Capture`（含加速 ×0.755、緩速 ×2、施法冷卻下限、合法姿勢退回空手）、`DurationFor`、`ActionCadence.ServerMs`、`CastMsFor`、`MagicRules.ReleaseMs／ReleaseFor`（含沒有 `!`、未知 gfx、SHA 不符、缺 ticks 的退路）、`RangedRules.ReleaseMs／ReleaseFor`（含空手退回時回 null）、SHA 不符與缺 ticks 在 cadence 路徑明確失敗、`IsCharacter`／`ForGfx` 只看已準備的 profile。
- `.github/workflows/build.yml` 加一步跑 `action-timing` 群組（自己一個行程，因為 `PlayerProfiles` 是行程層級的快取）。
- 既有合成資料改成帶 list.spr 式的 `ticks`：
  - `CombatAdmissionChecks.cs`：attack／spellDir／spellNoDir 的 ticks 16／33／37（R1 ＝ 640／1320／1480，期望值不變），manifest 拿掉三欄。這個 fixture 也被 cast-cadence、duel、afflictions、resurrection、hit-reaction-suppress 共用。
  - `PolymorphChecks.cs`：原本的伺服器值 644、964 等不是 40 的倍數，R1 算不出來；改成 ticks＝(16 或 24)+姿勢，期望的 `ServerBaseMs` 跟著改（800、1120、960、2960、2640），動畫長度與移動的期望值不變。
  - `MapContinuityChecks.cs`、`SlowActionChecks.cs`、`sound-packets-tests` 的 manifest 仍帶舊欄位，產品現在忽略，沒有改。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev`：成功，0 警告 0 錯誤。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`（含 asset-prep、polymorph 等所有模組）：成功，3 個既有的 CS8602 警告（不是本張的檔）。`CombatSfxChecks.csproj` 也建置成功。
- 基線等價：`scratch/t461-verify.py`（不入 git）以 Python 讀 `releases/r29/assets`，用 R1／R2 從各 profile 外觀檔的 ticks 重算，與 manifest 比對：42 個 profile、336 項全部相符；gfx 0 的 916、791 也相符。也確認 31 個非弓 profile 的攻擊動作帶 `!`，所以放箭點必須限定姿勢 20。
- **本機沒有執行任何編譯出的程式**（依本機執行限制），測試結果以 PR 的 CI 為準。
- **CI（PR #1086，run 43441928244）全部綠燈**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 都 pass。`map-continuity` job 內含 module tests、combat-admission（共用 fixture 的 cast-cadence 等）、polymorph-cadence，以及新步驟 `action-timing`（輸出 `PASS 359 DEMO checks`，數字含前置群組）。

## 沒有驗證的

- `PlayerProfiles.Load` 新加的 stepMs 核對，在合成測試沒有覆蓋（要完整外觀檔，含 8 方向與像素）；r29 基線 Python 核過 42／42 相符。T455 的 asset-prep 測試會走到 `Load`，由 CI 驗。
- 真機：L5 限制照舊（基線以外的姿勢、變身外型、伺服器改過的 spr_action 列沒有對照）。真機遇到伺服器回退或擋動作請記進 Issue，不在客戶端加表。由 T474 真機回歸留意。
- 在本機以 `engine/assets`（r29）跑的模組測試（`MagicRenderingTests` 的 `CastMs(4)==1320`、`RangedRenderingTests` 的 791）預期不變（Python 核過），但沒有實跑。

## 產出路徑

- `engine/source/ActionTiming.cs`（新）
- `engine/source/ActionCadence.cs`、`Combat.cs`、`Magic.cs`、`Ranged.cs`、`PlayerProfiles.cs`
- `engine/_integration/dev/demo-tests/ActionTimingChecks.cs`（新）、`CombatAdmissionChecks.cs`
- `engine/_integration/dev/polymorph-tests/PolymorphChecks.cs`
- `.github/workflows/build.yml`
- 本報告 `docs/reports/issue-1046.md`
- 沒有改 `rebuild-lock.json`、`docs/REPORT.md`、`Program.cs`、`DemoChecks.csproj`。

## 給下一棒的提示

- **T470（等價驗收）**：manifest 的 serverActionMs／castReleaseMs／projectileReleaseMs 還是準備器輸出的一部分，可以拿來和基線逐項比；產品只是不讀。若之後決定從 manifest 拿掉，`PlayerProfile` 的三個欄位可以刪，`asset-prep-tests/SprRecipeBChecks.cs:137–140` 要跟著改。
- **T477／T478（執行期按需準備）**：新外觀只要 sequences 帶 `ticks`，`ActionTiming` 就能算出時序，不需要 manifest 的時序欄位；`stepMs` 可以直接用 `ActionTiming.ServerMs(walk)`。
- **T474（真機回歸）**：L5——用基線以外的姿勢或變身攻擊／施法時，留意伺服器回退或擋動作。
- `ActionCadence` 的 native 長度仍優先用影格的 `milliseconds`、沒有才用 ticks 換算，沒動（那是動畫，不是伺服器間隔）。objects 的動畫長度照 D4 重現基線，本張沒動。
