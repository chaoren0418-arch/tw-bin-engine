# 協定空白查證：S_EXTENDED 子碼、訊息字串表、版本檢查、未知封包（T225，#532）

本文回答 [`gap-matrix.md`](gap-matrix.md) 第 8 節的「S_EXTENDED 子碼表」「S_MESSAGE_CODE 字串表」「版本檢查」「未知收包」「未知送包」「S87 同身份入口」六列。每項寫：原版怎麼做、3.81 伺服器是否支援、引擎現況、建議的 code 任務。

標記：**已驗證**＝反編譯或原始碼直接讀到；**推定**＝依名稱、欄位與行為推論；**待驗證**＝本輪查不到。

## 0. 方法與身分

- 原版：`TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，ImageBase `0x400000`。`.text` 的 File Offset（FO）＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`（`va2off.py` 核對）。
- IDA 7.7＋Hex-Rays：P01 的 `ida/03-ascii-isolated/TW1608262200.idb`（SHA `3a0bf206…e82f`）複製到 `scratch/rea/ida/tw16.idb`，複製前後雜湊相同；經 subst `Q:`（執行前 `subst` 無占用，每次結束由 `run_ida.ps1` 解除）。只做反編譯、反組譯與 xref，沒有改 IDB 以外的東西。
- 3.81 伺服器：`D:\lineage開發\1-伺服器\src`（`S_PacketBox.java` SHA `817ba5fd…e977`，與 [`server-protocol.md`](server-protocol.md) 相同），只讀原始碼。
- 引擎：本分支（`origin/main` `1d14437a`）的 `engine/source`，只讀，不建置、不執行。
- 字串表：客戶端 `D:\lineage開發\0-客戶端\Text.idx／Text.pak` 只在記憶體解出 `string-c.tbl`（作法同引擎 `MagicRendering.cs` 的 `DecodeText`），本文只引用少數幾條文字當證據；解出的表與摘要 JSON 留在 `scratch/`，**沒有進 git**。
- 沒有啟動遊戲、伺服器或 DB。

送包函式 `sub_9FBC00`（VA `0x9FBC00`／FO `0x5FB000`）的呼叫慣例：`sub_9FBC00(fmt, opcode, 欄位…)`，**格式字串的第一個 `c` 就是 opcode 本身**。例如 `"c"` 表示只有 opcode、沒有欄位；`"cd"` 表示 opcode 加一個 D。下文的「欄位」都已扣掉 opcode。全 BIN 共 847 個呼叫點（`ida_t225_sends.py`），其中 5 個的 opcode 不是立即值（第 6 節）。

## 1. 結論摘要

| 項目 | 結論 | 3.81 伺服器 | 引擎 | 建議 |
|---|---|---|---|---|
| S_EXTENDED（19／39／109） | **不是** S250 的對應。這三個 op 讀 2-byte message id 後交給 protobuf 分派器；註冊了 125 個 id（商城、製作、競技場、攻城、通知等 2010 年代新系統） | 不支援（伺服器原始碼沒有 protobuf） | 無 | **不建議補** |
| S250 PACKETBOX 的真正對應 | TW16 **S_EVENT（op 108）**，`sub_9357E0` 一個 158 個 case 的 switch，子碼與 3.81 `S_PacketBox` 常數一致（0–4 攻城訊息字串 639–643 完全對上） | 會送 62 個子碼 | 處理 24 個 | 補 39 個缺的子碼（第 2.3 節分組） |
| S_MESSAGE_CODE 字串表 | `Text.pak` 內的 `string-<語系>.tbl`，CP950 時為 `string-c.tbl`；id ≥ 表筆數直接丟棄 | 只送 id＋參數 | 只硬寫 4 個 id | **載入整張表**（code 任務 A） |
| 版本檢查 | 原版收到 S_KEY 後送 C_VERSION；S_VERSION_CHECK 第一個 byte 1／2／3 會跳錯並結束程式，否則讀語系、伺服器功能旗標 | **不要求**：C14 屬 TRANSPORT，任何階段都可送；登入不檢查是否送過 | 不送 C14、不收 S139，登入正常（#23 實機） | 不必補；如要補只為取得功能旗標（低優先） |
| 未知收包 6 個 | S_BLINK＝S149 傳送；S_HIT_RATIO＝S237 HP 條；S_CLONE＝S76 第二型；S_SHOW_MAP＝S187（推定）；S_EVENT＝S250；S_NOTICE＝登入期彈窗（3.81 無） | 見第 5 節 | 前三個已有，S187 缺 | 只有 S187 可開低優先任務 |
| 未知送包 13 個 | 10 個找到送出點與欄位；C_ALT_ATTACK、C_BOOK、C_SAVE 在 847 個呼叫點都沒有 | 見第 6 節 | C_PLATE／C_GOTO_MAP／C_BLINK／C_THROW 的 3.81 對應引擎已有；C_ONOFF（C26）缺 | C26 聊天頻道開關（code 任務 C） |
| S87 同身份入口 | TW16 物件建立封包是 **S_PUT_OBJECT（op 186）**；`A51920` 是套用物件記錄的共同入口，同 id 已存在就原地覆寫、重設移動狀態，不重建 | — | 引擎整筆取代記錄，結果大致相同 | 不另開任務，細節差異待驗證 |

## 2. S_EXTENDED 與 S250

### 2.1 S_EXTENDED（op 19／39／109）是 protobuf 通道（已驗證）

- 世界中分派（第一組，jump table 在 `0x94761D`）case 19／39／109 的區塊 `0x947D98`（FO `0x547198`）：`sub_926BF0` 讀一個 **H**（message id），再呼叫 `sub_E428F0`（取單例分派器，FO `0xA41CF0`）與 `sub_E42A50(資料, 長度−5, id)`（FO `0xA41E50`）。
- 登入期分派（第二組）：op 109 區塊 `0x947FC2` 只接受 id 13–14；op 19／39 區塊 `0x94801E` 另把 id 存進最近 5 筆歷史（`sub_9272C0`），再對 id 315–549 的 8 個值走同一個 protobuf 分派。
- 分派器建構子 `sub_E40290`（VA `0xE40290`／FO `0xA3F690`）以 `sub_E42840(id, handler)` 註冊 **125 個 id**：2–145（56 個）、315–586（66 個）、800、818、1000。
- id 的名稱表在 `sub_818140`（VA `0x818140`／FO `0x417540`，字串在 `.rdata` `0x1178644` 起）：例如 2＝`SC_SHOP_OPEN_ACK`、13＝`SC_SERVER_CONFIG_NOT`、47–49＝`SC_TEAM_BATTLE_ARENA_*`、55／57＝`SC_CRAFT_*_ACK`、101＝`SC_WAREHOUSE_INFO_NOT`、110＝`SC_SPELL_BUFF_NOTI`、118／119＝`SC_WORLD_PUT_NOTI`／`SC_WORLD_PUT_OBJECT_NOTI`、141／142＝`SC_NOTIFICATION_*`、145＝`SC_ALL_SPELL_PASSIVE_NOTI`。這張表只到 240；315 以上的名稱本輪沒找到（**待驗證**，不影響結論）。完整 id→handler 清單由 `t225_cases.py` 以外的步驟產生，見第 10 節。
- BIN 內嵌 `MainServer_Client.proto` 等 protobuf 描述（`.rdata` FO `0xD4EE2E` 附近），路徑字串含 `TaiwanClientDevStream\protobuf`。

**3.81 伺服器**：原始碼沒有任何 protobuf 依賴，`OpcodesServer` 也沒有對應 op。**結論：S_EXTENDED 整組不建議補。** gap-matrix 原本把它對到 S250 是錯的，已改正（第 1 節 op 19／39／109 列、op 108 列）。

### 2.2 S_EVENT（op 108）＝ S250 PACKETBOX（已驗證）

- 處理函式 `sub_9357E0`（VA `0x9357E0`／FO `0x534BE0`）：跳過 opcode 後 `sub_926C70` 讀一個 **C** 當子碼，進入 158 個 case 的 switch（兩組分派都指向它）。欄位讀取函式：`926C70`／`926B10`＝C、`926BF0`／`926CA0`＝H、`926B80`＝D、`926B40`＝Q（8 byte）、`926C40`＝S。
- 子碼語義與 3.81 `S_PacketBox` 常數一致，例：case 0／1／2 讀 D（城堡 id）並顯示字串 639／640／641（「%s的攻城戰開始。」「…結束。」「…正在進行中。」）＝`MSG_WAR_BEGIN／END／GOING`；case 0x4F 顯示 1511「攻城戰結束了」＋1513／1514 佔領血盟清單＝`MSG_WAR_OVER`（79）。
- 3.81 伺服器會送的子碼，由 `t225_s250.py` 掃描伺服器原始碼：寫 `C(S_OPCODE_PACKETBOX)` 後緊接的子碼常數，加上以參數傳子碼的類別（`S_PacketBox`、`S_PacketBoxWar`、`S_PacketBoxGame`、`S_IconConfig`）被呼叫時傳入的常數。結果 63 個；其中 255（`S_Classic*`／`S_BroadcastPos` 的自訂 0xFF 擴充）TW16 沒有，其餘 62 個 TW16 都有 case。
- 引擎處理的 S250 子碼（`engine/source` 搜尋）：5、10、11、17、18、19、20、21、22、34、35、36、40、41、42、44、57、104、105、106、110、111、154、171，共 24 個。17（`ADD_EXCLUDE2`）伺服器不會送。

下表是「伺服器會送、TW16 有 case」的 62 個子碼。「TW16 讀取」是 case 內直接讀的欄位（不含 opcode 與子碼；寫「另函式讀」的是交給別的函式讀，本輪沒有展開）；「字串」是 case 內直接取的 `string-c.tbl` id。

| 子碼 | 3.81 常數（類別） | TW16 讀取 | 字串 | 引擎 |
|---|---|---|---|---|
| 0／0x00 | MSG_WAR_BEGIN（S_PacketBoxWar） | `d` | 639 | 缺 |
| 1／0x01 | MSG_WAR_END（S_PacketBoxWar） | `d` | 640 | 缺 |
| 2／0x02 | MSG_WAR_GOING（S_PacketBoxWar） | `d` | 641 | 缺 |
| 3／0x03 | MSG_WAR_INITIATIVE（S_PacketBoxWar） | 無 | 642 | 缺 |
| 4／0x04 | MSG_WAR_OCCUPY（S_PacketBoxWar） | 無 | 643 | 缺 |
| 5／0x05 | MSG_DUEL（S_PacketBox） | `d` | 645 | `Duel.cs` |
| 10／0x0A | WEIGHT（S_PacketBox） | `c` | — | `BasicSupplies.cs` |
| 11／0x0B | FOOD（S_PacketBox） | `c` | — | `BasicSupplies.cs` |
| 12／0x0C | MSG_LEVEL_OVER（S_PacketBoxItemLv） | `ccc` | 672、673 | 缺 |
| 15／0x0F | MSG_ELF（S_PacketBox） | `c` | 679＋c | 缺 |
| 18／0x12 | ADD_EXCLUDE（S_PacketBox） | 另函式讀 | — | `Buddies.cs` |
| 19／0x13 | REM_EXCLUDE（S_PacketBox） | 另函式讀 | — | `Buddies.cs` |
| 20／0x14 | ICONS1（S_Icons、S_PacketBoxIcon1、S_PacketBoxActiveSpells） | `d`＋另函式 | — | `MagicStatus.cs` |
| 21／0x15 | ICONS2（S_PacketBoxIcon2） | `cccc` | — | `MagicStatus.cs` |
| 22／0x16 | ICON_AURA（S_PacketBox、S_PacketBoxIconAura） | `chhdhdhdhhhhhhhhhc` | — | `MagicStatus.cs` |
| 23／0x17 | MSG_TOWN_LEADER（S_PacketBox） | 另函式讀 | 764 | 缺 |
| 27／0x1B | MSG_RANK_CHANGED（S_PacketBox） | 另函式讀（`sub_B32AB0`） | — | 缺 |
| 30／0x1E | MSG_WIN_LASTAVARD（S_PacketBoxWar） | `dssd`（id、角色名、血盟名、城堡；FU5§2.2） | 782、783、919 | 缺 |
| 31／0x1F | MSG_FEEL_GOOD（S_PacketBoxHpMsg） | `d` | — | 缺 |
| 34／0x22 | ICON_BLUEPOTION（S_PacketBox） | `hc` | 79 | `MagicStatus.cs` |
| 35／0x23 | ICON_POLYMORPH（S_PacketBox） | `h` | — | `MagicStatus.cs` |
| 36／0x24 | ICON_CHATBAN（S_PacketBox） | `h` | — | `MagicStatus.cs` |
| 37／0x25 | GUI_PET_EQUIPMENT（S_PetEquipment） | 另函式讀 | — | 缺 |
| 40／0x28 | ICON_I2H（S_PacketBox） | `h` | — | `MagicStatus.cs` |
| 41／0x29 | CHARACTER_CONFIG（S_PacketBoxConfig） | `d`＋資料 | — | `CharacterConfig.cs` |
| 42／0x2A | LOGOUT（S_PacketBoxSelect） | 無 | — | `CharacterManagement.cs` |
| 44／0x2C | WIND_SHACKLE（S_PacketBoxWindShackle） | 另函式讀 | — | `MagicStatus.cs` |
| 45／0x2D | CALL_SOMETHING（S_PacketBoxGm） | `c` | — | 缺 |
| 52／0x34 | COOK_WINDOW（S_PacketBoxCooking） | `dcc` | — | 缺 |
| 53／0x35 | ICON_COOKING（S_PacketBoxCooking） | `cccccchcchc` | — | 缺 |
| 56／0x38 | DOLL（S_PacketBox） | `hhd` | — | 缺 |
| 57／0x39 | WISDOM_POTION（S_PacketBoxWisdomPotion） | `ch` | — | `MagicStatus.cs` |
| 59／0x3B | DEL_ICON（S_PacketBox）、S_PacketBoxWaterLife | 另函式讀 | — | 缺 |
| 60／0x3C | CAKE（S_PacketBoxThirdSpeed） | `cc` | — | 缺 |
| 66／0x42 | GAMEINFO（S_PacketBoxGame） | 另函式讀 | — | 缺 |
| 75／0x4B | （S_PacketBoxDk） | `c` | — | 缺 |
| 79／0x4F | MSG_WAR_OVER（S_PacketBoxWar） | `c` | 1511、1513、1514 | 缺 |
| 82／0x52 | LEAVES（S_PacketBox、S_PacketBoxExp） | `ddd` | — | 缺 |
| 83／0x53 | GLOBAL_EFFECT（S_GlobalEffect）、SECRETSTORY_GFX（S_PacketBoxGree） | `d` | — | 缺 |
| 84／0x54 | GREEN_MESSAGE（S_PacketBoxGree） | 另函式讀 | — | 缺 |
| 86／0x56 | BLUE_EXPREPLY（S_PacketBox） | `cchcccccccchcchcccddccccddds` | — | 缺 |
| 87／0x57 | （S_Karma） | `d` | — | 缺 |
| 104／0x68 | MSG_PARTY1（S_PacketBoxParty） | 另函式讀 | — | `Party.cs` |
| 105／0x69 | MSG_PARTY2（S_PacketBoxParty） | 另函式讀 | — | `Party.cs` |
| 106／0x6A | MSG_LEADER（S_PacketBoxParty） | 另函式讀 | — | `Party.cs` |
| 110／0x6E | MSG_PARTY3（S_PacketBoxParty） | 另函式讀 | — | `Party.cs` |
| 111／0x6F | SEND_LOC（S_PacketBoxLoc） | 另函式讀 | — | `LocationShare.cs` |
| 114／0x72 | （S_PacketBoxProtection） | `dd` | — | 缺 |
| 117／0x75 | HTML_CLAN_WARHOUSE_RECORD（S_PledgeWarehouseHistory） | `d` | — | 缺 |
| 132／0x84 | EVASION_UPDATE（S_PacketBox） | `c` | — | 缺 |
| 146／0x92 | APPRENTICE_HTML（S_PacketBox） | `ccc` | — | 缺 |
| 153／0x99 | MAP_LIMIT_TIMER（S_PacketBox） | `d` | — | 缺 |
| 154／0x9A | WEAPON_BUFF_ICON（S_PacketBox）、SKILL_ICON（S_IconConfig）、S_MagicEquipment | `hdh` | — | `MagicStatus.cs` |
| 159／0x9F | （S_PacketBoxMapTimer） | 另函式讀 | — | 缺 |
| 160／0xA0 | （S_WeaponRange） | `ccc` | — | 缺 |
| 168／0xA8 | HTML_PLEDGE_REALEASE_ANNOUNCE（S_PacketBox） | 另函式讀（`sub_B2EF40`） | — | 缺 |
| 169／0xA9 | HTML_PLEDGE_WRITE_NOTES（S_Pledge） | 另函式讀（`sub_B2EE50`） | — | 缺 |
| 171／0xAB | HTML_PLEDGE_ONLINE_MEMBERS（S_PacketBox） | 另函式讀 | — | `Clan.cs` |
| 173／0xAD | PLEDGE_EMBLEM_STATUS（S_PacketBox） | 另函式讀 | — | 缺 |
| 180／0xB4 | NONE_TIME_ICON（S_PacketBox） | 另函式讀 | — | 缺 |
| 190／0xBE | （S_ItemBoard） | `cc` | — | 缺 |
| 192／0xC0 | （S_ShowDrop） | `q` | — | 缺 |

TW16 另有 96 個子碼 3.81 伺服器不會送（6、9、13、14、16、17、32、33、38、43、47–51、55、58、61–65、67–74、76–78、80、85、88、100、101、107–109、112、115、116、118–121、125、127、129、134、138、141、142、144、147–151、156、158、161–167、170、172、174、176、178、179、181–185、187–189、191、193–202、204），**不建議補**。其中 0x21（33）是反外掛記憶體雜湊挑戰，見第 6 節 C_ADDR。

注意：3.81 有幾個類別寫的欄位寬度和 TW16 讀的不同（例：`S_GlobalEffect` 寫 `C(triggerNum)`，TW16 case 0x53 讀 `d`）。上表沒有逐一核對欄位寬度，code 任務實作時要以 [`server-protocol.md`](server-protocol.md) 的 3.81 寫法為準。

### 2.3 引擎缺的 39 個子碼，建議分組

| 組 | 子碼 | 內容 | 依賴 |
|---|---|---|---|
| B1 聊天欄系統訊息 | 0–4、30、79（攻城）、12、15、23、27、31 | 只需在聊天欄印字串表文字（字串 id 見上表） | code 任務 A（字串表） |
| B2 狀態圖示 | 53、56、59、60、75、114、132、153、159、180、160、82、87 | 料理／魔法娃娃／水中呼吸／三段加速／屬性防禦／迴避／地圖限時／經驗加成／友好度等圖示或數值 | 現有 `MagicStatus.cs` 圖示機制 |
| B3 視窗類 | 37、52、66、117、146、168、169、173、190、192、45、83、84、86 | 寵物裝備、料理視窗、遊戲資訊、血盟倉庫紀錄、師徒、血盟公告／筆記、徽章狀態、物品看板、掉落顯示、GM 呼叫、全畫面特效、綠字訊息、經驗回報 | 原生 UI（T190）；各自查版面後再開 |

## 3. S_MESSAGE_CODE（op 102）的字串表

**原版怎麼做（已驗證）**

- 處理函式 `sub_942B40`（VA `0x942B40`／FO `0x541F40`）讀 `hc`（訊息 id、參數個數），參數字串在其後；特殊 id（2533 地圖物件除錯、834、875、3387 等）另外處理。一般路徑：**id ≥ 字串表筆數就整包丟棄**（比較 `dword_153C998`），否則經 `sub_933850`（VA `0x933850`／FO `0x532C50`）把參數代入，送 `sub_833F20` 顯示。
- 字串表物件在 `.data` `0x153C998`（BSS；第一個 DWORD 是筆數，第二個是指標陣列），查表函式 `sub_4EAFF0`（VA `0x4EAFF0`／FO `0xEA3F0`）：越界時印「`%s dummy_%d`」並回空字串。
- 載入：`sub_A14550`（FO `0x613950`）最後呼叫 `sub_A121D0("string")`（FO `0x6115D0`），以格式 `"%s-%c.tbl"`（`.rdata` `0x118E690`／FO `0xD8D290`）組檔名，再由 `sub_A123D0` 讀入。語系字元由 `sub_A14A60`（FO `0x613E60`）依 CodePage 決定：932→`j`、936→`h`、949→`k`、**950→`c`**、其他→`e`。CodePage 來自 S_VERSION_CHECK 的語系 byte（第 4 節）。
- 參數格式（引擎 `ServerMessageText.cs` 已記錄，本輪再核對 `sub_933850`）：`%0`… 依序代入；`%s`／`%o` 只在 CP949 輸出。

**本機資源（已驗證）**：`Text.pak` 記錄 32559 是 `string-c.tbl`，payload FO `22783426`、長度 `105931`，解密後 SHA-256 `4ebbc1a7a002a4c1dcdfa73dbf056b0e00648e0789763410c40e5079a22d43e9`。第一行是筆數 **3484**，之後一行一筆、id 從 0 起。例：125＝「\f1你不能夠放棄此樣物品。」、189＝「\f1金幣不足。」（與引擎註解相同），1784＝「%s從 %s地區發送了座標位置情報。」、1785＝「請輸入要接受的角色名稱。」（WO 第 5 節待取的兩條）。

注意：TW16 BIN 會引用 4492、4536、4715 等 ≥3484 的 id，本機表裡沒有。依原版規則，這些 id 從伺服器來時會被丟棄、不顯示；引擎應照做，不要自行補字。

**3.81 伺服器**：S71 `S_ServerMessage` 只送 `H(id)`＋`C(個數)`＋字串參數，不帶文字，文字一律在客戶端查表。

**引擎現況**：`ServerMessageText.cs` 只硬寫 125、189、337、403 四個 id，其他顯示「原服訊息 {id}」。`MagicRendering.cs` 的 `EffectSpriteCatalog` 已有 Text.idx／Text.pak 的讀取與 DES 解碼，但只收 `list.spz`／`list.spr`。

**建議（code 任務 A）**：執行期唯讀載入 `string-c.tbl` 整張表（不落地、不複製進專案），以 `sub_933850` 的規則格式化；id ≥ 筆數時不顯示。檔案：`ServerMessageText.cs`、把 `MagicRendering.cs` 的 Text 封存讀取抽成共用類別、`ServerStatusMessages.cs`。這也解開 WO 第 5 節的 1784／1785、第 2.3 節 B1 組與 S_NOTICE 等需要文字的項目。

## 4. 版本檢查：C_VERSION（122）／S_VERSION_CHECK（107）↔ C14／S139

**原版怎麼做（已驗證）**

- 登入期分派 op 231（S_KEY）的處理函式 `sub_940750`（FO `0x53FB50`）在 `0x9408C1` 呼叫 `sub_8CBF80`（FO `0x4CB380`）送 **C_VERSION**：`sub_9FBC00("chdcddc", 122, 52, CodePage, FromCD, 雜湊, 參數, x)`，即欄位 `H(52) D(CodePage) C(登錄檔 FromCD) D(雜湊) D C`。
- S_VERSION_CHECK 處理函式 `sub_94DBB0`（VA `0x94DBB0`／FO `0x54CFB0`）：
  - 第一個 byte 為 1／2／3 時顯示字串 52「用戶端版本配合錯誤。請重新下載用戶端程式。」／45「發生錯誤因此無法再進行。」／3347「超過一台電腦裡可執行天堂程式的數量。」並 `PostQuitMessage` 結束。
  - 否則依序讀 `cdddddcc`、語系 `c`（0→949、3→950、4→932、5→936，其他→1252；932／936 與系統 CodePage 不合時顯示 558「無法連接此伺服器。」並結束，950 只在系統不是 950／1252 時改用 950）、`d`（`dword_13CE514`，伺服器功能旗標）、`d`（存到 `+460`）、再讀 3 個 `d`。
- 對照 3.81 `S_ServerVersion`：`C(0) C(SERVER_NO) D×4 D(0) C(0) C(0) C(CLIENT_LANGUAGE) D(0x77d82) D(UPTIME) H(1) C(0)`，欄位順序與 TW16 讀法一一對上，功能旗標＝`0x77d82`。

**3.81 伺服器是否要求（已驗證）**：`C_ServerVersion.start` 只讀包並回 `S_ServerVersion`，不記狀態；`SessionPacketPolicy` 把 C14 歸 `TRANSPORT`（任何階段都允許），`LOGINPACKET` 在 `CONNECTED` 階段就允許，**沒有「必須先送 C14」的檢查**。引擎目前不送 C14、不收 S139，#23 實機登入正常，與此一致。

**功能旗標的兩個用途（已驗證，供參考）**：

- `0x800000`：連續攻擊 C_ATTACK_CONTINUE 開關（第 6 節）。3.81 的 `0x77d82` **沒有**這一位，原版在 3.81 下不會送連續攻擊。
- `0x10000`：拾取節流，`sub_5001C0` 內 C_GET 間隔 986 ms，旗標開時 600 ms。3.81 的 `0x77d82` **有**這一位，所以原版接 3.81 時拾取節流是 **600 ms**（補 WO 第 2 節 T103 的條件）。

**建議**：不必為了登入補 C14／S139。若要讓拾取節流等行為跟著伺服器旗標走，可開低優先任務：送 C14、解析 S139，把 `CLIENT_LANGUAGE` 與功能旗標存進 session（`LoginConnection.cs`、`LoginProtocol.cs`）。

## 5. 未知收包

| TW16 op／名稱 | 處理函式 VA／FO | 原版怎麼做（已驗證） | 3.81 對應 | 引擎 | 建議 |
|---|---|---|---|---|---|
| 189 S_BLINK | `sub_931B90`／`0x530F90` | **不讀欄位**。若自己正在移動（`sub_A43510`），記下待辦 3，等這一步走完由 `sub_A48590`（FO `0x647990`）送；否則立刻送 **C_BLINK（139，無欄位）**，設傳送中旗標、`sub_9E3460(8)` | S149 TELEPORT（`S_Teleport`）→ 客戶端回 C52（推定，行為與欄位都吻合） | `Teleport.cs` 收 S149 後送 C52，已有 | 差異：原版在移動中會延後到該步結束才回確認；引擎立即回。只記錄，不另開 |
| 250 S_HIT_RATIO | `sub_93FAD0`／`0x53EED0` | 讀 `d`（物件）`c`（HP 比，>100 視為 100，−1 隱藏）`c`（MP 比），更新頭上條與隊伍名單；另在 `dword_1656930` 視窗條件成立時送一次 C_DIALOG（51）`d` | S237 HPMETER `D(objId) C(hpRatio)`（推定；3.81 沒有 MP 比） | `Combat.cs`、`WorldSession.cs`（`ReceivePartyHp`）已有 | 無 |
| 132 S_SHOW_MAP | `sub_94BFC0`／`0x54B3C0` | 讀 `d`（背包物品 objid）`c`（頁），在背包找到該物品（`sub_990D90`）後，用 `sub_A59AB0`→`sub_A57C60` 開 `dword_1656770` 視窗（推定為地圖視窗）並設為第 c−1 頁 | S187 HOUSEMAP `D(objectId) D(number)`（推定；寬度不同，但 little-endian 下第一個 byte 相同） | 無 | 低優先 code 任務 D（需地圖視窗） |
| 172 S_CLONE | `sub_933640`／`0x532A40` | 讀 `ddhhcs`：物件、第二個 D（存 `+212`）、`h`（`sub_A450C0`）、`h c`（交給變身函式 `sub_A43B50`／`sub_A3C930`，與 S_POLYMORPH 的佇列處理相同）、名稱 | S76 POLY 的 `S_ChangeShape(pc, npc, polyId)` 型 `D D H S`（推定；TW16 多 `h c`） | `Polymorph.cs` 兩種 S76 都解析，已有 | 無 |
| 108 S_EVENT | `sub_9357E0`／`0x534BE0` | 見第 2.2 節 | S250 | 部分 | 第 2.3 節 |
| 212 S_NOTICE | `sub_ECABC0`／`0xAC9FC0` | 登入期。讀 `h`（字串 id），以字串表文字開模態訊息框 `sub_8E1E30`；273–275 加上依語系的網址字串、307／437 帶剩餘點數時間、317 先 `Sleep(3000)`、438 改用另一種按鈕 | 無（3.81 用 S233 LoginResult 的 reason 與 S48 CommonNews） | 無 | **不建議補** |

## 6. 未知送包

| TW16 送包（op） | 送出點 VA（所在函式） | 欄位 | 原版用途（已驗證） | 3.81 對應 | 引擎 | 建議 |
|---|---|---|---|---|---|---|
| C_REGISTER_QUIZ（100） | `0xEC040C`／FO `0xABF80C`（登入期視窗，函式未被 IDA 切出） | `sssssss` | 帳號安全問答登錄：檢查問題 ≥5 字（字串 345）、答案 ≥2 字（346）後送出 | 無 | 無 | **不建議補** |
| C_WISH（254） | `sub_F43C50`（FO `0xB43050`）、`sub_F4AC90`（FO `0xB4A090`）、`sub_F4B1E0` | `s` | GM／建造者文字指令：`sub_F4AC90` 解析 `agit`、`allspell`、`mortal`、`level` 等，沒對上的整串以 C_WISH 送；`sub_F43C50` 送「`bot …`」 | 無（3.81 GM 指令走聊天 `.` 前綴） | 無 | **不建議補** |
| C_THROW（146） | `sub_E65D30`（FO `0xA65130`）、`sub_F07030`（FO `0xB06430`） | `c(1)` | 回呼內條件（`sub_A58380`）成立後結束釣魚：送出後顯示 1163「釣魚已經結束了。」 | C62 FISHCLICK（推定） | `Fishing.cs` 送 C62，已有 | 無 |
| C_BLINK（139） | `sub_931B90`、`sub_A48590` | 無 | S_BLINK 的確認（第 5 節） | C52 | `Teleport.cs`，已有 | 無 |
| C_ONOFF（184） | `sub_6BBD40`（FO `0x2BB140`）、`sub_99B430`（FO `0x59A830`）、`sub_F47FD0`（FO `0xB473D0`）等 29 處 | `cc`（類型、值） | 聊天頻道與拒收開關：選項視窗與 `on`／`off`（字串 401「開」／402「關」）指令；`sub_99B430` 一次送 255＋合併位元，再送 6、9、10 | **C26**（`C_LoginToServerOK`）`C(type) C(button)`：只處理 255（合併位元 90–95、122–127）、0 全體、2 密語、6 買賣 | 無 | **code 任務 C** |
| C_ADDR（64） | S_EVENT case 0x21，`0x938E75`／FO `0x538275` | `ddd` | 反外掛：伺服器給 `d d d`，客戶端對自身記憶體區段算雜湊（`sub_933BE0`）回傳 | 無（3.81 不送子碼 33） | 無 | **不建議補** |
| C_PLATE（121） | `sub_5001C0`（FO `0xFF5C0`） | `d`（物件） | 點到類型 14 的物件或 gfx 10445–10447 時送（推定為看板） | C10 BOARD `D`（推定） | `Board.cs` 送 C10，已有 | 無 |
| C_TELEPORT_USER（31） | `sub_A17380`（FO `0x616780`）、`sub_F49D40`（FO `0xB49140`） | `s`（角色名） | GM 傳送清單／指令：名稱項目送 C_TELEPORT_USER＋C_WHO，座標項目送 C_TELEPORT `hhh` | 無 | 無 | **不建議補** |
| C_GOTO_MAP（60） | `sub_9E9C90`（FO `0x5E9090`）、`sub_782370`（FO `0x381770`）；`sub_947140`（S_PORTAL op 202 處理） | `hhh`（地圖、x、y）；`h(0xFFFF)` | 船班抵達時依目前船圖送目的地圖與隨機下船座標；S_PORTAL 收到船圖轉換但沒有船票（檢查 `$314` 等物品）時送 0xFFFF 拒絕 | C231 SHIP `H H H` | `ShipTravel.cs` 送 C231，已有 | 0xFFFF 拒絕分支 3.81 不讀，不補 |
| C_ALT_ATTACK（96） | **找不到** | — | 847 個 `sub_9FBC00` 呼叫點都沒有 opcode 96；攻擊共用函式 `sub_A44FD0`（FO `0x6443D0`）只被傳入 43（C_ATTACK）與 4（C_FAR_ATTACK） | — | — | 推定未使用；**待驗證**是否經其他送出函式 |
| C_ATTACK_CONTINUE（130） | `sub_A3DE30`、`sub_A49600`、`sub_F02BA0`、`sub_A4C270`（開始，`d`＝目標）；`sub_78ECC0`、`sub_A4A020`、`sub_EFE700`、`sub_F112F0`、`sub_8C14F0`（停止，`d`＝0 或 −1） | `d` | 伺服器端連續攻擊，只在功能旗標 `0x800000` 開時送 | 無；3.81 旗標 `0x77d82` 沒開這位 | 無 | **不建議補** |
| C_BOOK（126） | **找不到** | — | 同上，沒有呼叫點 | — | — | **待驗證** |
| C_SAVE（150） | **找不到** | — | 同上，沒有呼叫點 | — | — | **待驗證** |

另 5 個 opcode 不是立即值的呼叫點（`0x7E5E97`、`0x7E5FF8` 聊天送出、`0xA45055` 攻擊、`0xDD94F7`、`0xDD97F8` 兩個以 `0xFD`／`0xBE`、89／210 組出的新系統封包），都不是 96／126／150。

## 7. S87 同身份入口（`sub_A51920` 的 6 個呼叫點）

**原版怎麼做（已驗證）**

- TW16 的物件建立封包是 **S_PUT_OBJECT（op 186）**，處理函式 `sub_948A60`（FO `0x547E60`），格式 `hhdhccccdhsscdsscccc`＋後續欄位，與 WO 第 0 節一致。它把欄位交給 `sub_9262D0`（FO `0x5256D0`）組成一筆物件記錄，排進佇列。
- `sub_A51920`（VA `0xA51920`／FO `0x650D20`）是套用這筆記錄的共同入口：
  1. 記錄的物件 id＝自己（`dword_15442C8`）時，先更新地圖、亮度與自己的狀態。
  2. 以 `sub_A4A230(id)` 找現有物件。**已存在**：清掉移動進度（`+32`／`+36`）、重新定位（`sub_A0BBE0`）、`+324` 有值時呼叫 `sub_A44980`，保留同一個 actor；**不存在**：`sub_A504A0` 新建。
  3. 之後兩條路共用：覆寫動作、方向、亮度、名稱、稱號、血盟、正義值、狀態旗標等全部欄位，動作為 8（死亡）時另更新隊伍名單（`sub_926AB0`）。
- 6 個呼叫點：

| 呼叫點 VA／FO | 所在函式 | 情境 |
|---|---|---|
| `0xA4C043`／`0x64B443` | `sub_A4BF00` | 物件延後訊息佇列，case 181（訊息類型 186−5＝S_PUT_OBJECT） |
| `0xA4E846`／`0x64DC46` | `sub_A4E5D0` | 地圖特效物件建立（REA B04） |
| `0xA50D83`／`0x650183` | `0xA50D20` 起的未切函式 | 佇列消化：物件不在畫面時直接套用 |
| `0xA50E23`／`0x650223` | 同上 | 佇列中有 S_ACTION（0xE2）且值為 8 時的分支（推定） |
| `0xA5104B`／`0x65044B`、`0xA51171`／`0x650571` | `sub_A50F20` | S_PUT_OBJECT 佇列的主要消化函式（含是否在畫面、是否變身中 `sub_A3EBF0` 的分支） |

**3.81**：S87 CHARPACK／DROPITEM。**引擎**：`WorldObjects.cs` `ReceiveObject` 以新記錄整筆取代同 id 的舊記錄（保留死亡狀態），與原版「全部欄位覆寫、移動進度歸零」的結果大致相同。差異只在原版保留同一個 actor（動畫播放位置等），引擎沒有 actor 物件的概念；是否有可見差異需實機比較（**待驗證**），本輪不建議另開任務。

## 8. 可以開的 code 任務與仍查不到的項目

**可以開 code 任務**

| 代號 | 目標 | 檔案 | 優先 |
|---|---|---|---|
| A | S_MESSAGE_CODE 載入整張 `string-c.tbl`，依 `sub_933850` 格式化；id ≥ 筆數不顯示 | `ServerMessageText.cs`、`ServerStatusMessages.cs`、從 `MagicRendering.cs` 抽出 Text 封存讀取 | 高 |
| B1 | S250 聊天訊息類子碼 0–4、12、15、23、27、30、31、79 | `WorldSession.cs`／新 `PacketBoxMessages.cs` | 中（依賴 A） |
| B2 | S250 狀態圖示類子碼 53、56、59、60、75、82、87、114、132、153、159、160、180 | `MagicStatus.cs` | 中 |
| C | C26 聊天頻道開關（255 合併位元、0、2、6），對應原版 C_ONOFF 的選項與 `on`／`off` 指令 | `Chat*.cs`、`CharacterConfig.cs` | 中 |
| D | S187 HouseMap（原版 S_SHOW_MAP）開地圖視窗 | 需原生 UI（T190）之後 | 低 |
| E | 送 C14、解析 S139，保存語系與功能旗標（例：拾取節流 600 ms） | `LoginConnection.cs`、`LoginProtocol.cs` | 低 |

B3（視窗類 14 個子碼）要等各視窗查版面，本輪不開。→ T245（#602）已逐一查完，見 [`packetbox-windows.md`](packetbox-windows.md)：66、146 伺服器沒有呼叫點，86 只有測試指令會送，190／192 是伺服器自訂格式；可開 PBW-A–E。

**不建議補（3.81 伺服器不支援或不需要）**：S_EXTENDED 整組（protobuf）、S_EVENT 中 96 個伺服器不送的子碼、S_NOTICE、C_REGISTER_QUIZ、C_WISH、C_TELEPORT_USER、C_ADDR、C_ATTACK_CONTINUE。

**仍查不到**：C_ALT_ATTACK、C_BOOK、C_SAVE 的送出點（847 個 `sub_9FBC00` 呼叫點都沒有）；protobuf id 315 以上的名稱；第 2.2 節「另函式讀」各子碼的完整欄位；S87 同身份路徑與引擎整筆取代的可見差異。

## 9. 限制

- 全部是靜態分析，沒有實機觀察。原版以 TW1608262200 為準。
- 3.81 子碼清單由原始碼正規式掃描產生（`t225_s250.py`），可能漏掉以運算式組出的子碼；`S_PacketBoxTest`、`ServerBasePacketConcurrencyProbe`、`S_Classic*` 是測試或自訂類別，已排除在比對之外。
- S_EVENT 的 case 切分以 Hex-Rays 偽碼為準（`t225_cases.py`），讀取欄位只算 case 內直接呼叫的讀取函式。
- 「3.81 對應」寫「推定」的，是依名稱、欄位與行為推論，沒有實機對包。
- 字串表內容來自本機客戶端的 `Text.pak`，與 TW16 BIN 同捆的版本可能不同（TW16 引用到 ≥3484 的 id）。

## 10. 可重跑腳本（`docs/reference/rea-scripts/`）

| 腳本 | 用途 | 執行 |
|---|---|---|
| `ida_t225_sends.py` | IDAPython：列出全部 `sub_9FBC00` 呼叫點的 opcode 與格式字串（`sends.json`） | `run_ida.ps1 -Script ida_t225_sends.py -List lists/dummy.txt -Out sends -Drive <未占用代號>` |
| `ida_dis.py`（既有） | 反編譯本文引用的函式（`dec`／`dis`／`xdec`） | 同上，`-Script ida_dis.py -List <清單>` |
| `t225_cases.py` | 把偽碼的頂層 switch 切成 case，列出讀取欄位、字串 id、呼叫 | `py -3 -I t225_cases.py <out>/009357E0.c "switch ( v657 )"` |
| `t225_s250.py` | 掃描 3.81 伺服器原始碼會送的 S250 子碼 | `py -3 -I t225_s250.py "D:\lineage開發\1-伺服器\src" scratch/t225/s250_server.json` |
| `t225_cmp.py`、`t225_table.py` | TW16 case／伺服器子碼／引擎子碼三方比對，產生第 2.2 節表格 | `py -3 -I t225_table.py s_event_cases.txt s250_server.json` |
| `t225_strings.py` | 記憶體內解 `Text.pak` 的 `string-c.tbl`，只印指定 id（輸出放 `scratch/`） | `py -3 -I t225_strings.py "D:\lineage開發\0-客戶端" scratch/t225/strings.json 639 640 …` |

protobuf id→handler 對照：從 `sub_E40290` 偽碼以正規式 `sub_E3E380\((sub_\w+)\);…sub_E42840\((\d+),` 取出，再以 `sub_818140` 的 `sub_8195D0(id, "名稱")` 對名稱。
