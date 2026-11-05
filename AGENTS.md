# tw-bin-engine — 自主 BIN 引擎（TW1608262200 學期專案）

本檔是所有 AI 工具（Claude、Cursor、Codex、ChatGPT）與人的共同規則。`AGENTS.md` 與 `.cursor/rules/project.mdc` 是本檔的複本，改動請只改本檔並重新同步。

## 對話分工與語言（使用者規定，所有 AI 必讀）
- **全程使用繁體中文**：對使用者的回報、Issue／PR 留言、報告文件一律繁體中文（程式碼、指令、檔名維持原文）。
- 每個專案（含調度器本身）都有兩個 Claude 對話：
  - **雲端對話**：監控與回報——看 GitHub 佇列、規劃（plan／design）、審查裁定、處理 `needs:claude`；碰不到使用者電腦。
  - **本機對話**（Claude 桌面版「本機」環境）：處理需要本機的事——本機資源、Windows 建置與檢查、測試前置、本機檔案整理；**使用者平常只跟本機對話溝通**。
- 雲端對話需要本機動作時：開 Issue（`stage: test` 或 `stage: resource`、`runner: codex-local`）交給本機執行，或標 `needs:local` 交給本機對話；**不要叫使用者做**。
- 調度器本身的問題標 `needs:orchestrator`；只有真機驗收（啟動遊戲／登入／看畫面）與不可逆決策才標 `needs:user`。
- 使用者只負責驗收與觀測。

## 目標
以 C#/.NET 重新實作天堂 3.81 舊客戶端（原 `LiTo.bin` / TW1608262200 BIN），**不再依賴舊式 bin**，讓遊戲能完整運作：登入、選角、世界移動、戰鬥、道具、魔法、地圖快取、UI、音效。評分重點：**可重現客戶端、遊戲完整運作**。

## 目前狀態（2026-11-04，詳見 `PLAN.md`）
- 正式入口仍是 **r33**（`啟動引擎.cmd` → `releases/r33`）；**r35 是 canonical 建置**：181 檔 lock 在 `engine/_integration/r35/`，`engine/build-product.ps1` 指向 r35。
- **M0 可重現建置已達成**（T001–T004、T013–T015）：CI 綠燈；使用者本機 r35 完整建置 0 警告、`engine\啟動新版客戶端.cmd --check-only` exit 0（revision r35，見 Issue #13 報告）。乾淨機器建置步驟見 `docs/BUILD.md`。
- `work/` 做到 **P38**（隔離交付候選，等覆核）。P27 UI／輸入查核 237 斷言無缺陷。
- **M1 真機基線已取得**（2026-11-05，Issue #23／#27）：dot 在 P24Lab 跑 r35 N01，✅ 01/02/08/11/12、✅限定 13/14、⚠️ 03/04/05/07/09/10/15、❌ 06，詳見 `docs/native-baseline/r35.md`。r35 凍結為此基線版本；後續改程式走 dev lock（T040），再做封包追蹤（T041）與局部補測（T042）。
- 未驗：NPC 服務、map4／6016 場景音訊、原生畫面／滑鼠、換圖連續性。
- 歷史治理文件（主管／工作者對話 ID、Goal、pins、seal）**只供參考**，不再是流程的一部分。流程見「工作方式」與 `PLAN.md`／`TASKS.md`。

## 技術棧與建置
- C# / .NET（SDK 版本見 `engine/global.json`），Windows x64。
- 原始碼：`engine/source`（主專案 `OfflineClient.csproj`）、`engine/audio`、`engine/web`、`engine/launcher`。
- 建置：`engine/build-product.ps1`；啟動：`啟動引擎.cmd`（`--check-only` 只檢查環境）。
- 唯讀來源核驗：`python -B -X utf8 engine/_integration/r35/rebuild.py`（exit 0 = 181 來源／190 pinned 全中）；完整建置加 `--build --write-receipt`。r33/r34 的 `rebuild.py` 已過時，不再使用。
- CI：`.github/workflows/build.yml` 在 windows-latest 上 `dotnet build`，用 `.github/nuget.ci.config`（nuget.org）；本機 `engine/NuGet.Config` 仍是離線 `vendor`。
- 素材：`engine/assets` 是指向 `releases/r28` 的唯讀 junction；遊戲資源一律引用 `D:\lineage開發\0-客戶端`，**不得複製 pak/map/sprite 進本專案**。
- 資料庫（MySQL）由使用者管理；AI 不查詢、不更改、不啟停 DB 或服務。
  - **例外（使用者 2026-11-05 授權，只限 `dot`）**：P24Lab 實驗環境（`environments\p24`，一律經 ASCII junction `D:\BinEngine\environments\p24` 存取；舊路徑 `D:\P24Lab-ClientEngine` 已不存在；MySQL `127.0.0.1:43324`（server_uuid `e3443e40-d090-11f1-8040-4cedfb679789`）、伺服器 port `42061`）的啟停與維護由 `dot` 自行安排，包含為了開服清掉孤兒限時紀錄。改 DB 前必須先把目前狀態完整備份（放 P24Lab 自己的備份目錄），只做開服需要的最小修改，結果寫進 Issue 的 RESULT。其他 AI（Cursor、Codex 雲端、Claude）仍不可碰；正式伺服器（port 2000）與正式 MySQL（3306）任何人都不可碰。

## 目錄
| 路徑 | 用途 | git |
|---|---|---|
| `engine/` | 唯一可寫的引擎來源 | 追蹤（排除 build/obj/bin/tmp/.dotnet/.nuget/vendor） |
| `docs/` | 文件 | 追蹤 |
| `work/` | 各階段工作產出（HANDOFF.md、result.json） | 追蹤（排除大型證據） |
| `主管/` | 歷史派工紀錄 | 追蹤（唯讀，不再新增） |
| `releases/` | 封存版本（r28–r33），不可變 | **不追蹤**，只留 `releases/SHA256SUMS.txt` |
| `P01-…P24-*/` | 研究歷史 | **不追蹤**，禁止當開發入口 |
| `history/`, `recovery/`, `environments/`, `evidence/` | 實驗室、隔離原件、DB 快照 | **不追蹤** |
| `scratch/` | 任何暫存、實驗、腳本輸出 | **不追蹤**，隨時可整個刪 |

## AI 產出規則（三個工具共用）
1. 只寫本專案資料夾內；不寫 `C:\`、桌面、`%TEMP%`、家目錄。
2. 暫存／實驗／一次性腳本一律進 `scratch/`。
3. 同一主題的報告或證據只保留 `docs/` 或 `work/<階段>/` 下的最新版，歷史靠 git；不再每輪新增帶時間戳的 JSON，不再維護 `工作佇列.json` 這類巨型狀態檔。
4. 建置產物只進 `bin/`、`obj/`、`build/`、`target/`。
5. 不以 stub、假畫面、合成資料冒充功能完成；報告分「已驗證／推測／待驗證」。
6. 來源檔以 SHA-256 識別；地址標明 File Offset／RVA／VA。
7. 不啟動、附加、停止既有遊戲程序；不連正式服務；不修改原 BIN、正式客戶端、封存 release 與硬連結。

## 工作方式
- `PLAN.md`：里程碑與目前狀態（Claude 維護）。`TASKS.md`：任務清單，每項含範圍、驗收條件、狀態。
- 分支：`main` 永遠可建置；工作在 `feat/<任務編號>-<短名>`，完成開 PR，Claude 審查、使用者合併。
- Cursor 執行任務時：先讀本檔與 `TASKS.md` 指定項，只改任務列出的檔案範圍，完成後在 PR 描述寫「做了什麼／怎麼驗證／未驗證什麼」。
- 原生實機測試（啟動遊戲、登入、畫面）由使用者授權的本機代理 `dot` 在 P24Lab 執行（見資料庫例外）；其他 AI 只提供測試步驟與核對清單。
- Commit 訊息：`<範圍>: <做了什麼>`，中英皆可。
