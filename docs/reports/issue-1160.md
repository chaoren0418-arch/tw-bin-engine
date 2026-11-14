# Issue #1160：分流 #1155 實機驗收的失敗項目

結論：#1155 只有 **N03 ❌**；N01、N02、N04 ✅。N03 分成 1 張 code 修正任務與 1 張只重測 N03／N04 的重測任務；排隊中的 #1156 依前例關閉，由計畫內的 `f1125-retest-3` 取代。沒有 resource 任務，也沒有新的已知限制。

## 做了什麼

1. 讀 #1155 的 `=== RESULT ===`（調度器留言）與 `docs/reports/issue-1155.md`（`origin/task/1155`，commit `5ac80d0f`）。
2. 靜態讀程式找 N03 的根因（只讀，沒有建置或執行）：
   - `engine/source/NativeUi/MenuButtonsPanel.cs:276` 的 `if(!inWorld)return false;`：原生 UI 的選單熱鍵（含 Ctrl+P）在世界外完全不處理。
   - `engine/source/MenuHotkeyTests.cs:107–109`：單元測試明寫「離開世界：新熱鍵也不收」，代表這是 R4-A（#705）的刻意設計。
   - `engine/source/DevToolsForm.cs:63–67`：classic 路徑一樣要求 `inWorld`。
   - `work/f1118-uia-path/README.md:110`、`:155` 與 `Test-NativeClient.ps1:206`：測試工具已經預測「登入畫面不會產生檔案」，但 `:207` 仍以「有新檔」當作通過條件。所以 N03 一定失敗，`:261` 的「全部成功回 0」也永遠走不到。
   - 原版：`docs/reference/followups-4.md` §1.1 指出 Ctrl+P（WM_CHAR 16）由 TW16 `sub_8C14F0`／`sub_8C2290`（上游 `sub_8CA740`）與 TW13 `sub_4D3910` 處理，但**沒有查過**這條分派在登入畫面會不會被呼叫。
3. 依規則查排隊中的相依任務（`gh issue list --state open`，內文含 1155）：只有 #1156。
4. 用 `scratch/make_plan_1160.py` 產生 `docs/plans/issue-1160.json`（3 項），寫完再用 `json.loads` 讀回確認。

## 已通過的項目（不重測）

| 項目 | #1155 結果 | 處理 |
|---|---|---|
| N01 GDI 工作區截圖（核對中／接手後） | ✅ | 沿用。修正任務不改擷取路徑，也不改畫面輸出。 |
| N02 帳號欄點擊＋輸入 `abc` | ✅ | 沿用。**例外**：如果修正任務採 A（改引擎在登入畫面的按鍵路由），重測時要附帶回歸，因為改到的是 N02 也會經過的按鍵分派。 |
| N04 關閉收尾（`closed`、PID 全收、兩埠無監聽、失敗回非 0） | ✅ | 沿用。但「全部成功回 0」只有讀過程式，沒有實際跑出來（報告第 18 行），所以這一點放進重測。 |

## 失敗項目 → 任務對照

| 項目 | 分類 | 對到 | 理由 |
|---|---|---|---|
| **N03** 登入畫面 Ctrl+P 沒有在 `engine/runtime/Capture` 產生檔案 | (a) 程式缺陷：測試的通過條件和引擎設計互相矛盾 | `f1155-capture-n03`（code，claude-local） | 按鍵確實送到了（SendInput 3/3、LastError=0、前景是客戶端）。引擎刻意只在世界內處理截圖熱鍵，工具卻要求登入畫面也要產檔。修正任務先用 IDA 靜態查原版登入畫面會不會處理 Ctrl+P，再二選一：**A** 原版會截圖 → 改引擎，只讓截圖熱鍵在世界外也生效；**B** 原版不會截圖或查不出來 → 不改引擎，把 N03 改成反向核對（沒有新檔、帳號欄沒有多出字元）。 |
| N04 的「成功回 0」沒有動態驗證 | 附屬於 N03（N03 修好前，這條分支跑不到） | `f1155-retest` | 不另外開任務。 |
| — | (b) 測試環境／資料 | 無 | #1155 前置、資源鎖、建置、check-only 都正常。 |
| — | (c) 已知限制 | 無新增 | WGC 不可用（#1126 D01）是之前就列的限制，沿用。 |

## 計畫（`docs/plans/issue-1160.json`）

| id | stage／runner | depends_on | 內容 |
|---|---|---|---|
| `f1155-capture-n03` | code／claude-local | — | 先查原版（IDA 唯讀，最多 25 分鐘），再依 A 或 B 修正；寫明重現步驟、證據路徑、驗收條件。A 要改 `MenuHotkeyTests.cs` 第 9 段並補 classic 測試；B 只改 `work/f1118-uia-path/`。CI 綠燈才合併。 |
| `f1155-retest` | test／codex-local、runner_lock、real_machine、milestone_smoke | `f1155-capture-n03` | **只重測 N03、N04（動態驗證 exit 0）**；採 A 時附帶回歸 N02。N01，以及採 B 時的 N02，沿用 #1155。不登入、不啟動伺服器。 |
| `f1125-retest-3` | test／codex-local、runner_lock、real_machine、milestone_smoke | `f1155-retest` | 取代 #1156，原文照搬。只改了四處：前置改成 `f1155-retest` 通過；來源補上 #1155／#1160；SM15 的 Ctrl+P 必須在世界內實際產檔並記錄檔名與 SHA-256（世界內 Ctrl+P 目前還沒有任何實機證據，採 B 時這一列就是唯一實證）；沿用 R 項的理由補上 #1160 的修正範圍。 |

## 排隊中相依任務的處理

- **#1156（f1125-retest-2，`depends_on: [1155, 1160]`）**：測的是 SM01–SM59，和 N03 不一樣，照規則應該把依賴改到修正任務。但調度器判斷依賴只看 Issue 有沒有關閉，計畫內任務的 Issue 編號要等本張合併後才會產生，現在指不過去；#1155 已經關了，本張一關，#1156 就會開跑，而它的前置（N01–N04 全 ✅）不成立，等於空轉。所以比照 #1143（#1139）與 #1152（#1146）的做法，**關閉 #1156**，留言註明由 `f1125-retest-3` 取代（內容已全部搬過去）。

## 怎麼驗證

- `python -B -X utf8 scratch/make_plan_1160.py`：輸出 3 項，id／stage／depends_on 跟上表一致；腳本會先 assert #1156 原文的每個替換點都存在，才進行替換。
- 引用的行號都在本分支（= main `6f7051fb`）讀過：`MenuButtonsPanel.cs:270,276`、`MenuHotkeyTests.cs:107–109`、`DevToolsForm.cs:63–67`、`UiClientBridge.cs:267–278`、`README.md:110,155`、`Test-NativeClient.ps1:206–208,261`。

## 第 2 次嘗試（2026-11-14）的複核

- 第 1 次的產出（commit `87411335`）已經推送，但調度器把結果標成 failed，沒有寫原因；log 只有 RESULT 摘要。
- 這次重新核對以下各點，結果都沒變，計畫內容不改：
  - `docs/plans/issue-1160.json` 是 3 項的陣列，欄位和 `issue-1143.json` 一致。
  - #1156 已關閉（NOT_PLANNED）。
  - 開啟中的 Issue 內文都沒有提到 1155，沒有漏改的依賴。
  - `task/1160` 和 `origin/main`（`6f7051fb`）同步，沒有衝突。
- 推測：failed 可能是因為上次的 RESULT 包在程式碼區塊裡，不是以 `=== RESULT ===` 開頭。這次會直接印出。

## 第 3 次嘗試（2026-11-14）的補正

- 第 2 次一樣被標 failed、沒寫原因；`task/1160` 也還沒有 PR（調度器要審查通過後才會開）。
- 比對已合併的同類分流 #1159（PR #1161）：它把來源驗收 #1150 的報告與證據帶進 main。#1155 沒有 PR（`gh pr list --head task/1155` 是空的），報告只在 `origin/task/1155`，後續任務在 main 讀不到。
- 本次補正：
  - 從 `origin/task/1155` 原樣帶入 `docs/reports/issue-1155.md`、`docs/reports/issue-1155-summary.json`，內容不改。
  - `f1155-capture-n03` 的來源說明改成「已帶進 main」。
  - 合併 `origin/main`（`1580924e`），沒有衝突。
- 分流結論、任務切法、#1156 的處理都和前兩次相同。

## 未驗證／推測

- 推測：原版 3.81 在登入畫面也不處理 Ctrl+P（WM_CHAR 分派在遊戲世界的處理函式裡）。**沒有查證**，交給 `f1155-capture-n03` 第 1 步。
- 沒有建置，也沒有執行任何程式；只改了文件與計畫檔。

## 產出路徑

- `docs/plans/issue-1160.json`、`docs/reports/issue-1160.md`
- 帶入的來源報告：`docs/reports/issue-1155.md`、`docs/reports/issue-1155-summary.json`（取自 `origin/task/1155`，內容不改）
- 產生腳本：`scratch/make_plan_1160.py`（不進 git）

## 給下一棒的提示

- `f1155-capture-n03`：關鍵是讓「腳本的通過條件」和「引擎實際行為」一致。如果原版查不出結論，就採 B，不要為了讓測試通過去改引擎行為。採 A 時要注意：焦點在帳號編輯框時，Ctrl+P 不可以把控制字元 16 塞進欄位。
- `f1155-retest` 開工前，先讀修正任務報告寫的是 A 還是 B，再決定要不要回歸 N02。
- `f1125-retest-3` 的 SM15 是世界內 Ctrl+P 截圖的實機驗收點，不能只寫「已按」。
