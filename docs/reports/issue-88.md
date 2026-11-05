# Issue #88／T017：全新 worktree 的 dev 建置與 check-only

日期：2026-11-06（Asia/Taipei）；階段：test；第 1 次嘗試；分支：`task/88`。

## 結論／已驗證

**通過**。全新 worktree 在同一來源版本完成 dev 建置及 `--check-only`，兩者 exit 0。建置輸出 `PASS dev {…}`，190 個來源檔／199 個鎖定輸入核驗成功；SDK、兩次 restore、兩次 build 全部 exit 0，主程式與 launcher 各 0 警告、0 錯誤。check-only JSON 已斷言確認 `status: checked-only`、`revision: dev`、`uiStarted: false`、`networkStarted: false`、`servicesStarted: false`。

本報告補足 TASKS.md M0／T017 的「全新 worktree 的 dev 建置加 check-only」證據；不自行宣告整個 M0 完成或修改任務表，T005 及其他驗收條件由主控裁定。

## 做了什麼／來源版本

- 已讀主目錄 AGENTS.md、docs/REPORT.md，以及新 worktree 的 AGENTS.md／CLAUDE.md、TASKS.md、docs/BUILD.md；參照 #13 與 #77 第 2a 輪報告。
- 主目錄：`D:\lineage開發\TW1608262200研究`。起始與驗證後追蹤檔狀態均乾淨，分支一直是 `task/77`，HEAD 一直是 `50d09756722961827411d2cca5d7900a1cf75f3b`。
- 本次來源 `<SRC>`：**`4021caf0b6b725d1d3973c33636aa0cad27685f9`**，是本次 fetch 得到的 `origin/main`，亦是新 worktree 建置及 check-only 時的 HEAD。`git merge-base --is-ancestor cbadf58 HEAD` exit 0，確認包含 PR #87。
- 全新 worktree：`D:\lineage開發\TW1608262200研究\scratch\wt\task-88`。建立前 `Test-Path scratch/wt/task-88` 回傳 False，`git branch --list task/88` 無輸出；未重用 #13 或其他 worktree。
- 依本任務具體禁止事項保留主目錄 checkout，未執行通用前言的 `checkout -B`。只在新 worktree 分支新增本報告；報告提交後 HEAD 會是 `<SRC>` 的文件子提交，產品輸入仍是 `<SRC>`。

### 前置命令與結果

以下 Git 命令都 exit 0；空輸出明列為「空」。建立 worktree 的 stderr 是正常 checkout 進度，沒有錯誤。

| 工作目錄／命令 | stdout／結果 |
|---|---|
| 主目錄：`git status --porcelain --untracked-files=no` | 空；追蹤檔乾淨 |
| 主目錄：`git fetch origin` | stdout 空；stderr 顯示 main `61750f0..4021caf`，另一遠端分支 `f327b4d..89f8001` |
| 主目錄：`git rev-parse origin/main` | `4021caf0b6b725d1d3973c33636aa0cad27685f9` |
| 主目錄：`git merge-base --is-ancestor cbadf58 origin/main` | 空；exit 0 |
| 主目錄：`git branch --show-current` | `task/77` |
| 主目錄：`git rev-parse HEAD` | `50d09756722961827411d2cca5d7900a1cf75f3b` |
| 主目錄：`git worktree add scratch/wt/task-88 -b task/88 origin/main` | `branch 'task/88' set up to track 'origin/main'.`；`HEAD is now at 4021caf0 report: index #76 [skip ci]` |
| 新 worktree：`git rev-parse HEAD` | `<SRC>` |
| 新 worktree：`git merge-base --is-ancestor cbadf58 HEAD` | 空；exit 0 |
| 新 worktree：`git status --porcelain --untracked-files=no`（建置前） | 空 |

### 資源與暫存接法

依 #13 使用**離線 vendor**；沒有使用 `.github/nuget.ci.config`，沒有修改 NuGet.Config。下列 PowerShell 前置執行 exit 0，Copy-Item 無輸出，junction 回傳 Target 如下：

```powershell
New-Item -ItemType Directory -Path scratch/issue-88, engine/vendor
New-Item -ItemType Junction -Path engine/assets -Target 'D:\lineage開發\TW1608262200研究\engine\assets'
Copy-Item -LiteralPath 'D:\lineage開發\TW1608262200研究\engine\vendor\microsoft.web.webview2.1.0.4258.31.nupkg' -Destination engine/vendor/
```

- 新 `engine/assets` junction 指向主專案 `D:\lineage開發\TW1608262200研究\engine\assets`，後者既有 Target 是 `D:\lineage開發\TW1608262200研究\releases\r28\assets`。只供本次唯讀引用，未改目標、ACL 或封存內容。
- 複製的套件僅 `microsoft.web.webview2.1.0.4258.31.nupkg`，9,801,922 bytes，SHA-256 `56f7f4b8bf9aee4b8efefbbdd4f67d5f74ebd1b100ed0806da71bf76af481aa9`。沒有複製 pak／map／sprite。
- 透過 `py -0p`（exit 0）找到既有 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe`，只於本次程序 PATH 前置該目錄，未安裝工具或修改全域環境。
- 命令工具一般模式兩次在建立程序前失敗：`helper_unknown_error: setup refresh had errors`；獲准提升權限後才實際執行 Git。`rg` 不在 PATH，改用 `git ls-files`／PowerShell；這些是工具環境問題，沒有更動產品或跳過檢驗。
- 一次性擷取腳本 `scratch/issue-88/run.py` 使用 subprocess 原樣執行下列兩條命令，記錄原始 stdout／stderr、argv、時間、退出碼；第一條失敗即停止，不執行 check-only。執行器 exit 0。
- TEMP、TMP、DOTNET_CLI_HOME、NUGET_PACKAGES 均指向新 worktree 的 `scratch/issue-88/`；另將 NuGet HTTP cache／scratch 也放在此處。rebuild.py 對自己的子程序把 TEMP／TMP 改成 `scratch/issue-88/rebuild/tmp`，DOTNET_CLI_HOME 改成 `scratch/issue-88/rebuild/.dotnet`，NUGET_SCRATCH 改成 `scratch/issue-88/rebuild/tmp/nuget`；NUGET_PACKAGES 仍保留 `scratch/issue-88/packages`。
- SDK 首次執行禁用 telemetry 與 ASP.NET certificate 生成；所有建置均使用既定腳本輸出位置：`engine/build/`、各專案 `obj/`／`bin/` 與既定 `engine/launcher/`。receipt 依指定 `--write-receipt` 產生於 `engine/_integration/dev/receipt.json`，未提交。

實際外層環境：

```json
{
  "TEMP": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\scratch\\issue-88\\tmp",
  "TMP": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\scratch\\issue-88\\tmp",
  "DOTNET_CLI_HOME": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\scratch\\issue-88\\.dotnet",
  "NUGET_PACKAGES": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\scratch\\issue-88\\packages",
  "NUGET_HTTP_CACHE_PATH": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\scratch\\issue-88\\http-cache",
  "NUGET_SCRATCH": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\scratch\\issue-88\\nuget-scratch"
}
```

## 怎麼驗證／完整命令與輸出

實際命令：

```text
python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-88/rebuild
engine\啟動新版客戶端.cmd --check-only
```

check-only 經 `cmd.exe /d /c` 呼叫；建置及 check-only 的 cwd 都是上述新 worktree。各 dotnet 子命令 cwd 是該 worktree 的 `engine/`。以下 stdout 全文從原始檔載入，僅統一換行；stderr 為空者明列大小，非空者全文保留。時間採 UTC。

### build

命令、退出碼與時間：

```json
{
  "argv": [
    "python",
    "-B",
    "-X",
    "utf8",
    "engine/_integration/dev/rebuild.py",
    "--build",
    "--write-receipt",
    "--output",
    "scratch/issue-88/rebuild"
  ],
  "cwd": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88",
  "startedAt": "2026-11-05T22:03:36.401446+00:00",
  "finishedAt": "2026-11-05T22:03:53.981969+00:00",
  "exitCode": 0
}
```

完整 stdout：

```text
{"status": "dev-inputs-verified", "sourceFiles": 190, "pinnedFiles": 199, "writes": false}
sdk 0
product-restore 0
product-build 0
launcher-restore 0
launcher-build 0
receipt written D:\lineage開發\TW1608262200研究\scratch\wt\task-88\engine\_integration\dev\receipt.json
PASS dev {"productDllSha256": "72fceea19183f74aa8222e4680e918a1894a3e2dda8e124445202ae2ed4d8a48", "audioDllSha256": "db4afa21d6ba2070c918568523f9c3f4341ccc755d8c400d44ce17b75fb9fcdc", "productManifestSha256": "78d4d560dc61f7147ca3da21f3d4dc52ebaf10f1024bfc77d484bde8a5d7766f", "launcherDllSha256": "9efc3daca5852b2a87b14e2ca76e89b3af85fb1f4a1adfb3218fce2ae8d480a3"}
```

stderr：空（0 bytes）。

### sdk

命令、退出碼：

```json
{
  "name": "sdk",
  "argv": [
    "dotnet",
    "--version"
  ],
  "exitCode": 0
}
```

完整 stdout：

```text
10.0.300
```

stderr：空（0 bytes）。

### product-restore

命令、退出碼：

```json
{
  "name": "product-restore",
  "argv": [
    "dotnet",
    "restore",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\source\\OfflineClient.csproj",
    "--configfile",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\NuGet.Config"
  ],
  "exitCode": 0
}
```

完整 stdout：

```text
  正在判斷要還原的專案...
  已還原 D:\lineage開發\TW1608262200研究\scratch\wt\task-88\engine\audio\P24.Audio.csproj (103 毫秒 內)。
  已還原 D:\lineage開發\TW1608262200研究\scratch\wt\task-88\engine\source\OfflineClient.csproj (858 毫秒 內)。
```

完整 stderr：

```text

歡迎使用 .NET 10.0!
---------------------
SDK 版本: 10.0.300

----------------
編寫您的第一個應用程式: https://aka.ms/dotnet-hello-world
了解新功能: https://aka.ms/dotnet-whats-new
探索文件: https://aka.ms/dotnet-docs
在 GitHub 上報告問題和尋找來源: https://github.com/dotnet/core
使用 'dotnet --help' 查看可用的命令或瀏覽: https://aka.ms/dotnet-cli
--------------------------------------------------------------------------------------
```

### product-build

命令、退出碼：

```json
{
  "name": "product-build",
  "argv": [
    "dotnet",
    "build",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\source\\OfflineClient.csproj",
    "-c",
    "Release",
    "--no-restore",
    "-o",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\build\\client-product-dev",
    "--nologo",
    "-p:NuGetAudit=false",
    "-p:UseSharedCompilation=false"
  ],
  "exitCode": 0
}
```

完整 stdout：

```text
  P24.Audio -> D:\lineage開發\TW1608262200研究\scratch\wt\task-88\engine\build\client-product-dev\P24.Audio.dll
  OfflineClient -> D:\lineage開發\TW1608262200研究\scratch\wt\task-88\engine\build\client-product-dev\P11.ClientEngine.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:08.45
```

stderr：空（0 bytes）。

### launcher-restore

命令、退出碼：

```json
{
  "name": "launcher-restore",
  "argv": [
    "dotnet",
    "restore",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\_integration\\dev\\launcher-source\\Launcher.csproj",
    "--configfile",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\NuGet.Config"
  ],
  "exitCode": 0
}
```

完整 stdout：

```text
  正在判斷要還原的專案...
  已還原 D:\lineage開發\TW1608262200研究\scratch\wt\task-88\engine\_integration\dev\launcher-source\Launcher.csproj (80 毫秒 內)。
```

stderr：空（0 bytes）。

### launcher-build

命令、退出碼：

```json
{
  "name": "launcher-build",
  "argv": [
    "dotnet",
    "build",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\_integration\\dev\\launcher-source\\Launcher.csproj",
    "-c",
    "Release",
    "--no-restore",
    "--nologo",
    "-p:NuGetAudit=false",
    "-p:UseSharedCompilation=false",
    "-o",
    "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88\\engine\\launcher"
  ],
  "exitCode": 0
}
```

完整 stdout：

```text
  Launcher -> D:\lineage開發\TW1608262200研究\scratch\wt\task-88\engine\launcher\P24.Launcher.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:02.85
```

stderr：空（0 bytes）。

### check-only

命令、退出碼與時間：

```json
{
  "argv": [
    "cmd.exe",
    "/d",
    "/c",
    "engine\\啟動新版客戶端.cmd",
    "--check-only"
  ],
  "cwd": "D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-88",
  "startedAt": "2026-11-05T22:03:53.981969+00:00",
  "finishedAt": "2026-11-05T22:04:21.205199+00:00",
  "exitCode": 0
}
```

完整 stdout／check-only JSON 全文：

```json
{
  "status": "checked-only",
  "revision": "dev",
  "executable": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-88\\engine\\build\\client-product-dev\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-88\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-88\\engine\\build\\product-dev-manifest.json",
  "manifestSha256": "78d4d560dc61f7147ca3da21f3d4dc52ebaf10f1024bfc77d484bde8a5d7766f",
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

stderr：空（0 bytes）。

## SHA-256 比較與證據交叉核對

| 項目 | P11.ClientEngine.dll SHA-256 |
|---|---|
| 本次全新 worktree dev 建置 | `72fceea19183f74aa8222e4680e918a1894a3e2dda8e124445202ae2ed4d8a48` |
| 主專案最近 dev 建置（#77 第 2a 輪） | `44eb5843f25833f2da1cdf7081eabeb7b8cd6e27562423e2d06b150ac787d477` |

**不同**，如實記錄，依本任務規定不算失敗。主目錄 DLL 本輪前後均實際讀取 SHA，與 #77 第 2a 輪報告相符；未重建或覆寫它。#77 第 2a 輪來源提交是 `0b747de4e230c72906c830be821de322894acbf2`，本次 Git 來源提交是 `<SRC>`，兩者提交號不同。下列產品來源／建置輸入差異查詢 exit 0，stdout 空：

```text
git diff --name-only 0b747de4e230c72906c830be821de322894acbf2 HEAD -- engine/source engine/audio engine/web engine/_integration/dev engine/global.json engine/NuGet.Config engine/build-product.ps1 engine/launch-product.ps1 engine/啟動新版客戶端.cmd
```

因此產品來源及指定建置輸入沒有差異；尚未歸因 DLL 位元差異，也未以路徑等假設作已驗證結論。位元可重現屬 M3 T006，本次不擴充或修改建置設定。

驗證腳本已確認實際 DLL SHA 與 receipt／result 一致，manifest SHA 與 receipt／check-only 一致；check-only 核對 46 個 runtime／web／索引檔及 13,957 個素材存在／大小。dev lock 建置前後 SHA 均為 `855863f1fc073b1bbc9a9adcc5ddde342e27bedbd04dd9d2715e71f985e9f802`，與 #77 第 2a 輪相同。

### 本機產物身分

| 路徑（相對新 worktree） | bytes | SHA-256 |
|---|---:|---|
| `engine/build/client-product-dev/P11.ClientEngine.dll` | 4994560 | `72fceea19183f74aa8222e4680e918a1894a3e2dda8e124445202ae2ed4d8a48` |
| `engine/build/product-dev-manifest.json` | 7953 | `78d4d560dc61f7147ca3da21f3d4dc52ebaf10f1024bfc77d484bde8a5d7766f` |
| `engine/launcher/P24.Launcher.dll` | 24064 | `9efc3daca5852b2a87b14e2ca76e89b3af85fb1f4a1adfb3218fce2ae8d480a3` |
| `engine/_integration/dev/receipt.json` | 474 | `1ac0baa684dcdc4e7624d15660d74895ebc49fb7873aa9a9345f3afa86c91d5a` |
| `engine/_integration/dev/rebuild-lock.json` | 28757 | `855863f1fc073b1bbc9a9adcc5ddde342e27bedbd04dd9d2715e71f985e9f802` |

### Receipt 全文

```json
{
  "schema": "dev-receipt-v1",
  "builtAt": "2026-11-05T22:03:53.943072+00:00",
  "sdk": "10.0.300",
  "productDllSha256": "72fceea19183f74aa8222e4680e918a1894a3e2dda8e124445202ae2ed4d8a48",
  "audioDllSha256": "db4afa21d6ba2070c918568523f9c3f4341ccc755d8c400d44ce17b75fb9fcdc",
  "productManifestSha256": "78d4d560dc61f7147ca3da21f3d4dc52ebaf10f1024bfc77d484bde8a5d7766f",
  "launcherDllSha256": "9efc3daca5852b2a87b14e2ca76e89b3af85fb1f4a1adfb3218fce2ae8d480a3"
}
```

### 建置 result.json 全文

```json
{
  "status": "pass-dev-build",
  "sourceFiles": 190,
  "GUI": false,
  "network": false,
  "database": false,
  "productDllSha256": "72fceea19183f74aa8222e4680e918a1894a3e2dda8e124445202ae2ed4d8a48",
  "audioDllSha256": "db4afa21d6ba2070c918568523f9c3f4341ccc755d8c400d44ce17b75fb9fcdc",
  "productManifestSha256": "78d4d560dc61f7147ca3da21f3d4dc52ebaf10f1024bfc77d484bde8a5d7766f",
  "launcherDllSha256": "9efc3daca5852b2a87b14e2ca76e89b3af85fb1f4a1adfb3218fce2ae8d480a3"
}
```

### 證據交叉核對結果

```json
{
  "status": "pass",
  "source": "4021caf0b6b725d1d3973c33636aa0cad27685f9",
  "checkOnlyFieldsVerified": true,
  "receiptResultAndFilesMatch": true,
  "mainDllSha256": "44eb5843f25833f2da1cdf7081eabeb7b8cd6e27562423e2d06b150ac787d477",
  "worktreeDllSha256": "72fceea19183f74aa8222e4680e918a1894a3e2dda8e124445202ae2ed4d8a48",
  "sameDll": false
}
```

## 未驗證什麼／推測界線

- 未啟動遊戲、P24Lab、MySQL 或任何伺服器，未登入、送出聊天、附加或停止既有程序，未連正式服務或 DB；沒有讀機密設定。
- check-only 不是原生畫面、遊戲流程或素材內容全面驗收。素材本體僅核對存在與大小；內容雜湊按既有契約由產品載入時檢查。本次沒有新增原生實機證據。
- 沒有重跑其他功能測試或新一輪 CI；#67 的既有 CI 結果不冒充本次測試。
- 沒有修改產品程式、lock、docs/REPORT.md、封存 release、原客戶端或硬連結。主目錄未 reset、stash、刪檔、切換分支。
- DLL 差異的原因尚未驗證；不影響本次 T017 指定的 build／check-only 驗收。

## 產出路徑與提交範圍

- **唯一提交文件：`docs/reports/issue-88.md`**（本報告）。建置前 worktree 無追蹤檔變更；建置後只新增未追蹤 receipt，產品來源與 lock 的 `git diff --name-only` 為空。receipt 留本機，僅明確 `git add docs/reports/issue-88.md`。
- 所有下列路徑均相對 `D:\lineage開發\TW1608262200研究\scratch\wt\task-88`。
- 本機原始證據：`scratch/issue-88/build.command.json`、`build.stdout`、`build.stderr`；`check-only.command.json`、`check-only.stdout`、`check-only.stderr`。
- 子步驟完整 argv／exitCode／stdout／stderr：`scratch/issue-88/rebuild/`，含 `result.json`。
- 本機前置及交叉核對：`scratch/issue-88/environment.json`、`baseline.json`、`validation.json`；一次性腳本 `run.py`、`report.py` 留在同目錄。
- 建置產物：`engine/build/client-product-dev/`、`engine/build/product-dev-manifest.json`、`engine/launcher/`、專案 `obj/`／`bin/`、`engine/_integration/dev/receipt.json`；不納入提交。完整必要輸出已內嵌報告，不依賴 scratch 保留才能審閱。

## 給下一棒的提示

請調度器／Claude 審查 `task/88` 的單一報告差異及來源祖先關係，依既定流程交使用者合併，再由主控更新 T017／M0 待驗證狀態；本任務不修改 TASKS.md 或 docs/REPORT.md。主目錄仍是 `task/77@50d09756722961827411d2cca5d7900a1cf75f3b`，#77 第 2a 輪 DLL 未變，後續 #77 可繼續使用原有主專案產物。DLL 位元差異留 M3 T006 處理，不要求為本報告重建主目錄或切換入口。
