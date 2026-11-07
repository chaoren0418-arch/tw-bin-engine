# 血盟戰爭離線測試

`ClanWarChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯，測試實際 product DLL，不啟動產品主程式、視窗或網路。使用既有 SocketGuard。

先更新 dev lock 並用 `rebuild.py --build` 建置，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/clan-war-checks.json clan-war
```

CI 的 `map-continuity` 群組與無群組的預設入口都接上此測試。

血盟戰爭本身 203 項（`clan-war` 群組回報 479 項，含 276 項共用檢查）：C227 三種子型別與名稱驗證、C121 六種回答逐 byte、S84 全部子型別、S219／217／221／222；截斷、非零填補、多餘區塊、requestId 拒絕；地圖切換保留、重設與重新登入清除、換盟清除、第三方廣播不影響己方、清單上限；十一種排隊失效情境；既有 S219／252、97 分派不變。

不代表真機通過。實際網路、原生 UI、敵對名稱的畫面效果待 dot 驗收。
