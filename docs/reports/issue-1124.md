# Issue #1124：P24Lab 主環境啟動與選角驗證

**結論：部分通過（✅ 16／❌ 0／⚠️ 1）。R03／R04 與九項資料讀回通過；C02 的全專案排他性稽核因歷史目錄拒讀及並行變動保留 ⚠️。未觀察到本次 P24Lab 寫入越界。**

2026-11-14（台北）於既有 `task/1124` 執行；使用主專案持久環境 `D:\lineage開發\TW1608262200研究\environments\p24`，沒有使用 worktree DB 副本。05:02:52 完成資料讀回，05:03:03 開服並登入到選角，05:03:05 完成收尾。

## 逐項結果

證據檔名均相對於主專案 `environments/p24/backups/issue-1124-evidence/`；可隨 Git 檢閱的摘要見 [issue-1124-evidence.json](issue-1124-evidence.json)。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| B01 前置 | ✅ | 啟動前 42061／43324 無監聽；D:\BinEngine 是主專案 junction | 列監聽、TCP 探測與解析 junction 目標 | `preflight.json` |
| B02 冷備份 | ✅ | 1,722 檔、279,503,440 bytes，逐檔 SHA 相同 | 禁止寫入的 ibdata1 handle 保護下完整複製 db-data、逐檔比對 | `snapshot.json`、`../issue-1124-before/manifest.json` |
| B03 DB 身分 | ✅ | MySQL 8.0.40、43324、D:\BinEngine\environments\p24\db-data\ | 使用原 mysql.ini 啟動；SELECT @@port,@@datadir,@@version | `readback.json`、`mysql-listener-process.json` |
| R07 資料面 | ✅ | 七職業 objid／名稱／Type／Class 與 #1108 一致 | SELECT characters，比對下列七筆 | `readback-checks.json` |
| R08 弓箭 | ✅ | 服務驗證 172×1、40743×200 | SELECT character_items，按持有人與 item_id 加總 | 同上 |
| R09 肉 | ✅ | 服務驗證 40056×19 | 同 R08 | 同上 |
| R10 戒指／卷軸 | ✅ | 20288×1、40100×20 | 同 R08 | 同上 |
| R11 料理書 | ✅ | 41255×1 | 同 R08 | 同上 |
| R12 法師／寵物 | ✅ | 資源法師 LV52、技能51、40314項圈×1，關聯寵物1900251199／45034 | SELECT 角色、技能、背包及寵物表 | `readback.json`、`readback-checks.json` |
| R13 血盟／盟倉 | ✅ | 10171 盟主服務驗證 rank4、透明王子 rank8；盟倉40010×1 | SELECT clan_data、成員與 clan_warehouse | 同上 |
| R14 金幣 | ✅ | 服務驗證40308=100000 | 同 R08 | 同上 |
| R15 GM | ✅ | 全部17角色 AccessLevel=0；讀回時均離線 | SELECT 全角色 | 同上 |
| R03 開服 | ✅ | Java PID5760 監聽42061；05:03:03 log 啟動完成，無「拒絕開服」 | JDK17＋原JAR，沿用 #1107 的持久 server 工作目錄 | `listeners.json`、`server.stdout.log`、`server.stderr.log` |
| R04 選角 | ✅ | p24svc01 收S233=0、S178=3/4、三筆S93：資源法師／服務驗證／服務交易 | 沿用 #1107 協定實作，單一session登入後關閉，不送進世界封包 | `login.json`、`packets.json`、`login_probe.py` |
| C01 設定完整性 | ✅ | mysql.ini、原JAR、遞迴 server/config 共24檔 SHA 前後相同 | 啟動前與關閉後逐檔SHA-256 | `protected-before.json`、`protected-after.json`、`protected-comparison.json` |
| C02 寫入範圍 | ⚠️ | 觀察到的44個DB檔與2個JAR日誌檔均授權；但11個歷史目錄拒讀，不能證明全專案沒有其他變動 | 掃描1,434,866檔，列出時間窗內mtime；另列並行worktree／Git變動，不調整權限 | `mtime-changes.json`、`mtime-after.json.gz`、`log-archive.json` |
| C03 收尾 | ✅ | Java5760、MySQL父3060／子17944均結束；兩埠空閒，mysqladmin=0 | 關閉自啟Java；核對MySQL父子PID後正常shutdown | `shutdown.json`、`mysql.stderr.log` |

R07 七筆為：10305資源王族、10314資源騎士、10325資源妖精、10334資源法師、10344資源暗妖、10356資源龍騎、10365資源幻術。此列僅驗證既有資料，不代表 GUI 建角驗收；GUI 項目仍由 #1119 負責。

## 寫入範圍與執行方式

本張明文例外允許主專案 `environments/p24/db-data`、`db-temp`、`db-files`、`db-logs`、原 JAR 在 p24 下產生的日誌／暫存，以及 `backups/issue-1124-*`。程式、設定、封存、正式客戶端仍只讀。報告與一次性腳本僅寫在本 worktree；沒有改 `docs/REPORT.md`、dev lock 或引擎程式，沒有刪除任何目錄或拆 junction。

mtime 稽核時間窗保守涵蓋 04:50–05:05:51（台北），共列出9,650筆：授權MySQL運行44、JAR日誌2、本張備份／證據29、本worktree4,770、其他worktree／共用Git4,805。最後一類為 task-1129 的4,776檔及 `.git` 29檔；本張的fetch／commit／push亦會使用共用Git中繼資料，不能將全部Git變動歸給單一任務。時間窗早於本張worktree建立，因此本worktree清單包含建立時的檔案，並非宣稱全部由本次編輯。

JAR另寫 `server/loginfo/server.log` 與 `server/back/2026-11-14-05-02-52.zip`；讀取ZIP目錄確認後者只有movement／server／teleport的log項目，是原服日誌封存，屬授權日誌範圍。11個拒讀目錄均位於歷史 `work/P26-*`／`work/P35-*` 下的 synthetic測試目錄；完整路徑在JSON，不變更權限。故C02保留的是**全專案排他性證據不足**，不是已證實的越界寫入或開服問題。

此稽核使用檔案mtime時間窗，沒有以目錄mtime略過子目錄；略過reparse目錄以避免沿junction重複追蹤。它無法識別已刪除檔案或保留舊mtime的新副本；本張未執行刪除，1,722份冷備份副本另由manifest完整列出。稽核後新增的證據摘要／manifest與本報告也分別位於核准證據目錄及本worktree。

啟動命令與程序證據收在 `runtime.ps1`、`mysql-process.json`、`mysql-listener-process.json`、`server-process.json`。使用既有 `D:\BinEngine` ASCII junction，原 mysql.ini 不變；Java 為 Microsoft OpenJDK17.0.17，參數為 `-Xms256m -Xmx1024m -Dfile.encoding=UTF-8 -Duser.home=D:\BinEngine\environments\p24\server\home -Djava.io.tmpdir=D:\BinEngine\environments\p24\server\tmp -jar Server_Game.jar`，工作目錄為同環境 `server`。僅以子程序參數允許執行本張 PowerShell 腳本，未變更系統執行原則。

MySQL 最初回傳父PID10516、監聽子PID5432；採證腳本一開始只比父PID，因此誤判等待逾時。確認父子關係後沿用該實例，但 mysql.exe 無法開啟中文路徑的 defaults 檔，尚未送出SQL即退出。該實例已正常shutdown；將採證腳本改用 #1107 既有ASCII路徑後，第二次啟動完整通過。中間紀錄保留於 `initial-*`、`launcher-observation.json`；沒有修引擎、MySQL或設定，也沒有把這兩項採證問題當成伺服器故障。MySQL 的既有 component_reference_cache／ICU／自簽憑證訊息未阻止啟動，未修改。

04:52初查API的lease持有人為#1119，當時依派工「程序疑慮只記錄、不停止」記錄。**05:00:45正式啟動前的 `preflight.json` 已顯示持有人 `chaoren0418-arch/tw-bin-engine#1124`、到期2026-11-13T22:21:59.495938Z，與派工時間吻合且有效。** 本張沒有修改調度器或鎖；啟動前確認測試埠空閒、備份時持有禁止寫入handle，程序僅按本張啟動證據管理。

## 備份與驗證限制

完整冷備份：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1124-before\db-data`；manifest SHA-256：`d8565746e17d061f7df429e1747be7b3180bafd5d54453885679bb7ea2e1e999`。原 JAR SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。

資料核對僅送出 `readback.sql` 中的 SELECT，沒有DML、建角、補物品或設GM。伺服器啟動及登入自身可能更新運行資料，因此不宣稱DB逐byte不變。沒有直接DML需要逆向SQL，也沒有執行還原；若需整體回復，由持有實驗環境資源鎖者另行處理完整冷備份。

未執行客戶端、GUI、進世界、R06雙開或SM smoke；無截圖，以真實SQL讀回、TCP明文解碼回包、程序與log作證。沒有連正式2000／3306。

提交後main新增#1129報告，因此另合併 `c91763653852e19ce2a74f4ceed6c88443d3513e`；只有兩份文件新增，沒有程式變動。依常設要求補做產品build（0錯誤／4個既有警告）與LoginChecks（396,589 assertions通過；network/game/database均false）。證據為 `build-check.ps1`、`build.log`、`login-checks.log`；此離線檢查不替代本次實機回包。所有CLI home、NuGet快取、TEMP與建置產物都在本worktree，並同時設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`。`git diff --check`與報告17列／JSON一致性核對通過；未改 `docs/REPORT.md` 或dev lock。

## 給下一棒的提示

1. dot／codex-local 可沿用本張 R03／R04 與九項資料讀回，接續 #1120 的後續重測；不要重放 #1107 孤兒處置或 #1108 補資料。
2. 本張已選擇收尾，**服務未保持啟動**；下一張需依相同寫入範圍重開持久環境。不要把端口空閒判成拒絕開服。
3. 原始證據與冷備份已在主專案核准路徑，不依賴本worktree保留；Git只提交本報告與摘要JSON。
4. **調度器／本機主控**只需處理C02的全專案稽核界線；不得因這項證據限制重跑已通過的R03／R04或補資料。本張沒有修改歷史目錄權限來強行通過稽核；啟動前lease已核實有效。
