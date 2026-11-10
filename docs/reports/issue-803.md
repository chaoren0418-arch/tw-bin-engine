# Issue #803 T340：R7-A 戰鬥排隊、S42 光照、選目標游標（code，第 1 次嘗試）

依據：`docs/reference/gap-matrix.md` 第 15.6 節 R7-A；SV§1.3、§2.1、§2.2（`docs/reference/skills-visual.md`）；`docs/reports/issue-735.md` 第 1、2、5 點。
分支：`task/803`（已同步 origin/main，含 PR #801）。

## 做了什麼

| 項目 | 結果 | 檔案 |
|---|---|---|
| 近戰 S30 在施法者忙碌時排隊（op 5） | **已做** | `Combat.cs`、`AttackSkillPackets.cs` |
| 箭矢 S30 在施法者忙碌時排隊（op 5） | **已做** | `Combat.cs`、`AttackSkillPackets.cs` |
| `SkillLight.Class`：kind 2 只在非 range 時當 Lightning；S42 kind 2 只有 type 8 提亮（op 240、137） | **已做** | `MagicRendering.cs`（只改 `SkillLight`）、`SkillLightTests.cs` |
| SV#3 選目標：沿用目前目標，沒有目標時進選目標模式（C_USE_SPELL） | **已做（只做狀態與協定，不換游標、不畫畫面）** | `SkillCast.cs`、`Magic.cs`、`PlayerPointer.cs` |
| 離線測試 | 新檔 `CombatQueueFollowupTests.cs`（`[WorldModuleTest("combat-queue-followup")]`，自動發現）；`SkillLightTests.cs` 更新 | 新檔 |

### 1. 近戰與箭矢 S30 排隊
- 依據：#735 用 Capstone 反組譯確認的 `sub_A3EBF0`（忙碌判斷）→ `sub_A3C710`／`sub_A3C5A0` → `sub_A40250`（佇列）。gap-matrix 15.6 把近戰、箭矢 S30 列為同一機制。本任務沒有重新反組譯。
- 實作：
  - `Combat.cs` 的近戰分支先解成新的 `MeleeAttackPacket`，箭矢分支保留 `AttackSkillPacket`。兩者都先問 `QueueIfCasterBusy`。
  - 原本的處理搬到 `ApplyMeleeAttack`／`ApplyArrowAttack`，內容不變，只是延後執行：
    - 回報：`LastAttack`、`AttackReports`、`LastProjectile`；
    - 轉向、動作、目標受擊、戰鬥音樂。
  - `QueuedSkillCast` 加上 `Melee` 欄與 `Kind`（melee／arrow／magic／range）。快照 `skillCastQueue` 也帶 `kind`。
  - 佇列規則照 #735：
    - 「忙碌」＝有已知長度、還沒播完的動作；
    - 同一施法者保持收包順序；
    - 施法者離開畫面時立即播出；
    - 最多等 5000 ms，佇列上限 16 筆；
    - 世界重置時清空。
- **引擎取捨（推定）**：自己角色的近戰／箭矢 S30 一律立即套用，不排隊。原因如下：
  1. 自己的攻擊已經由本地節流（`nextCombatAt`）控制。送出時有預測動作，回包是對預測的確認，排隊會讓確認延後、動作重播，`ApplyCombatAcknowledgement` 也拿不到回報。
  2. 既有測試 `HitReactionSuppressChecks.cs:22/31`（CI 的 combat-admission 群組）會連續收自己的兩包近戰 S30。`RangedRenderingTests.cs:13`、`DualBlockTests.cs:38/41` 也有同樣情況。這些檔案不在可改範圍。

  自己的**法術** S30／S42 維持 #735 的做法，照樣排隊。原版自己的攻擊有沒有本地預測，沒查。

### 2. S42 的 kind 2 光照
- `SkillLight.Class(kind, range)` 改成 `kind==2&&!range ? Lightning : …`。S42 的 kind 2 現在走預設 Effect，照 `+9`（type）判斷：
  - type 8 從第 1 格提亮；
  - type 0 不提亮。

  依據是 #735 的反組譯：`sub_92FE40` 沒有呼叫 Lightning 建構子 `sub_8AB900`。
- `SkillLightTests.cs`：
  - `:91` 改成 S42 kind 2＝Effect；
  - 原本 `:144` 的「S42 type 0 kind 2 仍提亮」改成「不提亮、Ambience 不變」；
  - 新增 S42 type 8 kind 2 的提亮與還原。
- `]` 的處理在 #735 已經是「S42 照常處理」，不用改。

### 3. SV#3 選目標模式
- 依據：SV§1.3（推測）。`sub_E2AB60` 若上一個目標還在 15 格內就沿用，否則呼叫 `sub_F691C0(…, 15, 179)`。
- 實作：
  - `Magic.cs`：
    - 新增 `SpellTargeting`（範圍 15、游標 179、提示文字）與 `SpellTargetPick` 狀態（技能、範圍、游標、世界 epoch、`TargetRevision`）。
    - `CastSelectedMagic()` 沒有指定目標、而且 `NeedsSpellTargetPick()` 為真時，進入選目標模式、不送 C6，並顯示「請點選施法目標（15 格內）；點地面取消」。
    - C6 成功送出時結束選目標模式。
    - 以下情況模式也會失效：換技能、世界重置，或從任何入口改過選取（`TargetRevision` 變了）。
  - `SkillCast.cs`：
    - 預設目標的解析抽成 `DefaultSkillTarget`，行為不變。
    - `NeedsSpellTargetPick()`：已學、可送的技能，沒有選取目標，而且不是書籤（HD）、召喚（51）或對自己施放的技能。
    - `PickSpellTarget(id)` 接受的條件：15 格內（Chebyshev）、可見、外觀已載入、活著（或技能允許屍體）。通過後選取目標，再照一般流程送 C6。
  - `PlayerPointer.cs`：選目標模式中，
    - 左鍵選取（`Select`）或攻擊路徑（`ActAsync(talk:false)`）點物件，都改成選施法目標，不送攻擊；
    - 點地面（`MoveTo`）取消選目標，而且不走路（推定）。
- **「上一個目標」的解讀**：用引擎的選取目標（`World.Target`）。有選取目標時照舊沿用。
- **與原版的差異（推定，待真機）**：已選取但超過 15 格的目標仍照現行規則處理，也就是技能自己的距離檢查與「10 格內」訊息，不會改進選目標模式。原因是既有測試 `CombatAdmissionChecks.cs:44–59`（距離 19）要求這個訊息，而那個檔案不在可改範圍。
- **畫面**：游標 179 是推測（Q7-8），本任務**不換游標、不畫畫面**，只提供 `SpellTargetPick.Cursor` 給之後的 UI。

## 怎麼驗證
- 編譯：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/t803/nuget.config --output scratch/t803/rebuild` 的結果是 `PASS dev`，product-build 0 錯誤。
  - nuget 設定指向原專案的 `engine/vendor`，唯讀。
  - lock 沒有寫入（temporary lock）。
- `dotnet build` DemoChecks.csproj（只編譯，不執行）：0 錯誤。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。
- 既有測試影響審查（只讀程式，沒有執行）：
  - 逐一檢查會連續送 S30 的測試：`HitReactionSuppressChecks`、`RangedRenderingTests`、`DualBlockTests`、`CombatLoopChecks`、`CombatAdmissionChecks`、`AttackSkillChecks`、`PolymorphChecks` 等。
  - 改成「自己角色的近戰／箭矢不排隊」之後，已知會壞的 4 處都不再走排隊。
- 離線測試 `combat-queue-followup`（CI 的 `--module-tests` 會跑），涵蓋：
  - 近戰排隊：回報、轉向、受擊、動作都延後；結束那一毫秒播出；長度不明不排；與法術 S30 依序排隊；其他攻擊者不受影響；快照 kind；離開畫面立即播；重置清空。
  - 箭矢排隊：飛行物在播出時刻建立。
  - 自己角色：近戰／箭矢不排隊，法術 S30 仍排隊。
  - 光照分類：S30／S42 的 kind 2。
  - 選目標模式：進入、換技能失效、書籤不進入、沿用選取目標、超過 15 格與不存在的物件被拒、選中後結束、重置清空。
  - 指標：點地面取消且不走路、左鍵與攻擊路徑改成選施法目標，不送 C229／C123。
- 依本機限制，**沒有在本機執行任何測試或程式**，結果以 PR 的 CI 為準。
- **CI（PR #826）**：
  - 第一次執行時 `combat-queue-followup` 失敗：書籤技能那一步走到 `MovementAvailable`，它要讀 CI 上沒有的 `engine/assets`。
  - 修正：會走到一般施法流程的呼叫，改成只看狀態、不看送出結果。
  - 第二次執行：dotnet、map-continuity（含 `--module-tests` 全部 `[WorldModuleTest]` 與 combat-admission 等 demo 群組）、npc-offline 都通過。

## 未驗證什麼
- 選中目標之後的完整送出（C6 bytes）在離線測試裡沒有斷言，因為動作節奏要讀 `engine/assets`，CI 沒有。這部分只驗證選目標的狀態。
- 沒有開遊戲做真機驗收（M23、M25）：
  - 他人連續近戰或射箭時，第二下有沒有等第一下播完；
  - S42 type 0 kind 2 的法術不再讓畫面變亮；
  - 選目標模式的實際操作感。
- 推定項目：
  - 自己角色的近戰／箭矢不排隊；
  - 點地面取消選目標、不走路；
  - 超過 15 格的已選目標不進選目標模式；
  - 游標 179。

## 產出路徑
- `engine/source/Combat.cs`、`AttackSkillPackets.cs`、`MagicRendering.cs`（`SkillLight`）、`SkillLightTests.cs`、`SkillCast.cs`、`Magic.cs`、`PlayerPointer.cs`
- `engine/source/CombatQueueFollowupTests.cs`（新檔）
- `docs/reports/issue-803.md`（本報告）

## 給下一棒的提示
1. **gap-matrix 更新**：
   - op 5 的「近戰／箭矢排隊」、op 240／137 的 S42 kind 2 光照、C_USE_SPELL 的 SV#3 選目標，都可以改成「已有」，各附「待真機」（M23、M25）。
   - 同時記下「自己角色的近戰／箭矢不排隊」「超過 15 格的已選目標不進選目標模式」兩個推定。
2. **Q7-8**：查 `sub_F691C0` 的游標 179 對應哪個資源。查到後，在原生 UI 游標層讀 `World.SpellTargetPick?.Cursor` 換游標。這要另開 UI 任務，可改的檔案要含游標繪製的位置。
3. **ESC 取消選目標**：可以呼叫 `WorldSession.CancelSpellTargetPick("Esc")`，但要改 `KeyboardNavigation.cs`／`UiInput.cs`，可以併進 15.6 的「小清理」或 R7-H。
4. 如果要讓「已選目標超過 15 格」也進選目標模式（更貼近原版），要同時改 `CombatAdmissionChecks.cs:44–59` 的距離 19 期望。
5. 真機（Codex／dot，P24Lab）要看的：
   - 他人連續攻擊、射箭時的動作銜接；
   - 法師的 S42 範圍法術（type 0 與 type 8）看畫面明暗；
   - 選光箭、不選目標按 M，確認進入選目標模式，再點怪物確認送出。
