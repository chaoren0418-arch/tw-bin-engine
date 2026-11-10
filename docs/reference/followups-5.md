# 查證補遺第 5 輪：名稱旁圖示、S250 27／30／31、光效 +5、物品欄位 +36

T289（#690）。查 [`gap-matrix.md`](gap-matrix.md) 第 12.6 節排到下一輪的 **Q3-2**、**Q3-7**，以及 T287（[`followups-4.md`](followups-4.md)，FU4）新增的 **Q4-1**、**Q4-2**；順便核對本機有沒有 surf 9183。簡稱：本文 **FU5**，引用寫 `FU5§n`。

每節分「原版怎麼做／3.81 是否支援／對應要改哪個檔案」。記號：**已驗證**＝反編譯、反組譯、原始碼或資源檔直接看到；**推測**＝依欄位、命名或旁證判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 方法與來源

- **TW16 BIN**：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- **TW16 IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/rea/ida/tw16.idb`，複製前後 SHA-256 都是 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`。唯讀批次只改副本（`mkfunc` 在副本補建 `sub_EFF300`）。
- IDA 7.7＋Hex-Rays，唯讀批次 14 輪（q1–q14），腳本與查詢清單在 `rea-scripts/t690/`（第 6 節）。
- **subst**：每輪前後都跑 `subst`，開始時沒有被占用的代號；本輪一律用 `W:`，每輪結束後 `subst` 都沒有輸出（沒有殘留）。
- **地址換算**：程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`（同 FU4§0）。bss 全域沒有 FO。
- **本機資源（只讀，不進 git）**：`D:\lineage開發\0-客戶端` 的 `Text.pak`（在記憶體解出 `string-c.tbl`，`decoded_sha256` `4ebbc1a7…43e9`，3484 筆）與 `Sprite*.idx`／`Tile.idx` 的檔名清單。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器`，唯讀）：`S_PacketBox.java`、`S_PacketBoxWar.java`、`S_PacketBoxHpMsg.java`、`S_GlobalEffect.java`、`S_RangeSkill.java`、`S_OtherCharPacks.java`、`S_NPCPack.java`、`ServerBasePacket.java`（補零到 8 的倍數，第 346 行）、`L1Clan.java`、`L1SkillUse.java`、`db/380_db.sql`。
- **引擎**：本分支 `engine/source`，只讀，不建置、不執行。
- 全程靜態分析。沒有啟動遊戲、伺服器或 DB；沒有改產品程式；沒有任何 BIN、IDB、pak 進 git。

## 1. Q3-2 名稱旁圖示與怪物等級差外框

### 1.1 原版怎麼做（已驗證）

滑鼠指到物件時的文字由 `sub_EFF300`（VA `0xEFF300`／FO `0xAFE700`，IDA 沒有自動切出，副本用 `mkfunc EFF300 F00195` 補建）畫；盟徽與 +181 圖示在每幀主繪 `sub_F0F0C0`（FO `0xB0E4C0`）的指到物件區塊畫。圖的來源有兩種：

- **surf**：`sub_E538D0(id)`（FO `0xA52CD0`，`this`＝`unk_1542680`）取 `<id>.img`，找不到就跳「Surf[%d] Error.」並 `PostQuitMessage`；`sub_542370` 畫在 (x, y)。
- **圖示（icon）**：`sub_88CE60(id)`（FO `0x48C260`，`this`＝`dword_13CE0A4`）第一次用時由 `sub_87B3A0`（FO `0x47A7A0`）讀 `<id>.tbt`，失敗記「Icon Resource Error[%d]」並退回第 0 個圖示；`sub_97B500` 用調色盤 `unk_153E240+1024` 畫。

| 圖示 | 條件 | 圖 | 位置 | 證據 |
|---|---|---|---|---|
| **+181 圖示** | 選項 `byte_135C1EB` 開，且物件 `+181` 為 1 或 2；**只在物件有血盟 id（`+212`）而且 `+604 == −1` 時才會呼叫**（`sub_F0F0C0` 第 260–271 行） | icon **5199**（`push 144Fh`，`0xF01056`）；1 與 2 用同一張 | 盟徽位置 X −10（有血盟）／+10（沒有血盟，這個呼叫者走不到）；Y 同盟徽 | `sub_F00FD0`（VA `0xF00FD0`／FO `0xB003D0`） |
| **隊伍圖示** | 物件在隊伍名單（`sub_953BC0` 回傳索引 ≠ −1），而且索引 > 0 或是隊長 | 隊長 surf **8264**；隊員查一張 `map<索引, surf>`（`sub_9552F0`），查不到用 **2826** | 名稱 X ＋ 名稱寬 ＋ 2，Y 同名稱 | `sub_957A80`（FO `0x556E80`）→ `sub_957D00` → `sub_9551E0`（FO `0x5545E0`） |
| 旗標圖示 | `dword_13CE514 & 0x40000000` 且 `+596 ≠ 0` | surf **5765**（`push 1685h`，`0xF000AA`） | 名稱 X ＋ 寬 ＋ 2 | `sub_EFF300` |
| **主人圖示** | 地圖 ≠ 509、`+196 == 0`、有稱號（`+240`）、vtable `+56` 為假、不是職業外型（`sub_8CA110`）、有主人名（`+248`） | surf **773**（`push 305h`，`0xF00177`） | 名稱 X ＋ 寬 ＋ 7，Y 同名稱 | `sub_EFF300` 結尾 |

- AN§2.3 寫的 `sub_E538D0(133)`／`(5)` 是 Hex-Rays 把 `thiscall` 參數截成低位 byte，實際值以反組譯為準（133＝`0x85`→`0x1685`，5＝`0x05`→`0x305`）。
- **+181 的寫入點**（`stores B5` 全程式掃描，只有 4 處）：S250 **子碼 148**（`0x93D6D7`／FO `0x53CAD7`，`d c`：物件 id、值）；物件建立 `sub_A50F20`、`sub_A4E1C0` 從 S_PUT_OBJECT 紀錄 `+200` 複製；基底建構子清 0。S_PUT_OBJECT（op 186，`sub_948A60`／FO `0x547E60`）讀 `hhdhccccdhsscdsscccc` 之後再讀 `S`、`H`、`C`，最後這個 `C` 經 `sub_9262D0` 第 30 個參數寫進紀錄 `+200`。
- **選項 `byte_135C1EB`**：`sub_583200`（FO `0x182600`）切換後重開 HTML 頁 `option11`；`sub_59DD30`、`sub_89A0F0`、`sub_999620` 從選項／設定讀回。是使用者選項（推測：`option11` 頁的某一項，頁面文字本輪沒對）。
- **怪物等級差外框的「選項」不是使用者選項**：`byte_13CE3FE` 只在啟動函式 `sub_8BA680`（`0x8BA7E0`／FO `0x4B9BE0` 起）依 `GetACP()` 設定：950（繁中）＝**3**、936（簡中）＝5、1252＝1、932（日文）＝4、其他 0。外框條件是 `byte_13CE3FE ∈ {1, 5}` 而且物件種類 10（`sub_EFF300` 第 206 行），所以**台版（950）永遠不畫等級差外框**。外框色 palette[3]／[16]／[1]／[22] 與門檻（≥10、3–9、−1–2、≤−2 而且不低於 `−自身等級/2 − 4`）同 AN§2.4。

### 1.2 3.81 是否支援

- **+181**：3.81 不送 S250 148（`S_PacketBox` 沒有 148 的常數；伺服器裡的 148 只有 `S_OPCODE_BOARDREAD` 與技能 `FIRE_WEAPON`）。S87 的尾端：PC（`S_OtherCharPacks`）在 4 個 `c` 之後是 `00 00 FF FF`，TW16 依序讀成 `S`＝空字串、`H`＝`0xFF00`、`C`＝`0xFF`（−1）；NPC（`S_NPCPack`）是 `00 FF FF`＋補零，最後的 `C` 落在補零＝0。兩者都不是 1／2，**3.81 永遠不畫 +181 圖示**（PC 已驗證；NPC 依 `ServerBasePacket` 補零推測）。引擎 `NameMarksFor` 把 +181 留空是對的。
- **隊伍圖示**：3.81 會送隊伍名單（引擎有 `partyMembers`）。本機 `Sprite13.idx` 有 `2826.img`；**`8264.img` 本機沒有**（只有 `Sprite12.idx` 的 `8264.tbt`）。原版缺 surf 會跳錯誤並結束程式，引擎要改成缺圖不畫。
- **主人圖示**：3.81 S87 的「主人名稱」欄（`writeS(null)`）一律空字串，主人名由 S_MASTER 另外送；本機 `Sprite.idx` 有 `773.img`。
- **旗標圖示**：`dword_13CE514 & 0x40000000` 與 `+596` 本輪沒追；`5765.img` 本機沒有。不建議做。
- **等級差外框**：台版不畫（已驗證）。3.81 S_NPCPack 雖然送了等級，但跟台版客戶端的行為無關。

### 1.3 對應要改的檔案

- `engine/source/ObjectRendering.cs`
  - `PaintNames` 依 `plan.Icons` 畫：`party`＝隊長 surf 8264、隊員 surf 2826（缺圖不畫）；`master`＝surf 773。位置維持現在的 `PartyIconDx=2`、`MasterIconDx=7`，改成已驗證（FU5§1）。主人圖示補「地圖 ≠ 509」條件。
  - `mark181-*` 在 3.81 永遠不會出現：保留空值，註解改成「icon 5199（`.tbt`），3.81 不送 S250/148，S87 尾端也不會是 1／2（FU5§1）」。
  - `MonsterLevelOption`：台版固定不畫。預設值改成 3（`GetACP()==950`），註解寫「不是使用者選項，是 `byte_13CE3FE` 地區碼，只有 1／5 才畫（FU5§1）」，**不做選項 UI**。
- 隊員圖示的 `map<索引, surf>` 由誰填本輪沒追（待驗證）；先一律用 2826。

## 2. Q3-7 S250 子碼 27／30／31 的顯示

處理函式 `sub_9357E0`（VA `0x9357E0`／FO `0x534BE0`）。三個子碼都**印在聊天欄**：`sub_7DC850(文字, palette[24], palette[14], −1, −1, 0)`（FO `0x3DBC50`）或 `sub_7DC800(id, palette[24], palette[14])`（FO `0x3DBC00`，查字串 id 後以類型 7 印出）。文字色是第 1 個色參數 palette[24]＝`0x7FD6`（RGB555，約 RGB 255, 247, 181，淡黃）；第 2 個色不被讀（FU4§7）。**沒有視窗、沒有畫面中央紅字。**

### 2.1 子碼 27 階級變更（已驗證）

- case 27（`0x938C5F`／FO `0x53805F`）讀 `cs`（階級、名稱），交給血盟管理器的 `sub_B32AB0`（VA `0xB32AB0`／FO `0x731EB0`）。
- 階級 < 2 或 > 13：**直接返回，什麼都不做**。
- 先在血盟成員清單找同名的人，更新他的階級欄（`+14`＝`sub_B34610(階級)`）；最後重繪血盟視窗（`sub_B30240`）。
- 名稱等於自己：自己 `+535`＝階級，印字串 **784**「你的階級變更為%s」；名稱是別人：印 **2125**「把%s任命為%s。」（名稱、階級名）。
- 階級名：2、7＝773「一般」；3＝1878「副君主」；4、10＝771「聯盟君主」；5、8＝1879「修習騎士」；6、9＝772「守護騎士」；12、13＝3503（本機表只有 3484 筆，查不到）。**11＝不印字**，只把自己 `+535` 設成 11（不比對名稱）。

### 2.2 子碼 30 打敗反王（已驗證）

- case 30（`0x938ACF`／FO `0x537ECF`）讀 `d s s d`：id、**第 1 個字串＝角色名**、**第 2 個字串＝血盟名**、城堡編號（protocol-gaps 第 2.2 節寫的 `dd` 要改）。
- 城堡編號 ≠ 8 時印 **782**「%s 血盟的 %s打敗了反王 」（血盟名、角色名）。
- 接著一律印 **783**「%s 血盟成為新主人」（血盟名）。程式另外傳了城名（1–12＝字串 912＋編號，其他＝919「亞丁城」），但 783 只有一個 `%s`，城名不會出現。

### 2.3 子碼 31 舒服多了（已驗證）

- case 31（`0x938CA2`／FO `0x5380A2`）讀 `d`（物件 id）。找得到物件時先印 **284**「%0%s 對你施法。」（物件名稱）。
- 一律再印 **77**「\f1你覺得舒服多了。」（`sub_7DC800(77, …)`；`\f1` 是換色碼）。
- 設 `byte_13D4401 = 1`：下次自己的物件建立（`sub_A50F20`／`sub_A4E1C0`，物件 id＝自己）時在該位置放一個 gfx **831** 的特效，然後清旗標（推測：用在傳送後）。

### 2.4 3.81 是否支援

- 3.81 的封包會補零到 8 的倍數（`ServerBasePacket.java:346`），TW16 讀超過的欄位會讀到 0。
- **27**：`C_PledgeRecommendation` 送三參數形式 `C(階級) S(名稱)`，和 TW16 一致 → 印 784／2125。`C_Rank` 用兩參數 `S_PacketBox(27, rank)`，那個建構子沒有 27 的 case，只寫子碼 → TW16 讀到階級 0 → 不顯示（已驗證）。`C_BanClan` 送 `(27, 11, "")` → 不印字。3.81 的階級常數 3 是 `CLAN_RANK_GUARDIAN`，台版字串是「副君主」；顯示一律照台版字串表。
- **30**：3.81 只有建構子 `S_PacketBoxWar(subCode, id, name, clanName)`，**沒有任何呼叫者**。若送了，`D S S` 後面讀到補零 → 城堡 0 → 兩行都印。
- **31**：`S_PacketBoxHpMsg()` 只寫子碼（補血道具、NPC 補血、釣魚），TW16 讀到 id 0 → 找不到物件 → 只印 77，並設 gfx 831 旗標。

### 2.5 對應要改的檔案

- `engine/source/PacketBoxMessages.cs`：
  - 27：有欄位時照 2.1 印 784（自己）或 2125（別人）＋階級名；階級 < 2、> 13 或 11 不印；不帶欄位的形式不印。更新自己的血盟階級（`ClanState` 之類的既有欄位，有的話）。
  - 30：印 782（城堡 ≠ 8 時）與 783，參數順序是（血盟、角色）。3.81 不會送，可以只寫測試。
  - 31：印 77。284 在 3.81 不會出現（id 0）。gfx 831 的「下次自己出現時播」列為選配（`WorldObjects`／`MapEffects` 的既有訂閱，推測用在傳送後，待真機驗證）。
  - 顏色：S250 這幾行用 palette[24] 淡黃（和其他 B1 子碼同一條路，現在 `playerChatLines` 的色碼 0 要確認對應）。
  - 檔頭表格 27／30／31 三列從「只存資料」改成「聊天欄」，`PacketBoxRankChange`／`PacketBoxLastavard`／`FeelGoodCount` 可以保留給測試。
- `docs/reference/protocol-gaps.md` 第 2.2 節 30 列的欄位 `dd` → `dssd`（下一輪順手改）。

## 3. Q4-1 光效 +5 的實際觸發

### 3.1 FU4§3 的更正（已驗證）

FU4§3 把 `+12` 當成「建構後要另外設成 1／2 的狀態」，並說 `sub_8ABC10` 是 vtable `+24`。兩點都要更正：

- **`+12` 是影格號**。`sub_A43560`（VA `0xA43560`／FO `0x642960`）每格 `++*(this+12)`，到該動作的影格數就歸 0 並回傳 0（播完）；影格表以 `36 × +12` 取每格資料。[`trade-window-and-projectiles.md`](trade-window-and-projectiles.md) §3.2 也已記為「影格號 `+12` 加 1」。
- **`sub_8ABC10` 是 `Effect` vtable `+144`**（`Effect::vftable` 在 `0x117EC54`／FO `0xD7D854`，`0x117ECE4` − `0x117EC54` ＝ 144），是每格的推進函式：`sub_8AE600` 計時回呼呼叫 vtable `+144`。開始播放（`+24`）是 `sub_8ACD20`（FO `0x4AC120`），它最後呼叫 `+148`（`sub_8AE440`，效能判斷後開始推進）。
- 所以條件實際是：**`Effect` 的 `+9 ≠ 0`，播到第 1 格時計數加 1，第一次加到 1 時 L＝min(15, L+5)；播完時減 1，歸 0 時還原 L**。
- **`Lightning`**（kind 2，vtable `0x117EBBC`／FO `0xD7D7BC`，建構子 `sub_8AB900`／FO `0x4AAD00`）的推進 `sub_8ACB20`（FO `0x4ABF20`）**不看 `+9`**：播到第 2 格時加 1、播完減 1。所以**每個 kind 2 特效都會提亮**。
- `ManyEffect`（vtable `0x1150CBC`／FO `0xD4F8BC`）的 `+144` 也是 `sub_8ABC10`（`0x1150D4C`）。
- 原版小瑕疵：影格數不到 2（`Effect`）或 3（`Lightning`）時不會加 1，但播完照樣減 1，計數可能變負，之後用舊的 L 還原。

### 3.2 哪些來源建出 `+9 ≠ 0` 的特效（已驗證，37 個 `sub_8AB390` 呼叫者＋`sub_8AB710`、`sub_8AB900`、`sub_A507A0` 全部看過）

| 來源 | 函式 VA／FO | `+9` | 3.81 會不會發生 |
|---|---|---|---|
| S_EFFECT_LOC（op 219＝3.81 **S106**） | `sub_934530`／`0x533930` | gfx **2028–2056** 時 1，其他 0 | 會，只要 S106 的 gfx 在這個範圍。3.81 送 S106 的地方是魔杖、陷阱、NPC 動作、`L1ILL`（5524）、`L1Chaser`（7025）；gfx 多半來自資料表，哪些落在 2028–2056 沒逐筆查（待驗證） |
| S_ATTACK_MANY（op 137＝3.81 **S42**） | `sub_92FE40`／`0x52F240`；解析在 `0x92FEF1` | `cdhhcdhccs` 的**第 8 欄（gfx 後的 `c`）**＝S42 的 `type` | **會**：`L1SkillUse` 的有方向範圍魔法送 `TYPE_DIR`＝**8**（第 2369、2476 行），kind 0／1 的 gfx 都會提亮；`TYPE_NODIR`＝0 不會 |
| S_ATTACK（op 5＝3.81 **S30**）kind 0／1／10／其他 | `sub_930A20`／`0x52FE20` | gfx 後的 1 byte | 3.81 `S_UseAttackSkill` 寫 0 → 不會（FU4§3） |
| S_ATTACK kind **2**（`Lightning`） | `sub_930A20` `case 2` | 不看 | **會**：kind 2 的 gfx（本機 list.spz 8 個，SV§2.2）播到第 2 格就提亮 |
| S250 子碼 **83**（3.81 `S_GlobalEffect`）值 3 | `sub_9357E0` case 83（`0x93B8DC`／FO `0x53ACDC`） | 1：gfx 2042／2030／2029 隨機，放在最多 5 個 `+196 ≠ 0` 的物件上 | 會：3.81 寫 `C(triggerNum)`，TW16 讀 `d`，補零後值相同；值由資料決定（`CombindTable`、`ClassicBagCombineService`、`WenyangSystem` 等） |
| S250 子碼 48 值 10001 | `sub_9357E0` case 48（`0x9397F8`／FO `0x538BF8`） | 1（同上的煙火） | 3.81 不送 48 |
| 物件 gfx **2001** 開始動作 19 | `sub_A467A0`（`UserObject` 開始動作，FO `0x645BA0`） | 5：5 個 gfx 762 | 會，只要伺服器讓 gfx 2001 播動作 19（gfx 2001 是什麼本輪沒對） |
| 物件 gfx **53** 走路（動作 1）第 3 格 | `sub_A4B060`（每格回呼，FO `0x64A460`）→ `sub_A507A0`（FO `0x64FBA0`） | 15：gfx 214 | 會，只要畫面上有 gfx 53 在走 |
| 移除 gfx 6706／6712／6718／6724 的物件 | S_REMOVE_OBJECT `sub_9493B0`（FO `0x5487B0`）→ `sub_A4E880`（FO `0x64DC80`），判斷 `sub_A4AB40` | 1：同 gfx | 3.81 有沒有這 4 個 gfx 沒查（推測是 TW16 新物件） |
| 特殊 gfx 的子特效（758、5893、8077、1819、757、2261 等） | `sub_8ACD20` | 照抄母特效的 `+9` | 跟著母特效 |

其他 `sub_8AB390`／`sub_8AB710`／`sub_A507A0` 呼叫點（S55、彈道落點、變身、信件、訊息代碼、狀態、門、UserObject 其他分支等）的 `+9` 都是 0。

### 3.3 3.81 是否支援

會。3.81 主要的觸發是 **S42 有方向範圍魔法（type 8）**、**S30／S42 用 kind 2 的 gfx**，其次是 S106 的 gfx 2028–2056、`S_GlobalEffect(3)` 煙火、gfx 2001／53 的特定動作。FU4§3.2「3.81 不會提亮」的推測要撤回。

### 3.4 對應要改的檔案

- `engine/source/Ambience.cs`：`BeginLightEffect`／`EndLightEffect` 已有（計數＋min(15, L+5)＋還原），註解改成 FU5§3 的條件。
- 特效播放端（`MagicRendering.cs`／`AttackSkillPackets.cs` 的特效影格推進，與 S106 的 `SoundPackets.cs`／`MapEffects.cs`）：
  - 特效物件加一個 `LightFlag`（＝`+9`）：S106 gfx 2028–2056＝1；S42＝封包的 `type`；S30 依封包那一 byte；其他 0。
  - 預設 `Effect`：`LightFlag ≠ 0` 且影格號變成 1 時 `BeginLightEffect()`，播完時 `EndLightEffect()`（只在有 Begin 過時 End，不照抄原版的負數瑕疵）。
  - kind 2：影格號變成 2 時 Begin，播完 End，不看旗標。
- S250 83 煙火、gfx 2001／53 的特例、移除 4 個 gfx 的特效列為選配（各自獨立，等 S250 83 的全畫面特效做時一起做）。
- 這是 R3-H 的範圍；`ObjectRendering.cs`／`Ambience.cs` 若被其他任務占用要排序。

## 4. Q4-2 物品 `+36 == 87` 的意義（已驗證）

- 背包物品物件的 `+36`（word）只有一個寫入函式 `sub_C02C40`（VA `0xC02C40`／FO `0x802040`），讀取是 `sub_C020D0`（FO `0x8014D0`）。
- S_ADD_INVENTORY（op 30，`sub_92D8B0`／FO `0x52CCB0`）讀 `dcchcdcsc`（旗標 2 時 `dhcchcdcsc`），先存成紀錄（`sub_926150`：`+0` id、`+6`／`+7` 兩個 `c`、**`+8` 的 `h`**、`+10` 祝福、`+12` 數量、`+16` 狀態、`+17` 名稱），再由 `sub_518CD0`（FO `0x1180D0`）取紀錄 `+8` 呼叫 `sub_C02C40`；gfx 487 或 1617 而且祝福 0 時改成 1617。
- 這個 `h` 就是 3.81 `S_AddItem` 的**背包圖示 gfx**。所以 **`+36 == 87` ＝背包圖示是 gfx 87 的物品**。
- 3.81 `380_db.sql` 的 `armor` 表裡背包圖示 87 的是戒指：「情人戒指」（`$10056`）與 6 個「GM戒指」（`t690_invgfx.py`）。沒有 `$431`「沙哈的戒指」。
- 結論：`$431` 隱身分支＝「背包裡有一枚圖示 87、名稱含『沙哈的戒指』的戒指」。3.81 沒有這件物品，也不送 S36 類型 3（FU4§4），**不必實作**。`Invisibility.cs` 的註解可以補上這個意義。

## 5. 本機 surf 9183 與本輪用到的圖檔

`t690_surfcheck.py` 掃 `0-客戶端` 全部 `Sprite*.idx`／`Tile.idx` 的檔名：

| id | `.img` | `.tbt` | 用途 |
|---|---|---|---|
| **9183** | **沒有** | 沒有 | 血盟倉庫紀錄窗的關閉鈕（FU4§5）、RenewalMailUI（gap-matrix 4.1） |
| 9184 | 沒有 | 沒有 | RenewalMailUI |
| 773 | `Sprite.idx` | `Sprite.idx` | 主人圖示 |
| 2826 | `Sprite13.idx` | `Sprite10.idx` | 隊員圖示 |
| 8264 | 沒有 | `Sprite12.idx` | 隊長圖示（surf 要 `.img`，本機缺） |
| 5765 | 沒有 | 沒有 | 旗標圖示 |
| 5199 | `Sprite03.idx` | `Sprite00.idx` | +181 圖示（icon 讀 `.tbt`，本機有） |

所以血盟倉庫紀錄窗的關閉鈕在本機**沒有圖**：R3-C 照 FU4§5 做時，關閉鈕改用 `HYPER_TEXT_WINDOW` 的 `IMAGE_BTN` 1962 或不畫（點擊區仍在 (215, 9)）。

## 6. 重跑

腳本在 `rea-scripts/t690/`：

| 腳本 | 用途 |
|---|---|
| `ida_t690.py` | `ida_t680.py` 加上 `mkfunc A B`（在副本補建函式後反編譯）、`vtab VA N`（列 vtable 內容與名稱）、`dwords VA N`、`fdis VA`（整個函式反組譯寫成 `.asm`）、`nameea 名稱`（符號位址與引用） |
| `queries.txt` | q1–q14 查詢清單 |
| `t690_surfcheck.py` | 給 id，查 `Sprite*.idx`／`Tile.idx` 有沒有 `<id>.img`／`.tbt`／`.png` |
| `t690_invgfx.py` | 列出 3.81 `380_db.sql` 的 `armor`／`weapon` 中背包圖示等於某值的物品（`etcitem` 欄名不同，略過） |

字串用 `rea-scripts/t680/t680_strings.py`（`string-c.tbl`）。執行方式同 FU4§10：把 `ida_t690.py`、`run_ida.ps1` 放進 `scratch/rea/scripts/`，IDB 副本放 `scratch/rea/ida/tw16.idb`，先跑 `subst` 確認代號沒有被占用，再執行 `run_ida.ps1 -Script ida_t690.py -List qN.txt -Out oN -Drive W:`。腳本必須是無 BOM 的 UTF-8。

## 7. 未驗證與限制

- 全程靜態分析，沒有實機驗證。3.81 `LiTo.bin` 有殼，行為以 TW16 為準（推測同代）。
- Q3-2：隊員圖示 `map<索引, surf>` 的填表者、`+604`、旗標圖示的 `0x40000000`／`+596`、選項 `byte_135C1EB` 在 `option11` 頁的文字沒有追。NPC 的 S87 尾端讀到補零是依 `ServerBasePacket` 推測。
- Q3-7：gfx 831 旗標的實際觸發時機（推測傳送後）沒有真機確認；字串 3503 本機表沒有。
- Q4-1：`+9` 在推進函式以外有沒有其他讀者沒有全掃；3.81 S106 的 gfx 有沒有落在 2028–2056、gfx 2001／53／6706 等是什麼，沒有逐筆查。
- 本輪沒有改產品程式；第 1.3、2.5、3.4 節是給 code 任務的建議。
