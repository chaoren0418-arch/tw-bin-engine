# Issue #751 T317：原生 UI 改為預設開啟（使用者 #743 決定 1）

依據：使用者 2026-11-10 07:58 裁定（#743 決定 1）「預設就使用原生的 UI」。

## 做了什麼

### 改了哪些預設
| 項目 | 之前 | 之後 |
|---|---|---|
| 正常產品入口（`--normal-product` 或不帶參數）沒有設定檔時 | 舊 HUD（原生 UI 關閉） | **原生 UI 開啟** |
| 診斷／測試入口（`--demo`、`--evidence`、`--login-demo` 等） | 舊 HUD | 舊 HUD（不變；`--native-ui` 仍可打開） |
| `--native-ui` | 開啟 | 開啟（相容參數，效果與預設相同，不報錯） |
| `--classic-ui`（新增） | — | 關閉，切回舊 WinForms HUD；與 `--native-ui` 同時給時優先 |
| 設定檔 `runtime/native-ui.json` `{"nativeUi":false}` | 等同預設（關） | **明確關閉**（launcher 只傳 `--normal-product`，這是從 launcher 切回舊 HUD 的方法） |
| 設定檔格式錯誤／值不是 JSON 布林 | 視為關閉 | 忽略，沿用預設 |

### 檔案
- `engine/source/NativeUi/UiRoot.cs`：`UiOptions` 新增 `ClassicArgument`（`--classic-ui`）、`Classic`、`DefaultOn`（預設 `true`）、`On`；新增四參數 `Resolve(commandLine, classic, settingsPath, defaultOn)`，舊的兩參數版改為讀靜態旗標後轉呼叫（`UiClientBridge.InitNativeUi` 不用改）。`Strip` 同時移除兩個開關參數。
- `engine/source/ProductEntry.cs`：新增 `NativeUiDefault(bool normalProduct)`（正常產品 → 原生 UI；診斷入口 → 舊 HUD）。
- `engine/source/Program.cs`：只加一行，在判斷完正常入口後設定 `UiOptions.DefaultOn`；`Strip` 註解更新。
- `docs/design/native-ui.md` §1.1：改寫功能開關（優先順序、相容參數、launcher 的切換方法）。
- `docs/BUILD.md`：check-only 一節後新增「介面開關」小節（啟動說明；`RUN.md` 不存在，README 沒有提到 `--native-ui`）。

### 測試
- 新檔 `engine/_integration/dev/native-ui-tests/NativeUiDefaultTests.cs`：`[CheckGroup("native-ui-default")]`，同時掛在 `native-ui-canvas`（CI 會跑的群組）與 Default。依 `Program.Main` 的順序（`Strip` → `ProductEntry.Normal` → `DefaultOn` → `Resolve`）跑三種情況：
  - 預設（`[--normal-product]`、`[]`）：正常入口、原生 UI 開、`Source=default`、面板註冊不變。
  - `--native-ui`（單獨、在 `--normal-product` 前或後）：仍是正常入口、不報錯、開啟、與預設同開關、面板註冊不變。
  - `--classic-ui`（單獨、搭配 `--normal-product`、與 `--native-ui` 同給）：仍是正常入口、關閉、面板註冊不變。
  - 另驗：`--classic-ui` 蓋過設定檔 true；設定檔 false 可關掉預設；`--native-ui` 蓋過設定檔 false；診斷入口預設關、加 `--native-ui` 開；`--normal-product` 搭真正的診斷參數仍被拒絕；面板註冊表無錯誤且含 LEFT_WINDOW／CENTER_WINDOW／選單鈕／小地圖。
- **因新預設而更新的既有測試**（沒有刪除任何檢查）：`engine/_integration/dev/native-ui-tests/CanvasChecks.cs` 開關區塊。
  - 「default off (no settings file)」→「T317: default on」，並新增「diagnostic default (DefaultOn=false) stays off」。
  - 「non-boolean value counts as off」「malformed settings counts as off」→ 改為「忽略、沿用預設」，兩種預設各驗一次。
  - 新增「settings nativeUi=false → off even though the default is on」。
  - 全部改用四參數 `Resolve`，避免程序的靜態旗標影響結果；`finally` 一併還原 `UiOptions.Classic`。
  - 「switch off: ClientForm branches are inert」區塊不用改（未初始化的表單狀態仍是 Off）。

## 怎麼驗證（已驗證）
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/nuget-751.config`：PASS（暫時 lock，未寫入 `rebuild-lock.json`；config 只是把 restore 來源指到原專案的離線 `engine/vendor`，因為 worktree 沒有 vendor）。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 錯誤。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`。
- 依本機規定**沒有在本機執行** DemoChecks 與產品；離線測試結果以 PR 的 CI（`native-ui-canvas` 群組）為準。

## 未驗證什麼（待驗證）
- 離線測試實際執行結果：等 CI。
- 真機：預設開啟原生 UI 的正常啟動、`--classic-ui` 回舊 HUD、launcher 搭設定檔 `{"nativeUi":false}`——依使用者裁定由 dot 在 #475 smoke 執行（Codex 額度 11/13 18:11 後）。

## 產出路徑
- 程式：`engine/source/NativeUi/UiRoot.cs`、`engine/source/ProductEntry.cs`、`engine/source/Program.cs`
- 測試：`engine/_integration/dev/native-ui-tests/NativeUiDefaultTests.cs`、`engine/_integration/dev/native-ui-tests/CanvasChecks.cs`
- 文件：`docs/design/native-ui.md`、`docs/BUILD.md`、本報告
- 分支：`task/751`

## 給下一棒的提示
- 範圍外但已過時的文字（沒改，建議另開小任務）：`engine/source/NativeUi/UiClientBridge.cs` 檔頭註解「Switch off (default)」；`BookmarkUi.cs`／`BuddyUi.cs`／`PersonalShopIntegration.cs` 的提示「請以 --native-ui 啟動」——預設已開，這些提示現在只會在 `--classic-ui` 或原生 UI 資源核對失敗時出現，措辭可改為「原生 UI 未啟用（--classic-ui 或資源核對失敗）」。
- `PLAN.md` 第 90 行仍寫「新 UI 預設關閉」，由 Claude 維護 PLAN 時更新。
- 原生 UI 啟動時會在背景核對客戶端資源 SHA-256（數 GB）；核對完成前仍顯示舊 HUD，失敗就退回舊 HUD 加一行紅字。#475 smoke 請留意第一次啟動的核對時間。
- 診斷入口刻意維持舊 HUD 預設；若之後要讓 `--demo`／`--login-demo` 等工具也預設原生 UI，只需改 `ProductEntry.NativeUiDefault`。
