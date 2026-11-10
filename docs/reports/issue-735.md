# Issue #735 T308：R5-C 特效收尾（code，第 1 次嘗試）

依據：`docs/reference/gap-matrix.md` 第 14.6 節 R5-C；SV§2–§4、TWP§3.1／§3.4、EF2§2／§4、FU5§3。
分支：`task/735`。

## 做了什麼

| R5-C 項目 | 結果 | 檔案 |
|---|---|---|
| 施法者忙碌時整包排隊（`sub_A40250`） | **已做** | `AttackSkillPackets.cs`、`MagicRendering.cs` |
| `range` 為真時 kind 2 也不處理 `]` | **查證後不改**：原版 S42 不建 Lightning，kind 2 走預設類別，`]` 本來就要處理（見下） | `MagicRendering.cs`（只補註解） |
| `.s32` 地圖特效的 `]` | **已做** | `MapEffects.cs` |
| S_GlobalEffect(3) 煙火提亮 | **已做** | `PacketBoxScreen.cs`、`MagicRendering.cs` |
| SV#3 選目標游標 | **沒做**：要改的檔案（`SkillCast.cs`／`Magic.cs`／游標）不在本組可改範圍 | — |
| 離線測試 | 新檔 `EffectFollowupTests.cs`（`[WorldModuleTest("effect-followup")]`，自動發現） | 新檔 |

### 1. 施法者忙碌時整包排隊
- 已驗證：本任務用 Capstone 唯讀反組譯 `sub_92FE40`（S_ATTACK_MANY，.text FO＝VA − `0x400C00`）。`0x930759` 呼叫 `sub_A3EBF0`（忙碌判斷），忙碌時 `0x930789` 走 `sub_A3C710`（排入 `sub_A40250`），否則 `0x9307B4` 走 `sub_A3DC10`（立即掛上並開始動作）。腳本只在 `scratch/t735/`，不進 repo。
- 實作：`ReceiveAttackMagic`／`ReceiveRangeMagic` 先問 `QueueIfCasterBusy`。施法者目前的 `CombatAnimation` 有已知長度（`Timing`）而且還沒播完時，**整包**（目標清單、特效、轉向、動作）排進 `WorldSession.QueuedSkillCasts`，目前動作不會被打斷。
- 排隊的封包在動作播完時播出，時間從播出那一刻算。推進點有兩個：`Renderer.ComposeMagic` 每格呼叫 `RunQueuedSkillCasts`（UI 執行緒，與收包同一條），以及下一包到達時。
- 同一施法者已有排隊中的封包時，新封包排在後面，保持收包順序；其他施法者的封包不受影響。
- 引擎取捨（推定）：
  - 長度不明的動作（受擊 2、沒有外型資料的 NPC）不排隊；
  - 施法者離開畫面時立即播出（SV§2.1）；
  - 最多等 5000 ms；
  - 佇列上限 16 筆，滿了就先播最舊的；
  - `[WorldReset]` 會清空佇列，另有快照 `skillCastQueue`。
- 箭矢 S30 由 `Combat.cs` 處理，不在可改範圍，**沒有排隊**。

### 2. kind 2 的 `]`：查證結果與 gap-matrix 14.2 第 5 點不符
- 已驗證：`sub_92FE40`（`0x92FE40`–`0x930A20`）的直接呼叫裡**沒有** `sub_8AB900`（Lightning 建構子）。只有：
  - `sub_83CB50`（kind 5 FireBall）；
  - 3 處 `sub_8AB710`（預設類別）；
  - 3 處 `sub_8AB390`（Effect）。
- 這和 SV§2.2 表「kind 2 的 S42 建構：同預設」一致。所以 S42 的 kind 2 gfx 是預設 Effect，原版**會**處理 `]`。現有程式 `range||kind is not (2 or 6)` 本來就是對的，本任務只補註解，不改行為。測試會確認 S30 kind 2 不生 `]`、S42 kind 2 會生 `]`。
- **附帶發現（沒改，超出可改範圍）**：同一個理由，`SkillLight.Class(2, range:true)` 回傳 `Lightning`（不看 `+9`、一律提亮）也不符原版。S42 kind 2 應該照預設 Effect，只有 `type`＝8 才提亮。要改這點，得一起改 `SkillLightTests.cs:91` 與 `:144`，但這個檔案不在本組範圍。→ 建議開小任務修正（見「給下一棒的提示」）。

### 3. `.s32` 地圖特效的 `]`
- 依據：TWP§3.4。地圖特效在原版是 `UserObject`，待機動畫經 `sub_A4B060` 處理 `]`。
- 實作：`ComposeMapEffects` 對每個地圖特效呼叫 `ObserveMapEffectFrames`。動畫循環播放，每一輪每個 `]N` 格各觸發一次，在特效位置建 gfx N 的一次性預設 Effect，走現有的 `SubEffects.cs` 路徑。
- `MapEffectAppearance.Timeline` 改成共用；S106 的 `PacketEffectAppearance(MapEffectAppearance)` 也改用它，行為不變。
- 推定：
  - 方向固定 0；
  - 6016（狀態驅動的影格）不處理；
  - 只處理可見場景內的特效。原版畫面外也照播，這裡為了不讓畫面外的特效占滿 256 筆子特效上限而限制在場景內。

### 4. S_GlobalEffect(3) 煙火提亮
- 依據：FU5§3.2。S250/83 值 3 用 `sub_8AB390` 建 `+9`＝1 的預設 Effect。
- 實作：`PacketBoxScreen.cs` 記下煙火的 `MagicEffect` 編號（`IsFireworkEffect`），世界重置時清空。`ComposeNativeMagic` 遇到煙火時：
  - 強制用預設類別（不看 list.spr 109 kind）；
  - light flag 設 1，存活期間 L＝min(15, L+5)，走既有的 `SyncLightEffects` 計數。

### 5. SV#3 選目標游標（沒做）
- 沒有目標時「沿用 15 格內的上一個目標，否則進選目標模式（`sub_F691C0(…, 15, 179)`）」要改 `SkillCast.cs` 的 `TryPrepareSkillCast`、`Magic.cs:73–75` 與游標輸入。這些檔案都不在本組可改範圍。
- 另外 SV§1.3 本身也標「推測」（游標編號 179 沒有對資源）。所以只寫在這裡，沒有留下沒接上的程式。

## 怎麼驗證
- 編譯：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile <指向原專案 engine/vendor 的 NuGet 設定> --output scratch/t735/rebuild` 結果是 `PASS dev`，product-build 0 錯誤（2 個警告是既有的 NU1701）。worktree 沒有 `engine/vendor`，所以 NuGet 設定改指原專案的 vendor（唯讀）。lock 沒有寫入（`writes: false`）。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。
- 離線測試 `effect-followup`（CI 的 `--module-tests` 會跑），涵蓋：
  - 排隊：忙碌時不建特效、不打斷動作；`ComposeMagic` 在動作結束那一毫秒播出；收包順序；S42 目標清單保留；其他施法者不受影響；caster 0 不排隊；重置清空；5000 ms 上限；施法者離開立即播出；16 筆上限；
  - kind 2 的 `]`：S30 沒有子特效，S42 有；
  - 地圖特效的 `]`：時間軸、子特效位置與時刻、每格只觸發一次、循環時下一輪再觸發；場景外、6016、沒有 token 的都不處理；
  - 煙火：3 個都提亮、L＋5、不看 109 kind 5、結束後還原；非煙火的同一個 gfx 照 kind 5 飛行、不提亮；重置清掉標記。
- 依本機限制，**沒有在本機執行任何測試或程式**，測試結果以 PR 的 CI 為準。

## 未驗證什麼
- 本機沒有執行測試，`effect-followup` 與既有單元測試是否全綠要看 CI。
- 沒有開遊戲做真機驗收，以下都沒有在畫面上看過：
  - 排隊的實際觀感：連續施法時，第二個法術要等第一個動作播完；
  - 地圖特效子特效的外觀，以及哪些 `.s32` 特效帶 `]`；
  - 煙火提亮。
- 推定項目：忙碌只認有 `Timing` 的動作；5000 ms／16 筆上限；地圖特效方向 0；只處理場景內。
- 原版其他排隊來源（S_ATTACK_ALL、箭矢）沒做。

## 產出路徑
- `engine/source/AttackSkillPackets.cs`、`MagicRendering.cs`、`MapEffects.cs`、`PacketBoxScreen.cs`
- `engine/source/EffectFollowupTests.cs`（新檔）
- `docs/reports/issue-735.md`（本報告）

## 給下一棒的提示
1. **gap-matrix 要更正**：14.2 第 5 點與 14.3 第 5 節 `sub_A4B060` 那列的「`range` 為真時 kind 2 仍處理 `]`」不是差異。原版 S42 不建 Lightning（上面第 2 點有證據），這一項應該刪掉。`sub_A4B060` 剩門的 `]`（T290）與飛行物以外的細項。
2. **建議開小 code 任務**：把 `SkillLight.Class` 改成 `kind==2&&!range ? Lightning : …`，並同步改 `SkillLightTests.cs:91`、`:144`。改完後 S42 kind 2 只在 `type`＝8 時提亮，`type`＝0 不提亮。
3. **SV#3** 要開一張可以改 `SkillCast.cs`、`Magic.cs`、`PlayerPointer.cs`（或原生 UI 游標）的任務，並先查 `sub_F691C0` 的游標 179。
4. **箭矢 S30** 的忙碌排隊要在 `Combat.cs` 的 `ReceiveCombat` 箭矢分支呼叫 `QueueIfCasterBusy` 的同類機制（目前只收 `AttackSkillPacket`／`RangeSkillPacket`）。
5. 真機（Codex／dot）：在 P24Lab 用法師連續施放範圍與單體魔法，確認第二發等第一發動作結束才出；站在有地圖特效的地方看有沒有子特效；如果伺服器會送 S_GlobalEffect(3)，看畫面有沒有變亮。
