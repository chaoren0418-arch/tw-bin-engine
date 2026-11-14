# #1220：#1209 實機驗收失敗項目分流

結論：#1209（f1186-smoke-fixtures，resource 準備）結果 ✅16／❌0／⚠️2。兩個 ⚠️ 都是**測試資料條件**（公告板、拍賣板 NPC 在 P24Lab 世界裡沒有出生），不是引擎程式缺陷，所以**不開 code 任務**；開一張 resource（`f1209-board-fixture`）＋一張只測 SM27／SM29 的重測（`f1209-board-retest`）。另把 #1211（f1186-retest）的 SM27、SM29 移出、拿掉對 #1220 的依賴，讓大重測不必等這兩項。

## 讀了什麼
- #1209 最新 `=== RESULT ===`（調度器留言，failed，`codex-local`）。
- `docs/reports/issue-1209.md`、`work/f1186-smoke/fixtures.json`（`task/1209` 分支 `fadb1e4f`，PR #1219 **尚未合併**）。
- `docs/testing/smoke-m7.md` SM27、SM29 列及 §14.2／§15.1 補充；`docs/plans/issue-1201.json`（#1209、#1211 的來源計畫）。
- 所有 open Issue 內文（找 `1209`／`f1186`）：相關的有 #1211（depends_on 含 1209、1220）、#1210、#1206、#1216。

## 已通過（✅，之後不重測）
B01 冷備份、R01（SM08／09 空欄位）、R02（SM48 七職業）、R03（SM40 弓箭）、R04（SM30 釣具與座標）、R05（SM26 料理）、R06（SM49 傳送控制戒指）、R07（SM47 溶解／緩速）、R08（SM18 藥水）、R09（SM25／36 寵物）、R10（SM58 變身方法）、R11（SM19／21／50 座標）、R14（SM24／37 血盟）、R15（SM06 公告來源）、V01 範圍／還原、C01 收尾。

本輪沒有 code 任務，fixture 角色、物品、產品程式都不因本輪改變，所以**不需要附帶回歸任何 ✅ 項目**。

## 失敗項目對照
| #1209 項目 | 結果 | 分類 | 處理 |
|---|---|---|---|
| R12 SM27 公告板：定義存在、`spawnlist_npc` 無出生列 | ⚠️ | (b) 測試環境／資料 | `f1209-board-fixture` → `f1209-board-retest` 重測 SM27 |
| R13 SM29 盟屋拍賣：拍賣佈告欄 81161 四列 `count=0` | ⚠️ | (b) 測試環境／資料；若證實伺服器版本刻意停用則轉 (c) | 同上，重測 SM29 |

### 為什麼 resource 任務設成「先唯讀、改世界表就停」
- #175 授權的測試準備寫入只有：設定／改回 GM、建立測試角色、發測試物品。**新增 NPC 出生列或改 `count` 不在範圍內**，所以 `f1209-board-fixture` 只能先找不寫 DB 的辦法：(A) 其他出生表或伺服器動態生成（例如盟屋系統）已有可用的板子；(B) GM 指令暫時召出、不寫 DB（照 #160 暫設 GM 準備）。
- 兩者都不成立時走 (C)：寫好最小 SQL 與還原 SQL，但**不執行**，標 `needs:user` 由使用者裁定（只授權本次一列並測完還原，或列已知限制）。這是授權範圍的決定，不是可以自己擴大的事。
- (D) 若位元碼證明伺服器沒有該功能，就交回分流列為已知限制（伺服器不支援），重測不跑。
- 順帶：#1209 的完整冷備份只在 `scratch\wt\task-1209\environments\p24\backups\issue-1209-before`（本機確認 manifest 存在；主專案 `environments\p24\backups\` 下沒有）。worktree 清理會丟失它，所以排在 resource 任務第 0 步，逐檔核 SHA 複製到主專案備份目錄（不覆寫）。

## 已知限制（不開任務）
- 無新增。SM29 若 `f1209-board-fixture` 走到 (D)，屆時才列為已知限制（伺服器不支援盟屋拍賣）。

## 其他排隊任務的處理
- **#1211（f1186-retest）**：重測範圍比本張大很多（SM03–SM58），**不關閉**。已用 `gh issue edit` 修改內文：
  - `depends_on` 從 `[1205, 1206, 1207, 1208, 1209, 1210, 1220]` 改成 `[1205, 1206, 1207, 1208, 1209, 1210]`（拿掉 1220；#1209 已結案保留無妨）。
  - B 組刪除 SM27、SM29，加一段「分流 #1220 調整」說明這兩列改由 `f1209-board-retest` 測。
  - 前置加上「PR #1219（fixtures.json）已合進 main」：#1209 已結案不代表 PR 已合，#1211 需要 main 上的 fixtures.json。
  - 這樣做的理由：計畫中的新任務要在本張結束後才有 Issue 編號，無法直接寫進 #1211 的 `depends_on`；與其讓整個大重測卡在兩塊板子（可能還要等使用者裁定），不如把這兩列拆出去，其他 fixture 已備好，可照常開跑。
- **#1210、#1206**：只在來源段落提到 #1186／#1209，不依賴 #1209，不改。
- **#1216**：與 #1209 無關（依賴 #1215），不改。

## 產出
- `docs/plans/issue-1220.json`：`f1209-board-fixture`（resource、codex-local、runner_lock、real_machine）、`f1209-board-retest`（test、codex-local、runner_lock、real_machine、milestone_smoke，depends_on `f1209-board-fixture`；內文前置要求 #1211 已結案、用 main 重建）。
- 本報告。

## 怎麼驗證／未驗證
- 已驗證（只讀）：#1209 RESULT 與報告原文；PR #1219 狀態 OPEN；#1209 備份 manifest 存在於 task-1209 worktree、主專案沒有；open Issue 依賴關係；#1211 修改後 `depends_on` 讀回正確；計畫 JSON 可被 `json.load` 解析（2 項）。
- 推測：一般公告板缺出生列、拍賣板 count=0 是 P24Lab 伺服器資料的原樣設定，不是 #1186／#1209 改出來的（#1209 V01 的 70 表逐列比對顯示既有列 0 更新）。
- 待驗證：公告板／拍賣板是否有不寫 DB 的取得方式（`f1209-board-fixture`）；SM27、SM29 的原生 UI 行為（`f1209-board-retest`）。
- 未執行任何程式、未碰 DB、未啟動伺服器或客戶端。

## 給下一棒的提示
- 調度器：PR #1219 要先合進 main，#1211 才有 fixtures.json 可用；task-1209 worktree 在 `f1209-board-fixture` 保全備份前請不要清理。
- `f1209-board-fixture` 若標 `needs:user`，請本機調度器對話把 (C) 的 SQL 與兩個選項轉給使用者；`f1209-board-retest` 會因 depends_on 自動等待。
- `f1209-board-retest` 要在 #1211 之後跑（用它驗收過的工具與字型修正）；這條寫在內文前置，計畫 JSON 無法表達對既有 Issue 編號的依賴。
