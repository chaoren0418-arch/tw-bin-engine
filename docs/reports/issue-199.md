# Issue #199／T111：登入密碼字元規則對齊伺服器

## 做了什麼

- 依 #132 差異 6 與 [伺服器規則 R03](../reference/server-rules.md)，帳號限 1–12 位 ASCII 英數字；密碼限 1–13 位 ASCII 英數字與 `!_=+-?.#`。拒絕空值、空白、其他標點、控制字元及非 ASCII 字元。
- `LoginPackets.LoginValidationError` 提供共用規則，`LoginPackets.Login` 與 `LoginControl.Start` 同時使用。登入畫面透過既有 `Notice`／`Changed` 顯示欄位與原因；無效輸入在 `beforeStart` 和 `session.Run` 前返回，不建立登入封包、不送出。
- 保留帳號小寫化、密碼大小寫、C119 欄位與 8-byte 零補齊。訊息不含輸入值或違規字元。未變更共用分派點、連線實作、Web／WinForms 版面。
- 透過 `--update-lock` 更新 dev lock，只有上述兩份來源的 size／SHA 變動；未改封存 lock。

依據來源：`C_AuthLogin.start/checkLogin`，#132 記錄 SHA-256 `1902f2244a893eaa8e9a434ce4293a3cbdfa460a5bef7e8210c8696d428bfaa1`。本次使用已收錄的對照表，未複製伺服器原始碼。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock`：172 個來源、181 個釘選輸入通過。
- `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-199/build`：SDK 10.0.300，產品與 launcher 成功，0 錯誤；產品仍有既有 `MagicUi.cs:20` 的 CS8600／CS8602 警告。
- `dotnet run --project engine/_integration/dev/login-tests/LoginChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false`：**396,589 個斷言通過**。直接測試本次產品 DLL；完整 UTF-16 字元域、長度邊界、全部合法長度組合、封包格式、大小寫／文化設定、提示發布、阻止連線、拒絕後重試、Web 命令轉交均通過。使用無 socket 的測試替身，沒有啟動遊戲。
- `check-no-game-assets.py`：通過，0 錯誤、4 個 embedded resource 宣告。
- `git diff --check`：通過。CI 執行狀態以本 PR 對應提交的 GitHub Checks 為準；新增登入測試由本機執行，既有 CI 工作流程未改動。

本機 `python` 不在 PATH，實際使用 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe -B -X utf8`。建置與測試的暫存、CLI home、NuGet 快取均置於本 worktree。

## 未驗證什麼

未啟動遊戲、伺服器或 DB，未連正式服務；未實際登入、查看登入畫面或核對部署 JAR 與 #132 來源的位元對應。本次離線驗證不代表原生實機驗收。未使用或記錄真實帳密，測試輸出不含合成帳密或封包內容。

## 產出路徑

- `engine/source/LoginProtocol.cs`、`engine/source/LoginControl.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `engine/_integration/dev/login-tests/`（程式、專案與重跑說明）
- `docs/reports/issue-199.md`
- 本機證據（不入 git）：`scratch/issue-199/build/`、`scratch/issue-199/login-checks.log`

來源 SHA-256：

| 路徑 | SHA-256 |
|---|---|
| `engine/source/LoginProtocol.cs` | `b85d6aa6eb165db795d295b19cec3f4321d4e2e014f4de119fa9e777b86c2c9e` |
| `engine/source/LoginControl.cs` | `51407cc9b3e5a91646bf24dba949bea012e1460d690579dea1e76196494120e4` |
| 建置的 `P11.ClientEngine.dll` | `a9eaad6cf84ddb864295dbd9bd25ca0bf5eb691eaac41944ae43f39005464829` |

## 給下一棒的提示

Claude 審查、使用者合併；dev lock 若衝突交由調度器同步輪重新產生，不手工拼接 hash。M6 smoke 由 dot 在既有授權的 P24Lab 測試角色驗證：WinForms／Web 登入畫面對不允許字元顯示原因且未開始連線，修正後可重試；合法符號與混合大小寫密碼可走正常登入。僅記錄結果，不在截圖、報告或追蹤中保存帳密。
