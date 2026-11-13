# Issue #1116：#1108 實機驗收失敗項目分流

**結論：#1108 共 14 項，✅ 13、❌ 0、⚠️ 1（R07）。R07 是測試環境（Windows GUI 擷取／輸入工具）問題，不是引擎程式缺陷，所以不開 code 任務。開 1 張 resource 任務修 GUI 自動化環境，再開 1 張只重測 R07 的 test 任務。**

依據：`task/1108` 分支（PR #1115）的 `docs/reports/issue-1108.md`、`docs/reports/issue-1108-evidence.json`（`gui` 段）、Issue #1108 的 `=== RESULT ===` 留言，以及 #1108 worktree 的原始證據 `scratch/wt/task-1108/scratch/issue-1108/gui-selection.txt`、`start_client.py`、`client-service/`（只讀）。本張沒有改程式，也沒有執行任何程式。

## 1. 已通過、不重測的項目（✅ 13）

| 編號 | 項目 | 之後要不要重測 |
|---|---|---|
| B01 | 備份 | 不重測 |
| R05 | 雙角色同時在線 | 不重測 |
| R08 | 弓箭 | 不重測 |
| R09 | 肉 | 不重測 |
| R10 | 戒指／卷軸 | 不重測 |
| R11 | 料理書 | 不重測 |
| R12 | 寵物／召喚 | 不重測 |
| R13 | 血盟／盟倉 | 不重測 |
| R14 | 金幣 | 不重測 |
| R15 | GM0 | 不重測（重測任務收尾時本來就要讀回全角色 GM0，這是收尾檢查，不算重測） |
| C01 | 變更邊界 | 不重測 |
| C02 | 程序收尾 | 不重測 |
| V01 | 建置／還原核驗 | 不重測 |

**不需要回歸任何 ✅ 項目**：這次沒有開 code 任務；f1108-gui-capture 不改程式、不寫 DB，上面各項的依據都沒有改變。

## 2. 失敗項目的歸類

| 編號 | #1108 結果 | 觀察 | 歸類 | 對應 |
|---|---|---|---|---|
| R07 七職業 | ⚠️ | 資料面 7/7 已完成：七職業都用一般玩家協定（C84）建立，原服回 S98=2＋S127，DB 讀回 objid 10305／10314／10325／10334／10344／10356／10365。**GUI 建角 0/7**：客戶端到了 `CharacterList`，UIA 樹完整（看得到「建立角色」按鈕），但 Windows 操作輔助工具回報 `FrameArrived timed out`、`window capture timed out`、`coordinate input geometry is unavailable` | **(b) 測試環境／工具** | `f1108-gui-capture`（resource）→ `f1108-r07-retest`（test） |

判定 (b)、不是 (a) 程式缺陷的理由：
- 被擷取的視窗是一般 WinForms 視窗「開發工具（過渡操作介面）」，UIA 讀得到完整控制項樹與數值（`gui-selection.txt`）。所以程序有回應、視窗存在，UI 也沒有卡住。
- 客戶端自己的截圖（`client-service/manual-screen.png`、`manual-client.png`）有正常產出，引擎的繪製本身沒有問題。
- 三個錯誤都出在操作輔助工具的 Windows.Graphics.Capture／輸入幾何這一層。#1108 的 log 和 `native-ui.log`／`stderr.log` 都沒有引擎錯誤（`stderr.log` 是 0 bytes）。
- 原服也沒有禁用任何職業。#1108 一開始被拒（S98=9）是因為名稱含禁用字「測試」，換名後七職業都成功。這是命名限制，已在重測任務寫明。

已知限制（(c)，不開任務）：
- 伺服器禁用名稱字「測試」（S98=9），這是原服規則，不改。
- p241db6e／p242db6e 都是 4/4 槽，p24svc01 是 3/4。GUI 真正送出建角只剩 1 個空位，所以重測只用這個空位做一次真正送出，再刪掉這次自己建的角色；其餘六個職業只驗到建角對話框的「可以建立」狀態。七職業的伺服器接受證據沿用 #1108 的協定建角。

## 3. 拆出的任務（`docs/plans/issue-1116.json`）

| id | stage／runner | 內容 | depends_on |
|---|---|---|---|
| `f1108-gui-capture` | resource／codex-local（runner_lock、real_machine） | 查執行 session 與桌面狀態；確認工具本身可以擷取；用 #1108 相同參數到選角，對主視窗與開發工具視窗各做截圖、讀文字、座標點擊。擷取還是失敗時，驗證不用座標的方法（`element_index` 點擊、`set_value`）配合客戶端內建截圖是否夠用。不寫 DB、不改系統設定；如果是桌面鎖定或非互動 session，就標 `needs:local` | — |
| `f1108-r07-retest` | test／codex-local（runner_lock、real_machine、milestone_smoke） | **只重測 R07**：GUI 建角對話框七職業各截一張「可以建立」的圖；用唯一空位真正送出一次（名稱不含「測試」），讀回後用 GUI 刪掉這次建的角色。其餘 13 項沿用 #1108 | f1108-gui-capture |

## 做了什麼／怎麼驗證／未驗證什麼

- 做了：讀 #1108 RESULT、報告、證據 JSON，以及 worktree 內的 GUI 證據和啟動腳本；對照 `engine/source/CharacterManagementUi.cs`（「建立角色」→ `CharacterCreationDialog`，本機檢查訊息「資料符合本機檢查，可以建立」），還有 `docs/testing/smoke-m7.md` 的 SM08／SM09／SM48 範圍；寫好分流計畫。
- 驗證：計畫 JSON 用 python `json.load` 確認格式正確。
- 未驗證（推測）：擷取逾時的確切根因（桌面鎖定、非互動 session、視窗被遮蔽或最小化、工具本身故障）沒有實機確認，交給 `f1108-gui-capture` 查。

## 產出路徑

- `docs/plans/issue-1116.json`（2 張任務）
- `docs/reports/issue-1116.md`（本檔）

## 給下一棒的提示

1. **#1109（f1098-retest）也會受同一個擷取問題影響**：它的 M7 smoke 每列都要截圖，而且第一段也列了 R07。建議調度器讓 #1109 加上 `depends_on` f1108-gui-capture，或在派 #1109 前先確認該張結果，避免 #1109 因為同樣的截圖逾時而空轉。#1109 的 R07 列可以直接引用 f1108-r07-retest 的結果。
2. 重測照 #1108 的方式用 `--classic-ui`（WinForms 建角對話框）。原生 CreateUI 的職業頁是 SM08，屬於 #1109，不要混在 R07。
3. **SM48 的資料風險（不是本輪失敗項目，只提醒）**：除了資源法師 LV52，七職業的新角色都是 LV1，只有起始技能。SM48 要求「非 1／3／4／8 技能、範圍法術」，跑 #1109 的 SM48 前要先確認各職業有沒有可以施放的技能。不夠的話，依 #160／#175 暫時設 GM 準備，用完改回。
4. #1108 提醒過：調度器清 task-1108 worktree 前，要先保留 `scratch/issue-1108/backup-before`，而且不可以遞迴刪除（裡面有 junction）。
