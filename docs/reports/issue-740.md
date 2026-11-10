# Issue #740：T313 R5-H 地圖窗發送座標

## 做了什麼
- `engine/source/NativeUi/FieldMapPanel.cs`
  - 新增 TransferMenu 入口：在 Field_Map 視窗上**按右鍵**（不在關閉鈕／放大鈕上）＝ TransferMenu，打開發送座標的輸入提示。
  - `TransferHandler`（執行期由清單面板裝上）與 `TransferMenu(commands)`；視窗沒開、沒有目前地圖、沒裝入口時都不動作。
  - 放大鈕的註解改成依 Q4-9：原版的 `WorldMapWindow`／`VillageMapWindow` 從來沒有建立，所以點了只吃掉事件、不開任何視窗。
- `engine/source/NativeUi/LocationListPanel.cs`
  - 新增 `MapSendEntry`：TransferMenu 會打開原生聊天輸入列（`IUiChatCommands.BeginChatInput`），填入 `/loc `。之後輸入角色名稱、按 Enter，就走既有的 `ChatPanel` → `SendChat` → `SendPlayerChat` → `SendLocation`，沿用同一套 session／世界／角色戳記、冷卻與 sender gate，**沒有另寫送包**。ESC 照聊天列的規則取消。
  - 清單面板的註冊工廠多傳 `root.Find`；每幀把入口裝到 Field_Map（只裝一次），並更新聊天面板與「可否發送」。
  - 輸入中（輸入列是 `/loc ` 開頭）時，清單最上面多一列提示 1785（`LocationShareProtocol.PromptText`；沒有字串表時顯示編號占位）。這一列只有文字、不能點，寬 420。清單本身的行為不變（只用 anchor 建構時沒有入口，R2-M 原有測試的條件都不變）。
- 新檔 `engine/source/MapSendTests.cs`（`[WorldModuleTest("map-send")]`，自動發現）：註冊、入口安裝、右鍵 → `/loc `、1785 提示列（位置、只有文字）、Enter 送出 `/loc 名稱`、ESC 取消、有草稿時會被取代、各種拒絕情況（不能發送、聊天列不讓開、沒有聊天指令、離開世界）、左鍵／窗外右鍵不觸發，以及 S187 的註冊。
- op 132 S_SHOW_MAP（PG 任務 D）：PG§5 推定它在 3.81 對應 **S187 HouseMap**，這已由 W2-C（#550）的 `Agit.cs`／`AgitMapPanel`（`AgitMapWindow`）接收並開窗，本張不重做；測試只確認 `WorldSession.ReceiveAgitHouseMap` 已註冊在 187。
- 沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`、`LocationShare.cs`（T290）或 T273 的檔案。

## 依據與推測
- 已驗證（WO §5）：地圖視窗事件表 `sub_8DE770` 註冊了 `TransferMenu`；點擊後 `sub_8E0620` 在聊天輸入列開提示 1785，Enter 由 `sub_8D4A90` 送 C_CHANNEL 55/11（＝3.81 C254/11）。
- 推測：解密後的 UI XML 沒有找到哪個控制項綁 `TransferMenu`，所以引擎先用「地圖視窗右鍵」當入口。查到原版的控制項之後，只要改 `FieldMapPanel.Mouse` 就好。
- 推測：原版的提示是輸入列本身的提示狀態；引擎改用 `/loc ` 前綴，加上清單面板上的 1785 提示列。輸入列原本的草稿會被取代。
- 原生 UI 關閉（WinForms HUD）時，入口仍是聊天指令 `/loc 名稱`，沒有變。
- `MapCommunicationSelectWindow`（0x137AFA8）的版面與 `MapLocationSaveWindow`（SaveMenu）都沒有實作新視窗：前者的入口用上面的方式補上，後者不在本組目標內。

## 怎麼驗證
- `python -B -X utf8 engine/tools/check-no-game-assets.py` → pass（errors 0）。
- 本機 `dotnet build`（含 CI 的參數）和 `rebuild.py --build` 都卡在本機環境：`Microsoft.Web.WebView2` 1.0.4258.31 的 build targets 沒有被 import，出現 NETSDK1130 `Microsoft.Web.WebView2.Core.winmd`。全域快取和專案 `scratch/` 下新還原的套件目錄都一樣，跟本次改的程式無關（main 的 CI 編譯是成功的，只有預期中的 strict lock check 失敗）。所以**編譯與測試交給 PR 的 CI**（Windows job 的 `--module-tests` 會跑 `map-send` 和既有的 `location-share-followup`）。
- 依規定沒有在本機執行任何編譯出來的程式。

## 未驗證什麼
- 本機編譯沒有成功（環境問題，見上）；以 PR CI 的結果為準。
- 真機：在 Field_Map 上按右鍵 → 聊天列出現 `/loc `、1785 提示列的位置與字樣、輸入名稱按 Enter 後對方收到 1784（待 Codex／dot 在 P24Lab 驗收）。
- 原版 TransferMenu 實際綁在哪個控制項、提示 1785 的畫法（待查證）。

## 產出路徑
- `engine/source/NativeUi/FieldMapPanel.cs`、`engine/source/NativeUi/LocationListPanel.cs`、`engine/source/MapSendTests.cs`、本報告。

## 給下一棒的提示
- 查到原版綁 TransferMenu 的控制項（例如地圖視窗的選單或按鈕）時，只改 `FieldMapPanel.Mouse` 的觸發條件；送出流程（`MapSendEntry`）不用動。
- 本機 WebView2 套件還原異常（`nuget.g.targets` 沒有 import WebView2 的 targets），需要由調度器或本機對話查清楚（可能是 NuGet／沙箱設定）。在那之前，本機編譯檢查無法進行。
- 真機驗收步驟：開 Field_Map（Ctrl+M）→ 在地圖上按右鍵 → 確認聊天列是 `/loc `、右側黃字是 1785 → 輸入測試角色名稱，按 Enter → 對方聊天列出現 1784 行，清單多一筆。
