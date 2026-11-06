# Issue #226／T123 執行報告

## 做了什麼

完成 #132 全部 103 個 C_、243 個 S_ 來源檔與 P24Lab d287 JAR 的唯讀核對，並補核正文提及的內部 `Teleportation.S_TeleportRecovery`。完整簽章、來源及 class SHA-256、opcode 常數、差異與受影響列收錄於 [版本對照](../reference/source-vs-jar.md)。

**來源快照與 d287 存在明確版本差異。** 346 個來源檔的結果為 334 個宣告方法集合相符、2 個有差異、9 個啟用類別在 JAR 缺席，以及 1 個全停用來源。

- `C_LoginToServer` 的來源多出 `traceLoginLife(ClientExecutor,L1PcInstance,String)`。
- `S_Message_YN` 的來源多出 `confirmation(Offer,Ticket,int,String)` 及五參數私有建構子，影響 S219 confirmation 入口。
- d287 缺少 `S_ClassicBadge`、`S_ClassicBagCombine`、`S_ClassicBoss`、`S_ClassicCollection`、`S_ClassicCraft`、`S_ClassicPass`、`S_ClassicQuest`、`S_ClassicRanking`、`S_ClassicReward`，影響 #132 的 S250 類別對照。
- 107 個 C 與 112 個 S opcode 常數、96 個具名 C 處理器分派全部相符。內部 recovery 類別的 4 個方法簽章亦相符。

## 怎麼驗證

- 使用搬移後的 `D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，核得 SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。開始時核實 `p24lab` lease 持有人及到期秒與派工一致。
- 346 個封包來源檔及 2 個 opcode 檔 SHA-256 均與 #132 相符，內部 recovery 的外層來源亦與 #132 規則表相符。
- JDK 17.0.17 的 `javap -p -constants` 列出宣告與常數，自建工具以 `JavacTask.parse()` 解析來源，解析錯誤 0。目標類別僅做靜態解析。
- `javap -p -c` 僅在記憶體分析 C 分派及 S 的首個直接 writeC 常數。交付只記簽章、數值與結論。class-file 方法表獨立核對 1,643 個宣告方法、231 個 synthetic 方法及 120 個類別初始化器的數量。
- 自動檢查 346 列覆蓋、219 個全域 opcode 常數、全部方法簽章收錄、內部連結、UTF-8、來源/JAR 前後雜湊及 `git diff --check`。文件任務採靜態檢查。

## 未驗證什麼

尚未核定 JAR 的確切建置提交、所有方法本文與封包條件等價性、目前程序載入的映像，以及登入／操作／畫面／聲音。簽章相符類別的完整 wire 行為仍由後續功能任務驗證。

本輪未讀配置、DB 或帳密，未執行目標 JAR 程式，未啟停遊戲、伺服器或 DB，未修改產品程式、`docs/REPORT.md`、原專案資源或封存。沒有複製來源本文或反編譯程式碼進交付。

## 產出路徑

- `docs/reference/source-vs-jar.md`
- `docs/reports/issue-226.md`

## 給下一棒的提示

Claude／調度器審查後交使用者合併。M6 執行者先查看版本對照的差異表，再使用 #132 的對應列。S219 raw confirmation 與 9 個 Classic serializer 應依 d287 可用入口核定契約。#132 差異 19 已有版本差異證據，其餘 18 列的行為判定仍須各功能任務取證，後續 P24Lab 原生 smoke 由 dot 接手。
