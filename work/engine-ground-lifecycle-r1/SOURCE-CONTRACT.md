# ENGINE-GROUND-LIFECYCLE-IMPL-R1 — 正常拾取生命週期

基底為主管接受的 immutable r32 stage，177來源、product DLL、audio DLL、product manifest、launcher DLL已逐項與正式 final r32 相等核對。原 baseline 不改；final-base-binding-pins.json 與 evidence/final-base-equivalence.json 保存後續綁定。只在本工作目錄的獨立副本實作，並非正式 r33 release。

## 來源與缺陷

r32 Ground.cs 的 SendGround 原完成／catch 只檢查 session generation，finally 檢查 generation+lease；最後准入缺 actor ID、ground lease、appearance revision、magic busy/deadline、ground pending/deadline。WorldEntry.ApplyWorldPacket 在 world/actor 身分改變只 ResetItemSend。WorldSession.pendingPickup 未包含 actor ID，same-world actor 替換仍可能保留舊 pending。

精確基底 DLL `c64bf0069722e28fc174d70d261b7540da6e3f74ee422d4874cbffb77149364f` 的 evidence/baseline-01/result.json 有36筆：6個世界／角色更換後 success/false/error 缺陷重現，5個人工 defensive final-gate 缺口，24個合法 early reply 對照，1個無 socket 事件檢查。這是缺陷觀察符合預期，不是36項產品安全契約全部成立。真S206/S87 wrapper換圖後等過實際900ms，舊groundSending仍阻擋新操作，success發舊事件，error使新世界Faulted。角色替換明列人工matching Status前提；不宣稱一般S8可以改已知角色ID。

獨立封存 r31 review 只作交叉佐證，不能取代上述r32重現：work/engine-slow-pickup-review-r1 seal `29fb10cb6919a9ea07c13185af3ffc0eab2b8be4944c18093cbf84cb00326095`。其140rows不是140pass，本交付不將其計入候選測試。

## 實作契約

只改 Ground.cs 與 WorldEntry.cs。WorldEntry 原 ResetItemSend 保留，在同一 world/actor 身分改變分支追加 ResetGroundSend。後者遞增groundLease、清groundSending/nextGroundAt，並清除舊pendingPickup；不預測成功、改背包或產生動畫。相同actor的正常S87、S158、ground/count、inventory及speed回包不觸發此重設。

送出前 CurrentOwner 必須符合 captured session generation、world epoch、actor ObjectId、ground lease，再檢查 pointerCurrent、inputVersion、SpeedRevision、selection revision、Corrections、AppearanceRevision、現行actor x/y/gfx/weapon、GroundAvailable與原選取物件/count。OtherActionsReady排除move/combat/magic/item busy，檢查其期限、MovementReadyAt、nextGroundAt及既有pendingPickup。不能直接用GroundBusy，因本操作已持有groundSending。

成功准入後的 success、false、exception、finally 只以 CurrentOwner 判斷歸屬，不重用 selection/count/appearance/speed/input fingerprints。合法回包先到時，同owner成功仍只發一次GroundSent；false不發事件；當前owner真正exception仍Cancel/Faulted。舊owner回呼不能發GroundSent、Cancel新世界、改新notice/state/deadline/busy。GroundSent只表示sender成功，非伺服器移轉證明。底層transport的全連線錯誤政策未改。

共用SendGround/ResetGroundSend的所有權防護自然涵蓋既有手動drop，原drop資格、C25格式、數量、功能未變。有限drop回歸檢查傳送與權威回包及stale world，不擴大drop能力。

## 保持原有邊界

- Speed0/1、Brave0肉／箭的單次拾取；原GroundAvailable與pointer gate、SlowActionsBlocked完全保留。unknownspeed／非零Brave的舊direct/pointer差異未處理，並非新增授權。
- GroundRules exact envelope、C112/C25、16byte wire、ground/backpack不同ObjectId、count、距離3、900ms local click guard與3000ms未確認pending政策不改。
- pending heading仍只由本人S158/action15與原pending計算；陌生actor action不清本人pending、重複action不預扣。S87/S120地面與S15/S24/S242/S57背包仍是數量權威。測試使用此引擎現行地物移除opcode120，未改任何protocol mapping。
- 其他175來源逐位元一致，包括Inventory、ActionCadence、PlayerPointer與既有r32 item保護。無新資產、動畫、自動撿物、新物品／NPC／buff。

pickup原生來源研究仍 qualified=false：條件式action15逐幀Speed算術不等於輸入與完整utility時間資格，命令佇列消費／取消、完整scheduler窗與像素未補齊。未知時間不記為0。本候選不開Slow pickup。

## 驗收限制

離線console載入真產品DLL，透過ApplyWorldPacket反射入口和公開操作API；場景初始化及延後sender為synthetic。反射busy/revision/deadline為防禦性測試，另有真公開producer互斥證據，兩者不混稱。所有時鐘採Environment.TickCount64，900與3000測試實際等待，不注入未來時間。未使用GUI、socket、原BIN/JAR handler、服務／DB／SQL，未跑完整3602或重掃14k素材。獨立review、唯一整合者合入與正常live仍待後續派工。
