# Issue #623（T262）R2-E：商店改用 `TRADE_WINDOW`

- 階段：code，第 1 次嘗試；分支 `task/623`
- 依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-E、`trade-window-and-projectiles.md`（TWP）§1.1–§1.9、`followups-3.md`（FU3）§6

## 做了什麼

1. **`NativeUi/TradeWindowPanel.cs`：依 Q-1 改寫面板**
   - 新的資料介面：`TradeListItem`、`TradeListView`（模式、版本、列、持有、是否顯示單價、能不能送出）。`ITradeWindowSource.Submit` 改成一次交出所有數量大於 0 的列。另外加了 `TradeWindowSources`，讓多個來源共用同一個窗。R3-F 的學魔法窗（模式 7）可以直接實作這個介面，不必改面板。
   - 版面依 TWP§1.3／§1.4：
     - 視窗大小取框 1984（239×317），並畫出框。
     - 一次 7 列，列距 36，第一列原點在 (19, 38)。
     - 每列畫圖示、名稱 (38,3)、數量（右緣對齊 176）、「－」「＋」鈕、單價。單價左邊加 ADENA 圖。
     - 已選的列畫 surf 24；數量大於 0 的列填 192×35 的底色。
     - 點選哪一列，用 ListBox (16,34)–(213,287)、格高 37 換算。
     - 頂部畫 TOTAL 1991，並在 (75,22) 顯示「合計/持有」。
     - 拉桿 2044 依「捲動列／(筆數−1)」放在軌道上，可以拖曳，也可以點軌道跳位。
     - 標題列 (1,1,239,20) 可拖曳移動視窗（`IUiMovablePanel`）。
   - 按鈕：左鈕依模式換圖（0／7＝BUY 1992、1＝SELL 1998、6＝OK 1996）。按鈕只有兩態：按下時畫 n+1，放開時游標還在鈕內才觸發。
   - 數量規則依 TWP§1.5：
     - 「＋」到上限後回到 0；「－」到 0 後跳到上限。
     - 「＋」「－」按下時分別畫 555／557。
     - 數字鍵把數量 ×10 再加該數字；Backspace 把數量除以 10。
     - 買的單筆上限依單價：9999／999／20／1（`BuyLimit`）。
2. **新檔 `ShopTradeSource.cs`：商店的資料來源**
   - S70 的清單開模式 0，S65 的清單開模式 1。
   - 送出沿用既有的 `LoginConnection.Interact("shop")`：先經 `ValidateShop`／`ValidateService` 驗證，再送 **C161**（type 0／1，每筆 `D id, D count`）。
   - 關閉走 `Interact("close")`。
   - 「持有」＝背包裡的金幣。貨幣不是金幣時不畫合計，也不能送出。
   - 賣出不跳確認框（FU3§6.1：3.81 不送 S250／162）。
   - 新增 `WorldSession.ShopNativeWindow` 開關：原生窗接上時，買賣列只交給 `TRADE_WINDOW`。
3. **拿掉 WinForms 商店清單**（只在原生 UI 開啟時）
   - `NpcInteraction.cs`：只改了 `ShopRows` 用到的地方。`ShopRows` 在原生窗接手時回傳空陣列，內部驗證改讀私有欄位 `shopRows`。`CanSubmitShop` 在原生窗接手時為 false。
   - `NpcServices.cs`：`CanSubmitService` 與 `ValidateService` 也照同樣方式拆開。
   - 結果是 WinForms 與 web 的舊清單、舊送出鈕不再出現商店列，也不能從舊清單送出。原生 UI 關閉時，維持舊清單作為過渡，和修理的做法一致。
4. **`WeaponRepairUi.cs`：接線與修理**
   - 面板接上 `TradeWindowSources`（修理＋商店）和背包圖示，並同時設定修理與商店的原生窗開關。
   - 修理模式 6 改用新介面（`RepairTrade.View`）：費用＝耐久 × 200；**數量上限＝背包裡該物品的數量**（FU3§6.2）。
5. **測試**
   - 新檔 `ShopTradeTests.cs`（`[WorldModuleTest("shop-trade")]`，100 項）涵蓋：
     - 舊清單和原生窗的切換。
     - 模式 0 的上限表、名稱轉換、C161 位元組。
     - 送出後窗關閉；舊版本不重送。
     - 非金幣貨幣不能送；空清單不開窗。
     - 賣出模式 1 的上限與 C161 type 1；關閉；多來源切換。
     - 修理模式 6 的費用與上限。
     - 面板：用合成 XML 和合成圖，驗證元件、框的大小、各模式的左鈕、列與命中、＋／－規則、鍵盤輸入、按下圖與放開觸發、合計、拉桿、拖曳、關閉與 Esc、像素檢查。
   - `TradeWindowTests.cs`：刪掉舊的面板段落（舊 API、列高 16），改由 `ShopTradeTests.cs` 測新版面。修理協定的段落保留不動（41 項）。

## 怎麼驗證（本機，worktree）

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` → `--build`（合併 origin/main 後） | `PASS dev`。productDll `e98755a1…908fa`。新增 0 個警告；MagicUi.cs 原本就有的 nullable 警告不是本次造成的 |
| `P11.ClientEngine.exe --module-tests` | pass：44 組、2242 項、0 失敗（shop-trade 100、trade-window 41） |
| npc-tests／weapon-repair-tests／world-ui-tests | PASS 165／98／278 |
| demo-tests：CI 的 17 個群組 | 全部 PASS。說明：native-ui-resources／macro／party／buff-icons 需要真實客戶端，所以在本機跑；其餘群組在 `TW_CLIENT_ROOT` 指向不存在的目錄下跑（等同 CI），用來避開 #546 已知的 S245 本機環境問題 |
| `engine/tools/check-no-game-assets.py` | pass，errors 0 |

建置前置：worktree 裡沒有 `engine/vendor` 和 `engine/assets`，所以照先例建了兩個 junction，唯讀指向主專案的同名目錄。沒有複製任何資源，也沒有改主專案。NuGet cache 與 CLI home 都放在 `scratch/t623`。

## 已驗證／推測／待驗證

- **已驗證**（依 TWP／FU3 的反編譯結論，並用離線測試核對）：
  - 模式與左鈕的對應、元件索引、7 列×36 的列版面、按鈕兩態、＋／－的循環規則、買的上限表。
  - C161 的欄位；賣出不跳確認；修理上限＝背包數量。
- **推測**（程式碼註解裡都有標明）：
  - 數量大於 0 的列底色，以及合計超過持有時的警告色（原版顏色值沒有查）。
  - ADENA 圖與單價之間的 1 px 間距。
  - 拉桿拖曳的換算方式。
  - 數字鍵輸入超過上限時，夾到上限。
  - Add／Subtract 鍵對應原版鍵碼 4203／4205。
  - 賣出模式也顯示單價（TradeBox `+153` 在商店模式的值沒有查）。
  - 名稱只畫一行；第二行與截短規則沒有做。
- **待驗證**：
  - 3.81 實際的交易窗外觀（TWP§1.8：3.81 可能是寫死在程式裡的窗）。
  - 用真實 `Tile.pak`／`Sprite*.pak` 渲染出 PNG 做目視比對：本輪沒做。
  - 真機上的買、賣、修理流程：本輪沒做，依規定不啟動遊戲。

## 未完成／限制

- **修理多選**：面板會交出所有數量大於 0 的列，但 `WeaponRepair.cs` 的送出狀態機送出一次後版本就變了，所以 `RepairTradeSource` 目前只送第一筆。原版是每列各送一包 C20。這需要改 `WeaponRepair.cs`，不在本組範圍。
- `TradeWindowTests.cs` 不在本組的可改清單內。因為面板的 API 和版面依任務要求改版，舊的面板測試必然失效，所以只刪掉該段（移到 `ShopTradeTests.cs`），修理協定測試沒有動。

## 產出路徑

- `engine/source/NativeUi/TradeWindowPanel.cs`、`engine/source/ShopTradeSource.cs`（新）、`engine/source/ShopTradeTests.cs`（新）
- `engine/source/WeaponRepairUi.cs`、`engine/source/NpcInteraction.cs`（只改 `ShopRows` 用到的地方）、`engine/source/NpcServices.cs`、`engine/source/TradeWindowTests.cs`
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- 本報告 `docs/reports/issue-623.md`

## 給下一棒的提示

- **R3-F（學魔法窗，模式 7）**：實作 `ITradeWindowSource` 回傳 `TradeListView(Mode: 7, …)`，每列 `Maximum` 設 1，學魔法的 `ShowPrice` 設 true，以物品學魔法設 false。接法是在 `WeaponRepairUi.cs` 的 `TradeWindowSources(...)` 再加一個來源，`TradeWindowPanel.cs` 不用改。
- **修理多選**：要讓 `WeaponRepair.cs` 一次送出多件，需要一個能逐件送 C20 的送出路徑（例如 `SubmitWeaponRepair` 收一批）。之後 `RepairTradeSource.Submit` 改成全部送出即可。
- **真機驗收**（dot，P24Lab）：開商店買賣與修理時，核對窗位置、7 列版面、BUY／SELL／OK 換圖、合計與持有、C161 封包（可開封包追蹤），以及 WinForms 舊清單已經不再出現。
