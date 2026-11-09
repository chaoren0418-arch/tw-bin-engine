# Issue #643 — T269：技能飛行 kind 6／7 與子特效 `]` 的範圍（Q-4 補做）

分支 `task/643`。依據：`docs/reference/trade-window-and-projectiles.md` §3（T266）、`followups-3.md` §1（T275）、`send-sites-2.md` §6。

## 做了什麼

### 1. kind 7（Eruption）逐段推進（`ProjectileRendering.cs`）
- 新增 `EruptionMotion.Plan`：起點是施法者座標；施法者 `!` 格時，終點改成目標的目前位置。方向取 4 方向（>3 減 4）。步數取切比雪夫距離，用 16.16 定點、長軸一格的步進，內部 x 取偶數。
- 每段在原地播一次。游標到影格號 2 時生下一段，也就是段開始後 `Steps[1]` 毫秒。gfx 7374／10145 每段走兩步，剩餘步數仍然只減 1。新位置等於終點，或剩餘步數 <1 時，不保留新段，改建終點物件。
- 終點物件一律走 EarthRising 路徑（`ComposeEarthRising`），不看參數 gfx 自己的 109 kind。啟動時如果目標還在，移到目標的目前位置。每個 `!` 格都受擊。
- 段本身不受擊，即使段的圖有 `!` 也一樣。`MagicClass.CasterHits` 拿掉 kind 7，施法者 `!` 格不再直接受擊。
- 例外處理：段的圖少於 3 格時，游標到不了 2，不會生下一段，也不會建終點物件；參數是 −1 時沒有終點物件。這兩種情況記到 `UndrawnMagicEffects`，不畫終點。

### 2. kind 6（EarthRising）
- 受擊維持原樣（每個 `!` 格觸發）。拿掉 S30 kind 6 的 `]`。

### 3. `]` 的範圍
- 拿掉 S30 kind 2（Lightning）與 kind 6 的 `ObserveEffectFrames`。kind 7 的段與終點物件不處理 `]`。
- S42 非飛行類是預設 Effect，`]` 照舊處理。落點圖照舊處理。
- 飛行物維持只記錄、不生成，說明文字從「待查證」改成「已查證不處理」（`SubEffects.cs`）。

### 4. 技能冷卻來源 `SpellList.xml`（`ActionCadence.cs`）
- 新增 `SpellListCooldowns`。執行期讀另一個本機客戶端的 `Tile.idx`／`Tile.pak`，預設 kor25 `D:\381懶人包kor25(原)`，可以用環境變數 `TW_SPELLLIST_CLIENT` 改。
- 讀取順序：先找 `SpellList-c.xml`，再找 `SpellList.xml`。在記憶體裡解密，記錄 XML 與 idx 的 SHA-256，並標示是否等於 kor25 的已知 SHA（`5d21a9ba…0cc2`）。檔案不進 repo。
- 換算：XML `id`＋1＝3.81 技能編號。沒寫 `delay` 時是 10 ms；`dir`／`direction` 只有 `"true"` 才是 1。
- 下列情況沿用現行 `ReuseMs`：
  - 沒有檔案、讀不到，或沒有這個技能；
  - 同一個 id 出現兩次；
  - XML 的 direction 對應的動作（18／19）和引擎實際施法的動作不同。這時視為換算不確定，不套用。
- XML 的 `+60` direction 只用來做上面的核對，不改施法動作。

### 5. 新測試 `ProjectileKindTests.cs`（`[WorldModuleTest("projectile-kind")]`，自動發現，接在 `--module-tests`）
- 段數、位置與時間（距離 3：3 段，間隔 83 ms，終點在 +249）、兩步 gfx、距離 0、2 格段、4 方向。
- 施法者 `!` 格不受擊；段不受擊；終點物件跟著目標移動後的位置；終點物件的兩個 `!` 格各受擊一次；參數沒有 `!` 時不受擊；沒有目標時不受擊；世界重置會清掉推進計畫。
- kind 6 每個 `!` 格都受擊；kind 2／6 不生 `]`，kind 0 和 S42 會生。
- SpellList：id＋1、預設 10、`dir`／`direction`、單引號、`<pspell>`、重複 id、direction 不符時退回原值；合成 idx／pak（明文與加密）、`-c` 優先、記錄 SHA、缺檔與 idx 格式錯誤。

## 超出可改清單的檔案（必要的最小掛鉤，請審查）
- `MagicRendering.cs`：kind 6／7 與 `]` 的分派原本就在這裡，不改就做不到任務目標。只改了下列幾處：
  - `CasterHits` 拿掉 7；
  - kind 7 轉給 `ComposeEruption`；
  - kind 2／6 不呼叫 `ObserveEffectFrames`；
  - epoch 重置時呼叫 `ClearEruptions()`；
  - 註解。
- `Magic.cs` 一行：`spell.ReuseMs` 改成 `SpellListCooldowns.ReuseMs(id,spell.Action,spell.ReuseMs)`。不改這行，冷卻來源接不上。
- `engine/_integration/dev/demo-tests/SkillEffectTimingChecks.cs`：舊檢查斷言「kind 7 只記錄、施法者 `!` 格受擊」，和新行為矛盾，所以改成新行為。這個群組（attack-skill）不在 CI 裡，但 DemoChecks 要能編譯。

## 怎麼驗證
- **已驗證（本機）**：
  - `rebuild.py --build`：用 scratch 的 NuGet 設定指向原專案的 `engine/vendor`，lock 只寫到 scratch，沒有改 `rebuild-lock.json`。product、audio、launcher 都建置成功，productDll SHA `93843261…ea1f`。
  - `DemoChecks.csproj` 編譯成功，0 個錯誤。
  - `check-no-game-assets.py`：pass。
- **已驗證（靜態讀檔，`scratch/t643/spell_probe.py`）**：
  - kor25 `Tile.idx` SHA `e205c88f…0a6a`。
  - `SpellList.xml` 加密 SHA `6430b2a1…1d37`，解密後 SHA `5d21a9ba95f294fb5813becc7a46f02b9eb6f66b91722bad81218f7a2f1a0cc2`，共 186 個 `<spell>`。
  - XML 宣告 UTF-8，名稱實際是 CP950，所以程式以 Latin-1 解碼，只讀 ASCII 屬性。
  - 引擎 catalog 的 4 個技能（1／3／4／8）動作全部相符，delay 都是 10。
- **已驗證（CI，PR #696，run 42634004552）**：
  - dotnet、map-continuity、npc-offline 三個 job 全部通過。
  - `--module-tests` 中 `projectile-kind` 45 項通過、`skill-visual` 47 項通過，沒有失敗項。
  - 依規定，本機沒有執行任何編譯產物。

## 未驗證什麼
- 實機畫面：kind 7 地裂的節奏與疊層、終點時機。需要 Codex／dot 在 P24Lab 實測（例：129→123 crust、4728→4730 沒有受擊）。
- 推測的部分：
  - 終點 EarthRising 的方向沿用段的 4 方向；
  - 少於 3 格的段照字面不連段；
  - gfx 10147／10145 的 `dword_13CCBA4` 疊層深度沒做；
  - `+40` 繪製旗標沒做。
- 門與 `.s32` 地圖特效的 `]`（原版會處理）沒加，因為要改 `ObjectRendering.cs`／`MapEffects.cs`，不在本張範圍。
- 3.81 客戶端本身會不會讀 SpellList.xml 無法查證（LiTo 有殼）。

## 產出路徑
- `engine/source/ProjectileRendering.cs`、`SubEffects.cs`、`ActionCadence.cs`、`ProjectileKindTests.cs`（新）
- `engine/source/MagicRendering.cs`、`Magic.cs`、`engine/_integration/dev/demo-tests/SkillEffectTimingChecks.cs`（最小掛鉤）
- 本報告 `docs/reports/issue-643.md`

## 給下一棒的提示
- 開一張小任務，給門（`ObjectRendering.cs:57` 的 `if(!door)`）與 `.s32` 地圖特效（`MapEffects.cs`）加上 `ObserveOwnerFrames`。依據是 TWP §3.4，要排除特例格（gfx 53／152／894）。
- 冷卻要改用 ly 等其他客戶端時，設定 `TW_SPELLLIST_CLIENT=<客戶端目錄>`。來源 SHA 在 `SpellListCooldowns.Table.Source`。
- 實機驗收要看：kind 7 的受擊時間應該晚於施法者 `!` 格，約等於「段數 × 第二格時間」再加上參數圖第一個 `!` 格的時間。
