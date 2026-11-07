# #325 T139 釣魚：模組草稿，接收掛接待裁定

## 做了什麼

- 唯讀核對 d287 JAR、釣竿執行器及計時器；確認 C62 只取消、C164/use_type 42 開始，回包為 S158/action 71、S119、S71，沒有該路徑的 S250 子型別。
- 新增 `Fishing.cs`：C62 編碼與受 session／world／actor／revision 保護的傳送、開始等待／結束／訊息狀態、收竿未確認與手動重試；保留原有 C164 座標使用流程。
- PlayerHudView 只加一個掛載呼叫，狀態與訊息顯示、收竿控制都留在 Fishing.cs。
- 測試提交於 fishing-tests，接入 DemoChecks 的 `fishing`、預設與既有 CI `map-continuity` 入口。
- 未修改 WorldSession.cs：任務只允許 S250 子分支，與部署協定衝突，已向使用者提出範圍確認，尚未取得回覆。**產品接收尚未接線，不可當作釣魚完成或合併驗收。**

## 怎麼驗證

- `rebuild.py --update-lock` 後執行 `rebuild.py --build --output scratch/fishing-build`：產品及 launcher 成功；產品 0 errors，2 個原有 MagicUi nullable 警告。
- NuGet HTTPS restore 在受限環境遭 TLS 認證錯誤；僅將主專案既有 WebView2 nupkg 複製至本 worktree 的忽略目錄 engine/vendor，使用既有離線 NuGet.Config 成功建置。未複製遊戲資源。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/fishing-tests.json fishing`：36 項通過。直接 observer 單元測試，涵蓋 C62、既有 C164 DHH、S158、S119、S71 四種訊息、其他 actor、損壞封包、通用403不誤標、重設／換圖、排隊失效、重複點擊與逾時。
- 既有 `map-continuity` 入口：394 項通過（包含新增 36 項釣魚模組檢查）。
- `check-no-game-assets.py`：提交後 3,293 tracked files 通過，0 errors；`git diff --check` 通過。
- 程式提交 `d8ff0ec6ddbc007f7935912cb5f8b2c879c89af2` 的 CI 全綠：[build（dotnet、map-continuity）](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42324541041)、[npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42324541053)。後續提交僅更新本報告，查核時未顯示新的 checks，不能宣稱後續提交已重新跑過 CI。CI 通過也不表示未接線的釣魚流程已完成。[草稿 PR #354](https://github.com/chaoren0418-arch/tw-bin-engine/pull/354) 保持未完成狀態。

## 未驗證什麼

- 未接產品接收掛接，因此尚未驗證由正常 Receive 自動更新釣魚狀態。
- 未啟動遊戲、伺服器、DB；未登入、真機畫面或操作。未查正式設定、機密、DB，也未修改封存、硬連結或原始專案。
- 不推測釣魚週期、漁獲機率與素材名稱對應；403 是通用訊息，無法唯一證明漁獲來源。

## 產出路徑

- `engine/source/Fishing.cs`
- `engine/source/PlayerHudView.cs`（一個掛載點）
- `engine/_integration/dev/fishing-tests/FishingChecks.cs`
- `engine/_integration/dev/demo-tests/{DemoChecks.csproj,Program.cs}`（測試入口）
- `engine/_integration/dev/rebuild-lock.json`（dev 重建）
- `docs/protocol/fishing.md`（格式、JAR class SHA-256、待接線說明）
- 本報告；本機暫存證據 `scratch/fishing-build/`、`scratch/fishing-tests.json`，不提交。

## 給下一棒的提示

Claude／調度器需先裁定：允許 WorldSession.cs 在 ReceiveCore 增加一次 `ObserveFishing(plain);`，取代原先只准 S250 子分支的限制。批准後由 codex-local 加掛接、移除單元測試直接 observer seam，以同一組案例驗證真實接收，再重建 dev lock、測試及 CI；目前應維持草稿 PR，不關閉 #325。真機由 dot 在後續獲授權的 P24Lab smoke 驗證釣竿、水面、魚餌、等待、獲得訊息及收竿。worktree 保留交調度器清理。
