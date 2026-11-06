# T100 離線技能施放測試

測試載入 dev 產品 DLL，使用合成封包與記憶體 sender；不呼叫遊戲入口、不開 socket、不啟動服務或 DB。Windows .NET 10 SDK／Desktop Runtime；完整送出測試唯讀使用既有 `engine/assets`（角色外觀 cadence）。不產生或複製遊戲資源。

先執行 `rebuild.py --update-lock` 與 `rebuild.py --build`，再於 repo 根執行：

```powershell
$env:DOTNET_CLI_HOME = "$PWD/scratch/dotnet"
$env:NUGET_PACKAGES = "$PWD/scratch/nuget"
$env:TEMP = "$PWD/scratch"
$env:TMP = $env:TEMP
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet restore engine/_integration/dev/skill-cast-tests/SkillCastChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/skill-cast-tests/SkillCastChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false
```

產物固定在 `engine/build/skill-cast-tests/`。加 `-- --protocol-only` 可只驗編碼與目錄回退，不讀角色素材；仍需 Windows Desktop Runtime，不能拿來宣稱已在 Linux 執行完整產品測試。

覆蓋：六種尾段、None／Self／Object／Location／Name／Bookmark／Summon、CP950 與 NUL、所有 1–220 id／padding、特殊參數缺漏、已學／未學／移除、屍體、真實送出入口的單次施法與 HP／MP 不預扣、等待 sender 時換技能／目標／地圖／外觀／速度／斷線／死亡／物件移動與重入，以及固定座標與取自物件座標的差別。
