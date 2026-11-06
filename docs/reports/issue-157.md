# Issue #157 封存還原：前置條件未通過

## 結論／做了什麼

**本輪阻塞，未執行還原，不能視為驗收通過。** #155 的 PR #158 已於 2026-11-06 16:44:33 UTC 合併，`engine/assets` 確認指向 `releases/r29/assets`；但專案內仍有 **14 個 junction 指向 r28**，且 **11 個歷史測試目錄存取被拒**，不符合本任務「開始前確認沒有任何 junction 指向 r28／r33」的前置條件。

讀取 `AGENTS.md`、`docs/REPORT.md`、#153 證據及 #155／#156／#157 討論。#156 主控已將裁定 A 的還原計畫寫入 #157，不需再提請使用者裁定。fetch 後執行 `git checkout -B task/157 origin/main`，待 #155 合併後再次同步，最終基線為 `1a1cabfd9c6991767fd5560c868f1030ad99a452`，並核對包含 #155 的提交 `ebfa97181588a66b4b78af4fff4754fbfa86afea`。

本輪新增報告、核驗摘要與 scratch 腳本。還原 0 檔、建立 hardlink 0 個、刪除 0 項、覆寫 0 檔、修改 junction 0 個。保留原有未追蹤 `engine/_integration/dev/receipt.json`，未改 `docs/REPORT.md`。

## 怎麼驗證（已驗證）

- 以 Python 3.12 `os.scandir` 遍歷專案實體目錄樹，不進入 reparse point，另解析連結最終目標。掃描 92,128 個目錄節點，記錄 84 個連結、14 個阻塞連結、11 處存取錯誤。掃描範圍是本專案；未宣稱已掃描整台電腦。
- 已登錄的 Git worktree 均位於專案內；已知 worktree 的素材 junction 經主目錄解析至 r29。阻塞連結集中在既有 `work/` 與研究歷史目錄。
- `verify-seals.py prepare` 先以目前 HEAD 的 Git pins 核對 r28–r33 的 seal 檔案 SHA，全部符合；隨後在 junction 閘門以 `AssertionError: junction precondition failed`、退出碼 **1** 中止，沒有進入素材核驗或還原。
- 重新查核 #153 缺檔清單的路徑存在性：r28 13,958 檔、r33 13,958 檔，合計 27,916 個路徑仍不存在。這只是存在性查核，不是素材全量 SHA 驗收。
- Python 腳本通過記憶體語法解析，未產生 `__pycache__`。還原 PowerShell 腳本僅為未執行草稿，不宣稱已實測。

以下 13 個 junction 的目標均為 `releases/r28/assets`：

- `work/engine-slow-utility-source-r1/assets`
- `work/engine-slow-utility-review-r1/r31-baseline-r1/assets`
- `work/engine-slow-utility-review-r1/candidate-review-r1/assets`
- `work/engine-slow-review-r1/candidate-review-r1/suite-scratch/assets`
- `work/engine-slow-r2/assets`
- `work/engine-slow-r1/assets`
- `work/engine-slow-pickup-review-r1/assets`
- `work/engine-slow-haste-item-r1/assets`
- `work/engine-haste-actions-review-r1/assets`
- `work/engine-haste-actions-review-r1/candidate-review-r1/assets`
- `work/engine-haste-actions-r1/assets`
- `work/engine-ground-lifecycle-review-r1/assets`
- `work/engine-ground-lifecycle-review-r1/candidate-review-r1/assets`

第 14 個為 `P24-原2x2地圖快取與跨區載入/reference/development/release-integration-r12/delivery`，目標是整個 `releases/r28`。未在已成功掃描部分發現指向 r33 的連結。11 處拒絕讀取目錄及完整 target／resolved 清單見 [verify.json](../../work/issue-157/verify.json)。

## 可重跑腳本與執行摘要

腳本僅存在 `scratch/issue-157/`，不提交。

| 腳本 | SHA-256 | 本輪執行 |
|---|---|---|
| `scan-junctions.py` | `6c854fdbacd18fbb717dde6ed20841061422b284943b7b211bb367a198136eea` | 完成唯讀掃描；結果含阻塞與讀取錯誤 |
| `verify-seals.py` | `234497f31f653db49885032c6830e3307efd4d5301139081b42bcc3454565e79` | `prepare` 於 junction 閘門退出 1 |
| `restore.ps1` | `ef4ad9903fdabc2abfc64bb65453aa7bdfd2f352c02e0049db376dbdd8eff9e0` | 未執行草稿 |

唯讀重跑指令（在專案根目錄）：

```powershell
py -3.12 -B -X utf8 scratch/issue-157/scan-junctions.py
py -3.12 -B -X utf8 scratch/issue-157/verify-seals.py prepare
```

掃描腳本即使找到阻塞仍會保存 JSON 並正常退出；第二個指令才是拒絕繼續的閘門，必須檢查其退出碼。重跑前應先同步並核對基線。還原草稿應在前置處理完成後再審查及執行；不能直接略過閘門。證據產生腳本 `write-blocked-evidence.py` 的 SHA 另記於核驗摘要。

## 未驗證什麼

- 未還原 r28／r33，未做修復後完整 seal 的逐檔 SHA 核驗，未重驗 r29–r32 全量素材；本輪沒有寫入任何 release。
- 無法讀取的 11 個目錄內是否另有 junction 未知；專案外連結亦不在此次掃描範圍。
- 未驗證 seal 排除檔的歷史正確性，未執行建置、check-only、遊戲、伺服器或 DB 操作。
- 未修改歷史連結或 ACL；本任務要求開始前先滿足無 junction 的條件，且原則上不刪除任何項目，因此將具體阻塞交回主控安排。

## 產出路徑／給下一棒的提示

- 提交 `docs/reports/issue-157.md` 與 `work/issue-157/verify.json`，分支 `task/157`。核驗狀態為 `blocked-precondition`，不可標記還原完成。
- 本機掃描結果、可重跑腳本與未執行還原草稿放 `scratch/issue-157/`。
- **調度器／Claude 主控接手**：處理上述 14 個歷史 junction 與 11 處讀取權限，明確安排保留／改接／非遞迴拆連結的範圍。不得沿 junction 清理目標，不得遞迴刪除。
- 前置排除後由 **codex-local** 重跑全專案掃描及 #157。通過後才依 seal 逐檔補回 r28、建立 r33 hardlink，再完整核驗 r28–r33，提交實際還原結果。無需使用者進行遊戲驗收才能解除此阻塞。
