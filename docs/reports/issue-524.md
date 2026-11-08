# Issue #524 報告：T218 M7-R HTML 結果改走 NPC 對話窗

日期：2026-11-08。階段：code（第 1 次嘗試）。分支：`task/524`。
依據：`docs/reference/gap-matrix.md` 第 7 節 M7-R；`docs/reference/windows.md`（WIN）§2 T140、T134，§2.1。

## 做了什麼

| WIN 條目 | 原版（文件） | 改動前 | 改動後 |
|---|---|---|---|
| T140 查詢玩家、排名 | `S_HYPERTEXT` 結果顯示在 NPC 對話窗（`HYPER_TEXT_WINDOW`） | `y_who`、`pc_show`、`/rank` 的 S39 結果全部轉成聊天行「[查詢] …」；NPC 排名頁另外也鏡射到聊天 | S39 結果改在 **NPC 對話面板的「查詢結果」頁**顯示，不寫聊天。NPC 排名頁只留在對話頁裡 |
| T134 PK 查詢 | HTML 動作 `HyperActCheckPK`，沒有專用視窗 | 只有 `WeaponRepairUi` 的專用鈕 | NPC 對話頁裡 `action="checkpk"`（不分大小寫）的選項改送**無欄位的 C51**（`WeaponRepairPackets.CheckPk()`），不送 C125；送出後等伺服器回覆（S71 會經過既有的 `InteractionMessage` 顯示在面板上） |
| §2.1 共用視窗 | NPC 頁、修理空清單、查詢與排名結果共用同一個視窗 | — | 查詢頁和 NPC 頁放在同一個 `npcPanel`。查詢頁獨立存放（`QueryPage`），NPC 對話、商店或已送出的訂單進行中收到查詢結果時，**不會蓋掉 NPC 狀態**，兩者上下並列 |

檔案：
- `engine/source/PlayerQuery.cs`：三個解碼器多回傳 `Html`、`Arguments`；`ReceivePlayerQueryHtml` 改成呼叫 `ShowQueryPage`，NPC 排名頁改呼叫 `OfferDialogFallback`。**S71 文字**（`/who` 的玩家資訊摘要、`/check` 失敗訊息）伺服器本來就是用聊天文字送的，所以**仍保留為聊天行**（m6-native-diff：「聊天行只留伺服器原本就送到聊天的訊息」）。
- `engine/source/NpcInteraction.cs`：
  - `RenderHyperText`：有原版模板就用模板（參數只插進文字）；沒有模板時，已知的查詢頁用解碼後的純文字顯示，並附上 `HyperTextFallbackNotice`，說明原版版面還沒做。
  - `QueryPage`／`QueryHtml`／`QueryRevision`／`QueryVisible`／`ShowQueryPage`／`CloseQueryPage`：只收「發給自己」的結果；換世界（epoch）或未 Ready 就不顯示；新結果會蓋掉舊結果（同一個視窗）；自己頁面上的連結和動作會顯示出來，但一律停用。
  - `CheckPkAction="checkpk"` 分派、快照多一個 `query` 欄位。
  - `dialogFallback` 只給同一個 S39 封包用，每次進 `ReceiveInteraction` 都會清掉，不會帶到下一頁。
- `engine/source/NpcUi.cs`：面板在「NPC 對話開著」或「查詢頁可見」時顯示；查詢頁畫在內容最上方（標題「查詢結果」、內文、限制說明、停用的選項）；只有查詢頁時，上方按鈕變成「關閉查詢結果」；NPC 也開著時，查詢頁另外有自己的關閉鈕。Web 狀態多一個 `queryText`。
- 測試：新檔 `engine/_integration/dev/hypertext-tests/HyperTextChecks.cs`，接到 `demo-tests` 的 `hypertext` 群組，也接進 `player-query`、`map-continuity`（CI）和預設全套；`build.yml` 加一個步驟「HyperText query result contracts T218」。既有 `PlayerQueryChecks.cs` 裡原本預期「結果寫進聊天」的 8 個斷言，改成預期「結果在查詢頁」；README 同步更新。
- `engine/_integration/dev/rebuild-lock.json`：`rebuild.py --update-lock` 重產。

## 怎麼驗證（已驗證）

- 建置：`rebuild.py --update-lock` 後 `rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`（sdk、product、launcher 全 0）。
- 本組離線測試 `DemoChecks … hypertext` → PASS。涵蓋：
  - `y_who`、`pc_show`、自己的排名頁會開查詢頁，不寫聊天，也不擋 NPC 對話；
  - 缺模板時用文字顯示並附說明；有模板時照模板渲染，選項停用；
  - 新結果會取代舊結果；關閉只算一次；session reset 後查詢頁隱藏；
  - NPC 請求等待中收到查詢結果，NPC 狀態不變，兩頁並存；
  - NPC 排名頁有模板用模板，沒模板用解碼列；
  - 別的 NPC 送來的排名頁被拒絕後，fallback 不會帶到下一頁；
  - `checkpk`／`CheckPK` 送出 C51、不重送；其他動作仍送 C125；目標失效時不送；
  - 快照的 `query` 欄位。
- 既有測試：`player-query`（1058）、`map-continuity`（32983）；CI 的 demo 群組 afflictions、buddies、combat-admission、doors、item-status、item-transfer、native-ui-resources、pickup、polymorph-cadence、resurrection 全綠；`npc-tests` 165 綠；`weapon-repair-tests` 98 綠。
- `engine/tools/check-no-game-assets.py` → pass（errors 0）。
- CI：見 PR。

## 沒驗證什麼／限制

- **沒有真機**：沒有啟動遊戲、伺服器或 DB。實際的查詢頁外觀、伺服器對 NPC 頁發 C51 的回覆，要等 smoke 確認。
- **原版 `HYPER_TEXT_WINDOW` 版面沒做**（239×317、關閉 X、捲軸；WIN§2.1），依任務說明等 T190。目前是 WinForms `npcPanel` 裡的文字頁，不宣稱是原版畫面。
- **`checkpk` 動作名稱屬於推測（有字串證據）**：在 `TW1608262200.bin` 的 HyperAct 名稱字串區唯讀找到 `checkpk`（FO `0xDA78A4`），旁邊是 `fix`、`getall`、`exitghost`（`exitghost` 已由 WO 文件證實是工廠 `sub_BB5180` 註冊的名稱）。RTTI 有 `.?AVHyperActCheckPK@@`（FO `0xF7C6E0`）。但 `checkpk` 註冊到 `HyperActCheckPK` 的交叉引用、以及它的動作函式送出 C51，**都沒有反組譯確認**。所以這部分只做協定分派，沒有新畫面。
- 查詢頁的模板：本 worktree 沒有 `engine/assets`，測試用合成模板。真實客戶端模板裡有沒有 `y_who`、`pc_show`、`y_h_2` 等頁（有的話會改用模板渲染），沒有確認。
- 查詢頁上的連結和動作一律停用。原版自己頁面的連結（例如重新整理）怎麼運作，沒有查證。
- `WeaponRepairUi` 的專用 PK 鈕和 HUD 鈕還在（`WeaponRepair*.cs` 不在本任務範圍；m6-native-diff 寫的是「等原生窗完成再移除」）。
- 原版是「單一視窗」：新內容會取代 NPC 頁。引擎讓查詢頁和 NPC 頁並列，是刻意保守的做法，避免查詢結果打斷進行中的商店或訂單。

## 產出路徑

- `engine/source/PlayerQuery.cs`、`engine/source/NpcInteraction.cs`、`engine/source/NpcUi.cs`
- `engine/_integration/dev/hypertext-tests/HyperTextChecks.cs`（新）、`engine/_integration/dev/player-query-tests/{PlayerQueryChecks.cs,README.md}`
- `engine/_integration/dev/demo-tests/{DemoChecks.csproj,Program.cs}`、`.github/workflows/build.yml`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-524.md`

## 給下一棒的提示

- **T190／原生 `HYPER_TEXT_WINDOW`**：資料來源直接用 `WorldSession.QueryPage`（`NpcPage`：Text／Options／Limitation）與 `InteractionPage`，用 `QueryRevision`／`InteractionRevision` 判斷要不要重畫。之後如果要改成原版的「單一視窗取代」，只要在 `ShowQueryPage` 決定是否 `CloseInteraction`。
- **T134 收尾（`WeaponRepair*.cs`）**：NPC 頁的 PK 查詢已經走 `checkpk`，移除專用 PK 鈕時不用再補協定。修理在原版的 HyperAct 名稱是 `fix`（同一個字串區，FO `0xDA78AC`，推測），可以用同樣方式接。
- 若要把 `checkpk` 從推測升級成已驗證：在 IDB 找 `sub_BB5180` 對 FO `0xDA78A4` 字串的引用，以及 `HyperActCheckPK` vtable 的執行函式是否送 opcode 51。
