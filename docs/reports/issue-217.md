# Issue #217／T122：S206 單參數 overload

## 做了什麼

- 唯讀核對 #132 對照表及伺服器 Java 使用點，單參數版僅見於 `L1MoveX.java:53` 的 GM 測試移動；來源 SHA-256 與線上格式記於 [map-id.md](../protocol/map-id.md)。未複製伺服器原始碼。
- `WorldSession.cs` 僅改 `case 206`，交由新增 `MapIdPacket.cs` 驗證。一般 8-byte 封包維持零 padding；新增的 16-byte 變體要求 water=0 且 12-byte 尾段逐 byte 相符。完整驗證後才更新世界，同圖重設行為保留。
- 新增兩種 overload 的合成向量與負例，接到既有 `map-continuity` 測試入口，因此既有 Windows CI job 會執行。未改 `docs/REPORT.md` 或其他模組分支。
- 第 2 次嘗試：快轉至遠端 `75741a9`，合併 `origin/main` 的 `25bc0f4`。唯一文字衝突為 dev lock，以 `--update-lock` 重產；`docs/REPORT.md` 僅帶入 main 版本，沒有新增或編輯索引。
- 同步後重跑既有 `teleport-tests`，發現 #198 的一般 S206 recovery 被誤當作單參數版。修正僅限 `MapIdPacket.cs`：完整比對單參數常數尾段，或接受 main 已核准的 water=0/1、12-byte 全零 recovery；其他尾段、長度與重複 wrapper 均拒絕。增加已掛入 CI 的回歸向量，未修改傳送模組或 `LoginReader`。

### 本次同步補驗

- 依調度器「只做同步」回饋，合併 `origin/main`（`c411ddf`）與遠端 `task/217` 的既有同步紀錄（`d2cb969`），沒有新增功能修改。唯一衝突為 dev lock，使用 `--update-lock` 重產，沒有手動拼接 hash。
- 前次 CI run `42317238568` 的 `dotnet` 失敗原因為 `sourceCount 176 != pinned source files 177`；本次 lock 核驗通過，179 個來源／188 個鎖定檔。
- 重跑下列既有命令：產品與 launcher 建置通過（產品仍有 `MagicUi.cs:20` 兩個既存警告、0 錯誤）；`map-continuity` **19,849 項通過**；`teleport-tests` **4,369 項通過**；`check-no-game-assets.py` 通過（0 錯誤、4 個合法 EmbeddedResource）；差異空白檢查通過。
- 本次產品 DLL SHA-256：`bbfedc3fcbd8436f36f2f298a01151f0c20765a0b1daabfd3be5812f7c06b8b4`。證據沿用 `scratch/issue-217/`，下方舊輪數字與 SHA 僅代表前一輪。本次未啟動遊戲、伺服器或 DB；PR #243 最新提交的 CI 於推送後追蹤。

## 怎麼驗證（已驗證）

環境：Windows x64、SDK 10.0.300、Python 3.12。此環境 `py` 找不到安裝記錄，實際使用 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe`。

以下為第 2 輪最終程式的本機重驗結果；另再次唯讀確認伺服器八個使用點及兩份來源 SHA 與契約相符。

1. `python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`：通過，176 個來源／185 個鎖定檔；只更新 dev lock。
2. `python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-217/NuGet.Config --output scratch/issue-217/build`：產品與 launcher 建置通過。產品有既存 `MagicUi.cs:20` 的 CS8600／CS8602 兩個警告、0 錯誤；launcher 0 警告、0 錯誤。
3. `dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile scratch/issue-217/NuGet.Config`，接著 `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-217/map-checks.json map-continuity`：**19,619 項檢查通過**，其中 S206 為 **19,491 項**。含兩種 overload、乾／水 recovery、跨圖與同圖清場、尾段各 byte 的所有錯誤值、非法 water、截斷、加長與錯誤封包不變更地圖／角色／物件／目標／epoch。
4. `python -B -X utf8 engine/tools/check-no-game-assets.py`：通過，0 錯誤、4 個合法 EmbeddedResource。
5. `git -c core.whitespace=cr-at-eol diff --check`：通過，保留既有來源 CRLF。
6. 以相同本機 NuGet 設定 restore 後，`dotnet run --project engine/_integration/dev/teleport-tests/TeleportChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- scratch/issue-217/teleport-checks.json`：**4,369 項通過**，包括產品 DLL 的 recovery 接收流程。

NuGet 線上還原曾因 Windows TLS 認證不可用失敗；改以 scratch 內的設定唯讀引用主專案 `engine/vendor` 既有 WebView2 nupkg，套件快取設在此 worktree 的 `scratch/nuget-packages`。未在主專案寫檔或複製遊戲資源。Git 傳輸使用單次 `-c http.sslBackend=openssl`，仍驗證 TLS，未修改全域設定。

產品 DLL SHA-256：`94606e850cf5da7ff8382594d40fac950432e173a6838220b1b8b671c85a1137`。

## 未驗證什麼

- 沒有啟動遊戲、伺服器、DB，也沒有實際執行 GM 指令或確認部署啟用情況；合成封包測試不是原生驗收。
- CI 狀態以既有 [PR #243](https://github.com/chaoren0418-arch/tw-bin-engine/pull/243) 的新 HEAD checks 為準，推送後追蹤；本機驗證不代替 CI。

## 產出路徑

- 程式：`engine/source/MapIdPacket.cs`、`engine/source/WorldSession.cs` 的 S206 分支。
- 已納入 repo 的測試：`engine/_integration/dev/demo-tests/MapIdChecks.cs`，由同目錄 `MapContinuityChecks.cs` 掛接。
- 契約：`docs/protocol/map-id.md`；報告：`docs/reports/issue-217.md`；dev lock：`engine/_integration/dev/rebuild-lock.json`。
- 本機暫存證據（不提交）：`scratch/issue-217/map-checks.json`、`scratch/issue-217/teleport-checks.json`、`scratch/issue-217/build/`。

## 給下一棒的提示

Claude 審查 PR #243 的嚴格變體辨識、#198 recovery 相容與新 HEAD CI 後，由使用者合併。如 main 再前進而需要同步，重產 dev lock，並重跑 `map-continuity` 與 `teleport-tests`，不能只看編譯。原生驗收併入 dot 的 M6 smoke；不要為了驗此變體自行啟動遊戲或伺服器。單參數常數的語義未查明，因此只辨認已查證格式，不延伸接受任意尾段。
