# #1222：公告板／盟屋拍賣板測試條件調查

結論：部分通過（✅ 5／❌ 0／⚠️ 2），屬 resource 調查，**不是 SM27／SM29 驗收**。兩項目前停在 **C 授權閘門**：A 沒有現成板子；已找到不改出生表的 B 候選 `.2`，但 GM200 會自動寫入 `log_GM指令`，不符合任務 B「不寫 DB」的字面條件。未執行召出、設 GM 或任何資料寫入 SQL；不能宣稱只能改世界表，也不能判為 D「伺服器不支援」。

使用者尚未裁定前，`readyForRetest=false`。請優先裁定是否接受 **B：保留正常 GM 稽核紀錄、NPC 不持久化**；若不接受，可列為已知測試限制。世界表單列替代方案列於下方，**不是已取得授權的操作**。

## 逐項結果

E＝[核對證據](issue-1222-evidence.json)，F＝[fixtures](../../work/f1186-smoke/fixtures.json)；原始 SELECT 與 javap 輸出在本 worktree `scratch/issue-1222/`。

| 編號 | 結果 | 實際觀察 | 重現／核對方式 | 證據 |
|---|---|---|---|---|
| B01 備份保全 | ✅ | #1209 的 1,722 檔、279,507,053 bytes 全部保全；含 manifest 新增1,723檔，無覆寫 | 先核來源，再逐檔複製並核目的地 SHA | E `backupPreservation` |
| A01 現成出生 | ✅ | 一般板7種模板無出生；81161四列均count0；拍賣管理者不等於拍賣板 | 全出生表及其他整數NPC欄位查詢，條件見下文 | E `queries`／`supplement` |
| J01 動態／GM路徑 | ✅ | 找到 `.2` 暫時 NPC 與 `.npc` 持久出生的不同路徑；未找到板子自然出生路徑 | 2,740 class 常數池＋相關 javap 方法 | E `constantScan`／`bytecode`；`L1SpawnCmd.class.txt` |
| R12／SM27 | ⚠️ | C待裁定；B候選 `.2 80006 1 0` 不持久化NPC，但會增加GM稽核DB紀錄 | `commands`→`GMCommands`→`L1SpawnCmd`，未執行 | F `locations`／`blockedFixtures`；E `commands` |
| R13／SM29 | ⚠️ | C待裁定；B候選 `.2 81161 1 0` 必須在map4精確座標；同樣有稽核寫入 | `S_AuctionBoard.buildPacket`座標篩選＋拍賣表SELECT，未執行 | F；E `auctionRegions`／`auctionSummary` |
| D01 拍賣功能／資料 | ✅ | 處理器與啟動路徑存在；62筆未到期拍賣，不能判為不支援 | 啟動、計時器、清單、Bid條件位元碼；非機密設定與DB讀回 | E；`Npc_AuctionBoard.class.txt`、`StartTimer_Server.class.txt` |
| C01 收尾／範圍 | ✅ | 查詢副本MySQL正常關閉；兩埠無監聽；持久DB 1,722檔SHA全相同 | 自啟PID核對後正常shutdown；逐檔前後比對 | E `shutdown`／`persistentManifestHashes` |

## 查了哪些資料

查詢使用**本輪目前持久 DB 的冷複本**，不是 #1209 舊備份；`@@datadir` 指向本 worktree 的 `scratch/issue-1222/runtime/db-data`，`@@port=43324`、`read_only=1`、`super_read_only=1`。未開遊戲伺服器或客戶端，未連正式服務。所有查詢只用 SELECT／SHOW；MySQL自身運行檔只落副本。

NPC篩選：`LOWER(impl) LIKE '%board%' OR LOWER(impl) LIKE '%auction%' OR LOWER(classname) LIKE '%board%' OR LOWER(classname) LIKE '%auction%'`，另外以名稱「公告／佈告／拍賣」擴大搜尋，共18種候選。真正一般板為 `classname='other.Npc_Board'` 的 **80006、81126–81130、81201**；拍賣板為 `other.Npc_AuctionBoard` 的 **81161**。它們的 `impl` 都是 `L1Merchant`，只查 `impl=L1Board` 會漏掉。

| 表／查詢 | 板子結果 |
|---|---|
| `p24_world.spawnlist_npc` 依 `npc_templateid` 聯結npc，並篩 `count>0` | 0列；不篩count時只有81161四列 |
| `spawnlist`、`spawnlist_npc_box`、`spawnlist_light`、`spawnlist_time`、`spawnlist_定時` | 0列 |
| `spawnlist_scenery`、`spawnlist_door`，以候選板gfxid查詢 | 0列；這兩表不是NPC模板出生 |
| `spawnlist_trap` | 欄位是trapId，無NPC模板；另掃其他整數NPC欄位，無板子參照 |
| `p24_accounts.server_boss出生`、`p24_cross.server_boss出生`、`p24_lito.lito_battle_spawn` | 0列 |
| `npc_死亡召喚`（npcid及changeto）、`道具_自訂怪物召喚` | 0列 |
| 五個非`*_lito_exp` P24 schema 的所有整數 `%npc%`、`changeto`、`mob_id` 欄位 | 板ID只出現在npc定義及81161四筆出生列 |

有兩筆 `70577` 拍賣管理者count1，座標4 `(33961,33253)`／`(33423,32823)`；`classname=0`、`npcaction=auction1`，不是 `Npc_AuctionBoard`，不能冒充SM29入口。

JAR SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。`NpcSpawnService.load`讀`spawnlist_npc`，`count=0`直接略過；`HouseTable`、`ServerAuctionTimer`檢查與結算資料，未見召板呼叫。全JAR常數池沒有上述板ID的整數常數；板處理器參照只有本身及`C_NPCAction`路由。**這是靜態調查未找到自然出生的證據，不是執行所有反射／動態路徑的證明。**

## B候選：方法、存活時間與實際副作用

`p24_world.commands`：名稱 **`2`**、access_level **200**、class_name **L1SpawnCmd**，不是`.spawn`。語法 `.2 NPCID [數量=1] [範圍=0]`。`L1SpawnCmd.execute`→`spawnAndLog`→`L1SpawnUtil.spawnRx(location,npcId,showId,range,0)`；範圍0取GM當前位置，建立NPC後只註冊記憶體世界物件，未呼叫`storeSpawn`。最後參數0不設定到期計時，**沒有固定秒數保證**，持續至NPC刪除或遊戲伺服器停止；退出GM角色或還原GM0不等於重開伺服器。

真正副作用：`GMCommands.executeDatabaseCommand`在執行後呼叫`ServerGmCommandTable.create`→`AuditLogService.logGmCommand`→`insertGmCommand`，經`DatabaseFactoryLogin`執行：

```sql
INSERT INTO `log_GM指令` SET `gmobjid`=?,`gmname`=?,`cmd`=?,`time`=?
```

因此只能說「不寫出生表／不持久化NPC」，**不能說完全不寫DB**。本張沒有關閉、規避稽核或提高權限來跳過稽核；現有GM紀錄9列只是唯讀核對。`.npc NPCID`會呼叫`NpcSpawnTable.storeSpawn`寫出生表，禁止拿來替代；`.summon`建立的是`L1PetInstance`，也不是這次的板子方法。

若使用者接受上述B副作用，後續重測才按 #160／#175：完整備份→測試角色離線暫設GM200→登入站到指定點→各召一次→記錄NPC objid及座標→角色移開並登出→改回GM0→在**同一遊戲伺服器程序**重新登入驗收。不得為降GM重開遊戲伺服器，否則板子會消失。收尾停止自己啟動的伺服器清除暫時NPC。本張全部未執行。

- SM27：`.2 80006 1 0`；候選準備點map4 `(32612,32775)`，未驗證地形／碰撞。`Npc_Board`支援BoardProtocol；讀文如為空清單，應由一般玩家正常發文建立內容，不直接造DB文章。
- SM29：站在map4 **`(32611,32775)`**，執行`.2 81161 1 0`。範圍必須0；`S_AuctionBoard.buildPacket`只依板子精確x/y分流，隨機落在旁邊會是空清單。

## 拍賣功能不是停用的證據

`GameServer.initialize`載入`AuctionBoardTable`／`HouseTable`，並啟動`StartTimer_Server.start`；後者排程`ServerAuctionTimer`每600,000ms執行，該路徑未見盟屋拍賣啟用旗標。`Npc_AuctionBoard.talk`直接建立S156，`action(select/apply)`支援資訊及Bid；`AuctionBoardPersistenceService.load`讀`server_盟屋_拍賣`。非機密`altsettings.properties`是`HouseTaxInterval=10`、`HouseTaxAdena=2000`，未改設定；沒有讀取`server.properties`或`sql.properties`。

`server_event.AuctionSet.eventstart=1`是**道具拍賣系統**，不能用它代替盟屋拍賣開關證據。81161四列count0能證明這些出生列停用，無法證明停用意圖或整個功能不支援。

`p24_accounts.server_盟屋_拍賣`有62列，均無bidder、目前價100,000、期限**2026-11-19 01:00:00（DB本地時間，UTC+8）**；查詢時62列均未到期。`server_盟屋_紀錄`與`server_盟屋_佈告`均0列，未補資料；成交／交屋結果不在本張驗證範圍。

| 板map4座標／spawnId | 對應house_id | 現行拍賣列數 | 原count |
|---|---|---:|---:|
| `(32611,32775)`／1016 | 524289–524294 | 6 | 0 |
| `(33959,33253)`／1017 | 458753–458819 | 0 | 0 |
| `(33585,33235)`／1018 | 327681–327691 | 11 | 0 |
| `(33421,32823)`／1019 | 262145–262189 | 45 | 0 |

Bid入口要求LV15以上王族、本人是盟主、沒有盟屋。現有「服務驗證」1900249001是LV59王族／資源血盟10171盟主、hashouse=0、hascastle=0、GM0／離線，金幣100,000；可作候選。這些只證明資料及開窗前置，**不保證足夠完成加價成交**；重測應先重新確認期限、所需最低價與金幣，不把後端拒絕出價冒充SM通過。

## C備用單列SQL：僅供裁定，未執行

如B稽核紀錄不被允許，而使用者改選「只限本次、只改這一列、測完還原」，每個SM須各自選一項，**不是一次授權兩列**。執行者須另持有效lease、先完整冷備份、確認43324及正確持久datadir、遊戲伺服器停機，再核前值。此表是MyISAM，不能用ROLLBACK保證還原；任何前值變動就停止重新評估。

R12：`id=39`目前不存在（查到的空洞，小於AUTO_INCREMENT=1346），80006無出生。新增一列的草案及精確還原：

```sql
SELECT * FROM p24_world.spawnlist_npc WHERE id=39;
INSERT INTO p24_world.spawnlist_npc
 (id,location,count,npc_templateid,locx,locy,randomx,randomy,heading,respawn_delay,mapid,movement_distance)
VALUES (39,'issue-1222-SM27',1,80006,32612,32775,0,0,5,0,4,0);
-- 測完、停服、重新備份及核對整列後才可還原；影響列數必須恰為1。
DELETE FROM p24_world.spawnlist_npc
WHERE id=39 AND location='issue-1222-SM27' AND count=1 AND npc_templateid=80006
 AND locx=32612 AND locy=32775 AND randomx=0 AND randomy=0
 AND heading=5 AND respawn_delay=0 AND mapid=4 AND movement_distance=0;
```

R13：只改現有1016，前值完整見E；後值僅count 0→1，還原1→0：

```sql
SELECT * FROM p24_world.spawnlist_npc WHERE id=1016;
UPDATE p24_world.spawnlist_npc SET count=1
WHERE id=1016 AND location='拍賣佈告欄' AND count=0 AND npc_templateid=81161
 AND locx=32611 AND locy=32775 AND randomx=0 AND randomy=0
 AND heading=0 AND respawn_delay=0 AND mapid=4 AND movement_distance=0;
-- 測完、停服、重新備份及核對整列後才可還原；影響列數必須恰為1。
UPDATE p24_world.spawnlist_npc SET count=0
WHERE id=1016 AND location='拍賣佈告欄' AND count=1 AND npc_templateid=81161
 AND locx=32611 AND locy=32775 AND randomx=0 AND randomy=0
 AND heading=0 AND respawn_delay=0 AND mapid=4 AND movement_distance=0;
```

## 備份、驗證與交接

#1209備份從`D:\lineage開發\TW1608262200研究\scratch\wt\task-1209\environments\p24\backups\issue-1209-before`保全至主專案`environments\p24\backups\issue-1209-before`。這是本張第0步明列的目的地例外；其他輸出只在task-1222。manifest SHA：`cf27f301533e42568fabef9620fbe3f23dd8a57c98a4b29f28e5234178995928`。資料檔1,722個／279,507,053 bytes不含manifest本身。

目前持久DB為1,722檔／279,511,437 bytes，與較早的#1209備份大小不同是兩個時間點，不是本張修改；本輪開始與收尾逐檔SHA完全相同。首次MySQL啟動因原生程式Unicode路徑解析失敗而退出（PID10548／exit1），改用既有`D:\BinEngine`別名後成功（PID19124／exit0），未建立或修改junction。有效查詢只有這一次服務工作階段。兩次都已退出，最終42061／43324無監聽，未啟停其他既有程序。

- F完整沿用`task/1209`的`fadb1e4f090e353ecc1d89f53540a53001fdab71`，只補板子方法、阻塞原因與保全資訊；其他角色物品沒有重測或重寫。**PR #1219仍OPEN**，F原有`restoreSql`／`dataDiff`指向其文件，調度器應先合#1219，再保留本PR的板子更新；本張沒有把尚未合併的#1209產品或其他檔案搬進main。
- 已同步main `90ae30dd`；JSON／欄位不變性／素材閘門／建置結果見E `validation`。不改產品來源、`docs/REPORT.md`或rebuild-lock；未執行產品。
- 已驗證：唯讀資料、方法位元碼、備份與持久DB保全。推測：同一伺服器內按上述流程能保留暫時板子至降GM後操作。待驗證：GM指令實際成功、板子可點、座標可站、SM27／SM29 UI及出價；未啟動客戶端，所以沒有截圖或SM判定。
- 下一棒：本機調度器先取得B稽核副作用裁定；允許後移除兩個`blockedFixtures`、設`readyForRetest=true`並交`f1209-board-retest`（#1211之後）只測SM27／SM29。不同意則選上述各別單列授權或列已知測試限制，不能誤標伺服器不支援。不得為了重測刪稽核紀錄或擅改世界表。
