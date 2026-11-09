# Issue #612 T251：R2-A 環境表現接上畫面

依據：`docs/reference/gap-matrix.md` §11.5 R2-A；AM§1–§4（`ambience-and-map-events.md`）、EF2§1。分支 `task/612`。

## 做了什麼

| 項目 | 做法 | 檔案 | 判定 |
|---|---|---|---|
| 日夜明暗＋FX1 光照圖接上畫面 | `PaintScene` 每格呼叫 `Renderer.PrepareAmbience`（讀 `Ambience.At`、L&lt;15 時 `ComposeAmbienceLights` 蓋光照圖、推進粒子）。需要明暗或下雨雪時，改走 `SceneView` 離屏圖 → `PaintAmbienceScene`（先畫天氣，再 `ShadeAmbience`，AM§2(b)）→ 縮放畫上。白天晴天仍走原本的背景＋patch 畫法，畫面不變 | `Program.cs`（只動 `PaintScene`）、`Ambience.cs` | 已驗證（離線） |
| 天氣粒子每格推進 | `Renderer.StepWeather`：用上一格到這一格的毫秒數呼叫 `WeatherParticles.Step`（24 ms tick、500 ms 上限沿用）；鏡頭補償＝自己顯示腳位的水平位移取反（小數累積），一次超過 800 px（傳送）不補償 | `Ambience.cs` | 規則已驗證；**補償正負號是推測** |
| `WeatherSprite` 實作 | 新檔 `WeatherSprite.cs`：`WeatherSpriteSet.Load` 執行期從原客戶端 `Sprite.idx/Sprite.pak` 唯讀讀 `924–927.img`（核 idx SHA、pak 長度、每張 payload SHA 與尺寸 10/9/9/7），層 0 用 924/925、層 1 用 926/927（序號奇偶）。層 2/3 水花與雪是程序圖樣：十字 5 點、4×4 共 12 點、雪 11／14 點，白色權重（水花 16/階段、中心 +12；雪 25）。讀不到雨圖時只畫程序圖樣，層 0/1 不畫替代圖，`WeatherSpriteNotice` 記原因 | `WeatherSprite.cs`、`Ambience.cs` | 雨圖來源與對照：已驗證；**程序圖樣的點位與混合方式是推測**（`sub_975C40`／`sub_9755E0` 的像素位置沒有 dump） |
| 盲目光圈改用 `Source.lmp` k=5 | `AfflictionColors.BlindLightStamp`：照 `AmbienceLightMap.Stamp` 的格子換算（中心 (400,240)、左 `((x−w)>>1)&~3`、1 格＝2×2 像素）取 k=5 影像，光圈中心改為自己的顯示腳位（與其他光源一致）。盲目 2 的係數改成係數表 mode 0 第 1 級整列（原本是線性內插）。產品在 S47≠0 時會先讀 `Source.lmp`；**讀不到時（CI、缺客戶端）才退回舊圓形**，舊 `afflictions` 測試照舊。盲目時 `PaintAmbienceScene` 不再做一次日夜明暗，避免變暗兩次 | `Afflictions.cs`、`Ambience.cs` | 已驗證（合成 LMP）；B==2 特定物件發光仍沒做 |
| 酒醉 `sub_81B0E0` 為假時減半 | `TimedBuffRules.LiquorOffset(level,phase,row,fullWave)`：假時 `ScanlineOffset/2`（C 整數除法）。`LiquorFullWave` 掛點預設回 true（`sub_81B0E0` 的意義沒查），`PaintLiquorWave` 改用它 | `TimedBuffs.cs` | 規則已驗證；**產品目前一律全振幅** |
| 地圖 67／420 最低醉度 | S103 型 0 在地圖 67 或 420 時醉度留 1，相位凍結在收到時（型 0 會停 50 ms 計時器）；再收到型 1–3 重新計時 | `TimedBuffs.cs` | 已驗證（AM§4(c)）；進入地圖時是否也會設醉度 1 沒查，沒做 |
| 隱身隊伍／血盟可見 | `InvisiblePeerVisible`：對方在自己隊伍（`partyMembers` 物件 id）或與自己同一個非 0 血盟 id 時，以 50% 混合畫（同自己的 `0x100`），影子跟著；其他人仍不畫 | `Invisibility.cs` | **推測**（`sub_956D30`＝隊伍／血盟、`dword_13DC4E4` 視為成立、`$431` 視為不成立；原版在收 S171 時決定旗標，這裡在繪製時判斷） |
| 畫面震動掛點（給 R2-O） | `Renderer.ScreenShake`（`Func<Point>?`，預設 null）；`PaintScene` 在線上畫世界前呼叫 `ApplyScreenShake` | `Renderer.cs`、`Program.cs` | 只是掛點 |
| 離線測試 | 新檔 `AmbienceWiringTests.cs`（`[WorldModuleTest("ambience-wiring")]`，52 checks），由 `--module-tests` 自動執行 | `AmbienceWiringTests.cs` | — |

`MapEffects.cs` 不需要改：地圖特效光源 `MapEffectLightSources` 已由 `ComposeAmbienceLights` 讀取。

## 怎麼驗證

- `rebuild.py --update-lock`：`dev-inputs-verified`（299 來源檔／308 pins）。`rebuild.py --build --configfile .github/nuget.ci.config`：`PASS dev`，productDll SHA `1c483250…abbe5`（同步 main 後的數字見下方）。
- `P11.ClientEngine.exe --module-tests`：25 組全部通過，1313 checks；`ambience-wiring` 52、`ambience-lights` 47。內容：
  - 白天晴天不走離屏、不蓋光照圖；午夜 L=1 整個畫面套係數表；自己 S40=5 → k=5 光源，800×480 視窗以腳為中心，腳下比遠處亮、視窗外未照亮；盲目時不重複變暗。
  - 天氣：240 ms＝10 tick、一格最多 20 tick；自己右移 3 px → 雨左移 3 px；超過一畫面寬不補償；雨 3 畫 512 顆且都在視窗內；雨點先畫再跟著變暗；雪 2 為 96 顆、每格 y+10。
  - 雨圖對照（924/925、926/927 奇偶，第二組 256 顆同樣）、程序圖樣點數、`.img` 解碼與長度錯誤、顏色 0 透明、沒有雨圖時層 0 不畫但雪照畫、缺客戶端丟 `IOException`。
  - 盲目 k=5 蓋章位置與範圍、空槽不補光圈、盲目 2 係數＝表第 1 級。
  - 酒醉減半表、67／420、相位凍結與重新計時。
  - 隱身：同血盟與隊伍可見（50%），其他人與無血盟時不可見。
- **本機原客戶端核對**（`TW_MODULE_CLIENT_CHECKS=1`，只跑這兩組）：104 checks 全過；實際讀到 924–927，尺寸 10/9/9/7、flags／colorKey 0，SHA 與 AM§3.1 相符。CI 不設這個變數。
- 既有 DemoChecks：`afflictions` PASS 580、`ambience` PASS 9881、`invisibility` PASS 397、`timed-buffs` PASS 645。
- `engine/tools/check-no-game-assets.py`：pass（errors 0）；`git diff --check`：通過。
- **與 origin/main 同步**（#517，1 個 commit，沒有衝突）：`--update-lock` 重產後內容不變（299 來源檔）；重建 `PASS dev`，productDll SHA `c7e75193…d241eb`；`--module-tests` 25 組 1313 checks 全過；`check-no-game-assets.py` pass。
- **第二次與 origin/main 同步**（2026-11-09，main 前進 73 個 commit；只有 `rebuild-lock.json` 衝突，取 main 版後 `--update-lock` 重產，320 來源檔／329 pins）：重建 `PASS dev`，productDll SHA `0572a880…f0e46`；`--module-tests` 34 組 1698 checks 全過（`ambience-wiring` 52、`ambience-lights` 47）；DemoChecks `afflictions`／`ambience`／`invisibility`／`timed-buffs` 全過；`check-no-game-assets.py` pass。

## 未驗證什麼

- **沒有真機**：沒有啟動遊戲、伺服器或 DB。畫面上的實際觀感（夜晚亮度、雨圖位置、雪花樣子、光圈大小）都要真機比對。
- 推測項：程序圖樣點位與混合、雨圖以粒子座標為左上角、colorKey 0 在 flags 0 時也透明、鏡頭補償正負號、隱身可見條件、`sub_81B0E0` 預設為真。
- 效能：明暗或下雨時每次重畫都會建一張可見範圍大小的 `SceneView` 離屏圖（船上背景原本就這樣做）；沒有量測 FPS。
- 非船上的線上畫面改走 `SceneView` 時，與原本「背景圖＋patch」畫法的像素是否完全相同，只有靠既有測試間接保證，沒有逐像素比對。
- 原生 UI 路徑（`--native-ui`）也經過 `PaintScene`，但沒有另外測。

## 產出路徑

- 程式：`engine/source/Ambience.cs`、`Renderer.cs`、`Program.cs`（`PaintScene`）、`Afflictions.cs`、`Invisibility.cs`、`TimedBuffs.cs`、新檔 `WeatherSprite.cs`、`AmbienceWiringTests.cs`
- lock：`engine/_integration/dev/rebuild-lock.json`
- 本報告：`docs/reports/issue-612.md`；測試輸出在 `scratch/issue-612/`（不進 git）

## 給下一棒的提示

1. **R2-O（畫面震動）**：設 `renderer.ScreenShake = () => offset` 即可，`PaintScene` 已在畫世界前套用；不必再改 `Program.cs`。
2. 真機 smoke（dot）建議看：夜晚（伺服器時間 18:00–06:00）畫面變暗、火盆等地圖特效周圍有光；下雨時有小水滴圖；盲目時只剩腳下一圈光。`Renderer.WeatherSpriteNotice`／`AmbienceLightNotice` 不為 null 代表素材沒讀到。
3. 要把程序圖樣、`sub_81B0E0`、`$431`／`sub_956D30` 做成「已驗證」，要在 IDB 查 `sub_975C40`（`0x975C40`／FO `0x575040`）、`sub_9755E0`（`0x9755E0`／FO `0x5749E0`）、`sub_81B0E0`、`sub_956D30` 的內容，再只改 `WeatherSprite.cs`、`TimedBuffRules.LiquorFullWave`、`Invisibility.cs`。
4. 盲目 2 讓特定物件發光（102 表種類 {5,6,10,12}）與職業 6/31 的係數變體仍沒做，改 `Afflictions.cs` 的 `ApplyBlindLight`。
5. gap-matrix 第 1 節 op 156、206、176 與第 5 節 `sub_F0B570` 可改為「已有（待真機）」；op 114、229、160 仍是「部分」（推測項）。gap-matrix 本身不在本任務範圍，沒改。
