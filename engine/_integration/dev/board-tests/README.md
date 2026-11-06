# 公告板離線測試

`BoardChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯，執行真正的產品 DLL；所有封包為測試生成、sender 只存記憶體。沿用入口的 SocketGuard，不建立連線、不啟動產品入口、不顯示視窗。

完成 dev 建置後：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/board-checks.json board
```

Windows CI 的既有 `map-continuity` 群組及預設完整測試都會執行本模組，無須新增 workflow。單獨重現用 `board` 群組；JSON 路徑的父目錄需先存在。

涵蓋五種 C 封包的獨立 byte vectors、兩種 S 封包、CP950 與長度邊界、逐 byte 截斷、錯誤前綴／padding／ID／次序、NPC 路由、重疊分頁與上一頁、空清單、作者權限、異動單次提交、錯誤／晚到回包、逾時、關閉／重開、排隊後換目標／移除／切圖／死亡／離開距離／斷線／交易／麻痺／移動／關閉、滑鼠末端檢查及傳送失敗。

此測試不宣稱原生視窗或原服操作已驗收。
