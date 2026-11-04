# Issue #19／T016 — 主專案 r35 建置前檢查

日期：2026-11-04 19:30（UTC+08:00）；階段：test；第 3 次嘗試；分支：`task/19`。

## 結論

**未完成，主目錄寫入範圍待確認。** 本次開頭明定原始專案目錄「只讀取、不要在那裡寫檔」，正文卻要求在同一目錄更新 Git、建置、覆寫 receipt。本次已提出確認，尚未收到回覆；沒有執行依賴該授權的操作。這不是建置失敗，也不是自動核准審查拒絕。

依本次 worktree 明確指示保留既有 `task/19`，未 checkout 或建立 `task/16`。只更新本報告及 `issue-19.md`，不改 `docs/REPORT.md`。本報告為本次最新證據，前次紀錄由 Git 保留。

## 做了什麼／已驗證

- 已讀兩處 AGENTS.md、docs/REPORT.md，以及 worktree CLAUDE.md、TASKS.md 與既有報告；TASKS.md 未找到 T015／T016，依 Issue 範圍處理。
- worktree 起始 HEAD：`8af37a6c3077cee006858c13f18d92d0b016baf2`，分支 `task/19`，工作區乾淨；`git ls-remote --heads origin refs/heads/task/19` 退出 0，遠端同一提交。
- 主目錄 `D:\lineage開發\TW1608262200研究` 仍在 `task/7`，HEAD `fdf40cb94c79d27e2b9049da0c958756adca078e`。
- 主目錄追蹤檔無未提交變更，只有未追蹤的 `engine/_integration/r35/receipt.json`。沒有把未追蹤檔誤判成任務的停止條件；未刪除或覆寫它。
- `engine/assets` 是 Junction，目標確為 `D:\lineage開發\TW1608262200研究\releases\r28\assets`。
- `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg` 存在，大小 9801922 bytes；未 restore，未複製素材或封存。
- 主目錄預設唯讀 r35 來源核驗退出 0，181 個來源檔／190 個鎖定輸入通過，回傳 `writes: false`。
- 對主目錄 HEAD 與本機既有 origin/main 做唯讀差異比較：engine 差異只有 `engine/_integration/r35/rebuild.py` 與新增的 `test_rebuild_paths.py`。主目錄尚無 T014 的 lexical containment 修正，仍會將 junction 解析到 engine 外；未 fetch，故此比較不聲稱遠端最新狀態。
- 已重新讀取主目錄入口及 receipt，並計算下列既有產物的大小與 SHA-256；均與前次紀錄一致。
- #13 報告不在本 worktree，基準由 `D:\lineage開發\TW1608262200研究\scratch\wt\task-13\docs\reports\issue-13.md` 唯讀取得。

## 指令、退出碼與真實輸出

一般執行工具在預設設定與明確指定 PowerShell／login:false 的重試中均於建立程序前失敗：`helper_unknown_error: setup refresh had errors`，沒有命令退出碼。經審查允許的提升權限命令可完成唯讀檢查。初次 Get-Content 使用預設編碼導致亂碼，已以 UTF-8 重讀；rg 未安裝，改用 Select-String 與 git ls-files。

主目錄 Git 檢查：

```text
git -c core.quotepath=false status --porcelain
?? engine/_integration/r35/receipt.json
STATUS_EXIT=0

git --no-optional-locks -C D:\lineage開發\TW1608262200研究 status --porcelain --untracked-files=no
（stdout 空）
TRACKED_STATUS_EXIT=0
```

唯讀核驗於主目錄執行，使用既有 Python312 的完整路徑：

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 engine/_integration/r35/rebuild.py
```

stdout：

```json
{"status": "r35-inputs-verified", "sourceFiles": 181, "pinnedFiles": 190, "writes": false}
```

退出碼：0。未帶 --build、--verify、--write-receipt，不能視為完整建置或 runtime 驗證通過。

資源及雜湊檢查使用 `Get-Item`、`Get-ChildItem -Filter '*webview2*.nupkg'`、`Get-FileHash -Algorithm SHA256`；承載檢查的 PowerShell 程序退出 0。

| 任務命令（主目錄） | 本次狀態／退出碼 |
|---|---|
| `git fetch origin` | 未執行／不適用 |
| `git checkout main` | 未執行／不適用 |
| `git merge --ff-only origin/main` | 未執行／不適用 |
| `python -B -X utf8 engine/_integration/r35/rebuild.py --build --write-receipt` | 未執行／不適用 |
| `engine\啟動新版客戶端.cmd --check-only` | 未執行／不適用 |

### check-only JSON

本次沒有 check-only JSON。未用 #13 的 JSON 或舊 receipt 替代；主目錄的 `revision: r35`、`uiStarted: false` 與退出 0 尚未取得本次證據。

## 既有產物大小與 SHA-256

以下均為主目錄本次建置前的既有檔案，並非本次產物。

| 路徑（相對主目錄） | 大小（bytes） | SHA-256 |
|---|---:|---|
| `engine/launcher/P24.Launcher.exe` | 162304 | `6ea4f23dd2923476849b03f8fe817d5f489434ab4e74d79c0d91ba06af29eba5` |
| `engine/launcher/P24.Launcher.dll` | 24064 | `d1bf270bb68956e11739de81013e7732bdc573306e8190ab047808678b104ca6` |
| `engine/build/client-product-r35/P11.ClientEngine.dll` | 4925440 | `96ddabf5cc830acac79e5aeb8abb3970b0d34e8eb6edbfaed6ff21d4056867a9` |
| `engine/_integration/r35/receipt.json` | 474 | `1e08cd572e98f7bada9eec853e25c51d0cd4646c514ff5a8346e3f614a73cf9b` |

| 比較項目 | #13 SHA-256 | 結果 |
|---|---|---|
| receipt `productDllSha256` | `dfa7d804705398d00cff7a955151824b06302f9081f6946b900bb332d4977737` | 不一致 |
| receipt `launcherDllSha256` | `f4e4a27094e2a4d4b759f99b122e8a2617f9207cfa8408a71a3107d3b2e1f731` | 不一致 |
| 報告 `P24.Launcher.exe` | `cc076e2093efe555cc2be088a056c780ebe934f391215c772202ea7ca45042c5` | 不一致 |

EXE 與 DLL 分別比較。主目錄兩個 DLL 符合自身舊 receipt（builtAt `2026-11-04T05:27:44.285045+00:00`、SDK `10.0.300`），不能據此宣告驗收通過。

## 未驗證什麼／推測／待驗證

尚未更新主目錄、建置、寫 receipt 或執行 check-only，故主目錄入口尚未證明就緒。DLL 不一致的原因未確認；不以腳本差異推論 DLL 差異原因。

未啟動遊戲、登入、連正式服務或 DB，未讀取機密設定、啟停服務、附加或停止遊戲程序。未修改正式入口、project-manifest.json、docs/REPORT.md、封存、硬連結或素材，未 reset／stash／刪除使用者檔案。

## 使用者真機驗收時要執行的確切路徑

```text
D:\lineage開發\TW1608262200研究\engine\啟動新版客戶端.cmd
```

這是任務指定的最終驗收入口，目前尚未證明備妥；必須先完成主目錄完整建置及 check-only，才交使用者做 M1 原生實機驗收。

## 產出路徑／給下一棒的提示

- `docs/reports/issue-16.md`：本次完整證據。
- `docs/reports/issue-19.md`：調度交接；交付分支 `task/19`。
- 由調度器／本機對話處理指令衝突，明定本次主目錄必要寫入例外後再派本機執行器續做。具體交接文字見 issue-19.md；不要把相同文字原樣重排或將 T016 標記完成。
