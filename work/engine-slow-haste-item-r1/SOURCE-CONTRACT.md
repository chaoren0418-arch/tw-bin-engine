# 緩速下普通加速水的有限請求契約

本候選沿用已接受 r31 stage 的完整 177 source/audio/web。主管後續提供 final-base-binding，另驗最終 r31 release 的 177 檔與四產品身份相同；不改原 staged baseline、不取用變動共享 engine。

## 資格來源與限制

依已封存 engine-slow-utility-source-r1/source-contract.json（SHA 85385401cdf40ef31ad966577da8527ed30ad1dae37b19eea6c8267fc138e0fc）與 engine-slow-utility-review-r1/CONTRACT.md（SHA 5c296ceaaf625410e1dbfa833812b9eb4312cadea9475b193868d4be0aed23ba）。兩來源 seal 分別為 e21d05d5638742afdb069d988626e103a024f8c0555102cda6264a21e9eb4639、0844c5f809903930bfe10ebfd658b3f22bbed72693b1bb584a555aecabcb301a，精確外部檔案身份在 input-pins.json。

原 JAR d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf 的 StatusHaste 完整方法包含慢速效果 29/76/152 的清除，以及其後設定 moveSpeed=1 的分支。沿用前階段已核的 C_ItemUSe／BuffAll／PotionBlocker／StatusHaste／L1ItemDelay／S_SkillHaste 來源鏈；這足以支持普通加速水的單次 server-authoritative 請求增量，沒有證明原 native 慢速 utility 輸入排程。

固定 BIN e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2，image base 0x400000。來源已證 VA 0x94C530（RVA 0x54C530、File Offset 0x54B930）依 actor 寫入 +195 的速度，VA 0x934690 特效建立與 VA 0x8ABC10 獨立特效週期。原 opcode 與目前 profile 的 S255 不可直接混為相同版本碼；只沿用已接受的語義適配。本輪沒有執行 BIN、JAR 或重新研究 SQL／DB。

普通紅／白水、肉、回家卷、武器／防具、拾取／丟棄、NPC talk 均未取得新增慢速放行資格。未知 Speed 或非零 Brave 的 ItemAvailable 保守拒絕是此輪明確政策收緊，不宣稱原服必然禁止。

## 最小實作

Inventory.cs 保留原 SupportedHastePotion：ObjectId 非零、Count>0、DescriptionId=17、IconId=77、UseType=246、Charges=0、Bless=1、Flags 上三位為零，Name 精確為單瓶 `$234` 或 `$234 (Count)`。Count 的數字格式沿用 invariant。沒有新增 template ID 識別或修改其他用品原有 envelope。

Speed0/1 且 Brave0 使用既有 SupportedUse；Speed2 且 Brave0 只允許上述普通加速水。C164 仍為持有 ObjectId 的 little-endian D，8-byte packet 尾部保持零。測試使用 ObjectId=1234，封包 A4D2040000000000；40013 是來源模板稱呼，不能替代封包持有 ID。

送出前與最後 admit 共用 ItemAvailable。queued 重驗 generation、WorldEpoch、actor ObjectId、itemLease、選取 revision、inputVersion、SpeedRevision、完整選中 record、死亡／HP、action/trade lock；另保留設備 revision，新增 appearance/correction/座標/gfx/weapon 快照。S255／S67 同值與往返都使 queued 舊命令失效。最後重驗 movement/combat/magic/ground busy 與真實 deadlines，刻意忽略這次操作自己的 itemSending。

admit 後成功、錯誤及 finally 的歸屬只用 generation、WorldEpoch、actor ObjectId、itemLease。不能用 selection、SpeedRevision、record 等送出前快照取代歸屬：合法 S255 直接1或2→0→1、S67、S24/S242、最後 S57 可先於 sender 完成，仍保留一次 ItemSent。沒有 S67 也可完成；不假設全域回包順序，不寫回舊速度／數量。

WorldEntry.cs 的 ApplyWorldPacket 在 World.Receive 後比較世界與自身 actor identity；改變即 ResetItemSend。此補充讓真 S206／S87 失效舊租約、清掉舊 busy/deadline，允許新世界公開 API 取得新租約。只把 finally 改成嚴格歸屬而不清理世界切換，會遺留 itemSending；因此這個第二檔差異是生命週期所需。原回包正常更新 Speed/Brave/count 不會改歸屬。

舊 world/session/actor/lease 成功不發 ItemSent、不延長新 deadline；舊錯誤不取消新世界；舊 finally 不清新操作。同歸屬真正錯誤仍 Cancel/Faulted，即使先收到合法 speed/count 回包。LoginConnection 的實際 transport/global fault 程式完全未改。

## 時間與權威

普通加速水 UseDelayMs=0、ItemUseMarginMs=100 保持原值。100ms 是本機保守 guard，不乘2或0.755，不是原 native scheduler=0 的證據。完成 deadline 仍取 max(existing, realNow+delay+margin)。紅水500、白水1000、肉0、回卷1500、既有裝備1000 的 delay 均保留，另加100ms local guard。

不由 C164／ItemSent／S55／timer 預扣物品、HP/MP、解除慢速、設定 Brave 或承諾成功。Speed、Brave、Count 只依各自既有原回包處理。r31 ActionCadence、NormalHaste、Combat、Magic、OnlineMovement、各 renderer、flight30/60 與 effect191 來源皆逐檔不變。以真實時鐘的公開慢 move/attack/magic 檢查確認 S255→0/1 後既有 deadline 不縮短且道具仍被 busy 擋住。

NPC direct/pointer 的既有差異保持：direct Interact 可走舊路，Speed2 pointer controller 仍擋住。本輪不把它修正或宣稱所有 talk 都禁止。
