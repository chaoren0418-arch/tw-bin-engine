# Issue #1053 — T468 素材準備入口與 launcher 接線

## 做了什麼

- 產品新增 `--prepare-assets`，只接受這一個參數，在 `ProductEntry`、WinForms 初始化與資源讀取之前分派；呼叫 T467 的 `AssetPrepOrchestrator.Product()`，沿用 `[AssetRecipe]` 自動發現、完整 13 類預熱、checkpoint 與原子發布流程。產品 `Program.cs` 只有一行新增分派，現有入口沒有可在 UI 初始化前使用的命令自動發現掛點，所以依任務明示例外接線。沒有修改 `DemoChecks.csproj` 或註冊表。
- 進度以 UTF-8 JSON lines 回報階段、類別、完成／總數、新做／重用與磁碟空間；成功後重新讀取 active 收據及輸出，才回報 `prepared`。取消或失敗回傳 2，不進入 UI 或登入流程。
- 共用 BCL-only `AssetPreparationState`，由產品與 launcher 編譯同一份來源。接受 `tw-assets-integrity-v2`／`tw-asset-prep-set-v1`，驗總收據 SHA、ready、每個索引輸出的大小及 SHA；拒絕新收據路徑中的 junction、重複／越界／保留路徑。這是本機產物一致性檢查，不把收據當成第三方簽章。
- launcher 先驗程式 manifest，再判讀素材。一般啟動遇到未準備、中斷或不符時，以獨立產品程序執行 `--prepare-assets`，傳遞明確 `TW_CLIENT_ROOT`／`TW_ENGINE_ROOT`，同步讀取 stdout／stderr；成功後再驗 active 收據與程式 manifest，最後檢查 Desktop／WebView2 Runtime 並啟動 `--normal-product`。即使準備程序回傳 0，沒有合格收據仍拒絕啟動。
- `--check-only` 不呼叫準備程序；尚未準備時也不載入 WebView2。已準備時保留 `checked-only`，新增 `assetState`、`assetReceipt`、`assetReceiptSha256`，保留舊版 runtime／manifest／安全旗標欄位。
- 舊 v1 素材必須仍由產品 manifest 鎖定 `assets/integrity.json` SHA；沿用檔案存在／大小檢查，允許開發機 junction，沒有要求開發機補設 `TW_CLIENT_ROOT`。v2 的 integrity／收據是本機生成資料，不要求打包 manifest 包含它們，也不以打包時的舊 integrity pin 拒絕重新生成結果。
- 可攜流程不使用開發機預設路徑；未設定、相對路徑、不存在目錄或缺少 Text／Tile／17 個 Sprite 容器與 map 目錄，會以繁體中文指出缺項，以及「修改啟動引擎.cmd 的 TW_CLIENT_ROOT；直接呼叫 exe 則設定同名環境變數」。容器內容版本與結構由既有準備器／provider 繼續驗證。Sound 維持既有 provider 的缺失／fallback 規則。

## 狀態與結束碼

| 情況 | launcher `--check-only` | 結束碼 | 是否呼叫產品 |
|---|---|---|---|
| 無 assets、無 staging | `unprepared` | 2 | 否 |
| staging／未完整 assets，或收據 `partial` | `interrupted` | 2 | 否 |
| JSON、schema、SHA、輸出或路徑不符 | `receipt-mismatch` | 2 | 否 |
| v2 ready，client 與執行期前置有效 | `checked-only`、`assetState=ready`，含收據 | 0 | 否 |
| 舊 v1 與 manifest 相符 | `checked-only`、`assetState=legacy-ready` | 0 | 否 |
| 程式 manifest、client 設定或執行期前置錯誤 | `failed` | 1 | 否 |

上述 JSON 均為 `uiStarted=false`、`networkStarted=false`、`servicesStarted=false`。一般啟動的 `preparing` 進度也保持這些旗標；準備成功後才會開正常登入畫面，不自動登入、不連 DB、不啟動服務。

## 怎麼驗證

已驗證：

- 本機僅編譯產品、launcher、DemoChecks 與 LauncherChecks：均成功。產品有 4 個既有警告、DemoChecks 有 3 個既有警告；launcher 與 LauncherChecks 為 0 警告。
- dotnet 的 CLI home、NuGet cache、TEMP／TMP 均在本 worktree 的 `scratch/`，同時設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`；本機沒有執行任何編譯產物。
- `git diff --check`；同步 `origin/main`。不修改 `rebuild-lock.json`、`docs/REPORT.md`、原始專案目錄、客戶端、封存或硬連結。

CI 測試已加入，結果待 PR 最新 commit 完成後補記：

- `LauncherChecks` 直接編譯正式 launcher 來源，用合成目錄、攔截程序邊界與執行期探測測試四種狀態、錯誤 client、準備後重驗、失敗／假成功阻擋、程式檔損壞與舊 v1；Windows CI 建立真 junction 驗相容性，結束時僅移除連結本身。
- `[CheckGroup("asset-prepare-command")]` 自動掛入既有 `native-ui-resources` CI 群組，使用合成客戶端與實際 13 個 recipe；驗中斷、續跑重用、ready、收據竄改、錯誤參數與重跑冪等，並核對合成客戶端樹保持不變。
- Windows CI 直接執行產品 WinExe 的 `--prepare-assets`（未設 client）與混合正常入口參數；要求 UTF-8 JSON、結束碼 2、`uiStarted=false`，驗證分派確實在 UI 前返回。這些程式只在 GitHub Actions 執行。
- CI artifact `launcher-preparation-checks` 保存 launcher 各情境輸出及產品命令成功進度；合成 fixture 不代表真實素材驗收。

## 未驗證什麼

- 真實 TW 客戶端完整準備、r29 等價性、耗時／峰值空間與登入畫面，交 T470／T474；本機遵守禁止執行編譯產物的限制。
- 可攜 zip 白名單／根目錄 cmd／README 的最終封裝交 T472／T473。repo 根目錄現行 r33 啟動入口沒有修改。
- 沒有宣稱跑過開發機真實 junction；此項以 CI 合成 junction 驗介面相容性，真實環境留後續驗收。

## 產出路徑

- `engine/source/AssetPrep/AssetPrepareCommand.cs`
- `engine/source/AssetPrep/AssetPreparationState.cs`
- `engine/source/Program.cs`（一行分派）
- `engine/_integration/dev/launcher-source/Program.cs`、`Launcher.csproj`
- `engine/_integration/dev/asset-prep-tests/AssetPrepareCommandChecks.cs`
- `engine/_integration/dev/launcher-tests/LauncherChecks.cs`、`LauncherChecks.csproj`
- `.github/workflows/build.yml`
- 本報告；不追蹤的本機編譯紀錄：`scratch/build-product.log`、`build-launcher.log`、`build-demo-checks.log`、`build-launcher-checks.log`。

## 給下一棒的提示

主要來源 SHA-256（本 worktree bytes）：

| 檔案 | SHA-256 |
|---|---|
| `AssetPreparationState.cs` | `123f0a82484e8c2d99ca3b04eebc3d94331e060087e509cdffc77cf84368eda9` |
| `AssetPrepareCommand.cs` | `9d79a33551502cfaceace8c6781115472c0abc4b906a91aaead9966885ba51e0` |
| launcher `Program.cs` | `d29f5ca0dd1d60ab07c5af0208350308a8bd600ff63afec35353d592086bad1d` |

- T470／T474：產品位於 `<engine>/build/client-product-dev/`，準備模式根目錄由 exe 所在位置往上兩層推導；launcher 位於 `<engine>/launcher/`，根目錄為上一層。避免用環境變數把輸出重導到其他目錄；`TW_ENGINE_ROOT` 由 launcher 傳給子程序供其他元件沿用，準備模式不以它覆寫 exe 相對根目錄。
- T472／T473：包內 manifest 只列程式／web 檔，不列本機生成 assets。cmd 應提供唯一的 `TW_CLIENT_ROOT` 設定並呼叫新 dev launcher；本任務沒有改現行 r33 入口。
- `--check-only` 結束碼 2 是明確未就緒；不要當作成功。一般啟動會續跑，不要刪 `runtime/assets-staging`。新收據會驗所有輸出 SHA，實際啟動成本由 T470 量測。
- 實機需驗準備期間進度可見、修正 client 後可重試、ready 後出現正常登入畫面，以及進入畫面前沒有連線。收到 `failed` 時核對錯誤訊息，不靠程序結束碼 0 取代收據檢查。
- 分支 `task/1053`；由 Claude／調度器審查最新 CI，合併依既定流程，本任務不自行合併。
