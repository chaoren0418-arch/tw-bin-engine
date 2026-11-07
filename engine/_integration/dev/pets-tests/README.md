# 寵物離線測試

`PetChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編入，執行建置後的產品 DLL。DemoChecks 各群組前皆執行本模組，因此既有 Windows CI 的 map-continuity／combat-admission 入口會執行，不需改 workflow。涵蓋 C34、C103、C104、C125、S83、S176、S87、S237、S39，以及倉庫分流、截斷、重複ID、索引、背包重排、物件重建、換圖、斷線。SocketGuard 禁止測試建立網路連線。

先更新 dev lock 並建置產品，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/pets-checks.json pets
```

第二次執行已加入真正經 `WorldSession.Receive` 的 anicom／moncom、C104 成功回覆、一般 NPC H旗標與 RaiseAttr 相容案例。目前 S39 分派仍待範圍裁定，測試會在第一個 anicom 回覆以 `ProtocolException: dialog arguments` 失敗；這是未修好的產品缺口，不可跳過或宣稱綠燈。其後案例尚未執行完成。

詳見 `docs/protocol/pets.md`。不啟動遊戲、不建視窗、不連伺服器或 DB。
