# Issue #1143：分流 #1138（記事本 UIA 輸入前置）的失敗項目

**結論**：#1138 的 2 個 ❌ 是同一個根因：工具取得前景的方式不夠（程式缺陷，屬於 `work/f1118-uia-path/` 測試工具，不是引擎）。1 個 ⚠️ 是「成功路徑還沒實證」，修好後重測就能驗。開 1 張 code 修正、1 張記事本重測；另外把排隊中的 #1139（客戶端重測）原樣改開成 `f1126-retest-2`，依賴記事本重測，並關閉 #1139。沒有測試環境或資料任務，沒有新的已知限制。

來源：#1138 `=== RESULT ===`（2026-11-13 22:02 UTC 留言）與 `docs/reports/issue-1138.md`（`origin/task/1138` commit `10bad1be`，草稿 PR #1142，未合進 main）；`work/f1118-uia-path/UiaGdi.ps1`、`Test-Notepad.ps1`（同一 commit）。

## 已通過、不重測

| 項目 | #1138 結果 | 處理 |
|---|---|---|
| S02-CLOSE（自啟記事本正常結束，未存檔） | ✅ | **附帶回歸**：修正任務會改 `Test-Notepad.ps1`；而且 #1138 沒有輸入文字，「有修改 → 儲存對話框 → 不要儲存」那條路沒走過，重測有輸入時一定會走到。 |
| P01 靜態部分（語法解析、INPUT 40 bytes、無帳密、失敗回傳 1） | ✅（在 ⚠️ 項目內） | 沿用，不重測。 |
| #1126 D01、C01；#1118 S01；#1108 B01、R05、R08–R15、C01、C02、V01 | ✅（前幾輪） | 沿用，修正任務只改 `work/` 下測試腳本，不碰引擎與 DB。 |

## 失敗項目對應

| #1138 項目 | 結果 | 分類 | 根因／說明 | 對應 |
|---|---|---|---|---|
| S02 輸入／讀回 | ❌ | (a) 程式缺陷 | `Set-UiaForeground` 只叫一次 `SetForegroundWindow`。Windows 前景鎖規則下，背景程序（調度器啟動的 powershell）不能搶前景；當時前景是 Claude 桌面版（HWND 132132、PID 4716）。失敗發生在 `Send-UiaText` 之前，`Readback=null`，SendInput 與 TextPattern 都還沒被測到。 | `f1138-foreground`（code）→ `f1138-retest` |
| S02-GDI | ❌ | (a) 程式缺陷（同根因） | `Save-UiaGdiScreenshot` 用 `CopyFromScreen`，先要求前景，因此一起失敗。修正：PrintWindow（`PW_RENDERFULLCONTENT`）優先，不需要前景；失敗才退回前景＋CopyFromScreen。 | `f1138-foreground` → `f1138-retest` |
| P01 可重用腳本 | ⚠️ | 待驗證（不是缺陷） | 成功回傳 0、輸入讀回、儲存對話框路徑未實證，因為被上面的前景失敗擋住。 | `f1138-retest` |

修正方向（細節在計畫檔）：先 `SetForegroundWindow`，失敗時 `AttachThreadInput` 接上目前前景執行緒再 `BringWindowToTop`＋`SetForegroundWindow`，一定解除；仍失敗就拋出完整診斷（含唯讀的 `SPI_GETFOREGROUNDLOCKTIMEOUT`）。**禁止**改前景鎖設定、改登錄檔、對其他程式送 ALT 或任何按鍵、拿掉 PID／焦點保護。

不開 resource 任務的原因：沒有需要準備的資料或環境；唯一可能的環境因素是「使用者正在別的程式操作」，重測會記錄開始前的前景。若修正後仍取不到前景，重測依停止條件標 `needs:local`，那時才改判為環境問題（需要沒有人使用的桌面）再分流。

## 新任務（`docs/plans/issue-1143.json`）

| id | stage／runner | depends_on | 內容 |
|---|---|---|---|
| `f1138-foreground` | code／claude-local | — | 帶入 `task/1138` 的工具與報告；改前景取得、截圖改 PrintWindow 優先、結果 JSON 加前景診斷；只做靜態檢查，實機效果待驗證。 |
| `f1138-retest` | test／codex-local（runner_lock、real_machine、milestone_smoke） | `f1138-foreground` | **只重測 S02、S02-GDI、P01，附帶回歸 S02-CLOSE**；其他照 #1138 結果沿用。 |
| `f1126-retest-2` | test／codex-local（runner_lock、real_machine、milestone_smoke） | `f1138-retest` | #1139 的內容原樣搬過來，只拿掉 S02（改由 `f1138-retest` 驗）並更新前置條件。 |

## 其他依賴 #1138 的排隊任務

- **#1139**（`depends_on: [1138, 1143]`）：第 1 步就是重跑記事本 S02，跟 `f1138-retest` 重測的東西一樣；它的 `depends_on` 也沒辦法指到調度器之後才建的 Issue 號碼。所以**關閉 #1139**，留言註明被 `f1138-retest`（S02）與 `f1126-retest-2`（其餘客戶端項目）取代，比照 #1136 用 #1139 取代 #1127 的做法。
- 其他開著的 Issue 都沒有依賴 #1138（`gh issue list --state open` 逐一比對內文）。

## 做了什麼／怎麼驗證／未驗證什麼

- 做了：讀 #1138 RESULT、報告與兩個腳本，找出根因；寫計畫檔與本報告；關閉 #1139 並留言。
- 驗證：計畫檔由 `scratch/gen1143.py` 產生，`json.load` 讀得回來，有 3 項；`f1126-retest-2` 的文字有用斷言確認已移除 S02 步驟、更新前置。
- 沒有驗證：沒有執行任何程式或 GUI（本機規定）。根因是從程式碼與 Windows 前景鎖的文件規則推論，加上失敗時前景確實是別的程式；`AttachThreadInput` 在這台電腦上能不能拿到前景，要等 `f1138-retest` 實機確認。

## 產出

- `docs/plans/issue-1143.json`
- `docs/reports/issue-1143.md`（本檔）
- 分支 `task/1143`

## 給下一棒的提示

- `f1138-foreground` 要先 `git checkout origin/task/1138 -- …` 把工具帶進來；草稿 PR #1142 在修正合併後可以關閉。
- `f1138-retest` 若仍取不到前景，**不要**送 ALT 鍵或改系統設定硬試，附診斷後標 `needs:local`。
- `f1126-retest-2` 開跑前要確認 `f1138-retest` 的 S02 是 ✅。
