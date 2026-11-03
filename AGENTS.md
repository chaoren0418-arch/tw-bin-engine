# tw-bin-engine — 自主 BIN 引擎（TW1608262200 學期專案）

本檔是所有 AI 工具（Claude、Cursor、Codex、ChatGPT）與人的共同規則。`AGENTS.md` 與 `.cursor/rules/project.mdc` 是本檔的複本，改動請只改本檔並重新同步。

## 目標
以 C#/.NET 重新實作天堂 3.81 舊客戶端（原 `LiTo.bin` / TW1608262200 BIN），**不再依賴舊式 bin**，讓遊戲能完整運作：登入、選角、世界移動、戰鬥、道具、魔法、地圖快取、UI、音效。評分重點：**可重現客戶端、遊戲完整運作**。

## 目前狀態（2026-11-03）
- 正式版本 **r33**（`releases/r33`，離線驗收通過，原生實機驗收未通過）。r34 拾取整合僅隔離 stage。
- P01–P24 階段完成；P25（架構對帳）、P26（r34 覆核）、P27（UI／輸入缺口）於 10/29 派工，詳 `主管/P25-P27-新主管接管-20261029/HANDOFF.md` 與 `work/P25-architecture-baseline-r1/`。
- 未驗項目：完整原生畫面／滑鼠、map4／6016 場景音訊、同版全流程、NPC 服務（商店／技能學習／交易／倉庫／船運）。
- 歷史治理文件（主管／工作者對話 ID、Goal、pins）**只供參考**，不再是流程的一部分。流程見下方「工作方式」。

## 技術棧與建置
- C# / .NET（SDK 版本見 `engine/global.json`），Windows x64。
- 原始碼：`engine/source`（主專案 `OfflineClient.csproj`）、`engine/audio`、`engine/web`、`engine/launcher`。
- 建置：`engine/build-product.ps1`；啟動：`啟動引擎.cmd`（`--check-only` 只檢查環境）。
- 唯讀來源核驗：`python -B -X utf8 engine/_integration/r33/rebuild.py`。
- 素材：`engine/assets` 是指向 `releases/r28` 的唯讀 junction；遊戲資源一律引用 `D:\lineage開發\0-客戶端`，**不得複製 pak/map/sprite 進本專案**。
- 資料庫（MySQL）由使用者管理；AI 不查詢、不更改、不啟停 DB 或服務。

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
- 原生實機測試（啟動遊戲、登入、畫面）只由使用者執行；AI 提供測試步驟與核對清單。
- Commit 訊息：`<範圍>: <做了什麼>`，中英皆可。
