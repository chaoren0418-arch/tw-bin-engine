# #1263：SM27／SM29 重測條件前置查核

結論：阻塞。2026-11-14 20:05（UTC+8）查核時，必要的 PR #1219、#1258 都尚未合併；依任務「前置不成立就在 Issue 留言說明後結束，不要空跑」，未進入資源準備與 SM 測試。

## 已驗證

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| P01 | ✅ | #1262 裁定為 SM27 甲、SM29 甲，兩項皆可進入後續準備 | 讀取裁定報告與 AGENTS.md 的 2026-11-14 例外 | `docs/reports/issue-1262.md`、`AGENTS.md` |
| P02 | ⚠️ | PR #1219 為 OPEN，mergedAt／mergeCommit 均為 null | `gh pr view 1219 --repo chaoren0418-arch/tw-bin-engine --json number,state,mergedAt,mergeCommit,url` | `scratch/issue-1263/pr-1219.json` |
| P03 | ⚠️ | PR #1258 為 OPEN，mergedAt／mergeCommit 均為 null | 同上，PR 編號改為 1258 | `scratch/issue-1263/pr-1258.json` |
| P04 | ⚠️ | 已執行 `git fetch origin`、`git merge origin/main`，回覆 Already up to date；fixtures 與 #1222 報告仍不存在 | `Test-Path work/f1186-smoke/fixtures.json`、`Test-Path docs/reports/issue-1222.md` 均為 False | 本報告所記錄的命令結果 |
| C01 | ✅ | 本輪未啟動任何程序；收尾 42061／43324 監聽數為 0 | `Get-NetTCPConnection -State Listen` 篩選 LocalPort 42061、43324 | `scratch/issue-1263/listeners-final.json` |

本輪只有前置與收尾查核，沒有執行任何 SM，故不以查核的 ✅ 計為實機「部分通過」。GitHub 前置狀態：[PR #1219](https://github.com/chaoren0418-arch/tw-bin-engine/pull/1219)、[PR #1258](https://github.com/chaoren0418-arch/tw-bin-engine/pull/1258)。

同步後的 HEAD／origin/main 均為 `3b33e73c52afd6bd5ec8f9f4f409e1b682a85e1d`，工作分支維持 `task/1263`。

## 未驗證／未執行

- 未讀寫 DB、未啟動 MySQL／伺服器／客戶端，因此沒有 SELECT 條件與列數、資料前後值、還原 SQL 或本輪備份 manifest SHA；這些均為未執行，不沿用歷史數值冒充本輪結果。
- 未重核拍賣期限／最低價／bidder、角色金幣與外觀、地圖通行及站位。
- 未召出 NPC、未改 spawnlist、未發金幣、未改 GM；沒有 SM27／SM29 通過或失敗判定。
- 必要的 `work/f1186-smoke/fixtures.json` 尚未進入 main，因此未建立替代檔，也未宣告 `readyForRetest=true`。
- 資源鎖端點曾讀取，但取出的 `leases` 沒有可核對的項目，未宣稱 lease 驗證成功；本輪未使用 P24Lab，阻塞依據為上述 PR 前置。

## 推測

無。未將 #1222 歷史拍賣與角色資訊推定為目前狀態。

## 產出與給下一棒的提示

- 產出：`docs/reports/issue-1263.md`；原始查核輸出在本 worktree 的 `scratch/issue-1263/`（不入 git）。未改產品程式、`docs/REPORT.md` 或 rebuild-lock。
- 調度器／Claude 先完成 PR #1219、#1258 的審查與既定合併流程，再重新派發 #1263；本機執行器重新同步 main，確認 fixtures 含 #1222 的 `locations`／`blockedFixtures` 後，依原任務一次完成冷備份、DB／地圖查核及 fixtures 更新。
- 後續重測 #1264 須等待資源準備完成；本報告不是可開始重測的通知。重新派發時應核對當時拍賣期限，不能沿用歷史未到期結論。
