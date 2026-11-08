# Issue #522 報告：T216 M7-P 技能特效時機與飛行

依據：`docs/reference/skills-visual.md`（SV）§2–§4、SV#5–#14；gap-matrix 第 7 節 M7-P；m6-native-diff G12。
只改可修改檔案 `MagicRendering.cs`、`ProjectileRendering.cs`、`AttackSkillPackets.cs`；測試為新檔，另外改了兩個既有測試的斷言（見下）。

## 做了什麼

| SV# | 原版（SV） | 改動 |
|---|---|---|
| #5 | 施法者 `!` 格才啟動特效；受擊在特效自己的 `!` 格 | 所有原生類別都從 `ActionAt + Timing.ReleaseMs`（施法者 `!`）開始；施法者沒有擷取到動作（不在／caster 0）時立刻開始（§2.1）。預設類別（kind 0／1／2／6，及 S42 的非 5 類）只在特效的 `!` 格受擊；特效圖沒有 `!` 就不受擊（§3） |
| — | list.spr 影格旗標 `!`（REA B07 `+22` bit 1） | `EffectStep.Hit`；`EffectSprite.HitOffsets()`／`LatestHit()`，順序與現有 `At()` 相同（游標先前進，index 0 最後顯示） |
| #6 | S42 預設類別只放**一個**特效，在封包 x,y | `ReceiveRangeMagic` 不論 type 0／8 都只建一個 `MagicEffect`（Source＝封包 x,y），整份目標清單存在 `WorldSession.RangeTargets(number)` |
| #7 | 預設類別只有**第一個**目標受擊（原版缺陷 §2.4，刻意保留） | 清單只要有任一筆 ≠0，就只對第 0 筆受擊；收包時不再 `AnimateCombat(target,2)` |
| — | 沒有特效物件：施法者 `!` 對每筆 ≠0 受擊（`sub_A4B060`） | 特效資源缺少時改走這條路，不畫任何東西 |
| #8 kind 5 | 60 ms 飛向第一目標；到達時每個有傷害的目標受擊，每個目標位置放 param | 已實作（S30 也是）；看不見的目標不推斷位置，所以不放落點 |
| #8 kind 6 | S30 放在目標位置（啟動時重新取目前位置） | 原地，位置取目標目前位置，受擊在 `!` 格 |
| #8 kind 7 | 逐格推進＋終點 param | **只做資料不畫**：`MagicMotion.EruptionPath`、`Renderer.UndrawnMagicEffects`（每段停留時間 SV§7.4 待驗證） |
| #8 kind 8 | S30 不建特效；S42 走預設類別 | S30 不畫，施法者 `!` 受擊；S42 走預設類別 |
| #10 | kind 3 不讀 param（2349 不畫 219） | `ProjectileRendering` 與 `MagicRendering` 的 kind 3 到達後都不畫 param；kind 4 照舊畫 |
| #9／#11 | 已一致 | 行為保留（kind 3／4 飛行、4 方向、到達受擊） |

受擊一律經 `IsHitReactionSuppressed` 過濾（之前只有箭矢會過濾）。

## 怎麼驗證（已驗證）

- 產品建置：`dotnet build engine/source/OfflineClient.csproj -c Release -o build/client-product-dev`，0 錯誤（2 個 `MagicUi.cs` 警告原本就有）。
- `rebuild.py --update-lock`：`dev-inputs-verified`，sourceFiles 231。`check-no-game-assets.py`：pass，errors 0。
- 新離線測試 `engine/_integration/dev/demo-tests/SkillEffectTimingChecks.cs`（42 項，用合成 list.spz 和一像素 sprite），接在 `AttackSkillChecks.Run`，所以 `attack-skills` 群組和 CI 的 `combat-admission` 都會跑到。內容涵蓋：`!` 解析與顯示順序；S30 在施法者 `!` 前不畫、`!` 時出現在目標位置、特效 `!` 才受擊；沒有 `!` 就不受擊；caster 0 立刻開始；S42 type 0／8 只有一個特效且在封包 x,y，只有第一目標受擊（含「第 2 筆有傷害仍打第 0 筆」）；全為 0 時無人受擊；空清單仍有特效；S42 kind 8 走預設；資源缺少時施法者 `!` 讓每個有傷害的目標受擊；kind 5 的 60 ms 飛行、到達受擊與各目標落點；kind 6 取目前位置；kind 2；S30 kind 8 不畫；kind 7 只產出路徑資料；kind 3 魔法與箭矢都不畫 param。
- 本機跑的 DemoChecks（與 CI 指令相同，全部 exit 0）：attack-skills 1110、combat-admission 1728、map-continuity 32953、item-status 409、native-ui-resources 539、afflictions 580、resurrection 553、polymorph-cadence 380、buddies 496、item-transfer 400、doors 648；CombatSfx selection 46。
- 依新行為改了兩個既有斷言：`AttackSkillChecks.cs`（S42 從「每個目標一個特效、收包立刻受擊」改成「一個特效帶整份清單、收包不受擊」）、`AttackSkillRenderingChecks.cs`（S42 draw 數 2→1，位置 32603→32600）。

## 沒驗證什麼／推測

- **沒做真機**：沒啟動遊戲、伺服器或 DB。原版是 TW16，LiTo 3.81 是否相同無法靜態證明（SV§7.6）。
- **推測**：kind 7 不畫時，暫時用施法者 `!` 讓目標受擊（原版的特效物件怎麼處理受擊未知）；kind 6 的推進細節（`sub_81C7C0`）沒有查證，目前當原地特效處理；S42 的 kind 3／4（SV 表標「—」）當預設類別。
- **沒做**：SV#12 type 8 提亮（併 G5）；SV#14 gfx 7972 強制 kind 3、757→10771（屬 TW16 專屬，3.81 未證實）；SV#4 轉向第一目標；`]` 子特效。
- 舊的素材路徑（`ComposeMagic` 寫死的 11722／221／2165／189／191／197／167，只有 S30／S55 會走）沒動，所以 S30 用 221 等 gfx 時仍在收包時立刻顯示並受擊。
- S42 受擊改由渲染器處理（與 S30 法術、箭矢相同），所以不再寫入目標的 `Combat`；依賴 `Combat` 動作 2 的受擊音效在 S42 不再觸發，與現有 S30 法術一致。

## 產出路徑

- 程式：`engine/source/MagicRendering.cs`、`engine/source/ProjectileRendering.cs`、`engine/source/AttackSkillPackets.cs`
- 測試：`engine/_integration/dev/demo-tests/SkillEffectTimingChecks.cs`（新），`AttackSkillChecks.cs`／`AttackSkillRenderingChecks.cs`（改斷言、接入口）
- lock：`engine/_integration/dev/rebuild-lock.json`
- 本機證據（不進 git）：`scratch/issue-522/*.json`

## 給下一棒的提示

- T112 真機：用法師的範圍法術（S42）看是否只畫一個特效、只有第一個目標播受擊；再用魔法弓（2349）看到達後是否沒有 219。
- 要畫 kind 7，先查 SV§7.4（`sub_81C9D0` 每段的停留時間與 `dword_13CCBA4`），再接 `UndrawnMagicEffects` 的路徑。
- 如果使用者裁定不照抄「只有第一目標受擊」的缺陷，只要改 `ComposeNativeMagic` 裡 `list[0]` 那一段。
- 舊素材路徑（221 等）要對齊施法者 `!` 時機，可以改走原生路徑（`AttackSkillChecks.NativeResources` 已核對過像素相同）。
