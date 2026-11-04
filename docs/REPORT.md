# 執行報告

## 本輪變更

- 2026-11-04 — Issue #13（T015，test，第 2 次嘗試）：T014 合併後 r35 完整建置與 engine 入口 check-only 皆退出 0；主程式與 launcher 各 0 警告、0 錯誤，回傳 `status: checked-only`、`revision: r35`、`uiStarted: false`。第 2 次補齊文件並核對既有證據，未重建；完整 JSON、receipt SHA-256 與產物清單見 [issue-13.md](reports/issue-13.md)，分支 task/13。

- 2026-11-04 — Issue #7（T013，test，第 1 次嘗試）：r35 主程式與 launcher 編譯成功、0 警告；完整命令退出 2，最終核驗拒絕 assets/integrity.json 的既有 junction。Receipt 已生成但不代表驗收通過，check-only 未執行；完整證據見 [issue-7.md](reports/issue-7.md)，分支 task/7。

- 2026-11-04 — Issue #3（test，第 2 次嘗試）：完成 r35 唯讀來源鎖定核驗，退出碼 0，181 個來源檔／190 個鎖定檔全部命中，SHA 不符檔名：無。完整輸出與驗證範圍見 [issue-3.md](reports/issue-3.md)，分支 task/3。
- 2026-11-04 — Issue #6（code，T004）：新建 [docs/BUILD.md](BUILD.md)（乾淨機器從零建置：SDK 10.0.300、Python 3.12、WebView2、資源目錄、restore／build-product.ps1／啟動引擎.cmd --check-only、r35 rebuild.py、CI 與常見錯誤）。驗證紀錄見 [issue-6.md](reports/issue-6.md)，分支 task/6。

## 下一步

- Issue #13／#7：T014 已修正 assets junction 相容性，T015 已完成 r35 完整建置與 engine 入口 check-only；Issue #7 的該項阻塞已解除。由調度器／Claude 審查 task/13 的補件與證據，再交使用者合併，並準備 M1／T011 的 N01 原生實機驗收；啟動遊戲、登入與畫面驗收僅由使用者執行。根目錄入口仍指向 r33，本次未驗證或切換正式入口；checked-only 不代表遊戲流程通過。

- Issue #3：由調度器／Claude 審查 task/3 的報告並確認驗收；Issue #3 當輪僅做來源核驗；後續完整建置與 check-only 已由 Issue #13 完成，實機測試仍由使用者執行。TASKS.md 的 NPC 服務 T020 與本 Issue 名稱重複，請以 Issue #3 範圍識別。
- Issue #6：Claude 審閱 docs/BUILD.md 與本 PR；CI 綠燈後由使用者在 Windows 乾淨目錄照文件重現 restore／建置／check-only。本輪未做原生實機驗收。
- 2026-11-04 — #13 T015: 本機重跑 r35 完整建置 + check-only（T014 合併後）（test，codex-local，分支 task/13）→ [報告](reports/issue-13.md)
- 2026-11-04 — #19 T016: 在本機主專案目錄建置 r35，作為 M1 真機驗收用的版本（test，codex-local，分支 task/19）→ [報告](reports/issue-19.md)
