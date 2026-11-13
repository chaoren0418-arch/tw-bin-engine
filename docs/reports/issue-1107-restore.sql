-- Issue #1107：僅還原本張移除的孤兒限時列，未執行。
-- 先停止 P24Lab 42061，並核對 43324 與持久 datadir；不適用正式環境。
-- 本列重新加入後，未補正持有人前會再次造成原 JAR 拒絕開服。
-- 表為 MyISAM，不依賴 ROLLBACK；完整復原使用 issue-1107-before 冷備份。
-- 若相同 ID 已存在，本句不覆寫；須先核對現有列。
SELECT @@port, @@datadir;
INSERT INTO p24_accounts.character_items_time (itemr_obj_id, usertime)
SELECT 10306, '2026-11-07 05:49:36'
WHERE @@port = 43324
  AND REPLACE(@@datadir, CHAR(92), '/') IN (
    'D:/BinEngine/environments/p24/db-data/',
    'D:/lineage開發/TW1608262200研究/environments/p24/db-data/'
  )
  AND NOT EXISTS (
    SELECT 1 FROM p24_accounts.character_items_time WHERE itemr_obj_id = 10306
  );
SELECT ROW_COUNT() AS restored_rows;
SELECT itemr_obj_id, usertime FROM p24_accounts.character_items_time
WHERE itemr_obj_id = 10306;
