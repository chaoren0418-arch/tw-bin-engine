# Issue #7 — T013 本機以 r35 鎖定來源執行完整建置並回報

階段：test；第 1 次嘗試。開始：2026-11-04T13:27:24.432175+08:00；結束：2026-11-04T13:27:44.334909+08:00。

## 結論

**完整建置驗證未通過（退出碼 2）**。主程式與 launcher 編譯皆成功，各為 0 警告、0 錯誤；最終 manifest 核驗拒絕 `assets/integrity.json`。既有 `engine/assets` junction 解析到 `releases/r28/assets`，超出核驗器允許的 engine 根目錄。不是 SDK 缺口或編譯失敗。

## 做了什麼

- 先讀 AGENTS.md、CLAUDE.md、docs/REPORT.md；原工作樹乾淨。依序執行 git fetch origin、git checkout main、git reset --hard origin/main，各退出 0。同步基準：`db5f7109061476eb778aeac90eb9a2bd3e323788`，再建立 task/7。
- 使用既有 Python 3.12.10 與 .NET SDK 10.0.300，符合 engine/global.json 的版本與 rollForward: disable；未安裝軟體。
- 執行 r35 完整建置及 receipt 寫入；僅加 --output 將診斷輸出集中到 scratch，避免新增時間戳證據目錄。保留原離線 NuGet 設定。
- 建置後再次唯讀核驗來源，181 個來源檔／190 個 pinned files 全部通過。未修改來源、lock、啟動入口。
- 只提交本報告與 docs/REPORT.md。receipt、manifest、exe/dll 與 scratch 暫存均保留本機，不提交。

## 怎麼驗證／已驗證

工作目錄：`D:\lineage開發\TW1608262200研究`。實際命令：

```text
python -B -X utf8 engine/_integration/r35/rebuild.py --build --write-receipt --output "D:\lineage開發\TW1608262200研究\scratch\issue-7\rebuild"
```

完整命令退出碼：**2**。以下完整保留實際 stdout/stderr 文字（僅統一換行）。

### 建置 stdout

```text
sdk 0
product-restore 0
product-build 0
launcher-restore 0
launcher-build 0
receipt written D:\lineage開發\TW1608262200研究\engine\_integration\r35\receipt.json
```

### 建置 stderr

```text
REFUSED: input path escapes root: assets/integrity.json
```

### 子命令與退出碼

| 階段 | 退出碼 |
|---|---:|
| sdk | 0 |
| product-restore | 0 |
| product-build | 0 |
| launcher-restore | 0 |
| launcher-build | 0 |

### sdk

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

stdout：

```text
10.0.300
```

stderr：
空（0 bytes）。

### product-restore

```json
{
  "name": "product-restore",
  "argv": [
    "dotnet",
    "restore",
    "D:\\lineage開發\\TW1608262200研究\\engine\\source\\OfflineClient.csproj",
    "--configfile",
    "D:\\lineage開發\\TW1608262200研究\\engine\\NuGet.Config"
  ],
  "exitCode": 0
}
```

stdout：

```text
  正在判斷要還原的專案...
  已還原 D:\lineage開發\TW1608262200研究\engine\audio\P24.Audio.csproj (141 毫秒 內)。
  已還原 D:\lineage開發\TW1608262200研究\engine\source\OfflineClient.csproj (226 毫秒 內)。
```

stderr：

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

```json
{
  "name": "product-build",
  "argv": [
    "dotnet",
    "build",
    "D:\\lineage開發\\TW1608262200研究\\engine\\source\\OfflineClient.csproj",
    "-c",
    "Release",
    "--no-restore",
    "-o",
    "D:\\lineage開發\\TW1608262200研究\\engine\\build\\client-product-r35",
    "--nologo",
    "-p:NuGetAudit=false",
    "-p:UseSharedCompilation=false"
  ],
  "exitCode": 0
}
```

stdout：

```text
  P24.Audio -> D:\lineage開發\TW1608262200研究\engine\build\client-product-r35\P24.Audio.dll
  OfflineClient -> D:\lineage開發\TW1608262200研究\engine\build\client-product-r35\P11.ClientEngine.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:11.90
```

stderr：
空（0 bytes）。

### launcher-restore

```json
{
  "name": "launcher-restore",
  "argv": [
    "dotnet",
    "restore",
    "D:\\lineage開發\\TW1608262200研究\\engine\\_integration\\r35\\launcher-source\\Launcher.csproj",
    "--configfile",
    "D:\\lineage開發\\TW1608262200研究\\engine\\NuGet.Config"
  ],
  "exitCode": 0
}
```

stdout：

```text
  正在判斷要還原的專案...
  已還原 D:\lineage開發\TW1608262200研究\engine\_integration\r35\launcher-source\Launcher.csproj (84 毫秒 內)。
```

stderr：
空（0 bytes）。

### launcher-build

```json
{
  "name": "launcher-build",
  "argv": [
    "dotnet",
    "build",
    "D:\\lineage開發\\TW1608262200研究\\engine\\_integration\\r35\\launcher-source\\Launcher.csproj",
    "-c",
    "Release",
    "--no-restore",
    "--nologo",
    "-p:NuGetAudit=false",
    "-p:UseSharedCompilation=false",
    "-o",
    "D:\\lineage開發\\TW1608262200研究\\engine\\launcher"
  ],
  "exitCode": 0
}
```

stdout：

```text
  Launcher -> D:\lineage開發\TW1608262200研究\engine\launcher\P24.Launcher.dll

建置成功。
    0 個警告
    0 個錯誤

經過時間 00:00:03.40
```

stderr：
空（0 bytes）。

### 建置後來源核驗

```text
python -B -X utf8 engine/_integration/r35/rebuild.py
```

退出碼：0；stderr：空。

```text
{"status": "r35-inputs-verified", "sourceFiles": 181, "pinnedFiles": 190, "writes": false}
```


## Receipt 與來源識別

**Receipt 在最終 manifest 核驗之前已被寫入，只記錄產物身分，不能作為完整建置通過證據。** 最終 PASS r35 與 scratch/issue-7/rebuild/result.json 均未產生。

| 檔案 | 大小（bytes） | SHA-256 |
|---|---:|---|
| `engine/_integration/r35/receipt.json` | 474 | `1e08cd572e98f7bada9eec853e25c51d0cd4646c514ff5a8346e3f614a73cf9b` |
| `engine/_integration/r35/rebuild.py` | 8629 | `f73130508b48a19758745efc50470c236a50c96b877df9b7ca6c677c375fd95f` |
| `engine/_integration/r35/rebuild-lock.json` | 27475 | `36b3e6991a69d5b06b8978022af61a8593dc5da41de84ecc50b52a1249e6794d` |
| `engine/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` |
| `engine/build/product-r35-manifest.json` | 7953 | `c30e8e9ba9f688416dd5a27ab30a46f10aa3b2a4cf51b64f80bfae5add5bd9be` |

Receipt 完整內容：

```json
{
  "schema": "r35-receipt-v1",
  "builtAt": "2026-11-04T05:27:44.285045+00:00",
  "sdk": "10.0.300",
  "productDllSha256": "96ddabf5cc830acac79e5aeb8abb3970b0d34e8eb6edbfaed6ff21d4056867a9",
  "audioDllSha256": "2a0d613657a5f6b3df773b11634a8f6ba8be27a84ca9d692d4d29b6d0c00adb6",
  "productManifestSha256": "c30e8e9ba9f688416dd5a27ab30a46f10aa3b2a4cf51b64f80bfae5add5bd9be",
  "launcherDllSha256": "d1bf270bb68956e11739de81013e7732bdc573306e8190ab047808678b104ca6"
}
```


## 實際產物清單

以下為本次產物位置所有 exe/dll（含 WebView2 相依檔）；大小為 bytes。檔案已生成，但完整 r35 驗證仍失敗。

| 路徑／名稱 | 大小（bytes） | SHA-256 |
|---|---:|---|
| `engine/build/client-product-r35/Microsoft.Web.WebView2.Core.dll` | 698208 | `d60e6e94245078cb56e44ebd5f360dc04bf18e460136be93bfc4cfca43b98505` |
| `engine/build/client-product-r35/Microsoft.Web.WebView2.WinForms.dll` | 39264 | `84b5772260dbdfe95591197edc9315dcd2cc86ad610e4b14ddd5e4291e31a381` |
| `engine/build/client-product-r35/P11.ClientEngine.dll` | 4925440 | `96ddabf5cc830acac79e5aeb8abb3970b0d34e8eb6edbfaed6ff21d4056867a9` |
| `engine/build/client-product-r35/P11.ClientEngine.exe` | 162304 | `9a4959063cecf4a6c93a443a87b47d162bc2fb320f2213a6f015b1905d2f85f1` |
| `engine/build/client-product-r35/P24.Audio.dll` | 53760 | `2a0d613657a5f6b3df773b11634a8f6ba8be27a84ca9d692d4d29b6d0c00adb6` |
| `engine/build/client-product-r35/runtimes/win-arm64/native/WebView2Loader.dll` | 180064 | `4f48cb32d34298c86cca2108057eea0d900b5e422575e926e8086318a0b9cbf0` |
| `engine/build/client-product-r35/runtimes/win-x64/native/WebView2Loader.dll` | 194912 | `3426dcc55fdfb8b5e7ac623cf33b4ccf283fbf68a0c5a65bddfdb4d749a8abee` |
| `engine/build/client-product-r35/runtimes/win-x86/native/WebView2Loader.dll` | 155488 | `87c61e2cae380112c60853e28178b8ea9a0d63739fc03cead680d4ad3fde6b7d` |
| `engine/launcher/P24.Launcher.dll` | 24064 | `d1bf270bb68956e11739de81013e7732bdc573306e8190ab047808678b104ca6` |
| `engine/launcher/P24.Launcher.exe` | 162304 | `6ea4f23dd2923476849b03f8fe817d5f489434ab4e74d79c0d91ba06af29eba5` |

## 警告與執行環境處理

- 兩個 build 均回報 0 警告、0 錯誤。product-restore stderr 是 .NET 歡迎訊息，非還原失敗，完整內容已列於上方。
- 原命令 sandbox 初始化失敗（helper_unknown_error: setup refresh had errors），改用獲准的直接命令執行。Git 使用單次 `-c safe.directory=D:/lineage開發/TW1608262200研究`，未修改全域設定。
- 原 PATH 無 python；僅在命令程序加入既有 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312`。rg 不可用，改用內建查閱工具。前置工具錯誤不混入建置退出碼。
- TEMP/TMP、DOTNET_CLI_HOME、NuGet HTTP/scratch cache 指定在 scratch/issue-7；NUGET_PACKAGES 指定 engine/.nuget。rebuild.py 子程序使用 scratch/issue-7/rebuild 下的 tmp/.dotnet。未修改全域 SDK 或使用者家目錄設定。
- 寫入前檢查既有輸出位置，未發現硬連結或 junction。依既有腳本生成 engine/launcher 與 engine/_integration/r35/receipt.json，作為本機產物，不提交。
- resolve() 對 manifest 的 assets/integrity.json 做解析後根目錄檢查，與既有 assets junction 衝突。本輪不繞過檢查、不修改 junction。腳本透過 junction 唯讀讀取 integrity 索引；未修改 releases/、硬連結或正式客戶端，未複製 pak/map/sprite。

## 未驗證什麼／推測／待驗證

- `啟動引擎.cmd --check-only`：**未執行，退出碼不適用，無輸出**。完整建置退出 2，未滿足 Issue 規定的建置成功前置條件。
- 靜態查閱確認根目錄啟動腳本仍指向 `releases/r33/launcher/P24.Launcher.exe`；將來該命令通過也不能直接當作 r35 產物驗證通過，需後續任務界定 r35 check-only 入口。
- 未執行 --verify、原生畫面／登入／遊戲流程驗收。未啟動遊戲、連正式服務或資料庫、讀取機密設定、啟停服務。
- 推測：無額外推測。根因由拒絕訊息、resolve() 程式與 junction 解析結果確認；修正後是否完整通過仍待驗證。

## 產出路徑

- docs/reports/issue-7.md：本報告，含真實輸出、退出碼、receipt 與 SHA-256、產物清單。
- docs/REPORT.md：本輪變更與下一步各新增一條 Issue #7。
- engine/_integration/r35/receipt.json、engine/build/product-r35-manifest.json、engine/build/client-product-r35/、engine/launcher/：本機生成檔，不提交。
- scratch/issue-7/：頂層 stdout/stderr、build-command.json、artifacts.json、source-verification.json、rebuild/ 子命令紀錄，不提交。

## 給下一棒的提示

由調度器／Claude 審查 task/7 報告，另派引擎維護者修正 r35 核驗與既有 assets junction 的相容性，保留路徑邊界檢查，不修改或搬移封存。並界定根目錄 r33 入口與 r35 環境檢查的分工。修正合併後由本機執行器重跑建置與 check-only；原生遊戲驗收只由使用者執行。TASKS.md 尚未列出 T013，本輪以 Issue #7 指派為準，不修改任務清單。
