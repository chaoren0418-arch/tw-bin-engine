# Issue #1107：P24Lab 孤兒限時紀錄處置與選角驗證

**結論：通過（✅ 10／❌ 0／⚠️ 0）。R03／R04／R06 均已取得實機證據。**

2026-11-14 00:56–01:05（台北）在主專案持久環境 `D:\lineage開發\TW1608262200研究\environments\p24` 執行。完整備份後確認 10306 沒有持有人，只刪除這一筆孤兒限時列。原 JAR 成功監聽 42061，測試帳號登入到選角；同帳號第二次登入遭拒，舊、新連線隨後均被關閉。未進入世界。

## 逐項結果

下表證據檔名相對於持久證據目錄 `environments/p24/backups/issue-1107-before/evidence/`；worktree 的 `scratch/issue-1107/` 亦保留工作副本。可隨 Git 檢閱的摘要與原始回包見 [issue-1107-evidence.json](issue-1107-evidence.json)。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| B01 冷備份 | ✅ | 1,722 檔、279,489,484 bytes，逐檔 SHA-256 相符 | 兩測試埠未監聽時持有禁止寫入的 ibdata1 讀取 handle，完整複製 db-data 並比對 | `snapshot.json`、備份同層 `../manifest.json` |
| B02 MySQL 身分 | ✅ | 127.0.0.1:43324、MySQL 8.0.40、持久 datadir；PID 16232 | 依下節命令啟動，再查 `@@port,@@datadir` 與監聽 PID | `identity.json`、`mysql-process.json`、`listeners.json` |
| D01 全面持有人查核 | ✅ | 全部限時列只有 10306；223 表／2,148 欄僅命中限時表本列 | SHOW TABLES／欄位盤點，掃所有限時列與候選 ID 的持有表 | `schema.json`、`tables-*.tsv`、`holder-audit.sql`、`holder-audit.json` |
| D02 最小資料處置 | ✅ | 寫入鎖內重新查得持有人 0，刪 1 列；限時表由 1 列變 0 列 | 先輸出 INSERT 還原 SQL，再鎖表查核並按 ID＋時間精確刪除，獨立讀回 | `timed-before.json`、`timed-after.json`、`delete-result.tsv`、`applied.sql` |
| R03 42061 開服 | ✅ | PID 18832 監聽 42061；01:02:11 log 顯示啟動完成，無拒絕開服訊息 | 用 JDK 17 與原 JAR 在持久 server 目錄啟動 | `listeners.json`、`server.stdout.log`、`server.stderr.log`、`java-version.txt` |
| R04 登入至選角 | ✅ | p24svc01 收 S233=0、S178=2/4、兩筆 S93：「服務驗證／服務交易」 | 協定探針連 127.0.0.1:42061，送 C119，只接收到選角 | `login-first.json`、`packets.json`、`login_probe.py` |
| R06 同帳號雙開 | ✅ | B 收 S233=22；A、B 均收 S227 後 EOF，不能同時維持選角 | 保持 A 選角 session，再以相同帳號登入 B，持續觀察兩連線 | `login-dual.json`、`packets.json`、`server.stdout.log` |
| C01 變更範圍 | ✅ | JAR、mysql.ini、server/config 共 14 檔 hash 不變；直接 DML 前後只有限時表 checksum 改變 | 比對保護檔 SHA 與 223 表 CHECKSUM；讀回全角色 GM0／離線 | `protected-before.json`、`checksums-before.tsv`、`checksums-after.tsv`、`final-audit.json` |
| C02 程序收尾 | ✅ | 自啟 PID 18832／18776／16232 均結束，兩埠無監聽；MySQL 正常 shutdown | 核對 PID 身分後停止 Java，以 mysqladmin 關閉自己持有的 43324 | `shutdown.json`、`mysql.stderr.log` |
| V01 同步後檢查 | ✅ | main 同步後產品建置 0 錯誤／4 個既有警告；登入離線檢查 396,589 assertions 通過 | 執行 `build-check.ps1` 的 restore、build、LoginChecks；git diff --check | `build-check.ps1`、`build.log`、`login-checks.log` |

## 持有人判定與前後對照

先以實際 SHOW TABLES 和 information_schema 盤點，不預設表名。`p24_accounts`、`p24_cross`、`p24_lito`、`p24_transit` 掃所有數值欄及 ID 類字串欄；`p24_world` 額外掃物品、商店、寵物、召喚等相關模板表。沒有查詢或修改正式 3306；其他 `*_lito_exp` 資料表沒有做 DML。

| 範圍 | 表數 | 欄數 | 10306 命中 |
|---|---:|---:|---|
| p24_accounts | 69 | 657 | 僅 character_items_time.itemr_obj_id |
| p24_cross | 69 | 657 | 0 |
| p24_lito | 19 | 128 | 0 |
| p24_transit | 34 | 379 | 0 |
| p24_world 相關表 | 32 | 327 | 0 |

主要持有／關聯欄位逐表查核均為 0：

| 表 | 查核物件欄位 |
|---|---|
| character_items／character_warehouse／character_elf_warehouse／clan_warehouse | id |
| character_warehouse_for_cha | item_objid |
| character_pets | item_obj_id、objid |
| character_items_card／character_items_dark | item_objid |
| character_items_doll | item_obj_id |
| 紀錄_拍賣 | id |
| 紀錄_拍賣拖售 | id、item_obj_id、auction_item_id |
| 紀錄_拍賣娃娃／紀錄_拍賣打洞／紀錄_拍賣暗黑 | auction_item_id |
| 紀錄_拍賣領取 | id、auction_item_id |
| log_商店收購／log_商店購買 | itemobjid |

另涵蓋跨服／轉移庫的同類持有表與商店、召喚相關實際表；沒有找到另一張獨立持有召喚物品的表。完整欄位、查詢及零結果保留於 `holder-audit.*`，上表不是僅憑背包推斷。

`character_items_time` 為 **MyISAM**，不依賴 ROLLBACK。第一次鎖定查詢因同表多次參照被 MySQL 拒絕，尚未執行 DELETE；調整為每表一個條件集合後，在同一連線鎖定持有表 READ／限時表 WRITE，重新查得 `holder_hits=0` 才刪除。

| 資料 | 前 | 後 |
|---|---|---|
| itemr_obj_id=10306 | usertime=2026-11-07 05:49:36 | 無此列 |
| 限時表總列數／全部背包孤兒數 | 1／1 | 0／0 |
| 限時表 CHECKSUM EXTENDED | 3901388329 | 0 |
| 其他 222 張已查核表 checksum（直接 DML 前後、開服前） | 原值 | 相同 |

遊戲服初始化與登入本身可能更新運行資料，不能把開服前 checksum 結果解讀成整輪 DB 位元組完全不變。沒有直接補物品、改 GM、改密碼或改登入政策。

## 備份、還原與證據保存

- 完整冷備份：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1107-before\db-data`，涵蓋 datadir 內全部資料庫與系統檔；來源無 junction／symlink／多重硬連結。
- 同層 `manifest.json` SHA-256：`916a407ad33ec6f387c42331763f9f9c138d5d98537bc92b940caecc0f940ffc`。
- [還原 SQL](issue-1107-restore.sql) 在刪除前已產出，**未執行**。INSERT 有 port、datadir、ID 不存在條件；若還原孤兒列而未補正持有人，原 JAR 會再次拒絕開服。完整回復使用冷備份。
- 持久證據目錄：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1107-before\evidence`，共 49 個檔案，含 SQL、查詢結果、回包、log、腳本與建置紀錄；不含密碼或私有憑證檔。
- `evidence/evidence-manifest.json` SHA-256：`5889208dfe75939023bb9036c293abddaff6b5fa71a520280cad95390770631b`。證據已複製到持久環境，後續清理本 worktree 不會遺失。
- JAR SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；JDK Microsoft OpenJDK 17.0.17。

## 啟動指令與回包

以下沿用既有 `D:\BinEngine` ASCII junction，實際資料仍在主專案持久環境。原 mysql.ini、server/config 均未改。

```powershell
$lab = 'D:\BinEngine\environments\p24'
$ev = 'D:\BinEngine\scratch\wt\task-1107\scratch\issue-1107'
$mysql = Start-Process "$lab\mysql\bin\mysqld.exe" `
  -ArgumentList "--defaults-file=$lab\mysql.ini",'--console' `
  -WorkingDirectory $lab -WindowStyle Hidden -PassThru `
  -RedirectStandardOutput "$ev\mysql.stdout.log" -RedirectStandardError "$ev\mysql.stderr.log"
# 核對 43324 的 PID 及 SELECT @@port,@@datadir；本次 datadir 為 D:\BinEngine\environments\p24\db-data\。
$env:TEMP = "$lab\server\tmp"; $env:TMP = $env:TEMP
$javaArgs = @('-Xms256m','-Xmx1024m','-Dfile.encoding=UTF-8',
  "-Duser.home=$lab\server\home","-Djava.io.tmpdir=$lab\server\tmp",'-jar','Server_Game.jar')
$game = Start-Process 'C:\Program Files\Microsoft\jdk-17.0.17.10-hotspot\bin\java.exe' `
  -ArgumentList $javaArgs -WorkingDirectory "$lab\server" -WindowStyle Hidden -PassThru `
  -RedirectStandardOutput "$ev\server.stdout.log" -RedirectStandardError "$ev\server.stderr.log"
python -B -X utf8 scratch/issue-1107/login_probe.py
```

R04 在 01:02:37 成功：S233 `e9000000000000000000000000000000`；S178 `b202040000000000` 表示 2 角色／4 槽。兩筆 S93 的原始明文 hex 與解碼名稱均保存於 Git 證據 JSON；C119 僅記 opcode／長度，未記錄帳密 bytes。

R06 同秒保持 A（本機埠 60206），建立 B（60207）登入相同帳號。B 收 `e9160000000000000000000000000000`（S233=22），log 記錄 `同服重複登入(記憶體): account=p24svc01 IP=127.0.0.1`。B 收兩筆 S227、A 收一筆 S227，內容皆 `e3f4010000000000`，約 2 秒內兩者均 EOF，發生在探針自行 close 之前。結論是**拒絕新登入並踢除舊連線，兩端都斷線**；僅涵蓋停留選角的情境，不推論進世界後的政策。

## 驗證範圍與給下一棒的提示

已驗證本張要求的冷備份、孤兒處置、R03／R04／R06 與收尾；沒有執行 GUI、進世界或 smoke，沒有截圖，以真實 SQL／TCP 回包／程序與 log 為證。主專案除授權的 `environments/p24` 外未寫入；未沿用 task-1098 的資料庫副本，也未移入其補發物品。

00:58 保存的 `/api/state` lease 持有人為 `chaoren0418-arch/tw-bin-engine#1107`，exp 對應 `2026-11-13T18:26:41Z`，執行時有效。最初顯示判讀疑慮已以原始 JSON 核實更正，未操作調度器狀態。

工作直接在既有 `task/1107`，fast-forward 同步 main `8c77fd91`；`docs/REPORT.md` 僅接受 main 內容，dev lock 未改。同步後產品 build 通過，LoginChecks 的 396,589 assertions 是離線合成輸入驗證，不替代本次真機回包。CLI home、NuGet 快取與 TEMP 都位於 worktree，並設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`。

1. **dot／f1098-test-data** 可在此持久環境準備下一張資料；本張已解除限時孤兒開服阻塞，不需重做已通過的 R03／R04／R06。
2. 同時登入測試須用兩個不同測試帳號；本次同帳號第二登入會把第一 session 也斷開，不能拿同帳號兩個角色當成雙 session。
3. 依本張結束前關閉自啟程序的明文要求，01:03:30 停止自己啟動的 Java，MySQL 01:03:31 正常關閉，01:03:53 確認兩埠空閒。Java 使用 PID 停止，未宣稱執行應用層 graceful shutdown。**服務未保持啟動**；下一時段由 dot 決定何時重開與是否維持。
4. #1098 的其他未通過資料準備項目仍由後續任務處理；本報告不宣稱七職業、寵物／召喚、血盟／倉庫或全部 SM 已就緒。
