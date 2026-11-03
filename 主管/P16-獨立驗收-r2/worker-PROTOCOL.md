# P16 協議與證據

範圍為 gfx0 空手單次近戰。固定 Server_Game.jar SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。未修改原服；原 class、Java 對照、雜湊及 javap 指令位移在 `reference/combat/manifest.json`。這些位移是 JVM method bytecode offset，並非 PE RVA/VA。

## 已驗證欄位

|方向 / opcode|內容與限制|
|---|---|
|C→S 229 / E5|D targetId、H targetX、H targetY、可選 byte；16-byte 加密前補零。選定目前世界的存活物件才送出。|
|S→C 30 / 1E|本階段24-byte melee：C action、D attacker、D target、H damageWord、C heading、H x=0、H y=0、C flags；實際資料18byte後補零。|
|S→C 225 / E1|ClientLogin=2：D currentHP、D maxHP，實際生命值以此及原 OwnCharStatus 為準。|
|S→C 237 / ED|D objectId、C HP byte；0..100表示比例，255隱藏血條，不能解釋為死亡。|
|S→C 158 / 9E|D objectId、C action；action8為原死亡動作。保留屍體，立即清除選取並禁止攻擊。|
|S→C 120 / 78|D objectId；原服移除時才移除世界物件。|

原 S_AttackPacketPc/Npc、S_UseAttackSkill、S_UseArrowSkill 共用 opcode，40-byte 等技能/遠程 constructor 不套用近戰格式。24-byte但XY非零亦保留為不支援變體，不能延長既有自身冷卻。短於24byte的近戰 envelope報截短錯誤。未知ID不建立幽靈物件。死亡後舊動作/受擊/重複死亡不復活或重設屍體動畫。

固定環境 ClientLogin=2。damageWord 是原 writeH 欄位，未證明等於最終實扣HP；零不能區分miss與零傷害。UI因此只標「原服傷害欄（非HP扣除量）」。命中動畫、送出預測、原攻擊回報與實際HP變化分別記錄；C_Attack onAction 返回時，非同步 DamageCommit 未必已完成，不能將該瞬間資料當最終傷害。

## 節奏與世代

原 gfx0 spr_action action1 為20frames/24fps，SprTable.calcActionSpeed使用40ms基準，得到800ms；原素材metadata 20ticks/24則為833.33ms。顯示序列按原服800ms縮放，未任意關閉原速度檢查。move仍為640ms。受擊action2無spr_action列，以原素材14ticks/24=583.33ms僅作顯示。零tick素材marker原樣保存。

攻擊與移動共用傳送gate。送出前重新核對連線generation、worldEpoch、TargetRevision、inputVersion、correction、物件ID與座標、存活/外觀狀態；忙碌輸入直接拒絕，不排隊或延後重送。真正新增的自身原攻擊回報才延長冷卻。G按下清除先前移動intent；按住不連擊，WebView輸入框焦點不攻擊。原HP0或死亡動作禁止操作。取消、斷線、地圖切換與重連清空戰鬥狀態。

## 素材

原Sprite.idx/PAK及list/spz來源；gfx0與狼人gfx1110使用共用ObjectAssets/Renderer compositor，walk/attack/damage/breath/die八方向，死亡停最後frame。不存在素材明示不支援，不用站立替代。新增/擴充72SPR、392frames已由獨立C#原解碼器逐像素及邏輯座標核對；全資產216SPR/1496frames另由主管獨立核對。證據為 `evidence/combat-pixels-result.json`、`assets/objects/manifest.json`（以實際檔案索引為準）。

## 動態與合成邊界

`combat-suite/result.json`、`combat-ui/result.json`包含全量前置與終點wire，CaptureWorld鎖及applied sequence封存；旁觀者先雙向可見再開始量測。`verify_combat.py`獨立解碼兩條連線、所有checkpoint、原HP比例/255隱藏/死亡/移除鏈及自身HP下降；不刪尾包、不跨連線世代拼接。SQL只作唯讀保存oracle。

`combat-regression.json`等回放含明示合成的截短、未知ID、舊世代、死屍重複、缺素材、傳送gate延遲與取消負例；不可替代真實戰鬥證明。`combat-visibility-plan.json`僅為靜態原素材遮擋規劃，實際UI需另由PNG及runtime快照證明。

## 初始fixture與保留失敗

原狼人template45024：gfx1110、Lv9、HP50、AC4、STR15、attack840ms、move640ms。唯一spawn fixture id400826在map0 (32598,32948)，count1、heading6、respawn60、movement_distance2。未更改原AI或戰鬥規則。狼人在原兩格移動範圍外可能返回，已保留失敗capture。

第一次Lv10測試角色被原服擊敗；死亡wire HP0與原正常離線保存HP1不同，該角色1900120001此後未再選取、未SQL重置。主管允許一次新初始角色1900160001「整合王子乙」：Lv60/Exp416461882/HP700，HP依原CalcInitHp、CalcStat及CON16的合法範圍663..899；來源及SQL在fixture-revision-r2.json。沒有第三次加強角色。新角色登入/保存/回血均由原協議，前後保護舊角色資料。

自然回血測試前置正常走至(32588,32930)，只等原HP封包；500HP為測試安全前置，不是產品屬性或傷害規則。舊版等待滿700的有界失敗亦保留。所有失敗在evidence/history完整保存，包括ED255初期解析錯誤及遮擋不足的UI輪次。

## 未支援

自動連擊、追擊、遠程、魔法、裝備背包、掉落拾取、完整復活、多地圖與全外觀仍未完成。原Netty為wildcard bind加loopback peer准入，非loopback-only bind；guard拒絕探針會關閉該probe listener，因此只在獨立probe進行，未注入正常JAR。

## 長時UI與診斷修正

原900秒等待暴露同步累積日誌重寫及UI接收餓死。Web診斷改用EventJournal逐筆JSONL、AutoFlush及固定種類計數，無完整List累積；Application.Run結束及Form.Dispose之後才串流產生相容JSON陣列，保留最後Deactivate事件。12,000筆/197,154,890 bytes已獨立逐筆核對，3k/3k/6k成本約165/134/332ms，GC後記憶體增149KB。

UI context接收每8包或16ms處理slice，及每個receive buffer之間讓出事件迴圈；這是排程公平性，不是攻擊/移動節奏變更。固定fixtures/combat-initialization.json保存原seed/前12包、原capture SHA與世代/sequence，供明示合成loopback高頻測試；新版5006有序時間包、479計時器ticks、1212繪圖、maxgap336ms、取消6ms與整個UI/WebView退出已通過。首輪5534ms、退出時EventJournal過早關閉例外及測試wrapper自動終止的輪次都保留未接受記錄。

舊阻塞UI的正常取消無法處理，主管有限授權後精確SetTcpEntry首次回317即停止，未升權；原服重複登入保持拒絕socket，原KickTimeController關閉舊main（OS CloseWait證據），原loop排完並保存3876主/4358旁觀包完整失敗。它是復原證據，非成功戰鬥；原JAR/DB未重啟。

## R1→R2：有限測試走位修正

主管 R1 乾淨實跑的狼人由 (32599,32947) 走到 (32600,32947) 後，立即以原 CHARPACK 回到 (32598,32948)。`reference/combat/lure-distance/` 保存同一固定 LAB JAR 的 NpcMove、Point、L1Location class、對照 Java、javap 與各自 SHA。NpcMove 原移動廣播後，method bytecode offset362 呼叫 getLineDistance、373/374 比較 movement_distance，超過即於402呼叫 teleport；Point.getLineDistance 的 offset33 呼叫 Math.sqrt。此處是歐氏距離，不是 getTileLineDistance 的 Chebyshev 距離：(2,-1)距離√5>2，(2,0)距離2合法。所有 offset 都是 JVM method bytecode offset。

只改 CombatUiTests 測試前置與 CombatVisibilityPlan 靜態候選過濾；原產品來源、fixture、AI、地圖、HP、角色初始數值與原服設定不改。測試先正常走至 (32599,32948)，有界等待原狼人 home 並完成原640ms移動，再由實際可見像素重新點選。原回巢 Remove/CharPack 可能清除選取，必須重新選取同 ID，不能繞過產品 guard。先單擊，再沿同 Y 正常向東各走一格，每段分別核對主與旁觀收到的原狼人位置及旁觀看到的主角位置；最後正常走至 (32600,32947)，狼人仍為 (32600,32948)。MotionAt==0 是模型原動作重設，不代表所有已完成步行都會歸零；測試也接受距 MotionAt 已過原移動640ms。

R1 凍結、主管原失敗與有限交還的原件副本保存在 `evidence/freeze-r1-lure-handback/`。本次第一個失敗為原回巢後 TargetId=null 導致第一擊被正確拒絕，沒有送出攻擊；全量 raw 在 history。新 `verify_lure.py` 純離線核對三段雙連線原位置、實際 C_Move heading2、引怪期間沒有 Remove/CharPack 回位且平方距離≤4，以及後段屍體影格。`verify_combat.py` 同時核對新增旁觀快照；未受影響的197MB日誌、四組回放及既有回歸保留原來源，不冒充 R2 新跑。

本輪成功戰鬥所執行的測試版本使用較嚴格 MotionAt==0；最終來源只把停止條件擴充成零或已過640ms，並明確等待旁觀home。`evidence/combat-ui/build-provenance.json`、`test-driver-built.cs`、`test-driver-final.diff` 記錄此差異及assembly SHA。離線 verifier 必須證明原checkpoint已符合新條件，不能宣稱最終driver已由工作者重新實跑；依主管指示不為此再戰鬥消耗HP，最終driver由主管乾淨建置後重跑。產品 C# 完全相同。
