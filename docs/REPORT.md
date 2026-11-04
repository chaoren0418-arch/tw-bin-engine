# 執行報告

## 本輪變更

- 2026-11-04 — Issue #3（test，第 2 次嘗試）：完成 r35 唯讀來源鎖定核驗，退出碼 0，181 個來源檔／190 個鎖定檔全部命中，SHA 不符檔名：無。完整輸出與驗證範圍見 [issue-3.md](reports/issue-3.md)，分支 task/3。
- 2026-11-04 — Issue #12（code，T014）：`engine/_integration/r35/rebuild.py` 的 `resolve()` 改為 lexical `os.path.normpath` + `is_relative_to(ROOT)`，不再跟隨 `engine/assets` junction；對應 Issue #7 的 `REFUSED: input path escapes root: assets/integrity.json`。新增 [test_rebuild_paths.py](../engine/_integration/r35/test_rebuild_paths.py)；報告見 [issue-12.md](reports/issue-12.md)，分支 task/12。

## 下一步

- Issue #3：由調度器／Claude 審查 task/3 的報告並確認驗收；本輪未建置或做原生驗收，後續建置另行派工，實機測試由使用者執行。TASKS.md 的 NPC 服務 T020 與本 Issue 名稱重複，請以 Issue #3 範圍識別。
- Issue #12：Claude 審查 task/12 後合併；本機執行器重跑 Issue #7／T015 完整建置，確認不再因 assets junction 退出 2。本輪未做 Windows 真機 `--build`。
