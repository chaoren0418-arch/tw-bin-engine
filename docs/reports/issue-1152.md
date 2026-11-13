# Issue #1152：分流 #1145（原生 UI 客戶端擷取／輸入工具）的失敗項目

**結論**：#1145 沒有任何引擎或工具的 ❌。它卡在**開工前置**：#1138 的 `UiaGdi.ps1` 沒在 main（PR #1142 未合併；修正已轉到 #1148／PR #1151，也還沒合併），所以 N01–N04 全部 ⚠️ 未執行。根本問題在任務設計：「寫工具」和「實機跑」放在同一張 real_machine 任務，前置一不成立就整張空轉。這次拆成兩張：先由 claude-local 靜態寫好工具（`f1145-native-input`），再由 codex-local 只重測 N01–N04（`f1145-retest`）。排隊中的 #1146 只等 #1145／#1152 關閉，會在工具做好前就開跑，所以關閉 #1146，原樣改開成 `f1125-retest-2`，依賴 `f1145-retest`。不開 resource 任務，也沒有新的已知限制。

來源：#1145 的 `=== RESULT ===`（2026-11-13 22:28 UTC 兩則留言）。#1145 沒有 `docs/reports/issue-1145.md`：`task/1145` 沒有 commit，也沒推送，main 上也沒有。另外參考 #1141 計畫、#1143 報告與計畫、#1148 的 RESULT 與閘門留言、PR #1142／#1151 的狀態（2026-11-14 查詢：兩個 PR 都還是 OPEN）。

## 已通過、不重測

| 項目 | #1145 結果 | 處理 |
|---|---|---|
| 前置：#1139 已結案 | ✅（2026-11-13 22:08 UTC） | 沿用。#1139 已由 #1150 取代，跟本線無關。 |
| #1126 D01（WGC 不可用）、#1118 S01、#1125 R03–R06／R12／R13／R17 | ✅（前幾輪） | 沿用，不回歸。理由：`f1145-native-input` 只改 `work/f1118-uia-path/` 測試工具，不改引擎、伺服器、DB。 |

沒有需要附帶回歸的 ✅：#1145 本身沒有改過任何程式。

## 失敗項目對應

| #1145 項目 | 結果 | 分類 | 根因／說明 | 對應 |
|---|---|---|---|---|
| 前置：#1138 合進 main（`UiaGdi.ps1` 含 `Send-UiaText`／`Get-UiaText`） | ⚠️ | 已由既有任務處理 | #1138 實機 ❌（前景取不到），#1143 分流成 #1148（code，PR #1151）→ #1149（記事本重測）→ #1150。#1148 目前被合併閘門退回：`.gitignore` 的 `work/*/*` 只放行 `.md`／`.json`，`.ps1` 被排除卻加進 git。 | 不另開任務。`f1145-native-input` 的前置寫明：#1151 已合併就從 main 開工；還沒合併就 merge `origin/task/1148` 繼續做（不空轉）。`.gitignore` 照 #1148 的最後解法處理。`f1145-retest` 的前置寫明 #1149 的 S02 必須是 ✅。 |
| N01 工作區 GDI 截圖（核對中／接手後＋耗時） | ⚠️ 未執行 | (a) 工具未實作 | `Get-GdiClientCapture`、`Test-NativeClient.ps1` 都還沒寫（#1145 第 1、2 步沒做） | `f1145-native-input` → `f1145-retest` |
| N02 帳號欄點擊＋輸入 `abc` | ⚠️ 未執行 | (a) 同上 | `Send-ClientClick`／`Send-ClientText` 還沒寫；帳號欄座標也還沒定 | 同上 |
| N03 Ctrl+P 內建截圖 | ⚠️ 未執行 | (a) 同上 | `Send-ClientKey` 還沒寫 | 同上 |
| N04 關閉收尾、回傳碼、無帳密 | ⚠️ 未執行 | (a) 同上 | `Test-NativeClient.ps1` 還沒寫 | 同上 |

N01–N04 的根因相同（工具還沒寫），所以合成一張 code 任務。實機部分只有一張重測。

已知限制：沒有新增。WGC 不可用（#1126 D01：`FrameArrived timed out`）在先前的報告已經列過，這次照舊沿用：不再嘗試，也不開任務。

## 新任務（`docs/plans/issue-1152.json`）

| id | stage／runner | depends_on | 內容 |
|---|---|---|---|
| `f1145-native-input` | code／claude-local | — | 依 #1145 第 1、2、4 步，靜態實作以下項目：`Get-GdiClientCapture`（PrintWindow 優先、BitBlt 備援）、`Send-ClientClick`／`Send-ClientKey`／`Send-ClientText`（800×600 邏輯座標換算、前景 PID 保護、JSONL），以及 `Test-NativeClient.ps1` 和 README 一節。另外要確認兩件事並寫下出處：帳號欄矩形（登入 XML／`LoginPanel.cs` 的 `IdInput`）、`window-events.json` 何時寫入檔案（`Program.cs` 在 `FormClosed` 才存檔，等待 active 的條件可能要改看別的證據）。 |
| `f1145-retest` | test／codex-local（runner_lock、real_machine、milestone_smoke） | `f1145-native-input` | **只重測 N01、N02、N03、N04**，其餘沿用。前置是 #1149 的 S02 為 ✅。只操作到登入畫面為止，不啟動伺服器。 |
| `f1125-retest-2` | test／codex-local（同上） | `f1145-retest` | #1146 原文搬過來（SM01–SM59），只改前置與依賴，並註明取代 #1146。 |

## 其他依賴 #1145 的排隊任務

- **#1146**（`depends_on: [1145, 1152]`，調度器自動加上 1152）：它重測的是 SM01–SM59，跟 N01–N04 不一樣，照規則應該改 `depends_on`。但調度器判斷依賴的方式是「Issue 已關閉」，而計畫檔的 Issue 編號要等本張合併後才會產生，現在沒辦法把 #1146 的 `depends_on` 指過去；#1145 和 #1152 一關閉，#1146 就會開跑，那時工具還沒做好。所以比照 #1143 用 #1150 取代 #1139 的做法：**關閉 #1146**，留言註明被 `f1125-retest-2` 取代。
- 其他開著的 Issue 沒有依賴 #1145（用 `gh issue list --state open` 逐一比對內文，只有 #1146 和本張提到 #1145）。

## 做了什麼／怎麼驗證／未驗證什麼

- 做了：
  - 讀 #1145 的 RESULT 與內文、#1141／#1143 的計畫與報告、#1148 的 RESULT 與閘門結果、PR #1142／#1151 的狀態。
  - 讀調度器的依賴判斷方式（只看 Issue 是否關閉）與計畫建單方式（`depends_on` 只解析計畫 id）。
  - 讀 `LoginPanel.cs`、`MenuButtonsPanel.cs`、`Program.cs`，找出帳號欄焦點、Capture 目錄、`window-events.json` 的寫入時機。
  - 寫計畫檔與本報告；關閉 #1146 並留言。
- 驗證：計畫檔由 `scratch/gen1152.py` 產生，`json.load` 讀得回 3 項。用斷言確認以下幾點：`f1125-retest-2` 沒有殘留 `f1125-native-capture`、保留「SM01–SM59 全部」、有「取代 #1146」。
- 未驗證：沒有執行任何程式或 GUI（依本機規定）。
  - 帳號欄矩形與 `window-events.json` 執行中是否會寫入檔案，只看了原始碼，沒有完整追完，交給 `f1145-native-input` 確認。
  - PrintWindow 能否擷取遊戲畫布，要等 `f1145-retest` 在實機確認。

## 產出

- `docs/plans/issue-1152.json`
- `docs/reports/issue-1152.md`（本檔）
- 分支 `task/1152`

## 給下一棒的提示

- **#1148 是整條線的瓶頸**：#1148 被閘門以 `.gitignore`（`work/*/*` 排除 `.ps1`）退回。調度器或 #1148 修好後要合併 PR #1151，再關閉草稿 PR #1142。`f1145-native-input` 也會新增 `.ps1`，會遇到同樣的閘門，要用同樣的放行規則。
- `f1145-native-input`：
  - 帳號為空時焦點預設就在 `IdInput`（`LoginPanel.cs:359`），所以就算點擊沒點中，輸入也可能成功。N02 要分開判「點擊」和「輸入」。
  - Ctrl+P 存到 `<stage>/runtime/Capture`（`MenuButtonsPanel.CaptureDirectory`）。
- `f1145-retest`：如果出現外部 GDI 全黑但 Ctrl+P 正常，或點擊固定偏移，那是客戶端的問題。照實寫進「推測」並建議開引擎 code 任務，不要換第三種擷取方法。
