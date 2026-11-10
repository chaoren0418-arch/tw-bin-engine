# Issue #853 — T369：R9-I 私商查詢頁（code）

依據：`docs/reference/followups-9.md` 第 15 節（3:C_QUERY_PERSONAL_SHOP，K5）、第 16.2 節 R9-I；`gap-matrix.md` 第 15.11 節。

## 做了什麼
- **點店主 → 原生 HyperText 開本機 `usershop` 頁**（`engine/source/PrivateShopUi.cs`）
  - 新的 `OpenUserShopPage(owner)`：右鍵店主或按「與選取目標對話」時，`OpenPrivateShopFromTalk` 先照舊做 `privateOwner`（選店主、不送封包），接著用 `dialogs/templates.json` 的 `usershop` 模板開頁，`#0`＝店主名（`ResolveNpcText` 解析過的名字）。和原版 `sub_5001C0` LABEL_46 一樣，開頁本身不送封包。
  - 頁有 HTML 文件、而且文字和選項數都對得上時，用 R9-F 的行內排版器畫：兩顆圖片鈕（`#312`→surf 1109、`#314`→surf 1107）排在文字流裡；缺圖時用 R9-F 既有的文字標籤代替。
  - **`userShop0`／`userShop1` → C47**：`RunUserShopLink` 取動作字串第 8 個字元（`UserShopPageModel.ActionSell`：`'0'`→買、`'1'`→賣，其他字元不送），走原有的 `RunPrivateShop("privateQuery")` → `LoginConnection.SendPrivateShop` → `PrivateShopPackets.Query(owner, sell)`（`2F type owner`）。版本、距離、走路中、查詢未結案等擋法全部沿用原有邏輯。兩顆鈕能不能按＝`CanQueryBuy`／`CanQuerySell`。
  - **關頁條件**（每格 `SyncUserShopPage`，只在本機關、不送封包）：店主不再是目前目標、世界或目標版本改變、店主看不見／收攤／死亡、離開世界、原生 TRADE_WINDOW 打開私商清單（`PrivateShopLegacyYields`，對應 `sub_A58980` 會關掉頁名含 usershop 的頁），或同一個窗出現新的 NPC／查詢頁。另外 ESC、關閉 X、堆疊擠出也會關頁。
  - **單窗**：原版共用同一個 HYPER_TEXT_WINDOW。因為不能改 `NpcUi.cs` 的頁面分流，所以另外註冊一個包住 `HyperTextPanel` 的面板 `UserShopPagePanel`（`[UiPanel(174)]`，Id `USERSHOP_HYPER_TEXT`，版面同樣是 `uiextra86`，實作 `IUiClosablePanel`／`IUiMovablePanel`／`IUiTextInput`）。開私商頁時關掉原生窗上的 NPC／查詢頁；之後有新的 NPC／查詢頁出現，就關掉私商頁，用這種方式模擬「同一個窗」。
  - **WinForms 降為備援**：沒有原生窗（`--classic-ui`、原生 UI 故障）或沒有 `usershop` 模板時，維持原本的做法，切到「個人商店」分頁；分頁本身和 web 面板都沒有改。
- `engine/source/PlayerPointerUi.cs`：右鍵店主的分支只補了註解，說明新的流程；行為由 `OpenPrivateShopFromTalk` 決定。
- 新檔 `engine/source/UserShopPageTests.cs`（`[WorldModuleTest("usershop-page")]`）：
  動作字串對應；用合成模板開頁（`#0`、兩個選項、行內文件、目標或版本改變就失效、可按條件、非 userShop 動作停用）；面板（合成 XML／圖、固定字寬：點兩顆圖片鈕會把對的選項交給宿主、不能按的鈕不動作、ESC／Close／離開世界會關頁、關掉的同一頁不會再浮出）；用合成店主送 C47（`userShop0` 送 type 0、`userShop1` 送 type 1，和 `PrivateShopPackets.Query` 逐 byte 相同，送出後進入 querying）；自動發現註冊；ClientForm 接線（反射檢查）。

## 怎麼驗證
- **編譯**：`dotnet build engine/source/OfflineClient.csproj -c Release`，**0 個錯誤**，本次改到或新增的檔案沒有任何警告。本機有既有的環境問題 NETSDK1130（WebView2 `.winmd`，#676–#689 都遇過，和本任務無關），所以編譯時暫時加了只放在 `scratch/task853/nowinmd.targets` 的設定（沒有入 git）：拿掉 winmd，改參照從主專案 `engine/vendor` 的 nupkg 解出來的 WebView2 DLL。restore 用的是 CI 的 `.github/nuget.ci.config`。
- `python engine/tools/check-no-game-assets.py` → `pass`（4364 個追蹤檔，0 個錯誤）。
- **離線測試**：依本機執行限制，**沒有在本機執行**（不跑 `--module-tests`）。交給 PR 的 GitHub Actions CI（`World module extension tests` 會跑所有 `[WorldModuleTest]`，包含新的 `usershop-page`）。
- 沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`NpcUi.cs`、`HyperTextPanel.cs`、`rebuild-lock.json`。

## 未驗證什麼（待驗證，交給真機）
- 真機上點店主：私商頁的位置和外觀是否和原版一樣（目前用 uiextra86 的 HYPER_TEXT_WINDOW 版面）；兩顆圖片鈕的圖（1109／1107）。
- **圖號差異（真機再確認）**：followups-9 記載本機 `Text.idx` #36115 `usershop-c.html` 用的是 `#602`／`#603`，但引擎素材 `releases/r29/assets/dialogs/templates.json` 的 `usershop` 用 `#312`／`#314`。引擎照 templates.json 畫。要不要改成讀 Text.idx 的版本另案處理；本組沒有宣稱任何資源「沒有」，所以沒有做 PakViewer 缺圖查詢。
- 原版點鈕之後頁面是否保留到 S140 回來；目前保留到 TRADE_WINDOW 開啟或其他關頁條件成立（推測）。
- 動作名不分大小寫、只接受 `'0'`／`'1'`（推測）。
- 原版是同一個窗；引擎用兩個面板互相關閉來模擬，所以拖曳後的窗位置不會在私商頁和 NPC 頁之間共用（引擎差異）。

## 產出路徑
- `engine/source/PrivateShopUi.cs`（`UserShopPageModel`、`UserShopPagePanel`、ClientForm 的 `OpenUserShopPage`／`SyncUserShopPage`／`RunUserShopLink`／`UserShopPageHost`）
- `engine/source/PlayerPointerUi.cs`（只有註解）
- `engine/source/UserShopPageTests.cs`（新檔）
- `docs/reports/issue-853.md`（本報告）

## 給下一棒的提示
- 之後如果允許改 `NpcUi.cs`／`HyperTextPanel.cs`，可以在 `HyperTextSource` 加一個 `UserShop`，讓 `SyncNativeHyperText` 的 router 直接接管這一頁，再把 `UserShopPagePanel` 拿掉，就是真正的單窗。
- 真機測試：右鍵店主 → 看到「〈店主名〉的個人商店／歡迎光臨～」和兩顆圖 → 點左邊（userShop0）應該開 TRADE_WINDOW 模式 13（買），點右邊（userShop1）應該開模式 14（賣）；用 `--classic-ui` 時應該照舊切到 WinForms「個人商店」分頁。
