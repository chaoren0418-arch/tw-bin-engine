# Issue #1155：原生登入畫面 N01–N04 重測

結論：部分通過（✅ 3／❌ 1／⚠️ 0）。N03 在登入畫面按 Ctrl+P 未產生截圖；未修改引擎或測試工具，未重跑。

## 做了什麼／已驗證

2026-11-14 07:49:42–07:50:00（台北時間）在 `task/1155` 執行一次指定腳本，來源 `6f7051fb27f60b32b2cd3eca7b008d6e5fcf9e64` 與 fetch 後 `origin/main` 相同。只操作登入畫面，輸入固定測試字串 `abc`，沒有登入、密碼、DB 操作或伺服器啟停。

下表證據 `E/` 代表本 worktree 的 `scratch/issue-1155/`；`P/` 代表 `scratch/issue-1155-prep/`。可攜摘要為 [issue-1155-summary.json](issue-1155-summary.json)，包含來源與原始證據 SHA-256、native-result 重點、完整 10 筆輸入摘要及前後置紀錄。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| N01 | ✅ 通過 | 兩張均 `Method=PrintWindow`、800×600，等於工作區，非全黑／全白。目視首張為核對期間離線場景，接手後為原生登入畫面；11 幀、時間差 5,021 ms，關窗事件順序為 verifying→active。 | 指定腳本每約 0.5 秒擷取；目視兩張原始 PNG 並核對事件。 | `E/n01-verifying.png`、`E/n01-active.png`、`E/native-result.json`、`E/window-events.json` |
| N02 | ✅ 通過 | **點擊**：(375,374) 落在 Account 欄與指定矩形內，HitRootHwnd 等於目標；SendInput 3/3。**輸入**：before 空白，after 清楚顯示 `abc`，密碼欄仍空白；帳號矩形內差異 108 像素，門檻 30。 | 同次腳本點帳號欄，以 VirtualKey 送 a/b/c，逐張目視 before／after。 | `E/n02-before.png`、`E/n02-after.png`、`E/client-input.jsonl`、`E/native-result.json` |
| N03 | ❌ 失敗 | Ctrl+P 三批 SendInput 均成功，3 秒內 `engine/runtime/Capture` 無新檔；事後該目錄不存在，所以無新檔名／修改時間可列。 | 同次腳本在登入畫面送 Ctrl+P，記錄送出時間並輪詢新檔。 | `E/native-result.json` 的 N03、`E/client-input.jsonl`、`P/postflight.json` |
| N04 | ✅ 通過 | Alt+F4 正常結束；`closed` 存在；`ForcedClose=false`、自啟 4 個 PID 全消失、兩埠無監聽。腳本無帳密；本輪因 N03 失敗，實際與 JSON 退出碼皆為 1，符合失敗非 0。 | 同次腳本 finally 收尾；外層立即記錄 `$LASTEXITCODE`，再唯讀核對程序／監聽。 | `E/window-events.json`、`E/native-result.json`、`P/execution.json`、`P/postflight.json` |

N04 的「全部成功回 0」分支只經靜態閱讀確認，這次沒有四項全成功，因此**未動態驗證 exit 0**；不將失敗的本輪宣稱為成功回 0，也不為此額外重跑。

## 怎麼驗證／前置與建置

- 前置 1：main 包含 #1154 的 `Test-NativeClient.ps1` 及四個指定函式；前置 2：GitHub #1149 為 closed／completed，報告 S02 ✅。沒有重跑記事本。
- 資源鎖：第一次唯讀查詢一度顯示 #1150，依任務守則記錄後繼續建置。實測前 07:49:39 的 `P/preflight.json` 已為 `p24lab`／`chaoren0418-arch/tw-bin-engine#1155`，`exp=1794619047.5155306`（2026-11-14 01:17:27.515 UTC），與派工一致至秒，涵蓋整次測試。
- `query session`：`>console CHAOREN 6 Active`；退出碼 1，但有工作階段輸出，僅記錄。前景 HWND=132132、PID=4716、ProcessName=`claude`；開始前沒有 `P11.ClientEngine.exe`。執行中未另外打字或切換視窗。
- 執行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File engine/build-product.ps1`，退出碼 0；product 4 個既有編譯警告、0 錯誤，launcher 成功。使用標準 temporary-lock 模式，未改 `rebuild-lock.json` 或 receipt。
- 在 worktree 建立 `engine/assets` junction 指向主專案 `releases/r29/assets`，`engine/vendor` junction 指向主專案 `engine/vendor`；只讀取兩個目標。NuGet 寫入 worktree 的 `engine/.nuget`，呼叫端設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`，TEMP／TMP 在本 worktree scratch。沒有複製 pak/map/sprite、移除 junction 或遞迴刪除。
- 根目錄 `啟動引擎.cmd --check-only` 回 2：worktree 沒有封存 r33 launcher。依 smoke-m7 §0.2／§0.3 使用 `engine/啟動新版客戶端.cmd --check-only`，回 0、`status=checked-only`、`revision=dev`、`assetState=legacy-ready`，素材存在／尺寸檢查 13,957 個；Desktop Runtime 10.0.8、WebView2 157.0.4341.46。沒有為了根入口複製或修改封存。
- Product DLL SHA-256：`1642ea357d5300fcdac8f5b40e6595ba3504c17aa10b830d8fd6816041726e93`；launcher DLL：`328d48685f12e45a94c84bfc2031575e4847ec649d0cde51e4bf81e520ae2435`。
- 標準建置腳本的原始輸出位於 `engine/_integration/evidence/dev-20261113T234841/`，已將檔案副本集中至 `P/rebuild/`；外層建置 log 為 `P/build.log`。全部未進 git。

唯一一次實測命令（`P/run-once.ps1` 只補前置紀錄、環境、stdout／stderr 與退出碼）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-NativeClient.ps1 -EvidenceDirectory scratch/issue-1155
```

## native-result／JSONL 重點

- 目標 `P24 客戶端`：PID=1660、HWND=2296378，工作區螢幕矩形 `(1520,762,800,600)`，DPI=192、整數倍率 1；程序 DPI context 設定成功。4 張圖均 PrintWindow，PrintWindowError／BitBltError 為 null，沒有使用 BitBlt 或其他擷取方法。
- N01 首張平均強度 113.446，NonBlackRatio=0.999765、NonWhiteRatio=0.999790；接手後平均強度 82.595，兩比率 0.977133／0.982446。畫面變化比例 0.956594；5,021 ms 是輪詢與存圖的時間差，並非精確接手時刻。首張沒有「核對中」文字，離線場景與稍後讀回的 verifying→active 事件共同作為階段證據。
- N02 帳號邏輯／工作區矩形 `(320,364,110,20)`；點擊工作區 `(375,374)`、螢幕 `(1895,1136)`、正規化 `(32349,34483)`。畫面實際欄位比指定內框寬，但點擊位於二者交集；不是僅以 `abc` 推論點擊正確。
- 輸入 JSONL 共 10 行：點擊 1、文字 3、Ctrl+P 3、Alt+F4 3。每行 SendInputReturned 等於 InputCount、LastError=0、InputSize=40、前景 PID=1660。點擊前前景 HWND=14943838（同 PID），文字與後續按鍵前景均為 2296378；點擊命中根視窗是 2296378。
- N02 全圖差異 1,626 像素，其中帳號內 108；其餘含動態背景，不能宣稱所有變化都只在帳號內。目視 `abc` 是主要輸入判據。
- N03 `KeySentUtc=2026-11-13T23:49:55.9631974Z`、`NewFiles=[]`。唯一錯誤原文（完整路徑保留於摘要 JSON）：`N03: no new file under D:\lineage開發\TW1608262200研究\scratch\wt\task-1155\engine\runtime\Capture within 3 s after Ctrl+P; key error: `。
- N04 自啟 PID：cmd 13956、conhost 17104、launcher 7368、client 1660，皆已退出；07:50:31 再查無本 worktree 的客戶端／launcher／WebView 程序、42061／43324 無監聽。沒有強制結束既有程序。
- `client-network.jsonl` 只有關窗的 `Disconnected/form closing` 與 `form-closed` 兩筆；`login-state.json` 為 Cancelled、profile=none、appliedPacketSequence=0、world.entered=false、renderable=false。未啟動伺服器、未送登入或連正式服務。

## PNG 目視與 SHA-256

| 檔案（均在 E/） | 目視結論 | SHA-256 |
|---|---|---|
| `n01-verifying.png` | 核對期離線房舍／地面場景，非黑白空圖。 | `c1ae3f16cc7405e2e178d477c551a6943fa7864d1b7f2fe541692cae20996947` |
| `n01-active.png` | 原生登入背景、Lineage 標誌、Account／Password／Login／Exit。 | `42d9c5853943bf4c63d787626a92a2b39532e8f62264216835c5f2cea2378816` |
| `n02-before.png` | 登入畫面，帳號與密碼空白。 | `705bd9547cd8e8915cc8597e8e64d0f9b3c6b0fd6a9f7a292b84dd7cafc92600` |
| `n02-after.png` | Account 欄清楚可見 `abc`，密碼仍空白。 | `12b084265ad43e304d24ffa17d6be8217dff6ac6f777c354da7682190abc1b68` |

## 沿用／推測／待驗證

| 項目 | 處理 |
|---|---|
| #1145 前置 #1139 已結案（後由 #1150 取代） | 沿用，與本輪無關。 |
| #1126 D01、#1118 S01、#1125 R03–R06／R12／R13／R17 | 沿用，不回歸；未試 WGC、computer-use 或 @oai/sky。 |
| #1149 S02／S02-GDI／S02-CLOSE／P01 | 沿用 ✅，未重測記事本。 |

推測：N03 是引擎登入階段不處理截圖熱鍵，與工具文件的預期一致。靜態依據為 `engine/source/NativeUi/MenuButtonsPanel.cs:276` 的 `if(!inWorld)return false;`；實證為 Ctrl+P 注入成功而無檔。尚未加入程式內熱鍵追蹤，故不宣稱已完整證明呼叫鏈根因。

待驗證：登入畫面 Ctrl+P 產檔，以及後續全部成功時 exit 0；未驗證登入、選角、世界功能或其他未列入項目。沒有點擊／文字無反應，也沒有兩條 GDI 路徑皆全黑，因此未觸發 `needs:local` 停止條件。

## 產出路徑／給下一棒的提示

只提交 `docs/reports/issue-1155.md` 與 `docs/reports/issue-1155-summary.json`；PNG／log／原始 JSON 在上述 scratch 證據目錄。`docs/REPORT.md`、lock、引擎、測試工具皆未修改。

請調度器／Claude 將 **N03** 分流成引擎 code 任務，使登入畫面也能使用內建截圖，再只重測 N03；修正後若會影響 N04 的整體退出碼，附帶驗證成功回 0。N01／N02 與關閉收尾實證沿用，不重跑整張清單。不要為通過 N03 而放寬本張「不登入」範圍。

本 worktree 未移除；調度器清理前請先保留所需原始 PNG／log。報告與摘要中的雜湊可核對原始證據，但不替代 PNG 目視。
