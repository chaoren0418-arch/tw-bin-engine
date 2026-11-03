# ENGINE-SLOW-UTILITY-SOURCE-R1 — 原始來源資格交付

本輪只完成來源查證，沒有產品候選或功能完成宣稱。固定基底r30；r31加速候選與其他封存工作完全未改。本目錄封存後停寫。

建議下一個最小增量：**Speed2／Brave0下，單次使用既有嚴格辨識的普通自我加速水40013**。資格限於server-authoritative請求與既有速度／效果回報，不認證原native輸入排程。原StatusHaste.execute bytecode262/294/326明確清除SLOW29、MASS_SLOW76、ENTANGLE152；360..414之後仍回報mode1、setMoveSpeed(1)、設STATUS_HASTE。這是正面慢速處理分支，不是以沒看到speed check推論。共同物品gate、decay698與其他原服拒絕照常。

| 項目 | 本輪資格 | 原因／下一缺口 |
|---|---|---|
| 普通加速水 | qualified，僅上述有限請求提案 | 明確慢速清除／回報分支，原native速度與獨立效果接收鏈已核；原輸入parity仍未認證 |
| 紅水／白水 | 各自unqualified | handler/消耗/效果已映射；不足以認證原native慢速使用動作／互斥 |
| 普通肉 | unqualified | Food1先消耗、飽食條件已證；慢速輸入資格未證，不能因缺speed參數開放 |
| 普通回家卷 | unqualified | 原服落點／拒絕／扣卷已證；慢速輸入及跨世界完成契約未證 |
| 武器／防具穿脫 | 各自unqualified | 500/501是禁止類別，不是毫秒；慢速姿態及裝備效果資格未證 |
| 拾取 | unqualified | 成功會送ACTION_Pickup15；缺少action15原native選擇器／幀／慢速時序證據 |
| 丟棄 | unqualified | 交易路徑與限制已映射；原native慢速輸入／節奏未證 |
| NPC單次交談 | unqualified | generic handler可再分流executor；原native慢速輸入未證，且目前入口不一致 |

unqualified不是宣稱原服禁止，也不否定現有正常功能；是本輪不足以移除緩速保守閘門。source-contract.json逐項列完整handler鏈、方法bytecode offsets、native地址／缺口、server與本機延遲區別、目前guard及待實作驗收矩陣。

## 具體發現與下一實作契約

1. **不能刪除全域SlowActionsBlocked。** 只能對SupportedHastePotion且Speed2/Brave0建立局部例外；其餘既有用品及操作保持保守邊界。未知Speed／非零Brave不進入新例外。r30 ItemAvailable本身不是通用NormalSpeed.Supported檢查，不可假定所有入口已有同一速率gate。
2. C164待送出已有speed revision，但Inventory最終validator尚未完整重驗magic互斥；completion/error只有generation。新實作須補同session換世界／外觀／校正／lease等回呼防護，不能照抄舊完成點。queued utility後若另一action已准入，須在真正admit重驗busy；注意不能直接使用包含自己itemSending的MovementBusy造成永遠拒絕。
3. 已送出加速水的100ms是r30本機guard，不是原服100ms reuse，也不乘2。原服速度從2到0再到1或直接1時，已准入慢步／慢攻／慢施法快照與deadline高水位不能縮短。飲用本身不新增角色攻擊／施法動畫，S55效果191維持獨立時序。沒有回報就不預扣物品、改Speed或推算成功。
4. **NPC入口差異已實測：** PlayerPointer.ActAsync(talk)擋Speed2；直接CanTalk/Interact("talk")可送C34；direct queued請求在2→0→2後仍送出，因未核SpeedRevision。這是現有r30觀察，不是開放其他入口的許可。NPC後續須先決定統一有限契約，不能僅刪pointer gate。
5. 共用矩陣要求speed同值／雙向／transient、各busy消費端−1/exact/+1、admit後late success/error、new lease、HP/MP不預測及原回包確認。這些是**下一實作待驗證**，本輪沒有用計畫冒充通過。

## 固定來源與本輪實際驗證

- 基底baseline SHA `1329700b9fabf59e7406d62790ea8a32758ee9771963d0dcd49c7cde85708a66`；r30 seal `199ce6fe81130138e9fedf98e688928ce44b0a45c2aebd1b92fdd9519b8225dc`。
- BIN SHA `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，ImageBase0x400000。VA0x94C530接收dch並寫actor+195；0x934690建立獨立效果；0x8ABC10走效果自身frame loop。VA/RVA/File Offset/chunk SHA詳見proof-index及collection。沒有原native執行或完整utility輸入鏈宣稱。
- JAR SHA `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。20個原class直接從固定JAR擷取並javap靜態反組譯，沒有類別初始化、handler交易或伺服器執行。Java offset是方法內offset，不是BIN地址。
- 讀取並核對r30必要source子集，未重掃177source以外的素材或全release。source-pins保存外部固定檔、JAR entries與native byte chunks；engine-readback僅是唯讀來源副本，不是候選。
- 36項離線helper觀察通過：實際r30 DLL、合成actor/item/NPC/clock、in-memory sender，核對正常/緩速用品與地面gate及NPC差異。沒有socket事件。此數字不是新產品測試或sourcequalification的動態證明。
- 物品500/1000/0/1500ms值只保留r30／歷史接受契約，**本輪不讀SQL、不查DB、不驗現行模板資料**。不能據此保證任何目前角色／服務配置。

## 產物與重跑

source-contract.json與proof-index.json為主契約；result.json明列1項qualified、9項unqualified、36項舊DLL觀察、0項新產品檢查；source-pins.json固定原件；artifact-manifest.json封存所有採用本文及實際產物，排除assets junction與暫存、cache、obj/bin，seal不列自己。

如需複核，先在主管授權的新scratch複製本報告與classes/native、probe、NuGet.Config、vendor；保留build/probe層級，建立scratch/assets唯讀consumer junction至releases/r28/assets，建立evidence/tmp/cli-home/nuget。evidence/probe-build.command.json及probe.command.json記錄實際argv；將輸出路徑換到scratch後只建置並執行console helper。不要在本封存區重跑collect/probe_setup/package，這些腳本會寫產物。靜態查證可直接核對class SHA與proof-index，不需再反組譯原件。

真Goal讀回blocked，未建立／恢復／修改。未操作DB／服務、GUI、正常產品、原生live、socket；未新增chat/subagent。本來源交付等待主管安排有限實作與獨立覆核，並非全案停止。
