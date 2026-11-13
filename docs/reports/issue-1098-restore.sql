-- #1098 還原：僅移除本次新增且仍保持原值的三筆物品，沒有執行本檔。
-- 先關閉本次 P24Lab 遊戲伺服器；只連 127.0.0.1:43324。
-- 若物品已被後續 smoke 移轉／消耗，條件不符時保留資料，交維護者逐筆核對。
-- MyISAM 沒有交易回滾；完整還原需使用報告所列冷備份。
SELECT @@port,@@datadir;
DELETE FROM p24_accounts.character_items
WHERE @@port=43324 AND REPLACE(@@datadir,CHAR(92),'/') LIKE '%/scratch/wt/task-1098/environments/p24/db-data/'
AND char_id=1900249001 AND is_equipped=0 AND is_sealed=0 AND enchantlvl=0
AND is_id=1 AND durability=0 AND charge_count=0 AND remaining_time=0
AND bless=0 AND attr_enchant_kind=0 AND attr_enchant_level=0
AND greater=0 AND magic_stone_item_id=0 AND magic_stone_level=0
AND magic_stone_time IS NULL AND last_used IS NULL AND gamno IS NULL AND Lotto IS NULL
AND ((id=1900251001 AND item_id=20288 AND item_name='傳送控制戒指' AND count=1)
 OR (id=1900251002 AND item_id=41255 AND item_name='料理書：1階段' AND count=1)
 OR (id=1900251003 AND item_id=40100 AND item_name='瞬間移動卷軸' AND count=20));
SELECT ROW_COUNT() AS removed_issue_1098_rows;
-- 本次未設定 GM：所有角色 AccessLevel 前後均為 0，不需還原 GM。
