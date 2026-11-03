# P19 原 EXP 與自然戰利品協議

固定 JAR SHA256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`，ClientLogin=2。`reference/rewards/manifest.json` 保存每個原 Java/class/javap hash；本文 offset 是 JVM method bytecode offset，不是 PE VA/RVA。P18 原地物、空背包同步、晚到拾取與遮擋行為沿用，既有8肉未證去向與拾取動畫限制不被本階段抹除。

## 原狀態封包

- S113 (`S_Exp`)：C opcode113/C level/D EXP低32位/C1，零補齊8bytes。固定 constructor offsets10–33；`ServerBasePacket.writeExp` offsets4–62只寫四位元組，沒有高32位。更新為絕對狀態，不能把此值當增加量。原level欄是C，完整實際等級來自該欄。
- S8 (`S_OwnCharStatus`)：D object/C level/D EXP低32位，接原六屬性、ClientLogin2的32位HP/MP/AC、時間、食物、重量、正義與16位抗性等。P19沿既有完整S8解碼，另更新經驗模型。登入與原升降級重送此狀態。
- `L1PcInstance.onChangeExpLocked` 比較 old/new EXP，使用原 ExpTable 算level；gap0且level<=127送S113，較高送S8；gap非零由levelUp/Down流程更新。S87自身外觀也有低32位EXP，但不作本階段經驗更新來源。
- S250/sub192 (`S_ShowDrop`)：C type0經驗／1金幣、D amount，是RO通知。`ClientLootNotifier`將正long數量cap到int max；`CalcExp`實入帳用d2l截斷，通知用Math.round，兩者可能不同。產品保留未知原包及raw，不用通知加經驗或物品。
- S250/sub190 (`S_ItemBoard`)：H icon/S name，是RO道具通知，不代表背包數量已更新。

原序列化器 `RewardOracle.java` 使用明示合成 Pc 輸入，直接呼叫固定JAR的S113兩建構形式、S8兩建構形式及RO通知；Pc只提供合成值，S8時鐘用原init，正常System.exit結束這個獨立oracle，沒有啟Server.main或連DB。16個EXP/status樣本與7個notification樣本見oracle.tsv和notification-oracle.tsv。曾漏時鐘init的失敗收據保留在oracle-run history。

## 原表與顯示

`ExpTable.getExpByLevel(L)=exp[L-1]`，本級上界為exp[L]；getPenaltyRate(L)=1/exp[L].expPenalty。`generate_exp_table.py` 從原380_db.sql擷取101筆原閾值，來源hash與rows在exp-table.json；只產生靜態OriginalExpTable.cs，產品不讀SQL或測試oracle。

Lv60下界416461882、上界452526974、exp[60].expPenalty=8。原全表MAX_EXP1895130654，MAX_LEVEL由最大列減1再與MaxCharacterLevel90取小；設定未改。產品只有在wire level及低32位值落於已證明且不跨uint欄寬的原級距時顯示總EXP、級距與百分比。其餘明示總值／進度未知，仍保留原低32位，不猜高位或由SQL填入。

每次S8/S113覆蓋狀態，只有真實狀態差異才記錄原回包差值；重複狀態不重複增加。重新連線清除前世代變化。升級邊界及超界資料為明示合成測試，不能代替真實擊殺。

## 原擊殺、分配與自然背包

`L1MonsterInstance$Death.run` 先原死亡action8廣播，再傳入死亡當下仇恨快照執行EXP/掉落，避免allTargetClear破壞分配。`distributeExpDropKarma` 的CalcExp呼叫offset53；原SetDrop與DropShare呼叫見同class的distributeDrop方法。

`CalcExp` 依有效攻擊者及仇恨分配名目EXP，再套本級懲罰、RateXp與TimedRate，葉子、料理、個人裝備／buff、map等原加成，最後`pc.addExp((long)addExp)`（addExp offsets213–217），而通知Math.round在220–226。狼人45024原HP50、Exp82不是玩家實得。原RateXp1.0，但原accounts.server_定時加倍有每日00:00–23:00 EXP2.0活動；必須對照啟服／測試時日誌。map0無mapids_exp列。若當時2倍且無其他加成，82/8*2=20.5，實得與提示有可能是20／21；這只是待實跑核對的來源預測。

原狼人droplist金幣40308最小22、最大50、chance1000000；其他具非零機率物品另列於sql-source.json。SetDrop先依原drop_rate與隨機判斷，再以min+nextInt(max-min+1)取數，金幣再依原伺服器與玩家倍率。原AutoLoot=2、RateDropItems1.0、RateDropAdena1.0不變；原定時金幣與掉寶活動未啟用，map0drop_rate1.0。全怪物掉落也在來源oracle中保留，不能把額外物品忽略或稱必然掉落。

SetDrop也先執行地圖掉落分支setMapDrop，再全怪掉落，再npc droplist。本例原droplist_地圖掉落 map0零列，npc_擊殺獎勵 mob_id45024亦零列；SELECT收據已納入sql-source.json，不能省略這些原分支或假設所有額外物品必出自狼人表。

核心原分配在`DropShare$DropShareR.run`，按仇恨選合法獲得者，使用原`tradeItemFromInventoryIfCanAdd`進背包；容量失敗才走原地面fallback。旁觀沒有攻擊不預先假定獲得值，由各自raw與SQL保存核對。自然AutoLoot沒有地物時明示未測自然地物拾取。

原S5清單、S15新增、S24及S242/sub1狀態、S57移除等沿P17/P18完整模型，以object、desc、icon、name、count及raw尾資料保存。template ID不在wire。已核對金幣指紋desc0/icon5/name$4，既有金幣應按原更新保留同object。沒有使用掉落表數字、攻擊次數或本地機率直接加背包。未知物品保留原欄位，缺素材明示缺圖，不使用替身；未實作其使用功能。

## 動態證據規則

`RewardScenario`只是一個有上限60次的測試控制器，每次明示單次原近戰，記錄每筆請求與檢查點；產品仍只一次G／按鈕送一次，不追擊、不自動攻擊。低HP可消耗原有限6藥，不能補發。主／旁觀第一個seed前掛capture，保留每世代雙端sequence及applied邊界、完整前後包，不能用snapshot比較代替獨立證據。

`verify_rewards.py`用獨立Python小端／CP950解析重建S8/S113與背包，對原死亡、原回包增量、同堆疊object、兩世代重登與保存SELECT互證；S250通知只作旁證。SQL是獨立oracle，不是產品資料源。每輪失敗獨立保留，續跑只接相同服務已保存狀態。

CLI r5 已完成兩世代完整流程，獨立解析44個雙端快照。EXP416461902→416461922（+20），原既有金幣object1900191012由26→62（+36），旁觀EXP及空背包不變。先前r1亦有一次真實擊殺（+20 EXP、+23金幣），但返回路線失敗，不計作完整成功流程。所有輪次保存於 evidence/finite-ledger.json，不能把r5稱為fresh無失敗起跑。

原定時倍率啟動日誌14:45:29與活動資料共同核對。TimedRateController.start先evaluate再記錄啟動，首次handleTransitions未initialized時不另寫活動開始，不能以缺少該行認定未啟用。實際+20 EXP對應82/8*2的long截斷；RO提示21對應同20.5的Math.round。這兩個原值分別保留，產品只採S113的絕對EXP。

CLI r3的固定軸向引怪條件與原AI斜向路徑不符；r4首擊送出前NPC移動，原目標座標gate取消請求。測試已移除固定追擊方向，有限合法靠近；未送出時最多10次新的單次輸入，各次記錄，沒有放寬產品gate。r4消耗一瓶既有藥水6→5，S24→S55/197→S225及HP327→407獨立核對；r5未再耗藥。自然NPC回血/移動照原邏輯，沒有重設怪物。

UI fresh流程已完整通過並獨立解析62雙端快照：EXP416461882→416461902（+20），金幣3→51（+48），6瓶原藥水未消耗，旁觀EXP／空背包不變。真實screen與WebView CapturePreview同值，重登後EXP／背包一致且變化區清空。三次擊殺沒有其他隨機物品，因果摘錄見 evidence/reward-causal-summary.json。最終交審狀態以result.json及HANDOFF為準。沒有裝備、魔法、商店、全物品、全地圖或P20實作。
