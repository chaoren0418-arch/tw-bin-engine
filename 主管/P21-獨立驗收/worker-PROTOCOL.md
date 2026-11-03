# P21 原弓箭協議與呈現

## 固定來源與範圍

原 JAR SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；沿用五 SQL，其中 380_db.sql SHA-256 `0875f7cff78d7d97a7380cc5d24f47b2c66b630d452377e5f8bfa6f3d29fd95e`。目前正式目錄的同名 SQL 不是本次固定版本。Java 是旁證，class/javap 與真實 wire 優先。類別、原始切片、索引、逐檔 hash 見 reference/ranged。

支援 gfx0 王族使用原 weapon172「弓」與 etcitem40743「箭」：bow/type1=20、weapon_type=4、range=-1、use_royal=1、非魔法弓；箭可堆疊，consume_on_attack=1、arrow_spr=66。背包箭指紋為 desc0/use254/icon7/normal wire bless1/name$61；normal DB bless0，不能把 name_id61 誤作 desc61。原箭 small6/large5，且原 tier0 雙手弓附加 small/large 各3；這些不是最終隨機傷害，產品不計算或覆寫原傷害。

## 請求與回包

所有多位元欄位 little endian；沿用原加密與 8-byte padding。

|方向|內容|位移與寬度|
|---|---|---|
|C→S|C123 bow attack，16 bytes|C opcode@0、D target@1、H targetX@5、H targetY@7，剩餘0|
|S→C|S30 arrow，40 bytes|C action@1、D attacker@2、D target@6、H damage@10、C heading@12、D338@13、H gfx66@17、C127@19、H sourceX@20、H sourceY@22、H targetX@24、H targetY@26、D0@28、C0@32、7 bytes0|
|S→C|原近戰 S30，24 bytes|保持 P20 解析，與 D338 箭分支分開；其他技能變體保留未支援|
|S→C|背包絕對狀態|S5/S15/S24/S242(sub1)/S57；只依原回包替換數量與移除物件|
|S→C|裝備／外觀|原 S100 名稱標記、S64 slot list/update、S119 weapon20；本地不預先裝備|

S30 的 damage 是原 unsigned16 欄位，不宣稱等於目標實際 HP 扣量。target0 合法，MISS／安全區 motion-only 也可能真正消耗一箭並播放箭；零傷害不代表唯一拒絕原因。無箭與 terrain-blocked 分支可能只有 S122 朝向與 S158/action1，不能要求不存在的錯誤包。未知 ID 不產生人物，截短包在任何狀態修改前拒絕。

原 `Point.isInScreen` 是 Manhattan<=18；>19 拒絕；=19 使用 `abs(tx-(x-18))+abs(ty-(y-18))` 介於19與52。原 LOS 由 JAR/map/glanceCheck 決定，客戶端不猜牆、不改原射程。動態超距證據是原 AOI S120 移除、清除選取、本機拒絕 stale target，未宣稱另外執行到原 calcHit 的超距分支。原阻擋分支具固定 bytecode 來源，未以合成結果冒充真實阻擋測試。

遊戲 G 與 Web 單次攻擊共用同一 admission。原弓攻擊間隔1200ms；本機因 Windows TickCount64 量化加入32ms輸入餘裕，不改原服時序。CLI r2 曾出現 UTC1196.139ms、TickCount1203ms且無新S30/扣箭，這支持時鐘競態的可能性，未宣稱有原服診斷直接證明。送出後不自動補發。忙碌、取消、換世代、死亡、移除、目標移動與武器切換在送出閘門再次檢查。

## 原圖與時間

原 BIN SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，以下均 VA，image base `0x00400000`。IDA 只分析隔離副本；未執行、附加原 BIN。

gfx0 weapon20 使用原3225 SPR：200–207 walk、208–215 breath、216–223 attack、224–231 damage、96–103 die。40個切片、8方向，保留原 block depth/pixels。攻擊每 frame 使用原整數 `1000*ticks/24`，總1249ms；`!` release marker 累計791ms，與 server admission1200ms分開。步行沿用原server640ms。

原 list.spr 與 list.spz #66 一致：`0.fly(1 3,0.2:1 0.0:1 0.1:1) 102.type(9) 109.effect(3 -1)`，8個66-0…7 SPR／24原frame。87C890解析type/effect與事件位元；930A20選effect3；7C36C0建立箭；A467A0 `!`→A46240→7C3950發射。

7C3950在release取當時仍存在的目標座標一次，否則保留封包座標。F1A190產生 signed16.16速度，internalX=2*worldX-32768；F1A340決定原8方向。7C3AB0立即第一步，其後30ms；加0x8000取高16位，X清最低位，抵達即停止。frame欄先preset0/1/2，再A43560先increment，故一般顯示physical0、末步physical1，非三格循環。抵達frame不繪出。原算術81方向／402步另由主管直接執行原x86核對。

箭塊參與既有 map compositor 的位置／depth排序，並非全局頂層；箭不能成為選取目標。多個尚未結束的回報保留，不能被 LastProjectile 覆寫。世界世代重置清除。原7C3AB0→759B30→758050→A49520在抵達觸發傷害動作，產品視覺按到達時點顯示；HP／死亡仍即時以原回包為準，死亡不被較晚 hurt 動作復活。

這是固定角色／武器／箭與正常動作範圍的重建，不代表其他 gfx、魔法、加速或所有 BIN 特效／中斷動作均已實作。

## 有限彈藥與失敗保留

原 dorpitem=false，強制丟棄白名單只有40056。箭40743的 C25 實際回 S71/125、數量不變、無地物。依主管修正派工，先真遠距擊殺，再使用原 safety-zone `PcPhysicalAttackFlow.executeMotionOnly`→`L1AttackPc.action/actionX1`，每次原投射物回報確實扣一箭；安全區禁止傷害並不禁止此原動作耗箭。最後S57刪除及下一次只有原無箭動作，與丟棄拒絕分開驗證。

有限測試迴圈只存在 CLI/UI 控制器，逐次等待原回包後操作；產品沒有自動攻擊、耗箭、追怪、補血或拾取。失敗保留原 result/raw/SQL/process/executed-build；後續 Fresh=false 比對前次正常保存值，沒有補發、reset或換庫。

原箭地面圖已有來源素材與解析，但因原規則拒絕丟棄，本次沒有真實箭地物丟棄／拾取成功宣稱。P18肉拾取動畫未畫與舊UI8肉未證、P19無自然地物與真實升級、P10舊EXE0/11未驗收等歷史限制保留。
