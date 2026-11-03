# ENGINE-SLOW-UTILITY-REVIEW-PREP-R1

此為獨立準備契約，`candidateReviewed=false`，不是候選驗收。只讀固定 releases/r30 的 177 個來源與產品 DLL，未讀取另一工作者未封存候選。來源、class、native chunk 身分分別在 source-pins.json、class-pins.json、native-byte-audit.json。後續須由主管提供新的精確基準及候選派工。

## 最小可採納範圍

僅能提出 **Speed2、Brave0、既有 exact SupportedHastePotion 的單次 C164 使用例外**。普通加速藥水40013與 handler 的關係採用既有封存研究的固定模板證據；本輪沒有讀取 SQL、連接 DB 或確認部署中的模板資料。封包沒有 template ID，C164 必須使用持有物件 ObjectId。

精確 envelope 沿用 r30 Inventory.cs:7–10：ObjectId非零、Count>0、Charges0、Bless1、Flags高三位全零、desc17、icon77、UseType246，名稱 Count1 時恰為 `$234`，其他數量恰為 `$234 (Count)`。不得把 icon、名稱前綴或物件ID40013當作單獨放行依據。Speed0/1普通使用維持；unknown Speed與非零 Brave 在這個新增例外應保守拒絕，不宣稱原服必然禁止。

這是依原服正向效果路徑取得的**最小引擎範圍**，不是「原生滑鼠在所有狀態都能喝水」的完整還原。原 BIN 的 utility 輸入與動畫排程尚未定位。

## 正向原服證據（固定 JAR bytecode）

固定 JAR SHA256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。下列 offset 都是具名 Java method 的 bytecode offset，不是檔案位移；完整 class SHA、entry 與 javap SHA 在 class-pins.json。

1. `com.lineage.server.clientpackets.C_ItemUSe.start(byte[],ClientExecutor)` 0–8呼叫 useItemRequest。`useItemRequest` 163–203從C164讀取持有ObjectId並找背包物件；376–501保留地圖、paralyzed及特殊地圖判斷；501–515呼叫getUseItemAll；632–637要求HP>0；683–821保留共同502與個別delay guard。並非只有一個Speed判斷可以代表全部資格。
2. 同一 `useItemRequest` 1210 tableswitch 的 useType -10 導向1575（固定普通加速藥水歷史映射之 wire UseType246）。1575–1580要求 `L1BuffUtil.stopPotion` 通過；1588–1601存在executor才呼叫 `ItemClass.item`。`com.lineage.data.ItemClass.item(int[],L1PcInstance,L1ItemInstance)` 10–40依物品template ID取已註冊ItemExecutor並execute。固定40013→`brave.StatusHaste 300 191 true 40308 10` 的設定身分來自已pin的舊HANDOFF與config.tsv，未重新查DB。
3. `com.lineage.data.item_etcitem.brave.StatusHaste.execute` 10–14再次檢查stopPotion；17–34在isConsume=true時 removeItem(item,1)，直接跳223，跳過37–222貨幣成本支線。223–246在gfx>0送S55；249–257清drink並hasteStart；258–261取消絕對屏障。
4. **同一execute 262–293、294–325、326–359，明確對效果29、76、152各自執行 killSkillEffectTimer + S_SkillHaste(actor,0,0)。360–379送本人S_SkillHaste(actor,1,time)，380–396向附近廣播mode1，397–416設moveSpeed1與STATUS_HASTE1001。** 這些正向移除慢速狀態再建立加速的分支才是最小例外依據，不能用「沒找到Speed拒絕」代替。
5. `L1BuffUtil.stopPotion` 0–24在decay potion狀態送698拒絕，否則通過；`getUseItemAll` 0–81仍拒絕多種效果狀態。`hasteStart` 0–112先對既有43/54/1001做清理，可能另送mode0。其他狀態、重量、地圖、擁有權等原服檢查仍由原服裁決；此契約不宣稱使用必成功。

## 回包與時間

| 層次 | 已證明內容 | 不得推論 |
|---|---|---|
| 原服 reuse | C_ItemUSe.useItemRequest:3506呼叫L1ItemDelay.onItemUse；該類 `(pc,item)` 68–86取etcitem delay ID/time，154–157交scheduleDelay。scheduleDelay:12–27遇0 ID或0time直接skip；67–100以原delayTime換算ns並建立arrival/execution deadline，沒有乘actor cadence。固定40013歷史配置delay0。 | 不能據此放寬共同502、其他物品delay或宣稱所有道具無冷卻。 |
| 本機 item guard | r30加速水 `UseDelayMs=0`，另加100ms本機保守防連點。 | 100ms不是原BIN動畫或原服reuse；本輪沒有證據可再乘2或0.755。 |
| 原生 utility 動畫／scheduler | unknown；尚未定位喝水、裝備、NPC交談、拾取、丟棄輸入排程的完整正向路徑。 | 不把攻擊／施法2x公式自動套用utility；也不把未知記成0ms。 |
| native速度接收 | VA0x94C530/RVA0x54C530/file0x54B930/325bytes/SHA a498af93ac848b174aa58db724f7af6a43776352f21d0342a0064ebc6bedb18f，解析dch、找actor並寫actor+195。原BIN dispatcher opcode93與此JAR S255是已知版本語意轉接。 | S255不能冒稱原BIN相同數字opcode；此接收器不是原生utility發送器。 |
| 視效 | VA0x934690讀d+h收包效果，VA0x8ABC10是既有純特效更新路徑；所選chunk已按PE區段重新映射並對原BIN逐段驗SHA。 | 不把視效時間當使用動作鎖；本輪未重新執行native指令或驗全renderer。 |

`S_SkillHaste.<init>(int,int,int)` offset9–28寫 C255 Dactor Cmode Hseconds；`S_SkillBrave.<init>(int,int,int)` 4–24寫 C67 Dactor Cmode Hseconds。StatusHaste的**直接**呼叫順序是慢速效果清除的S255(mode0)在S255(mode1)之前，既有haste清除可能增加mode0；不能承諾封包總數恰二。此方法沒有直接產生S67；kill-timer間接路徑與其他狀態造成的S67全域順序未完整覆核，故**不把S67當喝水成功必要條件或承諾相對順序**。

C164送出、sender成功、ItemSent、S55或本機timer到期皆不得修改Speed/Brave或預扣Count。S255/S67只更新指定actor；S24/242/57處理數量/移除。自己的同值S255也必須增加SpeedRevision，以取消過期排隊請求。即使Count先更新或沒有S255，也不可本機假解除緩速。S255的秒數到0不代替伺服器狀態回報。

## 其他 utility 的資格隔離

| 類別 | 本輪正向證據範圍 | Speed2處置 |
|---|---|---|
| 40010/40012補血水、肉、回家卷軸 | r30有各自envelope與delay，但本輪未重建各handler的慢速正向路徑。 | 不新增慢速放行。 |
| 其他藥水／任意道具 | 缺固定template、handler、envelope、timer證據。 | 不放行。 |
| 裝備 | C_ItemUSe.useItemRequest的裝備dispatch與ItemClass.item_weapon/item_armor只說明分派；各executor資格、原生排程未完整覆核。 | 不把SupportedEquipment列入加速水例外。 |
| NPC talk | C_NPCTalk.start:24–122要求current player、非dead/teleport/privateShop、NpcInstance、WorldInteractionGuard.npcTalk；234–257進NPC executor；WorldInteractionGuard.npcTalk:0–36為owner companion或withinDistance5。npc/quest分支仍各自有條件。 | 原生輸入／NPC專屬handler慢速契約未完成，不擴大開放。r30 direct與pointer入口差異列為既有觀察，不當作新功能驗收。 |
| pickup/drop | 固定C_PickUpItem/C_DropItem已抽取；pickup延後commit會重驗binding/session/current player/死亡/傳送/距離/物件身份。這些mutation路徑不等於native排程證據。 | 不新增慢速放行；r30 GroundClickGuard900ms仍明示本機policy，不宣稱原服或native時長。 |

## 未來候選最小審查條件

- 初始及sender最後predicate都使用相同exact haste判斷、已支援Speed/Brave、world/session/item selection/input/speedRevision/lease等快照；不可只改UI顯示。保留action lock、交易、HP/death、原有busy與冷卻，不建立自動重送佇列。
- 初始忙碌：move/attack/spell/item/ground五類均拒絕。最後predicate忽略本請求持有的itemSending，但保留其他producer、MoveReadyAt、combat、magic、ground等期限；不要因自身busy永遠拒絕，也不要用synthetic future ActionClock與真實TickCount64比較。
- 同值S255、0→2→0、0→1、S67更新、換圖、取消、換選取、Count/record更改、死亡後，尚未admit的請求不得送。Speed2初始可進sender後的同值/瞬態/恢復等向量必須在未來候選新增；r30初始就拒絕，不能假稱已覆蓋。
- admit後成功／例外／finally按session+world+actor identity+lease辨認歸屬。舊回呼不得推進新世界deadline、ItemSent或改掉新lease的busy；舊utility錯誤不得以自己的處理污染新世界。若transport本身已失效，連線層的全域斷線責任仍需獨立設計，不應靠忽略真實socket失敗來掩蓋問題。
- **入場快照不等於completion資格。** 已admit後的本人S255（含2→0→1或直接1）、S67、S24數量變更及最後一個物品的S57移除都是正常權威結果。只要仍是同session/world/actor/lease，sender成功應保留一次成功事件，不應因SpeedRevision、Brave、selection或item record已變而消掉成功；也不能回寫舊Speed/Count。queued時同值或往返速度仍必須失效。未來actor更替情境須新增測試，本輪未覆蓋獨立actor更替。
- 使用S255/S67/S24/57驗權威狀態，不使用真的伺服器、socket、DB、遊戲或正常GUI。未來不同基準必須重pin/rebuild；本輪90筆不是r31或待實作候選驗收。

## r30 離線實測解讀

r30-probe.json是實際固定DLL，sender/actor/packet是明示合成；兩個注入clock都指向Environment.TickCount64。90筆包含78個契約判斷及12個NPC入口觀察；其中18個不滿足擬議契約，屬既有基準差異或未來增量需求，**不是此次候選失敗數**。

- Speed2/Brave0的exact haste目前拒絕；這是本次擬議增量。Speed0/1+Brave1與unknown Speed3/17/255+Brave0/1目前會送，屬保守邊界待補。
- initial busy注入9種全拒絕；五類正式producer先admit後阻擋item，以及queued item阻擋五類正式producer，10個互斥案例全成立。故人工注入magicSending/nextMagicAt/groundSending/nextGroundAt後仍可送，只證明最後predicate缺少防禦性重驗，**尚未證明可由正常公開API產生同一競態**。
- queued speedRevision、input、selection、record、death、world、session拒絕成立；人工ResetItemSend的lease更替未被最後predicate阻擋，該更替的公開可達性未證明。
- sender已admit後，使用真S206 decoder改WorldEpoch，舊成功仍發ItemSent並延長deadline；舊例外仍Cancel/Faulted。使用實際Cancel改generation則成功/例外均被隔離。人工ResetItemSend+新busy owner重現lease成功/例外副作用；lease案例保持synthetic分類。LoginConnection.cs:62的正常收包路徑呼叫ApplyWorldPacket，WorldEntry.cs:45呼叫同一World.Receive；本helper沒有模擬完整連線pump或證明live網路時序。
- 已admit後先餵直接S2551+S670+S24、或S2552→0→1+S671+S24、或S255/S67+S57三種合法回報，再完成sender，三案均保留成功與一次ItemSent，且Count/Speed保持回包值。r30只能從Speed0入場，Speed2初始admit後的對應控制組留給未來候選，沒有假造已放行。
- direct Interact及低階PointerTalk在Speed2/3均送C34；PlayerPointerController.ActAsync在Speed2/3拒絕；Speed0/1三路可送。這是現有入口不一致，不可稱「NPC原本全面拒絕Slow」。

工具限制：沒有新JAR handler執行、native執行、正常產品啟動、DB/服務/SQL或網路測試。首次dotnet工具輸出顯示SDK執行了first-use HTTPS開發憑證步驟；未發出憑證信任指令，也未修改既有憑證。此工具自動副作用保留記錄，不聲稱工具僅有編譯stdout。
