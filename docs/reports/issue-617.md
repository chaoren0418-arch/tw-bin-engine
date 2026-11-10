# Issue #617 — T256：R2-H 技能表現收尾

依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-H（T244／#601）；`skills-visual.md`（SV）§1–§4、§7；`effects-2.md`（EF2）§2。分支 `task/617`。

## 做了什麼

| 小項 | 原版（依據） | 改動 | 狀態 |
|---|---|---|---|
| 舊素材路徑改走 `!` 時機（SV#5） | 施法者到 `!` 格才啟動特效；受擊在特效自己的 `!` 格（SV§2.1、§3） | `ComposeMagic` 寫死的 11722／221／2165／189／191／197 不再在收包當下畫出、受擊。改成從 `LegacyMagic.Start`＝`ActionAt + Timing.ReleaseMs` 開始（跟原生類別相同）；沒有擷取到施法者動作時（S55、caster 0、施法者不在）照舊立刻開始（§2.1、§2.5）。開始前套用和原生類別相同的施法者檢查（死亡／外型待定／外型不符就不啟動）。受擊：讀得到執行期 list.spr 時在特效自己的 `!` 格（沒有 `!` 就不受擊，§3）；讀不到時視為沒有特效物件，在施法者 `!` 格受擊（`sub_A4B060`）。畫面仍用原本的素材圖。167 原本就照 `!` 時機，不動 | 已做 |
| SV#4 轉向 | `sub_A3D050`／`sub_A3DC10` → `sub_A40EC0`：轉向目標（S42：第一個目標）的**目前位置**；沒有目標物件時用封包朝向 | `ApplySkillCaster` 改用 `SkillFacing.Toward`（和飛行相同的方向函式 F1A340）。目標不存在、目標是施法者自己、或在同一格時，用封包朝向。特效本身的方向仍用封包朝向 | 已做 |
| SV#12 提亮 | S42 type（`+9`）≠0 的特效存活期間，全域明暗 L＝min(15, L+5)，最後一個結束時還原；S106 gfx 2028–2056 也帶這個旗標（SV§2.5） | `MagicBrighten`（`Apply`、`LocationEffect`）；`Renderer.MagicBrighteningEffects`（目前存活的 type≠0 特效數，對應計數 `0x13CE258`）與 `Renderer.MagicLightLevel(L)` | **只做資料**：明暗畫面在 R2-A 才接上（`PaintAmbience` 目前產品沒有呼叫，gap-matrix 11.2 第 1 點），S106 那邊在 `MapEffects.cs`（不在本組可改範圍）。R2-A 接明暗時呼叫 `MagicLightLevel` 即可 |
| `]` 擴到落點特效 | 落點放的 param gfx 是一次性特效，預設 Effect 類別每格處理 `]`（EF2§2.1(a)） | kind 4／5 法術落點、kind 4 類箭矢落點（`ProjectileRendering`）、舊素材 167 的落點 219、以及上面的舊素材特效，都在播放期間處理自己的 `]`，在特效位置生子特效 | 已做（落點特效建成預設 Effect 類別屬**推測**，可信度高） |
| `]` 擴到飛行物 | Q-4：飛行類別（7C3AB0、821360、83CE90）是否處理 `]` 還沒查證 | 只做資料：飛行中跨過的 `]` 記在 `Renderer.WithheldSubEffects`（最多 64 筆，世界重置清空），**不生成** | 等 Q-4 |
| 冷卻改用技能資料 `+56`／`+60` | SV§1.2：冷卻＝動作 18／19 影格總長＋`+56`；用 18 或 19 由 `+60` 決定 | 沒有改。`+56`／`+60` 的來源檔還沒查到（Q-3），連 `+60` 哪個值對應 18／19 都不知道，沒有可接的資料；維持 `ActionCadence.ReuseMs` 暫代 | 等 Q-3 |
| kind 6／7 | SV§7.4 | 沒有改（kind 7 維持 #522 的路徑資料、不畫；kind 6 維持原地） | 等 Q-4 |

檔案：

| 檔案 | 內容 |
|---|---|
| `engine/source/MagicRendering.cs` | `LegacyMagic`、`MagicBrighten`；`ComposeMagic` 舊素材分支改時機與受擊；落點 `]`；飛行 `]` 記錄；提亮計數 |
| `engine/source/AttackSkillPackets.cs` | SV#4：`ApplySkillCaster` 轉向、`SkillFacing` |
| `engine/source/ProjectileRendering.cs` | 箭矢落點特效 `]`；箭矢飛行 `]` 記錄 |
| `engine/source/SubEffects.cs` | `WithheldSubEffect`、`WithholdFlightFrames`、`WithheldSubEffects`；世界重置一併清空 |
| `engine/source/SkillVisualTests.cs`（新） | `[WorldModuleTest("skill-visual")]`，47 項，接 `--module-tests` |
| `engine/_integration/dev/demo-tests/AttackSkillChecks.cs` | **改了一個既有斷言**（不在可改清單，比照 #522 的作法）：S42 施法者朝向從封包朝向 6 改成轉向第一個目標的 3（SV#4 的預期行為改變） |
| `engine/_integration/dev/rebuild-lock.json` | `--update-lock` 重產 |

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock` → `rebuild.py --build`：`PASS dev`。
- `P11.ClientEngine.exe --module-tests`：28 組、1417 項、0 失敗（含 `skill-visual` 47 項）。
- `skill-visual` 內容：SV#4 的方向函式；S42 轉向第一個目標的**目前**位置、未知目標／自己當目標時用封包朝向、特效方向不變；S30 自己施法與他人施法都轉向目標，自我施法用封包朝向。舊素材 221：施法者 `!` 前不畫也不受擊，`!` 時出現在目標位置，特效 `!` 格（+83 ms）才受擊，`]` 在特效位置生子特效，播完一次就消失；caster 0 立刻開始；讀不到 list.spr（189）時用素材圖畫、施法者 `!` 受擊、記下錯誤；施法者外型在 `!` 前改變就不啟動。SV#12：min(15, L+5)、S106 範圍、type 0 不提亮、type 8 從第一格到最後一格提亮、結束後還原、兩個重疊時計數 2。落點：kind 4 飛到目標後放 param gfx，落點特效的 `]` 在落點生子特效且每格只觸發一次；飛行中的 `]` 只記錄不生成；世界重置清空。全部用合成封包、直接放進 `EffectSpriteCatalog` 快取的合成 list.spr 條目與空白影格，不讀原客戶端或 `engine/assets`。
- DemoChecks（和 CI 相同的指令）：`combat-admission`、`attack-skills`、`item-status`、`afflictions`、`resurrection`、`polymorph-cadence`、`doors`、`pickup` 全部 exit 0。`map-continuity` 在本機會停在 `DisplayNoticeChecks` 的「notice S245 unknown message stays labeled」：本機讀得到完整的 `string-c.tbl`（#546），訊息 999 在表裡有字串，跟本次改動無關。把 `TW_CLIENT_ROOT` 指到不存在的 scratch 路徑（模擬 CI 沒有客戶端的環境）再跑：exit 0，33314 項通過。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。

## 未驗證什麼／推測

- **沒做真機**：沒有啟動遊戲、伺服器或 DB。原版是 TW16；LiTo 3.81 是否相同無法靜態證明（SV§7.6）。
- 舊素材特效讀得到執行期 list.spr 時，受擊依 list.spr 的 `!`。素材 JSON（`objects/magic-*.json`）的 events 只有音效、沒有 `!`，所以沒辦法用素材本身核對；這 6 個 gfx 實際的 `!` 格要看真機或 list.spr。如果某個 gfx 在 list.spr 沒有 `!`，帶傷害的 S30 用它時會變成不受擊（照原版 §3）。
- 舊素材路徑沒有改用 109 kind 的類別（例如 kind 8 不畫、kind 5 飛行）。原因：S55 也走這條路，而 S55 在原版是不分類別、立刻播放（§2.5），目前的 `MagicEffect` 沒辦法區分 S55 和 S30（`Magic.cs` 不在可改範圍）。
- 落點特效當成預設 Effect 類別處理 `]` 屬推測（SV 表只寫「放 param gfx」，沒寫建構函式）。
- SV#4 沒有處理施法者忙碌時整包排隊（`sub_A40250`）的情況；箭矢（S30 flight 127）的轉向不在本組檔案，沒有改。
- 本組離線測試沒有涵蓋箭矢落點的 `]`：箭矢的釋放時間要讀玩家外型資料（`RangedRules.ReleaseFor`），模組測試環境沒有 `engine/assets`。程式路徑和法術落點共用 `ObserveLandingFrames`。
- `MagicRenderingTests`（`--magic-render-tests`）與 `DualBlockTests` 要用 `engine/assets` 與 fixtures，這個 worktree 沒有，CI 也沒跑，所以**沒有執行**。其中「heal 在收包時刻畫第 0 格」的斷言在施法者有擷取到動作時會跟新行為不同（新行為是等施法者 `!`）。
- 2026-11-09 同步 origin/main（落後 5 個 commit，只有 `rebuild-lock.json` 衝突，採 main 版本後 `--update-lock` 重產）：`rebuild.py --build` PASS；`--module-tests` 28 組 1417 項 0 失敗；`combat-admission` 1750 項 PASS；`check-no-game-assets.py` 0 錯誤。
- 2026-11-09 第二次同步 origin/main（PR #637 與 main 衝突，落後 101 個 commit；只有 `rebuild-lock.json` 衝突，採 main 版本後 `--update-lock` 重產；`AttackSkillChecks.cs` 自動合併）：`rebuild.py --build` PASS；`--module-tests` 2180 項 0 失敗；`combat-admission` 1750 項 PASS；`check-no-game-assets.py` 0 錯誤。
- CI 結果以 PR 頁面為準。

## 產出路徑

- 程式：`engine/source/MagicRendering.cs`、`AttackSkillPackets.cs`、`ProjectileRendering.cs`、`SubEffects.cs`
- 測試：`engine/source/SkillVisualTests.cs`（新）；`engine/_integration/dev/demo-tests/AttackSkillChecks.cs`（改一個斷言）
- lock：`engine/_integration/dev/rebuild-lock.json`
- 報告：`docs/reports/issue-617.md`
- 本機證據（不進 git）：`scratch/t617/module-tests.json`、`scratch/t617/skill-visual.json`、`scratch/t617/demo/*.json`、`scratch/t617/demo-*.log`

## 給下一棒的提示

- **R2-A**：接日夜明暗時，把要套用的 L 先過 `renderer.MagicLightLevel(L)`。S106 gfx 2028–2056 的提亮可以用 `MagicBrighten.LocationEffect(gfx)` 判斷，並把存活數加進同一個計數（要改 `MapEffects.cs`）。
- **Q-3 查到 `+56`／`+60` 之後**：在 `ActionCadence.Capture` 傳入 `reuse:+56`，並依 `+60` 選 18／19（`MagicSpells` 的 `Action` 欄目前是暫代）。
- **Q-4 查到飛行類別的 `]` 之後**：把 `WithholdFlightFrames` 換成 `ObserveEffectFrames`（呼叫點在 `MagicRendering.cs` 的 `now<flight.Impact` 分支與 `ProjectileRendering.cs` 飛行分支），`WithheldSubEffects` 就是要生成的清單。kind 7 的路徑資料仍在 `UndrawnMagicEffects`。
- 真機（dot／P24Lab）：讓法師對怪物施放會出 221 等特效的技能，看特效是否在施法動作的 `!` 格才出現（不是一收到封包就出現）；S42 範圍法術看施法者是否轉向第一個目標。
- 這個 worktree 原本沒有 `engine/vendor`，照 #567 的作法從主目錄複製 WebView2 nupkg（非遊戲資源，`.gitignore` 已排除）。本機 PATH 沒有 `python`，用 `py -3` 跑 `rebuild.py`。
