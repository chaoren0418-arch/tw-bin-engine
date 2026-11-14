# #1266 把 SM27／SM29「甲：GM 暫時召出」裁定寫進規則與 smoke-m7

## 結論
已完成（只改文件）。使用者裁定 **SM27 甲、SM29 甲**（#1262 留言，2026-11-14）已寫回三份規則複本、smoke 清單與 #1262 報告。

## 做了什麼
1. `CLAUDE.md`「資料庫」段新增一條例外（日期 2026-11-14、出處：調度器雲端主導對話以選單取得、#1262 留言；適用：SM27／SM29 測試準備，#1263、#1264 與之後同類重測；條件：`.2 80006 1 0`／`.2 81161 1 0` 暫時召出、只在記憶體、不用 `.npc`、不寫出生表、`log_GM指令` 稽核列不刪不規避、只用測試角色、正式 2000／3306 不碰）。
2. 同一行同步到 `AGENTS.md`、`.cursor/rules/project.mdc`。
3. `docs/testing/smoke-m7.md` SM27、SM29 各加「測試準備」一條，寫甲的做法（SM29 另註站位 map4 `(32611,32775)` 與拍賣資料 2026-11-19 01:00（UTC+8）到期要重新核對，來源 #1259）。
4. `docs/reports/issue-1262.md` 補「裁定」段：原文、時間、出處、哪些後續由本張完成。

## 怎麼驗證
- `git diff --no-index CLAUDE.md AGENTS.md` 無差異；`CLAUDE.md` 與 `project.mdc` 差異只有原本的 front matter。
- `git diff --stat` 只有上述 5 個文件；沒改產品程式、`docs/REPORT.md`、rebuild-lock。
- 沒執行任何程式、沒碰 DB／伺服器／客戶端。

## 未驗證
- `.2 80006 1 0`／`.2 81161 1 0` 實際召出效果與稽核列筆數（交給 #1263、#1264 的 precheck／retest）。

## 產出路徑
- `CLAUDE.md`、`AGENTS.md`、`.cursor/rules/project.mdc`
- `docs/testing/smoke-m7.md`（SM27、SM29 段）
- `docs/reports/issue-1262.md`、`docs/reports/issue-1266.md`

## 給下一棒的提示
- #1263／#1264 執行時：只用 `.2`，不要用 `.npc`（會寫出生表）；RESULT 記下 `log_GM指令` 新增幾列（照常保留）。
- SM29 拍賣資料 2026-11-19 01:00（UTC+8）到期，過期就要先重新核對拍賣條件，不能直接沿用。
