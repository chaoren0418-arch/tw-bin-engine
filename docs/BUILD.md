# 乾淨機器從零建置

適用：Windows x64。目的是在另一台機器或乾淨目錄，用 repo 裡**實際存在的指令**核對來源並建出 r35 產品。本文件只整理 `engine/README.md`、`engine/_integration/r35/rebuild.py`、`.github/workflows/build.yml`、`PLAN.md`、`engine/global.json` 與相關入口腳本已寫明的事實；不代替真機登入。

正式入口仍是 **r33**（`啟動引擎.cmd` → `releases/r33`）。canonical 來源是 **r35**（181 檔，lock 在 `engine/_integration/r35/rebuild-lock.json`）。`engine/build-product.ps1` 指向 r35。

禁止：把 pak／map／sprite 複製進本專案；改 `releases/`、封存、硬連結或機密設定；AI 啟動遊戲、連正式服務或改資料庫。

---

## 需要的工具

| 項目 | 依據 | 備註 |
|---|---|---|
| **.NET SDK 10.0.300** | `engine/global.json`：`sdk.version` = `10.0.300`，`rollForward` = `disable` | `rebuild.py --build` 會跑 `dotnet --version`，版本必須完全相符，否則 `REFUSED: wrong SDK …` |
| **.NET 10 Windows Desktop Runtime** | r35 launcher（`engine/_integration/r35/launcher-source/Program.cs`） | 產品是 `net10.0-windows` + WinForms。一般 .NET Runtime **不等於** Desktop Runtime；check-only 會查 `Microsoft.WindowsDesktop.App` 10.x 與 `System.Windows.Forms.dll` |
| **Python 3.12** | T004／既有本機核驗（Issue #3）用 3.12；`rebuild.py` 呼叫 `hashlib.file_digest`（需 3.11+） | 指令固定為 `python -B -X utf8 …`。PATH 沒有 `python` 時，Windows 可用 `py -3.12` 找到後再執行同一條指令 |
| **WebView2 Runtime** | 產品參考 `Microsoft.Web.WebView2` 1.0.4258.31（`engine/source/OfflineClient.csproj`）；launcher 載入 `WebView2Loader.dll` 並呼叫 `GetAvailableCoreWebView2BrowserVersionString` | 建置拉的是 NuGet 套件；**執行／check-only** 還要機器已安裝 WebView2 Runtime，否則 `找不到可用 WebView2 Runtime` |
| **Windows x64** | `rebuild.py`：`--build` 要求 `platform.system() == 'Windows'`；launcher 要求 `OperatingSystem.IsWindows()` 且 `Architecture.X64` | 預設模式（只核 pin）可在非 Windows 跑；建置與啟動不行 |

SDK 安裝後核對：

```text
dotnet --version
```

預期輸出：`10.0.300`。

---

## 資源目錄約定

兩層資源，不要混：

1. **原客戶端檔**（pak／map／sprite 等）一律引用 `D:\lineage開發\0-客戶端`。`project-manifest.json` 的 `external_sources` 與 `AGENTS.md` 都寫這個路徑。**不得複製進本專案。**
2. **引擎素材** `engine/assets` 是指向 `releases/r28` 的**唯讀 junction**（`engine/README.md`、`AGENTS.md`）。`.gitignore` 排除 `engine/assets` 與 `releases/`（只留 `releases/SHA256SUMS.txt`），乾淨 `git clone` **不會**帶出這兩處。

建置與 check-only 怎麼依賴它們：

- `rebuild.py --build` 若看得到 `engine/assets/integrity.json`，會把它寫進 `engine/build/product-r35-manifest.json`。
- r35 launcher 的 `--check-only` **要求** manifest 含 `assets/integrity.json`，並依該索引核對每個素材的存在與大小。junction 沒接好時，check-only 會失敗。
- 根目錄 `啟動引擎.cmd` 依賴 `releases\r33\launcher\P24.Launcher.exe`，不是 git 內容。沒有封存 release 的乾淨 clone，這條入口會 `exit /b 2`。

TASKS T003 寫了環境變數 `TW_ENGINE_ROOT`／`TW_CLIENT_ROOT`（後者預設即 `D:\lineage開發\0-客戶端`）。目前 `rebuild.py` 與 `build-product.ps1` **沒有讀這兩個變數**：repo 根由腳本位置推導（`rebuild.py` 的 `Path(__file__)`；PowerShell 的 `$PSScriptRoot`）。

---

## 三條指令（本機 Windows，在 repo 根）

工作目錄：clone 下來的專案根（不必是歷史路徑 `D:\lineage開發\TW1608262200研究`）。

### 1. restore

CI 與無離線 `vendor` 的機器（`.github/workflows/build.yml` 的 Restore 步）：

```text
dotnet restore engine/source/OfflineClient.csproj --configfile .github/nuget.ci.config
```

本機若已有 `engine/vendor`（`.gitignore` 排除，不在 clone 裡），用釘在 lock 裡的離線設定：

```text
dotnet restore engine/source/OfflineClient.csproj --configfile engine/NuGet.Config
```

`engine/NuGet.Config` 只剩 `stage-offline` → `vendor`。沒有 `engine/vendor` 時這條會失敗，改走上面的 `nuget.ci.config`（`https://api.nuget.org/v3/index.json`）。

`rebuild.py --build` 自己也會再 restore 產品與 launcher；預設 `--configfile` 是 `engine/NuGet.Config`。本機沒有 vendor 時，建置要帶 CI 同一份設定，例如：

```text
python -B -X utf8 engine/_integration/r35/rebuild.py --build --configfile .github/nuget.ci.config
```

或：

```text
powershell -File engine/build-product.ps1 -ConfigFile .github/nuget.ci.config
```

### 2. 建置產品

```text
powershell -File engine/build-product.ps1
```

`engine/build-product.ps1` 只轉呼叫 `engine/_integration/r35/product-build.ps1`，後者再執行 `python -B -X utf8 …/rebuild.py --build`。等價於上一節的 `rebuild.py --build`。成功時寫入：

- `engine/build/client-product-r35/`（`P11.ClientEngine.exe` 等）
- `engine/build/product-r35-manifest.json`
- `engine/launcher/P24.Launcher.exe`

這一步**需要 Windows** 與 SDK 10.0.300。加 `--write-receipt` 才會寫 `engine/_integration/r35/receipt.json`（CI 會加）。

### 3. check-only（不開 GUI、不登入、不開 socket）

正式入口（`engine/README.md`：release SHA 通過後切到 r33）：

```text
啟動引擎.cmd --check-only
```

腳本本體：若缺少 `releases\r33\launcher\P24.Launcher.exe` 則 `exit /b 2`，然後把參數原樣傳給該 launcher。`--check-only` 只核環境與 runtime，不開 GUI／登入／socket，不啟停服務。

剛用 r35 建出來的產品，入口是另一支（`engine/啟動新版客戶端.cmd`，lock 有釘）：

```text
engine\啟動新版客戶端.cmd --check-only
```

預期 JSON 含 `status: checked-only`、`revision: r35`、`uiStarted: false`、`networkStarted: false`、`servicesStarted: false`（見 `docs/native-baseline/CHECKLIST.md`）。缺 `engine\launcher\P24.Launcher.exe` 時這支也是 exit 2。

資料庫與遊戲伺服器由使用者自行管理；這三條指令都不會動它們。

---

## 來源核驗（r35）

唯讀核 pin（任何 OS；本文件撰寫時在 Linux 跑過，exit 0）：

```text
python -B -X utf8 engine/_integration/r35/rebuild.py
```

通過時 stdout 類似：

```text
{"status": "r35-inputs-verified", "sourceFiles": 181, "pinnedFiles": 190, "writes": false}
```

失敗印 `REFUSED: …` 到 stderr，退出碼 2。

| 參數 | 作用（`rebuild.py` 檔頭與 `main()`） |
|---|---|
| （預設） | 核 `rebuild-lock.json` 每個 pinned 檔的存在、size、SHA-256；`sourceCount` 必須是 181 |
| `--build` | 先核 pin，再 restore／build 產品與 launcher（**僅 Windows**） |
| `--verify` | 核 pin，並把既有 `build/client-product-r35` 對照 `receipt.json` |
| `--write-receipt` | `--build` 成功後把 DLL／manifest SHA 寫入 `receipt.json` |
| `--configfile` | restore 用的 NuGet 設定；預設 `engine/NuGet.Config`；CI 傳 `.github/nuget.ci.config` |
| `--output` | 證據目錄；未指定則寫 `engine/_integration/evidence/r35-<UTC>` |

**不要**用 `engine/_integration/r33/rebuild.py` 或 `r34` 核現行來源：那兩份 lock 分別要 177／180 檔，現行 canonical 是 181，pin 核驗會拒絕（`PLAN.md`）。

Release 目錄不可拿來建置（`engine/README.md`）。

---

## CI 怎麼跑

工作流程：`.github/workflows/build.yml`。觸發：`push` 到 `main`，以及所有 `pull_request`。跑在 `windows-latest`。

順序（與 YAML 一致）：

1. `git config --global core.autocrlf false` — r35 lock 釘的是精確位元組；CRLF 轉換會造成 `pin drift`。repo 另有 `.gitattributes`：`* -text`。
2. `actions/checkout@v4`
3. `actions/setup-dotnet@v4`，`global-json-file: engine/global.json`（即 SDK 10.0.300）
4. `dotnet restore engine/source/OfflineClient.csproj --configfile .github/nuget.ci.config`
5. `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -warnaserror:false`
6. `python -B -X utf8 engine/_integration/r35/rebuild.py --build --configfile .github/nuget.ci.config --write-receipt --output engine/_integration/evidence/ci`
7. 上傳 `engine/_integration/r35/receipt.json` 與 `engine/_integration/evidence/ci/**`；失敗時另傳 `engine/source/obj/**/*.log`

**NuGet 兩套設定，不要混：**

| | 本機 | CI |
|---|---|---|
| 設定檔 | `engine/NuGet.Config` | `.github/nuget.ci.config` |
| 來源 | `<clear/>` 後只留 `vendor`（離線） | `<clear/>` 後只留 nuget.org |
| 套件目錄 | `rebuild.py` 預設 `NUGET_PACKAGES` = `engine/.nuget`（若環境未設） | 同樣邏輯，但 restore 走 nuget.org |

CI **沒有**跑 `啟動引擎.cmd --check-only`，也沒有 `engine/assets`（gitignore）。`--build` 在沒有 `assets/integrity.json` 時仍可能完成編譯；那不代表 launcher check-only 會過。

---

## 常見錯誤與處理

以下訊息都來自現有腳本，不是推測的「可能還會怎樣」。

| 現象 | 從哪來 | 處理 |
|---|---|---|
| `REFUSED: pin drift: …` 或 `missing pinned file` | `rebuild.py` `pin()` | 來源與 `rebuild-lock.json` 不符。先確認 `core.autocrlf` 為 false、checkout 沒被轉成 CRLF。不要改 lock 來「配合」本機換行。 |
| `REFUSED: wrong SDK …, global.json pins 10.0.300` | `rebuild.py --build` | 安裝 **正好** 10.0.300。`rollForward` 已關閉，較新的 10.0.x 也不行。 |
| `REFUSED: building the WinForms/WebView2 product requires Windows` | `rebuild.py --build` | 到 Windows x64 建；Linux／macOS 只能跑預設核 pin。 |
| `NU1101`／找不到套件，且指令沒用 `--configfile` | `engine/NuGet.Config` → `vendor` | clone 沒有 `engine/vendor`。改用 `.github/nuget.ci.config`，或在本機備好離線 vendor。 |
| `啟動引擎.cmd` 立刻 exit 2 | `啟動引擎.cmd` | 缺少 `releases\r33\launcher\P24.Launcher.exe`。這是未追蹤的封存，不是 clone 的一部分。剛建的 r35 請改跑 `engine\啟動新版客戶端.cmd --check-only`。 |
| `Missing launcher\P24.Launcher.exe` | `engine/啟動新版客戶端.cmd` | 尚未建置，或建置輸出不在 `engine/launcher/`。先跑 `engine/build-product.ps1`。 |
| `找不到 .NET 10 Windows Desktop Runtime` | r35 launcher | 已裝 SDK 或一般 Runtime 不夠。加裝 x64 **Desktop** Runtime 10。 |
| `找不到可用 WebView2 Runtime` | r35 launcher | 機器沒有可用的 WebView2 Runtime。NuGet 的 `WebView2Loader.dll` 不能代替 Runtime。 |
| `缺少 r35 產品 manifest`／`缺少或大小不符的素材` | r35 launcher | 沒跑過 `--build`，或 `engine/assets` junction 沒指到完整 r28 素材。不要為了補齊而去複製 pak／map／sprite。 |
| `r33`／`r34` 的 `rebuild.py` 拒絕 | 舊 lock 要 177／180 檔 | 現行來源用 `engine/_integration/r35/rebuild.py`。 |
| `productDllSha256 differs from receipt` | `rebuild.py --verify` | 產物與 `receipt.json` 不一致。改過程式後應重新 `--build --write-receipt`（這是 CI 的做法），不要手改 receipt。 |

---

## 本文件已核對／未核對

**已核對（文件內每條指令都在 repo 裡，路徑存在）：**

- `engine/global.json`、`engine/build-product.ps1`、`engine/_integration/r35/product-build.ps1`、`engine/_integration/r35/rebuild.py`、`engine/NuGet.Config`、`.github/nuget.ci.config`、`.github/workflows/build.yml`
- `啟動引擎.cmd`、`engine/啟動新版客戶端.cmd`、`engine/source/OfflineClient.csproj`、`engine/_integration/r35/launcher-source/Program.cs`
- 本環境執行 `python -B -X utf8 engine/_integration/r35/rebuild.py` → exit 0，`sourceFiles=181`，`pinnedFiles=190`

**未核對：**

- 未在 Windows 跑 `build-product.ps1`、`--build`、任一支 `--check-only`（本環境是 Linux，且規則禁止啟動遊戲）
- 未在另一台乾淨機器做端到端重現（T004 使用者驗收）
- 未連正式服務、未改資料庫、未碰 `releases/`
