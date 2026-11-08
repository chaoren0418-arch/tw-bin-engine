# Issue #618 — T257：R2-I 物件繪製收尾（code，第 1 次嘗試）

依據：`docs/reference/gap-matrix.md` 11.5 節 R2-I；`docs/reference/actions-and-names.md`（AN）§2.3、§2.4、§3.4；WO §4。
分支：`task/618`。只改了允許的檔案：`ObjectRendering.cs`、`PlayerProfiles.cs`、`Polymorph.cs`，新檔 `ObjectDrawTests.cs`；另外以 `--update-lock` 重產 `rebuild-lock.json`。`TitleAndEmote.cs` 不需要改。

## 做了什麼

### 1. 缺圖退回（AN§3.4，第 1 節 op 16、226）
- 新增 `ObjectDrawFallback`（`ObjectRendering.cs`），照 `sub_A3EF40` 的順序：
  - 外觀：(gfx, 姿勢) → (gfx, 0)（原版把 +194 改成 0）→ 整個 gfx 都沒有圖時改用 **gfx 1120**，並播動作 0（走路）。1120 本身也沒有圖時不畫，不借用別的圖。
  - 動作：要播的動作 → 同姿勢的姿勢動作（引擎的 `walk`）→ 姿勢 0 的 `walk`。姿勢 0 的動作 0 本身缺圖時不再退（原版只對動作 > 0 退回）。
- `ComposeObjects` 的他人物件改用這兩個函式：
  - 戰鬥動作（攻擊、受擊、死亡、施法）缺圖時不再整個不畫，而是逐級退回。只有完全一樣的動作才沿用原本的戰鬥節奏，退回的動作用它自己的長度。
  - 引擎沒有原圖的 S158 動作（例如 71 釣魚）改播姿勢動作，不再不畫。
  - 門與個人商店照舊：沒有查證過的圖就不畫。`DoorChecks` 的「不借圖」檢查仍然通過。
- 自己沒有加這條退回：原版第 3 點寫明「非自己」才換 1120。`PlayerProfiles.MissingSpriteGfx = 1120` 只是集中定義常數；自己缺 profile 時仍顯示「尚未支援此外觀」。

### 2. 名稱（AN§2.3、§2.4，第 1 節 op 186、36）
- `NameLayout.HoverIcons`：滑鼠指到時算出三種圖示的位置：
  - +181 圖示（值 1／2）：盟徽 X −10（有血盟）或 +10（沒有），Y 同盟徽。
  - 隊伍圖示：名稱 X＋名稱寬＋2。
  - 主人圖示 5：名稱 X＋名稱寬＋7；條件是有稱號、不是職業外型、+248 不是空的、+196＝0。
- 全名模式 +252 行：在 T−16，顏色 palette[54] `0x4B7F`；有這一行時，名稱移到 T−29、X 再 −3。畫面上已經會畫這行字（`NameText.Line252`）。
- 全名模式過濾 `AllNamesSkips`：+196＝1 與物件種類 2／3／9 不畫；種類 10 只畫名單內的。
- 怪物等級差外框 `LevelDiffOutline`：選項 ∈ {1,5} 且物件種類 10 時，外框依等級差換色：
  - ≥10：`0x7C00`
  - 3–9：`0x4200`
  - ≤−2：`0x02E0`
  - 其他：`0x001F`
- 畫名稱時，外框色改成每段文字各自的 `Outline`。
- 資料來源 `WorldSession.NameMarksFor`：
  - 主人：S87 的 owner 欄。
  - +252 行：個人商店標題（S158 70 的字串；AN§2.1 是**推測**）。
  - 等級差：S87 等級 − 自己等級（方向是**推測**）。

### 3. S76 接 `GhostSkipsSelfAppearance`（WO §4）
- `ReceivePolymorph` 會先完整解碼封包；格式錯誤照樣丟例外。之後如果自己在幽靈狀態，就略過自己的 S76，gfx 與 `AppearanceRevision` 都不變。他人的 S76 照常處理。

### 4. 城主王冠（op 252）——沒做
Q-2（圖源 `dword_15451B8` 與偏移表）還沒查，所以照共同規則只保留現有的協定與資料（`Castle.cs` 的 `CastleCrownFrames`），不接畫面。

## 怎麼驗證（2026-11-09，已同步 origin/main `c04142ab`）
- `rebuild.py --update-lock`：303 個來源檔。`rebuild.py --build --configfile .github/nuget.ci.config`：`PASS dev`，productDll `9f5b4cd5…77b556`。
- `P11.ClientEngine.exe --module-tests`：`pass`，28 組、1429 項，失敗 0。其中新的 `object-draw` 59 項，涵蓋：
  - 外觀與動作退回的每一級
  - 1120 本身也沒圖時不借圖
  - 各圖示位置、+252 行位置與顏色
  - 種類過濾、等級差色表
  - 用合成 S87／S158 跑完整的 `NameLayout.Plan`
  - 幽靈中略過自己的 S76，但他人照常、格式錯誤仍丟例外
- DemoChecks 相關組都通過：
  - `polymorph-cadence` 380
  - `name-display` 414
  - `doors` 648
  - `title-emote` 553
  - `ghost-mode` 721
  - `fishing` 366
  - `pets` 327
- `display-notices` 失敗，失敗的是「S245 unknown message stays labeled」（S245 鑑定訊息字串）。本任務沒有碰 S245 或字串表，推測和本機讀到完整 `string-c.tbl`（#546）有關。這組不在 CI 裡；本輪沒有在 main 上重跑確認。
- `check-no-game-assets.py`：`pass`。

## 未驗證／限制
- **1120 目前沒有素材**：`releases/r29/assets` 的 objects、怪物、場景與 players 都沒有 gfx 1120。所以「換成 1120」這一步在現有素材下不會畫出東西，物件仍列在 missing。要等有人抽出 1120 的原圖才看得到。只有逐動作退回與姿勢 0 退回現在就有效果。
- +181、物件種類（2／3／9／10）、種類 10 的「名單」：3.81 的 S87 沒有對應欄位，**需先查證**來源，現在一律是空值，對應規則在產品中不會觸發。等級差外框因此也不會觸發；選項 `NameLayout.MonsterLevelOption` 沒有 UI，預設 0。
- 三種圖示（+181、隊伍、主人 5）的**圖源沒有查證**，只在 `NamePlan.Icons` 算出位置，畫面不畫（不拿替代圖冒充）。
- 有 +252 行時盟徽的位置（`sub_F02870` 沒有反編譯）是推測：跟著名稱一起移動。
- 沒有啟動遊戲、伺服器或 DB；沒有真機看過畫面。

## 產出路徑
- `engine/source/ObjectRendering.cs`、`engine/source/PlayerProfiles.cs`、`engine/source/Polymorph.cs`
- `engine/source/ObjectDrawTests.cs`（新）
- `engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-618.md`

## 給下一棒的提示
- **Q-2（resource）**：查到王冠圖源後，在 `ObjectRendering.cs` 的名稱或物件繪製流程呼叫 `session.CastleCrownFrames(id)` 就能接上。同一輪可以順便查 +181、隊伍、主人圖示 5 與 `0x1685` 的圖號。
- **resource**：抽出 gfx 1120 的原圖（放進 objects 或 world-appearances 的清單），`ObjectDrawFallback` 不用再改就會生效。
- **需先查證**：+181、物件種類、種類 10 名單在 3.81 對應哪個封包欄位。查到後只要改 `WorldSession.NameMarksFor`。
- 真機驗收（dot）：
  - 變身成缺攻擊圖的外型時，他人攻擊會播走路動作，不會消失。
  - 開「全部顯示名稱」時，個人商店的標題會以淺藍色顯示在名稱上方。
