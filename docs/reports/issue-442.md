# Issue #442 — T170：變身缺動作時退回，以及特殊外型（code，第 1 次）

依據：`docs/reference/actions-and-names.md`（T160／#428）第 3.1、3.2、3.4、3.5 節與第 6 節差異表 #16–#18。

## 做了什麼

| 差異表 | 處理 | 檔案 |
|---|---|---|
| #17 缺動作 | `PlayerProfiles.Get(gfx, 姿勢)` 找不到時，若姿勢屬於 3.1 合法值 {0,4,11,20,24,40,46,50,54,58,62,83}，退回同 gfx 的空手 profile（3.4：先退回該姿勢的走路動作，再退回 0；一個 profile 一定帶自己的 walk，所以缺 profile 就是直接到空手）。精確 profile 仍優先。 | `PlayerProfiles.cs`（只改 `Get`） |
| #18 6080／6094 | `SpecialShapes.AttachedEffects`：6080／6094 附加 6099（3.2 步驟 2）；`LoopEndStance`／`DisplayStance`：每個動作迴圈結束時姿勢 0↔20、24↔62 互換（3.5，`sub_A43560`）。 | `Polymorph.cs` |
| #18 欄位 108／111／113 | `SpecialShapes.ActionEnd`（108＝2：動作 44 播完→姿勢 4 或 11（欄位 111 非 0）且 +199＝2；45 播完→姿勢 0、+199＝1）；`AttachedEffects(gfx, field113)` 附加欄位 113 的 gfx。 | `Polymorph.cs` |
| 特效掛載點 | `WorldSession.SpecialShapeAnchors()` 列出自己與可見物件的附加特效；`Renderer.ComposeSpecialShapeEffects` 以既有 `PacketEffectAssets` 取「已核定」素材畫出，缺素材時不畫、不放替代圖。`ObjectRendering.cs` 只加這一個呼叫。 | `Polymorph.cs`、`ObjectRendering.cs`（一行） |
| #16 | 文件標「推測」，**維持現狀**：S76 `H(29/0)` 仍是 0＝卸下武器、29＝保留原武器姿勢。測試另加一項確認語義未變。 | — |

另外（任務檔案清單以外，必須同步的既有斷言）：`engine/source/EquipmentTests.cs` 原本斷言 `Get(0,62)==null`（「拳套姿勢絕不替代」），與本任務的退回規則直接衝突，改為斷言退回空手外觀。此測試只在產品 `--equipment-tests` 模式執行，不在 CI。`weapon255` 不支援的斷言保留（255 不是合法姿勢，仍回 null）。

## 怎麼驗證

- `rebuild.py --update-lock` → `rebuild.py --build`：PASS（product、launcher 0 錯誤）。
- `DemoChecks polymorph-cadence`：PASS 380（含基本變身組；`T170` 檢查 71 筆）。涵蓋：
  - S76 CDHH（自己／他人、29 與 0）與 CDDHS（偽裝）兩種格式送入 6080／6094 後的錨點；undo、S87 取代、S120 移除後錨點消失。
  - 合成 profile（gfx 0／6894，姿勢 0／4／20／50）下：缺姿勢 11、24、40、46、54、58、62、83 都退回空手；1、8、70、255 不替代；沒有任何 profile 的 gfx 不替代。
  - 世界流程：姿勢 62 時 `SupportedAppearance` 為真、提示為「角色外觀已載入」、移動節奏取空手 960 ms；S30 攻擊用空手節奏但保留伺服器姿勢 62；S119 改 255 仍不支援。
  - 無新增 C 格式（本模組沒有送出封包）。
- CI 會跑的其他 DemoChecks 群組也全部 PASS：map-continuity、combat-admission、item-status、afflictions、buddies、item-transfer、doors。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。

## 沒驗證什麼／限制

- **沒有真機**（依契約集中在 M6 smoke）。
- DemoChecks 預設群組在 worktree 需要 `engine/assets`（`terrain0.bin`），worktree 沒有該 junction，未執行；CI 也不跑預設群組。
- **變身成「完全沒有 player profile」的 gfx**（例如一般變身怪物外型）仍會顯示「尚未支援此外觀」並停止移動與戰鬥。原版 3.4.3 對非自己改用 gfx 1120，自己的處理沒有查證；而且 `PlayerProfile` 需要 stepMs／伺服器動作時間等資料，不能用合成值冒充。要完全消除這個阻擋，需要資源任務替變身 gfx 產生 profile。
- 物件層的**逐動作**退回（`ObjectRendering.cs` 第 32／34 行：他人攻擊或表情缺圖時整個物件標為不可顯示）不在本任務可修改範圍，未改。
- 6080／6094 的姿勢互換只提供 `DisplayStance`，未接進角色本體繪製（需要這兩個 gfx 的外觀素材，目前 assets 沒有 6080／6094／6099）。6099 特效素材也不存在，所以掛載點目前畫不出東西（設計如此：缺素材不畫）。特效相位以絕對時間循環，未與變身時刻對齊。
- 欄位 108／111／113：list.spr 文字欄位與 `0x13CD850`／`0x13CD858`／`0x13CD870` 表的對應**未查證**，產品呼叫端一律傳 0，所以這組規則目前只有函式與測試、沒有資料來源。
- 3.4 的「自己把 +194 改成 0 並通知 UI」未做：退回只影響外觀與節奏，`Actor.Weapon` 保留伺服器值。
- 系統訊息 #1027（姿勢 >0 變 0）文字待驗證，未做。

## 產出路徑

- `engine/source/PlayerProfiles.cs`、`engine/source/Polymorph.cs`、`engine/source/ObjectRendering.cs`、`engine/source/EquipmentTests.cs`
- `engine/_integration/dev/polymorph-tests/PolymorphChecks.cs`、`README.md`
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- 本報告

## 給下一棒的提示

1. 資源任務：查出 list.spr 中對應欄位 108／111／113 的語法，並替常見變身 gfx 產生 player profile（含 stepMs、serverActionMs），之後把 `SpecialShapes.ActionEnd`／`AttachedEffects(gfx, field113)` 接上真資料。
2. 若要讓他人缺動作時不消失，需另開任務放寬 `ObjectRendering.cs` 第 32／34 行，改為退回 walk／breath。
3. 6080／6094／6099 素材封裝後，`ComposeSpecialShapeEffects` 會自動畫出；屆時 M6 smoke 要核對特效位置與相位。
