# T153 暫時能力、酒醉、水中呼吸、刪除技能離線測試

`TimedBuffChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編入，直接呼叫本次建出的產品 DLL 與 `WorldSession.Receive`。入口：`timed-buffs` 群組、不帶群組的預設入口，以及 CI 會跑的 `map-continuity` 群組。

獨立執行（先用 `rebuild.py --update-lock`、`--build` 建出產品，輸出目錄須先建立）：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-416/timed-buffs.json timed-buffs
```

涵蓋 S166 `HCCC`、S188 `HCC`、S103 `DC`、S126 `DH`、S160 兩種長度（tier + 28／32 組 mask）。檢查能力值、計時、解除、到期、其他物件、截斷／padding／多餘資料的原子拒絕，酒醉在 S206、自身加速、Reset 的清除，以及畫面搖晃的掛載點與 `OnPaint` 順序。本模組沒有新增 C 格式。

測試不連網、不啟動產品、不讀遊戲素材。像素測試用的是測試內的小控制項，能證明掛載順序，不能當作實際畫面證據。
