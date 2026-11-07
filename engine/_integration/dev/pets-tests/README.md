# 寵物離線測試

`PetChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編入，執行建置後的產品 DLL。DemoChecks 各群組前皆執行本模組，因此既有 Windows CI 的 map-continuity／combat-admission 入口會執行，不需改 workflow。涵蓋 C34、C103、C104、C125、S83、S176、S87、S237、S39，以及倉庫分流、截斷、重複ID、索引、背包重排、物件重建、換圖、斷線。SocketGuard 禁止測試建立網路連線。

先更新 dev lock 並建置產品，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/pets-checks.json pets
```

第三輪已依裁定接上 S39 分派；真正經 `WorldSession.Receive` 的 anicom／moncom、C104 防具與武器成功回覆鏈、一般 NPC H旗標、相似模板名稱、已開啟 NPC 頁面及 RaiseAttr 相容案例皆執行通過，pets 共 98 項。Follower 合成封包使用合法朝向 3；owner 空白、HP 255、朝向 0 的全零可變欄位會與 main 現有地面分類重疊，此共用分類限制另記於任務報告。

入口捕捉 Pets 例外後仍執行所選後續群組，最後寫入 `status: fail` 並退出 1，不會把失敗當作成功或跳過後續群組。此行為已用修正 fixture 前的實際失敗驗證，map-continuity 與 board 仍執行完成。

詳見 `docs/protocol/pets.md`。不啟動遊戲、不建視窗、不連伺服器或 DB。
