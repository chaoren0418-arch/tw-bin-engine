# Issue #175：M5 smoke 測試條件準備

## 做了什麼

2026-11-06，依 [正式派工](https://github.com/chaoren0418-arch/tw-bin-engine/issues/175#issuecomment-6694962643)與本張直接寫入授權，只在 P24Lab 完成冷備份、測試角色與背包準備。**DB 準備及獨立讀回通過；沒有啟動 Java 或客戶端，尚不代表 M5 遊戲驗收通過。**

| 項目 | 前值 → 後值／結果 |
|---|---|
| 原測試角色 | `服務驗證`，objid `1900249001`；AccessLevel **0→200**，兩次讀回均 OnlineStatus=0。200 為目前 `tp` 指令要求的最低權限。本張保留 GM，後續 smoke 結束須還原0。 |
| 第二測試角色 | 新增 `服務交易`，objid `1900249201`；與原角色同一既有帳號（只讀回相等結果1，不公開帳號值）。level59、AccessLevel0、OnlineStatus0，沒有新增帳號、密碼或權限憑證。 |
| 位置 | 原角色 map0 `(32557,32975)` 不變；第二角色 map0 `(32558,32975)`、heading6。伺服器及客戶端地形都可通行；既有角色、固定NPC及門範圍佔位計數均0。實測交易前仍須調整雙方面向。 |
| 普通弓 | item172，新增背包物件 `1900249202`，**0→1**，未裝備、未強化，採既有模板名稱與祝福值；此模板支援原角色職業。 |
| 普通箭 | item40743，新增物件 `1900249203`，**0→200**。 |
| 普通肉 | item40056，新增物件 `1900249204`，**0→20**；沒有放地上或食用。 |
| 治癒藥水 | item40010，既有物件10322，**40→40**，已足量所以未寫入。 |
| 購物金幣 | item40308，既有物件1900249101，**33300→100000**；只更新這一列。 |

第二角色從既有測試角色選取必要職業／等級／EXP／基礎屬性，HP／MP設為該模板上限，無裝備、技能複製、血盟或伴侶關聯，BonusStatus=0。原角色的等級、EXP、HP、能力點、位置及其他既有物品均未改；原角色當下 HP67/686，後續應依一般玩家操作恢復，不用資料寫入代替回血驗收。

## 備份與怎麼驗證

- 冷備份目錄：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-175-20261106T204343Z`，資料位於其 `db-data` 子目錄，備份不進 git。
- **1,407 檔、255,480,500 bytes；逐檔 SHA-256 全部一致**，20:43:55 UTC完成。`sha256-manifest.json` SHA-256：`6a938cab1edb9c3c07df87065df7579d48232da028cde94f174ab15cd0052790`。
- 本次重新確認無實驗監聽、無當前 PID 檔；來源／目的地解析均在 P24Lab。檢查資料樹沒有額外 reparse point、每檔硬連結數為1，整個複製期間持有所有來源檔案的唯讀共享控制，排除寫入／刪除。沒有把舊關機紀錄單獨當成當前冷狀態證據，沒有使用匯出命令。
- 備份核實後才啟動本次 MySQL；每次連線先核對實例身分。憑證只由既有安全檔交給原生工具使用，未讀出或記錄帳密。
- 四個新 ID 已對照伺服器 IdFactory 列出的13張資料表，衝突均0；寫入期間持有必要表鎖、使用已核對的前值與名稱／ID條件。資料表為 MyISAM，沒有宣稱交易可回滾；完整冷備份保留。
- 首次腳本在前置 `SET @ok` 因同表重複引用觸發 `ERROR 1100: Table 'characters' was not locked with LOCK TABLES`，**尚未到任何 UPDATE／INSERT**。新連線確認GM仍0、第二角色不存在、道具未新增、金幣仍33300後，修正表鎖別名及分段檢查。修正後6筆DML各影響1列，再以新連線獨立讀回上表；沒有盲目重送或工具政策拒絕。
- 商店、倉庫、技能NPC與港口位置由本次P24Lab必要表唯讀核對；洞穴口依既有協議報告。六個準備落點均核對伺服器／客戶端地形可通行；不代表當下無動態怪物。詳細位置與 `.tp x y map` 指令見 [RUN：M5 smoke 準備](../demo/RUN.md#m5-smoke-準備175)。本次未執行這些遊戲指令。

## 未驗證與交接

- 未登入新角色、裝弓、射箭、交易或使用GM指令。兩角色同帳號的同時登入／雙客戶端交易可用性仍待 smoke 核實；本張沒有新建第二帳號或修改登入政策。
- 第二角色及新物品只證明DB存在，尚未證明客戶端載入、圖示、彈藥消耗或保存。普通肉購買驗收仍須透過NPC成交，不能用本次發肉代替購買通過。
- 說島倉庫NPC60001沒有查到固定出生列，因此文件改列目前確有出生的map4倉庫60007；技能清單、未學技能與實際報價仍待遊戲驗證。沒有修改NPC、商品、伺服器配置、程式或原素材。
- **20:52:47 UTC 已正常 SHUTDOWN，自有 MySQL 啟動程序及服務程序均退出、實驗雙埠無監聽**。Java／客戶端從未啟動；沒有續租或操作未知程序。GM依本張要求保留200，還原SQL只提供未執行。
- 交付本報告及 `docs/demo/RUN.md`。冷備份、完整原始讀回與程序證據留本機；公開內容只含本張必要測試資料與SQL，不含帳號值、憑證或其他任務的受限內容。

## 已執行 SQL

以下是修正後實際執行內容；前後SELECT已包含在內。不要重跑：名稱、ID與前值保護會拒絕套用同一份準備。SQL內只引用既有帳號欄位，不含帳號字串或密碼。

```sql
-- Only P24Lab test role preparation. No account creation, credentials, deletion or restore.
USE p24_accounts;
SET NAMES utf8mb4;
LOCK TABLES characters WRITE, characters AS seed READ, character_items WRITE,
character_warehouse READ,character_elf_warehouse READ,clan_warehouse READ,
character_warehouse_for_cha READ,clan_data READ,character_teleport READ,
character_mail READ,character_letter READ,`紀錄_拍賣拖售` READ,
character_pets READ,character_lito READ,
p24_world.weapon READ,p24_world.etcitem READ,p24_world.spawnlist_npc READ;
SET @free =
 (SELECT COUNT(*) FROM characters WHERE objid BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_items WHERE id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_warehouse WHERE id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_elf_warehouse WHERE id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM clan_warehouse WHERE id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_warehouse_for_cha WHERE item_objid BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM clan_data WHERE clan_id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_teleport WHERE id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_mail WHERE id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_letter WHERE item_object_id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM `紀錄_拍賣拖售` WHERE item_obj_id BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_pets WHERE objid BETWEEN 1900249201 AND 1900249204)+
 (SELECT COUNT(*) FROM character_lito WHERE char_obj_id BETWEEN 1900249201 AND 1900249204);
SET @ok = @free=0;
SET @ok = @ok AND (SELECT COUNT(*) FROM characters WHERE objid=1900249001 AND char_name='服務驗證' AND AccessLevel=0 AND OnlineStatus=0 AND MapID=0 AND LocX=32557 AND LocY=32975)=1;
SET @ok = @ok AND (SELECT COUNT(*) FROM characters WHERE char_name='服務交易')=0;
SET @ok = @ok AND (SELECT COUNT(*) FROM characters WHERE MapID=0 AND LocX=32558 AND LocY=32975)=0;
SET @ok = @ok AND (SELECT COUNT(*) FROM p24_world.spawnlist_npc WHERE mapid=0 AND locx=32558 AND locy=32975 AND count>0)=0;
SET @ok = @ok AND (SELECT COUNT(*) FROM character_items WHERE id=1900249101 AND char_id=1900249001 AND item_id=40308 AND count=33300)=1;
SET @ok = @ok AND (SELECT COUNT(*) FROM character_items WHERE char_id=1900249001 AND item_id IN (172,40743,40056))=0;
SET @ok = @ok AND (SELECT COUNT(*) FROM character_items WHERE char_id=1900249001 AND item_id=40010 AND count>=20)=1;
SET @ok = @ok AND (SELECT COUNT(*) FROM p24_world.weapon WHERE item_id=172 AND 裝備類型='bow' AND 王族可用=1)=1;
SET @ok = @ok AND (SELECT COUNT(*) FROM p24_world.etcitem WHERE item_id IN (40743,40056) AND stackable=1)=2;
SELECT @ok AS preparation_guard, @free AS id_conflicts;
SELECT objid,char_name,AccessLevel,OnlineStatus,MapID,LocX,LocY FROM characters WHERE objid=1900249001;
UPDATE characters SET AccessLevel=200 WHERE @ok AND objid=1900249001 AND char_name='服務驗證' AND AccessLevel=0 AND OnlineStatus=0;
SELECT ROW_COUNT() AS gm_updated;
SELECT objid,char_name,AccessLevel,OnlineStatus,MapID,LocX,LocY FROM characters WHERE objid=1900249001;
SELECT COUNT(*) AS second_role_before FROM characters WHERE objid=1900249201 OR char_name='服務交易';
INSERT INTO characters
(OnlineStatus,account_name,AccessLevel,char_name,objid,level,HighLevel,Exp,MaxHp,CurHp,MaxMp,CurMp,Ac,Str,Con,Dex,Cha,Intel,Wis,Status,Class,Sex,Type,Heading,LocX,LocY,MapID,Food,Lawful,OriginalStr,OriginalCon,OriginalDex,OriginalCha,OriginalInt,OriginalWis,CreateTime)
SELECT 0,account_name,0,'服務交易',1900249201,level,level,Exp,MaxHp,MaxHp,MaxMp,MaxMp,10,OriginalStr,OriginalCon,OriginalDex,OriginalCha,OriginalInt,OriginalWis,0,Class,Sex,Type,6,32558,32975,0,Food,0,OriginalStr,OriginalCon,OriginalDex,OriginalCha,OriginalInt,OriginalWis,20261106
FROM characters AS seed WHERE @ok AND objid=1900249001 AND char_name='服務驗證' AND AccessLevel=200 AND OnlineStatus=0;
SELECT ROW_COUNT() AS second_role_inserted;
SELECT objid,char_name,AccessLevel,OnlineStatus,level,MapID,LocX,LocY,Heading FROM characters WHERE objid=1900249201;
SELECT id,item_id,count,is_equipped FROM character_items WHERE char_id=1900249001 AND item_id IN (172,40743,40056,40010,40308) ORDER BY item_id;
INSERT INTO character_items(id,item_id,char_id,item_name,count,is_equipped,is_sealed,enchantlvl,is_id,durability,charge_count,remaining_time,bless,attr_enchant_kind,attr_enchant_level)
SELECT 1900249202,item_id,1900249001,名稱,1,0,0,0,1,0,0,0,祝福狀態,0,0 FROM p24_world.weapon WHERE @ok AND item_id=172;
SELECT ROW_COUNT() AS bow_inserted;
INSERT INTO character_items(id,item_id,char_id,item_name,count,is_equipped,is_sealed,enchantlvl,is_id,durability,charge_count,remaining_time,bless,attr_enchant_kind,attr_enchant_level)
SELECT 1900249203,item_id,1900249001,name,200,0,0,0,1,0,0,0,bless,0,0 FROM p24_world.etcitem WHERE @ok AND item_id=40743;
SELECT ROW_COUNT() AS arrows_inserted;
INSERT INTO character_items(id,item_id,char_id,item_name,count,is_equipped,is_sealed,enchantlvl,is_id,durability,charge_count,remaining_time,bless,attr_enchant_kind,attr_enchant_level)
SELECT 1900249204,item_id,1900249001,name,20,0,0,0,1,0,0,0,bless,0,0 FROM p24_world.etcitem WHERE @ok AND item_id=40056;
SELECT ROW_COUNT() AS meat_inserted;
UPDATE character_items SET count=100000 WHERE @ok AND id=1900249101 AND char_id=1900249001 AND item_id=40308 AND count=33300;
SELECT ROW_COUNT() AS gold_updated;
SELECT id,item_id,item_name,count,is_equipped FROM character_items WHERE char_id=1900249001 AND item_id IN (172,40743,40056,40010,40308) ORDER BY item_id;
UNLOCK TABLES;

```

## 還原 SQL（只提供，未執行）

先正常停遊戲服務、確認測試角色離線並備份當前狀態。GM 還原可單獨執行；其餘是**本張準備後尚未登入、交易或消耗的原樣回復**，預設關閉。若已開始 smoke，應先核對角色關聯資料、物品去向與金幣變動，另行產生差異還原，不能刪除其他角色收到的物品或回退遊戲進度。本張沒有執行任何 DELETE。

```sql
-- 僅供後續已授權維護；先確認連線是P24Lab，不附任何憑證。
USE p24_accounts;
SELECT objid,char_name,OnlineStatus,AccessLevel
FROM characters WHERE objid=1900249001 AND char_name='服務驗證';
UPDATE characters SET AccessLevel=0
WHERE objid=1900249001 AND char_name='服務驗證'
  AND OnlineStatus=0 AND AccessLevel=200;
SELECT ROW_COUNT() AS gm_restored;

-- 預設不回收／刪除；只有核實「尚未登入或使用」後才可另行啟用。
SET @restore_unused_preparation=0;
SELECT id,char_id,item_id,count,is_equipped FROM character_items
WHERE id IN (1900249202,1900249203,1900249204,1900249101);
DELETE FROM character_items
WHERE @restore_unused_preparation=1 AND char_id=1900249001
  AND is_equipped=0 AND enchantlvl=0
  AND ((id=1900249202 AND item_id=172 AND count=1)
    OR (id=1900249203 AND item_id=40743 AND count=200)
    OR (id=1900249204 AND item_id=40056 AND count=20));
UPDATE character_items SET count=33300
WHERE @restore_unused_preparation=1 AND id=1900249101
  AND char_id=1900249001 AND item_id=40308 AND count=100000;
-- 第二角色若曾登入，須先盤點後續衍生資料；此段不能作一般刪角工具。
DELETE FROM characters
WHERE @restore_unused_preparation=1 AND objid=1900249201
  AND char_name='服務交易' AND AccessLevel=0 AND OnlineStatus=0
  AND CreateTime=20261106 AND MapID=0 AND LocX=32558 AND LocY=32975
  AND NOT EXISTS (SELECT 1 FROM character_items WHERE char_id=1900249201);
-- 原有藥水40未改，不需回復；不刪帳號或其他既有資料。
```
