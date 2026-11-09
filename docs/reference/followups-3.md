# 查證補遺第 3 輪：前幾輪留下的未驗證細項

T275（#665，M7／#503）。把 [`send-sites-2.md`](send-sites-2.md)（SS2）、[`sprites-and-strings.md`](sprites-and-strings.md)（SS）、[`packetbox-windows.md`](packetbox-windows.md)（PBW）、[`trade-window-and-projectiles.md`](trade-window-and-projectiles.md)（TWP）留下的「待驗證」細項查完。簡稱：本文 **FU3**，引用寫 `FU3§n`。

每節分「原版怎麼做／3.81 是否支援／對應的任務要改什麼」。記號：**已驗證**＝反編譯、反組譯、原始碼或資源檔直接看到；**推測**＝依欄位或命名判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 方法與來源

- **BIN**：未加殼的 `TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（`nat.py` 開檔時核對）。
- **IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/rea/ida/tw16.idb`，複製前後 SHA-256 都是 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`。IDA 7.7＋Hex-Rays，唯讀批次 19 輪（q1–q19，清單見 `rea-scripts/t665/queries.txt`）。
- **subst**：每輪執行前後都跑 `subst` 確認。開始時沒有被占用的代號；本輪用 `W:`。第 2 輪因為輸出被 `Select -First` 截斷，`run_ida.ps1` 的 `finally` 沒有執行，`W:` 殘留，當場用 `subst W: /D` 解除；之後各輪都改成整段輸出、結束後 `subst` 無輸出。
- **地址換算**：程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`（同 SS2§0）。bss 沒有 FO。
- **其他客戶端（只讀，不進 git）**：kor25 `SpellList.xml`（解密後 SHA-256 前 16 碼 `5d21a9ba95f294fb`）、ly `SpellList-c.xml`（`b6c9f5e1e5767a1b`）。兩份都是 T268 以 `ui_xml_decrypt.py` 解到 `scratch/` 的結果，本輪只複製到本 worktree 的 `scratch/rea/spell/` 讀取。本機 `effectlist2.xml`（`fc522b85…88fd`，#448／T268 解出）同樣只在 `scratch/` 讀。
- **本機字串表**：`Text.pak` 的 `string-c.tbl`（`decoded_sha256` `4ebbc1a7…43e9`，3484 筆），用 `t225_strings.py` 在記憶體中讀。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器\src`，唯讀），SHA-256 前 16 碼：

  | 檔案 | SHA-256 前 16 碼 |
  |---|---|
  | `L1SkillId.java` | `5b3dafdac87f504e` |
  | `C_UseSkill.java` | `4932c6dd71b330d9` |
  | `skillmode/TELEPORT.java` | `0fa1b03ba31890cc` |
  | `C_NPCAction.java` | `280ef4bb7316ca61` |
  | `Npc_AuctionBoard.java` | `f1e65aff7a858538` |
  | `S_AuctionBoardRead.java` | `0bc92c0a00302b8e` |
  | `S_PacketBox.java` | `817ba5fd5b8ada48` |

- **引擎**：本分支 `engine/source`，只讀，不建置、不執行。
- 全程靜態分析。沒有啟動遊戲、伺服器或 DB；沒有改產品程式；沒有任何 BIN、pak、XML 進 git。

## 1. ★ `SpellList.xml` 的 id 換算與預設值（T269／#643）

### 1.1 原版怎麼做（已驗證）

**解析（`sub_E177C0`，VA `0xE177C0`／FO `0xA16BC0`）與收尾（`sub_E17370`，VA `0xE17370`／FO `0xA16770`，vtable `0x11DF004` 第 2 格）**

- 每個 `<spell>`／`<pspell>` 先 `new(0x84)`，建構子 `sub_E14030`（VA `0xE14030`／FO `0xA13430`）設初值，再逐一套屬性。
- `id`：`atoi` 後同時寫進解析器 `+44` 與項目 `[0]`、`[1]`。
- `pos`（字串在 VA `0x11DECFC` 的指標所指，內容 `pos`）：只覆寫項目 `[1]`，是技能窗內的顯示位置（kor25 有 106 筆，例如 id 86→pos 16、id 96→pos 24），**不是**查表鍵。
- `</spell>` 時，`sub_E17370` 以解析器 `+44`（也就是 XML 的 `id`）為鍵插入對照表（`sub_E090F0`）；鍵重複時跳出「(SpellList.xml)」警告框。
- 查詢：`sub_E231D0` → `sub_E16A40`（VA `0xE16A40`／FO `0xA15E40`）以 **1 byte 的技能索引**查表。呼叫者有 21 處，包括冷卻計算 `sub_E246E0`（SS2§6）。

**結論：XML `id`＝TW16 技能索引（0 起算）＝3.81 技能編號 − 1。**

- 核對 20 筆，kor25 與 ly 都一致：0 初級治癒術→`HEAL`=1、1 日光術→`LIGHT`=2、4 指定傳送→`TELEPORT`=5、8 解毒術→`CURE_POISON`=9、12→`DETECTION`=13、25→`PHYSICAL_ENCHANT_DEX`=26、43 魔法相消術→`CANCELLATION`=44、50 召喚術→`SUMMON_MONSTER`=51、51 神聖疾走→`HOLY_WALK`=52、63 魔法封印→`SILENCE`=64、68 集體傳送術→69、86 衝擊之暈→`SHOCK_STUN`=87、87 增幅防禦→`REDUCTION_ARMOR`=88、97 附加劇毒→`ENCHANT_VENOM`=98、112 精準目標→`TRUE_TARGET`=113、128 魔法防禦→`RESIST_MAGIC`=129、129 心靈轉換→`BODY_TO_MIND`=130、160 封印禁地→`AREA_OF_SILENCE`=161、192 驚悚死神→`HORROR_OF_DEATH`=193、200 鏡像→`MIRROR_IMAGE`=201（`L1SkillId.java`）。
- 送包也一致：TW16 送 C_USE_SPELL 用 `索引>>3, 索引&7`（SS2§7），3.81 依同樣的 row／column 還原技能編號。
- kor25、ly 各 185 筆 `<spell>`（沒有 `<pspell>`），id 範圍 0–229，沒有重複。SS2§6 寫的「kor25 186 筆」是另一種數法，本輪以元素數為準。

**沒寫屬性時的預設值（建構子 `sub_E14030` 的初值）**

| 項目欄位 | 屬性 | 預設 |
|---|---|---|
| `[0]`／`[1]` | `id`／`pos` | −1（`id` 一定有寫） |
| `[2]`、`[3]` | `required mp`／`hp` | 0 |
| `[4]` | `duration` | 0 |
| `[5]` | `align` | **1**（neutral；chaotic＝0、lawful＝2） |
| `[6]` | `type="attack"` | 0（`category="range"` 也會設成 1） |
| `[7]` | `category` | 0（target 1、range 2、location 3、other 4、dead 5、command 7、teleport 8、runclan 9、callclan 10、item 11、pledge 12、passive 13、dummy −1） |
| `[8]` | `attribute` | 0（laser 5、earth 4） |
| `[9]` | `directTarget` | 0（self 1、other 2） |
| `[10]` | `major` | 0（fire 1、water 2、wind 3、earth 4） |
| `[11]` | `range` | 0 |
| `[12]`／`[13]` | `icon`／`xicon` | −1 |
| `[14]`（`+56`） | `delay`（毫秒） | **10** |
| `+60` | `dir`／`direction` | **0**（false → 動作 19） |
| `+62` | `savelastspell` | **1**（true） |

SS2§6 推測的「delay 預設 0」**不對**，正確是 **10 ms**。kor25 沒寫 `delay` 的 39 筆、明寫 `delay="10"` 的 45 筆，冷卻都是「動作時間＋10 ms」。沒寫 `direction` 的 119 筆都用動作 19。

### 1.2 3.81 是否支援

- 本機 `Tile.pak` 沒有 `SpellList.xml`（SS2§6）。3.81 的 `LiTo.bin` 有殼，是否讀同一份檔**待驗證**（卡在加殼）。
- 換算規則本身與伺服器無關：引擎的技能 id 已經是 3.81 編號（`MagicSpells.cs`、`SkillCast.Wire(id)`），只要把 XML `id` 加 1。

### 1.3 T269（#643）／R2-H 要改什麼

- 執行期讀 kor25（或其他客戶端）的 `SpellList.xml`：以 `id + 1` 對應引擎技能 id；沒寫 `delay` 時用 **10**，沒寫 `direction` 時用動作 **19**。`pos` 不要拿來當鍵。
- 找不到檔案時退回現在的 `ReuseMs`／`Action`（SS2§6 原建議不變）；不得把檔案複製進 git。

## 2. ★ 他人頭上王冠的可見條件 `sub_A42C30`（T272／#649）

### 2.1 原版怎麼做（已驗證）

- `sub_A42C30`（VA `0xA42C30`／FO `0x642030`）＝`return obj[85] != 0`，也就是人物物件 `+85` 的**隱身旗標**。呼叫者有 41 個函式，包括名稱繪製 `sub_F08E30`、目標選取、施法等。
- `+85` 只有 3 個寫入點（全程式掃描 `[reg+55h]` 的 byte 寫入）：
  1. `sub_A45760`（VA `0xA45760`／FO `0x644B60`）是 setter。它被三個地方呼叫：
     - `sub_A44CE0` 重置成 0。
     - 物件建立 `sub_A51920`（VA `0xA51920`／FO `0x650D20`）：在 `obj+88` 不成立時，用描述結構 `+103` 的值寫入。
     - `sub_A50850`：特殊外型 7138 在某個模式值為 0 時設成 1。
  2. `sub_A48990`（VA `0xA48990`／FO `0x647D90`）：寫 `a2 != 0`，同時更新子物件。唯一呼叫者是 **TW16 op 160 `S_INVISIBLE`**（`sub_940330`，VA `0x940330`／FO `0x53F730`，格式 `dc`，AM§4(d)）。
  3. `sub_7906F0`：清 0。
- 描述結構 `+103` 來自 S_PUT_OBJECT 的解碼 `sub_948A60`（VA `0x948A60`／FO `0x547E60`，格式 `hhdhccccdhsscdsscccc…`）。狀態 byte `v57` 拆成 `&1`、`&2`、`&4`、`&8`、`<0`，其中 **`(狀態 & 2) != 0`** 是 `sub_9262D0`（VA `0x9262D0`／FO `0x5256D0`）的第 20 個參數，寫進 `+103`。
- 所以王冠條件（SS§1.1 第 4 點）是：自己一定畫；別人要**沒有隱身**（S_PUT_OBJECT 狀態 bit 0x02、S_INVISIBLE 都沒有設），而且 y > 0。
- 這個判斷**只看隱身旗標**。同隊或同血盟的隱身者雖然會以 50% 混合畫出身體（AM§4(d)），也**不畫王冠**。

### 2.2 3.81 是否支援

支援。3.81 `S_CharPack` 的 `STATUS_INVISIBLE=2`（server-protocol.md），以及 S171 `S_Invis`，和 TW16 的兩個來源一一對應。

### 2.3 T272（#649）／R2-I 要改什麼

- 引擎已經把隱身存在物件 `Flags & 2`（`Invisibility.cs` 的 `InvisibleFlag`，S87 狀態與 S171 共用）。王冠的他人條件直接用 `(Flags & 2) == 0 && y > 0`。
- **不要**用 `InvisibilityOpacity`／`InvisiblePeerVisible` 的「同隊同盟可見」結果：原版只看旗標，看得見身體的隱身同伴也不畫王冠。
- 自己一律畫（原版不檢查自己的隱身）。

## 3. S36 type 1 的傳送目的地窗、type 5 的召喚窗（R2-H）

### 3.1 type 1：傳送目的地窗＝書籤窗 `CBookmarkWindowRenewal`（已驗證）

**類別與建立**

- `dword_1540828` 由 XML 工廠 `sub_B56A40` 在 `0xB56B05`（FO `0x755F05`）比對名稱 **`"BookmarkWindowUI"`** 後建立：`new(0x278)` → `sub_BD9F50`（VA `0xBD9F50`／FO `0x7D9350`），vtable 是 **`CBookmarkWindowRenewal`**。
- 版面檔推定是 `BookmarkUI.xml`。依據：XML 檔名表 `off_137B3A8` 第 0xC 筆（指標在 `.data` VA `0x137B3D8`），根視窗 `Bookmark_Window`，FrameSurf 4313。本機、kor25、ly、t89 都有這個檔；工廠名稱與檔名的對應沒有直接追到，所以是**推測**。
- 同一個窗也是一般的書籤（記憶座標）窗：
  - `sub_4FE1D0`／`sub_502720` 送 TW16 op 84 `cs`（加入書籤）。
  - `sub_BDC1F0` 送 op 106 `csd`（刪除，先跳字串 846「要刪除所選擇的項目？」）。
  - op 2 S_ADD_BOOKMARK 的收包 `sub_931D60`（`shhhdc`）把新書籤寫進這個窗後開窗。
  - 按 ESC（鍵碼 27）由 `sub_BDAA10` 關窗，必要時顯示 2847「座標記憶結束」。

**施放傳送時（`sub_E08A20`，VA `0xE08A20`／FO `0xA07E20`）**

- 條件都成立時，**不送 C_USE_SPELL**，改把窗設成「選目的地」模式（`sub_BDFE90(1)`、`sub_BDFE70(0)`），記下技能索引 `dword_1541DE8`、`dword_154082C`＝−1，再開窗（vtable `+68`(1)）。條件是：
  - S36 type 1 為 1–3，或職業碼 `byte_1542AA1`＝2（SS2§7 推定為王族）、Lv ≥ 50 且 `byte_13A931C`；
  - 窗沒有在其他模式；
  - 索引 ≠ 130。
- 條件不成立時：
  - 限制地圖（`dword_138BE60` 是 5、6、83、84 或 ≥ 0x4000）顯示 276「在此無法使用傳送。」；
  - 否則直接送 `ccchhh` 216＝索引>>3、索引&7、目前地圖、0、0。
  - 魔力條件不足時顯示 278。
- **選好目的地後**（`sub_BE0F10`，VA `0xBE0F10`／FO `0x7E0310`；只接受技能索引 4 指定傳送、68 集體傳送）：
  - 書籤有座標：送 `ccchhh` 216＝索引>>3、索引&7、**書籤地圖 H、書籤 x H、書籤 y H**。
  - 書籤沒有座標：送目前地圖、0、0。
  - 道具傳送分支（`dword_154082C` ≠ −1）：改送 `cdhhh` 208（C_USE_ITEM）＝道具 id、地圖、x、y。

### 3.2 type 1 在 3.81（已驗證）

- 3.81 會送 S36 type 1（傳送控制戒指 20288，SS2§7）。
- **格式不同**：3.81 `C_UseSkill` 對技能 5／69 讀 `H(地圖)`＋`D(書籤 id)`（`C_UseSkill.java:257–266`），`TELEPORT.java` 以書籤 id 查 `CharBookTable`。TW16 送的是 `H 地圖 H x H y`，3.81 會把 x、y 當成書籤 id，查不到書籤。
- 引擎的 `SkillCast` 已經用 3.81 的 `HD`（`SkillTargetKind.Bookmark`，`SkillCast.cs:39`）。所以 R2-H 只照抄原版的**開窗流程**，送包維持 `HD`，不要照抄 TW16 的 `hhh`。

### 3.3 type 5：召喚窗 `CSummonUI`（已驗證）

- `dword_1540858` 在 `sub_B56A40` 的 `0xB5706C`（FO `0x75646C`）以名稱 **`"SummonUI"`** 建立：`new(0x174)` → `sub_BFFB60`（VA `0xBFFB60`／FO `0x7FEF60`）。
  - 版面 `SummonUI.xml`（根視窗 `SummonMonster_Window`，BaseSurf 4893／FrameSurf 4894），清單資料 `summonList.xml`（檔名表 0x16、0x18）。本機都有。
- 開窗：施放技能索引 50（召喚術）時，`sub_DF8A50`（VA `0xDF8A50`／FO `0x9F7E50`）在 S36 type 5 為 1–3 時呼叫 `sub_503FF0`：窗沒開才開（vtable `+72`／`+68`），**不送封包**。
- 選擇後（`sub_C00890` FO `0x7FFC90`、`sub_C00F50` FO `0x800350`）：
  - 等級檢查 `(等級 − 28)/4 + 7 < 選項值 % 256` 時，顯示 743「等級太低而無法召喚怪物」，不送。
  - 否則送 `cccd` 216＝6、2（索引 50）、**選項值 D**。選項值就是 `summonList.xml` 的 `action`，例如 7／263／519（28 級）、8／264／520（32 級）。
- 沒有戒指時：送 `cccd` 216＋`clamp((等級−28)/4+7, 7, 13)`（SS2§7）。

### 3.4 type 5 在 3.81（已驗證）

- 3.81 會送 S36 type 5（召喚控制戒指 20284）。
- `C_UseSkill` 對技能 51：戴戒指時 `requestedSummonId = readD()`，沒戴時讀掉一個 D 不用（`C_UseSkill.java:278–288`）。和 TW16 一致。
- 引擎已經有本機召喚窗（`Pets.cs` `OpenLocalSummon`／`SendSummonChoice`，以 `SkillTargetKind.Summon` 送 C6 `D(action)`，#530）。

### 3.5 R2-H 要改什麼

- **type 1**：施放技能 5／69 時，若 `AbilityEquipped(1) == true`（或王族 Lv ≥ 50 的條件；`byte_13A931C` 的意義沒追，**待驗證**），且不在限制地圖：
  - 不送封包，開書籤窗的「選目的地」模式；
  - 選中後以 `HD`（地圖、書籤 id）送 C6；
  - ESC 取消。
  - 限制地圖顯示字串 276。
- **type 5**：施放技能 51 且 `AbilityEquipped(5) == true` 時，改呼叫既有的 `OpenLocalSummon()`，不送封包；送出前的等級檢查用 `(等級−28)/4+7 ≥ action%256`，不成立時顯示 743。
- 兩者都只用既有封包，不需要新協定。

## 4. 盟屋地圖 674 鈕與新物品清單的計入規則

### 4.1 674 鈕（已驗證）

- `sub_543810`（VA `0x543810`／FO `0x142C10`）→ `sub_542BF0`（VA `0x542BF0`／FO `0x141FF0`）：
  `sprintf("%u", 編號 + (區域 << 16))`，再送 `sub_9FBC00("cdss", 227, this[3], "select", 字串)`。
  - `this[3]` 是 S187 的物件 id（拍賣看板 NPC）。
  - TW16 op 227＝`C_HACTION`＝3.81 **C125 `C_NPCAction`**（gap-matrix 第 3 節）。
- **3.81**：
  - `C_NPCAction.java:514–521`：NPC 類別是 `other.Npc_AuctionBoard` 時，多讀一個 `S`。
  - 第 556–558 行以 `s + "," + s2` 交給 `Npc_AuctionBoard.action`。
  - `"select"` 分支（第 63–64 行）回 **`S_AuctionBoardRead`**：S39 HTML 頁 `agsel`，帶 9 個字串（小屋名稱、位置、面積、原擁有者、目前競標者、價格、截止月日時）。
- **結論**：674 鈕＝「查看這間盟屋的拍賣資訊」。3.81 支援，盟屋編號 `區域<<16 | 編號` 和 S187 送來的值相同。
- **#550（T232）要改什麼**：674 鈕按下（放開在鈕內）時送 C125 `D(S187 物件 id) S("select") S(十進位盟屋編號)`，伺服器回 S39 `agsel` 頁，由既有 NPC 對話窗顯示；678 仍是關窗。

### 4.2 新物品清單（`UserObtainUI`，`dword_154085C`）的計入規則（已驗證）

物件裡有兩份清單：**基準清單**（`+0x154`，`sub_C04040` 依物品 id 查、`sub_C040B0` 依名稱＋第 6 參數查）和**新物品清單**（`+0x160`，`sub_C04140` 依 id 查）。統計窗「獲得道具」顯示的是新物品清單的筆數（`sub_C042C0` → `+0x160` 的長度）。

| 封包（TW16→3.81） | 呼叫 | 做什麼 |
|---|---|---|
| op 216 S_ADD_INVENTORY_BATCH（3.81 S5 InvList） | `sub_92D360` → `sub_C03720`（VA `0xC03720`／FO `0x802B20`） | 每筆加進**基準清單**（末端 `0xC038A4` 是 `add ecx,154h`） |
| op 30 S_ADD_INVENTORY（S15 AddItem） | `sub_92D8B0` → `sub_518CD0` → `sub_C038D0`（VA `0xC038D0`／FO `0x802CD0`） | 基準裡有同 id 而且數量 ≥ 新數量 → 不計；基準數量較少 → 新清單記「差額」（已有就更新）；基準沒有這個 id → 再用名稱找，旗標 `&0x80` 而且同一個第 6 參數時沿用該筆的基準數量；都沒有 → 新清單記全部數量 |
| op 164 S_CHANGE_ITEM_DESC_EX（S24 ItemStatus 等） | `sub_932750` → `sub_C04B00`（VA `0xC04B00`／FO `0x803F00`） | 新數量 ≤ 基準 → 從新清單移除；新清單已有 → 改成差額；只有基準 → 新增一筆差額；兩邊都沒有 → 不動 |
| op 98 S_REMOVE_INVENTORY（S57） | `sub_9490F0` → `sub_C03EB0`（VA `0xC03EB0`／FO `0x8032B0`） | 從新清單移除同 id（基準不動） |

- 「重設」鈕清空新清單（SS§3.1）。基準清單只在整批背包封包時建立。
- **3.81**：支援，四種封包 3.81 都會送。
- **R2-D（T272／#649）要改什麼**：
  - 道具列＝「目前數量高於登入時整批背包數量」的物品筆數（每個物品 id 算 1 筆，不是數量總和）。
  - 規則照上表：S5 建基準；S15 依基準算差額；S24 數量回到基準以下就移除；S57 移除。
  - 第 6 參數（推測是祝福狀態）與名稱比對的分支屬於細節，可以先只用 id 比對，並在程式裡註明。

## 5. `QuitWindow` 的入口、S250 子碼 192／190／84（PBW）

### 5.1 `QuitWindow` 能不能用 ESC 或選單開啟（已驗證：不能）

- 全域 `dword_14EBE24` 的所有引用（18 處，PBW§4 列的 8 個函式）中，**開窗**只有 WndProc `sub_8BD990`（FO `0x4BCD90`）的 WM_TIMER `0x29A`（`0x8BF027`–`0x8BF052`，`QuitWindow` 沒開時 `sub_A59AB0(dword_14EBE24)`）。
  - `sub_A57C60`（視窗堆疊，FO `0x657060`）只在這個窗被推入堆疊時做特例處理。
  - `sub_8CB760` 只負責重畫。
- vtable `0x118702C` 只被建構子與解構子引用（PBW§4），沒有經由 XML 名稱或視窗 id 的間接建立。
- WndProc 的按鍵處理：ESC（27）只清 `byte_13D3E48`，其他鍵交給 `sub_8C1290`／`off_136EB78`。`sub_8C1290` 裡沒有開這個窗的路徑。
- `uiextra86.xml` 第 1 段 `NEW_QUIT_WINDOW`：全 BIN 的 `sub_7D7650(n)` 呼叫點（49 個函式）都沒有用常數 1，只有通用迴圈 `sub_A84160`／`sub_A842D0`。也就是說 TW16 沒有使用這一段。
- **結論**：原版 `QuitWindow` 只在死亡逾時後出現。ESC 與選單都不開它。
- **3.81／引擎**：PBW-B（#608 T247，已關閉）不需要補 ESC 入口。

### 5.2 S250 子碼 192 的讀取者（已驗證）

- S_EVENT 把 `Q`（8 bytes）存進角色 `+832`。這是 64 位元的**角色選項旗標**，讀寫者有：
  - **`HyperActShowVipMark`**（執行函式 `sub_584CC0`，VA `0x584CC0`／FO `0x1840C0`，vtable `0x11A9650`）：翻轉 bit 0、送 TW16 op 55 子碼 56（＋`+832`、`+836` 兩個 D），再重開 HTML 頁 `option11`（環境設定）。
  - 選項窗 `sub_9184B0`、`sub_918160`、`sub_91C8A0`：讀或翻轉 bit 0，`sub_91C8A0` 也送 op 55／56。
  - 對話窗 `sub_8676F0`：讀 bit 0。
- 所以 TW16 的 192＝伺服器存的選項旗標（bit 0＝顯示 VIP 標記）。
- **3.81**：192 是自訂 `S_ShowDrop`（PBW§2.13）；op 55＝3.81 C254 `C_Windows`，只處理子碼 0x00、0x0B、0x2E，不處理 56。**不支援**，維持 PBW「不建議」：收到時安靜消費，不要寫進選項。

### 5.3 S250 子碼 190 的圖示 512–514（已驗證）

本機 `effectlist2.xml` 的 effect 512／513／514：

- `startMsgName`＝`STR_STATUS_EFFECT_MAGIC_DOLL`，startMsg 1143「魔法娃娃會給你力量。」；
- kind `cook`，duration 1800，`remainWhenDie`；
- 圖示 `.tbt` 8341／8342／8343（32×32，分別在 Sprite08／09／10）。

也就是三種**魔法娃娃**狀態圖示，對應 190 的種類 0x40／0x20／0x60。

- **3.81**：190 是自訂 `S_ItemBoard`（PBW§2.13），不送這個格式。3.81 的娃娃圖示走 `S_PacketBox.DOLL`（子碼 56，`S_PacketBox.java:242`）。維持「不建議」。

### 5.4 S250 子碼 84 類型 3／4 的宿主（已驗證）

- `dword_14EBF50` 由 `sub_B56A40` 在 `0xB58E8A`（FO `0x75828A`）以 **`"RoundScoreUI"`** 建立（`sub_9D8240`）。本機沒有這個 XML，kor25／ly／t89 有。
- 類型 3：`sub_9DA410(0)` 把子元件 86–88 設成隱藏。
- 類型 4：`sub_9DA2B0`（VA `0x9DA2B0`／FO `0x5D96B0`）取前 10 字 `atoi`，交給 `sub_9D97F0` 用數字圖（surf 10688 起）畫最多 3 位數。
- 另有測試用聊天指令（`sub_F426D0`／`sub_F42860`，參數 `"0"`／`"1"`）。這是回合制小遊戲的記分窗。
- **3.81**：只送類型 2（PBW§2.6）。類型 3／4 不做。

## 6. 賣出確認清單 `sub_A2C660` 的來源、修理列的數量上限（R2-E）

### 6.1 確認清單（已驗證）

- `sub_A2C660`（VA `0xA2C660`／FO `0x62BA60`）比對的是 `TradeWindow`（`dword_153CE14`）`+48` 的 vector（建構子 `sub_A26E60` 以 `sub_4F8350` 初始化）。
- **唯一的填寫點是 S_EVENT 子碼 162（0xA2）**（`0x93DC5D`／FO `0x53D05D`）：讀 `D(n)`，先清空 `+48`，再逐筆讀 `H` 放進去。
- 比對的對象是已選 `TradeItem` 的 `+314`（int16）。它由 `sub_88BD70` 從背包物品複製（`sub_C021B0` 的值），**推測**是物品的 descId。
- 確認字串 3133 的內容是「刪除寵物後，即無法再次復原，真的要刪除嗎？ (Y/N)」。所以這份清單是**寵物項圈類物品**：賣掉就會刪除寵物，要先確認。
- **3.81**：`S_PacketBox.java` 沒有子碼 162，全伺服器原始碼也搜不到送 S250／162 的地方（已驗證）。清單永遠是空的，**不會跳確認**。
- **R2-E 要改什麼**：賣出不需要確認。TWP§1.9 第 4 點的「賣出前若在確認清單內跳 3133」可以拿掉，或只保留空清單的資料結構。

### 6.2 修理列的數量上限（已驗證）

- 修理清單 `sub_93F5E0` → `sub_A27820`（VA `0xA27820`／FO `0x626C20`）→ `sub_A26DD0` → 基底 `sub_88BD70`（FO `0x48B170`）：`+296`（`this[74]`）＝`sub_C01F30(背包物品)`，也就是**背包裡該物品的數量**。
- 修理的對象是武器或防具，數量正常是 1。所以上限 1 是對的，依據從「推測」改成「已驗證：上限＝物品數量」。
- 「＋」到上限後回到 0（TWP§1.5）。送出時每個數量 > 0 的列各送一包 C20。
- **R2-E**：上限用物品數量（通常 1），不必寫死 1。

## 7. `dword_153FCC4` 的間接寫入（已驗證：沒有）

- 全程式運算元掃描（IDA，code＋`.rdata`／`.data` 的 dword）：
  - `0x153FCC1`–`0x153FCC7` 範圍內沒有任何引用，只有 SS2§2.1 已列的直接讀取。
  - `0x153F000`–`0x153FCC7` 範圍內，帶索引暫存器的存取只有三個陣列：
    - `word_153F240[i]`：讀，顏色表。
    - `dword_153F460[i]`：寫 0。索引是 XML 檔名表的序號，表只有 70 筆，最遠到 `0x153F574`。
    - `dword_153F7B0[i*8]`／`word_153F7B4[i*8]`：讀。
  - 指向這段範圍的資料指標只有 `0x137B8B0` → `0x153F778`。
- 沒有任何寫入點能碰到 `0x153FCC4`，SS2§2.1 的保留可以拿掉：**TW16 收到 op 133 一定丟棄**。R2-K「不做推薦 UI」的結論不變。
- 殘留風險：執行期從遠處基底算出的指標（例如結構整塊複製）沒辦法用靜態掃描完全排除。但 `sub_B56A40` 的 `MatchMakingUI` 分支不建立物件（gap-matrix 4.1），所以可信度高。

## 8. 可以開 code 的項目

| 項目 | 依據 | 對應任務 | 檔案（建議） |
|---|---|---|---|
| `SpellList.xml` 讀 `delay`／`direction`：`id+1`＝技能 id，預設 delay 10、動作 19 | FU3§1 | T269（#643）／R2-H | `MagicSpells.cs`（或新的讀檔類別）＋測試 |
| 他人王冠條件：`(Flags & 2) == 0 && y > 0`，自己一律畫 | FU3§2 | T272（#649）／R2-I | `ObjectRendering.cs`、`Castle.cs` |
| 傳送控制戒指：施放 5／69 開書籤窗選目的地，送 `HD` | FU3§3.1–3.2 | R2-H（新開或併入） | `SkillCast.cs`、`BookmarkUi.cs`、`AbilityStatus.cs` |
| 召喚控制戒指：施放 51 開既有召喚窗；等級檢查與訊息 743 | FU3§3.3–3.4 | R2-H | `Pets.cs`、`SkillCast.cs` |
| 盟屋地圖 674 鈕：送 C125 `D S("select") S(編號)` | FU3§4.1 | #550（T232） | `Agit.cs`、`NativeUi/AgitMapPanel.cs` |
| 統計窗「獲得道具」計入規則（S5 基準、S15／S24／S57） | FU3§4.2 | T272（#649）／R2-D | `Recovery.cs` 或新面板＋`Inventory.cs` 的事件 |
| 賣出不跳確認；修理上限＝物品數量 | FU3§6 | R2-E | `NativeUi/TradeWindowPanel.cs` |

**不開**：`QuitWindow` 的 ESC 入口（原版沒有）、S250／192、190、84 類型 3／4（3.81 不送原版格式）、血盟推薦 UI（FU3§7）。

## 9. 未驗證與限制

- 全程靜態分析，沒有實機驗證。
- 3.81 `LiTo.bin` 有殼，是否讀 `SpellList.xml` 仍不知道（卡在加殼）。
- 卡在變數意義沒追的有三項：
  - `byte_13A931C`（王族 Lv ≥ 50 自動開書籤窗的附加條件）；
  - 技能索引 130 為什麼排除；
  - `sub_A50850` 對外型 7138 設隱身的模式值。
- `TradeItem+314` 是 descId，是依參數順序推定，沒有對到封包欄位名。
- `BookmarkWindowUI` 對應 `BookmarkUI.xml` 是推定（依檔名表與根視窗名稱，工廠與檔名的對應沒有直接追到）。
- 新物品清單「名稱＋第 6 參數」分支的第 6 參數意義（推測是祝福狀態）沒有追到封包欄位。
- `dword_153FCC4`：靜態掃描排除了直接、索引與資料指標寫入，但執行期遠端指標無法完全排除。

## 10. 重跑

腳本在 `rea-scripts/t665/`：

| 腳本 | 用途 |
|---|---|
| `ida_t665.py` | IDAPython 唯讀查詢：`dec`／`xref`／`dis`／`str`／`stores OFF`（全程式 `[reg+OFF]` 寫入）／`immscan LO HI`（運算元與資料 dword 落在範圍內）／`grepcallers VA REGEX`／`grepfuncs REGEX VA…`。REGEX 不能含空白（用 `\s`） |
| `queries.txt` | q1–q19 的查詢清單（q1 用 `ida_t268.py`，其餘用 `ida_t665.py`；q2 的 `vtdump` 列在 `ida_t665.py` 會被略過） |
| `t665_rtti.py` | 由 vtable 格位反查 RTTI 類別名稱（`nat.py`） |
| `t665_xmltab.py`／`t665_xmlcount.py` | 讀 XML 檔名表 `off_137B3A8` 的指定項與總筆數 |

執行方式同 SS2§13：把腳本與 `run_ida.ps1`、`nat.py` 放進 `scratch/rea/scripts/`，先跑 `subst` 確認代號沒被占用，再執行 `run_ida.ps1 -Script ida_t665.py -List qN.txt -Out oN -Drive W:`。**不要把輸出接 `Select -First`**，不然 `finally` 不會解除 subst。腳本必須是無 BOM 的 UTF-8。字串用 `t225_strings.py <客戶端> out.json 846 3133 1143 2847 743 276 278`。
