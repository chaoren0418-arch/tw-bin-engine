# Issue #779 — T333：R6-C 私商 `TRADE_WINDOW` 13／14

依據：`docs/reference/followups-7.md` 第 8 節 R6-C；WIN§2 T102；TWP§1.1–1.6；FU7 第 1 節 op 29。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/PrivateShopTradeSource.cs`（新） | `PrivateShopTradeSource`（`ITradeWindowSource`）：S140 私商清單 → 原生 `TRADE_WINDOW` 模式 13（買）／14（賣）；`LoginConnection.PrivateShopTradeWindow`、`SendPrivateShopBatch`（多筆一包 C161，送出閘門前再驗一次）；`WorldSession.PrivateShopNativeWindow`、`PrivatePurchaseRows`；`PrivateShopTrade`（上限、兩種不送的情形、提示字串 1028／897＋934） |
| `engine/source/PrivateShop.cs` | `PrivateShopPackets.SubmitMany`（多筆 C161）；`Submit` 改成呼叫它（一筆時 bytes 不變）；原生窗接上時，舊清單不給列、不能選或送，單筆 `SendPrivateShop` 送出也會擋下 |
| `engine/source/NativeUi/TradeWindowPanel.cs` | `ModePrivateBuy=13`／`ModePrivateSell=14`；`PriceColor`：單價分 5 級換色；模式 13 合計超過持有時用警告色 |
| `engine/source/PrivateShopUi.cs` | 原生窗接上、清單是 list 時，舊面板加一行提示「清單已在交易視窗」 |
| `engine/source/WeaponRepairUi.cs` | 只加來源：`TradeWindowSources` 加入 `PrivateShopTradeSource`，並同步 `PrivateShopNativeWindow` 旗標 |
| `engine/source/PrivateShopTradeTests.cs`（新） | `[WorldModuleTest("private-shop-trade")]` 離線測試，自動發現 |

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

### 依據核對（已驗證）

用 Capstone 靜態讀 `TW1608262200.bin`（`P01-分析基準/input`，SHA-256 `e12949cf…09641a2`；VA＝FO＋0x400C00）。腳本是 `scratch/t779/dis.py`，沒有進 git。

- **5 級換色**（`sub_A29AA0`，VA 0xA2A149–0xA2A2A4）：只在模式 13／14 生效。單價（TradeItem +0x5CC）≥1 萬用調色盤 `dword_1369D70[11]`（0x63B6），≥10 萬用 [40]（0x5A36），≥100 萬用 [29]（0x5FBC），≥1000 萬用 [4]（0x509E），≥1 億用 [34]（0x5E8D）。<1 萬維持平常字色。數值和 `ChatPalette`（R2-G #616 從 BIN 讀出的表）一致。
- **模式 13 合計上限**（`sub_A2BA10`，VA 0xA2BA1C）：合計由 `sub_A2A9D0` 算出，等於 Σ 數量（+0x5D0）× 單價（+0x5CC）。合計 >99999（0x1869F）時不送，呼叫 `sub_4FD770` 顯示字串 1028（0x404），窗不關。
- **模式 14 筆數上限**（`sub_A2BB90`，VA 0xA2BBCF）：`sub_A2AA40` 算出的筆數 >8 時不送，用 `sprintf(字串 897, 字串 934)` 印到聊天列（`sub_7DC850`），窗不關。
- **數量上限**（`sub_A2AD00`，VA 0xA2ADAE–0xA2AE1F）：上限＝min(TradeBox+0x94, TradeItem+0x128)。模式 13 的 +0x94 是 9999，所以上限是 min(9999, 清單上限)；模式 14 的 +0x94 是 0，只看物品數量。依單價分的 9999／999／20／1 只用在模式 0。
- **送出分支**（`sub_A2BA80`，VA 0xA2BAE0–0xA2BB14）：模式 13 → `sub_A2BA10(0)`，模式 14 → `sub_A2BA10(1)`。送出失敗時不關窗。
- **多筆 C161 的編法**（SP 原始碼 `D:\lineage開發\1-伺服器\src\…\C_Result.java`）：`mode_buypc`、`mode_sellpc` 先讀 `D owner, C type, C size, C(未用)`，接著有 size 筆，每筆 8 byte。買是 `D order, D count`，賣是 `D 物件, CH count, C order`。兩邊都用 `isRemoveFromList[8]`，而且 order 要 <8。所以一包最多 8 筆（`PrivateShopPackets.MaxRows`）。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release` 結果是 **0 錯誤**。2 個警告在既有的 `MagicUi.cs`，和本次修改無關。
  - 本機環境備註：worktree 直接 restore 會出現 NETSDK1130（WebView2 winmd），和 #705／#712 遇到的一樣。照同樣做法處理：用 `scratch/t779/nuget.config`（唯讀指向主專案 `engine/vendor`）、獨立的 `NUGET_PACKAGES`，加上 `UseArtifactsOutput`（輸出在 `scratch/t779/artifacts`）。CI 不受影響。
  - `DemoChecks.csproj` 參照固定輸出路徑的 product DLL，用隔離輸出時本機編不到它。npc-tests 用到的 `PrivateShopPackets.Submit`、`SendPrivateShop` 簽章沒變，所以交給 CI 驗。
- `python engine/tools/check-no-game-assets.py`：結果 `pass`（4172 檔，0 錯誤）。
- 離線測試 `private-shop-trade`（依規定本機不執行，由 CI 的 `--module-tests` 跑）涵蓋：
  - C161 多筆編碼：買和賣各一組逐 byte 比對；`Submit` 和一筆的 `SubmitMany` 輸出相同；超過 8 筆、空清單、數量超過上限都會拋例外。
  - 原生窗沒接上時，舊清單照舊顯示；接上後舊清單讓位，單筆送出被擋。
  - S140 買 → 模式 13：列號、圖示、名稱、單價正確，上限 9999；兩筆一包，送出後窗關，重送被擋；忙碌、未知列、重複列、數量超過上限都整包不送；關閉後回到 idle。
  - 模式 13 合計：180000 不送，窗與版本不變，提示走 1028 的路徑；99999 剛好可以送。
  - S140 賣 → 模式 14：9 筆不送並顯示 897 提示；2 筆一包，格式是 `D 物件, CH, C order`；同一物件跨列加總超過持有量時不送。
  - 5 級換色的邊界值，以及調色盤原值；模式 0／1／6／7 不換色。
  - 面板實際接上來源：模式 13 的左鈕是 BUY 1992，合計超過時按 BUY 不送、數量保留，改數量後送出一包；模式 14 的左鈕是 SELL 1998；CANCEL 會關掉私商清單。
- CI 結果（PR #797）：
  - 第 1 輪 `map-continuity` 失敗，原因在新測試本身：金幣 ID 903 和 901–909 的物品重疊，「持有」算出 0；查詢封包 C47 也算進送出數，預期值少算一包。改正後
  - 第 2 輪 `dotnet`、`map-continuity`（含所有 `[WorldModuleTest]`，包括 `private-shop-trade`）、`npc-offline` **全部通過**。

## 未驗證／推測

- **推測**：897 的字樣。原版用 C `sprintf` 把 934 套進 897；引擎把 897 的第一個 `%s` 換成 934 的字。實際字樣要等真機核對。沒有字串表時改用中文占位文字。
- **差異（標明）**：897 原版印在聊天列，1028 原版開提示框。引擎目前把兩者放在私商提示與狀態列，沒有另外開窗或寫進聊天列。
- **推測**：3.81 的 S140 不帶貨幣種類。原版 TradeBox +164 預設是 7（金幣），所以「持有」、TOTAL 與 ADENA 圖都照金幣畫。地圖如果設定成別的貨幣，畫面會不準。
- 模式 13 的合計原版是 32 位元有號運算；引擎用 64 位元。只有在溢位時結果才會不同。
- **待驗證（真機，交給 Codex）**：在 P24Lab 選擺攤的測試角色，查詢買或賣後應該跳出原生交易窗（BUY 或 SELL 鈕），單價依金額換色；同時買或賣多筆後核對背包；合計 >99999 或一次 >8 筆時不送出。

## 產出路徑

- 程式：上表 6 個檔案。
- 報告：`docs/reports/issue-779.md`（本檔）。
- 分支：`task/779`。

## 給下一棒的提示

- 如果要把 897 改成真的寫進聊天列、1028 改成提示框，需要找本地系統訊息的入口（引擎目前沒有共用的 API），會動到聊天相關檔案，建議另開一張任務。
- 私商的舊 WinForms 面板仍負責查詢、選店主與關閉；原版從點店主開始的流程（C47）沒有改。
- `PrivateShopTrade.Limit`、`Refusal` 是純函式；之後若加入倉庫等其他 `TradeWindow` 模式，可以照同樣做法寫。
