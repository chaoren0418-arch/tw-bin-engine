# Issue #1185：登入畫面 Ctrl+P 與成功退出碼重測

結論：通過（本輪 ✅ 2／❌ 0／⚠️ 0）。#1184 明確採 **B**，N03 依 `login-negative` 反向核對；N04 已動態驗證全部成功回 0。只執行一次，未修改引擎或測試工具。

## 做了什麼／已驗證

2026-11-14 13:32:40–13:32:59（台北時間）於 `task/1185` 執行指定腳本。建置與實測來源 `64c3b446fddade55a1df7e4f1db8ca4b4cd3a880` 等於 fetch 後 `origin/main`，已含 #1184／PR #1189。只操作登入畫面，固定輸入 `abc`，未登入、送密碼、寫 DB、設 GM 或啟動 42061／43324；未碰正式 2000／3306。

證據 `E/`＝本 worktree `scratch/issue-1185/`；`P/`＝`scratch/issue-1185-prep/`。可攜摘要見 [issue-1185-summary.json](issue-1185-summary.json)，含來源／二進位／原始證據 SHA-256、原始 N03／N04 欄位、JSONL 摘要及前後置紀錄。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| N03 | ✅ 通過 | Ctrl+P 送出無錯誤，完整等 3 秒無新檔，客戶端仍在；前後 Account 都只有 `abc`、Password 空白。帳號矩形差異 0 像素，小於門檻 30。 | 同次腳本於登入畫面送 Ctrl+P，完整輪詢 3 秒；比較並目視 n02-after／n03-after。 | `E/native-result.json`、`E/client-input.jsonl`、`E/n02-after.png`、`E/n03-after.png` |
| N04（成功回 0） | ✅ 通過 | 外層立即記錄 `$LASTEXITCODE=0`，JSON `ExitCode=0`，`Errors=[]`，腳本 N01–N04 機器欄位均 true。 | 指定腳本執行一次；子程序返回後第一個陳述式保存退出碼，再讀 native-result。 | `P/run-once.ps1`、`P/execution.json`、`E/native-result.json` |
| N01 | ✅ 沿用 #1155 | 本輪腳本自動產圖只存證，不重新判定。 | 不另跑；沿用 #1155 的 PrintWindow／5,021 ms 證據。 | `docs/reports/issue-1155.md`；本輪 `E/n01-verifying.png`、`E/n01-active.png` |
| N02 | ✅ 沿用 #1155 | 採 B 未改引擎；本輪 n02-after 僅作 N03 比對基準，不另列回歸結果。 | 不另跑點擊／輸入測試；只使用指定腳本所需基準。 | `docs/reports/issue-1155.md`、`E/n02-after.png` |
| N04（關閉部分） | ✅ 沿用並記錄 | 本輪 Alt+F4 正常退出，有 closed，未強制關閉，自啟 4 PID 全收，兩測試埠無監聽。 | 同次腳本 finally 收尾，外層再查所記錄 PID／監聽。 | `E/window-events.json`、`E/native-result.json`、`P/postflight.json` |
| #1149 S02／S02-GDI／S02-CLOSE／P01 | ✅ 沿用 | 不重測。 | 沿用原結果。 | `docs/reports/issue-1149.md` |
| #1126 D01、#1118 S01、#1125 R03–R06／R12／R13／R17 | 沿用原結果 | 未試 WGC、computer-use、@oai/sky，未延伸其他驗收。 | 沿用原結果，不計本輪統計。 | 對應原 Issue 報告 |

## 怎麼驗證／前置

- 讀取 AGENTS.md、docs/REPORT.md、#1155／#1160／#1184 報告；#1184 的 B 與通過條件清楚，未觸發 needs:orchestrator。
- 13:32:37 查得 lease 為 `p24lab`／`chaoren0418-arch/tw-bin-engine#1185`，`exp=1794639634.1046112`（2026-11-14 07:00:34.104611 UTC），符合派工且涵蓋實測。
- `query session`：`>console CHAOREN 6 Active`（退出碼 1，但有輸出，僅記錄）；前景 HWND=132132、PID=4716、程序 claude。實測前無 P11.ClientEngine，兩測試埠無監聽；執行期間未額外打字或切視窗。
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File engine/build-product.ps1` 回 0，temporary-lock 模式；product／launcher restore 與 build 皆回 0。未改 rebuild-lock.json 或 receipt。原始建置紀錄在 `engine/_integration/evidence/dev-20261114T053133/`，外層 log 在 `P/build.log`。
- worktree 內建立 `engine/assets` junction 指向原專案 `releases/r29/assets`、`engine/vendor` 指向原專案 `engine/vendor`，僅讀目標；NuGet／TEMP／TMP 寫於 worktree，設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`。沒有複製 pak/map/sprite、寫入原始專案或遞迴刪除。
- `engine/啟動新版客戶端.cmd --check-only` 回 0，`checked-only`／`dev`／`legacy-ready`，素材存在／尺寸檢查 13,957 個。Desktop Runtime 10.0.8、WebView2 157.0.4341.46。根目錄 r33 入口未重跑，沿用 #1155 的 worktree 缺封存 launcher 說明。
- Product DLL SHA-256：`58a017171ccd02a63a5003bc6b1a8fc8d21d47e4dad7f287f062420de9fddd24`；launcher DLL：`daf00174a308da706500ccd00c5596d6c8eca81a613e1b6197afd49cba319a2a`。

唯一一次實測命令（外層 `P/run-once.ps1` 僅補環境、前後置紀錄與立即保存退出碼）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File work/f1118-uia-path/Test-NativeClient.ps1 -EvidenceDirectory scratch/issue-1185
```

## native-result／JSONL 重點

- 客戶端 PID=15396、HWND=7276584；N03 PrintWindow 為 800×600，工作區螢幕矩形 `(1520,762,800,600)`，DPI=192、整數倍率 1，PrintWindowError／BitBltError 均 null，未用其他擷取方法。
- `N03.Mode=login-negative`、`KeyError=null`、`NewFiles=[]`、`ClientRunning=true`、`AccountBoxUnchanged=true`。送鍵時間 `2026-11-14T05:32:54.6516729Z`。完整等候 3 秒後截圖，再於 05:32:58.003 UTC 開始 Alt+F4；收尾時 Capture 目錄不存在，無新檔名／修改時間可列。
- N03 比較矩形 `(320,364,110,20)`，差異門檻 48；`ChangedInsideAccountRect=0`、`AllowedInside=30`、`ChangedTotal=379`。全圖 BoundingBox 是 `[11,97,766,584]`，不是帳號欄差異；判定使用帳號內像素與目視。
- 輸入 JSONL 共 10 行：點擊 1、文字 3、Ctrl+P 3、Alt+F4 3。每行 SendInputReturned 等於 InputCount、LastError=0、InputSize=40，前景 PID 都是 15396。Ctrl+P 三批為 1/1、2/2、1/1，前景 HWND 都是 7276584。`ForegroundAcquired=false` 僅表示當下未另取前景；實際前景識別已符合目標。
- 自啟 PID：cmd 13160、conhost 14592、launcher 18972、client 15396，全已退出。`ExitedAfterAltF4=true`、`WindowEventsClosed=true`、`ForcedClose=false`、`RemainingOwned=[]`、`OwnedListeners=0`；外層再查亦無 PID／兩埠監聽。
- network JSONL 只有 Disconnected／form closing 與 form-closed 兩筆；login-state 為 Cancelled、profile=none、appliedPacketSequence=0、world.entered=false、renderable=false。沒有登入與世界操作。

## PNG 目視與 SHA-256

下表 PNG 均在 E/；N01 圖只存證，不重新判定。

| 檔名 | 目視／處理 | SHA-256 |
|---|---|---|
| `n01-active.png` | 指定腳本自動存證，本輪未重新目視判定此沿用項。 | `32bedff9d2f2cf138e7e84acefa21095dc67a3f5a61700e7c8e7bdfa53b3c5de` |
| `n01-verifying.png` | 指定腳本自動存證，本輪未重新目視判定此沿用項。 | `c1ae3f16cc7405e2e178d477c551a6943fa7864d1b7f2fe541692cae20996947` |
| `n02-after.png` | N03 比對基準：原生登入畫面 Account 僅 abc，Password 空白。N02 判定沿用 #1155。 | `7efb48ee12bd0e5f8000affefd55aad5d22fa9478341219326b772665cbfb413` |
| `n02-before.png` | 指定腳本自動存證，本輪未重新目視判定此沿用項。 | `c2f90df1f013b85771dd1be91bf7606e7c5cbf8b6bc9392ae10032f58c34485d` |
| `n03-after.png` | Ctrl+P 後仍為登入畫面，Account 僅 abc，沒有多出字元，Password 空白。 | `2c81d95c011cbc1da8eb7fef175da293a6240d81fd8d5c2a24d12bb4c75a6d6e` |

## 推測／待驗證

推測：N03 全圖其餘 379 像素差異來自登入背景動畫；本輪沒有逐像素追來源，不將此推測作為通過條件。原版按鍵路由依 #1184 靜態查證，本輪未另執行原 BIN。

本張範圍無待驗證項目。世界內 Ctrl+P 實際產檔、登入／選角／世界功能仍不在本張範圍，交既定 `f1125-retest-3`／SM15。未發生點擊／輸入無反應或 GDI 兩路全黑，沒有觸發 needs:local。

## 產出路徑／給下一棒的提示

只提交 `docs/reports/issue-1185.md` 與 `docs/reports/issue-1185-summary.json`；PNG／log／原始 JSON 留在上述 scratch，未進 git。docs/REPORT.md、引擎、測試工具、lock 均未改。

請調度器／Claude 審查並接續 `f1125-retest-3`；N03 登入反向核對與 N04 成功回 0 均已通過，無須重測本張。世界內 Ctrl+P 的 SM15 必須另證實產檔。worktree 與 junction 保留，清理前請先保留所需原始證據。
