# Issue #1098：P24Lab 就緒與 SM 資料準備

**結論：部分通過（✅ 9／❌ 1／⚠️ 7）。f475-retest 尚不可直接開跑。**

2026-11-13 23:59 至 2026-11-14 00:07（台北）執行資源檢查與準備。MySQL 副本成功監聽 43324，但既有 JAR 因限時物品 `itemObjId=10306` 無效而拒絕開服，42061 未監聽，登入至選角與同時登入均未完成。本報告取代前次唯讀結果，歷史保留於 Git。

## 做了什麼

- 遵守「主專案只讀」：來源為 `D:\lineage開發\TW1608262200研究\environments\p24`；實際啟動與補物品的是 **worktree 的 `environments\p24` 副本**。下文相對路徑以 `D:\lineage開發\TW1608262200研究\scratch\wt\task-1098` 為根。**主專案沒有完成開服或補物品。**
- 先完整冷備份 `db-data`，再建立可寫副本。JAR、依賴及伺服器設定維持原位元組；僅新產生副本 `mysql.ini`，將資料、日誌、暫存導向 worktree，未改機密設定。
- `server/maps` 是指向來源地圖的 junction，未複製 pak／map／sprite。MySQL 使用本機原有 `D:\BinEngine` junction 的 ASCII 路徑，寫入仍在 worktree，沒有新建系統層路徑或改系統設定。
- 直接 DML 僅新增三筆物品；未刪既有資料、未新增帳號、未改密碼、未提升 GM，沒有執行 smoke 或進入世界。
- 23:59 的 `/api/state` 回覆 `leases=[]`，未印證派工所列 lease；依本次「程序疑慮只記錄、不停止」指示記錄，未修改調度器或宣稱鎖已核實。

## 逐項結果

✅ 僅表示該列資源／資料檢查通過，**不表示對應 SM 功能通過**。證據均位於 `scratch/issue-1098/`，不進 Git。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| R01 完整備份 | ✅ | 1,722 檔、278,882,680 bytes，逐檔 SHA-256 相符 | 兩測試埠未監聽時，以唯讀且拒絕其他寫入的 handle 持有來源 ibdata1，複製並核對全目錄 | `snapshot.json`、備份內 `manifest.json` |
| R02 MySQL 43324 | ✅ | 127.0.0.1:43324，實際 PID 5592；datadir 在 task-1098 | 執行下節 MySQL 命令，查監聽及 `@@port,@@datadir` | `mysql2.stderr.log`、`identity.tsv`、`listeners.json` |
| R03 遊戲服 42061 | ❌ | 限時紀錄 10306 找不到有效物件，JAR 拒絕開服，TCP 回 10061 | 在副本 server 目錄執行下節 Java 命令 | `server2.stdout.log`、`ports.json`、`timed-orphan.tsv` |
| R04 登入至選角 | ⚠️ | TCP 即被拒絕，未送帳密，沒有選角回包 | 執行 `protocol.py`，固定連 127.0.0.1:42061 | `login.json` |
| R05 雙角色（SM22／23／35） | ⚠️ | 同帳號已有服務驗證／服務交易，第二帳號已有透明王子；不能驗證兩 session | 查帳號映射；待開服後以兩個既有測試帳號登入 | `characters.tsv`、`accounts.tsv` |
| R06 同帳號雙開 | ⚠️ | **未知**，沒有改登入政策，兩筆角色資料不是雙開證據 | 待開服後保持第一 session，再以同帳號登入第二 session | `ports.json`、`login.json` |
| R07 七職業（SM48） | ⚠️ | 10 個現存角色全為王族；預設 4 欄，龍騎士／幻術師開關 true，但未確認七職實際可建 | 查 Type 與建角開關；待開服正常建角 | `characters.tsv`、`class-settings.txt` |
| R08 弓箭（SM40） | ✅ | 服務驗證已有 172×1、40743×200，未補發 | 查 char_id=1900249001 的背包 | `items-before.tsv`、`items-after.tsv` |
| R09 普通肉（SM46） | ✅ | 40056×19，未補發；不是文件舊值 20 | 同上 | `items-before.tsv`、`items-after.tsv` |
| R10 控制戒指（SM49） | ✅ | 新增 20288×1，另補 40100 瞬移卷×20，均未使用 | 核對模板，執行記錄的 INSERT，獨立讀回 | `applied.sql`、`items-after.tsv` |
| R11 料理書（SM26） | ✅ | 新增 41255「料理書：1階段」×1，未執行料理 | 同上 | `applied.sql`、`items-after.tsv` |
| R12 寵物／召喚（SM25／36） | ⚠️ | 寵物表為空、無技能 51 學習紀錄、全為王族；無法登入準備適用角色與寵物 | 查寵物、技能與角色；待開服準備技能、耗材及寵物關聯 | `pets.tsv`、`characters.tsv` |
| R13 血盟／盟倉（SM24／37） | ⚠️ | clan_data／clan_warehouse 為空，全角色 ClanID=0；無法登入建盟驗證權限 | 查資料；待開服正常建盟、入盟與準備倉庫 | `clans.tsv`、`characters.tsv` |
| R14 金幣（SM19／21／29） | ✅ | 40308×100000，足以支付文件的 30 金幣領取費與普通肉；未實際競標，超額標價須另準備 | 查背包並對照 RUN 最低費用 | `items-before.tsv`、`items-after.tsv` |
| R15 GM／來源未變 | ✅ | 全 10 角色 GM 0；來源 1,722 DB 檔 hash 未變，伺服器設定副本與來源一致 | 完成後重算來源 hash，讀回權限 | `readonly-audit.json`、`characters.tsv` |
| R16 程序收尾 | ✅ | 自啟 PID 全部結束，兩測試埠無監聽，MySQL 正常 shutdown | 核對擁有 PID 後關閉 43324，再查 PID／監聽 | `processes.json`、`shutdown.json`、`mysql2.stderr.log` |
| R17 retest 就緒 | ⚠️ | 主專案未修改，副本仍開服失敗；服務未保持啟動 | 先排除 R03，再補 R04–07、R12–13 | 本報告 |

## 啟動指令與證據

以下以原有 ASCII junction 表示相同路徑。Java 使用 Microsoft JDK 17.0.17，未用系統預設 Java 8。

```powershell
$wt = 'D:\BinEngine\scratch\wt\task-1098'
$lab = "$wt\environments\p24"
$ev = "$wt\scratch\issue-1098"
$mysql = Start-Process 'D:\BinEngine\environments\p24\mysql\bin\mysqld.exe' `
  -ArgumentList "--defaults-file=$lab\mysql.ini",'--console' `
  -WorkingDirectory $lab -WindowStyle Hidden -PassThru `
  -RedirectStandardOutput "$ev\mysql2.stdout.log" -RedirectStandardError "$ev\mysql2.stderr.log"
# 先確認 43324 的 PID 與 @@datadir 指向副本，再開遊戲服。
$env:TEMP = "$lab\server\tmp"; $env:TMP = $env:TEMP
$javaArgs = @('-Xms256m','-Xmx1024m','-Dfile.encoding=UTF-8',
  "-Duser.home=$lab\server\home","-Djava.io.tmpdir=$lab\server\tmp",'-jar','Server_Game.jar')
$game = Start-Process 'C:\Program Files\Microsoft\jdk-17.0.17.10-hotspot\bin\java.exe' `
  -ArgumentList $javaArgs -WorkingDirectory "$lab\server" -WindowStyle Hidden -PassThru `
  -RedirectStandardOutput "$ev\server2.stdout.log" -RedirectStandardError "$ev\server2.stderr.log"
Get-NetTCPConnection -State Listen -LocalPort 42061,43324
```

MySQL 父 PID 19636／實際子 PID 5592，16:02:12 UTC ready；遊戲服 PID 20148 在台北 00:03:10 失敗退出。先前 Unicode MySQL 啟動 PID 19864／1076 失敗，MySQL 未就緒時 Java PID 7064 亦失敗；均已結束。

```text
java.lang.IllegalStateException: character_items_time 未完整載入，拒絕開服；保留原始資料供核對
Caused by: java.sql.SQLException: Timed item missing/invalid; preserve original row itemObjId=10306
```

DB 讀回：`itemr_obj_id=10306`、`usertime=2026-11-07 05:49:36`，`character_items` 無此 id。僅確認背包關聯缺失，**尚未全面核對倉庫／拍賣持有人，不能據此裁定可刪除**。依「不刪既有資料、不改伺服器程式」保留原列，未清除、偽造對應物品或換 JAR 繞過。

## 備份、前後對照與還原 SQL

完整冷備份：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1098\environments\p24\backups\issue-1098-before\db-data`。

涵蓋該 datadir 全部資料庫、系統表、InnoDB 與 MyISAM 檔案。同層 `manifest.json` SHA-256：`5a1bbaad275dfedd5622acbe8c362b9687d8c9df64f3082c21926e8fa1a7da24`。執行 JAR SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。

| 資料 | 前 | 後 | 直接寫入 |
|---|---|---|---|
| 服務驗證：172／40743／40056／40308 | 1／200／19／100000 | 相同 | 無 |
| 服務驗證：20288 | 無 | 1，id 1900251001 | 新增 |
| 服務驗證：41255 | 無 | 1，id 1900251002 | 新增 |
| 服務驗證：40100 | 無 | 20，id 1900251003 | 新增 |
| 全角色 AccessLevel | 0 | 0 | 無 |
| 限時紀錄 10306 | 存在、無背包關聯 | 相同 | 無 |
| 帳號、角色、寵物、血盟 | 既有值 | 未直接修改 | 無 |

寫入前核對 43324、worktree datadir、全角色離線、服務驗證帳號與 GM 0，並查帳號庫物件 ID 欄位排除新 ID 衝突。`character_items`／`characters`／`clan_data` 均為 **MyISAM**，不宣稱可由 ROLLBACK 復原。遊戲服初始化可能自行更新運行資料，完整回復以冷備份為準；人工還原 SQL 只涵蓋補物品。

[還原 SQL](issue-1098-restore.sql) **未執行**；限定 43324 與本 worktree datadir，只移除本次三個新增 ID、模板、擁有者及數量相符的資料，不刪執行前既有資料。後續已使用／轉移的物品需逐筆核對。沒有 GM 提升，因此沒有需執行的 GM 還原 UPDATE。

## 測試帳號／角色（不含密碼）

| 帳號 | 角色 | objid | 狀態與位置 |
|---|---|---|---|
| p24svc01 | 服務驗證 | 1900249001 | 王族59、GM0；物品準備角色；map0 (32644,32951) |
| p24svc01 | 服務交易 | 1900249201 | 王族59、GM0；map0 (32558,32975)，目前與服務驗證不相鄰 |
| p241db6e | 透明王子 | 1900240001 | 王族60、GM0；第二測試帳號；map0 (32640,33109) |

憑證沿用來源 `private-service-test-role.json`、`private-tests.json`，未另設密碼或提交私有檔。另 7 個既有角色見無密碼的 `characters.tsv`；沒有操作保留給其他測試的角色。接手時需安排兩個帳號登入及調整位置，同帳號雙開仍未知。

## 怎麼驗證／未驗證什麼

- 已驗證：備份 hash、來源未變、43324 監聽／datadir、開服錯誤、登入埠拒絕、物品讀回、GM0、程序結束。
- 未驗證：42061 就緒、選角、雙 session、同帳號政策、七職業實際可建、寵物／召喚、血盟／倉庫及全部 SM 操作。本次沒有 GUI，沒有截圖，以 SQL／log／PID 為證據。
- Git：直接在既有 task/1098 merge `origin/main`（de4967d7），無衝突；`docs/REPORT.md` 僅接受 main 版本，未自行編輯，dev lock 未改。
- 同步後執行 `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o engine/build/client-product-dev`：成功，0 錯誤／4 個既有來源警告；依賴由主專案唯讀 `engine/vendor` 還原。另執行 `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-1098/audio-provider.json audio-provider`：**PASS 365**。這是離線合成資料測試，不是 smoke，也不替代開服驗收。
- 建置將 CLI home、NuGet 快取、TEMP 都指向 worktree，同時設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`；未改使用者 PATH。證據：`restore.log`、`build.log`、`test-build.log`、`audio-provider.json`；來源／產物／日誌 SHA-256 清單見 `evidence-manifest.json`。`git diff --check` 通過。

## 產出路徑／給下一棒的提示

追蹤：本報告及 `docs/reports/issue-1098-restore.sql`。證據：`scratch/issue-1098/`。完整備份與已補物品副本：本 worktree 的 `environments/p24/`，**不進 Git，push 不會把準備資料部署至主專案**。

1. **dot／P24Lab 維護任務**接手 R03：先確認 10306 所有持有人表，再依適用備份與維護授權處置，不能把本報告視為刪除授權。本次未修伺服器程式。
2. 調度器清理 worktree 前，安排 dot 保存副本與冷備份；主專案持久就緒需交允許寫入該環境的任務。不得沿 `server/maps` junction 遞迴刪除。
3. 開服後補 R04–07、R12–13、R17；已通過列不重做。若改用另一資料庫，需對照該環境物品／角色，不能沿用本副本結果。
4. 台北 **00:07:05 MySQL 正常關閉**，00:07:28 查得自啟 PID 全部結束、兩埠無監聽。服務未保持至 retest，由 dot 安排後續時段；f475-retest 維持等待。
