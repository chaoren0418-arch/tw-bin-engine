# 查證補遺第 10 輪

各節對應一張查證任務；簡稱 **FU10**，引用寫 `FU10§n`。標記沿用 FU9：**已驗證**（反編譯／反組譯／伺服器原始碼或位元碼直接看到）、**推測**、**待驗證**（真機才能確認）。

## 1. FishObtainUI 的釣魚物品表（T380／#884）

依據：FU9§6.1、§14.3；R9-P（#860）裁定「釣魚物品表查到之前不開 FishObtainUI 清單」。

### 1.0 方法與來源

- **TW16 BIN**：P01 `input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，唯讀。地址換算同 FU9§0：`.text` FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；bss 只寫 VA。下面 `.rdata` 的兩個遮罩已直接讀 BIN 位元組核對（FO `0xD81F20`、`0xD4F7C8` 都是 `08 00 00 00 00 00 00 00`）。
- **IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本任務 worktree 的 `scratch/t884/ida/tw16.idb`，複製前後 SHA-256 都是 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`。
- **IDA**：IDA 7.7＋Hex-Rays，唯讀批次 6 輪（q1–q6），腳本沿用 `rea-scripts/t742/ida_t742.py`，執行器 `rea-scripts/run_ida.ps1`，查詢清單在 `rea-scripts/t884/`。
- **subst**：每輪前後都跑 `subst`，開始時沒有任何代號被占用；本輪一律用 `W:`，每輪結束後 `subst` 都沒有輸出（沒有殘留）。
- **3.81 伺服器**：原始碼 `D:\lineage開發\1-伺服器\src` 的 `S_InvList.java`、`S_AddItem.java`、`S_ServerVersion.java`、`OpcodesServer.java`；另用 `javap -c -p` 核對部署 JAR `environments/p24/server/Server_Game.jar`（SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`）的 `S_InvList`、`S_AddItem` 位元碼。沒有啟動伺服器、沒有讀配置或 DB。
- **PakViewer**：`D:\lineage開發\PakViewer-main` 的 CLI `pak list`，用 `rea-scripts/pak/pak_index.ps1 -WithAlt` 加手動列 pmc、c760，共 **119 個 idx**（清單見 §1.4），輸出在 `scratch/t884/pak-index/`，不進 git。

### 1.1 `unk_1542864` 由誰填寫（已驗證）

`unk_1542864` 是一個 `vector<int>`（bss，VA `0x1542864`）。整個 BIN 只有 6 處引用：

| 引用 | 函式 | 作用 |
|---|---|---|
| `0x1145683` | `sub_1145680`（靜態建構） | `sub_4F8350` 初始化，登記解構 `sub_1149A30` |
| `0x1149A33` | `sub_1149A30`（靜態解構） | 釋放 |
| `0xE65037` | `sub_E65030`（FO `0xA64430`） | **唯一寫入點**：`sub_5286C0(&v)`＝`push_back` |
| `0xE65323` | `sub_E65320`（FO `0xA64720`） | `sub_54D870`＝清空 |
| `0xE65436`、`0xE65449` | `sub_E65420`（FO `0xA64820`） | 線性搜尋：`v` 在表裡 → 1 |

- **不是讀檔、也不是 .tbl／.txt 資源**：沒有任何開檔或資源讀取碰到這張表；表在程式啟動時是空的。
- **由背包封包填**：`sub_E65030` 只有 3 個呼叫者，全部在背包加入路徑：

| 呼叫點 | 所在函式 | 封包 |
|---|---|---|
| `0x92D74F`（FO `0x52CB4F`） | `sub_92D360`（FO `0x52C760`） | TW16 op 216 S_ADD_INVENTORY_BATCH＝3.81 **S5 S_InvList** |
| `0x92DEE3`（FO `0x52D2E3`） | `sub_92D8B0`（FO `0x52CCB0`） | TW16 op 30 S_ADD_INVENTORY＝3.81 **S15 S_AddItem**（`sub_92D8B0` 內另一條分支，分支條件沒細追） |
| `0x51902F`（FO `0x11842F`） | `sub_518CD0`（FO `0x1180D0`） | 同 op 30（`sub_92D8B0` 在 `0x92DD3B` 呼叫的一般路徑） |

- **寫入條件**（三處相同）：只在伺服器功能旗標 `dword_13CE514 & 0x40000` 時，封包尾段才會讀進一個 24 byte 的「延伸區塊」；寫入條件是**延伸區塊 +10 的 64 位元旗標有第 3 位（`& 8`）**，成立就把這件物品的 16 位元欄位（§1.2）`push_back` 進表。
  - 尾段讀法：`sub_926B10` 讀 1 byte 長度 n，`sub_926A70`（FO `0x525E70`）把接下來 n byte **原樣 `memcpy`** 到延伸區塊開頭。
  - 延伸區塊由 `sub_88ADE0`（FO `0x48A1E0`）清成 0，版面 `c d c d d(+10) d(+14) c d c`＝24 byte。
  - 判斷 `sub_518C80`（FO `0x118080`）：`(blk+10 & mask[0]) || (blk+14 & mask[1])`；遮罩 `asc_1183320`（`.rdata` FO `0xD81F20`）＝`{8, 0}`，`sub_518CD0` 用同值的 `asc_1150BC8`（FO `0xD4F7C8`）。
  - 判斷前，`sub_99BE70`（FO `0x59B270`）把延伸區塊存進背包物品（物品 `+1592` 起 24 byte）；判斷用的仍是剛讀到的區塊。
- **清空**：`sub_E65320` 只由 `sub_E65280`（FO `0xA64680`）呼叫；`sub_E65280` 只由世界重設 `sub_8C32B0`（`0x8C4278`，FO `0x4C3678`）呼叫，同時刪掉兩個釣魚物件（`dword_1542850`、`dword_1542860`）並清空 FishObtainUI 清單（`sub_C03D20`）。**結束釣魚時不清表**（FU9§6.1「結束釣魚時清空」指的是清單，不是表；更正見 §1.6）。
- **表的格式與筆數**：元素是 int（實際存 16 位元欄位的符號延伸，`movsx`），不排序、不去重（同一件物品重送會重複加，`sub_E65420` 只問有沒有）。筆數＝本次登入中帶旗標的背包封包筆數，**沒有固定內容**。

### 1.2 `sub_C021B0` 取的是哪個欄位（已驗證）

- `sub_C021B0`（FO `0x8015B0`）＝`return *(WORD*)(item+0x26)`；唯一的寫入 setter 是 `sub_C02D20`（FO `0x802120`，`mov [eax+26h], cx`）。
- S_InvList 處理 `sub_92D360` 在旗標 `& 2` 時用格式 `"dhcchcdcsc"`，`0x92D3E8`–`0x92D43C` 的推參順序對應：`d`→`var_634`（objid）、**`h`→`var_629+1`**、`c`→`var_619`、`c`→`var_621`、`h`→`var_618`（圖示）、`c`→`var_612`（祝福）、`d`→`var_63C`（數量）、`c`→`var_61A`、`s`→`var_210`（名稱）、`c`→`var_611`（狀態長度）。`0x92D650` 把 **`var_629+1`** 交給 `sub_C02D20`，`0x92D747` 也把同一個值交給 `sub_E65030`。
- 所以這個 16 位元欄位是 **objid 後面那個 `H`**；旗標沒有 `& 2` 時格式是 `"dcchcdcsc"`，這個欄位不存在（保持 0）。
- **3.81 有送**：`S_InvList.java:28–29`、`S_AddItem.java:20–21` 都是 `writeD(item.getId()); writeH(item.getItem().getPacketItemDescId());`。3.81 的功能旗標 `0x77d82`（`S_ServerVersion.java:28`，`protocol-gaps.md` 第 4 節）**有** `0x2` 與 `0x40000` 兩位，原版接 3.81 會讀這個 `H` 和尾段。引擎 `Inventory.cs:57` 已讀成 `InventoryItem.DescriptionId`。
  - 這和 `docs/protocol/fishing.md:35`「背包 wire 不帶模板 ID」不衝突：wire 帶的是 **item desc id**，不是模板編號；FishObtainUI 比的就是 desc id，**比對欄位不缺**。
- 別處的同一欄位：`sub_9490F0`（op 98，S57 刪除）與 `sub_932750`（op 164，`"dsdc"` 物品更新）也用 `sub_C021B0` 取 desc id，交給 `sub_E65220`／`sub_E655E0` 去比表（§1.3）。

### 1.3 清單怎麼用這張表（已驗證）

- 重填 `sub_E65050`（FO `0xA64450`）：走訪自己的背包，desc id 在表內的物品才 `sub_C03720` 加進 FishObtainUI。唯一呼叫者 `sub_E65130`（FO `0xA64530`），條件是物件＝自己（`dword_153CE60`）；`sub_E65130` 由物件動作封包在**動作 71**（釣魚）時呼叫（`sub_522FE0`、`sub_92CB70`，讀 `"hh"` 釣點座標）。3.81 S158 `C71`（`fishing.md`）會走到這裡。
- 數量更新 `sub_E655E0`（FO `0xA649E0`）：op 164 呼叫；desc id 在表內 → `sub_C04B00` 依數量差更新清單。desc id **14467** 另外更新釣魚窗的魚餌數（FU9§14.3）。
- 刪除 `sub_E65220`（FO `0xA64620`）：op 98 呼叫；desc id 在表內 → `sub_C03EB0` 從清單移除。
- 背包加入時（op 30 兩條路徑）表成立的那一筆也直接加進 FishObtainUI（`sub_518CD0` `0x519083` `sub_C038D0`）。

### 1.4 資源查詢（已驗證：沒有獨立的物品表資源）

表不來自資源（§1.1），本節只是照規定確認沒有遺漏的釣魚表檔：

- 本機 `0-客戶端` 24 個 idx：`Text`、`Tile`、`Sprite`、`Sprite00`–`Sprite15`、`LiTo\LTtext`、`LTsurf`、`LTsprite`、`LTtile`、`LTicon`（`launcher\TW13081901.pak` 沒有 idx，PakViewer 打不開）。
- 參考客戶端 5 套各 19 個 idx（`Tile`、`Text`、`Sprite`、`Sprite00`–`Sprite15`）：kor25 `D:\381懶人包kor25(原)`、pmc `D:\381懶人包泡麵陳(原)`、c760 `D:\760測試\超人760客戶端`、t89 `D:\天堂參考用客戶單\89天堂`、ly `D:\天堂參考用客戶單\雷雨天堂`。合計 **119 個 idx**。
- 檔名含 `fish`／`obtain` 的非 HTML 項目只有 UI 版面：本機 `Tile.idx` `UserObtainUI.xml`；參考客戶端 `FishingUI.xml`、`FishObtainUI.xml`、`UserObtainUI.xml`（t89 小寫、ly 另有 `-c` 版；c760 #4704／#4705／#4822）。其餘 556 筆是 NPC 對話 HTML（`fisher*`、`evfishman*` 等）。**沒有任何 .tbl／.txt／.dat 是釣魚物品表**。`FishObtainUI.xml` 的 SHA 見 FU9§6.2，不重算。

### 1.5 3.81 下表的內容（已驗證）

- 3.81 `S_InvList.java:77–80`、`S_AddItem.java:71–74`：尾段固定是 `writeC(10); writeH(0); writeD(0); writeD(0);`——長度 **10**，內容全 0。部署 JAR 位元碼相同（`S_InvList` `430: bipush 10` → `writeC`，接著 `iconst_0` `writeH`、`writeD`、`writeD`；`S_AddItem` `344:` 起相同）。
- 延伸區塊只被覆蓋前 10 byte（偏移 0–9），**偏移 +10 的旗標根本不在 3.81 的資料範圍內**，維持 `sub_88ADE0` 的 0。
- 所以原版接 3.81：`sub_518C80` 永遠回 0 → **`unk_1542864` 永遠是空表** → FishObtainUI 清單**永遠沒有任何一列**（開窗只看到底圖、外框、關閉鈕和標題位置）。

### 1.6 結論與更正

**結論：可以照原版做——而照原版做的結果是「清單恆空」。** 物品表不缺：表的來源（背包封包尾段旗標 bit 3）、比對欄位（desc id）都已驗證，3.81 也都有送；只是 3.81 從不設那個旗標。這和 R9-P 目前「不畫清單」在畫面上一致，差別只在理由從「待驗證」變成「已驗證：3.81 下為空」。

**更正**
- FU9§6.1「3.81 背包封包不帶模板編號，查到也可能無法比對」→ 比對欄位是 desc id，3.81 有送；問題不在比對，而在 3.81 不設旗標。
- FU9§6.1「結束釣魚時清空」→ 結束釣魚不清表；表只在世界重設（`sub_8C32B0`）清。清單在重設時清空，在每次自己的動作 71（開始釣魚）時重填。
- `NativeUi/FishObtainPanel.cs:12–13`、`:94` 與 `NativeUi/FishingPanel.cs:29` 的「物品表待驗證」註解應改成已驗證。

**建議 code 任務（R10-A，低優先）：FishObtainUI 清單照原版接上**
- 目標：
  1. 背包解析把尾段讀成「長度＋內容」（目前 `Inventory.cs:57` 固定讀 11 byte 存 `TailHex`），算出 `FishObtainFlag`＝長度 > 10 且內容第 10 byte `& 8`（64 位元旗標 bit 3）；3.81 永遠是 false。
  2. 新增釣魚物品表模型（desc id 集合）：S5／S15 帶旗標的物品加入；世界重設清空；自己 S158 動作 71 時用表重填清單；S57 刪除、數量更新時同步。
  3. `FishObtainPanel` 畫清單列與捲軸，版面與 `UserObtainPanel` 相同（同一建構子，可共用列幾何）；3.81 下表恆空，所以只會畫捲軸和空清單。拿掉 `Skipped` 的「清單待驗證」兩項。
  4. 測試：S5 尾段長度 10（3.81 實際格式）→ 表空、清單空；合成長度 ≥ 11 且 bit 3 → 該 desc id 入表、清單列出；世界重設清表。合成封包只驗程式邏輯，不代表 3.81 會送。
- 可改的檔案：`engine/source/Inventory.cs`（尾段解析、加欄位，不改既有欄位語意）、`engine/source/NativeUi/FishObtainPanel.cs`、`engine/source/NativeUi/FishingPanel.cs`（只改註解）、`engine/source/NativeUi/FishingFollowupTests.cs` 或新檔 `FishObtainListTests.cs`（`[CheckGroup]` 自動發現）。不改 `Program.cs`、註冊表、`rebuild-lock.json`。
- 若不想為 3.81 永遠不會出現的旗標寫程式：只做第 3 項的「改註解＋畫空清單與捲軸」也算照原版（行為相同），請調度器在派工時二選一。

**待驗證（真機）**：原版 TW16 客戶端接 3.81 時，FishObtainUI 開窗是否確實為空清單（需要 TW16 BIN 真機，屬 K2 1:127 同類限制）；捲軸在清單空時是否顯示拖曳鈕。

### 1.7 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 4:FishingUI | **K5**（`AniGauge` 子項 K1；FishObtainUI 清單已驗證，3.81 下恆空） | 物品表＝背包尾段旗標 bit 3 的 desc id，3.81 尾段只有 10 byte 全 0；建議 R10-A |
