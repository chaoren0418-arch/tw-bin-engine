# Issue #567 — T242：`]` 子特效與 `{`／`}` 淡入淡出（FX2）

依據：[`docs/reference/effects-2.md`](../reference/effects-2.md) 第 2 節（T227／#534）、[`skills-visual.md`](../reference/skills-visual.md) 第 2.2 節。分支 `task/567`。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/SubEffects.cs`（新） | `FrameTokens`（解析 `]N`、`{N`、`}N`）；`ActionTimeline`（每格開始時間，與 `ObjectAppearance.At`、`ActionCadence.Frame`、預設 Effect 的顯示順序一致，含循環）；`FrameEventTracker`（每格只觸發一次，新動作實例重新計算，停頓後只補最近 500 ms）；`FrameFade`（淡入淡出 alpha 0–31）；`SubEffectSet`（一次性預設 Effect：播完一次就刪除，每格處理自己的 `]`，可以遞迴，深度上限 8、同時最多 256 個）；`Renderer` 接點（`ObserveOwnerFrames`、`ObserveEffectFrames`、`ComposeSubEffects`、`ComposeFadedPatch`／`FadeMix`） |
| `engine/source/ObjectRendering.cs` | 怪物、NPC、其他角色（門除外）每次組畫面時，把當下的動作、方向、位置交給 `ObserveOwnerFrames`；自己的戰鬥／施法動作也一樣。物件畫完後呼叫 `ComposeSubEffects`。最後組 patch 改用 `ComposeFadedPatch`（套淡入淡出）。UI 快照加 `subEffects`、`subEffectMissingGfx`、`subEffectDropped`、`fades` |
| `engine/source/MagicRendering.cs` | `EffectStep` 多存每格原始 token（`Events`）；S30／S42 預設類別特效（`still` 分支）每格處理 `]`，在特效自己的位置與方向生子特效 |
| `engine/source/MapEffects.cs` | `PacketEffectAppearance.Timeline`；S55／S106 封包特效（原版走預設 Effect）每格處理 `]`，方向 0 |
| `engine/source/SubEffectsTests.cs`（新） | `[WorldModuleTest("sub-effects")]`，37 項，接 `--module-tests` |
| `engine/_integration/dev/rebuild-lock.json` | `--update-lock` 重新產生（合併 origin/main 後再產生一次） |

行為（對照 effects-2 §2.1(b)）：
- 擁有者跨過 `]N` 那一格時，在**那一格當下**的擁有者位置（`x,y` 與畫面使用的相同；移動中是起點格）與方向建立 gfx N 的預設 Effect；之後不跟著擁有者移動。**不看 list.spr 109 kind**。
- 其他物件隱身（S171，`Flags & 2`）時，這個旗標會複製到子特效，所以子特效也不畫（子特效仍存在，播完就刪除）。
- 圖檔一律在**執行期**由既有的 `EffectSpriteCatalog`（T167／#437 的讀取位置，`GroundSpriteReader.ClientRoot` 下的 `Text.idx/pak` 的 list.spr/spz 與 `Sprite*.idx/pak`）讀取，**沒有加入 `engine/assets`**。讀不到就不畫，gfx 編號記在 `SubEffects.Missing`（UI 快照 `subEffectMissingGfx`），並寫 Trace；錯誤訊息另記在既有的 `EffectResourceErrors`。
- `{N`／`}N`：擁有者 alpha 0→31／31→0，每 10 ms 走一步（`32/N`、`31/N`）。畫法：把同一組 commands 拿掉該擁有者的 actor／shadow 再組一次，兩張 patch 逐像素用 `Renderer.Weighted` 混合。被遮住的像素兩張相同，混合後不變，所以結果等同於把該擁有者以 alpha 畫入；只有 screen／multiply 疊在它上面時是近似。alpha 0 時直接用不含該擁有者的那張。只有正在淡入淡出時才會多組一次（本機 list.spr 只有 7 個 sprite 用到）。

## 怎麼驗證

- `rebuild.py --update-lock` → `rebuild.py --build`：`PASS dev`，productDll `3baf09a7…41c5`（合併 main 之後）。
- `P11.ClientEngine.exe --module-tests`：5 組、254 項全部通過（`agit`、`castle`、`recovery-death`、`sub-effects` 37 項、`world-extensions`）。
- 既有離線檢查（DemoChecks）：`map-continuity`、`doors`、`afflictions`、`combat-admission`、`pickup`、`item-status`、`polymorph-cadence`、`resurrection` 全部 exit 0（後四項在合併 main 之前跑）。其中 map-continuity 用無建構子的 Renderer 跑完整 `ComposeObjects`，確認新接點不影響既有畫面路徑。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。
- `sub-effects` 測試內容：token 解析；6325 型態序列的 `]6327` 在第 1 tick 之後觸發、只觸發一次、新動作實例再觸發；breath 每輪重新觸發，停頓後只補 500 ms；6327（與 list.spr 同格式的合成條目）在擁有者位置／方向畫出，790 ms 後刪除；讀不到的 gfx 不畫但有記錄；隱身旗標；互相生成的特效受深度上限限制，整串會過期；慢速節奏每格 ×2；淡入淡出 alpha；`FadeMix` 只改淡化擁有者影響的像素；Renderer 接點的子特效位置、方向與開始時間；世界重置會清空。
- **唯讀實資源抽查**（`scratch/t567/probe`，不進 git）：用正式 DLL 的 `EffectSpriteCatalog` 從 `D:\lineage開發\0-客戶端` 讀常見的 `]` 目標：6327、1054、189、194、197、1127、849、7842、9488 都能解出 list.spr 與第 0 格 SPR；**8086 缺 `8086-0.spr`**，正好會走「不畫並記錄」路徑。

## 未驗證什麼

- **沒有真機畫面驗收**（沒有啟動遊戲／伺服器／DB）。子特效的實際外觀、與原版的時間差還要看真機。
- 移動中的擁有者用起點格當作位置（與引擎畫面一致）；原版 `actor+16/+20` 在移動中是哪一格屬**推測**。
- 自己隱身時子特效照常畫（自己本身是 50% 半透明）；原版 Effect 對 `+195` 的處理**待驗證**。`actor+198` 的用途仍不明（只知道會被複製）。
- `{`／`}` 的時間單位（`sub_A231C0(10,…)` 的 10 當作 ms）與 `32/N` 的步長算法屬**推測**（effects-2 §2.1(c)、§6 第 4 項）。
- 沒處理的部分：.s32 地圖特效（它們也是 actor，可能也走 A4B060 的 `]`）、門、自己的走路／呼吸動作、飛行類（kind 3/4/5/6）與落點特效的 `]`；子特效影格上的音效（`<`／`[`）與 `!` 也沒處理（沒有傷害清單，原版應該也不會觸發受擊）。
- CI 結果以 PR 頁面為準（本機只跑了上面列出的項目）。
- 2026-11-08 第二次同步 origin/main（到 `f7bd263e`）：`rebuild-lock.json` 衝突採 main 版本後跑 `--update-lock` 重產；`rebuild.py --build` PASS；`--module-tests` 422 項 0 失敗（含 `P06.SubEffectsTests.Run`）；`check-no-game-assets.py` 0 錯誤。
- 2026-11-09 第三次同步 origin/main（落後 56 個 commit，到 `8354654e`）：`rebuild-lock.json` 衝突採 main 版本後跑 `--update-lock` 重產；`rebuild.py --build` PASS（productDll `ef701813…1cf4`）；`--module-tests` 11 組 608 項 0 失敗（`sub-effects` 37 項）；`check-no-game-assets.py` 0 錯誤。其餘程式檔自動合併無衝突。

## 產出路徑

- 程式：`engine/source/SubEffects.cs`、`engine/source/SubEffectsTests.cs`、`engine/source/ObjectRendering.cs`、`engine/source/MagicRendering.cs`、`engine/source/MapEffects.cs`
- lock：`engine/_integration/dev/rebuild-lock.json`
- 報告：`docs/reports/issue-567.md`
- 本機證據（不進 git）：`scratch/t567/module-tests-final.json`、`scratch/t567/demo/*.json`

## 給下一棒的提示

- 真機 smoke（dot／P24Lab）：找有 `]` 的怪物施法，例如 cockatrice 1052 的 spell（`]1054`）、黑妖 1125（`]1127`）、ent 847（`]849`），看子特效是否出現在怪物腳下、方向是否正確；用 UI 快照的 `subEffects` 與 `subEffectMissingGfx` 對照。
- 淡入淡出只有 soul of fire 等 7 個 sprite 用到（例：`11.appear`、`8.escape`、`8.death`），真機很難遇到，必要時用 GM 召喚。
- 要讓 .s32 地圖特效也處理 `]`，在 `MapEffects.cs` 的 `ComposeMapEffects` 對每個 effect 呼叫 `ObserveEffectFrames`（需要 `MapEffectAppearance` 的時間軸，與 `PacketEffectAppearance` 的做法相同），但要先確認地圖 actor 確實走 A4B060。
- 這個 worktree 原本沒有 `engine/vendor`（NU1301）；依 #13 的前例，從主目錄複製 WebView2 nupkg（SHA `56F7F4B8…1AA9`，非遊戲資源，已被 .gitignore 排除）後才能跑 `rebuild.py --build`。
