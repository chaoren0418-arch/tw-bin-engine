# Issue #674 T281：R3-E 交易窗 `TRANSACTION_WINDOW`

依據：`docs/reference/gap-matrix.md` 第 12.5 節（R3-E）；WIN§1.5、§2.1；第 1 節 op 88／145／167；第 3 節 C_ASK_XCHG；第 4.1 節 `ExchangeUI`。

## 做了什麼

| 檔案 | 變更 |
|---|---|
| `engine/source/NativeUi/TransactionPanel.cs`（新） | 原生 `TRANSACTION_WINDOW` 面板（`IUiMovablePanel`，`[UiPanel(185)]`，layout `uiextra86`）。內含 `TransactionView`（`From(WorldSession, resolve)` 轉接 Trade.cs 狀態）、`TransactionRules.Adena`（金幣列）、`ITransactionSource`（確定／取消）。|
| `engine/source/Trade.cs` | 加兩個唯讀屬性 `TradeOpened`（S52 之後、同一世界）、`TradeOwnConfirmed`；旗標 0 註解補一句「R3-E 重查仍無證據，維持現狀」。協定處理沒改。|
| `engine/source/TradeUi.cs` | 原生 UI 啟用時把 `TransactionSource` 接上面板（`AttachTransactionWindow`）；交易窗開著時（S52…S112），WinForms 面板縮成只剩「加入物品」那一列（物品下拉、數量、加入鈕＝C37），清單、提示文字、確認、取消鈕交給原生窗。原生 UI 關閉或面板故障時，WinForms 面板維持原樣。|
| `engine/source/TransactionPanelTests.cs`（新） | `[WorldModuleTest("transaction-window")]` 63 項。|

沒改：`UiPanelRegistry.cs`、`UiClientBridge.cs`、`YesNoPrompt.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

### 面板行為
- **開窗與關窗**：由 Trade.cs 的狀態決定。開窗條件是 `TradeOpened`，且狀態是 `open`／`adding`／`confirmed`／`attention`／`cancelling`。也就是收到 S52（op 88）開窗，收到 S112（op 167）結果、世界 epoch 改變或離開世界時關窗。
- **清單**：S35（op 145）的列依 side 分上下兩個清單，名稱用 `ResolveNpcText` 轉成顯示文字；金幣合計顯示在各清單右下角。新列進來時自動捲到最後；滑鼠滾輪只捲游標所在的清單。
- **確定**：送 C71（`Trade("confirm")`）。自己已確定、或 `TradeCanConfirm` 不成立時不送，確定鈕改畫按下圖。
- **取消、關閉 X、Esc**：送 C86（`Trade("cancel")`）。送出後窗不馬上關，等原服回 S112；狀態為 `cancelling` 時不重送。
- **加物品（C37）**：仍走 TradeUi 的加入控制項（見「未驗證／過渡」）。

## 版面依據（分已驗證／推定／推測）
- **已驗證**（本機 `scratch/task448-uixml/uiextra86-c.xml`，只讀不入 git）：
  - 底圖 2000 位於 (6,10)，框 2001 沒有座標；
  - 關閉 X 1962 位於 (179,15)，文字框 2008 位於 (15,213)；
  - 確定 `STAGE3_BTN` 2002 位於 (36,252)，取消 2005 位於 (104,252)，兩者都用 xRPos/yRPos；
  - `ITEM_LIST_ADJUST` 位於 (26,49)，tag 91。
  - 面板執行時從 XML 讀這些值；缺元件、缺座標或缺 tag 都丟 `UiLayoutException`。
- **推定**：`ITEM_LIST_ADJUST` 的 tag 是上下兩個清單的垂直間距。依據是同檔另外兩個窗都吻合：`EXCHANGE_WINDOW` 兩條捲軸 y 是 61／185，差 124，tag 也是 124；`ADENA_EXCHANGE_WINDOW` 是 81／225，差 144，tag 144。所以本窗下清單在 y=140。
- **推測**（程式檔頭也有標）：
  - 上清單是 side 0（自己）、下清單是 side 1（對方），依 L1J `S_TradeAddItem` 的慣例；
  - 清單的高、寬與列高 12 px；
  - 物品只畫名稱，不畫圖示格；
  - 金幣的判定是名稱為「`$4 (數量)`」的列；
  - 文字框裡畫交易提示，對方名稱畫在標題列；
  - STAGE3 三張圖依序是平常、按下、停留；
  - 起始位置用 XML 的 x／y，標題列可拖曳。

## 旗標 0（WIN§2.1）
`byte_136E99D` 在哪裡設定、自動回覆帶什麼值，都還沒有證據，所以照任務說明維持現狀：旗標 1 走聊天輸入列 Y/N；旗標 0 保留手動接受／拒絕，不自己編一個自動回覆。既有的 `trade-yes-no` 測試（DemoChecks，354 項）全過。

## 怎麼驗證
| 項目 | 結果 |
|---|---|
| `rebuild.py --build --configfile .github/nuget.ci.config`（本機先跑 `--update-lock` 產生暫用 lock，commit 前已還原成 main 版） | PASS dev，productDll `acf30190…78b2` |
| `P11.ClientEngine.exe --module-tests` | pass：46 組、2336 項、0 失敗；`transaction-window` 63 項、`auto-discovery` 28 項、`trade-window` 79 項全過 |
| DemoChecks `trade-yes-no` | PASS 354 |
| DemoChecks `native-ui-canvas` | PASS 429 |
| DemoChecks `map-continuity` | 本機有 1 項失敗：`notice S245 unknown message stays labeled`（display-notices，訊息 999）。本任務沒有改到相關程式；main 的 CI（609cba21）同一步驟是綠的。推測是本機字串表環境不同，待 CI 結果確認 |
| `check-no-game-assets.py` | pass，errors 0 |
| CI | 見 PR |

## 未驗證／過渡
- **沒有真機測試**：沒有啟動遊戲、伺服器或 DB。用真實 surf 畫出來的樣子沒有比對過。
- **加物品是過渡做法，不是原版行為**：原版是從背包把物品拖進交易窗，但原生背包的拖曳還沒實作。所以原生模式下 C37 仍由 WinForms 的加入列送出。
- 不畫物品圖示，因為原生 UI 還沒有物品圖示來源。
- 沒做金幣輸入框：原版有沒有、怎麼送都沒查到。
- 交易結果（`completed`／`cancelled`）與邀請階段仍顯示 WinForms 面板，原生窗只負責 S52…S112 這段。
- 原版窗的擺放位置（建構子）沒追。

## 產出路徑
- `engine/source/NativeUi/TransactionPanel.cs`
- `engine/source/TransactionPanelTests.cs`
- `engine/source/Trade.cs`
- `engine/source/TradeUi.cs`
- `docs/reports/issue-674.md`
- 分支：`task/674`

## 給下一棒的提示
- 原生背包（`ItemWindowPanel`）做出拖曳後，可以加一個「物品拖進 `TRANSACTION_WINDOW`」的入口，呼叫 `Trade("add")`。之後就能拿掉 TradeUi 在原生模式下的加入列（`TradeNativeWindow` 那段）。
- 要比對真實版面時：底圖 2000 的實際尺寸決定清單寬度。清單高度目前取「文字框頂 − 下清單頂」＝73，真機截圖可以拿來驗證上下清單的位置與列高。
- 旗標 0 的自動回覆值若在 Q 系列查到，只要改 `Trade.cs` 的 `TradeAskWindow` 分支（S219／252）。
- 合併後調度器要依 #646 重產 `rebuild-lock.json`，本 PR 沒有改它。
