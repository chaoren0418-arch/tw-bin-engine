# 單次地面肉／箭拾取來源契約

本輪是 source-only。基底為最終 releases/r31，未依賴尚在覆核的r32，沒有產品修改或候選建置。結論：**已補足條件式 action15 接收／metadata／逐幀計算來源，但尚不合格直接移除慢速拾取 gate。**

## 1. 固定伺服器請求與提交

固定JAR SHA d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf。OpcodesClient.C_OPCODE_PICKUPITEM=112；ClientPacketStructure 的112→1072分支為hhdd；C_PickUpItem.start讀H(x)、H(y)、D(地面ObjectId)、D(count)。count按有號32-bit轉long再限0..2147483647，0拒絕；不是模板ID，也不是背包物件ID。

`C_PickUpItem.start` bytecode 0–146驗packet至少13bytes、現行角色、in-game phase、ghost/dead/teleport/privateShop/invisible/invisDelay。147–306讀參數、物件必為L1ItemInstance、座標相符、withinDistance(...,3)、該地面inventory內同一物件引用。311–480再驗Count>0、要求量不超過現有、owner允許、tile-line distance<=3、checkAddItem與非零地面座標。WorldInteractionGuard.withinDistance 0–76還要求sameWorld、knownsObject、可見，且|dx|、|dy|各<=3；sameWorld涵蓋map/showId及物件仍在World的同一引用。

481–514把item、binding、actor、connectionGeneration、client、phaseGeneration、原地面x/y、source inventory、objectId與要求count捕獲進lambda，交L1PcInventory.runItemMutationSerialized。**這是鎖序列化後提交，不能稱已證固定時間延遲或定時排程**：runItemMutationSerialized→runPlayerInventorySerialized→callPlayerInventorySerialized→PlayerActionLock.call，以及inventory serialization，採ReentrantLock後直接呼叫Supplier/Runnable。鎖競爭可能使提交晚於入口。該pickup路徑沒有查得獨立pickup固定delay；900ms不是伺服器來源值，亦不能因此聲稱完整native utility delay為0。

`lambda$start$0` bytecode 0–190在item monitor內重驗binding/current generation、phase/inGame、inventory quarantine、現行角色、dead/ghost/teleport/privateShop/invisible/invisDelay、距離/世界、地面x/y、source inventory仍指向同一item、Count>=request、owner與容量。191–209呼叫source.tradeItem(item,count,pcInventory)。211–326在返回後再驗quarantine及session/binding/phase；失效只保留實際移轉結果而不發成功效果，不反向捏造移轉失敗。327–427 receipt null拒絕並回623；428以後receipt存在才進成功通知。

L1Inventory.tradeItem(item,long,destination)具有來源含有該item、正數、未equipped等檢查；全量移轉走deleteItem，部分移轉建立部分receipt物件並減來源數量，目的地storeTradeItem可能合併既有stack。**地面ObjectId與收到的背包ObjectId不保證相同**。本輪只靜態追packet/物件流，不稽核或操作DB持久化。

成功後C_PickUpItem lambda 428–580設showId、403訊息及loot notifier，設定heading；非GM-invis時先broadcast S_ChangeHeading，再sendPacketsAll(new S_DoActionGFX(actorId,15))。S_DoActionGFX構造器9–23為C158 Dactor C15。S158本身沒有itemId、count、requestId，不能單獨證明特定數量移轉。地面與背包變更發生在transfer內，可能先到；不可等固定S158順序才處理背包。

地面L1GroundInventory.updateItem回S_NPCPack_Item（C87，Count裝在該ground envelope），deleteItem回S_RemoveObject（C9 D地面ID）。目的地L1PcInventory新物品走S_AddItem（C15），既有數量／status回S_ItemAmount/S_ItemStatus（短C24或長C242）；各自用receipt/既有stack的ID。這些封包與S158的actor ID須分別處理，不能本地預扣或替換ID。完整classes與method index保留來源，未執行handler。

## 2. 原BIN接收、動作與逐幀分支

固定BIN SHA e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2；image base0x400000。native-byte-audit.json逐chunk列VA、RVA、File Offset、size、SHA；歷史反組譯先比回原bytes，新增反組譯使用Capstone讀檔，沒有attach、native/oracle或GUI執行。

原BIN `0x947530` dispatch 的case226呼叫 `0x92CB70`。其 `0x92CBA0` format指向 `0x1183AC4` 的`dc`，`0x92CBBB`呼叫 `0xA4A230` 依ID找actor。`0x92CFFE`檢查A3EBF0的當前命令／frame條件：false路徑末段 `0x92D31C–0x92D32C`把收到action送至actor vtable+0x4C；true路徑則在當前action非8時走vtable+0x64。後者在兩個衍生table指向A3C4F0，配置{226,action}並加入actor+0x19C命令集合；普通base table則是51E860空實作。本輪沒有追完此queue的消費與取消，不能宣稱所有actor狀態立即進共用逐幀路徑。未知actor直接結束。action8、70、71等有其他處理；未在所查接收分支找到直接依Speed2拒收action15的條件，但這不是輸入命令允許證明。

vtable base `0x1150C24`、`0x1150E8C`、`0x1150F7C` 的+0x4C分別是 `0x51E920` 或 `0x521E20`，原constructor/destructor存入上述table的指令另有六個靜態xref。兩wrapper均傳入action到 `0xA467A0`；521E20的額外處理限action8。探索曾嘗試+0x80對應933D90，查table起點後否決；舊探索raw chunks僅保留為排除記錄，不是正向證據。

因此固定JAR的S158/action15與固定BIN的opcode226/action15是**由payload與actor/action呼叫鏈對應的語義轉接**，不是相同opcode數字。不能把JAR112當作原BIN拾取輸出opcode；本輪尚未定位原BIN完整輸出命令鏈。

`0xA467A0` 的action switch特別處理1/2/18，15走default `sub_66D480(15)`，不是weapon+attack或spell。它仍受既有類別/外觀表、已死亡action8、特殊merchant/variant等一般分支影響；不可把此結果擴張到所有變身／buff。限定普通合格角色、有效metadata、相應條件中性時：設定frame index(+12)=0→A43560先++index→metadata frame-count檢查→A3EF40→A407D0。原helper會按metadata lookup失敗、特殊119/120等另行回退，沒有完整metadata時不能硬套。

`0xA3EF40`使用當前action、frame和方向選原frame資源，成功metadata分支最後呼叫A407D0(action,frame)。`0xA407D0`先讀frame record（36-byte stride，+14 XOR1812的毫秒或可變FPS分支），再處理其他狀態，然後共用speed switch：Speed0保留、Speed1每幀(3*t)>>2、Speed2每幀2*t。action15不在+540的特殊attack action列表，亦非此switch的排除項。其餘+536/+544/+547、Brave、status helpers、actor+412倍率、變FPS欄位仍須滿足中性前提；Brave0不等於其他狀態自動中性。self路徑從500610讀取加密狀態；other actor才直接讀+195/+198，不能以直接欄位假裝已驗self解密。

`0xA467A0`第一次A43560成功後以frameTime+1呼叫A231C0(callback=A4B060)，存actor+188。A231C0按A228B0時鐘加delay插scheduler；A4B060在 `0xA4B4BE` 再A43560。非末幀分支 `0xA4B5CA–0xA4B635`比較now與既有+188、取至少1的補償後加1，用A23260排下一次並更新+188。這不是「一次拿四幀總和sleep」。特殊遠端隱藏/狀態分支仍存在。

**frame0與完整操作窗仍不可混算**：42個metadata的15.get皆四token，ticks=[1,1,4,4]。上述初始化首先呈現index1，index2/3續行；結束時A43560重設index0並取得其時間，再經vtable+0x48交回動作狀態（51E8E0→A44500）。四token靜態base=[41,41,166,166]、條件式haste=[30,30,124,124]、slow=[82,82,332,332]。index1–3的373/278/746僅metadata片段和，**不是已認證的總utility冷卻、release或wall-clock origin**；還有scheduler+1、late compensation與terminal狀態轉接。下一個精確可查地址是A44500、A228B0餘下clock分支及輸出拾取命令的caller，不以0ms填補未知。

## 3. 42 profiles與現有素材

讀取既有reference/object-assets/inner-list.spz及releases/r28/assets/players/manifest.json。只按既有42 profiles的gfx抽原entry；42份profile.entryHash全符。13個唯一gfx全有明確`15.get(1 4,...)`，不需推測成attack/spell或weapon offset。

現有42個profile的sequences只有walk/attack/damage/breath/die/spellDir/spellNoDir，沒有pickup。各profile的frame集合都不足以呈現所需32個方向／frame鍵；例如gfx0需要sprite3225的sid88..95、gfx61需要sprite3213的sid24..31（詳見metadata/pickup-frames.json逐profile的missingFrameKeys）。即使站立／首token有部分共享frame，也不能用替代圖冒充其餘拾取幀。

本輪沒有讀全14k素材、沒有重解PAK、沒有新造／補寫sprite。metadata有來源≠所有SPR pixel slices已有交付。本輪把缺口精確縮小到指定sprite/sid/frame；後續若授權，應只核相關IDX entries與既有extractor輸出／原SPR slices，建立完整方向、frame bounds、pixel與parser證據，再談animation接線。

## 4. r31目前支援與最小後續

GroundRules.IsGround只辨識gfx36 `$23`肉與gfx66 `$61`箭的既有嚴格S87 envelope（action/direction/light/speed等地物欄位為0，Count<=2e9、name/count、空title/clan/owner、flags等精確條件），此處地物Speed不是玩家Speed。GroundAvailable與PlayerPointer.PickupAsync各有SlowActionsBlocked；目前不能用本次server檢查取代兩者。直接GroundAvailable另有unknown/nonzeroBrave邊界不足，後續資格應明確保守收斂。

SendGround已有單次C112、count/距離<=3、選取/輸入/SpeedRevision/校正/world/session重驗；900ms local click guard、等待本人action15的pending pickup與3000ms未知結果提示，均是engine政策。S158/action15只ReceivePickupAction記錄actor並依pending目標算朝向，立即return；沒有AnimateCombat或pickup動畫。現有ActionCadence只還原1/18/19；不可為15呼叫現有attack/spell時間路徑。S158到達會清pending，不代表動畫已播完，也不替背包計數。

後續若做ground送出流程還要獨立驗queued busy與admitted後world/actor/lease歸屬；r31 GroundSent/catch只看session、finally看session+lease，不能宣稱已具備r32 item的修復。r32目前只是另案Inventory/WorldEntry候選，本輪不引用它作已接受基底。

**目前合格項目**：固定server正向拾取/重驗/actor action15與分開數量回包；原BIN接收action15在A3EBF0=false等條件下到共用逐幀speed算術的靜態鏈；42份metadata與明確缺frame列表。**未合格項目**：直接移除Speed2 pickup gate、原BIN輸出輸入限制與全操作互斥、A3EBF0=true的action queue完整消費／取消、完整self/extra-status資格、utility總冷卻／起點、完整pickup pixel資產及renderer、live正常產品驗收。

建議下一最小增量先做「指定action15素材與terminal scheduler來源補齊」，不含丟棄、自動撿物、其他物品/NPC/buff。若產品要採僅server-authoritative請求而不聲稱原動畫等價，需主管另行明定這個縮限契約；本報告不自行授權開放gate。
