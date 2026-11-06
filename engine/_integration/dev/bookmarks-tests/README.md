# 書籤離線檢查

`BookmarkChecks.cs` 由既有 `demo-tests/DemoChecks.csproj` 編譯並執行實際產品 DLL；不呼叫遊戲入口、不開視窗、不連網。UI 測試只建立獨立 `BookmarkPanel` 控制項。

先完成 dev `--update-lock` 與 `--build`，再於 repo 根目錄執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/bookmarks.json bookmarks
```

CI 已有的 `map-continuity` 入口及 demo-tests 預設入口也會執行書籤測試，不需要新增 workflow。

涵蓋 C165／C3 Big5 golden bytes、舊名稱刪除、S64 完整／空清單、S92 更新、座標界限、全部有意義的截斷前綴、標頭／筆數／重複 ID／填補錯誤的原子性、S64 裝備分派共存、地圖切換保留、名稱與容量限制、傳送前狀態重驗、重複點擊、無回包刪除狀態與原生控制項。

生命週期回歸使用實際 `WorldSession.Bookmarks`：先建立正常項目與已送出刪除的待確認項目，再執行 `WorldSession.Reset()`、`Cancel()`、切回離線、傳送失敗的取消路徑，以及公開 `SelectCharacter()` 的同角色重登／切角流程。檢查新清單到達前項目與待刪狀態清空、兩種 `TryGet` 都無法取得舊 ID、容量／提示重設、revision 更新、舊操作快照失效；新清單可重新載入同 ID，且不殘留待刪狀態。

跨圖 S206 加自身 S87 回歸保留正常與待刪資料；UI 清理測試改用實際模型取消後的 `BookmarkSnapshot()`，不再直接傳入手工空 Items。斷線以記憶體 sender 拋出傳輸錯誤驗證產品清理路徑，未開 socket，未執行真實 EOF／重連。

第 2 次本機驗證：`bookmarks` 447 項、`map-continuity` 575 項通過。CI 狀態與同步交接見 `docs/reports/issue-258.md`。
