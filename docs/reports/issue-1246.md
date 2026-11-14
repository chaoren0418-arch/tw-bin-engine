# Issue #1246：補齊 #1211 重測條件

結論：部分通過（✅ 7／❌ 0／⚠️ 3，共 10 項資源條件）。**這是 fixture 準備結果，不是 SM 功能判定。**

## 做了什麼／怎麼驗證

- 全程在 `task/1246` worktree；原專案只讀。開始時 HEAD `269aeb77`，提交前同步 main `22d8a86b`（只增加報告及另一張 fixture）。沒有修改引擎、設定、dev lock 或手動修改 REPORT 索引。
- main 尚缺 #1211 報告與舊 fixture，唯讀使用 `origin/task/1211` 的報告、`origin/task/1209` 的 `work/f1186-smoke/fixtures.json`，並讀 main 的 #1237 分流。
- 租約 `p24lab` 持有人 `chaoren0418-arch/tw-bin-engine#1246`、到期 `2026-11-14T12:49:45Z`，已由 `/api/state` 核實。
- 冷備份原 P24Lab 後，另複製為本 worktree 的獨立 DB；只使用 `127.0.0.1:43324`／`42061`。MySQL 查詢核對 `@@datadir` 包含 `task-1246`。Java 與 MySQL 各啟動一次。
- 使用真實 P24Lab 伺服器資料、JAR 位元碼，以及本地準備用協定連線執行 GM 指令／讀取伺服器回包。沒有模擬伺服器回覆、沒有開引擎 GUI、沒有做 SM 畫面或功能驗收；因此本張沒有客戶端建置或截圖。
- 已保存前後資料、封包記錄、JAR 查核輸出與冷備份。證據根目錄 **E**：本 worktree `environments/p24/backups/issue-1246-evidence/`。下表證據路徑相對 E。

## 逐項條件

|編號|結果|實際觀察／條件|重現／下一棒操作|證據|
|---|---|---|---|---|
|SM13-82|✅|49553「殷海薩能量碎片」處理器 `event.EinhasadGem 5`；普通煙法甲使用後實收 `fa52050000000000`，即 S250/82＝5%。角色等級須 ≥49，LeavesSet 已啟用。|煙法甲 objid 1900252004；使用物品 objid 10304，原 3→剩 2；另備 objid 1900264601 ×10，須下次開服載入。|`fragment-use.json`、`ordinary-fixture-packets.jsonl`、`after.json`、`EinhasadGem.javap.txt`|
|SM13-6400／PC咖啡|⚠️|smoke 原文的 6400 是**地圖編號**，82 是封包子類型，並非三個技能 ID。mapids 無6400、伺服器 `maps/6400.txt` 不存在；未找到產生 PC咖啡 effect293 的道具／技能。|已知限制候選：先用已成立的普通82條件；6400／PC咖啡變體交資源／設計裁定，不反覆搜尋不存在的技能6400。|`catalog3.txt`、`icons.tsv`、`source-hashes.json`、`none-time-callers.json`|
|SM13-470|⚠️|skills、藥水 status_icon／buff_id、娃娃 icon 均未查得470；VIP道具名稱不代表 effect470。JAR未找到正常物品／技能送出該 effect 的路徑；可發任意測試封包的 L1PacketProbe 不作成立證據。|列已知限制候選；不發不相關 VIP 卡、不改世界表、不合成 S250/180 冒充條件。|`icons.tsv`、`icons-schema.tsv`、`VIP.javap.txt`、`target-constants.txt`、`none-time-callers.json`|
|SM20-map509|✅|GM .3 509 落在32736,32800；召回在線的 GM0 煙法甲後，實收 S206 map509、本人 S87＝32734,32802。|資源法師10334暫GM200：`.3 509`，收到S149須回C52；再 `.recall 煙法甲`，目標亦完成C52。**不要登出目標**：`getback_restart.area=509` 會把重新登入者送到map4 33439,32811。核對後GM還原0。本張未按技能窗、未判拒開。|`map509-landed.json`、`gm-map-helper-packets.jsonl`、`ordinary-fixture-packets.jsonl`、`pre-cleanup.tsv`|
|SM21-出售|⚠️|潘朵拉70014共有34筆shop列，`purchasing_price`全−1；全表1631筆亦無非負收購價。ShopTable只把收購價≥0的列加入商人的 purchasingItems；全域半價估價表不會填入該清單。|現有資料下沒有「發一件就能讓潘朵拉收購」的物品。交資源／伺服器負責人決定是否另提供啟用收購的實驗商店；本張沒有改shop，也沒有無效發物。|`catalog2.txt`、`catalog5.txt`、`ShopTable.javap.txt`、`L1Shop.javap.txt`|
|SM21-修理|✅|已新增煙法甲匕首 item4×1、objid1900264602、durability=1；金幣40308既有100000。修理NPC亞南70802，spawn1273，map4 33068,33382，頁面aanon1的fix；每損壞度200金幣。|下次從準備完成DB開服，靠近亞南，查修理清單後選該匕首。**只完成持久資料準備**：本輪開服後SQL新增物品未進背包快取，當輪C118回空清單；未宣稱原生修理窗／成交已驗證。|`items-after.tsv`、`after.json`、`repair-npcs.tsv`、`conditions-gm0-summary.json`、`S_FixWeaponList.javap.txt`|
|SM21-學法|✅|改用煙騎甲1900252002：52級、Lawful0、金幣100000；在吉倫70009旁，GM0實收S41基價6000、7筆可學第一階技能，含日光術id2。兩名法師Lawful−10會被拒，不能沿用。|煙騎甲停在map0 32560,33087；吉倫spawn1275，32560,33086。本次NPC objid2000039419（每次開服重取）。選未學日光術，需40308×6000，無其他材料；騎士第一階要求50級。沒有買下技能，保留給SM21重測。|`learning-offer.json`、`learning-db.tsv`、`PcLvSkillList.javap.txt`、`C_SkillBuy.javap.txt`、`C_SkillBuyOK.javap.txt`|
|SM58-1461|✅|煙法甲在線且暫GM200，執行`.poly 煙法甲 1461`後斷線保存，DB character_buff 的skill67／poly_id=1461、remaining_time=7196。|L1Poly查線上World.getPlayer；目標必須在線。此gfx無polymorphs列仍能設定。準備後還原GM0，外型畫面留待引擎重測。|`poly-1461-db.tsv`、`poly-1461-packets.jsonl`、`L1Poly.javap.txt`、`L1PolyMorph.javap.txt`|
|SM58-1395|✅|同法實讀skill67／poly_id=1395、remaining_time=7196。之後已回734；最終buff表無該角色變身列，原Class仍734。|`.poly 煙法甲 1395`，同上必須線上目標。#1211舊準備腳本只登入GM、目標離線，是該方法使用上的缺口；不由檔名判成功。|`poly-1395-db.tsv`、`poly-734-db.tsv`、`after.json`|
|SM40|✅|煙妖甲1900252003原已持有短弓173×1（1900252138）、箭40743×200（1900252143）、銀箭40744×200（1900252144）。未再發物。|使用煙妖甲；第二種箭就是40743普通箭。射擊與耗箭留給SM40。|`before.json`、`after.json`|

## 前後對照與還原

- 新增持久物品：煙法甲49553×10／1900264601、損壞匕首4×1／1900264602；透過正常GM發物另新增49553×3／10304，實用1後剩2。**準備完成DB共有碎片12個，分兩列**。前兩列是開服後SQL新增，下次開服才載入；不要再把它們當成本輪已送到客戶端。
- 暫GM的10334、1900252002、1900252004：0→200→0；1900252001／1900252003始終0。最終24角全GM0、OnlineStatus0。
- 準備連線期間普通角色在村外受到怪物攻擊：煙王甲與煙法甲最終HP1，等級52與EXP127941146不變；煙騎甲HP500。未直接修改既有角色HP、正義值、技能或經驗。下一棒不要把低HP當成引擎問題，先在安全位置用正常治癒／藥水準備。
- 位置：資源法師回0(32648,32948)；煙騎甲由0(32641,32955)→吉倫旁0(32560,33087)；煙王甲由0(32640,32955)→0(32557,32975)；煙法甲由0(32643,32955)→509→0，最終0(32600,32942)。詳見E的before.json／after.json。
- [還原 SQL](issue-1246-restore.sql)只針對本張新增物品、暫GM、位置及本張變身，沒有執行刪除。既有物品不刪；技能購買與修理均未成交。**完整回到任務開始（含自然戰鬥HP、伺服器自動存檔／其他表）應使用完整冷備份**，SQL不宣稱涵蓋所有伺服器副作用。

## 備份、證據與程序收尾

以下路徑都在本 worktree；不進git。不要直接啟動備份目錄，應複製為下一棒的獨立執行目錄。

|用途|路徑|檔案／大小|manifest SHA-256|
|---|---|---|---|
|開始前完整冷備份|`environments/p24/backups/issue-1246-before/db-data`|1722檔／279511437 bytes|`1fb267532c07870f087ce58d91fd820a855c189cfa74c0378e681c7609d224b4`|
|準備完成冷備份|`environments/p24/backups/issue-1246-ready/db-data`|1722檔／279513857 bytes|`18536bab3158a3e1adfd6b83a5575323febff204b17b60ae59b8f5ca103526f4`|

- manifest各在db-data上層；備份／副本逐檔SHA核對。收尾再次核對原專案DB1722檔，全部與開始前相同。
- E 的109個證據檔另有 `manifest.json`，SHA-256：`6adfe80e1ee4affe852c2bd7fc605d241ab06aea5cc825c8637312a8326b1508`。
- 伺服器 `Server_Game.jar` SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；server map509 SHA：`b3f2d34ae1d37828494c2689d77d3659ad1814a159d3bc4a3dae88b41b4c2530`。沒有對原BIN作地址分析；JAR證據標示的是Java方法／位元碼指令偏移，不是BIN RVA或VA。
- 所有準備連線已關閉；Java PID7532核對執行檔後關閉；MySQL PID17016以mysqladmin TCP43324正常shutdown；沒有啟動引擎／登入器GUI。42061／43324無監聽，無本張Java／MySQL程序殘留。見E `cleanup-and-ready.json`、`remaining-processes.json`。

## 產出與給下一棒的提示

- [新 fixture](../../work/f1211-smoke/fixtures.json)、本報告、還原SQL。舊 `work/f1186-smoke/fixtures.json` 不改。
- **必須保全上述before／ready／evidence三個目錄後才能由調度器清理worktree**。本張未寫回原專案DB；下一棒若仍從主專案db-data複製，會缺少這次新增物品。fixture的preparedDatabase指向本張ready快照。
- 僅交重測：82普通圖示、map509在線召回、修理、吉倫學法、1461／1395、SM40。不要把本張資源✅當作SM已通過。
- 6400／PC咖啡、470、潘朵拉收購三項需資源／設計裁定；現況不應原樣重跑。`readyForRetest=false`表示這三項仍缺，已建立的條件各自有狀態。
- 本地檢查：JSON讀回、fixture物品／角色與after.json比對、差異／機密欄位檢查、tracked-content gate。PR CI以最新提交的檢查結果為準；沒有產品變更，因此未重建客戶端。
