# 原版視窗類功能查證（G10b，T181／Issue #483）

整理日期：2026-11-08（Asia/Taipei）。範圍：`m6-native-diff.md` G10(b) 的 T102、T107、T126、T127、T130、T131、T133、T134、T139–T142。

## 0. 方法與身分

- **分析對象：** 未加殼的 `TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（`rea-scripts/nat.py` 會核對）。
- **IDB：** P01 IDB（`P01-分析基準/ida/03-ascii-isolated/TW1608262200.idb`，SHA-256 `3a0bf2065993989e…`）複製到 `scratch/rea/ida/tw16.idb` 再用。工具是 IDA 7.7＋Hex-Rays，查詢 q1–q6 都是唯讀，內容見 `rea-scripts/windows_queries.json`。
- **subst：** 執行前先檢查。W:、Q: 已被其他查證任務（task-484）占用，所以腳本每次挑未占用的代號（本輪實際用 Q:、V:），跑完立即 `subst /D`。
- **UI XML：** 用 `rea-scripts/ui_xml_decrypt.py` 從 `D:\lineage開發\0-客戶端\Tile.pak` 解到 `scratch/rea/uixml/`，共 65 個，**不進 git**。
  - `Tile.idx` SHA-256：`9291fd80…`
  - `Tile.pak` SHA-256：`82e52f47…`
- **地址：** VA 換 File Offset 用 `rea-scripts/va2off.py`：`.text` 減 `0x400C00`、`.rdata` 減 `0x401400`、`.data` 減 `0x401800`。
- **封包名稱：** 依 BIN 內的名稱表（`sub_9515E0`），以及第一組 dispatch table（index `0x94828C`／FO `0x54768C`），由 `rea-scripts/buff_handlers.py` 列出。這些是 **TW16 自己的編號**，和 3.81 伺服器的 opcode 不同（見 `rea-inventory.md`）。
- **引擎現況：** main `23b129ac` 的 `engine/source`。

**記號：** 「一致？」欄沿用 `m6-native-diff.md`：✅ 一致、⚠️ 部分一致、❌ 不一致、❓ 未查證。

### 0.1 先講兩個總體發現（已驗證）

1. **TW16 有兩套視窗系統。**
   - **舊式寫死視窗：** 在 `sub_8C7C80`（VA `0x8C7C80`／FO `0x4C7080`）一次全部建立。按鈕 surf 與座標寫死在建構子裡，有些會讀 `uiextra86.xml` 的設定。例如 `TradeWindow`、`BoardListWindow`、`ExcludeWindow`、`PetControl`、`InputWindow`。
   - **XML 視窗：** 由 `sub_B56A40`（VA `0xB56A40`／FO `0x755E40`）依 UI 名稱建立。例如 `FriendUI`、`ExcludeUI`、`MailExcludeUI`、`SummonUI`、`PersonalShopUI`、`ResetStatUI`、`RenewalMailUI`、`SelectUI`、`FishingUI`。
   - 兩套都經 `sub_A57C60`（VA `0xA57C60`／FO `0x657060`）開窗，這是最多 4 層的視窗堆疊（`dword_153D220[4]`）。
2. **本機資源缺了 TW16 要的 34 個 UI XML。**
   - BIN 的 XML 檔名表在 VA `0x137B39C`–`0x137B4BC`（FO `0xF79B9C` 起）。其中 34 個在本機 `Tile.pak`、`Text.pak` 和 17 個 `Sprite*.pak` 都找不到（`rea-scripts/idx_find.py`）。和本文有關的有：
     - `SelectUI.xml`、`CreateUI.xml`、`ResetStatUI.xml`
     - `FishingUI.xml`、`FishObtainUI.xml`
     - `RenewalMailUI.xml`、`GameRankingUI.xml`、`ItemShopUI.xml`、`ReNStatusUIEx.xml`
   - XML 載入函式 `sub_B3D390`（VA `0xB3D390`／FO `0x73C790`）找不到檔案時會跳出 `MessageBoxA("Cannot Found %s", "Error")` 並回傳 0，**沒有退回機制**。
   - 本機客戶端實際執行的是 `LiTo.bin`（3.81）。目前的 `Tile.pak` 是 3.81 世代的資源，所以 **TW16 的 XML 視窗只能查到類別與事件名稱，查不到版面**。反過來，本機有的 `ReNStatusWinUI-c.xml`／`ReNStatusWinUI.xml` 在 TW16 中**沒有任何字串引用**（ASCII 與 UTF-16 都搜過）。TW16 讀的是 `ReNStatusUIEx.xml`（VA `0x119CAB0`／FO `0xD9B6B0`），本機沒有這個檔。

## 1. 本輪解讀的 XML

本節只摘錄版面。座標都是視窗內的相對座標；surf 編號指 `SurfList.xml`。

### 1.1 `PersonalShopUI.xml`（個人商店**開店設定**窗）

SHA-256 前 16 碼：明文 `63a4def5…`、密文 `407eeb38…`。

- 視窗 `PersonalShop_Window`：底圖 `5734;1`、框 `5735;1`，可拖曳（`Moveable=18`），關閉鈕在 (166, 13)。
- **出售清單** `SellingItem_List` 位於 (22, 42)，2 列 × 4 格 `ItemIcon`。每格 33×33、間距 1，一頁 8 格。
- **收購清單** `BuyingItem_List` 位於 (22, 132)，格式同上，一頁 8 格。
- 兩行商店訊息 `Message1_Edit`／`Message2_Edit`：位於 (18, 206)／(18, 224)，144×16，**各限 20 字**（`LimitText=20`）。
- **商店外型** `Poly_List` 位於 (17, 243)：3 張圖（surf 5752），各帶一個 `CheckButton`（`5736;4`，事件 `SelectPoly`）。
- 翻頁：四個按鈕（5737–5745）在 y=311，頁碼 `0/0` 標籤放在 surf 5741 上。
- 確定 (30, 334)：5749–5751。取消 (98, 334)：5746–5748。
- BIN 中：類別 `PersonalShopUI`，RTTI VA `0x13855EC`。建構子 `sub_DDC740`（VA `0xDDC740`／FO `0x9DBB40`），由 `sub_B56A40` 依名稱建立，指標存在 `dword_1541CF8`。事件字串有 `ClosePersonalShop`（VA `0x11DDA68`）與 `SelectPoly`（VA `0x11DDA98`）。

### 1.2 `FriendUI.xml`／`ExcludeUI.xml`／`MailExcludeUI.xml`（好友、封鎖、信件封鎖）

三個檔版面相同，只差標題圖與事件名稱：

| 檔案 | 標題圖 | 事件 |
|---|---|---|
| `FriendUI.xml` | 4992 | `FriendName` |
| `ExcludeUI.xml` | 4993 | `ExcludeName` |
| `MailExcludeUI.xml` | 4994 | `MailExcludeName` |

- 底圖 `4990;1`、框 `4991;1`。根節點有 `Toggle="true"`，視窗預設不啟用（`Activate=0`）。
- 關閉鈕在 (222, 13)，用 4429／4430。
- **名單** `Item_List` 位於 (25, 50)，182×226，**14 列 `RadioButton`**。每列 182×16，選取底圖 `4995;4`；右側另有 `Info_Label`（x=122，寬 60），預設文字 `[Info]`。
- 垂直捲軸位於 (215, 56)，12×205，用 surf 4060–4066。
- 人數標籤 `Count_Label` 位於 (150, 28)，57×18。
- BIN 中：
  - `FriendUI` 建構子 `sub_B17DD0`（FO `0x7171D0`），存在 `dword_153FCB4`。
  - `ExcludeUI` 建構子 `sub_B17D10`（FO `0x717110`），存在 `dword_153FCB8`。
  - `MailExcludeUI` 建構子 `sub_B17E60`（FO `0x717260`），存在 `dword_153FCBC`。
  - 另有舊式 `FriendWindow`（vtable `0x117C8AC`）。它的建構子 `sub_846210`（FO `0x445610`）**沒有任何呼叫者**，推測是死碼。
  - 舊式 `ExcludeWindow` 仍在 `sub_8C7C80` 以 id 20 建立（`sub_8300B0`，FO `0x42F4B0`）。

### 1.3 `SummonUI.xml`＋`summonList.xml`（召喚怪物選擇）

- 視窗 `SummonMonster_Window`：底圖 `4893;1`、框 `4894;1`，關閉鈕在 (225, 15)。
- **快速召喚 3 格** `QuickSummonLevel_List` 位於 (25, 67)。每格底圖 4958，滑過 `5029;4`，等級標籤在 (37, 62)。
- **等級按鈕 6 列 × 2** 位於 (25, 169)，對應 `summonList.xml` 的 12 個等級：28、32、36…72。
- 選了等級後，`SummonLevelList_Control`（預設不啟用）顯示該等級最多 6 隻怪（2 列 × 3 格）。
- 魅力 `Charisma_Label` 位於 (173, 45)。
- 確定 (60, 340)：4949–4951。取消／返回 (127, 340)：4952–4957。
- `summonList.xml` 共 31 隻：level 28–72，每隻有 `action`、`desc`、`surf`、`minCha`（8／14／36／44）。
- BIN 中：類別 `CSummonUI`，建構子 `sub_BFFB60`（FO `0x7FEF60`），存在 `dword_1540858`。開窗函式 `sub_503FF0`（FO `0x1033F0`）會在視窗未開時才開，呼叫點是 `0xDF8AFB` 與 `0xE0415C`。事件字串 `QuickSummonButton` 在 VA `0x11AC778`。
- 另有 `CPetSummonUI`（`PetSummonUI`，建構子 `sub_759BF0`），本機沒有對應的 XML。

### 1.4 `ReNStatusWinUI-c.xml`（新版裝備／能力窗，**不是**重置窗）

- 根節點 `RenewalStatusUI`，含 4 個視窗：
  - `Equip_Window`：19 個裝備格，包括耳環、頭盔、項鍊、T 恤、盔甲、斗篷、手套、腰帶、盾、戒指 1／2、靴子、80／85 級戒指、武器、箭、3 個符文。另有 2 個分頁鈕（字串 882／883）。
  - `Status_Window`：等級、HP、MP 量表（2452–2454），負重，六項能力，魔法等級與加成，陣營，四屬性抗性，ER、DG，以及加成與業力兩個 tooltip 鈕。
  - `BonusStatusTooltip_Window`：16 行加成。
  - `KarmaTooltip_Window`。
- XML 裡**沒有任何重置相關元件**，也就是沒有 +1／+10 級、確認重置之類的東西。
- 類別 `CRenewalStatusUI`（建構子 `sub_EDCBE0`，FO `0xADBFE0`）在 TW16 中存在，但如第 0.1 節所述，TW16 讀的是 `ReNStatusUIEx.xml`，**不讀本檔**。

### 1.5 `uiextra86-c.xml` 的三個視窗（舊式設定）

| 視窗 | 元件 | BIN 讀取點 |
|---|---|---|
| `MESSAGE_WINDOW` | 底圖 1964 位於 (6, 8)、框 1965、**只有一個 OK 鈕** 1966 位於 (70, 118) | 字串 VA `0x1191DD4`／FO `0xD909D4`，在 `0xA84425` 讀取 |
| `HYPER_TEXT_WINDOW` | 底圖 1968 位於 (6, 9)、框 1969、關閉 X 1962 位於 (225, 15)、長捲軸 1970 位於 (217, 51)、上下鈕 2042／2040 位於 y=37／275、拉桿 2044 | 字串 VA `0x1191DE4`／FO `0xD909E4`，在 `0xA844B9` 讀取 |
| `PET_QUICK_WINDOW` | 底圖 2242 位於 (0, 0)、框 2243；**5 個指令鈕**：攻擊 2244 (11, 14)、防禦 2250 (36, 15)、散開 2790 (61, 15)、警戒 2788 (86, 15)、休息 2248 (111, 14) | 字串 VA `0x1191E78`／FO `0xD90A78`，在 `0xA84980` 讀取 |

相關的 `TRADE_WINDOW`（底圖 1983、BUY／SELL／OK／CANCEL 鈕都在 y=292、捲軸）與 `TRANSACTION_WINDOW`（玩家交易）也在同一檔，第 2 節會用到。`PET_QUICK_WINDOW` 對應的類別 `PetQuickWindow`（vtable `0x11E8ECC`）建構子 `sub_F3A390`（FO `0xB39790`）**沒有呼叫者**，推測 TW16 不再建立這個快捷窗；寵物指令窗是 `PetControl`（`sub_F3A0C0`，FO `0xB394C0`，由 `sub_8C7C80` 建立，按鈕 surf 1159／886、1156／885）。

## 2. 逐項對照

「原版來源」只列有證據的部分。「推測」與「待驗證」另外標明。

| T 編號 | 功能 | 原版來源（TW16） | 引擎現況 | 一致？ | 對應 `native-ui.md` 模組 |
|---|---|---|---|---|---|
| T102 | NPC 買賣、私商 | **NPC 商店：** `S_SELL_LIST`（TW16 op 40，`sub_94BD80`／FO `0x54B180`）與 `S_BUY_LIST`（op 131，`sub_931FA0`／FO `0x5313A0`）交給 `LineageShop::CNpcShop` 的 XML 視窗（`sub_DF1680`／FO `0x9F0A80` 找 `Items_List`、`ItemList_Scrollbar`）。這個 XML **本機缺檔**（65 個 XML 都沒有 `Items_List`）。3.81 世代可用的是 `uiextra86` 的 `TRADE_WINDOW`（由 `TradeWindow` 讀取，`sub_A26E60`／FO `0x626260`，id 19）。**私商購買：** `S_PERSONAL_SHOP_LIST`（op 29，`sub_9464D0`／FO `0x5458D0`）用同一套商店 UI，模式 13／14。**開店設定：** `PersonalShopUI`（第 1.1 節），出售與收購各 8 格一頁、訊息 2×20 字、3 種外型 | NPC：`npcPanel` 450×380 清單加數量框（`NpcUi.cs:13`）。私商：`privateShopPanel` 分頁（`PrivateShopUi.cs`）。**沒有開店設定窗** | ⚠️ | 無；`native-ui.md` §0 列為「不涵蓋」（交易、個人商店）。需新增 `ui-shop`（TRADE_WINDOW）與 `ui-personal-shop`（PersonalShopUI） |
| T107 | 建角、刪角、切角 | 選角與建角是 **XML 畫面**：`CSelectUI`（`sub_EEBB10`／FO `0xAEAF10`，`SelectUI.xml`）、`CCreateUI`（`CreateUI.xml`）。兩個 XML **本機缺檔**，版面無法取得 | `CharacterCreationDialog` 440×590 對話框（`CharacterManagementUi.cs:14`）；刪角用 `MessageBox` | ⚠️（結構不同：原版是全畫面，引擎是對話框；版面因資源缺而無法比對） | 無；§0「登入／選角畫面」另開設計 |
| T126 | 好友、封鎖 | **三個獨立 XML 視窗**：`FriendUI`、`ExcludeUI`、`MailExcludeUI`（第 1.2 節）。每窗一次顯示 14 列＋捲軸＋人數標籤，每列右側有 `Info` 欄。名單上限**待驗證**：XML 只決定可見列數 14 | 單一 `BuddyPanel` 分頁，好友與封鎖兩個 ListBox 並排（`BuddyUi.cs:64`）。**沒有信件封鎖** | ⚠️ | 無；需新增 `ui-friend`（三窗共用一個面板類別，換標題圖與事件） |
| T127 | 寵物、召喚物控制 | 寵物指令窗是舊式 `PetControl`（第 1.5 節）。`PET_QUICK_WINDOW` 有 5 個指令（攻擊、防禦、散開、警戒、休息），但類別推測沒有建立。**召喚怪物選擇**用 `CSummonUI`（第 1.3 節）。寵物分頁另見 UI§6 `PARTY_PET_RT_WINDOW` | 「寵物」分頁，6 個指令按鈕（多一個解散，`PetUi.cs:45`）。**沒有召喚怪物選擇窗** | ⚠️ | `ui-party`（寵物分頁）。召喚選擇需新增 `ui-summon` |
| T130 | 公告板 | **專用舊式視窗**，不是 `HYPER_TEXT_WINDOW`：`BoardListWindow`（`sub_7CB710`／FO `0x3CAB10`）、`BoardReadWindow`（`sub_7CBA70`／FO `0x3CAE70`）、`BoardWriteWindow`（`sub_7CBE90`／FO `0x3CB290`），都由 `sub_8C7C80` 建立。清單窗有 6 個鈕：surf 658 (209, 305)、660 (34, 332)、662 (139, 304)、664 (171, 304)、666 (64, 311)、668 (99, 311)。閱讀窗有 4 個鈕：672 (56, 312)、676 (110, 312)、678 (213, 297)、674 (90, 299)。`S_BOARD_LIST`（op 152）→ `sub_7CD600`（FO `0x3CCA00`），每筆 `dsss` 68 byte，**一頁 8 筆**；`S_BOARD_READ`（op 248）→ `sub_7CD9F0`（FO `0x3CCDF0`），格式 `dssss` | `BoardUi` Form 740×680（`BoardUi.cs:17`），一頁 8 筆；發文與刪文前用 OKCancel 確認（`:26-27`） | ⚠️（一頁 8 筆一致；視窗不一致） | 無；需新增 `ui-board`（3 窗），surf 與座標見本列 |
| T131 | 信件 | `RenewalMailUI`（建構子 `sub_9AEE70`／FO `0x5AE270`），`S_MAIL_INFO`（op 99，`sub_941000`／FO `0x540400`）把資料寫進這個視窗（`dword_14EBED8`）。XML **本機缺檔**，而且缺檔時 `sub_B3D390` 只跳錯誤框、**沒有退回** | `MailDialog` 780×640（`MailUi.cs:49`） | ❓（資源缺） | 無；需等資源，或改以 3.81 客戶端的信件介面為準 |
| T133 | 刪除、給予物品 | 伺服器 Yes/No 走 `S_ASK`（op 85，`sub_92EAD0`／FO `0x52DED0`）→ 通用確認框 `sub_4FC710`（FO `0xFBB10`）→ `sub_AC9560`（FO `0x6C8960`）。`MESSAGE_WINDOW` **只有 OK 鈕**，不能當刪除確認框。刪除時本地確認框長什麼樣、給予是否靠拖曳，**待驗證** | `MessageBox` YesNo，預設「否」（`ItemTransfer.cs:116`）；給予靠拖曳到場景物件 | ❓ | 無（背包窗不在 #448 範圍） |
| T134 | 武器修理、PK 查詢 | `S_FIXABLE_ITEM_LIST`（op 136，`sub_93F5E0`／FO `0x53E9E0`）逐筆加入 `TradeWindow`，再以**模式 6**（`sub_A59780`／FO `0x658B80`）開窗，用的就是 `TRADE_WINDOW` 清單窗。清單為空時改開 NPC 對話頁 `touma3`／`gayle5`（`HYPER_TEXT_WINDOW`）。PK 查詢是 HTML 動作 `HyperActCheckPK`（RTTI VA `0x137DEE0`），**沒有專用視窗** | 獨立 Form 580×420「武器修理與 PK 查詢」（`WeaponRepairUi.cs:18`），由 HUD 按鈕開啟 | ❌（引擎多出專用視窗與 HUD 鈕） | 併入新增的 `ui-shop`（TRADE_WINDOW 模式 6）；PK 查詢走 NPC 對話 |
| T139 | 釣魚 | XML 視窗 `LineageFishing::CFishingUI`（建構子 `sub_E65660`／FO `0xA64A60`，名稱 `FishingUI`），事件 `FishingNet`、`QuitFishing`，欄位 `+0x164` 初值 5000（推測是 5 秒計時）。另有 `CanFishingTileAreaDisplay`（可釣格提示）與 `SC_FISHING_NOT`。`FishingUI.xml`、`FishObtainUI.xml` **本機缺檔** | 只有 `FishingHud` 按鈕 96×30（`Fishing.cs:89`） | ⚠️（原版有專用視窗與釣點提示；版面因資源缺而無法比對） | 無；需新增 `ui-fishing`（等資源） |
| T140 | 查詢玩家、排名 | 指令類別 `Commandquery`、`Commandrank`（RTTI VA `0x138ED4C`、`0x138FD5C`），HTML 動作 `HyperActWho`、`HyperActRank`。伺服器回的 `S_HYPERTEXT`（op 144，`sub_940030`／FO `0x53F430`）→ `sub_BBCB00`（FO `0x7BBF00`），在 NPC 對話視窗（`HYPER_TEXT_WINDOW`）顯示。另有 `CHighLevelRankingUI`（建構子 `sub_612AA0`，沒有呼叫者）與缺檔的 `GameRankingUI.xml` | 全部轉成聊天行「[查詢] …」（`PlayerQuery.cs:108`）。排名頁另外也在 NPC 對話顯示 | ⚠️（原版是 HTML 窗，引擎是聊天行） | NPC 對話窗（§0 不涵蓋，需新增 `ui-hypertext`）；聊天行依 `ui-chat-format` |
| T141 | 求婚、結婚 | 求婚確認走**通用 Yes/No 框**。`S_ASK` 的 default 分支（msg 654 不在特例內）→ `sub_4FC710` → `sub_AC9560(文字, 1, …, 回呼 sub_4FCA30)`。回覆是 TW16 opcode 33 `chdhcs`（`sub_4FCA30`／FO `0xFBE30`）。只有 729（12 秒）、1232、1256、1268（10 秒）這幾個訊息有逾時自動回覆。HTML 動作有 `HyperActPropose`、`HyperActDivorce`、`HyperActMarriedMark` | 專用 Form「求婚與結婚」540×240（`Marriage.cs:149`），由 HUD 按鈕開啟；收到 S219/654 時自動開窗 | ❌（原版沒有專用視窗與 HUD 鈕） | 需新增通用 `ui-yesno`（`sub_AC9560` 的視覺待驗證） |
| T142 | 能力值重置 | **`CResetStatUI`**（`ResetStatUI.xml`；建構子 `sub_9CF150`／FO `0x5CE550`，存在 `dword_14EBF4C`）。由 TW16 opcode 43 的處理函式 `sub_94E0E0`（FO `0x54D4E0`）在子類型 1／2／3 時呼叫（vtable `+192`，再 `sub_9CF250`），和 3.81 `S_CharReset` 子類型 1–3 對得上。`ResetStatUI.xml` **本機缺檔**。**`ReNStatusWinUI-c.xml` 不是重置介面**（第 1.4 節） | 模態 Form 540×490（`CharResetUi.cs:16`），由 S64 子類型 1 自動開窗 | ⚠️（觸發時機一致；視窗版面因資源缺而無法比對） | 無；需新增 `ui-reset-stat`（等資源） |

### 2.1 通用視窗結論（給後續 code 任務）

- **Yes/No 確認（已驗證）：**
  - 伺服器詢問統一走 `sub_4FC710` → `sub_AC9560`。`S_ASK` 只對少數 id 特別處理：
    - `switch` 的特例是 102、180、289、321、325、479、512、622、252、217、951；default 分支另外略過 1565、特別處理 1906。其餘 id（含求婚 654）都走通用框。
    - 252（交易）看旗標 `byte_136E99D`：為 1 時顯示通用框，否則直接自動回覆 opcode 33（回覆值與旗標的設定來源待驗證）。
    - 953、954 在特定條件下自動回覆。
  - 引擎目前**依功能各自做按鈕**（`Trade.cs:43` 的分派、`Marriage.cs`、`Party.cs`、`Duel.cs`），與原版不同。
- **`MESSAGE_WINDOW`（已驗證）：** 只有 OK 鈕，是單純的訊息框，不是 Yes/No 框。
- **NPC 對話（已驗證來源，版面待做）：** `HYPER_TEXT_WINDOW` 的尺寸由 surf 1968／1969 的 `.img` 決定（UI§6 記為 239×317），含關閉 X、捲軸與上下鈕。它是 NPC HTML、修理空清單提示、查詢與排名結果的共用視窗。引擎沒有對應的視窗類別（`npcPanel` 是 WinForms 清單）。
- **視窗堆疊（已驗證）：** `sub_A57C60` 最多同時開 4 個視窗，第 5 個會擠掉最舊的那個。原生 UI 框架如要比照，需在 C2 的 `UiPanelRegistry` 加上這個上限。

## 3. 未驗證與限制

- 沒有啟動遊戲、伺服器或 DB；所有結論都來自靜態分析。
- TW16 的 XML 視窗中，`SelectUI`、`CreateUI`、`ResetStatUI`、`FishingUI`、`RenewalMailUI`、NPC 商店 XML、`GameRankingUI` 本機都缺檔，**版面無法取得**。這幾項只能等使用者提供對應的資源，或改以 3.81 `LiTo.bin` 的實機截圖為準。`LiTo.bin` 有 Themida 殼，不在本輪靜態分析範圍。
- `sub_AC9560` 確認框的 surf、尺寸與按鈕位置還沒追（要從 `sub_AC9410` 往下）。
- `CNpcShop` 用哪一個 XML、`TradeWindow` 的模式 0／1／7／13–16 各由誰觸發，只追了修理（模式 6）。
- 好友與封鎖的名單上限、`Info_Label` 顯示什麼（上線狀態或備註），待驗證。
- `CSummonUI` 的開窗呼叫點（`0xDF8AFB`、`0xE0415C`）屬於哪個封包，待驗證。
- 刪除物品的本地確認，以及給予物品的操作方式，待驗證。

## 4. 腳本

| 腳本 | 用途 |
|---|---|
| `rea-scripts/ui_xml_decrypt.py` | 解 `Tile.pak` 的 UI XML 到 `scratch/`（沿用 #429） |
| `rea-scripts/ui_ida_query.py` | IDA 唯讀查詢；本輪新增 `rtti` 鍵：TypeDescriptor → COL → vtable → 建構子與呼叫者 |
| `rea-scripts/windows_queries.json` | 本輪 q1–q6 查詢內容 |
| `rea-scripts/win_strings.py` | 找 ASCII／UTF-16 字串與絕對位址引用 |
| `rea-scripts/rtti_names.py` | 列出符合關鍵字的 RTTI 類別名 |
| `rea-scripts/xml_table.py` | 印出 BIN 的 UI XML 檔名表 |
| `rea-scripts/idx_find.py` | 在客戶端所有 `.idx` 中找檔名（缺檔判定） |
| `rea-scripts/buff_handlers.py`、`va2off.py` | 封包編號 → 處理函式；VA → FO |
