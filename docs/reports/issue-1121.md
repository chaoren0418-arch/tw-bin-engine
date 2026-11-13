# Issue #1121：#1118 實機驗收失敗項目分流

**結論：#1118 共 11 項，✅ 2、❌ 1（S02）、⚠️ 8（G01–G07、F01）。8 個 ⚠️ 都是因為 S02 觸發停止條件而沒執行，根因只有一個：執行器工具 `@oai/sky` 的 Windows.Graphics.Capture（WGC）擷取在這台機器不可用（連記事本都逾時），跟引擎無關。所以不開 code 任務。開 1 張 resource 任務改走不依賴 WGC 的路徑（UIA 操作＋GDI 截圖＋客戶端內建 F12 證據），再開 1 張只重測失敗項目的 test 任務。**

依據：`task/1118` 分支的 `docs/reports/issue-1118.md`、`docs/reports/issue-1118-evidence.json`，Issue #1118 的 `=== RESULT ===` 留言；分流前例 #1116（`docs/reports/issue-1116.md`、`docs/plans/issue-1116.json`）；#1108 worktree 的 `scratch/issue-1108/gui-selection.txt`（只讀）；`engine/source/Program.cs` `CaptureEvidence`、`engine/source/CharacterManagementUi.cs`（只讀）。本張沒有改程式，也沒有執行任何程式。

## 1. 已通過、不重測的項目（✅ 2）

| 編號 | 項目 | 之後要不要重測 |
|---|---|---|
| S01 | 互動桌面（console session 6／Active，未鎖定） | 不重測；重測任務開始時只重新記錄一行 `query session` 當前置，不算重測 |
| C01 | 收尾（記事本已關、42061／43324 無監聽） | 不重測；重測任務照常收尾，不算重測 |

**不需要回歸任何 ✅ 項目**：沒有開 code 任務，新的 resource 任務不改程式、不寫 DB。#1108 的 13 個 ✅（B01、R05、R08–R15、C01、C02、V01）也照 #1116 的判定繼續沿用。

## 2. 失敗項目的歸類

| 編號 | #1118 結果 | 觀察 | 歸類 | 對應 |
|---|---|---|---|---|
| S02 系統視窗擷取與 Recovery | ❌ | 記事本（可見、未最小化、前景、DPI 192）用 `sky.get_window_state` 兩次都 `FrameArrived timed out: timed out waiting on channel`，`js_reset` 後仍失敗 | **(b) 測試環境／工具**：執行器工具的 WGC 後端不可用，與被擷取的程式無關 | `f1118-uia-path`（resource）→ `f1118-r07-retest`（test） |
| G01 登入到 CharacterList | ⚠️ 未執行 | 被 S02 停止條件擋下 | (b)，同上根因 | 同上 |
| G02 主視窗截圖 | ⚠️ 未執行 | 同上 | (b) | 同上（改 GDI 截圖＋F12 內建證據） |
| G03 主視窗 include_text | ⚠️ 未執行 | 同上 | (b) | 同上（改 UIA 讀取） |
| G04 主視窗座標點擊 | ⚠️ 未執行 | 同上 | (b) | 同上（改 F12 鍵或 UIA 矩形＋SendInput） |
| G05 開發工具視窗截圖 | ⚠️ 未執行 | 同上 | (b) | 同上（改 GDI 截圖） |
| G06 開發工具 include_text | ⚠️ 未執行 | 同上 | (b) | 同上（改 UIA 讀取） |
| G07 開發工具座標點擊 | ⚠️ 未執行 | 同上 | (b) | 同上（改 UIA `SelectionItemPattern` 切分頁） |
| F01 無座標替代路徑 | ⚠️ 未執行 | 同上 | (b) | 同上（UIA `InvokePattern`／`ValuePattern`／`ExpandCollapsePattern`） |
| （#1108 R07 七職業 GUI 建角） | 仍 ⚠️ | #1118 是 R07 的前置，前置沒過所以 R07 沒有進展 | (b) | `f1118-r07-retest` |

為什麼這次不再「同一個工具修一次」，而是換路徑：
- #1118 已經排除了桌面鎖定、非互動 session、視窗最小化／背景這些假設（S01 ✅、記事本是前景可見視窗）。剩下的可能（顯示驅動、200% DPI、WGC helper、桌面合成器）都要動系統設定或驅動才能修，執行器不准做，本機對話修了也不保證。再派同一方法的任務只會空轉。
- 不用 WGC 的路徑已有兩個實證支持：(1) #1108 時 UIA 樹完整讀得到開發工具視窗的「建立角色」「登入／選角」等控制項（`gui-selection.txt`），表示 UIA 這條路本身是通的；(2) 客戶端 `CaptureEvidence`（F12）用 `DrawToBitmap` 和 GDI `CopyFromScreen` 截圖，#1108 已產出 `manual-screen.png`／`manual-client.png`，表示 GDI 截圖在這台機器可用。
- 建角對話框（`CharacterCreationDialog`）的職業與性別是 `ComboBox`（DropDownList）、能力值是 6 個 `NumericUpDown`，都是標準 WinForms 控制項，UIA 有對應 pattern，不需要座標。

已知限制（(c)，不開任務）：
- **`@oai/sky` 的 WGC 擷取在本機不可用，根因未確定**。這是執行器工具（專案外）的問題，修它要動驅動或系統設定，不在本專案範圍；`f1118-uia-path` 只做唯讀診斷記錄（顯示卡驅動、螢幕數與 DPI、再試一次），不修。之後若本機對話想修，看那張報告的診斷段。
- 沿用 #1116 的兩條：伺服器禁用名稱字「測試」（S98=9）；p24svc01 只剩 1 個空位，所以 GUI 真正送出只做一次、其餘六職業只驗到「可以建立」狀態。

## 3. 拆出的任務（`docs/plans/issue-1121.json`）

| id | stage／runner | 內容 | depends_on |
|---|---|---|---|
| `f1118-uia-path` | resource／codex-local（runner_lock、real_machine） | 先在記事本驗證 UIA 操作＋GDI 截圖；唯讀記錄 WGC 失敗的診斷資料；照 #1108 參數登入到選角，對主視窗與開發工具視窗各做 GDI 截圖、UIA 讀取、一個不改資料的操作；用 UIA 開建角對話框、切 2 個職業、取消。把方法寫成腳本 commit 到 `work/f1118-uia-path/`。不寫 DB、不送出、不改系統設定。兩條路都不通 → `needs:local` | — |
| `f1118-r07-retest` | test／codex-local（runner_lock、real_machine、milestone_smoke） | **只重測 #1118 S02、G01–G07、F01 與 #1108 R07**；S01、C01 與 #1108 的 13 個 ✅ 沿用 | f1118-uia-path |

## 做了什麼／怎麼驗證／未驗證什麼

- 做了：讀 #1118 Issue 內文、留言、RESULT、報告與證據 JSON；讀 #1116 的分流報告與計畫；讀 #1108 的 UIA 樹證據；對照 `Program.cs` `CaptureEvidence`（F12 → `manual-logical/client/screen.png`，screen 用 GDI `CopyFromScreen`）與 `CharacterManagementUi.cs` 的控制項型別；寫分流計畫與本報告。
- 驗證：計畫 JSON 用 python `json.load` 確認格式正確（見 commit 前檢查）。
- 未驗證（推測）：UIA＋GDI 路徑在本機能否完成建角對話框操作——依據是 #1108 的 UIA 樹與內建截圖都成功，但尚未實機操作，交給 `f1118-uia-path`。WGC 失敗的確切根因未知。

## 產出路徑

- `docs/plans/issue-1121.json`（2 張任務）
- `docs/reports/issue-1121.md`（本檔）

## 給下一棒的提示

1. **#1119（f1108-r07-retest）已被本計畫的 `f1118-r07-retest` 取代**：#1119 依賴已失敗結案的 #1118，內容又要求「照 f1108-gui-capture 的方法操作」，那個方法不存在。建議調度器把 #1119 以「由 #1121 拆出的重測取代」結案，不要派工，否則會空轉（`needs:orchestrator`）。
2. **#1109／#1120（M7 smoke）也受同一個 WGC 問題影響**：每列都要截圖。`f1118-uia-path` 的 `work/f1118-uia-path/` 腳本可以直接給 #1120 拆出的重測用；建議那邊的重測也 `depends_on` f1118-uia-path。
3. 截圖腳本一定要讓行程 DPI-aware（`SetProcessDpiAwarenessContext(-4)`），否則 200% DPI 下 UIA 矩形和 GDI 座標會差一倍，截到錯的地方。
4. 客戶端 F12 只截主視窗（`Width`×`Height`，從 `Left`,`Top`），**截不到獨立的建角對話框**；對話框要用 GDI 依對話框自己的 UIA 矩形截。
5. 調度器清 task-1108 worktree 前要先保留 `scratch/issue-1108/backup-before`，而且不可以遞迴刪除（裡面有 junction）——沿用 #1116 的提醒。
