# Issue #1094：分流 #475 實機驗收的失敗項目 → 修正任務＋重測

**結論：#475 的 59 列中沒有任何 ✅；❌ 1（SM01）、⚠️ 58。** 根因只有兩個程式缺陷加一個環境問題：SPELL_WINDOW 幾何例外讓整個原生 UI 退回 classic（已有 #1093），紅字把版面例外誤報成 pin 不符（新開 code 任務），P24Lab 服務沒有啟動（新開 resource 任務）。58 個 ⚠️ 都是被這三件事擋住，不是各自有缺陷。最後開一張重測任務，開頭設 SM01 閘門，避免再出現 58 列空轉。

## 做了什麼

1. 讀 #475 報告 `docs/reports/issue-475.md`（`origin/task/475`）、Issue #475 本文、已有的修正任務 #1093。
2. 讀原始碼確認誤報訊息的根因：`engine/source/NativeUi/UiLayout.cs:7` 的 `UiLayoutException` 繼承 `UiResourceException`（`ClientArchive.cs:20`），而 `UiClientBridge.cs` 的 `ApplyNativeUiStart` 遇到 `UiResourceException` 一律顯示 `UiRoot.PinMismatchMessage`（`UiRoot.cs:17`）。`SpellWindowPanel.cs:123` 拋出的正是 `UiLayoutException`。
3. 核對 P24Lab 路徑：`D:\P24Lab-ClientEngine` 不存在（`Test-Path` = False）；主專案 `environments\p24` 有 `server`、`mysql`、`db-data`、`backups` 等目錄。已改 `docs/testing/smoke-m7.md` §0.1 的路徑，並加一條「開跑前先確認 42061／43324 有在監聽」。
4. 寫 `docs/plans/issue-1094.json`（3 張任務）與本報告。沒有改程式，也沒有執行任何程式（只用 `python -c` 檢查 JSON 格式）。

## 已通過（✅）的項目

**沒有。** #475 表中 59 列都不是完整通過。下列幾列已有部分證據，重測時沿用、只補還沒測的子步驟：

| 項目 | 沿用的證據 |
|---|---|
| SM02 | 參考 WidgetSlot／WidgetSubSlot 與 option-png 已讀到 |
| SM15 | 日誌確認 WidgetSlot 參考 XML 已載入 |
| SM55 | `--classic-ui` 與設定檔 nativeUi=false 兩種關閉方式都生效；移除設定後恢復原生驗證 |
| SM56 | 五次啟動都留下 closed 事件 |
| SM59 | classic 啟動時自動開登入／選角頁，帳號框有焦點 |

## 每個失敗項目對到的任務或已知限制

| 項目 | #475 結果 | 分類 | 對應 |
|---|---|---|---|
| SM01 | ❌ native-ui verifying → failed | (a) 程式缺陷：SPELL_WINDOW 幾何＋紅字誤報 | **#1093**（幾何）＋ **f475-ui-fail-message**（訊息分類）→ f475-retest |
| SM20 | ⚠️ 真實素材重現魔法窗例外 | (a) 同 SPELL_WINDOW 根因 | **#1093** → f475-retest（必測，看清單不覆蓋等級鈕） |
| SM02、SM03、SM05、SM10–SM19、SM21、SM24–SM38、SM57 | ⚠️ 原生根未啟用，視窗無法驗 | (a) 被 #1093 擋住（原生 UI 全有或全無） | **#1093** → f475-retest |
| SM04、SM06–SM09 | ⚠️ 未登入、未取得角色清單／S48 | (a) 原生登入畫面被 #1093 擋住＋(b) 42061／43324 沒在監聽 | **#1093**＋**f475-p24lab-ready** → f475-retest |
| SM22、SM23、SM35 | ⚠️ 沒有雙角色 session | (b) 測試資料：兩個可同時登入的測試角色 | **f475-p24lab-ready** → f475-retest |
| SM19、SM21、SM26、SM29、SM40、SM46、SM48、SM49、SM25、SM36、SM24、SM37 | ⚠️ 沒有物品／角色／血盟／寵物條件 | (b) 測試資料（弓箭、肉、戒指、料理書、七職業、血盟、寵物、金幣） | **f475-p24lab-ready** → f475-retest |
| SM39、SM41–SM45、SM47、SM50–SM53、SM58 | ⚠️ 沒進世界 | (a)＋(b)：原生 UI 被擋，服務也沒啟動 | **#1093**＋**f475-p24lab-ready** → f475-retest |
| SM54、SM55、SM56、SM59 | ⚠️ classic 部分已驗，世界內未測 | (b) 服務沒啟動（classic 不受 #1093 影響） | **f475-p24lab-ready** → f475-retest（只補子步驟） |
| （程序面）#475 執行時 lease holder 顯示 `lito-launcher-server#284` | 只記錄差異 | 調度器的事，不屬本專案 smoke | 不開任務；交雲端調度器確認（`needs:orchestrator` 由調度器決定是否開） |

### 已知限制（不開任務）

1. **原生 UI 是「全有或全無」**：`UiRoot.Build`（`UiRoot.cs:223` 的註解）只要一個面板 Load 失敗，整個原生 UI 就退回 classic。這是 `docs/design/native-ui.md` 定好的設計，不算缺陷；它讓一個魔法窗缺陷擋掉 58 列。要不要改成單一面板失敗時只停用該面板，交主控裁定，本次不開任務。
2. **SM06 登入公告 S48**：要看 P24Lab 伺服器有沒有送 S48。重測時如果伺服器確實不送，就列為伺服器不支援的已知限制，不算客戶端缺陷。

## 拆出的任務（`docs/plans/issue-1094.json`）

| id | stage／runner | 內容 | depends_on |
|---|---|---|---|
| f475-ui-fail-message | code／claude-local | 版面例外和 pin 不符分開顯示；failed 事件加 category；附離線 `[CheckGroup]` 測試 | — |
| f475-p24lab-ready | resource／codex-local，runner_lock、real_machine | 啟動 `environments\p24` 的 43324／42061；備份後準備雙角色、七職業與 SM 用物品；附還原 SQL | — |
| f475-retest | test／codex-local，runner_lock、real_machine、milestone_smoke | 重測 SM01–SM59（沒有 ✅ 可沿用；上表 5 列只補子步驟）；**SM01 閘門**：沒到 active 就立刻停 | f475-ui-fail-message、f475-p24lab-ready，**以及 #1093** |

#1093 已經存在（code，但標籤是 `runner:codex-local`），所以沒有重開一張，避免重複。

## 怎麼驗證

- JSON 用 `python -B -X utf8 -c "json.load(...)"` 讀回，3 張任務的 id／stage／depends_on 都正確。
- 根因是讀原始碼確認的（行號見上），沒有執行程式。
- 文件只改 `docs/testing/smoke-m7.md` §0.1；沒改 `docs/REPORT.md`、lock、程式碼。

## 未驗證／待驗證

- 本計畫的 JSON 格式沒有能引用既有 Issue 的欄位。我在 f475-retest 加了 `depends_on_issues: [1093]`，本文的 yaml 也寫了 `depends_on: [1093]`；調度器不一定會讀這兩個地方（推測）。
- `environments\p24` 怎麼啟動（用哪個腳本）沒有實際驗證，交 f475-p24lab-ready。
- #475 的原生畫面、世界內項目都還沒有真機結果。

## 產出路徑

- `docs/plans/issue-1094.json`
- `docs/reports/issue-1094.md`
- `docs/testing/smoke-m7.md`（§0.1 P24Lab 路徑與開跑前檢查）
- 分支 `task/1094`

## 給下一棒的提示

- **調度器**：建出 f475-retest 後，確認它也依賴 #1093。建議把 #1093 的 runner 改成 `claude-local`，符合「程式缺陷交 claude-local」的分流規則。f475-ui-fail-message 和 #1093 都會改 `UiClientBridge.cs` 附近；如果 #1093 已經把訊息改好，f475-ui-fail-message 只要補測試就能結案。
- **resource 執行者**：P24Lab 在 `environments\p24`，不是 `D:\P24Lab-ClientEngine`。寫入前先備份，正式 2000／3306 不能碰。
- **重測執行者**：照閘門做。SM01 沒到 `native-ui active` 就停，不要再交 58 列 ⚠️。
