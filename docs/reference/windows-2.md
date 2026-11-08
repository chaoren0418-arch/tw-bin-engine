# 原版視窗查證（二）：視窗世代與待查視窗（T226／Issue #533）

整理日期：2026-11-08（Asia/Taipei）。範圍：[`gap-matrix.md`](gap-matrix.md) 第 8 節的「3.81 世代用哪套視窗」「血盟窗」「變身選擇窗」「城堡、盟屋、數量輸入的畫面」「其他 XML 窗」「其他舊式窗」「信件、釣魚、能力值重置」七列。前一份是 [`windows.md`](windows.md)（WIN，T181）。

## 0. 方法與身分

- **原版：** 未加殼的 `TW1608262200.bin`（P01 `input/`），SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。
- **IDB：** P01 `ida/03-ascii-isolated/TW1608262200.idb`，SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`，複製到本 worktree 的 `scratch/rea/ida/tw16.idb`（複製前後雜湊相同）。IDA 7.7＋Hex-Rays，唯讀批次查詢 7 輪（b1–b7，清單見 `rea-scripts/t226/queries.json`）。
- **subst：** 執行前 `subst` 無輸出（沒有被占用的代號）；本輪用 `N:`，每輪結束解除。中途有一次因為把輸出接到 `Select-Object -First 1`，`finally` 沒跑到而留下 `N:`，下一輪開始前已手動 `subst N: /D`。結束時 `subst` 無輸出。
- **UI XML：** `rea-scripts/ui_xml_decrypt.py` 解到 `scratch/rea/uixml/`（本機 `D:\lineage開發\0-客戶端`，65 個，`Tile.idx` `9291fd80…`）與 `scratch/rea/uixml_alt/<客戶端>/`（本機其他版本客戶端，見第 8 節）。**沒有任何 XML、pak 或圖檔進 git。**
- **地址：** `.text` FO = VA − `0x400C00`；`.rdata` FO = VA − `0x401400`；`.data` FO = VA − `0x401800`（同 WIN§0）。
- **3.81 伺服器：** [`server-protocol.md`](server-protocol.md)（SP）的欄位表，另唯讀讀了 `1-伺服器/src/.../C_Pledge.java` 與 `S_Pledge.java`（SHA-256 `5fbe220f…2e4a`）。
- **引擎：** 同步後的 main `401ffab5` 的 `engine/source`，只讀。
- 沒有啟動遊戲、伺服器或 DB。

**TW16 封包編號**沿用 gap-matrix 第 1 節（例如 op 108 是 S_EVENT，等於 3.81 的 S250 PACKETBOX，見 [`protocol-gaps.md`](protocol-gaps.md) 第 2.2 節）。

**記號：** 已驗證＝反編譯或資料直接看到；推測＝依名稱、結構推斷；待驗證＝本輪沒查到。

## 1. 結論摘要

| 主題 | 原版（TW16）實際做法 | 3.81 伺服器 | 能不能開 code |
|---|---|---|---|
| 背包 | 熱鍵與道具流程只開 XML 版 `RenewalInventoryUI`（`ReInventory.xml`）；舊式 `InventoryWindow` 只剩寵物背包的基底類別 | 協定已有 | **可以**：版面用本機 `uimagicitem.xml` 的 `ITEM_WINDOW`（3.81 世代設定，surf 全在本機） |
| 魔法 | 熱鍵開 XML 版 `SpellUI`；舊式 `SpellWindow` 仍建立並受視窗堆疊特別處理 | 協定已有 | **可以**：版面用 `uimagicitem.xml` 的 `SPELL_WINDOW` |
| 倉庫 | `S_RETRIEVE_LIST` 類型 3／5／9／18 一律開 XML 版 `RenewalWareHouseUI` | 類型 3／5／9／18 與 TW16 一致 | **可以**：版面用 `uiextra86.xml` 的 `WAREHOUSE_WINDOW` |
| 血盟 | `PledgeUI` 讀 **`RePledgeUI.xml`**（本機缺）；資料由 S250 子碼 167–171 送進來 | 只有 169、171 是正常 S250；167、170 送錯 opcode | **部分**：171、169 可做；版面以本機 `PledgeUI.xml` 為準（3.81 世代），`PledgeFocusUI.xml` TW16 不讀 |
| 變身 | 3.81 的 S39 `monlist` 走 NPC 對話窗（HTML）；`CPolymorphUI` 只在玩家選項開啟時才用 | S39 `monlist` | **可以**：S39 `monlist` 走 HTML 對話窗（M7-R 的延伸） |
| 城堡 | S69 王冠表 → 頭上 12 格動畫王冠；S185／S141／S4 都開 NPC 對話窗的 `tax`／`withdraw`／`deposit` 頁 | 欄位完全一致 | **可以**（M7-C 補畫面：王冠與 HTML 頁） |
| 盟屋 | S156 → XML 版 `AgitAuctionUI`（本機缺）；S187 → 舊式 `AgitMapWindow` | 欄位完全一致 | **可以**：地圖窗；拍賣清單窗缺資源，先用 HTML／清單 |
| 數量輸入 | S136 不開 `InputWindow`，而是 NPC 對話窗的 HTML 頁加數字框；`InputWindow` 是聊天輸入列的提示與 YES／NO 鈕 | S136 | **可以**（M7-E 改用 HTML 頁） |
| 信件、釣魚、重置 | 本機 3.81 資源缺檔；**本機其他版本客戶端有**（第 8 節），事件名與 TW16 相符 | 有 | 版面可取得；部分 surf 本機缺 |

## 2. 3.81 世代用哪套視窗：背包、魔法、倉庫

### 2.1 已驗證的事實

1. **XML 視窗是「讀到 XML 根元素才建立」。** `sub_B56510`（VA `0xB56510`／FO `0x755910`）是 XML 讀取的元素回呼；物件還沒建立時用根元素名稱呼叫 `sub_B56A40`。`Inventory`、`Warehouse`、`SpellUI` 等名稱字串在 BIN 中只被 `sub_B56A40` 自己引用（`win_strings.py`），所以要有 `ReInventory.xml`（根 `<Inventory>`）、`rewarehouse.xml`（根 `<Warehouse>`）才會產生物件。
2. **熱鍵：** `sub_8C14F0`（VA `0x8C14F0`／FO `0x4C08F0`，鍵盤字元分派）
   - 字元碼 9：`RenewalInventoryUI`（`dword_14EBEC0`）vtable `+76`（切換顯示）。
   - 字元碼 19：`sub_A59AF0(SpellUI)`（`dword_1542440`）。
   - 字元碼 1：`RenewalStatusUI`。
   - 這是 WM_CHAR 值；對應實體按鍵（例如 Tab／Ctrl+I、Ctrl+S）**推測**，未驗證。另一份熱鍵函式 `sub_8C2290`（FO `0x4C1690`）同樣只碰 `dword_14EBEC0`。
3. **舊式背包只剩寵物用：**
   - `initGame`（`sub_8C7C80`）建立 `dword_13CDF64 = sub_88C1D0(19, dword_13CDF60)`。
   - `sub_88C1D0`（FO `0x48B5D0`）寫的是 `PetInventoryWindow` 的 vtable（RTTI `.?AUPetInventoryWindow@@`），內部呼叫 `InventoryWindow` 基底建構子 `sub_88B060`（FO `0x48A460`）。
   - `sub_88B060` 內有字串「`uimagicitem.xml resource is not updated.`」（VA `0x117DD80`），表示它讀 `uimagicitem.xml`。
   - `S_RETRIEVE_LIST` 類型 11（寵物清單，`dchcdcs`）才把資料寫進 `dword_13CDF60`（`sub_949930` 第 139–180 行）。
   - `.?AVInvWindow@@` 是 XML 版背包的內部視窗（建構子 `sub_9A3BE0` 由 `sub_9AA0B0` 呼叫，位在 `RenewalInventoryUI` 的程式區），**不是**舊式背包。
4. **舊式魔法窗仍存在：**
   - `SpellWindow`（RTTI `.?AUSpellWindow@@`）由 `initGame` 建立：`dword_1541DF8 = sub_DFC1E0(20, …)`，FO `0x9FB5E0`。
   - 視窗堆疊 `sub_A57C60`（FO `0x657060`）在 `*dword_138BE60 == 509` 時拒絕推入 `SpellWindow`。
   - 引用者還有 `sub_E01D60`、`sub_E02BC0`、`sub_E05330`、`sub_E24340`（SpellUI 程式區附近）。熱鍵走的是 `SpellUI`；`SpellWindow` 何時開啟**待驗證**。
5. **倉庫一律 XML 版：** `S_RETRIEVE_LIST`（op 127，`sub_949930`／FO `0x548D30`）讀 `dhc`（物件、筆數、類型）。
   - 類型 3／20 → 子類型 1，上限 150；類型 5 → 子類型 2，上限 200；類型 9 → 子類型 6，上限 100；類型 18 → 子類型 3。
   - 類型 21／25 另一分支；類型 4、12 另有處理。
   - 最後一律以 `RenewalWareHouseUI`（`dword_14EBF48`）vtable `+72`／`+76` 開窗。寵物倉庫 `dword_14EBEEC`、附加倉庫 `dword_14EBE4C` 由同一函式在其他類型開啟。
   - 3.81 只送類型 3（個人）、5（血盟）、9（妖精）、18（角色），SP `S_RetrieveList` 等；**全部落在 TW16 的 XML 分支**。
6. **本機 3.81 資源有舊式設定：**
   - `uimagicitem.xml`（BIN XML 表第 8 項，VA `0x137B3BC`）含 `SPELL_WINDOW`、`ITEM_WINDOW`、`ADENA_ITEM_WINDOW`、`PET_ITEM_WINDOW`，由 `sub_A85FD0`（FO `0x6853D0`）讀取（`SPELL_WINDOW` 在 `0xA85FFD`、`ITEM_WINDOW` 在 `0xA86107`）。
   - `uiextra86.xml` 含 `WAREHOUSE_WINDOW`、`ADENA_WAREHOUSE_WINDOW`，由 `sub_A842D0` 讀取（`0xA84D04`、`0xA84C79`）。
   - TW16 讀這兩段設定的消費類別：`ITEM_WINDOW` → `InventoryWindow`（推測，依上一點的字串）；`WAREHOUSE_WINDOW` 的消費者**待驗證**。

### 2.2 結論

- **TW16 本身用 XML 版。** 背包、魔法、倉庫都開 `RenewalInventoryUI`、`SpellUI`、`RenewalWareHouseUI`。
- **3.81 世代（本機 `LiTo.bin`＋本機資源）只能是舊式。** 本機 `Tile.pak` 沒有 `ReInventory.xml`、`rewarehouse.xml`，TW16 的 XML 版在這套資源下根本建不起來（WIN§0.1：`sub_B3D390` 缺檔時跳錯誤框、沒有退回）。本機有的是 `uimagicitem.xml` 與 `uiextra86.xml` 的舊式設定。這是**推測**：`LiTo.bin` 有殼，沒有直接驗證。
- **引擎的版面來源：** 依本機資源用舊式設定（surf 都在本機 `Sprite*.idx`，`t226_surfcheck.py`）。

| 視窗 | 設定段（本機） | 版面（已驗證） | 本機缺的 surf |
|---|---|---|---|
| 背包 | `uimagicitem.xml` `ITEM_WINDOW` | 底 2046 (6, 8)、框 2047、關閉 2037 (179, 15)；捲軸 2039 (171, 58)，上 2042 (171, 44)、下 2040 (171, 225)、拉桿 2044；4 個分頁 2048–2051 (27, 235)；垃圾桶 2052 (142, 260)、寶箱 2054 (117, 260) | 無 |
| 金幣背包 | `ADENA_ITEM_WINDOW` | 底 4250、框 4251，捲軸 (166, 46)、下鈕 y=270，垃圾桶／寶箱 y=276 | 4250、4251、4254、4255、4257、4259（整組缺） |
| 魔法 | `SPELL_WINDOW` | 底 2056 (6, 8)、框 2057、關閉 2037 (179, 15)；捲軸同背包；等級鈕在 (12, 37)：王族 2058–2064（7 級）、一般 2065–2074（10 級）、妖精 2075–2085（11 級）、黑妖 2086 起 | 無 |
| 寵物背包 | `PET_ITEM_WINDOW` | 底 2034 (6, 8)、框 2035、分頁圖 2036 (27, 238) | 無 |
| 倉庫 | `uiextra86.xml` `WAREHOUSE_WINDOW` | 底 3001 (6, 9)、框 3002、離開 3031 (225, 15)；捲軸 3024 (217, 61)，上 3025 (217, 47)、下 3027 (217, 285)；總計 3007 (19, 25)；名稱 3008 (117, 35)、數量 3009 (162, 35)，排序鈕 3010／3012 在 (150, 37)、(206, 37)；分頁 3020–3023 (17, 300)；確定 3014 (60, 326)、取消 3017 (127, 326) | 無 |
| 金幣倉庫 | `ADENA_WAREHOUSE_WINDOW` | 同上，底 4476、框 4477，確定／取消在 y=345，另有 4426／4423 (15, 322) | 4423、4426、4476、4477 |

- **引擎現況：** `InventoryUi.cs`、`MagicUi.cs`、`NpcUi.cs`（倉庫清單）都是 WinForms，沒有上述版面。
- **建議 code 任務：**
  - 原生背包、魔法、倉庫三窗。依賴 T190（C2 畫布）。檔案：新檔 `NativeUi/ItemWindowPanel.cs`、`NativeUi/SpellWindowPanel.cs`、`NativeUi/WarehouseWindowPanel.cs`，資料層沿用 `Inventory.cs`、`Magic.cs`、`NpcServices.cs`（只讀）。
  - 金幣背包與金幣倉庫因 surf 缺，不做。

## 3. 血盟窗

**原版怎麼做（已驗證）：**
- **讀的檔：** `PledgeUI`（建構子 `sub_B2A0C0`，FO `0x7294C0`）讀的是 BIN XML 表第 21 項 **`RePledgeUI.xml`**（VA `0x137B3EC` → 字串 `0x119C98C`），**不是**本機的 `PledgeUI.xml`。`PledgeFocusUI` 在 BIN 中只出現在 HTML 動作類別名（`HyperActPledgeFocus`、`HyperActMyPledgeFocus`），**沒有任何 XML 讀取**，`PledgeFocusUI.xml` 的事件 `FocusName`、`FocusScroll`、`FocusWindow` 在 BIN 中都不存在。
- **事件比對：** 建構子註冊 32 個事件（`ModifyMemo`、`ChangeRank`、`BanPledgeMember`、`AcceptJoinRequest` 等）。
  - 本機 `PledgeUI.xml`（3 個視窗：`ChangeRank_PopupWindow`、`Noticeuserinfo_Window`（底 4512、框 4487、關閉 4429 (260, 13)、公告框 (21, 63) 限 160 字 9 行）、`Nonoticeuserinfo_Window`；共 48 個 surf）的 12 個事件只有 3 個（`ChangeRank`、`ModifyMemo`、`OpenPopup`）在建構子註冊。另外 9 個事件名在 BIN 中仍是字串，但沒有在建構子註冊。
  - 本機其他客戶端的 `RePledgeUI.xml`（第 8 節）命中 29／32。
- **開窗封包（S_EVENT op 108 子碼，`sub_9357E0` 第 3609–3622 行）：**

| 子碼 | TW16 函式 | TW16 讀的格式 | 3.81 送的格式（SP／原始碼） | 一致？ |
|---|---|---|---|---|
| 167 公告 | `sub_B2E510`（「RecvPledgeInfo」） | `ssdcccdb` | `S_Pledge(int)` 第一個 byte 直接寫 167（`S_OPCODE_PACKETBOX` 被註解掉），之後 `S S D D` 加公告位元組 | ❌（3.81 不是正常 S250 包） |
| 168 公告解除 | `sub_B2EF40`（「RecvPledgeNoti」） | `b` | `S(name)` | ❓ |
| 169 成員備註 | `sub_B2EE50`（「RecvPledgeMemberMemo」） | `sb`（名稱 13＋備註） | `S_Pledge(String,String)`：S250、169、`S(name)`＋位元組 | ✅ |
| 170 成員清單 | `sub_B2E7C0`（「RecvPledgeMemberInfo」） | `ccc` 後逐筆 `sub_B2CFC0` | `S_Pledge(L1Clan)` 第一個 byte 直接寫 170，`H(1) C(人數)`＋逐筆 | ❌（同 167） |
| 171 上線成員 | `sub_B2E3D0` | 人數＋逐筆名稱，然後 vtable `+68(1)` **開窗** | S250、171、`H(人數)`＋`S(name) C(0)` | ✅（開窗點） |

- `S_PLEDGE`（op 110，`sub_946770`）是 `dssd` 名稱變更通知，只在特定字串時關窗，不開窗。

**3.81 伺服器是否支援：**
- `C_Pledge.java` 第 42–44 行連送 `S_Pledge(clanId)`、`S_Pledge(clan)` 與 S250／171。
- 前兩包的第一個 byte 是 167／170，3.81 的 S opcode 表沒有這兩個值。這與 [`protocol-gaps.md`](protocol-gaps.md) 第 2.3 節「167、170 伺服器不會送（作為 S250 子碼）」一致。
- 實際能用的只有 171（上線成員）與 169（`C_PledgeContent` 寫備註後回送）。

**引擎現況：** `Clan.cs` 接 171（protocol-gaps 第 2.3 節）；`ClanUi.cs` 是 WinForms。

**建議 code 任務：**
- **血盟成員窗：** 171 → 開窗並列出上線成員；169 → 更新備註。版面以本機 `PledgeUI.xml` 的 `Noticeuserinfo_Window`／`Nonoticeuserinfo_Window` 為準（3.81 世代資源，48 個 surf 全在本機）。
- **依賴：** T190；`ClanUi.cs` 保留給 T168（gap-matrix 第 7 節），所以用新檔 `NativeUi/PledgePanel.cs`，資料讀 `Clan.cs`（只讀）。
- **不做：** 167、170 的格式在 3.81 是壞的，不做；`PledgeFocusUI` 不做。

## 4. 變身選擇窗

**原版怎麼做（已驗證）：**
- **S39 `monlist`：** S_HYPERTEXT（op 144，`sub_940030`）→ `sub_BBCB00` → NPC 對話窗（`dword_13CD834`）。
  - 顯示頁時 `sub_874A10`（FO `0x473E10`）判斷 `strncmp(id, "monlist", 7) == 0 && strlen(id) > 7` 才走特別模式（`sub_867530(id, 1)`）；**id 剛好是 `monlist`（3.81 的 `S_ShowPolyList`）時走一般 HTML**。
  - `sub_867530` 讀 `"%s-%c.html"`（語系字元），也就是 `monlist-c.html`。
- **道具使用（本機流程）：** `sub_503730`（FO `0x102B30`）在使用變身卷軸時：
  - 特定卷軸（字串 7539、7745、8866–8869、12958）開 HTML 頁 `archmonlist`、`evblacklist`、`evsilverlist`、`evgoldlist`、`evarchlist`、`magicp`。
  - 其他卷軸看選項旗標 `byte_135C1EA`：為 1 時開 **`CPolymorphUI`**（`dword_1540854` vtable `+68(1)`）；為 0 時用訊息 180 開通用確認框 `sub_AC98E0`。
- **S_ASK（op 85，`sub_92EAD0`）的 180／289：** 旗標為 0 → 通用 Yes/No；為 1 → 開 `CPolymorphUI`。
- **旗標來源：** `byte_135C1EA` 只在選項頁的切換函式 `sub_583260`（FO `0x182660`）被反轉，之後重畫 HTML 頁 `optiong`；設定檔存檔函式 `sub_814E10` 以第 16 項寫入。**預設值待驗證**（初始化在 `0xF53C87`，不在函式內）。
- **`CPolymorphUI`（建構子 `sub_BF8C00`）：** 讀本機有的 `PolymorphUI.xml`＋`polymorphList.xml`。
  - 開窗 `sub_BF8F80` 找 `Polymorph_Window`，套 surf 4893。
  - 依角色等級把清單項目設為可用／不可用（`sub_BFAB10`：等級 < 需求就停用，`pc` 項目看 `+1178` 旗標）。
  - XML 的 10 個事件全在 BIN。
- **`CSpecialPolymorphUI`（建構子 `sub_A09770`，讀 `SPolymorphUI.xml`＋`SPolymorphList.xml`）：**
  - 確定鈕 `sub_A0A460` 送 **TW16 opcode 208**，`sub_9FBC00(fmt, 208, 道具物件 id, 名稱)`。opcode 208 就是 gap-matrix 的 `C_USE_ITEM`，即「用道具並附變身名稱」。
  - 開啟點只在 S_EVENT（op 108）中，子碼未逐一確認（待驗證）。
- `Polymorphlist2.xml` 在 BIN 中沒有引用（XML 表與字串都沒有）。

**3.81 伺服器是否支援：**
- `S_ShowPolyList` 只送 S39 `monlist`（SP）。
- 伺服器不送 S_ASK 180／289 來開變身窗。
- 本機流程用的 opcode 208 對 3.81 是 C_ItemUSe，欄位是否接受名稱字串**待驗證**。

**引擎現況：**
- S39 只在 `npc == InteractionNpc` 時接受（`NpcInteraction.cs:78`）。`S_ShowPolyList` 的物件 id 是玩家自己，不是對話中的 NPC，所以**目前會被丟掉**（推測，依程式碼）。
- `monlist` 在 r29 `dialogs/templates.json` 有 152 處出現。

**建議 code 任務：**
- 接受伺服器主動送來、物件 id 是自己的 S39 頁（`monlist`、`pledge` 等），用 NPC 對話面板顯示，點選回 C_NPCAction（與 NPC 頁同一路徑）。檔案：`NpcInteraction.cs`、`NpcUi.cs`。
- 這和 M7-R 改的是同一組檔案，建議併入 M7-R 的後續任務。
- `CPolymorphUI` 是選項功能，不做。

## 5. 城堡、盟屋、數量輸入（M7-C、D、E 的畫面）

### 5.1 城堡（M7-C）

| 封包 | TW16 處理（已驗證） | 3.81 | 一致？ |
|---|---|---|---|
| S69 CastleMaster（TW16 op 252，`sub_9322C0`） | `cd`：城堡編號 < 9 時寫入 `dword_154434C[城堡] = 物件 id`（城主表） | `C(type) D(objid)` | ✅ |
| S185 TaxRate（op 95，`sub_94D8E0`） | `dccc` 寫進 NPC 對話窗欄位（模式 1），以 HTML 頁 **`tax`** 開啟（`.rdata` VA `0x1183F78`／FO `0xD82B78`） | `D(objid) C(10) C(50)`（SP 只有兩個 C） | ⚠️（TW16 多讀一個 C；最後一欄的意義待驗證） |
| S141 Drawal（op 12，`sub_94FF20`） | `dd`，模式 2，NPC 名稱＋「`%s : %d %s`」（字串 457、4），HTML 頁 **`withdraw`** | `D(objid) D(金額)` | ✅ |
| S4 Deposit（op 49，`sub_933F50`） | `d`，模式 3，顯示目前持有金額，HTML 頁 **`deposit`** | `D(objid)` | ✅ |

- **王冠（已驗證）：** 繪製函式 `sub_F08880`（FO `0xB07C80`）每格逐一比對畫面上的人物。
  - 物件 id 等於城主表第 1、3、4、5、6、7、8 格時畫王冠；**第 2 格（不比對）沒有王冠**。
  - 位置：人物錨點 x＋17；y − 高度 − 12，高度一般是 40，`sub_501260` 成立的外型是 50。再依外型類別（12／8／6／其他）加上方向與動作的偏移表（`dword_138CCC8`、`dword_138D560`、`dword_138D158`、`dword_138CA18`）。
  - 動畫：12 格，每格 500 ms（`0x1F4`），每座城各自計時。
  - 只畫自己，或是條件 `sub_A42C30` 不成立且 y > 0 的人物。
  - 王冠圖源 `dword_15451B8` 的載入點**待驗證**。
- **引擎現況：** 沒有 `Castle.cs`；S69／S185／S141／S4 都沒接（gap-matrix 第 1 節）。`templates.json` 有 `tax`、`withdraw`、`deposit` 頁。
- **建議 code 任務（M7-C 擴充）：**
  - S185／S141／S4 改為開 NPC 對話面板的 `tax`／`withdraw`／`deposit` 頁。
  - S69 存城主表，人物頭上畫王冠（12 格、500 ms）。
  - 檔案：新檔 `Castle.cs`、`CastleTests.cs`；畫王冠需要在 `ObjectRendering.cs` 加掛點，該檔保留給 T168，掛點等 T168 合併後再加。
  - 王冠圖號待查，未取得前只做資料。

### 5.2 盟屋（M7-D）

- **S156 HouseList（op 0，`sub_92E550`）：** `dh`＋逐筆 `dshccd`（盟屋 id、名稱、面積、月、日、價格），和 3.81 `S_AuctionBoard` **完全一致**。資料放進 `CAgitAuctionUI`（`dword_13AA1E8`，XML `AgitAuctionUI`）並開窗。這個 XML 在 BIN 的 XML 表裡沒有，本機與其他客戶端也都找不到（第 8 節）。
- **S187 HouseMap（op 18，`sub_92E710`）：** `dd` → `sub_5433D0(this, 物件, 區域, 編號)`，再以視窗堆疊開舊式 **`AgitMapWindow`**（`dword_13A96F0`，RTTI `.?AUAgitMapWindow@@`，建構子 `sub_5419E0`）。
  - 第二個 D 被拆成「區域＝高 16 位元（< 9）」與「編號＝低 16 位元（1–70）」。3.81 `S_HouseMap` 送 `D(house_number)`（盟屋 id，例如 `0x40001`），和這個拆法相符（推測，依變數配置）。
  - 繪製 `sub_542640`：
    - 區域 1 的地圖 surf 來自 `dword_135DA34[4]`，其他區域來自 `aAvadentelui` 表（地圖圖號表，內容待查）。
    - 標記 surf 176；區域 7 用 34，位置表 `dword_13A96F0[…+560]`。
    - 右側兩個鈕：surf 160 (312, 56)、164 (312, 110)。
  - `AgitWindow`（`dword_13A96EC`，建構子 `sub_541B80`）只在 `initGame` 建立，第一層沒有收包函式引用（待驗證）。
- **S136 `agapply`：** 見 5.3，會轉給 `CAgitAuctionUI`（`sub_546800`）。
- **引擎現況：** 沒有。
- **建議 code 任務（M7-D 擴充）：**
  - S187 → 盟屋地圖窗（地圖 surf＋標記 176／34＋兩個鈕）。依賴 T190；新檔 `NativeUi/AgitMapPanel.cs`。
  - S156 拍賣清單先用 WinForms 清單或 HTML 頁，因為缺 XML。

### 5.3 數量輸入（M7-E）與 `InputWindow`

- **S136 InputAmount（op 224，`sub_93FDF0`／FO `0x53F1F0`）：** 不開 `InputWindow`。
  - 欄位直接寫進 NPC 對話窗（`dword_13CD834+76` 起），模式 0。
  - 第一個字串（HTML id）為空時關窗；否則讀附加字串清單，以 vtable `+100` 顯示該 HTML 頁，**頁內帶數字輸入框**。
  - id 是 `agapply` 時改交 `CAgitAuctionUI`（`sub_546800`）。
  - 格式字串 `dddddcsssh`：3.81 是 `D D D D D H S S [H S…]`，前 5 個 D 對得上；後段的 `c`／`s` 怎麼對應 3.81 的 `H(0)` 與兩個字串，**待驗證**（Hex-Rays 的變數配置看不出）。
- **`InputWindow`（建構子 `sub_A57590`／FO `0x656990`，`initGame` 建 0、1 兩個）是聊天輸入列的提示：**
  - 第 1 號有文字鈕「YES」在 (361, 567)、「NO」在 (401, 567)，畫面座標，在 800×600 的聊天輸入列上。
  - 使用者是聊天輸入的 `sub_889380`／`sub_8895B0`（FO `0x488780`／`0x4889B0`）：有提示字串時把 `dword_153D238` 指向 0 或 1 號並顯示，結束時隱藏。
  - 這正是 WIN§2.1、WO 第 6 節「Yes/No 走聊天輸入列」的畫面。
- **引擎現況：** S136 沒接；`GroundUi.cs`、`NpcUi.cs` 各有自己的數量框；Yes/No 各功能自做按鈕。
- **建議 code 任務：**
  - **M7-E 改寫：** S136 → NPC 對話面板顯示 HTML 頁加一個數字框（min／max 照封包），送出回 C11。新檔 `AmountPrompt.cs` 不必另做 Form。
  - **M7-J 補畫面：** 聊天輸入列提示加 YES (361, 567)／NO (401, 567) 兩個文字鈕。

## 6. 其他 XML 窗：觸發與 3.81 支援

方法：把各視窗全域指標的引用函式（IDA xref）對到 gap-matrix 第 1 節的收包處理函式表（`t226_triggers.py`）。「本機觸發」表示第一層引用者沒有收包函式，推測由按鍵、選單或道具開啟。

| 視窗（全域） | 觸發（已驗證的第一層） | 3.81 伺服器 | 建議 |
|---|---|---|---|
| `BookmarkMemoryWindowUI`（`dword_1540824`） | S_EVENT op 108 | 書籤擴充（S250 141 `BOOKMARK_EXPAND`）推測 | 併 T198 |
| `StatInfoUI`（`dword_15433B4`） | op 43（重置） | S64 CharReset | 等重置窗資源（第 8 節） |
| `UserObtainUI`（`dword_154085C`） | op 30／216／98／164（背包新增、移除、更新） | 有對應封包，但原版是本機提示 | 低優先；本機 XML 有 |
| `QuickMotionUI`、`OptionUI`、`PopUpUI`、`PopupMsgUI`、`LinHelperUI`、`StoryBookUI`、`QuestUI`、`SiegeUI` | 本機觸發 | 不需封包（`QuestUI`、`SiegeUI` 是 TW16 新內容） | 不做（`OptionUI` 另見 gap-matrix） |
| `FightPlaceUI`（`dword_13CD1A4`） | S_EVENT 子碼 163、164（`hccccdsccsddsccsddcc`、`scsddddd`） | protocol-gaps：163、164 伺服器不會送 | 不做 |
| `SmithUI`（`dword_1540850`） | op 43、op 102（S_MESSAGE_CODE） | 無對應 | 不做 |
| `ZoneNameNotice`（`dword_16568A4`） | 只有 `sub_B56A40` 引用；推測由自身事件委派 `EventHandlerDelegator<CZoneNameNotice>` 觸發 | 不需封包 | 待驗證 |
| `PetSummonUI`（`dword_13AAD9C`） | op 141、89、1、43 | 3.81 寵物召喚走 S39 `summonlist`（WIN§1.3） | 不做；召喚用 `CSummonUI`（M7-W） |
| `RenewalMailUI`（`dword_14EBED8`） | op 99、89、108 | S186 Mail | 見第 8 節 |
| `FishingUI`（`dword_1542870`） | 本機觸發；S_EVENT 子碼 55 只顯示訊息 3885／3886／4450 | S250 55 `FISHING` | 見第 8 節 |
| `ResetStatUI`（`dword_14EBF4C`） | op 43 | S64 | 見第 8 節 |

## 7. 其他舊式窗

| 視窗 | 原版（已驗證） | 3.81 | 引擎 | 建議 |
|---|---|---|---|---|
| `CCookWindow`（`dword_153F25C`，建構子 `sub_A5AAC0`） | S_EVENT 子碼 52：`d`＋`c`＋`c`；第二欄為 1 時開／切換料理窗，第三欄＋1 是料理書等級 | `S_PacketBoxCooking`：`C(52) C(0xdb) C(0x31) C(0xdf) C(0x02) C(1) C(value)`，前 4 byte 正好是 TW16 的 D | `InventoryUi.cs`（料理書使用）；S250 52 沒接 | **可開**：接 S250 52 開料理窗；版面用 `uiextra86.xml` `COOK_BOOK_WINDOW`（底 2254、捲軸 2039、等級圖 2545–2555 (116, 257)、量表 2256 (35, 251)） |
| 血盟倉庫紀錄（`dword_153D218`，`sub_A559D0` 建立，vtable `off_11901C4`） | S_EVENT 子碼 117：`D(筆數)`＋逐筆 `scsdd`；筆數 0 時顯示訊息 1889，否則開窗 | `S_PledgeWarehouseHistory`：`D(n)`＋逐筆 `S C S D D`，**完全一致** | 沒接 | **可開**：接 S250 117；視窗用 `sub_A559D0` 的元件（按鈕 surf 9183，等原生 UI），先做資料與清單 |
| `TelTablesWindow`（3 個，`dword_153CD88`／`90`／`98`） | S_EVENT 子碼 38、45：「`TotalUsers=%d`」與「`%s (%s),lv=%d,m=%d`」使用者清單 | 3.81 的 38 是 `HTML_CLAN1`，**不是**這個 | `Teleport.cs` 無關 | 不做（GM 工具，推測） |
| `PKWindow`（`dword_1543334`，建構子 `sub_ECD480`） | 第一層沒有收包函式 | 無 | 無 | 不做 |
| `PetInventoryWindow`（`dword_13CDF64`） | `S_RETRIEVE_LIST` 類型 11 | 3.81 不送類型 11 | `Pets.cs` | 不做 |
| `AgitWindow`、`AgitMapWindow` | 第 5.2 節 | 第 5.2 節 | 無 | 第 5.2 節 |
| `QuitWindow`、`CNoticeWindow`、`WareHistoryWindow` | RTTI 的 vtable 在 `.rdata` 沒有程式引用（`ui_ida_query` `rtti`：users 空） | — | — | 版面另見 `uiextra86.xml` 的 `QUIT_WINDOW`（底 1951、鈕 1953／1956／1959）、`NOTICE_WINDOW`（3716 (618, 442)）；類別使用點待驗證 |
| 看板／提示類（b8） | `FontWindow` 建構子 `sub_83D7A0` 由熱鍵 `sub_8C14F0`／`sub_8C2290`、`sub_8C5380` 與血盟窗 `InputNickName`（`sub_B2D160`）建立。`SMSWindow`（`sub_8B1D30`）、`SMSPledgeWindow`（`sub_8B19C0`）、`DragonGateWindow`（`sub_A79CA0`）、`LevelUpHelpWindow`（`sub_A807A0`）、`GameRankShortCutWindow`（`sub_ECD060`）只在 `initGame` 建立，觸發未追。`SignWindow`、`TipWindow`、`GiftMessageWindow` 的 vtable 沒有程式引用 | 簡訊、排名捷徑沒有對應；其餘無已知封包 | 無 | 不做（TW16 新功能或沒有觸發點） |

## 8. 缺檔 XML：本機其他版本客戶端

`idx_find.py` 搜尋本機客戶端的 `Tile.idx`（只讀）：

- **沒有缺檔 XML 的客戶端：** `D:\lineage開發\0-客戶端`、`0-客戶端-公版260707修正門跟icon`、`D:\380` 下 4 套、凝時、古典經典版、韓版，以及 `K23`／`K25` 多開下 5 套。
  - `K25` 的「小宇宙」「犬夜」只有 `ResetStatUI.xml`。
  - `經典版UI資源` 的 15 個 `lineage*.xml` 是第三方經典 UI 替代檔，沒有信件、釣魚、重置。
- **有缺檔 XML 的客戶端：**

| 代號 | 客戶端 | `Tile.idx`／`Tile.pak` SHA-256 前 16 碼 | 解出 XML 數 |
|---|---|---|---|
| kor25 | `D:\381懶人包kor25(原)` | `e205c88fa977f711`／`45aa5ddf8c0ef648` | 196 |
| pmc | `D:\381懶人包泡麵陳(原)` | `db18fc5e4e0e6fd1`／`c26996d842fcee58` | 196 |
| c760 | `D:\760測試\超人760客戶端` | `f680a36ac8e44f5d`／`329cb79b679d3436` | 153 |
| t89 | `D:\天堂參考用客戶單\89天堂` | `b02a06831f0a1bb9`／`8cb8e8a7594f8f04` | 641 |
| ly | `D:\天堂參考用客戶單\雷雨天堂` | `5a2e2349b83c0d34`／`5ee159aa948b8a28` | 841 |

- 比對方法：
  - XML 的事件名在 TW16 BIN 中是否存在（`t226_evbin.py`）。
  - 建構子有註冊事件的類別，比對 XML 是否用到（`t226_evcmp.py`）。
  - XML 引用的 surf 在本機 `Sprite*.idx` 有沒有 `.img`（`t226_surfcheck.py`）。

| XML（kor25 版 SHA 前 12 碼） | 事件在 BIN | 建構子事件命中 | 本機缺 surf | 版面摘要 |
|---|---|---|---|---|
| `RenewalMailUI.xml`（`e6a0e037ca15`，kor25／pmc／c760／t89 相同） | 22／23（缺 `MailListScrollButton_Mouse`） | 建構子不在本函式註冊 | 2（9183、9184） | 三窗都是 285×413：`MailList_Window`（3 個分頁字串 4071–4073、寫信／保存／刪除／封鎖鈕、清單 (26, 96) 210×286）、`ReadMail_Window`、`WriteMail_Window`（收件人與主旨框 (105, 79)／(105, 98)，主旨限 20 字，內文 (38, 128) 207×245 限 256 字，血盟信件勾選 (156, 391)） |
| `ResetStatUI.xml`（`af94ac5257a2`，全部相同） | 8／8 | 同上 | 23（7156、8670–8688 等；kor25 也缺 17） | `ResetStat_Win` 800×600 全畫面；六項能力各一組 −／＋鈕（6504／6506）在 (520／628, 357–397)；`Message_Window` (171, 153) 確定 6489／取消 6492 |
| `FishingUI.xml`（`205b76bb7c18`，全部相同） | 2／2 | 2／2 | 0，但 8 個全是 `.tbt`、沒有 `.img` | `Fishing_Window` 底 6102、`FishingNet` 6103 (160, 0)、`QuitFishing` 5902 (188, 2) |
| `FishObtainUI.xml`（`01d94c4464e3`，全部相同） | 4／4 | 3／4 | 0 | 底 5194、框 5195、關閉 4429 (225, 15)、清單 (18, 37) 194×250，每列 35 高 |
| `RePledgeUI.xml`（`363fd9f2230e`，全部相同） | 39／39 | 29／32 | 19 | （第 3 節，因 3.81 只送 169／171，不建議用） |
| `ReInventory.xml`（`52ca2676edd3`） | 4／6 | 3／6 | 6 | 不建議用（第 2 節改用舊式） |
| `rewarehouse.xml`（`f7c9d6c60a73`，全部相同） | 7／7 | 6／9 | 4 | 不建議用 |
| `LoginUI`／`SelectUI`／`CreateUI.xml` | 13／13、15／15、15／15 | — | 44／15／25 | 登入畫面另案（gap-matrix 第 8 節「登入流程畫面」） |

**判斷：**
- 這幾份 XML 與 TW16 是同一套事件表，可當 TW16 版面依據。這是推測：「版本相同」只依事件名推斷，沒有版本號可比。
- 信件與釣魚取得視窗的版面已可取得；信件缺的 2 個 surf 是關閉鈕。
- 重置窗缺大量 surf（能力數字圖），**只能部分重現**。
- 釣魚窗的圖只有 `.tbt`，解碼與顯示方式**待驗證**。

**未找到：** `AgitAuctionUI`（BIN XML 表沒有它的檔名）、`SpellUI` 的視窗 XML（`SpellList.xml` 根是 `<spells>`，是資料表，不是版面；`SpellUI` 的視窗來源待驗證）。

**禁止事項的處理：** 上述 XML 只解到 `scratch/rea/uixml_alt/`，不進 git。若 code 任務要用，需由使用者決定是否把這些客戶端的 XML 或 surf 納入引擎資源。這屬於資源來源決策。

## 9. 建議的 code 任務

| 建議 | 目標 | 檔案 | 依賴 | 依據 |
|---|---|---|---|---|
| W2-A 伺服器主動 S39 頁 | 接受物件 id 是自己的 S39（`monlist`、`pledge` 等），用 NPC 對話面板顯示，點選走 C_NPCAction | `NpcInteraction.cs`、`NpcUi.cs`（與 M7-R 同檔，排在 M7-R 之後） | M7-R | 第 4 節 |
| W2-B 城堡 HTML 頁與王冠 | S185／S141／S4 → `tax`／`withdraw`／`deposit` 頁；S69 城主表；王冠先做資料 | 新檔 `Castle.cs`、`CastleTests.cs`（M7-C 擴充） | M7-A | 第 5.1 節 |
| W2-C 盟屋地圖窗 | S187 → 原生地圖窗（區域地圖、標記、兩鈕） | `Agit.cs`（M7-D）＋新檔 `NativeUi/AgitMapPanel.cs` | M7-D、T190 | 第 5.2 節 |
| W2-D 數量輸入改 HTML | S136 → NPC 對話面板 HTML＋數字框，回 C11 | M7-E 的新檔（`AmountPrompt.cs`、`AmountPromptTests.cs`），不另做 Form | M7-A | 第 5.3 節 |
| W2-E 聊天輸入列 YES／NO 鈕 | Yes/No 提示加 YES (361, 567)／NO (401, 567) | M7-J 的 `YesNoPrompt.cs` | M7-J | 第 5.3 節 |
| W2-F 料理窗 | 接 S250 52，開料理窗（`COOK_BOOK_WINDOW`） | 新檔 `Cooking.cs`、`CookingTests.cs`；畫面另等 T190 | M7-A | 第 7 節 |
| W2-G 血盟倉庫紀錄 | 接 S250 117，`scsdd` 清單；0 筆顯示訊息 1889 | 新檔 `ClanWarehouseHistory.cs`、測試 | M7-A | 第 7 節 |
| W2-H 血盟成員窗 | 171 開窗、169 更新備註；版面用本機 `PledgeUI.xml` | 新檔 `NativeUi/PledgePanel.cs` | T190、T168 | 第 3 節 |
| W2-I 舊式背包／魔法／倉庫原生窗 | `uimagicitem.xml` `ITEM_WINDOW`／`SPELL_WINDOW`、`uiextra86.xml` `WAREHOUSE_WINDOW` | 新檔 `NativeUi/ItemWindowPanel.cs`、`SpellWindowPanel.cs`、`WarehouseWindowPanel.cs` | T190 | 第 2 節 |

**需要使用者決定（不必等，但未決前不做畫面）：** 信件、釣魚、重置窗是否採用其他本機客戶端的 XML 與 surf（第 8 節）。

## 10. 未驗證與限制

- 全部是靜態分析；`LiTo.bin`（3.81 實際客戶端）有殼，沒查。「3.81 世代用舊式窗」是依本機資源推定。
- 熱鍵字元碼對應的實體按鍵、`SpellWindow` 何時開啟、`WAREHOUSE_WINDOW` 設定段的消費類別，都待驗證。
- S136 與 S185 的欄位在 TW16 與 3.81 之間有一兩欄對不上；code 任務以 3.81（SP）為準。
- `byte_135C1EA`（新式變身窗選項）的預設值待驗證。
- 王冠圖源、`aAvadentelui` 地圖圖號表的內容、`CSpecialPolymorphUI` 的開啟子碼，都待驗證。
- 第 6 節的觸發只看第一層引用者；間接呼叫（例如經由 vtable）可能漏掉。
- 鬆檔搜尋（`D:\lineage開發` 下各資料夾的 `*.xml` 檔名）在背景執行 10 分鐘逾時被停止、沒有結果；改以 `Tile.idx` 搜尋取代，結果見第 8 節。
- 本機 `Text.pak` 的檔名有加密，`monlist-c.html` 等頁是否在本機 `Text.pak` 沒有確認；引擎用的是 r29 `dialogs/templates.json`。

## 11. 腳本

| 腳本 | 用途 |
|---|---|
| `rea-scripts/t226/queries.json` | 本輪 IDA 查詢 b1–b7 的內容（`ida_dec_xref.py`、`ui_ida_query.py`） |
| `rea-scripts/t226/t226_cases.py` | 依頂層 `case` 切開偽碼，列每個 case 碰到的視窗全域與字串 |
| `rea-scripts/t226/t226_triggers.py` | 視窗全域的引用函式 → gap-matrix 收包處理函式 |
| `rea-scripts/t226/t226_evcmp.py` | 建構子註冊的事件 vs. XML 事件 |
| `rea-scripts/t226/t226_evbin.py` | XML 事件名是否存在於 BIN |
| `rea-scripts/t226/t226_xmlsum.py` | 摘要 XML 視窗與元件 |
| `rea-scripts/t226/t226_surfcheck.py` | XML 引用的 surf 在客戶端 `Sprite*.idx` 有無 `.img`／`.tbt` |

既有腳本沿用：`ui_xml_decrypt.py`、`idx_find.py`、`win_strings.py`、`rtti_names.py`、`xml_table.py`、`run_ida.ps1`、`ida_dec_xref.py`、`ui_ida_query.py`。
