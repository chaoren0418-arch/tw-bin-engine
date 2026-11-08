# #503 M7 重建補缺

**做了什麼**
- 依使用者 2026-11-08 17:53 指示，在 `TASKS.md` 新增：
  - 「暫行政策」：只做 TW BIN；審查與驗證暫緩；runner 一律 claude-local。
  - 「M7 重建補缺」：主線 1 項 T202（#504，差距盤點，產出 `docs/reference/gap-matrix.md`），加上不確定項 U9。
- `PLAN.md`：新增 U9（依 gap-matrix 開出的 code 任務，最多 +40）與 M7 里程碑；封頂改為 140 項，上限 180。
- 佇列調整：
  - T172（#446）：前置 T171 已合併，改為 queued；runner 改為 claude-local。
  - T151（#414）：重排一輪只做同步。
  - #475 原生 UI smoke：暫緩，改為 blocked。
- 檢查後確認已沒有指派給 codex-local 的開啟任務。

**怎麼驗**：`gh api` 逐一核對標籤；`TASKS.md` 中 M7 區段只有 T202 一項。

**沒驗什麼**：gap-matrix 本身由 T202 產出；依它開 code 任務是下一步。
