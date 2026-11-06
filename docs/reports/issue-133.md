# Issue #133／T070：NPC 服務與普通肉循環

## 做了什麼

在 `scratch/wt/task-133` 的 `task/133`，以 `origin/main` 的 `1cb4d024` 為基底完成。先於主目錄 fetch、`checkout -B task/133 origin/main`；為讓指定 worktree 使用同一分支，主目錄切為 detached。既有未追蹤截圖與 receipt 保留不動。

唯讀核對本機伺服器 NPC 購買／販售、個人倉庫、私商、直接交易處理器及靜態 SQL。四種服務本來已有產品實作；本次補齊合成封包回歸並修正：

- 重複／晚到 S70、S65 不再重開已提交的 NPC 訂單，也不能蓋過技能／倉庫專用等待。
- S112 完成必須本次已開交易視窗、自己的確認已送出；邀請階段或尚未確認不會誤報完成。
- 新增 Windows `npc-offline` CI，直接測正式產品 DLL。原 Linux build 工作流程未改。
- 提供潘朵拉普通肉 40056 的七段、57 步路線，以及靜態地形與正式快取的可重跑核驗。協議與來源 SHA-256 集中於 [npc.md](../protocol/npc.md)。

只改動 dev 滾動 lock；未改封存 lock、releases、原客戶端、伺服器檔案、機密設定、`docs/REPORT.md`。

## 怎麼驗證（已驗證）

| 檢查 | 結果／證據 |
|---|---|
| 修正前回歸 | 原 DLL 在 `duplicate S70 cannot reopen submitted purchase` 與 `S112 success before S52 is not completion` 分別失敗；修正後通過 |
| dev canonical build | 主程式／audio／launcher 編譯通過，0 error；主程式有2個既有 `MagicUi.cs:20` nullable warnings，launcher為0 warning；輸出在既定 engine/build、bin、obj、launcher 目錄；建置 log 在本 worktree `scratch/build-evidence/` |
| dev 來源 lock | 171 個 source、180 個 pinned inputs 全部符合；沒有改封存 lock |
| NPC 離線測試 | **83 項全部通過**；[offline-checks.json](../../work/issue-133/offline-checks.json) |
| 路線靜態核驗 | SQL 販售與出生點、雙方地形、避開 NPC／門，57 步通過；[route.json](../../work/issue-133/route.json) |
| 正式地圖快取／對話模板 | `OriginalMapCache.Passable/Move` 逐步通過，0 次換區，`pandora` buy 選項可操作；[cache-route.json](../../work/issue-133/cache-route.json) |
| 遊戲資源拒收 | `check-no-game-assets.py` 對含本次新增檔案的 Git index 通過；沒有提交素材或伺服器原始碼 |
| CI | 由 PR 的 `build / dotnet` 與 `npc-offline` checks 留存遠端結果；本機編譯不代替 CI 或真機驗收 |

產品 DLL SHA-256：`5bef20e43c8dcc7e71dbe883a17664a11efe6966c1b30c1b7583332446a4bc61`。

測試覆蓋：NPC 買入扣金幣／入袋與販售；倉庫存入／取出與每列30金幣費用；私商缺號 order 購入；兩個離線客戶端的邀請、接受、報價、雙方確認與物品移轉；普通肉丟1／撿1、丟10／撿10。另含不合法數量、錯誤身分、重送、逾時、取消／拒絕與目標消失。所有資產與金額只在注入權威回包後更新。

重跑一般離線測試（先 canonical build）：

```powershell
dotnet restore engine/_integration/dev/npc-tests/NpcChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/npc-tests/NpcChecks.csproj -c Release --no-restore -- scratch/npc-tests.json
py -3.12 -B -X utf8 engine/tools/check-no-game-assets.py
```

本機請把 `TEMP/TMP/DOTNET_CLI_HOME` 指向專案 `scratch/`，`NUGET_PACKAGES` 指向專案 `engine/.nuget`，並設 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`。Windows 沙箱初始化失敗，本輪執行命令經權限提升恢復；沒有因為該問題改用遊戲／DB 測試。

## 未驗證什麼

未啟動遊戲、伺服器或 DB；未連正式服務，未操作任何角色。合成封包不是 Java 執行、真實成交、跨角色持久化或真機 UI 證據。一般測試不讀遊戲素材；拾取動畫不在此測試範圍。路線的正式快取檢查只唯讀既有 assets junction，不開視窗。

目前測服 SQL／JAR 是否與來源相同、動態角色／怪物阻擋、現場私商與第二測試角色是否備妥、鍵盤移動終點、普通肉購買及四段丟撿的真機截圖均留 T075。協議沒有 request ID，本修正不能識別所有跨次延遲回包。

## 產出路徑

- `docs/protocol/npc.md`：封包、伺服器限制、來源 SHA-256、完整 T075 購肉與服務驗收指引。
- `docs/reports/issue-133.md`：本報告。
- `engine/source/NpcInteraction.cs`、`engine/source/Trade.cs`、`engine/_integration/dev/rebuild-lock.json`：產品修正。
- `engine/_integration/dev/npc-tests/`：離線測試與唯讀路線 checker。
- `.github/workflows/npc-checks.yml`：Windows 離線回歸。
- `work/issue-133/`：最新版離線與路線證據（不是原始遊戲資料）。

## 給下一棒的提示

Claude 審查 PR；使用者合併後，由本機執行器在 T070–T074 合併齊備時只建置一次 dev，交 **dot 執行 T075**，不在本張另開真機測試輪。

普通肉：map0 潘朵拉 70014 `(32645,32951)`；由 `(32600,32940)` 經 `(32600,32946)` → `(32622,32946)` → `(32622,32951)` → `(32631,32951)` → `(32631,32952)` → `(32644,32952)` → `(32644,32951)`。對應 PageDown6、PageUp22、PageDown5、PageUp9、PageDown1、PageUp13、Home1；以座標核對，勿長按越站。完整限制與可重跑命令見協議文件。

買普通肉10個，確認扣款與入袋，再完成丟1／撿1、丟10／撿10；每步保存前後數量與回包。倉庫需等級≥5與取物費；私商需合法擺攤測試賣家；直接交易需兩個測試角色相鄰且相向。以上條件由調度器／dot 準備，不能改 DB 製造通過。
