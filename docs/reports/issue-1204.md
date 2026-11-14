# Issue #1204：分流 #1197 實機驗收失敗項目

**結論：** #1197 只有 1 項沒過：M01 ⚠️。原因是測試資料：#1186 把資源法師瞬移到地圖 0，之後沒有移回來。這不是程式缺陷。本輪開 **1 張 resource（地圖 4 fixture）＋1 張重測**，**沒有開 code 任務**，也沒有新的已知限制。只改了文件與規劃檔，沒有改程式，也沒有執行任何程式。

來源：#1197 的 RESULT（Issue 留言）、`origin/task/1197:docs/reports/issue-1197.md`（`b4538e05`、PR #1202）、#1186 報告 `origin/task/1186:docs/reports/issue-1186.md`，以及 #1186 證據根 `environments/p24/backups/issue-1186-evidence/` 裡的 TSV（唯讀）。

## 1. 已通過（✅），之後不重測

| 編號 | #1197 結果 | 之後處理 |
|---|---|---|
| G01（前置） | ✅ p24svc01 到 CharacterList，3/3 角色 GM0 | 沿用；重測時仍要執行，因為它是進世界的前置步驟，結果照實記錄 |
| N01-2（回歸） | ✅ InWorld，ready/coreComplete/entered=true，decodeFailure=null | 沿用，不列為重測項。本輪沒有 code 任務，進世界的程式路徑沒變。M01 進世界時順便記錄這幾個欄位當佐證 |
| 收尾 | ✅ 自啟程序全退、兩個埠無監聽、17 角色 GM0／離線、原持久 DB SHA 不變 | 沿用；重測自己的收尾照常做 |
| #1188 N01-1、N01-3、N01-4a、N01-4b、收尾，以及 #1188／#1197 已沿用的全部列（#1179 G05–G07、#1173 P01、#1165 G04／F01、#1108 R07、#1150 G02／G03、#1149 S02 系列、#1118 S01、#1126 D01／C01、#1108 B01、R05、R08–R15、C01、C02、V01） | ✅ 沿用 | 繼續沿用 |

沒有任何 ✅ 項目需要附帶回歸：本輪分流沒有改產品程式的任務。

## 2. 失敗項目對應

| 項目 | #1197 結果 | 分類 | 對應 |
|---|---|---|---|
| M01（資源法師進世界後地圖 4／`4/80048001` 可載入） | ⚠️ 角色在 mapId=0（32648,32948），只載入地圖 0，沒有觸發 `4/80048001` | (b) 測試環境／資料問題 | `f1197-map4-fixture`（resource）→ `f1197-retest`（test） |
| （附帶）資源鎖查詢與派工不一致（#1197 報告：holder 顯示 #1186） | 記錄，不是驗收項 | 調度器流程問題，不屬本 repo 產品 | 不開任務；請調度器自行釐清（`needs:orchestrator` 範圍）。本報告只記錄 |

### M01 根因（已驗證，靠讀證據檔，沒有執行程式）
- #1188（05:22Z 冷備份）進世界時，資源法師在 **map 4（33071,32800）**，detail 是「地圖 4 資源無法載入：原始區塊只有尚未支援的 SEG／壓縮格式: 4/80048001」（`origin/task/1188:docs/reports/issue-1188-evidence.json`）。
- #1186 為了避開這個 map4 載圖錯誤，暫時把 10334／1900240001／1900249001 設成 GM，**瞬移到 map 0**。之後只把 AccessLevel 改回 0，**位置沒有改回**。#1186 報告原文：「位置仍為本次測試位置」。
  - `environments/p24/backups/issue-1186-evidence/gm-teleport-before.tsv`：`10334 資源法師 … MapID=4 LocX=33071 LocY=32800`
  - `gm-teleport-after.tsv`：`10334 … MapID=0 32644 32951`
  - `characters-final.tsv`：`10334 資源法師 p24svc01 … MapID=0 32648 32948`
- #1197 用持久 DB `environments/p24/db-data` 的隔離副本，所以資源法師在 map 0。這和 #1197 看到的 (32648,32948) 完全一致。
- 所以 #1197 的 ⚠️ **不能拿來判斷 #1195 的 SEG 修正是成是敗**。程式部分沒有新的缺陷證據，不開 code 任務。

### 為什麼用「凍結 fixture」，不直接改回持久 DB
- 用 SQL 改角色位置，不在 #175 授權列舉的範圍（GM、建角、發物品）。#1209（f1186-smoke-fixtures）也正排隊要寫持久 DB，#1211（f1186-retest）又可能再移動角色。
- `environments/p24/backups/issue-1186-before/db-data` 是 #1186 瞬移前的完整冷備份，已確認存在，而且不在 worktree，不會被清理。把它複製成專用 fixture，就**不必寫任何 DB**，也不受之後其他任務影響。備案是 #1188 的冷備份（`scratch/wt/task-1188/scratch/issue-1188/backup-before/db-data`，目前存在，但 worktree 會被清理）。
- 兩個來源都不符時，fixture 任務停止並回報 `needs:orchestrator`，不得自行 UPDATE 或用 GM 瞬移。

## 3. 開出的任務（`docs/plans/issue-1204.json`）

| id | stage／runner | depends_on | 內容 |
|---|---|---|---|
| `f1197-map4-fixture` | resource／codex-local，runner_lock、real_machine | — | 從 `issue-1186-before`（備案 #1188 冷備份）複製 fixture 到 `environments/p24/backups/issue-<本號>-map4-fixture/db-data`，逐檔核 SHA。在 scratch 隔離副本上開 43324，唯讀 SELECT 確認 10334 在 MapID=4、33071、32800。輸出 `work/f1197-map4/fixture.json`。不寫 DB、不啟客戶端，持久 DB 的 SHA 不變 |
| `f1197-retest` | test／codex-local，runner_lock、real_machine、milestone_smoke | `f1197-map4-fixture` | **只重測 M01**（G01 只當前置）。DB 改用 fixture 的隔離副本，進世界前先 SELECT 核對位置；M01 第 0 步先確認實際 mapId=4、loadedRegion 涵蓋 `4/80048001`，不是就列 ⚠️ 環境並停止。其餘全部沿用 |

## 4. 其他依賴 #1197 的排隊任務

查詢本 repo 所有 open Issue 內文的 `depends_on`：**沒有任何任務依賴 #1197**，不需要關閉或改依賴。內文提到 #1197 的只有 #1211（f1186-retest），內容是「若 #1197 M01 ❌，SM19／SM50 先記受 #1197 阻塞」。#1197 的結果是 ⚠️ 不是 ❌，而且 #1211 的 `depends_on` 是 [1205–1210]，不含 #1197，所以沒有改動。

## 已驗證／推測／待驗證
- **已驗證**（只讀檔）：#1197 結果是 ✅3／❌0／⚠️1；資源法師位置被 #1186 移到 map 0（三份 TSV，加上 #1186 報告原文）；`issue-1186-before/db-data` 與 #1188 冷備份目錄都存在；open Issue 中沒有任何 `depends_on` 含 1197。
- **推測**：`issue-1186-before` 是 13:52（台北）的冷備份，早於瞬移，所以 10334 應該還在 map4。這一點要由 fixture 任務用 SELECT 確認，我沒有啟動 DB。
- **待驗證**：M01 地圖 4 載入（交 `f1197-retest`）；fixture 的位置與 SHA（交 `f1197-map4-fixture`）。

## 給下一棒的提示
- `f1197-map4-fixture`：只複製、只 SELECT，**不要 UPDATE**。fixture 本身不要拿來直接開服，開服一律用 scratch 副本，fixture 才能重複使用。
- `f1197-retest`：DB 來源務必用 fixture，不要用持久 `environments/p24/db-data`。若看到 `Original cache source hash: TW_CLIENT_ROOT/map/4/…seg`，表示客戶端根目錄不對（見 #1195 報告 §3）。
- 給調度器／#1209、#1211：持久 DB 的 10334、1900240001、1900249001 目前都在 map 0（#1186 留下的）。#1211 的 SM19 若需要 map4 角色，可以沿用本輪的 fixture。另外，#1186 沒有改回位置，不符合 #160「smoke 結束後改回原值」，是否補做還原由主控決定。本輪不處理。
- 未做：沒有開 code 任務，沒有改程式，沒有執行任何程式或 DB，沒有改 `docs/REPORT.md`。

## 產出
- `docs/plans/issue-1204.json`（2 張任務）
- `docs/reports/issue-1204.md`（本檔）
