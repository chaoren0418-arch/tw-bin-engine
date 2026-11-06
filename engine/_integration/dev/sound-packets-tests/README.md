# T105 離線檢查

先在 repo 根目錄執行 `python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`，再執行 `--build --configfile .github/nuget.ci.config --output scratch/issue-192/build`。本測試引用該次產品 DLL，不啟動產品入口。

```powershell
dotnet restore engine/_integration/dev/sound-packets-tests/SoundPacketChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/sound-packets-tests/SoundPacketChecks.csproj -c Release --no-restore -- scratch/issue-192/checks.json
```

只用合成封包／圖像、記憶體音訊派送；不需要 engine/assets，不連網、不開音訊裝置、不啟動遊戲／DB／伺服器。Windows .NET 10.0.300；建置時將 DOTNET_CLI_HOME、TEMP、TMP、NUGET_PACKAGES 指向本 worktree，停用 first-time experience、telemetry 和 ASP.NET certificate generation。

目前 `Feed` 直接呼叫本模組接收方法；共用接收與音訊掛接等待檔案範圍確認後，須改用 `WorldSession.Receive` 並重跑，才能宣稱線上接線已完成。
