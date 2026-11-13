-- #1119：本輪 GUI 新角已由 GUI 立即刪除，無須再刪除任何角色。
-- 唯一剩餘列差異為 p24svc01.lastactive；本檔沒有執行。
-- 執行前須另做完整備份，確認實驗服已關閉；前置不符則 UPDATE 不生效。
-- 不還原 MyISAM 自增計數或物理檔時間；逐 byte 還原請使用本輪完整冷備份。
SET @r07_restore_ok = (
  @@port=43324
  AND @@datadir=CONVERT(0x443a5c42696e456e67696e655c656e7669726f6e6d656e74735c7032345c64622d646174615c USING utf8mb4)
  AND (SELECT COUNT(*) FROM p24_accounts.characters)=17
  AND (SELECT COUNT(*) FROM p24_accounts.characters WHERE AccessLevel<>0 OR OnlineStatus<>0)=0
  AND (SELECT COUNT(*) FROM p24_accounts.characters WHERE account_name='p24svc01')=3
  AND (SELECT COUNT(*) FROM p24_accounts.characters WHERE objid=10387 OR char_name=CONVERT(0xe699a8e9a2a8e9a88ee5a3ab USING utf8mb4))=0
);
SELECT @r07_restore_ok AS r07_restore_preflight;
UPDATE p24_accounts.accounts
SET lastactive=CONVERT(0x323032362d31312d31342030323a34333a3138 USING utf8mb4)
WHERE @r07_restore_ok=1 AND `login`=CONVERT(0x7032347376633031 USING utf8mb4)
  AND lastactive=CONVERT(0x323032362d31312d31342030343a32373a3330 USING utf8mb4);
SELECT ROW_COUNT() AS restored_rows;
