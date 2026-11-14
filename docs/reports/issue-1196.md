# Issue #1196：#1188 截圖證據移出 git

結論：通過（✅ 6／❌ 0／⚠️ 0）。PR #1191 最新提交 `d6221f72` 的 `dotnet`、`map-continuity`、`npc-offline` 全綠。原 #1188 的 G01／N01 結論沿用，未重跑實機。

## 做了什麼

- 在既有 `task/1188` worktree 修正證據引用；`task/1196` 僅提交本報告，未切換或建立分支。
- 六張原圖均已存在 `D:\lineage開發\TW1608262200研究\scratch\wt\task-1188\scratch\issue-1188\`，逐 byte 與原提交 `b25ae7b56a93ced5d691f69b138c931abf5c77d2` 相同，無需還原或複製。僅以明確檔名 `git rm` 六張受追蹤 PNG，未遞迴刪除、移除 worktree 或拆 junction。
- `docs/reports/issue-1188.md` 改用檔名、本機 scratch 路徑、826×711、SHA-256、沿用目視結論的表格。所有原有 ✅ 行（僅正規化圖片引用）逐行一致。
- `issue-1188-evidence.json` 無內嵌圖片 base64；只替換 `images` 陣列內六個 `path`，既有 SHA、UIA／狀態／log 與其他 JSON 值全部不變。
- 修正提交 `6fafe512`；同步 `origin/main` 的 `52045940ef444878cbaf499d021a781b1469cf1e`，無衝突；推送 `task/1188` 最新提交 `d6221f720be613bf8c777320cd98b9184cdfd09a`。`docs/REPORT.md` 與 `engine/`（含 lock、閘門與 allowlist）均與該 main 相同，未自行編修。

## 已驗證：逐項結果

本表的 `scratch/issue-1196/` 相對 `D:\lineage開發\TW1608262200研究\scratch\wt\task-1196`。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| E01-1 | ✅ 通過 | 六張 scratch 原圖與 `b25ae7b5` 逐 byte 相同，均 826×711，SHA 與原 JSON 相同且被 git 忽略 | 對原提交執行 `git show`、讀本機原圖、算 SHA、讀 PNG IHDR、`git check-ignore` | `scratch/issue-1196/verification.json`；#1188 報告截圖索引 |
| E01-2 | ✅ 通過 | 遠端 `origin/task/1188` 的 `docs/reports` 無任何 PNG | `git ls-tree -r --name-only origin/task/1188 docs/reports` 篩選 `.png`，結果零筆 | 遠端提交 `d6221f72`；`scratch/issue-1196/verification.json` |
| E01-3 | ✅ 通過 | 所有原有 ✅ 行保留；JSON 僅六個圖片路徑不同 | 與 `b25ae7b5` 比對報告；正規化六個 path 後 JSON 深度相等 | `scratch/issue-1196/verify_evidence.py`、`verification.json` |
| E01-4 | ✅ 通過 | 同步 main 後素材閘門 `status=pass`、`errors=0`、4841 個追蹤檔 | `python -B -X utf8 engine/tools/check-no-game-assets.py`；另執行 `git diff --check` | `scratch/issue-1196/check-no-game-assets.log` |
| E01-5 | ✅ 通過 | PR #1191 最新提交 `d6221f72` 三項 CI 均 COMPLETED／SUCCESS，包含原失敗的素材閘門 | `gh pr view 1191 --json headRefOid,statusCheckRollup`，核對 head SHA 與三個 check 名稱 | `scratch/issue-1196/pr-1191-checks.json`；[dotnet](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/43564114049/job/132354091661)、[map-continuity](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/43564114049/job/132354091947)、[npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/43564114050/job/132354091458) |
| E01-6 | ✅ 通過 | 產品、閘門、allowlist、lock 與索引均等於同步的 main | `git diff origin/main HEAD -- engine docs/REPORT.md` 為空 | `scratch/issue-1196/verification.json` |

## 未驗證什麼

本張只處理 E01 證據放置問題，沒有啟動遊戲、伺服器、DB 或操作 GUI，沒有重測 G01／N01；原有地圖 M01 問題仍交 #1192 的地圖修正與重測任務處理。CI 的離線建置／檢查不等於新的實機驗收。

## 產出路徑與給下一棒的提示

- `task/1188`：更新 `docs/reports/issue-1188.md`、`docs/reports/issue-1188-evidence.json`，移除六張受追蹤 PNG；原圖仍在該 worktree 的 `scratch/issue-1188/`。
- `task/1196`：本報告 `docs/reports/issue-1196.md`；檢核腳本、記錄留 `scratch/issue-1196/`，不追蹤。
- **之後的實機測試證據圖一律放 `scratch/<任務>/`，報告只記 SHA-256 與本機路徑**，並列尺寸和目視結論；不要提交 PNG 到 `docs/reports/`，不要放寬素材閘門或用 base64 繞過。
- 調度器／Claude 接續審閱 PR #1191 並依流程合併；本次不代為合併。#1188 已通過項目不原樣重測；後續只依指定清單測 M01 與附帶回歸 N01-2。
- scratch 不隨 git 保存；若需長期保留本機原圖，調度器應在清理 worktree 前依既有證據保留流程處理。本次保留全部 worktree 與原圖。
