# Issue #566 T241：光照圖接上畫面，以及地圖特效光源表（FX1）

階段 code，第 1 次嘗試。分支 `task/566`。

## 做了什麼

依據：`docs/reference/effects-2.md` 第 1.1 節（T227／#534）、`ambience-and-map-events.md` 第 3 節與第 3.1 節（T162／T167／#437）。

1. **地圖特效光源表（`MapEffects.cs`）**
   - 新增 `MapEffectLightTable.Radius(id)`，內容照 EF2 §1.1(b)：`word_138BE2C`（k=8，12 個 id）、`word_138BE44`（k=11，14 個 id）、1884／1885／1901–1987／2209→5、1892–1897→7、2195／2197–2202→9、149／1522→13、1886／2196→14、3442–3446→`2·id−6879`（5／7／9／11／13），其他→0。
   - `MapLocalEffect` 加上 `Light` 屬性，建立時（也就是 `.s32` 載入時）由 id 查表記下 k；`OriginalMapCache.cs` 不需要改。
   - `Renderer.ComposeMapEffects` 每次組畫面時，把 k > 0 的地圖特效收進 `Renderer.MapEffectLightSources`（id、世界座標、k）。
   - 3442–3446 照舊只畫原本那 1 個像素，**沒有另外畫光圈**；它們只會作為光源點進入光照圖。

2. **光源收集（`Ambience.cs` 的 `WorldSession.AmbienceLights`）**
   - 依序收：自己 → S87 物件 → 地圖特效（k > 0）。三種來源共用同一個 700 預算（k 的總和 ≥ 700 就停），也共用同一個距離濾除。
   - 距離濾除：如果光源的 `Source.lmp` 光源圖不可能碰到 800×480 視窗，就不收。濾除邊界用最大的光源圖（第 14 槽，312×156）來算，所以凡是可能照到視窗的光源都會保留。地圖特效依離視窗中心的距離由近到遠排序，預算用完時留下的是畫面上的光源。
   - 新增的兩個參數都是選用參數，舊的三參數呼叫行為不變（既有 `AmbienceChecks` 通過）。

3. **`Source.lmp` 執行期讀取（`AmbienceLightImages`）**
   - 從 `GroundSpriteReader.ClientRoot`（`TW_CLIENT_ROOT`，預設 `D:\lineage開發\0-客戶端`）唯讀讀取 `Sprite.idx`／`Sprite.pak`。依序核對：idx 的 SHA-256 pin、pak 長度、idx 中只有一筆 `Source.lmp`（比對名稱時不分大小寫）、條目範圍、payload SHA-256（`30cf051f…ce5aa`）。
   - 解析 16 槽：偏移從 payload 開頭算、配對槽共用同一塊資料、第 15 槽是 0×0，保留為空。越界或截斷就丟 `InvalidDataException`。
   - 讀不到時，光照圖保持全空（未照亮），並在 `Renderer.AmbienceLightNotice` 寫明原因；**不使用替代圖**。沒有把任何資源複製進 repo 或 `engine/assets`。

4. **光照圖與畫面接線（`Ambience.cs` 的 `Renderer` partial）**
   - `AmbienceLightMap.Render(lights, images)`：先清空，再對每個光源以第 k 張圖呼叫既有的 `Stamp`（座標換成半解析度後置中，重疊處取最大值）；空槽不畫。
   - `Renderer.ComposeAmbienceLights(session, visuals)`：以自己顯示中的腳點為中心，取 800×480 的場景視窗（`AmbienceWindow`），收集光源並蓋到 400×240 的 `AmbienceLightMap`。
   - `Renderer.ShadeAmbience(sceneBitmap, sceneArea, frame)`：視窗內用光照圖查係數表變暗；視窗外照 light 0 處理（未照亮）。
   - 已加入 `AmbienceLightsTests.cs`（`[WorldModuleTest("ambience-lights")]`），由 `--module-tests` 自動執行。

## 怎麼驗證

- `rebuild.py --update-lock`：`dev-inputs-verified`（lock 已重產）。`product-build.ps1 -ConfigFile .github/nuget.ci.config`：`PASS dev`，productDll SHA `5d8c8c0c…fb71a`。
- `P11.ClientEngine.exe --module-tests`：5 組全部通過，264 checks。本組 `ambience-lights` 有 47 checks，內容如下：
  - 驗收指定項：3442→5、3446→13、3737→8、2088→11、未列出的 id→0。另外測了 3443–3445、各範圍的兩端、1886／2196／149，以及 11 個未列出的 id。
  - `MapLocalEffect.Light` 會記下 k；3446 的外觀仍然只有 1 像素。
  - 地圖特效光源：k=0 不加入；100 個 k=13 的光源只收 54 個（總和 702 時停止）；遠處光源被濾除；近的先收；濾除邊界剛好是 312／156。
  - LMP 解析：寬高、共用偏移、空槽、越界與截斷時拒絕；客戶端不存在時丟 IOException。
  - 光照圖：蓋章位置（`&~3` 對齊）、重疊處取最大值、第 15 槽空、`Render` 會先清空；夜晚有光的地方比沒光的地方亮。
- **本機原客戶端核對**（設 `TW_MODULE_CLIENT_CHECKS=1`，只跑 `--module ambience-lights`）：50 checks 全部通過。`Source.lmp` 實際讀出 16 槽，尺寸與 AM §3.1 表一致（16×8 到 312×156，第 15 槽 0×0）；配對槽共用偏移；第 14 槽偏移 `0x19680`；第 0 槽不是全黑。CI 不設這個變數，所以 CI 不會讀素材。
- 既有 `DemoChecks`（含 ambience 群組）：`PASS 9881`。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。
- **2026-11-08 與 main 同步**（落後 27 個 commit）：只有 `rebuild-lock.json` 衝突，採 main 版後以 `--update-lock` 重產（`dev-inputs-verified`，249 來源檔）。重建 `PASS dev`，productDll SHA `0ccc5517…367ba`；`--module-tests` 7 組 394 checks 全部通過（`ambience-lights` 47）；DemoChecks `ambience` 群組 `PASS 9881`；`check-no-game-assets.py` pass。

## 未驗證／未完成

- **畫面上目前還看不到效果**：產品真正的繪製入口是 `Program.cs` 的 `PaintScene`，不在本任務可以改的檔案裡。而且日夜明暗本身（`Renderer.PaintAmbience`）原本也沒有被呼叫。本任務已把「每幀光源 → 光照圖 → 明暗處理」整條做好，**只差在繪製入口呼叫一次**（見下方提示）。在那之前，畫面不會變暗，也不會出現光源。
- 原版的 ±35／±15 欄位濾除與動作偏移（`0x138CCC8` 等表）沒有套用，因為欄位意義還未驗證。現在的濾除是「光源圖能不能碰到視窗」（推測，結果與畫面一致；但預算用完時保留哪些光源，可能與原版不同）。
- 視窗以自己的腳點為中心：原版玩家在 800×480 畫面上的固定位置還未查（推測）。
- 光源中心沒有加原版的 +24／+6 偏移，沿用既有的「腳點」語義（與 S87 光源一致）。
- 沒有啟動遊戲，也沒有實機比對畫面。

## 產出路徑

- `engine/source/Ambience.cs`、`engine/source/MapEffects.cs`、`engine/source/AmbienceLightsTests.cs`（新增）
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- `docs/reports/issue-566.md`

## 給下一棒的提示

- **接上畫面只需要改 `Program.cs`／`ObjectRendering.cs`**（另開一張 code 任務，檔案範圍列這兩個）：
  1. 在 `RefreshObjects()` 的 `renderer.ComposeObjects(...)` 之後呼叫 `renderer.ComposeAmbienceLights(login.World, objectVisuals);`
  2. 在 `PaintScene` 線上分支裡，`renderer.SceneView(objectPatch, area)` 取得 `scene` 之後、`DrawImage` 之前呼叫 `renderer.ShadeAmbience(scene, area, login.World.Ambience.At(now));`。`scene` 是 1:1 的場景點陣圖，不必換算 zoom。
  3. 非船艙的路徑（`Background` 加 `patch` 分開畫）不能直接處理，需要先改成和船艙一樣，用 `SceneView` 組成同一張點陣圖再處理。
- 天氣粒子（`AmbiencePainter.Draw` 的 weather 部分）也還沒有呼叫者，可以在同一張任務一起接。
- 盲目模式（`Afflictions.cs`）的光圈目前還是近似的圓；現在可以改用 `AmbienceLightImages` 第 5 張圖。
- 實機比對要等接上畫面之後，由 dot 依既有授權在 P24Lab 執行：夜晚或地城中，看 3442–3446 光源點與火堆周圍是否被照亮。
