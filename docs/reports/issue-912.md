# Issue #912 — T397：R10-M 個人商店收尾

依據：`docs/reference/gap-matrix.md` 15.12.6（R10-M 列）與 15.12.5 的 4:PersonalShopUI 列；原版行為見 `docs/reference/followups-9.md` §9.4（已驗證）。

## 做了什麼

1. **`/shop` 釣魚中印 2120**（`engine/source/Trade.cs`，只改 `ShopChatCommand`）
   - 原版 `sub_F480C0`：擺攤中 → 釣魚中印 2120、否則送 C38 type 1 並印 903；不在擺攤 → 切換設定窗。現在照這個順序做。
   - 釣魚狀態讀 `World.ReadFishing(generation).Active`。原生 `FishingPanel.State` 是同一份狀態經 `FishingWindow.Changed` 送過去的複本（#888 合併後的公開狀態也是從這裡來）；直接讀來源，`--classic-ui` 也能用。`FishingPanel.cs` 沒改。
   - 2120 字串：`PersonalShopText.Fishing`（字串表優先，讀不到時用本機原文「釣魚的時候不可以開個人商店」）。
2. **已開店時 OK 只關窗、Cancel＝關店**（`engine/source/PersonalShopIntegration.cs`）
   - 「已開店」＝自己的 S119 動作是 70（同 `/shop` 的判斷）：新增 `IPersonalShopHost.ShopActive`（預設 false，舊的實作不用改）、`LoginConnection.PersonalShopActive`、產品 host 接上。
   - 控制器新增 `OkWhileOpen()`（只關窗、不送）、`CancelWhileOpen()`（關窗＋送 C38 type 1）；`SendOpen` 開頭也先檢查（保險：已開店時就算 `Confirmed` 被觸發也不重送開店）。
   - `PersonalShopPanel.cs` 不在可改範圍，它的 Cancel、關閉鈕、Esc 都走同一個 `Close()`，分不出來。所以加了 `PersonalShopOpenGuardPanel`（Float 層，`[UiPanel(117)]`／`[UiPanel(118)]` 自動發現，實作 `IUiClosablePanel`），範圍＝設定窗 OK／Cancel 鈕的矩形；**只在設定窗可用、而且已開店時可見**，接手這兩個鈕的點擊（按下與放開都在鈕內才算）。沒開店時看不見、不攔，原流程不變。有其他視窗蓋在設定窗上而且碰到這個鈕時也讓開。關閉鈕與 Esc 照舊只關窗（原版 `ClosePersonalShop`）。
   - `--classic-ui`：沒有原生面板，WinForms 頁的「關閉我的個人商店」鈕照舊。
3. **離線測試** `engine/source/PersonalShopFollowupTests.cs`（`[WorldModuleTest("personal-shop-followup")]`）：2120 字串（無表／有表）；接手面板的註冊、圖層、範圍、命中順序；沒開店時 OK 送 C38 type 0、Cancel 只關窗；已開店時 OK 只關窗不送（含清單空）、Cancel 送 C38 type 1；移出鈕再放開、右鍵不動作；關閉鈕／Esc 只關窗；被其他視窗蓋住時讓開；不能送時只關窗；`/shop` 擺攤＋釣魚 → 2120 不送（含收竿等待確認）、釣魚結束 → 關店、沒擺攤只在釣魚 → 照舊開窗。

## 怎麼驗證

- 已驗證：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile ../.github/nuget.ci.config` → `PASS dev`（worktree 沒有 vendor，本機離線 restore 失敗，改用 CI 的 nuget 設定；不改 lock，用暫時 lock）。
- 已驗證：`engine/tools/check-no-game-assets.py` → `status: pass`。
- 字串 2120 用 `docs/reference/rea-scripts/t680/t680_strings.py` 唯讀讀 `D:\lineage開發\0-客戶端\Text.idx／Text.pak` 的 `string-c.tbl`（decoded SHA-256 `4ebbc1a7a002a4c1dcdfa73dbf056b0e00648e0789763410c40e5079a22d43e9`，與 #847 相同）：2120「釣魚的時候不可以開個人商店」、903「結束個人商店模式。」、902「開始個人商店模式。」。本任務沒有宣稱缺資源，所以沒有做 PakViewer 全 pak 搜尋。
- 單元測試與本組離線測試：依本機限制不在本機執行，交給 PR 的 GitHub Actions CI（`--module-tests`）。

## 未驗證什麼（待驗證／推測）

- 推測：收竿已送、等伺服器確認（`CancelPending`）時仍算釣魚中，`/shop` 照樣印 2120。
- 推測：原版「已開店」看 `& 0x400`，引擎用動作 70（沿用既有判斷）。
- 真機再確認：已開店時 OK／Cancel 由接手面板處理，**這兩個鈕的按下圖（Push）不會畫**（按下狀態在 `PersonalShopPanel` 內部，本任務不能改該檔）；Highlight 也只在滑鼠移過時由設定窗畫。若要完整外觀，之後可讓 `PersonalShopPanel` 加一個「已開店」的 Ok／Cancel 事件，再拿掉接手面板。
- 真機再確認：引擎的釣魚狀態在收到自己的 S119 時結束，所以「擺攤中又在釣魚」只有 S158 動作 71 在 S119 70 之後才會出現；實際伺服器是否會這樣送沒查。
- 沒有開遊戲、伺服器或 DB。

## 產出路徑

- `engine/source/Trade.cs`（只改 `/shop`）
- `engine/source/PersonalShopIntegration.cs`
- `engine/source/PersonalShopFollowupTests.cs`（新檔）
- `docs/reports/issue-912.md`（本檔）

## 給下一棒的提示

- CI 的 `personal-shop-followup` 是本組測試；`personal-shop-flow`、`chat-command-r9c` 的 `/shop` 舊斷言應維持綠。
- 若之後有任務可以改 `NativeUi/PersonalShopPanel.cs`：建議把 Cancel 與 Close 分成兩個事件，OK 不經 `BuildOpen` 也能通知整合端，就可以拿掉 `PersonalShopOpenGuardPanel`，按鈕的按下圖也會回來。
- gap-matrix 的 4:PersonalShopUI 列（15.12.5）兩項 code 差異已補，剩跨窗拖曳（替代做法）與 0xFF 顯示（M38）等真機。
