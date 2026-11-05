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
