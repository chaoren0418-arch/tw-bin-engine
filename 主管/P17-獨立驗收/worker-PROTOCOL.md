# P17 背包與原道具使用協議

固定原JAR SHA256 d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf，ClientLogin=2。原class／Java／javap與hash見reference/inventory/manifest.json。地址均為JVM method bytecode offset，非PE RVA/VA。

## 已靜態核對

|方向/opcode|原結構|
|---|---|
|S 5|C筆數，每筆 D item object ID、H packet item description ID、C packet use type、C charges、H inventory icon、C bless、D capped count、C flags、MS950 NUL name、C status length／bytes，最後C10/H0/D0/D0。|
|S 15|同上單筆，無筆數。|
|S 24|D object ID、S name、D count、C status length／bytes。|
|S 242/sub1|D object ID、S name、D count、H long status length／bytes。其他242變體仍未知保留。|
|S 100 / 240|D object ID加S name／C bless。|
|S 57|D item object ID，原服耗盡移除。|
|C 164|D當前item object ID；本階段hpr=-7無額外參數。8-byte零補齊。|
|S 55|本階段8-byte envelope：D actor、H skill gfx。記錄原效果事件，未宣稱顯示原藥水動畫。|
|S 71 / 243 subtype9|H訊息編號、C參數數／字串；或原system message MS950字串。|
|S 225|D HP／D MaxHP，沿P16 wide-vitals原格式。|

S_ItemCount的opcode136實際是NPC amount offer，不能誤作背包數量。L1PcInventory.updateItemBody COL_COUNT=16呼叫S_ItemStatus。InvList每筆尾部仍逐字保留，不丟棄；status保留原hex，未解釋未知狀態。template ID未由這些封包傳送，與description/icon明確分離。

UserAddHp.execute：移除1個→原藥水效果→隨機原回復量→必要原訊息→adjustCurrentHp，滿血也消耗。選定原40012模板參數60..80/gfx197/consume true，delay_id2／1000ms。相同desc130/icon81/use249/$238的140012亦有相同executor/延遲；產品只辨識這個已核對指紋，不宣稱wire含template ID。原JAR hasItemDelay同時使用ingress與runtime fence，不用本地成功回覆模擬原拒絕。

## 實作

Inventory.cs以當前WorldSession的object ID建模，重複全清單取代舊集合並清選取；未知更新不建立道具，耗盡／Delete清選取。取消／斷線／選角重設背包；pending send重查connection generation、worldEpoch、selectionRevision、inputVersion、item record、combat/movement busy。入口忙碌直接拒絕，不排隊。item send不扣物、不加HP。UI U按住只觸發一次，Web精確白名單selectItem/useItem含itemId/epoch，過期命令不送。

顯示原名稱經原desc-c.tbl解析，圖示取Sprite.idx/PAK的81.tbt、5.tbt、475.tbt。TBT byte skip/2與RGB555 bit replication依原helper LoadL1Image；舊LoadL1ImageRaw的未除2分支不適用，首次失敗保留。PNG與原entry/hash及C#獨立逐像素證據見reference/inventory/icons及evidence/inventory-icons.json。圖示之外未知項目顯示缺圖，不生成代用品。

## 已執行與證據界線

工作者CLI採三段原始capture連續使用同一角色，數量6→5→4→0；兩次失敗與SQL全部保留在evidence/history，resume.json以SHA綁定，沒有拼接或重新編號原封包。總9筆A4、6次實際消耗、3次原拒絕。首次本機TickCount64恰1000ms但UTC實隔998.2103ms，原服雙fence拒絕；現有客戶端保留1000ms原規則，加100ms明示本機保守餘裕，不自動重送。第二次因原狼站在(32599,32948)而無路，僅測試driver加90秒動態等待，不修改地形／AI。

workerUi一次7筆A4／6瓶完整消耗，遊戲U、Web DOM useItem、滿血、忙碌、快速、焦點及耗盡流程通過。UI初次seed事件因監聽掛接晚而缺失，server plain自sequence1起完整，原始generation0／重登1保留。capture-limitations.json固定其SHA並明示，不能視為完整seed取證。修正後在StartLogin前掛接；worker-review無A4的正常重登已捕獲seed及連續seq，原名稱與圖示均在viewport，主管未用的6瓶UI驗收將確認整輪修正版。

獨立Python從原plain重算背包，逐checkpoint/applied sequence核對：CLI28快照869欄位；UI33快照1012欄位。UI seed缺口不從別輪填補，判定分為功能欄位pass與seedCaptureComplete=false。CLI的game/web標籤代表相同共用入口的兩次測量；實際兩種UI操作只在workerUi／主管UI成立。

傷後HP：CLI519→584藥效+65，接續獨立225包584→588自然+4；第二瓶588→658藥效+70。UI502→582(+80)、582→647(+65)。每段都有數量更新→self gfx197→225 HP，短於1秒，原隨機量不設固定斷言。原CON16自然random1..2加王族bonus2為3..4；保守納入可能house15與life-stream3，上限22仍小於本次獨立藥效delta。完整所有HP包保留，不能把+69混稱藥效。原清理玩家歸屬與map recovery來源另見reference/inventory；產品不靠SQL作HP決策。

滿血仍消耗；最後1→0原57刪除並清選取；保存後重登只有原金幣3／鑑定卷軸1。兩worker均正常離線。獨立驗證也檢查帳號server_no／角色OnlineStatus為0。

必要回歸：16個TCP、cipher/Netty、離線原render、WebView2、原圖1779像素、原JAR八種序列化回放165欄位、背包合成負例，以及以本次CLI單一世代raw為輸入的combat/object/world/movement四組通過。P16的死亡終幀與長時EventJournal/receive fairness來源未改，沿用其已驗證證據，不冒充本階段重新殺怪或長時壓測。

新DB43317 UUID04891658-d029-11f1-9406-4cedfb679789；固定原JAR PID13880 game42037。實際父子、建立時間與身分以ready-for-supervisor.json為準。新初始fixture與玩家資料清空收據、21表3685列遷移、獨立loopback policy guard均已完成。正常JAR wildcard bind＋loopback准入，從未注入已知會關listener的LAN探針。所有原環境唯讀。

## 限制

只支援核對過的恢復道具指紋，未知型別阻止使用但可列出；無裝備／交易／倉庫／商店／製作／丟棄／自動喝水或戰鬥。只記錄原gfx197事件與消息，不顯示原藥水動畫。原名稱$238在客戶端desc-c.tbl為「白色藥水」，SQL模板40012名為「終極治癒藥水」，兩者不能混作wire模板ID。Game UI測試使用真實WinForms控制器與WebView2 DOM，並非外部原生鍵鼠注入。舊UI後段Web截圖停在輸入區，未補造藥水畫面；修正版review與主管新測提供viewport證據。原生停用按鈕仍使用系統暗字，啟用時可讀。
