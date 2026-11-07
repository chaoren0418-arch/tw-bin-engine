# 本機來源與 P24Lab d287 JAR 封包版本核對

Issue #226／T123，核對日期 2026-11-07。以 #132 的封包索引與 SHA-256 為固定清單，逐一核對宣告方法、opcode 常數及靜態分派。

## 結論

**來源快照與 d287 存在明確版本差異。** 346 個具名封包來源檔中，334 個的宣告方法簽章相符，2 個有簽章差異，9 個啟用類別在 JAR 中缺席，另 1 個來源全停用且 JAR 無類別。另補核 #132 正文提及的內部 `Teleportation.S_TeleportRecovery`，4 個方法簽章相符。

107 個 C opcode 常數與 112 個 S opcode 常數的名稱及數值全部相符。C63 的兩個常數別名也保留。96 個具名 C 處理器的 opcode 分派全部相符，`C_Unkonwn` 另作預設分派。S242/sub1 與 sub2 的對應類別均存在。

「簽章相符」的核對範圍為方法名稱、可見性、修飾詞、回傳型別、參數型別、泛型與 throws 宣告。方法本文、所有條件分支、wire 完整布局與實機行為仍待各功能任務驗證。

| 分類 | C | S | 合計 |
|---|---:|---:|---:|
| 宣告方法簽章相符 | 102 | 232 | 334 |
| 宣告方法簽章差異 | 1 | 1 | 2 |
| 啟用來源類別在 JAR 缺席 | 0 | 9 | 9 |
| 來源全停用且 JAR 無類別 | 0 | 1 | 1 |
| #132 來源索引總數 | 103 | 243 | 346 |

## 證據身分與方法

- 工作分支基準為 `28a8cab3ef0c940caf55e1fe1e2c26e5d8c68cae`。
- 封包表 `docs/reference/server-protocol.md` 的 SHA-256 為 `fb384416c8be0a59454fc34a2ac7da65f38ea278d7fea056b48049f04dea545d`。
- 本機來源根為 `D:/lineage開發/1-伺服器/src/`。346 個封包來源檔及 2 個 opcode 來源檔均與 #132 所列 SHA-256 相符。逐檔雜湊列於附錄。
- 分派來源 `com/lineage/echo/PacketHandler.java` 的 SHA-256 為 `001014e4f858abaef5734efe558d1759f97f85d4314d27bd44fefd72d4b3cd30`，亦與 #132 相符。
- JAR 位於 `D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，長度 6,928,681 bytes，SHA-256 為 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。歷史文件的 `D:/P24Lab-ClientEngine` 路徑在本機已不存在，本輪使用搬移後檔案。
- 開始核對時，調度器 `http://localhost:8787/api/state` 回報 lease 持有人為 `chaoren0418-arch/tw-bin-engine#226`，到期時間為 `2026-11-07T00:25:56.676078Z`，與派工到期秒一致。
- JDK 工具為 Microsoft OpenJDK `17.0.17`。對 JAR 使用 `javap -p -constants`，以 ZIP 目錄核實缺席類別。JAR 中具名頂層 C_/S_ 類別恰為 336 個，均在 #132 索引內。
- 原始碼用 JDK `JavacTask.parse()` 做語法解析，解析錯誤 0。解析器僅執行自建的分析工具，停用 annotation processor，目標來源僅進 parse 階段。以 import、同 package、java.lang 與內部類別解析完整型別，補上 Java 隱含無參數建構子，忽略參數名稱、參數 final、註解及修飾詞排列差異。
- 336 個頂層類別共核對 1,643 個宣告方法簽章，另保留 231 個 `ACC_SYNTHETIC` 方法及 120 個類別初始化器的盤點。以 class-file 方法表獨立驗證 javap 的方法數量與 synthetic 旗標。
- 為確認常數使用點，另以 `javap -p -c` 在記憶體中解析 C 分派與各 S 方法的第一個直接 `writeC` 引數，僅保留方法簽章、整數與比對結論。353 個常數位置相符，9 個位置由參數或運算決定，另有來源新增的 `S_Message_YN` 私有建構子缺席。這些位置也包含 helper 的資料欄位，逐項見附錄。
- 交付內容限於簽章、常數、檔案身分與結論。暫存分析工具及摘要放在本 worktree 的 `scratch/issue-226/`，產品程式與外部來源皆採唯讀。

可在已核定的 JAR 路徑重現單類別查詢，完整類別名由附錄的來源路徑去掉 `.java` 並把 `/` 換為 `.`。

```powershell
$jarPath = 'D:\lineage開發\TW1608262200研究\environments\p24\server\Server_Game.jar'
Get-FileHash -LiteralPath $jarPath -Algorithm SHA256
javap -J-Dfile.encoding=UTF-8 -p -constants -classpath $jarPath com.lineage.server.clientpackets.C_LoginToServer
javap -J-Dfile.encoding=UTF-8 -p -constants -classpath $jarPath com.lineage.server.serverpackets.S_Message_YN
javap -J-Dfile.encoding=UTF-8 -p -constants -classpath $jarPath com.lineage.echo.OpcodesClient com.lineage.server.serverpackets.OpcodesServer
```

## 差異及 #132 受影響列

| 類別 | 簽章／差異摘要 | #132 對照列與使用限制 |
|---|---|---|
| `C_LoginToServer` | 來源多出 `private static void traceLoginLife(ClientExecutor,L1PcInstance,String)`，JAR 的其他宣告方法相符 | C137 `LOGINTOSERVER`、`C_LoginToServer` 方法索引。C137 分派相符，新增追蹤 helper 的存在差異須與登入行為分開判讀。報告差異 12 的登入送出時機仍待驗證。 |
| `S_Message_YN` | 來源多出 `public static S_Message_YN confirmation(Offer,Ticket,int,String)` 與 `private S_Message_YN(Offer,Ticket,int,String,boolean)` | S219 `YES_NO`、同名方法索引的 `confirmation` @105 與私有建構子 @108。這兩個來源入口在 d287 缺席，使用 `requestId`／raw confirmation 的流程應另核部署契約。其餘 6 個建構子及其他宣告方法相符。 |
| `S_ClassicBadge` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicBadge` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicBagCombine` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicBagCombine` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicBoss` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicBoss` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicCollection` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicCollection` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicCraft` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicCraft` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicPass` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicPass` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicQuest` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicQuest` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicRanking` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicRanking` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_ClassicReward` | 來源啟用，JAR 缺少整個類別，全部來源簽章列於附錄 | S250 `PACKETBOX` 的 `S_ClassicReward` 類別列及同名欄位索引。此來源 serializer 的部署可用性須排除。 |
| `S_PacketNew` | 來源沒有啟用的類別宣告，JAR 無 class entry | 同名索引原已標記全停用，可沿用該狀態。 |

`Offer`、`Ticket` 的完整型別為 `com.lineage.server.model.npc.UiOperationContext` 的內部型別。上述來源多出項的完整簽章列於附錄。`S_ClassicInventoryFilter` 存在於 d287 且簽章相符，需逐類別區分 Classic 功能。

### #132 報告 19 條差異的對應

下表保留 #132 的優先序編號。共同的後續界線為業務規則、序列化完整條件與實際呼叫路徑仍待驗證。

| #132 差異列 | 本輪可採用的結論 |
|---|---|
| 1，傳送 recovery | 內部 `S_TeleportRecovery` 存在且 4 個方法簽章相符。N+8 包裝行為及客戶端相容性待驗證。 |
| 2，死亡／復活 | `C_Restart`、`S_Resurrection` 簽章及對應常數相符。參數指定 opcode 的 overload 保留動態條件。 |
| 3，完整技能 | `C_UseSkill` 簽章、C6 分派相符。各 skillId 的條件尾段待驗證。 |
| 4，S42 範圍技能 | `S_RangeSkill` 的兩個建構子均存在且簽章相符，S42 常數相符。逐目標傷害延伸條件待驗證。 |
| 5，S30 動作／箭矢 | `S_UseAttackSkill`、`S_UseArrowSkill` 簽章及 S30 常數相符。action／gfx 分支待驗證。 |
| 6，登入字元 | `C_AuthLogin` 簽章、C119 分派相符。密碼允許字元規則待驗證。 |
| 7，HP／MP profile | `S_OwnCharStatus`、`S_HPUpdate`、`S_MPUpdate` 簽章及 S8／S225／S33 常數相符。D/H 欄寬選擇待驗證。 |
| 8，物品尾段 | `C_ItemUSe` 簽章、C164 分派相符。use_type 尾段與前置檢查待驗證。 |
| 9，地面物品 | `C_DropItem`、`C_PickUpItem`、`S_NPCPack_Item` 簽章及對應常數相符。可交易條件與實際素材待驗證。 |
| 10，建角／刪角／切角 | `C_CreateChar`、`C_DeleteChar`、`C_NewCharSelect` 簽章與分派相符。完整狀態轉移待驗證。 |
| 11，多頻道聊天 | C 與 S 的 Chat 類別宣告簽章及 opcode 常數相符。各頻道參數與許可條件待驗證。 |
| 12，受擊抑制清單 | `S_HitReactionSuppressList` 簽章及局部常數 `CUSTOM_OPCODE=242`、`SUBOPCODE=2` 相符。`C_LoginToServer` 多出來源 helper，登入時機與 gfx 清單條件待驗證。 |
| 13，S22 聲音 | `S_Sound` 簽章及 S22 常數相符。repeat/soundId 語義與播放待驗證。 |
| 14，S106／S55 特效 | `S_EffectLocation`、`S_SkillSound` 簽章相符，含三個 int 的 S_SkillSound overload。完整布局與生命週期待驗證。 |
| 15，buff／packetbox | `S_SkillIconShield`、`S_PacketBoxIconAura`、`S_PacketBoxIcon1` 簽章相符。S250 全表包含 9 個 d287 缺席的 Classic 類別，新增子協議須逐類別核定。 |
| 16，S206 單參數 overload | `S_MapID(int)` 與 `S_MapID(int,boolean)` 均存在且簽章相符。尾段及實際使用點待驗證。 |
| 17，item status／裝備 | `S_ItemStatus`、`S_EquipmentWindow` 簽章相符。S242/sub1 的首個直接常數寫入為 242，status blob 語義待驗證。 |
| 18，緩速政策 | `C_ItemUSe`、`C_DropItem`、`C_PickUpItem`、`C_NPCTalk` 簽章與分派相符。緩速時的 handler 條件待驗證。 |
| 19，版本對應 | 已確認來源與 d287 有簽章及類別集合差異。確切 JAR 建置提交、全部本文等價性仍待證據。M6 應以本文件逐類別標記配合 #132 使用。 |

## 全部 opcode 常數

各列同時列出來源與 JAR 數值。`OpcodesClient._seed` 為 446195009，兩側相符，另列於此以區分封包 opcode。

### OpcodesClient

來源 `com/lineage/echo/OpcodesClient.java`，SHA-256 `3b9df4518cad4ab3021d7a6454f88fe4513bd715eb3eb461ffaa873a64fe8931`。class entry SHA-256 `6d1b160c0b381a56d7897c678a1f8bb5029ebb13417b052401092058aba5be5c`。

| 常數 | 來源十進位／hex | JAR 十進位／hex | 結論 |
|---|---|---|---|
| `C_OPCODE_TRADE` | 2 / 0x02 | 2 / 0x02 | 相符 |
| `C_OPCODE_BOOKMARKDELETE` | 3 / 0x03 | 3 / 0x03 | 相符 |
| `C_OPCODE_BUDDYLIST` | 4 / 0x04 | 4 / 0x04 | 相符 |
| `C_OPCODE_FIGHT` | 5 / 0x05 | 5 / 0x05 | 相符 |
| `C_OPCODE_USESKILL` | 6 / 0x06 | 6 / 0x06 | 相符 |
| `C_OPCODE_CHANGECHAR` | 7 / 0x07 | 7 / 0x07 | 相符 |
| `C_OPCODE_BOARD` | 10 / 0x0A | 10 / 0x0A | 相符 |
| `C_OPCODE_AMOUNT` | 11 / 0x0B | 11 / 0x0B | 相符 |
| `C_OPCODE_WAREHOUSELOCK` | 13 / 0x0D | 13 / 0x0D | 相符 |
| `C_OPCODE_CLIENTVERSION` | 14 / 0x0E | 14 / 0x0E | 相符 |
| `C_OPCODE_COMMONCLICK` | 16 / 0x10 | 16 / 0x10 | 相符 |
| `C_OPCODE_EMBLEMUPLOAD` | 18 / 0x12 | 18 / 0x12 | 相符 |
| `C_OPCODE_TAXRATE` | 19 / 0x13 | 19 / 0x13 | 相符 |
| `C_OPCODE_SELECTLIST` | 20 / 0x14 | 20 / 0x14 | 相符 |
| `C_OPCODE_BOARDNEXT` | 23 / 0x17 | 23 / 0x17 | 相符 |
| `C_OPCODE_DROPITEM` | 25 / 0x19 | 25 / 0x19 | 相符 |
| `C_OPCODE_LOGINTOSERVEROK` | 26 / 0x1A | 26 / 0x1A | 相符 |
| `C_OPCODE_MOVECHAR` | 29 / 0x1D | 29 / 0x1D | 相符 |
| `C_OPCODE_DUNGEONTELEPORT` | 30 / 0x1E | 30 / 0x1E | 相符 |
| `C_OPCODE_LEAVEPARTY` | 33 / 0x21 | 33 / 0x21 | 相符 |
| `C_OPCODE_NPCTALK` | 34 / 0x22 | 34 / 0x22 | 相符 |
| `C_OPCODE_防掛續存` | 35 / 0x23 | 35 / 0x23 | 相符 |
| `C_OPCODE_GMTELEPORT` | 36 / 0x24 | 36 / 0x24 | 相符 |
| `C_OPCODE_TRADEADDITEM` | 37 / 0x25 | 37 / 0x25 | 相符 |
| `C_OPCODE_SHOP` | 38 / 0x26 | 38 / 0x26 | 相符 |
| `C_OPCODE_SKILLBUYOK` | 39 / 0x27 | 39 / 0x27 | 相符 |
| `C_OPCODE_CHATGLOBAL` | 40 / 0x28 | 40 / 0x28 | 相符 |
| `C_OPCODE_DOOR` | 41 / 0x29 | 41 / 0x29 | 相符 |
| `C_OPCODE_PARTYLIST` | 43 / 0x2B | 43 / 0x2B | 相符 |
| `C_OPCODE_DRAWAL` | 44 / 0x2C | 44 / 0x2C | 相符 |
| `C_OPCODE_GIVEITEM` | 45 / 0x2D | 45 / 0x2D | 相符 |
| `C_OPCODE_PRIVATESHOPLIST` | 47 / 0x2F | 47 / 0x2F | 相符 |
| `C_OPCODE_PROPOSE` | 50 / 0x32 | 50 / 0x32 | 相符 |
| `C_OPCODE_CHECKPK` | 51 / 0x33 | 51 / 0x33 | 相符 |
| `C_OPCODE_TELEPORT` | 52 / 0x34 | 52 / 0x34 | 相符 |
| `C_OPCODE_DEPOSIT` | 56 / 0x38 | 56 / 0x38 | 相符 |
| `C_OPCODE_LEAVECLANE` | 61 / 0x3D | 61 / 0x3D | 相符 |
| `C_OPCODE_FISHCLICK` | 62 / 0x3E | 62 / 0x3E | 相符 |
| `C_OPCODE_RESTARTMENU` | 63 / 0x3F | 63 / 0x3F | 相符 |
| `C_OPCODE_RANK` | 63 / 0x3F | 63 / 0x3F | 相符 |
| `C_OPCODE_PLEDGE` | 68 / 0x44 | 68 / 0x44 | 相符 |
| `C_OPCODE_BANCLAN` | 69 / 0x45 | 69 / 0x45 | 相符 |
| `C_OPCODE_TRADEADDOK` | 71 / 0x47 | 71 / 0x47 | 相符 |
| `C_OPCODE_CLAN` | 72 / 0x48 | 72 / 0x48 | 相符 |
| `C_OPCODE_CLANMATCHING` | 76 / 0x4C | 76 / 0x4C | 相符 |
| `C_OPCODE_PLEDGECONTENT` | 78 / 0x4E | 78 / 0x4E | 相符 |
| `C_OPCODE_NEWCHAR` | 84 / 0x54 | 84 / 0x54 | 相符 |
| `C_OPCODE_TRADEADDCANCEL` | 86 / 0x56 | 86 / 0x56 | 相符 |
| `C_OPCODE_MAIL` | 87 / 0x57 | 87 / 0x57 | 相符 |
| `C_OPCODE_TITLE` | 90 / 0x5A | 90 / 0x5A | 相符 |
| `C_OPCODE_KEEPALIVE` | 95 / 0x5F | 95 / 0x5F | 相符 |
| `C_OPCODE_CHARRESET` | 98 / 0x62 | 98 / 0x62 | 相符 |
| `C_OPCODE_CHECK` | 99 / 0x63 | 99 / 0x63 | 相符 |
| `C_OPCODE_PETMENU` | 103 / 0x67 | 103 / 0x67 | 相符 |
| `C_OPCODE_USEPETITEM` | 104 / 0x68 | 104 / 0x68 | 相符 |
| `C_OPCODE_PICKUPITEM` | 112 / 0x70 | 112 / 0x70 | 相符 |
| `C_OPCODE_BOARDREAD` | 114 / 0x72 | 114 / 0x72 | 相符 |
| `C_OPCODE_FIX_WEAPON_LIST` | 118 / 0x76 | 118 / 0x76 | 相符 |
| `C_OPCODE_LOGINPACKET` | 119 / 0x77 | 119 / 0x77 | 相符 |
| `C_OPCODE_EXTCOMMAND` | 120 / 0x78 | 120 / 0x78 | 相符 |
| `C_OPCODE_ATTR` | 121 / 0x79 | 121 / 0x79 | 相符 |
| `C_OPCODE_QUITGAME` | 122 / 0x7A | 122 / 0x7A | 相符 |
| `C_OPCODE_ARROWATTACK` | 123 / 0x7B | 123 / 0x7B | 相符 |
| `C_OPCODE_NPCACTION` | 125 / 0x7D | 125 / 0x7D | 相符 |
| `C_OPCODE_CASTLESECURITY` | 128 / 0x80 | 128 / 0x80 | 相符 |
| `C_OPCODE_CLANATTENTION` | 129 / 0x81 | 129 / 0x81 | 相符 |
| `C_OPCODE_CHAT` | 136 / 0x88 | 136 / 0x88 | 相符 |
| `C_OPCODE_LOGINTOSERVER` | 137 / 0x89 | 137 / 0x89 | 相符 |
| `C_OPCODE_DELETEINVENTORYITEM` | 138 / 0x8A | 138 / 0x8A | 相符 |
| `C_OPCODE_BOARDWRITE` | 141 / 0x8D | 141 / 0x8D | 相符 |
| `C_OPCODE_SKILLBUY` | 145 / 0x91 | 145 / 0x91 | 相符 |
| `C_OPCODE_BOARDDELETE` | 153 / 0x99 | 153 / 0x99 | 相符 |
| `C_OPCODE_RESULT` | 161 / 0xA1 | 161 / 0xA1 | 相符 |
| `C_OPCODE_DELETECHAR` | 162 / 0xA2 | 162 / 0xA2 | 相符 |
| `C_OPCODE_USEITEM` | 164 / 0xA4 | 164 / 0xA4 | 相符 |
| `C_OPCODE_BOOKMARK` | 165 / 0xA5 | 165 / 0xA5 | 相符 |
| `C_OPCODE_EXCLUDE` | 171 / 0xAB | 171 / 0xAB | 相符 |
| `C_OPCODE_EXIT_GHOST` | 173 / 0xAD | 173 / 0xAD | 相符 |
| `C_OPCODE_RESTART` | 177 / 0xB1 | 177 / 0xB1 | 相符 |
| `C_OPCODE_CHATWHISPER` | 184 / 0xB8 | 184 / 0xB8 | 相符 |
| `C_OPCODE_CALL` | 185 / 0xB9 | 185 / 0xB9 | 相符 |
| `C_OPCODE_SKILLBUYOKITEM` | 191 / 0xBF | 191 / 0xBF | 相符 |
| `C_OPCODE_JOINCLAN` | 194 / 0xC2 | 194 / 0xC2 | 相符 |
| `C_OPCODE_CAHTPARTY` | 199 / 0xC7 | 199 / 0xC7 | 相符 |
| `C_OPCODE_DELBUDDY` | 202 / 0xCA | 202 / 0xCA | 相符 |
| `C_OPCODE_WHO` | 206 / 0xCE | 206 / 0xCE | 相符 |
| `C_OPCODE_ADDBUDDY` | 207 / 0xCF | 207 / 0xCF | 相符 |
| `C_OPCODE_WEB_WINDOW` | 208 / 0xD0 | 208 / 0xD0 | 相符 |
| `C_OPCODE_BEANFUNLOGINPACKET` | 210 / 0xD2 | 210 / 0xD2 | 相符 |
| `C_OPCODE_CUSTOM_RESERVED_211` | 211 / 0xD3 | 211 / 0xD3 | 相符 |
| `C_OPCODE_CUSTOM_RESERVED_212` | 212 / 0xD4 | 212 / 0xD4 | 相符 |
| `C_OPCODE_CUSTOM_RESERVED_213` | 213 / 0xD5 | 213 / 0xD5 | 相符 |
| `C_OPCODE_CUSTOM_RESERVED_214` | 214 / 0xD6 | 214 / 0xD6 | 相符 |
| `C_OPCODE_ENTERPORTAL` | 219 / 0xDB | 219 / 0xDB | 相符 |
| `C_OPCODE_CREATECLAN` | 222 / 0xDE | 222 / 0xDE | 相符 |
| `C_OPCODE_SELECTTARGET` | 223 / 0xDF | 223 / 0xDF | 相符 |
| `C_OPCODE_CHANGEHEADING` | 225 / 0xE1 | 225 / 0xE1 | 相符 |
| `C_OPCODE_WAR` | 227 / 0xE3 | 227 / 0xE3 | 相符 |
| `C_OPCODE_ATTACK` | 229 / 0xE5 | 229 / 0xE5 | 相符 |
| `C_OPCODE_CREATEPARTY` | 230 / 0xE6 | 230 / 0xE6 | 相符 |
| `C_OPCODE_SHIP` | 231 / 0xE7 | 231 / 0xE7 | 相符 |
| `C_OPCODE_CHARACTERCONFIG` | 244 / 0xF4 | 244 / 0xF4 | 相符 |
| `C_OPCODE_SKILLBUYITEM` | 245 / 0xF5 | 245 / 0xF5 | 相符 |
| `C_OPCODE_TELEPORTLOCK` | 246 / 0xF6 | 246 / 0xF6 | 相符 |
| `C_OPCODE_SMS` | 253 / 0xFD | 253 / 0xFD | 相符 |
| `C_OPCODE_SENDLOCATION` | 254 / 0xFE | 254 / 0xFE | 相符 |
| `C_OPCODE_BANPARTY` | 255 / 0xFF | 255 / 0xFF | 相符 |

### OpcodesServer

來源 `com/lineage/server/serverpackets/OpcodesServer.java`，SHA-256 `9044cb98cf8a0548a421fbb05e6cdcef26147b2a2b85a1ef896fde8a8016f152`。class entry SHA-256 `e38b4f92b2728287dd5ae1741dc7d0bbd4a2ca78bc6739f9b634609427a40b1d`。

| 常數 | 來源十進位／hex | JAR 十進位／hex | 結論 |
|---|---|---|---|
| `S_OPCODE_CLANMATCHING` | 0 / 0x00 | 0 / 0x00 | 相符 |
| `S_OPCODE_DEPOSIT` | 4 / 0x04 | 4 / 0x04 | 相符 |
| `S_OPCODE_INVLIST` | 5 / 0x05 | 5 / 0x05 | 相符 |
| `S_OPCODE_DETELECHAROK` | 6 / 0x06 | 6 / 0x06 | 相符 |
| `S_OPCODE_OWNCHARSTATUS` | 8 / 0x08 | 8 / 0x08 | 相符 |
| `S_OPCODE_MOVEOBJECT` | 10 / 0x0A | 10 / 0x0A | 相符 |
| `S_OPCODE_TRUETARGET` | 11 / 0x0B | 11 / 0x0B | 相符 |
| `S_OPCODE_SKILLBUYITEM` | 13 / 0x0D | 13 / 0x0D | 相符 |
| `S_OPCODE_ADDITEM` | 15 / 0x0F | 15 / 0x0F | 相符 |
| `S_OPCODE_SOUND` | 22 / 0x16 | 22 / 0x16 | 相符 |
| `S_OPCODE_ITEMSTATUS` | 24 / 0x18 | 24 / 0x18 | 相符 |
| `S_OPCODE_ITEMAMOUNT` | 24 / 0x18 | 24 / 0x18 | 相符 |
| `S_OPCODE_ATTACKPACKET` | 30 / 0x1E | 30 / 0x1E | 相符 |
| `S_OPCODE_MPUPDATE` | 33 / 0x21 | 33 / 0x21 | 相符 |
| `S_OPCODE_LAWFUL` | 34 / 0x22 | 34 / 0x22 | 相符 |
| `S_OPCODE_TRADEADDITEM` | 35 / 0x23 | 35 / 0x23 | 相符 |
| `S_OPCODE_ABILITY` | 36 / 0x24 | 36 / 0x24 | 相符 |
| `S_OPCODE_SPMR` | 37 / 0x25 | 37 / 0x25 | 相符 |
| `S_OPCODE_SHOWHTML` | 39 / 0x27 | 39 / 0x27 | 相符 |
| `S_OPCODE_LIGHT` | 40 / 0x28 | 40 / 0x28 | 相符 |
| `S_OPCODE_SKILLBUY` | 41 / 0x29 | 41 / 0x29 | 相符 |
| `S_OPCODE_RANGESKILLS` | 42 / 0x2A | 42 / 0x2A | 相符 |
| `S_OPCODE_CHANGENAME` | 46 / 0x2E | 46 / 0x2E | 相符 |
| `S_OPCODE_CURSEBLIND` | 47 / 0x2F | 47 / 0x2F | 相符 |
| `S_OPCODE_COMMONNEWS` | 48 / 0x30 | 48 / 0x30 | 相符 |
| `S_OPCODE_TRADE` | 52 / 0x34 | 52 / 0x34 | 相符 |
| `S_OPCODE_SKILLSOUNDGFX` | 55 / 0x37 | 55 / 0x37 | 相符 |
| `S_OPCODE_DELETEINVENTORYITEM` | 57 / 0x39 | 57 / 0x39 | 相符 |
| `S_OPCODE_PINKNAME` | 60 / 0x3C | 60 / 0x3C | 相符 |
| `S_OPCODE_CHARSYNACK` | 64 / 0x40 | 64 / 0x40 | 相符 |
| `S_OPCODE_CHARRESET` | 64 / 0x40 | 64 / 0x40 | 相符 |
| `S_OPCODE_SHOWSHOPSELLLIST` | 65 / 0x41 | 65 / 0x41 | 相符 |
| `S_OPCODE_SKILLBRAVE` | 67 / 0x43 | 67 / 0x43 | 相符 |
| `S_OPCODE_BOARD` | 68 / 0x44 | 68 / 0x44 | 相符 |
| `S_OPCODE_CASTLEMASTER` | 69 / 0x45 | 69 / 0x45 | 相符 |
| `S_OPCODE_SHOWSHOPBUYLIST` | 70 / 0x46 | 70 / 0x46 | 相符 |
| `S_OPCODE_SERVERMSG` | 71 / 0x47 | 71 / 0x47 | 相符 |
| `S_OPCODE_UPDATECLANID` | 72 / 0x48 | 72 / 0x48 | 相符 |
| `S_OPCODE_WHISPERCHAT` | 73 / 0x49 | 73 / 0x49 | 相符 |
| `S_OPCODE_POLY` | 76 / 0x4C | 76 / 0x4C | 相符 |
| `S_OPCODE_NORMALCHAT` | 81 / 0x51 | 81 / 0x51 | 相符 |
| `S_OPCODE_SELECTLIST` | 83 / 0x53 | 83 / 0x53 | 相符 |
| `S_OPCODE_WAR` | 84 / 0x54 | 84 / 0x54 | 相符 |
| `S_OPCODE_RESURRECTION` | 85 / 0x55 | 85 / 0x55 | 相符 |
| `S_OPCODE_CHARPACK` | 87 / 0x57 | 87 / 0x57 | 相符 |
| `S_OPCODE_DROPITEM` | 87 / 0x57 | 87 / 0x57 | 相符 |
| `S_OPCODE_BOOKMARKS` | 92 / 0x5C | 92 / 0x5C | 相符 |
| `S_OPCODE_CHARLIST` | 93 / 0x5D | 93 / 0x5D | 相符 |
| `S_OPCODE_NEWMASTER` | 72 / 0x48 | 72 / 0x48 | 相符 |
| `S_OPCODE_NEWCHARWRONG` | 98 / 0x62 | 98 / 0x62 | 相符 |
| `S_OPCODE_ITEMNAME` | 100 / 0x64 | 100 / 0x64 | 相符 |
| `S_OPCODE_LIQUOR` | 103 / 0x67 | 103 / 0x67 | 相符 |
| `S_OPCODE_REDMESSAGE` | 105 / 0x69 | 105 / 0x69 | 相符 |
| `S_OPCODE_EFFECTLOCATION` | 106 / 0x6A | 106 / 0x6A | 相符 |
| `S_OPCODE_TRADESTATUS` | 112 / 0x70 | 112 / 0x70 | 相符 |
| `S_OPCODE_EXP` | 113 / 0x71 | 113 / 0x71 | 相符 |
| `S_OPCODE_WEATHER` | 115 / 0x73 | 115 / 0x73 | 相符 |
| `S_OPCODE_PUTSOLDIER` | 117 / 0x75 | 117 / 0x75 | 相符 |
| `S_OPCODE_EMBLEM` | 118 / 0x76 | 118 / 0x76 | 相符 |
| `S_OPCODE_CHARVISUALUPDATE` | 119 / 0x77 | 119 / 0x77 | 相符 |
| `S_OPCODE_REMOVE_OBJECT` | 120 / 0x78 | 120 / 0x78 | 相符 |
| `S_OPCODE_CHANGEHEADING` | 122 / 0x7A | 122 / 0x7A | 相符 |
| `S_OPCODE_GAMETIME` | 123 / 0x7B | 123 / 0x7B | 相符 |
| `S_OPCODE_BLESSOFEVA` | 126 / 0x7E | 126 / 0x7E | 相符 |
| `S_OPCODE_NEWCHARPACK` | 127 / 0x7F | 127 / 0x7F | 相符 |
| `S_OPCODE_INPUTAMOUNT` | 136 / 0x88 | 136 / 0x88 | 相符 |
| `S_OPCODE_SERVERVERSION` | 139 / 0x8B | 139 / 0x8B | 相符 |
| `S_OPCODE_PRIVATESHOPLIST` | 140 / 0x8C | 140 / 0x8C | 相符 |
| `S_OPCODE_DRAWAL` | 141 / 0x8D | 141 / 0x8D | 相符 |
| `S_OPCODE_BOARDREAD` | 148 / 0x94 | 148 / 0x94 | 相符 |
| `S_OPCODE_TELEPORT` | 149 / 0x95 | 149 / 0x95 | 相符 |
| `S_OPCODE_INITPACKET` | 150 / 0x96 | 150 / 0x96 | 相符 |
| `S_OPCODE_OWNCHARSTATUS2` | 155 / 0x9B | 155 / 0x9B | 相符 |
| `S_OPCODE_HOUSELIST` | 156 / 0x9C | 156 / 0x9C | 相符 |
| `S_OPCODE_DOACTIONGFX` | 158 / 0x9E | 158 / 0x9E | 相符 |
| `S_OPCODE_DELSKILL` | 160 / 0xA0 | 160 / 0xA0 | 相符 |
| `S_OPCODE_NPCSHOUT` | 161 / 0xA1 | 161 / 0xA1 | 相符 |
| `S_OPCODE_ADDSKILL` | 164 / 0xA4 | 164 / 0xA4 | 相符 |
| `S_OPCODE_POISON` | 165 / 0xA5 | 165 / 0xA5 | 相符 |
| `S_OPCODE_STRUP` | 166 / 0xA6 | 166 / 0xA6 | 相符 |
| `S_OPCODE_INVIS` | 171 / 0xAB | 171 / 0xAB | 相符 |
| `S_OPCODE_OWNCHARATTRDEF` | 174 / 0xAE | 174 / 0xAE | 相符 |
| `S_OPCODE_SHOWRETRIEVELIST` | 176 / 0xB0 | 176 / 0xB0 | 相符 |
| `S_OPCODE_CHARAMOUNT` | 178 / 0xB2 | 178 / 0xB2 | 相符 |
| `S_OPCODE_CHARTITLE` | 183 / 0xB7 | 183 / 0xB7 | 相符 |
| `S_OPCODE_TAXRATE` | 185 / 0xB9 | 185 / 0xB9 | 相符 |
| `S_OPCODE_MAIL` | 186 / 0xBA | 186 / 0xBA | 相符 |
| `S_OPCODE_HOUSEMAP` | 187 / 0xBB | 187 / 0xBB | 相符 |
| `S_OPCODE_DEXUP` | 188 / 0xBC | 188 / 0xBC | 相符 |
| `S_OPCODE_CLANATTENTION` | 200 / 0xC8 | 200 / 0xC8 | 相符 |
| `S_OPCODE_PARALYSIS` | 202 / 0xCA | 202 / 0xCA | 相符 |
| `S_OPCODE_MAPID` | 206 / 0xCE | 206 / 0xCE | 相符 |
| `S_OPCODE_UNDERWATER` | 206 / 0xCE | 206 / 0xCE | 相符 |
| `S_OPCODE_ATTRIBUTE` | 209 / 0xD1 | 209 / 0xD1 | 相符 |
| `S_OPCODE_SKILLICONSHIELD` | 216 / 0xD8 | 216 / 0xD8 | 相符 |
| `S_OPCODE_YES_NO` | 219 / 0xDB | 219 / 0xDB | 相符 |
| `S_OPCODE_LOGINTOGAME` | 223 / 0xDF | 223 / 0xDF | 相符 |
| `S_OPCODE_HPUPDATE` | 225 / 0xE1 | 225 / 0xE1 | 相符 |
| `S_OPCODE_DISCONNECT` | 227 / 0xE3 | 227 / 0xE3 | 相符 |
| `S_OPCODE_SPOLY` | 230 / 0xE6 | 230 / 0xE6 | 相符 |
| `S_OPCODE_LOGINRESULT` | 233 / 0xE9 | 233 / 0xE9 | 相符 |
| `S_OPCODE_SELECTTARGET` | 236 / 0xEC | 236 / 0xEC | 相符 |
| `S_OPCODE_HPMETER` | 237 / 0xED | 237 / 0xED | 相符 |
| `S_OPCODE_ITEMCOLOR` | 240 / 0xF0 | 240 / 0xF0 | 相符 |
| `S_OPCODE_MOVELOCK` | 241 / 0xF1 | 241 / 0xF1 | 相符 |
| `S_OPCODE_GLOBALCHAT` | 243 / 0xF3 | 243 / 0xF3 | 相符 |
| `S_OPCODE_IDENTIFYDESC` | 245 / 0xF5 | 245 / 0xF5 | 相符 |
| `S_OPCODE_PACKETBOX` | 250 / 0xFA | 250 / 0xFA | 相符 |
| `S_OPCODE_ACTIVESPELLS` | 250 / 0xFA | 250 / 0xFA | 相符 |
| `S_OPCODE_SKILLICONGFX` | 250 / 0xFA | 250 / 0xFA | 相符 |
| `S_OPCODE_UNKNOWN2` | 250 / 0xFA | 250 / 0xFA | 相符 |
| `S_OPCODE_SKILLHASTE` | 255 / 0xFF | 255 / 0xFF | 相符 |

S242 額外局部常數在 `S_HitReactionSuppressList` 為 `CUSTOM_OPCODE=242`、`SUBOPCODE=2`，兩側相符。`S_ItemStatus.writeLongStatus` 的第一個直接 writeC 常數亦為 242，兩側相符。

## 346 個封包來源覆蓋表

opcode 欄沿用 #132 的常數／類別關聯，C 分派另經 JAR 核對。S 類別的參數化 overload、委派與停用條件仍須搭配原索引閱讀。每列連至完整簽章附錄。

| 類別 | #132 opcode 關聯 | 核對結果 |
|---|---|---|
| [C_AddBookmark](#c-addbookmark) | C165 | 簽章相符 |
| [C_AddBuddy](#c-addbuddy) | C207 | 簽章相符 |
| [C_Amount](#c-amount) | C11 | 簽章相符 |
| [C_Attack](#c-attack) | C229 | 簽章相符 |
| [C_AttackBow](#c-attackbow) | C123 | 簽章相符 |
| [C_Attr](#c-attr) | C121 | 簽章相符 |
| [C_AuthLogin](#c-authlogin) | C119 | 簽章相符 |
| [C_AutoLogin](#c-autologin) | C210 | 簽章相符 |
| [C_BanClan](#c-banclan) | C69 | 簽章相符 |
| [C_BanParty](#c-banparty) | C255 | 簽章相符 |
| [C_Board](#c-board) | C10 | 簽章相符 |
| [C_BoardBack](#c-boardback) | C23 | 簽章相符 |
| [C_BoardDelete](#c-boarddelete) | C153 | 簽章相符 |
| [C_BoardRead](#c-boardread) | C114 | 簽章相符 |
| [C_BoardWrite](#c-boardwrite) | C141 | 簽章相符 |
| [C_Buddy](#c-buddy) | C4 | 簽章相符 |
| [C_CallPlayer](#c-callplayer) | C185 | 簽章相符 |
| [C_ChangeHeading](#c-changeheading) | C225 | 簽章相符 |
| [C_ChangeWarTime](#c-changewartime) | 無具名 opcode 分派 | 簽章相符 |
| [C_CharcterConfig](#c-charcterconfig) | C244 | 簽章相符 |
| [C_CharReset](#c-charreset) | C98 | 簽章相符 |
| [C_Chat](#c-chat) | C136 | 簽章相符 |
| [C_ChatGlobal](#c-chatglobal) | C40 | 簽章相符 |
| [C_ChatParty](#c-chatparty) | C199 | 簽章相符 |
| [C_ChatWhisper](#c-chatwhisper) | C184 | 簽章相符 |
| [C_Check](#c-check) | C99 | 簽章相符 |
| [C_CheckPK](#c-checkpk) | C51 | 簽章相符 |
| [C_Clan](#c-clan) | C72 | 簽章相符 |
| [C_ClanAttention](#c-clanattention) | C129 | 簽章相符 |
| [C_CnItem](#c-cnitem) | 無具名 opcode 分派 | 簽章相符 |
| [C_CommonClick](#c-commonclick) | C16 | 簽章相符 |
| [C_CreateChar](#c-createchar) | C84 | 簽章相符 |
| [C_CreateClan](#c-createclan) | C222 | 簽章相符 |
| [C_CreateParty](#c-createparty) | C230 | 簽章相符 |
| [C_DelBuddy](#c-delbuddy) | C202 | 簽章相符 |
| [C_DeleteBookmark](#c-deletebookmark) | C3 | 簽章相符 |
| [C_DeleteChar](#c-deletechar) | C162 | 簽章相符 |
| [C_DeleteInventoryItem](#c-deleteinventoryitem) | C138 | 簽章相符 |
| [C_Deposit](#c-deposit) | C56 | 簽章相符 |
| [C_Disconnect](#c-disconnect) | C122 | 簽章相符 |
| [C_Door](#c-door) | C41 | 簽章相符 |
| [C_Drawal](#c-drawal) | C44 | 簽章相符 |
| [C_DropItem](#c-dropitem) | C25 | 簽章相符 |
| [C_Emblem](#c-emblem) | C18 | 簽章相符 |
| [C_EnterPortal](#c-enterportal) | C219 | 簽章相符 |
| [C_Exclude](#c-exclude) | C171 | 簽章相符 |
| [C_ExitGhost](#c-exitghost) | C173 | 簽章相符 |
| [C_ExtraCommand](#c-extracommand) | C120 | 簽章相符 |
| [C_Fight](#c-fight) | C5 | 簽章相符 |
| [C_FishClick](#c-fishclick) | C62 | 簽章相符 |
| [C_FixWeaponList](#c-fixweaponlist) | C118 | 簽章相符 |
| [C_GiveItem](#c-giveitem) | C45 | 簽章相符 |
| [C_HireSoldier](#c-hiresoldier) | 無具名 opcode 分派 | 簽章相符 |
| [C_ItemUSe](#c-itemuse) | C164 | 簽章相符 |
| [C_JoinClan](#c-joinclan) | C194 | 簽章相符 |
| [C_KeepALIVE](#c-keepalive) | C95 | 簽章相符 |
| [C_LeaveClan](#c-leaveclan) | C61 | 簽章相符 |
| [C_LeaveParty](#c-leaveparty) | C33 | 簽章相符 |
| [C_LoginToServer](#c-logintoserver) | C137 | 簽章差異 |
| [C_LoginToServerOK](#c-logintoserverok) | C26 | 簽章相符 |
| [C_Mail](#c-mail) | C87 | 簽章相符 |
| [C_MoveChar](#c-movechar) | C29 | 簽章相符 |
| [C_NewAccess](#c-newaccess) | 無具名 opcode 分派 | 簽章相符 |
| [C_NewCharSelect](#c-newcharselect) | C7 | 簽章相符 |
| [C_NPCAction](#c-npcaction) | C125 | 簽章相符 |
| [C_NPCTalk](#c-npctalk) | C34 | 簽章相符 |
| [C_Party](#c-party) | C43 | 簽章相符 |
| [C_Password](#c-password) | 無具名 opcode 分派 | 簽章相符 |
| [C_PetMenu](#c-petmenu) | C103 | 簽章相符 |
| [C_PickUpItem](#c-pickupitem) | C112 | 簽章相符 |
| [C_Pledge](#c-pledge) | C68 | 簽章相符 |
| [C_PledgeContent](#c-pledgecontent) | C78 | 簽章相符 |
| [C_PledgeRecommendation](#c-pledgerecommendation) | C76 | 簽章相符 |
| [C_Propose](#c-propose) | C50 | 簽章相符 |
| [C_Rank](#c-rank) | C63 | 簽章相符 |
| [C_Restart](#c-restart) | C177 | 簽章相符 |
| [C_Result](#c-result) | C161 | 簽章相符 |
| [C_ReturnToLogin](#c-returntologin) | 無具名 opcode 分派 | 簽章相符 |
| [C_SelectList](#c-selectlist) | C20 | 簽章相符 |
| [C_SelectTarget](#c-selecttarget) | C223 | 簽章相符 |
| [C_ServerVersion](#c-serverversion) | C14 | 簽章相符 |
| [C_Ship](#c-ship) | C231 | 簽章相符 |
| [C_Shop](#c-shop) | C38 | 簽章相符 |
| [C_ShopList](#c-shoplist) | C47 | 簽章相符 |
| [C_SkillBuy](#c-skillbuy) | C145 | 簽章相符 |
| [C_SkillBuyItem](#c-skillbuyitem) | C245 | 簽章相符 |
| [C_SkillBuyItemOK](#c-skillbuyitemok) | C191 | 簽章相符 |
| [C_SkillBuyOK](#c-skillbuyok) | C39 | 簽章相符 |
| [C_TaxRate](#c-taxrate) | C19 | 簽章相符 |
| [C_Teleport](#c-teleport) | C52 | 簽章相符 |
| [C_Title](#c-title) | C90 | 簽章相符 |
| [C_Trade](#c-trade) | C2 | 簽章相符 |
| [C_TradeAddItem](#c-tradeadditem) | C37 | 簽章相符 |
| [C_TradeCancel](#c-tradecancel) | C86 | 簽章相符 |
| [C_TradeOK](#c-tradeok) | C71 | 簽章相符 |
| [C_Unkonwn](#c-unkonwn) | 無具名 opcode 分派 | 簽章相符 |
| [C_UnLock](#c-unlock) | C246 | 簽章相符 |
| [C_UsePetItem](#c-usepetitem) | C104 | 簽章相符 |
| [C_UseSkill](#c-useskill) | C6 | 簽章相符 |
| [C_War](#c-war) | C227 | 簽章相符 |
| [C_WebWindow](#c-webwindow) | C208 | 簽章相符 |
| [C_Who](#c-who) | C206 | 簽章相符 |
| [C_Windows](#c-windows) | C254 | 簽章相符 |
| [S_Ability](#s-ability) | S36 | 簽章相符 |
| [S_AddItem](#s-additem) | S15 | 簽章相符 |
| [S_AddSkill](#s-addskill) | S164 | 簽章相符 |
| [S_ApplyAuction](#s-applyauction) | S136 | 簽章相符 |
| [S_AttackPacketNpc](#s-attackpacketnpc) | S30 | 簽章相符 |
| [S_AttackPacketPc](#s-attackpacketpc) | S30 | 簽章相符 |
| [S_AuctionBoard](#s-auctionboard) | S156 | 簽章相符 |
| [S_AuctionBoardRead](#s-auctionboardread) | S39 | 簽章相符 |
| [S_AuctionShopRetrieve](#s-auctionshopretrieve) | S176 | 簽章相符 |
| [S_AuctionShopSellList](#s-auctionshopselllist) | S70 | 簽章相符 |
| [S_BlueMessage](#s-bluemessage) | S105 | 簽章相符 |
| [S_Board](#s-board) | S68 | 簽章相符 |
| [S_BoardRead](#s-boardread) | S148 | 簽章相符 |
| [S_Bonusstats](#s-bonusstats) | S39 | 簽章相符 |
| [S_BookmarkList](#s-bookmarklist) | S64 | 簽章相符 |
| [S_Bookmarks](#s-bookmarks) | S92 | 簽章相符 |
| [S_BoxChoice](#s-boxchoice) | S70 | 簽章相符 |
| [S_BoxMessage](#s-boxmessage) | S161 | 簽章相符 |
| [S_BoxPack](#s-boxpack) | S87 | 簽章相符 |
| [S_BroadcastPos](#s-broadcastpos) | S250 | 簽章相符 |
| [S_Buddy](#s-buddy) | S39 | 簽章相符 |
| [S_CastleMaster](#s-castlemaster) | S69 | 簽章相符 |
| [S_ChangeHeading](#s-changeheading) | S122 | 簽章相符 |
| [S_ChangeName](#s-changename) | S46 | 簽章相符 |
| [S_ChangeShape](#s-changeshape) | S76 | 簽章相符 |
| [S_CharAmount](#s-charamount) | S178 | 簽章相符 |
| [S_CharCreateStatus](#s-charcreatestatus) | S98 | 簽章相符 |
| [S_CharPacks](#s-charpacks) | S93 | 簽章相符 |
| [S_CharReset](#s-charreset) | S64 | 簽章相符 |
| [S_CharResetInfo](#s-charresetinfo) | S64 | 簽章相符 |
| [S_CharTitle](#s-chartitle) | S183 | 簽章相符 |
| [S_CharVisualUpdate](#s-charvisualupdate) | S119 | 簽章相符 |
| [S_Chat](#s-chat) | S81 | 簽章相符 |
| [S_ChatClan](#s-chatclan) | S243 | 簽章相符 |
| [S_ChatClanUnion](#s-chatclanunion) | S243 | 簽章相符 |
| [S_ChatGlobal](#s-chatglobal) | S243 | 簽章相符 |
| [S_ChatOut](#s-chatout) | 無固定 opcode 宣告 | 簽章相符 |
| [S_ChatParty](#s-chatparty) | S243 | 簽章相符 |
| [S_ChatParty2](#s-chatparty2) | S81 | 簽章相符 |
| [S_ChatShouting](#s-chatshouting) | S81 | 簽章相符 |
| [S_ChatTransaction](#s-chattransaction) | S243 | 簽章相符 |
| [S_ChatWhisperFrom](#s-chatwhisperfrom) | S73 | 簽章相符 |
| [S_ChatWhisperTo](#s-chatwhisperto) | S243 | 簽章相符 |
| [S_ClanAttention](#s-clanattention) | S200 | 簽章相符 |
| [S_ClanUpdate](#s-clanupdate) | S72 | 簽章相符 |
| [S_ClassicBadge](#s-classicbadge) | S250 | JAR 缺少類別 |
| [S_ClassicBagCombine](#s-classicbagcombine) | S250 | JAR 缺少類別 |
| [S_ClassicBoss](#s-classicboss) | S250 | JAR 缺少類別 |
| [S_ClassicCollection](#s-classiccollection) | S250 | JAR 缺少類別 |
| [S_ClassicCraft](#s-classiccraft) | S250 | JAR 缺少類別 |
| [S_ClassicInventoryFilter](#s-classicinventoryfilter) | S250 | 簽章相符 |
| [S_ClassicPass](#s-classicpass) | S250 | JAR 缺少類別 |
| [S_ClassicQuest](#s-classicquest) | S250 | JAR 缺少類別 |
| [S_ClassicRanking](#s-classicranking) | S250 | JAR 缺少類別 |
| [S_ClassicReward](#s-classicreward) | S250 | JAR 缺少類別 |
| [S_CloseList](#s-closelist) | S39 | 簽章相符 |
| [S_CommonInfo](#s-commoninfo) | S48 | 簽章相符 |
| [S_CommonNews](#s-commonnews) | S48 | 簽章相符 |
| [S_CurseBlind](#s-curseblind) | S47 | 簽章相符 |
| [S_DeleteCharOK](#s-deletecharok) | S6 | 簽章相符 |
| [S_DeleteInventoryItem](#s-deleteinventoryitem) | S57 | 簽章相符 |
| [S_DelSkill](#s-delskill) | S160 | 簽章相符 |
| [S_Deposit](#s-deposit) | S4 | 簽章相符 |
| [S_Dexup](#s-dexup) | S188 | 簽章相符 |
| [S_Disconnect](#s-disconnect) | S227 | 簽章相符 |
| [S_DoActionGFX](#s-doactiongfx) | S158 | 簽章相符 |
| [S_DoActionShop](#s-doactionshop) | S158 | 簽章相符 |
| [S_Door](#s-door) | S209 | 簽章相符 |
| [S_DoorPack](#s-doorpack) | S87 | 簽章相符 |
| [S_Drawal](#s-drawal) | S141 | 簽章相符 |
| [S_EffectLocation](#s-effectlocation) | S106 | 簽章相符 |
| [S_Emblem](#s-emblem) | S118 | 簽章相符 |
| [S_EnterGame](#s-entergame) | S223 | 簽章相符 |
| [S_EquipmentWindow](#s-equipmentwindow) | S64 | 簽章相符 |
| [S_Exp](#s-exp) | S113 | 簽章相符 |
| [S_Fishing](#s-fishing) | S158 | 簽章相符 |
| [S_FixWeaponList](#s-fixweaponlist) | S83 | 簽章相符 |
| [S_FollowerPack](#s-followerpack) | S87 | 簽章相符 |
| [S_GameTime](#s-gametime) | S123 | 簽章相符 |
| [S_GlobalEffect](#s-globaleffect) | S250 | 簽章相符 |
| [S_GMHtml](#s-gmhtml) | S39 | 簽章相符 |
| [S_HelpMessage](#s-helpmessage) | S161 | 簽章相符 |
| [S_HireSoldier](#s-hiresoldier) | 無固定 opcode 宣告 | 簽章相符 |
| [S_HitReactionSuppressList](#s-hitreactionsuppresslist) | S242 | 簽章相符 |
| [S_HouseMap](#s-housemap) | S187 | 簽章相符 |
| [S_HowManyMake](#s-howmanymake) | S136 | 簽章相符 |
| [S_HPMeter](#s-hpmeter) | S237 | 簽章相符 |
| [S_HPUpdate](#s-hpupdate) | S225 | 簽章相符 |
| [S_IconConfig](#s-iconconfig) | S250 | 簽章相符 |
| [S_Icons](#s-icons) | S250 | 簽章相符 |
| [S_IdentifyDesc](#s-identifydesc) | S245 | 簽章相符 |
| [S_InitialAbilityGrowth](#s-initialabilitygrowth) | S64 | 簽章相符 |
| [S_Invis](#s-invis) | S171 | 簽章相符 |
| [S_InvList](#s-invlist) | S5 | 簽章相符 |
| [S_ItemAmount](#s-itemamount) | S24 | 簽章相符 |
| [S_ItemBoard](#s-itemboard) | S250 | 簽章相符 |
| [S_ItemColor](#s-itemcolor) | S240 | 簽章相符 |
| [S_ItemCount](#s-itemcount) | S136 | 簽章相符 |
| [S_ItemError](#s-itemerror) | 無固定 opcode 宣告 | 簽章相符 |
| [S_ItemName](#s-itemname) | S100 | 簽章相符 |
| [S_ItemStatus](#s-itemstatus) | S24、S242 | 簽章相符 |
| [S_Karma](#s-karma) | S250 | 簽章相符 |
| [S_KillMessage](#s-killmessage) | S161 | 簽章相符 |
| [S_Lawful](#s-lawful) | S34 | 簽章相符 |
| [S_Letter](#s-letter) | 無固定 opcode 宣告 | 簽章相符 |
| [S_Light](#s-light) | S40 | 簽章相符 |
| [S_Liquor](#s-liquor) | S103 | 簽章相符 |
| [S_Lock](#s-lock) | S241 | 簽章相符 |
| [S_LoginResult](#s-loginresult) | S233 | 簽章相符 |
| [S_MagicEquipment](#s-magicequipment) | S250 | 簽章相符 |
| [S_Mail](#s-mail) | S186 | 簽章相符 |
| [S_MapID](#s-mapid) | S206 | 簽章相符 |
| [S_Message_YN](#s-message-yn) | S219 | 簽章差異 |
| [S_MoveCharPacket](#s-movecharpacket) | S10 | 簽章相符 |
| [S_MoveNpcPacket](#s-movenpcpacket) | S10 | 簽章相符 |
| [S_MPUpdate](#s-mpupdate) | S33 | 簽章相符 |
| [S_NewCharPacket](#s-newcharpacket) | S127 | 簽章相符 |
| [S_NewMaster](#s-newmaster) | S72 | 簽章相符 |
| [S_NoSell](#s-nosell) | S39 | 簽章相符 |
| [S_NpcChat](#s-npcchat) | S161 | 簽章相符 |
| [S_NpcChatGlobal](#s-npcchatglobal) | S161 | 簽章相符 |
| [S_NpcChatShouting](#s-npcchatshouting) | S161 | 簽章相符 |
| [S_NPCPack](#s-npcpack) | S87 | 簽章相符 |
| [S_NPCPack_D](#s-npcpack-d) | S87 | 簽章相符 |
| [S_NPCPack_De](#s-npcpack-de) | S87 | 簽章相符 |
| [S_NPCPack_Doll](#s-npcpack-doll) | S87 | 簽章相符 |
| [S_NPCPack_Eff](#s-npcpack-eff) | S87 | 簽章相符 |
| [S_NPCPack_F](#s-npcpack-f) | S87 | 簽章相符 |
| [S_NPCPack_Ill](#s-npcpack-ill) | S87 | 簽章相符 |
| [S_NPCPack_Item](#s-npcpack-item) | S87 | 簽章相符 |
| [S_NPCPack_M](#s-npcpack-m) | S87 | 簽章相符 |
| [S_NPCPack_Pet](#s-npcpack-pet) | S87 | 簽章相符 |
| [S_NPCPack_Signboard](#s-npcpack-signboard) | S87 | 簽章相符 |
| [S_NPCPack_Summon](#s-npcpack-summon) | S87 | 簽章相符 |
| [S_NPCTalkActionTPUrl](#s-npctalkactiontpurl) | S39 | 簽章相符 |
| [S_NPCTalkReturn](#s-npctalkreturn) | S39 | 簽章相符 |
| [S_OtherCharPacks](#s-othercharpacks) | S87 | 簽章相符 |
| [S_OwnCharAttrDef](#s-owncharattrdef) | S174 | 簽章相符 |
| [S_OwnCharPack](#s-owncharpack) | S87 | 簽章相符 |
| [S_OwnCharStatus](#s-owncharstatus) | S8 | 簽章相符 |
| [S_OwnCharStatus2](#s-owncharstatus2) | S155 | 簽章相符 |
| [S_PacketBox](#s-packetbox) | S250 | 簽章相符 |
| [S_PacketBoxActiveSpells](#s-packetboxactivespells) | S250 | 簽章相符 |
| [S_PacketBoxConfig](#s-packetboxconfig) | S250 | 簽章相符 |
| [S_PacketBoxCooking](#s-packetboxcooking) | S250 | 簽章相符 |
| [S_PacketBoxDk](#s-packetboxdk) | S250 | 簽章相符 |
| [S_PacketBoxExp](#s-packetboxexp) | S250 | 簽章相符 |
| [S_PacketBoxGame](#s-packetboxgame) | S250 | 簽章相符 |
| [S_PacketBoxGm](#s-packetboxgm) | S250 | 簽章相符 |
| [S_PacketBoxGree](#s-packetboxgree) | S250 | 簽章相符 |
| [S_PacketBoxHpMsg](#s-packetboxhpmsg) | S250 | 簽章相符 |
| [S_PacketBoxIcon1](#s-packetboxicon1) | S250 | 簽章相符 |
| [S_PacketBoxIcon2](#s-packetboxicon2) | S250 | 簽章相符 |
| [S_PacketBoxIconAura](#s-packetboxiconaura) | S250 | 簽章相符 |
| [S_PacketBoxItemLv](#s-packetboxitemlv) | S250 | 簽章相符 |
| [S_PacketBoxLoc](#s-packetboxloc) | S250 | 簽章相符 |
| [S_PacketBoxMapTimer](#s-packetboxmaptimer) | S250 | 簽章相符 |
| [S_PacketBoxParty](#s-packetboxparty) | S250 | 簽章相符 |
| [S_PacketBoxPledgeRecommend](#s-packetboxpledgerecommend) | S0 | 簽章相符 |
| [S_PacketBoxProtection](#s-packetboxprotection) | S250 | 簽章相符 |
| [S_PacketBoxSelect](#s-packetboxselect) | S250 | 簽章相符 |
| [S_PacketBoxTest](#s-packetboxtest) | S250 | 簽章相符 |
| [S_PacketBoxThirdSpeed](#s-packetboxthirdspeed) | S250 | 簽章相符 |
| [S_PacketBoxWar](#s-packetboxwar) | S250 | 簽章相符 |
| [S_PacketBoxWaterLife](#s-packetboxwaterlife) | S250 | 簽章相符 |
| [S_PacketBoxWindShackle](#s-packetboxwindshackle) | S250 | 簽章相符 |
| [S_PacketBoxWisdomPotion](#s-packetboxwisdompotion) | S250 | 簽章相符 |
| [S_PacketNew](#s-packetnew) | 無固定 opcode 宣告 | 來源全停用／JAR 無類別 |
| [S_Paralysis](#s-paralysis) | S202 | 簽章相符 |
| [S_Party](#s-party) | S39 | 簽章相符 |
| [S_PcPositionCorrection](#s-pcpositioncorrection) | S87（繼承） | 簽章相符 |
| [S_PcPositionMapReset](#s-pcpositionmapreset) | S206 | 簽章相符 |
| [S_PetCtrlMenu](#s-petctrlmenu) | S64 | 簽章相符 |
| [S_PetEquipment](#s-petequipment) | S250 | 簽章相符 |
| [S_PetInventory](#s-petinventory) | S176 | 簽章相符 |
| [S_PetList](#s-petlist) | S83 | 簽章相符 |
| [S_PetMenuPacket](#s-petmenupacket) | S39 | 簽章相符 |
| [S_PinkName](#s-pinkname) | S60 | 簽章相符 |
| [S_Pledge](#s-pledge) | S250、S39 | 簽章相符 |
| [S_PledgeWarehouseHistory](#s-pledgewarehousehistory) | S250 | 簽章相符 |
| [S_Poison](#s-poison) | S165 | 簽章相符 |
| [S_PowerItemList](#s-poweritemlist) | S176 | 簽章相符 |
| [S_PrivateShop](#s-privateshop) | S140 | 簽章相符 |
| [S_RangeSkill](#s-rangeskill) | S42 | 簽章相符 |
| [S_RedMessage](#s-redmessage) | 無固定 opcode 宣告 | 簽章相符 |
| [S_RemoveObject](#s-removeobject) | S120 | 簽章相符 |
| [S_Resurrection](#s-resurrection) | S85 | 簽章相符 |
| [S_RetrieveChaList](#s-retrievechalist) | S176 | 簽章相符 |
| [S_RetrieveDevilKingList](#s-retrievedevilkinglist) | S176 | 簽章相符 |
| [S_RetrieveElfList](#s-retrieveelflist) | S176 | 簽章相符 |
| [S_RetrieveList](#s-retrievelist) | S176 | 簽章相符 |
| [S_RetrievePledgeList](#s-retrievepledgelist) | S176 | 簽章相符 |
| [S_RetrievePowerItem](#s-retrievepoweritem) | S176 | 簽章相符 |
| [S_RetrieveShiftingList](#s-retrieveshiftinglist) | S176 | 簽章相符 |
| [S_SelectTarget](#s-selecttarget) | S236 | 簽章相符 |
| [S_SellHouse](#s-sellhouse) | S136 | 簽章相符 |
| [S_ServerMessage](#s-servermessage) | S161、S71 | 簽章相符 |
| [S_ServerVersion](#s-serverversion) | S139 | 簽章相符 |
| [S_ShopBuyList](#s-shopbuylist) | S65 | 簽章相符 |
| [S_ShopBuyListAll](#s-shopbuylistall) | S65 | 簽章相符 |
| [S_ShopBuyListGam](#s-shopbuylistgam) | S65 | 簽章相符 |
| [S_ShopBuyListLotto](#s-shopbuylistlotto) | S65 | 簽章相符 |
| [S_ShopSellList](#s-shopselllist) | S70 | 簽章相符 |
| [S_ShopSellListCn](#s-shopselllistcn) | S70 | 簽章相符 |
| [S_ShopSellListCnX](#s-shopselllistcnx) | S70 | 簽章相符 |
| [S_ShopSellListGam](#s-shopselllistgam) | S70 | 簽章相符 |
| [S_ShopSellListLotto](#s-shopselllistlotto) | S70 | 簽章相符 |
| [S_ShopSellListTmp](#s-shopselllisttmp) | S70 | 簽章相符 |
| [S_ShopSellListX1](#s-shopselllistx1) | S70 | 簽章相符 |
| [S_ShowDrop](#s-showdrop) | S250 | 簽章相符 |
| [S_ShowPolyList](#s-showpolylist) | S39 | 簽章相符 |
| [S_ShowSummonList](#s-showsummonlist) | S39 | 簽章相符 |
| [S_SkillBrave](#s-skillbrave) | S67 | 簽章相符 |
| [S_SkillBuy](#s-skillbuy) | S41 | 簽章相符 |
| [S_SkillBuyCN](#s-skillbuycn) | S39、S41 | 簽章相符 |
| [S_SkillBuyItem](#s-skillbuyitem) | S13 | 簽章相符 |
| [S_SkillBuyItemCN](#s-skillbuyitemcn) | S13 | 簽章相符 |
| [S_SkillHaste](#s-skillhaste) | S255 | 簽章相符 |
| [S_SkillIconBlessOfEva](#s-skilliconblessofeva) | S126 | 簽章相符 |
| [S_SkillIconShield](#s-skilliconshield) | S216 | 簽章相符 |
| [S_SkillSound](#s-skillsound) | S55 | 簽章相符 |
| [S_Sound](#s-sound) | S22 | 簽章相符 |
| [S_SPMR](#s-spmr) | S37 | 簽章相符 |
| [S_Strup](#s-strup) | S166 | 簽章相符 |
| [S_SystemMessage](#s-systemmessage) | S161、S243 | 簽章相符 |
| [S_TaxRate](#s-taxrate) | S185 | 簽章相符 |
| [S_Teleport](#s-teleport) | S149 | 簽章相符 |
| [S_Teleport2](#s-teleport2) | S149 | 簽章相符 |
| [S_ToGmMessage](#s-togmmessage) | S161 | 簽章相符 |
| [S_Trade](#s-trade) | S52 | 簽章相符 |
| [S_TradeAddItem](#s-tradeadditem) | S35 | 簽章相符 |
| [S_TradeStatus](#s-tradestatus) | S112 | 簽章相符 |
| [S_Trap](#s-trap) | S87 | 簽章相符 |
| [S_TrueTarget](#s-truetarget) | S11 | 簽章相符 |
| [S_Unknown_B](#s-unknown-b) | S64 | 簽章相符 |
| [S_UseArrowSkill](#s-usearrowskill) | S30 | 簽章相符 |
| [S_UseAttackSkill](#s-useattackskill) | S30 | 簽章相符 |
| [S_War](#s-war) | S84 | 簽章相符 |
| [S_WarTime](#s-wartime) | 由參數決定 | 簽章相符 |
| [S_WeaponRange](#s-weaponrange) | S250 | 簽章相符 |
| [S_Weather](#s-weather) | S115 | 簽章相符 |
| [S_WhoAmount](#s-whoamount) | S71 | 簽章相符 |
| [S_WhoCharinfo](#s-whocharinfo) | S71 | 簽章相符 |

## 完整方法簽章與逐檔身分

以下採完整型別名，將 JVM 內部類別 `$` 顯示為 Java 的 `.`。每個類別列出全部 JAR 宣告方法與來源新增方法，另列編譯器產生的方法。簽章相符類別的「共同宣告方法」同時代表來源與 JAR 兩側。原始碼位置以 Java 來源路徑識別，class entry 由完整類別名轉成 `/` 路徑並加 `.class`。

首個直接 writeC 核對只列方法及整數結論，`動態` 表示引數需依參數或運算求值，`缺席` 表示 JAR 沒有對應方法。省略未宣告直接 writeC 的方法，其委派或繼承入口仍在完整簽章中。

<a id="c-addbookmark"></a>

### C_AddBookmark

來源 `com/lineage/server/clientpackets/C_AddBookmark.java`。

- 來源 SHA-256 `011bfc8e81c50067004becd19907e480b84a3afd91004f5de9fbe0d4e9f392c3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_AddBookmark](server-protocol.md#c-addbookmark)。
- class entry SHA-256 `add039ef12f1728bfec56c2b9238e0d3914c0ceea14c91b6b2c541a55f1d405b`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_AddBookmark()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
```

<a id="c-addbuddy"></a>

### C_AddBuddy

來源 `com/lineage/server/clientpackets/C_AddBuddy.java`。

- 來源 SHA-256 `7ddae0a303a2114d5299dcfb06c127dcd53b8472374e67699c3f821c12573484`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_AddBuddy](server-protocol.md#c-addbuddy)。
- class entry SHA-256 `779d9f80140b9cf28a7464b13b859cf0ba19688302ca362e8553ea988d28130b`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_AddBuddy()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
```

<a id="c-amount"></a>

### C_Amount

來源 `com/lineage/server/clientpackets/C_Amount.java`。

- 來源 SHA-256 `fc600dacece39a424a21bece8634aed3a223e7cd583a2084474cf8976e5510b3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Amount](server-protocol.md#c-amount)。
- class entry SHA-256 `80f2bee7b304ebd1e789195f1be1ddd27dcd8b032efb1138253c213a712b602f`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Amount()
private static void executeMonitored(com.lineage.server.utils.WorldInteractionGuard.Interaction, java.lang.Runnable)
private static void runHouseBidParticipants(com.lineage.server.model.Instance.L1PcInstance, int, java.util.function.BiConsumer<com.lineage.server.templates.L1AuctionBoardTmp, com.lineage.server.model.Instance.L1PcInstance>)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
private static void quarantineBidInventory(com.lineage.server.model.L1PcInventory, java.lang.String, java.lang.Throwable)
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.10(com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int)
private static void lambda.start.9(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.Instance.L1NpcInstance, int)
private static void lambda.start.8(java.lang.String, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, int)
private static void lambda.start.7(com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.npc.UiOperationContext.Ticket, int, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, int)
private static void lambda.start.6(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.npc.UiOperationContext.Ticket, java.lang.String, com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.start.5(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.npc.UiOperationContext.Ticket, java.lang.String, com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.templates.L1AuctionBoardTmp, com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.start.4(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1PcInventory, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1PcInventory, int, com.lineage.server.templates.L1AuctionBoardTmp, int, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.npc.UiOperationContext.Ticket, java.lang.String, com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.start.3(com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.templates.L1NpcMaking, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String, com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.runHouseBidParticipants.2(int, com.lineage.server.templates.L1AuctionBoardTmp, int, com.lineage.server.model.L1Object, boolean[], java.util.function.BiConsumer, com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.runHouseBidParticipants.1(com.lineage.server.templates.L1AuctionBoardTmp, int, com.lineage.server.model.L1Object, boolean[], java.util.function.BiConsumer, com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.executeMonitored.0(com.lineage.server.utils.WorldInteractionGuard.Interaction, java.lang.Runnable)
```

<a id="c-attack"></a>

### C_Attack

來源 `com/lineage/server/clientpackets/C_Attack.java`。

- 來源 SHA-256 `3726df0441bcdf88e62a24e63bb81d36b78106f7d21131d571979d8cc3946324`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Attack](server-protocol.md#c-attack)。
- class entry SHA-256 `4d56f3383955f26d0e581893ce479de5b8aabab4db7adccd2c493049961f1568`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Attack()
public void start(byte[], com.lineage.echo.ClientExecutor)
private void performAttack(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, int, int)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, int, int)
```

<a id="c-attackbow"></a>

### C_AttackBow

來源 `com/lineage/server/clientpackets/C_AttackBow.java`。

- 來源 SHA-256 `cc2087e4c62a3d6c5d48ba9e8851a0cbfa317219e2225fa5e98932c3b47d6613`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_AttackBow](server-protocol.md#c-attackbow)。
- class entry SHA-256 `1bafa1e5ce31deac723fd503faf27a19838768bd42a7e42ffbd388917180e755`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_AttackBow()
public void start(byte[], com.lineage.echo.ClientExecutor)
private void performAttack(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, int, int)
private void performEmptyAttack(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void arrowAction(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance, int, int, int)
private void nullAction(com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, int, int)
```

<a id="c-attr"></a>

### C_Attr

來源 `com/lineage/server/clientpackets/C_Attr.java`。

- 來源 SHA-256 `793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Attr](server-protocol.md#c-attr)。
- class entry SHA-256 `155774c9d94d29ffdd253ab12ce4bfea7b541f064cf85c7f519688f7e39ea2ac`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Attr()
private static boolean isCurrentMigrationRequest(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Permit, long)
private static void executeMonitored(java.lang.Runnable)
private static void executeMonitored(java.lang.Runnable, com.lineage.server.model.L1Clan.JoinCapacity, java.lang.String)
private static void renameHouse(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void scheduleMonitored(java.lang.Runnable, long)
private static void scheduleDelayedHandoff(java.lang.Runnable, long, int)
private static void dispatchDelayedWork(java.lang.Runnable, int)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void changeClan(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.ClanJoinRequest, com.lineage.server.model.npc.UiOperationContext.Ticket)
private static void mergeConfirmed(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.ClanJoinRequest, com.lineage.server.model.npc.UiOperationContext.Ticket) throws java.lang.Exception
private static void renamePet(com.lineage.server.model.Instance.L1PetInstance, java.lang.String)
private void callClan(com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.renamePet.15(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String, int, java.lang.String, com.lineage.server.model.Instance.L1PetInstance)
private static void lambda.mergeConfirmed.14(java.lang.Runnable, com.lineage.server.model.L1Clan.NpcDissolutionAttempt, com.lineage.server.model.L1Clan.NpcDissolutionAttempt, boolean[], com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, boolean[])
private static void lambda.mergeConfirmed.13(com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.ClanJoinRequest, com.lineage.server.model.Instance.L1PcInstance, boolean[])
private static void lambda.changeClan.12(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.ClanJoinRequest, com.lineage.server.model.npc.UiOperationContext.Ticket)
private static void lambda.start.11(com.lineage.server.model.Instance.L1PcInstance, int, int)
private static void lambda.start.10(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Permit, long, com.lineage.server.model.Instance.L1ItemInstance, int, java.lang.String)
private void lambda.start.9(com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.start.8(int)
private void lambda.start.7(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.scheduleDelayedHandoff.6(java.lang.Runnable, int)
private static void lambda.scheduleMonitored.5(java.lang.Runnable)
private static void lambda.renameHouse.4(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.npc.UiOperationContext.Ticket, java.lang.String)
private static void lambda.renameHouse.3(com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, java.lang.String)
private static java.lang.Object lambda.renameHouse.2(com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, java.lang.String)
private static void lambda.executeMonitored.1(java.lang.Runnable, com.lineage.server.model.L1Clan.JoinCapacity.Use)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-authlogin"></a>

### C_AuthLogin

來源 `com/lineage/server/clientpackets/C_AuthLogin.java`。

- 來源 SHA-256 `1902f2244a893eaa8e9a434ce4293a3cbdfa460a5bef7e8210c8696d428bfaa1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_AuthLogin](server-protocol.md#c-authlogin)。
- class entry SHA-256 `ea59837424753d59754024d8cc4cbc457eb006775043f60927261ff4b8f3d481`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_AuthLogin()
private static boolean executeMonitored(java.lang.Runnable)
private static boolean isClientReady(com.lineage.echo.ClientExecutor)
private static boolean sendPacket(com.lineage.echo.ClientExecutor, com.lineage.server.serverpackets.ServerBasePacket)
public void start(byte[], com.lineage.echo.ClientExecutor)
public void checkLogin(com.lineage.echo.ClientExecutor, java.lang.String, java.lang.String, boolean)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.checkLogin.1(com.lineage.echo.SessionPhaseGate.Lease, com.lineage.echo.ClientExecutor, java.lang.String, java.lang.String, java.lang.StringBuilder, java.lang.StringBuilder, boolean)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-autologin"></a>

### C_AutoLogin

來源 `com/lineage/server/clientpackets/C_AutoLogin.java`。

- 來源 SHA-256 `b9eb654a9c72a887eba477a1f14feb6fd5f8d1f09625a82e85a0d0c07275c9a5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_AutoLogin](server-protocol.md#c-autologin)。
- class entry SHA-256 `24d5efd57ebde91b870a6c0c43d7384afffdd1262ddc33aee4fd2f2f594958b2`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_AutoLogin()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-banclan"></a>

### C_BanClan

來源 `com/lineage/server/clientpackets/C_BanClan.java`。

- 來源 SHA-256 `aad634b1ae485cc7a85c756d7dca162beb7a5c4fdcc52c031cee0af314829ce9`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_BanClan](server-protocol.md#c-banclan)。
- class entry SHA-256 `b5d8592e687c4d8b2f3aade898fd9a528410148cf5810e95e51c1461ea4fe1f5`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_BanClan()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, com.lineage.server.model.L1Clan)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-banparty"></a>

### C_BanParty

來源 `com/lineage/server/clientpackets/C_BanParty.java`。

- 來源 SHA-256 `c149cad2cc052d5876eee112b88a2db7a560658b836310963badbcb57a1ad900`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_BanParty](server-protocol.md#c-banparty)。
- class entry SHA-256 `fd37a2fada0da4c672eae8f7cf83d9f9c48496fba3a71c718c5c6c3b0ca20c83`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_BanParty()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-board"></a>

### C_Board

來源 `com/lineage/server/clientpackets/C_Board.java`。

- 來源 SHA-256 `20e3437630822a9b866d9e1a56865046477f4110451fb0c23d870defe5da8da5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Board](server-protocol.md#c-board)。
- class entry SHA-256 `6094411cf5579e3447e2dd3bc0335115ccb409dc1c92d26bc9630aa314c123f2`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Board()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.data.executor.NpcExecutor)
```

<a id="c-boardback"></a>

### C_BoardBack

來源 `com/lineage/server/clientpackets/C_BoardBack.java`。

- 來源 SHA-256 `96cebb5ea143b18b64852449fab085d1174b8484787db62c1f5842b8848d1d8b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_BoardBack](server-protocol.md#c-boardback)。
- class entry SHA-256 `6c4160cdc16fe8596369ab8e83324024b69d543492e54d0e41f4dbdae3d58cd9`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_BoardBack()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, int, com.lineage.data.executor.NpcExecutor)
```

<a id="c-boarddelete"></a>

### C_BoardDelete

來源 `com/lineage/server/clientpackets/C_BoardDelete.java`。

- 來源 SHA-256 `411192fc2319fe4f8207de579123ae2a32ee688531815bc6f7d711bcac17be68`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_BoardDelete](server-protocol.md#c-boarddelete)。
- class entry SHA-256 `c59dac4fe97f9d9179265ed7e41ad397ff20f70ee7004087f60d9effd95096cb`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_BoardDelete()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, int, com.lineage.data.executor.NpcExecutor)
```

<a id="c-boardread"></a>

### C_BoardRead

來源 `com/lineage/server/clientpackets/C_BoardRead.java`。

- 來源 SHA-256 `8e99855f4bcd9abf356df2e4628b2a1c165c7b40e5271d2c277cb6c79d244f73`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_BoardRead](server-protocol.md#c-boardread)。
- class entry SHA-256 `3c7a87a322986a0438aa614dcb98eb669afd4642d26ba2c0237465de601accce`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_BoardRead()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, int, com.lineage.data.executor.NpcExecutor)
```

<a id="c-boardwrite"></a>

### C_BoardWrite

來源 `com/lineage/server/clientpackets/C_BoardWrite.java`。

- 來源 SHA-256 `d50fbae5dae1b86e75e163d8504a4ef5d3f5f582fe6fd2f80936b56fc4e82453`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_BoardWrite](server-protocol.md#c-boardwrite)。
- class entry SHA-256 `748428c431aa45a5001df903a88f8c7343868684bc5ffe8c77b4b3a31dbc2aa5`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_BoardWrite()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, java.lang.String, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.data.executor.NpcExecutor)
```

<a id="c-buddy"></a>

### C_Buddy

來源 `com/lineage/server/clientpackets/C_Buddy.java`。

- 來源 SHA-256 `8b8b6e9533f4d7064d03d1bf0e144d26d9cadd75fcf1186c0023fa41e21fd519`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Buddy](server-protocol.md#c-buddy)。
- class entry SHA-256 `640a627379d8397a86074a71c76ae32540f595a551baf47a422732f2e200faff`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Buddy()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-callplayer"></a>

### C_CallPlayer

來源 `com/lineage/server/clientpackets/C_CallPlayer.java`。

- 來源 SHA-256 `17f431846994d68d83ba269ec362f11698e4bd50084aec71d757cefc2a0e5338`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CallPlayer](server-protocol.md#c-callplayer)。
- class entry SHA-256 `6551be91c4658865a1f7af1d6b55de8e91543f44366987b2eaf4e048cc8ca773`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CallPlayer()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void stopSkill(com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.3(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.start.2(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, java.lang.String, com.lineage.server.model.Instance.L1PcInstance)
private void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-changeheading"></a>

### C_ChangeHeading

來源 `com/lineage/server/clientpackets/C_ChangeHeading.java`。

- 來源 SHA-256 `3ab45e30f01eb9e32f86c4981e64ef76426ff7601c1da8f04bcc4fb79b7691fb`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ChangeHeading](server-protocol.md#c-changeheading)。
- class entry SHA-256 `90f5870eed301bed60727c83646086f97a2aa35795ec25f9bdd0cdde1617d5e0`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ChangeHeading()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-changewartime"></a>

### C_ChangeWarTime

來源 `com/lineage/server/clientpackets/C_ChangeWarTime.java`。

- 來源 SHA-256 `3be0c6d5fec2fc727b26713ebef1256f9f616881c12f5eb275379e44503f9b85`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ChangeWarTime](server-protocol.md#c-changewartime)。
- class entry SHA-256 `f77dd6561f53be1ec773a8f72295af480b63b2c100bf1aba45acb27aab7202d9`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ChangeWarTime()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-charcterconfig"></a>

### C_CharcterConfig

來源 `com/lineage/server/clientpackets/C_CharcterConfig.java`。

- 來源 SHA-256 `4d935d45193e99d3598da1067dd2422c27194800d748bff0441c4d73cb01e555`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CharcterConfig](server-protocol.md#c-charcterconfig)。
- class entry SHA-256 `88f4b9b01f58f8c903b66883f037e0e5f791d7870124f51cf4fc1fb654de6add`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CharcterConfig()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, int, byte[])
```

<a id="c-charreset"></a>

### C_CharReset

來源 `com/lineage/server/clientpackets/C_CharReset.java`。

- 來源 SHA-256 `3bba21f5677f0625e63e7c407e828fdb721cf97b0353c83ff152ef1f5df5abdf`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CharReset](server-protocol.md#c-charreset)。
- class entry SHA-256 `29416a9e19f73de522aaecaa70d5e464420498be8fb3c41efc19e37fb0344393`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CharReset()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void saveNewCharStatus(com.lineage.server.model.Instance.L1PcInstance)
static void initCharStatus(com.lineage.server.model.Instance.L1PcInstance, int, int, int, int, int, int, int, int)
private void setLevelUp(com.lineage.server.model.Instance.L1PcInstance, int)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-chat"></a>

### C_Chat

來源 `com/lineage/server/clientpackets/C_Chat.java`。

- 來源 SHA-256 `72257d44684061e922f79dcd8f7b5ed4604ccaffd875506d2457a00a1873f4b2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Chat](server-protocol.md#c-chat)。
- class entry SHA-256 `b1f2f7620f0bbd578dfc2059273fee5b6ae44b1d53f8aad3f2ff7d2913915f9f`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Chat()
private static void executeMonitored(java.lang.Runnable)
private static boolean executePasswordPersistence(java.lang.Runnable)
private static boolean isSameConnectedPlayer(com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void re_repass(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void re_title(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void chatType_14(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void chatType_13(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void chatType_11(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void chatType_4(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void chatType_2(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void chatType_0(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void doppelGenerally(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void doppelShouting(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.chatType_0.9(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.chatType_2.8(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.chatType_2.7(java.lang.String, com.lineage.server.serverpackets.S_ChatShouting, com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.chatType_4.6(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.chatType_11.5(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.chatType_13.4(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.chatType_14.3(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.re_repass.2(java.lang.String, com.lineage.server.model.Instance.L1PcInstance.PasswordChangeClaim, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor, com.lineage.server.templates.L1Account)
private static void lambda.executePasswordPersistence.1(java.lang.Runnable)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-chatglobal"></a>

### C_ChatGlobal

來源 `com/lineage/server/clientpackets/C_ChatGlobal.java`。

- 來源 SHA-256 `5c673ed8aeaa1c5ec82b8edeb10ae670d8b99321c1042313da607b8bcd8d8708`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ChatGlobal](server-protocol.md#c-chatglobal)。
- class entry SHA-256 `609bc449972ae574093220d842f588e070134e8456493b6ea76f3951517c1b3a`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ChatGlobal()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void chatType_12(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void chatType_3(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.chatType_3.3(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.chatType_3.2(com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.chatType_12.1(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-chatparty"></a>

### C_ChatParty

來源 `com/lineage/server/clientpackets/C_ChatParty.java`。

- 來源 SHA-256 `30d29eb14b5f67ce4c4ba9ff3c02eba729b847b95f85102afa00ad53fd0db608`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ChatParty](server-protocol.md#c-chatparty)。
- class entry SHA-256 `95ba859b4eed12d26ea57fb895f0f71a22f79b9391f91d728b397fb901cfe257`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ChatParty()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-chatwhisper"></a>

### C_ChatWhisper

來源 `com/lineage/server/clientpackets/C_ChatWhisper.java`。

- 來源 SHA-256 `44d67c3dccbdbe6842ec3a59b6c7c3dcb512e98c8182d79ae563a31d5b87f8ff`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ChatWhisper](server-protocol.md#c-chatwhisper)。
- class entry SHA-256 `4e5ccd27268920d2d53af4bfd7f4c612078014b4cb0c1b8a124ab1b8c8c2d17c`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ChatWhisper()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public static com.lineage.server.model.Instance.L1DeInstance getDe(java.lang.String)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-check"></a>

### C_Check

來源 `com/lineage/server/clientpackets/C_Check.java`。

- 來源 SHA-256 `eeca0bd74d5bbcf6a50ac1b33855640984eca0ee626e0f237162fe871f938d34`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Check](server-protocol.md#c-check)。
- class entry SHA-256 `002717166139146b312e269660ae1e3ad9cce877aa1518ba8f7c7eadd41ba871`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Check()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-checkpk"></a>

### C_CheckPK

來源 `com/lineage/server/clientpackets/C_CheckPK.java`。

- 來源 SHA-256 `11ffa8ad46337a48bc3c0d0edaf97f18f9baf200aa79c483d172f986a37371be`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CheckPK](server-protocol.md#c-checkpk)。
- class entry SHA-256 `aed268472e5e37e7fe95f925b4924a200371a75eac1f9a0ad2e36c264bc95af4`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CheckPK()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-clan"></a>

### C_Clan

來源 `com/lineage/server/clientpackets/C_Clan.java`。

- 來源 SHA-256 `56d98e8cbdc9ff2a72e518050f3ec21699a0be0bf3348f0f75b8590258244d04`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Clan](server-protocol.md#c-clan)。
- class entry SHA-256 `a0e4b032e9d747a0de5db603d1eade82a1ba307b8c01a76a3d5b91f9cf1593d2`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Clan()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-clanattention"></a>

### C_ClanAttention

來源 `com/lineage/server/clientpackets/C_ClanAttention.java`。

- 來源 SHA-256 `29bc590d3fb56172d47938b4101fd1226934c750f13c87118220abb72d331f0e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ClanAttention](server-protocol.md#c-clanattention)。
- class entry SHA-256 `69da9a20920dc6fe0a5dae67976cfb94ad5ffb1bc4a7dacb3b8509e4d10a1eea`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ClanAttention()
public java.lang.String getType()
public void start(byte[], com.lineage.echo.ClientExecutor)
private boolean isClanPrince(com.lineage.server.model.Instance.L1PcInstance)
private void handleTypeList(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, byte[], int)
private void handleTypeRemove(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, byte[], int)
private boolean deleteByListIndex(com.lineage.server.model.L1Clan, byte, byte)
private void deleteSelected(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan)
private java.lang.String resolveAttentionName(com.lineage.server.model.L1Clan, byte, byte)
private void handleAddAttention(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, java.lang.String)
```

<a id="c-cnitem"></a>

### C_CnItem

來源 `com/lineage/server/clientpackets/C_CnItem.java`。

- 來源 SHA-256 `a7bc9c023841ca234b19c2b7aec5a81356fe5a7ae87dc0aa3530ab686ad5fc6b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CnItem](server-protocol.md#c-cnitem)。
- class entry SHA-256 `87063be7272f0ce554177608a77b53608d8b9276785513350d61f71c5c59259f`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CnItem()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-commonclick"></a>

### C_CommonClick

來源 `com/lineage/server/clientpackets/C_CommonClick.java`。

- 來源 SHA-256 `931376b9fe56edc6d222aa00fc2a4aa843a091ff7d224d868f5b3739530f259f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CommonClick](server-protocol.md#c-commonclick)。
- class entry SHA-256 `42d0d979d67e47b2f557d26862ae20768717252ec46d7224d9b9f145b1a0219f`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CommonClick()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public static void refreshAndSendCharacterList(com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.echo.SessionPhaseGate.Permit, com.lineage.echo.ClientExecutor)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-createchar"></a>

### C_CreateChar

來源 `com/lineage/server/clientpackets/C_CreateChar.java`。

- 來源 SHA-256 `400c2c4d48b63d0c73fb07a736dde7a5c5b3a03159ed063b76aff3cbe09eae72`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CreateChar](server-protocol.md#c-createchar)。
- class entry SHA-256 `d8946dc91e889dd4dc0502e21ac69cbb23725849f1ed4e75661b15812554b9e1`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CreateChar()
private static void executeMonitored(java.lang.Runnable)
private void skipCreateCharStats()
public void start(byte[], com.lineage.echo.ClientExecutor)
private static boolean current(com.lineage.echo.ClientExecutor, com.lineage.server.templates.L1Account, java.lang.String, com.lineage.echo.SessionPhaseGate.Permit)
private static void initializeNewChar(com.lineage.server.templates.L1Account, java.lang.String, com.lineage.server.model.Instance.L1PcInstance)
public static boolean isInvalidName(java.lang.String)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.2(com.lineage.echo.ClientExecutor, com.lineage.server.templates.L1Account, java.lang.String, com.lineage.echo.SessionPhaseGate.Permit, java.lang.String, int, com.lineage.server.model.Instance.L1PcInstance)
private static java.lang.Boolean lambda.start.1(com.lineage.echo.ClientExecutor, com.lineage.server.templates.L1Account, java.lang.String, com.lineage.echo.SessionPhaseGate.Permit, com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-createclan"></a>

### C_CreateClan

來源 `com/lineage/server/clientpackets/C_CreateClan.java`。

- 來源 SHA-256 `511c19e9ac3175c7d6554b99844704f09045f0cba000ea199651ef059d8556d6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CreateClan](server-protocol.md#c-createclan)。
- class entry SHA-256 `91743a8691dcdcb8993db73bc60332bbdf931a118e5c16eebfb3efe416e0f50e`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CreateClan()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
private static boolean current(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Permit, long)
private static boolean nameExists(java.lang.String)
private static boolean refund(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1PcInventory.InventoryMutationSnapshot)
private static void uncertain(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.2(com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Permit, long, java.lang.String)
private static void lambda.start.1(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Permit, long, java.lang.String)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-createparty"></a>

### C_CreateParty

來源 `com/lineage/server/clientpackets/C_CreateParty.java`。

- 來源 SHA-256 `cd0f739dafc8eae990eb59eee5b7706c4b387b9112edd44914dd9f89c27e9b9c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_CreateParty](server-protocol.md#c-createparty)。
- class entry SHA-256 `f691f4f27ff7da0bdda170535871aa51d7490d4be6d16d52ec82569bf758e408`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_CreateParty()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-delbuddy"></a>

### C_DelBuddy

來源 `com/lineage/server/clientpackets/C_DelBuddy.java`。

- 來源 SHA-256 `a841bcc49ef05dd2c287afa55f78f2dca3abb742864c67ae35dd34902c0421ea`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_DelBuddy](server-protocol.md#c-delbuddy)。
- class entry SHA-256 `e7f88ce676a8d963b8cd914d8ff85bfcb533a658ff6d108353cac17459c7d658`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_DelBuddy()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
```

<a id="c-deletebookmark"></a>

### C_DeleteBookmark

來源 `com/lineage/server/clientpackets/C_DeleteBookmark.java`。

- 來源 SHA-256 `1d322e0ee50f28c9a1765d5dbfbc24ebff8bc0aff441d629acff9f2759b6af19`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_DeleteBookmark](server-protocol.md#c-deletebookmark)。
- class entry SHA-256 `77e3f225d85467ae277f96e62fcda83c6f15fcdaf3989c46977218576fb33f17`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_DeleteBookmark()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
```

<a id="c-deletechar"></a>

### C_DeleteChar

來源 `com/lineage/server/clientpackets/C_DeleteChar.java`。

- 來源 SHA-256 `a3024d7b58c34ef6f3e1c197b4824710d56ffe0cf387ec4220e937249de1a628`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_DeleteChar](server-protocol.md#c-deletechar)。
- class entry SHA-256 `0221b9c224c250919e979454a9c11ecae616b3abe090fc30afc21b257fc206dd`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_DeleteChar()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.echo.ClientExecutor, java.lang.String, java.lang.String)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-deleteinventoryitem"></a>

### C_DeleteInventoryItem

來源 `com/lineage/server/clientpackets/C_DeleteInventoryItem.java`。

- 來源 SHA-256 `807ec633ed1a198c44fdef61f29896c2f70592fbf192404256fbcba9ea748d7f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_DeleteInventoryItem](server-protocol.md#c-deleteinventoryitem)。
- class entry SHA-256 `e5675c14b8da14351b3219a7c00eabaca935fe34c5c3c52d2d1125f0c39c307e`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_DeleteInventoryItem()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-deposit"></a>

### C_Deposit

來源 `com/lineage/server/clientpackets/C_Deposit.java`。

- 來源 SHA-256 `5afaf8c9b51ca531a7a02e4985bc3e1857770c8253b948d4ef132a08a88d9b29`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Deposit](server-protocol.md#c-deposit)。
- class entry SHA-256 `0ff4320b5f296997fca63be3c642a5faaa3b36db701a524ce6066cf22a803f4a`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Deposit()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.3(com.lineage.server.model.npc.CastleDepositAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.L1PcInventory, long)
private static void lambda.start.2(com.lineage.server.model.npc.CastleDepositAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.L1PcInventory, long)
private static void lambda.start.1(com.lineage.server.model.npc.CastleDepositAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.L1PcInventory, com.lineage.server.templates.L1Castle, long)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-disconnect"></a>

### C_Disconnect

來源 `com/lineage/server/clientpackets/C_Disconnect.java`。

- 來源 SHA-256 `da4267eea102482b68152e0498da49fd30badeff1b3807909b3f5a085ca6e69b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Disconnect](server-protocol.md#c-disconnect)。
- class entry SHA-256 `2fb2ba499d096144485b74dbdc87e347ac4cb40a589f1ec775e4550699abb725`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Disconnect()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-door"></a>

### C_Door

來源 `com/lineage/server/clientpackets/C_Door.java`。

- 來源 SHA-256 `34b3fc7271b49be0f5968acf3320c539fed750ccd23b8025de2aa27849f734a5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Door](server-protocol.md#c-door)。
- class entry SHA-256 `b728313e12337feb5c554392e9c49a2f189542fe7793cbd5974a22a2f4fa068f`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Door()
private static void scheduleMonitored(java.lang.Runnable, long)
private static void scheduleClockHandoff(java.lang.Runnable, long, int)
private static void dispatchWorldTask(java.lang.Runnable, int)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void interact(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object)
private boolean isExistKeeper(com.lineage.server.model.Instance.L1PcInstance, int)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
private void touchHauntedHouseFire(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private void lambda.start.2(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object)
private static void lambda.scheduleClockHandoff.1(java.lang.Runnable, int)
private static void lambda.scheduleMonitored.0(java.lang.Runnable)
```

<a id="c-drawal"></a>

### C_Drawal

來源 `com/lineage/server/clientpackets/C_Drawal.java`。

- 來源 SHA-256 `0357a9683398a1bd82f18cce9160f0ccb3d3b902835a143e10d5628cb95bab68`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Drawal](server-protocol.md#c-drawal)。
- class entry SHA-256 `69feb9f81185ba060fa2fc7f7606c05f5b7cb6b9b6cad5c14a167357e98d5362`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Drawal()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.2(com.lineage.server.model.npc.CastleTreasuryAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, long)
private static void lambda.start.1(com.lineage.server.model.npc.CastleTreasuryAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.templates.L1Castle, long)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-dropitem"></a>

### C_DropItem

來源 `com/lineage/server/clientpackets/C_DropItem.java`。

- 來源 SHA-256 `74e111eca656c740a59d0433c05698e734f699fd9199c006283484de1f553752`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_DropItem](server-protocol.md#c-dropitem)。
- class entry SHA-256 `5b097f52ad2f3b7f6836ceb4124208adc8d9f4c8ddf172c658456f5629225319`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_DropItem()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static com.lineage.server.model.Instance.L1ItemInstance lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance, int, int, int)
```

<a id="c-emblem"></a>

### C_Emblem

來源 `com/lineage/server/clientpackets/C_Emblem.java`。

- 來源 SHA-256 `4ea6cd5b26c72c4ac40d9dfb88a772352c0f9bbdc00c1af87930fa59e8c84415`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Emblem](server-protocol.md#c-emblem)。
- class entry SHA-256 `be2d82c8632d76066b5f36f10cf0233b101720cacfd511280748a1296a4b7cf1`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Emblem()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, int, byte[])
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-enterportal"></a>

### C_EnterPortal

來源 `com/lineage/server/clientpackets/C_EnterPortal.java`。

- 來源 SHA-256 `5ce4b59f857d121e54062fa6f4fa8b33ad878f2ee375dd953ca1bbd10feb1277`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_EnterPortal](server-protocol.md#c-enterportal)。
- class entry SHA-256 `cafdcaef4f13946f7e7c3220add2e3b550021d00f2a7d3e276b2d8ce3ef14760`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_EnterPortal()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, int, int)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-exclude"></a>

### C_Exclude

來源 `com/lineage/server/clientpackets/C_Exclude.java`。

- 來源 SHA-256 `35bb5412e2d0d77cf92360b9f2d3a62aa120de2da68d3b6b676ba609cc87f8ed`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Exclude](server-protocol.md#c-exclude)。
- class entry SHA-256 `53c165306d8087498262b1e249bcaab586b56dd64a033ead1c70a99b1f55acfb`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Exclude()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-exitghost"></a>

### C_ExitGhost

來源 `com/lineage/server/clientpackets/C_ExitGhost.java`。

- 來源 SHA-256 `5e7a14c59b100dd3f09384fac731a2e4668d06ab0e53b5c1f7308b25351f1839`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ExitGhost](server-protocol.md#c-exitghost)。
- class entry SHA-256 `4a3857754db73019686d1a009161646229470c1a4a17426949f237484a4482c6`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ExitGhost()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-extracommand"></a>

### C_ExtraCommand

來源 `com/lineage/server/clientpackets/C_ExtraCommand.java`。

- 來源 SHA-256 `5ac261f9461a33ccaa4f33e97e6f355408b59fb26fc8207852ec9b1e5f8dcc51`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ExtraCommand](server-protocol.md#c-extracommand)。
- class entry SHA-256 `0e1ab3b3978d8f823837022898c2c4e43a51c5bc4b1d7d35a8b2c578079d519a`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ExtraCommand()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-fight"></a>

### C_Fight

來源 `com/lineage/server/clientpackets/C_Fight.java`。

- 來源 SHA-256 `c2b6e5057d65b8c7e80f1552b4183361ac625b9fcb65c47f10ec836f275ff323`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Fight](server-protocol.md#c-fight)。
- class entry SHA-256 `572b9aee8f59396bcebbbeeb68340e93a418daf6a81354379c8ec207fe92b9e7`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Fight()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-fishclick"></a>

### C_FishClick

來源 `com/lineage/server/clientpackets/C_FishClick.java`。

- 來源 SHA-256 `ca3b730d75d466ecf4ab451c5695089897802ddb6d7e6224b0ae078ae8b8d729`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_FishClick](server-protocol.md#c-fishclick)。
- class entry SHA-256 `f1d9b46f4a34034cb0fb2ba9b308733f47fde780efcb7d677cae3dfd2727f350`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_FishClick()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-fixweaponlist"></a>

### C_FixWeaponList

來源 `com/lineage/server/clientpackets/C_FixWeaponList.java`。

- 來源 SHA-256 `cdd18397b303b5b1b103969a2bd8c02d5cabc6ae6a49844dd96aa420d99b3ad6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_FixWeaponList](server-protocol.md#c-fixweaponlist)。
- class entry SHA-256 `32b07bfdf8e76ac4069098b915e689001ceb84711876393cf63a1ee254198a7c`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_FixWeaponList()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-giveitem"></a>

### C_GiveItem

來源 `com/lineage/server/clientpackets/C_GiveItem.java`。

- 來源 SHA-256 `87446244c11ab3d1d41c52f83a4c98d8db08aaf1b51d47b30ef2d02666ebcc09`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_GiveItem](server-protocol.md#c-giveitem)。
- class entry SHA-256 `d5d9f168c5a72fcf4e31b561f96009a05bebc1d5a851401f492377e5690b2fdb`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_GiveItem()
private static void executeMonitored(com.lineage.server.clientpackets.C_GiveItem.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private boolean isNpcItemReceivable(com.lineage.server.templates.L1Npc)
private void tamePet(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.templates.L1PetType, com.lineage.server.model.L1PcInventory, com.lineage.server.model.L1Inventory, java.util.function.BooleanSupplier)
private boolean tameAdmissionCurrent(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.templates.L1PetType, com.lineage.server.model.L1PcInventory, com.lineage.server.model.L1Inventory, com.lineage.server.templates.L1Npc, java.util.function.BooleanSupplier)
private void tryEvolvePet(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetType, com.lineage.server.model.L1PcInventory, com.lineage.server.model.L1Inventory, java.util.function.BooleanSupplier)
private void compensatePetEvolution(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.L1PcInventory, com.lineage.server.model.L1Inventory, int, com.lineage.server.model.L1PcInventory.InventoryMutationSnapshot, boolean, java.lang.RuntimeException)
private static java.lang.Throwable retainPetCompensationFailure(java.lang.Throwable, java.lang.Throwable)
private boolean isTamePet(com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.templates.L1PetType)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private void lambda.tryEvolvePet.5(java.util.function.BooleanSupplier, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1PcInventory, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.L1Inventory, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetType, int, com.lineage.server.templates.L1PetType, int)
private void lambda.tamePet.4(com.lineage.server.model.L1PcInventory, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.templates.L1PetType, com.lineage.server.model.L1Inventory, com.lineage.server.templates.L1Npc, java.util.function.BooleanSupplier) throws java.lang.Exception
private void lambda.tamePet.3(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.templates.L1PetType, com.lineage.server.model.L1PcInventory, com.lineage.server.model.L1Inventory, com.lineage.server.templates.L1Npc, java.util.function.BooleanSupplier)
private static void lambda.start.2(com.lineage.server.model.Instance.L1ItemInstance, java.util.function.BooleanSupplier, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.model.L1Inventory, com.lineage.server.model.L1PcInventory, int, long, com.lineage.server.model.Instance.L1ItemInstance[])
private static boolean lambda.start.1(com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance, long, com.lineage.echo.ClientExecutor, long)
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_GiveItem.MonitoredTask)
```

<a id="c-hiresoldier"></a>

### C_HireSoldier

來源 `com/lineage/server/clientpackets/C_HireSoldier.java`。

- 來源 SHA-256 `616f62d6bca532fa3fe980520a9faf16c5ed6aadd76d172be5b6e105a6c27cee`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_HireSoldier](server-protocol.md#c-hiresoldier)。
- class entry SHA-256 `dac9019ccc21f0769d266c175440e13041fa74a1e134ca054cc72dd71f9d28d4`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_HireSoldier()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-itemuse"></a>

### C_ItemUSe

來源 `com/lineage/server/clientpackets/C_ItemUSe.java`。

- 來源 SHA-256 `9cba57e80f4e62f76ccfc461f12294ebc4e8428d74472765f3aba19a9bfd7959`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ItemUSe](server-protocol.md#c-itemuse)。
- class entry SHA-256 `32dea956650ff787697f465f52309ae4653b083281b279cfdb3dd5dcebf0fc6a`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ItemUSe()
private static void executeMonitored(com.lineage.server.clientpackets.C_ItemUSe.MonitoredTask)
static long remainingDelayEffectMillis(java.sql.Timestamp, int, long)
public void start(byte[], com.lineage.echo.ClientExecutor)
void useConfiguredInventoryItem(com.lineage.echo.ClientExecutor, int, int)
static boolean isFreeClassicFunction(com.lineage.server.templates.L1Item)
private void useClassicFunction(com.lineage.echo.ClientExecutor, int, int)
static boolean canUseClassicFunction(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.templates.L1Item)
static boolean supportsInventoryTarget(int)
private void useItemRequest(byte[], com.lineage.echo.ClientExecutor, int, int)
private boolean useItem(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance)
private void useArmor(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance)
private void useWeapon(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.useItemRequest.2(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance) throws java.lang.Exception
private static void lambda.useClassicFunction.1(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, int, int)
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_ItemUSe.MonitoredTask)
```

<a id="c-joinclan"></a>

### C_JoinClan

來源 `com/lineage/server/clientpackets/C_JoinClan.java`。

- 來源 SHA-256 `6a132cc69fae05b7a338f2772469689cdca119a326e75a2ebb9bf935348701af`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_JoinClan](server-protocol.md#c-joinclan)。
- class entry SHA-256 `d5c9db218575dbbc81bed1eb465c19bbbd056e7402c91926e1134cd4d6435bdf`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_JoinClan()
public void start(byte[], com.lineage.echo.ClientExecutor)
private void joinClan(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
```

<a id="c-keepalive"></a>

### C_KeepALIVE

來源 `com/lineage/server/clientpackets/C_KeepALIVE.java`。

- 來源 SHA-256 `d5c68dfc1d2711f9209a654bb6a0da3465360e03f0abafcfe75d3f2d2db3cd85`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_KeepALIVE](server-protocol.md#c-keepalive)。
- class entry SHA-256 `19f6a6728d4658d50f5c8a0307f2db5e694aa0a7f6933af527243c4447ab6213`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_KeepALIVE()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-leaveclan"></a>

### C_LeaveClan

來源 `com/lineage/server/clientpackets/C_LeaveClan.java`。

- 來源 SHA-256 `6e4da42ff3cd31ae98cfa5558a5b273cfc3d95af1fa3390c1a20bfeb128848b6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_LeaveClan](server-protocol.md#c-leaveclan)。
- class entry SHA-256 `e2f4059d8e539ceedfd630fbb6ed58a68131f1c5f04803a6bec8abfd0a43dafa`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_LeaveClan()
private static void executeMonitored(com.lineage.server.clientpackets.C_LeaveClan.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private static void dissolveConfirmed(com.lineage.server.model.L1Clan, java.lang.String[], int, java.lang.String, java.lang.String, com.lineage.server.model.ClanDepartureAccess) throws java.lang.Exception
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.L1Clan, java.lang.String[], int, java.lang.String, java.lang.String, com.lineage.server.model.ClanDepartureAccess) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_LeaveClan.MonitoredTask)
```

<a id="c-leaveparty"></a>

### C_LeaveParty

來源 `com/lineage/server/clientpackets/C_LeaveParty.java`。

- 來源 SHA-256 `5508cc450de0fbc5a4a1a20d3b8b846163b36e1c70e429cf169205bf642dd1ff`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_LeaveParty](server-protocol.md#c-leaveparty)。
- class entry SHA-256 `a4bbb1ceb058d03e20ac8508d2c0f69b0ac6e58dfd07999afd1f99449cbf5441`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_LeaveParty()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-logintoserver"></a>

### C_LoginToServer

來源 `com/lineage/server/clientpackets/C_LoginToServer.java`。

- 來源 SHA-256 `c2eedcb6d78d9da7f33af485c596d393d49818a0a05bc5a37cbedb79bcfc9de4`，與 #132 相符。
- 核對結果為簽章差異。#132 索引連結見 [C_LoginToServer](server-protocol.md#c-logintoserver)。
- class entry SHA-256 `7cb6fc885ca88b7972dab71b9e02ed7ee6c48b23f284b7ab9e29d8c580950cd1`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_LoginToServer()
public static java.lang.String getMonitorLine()
private static boolean isBlank(java.lang.String)
private static java.lang.String safeClientIp(com.lineage.echo.ClientExecutor)
private static void safeKick(com.lineage.echo.ClientExecutor)
private static boolean hasAuthoritativeInventory(com.lineage.server.model.Instance.L1PcInstance)
private static boolean isCurrentLoginRequest(com.lineage.echo.ClientExecutor, com.lineage.server.templates.L1Account, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Lease)
com.lineage.server.model.Instance.L1PcInstance loadAuthorizedCharacter(java.lang.String)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void crown(com.lineage.server.model.Instance.L1PcInstance)
private void getFocus(com.lineage.server.model.Instance.L1PcInstance)
private void marriage(com.lineage.server.model.Instance.L1PcInstance)
private void getOther(com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private void war(com.lineage.server.model.Instance.L1PcInstance)
private void backRestart(com.lineage.server.model.Instance.L1PcInstance)
private void rebindWorldMapAfterLogin(com.lineage.server.model.Instance.L1PcInstance)
private void correctLoginLocation(com.lineage.server.model.Instance.L1PcInstance)
private void items(com.lineage.server.model.Instance.L1PcInstance)
private void bookmarks(com.lineage.server.model.Instance.L1PcInstance)
private void skills(com.lineage.server.model.Instance.L1PcInstance)
private void serchSummon(com.lineage.server.model.Instance.L1PcInstance)
private boolean buff(com.lineage.server.model.Instance.L1PcInstance)
private boolean buff(com.lineage.server.model.Instance.L1PcInstance, boolean)
private void restoreTimedRateIcons(com.lineage.server.model.Instance.L1PcInstance)
private void restoreCustomBuffIcons(com.lineage.server.model.Instance.L1PcInstance)
private void restoreVipVisualsAtLogin(com.lineage.server.model.Instance.L1PcInstance, boolean)
private void syncLoginEquipmentState(com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
```

來源獨有宣告方法

```text
private static void traceLoginLife(com.lineage.echo.ClientExecutor,com.lineage.server.model.Instance.L1PcInstance,java.lang.String)
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static java.lang.Boolean lambda.start.1(com.lineage.echo.ClientExecutor, com.lineage.server.templates.L1Account, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Lease, com.lineage.server.model.CharacterPersistenceService.LoginCharacterIdentity, com.lineage.server.model.Instance.L1PcInstance, long, int)
private static java.lang.Boolean lambda.start.0(com.lineage.echo.ClientExecutor, com.lineage.server.templates.L1Account, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Lease, java.lang.String, com.lineage.server.model.Instance.L1PcInstance)
```

<a id="c-logintoserverok"></a>

### C_LoginToServerOK

來源 `com/lineage/server/clientpackets/C_LoginToServerOK.java`。

- 來源 SHA-256 `59461309d595f1118073a49cc338dce7a6c2fc9f034f1b3007063ba629c0d151`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_LoginToServerOK](server-protocol.md#c-logintoserverok)。
- class entry SHA-256 `806df6da33f7a51c6c688893209f30522259c86d14ee17476bbbffaec4f8352f`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_LoginToServerOK()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-mail"></a>

### C_Mail

來源 `com/lineage/server/clientpackets/C_Mail.java`。

- 來源 SHA-256 `c09c77c25764b894b715f3e84c5837f6e26dab61ae26e462607bbbc904001e38`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Mail](server-protocol.md#c-mail)。
- class entry SHA-256 `4ad9c6c22658918ffaa23f2601748b0ea687d78ff6ec68aa3f33239cc46861fb`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Mail()
private static void executeMonitored(com.lineage.server.clientpackets.C_Mail.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private boolean isOwnMail(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.templates.L1Mail)
private boolean isOwnClanTarget(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, com.lineage.server.model.L1Clan)
private boolean isValidMailPayload(byte[])
private static java.util.function.BooleanSupplier captureMutationAuthority(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.datatables.user.MailTable)
private void clientPackG(com.lineage.server.model.Instance.L1PcInstance, int, int, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier)
private void clientPackF(com.lineage.server.model.Instance.L1PcInstance, int, int, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier)
private void clientPackE(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String, byte[])
private void sendClanMail(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String, byte[])
private void clientPackD(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String, byte[])
private void sendNormalMail(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String, byte[])
private static void quarantineNormalMail(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void clientPackB(com.lineage.server.model.Instance.L1PcInstance, int, int, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier)
private void clientPackA(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static boolean lambda.clientPackB.10(java.util.function.BooleanSupplier, com.lineage.server.templates.L1Mail, int, java.lang.String)
private void lambda.clientPackD.9(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String, byte[])
private void lambda.clientPackE.8(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String, byte[])
private static boolean lambda.clientPackF.7(java.util.function.BooleanSupplier, com.lineage.server.templates.L1Mail, int, java.lang.String)
private static boolean lambda.clientPackG.6(java.util.function.BooleanSupplier, com.lineage.server.templates.L1Mail, int, java.lang.String)
private static boolean lambda.captureMutationAuthority.5(com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier, java.lang.String, com.lineage.server.model.Instance.L1PcInstance, int)
private static boolean lambda.captureMutationAuthority.4(com.lineage.echo.SessionPhaseGate.UiAuthority, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor, long, int, java.lang.String)
private void lambda.start.3(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier, java.lang.Integer, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier, java.lang.String, byte[], com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.echo.SessionPhaseGate.Permit, long, com.lineage.echo.ClientExecutor, java.lang.String, byte[], java.lang.Integer, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier, java.lang.Integer, com.lineage.server.datatables.user.MailTable, java.util.function.BooleanSupplier) throws java.lang.Exception
private void lambda.start.2(com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.SessionPhaseGate.Permit, long, com.lineage.echo.ClientExecutor, int, java.lang.String, byte[])
private void lambda.start.1(com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.SessionPhaseGate.Permit, long, com.lineage.echo.ClientExecutor, int, java.lang.String, byte[])
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_Mail.MonitoredTask)
```

<a id="c-movechar"></a>

### C_MoveChar

來源 `com/lineage/server/clientpackets/C_MoveChar.java`。

- 來源 SHA-256 `b569737e5fcda200291f803f41f4f9bddaa6cc6986c3869274934bcbc48a12d2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_MoveChar](server-protocol.md#c-movechar)。
- class entry SHA-256 `005be9ca4cf39bcf307454a8337dc1f81355b464c1f8f2da735b93b796c06341`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_MoveChar()
private static int clientDriftChebyshev(int, int, int, int)
private static boolean isHardBlockedTile(com.lineage.server.model.map.L1Map, int, int)
private static java.lang.String describeRawTile(com.lineage.server.model.map.L1Map, int, int)
private static void logRecentAutoHuntStopMoveReject(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int, int, int, java.lang.String)
private static void executeMonitored(com.lineage.server.clientpackets.C_MoveChar.MonitoredTask)
private static void softBlockMove(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int, int, int, int, int, int, int, java.lang.String)
private static void applyAcceptedMoveEffects(com.lineage.server.model.Instance.L1PcInstance)
private static void commitAcceptedMovePacing(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.clientpackets.AcceleratorChecker.AcceptedIntervalReservation, com.lineage.server.model.ClientActionTimeline.Reservation)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, int, int, int) throws java.lang.Exception
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance, int, int, int)
```

<a id="c-newaccess"></a>

### C_NewAccess

來源 `com/lineage/server/clientpackets/C_NewAccess.java`。

- 來源 SHA-256 `d694f991e774169b0739fa935698bb26f36595813da14cbe4ea057a7d991c434`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_NewAccess](server-protocol.md#c-newaccess)。
- class entry SHA-256 `0a60ae5720d9a0c188c9bf9879e6e28c35f7f31fc983cda5b127e5d7702fdc0e`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_NewAccess()
private static void executeMonitored(com.lineage.server.clientpackets.C_NewAccess.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.echo.ClientExecutor, java.lang.String, java.lang.String, java.lang.String, java.lang.String, java.lang.String) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_NewAccess.MonitoredTask)
```

<a id="c-newcharselect"></a>

### C_NewCharSelect

來源 `com/lineage/server/clientpackets/C_NewCharSelect.java`。

- 來源 SHA-256 `6154a69a9cd1b074667b98c077d4c349043b9cdc61e16fdb36a108b16bb5b1e3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_NewCharSelect](server-protocol.md#c-newcharselect)。
- class entry SHA-256 `d1fa6fbad496b5fec45d31cad9cc26ac0331a378cc7c4df1077033d74c7c7a80`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_NewCharSelect()
private static void scheduleMonitored(com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.clientpackets.C_NewCharSelect.MonitoredTask, long)
private static void failClosed(com.lineage.echo.ClientExecutor.ActiveCharacterBinding)
public void start(byte[], com.lineage.echo.ClientExecutor)
boolean quitForCharacterSwitch(com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private void lambda.start.2(com.lineage.echo.ClientExecutor, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private static void lambda.scheduleMonitored.1(com.lineage.echo.ClientExecutor.ActiveCharacterBinding, java.lang.Runnable)
private static void lambda.scheduleMonitored.0(com.lineage.server.clientpackets.C_NewCharSelect.MonitoredTask)
```

<a id="c-npcaction"></a>

### C_NPCAction

來源 `com/lineage/server/clientpackets/C_NPCAction.java`。

- 來源 SHA-256 `280ef4bb7316ca61e199ae97a71405ec45affc37a8b4955c4881a891b298b0a1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_NPCAction](server-protocol.md#c-npcaction)。
- class entry SHA-256 `a852ccac8ea10a55b590c311c4cc646891b1bd7d7d108f7ddb199d6c55a0142c`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_NPCAction()
private void executeMonitored(com.lineage.server.clientpackets.C_NPCAction.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private com.lineage.server.clientpackets.C_NPCAction.NpcActionResult handleSpecificNpcAction(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String, com.lineage.echo.ClientExecutor) throws java.lang.Exception
private com.lineage.server.clientpackets.C_NPCAction.NpcActionResult handleTownAction(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, java.lang.String) throws java.lang.Exception
private com.lineage.server.clientpackets.C_NPCAction.NpcActionResult handleCastleAction(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, java.lang.String) throws java.lang.Exception
private com.lineage.server.clientpackets.C_NPCAction.NpcActionResult handleElfAttribute(com.lineage.server.model.Instance.L1PcInstance, java.lang.String) throws java.lang.Exception
private com.lineage.server.clientpackets.C_NPCAction.NpcActionResult handlePirateIslandAction(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, java.lang.String) throws java.lang.Exception
private com.lineage.server.clientpackets.C_NPCAction.NpcActionResult handleDemonRealmAction(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Object, java.lang.String, com.lineage.echo.ClientExecutor) throws java.lang.Exception
private java.lang.String karmaLevelToHtmlId(int)
private java.lang.String watchUb(com.lineage.server.model.Instance.L1PcInstance, int)
private java.lang.String enterUb(com.lineage.server.model.Instance.L1PcInstance, int)
private java.lang.String enterHauntedHouse(com.lineage.server.model.Instance.L1PcInstance)
private void poly(com.lineage.echo.ClientExecutor, int)
private void polyByKeplisha(com.lineage.echo.ClientExecutor, int)
private java.lang.String sellHouse(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void openCloseDoor(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private static com.lineage.server.model.L1Clan currentClan(com.lineage.server.model.Instance.L1PcInstance)
private static boolean mayControlHouseDoor(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, int, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.templates.L1House, com.lineage.server.utils.WorldInteractionGuard.Interaction)
private com.lineage.server.clientpackets.C_NPCAction.GateState currentGateState(com.lineage.server.model.Instance.L1PcInstance, int)
private void upgradeHouseBasement(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
private static void quarantineBasementInventory(com.lineage.server.model.L1PcInventory, java.lang.Throwable)
private void openCloseGate(com.lineage.server.model.Instance.L1PcInstance, int, boolean)
private boolean isExistDefenseClan(int)
private void teleportFromHouse(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, int, com.lineage.server.model.npc.UiOperationContext.Ticket)
private void expelOtherClan(com.lineage.server.model.Instance.L1PcInstance, int)
private static boolean mayExpelFromHouse(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, int, java.lang.String, int, int, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.templates.L1House, com.lineage.server.utils.WorldInteractionGuard.Interaction)
private void repairGate(com.lineage.server.model.Instance.L1PcInstance)
private void payFee(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
private static void quarantineHouseTaxInventory(com.lineage.server.model.L1PcInventory, java.lang.Throwable)
private java.lang.String[] makeHouseTaxStrings(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
private java.lang.String[] makeWarTimeStrings(int)
private java.lang.String getYaheeAmulet(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private java.lang.String getBarlogEarring(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private java.lang.String[] makeUbInfoStrings(int)
private java.lang.String talkToDimensionDoor(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private void getBloodCrystalByKarma(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private void getSoulCrystalByKarma(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private void Shells(com.lineage.server.model.Instance.L1NpcInstance, int, int, int)
private void Shells1(com.lineage.server.model.Instance.L1NpcInstance, int, int, int)
private static boolean grantNpcRewards(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int[], long[], java.lang.String)
private static boolean grantNpcRewardsLocked(com.lineage.server.model.Instance.L1PcInstance, int[], long[], java.util.List<com.lineage.server.model.Instance.L1ItemInstance>, java.lang.String)
private static void quarantineNpcRewardFailure(com.lineage.server.model.L1PcInventory, java.lang.String, java.lang.Throwable)
private static com.lineage.server.model.Instance.L1ItemInstance npcRewardNoticeTarget(com.lineage.server.model.L1PcInventory, com.lineage.server.model.Instance.L1ItemInstance)
private static boolean upgradeSequentialNpcReward(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int, int[], java.lang.String)
private static com.lineage.server.model.Instance.L1ItemInstance upgradeSequentialNpcRewardLocked(com.lineage.server.model.Instance.L1PcInstance, int, int[], java.lang.String)
private static boolean exchangeQuestItems(com.lineage.server.model.Instance.L1PcInstance, int[], long[], int[], long[], java.lang.String)
private static boolean exchangeQuestItemsLocked(com.lineage.server.model.Instance.L1PcInstance, int[], long[], int[], long[], java.lang.String)
private static void quarantineQuestFailure(com.lineage.server.model.L1PcInventory, java.lang.String, java.lang.Throwable)
private static java.lang.IllegalStateException questFrameChanged(com.lineage.server.model.L1PcInventory, java.lang.String)
private static void restoreUnawardedQuestMaterials(com.lineage.server.model.L1PcInventory, com.lineage.server.model.L1PcInventory.InventoryMutationSnapshot, java.lang.String)
private static void requireQuestExchangeOwner(com.lineage.server.model.Instance.L1PcInstance)
private static java.util.List<com.lineage.server.clientpackets.C_NPCAction.QuestDebit> questDebitPlan(com.lineage.server.model.L1PcInventory, int[], long[])
private static void requireQuestDebit(com.lineage.server.model.L1PcInventory, com.lineage.server.clientpackets.C_NPCAction.QuestDebit)
private static java.util.Map<com.lineage.server.model.Instance.L1ItemInstance, java.lang.Long> questInventoryCounts(com.lineage.server.model.L1PcInventory)
private static void requireQuestGrant(com.lineage.server.clientpackets.C_NPCAction.QuestGrant)
private static void requireQuestProduct(com.lineage.server.clientpackets.C_NPCAction.QuestProduct)
private static void checkQuestPreparedCapacity(com.lineage.server.model.Instance.L1PcInstance, java.util.Map<com.lineage.server.model.Instance.L1ItemInstance, java.lang.Long>, java.util.List<com.lineage.server.clientpackets.C_NPCAction.QuestProduct>)
private static void discardUnusedQuestProduct(com.lineage.server.model.L1PcInventory, com.lineage.server.model.Instance.L1ItemInstance)
private static boolean isAuctionAction(java.lang.String)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static java.lang.Boolean lambda.exchangeQuestItems.37(com.lineage.server.model.Instance.L1PcInstance, int[], long[], int[], long[], java.lang.String)
private static void lambda.exchangeQuestItems.36(boolean[], com.lineage.server.model.Instance.L1PcInstance, int[], long[], int[], long[], java.lang.String)
private static java.lang.Boolean lambda.upgradeSequentialNpcReward.35(com.lineage.server.model.Instance.L1PcInstance, int, int[], java.lang.String, java.lang.String)
private static void lambda.upgradeSequentialNpcReward.34(com.lineage.server.model.Instance.L1ItemInstance[], com.lineage.server.model.Instance.L1PcInstance, int, int[], java.lang.String)
private static java.lang.Boolean lambda.grantNpcRewards.33(com.lineage.server.model.Instance.L1PcInstance, int[], long[], java.lang.String, java.lang.String)
private static void lambda.grantNpcRewards.32(boolean[], com.lineage.server.model.Instance.L1PcInstance, int[], long[], java.util.List, java.lang.String)
private static void lambda.payFee.31(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.L1PcInventory, java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.utils.WorldInteractionGuard.Interaction, int, java.util.function.BooleanSupplier) throws java.lang.Exception
private static void lambda.payFee.30(int, com.lineage.server.model.L1PcInventory, java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.Instance.L1PcInstance, int, java.util.function.BooleanSupplier)
private static java.lang.Object lambda.payFee.29(com.lineage.server.model.L1PcInventory, java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, int, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.Instance.L1PcInstance, int, java.util.function.BooleanSupplier)
private static void lambda.payFee.28(java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, int, com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1PcInventory, int, java.util.function.BooleanSupplier)
private static boolean lambda.payFee.27(java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, boolean)
private static boolean lambda.payFee.26(com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.utils.WorldInteractionGuard.Interaction, java.util.function.BooleanSupplier, com.lineage.server.model.L1PcInventory)
private static boolean lambda.payFee.25(java.lang.String, com.lineage.server.model.L1Clan, int, int)
private static void lambda.repairGate.24(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, int, com.lineage.server.utils.WorldInteractionGuard.Interaction) throws java.lang.Exception
private static void lambda.expelOtherClan.23(int, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, int, java.lang.String, int, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.utils.WorldInteractionGuard.Interaction) throws java.lang.Exception
private static void lambda.teleportFromHouse.22(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.clientpackets.C_NPCAction.HouseTeleportRequest, int, int) throws java.lang.Exception
private static void lambda.teleportFromHouse.21(com.lineage.server.clientpackets.C_NPCAction.HouseTeleportRequest, int, int, com.lineage.server.model.Instance.L1PcInstance)
private static boolean lambda.teleportFromHouse.20(com.lineage.server.clientpackets.C_NPCAction.HouseTeleportRequest)
private void lambda.openCloseGate.19(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.utils.WorldInteractionGuard.Interaction, boolean) throws java.lang.Exception
private static void lambda.upgradeHouseBasement.18(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.L1PcInventory, java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.utils.WorldInteractionGuard.Interaction, int, java.util.function.BooleanSupplier) throws java.lang.Exception
private static void lambda.upgradeHouseBasement.17(int, com.lineage.server.model.L1PcInventory, java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.utils.WorldInteractionGuard.Interaction, int, java.util.function.BooleanSupplier)
private static java.lang.Object lambda.upgradeHouseBasement.16(com.lineage.server.model.L1PcInventory, java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.utils.WorldInteractionGuard.Interaction, int, int, java.util.function.BooleanSupplier)
private static void lambda.upgradeHouseBasement.15(java.util.function.BooleanSupplier, com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1PcInventory, com.lineage.server.utils.WorldInteractionGuard.Interaction, int, int, java.util.function.BooleanSupplier)
private static boolean lambda.upgradeHouseBasement.14(com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.utils.WorldInteractionGuard.Interaction, java.util.function.BooleanSupplier, com.lineage.server.model.L1PcInventory, com.lineage.server.model.L1Clan)
private static boolean lambda.upgradeHouseBasement.13(java.lang.String, com.lineage.server.model.L1Clan, int, int)
private static void lambda.openCloseDoor.12(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.L1Clan, com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.utils.WorldInteractionGuard.Interaction, java.lang.String) throws java.lang.Exception
private static void lambda.handleElfAttribute.11(com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private static void lambda.handleCastleAction.10(com.lineage.server.model.npc.CastleTreasuryAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.L1Object) throws java.lang.Exception
private static void lambda.handleCastleAction.9(com.lineage.server.model.npc.CastleTreasuryAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.L1Object) throws java.lang.Exception
private static void lambda.handleCastleAction.8(com.lineage.server.model.Instance.L1PcInstance, int) throws java.lang.Exception
private static void lambda.handleTownAction.7(com.lineage.server.model.Instance.L1PcInstance, int) throws java.lang.Exception
private static void lambda.handleTownAction.6(com.lineage.server.model.Instance.L1PcInstance, int) throws java.lang.Exception
private static void lambda.handleTownAction.5(com.lineage.server.model.Instance.L1PcInstance, int) throws java.lang.Exception
private static void lambda.start.4(int[], long[], int, com.lineage.server.model.Instance.L1PcInstance, int[], int[], int[], java.lang.String, java.lang.String[], com.lineage.server.model.L1Object, java.lang.String) throws java.lang.Exception
private static void lambda.start.3(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.L1Object) throws java.lang.Exception
private static void lambda.start.2(com.lineage.server.model.npc.HouseNameAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.Instance.L1NpcInstance) throws java.lang.Exception
private static void lambda.start.1(com.lineage.server.model.npc.CastleDepositAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.L1Object) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.utils.WorldInteractionGuard.Interaction, com.lineage.server.clientpackets.C_NPCAction.MonitoredTask)
```

<a id="c-npctalk"></a>

### C_NPCTalk

來源 `com/lineage/server/clientpackets/C_NPCTalk.java`。

- 來源 SHA-256 `7a324af75ce43054f87a500d6a16600ddf01209ee5c5d5d4d2f4cf1377457027`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_NPCTalk](server-protocol.md#c-npctalk)。
- class entry SHA-256 `1786ac226467151513e5a51ea8629bcfbb3f6c4c80c8c108384766231c94d4a4`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_NPCTalk()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-party"></a>

### C_Party

來源 `com/lineage/server/clientpackets/C_Party.java`。

- 來源 SHA-256 `3a159744b4e70a125c12f7a73ecb67638473de73e6caecf834fc5a58a8062ee4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Party](server-protocol.md#c-party)。
- class entry SHA-256 `f599e271b89b3cef6fd693b79d3d3d12d900e89f5bf29f1545f7348eb2a61c66`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Party()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-password"></a>

### C_Password

來源 `com/lineage/server/clientpackets/C_Password.java`。

- 來源 SHA-256 `5f4f6b0042b734f5b36f5b8eb38ba5d84f81ad3b18231051c00ea84d82f4ebe0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Password](server-protocol.md#c-password)。
- class entry SHA-256 `8ece799f306cd6723cf7b68e21ec2b28283132059c95dc261008742c7e742545`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Password()
private static void executeMonitored(com.lineage.server.clientpackets.C_Password.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void stopAction(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(java.lang.String, int) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_Password.MonitoredTask)
```

<a id="c-petmenu"></a>

### C_PetMenu

來源 `com/lineage/server/clientpackets/C_PetMenu.java`。

- 來源 SHA-256 `ee4e63ee86f0a0122a2cdc87e8f0c106be2e8e1a6ba4c1da894487e3adbc8c9f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_PetMenu](server-protocol.md#c-petmenu)。
- class entry SHA-256 `03c1a12800d83279b6f653ffd7e585c066c3a248da003886760b486063f7cc60`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_PetMenu()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
private static void syncPetEquipmentStatus(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance)
```

<a id="c-pickupitem"></a>

### C_PickUpItem

來源 `com/lineage/server/clientpackets/C_PickUpItem.java`。

- 來源 SHA-256 `804b4ba715c505f458c63e78f6cfd81f03c65c618ff3a1b506ad7da7a0772254`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_PickUpItem](server-protocol.md#c-pickupitem)。
- class entry SHA-256 `721b3f27cd3be1bbe1ac9f5c1646946af7a53b6dcbff2d759ceb9081ff8c45a5`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_PickUpItem()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1ItemInstance, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance, long, com.lineage.echo.ClientExecutor, long, int, int, com.lineage.server.model.L1Inventory, int, long)
```

<a id="c-pledge"></a>

### C_Pledge

來源 `com/lineage/server/clientpackets/C_Pledge.java`。

- 來源 SHA-256 `aeacb3730a64876991c059f160899830d2bc3d71b430aa47f17296b000f2e04a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Pledge](server-protocol.md#c-pledge)。
- class entry SHA-256 `0277c1f541da96712536ea8c7b681c19e10df9c7df684805eae93f9125014005`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Pledge()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-pledgecontent"></a>

### C_PledgeContent

來源 `com/lineage/server/clientpackets/C_PledgeContent.java`。

- 來源 SHA-256 `78c906fd729aacbcc33385249e963c9494d7a373f52c579f94c82b4089eca4d1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_PledgeContent](server-protocol.md#c-pledgecontent)。
- class entry SHA-256 `60ac4558170e1e1b2678ecd0e7c788f0d006a55daf84d6434000b45f58c759ca`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_PledgeContent()
private static void executeMonitored(com.lineage.server.clientpackets.C_PledgeContent.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.5(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, java.lang.String) throws java.lang.Exception
private static void lambda.start.4(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, int, int, int, int) throws java.lang.Exception
private static void lambda.start.3(com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.Instance.L1PcInstance, int, int, int, int)
private static void lambda.start.2(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, int, int, int) throws java.lang.Exception
private static void lambda.start.1(com.lineage.server.model.npc.UiOperationContext.Ticket, com.lineage.server.model.Instance.L1PcInstance, int, int, int)
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_PledgeContent.MonitoredTask)
```

<a id="c-pledgerecommendation"></a>

### C_PledgeRecommendation

來源 `com/lineage/server/clientpackets/C_PledgeRecommendation.java`。

- 來源 SHA-256 `3b70b9fa2b3dba3d29918e176b21c04838b3a6fed227cc947adc946b64ea5850`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_PledgeRecommendation](server-protocol.md#c-pledgerecommendation)。
- class entry SHA-256 `1b674c073de26b5272701f1ccde2442c87a0bbdcc3de7bc5f21d1e628e75e3e0`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_PledgeRecommendation()
private static void executeMonitored(com.lineage.server.clientpackets.C_PledgeRecommendation.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private static void reportUnconfirmed(com.lineage.server.model.Instance.L1PcInstance)
private final com.lineage.server.model.L1Clan getClan(int)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.17(com.lineage.server.datatables.user.ClanRecommendTable, int, int, java.util.Map, java.util.concurrent.CopyOnWriteArrayList, boolean, java.util.function.BooleanSupplier, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.model.Instance.L1PcInstance, int, int) throws java.lang.Exception
private static boolean lambda.start.16(com.lineage.server.datatables.user.ClanTable, com.lineage.server.datatables.user.ClanRecommendTable, int, com.lineage.server.model.L1Clan, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.start.15(com.lineage.server.model.ClanRecommendationAccess, com.lineage.server.model.Instance.L1PcInstance, int, int, int, com.lineage.server.model.L1Clan) throws java.lang.Exception
private static boolean lambda.start.14(boolean, com.lineage.server.model.L1Clan, com.lineage.server.model.L1Clan, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.model.Instance.L1PcInstance, java.lang.String, com.lineage.server.datatables.user.ClanTable, com.lineage.server.datatables.user.ClanRecommendTable)
private static void lambda.start.13(com.lineage.server.datatables.user.ClanRecommendTable, int, java.lang.String, int, java.lang.String, java.util.function.BooleanSupplier, java.util.function.BooleanSupplier, com.lineage.server.model.Instance.L1PcInstance, int) throws java.lang.Exception
private static boolean lambda.start.12(com.lineage.server.datatables.user.ClanTable, com.lineage.server.datatables.user.ClanRecommendTable, int, com.lineage.server.model.L1Clan, java.lang.String, java.util.function.BooleanSupplier)
private static boolean lambda.start.11(java.util.function.BooleanSupplier, com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int)
private static boolean lambda.start.10(com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.SessionPhaseGate.UiAuthority, com.lineage.echo.ClientExecutor, long, int, java.lang.String)
private static void lambda.start.9(java.util.function.BooleanSupplier, com.lineage.server.datatables.user.ClanRecommendTable, int, com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private static boolean lambda.start.8(boolean, com.lineage.server.model.L1Clan, com.lineage.server.model.L1Clan, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.datatables.user.ClanTable, com.lineage.server.datatables.user.ClanRecommendTable, int, com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.start.7(java.util.function.BooleanSupplier, com.lineage.server.datatables.user.ClanRecommendTable, int, com.lineage.server.datatables.user.ClanTable, int, com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private static boolean lambda.start.6(com.lineage.server.datatables.user.ClanTable, com.lineage.server.datatables.user.ClanRecommendTable, java.util.function.BooleanSupplier, java.lang.String, com.lineage.server.model.Instance.L1PcInstance, int)
private static boolean lambda.start.5(com.lineage.echo.ClientExecutor.ActiveCharacterBinding, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.SessionPhaseGate.UiAuthority, com.lineage.echo.ClientExecutor, long, int, java.lang.String)
private static void lambda.start.4(com.lineage.server.datatables.user.ClanRecommendTable, int, java.util.function.BooleanSupplier, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private static boolean lambda.start.3(com.lineage.server.datatables.user.ClanTable, com.lineage.server.datatables.user.ClanRecommendTable, int, com.lineage.server.model.L1Clan, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.start.2(com.lineage.server.datatables.user.ClanRecommendTable, int, java.lang.String, java.lang.String, int, java.lang.String, java.util.function.BooleanSupplier, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private static boolean lambda.start.1(com.lineage.server.datatables.user.ClanTable, com.lineage.server.datatables.user.ClanRecommendTable, int, com.lineage.server.model.L1Clan, com.lineage.server.model.ClanDepartureAccess, com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_PledgeRecommendation.MonitoredTask)
```

<a id="c-propose"></a>

### C_Propose

來源 `com/lineage/server/clientpackets/C_Propose.java`。

- 來源 SHA-256 `81d8dd895063670d40f03b4712423f13d06fbccf672b43fac1b25ee066edf70d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Propose](server-protocol.md#c-propose)。
- class entry SHA-256 `f941cb2f7d4280697d6dc228b0c13e2857d59ca253025fab897c8178f91a856c`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Propose()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-rank"></a>

### C_Rank

來源 `com/lineage/server/clientpackets/C_Rank.java`。

- 來源 SHA-256 `0e284028348f160ed06a9241e0ec36ec5e4b0127f3f42dc2368773be76e17e14`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Rank](server-protocol.md#c-rank)。
- class entry SHA-256 `d3d43c50a90d6d57324021747b6035479a530bcafa7fb878e4c7899a55bbf292`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Rank()
private static void executeMonitored(com.lineage.server.clientpackets.C_Rank.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void rank(com.lineage.server.model.Instance.L1PcInstance, int, java.lang.String)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.rank.2(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int, int) throws java.lang.Exception
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_Rank.MonitoredTask)
```

<a id="c-restart"></a>

### C_Restart

來源 `com/lineage/server/clientpackets/C_Restart.java`。

- 來源 SHA-256 `69e421abeb5fe0bf168d179ea9c3719afee6fc8e41d04cf9696da14afcbab7b8`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Restart](server-protocol.md#c-restart)。
- class entry SHA-256 `4406eb782ca983e11ced3bb6169bc395a1f9e53566076a8a1880f8abac8430c3`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Restart()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-result"></a>

### C_Result

來源 `com/lineage/server/clientpackets/C_Result.java`。

- 來源 SHA-256 `4f616c8995cd1a43c6a1f4a133c98c68e6d686de0d89b05fcd910b86e155bae5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Result](server-protocol.md#c-result)。
- class entry SHA-256 `bb8fc20322346195e80fc159c413ee36b509a0114e4ebc6cd57d9285853a1f61`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Result()
private static void executeAsync(java.lang.Runnable)
private static com.lineage.server.clientpackets.C_Result.WarehouseWithdrawResult withdrawWarehouseItem(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Inventory, com.lineage.server.model.Instance.L1ItemInstance, long, int, long, java.lang.String)
private static boolean handleWarehouseWithdrawResult(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.clientpackets.C_Result.WarehouseWithdrawResult, int, java.lang.String, long)
private static boolean isActivePetControlItem(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance)
private static boolean isWarehousePersistenceSafe(com.lineage.server.model.Instance.L1ItemInstance, boolean)
private static void rejectUnsupportedWarehouseItem(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void mode_update_item(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_cn(com.lineage.server.model.Instance.L1PcInstance, int, int, boolean)
private void mode_gambling(com.lineage.server.model.Instance.L1PcInstance, int, int, boolean)
private void mode_lotto(com.lineage.server.model.Instance.L1PcInstance, int, int, boolean)
private void mode_warehouse_elf_out(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_warehouse_elf_in(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_warehouse_clan_out(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_warehouse_clan_in(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_warehouse_cha_out(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_warehouse_cha_in(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_warehouse_out(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_warehouse_in(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_sellde(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1DeInstance, int)
private void modeSellDeLocked(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1DeInstance, int)
private void mode_buyde(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1DeInstance, int)
private void modeBuyDeLocked(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1DeInstance, int)
private void mode_sellpc(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, int)
private void mode_buypc(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, int)
private void mode_sell(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void mode_buy(com.lineage.server.model.Instance.L1PcInstance, int, int)
private static void recordLocalRemoveQueueCreated()
private static void recordLocalRemoveQueued(int)
private static void recordLocalRemoveProcessed()
private static void updateLocalRemoveQueueMaxSize(int)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.mode_buy.16(com.lineage.server.model.Instance.L1PcInstance, int, com.lineage.server.model.Instance.L1PcInstance, int[], int[], int)
private static void lambda.mode_sell.15(com.lineage.server.model.Instance.L1PcInstance, int, int, int[], int[])
private static void lambda.mode_buypc.14(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance, int, int[], int[], com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.mode_buypc.13(com.lineage.server.model.Instance.L1ItemInstance, long, int, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance)
private static void lambda.mode_sellpc.12(com.lineage.server.model.Instance.L1ItemInstance, long, int, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance)
private void lambda.mode_buyde.11(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1DeInstance, int)
private void lambda.mode_sellde.10(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1DeInstance, int)
private static void lambda.mode_warehouse_in.9(com.lineage.server.model.Instance.L1PcInstance, int, int[], int[], int)
private static void lambda.mode_warehouse_cha_in.8(com.lineage.server.model.Instance.L1PcInstance, int, int[], int[], int)
private static void lambda.mode_warehouse_cha_out.7(com.lineage.server.model.Instance.L1PcInstance, int, int[], int[], int)
private static void lambda.mode_warehouse_clan_in.6(com.lineage.server.model.Instance.L1PcInstance, java.lang.String, int, int[], int[], int)
private static void lambda.mode_warehouse_clan_out.5(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, int, int[], int[], java.lang.String)
private static void lambda.mode_warehouse_elf_in.4(com.lineage.server.model.Instance.L1PcInstance, int, int[], int[], int)
private static void lambda.mode_warehouse_elf_out.3(com.lineage.server.model.Instance.L1PcInstance, int, int[], int[], java.lang.String)
private static void lambda.mode_update_item.2(com.lineage.server.model.Instance.L1PcInstance, int, int, int)
private static com.lineage.server.clientpackets.C_Result.WarehouseWithdrawResult lambda.withdrawWarehouseItem.1(com.lineage.server.model.L1Inventory, com.lineage.server.model.Instance.L1PcInstance, java.lang.String, com.lineage.server.model.Instance.L1ItemInstance, long, int, long)
private static void lambda.executeAsync.0(java.lang.Runnable)
```

<a id="c-returntologin"></a>

### C_ReturnToLogin

來源 `com/lineage/server/clientpackets/C_ReturnToLogin.java`。

- 來源 SHA-256 `47bda279765fe5b92f50c6d7d55248a05a6b5052b4515f47b94c16f25e47eb85`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ReturnToLogin](server-protocol.md#c-returntologin)。
- class entry SHA-256 `dfa2e549128334cedc5bef543f0abde0d57a1a256a8e3fbce708e0b361ed6a8b`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ReturnToLogin()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-selectlist"></a>

### C_SelectList

來源 `com/lineage/server/clientpackets/C_SelectList.java`。

- 來源 SHA-256 `49532fe36d8bb990cee9fb230845f33cfd813085e8e7f33e1881a411799f28c4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_SelectList](server-protocol.md#c-selectlist)。
- class entry SHA-256 `ef382d8eac8f7249f48b0e5981df597212b93ff24960cbfc6eb992e1aef8fa02`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_SelectList()
private static void executeMonitored(com.lineage.server.clientpackets.C_SelectList.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, int) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_SelectList.MonitoredTask)
```

<a id="c-selecttarget"></a>

### C_SelectTarget

來源 `com/lineage/server/clientpackets/C_SelectTarget.java`。

- 來源 SHA-256 `d6ac219ee87d0a89762db9efb799d23e22d65374bfbf82b91d245c91510ec141`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_SelectTarget](server-protocol.md#c-selecttarget)。
- class entry SHA-256 `bae8327148662d6ef67ab9d9370ab23ed22e50fe011f8bbc14246ccf9746de13`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_SelectTarget()
public void start(byte[], com.lineage.echo.ClientExecutor)
static com.lineage.server.model.L1Character resolveAuthorizedTarget(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Character, short, int, com.lineage.server.model.L1Object)
public java.lang.String getType()
```

<a id="c-serverversion"></a>

### C_ServerVersion

來源 `com/lineage/server/clientpackets/C_ServerVersion.java`。

- 來源 SHA-256 `2a88e34cfa5f7a097274e7cd1c6de50c7f966cf2595f05de436bcf4313f39e1d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ServerVersion](server-protocol.md#c-serverversion)。
- class entry SHA-256 `b213d648111597e8b55ef7535e045cdd920c514289511d64e1e0cd7156f20614`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ServerVersion()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-ship"></a>

### C_Ship

來源 `com/lineage/server/clientpackets/C_Ship.java`。

- 來源 SHA-256 `1be305fce6d3859627dc6ae189a6c97924997692a394eff81ca49c1e2faed0e2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Ship](server-protocol.md#c-ship)。
- class entry SHA-256 `0a55ae3cc87895da7e8308caf547ea3c121a5ac9b797676db1af47d0a2db9447`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Ship()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-shop"></a>

### C_Shop

來源 `com/lineage/server/clientpackets/C_Shop.java`。

- 來源 SHA-256 `cd94e74004c18471e01faf290e8f2320193fb2fcafadc94998fbcedc288b9bff`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Shop](server-protocol.md#c-shop)。
- class entry SHA-256 `0b75f057a27dc97041e6e74fc08bfde9b01ac68c2cbbf4d3455d8278d1279789`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Shop()
public void start(byte[], com.lineage.echo.ClientExecutor)
private static void closeShop(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.echo.ClientExecutor.ActiveCharacterBinding, long)
public java.lang.String getType()
```

<a id="c-shoplist"></a>

### C_ShopList

來源 `com/lineage/server/clientpackets/C_ShopList.java`。

- 來源 SHA-256 `1aef96d5889fa3590767e99287782e8f06cf7de496272b4da12e48d0197b37c5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_ShopList](server-protocol.md#c-shoplist)。
- class entry SHA-256 `17df8863cfa2943a9ca7a1006fd2723e191b1f80323c381f412016798b329624`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_ShopList()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-skillbuy"></a>

### C_SkillBuy

來源 `com/lineage/server/clientpackets/C_SkillBuy.java`。

- 來源 SHA-256 `ce89743248e65b2941c02bcc20f50610e442fa2364fbf7ecd75565cee8bc5111`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_SkillBuy](server-protocol.md#c-skillbuy)。
- class entry SHA-256 `422df146710dc60d0ecc1558051fe55eff83d37d73c4790faf3824627815f1aa`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_SkillBuy()
private static void executeMonitored(com.lineage.server.clientpackets.C_SkillBuy.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.npc.UiOperationContext.Ticket, java.util.ArrayList, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_SkillBuy.MonitoredTask)
```

<a id="c-skillbuyitem"></a>

### C_SkillBuyItem

來源 `com/lineage/server/clientpackets/C_SkillBuyItem.java`。

- 來源 SHA-256 `d54fa4c4cb1dcd6271dbfa494a6c4edf35465b93954efac79c6d7e9b93b433c3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_SkillBuyItem](server-protocol.md#c-skillbuyitem)。
- class entry SHA-256 `23ada630b0fff49a3976e875d300d946ea96d09b51eec4221cdcd3efa13e0df0`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_SkillBuyItem()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-skillbuyitemok"></a>

### C_SkillBuyItemOK

來源 `com/lineage/server/clientpackets/C_SkillBuyItemOK.java`。

- 來源 SHA-256 `e711e91ea6779a716c401315ef2fb0f1902bc7866b09bcc8306601a09fba9b00`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_SkillBuyItemOK](server-protocol.md#c-skillbuyitemok)。
- class entry SHA-256 `095ec0fc661b046439564d3c55c75939434c15495f1675ba07e0a90439b7a1bb`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_SkillBuyItemOK()
private static void runMonitored(com.lineage.server.clientpackets.C_SkillBuyItemOK.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.npc.UiOperationContext.Ticket, int, int[], com.lineage.server.model.Instance.L1PcInstance, java.util.ArrayList) throws java.lang.Exception
private static boolean lambda.start.0(com.lineage.server.model.npc.UiOperationContext.Ticket, int)
```

<a id="c-skillbuyok"></a>

### C_SkillBuyOK

來源 `com/lineage/server/clientpackets/C_SkillBuyOK.java`。

- 來源 SHA-256 `b8e4f9c9d7a35d87f2849b6f074143323740318f9cdb8d6ca6ac3dcbe2a0765a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_SkillBuyOK](server-protocol.md#c-skillbuyok)。
- class entry SHA-256 `87959493389c67575c382586ff82d5e39adecab39dc5ff29a1e8ac5c5c447a39`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_SkillBuyOK()
private static void runMonitored(com.lineage.server.clientpackets.C_SkillBuyOK.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.npc.UiOperationContext.Ticket, int[], com.lineage.server.model.Instance.L1PcInstance, java.util.ArrayList, boolean) throws java.lang.Exception
private static boolean lambda.start.0(com.lineage.server.model.npc.UiOperationContext.Ticket, int)
```

<a id="c-taxrate"></a>

### C_TaxRate

來源 `com/lineage/server/clientpackets/C_TaxRate.java`。

- 來源 SHA-256 `44e4581780d25dd6d22b46cbf0dbd3028f749fb3d244cdbea0b968e0ceb7451f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_TaxRate](server-protocol.md#c-taxrate)。
- class entry SHA-256 `41ea13752119b0720c9a2ebbf82d804db8c3b3b2318188085f2312fa9d1e016c`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_TaxRate()
private static void executeMonitored(com.lineage.server.clientpackets.C_TaxRate.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.2(com.lineage.server.model.npc.CastleTreasuryAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, int) throws java.lang.Exception
private static void lambda.start.1(com.lineage.server.model.npc.CastleTreasuryAccess, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.npc.UiOperationContext.Ticket, int)
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_TaxRate.MonitoredTask)
```

<a id="c-teleport"></a>

### C_Teleport

來源 `com/lineage/server/clientpackets/C_Teleport.java`。

- 來源 SHA-256 `0080261575be9445aedc3d98059937933a82ac4761fc2dbff79b87e499b5ef04`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Teleport](server-protocol.md#c-teleport)。
- class entry SHA-256 `145eca3132b00f1403a075e592dff478da7f1060cd35a92509beda05b731b52b`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Teleport()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.0(com.lineage.server.model.Instance.L1PcInstance)
```

<a id="c-title"></a>

### C_Title

來源 `com/lineage/server/clientpackets/C_Title.java`。

- 來源 SHA-256 `6ef78748cd0a63d1706eac0fa279c922c34cf19f1e5b66640d1cc9d6603d3b5b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Title](server-protocol.md#c-title)。
- class entry SHA-256 `2a3f175fefc384c86090f62708fa9955d60521a9f22c0b74d68f6fe6f26f3739`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Title()
public void start(byte[], com.lineage.echo.ClientExecutor)
private void changeTitle(com.lineage.server.model.Instance.L1PcInstance, java.lang.StringBuilder)
private boolean isClanLeader(com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
```

<a id="c-trade"></a>

### C_Trade

來源 `com/lineage/server/clientpackets/C_Trade.java`。

- 來源 SHA-256 `6466b1c760cbd96da6e1e417fd411557e2b3c781bfd7ff8408447822b4b529ab`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Trade](server-protocol.md#c-trade)。
- class entry SHA-256 `7ff525dbfedc40b0ff043abf89f7df386d18c079a435e61058d76b56cf56e5fe`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Trade()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-tradeadditem"></a>

### C_TradeAddItem

來源 `com/lineage/server/clientpackets/C_TradeAddItem.java`。

- 來源 SHA-256 `668c3c26f33fa0b99d2bafe6dec17cf7a2a1a5ca39cf07c2dc2a4a3039825e71`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_TradeAddItem](server-protocol.md#c-tradeadditem)。
- class entry SHA-256 `2b73d073bfd4888f99e3286e7d88e0ad444fc359fb34f1dc924003523a7f4877`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_TradeAddItem()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-tradecancel"></a>

### C_TradeCancel

來源 `com/lineage/server/clientpackets/C_TradeCancel.java`。

- 來源 SHA-256 `40c908a4a606c82917ee448e837abd8c0d55f06198e6b48bf42a2e77cee730a4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_TradeCancel](server-protocol.md#c-tradecancel)。
- class entry SHA-256 `9813577c096b8ab8a2e4219e5fa85bdb31f2a1b6a994d6b4768fb70dedb4c449`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_TradeCancel()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-tradeok"></a>

### C_TradeOK

來源 `com/lineage/server/clientpackets/C_TradeOK.java`。

- 來源 SHA-256 `af68f2ecc61d45b77b1579ad7325440f3d637cafd42a465bf6d22fdf74c5e749`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_TradeOK](server-protocol.md#c-tradeok)。
- class entry SHA-256 `21a326a49fad64abfc26a97bad60011c90c8d8092493126c3def06963fe0cb43`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_TradeOK()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-unkonwn"></a>

### C_Unkonwn

來源 `com/lineage/server/clientpackets/C_Unkonwn.java`。

- 來源 SHA-256 `4a2dc70d522b2cc5641703a7243737b52cb3159fbd227730c14d3792c9c9de82`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Unkonwn](server-protocol.md#c-unkonwn)。
- class entry SHA-256 `448943d1878145015be3ba4dbcda8c73b8b2e11edac64b187418e0a0c9175776`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Unkonwn()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
private final java.lang.String getNow_YMDHMS()
```

<a id="c-unlock"></a>

### C_UnLock

來源 `com/lineage/server/clientpackets/C_UnLock.java`。

- 來源 SHA-256 `996c01991ea075eb50ade8dcd1f416e4fed699c19c9c3c56a4bef5df103cf261`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_UnLock](server-protocol.md#c-unlock)。
- class entry SHA-256 `fca45a8e9c73d66a426f973abd3303aabb370ff8ebfafcb4bf58d7eab7852821`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_UnLock()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-usepetitem"></a>

### C_UsePetItem

來源 `com/lineage/server/clientpackets/C_UsePetItem.java`。

- 來源 SHA-256 `4f31c87a0ae456c320c12a99d67504bb44217d5c94c4d1bcfdc4f61f4c8cc1b1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_UsePetItem](server-protocol.md#c-usepetitem)。
- class entry SHA-256 `ff10832d4e81c6e258add7f0f459fb1dee57df3aa19db925b23237b8633bae59`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_UsePetItem()
private static void executeMonitored(com.lineage.server.clientpackets.C_UsePetItem.MonitoredTask)
public void start(byte[], com.lineage.echo.ClientExecutor)
private static boolean usePetWeapon(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetItem)
private static boolean usePetArmor(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetItem)
private static void setPetWeapon(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetItem)
private static void removePetWeapon(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetItem)
private static void setPetArmor(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetItem)
private static void removePetArmor(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1ItemInstance, com.lineage.server.templates.L1PetItem)
public static java.lang.String getMonitorLine()
public java.lang.String getType()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.1(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PetInstance, int, com.lineage.server.model.Instance.L1ItemInstance, int, int) throws java.lang.Exception
private static void lambda.executeMonitored.0(com.lineage.server.clientpackets.C_UsePetItem.MonitoredTask)
```

<a id="c-useskill"></a>

### C_UseSkill

來源 `com/lineage/server/clientpackets/C_UseSkill.java`。

- 來源 SHA-256 `4932c6dd71b330d91b75c4fcbf02c30ffa4712c35d7cb07537626159038e168e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_UseSkill](server-protocol.md#c-useskill)。
- class entry SHA-256 `3592ba46442ec134700d937a234599f37ae8bb7ae41d6759648901b776cc00ad`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_UseSkill()
public void start(byte[], com.lineage.echo.ClientExecutor)
private void performSkill(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.templates.L1Skills, int, int, int, int, boolean, java.lang.String, boolean, int, com.lineage.server.model.L1Object, boolean, com.lineage.server.model.L1Character, com.lineage.server.model.Instance.L1PcInstance)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
private static boolean isTeleportSkillId(int)
static boolean shouldResolveWorldObjectTarget(com.lineage.server.templates.L1Skills, int)
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.performSkill.1(boolean, com.lineage.server.model.Instance.L1PcInstance, java.lang.String, boolean, int, com.lineage.server.model.L1Character, com.lineage.server.model.Instance.L1PcInstance, int, int)
private void lambda.start.0(com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.templates.L1Skills, int, int, int, int, boolean, java.lang.String, boolean, int, com.lineage.server.model.L1Object, boolean, com.lineage.server.model.L1Character, com.lineage.server.model.Instance.L1PcInstance)
```

<a id="c-war"></a>

### C_War

來源 `com/lineage/server/clientpackets/C_War.java`。

- 來源 SHA-256 `733980808f998f818a52802b3cb8d36c6e556d7f115c2b84814040e43906fed0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_War](server-protocol.md#c-war)。
- class entry SHA-256 `80b08c6fb102f5fed638682cf54fb57b3f415dc4a66327333fe89373436a899c`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_War()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
private void war_castle(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan, com.lineage.server.model.L1Clan, int)
public java.lang.String getType()
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.war_castle.1(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Clan)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="c-webwindow"></a>

### C_WebWindow

來源 `com/lineage/server/clientpackets/C_WebWindow.java`。

- 來源 SHA-256 `205d0cb1c9a54c171279500e25d54903a05fca10d2cdf2a2bb212044ef0f8d93`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_WebWindow](server-protocol.md#c-webwindow)。
- class entry SHA-256 `34c9c9c286d713346f667ecfb1d261681f67e449b37c224b25dbb5821fcc5721`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_WebWindow()
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
```

<a id="c-who"></a>

### C_Who

來源 `com/lineage/server/clientpackets/C_Who.java`。

- 來源 SHA-256 `862ccf400889bba1d73d9ac956abd2eeffeb9db5ebf7bb807283b616aab575c0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Who](server-protocol.md#c-who)。
- class entry SHA-256 `cefe7fb9dc8b3ac1bcb80f0acf43558d220dbb4806eee4add1597bffde075b6e`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Who()
public void start(byte[], com.lineage.echo.ClientExecutor)
public static int deCount()
private com.lineage.server.model.Instance.L1DeInstance getDe(java.lang.String)
public java.lang.String getType()
```

<a id="c-windows"></a>

### C_Windows

來源 `com/lineage/server/clientpackets/C_Windows.java`。

- 來源 SHA-256 `2cb7e592dc2a449dc6421c2242d203ca11ca08ee63af8bb30e2d5cd323f299f0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [C_Windows](server-protocol.md#c-windows)。
- class entry SHA-256 `12a58987ce7f083c575bad43320f1f9dec14569d05943afc7443a3ac1adb1802`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.clientpackets.C_Windows()
private static void executeMonitored(java.lang.Runnable)
public void start(byte[], com.lineage.echo.ClientExecutor)
public java.lang.String getType()
static int classicInventoryField(byte[], int, int)
static int classicInventoryTarget(byte[], int)
static boolean classicInventoryTargetSuitable(com.lineage.server.templates.L1Item, com.lineage.server.model.Instance.L1ItemInstance)
private static void handleClassicInventory(byte[], com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, int)
public static java.lang.String getMonitorLine()
```

JAR 編譯器產生方法（已核對 ACC_SYNTHETIC）

```text
private static void lambda.start.2(com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.start.1(byte[], com.lineage.echo.ClientExecutor, com.lineage.server.model.Instance.L1PcInstance, int)
private static void lambda.executeMonitored.0(java.lang.Runnable)
```

<a id="s-ability"></a>

### S_Ability

來源 `com/lineage/server/serverpackets/S_Ability.java`。

- 來源 SHA-256 `1d7aeeb271e5f288b1fc6911e8895c7908d5048c7f317b971bcd7297d7fbaac2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Ability](server-protocol.md#s-ability)。
- class entry SHA-256 `7d436ea90d99d6db7c2ece3694c2b5dc882c7dcaaf42c9ca011f63ac8704636b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Ability(int, boolean)
private void buildPacket(int, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,boolean)` | 36 | 36 |

<a id="s-additem"></a>

### S_AddItem

來源 `com/lineage/server/serverpackets/S_AddItem.java`。

- 來源 SHA-256 `cde671d3b9de5f57bbd68bf5b2ef8d315bb196e49575d8fe8d4d966534d7dfa2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AddItem](server-protocol.md#s-additem)。
- class entry SHA-256 `c034106136f63cf4ccc7979cc6f5ba0d1f8dc7f6aacb13c7043d3e82e5e28d95`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_AddItem(com.lineage.server.model.Instance.L1ItemInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_AddItem(L1ItemInstance)` | 15 | 15 |

<a id="s-addskill"></a>

### S_AddSkill

來源 `com/lineage/server/serverpackets/S_AddSkill.java`。

- 來源 SHA-256 `13113864ecbe5af003216de30969abb8d1fc5515e961dbff32b2b24516fe0bdb`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AddSkill](server-protocol.md#s-addskill)。
- class entry SHA-256 `aeebde3caec2bc030491f4485934f08cb9745aa368c9db7cdd9bb9b277cb4a89`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_AddSkill(com.lineage.server.model.Instance.L1PcInstance, int)
public com.lineage.server.serverpackets.S_AddSkill(com.lineage.server.model.Instance.L1PcInstance, int[])
public com.lineage.server.serverpackets.S_AddSkill(com.lineage.server.model.Instance.L1PcInstance, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int)
public com.lineage.server.serverpackets.S_AddSkill(com.lineage.server.model.Instance.L1PcInstance, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_AddSkill(L1PcInstance,int)` | 164 | 164 |
| `public S_AddSkill(L1PcInstance,int[])` | 164 | 164 |
| `public S_AddSkill(L1PcInstance,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int)` | 164 | 164 |
| `public S_AddSkill(L1PcInstance,int,int)` | 164 | 164 |

<a id="s-applyauction"></a>

### S_ApplyAuction

來源 `com/lineage/server/serverpackets/S_ApplyAuction.java`。

- 來源 SHA-256 `0779eaa0b9f471808a941e39bb7a8bc4a56dbbb1fd942ce0fbb3eebbbe53a53f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ApplyAuction](server-protocol.md#s-applyauction)。
- class entry SHA-256 `b6a4f770ad95f63f39851838ecfe1bd78b4a988cf66b8c14e63149757397d08d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_ApplyAuction(int, java.lang.String)
private void buildPacket(int, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String)` | 136 | 136 |

<a id="s-attackpacketnpc"></a>

### S_AttackPacketNpc

來源 `com/lineage/server/serverpackets/S_AttackPacketNpc.java`。

- 來源 SHA-256 `aa612c0c22d3ca9dd6373ce170433f73cc8be3c1cb029edc8a1f7b53e656889d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AttackPacketNpc](server-protocol.md#s-attackpacketnpc)。
- class entry SHA-256 `ea3279ec3a491428344eeceab787e6d4150c792852f9cdaa38fdb79ec81ea89d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_AttackPacketNpc(com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.model.L1Character, int, int)
public com.lineage.server.serverpackets.S_AttackPacketNpc(com.lineage.server.model.Instance.L1NpcInstance, int, int, int)
public com.lineage.server.serverpackets.S_AttackPacketNpc(com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.model.L1Character, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_AttackPacketNpc(L1NpcInstance,L1Character,int,int)` | 30 | 30 |
| `public S_AttackPacketNpc(L1NpcInstance,int,int,int)` | 30 | 30 |
| `public S_AttackPacketNpc(L1NpcInstance,L1Character,int)` | 30 | 30 |

<a id="s-attackpacketpc"></a>

### S_AttackPacketPc

來源 `com/lineage/server/serverpackets/S_AttackPacketPc.java`。

- 來源 SHA-256 `8b1cf5a0e0cb00183dd2803b27161f6b8cc64a668b9a0ee761034e8d937bc3a1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AttackPacketPc](server-protocol.md#s-attackpacketpc)。
- class entry SHA-256 `17b131affbba01ada646a94c7f990c419a1b6cb3028d94c11a735c9c0cead5f8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_AttackPacketPc(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Character, int, int)
public com.lineage.server.serverpackets.S_AttackPacketPc(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Character)
public com.lineage.server.serverpackets.S_AttackPacketPc(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_AttackPacketPc(L1PcInstance,L1Character,int,int)` | 30 | 30 |
| `public S_AttackPacketPc(L1PcInstance,L1Character)` | 30 | 30 |
| `public S_AttackPacketPc(L1PcInstance)` | 30 | 30 |

<a id="s-auctionboard"></a>

### S_AuctionBoard

來源 `com/lineage/server/serverpackets/S_AuctionBoard.java`。

- 來源 SHA-256 `5f752fc7f7a91a8787125f56643a60f608152452f044c9cbeee752d48690c5ee`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AuctionBoard](server-protocol.md#s-auctionboard)。
- class entry SHA-256 `c587ff764ee095d8ab2ee33b0892571193c6545de9b936ddf4f009277cdb5e63`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_AuctionBoard(com.lineage.server.model.Instance.L1NpcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1NpcInstance)` | 156 | 156 |

<a id="s-auctionboardread"></a>

### S_AuctionBoardRead

來源 `com/lineage/server/serverpackets/S_AuctionBoardRead.java`。

- 來源 SHA-256 `0bc92c0a00302b8e5d4e67aac10a983fdeafec0952bef0688b502a2acb04da27`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AuctionBoardRead](server-protocol.md#s-auctionboardread)。
- class entry SHA-256 `f13c61e2fb9a22fcb4c150949f130af0896a18264e248434bde750dc31017e88`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_AuctionBoardRead(int, java.lang.String)
private void buildPacket(int, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String)` | 39 | 39 |

<a id="s-auctionshopretrieve"></a>

### S_AuctionShopRetrieve

來源 `com/lineage/server/serverpackets/S_AuctionShopRetrieve.java`。

- 來源 SHA-256 `826c8a79d252c470712e03de66c1937c233a606ac3fce66f779231befd324010`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AuctionShopRetrieve](server-protocol.md#s-auctionshopretrieve)。
- class entry SHA-256 `c058072fb061f376b78246d707ab011fd9290734bac15fd3111fcbfb41777fc4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_AuctionShopRetrieve(int, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_AuctionShopRetrieve(int,L1PcInstance)` | 176 | 176 |

<a id="s-auctionshopselllist"></a>

### S_AuctionShopSellList

來源 `com/lineage/server/serverpackets/S_AuctionShopSellList.java`。

- 來源 SHA-256 `5137ffcfeca894229fbbc9c350bca84089ce82a703129888a25ed09178c0efd6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_AuctionShopSellList](server-protocol.md#s-auctionshopselllist)。
- class entry SHA-256 `7420364a36b9c8b31f8e92e56fd850a62377e1e76052c4c536f6b9087595815d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_AuctionShopSellList(int, com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_AuctionShopSellList(int, java.util.List<com.lineage.server.model.AuctionShopItem>)
private void buildPacket(int, java.util.List<com.lineage.server.model.AuctionShopItem>)
public byte[] getContent()
private java.lang.String buildItemDisplayName(com.lineage.server.model.AuctionShopItem, com.lineage.server.templates.L1Item)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,List<AuctionShopItem>)` | 70 | 70 |

<a id="s-bluemessage"></a>

### S_BlueMessage

來源 `com/lineage/server/serverpackets/S_BlueMessage.java`。

- 來源 SHA-256 `9cc33bd5ff77514c36645b70bda07bc05925a3947d3a724ab5bd334f7002cf5b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_BlueMessage](server-protocol.md#s-bluemessage)。
- class entry SHA-256 `d3407c9e583eba418370e5a1cbcdfabd4664a9283df90bf5f4ad3fb1f0948b5a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_BlueMessage(int)
public com.lineage.server.serverpackets.S_BlueMessage(int, java.lang.String)
public com.lineage.server.serverpackets.S_BlueMessage(int, java.lang.String, java.lang.String)
public com.lineage.server.serverpackets.S_BlueMessage(int, java.lang.String[])
private void buildPacket(int, java.lang.String[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String[])` | 105 | 105 |

<a id="s-board"></a>

### S_Board

來源 `com/lineage/server/serverpackets/S_Board.java`。

- 來源 SHA-256 `d7e83c0bbf72d9084f267af9b22a47bc220db72d48e010708de4b58e21f42537`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Board](server-protocol.md#s-board)。
- class entry SHA-256 `5aa490323c56aa9aa01efc0d867eebdeba025b1c11821b016603743ae1ce0e50`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Board(com.lineage.server.model.Instance.L1NpcInstance)
public com.lineage.server.serverpackets.S_Board(com.lineage.server.model.Instance.L1NpcInstance, int)
private void buildPacket(com.lineage.server.model.Instance.L1NpcInstance, int)
public com.lineage.server.serverpackets.S_Board(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1NpcInstance,int)` | 68 | 68 |
| `public S_Board(int)` | 68 | 68 |

<a id="s-boardread"></a>

### S_BoardRead

來源 `com/lineage/server/serverpackets/S_BoardRead.java`。

- 來源 SHA-256 `2a8da353ad4f0aa287b131ca823006c97edc2bea7b1f61ab1a623f0746930490`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_BoardRead](server-protocol.md#s-boardread)。
- class entry SHA-256 `2ae220d27c2ff6aabaac940de313b6c7e3c813fc2fd9338c28dee295e94b3118`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_BoardRead(int)
private void buildPacket(int)
public com.lineage.server.serverpackets.S_BoardRead()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int)` | 148 | 148 |
| `public S_BoardRead()` | 148 | 148 |

<a id="s-bonusstats"></a>

### S_Bonusstats

來源 `com/lineage/server/serverpackets/S_Bonusstats.java`。

- 來源 SHA-256 `a6b745f35c42a048b5fc9928d3bac04779eb3d38c3bf30a87a5cb881108d15a4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Bonusstats](server-protocol.md#s-bonusstats)。
- class entry SHA-256 `81161711eb6683d9dfc4f41c422573b0f2628004b454e6ae823f93c962fa8173`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Bonusstats(int)
private void buildPacket(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int)` | 39 | 39 |

<a id="s-bookmarklist"></a>

### S_BookmarkList

來源 `com/lineage/server/serverpackets/S_BookmarkList.java`。

- 來源 SHA-256 `f2421a346fc320248c166d7e2342c0374037b6652174beacd4cd9839a4556c00`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_BookmarkList](server-protocol.md#s-bookmarklist)。
- class entry SHA-256 `c3e13f7ec0813bc9aa8edf77eb82975b6f345e067e35ac04a33196ca9474591c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_BookmarkList(java.util.List<com.lineage.server.templates.L1BookMark>)
public final byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_BookmarkList(List<L1BookMark>)` | 64 | 64 |

<a id="s-bookmarks"></a>

### S_Bookmarks

來源 `com/lineage/server/serverpackets/S_Bookmarks.java`。

- 來源 SHA-256 `749e8529cb083c13715ccf7e8858775ce647bd223e365692c899a296ac76b0e9`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Bookmarks](server-protocol.md#s-bookmarks)。
- class entry SHA-256 `3df5fbbb858cc41f4b72811f53a49183098699b0bd1d04ebb88ee63bba83fa01`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Bookmarks(java.lang.String, int, int, int, int)
public final byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Bookmarks(String,int,int,int,int)` | 92 | 92 |

<a id="s-boxchoice"></a>

### S_BoxChoice

來源 `com/lineage/server/serverpackets/S_BoxChoice.java`。

- 來源 SHA-256 `a49436ec84d5d797accfcbb4fc9433b39ac26d3da95258d7eaf7f2bb9bd7f2df`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_BoxChoice](server-protocol.md#s-boxchoice)。
- class entry SHA-256 `df61ebcce525c29f83a2d2e9966d33112f5f7fe6b9574afa41eb9c25d55f46ea`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_BoxChoice(int, com.lineage.server.datatables.ItemBoxTable.L1BoxSelect)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_BoxChoice(int,L1BoxSelect)` | 70 | 70 |

<a id="s-boxmessage"></a>

### S_BoxMessage

來源 `com/lineage/server/serverpackets/S_BoxMessage.java`。

- 來源 SHA-256 `49d4e3c56cb01e091a8f05450662dc613a3d7521a2631b9fe07439214561b742`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_BoxMessage](server-protocol.md#s-boxmessage)。
- class entry SHA-256 `2b4b7a0e60afeefb62a464e74406ca01b0e0acf28752e39c418c998e8be9ac3c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_BoxMessage(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_BoxMessage(String)` | 161 | 161 |

<a id="s-boxpack"></a>

### S_BoxPack

來源 `com/lineage/server/serverpackets/S_BoxPack.java`。

- 來源 SHA-256 `ce5efed8e93b46af4f2c27a25378538df2b36164f3b7f45e6f0b073cebb1a09c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_BoxPack](server-protocol.md#s-boxpack)。
- class entry SHA-256 `d744090d99a9e818dccfaf7d312a9d2fcc01d7e6a22125d0ee182dc9ee3a16d8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_BoxPack(com.lineage.server.model.Instance.L1NpcBoxInstance)
private void buildPacket(com.lineage.server.model.Instance.L1NpcBoxInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1NpcBoxInstance)` | 87 | 87 |

<a id="s-broadcastpos"></a>

### S_BroadcastPos

來源 `com/lineage/server/serverpackets/S_BroadcastPos.java`。

- 來源 SHA-256 `507d4bfa71ea4fa28c978bb9c2c5be8a5dd155284b2f5159fc71942bbe560c65`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_BroadcastPos](server-protocol.md#s-broadcastpos)。
- class entry SHA-256 `b5e523c3d7e5e38c1e0114b556e60e1996f3ded3956fc5abccb179dca9aa4af5`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_BroadcastPos(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_BroadcastPos(int)` | 250 | 250 |

<a id="s-buddy"></a>

### S_Buddy

來源 `com/lineage/server/serverpackets/S_Buddy.java`。

- 來源 SHA-256 `0fd54bd7ca3ec58459c28abb2a1d4d972b4c0c704725ebf1b8dfe2ebd6bfe4a9`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Buddy](server-protocol.md#s-buddy)。
- class entry SHA-256 `0bcee765fc12b0a77d5ee619401d35227faa6f83f7df3c911c22b60d8afa944a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Buddy(int)
private void buildPacket(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int)` | 39 | 39 |

<a id="s-castlemaster"></a>

### S_CastleMaster

來源 `com/lineage/server/serverpackets/S_CastleMaster.java`。

- 來源 SHA-256 `9232ef87beb2ee036e96ff76a63bcd03fa9aaff6242fb1e252955d2d361b8d00`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CastleMaster](server-protocol.md#s-castlemaster)。
- class entry SHA-256 `39710b793193128545c8fe7b4fbf7cd2a9508b9cc8a4180b0d3ebaf9020df6ec`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CastleMaster(int, int)
private void buildPacket(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int)` | 69 | 69 |

<a id="s-changeheading"></a>

### S_ChangeHeading

來源 `com/lineage/server/serverpackets/S_ChangeHeading.java`。

- 來源 SHA-256 `dc8eeae09bec67d5bb0daad7e658a87390718f8e0a3362480d633e80f749e015`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChangeHeading](server-protocol.md#s-changeheading)。
- class entry SHA-256 `11f7fe274cba2d1e87da37294f4a9bee99256610e9b8aa419e2abc8ffab86366`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChangeHeading(com.lineage.server.model.L1Character)
private void buildPacket(com.lineage.server.model.L1Character)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1Character)` | 122 | 122 |

<a id="s-changename"></a>

### S_ChangeName

來源 `com/lineage/server/serverpackets/S_ChangeName.java`。

- 來源 SHA-256 `50c9e9be95a07eab1798fe72df73a90ace88ed96b4e7bcacbb0c408e7ed6059b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChangeName](server-protocol.md#s-changename)。
- class entry SHA-256 `49b27d6bb82e7ab321c7877f7f82962b5cedea07381bec1cacedb773f028c0c1`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChangeName(int, java.lang.String)
public com.lineage.server.serverpackets.S_ChangeName(int, java.lang.String, int)
public com.lineage.server.serverpackets.S_ChangeName(com.lineage.server.model.Instance.L1PcInstance, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ChangeName(int,String)` | 46 | 46 |
| `public S_ChangeName(int,String,int)` | 46 | 46 |
| `public S_ChangeName(L1PcInstance,boolean)` | 46 | 46 |

<a id="s-changeshape"></a>

### S_ChangeShape

來源 `com/lineage/server/serverpackets/S_ChangeShape.java`。

- 來源 SHA-256 `b23ec55370d70c3536d1d1aaa0b73921b9c1a83b533bcdbc695e32cb9846794d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChangeShape](server-protocol.md#s-changeshape)。
- class entry SHA-256 `49122d05169183862df868b48fa5cbeb22d36edd2df6469abc7a157670de33ae`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChangeShape(com.lineage.server.model.L1Character, int)
public com.lineage.server.serverpackets.S_ChangeShape(com.lineage.server.model.L1Character)
public com.lineage.server.serverpackets.S_ChangeShape(com.lineage.server.model.L1Character, int, boolean)
private void buildPacket(com.lineage.server.model.L1Character, int, boolean)
public com.lineage.server.serverpackets.S_ChangeShape(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, int)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1Character,int,boolean)` | 76 | 76 |
| `public S_ChangeShape(L1PcInstance,L1NpcInstance,int)` | 76 | 76 |

<a id="s-charamount"></a>

### S_CharAmount

來源 `com/lineage/server/serverpackets/S_CharAmount.java`。

- 來源 SHA-256 `5d1aba56407fad1d758ed9fa240be6d44ef5c2ad4d70450bfdad00142d7f97a8`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CharAmount](server-protocol.md#s-charamount)。
- class entry SHA-256 `5957e1a5e853b2237475dc556603bc44578f253451d8ff6ba739335e4bed0eeb`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CharAmount(int, com.lineage.echo.ClientExecutor)
private void buildPacket(int, com.lineage.echo.ClientExecutor)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,ClientExecutor)` | 178 | 178 |

<a id="s-charcreatestatus"></a>

### S_CharCreateStatus

來源 `com/lineage/server/serverpackets/S_CharCreateStatus.java`。

- 來源 SHA-256 `6a666e05144f0506857fd485bf0e98ef67aa2f1edd4cd15b00dd3895e7b7b182`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CharCreateStatus](server-protocol.md#s-charcreatestatus)。
- class entry SHA-256 `4a6a22a7a539bef1c8956aff32bb8d9e94a04c2a4fc1d5072bedf1377ce145bb`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CharCreateStatus(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CharCreateStatus(int)` | 98 | 98 |

<a id="s-charpacks"></a>

### S_CharPacks

來源 `com/lineage/server/serverpackets/S_CharPacks.java`。

- 來源 SHA-256 `25020fc64589a38f32ad63b6c75b07d2dd360a76bdcca14a0b0f465ed8555f99`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CharPacks](server-protocol.md#s-charpacks)。
- class entry SHA-256 `f43b5d28aee68a405776f6072e5226fa16a86f3f0c340d554cbd7d6f35d83caf`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CharPacks(java.lang.String, java.lang.String, int, int, int, int, int, int, int, int, int, int, int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CharPacks(String,String,int,int,int,int,int,int,int,int,int,int,int,int,int,int)` | 93 | 93 |

<a id="s-charreset"></a>

### S_CharReset

來源 `com/lineage/server/serverpackets/S_CharReset.java`。

- 來源 SHA-256 `c1ae26bf59405be0d11ae4b2fae2ce63d28281ead804705f2effb8acbd4e3138`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CharReset](server-protocol.md#s-charreset)。
- class entry SHA-256 `02a23d0db730abaf1647aa8632239734aa7371347357bd79aa588c745c669b7c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CharReset(com.lineage.server.model.Instance.L1PcInstance, int, int, int, int, int, int, int, int, int, int)
public com.lineage.server.serverpackets.S_CharReset(int)
public com.lineage.server.serverpackets.S_CharReset(int, int)
public com.lineage.server.serverpackets.S_CharReset(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CharReset(L1PcInstance,int,int,int,int,int,int,int,int,int,int)` | 64 | 64 |
| `public S_CharReset(int)` | 64 | 64 |
| `public S_CharReset(int,int)` | 64 | 64 |
| `public S_CharReset(L1PcInstance)` | 64 | 64 |

<a id="s-charresetinfo"></a>

### S_CharResetInfo

來源 `com/lineage/server/serverpackets/S_CharResetInfo.java`。

- 來源 SHA-256 `76d3477398e1606db6dfc050df35d9a91d0a248e90b8a81bf15b31a493aa66f8`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CharResetInfo](server-protocol.md#s-charresetinfo)。
- class entry SHA-256 `6e5633970c4f35d5a273afba2e45d97015af21203bc763121bb45b059460525c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CharResetInfo(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CharResetInfo(L1PcInstance)` | 64 | 64 |

<a id="s-chartitle"></a>

### S_CharTitle

來源 `com/lineage/server/serverpackets/S_CharTitle.java`。

- 來源 SHA-256 `16ce2bc261cb0d9a4ae0ad5550172e483fa67fc09ab480d376f6f14e4df4e6c7`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CharTitle](server-protocol.md#s-chartitle)。
- class entry SHA-256 `a79e49206f49d32ce422a19369f62ab1c6b214dfd80397ab70e4faecf893fcc1`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CharTitle(int, java.lang.StringBuilder)
public com.lineage.server.serverpackets.S_CharTitle(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CharTitle(int,StringBuilder)` | 183 | 183 |
| `public S_CharTitle(int)` | 183 | 183 |

<a id="s-charvisualupdate"></a>

### S_CharVisualUpdate

來源 `com/lineage/server/serverpackets/S_CharVisualUpdate.java`。

- 來源 SHA-256 `4eab821186c8820b47a731ccd43a0e6481c334d1d537030cb73542cdbf97b8ee`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CharVisualUpdate](server-protocol.md#s-charvisualupdate)。
- class entry SHA-256 `4991209309da8765159d34d734fe6ba39a1035f10ea54084663b2e29266c528d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CharVisualUpdate(int, int)
public com.lineage.server.serverpackets.S_CharVisualUpdate(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CharVisualUpdate(int,int)` | 119 | 119 |
| `public S_CharVisualUpdate(L1PcInstance)` | 119 | 119 |

<a id="s-chat"></a>

### S_Chat

來源 `com/lineage/server/serverpackets/S_Chat.java`。

- 來源 SHA-256 `f1aa6a354745fe0f29b4d9154ea2c0b757e3aa264f943d1993d23ee4e6006f6b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Chat](server-protocol.md#s-chat)。
- class entry SHA-256 `38cde88e93429a34a9e0aa746c093bd5650e9580fbad8e7068b8018d16d25753`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Chat(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_Chat(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_Chat(com.lineage.server.model.L1Object, java.lang.String, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 81 | 81 |
| `public S_Chat(L1NpcInstance,String)` | 81 | 81 |
| `public S_Chat(L1Object,String,int)` | 81 | 81 |

<a id="s-chatclan"></a>

### S_ChatClan

來源 `com/lineage/server/serverpackets/S_ChatClan.java`。

- 來源 SHA-256 `bbda70430611bf6aa871bd55974d0a771401374014f5f7268980a3a151fe6b27`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatClan](server-protocol.md#s-chatclan)。
- class entry SHA-256 `2d6b76b0a3c90264292d75a9e4c1590403390282075715f5ce2b5b15b7ede5e4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatClan(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 243 | 243 |

<a id="s-chatclanunion"></a>

### S_ChatClanUnion

來源 `com/lineage/server/serverpackets/S_ChatClanUnion.java`。

- 來源 SHA-256 `0004c8b025f3d80b12e1bfa5e3e883071534e63a5d0466c95ebbe16fd61c35ef`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatClanUnion](server-protocol.md#s-chatclanunion)。
- class entry SHA-256 `e743ffe53df08b5182e3cccfde409a8880c680d2eba90e0904ad54d150186642`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatClanUnion(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 243 | 243 |

<a id="s-chatglobal"></a>

### S_ChatGlobal

來源 `com/lineage/server/serverpackets/S_ChatGlobal.java`。

- 來源 SHA-256 `f58aafed8c566655ed5cee1e45c44376b90e9104ba2993c32ee1a8012119fe1d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatGlobal](server-protocol.md#s-chatglobal)。
- class entry SHA-256 `d6095dddabaf0fb8afe78d3d1b4505989f1c67d57f2d2e2cf240835970746338`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatGlobal(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_ChatGlobal(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_ChatGlobal(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 243 | 243 |
| `public S_ChatGlobal(L1NpcInstance,String)` | 243 | 243 |
| `public S_ChatGlobal(String)` | 243 | 243 |

<a id="s-chatout"></a>

### S_ChatOut

來源 `com/lineage/server/serverpackets/S_ChatOut.java`。

- 來源 SHA-256 `1ca7b0ca493e7b010a745d5333bd2dd383fa053c380269cd6d6a9869ef65ad26`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatOut](server-protocol.md#s-chatout)。
- class entry SHA-256 `ac045951e8c01284ea437f5ca298d2369a2a40a2eda118afa5cfac3702bf3b46`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatOut(int)
public com.lineage.server.serverpackets.S_ChatOut(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(int)
public byte[] getContent()
public java.lang.String getType()
```

<a id="s-chatparty"></a>

### S_ChatParty

來源 `com/lineage/server/serverpackets/S_ChatParty.java`。

- 來源 SHA-256 `4f9aaf73fd2bfafd6ded24a3e4fbe74ada2468dfff554a6e4f7d01be40b2c605`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatParty](server-protocol.md#s-chatparty)。
- class entry SHA-256 `f249a039b42341d83d770c87c87d7d2cad00f1b88feec40c4ec775fa8c6d4244`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatParty(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 243 | 243 |

<a id="s-chatparty2"></a>

### S_ChatParty2

來源 `com/lineage/server/serverpackets/S_ChatParty2.java`。

- 來源 SHA-256 `13d26f80e88477e267ce0321618799d455534c6894098b9c3457b2847e718854`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatParty2](server-protocol.md#s-chatparty2)。
- class entry SHA-256 `221c8b41447d366b4c67b17cd17cb10a9bb6e2f5c3df63cf278f21aeaca4fac8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatParty2(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 81 | 81 |

<a id="s-chatshouting"></a>

### S_ChatShouting

來源 `com/lineage/server/serverpackets/S_ChatShouting.java`。

- 來源 SHA-256 `d08467b5b3fb66d2415c4e97a274e62525eacc6eca817d85f7953a5f21a75906`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatShouting](server-protocol.md#s-chatshouting)。
- class entry SHA-256 `af56cebeca878c03126bc90c099ca531241e786793bc13dabef4f5b2850029f3`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatShouting(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_ChatShouting(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 81 | 81 |
| `public S_ChatShouting(L1NpcInstance,String)` | 81 | 81 |

<a id="s-chattransaction"></a>

### S_ChatTransaction

來源 `com/lineage/server/serverpackets/S_ChatTransaction.java`。

- 來源 SHA-256 `751477efe4967ccfec4e5cf984e923cb3b82a954eb4b8208ec6592422694e6be`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatTransaction](server-protocol.md#s-chattransaction)。
- class entry SHA-256 `45e5da1388ddcf9eecab027ced8221483a7a509bf11e8cc17c821a8b62554901`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatTransaction(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_ChatTransaction(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 243 | 243 |
| `public S_ChatTransaction(L1NpcInstance,String)` | 243 | 243 |

<a id="s-chatwhisperfrom"></a>

### S_ChatWhisperFrom

來源 `com/lineage/server/serverpackets/S_ChatWhisperFrom.java`。

- 來源 SHA-256 `74d33c10375d463ac288d15c1f09786fdf1b1c855b5e5c11e0baff3ccd97f9aa`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatWhisperFrom](server-protocol.md#s-chatwhisperfrom)。
- class entry SHA-256 `15e3e2b22f06218461332bd155d1f3bfd908a5a383ffb506425a3d95183b8942`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatWhisperFrom(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 73 | 73 |

<a id="s-chatwhisperto"></a>

### S_ChatWhisperTo

來源 `com/lineage/server/serverpackets/S_ChatWhisperTo.java`。

- 來源 SHA-256 `b8531703eaedbca5bfc103577fb5e2b20493bfbbde5d9c68886e9855782f563e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ChatWhisperTo](server-protocol.md#s-chatwhisperto)。
- class entry SHA-256 `cd5223fb172e88e6e48d2df4cdc85152835d9fba0a3b75b6f748729c8472210d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ChatWhisperTo(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_ChatWhisperTo(com.lineage.server.model.Instance.L1DeInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,String)` | 243 | 243 |
| `public S_ChatWhisperTo(L1DeInstance,String)` | 243 | 243 |

<a id="s-clanattention"></a>

### S_ClanAttention

來源 `com/lineage/server/serverpackets/S_ClanAttention.java`。

- 來源 SHA-256 `e8322f46f48943591cd5060d1f51ca868abf680e606b1596b15284515439f35e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ClanAttention](server-protocol.md#s-clanattention)。
- class entry SHA-256 `bcb21a8647299b407bf72fad2a67ca43ab673549bdbb453fd21727088da954dd`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ClanAttention(com.lineage.server.model.L1Clan, int)
public com.lineage.server.serverpackets.S_ClanAttention()
public com.lineage.server.serverpackets.S_ClanAttention(java.util.List<java.lang.String>)
private void buildPacket(com.lineage.server.model.L1Clan, int)
public byte[] getContent() throws java.io.IOException
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClanAttention()` | 200 | 200 |
| `public S_ClanAttention(List<String>)` | 200 | 200 |
| `private void buildPacket(L1Clan,int)` | 200 | 200 |

<a id="s-clanupdate"></a>

### S_ClanUpdate

來源 `com/lineage/server/serverpackets/S_ClanUpdate.java`。

- 來源 SHA-256 `73518ee411eeea3e780892bbae64d39942bedea677e57d529d8586d4a5c9499a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ClanUpdate](server-protocol.md#s-clanupdate)。
- class entry SHA-256 `916b0c029bd2e9ab2c463b336fb0915a90fb37ac1285288c049680596e213b1a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ClanUpdate(int, int, java.lang.String, int)
public com.lineage.server.serverpackets.S_ClanUpdate(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClanUpdate(int,int,String,int)` | 72 | 72 |
| `public S_ClanUpdate(int)` | 72 | 72 |

<a id="s-classicbadge"></a>

### S_ClassicBadge

來源 `com/lineage/server/serverpackets/S_ClassicBadge.java`。

- 來源 SHA-256 `8340d1ea801bf704717ccc317a846f719333705333244cad24ada03b4cb04ae9`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicBadge](server-protocol.md#s-classicbadge)。
來源獨有宣告方法

```text
private com.lineage.server.serverpackets.S_ClassicBadge(int,int,int)
public byte[] getContent()
public com.lineage.server.serverpackets.S_ClassicBadge(int,int)
public java.lang.String getType()
public static com.lineage.server.serverpackets.S_ClassicBadge reset()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private S_ClassicBadge(int,int,int)` | 250 | 缺席 |

<a id="s-classicbagcombine"></a>

### S_ClassicBagCombine

來源 `com/lineage/server/serverpackets/S_ClassicBagCombine.java`。

- 來源 SHA-256 `b63ff41e7e06a65748b8ea67f3c9a1d97324b0fe3ed7e2d8f47f7a5364f61433`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicBagCombine](server-protocol.md#s-classicbagcombine)。
來源獨有宣告方法

```text
public byte[] getContent()
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_ClassicBagCombine(int,int,int,int,com.lineage.server.model.npc.UiOperationContext.Offer)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicBagCombine(int,int,int,int,UiOperationContext.Offer)` | 250 | 缺席 |

<a id="s-classicboss"></a>

### S_ClassicBoss

來源 `com/lineage/server/serverpackets/S_ClassicBoss.java`。

- 來源 SHA-256 `490f4243dc4542fdcb8e812cbccc8e456de47a88e35854a0ec3688bd2df166ab`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicBoss](server-protocol.md#s-classicboss)。
來源獨有宣告方法

```text
private void text(java.lang.String,int)
public byte[] getContent()
public com.lineage.server.serverpackets.S_ClassicBoss(int,int,int,int,int,int,int,int,java.util.List<com.lineage.server.serverpackets.S_ClassicBoss.Row>)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicBoss(int,int,int,int,int,int,int,int,List<Row>)` | 250 | 缺席 |
| `private void text(String,int)` | 動態 | 缺席 |

<a id="s-classiccollection"></a>

### S_ClassicCollection

來源 `com/lineage/server/serverpackets/S_ClassicCollection.java`。

- 來源 SHA-256 `da08bca686dc0abf76bb64198ddc0ba02874266239b55ad47ba6c21061143117`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicCollection](server-protocol.md#s-classiccollection)。
來源獨有宣告方法

```text
private void text(java.lang.String,int)
public byte[] getContent()
public com.lineage.server.serverpackets.S_ClassicCollection(int,int,int,int,int,int,int,int,int,int,int,int,int,java.util.List<com.lineage.server.serverpackets.S_ClassicCollection.Row>,java.util.List<java.lang.String>,java.util.List<com.lineage.server.serverpackets.S_ClassicCollection.Material>)
public java.lang.String getType()
public java.util.List<com.lineage.server.serverpackets.ServerBasePacket> fragments()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicCollection(int,int,int,int,int,int,int,int,int,int,int,int,int,List<Row>,List<String>,List<Material>)` | 250 | 缺席 |
| `private void text(String,int)` | 動態 | 缺席 |

#132 同節另列內部封包建構子 `S_ClassicCollection.Chunk(byte[],int,int)`，該內部 class entry 亦缺席。

<a id="s-classiccraft"></a>

### S_ClassicCraft

來源 `com/lineage/server/serverpackets/S_ClassicCraft.java`。

- 來源 SHA-256 `8cbcbcde95916a8da126e29823b049081b65d44f63523b4d97fe4a2136362b67`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicCraft](server-protocol.md#s-classiccraft)。
來源獨有宣告方法

```text
private void header(int,int)
private void item(com.lineage.server.model.npc.action.ClassicCraftService.Item)
private void text(java.lang.String)
public byte[] getContent()
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public com.lineage.server.serverpackets.S_ClassicCraft(com.lineage.server.model.npc.action.ClassicCraftService.Preview,com.lineage.server.model.npc.UiOperationContext.Ticket)
public com.lineage.server.serverpackets.S_ClassicCraft(int,int,int)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void header(int,int)` | 250 | 缺席 |
| `private void text(String)` | 動態 | 缺席 |
| `public S_ClassicCraft(Preview,UiOperationContext.Ticket)` | 動態 | 缺席 |

<a id="s-classicinventoryfilter"></a>

### S_ClassicInventoryFilter

來源 `com/lineage/server/serverpackets/S_ClassicInventoryFilter.java`。

- 來源 SHA-256 `6f01edc02158f1724e7a007f6dc01ac29536a17c500c267568711f00c7ae9fe3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ClassicInventoryFilter](server-protocol.md#s-classicinventoryfilter)。
- class entry SHA-256 `da236869cdd0313967805d01a1938f99df3189c1665f0b243f5dd8fa1015bdce`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ClassicInventoryFilter(int, int, java.util.List<java.lang.Integer>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicInventoryFilter(int,int,List<Integer>)` | 250 | 250 |

<a id="s-classicpass"></a>

### S_ClassicPass

來源 `com/lineage/server/serverpackets/S_ClassicPass.java`。

- 來源 SHA-256 `a62d21c5055a79de684a307c451df65b493d22cf0a7a5ffafc53b454d67043db`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicPass](server-protocol.md#s-classicpass)。
來源獨有宣告方法

```text
private void item(com.lineage.server.serverpackets.S_ClassicPass.Item)
private void text(java.lang.String)
public byte[] getContent()
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public com.lineage.server.serverpackets.S_ClassicPass(int,int,int,int,boolean,int,int,java.lang.String,java.util.List<com.lineage.server.serverpackets.S_ClassicPass.Row>)
public com.lineage.server.serverpackets.S_ClassicPass(int,int,int,int,boolean,int,int,java.lang.String,java.util.List<com.lineage.server.serverpackets.S_ClassicPass.Row>,com.lineage.server.model.npc.UiOperationContext.Offer,com.lineage.server.model.npc.UiOperationContext.Ticket)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicPass(int,int,int,int,boolean,int,int,String,List<Row>,UiOperationContext.Offer,UiOperationContext.Ticket)` | 250 | 缺席 |
| `private void item(Item)` | 動態 | 缺席 |
| `private void text(String)` | 動態 | 缺席 |

<a id="s-classicquest"></a>

### S_ClassicQuest

來源 `com/lineage/server/serverpackets/S_ClassicQuest.java`。

- 來源 SHA-256 `6ebeb0eeec7fb78e7fe0f20a404ad0d6430797c63a54ff9881c0311e6764d2c3`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicQuest](server-protocol.md#s-classicquest)。
來源獨有宣告方法

```text
private void text(java.lang.String)
public byte[] getContent()
public com.lineage.server.serverpackets.S_ClassicQuest(int,int,int,int,int,int,int,int,int,int,int,java.util.List<com.lineage.server.serverpackets.S_ClassicQuest.Row>,java.util.List<com.lineage.server.serverpackets.S_ClassicQuest.Reward>,java.util.List<java.lang.String>)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicQuest(int,int,int,int,int,int,int,int,int,int,int,List<Row>,List<Reward>,List<String>)` | 250 | 缺席 |
| `private void text(String)` | 動態 | 缺席 |

<a id="s-classicranking"></a>

### S_ClassicRanking

來源 `com/lineage/server/serverpackets/S_ClassicRanking.java`。

- 來源 SHA-256 `75266cba41fa1c665280564b4bc0f9ad1c886f96a5ce14810c5478beb731c5ff`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicRanking](server-protocol.md#s-classicranking)。
來源獨有宣告方法

```text
private void text(java.lang.String,int,boolean)
public byte[] getContent()
public com.lineage.server.serverpackets.S_ClassicRanking(int,int,int,int,int,int,int,int,int,java.util.List<com.lineage.server.model.ClassicRankingModel.Row>)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicRanking(int,int,int,int,int,int,int,int,int,List<ClassicRankingModel.Row>)` | 250 | 缺席 |
| `private void text(String,int,boolean)` | 動態 | 缺席 |

<a id="s-classicreward"></a>

### S_ClassicReward

來源 `com/lineage/server/serverpackets/S_ClassicReward.java`。

- 來源 SHA-256 `3140adb09abdbd783abbedc2c2edca10c063df1379ab4e607b029c785d122ec9`，與 #132 相符。
- 核對結果為JAR 缺少類別。#132 索引連結見 [S_ClassicReward](server-protocol.md#s-classicreward)。
來源獨有宣告方法

```text
public byte[] getContent()
public com.lineage.server.serverpackets.S_ClassicReward(int,int,int,int,int,int,int,int,int,int,java.util.List<com.lineage.server.model.ClassicRewardProtocol.Row>)
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ClassicReward(int,int,int,int,int,int,int,int,int,int,List<ClassicRewardProtocol.Row>)` | 250 | 缺席 |

<a id="s-closelist"></a>

### S_CloseList

來源 `com/lineage/server/serverpackets/S_CloseList.java`。

- 來源 SHA-256 `b9198c23d996b8289592fc0faae17af27bb44d410736ab1fb067d1bda4b0afe4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CloseList](server-protocol.md#s-closelist)。
- class entry SHA-256 `288525ca7d60851fe24b7f8e9944ffc0be76e07ca67ec22b94cf616abfc5ba07`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_CloseList(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CloseList(int)` | 39 | 39 |

<a id="s-commoninfo"></a>

### S_CommonInfo

來源 `com/lineage/server/serverpackets/S_CommonInfo.java`。

- 來源 SHA-256 `4cbde22e296525f4eea0ae77d851d94f87fea919e83e9836f62b165f3691d10d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CommonInfo](server-protocol.md#s-commoninfo)。
- class entry SHA-256 `132ba259a180d5e23547a7deabe9b04e4f16fbbf32e660f4fca1cb58c7d01a41`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CommonInfo(int, java.lang.String[])
private void buildPacket(int, java.lang.String[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String[])` | 48 | 48 |

<a id="s-commonnews"></a>

### S_CommonNews

來源 `com/lineage/server/serverpackets/S_CommonNews.java`。

- 來源 SHA-256 `3761de2cee68f46dbd351c264fd58eee9510ec978cf87cc11c275acefc3b8434`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CommonNews](server-protocol.md#s-commonnews)。
- class entry SHA-256 `ac035576d36c2607ad803664f48ef6adf2a878e46341146e7444569aa856571f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CommonNews()
public com.lineage.server.serverpackets.S_CommonNews(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_CommonNews()` | 48 | 48 |
| `public S_CommonNews(String)` | 48 | 48 |

<a id="s-curseblind"></a>

### S_CurseBlind

來源 `com/lineage/server/serverpackets/S_CurseBlind.java`。

- 來源 SHA-256 `cf6288f4245c94b02cbc91cf420fdf5e7a8bd9794b77acbbc30e05bea25947bf`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_CurseBlind](server-protocol.md#s-curseblind)。
- class entry SHA-256 `04c1fead833daea1a5330bc00f8412d2cfbd9e98bc48975aab03d09f9bb4d0f8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_CurseBlind(int)
private void buildPacket(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int)` | 47 | 47 |

<a id="s-deletecharok"></a>

### S_DeleteCharOK

來源 `com/lineage/server/serverpackets/S_DeleteCharOK.java`。

- 來源 SHA-256 `52405c3aeb5e1ef9a2d2a0e38d3cdf09694e4ec74a68183e88490fe8512dbb2a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_DeleteCharOK](server-protocol.md#s-deletecharok)。
- class entry SHA-256 `80998949ed7685a85be477e34cad06f46adeda7d765627f98b835d069cf5f0f1`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_DeleteCharOK(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_DeleteCharOK(int)` | 6 | 6 |

<a id="s-deleteinventoryitem"></a>

### S_DeleteInventoryItem

來源 `com/lineage/server/serverpackets/S_DeleteInventoryItem.java`。

- 來源 SHA-256 `53f78032ca8bbc25a32e94b31f0914b356c556a9ed2abd5a93cbea3b5daffb93`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_DeleteInventoryItem](server-protocol.md#s-deleteinventoryitem)。
- class entry SHA-256 `57f802a1e9f879b0b6343ab43fd26bfe68cc8d699a5043cb79e6d5e450b11a95`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_DeleteInventoryItem(com.lineage.server.model.Instance.L1ItemInstance)
public com.lineage.server.serverpackets.S_DeleteInventoryItem(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_DeleteInventoryItem(L1ItemInstance)` | 57 | 57 |
| `public S_DeleteInventoryItem(int)` | 57 | 57 |

<a id="s-delskill"></a>

### S_DelSkill

來源 `com/lineage/server/serverpackets/S_DelSkill.java`。

- 來源 SHA-256 `4a252f732bdfddb0040b5d14c8f3a047e50a3e9868a09c17b91edd8e68d63edf`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_DelSkill](server-protocol.md#s-delskill)。
- class entry SHA-256 `af1c5ae63f1592bca225e1005a21d661c9ded819243dd47e475667b717343db9`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_DelSkill(com.lineage.server.model.Instance.L1PcInstance, int)
public com.lineage.server.serverpackets.S_DelSkill(com.lineage.server.model.Instance.L1PcInstance, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_DelSkill(L1PcInstance,int)` | 160 | 160 |
| `public S_DelSkill(L1PcInstance,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int,int)` | 160 | 160 |

<a id="s-deposit"></a>

### S_Deposit

來源 `com/lineage/server/serverpackets/S_Deposit.java`。

- 來源 SHA-256 `768e007c1f4041a1f33a58bd47e6fbeec8ae38be6c92343d8182b4c28f262f97`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Deposit](server-protocol.md#s-deposit)。
- class entry SHA-256 `1c9552400266372c19d741cb828641041f5ab4f842b6cc536c8b8d578fe5b209`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Deposit(int)
public com.lineage.server.serverpackets.S_Deposit(int, com.lineage.server.model.npc.UiOperationContext.Offer, com.lineage.server.model.npc.UiOperationContext.Ticket)
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Deposit(int,Offer,Ticket)` | 4 | 4 |

<a id="s-dexup"></a>

### S_Dexup

來源 `com/lineage/server/serverpackets/S_Dexup.java`。

- 來源 SHA-256 `ef0a5702fb527ca4d71c72a83d5405f2083cff917993903b1a5fb02f900a18c6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Dexup](server-protocol.md#s-dexup)。
- class entry SHA-256 `ed05681e5a6d64ed304d87d882dff069a95cbf9b19b4f0bf3a9d25d1556accb1`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Dexup(com.lineage.server.model.Instance.L1PcInstance, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Dexup(L1PcInstance,int,int)` | 188 | 188 |

<a id="s-disconnect"></a>

### S_Disconnect

來源 `com/lineage/server/serverpackets/S_Disconnect.java`。

- 來源 SHA-256 `752eb3a4ee13049502ee86bf4b0251ca52b7cdb2732b44b941ee10e5c4dd6dcb`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Disconnect](server-protocol.md#s-disconnect)。
- class entry SHA-256 `9592313ac6c7023c37633f397a459a06dea7eacf5d93732fd0ba7c2a05b9926f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Disconnect()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Disconnect()` | 227 | 227 |

<a id="s-doactiongfx"></a>

### S_DoActionGFX

來源 `com/lineage/server/serverpackets/S_DoActionGFX.java`。

- 來源 SHA-256 `7d71e3bd0d6fa265058cd78223148e54e45106724c4eeb268f62fd90b136b6ba`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_DoActionGFX](server-protocol.md#s-doactiongfx)。
- class entry SHA-256 `dbaa27bdc71cf28e626a6062e537b5ede07d0fda76409777f2d3b3c62b34d11e`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_DoActionGFX(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_DoActionGFX(int,int)` | 158 | 158 |

<a id="s-doactionshop"></a>

### S_DoActionShop

來源 `com/lineage/server/serverpackets/S_DoActionShop.java`。

- 來源 SHA-256 `8ccc99bdb68203f49936775b60f3ff2456413ecaa1cc570d9e8742e0592e30fc`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_DoActionShop](server-protocol.md#s-doactionshop)。
- class entry SHA-256 `a0a7f3a98b918e0f2b47099dac0e20d930b0353c8809c1d149a0bcf75abe1d61`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_DoActionShop(int, byte[])
public com.lineage.server.serverpackets.S_DoActionShop(int, java.lang.String)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_DoActionShop(int,byte[])` | 158 | 158 |
| `public S_DoActionShop(int,String)` | 158 | 158 |

<a id="s-door"></a>

### S_Door

來源 `com/lineage/server/serverpackets/S_Door.java`。

- 來源 SHA-256 `9533efa6d0724e7ea307e9e78e16ff335db5da315df2bb67b32e57cbaa703fa3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Door](server-protocol.md#s-door)。
- class entry SHA-256 `cffe1a6e2177f844ade995c4634cc66509e7da974c4d82502223fc48ef9475d7`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Door(com.lineage.server.model.Instance.L1DoorInstance)
public com.lineage.server.serverpackets.S_Door(int, int, int, int)
private void buildPacket(int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int,int,int)` | 209 | 209 |

<a id="s-doorpack"></a>

### S_DoorPack

來源 `com/lineage/server/serverpackets/S_DoorPack.java`。

- 來源 SHA-256 `e1e26b0461705ece45a2d6c8d326d7023418a43fbd37b7048527de419bc37a48`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_DoorPack](server-protocol.md#s-doorpack)。
- class entry SHA-256 `19c1dcc593ec3c98c3624badb4f2e6da78aac41ea82b5befa69f1779f4de9b2d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_DoorPack(com.lineage.server.model.Instance.L1DoorInstance)
private void buildPacket(com.lineage.server.model.Instance.L1DoorInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1DoorInstance)` | 87 | 87 |

<a id="s-drawal"></a>

### S_Drawal

來源 `com/lineage/server/serverpackets/S_Drawal.java`。

- 來源 SHA-256 `4311dd098cc115ea028498415d5284014f5b2937a35f820a7d051342f7b11456`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Drawal](server-protocol.md#s-drawal)。
- class entry SHA-256 `3c9b7dc69a269aafc9683d29bba88043c9ef5b24be207e95dff502ae7e3d96ff`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Drawal(int, long)
public com.lineage.server.serverpackets.S_Drawal(int, long, com.lineage.server.model.npc.UiOperationContext.Offer, com.lineage.server.model.npc.UiOperationContext.Ticket)
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Drawal(int,long,Offer,Ticket)` | 141 | 141 |

<a id="s-effectlocation"></a>

### S_EffectLocation

來源 `com/lineage/server/serverpackets/S_EffectLocation.java`。

- 來源 SHA-256 `0816db3e29cc8b89da3d45e6114736c1068e61effff8ff8642af37945184d661`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_EffectLocation](server-protocol.md#s-effectlocation)。
- class entry SHA-256 `bc22c827193dacd0be88252dd2732b6aeca8bb7cc5a7de6357d1e88c16a524c4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_EffectLocation(com.lineage.server.types.Point, int)
public com.lineage.server.serverpackets.S_EffectLocation(com.lineage.server.model.L1Location, int)
public com.lineage.server.serverpackets.S_EffectLocation(int, int, int)
public com.lineage.server.serverpackets.S_EffectLocation(int, com.lineage.server.model.L1Location)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_EffectLocation(int,int,int)` | 106 | 106 |
| `public S_EffectLocation(int,L1Location)` | 動態 | 動態 |

<a id="s-emblem"></a>

### S_Emblem

來源 `com/lineage/server/serverpackets/S_Emblem.java`。

- 來源 SHA-256 `3cc2416ec03c685ca7ce5bb4509b390a2f16553016edb443d3b7ff348a494f72`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Emblem](server-protocol.md#s-emblem)。
- class entry SHA-256 `306edc55ae04ecaa3eeb39413c55123de0e8ee6915b629bc5c7560a02ca8065e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Emblem(int)
public com.lineage.server.serverpackets.S_Emblem(com.lineage.server.templates.L1EmblemIcon)
public com.lineage.server.serverpackets.S_Emblem(int, byte[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Emblem(int)` | 118 | 118 |
| `public S_Emblem(L1EmblemIcon)` | 118 | 118 |
| `public S_Emblem(int,byte[])` | 118 | 118 |

<a id="s-entergame"></a>

### S_EnterGame

來源 `com/lineage/server/serverpackets/S_EnterGame.java`。

- 來源 SHA-256 `e0028828980e665ce86d75f055cd17aad11cb3e629ba49f6633e54d156e1891f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_EnterGame](server-protocol.md#s-entergame)。
- class entry SHA-256 `8af5ed25684b17e08ee20e3186cd56bbe7f1097301f27d7823dc829facccd4ff`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_EnterGame()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_EnterGame()` | 223 | 223 |

<a id="s-equipmentwindow"></a>

### S_EquipmentWindow

來源 `com/lineage/server/serverpackets/S_EquipmentWindow.java`。

- 來源 SHA-256 `8cc712915a3aa7675b0c0a81b17e2aa0aeaaf5edbdcf14b9983919d6a7e6db08`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_EquipmentWindow](server-protocol.md#s-equipmentwindow)。
- class entry SHA-256 `dd5016e426d310d04ff0f5b6a41bbb9811ab464ce6633a4cfb9667704c811c4c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_EquipmentWindow(int, int, boolean)
public com.lineage.server.serverpackets.S_EquipmentWindow(java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public com.lineage.server.serverpackets.S_EquipmentWindow(int, int, int)
public com.lineage.server.serverpackets.S_EquipmentWindow(int)
public com.lineage.server.serverpackets.S_EquipmentWindow(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_EquipmentWindow(int,int,boolean)` | 64 | 64 |
| `public S_EquipmentWindow(List<L1ItemInstance>)` | 64 | 64 |
| `public S_EquipmentWindow(int,int,int)` | 64 | 64 |
| `public S_EquipmentWindow(int)` | 64 | 64 |
| `public S_EquipmentWindow(int,int)` | 64 | 64 |

<a id="s-exp"></a>

### S_Exp

來源 `com/lineage/server/serverpackets/S_Exp.java`。

- 來源 SHA-256 `ff2830f32bdc96b758dfde2f2b226449dc20451e6a0b896be52139b303901ea4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Exp](server-protocol.md#s-exp)。
- class entry SHA-256 `713f5129a5e5c3a8671d5a8ba1242641147ef4a824d6d5aa9c2feb4efe763762`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Exp(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_Exp()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Exp(L1PcInstance)` | 113 | 113 |
| `public S_Exp()` | 113 | 113 |

<a id="s-fishing"></a>

### S_Fishing

來源 `com/lineage/server/serverpackets/S_Fishing.java`。

- 來源 SHA-256 `d7b537dad8d074766c63dcce5958b39811538f11008445f9f563c5327d92f4a3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Fishing](server-protocol.md#s-fishing)。
- class entry SHA-256 `ff9718e6d6c6c4d6593321de39d189beba46561bf90bf551810bb19617155691`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Fishing(int, int, int, int)
private void buildPacket(int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int,int,int)` | 158 | 158 |

<a id="s-fixweaponlist"></a>

### S_FixWeaponList

來源 `com/lineage/server/serverpackets/S_FixWeaponList.java`。

- 來源 SHA-256 `ef59e1f5f87c538be34a5c55b72d72b81aaa89709559ef158e282ba38641ca66`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_FixWeaponList](server-protocol.md#s-fixweaponlist)。
- class entry SHA-256 `4a7534c4bf024d9849268db66e65ab24349c7bf17c9c8dcc63c310012b5f383a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_FixWeaponList(java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public com.lineage.server.serverpackets.S_FixWeaponList(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_FixWeaponList(com.lineage.server.model.Instance.L1ItemInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_FixWeaponList(List<L1ItemInstance>)` | 83 | 83 |
| `private void buildPacket(L1PcInstance)` | 83 | 83 |
| `public S_FixWeaponList(L1ItemInstance)` | 83 | 83 |

<a id="s-followerpack"></a>

### S_FollowerPack

來源 `com/lineage/server/serverpackets/S_FollowerPack.java`。

- 來源 SHA-256 `fb07f668b6ca254c17fe2f93707378d1a09cb7c0fcd0377a78fe88d83d24ad3b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_FollowerPack](server-protocol.md#s-followerpack)。
- class entry SHA-256 `65d1b8b27987deaeba2f075bfeb836861df6ab8c6bd046416945f2572f935f30`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_FollowerPack(com.lineage.server.model.Instance.L1FollowerInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_FollowerPack(L1FollowerInstance,L1PcInstance)` | 87 | 87 |

<a id="s-gametime"></a>

### S_GameTime

來源 `com/lineage/server/serverpackets/S_GameTime.java`。

- 來源 SHA-256 `14c950e819c2e9cc4a02835e85509929feeb788c56127404e9a2f008d15039ef`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_GameTime](server-protocol.md#s-gametime)。
- class entry SHA-256 `b368a5ed003e66a770d4859f872cac8764b3181f7aac9e58cbfd60746e70380c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_GameTime(int)
public com.lineage.server.serverpackets.S_GameTime()
private void buildPacket(int)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int)` | 123 | 123 |

<a id="s-globaleffect"></a>

### S_GlobalEffect

來源 `com/lineage/server/serverpackets/S_GlobalEffect.java`。

- 來源 SHA-256 `daf271131ad2912652c0b1680d7c46b928abbebc68c848cc8a67e91901de004c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_GlobalEffect](server-protocol.md#s-globaleffect)。
- class entry SHA-256 `7ac1ce06ac2955a7dabb2f928a4dd5d3da01f36f9111e85b71654d91c43aa279`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_GlobalEffect(int)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_GlobalEffect(int)` | 250 | 250 |

<a id="s-gmhtml"></a>

### S_GMHtml

來源 `com/lineage/server/serverpackets/S_GMHtml.java`。

- 來源 SHA-256 `8d95ed7683723167469a83c72e4c51cf8296241e1bfb1abab5b7090646889723`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_GMHtml](server-protocol.md#s-gmhtml)。
- class entry SHA-256 `25f2fe413baf5f9b4c4092d6a067d1f7f828b62a03ae399794ee2ff900437ac7`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_GMHtml(int, java.lang.String)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_GMHtml(int,String)` | 39 | 39 |

<a id="s-helpmessage"></a>

### S_HelpMessage

來源 `com/lineage/server/serverpackets/S_HelpMessage.java`。

- 來源 SHA-256 `69ca1d61b7913c44bd920d89a0951d707bcf3a7ee655af5ed4cb56d24a0ac6c2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_HelpMessage](server-protocol.md#s-helpmessage)。
- class entry SHA-256 `31993d817c23dbad202ef4936ae2106257ae5f555b418c6b3df58d7b68b836b2`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_HelpMessage(java.lang.String, java.lang.String)
public com.lineage.server.serverpackets.S_HelpMessage(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_HelpMessage(String,String)` | 161 | 161 |
| `public S_HelpMessage(String)` | 161 | 161 |

<a id="s-hiresoldier"></a>

### S_HireSoldier

來源 `com/lineage/server/serverpackets/S_HireSoldier.java`。

- 來源 SHA-256 `fb6a0508ba64560a6d5a62cb05186cc6ca95b451b138ba2258ebae6c9895078b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_HireSoldier](server-protocol.md#s-hiresoldier)。
- class entry SHA-256 `1cd1f44eef4ddbaa8c929b6b3f4c1be0c3933d92780c0f9cfeb6aefba8f3133e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_HireSoldier(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

<a id="s-hitreactionsuppresslist"></a>

### S_HitReactionSuppressList

來源 `com/lineage/server/serverpackets/S_HitReactionSuppressList.java`。

- 來源 SHA-256 `f4c064afe45f593ee8cce910fd41b554df704f9a0a955d8998e3466d5fd3aa1c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_HitReactionSuppressList](server-protocol.md#s-hitreactionsuppresslist)。
- class entry SHA-256 `8f27db38f4a5a6dc7e3ed26fcc653848888b0d42309c81209b87679e5c58d28c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_HitReactionSuppressList()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_HitReactionSuppressList()` | 242 | 242 |

<a id="s-housemap"></a>

### S_HouseMap

來源 `com/lineage/server/serverpackets/S_HouseMap.java`。

- 來源 SHA-256 `5b356ee3812405ff08677350dc30b04ae56892d53cc1b47daa0f76e021574ada`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_HouseMap](server-protocol.md#s-housemap)。
- class entry SHA-256 `8754788854af2659a6c36314223637e1af3e006c6c0c600aa5bf3636042b403a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_HouseMap(int, java.lang.String)
private void buildPacket(int, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String)` | 187 | 187 |

<a id="s-howmanymake"></a>

### S_HowManyMake

來源 `com/lineage/server/serverpackets/S_HowManyMake.java`。

- 來源 SHA-256 `25f4d9f97e01c40a9ec65ac1cc6229728f883523c7a6f2a48db8463bec670197`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_HowManyMake](server-protocol.md#s-howmanymake)。
- class entry SHA-256 `32cef1bb3bea666465298a0af77dfd55f8247f59259cc1abe8918b828b8f37df`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_HowManyMake(int, int, java.lang.String)
public com.lineage.server.serverpackets.S_HowManyMake(int, int, java.lang.String, java.lang.Object)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_HowManyMake(int,int,String)` | 136 | 136 |

<a id="s-hpmeter"></a>

### S_HPMeter

來源 `com/lineage/server/serverpackets/S_HPMeter.java`。

- 來源 SHA-256 `a60d3bf921345e1835b9ead6c71549e0042252a2757c09dba2e67c1ef3b6143f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_HPMeter](server-protocol.md#s-hpmeter)。
- class entry SHA-256 `f656214b270d44b5c690aa82f5f4e0bb750c6003c21d9fe14f082e3dae0f87e8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_HPMeter(int, int)
public com.lineage.server.serverpackets.S_HPMeter(com.lineage.server.model.L1Character)
private void buildPacket(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int)` | 237 | 237 |

<a id="s-hpupdate"></a>

### S_HPUpdate

來源 `com/lineage/server/serverpackets/S_HPUpdate.java`。

- 來源 SHA-256 `f96021109939b861a6442fdb9b731f11e30340329ec62177f42bedff0ef1b29d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_HPUpdate](server-protocol.md#s-hpupdate)。
- class entry SHA-256 `93dc3456c59e8e29c30e1fa0efa9e6a11c1dfd41fb50211b55bfee122b7ef164`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
private static int clampCurrent(int, boolean)
private static int clampMax(int, boolean)
public com.lineage.server.serverpackets.S_HPUpdate(int, int)
public com.lineage.server.serverpackets.S_HPUpdate(com.lineage.server.model.Instance.L1PcInstance)
public void buildPacket(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public void buildPacket(int,int)` | 225 | 225 |

<a id="s-iconconfig"></a>

### S_IconConfig

來源 `com/lineage/server/serverpackets/S_IconConfig.java`。

- 來源 SHA-256 `bc4f1629a14cc656c82402ca482aa55661345c85866e403b353bd3c7aba0cca9`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_IconConfig](server-protocol.md#s-iconconfig)。
- class entry SHA-256 `a2a4f41de074b7afd2dc8f43da87cf0241f5d4a91924c772e5935d8345325c17`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_IconConfig(int, int, int, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_IconConfig(int,int,int,boolean)` | 250 | 250 |

<a id="s-icons"></a>

### S_Icons

來源 `com/lineage/server/serverpackets/S_Icons.java`。

- 來源 SHA-256 `a79c613fb69d5765ac8eaf3b64f2f879afd026ec47e5ffaea5aa9f0e74da047b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Icons](server-protocol.md#s-icons)。
- class entry SHA-256 `bc935399e88c4ae8fb41fde9c329ceb70053db2eb8d2f0b5ad97ddfad3f1880b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public static byte toIcons1DurationByte(int)
public static byte toIcons1DebuffDurationByte(int)
public com.lineage.server.serverpackets.S_Icons(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_Icons(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void buildIcons1(com.lineage.server.model.Instance.L1PcInstance, int, int)
private static void applyIcons1DurationOverride(byte[], int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildIcons1(L1PcInstance,int,int)` | 250 | 250 |

<a id="s-identifydesc"></a>

### S_IdentifyDesc

來源 `com/lineage/server/serverpackets/S_IdentifyDesc.java`。

- 來源 SHA-256 `bf53ef84a9dc717357521347dd25b118e6220834925336aa8651c4b39b632123`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_IdentifyDesc](server-protocol.md#s-identifydesc)。
- class entry SHA-256 `368a1475aba453bf4a277d198930e628327dbf4e0ec41389bd6883c7d538adcc`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_IdentifyDesc(com.lineage.server.model.Instance.L1ItemInstance)
private void buildPacket(com.lineage.server.model.Instance.L1ItemInstance)
public com.lineage.server.serverpackets.S_IdentifyDesc()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1ItemInstance)` | 245 | 245 |
| `public S_IdentifyDesc()` | 245 | 245 |

<a id="s-initialabilitygrowth"></a>

### S_InitialAbilityGrowth

來源 `com/lineage/server/serverpackets/S_InitialAbilityGrowth.java`。

- 來源 SHA-256 `333fbe32fd60bb02d1584b75e892566158aadfb269ed90eb0b8f3ef1be3d2f2b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_InitialAbilityGrowth](server-protocol.md#s-initialabilitygrowth)。
- class entry SHA-256 `632618fc0beab03d0e34b8d0bbadb5c5414ec0f73fd6fee1bb97cc4cee9b797a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_InitialAbilityGrowth(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, int, int, int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,int,int,int,int,int,int)` | 64 | 64 |

<a id="s-invis"></a>

### S_Invis

來源 `com/lineage/server/serverpackets/S_Invis.java`。

- 來源 SHA-256 `891515fe8213d326999ceb8537f9543fe37100da96de23e16b59560f587f24c1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Invis](server-protocol.md#s-invis)。
- class entry SHA-256 `58199e0058460c8965cfbdbe3f4b9304a4716557f02c5813a39d41dbd528f767`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Invis(int, int)
private void buildPacket(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int)` | 171 | 171 |

<a id="s-invlist"></a>

### S_InvList

來源 `com/lineage/server/serverpackets/S_InvList.java`。

- 來源 SHA-256 `cd3c0c20a7285b0ef32d9305e0559cf7ea02030c2ff0665363ffad91521f2820`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_InvList](server-protocol.md#s-invlist)。
- class entry SHA-256 `64b4216a8606a38148780bbd65fb8d4116b6a4643f69b08d078bd91631b04a70`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_InvList(java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_InvList(List<L1ItemInstance>)` | 5 | 5 |

<a id="s-itemamount"></a>

### S_ItemAmount

來源 `com/lineage/server/serverpackets/S_ItemAmount.java`。

- 來源 SHA-256 `a1407126dab15dab3d37b3a017ed6966e08dade98564ffca3441c204bab9be18`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ItemAmount](server-protocol.md#s-itemamount)。
- class entry SHA-256 `3545602b5903895561c3539209e4d1cfad21a981231cedf8ddab6a54b734908a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ItemAmount(com.lineage.server.model.Instance.L1ItemInstance, int)
private void buildPacket(com.lineage.server.model.Instance.L1ItemInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1ItemInstance)` | 24 | 24 |

<a id="s-itemboard"></a>

### S_ItemBoard

來源 `com/lineage/server/serverpackets/S_ItemBoard.java`。

- 來源 SHA-256 `5c039926eaf8455e0e7cacc178ed26a523de79e0681364d321bf22aa971eabfa`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ItemBoard](server-protocol.md#s-itemboard)。
- class entry SHA-256 `03fc6e47db82fc24a0e2329ef260c02aac3b998d78522847661639958b226e7f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ItemBoard(int, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ItemBoard(int,String)` | 250 | 250 |

<a id="s-itemcolor"></a>

### S_ItemColor

來源 `com/lineage/server/serverpackets/S_ItemColor.java`。

- 來源 SHA-256 `f4bbd6054b8076a37bdf3e8591d0ffdc536e293cbe9f371636d774fa60c78d42`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ItemColor](server-protocol.md#s-itemcolor)。
- class entry SHA-256 `1f118002409e8799393b47e01ee74f6dda1568e26c593ce00985b1380322d9d9`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ItemColor(com.lineage.server.model.Instance.L1ItemInstance)
private void buildPacket(com.lineage.server.model.Instance.L1ItemInstance)
public com.lineage.server.serverpackets.S_ItemColor(com.lineage.server.model.Instance.L1ItemInstance, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1ItemInstance)` | 240 | 240 |
| `public S_ItemColor(L1ItemInstance,int)` | 240 | 240 |

<a id="s-itemcount"></a>

### S_ItemCount

來源 `com/lineage/server/serverpackets/S_ItemCount.java`。

- 來源 SHA-256 `eaf19618fbd008fbc571f52647fbda3920f824f88a7685e13a64e0cf9afefa50`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ItemCount](server-protocol.md#s-itemcount)。
- class entry SHA-256 `7685576fc7159de145da8c0c17a6cbed95ea5625fc0967733e0a33a8ee35ad89`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_ItemCount(int, int, java.lang.String)
public com.lineage.server.serverpackets.S_ItemCount(int, int, java.lang.String, java.lang.String)
public com.lineage.server.serverpackets.S_ItemCount(int, int, int, java.lang.String, java.lang.String, java.lang.String[])
public com.lineage.server.serverpackets.S_ItemCount(int, int, int, java.lang.String, java.lang.String)
public com.lineage.server.serverpackets.S_ItemCount(int, int, int, java.lang.String, java.lang.String, com.lineage.server.model.npc.UiOperationContext.AuctionSelection)
public com.lineage.server.serverpackets.S_ItemCount(int, int, int, java.lang.String, java.lang.String, java.lang.String[], java.lang.Object)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ItemCount(int,int,String)` | 136 | 136 |
| `public S_ItemCount(int,int,String,String)` | 136 | 136 |
| `public S_ItemCount(int,int,int,String,String,String[])` | 136 | 136 |
| `public S_ItemCount(int,int,int,String,String)` | 136 | 136 |

<a id="s-itemerror"></a>

### S_ItemError

來源 `com/lineage/server/serverpackets/S_ItemError.java`。

- 來源 SHA-256 `f8082ff7222c49bf72a3294012dbe819d3496f6efa4f3a586966577f31eb4436`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ItemError](server-protocol.md#s-itemerror)。
- class entry SHA-256 `e4cb250c4ac8261e35c8d15e1499396789c798e3b3ea4d8ab4610e2ecddbda95`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ItemError(int)
private void buildPacket(int)
public byte[] getContent()
public java.lang.String getType()
```

<a id="s-itemname"></a>

### S_ItemName

來源 `com/lineage/server/serverpackets/S_ItemName.java`。

- 來源 SHA-256 `498eb49f65ad9a308afaf98bbf91ac497d70c603e97ebc70a5c3623ee30172b0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ItemName](server-protocol.md#s-itemname)。
- class entry SHA-256 `ee38a15e9216db2bd853c0434645abe869f28dbc9b19399dc8ffe4a1b3925b99`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ItemName(com.lineage.server.model.Instance.L1ItemInstance)
public com.lineage.server.serverpackets.S_ItemName(com.lineage.server.model.Instance.L1ItemInstance, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ItemName(L1ItemInstance)` | 100 | 100 |
| `public S_ItemName(L1ItemInstance,int)` | 100 | 100 |

<a id="s-itemstatus"></a>

### S_ItemStatus

來源 `com/lineage/server/serverpackets/S_ItemStatus.java`。

- 來源 SHA-256 `6e69ff83cbbe91f7a8914a2e43bda498eb0d2fc5808e5218edea5cfe1d513eac`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ItemStatus](server-protocol.md#s-itemstatus)。
- class entry SHA-256 `11b3ed7797141bd48a6a16af27962cf9e8720c0bbe4cd2c9233d12ca331b9c4a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ItemStatus(com.lineage.server.model.Instance.L1ItemInstance)
private void buildPacket(com.lineage.server.model.Instance.L1ItemInstance)
public com.lineage.server.serverpackets.S_ItemStatus(com.lineage.server.model.Instance.L1ItemInstance, long)
public static boolean shouldUseLongStatus(com.lineage.server.model.Instance.L1ItemInstance)
private static boolean shouldUseLongStatus(com.lineage.server.model.Instance.L1ItemInstance, byte[])
private void writeLongStatus(com.lineage.server.model.Instance.L1ItemInstance, java.lang.String, int, byte[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1ItemInstance)` | 24 | 24 |
| `public S_ItemStatus(L1ItemInstance,long)` | 24 | 24 |
| `private void writeLongStatus(L1ItemInstance,String,int,byte[])` | 242 | 242 |

<a id="s-karma"></a>

### S_Karma

來源 `com/lineage/server/serverpackets/S_Karma.java`。

- 來源 SHA-256 `415b1e651d8ea6aa7aca1e50e2055fb48c041610aaf4d0f3c23212845cc9fdd6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Karma](server-protocol.md#s-karma)。
- class entry SHA-256 `3dc4e93f5910b60b3eb63cb33e7c851dad9c8885268baa388ddd53f965ba70a8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Karma(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Karma(L1PcInstance)` | 250 | 250 |

<a id="s-killmessage"></a>

### S_KillMessage

來源 `com/lineage/server/serverpackets/S_KillMessage.java`。

- 來源 SHA-256 `5758fe576dd6dcb6a4b25b51a91bad1231de81dc1e780cc4ff1de558d504ae4c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_KillMessage](server-protocol.md#s-killmessage)。
- class entry SHA-256 `37f365f0ed32468d16183245be12b2403b86c02efe16d84a5f2cf009858a98e7`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_KillMessage(java.lang.String, java.lang.String, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_KillMessage(String,String,int)` | 161 | 161 |

<a id="s-lawful"></a>

### S_Lawful

來源 `com/lineage/server/serverpackets/S_Lawful.java`。

- 來源 SHA-256 `e5e860264df795ce3c28e2387fa40e87c9aa1e800659a28ec430c420d1e8acdf`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Lawful](server-protocol.md#s-lawful)。
- class entry SHA-256 `d8153b0d418ac3d30648747de770e442532cee572c27f481d54339793c745ef9`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Lawful(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_Lawful(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance)` | 34 | 34 |
| `public S_Lawful(int)` | 34 | 34 |

<a id="s-letter"></a>

### S_Letter

來源 `com/lineage/server/serverpackets/S_Letter.java`。

- 來源 SHA-256 `19967c94bb49dcc383401b7461e55f5edeab57dce993e68a17236c1b65e38e68`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Letter](server-protocol.md#s-letter)。
- class entry SHA-256 `23e56d4eb480875d827cd76240f4051cdddcda646ae80ac288bd9c6788ee608b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Letter(com.lineage.server.model.Instance.L1ItemInstance)
private void buildPacket(com.lineage.server.model.Instance.L1ItemInstance)
public byte[] getContent()
public java.lang.String getType()
```

<a id="s-light"></a>

### S_Light

來源 `com/lineage/server/serverpackets/S_Light.java`。

- 來源 SHA-256 `d633c820c10ec2eda64381be17ea3adfbe00e256b0c212cf242414fed2815dd9`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Light](server-protocol.md#s-light)。
- class entry SHA-256 `d363fedb2db7d2bb63bfde6f2cf482721cd4c0e47f86de1ab8555f1497d7bd34`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Light(int, int)
private void buildPacket(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int)` | 40 | 40 |

<a id="s-liquor"></a>

### S_Liquor

來源 `com/lineage/server/serverpackets/S_Liquor.java`。

- 來源 SHA-256 `d45b43cdd6152348da1414e2febb06f2a5573bb2a4489feff2cef38c9b227e23`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Liquor](server-protocol.md#s-liquor)。
- class entry SHA-256 `a39ac47b326c459689fdfffcc4b64b8416295f7ba17e7e1286fa35259fa44ada`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Liquor(int)
public com.lineage.server.serverpackets.S_Liquor(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Liquor(int)` | 103 | 103 |
| `public S_Liquor(int,int)` | 103 | 103 |

<a id="s-lock"></a>

### S_Lock

來源 `com/lineage/server/serverpackets/S_Lock.java`。

- 來源 SHA-256 `0db0f932754f1f6da9adcb7f4fc9c6f32ef8db61e3094651be34299d394cc32c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Lock](server-protocol.md#s-lock)。
- class entry SHA-256 `1df3fc356f8f5207a9fd5f3923573ada04b94d2c6aba8b2ab2471ae82dfbda33`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Lock()
private void buildPacket()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket()` | 241 | 241 |

<a id="s-loginresult"></a>

### S_LoginResult

來源 `com/lineage/server/serverpackets/S_LoginResult.java`。

- 來源 SHA-256 `e821e56d72478a893518d754fe7d79812be558b5fdf47fea01b7d4e1882590ba`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_LoginResult](server-protocol.md#s-loginresult)。
- class entry SHA-256 `fee0fa4da638e2756644d0c27a14bc010c1121ac850f9790d2b913cf0d4e6aec`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_LoginResult(int)
private void buildPacket(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int)` | 233 | 233 |

<a id="s-magicequipment"></a>

### S_MagicEquipment

來源 `com/lineage/server/serverpackets/S_MagicEquipment.java`。

- 來源 SHA-256 `b6e76b4e0003906803e71d092ff1cc2b9e6051c82e2059ea733974ab2b14baa7`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_MagicEquipment](server-protocol.md#s-magicequipment)。
- class entry SHA-256 `c73e5b25efcc7541d303256b4c62ff8013e9b9ff5d225505374e44f9f7716f5d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_MagicEquipment(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_MagicEquipment(int,int)` | 250 | 250 |

<a id="s-mail"></a>

### S_Mail

來源 `com/lineage/server/serverpackets/S_Mail.java`。

- 來源 SHA-256 `c5d358f13d64554108dc8abb56305147d07737473105ec66ba297fe330c59a16`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Mail](server-protocol.md#s-mail)。
- class entry SHA-256 `d2c0dd6c7746241294260bdee6b62e856b106c4188ad073625b15a5768226bac`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Mail(java.util.ArrayList<com.lineage.server.templates.L1Mail>, int)
public com.lineage.server.serverpackets.S_Mail(int)
public com.lineage.server.serverpackets.S_Mail(com.lineage.server.templates.L1Mail, int)
private void buildPacket_1(com.lineage.server.templates.L1Mail, int)
private void buildPacket_2(com.lineage.server.templates.L1Mail, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Mail(ArrayList<L1Mail>,int)` | 186 | 186 |
| `public S_Mail(int)` | 186 | 186 |
| `private void buildPacket_1(L1Mail,int)` | 186 | 186 |
| `private void buildPacket_2(L1Mail,int)` | 186 | 186 |

<a id="s-mapid"></a>

### S_MapID

來源 `com/lineage/server/serverpackets/S_MapID.java`。

- 來源 SHA-256 `e4f8e0f4d4b98bdc90333233a9daba53ffee547881905956ef2e1cb33e28fb68`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_MapID](server-protocol.md#s-mapid)。
- class entry SHA-256 `98046990fd964bbd63f475674134916b00be9728ae35f5bd14d2acd7c4987be6`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_MapID(int, boolean)
public com.lineage.server.serverpackets.S_MapID(com.lineage.server.model.Instance.L1PcInstance, int, boolean)
public com.lineage.server.serverpackets.S_MapID(int)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_MapID(int,boolean)` | 206 | 206 |
| `public S_MapID(L1PcInstance,int,boolean)` | 206 | 206 |
| `public S_MapID(int)` | 206 | 206 |

<a id="s-message-yn"></a>

### S_Message_YN

來源 `com/lineage/server/serverpackets/S_Message_YN.java`。

- 來源 SHA-256 `f5c6bad673e5af10c851fd6206624597c2514fb88b98fc1f3e574e935c6d0c45`，與 #132 相符。
- 核對結果為簽章差異。#132 索引連結見 [S_Message_YN](server-protocol.md#s-message-yn)。
- class entry SHA-256 `c0ae97d980ae7a0d690616a49b3b670ae21f0cf765a737b6c2ca6fb915ffbdf4`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Message_YN(int)
public com.lineage.server.serverpackets.S_Message_YN(com.lineage.server.model.npc.UiOperationContext.Offer, com.lineage.server.model.npc.UiOperationContext.Ticket, int)
public com.lineage.server.serverpackets.S_Message_YN(java.lang.String)
public com.lineage.server.serverpackets.S_Message_YN(int, java.lang.String)
public com.lineage.server.serverpackets.S_Message_YN(com.lineage.server.model.npc.UiOperationContext.Offer, com.lineage.server.model.npc.UiOperationContext.Ticket, int, java.lang.String)
public com.lineage.server.serverpackets.S_Message_YN(int, java.lang.String, java.lang.String)
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public byte[] getContent()
public java.lang.String getType()
```

來源獨有宣告方法

```text
private com.lineage.server.serverpackets.S_Message_YN(com.lineage.server.model.npc.UiOperationContext.Offer,com.lineage.server.model.npc.UiOperationContext.Ticket,int,java.lang.String,boolean)
public static com.lineage.server.serverpackets.S_Message_YN confirmation(com.lineage.server.model.npc.UiOperationContext.Offer,com.lineage.server.model.npc.UiOperationContext.Ticket,int,java.lang.String)
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Message_YN(Offer,Ticket,int)` | 219 | 219 |
| `public S_Message_YN(String)` | 219 | 219 |
| `public S_Message_YN(int,String)` | 219 | 219 |
| `public S_Message_YN(Offer,Ticket,int,String)` | 219 | 219 |
| `public S_Message_YN(int,String,String)` | 219 | 219 |
| `private S_Message_YN(Offer,Ticket,int,String,boolean)` | 219 | 缺席 |

<a id="s-movecharpacket"></a>

### S_MoveCharPacket

來源 `com/lineage/server/serverpackets/S_MoveCharPacket.java`。

- 來源 SHA-256 `cfe7d3cd0080572bc275811b6eac90cb63e3dadb03383ddf95792d4bcd15d8b0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_MoveCharPacket](server-protocol.md#s-movecharpacket)。
- class entry SHA-256 `4151ab33112c2b5398a26f24a57803c6aaf80ab04b2b0f7b1cf5159bb3df64bc`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_MoveCharPacket(com.lineage.server.model.L1Character)
private static com.lineage.server.model.L1Character.MovementSnapshot captureLegacyMovement(com.lineage.server.model.L1Character)
public com.lineage.server.serverpackets.S_MoveCharPacket(com.lineage.server.model.L1Character, int, int, int)
private static com.lineage.server.model.L1Character.MovementSnapshot captureSuppliedMovement(com.lineage.server.model.L1Character, int, int, int)
public com.lineage.server.serverpackets.S_MoveCharPacket(com.lineage.server.model.L1Character, com.lineage.server.model.L1Character.MovementSnapshot)
public boolean isValidForWrite()
public void onInvalidForWrite()
public byte[] getContent()
public static java.lang.String getSafetyMonitorLine()
public java.lang.String getDiagnosticContext()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_MoveCharPacket(L1Character,L1Character.MovementSnapshot)` | 10 | 10 |

<a id="s-movenpcpacket"></a>

### S_MoveNpcPacket

來源 `com/lineage/server/serverpackets/S_MoveNpcPacket.java`。

- 來源 SHA-256 `f8dea418f0a77e429d635fd916658e8b0e5feeab9be7b54116ecaecbbe5c580e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_MoveNpcPacket](server-protocol.md#s-movenpcpacket)。
- class entry SHA-256 `b19dd931f48ba3520d8172a5d99185e3bdfbfdf33da72cdfe8bce3c1353e47a3`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_MoveNpcPacket(com.lineage.server.model.Instance.L1MonsterInstance, int, int, int)
public com.lineage.server.serverpackets.S_MoveNpcPacket(com.lineage.server.model.L1Character)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_MoveNpcPacket(L1MonsterInstance,int,int,int)` | 10 | 10 |
| `public S_MoveNpcPacket(L1Character)` | 10 | 10 |

<a id="s-mpupdate"></a>

### S_MPUpdate

來源 `com/lineage/server/serverpackets/S_MPUpdate.java`。

- 來源 SHA-256 `966ebd8c869f5bf0e50e14127ca9fd5adc37710d5dc90678819a8d6e71eef53d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_MPUpdate](server-protocol.md#s-mpupdate)。
- class entry SHA-256 `33e6cd4580a3377856ece6bfa15d35d9af9140f8b6557eb850dab012bf22453d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
private static int clampCurrent(int, boolean)
private static int clampMax(int, boolean)
public com.lineage.server.serverpackets.S_MPUpdate(int, int)
public com.lineage.server.serverpackets.S_MPUpdate(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int)` | 33 | 33 |

<a id="s-newcharpacket"></a>

### S_NewCharPacket

來源 `com/lineage/server/serverpackets/S_NewCharPacket.java`。

- 來源 SHA-256 `89f9f783ec9e38f16fd21c30cbb32be4c86d021750b74083ed6a547fac08707e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NewCharPacket](server-protocol.md#s-newcharpacket)。
- class entry SHA-256 `524e381505b5c36dc6784350b81778a87897f90b352a366b0ad3ef43b1d93e98`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NewCharPacket(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance)` | 127 | 127 |

<a id="s-newmaster"></a>

### S_NewMaster

來源 `com/lineage/server/serverpackets/S_NewMaster.java`。

- 來源 SHA-256 `8e0542080d330c47937166537416fdaf61b2115c7d753c528ddddb5f53de996e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NewMaster](server-protocol.md#s-newmaster)。
- class entry SHA-256 `74e7fa953a61beb12e7773276a8fa23baaddf3dd49056b505a396d64150b4122`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NewMaster(java.lang.String, com.lineage.server.model.Instance.L1NpcInstance)
private void buildPacket(java.lang.String, com.lineage.server.model.Instance.L1NpcInstance)
public com.lineage.server.serverpackets.S_NewMaster(com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(String,L1NpcInstance)` | 72 | 72 |
| `public S_NewMaster(L1NpcInstance)` | 72 | 72 |

<a id="s-nosell"></a>

### S_NoSell

來源 `com/lineage/server/serverpackets/S_NoSell.java`。

- 來源 SHA-256 `0ddcd75b29cf74b46662475af866270fea8c963619250b09fd3d03a4c0521ab9`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NoSell](server-protocol.md#s-nosell)。
- class entry SHA-256 `85bab2a4ac7f7b3080d01f4ddd0423f9d96486caf496cce25978db11540ae0c7`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NoSell(com.lineage.server.model.Instance.L1NpcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1NpcInstance)` | 39 | 39 |

<a id="s-npcchat"></a>

### S_NpcChat

來源 `com/lineage/server/serverpackets/S_NpcChat.java`。

- 來源 SHA-256 `f24851772a3111921f9de1a3301b47da5f7503066ea4293310e2567b12b3c12e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NpcChat](server-protocol.md#s-npcchat)。
- class entry SHA-256 `b6fb718071f8b3af38a0e3bc8e96ee4dde026aca6399baf1999c4ecd2f0b5b3f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NpcChat(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
public com.lineage.server.serverpackets.S_NpcChat(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String, boolean)
public com.lineage.server.serverpackets.S_NpcChat(com.lineage.server.model.Instance.L1PcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NpcChat(L1NpcInstance,String)` | 161 | 161 |
| `public S_NpcChat(L1NpcInstance,String,boolean)` | 161 | 161 |
| `public S_NpcChat(L1PcInstance,String)` | 161 | 161 |

<a id="s-npcchatglobal"></a>

### S_NpcChatGlobal

來源 `com/lineage/server/serverpackets/S_NpcChatGlobal.java`。

- 來源 SHA-256 `de1fd1d888f1c77b8e6d2f6e83d6ac4eb34efd9946a0514f7bf76b8bf1f2d4d2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NpcChatGlobal](server-protocol.md#s-npcchatglobal)。
- class entry SHA-256 `9fc3f24d2393fd2ce10355dd3237e3d8af96a961c70218693ddd010bdf0738bb`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NpcChatGlobal(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1NpcInstance,String)` | 161 | 161 |

<a id="s-npcchatshouting"></a>

### S_NpcChatShouting

來源 `com/lineage/server/serverpackets/S_NpcChatShouting.java`。

- 來源 SHA-256 `57517a252c5fe3cf12906c11bd4c648f56fd5994ef15cc55f4677560242140f4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NpcChatShouting](server-protocol.md#s-npcchatshouting)。
- class entry SHA-256 `71955a9ee5735a1592c18982ec14545cfd626e75a4b776fbe28881525d166e26`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NpcChatShouting(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
private void buildPacket(com.lineage.server.model.Instance.L1NpcInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1NpcInstance,String)` | 161 | 161 |

<a id="s-npcpack"></a>

### S_NPCPack

來源 `com/lineage/server/serverpackets/S_NPCPack.java`。

- 來源 SHA-256 `dee24b66cd725b3c70a110da0dbfbb61f9e20d6a13b00a798dff046e0251f0aa`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack](server-protocol.md#s-npcpack)。
- class entry SHA-256 `c5c1282cc2c311cedeab4f7b6aedb96dbdb5bbf4f636c6019236505a15c3520e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack(com.lineage.server.model.Instance.L1NpcInstance)
public com.lineage.server.serverpackets.S_NPCPack(com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack(L1NpcInstance)` | 87 | 87 |
| `public S_NPCPack(L1NpcInstance,L1PcInstance)` | 87 | 87 |

<a id="s-npcpack-d"></a>

### S_NPCPack_D

來源 `com/lineage/server/serverpackets/S_NPCPack_D.java`。

- 來源 SHA-256 `489af1914108cf85b824fc5cfe3d1a40e89444d5e746035da8d83ff76dc53635`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_D](server-protocol.md#s-npcpack-d)。
- class entry SHA-256 `9262eb23c46ed67767b0226a3857d42706f6b40964a922a9458e3455394e6ce3`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_D(com.lineage.server.model.Instance.L1DwarfInstance)
public com.lineage.server.serverpackets.S_NPCPack_D(com.lineage.server.model.Instance.L1DwarfInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_D(L1DwarfInstance)` | 87 | 87 |
| `public S_NPCPack_D(L1DwarfInstance,L1PcInstance)` | 87 | 87 |

<a id="s-npcpack-de"></a>

### S_NPCPack_De

來源 `com/lineage/server/serverpackets/S_NPCPack_De.java`。

- 來源 SHA-256 `3a5e568d9193bb9d73f9d8a7af4e10f905c3bee7727a1b2677bc919c1a769f5e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_De](server-protocol.md#s-npcpack-de)。
- class entry SHA-256 `2107c80bf114cdc366d572d1953297761ed12d268cd6924863d1c85d0a261229`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_De(com.lineage.server.model.Instance.L1DeInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_De(L1DeInstance)` | 87 | 87 |

<a id="s-npcpack-doll"></a>

### S_NPCPack_Doll

來源 `com/lineage/server/serverpackets/S_NPCPack_Doll.java`。

- 來源 SHA-256 `3e162622cdc9789fc5f166e68662d24b94a6b9350236db9f0e9fc7e9425971c2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_Doll](server-protocol.md#s-npcpack-doll)。
- class entry SHA-256 `ce4d1cccd9a1f75e07f42582be0067baa265c08c9fceabc9772ab3c934664cff`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_Doll(com.lineage.server.model.Instance.L1DollInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_Doll(L1DollInstance,L1PcInstance)` | 87 | 87 |

<a id="s-npcpack-eff"></a>

### S_NPCPack_Eff

來源 `com/lineage/server/serverpackets/S_NPCPack_Eff.java`。

- 來源 SHA-256 `f10f7fd37d085ab01360b6ee77615695bc8165187b136a70a2f30cfe6777bafb`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_Eff](server-protocol.md#s-npcpack-eff)。
- class entry SHA-256 `f63d433b1c6da93b3302925596d14348ee8f09a5f2fd881c4f8d55fc847355e4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_Eff(com.lineage.server.model.Instance.L1EffectInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_Eff(L1EffectInstance)` | 87 | 87 |

<a id="s-npcpack-f"></a>

### S_NPCPack_F

來源 `com/lineage/server/serverpackets/S_NPCPack_F.java`。

- 來源 SHA-256 `de4d7bc82922b581eb042ca772036500f9a01e5257cea2db0022d4e2cfc0a247`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_F](server-protocol.md#s-npcpack-f)。
- class entry SHA-256 `4ed95ac19e4504afcb4869912e9779a4ec786d21515a93942989ffab56831d14`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_F(com.lineage.server.model.Instance.L1FieldObjectInstance)
public com.lineage.server.serverpackets.S_NPCPack_F(com.lineage.server.model.Instance.L1FieldObjectInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_F(L1FieldObjectInstance)` | 87 | 87 |
| `public S_NPCPack_F(L1FieldObjectInstance,L1PcInstance)` | 87 | 87 |

<a id="s-npcpack-ill"></a>

### S_NPCPack_Ill

來源 `com/lineage/server/serverpackets/S_NPCPack_Ill.java`。

- 來源 SHA-256 `e35658e9ca7eaa4b8e668b81cdcc4e8710b9262178d6576e72c230d0662b3dc6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_Ill](server-protocol.md#s-npcpack-ill)。
- class entry SHA-256 `aa2ed068f62f3ccbccfc7111b55a0b827d51985fb0b01c5a8e692f818f6e988a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_Ill(com.lineage.server.model.Instance.L1IllusoryInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_Ill(L1IllusoryInstance)` | 87 | 87 |

<a id="s-npcpack-item"></a>

### S_NPCPack_Item

來源 `com/lineage/server/serverpackets/S_NPCPack_Item.java`。

- 來源 SHA-256 `80b2fa74a278161a3b30ea1f0807e1aaceae1ceac0de839b9baba3c9528f24ee`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_Item](server-protocol.md#s-npcpack-item)。
- class entry SHA-256 `342a67c96513e6dad7b7254d3b13da8819aa441430efe1a4756ef7284ccc5714`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_Item(com.lineage.server.model.Instance.L1ItemInstance)
private void buildPacket(com.lineage.server.model.Instance.L1ItemInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1ItemInstance)` | 87 | 87 |

<a id="s-npcpack-m"></a>

### S_NPCPack_M

來源 `com/lineage/server/serverpackets/S_NPCPack_M.java`。

- 來源 SHA-256 `b4c2e16d018f33497c6e2bdd58ebab8afc05ee225f514b43b3ca09bbae0ab9e2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_M](server-protocol.md#s-npcpack-m)。
- class entry SHA-256 `3c81266c74798ad888bef57f9c4d082636da182b857b5734bf8bf7034314b437`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_M(com.lineage.server.model.Instance.L1MerchantInstance)
public com.lineage.server.serverpackets.S_NPCPack_M(com.lineage.server.model.Instance.L1MerchantInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_M(L1MerchantInstance)` | 87 | 87 |
| `public S_NPCPack_M(L1MerchantInstance,L1PcInstance)` | 87 | 87 |

<a id="s-npcpack-pet"></a>

### S_NPCPack_Pet

來源 `com/lineage/server/serverpackets/S_NPCPack_Pet.java`。

- 來源 SHA-256 `4006924b41226494b8eac4073881c0194bbf4cb5e5d124b4c36a8fdd23fba303`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_Pet](server-protocol.md#s-npcpack-pet)。
- class entry SHA-256 `53fc75a1690b5a9f6d9af91f8354d6c588cfd7a771ce2a5da73cff1b2d4dfb22`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_Pet(com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PetInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PetInstance,L1PcInstance)` | 87 | 87 |

<a id="s-npcpack-signboard"></a>

### S_NPCPack_Signboard

來源 `com/lineage/server/serverpackets/S_NPCPack_Signboard.java`。

- 來源 SHA-256 `a25115c06513a3287425a4f0ac2abf6438f45e2aa592155531b37edcfb6ca4e5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_Signboard](server-protocol.md#s-npcpack-signboard)。
- class entry SHA-256 `61cf2cc6d694aac3f5333baed2a5d03daad0978a2fb45cf772ca4f123ec37410`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_Signboard(com.lineage.server.model.Instance.L1SignboardInstance)
private int getDirection(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCPack_Signboard(L1SignboardInstance)` | 87 | 87 |

<a id="s-npcpack-summon"></a>

### S_NPCPack_Summon

來源 `com/lineage/server/serverpackets/S_NPCPack_Summon.java`。

- 來源 SHA-256 `85592198bfcd61b7734602370d7ec0c1b68d2464cca3028ce2e44a4ca58be4f1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCPack_Summon](server-protocol.md#s-npcpack-summon)。
- class entry SHA-256 `6f214086d4a3f5338a43a78eb8cdb442a3b3855eaa9fb72aa3cc6ff4e9da5c2e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCPack_Summon(com.lineage.server.model.Instance.L1SummonInstance, com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_NPCPack_Summon(com.lineage.server.model.Instance.L1SummonInstance, com.lineage.server.model.Instance.L1PcInstance, boolean)
private void buildPacket(com.lineage.server.model.Instance.L1SummonInstance, com.lineage.server.model.Instance.L1PcInstance, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1SummonInstance,L1PcInstance,boolean)` | 87 | 87 |

<a id="s-npctalkactiontpurl"></a>

### S_NPCTalkActionTPUrl

來源 `com/lineage/server/serverpackets/S_NPCTalkActionTPUrl.java`。

- 來源 SHA-256 `834bab855ebb80cae0025dbda629e15587dbf14e0c0c6c2d3e8be2852f51faa6`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCTalkActionTPUrl](server-protocol.md#s-npctalkactiontpurl)。
- class entry SHA-256 `9eb997ed4cfec56f7e7e30e17e7c813f90db4aa5e4432e6b1ce57f9c72a55e07`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_NPCTalkActionTPUrl(com.lineage.server.model.L1NpcTalkData, java.lang.Object[], int)
private void buildPacket(com.lineage.server.model.L1NpcTalkData, java.lang.Object[], int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1NpcTalkData,Object[],int)` | 39 | 39 |

<a id="s-npctalkreturn"></a>

### S_NPCTalkReturn

來源 `com/lineage/server/serverpackets/S_NPCTalkReturn.java`。

- 來源 SHA-256 `bfc54936df47f821b54a6acedbe273225e4d53a2b247f21ecf2ce35c653a5847`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_NPCTalkReturn](server-protocol.md#s-npctalkreturn)。
- class entry SHA-256 `66d461eadec4140829b61d52320c9135140167919622830fd15e1252a43c927e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public com.lineage.server.serverpackets.S_NPCTalkReturn(int, java.lang.String, com.lineage.server.model.Instance.L1PcInstance, java.util.List<java.lang.Integer>)
public com.lineage.server.serverpackets.S_NPCTalkReturn(int, java.lang.String, java.util.List<com.lineage.server.model.Instance.L1ItemInstance>, com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_NPCTalkReturn(com.lineage.server.model.L1NpcTalkData, int, int, java.lang.String[])
public com.lineage.server.serverpackets.S_NPCTalkReturn(com.lineage.server.model.L1NpcTalkData, int, int)
public com.lineage.server.serverpackets.S_NPCTalkReturn(int, java.lang.String, java.lang.String[])
public com.lineage.server.serverpackets.S_NPCTalkReturn(int, java.lang.String)
public com.lineage.server.serverpackets.S_NPCTalkReturn(int, com.lineage.server.model.npc.L1NpcHtml)
public com.lineage.server.serverpackets.S_NPCTalkReturn(int, java.lang.String, java.lang.String[], com.lineage.server.model.npc.UiOperationContext.Offer, com.lineage.server.model.npc.UiOperationContext.Ticket)
private void buildPacket(int, java.lang.String, java.lang.String[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_NPCTalkReturn(int,String,L1PcInstance,List<Integer>)` | 39 | 39 |
| `public S_NPCTalkReturn(int,String,List<L1ItemInstance>,L1PcInstance)` | 39 | 39 |
| `private void buildPacket(int,String,String[])` | 39 | 39 |

<a id="s-othercharpacks"></a>

### S_OtherCharPacks

來源 `com/lineage/server/serverpackets/S_OtherCharPacks.java`。

- 來源 SHA-256 `95ee7228e1c9d14d1ae42ec35dbac5fd37166ab2a5ee06f94406960babf99ce5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_OtherCharPacks](server-protocol.md#s-othercharpacks)。
- class entry SHA-256 `dbe42b89593b59d882c636e07c2e044b0765a89c1500cf6535d61ac0ab96efac`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_OtherCharPacks(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_OtherCharPacks(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance)
private static boolean shouldHideOthersIdentity(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_OtherCharPacks(L1PcInstance,L1PcInstance)` | 87 | 87 |

<a id="s-owncharattrdef"></a>

### S_OwnCharAttrDef

來源 `com/lineage/server/serverpackets/S_OwnCharAttrDef.java`。

- 來源 SHA-256 `6d817d26e4bfd369b819f83e655fd4b7859a34d28b12a14646a0cdad113d013b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_OwnCharAttrDef](server-protocol.md#s-owncharattrdef)。
- class entry SHA-256 `6f3037f3c085083f4fabe2bbae3d164b73c7d4f5aacaceea7f0c12f49e376111`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_OwnCharAttrDef(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_OwnCharAttrDef()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance)` | 174 | 174 |
| `public S_OwnCharAttrDef()` | 174 | 174 |

<a id="s-owncharpack"></a>

### S_OwnCharPack

來源 `com/lineage/server/serverpackets/S_OwnCharPack.java`。

- 來源 SHA-256 `6925f701e7d0245e2ba41bf0e2892fa9d8a1c786d6ebe6d87363e77004020f26`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_OwnCharPack](server-protocol.md#s-owncharpack)。
- class entry SHA-256 `f985de3f8b097a0c10a654a36385acf9251f4d9a1e0510ee003e9fca402ec714`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_OwnCharPack(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance)` | 87 | 87 |

<a id="s-owncharstatus"></a>

### S_OwnCharStatus

來源 `com/lineage/server/serverpackets/S_OwnCharStatus.java`。

- 來源 SHA-256 `11c32ff65b296d0b4b4ac6ec9d1ce2632b731ee6664422c73d970d18fa4eac7c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_OwnCharStatus](server-protocol.md#s-owncharstatus)。
- class entry SHA-256 `20155b1e5c64dbdf0a2c3d194ba6b80904da9e27070b5831d4c85c9b9783c3d5`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_OwnCharStatus(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_OwnCharStatus(com.lineage.server.model.Instance.L1PcInstance, int)
public byte[] getContent()
public java.lang.String getType()
private void writeAc(com.lineage.server.model.Instance.L1PcInstance)
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_OwnCharStatus(L1PcInstance)` | 8 | 8 |
| `public S_OwnCharStatus(L1PcInstance,int)` | 8 | 8 |
| `private void writeAc(L1PcInstance)` | 動態 | 動態 |

<a id="s-owncharstatus2"></a>

### S_OwnCharStatus2

來源 `com/lineage/server/serverpackets/S_OwnCharStatus2.java`。

- 來源 SHA-256 `363d5c64f19995539132d5e8e730c55ccf2c7b3220330dd68c02e15e5d999b6b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_OwnCharStatus2](server-protocol.md#s-owncharstatus2)。
- class entry SHA-256 `befe85fe23204adcc33f46bb77adce36e02cba4815814094ad953c34c65d0cd4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_OwnCharStatus2(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_OwnCharStatus2(com.lineage.server.model.Instance.L1PcInstance, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance)` | 155 | 155 |
| `public S_OwnCharStatus2(L1PcInstance,int)` | 155 | 155 |

<a id="s-packetbox"></a>

### S_PacketBox

來源 `com/lineage/server/serverpackets/S_PacketBox.java`。

- 來源 SHA-256 `817ba5fd5b8ada480c6cef6d0529b9cb8ac8c6a33d84af550f8479a31fcce977`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBox](server-protocol.md#s-packetbox)。
- class entry SHA-256 `2beffea35e6cae7fcbafe8a4fba03fb8651b199b1e9500edee6ff0523d8535f0`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBox(int, java.lang.String, int)
public com.lineage.server.serverpackets.S_PacketBox(com.lineage.server.model.Instance.L1PcInstance, java.util.ArrayList<com.lineage.server.model.Instance.L1PcInstance>)
public com.lineage.server.serverpackets.S_PacketBox(int)
public com.lineage.server.serverpackets.S_PacketBox(int, int, java.lang.String)
public com.lineage.server.serverpackets.S_PacketBox(int, int)
public com.lineage.server.serverpackets.S_PacketBox(int, int, int)
public com.lineage.server.serverpackets.S_PacketBox(int, int, int, int)
public com.lineage.server.serverpackets.S_PacketBox(int, java.lang.String)
public com.lineage.server.serverpackets.S_PacketBox(int, java.lang.Object[])
public com.lineage.server.serverpackets.S_PacketBox(int, com.lineage.server.model.Instance.L1PcInstance[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBox(int,String,int)` | 250 | 250 |
| `public S_PacketBox(L1PcInstance,ArrayList<L1PcInstance>)` | 250 | 250 |
| `public S_PacketBox(int)` | 250 | 250 |
| `public S_PacketBox(int,int,String)` | 250 | 250 |
| `public S_PacketBox(int,int)` | 250 | 250 |
| `public S_PacketBox(int,int,int)` | 250 | 250 |
| `public S_PacketBox(int,int,int,int)` | 250 | 250 |
| `public S_PacketBox(int,String)` | 250 | 250 |
| `public S_PacketBox(int,Object[])` | 250 | 250 |
| `public S_PacketBox(int,L1PcInstance[])` | 250 | 250 |

<a id="s-packetboxactivespells"></a>

### S_PacketBoxActiveSpells

來源 `com/lineage/server/serverpackets/S_PacketBoxActiveSpells.java`。

- 來源 SHA-256 `0f4e0aa098041a1229582f91a10b56ca6d9798ac1aaadaa0f76fbd50ae77fde3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxActiveSpells](server-protocol.md#s-packetboxactivespells)。
- class entry SHA-256 `046e0b6c3531c01af0957806e82844cd3187dbcd653b500569a4538b1e168974`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxActiveSpells(com.lineage.server.model.Instance.L1PcInstance)
private int[] activeSpells(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxActiveSpells(L1PcInstance)` | 250 | 250 |

<a id="s-packetboxconfig"></a>

### S_PacketBoxConfig

來源 `com/lineage/server/serverpackets/S_PacketBoxConfig.java`。

- 來源 SHA-256 `2c94a77b314a0e38d01dbc0a47830913e52a7014502e543043bf5814ab3d0ba5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxConfig](server-protocol.md#s-packetboxconfig)。
- class entry SHA-256 `7c2016ce1c1aadfa419d95389d2c19394c4b9a54484887449e81bf3dfbcfe202`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxConfig(com.lineage.server.templates.L1Config)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxConfig(L1Config)` | 250 | 250 |

<a id="s-packetboxcooking"></a>

### S_PacketBoxCooking

來源 `com/lineage/server/serverpackets/S_PacketBoxCooking.java`。

- 來源 SHA-256 `2329862fc16b2ec42049074b6da8bf36fc6b01f4a5f11d0a3db239e2cdae9af2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxCooking](server-protocol.md#s-packetboxcooking)。
- class entry SHA-256 `bbb3273f66793c63c394b415b6e016b444e08b0fccdbc09d530bf4c67b39be2c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxCooking(int)
public com.lineage.server.serverpackets.S_PacketBoxCooking(com.lineage.server.model.Instance.L1PcInstance, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxCooking(int)` | 250 | 250 |
| `public S_PacketBoxCooking(L1PcInstance,int,int)` | 250 | 250 |

<a id="s-packetboxdk"></a>

### S_PacketBoxDk

來源 `com/lineage/server/serverpackets/S_PacketBoxDk.java`。

- 來源 SHA-256 `2200aab30640cd66edbd27476d200c201c10ca861f9c3181605b7f68758c6384`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxDk](server-protocol.md#s-packetboxdk)。
- class entry SHA-256 `9676edf9cb18ac4cd6c427b29d9ea9228005521ef5aa0429643b69dc5706d25b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxDk(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxDk(int)` | 250 | 250 |

<a id="s-packetboxexp"></a>

### S_PacketBoxExp

來源 `com/lineage/server/serverpackets/S_PacketBoxExp.java`。

- 來源 SHA-256 `df1090aa9c2b876c22f479499c5554901ecfab6fb826c44fbb433594d5f22921`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxExp](server-protocol.md#s-packetboxexp)。
- class entry SHA-256 `4ff459c089f25d9e1c3600c113d2478d276861bff710e1d35707c2900760b985`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxExp(int)
public com.lineage.server.serverpackets.S_PacketBoxExp()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxExp(int)` | 250 | 250 |

<a id="s-packetboxgame"></a>

### S_PacketBoxGame

來源 `com/lineage/server/serverpackets/S_PacketBoxGame.java`。

- 來源 SHA-256 `31df36bcf1b73118f1d16d31b95a3fe670461df352ec89c9fe3883851d842621`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxGame](server-protocol.md#s-packetboxgame)。
- class entry SHA-256 `e6364d3e2e010408b8e2f9f271f15bcfddd31bd61b946554f6fac731a9011cf4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxGame(int)
public com.lineage.server.serverpackets.S_PacketBoxGame(int, int)
public com.lineage.server.serverpackets.S_PacketBoxGame(java.util.ArrayList<java.lang.StringBuilder>)
public com.lineage.server.serverpackets.S_PacketBoxGame(java.lang.StringBuilder, java.util.ArrayList<java.lang.StringBuilder>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxGame(int)` | 250 | 250 |
| `public S_PacketBoxGame(int,int)` | 250 | 250 |
| `public S_PacketBoxGame(ArrayList<StringBuilder>)` | 250 | 250 |
| `public S_PacketBoxGame(StringBuilder,ArrayList<StringBuilder>)` | 250 | 250 |

<a id="s-packetboxgm"></a>

### S_PacketBoxGm

來源 `com/lineage/server/serverpackets/S_PacketBoxGm.java`。

- 來源 SHA-256 `d1fbf5ba0e7dd460aafb04d1afa318feb1b40fa850c9ab8b1ff3ce9d44a52494`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxGm](server-protocol.md#s-packetboxgm)。
- class entry SHA-256 `b1a9339126da3eccdea8e03af9aec301684504446a28bc53491f7e608bbff968`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxGm(com.lineage.server.model.Instance.L1PcInstance, int)
private void callSomething(com.lineage.server.model.Instance.L1PcInstance, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxGm(L1PcInstance,int)` | 250 | 250 |
| `private void callSomething(L1PcInstance,int)` | 動態 | 動態 |

<a id="s-packetboxgree"></a>

### S_PacketBoxGree

來源 `com/lineage/server/serverpackets/S_PacketBoxGree.java`。

- 來源 SHA-256 `d72e6b5151f8fcb0695ff6024736b3aa9ecc892a50d907a1f45bb65f6b7edac7`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxGree](server-protocol.md#s-packetboxgree)。
- class entry SHA-256 `78c2748e7f4063ed8510b6622c6fd5f6fa5fe07466d7c79cc72d7491e9ff9f13`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxGree(java.lang.String)
public com.lineage.server.serverpackets.S_PacketBoxGree(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxGree(String)` | 250 | 250 |
| `public S_PacketBoxGree(int)` | 250 | 250 |

<a id="s-packetboxhpmsg"></a>

### S_PacketBoxHpMsg

來源 `com/lineage/server/serverpackets/S_PacketBoxHpMsg.java`。

- 來源 SHA-256 `f3bf17847aab53a0e3beeeba39a6f7de3a9d0b0bfc01b5807d39a7a329a46bed`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxHpMsg](server-protocol.md#s-packetboxhpmsg)。
- class entry SHA-256 `892f74e7156d9851b6340b2cac070c712be99aecdd01be9e3011f81bc004423e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxHpMsg()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxHpMsg()` | 250 | 250 |

<a id="s-packetboxicon1"></a>

### S_PacketBoxIcon1

來源 `com/lineage/server/serverpackets/S_PacketBoxIcon1.java`。

- 來源 SHA-256 `028327d0f79578c5dba63dfe0a1348b5b484d26efb352821351a47eeb1daa7f2`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxIcon1](server-protocol.md#s-packetboxicon1)。
- class entry SHA-256 `e16d32028f335c3e2f3e9c1a224a166336e7a6ef1351c638506266685f9dc974`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxIcon1(int, int)
public com.lineage.server.serverpackets.S_PacketBoxIcon1(boolean, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxIcon1(int,int)` | 250 | 250 |
| `public S_PacketBoxIcon1(boolean,int)` | 250 | 250 |

<a id="s-packetboxicon2"></a>

### S_PacketBoxIcon2

來源 `com/lineage/server/serverpackets/S_PacketBoxIcon2.java`。

- 來源 SHA-256 `b15290a47cc1417954ec14da45d83e6df8f84ea121bbf5f5f3015330d6149cd3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxIcon2](server-protocol.md#s-packetboxicon2)。
- class entry SHA-256 `79a09a66c6eec3e523a86cd3a4b8bdd37f53523fc52ec537547318d66c00d2d2`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxIcon2(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxIcon2(int,int)` | 250 | 250 |

<a id="s-packetboxiconaura"></a>

### S_PacketBoxIconAura

來源 `com/lineage/server/serverpackets/S_PacketBoxIconAura.java`。

- 來源 SHA-256 `8ed97f704874e3f979570df08c87b914c5417d9b3be06815c3db78710cec1998`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxIconAura](server-protocol.md#s-packetboxiconaura)。
- class entry SHA-256 `05b27a3e8f4b672d190cfd2d8dd9d532cc96d1640c2b715f5926337cc3aae3a2`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxIconAura(int, int)
public com.lineage.server.serverpackets.S_PacketBoxIconAura(int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxIconAura(int,int)` | 250 | 250 |
| `public S_PacketBoxIconAura(int,int,int)` | 250 | 250 |

<a id="s-packetboxitemlv"></a>

### S_PacketBoxItemLv

來源 `com/lineage/server/serverpackets/S_PacketBoxItemLv.java`。

- 來源 SHA-256 `ada9cb21721cc48325ffa04d682d2a29ea0fd4916351e3420ee33022297bc3ab`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxItemLv](server-protocol.md#s-packetboxitemlv)。
- class entry SHA-256 `1437a979bf6a10d98993aa66513362f8711b24e7391f84a225c6e74a2fc0a48a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxItemLv(int, int)
public com.lineage.server.serverpackets.S_PacketBoxItemLv(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxItemLv(int,int)` | 250 | 250 |
| `public S_PacketBoxItemLv(int)` | 動態 | 動態 |

<a id="s-packetboxloc"></a>

### S_PacketBoxLoc

來源 `com/lineage/server/serverpackets/S_PacketBoxLoc.java`。

- 來源 SHA-256 `1d8c641c6cd2c105cec528e2025507608f0c09efacc4e7d2ead49b2fbf238fad`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxLoc](server-protocol.md#s-packetboxloc)。
- class entry SHA-256 `ddcc3c23abc3e87c7cc1e768114d3cf4653aed6b37a1d72dd47de8747a353479`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxLoc(java.lang.String, int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxLoc(String,int,int,int,int)` | 250 | 250 |

<a id="s-packetboxmaptimer"></a>

### S_PacketBoxMapTimer

來源 `com/lineage/server/serverpackets/S_PacketBoxMapTimer.java`。

- 來源 SHA-256 `c5788f37175f1f1ececdbe60b033f140b84f20cb44403cd78d7335a7c4619407`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxMapTimer](server-protocol.md#s-packetboxmaptimer)。
- class entry SHA-256 `5d4d224a45e3179a5f5d8b46d71aeb1abd2997c4b8a197c6600247486907e2a6`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxMapTimer(com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxMapTimer(L1PcInstance)` | 250 | 250 |

<a id="s-packetboxparty"></a>

### S_PacketBoxParty

來源 `com/lineage/server/serverpackets/S_PacketBoxParty.java`。

- 來源 SHA-256 `1f57efd8f5d88d01e0e6dc0ca369d29ffa822d5a27f7fce2db8d71a0762fa956`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxParty](server-protocol.md#s-packetboxparty)。
- class entry SHA-256 `a8265c8ddcbfdcc6c4799a69d5262f473850a36cb839db57a7023bb594cf1443`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxParty(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Party)
public com.lineage.server.serverpackets.S_PacketBoxParty(com.lineage.server.model.L1Party, com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_PacketBoxParty(com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_PacketBoxParty(int, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxParty(L1PcInstance,L1Party)` | 250 | 250 |
| `public S_PacketBoxParty(L1Party,L1PcInstance)` | 250 | 250 |
| `public S_PacketBoxParty(L1PcInstance)` | 250 | 250 |
| `public S_PacketBoxParty(int,String)` | 250 | 250 |

<a id="s-packetboxpledgerecommend"></a>

### S_PacketBoxPledgeRecommend

來源 `com/lineage/server/serverpackets/S_PacketBoxPledgeRecommend.java`。

- 來源 SHA-256 `6492db82484df8e7e815b594cd0692d14d884c98d9d13aac3bb6a24e27ac3d9d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxPledgeRecommend](server-protocol.md#s-packetboxpledgerecommend)。
- class entry SHA-256 `e5cd31250af87b3c3b86e844259eb0ec3edd138b766b5ffdce13ae5c16ee560e`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxPledgeRecommend(boolean, int)
public com.lineage.server.serverpackets.S_PacketBoxPledgeRecommend(java.util.List<com.lineage.server.templates.L1ClanRecommend>, java.util.Map<java.lang.Integer, java.util.concurrent.CopyOnWriteArrayList<java.lang.Integer>>)
public com.lineage.server.serverpackets.S_PacketBoxPledgeRecommend(java.util.Map<java.lang.Integer, com.lineage.server.templates.L1ClanRecommend>, java.util.List<java.lang.Integer>)
public com.lineage.server.serverpackets.S_PacketBoxPledgeRecommend(com.lineage.server.templates.L1ClanRecommend, java.util.List<java.lang.Integer>)
public com.lineage.server.serverpackets.S_PacketBoxPledgeRecommend(int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxPledgeRecommend(boolean,int)` | 0 | 0 |
| `public S_PacketBoxPledgeRecommend(List<L1ClanRecommend>,Map<Integer, CopyOnWriteArrayList<Integer>>)` | 0 | 0 |
| `public S_PacketBoxPledgeRecommend(Map<Integer, L1ClanRecommend>,List<Integer>)` | 0 | 0 |
| `public S_PacketBoxPledgeRecommend(L1ClanRecommend,List<Integer>)` | 0 | 0 |
| `public S_PacketBoxPledgeRecommend(int,int,int,int)` | 0 | 0 |

<a id="s-packetboxprotection"></a>

### S_PacketBoxProtection

來源 `com/lineage/server/serverpackets/S_PacketBoxProtection.java`。

- 來源 SHA-256 `45a856735ae94737bef866fe632a0771865bf60ca13a0a8587bb08050a2aca6c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxProtection](server-protocol.md#s-packetboxprotection)。
- class entry SHA-256 `fcee535830b2ec799f65a915a48a47f6acbb59b52bb2aad21f975599be3f4a0b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxProtection(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxProtection(int,int)` | 250 | 250 |

<a id="s-packetboxselect"></a>

### S_PacketBoxSelect

來源 `com/lineage/server/serverpackets/S_PacketBoxSelect.java`。

- 來源 SHA-256 `21ca14f9f68de4a90b7229e26f5f0831c5344d6441c17b8b885bde836ba70496`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxSelect](server-protocol.md#s-packetboxselect)。
- class entry SHA-256 `25370292a89e1791595667bdefde6e11508b174d1623c286638844118082e9b8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxSelect()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxSelect()` | 250 | 250 |

<a id="s-packetboxtest"></a>

### S_PacketBoxTest

來源 `com/lineage/server/serverpackets/S_PacketBoxTest.java`。

- 來源 SHA-256 `e571a9d12ac7cf4f60d5a52f7cd5e8b8065fd6c1b466fac095da3a80418cf8ff`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxTest](server-protocol.md#s-packetboxtest)。
- class entry SHA-256 `51fb631b5c466f1a9fff72bbc5025b6ad52fde2e7ac39d3291c02b96705d01e1`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxTest()
public com.lineage.server.serverpackets.S_PacketBoxTest(byte, int)
public com.lineage.server.serverpackets.S_PacketBoxTest(int, int)
public com.lineage.server.serverpackets.S_PacketBoxTest(int, java.lang.String[])
public com.lineage.server.serverpackets.S_PacketBoxTest(int)
public com.lineage.server.serverpackets.S_PacketBoxTest(byte[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxTest()` | 250 | 250 |
| `public S_PacketBoxTest(byte,int)` | 250 | 250 |
| `public S_PacketBoxTest(int,int)` | 250 | 250 |
| `public S_PacketBoxTest(int,String[])` | 250 | 250 |
| `public S_PacketBoxTest(int)` | 250 | 250 |
| `public S_PacketBoxTest(byte[])` | 動態 | 動態 |

<a id="s-packetboxthirdspeed"></a>

### S_PacketBoxThirdSpeed

來源 `com/lineage/server/serverpackets/S_PacketBoxThirdSpeed.java`。

- 來源 SHA-256 `af6faeb8c56c9280928d2e7cc0e4105ca5ea57785abd4d7dde3f3d85ca9e354e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxThirdSpeed](server-protocol.md#s-packetboxthirdspeed)。
- class entry SHA-256 `ff42ecb4f5100a5f7550677750261062a25ddfbe7b9cd74bb62519fcb1982ac4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxThirdSpeed(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxThirdSpeed(int)` | 250 | 250 |

<a id="s-packetboxwar"></a>

### S_PacketBoxWar

來源 `com/lineage/server/serverpackets/S_PacketBoxWar.java`。

- 來源 SHA-256 `eefdb707a818cb033b6d4e83f190beefd3e8cd6d95dbb0ed51a42507ea3b9d35`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxWar](server-protocol.md#s-packetboxwar)。
- class entry SHA-256 `f43648488fe2880f20f544cc7100a81f568a1c00fb0296034c4638cbb0a7890f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxWar()
public com.lineage.server.serverpackets.S_PacketBoxWar(int)
public com.lineage.server.serverpackets.S_PacketBoxWar(int, int)
public com.lineage.server.serverpackets.S_PacketBoxWar(int, int, java.lang.String, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxWar()` | 250 | 250 |
| `public S_PacketBoxWar(int)` | 250 | 250 |
| `public S_PacketBoxWar(int,int)` | 250 | 250 |
| `public S_PacketBoxWar(int,int,String,String)` | 250 | 250 |

<a id="s-packetboxwaterlife"></a>

### S_PacketBoxWaterLife

來源 `com/lineage/server/serverpackets/S_PacketBoxWaterLife.java`。

- 來源 SHA-256 `f32055fd3a810af04f65f0e45d576c67ab618f172b00914ed7f9f7be47502293`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxWaterLife](server-protocol.md#s-packetboxwaterlife)。
- class entry SHA-256 `0a26a0c823c45c7a52c108bee7a46f5a494d798645566c9249a4161efb2b8137`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxWaterLife()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxWaterLife()` | 250 | 250 |

<a id="s-packetboxwindshackle"></a>

### S_PacketBoxWindShackle

來源 `com/lineage/server/serverpackets/S_PacketBoxWindShackle.java`。

- 來源 SHA-256 `b0a1df2db3e1ddc881e23f3879ffe0e90a26c0a3c57927d7843407faf864621a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxWindShackle](server-protocol.md#s-packetboxwindshackle)。
- class entry SHA-256 `2a5af6881cffc24559059e045829fa8f000b66d02a9feb13b4cfc5e123ddcbad`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxWindShackle(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxWindShackle(int,int)` | 250 | 250 |

<a id="s-packetboxwisdompotion"></a>

### S_PacketBoxWisdomPotion

來源 `com/lineage/server/serverpackets/S_PacketBoxWisdomPotion.java`。

- 來源 SHA-256 `cc3d17cba81f2a85067dfd1eaaa60e01577bd656c0ccd0e72a7a56fd30e994b8`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PacketBoxWisdomPotion](server-protocol.md#s-packetboxwisdompotion)。
- class entry SHA-256 `60893e05c335311cb5b3fdf38f673e15f93e904401e7be87ac031178bb5003a4`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PacketBoxWisdomPotion(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PacketBoxWisdomPotion(int)` | 250 | 250 |

<a id="s-packetnew"></a>

### S_PacketNew

來源 `com/lineage/server/serverpackets/S_PacketNew.java`。

- 來源 SHA-256 `3d4888a6e76d6b8e351e921958f08334a725d969611940d73c91dafe34594f75`，與 #132 相符。
- 核對結果為來源全停用／JAR 無類別。#132 索引連結見 [S_PacketNew](server-protocol.md#s-packetnew)。
語法解析沒有類別或方法宣告，JAR 目錄亦沒有同名 class。

<a id="s-paralysis"></a>

### S_Paralysis

來源 `com/lineage/server/serverpackets/S_Paralysis.java`。

- 來源 SHA-256 `7e57e26309fa5e8221a189ce7c82c64be5d03ee9171d9be8d0d74fb4abf2146a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Paralysis](server-protocol.md#s-paralysis)。
- class entry SHA-256 `ab747298dc41203cdc46d7c493c84c24296157dab7ee69da94d9519e6377b9cf`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Paralysis(int, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Paralysis(int,boolean)` | 202 | 202 |

<a id="s-party"></a>

### S_Party

來源 `com/lineage/server/serverpackets/S_Party.java`。

- 來源 SHA-256 `0b63e4624368e487d5ef5a77fc0804cd2bf996150ff435454727177309946cb3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Party](server-protocol.md#s-party)。
- class entry SHA-256 `520e2cc8238663a1a9bf9f5cfd1118cf647015126d73937d3564b4d75c081933`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Party(java.lang.String, int)
public com.lineage.server.serverpackets.S_Party(java.lang.String, int, java.lang.String, java.lang.String)
private void buildPacket(java.lang.String, int, java.lang.String, java.lang.String, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(String,int,String,String,int)` | 39 | 39 |

<a id="s-pcpositioncorrection"></a>

### S_PcPositionCorrection

來源 `com/lineage/server/serverpackets/S_PcPositionCorrection.java`。

- 來源 SHA-256 `4d12c9d8c73c0a4ed13f1adc5d036e95eb0cdeaa92804951a204c5a9ce3c60ad`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PcPositionCorrection](server-protocol.md#s-pcpositioncorrection)。
- class entry SHA-256 `c9bfcd2668dafb4de3523daaa6f5077ee0dc3915ca2344540eb83cd175c0327b`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PcPositionCorrection(com.lineage.server.model.Instance.L1PcInstance, long, short, int, int, int, long)
public boolean isValidForWrite()
public void onInvalidForWrite()
public java.lang.String getDiagnosticContext()
public static long getStaleDropCount()
```

<a id="s-pcpositionmapreset"></a>

### S_PcPositionMapReset

來源 `com/lineage/server/serverpackets/S_PcPositionMapReset.java`。

- 來源 SHA-256 `60f2ecbb355ca18f65399c8b9d1737a3b935c98a2bdb9c11d821cc623d7677bb`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PcPositionMapReset](server-protocol.md#s-pcpositionmapreset)。
- class entry SHA-256 `812180860f02cf1f2044036734a96b03f43c1b8a133c15bcc70753de353030c6`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PcPositionMapReset(com.lineage.server.model.Instance.L1PcInstance, long, short, int, int, int, long, boolean)
public boolean isValidForWrite()
public byte[] getContent()
public void onInvalidForWrite()
public java.lang.String getDiagnosticContext()
public static long getStaleDropCount()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PcPositionMapReset(L1PcInstance,long,short,int,int,int,long,boolean)` | 206 | 206 |

<a id="s-petctrlmenu"></a>

### S_PetCtrlMenu

來源 `com/lineage/server/serverpackets/S_PetCtrlMenu.java`。

- 來源 SHA-256 `9a66c52a05c8723a310a2ac427afec2690779a33b9f060762c2097b0cf6ae66a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PetCtrlMenu](server-protocol.md#s-petctrlmenu)。
- class entry SHA-256 `b6a2142cd7721d5d6a389dc0198a92c3f4610226e97f5f5bfd351698de596d3c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PetCtrlMenu(com.lineage.server.model.L1Character, com.lineage.server.model.Instance.L1NpcInstance, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PetCtrlMenu(L1Character,L1NpcInstance,boolean)` | 64 | 64 |

<a id="s-petequipment"></a>

### S_PetEquipment

來源 `com/lineage/server/serverpackets/S_PetEquipment.java`。

- 來源 SHA-256 `47c457d75889387fc57ed210ac5ce1bcb269b38bf4c36db54fa63b04212a30a0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PetEquipment](server-protocol.md#s-petequipment)。
- class entry SHA-256 `02594a08d261582fdc9490945948bd17360a06cd1fc8d57f6d0956ffce5d9449`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PetEquipment(int, com.lineage.server.model.Instance.L1PetInstance, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PetEquipment(int,L1PetInstance,int)` | 250 | 250 |

<a id="s-petinventory"></a>

### S_PetInventory

來源 `com/lineage/server/serverpackets/S_PetInventory.java`。

- 來源 SHA-256 `ddc6e6f9bb6023ae58b38c3f28aac0890e610ab89642fdab9b9e5147660d67dd`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PetInventory](server-protocol.md#s-petinventory)。
- class entry SHA-256 `da03930f8e5a976115dda0553e2a65da1324e76ebf2121363d1b28765f338d23`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PetInventory(com.lineage.server.model.Instance.L1PetInstance, boolean)
private void isTrue(com.lineage.server.model.Instance.L1PetInstance)
private static int resolvePetInventoryItemType(com.lineage.server.model.Instance.L1ItemInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void isTrue(L1PetInstance)` | 176 | 176 |

<a id="s-petlist"></a>

### S_PetList

來源 `com/lineage/server/serverpackets/S_PetList.java`。

- 來源 SHA-256 `cd7e0f26c3809c98edae28887d545c19327c2a8ca1dcd3d82bd302e94c2e5528`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PetList](server-protocol.md#s-petlist)。
- class entry SHA-256 `ae8501f801e34f5eeb68a5b3dd44746ede8113b12b533d6a13dcd4fa3588ac2a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PetList(int, com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_PetList(int, com.lineage.server.model.Instance.L1PcInstance, java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
private void buildPacket(int, com.lineage.server.model.Instance.L1PcInstance)
private void buildPacketFromList(java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
private void writeSelectListBody(java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
private boolean isWithdraw(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1ItemInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void writeSelectListBody(List<L1ItemInstance>)` | 83 | 83 |

<a id="s-petmenupacket"></a>

### S_PetMenuPacket

來源 `com/lineage/server/serverpackets/S_PetMenuPacket.java`。

- 來源 SHA-256 `448af0d27a545bafc6978549304d5a9f87d8c1c9a2e4d5bf35d0b3ad89200d40`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PetMenuPacket](server-protocol.md#s-petmenupacket)。
- class entry SHA-256 `0ee874853499136ae037f672a5542729e6100b07c2c011003df9bd6bbaa953ab`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PetMenuPacket(com.lineage.server.model.Instance.L1NpcInstance, int)
private void buildpacket(com.lineage.server.model.Instance.L1NpcInstance, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildpacket(L1NpcInstance,int)` | 39 | 39 |

<a id="s-pinkname"></a>

### S_PinkName

來源 `com/lineage/server/serverpackets/S_PinkName.java`。

- 來源 SHA-256 `df1b1f78ba9216d32bdb6333b1c89410c3c783fefac2ddc4ac8b583ab4c05e67`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PinkName](server-protocol.md#s-pinkname)。
- class entry SHA-256 `4c172a8c752de180d20e3b9eb250cb53c260a3102f5bd168902bae1b911de8e7`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PinkName(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PinkName(int,int)` | 60 | 60 |

<a id="s-pledge"></a>

### S_Pledge

來源 `com/lineage/server/serverpackets/S_Pledge.java`。

- 來源 SHA-256 `5fbe220f68ee9f8923452ab5e1dbb01a038c77ad001a0c31087b86c89d1c2e4a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Pledge](server-protocol.md#s-pledge)。
- class entry SHA-256 `7a73ac0eadb302166d6d68ae7f11d2bf996cf8dcbfc93146ef6377735a10c81b`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Pledge(int, java.lang.String, java.lang.StringBuilder, java.lang.StringBuilder)
private void buildPacket(int, java.lang.String, java.lang.StringBuilder, java.lang.StringBuilder)
public com.lineage.server.serverpackets.S_Pledge(int, java.lang.String, java.lang.StringBuilder)
private void buildPacket(int, java.lang.String, java.lang.StringBuilder)
public com.lineage.server.serverpackets.S_Pledge(int)
public com.lineage.server.serverpackets.S_Pledge(java.lang.String, java.lang.String)
public com.lineage.server.serverpackets.S_Pledge(com.lineage.server.model.L1Clan)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String,StringBuilder,StringBuilder)` | 39 | 39 |
| `private void buildPacket(int,String,StringBuilder)` | 39 | 39 |
| `public S_Pledge(int)` | 167 | 167 |
| `public S_Pledge(String,String)` | 250 | 250 |
| `public S_Pledge(L1Clan)` | 170 | 170 |

<a id="s-pledgewarehousehistory"></a>

### S_PledgeWarehouseHistory

來源 `com/lineage/server/serverpackets/S_PledgeWarehouseHistory.java`。

- 來源 SHA-256 `b2fd732ec118746987d6a947533d4f9084a68560bb16ad163991f6c228846c89`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PledgeWarehouseHistory](server-protocol.md#s-pledgewarehousehistory)。
- class entry SHA-256 `733c66d9d57290c7f5d306614a1b9d4c741530fdf22a54879d65e92d5bb6359a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PledgeWarehouseHistory(int)
private static java.util.List<com.lineage.server.templates.L1ClanWarehouseHistory> recentHistory(int)
private static java.lang.String safe(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PledgeWarehouseHistory(int)` | 250 | 250 |

<a id="s-poison"></a>

### S_Poison

來源 `com/lineage/server/serverpackets/S_Poison.java`。

- 來源 SHA-256 `25e4d654db8f1faec19e0b8cf87f979315a3a1297a94f481014603b2cf5c9be3`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Poison](server-protocol.md#s-poison)。
- class entry SHA-256 `a33acabdb082599d95dbce12669134d6dc0febd4535993bfb731698203ed04f3`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Poison(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Poison(int,int)` | 165 | 165 |

<a id="s-poweritemlist"></a>

### S_PowerItemList

來源 `com/lineage/server/serverpackets/S_PowerItemList.java`。

- 來源 SHA-256 `4daf7045b25aeef8c7f887f580d0fa1b971fe9375e66d37775da362dd7987f3e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PowerItemList](server-protocol.md#s-poweritemlist)。
- class entry SHA-256 `1d0a1f2d70ee68d2ddeacbbf61ee668473f97cf069d574ec50d0e523e282d4e9`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PowerItemList(com.lineage.server.model.Instance.L1PcInstance, int, java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public com.lineage.server.serverpackets.S_PowerItemList(int, java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PowerItemList(L1PcInstance,int,List<L1ItemInstance>)` | 176 | 176 |
| `public S_PowerItemList(int,List<L1ItemInstance>)` | 176 | 176 |

<a id="s-privateshop"></a>

### S_PrivateShop

來源 `com/lineage/server/serverpackets/S_PrivateShop.java`。

- 來源 SHA-256 `a4e31e41005c0e274cbaa4782a3509f6947be64b350369f1d9e8e6b6c52e2eff`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_PrivateShop](server-protocol.md#s-privateshop)。
- class entry SHA-256 `51835df72ae03dc691da22ce90266396e1ff41fb8590bd81d9d37b21bd1b5708`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_PrivateShop(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void isDe(com.lineage.server.model.Instance.L1PcInstance, int, int)
private void isPc(com.lineage.server.model.Instance.L1PcInstance, int, int)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_PrivateShop(L1PcInstance,int,int)` | 140 | 140 |
| `private void isDe(L1PcInstance,int,int)` | 140 | 140 |
| `private void isPc(L1PcInstance,int,int)` | 140 | 140 |

<a id="s-rangeskill"></a>

### S_RangeSkill

來源 `com/lineage/server/serverpackets/S_RangeSkill.java`。

- 來源 SHA-256 `2e6cce916e7b43835793ae55f346284a94e078a5699e3779542a86d96b8d635f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RangeSkill](server-protocol.md#s-rangeskill)。
- class entry SHA-256 `07040c56853df208b247ed605fa37fd9c150481c525ddb9002b3ec5bfb3933cb`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RangeSkill(com.lineage.server.model.L1Character, java.util.ArrayList<com.lineage.server.model.skill.TargetStatus>, int, int, int)
public com.lineage.server.serverpackets.S_RangeSkill(com.lineage.server.model.L1Character, java.util.ArrayList<com.lineage.server.model.skill.TargetStatus>, int, int, int, boolean)
private static int calcheading(int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RangeSkill(L1Character,ArrayList<TargetStatus>,int,int,int,boolean)` | 42 | 42 |

<a id="s-redmessage"></a>

### S_RedMessage

來源 `com/lineage/server/serverpackets/S_RedMessage.java`。

- 來源 SHA-256 `db392b94ff625e3f1da6e14b90d29271be946d49aefd2117a545a1e846e056ef`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RedMessage](server-protocol.md#s-redmessage)。
- class entry SHA-256 `b55f177d390008cdc2412039ae624f717c1800e99f4a91fc78be1c225fc2f11a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RedMessage(java.lang.String, java.lang.String)
private void buildPacket(java.lang.String, java.lang.String[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(String,String[])` | 0 | 0 |

<a id="s-removeobject"></a>

### S_RemoveObject

來源 `com/lineage/server/serverpackets/S_RemoveObject.java`。

- 來源 SHA-256 `a3e3e0d61401bf29e702ddfd8055a54abdd170b064da24381748c12763bec5a1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RemoveObject](server-protocol.md#s-removeobject)。
- class entry SHA-256 `f957ace300bf2666a77aa7de284db481a8993360e1af7eeb6460183307f55dd8`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RemoveObject(com.lineage.server.model.L1Object)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RemoveObject(L1Object)` | 120 | 120 |

<a id="s-resurrection"></a>

### S_Resurrection

來源 `com/lineage/server/serverpackets/S_Resurrection.java`。

- 來源 SHA-256 `736617a045c801347d74f8434c96d8f096a0c3375f33c4b4d082b5ae7a109c20`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Resurrection](server-protocol.md#s-resurrection)。
- class entry SHA-256 `b8072191b5e6f65680533f7e56c471a5222accacb314a11131f260d02e099516`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Resurrection(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.L1Character, int)
public com.lineage.server.serverpackets.S_Resurrection(com.lineage.server.model.L1Character, com.lineage.server.model.L1Character, int)
public com.lineage.server.serverpackets.S_Resurrection(com.lineage.server.model.Instance.L1PcInstance, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Resurrection(L1PcInstance,L1Character,int)` | 85 | 85 |
| `public S_Resurrection(L1Character,L1Character,int)` | 85 | 85 |
| `public S_Resurrection(L1PcInstance,int,int)` | 動態 | 動態 |

<a id="s-retrievechalist"></a>

### S_RetrieveChaList

來源 `com/lineage/server/serverpackets/S_RetrieveChaList.java`。

- 來源 SHA-256 `1a4e59f517df13fac094b32d6d5e0a4dae1a1d215c4fd571512d41ca5d16c877`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RetrieveChaList](server-protocol.md#s-retrievechalist)。
- class entry SHA-256 `3c93e9bfa8d95c9ad81f79a76b2111c7a5e27859da84f33b6908b944709c7eda`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RetrieveChaList(int, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RetrieveChaList(int,L1PcInstance)` | 176 | 176 |

<a id="s-retrievedevilkinglist"></a>

### S_RetrieveDevilKingList

來源 `com/lineage/server/serverpackets/S_RetrieveDevilKingList.java`。

- 來源 SHA-256 `afbd9c73ac7d5e3b5a242e95d416ad71b99613d5c45bda58dbfb52e927525a9a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RetrieveDevilKingList](server-protocol.md#s-retrievedevilkinglist)。
- class entry SHA-256 `7977b92a65683c734949ace515b1b0006a80697f3e5f9a3207954fca7848d319`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RetrieveDevilKingList(com.lineage.server.model.Instance.L1PcInstance, int, java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RetrieveDevilKingList(L1PcInstance,int,List<L1ItemInstance>)` | 176 | 176 |

<a id="s-retrieveelflist"></a>

### S_RetrieveElfList

來源 `com/lineage/server/serverpackets/S_RetrieveElfList.java`。

- 來源 SHA-256 `9ac004532d09eb87a2ee9b7afb2a52f860029b6b5896920e6e9b5dd134a666b5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RetrieveElfList](server-protocol.md#s-retrieveelflist)。
- class entry SHA-256 `a4026764179eee9ec100146c06a8aadb64a852a2b4059935518a29c307cab933`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RetrieveElfList(int, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RetrieveElfList(int,L1PcInstance)` | 176 | 176 |

<a id="s-retrievelist"></a>

### S_RetrieveList

來源 `com/lineage/server/serverpackets/S_RetrieveList.java`。

- 來源 SHA-256 `1bdf02501d592d5658ed3e5a3d677ae592ff488807470d3e1e7c6bb1c6779f57`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RetrieveList](server-protocol.md#s-retrievelist)。
- class entry SHA-256 `d644779d98ddcbaee77b5817b4ebb23905a14a91cb3edd2e2f7dab8884f3d6ff`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RetrieveList(int, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RetrieveList(int,L1PcInstance)` | 176 | 176 |

<a id="s-retrievepledgelist"></a>

### S_RetrievePledgeList

來源 `com/lineage/server/serverpackets/S_RetrievePledgeList.java`。

- 來源 SHA-256 `83be9d6358b994fb24a66642293f60dce32e47db6bd71ac5f3f7c46cb4814f05`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RetrievePledgeList](server-protocol.md#s-retrievepledgelist)。
- class entry SHA-256 `7671e0c6f1b17656cf710e657816dcadb2ff2874c7c599a665816a427444e56b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RetrievePledgeList(int, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RetrievePledgeList(int,L1PcInstance)` | 176 | 176 |

<a id="s-retrievepoweritem"></a>

### S_RetrievePowerItem

來源 `com/lineage/server/serverpackets/S_RetrievePowerItem.java`。

- 來源 SHA-256 `387f8f0778f2f959590e69e07f3c97656269820f12174423d5c3e360340d5ab5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RetrievePowerItem](server-protocol.md#s-retrievepoweritem)。
- class entry SHA-256 `6a19f0e39f30857d263bc9097b2239ed78f4dfcb69336bc556ae9bc743713cc2`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RetrievePowerItem(com.lineage.server.model.Instance.L1PcInstance, int, java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RetrievePowerItem(L1PcInstance,int,List<L1ItemInstance>)` | 176 | 176 |

<a id="s-retrieveshiftinglist"></a>

### S_RetrieveShiftingList

來源 `com/lineage/server/serverpackets/S_RetrieveShiftingList.java`。

- 來源 SHA-256 `5c9b42c1106c56c0e83dcfd38e8732addbf866b6c02317801aec0e9f1db575cd`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_RetrieveShiftingList](server-protocol.md#s-retrieveshiftinglist)。
- class entry SHA-256 `fe998a5b6a2da88fce711bcc7f72d7e6d2da1c57e68917dbe2e91b6b2da887f3`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_RetrieveShiftingList(com.lineage.server.model.Instance.L1PcInstance, java.util.List<com.lineage.server.model.Instance.L1ItemInstance>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_RetrieveShiftingList(L1PcInstance,List<L1ItemInstance>)` | 176 | 176 |

<a id="s-selecttarget"></a>

### S_SelectTarget

來源 `com/lineage/server/serverpackets/S_SelectTarget.java`。

- 來源 SHA-256 `501d17863698d9d4773e3a9eb5ad3af73cf3b4dbe1ce20a237f1f81e3df3f76b`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SelectTarget](server-protocol.md#s-selecttarget)。
- class entry SHA-256 `e7a9c0b49e9d648f7e6d7a0d93710e85042a4d80fa90b066ae730079dfe09b5f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SelectTarget(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SelectTarget(int)` | 236 | 236 |

<a id="s-sellhouse"></a>

### S_SellHouse

來源 `com/lineage/server/serverpackets/S_SellHouse.java`。

- 來源 SHA-256 `5099a1864602225664127c706649e0db1dd6543a6dd62e2eaeb12fd206f3ec26`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SellHouse](server-protocol.md#s-sellhouse)。
- class entry SHA-256 `51dd4df59f241d18d902dc4b50749e809ffc0fab43b5e69e0713fc1c47ff966c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_SellHouse(int, java.lang.String)
public com.lineage.server.serverpackets.S_SellHouse(int, java.lang.String, java.lang.Object)
private void buildPacket(int, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String)` | 136 | 136 |

<a id="s-servermessage"></a>

### S_ServerMessage

來源 `com/lineage/server/serverpackets/S_ServerMessage.java`。

- 來源 SHA-256 `6f2ae74bc885da8d7807c816b1e139629cb14924ca38e176a44f031d26bcaf92`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ServerMessage](server-protocol.md#s-servermessage)。
- class entry SHA-256 `c4312bebc53f5539fd3c9f04bc28f1e2cf45aafd55d8f6d303ba272629d09d01`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ServerMessage(java.lang.String)
public com.lineage.server.serverpackets.S_ServerMessage(int)
public com.lineage.server.serverpackets.S_ServerMessage(int, java.lang.String)
public com.lineage.server.serverpackets.S_ServerMessage(int, java.lang.String, java.lang.String)
public com.lineage.server.serverpackets.S_ServerMessage(int, java.lang.String, java.lang.String, java.lang.String)
public com.lineage.server.serverpackets.S_ServerMessage(int, java.lang.String[])
private void buildPacket(int, java.lang.String[])
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ServerMessage(String)` | 161 | 161 |
| `public S_ServerMessage(int)` | 71 | 71 |
| `private void buildPacket(int,String[])` | 71 | 71 |

<a id="s-serverversion"></a>

### S_ServerVersion

來源 `com/lineage/server/serverpackets/S_ServerVersion.java`。

- 來源 SHA-256 `6f7259cc9d3e03d53d85ea21cc6f01e84f96a2e177d88c3d1db816d3f657f6a4`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ServerVersion](server-protocol.md#s-serverversion)。
- class entry SHA-256 `8a7711e4e1b1d720d75ba6c69c8df09029745f9bb873b78a9912cc1d1f715a85`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ServerVersion()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ServerVersion()` | 139 | 139 |

<a id="s-shopbuylist"></a>

### S_ShopBuyList

來源 `com/lineage/server/serverpackets/S_ShopBuyList.java`。

- 來源 SHA-256 `3c2d3d1a37644c9eabeeb221282009dd8d9199ed1f166563235c5e703e922ff8`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopBuyList](server-protocol.md#s-shopbuylist)。
- class entry SHA-256 `d734b0650dc4cc7ed023db8036cf018e3594b24ff8835d34add3aaf163e16bad`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopBuyList(int, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopBuyList(int,L1PcInstance)` | 65 | 65 |

<a id="s-shopbuylistall"></a>

### S_ShopBuyListAll

來源 `com/lineage/server/serverpackets/S_ShopBuyListAll.java`。

- 來源 SHA-256 `0d48c5088a8700bbd09f7e1fd0c5e11aa0a8edfec713e16d4a10c4aca79d34d0`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopBuyListAll](server-protocol.md#s-shopbuylistall)。
- class entry SHA-256 `c467e513fa2f8b6a99f8ac6fd806f9fc537944e60968a290d5a5b23b2c4c4a68`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopBuyListAll(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
private java.util.Map<com.lineage.server.model.Instance.L1ItemInstance, java.lang.Integer> assessItems(com.lineage.server.model.L1PcInventory)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopBuyListAll(L1PcInstance,L1NpcInstance)` | 65 | 65 |

<a id="s-shopbuylistgam"></a>

### S_ShopBuyListGam

來源 `com/lineage/server/serverpackets/S_ShopBuyListGam.java`。

- 來源 SHA-256 `e147eadd5a2a94a61a215ff4c31a24fae2c9276f0dbed2d3a5ea8911033a5171`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopBuyListGam](server-protocol.md#s-shopbuylistgam)。
- class entry SHA-256 `000b70514c0dafcbd5831a060b9f6f6157fcf4038f7cfe7666ca6d3f74c2f84a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopBuyListGam(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.util.Map<java.lang.Integer, com.lineage.server.templates.L1Gambling>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopBuyListGam(L1PcInstance,L1NpcInstance,Map<Integer, L1Gambling>)` | 65 | 65 |

<a id="s-shopbuylistlotto"></a>

### S_ShopBuyListLotto

來源 `com/lineage/server/serverpackets/S_ShopBuyListLotto.java`。

- 來源 SHA-256 `e31c5cf1bc9190746a2e1ba388db0a1c76591285061577bf43a97842dccb60ea`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopBuyListLotto](server-protocol.md#s-shopbuylistlotto)。
- class entry SHA-256 `70f337d07c2bbbfd2e10c7f53ad94fae153528e7b420330c88d9d8bd85d18d49`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopBuyListLotto(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.util.Map<java.lang.Integer, java.lang.Long>)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopBuyListLotto(L1PcInstance,L1NpcInstance,Map<Integer, Long>)` | 65 | 65 |

<a id="s-shopselllist"></a>

### S_ShopSellList

來源 `com/lineage/server/serverpackets/S_ShopSellList.java`。

- 來源 SHA-256 `5dfec708356a1c95755295393d4b9cd3a984bf26fa16660d6bf1de87f873a551`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopSellList](server-protocol.md#s-shopselllist)。
- class entry SHA-256 `fc14c962c14531e8a4fe65d76d55fd29adaeae34f24faba2b0232d100e661985`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopSellList(int)
public com.lineage.server.serverpackets.S_ShopSellList(int, com.lineage.server.model.Instance.L1PcInstance)
public com.lineage.server.serverpackets.S_ShopSellList(com.lineage.server.model.Instance.L1NpcInstance)
public com.lineage.server.serverpackets.S_ShopSellList(com.lineage.server.model.Instance.L1NpcInstance, com.lineage.server.model.Instance.L1PcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopSellList(int,L1PcInstance)` | 70 | 70 |
| `public S_ShopSellList(L1NpcInstance,L1PcInstance)` | 70 | 70 |

<a id="s-shopselllistcn"></a>

### S_ShopSellListCn

來源 `com/lineage/server/serverpackets/S_ShopSellListCn.java`。

- 來源 SHA-256 `a1b6740c18f2fad53be86cc27c0e4ec75b6c4add8b012f679c651b082d79a50a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopSellListCn](server-protocol.md#s-shopselllistcn)。
- class entry SHA-256 `f2c20e73d8f459dc61ba8901ca23d327f71afd486ca0d8264e684ec8a985ae37`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopSellListCn(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
public com.lineage.server.serverpackets.S_ShopSellListCn(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, boolean)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, int, int, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,int,int,boolean)` | 70 | 70 |

<a id="s-shopselllistcnx"></a>

### S_ShopSellListCnX

來源 `com/lineage/server/serverpackets/S_ShopSellListCnX.java`。

- 來源 SHA-256 `1bfcdfd5afadd779df26640cfef11cc9b1d3c2435d31d4c56094354fb46bf2ad`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopSellListCnX](server-protocol.md#s-shopselllistcnx)。
- class entry SHA-256 `3ec0b3c406e90e95baf21798458b4512235d8b34d8e45e379b9d0c1061d06ae0`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopSellListCnX(com.lineage.server.model.Instance.L1PcInstance, int)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1PcInstance,int)` | 70 | 70 |

<a id="s-shopselllistgam"></a>

### S_ShopSellListGam

來源 `com/lineage/server/serverpackets/S_ShopSellListGam.java`。

- 來源 SHA-256 `6c655b3abe64eeccdfb9e5e98c8c0d9bc37604001ad78f2f66ee7bdd35af8d53`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopSellListGam](server-protocol.md#s-shopselllistgam)。
- class entry SHA-256 `f9ee991c71496ff1d2c3e699d5a06724c358f933266a2635110401fcb5ec1737`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopSellListGam(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopSellListGam(L1PcInstance,L1NpcInstance)` | 70 | 70 |

<a id="s-shopselllistlotto"></a>

### S_ShopSellListLotto

來源 `com/lineage/server/serverpackets/S_ShopSellListLotto.java`。

- 來源 SHA-256 `75e28cf1a29fad68113a4f6cb218cf3c79883fec4eb5d751beb1dbf0adf5ad9f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopSellListLotto](server-protocol.md#s-shopselllistlotto)。
- class entry SHA-256 `9a7e58c9516352817a732ab8bd1ff904ef11a7755bf3908c446e6567bc1e834f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopSellListLotto(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopSellListLotto(L1PcInstance,L1NpcInstance)` | 70 | 70 |

<a id="s-shopselllisttmp"></a>

### S_ShopSellListTmp

來源 `com/lineage/server/serverpackets/S_ShopSellListTmp.java`。

- 來源 SHA-256 `6c3cff310bbc4d89882f9e399a4ff7737a04224db30d552b6d792e990418f37d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopSellListTmp](server-protocol.md#s-shopselllisttmp)。
- class entry SHA-256 `86532c4118e7dec8e01154a6455509b3f59dca9dca830bb48c494747ea27a0da`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopSellListTmp(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopSellListTmp(int,int)` | 70 | 70 |

<a id="s-shopselllistx1"></a>

### S_ShopSellListX1

來源 `com/lineage/server/serverpackets/S_ShopSellListX1.java`。

- 來源 SHA-256 `58c0d2740fb2b9e3a7548d4a7fecb97503c00fc1e0e72f5f4b496b514c6bb948`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShopSellListX1](server-protocol.md#s-shopselllistx1)。
- class entry SHA-256 `7cc67324233376bbe89e281f325f630696162fc31242b7843933fccf939af85c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShopSellListX1(int)
public com.lineage.server.serverpackets.S_ShopSellListX1(com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShopSellListX1(int)` | 70 | 70 |
| `public S_ShopSellListX1(L1NpcInstance)` | 70 | 70 |

<a id="s-showdrop"></a>

### S_ShowDrop

來源 `com/lineage/server/serverpackets/S_ShowDrop.java`。

- 來源 SHA-256 `b58cb29be61c3d588e95b9f7a296cee4da6dbe89800118260c6053b0691acf93`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShowDrop](server-protocol.md#s-showdrop)。
- class entry SHA-256 `42a5bebe6ac71d7293737c3fb4a05106036327dda74057fc1800f633e71a9813`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShowDrop(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShowDrop(int,int)` | 250 | 250 |

<a id="s-showpolylist"></a>

### S_ShowPolyList

來源 `com/lineage/server/serverpackets/S_ShowPolyList.java`。

- 來源 SHA-256 `a6e44a8e8101569d36953f8a413d672854c9d3ec7e93cc0b74a809b3f3bd35fb`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShowPolyList](server-protocol.md#s-showpolylist)。
- class entry SHA-256 `3921c79bf3015894e8e88a8339430fb09fe8decdf0c960002db81214dc559120`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShowPolyList(int)
public com.lineage.server.serverpackets.S_ShowPolyList(com.lineage.server.model.L1Character)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShowPolyList(int)` | 39 | 39 |
| `public S_ShowPolyList(L1Character)` | 39 | 39 |

<a id="s-showsummonlist"></a>

### S_ShowSummonList

來源 `com/lineage/server/serverpackets/S_ShowSummonList.java`。

- 來源 SHA-256 `18c966548e91c9b47a338a0151278a828ababc191b3e8884e18d66a704f11146`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ShowSummonList](server-protocol.md#s-showsummonlist)。
- class entry SHA-256 `1c85d568cc71ca9046a06e113272fd77e9ee86b4b8f7ac6135f244dff88d213f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ShowSummonList(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ShowSummonList(int)` | 39 | 39 |

<a id="s-skillbrave"></a>

### S_SkillBrave

來源 `com/lineage/server/serverpackets/S_SkillBrave.java`。

- 來源 SHA-256 `de52229b7de055ed83eae2f7dd38b73258611c947e03ffd23c96b31cbc56ba80`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillBrave](server-protocol.md#s-skillbrave)。
- class entry SHA-256 `f36fd8e62369f6ddb67102871e57e384126dc8fe876d12a4508cdd3854e3fc5e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SkillBrave(int, int, int)
public com.lineage.server.serverpackets.S_SkillBrave(int, int)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillBrave(int,int,int)` | 67 | 67 |
| `public S_SkillBrave(int,int)` | 67 | 67 |

<a id="s-skillbuy"></a>

### S_SkillBuy

來源 `com/lineage/server/serverpackets/S_SkillBuy.java`。

- 來源 SHA-256 `09628d128beb0187b8bf2e26bc17c27fddf995bc9c6bdca0f5e8d1dc03e414de`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillBuy](server-protocol.md#s-skillbuy)。
- class entry SHA-256 `2bcdd77473b55f1aa6bf0cda75f5fd36bcc22cabc8dd7c6ff82d93037cb08d5b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public com.lineage.server.serverpackets.S_SkillBuy(com.lineage.server.model.Instance.L1PcInstance, java.util.ArrayList<java.lang.Integer>)
public com.lineage.server.serverpackets.S_SkillBuy(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.util.ArrayList<java.lang.Integer>)
public com.lineage.server.serverpackets.S_SkillBuy(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance, java.util.ArrayList<java.lang.Integer>, com.lineage.server.model.npc.UiOperationContext.Ticket)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillBuy(L1PcInstance,com.lineage.server.model.Instance.L1NpcInstance,ArrayList<Integer>)` | 41 | 41 |

<a id="s-skillbuycn"></a>

### S_SkillBuyCN

來源 `com/lineage/server/serverpackets/S_SkillBuyCN.java`。

- 來源 SHA-256 `5c7fcd54829547198bf1d315b96517d3219a6079acbf6f59f225dcc73cc6d86f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillBuyCN](server-protocol.md#s-skillbuycn)。
- class entry SHA-256 `c19c43dce6b4707a8e5f41c6852b0f94f5f12893ff9e582ba0ebc78b9ce3d508`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_SkillBuyCN(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillBuyCN(L1PcInstance,L1NpcInstance)` | 39 | 39 |

<a id="s-skillbuyitem"></a>

### S_SkillBuyItem

來源 `com/lineage/server/serverpackets/S_SkillBuyItem.java`。

- 來源 SHA-256 `2652dfc43a61b96d384f7850c81d72ddeaaf3019f5499bd138ebaf8a8d7bfd67`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillBuyItem](server-protocol.md#s-skillbuyitem)。
- class entry SHA-256 `a9e56a3a241bd3009b7c9b557bb45143ba7f1e753fa5397f3cc71f2e8337c91e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.serverpackets.S_SkillBuyItem(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillBuyItem(L1PcInstance,L1NpcInstance)` | 13 | 13 |

<a id="s-skillbuyitemcn"></a>

### S_SkillBuyItemCN

來源 `com/lineage/server/serverpackets/S_SkillBuyItemCN.java`。

- 來源 SHA-256 `c42d71cf4751f85cfb1a22a307f59cd9e92d424a5885f73e95f56fd97e07e899`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillBuyItemCN](server-protocol.md#s-skillbuyitemcn)。
- class entry SHA-256 `a1c66e58792aace23e0ed4cf1d83360cbe2f0eec52f80b311339ca0f88996646`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SkillBuyItemCN(com.lineage.server.model.Instance.L1PcInstance, com.lineage.server.model.Instance.L1NpcInstance)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillBuyItemCN(L1PcInstance,L1NpcInstance)` | 13 | 13 |

<a id="s-skillhaste"></a>

### S_SkillHaste

來源 `com/lineage/server/serverpackets/S_SkillHaste.java`。

- 來源 SHA-256 `365938971888140370dafa5b219ce48dcbc12a8ed30c4bd53b551d09badac09c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillHaste](server-protocol.md#s-skillhaste)。
- class entry SHA-256 `65d33e67194b603f22f323782566475ca33b2534d4659fc54891de6a84df5003`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SkillHaste(int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillHaste(int,int,int)` | 255 | 255 |

<a id="s-skilliconblessofeva"></a>

### S_SkillIconBlessOfEva

來源 `com/lineage/server/serverpackets/S_SkillIconBlessOfEva.java`。

- 來源 SHA-256 `43eef6d336e1a0ba66305aaf1cb0a763445279ce8aeec78f16f3ee7ea20c311d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillIconBlessOfEva](server-protocol.md#s-skilliconblessofeva)。
- class entry SHA-256 `ee01391c1e284ccf0c648761bc2f5b415170aabec6150de897c0edc9319a83a6`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SkillIconBlessOfEva(int, int)
public byte[] getContent()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillIconBlessOfEva(int,int)` | 126 | 126 |

<a id="s-skilliconshield"></a>

### S_SkillIconShield

來源 `com/lineage/server/serverpackets/S_SkillIconShield.java`。

- 來源 SHA-256 `42b6de9985acd196d51f278278d66f513e98081529e8f1fe180b1cfc3a8c05a8`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillIconShield](server-protocol.md#s-skilliconshield)。
- class entry SHA-256 `c61c4cf69f20dfbbbe946bd6196a2c64a101d856374699b88821af8b2078b36e`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SkillIconShield(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SkillIconShield(int,int)` | 216 | 216 |

<a id="s-skillsound"></a>

### S_SkillSound

來源 `com/lineage/server/serverpackets/S_SkillSound.java`。

- 來源 SHA-256 `d59df752b4bacbd0158d9962afed424cc9c876d348d1154f633ac0e38b986ccb`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SkillSound](server-protocol.md#s-skillsound)。
- class entry SHA-256 `58cbc861e1a6a29122d263cc0f7ffe4706a3f9d73baeb7fdd8b8c3db20e928d7`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SkillSound(int, int)
private void buildPacket(int, int)
public com.lineage.server.serverpackets.S_SkillSound(int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int)` | 55 | 55 |
| `public S_SkillSound(int,int,int)` | 55 | 55 |

<a id="s-sound"></a>

### S_Sound

來源 `com/lineage/server/serverpackets/S_Sound.java`。

- 來源 SHA-256 `b7cae793e3c64f562fc4655ee6dba153f30ce55ae86ce072078bd73f3e4bd06f`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Sound](server-protocol.md#s-sound)。
- class entry SHA-256 `462dc98791b301dfb630f5f9611a4c4d9da9751c2431fea336b6d376a2918e0a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Sound(int)
public com.lineage.server.serverpackets.S_Sound(int, int)
private void buildPacket(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int)` | 22 | 22 |

<a id="s-spmr"></a>

### S_SPMR

來源 `com/lineage/server/serverpackets/S_SPMR.java`。

- 來源 SHA-256 `ab57b5a80b8e4406ddea06e0258071caba472337548cfec2df9927782d6f2823`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SPMR](server-protocol.md#s-spmr)。
- class entry SHA-256 `87793a75a5ee7a3cf76f3506321c14886a4d806f8793daba00e387bbc7ca641d`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SPMR(com.lineage.server.model.Instance.L1PcInstance, boolean)
public com.lineage.server.serverpackets.S_SPMR(com.lineage.server.model.Instance.L1PcInstance)
private void buildPacket(com.lineage.server.model.Instance.L1PcInstance)
private void writeSp(int)
private int addLegacyIntSpBonus(int, int)
private void writeMr(int)
public com.lineage.server.serverpackets.S_SPMR()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SPMR(L1PcInstance,boolean)` | 37 | 37 |
| `private void buildPacket(L1PcInstance)` | 37 | 37 |
| `private void writeSp(int)` | 動態 | 動態 |
| `private void writeMr(int)` | 動態 | 動態 |
| `public S_SPMR()` | 37 | 37 |

<a id="s-strup"></a>

### S_Strup

來源 `com/lineage/server/serverpackets/S_Strup.java`。

- 來源 SHA-256 `e56401fbaa79cb5fbce24c182efd44ca96158495e5090481083dd1e68e60fa0c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Strup](server-protocol.md#s-strup)。
- class entry SHA-256 `6b5d761d8b9f03a34838bbf505cfea538d60d71f4c43683c88fcfe0a6091971a`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Strup(com.lineage.server.model.Instance.L1PcInstance, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Strup(L1PcInstance,int,int)` | 166 | 166 |

<a id="s-systemmessage"></a>

### S_SystemMessage

來源 `com/lineage/server/serverpackets/S_SystemMessage.java`。

- 來源 SHA-256 `5b137ec3683e6170bc4ebc960c290cd672d24513d8bfe9c7c69d02296ddc85ed`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_SystemMessage](server-protocol.md#s-systemmessage)。
- class entry SHA-256 `a7cbc563cc6138f509193ba260a94ca65fbb49f23955ed9ef71f4a71dcdd1eac`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_SystemMessage(java.lang.String)
public com.lineage.server.serverpackets.S_SystemMessage(java.lang.String, boolean)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_SystemMessage(String)` | 243 | 243 |
| `public S_SystemMessage(String,boolean)` | 161 | 161 |

<a id="s-taxrate"></a>

### S_TaxRate

來源 `com/lineage/server/serverpackets/S_TaxRate.java`。

- 來源 SHA-256 `2973e2e0b4c8914bce29bb6360755a2d1ad3c7e6cdf32b751da84d9a3fc021cf`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_TaxRate](server-protocol.md#s-taxrate)。
- class entry SHA-256 `16c0ffb614ad75504cb0783e477a04f3cf35994467f8445123771f33208b2479`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_TaxRate(int)
public com.lineage.server.serverpackets.S_TaxRate(int, com.lineage.server.model.npc.UiOperationContext.Offer, com.lineage.server.model.npc.UiOperationContext.Ticket)
public com.lineage.server.model.npc.UiOperationContext.Offer uiOffer()
public com.lineage.server.model.npc.UiOperationContext.Ticket uiPrerequisite()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_TaxRate(int,Offer,Ticket)` | 185 | 185 |

<a id="s-teleport"></a>

### S_Teleport

來源 `com/lineage/server/serverpackets/S_Teleport.java`。

- 來源 SHA-256 `9080faaf3aaae8c2be0e3c63a15de74119f5831b40293af234b4f3b13e5b7f0c`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Teleport](server-protocol.md#s-teleport)。
- class entry SHA-256 `5ad4f22bde87bc280f34b32a8dc67c55dc4333f7b93d3e0aaf370c89473f6585`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Teleport()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Teleport()` | 149 | 149 |

<a id="s-teleport2"></a>

### S_Teleport2

來源 `com/lineage/server/serverpackets/S_Teleport2.java`。

- 來源 SHA-256 `4fdbeb813375129256f1fa5811896ae49834563dfd1a87698b6a27cc35219430`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Teleport2](server-protocol.md#s-teleport2)。
- class entry SHA-256 `2007c37d19ebb54817d71c6603bf1962cf27c0bfe5ed79faa717a9c3c58bc0c1`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Teleport2(int, int, com.lineage.server.model.Instance.L1PcInstance, long, long)
public boolean isValidForWrite()
public com.lineage.server.serverpackets.S_Teleport2(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Teleport2(int,int)` | 149 | 149 |

<a id="s-togmmessage"></a>

### S_ToGmMessage

來源 `com/lineage/server/serverpackets/S_ToGmMessage.java`。

- 來源 SHA-256 `1bac008f244c17234b57f450a8e1754dd127e49cad8f0646fc415cd0a8faf87e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_ToGmMessage](server-protocol.md#s-togmmessage)。
- class entry SHA-256 `c836449e0235e3d86019451ebbedce42aff8208a0b079ab0399698a467033256`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_ToGmMessage(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_ToGmMessage(String)` | 161 | 161 |

<a id="s-trade"></a>

### S_Trade

來源 `com/lineage/server/serverpackets/S_Trade.java`。

- 來源 SHA-256 `c055c7f2c4cc31ad852c7aa0e9344c415d2aa454b1fd5dccdd1e11a77562f43e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Trade](server-protocol.md#s-trade)。
- class entry SHA-256 `61aedcf07c70527a0ce8d7c3c9fe8abe55f5bdb24d92963fcfcede667dfe8b8f`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Trade(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Trade(String)` | 52 | 52 |

<a id="s-tradeadditem"></a>

### S_TradeAddItem

來源 `com/lineage/server/serverpackets/S_TradeAddItem.java`。

- 來源 SHA-256 `0a04d8f3c942a160e0ebfbb95f82f6e68837bd4d105828ecc08d09c7a5a5a00d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_TradeAddItem](server-protocol.md#s-tradeadditem)。
- class entry SHA-256 `8be6a28ad1ba67abfc4cef46aec77f113ebf2272eb17f1808575e60db6f1d470`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_TradeAddItem(com.lineage.server.model.Instance.L1ItemInstance, long, int)
public com.lineage.server.serverpackets.S_TradeAddItem()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_TradeAddItem(L1ItemInstance,long,int)` | 35 | 35 |
| `public S_TradeAddItem()` | 35 | 35 |

<a id="s-tradestatus"></a>

### S_TradeStatus

來源 `com/lineage/server/serverpackets/S_TradeStatus.java`。

- 來源 SHA-256 `f793630e477aa9635d6467766babf3468addcc6e011554e98d674b23c0d5316e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_TradeStatus](server-protocol.md#s-tradestatus)。
- class entry SHA-256 `12e8981f5d93d764bcd204ccdba67eb3ec247a89fbe37d6ba8d3c6fd3c386987`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_TradeStatus(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_TradeStatus(int)` | 112 | 112 |

<a id="s-trap"></a>

### S_Trap

來源 `com/lineage/server/serverpackets/S_Trap.java`。

- 來源 SHA-256 `4c982c7edd5b38294d45f270a8c4d6e9ee290dd1bbb4b47f5eb69de09a70bf3e`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Trap](server-protocol.md#s-trap)。
- class entry SHA-256 `ba63c510cca8eb7df1d2d41375880bacb7c9e16de4a47b04357292c515ad1cbb`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Trap(com.lineage.server.model.Instance.L1TrapInstance, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Trap(L1TrapInstance,String)` | 87 | 87 |

<a id="s-truetarget"></a>

### S_TrueTarget

來源 `com/lineage/server/serverpackets/S_TrueTarget.java`。

- 來源 SHA-256 `8814433e64d0eee71d844d9f3fa48a935b62daeda5f540c0ebc2c31ec0a9c7a5`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_TrueTarget](server-protocol.md#s-truetarget)。
- class entry SHA-256 `835af434836c8c9d8bc0cead688b13c37b1a60aaf66f1eb9d0221a68bdfab5ac`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_TrueTarget(int, int, java.lang.String)
private void buildPacket(int, int, java.lang.String)
public com.lineage.server.serverpackets.S_TrueTarget(int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,int,String)` | 11 | 11 |
| `public S_TrueTarget(int,int)` | 11 | 11 |

<a id="s-unknown-b"></a>

### S_Unknown_B

來源 `com/lineage/server/serverpackets/S_Unknown_B.java`。

- 來源 SHA-256 `7bc8e6b38ef6680bdd0203d09d526c9287c77eee0470bb5ad7da7de68c0fb508`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Unknown_B](server-protocol.md#s-unknown-b)。
- class entry SHA-256 `cb6009df80b3d350ccbd77650da238e721ee677d3af11cac90bc45197c3fa596`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Unknown_B()
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_Unknown_B()` | 64 | 64 |

<a id="s-usearrowskill"></a>

### S_UseArrowSkill

來源 `com/lineage/server/serverpackets/S_UseArrowSkill.java`。

- 來源 SHA-256 `870d1757e281e346a635c52e6f3b4cf8b5dc22a06959ff5c88a1029bc6a0a769`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_UseArrowSkill](server-protocol.md#s-usearrowskill)。
- class entry SHA-256 `c8133d3a76daba30590bd5dbc54ed288a58591c54269ff35b2c1f4fd5cf794b6`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_UseArrowSkill(com.lineage.server.model.L1Character, int, int, int, int, int)
public com.lineage.server.serverpackets.S_UseArrowSkill(com.lineage.server.model.L1Character, int, int, int, int, int, int)
public com.lineage.server.serverpackets.S_UseArrowSkill(com.lineage.server.model.L1Character, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_UseArrowSkill(L1Character,int,int,int,int,int)` | 30 | 30 |
| `public S_UseArrowSkill(L1Character,int,int,int,int,int,int)` | 30 | 30 |
| `public S_UseArrowSkill(L1Character,int,int,int)` | 30 | 30 |

<a id="s-useattackskill"></a>

### S_UseAttackSkill

來源 `com/lineage/server/serverpackets/S_UseAttackSkill.java`。

- 來源 SHA-256 `83e00ecf1da363510643ae259d8da3c43d99508fef9edb36fa9dacf74403dcfe`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_UseAttackSkill](server-protocol.md#s-useattackskill)。
- class entry SHA-256 `d11f5d72b06e10353c468506263adb66b80c7bec1fbef523928e21b961578f25`。
- 類別初始化器 `<clinit>()V` 存在。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_UseAttackSkill(com.lineage.server.model.L1Character, int, int, int, int, int, boolean)
public com.lineage.server.serverpackets.S_UseAttackSkill(com.lineage.server.model.L1Character, int, int, int, int, int, int)
private void buildPacket(com.lineage.server.model.L1Character, int, int, int, int, int, int, boolean)
private static int calcheading(int, int, int, int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(L1Character,int,int,int,int,int,int,boolean)` | 30 | 30 |

<a id="s-war"></a>

### S_War

來源 `com/lineage/server/serverpackets/S_War.java`。

- 來源 SHA-256 `a9eea0aa678c021127f29bdb53cb52f7e04b6243cc46a9e7ad322a5e4812b348`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_War](server-protocol.md#s-war)。
- class entry SHA-256 `4e4643bbb2c68b5899b061eb09a9545cf3558c8603a3379be20a0cfcffb52bb2`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_War(int, java.lang.String, java.lang.String)
private void buildPacket(int, java.lang.String, java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int,String,String)` | 84 | 84 |

<a id="s-wartime"></a>

### S_WarTime

來源 `com/lineage/server/serverpackets/S_WarTime.java`。

- 來源 SHA-256 `98f74c6634ee3e2be0529ba3a5f2d8ac88bb02cfb363ef3937e9f523fef1401a`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_WarTime](server-protocol.md#s-wartime)。
- class entry SHA-256 `4f147052e8b212a30b84802ddf6d8784b88a4b265a27cc5f6714406f631a34fa`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_WarTime(java.util.Calendar)
public com.lineage.server.serverpackets.S_WarTime(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_WarTime(Calendar)` | 0 | 0 |
| `public S_WarTime(int)` | 動態 | 動態 |

<a id="s-weaponrange"></a>

### S_WeaponRange

來源 `com/lineage/server/serverpackets/S_WeaponRange.java`。

- 來源 SHA-256 `77f1fc1074a4de34bd4f76f64f3f4b82c566758987cf176b24dcdd1024c77ca1`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_WeaponRange](server-protocol.md#s-weaponrange)。
- class entry SHA-256 `0c3b76e028ee82df4c938e4af77274f8d5d8c05b507a731b0cb024be74c11e1c`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_WeaponRange(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_WeaponRange(int)` | 250 | 250 |

<a id="s-weather"></a>

### S_Weather

來源 `com/lineage/server/serverpackets/S_Weather.java`。

- 來源 SHA-256 `4e1d228c83c39bc5b034e0acff23732ce019530add9cf0b4aa664897e5aa2889`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_Weather](server-protocol.md#s-weather)。
- class entry SHA-256 `8f8469538c388e5412f49018499d0fa9a4b79ac469a3f865d8697ead98b6a24b`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_Weather(int)
private void buildPacket(int)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `private void buildPacket(int)` | 115 | 115 |

<a id="s-whoamount"></a>

### S_WhoAmount

來源 `com/lineage/server/serverpackets/S_WhoAmount.java`。

- 來源 SHA-256 `53818c57e720d11e3016f9219dabddf01fe020ddf66c45ff023e92c271bff66d`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_WhoAmount](server-protocol.md#s-whoamount)。
- class entry SHA-256 `6bcff30f4fa10834b0c984fc1da354bbdf3c47ef611b2de6240460be82b3fb50`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_WhoAmount(java.lang.String)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_WhoAmount(String)` | 71 | 71 |

<a id="s-whocharinfo"></a>

### S_WhoCharinfo

來源 `com/lineage/server/serverpackets/S_WhoCharinfo.java`。

- 來源 SHA-256 `31cc89b1056970582818228273883dfdeacdd57666fb7c688d96c38c2aee5f15`，與 #132 相符。
- 核對結果為簽章相符。#132 索引連結見 [S_WhoCharinfo](server-protocol.md#s-whocharinfo)。
- class entry SHA-256 `982ec3ac84a38b22abe239f354f5cda18a9ccf1149e8bacb38c00d8e278bceb7`。
- 類別初始化器 `<clinit>()V` 無宣告。

共同宣告方法

```text
public com.lineage.server.serverpackets.S_WhoCharinfo(com.lineage.server.model.L1Character)
public byte[] getContent()
public java.lang.String getType()
```

| 首個直接 writeC 的方法 | 來源 | JAR |
|---|---|---|
| `public S_WhoCharinfo(L1Character)` | 71 | 71 |

## 正文內部封包補核

### Teleportation.S_TeleportRecovery

來源 `com/lineage/server/utils/Teleportation.java`，#132「容易混淆的共用 opcode」提及此內部封包。

- 來源 SHA-256 `2108857a47e620d35fa06b2b6dfc0ff867dcd97a913fcc57616565ba8a27e38b`，與 #132 規則表所列相符。
- class entry `com/lineage/server/utils/Teleportation$S_TeleportRecovery.class`，SHA-256 `42a6e57d382629a992ed7014dbe4be57568024f2ce4d7518796f106729bffe81`。
- 以下 4 個方法兩側簽章相符。此類別沿用被包裝封包的 opcode，包裝行為留待差異 1 的後續任務核對。

```text
com.lineage.server.utils.Teleportation.S_TeleportRecovery(com.lineage.server.serverpackets.ServerBasePacket, java.util.function.BooleanSupplier)
public boolean isValidForWrite()
public byte[] getContent()
public java.lang.String getType()
```

## 已驗證、推測與待驗證

- 已驗證項包括 346 個來源檔完整覆蓋、來源雜湊相符、d287 JAR 身分、334 個方法集合相符、2 個方法集合差異、9 個缺席類別、1 個停用來源、219 個全域 opcode 常數、96 個具名 C 分派，以及內部 recovery 類別的 4 個方法。
- 推測項為來源快照包含 d287 之後或另一分支的功能增補。確切先後順序與建置提交需建置溯源證據支持。
- 待驗證項包括完整方法本文、所有封包布局與條件、設定值、資料表內容、目前程序載入的映像、登入與遊戲行為。本輪核定的是磁碟 JAR 身分。
- 下一棒由 Claude／調度器審查差異對照，M6 code 執行者逐項採用部署適用的入口與 opcode。dot 在後續已授權的 P24Lab smoke 驗證實際流程。
