# 主伺服器封包對照表

本表是 Issue #132 的唯讀原始碼查核，日期 2026-11-06。後續封包／狀態任務先查本表與 [規則表](server-rules.md)，再核對相應來源 SHA-256、方法及分支；不要以舊客戶端 opcode 猜本伺服器格式。差異與後續工作見 [報告](../reports/issue-132.md)。

來源根為 `1-伺服器/src/com/lineage/`（相對於 `D:/lineage開發/`）。下文每個類別都有完整相對路徑、SHA-256、方法簽章及 Java 行號；行號不是 File Offset／RVA／VA。這是來源快照，不代表已核對正在執行的 JAR，也不宣稱主伺服器來源與引擎 `LoginProfile.JarSha256=d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf` 相同。未讀配置、DB、日誌或封存檔；配置與資料表決定的實際值保留為條件。

## 查表方式與欄位記號

先在「C opcode」找請求與 `start(byte[], ClientExecutor)`，在「S opcode」找回包序列化類別，再查同名欄位索引。所有 opcode 使用**十進位／十六進位並列**，`S87` 表示十進位 87，不是 0x87。多個名稱可共用數值；S 沒有伺服器接收處理器，其對應方法是建構子／`buildPacket` 等序列化方法，引擎接收處理器另列於報告。

| 記號 | 型別、長度與順序 |
|---|---|
| C、P、L | 1 byte；C 讀取為 0..255；本來源 `writeL(long)` **也只寫 1 byte** |
| H | 2 bytes，小端；讀為 0..65535；有號語義（lawful 等）由使用端解讀 |
| CH | 3 bytes，小端，只出現在 C 讀取 |
| D、Exp | 4 bytes，小端；Java `readD()` 回傳有號 int；`writeExp(long)` 只寫低 32 位 |
| F | 8 bytes，小端 IEEE 754 double |
| S | `Config.CLIENT_LANGUAGE_CODE` 編碼的可變長 bytes 加 00；長度 = 編碼後 bytes + 1，不是字元數。S 端另套用文字過濾 |
| Byte（C 讀取） | 剩餘所有 bytes，含客戶端尾端資料；不是長度前綴 |
| Byte（S 寫入） | 原始 byte[] 加一個 00；長度 = 陣列長度 + 1 |
| Byte1 | 原始 byte[]，不加 00；長度 = 陣列長度 |
| damageWordU16 | H；RO 模式寫 dmg，公版寫 10；個別 NPC／法術類別另有「零傷害寫 0」分支，見索引 |

封包明文從 opcode 起算 offset 0。C 處理器 `read(packet)` 先跳過 opcode，所以 C 欄位表**不含 opcode**；S 序列化表**包含 opcode**。`NettyChannelInitializer.initChannel` 的 frame 長度為小端 H，包含本身 2 bytes，最大 10240；`L1CipherEncoder.encode` 寫 H(加密後 payload 長度 + 2)。`ServerBasePacket.getBytes()` 在語義欄位後補 0 至 8-byte 倍數：若欄位總長 L，明文長度 `L + ((8 - (L mod 8)) mod 8)`，frame 再加 2。第一個初始化包由 `OpcodesClient._firstPacket` 提供：18-byte frame，S150 + D(seed) + 11-byte 固定尾段。加密／分幀不屬於欄位表的欄位。

**完整索引的讀法：**每列是一個方法內，依來源行序排列的讀／寫欄位簽章。`@行號` 可回查；`〔條件〕` 是來源分支／迴圈，只有選中的分支才出現在 wire，迴圈依實際次數展開。`else` 對應該來源位置的前一個 if，不能把兩支串接；switch case 的 fall-through 要一起核對。方法間的「委派」要展開到被呼叫方法；建構子 overload 是替代入口，不是前後串接。這是可追溯的欄位索引，並非自動產生的可執行解碼器；格式相依於物品模板、列表或選項時沒有單一固定總長。

## 常用封包的已核對完整布局

以下 L 是含 opcode、不含 padding／frame 的明文語義長度。`n(S)` 表示字串含終止零的 byte 長度。

| 方向／opcode | 欄位依 wire 順序 | L／來源 |
|---|---|---|
| C119 / 0x77 登入 | C119, S(account), S(password) | 1+n(account)+n(password)；C_AuthLogin.start |
| C210 / 0xD2 | C210, C(type), S(loginName), S(password) | 2+n+n；C_AutoLogin.start；type=11 由 SessionPacketPolicy 忽略控制分支攔下，非登入成功 |
| C14、C16、C95、C52、C177 | 僅 opcode；處理器不讀參數 | 1；分別版本、公告確認、keepalive、傳送 ACK、重生請求；C177 的伺服器方法本身未以死亡作入口檢查 |
| C137 / 0x89 選角 | C137, S(charName) | 1+n；C_LoginToServer.start |
| C26 / 0x1A 設定 | C26, C(type), C(button) | 3；C_LoginToServerOK.start，聊天顯示設定，不能誤認選角成功 ACK |
| C29 / 0x1D 移動 | C29, H(baseX), H(baseY), C(heading) | 6；C_MoveChar.start；base 是來源格，heading 0..7 |
| C225 / 0xE1 面向 | C225, C(heading) | 2；C_ChangeHeading.start |
| C229 / 0xE5、C123 / 0x7B 攻擊 | C(op), D(targetId), H(targetX), H(targetY) | 9；C_Attack.start 可另讀一個未用 C；C_AttackBow.start |
| C25 / 0x19、C112 / 0x70 丟／撿 | C(op), H(x), H(y), D(objectId), D(count) | 13；C_DropItem.start／C_PickUpItem.start；objectId 非模板 ID |
| C164 / 0xA4 使用／裝備 | C164, D(ownedItemObjectId), use_type 尾段 | 5+尾段；C_ItemUSe.useItemRequest／ClientPacketStructure.validateItemArguments |
| C136 / 0x88、C40 / 0x28 聊天 | C(op), C(type), S(text) | 2+n；C_Chat.start／C_ChatGlobal.start |
| C184 / 0xB8 密語 | C184, S(receiver), S(text) | 1+n+n；C_ChatWhisper.start |
| C219 / 0xDB 入口 | C219, H(x), H(y) | 5；C_EnterPortal.start；不是直接指定新 map |
| C231 / 0xE7 下船 | C231, H(destinationMap), H(x), H(y) | 7；C_Ship.start，只有目前位於已知船圖才讀入 |
| C34／C125 NPC | C34,D(npcId)；C125,D(npcId),S(action)，部分 action 後讀 S(sub) | 5／5+n(+n)；C_NPCTalk.start／C_NPCAction.start |
| C161 / 0xA1 列表回覆 | C161,D(objectId),C(resultType),C(size),C(未用)，接 size 筆記錄 | 8+記錄；C_Result.start；一般 D(id),D(count)，假玩家店 D(id),CH(count)，玩家買店 D(id),CH(count),C(order)，須依 type／對象選支 |
| C39／C191 學習確認 | C(op),H(count),count×D(skillIndex) | 3+4×count；C_SkillBuyOK.start／C_SkillBuyItemOK.start；索引與 skillId 的 +1 換算查處理器 |
| S10 / 0x0A 移動 | C10,D(objectId),H(sourceX),H(sourceY),C(heading),C(128) | 11 → padding 16；S_MoveCharPacket 建構子；不是終點座標 |
| S30 / 0x1E 近戰 | C30,C(action),D(attacker),D(target),H(damageWord),C(heading),H(0),H(0),C(type) | 18 → 24；S_AttackPacketPc／Npc；target 可因受擊抑制而為 0 |
| S30 / 0x1E 箭矢 | C30,C(action),D(attacker),D(target),H(damageWord),C(heading),D(338),H(gfx),C(127),H(sx),H(sy),H(tx),H(ty),D(0),C(0) | 33 → 40；S_UseArrowSkill；action 不一定是 1 |
| S30 / 0x1E 攻擊法術 | 同箭矢寬度；D(338) 改為遞增 sequence，C(127) 改為 0；施法動作關閉時 attacker=0 | 33 → 40；S_UseAttackSkill.buildPacket；action 有 18／19／1 等分支 |
| S42 / 0x2A 範圍法術 | C42,C(action),D(caster),H(x),H(y),C(heading),D(seq),H(gfx),C(type),H(0),H(n)，每筆 D(target),H(calc?32:0) | 正常 22+6n；includeDamage 且 RO 且 resolvedId≠0 的筆另加 D(0xD14A6E3F),D(damage)；S_RangeSkill |
| S55 / 0x37 附著特效 | C55,D(objectId),H(gfx) | 7 → 8；S_SkillSound；另有參數化效果 overload，見索引 |
| S106 / 0x6A 座標特效 | C106,H(x),H(y),H(gfx) | 7 → 8；S_EffectLocation |
| S22 / 0x16 聲音 | C22,C(repeat),H(soundId) | 4 → 8；S_Sound.buildPacket |
| S158 / 0x9E 動作 | C158,D(objectId),C(actionId) | 6 → 8；S_DoActionGFX；死亡、受擊、拾取皆依 action |
| S120 / 0x78 移除 | C120,D(objectId) | 5 → 8；S_RemoveObject |
| S122 / 0x7A 面向 | C122,D(objectId),C(heading) | 6 → 8；S_ChangeHeading |
| S149 / 0x95 傳送初始化 | C149,H(mapId),D(initializationId) | 7 → 8；S_Teleport2；舊 S_Teleport 為 C149,C(235),C(167),D(seq) |
| S206 / 0xCE 地圖 | C206,H(mapId),C(underwater) | 4 → 8；S_MapID／S_PcPositionMapReset |
| S202 / 0xCA 鎖定 | C202,C(mode) | 2 → 8；S_Paralysis；mode 由 type 與 flag 轉換，詳索引 |
| S225 / 0xE1、S33 / 0x21 | C(op),current,max；RO 用 D,D，公版用 H,H | 9 → 16／5 → 8；S_HPUpdate／S_MPUpdate.buildPacket |
| S113 / 0x71 EXP | C113,C(level),Exp(total低32位),C(1) | 7 → 8；S_Exp；不能據此還原完整 long |
| S81 / 0x51 一般聊天 | C81,C(type),D(speaker),S(formattedText) | 6+n；S_Chat |
| S73 / 0x49 密語 | C73,S(name),S(text) | 1+n+n；S_ChatWhisperFrom |

### C6 技能的條件尾段

固定前綴 `C6,C(row),C(column)`，skillId=`row×8+column+1`。`ClientPacketStructure.validateSkill` 對明文長度 ≤4 接受 self/no-target 形式；長於 4 則依下表讀。不要任意把短包補成 8 bytes 後仍宣稱「無目標」：本來源依解密後長度選支，補零會進入其他讀取路徑。

| skillId | 前綴後的欄位／長度 |
|---|---|
| 116、118 | S(charName)／可變 |
| 113 | D(targetId),H(x),H(y),S(text)／8+n |
| 5、69 | H(mapId),D(bookmarkId)／6 |
| 58、63 | H(x),H(y)／4 |
| 4、6、7、10、15、16、17、18、22、25、28、30、34、38、45、46、50、65、74、77、108、132、184、187、192、194、202、203、207、208、213 | D(targetId),H(x),H(y)／8 |
| 其他 | D(targetId)／4；實際語義查 C_UseSkill.start（例如部分技能將值用作選擇 ID） |

### C164 物品 use_type 的條件尾段

來源 `ClientPacketStructure.validateItemArguments(byte[], int)`；物品必須先由角色背包解析，不能讓用戶端任意指定 use_type。

| use_type | D(itemObjectId) 後欄位／長度 |
|---|---|
| -8 | C,C／2 |
| 5、17、39 | D,H,H／8 |
| 6、29 | H,D／6 |
| 7、8、14、26、27、30、46、55 | D／4 |
| 16 | S／可變 |
| 28 | C／1 |
| 12、31、33、35 | H,S，加可選二進位信件本文／可變 |
| 42 | H,H／4 |
| 其餘 | preflight 不要求尾段；仍須查 C_ItemUSe.useItemRequest 及物品 executor |

### 容易混淆的共用 opcode

- C63：`RESTARTMENU` 與 `RANK` 同值，實際分派 `C_Rank.start`，不可把它當 C177 重生。
- S24：ItemStatus／ItemAmount；S64：角色清單確認／點數重置；S72：血盟資訊／NEWMASTER；S87：PC、NPC、地面物品等；S206：map／underwater；S250：大量 subcode。不可只以 opcode 決定唯一類別。
- S87 地面物品的精確欄位在 `S_NPCPack_Item`，與 `S_OwnCharPack`／`S_NPCPack` 不完全相同；objectId、gfx、數量與名稱不等於模板 ID。
- 未列入 OpcodesServer 的 **242 / 0xF2** 有兩種已發現子型：`S_HitReactionSuppressList` 是 C242,C2,H(count),count×H(gfxId)，count≤512；`S_ItemStatus.writeLongStatus` 是 C242,C1,D(itemId),S(viewName),D(count),H(statusLength),statusLength×C(byte)，RO 且已鑑定且 status 長度 256..65535 才使用。
- 常用表只列明示的 overload：`S_MapID(int)` 另有 16-byte 含非零常數尾段，不能套用 `(int,boolean)` 的零 padding 規則；`S_SkillSound(int,int,int)` 比一般 S55 多 H(time)，語義長度 9、補齊 16。`S_EffectLocation`／`S_Resurrection`／`S_PacketBoxItemLv`／`S_PacketBoxTest` 亦有參數指定 opcode 的建構子，須查呼叫者，不能猜固定數值。
- `S_ChatOut`、`S_HireSoldier`、`S_ItemError`、`S_RedMessage` 的 opcode 寫入已被註解，`S_Letter` 沒有欄位，`S_PacketNew` 全部停用；不能把檔名當作已啟用封包。`S_WarTime(int op)` 由參數決定 opcode，另一 overload 沒有寫 opcode。`S_PcPositionCorrection` 繼承 S_OwnCharPack 的 S87 格式。
- `server/utils/Teleportation.java` 另有內部 `S_TeleportRecovery(ServerBasePacket,BooleanSupplier)`：沒有新 opcode，透過 `Byte(packet.prepareForWrite())` 包裝既有完整 payload，再 `getBytes()`。本來源的 Byte 會多加 00，所以原本已補齊的 N-byte payload 最終變成 N+8 bytes；需特別核對接收端的嚴格尾端檢查，不能把這個 wrapper 當成位元完全不變。

## C opcode 全表

來源 `1-伺服器/src/com/lineage/echo/OpcodesClient.java`、`PacketHandler.handlePacket`、`ClientPacketStructure.layoutFor/validate`。常數 107 個（106 個不同數值）。layout 是前置檢查的**必要前綴**，不是全部條件尾段；空字串表示該層不要求欄位，並不保證 handler 不讀資料。C13 等只有常數而無分派者，在目前 SessionPacketPolicy 也不獲准執行。

| 十進位／hex | 常數（省略 C_OPCODE_） | 實際處理器 .start | preflight 前綴（不含 opcode） |
|---|---|---|---|
| 2 / 0x02 | `TRADE` | [C_Trade](#c-trade) | 空（另查條件尾段） |
| 3 / 0x03 | `BOOKMARKDELETE` | [C_DeleteBookmark](#c-deletebookmark) | `S` |
| 4 / 0x04 | `BUDDYLIST` | [C_Buddy](#c-buddy) | 空（另查條件尾段） |
| 5 / 0x05 | `FIGHT` | [C_Fight](#c-fight) | 空（另查條件尾段） |
| 6 / 0x06 | `USESKILL` | [C_UseSkill](#c-useskill) | `CC` |
| 7 / 0x07 | `CHANGECHAR` | [C_NewCharSelect](#c-newcharselect) | 空（另查條件尾段） |
| 10 / 0x0A | `BOARD` | [C_Board](#c-board) | `D` |
| 11 / 0x0B | `AMOUNT` | [C_Amount](#c-amount) | `DDCS` |
| 13 / 0x0D | `WAREHOUSELOCK` | 無啟用分派 | 無 |
| 14 / 0x0E | `CLIENTVERSION` | [C_ServerVersion](#c-serverversion) | 空（另查條件尾段） |
| 16 / 0x10 | `COMMONCLICK` | [C_CommonClick](#c-commonclick) | 空（另查條件尾段） |
| 18 / 0x12 | `EMBLEMUPLOAD` | [C_Emblem](#c-emblem) | 空（另查條件尾段） |
| 19 / 0x13 | `TAXRATE` | [C_TaxRate](#c-taxrate) | `DC` |
| 20 / 0x14 | `SELECTLIST` | [C_SelectList](#c-selectlist) | `DD` |
| 23 / 0x17 | `BOARDNEXT` | [C_BoardBack](#c-boardback) | `DD` |
| 25 / 0x19 | `DROPITEM` | [C_DropItem](#c-dropitem) | `HHDD` |
| 26 / 0x1A | `LOGINTOSERVEROK` | [C_LoginToServerOK](#c-logintoserverok) | `CC` |
| 29 / 0x1D | `MOVECHAR` | [C_MoveChar](#c-movechar) | `HHC` |
| 30 / 0x1E | `DUNGEONTELEPORT` | 無啟用分派 | 無 |
| 33 / 0x21 | `LEAVEPARTY` | [C_LeaveParty](#c-leaveparty) | 空（另查條件尾段） |
| 34 / 0x22 | `NPCTALK` | [C_NPCTalk](#c-npctalk) | `D` |
| 35 / 0x23 | `防掛續存` | 無啟用分派 | 無 |
| 36 / 0x24 | `GMTELEPORT` | 無啟用分派 | 無 |
| 37 / 0x25 | `TRADEADDITEM` | [C_TradeAddItem](#c-tradeadditem) | `DD` |
| 38 / 0x26 | `SHOP` | [C_Shop](#c-shop) | `C` |
| 39 / 0x27 | `SKILLBUYOK` | [C_SkillBuyOK](#c-skillbuyok) | 空（另查條件尾段） |
| 40 / 0x28 | `CHATGLOBAL` | [C_ChatGlobal](#c-chatglobal) | `CS` |
| 41 / 0x29 | `DOOR` | [C_Door](#c-door) | `DHH` |
| 43 / 0x2B | `PARTYLIST` | [C_Party](#c-party) | 空（另查條件尾段） |
| 44 / 0x2C | `DRAWAL` | [C_Drawal](#c-drawal) | `DD` |
| 45 / 0x2D | `GIVEITEM` | [C_GiveItem](#c-giveitem) | `DHHDD` |
| 47 / 0x2F | `PRIVATESHOPLIST` | [C_ShopList](#c-shoplist) | `CD` |
| 50 / 0x32 | `PROPOSE` | [C_Propose](#c-propose) | `C` |
| 51 / 0x33 | `CHECKPK` | [C_CheckPK](#c-checkpk) | 空（另查條件尾段） |
| 52 / 0x34 | `TELEPORT` | [C_Teleport](#c-teleport) | 空（另查條件尾段） |
| 56 / 0x38 | `DEPOSIT` | [C_Deposit](#c-deposit) | `DD` |
| 61 / 0x3D | `LEAVECLANE` | [C_LeaveClan](#c-leaveclan) | 空（另查條件尾段） |
| 62 / 0x3E | `FISHCLICK` | [C_FishClick](#c-fishclick) | 空（另查條件尾段） |
| 63 / 0x3F | `RESTARTMENU` | [C_Rank](#c-rank) | `C` |
| 63 / 0x3F | `RANK` | [C_Rank](#c-rank) | `C` |
| 68 / 0x44 | `PLEDGE` | [C_Pledge](#c-pledge) | 空（另查條件尾段） |
| 69 / 0x45 | `BANCLAN` | [C_BanClan](#c-banclan) | `S` |
| 71 / 0x47 | `TRADEADDOK` | [C_TradeOK](#c-tradeok) | 空（另查條件尾段） |
| 72 / 0x48 | `CLAN` | [C_Clan](#c-clan) | `D` |
| 76 / 0x4C | `CLANMATCHING` | [C_PledgeRecommendation](#c-pledgerecommendation) | `C` |
| 78 / 0x4E | `PLEDGECONTENT` | [C_PledgeContent](#c-pledgecontent) | `C` |
| 84 / 0x54 | `NEWCHAR` | [C_CreateChar](#c-createchar) | `SCCCCCCCC` |
| 86 / 0x56 | `TRADEADDCANCEL` | [C_TradeCancel](#c-tradecancel) | 空（另查條件尾段） |
| 87 / 0x57 | `MAIL` | [C_Mail](#c-mail) | `C` |
| 90 / 0x5A | `TITLE` | [C_Title](#c-title) | `SS` |
| 95 / 0x5F | `KEEPALIVE` | [C_KeepALIVE](#c-keepalive) | 空（另查條件尾段） |
| 98 / 0x62 | `CHARRESET` | [C_CharReset](#c-charreset) | `C` |
| 99 / 0x63 | `CHECK` | [C_Check](#c-check) | `D` |
| 103 / 0x67 | `PETMENU` | [C_PetMenu](#c-petmenu) | `D` |
| 104 / 0x68 | `USEPETITEM` | [C_UsePetItem](#c-usepetitem) | `CDC` |
| 112 / 0x70 | `PICKUPITEM` | [C_PickUpItem](#c-pickupitem) | `HHDD` |
| 114 / 0x72 | `BOARDREAD` | [C_BoardRead](#c-boardread) | `DD` |
| 118 / 0x76 | `FIX_WEAPON_LIST` | [C_FixWeaponList](#c-fixweaponlist) | 空（另查條件尾段） |
| 119 / 0x77 | `LOGINPACKET` | [C_AuthLogin](#c-authlogin) | `SS` |
| 120 / 0x78 | `EXTCOMMAND` | [C_ExtraCommand](#c-extracommand) | `C` |
| 121 / 0x79 | `ATTR` | [C_Attr](#c-attr) | 空（另查條件尾段） |
| 122 / 0x7A | `QUITGAME` | [C_Disconnect](#c-disconnect) | 空（另查條件尾段） |
| 123 / 0x7B | `ARROWATTACK` | [C_AttackBow](#c-attackbow) | `DHH` |
| 125 / 0x7D | `NPCACTION` | [C_NPCAction](#c-npcaction) | `DS` |
| 128 / 0x80 | `CASTLESECURITY` | 無啟用分派 | 無 |
| 129 / 0x81 | `CLANATTENTION` | [C_ClanAttention](#c-clanattention) | `C` |
| 136 / 0x88 | `CHAT` | [C_Chat](#c-chat) | `CS` |
| 137 / 0x89 | `LOGINTOSERVER` | [C_LoginToServer](#c-logintoserver) | `S` |
| 138 / 0x8A | `DELETEINVENTORYITEM` | [C_DeleteInventoryItem](#c-deleteinventoryitem) | `D` |
| 141 / 0x8D | `BOARDWRITE` | [C_BoardWrite](#c-boardwrite) | `DSS` |
| 145 / 0x91 | `SKILLBUY` | [C_SkillBuy](#c-skillbuy) | `D` |
| 153 / 0x99 | `BOARDDELETE` | [C_BoardDelete](#c-boarddelete) | `DD` |
| 161 / 0xA1 | `RESULT` | [C_Result](#c-result) | 空（另查條件尾段） |
| 162 / 0xA2 | `DELETECHAR` | [C_DeleteChar](#c-deletechar) | `S` |
| 164 / 0xA4 | `USEITEM` | [C_ItemUSe](#c-itemuse) | `D` |
| 165 / 0xA5 | `BOOKMARK` | [C_AddBookmark](#c-addbookmark) | `S` |
| 171 / 0xAB | `EXCLUDE` | [C_Exclude](#c-exclude) | `S` |
| 173 / 0xAD | `EXIT_GHOST` | [C_ExitGhost](#c-exitghost) | 空（另查條件尾段） |
| 177 / 0xB1 | `RESTART` | [C_Restart](#c-restart) | 空（另查條件尾段） |
| 184 / 0xB8 | `CHATWHISPER` | [C_ChatWhisper](#c-chatwhisper) | `SS` |
| 185 / 0xB9 | `CALL` | [C_CallPlayer](#c-callplayer) | `S` |
| 191 / 0xBF | `SKILLBUYOKITEM` | [C_SkillBuyItemOK](#c-skillbuyitemok) | 空（另查條件尾段） |
| 194 / 0xC2 | `JOINCLAN` | [C_JoinClan](#c-joinclan) | 空（另查條件尾段） |
| 199 / 0xC7 | `CAHTPARTY` | [C_ChatParty](#c-chatparty) | `C` |
| 202 / 0xCA | `DELBUDDY` | [C_DelBuddy](#c-delbuddy) | `S` |
| 206 / 0xCE | `WHO` | [C_Who](#c-who) | `S` |
| 207 / 0xCF | `ADDBUDDY` | [C_AddBuddy](#c-addbuddy) | `S` |
| 208 / 0xD0 | `WEB_WINDOW` | [C_WebWindow](#c-webwindow) | 空（另查條件尾段） |
| 210 / 0xD2 | `BEANFUNLOGINPACKET` | [C_AutoLogin](#c-autologin) | `CSS` |
| 211 / 0xD3 | `CUSTOM_RESERVED_211` | 無啟用分派 | 無 |
| 212 / 0xD4 | `CUSTOM_RESERVED_212` | 無啟用分派 | 無 |
| 213 / 0xD5 | `CUSTOM_RESERVED_213` | 無啟用分派 | 無 |
| 214 / 0xD6 | `CUSTOM_RESERVED_214` | 無啟用分派 | 無 |
| 219 / 0xDB | `ENTERPORTAL` | [C_EnterPortal](#c-enterportal) | `HH` |
| 222 / 0xDE | `CREATECLAN` | [C_CreateClan](#c-createclan) | `S` |
| 223 / 0xDF | `SELECTTARGET` | [C_SelectTarget](#c-selecttarget) | `DCD` |
| 225 / 0xE1 | `CHANGEHEADING` | [C_ChangeHeading](#c-changeheading) | `C` |
| 227 / 0xE3 | `WAR` | [C_War](#c-war) | `CS` |
| 229 / 0xE5 | `ATTACK` | [C_Attack](#c-attack) | `DHH` |
| 230 / 0xE6 | `CREATEPARTY` | [C_CreateParty](#c-createparty) | `C` |
| 231 / 0xE7 | `SHIP` | [C_Ship](#c-ship) | `HHH` |
| 244 / 0xF4 | `CHARACTERCONFIG` | [C_CharcterConfig](#c-charcterconfig) | 空（另查條件尾段） |
| 245 / 0xF5 | `SKILLBUYITEM` | [C_SkillBuyItem](#c-skillbuyitem) | `D` |
| 246 / 0xF6 | `TELEPORTLOCK` | [C_UnLock](#c-unlock) | `C` |
| 253 / 0xFD | `SMS` | 無啟用分派 | 無 |
| 254 / 0xFE | `SENDLOCATION` | [C_Windows](#c-windows) | `C` |
| 255 / 0xFF | `BANPARTY` | [C_BanParty](#c-banparty) | `S` |

## S opcode 全表

來源 `1-伺服器/src/com/lineage/server/serverpackets/OpcodesServer.java`。列出所有 112 個常數，包含別名；類別欄按實際引用常數配對，沒有引用不代表數值未被別名使用。所有建構子／序列化方法見下方同名索引。

| 十進位／hex | 常數（省略 S_OPCODE_） | 序列化類別 |
|---|---|---|
| 0 / 0x00 | `CLANMATCHING` | [S_PacketBoxPledgeRecommend](#s-packetboxpledgerecommend) |
| 4 / 0x04 | `DEPOSIT` | [S_Deposit](#s-deposit) |
| 5 / 0x05 | `INVLIST` | [S_InvList](#s-invlist) |
| 6 / 0x06 | `DETELECHAROK` | [S_DeleteCharOK](#s-deletecharok) |
| 8 / 0x08 | `OWNCHARSTATUS` | [S_OwnCharStatus](#s-owncharstatus) |
| 10 / 0x0A | `MOVEOBJECT` | [S_MoveCharPacket](#s-movecharpacket)、[S_MoveNpcPacket](#s-movenpcpacket) |
| 11 / 0x0B | `TRUETARGET` | [S_TrueTarget](#s-truetarget) |
| 13 / 0x0D | `SKILLBUYITEM` | [S_SkillBuyItem](#s-skillbuyitem)、[S_SkillBuyItemCN](#s-skillbuyitemcn) |
| 15 / 0x0F | `ADDITEM` | [S_AddItem](#s-additem) |
| 22 / 0x16 | `SOUND` | [S_Sound](#s-sound) |
| 24 / 0x18 | `ITEMSTATUS` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 24 / 0x18 | `ITEMAMOUNT` | [S_ItemAmount](#s-itemamount)、[S_ItemStatus](#s-itemstatus) |
| 30 / 0x1E | `ATTACKPACKET` | [S_AttackPacketNpc](#s-attackpacketnpc)、[S_AttackPacketPc](#s-attackpacketpc)、[S_UseArrowSkill](#s-usearrowskill)、[S_UseAttackSkill](#s-useattackskill) |
| 33 / 0x21 | `MPUPDATE` | [S_MPUpdate](#s-mpupdate) |
| 34 / 0x22 | `LAWFUL` | [S_Lawful](#s-lawful) |
| 35 / 0x23 | `TRADEADDITEM` | [S_TradeAddItem](#s-tradeadditem) |
| 36 / 0x24 | `ABILITY` | [S_Ability](#s-ability) |
| 37 / 0x25 | `SPMR` | [S_SPMR](#s-spmr) |
| 39 / 0x27 | `SHOWHTML` | [S_AuctionBoardRead](#s-auctionboardread)、[S_Bonusstats](#s-bonusstats)、[S_Buddy](#s-buddy)、[S_CloseList](#s-closelist)、[S_GMHtml](#s-gmhtml)、[S_NoSell](#s-nosell)、[S_NPCTalkActionTPUrl](#s-npctalkactiontpurl)、[S_NPCTalkReturn](#s-npctalkreturn)、[S_Party](#s-party)、[S_PetMenuPacket](#s-petmenupacket)、[S_Pledge](#s-pledge)、[S_ShowPolyList](#s-showpolylist)、[S_ShowSummonList](#s-showsummonlist)、[S_SkillBuyCN](#s-skillbuycn) |
| 40 / 0x28 | `LIGHT` | [S_Light](#s-light) |
| 41 / 0x29 | `SKILLBUY` | [S_SkillBuy](#s-skillbuy)、[S_SkillBuyCN](#s-skillbuycn) |
| 42 / 0x2A | `RANGESKILLS` | [S_RangeSkill](#s-rangeskill) |
| 46 / 0x2E | `CHANGENAME` | [S_ChangeName](#s-changename) |
| 47 / 0x2F | `CURSEBLIND` | [S_CurseBlind](#s-curseblind) |
| 48 / 0x30 | `COMMONNEWS` | [S_CommonInfo](#s-commoninfo)、[S_CommonNews](#s-commonnews) |
| 52 / 0x34 | `TRADE` | [S_Trade](#s-trade) |
| 55 / 0x37 | `SKILLSOUNDGFX` | [S_SkillSound](#s-skillsound) |
| 57 / 0x39 | `DELETEINVENTORYITEM` | [S_DeleteInventoryItem](#s-deleteinventoryitem) |
| 60 / 0x3C | `PINKNAME` | [S_PinkName](#s-pinkname) |
| 64 / 0x40 | `CHARSYNACK` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 64 / 0x40 | `CHARRESET` | [S_BookmarkList](#s-bookmarklist)、[S_CharReset](#s-charreset)、[S_CharResetInfo](#s-charresetinfo)、[S_EquipmentWindow](#s-equipmentwindow)、[S_InitialAbilityGrowth](#s-initialabilitygrowth)、[S_PetCtrlMenu](#s-petctrlmenu)、[S_Unknown_B](#s-unknown-b) |
| 65 / 0x41 | `SHOWSHOPSELLLIST` | [S_ShopBuyList](#s-shopbuylist)、[S_ShopBuyListAll](#s-shopbuylistall)、[S_ShopBuyListGam](#s-shopbuylistgam)、[S_ShopBuyListLotto](#s-shopbuylistlotto) |
| 67 / 0x43 | `SKILLBRAVE` | [S_SkillBrave](#s-skillbrave) |
| 68 / 0x44 | `BOARD` | [S_Board](#s-board) |
| 69 / 0x45 | `CASTLEMASTER` | [S_CastleMaster](#s-castlemaster) |
| 70 / 0x46 | `SHOWSHOPBUYLIST` | [S_AuctionShopSellList](#s-auctionshopselllist)、[S_BoxChoice](#s-boxchoice)、[S_ShopSellList](#s-shopselllist)、[S_ShopSellListCn](#s-shopselllistcn)、[S_ShopSellListCnX](#s-shopselllistcnx)、[S_ShopSellListGam](#s-shopselllistgam)、[S_ShopSellListLotto](#s-shopselllistlotto)、[S_ShopSellListTmp](#s-shopselllisttmp)、[S_ShopSellListX1](#s-shopselllistx1) |
| 71 / 0x47 | `SERVERMSG` | [S_ServerMessage](#s-servermessage)、[S_WhoAmount](#s-whoamount)、[S_WhoCharinfo](#s-whocharinfo) |
| 72 / 0x48 | `UPDATECLANID` | [S_ClanUpdate](#s-clanupdate) |
| 72 / 0x48 | `NEWMASTER` | [S_NewMaster](#s-newmaster) |
| 73 / 0x49 | `WHISPERCHAT` | [S_ChatWhisperFrom](#s-chatwhisperfrom) |
| 76 / 0x4C | `POLY` | [S_ChangeShape](#s-changeshape) |
| 81 / 0x51 | `NORMALCHAT` | [S_Chat](#s-chat)、[S_ChatParty2](#s-chatparty2)、[S_ChatShouting](#s-chatshouting) |
| 83 / 0x53 | `SELECTLIST` | [S_FixWeaponList](#s-fixweaponlist)、[S_PetList](#s-petlist) |
| 84 / 0x54 | `WAR` | [S_War](#s-war) |
| 85 / 0x55 | `RESURRECTION` | [S_Resurrection](#s-resurrection) |
| 87 / 0x57 | `CHARPACK` | [S_BoxPack](#s-boxpack)、[S_DoorPack](#s-doorpack)、[S_FollowerPack](#s-followerpack)、[S_NPCPack](#s-npcpack)、[S_NPCPack_D](#s-npcpack-d)、[S_NPCPack_De](#s-npcpack-de)、[S_NPCPack_Doll](#s-npcpack-doll)、[S_NPCPack_Eff](#s-npcpack-eff)、[S_NPCPack_F](#s-npcpack-f)、[S_NPCPack_Ill](#s-npcpack-ill)、[S_NPCPack_Item](#s-npcpack-item)、[S_NPCPack_M](#s-npcpack-m)、[S_NPCPack_Pet](#s-npcpack-pet)、[S_NPCPack_Signboard](#s-npcpack-signboard)、[S_NPCPack_Summon](#s-npcpack-summon)、[S_OtherCharPacks](#s-othercharpacks)、[S_OwnCharPack](#s-owncharpack)、[S_Trap](#s-trap) |
| 87 / 0x57 | `DROPITEM` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 92 / 0x5C | `BOOKMARKS` | [S_Bookmarks](#s-bookmarks) |
| 93 / 0x5D | `CHARLIST` | [S_CharPacks](#s-charpacks) |
| 98 / 0x62 | `NEWCHARWRONG` | [S_CharCreateStatus](#s-charcreatestatus) |
| 100 / 0x64 | `ITEMNAME` | [S_ItemName](#s-itemname) |
| 103 / 0x67 | `LIQUOR` | [S_Liquor](#s-liquor) |
| 105 / 0x69 | `REDMESSAGE` | [S_BlueMessage](#s-bluemessage) |
| 106 / 0x6A | `EFFECTLOCATION` | [S_EffectLocation](#s-effectlocation) |
| 112 / 0x70 | `TRADESTATUS` | [S_TradeStatus](#s-tradestatus) |
| 113 / 0x71 | `EXP` | [S_Exp](#s-exp) |
| 115 / 0x73 | `WEATHER` | [S_Weather](#s-weather) |
| 117 / 0x75 | `PUTSOLDIER` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 118 / 0x76 | `EMBLEM` | [S_Emblem](#s-emblem) |
| 119 / 0x77 | `CHARVISUALUPDATE` | [S_CharVisualUpdate](#s-charvisualupdate) |
| 120 / 0x78 | `REMOVE_OBJECT` | [S_RemoveObject](#s-removeobject) |
| 122 / 0x7A | `CHANGEHEADING` | [S_ChangeHeading](#s-changeheading) |
| 123 / 0x7B | `GAMETIME` | [S_GameTime](#s-gametime) |
| 126 / 0x7E | `BLESSOFEVA` | [S_SkillIconBlessOfEva](#s-skilliconblessofeva) |
| 127 / 0x7F | `NEWCHARPACK` | [S_NewCharPacket](#s-newcharpacket) |
| 136 / 0x88 | `INPUTAMOUNT` | [S_ApplyAuction](#s-applyauction)、[S_HowManyMake](#s-howmanymake)、[S_ItemCount](#s-itemcount)、[S_SellHouse](#s-sellhouse) |
| 139 / 0x8B | `SERVERVERSION` | [S_ServerVersion](#s-serverversion) |
| 140 / 0x8C | `PRIVATESHOPLIST` | [S_PrivateShop](#s-privateshop) |
| 141 / 0x8D | `DRAWAL` | [S_Drawal](#s-drawal) |
| 148 / 0x94 | `BOARDREAD` | [S_BoardRead](#s-boardread) |
| 149 / 0x95 | `TELEPORT` | [S_Teleport](#s-teleport)、[S_Teleport2](#s-teleport2) |
| 150 / 0x96 | `INITPACKET` | OpcodesClient._firstPacket |
| 155 / 0x9B | `OWNCHARSTATUS2` | [S_OwnCharStatus2](#s-owncharstatus2) |
| 156 / 0x9C | `HOUSELIST` | [S_AuctionBoard](#s-auctionboard) |
| 158 / 0x9E | `DOACTIONGFX` | [S_DoActionGFX](#s-doactiongfx)、[S_DoActionShop](#s-doactionshop)、[S_Fishing](#s-fishing) |
| 160 / 0xA0 | `DELSKILL` | [S_DelSkill](#s-delskill) |
| 161 / 0xA1 | `NPCSHOUT` | [S_BoxMessage](#s-boxmessage)、[S_HelpMessage](#s-helpmessage)、[S_KillMessage](#s-killmessage)、[S_NpcChat](#s-npcchat)、[S_NpcChatGlobal](#s-npcchatglobal)、[S_NpcChatShouting](#s-npcchatshouting)、[S_ServerMessage](#s-servermessage)、[S_SystemMessage](#s-systemmessage)、[S_ToGmMessage](#s-togmmessage) |
| 164 / 0xA4 | `ADDSKILL` | [S_AddSkill](#s-addskill) |
| 165 / 0xA5 | `POISON` | [S_Poison](#s-poison) |
| 166 / 0xA6 | `STRUP` | [S_Strup](#s-strup) |
| 171 / 0xAB | `INVIS` | [S_Invis](#s-invis) |
| 174 / 0xAE | `OWNCHARATTRDEF` | [S_OwnCharAttrDef](#s-owncharattrdef) |
| 176 / 0xB0 | `SHOWRETRIEVELIST` | [S_AuctionShopRetrieve](#s-auctionshopretrieve)、[S_PetInventory](#s-petinventory)、[S_PowerItemList](#s-poweritemlist)、[S_RetrieveChaList](#s-retrievechalist)、[S_RetrieveDevilKingList](#s-retrievedevilkinglist)、[S_RetrieveElfList](#s-retrieveelflist)、[S_RetrieveList](#s-retrievelist)、[S_RetrievePledgeList](#s-retrievepledgelist)、[S_RetrievePowerItem](#s-retrievepoweritem)、[S_RetrieveShiftingList](#s-retrieveshiftinglist) |
| 178 / 0xB2 | `CHARAMOUNT` | [S_CharAmount](#s-charamount) |
| 183 / 0xB7 | `CHARTITLE` | [S_CharTitle](#s-chartitle) |
| 185 / 0xB9 | `TAXRATE` | [S_TaxRate](#s-taxrate) |
| 186 / 0xBA | `MAIL` | [S_Mail](#s-mail) |
| 187 / 0xBB | `HOUSEMAP` | [S_HouseMap](#s-housemap) |
| 188 / 0xBC | `DEXUP` | [S_Dexup](#s-dexup) |
| 200 / 0xC8 | `CLANATTENTION` | [S_ClanAttention](#s-clanattention) |
| 202 / 0xCA | `PARALYSIS` | [S_Paralysis](#s-paralysis) |
| 206 / 0xCE | `MAPID` | [S_MapID](#s-mapid)、[S_PcPositionMapReset](#s-pcpositionmapreset) |
| 206 / 0xCE | `UNDERWATER` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 209 / 0xD1 | `ATTRIBUTE` | [S_Door](#s-door) |
| 216 / 0xD8 | `SKILLICONSHIELD` | [S_SkillIconShield](#s-skilliconshield) |
| 219 / 0xDB | `YES_NO` | [S_Message_YN](#s-message-yn) |
| 223 / 0xDF | `LOGINTOGAME` | [S_EnterGame](#s-entergame) |
| 225 / 0xE1 | `HPUPDATE` | [S_HPUpdate](#s-hpupdate) |
| 227 / 0xE3 | `DISCONNECT` | [S_Disconnect](#s-disconnect) |
| 230 / 0xE6 | `SPOLY` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 233 / 0xE9 | `LOGINRESULT` | [S_LoginResult](#s-loginresult) |
| 236 / 0xEC | `SELECTTARGET` | [S_SelectTarget](#s-selecttarget) |
| 237 / 0xED | `HPMETER` | [S_HPMeter](#s-hpmeter) |
| 240 / 0xF0 | `ITEMCOLOR` | [S_ItemColor](#s-itemcolor) |
| 241 / 0xF1 | `MOVELOCK` | [S_Lock](#s-lock) |
| 243 / 0xF3 | `GLOBALCHAT` | [S_ChatClan](#s-chatclan)、[S_ChatClanUnion](#s-chatclanunion)、[S_ChatGlobal](#s-chatglobal)、[S_ChatParty](#s-chatparty)、[S_ChatTransaction](#s-chattransaction)、[S_ChatWhisperTo](#s-chatwhisperto)、[S_SystemMessage](#s-systemmessage) |
| 245 / 0xF5 | `IDENTIFYDESC` | [S_IdentifyDesc](#s-identifydesc) |
| 250 / 0xFA | `PACKETBOX` | [S_BroadcastPos](#s-broadcastpos)、[S_ClassicBadge](#s-classicbadge)、[S_ClassicBagCombine](#s-classicbagcombine)、[S_ClassicBoss](#s-classicboss)、[S_ClassicCollection](#s-classiccollection)、[S_ClassicCraft](#s-classiccraft)、[S_ClassicInventoryFilter](#s-classicinventoryfilter)、[S_ClassicPass](#s-classicpass)、[S_ClassicQuest](#s-classicquest)、[S_ClassicRanking](#s-classicranking)、[S_ClassicReward](#s-classicreward)、[S_GlobalEffect](#s-globaleffect)、[S_IconConfig](#s-iconconfig)、[S_Icons](#s-icons)、[S_ItemBoard](#s-itemboard)、[S_Karma](#s-karma)、[S_MagicEquipment](#s-magicequipment)、[S_PacketBox](#s-packetbox)、[S_PacketBoxActiveSpells](#s-packetboxactivespells)、[S_PacketBoxConfig](#s-packetboxconfig)、[S_PacketBoxCooking](#s-packetboxcooking)、[S_PacketBoxDk](#s-packetboxdk)、[S_PacketBoxExp](#s-packetboxexp)、[S_PacketBoxGame](#s-packetboxgame)、[S_PacketBoxGm](#s-packetboxgm)、[S_PacketBoxGree](#s-packetboxgree)、[S_PacketBoxHpMsg](#s-packetboxhpmsg)、[S_PacketBoxIcon1](#s-packetboxicon1)、[S_PacketBoxIcon2](#s-packetboxicon2)、[S_PacketBoxIconAura](#s-packetboxiconaura)、[S_PacketBoxItemLv](#s-packetboxitemlv)、[S_PacketBoxLoc](#s-packetboxloc)、[S_PacketBoxMapTimer](#s-packetboxmaptimer)、[S_PacketBoxParty](#s-packetboxparty)、[S_PacketBoxProtection](#s-packetboxprotection)、[S_PacketBoxSelect](#s-packetboxselect)、[S_PacketBoxTest](#s-packetboxtest)、[S_PacketBoxThirdSpeed](#s-packetboxthirdspeed)、[S_PacketBoxWar](#s-packetboxwar)、[S_PacketBoxWaterLife](#s-packetboxwaterlife)、[S_PacketBoxWindShackle](#s-packetboxwindshackle)、[S_PacketBoxWisdomPotion](#s-packetboxwisdompotion)、[S_PetEquipment](#s-petequipment)、[S_Pledge](#s-pledge)、[S_PledgeWarehouseHistory](#s-pledgewarehousehistory)、[S_ShowDrop](#s-showdrop)、[S_WeaponRange](#s-weaponrange) |
| 250 / 0xFA | `ACTIVESPELLS` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 250 / 0xFA | `SKILLICONGFX` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 250 / 0xFA | `UNKNOWN2` | 沒有直接引用此常數的啟用 serializer（檢查同值別名） |
| 255 / 0xFF | `SKILLHASTE` | [S_SkillHaste](#s-skillhaste) |
| 242 / 0xF2 | CUSTOM_OPCODE／直接常數（類別內） | [S_HitReactionSuppressList](#s-hitreactionsuppresslist)（sub2）、[S_ItemStatus](#s-itemstatus)（sub1） |

## C 欄位與方法索引

103 個 C_ 來源檔全部列出；沒有被 PacketHandler 分派的類別僅作來源索引，不代表可使用。條件尾段以對應方法為準。

<a id="c-addbookmark"></a>

### C_AddBookmark

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_AddBookmark.java`；SHA-256：`011bfc8e81c50067004becd19907e480b84a3afd91004f5de9fbe0d4e9f392c3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @52 | `S(locName)@82` |

<a id="c-addbuddy"></a>

### C_AddBuddy

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_AddBuddy.java`；SHA-256：`7ddae0a303a2114d5299dcfb06c127dcd53b8472374e67699c3f821c12573484`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @53 | `S(charName)@59` |

<a id="c-amount"></a>

### C_Amount

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Amount.java`；SHA-256：`fc600dacece39a424a21bece8634aed3a223e7cd583a2084474cf8976e5510b3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @135 | `D(objectId)@169` → `D(未命名／忽略)@171` → `C(未命名／忽略)@177` → `S(s)@179` |

<a id="c-attack"></a>

### C_Attack

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Attack.java`；SHA-256：`3726df0441bcdf88e62a24e63bb81d36b78106f7d21131d571979d8cc3946324`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @33 | `D(targetId)@94` → `H(locx)@95` → `H(locy)@96`<br>〔`if remainingBytes() > 0`〕 `C(未命名／忽略)@99` |

<a id="c-attackbow"></a>

### C_AttackBow

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_AttackBow.java`；SHA-256：`cc2087e4c62a3d6c5d48ba9e8851a0cbfa317219e2225fa5e98932c3b47d6613`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @53 | `D(targetId)@136` → `H(locx)@137` → `H(locy)@138` |

<a id="c-attr"></a>

### C_Attr

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Attr.java`；SHA-256：`793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23`。

局部常數（subcode／格式旗標／限制）：`MAX_DELAYED_DISPATCH_RETRIES=8`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @275 | `H(mode)@297`<br>〔`if mode == 0`〕 `D(confirmationId)@300` → `H(mode)@301`<br>〔`switch mode / case 622`〕 `C(c)@308`<br>〔`switch mode / case 97`〕 `C(c)@318`<br>〔`switch mode / case 222`〕 `C(c)@423`<br>〔`switch mode / case 252`〕 `C(c)@465`<br>〔`switch mode / case 321`〕 `C(c)@472`<br>〔`switch mode / case 322`〕 `C(c)@497`<br>〔`switch mode / case 325`〕 `C(c)@528` → `S(petName)@529`<br>〔`switch mode / case 512`〕 `C(c)@602` → `S(未命名／忽略)@603`<br>〔`switch mode / case 630`〕 `C(c)@607`<br>〔`switch mode / case 653`〕 `C(c)@628`<br>〔`switch mode / case 654`〕 `C(c)@665`<br>〔`switch mode / case 729`〕 `C(c)@698`<br>〔`switch mode / case 738`〕 `C(c)@708`<br>〔`switch mode / case 748`〕 `C(c)@725`<br>〔`switch mode / case 2751`〕 `C(c)@739`<br>〔`switch mode / case 951`〕 `C(c)@837`<br>〔`switch mode / case 953`〕 `C(c)@867`<br>〔`switch mode / case 479`〕 `C(c)@900`<br>〔`switch mode / case 479 → if c == 1`〕 `S(s)@902`<br>〔`switch mode / case 3333`〕 `C(c)@989`<br>〔`switch mode / case 3312`〕 `C(c)@1067`<br>〔`switch mode / case 3313`〕 `C(c)@1080`<br>〔`switch mode / case 3589`〕 `C(c)@1093`<br>〔`switch mode / case 3348`〕 `C(attentionYn)@1106` |

<a id="c-authlogin"></a>

### C_AuthLogin

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_AuthLogin.java`；SHA-256：`1902f2244a893eaa8e9a434ce4293a3cbdfa460a5bef7e8210c8696d428bfaa1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @100 | `S(loginName)@113` → `S(password)@128` |

<a id="c-autologin"></a>

### C_AutoLogin

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_AutoLogin.java`；SHA-256：`b9eb654a9c72a887eba477a1f14feb6fd5f8d1f09625a82e85a0d0c07275c9a5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @18 | `C(un)@23` → `S(loginName)@26` → `S(srcpassword)@27` |

<a id="c-banclan"></a>

### C_BanClan

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_BanClan.java`；SHA-256：`aad634b1ae485cc7a85c756d7dca162beb7a5c4fdcc52c031cee0af314829ce9`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @73 | `S(s)@79` |

<a id="c-banparty"></a>

### C_BanParty

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_BanParty.java`；SHA-256：`c149cad2cc052d5876eee112b88a2db7a560658b836310963badbcb57a1ad900`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @37 | `S(userName)@49` |

<a id="c-board"></a>

### C_Board

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Board.java`；SHA-256：`20e3437630822a9b866d9e1a56865046477f4110451fb0c23d870defe5da8da5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @43 | `D(objId)@48` |

<a id="c-boardback"></a>

### C_BoardBack

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_BoardBack.java`；SHA-256：`96cebb5ea143b18b64852449fab085d1174b8484787db62c1f5842b8848d1d8b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @35 | `D(objId)@40` → `D(topicNumber)@41` |

<a id="c-boarddelete"></a>

### C_BoardDelete

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_BoardDelete.java`；SHA-256：`411192fc2319fe4f8207de579123ae2a32ee688531815bc6f7d711bcac17be68`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @35 | `D(objId)@40` → `D(topicId)@41` |

<a id="c-boardread"></a>

### C_BoardRead

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_BoardRead.java`；SHA-256：`8e99855f4bcd9abf356df2e4628b2a1c165c7b40e5271d2c277cb6c79d244f73`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @35 | `D(objId)@40` → `D(topicNumber)@41` |

<a id="c-boardwrite"></a>

### C_BoardWrite

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_BoardWrite.java`；SHA-256：`d50fbae5dae1b86e75e163d8504a4ef5d3f5f582fe6fd2f80936b56fc4e82453`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @36 | `D(objId)@56` → `S(title)@63` → `S(content)@68` |

<a id="c-buddy"></a>

### C_Buddy

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Buddy.java`；SHA-256：`8b8b6e9533f4d7064d03d1bf0e144d26d9cadd75fcf1186c0023fa41e21fd519`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-callplayer"></a>

### C_CallPlayer

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CallPlayer.java`；SHA-256：`17f431846994d68d83ba269ec362f11698e4bd50084aec71d757cefc2a0e5338`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @92 | `S(arg)@113` |

<a id="c-changeheading"></a>

### C_ChangeHeading

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ChangeHeading.java`；SHA-256：`3ab45e30f01eb9e32f86c4981e64ef76426ff7601c1da8f04bcc4fb79b7691fb`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @34 | `C(heading)@52` |

<a id="c-changewartime"></a>

### C_ChangeWarTime

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ChangeWarTime.java`；SHA-256：`3be0c6d5fec2fc727b26713ebef1256f9f616881c12f5eb275379e44503f9b85`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-charcterconfig"></a>

### C_CharcterConfig

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CharcterConfig.java`；SHA-256：`4d935d45193e99d3598da1067dd2422c27194800d748bff0441c4d73cb01e555`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @51 | `D(length)@67` → `Byte(data[])@72` |

<a id="c-charreset"></a>

### C_CharReset

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CharReset.java`；SHA-256：`3bba21f5677f0625e63e7c407e828fdb721cf97b0353c83ff152ef1f5df5abdf`。

局部常數（subcode／格式旗標／限制）：`QUEST_STEP_CLASS_CHANGE=30677`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @81 | `C(stage)@96`<br>〔`switch stage / case 0x01`〕 `C(str)@99` → `C(intel)@100` → `C(wis)@101` → `C(dex)@102` → `C(con)@103` → `C(cha)@104`<br>〔`switch stage / case 0x02`〕 `C(type2)@138`<br>〔`switch stage / case 0x08 → switch type2 / case 0x08`〕 `C(未命名／忽略)@182`<br>〔`switch stage / case 0x03`〕 `C(read1)@212` → `C(read2)@213` → `C(read3)@214` → `C(read4)@215` → `C(read5)@216` → `C(read6)@217` |

<a id="c-chat"></a>

### C_Chat

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Chat.java`；SHA-256：`72257d44684061e922f79dcd8f7b5ed4604ccaffd875506d2457a00a1873f4b2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @139 | `C(chatType)@201` → `S(chatText)@202` |

<a id="c-chatglobal"></a>

### C_ChatGlobal

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ChatGlobal.java`；SHA-256：`5c673ed8aeaa1c5ec82b8edeb10ae670d8b99321c1042313da607b8bcd8d8708`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @95 | `C(chatType)@183` → `S(chatText)@184` |

<a id="c-chatparty"></a>

### C_ChatParty

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ChatParty.java`；SHA-256：`30d29eb14b5f67ce4c4ba9ff3c02eba729b847b95f85102afa00ad53fd0db608`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @38 | `C(type)@53`<br>〔`switch type / case 0`〕 `S(name)@56` |

<a id="c-chatwhisper"></a>

### C_ChatWhisper

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ChatWhisper.java`；SHA-256：`44d67c3dccbdbe6842ec3a59b6c7c3dcb512e98c8182d79ae563a31d5b87f8ff`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @85 | `S(targetName)@123` → `S(text)@124` |

<a id="c-check"></a>

### C_Check

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Check.java`；SHA-256：`eeca0bd74d5bbcf6a50ac1b33855640984eca0ee626e0f237162fe871f938d34`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @17 | `D(checkType)@23` → `Byte(remainingData)@26` |

<a id="c-checkpk"></a>

### C_CheckPK

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CheckPK.java`；SHA-256：`11ffa8ad46337a48bc3c0d0edaf97f18f9baf200aa79c483d172f986a37371be`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-clan"></a>

### C_Clan

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Clan.java`；SHA-256：`56d98e8cbdc9ff2a72e518050f3ec21699a0be0bf3348f0f75b8590258244d04`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @75 | `D(emblemId)@103` |

<a id="c-clanattention"></a>

### C_ClanAttention

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ClanAttention.java`；SHA-256：`29bc590d3fb56172d47938b4101fd1226934c750f13c87118220abb72d331f0e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @44 | `C(type)@52`<br>〔`switch type / case 0`〕 `S(clanName)@79`<br>〔`switch type / case 1`〕 委派 `handleTypeRemove(pc, clan, decrypt, remain)@85`<br>〔`switch type / case 2`〕 委派 `handleTypeList(pc, clan, decrypt, remain)@86` |
| `private void handleTypeList(final L1PcInstance pc, final L1Clan clan, final byte[] decrypt, final int remain)` @105 | 〔`if remain > 2`〕 `S(未命名／忽略)@125` |
| `private void handleTypeRemove(final L1PcInstance pc, final L1Clan clan, final byte[] decrypt, final int remain)` @131 | 〔`if remain > 2`〕 `S(clanName)@146` |

<a id="c-cnitem"></a>

### C_CnItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CnItem.java`；SHA-256：`a7bc9c023841ca234b19c2b7aec5a81356fe5a7ae87dc0aa3530ab686ad5fc6b`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-commonclick"></a>

### C_CommonClick

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CommonClick.java`；SHA-256：`931376b9fe56edc6d222aa00fc2a4aa843a091ff7d224d868f5b3739530f259f`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-createchar"></a>

### C_CreateChar

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CreateChar.java`；SHA-256：`400c2c4d48b63d0c73fb07a736dde7a5c5b3a03159ed063b76aff3cbe09eae72`。

局部常數（subcode／格式旗標／限制）：`TYPE_COUNT=ORIGINAL_STR.length`；`TYPE_DRAGON_KNIGHT=5`；`TYPE_ILLUSIONIST=6`；`CREATE_CHAR_STAT_BYTES=6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `private void skipCreateCharStats()` @126 | 〔`for int i = 0; i < CREATE_CHAR_STAT_BYTES; i++`〕 `C(未命名／忽略)@128` |
| `public void start(final byte[] decrypt, final ClientExecutor client)` @133 | `S(未命名／忽略)@145` → `C(type)@178` → `C(sex)@179`<br>〔`if type < 0 \|\| type >= TYPE_COUNT \|\| sex < 0 \|\| sex >= CLASS_LIST.length`〕 委派 `skipCreateCharStats()@182`<br>〔`if (type == TYPE_DRAGON_KNIGHT && !ConfigCharSetting.CREATE_CHAR_ALLOW_DRAGON_KNIGHT) \|\| (type == TYPE_ILLUSIONIST && !ConfigCharSetting.CREATE_CHAR_ALLOW_ILLUSIONIST)`〕 委派 `skipCreateCharStats()@189`<br>`C(未命名／忽略)@198` → `C(未命名／忽略)@199` → `C(未命名／忽略)@200` → `C(未命名／忽略)@201` → `C(未命名／忽略)@202` → `C(未命名／忽略)@203` |

<a id="c-createclan"></a>

### C_CreateClan

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CreateClan.java`；SHA-256：`511c19e9ac3175c7d6554b99844704f09045f0cba000ea199651ef059d8556d6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @74 | `S(name)@84` |

<a id="c-createparty"></a>

### C_CreateParty

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_CreateParty.java`；SHA-256：`cd0f739dafc8eae990eb59eee5b7706c4b387b9112edd44914dd9f89c27e9b9c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @42 | `C(type)@61`<br>〔`switch type / case 1`〕 `D(targetId)@66`<br>〔`switch type / case 2`〕 `S(name)@100`<br>〔`switch type / case 3 → if pc.isInParty() → if pc.getParty().isLeader(pc)`〕 `D(objid)@147` |

<a id="c-delbuddy"></a>

### C_DelBuddy

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_DelBuddy.java`；SHA-256：`a841bcc49ef05dd2c287afa55f78f2dca3abb742864c67ae35dd34902c0421ea`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @48 | `S(charName)@59` |

<a id="c-deletebookmark"></a>

### C_DeleteBookmark

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_DeleteBookmark.java`；SHA-256：`1d322e0ee50f28c9a1765d5dbfbc24ebff8bc0aff441d629acff9f2759b6af19`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @48 | `S(bookmarkname)@54` |

<a id="c-deletechar"></a>

### C_DeleteChar

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_DeleteChar.java`；SHA-256：`a3024d7b58c34ef6f3e1c197b4824710d56ffe0cf387ec4220e937249de1a628`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @82 | `S(name)@88` |

<a id="c-deleteinventoryitem"></a>

### C_DeleteInventoryItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_DeleteInventoryItem.java`；SHA-256：`807ec633ed1a198c44fdef61f29896c2f70592fbf192404256fbcba9ea748d7f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @36 | `D(itemObjectId)@43` |

<a id="c-deposit"></a>

### C_Deposit

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Deposit.java`；SHA-256：`5afaf8c9b51ca531a7a02e4985bc3e1857770c8253b948d4ef132a08a88d9b29`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @82 | `D(objid)@88` → `D(count)@89` |

<a id="c-disconnect"></a>

### C_Disconnect

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Disconnect.java`；SHA-256：`da4267eea102482b68152e0498da49fd30badeff1b3807909b3f5a085ca6e69b`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-door"></a>

### C_Door

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Door.java`；SHA-256：`34b3fc7271b49be0f5968acf3320c539fed750ccd23b8025de2aa27849f734a5`。

局部常數（subcode／格式旗標／限制）：`MAX_DISPATCH_RETRIES=8`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @128 | `H(未命名／忽略)@158` → `H(未命名／忽略)@159` → `D(objectId)@160` |

<a id="c-drawal"></a>

### C_Drawal

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Drawal.java`；SHA-256：`0357a9683398a1bd82f18cce9160f0ccb3d3b902835a143e10d5628cb95bab68`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @88 | `D(objectId)@116` → `D(count)@117` |

<a id="c-dropitem"></a>

### C_DropItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_DropItem.java`；SHA-256：`74e111eca656c740a59d0433c05698e734f699fd9199c006283484de1f553752`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @40 | `H(x)@45` → `H(y)@46` → `D(objectId)@47` → `D(count)@48` |

<a id="c-emblem"></a>

### C_Emblem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Emblem.java`；SHA-256：`4ea6cd5b26c72c4ac40d9dfb88a772352c0f9bbdc00c1af87930fa59e8c84415`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @81 | 〔`if clan_id > 0`〕 `Byte(iconByte)@119` |

<a id="c-enterportal"></a>

### C_EnterPortal

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_EnterPortal.java`；SHA-256：`5ce4b59f857d121e54062fa6f4fa8b33ad878f2ee375dd953ca1bbd10feb1277`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @71 | `H(locx)@99` → `H(locy)@100` |

<a id="c-exclude"></a>

### C_Exclude

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Exclude.java`；SHA-256：`35bb5412e2d0d77cf92360b9f2d3a62aa120de2da68d3b6b676ba609cc87f8ed`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @36 | `S(name)@41` |

<a id="c-exitghost"></a>

### C_ExitGhost

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ExitGhost.java`；SHA-256：`5e7a14c59b100dd3f09384fac731a2e4668d06ab0e53b5c1f7308b25351f1839`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-extracommand"></a>

### C_ExtraCommand

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ExtraCommand.java`；SHA-256：`5ac261f9461a33ccaa4f33e97e6f355408b59fb26fc8207852ec9b1e5f8dcc51`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @37 | `C(actionId)@60` |

<a id="c-fight"></a>

### C_Fight

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Fight.java`；SHA-256：`c2b6e5057d65b8c7e80f1552b4183361ac625b9fcb65c47f10ec836f275ff323`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-fishclick"></a>

### C_FishClick

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_FishClick.java`；SHA-256：`ca3b730d75d466ecf4ab451c5695089897802ddb6d7e6224b0ae078ae8b8d729`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-fixweaponlist"></a>

### C_FixWeaponList

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_FixWeaponList.java`；SHA-256：`cdd18397b303b5b1b103969a2bd8c02d5cabc6ae6a49844dd96aa420d99b3ad6`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-giveitem"></a>

### C_GiveItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_GiveItem.java`；SHA-256：`87446244c11ab3d1d41c52f83a4c98d8db08aaf1b51d47b30ef2d02666ebcc09`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @97 | `D(targetId)@148` → `H(未命名／忽略)@149` → `H(未命名／忽略)@150` → `D(itemObjId)@152` → `D(count)@153` |

<a id="c-hiresoldier"></a>

### C_HireSoldier

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_HireSoldier.java`；SHA-256：`616f62d6bca532fa3fe980520a9faf16c5ed6aadd76d172be5b6e105a6c27cee`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @32 | `H(未命名／忽略)@37` → `H(未命名／忽略)@38` → `D(未命名／忽略)@39` → `D(未命名／忽略)@40` → `H(未命名／忽略)@41` |

<a id="c-itemuse"></a>

### C_ItemUSe

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ItemUSe.java`；SHA-256：`9cba57e80f4e62f76ccfc461f12294ebc4e8428d74472765f3aba19a9bfd7959`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @123 | 委派 `useItemRequest(decrypt, client, 0, 0)@124` |
| `void useConfiguredInventoryItem(final ClientExecutor client, final int itemId, final int targetId)` @128 | 〔`if Config.CLIENT_LOGIN_TYPE == 2 && itemId > 0 && targetId > 0 → else`〕 委派 `useItemRequest(null, client, itemId, targetId)@133` |
| `private void useItemRequest(final byte[] decrypt, final ClientExecutor client, final int configuredItemId, final int configuredTargetId)` @212 | `D(未命名／忽略)@260`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case -8 → if isClass`〕 `C(newData[0])@481` → `C(newData[1])@482`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 5 → if isClass`〕 `D(newData[0])@578` → `H(newData[1])@579` → `H(newData[2])@580`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 6 → if isClass`〕 `H(newData[1])@597` → `D(newData[0])@598`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 7 → if isClass`〕 `D(未命名／忽略)@611`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 8 → if isClass`〕 `D(newData[0])@624`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 14 → if isClass`〕 `D(未命名／忽略)@657`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 16 → if isClass`〕 `S(cmd)@674`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 17 → if isClass`〕 `D(newData[0])@684` → `H(newData[1])@685` → `H(newData[2])@686`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 26, 27, 46 → if isClass`〕 `D(未命名／忽略)@700`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 28 → if isClass`〕 `C(newData[0])@713`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 29 → if isClass`〕 `H(newData[1])@731` → `D(newData[0])@733`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 30 → if isClass`〕 `D(obj)@745`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 12, 31, 33, 35 → if isClass`〕 `H(newData[0])@759` → `S(未命名／忽略)@760` → `Byte(未命名／忽略)@761`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 39 → if isClass`〕 `D(newData[0])@789` → `H(newData[1])@790` → `H(newData[2])@791`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 42 → if isClass`〕 `H(newData[0])@804` → `H(newData[1])@805`<br>〔`if pc.getCurrentHp() > 0 → switch use_type / case 55 → if isClass`〕 `D(未命名／忽略)@818` |

<a id="c-joinclan"></a>

### C_JoinClan

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_JoinClan.java`；SHA-256：`6a132cc69fae05b7a338f2772469689cdca119a326e75a2ebb9bf935348701af`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-keepalive"></a>

### C_KeepALIVE

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_KeepALIVE.java`；SHA-256：`d5c68dfc1d2711f9209a654bb6a0da3465360e03f0abafcfe75d3f2d2db3cd85`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-leaveclan"></a>

### C_LeaveClan

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_LeaveClan.java`；SHA-256：`6e4da42ff3cd31ae98cfa5558a5b273cfc3d95af1fa3390c1a20bfeb128848b6`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-leaveparty"></a>

### C_LeaveParty

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_LeaveParty.java`；SHA-256：`5508cc450de0fbc5a4a1a20d3b8b846163b36e1c70e429cf169205bf642dd1ff`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-logintoserver"></a>

### C_LoginToServer

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_LoginToServer.java`；SHA-256：`c2eedcb6d78d9da7f33af485c596d393d49818a0a05bc5a37cbedb79bcfc9de4`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @192 | `S(charName)@233` |

<a id="c-logintoserverok"></a>

### C_LoginToServerOK

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_LoginToServerOK.java`；SHA-256：`59461309d595f1118073a49cc338dce7a6c2fc9f034f1b3007063ba629c0d151`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @33 | `C(type)@38` → `C(button)@39` |

<a id="c-mail"></a>

### C_Mail

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Mail.java`；SHA-256：`c09c77c25764b894b715f3e84c5837f6e26dab61ae26e462607bbbc904001e38`。

局部常數（subcode／格式旗標／限制）：`TYPE_NORMAL_MAIL=0x00`；`TYPE_CLAN_MAIL=0x01`；`TYPE_MAIL_BOX=0x02`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @137 | `C(type)@142`<br>〔`switch type / case 0x12`〕 `D(id)@191`<br>〔`switch type / case 0x20`〕 `H(未命名／忽略)@194` → `S(receiverName)@195` → `Byte(textr)@196`<br>〔`switch type / case 0x21`〕 `H(未命名／忽略)@199` → `S(clanName)@200` → `Byte(text)@201`<br>〔`switch type / case 0x32`〕 `D(delid)@206`<br>〔`switch type / case 0x40`〕 `D(saveid)@209` |

<a id="c-movechar"></a>

### C_MoveChar

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_MoveChar.java`；SHA-256：`b569737e5fcda200291f803f41f4f9bddaa6cc6986c3869274934bcbc48a12d2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @151 | `H(locx)@170` → `H(locy)@171` → `C(heading)@172` |

<a id="c-newaccess"></a>

### C_NewAccess

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_NewAccess.java`；SHA-256：`d694f991e774169b0739fa935698bb26f36595813da14cbe4ea057a7d991c434`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @78 | `S(loginName)@84` → `S(password)@85` → `S(未命名／忽略)@98` → `S(spwd)@99` → `C(未命名／忽略)@101` → `S(未命名／忽略)@102` → `S(未命名／忽略)@103` → `S(未命名／忽略)@104` |

<a id="c-newcharselect"></a>

### C_NewCharSelect

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_NewCharSelect.java`；SHA-256：`6154a69a9cd1b074667b98c077d4c349043b9cdc61e16fdb36a108b16bb5b1e3`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-npcaction"></a>

### C_NPCAction

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_NPCAction.java`；SHA-256：`280ef4bb7316ca61e199ae97a71405ec45affc37a8b4955c4881a891b298b0a1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @209 | `D(objid)@243` → `S(s)@244`<br>〔`if obj instanceof L1NpcInstance → if remainingBytes() > 0 && tmp.getNpcTemplate().get_classname().equalsIgnoreCase("other.Npc_AuctionBoard")`〕 `S(s2)@518`<br>〔`if obj instanceof L1NpcInstance → if remainingBytes() > 0 && tmp.getNpcTemplate().get_classname().equalsIgnoreCase("other.Npc_Board")`〕 `S(s2)@520` |

<a id="c-npctalk"></a>

### C_NPCTalk

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_NPCTalk.java`；SHA-256：`7a324af75ce43054f87a500d6a16600ddf01209ee5c5d5d4d2f4cf1377457027`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @41 | `D(objid)@66` |

<a id="c-party"></a>

### C_Party

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Party.java`；SHA-256：`3a159744b4e70a125c12f7a73ecb67638473de73e6caecf834fc5a58a8062ee4`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-password"></a>

### C_Password

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Password.java`；SHA-256：`5f4f6b0042b734f5b36f5b8eb38ba5d84f81ad3b18231051c00ea84d82f4ebe0`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @85 | `C(mode)@91` → `D(olepwd)@92` → `D(newpwd)@93` |

<a id="c-petmenu"></a>

### C_PetMenu

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_PetMenu.java`；SHA-256：`ee4e63ee86f0a0122a2cdc87e8f0c106be2e8e1a6ba4c1da894487e3adbc8c9f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @38 | `D(petId)@61` |

<a id="c-pickupitem"></a>

### C_PickUpItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_PickUpItem.java`；SHA-256：`804b4ba715c505f458c63e78f6cfd81f03c65c618ff3a1b506ad7da7a0772254`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @45 | `H(x)@87` → `H(y)@88` → `D(objectId)@89` → `D(pickupCount)@90` |

<a id="c-pledge"></a>

### C_Pledge

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Pledge.java`；SHA-256：`aeacb3730a64876991c059f160899830d2bc3d71b430aa47f17296b000f2e04a`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-pledgecontent"></a>

### C_PledgeContent

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_PledgeContent.java`；SHA-256：`78c906fd729aacbcc33385249e963c9494d7a373f52c579f94c82b4089eca4d1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @72 | `C(data)@87`<br>〔`switch data / case 13`〕 `D(npcObjId)@92` → `D(itemObjId)@93` → `D(assistItemObjId)@94`<br>〔`switch data / case 14`〕 `D(npcObjId)@113` → `H(actionId)@114` → `D(plusItemObjId)@115` → `D(plusItemCount)@116`<br>〔`switch data / case 15`〕 `S(announce)@142`<br>〔`switch data / case 16`〕 `S(notes)@173` |

<a id="c-pledgerecommendation"></a>

### C_PledgeRecommendation

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_PledgeRecommendation.java`；SHA-256：`3b70b9fa2b3dba3d29918e176b21c04838b3a6fed227cc947adc946b64ea5850`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @100 | `C(data)@116`<br>〔`if data == 0`〕 `C(clanType)@140` → `S(typeMessage)@141`<br>〔`if data == 5`〕 `D(clan_id)@341`<br>〔`if data == 6`〕 `D(objid)@417` → `C(type)@418` |

<a id="c-propose"></a>

### C_Propose

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Propose.java`；SHA-256：`81d8dd895063670d40f03b4712423f13d06fbccf672b43fac1b25ee066edf70d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @36 | `C(c)@41` |

<a id="c-rank"></a>

### C_Rank

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Rank.java`；SHA-256：`0e284028348f160ed06a9241e0ec36ec5e4b0127f3f42dc2368773be76e17e14`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @84 | `C(data)@94`<br>〔`if data == 1`〕 `C(rank)@96` → `S(name)@97` |

<a id="c-restart"></a>

### C_Restart

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Restart.java`；SHA-256：`69e421abeb5fe0bf168d179ea9c3719afee6fc8e41d04cf9696da14afcbab7b8`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-result"></a>

### C_Result

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Result.java`；SHA-256：`4f616c8995cd1a43c6a1f4a133c98c68e6d686de0d89b05fcd910b86e155bae5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @285 | `D(npcObjectId)@316` → `C(resultType)@317` → `C(size)@318` → `C(未命名／忽略)@319`<br>〔`if resultType == 0 && size > 0 && pc.get_otherList().hasLottoBuyIntent() → for int i = 0; i < size; i++`〕 `D(orderId)@326` → `D(未命名／忽略)@327`<br>〔`if pc.getTempID() > 0 && resultType == 0 && npcObjectId == 71702`〕 委派 `mode_buy(pc, 71702, size)@337`<br>〔`if findObject != null → if findObject == pc && com.lineage.server.datatables.AuctionEventTable.get().isEnabled() && size == 1 && (resultType == 12 \|\| resultType == 0)`〕 `D(dataId)@357` → `D(count)@358`<br>〔`if findObject != null → if findObject instanceof L1NpcInstance → if npcId == 70535`〕 `D(dataId)@387` → `D(count)@388`<br>〔`switch resultType / case 0 → if size > 0 → if findObject instanceof L1MerchantInstance`〕 委派 `mode_buy(pc, npcId, size)@426`<br>〔`switch resultType / case 0 → if size > 0 → if findObject instanceof L1GamblingInstance`〕 委派 `mode_gambling(pc, npcId, size, true)@431`<br>〔`switch resultType / case 0 → if size > 0 → if findObject instanceof L1LottoInstance \|\| pc.get_otherList().hasLottoBuyIntent()`〕 委派 `mode_lotto(pc, npcId, size, true)@436`<br>〔`switch resultType / case 0 → if size > 0 → if findObject instanceof L1CnInstance`〕 委派 `mode_cn(pc, npcId, size, true)@441`<br>〔`switch resultType / case 0 → if size > 0 → if findObject instanceof L1DeInstance`〕 委派 `mode_buyde(pc, de, size)@447`<br>〔`switch resultType / case 0 → if size > 0 → if pc.equals(findObject)`〕 委派 `mode_cn(pc, 0, size, true)@452`<br>〔`switch resultType / case 0 → if size > 0 → if findObject instanceof L1PcInstance → if isPrivateShop`〕 委派 `mode_buypc(pc, targetPc, size)@459`<br>〔`switch resultType / default → if size > 0 → if findObject instanceof L1MerchantInstance → switch npcId / default`〕 委派 `mode_sell(pc, npcId, size)@471`<br>〔`switch resultType / default → if size > 0 → if findObject instanceof L1GamblingInstance`〕 委派 `mode_gambling(pc, npcId, size, false)@478`<br>〔`switch resultType / default → if size > 0 → if findObject instanceof L1LottoInstance`〕 委派 `mode_lotto(pc, npcId, size, false)@483`<br>〔`switch resultType / default → if size > 0 → if findObject instanceof L1DeInstance → if findObject instanceof L1DeInstance`〕 委派 `mode_sellde(pc, de, size)@490`<br>〔`switch resultType / default → if size > 0 → if findObject instanceof L1PcInstance → if isPrivateShop`〕 委派 `mode_sellpc(pc, targetPc, size)@498`<br>〔`switch resultType / case 2 → if size > 0 → if findObject instanceof L1DwarfInstance \|\| pcStorageContext → if level >= 5`〕 委派 `mode_warehouse_in(pc, npcId, size)@509`<br>〔`switch resultType / case 3 → if size > 0 → if findObject instanceof L1DwarfInstance \|\| pcStorageContext → if level >= 5`〕 委派 `mode_warehouse_out(pc, npcId, size)@520`<br>〔`switch resultType / case 4 → if size > 0 → if findObject instanceof L1DwarfInstance \|\| pcStorageContext → if level >= 5`〕 委派 `mode_warehouse_clan_in(pc, npcId, size)@531`<br>〔`switch resultType / case 5 → if size > 0 && (findObject instanceof L1DwarfInstance \|\| pcStorageContext) && pc.getLevel() >= 5`〕 委派 `mode_warehouse_clan_out(pc, npcId, size)@540`<br>〔`switch resultType / case 8 → if size > 0 → if findObject instanceof L1DwarfInstance → if (level >= 5) && pc.isElf()`〕 委派 `mode_warehouse_elf_in(pc, npcId, size)@562`<br>〔`switch resultType / case 9 → if size > 0 → if findObject instanceof L1DwarfInstance → if (level >= 5) && pc.isElf()`〕 委派 `mode_warehouse_elf_out(pc, npcId, size)@573`<br>〔`switch resultType / case 91143 → if size > 0 → switch npcId / case 91143`〕 委派 `mode_update_item(pc, size, npcObjectId)@585`<br>〔`switch resultType / case 17 → if size > 0 → if findObject instanceof L1DwarfInstance \|\| pcStorageContext → if level >= 5`〕 委派 `mode_warehouse_cha_in(pc, npcId, size)@622`<br>〔`switch resultType / case 18 → if size > 0 → if findObject instanceof L1DwarfInstance \|\| pcStorageContext → if level >= 5`〕 委派 `mode_warehouse_cha_out(pc, npcId, size)@633` |
| `private void mode_update_item(L1PcInstance pc, int size, int npcObjectId)` @648 | `D(orderId)@658` → `D(未命名／忽略)@659` |
| `private void mode_cn(final L1PcInstance pc, final int npcId, final int size, final boolean isShop)` @712 | 〔`if isShop → for int i = 0; i < size; i++`〕 `D(orderId)@719` → `D(未命名／忽略)@720` |
| `private void mode_gambling(final L1PcInstance pc, final int npcId, final int size, final boolean isShop)` @742 | 〔`if isShop → for int i = 0; i < size; i++`〕 `D(orderId)@753` → `D(未命名／忽略)@754`<br>〔`else → for int i = 0; i < size; i++`〕 `D(objid)@765` → `D(未命名／忽略)@766` |
| `private void mode_lotto(final L1PcInstance pc, final int npcId, final int size, final boolean isShop)` @779 | 〔`if isShop → for int i = 0; i < size; i++`〕 `D(orderId)@793` → `D(未命名／忽略)@794`<br>〔`else → for int i = 0; i < size; i++`〕 `D(objid)@803` → `D(未命名／忽略)@804` |
| `private void mode_warehouse_elf_out(final L1PcInstance pc, final int npcId, final int size)` @820 | 〔`for int i = 0; i < size; i++`〕 `D(objectIds[i])@827` → `D(未命名／忽略)@828` |
| `private void mode_warehouse_elf_in(final L1PcInstance pc, final int npcId, final int size)` @878 | 〔`for int i = 0; i < size; i++`〕 `D(objectIds[i])@885` → `D(未命名／忽略)@886` |
| `private void mode_warehouse_clan_out(final L1PcInstance pc, final int npcId, final int size)` @971 | 〔`for int i = 0; i < size; i++`〕 `D(objectIds[i])@978` → `D(未命名／忽略)@979` |
| `private void mode_warehouse_clan_in(final L1PcInstance pc, final int npcId, final int size)` @1047 | 〔`for int i = 0; i < size; i++`〕 `D(objectIds[i])@1054` → `D(未命名／忽略)@1055` |
| `private void mode_warehouse_cha_out(final L1PcInstance pc, final int npcId, final int size)` @1153 | 〔`for int i = 0; i < size; i++`〕 `D(objectIds[i])@1160` → `D(未命名／忽略)@1161` |
| `private void mode_warehouse_cha_in(final L1PcInstance pc, final int npcId, final int size)` @1211 | 〔`for int i = 0; i < size; i++`〕 `D(objectIds[i])@1222` → `D(未命名／忽略)@1223` |
| `private void mode_warehouse_out(final L1PcInstance pc, final int npcId, final int size)` @1312 | 〔`for int i = 0; i < size; i++`〕 `D(objectId)@1318` → `D(未命名／忽略)@1319` |
| `private void mode_warehouse_in (final L1PcInstance pc, final int npcId, final int size)` @1349 | 〔`for int i = 0; i < size; i++`〕 `D(objectIds[i])@1356` → `D(未命名／忽略)@1357` |
| `private void mode_sellde(L1PcInstance pc, L1DeInstance de, int size)` @1445 | 委派 `modeSellDeLocked(pc, de, size)@1446` |
| `private void modeSellDeLocked(L1PcInstance pc, L1DeInstance de, int size)` @1449 | 〔`for int i = 0; i < size; i++`〕 `D(itemObjectId)@1463` → `CH(count)@1464` |
| `private void mode_buyde(L1PcInstance pc, L1DeInstance de, int size)` @1545 | 委派 `modeBuyDeLocked(pc, de, size)@1546` |
| `private void modeBuyDeLocked(L1PcInstance pc, L1DeInstance de, int size)` @1549 | 〔`for int i = 0; i < size; i++`〕 `D(order)@1563` → `D(未命名／忽略)@1564` |
| `private void mode_sellpc(final L1PcInstance pc, final L1PcInstance targetPc, final int size)` @1657 | 〔`for int i = 0; i < size; i++`〕 `D(itemObjectId)@1702` → `CH(count)@1703` → `C(order)@1709` |
| `private void mode_buypc(final L1PcInstance pc, final L1PcInstance targetPc, final int size)` @1862 | 〔`for int i = 0; i < finalSize; i++`〕 `D(orders[i])@1874` → `D(未命名／忽略)@1875` |
| `private void mode_sell(final L1PcInstance pc, final int npcId, final int size)` @2139 | 〔`for int i = 0; i < size; i++`〕 `D(objids[i])@2146` → `D(未命名／忽略)@2147` |
| `private void mode_buy(final L1PcInstance pc, final int npcId, final int size)` @2191 | 〔`for int i = 0; i < size; i++`〕 `D(orderIds[i])@2198` → `D(未命名／忽略)@2199` |

<a id="c-returntologin"></a>

### C_ReturnToLogin

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ReturnToLogin.java`；SHA-256：`47bda279765fe5b92f50c6d7d55248a05a6b5052b4515f47b94c16f25e47eb85`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-selectlist"></a>

### C_SelectList

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_SelectList.java`；SHA-256：`49532fe36d8bb990cee9fb230845f33cfd813085e8e7f33e1881a411799f28c4`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @97 | `D(itemObjectId)@133` → `D(npcObjectId)@134` |

<a id="c-selecttarget"></a>

### C_SelectTarget

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_SelectTarget.java`；SHA-256：`d6ac219ee87d0a89762db9efb799d23e22d65374bfbf82b91d245c91510ec141`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @44 | `D(petId)@49` → `C(未命名／忽略)@50` → `D(targetId)@51` |

<a id="c-serverversion"></a>

### C_ServerVersion

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ServerVersion.java`；SHA-256：`2a88e34cfa5f7a097274e7cd1c6de50c7f966cf2595f05de436bcf4313f39e1d`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-ship"></a>

### C_Ship

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Ship.java`；SHA-256：`1be305fce6d3859627dc6ae189a6c97924997692a394eff81ca49c1e2faed0e2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @29 | 〔`if item_id != 0`〕 `H(shipMapId)@67` → `H(locX)@68` → `H(locY)@69` |

<a id="c-shop"></a>

### C_Shop

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Shop.java`；SHA-256：`cd94e74004c18471e01faf290e8f2320193fb2fcafadc94998fbcedc288b9bff`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @46 | `C(type)@58`<br>〔`if type == 0`〕 `H(sellTotalCount)@130`<br>〔`if type == 0 → for int i = 0; i < sellTotalCount; i++`〕 `D(sellObjectId)@139` → `D(未命名／忽略)@140` → `D(未命名／忽略)@145`<br>〔`if type == 0`〕 `H(buyTotalCount)@209`<br>〔`if type == 0 → for int i = 0; i < buyTotalCount; i++`〕 `D(buyObjectId)@218` → `D(未命名／忽略)@219` → `D(未命名／忽略)@226`<br>〔`if type == 0`〕 `Byte(chat)@317` |

<a id="c-shoplist"></a>

### C_ShopList

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_ShopList.java`；SHA-256：`1aef96d5889fa3590767e99287782e8f06cf7de496272b4da12e48d0197b37c5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @34 | `C(type)@90` → `D(objectId)@91` |

<a id="c-skillbuy"></a>

### C_SkillBuy

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_SkillBuy.java`；SHA-256：`ce89743248e65b2941c02bcc20f50610e442fa2364fbf7ecd75565cee8bc5111`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @85 | `D(objid)@121` |

<a id="c-skillbuyitem"></a>

### C_SkillBuyItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_SkillBuyItem.java`；SHA-256：`d54fa4c4cb1dcd6271dbfa494a6c4edf35465b93954efac79c6d7e9b93b433c3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @37 | `D(objid)@61` |

<a id="c-skillbuyitemok"></a>

### C_SkillBuyItemOK

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_SkillBuyItemOK.java`；SHA-256：`e711e91ea6779a716c401315ef2fb0f1902bc7866b09bcc8306601a09fba9b00`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @81 | `H(count)@151`<br>〔`for int i = 0; i < count; i++`〕 `D(skillIds[i])@161` |

<a id="c-skillbuyok"></a>

### C_SkillBuyOK

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_SkillBuyOK.java`；SHA-256：`b8e4f9c9d7a35d87f2849b6f074143323740318f9cdb8d6ca6ac3dcbe2a0765a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @112 | `H(count)@152`<br>〔`for int i = 0; i < count; i++`〕 `D(skillIds[i])@195` |

<a id="c-taxrate"></a>

### C_TaxRate

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_TaxRate.java`；SHA-256：`44e4581780d25dd6d22b46cbf0dbd3028f749fb3d244cdbea0b968e0ceb7451f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @82 | `D(i)@88` → `C(j)@89` |

<a id="c-teleport"></a>

### C_Teleport

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Teleport.java`；SHA-256：`0080261575be9445aedc3d98059937933a82ac4761fc2dbff79b87e499b5ef04`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-title"></a>

### C_Title

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Title.java`；SHA-256：`6ef78748cd0a63d1706eac0fa279c922c34cf19f1e5b66640d1cc9d6603d3b5b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @39 | `S(charName)@45` → `S(未命名／忽略)@48` |

<a id="c-trade"></a>

### C_Trade

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Trade.java`；SHA-256：`6466b1c760cbd96da6e1e417fd411557e2b3c781bfd7ff8408447822b4b529ab`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-tradeadditem"></a>

### C_TradeAddItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_TradeAddItem.java`；SHA-256：`668c3c26f33fa0b99d2bafe6dec17cf7a2a1a5ca39cf07c2dc2a4a3039825e71`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @39 | `D(itemObjid)@59` → `D(itemcount)@61` |

<a id="c-tradecancel"></a>

### C_TradeCancel

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_TradeCancel.java`；SHA-256：`40c908a4a606c82917ee448e837abd8c0d55f06198e6b48bf42a2e77cee730a4`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-tradeok"></a>

### C_TradeOK

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_TradeOK.java`；SHA-256：`af68f2ecc61d45b77b1579ad7325440f3d637cafd42a465bf6d22fdf74c5e749`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-unkonwn"></a>

### C_Unkonwn

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Unkonwn.java`；SHA-256：`4a2dc70d522b2cc5641703a7243737b52cb3159fbd227730c14d3792c9c9de82`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-unlock"></a>

### C_UnLock

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_UnLock.java`；SHA-256：`996c01991ea075eb50ade8dcd1f416e4fed699c19c9c3c56a4bef5df103cf261`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @34 | `C(type)@39` |

<a id="c-usepetitem"></a>

### C_UsePetItem

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_UsePetItem.java`；SHA-256：`4f31c87a0ae456c320c12a99d67504bb44217d5c94c4d1bcfdc4f61f4c8cc1b1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @89 | `C(data)@95` → `D(petId)@96` → `C(listNo)@97` |

<a id="c-useskill"></a>

### C_UseSkill

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_UseSkill.java`；SHA-256：`4932c6dd71b330d91b75c4fcbf02c30ffa4712c35d7cb07537626159038e168e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @52 | `C(row)@106` → `C(column)@107`<br>〔`if decrypt.length > 4 → switch skillId / case 43`〕 `D(targetId)@203`<br>〔`if decrypt.length > 4 → switch skillId / case 150`〕 `D(targetId)@216`<br>〔`if decrypt.length > 4 → switch skillId / case 186`〕 `D(targetId)@225`<br>〔`if decrypt.length > 4 → switch skillId / case 118`〕 `S(charName)@235`<br>〔`if decrypt.length > 4 → switch skillId / case 113`〕 `D(targetId)@244` → `H(targetX)@245` → `H(targetY)@246` → `S(requestedTrueTargetText)@248`<br>〔`if decrypt.length > 4 → switch skillId / case 69`〕 `H(未命名／忽略)@260` → `D(targetId)@261`<br>〔`if decrypt.length > 4 → switch skillId / case 63`〕 `H(targetX)@271` → `H(targetY)@272`<br>〔`if decrypt.length > 4 → switch skillId / case 51 → if (pc.getInventory().checkEquipped(20284)) \|\| (pc.getInventory().checkEquipped(120284))`〕 `D(requestedSummonId)@280`<br>〔`if decrypt.length > 4 → switch skillId / case 51 → else`〕 `D(未命名／忽略)@287`<br>〔`if decrypt.length > 4 → switch skillId / case 213`〕 `D(targetId)@322` → `H(targetX)@323` → `H(targetY)@324`<br>〔`if decrypt.length > 4 → switch skillId / case 219`〕 `D(targetId)@398`<br>〔`if decrypt.length > 4 → switch skillId / case 220`〕 `D(targetId)@486`<br>〔`if decrypt.length > 4 → switch skillId / case 88 → if pc.getInventory().getTypeEquipped(2, 7) >= 1`〕 `D(targetId)@497`<br>〔`if decrypt.length > 4 → switch skillId / case 91 → if pc.getInventory().getTypeEquipped(1, 3) >= 1`〕 `D(targetId)@510`<br>〔`if decrypt.length > 4 → switch skillId / default`〕 `D(targetId)@521` |

<a id="c-war"></a>

### C_War

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_War.java`；SHA-256：`733980808f998f818a52802b3cb8d36c6e556d7f115c2b84814040e43906fed0`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @83 | `C(type)@126` → `S(tgname)@127` |

<a id="c-webwindow"></a>

### C_WebWindow

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_WebWindow.java`；SHA-256：`205d0cb1c9a54c171279500e25d54903a05fca10d2cdf2a2bb212044ef0f8d93`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="c-who"></a>

### C_Who

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Who.java`；SHA-256：`862ccf400889bba1d73d9ac956abd2eeffeb9db5ebf7bb807283b616aab575c0`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @51 | `S(s)@56` |

<a id="c-windows"></a>

### C_Windows

來源：`1-伺服器/src/com/lineage/server/clientpackets/C_Windows.java`；SHA-256：`2cb7e592dc2a449dc6421c2242d203ca11ca08ee63af8bb30e2d5cd323f299f0`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public void start(final byte[] decrypt, final ClientExecutor client)` @77 | `C(type)@92`<br>〔`switch type / case 0x00`〕 `D(objid)@125`<br>〔`switch type / case 0x0b`〕 `S(name)@141` → `H(mapid)@146` → `H(x)@147` → `H(y)@148` → `D(zone)@149`<br>〔`switch type / case 0x2e`〕 `C(emblemStatus)@168` |

## S 欄位與方法索引

243 個 S_ 來源檔全部列出，含空殼／停用／繼承類別。Byte1(status) 等不透明內嵌資料的長度在前置 C/H/D 欄位或陣列參數中；物品 status bytes 的內部標記由 `Instance/L1ItemStatus.getStatusBytes` 產生，不能用封包總長猜單一固定物品格式。

<a id="s-ability"></a>

### S_Ability

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Ability.java`；SHA-256：`1d7aeeb271e5f288b1fc6911e8895c7908d5048c7f317b971bcd7297d7fbaac2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Ability(final int type, final boolean equipped)` @17 | 委派 `buildPacket(type, equipped)@18` |
| `private void buildPacket(final int type, final boolean equipped)` @21 | `C(S_OPCODE_ABILITY)@22` → `C(type)@23` → `C(equipped? 0x01 : 0x00)@24` |

<a id="s-additem"></a>

### S_AddItem

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AddItem.java`；SHA-256：`cde671d3b9de5f57bbd68bf5b2ef8d315bb196e49575d8fe8d4d966534d7dfa2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AddItem(final L1ItemInstance item)` @18 | `C(S_OPCODE_ADDITEM)@19` → `D(item.getId())@20` → `H(item.getItem().getPacketItemDescId())@21` → `C(item.getItem().getPacketUseType())@22`<br>〔`if item.getChargeCount() > 0`〕 `C(item.getChargeCount())@24`<br>`C(0)@26` → `H(item.get_gfxid())@27` → `C(item.isSealed() ? L1Item.toPacketBless(item.getBless()) + 128 : L1Item.toPacketBless(item.getBless()))@29` → `D((int)Math.min(item.getCount(), 2000000000))@30` → `C(statusX)@55` → `S(item.getViewName())@56`<br>〔`if !item.isIdentified()`〕 `C(0)@58`<br>〔`if S_ItemStatus.shouldUseLongStatus(item)`〕 `C(0)@60`<br>〔`else`〕 `C(status.length)@63`<br>〔`else → for int i = 0; i < j; i++`〕 `C(b)@68`<br>`C(10)@71` → `H(0)@72` → `D(0)@73` → `D(0)@74` |

<a id="s-addskill"></a>

### S_AddSkill

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AddSkill.java`；SHA-256：`13113864ecbe5af003216de30969abb8d1fc5515e961dbff32b2b24516fe0bdb`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AddSkill(final L1PcInstance pc, final int skillid)` @21 | `C(S_OPCODE_ADDSKILL)@29` → `C(skill_list_values)@30`<br>〔`for final int element : skillList`〕 `C(element)@33` |
| `public S_AddSkill(L1PcInstance pc, int[] skills)` @47 | `C(S_OPCODE_ADDSKILL)@52`<br>〔`if (i6 > 0) && (j6 == 0)`〕 `C(0x32)@55`<br>〔`if j6 > 0`〕 `C(0x64)@58`<br>〔`else`〕 `C(0x20)@61`<br>`C(skills[0])@64` → `C(skills[1])@65` → `C(skills[2])@66` → `C(skills[3])@67` → `C(skills[4])@68` → `C(skills[5])@69` → `C(skills[6])@70` → `C(skills[7])@71` → `C(skills[8])@72` → `C(skills[9])@73` → `C(skills[10])@75` → `C(skills[11])@76` → `C(skills[12])@78` → `C(skills[13])@79` → `C(skills[14])@81` → `C(skills[15])@83` → `C(skills[16])@85` → `C(skills[17])@86` → `C(skills[18])@87` → `C(skills[19])@88` → `C(skills[20])@89` → `C(skills[21])@90` → `C(skills[22])@92` → `C(skills[23])@93` → `C(skills[24])@94` → `C(skills[25])@96` → `C(skills[26])@97` → `C(skills[27])@98` |
| `public S_AddSkill(final L1PcInstance pc, final int level1, final int level2, final int level3, final int level4, final int level5, final int level6, final int level7, final int level8, final int level9, final int level10, final int knight1, final int knight2, final int de1, final int de2, final int royal, final int un, final int elf1, final int elf2, final int elf3, final int elf4, final int elf5, final int elf6, final int k1, final int k2, final int k3, final int l1, final int l2, final int l3 )` @205 | `C(S_OPCODE_ADDSKILL)@219`<br>〔`if (i6 > 0) && (j6 == 0)`〕 `C(0x32)@221`<br>〔`if j6 > 0`〕 `C(0x64)@224`<br>〔`else`〕 `C(0x20)@227`<br>`C(level1)@230` → `C(level2)@231` → `C(level3)@232` → `C(level4)@233` → `C(level5)@234` → `C(level6)@235` → `C(level7)@236` → `C(level8)@237` → `C(level9)@238` → `C(level10)@239` → `C(knight1)@241` → `C(knight2)@242` → `C(de1)@244` → `C(de2)@245` → `C(royal)@247` → `C(un)@249` → `C(elf1)@251` → `C(elf2)@252` → `C(elf3)@253` → `C(elf4)@254` → `C(elf5)@255` → `C(elf6)@256` → `C(k1)@258` → `C(k2)@259` → `C(k3)@260` → `C(l1)@262` → `C(l2)@263` → `C(l3)@264` |
| `public S_AddSkill(L1PcInstance pc, int mode, int dx)` @334 | `C(S_OPCODE_ADDSKILL)@337` → `C(skill_list_values)@338`<br>〔`for int i = 0 ; i < 28 ; i++ → if i == mode`〕 `C(dx)@342`<br>〔`for int i = 0 ; i < 28 ; i++ → else`〕 `C(0x00)@345` |

<a id="s-applyauction"></a>

### S_ApplyAuction

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ApplyAuction.java`；SHA-256：`0779eaa0b9f471808a941e39bb7a8bc4a56dbbb1fd942ce0fbb3eebbbe53a53f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ApplyAuction(final int objectId, final String houseNumber)` @29 | 委派 `buildPacket(objectId, houseNumber)@30` |
| `private void buildPacket(final int objectId, final String houseNumber)` @34 | `C(S_OPCODE_INPUTAMOUNT)@37` → `D(objectId)@38` → `D(0x00000000)@39` → `D((int) minimum)@43` → `D((int) minimum)@44` → `D(0x77359400)@46` → `H(0x0000)@47` → `S("agapply")@48` → `S("agapply " + houseNumber)@49` |

<a id="s-attackpacketnpc"></a>

### S_AttackPacketNpc

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AttackPacketNpc.java`；SHA-256：`aa612c0c22d3ca9dd6373ce170433f73cc8be3c1cb029edc8a1f7b53e656889d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AttackPacketNpc(final L1NpcInstance npc, final L1Character target, final int type, final int dmg )` @23 | `C(S_OPCODE_ATTACKPACKET)@38` → `C(type)@39` → `D(npc.getId())@40` → `D(HitReactionSuppressTable.get().resolveTargetObjId(target.getId(), target))@41`<br>〔`if com.lineage.config.ConfigClient.isROClient()`〕 `H(dmg)@47`<br>〔`else`〕 `H(dmg > 0 ? 0x0a : 0x00)@49`<br>`C(npc.getHeading())@52` → `H(0x00)@54` → `H(0x00)@55` → `C(0x00)@57` |
| `public S_AttackPacketNpc(final L1NpcInstance npc, final int targetid, final int type, final int dmg )` @68 | `C(S_OPCODE_ATTACKPACKET)@73` → `C(type)@74` → `D(npc.getId())@75` → `D(HitReactionSuppressTable.get().resolveTargetObjId(targetid))@76`<br>〔`if com.lineage.config.ConfigClient.isROClient()`〕 `H(dmg)@82`<br>〔`else`〕 `H(dmg > 0 ? 0x0a : 0x00)@84`<br>`C(npc.getHeading())@87` → `H(0x00)@89` → `H(0x00)@90` → `C(0x00)@92` |
| `public S_AttackPacketNpc(final L1NpcInstance npc, final L1Character target, final int type )` @102 | `C(S_OPCODE_ATTACKPACKET)@106` → `C(type)@107` → `D(npc.getId())@108` → `D(HitReactionSuppressTable.get().resolveTargetObjId(target.getId(), target))@109` → `H(0x00)@110` → `C(npc.getHeading())@111` → `H(0x00)@113` → `H(0x00)@114` → `C(0x00)@116` |

<a id="s-attackpacketpc"></a>

### S_AttackPacketPc

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AttackPacketPc.java`；SHA-256：`8b1cf5a0e0cb00183dd2803b27161f6b8cc64a668b9a0ee761034e8d937bc3a1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AttackPacketPc(final L1PcInstance pc, final L1Character target, final int type, final int dmg)` @24 | `C(S_OPCODE_ATTACKPACKET)@38` → `C(ActionCodes.ACTION_Attack)@39` → `D(pc.getId())@40` → `D(HitReactionSuppressTable.get().resolveTargetObjId(target.getId(), target))@41` → `damageWordU16(this, dmg)@44` → `C(pc.getHeading())@46` → `H(0x00)@48` → `H(0x00)@49` → `C(type)@50` |
| `public S_AttackPacketPc(final L1PcInstance pc, final L1Character target)` @59 | `C(S_OPCODE_ATTACKPACKET)@61` → `C(ActionCodes.ACTION_Attack)@62` → `D(pc.getId())@63` → `D(HitReactionSuppressTable.get().resolveTargetObjId(target.getId(), target))@64` → `H(0x00)@65` → `C(pc.getHeading())@66` → `H(0x00)@68` → `H(0x00)@69` → `C(0x00)@70` |
| `public S_AttackPacketPc(final L1PcInstance pc)` @78 | `C(S_OPCODE_ATTACKPACKET)@79` → `C(ActionCodes.ACTION_Attack)@80` → `D(pc.getId())@81` → `D(0x00)@82` → `H(0x00)@83` → `C(pc.getHeading())@84` → `H(0x00)@86` → `H(0x00)@87` → `C(0x00)@88` |

<a id="s-auctionboard"></a>

### S_AuctionBoard

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AuctionBoard.java`；SHA-256：`5f752fc7f7a91a8787125f56643a60f608152452f044c9cbeee752d48690c5ee`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AuctionBoard(final L1NpcInstance board)` @27 | 委派 `buildPacket(board)@29` |
| `private void buildPacket(final L1NpcInstance board)` @33 | `C(S_OPCODE_HOUSELIST)@57` → `D(board.getId())@59` → `H(houseListX.size())@60`<br>〔`for final L1AuctionBoardTmp boardX : houseListX`〕 `D(boardX.getHouseId())@62` → `S(boardX.getHouseName())@63` → `H(boardX.getHouseArea())@64` → `C(cal.get(Calendar.MONTH) + 1)@66` → `C(cal.get(Calendar.DATE))@67` → `D((int) boardX.getPrice())@68` |

<a id="s-auctionboardread"></a>

### S_AuctionBoardRead

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AuctionBoardRead.java`；SHA-256：`0bc92c0a00302b8e5d4e67aac10a983fdeafec0952bef0688b502a2acb04da27`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AuctionBoardRead(final int objectId, final String house_number)` @17 | 委派 `buildPacket(objectId, house_number)@18` |
| `private void buildPacket(final int objectId, final String house_number)` @22 | `C(S_OPCODE_SHOWHTML)@25` → `D(objectId)@26` → `S("agsel")@27` → `S(house_number)@28` → `H(0x0009)@29` → `S(board.getHouseName())@30` → `S(board.getLocation() + "$1195")@31` → `S(String.valueOf(board.getHouseArea()))@32` → `S(board.getOldOwner())@33` → `S(board.getBidder())@34` → `S(String.valueOf(board.getPrice()))@35` → `S(String.valueOf(month))@40` → `S(String.valueOf(day))@41` → `S(String.valueOf(hour))@42` |

<a id="s-auctionshopretrieve"></a>

### S_AuctionShopRetrieve

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AuctionShopRetrieve.java`；SHA-256：`826c8a79d252c470712e03de66c1937c233a606ac3fce66f779231befd324010`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AuctionShopRetrieve(final int objid, final L1PcInstance pc)` @31 | `C(S_OPCODE_SHOWRETRIEVELIST)@51` → `D(objid)@52` → `H(itemList.size())@53` → `C(0x0c)@54`<br>〔`for final L1ItemInstance item : itemList`〕 `D(item.getId())@57` → `C(i)@62` → `H(item.get_gfxid())@63` → `C(L1Item.toPacketBless(item.getBless()))@64` → `D((int) Math.min(item.getCount(), 2000000000))@65` → `C(item.isIdentified() ? 0x01 : 0x00)@66` → `S(item.getViewName())@67`<br>`D(terms.fee())@71` |

<a id="s-auctionshopselllist"></a>

### S_AuctionShopSellList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_AuctionShopSellList.java`；SHA-256：`5137ffcfeca894229fbbc9c350bca84089ce82a703129888a25ed09178c0efd6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_AuctionShopSellList(final int objid, final L1PcInstance pc)` @30 | 委派 `buildPacket(objid, filteredList)@41` |
| `public S_AuctionShopSellList(final int objid, final List<AuctionShopItem> filteredList)` @47 | 委派 `buildPacket(objid, filteredList)@48` |
| `private void buildPacket(final int objid, final List<AuctionShopItem> filteredList)` @54 | `C(S_OPCODE_SHOWSHOPBUYLIST)@59` → `D(objid)@60`<br>〔`if filteredList == null \|\| filteredList.isEmpty()`〕 `H(0x0000)@63`<br>`H(filteredList.size())@67`<br>〔`for final AuctionShopItem auction : filteredList`〕 `D(i)@72` → `H(gfx)@80` → `D(auction.getPrice())@81` → `S(nameInfo)@100`<br>〔`for final AuctionShopItem auction : filteredList → if ConfigOther.SHOPINFO && template != null → if tempItem != null`〕 `C(status.length)@136`<br>〔`for final AuctionShopItem auction : filteredList → if ConfigOther.SHOPINFO && template != null → if tempItem != null → for final byte b : status`〕 `C(b)@138`<br>〔`for final AuctionShopItem auction : filteredList → if ConfigOther.SHOPINFO && template != null → else`〕 `C(0x00)@141`<br>〔`for final AuctionShopItem auction : filteredList → if ConfigOther.SHOPINFO && template != null`〕 `C(0x00)@144`<br>〔`for final AuctionShopItem auction : filteredList → else`〕 `C(0x00)@147`<br>`H(0x0007)@153` |

<a id="s-bluemessage"></a>

### S_BlueMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_BlueMessage.java`；SHA-256：`9cc33bd5ff77514c36645b70bda07bc05925a3947d3a724ab5bd334f7002cf5b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_BlueMessage(final int type)` @16 | 委派 `buildPacket(type, null)@17` |
| `public S_BlueMessage(final int type, final String msg1)` @26 | 委派 `buildPacket(type, new String[]{ msg1 })@27` |
| `public S_BlueMessage(final int type, final String msg1, final String msg2)` @37 | 委派 `buildPacket(type, new String[]{ msg1, msg2 })@38` |
| `public S_BlueMessage(final int type, final String[] info)` @47 | 委派 `buildPacket(type, info)@48` |
| `private void buildPacket(final int type, final String[] info)` @52 | `C(S_OPCODE_REDMESSAGE)@53` → `H(type)@54`<br>〔`if info == null`〕 `C(0x00)@56`<br>〔`else`〕 `C(info.length)@59`<br>〔`else → for int i = 0 ; i < info.length ; i++`〕 `S(info[i])@61` |

<a id="s-board"></a>

### S_Board

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Board.java`；SHA-256：`d7e83c0bbf72d9084f267af9b22a47bc220db72d48e010708de4b58e21f42537`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Board(final L1NpcInstance npc)` @19 | 委派 `buildPacket(npc, 0)@20` |
| `public S_Board(final L1NpcInstance npc, final int number)` @29 | 委派 `buildPacket(npc, number)@30` |
| `private void buildPacket(final L1NpcInstance npc, final int number)` @34 | `C(S_OPCODE_BOARD)@37` → `C(0x00)@38` → `D(npc.getId())@39` → `C(0xff)@40` → `C(0xff)@41` → `C(0xff)@42` → `C(0x7f)@43` → `H(showList.size())@44` → `H(0x012c)@45`<br>〔`for int i = 0 ; i < showList.size() ; i++`〕 `D(boardInfo.id())@48` → `S(boardInfo.name())@49` → `S(boardInfo.date())@50` → `S(boardInfo.title())@51` |
| `public S_Board(final int objectid)` @59 | `C(S_OPCODE_BOARD)@60` → `C(0x00)@61` → `D(objectid)@62` → `C(0xFF)@63` → `C(0xFF)@64` → `C(0xFF)@65` → `C(0x7F)@66` → `H(2)@67` → `H(300)@68` → `D(2)@70` → `S("佈告欄列表")@71` → `S("2010-2-2")@72` → `S("佈告欄列表TITLE")@73` → `D(1)@75` → `S("佈告欄列表2")@76` → `S("2010-2-3")@77` → `S("佈告欄列表TITLE2")@78` |

<a id="s-boardread"></a>

### S_BoardRead

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_BoardRead.java`；SHA-256：`2a8da353ad4f0aa287b131ca823006c97edc2bea7b1f61ab1a623f0746930490`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_BoardRead(final int number)` @19 | 委派 `buildPacket(number)@20` |
| `private void buildPacket(final int number)` @24 | `C(S_OPCODE_BOARDREAD)@26` → `D(board.get_id())@27` → `S(board.get_name())@28` → `S(board.get_date())@29` → `S(board.get_title())@30` → `S(board.get_content())@31` |
| `public S_BoardRead()` @38 | `C(S_OPCODE_BOARDREAD)@39` → `D(10)@40` → `S("測試NAME")@41` → `S("2010-02-02")@42` → `S("測試TITLE")@43` → `S("測試內容")@44` |

<a id="s-bonusstats"></a>

### S_Bonusstats

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Bonusstats.java`；SHA-256：`a6b745f35c42a048b5fc9928d3bac04779eb3d38c3bf30a87a5cb881108d15a4`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Bonusstats(final int objid)` @16 | 委派 `buildPacket(objid)@17` |
| `private void buildPacket(final int objid)` @21 | `C(S_OPCODE_SHOWHTML)@22` → `D(objid)@23` → `S("RaiseAttr")@24` |

<a id="s-bookmarklist"></a>

### S_BookmarkList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_BookmarkList.java`；SHA-256：`f2421a346fc320248c166d7e2342c0374037b6652174beacd4cd9839a4556c00`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_BookmarkList(List<L1BookMark> bookmarks)` @37 | `C(S_OPCODE_CHARRESET)@38` → `C(0x2a)@39` → `H(0x0080)@40` → `C(0x02)@41`<br>〔`for int i = 1; i < 128; ++i`〕 `C(0xff)@44`<br>`H(BookmarkService.MAX_BOOKMARKS)@47` → `H(bookmarks.size())@48`<br>〔`for L1BookMark book : bookmarks`〕 `D(book.getId())@51` → `S(book.getName())@52` → `H(book.getMapId())@53` → `H(book.getLocX())@54` → `H(book.getLocY())@55` |

<a id="s-bookmarks"></a>

### S_Bookmarks

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Bookmarks.java`；SHA-256：`749e8529cb083c13715ccf7e8858775ce647bd223e365692c899a296ac76b0e9`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Bookmarks(final String name, final int map, final int x, final int y, final int id)` @40 | `C(S_OPCODE_BOOKMARKS)@44` → `S(name)@45` → `H(map)@46` → `H(x)@47` → `H(y)@48` → `D(id)@49` |

<a id="s-boxchoice"></a>

### S_BoxChoice

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_BoxChoice.java`；SHA-256：`a49436ec84d5d797accfcbb4fc9433b39ac26d3da95258d7eaf7f2bb9bd7f2df`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_BoxChoice(int boxObjId, L1BoxSelect box)` @19 | `C(S_OPCODE_SHOWSHOPBUYLIST)@21` → `D(boxObjId)@22`<br>〔`if size <= 0`〕 `H(0x0000)@26`<br>`H(size)@30`<br>〔`for int i = 0; i < size; i++`〕 `D(i)@42` → `H(template.getGfxId())@45` → `D(0)@48` → `S(name.toString())@59`<br>〔`for int i = 0; i < size; i++ → if status != null && status.length > 0`〕 `C(status.length)@77`<br>〔`for int i = 0; i < size; i++ → if status != null && status.length > 0 → for byte b : status`〕 `C(b)@79`<br>〔`for int i = 0; i < size; i++ → else`〕 `C(0x00)@82`<br>`H(0x0007)@88` |

<a id="s-boxmessage"></a>

### S_BoxMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_BoxMessage.java`；SHA-256：`49d4e3c56cb01e091a8f05450662dc613a3d7521a2631b9fe07439214561b742`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_BoxMessage(final String msg)` @16 | `C(S_OPCODE_NPCSHOUT)@17` → `C(0x00)@18` → `D(0x00000000)@19` → `S(msg)@20` |

<a id="s-boxpack"></a>

### S_BoxPack

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_BoxPack.java`；SHA-256：`ce5efed8e93b46af4f2c27a25378538df2b36164f3b7f45e6f0b073cebb1a09c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_BoxPack(final L1NpcBoxInstance box)` @15 | 委派 `buildPacket(box)@16` |
| `private void buildPacket(final L1NpcBoxInstance box)` @19 | `C(S_OPCODE_CHARPACK)@20` → `H(box.getX())@21` → `H(box.getY())@22` → `D(box.getId())@23` → `H(box.getGfxId())@24` → `C(box.getOpenStatus())@25` → `C(0x00)@26` → `C(0x00)@27` → `C(0x00)@28` → `D(0x00000001)@29` → `H(0x0000)@30` → `S(null)@31` → `S(null)@32` → `C(0x00)@33` → `D(0x00000000)@34` → `S(null)@35` → `S(null)@36` → `C(0x00)@38` → `C(0xff)@40` → `C(0x00)@41` → `C(0x00)@42` → `C(0x00)@43` → `C(0xff)@44` → `C(0xff)@45` → `C(0x00)@46` |

<a id="s-broadcastpos"></a>

### S_BroadcastPos

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_BroadcastPos.java`；SHA-256：`507d4bfa71ea4fa28c978bb9c2c5be8a5dd155284b2f5159fc71942bbe560c65`。

局部常數（subcode／格式旗標／限制）：`CUSTOM_MARKER=0xFF`；`SUB_BROADCAST_POS=0x08`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_BroadcastPos(final int posIdx)` @18 | `C(S_OPCODE_PACKETBOX)@19` → `C(CUSTOM_MARKER)@20` → `C(SUB_BROADCAST_POS)@21` → `C(posIdx & 0x07)@22` |

<a id="s-buddy"></a>

### S_Buddy

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Buddy.java`；SHA-256：`0fd54bd7ca3ec58459c28abb2a1d4d972b4c0c704725ebf1b8dfe2ebd6bfe4a9`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Buddy(final int objId)` @27 | 委派 `buildPacket(objId)@28` |
| `private void buildPacket(final int objId)` @32 | `C(S_OPCODE_SHOWHTML)@33` → `D(objId)@34` → `S(_buddy)@35` → `H(0x02)@36` → `H(0x02)@37` → `S(result)@59` → `S(onlineBuddy)@60` |

<a id="s-castlemaster"></a>

### S_CastleMaster

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CastleMaster.java`；SHA-256：`9232ef87beb2ee036e96ff76a63bcd03fa9aaff6242fb1e252955d2d361b8d00`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CastleMaster(final int type, final int objecId)` @17 | 委派 `buildPacket(type, objecId)@18` |
| `private void buildPacket(final int type, final int objecId)` @22 | `C(S_OPCODE_CASTLEMASTER)@23` → `C(type)@24` → `D(objecId)@25` |

<a id="s-changeheading"></a>

### S_ChangeHeading

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChangeHeading.java`；SHA-256：`dc8eeae09bec67d5bb0daad7e658a87390718f8e0a3362480d633e80f749e015`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChangeHeading(final L1Character cha)` @18 | 委派 `buildPacket(cha)@19` |
| `private void buildPacket(final L1Character cha)` @23 | `C(S_OPCODE_CHANGEHEADING)@24` → `D(cha.getId())@25` → `C(cha.getHeading())@26` |

<a id="s-changename"></a>

### S_ChangeName

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChangeName.java`；SHA-256：`50c9e9be95a07eab1798fe72df73a90ace88ed96b4e7bcacbb0c408e7ed6059b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChangeName(final int objectId, final String name)` @19 | `C(S_OPCODE_CHANGENAME)@20` → `D(objectId)@21` → `S(name)@22` |
| `public S_ChangeName(final int objectId, final String name, final int mode)` @32 | `C(S_OPCODE_CHANGENAME)@69` → `D(objectId)@70` → `S(color + "GM \\f=" + name)@71` |
| `public S_ChangeName(final L1PcInstance pc, final boolean isName)` @80 | `C(S_OPCODE_CHANGENAME)@81` → `D(pc.getId())@82` → `S(stringBuilder.toString())@91` |

<a id="s-changeshape"></a>

### S_ChangeShape

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChangeShape.java`；SHA-256：`b23ec55370d70c3536d1d1aaa0b73921b9c1a83b533bcdbc695e32cb9846794d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChangeShape(final L1Character obj, final int polyId)` @21 | 委派 `buildPacket(obj, polyId, false)@22` |
| `public S_ChangeShape(final L1Character obj)` @29 | 委派 `buildPacket(obj, obj.getGfxId(), false)@30` |
| `public S_ChangeShape(final L1Character obj, final int polyId, final boolean weaponTakeoff)` @40 | 委派 `buildPacket(obj, polyId, weaponTakeoff)@41` |
| `private void buildPacket(final L1Character obj, final int polyId, final boolean weaponTakeoff)` @45 | `C(S_OPCODE_POLY)@46` → `D(obj.getId())@47` → `H(polyId)@48` → `H(weaponTakeoff ? 0 : 29)@50` |
| `public S_ChangeShape(final L1PcInstance pc, final L1NpcInstance npc, final int polyId)` @59 | `C(S_OPCODE_POLY)@60` → `D(npc.getId())@61` → `D(pc.getId())@62` → `H(polyId)@63` → `S(pc.getName())@64` |

<a id="s-charamount"></a>

### S_CharAmount

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CharAmount.java`；SHA-256：`5d1aba56407fad1d758ed9fa240be6d44ef5c2ad4d70450bfdad00142d7f97a8`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CharAmount(final int value, final ClientExecutor client)` @20 | 委派 `buildPacket(value, client)@21` |
| `private void buildPacket(final int value, final ClientExecutor client)` @25 | `C(S_OPCODE_CHARAMOUNT)@31` → `C(value)@32` → `C(maxAmount)@33` |

<a id="s-charcreatestatus"></a>

### S_CharCreateStatus

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CharCreateStatus.java`；SHA-256：`6a666e05144f0506857fd485bf0e98ef67aa2f1edd4cd15b00dd3895e7b7b182`。

局部常數（subcode／格式旗標／限制）：`REASON_OK=0x02`；`REASON_ALREADY_EXSISTS=0x06`；`REASON_INVALID_NAME=0x09`；`REASON_WRONG_AMOUNT=0x15`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CharCreateStatus(final int reason)` @24 | `C(S_OPCODE_NEWCHARWRONG)@26` → `C(reason)@27` |

<a id="s-charpacks"></a>

### S_CharPacks

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CharPacks.java`；SHA-256：`25020fc64589a38f32ad63b6c75b07d2dd360a76bdcca14a0b0f465ed8555f99`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CharPacks(final String name, final String clanName, final int type, final int sex, final int lawful, final int hp, final int mp, final int ac, final int lv, final int str, final int dex, final int con, final int wis, final int cha, final int intel, final int time)` @32 | `C(S_OPCODE_CHARLIST)@35` → `S(name)@36` → `S(clanName)@37` → `C(type)@38` → `C(sex)@39` → `H(lawful)@40`<br>〔`if ConfigClient.isROClient()`〕 `D(hp)@44` → `D(mp)@45`<br>〔`else`〕 `H(hp)@47` → `H(mp)@48`<br>〔`if ac > 0x0a`〕 `C(0x0a)@52`<br>〔`else`〕 `C(ac)@55`<br>〔`if lv > 0x7f`〕 `C(0x7f)@59`<br>〔`else`〕 `C(lv)@62`<br>〔`if str > 0x7f`〕 `C(0x7f)@66`<br>〔`else`〕 `C(str)@68`<br>〔`if dex > 0x7f`〕 `C(0x7f)@71`<br>〔`else`〕 `C(dex)@73`<br>〔`if con > 0x7f`〕 `C(0x7f)@76`<br>〔`else`〕 `C(con)@78`<br>〔`if wis > 0x7f`〕 `C(0x7f)@81`<br>〔`else`〕 `C(wis)@83`<br>〔`if cha > 0x7f`〕 `C(0x7f)@86`<br>〔`else`〕 `C(cha)@88`<br>〔`if intel > 0x7f`〕 `C(0x7f)@91`<br>〔`else`〕 `C(intel)@93`<br>`C(0x00)@97` → `D(time)@98` → `C(checkcode & 0xFF)@100` |

<a id="s-charreset"></a>

### S_CharReset

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CharReset.java`；SHA-256：`c1ae26bf59405be0d11ae4b2fae2ce63d28281ead804705f2effb8acbd4e3138`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CharReset(final L1PcInstance pc, final int lv, final int hp, final int mp, final int ac, final int str, final int intel, final int wis, final int dex, final int con, final int cha)` @15 | `C(S_OPCODE_CHARRESET)@17` → `C(0x02)@18` → `C(lv)@19` → `C(pc.getTempMaxLevel())@20` → `H(hp)@21` → `H(mp)@22` → `H(ac)@23` → `C(str)@24` → `C(intel)@25` → `C(wis)@26` → `C(dex)@27` → `C(con)@28` → `C(cha)@29` |
| `public S_CharReset(final int point)` @33 | `C(S_OPCODE_CHARRESET)@34` → `C(0x03)@35` → `C(point)@36` |
| `public S_CharReset(int pcObjId, int emblemId)` @44 | `C(S_OPCODE_CHARRESET)@45` → `C(0x3c)@46` → `D(pcObjId)@47` → `D(emblemId)@48` |
| `public S_CharReset(final L1PcInstance pc)` @55 | `C(S_OPCODE_CHARRESET)@56` → `C(0x01)@57`<br>〔`if pc.isCrown()`〕 `H(14)@59` → `H(2)@60`<br>〔`if pc.isKnight()`〕 `H(16)@63` → `H(1)@64`<br>〔`if pc.isElf()`〕 `H(15)@67` → `H(4)@68`<br>〔`if pc.isWizard()`〕 `H(12)@71` → `H(6)@72`<br>〔`if pc.isDarkelf()`〕 `H(12)@75` → `H(3)@76`<br>〔`if pc.isDragonKnight()`〕 `H(15)@79` → `H(4)@80`<br>〔`if pc.isIllusionist()`〕 `H(15)@83` → `H(4)@84`<br>`C(0x0a)@86` → `C(pc.getTempMaxLevel())@87` |

<a id="s-charresetinfo"></a>

### S_CharResetInfo

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CharResetInfo.java`；SHA-256：`76d3477398e1606db6dfc050df35d9a91d0a248e90b8a81bf15b31a493aa66f8`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CharResetInfo(final L1PcInstance pc)` @18 | `C(S_OPCODE_CHARRESET)@40` → `C(0x04)@41` → `C((upInt << 4) + upStr)@42` → `C((upDex << 4) + upWis)@43` → `C((upCha << 4) + upCon)@44` → `C(0x00)@45` → `H(0x00)@46` |

<a id="s-chartitle"></a>

### S_CharTitle

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CharTitle.java`；SHA-256：`16ce2bc261cb0d9a4ae0ad5550172e483fa67fc09ab480d376f6f14e4df4e6c7`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CharTitle(int objid, StringBuilder title)` @28 | `C(S_OPCODE_CHARTITLE)@29` → `D(objid)@30` → `S(title.toString())@31` |
| `public S_CharTitle(final int objid)` @40 | `C(S_OPCODE_CHARTITLE)@41` → `D(objid)@42` → `S("")@43` |

<a id="s-charvisualupdate"></a>

### S_CharVisualUpdate

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CharVisualUpdate.java`；SHA-256：`4eab821186c8820b47a731ccd43a0e6481c334d1d537030cb73542cdbf97b8ee`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CharVisualUpdate(final int objid, final int weaponType)` @19 | `C(S_OPCODE_CHARVISUALUPDATE)@20` → `D(objid)@21` → `C(weaponType)@22` |
| `public S_CharVisualUpdate(final L1PcInstance cha)` @30 | `C(S_OPCODE_CHARVISUALUPDATE)@31` → `D(cha.getId())@32` → `C(cha.getCurrentWeapon())@33` |

<a id="s-chat"></a>

### S_Chat

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Chat.java`；SHA-256：`f1aa6a354745fe0f29b4d9154ea2c0b757e3aa264f943d1993d23ee4e6006f6b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Chat(final L1PcInstance pc, final String chat)` @21 | 委派 `buildPacket(pc, chat)@22` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @26 | `C(S_OPCODE_NORMALCHAT)@27` → `C(0x00)@28` → `D(pc.isInvisble()? 0 : pc.getId())@29` → `S(pc.getName() + ": " + chat)@30` |
| `public S_Chat(final L1NpcInstance npc, final String chat)` @38 | `C(S_OPCODE_NORMALCHAT)@39` → `C(0x00)@40` → `D(npc.isInvisble()? 0 : npc.getId())@41` → `S(npc.getNameId() + ": " + chat)@42` |
| `public S_Chat(L1Object object, String chat, int x)` @46 | `C(S_OPCODE_NORMALCHAT)@47` → `C(0x00)@48` → `D(object.getId())@49` → `S(chat)@50` |

<a id="s-chatclan"></a>

### S_ChatClan

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatClan.java`；SHA-256：`bbda70430611bf6aa871bd55974d0a771401374014f5f7268980a3a151fe6b27`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatClan(final L1PcInstance pc, final String chat)` @19 | 委派 `buildPacket(pc, chat)@20` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @24 | `C(S_OPCODE_GLOBALCHAT)@25` → `C(0x04)@26` → `S("{" + pc.getName() + "} " + chat)@27` |

<a id="s-chatclanunion"></a>

### S_ChatClanUnion

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatClanUnion.java`；SHA-256：`0004c8b025f3d80b12e1bfa5e3e883071534e63a5d0466c95ebbe16fd61c35ef`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatClanUnion(final L1PcInstance pc, final String chat)` @19 | 委派 `buildPacket(pc, chat)@20` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @24 | `C(S_OPCODE_GLOBALCHAT)@25` → `C(0x04)@26` → `S("{{" + pc.getName() + "}} " + chat)@27` |

<a id="s-chatglobal"></a>

### S_ChatGlobal

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatGlobal.java`；SHA-256：`f58aafed8c566655ed5cee1e45c44376b90e9104ba2993c32ee1a8012119fe1d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatGlobal(final L1PcInstance pc, final String chat)` @24 | 委派 `buildPacket(pc, chat)@25` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @29 | `C(S_OPCODE_GLOBALCHAT)@30` → `C(0x03)@31` → `S(pc.isGm()? "[******] " + chat : "[" + pc.getName() + "] " + chat)@32` |
| `public S_ChatGlobal(final L1NpcInstance npc, final String chat)` @40 | `C(S_OPCODE_GLOBALCHAT)@41` → `C(0x03)@42` → `S("[" + (nameId != null ? nameId : "") + "] " + chat)@48` |
| `public S_ChatGlobal(final String chat)` @57 | `C(S_OPCODE_GLOBALCHAT)@58` → `C(0x03)@59` → `S(chat)@60` |

<a id="s-chatout"></a>

### S_ChatOut

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatOut.java`；SHA-256：`1ca7b0ca493e7b010a745d5333bd2dd383fa053c380269cd6d6a9869ef65ad26`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatOut(final int objid)` @18 | 委派 `buildPacket(objid)@19` |
| `public S_ChatOut(L1PcInstance pc)` @27 | 委派 `buildPacket(pc.getId())@28` |
| `private void buildPacket(final int objid)` @32 | `D(objid)@34` → `D(0x00000000)@35` → `D(0x00000000)@36` → `D(0x00000000)@37` |

<a id="s-chatparty"></a>

### S_ChatParty

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatParty.java`；SHA-256：`4f9aaf73fd2bfafd6ded24a3e4fbe74ada2468dfff554a6e4f7d01be40b2c605`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatParty(final L1PcInstance pc, final String chat)` @19 | 委派 `buildPacket(pc, chat)@20` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @24 | `C(S_OPCODE_GLOBALCHAT)@25` → `C(0x0b)@26` → `S("(" + pc.getName() + ") " + chat)@27` |

<a id="s-chatparty2"></a>

### S_ChatParty2

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatParty2.java`；SHA-256：`13d26f80e88477e267ce0321618799d455534c6894098b9c3457b2847e718854`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatParty2(final L1PcInstance pc, final String chat)` @19 | 委派 `buildPacket(pc, chat)@20` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @24 | `C(S_OPCODE_NORMALCHAT)@25` → `C(0x0e)@26` → `D(pc.isInvisble()? 0 : pc.getId())@27` → `S("(" + pc.getName() + ") " + chat)@28` |

<a id="s-chatshouting"></a>

### S_ChatShouting

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatShouting.java`；SHA-256：`d08467b5b3fb66d2415c4e97a274e62525eacc6eca817d85f7953a5f21a75906`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatShouting(final L1PcInstance pc, final String chat)` @20 | 委派 `buildPacket(pc, chat)@21` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @25 | `C(S_OPCODE_NORMALCHAT)@26` → `C(0x02)@27` → `D(pc.isInvisble()? 0 : pc.getId())@28` → `S("<" + pc.getName() + "> " + chat)@29` → `H(pc.getX())@31` → `H(pc.getY())@32` |
| `public S_ChatShouting(final L1NpcInstance npc, final String chat)` @40 | `C(S_OPCODE_NORMALCHAT)@41` → `C(0x02)@42` → `D(npc.isInvisble()? 0 : npc.getId())@43` → `S("<" + npc.getNameId() + "> " + chat)@44` → `H(npc.getX())@46` → `H(npc.getY())@47` |

<a id="s-chattransaction"></a>

### S_ChatTransaction

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatTransaction.java`；SHA-256：`751477efe4967ccfec4e5cf984e923cb3b82a954eb4b8208ec6592422694e6be`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatTransaction(final L1PcInstance pc, final String chat)` @20 | 委派 `buildPacket(pc, chat)@21` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @25 | `C(S_OPCODE_GLOBALCHAT)@26` → `C(0x0c)@27` → `S("[" + pc.getName() + "] " + chat)@28` |
| `public S_ChatTransaction(L1NpcInstance npc, String chat)` @36 | `C(S_OPCODE_GLOBALCHAT)@37` → `C(0x0c)@38` → `S("[" + npc.getNameId() + "] " + chat)@39` |

<a id="s-chatwhisperfrom"></a>

### S_ChatWhisperFrom

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatWhisperFrom.java`；SHA-256：`74d33c10375d463ac288d15c1f09786fdf1b1c855b5e5c11e0baff3ccd97f9aa`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatWhisperFrom(final L1PcInstance pc, final String chat)` @19 | 委派 `buildPacket(pc, chat)@20` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @24 | `C(S_OPCODE_WHISPERCHAT)@25` → `S(pc.getName())@26` → `S(chat)@27` |

<a id="s-chatwhisperto"></a>

### S_ChatWhisperTo

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ChatWhisperTo.java`；SHA-256：`b8531703eaedbca5bfc103577fb5e2b20493bfbbde5d9c68886e9855782f563e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ChatWhisperTo(final L1PcInstance pc, final String chat)` @15 | 委派 `buildPacket(pc, chat)@16` |
| `private void buildPacket(final L1PcInstance pc, final String chat)` @20 | `C(S_OPCODE_GLOBALCHAT)@21` → `C(0x09)@22` → `S("-> (" + pc.getName() + ") " + chat)@23` |
| `public S_ChatWhisperTo(final L1DeInstance de, final String chat)` @26 | `C(S_OPCODE_GLOBALCHAT)@27` → `C(0x09)@28` → `S("-> (" + de.getNameId() + ") " + chat)@29` |

<a id="s-clanattention"></a>

### S_ClanAttention

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClanAttention.java`；SHA-256：`e8322f46f48943591cd5060d1f51ca868abf680e606b1596b15284515439f35e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClanAttention(final L1Clan clan, final int type)` @19 | 委派 `buildPacket(clan, type)@20` |
| `public S_ClanAttention()` @25 | `C(S_OPCODE_CLANATTENTION)@26` → `H(2)@27` → `D(0)@28` → `H(0)@29` |
| `public S_ClanAttention(final List<String> names)` @34 | `C(S_OPCODE_CLANATTENTION)@35` → `H(2)@36`<br>〔`if names == null \|\| names.isEmpty()`〕 `D(0)@38`<br>〔`else`〕 `D(names.size())@40`<br>〔`else → for final String name : names`〕 `S(name)@42`<br>`H(0)@45` |
| `private void buildPacket(final L1Clan clan, final int type)` @49 | `C(S_OPCODE_CLANATTENTION)@50` → `H(type)@51`<br>〔`if type == 2 → if clan == null`〕 `D(0)@54`<br>〔`if type == 2 → else`〕 `D(size)@58`<br>〔`if type == 2 → else → for int i = 0; i < size; i++`〕 `S(list.get(i))@60`<br>`H(0)@64` |

<a id="s-clanupdate"></a>

### S_ClanUpdate

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClanUpdate.java`；SHA-256：`73518ee411eeea3e780892bbae64d39942bedea677e57d529d8586d4a5c9499a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClanUpdate(final int objid, final int clanid,final String Clanname, final int rank)` @19 | `C(S_OPCODE_UPDATECLANID)@21` → `D(objid)@22` → `S(Clanname)@23` → `S(null)@24` → `D(0x00000000)@25` → `C(rank)@26` |
| `public S_ClanUpdate(final int objid)` @35 | `C(S_OPCODE_UPDATECLANID)@37` → `D(objid)@38` → `S(null)@39` → `S(null)@40` → `D(0x00000000)@41` → `C(0)@42` |

<a id="s-classicbadge"></a>

### S_ClassicBadge

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicBadge.java`；SHA-256：`8340d1ea801bf704717ccc317a846f719333705333244cad24ada03b4cb04ae9`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicBadge(int objectId, int badgeId)` @6 | 委派 `this(1, objectId, badgeId)@6` |
| `private S_ClassicBadge(int command, int objectId, int badgeId)` @7 | `C(S_OPCODE_PACKETBOX)@10` → `C(0xff)@10` → `C(0x34)@10` → `D(0x31474442)@10` → `C(1)@11` → `C(command)@11` → `D(objectId)@11` → `D(badgeId)@11`<br>〔`for int n=0;n<7;n++`〕 `C(0)@12` |
| `public static S_ClassicBadge reset()` @15 | 委派 `S_ClassicBadge(0, 0, 0)@15` |

<a id="s-classicbagcombine"></a>

### S_ClassicBagCombine

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicBagCombine.java`；SHA-256：`b63ff41e7e06a65748b8ea67f3c9a1d97324b0fe3ed7e2d8f47f7a5364f61433`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicBagCombine(int command,int nonce,int request,int token,UiOperationContext.Offer offer)` @10 | `C(S_OPCODE_PACKETBOX)@14` → `C(0xff)@14` → `C(0x33)@14` → `D(0x47414243)@14` → `C(2)@15` → `C(command)@15` → `D(nonce)@15` → `D(request)@15` → `D(token)@15` |

<a id="s-classicboss"></a>

### S_ClassicBoss

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicBoss.java`；SHA-256：`490f4243dc4542fdcb8e812cbccc8e456de47a88e35854a0ec3688bd2df166ab`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicBoss(int command,int nonce,int player,int token,int prior,int npc,int total,int start,List<Row> rows)` @11 | `C(S_OPCODE_PACKETBOX)@15` → `C(255)@15` → `C(0x37)@15` → `D(0x31534243)@15` → `C(2)@15` → `C(command)@15` → `D(nonce)@16` → `D(player)@16` → `D(token)@16` → `D(prior)@16` → `D(npc)@16` → `D(total)@16` → `D(start)@16` → `H(rows.size())@16`<br>〔`for var r:rows`〕 `D(r.id())@17` → `C(r.state())@17` → 委派 `text(r.name(),96)@17` → 委派 `text(r.status(),96)@17` |
| `private void text(String text,int max)` @20 | `H(bytes.length)@23`<br>〔`for byte b:bytes`〕 `C(b&255)@23` |

<a id="s-classiccollection"></a>

### S_ClassicCollection

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicCollection.java`；SHA-256：`da08bca686dc0abf76bb64198ddc0ba02874266239b55ad47ba6c21061143117`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicCollection(int command,int nonce,int player,int token,int npc,int serial,int version, int page,int total,int selected,int flags,int materialTotal,int offset, List<Row> rows,List<String> lines,List<Material> materials)` @11 | `C(S_OPCODE_PACKETBOX)@18` → `C(255)@18` → `C(0x39)@18` → `D(0x314c4f43)@18` → `C(1)@18` → `C(command)@18` → `D(nonce)@19` → `D(player)@19` → `D(token)@19` → `D(npc)@19` → `D(serial)@19` → `D(version)@19` → `D(page)@20` → `D(total)@20` → `D(selected)@20` → `C(flags)@20` → `H(materialTotal)@20` → `H(offset)@20` → `C(rows.size())@21` → `C(lines.size())@21` → `C(materials.size())@21`<br>〔`for var r:rows`〕 `D(r.quest)@22` → `C(r.state)@22` → 委派 `text(r.name,96)@22`<br>〔`for String line:lines`〕 委派 `text(line,120)@23`<br>〔`for var m:materials`〕 `D(m.item)@25` → `H(m.icon)@25` → `D(m.required)@25` → `D((int)Math.min(Integer.MAX_VALUE,m.available))@25` → `D(m.enchant)@25` → 委派 `text(m.name,144)@25` |
| `private void text(String s,int max)` @28 | `H(b.length)@31`<br>〔`for byte v:b`〕 `C(v&255)@31` |
| `public List<ServerBasePacket> fragments()` @35 | 〔`for int i=0;i<count;i++`〕 委派 `Chunk(content,i,count)@38` |
| `Chunk(byte[] whole,int index,int count)` @42 | 〔`for int i=0;i<33;i++`〕 `C(i==8?4:whole[i]&255)@43`<br>`H(index)@44` → `H(count)@44` → `H(whole.length)@44` → `H(n)@44`<br>〔`for int i=start;i<start+n;i++`〕 `C(whole[i]&255)@45` |

<a id="s-classiccraft"></a>

### S_ClassicCraft

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicCraft.java`；SHA-256：`8cbcbcde95916a8da126e29823b049081b65d44f63523b4d97fe4a2136362b67`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `private void header(int command,int nonce)` @14 | `C(S_OPCODE_PACKETBOX)@15` → `C(0xff)@15` → `C(0x32)@15` → `D(0x46524343)@15` → `C(1)@16` → `C(command)@16` → `D(nonce)@16` |
| `private void text(String text)` @18 | `H(bytes.length)@21`<br>〔`for byte b:bytes`〕 `C(b&255)@21` |
| `private void item(Item item)` @23 | `D(item.id())@24` → `D(item.icon())@24` → `D(item.enchant())@24` → `D(item.count())@24` → `D(item.owned())@24` → 委派 `text(item.name())@24` |
| `public S_ClassicCraft(int command,int nonce,int token)` @26 | 委派 `header(command,nonce)@28` → `D(token)@28` |
| `public S_ClassicCraft(Preview p,UiOperationContext.Ticket prerequisite)` @30 | 委派 `header(1,p.nonce())@33` → `D(p.token())@33` → `D(p.priorToken())@33` → `D(p.npc())@33` → `D(p.recipe())@33` → `D(p.maximum())@33` → `H(p.level())@34` → `H(p.rate())@34` → `C(p.eligible()?1:0)@34` → `C(p.materials().size())@35` → `C(p.products().size())@35` → 委派 `text("全職業")@35`<br>〔`for Item item:p.materials()`〕 委派 `item(item)@36`<br>〔`for Item item:p.products()`〕 委派 `item(item)@37` |

<a id="s-classicinventoryfilter"></a>

### S_ClassicInventoryFilter

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicInventoryFilter.java`；SHA-256：`6f01edc02158f1724e7a007f6dc01ac29536a17c500c267568711f00c7ae9fe3`。

局部常數（subcode／格式旗標／限制）：`MAX_TARGETS=2048`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicInventoryFilter(final int command, final int request, final List<Integer> targets)` @10 | `C(S_OPCODE_PACKETBOX)@13` → `C(0xff)@13` → `C(0x31)@13` → `D(0x46495543)@14` → `C(1)@15` → `C(command)@15` → `D(request)@15` → `H(targets.size())@15`<br>〔`for final int target : targets`〕 `D(target)@18` |

<a id="s-classicpass"></a>

### S_ClassicPass

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicPass.java`；SHA-256：`a62d21c5055a79de684a307c451df65b493d22cf0a7a5ffafc53b454d67043db`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicPass(int nonce,int player,int sequence,int level,boolean premium,int costItem,int costCount,String costName,List<Row> rows)` @16 | 委派 `this(nonce,player,sequence,level,premium,costItem,costCount,costName,rows,null,null)@17` |
| `public S_ClassicPass(int nonce,int player,int sequence,int level,boolean premium,int costItem,int costCount,String costName,List<Row> rows, UiOperationContext.Offer offer,UiOperationContext.Ticket prerequisite)` @19 | `C(S_OPCODE_PACKETBOX)@23` → `C(255)@23` → `C(0x35)@23` → `D(0x31535043)@23` → `C(offer==null?1:2)@23` → `D(nonce)@24` → `D(player)@24` → `D(sequence)@24`<br>〔`if offer!=null`〕 `D(offer.originId())@25` → `D(prerequisite!=null&&prerequisite.offer().subject() instanceof com.lineage.server.model.ClassicPassService.Session old?old.token():0)@26`<br>`H(level)@28` → `C(premium?1:0)@28` → `C(rows.size())@28` → `D(costItem)@29` → `D(costCount)@29` → 委派 `text(costName)@29`<br>〔`for Row row:rows`〕 `D(row.id())@31` → `H(row.level())@31` → `C(row.gold().size())@31` → 委派 `item(row.ordinary())@31`<br>〔`for Row row:rows → for Item i:row.gold()`〕 委派 `item(i)@32` |
| `private void item(Item i)` @36 | `D(i.id())@38` → `H(i.icon())@38` → `D(i.count())@38` → `C(i.state())@38` → 委派 `text(i.name())@38` |
| `private void text(String value)` @40 | `H(raw.length)@44`<br>〔`for byte b:raw`〕 `C(b&255)@44` |

<a id="s-classicquest"></a>

### S_ClassicQuest

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicQuest.java`；SHA-256：`6ebeb0eeec7fb78e7fe0f20a404ad0d6430797c63a54ff9881c0311e6764d2c3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicQuest(int command,int nonce,int player,int token,int prior,int npc,int page,int total, int selected,int state,int permissions,List<Row> rows,List<Reward> rewards,List<String> lines)` @11 | `C(S_OPCODE_PACKETBOX)@15` → `C(255)@15` → `C(0x36)@15` → `D(0x31535143)@15` → `C(2)@15` → `C(command)@15` → `D(nonce)@16` → `D(player)@16` → `D(token)@16` → `D(prior)@16` → `D(npc)@16` → `D(page)@16` → `D(total)@16` → `D(selected)@16` → `C(state)@17` → `C(permissions)@17` → `C(rows.size())@17` → `H(rewards.size())@17` → `H(lines.size())@17`<br>〔`for var r:rows`〕 `D(r.id())@18` → `C(r.state())@18` → 委派 `text(r.name())@18`<br>〔`for var r:rewards`〕 `D(r.id())@19` → `H(r.icon())@19` → `D(r.count())@19` → 委派 `text(r.name())@19`<br>〔`for var line:lines`〕 委派 `text(line)@20` |
| `private void text(String text)` @23 | `H(raw.length)@27`<br>〔`for byte b:raw`〕 `C(b&255)@27` |

<a id="s-classicranking"></a>

### S_ClassicRanking

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicRanking.java`；SHA-256：`75266cba41fa1c665280564b4bc0f9ad1c886f96a5ce14810c5478beb731c5ff`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicRanking(int command,int nonce,int player,int token,int npc,int serial,int version,int board,int job,List<ClassicRankingModel.Row> rows)` @10 | `C(S_OPCODE_PACKETBOX)@17` → `C(255)@17` → `C(0x38)@17` → `D(0x314b5243)@17` → `C(1)@17` → `C(command)@17` → `D(nonce)@18` → `D(player)@18` → `D(token)@18` → `D(npc)@18` → `D(serial)@18` → `D(version)@18` → `C(board)@18` → `C(job)@18` → `C(rows.size())@18`<br>〔`for var r:rows`〕 `D(r.id())@22` → `C(r.job())@22` → `D(r.value())@22` → 委派 `text(r.name(),96,false)@22` → 委派 `text(r.item(),192,board==0\|\|board==4)@22` |
| `private void text(String s,int max,boolean empty)` @26 | `H(b.length)@29`<br>〔`for byte v:b`〕 `C(v&255)@29` |

<a id="s-classicreward"></a>

### S_ClassicReward

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ClassicReward.java`；SHA-256：`3140adb09abdbd783abbedc2c2edca10c063df1379ab4e607b029c785d122ec9`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ClassicReward(int command,int nonce,int player,int token,int npc,int serial,int version,int page,int total,int claimedTotal,List<ClassicRewardProtocol.Row> rows)` @8 | `C(S_OPCODE_PACKETBOX)@17` → `C(255)@17` → `C(0x3a)@17` → `D(0x31445752)@17` → `C(4)@17` → `C(command)@17` → `D(nonce)@18` → `D(player)@18` → `D(token)@18` → `D(npc)@18` → `D(serial)@18` → `D(version)@18` → `D(page)@18` → `D(total)@18` → `D(claimedTotal)@18` → `C(rows.size())@18`<br>〔`for var r:rows`〕 `D(r.id())@22` → `H(r.icon())@22` → `D(r.count())@22` → `D(r.enchant())@22` → `C(r.claimed()?1:0)@22` → `H(text.length)@22`<br>〔`for var r:rows → for byte v:text`〕 `C(v&255)@22` |

<a id="s-closelist"></a>

### S_CloseList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CloseList.java`；SHA-256：`b9198c23d996b8289592fc0faae17af27bb44d410736ab1fb067d1bda4b0afe4`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CloseList(final int objid)` @19 | `C(S_OPCODE_SHOWHTML)@20` → `D(objid)@21` → `S("")@22` |

<a id="s-commoninfo"></a>

### S_CommonInfo

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CommonInfo.java`；SHA-256：`4cbde22e296525f4eea0ae77d851d94f87fea919e83e9836f62b165f3691d10d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CommonInfo(final int type, final String[] info)` @16 | 委派 `buildPacket(type, info)@17` |
| `private void buildPacket(final int type, final String[] info)` @21 | `C(S_OPCODE_COMMONNEWS)@22` → `H(type)@23`<br>〔`if info == null`〕 `C(0x00)@26`<br>〔`else`〕 `C(info.length)@29`<br>〔`else → for int i = 0 ; i < info.length ; i++`〕 `S(info[i])@31` |

<a id="s-commonnews"></a>

### S_CommonNews

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CommonNews.java`；SHA-256：`3761de2cee68f46dbd351c264fd58eee9510ec978cf87cc11c275acefc3b8434`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CommonNews()` @20 | `C(S_OPCODE_COMMONNEWS)@22` → `S(messagePack.toString())@27` |
| `public S_CommonNews(final String s)` @35 | `C(S_OPCODE_COMMONNEWS)@36` → `S(s)@37` |

<a id="s-curseblind"></a>

### S_CurseBlind

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_CurseBlind.java`；SHA-256：`cf6288f4245c94b02cbc91cf420fdf5e7a8bd9794b77acbbc30e05bea25947bf`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_CurseBlind(final int type)` @16 | 委派 `buildPacket(type)@17` |
| `private void buildPacket(final int type)` @21 | `C(S_OPCODE_CURSEBLIND)@22` → `H(type)@23` |

<a id="s-deletecharok"></a>

### S_DeleteCharOK

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_DeleteCharOK.java`；SHA-256：`52405c3aeb5e1ef9a2d2a0e38d3cdf09694e4ec74a68183e88490fe8512dbb2a`。

局部常數（subcode／格式旗標／限制）：`DELETE_CHAR_NOW=0x05`；`DELETE_CHAR_AFTER_7DAYS=0x51`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_DeleteCharOK(final int type)` @20 | `C(S_OPCODE_DETELECHAROK)@22` → `C(type)@23` |

<a id="s-deleteinventoryitem"></a>

### S_DeleteInventoryItem

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_DeleteInventoryItem.java`；SHA-256：`53f78032ca8bbc25a32e94b31f0914b356c556a9ed2abd5a93cbea3b5daffb93`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_DeleteInventoryItem(final L1ItemInstance item)` @19 | `C(S_OPCODE_DELETEINVENTORYITEM)@20` → `D(item.getId())@21` |
| `public S_DeleteInventoryItem(final int objid)` @25 | `C(S_OPCODE_DELETEINVENTORYITEM)@26` → `D(objid)@27` |

<a id="s-delskill"></a>

### S_DelSkill

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_DelSkill.java`；SHA-256：`4a252f732bdfddb0040b5d14c8f3a047e50a3e9868a09c17b91edd8e68d63edf`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_DelSkill(final L1PcInstance pc, final int skillid)` @21 | `C(S_OPCODE_DELSKILL)@34` → `C(skill_list_values)@35`<br>〔`for final int element : skillList`〕 `C(element)@38` |
| `public S_DelSkill(final L1PcInstance pc, final int level1, final int level2, final int level3, final int level4, final int level5, final int level6, final int level7, final int level8, final int level9, final int level10, final int knight1, final int knight2, final int de1, final int de2, final int royal, final int un, final int elf1, final int elf2, final int elf3, final int elf4, final int elf5, final int elf6, final int k1, final int k2, final int k3, final int l1, final int l2, final int l3 )` @87 | `C(S_OPCODE_DELSKILL)@102`<br>〔`if (i6 > 0x00) && (j6 == 0x00)`〕 `C(0x32)@105`<br>〔`if j6 > 0x00`〕 `C(0x64)@108`<br>〔`else`〕 `C(0x20)@111`<br>`C(level1)@114` → `C(level2)@115` → `C(level3)@116` → `C(level4)@117` → `C(level5)@118` → `C(level6)@119` → `C(level7)@120` → `C(level8)@121` → `C(level9)@122` → `C(level10)@123` → `C(knight1)@125` → `C(knight2)@126` → `C(de1)@128` → `C(de2)@129` → `C(royal)@131` → `C(un)@133` → `C(elf1)@135` → `C(elf2)@136` → `C(elf3)@137` → `C(elf4)@138` → `C(elf5)@139` → `C(elf6)@140` → `C(k1)@142` → `C(k2)@143` → `C(k3)@144` → `C(l1)@146` → `C(l2)@147` → `C(l3)@148` |

<a id="s-deposit"></a>

### S_Deposit

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Deposit.java`；SHA-256：`768e007c1f4041a1f33a58bd47e6fbeec8ae38be6c92343d8182b4c28f262f97`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Deposit(final int objecId)` @22 | 委派 `this(objecId, null, null)@23` |
| `public S_Deposit(final int objecId, final Offer offer, final Ticket prerequisite)` @26 | `C(S_OPCODE_DEPOSIT)@29` → `D(objecId)@30` |

<a id="s-dexup"></a>

### S_Dexup

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Dexup.java`；SHA-256：`ef0a5702fb527ca4d71c72a83d5405f2083cff917993903b1a5fb02f900a18c6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Dexup(final L1PcInstance pc, final int type, final int time)` @20 | `C(S_OPCODE_DEXUP)@22` → `H(time)@23` → `C(pc.getDex())@24` → `C(type)@25` |

<a id="s-disconnect"></a>

### S_Disconnect

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Disconnect.java`；SHA-256：`752eb3a4ee13049502ee86bf4b0251ca52b7cdb2732b44b941ee10e5c4dd6dcb`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Disconnect()` @15 | `C(S_OPCODE_DISCONNECT)@18` → `H(0x01f4)@19` → `D(0x00000000)@20` |

<a id="s-doactiongfx"></a>

### S_DoActionGFX

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_DoActionGFX.java`；SHA-256：`7d71e3bd0d6fa265058cd78223148e54e45106724c4eeb268f62fd90b136b6ba`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_DoActionGFX(final int objectId, final int actionId)` @19 | `C(S_OPCODE_DOACTIONGFX)@20` → `D(objectId)@21` → `C(actionId)@22` |

<a id="s-doactionshop"></a>

### S_DoActionShop

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_DoActionShop.java`；SHA-256：`8ccc99bdb68203f49936775b60f3ff2456413ecaa1cc570d9e8742e0592e30fc`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_DoActionShop(final int object, final byte[] message)` @17 | `C(S_OPCODE_DOACTIONGFX)@18` → `D(object)@19` → `C(ActionCodes.ACTION_Shop)@20` → `Byte(message)@21` |
| `public S_DoActionShop(final int object, final String message)` @30 | `C(S_OPCODE_DOACTIONGFX)@31` → `D(object)@32` → `C(ActionCodes.ACTION_Shop)@33` → `S(message)@34` |

<a id="s-door"></a>

### S_Door

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Door.java`；SHA-256：`9533efa6d0724e7ea307e9e78e16ff335db5da315df2bb67b32e57cbaa703fa3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Door(final L1DoorInstance door)` @18 | 委派 `buildPacket(door.getEntranceX(), door.getEntranceY(), door.getDirection(), door.getPassable())@19` |
| `public S_Door(final int x, final int y, final int direction, final int passable)` @30 | 委派 `buildPacket(x, y, direction, passable)@31` |
| `private void buildPacket(final int x, final int y, final int direction, final int passable)` @35 | `C(S_OPCODE_ATTRIBUTE)@36` → `H(x)@37` → `H(y)@38` → `C(direction)@39` → `C(passable)@40` |

<a id="s-doorpack"></a>

### S_DoorPack

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_DoorPack.java`；SHA-256：`e1e26b0461705ece45a2d6c8d326d7023418a43fbd37b7048527de419bc37a48`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_DoorPack(final L1DoorInstance door)` @19 | 委派 `buildPacket(door)@20` |
| `private void buildPacket(final L1DoorInstance door)` @24 | `C(S_OPCODE_CHARPACK)@25` → `H(door.getX())@26` → `H(door.getY())@27` → `D(door.getId())@28` → `H(door.getGfxId())@29`<br>〔`if door.isDead()`〕 `C(doorStatus)@34`<br>〔`if openStatus == ActionCodes.ACTION_Open`〕 `C(openStatus)@37`<br>〔`if (door.getMaxHp() > 1) && (doorStatus != 0)`〕 `C(doorStatus)@40`<br>〔`else`〕 `C(openStatus)@43`<br>`C(0x00)@45` → `C(0x00)@46` → `C(0x00)@47` → `D(0x00000001)@48` → `H(0x0000)@49` → `S(null)@50` → `S(null)@51` → `C(0x00)@52` → `D(0x00000000)@53` → `S(null)@54` → `S(null)@55` → `C(0x00)@57` → `C(0xff)@59` → `C(0x00)@60` → `C(0x00)@61` → `C(0x00)@62` → `C(0xff)@63` → `C(0xff)@64` |

<a id="s-drawal"></a>

### S_Drawal

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Drawal.java`；SHA-256：`4311dd098cc115ea028498415d5284014f5b2937a35f820a7d051342f7b11456`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Drawal(final int objectId, final long count)` @23 | 委派 `this(objectId, count, null, null)@24` |
| `public S_Drawal(final int objectId, final long count, final Offer offer, final Ticket prerequisite)` @27 | `C(S_OPCODE_DRAWAL)@30` → `D(objectId)@31` → `D((int) Math.min(count, 2000000000))@32` |

<a id="s-effectlocation"></a>

### S_EffectLocation

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_EffectLocation.java`；SHA-256：`0816db3e29cc8b89da3d45e6114736c1068e61effff8ff8642af37945184d661`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_EffectLocation(final Point pt, final int gfxId)` @23 | 委派 `this(pt.getX(), pt.getY(), gfxId)@24` |
| `public S_EffectLocation(final L1Location loc, final int gfxId)` @36 | 委派 `this(loc.getX(), loc.getY(), gfxId)@37` |
| `public S_EffectLocation(final int x, final int y, final int gfxId)` @51 | `C(S_OPCODE_EFFECTLOCATION)@53` → `H(x)@54` → `H(y)@55` → `H(gfxId)@56` |
| `public S_EffectLocation(final int opid, final L1Location loc)` @65 | `C(opid)@66` → `H(loc.getX())@67` → `H(loc.getY())@68` → `H(4842)@69` |

<a id="s-emblem"></a>

### S_Emblem

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Emblem.java`；SHA-256：`3cc2416ec03c685ca7ce5bb4509b390a2f16553016edb443d3b7ff348a494f72`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Emblem(final int clanid)` @15 | `C(S_OPCODE_EMBLEM)@22` → `D(clanid)@23`<br>〔`for int i = 0 ; i < icon.length ; i++`〕 `P(icon[i])@26` |
| `public S_Emblem(final L1EmblemIcon emblemIcon)` @31 | `C(S_OPCODE_EMBLEM)@33` → `D(emblemIcon.get_clanid())@34` → `Byte(emblemIcon.get_clanIcon())@35` |
| `public S_Emblem(final int clanid, final byte[] clanIcon)` @43 | `C(S_OPCODE_EMBLEM)@44` → `D(clanid)@45` → `Byte(clanIcon)@46` |

<a id="s-entergame"></a>

### S_EnterGame

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_EnterGame.java`；SHA-256：`e0028828980e665ce86d75f055cd17aad11cb3e629ba49f6633e54d156e1891f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_EnterGame()` @13 | `C(S_OPCODE_LOGINTOGAME)@15` → `C(0x03)@16` → `C(0x15)@17` → `C(0x8b)@18` → `C(0x7b)@19` → `C(0x94)@20` → `C(0xf0)@21` → `C(0x2f)@22` |

<a id="s-equipmentwindow"></a>

### S_EquipmentWindow

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_EquipmentWindow.java`；SHA-256：`8cc712915a3aa7675b0c0a81b17e2aa0aeaaf5edbdcf14b9983919d6a7e6db08`。

局部常數（subcode／格式旗標／限制）：`TYPE_1=65`；`TYPE_2=66`；`TYPE_3=67`；`TYPE_4=68`；`TYPE_FIRE_REFINE=48`；`TYPE_FIRE_CRAFT=49`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_EquipmentWindow(final int itemObjId, final int index, final boolean equipped)` @44 | `C(S_OPCODE_CHARRESET)@46` → `C(TYPE_2)@47` → `D(itemObjId)@48` → `C(index)@49` |
| `public S_EquipmentWindow(final List<L1ItemInstance> items)` @59 | `C(S_OPCODE_CHARRESET)@60` → `C(TYPE_1)@61` → `C(items.size())@62`<br>〔`for final L1ItemInstance item : items`〕 `D(item.getId())@65` → `H(item.getEquipWindow())@66` → `H(0x00)@67` |
| `public S_EquipmentWindow(final int type, final int count, final int type2)` @78 | `C(S_OPCODE_CHARRESET)@79` → `C(type)@80` → `D(count)@81` → `D(type2)@84` → `D(type2)@85` → `D(type2)@86` → `D(type2)@87` |
| `public S_EquipmentWindow(final int size)` @95 | `C(S_OPCODE_CHARRESET)@96` → `C(TYPE_4)@97` → `D(size)@98` |
| `public S_EquipmentWindow(final int smithUiType, final int npcObjId)` @108 | `C(S_OPCODE_CHARRESET)@111` → `C(smithUiType)@112` → `D(npcObjId)@113` → `C(0x95)@114` → `C(0x19)@115` |

<a id="s-exp"></a>

### S_Exp

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Exp.java`；SHA-256：`ff2830f32bdc96b758dfde2f2b226449dc20451e6a0b896be52139b303901ea4`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Exp(final L1PcInstance pc)` @19 | `C(S_OPCODE_EXP)@20` → `C(pc.getLevel())@21` → `Exp(pc.getExp())@23` → `C(0x01)@24` |
| `public S_Exp()` @33 | `C(S_OPCODE_EXP)@34` → `C(59)@35` → `D(414931028)@36` → `C(0x01)@37` |

<a id="s-fishing"></a>

### S_Fishing

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Fishing.java`；SHA-256：`d7b537dad8d074766c63dcce5958b39811538f11008445f9f563c5327d92f4a3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Fishing(final int objectId, final int motionNum, final int x, final int y)` @21 | 委派 `buildPacket(objectId, motionNum, x, y)@22` |
| `private void buildPacket(final int objectId, final int motionNum, final int x, final int y)` @26 | `C(S_OPCODE_DOACTIONGFX)@27` → `D(objectId)@28` → `C(motionNum)@29` → `H(x)@30` → `H(y)@31` |

<a id="s-fixweaponlist"></a>

### S_FixWeaponList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_FixWeaponList.java`；SHA-256：`ef59e1f5f87c538be34a5c55b72d72b81aaa89709559ef158e282ba38641ca66`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_FixWeaponList(List<L1ItemInstance> weaponList)` @22 | `C(S_OPCODE_SELECTLIST)@23` → `D(0x000000c8)@24` → `H(weaponList.size())@26`<br>〔`for final L1ItemInstance weapon : weaponList`〕 `D(weapon.getId())@29` → `C(weapon.get_durability())@30` |
| `public S_FixWeaponList(final L1PcInstance pc)` @39 | 委派 `buildPacket(pc)@40` |
| `private void buildPacket(final L1PcInstance pc)` @44 | `C(S_OPCODE_SELECTLIST)@45` → `D(0x000000c8)@46` → `H(weaponList.size())@62`<br>〔`for final L1ItemInstance weapon : weaponList`〕 `D(weapon.getId())@66` → `C(weapon.get_durability())@67` |
| `public S_FixWeaponList(final L1ItemInstance weapon)` @74 | `C(S_OPCODE_SELECTLIST)@75` → `D(0x000000c8)@76` → `H(1)@78` → `D(weapon.getId())@80` → `C(weapon.get_durability())@81` |

<a id="s-followerpack"></a>

### S_FollowerPack

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_FollowerPack.java`；SHA-256：`fb07f668b6ca254c17fe2f93707378d1a09cb7c0fcd0377a78fe88d83d24ad3b`。

局部常數（subcode／格式旗標／限制）：`STATUS_POISON=0x01`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_FollowerPack(final L1FollowerInstance follower, final L1PcInstance pc)` @29 | `C(S_OPCODE_CHARPACK)@30` → `H(follower.getX())@31` → `H(follower.getY())@32` → `D(follower.getId())@33` → `H(follower.getGfxId())@34` → `C(follower.getStatus())@35` → `C(follower.getHeading())@36` → `C(follower.getChaLightSize())@37` → `C(follower.getMoveSpeed())@38` → `D(0x00000000)@39` → `H(0x0000)@40` → `S(follower.getNameId())@41` → `S(follower.getTitle())@42` → `C(status)@49` → `D(0x00000000)@50` → `S(null)@51` → `S(null)@52` → `C(0x00)@54` → `C(0xff)@56` → `C(0x00)@57` → `C(0x00)@58` → `C(0x00)@59` → `C(0xff)@60` → `C(0xff)@61` |

<a id="s-gametime"></a>

### S_GameTime

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_GameTime.java`；SHA-256：`14c950e819c2e9cc4a02835e85509929feeb788c56127404e9a2f008d15039ef`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_GameTime(final int time)` @11 | 委派 `buildPacket(time)@12` |
| `public S_GameTime()` @16 | 委派 `buildPacket(time)@18` |
| `private void buildPacket(final int time)` @22 | `C(S_OPCODE_GAMETIME)@24` → `D(time)@25` |

<a id="s-globaleffect"></a>

### S_GlobalEffect

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_GlobalEffect.java`；SHA-256：`daf271131ad2912652c0b1680d7c46b928abbebc68c848cc8a67e91901de004c`。

局部常數（subcode／格式旗標／限制）：`GLOBAL_EFFECT=83`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_GlobalEffect(final int triggerNum)` @10 | `C(S_OPCODE_PACKETBOX)@11` → `C(GLOBAL_EFFECT)@12` → `C(triggerNum)@13` |

<a id="s-gmhtml"></a>

### S_GMHtml

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_GMHtml.java`；SHA-256：`8d95ed7683723167469a83c72e4c51cf8296241e1bfb1abab5b7090646889723`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_GMHtml(final int _objid, final String html)` @15 | `C(S_OPCODE_SHOWHTML)@16` → `D(_objid)@17` → `S(html)@18` |

<a id="s-helpmessage"></a>

### S_HelpMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_HelpMessage.java`；SHA-256：`69ca1d61b7913c44bd920d89a0951d707bcf3a7ee655af5ed4cb56d24a0ac6c2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_HelpMessage(final String name, final String info)` @16 | `C(S_OPCODE_NPCSHOUT)@17` → `C(0x00)@18` → `D(0x00000000)@19` → `S(name + " --> \\f4" + info)@20` |
| `public S_HelpMessage(final String string)` @37 | `C(S_OPCODE_NPCSHOUT)@38` → `C(0x00)@39` → `D(0x00000000)@40` → `S(string)@41` |

<a id="s-hiresoldier"></a>

### S_HireSoldier

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_HireSoldier.java`；SHA-256：`fb6a0508ba64560a6d5a62cb05186cc6ca95b451b138ba2258ebae6c9895078b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_HireSoldier(final L1PcInstance pc)` @11 | `H(0x0000)@13` → `H(0x0000)@14` → `H(0x0000)@15` → `S(pc.getName())@16` → `D(0x00000000)@17` → `H(0x0000)@18` |

<a id="s-hitreactionsuppresslist"></a>

### S_HitReactionSuppressList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_HitReactionSuppressList.java`；SHA-256：`f4c064afe45f593ee8cce910fd41b554df704f9a0a955d8998e3466d5fd3aa1c`。

局部常數（subcode／格式旗標／限制）：`CUSTOM_OPCODE=0xF2`；`SUBOPCODE=0x02`；`MAX_ENTRIES=512`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_HitReactionSuppressList()` @17 | `C(CUSTOM_OPCODE)@26` → `C(SUBOPCODE)@27` → `H(count)@28`<br>〔`for final int gfxId : gfxIds → if gfxId >= 0 && gfxId <= 0xFFFF && written < count`〕 `H(gfxId)@32` |

<a id="s-housemap"></a>

### S_HouseMap

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_HouseMap.java`；SHA-256：`5b356ee3812405ff08677350dc30b04ae56892d53cc1b47daa0f76e021574ada`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_HouseMap(final int objectId, final String house_number)` @17 | 委派 `buildPacket(objectId, house_number)@18` |
| `private void buildPacket(final int objectId, final String house_number)` @22 | `C(S_OPCODE_HOUSEMAP)@25` → `D(objectId)@26` → `D(number)@27` |

<a id="s-howmanymake"></a>

### S_HowManyMake

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_HowManyMake.java`；SHA-256：`25f4d9f97e01c40a9ec65ac1cc6229728f883523c7a6f2a48db8463bec670197`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_HowManyMake(final int objId, final int max, final String htmlId)` @27 | `C(S_OPCODE_INPUTAMOUNT)@29` → `D(objId)@30` → `D(0x00000000)@31` → `D(0x00000000)@32` → `D(0x00000000)@33` → `D(max)@34` → `H(0x0000)@35` → `S("request")@36` → `S(htmlId)@37` |
| `public S_HowManyMake(int objId,int max,String action,Object recipe)` @41 | 委派 `this(objId,max,action)@42` |

<a id="s-hpmeter"></a>

### S_HPMeter

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_HPMeter.java`；SHA-256：`a60d3bf921345e1835b9ead6c71549e0042252a2757c09dba2e67c1ef3b6143f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_HPMeter(final int objId, final int hpRatio)` @19 | 委派 `buildPacket(objId, hpRatio)@20` |
| `public S_HPMeter(final L1Character cha)` @28 | 委派 `buildPacket(objId, hpRatio)@35` |
| `private void buildPacket(final int objId, final int hpRatio)` @39 | `C(S_OPCODE_HPMETER)@40` → `D(objId)@41` → `C(hpRatio)@42` |

<a id="s-hpupdate"></a>

### S_HPUpdate

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_HPUpdate.java`；SHA-256：`f96021109939b861a6442fdb9b731f11e30340329ec62177f42bedff0ef1b29d`。

局部常數（subcode／格式旗標／限制）：`HP_CAP_RO=L1Character.MAX_HP_MP_RO`；`HP_CAP_STANDARD=L1Character.MAX_HP_MP_STANDARD`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_HPUpdate(final int currentHp, final int maxHp)` @41 | 委派 `buildPacket(currentHp, maxHp)@42` |
| `public S_HPUpdate(final L1PcInstance pc)` @50 | 委派 `buildPacket(pc.getCurrentHp(), pc.getMaxHp())@51` |
| `public void buildPacket(final int currentHp, final int maxHp)` @55 | `C(S_OPCODE_HPUPDATE)@57`<br>〔`if isRO`〕 `D(clampCurrent(currentHp, true))@61` → `D(clampMax(maxHp, true))@62`<br>〔`else`〕 `H(clampCurrent(currentHp, false))@65` → `H(clampMax(maxHp, false))@66` |

<a id="s-iconconfig"></a>

### S_IconConfig

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_IconConfig.java`；SHA-256：`bc4f1629a14cc656c82402ca482aa55661345c85866e403b353bd3c7aba0cca9`。

局部常數（subcode／格式旗標／限制）：`SKILL_ICON=0x9a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_IconConfig(final int subCode, final int type, final int time, final boolean second)` @25 | `C(S_OPCODE_PACKETBOX)@26` → `C(subCode)@27`<br>〔`switch subCode / case SKILL_ICON`〕 `H(time)@31` → `H(type)@32` → `H(0x00)@33` → `H(second ? 0x01 : 0x00)@35` |

<a id="s-icons"></a>

### S_Icons

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Icons.java`；SHA-256：`a79c613fb69d5765ac8eaf3b64f2f879afd026ec47e5ffaea5aa9f0e74da047b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Icons(final L1PcInstance pc)` @75 | 委派 `buildIcons1(pc, 0, 0)@76` |
| `public S_Icons(final L1PcInstance pc, final int overrideSkillId, final int overrideTimeSec)` @82 | 委派 `buildIcons1(pc, overrideSkillId, overrideTimeSec)@83` |
| `private void buildIcons1(final L1PcInstance pc, final int overrideSkillId, final int overrideTimeSec)` @86 | `C(S_OPCODE_PACKETBOX)@87` → `C(S_PacketBox.ICONS1)@88` → `D(data.length)@92` → `Byte(data)@326` |

<a id="s-identifydesc"></a>

### S_IdentifyDesc

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_IdentifyDesc.java`；SHA-256：`bf53ef84a9dc717357521347dd25b118e6220834925336aa8651c4b39b632123`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_IdentifyDesc(final L1ItemInstance item)` @18 | 委派 `buildPacket(item)@19` |
| `private void buildPacket(final L1ItemInstance item)` @23 | `C(S_OPCODE_IDENTIFYDESC)@24` → `H(item.getItem().getItemDescId())@25`<br>〔`switch item.getItem().getType2() / case 1`〕 `H(0x0086)@43` → `C(0x03)@44` → `S(name.toString())@45` → `S(item.getItem().getDmgSmall() + "+" + item.getEnchantLevel())@46` → `S(item.getItem().getDmgLarge() + "+" + item.getEnchantLevel())@47`<br>〔`switch item.getItem().getType2() / default → switch item.getItem().getItemId() / default`〕 `H(0x0087)@53` → `C(0x02)@54` → `S(name.toString())@55` → `S(Math.abs(item.getItem().get_ac()) + "+" + item.getEnchantLevel())@56`<br>〔`switch item.getItem().getType2() / case 1 → switch item.getItem().getType() / case 1`〕 `H(0x0089)@65` → `C(0x03)@66` → `S(name.toString())@67` → `S(String.valueOf(item.getChargeCount()))@68`<br>〔`switch item.getItem().getType2() / case 2 → switch item.getItem().getType() / case 2`〕 `H(0x008a)@72` → `C(0x02)@73` → `S(name.toString())@76`<br>〔`switch item.getItem().getType2() / case 7 → switch item.getItem().getType() / case 7`〕 `H(0x0088)@80` → `C(0x03)@81` → `S(name.toString())@82` → `S(String.valueOf(item.getItem().getFoodVolume()))@83`<br>〔`switch item.getItem().getType2() / default → switch item.getItem().getType() / default`〕 `H(0x008a)@87` → `C(0x02)@88` → `S(name.toString())@89`<br>〔`switch item.getItem().getType2() / default`〕 `S(String.valueOf(item.getWeight()))@92` |
| `public S_IdentifyDesc()` @100 | `C(S_OPCODE_IDENTIFYDESC)@104` → `H(item.getItem().getItemDescId())@105` → `H(134)@107` → `C(3)@108` → `S(item.getName())@109` → `S(item.getItem().getDmgSmall() + "+" + item.getEnchantLevel())@110` → `S(item.getItem().getDmgLarge() + "+" + item.getEnchantLevel())@111` → `S(String.valueOf(item.getWeight()))@112` |

<a id="s-initialabilitygrowth"></a>

### S_InitialAbilityGrowth

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_InitialAbilityGrowth.java`；SHA-256：`333fbe32fd60bb02d1584b75e892566158aadfb269ed90eb0b8f3ef1be3d2f2b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_InitialAbilityGrowth(final L1PcInstance pc)` @12 | 委派 `buildPacket(pc, growth[0], growth[1], growth[2], growth[3], growth[4], growth[5])@93` |
| `private void buildPacket(L1PcInstance pc, int Str, int Dex, int Con, int Wis, int Cha, int Int)` @115 | `C(S_OPCODE_CHARRESET)@120` → `C(0x04)@121` → `C(write1)@122` → `C(write2)@123` → `C(write3)@124` → `C(0x00)@125` |

<a id="s-invis"></a>

### S_Invis

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Invis.java`；SHA-256：`891515fe8213d326999ceb8537f9543fe37100da96de23e16b59560f587f24c1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Invis(final int objid, final int type)` @17 | 委派 `buildPacket(objid, type)@18` |
| `private void buildPacket(final int objid, final int type)` @22 | `C(S_OPCODE_INVIS)@24` → `D(objid)@25` → `C(type)@26` |

<a id="s-invlist"></a>

### S_InvList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_InvList.java`；SHA-256：`cd3c0c20a7285b0ef32d9305e0559cf7ea02030c2ff0665363ffad91521f2820`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_InvList(final List<L1ItemInstance> items)` @21 | `C(S_OPCODE_INVLIST)@22` → `C(count)@24`<br>〔`for int index = 0; index < count; index++`〕 `D(item.getId())@28` → `H(item.getItem().getPacketItemDescId())@29` → `C(item.getItem().getPacketUseType())@30`<br>〔`for int index = 0; index < count; index++ → if item.getChargeCount() <= 0`〕 `C(0)@32`<br>〔`for int index = 0; index < count; index++`〕 `C(item.getChargeCount())@34` → `H(item.get_gfxid())@35` → `C(item.isSealed() ? L1Item.toPacketBless(item.getBless()) + 128 : L1Item.toPacketBless(item.getBless()))@37` → `D((int) Math.min(item.getCount(), 2000000000))@38` → `C(statusX)@61` → `S(item.getViewName())@62`<br>〔`for int index = 0; index < count; index++ → if !item.isIdentified()`〕 `C(0)@64`<br>〔`for int index = 0; index < count; index++ → if S_ItemStatus.shouldUseLongStatus(item)`〕 `C(0)@66`<br>〔`for int index = 0; index < count; index++ → else`〕 `C(status.length)@69`<br>〔`for int index = 0; index < count; index++ → else → for int i = 0; i < j; i++`〕 `C(b)@74`<br>〔`for int index = 0; index < count; index++`〕 `C(10)@77` → `H(0)@78` → `D(0)@79` → `D(0)@80` |

<a id="s-itemamount"></a>

### S_ItemAmount

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ItemAmount.java`；SHA-256：`a1407126dab15dab3d37b3a017ed6966e08dade98564ffca3441c204bab9be18`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ItemAmount(final L1ItemInstance item, int test)` @18 | 委派 `buildPacket(item)@25` |
| `private void buildPacket(final L1ItemInstance item)` @29 | `C(S_OPCODE_ITEMAMOUNT)@30` → `D(item.getId())@31` → `S(item.getViewName())@32` → `D(count)@47`<br>〔`if !item.isIdentified()`〕 `C(0x00)@54`<br>〔`else`〕 `C(status.length)@58`<br>〔`else → for final byte b : status`〕 `C(b)@60` |

<a id="s-itemboard"></a>

### S_ItemBoard

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ItemBoard.java`；SHA-256：`5c039926eaf8455e0e7cacc178ed26a523de79e0681364d321bf22aa971eabfa`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ItemBoard(final int gfxid, final String msg)` @13 | `C(S_OPCODE_PACKETBOX)@14` → `C(190)@15` → `H(gfxid)@16` → `S(msg)@17` |

<a id="s-itemcolor"></a>

### S_ItemColor

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ItemColor.java`；SHA-256：`f4bbd6054b8076a37bdf3e8591d0ffdc536e293cbe9f371636d774fa60c78d42`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ItemColor(final L1ItemInstance item)` @18 | 委派 `buildPacket(item)@24` |
| `private void buildPacket(final L1ItemInstance item)` @28 | `C(S_OPCODE_ITEMCOLOR)@29` → `D(item.getId())@30` → `C(item.isSealed() ? L1Item.toPacketBless(item.getBless()) + 128 : L1Item.toPacketBless(item.getBless()))@34` |
| `public S_ItemColor(final L1ItemInstance item, int id)` @55 | `C(S_OPCODE_ITEMCOLOR)@56` → `D(item.getId())@57` → `C(id)@60` |

<a id="s-itemcount"></a>

### S_ItemCount

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ItemCount.java`；SHA-256：`eaf19618fbd008fbc571f52647fbda3920f824f88a7685e13a64e0cf9afefa50`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ItemCount(final int objId, final int max, final String cmd)` @27 | `C(S_OPCODE_INPUTAMOUNT)@29` → `D(objId)@30` → `D(0x00000000)@31` → `D(0x00000000)@32` → `D(0x00000000)@33` → `D(max)@34` → `H(0x0000)@35` → `S("request")@36` → `S(cmd)@37` |
| `public S_ItemCount(final int objId, final int max, final String html, final String cmd)` @49 | `C(S_OPCODE_INPUTAMOUNT)@51` → `D(objId)@52` → `D(0x00000000)@53` → `D(0x00000000)@54` → `D(0x00000000)@55` → `D(max)@56` → `H(0x0000)@57` → `S(html)@58` → `S(cmd)@59` |
| `public S_ItemCount(final int objId, final int min, final int max, final String html, final String cmd, final String[] data)` @63 | `C(S_OPCODE_INPUTAMOUNT)@65` → `D(objId)@66` → `D(0x00000000)@67` → `D(min)@68` → `D(min)@69` → `D(max)@70` → `H(0x0000)@71` → `S(html)@72` → `S(cmd)@73`<br>〔`if (data != null) && (1 <= data.length)`〕 `H(data.length)@75`<br>〔`if (data != null) && (1 <= data.length) → for final String datum : data`〕 `S(datum)@77` |
| `public S_ItemCount(final int objId, final int min, final int max, final String html, final String cmd)` @92 | `C(S_OPCODE_INPUTAMOUNT)@94` → `D(objId)@95` → `D(0x00000000)@96` → `D(min)@97` → `D(min)@98` → `D(max)@99` → `H(0x0000)@100` → `S(html)@101` → `S(cmd)@102` |
| `public S_ItemCount(int objId,int min,int max,String html,String cmd,UiOperationContext.AuctionSelection selection)` @106 | 委派 `this(objId,min,max,html,cmd)@107` |
| `public S_ItemCount(int objId,int min,int max,String html,String cmd,String[] data,Object recipe)` @111 | 委派 `this(objId,min,max,html,cmd,data)@112` |

<a id="s-itemerror"></a>

### S_ItemError

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ItemError.java`；SHA-256：`f8082ff7222c49bf72a3294012dbe819d3496f6efa4f3a586966577f31eb4436`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ItemError(final int skillid)` @16 | 委派 `buildPacket(skillid)@17` |
| `private void buildPacket(final int skillid)` @21 | `D(skillid)@24` |

<a id="s-itemname"></a>

### S_ItemName

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ItemName.java`；SHA-256：`498eb49f65ad9a308afaf98bbf91ac497d70c603e97ebc70a5c3623ee30172b0`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ItemName(final L1ItemInstance item)` @17 | `C(S_OPCODE_ITEMNAME)@23` → `D(item.getId())@24` → `S(item.getViewName())@25` |
| `public S_ItemName(final L1ItemInstance item, int id)` @32 | `C(S_OPCODE_ITEMNAME)@38` → `D(item.getId())@39` → `S(item.getViewName() + " ($" + id + ")")@40` |

<a id="s-itemstatus"></a>

### S_ItemStatus

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ItemStatus.java`；SHA-256：`6e69ff83cbbe91f7a8914a2e43bda498eb0d2fc5808e5218edea5cfe1d513eac`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ItemStatus(final L1ItemInstance item)` @18 | 委派 `buildPacket(item)@24` |
| `private void buildPacket(final L1ItemInstance item)` @28 | 〔`if shouldUseLongStatus(item, status)`〕 委派 `writeLongStatus(item, item.getViewName(), (int) Math.min(item.getCount(), 2000000000), status)@31`<br>`C(S_OPCODE_ITEMAMOUNT)@36` → `D(item.getId())@37` → `S(item.getViewName())@38` → `D(count)@43`<br>〔`if !item.isIdentified()`〕 `C(0x00)@48`<br>〔`else`〕 `C(inlineStatus.length)@51`<br>〔`else → for final byte b : inlineStatus`〕 `C(b)@53` |
| `public S_ItemStatus(final L1ItemInstance item, final long count)` @61 | 〔`if shouldUseLongStatus(item, status)`〕 委派 `writeLongStatus(item, item.getNumberedViewName(count), out_count, status)@65`<br>`C(S_OPCODE_ITEMAMOUNT)@71` → `D(item.getId())@72` → `S(item.getNumberedViewName(count))@73` → `D(out_count)@76`<br>〔`if !item.isIdentified()`〕 `C(0x00)@80`<br>〔`else`〕 `C(inlineStatus.length)@83`<br>〔`else → for final byte b : inlineStatus`〕 `C(b)@85` |
| `private void writeLongStatus(final L1ItemInstance item, final String viewName, final int count, final byte[] status)` @107 | `C(0xF2)@108` → `C(0x01)@109` → `D(item.getId())@110` → `S(viewName)@111` → `D(count)@112` → `H(status.length)@113`<br>〔`for final byte b : status`〕 `C(b)@115` |

<a id="s-karma"></a>

### S_Karma

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Karma.java`；SHA-256：`415b1e651d8ea6aa7aca1e50e2055fb48c041610aaf4d0f3c23212845cc9fdd6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Karma(final L1PcInstance pc)` @9 | `C(S_OPCODE_PACKETBOX)@10` → `C(0x57)@11` → `D(pc.getKarma())@12` |

<a id="s-killmessage"></a>

### S_KillMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_KillMessage.java`；SHA-256：`5758fe576dd6dcb6a4b25b51a91bad1231de81dc1e780cc4ff1de558d504ae4c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_KillMessage(final String name, final String msg, int i)` @33 | `C(S_OPCODE_NPCSHOUT)@34` → `C(0x00)@35` → `D(0x00000000)@36` → `S(" \\fY[" + name + "] " + msg)@37` |

<a id="s-lawful"></a>

### S_Lawful

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Lawful.java`；SHA-256：`e5e860264df795ce3c28e2387fa40e87c9aa1e800659a28ec430c420d1e8acdf`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Lawful(L1PcInstance pc)` @18 | 委派 `buildPacket(pc)@19` |
| `private void buildPacket(L1PcInstance pc)` @23 | `C(S_OPCODE_LAWFUL)@25` → `D(pc.getId())@26` → `H(pc.getLawful())@27` |
| `public S_Lawful(final int objid)` @35 | `C(S_OPCODE_LAWFUL)@36` → `D(objid)@37` → `H(-32768)@38` → `H(-32768)@39` → `H(-32768)@40` |

<a id="s-letter"></a>

### S_Letter

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Letter.java`；SHA-256：`19967c94bb49dcc383401b7461e55f5edeab57dce993e68a17236c1b65e38e68`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="s-light"></a>

### S_Light

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Light.java`；SHA-256：`d633c820c10ec2eda64381be17ea3adfbe00e256b0c212cf242414fed2815dd9`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Light(final int objid, final int type)` @17 | 委派 `buildPacket(objid, type)@18` |
| `private void buildPacket(final int objid, final int type)` @22 | `C(S_OPCODE_LIGHT)@24` → `D(objid)@25` → `C(type)@26` |

<a id="s-liquor"></a>

### S_Liquor

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Liquor.java`；SHA-256：`d45b43cdd6152348da1414e2febb06f2a5573bb2a4489feff2cef38c9b227e23`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Liquor(final int objecId)` @16 | `C(S_OPCODE_LIQUOR)@17` → `D(objecId)@18` → `C(0x01)@19` |
| `public S_Liquor(final int objecId, final int type)` @28 | `C(S_OPCODE_LIQUOR)@29` → `D(objecId)@30` → `C(type)@31` |

<a id="s-lock"></a>

### S_Lock

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Lock.java`；SHA-256：`0db0f932754f1f6da9adcb7f4fc9c6f32ef8db61e3094651be34299d394cc32c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Lock()` @17 | 委派 `buildPacket()@18` |
| `private void buildPacket()` @22 | `C(S_OPCODE_MOVELOCK)@23` → `C(0x00)@24` |

<a id="s-loginresult"></a>

### S_LoginResult

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_LoginResult.java`；SHA-256：`e821e56d72478a893518d754fe7d79812be558b5fdf47fea01b7d4e1882590ba`。

局部常數（subcode／格式旗標／限制）：`REASON_LOGIN_OK=0x0000`；`REASON_ACCESS_FAILED=0x0008`；`REASON_ACCOUNT_IN_USE=0x0016`；`EVENT_CANT_USE=0x001a`；`EVENT_CANT_LOGIN=0x001c`；`EVENT_REGISTER_ACCOUNTS=0x002f`；`EVENT_LAN_ERROR=0x0027`；`EVENT_MAIL_ERROR=0x000b`；`REASON_ACCOUNT_ALREADY_EXISTS=0x0007`；`EVENT_C_ERROR=0x0011`；`EVENT_RE_LOGIN=0x0004`；`EVENT_CLOCK_ERROR=0x0032`；`EVENT_NAME_ERROR=0x0034`；`EVENT_PASS_ERROR=0x0035`；`EVENT_PASS_CHECK=0x000a`；`EVENT_ERROR_USER=0x009b`；`EVENT_ERROR_PASS=0x0095`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_LoginResult(final int reason)` @103 | 委派 `buildPacket(reason)@104` |
| `private void buildPacket(final int reason)` @108 | `C(S_OPCODE_LOGINRESULT)@110` → `H(reason)@111` → `D(0x00000000)@113` → `D(0x00000000)@114` → `D(0x00000000)@115` |

<a id="s-magicequipment"></a>

### S_MagicEquipment

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_MagicEquipment.java`；SHA-256：`b6e76b4e0003906803e71d092ff1cc2b9e6051c82e2059ea733974ab2b14baa7`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_MagicEquipment(final int timeSec, final int gfxId)` @14 | `C(S_OPCODE_PACKETBOX)@15` → `C(0x9a)@16` → `H(timeSec)@17` → `D(gfxId)@18` |

<a id="s-mail"></a>

### S_Mail

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Mail.java`；SHA-256：`c5d358f13d64554108dc8abb56305147d07737473105ec66ba297fe330c59a16`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Mail(final ArrayList<L1Mail> mails, final int type)` @21 | `C(S_OPCODE_MAIL)@23` → `C(type)@24` → `H(mails.size())@25`<br>〔`for int i = 0; i < mails.size(); i++`〕 `D(mail.getId())@28` → `C(mail.getReadStatus())@29`<br>〔`for int i = 0; i < mails.size(); i++ → if dateText != null && !dateText.isEmpty() → if st.length >= 3`〕 `C(Integer.parseInt(st[0].trim()))@43` → `C(Integer.parseInt(st[1].trim()))@44` → `C(Integer.parseInt(st[2].trim()))@45` → `C(0)@48` → `C(0)@49` → `C(0)@50`<br>〔`for int i = 0; i < mails.size(); i++ → if dateText != null && !dateText.isEmpty() → else`〕 `C(0)@54` → `C(0)@55` → `C(0)@56`<br>〔`for int i = 0; i < mails.size(); i++ → else`〕 `C(0)@60` → `C(0)@61` → `C(0)@62`<br>〔`for int i = 0; i < mails.size(); i++`〕 `S(mail.getSenderName())@65` → `Byte(mail.getSubject())@66` → `C(0x00)@67` |
| `public S_Mail(final int type)` @80 | `C(S_OPCODE_MAIL)@81` → `C(type)@82` |
| `public S_Mail(final L1Mail mail, final int type)` @92 | 〔`switch type / case 0x40`〕 委派 `buildPacket_1(mail, type)@98`<br>〔`switch type / default`〕 委派 `buildPacket_2(mail, type)@102` |
| `private void buildPacket_1(final L1Mail mail, final int type)` @108 | `C(S_OPCODE_MAIL)@109` → `C(type)@110` → `D(mail.getId())@111` → `C(0x01)@112` |
| `private void buildPacket_2(final L1Mail mail, final int type)` @115 | `C(S_OPCODE_MAIL)@116` → `C(type)@117` → `D(mail.getId())@118` → `Byte(mail.getContent())@119` |

<a id="s-mapid"></a>

### S_MapID

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_MapID.java`；SHA-256：`e4f8e0f4d4b98bdc90333233a9daba53ffee547881905956ef2e1cb33e28fb68`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_MapID(final int mapid, final boolean isUnderwater)` @15 | `C(S_OPCODE_MAPID)@16` → `H(mapid)@17` → `C(isUnderwater ? 0x01 : 0x00)@18` |
| `public S_MapID(final L1PcInstance pc, final int mapid, final boolean isUnderwater)` @28 | `C(S_OPCODE_MAPID)@47` → `H(mapid)@48` → `C(isUnderwater ? 0x01 : 0x00)@49` |
| `public S_MapID(final int mapid)` @69 | `C(S_OPCODE_MAPID)@72` → `H(mapid)@73` → `C(0x00)@74` → `C(0x10)@76` → `H(0x00f8)@77` → `C(0x00)@78` → `C(0x00)@79` → `C(0x00)@80` → `C(0x00)@81` → `C(0x00)@82` → `C(0x10)@83` → `C(0x0d)@84` → `C(0x35)@85` → `C(0xc5)@86` |

<a id="s-message-yn"></a>

### S_Message_YN

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Message_YN.java`；SHA-256：`f5c6bad673e5af10c851fd6206624597c2514fb88b98fc1f3e574e935c6d0c45`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Message_YN(final int type)` @26 | 委派 `this(null, null, type)@27` |
| `public S_Message_YN(final Offer offer, final Ticket prerequisite, final int type)` @30 | `C(S_OPCODE_YES_NO)@33` → `H(0x0000)@34` → `D(0x00000000)@35` → `H(type)@36` |
| `public S_Message_YN(final String name)` @45 | `C(S_OPCODE_YES_NO)@48` → `H(0x0000)@49` → `D(_sequentialNumber.incrementAndGet())@50` → `H(0x00fc)@51` → `S(name)@52` |
| `public S_Message_YN(final int type, final String msg)` @61 | `C(S_OPCODE_YES_NO)@64` → `H(0x0000)@65` → `D(0x00000000)@66` → `H(type)@67` → `S(msg)@68` |
| `public S_Message_YN(final Offer offer, final Ticket prerequisite, final int type, final String msg)` @73 | `C(S_OPCODE_YES_NO)@76` → `H(0x0000)@77` → `D(0x00000000)@78` → `H(type)@79` → `S(msg)@80` |
| `public S_Message_YN(final int type, final String msg1, final String msg2)` @90 | `C(S_OPCODE_YES_NO)@93` → `H(0x0000)@94` → `D(0x00000000)@95` → `H(type)@96` → `S(msg1)@97` → `S(msg2)@98` |
| `public static S_Message_YN confirmation(final Offer offer, final Ticket prerequisite, final int requestId, final String text)` @105 | 委派 `S_Message_YN(offer, prerequisite, requestId, text, true)@106` |
| `private S_Message_YN(final Offer offer, final Ticket prerequisite, final int requestId, final String text, final boolean raw)` @108 | `C(S_OPCODE_YES_NO)@113` → `H(0)@113` → `D(requestId)@113` → `H(622)@113` → `S(text)@113` |

<a id="s-movecharpacket"></a>

### S_MoveCharPacket

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_MoveCharPacket.java`；SHA-256：`cfe7d3cd0080572bc275811b6eac90cb63e3dadb03383ddf95792d4bcd15d8b0`。

局部常數（subcode／格式旗標／限制）：`FINALIZED_LENGTH=16`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_MoveCharPacket(final L1Character cha)` @53 | 委派 `this(cha, captureLegacyMovement(cha))@54` |
| `public S_MoveCharPacket(final L1Character cha, final int sourceX, final int sourceY, final int heading)` @79 | 委派 `this(cha, captureSuppliedMovement(cha, sourceX, sourceY, heading))@81` |
| `public S_MoveCharPacket(final L1Character cha, final L1Character.MovementSnapshot snapshot)` @98 | `C(S_OPCODE_MOVEOBJECT)@124` → `D(_objectId)@125` → `H(snapshot.sourceX())@126` → `H(snapshot.sourceY())@127` → `C(heading)@128` → `C(0x80)@129` |

<a id="s-movenpcpacket"></a>

### S_MoveNpcPacket

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_MoveNpcPacket.java`；SHA-256：`f8dea418f0a77e429d635fd916658e8b0e5feeab9be7b54116ecaecbbe5c580e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_MoveNpcPacket(final L1MonsterInstance npc, final int x, final int y, final int heading)` @22 | `C(S_OPCODE_MOVEOBJECT)@24` → `D(npc.getId())@25` → `H(x)@26` → `H(y)@27` → `C(heading)@28` → `C(0x80)@30` |
| `public S_MoveNpcPacket(final L1Character cha)` @38 | `C(S_OPCODE_MOVEOBJECT)@40` → `D(cha.getId())@41` → `H(cha.getX())@42` → `H(cha.getY())@43` → `C(cha.getHeading())@44` → `C(0x80)@46` |

<a id="s-mpupdate"></a>

### S_MPUpdate

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_MPUpdate.java`；SHA-256：`966ebd8c869f5bf0e50e14127ca9fd5adc37710d5dc90678819a8d6e71eef53d`。

局部常數（subcode／格式旗標／限制）：`MP_CAP_RO=L1Character.MAX_HP_MP_RO`；`MP_CAP_STANDARD=L1Character.MAX_HP_MP_STANDARD`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_MPUpdate(final int currentmp, final int maxmp)` @41 | 委派 `buildPacket(currentmp, maxmp)@42` |
| `public S_MPUpdate(final L1PcInstance pc)` @50 | 委派 `buildPacket(pc.getCurrentMp(), pc.getMaxMp())@51` |
| `private void buildPacket(final int currentmp, final int maxmp)` @60 | `C(S_OPCODE_MPUPDATE)@62`<br>〔`if isRO`〕 `D(clampCurrent(currentmp, true))@66` → `D(clampMax(maxmp, true))@67`<br>〔`else`〕 `H(clampCurrent(currentmp, false))@70` → `H(clampMax(maxmp, false))@71` |

<a id="s-newcharpacket"></a>

### S_NewCharPacket

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NewCharPacket.java`；SHA-256：`89f9f783ec9e38f16fd21c30cbb32be4c86d021750b74083ed6a547fac08707e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NewCharPacket(final L1PcInstance pc)` @21 | 委派 `buildPacket(pc)@22` |
| `private void buildPacket(final L1PcInstance pc)` @26 | `C(S_OPCODE_NEWCHARPACK)@27` → `S(pc.getName())@28` → `S("")@29` → `C(pc.getType())@30` → `C(pc.get_sex())@31` → `H(pc.getLawful())@32`<br>〔`if ConfigClient.isROClient()`〕 `D(pc.getMaxHp())@36` → `D(pc.getMaxMp())@37`<br>〔`else`〕 `H(pc.getMaxHp())@39` → `H(pc.getMaxMp())@40`<br>`C(pc.getAc())@43` → `C(pc.getLevel())@44` → `C(pc.getStr())@45` → `C(pc.getDex())@46` → `C(pc.getCon())@47` → `C(pc.getWis())@48` → `C(pc.getCha())@49` → `C(pc.getInt())@50` → `C(0x00)@53` → `D(time)@57` → `C(checkcode & 0xFF)@60` |

<a id="s-newmaster"></a>

### S_NewMaster

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NewMaster.java`；SHA-256：`8e0542080d330c47937166537416fdaf61b2115c7d753c528ddddb5f53de996e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NewMaster(final String name, final L1NpcInstance npc)` @19 | 委派 `buildPacket(name, npc)@20` |
| `private void buildPacket(final String name, final L1NpcInstance npc)` @24 | `C(S_OPCODE_NEWMASTER)@25` → `D(npc.getId())@26` → `S(name)@27` |
| `public S_NewMaster(final L1NpcInstance npc)` @30 | `C(S_OPCODE_NEWMASTER)@31` → `D(npc.getId())@32` → `S("")@33` |

<a id="s-nosell"></a>

### S_NoSell

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NoSell.java`；SHA-256：`0ddcd75b29cf74b46662475af866270fea8c963619250b09fd3d03a4c0521ab9`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NoSell(final L1NpcInstance npc)` @8 | 委派 `buildPacket(npc)@9` |
| `private void buildPacket(final L1NpcInstance npc)` @13 | `C(S_OPCODE_SHOWHTML)@14` → `D(npc.getId())@15` → `S("nosell")@16` → `C(0x01)@17` |

<a id="s-npcchat"></a>

### S_NpcChat

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NpcChat.java`；SHA-256：`f24851772a3111921f9de1a3301b47da5f7503066ea4293310e2567b12b3c12e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NpcChat(final L1NpcInstance npc, final String chat)` @20 | `C(S_OPCODE_NPCSHOUT)@21` → `C(0x00)@23` → `D(npc.getId())@24` → `S(npc.getNameId() + ": " + chat)@25` |
| `public S_NpcChat(final L1NpcInstance npc, final String chat, boolean name)` @35 | `C(S_OPCODE_NPCSHOUT)@36` → `C(0x00)@38` → `D(npc.getId())@39` → `S((name? npc.getNameId() + ": ":"") + chat)@40` |
| `public S_NpcChat(final L1PcInstance pc, final String chat)` @49 | `C(S_OPCODE_NPCSHOUT)@50` → `C(0x00)@52` → `D(pc.getId())@53` → `S(pc.getName() + ": " + chat)@54` |

<a id="s-npcchatglobal"></a>

### S_NpcChatGlobal

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NpcChatGlobal.java`；SHA-256：`de1fd1d888f1c77b8e6d2f6e83d6ac4eb34efd9946a0514f7bf76b8bf1f2d4d2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NpcChatGlobal(final L1NpcInstance npc, final String chat)` @14 | 委派 `buildPacket(npc, chat)@15` |
| `private void buildPacket(final L1NpcInstance npc, final String chat)` @19 | `C(S_OPCODE_NPCSHOUT)@20` → `C(0x03)@21` → `D(npc.getId())@22` → `S("[" + npc.getNameId() + "] " + chat)@23` |

<a id="s-npcchatshouting"></a>

### S_NpcChatShouting

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NpcChatShouting.java`；SHA-256：`57517a252c5fe3cf12906c11bd4c648f56fd5994ef15cc55f4677560242140f4`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NpcChatShouting(final L1NpcInstance npc, final String chat)` @19 | 委派 `buildPacket(npc, chat)@20` |
| `private void buildPacket(final L1NpcInstance npc, final String chat)` @24 | `C(S_OPCODE_NPCSHOUT)@25` → `C(0x02)@27` → `D(npc.getId())@28` → `S("<" + npc.getNameId() + "> " + chat)@29` |

<a id="s-npcpack"></a>

### S_NPCPack

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack.java`；SHA-256：`dee24b66cd725b3c70a110da0dbfbb61f9e20d6a13b00a798dff046e0251f0aa`。

局部常數（subcode／格式旗標／限制）：`STATUS_POISON=1`；`STATUS_PC=4`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack(final L1NpcInstance npc)` @28 | `C(S_OPCODE_CHARPACK)@29` → `H(npc.getX())@30` → `H(npc.getY())@31` → `D(npc.getId())@32`<br>〔`if npc.getTempCharGfx() == 0`〕 `H(npc.getGfxId())@35`<br>〔`else`〕 `H(npc.getTempCharGfx())@38`<br>〔`if npc.getNpcTemplate().is_doppel() && (npc.getGfxId() != 31)`〕 `C(0x04)@42`<br>〔`else`〕 `C(npc.getStatus())@45`<br>`C(npc.getHeading())@48` → `C(npc.getChaLightSize())@49` → `C(npc.getMoveSpeed())@50` → `D((int) npc.getExp())@51` → `H(npc.getTempLawful())@52` → `S(npc.getNameId())@53` → `S(npc.getTitle())@55` → `C(status)@76` → `D(0x00000000)@78` → `S(null)@79` → `S(null)@80` → `C(0x00)@82` → `C(0xff)@84` → `C(0x00)@85` → `C(npc.getLevel())@87` → `C(0x00)@88` → `C(0xff)@89` → `C(0xff)@90` |
| `public S_NPCPack(final L1NpcInstance npc, final L1PcInstance pc)` @99 | `C(S_OPCODE_CHARPACK)@100` → `H(npc.getX())@101` → `H(npc.getY())@102` → `D(npc.getId())@103`<br>〔`if npc.getTempCharGfx() == 0`〕 `H(npc.getGfxId())@106`<br>〔`else`〕 `H(npc.getTempCharGfx())@109`<br>〔`if npc.getNpcTemplate().is_doppel() && (npc.getGfxId() != 31)`〕 `C(0x04)@113`<br>〔`else`〕 `C(npc.getStatus())@116`<br>`C(npc.getHeading())@119` → `C(npc.getChaLightSize())@120` → `C(npc.getMoveSpeed())@121` → `D((int) npc.getExp())@122` → `H(npc.getTempLawful())@123` → `S(name)@131` → `S(npc.getTitle())@134` → `C(status)@155` → `D(0x00000000)@157` → `S(null)@158` → `S(null)@159` → `C(0x00)@161` → `C(0xff)@163` → `C(0x00)@164` → `C(npc.getLevel())@166` → `C(0x00)@167` → `C(0xff)@168` → `C(0xff)@169` |

<a id="s-npcpack-d"></a>

### S_NPCPack_D

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_D.java`；SHA-256：`489af1914108cf85b824fc5cfe3d1a40e89444d5e746035da8d83ff76dc53635`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_D(final L1DwarfInstance npc)` @19 | `C(S_OPCODE_CHARPACK)@20` → `H(npc.getX())@21` → `H(npc.getY())@22` → `D(npc.getId())@23` → `H(npc.getGfxId())@25` → `C(npc.getStatus())@27` → `C(npc.getHeading())@29` → `C(npc.getChaLightSize())@30` → `C(npc.getMoveSpeed())@31` → `D((int) npc.getExp())@32` → `H(npc.getTempLawful())@33` → `S(npc.getNameId())@34` → `S(null)@35` → `C(0)@37` → `D(0x00000000)@39` → `S(null)@40` → `S(null)@41` → `C(0x00)@42` → `C(0xff)@43` → `C(0x00)@44` → `C(0x00)@45` → `C(0x00)@46` → `C(0xff)@47` → `C(0xff)@48` |
| `public S_NPCPack_D(final L1DwarfInstance npc, final L1PcInstance pc)` @57 | `C(S_OPCODE_CHARPACK)@58` → `H(npc.getX())@59` → `H(npc.getY())@60` → `D(npc.getId())@61` → `H(npc.getGfxId())@63` → `C(npc.getStatus())@65` → `C(npc.getHeading())@67` → `C(npc.getChaLightSize())@68` → `C(npc.getMoveSpeed())@69` → `D((int) npc.getExp())@70` → `H(npc.getTempLawful())@71` → `S(name)@79` → `S(null)@82` → `C(0)@84` → `D(0x00000000)@86` → `S(null)@87` → `S(null)@88` → `C(0x00)@89` → `C(0xff)@90` → `C(0x00)@91` → `C(0x00)@92` → `C(0x00)@93` → `C(0xff)@94` → `C(0xff)@95` |

<a id="s-npcpack-de"></a>

### S_NPCPack_De

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_De.java`；SHA-256：`3a5e568d9193bb9d73f9d8a7af4e10f905c3bee7727a1b2677bc919c1a769f5e`。

局部常數（subcode／格式旗標／限制）：`STATUS_POISON=1`；`STATUS_INVISIBLE=2`；`STATUS_PC=4`；`STATUS_BRAVE=16`；`STATUS_ELFBRAVE=32`；`STATUS_FASTMOVABLE=64`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_De(final L1DeInstance de)` @29 | `C(S_OPCODE_CHARPACK)@52` → `H(de.getX())@53` → `H(de.getY())@54` → `D(de.getId())@55` → `H(de.getTempCharGfx())@57` → `C(de.getStatus())@59` → `C(de.getHeading())@61` → `C(de.getChaLightSize())@63` → `C(de.getMoveSpeed())@64` → `D(0x00000000)@65` → `H(de.getLawful())@66` → `S(de.getNameId())@68` → `S(de.getTitle())@69` → `C(status)@70` → `D(de.getClanid())@71` → `S(de.getClanname())@72` → `S(null)@73` → `C(0x00)@75` → `C(0xff)@77` → `C(de.hasSkillEffect(STATUS_BRAVE3) ? 0x08 : 0x00)@78` → `C(0x00)@79` → `C(0x00)@80` → `C(0xff)@81` → `C(0xff)@82` |

<a id="s-npcpack-doll"></a>

### S_NPCPack_Doll

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_Doll.java`；SHA-256：`3e162622cdc9789fc5f166e68662d24b94a6b9350236db9f0e9fc7e9425971c2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_Doll(final L1DollInstance pet, final L1PcInstance pc)` @21 | `C(S_OPCODE_CHARPACK)@26` → `H(pet.getX())@27` → `H(pet.getY())@28` → `D(pet.getId())@29` → `H(pet.getGfxId())@30` → `C(pet.getStatus())@31` → `C(pet.getHeading())@32` → `C(0x00)@33` → `C(pet.getMoveSpeed())@34` → `D(0x00000000)@35` → `H(0x0000)@36` → `S(pet.getNameId())@38` → `S(pet.getTitle())@39` → `C(0x00)@41` → `D(0x00000000)@42` → `S(null)@44`<br>〔`if pet.getMaster() != null → if pet.getMaster() instanceof L1PcInstance`〕 `S(pet.getMaster().getName())@48`<br>〔`if pet.getMaster() != null → if pet.getMaster() instanceof L1NpcInstance`〕 `S(npc.getNameId())@51`<br>〔`else`〕 `S("")@55`<br>`C(0x00)@58` → `C(0xff)@60` → `C(0x00)@61` → `C(0x00)@62` → `C(0x00)@64` → `C(0xff)@65` → `C(0xff)@66` |

<a id="s-npcpack-eff"></a>

### S_NPCPack_Eff

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_Eff.java`；SHA-256：`f10f7fd37d085ab01360b6ee77615695bc8165187b136a70a2f30cfe6777bafb`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_Eff(final L1EffectInstance npc)` @18 | `C(S_OPCODE_CHARPACK)@19` → `H(npc.getX())@20` → `H(npc.getY())@21` → `D(npc.getId())@22` → `H(npc.getGfxId())@24` → `C(npc.getStatus())@26` → `C(npc.getHeading())@28` → `C(npc.getChaLightSize())@29` → `C(npc.getMoveSpeed())@30` → `D((int) npc.getExp())@31` → `H(npc.getTempLawful())@32` → `S(npc.getNameId())@33` → `S(npc.getTitle())@35` → `C(0x00)@37` → `D(0x00000000)@39` → `S(null)@40` → `S(null)@41` → `C(0x00)@43` → `C(0xff)@45` → `C(0x00)@46` → `C(0x00)@47` → `C(0x00)@49` → `C(0xff)@50` → `C(0xff)@51` |

<a id="s-npcpack-f"></a>

### S_NPCPack_F

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_F.java`；SHA-256：`de4d7bc82922b581eb042ca772036500f9a01e5257cea2db0022d4e2cfc0a247`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_F(final L1FieldObjectInstance npc)` @20 | `C(S_OPCODE_CHARPACK)@21` → `H(npc.getX())@22` → `H(npc.getY())@23` → `D(npc.getId())@24` → `H(npc.getGfxId())@26` → `C(npc.getStatus())@28` → `C(npc.getHeading())@30` → `C(npc.getChaLightSize())@31` → `C(npc.getMoveSpeed())@32` → `D((int) npc.getExp())@33` → `H(npc.getTempLawful())@34` → `S(npc.getNameId())@35`<br>〔`if sceneryHtml != null`〕 `S(sceneryHtml)@39`<br>〔`else`〕 `S(null)@42`<br>`C(0x00)@45` → `D(0x00000000)@47` → `S(null)@48` → `S(null)@49` → `C(0x00)@51` → `C(0xff)@53` → `C(0x00)@54` → `C(0x00)@55` → `C(0x00)@56` → `C(0xff)@57` → `C(0xff)@58` |
| `public S_NPCPack_F(final L1FieldObjectInstance npc, final L1PcInstance pc)` @67 | `C(S_OPCODE_CHARPACK)@68` → `H(npc.getX())@69` → `H(npc.getY())@70` → `D(npc.getId())@71` → `H(npc.getGfxId())@73` → `C(npc.getStatus())@75` → `C(npc.getHeading())@77` → `C(npc.getChaLightSize())@78` → `C(npc.getMoveSpeed())@79` → `D((int) npc.getExp())@80` → `H(npc.getTempLawful())@81` → `S(name)@89`<br>〔`if sceneryHtml != null`〕 `S(sceneryHtml)@94`<br>〔`else`〕 `S(null)@97`<br>`C(0x00)@100` → `D(0x00000000)@102` → `S(null)@103` → `S(null)@104` → `C(0x00)@106` → `C(0xff)@108` → `C(0x00)@109` → `C(0x00)@110` → `C(0x00)@111` → `C(0xff)@112` → `C(0xff)@113` |

<a id="s-npcpack-ill"></a>

### S_NPCPack_Ill

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_Ill.java`；SHA-256：`e35658e9ca7eaa4b8e668b81cdcc4e8710b9262178d6576e72c230d0662b3dc6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_Ill(final L1IllusoryInstance de)` @18 | `C(S_OPCODE_CHARPACK)@19` → `H(de.getX())@20` → `H(de.getY())@21` → `D(de.getId())@22` → `H(de.getTempCharGfx())@24` → `C(de.getStatus())@26` → `C(de.getHeading())@28` → `C(de.getChaLightSize())@29` → `C(de.getMoveSpeed())@30` → `D(0x00000000)@31` → `H(de.getLawful())@32` → `S(de.getNameId())@34` → `S(de.getTitle())@35` → `C(0x00)@37` → `D(0x00000000)@38` → `S(null)@39` → `S(null)@40` → `C(0x00)@42` → `C(0xff)@44` → `C(0x00)@45` → `C(0x00)@46` → `C(0x00)@47` → `C(0xff)@48` → `C(0xff)@49` |

<a id="s-npcpack-item"></a>

### S_NPCPack_Item

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_Item.java`；SHA-256：`80b2fa74a278161a3b30ea1f0807e1aaceae1ceac0de839b9baba3c9528f24ee`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_Item(final L1ItemInstance item)` @18 | 委派 `buildPacket(item)@19` |
| `private void buildPacket(final L1ItemInstance item)` @23 | `C(S_OPCODE_CHARPACK)@24` → `H(item.getX())@25` → `H(item.getY())@26` → `D(item.getId())@27` → `H(item.getItem().getGroundGfxId())@28` → `C(0x00)@29` → `C(0x00)@30` → `C(item.isNowLighting()? item.getItem().getLightRange() : 0x00)@31` → `C(0x00)@32` → `D((int) Math.min(item.getCount(), 2000000000))@33` → `H(0x0000)@34` → `S(name)@69` → `S(null)@70` → `C(0x00)@71` → `D(0x00000000)@72` → `S(null)@73` → `S(null)@74` → `C(0x00)@76` → `C(0xff)@78` → `C(0x00)@79` → `C(0x00)@80` → `C(0x00)@81` → `C(0xff)@82` → `C(0xff)@83` |

<a id="s-npcpack-m"></a>

### S_NPCPack_M

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_M.java`；SHA-256：`b4c2e16d018f33497c6e2bdd58ebab8afc05ee225f514b43b3ca09bbae0ab9e2`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_M(final L1MerchantInstance npc)` @19 | `C(S_OPCODE_CHARPACK)@20` → `H(npc.getX())@21` → `H(npc.getY())@22` → `D(npc.getId())@23` → `H(npc.getGfxId())@25` → `C(npc.getStatus())@27` → `C(npc.getHeading())@29` → `C(npc.getChaLightSize())@30` → `C(npc.getMoveSpeed())@31` → `D((int) npc.getExp())@32` → `H(npc.getTempLawful())@33` → `S(npc.getNameId())@34` → `S(null)@35` → `C(0)@37` → `D(0x00000000)@39` → `S(null)@40` → `S(null)@41` → `C(0x00)@42` → `C(0xff)@43` → `C(0x00)@44` → `C(0x00)@45` → `C(0x00)@46` → `C(0xff)@47` → `C(0xff)@48` |
| `public S_NPCPack_M(final L1MerchantInstance npc, final L1PcInstance pc)` @57 | `C(S_OPCODE_CHARPACK)@58` → `H(npc.getX())@59` → `H(npc.getY())@60` → `D(npc.getId())@61` → `H(npc.getGfxId())@63` → `C(npc.getStatus())@65` → `C(npc.getHeading())@67` → `C(npc.getChaLightSize())@68` → `C(npc.getMoveSpeed())@69` → `D((int) npc.getExp())@70` → `H(npc.getTempLawful())@71` → `S(name)@79` → `S(null)@82` → `C(0)@84` → `D(0x00000000)@86` → `S(null)@87` → `S(null)@88` → `C(0x00)@89` → `C(0xff)@90` → `C(0x00)@91` → `C(0x00)@92` → `C(0x00)@93` → `C(0xff)@94` → `C(0xff)@95` |

<a id="s-npcpack-pet"></a>

### S_NPCPack_Pet

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_Pet.java`；SHA-256：`4006924b41226494b8eac4073881c0194bbf4cb5e5d124b4c36a8fdd23fba303`。

局部常數（subcode／格式旗標／限制）：`STATUS_POISON=1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_Pet(final L1PetInstance pet, final L1PcInstance pc)` @29 | 委派 `buildPacket(pet, pc)@30` |
| `private void buildPacket(final L1PetInstance pet, final L1PcInstance pc)` @34 | `C(S_OPCODE_CHARPACK)@40` → `H(pet.getX())@41` → `H(pet.getY())@42` → `D(pet.getId())@43` → `H(pet.getGfxId())@44` → `C(pet.getStatus())@45` → `C(pet.getHeading())@46` → `C(pet.getChaLightSize())@47` → `C(pet.getMoveSpeed())@48` → `D((int) pet.getExp())@49` → `H(pet.getTempLawful())@50` → `S(pet.getName())@51` → `S(pet.getTitle())@52` → `C(status)@59` → `D(0x00000000)@60` → `S(null)@61` → `S(stringBuilder.toString())@77` → `C(0x00)@79`<br>〔`if (pet.getMaster() != null) && (pet.getMaster().getId() == pc.getId())`〕 `C(100 * pet.getCurrentHp() / pet.getMaxHp())@83`<br>〔`else`〕 `C(0xFF)@86`<br>`C(0x00)@89` → `C(0x00)@90` → `C(0x00)@92` → `C(0xFF)@93` → `C(0xFF)@94` |

<a id="s-npcpack-signboard"></a>

### S_NPCPack_Signboard

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_Signboard.java`；SHA-256：`a25115c06513a3287425a4f0ac2abf6438f45e2aa592155531b37edcfb6ca4e5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_Signboard(final L1SignboardInstance signboard)` @18 | `C(S_OPCODE_CHARPACK)@19` → `H(signboard.getX())@20` → `H(signboard.getY())@21` → `D(signboard.getId())@22` → `H(signboard.getGfxId())@23` → `C(0x00)@24` → `C(this.getDirection(signboard.getHeading()))@25` → `C(0x00)@26` → `C(0x00)@27` → `D(0x00000000)@28` → `H(0x0000)@29` → `S(null)@30` → `S(signboard.getName())@31` → `C(0x00)@32` → `D(0x00000000)@33` → `S(null)@34` → `S(null)@35` → `C(0x00)@46` → `C(0xFF)@48` → `C(0x00)@49` → `C(0x00)@50` → `C(0x00)@51` → `C(0xFF)@52` → `C(0xFF)@53` |

<a id="s-npcpack-summon"></a>

### S_NPCPack_Summon

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCPack_Summon.java`；SHA-256：`85592198bfcd61b7734602370d7ec0c1b68d2464cca3028ce2e44a4ca58be4f1`。

局部常數（subcode／格式旗標／限制）：`STATUS_POISON=1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCPack_Summon(final L1SummonInstance pet, final L1PcInstance pc)` @30 | 委派 `buildPacket(pet, pc, true)@31` |
| `public S_NPCPack_Summon(final L1SummonInstance pet, final L1PcInstance pc, final boolean isCheckMaster)` @35 | 委派 `buildPacket(pet, pc, isCheckMaster)@37` |
| `private void buildPacket(final L1SummonInstance pet, final L1PcInstance pc, final boolean isCheckMaster)` @41 | `C(S_OPCODE_CHARPACK)@43` → `H(pet.getX())@44` → `H(pet.getY())@45` → `D(pet.getId())@46` → `H(pet.getGfxId())@47` → `C(pet.getStatus())@48` → `C(pet.getHeading())@49` → `C(pet.getChaLightSize())@50` → `C(pet.getMoveSpeed())@51` → `D(0x00000000)@52` → `H(0x0000)@53` → `S(pet.getNameId())@54` → `S(pet.getTitle())@55` → `C(status)@62` → `D(0x00000000)@63` → `S(null)@64` → `S(stringBuilder.toString())@88` → `C(0x00)@90`<br>〔`if !(pet.getMaster() instanceof L1NpcInstance) → if (pet.getMaster() != null) && (pet.getMaster().getId() == pc.getId())`〕 `C(percent)@96`<br>〔`if !(pet.getMaster() instanceof L1NpcInstance) → else`〕 `C(0xFF)@98`<br>〔`else`〕 `C(0xFF)@101`<br>`C(0x00)@104` → `C(0x00)@105` → `C(0x00)@107` → `C(0xFF)@108` → `C(0xFF)@109` |

<a id="s-npctalkactiontpurl"></a>

### S_NPCTalkActionTPUrl

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCTalkActionTPUrl.java`；SHA-256：`834bab855ebb80cae0025dbda629e15587dbf14e0c0c6c2d3e8be2852f51faa6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCTalkActionTPUrl(final L1NpcTalkData cha, final Object[] prices, final int objid)` @20 | 委派 `buildPacket(cha, prices, objid)@21` |
| `private void buildPacket(final L1NpcTalkData npc, final Object[] prices, final int objid)` @25 | `C(S_OPCODE_SHOWHTML)@28` → `D(objid)@29` → `S(htmlid)@30` → `H(0x01)@31` → `H(prices.length)@32`<br>〔`for final Object price : prices`〕 `S(String.valueOf(((Integer) price).intValue()))@35` |

<a id="s-npctalkreturn"></a>

### S_NPCTalkReturn

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_NPCTalkReturn.java`；SHA-256：`bfc54936df47f821b54a6acedbe273225e4d53a2b247f21ecf2ce35c653a5847`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_NPCTalkReturn(int objid, String htmlid, L1PcInstance pc, List<Integer> list)` @37 | `C(S_OPCODE_SHOWHTML)@39` → `D(objid)@40` → `S(htmlid)@41` → `H(0x01)@42` → `H(0x0b)@43`<br>〔`for final Integer v : list`〕 `S(datum.getNameId())@49`<br>〔`if list.size() < 11 → for int i = 0 ; i < x ; i++`〕 `S(" ")@54` |
| `public S_NPCTalkReturn(int objid, String htmlid, List<L1ItemInstance> list, L1PcInstance pc)` @67 | `C(S_OPCODE_SHOWHTML)@69` → `D(objid)@70` → `S(htmlid)@71` → `H(0x01)@72` → `H(0x0b)@73`<br>〔`for final L1ItemInstance datum : list`〕 `S(datum.getViewName())@78`<br>〔`if list.size() < 11 → for int i = 0 ; i < x ; i++`〕 `S(" ")@83` |
| `public S_NPCTalkReturn(final L1NpcTalkData npc, final int objid, final int action, final String[] data)` @96 | 委派 `buildPacket(objid, htmlid, data)@110` |
| `public S_NPCTalkReturn(final L1NpcTalkData npc, final int objid, final int action)` @120 | 委派 `this(npc, objid, action, null)@121` |
| `public S_NPCTalkReturn(final int objid, final String htmlid, final String[] data)` @131 | 委派 `buildPacket(objid, htmlid, data)@132` |
| `public S_NPCTalkReturn(final int objid, final String htmlid)` @141 | 委派 `buildPacket(objid, htmlid, null)@142` |
| `public S_NPCTalkReturn(final int objid, final L1NpcHtml html)` @151 | 委派 `buildPacket(objid, html.getName(), html.getArgs())@152` |
| `public S_NPCTalkReturn(int objid,String htmlid,String[] data,UiOperationContext.Offer offer, UiOperationContext.Ticket prerequisite)` @157 | 委派 `this(objid,htmlid,data)@159` |
| `private void buildPacket(final int objid, final String htmlid, final String[] data)` @165 | `C(S_OPCODE_SHOWHTML)@167` → `D(objid)@168` → `S(htmlid)@169`<br>〔`if (data != null) && (1 <= data.length)`〕 `H(0x01)@171` → `H(data.length)@172`<br>〔`if (data != null) && (1 <= data.length) → for final String datum : data`〕 `S(datum)@174`<br>〔`else`〕 `H(0x00)@178` → `H(0x00)@179` |

<a id="s-othercharpacks"></a>

### S_OtherCharPacks

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_OtherCharPacks.java`；SHA-256：`95ee7228e1c9d14d1ae42ec35dbac5fd37166ab2a5ee06f94406960babf99ce5`。

局部常數（subcode／格式旗標／限制）：`STATUS_POISON=1`；`STATUS_INVISIBLE=2`；`STATUS_PC=4`；`STATUS_BRAVE=16`；`STATUS_ELFBRAVE=32`；`STATUS_FASTMOVABLE=64`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_OtherCharPacks(final L1PcInstance pc)` @29 | 委派 `this(pc, null)@30` |
| `public S_OtherCharPacks(final L1PcInstance pc, final L1PcInstance viewer)` @36 | `C(S_OPCODE_CHARPACK)@60` → `H(pc.getX())@61` → `H(pc.getY())@62` → `D(pc.getId())@63`<br>〔`if pc.isDead()`〕 `H(pc.getTempCharGfxAtDead())@66`<br>〔`else`〕 `H(pc.getTempCharGfx())@69`<br>〔`if pc.isDead()`〕 `C(pc.getStatus())@73`<br>〔`else`〕 `C(pc.getCurrentWeapon())@75`<br>`C(pc.getHeading())@78` → `C(pc.getChaLightSize())@80` → `C(pc.getMoveSpeed())@81` → `D(0x00000000)@82` → `H(pc.getLawful())@83` → `S(stringBuilder.toString())@91` → `S(hideOthersIdentity ? "" : pc.getTitle())@92` → `C(status)@93` → `D(hideOthersIdentity ? 0 : pc.getClanid())@94` → `S(hideOthersIdentity ? "" : pc.getClanname())@95` → `S(null)@96` → `C(hideOthersIdentity ? 0 : (pc.getClanRank() << 4))@107` → `C(0xff)@110`<br>〔`if pc.hasSkillEffect(STATUS_BRAVE3)`〕 `C(0x08)@112`<br>〔`else`〕 `C(0x00)@115`<br>`C(0x00)@117` → `C(0x00)@118` → `C(0xff)@119` → `C(0xff)@120` |

<a id="s-owncharattrdef"></a>

### S_OwnCharAttrDef

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_OwnCharAttrDef.java`；SHA-256：`6d817d26e4bfd369b819f83e655fd4b7859a34d28b12a14646a0cdad113d013b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_OwnCharAttrDef(final L1PcInstance pc)` @19 | 委派 `buildPacket(pc)@20` |
| `private void buildPacket(final L1PcInstance pc)` @24 | `C(S_OPCODE_OWNCHARATTRDEF)@25` → `C(ac)@28` → `H(pc.getFire())@29` → `H(pc.getWater())@30` → `H(pc.getWind())@31` → `H(pc.getEarth())@32` |
| `public S_OwnCharAttrDef()` @39 | `C(S_OPCODE_OWNCHARATTRDEF)@40` → `C(-99)@41` → `H(90)@42` → `H(85)@43` → `H(80)@44` → `H(75)@45` |

<a id="s-owncharpack"></a>

### S_OwnCharPack

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_OwnCharPack.java`；SHA-256：`6925f701e7d0245e2ba41bf0e2892fa9d8a1c786d6ebe6d87363e77004020f26`。

局部常數（subcode／格式旗標／限制）：`STATUS_POISON=1`；`STATUS_INVISIBLE=2`；`STATUS_PC=4`；`STATUS_BRAVE=16`；`STATUS_ELFBRAVE=32`；`STATUS_FASTMOVABLE=64`；`STATUS_GHOST=128`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_OwnCharPack(final L1PcInstance pc)` @34 | 委派 `buildPacket(pc)@35` |
| `private void buildPacket(final L1PcInstance pc)` @39 | `C(S_OPCODE_CHARPACK)@67` → `H(pc.getX())@68` → `H(pc.getY())@69` → `D(pc.getId())@70`<br>〔`if pc.isDead()`〕 `H(pc.getTempCharGfxAtDead())@73`<br>〔`else`〕 `H(pc.getTempCharGfx())@76`<br>〔`if pc.isDead()`〕 `C(pc.getStatus())@80`<br>〔`else`〕 `C(pc.getCurrentWeapon())@82`<br>`C(pc.getHeading())@85` → `C(pc.getOwnLightSize())@87` → `C(pc.getMoveSpeed())@88` → `D((int) pc.getExp())@89` → `H(pc.getLawful())@90` → `S(stringBuilder.toString())@98` → `S(pc.getTitle())@99` → `C(status)@100` → `D(pc.getClanid())@101` → `S(pc.getClanname())@102` → `S(null)@103` → `C(pc.getClanRank() > 0 ? pc.getClanRank() << 4 : 0xb0)@117`<br>〔`if pc.isInParty()`〕 `C(100 * pc.getCurrentHp() / pc.getMaxHp())@120`<br>〔`else`〕 `C(0xff)@123`<br>〔`if pc.hasSkillEffect(STATUS_BRAVE3)`〕 `C(0x08)@127`<br>〔`else`〕 `C(0x00)@130`<br>`C(0x00)@132` → `C(0x00)@133` → `C(0xff)@134` → `C(0xff)@135` |

<a id="s-owncharstatus"></a>

### S_OwnCharStatus

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_OwnCharStatus.java`；SHA-256：`11c32ff65b296d0b4b4ac6ec9d1ce2632b731ee6664422c73d970d18fa4eac7c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_OwnCharStatus(final L1PcInstance pc)` @21 | `C(S_OPCODE_OWNCHARSTATUS)@31` → `D(pc.getId())@32` → `C(pc.getLevel())@34` → `Exp(pc.getExp())@36` → `C(pc.getStr())@38` → `C(pc.getInt())@39` → `C(pc.getWis())@40` → `C(pc.getDex())@41` → `C(pc.getCon())@42` → `C(pc.getCha())@43`<br>〔`if com.lineage.config.ConfigClient.isROClient()`〕 `D(pc.getCurrentHp())@47` → `D(pc.getMaxHp())@48` → `D(pc.getCurrentMp())@49` → `D(pc.getMaxMp())@50`<br>〔`else`〕 `H(pc.getCurrentHp())@52` → `H(pc.getMaxHp())@53` → `H(pc.getCurrentMp())@54` → `H(pc.getMaxMp())@55`<br>委派 `writeAc(pc)@57` → `D(time)@58` → `C(pc.get_food())@59` → `C(pc.getInventory().getWeight240())@60` → `H(pc.getLawful())@61` → `H(pc.getFire())@63` → `H(pc.getWater())@64` → `H(pc.getWind())@65` → `H(pc.getEarth())@66` → `D(pc.getKillCount())@67` |
| `public S_OwnCharStatus(final L1PcInstance pc, int str)` @76 | `C(S_OPCODE_OWNCHARSTATUS)@80` → `D(pc.getId())@81` → `C(pc.getLevel())@83` → `Exp(pc.getExp())@85` → `C(str)@87` → `C(pc.getInt())@88` → `C(pc.getWis())@89` → `C(pc.getDex())@90` → `C(pc.getCon())@91` → `C(pc.getCha())@92`<br>〔`if com.lineage.config.ConfigClient.isROClient()`〕 `D(pc.getCurrentHp())@96` → `D(pc.getMaxHp())@97` → `D(pc.getCurrentMp())@98` → `D(pc.getMaxMp())@99`<br>〔`else`〕 `H(pc.getCurrentHp())@101` → `H(pc.getMaxHp())@102` → `H(pc.getCurrentMp())@103` → `H(pc.getMaxMp())@104`<br>委派 `writeAc(pc)@106` → `D(time)@107` → `C(pc.get_food())@108` → `C(pc.getInventory().getWeight240())@109` → `H(pc.getLawful())@110` → `H(pc.getFire())@111` → `H(pc.getWater())@112` → `H(pc.getWind())@113` → `H(pc.getEarth())@114` → `D(pc.getKillCount())@115` |
| `private void writeAc(final L1PcInstance pc)` @132 | 〔`if com.lineage.config.ConfigClient.isROClient()`〕 `D(pc.getAc())@134`<br>〔`else`〕 `C(pc.getAc())@136` |

<a id="s-owncharstatus2"></a>

### S_OwnCharStatus2

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_OwnCharStatus2.java`；SHA-256：`363d5c64f19995539132d5e8e730c55ccf2c7b3220330dd68c02e15e5d999b6b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_OwnCharStatus2(final L1PcInstance pc)` @19 | 委派 `buildPacket(pc)@25` |
| `private void buildPacket(final L1PcInstance pc)` @29 | `C(S_OPCODE_OWNCHARSTATUS2)@30` → `C(pc.getStr())@31` → `C(pc.getInt())@32` → `C(pc.getWis())@33` → `C(pc.getDex())@34` → `C(pc.getCon())@35` → `C(pc.getCha())@36` → `C(pc.getInventory().getWeight240())@37` |
| `public S_OwnCharStatus2(final L1PcInstance pc, final int str)` @45 | `C(S_OPCODE_OWNCHARSTATUS2)@46` → `C(str)@47` → `C(pc.getInt())@48` → `C(pc.getWis())@49` → `C(pc.getDex())@50` → `C(pc.getCon())@51` → `C(pc.getCha())@52` → `C(pc.getInventory().getWeight240())@53` |

<a id="s-packetbox"></a>

### S_PacketBox

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBox.java`；SHA-256：`817ba5fd5b8ada480c6cef6d0529b9cb8ac8c6a33d84af550f8479a31fcce977`。

局部常數（subcode／格式旗標／限制）：`MSG_DUEL=5`；`MSG_SMS_SENT=6`；`MSG_MARRIED=9`；`WEIGHT=10`；`FOOD=11`；`HTML_UB=14`；`MSG_ELF=15`；`ADD_EXCLUDE2=17`；`ADD_EXCLUDE=18`；`REM_EXCLUDE=19`；`ICONS1=20`；`ICONS2=21`；`ICON_AURA=22`；`MSG_TOWN_LEADER=23`；`PLEDGE_TWO=24`；`PLEDGE_REFRESH_PLUS=25`；`PLEDGE_REFRESH_MINUS=26`；`MSG_RANK_CHANGED=27`；`PLEDGE_ONE=29`；`MSG_WIN_LASTAVARD=30`；`MSG_FEEL_GOOD=31`；`SOMETHING1=33`；`ICON_BLUEPOTION=34`；`ICON_POLYMORPH=35`；`ICON_CHATBAN=36`；`GUI_PET_EQUIPMENT=37`；`HTML_CLAN1=38`；`ICON_I2H=40`；`CHARACTER_CONFIG=41`；`LOGOUT=42`；`MSG_CANT_LOGOUT=43`；`MSG_COLOSSEUM=49`；`HTML_CLAN2=51`；`FISHING=55`；`DOLL=56`；`WISDOM_POTION=57`；`DEL_ICON=59`；`ALLIANCE_LIST=62`；`DMG=74`；`TGDMG=75`；`LEAVES=82`；`BLUE_EXPREPLY=86`；`DRAGON_BLOOD_ICON=100`；`DRAGON=101`；`EVASION_UPDATE=132`；`WEAPON_DURABILITY=138`；`BOOKMARK_EXPAND=141`；`PROTECTOR_ICON=144`；`APPRENTICE_HTML=146`；`APPRENTICE_ICON=147`；`ITEM_STATUS_RENEW=149`；`MAP_LIMIT_TIMER=153`；`WEAPON_BUFF_ICON=154`；`POISON_ICON=161`；`HTML_PLEDGE_ANNOUNCE=167`；`HTML_PLEDGE_REALEASE_ANNOUNCE=168`；`HTML_PLEDGE_WRITE_NOTES=169`；`HTML_PLEDGE_MEMBERS=170`；`HTML_PLEDGE_ONLINE_MEMBERS=171`；`PLEDGE_EMBLEM_STATUS=173`；`TOWN_TELEPORT=176`；`NONE_TIME_ICON=180`；`HTML_CLAN_WARHOUSE_RECORD=117`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBox(final int subCode, final String msg, final int value)` @393 | `C(S_OPCODE_PACKETBOX)@394` → `C(LEAVES)@395` → `S(msg)@396` → `H(value)@397` |
| `public S_PacketBox(final L1PcInstance master, final ArrayList<L1PcInstance> totalList)` @406 | `C(S_OPCODE_PACKETBOX)@408` → `C(APPRENTICE_HTML)@409` → `C(totalList.size())@410`<br>〔`for L1PcInstance l1Char : totalList`〕 `S(l1Char.getName())@414` → `C(l1Char.getLevel())@415` → `C(l1Char.getType())@416`<br>`S(master.getName())@420` → `C(master.getLevel())@421` → `C(master.getType())@422` |
| `public S_PacketBox(final int subCode)` @426 | `C(S_OPCODE_PACKETBOX)@427` → `C(subCode)@428`<br>〔`switch subCode / case DEL_ICON`〕 `H(0x0000)@444` |
| `public S_PacketBox(final int subCode, final int value, final String name)` @453 | `C(S_OPCODE_PACKETBOX)@454` → `C(subCode)@455`<br>〔`switch subCode / case MSG_RANK_CHANGED`〕 `C(value)@459` → `S(name)@460` |
| `public S_PacketBox(final int subCode, final int value)` @466 | `C(S_OPCODE_PACKETBOX)@467` → `C(subCode)@468`<br>〔`switch subCode / case TGDMG`〕 `C(time)@474`<br>〔`switch subCode / case ICON_POLYMORPH`〕 `H(value)@481`<br>〔`switch subCode / case WISDOM_POTION`〕 `C(0x2c)@492` → `H(value)@493`<br>〔`switch subCode / case EVASION_UPDATE`〕 `C(value)@501`<br>〔`switch subCode / case MSG_COLOSSEUM`〕 `C(value)@507`<br>〔`switch subCode / case DOLL`〕 `H(value)@513`<br>〔`switch subCode / case MAP_LIMIT_TIMER`〕 `D(value)@517`<br>〔`switch subCode / case WEAPON_DURABILITY`〕 `C(value)@521`<br>〔`switch subCode / case PLEDGE_EMBLEM_STATUS`〕 `C(1)@524`<br>〔`switch subCode / case PLEDGE_EMBLEM_STATUS → if value==1`〕 `C(1)@527`<br>〔`switch subCode / case PLEDGE_EMBLEM_STATUS → else`〕 `C(0)@529`<br>〔`switch subCode / case PLEDGE_EMBLEM_STATUS`〕 `D(0x00)@531`<br>〔`switch subCode / case 121`〕 `D(value)@534` |
| `public S_PacketBox(final int subCode, final int type, final int time)` @544 | `C(S_OPCODE_PACKETBOX)@545` → `C(subCode)@546`<br>〔`switch subCode / case MSG_DUEL`〕 `D(type)@551` → `D(time)@552`<br>〔`switch subCode / case 12 → if type == 0`〕 `C(time)@557` → `C(100)@558` → `C(type)@559`<br>〔`switch subCode / case 12 → if type == 1`〕 `C(0)@561` → `C(time)@562` → `C(type)@563`<br>〔`switch subCode / case 20`〕 `C(type)@568` → `H(time)@569`<br>〔`switch subCode / case 21`〕 `C(time)@573` → `C(type)@574`<br>〔`switch subCode / case 22`〕 `H(type)@578` → `H(time)@579`<br>〔`switch subCode / case BLUE_EXPREPLY`〕 `C(0x70)@583` → `C(0x01)@584` → `C(type)@585` → `H(time)@586`<br>〔`switch subCode / case DRAGON_BLOOD_ICON`〕 `C(type)@590` → `H(time)@591`<br>〔`switch subCode / case 114`〕 `D(type)@595` → `D(time)@596`<br>〔`switch subCode / case APPRENTICE_ICON`〕 `C(type)@600` → `C(time)@601`<br>〔`switch subCode / case WEAPON_BUFF_ICON`〕 `H(time)@605` → `H(type)@606`<br>〔`switch subCode / case ITEM_STATUS_RENEW`〕 `D(type)@610` → `C(time)@611`<br>〔`switch subCode / case NONE_TIME_ICON`〕 `C(type)@615` → `D(time)@616` → `D(0x00000D67)@617` → `H(0x00)@618` |
| `public S_PacketBox(final int subCode, final int type, final int time, final int stringId)` @627 | `C(S_OPCODE_PACKETBOX)@628` → `C(subCode)@629`<br>〔`switch subCode / case NONE_TIME_ICON`〕 `C(type)@633` → `D(time)@634` → `D(stringId)@635` → `H(0x00)@636` |
| `public S_PacketBox(final int subCode, final String name)` @645 | `C(S_OPCODE_PACKETBOX)@646` → `C(subCode)@647`<br>〔`switch subCode / case HTML_PLEDGE_REALEASE_ANNOUNCE`〕 `S(name)@655` |
| `public S_PacketBox(final int subCode, final Object[] names)` @664 | `C(S_OPCODE_PACKETBOX)@665` → `C(subCode)@666`<br>〔`switch subCode / case HTML_PLEDGE_ONLINE_MEMBERS`〕 `C(names.length)@671`<br>〔`switch subCode / case HTML_PLEDGE_ONLINE_MEMBERS → for final Object name : names`〕 `S(name.toString())@673` |
| `public S_PacketBox(final int subCode, final L1PcInstance[] names)` @683 | `C(S_OPCODE_PACKETBOX)@684` → `C(subCode)@685`<br>〔`switch subCode / case HTML_PLEDGE_ONLINE_MEMBERS`〕 `H(names.length)@689`<br>〔`switch subCode / case HTML_PLEDGE_ONLINE_MEMBERS → for final L1PcInstance name : names`〕 `S(name.getName())@691` → `C(0x00)@692` |

<a id="s-packetboxactivespells"></a>

### S_PacketBoxActiveSpells

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxActiveSpells.java`；SHA-256：`0f4e0aa098041a1229582f91a10b56ca6d9798ac1aaadaa0f76fbd50ae77fde3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxActiveSpells(final L1PcInstance pc)` @17 | `C(S_OPCODE_PACKETBOX)@18` → `C(0x14)@19`<br>〔`for int i : type → switch i / case 72`〕 `D((int) (System.currentTimeMillis() / 1000))@24`<br>〔`for int i : type → switch i / default`〕 `C(i)@27` |

<a id="s-packetboxconfig"></a>

### S_PacketBoxConfig

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxConfig.java`；SHA-256：`2c94a77b314a0e38d01dbc0a47830913e52a7014502e543043bf5814ab3d0ba5`。

局部常數（subcode／格式旗標／限制）：`CHARACTER_CONFIG=0x29`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxConfig(L1Config config)` @23 | `C(S_OPCODE_PACKETBOX)@27` → `C(CHARACTER_CONFIG)@28` → `D(length)@29` → `Byte(data)@30` |

<a id="s-packetboxcooking"></a>

### S_PacketBoxCooking

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxCooking.java`；SHA-256：`2329862fc16b2ec42049074b6da8bf36fc6b01f4a5f11d0a3db239e2cdae9af2`。

局部常數（subcode／格式旗標／限制）：`COOK_WINDOW=52`；`ICON_COOKING=53`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxCooking(final int value)` @17 | `C(S_OPCODE_PACKETBOX)@18` → `C(COOK_WINDOW)@19` → `C(0xdb)@20` → `C(0x31)@21` → `C(0xdf)@22` → `C(0x02)@23` → `C(0x01)@24` → `C(value)@25` |
| `public S_PacketBoxCooking(final L1PcInstance pc, final int type, final int time)` @29 | `C(S_OPCODE_PACKETBOX)@30` → `C(ICON_COOKING)@31`<br>〔`switch type / case 0x07`〕 `C(pc.getStr())@39` → `C(pc.getInt())@40` → `C(pc.getWis())@41` → `C(pc.getDex())@42` → `C(pc.getCon())@43` → `C(pc.getCha())@44` → `H(food)@45` → `C(type)@46` → `C(0x24)@47` → `H(time)@48` → `C(0x0)@49`<br>〔`switch type / case 54`〕 `C(0x00)@53` → `C(0x00)@54` → `C(0x00)@55` → `C(0x00)@56` → `C(0x00)@57` → `C(0x00)@58` → `H(0x00)@59` → `C(type)@60` → `C(0x2a)@61` → `H(time)@62` → `C(0x0)@63`<br>〔`switch type / default`〕 `C(pc.getStr())@67` → `C(pc.getInt())@68` → `C(pc.getWis())@69` → `C(pc.getDex())@70` → `C(pc.getCon())@71` → `C(pc.getCha())@72` → `H(food)@73` → `C(type)@74` → `C(0x26)@75` → `H(time)@76` → `C(0x0)@77` |

<a id="s-packetboxdk"></a>

### S_PacketBoxDk

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxDk.java`；SHA-256：`2200aab30640cd66edbd27476d200c201c10ca861f9c3181605b7f68758c6384`。

局部常數（subcode／格式旗標／限制）：`LV1=0x01`；`LV2=0x02`；`LV3=0x03`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxDk(final int lv)` @21 | `C(S_OPCODE_PACKETBOX)@22` → `C(0x4b)@23` → `C(lv)@24` |

<a id="s-packetboxexp"></a>

### S_PacketBoxExp

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxExp.java`；SHA-256：`df1090aa9c2b876c22f479499c5554901ecfab6fb826c44fbb433594d5f22921`。

局部常數（subcode／格式旗標／限制）：`LEAVES=82`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxExp(final int exp)` @19 | `C(S_OPCODE_PACKETBOX)@20` → `C(LEAVES)@21` → `C(exp)@22` → `C(0x00)@23` → `C(0x00)@24` → `C(0x00)@25` |
| `public S_PacketBoxExp()` @32 | 委派 `this(0)@33` |

<a id="s-packetboxgame"></a>

### S_PacketBoxGame

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxGame.java`；SHA-256：`31df36bcf1b73118f1d16d31b95a3fe670461df352ec89c9fe3883851d842621`。

局部常數（subcode／格式旗標／限制）：`GAMESTART=64`；`TIMESTART=65`；`GAMEINFO=66`；`GAMEOVER=69`；`GAMECLEAR=70`；`STARTTIME=71`；`STARTTIMECLEAR=72`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxGame(final int subCode)` @36 | `C(S_OPCODE_PACKETBOX)@37` → `C(subCode)@38` |
| `public S_PacketBoxGame(final int subCode, final int value)` @56 | `C(S_OPCODE_PACKETBOX)@57` → `C(subCode)@58`<br>〔`switch subCode / case GAMEOVER`〕 `C(value)@63`<br>〔`switch subCode / case STARTTIME`〕 `H(value)@67` |
| `public S_PacketBoxGame(final ArrayList<StringBuilder> list)` @77 | `C(S_OPCODE_PACKETBOX)@78` → `C(GAMEINFO)@79` → `C(list.size())@80` → `C(0x00)@81` → `C(0x00)@82` → `C(0x00)@83`<br>〔`if list != null → for final StringBuilder string : list`〕 `S(string.toString())@87` |
| `public S_PacketBoxGame(final StringBuilder title, final ArrayList<StringBuilder> list)` @99 | `C(S_OPCODE_PACKETBOX)@100` → `C(GAMEINFO)@101` → `C(list.size() + 1)@102` → `C(0x00)@103` → `C(0x00)@104` → `C(0x00)@105` → `S(title.toString())@107`<br>〔`if list != null → for final StringBuilder c : list`〕 `S(c.toString())@110` |

<a id="s-packetboxgm"></a>

### S_PacketBoxGm

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxGm.java`；SHA-256：`d1fbf5ba0e7dd460aafb04d1afa318feb1b40fa850c9ab8b1ff3ce9d44a52494`。

局部常數（subcode／格式旗標／限制）：`CALL_SOMETHING=0x2d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxGm(final L1PcInstance srcpc, final int mode)` @34 | `C(S_OPCODE_PACKETBOX)@35` → `C(CALL_SOMETHING)@36`<br>〔`if srcpc.isGm()`〕 委派 `callSomething(srcpc, mode)@38` |
| `private void callSomething(final L1PcInstance srcpc, final int mode)` @43 | `C(World.get().getAllPlayers().size())@47`<br>〔`while itr.hasNext() → if acc == null`〕 `D(0)@55`<br>〔`while itr.hasNext() → else`〕 `D(time)@63`<br>〔`while itr.hasNext()`〕 `S(mode + ":" + pc.getName())@67` → `S(String.valueOf(pc.getLevel()))@69` |

<a id="s-packetboxgree"></a>

### S_PacketBoxGree

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxGree.java`；SHA-256：`d72e6b5151f8fcb0695ff6024736b3aa9ecc892a50d907a1f45bb65f6b7edac7`。

局部常數（subcode／格式旗標／限制）：`GREEN_MESSAGE=0x54`；`SECRETSTORY_GFX=0x53`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxGree(final String msg)` @30 | `C(S_OPCODE_PACKETBOX)@31` → `C(GREEN_MESSAGE)@32` → `C(0x02)@33` → `S(msg)@34` |
| `public S_PacketBoxGree(int type)` @44 | `C(S_OPCODE_PACKETBOX)@45` → `C(SECRETSTORY_GFX)@46` → `D(type)@47` → `C(0x00)@48` → `C(0x00)@49` → `C(0x00)@50` |

<a id="s-packetboxhpmsg"></a>

### S_PacketBoxHpMsg

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxHpMsg.java`；SHA-256：`f3bf17847aab53a0e3beeeba39a6f7de3a9d0b0bfc01b5807d39a7a329a46bed`。

局部常數（subcode／格式旗標／限制）：`MSG_FEEL_GOOD=31`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxHpMsg()` @16 | `C(S_OPCODE_PACKETBOX)@17` → `C(MSG_FEEL_GOOD)@18` |

<a id="s-packetboxicon1"></a>

### S_PacketBoxIcon1

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxIcon1.java`；SHA-256：`028327d0f79578c5dba63dfe0a1348b5b484d26efb352821351a47eeb1daa7f2`。

局部常數（subcode／格式旗標／限制）：`_icons_1=0x14`；`_dodge_up=0x58`；`_dodge_down=0x65`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxIcon1(final int type, final int time)` @14 | `C(S_OPCODE_PACKETBOX)@15` → `C(_icons_1)@16` → `H(time)@17` → `C(type)@18` |
| `public S_PacketBoxIcon1(final boolean type, final int i)` @27 | `C(S_OPCODE_PACKETBOX)@28`<br>〔`if type`〕 `C(_dodge_up)@30` → `C(i)@31`<br>〔`else`〕 `C(_dodge_down)@34` → `C(i)@35` |

<a id="s-packetboxicon2"></a>

### S_PacketBoxIcon2

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxIcon2.java`；SHA-256：`b15290a47cc1417954ec14da45d83e6df8f84ea121bbf5f5f3015330d6149cd3`。

局部常數（subcode／格式旗標／限制）：`ICONS2=0x15`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxIcon2(final int type, final int time)` @10 | `C(S_OPCODE_PACKETBOX)@11` → `C(ICONS2)@12` → `H(time)@13` → `C(type)@14` |

<a id="s-packetboxiconaura"></a>

### S_PacketBoxIconAura

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxIconAura.java`；SHA-256：`8ed97f704874e3f979570df08c87b914c5417d9b3be06815c3db78710cec1998`。

局部常數（subcode／格式旗標／限制）：`ICON_AURA=0x16`；`ICON_OS=0x7D`；`ICON_E3=0xE3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxIconAura(final int iconid, final int time)` @26 | `C(S_OPCODE_PACKETBOX)@27` → `C(ICON_AURA)@28` → `C(iconid)@29` → `H(time)@30` |
| `public S_PacketBoxIconAura(final int iconid, final int time, final int type)` @40 | `C(S_OPCODE_PACKETBOX)@41` → `C(ICON_AURA)@42` → `C(iconid)@43` → `H(time)@44` → `C(type)@45` |

<a id="s-packetboxitemlv"></a>

### S_PacketBoxItemLv

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxItemLv.java`；SHA-256：`ada9cb21721cc48325ffa04d682d2a29ea0fd4916351e3420ee33022297bc3ab`。

局部常數（subcode／格式旗標／限制）：`MSG_LEVEL_OVER=0x0c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxItemLv(final int minLv, final int maxLv)` @23 | `C(S_OPCODE_PACKETBOX)@24` → `C(MSG_LEVEL_OVER)@25` → `C(minLv)@26` → `C(maxLv)@27` |
| `public S_PacketBoxItemLv(final int opid)` @35 | `C(opid)@36` → `C(MSG_LEVEL_OVER)@37` → `C(10)@38` → `C(1249)@39` |

<a id="s-packetboxloc"></a>

### S_PacketBoxLoc

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxLoc.java`；SHA-256：`1d8c641c6cd2c105cec528e2025507608f0c09efacc4e7d2ead49b2fbf238fad`。

局部常數（subcode／格式旗標／限制）：`SEND_LOC=111`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxLoc(String name,int map,int x,int y, int zone)` @23 | `C(S_OPCODE_PACKETBOX)@24` → `C(SEND_LOC)@25` → `S(name)@26` → `H(map)@27` → `H(x)@28` → `H(y)@29` → `D(zone)@30` |

<a id="s-packetboxmaptimer"></a>

### S_PacketBoxMapTimer

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxMapTimer.java`；SHA-256：`c5788f37175f1f1ececdbe60b033f140b84f20cb44403cd78d7335a7c4619407`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxMapTimer(final L1PcInstance pc)` @16 | `C(S_OPCODE_PACKETBOX)@20` → `C(0x9f)@21` → `D(mapLimitList.size())@22`<br>〔`for final L1MapsLimitTime mapLimit : mapLimitList`〕 `D(order_id)@29` → `S(mapLimit.getMapName())@30` → `D(time_str)@31` |

<a id="s-packetboxparty"></a>

### S_PacketBoxParty

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxParty.java`；SHA-256：`1f57efd8f5d88d01e0e6dc0ca369d29ffa822d5a27f7fce2db8d71a0762fa956`。

局部常數（subcode／格式旗標／限制）：`MSG_PARTY1=0x68`；`MSG_PARTY2=0x69`；`MSG_PARTY3=0x6e`；`MSG_LEADER=0x6a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxParty(final L1PcInstance pc, final L1Party party)` @50 | `C(S_OPCODE_PACKETBOX)@54` → `C(MSG_PARTY1)@55` → `C(map.size() - 1)@56`<br>〔`for final Iterator<Entry<Integer, L1PcInstance>> iter = map.iterator(); iter.hasNext();`〕 `D(tgpc.getId())@69` → `S(tgpc.getName())@70` → `C((int) ((nowhp / maxhp) * 100D))@75` → `D(tgpc.getMapId())@76` → `H(tgpc.getX())@77` → `H(tgpc.getY())@78` |
| `public S_PacketBoxParty(final L1Party party, final L1PcInstance pc)` @95 | `C(S_OPCODE_PACKETBOX)@99` → `C(MSG_PARTY3)@100` → `C(map.size() - 1)@101`<br>〔`for final Iterator<Entry<Integer, L1PcInstance>> iter = map.iterator(); iter.hasNext();`〕 `D(tgpc.getId())@114` → `D(tgpc.getMapId())@115` → `H(tgpc.getX())@116` → `H(tgpc.getY())@117` |
| `public S_PacketBoxParty(final L1PcInstance tgpc)` @132 | `C(S_OPCODE_PACKETBOX)@134` → `C(MSG_PARTY2)@135` → `D(tgpc.getId())@136` → `S(tgpc.getName())@137` → `C((int) ((nowhp / maxhp) * 100D))@142` → `D(tgpc.getMapId())@143` → `H(tgpc.getX())@144` → `H(tgpc.getY())@145` |
| `public S_PacketBoxParty(final int objid, final String name)` @159 | `C(S_OPCODE_PACKETBOX)@161` → `C(MSG_LEADER)@162` → `D(objid)@163` → `S(name)@164` |

<a id="s-packetboxpledgerecommend"></a>

### S_PacketBoxPledgeRecommend

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxPledgeRecommend.java`；SHA-256：`6492db82484df8e7e815b594cd0692d14d884c98d9d13aac3bb6a24e27ac3d9d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxPledgeRecommend(final boolean postStatus, final int clan_id)` @33 | `C(S_OPCODE_CLANMATCHING)@34` → `C(postStatus ? 0x00 : 0x01)@35` → `C(0x00)@36` |
| `public S_PacketBoxPledgeRecommend( final List<L1ClanRecommend> allClanRecommend, final Map<Integer, CopyOnWriteArrayList<Integer>> allApply)` @40 | `C(S_OPCODE_CLANMATCHING)@43` → `C(0x02)@44` → `C(0x00)@45` → `C(rows.size())@52`<br>〔`for final RecommendationRow row : rows`〕 `D(clan.getClanId())@56` → `S(clan.getClanName())@57` → `S(clan.getLeaderName())@58` → `D(clan.getLoginLevel())@59` → `C(recommend.getTypeId())@60` → `C(clan.getHouseId() > 0 ? 1 : 0)@61` → `C(clan.getCastleId() > 0 ? 1 : 0)@62` → `C(0)@63` → `S(recommend.getTypeMessage())@64` → `D(clan.getEmblemId())@65` |
| `public S_PacketBoxPledgeRecommend( final Map<Integer, L1ClanRecommend> allRecommend, final List<Integer> allClanApply)` @70 | `C(S_OPCODE_CLANMATCHING)@73` → `C(0x03)@74` → `C(0x00)@75` → `C(rows.size())@84`<br>〔`for final RecommendationRow row : rows`〕 `D(clan_id)@89` → `C(0x00)@90` → `D(clan_id)@91` → `S(clan.getClanName())@92` → `S(clan.getLeaderName())@93` → `D(clan.getLoginLevel())@94` → `C(recommend.getTypeId())@95` → `C(clan.getHouseId() > 0 ? 1 : 0)@96` → `C(clan.getCastleId() > 0 ? 1 : 0)@97` → `C(0)@98` → `S(recommend.getTypeMessage())@99` → `D(clan.getEmblemId())@100` |
| `public S_PacketBoxPledgeRecommend(final L1ClanRecommend recommend, final List<Integer> apply)` @105 | `C(S_OPCODE_CLANMATCHING)@107` → `C(0x04)@108`<br>〔`if recommend == null`〕 `C(0x82)@111`<br>〔`else`〕 `C(0x00)@114` → `C(recommend.getTypeId())@115` → `S(recommend.getTypeMessage())@116`<br>〔`else → if apply == null`〕 `C(0x00)@119`<br>〔`else`〕 `C(apply.size())@123`<br>〔`else → for final int objid : apply`〕 `D(objid)@126` → `C(0x00)@127`<br>〔`else → for final int objid : apply → if name != null → if pc == null`〕 `C(0)@139`<br>〔`else → for final int objid : apply → if name != null → else`〕 `C(1)@142`<br>〔`else → for final int objid : apply → else`〕 `C(0)@146`<br>〔`else → for final int objid : apply`〕 `S(name)@149` → `C(pc == null ? 0 : pc.getType())@150` → `H(pc == null ? 0 : pc.getLawful())@151` → `H(pc == null ? 0 : pc.getLevel())@152` |
| `public S_PacketBoxPledgeRecommend(final int type, final int record_id, final int acceptType, final int actionType)` @158 | `C(S_OPCODE_CLANMATCHING)@161` → `C(type)@162` → `C(actionType)@163` → `D(record_id)@164` → `C(acceptType)@165` |

<a id="s-packetboxprotection"></a>

### S_PacketBoxProtection

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxProtection.java`；SHA-256：`45a856735ae94737bef866fe632a0771865bf60ca13a0a8587bb08050a2aca6c`。

局部常數（subcode／格式旗標／限制）：`JUSTICE_L1=0`；`JUSTICE_L2=1`；`JUSTICE_L3=2`；`EVIL_L1=3`；`EVIL_L2=4`；`EVIL_L3=5`；`ENCOUNTER=6`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxProtection(int model, final int type)` @38 | `C(S_OPCODE_PACKETBOX)@39` → `C(0x72)@40` → `D(model)@41` → `D(type)@42` |

<a id="s-packetboxselect"></a>

### S_PacketBoxSelect

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxSelect.java`；SHA-256：`21ca14f9f68de4a90b7229e26f5f0831c5344d6441c17b8b885bde836ba70496`。

局部常數（subcode／格式旗標／限制）：`LOGOUT=0x2a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxSelect()` @20 | `C(S_OPCODE_PACKETBOX)@21` → `C(LOGOUT)@22` → `C(0x00)@23` → `C(0x00)@24` → `C(0x00)@25` → `C(0x00)@26` → `C(0x00)@27` → `C(0x00)@28` |

<a id="s-packetboxtest"></a>

### S_PacketBoxTest

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxTest.java`；SHA-256：`e571a9d12ac7cf4f60d5a52f7cd5e8b8065fd6c1b466fac095da3a80418cf8ff`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxTest()` @12 | `C(S_OPCODE_PACKETBOX)@14` → `C(0x52)@15` → `C(150)@16` → `C(0x00)@17` → `C(0x00)@18` → `C(0x00)@19` |
| `public S_PacketBoxTest(byte ocid, final int time)` @23 | `C(S_OPCODE_PACKETBOX)@24` → `C(ocid)@25` → `C(time)@26` → `C(0x00)@27` → `C(0x00)@28` → `C(0x00)@29` |
| `public S_PacketBoxTest(final int type, final int time)` @34 | `C(S_OPCODE_PACKETBOX)@35` → `C(type)@36` → `H(time)@37` → `H(0x00)@38` |
| `public S_PacketBoxTest(final int value, String[] clanName)` @42 | `C(S_OPCODE_PACKETBOX)@43` → `C(value)@44`<br>〔`for int i = 0; i < value ; i++`〕 `S(clanName[i])@46` |
| `public S_PacketBoxTest(final int time)` @51 | `C(S_OPCODE_PACKETBOX)@52` → `C(79)@53` → `C(2)@54` → `S("TEMP")@55` → `S("AASS")@56` |
| `public S_PacketBoxTest(byte[] bs)` @61 | 〔`for byte outbs : bs`〕 `C(outbs)@63` |

<a id="s-packetboxthirdspeed"></a>

### S_PacketBoxThirdSpeed

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxThirdSpeed.java`；SHA-256：`af6faeb8c56c9280928d2e7cc0e4105ca5ea57785abd4d7dde3f3d85ca9e354e`。

局部常數（subcode／格式旗標／限制）：`CAKE=0x3c`；`CAKE_TYPE=0x08`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxThirdSpeed(int time)` @19 | `C(S_OPCODE_PACKETBOX)@20` → `C(CAKE)@21` → `C(time >> 2)@22` → `C(CAKE_TYPE)@23` |

<a id="s-packetboxwar"></a>

### S_PacketBoxWar

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxWar.java`；SHA-256：`eefdb707a818cb033b6d4e83f190beefd3e8cd6d95dbb0ed51a42507ea3b9d35`。

局部常數（subcode／格式旗標／限制）：`MSG_WAR_BEGIN=0`；`MSG_WAR_END=1`；`MSG_WAR_GOING=2`；`MSG_WAR_INITIATIVE=3`；`MSG_WAR_OCCUPY=4`；`MSG_WIN_LASTAVARD=30`；`MSG_WAR_OVER=79`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxWar()` @68 | `C(S_OPCODE_PACKETBOX)@71` → `C(MSG_WAR_OVER)@72` → `C(count)@73`<br>〔`for int key = 1; key < count ; key++ → if clanName != null`〕 `S(clanName)@77`<br>〔`for int key = 1; key < count ; key++ → else`〕 `S(" ")@80` |
| `public S_PacketBoxWar(final int subCode)` @95 | `C(S_OPCODE_PACKETBOX)@96` → `C(subCode)@97` |
| `public S_PacketBoxWar(final int subCode, final int value)` @111 | `C(S_OPCODE_PACKETBOX)@112` → `C(subCode)@113` → `C(value)@115` → `H(0x0000)@116` |
| `public S_PacketBoxWar(final int subCode, final int id, final String name, final String clanName)` @131 | `C(S_OPCODE_PACKETBOX)@132` → `C(MSG_WIN_LASTAVARD)@133` → `D(id)@135` → `S(name)@136` → `S(clanName)@137` |

<a id="s-packetboxwaterlife"></a>

### S_PacketBoxWaterLife

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxWaterLife.java`；SHA-256：`f32055fd3a810af04f65f0e45d576c67ab618f172b00914ed7f9f7be47502293`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxWaterLife()` @13 | `C(S_OPCODE_PACKETBOX)@14` → `C(0x3b)@15` → `H(0x0000)@16` |

<a id="s-packetboxwindshackle"></a>

### S_PacketBoxWindShackle

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxWindShackle.java`；SHA-256：`b0a1df2db3e1ddc881e23f3879ffe0e90a26c0a3c57927d7843407faf864621a`。

局部常數（subcode／格式旗標／限制）：`WIND_SHACKLE=0x2c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxWindShackle(final int objectId, final int time)` @22 | `C(S_OPCODE_PACKETBOX)@24` → `C(WIND_SHACKLE)@25` → `D(objectId)@26` → `H(buffTime)@27` |

<a id="s-packetboxwisdompotion"></a>

### S_PacketBoxWisdomPotion

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketBoxWisdomPotion.java`；SHA-256：`cc3d17cba81f2a85067dfd1eaaa60e01577bd656c0ccd0e72a7a56fd30e994b8`。

局部常數（subcode／格式旗標／限制）：`WISDOM_POTION=0x39`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PacketBoxWisdomPotion(final int time)` @15 | `C(S_OPCODE_PACKETBOX)@16` → `C(WISDOM_POTION)@17` → `C(0x2c)@18` → `H(time)@19` |

<a id="s-packetnew"></a>

### S_PacketNew

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PacketNew.java`；SHA-256：`3d4888a6e76d6b8e351e921958f08334a725d969611940d73c91dafe34594f75`。

沒有啟用的直接欄位讀寫；若為子類別，依父類別；其餘不得推定有可用 opcode。

<a id="s-paralysis"></a>

### S_Paralysis

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Paralysis.java`；SHA-256：`7e57e26309fa5e8221a189ce7c82c64be5d03ee9171d9be8d0d74fb4abf2146a`。

局部常數（subcode／格式旗標／限制）：`TYPE_PARALYSIS=0x01`；`TYPE_PARALYSIS2=0x02`；`TYPE_SLEEP=0x03`；`TYPE_FREEZE=0x04`；`TYPE_STUN=0x05`；`TYPE_BIND=0x06`；`TYPE_TELEPORT_UNLOCK=0x07`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Paralysis(final int type, final boolean flag)` @38 | `C(S_OPCODE_PARALYSIS)@39`<br>〔`switch type / case TYPE_PARALYSIS → if flag == true`〕 `C(0x02)@43`<br>〔`switch type / case TYPE_PARALYSIS → else`〕 `C(0x03)@45`<br>〔`switch type / case TYPE_PARALYSIS2 → if flag == true`〕 `C(0x04)@51`<br>〔`switch type / case TYPE_PARALYSIS2 → else`〕 `C(0x05)@53`<br>〔`switch type / case TYPE_TELEPORT_UNLOCK → if flag == true`〕 `C(0x06)@59`<br>〔`switch type / case TYPE_TELEPORT_UNLOCK → else`〕 `C(0x07)@61`<br>〔`switch type / case TYPE_SLEEP → if flag == true`〕 `C(0x0a)@67`<br>〔`switch type / case TYPE_SLEEP → else`〕 `C(0x0b)@69`<br>〔`switch type / case TYPE_FREEZE → if flag == true`〕 `C(0x0c)@75`<br>〔`switch type / case TYPE_FREEZE → else`〕 `C(0x0d)@77`<br>〔`switch type / case TYPE_STUN → if flag == true`〕 `C(0x16)@83`<br>〔`switch type / case TYPE_STUN → else`〕 `C(0x17)@85`<br>〔`switch type / case TYPE_BIND → if flag == true`〕 `C(0x18)@91`<br>〔`switch type / case TYPE_BIND → else`〕 `C(0x19)@93` |

<a id="s-party"></a>

### S_Party

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Party.java`；SHA-256：`0b63e4624368e487d5ef5a77fc0804cd2bf996150ff435454727177309946cb3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Party(final String htmlid, final int objid)` @12 | 委派 `buildPacket(htmlid, objid, "", "", 0)@13` |
| `public S_Party(final String htmlid, final int objid, final String partyname, final String partymembers)` @17 | 委派 `buildPacket(htmlid, objid, partyname, partymembers, 1)@20` |
| `private void buildPacket(final String htmlid, final int objid, final String partyname, final String partymembers, final int type)` @24 | `C(S_OPCODE_SHOWHTML)@26` → `D(objid)@27` → `S(htmlid)@28` → `H(type)@29` → `H(0x02)@30` → `S(partyname)@31` → `S(partymembers)@32` |

<a id="s-pcpositioncorrection"></a>

### S_PcPositionCorrection

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PcPositionCorrection.java`；SHA-256：`4d12c9d8c73c0a4ed13f1adc5d036e95eb0cdeaa92804951a204c5a9ce3c60ad`。
繼承 [S_OwnCharPack](#s-owncharpack) 的封包布局。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PcPositionCorrection(final L1PcInstance pc, final long connectionGeneration, final short mapId, final int x, final int y, final int heading, final long locationRevision)` @30 | 委派 `super(pc)@34` |

<a id="s-pcpositionmapreset"></a>

### S_PcPositionMapReset

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PcPositionMapReset.java`；SHA-256：`60f2ecbb355ca18f65399c8b9d1737a3b935c98a2bdb9c11d821cc623d7677bb`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PcPositionMapReset(final L1PcInstance pc, final long connectionGeneration, final short mapId, final int x, final int y, final int heading, final long locationRevision, final boolean underwater)` @30 | `C(S_OPCODE_MAPID)@41` → `H(mapId)@42` → `C(underwater ? 0x01 : 0x00)@43` |

<a id="s-petctrlmenu"></a>

### S_PetCtrlMenu

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PetCtrlMenu.java`；SHA-256：`9a66c52a05c8723a310a2ac427afec2690779a33b9f060762c2097b0cf6ae66a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PetCtrlMenu(L1Character cha, L1NpcInstance npc, boolean open)` @21 | `C(S_OPCODE_CHARRESET)@22` → `C(0x0c)@23`<br>〔`if open`〕 `H(0x03)@25` → `D(0x00000000)@26` → `D(npc.getId())@27` → `D(0x00000000)@28` → `H(npc.getX())@29` → `H(npc.getY())@30` → `S(npc.getName())@31`<br>〔`else`〕 `H(0x0000)@34` → `D(0x00000001)@35` → `D(npc.getId())@36` → `S(null)@37` |

<a id="s-petequipment"></a>

### S_PetEquipment

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PetEquipment.java`；SHA-256：`47c457d75889387fc57ed210ac5ce1bcb269b38bf4c36db54fa63b04212a30a0`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PetEquipment(final int data, final L1PetInstance pet, final int listNo)` @18 | `C(S_OPCODE_PACKETBOX)@19` → `C(S_PacketBox.GUI_PET_EQUIPMENT)@20` → `C(data)@21` → `D(pet.getId())@22` → `C(listNo)@23` → `C(pet.getAc())@24` |

<a id="s-petinventory"></a>

### S_PetInventory

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PetInventory.java`；SHA-256：`ddc6e6f9bb6023ae58b38c3f28aac0890e610ab89642fdab9b9e5147660d67dd`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PetInventory(final L1PetInstance pet, boolean b)` @50 | 委派 `isTrue(pet)@51` |
| `private void isTrue(final L1PetInstance pet)` @55 | `C(S_OPCODE_SHOWRETRIEVELIST)@58` → `D(pet.getId())@59` → `H(itemList.size())@60` → `C(0x0b)@61`<br>〔`for final L1ItemInstance item : itemList → if item != null`〕 `D(item.getId())@64` → `C(resolvePetInventoryItemType(item))@66` → `H(item.get_gfxid())@67` → `C(L1Item.toPacketBless(item.getBless()))@68` → `D((int) Math.min(item.getCount(), 2000000000))@69`<br>〔`for final L1ItemInstance item : itemList → if item != null → if item.getItem().getType2() == 0 && item.getItem().getType() == 11 && item.isEquipped()`〕 `C(item.isIdentified() ? 3 : 2)@72`<br>〔`for final L1ItemInstance item : itemList → if item != null → else`〕 `C(item.isIdentified() ? 1 : 0)@74`<br>〔`for final L1ItemInstance item : itemList → if item != null`〕 `S(item.getViewName())@76`<br>`C(pet.getAc())@79` |

<a id="s-petlist"></a>

### S_PetList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PetList.java`；SHA-256：`cd7e0f26c3809c98edae28887d545c19327c2a8ca1dcd3d82bd302e94c2e5528`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PetList(final int npcObjId, final L1PcInstance pc)` @19 | 委派 `buildPacket(npcObjId, pc)@20` |
| `public S_PetList(final int npcObjId, final L1PcInstance pc, final List<L1ItemInstance> amuletList)` @30 | 委派 `buildPacketFromList(amuletList)@31` |
| `private void buildPacket(final int npcObjId, final L1PcInstance pc)` @35 | 〔`if amuletList.size() != 0`〕 委派 `writeSelectListBody(amuletList)@49` |
| `private void buildPacketFromList(final List<L1ItemInstance> amuletList)` @53 | 〔`if amuletList != null && amuletList.size() != 0`〕 委派 `writeSelectListBody(amuletList)@55` |
| `private void writeSelectListBody(final List<L1ItemInstance> amuletList)` @59 | `C(S_OPCODE_SELECTLIST)@60` → `D(0x00000046)@61` → `H(amuletList.size())@62`<br>〔`for final L1ItemInstance item : amuletList`〕 `D(item.getId())@64` → `C((int) Math.min(item.getCount(), 2000000000))@65` |

<a id="s-petmenupacket"></a>

### S_PetMenuPacket

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PetMenuPacket.java`；SHA-256：`448af0d27a545bafc6978549304d5a9f87d8c1c9a2e4d5bf35d0b3ad89200d40`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PetMenuPacket(final L1NpcInstance npc, final int exppercet)` @21 | 委派 `buildpacket(npc, exppercet)@22` |
| `private void buildpacket(final L1NpcInstance npc, final int exppercet)` @26 | `C(S_OPCODE_SHOWHTML)@27`<br>〔`if npc instanceof L1PetInstance pet`〕 `D(pet.getId())@30` → `S("anicom")@31` → `C(0x00)@32` → `H(0x0a)@33`<br>〔`if npc instanceof L1PetInstance pet → switch pet.getCurrentPetStatus() / case 1`〕 `S("$469")@35`<br>〔`if npc instanceof L1PetInstance pet → switch pet.getCurrentPetStatus() / case 2`〕 `S("$470")@36`<br>〔`if npc instanceof L1PetInstance pet → switch pet.getCurrentPetStatus() / case 3, 5`〕 `S("$471")@37`<br>〔`if npc instanceof L1PetInstance pet → switch pet.getCurrentPetStatus() / default`〕 `S("$471")@38`<br>〔`if npc instanceof L1PetInstance pet`〕 `S(Integer.toString(pet.getCurrentHp()))@40` → `S(Integer.toString(pet.getMaxHp()))@41` → `S(Integer.toString(pet.getCurrentMp()))@42` → `S(Integer.toString(pet.getMaxMp()))@43` → `S(Integer.toString(pet.getLevel()))@44` → `S(pet.getName())@45` → `S("$611")@46` → `S(Integer.toString(exppercet))@47` → `S(Integer.toString(pet.getLawful()))@48`<br>〔`if npc instanceof L1SummonInstance summon`〕 `D(summon.getId())@52` → `S("moncom")@53` → `C(0x00)@54` → `H(0x06)@55`<br>〔`if npc instanceof L1SummonInstance summon → switch summon.get_currentPetStatus() / case 1`〕 `S("$469")@57`<br>〔`if npc instanceof L1SummonInstance summon → switch summon.get_currentPetStatus() / case 2`〕 `S("$470")@58`<br>〔`if npc instanceof L1SummonInstance summon → switch summon.get_currentPetStatus() / case 3`〕 `S("$471")@59`<br>〔`if npc instanceof L1SummonInstance summon → switch summon.get_currentPetStatus() / case 5`〕 `S("$472")@60`<br>〔`if npc instanceof L1SummonInstance summon → switch summon.get_currentPetStatus() / default`〕 `S("$471")@61`<br>〔`if npc instanceof L1SummonInstance summon`〕 `S(Integer.toString(summon.getCurrentHp()))@63` → `S(Integer.toString(summon.getMaxHp()))@64` → `S(Integer.toString(summon.getCurrentMp()))@65` → `S(Integer.toString(summon.getMaxMp()))@66` → `S(Integer.toString(summon.getLevel()))@67` |

<a id="s-pinkname"></a>

### S_PinkName

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PinkName.java`；SHA-256：`df1b1f78ba9216d32bdb6333b1c89410c3c783fefac2ddc4ac8b583ab4c05e67`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PinkName(final int objecId, final int time)` @17 | `C(S_OPCODE_PINKNAME)@19` → `D(objecId)@20` → `C(time)@21` |

<a id="s-pledge"></a>

### S_Pledge

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Pledge.java`；SHA-256：`5fbe220f68ee9f8923452ab5e1dbb01a038c77ad001a0c31087b86c89d1c2e4a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Pledge(final int objid, final String clanName, final StringBuilder olmembers, final StringBuilder allmembers)` @36 | 委派 `buildPacket(objid, clanName, olmembers, allmembers)@37` |
| `private void buildPacket(final int objid, final String clanname, final StringBuilder olmembers, final StringBuilder allmembers)` @41 | `C(S_OPCODE_SHOWHTML)@43` → `D(objid)@44` → `S("pledgeM")@45` → `H(0X02)@46` → `H(0x03)@47` → `S(clanname)@48` → `S(olmembers.toString())@49` → `S(allmembers.toString())@50` |
| `public S_Pledge(final int objid, final String clanName, final StringBuilder olmembers)` @61 | 委派 `buildPacket(objid, clanName, olmembers)@62` |
| `private void buildPacket(final int objid, final String clanname, final StringBuilder olmembers)` @66 | `C(S_OPCODE_SHOWHTML)@68` → `D(objid)@69` → `S("pledge")@70` → `H(0X01)@71` → `H(0x02)@72` → `S(clanname)@73` → `S(olmembers.toString())@74` |
| `public S_Pledge(int ClanId)` @77 | `C(S_PacketBox.HTML_PLEDGE_ANNOUNCE)@80` → `S(clan.getClanName())@81` → `S(clan.getLeaderName())@82` → `D(clan.getEmblemId())@83` → `D((int) (clan.get_clanBirthday().getTime() / 1000L))@84` → `Byte1(text)@93` |
| `public S_Pledge(String name, String notes)` @99 | `C(S_OPCODE_PACKETBOX)@100` → `C(S_PacketBox.HTML_PLEDGE_WRITE_NOTES)@101` → `S(name)@102` → `Byte1(text)@115` |
| `public S_Pledge(L1Clan clan)` @118 | `C(S_PacketBox.HTML_PLEDGE_MEMBERS)@120` → `H(1)@121` → `C(clan.getAllMembersSize())@122`<br>〔`if clan.getAllMembersSize() > 0 → for String name : membername`〕 `S(pc.getName())@132` → `C(pc.getClanRank())@133` → `C((short)pc.getLevel())@134` → `Byte1(text)@149` → `D(pc.getId())@150` → `C(pc.getType())@151` |

<a id="s-pledgewarehousehistory"></a>

### S_PledgeWarehouseHistory

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PledgeWarehouseHistory.java`；SHA-256：`b2fd732ec118746987d6a947533d4f9084a68560bb16ad163991f6c228846c89`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PledgeWarehouseHistory(final int clanId)` @36 | `C(S_OPCODE_PACKETBOX)@39` → `C(S_PacketBox.HTML_CLAN_WARHOUSE_RECORD)@40` → `D(history.size())@41`<br>〔`for final L1ClanWarehouseHistory record : history`〕 `S(safe(record.get_char_name()))@45` → `C(record.get_type())@46` → `S(item != null ? item.getName() : itemName)@47` → `D(record.get_item_count())@48` → `D((int) ((System.currentTimeMillis() - record.get_record_time().getTime()) / 60000L))@49` |

<a id="s-poison"></a>

### S_Poison

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Poison.java`；SHA-256：`25e4d654db8f1faec19e0b8cf87f979315a3a1297a94f481014603b2cf5c9be3`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Poison(final int objId, final int type)` @20 | `C(S_OPCODE_POISON)@22` → `D(objId)@23`<br>〔`switch type / case 0`〕 `C(0x00)@26` → `C(0x00)@27`<br>〔`switch type / case 1`〕 `C(0x01)@31` → `C(0x00)@32`<br>〔`switch type / case 2`〕 `C(0x00)@36` → `C(0x01)@37` |

<a id="s-poweritemlist"></a>

### S_PowerItemList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PowerItemList.java`；SHA-256：`4daf7045b25aeef8c7f887f580d0fa1b971fe9375e66d37775da362dd7987f3e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PowerItemList(final L1PcInstance pc, final int objid, final List<L1ItemInstance> items)` @23 | `C(S_OPCODE_SHOWRETRIEVELIST)@24` → `D(objid)@25` → `H(items.size())@26` → `C(0x0a)@27`<br>〔`for final L1ItemInstance item : items`〕 `D(itemobjid)@30` → `C(0x00)@31` → `H(item.get_gfxid())@32` → `C(L1Item.toPacketBless(item.getBless()))@33` → `D(0x01)@34` → `C(item.isIdentified() ? 0x01 : 0x00)@35` → `S(item.getViewName())@36` |
| `public S_PowerItemList(final int objid, final List<L1ItemInstance> items)` @42 | `C(S_OPCODE_SHOWRETRIEVELIST)@43` → `D(objid)@44` → `H(items.size())@45` → `C(0x0c)@46`<br>〔`for final L1ItemInstance item : items`〕 `D(itemobjid)@49` → `C(0x00)@50` → `H(item.get_gfxid())@51` → `C(L1Item.toPacketBless(item.getBless()))@52` → `D(0x01)@53` → `C(item.isIdentified() ? 0x01 : 0x00)@54` → `S(item.getViewName())@55` |

<a id="s-privateshop"></a>

### S_PrivateShop

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_PrivateShop.java`；SHA-256：`a4e31e41005c0e274cbaa4782a3509f6947be64b350369f1d9e8e6b6c52e2eff`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_PrivateShop(final L1PcInstance pc, final int objectId, final int type)` @24 | 〔`if shopObj instanceof L1PcInstance`〕 委派 `isPc(pc, objectId, type)@27`<br>〔`if shopObj instanceof L1DeInstance`〕 委派 `isDe(pc, objectId, type)@29`<br>〔`else`〕 `C(S_OPCODE_PRIVATESHOPLIST)@32` → `C(type)@33` → `D(objectId)@34` → `H(0x0000)@35` |
| `private void isDe(L1PcInstance pc, int objectId, int type)` @46 | `C(S_OPCODE_PRIVATESHOPLIST)@52` → `C(type)@53` → `D(objectId)@54`<br>〔`if type == 0 → if map == null \|\| map.isEmpty()`〕 `H(0x0000)@61`<br>〔`if type == 0 → if map.size() <= 0`〕 `H(0x0000)@66`<br>〔`if type == 0`〕 `H(map.size())@70`<br>〔`if type == 0 → for L1ItemInstance item : map.keySet()`〕 `C(i)@74` → `C(L1Item.toPacketBless(item.getBless()))@75` → `H(item.getItem().getGfxId())@76` → `D((int) item.getCount())@77` → `D(price)@79` → `S(name)@83` → `C(0)@84`<br>〔`if type == 1 → if list == null \|\| list.isEmpty()`〕 `H(0x0000)@96`<br>〔`if type == 1 → if list.size() <= 0`〕 `H(0x0000)@101`<br>〔`if type == 1`〕 `H(list.size())@105`<br>〔`if type == 1 → for int key : list.keySet() → for final L1ItemInstance pcItem : pc.getInventory().getItems() → if pcItem.getItemId() == key && pcItem.getEnchantLevel() == level`〕 `C(i)@116` → `D(pcItem.getId())@117` → `D(count)@118` → `D(price)@119` |
| `private void isPc(final L1PcInstance pc, final int objectId, final int type)` @133 | `C(S_OPCODE_PRIVATESHOPLIST)@139` → `C(type)@140` → `D(objectId)@141`<br>〔`if type == 0 → if list.isEmpty()`〕 `H(0x0000)@150`<br>〔`if type == 0 → if size <= 0`〕 `H(0x0000)@157`<br>〔`if type == 0`〕 `H(size)@163`<br>〔`if type == 0 → for int i = 0; i < size; i++ → if item != null`〕 `C(i)@171` → `C(L1Item.toPacketBless(item.getBless()))@172` → `H(item.getItem().getGfxId())@173` → `D(count)@174` → `D(price)@175` → `S(name)@179` → `C(0x00)@180`<br>〔`if type == 1 → if list.isEmpty()`〕 `H(0x0000)@202`<br>〔`if type == 1 → if size <= 0`〕 `H(0x0000)@209`<br>〔`if type == 1`〕 `H(size)@213`<br>〔`if type == 1 → for int i = 0; i < size; i++ → for final L1ItemInstance pcItem : pc.getInventory().getItems() → if (item.getItemId() == pcItem.getItemId()) && (item.getEnchantLevel() == pcItem.getEnchantLevel())`〕 `C(i)@223` → `D(pcItem.getId())@224` → `D(count)@225` → `D(price)@226` |

<a id="s-rangeskill"></a>

### S_RangeSkill

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RangeSkill.java`；SHA-256：`2e6cce916e7b43835793ae55f346284a94e078a5699e3779542a86d96b8d635f`。

局部常數（subcode／格式旗標／限制）：`TYPE_NODIR=0`；`TYPE_DIR=8`；`DAMAGE_EXTENSION_MAGIC=0xD14A6E3F`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RangeSkill(final L1Character cha, final ArrayList<TargetStatus> targetList, final int spellgfx, final int actionId, final int type)` @40 | 委派 `this(cha, targetList, spellgfx, actionId, type, false)@43` |
| `public S_RangeSkill(final L1Character cha, final ArrayList<TargetStatus> targetList, final int spellgfx, final int actionId, final int type, final boolean includeDamage)` @52 | `C(S_OPCODE_RANGESKILLS)@60` → `C(actionId)@61` → `D(cha.getId())@63` → `H(cha.getX())@64` → `H(cha.getY())@65`<br>〔`switch type / case TYPE_NODIR`〕 `C(cha.getHeading())@69`<br>〔`switch type / case TYPE_DIR`〕 `C(cha.getHeading())@81`<br>`D(_sequentialNumber.incrementAndGet())@85` → `H(spellgfx)@86` → `C(type)@87` → `H(0x0000)@88` → `H(targetList.size())@89`<br>〔`for TargetStatus target : targetList`〕 `D(resolvedId)@95`<br>〔`for TargetStatus target : targetList → if target.isCalc()`〕 `H(0x20)@97`<br>〔`for TargetStatus target : targetList → else`〕 `H(0x00)@100`<br>〔`for TargetStatus target : targetList → if includeDamage && ConfigClient.isROClient() && resolvedId != 0`〕 `D(DAMAGE_EXTENSION_MAGIC)@103` → `D(Math.max(0, target.getDamage()))@104` |

<a id="s-redmessage"></a>

### S_RedMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RedMessage.java`；SHA-256：`db392b94ff625e3f1da6e14b90d29271be946d49aefd2117a545a1e846e056ef`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RedMessage(final String acc, final String msg1)` @17 | 委派 `buildPacket(acc, new String[]{ msg1 })@18` |
| `private void buildPacket(final String acc, final String[] info)` @22 | `S(acc)@24`<br>〔`if info == null`〕 `C(0x00)@26`<br>〔`else`〕 `C(info.length)@29`<br>〔`else → for int i = 0 ; i < info.length ; i++`〕 `S(info[i])@31` |

<a id="s-removeobject"></a>

### S_RemoveObject

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RemoveObject.java`；SHA-256：`a3e3e0d61401bf29e702ddfd8055a54abdd170b064da24381748c12763bec5a1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RemoveObject(final L1Object obj)` @18 | `C(S_OPCODE_REMOVE_OBJECT)@20` → `D(obj.getId())@21` |

<a id="s-resurrection"></a>

### S_Resurrection

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Resurrection.java`；SHA-256：`736617a045c801347d74f8434c96d8f096a0c3375f33c4b4d082b5ae7a109c20`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Resurrection(final L1PcInstance target, final L1Character use, final int type)` @21 | `C(S_OPCODE_RESURRECTION)@22` → `D(target.getId())@23` → `C(type)@24` → `D(use.getId())@25` → `D(target.getClassId())@26` |
| `public S_Resurrection(final L1Character target, final L1Character use, final int type)` @36 | `C(S_OPCODE_RESURRECTION)@37` → `D(target.getId())@38` → `C(type)@39` → `D(use.getId())@40` → `D(target.getGfxId())@41` |
| `public S_Resurrection(final L1PcInstance target, final int opid, final int type)` @51 | `C(opid)@52` → `D(target.getId())@53` → `C(type)@54` → `D(target.getId())@55` → `D(target.getClassId())@56` |

<a id="s-retrievechalist"></a>

### S_RetrieveChaList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RetrieveChaList.java`；SHA-256：`1a4e59f517df13fac094b32d6d5e0a4dae1a1d215c4fd571512d41ca5d16c877`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RetrieveChaList(final int objid, final L1PcInstance pc)` @21 | `C(S_OPCODE_SHOWRETRIEVELIST)@25` → `D(objid)@26` → `H(size)@27` → `C(0x12)@28`<br>〔`if inventoryHasRoom → for final Object itemObject : pc.getDwarfForChaInventory().getItems()`〕 `D(item.getId())@32` → `C(i)@37` → `H(item.get_gfxid())@38` → `C(L1Item.toPacketBless(item.getBless()))@39` → `D((int) Math.min(item.getCount(), 2000000000))@40` → `C(item.isIdentified() ? 0x01 : 0x00)@41` → `S(item.getViewName())@42` |

<a id="s-retrievedevilkinglist"></a>

### S_RetrieveDevilKingList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RetrieveDevilKingList.java`；SHA-256：`afbd9c73ac7d5e3b5a242e95d416ad71b99613d5c45bda58dbfb52e927525a9a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RetrieveDevilKingList(final L1PcInstance pc, final int objid, final List<L1ItemInstance> items)` @23 | `C(S_OPCODE_SHOWRETRIEVELIST)@24` → `D(objid)@25` → `H(items.size())@26` → `C(0x0c)@27`<br>〔`for final L1ItemInstance item : items`〕 `D(itemobjid)@30` → `C(0x00)@32` → `H(item.get_gfxid())@33` → `C(L1Item.toPacketBless(item.getBless()))@34` → `D(1)@35` → `C(item.isIdentified() ? 0x01 : 0x00)@36` → `S(item.getViewName())@37` |

<a id="s-retrieveelflist"></a>

### S_RetrieveElfList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RetrieveElfList.java`；SHA-256：`9ac004532d09eb87a2ee9b7afb2a52f860029b6b5896920e6e9b5dd134a666b5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RetrieveElfList(final int objid, final L1PcInstance pc)` @21 | `C(S_OPCODE_SHOWRETRIEVELIST)@25` → `D(objid)@26` → `H(size)@27` → `C(0x09)@28`<br>〔`if inventoryHasRoom → for final Object itemObject : pc.getDwarfForElfInventory().getItems()`〕 `D(item.getId())@32` → `C(i)@37` → `H(item.get_gfxid())@38` → `C(L1Item.toPacketBless(item.getBless()))@39` → `D((int) Math.min(item.getCount(), 2000000000))@40` → `C(item.isIdentified() ? 0x01 : 0x00)@41` → `S(item.getViewName())@42` |

<a id="s-retrievelist"></a>

### S_RetrieveList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RetrieveList.java`；SHA-256：`1bdf02501d592d5658ed3e5a3d677ae592ff488807470d3e1e7c6bb1c6779f57`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RetrieveList(final int objid, final L1PcInstance pc)` @21 | `C(S_OPCODE_SHOWRETRIEVELIST)@26` → `D(objid)@27` → `H(size)@28` → `C(0x03)@29`<br>〔`if inventoryHasRoom → for final Object itemObject : pc.getDwarfInventory().getItems()`〕 `D(item.getId())@34` → `C(i)@39` → `H(item.get_gfxid())@40` → `C(L1Item.toPacketBless(item.getBless()))@41` → `D((int) Math.min(item.getCount(), 2000000000))@42` → `C(item.isIdentified() ? 0x01 : 0x00)@43` → `S(item.getViewName())@44`<br>`D(30)@50` |

<a id="s-retrievepledgelist"></a>

### S_RetrievePledgeList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RetrievePledgeList.java`；SHA-256：`83be9d6358b994fb24a66642293f60dce32e47db6bd71ac5f3f7c46cb4814f05`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RetrievePledgeList(final int objid, final L1PcInstance pc)` @23 | `C(S_OPCODE_SHOWRETRIEVELIST)@28` → `D(objid)@29`<br>〔`if clan == null \|\| !inventoryHasRoom`〕 `H(0x0000)@32` → `C(0x05)@33` → `D(30)@34`<br>〔`if inventoryHasRoom → if size > 0 → if !clan.tryClaimWarehouse(pc.getId())`〕 `H(0x0000)@48` → `C(0x05)@49` → `D(30)@50`<br>〔`if inventoryHasRoom → if size > 0`〕 `H(size)@53` → `C(0x05)@54`<br>〔`if inventoryHasRoom → if size > 0 → for final Object itemObject : clan.getDwarfForClanInventory().getItems()`〕 `D(item.getId())@57` → `C(i)@62` → `H(item.get_gfxid())@63` → `C(L1Item.toPacketBless(item.getBless()))@64` → `D((int) Math.min(item.getCount(), 2000000000))@65` → `C(item.isIdentified() ? 0x01 : 0x00)@66` → `S(item.getViewName())@67`<br>〔`if inventoryHasRoom → if size > 0`〕 `D(30)@70`<br>〔`if inventoryHasRoom → else`〕 `H(0x0000)@72` → `C(0x05)@73` → `D(30)@74` |

<a id="s-retrievepoweritem"></a>

### S_RetrievePowerItem

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RetrievePowerItem.java`；SHA-256：`387f8f0778f2f959590e69e07f3c97656269820f12174423d5c3e360340d5ab5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RetrievePowerItem(final L1PcInstance pc, final int objid, final List<L1ItemInstance> items)` @23 | `C(S_OPCODE_SHOWRETRIEVELIST)@24` → `D(objid)@25` → `H(items.size())@26` → `C(0x0a)@27`<br>〔`for final L1ItemInstance item : items`〕 `D(itemobjid)@30` → `C(0x00)@32` → `H(item.get_gfxid())@33` → `C(L1Item.toPacketBless(item.getBless()))@34` → `D(0x01)@36` → `C(item.isIdentified() ? 0x01 : 0x00)@37` → `S(item.getViewName())@38` |

<a id="s-retrieveshiftinglist"></a>

### S_RetrieveShiftingList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_RetrieveShiftingList.java`；SHA-256：`5c9b42c1106c56c0e83dcfd38e8732addbf866b6c02317801aec0e9f1db575cd`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_RetrieveShiftingList(final L1PcInstance pc, List<L1ItemInstance> items)` @23 | `C(S_OPCODE_SHOWRETRIEVELIST)@24` → `D(pc.getId())@25` → `H(items.size())@26` → `C(0x02)@27`<br>〔`for final L1ItemInstance item : items`〕 `D(itemid)@30` → `C(0x00)@32` → `H(item.get_gfxid())@33` → `C(L1Item.toPacketBless(item.getBless()))@34` → `D(1)@35` → `C(item.isIdentified() ? 0x01 : 0x00)@36` → `S(item.getViewName())@37` |

<a id="s-selecttarget"></a>

### S_SelectTarget

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SelectTarget.java`；SHA-256：`501d17863698d9d4773e3a9eb5ad3af73cf3b4dbe1ce20a237f1f81e3df3f76b`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SelectTarget(final int ObjectId)` @16 | `C(S_OPCODE_SELECTTARGET)@17` → `D(ObjectId)@18` → `C(0x00)@19` → `C(0x00)@20` → `C(0x02)@21` |

<a id="s-sellhouse"></a>

### S_SellHouse

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SellHouse.java`；SHA-256：`5099a1864602225664127c706649e0db1dd6543a6dd62e2eaeb12fd206f3ec26`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SellHouse(final int objectId, final String houseNumber)` @26 | 委派 `buildPacket(objectId, houseNumber)@27` |
| `public S_SellHouse(int objectId,String houseNumber,Object house)` @31 | 委派 `this(objectId,houseNumber)@32` |
| `private void buildPacket(final int objectId, final String houseNumber)` @36 | `C(S_OPCODE_INPUTAMOUNT)@38` → `D(objectId)@39` → `D(0)@40` → `D(100000)@41` → `D(100000)@42` → `D(2000000000)@43` → `H(0)@44` → `S("agsell")@45` → `S("agsell " + houseNumber)@46` |

<a id="s-servermessage"></a>

### S_ServerMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ServerMessage.java`；SHA-256：`6f2ae74bc885da8d7807c816b1e139629cb14924ca38e176a44f031d26bcaf92`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ServerMessage(final String name)` @25 | `C(S_OPCODE_NPCSHOUT)@26` → `C(0x00)@27` → `D(0x00000000)@28` → `S(name)@29` |
| `public S_ServerMessage(final int type)` @37 | `C(S_OPCODE_SERVERMSG)@38` → `H(type)@39` → `C(0x00)@40` |
| `public S_ServerMessage(final int type, final String msg1)` @49 | 委派 `buildPacket(type, new String[]{ msg1 })@50` |
| `public S_ServerMessage(final int type, final String msg1, final String msg2)` @60 | 委派 `buildPacket(type, new String[]{ msg1, msg2 })@61` |
| `public S_ServerMessage(final int type, final String msg1, final String msg2, final String msg3)` @72 | 委派 `buildPacket(type, new String[]{ msg1, msg2, msg3 })@73` |
| `public S_ServerMessage(final int type, final String[] info)` @107 | 委派 `buildPacket(type, info)@108` |
| `private void buildPacket(final int type, final String[] info)` @112 | `C(S_OPCODE_SERVERMSG)@113` → `H(type)@114`<br>〔`if info == null`〕 `C(0x00)@117`<br>〔`else`〕 `C(info.length)@120`<br>〔`else → for int i = 0 ; i < info.length ; i++`〕 `S(info[i])@122` |

<a id="s-serverversion"></a>

### S_ServerVersion

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ServerVersion.java`；SHA-256：`6f7259cc9d3e03d53d85ea21cc6f01e84f96a2e177d88c3d1db816d3f657f6a4`。

局部常數（subcode／格式旗標／限制）：`CLIENT_LANGUAGE=Config.CLIENT_LANGUAGE`；`SERVER_NO=0x01`；`UPTIME=(int) (System.currentTimeMillis() / 1000L)`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ServerVersion()` @14 | `C(S_OPCODE_SERVERVERSION)@15` → `C(0)@16` → `C(SERVER_NO)@17` → `D(0x07cbf4dd)@18` → `D(0x07cbf4dd)@19` → `D(0x77fc692d)@20` → `D(0x07cbf4d9)@21` → `D(0)@23` → `C(0)@25` → `C(0)@26` → `C(CLIENT_LANGUAGE)@27` → `D(0x77d82)@28` → `D(UPTIME)@29` → `H(1)@30` → `C(0)@31` |

<a id="s-shopbuylist"></a>

### S_ShopBuyList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopBuyList.java`；SHA-256：`3c2d3d1a37644c9eabeeb221282009dd8d9199ed1f166563235c5e703e922ff8`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopBuyList(final int objid, final L1PcInstance pc)` @22 | `C(S_OPCODE_SHOWSHOPSELLLIST)@23` → `D(objid)@24`<br>〔`if !(object instanceof L1NpcInstance npc)`〕 `H(0x0000)@28`<br>〔`if shop == null`〕 `H(0x0000)@35`<br>〔`if assessedItems.isEmpty()`〕 `H(0x0000)@44`<br>`H(assessedItems.size())@48`<br>〔`for final L1AssessedItem item : assessedItems`〕 `D(item.getTargetId())@51` → `D(item.getAssessedPrice())@52` |

<a id="s-shopbuylistall"></a>

### S_ShopBuyListAll

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopBuyListAll.java`；SHA-256：`0d48c5088a8700bbd09f7e1fd0c5e11aa0a8edfec713e16d4a10c4aca79d34d0`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopBuyListAll(final L1PcInstance pc, final L1NpcInstance npc)` @21 | `C(S_OPCODE_SHOWSHOPSELLLIST)@23` → `D(npc.getId())@24`<br>〔`if assessedItems.isEmpty()`〕 `H(0x0000)@29`<br>`H(assessedItems.size())@33`<br>〔`for final L1ItemInstance key : assessedItems.keySet()`〕 `D(key.getId())@36` → `D(assessedItems.get(key))@37` |

<a id="s-shopbuylistgam"></a>

### S_ShopBuyListGam

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopBuyListGam.java`；SHA-256：`e147eadd5a2a94a61a215ff4c31a24fae2c9276f0dbed2d3a5ea8911033a5171`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopBuyListGam(final L1PcInstance pc, final L1NpcInstance npc, final Map<Integer, L1Gambling> sellList)` @25 | `C(S_OPCODE_SHOWSHOPSELLLIST)@26` → `D(npc.getId())@27`<br>〔`if sellList.isEmpty()`〕 `H(0x0000)@30`<br>〔`if sellList.size() <= 0`〕 `H(0x0000)@35`<br>`H(sellList.size())@39`<br>〔`for final Integer itemobjid : sellList.keySet()`〕 `D(itemobjid)@42` → `D(adena)@45` |

<a id="s-shopbuylistlotto"></a>

### S_ShopBuyListLotto

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopBuyListLotto.java`；SHA-256：`e31c5cf1bc9190746a2e1ba388db0a1c76591285061577bf43a97842dccb60ea`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopBuyListLotto(final L1PcInstance pc, final L1NpcInstance npc, final Map<Integer, Long> sellList)` @15 | `C(S_OPCODE_SHOWSHOPSELLLIST)@16` → `D(npc.getId())@17`<br>〔`if sellList == null \|\| sellList.isEmpty()`〕 `H(0x0000)@19`<br>`H(sellList.size())@22`<br>〔`for final Integer itemobjid : sellList.keySet()`〕 `D(itemobjid)@24` → `D(pack)@27` |

<a id="s-shopselllist"></a>

### S_ShopSellList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopSellList.java`；SHA-256：`5dfec708356a1c95755295393d4b9cd3a984bf26fa16660d6bf1de87f873a551`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopSellList(final int objId)` @32 | 委派 `this(objId, null)@33` |
| `public S_ShopSellList(final int objId, final L1PcInstance pc)` @42 | `C(S_OPCODE_SHOWSHOPBUYLIST)@44` → `D(objId)@45`<br>〔`if !(npcObj instanceof L1NpcInstance npcInstance)`〕 `H(0x0000)@49`<br>〔`if shopItems.size() <= 0`〕 `H(0x0000)@59`<br>`H(shopItems.size())@63`<br>〔`for int i = 0; i < shopItems.size(); i++`〕 `D(i)@69` → `H(shopItem.getItem().getGfxId())@71` → `D(price)@73` → `S(nameBuilder.toString())@91`<br>〔`for int i = 0; i < shopItems.size(); i++ → if ConfigOther.SHOPINFO`〕 `C(status.length)@98`<br>〔`for int i = 0; i < shopItems.size(); i++ → if ConfigOther.SHOPINFO → for final byte b : status`〕 `C(b)@100`<br>〔`for int i = 0; i < shopItems.size(); i++ → else`〕 `C(0x00)@105`<br>`H(0x0007)@108` |
| `public S_ShopSellList(final L1NpcInstance npc)` @115 | 委派 `this(npc, null)@116` |
| `public S_ShopSellList(final L1NpcInstance npc, final L1PcInstance pc)` @125 | `C(S_OPCODE_SHOWSHOPBUYLIST)@127` → `D(npc.getId())@128`<br>〔`if shopItems.size() <= 0`〕 `H(0x0000)@136`<br>`H(shopItems.size())@140`<br>〔`for int i = 0; i < shopItems.size(); i++`〕 `D(i)@146` → `H(shopItem.getItem().getGfxId())@147` → `D(price)@148` → `S(nameBuilder.toString())@165` → `C(0x00)@168`<br>`H(0x0007)@170` |

<a id="s-shopselllistcn"></a>

### S_ShopSellListCn

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopSellListCn.java`；SHA-256：`a1b6740c18f2fad53be86cc27c0e4ec75b6c4add8b012f679c651b082d79a50a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopSellListCn(final L1PcInstance pc, final L1NpcInstance npc)` @31 | 委派 `this(pc, npc, true)@32` |
| `public S_ShopSellListCn(final L1PcInstance pc, final L1NpcInstance npc, final boolean showItemDetails)` @42 | 委派 `buildPacket(pc, npc.getId(), npc.getNpcId(), showItemDetails)@43` |
| `private void buildPacket(final L1PcInstance pc, final int tgObjid, final int npcid, final boolean showItemDetails)` @47 | `C(S_OPCODE_SHOWSHOPBUYLIST)@55` → `D(tgObjid)@57` → `H(shopItems.size())@59`<br>〔`for final L1ShopItem shopItem : shopItems`〕 `D(i)@68` → `H(item.getGfxId())@69` → `D(shopItem.getPrice())@70` → `S(nameBuilder.toString())@83`<br>〔`for final L1ShopItem shopItem : shopItems → if showItemDetails`〕 `C(status.length)@89`<br>〔`for final L1ShopItem shopItem : shopItems → if showItemDetails → for final byte b : status`〕 `C(b)@91`<br>〔`for final L1ShopItem shopItem : shopItems → else`〕 `C(0x00)@95`<br>`H(0x17d4)@99` |

<a id="s-shopselllistcnx"></a>

### S_ShopSellListCnX

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopSellListCnX.java`；SHA-256：`1bfcdfd5afadd779df26640cfef11cc9b1d3c2435d31d4c56094354fb46bf2ad`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopSellListCnX(final L1PcInstance pc, final int tgObjid)` @23 | 委派 `buildPacket(pc, tgObjid)@24` |
| `private void buildPacket(final L1PcInstance pc, final int tgObjid)` @28 | `C(S_OPCODE_SHOWSHOPBUYLIST)@30` → `D(tgObjid)@32`<br>〔`if shopItems.size() <= 0`〕 `H(0x0000)@37`<br>`H(shopItems.size())@41`<br>〔`for final L1ShopItem shopItem : shopItems`〕 `D(i)@50` → `H(item.getGfxId())@51` → `D(shopItem.getPrice())@52` → `S(nameBuilder.toString())@66` → `C(status.length)@72`<br>〔`for final L1ShopItem shopItem : shopItems → for final byte b : status`〕 `C(b)@74`<br>`H(0x17d4)@80` |

<a id="s-shopselllistgam"></a>

### S_ShopSellListGam

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopSellListGam.java`；SHA-256：`6c655b3abe64eeccdfb9e5e98c8c0d9bc37604001ad78f2f66ee7bdd35af8d53`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopSellListGam(final L1PcInstance pc, final L1NpcInstance npc)` @25 | `C(S_OPCODE_SHOWSHOPBUYLIST)@26` → `D(npc.getId())@27`<br>〔`if list.size() <= 0`〕 `H(0x0000)@32`<br>`H(list.size())@36`<br>〔`for final GamblingNpc gamblingNpc : list.values()`〕 `D(i)@46` → `H(item.getGfxId())@47` → `D(GamblingManager.GAMADENA)@49` → `S(stringBuilder.toString())@56` → `C(0x00)@57`<br>`H(0x0007)@60` |

<a id="s-shopselllistlotto"></a>

### S_ShopSellListLotto

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopSellListLotto.java`；SHA-256：`75e28cf1a29fad68113a4f6cb218cf3c79883fec4eb5d751beb1dbf0adf5ad9f`。

局部常數（subcode／格式旗標／限制）：`ORDER_ID=1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopSellListLotto(final L1PcInstance pc, final L1NpcInstance npc)` @18 | `C(S_OPCODE_SHOWSHOPBUYLIST)@19` → `D(npc.getId())@20` → `H(1)@21` → `D(ORDER_ID)@28` → `H(gfx)@29` → `D(price)@30` → `S(suf.isEmpty() ? name : name + suf)@36` → `C(0x00)@37` → `H(0x0007)@38` |

<a id="s-shopselllisttmp"></a>

### S_ShopSellListTmp

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopSellListTmp.java`；SHA-256：`6c3cff310bbc4d89882f9e399a4ff7737a04224db30d552b6d792e990418f37d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopSellListTmp(final int objId, int tmp)` @22 | `C(S_OPCODE_SHOWSHOPBUYLIST)@26` → `D(objId)@27` → `H(size)@29`<br>〔`for int i = 0; i < size; i++`〕 `D(i)@33` → `H(gfx)@34` → `D(0x00)@35` → `S("gfxid = " + gfx)@36` → `C(0x00)@38`<br>`H(0x0007)@40` |

<a id="s-shopselllistx1"></a>

### S_ShopSellListX1

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShopSellListX1.java`；SHA-256：`58c0d2740fb2b9e3a7548d4a7fecb97503c00fc1e0e72f5f4b496b514c6bb948`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShopSellListX1(final int objId)` @27 | `C(S_OPCODE_SHOWSHOPBUYLIST)@29` → `D(objId)@30`<br>〔`if !(npcObj instanceof L1NpcInstance)`〕 `H(0x0000)@34`<br>〔`if shopItems.size() <= 0`〕 `H(0x0000)@44`<br>`H(shopItems.size())@48`<br>〔`for int i = 0; i < shopItems.size(); i++`〕 `H(i)@54` → `H(shopItem.getItem().getGfxId())@56` → `H(price)@58`<br>〔`for int i = 0; i < shopItems.size(); i++ → if shopItem.getPackCount() > 1`〕 `S(item.getNameId() + " (" + shopItem.getPackCount() + ")")@61`<br>〔`for int i = 0; i < shopItems.size(); i++ → else`〕 `S(item.getNameId())@64`<br>〔`for int i = 0; i < shopItems.size(); i++`〕 `C(0x00)@68`<br>`H(0x0000)@83` |
| `public S_ShopSellListX1(final L1NpcInstance npc)` @90 | `C(S_OPCODE_SHOWSHOPBUYLIST)@91` → `D(npc.getId())@92`<br>〔`if shopItems.size() <= 0`〕 `H(0x0000)@100`<br>`H(shopItems.size())@104`<br>〔`for int i = 0; i < shopItems.size(); i++`〕 `D(i)@110` → `H(shopItem.getItem().getGfxId())@111` → `D(price)@112`<br>〔`for int i = 0; i < shopItems.size(); i++ → if shopItem.getPackCount() > 1`〕 `S(item.getNameId() + " (" + shopItem.getPackCount() + ")")@114`<br>〔`for int i = 0; i < shopItems.size(); i++ → else`〕 `S(item.getNameId())@117`<br>〔`for int i = 0; i < shopItems.size(); i++`〕 `C(0x00)@121`<br>`H(0x0000)@136` |

<a id="s-showdrop"></a>

### S_ShowDrop

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShowDrop.java`；SHA-256：`b58cb29be61c3d588e95b9f7a296cee4da6dbe89800118260c6053b0691acf93`。

局部常數（subcode／格式旗標／限制）：`SHOW_EXP=0`；`SHOW_ADENA=1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShowDrop(final int type, final int amount)` @16 | `C(S_OPCODE_PACKETBOX)@17` → `C(192)@18` → `C(type)@19` → `D(amount)@20` |

<a id="s-showpolylist"></a>

### S_ShowPolyList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShowPolyList.java`；SHA-256：`a6e44a8e8101569d36953f8a413d672854c9d3ec7e93cc0b74a809b3f3bd35fb`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShowPolyList(final int objid)` @18 | `C(S_OPCODE_SHOWHTML)@19` → `D(objid)@20` → `S("monlist")@21` |
| `public S_ShowPolyList(final L1Character target)` @29 | `C(S_OPCODE_SHOWHTML)@30` → `D(target.getId())@31` → `S("monlist")@32` |

<a id="s-showsummonlist"></a>

### S_ShowSummonList

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ShowSummonList.java`；SHA-256：`18c966548e91c9b47a338a0151278a828ababc191b3e8884e18d66a704f11146`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ShowSummonList(final int objid)` @12 | `C(S_OPCODE_SHOWHTML)@13` → `D(objid)@14` → `S("summonlist")@15` |

<a id="s-skillbrave"></a>

### S_SkillBrave

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillBrave.java`；SHA-256：`de52229b7de055ed83eae2f7dd38b73258611c947e03ffd23c96b31cbc56ba80`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillBrave(final int objid, final int mode, final int time)` @22 | `C(S_OPCODE_SKILLBRAVE)@24` → `D(objid)@25` → `C(mode)@26` → `H(time)@27` |
| `public S_SkillBrave(final int objid, final int mode)` @36 | `C(S_OPCODE_SKILLBRAVE)@38` → `D(objid)@39` → `C(mode)@40` → `H(300)@41` |

<a id="s-skillbuy"></a>

### S_SkillBuy

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillBuy.java`；SHA-256：`09628d128beb0187b8bf2e26bc17c27fddf995bc9c6bdca0f5e8d1dc03e414de`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillBuy(final L1PcInstance pc, final ArrayList<Integer> newSkillList)` @30 | 委派 `this(pc, null, newSkillList)@31` |
| `public S_SkillBuy(final L1PcInstance pc, final com.lineage.server.model.Instance.L1NpcInstance npc, final ArrayList<Integer> newSkillList)` @34 | 〔`if newSkillList.size() <= 0`〕 `C(S_OPCODE_SKILLBUY)@38` → `H(0x0000)@39`<br>〔`else`〕 `C(S_OPCODE_SKILLBUY)@42` → `D(6000)@43` → `H(newSkillList.size())@44`<br>〔`else → for final Integer integer : newSkillList`〕 `D(integer)@46` |
| `public S_SkillBuy(L1PcInstance pc,com.lineage.server.model.Instance.L1NpcInstance npc, ArrayList<Integer> skills,UiOperationContext.Ticket prerequisite)` @56 | 委派 `this(pc,npc,skills)@58` |

<a id="s-skillbuycn"></a>

### S_SkillBuyCN

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillBuyCN.java`；SHA-256：`5c7fcd54829547198bf1d315b96517d3219a6079acbf6f59f225dcc73cc6d86f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillBuyCN(final L1PcInstance pc, final L1NpcInstance npc)` @36 | 〔`if newSkillList.size() <= 0`〕 `C(S_OPCODE_SHOWHTML)@75` → `D(npc.getId())@76` → `S("y_skill_02")@77` → `H(0x00)@78` → `H(0x00)@79`<br>〔`else`〕 `C(S_OPCODE_SKILLBUY)@86` → `D(startAdena)@87` → `H(newSkillList.size())@88`<br>〔`else → for final Integer integer : newSkillList`〕 `D(integer)@90` |

<a id="s-skillbuyitem"></a>

### S_SkillBuyItem

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillBuyItem.java`；SHA-256：`2652dfc43a61b96d384f7850c81d72ddeaaf3019f5499bd138ebaf8a8d7bfd67`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillBuyItem(final L1PcInstance pc, final L1NpcInstance npc)` @30 | 〔`if newSkillList.size() <= 0`〕 `C(S_OPCODE_SKILLBUYITEM)@47` → `H(0x0000)@48`<br>〔`else`〕 `C(S_OPCODE_SKILLBUYITEM)@52` → `H(newSkillList.size())@53`<br>〔`else → for final Integer integer : newSkillList`〕 `D(integer)@55` |

<a id="s-skillbuyitemcn"></a>

### S_SkillBuyItemCN

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillBuyItemCN.java`；SHA-256：`c42d71cf4751f85cfb1a22a307f59cd9e92d424a5885f73e95f56fd97e07e899`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillBuyItemCN(final L1PcInstance pc, final L1NpcInstance npc)` @24 | 〔`if newSkillList.size() <= 0`〕 `C(S_OPCODE_SKILLBUYITEM)@59` → `H(0x0000)@60`<br>〔`else`〕 `C(S_OPCODE_SKILLBUYITEM)@65` → `H(newSkillList.size())@66`<br>〔`else → for final Integer integer : newSkillList`〕 `D(integer)@68` |

<a id="s-skillhaste"></a>

### S_SkillHaste

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillHaste.java`；SHA-256：`365938971888140370dafa5b219ce48dcbc12a8ed30c4bd53b551d09badac09c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillHaste(final int objid, final int mode, final int time)` @21 | `C(S_OPCODE_SKILLHASTE)@23` → `D(objid)@24` → `C(mode)@25` → `H(time)@26` |

<a id="s-skilliconblessofeva"></a>

### S_SkillIconBlessOfEva

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillIconBlessOfEva.java`；SHA-256：`43eef6d336e1a0ba66305aaf1cb0a763445279ce8aeec78f16f3ee7ea20c311d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillIconBlessOfEva(final int objectId, final int time)` @15 | `C(S_OPCODE_BLESSOFEVA)@16` → `D(objectId)@17` → `H(time)@18` |

<a id="s-skilliconshield"></a>

### S_SkillIconShield

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillIconShield.java`；SHA-256：`42b6de9985acd196d51f278278d66f513e98081529e8f1fe180b1cfc3a8c05a8`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillIconShield(final int type, final int time)` @17 | `C(S_OPCODE_SKILLICONSHIELD)@19` → `H(time)@20` → `C(type)@21` |

<a id="s-skillsound"></a>

### S_SkillSound

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SkillSound.java`；SHA-256：`d59df752b4bacbd0158d9962afed424cc9c876d348d1154f633ac0e38b986ccb`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SkillSound(final int objid, final int gfxid)` @17 | 委派 `buildPacket(objid, gfxid)@18` |
| `private void buildPacket(final int objid, final int gfxid)` @22 | `C(S_OPCODE_SKILLSOUNDGFX)@24` → `D(objid)@25` → `H(gfxid)@26` |
| `public S_SkillSound(final int objid, final int gfxid, final int time)` @35 | `C(S_OPCODE_SKILLSOUNDGFX)@37` → `D(objid)@38` → `H(gfxid)@39` → `H(time)@40` |

<a id="s-sound"></a>

### S_Sound

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Sound.java`；SHA-256：`b7cae793e3c64f562fc4655ee6dba153f30ce55ae86ce072078bd73f3e4bd06f`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Sound(final int sound)` @17 | 委派 `buildPacket(sound, 0)@18` |
| `public S_Sound(final int sound, final int repeat)` @28 | 委派 `buildPacket(sound, repeat)@29` |
| `private void buildPacket(final int sound, final int repeat)` @33 | `C(S_OPCODE_SOUND)@35` → `C(repeat)@36` → `H(sound)@37` |

<a id="s-spmr"></a>

### S_SPMR

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SPMR.java`；SHA-256：`ab57b5a80b8e4406ddea06e0258071caba472337548cfec2df9927782d6f2823`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SPMR(final L1PcInstance pc, final boolean isLogin)` @17 | `C(S_OPCODE_SPMR)@18` → 委派 `writeSp(pc.getTrueSp())@20` → 委派 `writeMr(pc.getBaseMr())@21` |
| `public S_SPMR(final L1PcInstance pc)` @29 | 委派 `buildPacket(pc)@30` |
| `private void buildPacket(final L1PcInstance pc)` @34 | `C(S_OPCODE_SPMR)@35` → 委派 `writeSp(sp)@74` → 委派 `writeMr(mr)@75` |
| `private void writeSp(final int sp)` @78 | 〔`if ConfigClient.isROClient()`〕 `D(sp)@80`<br>〔`else`〕 `C(sp)@82` |
| `private void writeMr(final int mr)` @257 | 〔`if ConfigClient.isROClient()`〕 `D(mr)@259`<br>〔`else`〕 `C(mr)@261` |
| `public S_SPMR()` @269 | `C(S_OPCODE_SPMR)@270` → `C(50)@271` → `C(100)@272` |

<a id="s-strup"></a>

### S_Strup

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Strup.java`；SHA-256：`e56401fbaa79cb5fbce24c182efd44ca96158495e5090481083dd1e68e60fa0c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Strup(final L1PcInstance pc, final int type, final int time)` @20 | `C(S_OPCODE_STRUP)@22` → `H(time)@23` → `C(pc.getStr())@24` → `C(pc.getInventory().getWeight240())@25` → `C(type)@26` |

<a id="s-systemmessage"></a>

### S_SystemMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_SystemMessage.java`；SHA-256：`5b137ec3683e6170bc4ebc960c290cd672d24513d8bfe9c7c69d02296ddc85ed`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_SystemMessage(final String msg)` @14 | `C(S_OPCODE_GLOBALCHAT)@15` → `C(0x09)@16` → `S(msg)@17` |
| `public S_SystemMessage(final String msg, final boolean nameid)` @29 | `C(S_OPCODE_NPCSHOUT)@30` → `C(2)@31` → `D(0)@32` → `S(msg)@33` |

<a id="s-taxrate"></a>

### S_TaxRate

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_TaxRate.java`；SHA-256：`2973e2e0b4c8914bce29bb6360755a2d1ad3c7e6cdf32b751da84d9a3fc021cf`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_TaxRate(final int objecId)` @13 | 委派 `this(objecId, null, null)@14` |
| `public S_TaxRate(final int objecId, final Offer offer, final Ticket prerequisite)` @17 | `C(S_OPCODE_TAXRATE)@20` → `D(objecId)@21` → `C(0x0a)@22` → `C(0x32)@23` |

<a id="s-teleport"></a>

### S_Teleport

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Teleport.java`；SHA-256：`9080faaf3aaae8c2be0e3c63a15de74119f5831b40293af234b4f3b13e5b7f0c`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Teleport()` @20 | `C(S_OPCODE_TELEPORT)@22` → `C(0xeb)@24` → `C(0xa7)@25` → `D(_nextId.incrementAndGet())@27` |

<a id="s-teleport2"></a>

### S_Teleport2

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Teleport2.java`；SHA-256：`4fdbeb813375129256f1fa5811896ae49834563dfd1a87698b6a27cc35219430`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Teleport2(final int mapid, final int id, final L1PcInstance owner, final long generation, final long connection)` @18 | 委派 `this(mapid, id)@20` |
| `public S_Teleport2(final int mapid, final int id)` @36 | `C(S_OPCODE_TELEPORT)@38` → `H(mapid)@39` → `D(id)@40` |

<a id="s-togmmessage"></a>

### S_ToGmMessage

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_ToGmMessage.java`；SHA-256：`1bac008f244c17234b57f450a8e1754dd127e49cad8f0646fc415cd0a8faf87e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_ToGmMessage(final String info)` @16 | `C(S_OPCODE_NPCSHOUT)@17` → `C(0x00)@18` → `D(0x00000000)@19` → `S("\\fY" + info)@20` |

<a id="s-trade"></a>

### S_Trade

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Trade.java`；SHA-256：`c055c7f2c4cc31ad852c7aa0e9344c415d2aa454b1fd5dccdd1e11a77562f43e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Trade(final String name)` @16 | `C(S_OPCODE_TRADE)@17` → `S(name)@18` |

<a id="s-tradeadditem"></a>

### S_TradeAddItem

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_TradeAddItem.java`；SHA-256：`0a04d8f3c942a160e0ebfbb95f82f6e68837bd4d105828ecc08d09c7a5a5a00d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_TradeAddItem(final L1ItemInstance item, final long count, final int type)` @19 | `C(S_OPCODE_TRADEADDITEM)@21` → `C(type)@22` → `H(item.getItem().getGfxId())@23` → `S(name)@27` → `C(L1Item.toPacketBless(item.getBless()))@36` |
| `public S_TradeAddItem()` @43 | `C(S_OPCODE_TRADEADDITEM)@44` → `C(0x01)@45` → `H(714)@46` → `S("測試物品(55)")@47` → `C(0x00)@49` |

<a id="s-tradestatus"></a>

### S_TradeStatus

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_TradeStatus.java`；SHA-256：`f793630e477aa9635d6467766babf3468addcc6e011554e98d674b23c0d5316e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_TradeStatus(final int type)` @16 | `C(S_OPCODE_TRADESTATUS)@17` → `C(type)@18` |

<a id="s-trap"></a>

### S_Trap

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Trap.java`；SHA-256：`4c982c7edd5b38294d45f270a8c4d6e9ee290dd1bbb4b47f5eb69de09a70bf3e`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Trap(final L1TrapInstance trap, final String name)` @19 | `C(S_OPCODE_CHARPACK)@20` → `H(trap.getX())@21` → `H(trap.getY())@22` → `D(trap.getId())@23` → `H(0x0007)@24` → `C(0x00)@25` → `C(0x00)@26` → `C(0x00)@27` → `C(0x00)@28` → `D(0x00000000)@29` → `H(0x0000)@30` → `S(name)@31` → `S(null)@32` → `C(0x00)@33` → `D(0x00000000)@34` → `S(null)@35` → `S(null)@36` → `C(0x00)@37` → `C(0xFF)@38` → `C(0x00)@39` → `C(0x00)@40` → `C(0x00)@41` → `C(0xFF)@42` → `C(0xFF)@43` |

<a id="s-truetarget"></a>

### S_TrueTarget

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_TrueTarget.java`；SHA-256：`8814433e64d0eee71d844d9f3fa48a935b62daeda5f540c0ebc2c31ec0a9c7a5`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_TrueTarget(final int targetId, final int objectId, final String message)` @18 | 委派 `buildPacket(targetId, objectId, message)@19` |
| `private void buildPacket(final int targetId, final int objectId, final String message)` @23 | `C(S_OPCODE_TRUETARGET)@24` → `D(targetId)@25` → `D(objectId)@26` → `S(message)@27` |
| `public S_TrueTarget(final int targetId, final int gfxid)` @30 | `C(S_OPCODE_TRUETARGET)@31` → `D(targetId)@32` → `D(targetId)@33` → `S(null)@34` → `H(gfxid)@35` |

<a id="s-unknown-b"></a>

### S_Unknown_B

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Unknown_B.java`；SHA-256：`7bc8e6b38ef6680bdd0203d09d526c9287c77eee0470bb5ad7da7de68c0fb508`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Unknown_B()` @16 | `C(S_OPCODE_CHARRESET)@21` → `C(0x0a)@23` → `C(0x02)@24` → `C(0x00)@25` → `C(0x00)@26` → `C(0x00)@27` → `C(0x2b)@28` → `C(0x7f)@29` |

<a id="s-usearrowskill"></a>

### S_UseArrowSkill

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_UseArrowSkill.java`；SHA-256：`870d1757e281e346a635c52e6f3b4cf8b5dc22a06959ff5c88a1029bc6a0a769`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_UseArrowSkill(final L1Character cha, final int targetobj, final int spellgfx, final int x, final int y, final int dmg)` @24 | `C(S_OPCODE_ATTACKPACKET)@52` → `C(aid)@53` → `D(cha.getId())@54` → `D(HitReactionSuppressTable.get().resolveTargetObjId(targetobj))@55` → `damageWordU16(this, dmg)@58` → `C(cha.getHeading())@60` → `D(0x00000152)@63` → `H(spellgfx)@65` → `C(0x7f)@66` → `H(cha.getX())@67` → `H(cha.getY())@68` → `H(x)@69` → `H(y)@70` → `D(0x00000000)@72` → `C(0x00)@73` |
| `public S_UseArrowSkill(final L1Character cha, final int targetobj, final int spellgfx, final int x, final int y, final int dmg, final int actionId)` @77 | `C(S_OPCODE_ATTACKPACKET)@84` → `C(actionId)@85` → `D(cha.getId())@86` → `D(HitReactionSuppressTable.get().resolveTargetObjId(targetobj))@87` → `damageWordU16(this, dmg)@90` → `C(cha.getHeading())@92` → `D(0x00000152)@93` → `H(spellgfx)@95` → `C(0x7f)@96` → `H(cha.getX())@97` → `H(cha.getY())@98` → `H(x)@99` → `H(y)@100` → `D(0x00000000)@102` → `C(0x00)@103` |
| `public S_UseArrowSkill(final L1Character cha, final int spellgfx, final int x, final int y )` @115 | `C(S_OPCODE_ATTACKPACKET)@126` → `C(aid)@127` → `D(cha.getId())@128` → `D(0x00)@129` → `H(0x00)@130` → `C(cha.getHeading())@131` → `D(0x00000152)@134` → `H(spellgfx)@136` → `C(0x7f)@137` → `H(cha.getX())@138` → `H(cha.getY())@139` → `H(x)@140` → `H(y)@141` → `D(0x00000000)@143` → `C(0x00)@144` |

<a id="s-useattackskill"></a>

### S_UseAttackSkill

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_UseAttackSkill.java`；SHA-256：`83e00ecf1da363510643ae259d8da3c43d99508fef9edb36fa9dacf74403dcfe`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_UseAttackSkill(final L1Character cha, final int targetobj, final int spellgfx, final int x, final int y, final int actionId, final boolean motion)` @33 | 委派 `buildPacket(cha, targetobj, spellgfx, x, y, actionId, 0, motion)@35` |
| `public S_UseAttackSkill(final L1Character cha, final int targetobj, final int spellgfx, final int x, final int y, final int actionId, final int dmg)` @49 | 委派 `buildPacket(cha, targetobj, spellgfx, x, y, resolvedActionId, dmg, true)@53` |
| `private void buildPacket(final L1Character cha, final int targetobj, final int spellgfx, final int x, final int y, int actionId, final int dmg, final boolean withCastMotion)` @68 | `C(S_OPCODE_ATTACKPACKET)@111` → `C(actionId)@112` → `D(withCastMotion ? cha.getId() : 0x00000000)@113` → `D(HitReactionSuppressTable.get().resolveTargetObjId(targetobj))@114`<br>〔`if com.lineage.config.ConfigClient.isROClient()`〕 `H(dmg)@120`<br>〔`else`〕 `H(dmg > 0 ? 0x000a : 0x0000)@122`<br>`C(newheading)@125` → `D(_sequentialNumber.incrementAndGet())@128` → `H(spellgfx)@130` → `C(0x00)@131` → `H(cha.getX())@132` → `H(cha.getY())@133` → `H(x)@134` → `H(y)@135` → `D(0x00000000)@137` → `C(0x00)@138` |

<a id="s-war"></a>

### S_War

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_War.java`；SHA-256：`a9eea0aa678c021127f29bdb53cb52f7e04b6243cc46a9e7ad322a5e4812b348`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_War(final int type, final String clan_name1, final String clan_name2)` @25 | 委派 `buildPacket(type, clan_name1, clan_name2)@26` |
| `private void buildPacket(final int type, final String clan_name1, final String clan_name2)` @30 | `C(S_OPCODE_WAR)@39` → `C(type)@40` → `S(clan_name1)@41` → `S(clan_name2)@42` |

<a id="s-wartime"></a>

### S_WarTime

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_WarTime.java`；SHA-256：`98f74c6634ee3e2be0529ba3a5f2d8ac88bb02cfb363ef3937e9f523fef1401a`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_WarTime(final Calendar cal)` @22 | `H(0x0006)@38` → `S(Config.TIME_ZONE)@39` → `C(0x00)@40` → `C(0x00)@41` → `C(0x00)@42` → `D(time)@43` → `C(0x00)@44` → `D(time - 1)@45` → `C(0x00)@46` → `D(time - 2)@47` → `C(0x00)@48` → `D(time - 3)@49` → `C(0x00)@50` → `D(time - 4)@51` → `C(0x00)@52` → `D(time - 5)@53` → `C(0x00)@54` |
| `public S_WarTime(int op)` @62 | `C(op)@76` → `H(6)@77` → `S(Config.TIME_ZONE)@78` → `C(0)@79` → `C(0)@80` → `C(0)@81` → `D(time)@82` → `C(0)@83` → `D(time - 1)@84` → `C(0)@85` → `D(time - 2)@86` → `C(0)@87` → `D(time - 3)@88` → `C(0)@89` → `D(time - 4)@90` → `C(0)@91` → `D(time - 5)@92` → `C(0)@93` |

<a id="s-weaponrange"></a>

### S_WeaponRange

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_WeaponRange.java`；SHA-256：`77f1fc1074a4de34bd4f76f64f3f4b82c566758987cf176b24dcdd1024c77ca1`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_WeaponRange(final int size)` @12 | `C(S_OPCODE_PACKETBOX)@13` → `C(160)@14` → `C(size == -1 ? 15 : size)@15` |

<a id="s-weather"></a>

### S_Weather

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_Weather.java`；SHA-256：`4e1d228c83c39bc5b034e0acff23732ce019530add9cf0b4aa664897e5aa2889`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_Weather(final int weather)` @19 | 委派 `buildPacket(weather)@20` |
| `private void buildPacket(final int weather)` @24 | `C(S_OPCODE_WEATHER)@25` → `C(weather)@26` |

<a id="s-whoamount"></a>

### S_WhoAmount

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_WhoAmount.java`；SHA-256：`53818c57e720d11e3016f9219dabddf01fe020ddf66c45ff023e92c271bff66d`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_WhoAmount(final String amount)` @7 | `C(S_OPCODE_SERVERMSG)@8` → `H(0x0051)@9` → `C(0x01)@10` → `S(amount)@11` |

<a id="s-whocharinfo"></a>

### S_WhoCharinfo

來源：`1-伺服器/src/com/lineage/server/serverpackets/S_WhoCharinfo.java`；SHA-256：`31cc89b1056970582818228273883dfdeacdd57666fb7c688d96c38c2aee5f15`。

| 方法簽章／行號 | 有序欄位簽章（型別長度見前表；分支不可串接） |
|---|---|
| `public S_WhoCharinfo(final L1Character cha)` @20 | `C(S_OPCODE_SERVERMSG)@33` → `H(0x00a6)@34` → `C(0x01)@35` → `S(title + name + " " + lawfulness + " " + clan)@62` |

## 共用來源 SHA-256

| 相對來源 | SHA-256 |
|---|---|
| `1-伺服器/src/com/lineage/echo/OpcodesClient.java` | `3b9df4518cad4ab3021d7a6454f88fe4513bd715eb3eb461ffaa873a64fe8931` |
| `1-伺服器/src/com/lineage/echo/PacketHandler.java` | `001014e4f858abaef5734efe558d1759f97f85d4314d27bd44fefd72d4b3cd30` |
| `1-伺服器/src/com/lineage/echo/SessionPacketPolicy.java` | `e6b33150986f64550a1f02b148c68fc27f5085b41179d2796aac833a80dce8b2` |
| `1-伺服器/src/com/lineage/echo/SessionPhaseGate.java` | `ea7e28fd276ba9b469222b3a655be279d729b69811f06978aaff74877b52c13a` |
| `1-伺服器/src/com/lineage/server/clientpackets/ClientBasePacket.java` | `83b48cc84680c6238d2491c71a776ec5beb542c39b6232f3a1b4e620aaf77636` |
| `1-伺服器/src/com/lineage/server/clientpackets/ClientPacketStructure.java` | `ca88c05673188ec1b99a961be0d67561808f9bd42adf8a937b9ebda6767e96a9` |
| `1-伺服器/src/com/lineage/server/serverpackets/OpcodesServer.java` | `9044cb98cf8a0548a421fbb05e6cdcef26147b2a2b85a1ef896fde8a8016f152` |
| `1-伺服器/src/com/lineage/server/serverpackets/ServerBasePacket.java` | `3348c199e9694bd1eec050b130f28ab99590cae16f0047964c17b32df309a439` |
| `1-伺服器/src/com/lineage/server/serverpackets/ItemStatusPacketSupport.java` | `287916759504c6f1b756231a61d33c841958adbf83f8fdebc2f94f80ce19191c` |
| `1-伺服器/src/com/lineage/netty/NettyChannelInitializer.java` | `6549a355719866c148f99cc8a5a5707143167919698e5362e03761507e5d239b` |
| `1-伺服器/src/com/lineage/netty/codec/L1CipherEncoder.java` | `09757b2c141a0b69ac8c1e83688815ae5e830d6543e67d3b02f15df91d061c1f` |
| `1-伺服器/src/com/lineage/netty/codec/L1CipherDecoder.java` | `365dc611d9b0a93fad40863d53e59d7f0175fe907d288748d9253941c56b5499` |
