# #1274：分流 #1263（f1222-board-precheck）的失敗項目

結論：#1263 沒有程式缺陷，也沒有測試環境或資料缺陷。全部 ⚠️（P02–P04）都是**同一個流程原因**：前置 PR #1219、#1258 還沒合併。所以**不開 code 任務**。改開一張資源重做任務 `f1263-board-precheck-redo`（取代 #1263），再開一張重測任務 `f1263-board-retest`（取代 #1264）。兩個前置 PR 已標 `needs:orchestrator`，等調度器合併。

## 來源
- #1263 最新 RESULT（2026-11-14 12:06 UTC）：阻塞。前置 PR 沒合併，沒有讀寫 DB，也沒有啟動任何程序。
- 報告 `docs/reports/issue-1263.md`（只在 `task/1263` 分支，commit `d0b19161`）。

## 已通過（✅）的項目：沿用，不重測
| 編號 | 內容 | 處理 |
|---|---|---|
| P01 | #1262 裁定 SM27 甲、SM29 甲 | 沿用。重做任務直接採用這個裁定，不再核對一次 |
| C01 | 收尾時 42061／43324 沒有監聽 | 沿用。重做任務照常做自己的收尾檢查，這屬於收尾，不算重測 |

本輪不開任何 code 任務，所以沒有需要附帶回歸的 ✅ 項目。

## ❌／⚠️ 項目的對應
| 編號 | 結果 | 分類 | 對應 |
|---|---|---|---|
| P02 | ⚠️ PR #1219（#1209 fixtures）OPEN | 流程／調度：只差合併，不是程式或環境問題 | PR 已標 `needs:orchestrator` 並留言；`f1263-board-precheck-redo` 的前置閘門 |
| P03 | ⚠️ PR #1258（#1222 板子方法）OPEN | 同 P02，同一個根因 | 同上 |
| P04 | ⚠️ 同步 main 後，`work/f1186-smoke/fixtures.json`、`docs/reports/issue-1222.md` 都不存在 | P02／P03 的直接結果 | 同上：兩個 PR 合併後自然解除 |
| （未執行） | 冷備份、拍賣期限／最低價／bidder、角色金幣與外觀、站位、fixtures 更新 | 因為阻塞沒有執行，不是失敗 | `f1263-board-precheck-redo`：內容和 #1263 相同，整張重做 |

分流時已核對（唯讀，2026-11-14）：
- PR #1219（`task/1209`）、PR #1258（`task/1222`）都是 `state=OPEN`、`mergeStateStatus=CLEAN`、不是草稿，CI 三項都是 SUCCESS，也沒有審查意見。卡住的只是合併這一步。
- main 上 `work/f1186-smoke/fixtures.json`、`docs/reports/issue-1222.md` 仍然不存在。
- #1209、#1222 都已結案（`task:done`）。#1222 還留著 `needs:user` 標籤：那次要的稽核副作用裁定已經由 #1262 取得（SM27 甲、SM29 甲，已寫進 CLAUDE.md 2026-11-14 例外）。

### 已知限制（不開任務）
- 「PR 合併」是調度器／使用者的職責，不屬於 code、resource、test 任何一種任務，所以不開任務，改在兩個 PR 標 `needs:orchestrator`。
- 時效風險：#1222 查到的盟屋拍賣期限是 2026-11-19 01:00（UTC+8）。如果合併拖過這個時間，SM29 可能沒有可出價的拍賣資料。重做任務已寫明這種情況要查伺服器到期後的行為和其他板子座標，不得自造資料。

## 開出的任務（`docs/plans/issue-1274.json`）
1. `f1263-board-precheck-redo`（stage: resource、runner: codex-local、runner_lock、real_machine、P1、depends_on: []）：內文就是 #1263 原文，前面多加「前置閘門」——先確認兩個 PR 已合進 main；還沒合併就不碰 DB、不啟動程序，標 `needs:orchestrator` 後結束，不列 ❌，也不再分流，以免空轉。
2. `f1263-board-retest`（stage: test、runner: codex-local、runner_lock、real_machine、milestone_smoke、P1、depends_on: [`f1263-board-precheck-redo`]）：內文就是 #1264 原文，前置加上「PR #1219、#1258 與重做任務的 PR 都已合併」。**只重測 #1222 的 R12（SM27 公告板）、R13（SM29 盟屋拍賣）**；#1222 的 ✅（B01、A01、J01、D01、C01）、#1209 的 ✅、#1263 的 ✅ P01／C01 都沿用。

## 其他依賴 #1263 的排隊任務
- #1264（f1222-board-retest，`depends_on: [1262, 1263, 1274]`，queued，從未執行）：要重測的內容和 `f1263-board-retest` 完全相同，已**關閉**並留言註明由 `f1263-board-retest` 取代。
- 其他開啟中的 Issue 內文都沒有引用 #1263。

## 做了什麼／怎麼驗證／未驗證什麼
- 做了：讀 #1263 的 RESULT 與報告、#1264 內文、#1222 留言；用 `gh pr view` 查兩個 PR 的狀態；寫計畫 JSON 與本報告；在兩個 PR 加 `needs:orchestrator` 並留言；關閉 #1264。
- 驗證：計畫 JSON 用 python 載入確認格式正確；兩張任務內文都以 yaml 區塊開頭，`depends_on` 指向計畫內的 id。
- 未驗證：沒有執行任何程式，也沒有碰 DB 或 P24Lab。拍賣資料目前是否還有效，留給重做任務查。

## 產出路徑
- `docs/plans/issue-1274.json`
- `docs/reports/issue-1274.md`

## 給下一棒的提示
- 調度器：先合併 PR #1219、#1258（都已 CLEAN、CI 綠燈），再派 `f1263-board-precheck-redo`，否則它只會再次在閘門停下。拍賣期限 2026-11-19 01:00（UTC+8）前要跑完重做和重測。
- `f1263-board-retest` 另外還要等 #1247（IME 工具）結案。這條前置是從 #1264 原樣保留的，請一起排程。（第 2 次嘗試時查到 #1247 已經 CLOSED，這條前置已經成立。）

## 第 2 次嘗試（2026-11-14）補記
- 前次 log 沒有寫出被判 failed 的原因。唯一的已知瑕疵：關閉 #1264 時用了 `--comment` 直接帶內文，沒有照規定用 `--body-file`。留言已確認中文顯示正常，所以不重發，以免重複留言。本輪的 gh 讀取都先寫進 `scratch/` 再解析。
- 重新核對（唯讀）：PR #1219、#1258 仍然是 OPEN、還沒合併（`mergeStateStatus` 目前顯示 UNKNOWN，GitHub 會在 main 有新 commit 後重算）；#1264 是 CLOSED；#1247 是 CLOSED。
- 分流結論和計畫 JSON 都不變：沒有程式缺陷，也沒有環境缺陷，所以不開 code 任務。
