# Issue #38 — T043 本機同步 main 並建置 dev

## 做了什麼／已驗證

2026-11-05，第 1 次任務嘗試。在 `D:\lineage開發\TW1608262200研究` 完成 main 快轉、dev 完整建置及入口 check-only。完整建置與 check-only 均 exit 0；主程式與 launcher 各 0 警告、0 錯誤。JSON 確認 `revision: dev`、`uiStarted: false`、`networkStarted: false`、`servicesStarted: false`。

已先讀 AGENTS.md、docs/REPORT.md。初始追蹤檔乾淨；未 reset、stash、刪檔，未修改產品程式、lock、docs/REPORT.md 或機密設定，未啟動遊戲、P24Lab、DB 或連正式服務。本次只提交本報告；main 快轉帶入的既有更新不屬於本次新增修改。

## HEAD 與指令紀錄

- 初始分支 `task/27`，HEAD `34674bc47ff458449d33baf135c48c8d2fc2a48f`。
- 本機 main 由 `20dedf6` 快轉至 `6c86710a175ca3192730cc1b7750ae20e569ec3a`。
- 建置來源 HEAD、本次 fetch 的 origin/main、同步後本機 main 均為 `6c86710a175ca3192730cc1b7750ae20e569ec3a`。
- 報告分支 `task/38` 建立於上述來源，報告 commit 是其後代且不更動建置輸入。

| 指令／檢查 | 退出碼／結果 |
|---|---|
| `git status --porcelain --untracked-files=no` | 0；無輸出 |
| `git fetch origin` | 0 |
| `git checkout main` | 0 |
| `git merge --ff-only origin/main` | 0；fast-forward |
| `git merge-base --is-ancestor 798bdf1 HEAD` | 0 |
| `Test-Path engine/_integration/dev/rebuild.py` | True |
| `Test-Path engine/source/PacketTrace.cs` | True |
| `git checkout -B task/38 origin/main` | 0 |
| `python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-38/rebuild` | 0 |
| SDK／product-restore／product-build／launcher-restore／launcher-build | 各 0 |
| `engine\啟動新版客戶端.cmd --check-only`（經 `cmd.exe /d /c`） | 0 |

一般 shell 初始化失敗，改用執行器核准的本機模式。預設 PATH 缺 Python，以 `py -0p` 找到既有 Python 3.12，僅在本次程序 PATH 加入 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312`。本次程序 `NUGET_PACKAGES` 指向專案內 `engine/.nuget`；rebuild 將 TEMP／TMP／DOTNET_CLI_HOME／NUGET_SCRATCH 限定於 scratch 輸出樹。未安裝軟體或更改全域設定。

兩次 check-only 呼叫包裝曾 exit 1：首次 PowerShell 管線中文字元轉碼，第二次 cmd 入口使用正斜線；均回報 `The system cannot find the path specified.`，無 JSON，未執行入口。改以 Unicode 路徑及 Windows 反斜線後成功。前兩次紀錄保存在 `scratch/issue-38/initial-check-only.*` 與 `path-retry-check-only.*`；以下為成功執行的完整 JSON。

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
  "manifestSha256": "f4e44a1ba5e702aebe696f5e12f0da91c52e3e7de4c6639faa32d58dc0798013",
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

## Receipt 與產物身分

以下由實際建置後檔案計算，大小單位為 bytes。

```json
[
  {
    "path": "engine/_integration/dev/receipt.json",
    "size": 474,
    "sha256": "33069335f27f203f086f95527c506f735982df360b637e01cff01d274dc87024"
  },
  {
    "path": "engine/launcher/P24.Launcher.exe",
    "size": 162304,
    "sha256": "2df57d80456be6f33e095ce6e4a757bd9cdd7b1d91fe6d7bc65b61f9a0eaf54a"
  },
  {
    "path": "engine/build/client-product-dev/P11.ClientEngine.dll",
    "size": 4934656,
    "sha256": "5c45ebe0bd0d779b3ca0769d701a1c10486773569da1e5462e441a5f42eed0ae"
  }
]
```

Receipt 完整內容：

```json
{
  "schema": "dev-receipt-v1",
  "builtAt": "2026-11-05T04:19:42.040156+00:00",
  "sdk": "10.0.300",
  "productDllSha256": "5c45ebe0bd0d779b3ca0769d701a1c10486773569da1e5462e441a5f42eed0ae",
  "audioDllSha256": "7d78540f7ea6d1ff42c16b6445cad963b5466a4742b6bf254c726a4e9f7985ed",
  "productManifestSha256": "f4e44a1ba5e702aebe696f5e12f0da91c52e3e7de4c6639faa32d58dc0798013",
  "launcherDllSha256": "2a9abc1d4da078f5cf1a736dcd3cea7963990520221ec64bf76f9249af74c14a"
}
```

## 產出路徑

- 報告：`docs/reports/issue-38.md`。
- 主程式：`engine/build/client-product-dev/`；manifest：`engine/build/product-dev-manifest.json`。
- 既定 launcher 輸出：`engine/launcher/`；receipt：`engine/_integration/dev/receipt.json`。
- 本機證據：`scratch/issue-38/build.log`、`build-exit.txt`、`check-only.json`、`check-only.stderr`、`check-only-exit.txt`、`identities.json`。
- 子命令／退出碼／stdout／stderr：`scratch/issue-38/rebuild/*.command.json`、`*.stdout`、`*.stderr`；彙整：`scratch/issue-38/rebuild/result.json`。
- 建置前檢查既有輸出與 obj，未發現 symlink、junction 或多重硬連結；素材沿用既有唯讀 assets junction，未複製或修改素材。

## 未驗證什麼／給下一棒的提示

- 本次完成 T043 前置；未做登入、選角、世界、移動、戰鬥、滑鼠畫面或封包追蹤真機驗收，check-only 不代表 T042 通過。
- check-only 核對 46 個 runtime／web 檔案雜湊，以及 13,957 個素材的存在與大小；素材本體雜湊依既有契約於產品載入時驗證，本次未逐個重算。
- 調度器／Claude 審查報告後，由 `dot` 依 Issue #37（T042）的授權與步驟接手離線真機補測。
- 本機 main 已同步至上述來源 HEAD，工作目錄留在 `task/38`（僅多本報告），產品輸入與 main 相同。dot 不需 fetch 或重建，可先核對本報告雜湊與 check-only。
- T042 使用主專案 `engine\啟動新版客戶端.cmd` 對應的 dev 產物；避免誤用根目錄舊入口。本次未變更正式入口。
