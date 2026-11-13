# Issue #1057 T472 可攜 zip 白名單打包與內容審查

分支 `task/1057`，PR #1112。依 `docs/design/portable-delivery.md` 的「zip 內容白名單草案」實作。

## 做了什麼

- 新增 `engine/tools/package-portable.py`：
  - **白名單收集**：只收腳本中逐檔列出的路徑，不遞迴複製 engine。每個來源檔與它在 repo 內的每一層上層目錄都用 `lstat` 檢查，遇到 junction 或 symlink 就拒絕；不是一般檔案也拒絕。
  - 白名單共 23 檔：
    - 根目錄 `README.md`、`啟動引擎.cmd`
    - `engine/launcher/` 4 檔（exe／dll／deps.json／runtimeconfig.json）
    - `engine/build/client-product-dev/` 8 檔：P11.ClientEngine 的 exe／dll／deps／runtimeconfig、P24.Audio.dll、WebView2 Core 與 WinForms、`runtimes/win-x64/native/WebView2Loader.dll`
    - `engine/web/` 的 8 個文字頁面檔
    - 產品 manifest
    - `docs/` 目前不收（見「未驗證」）。
  - **產品 manifest**：從建置的 `engine/build/product-dev-manifest.json` 產生可攜版：
    - 每個打包檔的 size 與 SHA-256 必須和建置 manifest 一致；不一致代表建置輸出過期或被改過，直接拒絕。
    - `files` 只保留打包的程式檔與 web 檔，拿掉 PDB、XML、非 x64 loader 和 `assets/integrity.json`。
    - 其他欄位不動，launcher 的 `VerifyProduct` 規則仍然成立。
  - **確定性 zip**：檔名排序、時間戳固定為 1980-01-01、權限固定 0644。產出寫到 `engine/build/portable/`：
    - `tw-bin-engine-portable.zip`
    - `tw-bin-engine-portable.files.json`：每檔的路徑、大小、SHA-256，加上 zip 的 SHA、檔數、總大小，以及沒打包的建置檔和原因。
  - **審查**：打包後重新打開 zip，逐檔檢查：
    - **路徑**：不得是絕對路徑、不得含 `..` 或反斜線、不得有目錄 entry 或重複；必須正好等於白名單，多一檔或少一檔都拒絕；symlink 屬性也拒絕。
    - **檔名**：拒絕 assets／icons／ground／Sound／map／Sprite／Tile／Text／vendor／source／runtime 等目錄，也拒絕 png／wav／pak／spr／til／s32／pdb／cs／py／sql／jar／bin／idb／xml 等副檔名。所有副檔名都會檢查，不只最後一個。
    - **magic**：PNG、GIF、JPEG、RIFF/WAV、Ogg、MP3、zip、gzip、7z、PDB（MSF 與 portable）、Java class、IDA、SQLite、CAB。
      - exe／dll 必須有 PE 標頭；非 exe／dll 的檔名出現 PE 內容也拒絕。
      - 其他檔案必須是 UTF-8 文字、不含 NUL。所以副檔名改掉的 SPR、TIL、pak 這類二進位檔都會被擋。
    - **文字內容**：拒絕 `data:image|audio` URI、長度 ≥ 2048 的 base64、SQL DDL／DML；JSON 再用 `check-no-game-assets.py` 的 pixels 與 timing 偵測。
    - **嵌入資源**：用 `pe_resource.manifest_resources` 直接讀 PE 與 CLR metadata 的 ManifestResource，不載入 DLL。
      - 只允許 T471 封閉清單中的 `P24.Audio.dll / P24.Audio.CombatSfxPins.json`。啟動時有 assert，確認這份清單和 `check-no-game-assets.RESOURCES` 一致。
      - 其餘資源一律拒絕。允許的那一項也要通過 payload 檢查：magic、內含 PNG／GIF／WAV／PDB 簽章、pixels、timing。
      - 第三方唯一例外是 WebView2 WinForms 設計工具的 toolbox bitmap（822 bytes），用 SHA-256 `0d0e717f…a20d64` 釘住，內容不同就拒絕。
    - **manifest 一致性**：zip 內的程式檔、web 檔和可攜 manifest 必須一一對應且 hash 相符，launcher 必要的 4 個 pin 都要在。
  - 審查不通過時會刪掉 zip，只留清單（status=refused），避免留下看起來可以交付的檔案。
  - `--review <zip>` 可以單獨審查既有的 zip。
- `engine/_integration/dev/pe_resource.py`：把資源列舉抽成通用的 `manifest_resources(data)`；native PE（沒有 CLI header）回傳 None。原本的 `verify_resource` 改為呼叫它，行為不變。
- 新增 `engine/tools/portable/README.md`、`engine/tools/portable/啟動引擎.cmd`：可攜版的使用者說明與入口範本。
  - 打包時放到 zip 根目錄。repo 根目錄的 README 與 `啟動引擎.cmd` 是開發者用的，入口指向 r33，不適合放進 zip。
  - cmd 內容只用 ASCII、CRLF 換行，設定 `TW_CLIENT_ROOT` 後呼叫 `engine\launcher\P24.Launcher.exe`。
- `.github/workflows/build.yml`：
  - ubuntu `dotnet` job：在既有資源閘門後執行 `package-portable.py --self-test`（symlink 案例）。
  - Windows `map-continuity` job 最後依序執行：
    1. `--self-test`（真正的 NTFS junction 案例）
    2. `rebuild.py --build`（不帶 RID 的 native 版面，才會有 `runtimes/win-x64/native/WebView2Loader.dll`）
    3. 打包
    4. `--review`
  - artifact 只上傳 `tw-bin-engine-portable.files.json`，**不上傳 zip、不建立 Release**。
- 沒有改 `rebuild-lock.json`。`pe_resource.py` 在 lock 的 pin 內，CI 以暫時 lock 建置（#646 規則）；合併後由調度器重產 lock。

## 怎麼驗證（CI，PR #1112 最新 commit 三個 job 全綠）

- **ubuntu `dotnet`** 與 **Windows `map-continuity`**：`--self-test` 15 項全部通過。項目包括：
  - 改名 PNG（放進 panel.css／index.html、新增 logo.js，以及經實際 collector 的 trade.js）
  - 目錄 junction（Windows 用 `_winapi.CreateJunction`，Linux 用 symlink）與檔案 symlink
  - 在 P11.ClientEngine.dll 嵌入 pixels JSON；在允許的 CombatSfxPins 名稱下放 pixels 或 PNG
  - 改名的 PDB、WAV、PE、Java class、IDA、二進位 sprite bytes
  - data URI、長 base64、SQL
  - 禁止的檔名：pdb／assets／Sprite／icons／sql／jar／bin／idb／cs／s32／pak／vendor／`..`
  - WinForms 資源 SHA 不符、建置輸出和 manifest 不符、zip 內檔案和 manifest 不符、缺檔
  - zip 確定性、`pe_resource` 讀取合成 assembly
- **Windows 實際建置後打包**：`status: pass`，errors 0，`--review` 再審一次也是 pass。
  - **zip 清單：23 檔，總大小 10,239,010 bytes**；zip 本身 3,458,059 bytes，SHA-256 `6de18d0016561e33b1135a571d49135cb9830464d5d56fe1d2446b27829e7f7f`。
  - 有 13 個建置檔沒有打包：PDB、XML、arm64／x86 loader、P24.Audio.deps.json 等，原因寫在清單 JSON。

| 檔案 | bytes |
|---|---:|
| P11.ClientEngine.dll | 8,783,872 |
| Microsoft.Web.WebView2.Core.dll | 698,208 |
| WebView2Loader.dll (win-x64) | 194,912 |
| P11.ClientEngine.exe／P24.Launcher.exe | 各 162,304 |
| P24.Audio.dll | 94,720 |
| P24.Launcher.dll | 44,544 |
| Microsoft.Web.WebView2.WinForms.dll | 39,264 |
| web 8 檔合計 | 49,683 |
| 其餘（json、README、cmd、manifest） | 9,199 |

- 本機只做靜態解析：用 `pe_resource` 讀使用者既有的舊 dev 建置（唯讀），確認 WinForms bitmap 的 SHA。同時看到舊建置的 P11.ClientEngine.dll 還有 pickup／GroundCatalog 嵌入資源（T463／T464 之前的版本），這種過期建置會被本審查拒絕。沒有在本機執行任何編譯產物或自測。

## 未驗證／待驗證

- **待驗證（交 Codex／dot）**：把 zip 解壓到乾淨 Windows 目錄後，執行 `啟動引擎.cmd --check-only`，確認 launcher 接受可攜 manifest。CI 只驗了 manifest 欄位與 pin 齊全，沒有真的執行 launcher。
- **推測**：
  - 不帶 PDB／XML 不影響執行。
  - 不帶 `P24.Audio.deps.json` 也沒問題：它是 ProjectReference 的 deps，主程式的 deps.json 已經列出 P24.Audio。
- **`docs/`**：目前沒有收任何文件。使用說明在 zip 根目錄的 README.md；授權說明或排錯文件要等內容審查後，再加進 `DOCS` 常數。
- **審查範圍限制**：native PE（apphost、WebView2Loader）的 Win32 `.rsrc` 沒有逐項審查。這兩個檔來自 SDK 或 NuGet 原樣，不是本專案產生的。
- 是否發布 GitHub Release，由使用者另開任務決定。

## 產出路徑

- `engine/tools/package-portable.py`、`engine/tools/portable/README.md`、`engine/tools/portable/啟動引擎.cmd`
- `engine/_integration/dev/pe_resource.py`（重構）、`.github/workflows/build.yml`
- CI artifact `portable-zip-file-list`（`engine/build/portable/tw-bin-engine-portable.files.json`）

## 給下一棒的提示

- 新增會打包的檔案時，必須同時改 `PRODUCT_FILES`、`LAUNCHER_FILES`、`WEB_FILES`、`DOCS`；建置目錄裡多出的新檔只會列進 excludedBuildFiles，不會自動打包。
- 新增 EmbeddedResource 時，必須同時更新 `check-no-game-assets.RESOURCES` 與本腳本的 `ALLOWED_RESOURCES`（有 assert 保護）。WebView2 套件升級後，WinForms bitmap 的 pin 要重算。
- Linux 交叉建置（`-r win-x64`）會把 WebView2Loader 攤平到輸出根目錄，launcher 版面不符；所以打包只在 Windows job、用 `rebuild.py --build` 的 native 版面執行。
