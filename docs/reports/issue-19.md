# Issue #19 — T016 主專案 r35 建置與 M1 驗收交接

日期：2026-11-04；階段：test；本次授權後第 1 次執行；交付分支：`task/19`。

## 結論

**本次建置與 check-only 驗收通過。** 主專案目錄的 r35 完整建置及指定入口 `--check-only` 均退出 0；主程式與 launcher 各為 0 警告、0 錯誤。JSON 回傳 `revision: r35`、`uiStarted: false`，可交使用者進行 M1 原生實機驗收。

與 #13 的 DLL／EXE SHA-256 **不一致**，不宣稱兩次產物位元相同。已確認兩次產生的 AssemblyInformationalVersion 嵌入不同 Git commit；來源 lock 與 rebuild.py 相同，兩個提交之間的 `engine` 無 Git 差異。詳見下方雜湊比較與限制。

## 做了什麼／已驗證

- 已讀 worktree 與主專案的 AGENTS.md、docs/REPORT.md，並讀主專案更新後 CLAUDE.md、TASKS.md 相關項目、#13 與既有 #16／#19 報告。TASKS.md 未列 T015／T016，依本次 Issue 執行。
- 本次明確寫入授權取代先前主目錄唯讀衝突；主專案只進行指定的 Git 更新、建置及快取／證據寫入。報告只寫 task-19 worktree，沒有切換或建立此 worktree 的分支。
- 主專案起始分支 `task/7`、HEAD `fdf40cb94c79d27e2b9049da0c958756adca078e`。追蹤檔狀態乾淨後執行 fetch、checkout main、merge --ff-only；全部退出 0。
- 主專案更新後分支 `main`、HEAD `20dedf6b12f03128742b1366a14483bb7b36f146`，包含 T014。ff-only 從 main 的 `db5f710` 前進到 `20dedf6`。
- `engine/assets` 為既有 Junction，目標 `D:\lineage開發\TW1608262200研究\releases\r28\assets`；只讀取，未建立或改動 junction。
- `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg` 存在，9801922 bytes。沿用離線 NuGet.Config，未複製任何 pak/map/sprite。
- 建置前檢查輸出及快取路徑與父目錄，652 個現存項目未發現 reparse point 或多重硬連結；assets 不列為寫入路徑。
- 完整建置核驗 181 個來源檔、190 個鎖定輸入，SDK 10.0.300。check-only 核對 46 個 runtime／web／素材索引檔案的大小與 SHA-256，以及 13957 個素材檔案的存在與大小。
- 建置及 check-only 後，主目錄追蹤檔仍乾淨。建置前後 `啟動引擎.cmd`、`project-manifest.json`、`docs/REPORT.md`、engine 入口及 `assets/integrity.json` 的 SHA-256 均不變。主目錄 docs/REPORT.md 僅隨已授權的 ff-only 更新上游版本，未手動編輯；worktree docs/REPORT.md 未改動。

## 指令、環境與退出碼

工作目錄：`D:\lineage開發\TW1608262200研究`。

```powershell
git status --porcelain --untracked-files=no
git fetch origin
git checkout main
git merge --ff-only origin/main
python -B -X utf8 engine/_integration/r35/rebuild.py --build --write-receipt --output scratch/issue-19/rebuild
engine\啟動新版客戶端.cmd --check-only
```

追蹤檔狀態 stdout 為空；Git 四個命令均退出 0。最後一個入口由 `cmd.exe /d /c` 執行，僅帶 `--check-only`。

一般執行工具兩次在建立程序前回報 `helper_unknown_error: setup refresh had errors`；改用經核准的提升權限命令。最初 Get-Content 編碼錯誤已用 UTF-8 重讀。第一次建置包裝程式在讀取保護檔案時，因 PowerShell 管線將中文字轉成問號而退出 1，當時尚未啟動 rebuild.py；設定 `$OutputEncoding` 與 `[Console]::OutputEncoding` 為 UTF-8 後，真正建置執行一次並成功。

Python 使用既有 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe`，僅在子程序 PATH 加入該目錄，未修改全域設定。環境變數如下（均相對主專案）：

| 變數 | 路徑 |
|---|---|
| TEMP / TMP | `scratch/issue-19/tmp` |
| DOTNET_CLI_HOME | `scratch/issue-19/.dotnet` |
| NUGET_HTTP_CACHE_PATH | `scratch/issue-19/nuget-http` |
| NUGET_PACKAGES | `engine/.nuget` |
| NUGET_SCRATCH | `scratch/issue-19/nuget-scratch` |

rebuild.py 再將其子程序 TEMP/TMP、DOTNET_CLI_HOME、NUGET_SCRATCH 分別設為 `scratch/issue-19/rebuild/tmp`、`.dotnet`、`tmp/nuget`，維持專案內快取。

建置開始：2026-11-04T19:39:06.207760+08:00；結束：2026-11-04T19:39:22.245566+08:00。check-only 完成：2026-11-04T19:39:48.027466+08:00。

| 命令／階段 | 退出碼 |
|---|---:|
| 完整建置 | 0 |
| sdk | 0 |
| product-restore | 0 |
| product-build | 0 |
| launcher-restore | 0 |
| launcher-build | 0 |
| check-only | 0 |

### 完整建置 stdout

```text
sdk 0
product-restore 0
product-build 0
launcher-restore 0
launcher-build 0
receipt written D:\lineage開發\TW1608262200研究\engine\_integration\r35\receipt.json
PASS r35 {"productDllSha256": "04ab984c350effd6811f6f225c6fc2db90cf7a45fa3615ed1237811c60805dbf", "audioDllSha256": "273d30e88313fd5da401d6b4d95d2add3f43aed96baf40d7dd6715d3acb47e77", "productManifestSha256": "3a27f9fab63ce30b08281428bdfea3728514d1c755ded05d7d90e332d30f936c", "launcherDllSha256": "97dde93946ea7eb97f4fb8d1c0c11c976d083d39b95ca6344cbb959b254ee446"}
```

### product-build stdout

```text
P24.Audio -> D:\lineage開發\TW1608262200研究\engine\build\client-product-r35\P24.Audio.dll
  OfflineClient -> D:\lineage開發\TW1608262200研究\engine\build\client-product-r35\P11.ClientEngine.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:09.34
```

### launcher-build stdout

```text
Launcher -> D:\lineage開發\TW1608262200研究\engine\launcher\P24.Launcher.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:03.06
```

頂層建置與 check-only stderr 均為空（0 bytes）。子命令 stderr 大小：sdk=0 bytes, product-restore=499 bytes, product-build=0 bytes, launcher-restore=0 bytes, launcher-build=0 bytes。

### check-only 完整 JSON

以下為本次 stdout 全文，僅統一換行；不引用 #13 的執行結果代替。

```json
{
  "status": "checked-only",
  "revision": "r35",
  "executable": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\client-product-r35\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\product-r35-manifest.json",
  "manifestSha256": "3a27f9fab63ce30b08281428bdfea3728514d1c755ded05d7d90e332d30f936c",
  "hashedRuntimeAndWebFiles": 46,
  "assetFilesCheckedForPresenceAndSize": 13957,
  "assetContentVerification": "Pinned integrity manifest; body hashes remain verified by the product when loaded, matching prior entry contract.",
  "runtime": {
    "architecture": "x64",
    "loadedCoreVersion": "10.0.8",
    "loadedCoreDirectory": "C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\10.0.8\\",
    "desktopVersion": "10.0.8",
    "desktopDirectory": "C:\\Program Files\\dotnet\\shared\\Microsoft.WindowsDesktop.App\\10.0.8"
  },
  "webView2Version": "156.0.4314.58",
  "powerShellRequired": false,
  "pathLookupUsed": false,
  "autoLogin": false,
  "uiStarted": false,
  "networkStarted": false,
  "servicesStarted": false
}
```

### 本次 receipt

```json
{
  "schema": "r35-receipt-v1",
  "builtAt": "2026-11-04T11:39:22.202157+00:00",
  "sdk": "10.0.300",
  "productDllSha256": "04ab984c350effd6811f6f225c6fc2db90cf7a45fa3615ed1237811c60805dbf",
  "audioDllSha256": "273d30e88313fd5da401d6b4d95d2add3f43aed96baf40d7dd6715d3acb47e77",
  "productManifestSha256": "3a27f9fab63ce30b08281428bdfea3728514d1c755ded05d7d90e332d30f936c",
  "launcherDllSha256": "97dde93946ea7eb97f4fb8d1c0c11c976d083d39b95ca6344cbb959b254ee446"
}
```

### 本次 result.json

```json
{
  "status": "pass-r35-build",
  "sourceFiles": 181,
  "GUI": false,
  "network": false,
  "database": false,
  "productDllSha256": "04ab984c350effd6811f6f225c6fc2db90cf7a45fa3615ed1237811c60805dbf",
  "audioDllSha256": "273d30e88313fd5da401d6b4d95d2add3f43aed96baf40d7dd6715d3acb47e77",
  "productManifestSha256": "3a27f9fab63ce30b08281428bdfea3728514d1c755ded05d7d90e332d30f936c",
  "launcherDllSha256": "97dde93946ea7eb97f4fb8d1c0c11c976d083d39b95ca6344cbb959b254ee446"
}
```

## 產物大小與 SHA-256

路徑相對主專案，均由本次執行後實檔計算。

| 路徑 | 大小（bytes） | SHA-256 |
|---|---:|---|
| `engine/launcher/P24.Launcher.exe` | 162304 | `4228c0c953c8be8288fd36871fa4afdd85bd3717609550667efc2fedabf06c6e` |
| `engine/launcher/P24.Launcher.dll` | 24064 | `97dde93946ea7eb97f4fb8d1c0c11c976d083d39b95ca6344cbb959b254ee446` |
| `engine/build/client-product-r35/P11.ClientEngine.dll` | 4925440 | `04ab984c350effd6811f6f225c6fc2db90cf7a45fa3615ed1237811c60805dbf` |
| `engine/build/client-product-r35/P24.Audio.dll` | 53760 | `273d30e88313fd5da401d6b4d95d2add3f43aed96baf40d7dd6715d3acb47e77` |
| `engine/build/product-r35-manifest.json` | 7953 | `3a27f9fab63ce30b08281428bdfea3728514d1c755ded05d7d90e332d30f936c` |
| `engine/_integration/r35/receipt.json` | 474 | `24d626650676982d12243a370e454a8ef8873b348c500de1fbf4bb1b6dac7830` |
| `engine/_integration/r35/rebuild.py` | 8890 | `24417d03d3ba264c9e4ef1624e4cfe7ddef223b17485645e7f97a454a8cb560d` |
| `engine/_integration/r35/rebuild-lock.json` | 27475 | `36b3e6991a69d5b06b8978022af61a8593dc5da41de84ecc50b52a1249e6794d` |

### 與 #13 比較

基準唯讀取自 `scratch/wt/task-13/engine/_integration/r35/receipt.json` 及其 `docs/reports/issue-13.md`。

| 比較項目 | #13 SHA-256 | 本次結果 |
|---|---|---|
| `productDllSha256` | `dfa7d804705398d00cff7a955151824b06302f9081f6946b900bb332d4977737` | 不一致 |
| `launcherDllSha256` | `f4e4a27094e2a4d4b759f99b122e8a2617f9207cfa8408a71a3107d3b2e1f731` | 不一致 |
| `audioDllSha256` | `2e1ba196373e5d9927e5a5f3ba5aaea818e4f62b970763d33425f89467f1038c` | 不一致 |
| `productManifestSha256` | `eaac44a43e6c06e5e544df0e1503d58a72c829ccd33e5345c56d858e0a03df5c` | 不一致 |
| `P24.Launcher.exe` | `cc076e2093efe555cc2be088a056c780ebe934f391215c772202ea7ca45042c5` | 不一致 |

已驗證：`git diff e32fe7bcce38ddc03f7151fd7278190263078155 HEAD -- engine` 為空；rebuild.py、rebuild-lock.json 的大小與 SHA-256 與 #13 報告一致。三個專案的 `obj/Release/net10.0-windows/*.AssemblyInfo.cs` 都顯示：

```text
本次：AssemblyInformationalVersionAttribute("1.0.0+20dedf6b12f03128742b1366a14483bb7b36f146")
#13 ：AssemblyInformationalVersionAttribute("1.0.0+e32fe7bcce38ddc03f7151fd7278190263078155")
```

推測：Git commit 版本資訊是 DLL 差異的原因之一；尚未做二進位逐欄位分析或同 commit 的跨目錄重建，不能宣稱它是唯一原因，也不能宣稱與 #13 位元可重現。未為追求雜湊相同而覆寫來源、版本設定或搬入 #13 二進位。本次產物符合本次新 receipt，manifest 雜湊與 check-only 一致。

## 未驗證什麼

- 未啟動遊戲、登入、看原生畫面，未執行 NPC、戰鬥、地圖／音訊等 M1 功能驗收。check-only 不代表遊戲流程通過。
- 未連正式服務或 DB、讀取機密設定、啟停服務、附加或停止遊戲程序。
- 素材本體只核對存在與大小，未逐檔重算雜湊；依既有契約由產品載入時核驗內容。
- 未切換根目錄正式入口或 project-manifest.json 的 entry；未修改封存、硬連結、客戶端素材，未 reset／stash／rebase／clean 或刪除使用者檔案。
- 未驗證推送後 CI，未做第二次建置的位元一致性比較。

## 產出路徑

- task-19 worktree：`docs/reports/issue-19.md`（本次完整證據）、`docs/reports/issue-16.md`（指向本報告的交接摘要）。僅提交這兩份文件。
- 主專案：`scratch/issue-19/build-command.json`、`build.stdout`、`build.stderr`、`check-only-command.json`、`check-only.stdout`、`check-only.stderr`、`artifacts.json`、`protected-before.json`。
- 主專案：`scratch/issue-19/rebuild/` 保存五個建置子命令的 argv／退出碼／stdout／stderr 及 result.json；快取也在授權路徑內。
- 主專案：`engine/build/client-product-r35/`、`engine/build/product-r35-manifest.json`、`engine/launcher/`、`engine/_integration/r35/receipt.json`；二進位與 receipt 保留本機，不納入報告提交。

## 給下一棒的提示

調度器／Claude 審閱 `task/19` 報告，確認本次主目錄建置與 check-only 已通過；#13 雜湊不同是已揭露的比較結果，若需要位元可重現性保證，另派同 commit 對照建置與版本資訊調查。報告合併交使用者處理。

使用者 M1 真機驗收的確切入口已在主目錄備妥：

```text
D:\lineage開發\TW1608262200研究\engine\啟動新版客戶端.cmd
```

由使用者執行上列入口並依既有 N01／M1 核對表記錄登入、選角、世界畫面／滑鼠與流程觀測；AI 本次沒有執行不帶 `--check-only` 的入口。不要將根目錄的 `啟動引擎.cmd` 與這個 r35 入口混用。原生功能結果仍待使用者驗收。
