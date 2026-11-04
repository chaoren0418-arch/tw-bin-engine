# Issue #19 — T016 交接紀錄

日期：2026-11-04；階段：test；第 3 次嘗試；分支：`task/19`。

## 做了什麼／怎麼驗證

本次從 `8af37a6c3077cee006858c13f18d92d0b016baf2` 續查。主目錄追蹤檔乾淨，僅有未追蹤舊 receipt；assets junction 與離線 WebView2 套件存在，既有 EXE／DLL 雜湊與 #13 仍不一致。

新增執行主目錄 r35 預設唯讀來源核驗，退出 0，回傳 181 個來源檔、190 個鎖定輸入、writes=false。與本機既有 origin/main 比對，確認主目錄 rebuild.py 尚無 T014 的 junction 修正。完整命令、輸出與雜湊見 [issue-16.md](issue-16.md)。

## 未驗證什麼

T016 未完成。主目錄更新、完整建置、寫 receipt 與 check-only 都未執行，沒有本次 check-only JSON，也未啟動遊戲。本次仍同時要求主目錄唯讀與主目錄建置，已提出寫入範圍確認，尚待回覆。一般執行環境初始化故障可透過獲准的提升權限命令處理，不是自動核准審查拒絕或建置失敗。

## 產出路徑

- `docs/reports/issue-16.md`：完整檢查報告。
- `docs/reports/issue-19.md`：本次交接。
- 沿用既有 `task/19`，不 checkout／建分支，不修改 docs/REPORT.md。

## 給下一棒的提示

請調度器／本機對話先修正派工文字，明確允許以下例外；建議採用：

> 本次允許在主專案 D:\lineage開發\TW1608262200研究 執行 Git fetch、切換 main、ff-only 更新，以及 r35 建置所需的 build／launcher／obj／bin／套件快取與 receipt 寫入。報告仍只寫 task-19 worktree。封存、客戶端、硬連結、機密設定與正式入口仍禁止修改；不得啟動遊戲或連正式服務／DB。

以上是待確認的派工文字，不是本次已取得的授權。不要僅以相同矛盾文字重排第四次，也不要將 T016 標記完成。

獲准後由本機執行器重查主目錄追蹤檔，依序 fetch、checkout main、merge --ff-only、完整建置、check-only。保留使用者檔案；receipt 僅在取得建置寫入允許後由指定命令重新產生。先檢查建置寫入路徑無封存／硬連結，再執行；日誌與暫存指定 task-19 worktree 的 scratch，避免預設建立帶時間戳證據目錄，且須將 NuGet 等快取限制在專案內。

取得退出 0、revision=r35、uiStarted=false 的真實證據後，補上 JSON、大小及雜湊比較，再由使用者執行 `D:\lineage開發\TW1608262200研究\engine\啟動新版客戶端.cmd` 做 M1 原生驗收。
