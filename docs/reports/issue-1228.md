# Issue #1228：#1215 實機驗收分流（map4 fixture）

**結論**：#1215 共 7 項，✅ 6／❌ 0／⚠️ 1。唯一的 ⚠️（F07）屬 **(b) 測試環境問題**（fixture 沒有保存到持久位置），沒有程式缺陷，也沒有範圍外的已知限制。開 1 張 resource 任務＋1 張重測任務；原本排隊的 #1216（M01 重測）因 fixture 位置會失效而關閉，內容併入新的重測任務。

本張只改文件與規劃檔，沒有改程式、沒有執行任何程式、沒有開 MySQL 或客戶端。

## 讀取的來源
- #1215 最新 `=== RESULT ===`（2026-11-14 08:32 UTC 留言）與 `docs/reports/issue-1215.md`（`origin/task/1215`，`49fa09e2`，PR #1227 尚未合併）。
- `work/f1197-map4/fixture.json`（同一分支）。
- #1216 內文（`depends_on: [1215, 1228]`，stage test，queued）。
- 唯讀確認：`scratch\wt\task-1215\environments\p24\backups\issue-1215-map4-fixture`（含 `db-data`、`manifest.json`、證據檔）**目前仍存在**；原始專案 `environments\p24\backups\issue-1215-map4-fixture` **不存在**。

## 已通過（✅，之後不重測）
| 項目 | 內容 | 處理 |
|---|---|---|
| F01 | p24lab 資源鎖有效，起始 42061／43324 無監聽 | 沿用 |
| F02 | 首選 #1186 冷備份，1,722 檔／279,503,832 bytes 逐檔複製，SHA 相符 | 沿用 |
| F03 | 10334 資源法師／p24svc01／map4(33071,32800)／GM0／離線；帳號存在 | 沿用（重測任務開服前置的 SELECT 會順帶再確認，不另列項） |
| F04 | `map/4/80048001.seg` 90,398 bytes，SHA `e2dd9928…ba12` 與 #1197 相同 | 沿用 |
| F05 | fixture 從未啟動，結束時逐檔 SHA 符合 manifest | 沿用 |
| F06 | 持久 DB 前後 SHA 相同（`f94bf983…9668`），自啟程序全退出 | 沿用 |

沒有任何修正任務改到程式，所以不需要附帶回歸 ✅ 項目。

## 失敗／缺失項目對應
| 項目 | 結果 | 分類 | 對應 |
|---|---|---|---|
| F07 長期保存 | ⚠️ fixture 只在 `scratch\wt\task-1215`；派工限制「原始專案只讀」，執行器沒有寫入 `environments\p24\backups\issue-1215-map4-fixture` | (b) 測試環境／資料保存 | `f1215-fixture-persist`（resource，codex-local，runner_lock，real_machine） |
| （重測） | F07 ＋ 原 #1216 的 M01（G01 當前置） | — | `f1215-retest`（test，milestone_smoke，depends_on `f1215-fixture-persist`） |

已知限制：無。

### 為什麼 F07 不是程式缺陷
資料本身已由 F02–F05 驗證正確，問題只在 fixture 放在會被清理的 worktree。#1215 報告也確認 fixture manifest SHA 與 #1186 冷備份 manifest SHA 相同（`996a3997…f06b`，逐 byte 複本），所以即使 task-1215 worktree 在修正任務開跑前被清掉，也能從仍在原始專案的 `environments\p24\backups\issue-1186-before` 逐 byte 重建；resource 任務已寫入這條備援路徑。

## 排隊任務處理
- **#1216**（f1197-retest，`depends_on: [1215, 1228]`）：它要重測的 M01 與新重測任務相同，而且它指向的 fixture 路徑會隨 worktree 清理失效。依第 5 點**關閉**並留言註明被 `f1215-retest`（#1228 計畫合併後建立）取代；#1216 的 M01／G01 步驟、沿用清單、不做清單全部搬進 `f1215-retest`，只改 DB 來源為持久 fixture，並加上 F07。
- 其他 open issue 內文含 #1215 的只有本張 #1228。

## 產出
- `docs/plans/issue-1228.json`：`f1215-fixture-persist`、`f1215-retest` 兩張。
- 本報告 `docs/reports/issue-1228.md`。

## 怎麼驗證
- `docs/plans/issue-1228.json` 以 `json.load` 解析通過，欄位與 `docs/plans/issue-1220.json` 同格式。
- 只改文件，未建置、未執行程式；CI 結果看 PR。

## 未驗證／推測
- **推測**：task-1215 worktree 在 `f1215-fixture-persist` 開跑時可能已被清理；此時走 #1186 冷備份重建，#1215 的證據小檔（TSV／JSON）會遺失，但 fixture 本體可逐 byte 還原，報告與 SELECT 原文已在 git（`docs/reports/issue-1215.md`、`fixture.json`）。
- **待驗證**：M01 實際地圖 4 載入，交 `f1215-retest`。

## 給下一棒的提示
1. **調度器**：`f1215-fixture-persist` 需要寫入原始專案 `environments\p24\backups\issue-1215-map4-fixture`（只新增這個目錄）。#1215 就是因為派工訊息寫「原始專案只讀」而沒寫；派工時請在訊息中明列這個例外，否則執行器會照任務內文標 `needs:orchestrator` 停下。最好在 `f1215-fixture-persist` 完成前不要清理 `scratch\wt\task-1215`（清了也能從 #1186 冷備份重建，只是會少 #1215 證據小檔）。
2. **PR #1227**（#1215 的 `fixture.json`）最好先合併；`f1215-fixture-persist` 會在 main 已有 fixture.json 時一併更新路徑，否則只寫新的 `work/f1197-map4/fixture-persist.json`（重測以它為準）。
3. `f1215-retest` 只重測 F07、M01（G01 當前置）；其餘全部沿用。
