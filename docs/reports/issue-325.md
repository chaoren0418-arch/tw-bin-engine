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
- 修正程式提交 `605d5f9a97171ecb36a836be473c71d572730230` 的 CI 全綠：[build（dotnet、map-continuity）](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42331359099)、[npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42331359220)。後續僅本報告補入此證據；最終文件提交的 CI 另在 [PR #354](https://github.com/chaoren0418-arch/tw-bin-engine/pull/354) 核對並記錄，不將這兩筆執行冒充後續提交的結果。

## 同步輪（調度器退回：PR #354 無法自動合入 main）

此節為最新狀態；上方的來源數與測試數是同步前的紀錄。本輪只做同步，釣魚功能未改。

- 合入 origin/main `82d3a45`（分支原落後 158 個 commit）。衝突 4 檔：`WorldSession.cs`、`demo-tests/Program.cs`、`demo-tests/DemoChecks.csproj`、`rebuild-lock.json`。
- `WorldSession.cs`：main 新增的 `case 71:` 與釣魚的 `case 71:` 不能並存，而且玩家查詢處理器可能直接 `return`。因此改成 `case 71:ObserveFishing(plain);if(ReceivePlayerQueryMessage(plain))return;break;`，S119／S158 維持 `ObserveFishing`。已讀過程式碼確認訊息編號不重疊：玩家查詢只處理 166，決鬥只處理 91／93／631／633／634，釣魚只處理 403／1137／1138／1163。
- 測試入口保留 main 全部群組，再加回 `fishing` 群組與 `FishingChecks`。`rebuild-lock.json` 取 main 版本後以 `--update-lock` 重產，未手動拼 hash；`docs/REPORT.md` 未改。
- 驗證：`rebuild.py --build` 成功（197 個來源、206 個鎖定檔；產品 DLL SHA-256 `b792386e…e607`）。離線測試全部通過，數字是該次執行的總檢查數，含每次都先跑的 `PolymorphChecks`：`fishing` 129、`map-continuity` 21297、`combat-admission` 1223、`polymorph-cadence` 123、`buddies` 259、`item-transfer` 163、`player-query` 791、`location-share` 189、`duel` 204。`check-no-game-assets.py` 通過（3355 個追蹤檔、0 錯誤）。
- 同步後最新 head 的 CI 以 [PR #354](https://github.com/chaoren0418-arch/tw-bin-engine/pull/354) 的檢查為準。

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
