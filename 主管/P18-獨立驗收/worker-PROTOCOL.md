# P18 地面物品與拾取協議

固定原 JAR SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；ClientLogin=2。原class/Java/javap與hash在reference/ground/manifest.json。位移均為 JVM method bytecode offset，非 PE VA/RVA。

## 已由來源及固定序列化器核對

|方向/opcode|結構與來源|
|---|---|
|C25 丟棄|H x、H y、D 背包object ID、D signed-positive count；客戶端零補齊到16bytes。C_DropItem.start offsets 6/11/17/23。|
|C112 拾取|相同欄寬，ID是地面object；readD原回傳int再i2l，正數最大2147483647。|
|S87 地面|共用CHARPACK：H x/H y/D object/H ground gfx/C0/C0/C light/C0/D min(count,2000000000)/H0/S name/S空/C0/D0/S空/S空/C0/C255/C0/C0/C0/C255/C255。MS950 NUL字串，8byte envelope zero padding。|
|S120|D object ID，原地面移除；同opcode也可移除人物。|
|S5/15/24/57等|沿P17原背包清單/加入/狀態/移除模型；template ID不在wire。|
|S158 action15|D actor/C15，原拾取動作確認，含自身；記錄事件，尚未繪製原拾取動畫。|
|S122|D actor/C heading；原拾取broadcastPacketAll排除自身，由旁觀連線接收。|

S_NPCPack_Item.buildPacket offsets 0–38為opcode/位置/ID/groundgfx；78–90取數量並cap；98–303建立名稱；304–371寫完整尾封套。原serializer七組樣本及C#解碼見reference/ground/oracle.tsv與evidence/ground-oracle.json，明示合成L1ItemInstance subclass參數，沒有啟動遊戲或DB。超過2B名稱可保留完整原數量，而數量欄cap；本階段不將該不一致指紋冒認為可操作地面物。

## 原限制與有限支援

原altsettings `dorpitem=false`，原`道具_物品強制丟棄`唯一40056肉。ItemTable將trade=0解作tradable=true。原40056為desc10/icon124/use38/name$23/groundgfx36/stackable；產品辨識已核對的封包指紋，不宣稱wire有template ID。只開放該可丟棄堆疊；其他原物品仍列背包但不提供未核對的丟棄。

C_PickUpItem檢查當前玩家/世代、同世界與可見/known條件、原地面座標、距離3、owner、正數量<=原可用量、容量與重量；提交前再次檢查。成功可能合併既有背包堆疊或新增物件，部分轉移可能創新ID；產品完全按原包更新，無本地假扣假增。原ClientLootNotifier的RO分支自訂190/192保留raw，不作通用遊戲協議使用。

地面並無通用type discriminator。僅對原肉groundgfx36、name/count、完整尾資料與原type9素材均匹配的S87辨識為GroundItem，與WorldObject/近戰目標分開。其餘外觀保留未知/既有物件，不靠非PC旗標猜成物品。未知更新不建立幽靈。S120、零量、map/world世代、取消/斷線清除選取；地面不進角色占格路徑。

## 單次操作與朝向

拾取/丟棄入口重查connection generation、world epoch、input version、選取revision與當前record、座標、移動/戰鬥/藥水忙碌及正signed32數量。入口忙碌直接拒絕，不排隊；發送鎖取得後再驗證。900ms是明示本機連點guard，未改原規則。P按住只觸發一次；Web命令嚴格epoch/itemId/count白名單；輸入焦點停止遊戲輸入。禁止自動行走、自動拾取與自動重送。

成功自身S158/action15與尚未結束的同世代C112關聯，按固定原L1Character.targetDirection的float比值+0.59f捨入計算朝向；同格保留舊heading。沒有把請求本身當成功。392組原JAR向量與來源存reference/ground/supervisor。旁觀原S122獨立驗證方向。3秒沒有確認時僅顯示「結果未確認」，保留原請求關聯並停止後續操作；不因超時開放第二請求覆寫關聯。晚到成功仍對應第一請求。取消/重登或world重置會清除；合成延遲反例見ground-final-regression。

## 原素材

肉地面外觀取原Sprite.idx entry48732、PAK File Offset971303715、244bytes的36-0.spr，raw SHA `1734424f17adb848324f8e6c2aba6ad4bafeb687f990aa59b62426d448899ee8`。decoded-list.spr #36標type9；SPR frame type1，共2 blocks、57個opaque pixels。原C# SprReader獨立48×24 canvas1152像素與11×9裁切對比由主管提供，原收據與hash保留reference/ground/supervisor。背包使用獨立124.tbt，不能拿背包圖代替地面圖。

沿既有tile/actor排序保留地圖遮擋，不抬高物品讓它穿過建物。ground-layout及r2保留被房屋覆蓋而沒有可點像素的失敗；r3明示合成空地位置，可見原SPR像素與選取、真實WebView2地面PNG通過。不將合成布局稱為原服轉移。

## 真實測試邊界

全新43318 DB / 42039 game，四主角與四獨立旁觀為有限初始合成fixture，各主角8肉、6藥水、3金幣、1卷軸，旁觀無物。所有轉移由原協議，動態開始後SQL僅SELECT/SHOW作oracle。原ItemDeletionTime=10是全服周期，不是逐物品10分鐘；倒數/清理raw保留，測試不改timer、不補發清除物。

完整動態通過證據為 `evidence/ground-worker-cli-r2`（40雙端檢查點、1319欄位）及 `evidence/ground-worker-ui-r5`（60雙端檢查點、1991欄位）的 result/db-proof/independent-ground；不以合成回歸替代真實結果。主/旁觀每連線與重登首seed先掛capture，完整server plain、sent world commands及applied sequence保留。原登入帳密不寫入證據。兩端逐包獨立Python重建地面/背包，再核個別流程8肉守恆、S158/S122方向及SQL正常離線保存。

首次CLI等待空背包S5失敗，原C_LoginToServer.loadItems offsets111–116/370證實空背包不送S5。產品在真實世界核心與自身C95→S123同步完成後明示推定空背包，不偽造S5。重登清除該狀態，真實S5仍覆蓋清單。

第一輪UI在全量丟棄8肉後因角色遮擋無可點像素而失敗，正常斷線後UI-r2/r3/r4重登、等待與步行未再收到該原地物。原UI物件1900181024的8肉去向未證；詳 `VISIBILITY-NOTE.md`、失敗history-audit與 `evidence/finite-ledger.json`。UI-r5使用CLI另一份原始8肉經原丟棄／拾取暫借，通過後由finite-return-r2原協議歸還；finite-return第一次占格路線失敗未送地面請求。已保存24肉，另8肉未證，不能宣稱全環境32肉守恆。沒有SQL補回或使用主管保留角色。

本階段不是完整引擎；沒有自然怪物地面掉落實證時保持未驗證。沒有裝備、交易、商店、倉庫、製作、全地圖或自動戰鬥。P10舊EXE仍未驗收但依授權不阻擋本自主引擎階段。
