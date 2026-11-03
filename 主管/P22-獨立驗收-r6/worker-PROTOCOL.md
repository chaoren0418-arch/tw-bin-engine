# r6：雙介面選取測試覆蓋

原協定／產品renderer／AI／選取門檻不改。完整測試每次交替明確native與Web選取，不能因同一ID持續存活略過Web。Web之前用原game viewport內、未被child control覆蓋、原map raster且EntityOwners=null的像素呼叫ClickScene/PickObject，確認實際target=null；然後真DOM selectMagicTarget、確認正確ID與positive pixels。清除與選取區間C6施法／C164物品使用／CombatSent對應C229或C123攻擊各0，末尾要求兩介面皆覆蓋才pass。原Web target0拒絕維持，不新增產品行為。

r5站位／安全與單次施法規則維持。第19項magic-target-tests新增實際隱藏WinForms native clear回歸（無Show/network/WebView），僅offline；真Web DOM由完整UI驗收。legacy單發補驗已失敗保留，不再執行。本次原r5完整UI/CLI成功與後續補驗失敗均保留。

r6 UI唯一保存553HP3MP／32598,32941／Food40／gold38／0藥；CLI633HP1MP／32588,32930。完整after/hash見correction-authorization-r6.json，首seed前exact guarded fullSELECT。history/r5保留舊envelopes與變更前來源；最新19項見correction-r6-validation.json。以下舊保存值只屬歷史，沒有r6修復登入或清理授權。

# r5：測試站位admission與原r4失敗序列

魔法協定與所有產品門檻未改。首次戰鬥C6之前，控制器必須完成原地形的主角先行／觀察者後行站位及原雙端互見位置確認；當下positive renderer像素不能跳過此步。兩者目前座標必須仍符計畫，目標仍正可見，才可進既有native/Web選取與單次施法。失敗鎖住控制器，不暗中發第二發。

同一累積3站位／60合計正常Move／45秒、≤2秒每wait、50ms原HP／death／disconnect護欄維持。r4 raw首次bolt前與失敗後序列、原S225539→526加入離線回歸；合成站位與settled render時間明示，沒有冒充原當下畫面。原flight命名PNG未顯示清楚飛行的限制保留。測試仍19項，最新結果correction-r5-validation.json。

r5唯一UI proof是原r4保存526HP／79MP、Food40、32595/32948、0藥水；CLI原624／2。`correction-authorization-r5.json`指定完整after及hash，禁止舊r3保存值、重置、補發或修復登入。r4歷史封存history/r4；以下較舊保存值與ready只作歷史。

# r4 測試控制器與保存證據修訂

原魔法封包、原depth／opacity／terrain／AI／產品門檻完全不變。測試 controller 在每輪攻擊前只選當下在距離10內、被觀察者接收、有正原 renderer 可選像素的存活狼；UI另檢查原viewport像素。隱藏候選不觸發選取或正面C6；原負面 probes 保留。

無可見候選時依原多姿態遮擋與spawn歐氏半徑2規劃、當下原地形及occupancy求路。最多3waypoints／兩角色合計60正常Move／累積45秒，所有等待≤2秒且50ms原HP／death／disconnect護欄。原NPC可能改變位置或回家，不假定規劃成功；耗盡為保留失敗。計畫收據展開具名座標與步數，避免ValueTuple序列化為空物件。

r3主管真UI失敗正常保存HP521／MP83、32595/32948、Food40、藥水0；這是r4 UI唯一續跑基準。CLI仍624HP／MP2。`correction-authorization-r4.json`保存兩份完整after及path/hash；`magic_integration.py`要求來源精確符合最新授權proof，並在首seed前guarded fullSELECT比對。r3實際已完成6藥／11heal／44跨區Move，不能再宣稱未使用或回到town初態。

`--magic-target-tests`使用真r3失敗原包，明示衍生自身最終位置與另行合成可見情境，僅離線回歸。最新18＋1回歸見correction-r4-validation.json；歷史r3文件與變更前來源在history/r3。尚待主管新DLL實際完整UI/CLI續驗，沒有功能通過或清理授權。原屋頂opacity/group觸發、2×2cache歷史、額外區塊／全圖streaming與portal仍是完整引擎待辦。

以下r3/r2/r1為歷史記錄；舊保存值不適用r4。

# r3 證據／測試修訂

原雙區塊常駐產品與魔法協定維持r2。ObjectRegression明確區分localSourceIndex42、global map serial7/100007及tagged entity ID7，每個map覆蓋皆清EntityOwners；產品Renderer不回退。reference/dual-block/bin為原BIN來源證據，現在精確納入sourcefreeze，生成bin/obj仍排除。r2前版收據及所有變更前檔封存history/r2，完整18項最終DLL結果見correction-r3-validation.json。原r1角色保存值仍是續跑唯一基準，沒有r2/r3修復登入。

# r2 修訂：常駐雙區塊與續跑界線

本節取代下文r1任何單區塊／主管角色尚未使用描述。魔法封包、原JAR/BIN版本、動畫／投射物節奏不變。完整更新見HANDOFF.md、correction-r2-validation.json及reference/dual-block/resident-scope.json。

載入完整7ffd8002與7ffd8003，範圍X32576..32639/Y32896..33023；26960原draws，4608×2328。南區局部y+64，原越界anchor/pixels一律保留，原source_index留存；serial採北offset100000、南offset0，以符合原冷載南先插、equal-z反插入的北先畫順序。map遮擋owner使用唯一serial；EntityOwners仍為獨立原object ID，特效無selectable ID。原Draw.Px/Py、RGB555混色與depth未變。全10727424像素/mask對主管獨立原golden完全相同。

原地形完整512×512直接送固定JAR L1V1Map.isPassable取得65536向量，產品範圍門檻與原passable分欄；384雙向接縫向量一致。動態門/occupancy仍依原服校正，靜態oracle不宣稱涵蓋。

原BIN初載F09C70/F0B570/F0ED00及F117C0/F10A70/F110A0/F116D0和方向表有VA/RVA/FileOffset/hash原byte證據。兩區常駐只採冷載順序，並非歷史相關2×2快取；Y32984南移卸載／水平重插／opacity與portals均未完成，後續全圖streaming必補。本次產品允許兩區雙向移動，不以單一路線代替支援範圍。

主管原UI失敗保存HP1/MP115/Food40/town32580,32974，原CLI保存HP624/MP2/32588,32930；下一輪Fresh=false/ResumeRecovery=true，完整after snapshot精確相等才首登入。恢復只透過真native/Web既有產品控制及原資源，6瓶/30單次heal/600秒上限，HP500/MP20為測試安全門檻。所有焦點/可見目標/自然MP/有限heal等待有原state停止護欄；不預測回復，不修SQL。

以下是封存r1魔法協定與歷史驗證的原文；r1完整文件另在history/r1。

# P22 固定原服技能清單與兩個手動技能

此文件描述已實作的固定 JAR profile 與原資源對照。動態驗收以獨立 evidence/result、原封包、SQL 保存核對及主管驗收為準；文件不將合成測試當作實際遊戲通過。

## 來源與適用範圍

- 固定 Server_Game.jar SHA256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。
- 380_db.sql SHA256 `0875f7cff78d7d97a7380cc5d24f47b2c66b630d452377e5f8bfa6f3d29fd95e`。
- TW1608262200.bin SHA256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，image base `0x400000`。未執行原 BIN；IDA 僅使用 P22 真複製 IDB，無 patches。
- `reference/magic/manifest.json` 記錄每個原 class、Java、javap 雜湊。固定 class 與實際 wire 優先於可能較新的 Java。
- `reference/magic/bin-raw-manifest.json` 對每個函數 chunk 列 VA、RVA、PE FileOffset、大小、SHA256，逐 byte 與原 BIN 核對；`bin-raw/*` 保留 request/process/result、反組譯、原 bytes、xref 及反編譯。

固定 JAR 的 opcode 與此 BIN 原生 dispatcher **不相同**。本產品使用固定 JAR profile；BIN 用於位元語義、素材對應與呈現時序，沒有宣稱兩者原生 wire 完全相容。原 BIN 947530 的 native S21→92E360→E24170（新增）、S82→9494D0→E24340（刪除），native C216→E24FD0，native S5→930A20（攻擊 envelope）、S86→934690（純特效）。原 BIN mask header 是實際 group count，固定 JAR 32/50/100 是 tier flag，不能混用。

## 固定 JAR 封包

下表 offset 自解密 body opcode 起算，C=uint8、H=uint16LE、D=uint32LE。網路加密／長度前綴沿用已驗收連線層；此處列 padded plaintext。

|方向／opcode|欄位|原 serializer／handler與長度|
|---|---|---|
|S164 新增／S160 刪除|C opcode0，C tier1，mask從2起；bit j於group i代表技能 `8*i+j+1`|S_AddSkill/S_DelSkill：28mask body30→32bytes；single-skill32mask body34→40bytes；增為union，刪為remove，不重複累加|
|C6 單次施法|C opcode0，C row1=`(id-1)>>3`，C col2=`(id-1)&7`，D target3；光箭再H x7、H y9|C_UseSkill／ClientPacketStructure；產品送16byte。治癒有目標形式至少7byte、光箭至少11byte；原3/4byte自我形式結構可接受不代表任意技能業務合法|
|S33 MP／S225 HP|C opcode0，D current1，D max5|CLIENT_LOGIN_TYPE2為body9→16bytes；不可誤讀H。絕對狀態更新／重複不累加；未預扣資源|
|S55 原治癒特效|C0，D object1，H gfx5=11722|S_SkillSound一般形式body7→8bytes；本階段不宣稱額外持續時間形式已支援|
|S158 動作|C0，D object1，C action5|19自癒姿勢；18指定施法；既有受擊／死亡同原動作流|
|S30 光箭|C0，C action1=18，D caster2，D target6，H damageWord10，C heading12，D sequence13，H gfx17=167，C flight19=0，H sx20、sy22、tx24、ty26，D reserved28=0，C tail32=0|S_UseAttackSkill body33→40bytes。原傷害word是unsigned16欄位，非獨立證明的NPC實際HP減量|
|S71 訊息|沿用原S_ServerMessage decoder|MP不足原message278；無錯誤包的拒絕不能捏造回包|

有意義的截短測試依完整封包型態判斷：40byte single-mask的32byte前綴若尾端為合法padding，正好也是有效28mask封包，不能一律拒絕。padding驗證與可辨識的非合法長度須在任何狀態更新前完成。

## 原技能與權威邏輯

`PcLvSkillList` 固定 bytecode 48–80：王族type0且level>=10合法技能1–8。初始四主角type0/gfx0/Lv60/INT11/OriginalInt11未改職業或屬性，僅首次初始化 character_skills 插入1及4，旁觀者未學技能。

|技能|原skills欄位|本階段行為|
|---|---|---|
|1 初級治癒術|level1/number0/id mask1；MP4、HP0、item0；target buff/target_to3/type16；range-1；action19/gfx11722/reuse_delay10|UI限定自我，送自己objectId；原回包才更新HP/MP|
|4 光箭|level1/number3/id mask8；MP3、HP0、item0；target attack/target_to3/type64；range10/area0/through0；action18/gfx167/reuse_delay10|明確選取原可見目標後一次施放；不自動追怪或連放|

`L1SkillUse.isHPMPConsume` 將模板MP成本經原INT及裝備等條件處理，並減去 originalMagicConsumeReduction。此原王族INT11減1，故本fixture heal3、bolt2。產品顯示的是模板MP，**不**將前後MP差直接當消耗；自然恢復可能介入。MP不足於固定class offset852生成278，成功後原 `adjustCurrentMp` 才更改狀態。完整原技能表保留在 `original-sql-candidates.json`，沒有更改原消耗／傷害／恢復／掉落。

原 C_UseSkill 依序檢查連線、死亡、傳送、重量、地圖、封包結構、技能delay、技能範圍、模板、狀態及mastery；未學技能通常直接return。原 makeTargetList 用 `getTileLineDistance>getRanged`（javap offset114/121）排除超距；`isTarget` 的 `glanceCheck`（offset1015）及攻擊類型控制權威LOS。未知object及座標fence的實際結果以raw記錄為準。受保護SELECT只作oracle，不是產品技能或MP資料源。

## 原姿勢、特效與時鐘

- gfx0使用原3225，18/19八方向對應四種既有武器外觀（0/4/20/50）；每frame原`floor(1000*ticks/24)`，畫面指定施法1374ms、自癒1539ms，兩者release marker916ms。伺服器spr_action/SprTable動作時間1320/1480ms，產品送出閘門另加100ms，與呈現時間分開。
- gfx167 `109.effect(4 219)`；930A20選原effect4類820F90；8211E0在release解析當時目標位置、F1A340八向轉四SPR（方向>3減4），F1A190原16.16步進；821360立即首步，再每60ms前進。沿用原目標fallback座標，不用弓箭30ms機制。飛行每步 A43560前進metadata index，原順序物理frame0,1,2,3循環，抵達產生219。
- gfx219原11token總576ms；gfx11722原21token總2625ms。8AB390初始化index0，8ACD20→8AE440→8AE3C0→vtable+144=8ABC10立即呼叫A43560先遞增index；因此metadata1..末,0只播一圈。沒有把metadata0錯當第一畫格。純治癒特效固定在接收時原座標，不跟隨後續移動。
- `asset-records.json` 保存原PAK/IDX entry、offset、壓縮大小、SPR雜湊；`asset-entries.json`保存原list項；原11722來自inner-list.spz/Sprite13，含合法空白SPR sentinel，不補畫猜測像素。3225在base Sprite與Sprite00有異版，選既有已驗收base Sprite並保留來源，沒有任意混合版本。
- 特效與角色／地圖進同一深度合成，特效不成為可選取實體。原受擊／死亡優先，舊world generation清除排程，不偽造HP或死亡。

## 測試與保留限制

MagicOracle使用原固定JAR構造器，synthetic actors/skill templates僅在oracle程序內，拒絕network/listen/exec，未呼叫原server main或修改DB。新增single1/4的40byte原新增／刪除向量。`MagicTests`、`MagicRenderingTests`與既有弓箭／近戰／背包／地物／裝備測試分別驗證，不將測試輸入注入產品實際連線。

動態控制器是有限測試程式：每次明確送一次、等原回覆，保存雙端首seed之前起的所有世代raw/sent/applied。產品沒有測試式自動施法。自癒、光箭擊殺收益、MP耗盡原拒絕、自然恢復及保存重登的實測證據將由最終HANDOFF索引；目前不得從本文件推定已全部實測。未知技能、技能學習、其他魔法、音效播放及完整引擎不在本階段已驗收範圍。
