# 戰鬥閉環封包契約（T072／#129）

查核日期：2026-11-06。唯讀來源：`D:\lineage開發\1-伺服器\src\com\lineage`；只記錄契約、方法簽章與 SHA-256，未複製伺服器程式或遊戲素材。這是目前本機原始碼的靜態結論，**不代表 P24Lab 執行中的 JAR 與本機來源完全一致**；T075 應核對實際回包。

## 線上格式

`C/S` 前綴表示方向，後面的數字是十進位 opcode；欄位記號 `u8/u16/u32` 表示 1/2/4 bytes，整數採 little-endian；`str` 是客戶端文字編碼的 NUL 結尾字串。表中為解密後 payload，包含 opcode，末尾補零至 8 bytes。位移若提及，一律是 **payload byte offset**，不是 File Offset／RVA／VA。

| 子流程 | 封包簽章／權威狀態 | 伺服器處理器與方法 |
|---|---|---|
| 近戰 | C229 `u32 target,u16 x,u16 y`；引擎每次明確操作只送一包 | `C_Attack.start(byte[],ClientExecutor)` → `performAttack(L1PcInstance,L1Object,int,int)`，重新檢查玩家、目標與動作時序 |
| 射擊 | C123 同上；武器動作類型 20 使用弓箭 | `C_AttackBow.start` → `performAttack`；空目標分支 `performEmptyAttack`；`arrowAction` 成功消耗才送投射物 |
| 近戰回報 | S30 `u8 action,u32 attacker,u32 target,u16 damage,u8 heading,u16 x=0,u16 y=0,u8 flags`，補至 24 bytes | `S_AttackPacketPc`／`S_AttackPacketNpc`；damage 受客戶端模式與回報方法影響，不能當作獨立 HP 差值 |
| 箭矢回報 | S30 前 13 bytes 同上，offset 13 為 `u32 338`，後接 `u16 gfx,u8 127,u16 sx,sy,tx,ty,u32 0,u8 0`，補至 40 bytes | `S_UseArrowSkill`；本引擎支援 action 1、gfx 66；沿用既有投射物飛行／到達受擊排程 |
| 死亡 | S158 `u32 object,u8 action=8`；S87 自身角色的武器欄 8 也是死亡外觀 | `S_DoActionGFX`、`S_OwnCharPack`；目標死亡清選取；自身死亡停移動、戰鬥與排隊輸入 |
| EXP | S113 `u8 level,u32 expLow,u8 1`；S8 提供基線 | `S_Exp(L1PcInstance)` → `ServerBasePacket.writeExp(long)` 僅寫低 32 bits；不從怪物死亡本地加 EXP |
| 自然掉落 | 地面物品 S87；拾取 C112 `u16 x,u16 y,u32 object,u32 count`；移除 S120 `u32 object`；入袋 S15 或更新 S24/S242 | `L1MonsterInstance.distributeExpDropKarma` → `CalcExp.calcExp`／`distributeDrop` → `DropShare`；`C_PickUpItem.start` 驗證拾取；依伺服器政策可能直接入袋或落地 |
| 技能清單 | C145 `u32 npc` → S41 `u32 basePrice,u16 count,{u32 skillIndex}` | `C_SkillBuy.start`、`S_SkillBuy`；index 是技能 ID 減 1；空清單可只有 opcode 加 u16 0，其 padding 也可由目前解析器讀成零基價、零筆數 |
| 學習 | C39 `u16 count,{u32 skillIndex}` → S164 | `C_SkillBuyOK.start`：清單授權、職業／等級、金額檢查後才授予；一般學費 basePrice × 技能等級平方（等級 16 的價格表為 0，停用） |
| 已學／移除 | S164／S160 `u8 tier,{u8 mask}`；28 或 32 組 mask，補至 32／40 bytes | `S_AddSkill`；tier 32/50/100 不是 byte count；ID=`row*8+bit+1`；每次回包增／刪集合，不能把增量當整份重置 |
| 施法 | C6 `u8 row,u8 column,u32 target`；光箭 4 再接 `u16 x,u16 y` | `C_UseSkill.start`：ID=`(row<<3)+column+1`；本次驗證既有技能 4，未擴張技能種類 |
| 彈藥 | S5 清單基線；S24 `u32 object,str name,u32 count,u8 statusLen,status`；S242 subtype 1 使用 u16 statusLen；S57 `u32 object` 移除 | `L1PcInventory.consumeProjectile(L1ItemInstance)` 透過序列化背包扣一發；`debitProjectileMemoryLocked`、`drainProjectilePersistence` 負責記憶體與延後保存；`S_ItemStatus` 發絕對數量 |
| 死亡後重新開始 | C177 無欄位，補至 8 bytes；接收 HP、S206 地圖、S87 自身與 S119 外觀 | `C_Restart.start` → `L1PcInstance.restartLikeC_Restart(int,int,short)`；伺服器決定恢復 HP 與返回位置；引擎按鈕只在死亡時可用，送出後不重送、不預先復活 |
| 原地復活 | S85 `u32 target,u8 type,u32 caster,u32 gfx`，補至 16 bytes；目前處理 type 0 | `S_Resurrection(L1PcInstance,L1Character,int)`、`C_Attr.start`、`L1Character.resurrect(int)`；S85 不帶 HP 或武器，必須等正 HP 與 S119／完整自身 S87，才能恢復移動 |

## 引擎處理與界線

- `Combat.cs` 保留單次攻擊與既有冷卻；可明確連續操作至擊殺，沒有自動追擊。死亡時增加外觀版本，取消死亡前已排隊的動作；一般自身 S87、HP 回升或傷害動畫都不能單獨解除死亡鎖定。C177 的換圖／自身包與 S85 才提供恢復路徑。
- `Ranged.cs` 從權威背包計算箭數；沒有支援的箭矢時停用射擊，真正送出前再檢查一次。點擊與 S30 都不扣箭。延遲的 S24 不會重新建立已移除的物品。支援範圍仍是既有 `$61` 普通箭指紋（desc 0、useType 254、icon 7、bless 1）；魔法弓／其他箭種不在此次擴充範圍。
- `Rewards.cs`／背包處理原本已是絕對回包更新，重複回包不重複加獎勵。高等級 EXP 高位無法由 wire 得知時顯示未知，不推算補值。
- `NpcServices.cs`／`Magic*.cs` 原有流程已能依 NPC offer 學技，待 S164 才加入可用技能；未改價格、職業限制或伺服器授權。離線測試涵蓋實際 Interact → offer → submit → S164 → C6。
- 地面拾取沿用已核定 `GroundCatalog` 與圖像證據。離線閉環使用合成的肉掉落；未知圖像／未核定 catalog 的掉落不冒充已完整支援，T075 必須選有合法自然掉落且可識別的怪物與物品。
- 新增 `Recovery.cs` 原生死亡操作面板、C177 與 S85；沒有實作他人施放復活時的 Y/N 提示回覆。本次死亡恢復 smoke 可走「死亡後重新開始」；S85 接收以離線回包驗證。

## 驗證與待驗證

合成封包測試：`engine/_integration/dev/demo-tests/CombatLoopChecks.cs`，透過既有 `DemoChecks` 執行本次產品 DLL、記憶體 sender 與 socket 禁用 guard，不呼叫遊戲入口、不顯示遊戲視窗。覆蓋逐次攻擊到死亡、EXP 去重、地面拾取、學技施放、單發與箭數、耗盡與排隊競態、重登背包基線、死亡／恢復／截斷封包／舊請求取消。

實際怪物是否掉落、實際學費扣除、箭矢畫面與音效、伺服器保存後重登箭數，以及原生死亡畫面與恢復後步行，均留給 T075 的 dot／使用者驗收。合成重登基線只證明客戶端接收新清單，不證明 DB 持久化。

## 唯讀來源簽章

以下 SHA-256 釘住本次讀取的檔案；相對於上列 `src/com/lineage`。未讀取 `config/server.properties`、DB 或服務狀態。

| 來源檔 | SHA-256 |
|---|---|
| `echo/OpcodesClient.java` | `3b9df4518cad4ab3021d7a6454f88fe4513bd715eb3eb461ffaa873a64fe8931` |
| `server/serverpackets/OpcodesServer.java` | `9044cb98cf8a0548a421fbb05e6cdcef26147b2a2b85a1ef896fde8a8016f152` |
| `server/clientpackets/C_Attack.java` | `3726df0441bcdf88e62a24e63bb81d36b78106f7d21131d571979d8cc3946324` |
| `server/clientpackets/C_AttackBow.java` | `cc2087e4c62a3d6c5d48ba9e8851a0cbfa317219e2225fa5e98932c3b47d6613` |
| `server/clientpackets/C_PickUpItem.java` | `804b4ba715c505f458c63e78f6cfd81f03c65c618ff3a1b506ad7da7a0772254` |
| `server/clientpackets/C_SkillBuy.java` | `ce89743248e65b2941c02bcc20f50610e442fa2364fbf7ecd75565cee8bc5111` |
| `server/clientpackets/C_SkillBuyOK.java` | `b8e4f9c9d7a35d87f2849b6f074143323740318f9cdb8d6ca6ac3dcbe2a0765a` |
| `server/clientpackets/C_UseSkill.java` | `4932c6dd71b330d91b75c4fcbf02c30ffa4712c35d7cb07537626159038e168e` |
| `server/clientpackets/C_Restart.java` | `69e421abeb5fe0bf168d179ea9c3719afee6fc8e41d04cf9696da14afcbab7b8` |
| `server/clientpackets/C_Attr.java` | `793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23` |
| `server/serverpackets/S_AttackPacketPc.java` | `8b1cf5a0e0cb00183dd2803b27161f6b8cc64a668b9a0ee761034e8d937bc3a1` |
| `server/serverpackets/S_AttackPacketNpc.java` | `aa612c0c22d3ca9dd6373ce170433f73cc8be3c1cb029edc8a1f7b53e656889d` |
| `server/serverpackets/S_UseArrowSkill.java` | `870d1757e281e346a635c52e6f3b4cf8b5dc22a06959ff5c88a1029bc6a0a769` |
| `server/serverpackets/S_Exp.java` | `ff2830f32bdc96b758dfde2f2b226449dc20451e6a0b896be52139b303901ea4` |
| `server/serverpackets/S_SkillBuy.java` | `09628d128beb0187b8bf2e26bc17c27fddf995bc9c6bdca0f5e8d1dc03e414de` |
| `server/serverpackets/S_AddSkill.java` | `13113864ecbe5af003216de30969abb8d1fc5515e961dbff32b2b24516fe0bdb` |
| `server/serverpackets/S_ItemStatus.java` | `6e69ff83cbbe91f7a8914a2e43bda498eb0d2fc5808e5218edea5cfe1d513eac` |
| `server/serverpackets/S_Resurrection.java` | `736617a045c801347d74f8434c96d8f096a0c3375f33c4b4d082b5ae7a109c20` |
| `server/serverpackets/ServerBasePacket.java` | `3348c199e9694bd1eec050b130f28ab99590cae16f0047964c17b32df309a439` |
| `server/model/Instance/L1MonsterInstance.java` | `5c7637688fbacee7ebdf052aef84fd458bf7dd392b0f011d47534317b188cec1` |
| `server/model/Instance/L1PcInstance.java` | `0d1f267891c99e662bf0695d799a50e302f42a7f52a0440945372a443ac05c92` |
| `server/model/L1Character.java` | `efde278d67f29468b2581188c6966a6d6ee9e7f6a87cc2d5650b624700410fa3` |
| `server/model/L1PcInventory.java` | `c8644b41c37fb818f60b20ea386eea158b13d75ed42f7b451e668256e6882505` |
| `server/model/drop/DropShare.java` | `a648a8f4abe1e4b90c3b9ba29c3b80251c9fc4b4ec63ce1f6974f751b2c9b8b3` |
