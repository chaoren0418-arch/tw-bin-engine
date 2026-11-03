> 最新交接：整理停寫已解除，既有唯一主管已實際接收並開始搬移後環境煙霧驗收；通過前不派功能開發。Goal 仍回報 paused，工具未提供 active 恢復操作。下方整理結果保留完成當時快照。詳見 [恢復收據](physical-migration/resume-handoff.json)。

# 整理交付

完成時間：2026-10-28T18:20:31.138849+00:00。唯一根 `D:\lineage開發\TW1608262200研究`。B1～B6 已完成授權的 16 根實體整併與可回復去重；未恢復開發。

- 16 個舊 D 槽根已不存在；全部內容搬至根內的 environments/history。r28 已集中 releases/r28；engine 是後續唯一有效來源。
- 原始26,929檔全 SHA 核對；r28 seal 14,364檔全 SHA；正常入口 --check-only、engine 重建及14組 MySQL 配置驗證通過。
- 10,868 個重複部署檔改為不可變硬連結，2,957,933,947 bytes 原件仍在 recovery/duplicate-quarantine。回復抽查通過；永久刪除0，回收空間0。
- 14組 DB 冷備份及原設定已保留；只做檔案解壓／SHA回復，未驗 DB 啟動。1,295個r28易變證據僅核對大小／mtime／搬移收據。
- D:\BinEngine 是中文路徑相容 junction，非另一專案複本；既有 P01～P24 根內歷史及必要外部原始資產保留。
- 主管與三工作者停寫，無現行專案監聽埠；系統 MySQL 未觸碰。沒有待使用者解除的權限阻礙。仍未完成的 P24 遊戲驗收見 progress。

閱讀順序：[總覽](index.html) → [功能進度](progress.md) → [16路徑](inventory-report.md) → [結構](structure.md) → [下一步](migration-plan.md)。

機器收據：[result](result.json)、[manifest](../../project-manifest.json)、[最終保全](physical-migration/final-verification.json)、[回復抽查](physical-migration/rollback-probe.json)、[驗證更正](physical-migration/verification-correction.json)。
