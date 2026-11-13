# Issue #1141：分流 #1125 實機驗收的失敗項目

**結論：#1125 的 59 個 ⚠️ 全部是同一個環境根因（GUI 擷取／座標輸入工具不可用），沒有任何引擎 ❌，所以不開 code 任務。開出 1 張 resource（`f1125-native-capture`）補齊原生 UI 客戶端用的 GDI 截圖＋SendInput 工具，再開 1 張 test（`f1125-retest`）只重測 SM01–SM59；七個 R 項照 #1125 沿用。**

## 做了什麼

1. 讀 #1125 的 `=== RESULT ===`（2026-11-13 21:54 UTC）與 `docs/reports/issue-1125.md`（`origin/task/1125` commit `5022a95d`，PR #1140）。
2. 對照擷取問題的既有處理鏈：#1118 → #1126（D01：此機 WGC `FrameArrived timed out`，GTX 1080、3840×2160 DPI 192、session 6）→ 分流 #1136 → #1138（記事本前置改 SendInput＋TextPattern，排隊中）→ #1139（以 UIA＋GDI 重測 classic 開發工具與 R07，`depends_on: [1138]`）。
3. 寫 `docs/plans/issue-1141.json`（兩張任務），本報告。

## 已通過（✅），之後不重測

| 編號 | #1125 結果 | 是否回歸 |
|---|---|---|
| R03 開服 42061 | ✅ | 不回歸；重測時開服只記「前置成立」一行 |
| R04 登入至選角 | ✅ | 不回歸；同上 |
| R05 雙角色同時在世界 | ✅ | 不回歸 |
| R06 進世界後同帳號雙開 | ✅ | 不回歸 |
| R12 寵物／召喚條件 | ✅ | 不回歸 |
| R13 血盟／盟倉條件 | ✅ | 不回歸 |
| R17 retest 就緒 | ✅ | 不回歸 |

不需回歸的理由：本分流沒有 code 任務；`f1125-native-capture` 只改 `work/f1118-uia-path/` 測試工具，不改引擎、伺服器、設定或 DB，這七項用到的程式與資料都沒變。R07（#1119）、R01／R02／R16、R08–R11／R14／R15（#1124）也照原結果沿用。

## 失敗項目 → 任務對照

| 編號 | #1125 結果 | 分類 | 對到 |
|---|---|---|---|
| SM01 | ⚠️ 原生 UI 已 active，但 WGC 擷取兩次逾時、座標輸入不可用，無截圖與耗時判定 | (b) 測試環境（擷取工具） | `f1125-native-capture`（工具）→ `f1125-retest`（重測） |
| SM02–SM59（58 列） | ⚠️ 依 SM01 擷取閘門未執行 | (b) 同一根因的連帶未執行，不是 58 個缺陷 | `f1125-retest` |
| SM02／SM15／SM55／SM56／SM59 中 #475 已取得的子步驟 | 沿用 | — | `f1125-retest` 只補未測子步驟 |

分類依據：
- **不是程式缺陷**：`window-events.json` 有 `verifying → active`、63 面板含 `SPELL_WINDOW`，`native-ui.log` 沒有 #1097 的失敗分類訊息；錯誤原文 `FrameArrived timed out: timed out waiting on channel`、`coordinate input geometry is unavailable` 都出自外部工具（`computer-use`／`@oai/sky`），與 #1108／#1118／#1126 同型。
- **為什麼另開 resource，而不是直接沿用 #1138／#1139**：那兩張的對象是記事本與 classic 開發工具視窗（WinForms，UIA 看得到控制項）。M7 smoke 主要測原生 UI，面板畫在遊戲畫布上，UIA 看不到，需要「只擷取工作區的 GDI 截圖」與「依 800×600 邏輯座標換算縮放後的 SendInput 點擊／按鍵」，這兩樣現有工具都沒有。
- **(c) 已知限制**：此機 Windows.Graphics.Capture 不可用（#1126 D01）。屬環境限制、不開任務；之後的真機 GUI 任務一律改走 GDI／SendInput 或客戶端內建 Ctrl+P／F12 截圖，不再試 WGC。

## 開出的任務（`docs/plans/issue-1141.json`）

1. **`f1125-native-capture`**：`stage: resource`、`runner: codex-local`、`runner_lock: true`、`real_machine: true`。在 `UiaGdi.ps1` 新增 `Get-GdiClientCapture`（PrintWindow PW_RENDERFULLCONTENT，失敗才用 BitBlt）、`Send-ClientClick`／`Send-ClientKey`／`Send-ClientText`（邏輯座標→工作區縮放→ClientToScreen→SendInput absolute，送前核對前景 PID），以及 `Test-NativeClient.ps1`：只到登入畫面為止，實證 N01（核對中／接手後兩張截圖＋耗時）、N02（帳號欄點擊輸入）、N03（Ctrl+P 產生檔案）、N04（收尾）。不登入、不寫 DB。
   - **開工前置寫在內文**：#1138 已合併且 #1139 已結案。調度器的 plan 解析（`orchestrator/plans.py`）只會解析 plan 內的 id，無法把既有 Issue 編號寫進 `depends_on`，所以改在內文設閘門，不成立就標 `needs:orchestrator`。`runner_lock`（p24lab）也會讓它與 #1139 排隊執行。
2. **`f1125-retest`**：`stage: test`、`runner: codex-local`、`runner_lock: true`、`real_machine: true`、`milestone_smoke: true`、`depends_on: [f1125-native-capture]`。**只重測 SM01–SM59**（SM02／SM15／SM55／SM56／SM59 只補 #475 未測子步驟）；R03–R06／R12／R13／R17 照 #1125 沿用，開服登入只記前置。內文附與 #1125 相同的主專案寫入範圍例外、SM01 閘門、GM 準備條件（#160／#175）與收尾要求。

## 第 5 點：其他依賴 #1125 的排隊任務

以 `gh issue list --state open` 查了全部 9 張開啟中的 Issue，內文 `depends_on` 含 1125 的：**無**。#1138／#1139 依賴的是 #1136／#1138，與 #1125 無關、測的也不是 SM 項目，所以不關閉也不改。沒有執行任何 `gh issue edit`。

## 怎麼驗證

- `python -c "json.load(...)"` 確認 plan 檔是合法 JSON，兩張任務的 id／stage／depends_on 如上。
- 對照 `D:\dev-orchestrator\orchestrator\plans.py`（唯讀）確認 `depends_on` 只解析 plan 內 id。

## 未驗證／待驗證

- PrintWindow／BitBlt 能否擷取到此機上的遊戲畫布（DPI 192、GTX 1080）**待 `f1125-native-capture` 實機驗證**；若兩者都全黑，該張的停止條件會交回主控，可能要另開 code 任務（例如以客戶端內建 Ctrl+P 作為 smoke 唯一截圖來源）。
- SendInput 點擊的座標換算在 2× 縮放下是否準確，待實機驗證。
- 本張沒有執行任何程式、沒有啟動遊戲或伺服器。

## 產出路徑

- `docs/plans/issue-1141.json`
- `docs/reports/issue-1141.md`
- 分支 `task/1141`

## 給下一棒的提示

1. 調度器：`f1125-native-capture` 必須排在 #1138 合併、#1139 結案之後（內文有閘門）；它碰的是 `work/f1118-uia-path/`，若 #1139 也回寫 README，要先同步 main 再動工。
2. 執行 `f1125-retest` 的人：照 `work/f1118-uia-path/README.md` 的「原生 UI 客戶端」一節操作，不要再用 WGC；SM01 拿不到截圖就停，不要把擷取失敗記成引擎 ❌。
3. 若重測出現引擎 ❌，才由主控開 code 任務；屆時再依改到的程式決定是否回歸 R 項。
4. PR #1140（#1125 報告）是否合併由主控決定；本張只引用其分支內容。
