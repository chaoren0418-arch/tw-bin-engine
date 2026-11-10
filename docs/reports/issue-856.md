# Issue #856（T372）R9-L 背包操作 — 報告

依據：`docs/reference/followups-9.md` §13（Q7-13）與 §16.2 R9-L 列、`followups-8.md` §8.1、`gap-matrix.md` §15.11。

## 做了什麼

### `engine/source/NativeUi/ItemWindowPanel.cs`
- `LegacyWindowPanel`（同檔基底類）：新增 `Click(UiMouseEvent, …)` 多載（帶 `Clicks`，預設轉給原本的 `Click(Point, …)`），`Key` 改成 `virtual`。法術窗、倉庫窗行為不變。
- 新介面 `IItemWindowHost { Use(id); Delete(id); }`，以及靜態 `ItemWindowPanel.Host`（由 `ClientForm.InitItemTransfer` 設定）。建構子可另外注入 host 與 `find`（`root.Find`，用來看聊天輸入列有沒有開）。`[UiPanel(175)]` 自動發現的註冊只多傳 `find:root.Find`，沒有改任何註冊表。
- **格子雙擊使用**（FU9§13.1）：同一格按第二下（`Clicks ≥ 2`，而且之前已經選了這一件）→ `Host.Use`。第一下只選取。U 鍵保留。
- **垃圾桶**（`SURF_RENEWAL_BTN_ITEM_WASTE_OFF` 2052，(142,260)）：有選取 → `Host.Delete(選取)`；垃圾桶列入控制項，不能拿來拖窗。
- **Delete 鍵**（4142，FU9§13.2）：窗開著、沒有修飾鍵、不是自動重複 → 同垃圾桶。聊天輸入列開著時不攔（Delete 留給聊天列，和 R9-H 好友窗一樣）。
- **寶箱** 2054：照 FU9§13.3 不做，只畫圖，點了不送任何東西。

### `engine/source/ItemTransfer.cs`
- `ItemDeleteText`：新增 668（`AskFirst`）；數量提示改成 4115 → **3054** → 原文代用（`%u`＝持有量）。本機 `string-c.tbl` 原文（色碼拿掉）當 fallback：
  - 668 `真的要把 \fQ"%s"\f> 刪除嗎？(Y/N)`
  - 3054 `要刪除幾個呢？(1-%u, Enter=全部, Esc=取消)`
  - 3122 `\fQ"%s"\f> 刪除後，即無法再次復原，真的要刪除嗎？ (Y/N)`（R6-E 已有）
  - 4114、4115：本機表筆數之外（沒有），沿用引擎文字。
- 新類別 `ItemDeleteAlertList`：`alertitems.tbl`（Text.pak 第 806 筆，解出後 1,382 B，232 個編號，每行一個整數，沒有筆數行，SHA-256 pin `a39418ec…5149ceae`）。執行期從客戶端延遲唯讀載入；讀不到或 pin 不符時是 null，這時**一律再問**（只會比原版多問，不會少問）。
- `LoginConnection.BeginInventoryDelete`（背包窗流程，sub_9A52C0 → sub_98D2E0）：
  - 單件 → **668** Y/N → 是 → 物品在 `alertitems.tbl` 才再問 **3122**，不在就直接送 C138；
  - 堆疊（數量 > 1）→ 數量提示（**3054**；Enter／空白＝全部；只輸入部分時不送，原因同 R6-E：3.81 C138 只帶物件編號、伺服器整疊刪）→ 在 `alertitems.tbl` 才再問 4114，否則直接送。
  - `ItemDeletePrompt` 多兩個選填欄位 `AskFirst`、`AlertOnly`；`AnswerItemDelete` 的送出改用共用的 `FinishItemDelete`。
- R6-E 的 `BeginItemDelete`（WinForms 垃圾桶鈕、拖進垃圾桶）**不變**：單件直接 3122、一律確認。既有測試 `chat-command-followup` 依舊照這個行為。
- `ClientForm.NativeItemWindowHost`：`Use` ＝ 選取後走既有 `UseSelectedItem`（同 U 鍵與「使用」鈕，送 C208 的路徑）；`Delete` ＝ 選取後 `DispatchItemTransfer(…, inventoryWindow:true)` → `BeginInventoryDelete`。`DispatchItemTransfer` 多一個選填參數 `inventoryWindow`。

### `engine/source/NativeUi/InventoryActionTests.cs`（新檔）
`[WorldModuleTest("inventory-action")]`，CI 的 `--module-tests` 會跑：
- 文字：668／3054 的 fallback 原文、合成表讀 668／3054（`%s`、`%u`、色碼拿掉）、超出表的編號改用 fallback。
- `alertitems.tbl`：解析（空行、空白、`\r`、沒有筆數行）、非整數／負數 → 例外；合成 Text 封存 pin 不符、沒有這個檔 → 例外；清單內才要確認、沒有表一律確認。
- 刪除流程（合成封包、記憶體內假傳送端）：單件不在清單（668 N 不送、Y 直接送一次 C138、之後這一列回到聊天）；單件在清單（668 Y → 3122，N 不送、Y 送）；沒有清單一律問 3122；堆疊不在清單（部分數量拒絕、Enter＝全部直接送、打滿數量直接送）；堆疊在清單（數量 → 4114 → Y 送）；R6-E 路徑不變；新提示取代舊請求、換世界後提示失效。
- 背包窗輸入：單擊只選取、同格雙擊 → `Use`、別格 `Clicks=2` 只選取、右鍵不動作；垃圾桶不是拖曳區、有選取 → `Delete`；寶箱不送；Delete 鍵 → `Delete`，自動重複、放開、Shift+Delete、其他鍵不收；聊天列開著時不收；沒有選取、沒有宿主、關窗時不刪；預設宿主是 `ItemWindowPanel.Host`。

## 怎麼驗證
- **本機編譯：通過（0 錯誤；改動的三個檔案沒有警告）**。這台機器的 WebView2 有已知的 NETSDK1130 問題（#817／#846），只為編譯在 `scratch/task-856/fix.targets` 用 `-p:CustomAfterMicrosoftCommonTargets` 拿掉 `.winmd`、改參考 net462 DLL（從專案的 `engine/.nuget` 唯讀參考）；repo 檔案沒動。指令：
  `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -p:CustomAfterMicrosoftCommonTargets=<scratch>\fix.targets -o scratch/task-856/build`
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4341 個追蹤檔，0 錯誤）。
- 字串原文與 `alertitems.tbl` 的大小、筆數、SHA：用 `scratch/task-856/probe_text.py`（唯讀，在記憶體解 `D:\lineage開發\0-客戶端\Text.idx／Text.pak`，沒寫任何檔）讀出：
  - 查的 pak：`Text.pak`（`Text.idx` 索引）；檔名：`string-c.tbl`（第 32559 筆，105,931 B，SHA `4ebbc1a7…`）、`alertitems.tbl`（第 806 筆，1,382 B，232 筆，全部是整數）。
  - 讀出的編號：668、3049、3054、3122 有；4114、4115 超出表的筆數。
  - 圖（2052 垃圾桶、2054 寶箱）沿用 R4-B 既有的 surf，本組沒有新圖。
- 離線測試 `inventory-action` 由 CI 執行（本機依規定不執行任何編譯出來的程式）。CI 結果見 PR。

## 未驗證什麼（真機再確認）
- 雙擊是不是原版的觸發事件（FU9§13.1 屬推測）；雙擊判定用 WinForms 的 `Clicks`。
- `alertitems.tbl` 的編號對到 3.81 哪個欄位：引擎用 S_AddItem 的 `DescriptionId`（推測，FU8§8.1 待對 SP 原始碼）。對錯時的影響只是多問或少問一次 3122，刪除本身仍要先過 668 或數量提示。
- 數量 1 的可堆疊物品：引擎以「數量 > 1」判定堆疊，數量 1 一律走 668（原版走哪一支沒追）。
- 原生介面下，刪除提示出現在哪：沿用 R6-E，把提示放在聊天輸入列的 notice（`playerChatNotice`），答覆經 `SendPlayerChat`；原生聊天列是否自動打開、提示是否看得到要真機看。
- 3054 寫「Esc=取消」，但聊天列的 Esc 目前沒有接 `CancelItemDelete`（R6-E 就這樣；不在本組可改檔案）。現在取消的方法是回答 N／非數字。
- 背包窗自己的 Esc（27 關窗）沒做；全域 ESC（R7-H）仍會關這個窗。
- 多選刪除（3049「%d 次」）：3.81 背包窗沒有多選，不做。

## 產出路徑
- `engine/source/NativeUi/ItemWindowPanel.cs`、`engine/source/ItemTransfer.cs`、`engine/source/NativeUi/InventoryActionTests.cs`
- `docs/reports/issue-856.md`（本檔）
- 分支 `task/856`

## 給下一棒的提示
- 真機（Codex／dot，P24Lab 測試角色）：開背包窗（選單 4／Tab），雙擊藥水看是否使用；選一件非堆疊物品按垃圾桶與 Delete，看聊天列是否出現 668、Y 之後是否直接刪（不在 alertitems）或再問 3122；堆疊（箭）看 3054。
- 若要讓 Esc 取消刪除提示：在聊天輸入列的 Esc 處理（`PlayerChatKeyboardFlow.cs`／原生 `ChatPanel`）呼叫 `LoginConnection.CancelItemDelete()`，需另開任務（不在本組檔案範圍）。
- WinForms 垃圾桶要不要也改走 668／alertitems：改 `DispatchItemTransfer` 的呼叫端傳 `inventoryWindow:true` 即可，但 `chat-command-followup` 的 R6-E 測試要一起改。
