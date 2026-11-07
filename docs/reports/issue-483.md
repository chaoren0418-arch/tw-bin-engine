# Issue #483 報告：T181 查證原版——視窗類功能（G10b）

日期：2026-11-08（Asia/Taipei）。runner：claude-local。階段：resource，第 1 次嘗試。分支：`task/483`。

## 結論

- **兩套視窗系統。** TW16 同時有兩套視窗：
  - 舊式寫死視窗：由 `sub_8C7C80` 建立，例如 `TradeWindow`、公告板 3 窗、`PetControl`。
  - XML 視窗：由 `sub_B56A40` 建立，例如 `FriendUI`、`SummonUI`、`PersonalShopUI`、`ResetStatUI`。
- **XML 缺檔。** TW16 要的 UI XML 中有 34 個不在本機任何 pak 裡，包括 `SelectUI`、`CreateUI`、`ResetStatUI`、`FishingUI`、`RenewalMailUI` 與 NPC 商店 XML。缺檔時 `sub_B3D390` 只會跳出 `Cannot Found %s`，沒有退回機制。所以 T107、T131、T139、T142 只能查到類別與觸發，查不到版面。
- **`ReNStatusWinUI-c.xml` 不是重置介面。** 它是新版裝備與能力窗，而且 TW16 不讀它。重置介面是 `CResetStatUI`，由 TW16 opcode 43 的子類型 1–3 開啟。
- **公告板**用專用舊式窗，不是 `HYPER_TEXT_WINDOW`。**修理**用 `TRADE_WINDOW` 的模式 6，清單為空時開 NPC 對話頁。**求婚**走通用 Yes/No 框，沒有專用視窗。**`MESSAGE_WINDOW`** 只有 OK 鈕。
- **逐項結果（12 項）：**

  | 結果 | 數量 | 功能 |
  |---|---:|---|
  | ❌ | 2 | T134、T141 |
  | ⚠️ | 8 | T102、T107、T126、T127、T130、T139、T140、T142 |
  | 仍 ❓ | 2 | T131（資源缺）、T133（本地刪除確認未追） |

## 做了什麼

1. 把 P01 IDB 複製到 `scratch/rea/ida/tw16.idb`（SHA-256 `3a0bf206…`），用 IDA 7.7＋Hex-Rays 做了 q1–q6 六輪唯讀查詢，內容存成 `rea-scripts/windows_queries.json`。
   - 每輪先檢查 subst：W:、Q: 被 task-484 占用，所以改挑空的代號（本輪實際用 Q:、V:）。
2. `ui_ida_query.py` 新增 `rtti` 查詢，路徑是 TypeDescriptor → COL → vtable → 建構子 → 呼叫者。用它從 RTTI 類別名找出各視窗的建構子與建立者。
3. 用 `ui_xml_decrypt.py` 把 65 個 XML 解到 `scratch/rea/uixml/`，不進 git。解讀了這些：
   - `PersonalShopUI`、`FriendUI`、`ExcludeUI`、`MailExcludeUI`
   - `SummonUI`、`summonList`
   - `ReNStatusWinUI-c`
   - `uiextra86-c` 的 `MESSAGE_WINDOW`、`HYPER_TEXT_WINDOW`、`PET_QUICK_WINDOW`、`TRADE_WINDOW`
4. 從封包名稱表與 dispatch table 找出相關處理函式並反編譯：
   - `S_ASK`、`S_BOARD_LIST`／`S_BOARD_READ`、`S_FIXABLE_ITEM_LIST`、`S_SELL_LIST`／`S_BUY_LIST`
   - `S_PERSONAL_SHOP_LIST`、`S_MAIL_INFO`、`S_HYPERTEXT`
   - opcode 43
5. 新增 `docs/reference/windows.md`：每項都寫「原版來源／引擎現況／一致？」，並對應 `native-ui.md` 的模組。
6. 回填 `m6-native-diff.md`：
   - 第 1 節：更新 12 列，新增引用代號 WIN§n。
   - 第 2 節：重算計數，有差異 16→26、未查證 24→14。
   - 第 3 節：新增 G11（原生視窗模組，design 先行）。
7. 更新 `INDEX.md` 一列，並修正 `ui-layout.md` UI#15 中公告板「可能用 HYPER_TEXT_WINDOW」的推測。

## 怎麼驗證

- 所有地址都以反編譯結果為準。VA 換 FO 用 `va2off.py`，字串的 FO 用原始 byte 搜尋（`win_strings.py`）交叉核對。
- 缺檔判定：`xml_table.py` 列出 BIN 的 XML 檔名表，`idx_find.py` 再到客戶端所有 `.idx` 搜尋，含 `-c` 變體。
- 引擎現況是讀 main `23b129ac` 的原始碼，並抽查行號：`BoardUi.cs:17`、`Marriage.cs:149`、`MailUi.cs:49`、`WeaponRepairUi.cs:18`、`CharResetUi.cs:16`、`ItemTransfer.cs:116`、`Trade.cs:43`。
- `git status` 確認：只新增或修改 `docs/` 下的 md、py、json。沒有 XML、`.img` 或遊戲資源。

## 未驗證什麼

- 沒有啟動遊戲、伺服器或 DB，也沒有改產品程式；所有結論都來自靜態分析。
- 缺檔 XML 的版面無法取得。`LiTo.bin`（實際執行的 3.81 BIN）有 Themida 殼，不在本輪範圍。
- 還沒追的細節：
  - `sub_AC9560` Yes/No 框的 surf 與尺寸
  - `CNpcShop` 用哪個 XML
  - `TradeWindow` 除了模式 6 以外的觸發者
  - 好友名單上限與 `Info` 欄內容
  - `CSummonUI` 開窗呼叫點（`0xDF8AFB`、`0xE0415C`）對應的封包
  - 刪除物品的本地確認

## 產出路徑

- `docs/reference/windows.md`（新增）
- `docs/reference/m6-native-diff.md`、`docs/reference/INDEX.md`、`docs/reference/ui-layout.md`（更新）
- `docs/reference/rea-scripts/`：
  - `ui_ida_query.py`：新增 `rtti`
  - 新增 `win_strings.py`、`rtti_names.py`、`xml_table.py`、`idx_find.py`、`windows_queries.json`
- `docs/reports/issue-483.md`（本檔）
- 暫存（不進 git）：`scratch/rea/`，含 IDB 副本、反編譯輸出與解密 XML

## 給下一棒的提示

- **建議主控開 G11 design。** 把 `native-ui.md` §0 的「不涵蓋」視窗拆成模組。優先順序：
  1. `ui-yesno`：所有 `S_ASK` 共用，可取代引擎目前逐功能的按鈕。
  2. `ui-hypertext`：NPC 對話，也是查詢、排名、修理空清單提示的共用視窗。
  3. `ui-shop`：`TRADE_WINDOW`，含修理模式 6。
  4. `ui-board`：surf 與座標已在 windows.md。
  5. `ui-friend`：三窗共用一個類別。
  6. `ui-summon`、`ui-personal-shop`。
- **需要使用者決定或提供資源：** T107、T131、T139、T142 的 XML 在本機缺檔，要嘛提供 TW16 對應的 `Tile.pak`，要嘛改以 3.81 實機截圖為準（`needs:user`）。資源到位前，這四項不要開 code。
- **`ReNStatusWinUI-c.xml` 的去處：** 角色狀態窗設計（§0 不涵蓋）可以直接用它，但要註明 TW16 讀的是 `ReNStatusUIEx.xml`，而這個檔本機沒有。
- **IDA 執行注意：** `run_ui_query.ps1` 遇到磁碟代號已被占用時會直接退出。並行任務多時，先用 `subst` 看哪些代號空著，再指定 `-Drive`。
