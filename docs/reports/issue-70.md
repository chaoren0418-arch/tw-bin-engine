# Issue #70 — DEMO-1：同步 main、建置 dev 與展示頁骨架

## 做了什麼／已驗證

2026-11-06（Asia/Taipei），test，第 1 次嘗試。已完成主專案同步、dev 建置與 check-only，兩者退出碼均為 0；主程式與 launcher 各 0 警告、0 錯誤。check-only 回傳 `revision: dev`、`uiStarted: false`、`networkStarted: false`、`servicesStarted: false`。展示頁 18 個功能狀態全部保留「待 DEMO-2」。

先讀根目錄 AGENTS.md、docs/REPORT.md 與 TASKS.md；初始分支 task/50，追蹤檔無變更。PR #66 已於 `2026-11-05T18:50:00Z` 合併，merge commit 為 `465489c604463468f9481a1ce8c5064c209709bd`。本機 main 由 `6c86710a175ca3192730cc1b7750ae20e569ec3a` 快轉至下列來源 HEAD，再依派工建立 task/70。

- 建置來源 HEAD、本次 fetch 的 origin/main、快轉後本機 main：`71ebe7eddf2613f20b86b1f009745ccf7c27ea8a`。
- 包含 #50 的 `eab1c7d` 與 #51／PR #66 的合併 commit；兩個祖先檢查均退出 0。
- `engine/source/CombatSfx.cs`、`GroundCatalog.cs`、`PacketTrace.cs` 均存在，SHA-256 見下方。
- 文件提交位於建置來源之後；最終工作分支為 `task/70`，產品輸入與上述 main 相同。
- 建置時間：`2026-11-06 02:57:03 +08:00`；receipt SHA-256：`425b11569872a999aa03e7d623d4f68ea5b3556e214ed00c777a02f5d017cd84`。

本輪只新增三份任務文件，未手動修改 docs/REPORT.md、產品程式或 lock。main 快轉帶入的既有檔案不算本次新增修改。未 reset、stash、刪檔，未啟動遊戲、P24Lab、DB 或正式服務；未讀取機密設定、修改封存 release、硬連結或素材。

## 指令與退出碼

| 指令／檢查 | 退出碼／結果 |
|---|---|
| `git status --porcelain --untracked-files=no` | 0；初始及建置後皆無輸出 |
| `git fetch origin` | 0 |
| `gh pr view 66 --repo chaoren0418-arch/tw-bin-engine --json state,mergedAt,mergeCommit` | 0；MERGED |
| `git checkout main` | 0 |
| `git merge --ff-only origin/main` | 0；fast-forward |
| `git checkout -B task/70 origin/main` | 0 |
| `git merge-base --is-ancestor eab1c7d HEAD` | 0 |
| `git merge-base --is-ancestor 465489c604463468f9481a1ce8c5064c209709bd HEAD` | 0 |
| `python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/demo-1/rebuild` | 0 |
| sdk／product-restore／product-build／launcher-restore／launcher-build | 各 0 |
| `engine\啟動新版客戶端.cmd --check-only`（經 `cmd.exe /d /c`） | 0 |
| JSON 欄位斷言 | PASS；status、revision、四個 false 欄位均正確 |

一般命令工具與 Node 工具因 Windows sandbox 初始化錯誤 `helper_unknown_error: setup refresh had errors` 未能建立程序；改用執行器核准的本機執行模式。`rg` 未安裝，改用 git ls-files 與 PowerShell 查找，未安裝新工具。原 PATH 無 python，以既有 Python 3.12 補入本次程序 PATH；`NUGET_PACKAGES` 限定 `engine/.nuget`。rebuild 自行把 TEMP／TMP／DOTNET_CLI_HOME／NUGET_SCRATCH 指向 scratch 證據樹。沒有改全域環境設定。

建置前檢查既定輸出、receipt 與 obj／bin，共 106 個既有路徑，未發現 reparse point 或多重硬連結。assets 只沿用既有唯讀 junction。

## 建置結果 JSON

```json
{
  "status": "pass-dev-build",
  "sourceFiles": 190,
  "GUI": false,
  "network": false,
  "database": false,
  "productDllSha256": "4b1e5ac8841edaf7963f45154a97bec2611a678bbffc66b5192c635f1176d0e5",
  "audioDllSha256": "a727b2554872293a5411ea1f40456ff4d4e19449bce94e0eae1f7d787538627b",
  "productManifestSha256": "5de2fcf6050b232df023bd31d168926c08c694e1b630ac0cb02a7dab0e599add",
  "launcherDllSha256": "3f1cb10ead35e79414f36606ddda0f9d2317555f0d124ba18697a1b59083a477"
}
```

建置標準輸出以 `PASS dev` 開頭的最後一行列出上述四個產物 SHA-256。每個子命令的 argv、退出碼與 stdout／stderr 均保存在 scratch 證據目錄。

## check-only 完整 JSON

```json
{
  "status": "checked-only",
  "revision": "dev",
  "executable": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\client-product-dev\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\product-dev-manifest.json",
  "manifestSha256": "5de2fcf6050b232df023bd31d168926c08c694e1b630ac0cb02a7dab0e599add",
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

本次核對 46 個 runtime／web 檔案雜湊與 13,957 個素材的存在及大小；素材本體雜湊依既有契約在產品載入時驗證，本輪沒有逐個重算。

## Receipt 完整內容

```json
{
  "schema": "dev-receipt-v1",
  "builtAt": "2026-11-05T18:57:03.804670+00:00",
  "sdk": "10.0.300",
  "productDllSha256": "4b1e5ac8841edaf7963f45154a97bec2611a678bbffc66b5192c635f1176d0e5",
  "audioDllSha256": "a727b2554872293a5411ea1f40456ff4d4e19449bce94e0eae1f7d787538627b",
  "productManifestSha256": "5de2fcf6050b232df023bd31d168926c08c694e1b630ac0cb02a7dab0e599add",
  "launcherDllSha256": "3f1cb10ead35e79414f36606ddda0f9d2317555f0d124ba18697a1b59083a477"
}
```

## 檔案身分（SHA-256；大小為 bytes）

```json
[
    {
        "path":  "engine/_integration/dev/receipt.json",
        "size":  474,
        "sha256":  "425b11569872a999aa03e7d623d4f68ea5b3556e214ed00c777a02f5d017cd84"
    },
    {
        "path":  "engine/_integration/dev/rebuild-lock.json",
        "size":  28757,
        "sha256":  "a824260c6da50a92e938bb2059a76a7a1672c250eea3551ba8b695a89f0fb1db"
    },
    {
        "path":  "engine/source/CombatSfx.cs",
        "size":  8329,
        "sha256":  "04bb9e986f9ca48ca345faad098b02581184086e45ce9771a8bde60c538553d4"
    },
    {
        "path":  "engine/source/GroundCatalog.cs",
        "size":  4169,
        "sha256":  "13221808cef3b6f06cf5bc00e1b6ad9e9f475ac40a8326236e5b1cece99df465"
    },
    {
        "path":  "engine/source/PacketTrace.cs",
        "size":  5670,
        "sha256":  "f5b8b0f0af0a455bd75db265cb3995dd2508a095c8581f0a6449f5fbd68f19dd"
    },
    {
        "path":  "engine/build/client-product-dev/P11.ClientEngine.dll",
        "size":  4993536,
        "sha256":  "4b1e5ac8841edaf7963f45154a97bec2611a678bbffc66b5192c635f1176d0e5"
    },
    {
        "path":  "engine/build/client-product-dev/P24.Audio.dll",
        "size":  66048,
        "sha256":  "a727b2554872293a5411ea1f40456ff4d4e19449bce94e0eae1f7d787538627b"
    },
    {
        "path":  "engine/build/product-dev-manifest.json",
        "size":  7953,
        "sha256":  "5de2fcf6050b232df023bd31d168926c08c694e1b630ac0cb02a7dab0e599add"
    },
    {
        "path":  "engine/launcher/P24.Launcher.dll",
        "size":  24064,
        "sha256":  "3f1cb10ead35e79414f36606ddda0f9d2317555f0d124ba18697a1b59083a477"
    },
    {
        "path":  "engine/launcher/P24.Launcher.exe",
        "size":  162304,
        "sha256":  "5145b3c65be93ed89e3c9b05cf1f03e52d6276daf6a59b9de8d4f9bb86717b1f"
    }
]
```

文件驗證已核對三份文件的相對連結、18 列待驗狀態、來源 HEAD、receipt 與產物雜湊；皆通過。暫存差異檢查曾發現引用 JSON 的 CRLF 混用，統一文件為 LF 後重驗。文件核對腳本位於 `scratch/demo-1/verify-docs.py`（不提交）。

## 產出路徑

- 提交：`docs/demo/README.md`、`docs/demo/RUN.md`、`docs/reports/issue-70.md`。
- 主程式：`engine/build/client-product-dev/`；manifest：`engine/build/product-dev-manifest.json`。
- 既定 launcher：`engine/launcher/`；receipt：`engine/_integration/dev/receipt.json`。
- 建置證據：`scratch/demo-1/build.stdout`、`build.stderr`、`build.command.json`。
- check-only 證據：`scratch/demo-1/check-only.json`、`check-only.stderr`、`check-only.command.json`。
- 雜湊：`scratch/demo-1/identities.json`。
- 子命令與建置結果：`scratch/demo-1/rebuild/*.command.json`、`*.stdout`、`*.stderr`、`result.json`。
- scratch 證據與建置產物不提交；本報告保留主要 JSON、退出碼與雜湊以供審閱。

## 未驗證什麼

未做登入、選角、移動、戰鬥、技能、道具／裝備、金幣丟撿、聊天、保存、音效或音量的真機驗收；沒有截圖或新遊戲錄影。check-only 通過不代表上述流程通過。RUN.md 的手動啟動指令僅供使用者／DEMO-2 依授權執行，本輪未執行；啟動文字根據 dev launcher 原始碼，畫面仍待驗證。未在另一台乾淨電腦驗證安裝流程；無推測性功能通過結論。

## 給下一棒的提示

1. 雲端主控／Claude 審查 task/70，依既定流程交使用者合併；DEMO-1 文件與建置前置已完成。
2. 由 dot 接手 DEMO-2，依其任務授權管理 P24Lab 與錄製真機流程；P24Lab 的啟停由 dot 執行，詳見 AGENTS.md。
3. 使用主專案 `engine\啟動新版客戶端.cmd` 的 dev 產物，先比對本報告 receipt／產物雜湊，再跑 check-only；若重建，更新展示首頁版本與 receipt 身分。
4. 本機 main 已快轉至上述建置 HEAD；工作目錄留在 task/70 供提交與推送，僅新增文件。dot 可用現有建置接棒，無需為本文件提交重建。
5. 將真機結果、截圖連結與限制填進 docs/demo/README.md；未驗證項維持待驗，不把本輪建置證據視為遊戲驗收。缺陷交雲端主控排入後續任務。
