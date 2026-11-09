# Issue #620（T259）R2-N 個人商店開店設定窗接上產品

依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-N（T244／#601）；WIN§1.1 `PersonalShopUI`；SP C38（C_Shop）。

## 做了什麼
- 註冊：同步 main 後 `UiPanelRegistry.cs` 已改為 T270（#645）`[UiPanel]` 自動發現，所以**不改 `UiPanelRegistry.cs`**；改在 `PersonalShopIntegration.cs` 宣告 `[UiPanel(115)] PersonalShopPanelRegistration.Registration`（`PersonalShop_Window`／`PersonalShopUI`，排在 TRADE_WINDOW 110 之後、Friend 120 之前）。
- 新檔 `engine/source/PersonalShopIntegration.cs`：
  - `IPersonalShopHost`＋`PersonalShopController`（不依賴視窗，可離線測試）：
    - `Attach(panel)`：設 `IconSource`（物品圖示＝背包同一來源 `ClientForm.ItemIcon`），訂 `Confirmed`；換面板時退訂舊的。
    - `Open()`：在世界中、有面板才開窗；面板用控制器的 `PersonalShopSetup`。
    - `Place(price,count)`：面板點的格子（`Setup.Selected`）＋背包目前選取物品（`World.SelectedItemId`）→ `WorldSession.PersonalShopCandidate` → `Setup.Set`。`ClearSlot()`、`SetMessages()`。
    - `SendOpen`（面板 OK → `Confirmed`）：只收 C38 type 0；送出前每筆重新以背包核對（用掉、裝備、數量不足就不送）；送出成功 → `OpenSent`、關閉設定窗、保留設定。
    - `CloseShop()`：送 C38 type 1（`PersonalShopPackets.Close()`）。
    - 連線世代改變（重登／換角）→ 清設定、關窗；跨世代的送出結果不算成功。
  - `LoginConnection.SendPersonalShop(byte[])`：只放行 C38 type 0／1；經 `sendMovementGate`，送出前驗證同世代、在世界中、角色與狀態一致、不在傳送中；同時只允許一個。`PersonalShopCanSend`、`PersonalShopCandidate`（在 `CaptureWorld` 內讀背包）。
  - `ClientForm`：在既有「個人商店」頁加過渡控制項（開店設定窗、關閉我的個人商店、單價、數量、放入選取格、清除選取格、兩行訊息＋套用），每 200 ms 把原生面板接上控制器。
- `engine/source/PrivateShopUi.cs`：`InitPrivateShop` 把上述控制項加進頁面並呼叫 `InitPersonalShop()`（一處修改）。
- 新檔 `engine/source/PersonalShopIntegrationTests.cs`：`[WorldModuleTest("personal-shop-integration")]`，40 項。
- 沒有改 `WorldSession.cs`；沒有新的收包（伺服器對開／關店沒有專用確認回包），所以沒用 `[WorldReceive]`。

## 怎麼驗證（已驗證）
- 同步 origin/main（120 commits，含 #645 自動發現）後：`rebuild.py --update-lock` → 336 個來源檔；`rebuild.py --build` → `PASS dev`。
  - productDllSha256 `c5c4e38130acd2653c5950c3b7d0b2ed0228f328a7156187d2e902cf24dbee6c`
- `P11.ClientEngine.exe --module-tests`（照 CI 指令）→ `pass`，46 組、2313 項、失敗 0；`personal-shop` 91、`personal-shop-integration` 40、`auto-discovery` 28（無無效 [UiPanel] 成員）。
- `DemoChecks native-ui-canvas` → `PASS 429`；`native-ui-macro` → `PASS 403`。
- `engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- 測試內容：註冊表一行；無面板／不在世界不能開窗；圖示來源；選格→放入（不在背包、超過持有、單價 0、同物件重複都拒絕；收購可超過持有量）；OK 送出的 bytes 與 `PersonalShopPackets.Open` 逐 byte 相同；背包變動後不送；不能送／送出被拒時窗不關；關店送 C38 type 1；換世代清空；換面板後舊面板 OK 不送；`LoginConnection.SendPersonalShop` 不在世界不送、非 C38／type 2 拒絕、在世界時 type 0／1 都送出。

## 未驗證／推測
- **未做真機**：沒有啟動遊戲、伺服器或 DB；原 `PersonalShopUI.xml` 在產品裡實際載入與畫面待真機驗收。
- **推測**：OK 送出成功後關閉設定窗並保留設定；開窗入口（原版從哪裡開）未查證，這裡由過渡頁按鈕開窗。
- **過渡（非原版）**：單價／數量與訊息文字由舊 WinForms 頁輸入；原版的拖放、單價／數量輸入窗與 IME 文字輸入未查證，沒有做假畫面。
- 伺服器是否回 S_DoActionShop 給自己、本端是否要據此顯示開店狀態，未查；`OpenSent` 只代表「已送出」。

## 產出路徑
- `engine/source/PersonalShopIntegration.cs`、`engine/source/PersonalShopIntegrationTests.cs`
- `engine/source/PrivateShopUi.cs`（`UiPanelRegistry.cs` 最終與 main 相同）
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- 分支 `task/620`

## 給下一棒的提示
- 真機驗收（dot／P24Lab 測試角色）：`--native-ui` 進世界 → 「個人商店」頁按「開店設定窗」→ 點出售格 → 背包選物品 → 填單價數量按「放入選取格」→ 窗內 OK；看角色是否進入開店狀態、其他角色能否查到；再按「關閉我的個人商店」。
- 之後若查到原版開窗入口（指令或選單）與拖放流程，可把 `PersonalShopController.Open/Place` 接到原生事件，移除過渡控制項。
- 伺服器 S_DoActionShop 若有回給自己，可加 `[WorldReceive]` 把 `OpenSent` 改成以回包確認。
- worktree 的 `engine/vendor` 是我建立的 junction（指向主目錄 `engine/vendor`，供離線 restore）；清理時只能用 `cmd /c rmdir`（不加 /s）。
