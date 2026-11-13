# Issue #1120：分流 #1109 實機驗收失敗項目

**結論：#1109 沒有程式缺陷。兩個 ⚠️（R03、R04）的原因相同：執行器的指示互相矛盾，所以沒有把服務開起來。拆成 1 張 resource（把寫入範圍寫清楚＋開服到選角）和 1 張重測（只測 #1109 沒測到的項目）。沒有開 code 任務。**

## 來源

- #1109 最新 `=== RESULT ===`（Issue 留言，runner `codex-local`，failed）。
- `docs/reports/issue-1109.md`、`docs/reports/issue-1109-evidence.json`（`origin/task/1109`，commit `d3d89c8f`）。
- 參考：#1104 分流計畫（`docs/plans/issue-1104.json`）、#1107 報告（`docs/reports/issue-1107.md`，在 main）、#1108 報告（`origin/task/1108`）、#1116 分流計畫（`docs/plans/issue-1116.json`）、#1119 內文。

## 1. #1109 已通過（✅）的項目

**沒有。** #1109 停在 R03／R04 閘門，沒有任何功能項目判定 ✅。只有以下幾件事是「已驗證的事實」，不是驗收項目：
- main（`50679105`）已包含 #1093（PR #1096）和 #1097（PR #1102）。
- worktree 建置成功：0 錯誤、4 個警告；產品 DLL `1642ea35…6e93`，跟 #475 的 `7f861fce…2c77` 不同。

所以不需要寫「哪些 ✅ 項目要附帶回歸」。原本就排定沿用的項目（R01／R02／R16、#475 已取得的子步驟）照舊沿用。

## 2. 每個失敗項目的分類

| #1109 項目 | 結果 | 根因 | 分類 | 對應 |
|---|---|---|---|---|
| R03 遊戲服 42061 開服 | ⚠️ 未測 | 42061／43324 都沒有監聽：#1107 依規定收尾，所以服務沒保持啟動。要開服就得寫入主專案的 `environments\p24\db-data` 等目錄（`mysql.ini` 的 datadir 是 `D:/BinEngine/...`，而 `D:\BinEngine` 是指向主專案的 junction，這點已核對）。執行器提示又寫「原始專案目錄只讀」，執行器判定兩者衝突，就停下來了。 | (b) 測試環境／授權範圍 | **f1109-lab-runtime** |
| R04 登入至選角 | ⚠️ 未測 | 跟 R03 同一個根因：服務沒開，TCP 探測逾時，也沒有送帳密 | (b)，與 R03 合成一張 | **f1109-lab-runtime**（R03／R04 實測）＋**f1109-retest**（閘門重測） |
| R05、R06、R07、R12、R13、R17；R08–R11／R14／R15 唯讀回歸；§0.2 check-only；SM01–SM59 | 未執行 | 被 R03／R04 閘門擋下，本身沒有失敗證據 | 不另開修正任務 | **f1109-retest**（R07 例外，見下一列） |
| R07 七職業 GUI 建角 | 未執行 | 範圍跟 #1119（f1108-r07-retest，前置 #1118 修 GUI 擷取）完全重疊 | 不重複開 | 交給**現有的 #1119**；f1109-retest 不重測 R07，只沿用它的結果 |

判定「不是程式缺陷」的依據：#1109 沒有啟動客戶端、伺服器或 DB，也沒有產生任何引擎輸出可以拿來判定；同一個環境在 #1107（R03／R04／R06 ✅）和 #1108（✅ 13／⚠️ 1）都能正常開服並登入。

## 3. 已知限制（不開任務）

- **nativeUi 要不要設為預設、#475／T112 能不能結案**：不是失敗項目，交主控裁定（#1109 原文也是這樣寫）。
- **產品 DLL SHA 跟 #475 不同**：這是預期結果，不是缺陷。

## 4. 新任務（`docs/plans/issue-1120.json`）

| id | stage | 依賴 | 要點 |
|---|---|---|---|
| `f1109-lab-runtime` | resource（codex-local、runner_lock、real_machine） | — | 明文列出主專案可以寫的位置，只限 `environments\p24` 的 db-data／db-temp／db-files／db-logs、JAR 自己寫的 log、`backups\issue-<本號>-*`；其他位置照舊只讀。如果執行器仍判定衝突，標 `needs:orchestrator`，不轉給使用者。照 #1107 的方式開 43324／42061，實測 R03／R04；**只讀**讀回 #1108 寫進持久 DB 的狀態（R07 資料面、R08–R15）；用 SHA 證明設定檔沒改；列出寫入清單；最後收尾。 |
| `f1109-retest` | test（codex-local、runner_lock、real_machine、milestone_smoke） | f1109-lab-runtime | **只重測 R03、R04、R05、R06（只補進世界後的情境）、R12、R13、R17**，以及 #475 SM01–SM59（R03／R04 閘門、SM01 閘門照舊）。沿用：R01／R02／R16；R08–R11／R14／R15 用前一張的讀回結果；R07 交 #1119；#475 已取得的 SM55／SM56／SM59／SM15／SM02 子步驟。寫入範圍段落跟前一張相同。 |

## 怎麼驗證

- 用 `scratch/issue-1120/make_plan.py` 產生計畫 JSON，再用 `json.load` 讀回：2 項，id、stage、depends_on 都正確。
- 唯讀核對：`D:\BinEngine` 是 junction，目標是 `D:\lineage開發\TW1608262200研究`；`environments\p24` 是一般目錄。
- 用 `gh issue view` 查過 #1107／#1108／#1116 已關閉，#1118／#1119 仍開著。
- 沒有改程式、沒有執行任何程式或遊戲、沒有碰 DB、沒有寫主專案目錄。

## 未驗證

- 新任務裡的寫入範圍說明，執行器會不會接受：要看 f1109-lab-runtime 實際跑的結果。
- 調度器的 plan 不能用 Issue 編號當依賴，所以 f1109-retest 對 #1119 的關係只寫在內文，`depends_on` 裡沒有。

## 給下一棒的提示

- **給調度器（建議標 `needs:orchestrator`）**：每張 runner 提示都會注入「原始專案目錄只讀取、不要在那裡寫檔」。`real_machine: true` 而且指定主專案 `environments\p24` 的任務，會一直撞上這句。#1118／#1119 也是在主專案 P24Lab 執行，同樣有風險。建議在提示範本裡把 P24Lab 授權的 `environments\p24` 運行目錄排除在外，或者在 #1118／#1119 開跑前補上跟本計畫相同的寫入範圍段落。
- 不要重做 #1107 的孤兒處置或 #1108 的補資料。f1109-lab-runtime 讀回結果不一致時，先停下來回報，不要自己補。
- 產出：`docs/plans/issue-1120.json`、`docs/reports/issue-1120.md`；產生腳本在 `scratch/issue-1120/make_plan.py`（不進 git）。
