# Issue #710 T298：R4-G 技能提亮與舊素材 kind 分類

依據：`docs/reference/gap-matrix.md` 13.5 節 R4-G；`docs/reference/followups-5.md` §3（FU5）；`trade-window-and-projectiles.md` §3.2–§3.3（TWP）；`actions-and-names.md`（`+40`＝104）。
分支 `task/710`，PR #723。

## 做了什麼

| 項目 | 檔案 | 內容 |
|---|---|---|
| `LightFlag`／提亮規則 | `MagicRendering.cs`（`SkillLight`、`LightClass`） | 預設 `Effect`：`+9 ≠ 0` 且至少 2 格時，從第 1 格（`sub_8ABC10` 先推進游標，所以就是開始時）加 1，播完減 1。kind 2 `Lightning`：至少 3 格時，從第 2 格（開始後 `Steps[1]` 毫秒）加 1，不看 `+9`。飛行類別（3／4／5）、S30 kind 6／7／8 不提亮。不照抄原版「沒加就減」造成計數變負的瑕疵 |
| S42 type 8 | `MagicRendering.cs`、`AttackSkillPackets.cs` | `+9`＝S42 `type`（`RangeSkillPacket.LightFlag`）；S30 魔法的那一 byte 解碼只接受 0（`AttackSkillPacket.LightFlag`），所以只有 kind 2 會亮 |
| S106 gfx 2028–2056 | `MagicRendering.cs`（`LocationLights`） | S106（`Target == null`）且 gfx 在 2028–2056 時 `+9`＝1，播一輪的期間提亮；S55 不算。時間優先用執行期 list.spr，讀不到時用 `ComposePacketEffects` 畫的那份已核定素材的長度 |
| `MagicLightLevel` 套到明暗 | `MagicRendering.cs`（`SyncLightEffects`）、`Ambience.cs` 註解 | 每次組畫面，讓 `Ambience.BeginLightEffect／EndLightEffect` 的次數等於目前握著計數的特效數，所以 `Ambience.At().Level` 就是 min(15, L+5)，最後一個結束時還原。`MagicLightLevel` 保留為純函式，註解寫明不要再套一次 |
| 舊素材 gfx 照 109 kind | `MagicRendering.cs`（`LegacyMagic.UsesNativeClass`） | 寫死的舊素材 gfx（221、189、191、197、2165、11722）在讀得到 list.spr、且 109 kind 是 2–8 時改走原生類別：kind 5 會飛（到達才受擊）、kind 8 不畫（施法者 `!` 受擊）。167 維持自己的飛行＋219 路徑；讀不到 list.spr 時維持舊路徑 |
| 10147／10145 疊層深度 | `ProjectileRendering.cs`（`EarthDepth`）、`MagicRendering.cs` | `dword_13CCBA4`：每建一個 EarthRising（S30 kind 6 收到封包時、kind 7 終點）加 29，kind 7 終點建好後歸 0。gfx 10147（EarthRising）與 10145（Eruption 段）把建立時的值加到每個 block 的 Z（上限 255）。世界重置歸 0 |
| `+40` 旗標 | `ProjectileRendering.cs` 註解、測試 | `+40`＝`dword_13CD84C[gfx]`＝list.spr 欄位 104，在引擎就是 `EffectSprite.TileType`：段用自己的 gfx，終點 EarthRising 用參數 gfx 自己的 104。本來就這樣畫，這次補註解與測試 |
| 死碼 | `Renderer.cs` | 刪除 `Renderer.PaintAmbience`（全 repo 沒有呼叫者） |
| 子特效 | `SubEffects.cs` 註解 | `]` 子特效的 `+9`＝0，不提亮；`sub_8ACD20` 特定 gfx 照抄母特效 `+9` 的部分沒做 |
| 測試 | 新檔 `SkillLightTests.cs`（`[WorldModuleTest("skill-light")]`） | 規則表、S42 type 0／8、重疊兩個特效只加一次 5、1 格 Effect 不亮、飛行不亮、S30／S42 kind 2、2 格 Lightning 不亮、S106 範圍內外與 S55、舊素材 kind 8／5／0／167、10147 疊兩層（Z+29／+58）、10145 段＋終點（Z+29、計數歸 0 一次）、104→TileType |

既有的 `skill-visual` 測試組的提亮斷言（S42 type 8、gfx 40200）在新規則下結果不變。

## 怎麼驗證

- `py -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`：**PASS**（暫時 lock，沒有寫入 `rebuild-lock.json`）。
- `py -B -X utf8 engine/tools/check-no-game-assets.py`：**pass**（errors 0）。
- 模組測試（`skill-light`、`skill-visual` 等）：依規定本機不執行編譯產物，交給 PR #723 的 CI。CI 結果見 PR。

## 沒有驗證（待驗證）

- 真機畫面：施放有方向範圍魔法（S42 type 8）、kind 2 魔法時畫面變亮、結束還原；需要開遊戲（交給 Codex／dot）。
- 實際客戶端 list.spr 裡，舊素材 gfx（221、189、191、197、2165、11722）各自的 109 kind；本次只照 kind 分路，哪些真的會改走原生路徑沒有逐筆查。
- 3.81 S106 實際送的 gfx 有哪些落在 2028–2056（FU5 也標待驗證）。
- **推測**：
  - 「播完」以特效整段長度（`Duration`，含最後顯示一次的 index0）結束，不是游標繞回 0 的那一刻；兩者最多差最後一格的時間。
  - `Lightning` 的顯示順序假設與預設 `Effect` 相同（先推進游標）。
  - 疊層深度取「物件建立時」的 `dword_13CCBA4`，並用 SPR block 的 Z 代表原版圖層深度；S30 kind 6 的建立時間以第一次組畫面時（已收到封包）計算，順序依組畫面時的特效清單順序。
  - S106 讀不到 list.spr 時，無法判斷格數是否 ≥ 2，直接以素材長度提亮。

## 沒做（不在本組範圍）

- `S_GlobalEffect(3)`／S250 83 煙火提亮：在 `PacketBoxScreen.cs`，原表註明另做。
- gfx 2001 動作 19、gfx 53 走路第 3 格、移除 6706／6712／6718／6724 物件、`sub_8ACD20` 子特效照抄 `+9`：FU5§3.4 列為選配。
- S42 kind 2 是 `Lightning` 類別，照理也不處理 `]`；目前 S42 kind 2 仍照預設 Effect 跑 `]`（`ObserveEffectFrames`），沒改。

## 產出路徑

- `engine/source/MagicRendering.cs`、`ProjectileRendering.cs`、`SubEffects.cs`、`AttackSkillPackets.cs`、`Ambience.cs`（只有註解）、`Renderer.cs`（只刪 `PaintAmbience`）
- `engine/source/SkillLightTests.cs`（新）
- `docs/reports/issue-710.md`（本檔）

## 給下一棒的提示

- S250 83 煙火提亮：在 `PacketBoxScreen.cs` 依 FU5§3.2 每個煙火特效呼叫 `session.Ambience.BeginLightEffect()`、播完 `EndLightEffect()`；它和本組的 `Renderer.SyncLightEffects` 共用同一個計數，兩邊各自只 End 自己 Begin 過的次數就不會互相干擾。
- 要對真實資料檢查舊素材分路：用 `EffectSpriteCatalog.Get(gfx).Kind` 看 221、189、191、197、2165、11722 的 109 kind；kind 2–8 的會走原生路徑。
- 真機驗收清單：①夜間地圖施放 S42 type 8 範圍魔法，畫面變亮、特效結束還原；②kind 2 魔法（list.spr 8 個）第 2 格開始變亮；③兩個重疊時只亮一級；④白天（L=15）不變。
