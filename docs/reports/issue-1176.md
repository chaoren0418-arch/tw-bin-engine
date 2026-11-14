# Issue #1176：#1173 實機驗收失敗項目分流

**結論：** #1173 的兩個 ❌ 分流完成：G06 → 1 張程式修正任務；N01 → 已證實是**測試條件錯誤**（服務分頁在選角畫面被刻意停用），不算程式缺陷，改由重測任務在世界中重驗（helper 的診斷缺口併入同一張修正任務）。最後加一張重測任務，只重測 G06、N01，附帶回歸 G05、G07。沒有其他排隊任務依賴 #1173。

本張只改文件與規劃檔，沒有改程式、沒有執行任何程式或 UIA 腳本。

## 已讀來源

- #1173 Issue 內文與最新 `=== RESULT ===`（部分通過：✅ 3／❌ 2／⚠️ 0）。
- `task/1173` 分支 `cf5470ff`：`docs/reports/issue-1173.md`、`docs/reports/issue-1173-evidence.json`（內含 `g06-dev-tree.json`、`n01-nested-tree.json` 等原始輸出）。
- 程式（靜態閱讀）：`engine/source/ProductEntry.cs:59–75`、`engine/source/DevToolsForm.cs:1–116,191–209`、`engine/web/index.html:16`、`engine/web/panel.js:94–114`、`work/f1118-uia-path/UiaGdi.ps1:102–150,182–204`。

## 已通過（✅），之後不重測

| 編號 | #1173 結果 | 之後處理 |
|---|---|---|
| G01（前置） | ✅ 前置成功 | 重測時照樣當前置執行並記錄 |
| G05 | ✅ 通過 | **附帶回歸**：修正任務改了網頁面板 `panel.js`，G05 截的就是這一頁 |
| G07 | ✅ 通過 | **附帶回歸**：修正任務改了 `UiaGdi.ps1` 的 `Read-UiaElement`（`Find-UiaControl` 會用到）與 `Select-UiaTab` |
| P01 | ✅ 通過 | 沿用；只有 G07 回歸失敗時才改列 ❌ |
| #1173「沿用」表中的 24 項（#1165 G04、F01；#1108 R07、B01、R05、R08–R15、C01、C02、V01；#1150 G02、G03；#1149 S02 三項；#1118 S01；#1126 D01、C01） | ✅ 沿用 | 繼續沿用，不重測 |

## 失敗項目分流

| 編號 | #1173 結果 | 分類 | 對應 |
|---|---|---|---|
| G06 | ❌ 網頁面板 183 個節點中有 1 個 `UIA property 'Name' is null` | (a) 程式缺陷（根因推測）＋ helper 診斷缺口（已驗證） | 修正任務 `f1173-g06-web-tree`；重測 `f1173-retest` |
| N01 | ❌ 聲音／血盟的 `Select-UiaTab` 讀回 IsSelected=false、仍是背包 | (b) 測試條件錯誤（已驗證）：不需要準備資料，**不開 resource 任務**；改重測步驟。helper 失敗訊息看不出原因，這個診斷缺口併入 `f1173-g06-web-tree` | 重測 `f1173-retest`（改在世界中測）；helper 診斷併入 `f1173-g06-web-tree` |

沒有 (c) 類的已知限制。

### G06 分析

- **已驗證**：在 `g06-dev-tree.json` 中，錯誤節點（零起算第 54 個）前一個是 Image／`sceneImage`，後一個是 StatusBar／`incomingAttack`；整棵樹**沒有** `sceneStatus` 節點。`engine/web/index.html:16` 的順序是 `sceneImage` → `<p id="sceneStatus">` → `incomingAttack`，所以壞節點的位置正好是 `sceneStatus`。
- **推測（根因）**：`engine/web/panel.js:97` 每收到一次 scene 訊息，只要還沒進入世界，就**無條件**重寫 `sceneStatus.textContent`（選角畫面時一直是這樣）；`:107`、`:112`、`:113` 也一樣。每次重寫都會換掉文字節點，Chromium 的無障礙節點跟著重建，UIA 讀 `Current.Name` 時元素可能已經失效。#1165 G06（登入頁）、#1173 G07／N01（服務頁）的樹都沒有 Error，跟「只有一直重繪的網頁面板出錯」相符。
- **已驗證（helper 缺口）**：`UiaGdi.ps1:102–117` `Read-UiaElement` 只要有一個屬性是 null，整個節點就只剩 `Error`，所以 #1173 無法直接指出是哪個控制項。
- 修法（交 `f1173-g06-web-tree`）：`panel.js` 改成「值不同才寫」；helper 在節點出錯時保留能讀到的身分欄位；停止條件是：如果證據指向 WebView2 本身，就只做 helper，並標 `needs:claude`。

### N01 分析

- **已驗證**：`n01-nested-tree.json` 中「開發工具服務分頁」Tab 和 16 個 TabItem 全部 `Enabled=false`。來源是 `engine/source/ProductEntry.cs:73` 的 `normalPanels.Enabled=login.State==LoginState.InWorld`。#1173 在 CharacterList 狀態測，停用的 TabControl 本來就不接受選取。這是設計行為，不是 #1172 的修正失效；#1172 修的名稱／矩形在 #1173 已經驗證通過。
- 外層 Tab（G07）是 `DevToolsForm.pages`，沒有被停用，所以同一個 `Select-UiaTab` 在外層能切換。這也說明選取 API 本身沒問題。
- 為什麼不開 resource 任務：重測只要用 p24svc01 **既有**角色進入世界，不需要建角、補物品，也不用改 DB（進入世界造成的自動寫入只落在隔離副本）。這一步直接寫進重測任務，另開 resource 任務只會讓任務空轉。
- 為什麼不單獨開程式任務：停用是刻意設計，不改。只把「`Select-UiaTab` 先檢查 IsEnabled，丟出明確錯誤」與 README 前提說明併入 `f1173-g06-web-tree`，因為改的是同一個 `UiaGdi.ps1`，拆成兩張 PR 會互相衝突。

## 拆出的任務（`docs/plans/issue-1176.json`）

| id | stage／runner | 內容 | depends_on |
|---|---|---|---|
| `f1173-g06-web-tree` | code／claude-local | `panel.js` 文字值不同才寫；`Read-UiaElement` 保留壞節點身分；`Select-UiaTab` 先檢查 IsEnabled；契約 fixture；README 加 N01 前提 | — |
| `f1173-retest` | test／codex-local、runner_lock、real_machine、milestone_smoke | **只重測 G06（連讀 3 次都要 0 個 Error）、N01（先在 CharacterList 記錄停用狀態作對照，再進入世界切聲音／血盟）**；附帶回歸 G05、G07；G01 只當前置；P01 與其他項目沿用 | `f1173-g06-web-tree` |

## 第 5 點：依賴 #1173 的排隊任務

用 `gh issue list --state open` 查內文含 `1173` 的 Issue，以及 `depends_on` 含 1173 的 Issue：只找到本張 #1176，**沒有需要關閉或改依賴的任務**。

## 怎麼驗證

- `docs/plans/issue-1176.json` 由 `scratch/t1176/build_plan.py` 產生，並用 `json.loads` 讀回確認 JSON 格式正確（2 項，`depends_on` 指向計畫內的 id）。
- 根因核對全部是靜態的：讀 #1173 證據 JSON 的樹節點（`Enabled` 欄位、節點順序）與原始碼行號。

## 未驗證

- G06 的根因（`panel.js` 重寫造成元素失效）只是推測，要等修正後的重測確認。
- 進入世界後，巢狀分頁實際可見的分頁數量與能否切換，要等 `f1173-retest` 驗證。

## 給下一棒的提示

- `f1173-g06-web-tree`：`engine/_integration/delta/web/` 是封存副本，不要改。不要把「Name=null」判定放寬當成修好；G06 的判定標準不變。
- `f1173-retest`：N01 第 1 步在 CharacterList 讀到 Enabled=false 是預期的對照，不算失敗。進入世界只用既有角色，不移動、不按服務頁內任何按鈕。
