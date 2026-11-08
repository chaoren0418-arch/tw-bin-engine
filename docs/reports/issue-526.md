# Issue #526 報告：T219 M7-S 商店與修理改用 `TRADE_WINDOW`

日期：2026-11-09。階段：code（第 1 次嘗試）。分支：`task/526`。
依據：`docs/reference/gap-matrix.md` 第 7 節 M7-S；`docs/reference/windows.md`（WIN）§1.5、§2 T102／T134、§2.1、§3；`docs/design/native-ui.md` §5。

## 做了什麼

| 項目 | 原版（文件） | 改動前 | 改動後 |
|---|---|---|---|
| 修理入口 | 從 NPC 對話頁開始；沒有專用視窗（WIN§2 T134） | HUD「修理」鈕 → 專用 Form 580×420「武器修理與 PK 查詢」 | **移除 Form 與 HUD 鈕**。NPC 對話頁的 `fix` 動作在本機處理，送無欄位的 **C118**（不送 C125） |
| 可修清單 | `S_FIXABLE_ITEM_LIST` → `TradeWindow` **模式 6**（`TRADE_WINDOW`） | Form 裡的 ListBox | 原生 UI 開啟時，在新的原生 **`TRADE_WINDOW` 面板（模式 6）**顯示；選一列按 OK 送 C20 |
| 空清單 | 改開 NPC 對話頁 `touma3`／`gayle5` | 只有文字「原服沒有列出損壞武器」 | 依這位 NPC 上一個對話頁名的前綴開 `touma3` 或 `gayle5`；前綴對不上就不開頁、只留文字 |
| PK 查詢 | HTML 動作 `HyperActCheckPK` | Form 裡的專用鈕 | 專用鈕跟著 Form 移除；PK 查詢已由 M7-R（#524）改走對話頁 `checkpk`（C51），協定 `LoginConnection.CheckPk` 保留 |
| 原生 UI 關閉時（預設） | — | Form | **過渡**：可修清單改列成同一個 NPC 對話頁的選項（`fix-item:<物件>:<版本>`，引擎內部值，不送伺服器），點選即送 C20。這不是原版畫面，只為了讓預設模式仍能修理 |

檔案：
- `engine/source/WeaponRepair.cs`：`FixAction`、`TradeWindowMode=6`、`EmptyListPage`、武器選項值；`TradeWindowRow`／`TradeWindowView`；S39 **只觀察**處理器 `ObserveRepairDialog`（經 M7-A 擴充點註冊，不改 `WorldSession.cs`）記錄每位 NPC 的頁名；`PresentWeaponRepair`（空清單開頁／舊介面列選項）；`HandleRepairOption`；`WeaponRepairWindow()`（模式 6 資料）；`LoginConnection.WireWeaponRepairDialog()` 把對話動作接到既有的 `SendWeaponRepair`（重複呼叫只接一次）。既有的世代、距離、逾時、不重送等防護沒有改。
- `engine/source/NpcServices.cs`：`OpenDeposit`（`NpcInteraction` 送 C125 前會先問它）多處理修理動作；倉庫存入邏輯不變。
- `engine/source/WeaponRepairUi.cs`：只剩接線：`WireWeaponRepairDialog`，原生 UI 啟動後把 `TradeWindowPanel.Source` 接上 `RepairTradeSource`（名稱經既有 `ResolveNpcText`、OK 走 `SendWeaponRepair`、關閉走 `CloseWeaponRepair`）。`InitWeaponRepairService` 名稱保留，因為 `NpcUi.cs`（不在本組範圍）會呼叫它。
- `engine/source/NativeUi/TradeWindowPanel.cs`（新）：`IUiPanel`，`TRADE_WINDOW`／`uiextra86`，執行期從 Tile.pak 讀 XML、從 Sprite*.pak 讀 surf。
- `engine/source/NativeUi/UiPanelRegistry.cs`：依 native-ui.md §5.1／§5.2 加**一行**註冊（契約允許的唯一改動點；不在任務列出的檔案內，特此說明）。
- `engine/source/TradeWindowTests.cs`（新）：`[WorldModuleTest("trade-window")]`，由既有的 `--module-tests` 入口自動執行（CI 已跑）。
- `engine/_integration/dev/rebuild-lock.json`：`rebuild.py --update-lock` 重產。

## 已驗證／推測／待驗證

**已驗證（有文件或 XML 依據）**
- 模式 6、空清單頁名 `touma3`／`gayle5`（WIN§2 T134）。
- 面板畫的元件與座標全部取自 `uiextra86-c.xml` 的 `TRADE_WINDOW`：底圖 1983 (6,9)、關閉 X 1962 (225,15)、長捲軸 1970 (217,51)、上鈕 2042 (217,37)、下鈕 2040 (217,275)、OK 1996 (60,292)、CANCEL 1994 (127,292)。程式不寫死這些數字，執行期從 XML 讀；缺元件或缺座標丟 `UiLayoutException`（不推定）；缺圖的元件不畫。

**推測（已在程式註解與這裡標明）**
- `fix` 動作名稱：TW16 HyperAct 名稱字串區 FO `0xDA78AC`（#524 報告的線索，與 `checkpk` 同一區），送 C118 的行為沒有反組譯確認。
- 空清單選頁規則（看 NPC 上一頁名前綴 `touma`／`gayle`）：原版選頁條件沒有追。
- 模式 6 只顯示 OK／CANCEL；清單區＝底圖左緣到捲軸左緣、上鈕底到下鈕頂；列高 16 px；名稱靠左、費用靠右；文字白／選取黃／停用灰；按下即觸發。原版 `TradeWindow` 的列版面寫在 BIN 裡，沒有追。**這部分違反「推測不做畫面」的原則**，理由是任務目標要求可用的原生清單窗；所有圖都是原 surf、座標都來自 XML，沒有自製圖。若要嚴格，可只保留外框、把列與按鈕拿掉。
- 窗口位置用 XML 的 `x=0 y=0`；原版開窗時是否另外定位（`sub_A57C60` 視窗堆疊）沒有追，也還沒做「最多 4 窗」上限。

**沒做（待驗證）**
- **商店（T102）沒有改用 TRADE_WINDOW**：`TradeWindow` 模式 0／1／7／13–16 由誰觸發沒有追（WIN§3），NPC 商店原版用的 `CNpcShop` XML 本機缺檔；商店清單仍在 `npcPanel`（`NpcUi.cs` 不在本組範圍）。
- 框 1984、拉桿 2044、TRADE_UP／DOWN 1985／1987（XML 沒有座標）、ADENA／TOTAL 1990／1991、BUY 1992、SELL 1998（依模式而定）都不畫。
- 按鈕按下／停用的狀態圖（V4）沒有做，只用 OFF 圖。

## 怎麼驗證

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`（`dev-inputs-verified`，274 來源檔）→ `rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`（productDll `86d47ae9…955d`）。
- `P11.ClientEngine.exe --module-tests`：**15 組 829 checks 全部通過**；新組 `trade-window` 79 checks，涵蓋：
  - 註冊：S39 觀察與 reset 掛上、登錄表無錯誤、`TRADE_WINDOW` 在 `UiPanelRegistry` 只註冊一次；`ClientForm` 已沒有 `OpenWeaponRepair`／`weaponRepairWindow`。
  - 協定：未接線時 `fix` 不在本機處理；接線（兩次只接一次）後 `fix` 只送一個 C118、不送 C125；空清單在 `touma1`→`touma3`、`gayle1`→`gayle5`，其他頁名不換頁；有清單時舊介面列選項、點選送 C20（物件＋NPC）、送出後不重送；原生窗接上時不動對話頁、`WeaponRepairWindow` 給模式 6 資料；其他動作仍送 C125。
  - 面板（合成 XML／合成圖，座標刻意與真實檔不同）：原點、Bounds、清單區、可見列數；缺元件／缺座標／缺 `TRADE_WINDOW` 丟例外；缺圖不畫；選列、OK 送出（停用列不送）、上下鈕與滾輪捲動夾限、新版本重設選取、關閉 X／CANCEL／Esc 關閉且同一版本不重開、隱藏時不吃滑鼠與按鍵；底圖畫在原點＋偏移、範圍外不畫。
- 既有測試：`weapon-repair-tests` 98 checks 綠；`DemoChecks` 的 `hypertext`（357）、`native-ui-canvas`（429）、`native-ui-resources`（539）綠。
- `map-continuity` 本機在 `DisplayNoticeChecks` 的「notice S245 unknown message stays labeled」失敗：預設客戶端根目錄 `D:\lineage開發\0-客戶端` 在本機存在，訊息表讀得到，所以 999 被辨識。這是本機環境造成的，與本任務無關（CI 沒有客戶端；main 最近的 CI 是綠的）。
- `engine/tools/check-no-game-assets.py` → pass（errors 0）。沒有新增 `.xml`／`.img`／`.png`。
- CI：見 PR。

## 沒驗證什麼

- **沒有真機**：沒有啟動遊戲、伺服器或 DB。真實客戶端 `uiextra86-c.xml` 與 surf 的實際渲染（native-ui.md §5.2 第 2 項的 800×600 PNG 比對）沒有做。
- 伺服器對 C118 的回應、真實 NPC 對話頁裡有沒有 `action="fix"` 連結、`touma`／`gayle` 頁名前綴是否成立，都沒有確認。
- 原生 UI 開啟時 NPC 對話仍是舊的 `npcPanel`（原生 `HYPER_TEXT_WINDOW` 還沒做），所以空清單頁顯示在 `npcPanel`。

## 產出路徑

- `engine/source/WeaponRepair.cs`、`engine/source/WeaponRepairUi.cs`、`engine/source/NpcServices.cs`
- `engine/source/NativeUi/TradeWindowPanel.cs`（新）、`engine/source/NativeUi/UiPanelRegistry.cs`（一行）
- `engine/source/TradeWindowTests.cs`（新）
- `engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-526.md`

## 給下一棒的提示

- **真機 smoke（test）**：開原生 UI（`--native-ui`），找修理 NPC 對話 → 點修理連結 → 確認送出的是 C118、伺服器回 S83 D200；有損壞武器時 `TRADE_WINDOW` 出現在 (0,0)、OK 送 C20；沒有時對話頁換成 `touma3`／`gayle5`。順便截 800×600 畫面和 `ui_img_render.py` 的底圖比對。
- **查證（REA）**：`sub_A59780`（FO `0x658B80`）模式 6 用哪些按鈕、列的起點與列高、文字顏色；`sub_93F5E0` 空清單時選 `touma3`／`gayle5` 的條件；`fix` 是否註冊到送 C118 的 HyperAct。查到後只需改 `TradeWindowPanel` 的常數與檔頭註解、`WeaponRepairPackets.EmptyListPage`。
- **商店（T102）**：等 `TradeWindow` 其他模式查證後，在 `WorldSession` 加對應的 `TradeWindowView` 來源（`ShopRows` 已有資料），面板本身已支援任意模式的列與 OK／關閉。
- **原生 `HYPER_TEXT_WINDOW`** 做好後，空清單頁會自然顯示在原生窗（`PresentWeaponRepair` 用的是同一個 `ShowPage`）。
- 本機跑 `map-continuity` 要先把 `TW_CLIENT_ROOT` 指到不存在的路徑，才會和 CI 一致。
