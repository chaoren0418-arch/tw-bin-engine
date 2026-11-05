# Issue #76 — 範圍凍結：完成定義、主線封頂、backlog 分流

日期：2026-11-06；執行者：雲端 Claude 主控；分支：`task/76`（內容同 PR #20 的 `claude/clever-dirac-5q77j9`）。

## 做了什麼
- **步驟 0 盤點**：`PLAN.md` 新增「盤點：已知範圍」。來源是 P25 `completion-matrix.json`（32 列加 8 個補充列）、`finite-backlog.json`（12 項）、`work/P26–P38`、`docs/native-baseline`、`docs/design`、`docs/reports` 與 Issue #1–#76。每一項都對應到 M1、M2、M3 或 Backlog。
- **完成定義（凍結）**：`TASKS.md` 每個里程碑都有「驗收：使用者打開會看到什麼」。
- **主線封頂**：M0 12、M1 13、M2 5、M3 3，合計 33 項。不確定項 U1–U4 最多 +9，U5 估 0。
- **分流**：11 項移到 `## Backlog`（B01–B11），對照表見 PR #20 的描述。

## 怎麼驗證
- `grep -c '^### T' TASKS.md` = 33，與封頂數一致；Backlog 用表格，不使用 `### T` 格式。
- 只改 `TASKS.md`、`PLAN.md`、本報告，以及先前累積的規則與報告文件同步；沒有動產品程式。

## 未驗證什麼
- 調度台改成「只算 `## M*`」的解析邏輯，由調度器對話實作。

## 給下一棒
- 合併後，PR #20（同內容）可以關閉。
- 之後新工作一律先進 Backlog；分母只因不確定項 U1–U4 兌現而變動，PR 要引用當初的估算。

## 第 2 輪（依 2026-11-05 20:56 審查退回修正）
1. **P01–P24 盤點**：審查反例（P21 弓箭／遠程／彈藥）成立。U5「估 0」撤回，改開 #81 由 codex-local 在本機盤點。P21 暫列 M2 T072 範圍，並列入 PLAN 對應表。**#76 等 #81 完成、依結果補上對應後才封頂。**
2. **M0 驗收**：寫入前置條件（客戶端資源目錄、`engine/assets` junction、SDK、Python），證據改引 #13（新建 worktree 接好 junction 後建置加 check-only 都 exit 0）、#38、#70、CI。
3. **M3 驗收**：zip **不含任何遊戲資源**（含衍生的 `engine/assets`），由使用者自己的客戶端目錄提供；不涉及公開發布遊戲資源，所以不需要不可逆決策。新增不確定項 U6（使用者本機產生 assets 的工具或步驟，+1）。
4. **數字**：rebase 到最新 main（`bb27a49`），重新計算：33 項，24 完成、1 進行中、8 待辦，剩 9 項。現況表已更新（DEMO-2 已由 PR #78 合併、T050 已合併）。
5. **範圍外檔案**：`CLAUDE.md`、`AGENTS.md`、`.cursor/rules/project.mdc`、`docs/reports/issue-27.md` 已還原成 main 的版本，移出本分支。這些規則檔的狀態更新（build-product 指向 dev、CI 改 ubuntu、真機由 dot 執行的授權出處：#23 使用者決定與 commit `91a0d5c`）另外處理。
6. **金幣例外**：M1 凍結驗收已寫入唯一例外，跟 #77 一致：伺服器規則不允許丟金幣時（需 JAR 證據），改用另一個已啟用的可疊加物品證明循環，並把金幣列入 Backlog。
