# Issue #715 報告：T303 R4-C 交易、商店、修理、學魔法窗收尾

依據：`docs/reference/gap-matrix.md` 13.5 節 R4-C 列（T291／#701）；TWP§1、§1.6；WIN§1.5、§2.1；FU5§4（圖示）。
分支：`task/715`。只改了任務列出的檔案，另外新增測試檔 `TradeFollowupTests.cs`。

## 做了什麼

| 項目 | 檔案 | 內容 |
|---|---|---|
| 交易窗物品圖示 | `NativeUi/TransactionPanel.cs` | 註冊改成 `new TransactionPanel(ItemIconSource.FromRoot(root))`（R4-B 的共用來源，執行期從原客戶端 Sprite `<icon>.tbt` 讀，不改 `ItemIconSource`）。有圖示來源時，兩個清單改畫 33 px 圖示格（`LegacyCellGrid.Fit` 填滿清單區，本機版面是每邊 4×2）；S35 的 icon＋bless 照 FU5§4 `IconOf` 轉（487／1617 規則）；堆疊數量畫在格子右下；滑過格子時文字框顯示名稱；捲動以一列格子為單位。沒有圖示來源時照舊畫文字列（R3-E 測試不變）。 |
| 從原生背包加物品（C37） | `TransactionPanel.cs`、`TradeUi.cs` | 在原生 `ITEM_WINDOW` 點選物品（`WorldSession.SelectedItemId`，跟 WinForms 背包共用），再點交易窗自己那一側的清單，就把整疊送出（C37 物件＋數量）。`ITransactionSource.Add` 接到 `Trade.cs` 原本的 `"add"` 入口，資格會再檢查一次。原生窗顯示交易時，WinForms 交易面板整個隱藏，不再留加入列。 |
| 金幣輸入框 | `TransactionPanel.cs` | 框放在上下清單之間的空隙，靠右對齊（本機版面是 (82,124,84,14)）。點框後輸入數字（WM_CHAR → `IUiTextInput.Char`），Enter 送 C37（金幣物件＋數量）。數量上限是背包裡的金幣數，也不超過 `int.MaxValue`；超過上限、0 或空白都不送，輸入的內容保留。Backspace 刪一位；Esc 第一次只是離開輸入框，第二次才照舊送 C86。 |
| `IUiClosablePanel` | `TransactionPanel.cs`、`TradeWindowPanel.cs`、`TradeUi.cs` | 交易窗 `Close()`：先標記 `Dismissed`，可以取消時再送 C86（跟按關閉 X 一樣），之後這筆交易（`TradeGeneration`）結束前不再畫。`TradeUi` 看到 `Dismissed` 就把整個 WinForms 交易面板叫回來，當作原生 UI 下的備援入口。`TradeWindowPanel.Close()` 改成 public（記下這份清單的模式和版本，再交給來源關閉），另外新增 `Dismissed(view)`。 |
| 修理多選每列一包 C20 | `WeaponRepair.cs`、`WeaponRepairUi.cs` | 新增 `LoginConnection.SendWeaponRepairBatch`：送第一包前先驗整批（同一份清單版本、每件都能修），送出後進入 `submitted` 並記一個批號；後面每一包只驗連線、世界、目標、NPC 距離和批號（`WeaponRepairBatchLive`）。如果原服在中途回 464／189（狀態轉成 review），剩下的包照原版一次送完；清單被關閉或重新查詢時批號失效，剩下的包就不送。只選一列時走原本的單件路徑。`RepairTradeSource.Submit` 改成送整批，`RepairTrade.Items` 會把重複和數量 0 的列去掉。TRADE_WINDOW 的圖示改成先用 `ItemIconSource`，讀不到再用舞台的背包 PNG。 |
| 學魔法舊清單讓位 | `SpellExchangeUi.cs` | 原生 TRADE_WINDOW 正在畫 op 44 金幣學魔法清單時（模式 7、版本相同、沒被關掉），這一輪不顯示 WinForms NPC 面板，也就是不顯示舊的數量框清單和提交鈕。下列情況照舊顯示 NPC 面板：原生 UI 關閉、面板故障、沒接上學魔法來源、使用者關掉了這份清單、op 175 清單排在前面、或 WinForms 要同時顯示查詢頁。判斷寫在 `SpellTradeOwnership.Owned`，方便單獨測試。 |

`Trade.cs` 不用改：原本的 `"add"` 入口和資格檢查就夠了。

### 已驗證／推測
- **已驗證（靜態依據，沿用既有文件）**：
  - TRANSACTION_WINDOW 的元件座標（R3-E）。
  - FU5§4 的圖示意義與 487／1617 規則。
  - TWP§1.6：修理是每列送一包 C20。
  - C37 格式：`Trade.cs` 原本就有。
- **推測（程式註解有標明）**：
  - 圖示格的幾何：原版的格子幾何沒有追。
  - 加物品的方式：用「背包點選＋點自己的清單」代替原版的拖曳。原生背包不能跨窗拖曳，而 `ItemWindowPanel` 不在這次可改的範圍。
  - 一次加整疊：部分數量要用金幣框或 WinForms 備援。
  - 金幣框的位置與外觀：原版 `ADENA_EXCHANGE_WINDOW` 的版面沒有接上。
  - 被擠出窗時送 C86：只知道原版會呼叫 vtable+36，這個函式在交易窗裡實際做什麼沒有追。
  - 修理批次在原服中途回訊息時仍繼續送：依據是原版用一個迴圈一次送完。

## 怎麼驗證
- 新增 `engine/source/TradeFollowupTests.cs`，用 `[WorldModuleTest("trade-followup")]` 自動發現，不改 `Program.cs` 和 `DemoChecks.csproj`。全部用合成封包、XML、surf、圖示和記憶體內的送出端。涵蓋：
  - 介面：兩個面板都實作 `IUiClosablePanel`，各只註冊一次。
  - 數量解析與金幣上限規則。
  - 背包選取 → C37 整疊；`adding` 狀態下不重送；點對方清單或沒選物品時不送。
  - 金幣框的焦點、數字、Backspace、Enter → C37；超過持有不送；兩段 Esc。
  - `Close` → 只送一次 C86、隱藏，同一筆交易內不再出現，S112 後清除，下一筆交易照常顯示；取消中被關閉時不送包。
  - 圖示格：4×2 的幾何、捲到最後一頁、滾輪、滑過顯示名稱、像素確實畫出圖示、S35 圖示經過 `IconOf` 轉換。
  - TRADE_WINDOW 的 `Close`／`Dismissed`。
  - 修理批次：兩列送兩包 C20 且順序正確；舊版本不重送；原服中途回 464 時照樣送完；中途關閉時停止；有一列不能修就整批不送；單列走原路徑。
  - 學魔法讓位的判斷，各種情況都有測。
- 既有測試（`transaction-window`、`trade-window`、`shop-trade` 等）沒有改。為了讓它們照原樣編譯，`TransactionView`／`TransactionOffer` 新增的欄位都有預設值，`ITransactionSource.Add` 用預設實作。
- **本機無法編譯**：worktree 用 SDK 10.0.300 建置時，`Microsoft.Web.WebView2 1.0.4258.31` 在參照解析階段報 NETSDK1130（winmd）。改用 vendor、nuget.org 或獨立的套件快取都一樣。這個錯誤出在編譯之前，和本次改動無關。所以**編譯、既有單元測試、本組離線測試、`check-no-game-assets.py` 都交給 PR 的 CI**，結果以 PR 頁面為準。依本機執行限制，沒有在本機執行任何編譯出來的程式。

## 未驗證什麼（待驗證，需要真機）
- 原生 UI 開啟時的實際交易：背包點選後點交易窗會送 C37、原服回 S35 後格子會出現、金幣框可以輸入（含中文 IME 下的數字輸入）、被擠出窗後 WinForms 面板確實回來。
- 修理多選時，原服對連續幾包 C20 的實際回應（每包一則 464，還是合併）。
- NPC 面板讓位後，學魔法清單只出現在 TRADE_WINDOW；按 X 關閉後，NPC 面板回來的時機。
- 圖示格的位置、大小與原版的差異（原版格子幾何沒有追）。

## 產出路徑
- `engine/source/NativeUi/TransactionPanel.cs`、`engine/source/NativeUi/TradeWindowPanel.cs`、`engine/source/TradeUi.cs`、`engine/source/WeaponRepair.cs`、`engine/source/WeaponRepairUi.cs`、`engine/source/SpellExchangeUi.cs`
- 新增：`engine/source/TradeFollowupTests.cs`
- 本報告：`docs/reports/issue-715.md`

## 給下一棒的提示
- 原版是從背包拖到交易窗。要做到這點，得讓 `ItemWindowPanel` 支援跨窗拖曳，需要另開任務（R4-B 的檔案）。之後也可以讓交易窗接受「拖入」，取代現在的「點選＋點清單」。
- 原版 `ADENA_EXCHANGE_WINDOW`（uiextra86-c.xml，tag 144）應該就是金幣交易窗。版面接上之後，可以把金幣框換成原版的窗。
- 本機 WebView2 的 NETSDK1130 會擋住所有 worktree 的本機編譯，建議調度器開一張 `needs:local` 查本機 SDK／套件環境（CI 不受影響）。
- 實機驗收請照上面「未驗證什麼」的清單，交給 Codex／dot 在 P24Lab 用測試角色做。
