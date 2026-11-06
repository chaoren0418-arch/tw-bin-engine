# Issue #197／T109 通用道具 use_type 尾段

## 做了什麼

- 新增 `ItemUseArguments.cs`，依伺服器背包回報的 use_type 編碼 C164 條件尾段，涵蓋 #132 列出的全部尾段類型。補給品與裝備持續經由原有使用入口。
- 加入選背包物品、點場景物件、點地面、輸入變身名稱及其他尾段所需的參數視窗。U、背包按鈕、HUD 與 Web 共用入口皆先要求參數，確認後才送出。
- 送出前重驗持有道具、選取版本、角色／世界、目標及動作狀態。取消、過期、重複提交與錯誤輸入會拒絕，效果由原服回報。
- 唯讀核對伺服器 handler／executor，欄位結論與 SHA-256 見 `docs/protocol/item-use.md`。

## 已驗證

- SDK `10.0.300`，先執行 `rebuild.py --update-lock`，再執行 `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-197/build`，產品與 launcher 編譯通過。
- `ItemUseChecks` 的 250 項離線斷言通過。內容包括各 use_type 的完整位元組、Big5／UTF-16LE、padding、錯誤參數、未知類型、既有補給品／裝備、真實產品送出入口、HUD 入口、無本地效果預測，以及選取後與排隊中取消／換圖／背包／目標異動。
- 參數視窗控制項在未顯示視窗的測試中可產生各類型參數。測試以合成封包與記憶體 sender 執行產品 DLL，SocketGuard 禁止網路事件。
- `check-no-game-assets.py` 通過，0 errors。
- 既有 `DemoChecks` 的 `combat-admission` 212 項與 `map-continuity` 128 項皆通過，證據分別位於 `scratch/issue-197/combat-checks.json` 與 `scratch/issue-197/map-checks.json`。
- 最終本機產品 DLL SHA-256 為 `12d96751e1220d30bac8010eedd8540cec333ea2518102f63de9422401091e6b`。
- CI 狀態於 PR 建立後補記。

本機重跑測試指令如下，先完成產品建置。

```powershell
dotnet restore engine/_integration/dev/item-use-tests/ItemUseChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/item-use-tests/ItemUseChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- scratch/issue-197/item-use-checks.json
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

請 Claude 審查 `task/197` 的程式與格式對照，再由調度器依流程處理合併及 dev lock 同步。dot 的 M6 smoke 請覆蓋鑑定／武防強化選物品、魔杖或卷軸選世界目標、復活選屍體、釣魚選地面、變身名稱、傳送與取消／換圖後重試。確認目標點擊只進入道具選取流程，並核對實際物品數量與效果回包。
