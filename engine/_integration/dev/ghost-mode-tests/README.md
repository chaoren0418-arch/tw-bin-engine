# 幽靈模式離線測試

`GhostModeChecks.cs` 編入既有 DemoChecks，執行實際產品 DLL。封包為合成資料，sender 僅使用記憶體；沿用 SocketGuard，不建立網路連線、不執行遊戲入口、不顯示視窗。

先更新 dev lock 並建置產品，再執行：

```powershell
New-Item -ItemType Directory -Force scratch/issue-360 | Out-Null
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-360/ghost-mode.json ghost-mode
```

Windows CI 已執行的 `map-continuity` 群組及預設完整入口均包含本模組，沒有額外 workflow。

涵蓋 C173 exact bytes、既有 C125 `ent`、完整 S87（含 CP950 字串、外觀與尾段）、全部 256 種 flags、其他物件隔離、死亡與幽靈區分、逐 byte 截斷與錯誤資料的原子性、N+8 recovery、非樂觀更新、重複點擊、手動逾時重試、session/world/ghost revision stale fence、切圖與重設、舊傳送失敗、回包早於送出完成，以及缺少可渲染外觀仍能離開。

測試不是原服或畫面驗收；實機觀戰入口、伺服器拒絕的動作、離開後回原地及 HUD 排版交由 dot 的 M6 smoke 核對。
