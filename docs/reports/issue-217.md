# Issue #217／T122：S206 單參數 overload

## 做了什麼

- 唯讀核對 #132 對照表及伺服器 Java 使用點，單參數版僅見於 `L1MoveX.java:53` 的 GM 測試移動；來源 SHA-256 與線上格式記於 [map-id.md](../protocol/map-id.md)。未複製伺服器原始碼。
- `WorldSession.cs` 僅改 `case 206`，交由新增 `MapIdPacket.cs` 驗證。一般 8-byte 封包維持零 padding；新增的 16-byte 變體要求 water=0 且 12-byte 尾段逐 byte 相符。完整驗證後才更新世界，同圖重設行為保留。
- 新增兩種 overload 的合成向量與負例，接到既有 `map-continuity` 測試入口，因此既有 Windows CI job 會執行。未改 `docs/REPORT.md` 或其他模組分支。

## 怎麼驗證（已驗證）

環境：Windows x64、SDK 10.0.300、Python 3.12。此環境 `py` 找不到安裝記錄，實際使用 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe`。

1. `python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`：通過，174 個來源／183 個鎖定檔；只更新 dev lock。
2. `python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-217/NuGet.Config --output scratch/issue-217/build`：產品與 launcher 建置通過。產品有既存 `MagicUi.cs:20` 的 CS8600／CS8602 兩個警告、0 錯誤；launcher 0 警告、0 錯誤。
3. `dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile scratch/issue-217/NuGet.Config`，接著 `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-217/map-checks.json map-continuity`：**6,851 項檢查通過**，其中本次 S206 為 6,723 項。含乾／水旗標、跨圖與同圖清場、尾段各 byte 的所有錯誤值、非零 water、截斷、加長與錯誤封包不變更地圖／角色／物件／目標／epoch。
4. `python -B -X utf8 engine/tools/check-no-game-assets.py`：通過，0 錯誤、4 個合法 EmbeddedResource。
5. `git -c core.whitespace=cr-at-eol diff --check`：通過，保留既有來源 CRLF。

NuGet 線上還原曾因 Windows TLS 認證不可用失敗；改以 scratch 內的設定唯讀引用主專案 `engine/vendor` 既有 WebView2 nupkg，套件快取設在此 worktree 的 `scratch/nuget-packages`。未在主專案寫檔或複製遊戲資源。Git 傳輸使用單次 `-c http.sslBackend=openssl`，仍驗證 TLS，未修改全域設定。

產品 DLL SHA-256：`abef5a015bf450ab9098ce552974697540ac09929613657ff0bd343d8f0019e5`。

## 未驗證什麼

- 沒有啟動遊戲、伺服器、DB，也沒有實際執行 GM 指令或確認部署啟用情況；合成封包測試不是原生驗收。
- CI 狀態以本 PR 的 GitHub Actions checks 為準，開 PR 後追蹤。

## 產出路徑

- 程式：`engine/source/MapIdPacket.cs`、`engine/source/WorldSession.cs` 的 S206 分支。
- 已納入 repo 的測試：`engine/_integration/dev/demo-tests/MapIdChecks.cs`，由同目錄 `MapContinuityChecks.cs` 掛接。
- 契約：`docs/protocol/map-id.md`；報告：`docs/reports/issue-217.md`；dev lock：`engine/_integration/dev/rebuild-lock.json`。
- 本機暫存證據（不提交）：`scratch/issue-217/map-checks.json`、`scratch/issue-217/build/`。

## 給下一棒的提示

Claude 審查 S206 的嚴格變體辨識與測試後，由使用者合併；如 dev lock 與同期任務衝突，交調度器同步輪重新產生，不手動拼接 hash。原生驗收併入 dot 的 M6 smoke；不要為了驗此變體自行啟動遊戲或伺服器。封包格式未提供尾段語義證據，因此只辨認已查證常數，不延伸接受其他尾段。
