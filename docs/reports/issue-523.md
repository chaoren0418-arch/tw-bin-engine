# Issue #523 — T217：M7-Q 施法拿掉本地預測動作，冷卻改用動作影格總長

依據：`docs/reference/skills-visual.md`（SV）§1.1、§1.2、第 7 節差異表 SV#1–#4；gap-matrix 第 7 節 M7-Q；主控裁定見 #504／#535（照原版做）。

## 做了什麼
- `engine/source/Magic.cs`：`CastSelectedMagic` 送出 C6 時，**不再呼叫 `World.PredictAction`，也不轉向**（SV§1.1，SV#1）。施法者動作完全由伺服器的 S30／S42 回包驅動（既有 `CaptureCombat`，`Predicted=false`）。冷卻改用 `timing.CastWaitMs`。
- `engine/source/ActionCadence.cs`：
  - 新增 `NativeCastMs`：自身外型 list.spr 動作 18／19 的影格總長，套用速度倍率（一般＝原值；加速 speed 1＝每格 ×3/4 後加總，沿用既有 `HasteFrameMs`；緩速 speed 2＝×2）。
  - 新增 `CastWaitMs = max(NativeCastMs + ReuseMs, ServerIntervalMs)`（SV§1.2，SV#2）。`ReuseMs` 暫代原版技能資料 `+56`（該欄的來源表 SV 標「待驗證」）。**不再加本地 margin**（原版沒有）；伺服器動作間隔只當下限，避免 list.spr 較短的外型送得比伺服器允許的快。
  - 新增 `CastSpeedWaitMs(speed)`；`ExtendActionCooldown` 的施法分支改用它（權威緩速後重算施法冷卻）。
  - `PredictAction` 保留，只剩近戰（`Combat.cs`，動作 1）使用，不在本任務範圍。
- `engine/source/SkillCast.cs`：查過不需要改。
- 新測試 `engine/_integration/dev/demo-tests/CastCadenceChecks.cs`，接到既有 `DemoChecks` 入口：`combat-admission` 模式（CI 已在跑）結尾加跑，另加獨立模式 `cast-cadence`。
- `engine/_integration/dev/rebuild-lock.json`：`--update-lock` 重產。

實際數值（真實 61/0 spellDir 的形狀：伺服器 1240、list.spr 總長 1290、技能延遲 10）：一般 1300 ms（原本 1340）、加速 977 ms、緩速 2590 ms。

## 怎麼驗證（本機 Windows，2026-11-08）
- `rebuild.py --update-lock` 通過；`product-build.ps1 -ConfigFile .github/nuget.ci.config` → `PASS dev`（worktree 沒有離線 `engine/vendor`，所以用 CI 的 nuget 設定）。
- `DemoChecks cast-cadence`：PASS 349（含新檢查：送出 C6 後 `OwnCombat==null`、不轉向、沒有 pendingAction；`nextMagicAt` 等於 `now+CastWaitMs`；1319 ms 時仍在冷卻、1320 ms 時解除；S30 回包才開始動作 18；送出後收到緩速時改用 list.spr×2／伺服器下限 2640；近戰預測照舊；公式單元檢查涵蓋一般／加速／緩速、伺服器下限與技能延遲）。
- `DemoChecks combat-admission`（CI 同一步驟）：PASS 1708。
- 其他相關模式全部通過：polymorph-cadence 380、item-status 409、map-continuity 32953、afflictions 580、resurrection 553、title-emote 553、duel 441。
- `SkillCastChecks --protocol-only`：PASS 691。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。

## 沒驗什麼
- `DemoChecks combat-loop` 與 `SkillCastChecks` 完整送出段：需要 `engine/assets` junction，worktree 沒有。為了避免測試透過 junction 寫進 `releases/r29`，**沒有建立 junction**，這兩項沒跑（CI 也沒跑這兩項）。
- 真機：沒有啟動遊戲／伺服器。自己施法時要多等一個往返才看到動作（原版就是這樣）；伺服器加速檢查在沒有 +100 ms margin 時是否會誤判，要靠真機 smoke 確認。
- SV§1.2 的其他速度倍率（變身 `+547`／×29/32、`0x14EC00C` 勇敢類 ×3/4、×7/8、×3/8、`+544` ×2）沒有做：引擎目前只支援 speed 0/1/2、brave 0（`NormalSpeed.Supported`），其他組合本來就拒絕施法。
- 外型沒有該動作時原版固定 1234 ms：引擎在沒有角色 profile 時本來就不允許施法，所以沒有實作這個退回值。
- SV#3（沒有目標時進入選目標游標，SV 標「推測」）：照共同規則只做協定與資料，不做畫面；仍等 #448。
- SV#4（施法者轉向目標**目前位置**）：要改 `AttackSkillPackets.cs`，不在本任務可改的檔案內，沒做。

## 產出路徑
- `engine/source/Magic.cs`、`engine/source/ActionCadence.cs`、`engine/_integration/dev/rebuild-lock.json`
- `engine/_integration/dev/demo-tests/CastCadenceChecks.cs`（新）、`engine/_integration/dev/demo-tests/Program.cs`（掛入口）
- `docs/reports/issue-523.md`

## 給下一棒的提示
- 真機 smoke（dot／P24Lab）：連續施放光箭，確認沒被伺服器判加速；畫面上自己施法的動作晚一個往返才出現（這是預期的）。若有誤判，再考慮把伺服器下限加回 margin（`CastWaitMs` 一行）。
- 原版技能資料 `+56`／`+60` 的來源表找到後，把 `MagicSpells.ReuseMs` 換成真正的欄位，並用 `+60` 決定 18／19。
- SV#4 可另開一張小單給 `AttackSkillPackets.cs` 的擁有者。
