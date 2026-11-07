# Issue #266／T130：公告板

分支：`task/266`。工作位置：`scratch/wt/task-266`。日期：2026-11-07。

## 做了什麼

- 新增 `Board.cs`：C10／C23／C114／C141／C153 編碼、S68／S148 嚴格解析、列表／分頁／閱讀／發文／刪文流程，以及末端傳送與舊回包防護。
- 新增 `BoardUi.cs`：一般公告板物件沿既有交談入口開啟原生擁有視窗，提供文章列表、上一頁／下一頁／刷新首頁、閱讀、Big5 文字輸入及發文／刪文確認。拍賣板共用 gfx，依精確名稱排除。
- 發文與刪文沒有成功 ACK，提交後只顯示尚未確認，必須手動刷新；不推算金幣或文章結果。一般玩家只能刪除本人文章。
- 共用來源只新增 `WorldSession.cs` 的 S68／S148 各一個 case，以及 `NpcInteraction.cs` 一個公告板分支。其餘為本模組測試、既有測試入口接線、dev lock 及文件；未修改 `docs/REPORT.md`。

## 已驗證／怎麼驗證

1. `rebuild.py --update-lock`：176 個來源、185 個鎖定輸入通過；僅更新 dev lock。
2. `rebuild.py --build --configfile <scratch/issue-266/nuget.config> --output scratch/issue-266/build`：產品與 launcher 均建置成功。SDK `10.0.300`；產品有既有 `MagicUi.cs:20` 的 CS8600／CS8602 兩個警告，0 錯誤。
3. `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-266/board-checks.json board`：**230 項通過**。
4. 同一入口的 `map-continuity` 群組：**358 項通過**，含原有地圖檢查及公告板檢查。新測試亦接入預設測試群組與現有 Windows CI 的 `map-continuity` 步驟，不另改 workflow。
5. `check-no-game-assets.py`：已加入索引的 3262 個檔案、4 個 EmbeddedResource 宣告，0 問題。`git -c core.whitespace=cr-at-eol diff --cached --check` 通過；共用來源沿用原本 CRLF，不為換行重寫整檔。
6. GitHub CI：PR 的 `build` workflow 同時執行 Windows 離線測試與 Linux 交叉建置；最終執行狀態以該 PR checks 為準。

測試使用真正產品 DLL、獨立 byte vectors、合成回包與記憶體 sender，涵蓋全部 5 種 C／2 種 S 格式、CP950／長度、截斷／padding／ID／次序、分頁游標、本人刪文、單次提交、逾時／晚到回包，以及排隊後目標／世界／角色／動作狀態改變。SocketGuard 禁止測試建立網路連線。

本機 NuGet 線上還原遇到 Windows TLS 認證錯誤，改用 scratch 內的 NuGet 設定，**唯讀**引用原專案 `engine/vendor` 的 WebView2 nupkg，套件快取與輸出均在本 worktree。未複製遊戲素材。Git HTTPS 使用單次命令的 OpenSSL backend 避開相同 schannel 問題，不停用憑證驗證。

產品 DLL SHA-256：`412a0322902bc96afa5528bf5de21bbc5e94f790663260411f7332f61f0425b3`。

## 未驗證／限制

- 未啟動遊戲、伺服器或 DB，未登入、未做原生點擊／視窗焦點／解析度驗收，未實測文章與金幣持久化。
- S68／S148 無請求序號，相同公告板／文章的重新請求無法完全區別極晚到的相同回包；本機只接受目前等待種類及身分／游標相符者。
- NPC 辨識來自唯讀 SQL 來源快照，不能宣稱正式或實驗資料庫目前必然相同。GM 代刪與拍賣板不在本次一般玩家 UI 支援範圍。

## 產出路徑

- `engine/source/Board.cs`、`engine/source/BoardUi.cs`
- `engine/_integration/dev/board-tests/BoardChecks.cs`、`README.md`
- `docs/protocol/board.md`：欄位、規則、來源 SHA-256、限制
- `docs/reports/issue-266.md`：本報告
- 本機未追蹤證據：`scratch/issue-266/build/result.json`、建置 stdout／stderr、`board-checks.json`、`map-and-board-checks.json`

## 給下一棒的提示

由 Claude 審查 PR，調度器處理 dev lock 同步，使用者合併。dot 集中 smoke 時，以 P24Lab 測試角色靠近一般公告板，核對開窗、8 篇重疊翻頁／上一頁、Big5 多行閱讀、發文 300 金幣與刷新、本人刪文、他人文章不可刪；同時確認拍賣板不誤入、遠離／換目標／切圖後舊視窗失效。不要以「請求已送出」判定持久化成功。
