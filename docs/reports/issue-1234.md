# Issue #1234：map4 fixture 持久保存

結論：通過（本機資源核驗 ✅ 7／❌ 0／⚠️ 0；PR CI 另以最新提交的檢查結果為準）。本張只處理 #1215 F07 保存問題；未重測已通過的 F01–F06 遊戲／SQL 項目。

## 已驗證

2026-11-14 執行於 `task/1234`；起始 main 為 `1ef9039d6b6ef87151859457bdbd35b18d64724d`。已讀 AGENTS.md、docs/REPORT.md、#1228 分流報告；#1215 報告與 fixture.json 從 `origin/task/1215`（`49fa09e22d50abbb0a5449e0d3a84faadeaf1861`）唯讀取得。採用本張明列的例外，只新增原始專案下指定的 fixture 目錄；沒有修改其他原始專案資源。

以下 `記錄` 指 Git 追蹤的 `work/f1197-map4/fixture-persist.json`，是持久位置的權威記錄；`目的地` 指下節的持久 fixture 根目錄。暫存核驗腳本與完整前後清單在本 worktree 的 `scratch/issue-1234/`，不進 Git。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| P01 資源鎖 | ✅ | 開始、複製前、結束均為 #1234 持有 p24lab；到期 2026-11-14T11:06:21.039294Z | 讀 localhost:8787/api/state 的 leases，比對 holder、exp 與當時時間 | 記錄 `lease`；`scratch/issue-1234/lease.json` |
| P02 來源選擇 | ✅ | 採來源 (a)，task-1215 fixture 仍存在；manifest SHA 正確，1,722 檔／279,503,832 bytes 全相符 | SHA-256 核 manifest；正規化清單路徑分隔符，逐檔核 SHA、大小與完整檔名集合 | 記錄 `source`、`verification`；來源 manifest |
| P03 安全複製 | ✅ | 目的地原不存在；以 exclusive-create 新增 1,736 檔，含 DB 1,722、manifest 1、原證據 13；覆寫 0 檔 | 檢查來源／目的地及祖先的 reparse point、檔案 hardlink count；逐檔以 xb 建立並複製 | 記錄 `verification`；`scratch/issue-1234/persist.py` |
| P04 目的地核對 | ✅ | DB 全部符合 manifest；13 份原證據檔與來源逐 byte 相同，無新增或缺檔 | 重算目的地全部檔案 SHA，核對來源完整清單、DB manifest、檔數及 bytes | 記錄 `verification.evidenceFiles`；目的地全部檔案 |
| P05 結尾來源核對 | ✅ | 來源 DB／證據清單皆不變，來源與目的地 manifest 結尾 SHA 都符合指定值 | 再核來源全部檔案，重算兩份 manifest SHA | 記錄 `source.manifestSha256After`、`fixture.manifestSha256` |
| P06 持久 DB 不變 | ✅ | 前後 1,722 檔／279,511,437 bytes；完整清單相同，SHA 皆為 f94bf983…9668 | 前後各讀取全部資料檔並計算 SHA；相同序列化後再雜湊清單 | 記錄 `persistentDb`；`scratch/issue-1234/persistent-before.json`、`persistent-after.json` |
| P07 交接範圍 | ✅ | PR #1227 尚未合併且 main 無 fixture.json，依規則只新增 fixture-persist.json；未啟動服務、客戶端或 SQL，未刪目錄 | 查 PR 狀態與 main 檔案樹；檢視執行腳本及 Git 差異 | 記錄 `fixtureJsonUpdate`、`scope`；本報告 |

## 來源、目的地與雜湊

- 來源 (a) 根目錄：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1215\environments\p24\backups\issue-1215-map4-fixture`。
- 持久目的地根目錄：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1215-map4-fixture`。
- 持久 DB fixture：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1215-map4-fixture\db-data`。
- 持久 manifest：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1215-map4-fixture\manifest.json`。
- 來源開始／結尾與目的地 manifest SHA-256：`996a39971570b7a2d8ab2547fea02e737a24c898f02b5f7f740135e6b3caf06b`。
- 持久 DB（非 fixture）`D:\lineage開發\TW1608262200研究\environments\p24\db-data` 前後清單 SHA-256：`f94bf98347b9789f368dc9863a28ab09132ad613cb86373f87bd7283d0f19668`。

持久 DB 清單採 POSIX 相對路徑排序，每列依序為 `path`、`bytes`、`sha256`；`json.dumps(ensure_ascii=False, indent=2)` 加結尾換行，以 UTF-8 無 BOM、CRLF 儲存。這與 #1215 的清單格式相同；本次前後 SHA 也與已保存到目的地的原 `persistent-before.json`／`persistent-after.json` 相同。目的地的這兩份證據仍是 #1215 原件，沒有冒充本次新產出。

13 份原證據為 `account-exists.tsv`、`characters.tsv`、`cleanup.json`、`final-verification.json`、`identity.tsv`、`lease.json`、`mysql-owned.json`、`mysql-process.json`、`persistent-after.json`、`persistent-before.json`、`preparation.json`、`select-result.json`、`source-manifest.json`。各檔大小及 SHA 都在權威記錄中；內含歷史路徑／lease 保留原文，不改寫。來源 (b) 無需使用，沒有讀寫 #1186 備份。

## 交付驗證

Git 僅新增本報告與 `work/f1197-map4/fixture-persist.json`。DB 與原證據留在忽略的持久 environments 路徑；未改產品程式、docs/REPORT.md 或 rebuild-lock。交付前執行 JSON／交接欄位一致性檢查、`git diff --cached --check` 與 `python -B -X utf8 engine/tools/check-no-game-assets.py`；PR CI 由 GitHub 對最新提交執行。本張未做本機產品建置。

## 推測與待驗證

- 推測：逐 byte 相同的 fixture 可沿用 #1215 F03 已驗證的 10334 資源法師／p24svc01／MapID=4（33071,32800）／GM0／離線條件；本張沒有重新 SELECT。
- 待驗證：下一棒的 M01 地圖 4 實際載入，以及其清單要求的 F07 交接核驗。本張資源保存沒有待補項。

## 給下一棒的提示

1. 以 `work/f1197-map4/fixture-persist.json` 為持久位置權威；PR #1227 仍未合併，本張未帶入 fixture.json。後續合併 #1227 時，請用此記錄更新該檔的 fixture 路徑與 durable 狀態。
2. 從持久 fixture 複製新的隔離 datadir；fixture 本身不得開服，也不要改以持久 DB 開服。只重測 F07、M01（G01 當前置）；其餘通過項沿用。
3. 調度器依正常流程處理 worktree；本張未清理或移除 task-1215／task-1234，沒有遞迴刪除。
