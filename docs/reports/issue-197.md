# Issue #197／T109 通用道具 use_type 尾段

## 第 3 次嘗試：僅同步與重驗

- 先快轉至遠端 `task/197` 的 `a80c539`，再合併 `origin/main` 的 `a1ecadf`，同步提交 `e4e27f5`，無衝突；未新增功能或修改產品程式。
- 原 CI 的 `dotnet` 工作失敗原因為 `sourceCount 174 != pinned source files 175`。執行 `rebuild.py --update-lock` 後只修正來源計數為 175，184 個 pinned 輸入核驗通過，沒有手動拼接 hash。
- 重跑 canonical build，產品與 launcher 通過；產品仍有既存 `MagicUi.cs` 兩項 nullable 警告。產品 DLL SHA-256：`ee8282998627259654df8d5f4dd63fc9c48da86ad47d3241f5dbc42ac8a73b89`。
- 既有離線測試通過：`ItemUseChecks` 426 項、`slow-actions` 364 項、`combat-admission` 212 項、`map-continuity` 128 項；證據覆寫於 `scratch/issue-197/` 既有檔案。
- `check-no-game-assets.py` 通過（3265 個追蹤檔案、0 errors）；`git diff --check` 通過。
- 本輪產出為 dev lock、本報告及既有 PR #223；`docs/REPORT.md` 只有主線合併內容。本輪最終提交與 CI 結果見 PR 驗收留言。
- 未驗證：原生遊戲操作與效果，留給 dot 的 M6 smoke；本輪未啟動遊戲、伺服器或 DB。
- 給下一棒：Claude／調度器確認 PR #223 最新 head 的 CI 後續審，再依既定流程交使用者合併。

以下保留前輪功能範圍與測試說明；前輪提交及 DLL 雜湊不是本輪產物。

## 做了什麼

- 新增 `ItemUseArguments.cs`，依伺服器背包回報的 use_type 編碼 C164 條件尾段，涵蓋 #132 列出的全部尾段類型。補給品與裝備持續經由原有使用入口。
- 加入選背包物品、點場景物件、點地面、輸入變身名稱及其他尾段所需的參數視窗。U、背包按鈕、HUD 與 Web 共用入口皆先要求參數，確認後才送出。
- 送出前重驗持有道具、選取版本、角色／世界、目標及動作狀態。取消、過期、重複提交與錯誤輸入會拒絕，效果由原服回報。
- 唯讀核對伺服器 handler／executor，欄位結論與 SHA-256 見 `docs/protocol/item-use.md`。
- 第 2 次嘗試：合併主線 `aa20dfa890d0f6f02cd07edba7fffde2818bca87`，解決 `Inventory.cs` 衝突，保留 #191 緩速政策及本次參數選取／取消／送出前重驗流程；dev lock 以 `--update-lock` 重新產生，未手動拼接 hash。同步修復提交為 `9724795`，再合併主線純報告索引更新 `89f5adf`，推送提交為 `dbfb264eed66f639261c6c211628b88e0fcad311`。

## 已驗證

- SDK `10.0.300`，先執行 `rebuild.py --update-lock`，再執行 `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-197/build`，產品與 launcher 編譯通過。
- `ItemUseChecks` 的 426 項離線斷言通過。內容包括各 use_type 的完整位元組、Big5／UTF-16LE、padding、錯誤參數、未知類型、既有補給品／裝備、真實產品送出入口、HUD 入口、無本地效果預測，以及選取後與排隊中取消／換圖／背包／目標異動。本輪增加正常、加速、緩速下的參數送出案例，以及選取後／排隊中切換成未支援速度的拒絕案例。
- 參數視窗控制項在未顯示視窗的測試中可產生各類型參數。測試以合成封包與記憶體 sender 執行產品 DLL，SocketGuard 禁止網路事件。
- `check-no-game-assets.py` 通過，0 errors。
- 同步後重跑 `DemoChecks`：`slow-actions` 364 項、`combat-admission` 212 項與 `map-continuity` 128 項皆通過，證據位於 `scratch/issue-197/` 下同名 JSON。
- 本輪 dev lock 核驗 174 個來源、183 個 pinned 輸入通過；`git diff --check` 通過。`docs/REPORT.md` 僅隨主線合併，沒有本任務的差異。
- 最終本機產品 DLL SHA-256 為 `26738952ad4ea1ec41203d55849c00f8bdf049720b611dc2eead505de94fa2be`。
- [PR #223](https://github.com/chaoren0418-arch/tw-bin-engine/pull/223) 已解除衝突，GitHub 回報 `MERGEABLE`。同步後提交 `dbfb264eed66f639261c6c211628b88e0fcad311` 的 CI 三項全部成功：[dotnet](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42307034929/job/128198863351)、[map-continuity](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42307034929/job/128198863577)、[npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42307034715/job/128198862387)。本報告補件後仍須核對最後 head 的 checks；最後驗收留言記錄完整 SHA 與該輪 CI 連結。

本機重跑測試指令如下，先完成產品建置。

```powershell
dotnet restore engine/_integration/dev/item-use-tests/ItemUseChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/item-use-tests/ItemUseChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- scratch/issue-197/item-use-checks.json
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-197/slow-actions.json slow-actions
```

本輪將 `DOTNET_CLI_HOME`、`TEMP`、`TMP`、`NUGET_SCRATCH`、`NUGET_HTTP_CACHE_PATH` 放在 worktree 的 `scratch/issue-197/` 下，`NUGET_PACKAGES` 放在 worktree 的 `engine/.nuget`。Python 使用環境已提供的 Python 3.12 執行檔。首次 NuGet 還原遇到沙箱 TLS 錯誤，取得沙箱外執行核准後成功下載，後續建置使用工作樹內快取。

## 未驗證與限制

- 原生點擊、焦點／視窗遮擋、實際物品作用與原服拒絕回包留給 M6 smoke。所有本機驗證均未啟動遊戲、伺服器或 DB。
- 傳送書籤採手動 ID 輸入，料理與空白卷軸採數字編號輸入，信件提供代碼、收件人、主旨與本文欄位。書籤名稱列表與料理回包選單尚待相關 UI 模組接續。
- 建置有既存 `MagicUi.cs` 的兩項 nullable 警告。
- 新測試專案由上述指令獨立執行，現有 CI workflow 的涵蓋範圍以 PR checks 為準。

## 產出路徑

- 程式 `engine/source/Inventory.cs`、`engine/source/InventoryUi.cs`、`engine/source/ItemUseArguments.cs`
- 測試 `engine/_integration/dev/item-use-tests/`
- 滾動鎖定 `engine/_integration/dev/rebuild-lock.json`
- 協定 `docs/protocol/item-use.md`
- 本報告 `docs/reports/issue-197.md`
- 未追蹤證據 `scratch/issue-197/build/`、`scratch/issue-197/item-use-checks.json`

## 給下一棒的提示

本輪已處理前次退回的同步衝突，保留 #191 政策並重跑兩模組測試；請 Claude／調度器依 PR 最新 head 的 checks 重新審查，再依流程交使用者合併。報告補件提交不修改產品來源；其最新 SHA 與 CI 結果以 PR 最後驗收留言核對，避免報告自我引用提交雜湊。dot 的 M6 smoke 請覆蓋鑑定／武防強化選物品、魔杖或卷軸選世界目標、復活選屍體、釣魚選地面、變身名稱、傳送與取消／換圖後重試，並補驗緩速下使用帶參數道具。確認目標點擊只進入道具選取流程，並核對實際物品數量與效果回包。
