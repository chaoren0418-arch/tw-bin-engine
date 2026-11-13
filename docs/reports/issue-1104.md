# Issue #1104：分流 #1098 實機驗收失敗項目

**結論：#1098 的 1 個 ❌、7 個 ⚠️ 都是測試環境／資料問題，沒有引擎程式缺陷；開 2 張 resource 修正任務＋1 張重測任務（`docs/plans/issue-1104.json`）。**

## 做了什麼

- 讀 #1098 最新 `=== RESULT ===`（✅ 9／❌ 1／⚠️ 7）與 `docs/reports/issue-1098.md`（`origin/task/1098`，commit `7467af1e`）。
- 查 #1099（f475-retest）：雲端因 #1098 開服失敗先關閉，並說明由本張建立新的重測任務；#1093、#1097 已關閉。因此重測任務第二段接回 #1099 的 SM01–SM59 範圍。
- 確認路徑：task-1098 worktree 的副本環境與冷備份 `environments\p24\backups\issue-1098-before`、`scratch\issue-1098\applied.sql` 目前仍在；主專案 `environments\p24` 存在。
- 只改文件與規劃檔，沒有執行程式、沒有碰 DB 或服務。

## 已通過（✅）項目——不重測

| 編號 | 內容 | 處理 |
|---|---|---|
| R01 | 完整冷備份流程 | 沿用；新備份由修正任務各自做 |
| R02 | MySQL 43324 啟動 | 沿用啟動方式 |
| R08 | 弓 172×1、箭 40743×200 | **只讀回歸**（原因見下） |
| R09 | 普通肉 40056×19 | **只讀回歸** |
| R10 | 控制戒指 20288×1、瞬移卷 40100×20 | **只讀回歸**（f1098-test-data 重放後） |
| R11 | 料理書 41255×1 | **只讀回歸**（f1098-test-data 重放後） |
| R14 | 金幣 40308×100000 | **只讀回歸** |
| R15 | 全角色 GM0、來源未變 | **只讀回歸**（GM0） |
| R16 | 程序收尾 | 沿用，重測結束照常收尾 |

回歸原因：沒有程式修改，但 #1098 是在 **task-1098 worktree 的 DB 副本**驗證與補物品，該 worktree 會被調度器清掉；修正任務改在主專案持久環境 `environments\p24` 操作，R10／R11 必須重放、R08／R09／R14／R15 要在新 DB 讀回確認。

## 失敗／缺失項目 → 對應任務

| 編號 | 前次 | 分類 | 對應 |
|---|---|---|---|
| R03 遊戲服 42061 | ❌ | (b) 測試資料：`character_items_time` 孤兒列 itemObjId=10306，JAR 拒絕開服 | **f1098-timed-orphan**（CLAUDE.md 2026-11-05 授權明文包含清孤兒限時紀錄；先全面核對持有表並備份） |
| R04 登入至選角 | ⚠️ | (b) R03 連帶 | f1098-timed-orphan |
| R06 同帳號雙開 | ⚠️ | (b) R03 連帶 | f1098-timed-orphan |
| R05 雙角色同時在世界內 | ⚠️ | (b) 測試資料 | **f1098-test-data** |
| R07 七職業 | ⚠️ | (b) 測試資料（現有角色全為王族） | f1098-test-data |
| R12 寵物／召喚 | ⚠️ | (b) 測試資料 | f1098-test-data |
| R13 血盟／盟倉 | ⚠️ | (b) 測試資料 | f1098-test-data |
| R17 retest 就緒 | ⚠️ | 綜合 | **f1098-retest** 第一段 |

(a) 程式缺陷：無。伺服器 JAR 拒絕載入孤兒資料是伺服器的保護行為，不是本引擎程式，也不改 JAR。
(c) 已知限制：無新增。R06 同帳號雙開若伺服器不允許，屬伺服器政策，記入結論、改用第二帳號，不開任務。

## 新任務（`docs/plans/issue-1104.json`）

1. `f1098-timed-orphan`（resource，codex-local，runner_lock，real_machine）：主專案 P24Lab 冷備份 → 全面核對 10306 及其他孤兒持有表 → 確認無持有者才刪並附還原 SQL → 開服 42061 → R04 登入選角、R06 同帳號雙開結論。
2. `f1098-test-data`（resource，同上，依賴 1）：重放 #1098 三筆補物品並讀回 R08–R15；準備 R05／R07／R12／R13。
3. `f1098-retest`（test，milestone_smoke，依賴 1、2）：第一段只重測 R03–R07、R12、R13、R17＋上列只讀回歸；R03／R04 不過即停。第二段接 #1099 的 SM01–SM59（SM01 閘門）。

## 怎麼驗證／未驗證什麼

- 已驗證：#1098 RESULT 與報告內容、#1099／#1093／#1097 狀態、task-1098 副本與 applied.sql 仍存在、`issue-1104.json` 為合法 JSON。
- 未驗證：10306 是否在倉庫／拍賣等表有持有者（交 f1098-timed-orphan）；主專案 DB 與副本差異。

## 產出路徑

- `docs/plans/issue-1104.json`
- `docs/reports/issue-1104.md`（本檔）

## 給下一棒的提示

- 調度器清理 `scratch\wt\task-1098` 前，最好等 f1098-timed-orphan 決定是否沿用副本（任務內已寫：沿用需先搬到主專案 `environments\p24\backups\`）。該 worktree 有 `server/maps` junction，不可遞迴刪除。
- f1098-retest 依賴的 #1093、#1097 已關閉，plan 內只寫在內文；若調度器要硬依賴，請補 `depends_on_issues`。
