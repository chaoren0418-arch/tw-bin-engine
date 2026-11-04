# Issue #6 — T004 docs/BUILD.md 乾淨機器從零建置文件

階段：code。執行時間：2026-11-04。分支：task/6。

## 做了什麼

- 已讀專案根 AGENTS.md、CLAUDE.md、docs/REPORT.md；遵守不碰 releases／封存／機密、不複製 pak/map/sprite、不啟動遊戲、不連正式 DB。
- 依 T004／Issue #6 指定依據讀了 `engine/README.md`、`engine/_integration/r35/rebuild.py`、`.github/workflows/build.yml`、`PLAN.md`，並交叉核對 `engine/global.json`、`engine/build-product.ps1`、`啟動引擎.cmd`、`engine/啟動新版客戶端.cmd`、兩邊 NuGet 設定、r35 launcher。
- 新建 `docs/BUILD.md`：SDK／Python 3.12／WebView2、資源目錄約定、restore／`build-product.ps1`／`啟動引擎.cmd --check-only`、r35 `rebuild.py`（含 `--build`）、CI 與 nuget.ci.config vs 本機 vendor、腳本裡實際會出現的錯誤訊息。
- 依調度器要求更新 `docs/REPORT.md` 本輪變更／下一步，並寫本報告。未改引擎程式、lock、CI、releases。

## 怎麼驗證／已驗證

- 文件列出的路徑全部存在於 repo（見下方清單）。
- 文件內每條指令字串都對得上現有檔案（workflow、腳本、rebuild.py）。
- 本環境執行唯讀核 pin（未加 `--build`）：

```text
python -B -X utf8 engine/_integration/r35/rebuild.py
```

stdout：`{"status": "r35-inputs-verified", "sourceFiles": 181, "pinnedFiles": 190, "writes": false}`，退出碼 0。

核對過的路徑：`engine/global.json`、`engine/build-product.ps1`、`engine/_integration/r35/{rebuild.py,product-build.ps1,rebuild-lock.json,launcher-source/Program.cs}`、`engine/NuGet.Config`、`.github/nuget.ci.config`、`.github/workflows/build.yml`、`啟動引擎.cmd`、`engine/啟動新版客戶端.cmd`、`engine/source/OfflineClient.csproj`。

## 未驗證什麼／推測／待驗證

- 未在 Windows 跑 restore、`engine/build-product.ps1`、`rebuild.py --build`、任一支 `--check-only`（本環境 Linux；規則禁止啟動遊戲）。
- 未在另一台乾淨機器做 T004 使用者驗收。
- 未等本 PR 的 GitHub Actions；CI 綠燈屬待驗證。
- 未碰 `releases/`、`engine/assets` junction、vendor、資料庫。
- 已寫明、非本輪實作：TASKS T003 的 `TW_ENGINE_ROOT`／`TW_CLIENT_ROOT` 目前腳本未讀取；`啟動引擎.cmd` 依賴未追蹤的 `releases/r33`。

## 產出路徑

- docs/BUILD.md（T004 指定）
- docs/reports/issue-6.md（本報告）
- docs/REPORT.md（本輪變更／下一步各加 Issue #6 一條）

## 給下一棒的提示

請 Claude 審 `docs/BUILD.md` 是否只依據 repo 事實、指令是否都存在。CI 綠燈後由使用者在 Windows 乾淨目錄照文件跑三條指令＋r35 check-only。不要用本文件當真機登入步驟（那是 T010／T011）。
