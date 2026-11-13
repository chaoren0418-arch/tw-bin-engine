# Issue #1108：P24Lab SM 測試資料準備

**結論：部分通過（✅ 13／❌ 0／⚠️ 1）。資料已寫入主專案持久 DB；R07 七職業已建立，但未完成 GUI 建角驗證。**

2026-11-14 02:28–02:52（台北），在既有 `task/1108` 執行。只連 P24Lab `127.0.0.1:42061／43324`。沒有改引擎程式、伺服器 JAR、機密設定、原客戶端、封存或正式服務，沒有跑 SM smoke。原專案的寫入限本張指定的持久資料庫；備份、伺服器執行目錄、log、腳本與截圖均在本 worktree。

可隨 Git 檢閱：[驗證證據](issue-1108-evidence.json)、[完整資料前後差異](issue-1108-data-diff.json)、[還原 SQL](issue-1108-restore.sql)。下表未另註的證據檔名，均相對於 `scratch/issue-1108/`。

## 逐項結果

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| B01 備份 | ✅ | #1107 備份完整；目前 DB 有 19 檔與其不同，另做 1,722 檔完整冷備份並逐檔比對 SHA-256 | 兩埠空閒，禁止寫入的 ibdata1 handle 保護下複製及比對 | `snapshot.json`、`backup-before/manifest.json`；Git 證據 `snapshot` |
| R05 雙角色 | ✅ | 兩個不同帳號同時在 map 4、距離 1 格；雙方收到對方 S87，SQL 同時 OnlineStatus=1 | 離線準備相鄰座標；各自登入、C137 進世界、C95 時間探測，保持兩連線讀回 | `dual-clan.json`、`dual-online.tsv`、`dual-*-packets.jsonl`；Git 證據 `dual` |
| R07 七職業 | ⚠️ | 七職業均以 GM0 選角連線 C84 建立，S98=2＋S127 與 DB 讀回成功；**GUI 建角 0/7** | 一般玩家協定送合法職業／能力值；GUI 到選角後截圖逾時，滑鼠報 geometry unavailable，未完成建角操作 | `classes.json`、`create-ok-*-packets.jsonl`、`gui-selection.txt`；Git 證據 `classes`／`gui` |
| R08 弓箭 | ✅ | 服務驗證持有 172×1、40743×200，原本即成立 | 主 DB 初次與最終 SELECT | `items-before.tsv`、`final-readback.tsv` |
| R09 肉 | ✅ | 40056×19，未補發或消耗 | 同上 | 同上 |
| R10 戒指／卷軸 | ✅ | 新增 20288×1、40100×20，持久 DB 最終仍成立 | 跨四個實驗 schema 查 ID 不衝突後重放 INSERT，再讀回 | `item-id-audit.json`、`applied.sql`、`items-after.tsv` |
| R11 料理書 | ✅ | 新增 41255×1，持久 DB 最終仍成立 | 同 R10 | 同上 |
| R12 寵物／召喚 | ✅ | 資源法師 LV52、技能 51；項圈叫出資源牧羊，召喚術另生成 gfx 3357；登出後笛 9、寶石 57 | 離線準備測試資料，重開原服載入；GM0 登入後 C164 使用項圈、C6 施放一次召喚 | `roles-preparation-*.tsv`、`pet-summon.json`、`pet-summon-packets.jsonl`；Git 證據 `pets` |
| R13 血盟／盟倉 | ✅ | 資源血盟 10171；服務驗證盟主 rank 4，透明王子 rank 8；盟倉治癒藥水×1 | GM0 C222 建盟；面對面 C194 申請、收到 S219/97 後 C121 接受；向索連正常送存倉請求 | `clan-warehouse.json`、`clan-members-after.tsv`、`warehouse-*.tsv`；Git 證據 `clan` |
| R14 金幣 | ✅ | 建盟扣 30000：100000→70000；離線補回 30000，最終 100000 | 確認指定堆疊／持有人及數量後條件 UPDATE，讀回 | `gold-before-replenish.tsv`、`gold-after-replenish.tsv`、`final-readback.tsv` |
| R15 GM | ✅ | 全部 17 角色 AccessLevel=0、OnlineStatus=0；全程未設 GM | 最終全角色讀回，與冷備份資料對照 | `final-readback.tsv`；Git 證據 `characters` |
| C01 變更邊界 | ✅ | 70 張帳號庫表前後比較：76 新增、8 更新、0 刪除；只有測試角色／帳號及其關聯資料改變；14 個保護檔 hash 相同 | 冷備份另複製到隔離目錄，以同一 43324 順序啟動讀回；與主 DB 最終快照比較 | `row-diff-summary.json`、`protected-final.json`；Git 資料差異與證據 |
| C02 程序收尾 | ✅ | 所有本次 Java、MySQL、客戶端及 WebView 程序均結束；42061／43324 無監聽 | 按自啟 PID 核對後結束；MySQL 用 mysqladmin 正常 shutdown；最後列程序／監聽 | `shutdown.json`；Git 證據 `shutdown` |
| V01 建置／還原核驗 | ✅ | 首次建置 0 錯誤／4 既有警告；同步後增量 build 0 錯誤／0 警告；84 條還原 SQL 的 EXPLAIN 通過，預檢=1 | build、逐條 EXPLAIN、反向資料模型與備份全列比對、git diff --check | `build.log`、`build-after-sync.log`、`restore-preflight.tsv`；Git 證據 `restoreVerification` |

## 測試帳號與角色

未新增帳號、未改密碼。七職業是原服實際分配的 objid，沒有以 SQL 偽造建角成功。最初名稱含「測試」遭 S98=9 拒絕，伺服器 log 明示禁用字「測試」；改為下列名稱後七筆均接受。

| 用途／職業 | 帳號 | 角色 | objid | 等級 | Type／Class |
|---|---|---|---:|---:|---|
| 主驗證、盟主 | p24svc01 | 服務驗證 | 1900249001 | 59 | 0／0 |
| 第二 session、盟員 | p241db6e | 透明王子 | 1900240001 | 60 | 0／0 |
| 新王族 | p242db6e | 資源王族 | 10305 | 1 | 0／0 |
| 新騎士 | p241db6e | 資源騎士 | 10314 | 1 | 1／61 |
| 新妖精 | p241db6e | 資源妖精 | 10325 | 1 | 2／138 |
| 新法師、寵物／召喚 | p24svc01 | 資源法師 | 10334 | 52 | 3／734 |
| 新黑暗妖精 | p241db6e | 資源暗妖 | 10344 | 1 | 4／2786 |
| 新龍騎士 | p242db6e | 資源龍騎 | 10356 | 1 | 5／6658 |
| 新幻術師 | p242db6e | 資源幻術 | 10365 | 1 | 6／6671 |

原有其他八個角色未直接修改；全部 17 個角色清單收在 Git 證據。`p241db6e`／`p242db6e` 各 4/4 槽，`p24svc01` 為 3/4 槽。沿用 #1107 同帳號雙開結論，不重測；同時操作服務驗證與資源法師會衝突，應分時登入。

## 準備內容與前後對照

| 資料 | 任務前 | 最終／準備結果 |
|---|---|---|
| 服務驗證物品 1900251001／1002／1003 | 不存在 | 20288×1／41255×1／40100×20；char_id=1900249001 |
| 透明王子位置 | map 0 (32640,33109)，heading 7 | map 4 (33072,32800)，heading 0 |
| 服務驗證位置 | map 0 (32644,32951)，heading 5 | map 4 (33072,32799)，heading 4 |
| 資源法師 | 不存在；正常建角後 LV1／HP12／MP6 | 準備為 LV52、HighLevel52、Exp127941146、MaxHp/MaxMp300；map 4 (33071,32800)；一次召喚後 HP270／MP276 |
| 技能 | 新法師建角自帶 4 | 增加 51 召喚術（id11986）；技能表模板耗 HP30、MP30、寶石40318×3 |
| 寵物 | 空表 | item_obj_id1900251101、pet objid1900251199、npc45034、資源牧羊 LV5／HP30／MP5／exp750 |
| 法師背包準備 | 無三項物品 | 項圈40314×1（id1900251101）、寵物召喚笛41160 10→9（1102）、魔法寶石40318 60→57（1103） |
| 血盟 | clan_data 空 | 10171／資源血盟；leader_id1900249001；正常建盟費30000已補回 |
| 盟員權限 | 兩角色無血盟、rank0 | 服務驗證 rank4（盟主）；透明王子 rank8（修習騎士）；未提高第二角色倉庫權限 |
| 盟倉 | 空 | id10276、40010治癒藥水×1；來源背包 id10322 40→39 |
| 角色及附屬資料 | 10 角色 | 17 角色；原服建角附帶 56 筆起始物品，另有新法師技能／附屬表資料 |

R05 的兩連線本機端點與 UTC 時間已收入 Git 證據；兩邊都收到自己及對方的真實 S87，且維持連線時兩角色 OnlineStatus 同為 1。位置是離線資料準備，**沒有宣稱完成 GUI 步行測試**。

R12 的項圈需配合 **41160 寵物召喚笛**；已核對原 JAR `Pet_Collar` 行為並實際成功。召喚物本次 objectId=2000041065、gfx=3357、名稱代碼 `$2893`；此動態 objectId 不應作下次固定設定。寵物／召喚在登出後不留線上 session，下次以已準備的角色與物品重新叫出。

R13 由一般玩家協定完成，沒有 INSERT 血盟／盟倉或強設成員：C222 → C194 → S219/97 → C121；與 NPC 索連（template60003、此次 objectId2000038635）對話收到 `storage`，再送 C161 type4 存入一瓶藥水。盟主的存入已驗證；第二角色領取權限與 GUI 盟倉操作未跑，留給 SM 任務。

## 備份、還原與資料完整性

- 上一張冷備份：主專案 `environments/p24/backups/issue-1107-before/db-data`，原 manifest SHA-256 `916a407ad33ec6f387c42331763f9f9c138d5d98537bc92b940caecc0f940ffc`，逐檔核對通過，沒有覆寫。
- 本次完整冷備份：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1108\scratch\issue-1108\backup-before\db-data`，1,722 檔、279,489,118 bytes；同層 manifest SHA-256 `a860a6ed312407dd9e73c616a82f9be24fc351e2c7c4a82bdcd583bda94e9adb`。來源沒有 junction／symlink／多重硬連結。
- `baseline-readback/db-data` 是上述備份的另一份副本，只在主 MySQL 停止後暫用 43324 做邏輯讀回，沒有接遊戲服；它沒有承接本次準備結果。**最終測試資料在主專案 `environments/p24/db-data`。**
- 全部 70 張 `p24_accounts` 表比較，424→500 列，9 張表有差異：76 新增、8 更新、0 刪除。差異包含起始物品、建盟、正常恢復的 HP、登入時間；帳號密碼等機密值沒有進 Git。完整逐列前後對照見資料差異 JSON。
- [還原 SQL](issue-1108-restore.sql) 依最終資料與冷備份差異產生，涵蓋全部上述資料差異。84 條 DML 已以 **EXPLAIN** 核對語法與真實 schema；反向資料模型等於任務前快照；預檢在最終持久 DB 得 1。**沒有執行還原 DML**，沒有宣稱做過實際還原測試。
- 還原檔會先核對 port、完整 datadir、全角色離線／GM0、受影響表列數與完整資料；任一不符則所有 DML no-op。MyISAM 無交易回滾，執行還原前另做備份；後續若已有新測試寫入，不能硬改預檢讓它通過。系統檔／自增計數要完全回到先前狀態，使用完整冷備份。
- JAR SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；mysql.ini、JAR、server/config 合計 14 檔與 #1107 保護清單相同。伺服器用 worktree 的工作目錄，config／jar／maps 透過 junction 唯讀引用；未遞迴刪除或拆除任何目錄。

## 已驗證、待驗證與給下一棒的提示

已驗證持久資料、雙 session、一般玩家建角回包、寵物及召喚生成、建盟／入盟／存倉與最終讀回。GUI 用本次建置的客戶端到達 `CharacterList`，可存取性資料能列出角色與建角按鈕；Windows 擷取連續逾時，滑鼠操作回報 `coordinate input geometry is unavailable`。客戶端內建截圖 `client-service/manual-screen.png` 只是選角階段主畫面，**不作七職業 GUI 成功證據**。R07 因此保留 ⚠️，不是七職業被原服禁用。

1. **dot／codex-local 真機執行器**只需補 R07 的 GUI 建角／操作證據；資料已存在，不要重放補物品或再次建立同名角色。其他已通過項目不重做；#1109 可引用本次帳號、座標、物品與血盟資料準備其 SM 項目。
2. 主環境服務已關閉。下一時段重開持久 MySQL／原遊戲服即可讀到本次資料；不要改用 task-1098 副本或本張 `baseline-readback`。
3. **調度器清理 worktree 前，先保留 `scratch/issue-1108/backup-before` 與需要的原始證據到核准的持久位置。** 本張依原專案只讀限制，沒有另寫主專案備份目錄；Git 已保留可檢閱證據、完整資料差異及還原 SQL。不得遞迴刪除本 worktree，其 server 下有 junction。
4. 02:28 API 的 `leases` 為空，與派工宣告的持有人／到期值未核實；已記錄，依本張「程序疑慮只記錄、不停止」繼續，未修改調度器實作或鎖狀態。
5. 分支直接使用既有 `task/1108`，fast-forward 同步 `origin/main` 到 `77e9ca77`。`docs/REPORT.md` 只接受 main 的索引更新；沒有自行修改它或 dev lock。同步後建置通過；沒有修引擎／伺服器程式，也沒有測正式 2000／3306。
