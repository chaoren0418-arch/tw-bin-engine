# PLAN — tw-bin-engine

更新：2026-11-04。本檔由 Claude 維護；任務細目在 `TASKS.md`。

## 一句話目標
用 C#/.NET 重現天堂 3.81 客戶端，**不依賴 LiTo.bin**，在使用者的機器上從入口啟動、登入正式伺服器、完整玩一輪（移動／戰鬥／道具／魔法／NPC／換圖／音效）並能存檔重登。評分重點是「可重現 + 完整運作」，沒有日期壓力。

## 現況（事實）
| 項目 | 狀態 |
|---|---|
| 正式入口 | `啟動引擎.cmd` → `releases/r33/launcher/P24.Launcher.exe`；`project-manifest.json` revision=r33 |
| r34 | 已封裝（`releases/r34`），入口未切換；拾取呈現產品 |
| canonical 來源 | `engine/source+audio+web` = **181 檔**，r35 lock（`engine/_integration/r35/`）核驗 exit 0；`build-product.ps1` 指向 r35 |
| 可重現建置 | **M0 達成**：CI 綠燈；使用者本機 r35 完整建置 + `engine\啟動新版客戶端.cmd --check-only` exit 0（Issue #13） |
| 最新候選 | P38 隔離交付候選（`work/P38-delivery-candidate-r1`），launcher check-only exit 0，等獨立覆核；入口未啟用 |
| 離線測試 | r33：4337 assertions + 93 observations；r34 review +184；P27：237 斷言；P31–P34 拾取覆核全過 |
| **原生實機驗收** | **自 r29 起每一版都是 native=false**。最後的實機證據是 r22/r27/r28（登入、選角、移動、匕首、能力點、聊天、旅館、拾取、洞穴戰鬥、回城） |

## 真正的缺口（決定里程碑的依據）
1. **沒有人跑過最新版的真機流程。** 30 多個離線階段堆在一個從未被實機驗證的基礎上。
2. 來源 pin 與建置腳本互相矛盾（r33/r34/181 檔），**在乾淨機器上無法一鍵重建**——這直接牴觸「可重現」的評分重點。
3. 所有腳本硬編 `D:\lineage開發\TW1608262200研究` 絕對路徑。
4. NPC 服務（買賣、技能學習、交易、倉庫、船運）、map4/6016 場景音訊、原生畫面／滑鼠 完全未驗。

## 里程碑
### M0 可重現建置 — ✅ 達成（2026-11-04）
- GitHub Actions `dotnet build` 綠燈（CI 用 nuget.org，本機維持離線 vendor）。
- 統一 canonical：以 181 檔為準，產生新的 lock（r35），`build-product.ps1` 與 `rebuild.py` 指向同一版；`engine/README.md`、`project-manifest.json` 同步。
- 消除硬編路徑：腳本改用 `$PSScriptRoot` / 環境變數 `TW_ENGINE_ROOT`。
- 驗收：乾淨 clone + `D:\lineage開發\0-客戶端` 資源 → 一條指令建出可 `--check-only` 通過的 launcher。

### M1 真機基線 — 進行中（T016 本機建置 → T011 使用者真機）
- 用 M0 的建置跑 N01 流程：登入→移動→攻擊／技能 1/3/4/8→道具→能力點→存檔→重登。
- 每一步截圖＋server 回包紀錄，寫進 `docs/native-baseline/r35.md`。
- 失敗項直接變成 TASKS 的 bug 項；通過項把 completion-matrix 對應列的 nativeR35Accepted 設 true。
- 這是 **第一次把 30 階段的離線工作接回現實**，優先於任何新功能。

### M2 完整遊戲循環
- NPC 服務：商店買賣、技能學習、倉庫、直接交易、私商（N03、C01）。
- 換圖連續性：快取換區、旅館、船運（N04）。
- 6016 音訊在正式世界的 dispatch／退役（N05）。
- 原生畫面：滑鼠點地／攻擊／交談／拾取、HUD、陰影、聊天焦點（N02，用 P27 的 12 組計畫）。

### M3 內容擴充與交付
- 技能從 1/3/4/8 擴到職業基本技能；Slow 速度拾取／丟棄；MAP-F4 fade（S01–S03，需來源資料）。
- 可攜交付：含 runtime 的安裝包、安裝說明、架構文件（`docs/ARCHITECTURE.md` 重寫為交作業版本）。

## 分工
- Claude：維護本檔／TASKS、審 PR、CI、架構決定、寫測試步驟。
- Cursor：實作 TASKS 指定項，`feat/<ID>-<name>` 分支開 PR。
- ChatGPT：交叉驗證架構方案、產生測試案例。
- 使用者：真機測試（M1 起每個里程碑一次）、合併 PR、資料庫。

## 停用的舊流程
主管／工作者對話、Goal、pins、seal、每輪時間戳 JSON、`工作佇列.json`。`work/` 與 `主管/` 保留為歷史，不再新增。
