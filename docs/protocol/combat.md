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

## T093／#173 攻擊准入與靜默分支

2026-11-07 唯讀核對本機伺服器來源。以下行號為 Java 原始碼行號，offset 為解密 payload byte offset。來源身分沿用上表，新增依據列在本節末尾。

### 格式、距離及面向

- C229 的 offset 1 是目標世界 object ID（u32），offset 5／7 是目標 x／y（u16）。有效欄位共 9 bytes，客戶端補零成 16 bytes。C_Attack.start 第 98–117 行依序讀 D、H、H，允許尾隨 byte，拒絕任一零座標。CombatRules.Attack 的編碼符合此順序。
- C6 光箭的 offset 1／2 是 row=0、column=3，技能 ID 為 4，offset 3 是 object ID，offset 7／9 是目標 x／y，補零成 16 bytes。C_UseSkill.start 第 290–332 行讀 D、H、H，設 isAttack／checkLoc。MagicRules.Request 與之吻合。
- 近戰判定採伺服器當下角色／目標位置。L1AttackPc.calcHit 第 223–242 行用 Chebyshev 距離 `max(abs(dx),abs(dy))`，超過 `weaponRange+1` 判 miss。空手預設 range=1，普通近戰武器由模板取得 range。弓採 isInScreen，另檢查彈藥。客戶端本次對目前支援的近戰姿勢採 2 格上限。
- 光箭靜態模板 `db/380_db.sql` 第 48029 行的 ranged=10。L1SkillUse.makeTargetList 第 1703–1717 行超距離直接留下空目標集合。現有 MagicSpells 四項 catalog 沒有通用射程欄位，本次只為光箭加入 10 格門檻。
- C229、C6 都沒有 heading 欄位。L1AttackPc.action 第 1548 行、S_UseAttackSkill.buildPacket 由目標座標計算並設定面向。攻擊前的舊面向不構成這兩個處理器的拒絕條件。
- `PlayerTargetPositionFence.isAuthorized` 額外要求角色位置同步，玩家目標也須同步，同圖、同 showId，且在要求畫面可見時同時符合 known-set 與伺服器可見幾何。客戶端未收到逐步位置回包是既有協定行為，僅憑 HUD 顯示本地預測，無法判定這個伺服器同步旗標失敗。

### 靜默拒絕、miss 與無 S30 的條件

| 路徑 | 條件及結果 |
|---|---|
| C_Attack.start／performAttack | 幽靈、死亡、傳送、私商、AI／Follow、隱身／延遲、麻痺，格式不足／零座標、跨圖、潛地／飛行隱藏目標，鎖內目標已被替換、位置 fence 失敗，動作時長不存在、共用 timeline 或 accelerator 拒絕，均可提前返回。過重另回訊息 110。 |
| C_Attack 空擊 | 目標不存在或已死，通過動作准入後可送 target=0、damage=0 的 S30。有效活目標交 onAction，NPC 子類決定後續處理。 |
| L1AttackPc.calcHit／actionX2 | 超近戰距離通常送目標 S30、damage=0。glanceCheck 地形阻擋會送空擊 S30。命中機率、目標保護與傷害減免仍由伺服器決定。 |
| C_UseSkill.start／performSkill | 死亡、傳送、私商、技能 CD、非法／缺失技能模板、未學技能、目標不存在、位置 fence／鎖內身分失效可提前返回。過重、禁用地圖、麻痺／沉默／變身限制、AI／Follow 攻擊限制另有訊息。 |
| L1SkillUse | 目標類型／安全區等限制、射程與 LOS 在 isTarget／makeTargetList 處理，HP／MP／材料不足、技能 reuse 與共用動作 timeline 也可拒絕。isTarget 第 1466–1474 行拒絕攻擊技能 LOS 失敗。 |
| 光箭空目標集合 | PC 的空集合可通過 checkUseSkill，後續仍消耗資源。sendGrfx 第 2335–2350 行只播 S158 施法動作，隨即返回。此路徑可出現「C6、MP 下降、S158、無 S30」。 |

### 引擎修正與證據界線

Combat.cs 在正常 Attack／滑鼠入口、取得 sender 前檢查距離，超距離顯示目前格數與走近提示。Magic.cs 對光箭做同樣檢查。排隊期間自身／目標座標異動會取消舊請求，近戰補齊自身座標重核。拒絕不占冷卻、不播預測動作，走近後可明確重試。位置以目前 Actor 的正常移動預測計算，伺服器保留最終幾何及 LOS 裁定。

已確認的產品缺口為「把任意可選取目標當成可攻擊距離」以及近戰排隊缺少自身位置重核。#164 的舊 trace 近戰時窗有 C229×52、S30×18、S158×19。光箭量測窗有 C6×1、S158×1、S33×1、S30×0。舊資料沒有 object ID／傷害／攻擊者，無法證明 18 筆 S30 對應同一隻牛，也無法唯一還原每次伺服器拒絕條件。超距離／LOS 的空目標路徑與光箭觀測相符，歸因仍屬推測。距離修正已由離線測試驗證，歷史事件與實際擊殺仍待真機。

### 選用戰鬥追蹤

在啟動客戶端的同一個環境設定以下兩項。檔名必須尚未存在。

```powershell
$env:TW_PACKET_TRACE = 'scratch/packet-trace/m5-combat.jsonl'
$env:TW_PACKET_TRACE_COMBAT = '1'
```

原有 utc／dir／op／len／seq 保留。額外只允許下列數字，queue 保存純量，完成擷取後不持有封包 buffer。

| 封包 | 額外 JSON 欄位 |
|---|---|
| out C229，16 bytes | `target`，讀 offset 1 的 u32 |
| out C6，已支援的技能 1／3／4／8 | `target`，讀 offset 3 的 u32。D 形式允許 8／16 bytes，光箭 DHH 要求 16 bytes。 |
| in S30，24／40 bytes | `target`，讀 offset 6 的 u32，以及 `damage`，讀 offset 10 的 u16 |

其他 opcode、反方向、截斷封包及名稱／文字型 C6 使用原本五欄。只有環境值精確等於 `1` 才開啟擴充，未設定時維持五欄。未設定 TW_PACKET_TRACE 時沒有追蹤檔。

`damage` 是 wire 傷害欄，RO 模式通常是傷害低 16 位，公版近戰命中可固定為 10。0 可表示 miss 或零傷害。target=0 也可能是空擊／地形／目標受擊動畫抑制。因本次只記允許的兩個數字，仍須搭配單一目標的操作時窗、HP／EXP 與死亡回包判讀。

### 離線驗證

`engine/_integration/dev/demo-tests/CombatAdmissionChecks.cs` 以本次產品 DLL、合成的純 timing metadata、記憶體 sender 與禁止 socket 的 guard 執行。212 項檢查涵蓋 1／2／3 格近戰、四種近戰姿勢、滑鼠入口、10／11 格光箭、獨立 wire 解碼與條件對照、排隊位置／選取／死亡／換圖／斷線、冷卻與既有擊殺／EXP／學技／箭矢接收回歸。條件對照模型僅涵蓋格式、目標與幾何，實際服務、隨機命中、LOS 與 DB 留待真機。

`PacketTraceChecks` 的 3,468 項檢查涵蓋預設關閉、擴充啟用、數字 offset／零值／u32 與 u16 最大值、所有 256 opcode 雙方向白名單、文字型 C6、截斷與機密假字串、併發／離開 flush、路徑與 junction 拒絕。測試保留 scratch 證據，沒有遞迴刪除。

### 新增唯讀來源簽章

下列 Java 路徑相對 `D:\lineage開發\1-伺服器\src\com\lineage`。SQL 路徑相對伺服器根，僅查技能／武器／牛 NPC 靜態模板。C_Attack、C_UseSkill、Opcodes、S_AttackPacketPc 與 ServerBasePacket 的 SHA 與上表相同。

| 來源檔 | SHA-256 |
|---|---|
| `server/model/L1AttackMode.java` | `f29c4d0bcc85ca3e6399497fa45b97b14047d4161e32a02525940e14290ae2c2` |
| `server/model/L1AttackPc.java` | `3fdc94cedc6c3ec803e731be238093bd7fe55c432cf84be6822a079a1627ddfc` |
| `server/model/skill/L1SkillUse.java` | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |
| `server/types/Point.java` | `a8b342463e57e684f3382da9d0b5b51766c9ebe893c491024ef7babeb5ad801c` |
| `server/utils/PlayerTargetPositionFence.java` | `5aff67e033227d155772049aac9348c197a13ccb7cdead11c9992c2cedbd7618` |
| `server/utils/PcPositionSync.java` | `6c1ff6ec2bf53900520dc4aacd05c5513becc19a875392294957a3a17811f8d5` |
| `server/model/ClientActionTimeline.java` | `9e888bd225bb24636068aa9b411ca46b8cbd8f21caae132ef86c07260c022c3b` |
| `server/model/combat/PcPhysicalAttackFlow.java` | `00584ca2c3e108ae05448d3e09d16e68d486b48b1017c331e7fe8397046a2136` |
| `server/serverpackets/S_UseAttackSkill.java` | `83e00ecf1da363510643ae259d8da3c43d99508fef9edb36fa9dacf74403dcfe` |
| `db/380_db.sql` | `2afbc066fa7dbde391169050deeed206a20a39afbc0390378ddf2800cd3328b5` |
