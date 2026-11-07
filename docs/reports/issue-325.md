# #325 T139 釣魚：第 2 次修正

## 做了什麼

- 修正正常接收不會更新釣魚狀態的問題：`WorldSession.ReceiveCore` 在 S158／S119／S71 共用一處 `ObserveFishing(plain)` 呼叫，使用獨立 reader，繼續原有動作、外觀及訊息處理。依本次重新派工所附退回要求採用已核實 opcode，沒有猜測 S250 子型別。
- 保留前次 `Fishing.cs` 的 C62 收竿、伺服器確認狀態、逾時手動重試，以及 PlayerHudView 唯一掛載點；開始釣魚沿用既有 C164/use_type 42 座標流程。
- 移除 fishing-tests 以反射直接呼叫 observer 的測試旁路，所有合成回包都走正常 `WorldSession.Receive`。新增只觀測一次及既有 S158／S119 處理器相容檢查，保留 S71 原處理器檢查。
- 合入 main `9f62553`，保留好友模組的共用分派與測試入口，釣魚 case 放在好友的 S71 條件分支之後。dev lock 以正式 `--update-lock` 全量重建，未手動拼接雜湊；相對 main 未修改 docs/REPORT.md。
- 再次唯讀核對部署 JAR SHA-256 為 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`，並以 javap 核對 S_Fishing 及 C_FishClick。完整協定與 class 身分見 `docs/protocol/fishing.md`。

## 怎麼驗證（已驗證）

- 先移除測試旁路、使用尚未接線的產品執行測試：於「S158 CDCHH starts own waiting state」確實失敗，證明新版測試可攔截原缺陷。
- `rebuild.py --update-lock` 後執行 `rebuild.py --build --output scratch/fishing-build`：185 個來源、194 個 lock 輸入核驗通過；產品與 launcher 建置成功，產品 0 errors、2 個既有 MagicUi nullable 警告。沿用 worktree 內既有離線 WebView2 套件；快取與暫存均限定此 worktree。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/fishing-tests.json fishing`：39 項通過。涵蓋 C62、C164 DHH、S158、S119、S71 四種釣魚相關訊息、其他角色、截斷／超長／非零 padding、不明參數、換圖／重設、過期排隊、重複收竿、逾時及手動重試。
- 相同測試入口 `map-continuity`：397 項通過，包含 39 項釣魚檢查。`buddies`：169 項通過，另驗證合入的好友共用分派。
- `check-no-game-assets.py` 與 `git -c core.whitespace=cr-at-eol diff --check` 通過；沒有提交遊戲資源或伺服器原始碼。
- 最新提交的 CI 結果待推送後確認，記錄於 PR #354；不沿用前次提交的 CI 結果。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB，未登入或進行真機操作、畫面及音效驗收。未讀機密設定，未修改原始專案工作檔、封存或硬連結；Git worktree 共用中繼資料由 Git 正常維護。
- 不推測釣魚週期、漁獲機率、素材名稱或魚餌模板對應。403 為通用取得通知，僅顯示「釣魚期間收到」，不唯一認定為漁獲。
- S119 不帶停止原因，依伺服器外觀更新結束釣魚狀態。UI 未宣稱獨立的釣魚停止確認封包。

## 產出路徑

- `engine/source/Fishing.cs`、`engine/source/WorldSession.cs`、`engine/source/PlayerHudView.cs`
- `engine/_integration/dev/fishing-tests/FishingChecks.cs`
- `engine/_integration/dev/demo-tests/{DemoChecks.csproj,Program.cs}`（既有測試入口）
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/fishing.md`、本報告 `docs/reports/issue-325.md`
- 本機證據：`scratch/fishing-build/`、`scratch/fishing-tests.json`、`scratch/fishing-map-regression.json`、`scratch/fishing-buddies-regression.json`；不提交。

## 給下一棒的提示

Claude／調度器審查 PR #354 的接收掛接與正常 Receive 回歸測試，核對最新 head 的 CI 後交使用者決定合併。後續由 dot 在獲授權的 P24Lab smoke 驗證釣竿使用、水面選點、魚餌拒絕、等待、取得通知與收竿；本次沒有真機結論。worktree 保留交調度器清理。
