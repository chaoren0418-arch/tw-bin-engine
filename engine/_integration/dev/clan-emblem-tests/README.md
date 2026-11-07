# 盟徽查詢與 ID 對應離線測試（T135，#316）

`ClanEmblemChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯，測試實際 product DLL：封包經 `World.Receive`，盟徽查詢經真實繪製掛載點 `LoginConnection.ClanEmblemFor` 與 `sendMovementGate`。不啟動產品主程式、視窗或網路；測試期間關閉磁碟快取（`ClanEmblemCache.DiskPath=null`）。

先更新 dev lock 並用 `rebuild.py --build` 建置，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/clan-emblem-checks.json clan-emblem
```

CI 的 `map-continuity` 群組及預設入口都接上本測試。#440 的快取與 C72 送出規則（32 筆待回覆、LRU、磁碟快取）由 `name-display-tests` 測試。

涵蓋 S64/0x3c（含伺服器註解裡的樣本 bytes）、S167、S118、C72 逐 byte；d287 實際行為（emblem ID 恆為 0、不送 C72 0）、有 emblem ID 時用 emblem ID 查詢而不是 clan ID、回覆前顯示 clan 推送圖、沉默未找到與無法解析不重送、換盟與撤銷、無持有者不記錄、切圖保留、重設清除對應但保留圖像、數量上限。

不代表真機通過。協定結論見 `docs/protocol/clan-emblem.md`。