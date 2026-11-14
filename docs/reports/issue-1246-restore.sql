-- #1246 P24Lab 準備資料還原；只在伺服器停止、所有角色離線時人工審閱執行。
-- 不適用正式 DB。預設只允許本 worktree 的 43324 執行副本。
-- 本張沒有執行任何 DELETE；以下僅刪本張新增列，不刪既有資料。
-- 若快照已保全到別處，先核對 issue-1246-ready manifest，另行調整 datadir 條件。
-- 完整還原（包含自然戰鬥HP／伺服器自動存檔）使用 issue-1246-before 完整冷備份。
SET @fixture_restore_allowed = (
  @@port = 43324
  AND @@datadir LIKE '%task-1246%'
  AND (SELECT COUNT(*) FROM p24_accounts.characters WHERE OnlineStatus <> 0) = 0
);
SELECT @@port, @@datadir, @fixture_restore_allowed;
START TRANSACTION;
DELETE FROM p24_accounts.character_items
WHERE @fixture_restore_allowed AND char_id = 1900252004
  AND ((id = 1900264601 AND item_id = 49553 AND count = 10)
    OR (id = 1900264602 AND item_id = 4 AND count = 1 AND durability = 1)
    OR (id = 10304 AND item_id = 49553 AND count = 2));
-- 任務前煙法甲沒有變身列；只清理本張可能留下的三個gfx。
DELETE FROM p24_accounts.character_buff
WHERE @fixture_restore_allowed AND char_obj_id = 1900252004
  AND skill_id = 67 AND poly_id IN (1461, 1395, 734);
UPDATE p24_accounts.characters SET AccessLevel = 0
WHERE @fixture_restore_allowed AND objid IN (10334, 1900252002, 1900252004)
  AND OnlineStatus = 0;
UPDATE p24_accounts.characters SET LocX=32648, LocY=32948, MapID=0, Heading=0
WHERE @fixture_restore_allowed AND objid=10334 AND OnlineStatus=0;
UPDATE p24_accounts.characters SET LocX=32640, LocY=32955, MapID=0, Heading=0
WHERE @fixture_restore_allowed AND objid=1900252001 AND OnlineStatus=0;
UPDATE p24_accounts.characters SET LocX=32641, LocY=32955, MapID=0, Heading=0
WHERE @fixture_restore_allowed AND objid=1900252002 AND OnlineStatus=0;
UPDATE p24_accounts.characters SET LocX=32643, LocY=32955, MapID=0, Heading=0
WHERE @fixture_restore_allowed AND objid=1900252004 AND OnlineStatus=0;
COMMIT;
SELECT objid,char_name,AccessLevel,OnlineStatus,LocX,LocY,MapID,Heading
FROM p24_accounts.characters WHERE objid IN (10334,1900252001,1900252002,1900252004);
