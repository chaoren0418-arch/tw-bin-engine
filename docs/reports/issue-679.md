# Issue #679：T286 R3-F 學魔法窗改用 `TRADE_WINDOW` 模式 7

分支：`task/679`｜依據：gap-matrix 12.5（R3-F）、TWP§1.1／§1.6、SS2§1.2、第 1 節 op 44／175、第 3 節 C_BUYABLE_SPELL。

## 做了什麼
- **新檔 `engine/source/SpellTradeSource.cs`**
  - `SpellTradeSource : ITradeWindowSource`（只用 R2-E 留下的介面，`TradeWindowPanel.cs` 沒改）：先看 op 175 清單，再看 op 44 清單，兩者都是模式 7（左鈕 BUY 1992）。
  - op 44（3.81 S41）：`ShowPrice=true`；單價＝基價×((`索引>>3`)+1)²；TOTAL 顯示背包金幣；每列上限 1；已學的列停用。左鈕走既有的 `Interact("shop")`，由 `ValidateService` 驗證後送出 **C39**（與舊清單送出的封包相同）。
  - op 175（3.81 S13）：`ShowPrice=false`、不顯示 TOTAL；本機已學的技能不列（SS2§1.2 步驟 2）。左鈕走 `LearnSpellExchange`，送出 **C191**。
  - 兩份清單共用模式 7，但面板用 (模式, 版本) 判斷「新清單／已關閉」。所以版本用奇偶分開（op 44 偶數、op 175 奇數），避免互相誤判。
  - op 44 的空清單：用 M7-A 觀察器 `[WorldReceive(41,Observe=true)]` 在同一包先處理。依 NPC 外型開頁：237→`GerengEv3`、2820→`Sedia1`、其他→`Siriss1`。換頁之後 NpcServices 的「waiting／skill-list」條件不成立，所以不會再蓋成空清單。非空清單時，觀察器只記下基價和對應的對話版本；S41 照舊由 `NpcServices.cs` 解析（沒改檔）。觀察器的解析和 NpcServices 一樣嚴格，格式錯誤時不動作，交給主分派丟例外。
- **`SpellExchangeUi.cs`**：拿掉暫行 WinForms `SpellExchangeUi` Form。改成在 NPC 面板同步（`SyncSpellExchangeWindow`，呼叫點在 NpcUi.cs，沒改）時，把 `SpellTradeSource` 插到 `TradeWindowSources` 最前面（只插一次）。同時包裝 `IconSource`，讓模式 7 不畫物品圖示。
- **`SpellExchange.cs`**：更新註解；`RequestSpellExchangeList(openWindow)` 不再開 Form（參數保留給既有呼叫端，已無作用）。
- **新檔 `engine/source/SpellTradeTests.cs`**：`[WorldModuleTest("spell-trade")]`，由 CI 的 `--module-tests` 自動執行。

沒改 `TradeWindowPanel.cs`、`NpcServices.cs`、`NpcUi.cs`、`WeaponRepairUi.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 怎麼驗證
- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release` 成功，新檔沒有警告。本機 SDK 對 WebView2 winmd 會報 NETSDK1130（與本次修改無關，#676／#677／#678 也遇過），所以編譯時暫時加了 `scratch/task679/nowinmd.targets`（未入 git）。
- `python engine/tools/check-no-game-assets.py`：pass（errors 0）。
- 離線測試 `spell-trade`（合成封包、合成模板與技能表、記憶體送出器）：
  - 觀察器有註冊、S41 沒被擴充消化；單價公式（索引 0／7／8／16、溢位）；奇偶版本；S41 解碼（包含 3.81 只送 `H(0)` 的空包）。
  - op 44：buyspell→C145→S41→模式 7 欄位（單價、名稱、上限、已學停用、無圖示）；C39 的十六進位內容；已學、數量 2、舊版本都不會送；送出後關窗；不重送；沒要求過的 S41 不開窗；關閉不送封包；Reset 會清掉。
  - op 44 空清單：外型 237／2820／1110 分別開 `GerengEv3`／`Sedia1`／`Siriss1`；之後再來的 S41 不會蓋掉頁面；`D(6000) H(0)` 也一樣；沒要求過的空包不換頁；格式錯誤照舊丟例外。
  - op 175：模式 7（無單價、無 TOTAL、已學的不列）；C191 的十六進位內容；op 44 版本或不在清單的索引都不會送；送出後關窗；關閉不送封包；不在世界中時沒有窗。
  - 多來源：學魔法優先；關閉會轉給它；忙碌時不送。
- CI：在 PR 上跑（本機依規定不執行測試程式）；結果見 PR。

## 未驗證／推測
- **待驗證（需真機，交 Codex／dot）**：原生 UI 開啟時，與 NPC 對話→以金幣學魔法／以物品學魔法，TRADE_WINDOW 模式 7 的實際畫面、單價、BUY 送出後原服回 S_AddSkill 的流程；空清單時的換頁。
- **推測**：
  - TW16 op 44 的空清單從封包尾端讀 NPC id，3.81 不送這個欄位（SS2§1.2）。本引擎改用目前對話 NPC 的外型。
  - op 44 已學技能在原版是否濾掉沒查證，這裡列出但停用。
  - 列圖示：原版用魔法圖示表 `word_136D548[索引]`，還沒對應到 surf，所以模式 7 不畫圖示。
  - 顯示的單價用客戶端公式（索引>>3）；伺服器實際扣款以 3.81 技能等級為準，如果兩者不同，以背包金額為準。
- **已知限制**：
  - 原生 UI 開啟時，op 44 清單**同時**仍列在 NPC 對話面板（舊的過渡清單，也能送出）。要隱藏得改 `NpcServices.cs`／`NpcUi.cs`，不在本組可改範圍。
  - 原生 UI 沒啟動時，op 175 沒有清單窗（只剩 NPC 面板的提示文字），op 44 照舊用 NPC 面板清單。
  - 模式 7 的「＋／－」是 TradeWindowPanel 共用的規則（上限 1：＋到 1、再按回 0）。原版學魔法是否也這樣沒查。

## 產出路徑
- `engine/source/SpellTradeSource.cs`（新）
- `engine/source/SpellTradeTests.cs`（新）
- `engine/source/SpellExchangeUi.cs`、`engine/source/SpellExchange.cs`（改）
- `docs/reports/issue-679.md`（本檔）

## 給下一棒的提示
- 想在原生 UI 開啟時隱藏 NPC 面板的 op 44 舊清單，可以比照 `ShopNativeActive`：在 `NpcServices.cs`／`ShopTradeSource.cs` 把 `"skills"` 也納入「原生窗接手」，並讓 `CanSubmitService` 讓位。這要另開任務，並記得更新 `SpellTradeSource` 的 `ServiceSubmittable` 用法。
- 魔法圖示表 `word_136D548` 對應到 surf 之後，可以在 `SpellExchangeUi.cs` 包裝的 `IconSource` 裡，依模式 7 改畫魔法圖示（TradeListItem.Icon 目前填 0）。
- 真機驗收請在 P24Lab 用測試角色，找外型 ≠ 834 的學魔法 NPC（buyspell）和外型 834 的 NPC（xchgspell）。
