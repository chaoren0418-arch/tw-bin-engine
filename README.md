> 2026-10-29 最新狀態：使用者授權新主管 **01a185b4-ecf6-705c-90e6-83b9eb1ba02f** 接管並實際新開 P25／P26／P27 工作對話。正式入口為 **r33**，r34 僅隔離 stage 接受，未正式合入；資料庫由使用者管理，不阻擋離線引擎開發。新主管 Goal 工具不可用，實際狀態未知。詳見 [現況、階段責任與接管證據](主管/P25-P27-新主管接管-20261029/HANDOFF.md)。下方為整理完成當時的歷史快照，不代表現行版本或停寫狀態。

# 自主 BIN 引擎專案

唯一專案根：`D:\lineage開發\TW1608262200研究`。

16 個外部資料夾已完成實體整併、來源核對及可回復去重。開發停留 P24／r28；整理完成不代表遊戲驗收完成。主管及工作者仍暫停，待本次交付接續新目標。

- [整理總覽（HTML）](docs/project-consolidation/index.html)
- [功能進度與未驗項目](docs/project-consolidation/progress.md)
- [16 路徑對照與分類](docs/project-consolidation/inventory-report.md)
- [專案結構與入口](docs/project-consolidation/structure.md)
- [執行收據、回復與下一步](docs/project-consolidation/migration-plan.md)
- [機器可讀 manifest](project-manifest.json)

正常客戶端入口：[啟動引擎.cmd](啟動引擎.cmd)。本輪只以 `--check-only` 核對，沒有啟動遊戲／服務。後續原始碼在 [engine](engine/README.md)，正式封存為 releases/r28，現用環境為 environments/p24。

原件保存在 recovery；重複檔以不可變硬連結共用，原重複本文仍可回復，因此本輪回收磁碟空間為 0。D:\BinEngine 只是本根的 ASCII 相容連結，沒有第二套實體專案。既有 P01～P24 目錄是根內研究歷史，禁止當成新的開發入口。
