# Issue #1129：分流 #1119 實機驗收的失敗項目

**結論：#1119 沒有任何實機 ❌／⚠️ 項目；失敗只有合併前閘門（PR #1128 把 PNG 截圖與 build log 加進 git，CI 擋下）。** 開 1 張證據重新封裝任務（`f1119-evidence-pack`），**不開重測任務**——沒有需要重測的項目，開了就是原樣重跑、空轉。

## 讀到的來源

- #1119 最新 `=== RESULT ===`（第 2 次嘗試，2026-11-13T20:51Z）：「通過；R07 本輪 ✅，其餘 13 項沿用」。第 1 次嘗試（20:12Z）的「R07 ⚠️ GUI 擷取逾時」已被第 2 次取代。
- `docs/reports/issue-1119.md`（`origin/task/1119`，commit `79d30d6e`）：✅ 14／❌ 0／⚠️ 0。
- 合併前閘門（#1119 留言 20:55Z）：
  - ❌ `docs/reports/issue-1119/launcher-build.log`、`product-build.log` 被 `.gitignore`（`*.log`）排除卻被加進 git
  - ❌ PR #1128 CI 失敗
- PR #1128 CI：`build / dotnet` FAILURE（run 43505150462，job 132160744267），失敗步驟 `Reject tracked game assets`：`engine/tools/check-no-game-assets.py` 拒收 `docs/reports/issue-1119/` 下 11 張 PNG（`PNG magic`；第 99 行只允許 `docs/demo/`、`docs/native-baseline/`）。`map-continuity`、`npc-offline` 均 SUCCESS。

## 已通過（✅）、之後不重測

| 編號 | 來源 | 說明 |
|---|---|---|
| R07 七職業 GUI 建角 | #1119 本輪 | 7/7 可建立截圖；GUI 建立騎士 objid 10387 並立即 GUI 刪除，3→4→3 槽 |
| B01、R05、R08、R09、R10、R11、R12、R13、R14、R15、C01、C02、V01 | #1108 沿用 | #1119 未重測，依據未變 |

修正任務只搬移證據檔、改報告連結，**不改任何程式**，所以沒有需要附帶回歸的 ✅ 項目。

## 失敗項目 → 任務／已知限制

| 失敗項目 | 分類 | 對應 |
|---|---|---|
| 閘門：11 張 PNG 進 git，CI `Reject tracked game assets` 失敗 | (b) 證據封裝問題（非程式缺陷、非實機失敗） | `f1119-evidence-pack` |
| 閘門：2 個 `*.log` 被 .gitignore 排除卻加入 git | (b) 同上，同一根因（證據放錯位置） | `f1119-evidence-pack` |
| 報告「待驗證」：未保存原始回包 bytes（S98／S127 以解碼後狀態＋DB 讀回佐證） | (c) 已知限制 | 不開任務。R07 驗收條件只要求 S98=2＋清單／DB 讀回，已滿足；要原始 bytes 需另開授權測試配置封包記錄，#1119 報告已說明不為補 log 再送一次建角 |
| 報告「待驗證」：WGC 擷取逾時根因 | (c) 已知限制（工具環境） | 不開任務。已有可用替代方法（Alt+Print→小畫家），不影響判定；#1125 已寫明遇到時的處置 |

### 為什麼 `f1119-evidence-pack` 不照 (b) 範本設 `runner_lock: true`、`real_machine: true`

這張只需要在本機檔案系統複製檔案、算 SHA-256、改 Markdown 與 push，不用 P24Lab、不開遊戲。設成真機鎖會白白搶 `p24lab` 資源鎖、擋住 #1125。由 `codex-local` 執行是因為要寫 `environments\p24\backups\issue-1119-evidence\`（主專案目錄，#1120／#1125 已用此慣例），`claude-local` 不能寫主專案目錄。

### 為什麼不開重測任務

分流規則第 4 點要求「最後加一張重測任務」，前提是有 ❌／⚠️ 項目要重測。#1119 的實機項目全部 ✅，修正任務也不改程式，重測清單是空的；硬開一張就是使用者 2026-11-13 禁止的原樣重跑。改由 `f1119-evidence-pack` 的驗收（CI 全綠、閘門無 ❌、報告進 main）收尾 #1119。若調度器規則一定要有 test 階段任務，請標 `needs:orchestrator` 再討論，不要開空的重測。

## 其他依賴 #1119 的任務（第 5 點）

- 查詢 open issue 內文含「1119」的：只有 #1125（f1109-retest）與本張。#1125 的 `depends_on: [1124]`，**不含 #1119**，不需改 depends_on。
- 但 #1125 內文寫「開始時看 #1119：已以 R07 ✅ 結案就沿用；未結案或失敗，本張照樣做」。#1119 是帶 `gate:fail` 結案，執行器可能誤讀成失敗而重做 R07（空轉，且會再建角）。已用 `gh issue edit` 把那句改成明確的「R07 已 ✅（#1119 報告 commit `79d30d6e`），沿用，不重做；閘門失敗只是證據封裝，由本分流的修正任務處理」。

## 怎麼驗證

- 讀 #1119 全部留言、報告、PR #1128 的 statusCheckRollup 與失敗 job log；`git diff --stat origin/main...origin/task/1119`（33 檔：3 個頂層＋30 個 `issue-1119/` 下，11 PNG、2 log、17 文字）。
- 讀 `engine/tools/check-no-game-assets.py` 確認 PNG 例外路徑只有 `docs/demo/`、`docs/native-baseline/`。
- `docs/plans/issue-1129.json` 以 Python `json.load` 解析通過。
- 未執行任何程式、未碰 DB／伺服器。

## 未驗證

- `f1119-evidence-pack` 執行時 `scratch/wt/task-1119` 是否仍在（已寫備案：從 commit `79d30d6e` 的 blob 取出）。
- #1119 的冷備份 `scratch/issue-1119/backup-before`（task-1119 worktree 內）不在本分流範圍，是否需要搬到 `environments\p24\backups` 由調度器／dot 決定。

## 產出

- `docs/plans/issue-1129.json`（1 張任務：`f1119-evidence-pack`）
- `docs/reports/issue-1129.md`（本檔）
- #1125 內文 R07 那一句已更新

## 給下一棒的提示

- `f1119-evidence-pack` 不要放寬 `check-no-game-assets.py` 或 allowlist 來過 CI；PNG 一律移出 git，報告只留路徑＋SHA-256。
- 完成前**不要清理 `scratch/wt/task-1119` worktree**（內有 PNG 原檔、冷備份與 junction，不可遞迴刪除）。
- 之後的實機任務請在任務內文預先寫明：截圖與 log 放 `environments\p24\backups\issue-<本號>-evidence\`，Git 只放 Markdown／JSON／TSV／TXT 摘要，避免同樣的閘門退回。
