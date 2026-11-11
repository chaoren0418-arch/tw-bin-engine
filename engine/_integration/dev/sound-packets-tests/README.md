# T105 離線檢查

先在 repo 根目錄執行 `python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`，再執行 `--build --configfile .github/nuget.ci.config --output scratch/issue-192/build`。本測試引用該次產品 DLL，不啟動產品入口。

```powershell
dotnet restore engine/_integration/dev/sound-packets-tests/SoundPacketChecks.csproj --configfile .github/nuget.ci.config
dotnet build engine/_integration/dev/sound-packets-tests/SoundPacketChecks.csproj -c Release --no-restore -o scratch/issue-192/tests/bin
dotnet scratch/issue-192/tests/bin/SoundPacketChecks.dll scratch/issue-192/checks.json
```

只用合成封包／圖像、記憶體音訊派送；不需要 engine/assets，不連網、不開音訊裝置、不啟動遊戲／DB／伺服器。Windows .NET 10.0.300；建置時將 DOTNET_CLI_HOME、TEMP、TMP、NUGET_PACKAGES 指向本 worktree（同時設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`，#999），停用 first-time experience、telemetry 和 ASP.NET certificate generation。

`Feed` 使用正式 `WorldSession.Receive`；另外在跳過 Form 建構子的宿主呼叫實際 `PumpCombatSfx`，驗證接線、單次派送與退役。合成 catalog 只寫 `scratch/issue-192/assets`；148 項檢查涵蓋新舊 S55 分流、S22／S106、去重的兩種收包順序、repeat 保留、邊界及生命周期。這是離線驗證，不代表已通過原生畫面／聽音驗收。
