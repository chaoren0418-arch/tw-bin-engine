# 書籤離線檢查

`BookmarkChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯並執行實際產品 DLL；不呼叫遊戲入口、不開視窗、不連網。UI 測試只建立獨立 `BookmarkPanel` 控制項。

先完成 dev `--update-lock` 與 `--build`，再於 repo 根目錄執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/bookmarks.json bookmarks
```

CI 已有的 `map-continuity` 入口及 demo-tests 預設入口也會執行書籤測試，不需要新增 workflow。

涵蓋 C165／C3 Big5 golden bytes、舊名稱刪除、S64 完整／空清單、S92 更新、座標界限、全部有意義的截斷前綴、標頭／筆數／重複 ID／填補錯誤的原子性、S64 裝備分派共存、地圖切換保留、名稱與容量限制、傳送前狀態重驗、重複點擊、無回包刪除狀態與原生控制項。

**未完成的生命週期驗收**：`WorldSession.Reset()` 尚未掛接 `ResetBookmarks()`，原因是 #258 限定該檔只改封包分派。391 項通過不代表跨角色資料清理通過；須先確認範圍，再加掛接及 reset／斷線／同角色重登測試。詳見 `docs/reports/issue-258.md`。
