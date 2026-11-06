# Issue #258：T124 記憶座標

## 結論

書籤封包、模型、UI 與離線測試已實作並通過本機建置，但**任務尚未完成，不可合併**：`WorldSession.Reset()` 尚未加入 `ResetBookmarks()`。原任務明確限制 `WorldSession.cs` 只新增 S92 case 與 S64 子型別分派；已在本次對話提出放寬一行的範圍確認，尚未收到答覆。將以草稿 PR 交付可審查成果。

## 做了什麼

- 新增 `Bookmarks.cs`：S64／0x2A 清單原子替換、S92 單筆更新，保存 ID／名稱／地圖／XY，提供 `World.Bookmarks.TryGet(id)` 及 out overload。
- C165 只送目前位置的名稱請求；C3 依伺服器書籤名稱送出。新增等待 S92；刪除無確認回包，因此保留待確認列、停用再次刪除及技能查詢，重新登入清單才調和結果。
- 新增 `BookmarkUi.cs`：玩家「更多」→「記憶座標」分頁，列表、座標明細、新增及刪除；利用本檔 `ClientForm.OnLoad` 掛接，不改 `MagicUi.cs` 或共用 HUD。
- 送出前重驗 session／世界 epoch／清單 revision／角色／位置／動作狀態；等待 sender 時拒絕重複點擊。
- 新測試放 `engine/_integration/dev/bookmarks-tests/`，接上 demo-tests 的專用 `bookmarks`、預設及既有 CI `map-continuity` 入口。
- 依官方 dev 腳本更新滾動 lock；未改封存 lock、`docs/REPORT.md`、原專案或遊戲資源。

## 怎麼驗證（已驗證）

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，175 個 source／184 個 pinned inputs |
| `rebuild.py --build --configfile scratch/issue-258/NuGet.Config --output scratch/issue-258/build` | 產品、audio、launcher 編譯通過；未執行遊戲 |
| demo-tests `bookmarks` | 391 項通過，合成封包／記憶體 sender／獨立控制項 |
| demo-tests `map-continuity` | 519 項通過，包含書籤測試 |
| `engine/tools/check-no-game-assets.py` | 通過，0 errors（提交前會再核對新增檔案） |
| `git -c core.whitespace=cr-at-eol diff --check` | 通過；保留既有 CRLF |
| GitHub CI | 待 PR 建立後檢查 |

環境：Windows x64、SDK 10.0.300、Python 3.12.10。PATH 無可用 `python`，實際使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`。NuGet 線上還原遭本機 TLS 認證錯誤，改以 scratch 中的設定唯讀引用原專案 `engine/vendor`；套件快取與暫存均落在本 worktree。

產品 DLL SHA-256：`9530e60c5c2ee197a194a6c2dd6d95ecd648879528e5090f7f2e1ed7bf55e1b2`。

產品建置有兩個既有 `MagicUi.cs:20` nullable 警告（CS8600／CS8602），沒有編譯錯誤；該檔未修改。

## 未驗證／未完成

- **已知缺口**：`ResetBookmarks()` 目前只有定義，尚未從 `WorldSession.Reset()` 呼叫。斷線／重登／換角色可能保留舊書籤，不能以現有測試綠燈宣稱功能完整；本 PR 應維持草稿。
- 未啟動遊戲、伺服器或 DB；未做登入後顯示、實機新增／刪除、重新登入持久化驗收。
- 未驗證技能 5／69 施放；此張不負責技能串接。
- 本機 GitHub CLI 登入 token 無效（401）；Git HTTPS 讀取使用單次 `http.sslBackend=openssl` 成功，PR 將使用對應 repo 擁有者的 GitHub connector。

## 產出路徑

- `engine/source/Bookmarks.cs`、`engine/source/BookmarkUi.cs`
- `engine/source/WorldSession.cs`：僅新增 S92 case、S64 書籤子型別分派
- `engine/_integration/dev/bookmarks-tests/BookmarkChecks.cs`、`README.md`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`Program.cs`：測試入口
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/bookmarks.md`：格式、伺服器來源 SHA-256、刪除語意及技能交接
- `scratch/issue-258/bookmarks.json`、`map-continuity.json`、`build/`：本機證據，未納入 git

## 給下一棒的提示

1. 調度器／Claude 先確認是否允許在 `WorldSession.Reset()` 加 `ResetBookmarks();` 一行。這是目前唯一已知的實作阻塞；確認後補上斷線／reset／同角色重登清理測試，重新更新 dev lock、建置及測試，才能解除草稿。
2. 法師技能任務用 `World.Bookmarks.TryGet(id)` 取得資料；回傳 null 表示不存在或刪除待確認。唯讀查閱發現 `BookmarkService.getBookMark(pc, i)` 將技能參數拆成 X/Y；請核對實際 C6 消費端，不要直接把清單資料 ID 當傳送參數。
3. dot 在集中 smoke 驗證「更多」→「記憶座標」的 Big5 名稱、新增不可標記地點／重名／容量、刪除待確認、重登清單與跨圖保留。先完成第 1 項才安排跨角色測試。
