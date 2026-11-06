# 寵物離線測試

`PetChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編入，執行建置後的產品 DLL。DemoChecks 各群組前皆執行本模組，因此既有 Windows CI 的 map-continuity／combat-admission 入口會執行，不需改 workflow。共 52 項，涵蓋 C103、C104、C125、S83、S176、S87、S237、S39 純解析，以及倉庫分流、截斷、重複ID、索引、背包重排、物件重建、換圖、斷線。SocketGuard 禁止測試建立網路連線。

先更新 dev lock 並建置產品，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/pets-checks.json pets
```

限制：S39 目前只測 `Pets.cs` 解析器，尚未掛接世界分派；此套件綠燈不表示 C104 的實際回覆鏈已完成。詳見 `docs/protocol/pets.md`。不啟動遊戲、不建視窗、不連伺服器或 DB。
