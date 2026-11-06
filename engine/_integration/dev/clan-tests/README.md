# 血盟離線測試

`ClanChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯，測試實際 product DLL，不啟動產品主程式、視窗或網路。使用既有 SocketGuard，禁止 socket 事件。

先更新 dev lock 並用 `rebuild.py --build` 建置，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/clan-checks.json clan
```

CI 原有 `map-continuity` 群組會接著執行本測試，因此無須另建未掛接的測試入口。

涵蓋六種 C 格式、三種 S 格式、Big5／NUL／長度、BMP 各支援位深與方向、截斷與惡意大小、本人／他人血盟更新、舊盟徽清除、快取重設、記憶體傳送、面對面及排隊時身份／世界變動。

不代表真機通過；未包含尚未授權掛接的 S219 入盟確認、S167/S170/S250 成員列表或未知 emblem ID 的自動查詢。詳見 `docs/protocol/clan.md`。
