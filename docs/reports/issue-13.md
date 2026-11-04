# Issue #13 — T015 本機重跑 r35 完整建置與 check-only

階段：test；第 2 次嘗試（審查退回補件）；日期：2026-11-04；分支：`task/13`。

## 結論

**驗證通過**。r35 完整建置與 `engine\啟動新版客戶端.cmd --check-only` 皆退出 **0**。主程式與 launcher 編譯各為 0 警告、0 錯誤；check-only 實際回傳 `status: checked-only`、`revision: r35`、`uiStarted: false`。T014 修正後，最終 manifest 核驗不再拒絕透過 junction 引用的 `assets/integrity.json`。

## 做了什麼

- 已讀根目錄 AGENTS.md、CLAUDE.md、TASKS.md、docs/REPORT.md 及 Issue #7、#12 報告。TASKS.md 尚無 T015，依本次 Issue #13 指派執行。
- 第 1 次嘗試保留既有 `task/13` worktree，未 checkout、建分支或 reset。當時 `git fetch origin` 退出 0；建置時 HEAD 與 origin/main 相同，包含 T014 的 PR #14 合併。
- 第 1 次嘗試新增本報告；第 2 次依退回意見補齊 `docs/REPORT.md` 的 T015／Issue #13 結果、報告連結與下一步，並同步修正本報告的交付說明。引擎來源、lock 與入口均未修改。
- worktree 原無 `engine/assets` 與 `engine/vendor`：在 worktree 新建 `engine/assets` junction，指向原專案的 `engine/assets`，供本次驗證唯讀存取。原專案既有 junction 與封存內容未修改；未複製 pak/map/sprite。
- 將原專案的離線 `microsoft.web.webview2.1.0.4258.31.nupkg` 複製到 worktree 的 `engine/vendor/`。保留原 NuGet.Config；未改用線上套件來源，未安裝軟體。
- 沙箱初始化回報 `helper_unknown_error: setup refresh had errors`，改用獲准的提升權限命令。PATH 在單次命令中加入既有 Python312；未修改全域環境。
- TEMP/TMP、DOTNET_CLI_HOME、NuGet HTTP cache 均設在 worktree 的 `scratch/issue-13/`；NUGET_PACKAGES 設為 worktree 的 `engine/.nuget`。rebuild.py 自行使用 `scratch/issue-13/rebuild/tmp`、`.dotnet` 與 NuGet scratch。
- 產物依既有建置腳本留在此 worktree 的 build/、obj/、bin/、engine/launcher/ 及 r35 receipt 路徑；沒有對原專案的來源、封存、素材或硬連結寫入。Git fetch/commit/push 使用 worktree 共用的 Git 中繼資料。

工作目錄：`D:\lineage開發\TW1608262200研究\scratch\wt\task-13`。

建置來源 commit：`e32fe7bcce38ddc03f7151fd7278190263078155`。

建置開始：2026-11-04T18:31:03.378903+08:00；建置結束：2026-11-04T18:31:20.902733+08:00；check-only 完成：2026-11-04T18:32:04.092257+08:00。

## 第 2 次補件核對

本次退回原因為文件交付缺漏；前次審查已核對建置證據通過。本次沿用第 1 次嘗試的實際建置與 check-only 結果，未再次執行建置或 check-only；下列命令、時間、JSON 與產物清單皆屬第 1 次執行。

- `git fetch origin` 退出 0；補件前 HEAD／origin/task/13 均為 `92d45b81e9b98f8efcb86aa6418cad1c36b26486`，origin/main 仍為建置來源 `e32fe7bcce38ddc03f7151fd7278190263078155`。兩者差異僅為本報告，建置輸入沒有變更。
- 重新唯讀核對報告中的 27 筆大小／SHA-256 紀錄，全部吻合；check-only、receipt、result 三份 JSON 與本機原檔全文一致（僅統一換行及結尾空白）。頂層建置、check-only 與五個建置子命令的既有退出碼皆為 0。
- 已補齊 `docs/REPORT.md` 的 T015 結果與報告連結，將 Issue #7 的 junction 阻塞／待重跑敘述更新為 T014 修正、T015 建置與 check-only 通過；保留根入口仍為 r33 與原生實機驗收待使用者執行的界線。
- 本次只提交兩份文件；receipt 與二進位保留本機。未將 T014 PR #14 的 CI 結果算作 task/13 的 CI。

## 怎麼驗證／已驗證

實際命令：

```text
python -B -X utf8 engine/_integration/r35/rebuild.py --build --write-receipt --output scratch/issue-13/rebuild
engine\啟動新版客戶端.cmd --check-only
```

建置核驗涵蓋 181 個來源檔、190 個鎖定輸入。check-only 核對 46 個 runtime／web／素材索引檔案的大小與 SHA-256，並核對 13957 個素材檔案的存在與大小。

| 命令／階段 | 退出碼 |
|---|---:|
| r35 完整建置 | 0 |
| sdk | 0 |
| product-restore | 0 |
| product-build | 0 |
| launcher-restore | 0 |
| launcher-build | 0 |
| launcher check-only | 0 |

### 完整建置 stdout

```text
sdk 0
product-restore 0
product-build 0
launcher-restore 0
launcher-build 0
receipt written D:\lineage開發\TW1608262200研究\scratch\wt\task-13\engine\_integration\r35\receipt.json
PASS r35 {"productDllSha256": "dfa7d804705398d00cff7a955151824b06302f9081f6946b900bb332d4977737", "audioDllSha256": "2e1ba196373e5d9927e5a5f3ba5aaea818e4f62b970763d33425f89467f1038c", "productManifestSha256": "eaac44a43e6c06e5e544df0e1503d58a72c829ccd33e5345c56d858e0a03df5c", "launcherDllSha256": "f4e4a27094e2a4d4b759f99b122e8a2617f9207cfa8408a71a3107d3b2e1f731"}
```

完整建置 stderr：空（0 bytes）。子命令 product-restore stderr 為 .NET 首次使用歡迎訊息（499 bytes），其餘子命令 stderr 均為空；完整原始紀錄保留於 scratch。

### product-build stdout

```text
  P24.Audio -> D:\lineage開發\TW1608262200研究\scratch\wt\task-13\engine\build\client-product-r35\P24.Audio.dll
  OfflineClient -> D:\lineage開發\TW1608262200研究\scratch\wt\task-13\engine\build\client-product-r35\P11.ClientEngine.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:08.81
```

### launcher-build stdout

```text
  Launcher -> D:\lineage開發\TW1608262200研究\scratch\wt\task-13\engine\launcher\P24.Launcher.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:02.86
```

### check-only 完整 JSON

以下為 stdout 全文，保留原 JSON 跳脫字元，僅統一換行；stderr 為空（0 bytes）。

```json
{
  "status": "checked-only",
  "revision": "r35",
  "executable": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-13\\engine\\build\\client-product-r35\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-13\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-13\\engine\\build\\product-r35-manifest.json",
  "manifestSha256": "eaac44a43e6c06e5e544df0e1503d58a72c829ccd33e5345c56d858e0a03df5c",
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

## Receipt 與來源識別

Receipt 已產生且最終 PASS r35 成功；其 manifest SHA-256 與 check-only 回傳值一致。

| 路徑 | 大小（bytes） | SHA-256 |
|---|---:|---|
| `engine/_integration/r35/receipt.json` | 474 | `067b43cf3ad1075add84672b687e926e615a0dfa18176537b50a38c6e08aba55` |
| `engine/_integration/r35/rebuild.py` | 8890 | `24417d03d3ba264c9e4ef1624e4cfe7ddef223b17485645e7f97a454a8cb560d` |
| `engine/_integration/r35/rebuild-lock.json` | 27475 | `36b3e6991a69d5b06b8978022af61a8593dc5da41de84ecc50b52a1249e6794d` |
| `engine/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` |
| `engine/assets/integrity.json` | 1729333 | `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98` |

Receipt 完整內容：

```json
{
  "schema": "r35-receipt-v1",
  "builtAt": "2026-11-04T10:31:20.849326+00:00",
  "sdk": "10.0.300",
  "productDllSha256": "dfa7d804705398d00cff7a955151824b06302f9081f6946b900bb332d4977737",
  "audioDllSha256": "2e1ba196373e5d9927e5a5f3ba5aaea818e4f62b970763d33425f89467f1038c",
  "productManifestSha256": "eaac44a43e6c06e5e544df0e1503d58a72c829ccd33e5345c56d858e0a03df5c",
  "launcherDllSha256": "f4e4a27094e2a4d4b759f99b122e8a2617f9207cfa8408a71a3107d3b2e1f731"
}
```

建置 result.json：

```json
{
  "status": "pass-r35-build",
  "sourceFiles": 181,
  "GUI": false,
  "network": false,
  "database": false,
  "productDllSha256": "dfa7d804705398d00cff7a955151824b06302f9081f6946b900bb332d4977737",
  "audioDllSha256": "2e1ba196373e5d9927e5a5f3ba5aaea818e4f62b970763d33425f89467f1038c",
  "productManifestSha256": "eaac44a43e6c06e5e544df0e1503d58a72c829ccd33e5345c56d858e0a03df5c",
  "launcherDllSha256": "f4e4a27094e2a4d4b759f99b122e8a2617f9207cfa8408a71a3107d3b2e1f731"
}
```

## 產物清單

以下列出本次產品輸出、launcher、manifest 與 receipt；所有檔案均位於本 worktree。只提交報告；二進位、receipt 與 scratch 證據保留本機。

| 路徑 | 大小（bytes） | SHA-256 |
|---|---:|---|
| `engine/build/client-product-r35/Microsoft.Web.WebView2.Core.dll` | 698208 | `d60e6e94245078cb56e44ebd5f360dc04bf18e460136be93bfc4cfca43b98505` |
| `engine/build/client-product-r35/Microsoft.Web.WebView2.Core.xml` | 612031 | `8684cd74000c77533e563785208308ada63808da85825206856cab11a9eeffce` |
| `engine/build/client-product-r35/Microsoft.Web.WebView2.WinForms.dll` | 39264 | `84b5772260dbdfe95591197edc9315dcd2cc86ad610e4b14ddd5e4291e31a381` |
| `engine/build/client-product-r35/Microsoft.Web.WebView2.WinForms.xml` | 41869 | `6a8ec199373824fdebceda7b3d780d0b31c82edceae04d92391027c971dea6c2` |
| `engine/build/client-product-r35/P11.ClientEngine.deps.json` | 2750 | `9a336a6f36c706c5f9dc0c586e90e1f87502d21a24b747bc32bce85eb99d8d37` |
| `engine/build/client-product-r35/P11.ClientEngine.dll` | 4925440 | `dfa7d804705398d00cff7a955151824b06302f9081f6946b900bb332d4977737` |
| `engine/build/client-product-r35/P11.ClientEngine.exe` | 162304 | `433a94457d8ce67b4984f940200224ed8a22de9e8be2a070e327903a25f42a20` |
| `engine/build/client-product-r35/P11.ClientEngine.pdb` | 358404 | `4698b6c6c55d3071748910891a51fb0b82f5fac1684e6081f8937068c87b8203` |
| `engine/build/client-product-r35/P11.ClientEngine.runtimeconfig.json` | 519 | `96bc2946dff8790400d652ec7f8de7bb043e954980d59470c112788b68a1fbd3` |
| `engine/build/client-product-r35/P24.Audio.deps.json` | 421 | `b9a05c9915f7c361f595f0cd4bebf0510902a603e1431ca435468842597d03b5` |
| `engine/build/client-product-r35/P24.Audio.dll` | 53760 | `2e1ba196373e5d9927e5a5f3ba5aaea818e4f62b970763d33425f89467f1038c` |
| `engine/build/client-product-r35/P24.Audio.pdb` | 21624 | `3a635ac8a56ffa9d9d2606548375a3a61194a64ddb9edc22e6de979ef42b92db` |
| `engine/build/client-product-r35/runtimes/win-arm64/native/WebView2Loader.dll` | 180064 | `4f48cb32d34298c86cca2108057eea0d900b5e422575e926e8086318a0b9cbf0` |
| `engine/build/client-product-r35/runtimes/win-x64/native/WebView2Loader.dll` | 194912 | `3426dcc55fdfb8b5e7ac623cf33b4ccf283fbf68a0c5a65bddfdb4d749a8abee` |
| `engine/build/client-product-r35/runtimes/win-x86/native/WebView2Loader.dll` | 155488 | `87c61e2cae380112c60853e28178b8ea9a0d63739fc03cead680d4ad3fde6b7d` |
| `engine/launcher/P24.Launcher.deps.json` | 430 | `1cc0f476da7833ffe9258f8ac190eb56d167d517c1004acc98c2e054f168e90a` |
| `engine/launcher/P24.Launcher.dll` | 24064 | `f4e4a27094e2a4d4b759f99b122e8a2617f9207cfa8408a71a3107d3b2e1f731` |
| `engine/launcher/P24.Launcher.exe` | 162304 | `cc076e2093efe555cc2be088a056c780ebe934f391215c772202ea7ca45042c5` |
| `engine/launcher/P24.Launcher.pdb` | 13488 | `c301ae814b950ca85c31a796d6625af39fc80ae5bf08fd4af47058be4c2d429c` |
| `engine/launcher/P24.Launcher.runtimeconfig.json` | 399 | `3e965cd7cfd553c2ff4e842d8d8019aaae2da0a21d92ff85a87573dd8d1b95c6` |
| `engine/build/product-r35-manifest.json` | 7953 | `eaac44a43e6c06e5e544df0e1503d58a72c829ccd33e5345c56d858e0a03df5c` |
| `engine/_integration/r35/receipt.json` | 474 | `067b43cf3ad1075add84672b687e926e615a0dfa18176537b50a38c6e08aba55` |

## 未驗證什麼／推測／待驗證

- 未啟動遊戲、登入、執行原生畫面或遊戲流程驗收；未連正式服務或 DB、查詢機密設定、啟停服務、附加或停止既有遊戲程序。check-only 回傳 uiStarted/networkStarted/servicesStarted 均為 false。
- 素材本體僅檢查存在與大小，未逐檔重算內容 SHA-256；本體雜湊依既有契約由遊戲載入時核驗。本次不能視為素材內容或遊戲功能全面驗收。
- 未執行指向 r33 的根目錄 `啟動引擎.cmd`；本次使用 Issue 指定的 engine 入口，回傳明確為 r35。
- 未進行第二次重建以比較位元一致性，未驗證本次推送後的 CI。T014 的 junction 修正由本次實際完整建置驗證通過；無額外推測。

## 產出路徑

- `docs/reports/issue-13.md`：完整建置證據與第 2 次補件紀錄，納入提交。
- `docs/REPORT.md`：T015／Issue #13 結果索引、報告連結及更新後的下一步，納入提交。
- `scratch/issue-13/build-command.json`、`build.stdout`、`build.stderr`：頂層建置命令、時間、退出碼與完整輸出。
- `scratch/issue-13/check-only-command.json`、`check-only.stdout`、`check-only.stderr`：check-only 命令、退出碼及完整 JSON。
- `scratch/issue-13/rebuild/`：SDK／restore／build 子命令 argv、退出碼、stdout／stderr 與 result.json。
- `scratch/issue-13/artifacts.json`：產物路徑、大小、SHA-256 清單。
- `engine/_integration/r35/receipt.json`、`engine/build/product-r35-manifest.json`、`engine/build/client-product-r35/`、`engine/launcher/`：實際本機產物。receipt 為未追蹤檔案，刻意不納入提交。

## 給下一棒的提示

由調度器／Claude 審閱 `task/13` 的本報告與已更新的 `docs/REPORT.md`，確認第 2 次補件符合 T015 驗收，再交使用者合併。本輪已通過完整建置與 r35 check-only，可接續安排 M1／T011 的原生實機驗收，由使用者依既有 N01 核對表執行；不要將本次 checked-only 當成登入、畫面或遊戲流程通過。測試產物仍在 task/13 worktree；合併報告不會搬移二進位產物或切換正式入口，且素材引用仍依賴原專案的本機 assets。
