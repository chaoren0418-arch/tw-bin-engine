# Issue #157 封存素材還原

## 結論與做了什麼

**已完成還原及全量驗收。** 從 r29 單檔複製 13,958 檔至 r28，再建立 r33 對應的 13,958 個 hardlink。r28–r33 共 86,276 個 seal 預期路徑全部通過，缺檔與 SHA 不符皆為 0。r29–r32 的逐檔 SHA、大小、mtime、File ID、link count 與還原前一致。

讀取 `AGENTS.md`、`CLAUDE.md`、`docs/REPORT.md` 與 #153 證據。fetch 後依指示將 `task/157` 建於 `origin/main` 的 `f52f80c610c5d49abe7f2c3d237d88ff43e3cb53`，再合入既有遠端 `task/157` 以保全前輪阻塞紀錄。核驗基線為 `420d97e613388b2e07868b5e6690e919397ca8ed`。本輪結果取代前輪阻塞報告，歷史由 Git 保留。

## 前置與執行依據

- #155 的 PR #158 已於 2026-11-06 16:44:33 UTC 合併，還原前實讀 `engine/assets` 的 junction 目標為 `releases/r29/assets`。#170／PR #171 將 #157 一次性還原例外加入 `CLAUDE.md`，本輪依該例外補回缺檔。
- 依主控 16:51 UTC 與 18:39 UTC 的後續裁定，保留歷史 junction，改以 `scratch/wt/` 無指向 r28／r33 的 junction、封存可讀、沒有其他可能清理 worktree 的執行中專案任務為閘門。[前置調整](https://github.com/chaoren0418-arch/tw-bin-engine/issues/157#issuecomment-6690336331) · [重新派工裁定](https://github.com/chaoren0418-arch/tw-bin-engine/issues/157#issuecomment-6692539651)
- 還原前重新掃描 93,149 個實體目錄節點，沿途遇 reparse point 即記錄並停止向下遍歷。發現 14 個歷史 r28 連結，`scratch/wt/` 阻塞連結 0。11 處存取拒絕全位於 `work/`，`releases/` 及 `scratch/wt/` 讀取錯誤均為 0。完整清單收入 `verify.json`。
- `2026-11-06T18:49:05.727686+00:00` 查詢 `localhost:8787/api/state`，本專案僅 #157 執行中，活動 code 任務 0，狀態為即時。逐檔執行前另檢查來源與目的路徑各層皆為實體路徑。

## 怎麼驗證（已驗證）

先由 Git HEAD 讀取 `r28-pins.json`、`reference-pins.json` 與 #153 缺檔清單，核對本機資料內容及六份完整 seal 自身 SHA。還原前完整核驗一次，確認每版 r28／r33 恰缺 13,958 檔，其餘封存檔 SHA 全部符合。

每個 r28 檔案均先核對 r29 來源 SHA 等於 seal、目的不存在，再以 `Copy-Item` 單檔複製，立即核對目的 SHA。r33 以相同條件核對 r28 來源後，使用 `New-Item -ItemType HardLink`，立即核對 SHA。操作紀錄共 27,916 筆，路徑唯一且均標記核驗通過。

| 版本 | 預期檔數（含 seal） | SHA 與大小符合 | 缺檔 | 不符 |
|---|---:|---:|---:|---:|
| r28 | 14,365 | 14,365 | 0 | 0 |
| r29 | 14,287 | 14,287 | 0 | 0 |
| r30 | 14,297 | 14,297 | 0 | 0 |
| r31 | 14,330 | 14,330 | 0 | 0 |
| r32 | 14,343 | 14,343 | 0 | 0 |
| r33 | 14,654 | 14,654 | 0 | 0 |

全面重驗時間為 `2026-11-06T19:02:03.627473+00:00` 至 `2026-11-06T19:03:21.626496+00:00`。逐一確認 13,958 組 r28／r33 檔案的 NTFS File ID 相同且 link count 為 2，與 r29 的獨立 File ID 不同，r29 link count 為 1。既有 r28／r33 seal 內檔案及 r29–r32 全部 seal 內檔案的前後紀錄一致。

刪除 0 項、覆寫 0 檔、junction 修改 0 個。Git 交付僅兩份報告證據，素材留在本機封存目錄。

## 可重跑腳本與執行摘要

腳本位於 `scratch/issue-157/`，本輪 Prepare、Restore、Verify 三階段退出碼皆為 0。

| 腳本 | SHA-256 |
|---|---|
| `scan-junctions.py` | `cc2ae883ab6e166f1ab19e75d379788adc1ecea0735998f1a28a028b0b830b5f` |
| `verify-seals.py` | `e3b3614ef2072337648f0d28fa6d48f7954544e27d5e3ff07b9a6d2b5793887c` |
| `restore.ps1` | `3f0a306fe27d922c717887b279df07c5659f4263c67d7375c43d9b3f548d32c4` |

```powershell
# 在專案根目錄，已還原後重跑唯讀全面核驗
& ./scratch/issue-157/restore.ps1 -Mode Verify
```

`Prepare` 執行前置掃描與還原前全量核驗，`Restore` 再查前置並逐檔補回。目的檔已存在時兩者會中止，成功還原後請使用 `Verify`。部分執行失敗也會中止，應先比對逐檔 log 與現況再另行處理。

逐檔 log 的 SHA-256 為 `59e7bd68d397cd347578bb492a65a7b6dab2a2555e3dbd1b0b2b3d5825fc6016`。前後完整逐檔紀錄、還原計畫及 log 保存在 scratch，摘要與腳本 SHA 收入提交證據。

## 未驗證什麼

- seal 排除檔的歷史正確性仍待取得基準，其中 r28 的 129 個排除檔沿用 #153 的範圍限制。
- 11 個拒絕讀取的歷史測試目錄內部與專案外 junction 的完整性留待其管理任務查核。
- 本輪驗收範圍為封存 byte 內容與 hardlink 結構，建置、check-only、遊戲、伺服器及 DB 均未執行。

## 產出路徑與給下一棒的提示

- `docs/reports/issue-157.md`
- `work/issue-157/verify.json`
- 本機腳本及逐檔證據位於 `scratch/issue-157/`。
- 交調度器／Claude 審查 `task/157` 與既有 PR #159 的本輪通過證據，合併後結案。後續建置或真機任務可接續安排，真機交授權的 dot。#157 結案後一次性封存還原例外失效。
