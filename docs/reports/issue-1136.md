# Issue #1136：#1126 實機驗收失敗項目分流

**結論：#1126 共 13 項，✅ 3（S02-GDI、D01、C01）、❌ 1（S02 記事本輸入前置）、⚠️ 9（G01–G07、F01、P01）。9 個 ⚠️ 中，G01–G07、F01 是被 S02 停止條件擋下沒有執行，P01 是腳本還沒在客戶端實證，所以根因只有一個：測試契約要求對 Windows 10 記事本編輯區用 `ValuePattern.SetValue`，但這個控制項本來就不支援（`Unsupported Pattern.`）。這是測試設計／環境的問題，不是引擎缺陷，所以不開 code 任務。開 1 張 resource 任務把前置改成等價的「聚焦＋SendInput 輸入＋TextPattern 讀回」並實機驗證，再開 1 張只重測失敗項目的 test 任務，取代 #1127。**

依據：#1126 Issue 內文與 `=== RESULT ===` 留言（2026-11-13 21:18 UTC）；`task/1126` 分支的 `docs/reports/issue-1126.md`、`work/f1118-uia-path/README.md`、`UiaGdi.ps1` 的函式清單；分流前例 #1121（`docs/reports/issue-1121.md`、`docs/plans/issue-1121.json`）；排隊中的 #1127 內文。本張沒有改程式，也沒有執行任何程式。

## 1. 已通過、不重測的項目（✅ 3）

| 編號 | 項目 | 之後要不要重測 |
|---|---|---|
| S02-GDI | 記事本 GDI 截圖（1812×1600、非全黑／全白、人工檢視正常） | **附帶回歸一次**：`f1126-notepad-input` 會改 `work/f1118-uia-path/UiaGdi.ps1`（截圖函式所在檔案），重測跑 S02 時順便截 1 張圖，不另外算一項 |
| D01 | 唯讀診斷（WGC 仍 `FrameArrived timed out`；GTX 1080、驅動 32.0.15.6094；單螢幕 3840×2160、DPI 192；session 6 console／Active） | 不重測、不再試 WGC |
| C01 | 收尾（記事本 PID 4468 已關、42061／43324 無監聽） | 不重測；重測任務照常收尾，不算重測 |

另外沿用：#1118 的 S01（重測開始時只記一行 `query session`）；#1108 的 B01、R05、R08–R15、C01、C02、V01（13 項 ✅）。理由：本張沒有開 code 任務，新的 resource 任務只改 `work/` 下的測試腳本，不改引擎、不寫 DB。

## 2. 失敗項目的歸類

| 編號 | #1126 結果 | 觀察 | 歸類 | 對應 |
|---|---|---|---|---|
| S02 記事本 UIA 輸入前置 | ❌ | 編輯區是 `ControlType.Document`／Class `Edit`／AutomationId `15`，只有 `TextPattern`、`ScrollPattern`；`GetCurrentPattern(ValuePattern.Pattern)` → `Unsupported Pattern.` | **(b) 測試環境／契約**：Windows 10 記事本控制項的固有行為；換成等價的輸入／讀回方法即可 | `f1126-notepad-input` → `f1126-retest` |
| G01 登入到 CharacterList | ⚠️ 未執行 | 被 S02 停止條件擋下 | (b)，同上根因 | `f1126-retest` |
| G02 主視窗截圖 | ⚠️ 未執行 | 同上 | (b) | `f1126-retest` |
| G03 主視窗 UIA 樹 | ⚠️ 未執行 | 同上 | (b) | `f1126-retest` |
| G04 主視窗操作（F12） | ⚠️ 未執行 | 同上 | (b) | `f1126-retest` |
| G05 開發工具截圖 | ⚠️ 未執行 | 同上 | (b) | `f1126-retest` |
| G06 開發工具 UIA 樹 | ⚠️ 未執行 | 同上 | (b) | `f1126-retest` |
| G07 開發工具切分頁 | ⚠️ 未執行 | 同上 | (b) | `f1126-retest` |
| F01 建角對話框替代路徑 | ⚠️ 未執行 | 同上 | (b) | `f1126-retest` |
| P01 重用腳本 | ⚠️ | 找窗／樹／GDI 已實證；客戶端 pattern 包裝只做語法檢查；腳本**只在 `task/1126` 分支，沒有合進 main** | (b) | `f1126-notepad-input`（補輸入／讀回函式、把腳本帶進 main）＋`f1126-retest`（客戶端實證） |
| （#1108 R07 七職業 GUI 建角） | 仍 ⚠️ | 原本由 #1127 重測；#1127 依賴失敗的 #1126 | (b) | 併入 `f1126-retest` |

為什麼不開 code 任務：失敗點在記事本（作業系統程式），還沒碰到客戶端；沒有任何證據指向引擎。`work/f1118-uia-path/` 是測試工具，要在真機上改完立刻驗證，`claude-local` 不准執行程式，所以放在 `codex-local` 的 resource 任務。

為什麼新方法不是「同一招再試一次」：#1126 的錯誤是控制項不提供這個 pattern，重跑一定同樣失敗。前置的目的只是證明「UIA 能找到控制項、送進輸入、讀得回來」，`SetFocus`＋`SendInput`（`KEYEVENTF_UNICODE`）＋`TextPattern.DocumentRange.GetText` 達到同樣目的，且記事本編輯區確定有 `TextPattern`（#1126 樹已列出）。

## 3. 已知限制（(c)，不開任務）

- **`@oai/sky` 的 WGC 擷取在本機不可用**，根因未確定（沿用 #1121）。執行器工具在專案外，修它要動驅動或系統設定。GDI 已證實可用，之後一律走 GDI＋F12。
- **Windows 10 記事本編輯區不支援 `ValuePattern`**：作業系統行為，用 TextPattern 讀回代替，不算缺陷。
- 沿用 #1116／#1121：伺服器禁用名稱字「測試」（S98=9）；p24svc01 只剩 1 個空位，所以 GUI 真正送出只做一次，其餘六職業只驗到「可以建立」狀態。

## 4. 拆出的任務（`docs/plans/issue-1136.json`）

| id | stage／runner | 內容 | depends_on |
|---|---|---|---|
| `f1126-notepad-input` | resource／codex-local（runner_lock、real_machine） | 把 `task/1126` 的 `work/f1118-uia-path/` 與 #1126 報告帶進 main；`UiaGdi.ps1` 新增 `Send-UiaText`（聚焦＋SendInput Unicode）與 `Get-UiaText`（ValuePattern → TextPattern 備援）；改 `Test-Notepad.ps1` 與 README；記事本實機跑一次，輸入讀回逐字相符、GDI 圖看得到文字、不存檔關閉。只碰記事本，不啟動客戶端／伺服器／DB | — |
| `f1126-retest` | test／codex-local（runner_lock、real_machine、milestone_smoke） | **只重測 S02（附帶回歸 S02-GDI）、G01–G07、F01、P01 與 #1108 R07**；D01、C01、S01 與 #1108 的 13 個 ✅ 沿用 | f1126-notepad-input |

## 5. 其他依賴 #1126 的任務

- **#1127（f1118-r07-retest）**：`depends_on: [1126, 1136]`，要重測的 S02、G01–G07、F01、R07 與 `f1126-retest` 完全重疊。已關閉並留言註明「由 #1136 拆出的 `f1126-retest` 取代」，內容（含七職業、一次真正送出、刪除自建角色、收尾條件）已全部併入 `f1126-retest`。
- 用 `gh issue list --state open --search 1126` 查過，沒有其他開啟中的 Issue 依賴 #1126。

## 做了什麼／怎麼驗證／未驗證什麼

- 做了：讀 #1126 Issue 內文、留言、RESULT；讀 `task/1126` 的報告、README、腳本函式清單；讀 #1121 分流報告與計畫、#1127 內文；寫分流計畫與本報告；關閉 #1127。
- 驗證：計畫 JSON 用 python `json.load` 確認格式正確；`git ls-tree origin/main` 確認 `work/f1118-uia-path/` 尚未在 main（所以 resource 任務要負責帶進來）；`gh pr list --head task/1126` 確認 #1126 沒有開 PR。
- 未驗證（推測）：`SendInput`＋`TextPattern` 在本機記事本可行——依據是 #1126 樹顯示編輯區有 TextPattern、DPI-aware 行程與前景核對已實證，但尚未實機執行，交給 `f1126-notepad-input`。客戶端 WinForms `TextBox`／`NumericUpDown` 是否提供 ValuePattern 也待重測確認。

## 產出路徑

- `docs/plans/issue-1136.json`（2 張任務）
- `docs/reports/issue-1136.md`（本檔）

## 給下一棒的提示

1. `f1126-notepad-input` 先 `git checkout origin/task/1126 -- work/f1118-uia-path docs/reports/issue-1126.md docs/reports/issue-1126-evidence.json`，否則 main 上沒有這套腳本，重測會找不到。調度器清 task-1126 worktree 前若要保留 `scratch/issue-1126/` 的 PNG，要先另存；不可遞迴刪除 worktree（有 junction）。
2. 記事本關閉時可能跳「是否儲存」：用 UIA 找「不要儲存」Invoke，絕對不要存檔。
3. 編輯區要用 ControlType `Document`／AutomationId `15` 找；狀態列有五個 `ControlType.Edit`，會誤取（#1126 已踩過）。
4. 開發工具與建角對話框都有名為「建立角色」的按鈕；開對話框時限定開發工具根視窗，對話框內只按「取消」，只有重測第 5 步那一次才按送出。
5. #1109／#1120（M7 smoke）如果也需要截圖，之後應依賴 `f1126-notepad-input` 合進 main 的腳本，不要再用 WGC。
