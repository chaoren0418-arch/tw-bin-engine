# P32 首次組建憑證訊息：有限唯讀核對

**指定10個庫目前未發現符合條件的憑證。沒有組建前快照，不能證明從未新增，也不能推論其他帳戶或全機不存在。** 本輪沒有新增、刪除、信任憑證或修改憑證庫；沒有dotnet命令、chain build、網路、私鑰存取或組建／runtime重跑。

目前WindowsIdentity為 `DESKTOP-CV2L01A\CodexSandboxOffline`，SID `S-1-5-21-1644292704-59536754-1411949733-1003`，與主管回報的帳號與SID末碼一致。主管完整SID未提供，未宣稱逐byte比對完整SID。原build-01使用相同exec工具類型與一般subprocess啟動，沒有runas或替代認證；但原記錄沒有Windows token SID，不能回溯確證當時身分。HOME／USERPROFILE覆寫不是WindowsIdentity證據。

於2026-10-29 **15:26:16 +08:00**，以 `X509Store`、`ReadOnly | OpenExistingOnly` 開啟CurrentUser與LocalMachine的My、Root、CA、TrustedPeople、Disallowed。10庫皆成功、0錯誤、0符合憑證。只保留以下篩選結果：ASP.NET marker OID `1.3.6.1.4.1.311.84.1.1`、FriendlyName含ASP.NET、或Subject CN=localhost且NotBefore落在build-01時間前後15分鐘。沒有輸出不相關憑證資料。

精確Root membership使用公開certificate DER SHA256比對兩個Root庫；因沒有符合憑證，本次符合Root成員也是0，**不是對整體信任鏈或其他憑證的信任判斷**。詳見 `store-observation.json` 與唯讀腳本 `inspect-stores.ps1`。

原build-01時間為 **07:08:21.892275–07:08:25.970619Z**，即台北 **15:08:21.892275–15:08:25.970619**。原command.json、stdout.raw、stderr.raw與run.py皆保存byte copy；build-02／03同樣四檔也保存，共12份。每份先核對既有P32 seal內的SHA／size，再核對複本。原stderr確實有 `Installed an ASP.NET Core HTTPS development certificate.`；此訊息不能替代實際新增的before/after證據。

build-02／03 runner snapshots均確認 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false` 與 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`，且以 `env=env` 傳給subprocess。兩值沒有獨立列在當時command.environmentOverrides；本次依據是command綁定的runner snapshot及實際啟動程式碼，沒有補寫歷史收據。兩次原stderr皆0 bytes。見 `later-runner-checks.json`。

原P32 seal維持 `2668b3754b39af60659063ea096439b872c5417584e5cb2c5f1860924be588db`，原封存未修改。本輪Goal工具實讀null；歷史封存goal-readback.json為complete。本輪沒有建立、更新Goal或修改平台儲存。

輸出：`result.json`、`copy-receipts.json`、`store-observation.json`、`later-runner-checks.json`、`artifact-manifest.json`、`verify-delivery.json`。完成後停寫，待主管接回；不進行其他帳戶搜尋或安全設定修復。
