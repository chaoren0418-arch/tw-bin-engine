# #1249：相鄰測試角色持久 fixture

結論：通過。方法 A 普通步行完成，兩角色重新登入仍在 map0 相鄰；已保存可供下游複製的持久 DB fixture。未使用 GM、未直接 UPDATE 座標、未做 R06／SM 判定。PR CI 的最新提交結果見 PR checks 與交付 RESULT。

## 產出與逐項結果

- Git 輸入：`work/f1186-smoke/adjacent-pcs.json`，不含密碼，`storageScope: durable`。
- 持久目錄 `F`：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1249-adjacent-fixture`。
- DB：`F\db-data`；清單：`F\manifest.json`；下表證據均相對 `F\evidence`。截圖與 DB 不進 git。
- 本輪 worktree 暫存：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1249\scratch\issue-1249`。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| P00 隔離與備份 | ✅ | 兩實驗 port 無監聽時，持久 DB 完整冷備份及隔離副本各 1,722 檔／279,511,437 bytes；每檔複製前後 SHA 相符。 | 掃描原 DB、排除 reparse point／硬連結、逐檔核對冷備份與 `lab/db-data`。 | `preparation.json`、`source-manifest.json`、`lease-start.json` |
| P01 普通步行 | ✅ | 法師繞開石牆走到王子旁，約 3 分 32 秒成立；同圖、Chebyshev 距離 1。兩站點周圍 3×3 可走、沒有 NPC 重疊。 | 以地形資料規劃短路點，透過一般地面單擊走路；每段讀回位置，再檢查地形與附近物件。 | `walk-start.json`、`route.json`、`route-moves.json`、`relogin-check.json` |
| P02 GM 備援條件 | ✅ | A 在 15 分鐘內成立，依規定不啟用 B；全程 GM0。此列驗證分支條件，沒有宣稱測過瞬移。 | 核對 A 開始／到位時間及前後 AccessLevel。 | `walk-start.json`、`route-moves.json`、`before.tsv`、`final-characters.tsv` |
| P03 登出及重登入 | ✅ | 兩角色各登出再登入；兩邊 S87 都回報保存座標，`predictedSince=false`，互相出現在附近物件，兩張同框圖均已目視核對。 | 正常關窗登出、重新登入原角色，不再移動，讀回座標並截圖，再登出。 | `after-first-logout.tsv`、`relogin-check.json`、`adjacent-relogin-A.png`、`adjacent-relogin-B.png` |
| P04 最終 SELECT | ✅ | 全部 24 個角色 AccessLevel=0、OnlineStatus=0；指定兩角色仍相鄰。 | 最後登出後，於副本 127.0.0.1:43324 查詢全部角色。 | `final-characters.tsv` |
| P05 收尾 | ✅ | 兩輪共四個客戶端皆正常關窗退出；自啟 Java 已結束、MySQL 以 mysqladmin 正常關閉（exit 0）；自啟程序與 WebView 子程序無殘留，42061／43324 無監聽。 | 依本輪 PID 收尾，再核對程序與監聽。 | `client-A-close.json`、`client-B-close.json`、`client-A2-close.json`、`client-B2-close.json`、`runtime-shutdown.json`、`final-check.json` |
| P06 持久保存 | ✅ | 關庫後新增 F，DB 1,722 檔／279,512,787 bytes，逐檔與副本一致；未覆寫、未刪除，目的地無 junction／symlink／硬連結。原持久 DB 前後逐檔 manifest 完全相同。 | 確認 F 不存在、拒絕連結，逐檔複製並核 SHA；保存後重新核對原 DB。 | `F\manifest.json`、`source-manifest.json`、Git 的 `adjacent-pcs.json` |

## 前後位置與來源識別

| 帳號／角色／objid | 前值 map／X／Y／面向 | 重登入及最終值 map／X／Y／面向 | AccessLevel 前→後 |
|---|---|---|---|
| p24svc01／資源法師／10334 | 0／32648／32948／0 | 0／32579／32974／0 | 0→0 |
| p241db6e／透明王子／1900240001 | 0／32580／32974／5 | 0／32580／32974／5 | 0→0 |

- 步行：2026-11-14 `11:45:20Z` 開始，`11:48:51Z` 到位；重登入確認 `11:53:24.516625Z`（台北 19:53:24）。
- 冷備份保存在本 worktree `scratch/issue-1249/cold-backup/db-data`，實際啟動副本為 `scratch/issue-1249/lab/db-data`。原持久 DB 完全未啟動、未改寫，可繼續作為原始狀態來源；下游應使用 F，不能依賴本 worktree 暫存。
- 原持久 DB 開始與結束 manifest SHA-256 **同為** `f94bf98347b9789f368dc9863a28ab09132ad613cb86373f87bd7283d0f19668`；按相對路徑排序，記錄 path／bytes／sha256。完整清單保存在 `F\evidence\source-manifest.json`；收尾重算結果也留在本 worktree `persistent-after-manifest.json`。
- **Fixture manifest SHA-256**：`4c4e40886102e9165294f2d28e0f0af8349ecbd36db1587612106395171d080c`。manifest 同時收錄 DB 逐檔 SHA、檔數／bytes、原 DB 前後清單 SHA 及證據檔 SHA。
- `adjacent-relogin-A.png` SHA-256：`e5faa58703916ac35f1dc876e003b4d095fc46df2a685abf6eef9cfc6bd81170`。
- `adjacent-relogin-B.png` SHA-256：`572896d8e6bd05cba15d4d38c13aeea8ce08fc9404ea84c4f051a916e1dd4206`。
- 原服 `maps/0.txt` SHA-256：`ece432f3af3cc20bcbf2722dea31a379a9e6ee2b05d7db8940f35ca52ec6bc44`。512×512，座標範圍 `(32256,32768)`–`(32767,33279)`，與實驗庫 `p24_world.mapids` 一致；與引擎 `terrain0.bin` 重疊的 8,192 格全部相同。兩角色各 3×3 的逐格值與附近物件在 `relogin-check.json`，均符合低兩位可通行、無 0x80 阻擋；同框圖另確認站點周圍沒有牆或 NPC。
- #1210 `positions.tsv` 已核對指定 SHA `b2769834a84038a44fe55c127fcbc6c39a2446beab06689c8deb120fc1163baf`；#1229 報告由指定 commit `7ff9e064` 讀取。本次前值另由副本 SELECT 取得，沒有把歷史值當即時證據。

## 建置、範圍與未驗證項目

本輪來源 main `2e8baae74940c215f94844dd050ecfc3a14f687f`，SDK 10.0.300；`engine/build-product.ps1` 建置成功（exit 0），使用暫時 lock、沒有改 rebuild-lock。product DLL SHA-256 `1cb04035a55c3a82bae1d813493a8c0473eec82428849452cbb7b52d9a066046`，建置 log 在本 worktree `scratch/issue-1249/build.log`。設定 dotnet 環境時同時設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`。

初查 lease 曾暫顯示 #1247；開始備份／開服前已核實 holder=#1249，exp=`2026-11-14T14:39:06.917511Z`，與派工相符，未改調度器。只啟動副本的 MySQL／伺服器，僅使用 43324／42061。原專案唯一新增寫入為 F；未改機密設定、產品、`work/f1118-uia-path/`、`docs/REPORT.md`、封存 release 或原 BIN。未停止既有程序，未碰正式服務、登錄檔、權限、系統設定或服務，未移除任何資料／目錄／worktree。

**已驗證**：上表各列；正常登入／步行／登出對隔離副本產生的保存結果，以及原 DB 未變。

**推測**：無需以推測補足本張驗收；沒有把步行中的本地預測當成最終保存證據。

**待驗證／範圍外**：GM 瞬移未執行（A 已成立，無需 GM 還原 SQL）；下游從 fixture 複製後的再次啟動、R06 指定 PC 點選與各 SM 功能留給 `f1229-retest`，本張不判定。沒有為了驗 fixture 而重啟一次已封存的 DB。

## 給下一棒的提示

調度器／`f1229-retest` 先讀 `work/f1186-smoke/adjacent-pcs.json`，核對 F 的 manifest SHA 及每個 DB 檔案，**只從 F\db-data 複製到自己的 scratch 後開服**，不要直接啟動 F 或原持久 DB。登入指定兩角色應直接得到上表相鄰位置；不符時依重測規定列環境失敗，不自行移動。後續照 #1238 分流的重測清單執行；本張沒有其他待補的 fixture 準備項。

本 worktree 內有唯讀資源 junction，交調度器管理；不得遞迴刪除。
