# #1238：#1229 實機驗收失敗項目分流

**結論**：#1229 共 6 項，✅ 2／❌ 0／⚠️ 4。4 個 ⚠️（P01–P04）是**同一個根因**：派工訊息寫「原專案只讀取、不要寫檔」，任務本身卻要改原專案 `environments/p24/db-data` 的角色位置；執行器沒有寫入例外就停手。歸類為 **(b) 測試環境／派工問題**，沒有程式缺陷，也沒有範圍外的已知限制。開 1 張 resource 任務＋1 張重測任務；原本排隊的 #1231（`depends_on: [1229, 1230, 1238]`）第一步就要讀回相鄰位置，與新重測的 P03 重疊，依第 5 點**關閉**，內容併入新重測任務。

## 做了什麼

1. 讀 #1229 Issue 內文、RESULT 留言（commit `7ff9e064`）與 `task/1229` 分支的 `docs/reports/issue-1229.md`。
2. 對照 #1225 原始計畫（`docs/plans/issue-1225.json`）、#1228 的同類分流（#1215 F07：fixture 只在 worktree）作為前例；確認 #1230（loopback 修正）已關閉、PR #1233 已合進 main（`SmokeTools.ps1`／`README.md` 有 `ownMaxPeak`／`otherSessions`），main 上沒有 `work/f1186-smoke/`。
3. 掃描開啟中的 Issue，內文含 1229 的只有本張與 #1231。
4. 寫 `docs/plans/issue-1238.json`（2 張任務）與本報告；關閉 #1231 並留言。

## 已通過（✅）項目：不重測

| 編號 | 內容 | 處理 |
|---|---|---|
| P00 備份 | 1,722 檔／279,511,437 bytes，逐檔 SHA 一致，manifest SHA `105dfc63…8638` | 沿用為「#1229 當時持久 DB 狀態」的紀錄。但備份只在 `scratch/wt/task-1229`（會被清理），新 resource 任務依規定「寫入前重新備份／核對」自行做副本與 manifest，不算重測 |
| P05 收尾 | 未自啟程序，42061／43324 無監聽 | 每輪必做的收尾，不算重測 |

## 失敗／缺失項目對應

| 編號 | #1229 結果 | 分類 | 對應 |
|---|---|---|---|
| P01 普通步行到位 | ⚠️ 未開始 | (b) 派工寫入範圍衝突 | `f1229-adjacent-fixture-durable` 步驟 3A；重測看 `f1229-retest` P03 |
| P02 暫設 GM 備援 | ⚠️ 前提未成立 | (b) 同上 | `f1229-adjacent-fixture-durable` 步驟 3B（只在 A 15 分鐘不成立時） |
| P03 保存與重登入 | ⚠️ 無 `adjacent-pcs.json` | (b) 同上 | 準備：`f1229-adjacent-fixture-durable` 步驟 4／6；重測：`f1229-retest` P03 |
| P04 最終 SELECT | ⚠️ 未開 DB | (b) 同上 | 準備：`f1229-adjacent-fixture-durable` 步驟 5；重測：`f1229-retest` P04 |

已知限制：無。

## 設計重點

- **不改共用持久 DB**：#1229 原設計要直接改 `environments/p24/db-data`。改成在 worktree 隔離副本上步行／GM 備援／重登入，關庫後把副本**新增**到 `environments\p24\backups\issue-<本號>-adjacent-fixture\`。持久 DB 全程只讀、前後 SHA 必須相同。好處是寫入只有「新增一個目錄」，可還原，也不會移動別的任務依賴的角色位置（前例：#1228 的 `f1215-fixture-persist`）。
- **寫入範圍明列在任務內文**，並規定：派工訊息若仍寫「原專案只讀」且沒列這個例外，就標 `needs:orchestrator` 停下，不在 worktree 留 fixture 冒充完成。
- **重測不寫原專案**：`f1229-retest` 從 fixture 複製到自己的 scratch 再開服，不用 GM、不寫 DB。

## 第 5 點：其他依賴 #1229 的任務

- **#1231**（`f1210-tooling-retest`，`depends_on: [1229, 1230, 1238]`）：R06 第一步就是讀回兩角色座標是否相鄰，與 #1229 P03 重疊；而且它指定直接開持久 DB `environments/p24/db-data`，會碰上同樣的唯讀衝突。依第 5 點**關閉**，留言註明被 `f1229-retest`（本計畫合併後建立）取代。#1231 的前置、R01／R06／R07／SM22／SM35／SM51 步驟、沿用清單全部搬進 `f1229-retest`，只改：DB 來源改成新 fixture 的副本；新增 P03／P04；#1230 已合併，前置不再等它；SM51 的 map4 子項改看 `f1215-retest`（#1216 已被 #1228 關閉）。

## 怎麼驗證

- `docs/plans/issue-1238.json` 用 Python `json.load` 讀回成功，2 項，`f1229-retest.depends_on = ["f1229-adjacent-fixture-durable"]`；欄位格式照 #1225／#1228 的 plan。
- 引用的 SHA 與路徑都從 #1229 報告、#1225 plan、main 檔案清單抄錄核對過。

## 未驗證什麼

- 沒有執行任何程式、沒有開 DB 或伺服器（design 階段，依規定）。
- 調度器的派工模板是否會照任務內文給出寫入例外：**待驗證**。若不會，`f1229-adjacent-fixture-durable` 會停在 `needs:orchestrator`。
- #1186 參考相鄰點周圍 3×3 是否真的沒有牆：**未知**，留給 resource 任務實地確認。

## 產出路徑

- `docs/plans/issue-1238.json`
- `docs/reports/issue-1238.md`
- 分支 `task/1238`

## 給下一棒的提示

- **調度器**：派 `f1229-adjacent-fixture-durable` 時，派工訊息的「原專案只讀」要加上例外：允許新增 `D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-<本號>-adjacent-fixture\`。不加的話，這張會跟 #1229 一樣停住（這次會標 `needs:orchestrator`）。目前 #1234（`f1215-fixture-persist`）也在等同類例外，可以一起看。
- **resource 執行器**：既有 `mysqld.exe` PID 4144 不是自啟的，不要碰；開服前先確認 43324 沒有監聽。
- **重測執行器**：fixture 不符時列 ❌（環境）並停止 R06／SM22／SM35，不要自己移動角色。
