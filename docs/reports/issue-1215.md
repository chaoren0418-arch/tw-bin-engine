# Issue #1215：地圖 4 DB fixture

**結論：部分通過（✅ 6／❌ 0／⚠️ 1）。** fixture 資料驗證完成；長期保存位置尚待調度器處理。原始專案目錄依本張開頭限制只讀，所有新增檔留在 `task-1215` worktree，沒有自行寫入原始專案的 backups。已提出僅新增指定 fixture 目錄的釐清，未將未回覆視為授權。

執行時間：2026-11-14 16:26–16:28（台北）。來源程式版本 `51c118525aa6761776a5f798f42580f2554b9954`；分支 `task/1215`。無產品程式變更，未建置或啟動遊戲。已讀 AGENTS.md、docs/REPORT.md、#1204；#1197／#1186 不在 main 檔案樹，從既有 origin/task/1197、origin/task/1186 唯讀取得指定報告。

## 已驗證：逐項清單

以下證據短檔名相對於 fixture 根目錄（不是 db-data）；原始執行紀錄另在 `scratch/issue-1215/`。SQL 原文亦收錄於本報告與 Git 追蹤的 fixture.json。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| F01 鎖與環境 | ✅ | p24lab holder=#1215，到期 2026-11-14T09:54:31.791356Z；起始 42061／43324 無監聽 | 讀 localhost:8787/api/state 的 leases；Get-NetTCPConnection 核埠，啟動前再查 | `lease.json`、`mysql-process.json` |
| F02 來源與複製 | ✅ | 首選 #1186 備份符合既有 manifest；1,722 檔、279,503,832 bytes；fixture／隔離副本逐檔 SHA 相同 | 核來源既有 manifest → 逐檔複製 fixture 與 scratch → 重算 SHA；拒絕 junction、symlink、硬連結 | `manifest.json`、`source-manifest.json`、`preparation.json` |
| F03 SELECT | ✅ | 10334 資源法師／p24svc01／map4(33071,32800)／GM0／離線；帳號存在；17 角色全 GM0／離線 | 僅啟 scratch datadir 的 127.0.0.1:43324，執行下列三個 SELECT | `identity.tsv`、`characters.tsv`、`account-exists.tsv`、`select-result.json` |
| F04 SEG | ✅ | map/4/80048001.seg 為 90,398 bytes，SHA 與 #1197 一致 | 唯讀雜湊原客戶端檔，不複製資源 | `preparation.json`、`work/f1197-map4/fixture.json` |
| F05 fixture 收尾核對 | ✅ | fixture 從未啟動，結束逐檔 SHA 符合 manifest；來源亦未變 | MySQL 停止後重算來源與 fixture 全部檔案 | `final-verification.json` |
| F06 持久 DB 與程序 | ✅ | 持久 DB 1,722 檔前後 SHA／清單相同；自啟父 PID 9140、子 PID 5488 全退出，shutdown=0，42061／43324 無監聽 | 開始／結束各做完整 SHA；mysqladmin 43324 shutdown → PID 與埠核對 | `persistent-before.json`、`persistent-after.json`、`cleanup.json`、`final-verification.json` |
| F07 長期保存 | ⚠️ | 已驗證 fixture 暫存本 worktree；尚未建立原始專案下不受 worktree 清理影響的副本 | 遵守原始專案只讀限制；待調度器保存並核 SHA，清理前必須完成 | 本報告「給下一棒的提示」與 fixture.json 的 handoff |

## 來源選擇與雜湊

首選來源 `D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1186-before\db-data` 是 #1186 瞬移前冷備份。其既有 manifest SHA 與 #1186 報告一致，所有檔案重新計算後亦相符；SELECT 已直接確認目標條件，因此不需要啟動或複製備援 #1188（備援目錄存在，只核對存在）。沒有 UPDATE、GM 瞬移、建角、補物品或其他 DML。

- 來源 manifest：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1186-before\manifest.json`。
- fixture：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1215\environments\p24\backups\issue-1215-map4-fixture\db-data`。
- fixture manifest：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1215\environments\p24\backups\issue-1215-map4-fixture\manifest.json`。
- 來源與 fixture manifest SHA-256：`996a39971570b7a2d8ab2547fea02e737a24c898f02b5f7f740135e6b3caf06b`（manifest 逐 byte 複製，兩者相同）。
- 資料檔：**1,722 檔／279,503,832 bytes**。manifest 與證據位於 db-data 外，不計入資料檔數。
- 原客戶端 SEG：`D:\lineage開發\0-客戶端\map\4\80048001.seg`；SHA-256 `e2dd9928bfcdf46874de18e875543c7378e66170594a1828937a12daf360ba12`；90,398 bytes。
- 持久 DB：`D:\lineage開發\TW1608262200研究\environments\p24\db-data`；1,722 檔／279,511,437 bytes。
- 持久 DB 前後 manifest SHA-256 都是 `f94bf98347b9789f368dc9863a28ab09132ad613cb86373f87bd7283d0f19668`，完整檔名、大小、SHA 清單相同，無新增或缺檔。

## SELECT 原文與 TSV

```sql
SELECT @@port, @@datadir;
SELECT account_name,char_name,objid,Type,AccessLevel,OnlineStatus,MapID,LocX,LocY
FROM p24_accounts.characters ORDER BY objid;
SELECT EXISTS(SELECT 1 FROM p24_accounts.accounts WHERE login='p24svc01') AS p24svc01_exists;
```

```tsv
@@port	@@datadir
43324	D:\BinEngine\scratch\wt\task-1215\scratch\issue-1215\db-data\
account_name	char_name	objid	Type	AccessLevel	OnlineStatus	MapID	LocX	LocY
p242db6e	資源王族	10305	0	0	0	4	33083	33393
p241db6e	資源騎士	10314	1	0	0	4	33083	33393
p241db6e	資源妖精	10325	2	0	0	4	33083	33393
p24svc01	資源法師	10334	3	0	0	4	33071	32800
p241db6e	資源暗妖	10344	4	0	0	4	33083	33393
p242db6e	資源龍騎	10356	5	0	0	4	33083	33393
p242db6e	資源幻術	10365	6	0	0	4	33083	33393
p241db6e	透明王子	1900240001	0	0	0	4	33072	32800
p242db6e	透明王子乙	1900240002	0	0	0	0	32477	32853
p243db6e	驗收王子	1900240003	0	0	0	0	32588	32981
p244db6e	驗收王子乙	1900240004	0	0	0	0	32588	32981
p245db6e	透明觀察甲	1900240005	0	0	0	0	32648	33068
p246db6e	透明觀察乙	1900240006	0	0	0	0	32598	32932
p247db6e	驗收觀察甲	1900240007	0	0	0	0	32587	32998
p248db6e	驗收觀察乙	1900240008	0	0	0	0	32587	32998
p24svc01	服務驗證	1900249001	0	0	0	4	33072	32799
p24svc01	服務交易	1900249201	0	0	0	0	32558	32975
p24svc01_exists
1
```

`D:\BinEngine` 是既有專案 junction 別名，SELECT datadir 實際指向本 worktree 的 `scratch/issue-1215/db-data`。沿用 #1197 的既有 MySQL 執行檔與設定作唯讀來源，命令列覆寫 datadir、tmpdir、secure-file-priv、error log、pid file 到本張 scratch，顯式限制 port=43324、bind-address=127.0.0.1、mysqlx=0。TEMP／TMP 同樣在本張 scratch。連線認證只引用既有 private-root.cnf，沒有讀出密碼或改機密設定。

MySQL runtime 自動寫入只發生於隔離副本；沒有執行 DML，無需還原 SQL。持久 DB 與 fixture 都未啟動；mysqladmin 正常 shutdown 退出 0，父程序退出 0，最後依兩個 PID 查無殘留。沒有啟動客戶端／遊戲伺服器、沒有碰正式 2000／3306，也沒有刪除任何目錄。

## 推測與待驗證

- **推測**：這份已驗證位置的 fixture 應可讓下一棒觸發 `4/80048001`；本張沒有啟動客戶端，因此不把位置正確當成 M01 通過。
- **待驗證**：M01 實際地圖載入，交 f1197-retest；長期保存副本交調度器處理。
- 未改產品、docs/REPORT.md、rebuild-lock 或原持久 DB。只有報告與機器可讀 fixture.json 進 Git；DB／manifest／原始 TSV 全留忽略的 environments／scratch。

## 給下一棒的提示

1. **needs:orchestrator：清理 task-1215 前，先將 fixture 根目錄（含 db-data、manifest 與證據）保存到持久位置，逐檔比對 manifest，再更新 fixture.json 的絕對路徑。** 本張開頭明定原始專案只讀，所以未擅自新增原始專案的 `environments/p24/backups/issue-1215-map4-fixture`。不要先清 worktree。
2. retest 只從 fixture 複製新的 scratch datadir，再開 43324；fixture 本身永遠不開服。先核 manifest 與 SELECT，再以 p24svc01／10334 進世界。
3. 只重測 #1197 M01；第 0 步確認 mapId=4、loadedRegion 涵蓋 `4/80048001`。其餘既有通過項沿用，不重做。持久 DB 目前資料不等同本 fixture，不要改回以持久 DB 開服。

產出：`docs/reports/issue-1215.md`、`work/f1197-map4/fixture.json`；DB 與 manifest 位於上列 fixture 絕對路徑。

交付檢查：JSON／報告／SELECT／manifest 參照一致性通過；`git diff --cached --check` 通過；`python -B -X utf8 engine/tools/check-no-game-assets.py` 通過（4,858 個追蹤檔、0 errors，既有 L1 例外不變）。初次產出使用 CRLF 造成 diff-check 提示，兩個提交檔已正規化為 LF 後通過。僅文件與資料描述變更，未跑產品建置；本張不判定 M01。
