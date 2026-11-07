# T166 離線檢查：地圖事件 `^` 環境音

`MapAmbientChecks.cs` 經 `demo-tests/DemoChecks.csproj` 編譯，直接引用本次產品 DLL，不啟動產品入口、視窗、網路、伺服器或 DB，也不播放聲音（用記錄器實作 `IAmbientLoopAudio`）。

先依專案入口更新 dev lock 並建置產品，再執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/map-ambient.json map-ambient
```

`map-continuity` 群組（CI 會跑）與預設群組也呼叫本模組。

涵蓋：音量公式對照表與曼哈頓 14 邊界、`d²`、token 解析（模式、5 個候選上限、0 略過）、24 tick/s 進幀、循環不重播、每個 id 全域 200 ms 冷卻、5 槽依 `d²` 排序與換入／拒絕（含等距）、物件移除與換圖時停止、走遠只降音量不停止、音量 0 不啟動、啟動失敗不動槽表、靜音、沒有自身角色、多候選抽選、跨多幀只補一個循環、沒有後端時掛載點不建立狀態。

本模組沒有 C／S 封包（觸發來源是地圖特效影格，不是伺服器封包），測試用合成的 `MapEffectAppearance` JSON，不讀遊戲素材。
