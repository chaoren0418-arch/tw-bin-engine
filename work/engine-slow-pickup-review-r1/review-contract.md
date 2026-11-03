# ENGINE-SLOW-PICKUP-REVIEW-PREP-R1

本交付為獨立來源／驗收準備，`candidateReviewed=false`、`productCandidate=false`，不授權實作。基底固定最終releases/r31，沒有讀取作者未封存資料；其來源seal於本輪後段收到後才新增靜態交叉核對，見sealed-source-conclusion.md。r32整合中，不以它冒充本次基準。所有判定限既有嚴格辨識的地面肉／箭、單次拾取，不含drop、自動追物、全物品、NPC或新buff。

## 固定身分

baseline-final SHA `00e9d4664024119595faeead50ade5827258763e47dc3d7068ea9b78abf8c813`，r31 release seal `1d5b1c149495cb2b44f50daf2c3361eb15e8000ba27bf133d1029735f10cfd90`。177來源及產品DLL `12ad5bbfac696033ee1f8d807346f401216138f4ce51cb58da9438e0b4ba5ec1`、audio與assets integrity均重核。

JAR `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`：4個原class bytes及新靜態javap保存於classes，完整entry/class SHA/javap SHA/argv在class-pins.json。沒有初始化或執行handler。BIN `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，imagebase0x400000；後段交叉核對重驗101個作者列冊chunk的PE映射與原bytes，僅接受所列條件式接收鏈，不宣稱完整輸入與scheduler還原。

## JAR 正向提交與 action15 必要條件

以下offset皆為具名Java method內bytecode offset，不是BIN檔案位置。

| method與offset | 正向控制流與拒絕條件 |
|---|---|
| `C_PickUpItem.start` 0..54 | packet至少13bytes、client存在；active player與session phase必須current/in-game。 |
| start54..147 | 捕捉ActiveCharacterBinding、pc connectionGeneration與sessionPhase generation；拒絕ghost/dead/teleport/privateShop/invisible/invisDelay。 |
| start147..205 | 讀H x、H y、D objectId、D count；讀取的signed D經long處理後必須>0，正常client限1..Int32.MaxValue。不能用item template ID代替ground ObjectId。 |
| start206..310 | World.findObject必須是L1ItemInstance；物件x/y等於請求；withinDistance3；從該物件map/x/y取得ground inventory且getItem(id)必須同一物件參照。 |
| start311..478 | 物件Count>0、requested<=Count、ownerId為0或本人，否則訊息623；tileLineDistance<=3；checkAddItem==0；x/y非零。 |
| start481..514 | 捕捉item、binding、pc、兩種generation、x/y、ground inventory、ObjectId、requested count，交`L1PcInventory.runItemMutationSerialized(Runnable)`，不是在最初檢查通過即宣稱成功。 |
| `lambda$start$0` 0..190 | synchronize item；再次驗binding current、connectionGeneration、phase generation/inGame、inventory非quarantined、current player、角色狀態、withinDistance3、座標未改、ground lookup仍同一物件、可用數量>=requested、owner、checkAddItem。失敗提前退出。 |
| lambda191..209 | `groundInventory.tradeItem(item,requested,pcInventory)`取得receipt。此處不把未讀的tradeItem內部交易/持久化流程當已全面驗證。 |
| lambda211..327 | 提交後再驗非quarantined、binding/connection/phase/current player；失效時保留結果但不產生成功效果。 |
| lambda327..427 | receipt必須非null；null記錄並送623後返回。不因方法被呼叫就視為拾取成功。 |
| lambda428..541 | 有效receipt設showId -1，通知403及相關loot/log等，處理drop source/light；依原請求座標targetDirection再setHeading。 |
| lambda544..580 | `isGmInvis`為true直接跳580；否則先broadcast S_ChangeHeading，再sendPacketsAll(new S_DoActionGFX(pcId,15))。**action15不是所有成功提交都必然出現的回報**，GM隱身是明確例外。 |

`WorldInteractionGuard.isCurrentPlayer` 0..78要求connected、client activeChar與pc netConnection雙向一致、account相同、正ID及World.get.findObject(id)同參照。`currentObjects`0..55要求玩家與物件都在World同參照；`sameWorld`0..35要求mapId及showId相同；`withinDistance`0..79還要求known/visible，x/y差各不超過上限。這些為正向身份與空間約束，不是只看client距離。

`S_DoActionGFX.<init>(int,int)` 9..23寫 **C158 Dactor Caction**；`S_ChangeHeading.buildPacket`0..19寫 **C122 Dactor Cheading**。上述只證固定JAR wire。後段封存交叉核對支持固定BIN opcode226→0x92CB70的條件式語意對應，**禁止把JAR158直接當BIN opcode158或同一handler地址**。原native輸出C112對應仍未知。

伺服器handler的允許狀態、提交回報不等於原native慢速輸入與動畫時間資格。即使這段沒有Speed參數，也不能由此開Slow gate。900ms是現有引擎防連點政策，本輪不稱它為server reuse或native動畫長度；原生utility時間未知，不填0、不用attack/spell時間代替。

## r31 實際產品路由

- Ground.cs的GroundRules.IsGround只認gfx36/`$23`與gfx66/`$61`，以及Action/Direction/Light/Speed/Lawful/Title/Flags/Clan/Owner/Rank/Hp/Brave/Level/Unknown/Tail等完整envelope；WireExp作count，上限2,000,000,000。ReceiveGround把物件從可攻擊objects移走，Count0移除、selection失效。S87沒有template/type判別，不能以gfx單獨擴大到其他道具。
- `PickupSelected(count)`及`PointerPickup`都進`SendGround(true,count,...)`。C112固定H地面x/y、Dground ObjectId、Dcount，16bytes含padding。數量正值且<=地面數量，距離Chebyshev<=3，無自動走近。
- 初始GroundAvailable拒絕Speed2、action lock、trade、未ready、dead/HP0；MovementBusy另含move/attack/magic/item/ground。最後predicate核session/world/input/speedRevision/selection/correction、ground記錄與Actor位置、movement/combat/item及MoveReadyAt/combat期限，但沒有獨立actor ID/groundLease/magicBusy快照。
- admit建立`nextGroundAt=TickCount64+900`與pendingPickup（座標、朝向、world epoch、3秒期限）；未預扣地面/背包，未預測pickup動畫。GroundSent只代表sender成功，非伺服器交易成功。
- `PlayerPointerController.PickupAsync`另有Slow、focus、MovementAvailable、walking、visibility、selection與stamp guard；正常單次呼叫。direct/低階PointerPickup在unknown Speed3或Brave1可送，controller拒絕；這是現有入口差異，非開放許可。
- 正常收包 `LoginConnection.cs:62 → ApplyWorldPacket(WorldEntry.cs) → World.Receive`。`WorldObjects.cs` S158/action15專門呼叫ReceivePickupAction並return，未走AnimateCombat。Ground.cs只更新PickupActions、本人pending朝向、清pending；其他actor不消耗本人pending。S122更新遠端朝向，本人由pending+action15推導。
- GroundTransfer/GroundIntegration包含既有live登入、drop/pickup、兩角色守恆及保存重登腳本；本輪只讀，不執行、不把其歷史scenario當本輪live證據。

## 42 profiles 與原native待覆核門檻

本輪只讀players/manifest.json明列的42個既有appearance JSON，逐一驗manifest SHA與檔案SHA；沒有掃PAK、14k素材或抽取新資產。profile-audit.json逐列列gfx、weapon、sprite、path/hash、action集合、pickup方向／key／duration等。

**42/42都沒有`pickup` sequence，完整8方向pickup profile數為0；serverActionMs及actionMs也無pickup欄。** 現有action集合僅attack/breath/damage/die/spellDir/spellNoDir/walk。獨立第一階段僅能證明產品未提供可用pickup序列；後段封存交叉核對另以原inner-list.spz重算13gfx/42profiles的15.get四token與missing frame keys，確認42份均不完整。仍**不等於原PAK中不存在該幀**，未做新像素抽取或完整alias/runtime資格。不可把frame總數或相似畫面當pickup資產資格。

PlayerProfiles.Load只要求原七類action完整8方向；ObjectAppearance.At只按已存在sequence/ticks/milliseconds抽樣，不能替不存在的pickup推導初始索引。ActionCadence.Name只接受1/18/19；generic actor倍率不是action15適用證據。

待作者封存後必須逐項交叉核驗，缺一則保留unknown：

1. 原BIN dispatcher table與原action封包接收鏈：exact opcode、payload布局、actor lookup、action15分支、VA/RVA/file offset、每chunk SHA與固定BIN原bytes一致；說明JAR158到BIN版本的語意映射。
2. action15至actor action selector完整正向路徑：own/remote、gfx/weapon/transform/姿態、任何特殊角色或動作例外；不能只引用某通用函式含case15。要求選用sprite/action metadata的參數、分支條件與alias解參照。
3. frame初始化、advance、draw、duration取樣的實際先後次序。首個顯示是index0或先advance到1、零tick處理、末幀/循環/中止、8方向索引、每步metadata取樣都必須由原指令與資料支持，不用其他effect動畫的順序代替。
4. Speed0/1/2、Brave0是否**真的從action15路徑呼叫A407D0**，其call-site、action參數、base/frame-duration來源、整數乘除與截斷次序；列出未走該路徑的例外。已知其他動作generic倍率不能證明pickup倍率。unknown/Brave保持未資格邊界。
5. 42profiles逐個對照原action metadata、direction0..7的sequence/ticks/frame key和映射；alias須證原來源鏈，不能複製相近weapon/性別/方向圖片。把「缺metadata」「缺像素」「已存在可重用」三者分開。
6. 若另派定向抽取：固定IDX/PAK檔SHA、entry index/name/offset/compressed/raw size與hash、解碼器版本、原SPR header/bounds/pivot/blend/透明/像素完整核對、八方向所有引用幀與缺幀清單、不可見邊界與alias證明。這是未來抽取門檻，本輪沒有提取或像素驗收。

## 後續候選負向與生命週期契約

Slow輸入/動畫目前unqualified；可先把已存在正常pickup生命週期修正作獨立、可測的小範圍提案，但要新派工與final r32基準差異綁定，不得自修。

准入：exact肉/箭、count/距離/選取/record、session/world/actor/lease/input/correction/speed revision及cross-action busy；同值或往返revision必失效。人工mutation與正式公開API互斥分開，不能用注入magicSending證明正常並行一定可達。

完成：根據session/world/actor/lease隔離舊成功/error/finally，世界或actor換置時必須同時處理groundSending/deadline與pendingPickup歸屬，避免只擋舊finally卻永久busy。真正wrapper新世界ready後，新操作可admit；舊callback不得改新busy/deadline/events/state。actor同world替換測試需保持Ready且標出synthetic Status前提，不能用初始化不完整掩蓋身份問題。

合法回包：已admit後S158(action15)、S87地面數量/本人校正、S67、S15新增背包、S24數量可先於sender完成。不能機械搬入場snapshot到completion，也不能由S158推算扣量或把sender false/error變成功。回報數量/朝向以原包為準。當前真正sender錯誤仍需原連線失敗處理，不吞全域transport failure。

驗證負向至少包含失效玩家/物件、old session、world/actor更替、0/超量/count>Int32.Max、距離4、地面座標移動/記錄變更/移除、擁有者/capacity/quarantine（後四者server static或將來隔離oracle，不能假造client成功）、同值/往返speed、busy、false/error、重複/未知actor action15、timeout、合法回包早到。實際已跑與僅待驗務必分開。

## 執行界線

最終baseline-probe.json為140筆實際r31 DLL資料；含觀察與未來安全契約差異，不是產品接受測試。時鐘同真Environment.TickCount64；一次3100ms確認未收到action15時的timeout fence，各wrapper生命週期案例實等900ms後再嘗試新操作。helper沒有反射改Clock到未來。類別反組譯只靜態，沒有handler/native emulator/GUI/socket/服務/DB/SQL；沒有drop命令。Goal null未修改，沒有chat/subagent/模型更改。封存後停寫，等待作者精確seal及主管後續派工。
