# Issue #1119：R07 七職業 GUI 建角重測

**結論：阻塞。前置 f1108-gui-capture（#1118）尚未提供可用的 GUI 方法，本輪最小擷取檢查亦失敗，R07 一項都未完成。** 合併歷次結果仍是 ✅ 13（全部沿用）／❌ 0／⚠️ 1；不是本輪新通過 13 項，也不是引擎建角失敗。

2026-11-14 04:10（台北）完成本輪擷取檢查與收尾。既有 `task/1119` 分支，來源 `50679105c5e9643fccc490e1728412730867b8aa`；fetch 後與 origin/main 相同，未 checkout／建分支。未改程式、dev lock 或 `docs/REPORT.md`。

## 逐項結果

下列 13 個「沿用」列保留 #1108 的觀察及重現步驟，**本輪沒有重測**。其證據路徑屬於 #1108 的 `scratch/issue-1108/` 或 Git 報告；不能當作本輪量測。來源為 [#1108 報告](https://github.com/chaoren0418-arch/tw-bin-engine/blob/f416df309bdcc8b2e44a4462ae14a847fb355ef5/docs/reports/issue-1108.md)及其證據 JSON。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| B01 備份 | ✅ 沿用 | #1107 備份完整；目前 DB 有 19 檔與其不同，另做 1,722 檔完整冷備份並逐檔比對 SHA-256 | 兩埠空閒，禁止寫入的 ibdata1 handle 保護下複製及比對 | #1108：`snapshot.json`、`backup-before/manifest.json`；Git 證據 `snapshot` |
| R05 雙角色 | ✅ 沿用 | 兩個不同帳號同時在 map 4、距離 1 格；雙方收到對方 S87，SQL 同時 OnlineStatus=1 | 離線準備相鄰座標；各自登入、C137 進世界、C95 時間探測，保持兩連線讀回 | #1108：`dual-clan.json`、`dual-online.tsv`、`dual-*-packets.jsonl`；Git 證據 `dual` |
| R07 七職業 | ⚠️ 阻塞 | 本輪 GUI 0/7、送出 0 次；前置 #1118 未通過，本輪記事本擷取兩次仍逾時 | 新開記事本 → 選窗、啟用、擷取 → 重新選窗後重試一次；均失敗 | 本輪 `issue-1119-evidence.json` 的 `capture` |
| R08 弓箭 | ✅ 沿用 | 服務驗證持有 172×1、40743×200，原本即成立 | 主 DB 初次與最終 SELECT | #1108：`items-before.tsv`、`final-readback.tsv` |
| R09 肉 | ✅ 沿用 | 40056×19，未補發或消耗 | 同上 | #1108：同上 |
| R10 戒指／卷軸 | ✅ 沿用 | 新增 20288×1、40100×20，持久 DB 最終仍成立 | 跨四個實驗 schema 查 ID 不衝突後重放 INSERT，再讀回 | #1108：`item-id-audit.json`、`applied.sql`、`items-after.tsv` |
| R11 料理書 | ✅ 沿用 | 新增 41255×1，持久 DB 最終仍成立 | 同 R10 | #1108：同上 |
| R12 寵物／召喚 | ✅ 沿用 | 資源法師 LV52、技能 51；項圈叫出資源牧羊，召喚術另生成 gfx 3357；登出後笛 9、寶石 57 | 離線準備測試資料，重開原服載入；GM0 登入後 C164 使用項圈、C6 施放一次召喚 | #1108：`roles-preparation-*.tsv`、`pet-summon.json`、`pet-summon-packets.jsonl`；Git 證據 `pets` |
| R13 血盟／盟倉 | ✅ 沿用 | 資源血盟 10171；服務驗證盟主 rank 4，透明王子 rank 8；盟倉治癒藥水×1 | GM0 C222 建盟；面對面 C194 申請、收到 S219/97 後 C121 接受；向索連正常送存倉請求 | #1108：`clan-warehouse.json`、`clan-members-after.tsv`、`warehouse-*.tsv`；Git 證據 `clan` |
| R14 金幣 | ✅ 沿用 | 建盟扣 30000：100000→70000；離線補回 30000，最終 100000 | 確認指定堆疊／持有人及數量後條件 UPDATE，讀回 | #1108：`gold-before-replenish.tsv`、`gold-after-replenish.tsv`、`final-readback.tsv` |
| R15 GM | ✅ 沿用 | 全部 17 角色 AccessLevel=0、OnlineStatus=0；全程未設 GM | 最終全角色讀回，與冷備份資料對照 | #1108：`final-readback.tsv`；Git 證據 `characters` |
| C01 變更邊界 | ✅ 沿用 | 70 張帳號庫表前後比較：76 新增、8 更新、0 刪除；只有測試角色／帳號及其關聯資料改變；14 個保護檔 hash 相同 | 冷備份另複製到隔離目錄，以同一 43324 順序啟動讀回；與主 DB 最終快照比較 | #1108：`row-diff-summary.json`、`protected-final.json`；Git 資料差異與證據 |
| C02 程序收尾 | ✅ 沿用 | 所有本次 Java、MySQL、客戶端及 WebView 程序均結束；42061／43324 無監聽 | 按自啟 PID 核對後結束；MySQL 用 mysqladmin 正常 shutdown；最後列程序／監聽 | #1108：`shutdown.json`；Git 證據 `shutdown` |
| V01 建置／還原核驗 | ✅ 沿用 | 首次建置 0 錯誤／4 既有警告；同步後增量 build 0 錯誤／0 警告；84 條還原 SQL 的 EXPLAIN 通過，預檢=1 | build、逐條 EXPLAIN、反向資料模型與備份全列比對、git diff --check | #1108：`build.log`、`build-after-sync.log`、`restore-preflight.tsv`；Git 證據 `restoreVerification` |

## R07 本輪子步驟

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| R07-P GUI 前置 | ⚠️ | 新開記事本第一次 `FrameArrived timed out`；重新選窗重試後 `window capture timed out`，均附 `timed out waiting on channel` | `sky.list_windows` → 唯一記事本 → `get_window` → `activate_window` → `get_window_state`；重新選窗後重試一次 | [本輪證據](issue-1119-evidence.json) `capture` |
| R07-1 建置／登入 | ⚠️ | 未建置、未啟動 MySQL／原服／客戶端、未登入；GUI 前置仍不可用 | 前置恢復後才依任務建置並以 `--classic-ui` 登入 p24svc01 | 本報告、證據 `notPerformed` |
| R07-2 王族／騎士／妖精／法師／黑暗妖精／龍騎士／幻術師 | ⚠️ | 七職業全數未操作；「可以建立」截圖 0/7，未取得 Validate 錯誤 | 待前置通過後逐職業合法配點至剩餘 0、截圖、取消 | 本報告；沒有可交付截圖 |
| R07-3 真正送出 | ⚠️ | 未建立角色，無本輪 S98／S127、選角清單或 DB 讀回 | 待七職業畫面可驗證後，使用唯一空位及不含「測試」的新名送出 | 證據 `results` |
| R07-4 GUI 刪角 | ⚠️ | 未建立角色，因此沒有可刪的本輪角色；未刪任何既有角色 | 僅在步驟 3 成功後刪除該新角色，觀察立即刪除或排程 | 證據 `results` |
| R07-5 收尾 | ⚠️ | 自啟記事本 PID 5904 已結束、兩埠無監聽；未啟動 DB，未做本輪全角色 SELECT，GM0／OnlineStatus0 僅沿用 #1108 | 按 PID＋程序名＋啟動時間核對後結束；查 42061／43324 LISTEN | 證據 `cleanup` |

## 已驗證／推測／待驗證

- **已驗證**：本輪記事本 HWND 2753956 的兩次擷取均失敗；沒有成功截圖。自啟記事本 PID 5904 已結束，2026-11-13 20:10:58 UTC 的 42061／43324 監聽清單為空。未啟動任何遊戲、Java 或 MySQL 程序，未查寫 DB，未補物品、未動血盟／寵物資料；不涉及備份或還原 SQL 的新變更。原始專案僅讀取報告；所有本輪檔案均寫在本 worktree。
- **已驗證的前置依據**：[#1118 報告](https://github.com/chaoren0418-arch/tw-bin-engine/blob/a60fc6f3d5deff981e5dd37e6810cda224c780ce/docs/reports/issue-1118.md)明列驗收 (a)、(b) 均未成立，無座標替代方法也未驗證，與派工所列「已確認可用」不一致。本輪並非僅因文字疑慮停止，而是再實測仍有擷取故障。
- **推測**：GUI 擷取後端問題仍持續；確切根因未確認，不能歸因於引擎、職業規則、DPI 或桌面鎖定。
- **待驗證**：R07 所有 GUI 配點、送出、刪角與本輪 DB 收尾讀回。未跑 SM08／SM09／SM48。#1108 七職業協定建角成功證據保持有效，但不能替代本輪 GUI 驗收。
- lease 已核實 holder 為 `chaoren0418-arch/tw-bin-engine#1119`，exp 為 `2026-11-13T21:39:13.976906+00:00`，與派工到秒值一致且檢查時有效；沒有改動鎖或調度器。
- 使用 `computer-use` 的恢復規則：「Refresh the app/window selection and retry once; report the exact error if recovery fails.」本輪已重新選窗重試一次；不再用失效座標或反覆擷取。技能指引：`C:/Users/CHAOREN/.codex/plugins/cache/openai-bundled/computer-use/26.1103.32239/docs/guidance.md`。

## 產出與給下一棒的提示

- 產出：`docs/reports/issue-1119.md`、`docs/reports/issue-1119-evidence.json`；本機原始紀錄在 `scratch/issue-1119/`。來源報告與證據的 SHA-256 收在本輪 JSON。驗證使用 JSON 解析、結果計數、收尾紀錄與 `git diff --check`；未宣稱完成建置或真機建角驗收。
- **交 needs:local 處理 #1118 的 GUI 擷取環境，調度器應先等前置通過再派 R07。** 先證明普通視窗可擷取，再取得開發工具建角對話框可操作且可截圖的方法；不要將本輪 ⚠️ 開成引擎 code 修正。
- 前置修復後只重測 R07：七職業各一張可建立截圖、一次 GUI 送出並 S98=2＋清單／DB、僅刪本輪新角色、收尾讀回及關閉自啟程序。既有 13 項繼續沿用；p24svc01 的 3/4 槽是 #1108 最後觀察，本輪沒有重新查證。
