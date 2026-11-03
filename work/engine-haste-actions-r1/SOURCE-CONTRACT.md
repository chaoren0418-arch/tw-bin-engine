# ENGINE-HASTE-ACTIONS-IMPL-R1 來源與政策

本候選只擴充 Speed1／Brave0 的既有角色、武器攻擊及技能1／3／4／8。基底為主管指定 r30，完整177個source/audio/web pins；唯一產品差異是 ActionCadence.cs。舊 constructor、Slow、SlowWaitMs、Capture、Frame 等 API 保留，正常與緩速路徑保留 r30 計時；其他動作閘門及生命週期函式未改。

## 原碼與適用範圍

- 固定 BIN SHA256：`e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。ImageBase `0x400000`；A407D0 的 RVA `0x6407D0`、File Offset `0x63FBD0`，1644 bytes SHA `df99bc065dd5014d263cc0ea9b59a50e659504454294d558ad2c956718b223af`。本輪讀回固定BIN核對既有10函式15 chunks，沒有重跑原native執行。
- 沿用獨立準備的270向量，其中Speed1共90列：action1／18／19、frame0／1／7，包含0與1ms基底。原逐幀算術是 `(3*t)>>2`，0仍為0，1也可以降為0。原lookup和其他狀態callee為明示stub；原actor self-state解密、完整scheduler未執行。此限制不因Brave0而消失：Brave0不等於其他buff皆中性。
- 固定 JAR SHA256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。核對已封存oracle使用的三個ActionIntervalCalculator class entries，沿用36列×MOVE/ATTACK/SPELL＝108個純函式結果。`calculate` bytecode 先處理slowed，否則haste乘double0.755；`multiply`為i2d/dmul/d2i。其餘fastAttackable、brave、elfBrave、windShackle等另有分支，本輪不擴張這些規則。沒有執行Java類別或啟動伺服器。
- 舊 server-action-vectors.json 的Speed1 minimumProductGateMs刻意保留當時未加速政策，不能當成本輪haste預期；本輪依固定JAR oracle與原multiply bytecode推導server值。
- profile-haste-timings.json逐一讀取42個已核SHA的profile、126組動作，分別保存原幀、加速幀、marker、server基底。126組全部有逐幀和與整段乘0.75的取整差異。八方向時間／marker一致性沿用主管已做的1008序列靜態核對，不重新解PAK，不稱為原生執行。

## 計時策略

| 量 | Speed1 | 保全界線 |
|---|---|---|
| actor每幀 | `(3*原frameMs)>>2` | 缺milliseconds欄位先用ticks*1000/24整數截斷；零幀不強制1ms |
| animation總長 | 加速後逐幀相加 | 不用`(3*原總長)>>2` |
| marker等待 | 同一r30 marker之前的加速幀相加 | 不整段縮放前綴，不改索引或加入scheduler+1 |
| server interval | `max(1,(int)(serverBase*0.755))` | 不以動畫0.75代替，不處理其他buff分支 |
| reuse | 原值 | 不乘倍率、不與動作直接相加 |
| admission wait | `max(serverInterval,hasteAnimation,reuse)+margin` | 保留已接受的動畫互斥guard；弓32ms、施法100ms不縮放 |
| 箭／光箭flight | 立即首步，後續30／60ms | 不隨caster加速 |
| 附著／impact | 自己原序列 | 不隨caster加速 |

gfx0例：空手動畫621／server604；劍745／724；匕首652／634；雙手870／845；弓動畫931、server906、release590、wait963；技能4動畫1024、server996、release682、wait1124；action19動畫1149、server1117、wait1249。技能reuse10不改。synthetic reuse5000驗證等待5100，防止小reuse掩蓋錯誤公式。

Speed0持續使用r30 legacy animation、server base及原marker前綴，Speed2持續使用原逐幀×2及原較長guard。Slow優先於Haste，讓既有`with {Slow=true}`及SlowWaitMs相容。HasteFrameMs只用於逐幀非負值，與會要求basis>0的步行helper分開。

**未認證原native絕對release起算。** 沿用r30已接受marker及預測／回報時間原點，僅增加Speed1逐幀相對取整。A467A0→A43560的frame初始化／先遞增、duration+1及完整scheduler仍不是本輪執行證據，不將590／682稱作原native絕對wall-clock復原值。

## 快照與冷卻

已准入動作的速度、動畫、release與時間起點固定；同次ack沿用r30身份核對，不因中途速度更新改速、回捲或重播。下一次准入才建立新快照。加入NormalWaitMs，速度回報為0或2時分別以原admit時點加normal／slow wait，Math.Max延長有效deadline；恢復1不縮短，也不重新起算。0→1與2→1不能抹去已預約較長窗，1→0→1／1→2→1也不能遺失中間較慢狀態。

高水位是已接受引擎的保守政策，不宣稱原BIN每個tick都採這種快照。Speed0的native動畫與server微差仍依r30正常路徑，未藉本輪重寫。completion仍沿用r30 Math.Max及session/world/appearance/weapon/correction/target/input/lease/death guard；未知速度與非零Brave拒絕、F1未知motion恢復路徑均保持原文。

## 測試範圍與限制

基底gap helper載入固定r30 DLL，實際Attack/CastSelectedMagic/admit與renderer證實Speed1未縮短。候選helper從封存r30的916項suite衍生：僅將「Speed1保留正常值」改為新獨立metadata算式，擴充Speed1生命週期矩陣，增加逐幀邊界、短暫較慢狀態、所有busy消費端、外來速度隔離及乾淨renderer的實際晚到時點。舊helper未改，也不宣稱新候選應繼續滿足舊的Speed1缺口預期。

全部是實際候選DLL／封包入口／sender admit／renderer搭配synthetic actor、clock、in-memory sender；不啟動正常產品或GUI/socket/DB/服務。Item/ground既有final gate仍讀Environment.TickCount64；跨consumer測試把注入的action時鐘錨定在過去，讓未修改的wall-clock gate已開，再測action busy的−1／exact，沒有為測試擴改產品時鐘。新原native/JAR執行數為0；引用既有向量與metadata assertions不混成新原碼實測或獨立接受。
