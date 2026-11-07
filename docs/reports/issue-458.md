# Issue #458 報告：V3 查證——舊封包狀態圖示的圖示對照、畫面位置與排序

日期：2026-11-07（Asia/Taipei）。runner：claude-local。階段：resource，第 1 次嘗試。

## 結論

- **圖示資料來源：** `Tile.pak` 的 `effectlist2.xml`（540 筆，id 0–539）。舊封包處理函式把封包換成 effect id，客戶端再用 id 查表，取得 `icon`／`offIcon`／`kind`／`priority`。
- **圖示檔：** `<icon>.tbt`，32×32，放在 `Sprite*.pak`，**不經過 SurfList**。
- **畫面位置：** 右上角，第一格在 (763, 5)。往左每格 35 px，每列 22 格。增益在前；減益（kind 為 slow／curse／paralyse）從下一列開始。
- **排序：** 先比 `priority`（小的在前），其次依到達順序。選項旗標 `0x800` 會改成依剩餘時間排序。
- **剩餘時間：** 不畫在圖示上，只在 tooltip 顯示。時間用完、但伺服器還沒送移除時，改畫 `offIcon`。
- **`SpellIcon.idx`：** 不是 buff 圖示來源，兩個 BIN 中都沒有引用它的字串。
- **除錯字串 `0x1183E1C`–`0x1184C34`：** 沒有任何 xref，只是殘留字串。

完整的對照表與地址已併入 `docs/reference/ui-layout.md` 第 4 節。

## 做了什麼

1. 依 T161 方法，把 P01 IDB（SHA-256 `3a0bf206…`，與文件一致）複製到 `scratch/rea/ida/tw16.idb`，用 IDA 7.7＋Hex-Rays 執行 9 輪唯讀查詢（q1–q9，已存成 `rea-scripts/buff_icon_queries.json`）。分析對象是 `TW1608262200.bin`（SHA-256 `e12949cf…`）。
2. 從 TW16 的分派表找出舊封包處理函式：`S_SPEED`、`S_EMOTION`、`S_MAGE_SHIELD`、`S_MAGE_STRENGTH`、`S_MAGE_DEXTERITY`、`S_BREATH`、`S_INVISIBLE`、`S_PARALYSE`、`S_EVENT`（packetbox）。發現它們最後都呼叫 `sub_F05A90(秒, effect, …)`。
3. 從 `sub_F05A90` 往下追：表格紀錄（`dword_13D4304`）→ 載入者 `sub_AA0610`（檔名 `effectlist2.xml`）→ XML 屬性解析 `sub_81DF30` → 圖示類別 `LStatusEffectButton`。接著讀出它的版面 `sub_903470`、排序 `sub_8FDE00`／`sub_8FDD20`、繪製 `sub_9001F0`、tooltip `sub_9061E0`，以及圖示載入 `sub_87B3A0`（`"%d.tbt"`）。
4. 用 `ui_xml_decrypt.py` 把 `Tile.pak` 的 XML 解到 `scratch/rea/uixml/`。`effectlist2.xml` 的 SHA-256 與 #448 解出的版本相同（`fc522b85…`）。再用新增的 `buff_icon_table.py` 讀 `.tbt` 標頭，產出 effect→圖示表。
5. 解析 `Sprite.pak` 的 `SpellIcon.idx`（輸出只在 `scratch/rea/out/pak/`）。
6. 與 TW13081901 交叉比對字串，並與引擎 `MagicStatus.cs` 的快照格位比對。

## 怎麼驗證

- 每個地址都以 IDA 反編譯結果為準，並用 `nat.py` 換算 File Offset（`.text` VA−0x400C00、`.rdata` VA−0x401400、`.data` VA−0x401800）。字串的 FO 另外用原始 byte 搜尋核對：`effectlist2.xml` 在 FO `0xD9B4F4`，`%d.tbt` 在 FO `0xD7C524`。
- 所列 effect 的 `icon`／`offIcon` 都在 `Sprite*.pak` 中找到對應的 `.tbt`，標頭都是 32×32，與版面函式寫死的 32×32 一致。只有 effect 480 的兩個圖示缺檔，1740 有一份是 30×30。
- 代理 key 518–537 正好等於 `effectlist2.xml` 中 `graphic` 為 518–537 的 effect（`rea-scripts/buff_graphic_map.py` 逐一比對），與 `docs/protocol/status-icons.md` 的代理 key 一致。
- 引擎快照格位的倍率中，第 10／11、23、52–54、60 格與 TW16 讀法一致。第 3 格不一致：引擎用 4 秒，TW16 用 16 秒。
- `git ls-files` 沒有新增 `.tbt`、`.img`、`.xml`、`.png` 或其他遊戲資料。只新增或修改 `docs/` 下的 md／py／json。

## 未驗證什麼

- 沒有啟動遊戲或真機截圖。位置、排序、offIcon 的視覺效果都只來自程式碼。
- `.tbt` 的 RLE 解碼規則。
- 56 個圖示在 `Sprite.pak` 與 `SpriteNN.pak` 中各有一份且內容不同，客戶端讀哪一份要看 V4 (e)。
- 250/21（ICONS2）、`S_Paralysis`、250/20 開頭的長度欄位：TW16 的讀法與 3.81 serializer 不同。伺服器實際送出的 byte 會被解成什麼，沒有用封包紀錄核對。
- 選項旗標 `0x800` 的預設值，以及字串 930／931／2854 的實際文字。
- `SpellIcon.idx` 的實際用途。
- 沒有改任何產品程式、DB 或伺服器。

## 產出路徑

- `docs/reference/ui-layout.md` 第 4 節（重寫），並更新第 7 節差異 #11、「給後續任務」與腳本表
- `docs/reference/rea-scripts/buff_icon_table.py`（effect→圖示表產生器）
- `docs/reference/rea-scripts/buff_handlers.py`、`buff_casegrep.py`（處理函式與呼叫點）
- `docs/reference/rea-scripts/buff_icon_queries.json`（IDA 查詢 q1–q9）
- `docs/reference/rea-scripts/ui_ida_query.py`：支援 `ranges`（依位址範圍反編譯），JSON 允許 UTF-8 BOM
- 本報告 `docs/reports/issue-458.md`
- 暫存（不進 git）：`scratch/rea/`（IDB 副本、反編譯輸出、解密 XML、`SpellIcon.idx`、`effects.md`）

## 給下一棒的提示

- **ui-buff-icons 不能只靠 C1 的 `SurfCatalog`。** 圖示是 `<icon>.tbt`，不是 SurfList 的 `.img`。需要另外做：
  - `.tbt` 解碼器（RLE 格式待查，可以從 `sub_878AE0`／`sub_66E050` 型別 3 追）
  - `effectlist2.xml` 解析
  
  建議主控開一張小的查證或 code 任務專做 `.tbt`，或把它併進 V4。`native-ui.md` 2.3 節的契約也要補一句「buff 圖示走 `.tbt`」。
- **實作規則：** 照 `ui-layout.md` 第 4 節做：
  1. 封包 → effect id，用「舊封包 → effect id」表。
  2. effect id ≥ 540 或表中沒有的 → **不畫**，記錄下來（客戶端本身也不建立）。
  3. 版面公式 `x = 798 − 35·(n mod 22 + 1)`、`y = 5 + 35·⌊n/22⌋`，增益在前、減益從下一列開始。
  4. 比較器用預設的 `sub_8FDE00`。
  5. 剩餘時間只放在 tooltip。
- **雙份 `.tbt`：** 等 V4 (e) 的優先序。結果出來前，依 C1 契約拒絕雙份不同內容的圖示，否則 56 個常用圖示（包含 SHIELD、SLOW、各種 attack／defense）會無圖可畫。這點要請主控決定，是先等 V4，還是暫時指定 `SpriteNN` 優先。
- **真機 smoke（ui-native-smoke）要核對：** 第一格在 (763, 5)；加速（priority 5）排在勇敢（priority 6）前面；到期後切換成 offIcon；tooltip 文字。
- **引擎快照第 3 格的倍率（4 或 16 秒）** 要用封包紀錄確認，屬於 T108／`MagicStatus.cs` 的問題，不在 ui-buff-icons 範圍內。
