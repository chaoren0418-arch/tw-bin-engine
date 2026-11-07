# T150 小地圖離線測試

測試實作：`MinimapChecks.cs`，連結到既有 `demo-tests/DemoChecks.csproj`，使用實際建出的產品 DLL。

先依專案流程更新 dev lock 並建置，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/minimap.json minimap
```

`scratch/` 須先存在。`map-continuity` 與預設測試入口也會執行本模組，因此現有 Windows CI 的 map-continuity 步驟涵蓋此測試，不需新增 workflow。

涵蓋 51 項檢查：

- 以記憶體合成 S87（自己、NPC、PC 旗標、地面物品）、S10（自己與 NPC 八方向）、S122、S158、S76、S120、S206、S149 驅動既有世界解析器；驗證標記、校正、死亡、外觀更新、移除與切圖。沒有新增 C／S 格式，也不送 C 封包。
- RGB555 每像素比對、黑色與透明區別、圖層覆蓋、負座標、完整 2×2 快取大小、縮圖與既有 Renderer 密集合成結果逐像素對照。
- 同快取重用、revision／快取實例／epoch 變更失效、失敗／不符地圖清除、縮放上下限、關閉／重開／登出／Dispose 釋放。
- F5 焦點政策、HUD 單一掛載點，以及真實 MinimapUi 控制項的離屏繪製；不顯示視窗、不建構 ClientForm、不啟動遊戲。

快取資料完全由程式合成；不需要 `engine/assets`、客戶端或伺服器。測試入口的 SocketGuard 禁止網路。合成資料只能證明離線程式契約，不是實機證據。
