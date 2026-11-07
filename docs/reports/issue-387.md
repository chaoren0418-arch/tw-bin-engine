# Issue #387 — T147 日夜、天氣與光源

## 結論

**模組與離線驗證已完成，實際畫面接線尚未完成，不可視為 T147 功能驗收通過。** 分支 `task/387`，已推送並建立[草稿 PR #400](https://github.com/chaoren0418-arch/tw-bin-engine/pull/400)，未切換分支或建立其他 worktree。

## 做了什麼

- 新增 `Ambience.cs`：S123 遊戲時刻與 6 倍視覺推進、日夜漸層、S115 三段雨雪粒子、S40 自身／物件光照；更新沿用 S87 Light，S120／換圖清除物件光源，Reset 清除環境狀態。
- `WorldSession.cs` 僅增加 S115／S40 各一個 case，S123 在既有船班／ready 邏輯後通知 Ambience，不更改船班規則。
- `Renderer.cs` 新增單一 `PaintAmbience` 繪製入口；不改地形、物件深度、選取 ownership buffer。
- 測試提交於 `engine/_integration/dev/ambience-tests/`，接入既有 `DemoChecks` 的 `ambience`、預設及 `map-continuity` 群組。Windows CI 原有 map-continuity 步驟會執行本模組。
- 更新 dev `rebuild-lock.json`；未動 `docs/REPORT.md`、封存 lock、releases 或遊戲資源。

## 怎麼驗證（已驗證）

1. `rebuild.py --update-lock`：185 來源、194 鎖定輸入通過。
2. `rebuild.py --build --configfile scratch/issue-387/NuGet.Config --output scratch/issue-387/build`：產品、音訊及 launcher 建置成功。SDK 10.0.300；產品有既有 `MagicUi.cs:20` CS8600／CS8602 兩項警告，無錯誤。NuGet 僅唯讀使用原專案 vendor，建置與暫存位於本 worktree；未執行產品。
3. `DemoChecks ... ambience`：**356 項通過**；涵蓋每個本模組 S 格式、全部天氣碼、截斷／padding、時刻邊界、船班與 ready probe、光源生命週期、相機位置與縮放、GDI 像素效果與 clip 保護。沒有新增 C 格式。
4. `DemoChecks ... map-continuity`：**714 項通過**，包含原有跨圖／船運／旅館／地圖效果／公告板及環境測試。
5. `check-no-game-assets.py`：通過，0 errors。
6. `git -c core.whitespace=cr-at-eol diff --check`：通過；既有 Renderer／WorldSession 的 CRLF 位元組格式保留。
7. 程式提交 `983bde6c0ff04c8323e1f0e221cc7ab263f6d28d` 的 CI 全綠：[build #532](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42329561655)（Linux dotnet、Windows map-continuity）與 [npc-offline #401](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42329561372)。包含本模組實際執行及既有 WebView2 回歸。

本機產品 DLL SHA-256：`b66b009a299253e52eca95c06401b988ccdf0f7146480a07cbfd6625d1784c4a`。後續報告提交只補驗證連結，不改程式／測試／lock。

離線輸出：`scratch/issue-387/ambience.json`、`map-continuity.json`、`build/result.json`。測試程式已入 repo；上述一次性結果留 scratch，不冒充真機證據。

## 未驗證／阻塞

- **實際畫面掛載點不在允許檔案內**：`Program.cs` 的 `ClientForm.OnPaint` 直接畫 Background／patch，船上另走 SceneView 並提早 return；`OpacityRendering.cs` 只有 SceneView。`Renderer.cs` 沒有最終畫面繪製呼叫點。只新增入口不會讓環境效果出現在遊戲畫面。
- 已在本次對話請求允許 `Program.cs` 增加單一環境層掛載點；尚未取得答覆，沒有越過明列的檔案範圍。需要在兩條世界畫面路徑完成後，以 `Ambience.At(SceneClock())`、`AmbienceLights(Camera.ToView, Camera.Zoom, renderer.OwnCombatVisual, objectVisuals)` 呼叫 `renderer.PaintAmbience`，並保護 HUD clip。
- 未啟動遊戲、伺服器或 DB，未做真機畫面／效能驗收。
- 雨雪代碼的兩份伺服器註解相反；目前依 `S_Weather` 類別，需 smoke 核對。光圈像素比例、日夜漸層與粒子造型為引擎政策，不宣稱逐像素重現原 BIN。

## 產出路徑

- `engine/source/Ambience.cs`
- `engine/source/WorldSession.cs`、`engine/source/Renderer.cs` 最小掛接
- `engine/_integration/dev/ambience-tests/AmbienceChecks.cs`、`README.md`
- `engine/_integration/dev/demo-tests/{DemoChecks.csproj,Program.cs}`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/ambience.md`：封包規則、來源 SHA-256、視覺政策及矛盾
- `docs/reports/issue-387.md`

## 給下一棒的提示

由調度器／Claude 裁定畫面掛載檔案範圍，再交 codex-local 補接 `Program.cs`；在接線完成前保留草稿 PR，不應合併或關閉 #387。接線後重新更新 lock、建置、跑 ambience／map-continuity 與 CI。最後由 dot 依 P24Lab 授權 smoke 核對雨雪類別、夜晚明暗、火把／照明法術、移動／換圖／重登及船上視圖；不連正式服務。
