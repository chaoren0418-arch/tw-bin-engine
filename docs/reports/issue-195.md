# Issue #195／T107：角色建立、刪除、同連線切角

## 做了什麼

- 本輪依主控指示僅做同步：在前次提交 `bdfe3bd` 上合併 `origin/main`（`bbaac76`），並保留遠端 `task/195` 的同內容同步提交 `a1226fe`。合併無衝突；dev lock 已以 `--update-lock` 重新生成，修正合併後的來源數量；`docs/REPORT.md` 與該主線完全相同，角色管理功能未另行修改。沿用已審查 `10bb9b2` 的 PR #219。
- 原生選角列加入建立與刪除；建立視窗提供七職業、兩性別、名稱及六能力分配，依 #132 與唯讀伺服器來源檢查 Big5 名稱、75 點總和與職業下限。刪除必須確認，預設選「否」，並可取消七日刪除排程。
- `CharacterManagement.cs` 實作 C84／S98／S127、C162／S6＋C16 清單刷新、C7／S250-42／S178／S93。只有收到權威回包才更新清單；切角保留原連線、cipher 與 session，清除舊世界及動作 lease，之後可直接 C137 選另一角色。
- 世界畫面右上角有獨立「切換角色」入口，避免正常 HUD 隱藏登入列後無法操作。等待期間禁止重複與過期操作；15 秒逾時關閉並要求重新登入確認結果，不自動重送破壞性請求。
- `LoginConnection.cs` 僅增加兩個階段名稱、模組接收／生命週期掛接與等待階段判斷，未改寫既有登入或世界回包分支。已用腳本更新 dev lock，未動封存 lock、資源與 `docs/REPORT.md`。

## 怎麼驗證（已驗證）

- SDK 10.0.300：合併後先 `rebuild.py --update-lock`，再 `--build --configfile .github/nuget.ci.config --write-receipt --output scratch/issue-195/build`。180 來源／189 pins，產品與 launcher 建置通過；產品僅既有 `MagicUi.cs` CS8600／CS8602 兩警告，launcher 零警告。218 項模組測試已對合併後重新建置的產品重跑通過。本輪使用本機 Python 3.12 執行檔完整路徑，因 shell 未提供可用的 `python` 命令。
- `CharacterManagementChecks` 218 項通過：七職業／兩性別的封包欄位、Big5 邊界、能力限制、拒絕及成功回包、16／32-bit HP/MP、立即／七日刪除、取消刪除、空清單、截斷／非零 padding／重名／錯序、同 session 切角後 C137，以及排隊取消、同步回呼取消、傳送失敗、逾時與舊 deadline。
- 測試以產品 DLL、合成封包及記憶體 sender 執行；SocketGuard 禁止網路事件。不執行遊戲入口、不顯示遊戲視窗、不連伺服器或 DB，沒有帳密資料。
- `check-no-game-assets.py` 通過：3,286 個追蹤檔、4 個 embedded resource declarations、0 錯誤。來源檔保留既有換行格式，`git -c core.whitespace=cr-at-eol diff --cached --check` 通過。PR CI 最終結果見交付摘要及 GitHub checks。
- 主線帶入的 `LoginChecks` 離線登入回歸通過：396,589 項斷言，涵蓋全部 65,536 個 UTF-16 值；不記錄帳密。
- 既有 `DemoChecks` 離線回歸通過：`map-continuity` 358 項（含本次 main 帶入的公告板測試）、`combat-admission` 212 項、`item-transfer` 73 項；皆為合成封包及記憶體 sender。
- 本機產品 DLL SHA-256：`a195f969ce0a5ee2e48b9a618e071e28d9818c6d19b4078e092933f23e1096d0`。

本機測試重跑方式（先完成上述產品建置；NuGet／TEMP／DOTNET_CLI_HOME 指向 worktree 內）：

```powershell
dotnet restore engine/_integration/dev/character-management-tests/CharacterManagementChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/character-management-tests/CharacterManagementChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- scratch/issue-195/checks.json
python -B -X utf8 engine/tools/check-no-game-assets.py
```

## 未驗證什麼

尚未啟動遊戲、伺服器或 DB，未做真機 UI／鍵鼠、資料持久化及實際切角驗收。本機 Java 來源與部署 JAR 是否一致、龍騎士／幻術師是否開放、ConfigBad 禁用詞、七日刪除門檻仍由伺服器決定；未讀取機密配置。218 項為離線模組測試，既有 CI 工作流程不會自動執行這個新增測試專案。

## 產出路徑

- 產品：`engine/source/CharacterManagement.cs`、`CharacterManagementUi.cs`、`ClientLogin.cs`、`LoginConnection.cs`。
- 測試：`engine/_integration/dev/character-management-tests/`；來源 lock：`engine/_integration/dev/rebuild-lock.json`。
- 協議與來源 SHA：`docs/protocol/character-management.md`；本報告：`docs/reports/issue-195.md`。
- 本機證據：`scratch/issue-195/checks.json`、`scratch/issue-195/build/`、`scratch/issue-195/map-continuity.json`、`combat-admission.json`、`item-transfer.json`（後兩者同目錄），不提交建置產物與 receipt。

## 給下一棒的提示

Claude 審查 PR，CI 綠後交使用者合併；dev lock 若衝突由調度器重新生成，不手動拼 hash。dot 的 M6 smoke 使用可刪除的測試角色：建立（含重名／錯誤名稱）、登入、右上「切換角色」、選另一角色，確認同一連線只有 C7→S250/42→S178/S93→C137，沒有第二次認證／重連；核對舊背包、技能、目標、地圖與音效均已退役。刪除先驗證取消按鈕，再驗證立即刪除與伺服器適用時的七日排程／取消；不要對主要角色操作。補驗 900×650 最小視窗、建立視窗能力分配、選角列捲動與右上切角入口可操作。
