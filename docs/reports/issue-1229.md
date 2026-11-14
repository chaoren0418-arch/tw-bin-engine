# #1229：相鄰測試角色持久 fixture 準備

結論：部分通過（✅ 2／❌ 0／⚠️ 4）。已完成備份與無自啟程序的收尾檢查；尚未產生可用的相鄰角色 fixture。未執行 R06／SM 判定。

## 未完成原因

本次派工開頭明定原專案 `D:\lineage開發\TW1608262200研究`「只讀取、不要在那裡寫檔」，任務內容卻要求在該目錄的 `environments/p24/db-data` 保存角色位置。本 worktree 沒有持久 DB。即使只用普通步行，啟動 MySQL、登入與登出仍會寫入原專案，因此已詢問是否將該持久 DB 明列為可寫例外；截至本次交付未收到確認。這是派工中的路徑限制衝突，不是 AGENTS.md 的一般真機限制，也不是引擎或工具失敗。

沒有另跑副本冒充持久成果，沒有直接 UPDATE 角色座標。先完成不依賴該確認的備份、來源識別與證據整理。

## 逐項結果

證據根 `W`：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1229`。以下證據路徑相對 W；備份與 scratch 均不進 git。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| P00 備份 | ✅ | 42061／43324 無監聽時，複製持久 DB 全部 1,722 檔、279,511,437 bytes；每檔複製前後與副本 SHA 一致，全部複製後再核原件一致。備份放本 worktree，以遵守原專案唯讀。 | 執行備份腳本；比較 manifest 各檔與原件、副本 SHA。 | `scratch/issue-1229/backup.py`、`backup-summary.json`；`environments/p24/backups/issue-1229-before/manifest.json` |
| P01 普通步行到位 | ⚠️ | 未啟動持久 DB 或客戶端；未開始步行計時，無本次即時座標及 3×3 通行證據。 | 路徑限制釐清後，登入指定兩角色，以普通地面點選繞路到同圖相鄰開闊地面。 | `scratch/issue-1229/preflight.json`；本報告「未完成原因」 |
| P02 暫設 GM 備援 | ⚠️ | 尚未符合「步行 15 分鐘不成立」前提，未設 GM、未瞬移、未執行 SQL 寫入。 | 只有 P01 實際嘗試仍不成立才依 B 準備，記錄 AccessLevel 前後並恢復。 | `scratch/issue-1229/preflight.json` |
| P03 保存與重登入 fixture | ⚠️ | 尚無雙方登出再登入、相鄰座標讀回或同框圖；未建立 `adjacent-pcs.json`，避免下游誤用。 | 完成 A 或 B 後，各登出再登入，確認同圖 Chebyshev 距離 1、面向及周圍地面，保存同框圖及 fixture。 | 本報告；沒有本次遊戲截圖 |
| P04 最終角色 SELECT | ⚠️ | 未啟動 DB，未查詢本次 AccessLevel／OnlineStatus，不能沿用歷史值宣稱全角色為 0。 | 具備持久 DB 可寫例外後，在最終登出後 SELECT 全角色狀態並留證，再關庫。 | `scratch/issue-1229/preflight.json` |
| P05 程序與監聽收尾 | ✅ | 本次未啟動任何遊戲、Java、MySQL 或登入器；42061／43324 無監聽。 | 核對自啟 PID 清單與兩實驗 port 的 Listen 狀態。 | `scratch/issue-1229/final-check.json` |

## 備份與來源識別

- 備份完成時間：`2026-11-14T09:32:30.308499Z`。原件為原專案 `environments/p24/db-data`，副本為 `W\environments\p24\backups\issue-1229-before\db-data`。
- manifest SHA-256：`105dfc633f9eb524d4ab94f380f38c960a2870234ee4a3dbe235e6cf6ab68638`。備份核對期間原件未變；本次沒有啟動資料庫驗證備份可恢復。另有既存 `mysqld.exe` PID 4144，無法讀取其命令列；未附加、停止或連線該程序，不將其推定為 P24Lab。
- #1210 `positions.tsv`：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1210\scratch\issue-1210\positions.tsv`，SHA-256 `b2769834a84038a44fe55c127fcbc6c39a2446beab06689c8deb120fc1163baf`。
- #1186 參考圖：原專案 `environments/p24/backups/issue-1186-evidence/smoke-B/m7-SM22-B-1.png`，SHA-256 `39d8e7250fc0ddae8c3d39545fa3ff96964f19bcfe6134beb914e61b3d45e3cd`。圖中兩人靠近石牆，不能單憑該圖證明 3×3 無牆；不是本次驗收圖。

以下是 #1210 的歷史值，**不是本次 DB 查詢或確認後座標**：

| 帳號 | 角色 | objid | map | X | Y | AccessLevel |
|---|---|---|---|---|---|---|
| p24svc01 | 資源法師 | 10334 | 0 | 32648 | 32948 | 0 |
| p241db6e | 透明王子 | 1900240001 | 0 | 32580 | 32974 | 0 |

本分支沒有 `work/f1186-smoke/fixtures.json`，未替換指定角色。沒有本次後座標／面向／AccessLevel 讀回；因未用 B，沒有需執行的 GM 還原 SQL。

## 資源鎖與變更範圍

- 初查 `/api/state` 的 p24lab holder 是 #1211，exp=`2026-11-14T10:03:03Z`，原始觀察存 `scratch/issue-1229/leases-observed.json`。
- `2026-11-14T17:31:55+08:00` 再查已為 `chaoren0418-arch/tw-bin-engine#1229`，exp=`2026-11-14T10:59:41Z`，與派工一致，見 `preflight.json`。鎖差異已解除，**不是交付時的未完成原因**；未修改調度器或 lease。
- 只在本 worktree 寫入備份、scratch 與本報告；未更動原專案、秘密設定、產品程式、`work/f1118-uia-path/`、`docs/REPORT.md` 或 rebuild-lock。
- 未連正式 2000／3306，未改 DB、登錄檔、服務或權限；未刪除資料、目錄、junction 或 worktree。本次只有備份與文件，未重建或重跑產品測試。

## 給下一棒的提示

調度器／本機執行器先釐清原專案唯讀規則是否排除本張持久 DB 的必要寫入。若要保留原專案唯讀，必須先由任務擁有者指定新的持久 DB 路徑及後續任務共用方式；不能自行建立暫存副本就宣稱完成。

釐清後接續 P01–P04；P00 備份已完成，但若來源已變動，寫入前須重新備份。先一般步行，必要時才按 B 暫設單一角色 GM；保存角色位置後重登入確認，產生真正的 `work/f1186-smoke/adjacent-pcs.json` 與同框圖。P05 在實際啟停程序後須再做收尾。重測工作不要把歷史參考圖或上述歷史座標當成已就緒 fixture。
