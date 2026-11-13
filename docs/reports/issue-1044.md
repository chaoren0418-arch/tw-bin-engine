# Issue #1044 — T459 產品規則：motion 與混色查表改為程式計算

日期：2026-11-13。執行者：本機 Claude（claude-local），分支 `task/1044`。依據：T448（#1033）`docs/reference/asset-recipes/motion-blend.md`。

## 做了什麼

1. **混色查表改為程式計算**（新檔 `engine/source/BlendRules.cs`）
   - 三個 5-bit 色版算式（T448 窮舉驗證過、與基線逐 byte 相同）：
     - screen：`hi + (31-hi)*lo/31`（`hi=max`、`lo=min`；整數式，與原碼 double `trunc(((31.0-hi)/31.0)*lo+hi)` 1,024 格全同）
     - multiply：`a*b/31`
     - half：`(a+b)>>1`
   - 三張 32×32 表在型別初始化時（啟動時）用兩層 0–31 迴圈算出，沒有把表寫成常數陣列。
   - 檔案版契約保留成 API：`ChannelFileBytes(kind)` 產出 2,048 bytes LE `u16`（準備器若仍要寫 `cache/*-channel.bin` 可直接用）、`ParseChannelFile(bytes)` 讀舊檔（同樣只接受 2,048 bytes）、`Compose(table,a,b)` 以任一張表逐 RGB555 色版查。
   - `Renderer.cs:33–34`：刪掉讀 `assets/cache/screen-channel.bin` 的 `Lazy`，`ScreenBlend` 改成 `BlendRules.Compose(ChannelBlend.Screen,…)`。函式簽章不變，呼叫端（`Renderer.cs` 混色、`CacheTests.cs`）不必改。
   - `MultiplyBlend`、`Blend` 原本就是算式，沒改；測試證明它們等於 multiply／half 表逐色版查。

2. **motion.json 改為產品規則**（新檔 `engine/source/MotionRules.cs`）
   - `Directions()`：由 `MovementRules.Dx／Dy` 產生，`Sx=24*(dx+dy)`、`Sy=12*(dy-dx)`。
   - `FrameMs = 4 tick × 1000 / 24 = 166`（list.spr `#3225` walk）。
   - `Spawn = (32588, 32928)`：註明是 P06 研究時選的離線預覽起點，不是原客戶端資料。
   - `Assets.cs:53–55`：不再讀 `motion.json`，改成 `MotionRules`。`Assets.Directions／FrameMs／Spawn` 的型別與值都不變，`World.cs`、`Program.cs`、`Tests.cs` 不必改。

3. **測試**（新檔 `engine/_integration/dev/motion-blend-tests/MotionBlendChecks.cs`，`[CheckGroup("motion-blend")]`，另掛 CI 已在跑的 `native-ui-resources`（Order 62）與 Default；沒有改 `build.yml`、`Program.cs`、`DemoChecks.csproj`）
   - 算式：三張表窮舉 1,024 格的對稱性、值域 0–31、單調性；邊界值（`screen(0,x)=x`、`screen(31,x)=31`、`multiply(0,x)=0`、`multiply(31,x)=x`、`half(x,x)=x`、`half(0,31)=15` 等）；`screen ≥ max`、`multiply ≤ min`、`half` 介於兩者之間；超出 0–31 丟例外。
   - screen 整數式與原碼 double 截斷式全同、與 `(31*(a+b)-a*b)/31` 全同、與錯誤式 `31-floor(...)` 恰好 900 格不同（重現 T448 結論）。
   - 與檔案版的介面相容：`ChannelFileBytes` 的 SHA-256 等於 r29 基線三個檔（`9F394F6F…`、`4E2A67F5…`、`52F925E7…`）；檔案往返等於算出的表；舊格式長度錯誤會拒絕；`Renderer.ScreenBlend／MultiplyBlend／Blend` 在每個色版窮舉 32×32（另兩色版放不同值）加 20,000 組亂數 RGB555 下，都等於用檔案版表逐色版查；bit 15 被忽略。本機有 `engine/assets` 時另做逐 byte 比對（CI 沒有素材，自動跳過）。
   - motion：八方向等於 T448 表、來自 `MovementRules`、畫面位移可整除 4、相反方向互為負、封閉式 `dx=sgn(sin)`、`dy=-sgn(cos)`；`FrameMs=166`；Spawn 常數。用合成最小素材樹建 `Assets`：沒有 `motion.json` 能載入；放一份錯誤的 `motion.json` 也會被忽略；`World` 起點等於 Spawn。本機有基線 `motion.json` 時比對產品欄位與 `internal_delta=(2dx,dy)`。
   - 夾具輸出寫在 `scratch/issue-1044/run-<時間>/`，不遞迴刪除。

4. **既有測試的連動**：`DoorChecks.cs` 的門渲染夾具原本寫一份 `spawn=(32600,32940)` 的 `motion.json`，讓 `own` 角色出生在門旁。改成不寫 `motion.json`，建 `World` 後 `own.Teleport(32600,32940)`，維持原本的位置與測試語義。

## 怎麼驗證

- 本機只做編譯（依 2026-11-09 本機限制，沒有執行任何編譯出的程式）：
  - `dotnet build engine/source/OfflineClient.csproj -c Release`：0 錯誤（3 個既有警告，與本次無關）。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 錯誤。
- 執行測試交給 GitHub Actions：Windows job 的 `native-ui-resources` 步驟會跑 `MotionBlendChecks`，`doors` 步驟跑改過的門夾具；Linux job 交叉建置與 `rebuild.py --build`（不改 lock）。
- 沒有改 `engine/_integration/dev/rebuild-lock.json`。
- PR #1084 第一次 CI（run 43437984475）：`dotnet`、`map-continuity`、`npc-offline` 全部通過。`native-ui-resources` 步驟由 main 的 1,242 項增為 1,292 項 PASS（新增的 `MotionBlendChecks`），`doors` 步驟（改過的門夾具）通過。

## 未驗證什麼

- 本機沒有跑任何測試；結果以 PR 的 CI 為準。
- 產品 `--module-tests`／`Tests.cs` 的畫面與移動自我測試（使用真素材）CI 不跑，畫面相同交 T470／T474 真機或 Codex 本機驗證。
- 真機畫面（screen 混色的光影、角色移動）待驗證。

## 已知限制／沒做的事

- T448 沒有 blocked 項，所以沒有「不動的表」。
- `assets/cache/*-channel.bin` 與 `assets/motion.json` 仍在 r29 基線素材與 `integrity.json` 裡（唯讀 junction，不改）；產品已不讀它們。`engine/_integration/baseline-pins.json` 是封存 seal 的 pin 清單，沒有動。日後準備器若改交付清單（拿掉這四個檔），要一起從 integrity 清單拿掉；若仍要產出，用 `BlendRules.ChannelFileBytes`。
- `motion.json` 的研究用欄位（`schema`、`key`、`root_motion`、`source_va` 等）產品不需要，沒有做成規則；沒有提供逐 byte 重產 `motion.json` 的寫出器。
- 原碼同一函式另外產生的 32 級權重表與封頂相加表（T448 只記位置）不在本張範圍；產品的 `Renderer.Weighted` 沒有動。

## 產出路徑

- `engine/source/BlendRules.cs`（新）、`engine/source/MotionRules.cs`（新）
- `engine/source/Renderer.cs`、`engine/source/Assets.cs`（改接）
- `engine/_integration/dev/motion-blend-tests/MotionBlendChecks.cs`（新測試）
- `engine/_integration/dev/doors-tests/DoorChecks.cs`（夾具連動）
- 本報告 `docs/reports/issue-1044.md`

## 給下一棒的提示

- 若 CI 的 `native-ui-resources` 或 `doors` 步驟失敗，先看失敗訊息前綴：`MB` 是混色、`MO` 是 motion。
- 準備器（asset-prep recipe）若要接手 `cache/*-channel.bin`，直接呼叫 `BlendRules.ChannelFileBytes`；SHA 已由測試釘住。
- 真機驗收時留意：離線預覽按 R 回起點（`Program.cs:136`）的位置應不變；含 screen 混色的特效亮度應與 r29 相同。
