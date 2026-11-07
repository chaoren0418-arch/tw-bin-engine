# Issue #377／T145：踩點進入傳送門

## 做了什麼

- 新增 `engine/source/Portals.cs`：讀取現有 map provider 已核驗區塊的 S32／受限 SEG 入口座標，編碼 C219 `HH`，在步行到達後送出。原始 S32 attribute 不是入口旗標，已唯讀核對實際記錄與 SHA-256，詳見 [入口契約](../protocol/portals.md)。
- `OnlineMovement.cs` 只增加一個成功步行掛載點；等待期間的校正、傳送、換圖、斷線等使舊請求失效。轉向、普通格、失敗步行不送 C219；可離開後重新踩入。
- 沿用 S149／C52／S206／S87 與 T110 recovery，未修改 `Teleport.cs`。未改 `docs/REPORT.md`、原專案、releases 或封存 lock；未建立／切換分支或移除 worktree。
- 測試提交在 `engine/_integration/dev/portals-tests/PortalChecks.cs`，透過 `DemoChecks.csproj`／`Program.cs` 接入 `portals` 與既有 `map-continuity` 入口，因此 Windows CI 會執行。

## 怎麼驗證（已驗證）

- .NET SDK `10.0.300`；先 `rebuild.py --update-lock`，183 個來源／192 個 pins；只更新 dev 滾動 lock。
- `rebuild.py --build --configfile scratch/issue-377/NuGet.Config --write-receipt --output engine/build/issue-377`：產品與 launcher 成功。產品保留既有 `MagicUi.cs:20` 兩個 nullable 警告，0 錯誤。
- 本機 NuGet 設定在 scratch，唯讀引用原專案 `engine/vendor`；套件解壓、暫存、建置與測試輸出均在此 worktree。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-377/portals.json portals`：100 項檢查通過。涵蓋 C219 全 bytes／padding／範圍、S32／SEG 可變 section 與截斷／越界、八方向、普通格、轉向、再次進入、等待與 sender gate 失效、壞 manifest 拒絕、既有傳送握手。
- 同一入口改用 `map-continuity`：458 項通過，包含原有洞穴／船運／旅館／地圖生命週期及公告板測試。
- `teleport-tests/TeleportChecks.csproj`：4,369 項 T110 recovery 回歸通過。
- 測試呼叫實際產品 DLL、合成封包與記憶體 sender，啟用 socket guard；沒有呼叫遊戲入口或連伺服器。
- `engine/tools/check-no-game-assets.py`：PASS、0 errors；`git -c core.whitespace=cr-at-eol diff --check` 通過。CI 結果見本 PR 同一 commit 的 checks。
- 最終產品 DLL SHA-256：`94b8e7212d30031be2924475e5c49f322fddef78203b38763ce8e686144ff59b`。

## 未驗證什麼

- 未啟動遊戲、伺服器、DB 或原 BIN；未驗證實際登入、入口畫面、原生 C219 時序、目前 P24Lab 入口表／物品／船班資格。
- 原始來源查核只證明入口記錄位置與格式；合成離線測試不等於實機通過。未載入的 map／尚未支援的格式不會被本模組擴充成可用地圖。

## 產出路徑

- `engine/source/Portals.cs`、`OnlineMovement.cs`。
- `engine/_integration/dev/portals-tests/PortalChecks.cs`、`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`、`rebuild-lock.json`。
- `docs/protocol/portals.md`、本報告。
- 不提交的本機證據：`scratch/issue-377/*.json`、`scratch/issue-377/synthetic-maps/`；建置紀錄與 DLL 身分：`engine/build/issue-377/result.json`。

## 給下一棒的提示

由 Claude 審查 PR，使用者合併；調度器在同步輪處理 dev lock，不手拼 hash。後續由獲授權的 dot 在 P24Lab 集中 smoke：測試 map0 `(32477,32851)` 洞口及 map1 `(32669,32802)` 返回、轉向不重送、離開再踩入、同圖／跨圖後入口索引不殘留。記錄 C29／可能的 C219／S149／C52／S206／S87 與最終原服座標；若 C29 已引發 S149，C219 應被取消，不能把缺少 C219 單獨判成失敗。未收到換圖時不得宣稱傳送成功。
