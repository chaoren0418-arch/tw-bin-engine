# Issue #19／T016 — 主專案 r35 建置前檢查

日期：2026-11-04；階段：test；第 1 次嘗試；交付分支：`task/19`。

## 結論

**未完成，待釐清主目錄寫入授權。** 本次開頭指示要求原始專案目錄「只讀取、不要在那裡寫檔」，但任務正文要求在該處 fetch、checkout main、merge、建置與寫入 receipt。已提出確認問題，在收到允許之前只做唯讀檢查，未更新主目錄或執行建置。不能將既有產物或 #13 的成功紀錄視為本次通過。

依本次 worktree 指示使用既有 `task/19`，沒有 checkout 或建立 `task/16`。本報告沿用正文指定的 `docs/reports/issue-16.md`；本次 Issue #19 的交接提示另見 `issue-19.md`。

## 做了什麼／已驗證

- 已讀取 worktree 及主目錄的 `AGENTS.md`、`docs/REPORT.md`。查詢 worktree `TASKS.md` 未找到 T015／T016，依本次指派範圍處理。
- 主目錄：`D:\lineage開發\TW1608262200研究`；目前分支 `task/7`，HEAD `fdf40cb94c79d27e2b9049da0c958756adca078e`。
- 報告 worktree：`D:\lineage開發\TW1608262200研究\scratch\wt\task-19`；開始時分支 `task/19`，HEAD `20dedf6b12f03128742b1366a14483bb7b36f146`，工作區乾淨。
- 主目錄沒有未提交的追蹤檔變更，僅有未追蹤的 `engine/_integration/r35/receipt.json`。未刪除或覆寫此檔；未執行 reset、stash。
- `engine/assets` 存在，LinkType 為 `Junction`，Target 為 `D:\lineage開發\TW1608262200研究\releases\r28\assets`。沒有更改 junction 或封存內容。
- 主目錄離線套件 `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg` 存在，大小 9801922 bytes；本次僅確認存在與大小，未執行 restore。
- 唯讀檢查 `engine/啟動新版客戶端.cmd`，確認轉交參數給 `engine/launcher/P24.Launcher.exe`，並傳回其退出碼。
- #13 完整報告不在本 worktree 的追蹤檔清單內，因此從 `D:\lineage開發\TW1608262200研究\scratch\wt\task-13\docs\reports\issue-13.md` 唯讀取得比較基準。

## 指令、輸出與退出碼

一般沙箱執行器及 Node 執行器皆在程序啟動前失敗：`helper_unknown_error: setup refresh had errors`，無命令退出碼。改用獲准的提升權限 PowerShell 完成唯讀檢查；這是執行環境初始化問題，並非建置失敗或自動核准審查拒絕。

以下 Git 命令各退出 0：

```powershell
git --no-optional-locks -C 'D:\lineage開發\TW1608262200研究' status --porcelain
# stdout:
# ?? engine/_integration/r35/receipt.json

git --no-optional-locks -C 'D:\lineage開發\TW1608262200研究' status --porcelain --untracked-files=no
# stdout: 空

# 工作目錄為 task-19 worktree
git --no-optional-locks status --porcelain
# stdout: 空（寫報告前）
git branch --show-current
# stdout: task/19
```

`--no-optional-locks` 用來避免 Git 狀態檢查選擇性更新 index，維持唯讀要求。

資源與雜湊檢查使用 `Get-Item`、`Get-ChildItem -Filter '*webview2*.nupkg'`、`Get-FileHash -Algorithm SHA256`；承載檢查的 PowerShell 程序退出 0。`fsutil hardlink list` 對下表的三個產物及 receipt 各只列出自身路徑；此結果不代表已檢查所有建置輸入／輸出。

| 後續命令（主目錄執行） | 本次狀態／退出碼 |
|---|---|
| `git fetch origin` | 未執行／不適用 |
| `git checkout main` | 未執行／不適用 |
| `git merge --ff-only origin/main` | 未執行／不適用 |
| `python -B -X utf8 engine/_integration/r35/rebuild.py --build --write-receipt` | 未執行／不適用 |
| `engine\啟動新版客戶端.cmd --check-only` | 未執行／不適用 |

### check-only JSON

本次未執行，沒有可提供的 JSON。未以 #13 的 JSON、舊 receipt 或推測內容代替；`revision: r35`、`uiStarted: false` 與退出 0 均尚未在本次主目錄流程取得證據。

## 既有產物大小與 SHA-256

下列都是主目錄**本次建置前的既有檔案**，不是本次新建產物；路徑相對於主目錄。

| 路徑 | 大小（bytes） | SHA-256 |
|---|---:|---|
| `engine/launcher/P24.Launcher.exe` | 162304 | `6ea4f23dd2923476849b03f8fe817d5f489434ab4e74d79c0d91ba06af29eba5` |
| `engine/launcher/P24.Launcher.dll` | 24064 | `d1bf270bb68956e11739de81013e7732bdc573306e8190ab047808678b104ca6` |
| `engine/build/client-product-r35/P11.ClientEngine.dll` | 4925440 | `96ddabf5cc830acac79e5aeb8abb3970b0d34e8eb6edbfaed6ff21d4056867a9` |
| `engine/_integration/r35/receipt.json` | 474 | `1e08cd572e98f7bada9eec853e25c51d0cd4646c514ff5a8346e3f614a73cf9b` |

與 #13 的實際紀錄比較：

| 比較項目 | #13 SHA-256 | 結果 |
|---|---|---|
| receipt `productDllSha256` | `dfa7d804705398d00cff7a955151824b06302f9081f6946b900bb332d4977737` | 主目錄既有 DLL 不一致 |
| receipt `launcherDllSha256` | `f4e4a27094e2a4d4b759f99b122e8a2617f9207cfa8408a71a3107d3b2e1f731` | 主目錄既有 DLL 不一致 |
| 報告產物表 `P24.Launcher.exe` | `cc076e2093efe555cc2be088a056c780ebe934f391215c772202ea7ca45042c5` | 主目錄既有 EXE 不一致 |

EXE 與 DLL 分開比較，未拿 EXE 雜湊與 receipt 的 DLL 雜湊互相比較。主目錄既有 product／launcher DLL 雜湊均吻合自己的舊 receipt；舊 receipt 的 `builtAt` 為 `2026-11-04T05:27:44.285045+00:00`、SDK 為 `10.0.300`。這不足以證明舊建置最終核驗或本次驗收通過。

## 未驗證什麼／推測／待驗證

- 尚未更新主目錄、重新建置、寫 receipt 或執行 check-only；主目錄入口可直接供 M1 驗收的條件尚未成立。
- 沒有推定 DLL 不同的原因，尚未在更新來源後重建比較。
- 未啟動遊戲、登入、連正式服務或 DB，未查詢機密設定、啟停服務、附加或停止既有程序。
- 未修改正式入口、`project-manifest.json`、`docs/REPORT.md`、封存、硬連結或素材；未複製 pak/map/sprite。

## 使用者真機驗收時要執行的確切路徑

```text
D:\lineage開發\TW1608262200研究\engine\啟動新版客戶端.cmd
```

此為任務指定的最終驗收入口，**本次尚未證明已備妥**。須由本機執行器先完成主目錄建置與 check-only，取得 r35、uiStarted=false、退出 0 的證據，再交使用者執行原生實機驗收。

## 產出路徑與交接

- `docs/reports/issue-16.md`：本次檢查證據與未完成項目。
- `docs/reports/issue-19.md`：本次調度任務的交接提示。
- 交付分支 `task/19`；只提交上述兩份報告，不改索引。
- 請調度器／本機對話先釐清主目錄唯讀限制與建置指令的衝突；取得允許後由本機執行器重查追蹤檔狀態並續做。暫存與命令紀錄應放在本 worktree 的 `scratch/`，重建可使用 `--output` 指定該處，避免新增帶時間戳的證據目錄。
