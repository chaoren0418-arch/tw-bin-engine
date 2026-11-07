# 血盟離線測試

`ClanChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯，測試實際 product DLL，不啟動產品主程式、視窗或網路。使用既有 SocketGuard。

先更新 dev lock 並用 `rebuild.py --build` 建置，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/clan-checks.json clan
```

本機可用 worktree 內的 NuGet config 引用原專案 vendor 唯讀來源。CI `map-continuity` 群組及預設入口均接上血盟測試。

本輪血盟 118 項，map-continuity 合計 476 項：六種既有 C 格式與 C121、S72/S118/S200/S219/S167/S170/S250-sub171；Big5／NUL／長度、BMP 位深與方向；本人／他人更新、快取及世界失效；接受／拒絕逐 byte 驗證、九種排隊失效情境、重複送出、成員快照及上線集合、錯誤封包不覆寫既有資料。另驗證既有 S219/message252 與 S250/sub34 分派。

不代表真機通過。盟徽查詢及 ID 對應已拆到 T135（#316），本輪不測自動查詢；實際網路、原生 UI、真實盟徽 payload 待 dot 驗收。
