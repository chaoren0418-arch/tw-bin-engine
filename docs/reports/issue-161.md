# Issue #161／T090 F9 載入調查與回歸

## 結論與根因裁定

**既有紀錄與本次獨立宿主均顯示產品頁面成功載入。「持續停在初始化」未重現，仍須主控核對派工判讀。** 本次修補已確認的驗收缺口，加入實際 `engine/web` 的回歸與 Windows CI。產品程式修正的必要性尚無證據支持。

基線為 `task/161@e85f98b`。唯讀檢查原專案 `engine/runs/interactive-18948/web-events.json` 與兩張 #151 截圖，得到以下 UTC 時序。

| 事件 | 時間與結果 |
|---|---|
| 第一次 F9 | 17:13:21.813 初始化，17:13:22.859 建立 Runtime 156.0.4314.58 |
| `demo151-f9.png` | 檔案修改時間 17:13:25.289，右側空白 |
| 產品頁面 ready | 17:13:26.257 |
| 第一次導覽完成 | 17:13:26.412，成功、HTTP 200 |
| 第二次 F9 | 17:30:02.918 初始化，17:30:03.490 ready，17:30:03.502 HTTP 200 |
| `demo151-f9-final.png` | 檔案修改時間 17:30:17.771，可見角色控制面板、音量及登入欄位 |

首次截圖的檔案時間早於 ready 約 0.97 秒，兩次導覽皆成功，也沒有 initialization-error 或 process-failed。此時序支持「載入完成前取樣」的解釋，檔案修改時間的限制仍須保留。單張空白截圖不足以判定永久初始化失敗。後續實際 DOM 與圖示測試亦成功。

T064／T073 的既有宿主只載入合成 HTML 與 PNG，未覆蓋產品 HTML、六個 script、CSS、ready 與可操作控制項。這是本次確認並修補的測試缺口。

## 排除方向與已驗證

- 主目錄與 worktree 的 `WebPanel.cs`、`WebLocalResource.cs`、八個 web 檔及產品 DLL SHA-256 全部一致。產品 DLL 為 `7fa85547ec50caf0049d503db114da5f519b235c8173ea45cfda26788946fe08`。
- #155 nupkg 的 SHA-256 為 `56f7f4b8bf9aee4b8efefbbdd4f67d5f74ebd1b100ed0806da71bf76af481aa9`。套件內 x64 loader、主目錄輸出與本次輸出皆為 `3426dcc55fdfb8b5e7ac623cf33b4ccf283fbf68a0c5a65bddfdb4d749a8abee`，版本／loader 比對相符。
- 實際 `WebLocalResource.Install/Read` 路由送出產品 HTML、CSS 與全部六個 JS，皆 HTTP 200。既有 favicon 403 可與成功的頁面載入同時出現。
- 真實 WebView2 Runtime 156.0.4314.58 下，建立、銷毀、重建 controller 共兩輪。每輪驗證標題、8 個轉向／8 個移動按鈕、CSS、記事輸入，點擊產品按鈕取得恰好一次 `face`、`stop`、`focusGame`，調整尺寸後輸入保留。
- 唯讀圖示 `inventory/124.png` 經產品路由收到 HTTP 200、image/png、no-store，瀏覽器回應 bytes 與來源完全相同，解碼為 24×20。圖示 SHA-256 為 `7a989ea2e4f676a42d82e612ca9ff6a1d25e27005acf0cc312ffd270cef62919`。另以合成 PNG 重跑同一產品頁面測試，供無遊戲資源的 CI 使用。
- 原 T064 合成 HTML／PNG 的 bytes、MIME、解碼、404 與 cache 測試通過。反向測試只在 scratch 的網頁副本移除 `send('ready')`，收到 HTTP 200 後因 ready 缺失於 20 秒失敗，退出 1，證明此回歸會拒絕橋接未完成。
- canonical dev 建置通過，產品 0 錯誤、2 個既有 `MagicUi.cs:20` nullable 警告，launcher 0 警告／0 錯誤。WebResourceChecks 0 警告／0 錯誤。素材拒收檢查以加入本次新增檔的 index 核驗，3,185 個追蹤檔、0 錯誤、4 個 EmbeddedResource 宣告。dev 核驗為 172 個來源、181 個 pins。

## 怎麼重跑

先將 TEMP、TMP、DOTNET_CLI_HOME、NUGET_PACKAGES、NUGET_HTTP_CACHE_PATH、NUGET_SCRATCH 指向 worktree 內的 scratch／套件快取，停用首次體驗與 telemetry。SDK 10.0.300，Windows 桌面與 WebView2 Runtime 必須可用。

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --build --output scratch/issue-161/build
dotnet restore engine/_integration/dev/web-resource-tests/WebResourceChecks.csproj --configfile engine/NuGet.Config
dotnet run --project engine/_integration/dev/web-resource-tests/WebResourceChecks.csproj -c Release --no-restore -- scratch/issue-161/product scratch/issue-161/product.json --product-page engine
dotnet run --project engine/_integration/dev/web-resource-tests/WebResourceChecks.csproj -c Release --no-restore -- scratch/issue-161/local scratch/issue-161/local.json --product-page engine 124
dotnet run --project engine/_integration/dev/web-resource-tests/WebResourceChecks.csproj -c Release --no-restore -- scratch/issue-161/legacy scratch/issue-161/legacy.json
python -B -X utf8 engine/tools/check-no-game-assets.py
```

省略末尾圖示 ID 時使用合成 PNG，`124` 模式要求 `engine/assets/inventory/124.png` 已有合法本機來源。測試直接讀取 `engine/web`。本機 assets／vendor 以新建 junction 唯讀引用原目錄，圖示 bytes 只進記憶體。測試 JSON 含來源 SHA、請求狀態與 DOM 操作結果，預覽圖片與 WebView2 profile 留在 scratch。

## 未驗證什麼

本輪未啟動遊戲、伺服器或 DB，未重做 #151 實機 F9 操作、角色狀態同步與完整 WinForms／HUD 焦點驗收。測試僅建立獨立 WebView2 宿主，按鈕命令由測試接收。曾出現永久卡住的其他 session 或環境尚無證據。

初期 shell 的 Python／腳本編碼與測試 CSS 斷言前置已修正後重跑。GitHub CLI 沙箱內驗證為 401，提升後可使用本機既有 keyring 登入。WebView2 宿主經提升執行。未修改機密設定、封存、主目錄或硬連結，未遞迴刪除。

## 產出路徑與給下一棒的提示

- 回歸入口 `engine/_integration/dev/web-resource-tests/ProductPageChecks.cs` 與既有 `Program.cs`。
- `.github/workflows/build.yml` 在既有 Windows job 加入真實 WebView2 產品頁面及 T064 測試，JSON 隨 Actions artifact 保存。
- 證據 `work/issue-161/historical-evidence.json`、`product-local.json`、`product-synthetic.json`、`legacy.json`、`negative.json`。原始建置記錄與預覽圖在本 worktree `scratch/issue-161/`。
- Claude／主控請依兩次 HTTP 200／ready 與成功截圖重新判定 #161。若仍有永久空白，交 dot 取得同一次操作的初始化、導覽、ready 時間與載入後畫面，區分瞬間取樣、頁面橋接與實際視窗顯示。本次 PR 提供回歸保護與調查證據，結案判定交主控。
