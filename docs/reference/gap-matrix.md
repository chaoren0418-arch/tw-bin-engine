# 原版功能對照引擎的差距盤點（gap-matrix，T202／Issue #504）

整理日期：2026-11-08（Asia/Taipei）。依使用者 #503（M7 重建補缺）：以原版 `TW1608262200.bin` 為準，列出引擎還沒實作或只做一半的功能，作為 M7 開 code 任務的依據。

## 0. 方法、身分與記號

**來源與工具（全部唯讀）**
- 原版：`TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（與 [`rea-inventory.md`](rea-inventory.md) 相同）。ImageBase `0x400000`，未啟用 `dynamic_base`。
- `.text` 的 File Offset = VA − `0x400C00`；`.data` 的 File Offset = VA − `0x401800`（區段表由 `nat.py` 讀出，與 UI 文件既有值相符，例如 `HpMpWindow` RTTI `0x138B808`／`0xF8A008`）。
- IDA 7.7＋Hex-Rays：P01 的 `ida/03-ascii-isolated/TW1608262200.idb`（SHA `3a0bf206…e82f`，複製前後雜湊相同）複製到 `scratch/rea/ida/tw16.idb` 後才開，經 subst `W:`（執行前確認未被占用，結束時解除）。只反編譯 `sub_8C7C80`（舊式視窗建立，initGame）與 `sub_B56A40`（XML 視窗工廠）。
- pefile＋Capstone：`rea-scripts/gap_inventory.py`（三組 dispatch table、收包名稱表、送包名稱表、XML 檔名引用、UI 類 RTTI）與 `rea-scripts/ui_classes.py`（XML 視窗建構子 → vtable → RTTI 類別名）。
- 引擎：本分支同步後的 main `003a5a34` 的 `engine/source`。只讀程式碼，不建置、不執行。
- 沒有啟動遊戲、伺服器或 DB；沒有把 BIN、pak 或解出的 XML 放進 git。

**原版與 3.81 伺服器的 opcode 不同。** 引擎連的是 3.81 伺服器（P24Lab，`server-protocol.md` 的 S／C 全表），TW16 的收包編號與它不同（REA：值 8、11、87 都落在預設區塊）。因此本表的對應方式是：TW16 封包名稱（`sub_9515E0` 表）→ 依名稱與欄位**推定**的 3.81 封包 → 引擎有沒有處理那個 3.81 封包。「3.81 對應」欄除了已在 reference 文件查證過的列（依據欄會寫出），都屬**推定**。

**狀態**

| 狀態 | 意義 |
|---|---|
| 已有 | 引擎有對應的處理，協定與已查證的表現沒有已知差異 |
| 部分 | 引擎有處理，但 reference 文件已查出表現差異，或只做了協定、沒有原版視窗 |
| 缺 | 引擎沒有對應的處理。依據欄註明「伺服器不支援」的，是 3.81 伺服器沒有對應封包（依 SP 全表），**不建議補** |

**依據代號**：沿用 [`m6-native-diff.md`](m6-native-diff.md) 的代號：AN＝[`actions-and-names.md`](actions-and-names.md)、UI＝[`ui-layout.md`](ui-layout.md)、AM＝[`ambience-and-map-events.md`](ambience-and-map-events.md)、SD＝[`sound.md`](sound.md)、SV＝[`skills-visual.md`](skills-visual.md)、WIN＝[`windows.md`](windows.md)、REA＝[`rea-inventory.md`](rea-inventory.md)、SP＝[`server-protocol.md`](server-protocol.md)、R＝[`server-rules.md`](server-rules.md)。另加 WO＝[`world-objects.md`](world-objects.md)、M6＝[`m6-native-diff.md`](m6-native-diff.md)、PG＝[`protocol-gaps.md`](protocol-gaps.md)（T225）、WIN2＝[`windows-2.md`](windows-2.md)（T226）、EF2＝[`effects-2.md`](effects-2.md)（T227）、PBW＝[`packetbox-windows.md`](packetbox-windows.md)（T245）、TWP＝[`trade-window-and-projectiles.md`](trade-window-and-projectiles.md)（T266）。「T1xx 審查中」是 `TASKS.md` 已排定、尚未合併的任務。

## 1. 收包：第一組 dispatch（世界中，`sub_947530`）

已驗證：`sub_947530`（VA `0x947530`／FO `0x546930`）在 `[0x13CE3D8]==3` 時用 index `0x94828C`（FO `0x54768C`）→ target `0x9480C0`（FO `0x5474C0`）分派 opcode 0–254；有處理器的 case 共 114 個區塊（19／39／109 共用一個），其餘 **139 個值**落在預設區塊 `0x947DF4`（FO `0x5471F4`，不計入下表）。名稱來自 `sub_9515E0` 的名稱表（index `0x951E78`、jump `0x951C5C`）；名稱表有、但沒有 case 的是 `S_CHANGE_ITEM_TYPE`（8）、`S_DECREE`（14）、`S_CLIENT_READY`（128）。處理函式是該 case 區塊的第一個直接呼叫（`gap_inventory.py`）。

| TW16 op | 名稱 | 處理函式 VA／FO | 功能 | 3.81 對應 | 引擎對應檔案 | 狀態 | 依據 |
|---|---|---|---|---|---|---|---|
| 60 | S_CHANGE_LEVEL | `sub_932FA0`／`0x5323A0` | 等級變更 | 無獨立封包（等級在 S8／S113） | `WorldSession.cs`、`Rewards.cs` | 已有 | SP S8、S113；對應為推定 |
| 116 | S_STATUS | `sub_94C950`／`0x54BD50` | 自身狀態（能力、HP/MP、時間、飽食、負重） | S8 OwnCharStatus | `WorldSession.cs` | 已有 | SP S8；時間欄的日夜重算見 AM§1（T177） |
| 47 | S_HIT_POINT | `sub_93F910`／`0x53ED10` | HP 更新 | S225 HPUpdate | `WorldSession.cs` | 已有 | SP S225 |
| 120 | S_CHANGE_DIRECTION | `sub_932630`／`0x531A30` | 物件轉向 | S122 ChangeHeading | `WorldObjects.cs` | 已有 | SP S122 |
| 112 | S_SAY | `sub_94B4F0`／`0x54A8F0` | 一般／喊話聊天 | S81 NormalChat | `PlayerChat.cs`、`PlayerHudChat.cs` | 部分 | UI#7–#10：前綴與顏色不同（T175 審查中） |
| 61 | S_SAY_CODE | `sub_94AF00`／`0x54A300` | 代碼聊天（NPC 喊話、系統） | S161 NpcShout | `Inventory.cs`、`PlayerChat.cs` | 部分 | SP S161；顯示格式依 UI§3（T175） |
| 104 | S_TELL | `sub_94D990`／`0x54CD90` | 密語 | S73 WhisperChat | `PlayerChat.cs` | 部分 | UI§3（T175） |
| 186 | S_PUT_OBJECT | `sub_948A60`／`0x547E60` | 物件出現（PC／NPC／地面物品／門） | S87 CharPack | `WorldSession.cs`、`WorldObjects.cs`、`ObjectRendering.cs`、`Doors.cs` | 部分 | REA B07（類別表、S87 同身份入口未定位）；AN§2 名稱繪製（T168） |
| 51 | S_REMOVE_OBJECT | `sub_9493B0`／`0x5487B0` | 物件移除 | S120 RemoveObject | `WorldObjects.cs` | 已有 | SP S120 |
| 206 | S_CHANGE_LIGHT | `sub_933000`／`0x532400` | 光源變更 | S40 Light | `Ambience.cs` | 部分 | AM§3：光源畫成圓形、未用 `source.lmp`（G5／T177） |
| 30 | S_ADD_INVENTORY | `sub_92D8B0`／`0x52CCB0` | 背包新增一筆 | S15 AddItem | `Inventory.cs` | 已有 | SP S15 |
| 98 | S_REMOVE_INVENTORY | `sub_9490F0`／`0x5484F0` | 背包移除 | S57 DeleteInventoryItem | `Inventory.cs` | 已有 | SP S57 |
| 194 | S_CHANGE_ITEM_DESC | `sub_932D00`／`0x532100` | 物品名稱變更 | S100 ItemName | `Inventory.cs` | 已有 | SP S100 |
| 164 | S_CHANGE_ITEM_DESC_EX | `sub_932750`／`0x531B50` | 物品說明（鑑定 blob） | S24 ItemStatus／S242 sub1 | `ItemStatus.cs`、`Inventory.cs` | 已有 | R25、R26（T121） |
| 237 | S_CHANGE_ITEM_USE | `sub_932F40`／`0x532340` | 物品使用中狀態 | S24 ItemStatus（名稱含使用中） | `Inventory.cs`、`Equipment.cs` | 已有 | 對應為推定；R26 |
| 15 | S_CHANGE_COUNT | `sub_9324D0`／`0x5318D0` | 物品數量變更 | S24 ItemAmount | `Inventory.cs` | 已有 | SP S24 |
| 70 | S_CHANGE_ITEM_BLESS | `sub_932710`／`0x531B10` | 物品祝福／詛咒色 | S240 ItemColor | `Inventory.cs` | 已有 | SP S240 |
| 21 | S_ADD_SPELL | `sub_92E360`／`0x52D760` | 學會魔法 | S164 AddSkill | `Magic.cs` | 已有 | SP S164 |
| 82 | S_REMOVE_SPELL | `sub_9494D0`／`0x5488D0` | 移除魔法 | S160 DelSkill | `Magic.cs`、`TimedBuffs.cs` | 已有 | SP S160 |
| 211 | S_MOVE_OBJECT | `sub_945770`／`0x544B70` | 物件移動 | S10 MoveObject | `WorldObjects.cs` | 已有 | SP S10 |
| 153 | S_MESSAGE | `sub_944F50`／`0x544350` | 系統文字訊息 | S243 GlobalChat／SystemMessage | `PlayerChat.cs`、`Inventory.cs` | 部分 | UI§3 顏色與前綴（T175） |
| 102 | S_MESSAGE_CODE | `sub_942B40`／`0x541F40` | 訊息代碼（字串表 + 參數） | S71 ServerMessage | `ServerMessageText.cs`、`ServerStatusMessages.cs`、各模組 | 部分 | PG§3：原版查 `Text.pak` 的 `string-c.tbl`（本機 3484 筆），id ≥ 筆數整包丟棄；引擎只硬寫 4 個 id → PG 任務 A 載入整張表 |
| 193 | S_WIELD | `sub_94FE30`／`0x54F230` | 換武器／外觀姿勢 | S119 CharVisualUpdate | `Equipment.cs`、`Fishing.cs` | 已有 | SP S119；`sub_94FE30` 見 AN（t160_opmap） |
| 226 | S_ACTION | `sub_92CB70`／`0x52BF70` | 播放動作 | S158 DoActionGFX | `WorldObjects.cs`、`ObjectRendering.cs` | 部分 | REA（opcode226→`0x92CB70`）；缺圖退回見 AN§3.4（T170） |
| 156 | S_TIME | `sub_94DA90`／`0x54CE90` | 遊戲時間 | S123 GameTime | `WorldSession.cs`、`Ambience.cs` | 部分 | AM§1：本地 6 倍速推進，原版只在收包時重算（T177） |
| 38 | S_CHANGE_ATTR | `sub_932470`／`0x531870` | 格子屬性（門通行） | S209 Door | `Doors.cs` | 已有 | WO 第 3 節 |
| 5 | S_ATTACK | `sub_930A20`／`0x52FE20` | 單體攻擊／技能攻擊 | S30 AttackPacket | `Combat.cs`、`AttackSkillPackets.cs`、`MagicRendering.cs` | 部分 | SV#5–#8：特效與受擊時機（G12） |
| 240 | S_ATTACK_ALL | `sub_92F2C0`／`0x52E6C0` | 範圍攻擊（全部目標） | S42 RangeSkill | `AttackSkillPackets.cs`、`MagicRendering.cs` | 部分 | SV§2：S42 預設類別只畫一個特效（G12） |
| 137 | S_ATTACK_MANY | `sub_92FE40`／`0x52F240` | 多目標攻擊 | S42／S30 多目標 | `AttackSkillPackets.cs` | 部分 | SV§2；對應為推定 |
| 85 | S_ASK | `sub_92EAD0`／`0x52DED0` | 伺服器 Yes/No 詢問 | S219 YES_NO | `Trade.cs`、`Party.cs`、`Duel.cs`、`Marriage.cs` | 部分 | WIN§2.1：原版走通用 `sub_4FC710`→`sub_AC9560`；WO 第 6 節：聊天輸入列 Y/N |
| 33 | S_PARALYSE | `sub_945BD0`／`0x544FD0` | 麻痺 | S202 Paralysis | `Afflictions.cs` | 部分 | AM§4（G7／T179） |
| 122 | S_CHANGE_ABILITY | `sub_932300`／`0x531700` | 能力旗標變更 | S36 Ability | （無） | 缺 | SP S36；引擎無 36 接收 |
| 114 | S_BLIND | `sub_931B60`／`0x530F60` | 詛咒盲目 | S47 CurseBlind | `Afflictions.cs` | 部分 | AM§4（G7） |
| 197 | S_RESURRECT | `sub_9496B0`／`0x548AB0` | 復活詢問 | S85 Resurrection | `Recovery.cs` | 部分 | WO 第 6 節（T151 審查中） |
| 16 | S_POLYMORPH | `sub_946CF0`／`0x5460F0` | 變身 | S76 Poly | `Polymorph.cs`、`PlayerProfiles.cs` | 部分 | AN§3.4／§3.5（T170 審查中） |
| 141 | S_WORLD | `sub_950080`／`0x54F480` | 換地圖 | S206 MapID | `WorldSession.cs`、`MapIdPacket.cs` | 已有 | SP S206（T122） |
| 93 | S_SPEED | `sub_94C530`／`0x54B930` | 速度（加速、勇敢） | S255 SkillHaste／S67 SkillBrave | `NormalHaste.cs`、`WorldSession.cs` | 已有 | SP S255、S67 |
| 229 | S_DRUNKEN | `sub_9343C0`／`0x5337C0` | 酒醉 | S103 Liquor | `TimedBuffs.cs` | 部分 | AM§4(c)：逐掃描線正弦（G7） |
| 36 | S_TITLE | `sub_94DB20`／`0x54CF20` | 稱號 | S183 CharTitle | `TitleAndEmote.cs` | 部分 | AN§2（T168） |
| 131 | S_BUY_LIST | `sub_931FA0`／`0x5313A0` | NPC 出售清單 | S70 ShowShopBuyList | `NpcInteraction.cs`、`NpcUi.cs` | 部分 | WIN§2 T102：原版 `CNpcShop`／`TRADE_WINDOW` |
| 40 | S_SELL_LIST | `sub_94BD80`／`0x54B180` | NPC 收購清單 | S65 ShowShopSellList | `NpcServices.cs`、`NpcUi.cs` | 部分 | WIN§2 T102 |
| 144 | S_HYPERTEXT | `sub_940030`／`0x53F430` | HTML 對話頁 | S39 ShowHtml | `NpcInteraction.cs`、`PlayerQuery.cs`、`Party.cs`、`Pets.cs` | 部分 | WIN§2.1：`HYPER_TEXT_WINDOW`；查詢結果被轉成聊天行（T140）；WIN2§4：`monlist`（3.81 `S_ShowPolyList`）走一般 HTML 頁，引擎只收對話中 NPC 的 S39（W2-A） |
| 2 | S_ADD_BOOKMARK | `sub_931D60`／`0x531160` | 新增書籤 | S92 Bookmarks | `Bookmarks.cs`、`BookmarkUi.cs` | 部分 | UI§6 `BookmarkUI.xml`（T124 審查、T198） |
| 127 | S_RETRIEVE_LIST | `sub_949930`／`0x548D30` | 倉庫清單 | S176 ShowRetrieveList | `NpcServices.cs`、`NpcUi.cs` | 部分 | WIN2§2：類型 3／5／9／18 一律開 `RenewalWareHouseUI`；3.81 世代版面用 `uiextra86.xml` `WAREHOUSE_WINDOW` |
| 213 | S_POISON | `sub_946B00`／`0x545F00` | 中毒 | S165 Poison | `Afflictions.cs` | 部分 | AM§4（G7） |
| 176 | S_WEATHER | `sub_94FD10`／`0x54F110` | 天氣 | S115 Weather | `Ambience.cs` | 部分 | AM§2（G6／T178） |
| 160 | S_INVISIBLE | `sub_940330`／`0x53F730` | 隱身 | S171 Invis | `Invisibility.cs` | 部分 | AM§4(d)（G7） |
| 161 | S_EMBLEM | `sub_935220`／`0x534620` | 盟徽資料 | S118 Emblem | `ClanEmblem.cs`、`Clan.cs` | 部分 | AN§2.8（G4、T135 審查中） |
| 63 | S_WAR | `sub_94F190`／`0x54E590` | 血盟戰爭 | S84 War | `ClanWar.cs` | 部分 | AN#15（T168） |
| 86 | S_EFFECT | `sub_934690`／`0x533A90` | 物件特效（含音效） | S55 SkillSoundGFX | `SoundPackets.cs`、`MagicRendering.cs` | 部分 | SV§2、SD#7 |
| 88 | S_XCHG_START | `sub_9504D0`／`0x54F8D0` | 交易開始 | S52 Trade | `Trade.cs`、`TradeUi.cs` | 部分 | 原版 `TRANSACTION_WINDOW`（WIN§1.5）；引擎 WinForms |
| 145 | S_ADD_XCHG | `sub_92E3B0`／`0x52D7B0` | 交易加入物品 | S35 TradeAddItem | `Trade.cs` | 部分 | 同上 |
| 167 | S_XCHG_RESULT | `sub_950450`／`0x54F850` | 交易結果 | S112 TradeStatus | `Trade.cs` | 已有 | SP S112 |
| 181 | S_IDENTIFY_CODE | `sub_940070`／`0x53F470` | 鑑定結果 | S245 IdentifyDesc | `WorldSession.cs`、`DisplayNotices.cs` | 部分 | T152：說明窗版面 |
| 66 | S_MERCENARYEMPLOY | `sub_942690`／`0x541A90` | 傭兵雇用 | 無（`S_HireSoldier` opcode 已註解） | （無） | 缺 | SP〈容易混淆〉：S_HireSoldier 停用；伺服器不支援 |
| 57 | S_MERCENARYNAME | （無呼叫，case `0x9479CB`） | 傭兵名稱 | 無 | （無） | 缺 | case `0x9479CB` 無呼叫（直接落到下一格）；伺服器不支援 |
| 158 | S_MERCENARYSELECT | `sub_942840`／`0x541C40` | 傭兵選擇 | 無 | （無） | 缺 | 伺服器不支援 |
| 13 | S_MERCENARYARRANGE | `sub_942460`／`0x541860` | 傭兵配置 | 無（S117 PUTSOLDIER 無 serializer） | （無） | 缺 | SP S117；伺服器不支援 |
| 249 | S_ARCHERARRANGE | `sub_92E8D0`／`0x52DCD0` | 弓兵配置 | 無 | （無） | 缺 | 伺服器不支援 |
| 95 | S_TAX | `sub_94D8E0`／`0x54CCE0` | 城堡稅率 | S185 TaxRate | （無） | 缺 | SP S185；WIN2§5.1：開 NPC 對話窗 `tax` 頁（模式 1） |
| 12 | S_WITHDRAW | `sub_94FF20`／`0x54F320` | 城堡金庫提領 | S141 Drawal | （無） | 缺 | SP S141；WIN2§5.1：`withdraw` 頁（模式 2） |
| 136 | S_FIXABLE_ITEM_LIST | `sub_93F5E0`／`0x53E9E0` | 可修理物品清單 | S83 FixWeaponList | `WeaponRepair.cs`、`WeaponRepairUi.cs` | 部分 | WIN§2 T134：原版 `TRADE_WINDOW` 模式 6 |
| 252 | S_CASTLE_OWNER | `sub_9322C0`／`0x5316C0` | 城主（王冠） | S69 CastleMaster | （無） | 缺 | SP S69；WIN2§5.1：城主表 `dword_154434C`＋頭上 12 格王冠（500 ms／格，城 2 無王冠） |
| 91 | S_SOUND_EFFECT | `sub_94C4C0`／`0x54B8C0` | 音效 | S22 Sound | `SoundPackets.cs` | 部分 | SD#4–#9（G8／T173 審查中） |
| 49 | S_DEPOSIT | `sub_933F50`／`0x533350` | 城堡金庫存入 | S4 Deposit | （無） | 缺 | SP S4；WIN2§5.1：`deposit` 頁（模式 3） |
| 254 | S_MANA_POINT | `sub_941EA0`／`0x5412A0` | MP 更新 | S33 MPUpdate | `WorldSession.cs` | 已有 | SP S33 |
| 44 | S_BUYABLE_SPELL_LIST | `sub_931FF0`／`0x5313F0` | 可學魔法清單 | S41 SkillBuy | `NpcServices.cs`、`NpcUi.cs` | 部分 | 協定已有；原版學魔法窗未查證 |
| 1 | S_MASTER | `sub_942030`／`0x541430` | 主人名稱（寵物／召喚） | S87 欄位（無獨立封包） | `WorldObjects.cs` | 已有 | AN（t160_opmap：`sub_942030` 寫 +248 owner）；對應為推定 |
| 223 | S_ABILITY_SCORES | `sub_92C820`／`0x52BC20` | 能力值（六項、加成） | S155 OwnCharStatus2 | `BonusStats.cs` | 已有 | SP S155 |
| 41 | S_EXP | `sub_93F3B0`／`0x53E7B0` | 經驗值 | S113 Exp | `Rewards.cs` | 已有 | SP S113 |
| 106 | S_AC | `sub_92C900`／`0x52BD00` | AC 與屬性防禦 | S174 OwnCharAttrDef | `MagicStatus.cs` | 已有 | SP S174 |
| 219 | S_EFFECT_LOC | `sub_934530`／`0x533930` | 座標特效 | S106 EffectLocation | `SoundPackets.cs`、`MapEffects.cs` | 部分 | EF2§4（T227）：協定、位置、存活一致；差在素材覆蓋與 `]`（併入 FX2） |
| 166 | S_SELECTABLE_TIME_LIST | `sub_94BB90`／`0x54AF90` | 可選時間清單（攻城時間） | S_WarTime（opcode 由參數決定） | （無） | 缺 | SP〈容易混淆〉S_WarTime；C_ChangeWarTime 無啟用分派，伺服器不支援 |
| 52 | S_REQUEST_SUMMON | `sub_9495A0`／`0x5489A0` | 召喚怪物選擇 | S39 ShowSummonList | `Pets.cs`、`PetUi.cs` | 缺 | WIN§1.3 `CSummonUI`；引擎沒有召喚選擇窗 |
| 25 | S_MAGIC_STATUS | `sub_940F90`／`0x540390` | 魔法狀態（buff 圖示） | S216 SkillIconShield／S250 圖示子碼 | `MagicStatus.cs`、`StatusIcons.cs` | 部分 | UI§4（T199） |
| 22 | S_SLAVE_CONTROL | `sub_94C0E0`／`0x54B4E0` | 寵物控制 | S83 PetList／S64 PetCtrlMenu | `Pets.cs`、`PetUi.cs` | 部分 | WIN§1.5 `PetControl`（T127 ⚠️） |
| 71 | S_CHANGE_DESC | `sub_932510`／`0x531910` | 名稱變更 | S46 ChangeName | `WorldSession.cs` | 已有 | SP S46 |
| 84 | S_CHANGE_ALIGNMENT | `sub_932350`／`0x531750` | 正義值 | S34 Lawful | `WorldSession.cs`、`ObjectRendering.cs` | 部分 | AN§2.4 名稱色（T168） |
| 202 | S_PORTAL | `sub_947140`／`0x546540` | 傳送 | S149 Teleport | `Teleport.cs` | 已有 | R33–R35（T110） |
| 224 | S_HYPERTEXT_INPUT | `sub_93FDF0`／`0x53F1F0` | 數量／文字輸入詢問 | S136 InputAmount（ItemCount、HowManyMake） | （無） | 缺 | SP S136；WIN2§5.3：NPC 對話窗 HTML 頁＋數字框，不是 `InputWindow`；`agapply` 轉 `AgitAuctionUI` |
| 175 | S_EXCHANGEABLE_SPELL_LIST | `sub_93F200`／`0x53E600` | 可交換魔法清單（以物品學魔法） | S13 SkillBuyItem | （無） | 缺 | SP S13；引擎無接收 |
| 129 | S_NOT_ENOUGH_FOR_SPELL | `sub_945860`／`0x544C60` | 魔力不足等施法失敗 | S71 訊息代碼 | `Magic.cs` | 已有 | 伺服器改送 S71；對應為推定 |
| 155 | S_READ_MAIL | `sub_948E60`／`0x548260` | 讀信 | S186 Mail | `Mail.cs`、`MailUi.cs` | 部分 | WIN§2 T131：`RenewalMailUI` 本機缺檔 |
| 152 | S_BOARD_LIST | `sub_931C50`／`0x531050` | 公告板清單 | S68 Board | `Board.cs`、`BoardUi.cs` | 部分 | WIN§2 T130：`BoardListWindow` |
| 248 | S_BOARD_READ | `sub_931CA0`／`0x5310A0` | 公告板內文 | S148 BoardRead | `Board.cs`、`BoardUi.cs` | 部分 | WIN§2 T130：`BoardReadWindow` |
| 92 | S_EMOTION | `sub_935260`／`0x534660` | 表情動作 | S158 DoActionGFX（表情 action） | `TitleAndEmote.cs` | 部分 | AN§1（T169 審查中） |
| 189 | S_BLINK | `sub_931B90`／`0x530F90` | 傳送開始（回 C_BLINK 確認） | S149 Teleport（推定） | `Teleport.cs` | 部分 | PG§5：不讀欄位，回 C_BLINK（＝C52）；原版在移動中延後到該步結束才回，引擎立即回 |
| 89 | S_RESTART | `sub_981490`／`0x580890` | 重新開始 | C177 後的回包序列 | `Recovery.cs` | 部分 | WO 第 6 節：`UserPlayStatsUI` 死亡窗與逾時 |
| 245 | S_KICK | `sub_940960`／`0x53FD60` | 踢出 | S227 Disconnect | `WorldSession.cs` | 部分 | 引擎丟例外顯示錯誤；原版提示窗未查證 |
| 250 | S_HIT_RATIO | `sub_93FAD0`／`0x53EED0` | 頭上 HP／MP 比例條 | S237 HpMeter（推定） | `Combat.cs`、`WorldSession.cs` | 已有 | PG§5：`d c c`（物件、HP 比、MP 比）；3.81 只送 HP 比 |
| 110 | S_PLEDGE | `sub_946770`／`0x545B70` | 血盟資訊 | S72 ClanUpdate／S250 Pledge | `Clan.cs`、`ClanUi.cs` | 部分 | WIN2§3：`dssd` 名稱變更通知，不開窗；血盟窗由 S250 171 開（`RePledgeUI.xml`） |
| 24 | S_CRIMINAL | `sub_933D90`／`0x533190` | 犯罪者（粉紅名） | S60 PinkName | `DisplayNotices.cs`、`ObjectRendering.cs` | 部分 | AN§2.5（T168） |
| 4 | S_MAGE_STRENGTH | `sub_940E70`／`0x540270` | 力量提升 | S166 Strup | `TimedBuffs.cs` | 已有 | SP S166；圖示另見 T199 |
| 196 | S_MAGE_DEXTERITY | `sub_940C80`／`0x540080` | 敏捷提升 | S188 Dexup | `TimedBuffs.cs` | 已有 | SP S188 |
| 69 | S_MAGE_SHIELD | `sub_940D80`／`0x540180` | 魔法盾 | S216 SkillIconShield | `MagicStatus.cs` | 已有 | SP S216 |
| 132 | S_SHOW_MAP | `sub_94BFC0`／`0x54B3C0` | 以背包物品開地圖視窗 | S187 HouseMap（推定） | （無） | 缺 | PG§5：`d c`（物品 objid、頁）；PG 任務 D（低，需原生 UI） |
| 225 | S_WANTED_LOGIN | `sub_94F100`／`0x54E500` | 通緝登入通知 | 無 | （無） | 缺 | 伺服器不支援 |
| 0 | S_AGIT_LIST | `sub_92E550`／`0x52D950` | 盟屋清單 | S156 HouseList（AuctionBoard） | （無） | 缺 | SP S156；WIN2§5.2：欄位完全一致；`AgitAuctionUI` XML 缺資源 |
| 18 | S_AGIT_MAP | `sub_92E710`／`0x52DB10` | 盟屋地圖 | S187 HouseMap | （無） | 缺 | SP S187；WIN2§5.2：開舊式 `AgitMapWindow`（區域＝高 16 位元、編號＝低 16 位元） |
| 45 | S_WARNING_CODE | `sub_94FB00`／`0x54EF00` | 紅字警告 | S105 BlueMessage | `DisplayNotices.cs` | 部分 | UI§2 V2（T200） |
| 17 | S_BREATH | `sub_931F60`／`0x531360` | 水中呼吸 | S126 SkillIconBlessOfEva | `TimedBuffs.cs`、`WorldSession.cs` | 部分 | EF2§3（T227）：原版只加圖示 effect 11（`552.tbt`），無畫面特效；差異只在圖示（併入狀態圖示任務） |
| 198 | S_PING | `sub_9465A0`／`0x5459A0` | Ping | 無（C95 KeepAlive 無回包） | `WorldEntry.cs` | 已有 | SP C95；原版回應方式未查 |
| 172 | S_CLONE | `sub_933640`／`0x532A40` | 分身／外觀複製 | S76 Poly 的 `S_ChangeShape(pc,npc,poly)` 型（推定） | `Polymorph.cs` | 已有 | PG§5：`ddhhcs`，交給變身函式；引擎兩種 S76 都解析 |
| 228 | S_COMMAND_TARGET | `sub_933760`／`0x532B60` | 指令目標（寵物攻擊目標） | S236 SelectTarget | （無） | 缺 | SP S236；引擎無接收 |
| 108 | S_EVENT | `sub_9357E0`／`0x534BE0` | PACKETBOX 子碼（158 個 case） | S250 PACKETBOX | `Party.cs`、`Duel.cs`、`MagicStatus.cs`、`LocationShare.cs`、`CharacterConfig.cs`、`Buddies.cs`、`Clan.cs`、`BasicSupplies.cs`、`CharacterManagement.cs` | 部分 | PG§2.2：子碼與 3.81 `S_PacketBox` 一致；伺服器會送 62 個，引擎處理其中 23 個，缺 39 個（PG§2.3 B1–B3）；B3 已逐一查（PBW§2）：開窗的只有 52／117／146，66、146 伺服器沒有呼叫點，190／192 是伺服器自訂格式；可開 PBW-A（83／84）、PBW-C–E |
| 216 | S_ADD_INVENTORY_BATCH | `sub_92D360`／`0x52C760` | 背包整批 | S5 InvList | `Inventory.cs` | 已有 | SP S5 |
| 238 | S_BOOK_LIST | `sub_931CC0`／`0x5310C0` | 書籤清單 | S64 BookmarkList | `Bookmarks.cs` | 部分 | T124 審查中；UI 見 T198 |
| 29 | S_PERSONAL_SHOP_LIST | `sub_9464D0`／`0x5458D0` | 個人商店清單 | S140 PrivateShop | `PrivateShop.cs`、`PrivateShopUi.cs` | 部分 | WIN§2 T102（模式 13／14） |
| 46 | S_SERVER_LIST | `sub_ECB450`／`0xACA850` | 伺服器清單 | 無（單一伺服器） | （無） | 缺 | 伺服器不支援 |
| 99 | S_MAIL_INFO | `sub_941000`／`0x540400` | 信件清單 | S186 Mail | `Mail.cs` | 部分 | WIN§2 T131 |
| 43 | S_VOICE_CHAT | `sub_94E0E0`／`0x54D4E0` | 能力值重置（名稱表為 VOICE_CHAT） | S64 CharReset 子類 1–3 | `CharReset.cs`、`CharResetUi.cs` | 部分 | WIN§2 T142：`sub_94E0E0` 開 `CResetStatUI` |
| 133 | S_MATCH_MAKING | `sub_B243A0`／`0x7237A0` | 血盟推薦 | S0 PledgeRecommend | （無） | 缺 | SP S0；引擎無接收 |
| 78 | S_PLEDGE_WATCH | `sub_B2E1D0`／`0x72D5D0` | 血盟注意（關注） | S200 ClanAttention | `Clan.cs` | 部分 | 協定已有；`sub_B2E1D0` 的 UI 未查 |
| 19、39、109 | S_EXTENDED_PROTOBUF／S_EXTENDED_HYBRID／S_EXTENDED | `sub_926BF0`／`0x525FF0`；protobuf `sub_E428F0`、`sub_E42A50` | protobuf 擴充封包（H message id，125 個） | 無（S250 的對應是 op 108） | （無） | 缺 | PG§2.1：商城、製作、競技場、攻城、通知等新系統；伺服器不支援，不建議補 |

## 2. 收包：第二組（登入期）與第三組

已驗證：`[0x13CE3D8]!=3` 時，`0x947E8A` 起用 opcode−6 查 index `0x9483D4`（FO `0x5477D4`）→ target `0x94838C`（FO `0x54778C`），有 17 個處理區塊，其餘落到結尾 `0x9480AE`（不處理）。第三組在 `0x948056` 把子 id 減 `0x13B`，查 index `0x9484CC` → target `0x9484C4`，只有一個處理區塊 `0x948078`（FO `0x547478`），子 id 315、450、466、468、469、483、538、549 交給 protobuf 處理 `sub_E428F0`／`sub_E42A50`。

| TW16 op | 名稱 | 處理函式 VA／FO | 功能 | 3.81 對應 | 引擎對應檔案 | 狀態 | 依據 |
|---|---|---|---|---|---|---|---|
| 231 | S_KEY | `sub_940750`／`0x53FB50` | 連線金鑰（首包） | S150 INITPACKET | `LoginConnection.cs`、`Network.cs` | 已有 | SP〈查表方式〉首包 18-byte |
| 107 | S_VERSION_CHECK | `sub_94DBB0`／`0x54CFB0` | 版本檢查回應 | S139 ServerVersion（回 C14） | （無） | 缺 | PG§4：3.81 不要求 C14（屬 TRANSPORT），引擎不送也能登入；不建議為登入補，選做 PG 任務 E（語系與功能旗標） |
| 81 | S_LOGIN_CHECK | `sub_EC9D90`／`0xAC9190` | 登入結果 | S233 LoginResult | `LoginConnection.cs`、`LoginProtocol.cs` | 已有 | R03（T111） |
| 179 | S_NEWS | `sub_ECAA10`／`0xAC9E10` | 登入公告 | S48 CommonNews | `LoginConnection.cs` | 部分 | 引擎收下後自動回 C16，不顯示；PBW§5.3：原版以通用模態訊息框 `sub_8E1E30` 顯示（並存 `Capture\NewsNNNN.txt`），**不是** `CNoticeWindow` |
| 212 | S_NOTICE | `sub_ECABC0`／`0xAC9FC0` | 登入期字串表彈窗 | 無（3.81 用 S233 reason 與 S48） | （無） | 缺 | PG§5：讀 `h` 字串 id 開模態框；伺服器不支援，不建議補 |
| 190 | S_NUM_CHARACTER | `sub_9459C0`／`0x544DC0` | 角色數量 | S178 CharAmount | `LoginConnection.cs`、`CharacterManagement.cs` | 已有 | R03–R06 |
| 31、204 | S_CHARACTER_INFO／S_NEW_CHAR_INFO | `sub_933040`／`0x532440` | 角色清單、新角色 | S93 CharPacks／S127 NewCharPack | `LoginConnection.cs`、`CharacterManagement.cs` | 已有 | R03–R06（T107） |
| 42、67、83、215 | S_CHANGE_PASSWORD_CHECK、S_CHANGE_ACCOUNTINFO_CHECK、S_NEW_ACCOUNT_CHECK、S_CREATE_CHARACTER_CHECK | `sub_ECB0F0`／`0xACA4F0` | 建角結果（另三種是帳號操作） | S98 NewCharWrong（只對建角） | `CharacterManagement.cs` | 部分 | 建角結果已有（T107）；改密碼、改帳號、新帳號在 3.81 無啟用分派，伺服器不支援 |
| 143 | S_DELETE_CHARACTER_CHECK | `sub_EC8C40`／`0xAC8040` | 刪角結果 | S6 DeleteCharOK | `CharacterManagement.cs` | 已有 | R05 |
| 117 | S_ENTER_WORLD_CHECK | `sub_EC9330`／`0xAC8730` | 進入世界確認 | S223 LoginToGame | `WorldSession.cs`、`WorldEntry.cs` | 已有 | SP S223 |
| 46 | S_SERVER_LIST | `sub_ECB450`／`0xACA850` | 伺服器清單（第二組） | 無 | （無） | 缺 | 伺服器不支援 |
| 6 | S_ROLL_RESULT | `sub_94ADB0`／`0x54A1B0` | 建角擲骰 | 無 | （無） | 缺 | 伺服器不支援（SP 全表沒有對應封包） |
| 245、108、43、109、19、39 | （與第一組同名） | 同第一組 | 登入期也收 | 同第一組 | 同第一組 | — | 只計一次：本列不另計功能，狀態取第一組最差者 |
| 315、450、466、468、469、483、538、549 | （protobuf 子 id） | case `0x948078`／`0x547478` → `sub_E428F0`／`0xA41CF0`、`sub_E42A50`／`0xA41E50` | 新版 protobuf 訊息 | 無（3.81 沒有 protobuf） | （無） | 缺 | 伺服器不支援 |

## 3. 送包：`sub_951F80` 名稱表

已驗證：`sub_951F80`（VA `0x951F80`／FO `0x551380`）以 opcode−3 查 index `0x952870`（FO `0x551C70`）→ jump `0x952640`（FO `0x551A40`），共 139 個 `C_*` 名稱（`gap_inventory.py` 的 `send`，與 `wo_copnames.py` 相同）。送出端的實際組包函式只在 WO、SV、WIN 查過的部分有地址；其餘只到名稱表。下表一列一個功能，TW16 每個送包名稱只出現一次。

| TW16 送包（op） | 功能 | 3.81 C 對應 | 引擎對應檔案 | 狀態 | 依據 |
|---|---|---|---|---|---|
| C_LOGIN(85)、C_LOGIN_TEST(72)、C_LOGIN_RESULT(18)、C_ENTER_WORLD(168)、C_CLIENT_READY(202)、C_READ_NEWS(110)、C_READ_NOTICE(27) | 登入、選角進入、公告確認 | C119、C137、C26、C16 | `LoginConnection.cs`、`LoginProtocol.cs`、`WorldSession.cs` | 已有 | R03；引擎未送 C26 與 C_LOGIN_TEST，是否必要未查 |
| C_VERSION(122) | 版本 | C14 | （無） | 缺 | PG§4：S_KEY 後送 `H D C D D C`；3.81 不要求，不建議為登入補（選做 PG 任務 E） |
| C_CREATE_CUSTOM_CHARACTER(35)、C_DELETE_CHARACTER(81)、C_RESTART(194) | 建角、刪角、切角 | C84、C162、C7 | `CharacterManagement.cs` | 已有 | R04–R06（T107） |
| C_NEW_ACCOUNT(52)、C_CHANGE_PASSWORD(19)、C_CHANGE_ACCOUNTINFO(104)、C_SERVER_SELECT(3)、C_SHIFT_SERVER(29)、C_REQUEST_ROLL(236) | 帳號、伺服器、擲骰 | 無啟用分派 | （無） | 缺 | 伺服器不支援 |
| C_MOVE(175)、C_CHANGE_DIRECTION(79) | 移動、轉向 | C29、C225 | `OnlineMovement.cs` | 已有 | R08 |
| C_ATTACK(43)、C_ALT_ATTACK(96)、C_ATTACK_CONTINUE(130)、C_FAR_ATTACK(4) | 近戰、遠程攻擊 | C229、C123 | `Combat.cs`、`RangedIntegration.cs` | 部分 | PG§6：C_ALT_ATTACK 在 847 個送出點都沒有（推定未使用）；C_ATTACK_CONTINUE 是伺服器端連續攻擊，受旗標 0x800000 控制，3.81 旗標未開，不補 |
| C_USE_SPELL(216)、C_START_CASTING(144) | 施法 | C6 | `MagicIntegration.cs`、`Magic.cs` | 部分 | SV§1：送出時不該本地播動作（G12） |
| C_SAY(16)、C_CHAT(170)、C_TELL(67)、C_CHAT_PARTY_CONTROL(120) | 聊天、密語、隊伍聊天 | C136、C40、C184、C199 | `PlayerChat.cs` | 部分 | UI§3（T175）；C199 引擎未送 |
| C_CHANNEL(55) | 發送座標 | C254 SENDLOCATION | `LocationShare.cs` | 部分 | WO 第 5 節：地區 id、入口不同 |
| C_GET(23) | 拾取 | C112 | `Ground.cs`、`GroundUi.cs` | 部分 | WO 第 2 節：距離、數量、節流 986 ms |
| C_DROP(97)、C_GIVE(137)、C_DESTROY_ITEM(198) | 丟、給、刪物品 | C25、C45、C138 | `Ground.cs`、`ItemTransfer.cs` | 部分 | M6 T133 ❓（刪除本地確認待查） |
| C_USE_ITEM(208)、C_CHECK_INVENTORY(160) | 使用物品、背包確認 | C164、C99 | `Inventory.cs`、`ItemUseArguments.cs` | 已有 | R24（T109） |
| C_OPEN(131) | 開關門 | C41 | `Doors.cs` | 部分 | WO 第 3 節：1 格內才送 |
| C_DIALOG(51)、C_HACTION(227)、C_HYPERTEXT_INPUT_RESULT(188) | NPC 對話、HTML 動作、輸入結果 | C34、C125、C11 | `NpcInteraction.cs` | 部分 | C11（數量輸入回覆）引擎未送，見第 1 節 op 224 |
| C_BUY_SELL(89)、C_WAREHOUSE_CONTROL(71) | 買賣、倉庫存取 | C38 | `NpcInteraction.cs`、`NpcServices.cs` | 部分 | WIN§2 T102 |
| C_BUYABLE_SPELL(24)、C_BUY_SPELL(123) | 學魔法 | C145、C39 | `NpcServices.cs` | 部分 | 原版學魔法窗未查證 |
| C_EXCHANGEABLE_SPELL(125)、C_EXCHANGE_SPELL(210) | 以物品學魔法 | C245、C191 | （無） | 缺 | 見第 1 節 op 175 |
| C_FIXABLE_ITEM(233)、C_FIX(228) | 武器修理 | C118 | `WeaponRepair.cs` | 部分 | WIN§2 T134 |
| C_ASK_XCHG(5)、C_ADD_XCHG(247)、C_ACCEPT_XCHG(111)、C_CANCEL_XCHG(195) | 交易 | C2、C37、C71、C86 | `Trade.cs`、`TradeUi.cs` | 部分 | 交易窗不是原版 `TRANSACTION_WINDOW` |
| C_QUERY_PERSONAL_SHOP(86)、C_PERSONAL_SHOP(98) | 私商查詢、開店 | C47、C161／C38 | `PrivateShop.cs`、`PrivateShopUi.cs` | 部分 | WIN§1.1：沒有 `PersonalShopUI` 開店設定窗 |
| C_ANSWER(33) | Yes/No 回覆 | C121 Attr | `Party.cs`、`Duel.cs`、`Trade.cs`、`Marriage.cs`、`Clan.cs`、`ClanWar.cs`、`BonusStats.cs` | 部分 | WIN§2.1、WO 第 6 節 |
| C_INVITE_PARTY(82)、C_INVITE_PARTY_TARGET(42)、C_LEAVE_PARTY(119)、C_BANISH_PARTY(65)、C_WHO_PARTY(108) | 組隊 | C230、C33、C255、C43 | `Party.cs`、`PartyUi.cs` | 部分 | UI#13（T197） |
| C_CREATE_PLEDGE(26)、C_JOIN_PLEDGE(141)、C_LEAVE_PLEDGE(112)、C_BAN(46)、C_BAN_MEMBER(105)、C_WHO_PLEDGE(135)、C_RANK_CONTROL(10) | 血盟 | C222、C194、C61、C69、C68、C63 | `Clan.cs`、`ClanUi.cs` | 部分 | AN§2（T135 審查中）；C63 階級設定引擎未送 |
| C_EMBLEM(28)、C_UPLOAD_EMBLEM(20) | 盟徽取得、上傳 | C72、C18 | `ClanEmblem.cs`、`Clan.cs` | 部分 | AN§2.8（G4）；上傳 C18 引擎未送 |
| C_PLEDGE_WATCH(201) | 血盟注意 | C129 | `Clan.cs` | 部分 | 協定已有，UI 未查 |
| C_MATCH_MAKING(221) | 血盟推薦 | C76、C78 | （無） | 缺 | 見第 1 節 op 133 |
| C_WAR(197) | 宣戰 | C227 | `ClanWar.cs` | 部分 | AN#15 |
| C_DUEL(118) | 決鬥 | C5 | `Duel.cs`、`DuelUi.cs` | 部分 | AN#14（T168） |
| C_MARRIAGE(21) | 求婚 | C50 | `Marriage.cs` | 部分 | WIN§2 T141 ❌ |
| C_ADD_BUDDY(138)、C_REMOVE_BUDDY(124)、C_QUERY_BUDDY(56)、C_EXCLUDE(7)、C_INCLUDE(217) | 好友、封鎖 | C207、C202、C4、C171 | `Buddies.cs`、`BuddyUi.cs` | 部分 | WIN§1.2；信件封鎖缺 |
| C_WHO(174)、C_CHECK_PK(9) | 查詢玩家、PK 查詢 | C206、C51 | `PlayerQuery.cs`、`WeaponRepair.cs` | 部分 | WIN§2 T140、T134 |
| C_BOOKMARK(84)、C_DELETE_BOOKMARK(106)、C_BOOK(126) | 書籤 | C165、C3 | `Bookmarks.cs` | 部分 | T124 審查中；C_BOOK 在 847 個送出點都沒有（PG§6，仍待驗證） |
| C_TELEPORT(226)、C_TELEPORT_USER(31)、C_GOTO_PORTAL(200)、C_ENTER_PORTAL(101)、C_GOTO_MAP(60) | 傳送、傳送門、下船 | C52、C219、C231 | `Teleport.cs`、`Portals.cs`、`ShipTravel.cs` | 部分 | R33–R36；PG§6：C_TELEPORT_USER 是 GM 傳送清單（不補）；C_GOTO_MAP＝C231 下船 `H H H`（已有） |
| C_ENTER_SHIP(185) | 搭船 | C231 | `ShipTravel.cs` | 已有 | SP C231 |
| C_DEAD_RESTART(58)、C_QUIT(68)、C_LOGOUT(235) | 死亡重新開始、離開 | C177、C122 | `Recovery.cs` | 部分 | WO 第 6 節：死亡窗、約 8 分 21 秒逾時 `C_QUIT` |
| C_EXIT_GHOST(179) | 離開幽靈 | C173 | `GhostMode.cs` | 部分 | WO 第 4 節 |
| C_ACTION(50) | 表情 | C120 ExtCommand | `TitleAndEmote.cs` | 部分 | AN§1（T169） |
| C_TITLE(214) | 稱號 | C90 | `TitleAndEmote.cs` | 已有 | SP C90 |
| C_SLAVE_CONTROL(45)、C_RETURN_SUMMON(147)、C_NPC_ITEM_CONTROL(37)、C_SUMMON(164) | 寵物指令、解散、寵物道具、召喚選擇 | C103、C104、C125（summonlist 動作） | `Pets.cs`、`PetUi.cs` | 部分 | WIN§1.3：沒有召喚選擇窗 |
| C_BOARD_LIST(6)、C_BOARD_READ(93)、C_BOARD_WRITE(91)、C_BOARD_DELETE(222) | 公告板 | C10、C23、C114、C141、C153 | `Board.cs`、`BoardUi.cs` | 部分 | WIN§2 T130 |
| C_MAIL(95) | 信件 | C87 | `Mail.cs`、`MailUi.cs` | 部分 | WIN§2 T131 ❓ |
| C_SAVEIO(128)、C_SAVE(150) | 角色設定存取 | C244 | `CharacterConfig.cs` | 部分 | UI#5（T176）；C_SAVE 在 847 個送出點都沒有（PG§6，仍待驗證） |
| C_VOICE_CHAT(232) | 能力值重置（名稱表為 VOICE_CHAT，推定） | C98 | `CharReset.cs` | 部分 | WIN§2 T142 |
| C_ALIVE(176) | 保持連線 | C95 | `WorldEntry.cs` | 已有 | SP C95 |
| C_TAX(41)、C_DEPOSIT(36)、C_WITHDRAW(59)、C_CHANGE_CASTLE_SECURITY(63)、C_QUERY_CASTLE_SECURITY(171) | 城堡稅率、金庫、治安 | C19、C56、C44（治安 C128 無啟用分派） | （無） | 缺 | 見第 1 節 op 95／49／12 |
| C_SELECTABLE_TIME(167)、C_SELECT_TIME(109) | 攻城時間 | 無啟用分派 | （無） | 缺 | 伺服器不支援 |
| C_MERCENARYSELECT(14)、C_MERCENARYNAME(148)、C_MERCENARYARRANGE(169)、C_ARCHERARRANGE(190)、C_MERCENARYEMPLOY(253) | 傭兵 | C_HireSoldier 停用 | （無） | 缺 | 伺服器不支援 |
| C_EXTENDED(53)、C_EXTENDED_PROTOBUF(157)、C_EXTENDED_HYBRID(207) | 擴充送包 | C120 等／無 protobuf | 各模組 | 部分 | 子碼未列，需先查證 |
| C_REGISTER_QUIZ(100)、C_WISH(254)、C_ADDR(64) | 帳號安全問答、GM 文字指令、反外掛記憶體雜湊 | 無 | （無） | 缺 | PG§6：伺服器不支援，不建議補 |
| C_THROW(146)、C_BLINK(139)、C_PLATE(121) | 結束釣魚、傳送確認、點看板 | C62、C52、C10（推定） | `Fishing.cs`、`Teleport.cs`、`Board.cs` | 已有 | PG§6 |
| C_ONOFF(184) | 聊天頻道與拒收開關 | C26（`C_LoginToServerOK` type／button） | （無） | 缺 | PG§6：3.81 處理 255／0／2／6；PG 任務 C |
| C_SMS(224)、C_WANTED(231)、C_KICK(252)、C_SILENCE(223)、C_SHUTDOWN(140)、C_CONTROL_WEATHER(132)、C_BUILDER_CONTROL(161)、C_MONITOR_CONTROL(178) | 簡訊、通緝、GM 指令 | C253 無啟用；GM 走聊天指令 | （無） | 缺 | 伺服器不支援（GM 功能不在範圍） |

## 4. UI 視窗

### 4.1 XML 視窗（`sub_B56A40` 依名稱建立）

已驗證：`sub_B56A40`（VA `0xB56A40`／FO `0x755E40`，12,870 bytes）依名稱比對 80 個 UI；79 個會 `new` 出物件，**`MatchMakingUI` 的分支直接回傳、不建立任何物件**。類別名稱由建構子寫入的 vtable 往回讀 RTTI（`ui_classes.py`）。XML 檔名表在 `0x137B39C`–`0x137B4BC`（FO `0xF79B9C` 起，WIN§0.1）。「本機 XML」指 `Tile.pak` 的 65 個檔中有沒有（#448 解密）。

| UI 名稱 | 類別（RTTI） | 建構子 VA／FO | 本機 XML | 功能 | 引擎對應 | 狀態 | 依據 |
|---|---|---|---|---|---|---|---|
| LoginUI | CLoginUI | `0xEE8260`／`0xAE7660` | 缺檔 | 登入畫面 | `LoginControl.cs`、`ClientLogin.cs`（WinForms） | 部分 | EF2§5（T227）：本機所有版本都沒有 `LoginUI.xml`，缺資源 |
| SelectUI | CSelectUI | `0xEEBB10`／`0xAEAF10` | 缺檔 | 選角畫面 | `CharacterManagementUi.cs` | 部分 | WIN§2 T107；EF2§5（T227）：本機所有版本都沒有，缺資源 |
| CreateUI | CCreateUI | `0xEE2DF0`／`0xAE21F0` | 缺檔 | 建角畫面 | `CharacterManagementUi.cs` | 部分 | WIN§2 T107；EF2§5（T227）：只有他版本（89 參考 PAK）的 `createui.xml`，15 個事件名稱與 TW16 相容，surf 7091–7225 不在本機客戶端 |
| NewPasswordUI | NewPasswordUI | `0x907580`／`0x506980` | 缺檔 | 改密碼 | （無） | 缺 | 伺服器不支援 |
| LoadingWindowUI | LoadingWindowUI（Lineage） | `0x6EEEB0`／`0x2EE2B0` | 缺檔 | 讀取畫面 | `WorldEntry.cs`（文字狀態） | 部分 | 需先查證（3.81 世代是否有讀取畫面） |
| MainChatUI | MainChatUI | `0x7021B0`／`0x3015B0` | 缺檔 | 新版聊天 widget | 原版 800×600 走 `uistatus86` 的 `CENTER_WINDOW`：`PlayerHudChat.cs` | 部分 | UI§3、§2 V5：本機無 widget XML，3.81 世代用 `uistatus86`（T195） |
| MainButtonUI | MainButtonUI | `0x6FBA30`／`0x2FAE30` | 缺檔 | 新版選單鈕 widget | `PlayerHudView.cs` | 部分 | 同上（T194） |
| MiniMapUI | MiniMapUI | `0x707020`／`0x306420` | 缺檔 | 新版小地圖 widget | `Minimap.cs`、`MinimapUi.cs` | 部分 | UI§5（T196） |
| WidgetSlot、WidgetSubSlot | SlotWidgetUI、SubSlotWidgetUI | `0x9F82E0`／`0x5F76E0`、`0xF6B4A0`／`0xB6A8A0` | 缺檔 | 新版快捷格 widget | `PlayerHudView.cs`、`CharacterConfig.cs` | 部分 | UI§2 V5（T176、T193） |
| MainCharInfoUI | MainCharInfoUI（Lineage） | `0x6FE180`／`0x2FD580` | 缺檔 | 新版角色資訊 widget | `PlayerHudView.cs` | 部分 | UI§2（T191） |
| MapUI | MapUI | `0x8DE770`／`0x4DDB70` | `MapSystem-c.xml` | 地圖視窗 | `Minimap.cs`、`MinimapUi.cs` | 部分 | UI§5（T196） |
| ZoneNameNotice | CZoneNameNotice | `0xF2D390`／`0xB2C790` | 缺檔 | 進入區域名稱提示 | （無） | 缺 | WIN2§6：只有 `sub_B56A40` 引用，推測由自身事件委派觸發；3.81 不需封包，不建議做 |
| HyperTextUI、HyperTextSubUI | HyperTextUI、HyperTextSubUI | `0x875810`／`0x474C10`、`0xA7F770`／`0x67EB70` | `uiextra86-c.xml`（`HYPER_TEXT_WINDOW`） | NPC 對話、HTML 頁 | `NpcUi.cs`、`NpcInteraction.cs`（WinForms 清單） | 部分 | WIN§2.1 |
| NpcDialogUI | CNpcDialogUI | `0x660550`／`0x25F950` | 缺檔 | 新版 NPC 對話 | 同上 | 部分 | 3.81 世代用 `HYPER_TEXT_WINDOW`，本列不另做 |
| Inventory | RenewalInventoryUI | `0x9A40D0`／`0x5A34D0` | 缺檔（`ReInventory.xml`） | 背包 | `InventoryUi.cs`（WinForms） | 部分 | WIN2§2：TW16 熱鍵只開本窗；3.81 世代資源沒有 `ReInventory.xml`，版面改用 `uimagicitem.xml` `ITEM_WINDOW`（W2-I） |
| Warehouse、PetWarehouse、AdditionalServiceWarehouse | RenewalWareHouseUI、RenewalPetWareHouseUI、RenewalAdditionalServiceWareHouseUI | `0x9CB850`／`0x5CAC50`、`0x9B8EA0`／`0x5B82A0`、`0x983AE0`／`0x582EE0` | 缺檔 | 個人／寵物／附加倉庫 | `NpcServices.cs`、`NpcUi.cs` | 部分 | WIN2§2：S_RETRIEVE_LIST 類型 3／5／9／18 一律開本窗；3.81 世代版面用 `uiextra86.xml` `WAREHOUSE_WINDOW`（W2-I）；寵物／附加倉庫 3.81 不送 |
| ExchangeUI | ExchangeUI（Lineage） | `0x82EFC0`／`0x42E3C0` | 缺檔 | 交易 | `TradeUi.cs` | 部分 | 3.81 世代用 `uiextra86` 的 `TRANSACTION_WINDOW`（WIN§1.5） |
| PersonalShopUI | PersonalShopUI | `0xDDC740`／`0x9DBB40` | 有 | 個人商店開店設定 | （無） | 缺 | WIN§1.1 |
| ShopUI | CTamShopUI | `0xDEDC50`／`0x9ED050` | 缺檔（`TAMShopUI.xml`） | TAM 點數商店 | （無） | 缺 | 伺服器不支援 |
| ItemShopUI | ItemShop | `0xBE6F30`／`0x7E6330` | 缺檔 | 商城 | （無） | 缺 | 伺服器不支援 |
| SpellUI | SpellUI（LineageSpell） | `0xE2E5A0`／`0xA2D9A0` | 缺檔（`SpellList.xml`） | 魔法清單 | `MagicUi.cs` | 部分 | WIN2§2：熱鍵開本窗；`SpellList.xml` 是資料表不是版面；3.81 世代版面用 `uimagicitem.xml` `SPELL_WINDOW`（W2-I） |
| PartyUI | PartyUI（Lineage） | `0x9570C0`／`0x5564C0` | 無專用檔（`uiextra86-c.xml` 的 `PARTY_INTERFACE`） | 隊伍 | `PartyUi.cs` | 部分 | UI#13（T197） |
| PledgeUI | PledgeUI | `0xB2A0C0`／`0x7294C0` | 讀 `RePledgeUI.xml`（缺檔；本機 `PledgeUI.xml` 是 3.81 世代版，`PledgeFocusUI.xml` 不讀） | 血盟 | `ClanUi.cs` | 部分 | WIN2§3：TW16 讀 `RePledgeUI.xml`（本機缺）；S250 167–171 開窗，3.81 只有 169、171 可用；版面用本機 `PledgeUI.xml`；`PledgeFocusUI.xml` TW16 不讀（W2-H） |
| PledgeNoticeUI | PledgeNoticeUI | `0x95D8F0`／`0x55CCF0` | 缺檔 | 血盟公告 | `Clan.cs`、`ClanUi.cs` | 部分 | 協定已有（S167），視窗需先查證 |
| CommunityUI | RenewalCommunityUI | `0x985B40`／`0x584F40` | 缺檔（`ReCommunityUI.xml`） | 社群（好友、血盟整合） | `BuddyUi.cs` | 部分 | 3.81 世代用 `FriendUI` 系列（下列） |
| FriendUI、ExcludeUI、MailExcludeUI | FriendUI、ExcludeUI、MailExcludeUI | `0xB17DD0`／`0x7171D0`、`0xB17D10`／`0x717110`、`0xB17E60`／`0x717260` | 有 | 好友、封鎖、信件封鎖 | `Buddies.cs`、`BuddyUi.cs` | 部分 | WIN§1.2：引擎單一分頁、無信件封鎖 |
| BookmarkWindowUI | CBookmarkWindowRenewal | `0xBD9F50`／`0x7D9350` | 有（`BookmarkUI.xml`） | 書籤 | `BookmarkUi.cs` | 部分 | UI§6（T124、T198） |
| BookmarkMemoryWindowUI | CBookmarkMemoryWindow | `0xBD39E0`／`0x7D2DE0` | 有（`BookmarkMemUI.xml`） | 書籤記憶 | （無） | 缺 | WIN2§6：第一層由 S_EVENT（op 108）開啟（子碼推測為 141 `BOOKMARK_EXPAND`），併 T198 |
| RenewalMailUI | RenewalMailUI | `0x9AEE70`／`0x5AE270` | 缺檔 | 信件 | `MailUi.cs` | 部分 | WIN2§8：本機缺；kor25／pmc／c760／t89／ly 客戶端有，事件 22／23 在 BIN；本機缺 surf 9183、9184 |
| SummonUI | CSummonUI | `0xBFFB60`／`0x7FEF60` | 有（＋`summonList.xml`） | 召喚怪物選擇 | （無） | 缺 | WIN§1.3 |
| PetSummonUI | CPetSummonUI | `0x759BF0`／`0x358FF0` | 缺檔 | 寵物召喚 | （無） | 缺 | WIN2§6：3.81 召喚走 S39 `summonlist`（`CSummonUI`，M7-W）；不建議另做 |
| PolymorphUI | CPolymorphUI | `0xBF8C00`／`0x7F8000` | 有（＋`polymorphList.xml`、`Polymorphlist2.xml`） | 變身選擇 | （無；3.81 伺服器送 S39 變身清單 HTML） | 缺 | WIN2§4：3.81 的 S39 `monlist` 走 NPC 對話窗 HTML；本窗只在選項 `byte_135C1EA` 開啟時由道具或 S_ASK 180／289 開，不建議做 |
| SpecialPolymorphUI | CSpecialPolymorphUI | `0xA09770`／`0x608B70` | 有（`SPolymorphUI.xml`＋`SPolymorphList.xml`） | 特殊變身選擇 | （無） | 缺 | WIN2§4：由 S_EVENT 開，確定鈕送 TW16 op 208（用道具＋名稱）；3.81 不送開窗封包，不建議做 |
| ResetStatUI | CResetStatUI | `0x9CF150`／`0x5CE550` | 缺檔 | 能力值重置 | `CharResetUi.cs` | 部分 | WIN2§8：他版客戶端有 `ResetStatUI.xml`，事件 8／8；本機缺 23 個 surf，只能部分重現 |
| RenewalStatusUI | CRenewalStatusUI | `0xEDCBE0`／`0xADBFE0` | 讀 `ReNStatusUIEx.xml`（缺檔） | 裝備／能力窗 | `BonusStatsUi.cs`、`InventoryUi.cs` | 部分 | WIN§1.4：本機 `ReNStatusWinUI-c.xml` TW16 不讀 |
| StatInfoUI | StatInfoUI | `0xEDCF00`／`0xADC300` | 缺檔 | 能力說明 | （無） | 缺 | WIN2§6：由 op 43（重置）開；隨重置窗處理（WIN2§8） |
| UserPlayStatsUI | CUserPlayStatsUI | `0xC0AB50`／`0x809F50` | 有（`UserPlayUI.xml`） | 死亡統計窗 | `Recovery.cs`（自製面板） | 部分 | WO 第 6 節（T151 審查中） |
| UserObtainUI、FishObtainUI | CUserObtainUI（同一建構子） | `0xC03330`／`0x802730` | `UserObtainUI.xml` 有；`FishObtainUI.xml` 缺檔 | 取得物品提示 | （無） | 缺 | WIN2§6：背包新增／移除時的本機提示（op 30／216／98／164）；`FishObtainUI.xml` 他版客戶端有（WIN2§8）；低優先 |
| FishingUI | CFishingUI（LineageFishing） | `0xE65660`／`0xA64A60` | 缺檔 | 釣魚 | `Fishing.cs`（HUD 按鈕） | 部分 | WIN2§8：他版客戶端有 `FishingUI.xml`，事件 2／2；圖只有 `.tbt`；開窗為本機觸發，S250 55 只顯示訊息 |
| BehaviourUI | BehaviourUI（Lineage） | `0x7C6910`／`0x3C5D10` | 有（`Actions.xml`，另有 `ActionUI.xml`） | 動作／表情選單 | `TitleAndEmote.cs` | 部分 | AN§1（T169） |
| QuickMotionUI | QuickMotionUi（Lineage） | `0xEAA290`／`0xAA9690` | 缺檔（`qmotioninfo.xml`） | 快速表情 | （無） | 缺 | WIN2§6：本機觸發，TW16 新內容；不建議做 |
| OptionUI | OptionUI（Lineage） | `0x919170`／`0x518570` | 缺檔 | 選項設定 | `PlayerAudioSettings.cs`、`ClientAudioSettings.cs`（只有音量） | 部分 | WIN2§6：本機觸發；含新式變身窗選項 `byte_135C1EA`（`optiong` 頁） |
| PopUpUI、PopupMsgUI | PopUpUI、PopupMsgUI | `0x95FA00`／`0x55EE00`、`0x95F510`／`0x55E910` | 缺檔 | 彈出訊息 | `DisplayNotices.cs` | 部分 | WIN2§6：本機觸發 |
| GameRankingUI | CGameRankWin | `0x848830`／`0x447C30` | 缺檔 | 排名 | `PlayerQuery.cs`（聊天行） | 部分 | WIN§2 T140 |
| LinHelperUI | L1HelperUI | `0xDF3200`／`0x9F2600` | 有 | 新手幫手 | （無） | 缺 | WIN2§6：本機觸發，TW16 新內容；不建議做 |
| FightPlaceUI | CFightPlaceWin | `0x831AA0`／`0x430EA0` | 有 | 競技場 | （無） | 缺 | WIN2§6：S250 子碼 163／164，3.81 伺服器不會送；不建議做 |
| SmithUI | SmithUI | `0xBEFB90`／`0x7EEF90` | 有 | 鍛造 | （無） | 缺 | WIN2§6：op 43／102 觸發，3.81 無對應；伺服器不支援 |
| StoryPaperUI、StoryBookUI | CStoryPaper、CStoryBook | `0xBCB9A0`／`0x7CADA0`、`0xBCA7B0`／`0x7C9BB0` | 有（另有 `StoryBook.xml`） | 故事書 | （無） | 缺 | WIN2§6：本機觸發；不建議做 |
| QuestUI、QuestProgressUI | CQuestUI、CQuestProgressUI | `0x698130`／`0x297530`、`0x693BB0`／`0x292FB0` | `QuestInfo.xml` 有 | 任務 | （無） | 缺 | PBW§3：熱鍵 N 切換，資料走 protobuf `SC_QUEST_*`（S_EXTENDED）與 `contents\quest-common.bin`，不走 S250；3.81 `S_ClassicQuest` 是 S250 子碼 255＋`CQS1` 的伺服器自訂擴充（只在 `ClientLogin=2`），TW16 沒有 case 255；不建議做 |
| AgitAuctionUI | CAgitAuctionUI | `0x5448F0`／`0x143CF0` | 缺檔 | 盟屋拍賣 | （無） | 缺 | WIN2§5.2：S156 欄位與 3.81 完全一致；XML 不在 BIN XML 表、本機與他版客戶端都沒有，先做資料（M7-D） |
| SiegeUI | CSiegeUI（LineageSiege） | `0xEFBF10`／`0xAFB310` | 缺檔 | 攻城 | `ClanWar.cs`（只有戰爭狀態） | 缺 | WIN2§6：本機觸發，TW16 新內容；不建議做 |
| DailyGiftUI、GoldenHornUI、GotChaUI、CraftUI、ArcaManageUI、MonsterDexUI、ThebeRankUI、InterBattleEventUI、MGCatchGremlinUI、PromoteDollUI、CampaignUI、SihonTimer、SihonRanking、SihonNameUI、TimerUI、RoundScoreUI、AdenTeleportUI、WebMoviePlayer | 依序 DailyGiftUI、GoldenHornUI、CGotChaUI、CraftUI、ArcaManageUI、MonsterDexUI、ThebeRankUI、InterBattleEventUI、MiniGameCatchGremlinUI、PromoteDollUI、CampaignUI、SihonTimer、SihonRanking、SihonMapName、TimerUI、RoundScoreUI、AdenTelUI、CWebMoviePlayer | `0xBE2850`、`0xBE37F0`、`0xB1D1B0`、`0xE80860`、`0x56D2A0`、`0x64F2F0`、`0x7C0FE0`、`0x6EA4E0`、`0x625450`、`0x75F7E0`、`0x7D8360`、`0xB353D0`、`0x9ECF00`、`0x9EAE80`、`0xA241E0`、`0x9D8240`、`0x53EEB0`、`0xA56F90`（FO = VA − `0x400C00`） | 全部缺檔 | TW16 新增內容（每日禮物、喇叭、轉蛋、製作、阿卡、怪物圖鑑、排名、活動、小遊戲、娃娃、戰役、時空裂痕、計時、計分、亞丁傳送、網頁影片） | （無） | 缺 | 伺服器不支援（SP 全表沒有對應封包；本機伺服器的 `S_ClassicCraft` 等 S250 子碼與 TW16 是否同格式未比對） |
| MatchMakingUI | （不建立物件） | 分支在 `sub_B56A40` 內 | 有 | 血盟推薦 | （無） | 缺 | 本列已驗證：TW16 不建立此窗，不建議另做視窗；第 1 節 op 133 的 `sub_B243A0` 另有處理 |

### 4.2 舊式視窗（`sub_8C7C80` 建立，按鈕寫死）

已驗證：`sub_8C7C80`（VA `0x8C7C80`／FO `0x4C7080`，8,376 bytes，字串標籤 `initGame`）一次建立舊式視窗（WIN§0.1）。下表的類別名稱來自 `.data` 的 RTTI 字串（`ui_classes.py`，VA 是 `.?AV` 字串位置，FO = VA − `0x401800`）。只列和 3.81 功能有關的；`IOleWindow` 等 COM 介面、`Xml*Control` 框架類別不列。

| 類別（RTTI VA） | 功能 | 引擎對應 | 狀態 | 依據 |
|---|---|---|---|---|
| `TradeWindow`（`0x13794CC`） | NPC 商店、修理（模式 6）、私商購買 | `NpcUi.cs`、`WeaponRepairUi.cs`、`PrivateShopUi.cs` | 部分 | WIN§2 T102、T134 |
| `BoardListWindow`、`BoardReadWindow`、`BoardWriteWindow`（`0x1368CD4`、`0x1368C94`、`0x1368CB4`） | 公告板 | `BoardUi.cs` | 部分 | WIN§2 T130：surf 658–678 與座標已查 |
| `ExcludeWindow`（`0x136A954`）、`FriendWindow`（`0x136ABB0`，死碼） | 舊式封鎖名單 | `BuddyUi.cs` | 部分 | WIN§1.2 |
| `PetControl`（`0x138E118`）、`PetQuickWindow`（`0x138E134`，無呼叫者） | 寵物指令 | `PetUi.cs` | 部分 | WIN§1.5 |
| `PetInventoryWindow`（`0x136DEA0`） | 寵物背包 | `Pets.cs`（S176 清單） | 部分 | WIN2§2、§7：`InventoryWindow` 子類別，S_RETRIEVE_LIST 類型 11；3.81 不送類型 11 |
| `InventoryWindow`（`0x136DE80`）、`InvWindow`（`0x13773A4`） | 舊式背包 | `InventoryUi.cs` | 部分 | WIN2§2：TW16 只剩寵物背包基底；3.81 世代版面 `uimagicitem.xml` `ITEM_WINDOW`（W2-I） |
| `SpellWindow`（`0x13863F4`） | 舊式魔法清單 | `MagicUi.cs` | 部分 | WIN2§2：仍在 initGame 建立；3.81 世代版面 `uimagicitem.xml` `SPELL_WINDOW`（W2-I） |
| `HpMpWindow`（`0x138B808`） | HP／MP 浮動窗 | `PlayerHudView.cs` | 部分 | UI§2 V1（T192） |
| `MacroWindow`（`0x1369044`） | 巨集框 | `PlayerHudView.cs`、`CharacterConfig.cs` | 部分 | UI§2 V5（T176、T193） |
| `MapSystemWindow`、`FieldMapWindow`、`WorldMapWindow`、`VillageMapWindow`、`ImageBasedMapSystemWindow`、`LargeImageBasedMapSystemWindow`、`BGMapWindow`、`SmallBGMapWindow`、`MapWindow`（`0x137AE94`–`0x137AF88`、`0x138DA10`） | 地圖系統（小地圖、世界、村莊） | `Minimap.cs`、`MinimapUi.cs` | 部分 | UI§5（T196）；世界／村莊地圖引擎沒有 |
| `MapLocationSaveWindow`、`MapCommunicationSelectWindow`（`0x137AFD4`、`0x137AFA8`） | 地圖存點、地圖發送座標（`TransferMenu`） | `LocationShare.cs`、`Bookmarks.cs` | 部分 | WO 第 5 節 |
| `InputWindow`（`0x137A09C`） | 數量／文字輸入 | `GroundUi.cs`、`NpcUi.cs`（各自的數量框） | 部分 | WIN2§5.3：聊天輸入列提示（YES (361, 567)／NO (401, 567)，W2-E）；S136 改走 NPC 對話 HTML 頁 |
| `QuitWindow`（`0x13767B0`） | 離開遊戲確認 | `Recovery.cs`（逾時直接送 C122 斷線） | 缺 | PBW§4：vtable `0x118702C`，建構子 `sub_980580`（`dword_14EBE24`），版面 `uiextra86.xml` 第 0 段 `QUIT_WINDOW`（置中 y＝150）；死亡逾時（WM_TIMER `0x29A`，501,264 ms）送 C_QUIT 後開窗；鈕：重新開始（C_DEAD_RESTART／C_RESTART，戰鬥 15 秒限制）、離開（C_QUIT）、取消 → PBW-B |
| `CNoticeWindow`（`0x1372600`） | 拍賣物品通知圖示（不是公告） | （無） | 缺 | PBW§5：vtable `0x1181F7C`，建構子 `sub_909050`（`dword_13D43BC`），版面 `uiextra86.xml` 第 25 段 `NOTICE_WINDOW`（`SURF_AUCTION_NOTICE_OFF` 3716）；提示文字是韓文「拍賣物品通知」，沒有程式讓它顯示；3.81 無拍賣通知，不建議做 |
| `PKWindow`（`0x138B824`） | PK 狀態 | （無） | 缺 | WIN2§7：第一層沒有收包函式，3.81 無對應；不建議做 |
| `CCookWindow`（`0x137A28C`） | 料理 | `InventoryUi.cs`（料理書使用） | 部分 | WIN2§7：S250 52 開窗，欄位與 3.81 一致；版面 `uiextra86.xml` `COOK_BOOK_WINDOW`（W2-F） |
| `TelTablesWindow`（`0x137929C`） | 傳送表 | `Teleport.cs` | 部分 | WIN2§7：S_EVENT 38／45 的使用者清單（推測 GM 工具），與傳送無關；不建議做 |
| `AgitWindow`、`AgitMapWindow`（`0x135DB04`、`0x135DB3C`） | 盟屋 | （無） | 缺 | WIN2§5.2：S187 開 `AgitMapWindow`（W2-C）；`AgitWindow` 觸發待驗證 |
| `WareHistoryWindow`、`RenewalWareHouseWin` 系列（`0x137A014`、`0x13788B8`–`0x137892C`） | 倉庫紀錄、倉庫 | `NpcUi.cs` | 部分 | WIN2§7：血盟倉庫紀錄是 `sub_A559D0` 的視窗，S250 117 欄位與 3.81 完全一致（W2-G） |
| `SignWindow`（`0x1378B44`）、`TipWindow`（`0x137945C`）、`LevelUpHelpWindow`（`0x137B180`）、`GiftMessageWindow`（`0x136ABF8`）、`FontWindow`（`0x136AB4C`）、`DragonGateWindow`（`0x137B02C`）、`SMSWindow`、`SMSPledgeWindow`（`0x136E954`、`0x136E96C`）、`GameRankShortCutWindow`（`0x138B83C`）、`WebBrowserWindow`、`CWebWindow`、`CantWebWindow`（`0x137B730`、`0x137BC2C`、`0x137B750`） | 看板、提示、升級說明、禮物訊息、字型、龍門、簡訊、排名捷徑、網頁 | （無） | 缺 | WIN2§7：`FontWindow` 由熱鍵與血盟窗建立；SMS、龍門、升級說明、排名捷徑只在 initGame 建立；Sign／Tip／Gift 的 vtable 無程式引用。TW16 新功能或沒有觸發點，不建議做（簡訊、網頁伺服器不支援） |

**框架層（已驗證，引擎缺）**：視窗堆疊 `sub_A57C60`（VA `0xA57C60`／FO `0x657060`，最多 4 層）、XML 視窗工廠 `sub_B56A40`、舊式建立 `sub_8C7C80`。引擎目前全部是 WinForms，原生 800×600 畫布（C2，T190）尚未完成，所以上面所有「部分」的視窗都還沒有原版版面。

## 5. 物件、技能、道具、地圖事件與繪製的處理函式

| 原版函式 VA／FO | 功能 | 引擎對應檔案 | 狀態 | 依據 |
|---|---|---|---|---|
| `sub_87C890`／`0x47BC90` | `list.spr` parser（欄位 100–119、36-byte frame） | `MagicRendering.cs`、`Polymorph.cs`、`ActionCadence.cs` | 部分 | REA B07；欄位 101、103–108、111–118 的語義多數未用 |
| `sub_A3EF40`／`0x63E340`（`A3FB..` 段） | 動作影格音效選擇（`<`／`[`／`^`、候選 ≤5、0 不重抽） | `CombatSfx.cs`、`ClientCombatSfx.cs` | 已有 | REA 產品差異 1、3（T165 已修） |
| `sub_A06020`／`0x605420`、`sub_A060F0`／`0x6054F0` | 單次音效與同 sample 200 ms 冷卻 | `SoundRules.cs`、`CombatSfx.cs` | 部分 | SD#5（G8，T173 審查中） |
| `sub_A06210`／`0x605610` | `^` 循環環境音，每 id 最多 5 個近距音源 | `MapAmbientSound.cs` | 部分 | REA B04、SD（T166 審查中） |
| `sub_F0B570`／`0xB0A970` | 地圖特效載入與 id→光源半徑表（3442–3446＝隱形光源點 k=5–13） | `MapEffects.cs`、`OriginalMapCache.cs` | 部分 | EF2§1（T227）：第 7 參數＝`actor+9` 光源 k；六成地圖特效是光源，引擎沒有表也沒畫光源（FX1） |
| `sub_F0E9B0`／`0xB0DDB0` | 地圖 actor 建立（經 `A4E5D0`） | `MapEffects.cs`、`Effect6016Host.cs` | 部分 | REA B04；只有 6016 走完整路徑 |
| `sub_85E090`／`0x45D490` | MAP-F4 區域特效淡出（`actor+612` 群組） | `OriginalMapCache.cs`（`NotSupportedException`） | 缺 | REA B03：本機 7,807 個 `.s32` 延伸記錄 0 筆，**不建議做** |
| `sub_A51920`／`0x650D20`（6 個呼叫點） | 物件建立（S87 同身份入口） | `WorldObjects.cs`、`ObjectRendering.cs` | 部分 | PG§7：S_PUT_OBJECT（186）→`sub_9262D0`→`A51920`；同 id 原地覆寫、移動進度歸零；引擎整筆取代，可見差異待驗證 |
| `sub_A4B060`／`0x64A460`、`sub_A4BA20`／`0x64AE20` | `]` 旗標：建立 `0x2A0` 預設 Effect 子特效（`{`／`}` 為淡入淡出） | （無） | 缺 | EF2§2（T227）：已驗證；list.spr 2,812 處、485 個 sprite（FX2） |
| `sub_81C7C0`／`0x41BBC0`、`sub_8ABC10`／`0x4AB010` | `!` 旗標：待處理動作的釋放時機 | `MagicRendering.cs`（收包時刻就放） | 部分 | SV§2：特效在施法者 `!` 格、受擊在特效 `!` 格（G12） |
| （109 kind 0–10，SV§3） | 投射物飛行 kind 3／4／5／7 等 | `ProjectileRendering.cs` | 部分 | SV§3：只做 kind 3／4（G12） |
| `LineageSpell::CSpell` vtable 第 3 格（SV§1） | 施法只送封包、不在本地播動作 | `Magic.cs`、`SkillCast.cs` | 部分 | SV#1（需使用者裁定） |
| `sub_F112F0`／`0xB106F0` | 物件點擊共通流程（距離 1、自動走近、類別分派） | `PlayerPointer.cs`、`Ground.cs`、`Doors.cs`、`NpcInteraction.cs` | 部分 | WO 第 1 節 |
| `sub_5001C0`／`0xFF5C0` | 拾取送 `C_GET`（`chhddc`、節流 986／600 ms） | `Ground.cs`、`PickupPresentation.cs` | 部分 | WO 第 2 節 |
| （門類別 7／8，WO 第 3 節） | 門點擊送 `C_OPEN`（1 格內） | `Doors.cs` | 部分 | WO 第 3 節 |
| `byte_13CE51E`（WO 第 4 節） | 幽靈本機限制 | `GhostMode.cs` | 部分 | WO 第 4 節 |
| （`TransferMenu`，WO 第 5 節） | 發送座標入口與顯示（訊息 1784／1785） | `LocationShare.cs` | 部分 | WO 第 5 節 |
| `sub_4FC710`／`0xFBB10` → `sub_AC9560`／`0x6C8960` | 通用 Yes/No（聊天輸入列 Y/N） | `Party.cs`、`Duel.cs`、`Trade.cs`、`Marriage.cs`（各自按鈕） | 部分 | WIN§2.1、WO 第 6 節 |
| `sub_4FCA30`／`0xFBE30` | Yes/No 回覆 `chdhcs` | 同上（送 C121） | 已有 | WIN§2 T141（欄位一致） |
| （表情選單，AN§1） | 表情順序與外型重映射 | `TitleAndEmote.cs` | 部分 | AN§1（T169 審查中） |
| （名稱、稱號、盟徽繪製，AN§2） | 名稱色、位置、只畫指到的物件 | `ObjectRendering.cs`、`TitleAndEmote.cs` | 部分 | AN§2（T168 審查中） |
| （變身缺圖退回，AN§3.4） | 外型缺動作時逐級退回 | `Polymorph.cs`、`PlayerProfiles.cs` | 部分 | AN§3（T170 審查中） |
| （日夜曲線、光源，AM§1、§3） | 明暗等級與光源圖 | `Ambience.cs` | 部分 | AM（T177） |
| （天氣粒子，AM§2） | 雨雪 | `Ambience.cs` | 部分 | AM（T178） |
| （中毒、麻痺、盲目、酒醉、隱身，AM§4） | 異常狀態外觀 | `Afflictions.cs`、`TimedBuffs.cs`、`Invisibility.cs` | 部分 | AM（T179） |
| `sub_8444A0`／`0x4438A0`、`sub_83E490`／`0x43D890` | MingLiU 12 字型與 4 向描邊 | `ObjectRendering.cs`、`PlayerHudView.cs` | 部分 | UI§1 V4 (f)：引擎字型不同 |
| `sub_A97450`／`0x696850`、`sub_52D6D0`／`0x12CAD0` | UI XML 讀取、解密、`-c` 退回 | `NativeUi/UiXmlSource.cs`、`NativeUi/ClientArchive.cs` | 已有 | UI§1（C1 #461 已合併） |
| `sub_52D8A0`／`0x12CCA0` | Sprite／Surf 雜湊封存讀取與 c 版圖 | `NativeUi/SurfCatalog.cs`、`NativeUi/ClientArchive.cs` | 已有 | UI§1 V4 (e)（C1） |
| （`.img` 解碼，UI§1 V4） | UI 圖旗標與透明色 | `NativeUi/SurfCatalog.cs`、`NativeUi/UiSurface.cs` | 已有 | UI§1 V4（C1） |
| `sub_A57C60`／`0x657060` | 視窗堆疊（最多 4 層） | （無） | 缺 | WIN§2.1（C2，T190） |
| vtable 表 `0x1165C80`／`0xD64880` | 聊天各類型的格式化與上色 | `PlayerChat.cs`、`PlayerHudChat.cs` | 部分 | UI§3（T175 審查中） |
| `sub_8E66D0`／`0x4E5AD0`（`MiniHpMpBarManager`） | 頭上 HP／MP 條 | `PlayerHudView.cs`、`ObjectRendering.cs` | 部分 | UI§2 V1（T192） |
| `sub_8FB4D0`／`0x4FA8D0`（`LStatusEffectButton`）、`sub_AA0610`／`0x69FA10` | 狀態圖示與 `effectlist2.xml` | `StatusIcons.cs`、`MagicStatus.cs` | 部分 | UI§4（T199） |
| （`.s32`／`.seg` 地圖區塊，REA B03） | 地圖快取與 2×2 移動 | `OriginalMapCache.cs` | 已有 | REA B03（全量解析失敗 0） |
| （物品使用尾段，R24） | 道具使用 | `Inventory.cs`、`ItemUseArguments.cs` | 已有 | R24（T109） |
| （裝備槽位，R26） | 裝備 | `Equipment.cs`、`ItemStatus.cs` | 已有 | R25、R26（T121） |
| （加密與分幀，SP） | 封包加解密 | `Network.cs`、`LoginConnection.cs`、`Protocol.cs` | 已有 | SP〈查表方式〉 |

## 6. 統計

以本文第 1–5 節的表格列數計（一列一個功能；第 2 節「登入期也收」那列不計；第 4.1 節最後一列把 18 個 TW16 新增視窗合成一列）。數字由 `rea-scripts/gap_count.py` 從本文重算。

| 節 | 已有 | 部分 | 缺 | 其中需先查證 | 其中伺服器不支援／不建議 | 其中可直接補 |
|---|---:|---:|---:|---:|---:|---:|
| 1. 收包：第一組 dispatch | 35 | 57 | 22 | 0 | 9 | 13 |
| 2. 收包：第二組 | 6 | 2 | 5 | 0 | 5 | 0 |
| 3. 送包：`sub_951F80` 名稱表 | 8 | 34 | 10 | 0 | 6 | 4 |
| 4. UI 視窗 | 0 | 46 | 27 | 0 | 19 | 8 |
| 5. 物件、技能、道具、地圖事件與繪製的處理函式 | 9 | 25 | 3 | 0 | 1 | 2 |
| **合計** | **58** | **164** | **67** | 0 | 40 | 27 |

T225（#532）回填後重算：第 3 節「其他」一列拆成三列（共 289 列），第 1–3 節的「需先查證」已全部查完，見 [`protocol-gaps.md`](protocol-gaps.md)。

T226（#533）回填後重算：第 4 節依 [`windows-2.md`](windows-2.md) 更新依據欄（列數與「已有／部分／缺」不變）；第 4 節的「需先查證」剩 2 列（`QuestUI` 的 S250 格式、`QuitWindow` 的使用點）。

T245（#602）回填後重算：`QuestUI`（不建議）、`QuitWindow`（可補，PBW-B）兩列查完；`CNoticeWindow` 查出是拍賣通知圖示、與登入公告無關，狀態由「部分」改「缺」（不建議）。第 5 節原表的「需先查證 1／可直接補 1」與重算結果（0／2）不符，是先前回填時沒有重算，本次一併更正。全表「需先查證」為 0，見 [`packetbox-windows.md`](packetbox-windows.md)。

「部分」多數屬於兩類：(a) reference 文件已查出表現差異、且 `TASKS.md` 已排了修正任務（T168–T200）；(b) 只有協定，沒有原版視窗（等原生 UI 畫布 T190）。所以 M7 新開的任務集中在「缺」與沒有排到任務的「部分」。

## 7. 建議的 code 任務分組

**原則**
- 每組只動「可改的檔案」欄列出的檔案；同一個既有檔案只出現在一組。新增檔案用該組自己的名稱，不會互相衝突。
- `WorldSession.cs`（接收分派、`Reset`、`Snapshot`）與 `Program.cs`（測試註冊）是共用入口，**只給 M7-A**。M7-A 先做擴充點，其他新模組透過它掛上，就不必再改這兩個檔。
- 已排定、尚未合併的任務（下表）用到的檔案，M7 各組不碰，列為依賴。
- 驗收照 #503 暫行政策：編譯＋既有單元測試綠；每組另加自己的離線測試（新檔）。表現一律引用下列 reference；標「需先查證」的部分不做畫面，只做協定與資料。

**已排定、不重開的任務（檔案保留給它們）**

| 任務 | 涵蓋本表的列 | 保留的檔案 |
|---|---|---|
| T124（書籤）、T198 | op 2、238；C_BOOKMARK | `Bookmarks.cs`、`BookmarkUi.cs` |
| T135（盟徽）、G4 | op 161；C_EMBLEM | `Clan.cs`、`ClanEmblem.cs` |
| T151（復活與移動鎖） | op 197 | `Recovery.cs` 相關變更在 PR #425 |
| T166（`^` 環境音） | `sub_A06210` | `MapAmbientSound.cs` |
| T168（名稱、稱號、盟徽色）、T169（表情）、T170（變身） | op 36、24、84、63、186 名稱部分、92、16 | `ObjectRendering.cs`、`TitleAndEmote.cs`、`DisplayNotices.cs`、`ClanUi.cs`、`DuelUi.cs`、`ClanWar.cs`、`Polymorph.cs`、`PlayerProfiles.cs` |
| T173（音效共用層） | op 91、`sub_A06020` | `SoundPackets.cs`、`SoundRules.cs`、`AudioCore.cs`、`AudioAssets.cs`、`PlayerAudioSettings.cs`、`ClientAudioSettings.cs` |
| T175（聊天格式） | op 112、61、104、153 | `PlayerChat.cs`、`PlayerHudChat.cs` |
| T176、T193（快捷列） | `MacroWindow`、`WidgetSlot` | `CharacterConfig.cs`、`PlayerHud*.cs` |
| T177–T179（日夜、天氣、異常狀態） | op 206、156、176、33、114、213、160、229 | `Ambience.cs`、`Afflictions.cs`、`TimedBuffs.cs`、`Invisibility.cs` |
| T190–T200（原生 UI 畫布與模組） | 第 4 節的版面、`sub_A57C60` | `NativeUi/` 下 C2 與各模組的新檔 |

**M7 新任務分組（22 組，可平行；依賴只指向 M7-A、M7-J、M7-L 或已排定任務）**

| 組 | 目標 | 可改的檔案 | 依賴 | 引用 |
|---|---|---|---|---|
| M7-A 接收與快照擴充點 | 在 `WorldSession` 加「模組註冊」：opcode（含 S250 子碼）→ 處理器、`Reset`、`Snapshot` 三個掛點，以 partial class 新檔註冊；保留未處理 opcode 計數。`Program.cs` 加測試自動註冊點 | `WorldSession.cs`、`Program.cs`、新檔 `WorldExtensions.cs` | 無 | 本文第 1 節；SP〈S opcode 全表〉 |
| M7-B 能力旗標與 SP／MR | 接 S36 Ability、S37 SPMR，存進快照；狀態窗顯示等原生 UI | 新檔 `AbilityStatus.cs`、`AbilityStatusTests.cs` | M7-A | 第 1 節 op 122；SP S36、S37 |
| M7-C 城堡：城主、稅率、金庫 | 接 S69、S185、S141、S4，送 C19、C44、C56；城主王冠的畫面需先查證，先只做資料與文字通知 | 新檔 `Castle.cs`、`CastleTests.cs` | M7-A | 第 1 節 op 252、95、12、49；SP S69、S185、S141、S4 與對應 C 索引 |
| M7-D 盟屋清單與地圖 | 接 S156 HouseList、S187 HouseMap；視窗需先查證，先做資料 | 新檔 `Agit.cs`、`AgitTests.cs` | M7-A | 第 1 節 op 0、18；SP S156、S187 |
| M7-E 數量／文字輸入詢問 | 接 S136（ItemCount、HowManyMake 等），回 C11；先用既有 WinForms 輸入框，原版 `InputWindow` 版面需先查證 | 新檔 `AmountPrompt.cs`、`AmountPromptUi.cs`、`AmountPromptTests.cs` | M7-A | 第 1 節 op 224；SP S136、C11 |
| M7-G 以物品學魔法 | 接 S13 SkillBuyItem，送 C245、C191 | 新檔 `SpellExchange.cs`、`SpellExchangeUi.cs`、`SpellExchangeTests.cs` | M7-A | 第 1 節 op 175；SP S13、C245、C191 |
| M7-H 血盟推薦 | 接 S0，送 C76、C78；TW16 不建立 `MatchMakingUI`（第 4.1 節），畫面需先查證 | 新檔 `ClanMatching.cs`、`ClanMatchingTests.cs` | M7-A | 第 1 節 op 133；SP S0、C76、C78 |
| M7-J 通用 Yes/No（聊天輸入列 Y/N） | 新增共用的伺服器詢問佇列：S219 → 聊天輸入列提示，打 Y／N＋Enter 回 C121；只對 WIN§2.1 列出的特例 id 特別處理，逾時只套用 729（12 秒）、1232／1256／1268（10 秒）。把組隊、決鬥、求婚改用它，拿掉求婚專用 Form 與 HUD 鈕 | 新檔 `YesNoPrompt.cs`、`YesNoPromptTests.cs`；`Party.cs`、`Duel.cs`、`Marriage.cs` | 無 | WIN§2.1、WIN§2 T141；WO 第 6 節 |
| M7-K 交易詢問與交易流程 | 交易邀請改用 M7-J；252 的旗標行為照 WIN§2.1（自動回覆值待驗證的部分保留現狀並標推測） | `Trade.cs`、`TradeUi.cs` | M7-J | WIN§2.1；第 1 節 op 85、88、145、167 |
| M7-L 拾取對齊 | 點物品自動走到 1 格內再送；單個或「全部拾取」送整疊、否則跳數量提示；送出前本機轉向；節流 986 ms（旗標 `0x10000` 時 600 ms）；幽靈中擋拾取。走近邏輯放新檔供 M7-M 使用 | `Ground.cs`、`GroundUi.cs`、`PickupPresentation.cs`、新檔 `ApproachWalk.cs` | 無 | WO 第 1–2 節；REA B07〈點擊流程〉 |
| M7-M 門自動走近 | 點門先用 `ApproachWalk` 走到 1 格內再送 C41；死亡時擋物件點擊 | `Doors.cs` | M7-L | WO 第 3 節 |
| M7-N 幽靈本機限制 | 拿掉常駐「離開幽靈」鈕；幽靈中擋快捷、自動攻擊與 F 鍵瞬移，略過自身外觀變更；拾取由 M7-L 處理 | `GhostMode.cs`、`OnlineInput.cs` | M7-L | WO 第 4 節 |
| M7-O 發送座標 | 送 MapSystem 地區 id；收到時顯示「寄件者＋地區名稱」，另存最多 10 筆；字串 1784／1785 的文字需先查證，未取得前用編號占位並標明 | `LocationShare.cs` | 無 | WO 第 5 節 |
| M7-P 技能特效時機與飛行 | 特效在施法者 `!` 格啟動、受擊在特效 `!` 格；S42 預設類別只放一個特效；補 109 kind 5／6／7／8；kind 3 不畫落點 | `MagicRendering.cs`、`ProjectileRendering.cs`、`AttackSkillPackets.cs` | 無 | SV§2–§4、SV#5–#14；M6 G12 |
| M7-Q 施法預測動作與冷卻 | 拿掉送 C6 時的本地預測動作，冷卻改用 `list.spr` 動作 18／19 影格總長；**開工前要使用者裁定**（M6 G12 第 4 項） | `Magic.cs`、`ActionCadence.cs`、`SkillCast.cs` | 使用者裁定 | SV§1、SV#1–#4 |
| M7-R HTML 結果走 NPC 對話窗 | 查詢玩家、排名等 S39 結果不再轉成聊天行，改在 NPC 對話面板顯示；PK 查詢走 HTML 動作 | `PlayerQuery.cs`、`NpcInteraction.cs`、`NpcUi.cs` | 無（原生 `HYPER_TEXT_WINDOW` 版面另等 T190） | WIN§2 T140、T134；WIN§2.1 |
| M7-S 商店與修理改用 `TRADE_WINDOW` | 原生清單窗，含修理模式 6；空清單時開 `touma3`／`gayle5` 對話頁；移除修理專用 Form 與 HUD 鈕 | `NpcServices.cs`、`WeaponRepair.cs`、`WeaponRepairUi.cs`、新檔 `NativeUi/TradeWindowPanel.cs` | T190 | WIN§2 T102、T134；WIN§1.5 |
| M7-T 個人商店開店設定窗 | `PersonalShopUI`：出售／收購各 8 格一頁、訊息 2×20 字、3 種外型 | `PrivateShop.cs`、`PrivateShopUi.cs`、新檔 `NativeUi/PersonalShopPanel.cs` | T190 | WIN§1.1 |
| M7-U 公告板原生窗 | `BoardListWindow`／`BoardReadWindow`／`BoardWriteWindow`，surf 658–678 與座標照 WIN§2 | `Board.cs`、`BoardUi.cs`、新檔 `NativeUi/BoardPanels.cs` | T190 | WIN§2 T130 |
| M7-V 好友／封鎖／信件封鎖三窗 | 三窗共用一個面板類別：14 列＋捲軸＋人數標籤；補信件封鎖名單 | `Buddies.cs`、`BuddyUi.cs`、新檔 `NativeUi/FriendPanel.cs` | T190 | WIN§1.2 |
| M7-W 寵物：召喚選擇與指令目標 | `CSummonUI`（快速 3 格、12 個等級、魅力標籤，`summonList.xml`）；接 S236 SelectTarget、送 C223 | `Pets.cs`、`PetUi.cs`、新檔 `NativeUi/SummonPanel.cs`、`PetTarget.cs` | T190、M7-A | WIN§1.3、WIN§1.5；第 1 節 op 52、228；SP S236 |
| M7-X 死亡統計窗與逾時離線 | `UserPlayStatsUI`（重新開始／結束／取消＋統計，`UserPlayUI.xml` 本機有），約 8 分 21 秒自動 `C_QUIT` | `Recovery.cs` | T151 合併 | WO 第 6 節；第 4.1 節 `UserPlayStatsUI` |

（M7-F、M7-I 編號保留不用：寵物目標併入 M7-W；攻城時間伺服器不支援。）

**建議順序**：先派 M7-A、M7-J、M7-L、M7-O、M7-P、M7-R（不等別人）；M7-A 合併後派 M7-B、C、D、E、G、H；M7-J 後派 M7-K；M7-L 後派 M7-M、M7-N；T190 合併後派 M7-S、T、U、V、W；T151 合併後派 M7-X；M7-Q 等使用者裁定。佇列隨時有 ≥4 張可派。

## 8. 需先查證（狀態為「缺」或「部分」、但原版表現還沒查證）

每項寫：查什麼、從哪裡開始。查證任務只產出 `docs/reference/*.md`，查完再依結果開 code 任務。

| 項目 | 要查的內容 | 起點 |
|---|---|---|
| S_EXTENDED 子碼表 | ✅ T225 已查（[PG§2](protocol-gaps.md)）。TW16 op 19／39／109 的子碼全表，對 3.81 S250 子碼逐一比對，列出引擎沒處理的子碼 | `sub_926BF0`／`0x525FF0`、`sub_E428F0`、`sub_E42A50` |
| S_MESSAGE_CODE 字串表 | ✅ T225 已查（[PG§3](protocol-gaps.md)）。原版從哪個字串表取訊息文字（`Text.pak`？），引擎要不要載入整張表取代個別 id | `sub_942B40`／`0x541F40`；WO 第 7 節 |
| 版本檢查 | ✅ T225 已查（[PG§4](protocol-gaps.md)）。3.81 伺服器是否要求 C14／S139；TW16 收到 S_VERSION_CHECK 後做什麼 | `sub_94DBB0`／`0x54CFB0`；SP C14、S139 |
| 未知收包 | ✅ T225 已查（[PG§5](protocol-gaps.md)）。S_BLINK、S_HIT_RATIO、S_SHOW_MAP、S_CLONE、S_EVENT、S_NOTICE 的欄位與表現，以及在 3.81 是否有對應 | `sub_931B90`、`sub_93FAD0`、`sub_94BFC0`、`sub_933640`、`sub_9357E0`、`sub_ECABC0` |
| 未知送包 | ✅ T225 已查（[PG§6](protocol-gaps.md)）。C_REGISTER_QUIZ、C_WISH、C_THROW、C_BLINK、C_ONOFF、C_ADDR、C_PLATE、C_BOOK、C_SAVE、C_TELEPORT_USER、C_GOTO_MAP、C_ALT_ATTACK、C_ATTACK_CONTINUE 的送出點與欄位 | `sub_951F80` 名稱表 → 各送出點（`wo_sendsites.py` 的作法） |
| 3.81 世代用哪套視窗 | 背包、魔法、倉庫：舊式 `InventoryWindow`／`SpellWindow`／`RenewalWareHouseWin` 與 XML 版 `RenewalInventoryUI`／`SpellUI`／`RenewalWareHouseUI`，TW16 實際開的是哪個；XML 版缺檔時的版面來源 | `sub_8C7C80` 的建立順序；`sub_A57C60` 的呼叫者；✅ T226（#533）已查：[`windows-2.md`](windows-2.md) 第 2 節 |
| 血盟窗 | `PledgeUI.xml`、`PledgeFocusUI.xml`（本機有）的版面與開窗封包 | #448 解密的 XML；`sub_B2A0C0`／`0x7294C0`；✅ T226（#533）已查：[`windows-2.md`](windows-2.md) 第 3 節 |
| 變身選擇窗 | `CPolymorphUI`／`CSpecialPolymorphUI` 由哪個封包開啟，與 3.81 的 S39 變身清單 HTML 的關係 | `sub_BF8C00`／`0x7F8000`、`sub_A09770`／`0x608B70`；`polymorphList.xml`；✅ T226（#533）已查：[`windows-2.md`](windows-2.md) 第 4 節 |
| 城堡、盟屋、數量輸入的畫面 | 城主王冠、稅率／金庫窗（`AgitWindow`、`AgitMapWindow`、`InputWindow`）的版面與按鈕 | 第 1 節 op 252、95、12、49、0、18、224 的處理函式；✅ T226（#533）已查：[`windows-2.md`](windows-2.md) 第 5 節 |
| ~~地圖特效 id→類別~~ | ✅ 已查（T227）：第 7 參數＝光源半徑 k；3442–3446＝隱形光源點 k=5–13。可開 FX1 | EF2§1 |
| S87 同身份入口 | ✅ T225 已查（[PG§7](protocol-gaps.md)）。TW16 的物件建立封包是哪一個 case，`A51920` 的 6 個呼叫點 | REA B07 |
| ~~`]` 子特效~~ | ✅ 已查（T227）：預設 Effect，位置／方向取當格擁有者，播完自刪。可開 FX2 | EF2§2 |
| 登入流程畫面 | T227 已搜本機所有版本：`LoginUI`／`SelectUI`／`ServerName`／`LoadingScene` 缺資源；`CreateUI` 只有他版本候選。剩 `LoadingWindowUI` 待查；✅ T245 已查 `QuitWindow`（讀 `uiextra86.xml` `QUIT_WINDOW`，死亡逾時開啟）與 `CNoticeWindow`（拍賣通知圖示，與登入無關），見 [PBW§4–5](packetbox-windows.md) | EF2§5；WIN§2 T107；第 4 節 |
| 其他 XML 窗 | `BookmarkMemoryWindowUI`、`StatInfoUI`、`UserObtainUI`、`QuickMotionUI`、`OptionUI`、`PopUpUI`／`PopupMsgUI`、`LinHelperUI`、`FightPlaceUI`、`SmithUI`、`StoryBookUI`、`QuestUI`、`SiegeUI`、`ZoneNameNotice`、`PetSummonUI` 的觸發封包與 3.81 伺服器是否支援 | 第 4.1 節的建構子地址；✅ T226（#533）已查：[`windows-2.md`](windows-2.md) 第 6 節 |
| S250 視窗類子碼（PG B3） | ✅ T245（#602）已查（[PBW§2](packetbox-windows.md)）。37、45、52、66、83、84、86、117、146、168、169、173、190、192 的原版行為、3.81 呼叫點與建議任務 PBW-A–E | `sub_9357E0`／`0x534BE0` 各 case |
| 其他舊式窗 | `PKWindow`、`CCookWindow`、`TelTablesWindow`、`PetInventoryWindow`、`WareHistoryWindow` 與看板／提示類 | 第 4.2 節的 RTTI；✅ T226（#533）已查：[`windows-2.md`](windows-2.md) 第 7 節 |
| ~~水中呼吸、座標特效~~ | ✅ 已查（T227）：S_BREATH 只加圖示 effect 11；S_EFFECT_LOC 與引擎一致（`]` 併入 FX2） | EF2§3、§4 |
| 信件、釣魚、能力值重置 | XML 本機缺檔（`RenewalMailUI`、`FishingUI`、`ResetStatUI`） | WIN§3；✅ T226（#533）已查：[`windows-2.md`](windows-2.md) 第 8 節 |

## 9. 限制

- 全部是靜態分析，沒有實機觀察。原版行為以 TW1608262200 為準；LiTo 3.81 是否相同沒有查。
- 「3.81 對應」多數依名稱推定。名稱表與 dispatch 都是已驗證的資料，但 TW16 封包欄位和 3.81 伺服器欄位是否相同，只有依據欄引用 reference 文件的列才查過。
- 處理函式取 case 區塊的第一個直接呼叫，不代表整個處理流程；19／39／109 的區塊有多個呼叫。
- 引擎狀態以本分支同步後的 main 為準；T1xx「審查中」的分支沒有比對，合併後部分「部分」可能改成「已有」。
- 送包只到名稱表，沒有逐一找送出點；引擎是否送某個 C opcode 由原始碼搜尋判斷，可能漏掉以變數組出的 opcode。
- 「伺服器不支援」依 `server-protocol.md` 的 S／C 全表判斷，沒有讀伺服器設定或 DB。

## 10. 可重跑腳本

| 腳本 | 用途 | 執行 |
|---|---|---|
| `rea-scripts/gap_inventory.py` | 三組收包 dispatch 的 case 與處理函式、收包名稱表、送包名稱表、`.xml` 檔名與引用函式、UI 類 RTTI | `py -3 -I docs/reference/rea-scripts/gap_inventory.py scratch/t202/inventory.json` |
| `rea-scripts/ui_classes.py` | XML 視窗建構子 → vtable → RTTI 類別名；舊式視窗 RTTI 清單 | 先用 `run_ida.ps1`＋`ida_dec.py` 反編譯 `B56A40`（IDB 複製到 `scratch/rea/ida/tw16.idb`，`-Drive` 選未占用的磁碟代號），再 `py -3 -I docs/reference/rea-scripts/ui_classes.py scratch/rea/ida/<out>/00B56A40.c scratch/t202/ui_classes.json` |
| `rea-scripts/gap_count.py` | 從本文重算第 6 節的統計 | `py -3 -I docs/reference/rea-scripts/gap_count.py docs/reference/gap-matrix.md` |

## 11. 第二輪盤點（T244／#601，2026-11-09）

依目前的 main `6d07fe0d`（含 #527、#528 與 T245 #602 的回填）重新核對第 1–5 節的每一列。第 1–10 節保留原文不改；本節的變更表覆蓋原表的狀態，統計由 `rea-scripts/gap_count_r2.py` 從「原表＋本節變更表」重算。

### 11.1 方法

- 列出 T202 基準 `003a5a34` 之後合併的 PR，以及 T202 當時標成「審查中」、其實已在基準內合併的任務（T124、T135、T166、T168–T170、T173、T175–T179）。逐一讀 `docs/reports/issue-<N>.md` 的「做了什麼／未完成」，再用 Grep 在 `engine/source` 抽查（每張任務至少一個 `檔案:行`）。
- 本輪沒有新的反組譯：BIN 端的 VA／File Offset 全部沿用第 1–5 節（已驗證）。所以沒有開 IDB、沒有掛 subst 磁碟。
- 判定規則與第 0 節相同，另加一條：**程式在、但產品沒有呼叫**（只有測試呼叫，或要加 `--native-ui` 才啟用）的，一律算「部分」，不算「已有」。
- 沒有建置、沒有執行、沒有啟動遊戲、伺服器或 DB。

### 11.2 本輪新發現（影響多列）

1. **只有測試在呼叫、產品沒接上**（已用 Grep 確認，產品程式找不到呼叫點）：
   - 畫面：`Renderer.PaintAmbience`（`Renderer.cs:9`）、`ComposeAmbienceLights`（`Ambience.cs:493`）、`ShadeAmbience`（`Ambience.cs:505`）、`WeatherParticles`。所以日夜明暗、光照圖（#566 FX1）、雨雪粒子在畫面上都看不到。
   - 聲音：`ClientSoundScene.Configure`（`ClientSoundScene.cs:16`）、`MapAmbientSound.Backend`。`Observe` 有接（`MapEffects.cs:62–63`），但沒有後端，所以背景音樂、envsound、`^` 環境音都不會出聲。
   - 資料：`CastleCrownFrames`（王冠不畫）、`StatusEffectIcons`（水中呼吸圖示）、`RequestSpellExchangeList`、`SendCastleReply`、`SendClanMatching`、`ChatChannelCommands.IsCommand`、`GhostSkipsSelfAppearance`，以及 #527 的 `PersonalShopPanel.Registration`（沒有加進 `UiPanelRegistry.cs`）。
2. **原生 UI 預設關閉**：C2 畫布（#463）與 #464–#468、#526 的面板，只有在加 `--native-ui` 或 `native-ui.json` 寫 `{"nativeUi":true}` 時才出現。`UiLayer` 只有 4 個繪製圖層，Window 層數量**沒有上限**，和原版 `sub_A57C60` 的「最多 4 層」不同。
3. **S250（op 108）子碼**：3.81 伺服器會送 62 個，引擎現在處理 **37 個**（原本 23 個）。還缺 25 個：B1 的 12 個由 #547 處理；B3 剩 13 個（37、45、66、83、84、86、117、146、168、169、173、190、192），其中 117 由 #560 處理，其他 12 個已由 #602（T245）查完（PBW§2）：66、146 伺服器沒有呼叫點，190／192 是伺服器自訂格式；可做的是 PBW-A（83／84）、PBW-C（168／169，隨 #561）、PBW-D（173）、PBW-E（37），已併入 11.5。
4. **完整字串表已可用**：#546 已在執行期讀入整張 `string-c.tbl`（`ServerMessageText.cs:14`）。各模組用編號占位的字串（1784／1785／2139、3123–3126、Yes/No 提示文字）可以改讀這張表，但要先確認這些編號屬於同一張表（見 11.5 的 Q-2）。
5. 商店清單（op 131／40）**沒有**改用 `TradeWindowPanel`。#526 只把修理模式 6 接上（`TradeWindowPanel.Source` 只有 `RepairTradeSource`），商店仍走 `NpcInteraction.cs:113` 的 `ShopRows`。

### 11.3 狀態變更表

「列」是第 1–5 節該列的第一格（或可唯一識別的前綴）。原狀態＝新狀態的列，只更新依據。

| 節 | 列 | 原狀態 | 新狀態 | 補上的任務 | 剩下的差異／依據 |
|---|---|---|---|---|---|
| 1 | 102 | 部分 | 已有 | #546 T228 | 執行期讀完整 `string-c.tbl`，id ≥ 筆數整包丟棄，依 `sub_933850` 格式化（`ServerMessageText.cs:14`）。讀不到表時（CI）才退回 4 個硬寫 id |
| 1 | 84 | 部分 | 已有 | T168（#440） | AN§2.4 正義值上色已做（`DisplayNotices.cs:54`） |
| 1 | 24 | 部分 | 已有 | T168（#440） | AN§2.5：`0x767D`、127＝永久、0＝立即清除（`DisplayNotices.cs:12`） |
| 1 | 213 | 部分 | 已有 | T179（#481） | AM§4 綠色脈動已做（`Afflictions.cs:69`）；只有起始相位未知 |
| 1 | 33 | 部分 | 已有 | T179（#481） | AM§4 `3+c/2` 已做（`Afflictions.cs:69`） |
| 1 | 122 | 缺 | 部分 | #510 M7-B | S36／S37 只存進快照（`AbilityStatus.cs:27`、`:40`）；S36 各 type 的意義沒查；SP／MR 沒有顯示 |
| 1 | 252 | 缺 | 部分 | #511 M7-C、#557 T235 | 城主表與 12 格動畫資料已有（`Castle.cs:136`），王冠沒畫：`CastleCrownFrames` 沒有產品呼叫；圖源與偏移表已由 T267 查到（SS§1：`crown.raw`、走路內插表）；另有暫代聊天行「[城堡]」 |
| 1 | 95 | 缺 | 部分 | #511、#557 | `tax` 頁在 WinForms NPC 面板開（`Castle.cs:160`）；頁內數字框與送出鈕沒有畫面，`SendCastleReply` 沒有入口；S185 第 3 個 C 的意義沒查 |
| 1 | 12 | 缺 | 部分 | #511、#557 | 同 op 95（`withdraw` 頁） |
| 1 | 49 | 缺 | 部分 | #511、#557 | 同 op 95（`deposit` 頁） |
| 1 | 0 | 缺 | 部分 | #512 M7-D | S156 只有資料層（`Agit.cs:67`）；`AgitAuctionUI` XML 缺資源 |
| 1 | 18 | 缺 | 部分 | #512 M7-D | S187 只有資料層（`Agit.cs:75`），開窗事件沒有訂閱者；「高 16 位元＝區域」是推測；地圖窗由 #550（T232）處理 |
| 1 | 224 | 缺 | 部分 | #513 M7-E | 在 WinForms NPC 面板底部加數字列並回 C11（`AmountPrompt.cs:76`、`:188`）；web 面板沒有數字框；C11 中間 byte 送 0 是推測 |
| 1 | 175 | 缺 | 部分 | #514 M7-G | 收 S13、送 C245／C191 已有（`SpellExchange.cs:66`），但 `RequestSpellExchangeList()` 沒有產品入口（報告原文：不能算功能完成）；暫行 WinForms 窗 |
| 1 | 133 | 缺 | 部分 | #515 M7-H | S0 type 0–6 只有資料層（`ClanMatching.cs:166`）；`SendClanMatching` 沒有 UI；TW16 op 133 對 3.81 S0 是推定（`sub_B243A0` 沒反編譯） |
| 1 | 108 | 部分 | 部分 | #548 T230、#559 T237 | 處理 62 個中的 37 個（原 23 個）；缺 B1 12 個（#547）、B3 13 個（#560、#602）。見 11.2 第 3 點 |
| 1 | 25 | 部分 | 部分 | #548 T230 | B2 的 13 個子碼只有資料與計時（`PacketBoxStatus.cs:71–74`）；59 刪哪個圖示是推定；圖示畫面等 #473（PR #600） |
| 1 | 17 | 部分 | 部分 | #568 T243 | effect 11 的資料已有（`TimedBuffs.cs:35`），沒有產品呼叫；圖示畫面等 #473 |
| 1 | 206 | 部分 | 部分 | T177（#479）、#566 T241 | 「未用 `source.lmp`」已過時：#566 已在執行期讀並組光照圖（`Ambience.cs:434`），但產品沒有呼叫（11.2 第 1 點）；±35／±15 篩選是推測 |
| 1 | 156 | 部分 | 部分 | T177（#479） | 不再本地 6 倍速推進，AM§1 的差異已修；但明暗沒有接上畫面（`Renderer.cs:9` 只有測試呼叫） |
| 1 | 176 | 部分 | 部分 | T178（#480） | 解碼、每包 L−1、粒子數與移動已做；沒有 `WeatherSprite` 實作，雨圖 surf 924–927 與雪都沒畫，粒子沒有產品掛接 |
| 1 | 114 | 部分 | 部分 | T179（#481） | 光圈用圓形 `5*24f`，沒用 `source.lmp` k=5（`Afflictions.cs:91`）；102 表物件發光、職業 6／31 沒做 |
| 1 | 229 | 部分 | 部分 | T179（#481） | 逐掃描線正弦已做（`TimedBuffs.cs:95`）；`sub_81B0E0` 為假時位移減半、地圖 67／420 最低醉度沒做 |
| 1 | 160 | 部分 | 部分 | T179（#481） | 他人隱身一律不畫；`$431`、`sub_956D30` 的隊伍／血盟可見例外沒做 |
| 1 | 36 | 部分 | 部分 | T168（#440） | AN§2 #7、#8、#19 已做（`TitleAndEmote.cs:91`）；自己名稱 X0 是近似值；字型屬第 5 節 `sub_8444A0` |
| 1 | 186 | 部分 | 部分 | T168（#440）、#509 | 沒做：+181 圖示、隊伍與主人圖示、+252 行、物件種類 2／3／9／10 過濾、怪物等級差外框；S87 同身份原地覆寫（PG§7）待驗證 |
| 1 | 16 | 部分 | 部分 | T170（#442） | AN§3.4 第 3 點沒做：沒有 profile 的變身 gfx 仍顯示「尚未支援此外觀」，沒有退回 gfx 1120（`PlayerProfiles.cs:21`）；欄位 108／111／113 一律 0 |
| 1 | 226 | 部分 | 部分 | T170（#442） | 他人缺動作圖時整個不畫，沒有逐動作退回（`ObjectRendering.cs`） |
| 1 | 5 | 部分 | 部分 | #522 M7-P | 原生路徑已照 `!` 格；舊素材路徑（`ComposeMagic` 寫死 221 等 gfx）仍在收包時立即顯示並受擊（`MagicRendering.cs:254`）；SV#4 轉向、SV#12 提亮沒做 |
| 1 | 240 | 部分 | 部分 | #522 M7-P | 同 op 5（`AttackSkillPackets.cs:95`） |
| 1 | 137 | 部分 | 部分 | #522 M7-P | 同 op 5 |
| 1 | 85 | 部分 | 部分 | #516 M7-J、#558 T236 | 通用詢問槽已有，組隊／決鬥／求婚已改用；提示與 YES／NO 鈕是 WinForms，不是 `IUiPanel`，鈕 32×14 是推定；提示文字不讀字串表；血盟、盟戰、復活 322 還沒移入；交易由 #517（PR #594）處理 |
| 1 | 197 | 部分 | 部分 | T151（#414） | S219／321、322 確認、C121、S241 移動鎖已做（`Resurrection.cs:16`）；5 秒上限是引擎自訂；原版 S241 行為與 321／322 文字沒核對 |
| 1 | 161 | 部分 | 部分 | T135（#316）、T168 | C72 查詢與快取已有；上傳 C18 沒送；S250／173 盟徽開關沒接（B3，#602） |
| 1 | 92 | 部分 | 部分 | T169（#441） | AN§1 #1–#6 已做；`hold=1` 的 1234 ms 計時器、節流速度係數沒做 |
| 1 | 91 | 部分 | 部分 | T173（#447） | S22 三種模式、距離衰減、全域 200 ms 冷卻、4 類音量已做（`SoundRules.cs:11`）；播放中不隨距離更新音量（原版 `A091C0`）；`ext` 參數沒做 |
| 1 | 144 | 部分 | 部分 | #524 M7-R、#556 T234 | 伺服器主動送的 S39 頁（含 `monlist`）與查詢結果都進 NPC 面板（`NpcInteraction.cs:65`、`:78`）；仍是 WinForms，不是 `HYPER_TEXT_WINDOW` |
| 1 | 136 | 部分 | 部分 | #526 M7-S | 原生 `TRADE_WINDOW` 模式 6 只在 `--native-ui` 時出現（`WeaponRepairUi.cs:17`）；列版面、框 1984、拉桿、按鈕狀態圖是推測或沒畫 |
| 1 | 131 | 部分 | 部分 | （無） | #526 沒涵蓋商店，仍走 `ShopRows`（`NpcInteraction.cs:113`） |
| 1 | 40 | 部分 | 部分 | （無） | 同 op 131 |
| 1 | 89 | 部分 | 部分 | #531 M7-X | 約 8 分 21 秒逾時送 C122 已做（`Recovery.cs:110`）；死亡窗見第 4.1 節 `UserPlayStatsUI` |
| 2 | 179 | 部分 | 部分 | （無） | PBW§5.3：原版用通用模態訊息框 `sub_8E1E30` 顯示（並存 `Capture\NewsNNNN.txt`）；引擎收下後自動回 C16、不顯示 → R2-Q |
| 2 | 107 | 缺 | 部分 | #551 T233 | 送 C14、收 S139，保存語系與功能旗標（`ServerFeatures.cs:157`）；拾取節流還沒改用伺服器旗標（`Ground.cs:84`）；沒有用 3.81 真機測過 |
| 3 | C_VERSION(122) | 缺 | 部分 | #551 T233 | 同第 2 節 op 107 |
| 3 | C_EXCHANGEABLE_SPELL(125) | 缺 | 部分 | #514 M7-G | 同第 1 節 op 175（沒有產品入口） |
| 3 | C_MATCH_MAKING(221) | 缺 | 部分 | #515 M7-H | 同第 1 節 op 133（沒有 UI） |
| 3 | C_TAX(41) | 缺 | 部分 | #511 M7-C | C19／C44／C56 有 API（`Castle.cs:216`），沒有入口；治安 C_CHANGE／QUERY_CASTLE_SECURITY 伺服器不支援，維持不做 |
| 3 | C_ONOFF(184) | 缺 | 部分 | #549 T231 | C26 能送（`ChatChannelToggle.cs:62`），但聊天列沒接 `ChatChannelCommands.IsCommand`，`/on`、`/off` 會被當成一般聊天送出；選項窗沒做 |
| 3 | C_OPEN(131) | 部分 | 已有 | #519 M7-M | 先走近到 1 格內再送 C41、死亡時擋點擊（`Doors.cs:106`、`:115`）；剩下的只是未驗證項（尋路是否逐步相同） |
| 3 | C_GET(23) | 部分 | 部分 | #518 M7-L | 新流程已有（`Ground.cs:82`、`:104`），但滑鼠雙擊地上物品仍走 `Pointer.PickupAsync`（數量 1、不走近，`PlayerPointerUi.cs:19`）；牆阻擋沒做；旗標 `0x10000` 是推測 |
| 3 | C_DIALOG(51) | 部分 | 部分 | #513 M7-E | C11 已補；剩原版 `HYPER_TEXT_WINDOW` 版面 |
| 3 | C_USE_SPELL(216) | 部分 | 部分 | #523 M7-Q | 本地預測動作已拿掉；冷卻用 `ReuseMs` 暫代技能資料 `+56`；SV#3 選目標游標、SV#4 轉向沒做；沒有 +100 ms 餘裕，是否會被判加速要真機確認 |
| 3 | C_CHANNEL(55) | 部分 | 部分 | #521 M7-O | 送 MapSystem 地區 id、存 10 筆（`LocationShare.cs:11`、`:23`）；字串 1784／1785／2139 與 mapDesc 是編號占位（T267 SS§4：前三個在 `string-c.tbl`，mapDesc 在 `MapDesc-c.tbl`）；10 筆清單沒有畫面；沒有 `TransferMenu` 入口 |
| 3 | C_EXIT_GHOST(179) | 部分 | 部分 | #520 M7-N | 常駐鈕已拿掉，但 `exitghost` HTML 入口沒接，目前沒有任何 UI 能離開幽靈；`GhostSkipsSelfAppearance` 沒有呼叫點 |
| 3 | C_DEAD_RESTART(58) | 部分 | 部分 | #531 M7-X | 同第 1 節 op 89 |
| 3 | C_BOOKMARK(84) | 部分 | 部分 | T124（#258） | 拿掉「審查中」：協定已合併；視窗是 WinForms 分頁（`BookmarkUi.cs:60`），原生窗由 #472 處理；C_BOOK 仍待驗證 |
| 3 | C_SAY(16) | 部分 | 部分 | T175（#449） | 12 色調色盤已做；喊話用伺服器的 `<name>`，原版是 `[name]`；`*` 開頭變色、`\f`／`\b` 色碼、`$` 前綴沒做；C199 沒送（`PlayerHudChat.cs:40`） |
| 3 | C_QUERY_PERSONAL_SHOP(86) | 部分 | 部分 | #527 M7-T | 開店設定窗 `PersonalShopPanel` 與 C38 開關店封包已有，但面板沒有註冊、`Confirmed` 沒有接送出（見第 4.1 節） |
| 4.1 | PersonalShopUI | 缺 | 部分 | #527 M7-T | 原生面板從 `PersonalShopUI.xml` 讀版面（`NativeUi/PersonalShopPanel.cs`），但沒有加進 `UiPanelRegistry.cs`，產品看不到 |
| 4.1 | UserPlayStatsUI | 部分 | 部分 | T151、#531 M7-X | 重新開始／結束／取消已有（`Recovery.cs:10`）；統計值只顯示「—」（T267 SS§3：客戶端以基準快照相減，殺怪數在 3.81 恆為 0）；字串 3123–3126 用 WO 的中文代替（SS§4：在 `string-c.tbl`）；外框 surf 5323 與按鈕狀態圖沒做；仍是 WinForms，不是 `IUiPanel` |
| 4.1 | MainCharInfoUI | 部分 | 部分 | #464 T191 | 原生 `LEFT_WINDOW` 已有（`NativeUi/LeftStatusPanel.cs`），預設關閉 |
| 4.1 | MainChatUI | 部分 | 部分 | #468 T195 | 原生 `CENTER_WINDOW` 已有（`NativeUi/ChatPanel.cs`），預設關閉；中文 IME 沒有真機驗證 |
| 4.1 | HyperTextUI、HyperTextSubUI | 部分 | 部分 | #524、#556 | 見第 1 節 op 144；查詢頁與 NPC 頁並列，原版是單窗取代 |
| 4.2 | `HpMpWindow` | 部分 | 部分 | #465 T192 | 原生浮動窗與頭上條已有（`NativeUi/HpMpPanel.cs`），預設關閉；閃爍頭上條 surf 8393、5866–5868 本機缺檔 |
| 4.2 | `MacroWindow` | 部分 | 部分 | T176（#450）、#466 T193 | F5–F12 共 8 格、3 分頁、鎖定、擴充巨集已有（`NativeUi/MacroPanel.cs`），預設關閉；選單鈕由 #467（PR #596）處理 |
| 4.2 | `TradeWindow` | 部分 | 部分 | #526 M7-S | 只有修理模式 6；商店、私商購買仍是 WinForms |
| 4.2 | `InputWindow` | 部分 | 部分 | #558 T236 | YES (361, 567)／NO (401, 567) 鈕已有（`YesNoPrompt.cs:153`），是疊在畫面上的 WinForms 控制項；舊 HUD 下的位置是權宜做法 |
| 4.2 | `BoardListWindow` | 部分 | 部分 | #528 M7-U | 清單窗 6 鈕、閱讀窗 4 鈕依 WIN§2 T130 畫出（`NativeUi/BoardPanels.cs`），只在原生 UI 開啟時掛上；背景 656／670、清單列與文章文字沒畫（仍由 WinForms `BoardUi` 顯示）；`BoardWriteWindow` 只有資料；672／676 用途待驗證 |
| 4.2 | `QuitWindow` | 缺 | 缺 | T245（#602） | 已查（PBW§4）：死亡逾時送 C_QUIT 後開 `uiextra86.xml` `QUIT_WINDOW`（重新開始／離開／取消）；引擎逾時只送 C122（`Recovery.cs`）。可直接補 → R2-D（PBW-B） |
| 4.2 | `CCookWindow` | 部分 | 部分 | #559 T237 | S250 52 的資料與版面常數已有（`Cooking.cs:42`），沒有 `COOK_BOOK_WINDOW` 原生面板 |
| 5 | `sub_A06020` | 部分 | 已有 | T173（#447） | SD#5 單次播放與同 id 200 ms 冷卻已做，三條管線共用一份冷卻表（`SoundRules.cs:70`） |
| 5 | `LineageSpell::CSpell` | 部分 | 已有 | #523 M7-Q | 送 C6 時不再本地播動作（`Magic.cs:80`） |
| 5 | （門類別 7／8 | 部分 | 已有 | #519 M7-M | 同第 3 節 C_OPEN |
| 5 | `sub_A4B060` | 缺 | 部分 | #567 T242 | 一般 sprite 的 `]` 與 `{`／`}` 已做（`SubEffects.cs:157`）；`.s32` 地圖特效、門、自己的走路／呼吸動作、飛行物、落點特效、子特效影格的音效與 `!` 沒做；`{`／`}` 的時間單位是推測 |
| 5 | `sub_A57C60` | 缺 | 部分 | #463 T190 | C2 畫布有 Window 圖層與前後順序（`NativeUi/UiRoot.cs:122`、`UiInput.cs:54`），但沒有 4 層上限，且預設關閉 |
| 5 | `sub_A06210` | 部分 | 部分 | T166（#434）、T173 | 5 槽、距離平方排序、共用冷卻已做；但 `MapAmbientSound.Backend` 沒有產品端設定，**不會出聲**（`MapAmbientSound.cs:149`）；沒乘上 EnvSound 類別音量 |
| 5 | `sub_F0B570` | 部分 | 部分 | #566 T241 | 光源表與 `Source.lmp` 已讀入；畫面沒有接上（11.2 第 1 點） |
| 5 | `sub_F112F0` | 部分 | 部分 | #518、#519 | 走近（`ApproachWalk.cs`）已有；死亡阻擋只做在門，NPC 與攻擊沒有統一 |
| 5 | music.def／envsound.def 背景音樂與環境音（T172） | 新增 | 部分 | T172（#446） | 本表原本沒有這一列。定義檔解析與播放政策已有（`BackgroundMusic.cs`、`MapEnvSound.cs`），但產品沒有呼叫 `ClientSoundScene.Configure`，不會出聲；頭目 226 曲、SPAWN／DEATH 曲、MIDI、聲像沒做 |

未列出的列狀態不變。仍為「缺」、且有待合併 PR 的列：op 52 與 op 228（#530，PR #595）；`SummonUI`（同上）；`AgitMapWindow`（#550）。

### 11.4 統計（第二輪）

以第 1–5 節原表（已含 T245 的回填：58／164／67）套用 11.3 的 79 列變更（`py -3 -I docs/reference/rea-scripts/gap_count_r2.py docs/reference/gap-matrix.md`），共 290 列：

| 節 | 已有 | 部分 | 缺 | 其中需先查證 | 其中伺服器不支援／不建議 | 其中可直接補 |
|---|---:|---:|---:|---:|---:|---:|
| 1. 收包：第一組 dispatch | 40 | 62 | 12 | 0 | 9 | 3 |
| 2. 收包：第二組 | 6 | 3 | 4 | 0 | 4 | 0 |
| 3. 送包：`sub_951F80` 名稱表 | 9 | 38 | 5 | 0 | 5 | 0 |
| 4. UI 視窗 | 0 | 47 | 26 | 0 | 19 | 7 |
| 5. 物件、技能、道具、地圖事件與繪製的處理函式 | 12 | 25 | 1 | 0 | 1 | 0 |
| **合計** | **67** | **175** | **48** | 0 | 38 | 10 |

和第 6 節的 T245 回填後數字比較：「缺→部分」19 列、「部分→已有」9 列、新增 1 列（背景音樂）。「部分」變多，是因為補上的大多是**協定或資料層**，原版視窗、畫面掛接或字串還沒做。全表「需先查證」為 0（T245 查完最後兩列）。

仍為「缺」且「可直接補」的 10 列：op 52、op 228、`SummonUI`（#530，PR #595）；`AgitWindow`／`AgitMapWindow`（#550）；`QuitWindow`（R2-D，PBW-B）；`BookmarkMemoryWindowUI`（建議併入 #472 之後的書籤後續）；op 132 S_SHOW_MAP（PG 任務 D，需原生窗，低）；`StatInfoUI`（隨重置窗，本機缺 23 個 surf）；`UserObtainUI`（低）；`AgitAuctionUI`（XML 缺資源）。後四列本批不分組。

### 11.5 下一批 code 任務分組（17 組）

**原則**（沿用第 7 節）
- 同一個既有檔案只分給一組。新檔用該組自己的名稱。
- 新封包一律用 M7-A 的擴充點（`[WorldReceive]`、`[WorldReset]`、`[WorldSnapshot]`、`[WorldModuleTest]`），不改 `WorldSession.cs`。
- `UiPanelRegistry.cs` 是共用的註冊檔：每組只准加自己那一行（#472、#474 也是這樣做），衝突時照常設授權自行同步。
- 下列待合併 PR 用到的檔案，本批不碰，列為依賴：#594（`Trade.cs`、`TradeUi.cs`、`YesNoPrompt.cs`）、#595（`Pets.cs`、`PetUi.cs`、`PetTarget.cs`、`NativeUi/SummonPanel.cs`）、#593（`Buddies.cs`、`BuddyUi.cs`、`NativeUi/FriendPanel.cs`）、#596–#600（`NativeUi/` 的選單鈕、小地圖、隊伍、血盟、狀態圖示面板與 `LeftStatusState.cs`、`UiClientBridge.cs`）、#604（`NativeUi/ItemWindowPanel.cs`、`SpellWindowPanel.cs`、`WarehouseWindowPanel.cs`）、#579（`ClanWarehouseHistory.cs`）；排隊中的 #547（`PacketBoxMessages.cs`）、#550（`Agit.cs`、`NativeUi/AgitMapPanel.cs`）、#472（`NativeUi/BookmarkPanel.cs`）、#474（`NativeUi/RedNoticePanel.cs`）。
- 驗收照 #503 暫行政策：編譯＋既有單元測試綠，每組另加自己的離線測試（新檔）。標「需先查證」的部分只做協定與資料，不做畫面。

| 組 | 目標 | 可改的檔案 | 依賴 | 引用 |
|---|---|---|---|---|
| R2-A 環境表現接上畫面 | 在 `PaintScene` 呼叫 `ComposeAmbienceLights`／`ShadeAmbience`／`PaintAmbience`，讓日夜明暗與 FX1 光照圖出現在畫面上；實作 `WeatherSprite`（雨 surf 924–927、程序雪）並在每格推進 `WeatherParticles`；盲目光圈改用 `source.lmp` k=5；酒醉補 `sub_81B0E0` 為假時位移減半、地圖 67／420 最低醉度；隱身補隊伍／血盟可見例外 | `Program.cs`（只限 `PaintScene`）、`Renderer.cs`、`Ambience.cs`、`MapEffects.cs`、`Afflictions.cs`、`Invisibility.cs`、`TimedBuffs.cs`（只限酒醉）、新檔 `WeatherSprite.cs`、`AmbienceWiringTests.cs` | 無 | AM§1–§4；EF2§1；第 1 節 op 156、206、176、114、229、160；第 5 節 `sub_F0B570` |
| R2-B 聲音接上產品 | 實作 `IMusicStreamAudio`／`IEnvSoundAudio` 與 `^` 環境音的產品後端（走 `engine/audio` 的既有傳輸），在進入世界時呼叫 `ClientSoundScene.Configure` 並設定 `MapAmbientSound.Backend`；環境音乘上 EnvSound 類別音量；播放中隨距離更新音量（`A091C0`） | 新檔 `ProductSoundBackends.cs`、`ProductSoundTests.cs`；`ClientSoundScene.cs`、`MapAmbientSound.cs`、`AudioApiTransport.cs`、`SoundRules.cs`、`engine/audio/` | 無 | SD#4–#14；REA B04；第 1 節 op 91；第 5 節 `sub_A06210`、背景音樂列 |
| R2-C 點擊與拾取收尾 | 雙擊地上物品改走 `PickupGroundClicked`（走近、整疊或數量提示）；死亡阻擋套到 NPC 與攻擊點擊；幽靈中擋滑鼠攻擊（`GhostLocalBlocked`）；拾取節流改讀 S139 功能旗標；門提示改讀 `DoorNotice` | `PlayerPointer.cs`、`PlayerPointerUi.cs`、`PlayerPointerHud.cs`、`Ground.cs`、`GroundUi.cs`、`ApproachWalk.cs`、新檔 `PointerFlowTests.cs` | 無 | WO 第 1、2、4 節；PG§4；第 3 節 C_GET；第 5 節 `sub_F112F0`、`sub_5001C0` |
| R2-D 幽靈、死亡窗與離開窗 | 提供 `exitghost` 動作的處理（由 R2-F 接到 HTML 入口）；S85 接 `GhostSkipsSelfAppearance`；死亡統計窗改成 `IUiPanel`（外框 surf 5323、按鈕狀態圖），字串 3123–3127、3131 改讀字串表；統計值照 SS§3（經驗／金幣差、道具清單筆數、殺怪 0、地監時間 S250／159）；**PBW-B**：原生 `QUIT_WINDOW`（重新開始 C177／C7、離開 C122、取消），死亡逾時改開此窗＋訊息 47 | `GhostMode.cs`、`Recovery.cs`、`OnlineInput.cs`、新檔 `NativeUi/UserPlayStatsPanel.cs`、`NativeUi/QuitWindowPanel.cs`、`UserPlayStatsTests.cs`；`UiPanelRegistry.cs` 兩行 | 無（Q-2 已由 T267 查完） | WO 第 4、6 節；PBW§4；第 4.1 節 `UserPlayStatsUI`；第 4.2 節 `QuitWindow`；第 3 節 C_EXIT_GHOST、C_DEAD_RESTART |
| R2-E 商店改用 `TRADE_WINDOW` | 新增商店用的 `TradeWindowPanel` 資料來源（買、賣），接上 S70／S65 並回 C38；修理模式 6 的列版面、框 1984、拉桿、按鈕狀態圖依 Q-1 修正；拿掉 WinForms 商店清單 | `NpcServices.cs`、`NativeUi/TradeWindowPanel.cs`、`WeaponRepairUi.cs`、新檔 `ShopTradeSource.cs`、`ShopTradeTests.cs`；`NpcInteraction.cs` 只限 `ShopRows` 呼叫點 | R2-F 合併（共用 `NpcInteraction.cs`）；Q-1 ✅（T266，TWP§1.9） | WIN§2 T102、T134；WIN§1.5；TWP§1；第 1 節 op 131、40、136；第 4.2 節 `TradeWindow` |
| R2-F NPC 對話面板收尾 | 城堡 `tax`／`withdraw`／`deposit` 頁內的數字框與送出鈕接 `SendCastleReply`，拿掉暫代聊天行「[城堡]」；S136 數字框補到 web 面板；把 R2-D 的 `exitghost` 接到 HTML 動作；以物品學魔法的入口（`RequestSpellExchangeList`）在 Q-3 查到送出點後接上 | `NpcInteraction.cs`、`NpcUi.cs`、`Castle.cs`、`AmountPrompt.cs`、`SpellExchange.cs`、`SpellExchangeUi.cs`、`engine/web/` 的 NPC 互動腳本、新檔 `NpcPageTests.cs` | R2-D（`exitghost` API）；Q-3（只擋學魔法入口） | WIN2§5.1、§5.3；第 1 節 op 95、12、49、224、175；第 3 節 C_TAX、C_DIALOG |
| R2-G 聊天收尾 | 聊天列接 `ChatChannelCommands.IsCommand`（`/on`、`/off` 不再當聊天送出）；喊話改 `[name]`；`*` 開頭變色、type 18／25 自帶色號、`\f`／`\b` 色碼、血盟第二色 #BDBDBD、`$` 前綴；補送 C199 | `PlayerChat.cs`、`PlayerHudChat.cs`、`PlayerChatKeyboardFlow.cs`、`ChatChannelToggle.cs`、`NativeUi/ChatPanel.cs`、`NativeUi/State/ChatState.cs`、新檔 `ChatFormatTests.cs` | 無 | UI§3；PG§6 C_ONOFF；第 1 節 op 112、61、104、153；第 3 節 C_SAY、C_ONOFF；第 5 節 vtable `0x1165C80` |
| R2-H 技能表現收尾 | 舊素材路徑（`ComposeMagic` 寫死的 gfx）改走 `!` 時機；SV#4 轉向、SV#12 提亮；`]` 擴到飛行物與落點特效；冷卻改用技能資料 `+56`／`+60`（Q-3 查到後）；kind 6／7 等 Q-4 | `MagicRendering.cs`、`AttackSkillPackets.cs`、`ProjectileRendering.cs`、`ActionCadence.cs`、`SkillCast.cs`、`SubEffects.cs`、新檔 `SkillVisualTests.cs` | Q-3（只擋冷卻）；Q-4 ✅（T266，TWP§3.6） | SV§1–§4、§7；EF2§2；TWP§3；第 1 節 op 5、240、137；第 3 節 C_USE_SPELL；第 5 節 `!`、109 kind、`sub_A4B060` |
| R2-I 物件繪製收尾 | 變身缺 profile 時退回 gfx 1120、他人逐動作退回；名稱補 +181 圖示、隊伍與主人圖示、+252 行、物件種類 2／3／9／10 過濾、怪物等級差外框；S76 接 `GhostSkipsSelfAppearance`；城主王冠照 SS§1（`crown.raw`）接 `CastleCrownFrames` | `ObjectRendering.cs`、`PlayerProfiles.cs`、`Polymorph.cs`、`TitleAndEmote.cs`、新檔 `ObjectDrawTests.cs` | 無（Q-2 已由 T267 查完） | AN§2、§3.4；WIN2§5.1；第 1 節 op 16、226、186、36、252 |
| R2-J Yes/No 收尾 | 復活 322 移進共用詢問槽；Esc 交給詢問槽送拒絕；拿掉組隊 30 秒、求婚 15 秒的本地逾時與 PartyUi／DuelUi 的回覆鈕；提示文字改讀字串表；補發起求婚的入口；YES／NO 鈕改成 `IUiPanel` | `YesNoPrompt.cs`、`Party.cs`、`PartyUi.cs`、`Duel.cs`、`DuelUi.cs`、`Marriage.cs`、`Resurrection.cs`、`KeyboardNavigation.cs`、新檔 `NativeUi/YesNoPanel.cs`、`YesNoFollowupTests.cs`；`UiPanelRegistry.cs` 一行 | #517（PR #594）合併 | WIN§2.1；WO 第 6 節；WIN2§5.3；第 1 節 op 85、197；第 3 節 C_ANSWER；第 5 節 `sub_4FC710` |
| R2-K 血盟收尾 | 送 C18 上傳盟徽、C63 階級設定；入盟與盟戰詢問移進共用詢問槽；**PBW-D**：S250／173 存「顯示盟徽」旗標、類型 130 顯示訊息 3383；血盟推薦的 UI 等 Q-3 查完 `sub_B243A0` | `Clan.cs`、`ClanEmblem.cs`、`ClanUi.cs`、`ClanWar.cs`、`ClanMatching.cs`、新檔 `ClanFollowupTests.cs` | R2-J（詢問槽改動）；Q-3（只擋推薦 UI） | AN§2.8；WIN2§3；PG§2.3；PBW§2；第 1 節 op 161、110、133、63；第 3 節血盟、C_EMBLEM、C_MATCH_MAKING |
| R2-L 狀態圖示資料收尾 | B2 的 82／75／180 補文字與圖示對照，59 刪除對象依原版修正；水中呼吸 effect 11 與 S36／S37（SP／MR）接到 #473 的圖示與左狀態框資料 | `PacketBoxStatus.cs`、`StatusIcons.cs`、`MagicStatus.cs`、`AbilityStatus.cs`、新檔 `StatusIconFeedTests.cs` | #473（PR #600）、#464 後續（PR #598 的 `LeftStatusState.cs`）合併 | UI§4；EF2§3；PG§2.3 B2；第 1 節 op 25、17、122 |
| R2-M 發送座標收尾 | 字串 1784／1785／2139 改讀 `string-c.tbl`、mapDesc 改讀 `MapDesc-c.tbl`（SS§4）；10 筆清單改用 `[WorldReset]` 清空並加畫面；`+16`＝0 分支 | `LocationShare.cs`、新檔 `NativeUi/LocationListPanel.cs`、`LocationShareFollowupTests.cs`；`UiPanelRegistry.cs` 一行 | 無（Q-2 已由 T267 查完） | WO 第 5 節；第 3 節 C_CHANNEL；第 4.2 節 `MapLocationSaveWindow` |
| R2-N 個人商店窗接上產品 | 把 `PersonalShopPanel.Registration` 加進註冊表；從背包選格（`PersonalShopCandidate`）、`Confirmed` 送 C38、關店送 C38 type 1；物品圖示來源 | `PrivateShopUi.cs`、新檔 `PersonalShopIntegration.cs`、`PersonalShopIntegrationTests.cs`；`UiPanelRegistry.cs` 一行 | 無 | WIN§1.1；SP C38；第 4.1 節 `PersonalShopUI` |
| R2-O 畫面中央訊息與震動（PBW-A） | S250／84 類型 2 → 畫面中央訊息佇列（7 秒、最多 14 則、`$` 字串表代換）；S250／83 → 1 短震動、2 持續晃動開關、3 隨機煙火 gfx 2042／2030／2029 | 新檔 `PacketBoxScreen.cs`、`PacketBoxScreenTests.cs` | R2-A（畫面震動掛在 `Renderer.cs`，由 R2-A 留掛點）；#474（紅字面板，避免兩處畫中央訊息） | PBW§2 PBW-A；第 1 節 op 108 |
| R2-P 寵物裝備（PBW-E） | S250／37 更新寵物 AC 與裝備欄，不回送 C99 | 新檔 `PetEquipment.cs`、`PetEquipmentTests.cs` | #530（PR #595，`Pets.cs`）合併 | PBW§2 PBW-E；第 1 節 op 108 |
| R2-Q 登入公告顯示 | S48（TW16 op 179 S_NEWS）改用通用模態訊息框顯示，確認後才回 C16 | `LoginConnection.cs`、`ClientLogin.cs`、新檔 `LoginNewsTests.cs` | 無 | PBW§5.3；第 2 節 op 179 |

**建議順序**：先派 R2-A、R2-B、R2-C、R2-G、R2-I、R2-N、R2-Q（不等別人）；R2-A 合併後派 R2-O；#530 合併後派 R2-P；R2-D 合併後派 R2-F，R2-F 合併後派 R2-E；#594 合併後派 R2-J，R2-J 後派 R2-K；#600 與 #598 合併後派 R2-L；R2-M 可以直接派（Q-2 已由 T267 查完）。

**另外建議（需要使用者決定，不開 code 任務）**：原生 UI 目前預設關閉。等 #475（T201）真機 smoke 通過後，再決定要不要改成預設開啟。

### 11.6 需先查證（還沒有查證依據的列）

| 代號 | 要查的內容 | 起點 | 影響的組 |
|---|---|---|---|
| ~~Q-1 視窗版面~~ | ✅ T266（#627）已查（[TWP§1–2](trade-window-and-projectiles.md)）：模式表、元件索引、7 列×36 px 列版面、按鈕 n／n+1 兩態、數量上限、各模式送包、空清單頁；`LoadingWindowUI` 是 TW16 XML 窗，3.81 沒有。剩確認清單 `sub_A2C660` 的來源待驗證（T275 已補：S250／162 填寫，3.81 不送 → 不跳確認，見 11.10） | TWP | R2-E |
| ~~Q-2 圖源與字串~~ | ✅ T267（#628）已查（[`sprites-and-strings.md`](sprites-and-strings.md)，下稱 SS）：王冠＝`Sprite.pak` 的 `crown.raw`（12 格 16×12 RGB555，0 透明），偏移表是人物走路內插表（SS§1）；盟屋地圖 surf＝`dword_135DA34` {689,741,741,801,1482}（`aAvadentelui` 是 IDA 誤命名），標記位置 350 筆由 `sub_113F600` 靜態初始化（SS§2）；死亡統計值由客戶端以基準快照相減，殺怪數只來自 S250／61（3.81 不送，恆為 0），地監時間走 S250／159（SS§3）；1784、1785、2139、3123–3127、3131 都在 `string-c.tbl`，**mapDesc 查的是 `MapDesc-c.tbl`，不是 `string-c.tbl`**（SS§4） | SS | R2-D、R2-I、R2-M；#550 |
| ~~Q-3 送出點與欄位~~ | ✅ T268（#629）已查（[`send-sites-2.md`](send-sites-2.md)，簡稱 SS2）：結果見 11.9；T267 轉入的「死亡統計窗 TW16 op 10／9」＝3.81 C63 `C(9)` 要地監時間清單（SS2§14） | SS2 | R2-D、R2-F、R2-H、R2-K、R2-L |
| ~~Q-4 技能飛行~~ | ✅ T266（#627）已查（[TWP§3](trade-window-and-projectiles.md)）：kind 6 `EarthRising` 在目標處播一次、每個 `!` 受擊；kind 7 `Eruption` 每段第三格生下一段、只由終點 EarthRising 受擊；`]` 只在 `UserObject`（角色、門、`.s32` 地圖特效）與預設 `Effect` 處理，飛行物與 kind 2／6／7 不處理 | TWP | R2-H |
| ~~T245 #602~~ | ✅ 已合併（[`packetbox-windows.md`](packetbox-windows.md)）：B3 子碼、`QuestUI`、`QuitWindow`、`CNoticeWindow`、S_NEWS 的顯示方式都已查；結果已併入 11.3 與 11.5（R2-D、R2-K、R2-O、R2-P、R2-Q）。剩 `QuitWindow` 的 ESC 入口與 192 的讀取者待驗證（T275 已補：ESC／選單都不開；192＝角色選項旗標，見 11.10） | PBW | — |

建議開 **3 張 resource 任務**：Q-1＋Q-4（畫面與版面）、Q-2（圖源與字串，量小，可先做）、Q-3（送出點與欄位）。

### 11.7 限制

- 只讀報告與原始碼，沒有建置或執行；「產品沒有呼叫」是用 Grep 搜呼叫點判斷，以反射或事件間接呼叫的可能會漏。
- 各任務都沒有真機驗收；「已有」只表示程式和已查證的表現沒有已知差異。
- 本節的 `檔案:行` 以 main `6d07fe0d` 為準。

### 11.8 T266（#627）回填：Q-1＋Q-4

依 [`trade-window-and-projectiles.md`](trade-window-and-projectiles.md)（TWP）。本輪**狀態都不變**，只更新依據，所以 11.4 的統計不變（`gap_count_r2.py` 重算仍是 67／175／48）。

| 節 | 列 | 原狀態 | 新狀態 | 補上的任務 | 剩下的差異／依據 |
|---|---|---|---|---|---|
| 1 | 136 | 部分 | 部分 | T266 | TWP§1：模式 6 的元件、7 列×36 px 列版面、OK 1996、按鈕 n／n+1、每個數量 >0 的列各送一包 C20；空清單時，點的 NPC 外型 727 開 `touma3`，否則開 `gayle5`。引擎列高 16、只送一筆、沒有框與拉桿 → R2-E |
| 1 | 131 | 部分 | 部分 | T266 | TWP§1.1、§1.6：TW16 走 `CNpcShop`。`TradeWindow` 保留的模式 0（BUY 1992、單筆上限依單價 9999／999／20／1）送 op 89，與 3.81 C161 `C_Result` 逐欄相符 → R2-E 用模式 0 |
| 1 | 40 | 部分 | 部分 | T266 | 同 op 131，模式 1（SELL 1998）；賣出前字串 3133 確認，確認清單 `sub_A2C660` 的來源待驗證 |
| 1 | 44 | 部分 | 部分 | T266 | TWP§1.1：學魔法窗＝`TRADE_WINDOW` 模式 7（BUY 鈕、顯示單價＝(`id>>3`＋1)²×基價），送 C39；空清單依 NPC 外型 237／2820／其他開 `GerengEv3`／`Sedia1`／`Siriss1`（R2-F） |
| 1 | 175 | 部分 | 部分 | T266 | TWP§1.6：模式 7（不顯示單價），送 C191（Q-3 的送出點）；空清單開 `HorunEv3`（R2-F） |
| 1 | 5 | 部分 | 部分 | T266 | TWP§3.2–3.3：kind 7 逐段推進、只由終點 EarthRising 受擊（引擎目前不畫、改在施法者 `!` 格受擊）；kind 6 不處理 `]` → R2-H |
| 4.1 | LoadingWindowUI | 部分 | 部分 | T266 | TWP§2：TW16 XML 窗（`LoadingScene.xml`，本機缺），3.81 快照沒有任何 `Loading`／`.xml` 字串 → 3.81 世代沒有讀取畫面，**不建議補**；維持文字狀態 |
| 4.2 | `TradeWindow` | 部分 | 部分 | T266 | TWP§1：入口只有修理 6、學魔法 7、傭兵 15、弓兵 16；商店 0／1、私商 13／14 是保留模式；元件索引見 TWP§1.3 |
| 5 | `sub_A4B060` | 部分 | 部分 | T266 | TWP§3.4：`.s32` 地圖特效與門是 `UserObject`，**原版有** `]`；落點圖（預設 Effect）有；飛行物、kind 2／6／7、`EffectLoop` **原版沒有**（不必做）；子特效的 `!` 不受擊（沒有目標），`+0x20` 音效照播 |

**Q-3 的附帶結果：** `C_EXCHANGEABLE_SPELL`（op 210→C191）與學魔法（op 123→C39）的送出點都是 `TRADE_WINDOW` 模式 7 的 `sub_A2BD10`（TWP§1.6），op 44 學魔法窗＝`TRADE_WINDOW` 模式 7。Q-3 其餘項目不變。

### 11.9 Q-3 回填（T268／#629，2026-11-09）

> 與 11.8 的關係：本輪的 `C_EXCHANGEABLE_SPELL`(125) 是**要清單**的請求（HyperAct `xchgspell`，3.81 C245）；11.8 的 op 210→C191 是學會時的**確定**送出（`sub_A2BD10`）。兩者不同封包，不衝突。

依據 [`send-sites-2.md`](send-sites-2.md)（SS2）。這一輪只是查證，**11.3 的狀態與 11.4 的統計都沒有改**。下表「建議」欄是給下一輪盤點或對應 R2 組參考的。

| 節 | 列 | 查到什麼 | 建議 |
|---|---|---|---|
| 1 | 175 | 收包後清單進 `TRADE_WINDOW` 模式 7（`+153`＝0）；空清單開 `HorunEv3`（SS2§1.2） | R2-F：空清單改開頁 |
| 1 | 44 | 同一個窗，`+153`＝1；價格＝基礎價×((索引>>3)+1)²；空清單依 NPC 外型開 `GerengEv3`／`Sedia1`／`Siriss1`（SS2§1.2） | R2-F；版面歸 Q-1 |
| 3 | C_EXCHANGEABLE_SPELL(125) | 唯一送出點是 HyperAct `xchgspell`（`sub_585520`），只在 NPC 外型 834 時送 `cd`＋NPC id；`buyspell` 在外型 ≠ 834 時送 C24（SS2§1.1） | R2-F：`xchgspell` 接 `RequestSpellExchangeList()` |
| 1 | 133 | 處理函式要 `dword_153FCC4` 不為 0 才會呼叫，但這個全域沒有寫入點 → **TW16 丟棄 S0**；欄位與 3.81 一致（SS2§2） | R2-K **不做**推薦 UI；建議改列「不建議」 |
| 3 | C_MATCH_MAKING(221) | 7 個送出點（data 0–6）全在死窗內，與 3.81 C76 一一對應（SS2§2.3） | 同上 |
| 1 | 78 | `PledgeUI` 內的關注清單（7 列，圖示＋名稱，可捲動）；type 0 的錯誤訊息 3349／3324（SS2§3） | R2-K：補清單畫面 |
| 4.1 | PledgeNoticeUI | 只由 S_MESSAGE（op 153）type 17 開；3.81 不送 type 17（SS2§4） | 建議改列「伺服器不支援」 |
| 3 | C_WHO(174)、C_CHECK_PK(9) | `checkpk` 只送 opcode（3 個送出點）；引擎已照做（SS2§5） | 只差聊天指令入口 |
| 3 | C_USE_SPELL(216) | `+56`＝`SpellList.xml` 的 `delay`、`+60`＝`direction`（18／19）；本機客戶端沒有這個檔，kor25／ly 有（SS2§6） | R2-H：執行期讀檔，沒有檔時維持 `ReuseMs` |
| 1 | 122 | S36＝戒指能力旗標：type 1 傳送控制 → 開目的地窗；type 5 召喚控制 → 施放召喚術時開 `CSummonUI`；type 2 客戶端不用（SS2§7） | 依據欄「SP／MR 沒有顯示」要更正；施法分支歸 R2-H，R2-L 拿掉這一項 |
| 1 | 95 | 第 3 個 C＝數字框初始值（`+0x68`）；範圍 10–50；3.81 不送第 3 個 C（SS2§8） | R2-F：初始值由畫面決定 |
| 1 | 245 | 通用模態框（`sub_8E4670`）加 `PostQuitMessage`；3.81 S227 的首 byte 是 244 → 字串 64「連線中斷。」（SS2§9） | 另開小任務（或併入 R2-Q） |
| 3 | C_EXTENDED(53)… | 只有子碼 16（`Commandtimeleft`），而且 CodePage 950 不送（SS2§10） | 建議改列「不建議」 |
| 4.1 | UserPlayStatsUI | 顯示時送的 TW16 op 10／9＝3.81 C63 `C(9)`；`C_Rank` 的 case 9 回 S250／159（地監時間清單）（SS2§14，T267 轉入） | R2-D：顯示時送 C63 `C(9)` |

### 11.10 T275（#665）回填：前幾輪的未驗證細項

依據 [`followups-3.md`](followups-3.md)（FU3）。這一輪只是查證，**11.3 的狀態與 11.4 的統計都沒有改**。

| 節 | 列 | 查到什麼 | 建議 |
|---|---|---|---|
| 3 | C_USE_SPELL(216) | `SpellList.xml` 以 XML `id` 為鍵＝TW16 技能索引，**3.81 技能編號＝`id`＋1**；沒寫屬性時 delay **10 ms**、direction 0（動作 19）、savelastspell 1（FU3§1） | T269（#643）／R2-H：讀檔時 `id+1`，預設 10／19 |
| 5 | 王冠 `sub_F08880` | `sub_A42C30`＝物件 `+85` 隱身旗標（S_PUT_OBJECT 狀態 bit 0x02、S_INVISIBLE）；同隊同盟看得見的隱身者也不畫王冠（FU3§2） | T272（#649）／R2-I：他人用 `(Flags & 2) == 0 && y > 0` |
| 1 | 122 | type 1 的目的地窗＝書籤窗 `CBookmarkWindowRenewal`（`BookmarkWindowUI`）；TW16 選定後送 `H 地圖 H x H y`，3.81 要 `H 地圖 D 書籤 id`；type 5＝`SummonUI`（`summonList.xml` 的 `action` 當 D 送出，等級不足顯示 743）（FU3§3） | R2-H：開窗流程照原版，送包用 3.81 `HD`／`D(action)` |
| 4.2 | `AgitMapWindow` | 674 鈕送 C125 `D S("select") S(編號)`，3.81 `Npc_AuctionBoard` 回 S39 `agsel` 拍賣資訊頁（FU3§4.1） | #550：接上 674 |
| 4.1 | UserPlayStatsUI／UserObtainUI | 「獲得道具」＝數量高於 S5 基準的物品筆數；S15 記差額、S24 回到基準以下移除、S57 移除（FU3§4.2） | T272（#649）／R2-D |
| 4.2 | `QuitWindow` | ESC 與選單都不開；`NEW_QUIT_WINDOW` 在 TW16 沒有使用（FU3§5.1） | PBW-B 不必補入口 |
| 1 | 108（S250 192／190／84） | 192＝角色選項旗標 `+832`（bit 0 顯示 VIP 標記，回送 op 55／56，3.81 不處理）；190 的 512–514＝魔法娃娃狀態 effect；84 類型 3／4 的宿主＝`RoundScoreUI`（FU3§5.2–5.4） | 維持「不建議」 |
| 1 | 40（賣出） | 確認清單只由 S250 子碼 162 填寫（寵物項圈類，字串 3133）；3.81 不送 162 → 不跳確認（FU3§6.1） | R2-E：拿掉賣出確認 |
| 1 | 136 | 修理列 `+296`＝背包物品數量（通常 1）（FU3§6.2） | R2-E：上限用物品數量 |
| 1 | 133 | `dword_153FCC4` 沒有間接寫入（運算元、索引陣列與資料指標全掃）（FU3§7） | R2-K「不做推薦 UI」確定 |

## 12. 第三輪盤點（T277／#668，2026-11-09）

依目前的 main `77baa88d` 重新核對。第 1–11 節保留原文不改；本節的變更表（12.3）接在 11.3 之後套用，統計由 `rea-scripts/gap_count_r3.py` 從「原表＋11.3＋12.3」重算。

### 12.1 方法

- 對象是 T244 基準 `6d07fe0d` 之後合併的 PR：R2-A–D、F、G、I（#612–#616、#618、#622）、#517（M7-K）、#529（M7-V）、#530（M7-W）、#547（T229 B1）、#560（T238）、#561（T239）、#562（T240）、#467／#470／#471（選單鈕、小地圖、隊伍）、#473（狀態圖示）、#645（T270 自動發現）。查證任務 T266–T268、T275 的結果已在 11.8–11.10，本輪據此改狀態。
- 逐張讀 `docs/reports/issue-<N>.md`，再用 Grep 在 `engine/source` 找**產品端**的呼叫點（排除 `*Tests.cs`、`engine/_integration/`、產品檔內嵌的 `WorldModuleTest` 區塊）。報告閱讀與呼叫點掃描分給 4 個唯讀子代理平行做；`ComposeAmbienceLights`、`ClientSoundScene.Configure`、`CastleCrownFrames`、`SendCastleReply`、`RequestSpellExchangeList`、`ChatChannelCommands.IsCommand` 與全部 `[UiPanel]` 註冊，我另外用 Grep 複核過。
- 判定規則同 11.1：**程式在、但產品沒有呼叫**（只有測試呼叫、沒有訂閱者，或只在 `--native-ui` 時出現）一律算「部分」。另加一條：**原版在 3.81 下沒有可見行為**（原版丟棄封包、送出點全在死窗、開窗封包 3.81 不送）時，引擎不做畫面也算與原版一致——有資料層的改「已有」，沒有的改「缺（伺服器不支援）」。
- 本輪沒有新的反組譯，沒有開 IDB、沒有掛 subst 磁碟；BIN 端地址沿用第 1–5 節。沒有建置、執行，也沒有啟動遊戲、伺服器或 DB。

### 12.2 本輪新發現

1. **第二輪「只有測試呼叫」的 10 項，7 項已接上**：日夜明暗與光照圖（`Program.cs:184`、`:190` → `Ambience.cs:542`、`:577`）、雨雪（`Ambience.cs:544`）、背景音樂／envsound／`^` 環境音（`SoundRules.cs:119` 的 `ProductSound.Arm()` → `ProductSoundBackends.cs:125`、`:134`）、王冠（`ObjectRendering.cs:162`）、城堡回覆（`Castle.cs:237`、`NpcUi.cs:41`）、學魔法清單（`NpcInteraction.cs:153`）、聊天指令（`PlayerChat.cs:247`）、`GhostSkipsSelfAppearance`（`Polymorph.cs:63`、`Recovery.cs:141`）。仍沒接上的見 12.2 第 2 點。
2. **程式已寫但產品沒接上**（本輪的主要發現，下表）。最大宗是**原生面板寫好了卻沒註冊**：`ItemWindowPanel`、`SpellWindowPanel`、`WarehouseWindowPanel`（#562）、`PledgePanel`（#561）、`PersonalShopPanel`（#527）都沒有 `[UiPanel]`，`UiClientBridge` 也沒有包對應的 game state 與指令介面，所以就算開了 `--native-ui` 也看不到。
3. **原生 UI 仍預設關閉**（`NativeUi/UiRoot.cs:112` `UiOptions.Off`；`:116–128` 只認 `--native-ui` 或 `native-ui.json`）。已註冊的 16 個面板預設都看不到。其中**召喚選擇窗沒有 WinForms 備援**：原生 UI 關閉時，S39 `summonlist` 只顯示一行提示（`PetUi.cs:73–74`），玩家選不了召喚怪物。好友三窗的開窗鈕在關閉時只顯示「原生 UI 未啟用」（`BuddyUi.cs:33`），舊的 WinForms 清單仍在。
4. **選單鈕與熱鍵沒有接到原生窗**：8 個選單鈕除了「離開」以外都呼叫 `ShowDevTools`，而且 `page` 參數在 `UiClientBridge.cs:135` 被丟掉（`MenuButtonsPanel.cs:114`）；Map 鈕不開 `Field_Map`（只有 Ctrl+M，`FieldMapPanel.cs:58`）。`ItemWindowPanel.Key` 直接回傳 false（`:166`），原版開背包、魔法窗的熱鍵沒有接。
5. **視窗堆疊仍沒有 4 層上限**：`UiRoot.Add`（`UiRoot.cs:37`）只檢查 Id 重複，`BringToFront`（`:49`）只調順序。11.3 引用的 `UiRoot.cs:122` 已過時。
6. **S250（op 108）**：3.81 會送的 62 個子碼，引擎現在處理 **51 個**（第二輪 37 個；#547 加 0、1、2、3、4、12、15、23、27、30、31、79，#560 加 117，`NativeUi/PledgePanel.cs:86` 加 169）。還缺 11 個：37（#611 PR #663）、83／84（#607）、168（#609 PR #654）、173（#625）、45、86、66、146、190、192；後六個依 PBW§2、FU3§5 不建議做。
7. **依 11.9／11.10 改列**：op 133 與 C_MATCH_MAKING（TW16 丟棄 S0、送出點全在死窗）改「已有」；`PledgeNoticeUI`（只由 3.81 不送的 S_MESSAGE type 17 開）改「缺（伺服器不支援）」；C_EXTENDED(53)（只有子碼 16，繁中不送）改「已有」。
8. 過時的註解：`ServerFeatures.cs:7` 仍寫「拾取節流改跟旗標走需另開任務」，#614 已做（`Ground.cs:85`）。

**程式已寫但產品沒接上（依 Grep，`檔案:行` 為定義處）**

| 項目 | 位置 | 原因 | 處理 |
|---|---|---|---|
| `ItemWindowPanel`／`SpellWindowPanel`（ITEM_WINDOW／SPELL_WINDOW） | `NativeUi/ItemWindowPanel.cs:211`、`NativeUi/SpellWindowPanel.cs:34` | 沒有 `[UiPanel]`；`Ui*GameState` 包裝（`ItemWindowPanel.cs:199`、`SpellWindowPanel.cs:21`）沒有產品使用者；沒有熱鍵（`:166`）；`SetLevel`（`SpellWindowPanel.cs:58`）只有測試呼叫 | R3-A；熱鍵 Q3-1 |
| `WarehouseWindowPanel`（WAREHOUSE_WINDOW） | `NativeUi/WarehouseWindowPanel.cs:33` | 沒有 `[UiPanel]`；`NativeUiCommands` 沒實作 `IUiWarehouseCommands`（`UiClientBridge.cs:134`），S176 仍走 `NpcUi` | R3-A |
| `PledgePanel`（血盟窗） | `NativeUi/PledgePanel.cs:149` | 沒有 `[UiPanel]`；橋接沒包 `UiPledgeGameState`（`:121`）；S250 169／171 只收狀態，窗不會開 | R3-A（等 PR #654） |
| `PersonalShopPanel.Registration` | `NativeUi/PersonalShopPanel.cs:18` | 沒有 `[UiPanel]`；`Confirmed`（`:33`）、`Closed`（`:35`）沒有訂閱者 | R2-N（#620，PR #640 審查中） |
| 選單鈕 `DevToolsPage` | `NativeUi/MenuButtonsPanel.cs:114` → `UiClientBridge.cs:135` | 8 鈕都開 DevTools，page 被丟掉 | R3-A |
| `SummonPanel`（召喚選擇） | `NativeUi/SummonPanel.cs:48`、`PetUi.cs:74` | 只在原生 UI 下出現，沒有 WinForms 備援 | R3-B |
| `ClanWarehouseHistoryReceived` | `ClanWarehouseHistory.cs:52` | 開窗事件沒有訂閱者，沒有視窗 | R3-C（版面 Q3-6） |
| `Cooking.CookBook`／`CloseCookBook` | `Cooking.cs:40`、`:51` | 只進快照；沒有 `COOK_BOOK_WINDOW` 面板 | R3-C |
| `Agit` 開窗事件 `AgitHouseListReceived`／`AgitHouseMapReceived` | `Agit.cs:60`、`:62` | 沒有訂閱者；地圖窗由 #550（PR #664）接；清單窗 `AgitAuctionUI` 缺資源 | #550；清單不分組 |
| `PacketBoxStatusChanged` 與 B2 數值（DkLevel、Karma、Evasion、MapTimes…） | `PacketBoxStatus.cs:59–69` | 沒有訂閱者，只進快照；只有 MagicBuffs 經 `BuffState` 在原生 UI 顯示 | R2-L（#626，PR #653）合併後再查 |
| `StatusEffectIcons`（水中呼吸 effect 11） | `TimedBuffs.cs:44` | 只進快照；原生 UI 由 `NativeUi/State/BuffState.cs:120` 另外對應 | 同上 |
| `AbilityStatus` S36 旗標 | `AbilityStatus.cs:21`、`:23` | 只進快照（S37 SP／MR 只有原生左狀態框讀） | T276（#667） |
| `PacketBoxMessageReceived`、`RankChange`／`LastavardWin`／`FeelGoodCount` | `PacketBoxMessages.cs:88–90` | 只有測試訂閱；B1 27／30／31 只存資料 | Q3-7 |
| `SendClanMatching`／`SendClanContent` | `ClanMatching.cs:218`、`:221` | 沒有任何呼叫者 | **不做**（原版也不送，12.2 第 7 點） |
| `ChatFormat.SecondColor` | `PlayerChat.cs:153` | 沒有呼叫者，用途待驗證 | Q3-9 |
| `ChatState.SetHeights`／`SetHeight` | `NativeUi/State/ChatState.cs:103–104` | 沒有呼叫者，聊天窗高度不能調 | R3-H |
| `NamePlan.Icons`／`HoverIcons`、`NameLayout.MonsterLevelOption` | `ObjectRendering.cs:190`、`:206`、`:223` | 算了位置但 `PaintNames` 不畫（圖源沒查證）；等級差外框沒有選項 UI | Q3-2 |
| `ClientSoundScene.ObserveScene`（登入／選角音樂） | `ClientSoundScene.cs:43` | 只有測試呼叫 | R3-G |
| `BackgroundMusic.RegisterBoss` | `BackgroundMusic.cs:39` | 只有測試呼叫，頭目音樂不會觸發 | R3-G（觸發點 Q3-8） |
| `Ambience.BeginLightEffect`／`EndLightEffect` | `Ambience.cs:165–166` | 只有測試呼叫，光效亮度 +5 沒有封包觸發 | Q3-4 |
| `TimedBuffRules.LiquorFullWave` | `TimedBuffs.cs:23` | 固定回 true，減半分支只有測試會走 | Q3-3 |
| `ServerFeatures` 的 ContinuousAttack、CodePageMismatch、ServerNo、LanguageCode | `ServerFeatures.cs:19–24` | 沒有使用者（只有 FastPickup 有用） | 不分組（C_ATTACK_CONTINUE 不補，第 3 節） |
| `Renderer.PaintAmbience` | `Renderer.cs:10` | 死碼：產品改走 `PrepareAmbience`＋`PaintAmbienceScene`，連測試都不呼叫 | 可順手刪（R3-I 以外的組不碰） |
| `ClientForm.HyperAction`、`PlayerPointerController.PickupAsync` | `OnlineInput.cs:38`、`PlayerPointer.cs:101` | 被新流程取代，只剩測試呼叫或沒有呼叫 | 不分組 |
| 診斷事件 `ShipSent`、`MagicSent`、`MovementSent` | `ShipTravel.cs:89`、`Magic.cs:68`、`OnlineMovement.cs:55` | 只有測試訂閱或沒有訂閱 | 預期如此，不處理 |
### 12.3 狀態變更表

「列」與 11.3 相同（第 1–5 節該列的第一格或唯一前綴）；「原狀態」是套用 11.3 之後的狀態。原狀態＝新狀態的列只更新依據。

| 節 | 列 | 原狀態 | 新狀態 | 補上的任務 | 剩下的差異／依據 |
|---|---|---|---|---|---|
| 1 | 156 | 部分 | 已有 | #612 R2-A | 明暗接上畫面（`Program.cs:184`、`:190`）；夜晚亮度沒有真機看過 |
| 1 | 206 | 部分 | 已有 | #566、#612 | 光照圖每格組合（`Ambience.cs:542`）；±35／±15 篩選是推測 |
| 1 | 176 | 部分 | 已有 | #612 | `WeatherSprite` 執行期讀雨圖 924–927、程序雪（`Ambience.cs:524`、`:544`）；水花與雪的點位、鏡頭補償正負號是推測 |
| 1 | 114 | 部分 | 部分 | #612 | 光圈改用 `Source.lmp` k=5（`Afflictions.cs:152`，讀不到才退回圓形）；盲目 2 讓 102 表物件發光、職業 6／31 沒做 |
| 1 | 229 | 部分 | 部分 | #612 | 地圖 67／420 最低醉度已做；`LiquorFullWave` 固定回 true（`TimedBuffs.cs:23`），產品一律全振幅（Q3-3） |
| 1 | 160 | 部分 | 部分 | #612 | 隊伍／血盟可見已接（`Invisibility.cs:25`），條件 `sub_956D30`、`$431` 是推測（Q3-5） |
| 1 | 91 | 部分 | 部分 | #613 R2-B | 共用層與產品後端已接（`SoundRules.cs:119`）；S22 播放中不隨距離更新音量（SD§2.3 `A091C0`）、`ext` 沒做 → R3-G |
| 1 | 89 | 部分 | 部分 | #615 R2-D | 逾時送 C122 後開 `QUIT_WINDOW`（`Recovery.cs:199`），但只在原生 UI 下；預設只有狀態列訊息 47 |
| 1 | 95 | 部分 | 已有 | #622 R2-F | 頁內數字框與送出鈕接 `SendCastleReply`（`Castle.cs:277`、`NpcUi.cs:41`），拿掉「[城堡]」聊天行；初始值用下限（SS2§8：3.81 不送第 3 個 C）；版面差異歸 op 144 |
| 1 | 12 | 部分 | 已有 | #622 | 同 op 95（`withdraw` 頁，`Castle.cs:234`） |
| 1 | 49 | 部分 | 已有 | #622 | 同 op 95（`deposit` 頁） |
| 1 | 224 | 部分 | 已有 | #622 | web 面板補上數字框（`NpcUi.cs:40`）；C11 中間 byte 送 0 是推測；版面差異歸 op 144 |
| 1 | 175 | 部分 | 部分 | #622 | `xchgspell` 入口已接（`NpcInteraction.cs:153`）、空清單開 `HorunEv3`；學魔法窗仍是暫行 WinForms，不是 `TRADE_WINDOW` 模式 7 → R3-F |
| 1 | 44 | 部分 | 部分 | （無） | 仍是 WinForms 清單；原版＝`TRADE_WINDOW` 模式 7（TWP§1.1、SS2§1.2）→ R3-F |
| 1 | 144 | 部分 | 部分 | #622 | 伺服器頁、查詢頁補到 web 面板（`NpcInteraction.cs:132`）；仍不是 `HYPER_TEXT_WINDOW` |
| 1 | 112 | 部分 | 已有 | #616 R2-G | 喊話 `[name]`、`*` 開頭變色、12 色調色盤（`PlayerChat.cs:166`、`:255`）；沒有真機看過 |
| 1 | 153 | 部分 | 已有 | #616 | type 18／25 自帶色號、`$` 前綴（`PlayerChat.cs:166`） |
| 1 | 61 | 部分 | 部分 | #616 | 只改成調色盤取色（`PlayerHudChat.cs:44`）；UI§3 的前綴與格式沒做 → R3-H |
| 1 | 104 | 部分 | 部分 | #616 | 同 op 61 → R3-H |
| 1 | 252 | 部分 | 已有 | #618 R2-I、#622 | 王冠在產品畫面畫出（`ObjectRendering.cs:162` 取 `CastleCrownFrames`），暫代聊天行已拿掉；他人可見條件照 FU3§2 由 T272（#649）收尾 |
| 1 | 16 | 部分 | 已有 | #618 | 缺 profile 退回 gfx 1120（`ObjectRendering.cs:32`）；欄位 108／111／113 一律 0，待驗證 |
| 1 | 226 | 部分 | 已有 | #618 | 他人逐動作退回（`ObjectRendering.cs:45`） |
| 1 | 186 | 部分 | 部分 | #618 | +252 行已畫；+181、隊伍、主人圖示只算位置不畫（`ObjectRendering.cs:323`，圖源 Q3-2）；+181 與物件種類 3.81 沒有欄位，永遠空值；等級差外框沒有選項 UI；S87 同身份原地覆寫仍待驗證 |
| 1 | 36 | 部分 | 部分 | #618 | +252 行已畫（`ObjectRendering.cs:227`）；自己名稱 X0 仍是近似值；字型屬第 5 節 `sub_8444A0` |
| 1 | 85 | 部分 | 部分 | #517 M7-K | 交易邀請進共用詢問槽（`Trade.cs:59`）；提示仍是 WinForms；血盟、盟戰、復活 322 由 R2-J（PR #647）、R2-K（#625）處理 |
| 1 | 88 | 部分 | 部分 | #517 | 交易窗仍是 WinForms，不是 `TRANSACTION_WINDOW` → R3-E |
| 1 | 145 | 部分 | 部分 | #517 | 同 op 88 |
| 1 | 52 | 缺 | 部分 | #530 M7-W | 收 S39 `summonlist`，`CSummonUI` 原生窗已註冊（`NativeUi/SummonPanel.cs:48`），但只在原生 UI 下出現，關閉時只有一行提示（`PetUi.cs:73`）→ R3-B |
| 1 | 228 | 缺 | 已有 | #530 | S236 → 下一次選目標送 C223，已在產品寵物分頁接上（`PetUi.cs:68`）；選目標游標外觀沒做 |
| 1 | 22 | 部分 | 部分 | （無） | 仍是 WinForms 寵物分頁，不是舊式 `PetControl`（WIN§1.5）→ R3-B |
| 1 | 108 | 部分 | 部分 | #547 T229、#560 T238、#561 | 處理 62 個中的 51 個（12.2 第 6 點）；27／30／31 只存資料（Q3-7） |
| 1 | 25 | 部分 | 部分 | #473 | `effectlist2.xml` 原生狀態圖示已註冊（`NativeUi/BuffIconsPanel.cs:164`），只在原生 UI 下；預設仍走 `StatusIcons.cs`；B2 資料由 R2-L（PR #653） |
| 1 | 17 | 部分 | 部分 | #473 | effect 11 圖示只在原生 UI 下出現（`NativeUi/State/BuffState.cs:120`）；`StatusEffectIcons` 沒有產品使用者 |
| 1 | 127 | 部分 | 部分 | #562 T240 | `WAREHOUSE_WINDOW` 原生窗已寫，但沒註冊、沒有指令介面（`NativeUi/WarehouseWindowPanel.cs:33`），產品仍走 `NpcUi` → R3-A |
| 1 | 110 | 部分 | 部分 | #561 T239 | 169／171 的狀態有收（`NativeUi/PledgePanel.cs:86`），窗沒註冊、不會開 → R3-A |
| 1 | 133 | 部分 | 已有 | T268、T275（查證） | SS2§2、FU3§7：處理函式要 `dword_153FCC4`≠0 才呼叫，而這個全域沒有任何寫入點 → **TW16 丟棄 S0**；引擎只存資料、不顯示（`ClanMatching.cs:166`），與原版一致 |
| 1 | 78 | 部分 | 部分 | T268（查證） | SS2§3：血盟窗內 7 列關注清單、錯誤訊息 3349／3324；引擎只有協定 → 等 R3-A 接上血盟窗後再做 |
| 1 | 122 | 部分 | 部分 | T268、T275（查證） | S36＝戒指能力旗標，type 1 開書籤窗、type 5 開 `SummonUI`（SS2§7、FU3§3）；依據欄「SP／MR 沒有顯示」更正為「S36 旗標沒有開窗」；由 T276（#667）處理 |
| 1 | 152 | 部分 | 部分 | #528 M7-U | 原生公告板按鈕只在原生 UI 下掛上（`BoardUi.cs:98–104`）；背景、清單列、內文仍是 WinForms |
| 1 | 248 | 部分 | 部分 | #528 | 同 op 152 |
| 2 | 107 | 部分 | 部分 | #551、#614 | 拾取節流已改讀 S139 旗標（`Ground.cs:85`）；其他旗標沒有使用者；沒有用 3.81 真機測過 |
| 3 | C_GET(23) | 部分 | 已有 | #614 R2-C | 雙擊地上物品改走走近＋整疊／數量提示（`PlayerPointerUi.cs:20`）；牆阻擋、旗標 `0x10000` 待真機確認 |
| 3 | C_EXIT_GHOST(179) | 部分 | 已有 | #615、#622 | `exitghost` 由 NPC 動作在客戶端處理（`NpcInteraction.cs:132`）；S85／S76 略過自身外觀（`Recovery.cs:141`、`Polymorph.cs:63`） |
| 3 | C_EXCHANGEABLE_SPELL(125) | 部分 | 已有 | #622 | 只在 NPC 外型 834 時送 C245（`NpcInteraction.cs:153`，SS2§1.1） |
| 3 | C_TAX(41) | 部分 | 已有 | #622 | C19／C56／C44 都有畫面入口（`Castle.cs:277`）；治安兩個封包伺服器不支援，維持不做 |
| 3 | C_SAY(16) | 部分 | 已有 | #616 | `[name]`、`*`、`\f`、`$`、C199 都已做（`PlayerChat.cs:255`）；`\b` 只拿掉不畫 |
| 3 | C_ONOFF(184) | 部分 | 部分 | #616 | 聊天列已接 `IsCommand`（`PlayerChat.cs:247`）；`/on`、`/off` 語法是推定；選項窗（`OptionUI`）沒做 |
| 3 | C_DIALOG(51) | 部分 | 部分 | #622 | C11、`exitghost` 已補；剩原版 `HYPER_TEXT_WINDOW` 版面 |
| 3 | C_ASK_XCHG(5) | 部分 | 部分 | #517 | 同第 1 節 op 88；旗標為 0 時的自動回覆沒做（沒有地方把旗標設成 0） |
| 3 | C_SLAVE_CONTROL(45) | 部分 | 部分 | #530 | 召喚選擇的 C125 要靠原生窗送出 → R3-B |
| 3 | C_ADD_BUDDY(138) | 部分 | 部分 | #529 M7-V | 三窗原生面板只在原生 UI 下；信件封鎖只存本機、不送封包（`Buddies.cs:126`） |
| 3 | C_DEAD_RESTART(58) | 部分 | 部分 | #615 | 同第 1 節 op 89 |
| 3 | C_MATCH_MAKING(221) | 部分 | 已有 | T268（查證） | SS2§2.3：7 個送出點全在不會建立的推薦窗內 → TW16 從不送；引擎不送，與原版一致 |
| 3 | C_EXTENDED(53) | 部分 | 已有 | T268（查證） | SS2§10：C53 只有子碼 16（`Commandtimeleft`），CodePage 950 不送；引擎不送 C53，與原版一致（C120 等由各模組送，屬其他列） |
| 4.1 | MainButtonUI | 部分 | 部分 | #467 | 8 鈕已畫（`NativeUi/MenuButtonsPanel.cs:15`），但只在原生 UI 下；除「離開」外都開 DevTools、page 被丟掉（`UiClientBridge.cs:135`）→ R3-A |
| 4.1 | MiniMapUI | 部分 | 部分 | #470 | 原生 `MINI_MAP_WINDOW` 只在原生 UI 下（`NativeUi/MiniMapPanel.cs:11`） |
| 4.1 | MapUI | 部分 | 部分 | #470 | `Field_Map` 只能 Ctrl+M 開（`NativeUi/FieldMapPanel.cs:58`），Map 鈕沒接；世界／村莊地圖沒有 |
| 4.1 | PartyUI | 部分 | 部分 | #471 | `PARTY_INTERFACE`／RT 只在原生 UI 下（`NativeUi/PartyPanel.cs:11`）；Alt+F3 輪替 WinForms 是否收得到未驗證 |
| 4.1 | PledgeUI | 部分 | 部分 | #561 | 原生血盟窗已寫，沒有 `[UiPanel]`、橋接沒包 state（`NativeUi/PledgePanel.cs:149`）→ R3-A |
| 4.1 | PledgeNoticeUI | 部分 | 缺 | T268（查證） | SS2§4：只由 S_MESSAGE（op 153）type 17 開，3.81 不送 type 17 → 伺服器不支援，不建議做；S167 協定保留 |
| 4.1 | Inventory | 部分 | 部分 | #562 | `ITEM_WINDOW` 原生窗已寫，沒註冊、沒有熱鍵（`NativeUi/ItemWindowPanel.cs:166`、`:211`）→ R3-A、Q3-1 |
| 4.1 | Warehouse | 部分 | 部分 | #562 | 同第 1 節 op 127 |
| 4.1 | SpellUI | 部分 | 部分 | #562 | `SPELL_WINDOW` 原生窗已寫，沒註冊（`NativeUi/SpellWindowPanel.cs:34`）→ R3-A |
| 4.1 | ExchangeUI | 部分 | 部分 | （無） | 同第 1 節 op 88 → R3-E |
| 4.1 | FriendUI | 部分 | 部分 | #529 | 三窗已註冊（`NativeUi/FriendPanel.cs:43`），只在原生 UI 下；關閉時開窗鈕只顯示「原生 UI 未啟用」（`BuddyUi.cs:33`） |
| 4.1 | SummonUI | 缺 | 部分 | #530 | 同第 1 節 op 52 → R3-B |
| 4.1 | UserPlayStatsUI | 部分 | 部分 | #615 | 原生面板已註冊（`NativeUi/UserPlayStatsPanel.cs:23`），只在原生 UI 下（`Recovery.cs:320`）；統計值仍是「—」，由 T272（#649）處理 |
| 4.2 | `QuitWindow` | 缺 | 部分 | #615 | 原生 `QUIT_WINDOW` 已註冊（`NativeUi/QuitWindowPanel.cs:19`），只在原生 UI 下；FU3§5.1：ESC／選單不開，不必補入口 |
| 4.2 | `InventoryWindow` | 部分 | 部分 | #562 | 同第 4.1 節 Inventory |
| 4.2 | `SpellWindow` | 部分 | 部分 | #562 | 同第 4.1 節 SpellUI |
| 4.2 | `ExcludeWindow` | 部分 | 部分 | #529 | 同第 4.1 節 FriendUI |
| 4.2 | `PetControl` | 部分 | 部分 | （無） | 同第 1 節 op 22 → R3-B |
| 4.2 | `MapSystemWindow` | 部分 | 部分 | #470 | 只有 Field／Dungeon 外觀（`NativeUi/MiniMapPanel.cs:31`）；World／Village 沒有 |
| 4.2 | `BoardListWindow` | 部分 | 部分 | #528 | 同第 1 節 op 152 |
| 4.2 | `CCookWindow` | 部分 | 部分 | #559 | 資料已有（`Cooking.cs:40`），沒有 `COOK_BOOK_WINDOW` 面板 → R3-C |
| 4.2 | `WareHistoryWindow` | 部分 | 部分 | #560 T238 | S250／117 只有資料，開窗事件沒有訂閱者（`ClanWarehouseHistory.cs:52`）→ R3-C |
| 5 | （日夜曲線 | 部分 | 已有 | #612 | 同第 1 節 op 156、206 |
| 5 | （天氣粒子 | 部分 | 已有 | #612 | 同第 1 節 op 176 |
| 5 | （中毒、麻痺 | 部分 | 部分 | #612 | 中毒、麻痺已有（11.3）；盲目、酒醉、隱身的剩餘差異見第 1 節 op 114、229、160 |
| 5 | （變身缺圖退回 | 部分 | 已有 | #618 | 同第 1 節 op 16、226 |
| 5 | `sub_F0B570` | 部分 | 已有 | #566、#612 | 光源表與 `Source.lmp` 已接上畫面（`Ambience.cs:542`） |
| 5 | `sub_A06210` | 部分 | 已有 | #613 | `MapAmbientSound.Backend` 由產品設定（`ProductSoundBackends.cs:125`），已乘 EnvSound 類別音量 |
| 5 | `sub_F112F0` | 部分 | 已有 | #614 | NPC、攻擊、門都套死亡阻擋，幽靈中擋攻擊（`PlayerPointer.cs:115`） |
| 5 | music.def | 部分 | 部分 | #613 | 進入世界後 `Configure`（`ProductSoundBackends.cs:134`），背景音樂與 envsound 會出聲；登入／選角音樂（`ClientSoundScene.ObserveScene`）、頭目曲（`RegisterBoss`）沒有產品呼叫；MIDI、聲像沒做 → R3-G |
| 5 | `sub_A57C60` | 部分 | 部分 | #463 | 仍沒有 4 層上限（`NativeUi/UiRoot.cs:37`、`:49`；11.3 的 `:122` 已過時）→ R3-I |
| 5 | `sub_8FB4D0` | 部分 | 部分 | #473 | 同第 1 節 op 25 |
| 5 | vtable 表 `0x1165C80` | 部分 | 部分 | #616 | 各類型上色已做；第二色 `SecondColor` 沒有呼叫者（`PlayerChat.cs:153`，Q3-9） |

未列出的列狀態不變。仍為「缺」、且有待合併 PR 的列：`AgitWindow`／`AgitMapWindow`（#550，PR #664）。
### 12.4 統計（第三輪）

第 1–5 節原表（58／164／67）先套 11.3（含 11.8）再套 12.3，共 290 列（`py -3 -I docs/reference/rea-scripts/gap_count_r3.py docs/reference/gap-matrix.md`；第 11 節套用 88 列、第 12 節套用 87 列，沒有錯誤）：

| 節 | 已有 | 部分 | 缺 | 其中需先查證 | 其中伺服器不支援／不建議 | 其中可直接補 |
|---|---:|---:|---:|---:|---:|---:|
| 1. 收包：第一組 dispatch | 54 | 50 | 10 | 0 | 9 | 1 |
| 2. 收包：第二組 | 6 | 3 | 4 | 0 | 4 | 0 |
| 3. 送包：`sub_951F80` 名稱表 | 16 | 31 | 5 | 0 | 5 | 0 |
| 4. UI 視窗 | 0 | 48 | 25 | 0 | 20 | 5 |
| 5. 物件、技能、道具、地圖事件與繪製的處理函式 | 18 | 19 | 1 | 0 | 1 | 0 |
| **合計** | **94** | **151** | **45** | 0 | 39 | 6 |

和第二輪（67／175／48）比較：「部分→已有」26 列、「缺→已有」1 列（op 228）、「缺→部分」3 列（op 52、`SummonUI`、`QuitWindow`）、「部分→缺（伺服器不支援）」1 列（`PledgeNoticeUI`）。已有 +27、部分 −24、缺 −3。

- 第 4 節仍然 **0 列「已有」**：所有原生視窗都只在 `--native-ui` 下出現（12.2 第 3 點），依 11.1 的規則一律算「部分」。原生 UI 改成預設開啟（需使用者決定）後，第 4 節會一次大量變動。
- 仍為「缺」且「可直接補」的 6 列：op 132 S_SHOW_MAP（PG 任務 D，低）、`BookmarkMemoryWindowUI`（併書籤後續）、`StatInfoUI`（隨重置窗，本機缺 23 個 surf）、`UserObtainUI`（低，計入規則見 FU3§4.2）、`AgitAuctionUI`（XML 缺資源）、`AgitWindow`／`AgitMapWindow`（#550，PR #664 審查中）。本批不分組。
- 全表「需先查證」仍是 0 列；12.6 的 Q3 項都屬「部分」列裡的細項。

### 12.5 第三批 code 任務分組（9 組）

**原則**（沿用第 7、11.5 節）
- 同一個既有檔案只分給一組；新檔用該組自己的名稱。
- 新封包用 M7-A 擴充點（`[WorldReceive]`、`[WorldReset]`、`[WorldSnapshot]`、`[WorldModuleTest]`），不改 `WorldSession.cs`。新面板用 #645 的 `[UiPanel(順序)]` 在**自己的檔案**註冊，**不再改 `UiPanelRegistry.cs`**；測試用 `[CheckGroup]`／`[WorldModuleTest]` 自動發現，不改 `Program.cs`、`DemoChecks.csproj`。PR 不改 `rebuild-lock.json`（#646）。
- `NativeUi/UiClientBridge.cs`（橋接 game state 與指令介面）是共用熱點，本批**只給 R3-A**。其他組的原生面板若要取 game state，用面板自己的 `Attach`／host 介面（`SummonPanel` 的 `ISummonUiHost` 做法），由該組自己的 WinForms 檔掛上。
- 下列待合併的 PR／已開任務用到的檔案，本批不碰，列為依賴：PR #664（`Agit.cs`、`NativeUi/AgitMapPanel.cs`）、#663（`Pets.cs`、`PetEquipment.cs`）、#662（`DisplayNotices.cs`、`NativeUi/RedNoticePanel.cs`、`NativeUi/UiClientBridge.cs`）、#654（`NativeUi/PledgePanel.cs`）、#653（`AbilityStatus.cs`、`PacketBoxStatus.cs`、`StatusIcons.cs`）、#647（`YesNoPrompt.cs`、`Party*.cs`、`Duel*.cs`、`Marriage.cs`、`Resurrection.cs`、`KeyboardNavigation.cs`）、#640（`PrivateShopUi.cs`、`PersonalShopIntegration.cs`）、#639（`LocationShare.cs`、`NativeUi/LocationListPanel.cs`）、#638（`ClientLogin.cs`、`LoginConnection.cs`）、#637（`AttackSkillPackets.cs`、`MagicRendering.cs`、`ProjectileRendering.cs`、`SubEffects.cs`）；未開 PR 的 #607（`Renderer.cs` 掛點、`PacketBoxScreen.cs`）、#623 R2-E（`NpcServices.cs`、`NativeUi/TradeWindowPanel.cs`、`WeaponRepairUi.cs`、`NpcInteraction.cs` 的 `ShopRows`）、#625 R2-K（`Clan*.cs`、`ClanMatching.cs`）、#643（`ActionCadence.cs`）、#649（`ObjectRendering.cs`、`Castle.cs`、`Recovery.cs`、`NativeUi/UserPlayStatsPanel.cs`、`TextArchive.cs`）、#651（`KickNotice.cs`）、#667（`NativeUi/SummonPanel.cs`、`NativeUi/BookmarkPanel.cs`）。
- 驗收照 #503 暫行政策：編譯＋既有單元測試綠，每組另加自己的離線測試（新檔）。標 Q3-n 的部分只做協定與資料，不做畫面，報告寫明。

| 組 | 目標 | 可改的檔案 | 依賴 | 引用 |
|---|---|---|---|---|
| R3-A 原生舊式窗接上產品 | `ItemWindowPanel`、`SpellWindowPanel`、`WarehouseWindowPanel`、`PledgePanel` 加 `[UiPanel]`；橋接包上 `UiItemWindowGameState`／`UiSpellWindowGameState`／`UiPledgeGameState`，實作 `IUiWarehouseCommands`（確定鈕送 C38）；原生 UI 開啟時 S176 改開 `WAREHOUSE_WINDOW`、S250 171 開血盟窗（原 `NpcUi` 倉庫清單保留給關閉時）；選單鈕 `DevToolsPage` 的 page 不再丟掉，Map 鈕開 `Field_Map`，其餘鈕對應到已註冊的原生窗，對不到的維持 DevTools 並標待驗證；op 78 關注清單（SS2§3）放進血盟窗。熱鍵等 Q3-1 | `NativeUi/UiClientBridge.cs`、`NativeUi/ItemWindowPanel.cs`、`NativeUi/SpellWindowPanel.cs`、`NativeUi/WarehouseWindowPanel.cs`、`NativeUi/PledgePanel.cs`、`NativeUi/MenuButtonsPanel.cs`、`NativeUi/FieldMapPanel.cs`、`NpcUi.cs`（只限倉庫清單分流）、新檔 `NativeWindowWiringTests.cs` | #662（`UiClientBridge.cs`）、#654（`PledgePanel.cs`）合併；熱鍵 Q3-1 | WIN2§2、§3；SS2§3；UI§2 V5；第 1 節 op 127、110、78；第 4.1 節 Inventory、Warehouse、SpellUI、PledgeUI、MainButtonUI、MapUI；第 4.2 節 `InventoryWindow`、`SpellWindow` |
| R3-B 寵物：召喚備援與 `PetControl` | 原生 UI 關閉時，S39 `summonlist` 改用暫行 WinForms 清單選怪（送同一個 C125 動作），不再只有提示；`PetControl` 舊式寵物指令窗做成原生面板（按鈕 surf 1159／886、1156／885），WinForms 寵物分頁保留給關閉時 | `PetUi.cs`、新檔 `NativeUi/PetControlPanel.cs`、`SummonFallbackUi.cs`、`PetControlTests.cs` | #663（`Pets.cs`）合併；#667（`SummonPanel.cs`）不碰 | WIN§1.3、§1.5；第 1 節 op 52、22；第 3 節 C_SLAVE_CONTROL；第 4.1 節 `SummonUI`；第 4.2 節 `PetControl` |
| R3-C 料理窗與血盟倉庫紀錄窗 | `COOK_BOOK_WINDOW` 原生面板（底 2254、捲軸 2039、等級圖 2545–2555 (116, 257)、量表 2256 (35, 251)），接 `Cooking` 的 S250 52；訂閱 `ClanWarehouseHistoryReceived`，先用 WinForms 清單顯示 S250／117 的紀錄，原生版面等 Q3-6 | `Cooking.cs`、`ClanWarehouseHistory.cs`、新檔 `NativeUi/CookBookPanel.cs`、`ClanWarehouseHistoryUi.cs`、`CookAndHistoryTests.cs` | 無（版面 Q3-6 只擋倉庫紀錄的原生窗） | WIN2§7（W2-F、W2-G）；第 1 節 op 108；第 4.2 節 `CCookWindow`、`WareHistoryWindow` |
| R3-D 聲音收尾 | S22 播放中每次更新依距離重算音量（SD§2.3 `A091C0`：vol＝100−14·(nx²+ny²)/20，夾 0），補 `ext` 參數；登入／選角畫面呼叫 `ClientSoundScene.ObserveScene` 放登入音樂；頭目曲 `RegisterBoss` 的觸發點等 Q3-8 | `SoundPackets.cs`、`ClientSoundScene.cs`、`BackgroundMusic.cs`、`CharacterManagementUi.cs`（只加選角畫面的 `ObserveScene`）、新檔 `SoundFollowupTests.cs` | 登入畫面那一處等 #638（`ClientLogin.cs`）合併；頭目曲 Q3-8 | SD§2.3、SD#4–#14；第 1 節 op 91；第 5 節背景音樂列 |
| R3-E 交易窗 `TRANSACTION_WINDOW` | 玩家交易改用 `uiextra86.xml` 的 `TRANSACTION_WINDOW` 原生面板（雙方物品格、金幣、確定／取消），接 S52／S35／S112，送 C37／C71／C86；WinForms `TradeUi` 保留給原生 UI 關閉時；旗標 0 的自動回覆照 WIN§2.1（自動回覆值待驗證的部分維持現狀） | `Trade.cs`、`TradeUi.cs`、新檔 `NativeUi/TransactionPanel.cs`、`TransactionPanelTests.cs` | 無（`YesNoPrompt.cs` 屬 PR #647，不碰） | WIN§1.5、§2.1；第 1 節 op 88、145、167；第 3 節 C_ASK_XCHG；第 4.1 節 `ExchangeUI` |
| R3-F 學魔法窗改用 `TRADE_WINDOW` 模式 7 | op 44（S41）與 op 175（S13）的清單改進 `TradeWindowPanel` 模式 7：學魔法顯示單價＝(`id>>3`＋1)²×基價、送 C39；以物品學魔法不顯示單價、送 C191；空清單依 NPC 外型開 `GerengEv3`／`Sedia1`／`Siriss1`／`HorunEv3`；拿掉暫行 WinForms 窗 | `SpellExchange.cs`、`SpellExchangeUi.cs`、新檔 `SpellTradeSource.cs`、`SpellTradeTests.cs`（`TradeWindowPanel` 只用 R2-E 留下的資料來源介面，不改檔） | #623 R2-E 合併（`TradeWindowPanel.cs`、`NpcServices.cs`） | TWP§1.1、§1.6；SS2§1.2；第 1 節 op 44、175；第 3 節 C_BUYABLE_SPELL |
| R3-G 聊天收尾二 | op 61（代碼聊天）、op 104（密語）的前綴與格式照 UI§3；原生聊天窗接 `ChatState.SetHeights`（高度切換）；第二色 `SecondColor` 等 Q3-9 | `PlayerHudChat.cs`、`PlayerChat.cs`、`NativeUi/ChatPanel.cs`、`NativeUi/State/ChatState.cs`、新檔 `ChatFollowupTests.cs` | 無 | UI§3；第 1 節 op 61、104；第 5 節 vtable `0x1165C80`；第 4.1 節 `MainChatUI` |
| R3-H 盲目光源與酒醉、隱身細項 | 盲目 2 讓 102 表物件發光、職業 6／31 照 AM§4；`LiquorFullWave` 照 Q3-3、隱身例外照 Q3-5、光效 +5 照 Q3-4 查到後接上（沒查到前只做盲目）；順手刪死碼 `Renderer.PaintAmbience` | `Afflictions.cs`、`Invisibility.cs`、`TimedBuffs.cs`（只限酒醉）、`Ambience.cs`（只限 `BeginLightEffect` 的觸發）、新檔 `AfflictionFollowupTests.cs` | 刪 `Renderer.PaintAmbience` 等 #607（`Renderer.cs`）合併；Q3-3／Q3-4／Q3-5 | AM§1、§4；第 1 節 op 114、229、160 |
| R3-I 視窗堆疊 4 層上限 | `UiRoot` 的 Window 層照 `sub_A57C60`：最多 4 層，第 5 個開啟時依原版規則關掉最舊的；`BringToFront` 維持順序；加測試 | `NativeUi/UiRoot.cs`、`NativeUi/UiInput.cs`、新檔 `WindowStackTests.cs` | 無 | WIN§2.1；第 5 節 `sub_A57C60` |

**建議順序**：先派 R3-B、R3-C、R3-E、R3-G、R3-I（不等別人）；R3-D 先做 S22 與選角音樂，#638 合併後補登入畫面；#662、#654 合併後派 R3-A；#623 合併後派 R3-F；R3-H 先做盲目，Q3 查完再補其他。

**需要使用者決定（不開 code 任務）**：原生 UI 仍預設關閉，第 4 節 48 列「部分」大多卡在這裡，召喚選擇窗也因此在預設狀態用不了（R3-B 先做備援）。建議等 #475（T201）真機 smoke 通過後，請使用者決定是否改成預設開啟。

### 12.6 需先查證（還沒有查證依據的細項）

| 代號 | 要查的內容 | 起點 | 影響的組 |
|---|---|---|---|
| Q3-1 開窗熱鍵 | 背包、魔法、倉庫（`RenewalInventoryUI`／`SpellUI`）與選單鈕 8 鈕各開哪個窗、用哪個鍵；`ItemWindowPanel` 測試目前寫「no hotkeys（待驗證）」 | WIN2§2「熱鍵只開本窗」的呼叫者；`sub_B56A40` 的呼叫者；`uistatus86` 選單鈕事件 | R3-A |
| Q3-2 名稱旁圖示 | +181 圖示、隊伍與主人圖示的 surf 與位置；怪物等級差外框的選項來源 | AN§2；`NamePlan.Icons`（`ObjectRendering.cs:190`） | 下一輪（`ObjectRendering.cs` 目前由 #649 使用） |
| Q3-3 酒醉減半條件 | `sub_81B0E0` 何時為假（位移減半） | AM§4(c)；`sub_81B0E0` | R3-H |
| Q3-4 光效亮度 +5 | 哪個封包或狀態觸發 `BeginLightEffect`／`EndLightEffect` | AM§1；S_CHANGE_LIGHT（op 206）、S_EFFECT（op 86） | R3-H |
| Q3-5 隱身可見例外 | `sub_956D30`、`$431` 的條件是否就是同隊／同盟 | AM§4(d)；FU3§2 | R3-H |
| Q3-6 血盟倉庫紀錄窗版面 | `sub_A559D0` 視窗的 XML 或寫死座標、surf | WIN2§7；`sub_A559D0` | R3-C |
| Q3-7 S250 27／30／31 與 RankChange 顯示 | 原版收到後顯示在哪裡（聊天、紅字、視窗） | PG§2.3 B1；`sub_9357E0` 各 case | 下一輪（`PacketBoxMessages.cs`） |
| Q3-8 頭目音樂觸發 | 頭目曲（SD 的 226 曲）由哪個封包或物件條件觸發 | SD 背景音樂節；`BackgroundMusic.RegisterBoss` 的註解 | R3-D |
| Q3-9 聊天第二色 | `#BDBDBD` 第二色用在哪一類訊息的哪一段 | UI§3；vtable `0x1165C80` | R3-G |

建議開 **1 張 resource 任務**合併查 Q3-1、Q3-3–Q3-6、Q3-8、Q3-9（都是 IDA 靜態查證，照 #417，subst 磁碟先檢查）；Q3-2、Q3-7 量小、對應檔案目前被別的任務占用，可以排在下一輪。

### 12.7 限制

- 只讀報告與原始碼，沒有建置或執行；「產品沒有呼叫」是用 Grep 判斷，以反射、委派或事件間接呼叫的可能會漏（`[UiPanel]` 自動發現已逐一列出）。
- 審查中的 PR（#637–#640、#647、#653–#656、#662–#664）沒有比對；合併後部分列會再變動，尤其是 R2-H、R2-J、R2-L、R2-M、R2-N、R2-Q。
- 各任務都沒有真機驗收；「已有」只表示程式和已查證的表現沒有已知差異。
- 本節的 `檔案:行` 以 main `77baa88d` 為準。