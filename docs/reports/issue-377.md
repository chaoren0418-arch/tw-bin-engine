# Issue #377／T145：踩點進入傳送門

## 做了什麼

- 新增 `engine/source/Portals.cs`：讀取現有 map provider 已核驗區塊的 S32／受限 SEG 入口座標，編碼 C219 `HH`，在步行到達後送出。原始 S32 attribute 不是入口旗標，已唯讀核對實際記錄與 SHA-256，詳見 [入口契約](../protocol/portals.md)。
- `OnlineMovement.cs` 只增加一個成功步行掛載點；等待期間的校正、傳送、換圖、斷線等使舊請求失效。轉向、普通格、失敗步行不送 C219；可離開後重新踩入。
- 沿用 S149／C52／S206／S87 與 T110 recovery，未修改 `Teleport.cs`。未改 `docs/REPORT.md`、原專案、releases 或封存 lock；未建立／切換分支或移除 worktree。
- 測試提交在 `engine/_integration/dev/portals-tests/PortalChecks.cs`，透過 `DemoChecks.csproj`／`Program.cs` 接入 `portals` 與既有 `map-continuity` 入口，因此 CI 的 Windows job 會執行。

## 怎麼驗證（已驗證）

以下數字都是同步輪合入 `origin/main`（`ef1f762`，含門、釣魚、幽靈模式等 #322／#360 一帶的變更）後重跑的結果。

- 同步：`demo-tests/Program.cs` 的 selector 衝突保留雙方（`portals`、`doors`、`pets`、`ghost-mode`、`fishing`），`PortalChecks()` 排在 `map-continuity` 最後、`GhostModeChecks()` 之後（它會切換 fixture 根目錄）；`rebuild-lock.json` 一律由 `--update-lock` 重產，未手拼。同步後與 main 的 `engine/source` 差異只有 `Portals.cs`、`OnlineMovement.cs` 掛載點。
- .NET SDK `10.0.300`；`rebuild.py --update-lock`：202 個來源／211 個 pins；只更新 dev 滾動 lock。
- `rebuild.py --build --configfile scratch/issue-377/NuGet.Config --output engine/build/issue-377`：產品與 launcher 成功，`PASS dev`。
- 本機 NuGet 設定在 scratch，唯讀引用原專案 `engine/vendor`；套件解壓、暫存、建置與測試輸出均在此 worktree。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-377/portals.json portals`：293 項通過（數字含 main 在所有 selector 之前固定執行的 `Pets()`、`PolymorphChecks()`）。涵蓋 C219 全 bytes／padding／範圍、S32／SEG 可變 section 與截斷／越界、八方向、普通格、轉向、再次進入、等待與 sender gate 失效、壞 manifest 拒絕、既有傳送握手。全新環境自建 fixture 只在較早的同步輪本機驗過，這輪由 CI 的全新 checkout 涵蓋。
- 同一專案的 CI selector：`map-continuity` 21,859、`combat-admission` 1,326、`polymorph-cadence` 226、`buddies` 362、`item-transfer` 266、`doors` 514 項，全部通過。`map-continuity` 內含 `PortalChecks()`，所以 CI 會跑到本模組。
- `teleport-tests/TeleportChecks.csproj`：4,369 項 T110 recovery 回歸通過。`npc-tests/NpcChecks.csproj`：165 項通過。
- 測試呼叫實際產品 DLL、合成封包與記憶體 sender，啟用 socket guard；沒有呼叫遊戲入口或連伺服器。
- `engine/tools/check-no-game-assets.py`：PASS、0 errors、3,379 個追蹤檔。
- 最終產品 DLL SHA-256：`e9b1e5afae30307af8b2179ad9bed357271281f85de880203736ab11f49db038`（`rebuild.py --build`）。

## 未驗證什麼

- CI 結果以 [PR #395](https://github.com/chaoren0418-arch/tw-bin-engine/pull/395) 的 checks 為準，本機全綠不代替 CI。
- 未啟動遊戲、伺服器、DB 或原 BIN；未驗證實際登入、入口畫面、原生 C219 時序、目前 P24Lab 入口表／物品／船班資格。
- 原始來源查核只證明入口記錄位置與格式；合成離線測試不等於實機通過。未載入的 map／尚未支援的格式不會被本模組擴充成可用地圖。

## 產出路徑

- `engine/source/Portals.cs`、`OnlineMovement.cs`。
- `engine/_integration/dev/portals-tests/PortalChecks.cs`、`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`、`rebuild-lock.json`。
- `docs/protocol/portals.md`、本報告。
- 不提交的本機證據：`scratch/issue-377/*.json`、`scratch/issue-377/synthetic-maps/`；建置紀錄與 DLL 身分：`engine/build/issue-377/result.json`。

## 給下一棒的提示

PR 走調度器的閘門合併流程。後續由獲授權的 dot 在 P24Lab 集中 smoke：測試 map0 `(32477,32851)` 洞口及 map1 `(32669,32802)` 返回、轉向不重送、離開再踩入、同圖／跨圖後入口索引不殘留。記錄 C29／可能的 C219／S149／C52／S206／S87 與最終原服座標；若 C29 已引發 S149，C219 應被取消，不能把缺少 C219 單獨判成失敗。未收到換圖時不得宣稱傳送成功。

再次同步時：`portals` selector、`map-continuity` 裡的 `PortalChecks()`（維持最後）與 csproj Include 都要保留。`rebuild-lock.json` 就算 git 自動合併成功也要重跑 `--update-lock`，這次自動合併的 lock 就是過時的。另外 worktree 共用 refs，其他任務 fetch 後 `origin/main` 會在工作途中前進，push 前要再確認一次落後數。本機 `python` 不在 PATH，用 `%LOCALAPPDATA%\Programs\Python\Python312`。
