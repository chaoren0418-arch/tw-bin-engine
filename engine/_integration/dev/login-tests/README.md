# T111 登入規則離線測試

直接參考 `engine/build/client-product-dev/P11.ClientEngine.dll`，用合成輸入與無 socket 的 `ILoginSession` 測試替身驗證正式 `LoginPackets`／`LoginControl`。不執行遊戲入口、不開視窗、不連網、不讀環境中的帳密。成功只輸出斷言總數；失敗只輸出固定訊息與已完成斷言數，不輸出輸入值、封包或例外內容。

在專案根目錄，先依 dev 流程更新 lock 並建置產品。以下 PowerShell 將測試暫存與 NuGet 快取限制在目前 worktree：

```powershell
$taskRoot = (Get-Location).Path
$env:DOTNET_CLI_HOME = Join-Path $taskRoot 'scratch/issue-199/dotnet'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'   # #999：避免把 CLI home 的 tools 寫進使用者 PATH
$env:DOTNET_NOLOGO = '1'
$env:TEMP = Join-Path $taskRoot 'scratch/issue-199/tmp'
$env:TMP = $env:TEMP
$env:NUGET_SCRATCH = Join-Path $env:TEMP 'nuget'
$env:NUGET_PACKAGES = Join-Path $taskRoot 'engine/.nuget'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force $env:DOTNET_CLI_HOME,$env:TEMP,$env:NUGET_SCRATCH | Out-Null
dotnet restore engine/_integration/dev/login-tests/LoginChecks.csproj --configfile .github/nuget.ci.config
if ($LASTEXITCODE -ne 0) { throw '測試 restore 失敗' }
dotnet run --project engine/_integration/dev/login-tests/LoginChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw '登入規則測試失敗' }
```

涵蓋：全部 65,536 個 UTF-16 字元的帳號／密碼白名單、空值與長度邊界、全部合法長度組合的 C119 欄位與補齊、帳號小寫化與密碼原樣保留、土耳其語系、登入提示與重試、無效輸入不呼叫 `beforeStart`／`session.Run`、Web 命令轉交與密碼一次性取用。畫面顯示與部署伺服器相容性由 M6 smoke 驗收。
