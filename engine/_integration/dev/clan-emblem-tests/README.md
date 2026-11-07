# 盟徽查詢與 ID 對應離線測試（T135，#316）

`ClanEmblemChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯，測試實際 product DLL，全部走正式入口：封包經 `World.Receive`，查詢經 `PumpWorldAsync` 與 `sendMovementGate`。不啟動產品主程式、視窗或網路。

先更新 dev lock 並用 `rebuild.py --build` 建置，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/clan-emblem-checks.json clan-emblem
```

CI 的 `map-continuity` 群組及預設入口都接上本測試。

涵蓋 S64/0x3c（含伺服器註解裡的樣本 bytes）、S167、S118 兩種語意、C72 逐 byte；d287 實際行為（emblem ID 恆為 0）、查一次與快取、沉默未找到、相同數字的 clan ID／emblem ID、換盟與清除、切圖保留、重設清除、排隊失效、數量上限。

不代表真機通過。協定結論見 `docs/protocol/clan-emblem.md`。
