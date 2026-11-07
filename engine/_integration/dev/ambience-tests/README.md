# T147 離線檢查

`AmbienceChecks.cs` 經 `demo-tests/DemoChecks.csproj` 編譯，直接引用本次產品 DLL，不啟動產品入口、視窗、網路、伺服器或 DB。

先依專案入口更新 dev lock 並建置產品，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/ambience.json ambience
```

既有 `map-continuity` 群組及預設群組也呼叫本模組，因此 Windows CI 會實際執行，不只是編譯。

涵蓋 S123／S115／S40 合成封包、每個截斷長度／非零 padding／額外區塊、無號時間與跨午夜、6 倍時鐘、船班與 ready probe、全部 256 天氣碼、S87 光源、S120／S206／Reset 生命週期、相機位置／縮放、實際 GDI bitmap 的暗化／光圈／雨雪動畫／clip 保護。沒有新增 C 封包格式。

圖形測試底圖為白色合成 bitmap，不能當作原生遊戲畫面證據。
