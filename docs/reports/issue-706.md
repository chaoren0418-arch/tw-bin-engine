# Issue #706 報告：T294 R4-B 背包、魔法、倉庫原生窗畫內容

分支 `task/706`，PR #720。依據 `docs/reference/gap-matrix.md` 13.5 節 R4-B；圖示意義引用 FU5§4（`docs/reference/followups-5.md` 第 4 節）。

## 做了什麼

只改了任務列出的檔案：

| 檔案 | 內容 |
|---|---|
| 新檔 `engine/source/NativeUi/ItemIconSource.cs` | 共用的圖示來源與幾何，R4-C 可以直接用 |
| `engine/source/NativeUi/ItemWindowPanel.cs` | 共用基底加上內容繪製與放開滑鼠的處理；`ITEM_WINDOW` 畫格子、圖示、數量、選取框；點格子選取物品；分頁條可點；任務／其他分頁分開 |
| `engine/source/NativeUi/SpellWindowPanel.cs` | 點等級鈕切頁；每列畫名稱與 MP；點列選取魔法 |
| `engine/source/NativeUi/WarehouseWindowPanel.cs` | 每列畫圖示、名稱、數量；點列選取；把列拖到確定鈕就送出；分頁條可點；提領時畫費用 |
| 新檔 `engine/source/LegacyWindowContentTests.cs` | 離線測試 `[WorldModuleTest("native-legacy-window-content")]`，自動發現 |

### `ItemIconSource.cs`
- `ItemIconSource`：依圖示編號取出解好的圖示。檔名是 Sprite*.pak 的 `<icon>.tbt`，查法沿用狀態圖示的 `BuffIconCatalog`（`<n>c.tbt`／`<n>.tbt`，先查 Sprite{h} 再查 Sprite）。同一組 archive 只建一個快取（`FromSprites`／`FromRoot`）。缺圖時回傳 null，不畫任何替代圖。
- `IconOf(icon, bless)`：照 FU5§4 的 `sub_518CD0`，gfx 487 或 1617 而且祝福為 0 時改用 1617，其他編號照原值。
- `ItemText`：`$n` 用 stage 既有的 `objects/names.json` 解析，規則和 `ObjectAssets.Name` 相同；去掉名稱尾端的 ` (數量)`（同 `InventoryUi.ItemName`）；數量大於 1 才顯示數字。
- `LegacyCellGrid`：格子幾何。`Fit` 在 XML 已有位置的元件之間排出等大的格子（物品格每格 33 px＝32 px 圖示＋1 px 間隔）。另有 `SlotAt`、`CellRect`、以列為單位的 `ScrollPositions`。
- `LegacyStrip.SegmentAt`：把分頁條或等級條依圖寬平均切段，算出點到第幾段。

### 三個窗
- **背包**：格子放在「底圖左緣到捲軸」、「上箭頭到分頁條」之間。用本機 `uimagicitem.xml` 的數字算出來是 5×5 格，從 (6, 57) 開始。每格畫圖示（含 .tbt 位移），數量靠右下，選中的格子畫 1 px 框。點格子時呼叫 `WorldSession.SelectItem`，透過 `UiItemWindowData.From` 的 selector，和 WinForms 背包、U 鍵用同一個選取，**不送封包**。分頁：全部／裝備（`EquipmentRules.IsEquipment`）／任務（空）／其他（非裝備）。捲動一次移一列。
- **魔法**：等級條依鈕數平均切段，點了就切頁。第 k 頁是技能 id 在 C164 位元列 k 的技能，也就是 (id−1)/8＝k。清單從等級條底部排到下箭頭，每列 16 px，本機數字算出來是 10 列。每列畫名稱，右邊畫 MP；不支援的技能用灰字。點列時呼叫 `WorldSession.SelectMagic`。
- **倉庫**：清單從分頁條左緣排到捲軸、從上箭頭排到分頁條，本機數字算出來是 7 列，每列 200×32，從 (17, 58) 開始。每列畫圖示、名稱（畫到名稱排序鈕左邊為止）、數量（靠右）。
  - 在同一列按下再放開＝切換選取，選取時取整個數量。
  - 在某一列按下、拖到確定鈕放開＝選取這一列再送出（`IUiWarehouseCommands.SubmitWarehouse`；沒有這個指令介面時，照舊開 NPC 頁）。
  - 放開在其他地方時什麼都不做。
  - 選了的列畫框、改成黃字。提領時在 TOTAL 圖右邊畫費用。
- 三個窗都沿用基底的 `IUiClosablePanel`。原生 UI 關閉時仍可用 WinForms 的背包、魔法分頁與 NPC 倉庫頁，沒有新增「關閉時用不到」的功能。`UiClientBridge.cs`、註冊表、`Program.cs`、`rebuild-lock.json` 都沒有改：背包與倉庫的工廠在各自的 `[UiPanel]` 中用 `root.Resources` 取圖示來源；選取透過 `UiItemWindowData.From`／`UiSpellWindowData.From`（本來就在這兩個檔案裡）帶入。

## 怎麼驗證
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4047 個檔案，0 個錯誤）。
- 離線測試 `native-legacy-window-content` 只用合成的 XML、surf 與圖示，內容包括：
  - 圖示編號規則、名稱與數量文字、格子幾何與分段點擊。
  - 背包：30 個物品的捲動、點格子呼叫 selector、間隔與空格不選、圖示像素（487→1617）、缺圖不畫、選取框、四個分頁。
  - 魔法：點等級段切頁（一般 10 段、妖精 11 段）、點列選取、列框與文字像素。
  - 倉庫：捲動、點選切換、按下與放開在不同列時不動作、拖到確定鈕時只送一次、滑鼠移到窗外放開不送、沒有指令介面時的退路、排序後點擊仍對到畫面上那一列、圖示與選取框像素、新清單重設狀態。
- 三個面板原有的檢查依新行為更新：捲動改以列為單位、魔法等級條可點、拖曳起點改到窗框。
- **本機沒有執行任何測試**（使用者規定本機不跑編譯出的程式）。編譯與 `--module-tests` 交給 PR #720 的 CI，結果見下方「CI 結果」。
- 本機編譯備註：這個 worktree 的 restore 出現 NETSDK1130（WebView2 winmd），和 #705 遇到的一樣。換新的 packages 目錄、改用 vendor 或 nuget.org 的設定、加 CI 用的參數，都還是同樣的錯誤，所以本機沒有編過。這是本機環境問題，CI 用的是乾淨的 runner。

## CI 結果
PR #720 第一次 push（run 42673251414、42673251511）：`dotnet`、`map-continuity`、`npc-offline` 全部通過。
- `--module-tests` 全部 `failed: 0`。
- 新測試 `native-legacy-window-content` 通過，74 項檢查，0 失敗。
- 更新過的 `native-item-window`、`native-spell-window`、`native-warehouse-window`、`native-window-wiring` 都通過。

同步 main 之後（run 42674637674、42674638020）：
- 全部通過，`--module-tests` 是 `failed: 0`；`native-legacy-window-content` 74 項全過。
- 中間有一輪 run 42673875046 失敗，原因是 `map-continuity` job 沒有被 runner 啟動，而且那一輪不能重跑；我推了一個空 commit 重新觸發，和程式無關。

## 未驗證／推測
| 項目 | 狀態 |
|---|---|
| 圖示編號＝S_AddItem 背包圖示 gfx；487／1617 規則 | 已驗證（FU5§4，靜態） |
| 圖示來源是 Icon 群組的 `<icon>.tbt` | 推測（和狀態圖示同一個讀取函式；stage 的 `assets/inventory` PNG 由 Sprite `<icon>.tbt` 產生，見 issue-102） |
| 格子／列的位置、大小、33 px 格距、每列 16 px | 推測：XML 沒有格子幾何（WIN2§2.2），是從已知元件的位置推算 |
| 分頁條、等級條平均切段 | 推測 |
| 等級鈕第 k 個＝技能位元列 k | 一般魔法 10 級合理；王族 7、妖精 11、黑妖 5 個鈕對應到哪幾列**待驗證**；職業專屬技能（id 97–176）目前不在任何鈕上，仍可從 WinForms 魔法分頁使用 |
| 任務分頁為空 | 推測：3.81 的 S_AddItem 沒有任務屬性 |
| 倉庫分頁 | S176 的列沒有保留用途類型（`NpcServices` 不在本任務範圍），所以四個分頁都列出全部，**待驗證** |
| 倉庫點選取整個數量 | 堆疊物品的數量輸入窗**待驗證**；要取部分數量時用 WinForms NPC 頁 |
| 選取框、黃字、數量字的位置與字型 | 推測（XML 沒有選取用的 surf；字型沿用 `UiText` 的暫定值） |
| 雙擊使用、拖到垃圾桶／寶箱、背包拖到倉庫 | 沒有做（原版行為未查） |
| 真機畫面 | 待驗證（需要開遊戲，交給 Codex／dot） |

本任務沒有 Q4-n 項目。

## 產出路徑
- `engine/source/NativeUi/ItemIconSource.cs`（新）
- `engine/source/NativeUi/ItemWindowPanel.cs`、`SpellWindowPanel.cs`、`WarehouseWindowPanel.cs`
- `engine/source/LegacyWindowContentTests.cs`（新）
- `docs/reports/issue-706.md`（本檔）

## 給下一棒的提示
- **R4-C**：用 `ItemIconSource.FromRoot(root)` 取圖示；物品用 `For(InventoryItem)`，會套用 487／1617 規則。名稱用 `ItemText.Resolve`＋`ItemText.Name`，格子用 `LegacyCellGrid.Fit`。不要改 `ItemIconSource.cs`。需要不同的格距時，把參數傳給 `Fit`。
- 真機驗收時對照原版截圖，確認格子起點與格距（背包 (6, 57)、33 px；倉庫 (17, 58)、33 px；魔法 (6, 60)、16 px）。有差異時只改各窗 `Build` 裡呼叫 `Fit` 的參數。
- 等級鈕與職業技能列的對應要靜態追 TW13 的 `SpellWindow`（`sub_741080` 一帶）。追到之後改 `SpellWindowPanel.PageOf` 和 `PageSpells`。
- 倉庫分頁要分類的話，需要 `ShopRow` 帶用途類型（`NpcServices.cs`／`NpcInteraction.cs`，另開任務）。
