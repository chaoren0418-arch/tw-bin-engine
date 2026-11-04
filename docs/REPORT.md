# 執行報告

## 本輪變更

- 2026-11-04 — Issue #7（T013，test，第 1 次嘗試）：r35 主程式與 launcher 編譯成功、0 警告；完整命令退出 2，最終核驗拒絕 assets/integrity.json 的既有 junction。Receipt 已生成但不代表驗收通過，check-only 未執行；完整證據見 [issue-7.md](reports/issue-7.md)，分支 task/7。

- 2026-11-04 — Issue #3（test，第 2 次嘗試）：完成 r35 唯讀來源鎖定核驗，退出碼 0，181 個來源檔／190 個鎖定檔全部命中，SHA 不符檔名：無。完整輸出與驗證範圍見 [issue-3.md](reports/issue-3.md)，分支 task/3。
- 2026-11-04 — Issue #6（code，T004）：新建 [docs/BUILD.md](BUILD.md)（乾淨機器從零建置：SDK 10.0.300、Python 3.12、WebView2、資源目錄、restore／build-product.ps1／啟動引擎.cmd --check-only、r35 rebuild.py、CI 與常見錯誤）。驗證紀錄見 [issue-6.md](reports/issue-6.md)，分支 task/6。

## 下一步

- Issue #7：由調度器／Claude 審查 task/7，另派引擎維護者修正 r35 核驗與 assets junction 的相容性，並界定仍指向 r33 的根入口檢查；合併後由本機執行器重跑建置與 check-only，原生驗收由使用者執行。

- Issue #3：由調度器／Claude 審查 task/3 的報告並確認驗收；本輪未建置或做原生驗收，後續建置另行派工，實機測試由使用者執行。TASKS.md 的 NPC 服務 T020 與本 Issue 名稱重複，請以 Issue #3 範圍識別。
- Issue #6：Claude 審閱 docs/BUILD.md 與本 PR；CI 綠燈後由使用者在 Windows 乾淨目錄照文件重現 restore／建置／check-only。本輪未做原生實機驗收。
- 2026-11-04 — #13 T015: 本機重跑 r35 完整建置 + check-only（T014 合併後）（test，codex-local，分支 task/13）→ [報告](reports/issue-13.md)
