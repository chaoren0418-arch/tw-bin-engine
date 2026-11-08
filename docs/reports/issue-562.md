# Issue #562 — T240：舊式背包、魔法、倉庫原生窗（W2-I）

階段：code（第 1 次嘗試）。分支：`task/562`。依據：`docs/reference/windows-2.md`（T226／#533）第 2、9 節；契約：`docs/design/native-ui.md`。

## 做了什麼

只新增任務列出的三個檔案，外加 `rebuild.py --update-lock` 重產的 `rebuild-lock.json`：

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/ItemWindowPanel.cs` | 三個窗共用的 `LegacyWindowLayout`（讀舊式 `*_WINDOW` 區段：平鋪的 `SURF_IMAGE`／`IMAGE`／`IMAGE_BTN`，以 `surfName` 辨識，`xPos`／`yPos` 相對視窗）、`LegacyWindowPanel` 基底（`UiLayer.Window`、可拖曳、依 XML 順序畫、關閉鈕、上下捲動鈕、離開世界自動關）、背包窗 `ItemWindowPanel`（`uimagicitem.xml` 的 `ITEM_WINDOW`）、資料轉接 `IUiItemWindowState`／`UiItemWindowData.From(WorldSession)`／`UiItemWindowGameState`，以及模組測試 `native-item-window` |
| `engine/source/NativeUi/SpellWindowPanel.cs` | 魔法窗 `SpellWindowPanel`（`SPELL_WINDOW`）：王族 7／一般 10／妖精 11／黑妖 5 級等級鈕，數量照 XML 讀，編號不連續就拒絕；3.81 職業代碼 → 等級鈕組；資料 `IUiSpellWindowState`（職業、已學技能，`Magic.cs` 的 `LearnedMagic`）；測試 `native-spell-window` |
| `engine/source/NativeUi/WarehouseWindowPanel.cs` | 倉庫窗 `WarehouseWindowPanel`（`uiextra86.xml` 的 `WAREHOUSE_WINDOW`）：收到倉庫清單就自動開窗；名稱／數量排序鈕；選取數量（不能超過清單上的可領數量）；領取費＝每列費用 × 選取列數（與 `NpcServices.cs` 相同）；確定鈕透過 `IUiWarehouseCommands.SubmitWarehouse` 送出一次；取消／離開鈕呼叫 `CancelWarehouse` 並隱藏視窗。資料 `UiWarehouseView.From(WorldSession)`（讀既有的 `InteractionKind`／`ShopRows`）；測試 `native-warehouse-window` |

- 執行期讀取：面板的 `Load(UiLayout, SurfCatalog)` 走 C1 的 `UiXmlSource`／`SurfCatalog`。`uimagicitem` 沒有 `-c` 版，會退回無後綴檔，並記一行 `ui-xml-fallback`；`uiextra86` 讀 `-c` 版。repo 不含任何 XML、圖檔或像素。
- 沒有新增封包處理，也沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`UiClientBridge.cs` 或其他既有檔案。
- 測試用的狀態走 `LegacyWindowPanel.FindState<T>`，會穿過產品現有的裝飾器（`UiHpMpGameState`、`UiMacroGameState`、`UiLeftStatusGameState`、`UiChatGameState`）；倉庫的指令介面會從 `UiMacroCommands` 底下找出來。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` | `dev-inputs-verified`，sourceFiles 290 |
| `rebuild.py --build --configfile .github/nuget.ci.config` | 成功（本機沒有 vendor，所以用 CI 的 NuGet 設定） |
| `P11.ClientEngine.exe --module-tests`（全部） | `pass`：21 個測試，0 失敗，1007 項檢查。本組：`native-item-window` 40、`native-spell-window` 23、`native-warehouse-window` 34 |
| DemoChecks `native-ui-canvas`／`native-ui-macro`／`native-ui-resources` | PASS 429／403／539 |
| `engine/tools/check-no-game-assets.py` | `pass`，errors 0 |
| CI | push 後看 PR |

離線測試只用程式合成的 XML（元素順序與屬性值照本機檔）和合成的 `UiSurface`（每個 surf 一種顏色），不讀客戶端、不用網路。測試內容：每個元件的邏輯座標、XML 繪製順序、多狀態圖只畫一張（分頁、等級、排序 UP／DOWN）、沒有座標的元素不畫、surf 缺少時矩形為空也不畫替代色塊、缺節點或只有半個座標時丟 `UiLayoutException`、拖曳後限制在 800×600 內、命中測試跟著視窗移動、關閉與捲動、窗外點擊不攔、沒有熱鍵、倉庫的開窗／重設／選取／排序／送出一次／取消／離開世界、資料穿過裝飾器，以及合成畫布上的像素顏色。

**本機選用渲染**（預設關閉，CI 不跑）：設定 `NATIVE_UI_LEGACY_RENDER=<repo>/scratch/issue-562/render` 後執行 `--module-tests … --module native-item-window`，以鎖定的 `D:\lineage開發\0-客戶端`（唯讀）渲染三窗。結果：43 項檢查通過，所有有座標的 surf 都在本機。PNG 與元件表只放在 `scratch/issue-562/render/`，沒有進 git。目視確認框、底、關閉鈕、捲軸、分頁、垃圾桶／寶箱、倉庫標題列與確定／取消都對齊。實際尺寸：

| 視窗 | 範圍 | 主要元件（邏輯座標，視窗原點在 0,0） |
|---|---|---|
| ITEM_WINDOW | 193×282 | 底 2046 (6,8) 181×264；框 2047 (0,0)；X 2037 (179,15) 8×8；捲軸 2039 (171,58) 12×167；上 2042 (171,44)／下 2040 (171,225) 12×14；分頁 2048 (27,235) 139×23；垃圾桶 2052 (142,260) 15×18；寶箱 2054 (117,260) 20×18 |
| SPELL_WINDOW | 193×282 | 底 2056 (6,8) 181×271；框 2057；等級直條 2065 (12,37) 14×212 |
| WAREHOUSE_WINDOW | 239×350 | 底 3001 (6,9) 228×338；框 3002；離開 3031 (225,15)；捲軸 3024 (217,61) 12×224；上 3025 (217,47)／下 3027 (217,285)；總計 3007 (19,25)；名稱 3008 (117,35)；排序 3010 (150,37)、(206,37) 8×8；數量 3009 (162,35)；分頁 3020 (17,300) 195×22；確定 3014 (60,326) 65×17；取消 3017 (127,326) 77×17 |

## 已驗證／推測／待驗證

- **已驗證**：三段設定的元件與座標（windows-2.md §2.2 表，本機 XML 與 surf 確認存在）；`-c` 退回行為（C1）。
- **推測**（每項只集中在一處）：
  - 框元素沒有座標時放在視窗原點。
  - 同一位置的多張圖是「整條、其中一格點亮」，只畫選中的那張（分頁、魔法等級、排序 UP／DOWN）。渲染結果看起來一致，例如等級圖 14×212 是一整條直欄。
  - 騎士（職業 1）使用一般等級鈕組。
  - 排序 UP 圖代表遞增。
  - 上下捲動鈕依位置判斷（上方 = 往前）。
  - 視窗任何不是控制項的地方都可以拖曳。
- **待驗證**（只做資料，不畫、不接輸入）：
  - 格子（道具／技能／倉庫列）的座標與圖示：底圖上看得到 4×6 格，但格距與起點沒有查證。
  - 捲軸拉桿位置、每次捲動步距與頁大小（目前一格 = 一筆）。
  - 分頁與等級鈕在整條圖裡各自的點擊區。
  - 任務／其他分頁的分類規則（目前列出全部）；倉庫 `ShopRow` 沒有道具類型，裝備分頁也無法分。
  - 魔法等級與技能遮罩群組的對應（目前列出全部已學技能）。
  - 原版開窗熱鍵（這三窗不接任何按鍵）、視窗初始位置（目前用 XML 的 x,y = 0,0）。
  - 倉庫 3003／3005、信件 2303／2304 這些沒有座標的元素。

## 未做／限制

- **沒有接到產品**：`UiPanelRegistry.cs`、`UiClientBridge.cs` 不在本張可改範圍，所以三窗還沒註冊進 `UiRoot`，產品狀態也還沒包上 `UiItemWindowGameState`／`UiSpellWindowGameState`／`UiWarehouseGameState`，`IUiWarehouseCommands` 也沒有實作。沒有指令實作時，按確定會打開開發工具的 NPC 頁（過渡做法，不是原版行為）。
- 職業代碼：`WorldSession` 沒有保存角色職業，`UiSpellWindowData.From` 要由呼叫端傳入（來自角色清單）。
- 倉庫類型：引擎目前只接受 S_RETRIEVE_LIST 類型 3（個人）；血盟 5、妖精 9、角色 18 仍由 `NpcServices.cs` 關閉對話，本張沒有動封包。
- 金幣背包、金幣倉庫、寵物背包沒有做（前兩者本機缺 surf）。
- 沒有真機驗收。

## 產出路徑

- `engine/source/NativeUi/ItemWindowPanel.cs`、`SpellWindowPanel.cs`、`WarehouseWindowPanel.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-562.md`（本檔）

## 給下一棒的提示

1. **整合任務**（建議開一張 code，可改 `UiPanelRegistry.cs`、`UiClientBridge.cs`）：
   - 註冊三行：`new(ItemWindowPanel.WindowId,ItemWindowPanel.Layout,_=>new ItemWindowPanel())`，另外兩窗照同樣寫法。
   - 在 `NativeUiGameState()` 外層包上 `UiItemWindowGameState(…, UiItemWindowData.From(login.World))`、`UiSpellWindowGameState(…, UiSpellWindowData.From(login.World, 職業代碼))`、`UiWarehouseGameState(…, UiWarehouseView.From(login.World))`。這三個都實作 `ILegacyWindowStateDecorator`，可以疊加。
   - 實作 `IUiWarehouseCommands`：`SubmitWarehouse` 接既有的 NPC 服務送出路徑（要先過 `ValidateService`），`CancelWarehouse` 接 `CloseInteraction`。
   - 由 ui-menu-buttons 的 Item／Spell 按鈕呼叫 `Toggle()`。
2. **查證任務**（claude-local，IDA）：查 `InventoryWindow`（`sub_88B060`）與 `SpellWindow`（`sub_DFC1E0`）的格子座標、格距、拉桿與分頁點擊區，以及 `WAREHOUSE_WINDOW` 的消費類別。查完後只改各面板集中的那幾處推測。
3. 本機選用渲染指令見上方「本機選用渲染」，可用來和查證結果比對。
