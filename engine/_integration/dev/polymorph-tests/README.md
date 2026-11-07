# T129 變身離線測試

`PolymorphChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編入，直接呼叫本次建出的產品 DLL 與 `WorldSession.Receive`。每次 DemoChecks 都會先執行基本變身組；既有 Windows CI job 另加 `polymorph-cadence` 步驟，以獨立程序驗證合成動作資料，避免污染其他群組的靜態 profile 快取。

獨立執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-263/polymorph-checks.json polymorph-cadence
```

先用 `rebuild.py --update-lock`／`--build` 建出產品；本機離線環境可改用可讀的 vendor NuGet 設定。輸出目錄須先建立。

測試使用合成 S76 CDHH／CDDHS（涵蓋所有公開 serializer 路徑），以及 S87、S119、S158、S10、S120、S255、S67、S206 交錯序列；無新增 C 格式。檢查 gfx、武器姿勢、後續攻擊、解除、死亡保留、速度值保留、未知物件／ID 重用、重新進入世界、錯誤欄位／截斷／padding 的原子拒絕及 S230 不誤分派。

測試不連網、不啟動產品、不讀遊戲素材；沿用 DemoChecks 的 SocketGuard。基本 `polymorph` 組使用未支援的 Brave 模式驗證動作狀態傳遞，90 項檢查；`polymorph-cadence` 再以 scratch 內自行撰寫的合成 timing／profile 驗證 S30、新舊 gfx／武器動作選取、速度 0／1／2 與後續 S255，總計 123 項。合成 timing 只能證明資料路由與既有速度規則，不能當作實際畫面、原始 frame timing 或可用變身素材的證據。
