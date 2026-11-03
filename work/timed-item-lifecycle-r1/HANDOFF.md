# timed-item-lifecycle-r1：10326 限時道具生命週期診斷

已完成本次唯讀診斷，未修復資料或重啟服務。10326 已由歷史租房收據及資料庫快照識別為角色1900249001的旅館房間鑰匙49290，正常租房成本100金幣。**主表物件在搬移前已消失；目前限時孤兒及其造成拒絕開服均已證實，但此次具體刪除路徑仍未知。**

## 邊界與身分

- 唯一主管：01a17e6a-9270-7cc2-a338-0120ed68d0ec。唯一寫入位置：`D:\lineage開發\TW1608262200研究\work\timed-item-lifecycle-r1`。
- 基準為封存r28；實讀現用 `environments/p24/server/Server_Game.jar` SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。
- 已讀專案根 `TW1608262200研究/AGENTS.md` 與派工；上一層 `D:\lineage開發\AGENTS.md` 不存在。歷史證據經project-manifest.path_aliases／resolve-path規則解析，沒有重建舊根或執行舊腳本。
- 本工作者真正get_goal回傳舊P24 Goal為 **blocked**，不是paused；主管煙霧收據記載其自己的Goal為paused。兩者分開記錄，未重建、resume或complete任何Goal。
- 無DB連線、SQL執行、socket、UI、遊戲／服務啟停、修復、刪列、補發、繞過檢查、子代理或新聊天。只執行離線Python與獲授權的javap；Python均使用-B。未讀取執行中DB資料檔本文；MyISAM與冷備份結論引用主管已保存收據。

## 時間線能證明的事

以下採Asia/Taipei呈現；原始UTC、各檔雜湊及限制在 `historical-evidence.json`。

|時間|證據|
|---|---|
|10/28 03:33:35|正常r28租房後快照已有10326；review記錄金幣33500→33400、鑰匙1顆、房間進出|
|10/28 05:31:39|歷史受控DB快照仍有10326／49290／owner1900249001|
|10/28 05:33:31|目前保留限時列的到期時間；此時間本身不識別刪除原因|
|10/28 05:33:40|重新選角後第一個InWorld快照已無鑰匙，尚非SQL刪除證明|
|10/28 05:50:55|歷史受控DB快照確認主表已無10326，早於搬移|
|10/29 啟動前／失敗後|主表皆無10326；限時表仍有該列；啟動在CharItemsTimeTable.load拒絕服務|

05:31:18的Offline空背包只是客戶端狀態清空，不列為刪除證據。主管preflight的904檔與冷備份一致，包含character_items/time的MYD、MYI四檔；本次另核對這四筆與時間線引用相符。這支持搬移保留當時位元組，不是對歷史刪除原因的證明。也不能把主表消失的時間，當作附表孤兒形成時間的完整物件級稽核紀錄。

## 實際JAR中的生命週期

完整12列因果表見 `CAUSAL-TABLE.md`／`causal-table.json`，32個方法本文、來源行號對照與方法內bytecode offset見 `method-evidence.json`。

1. **建立：** 歷史瑟琳娜room配置id1237，40308×100換49290×1。實際JAR `NpcMakingExecutor.storeCraftResult` 在offset135/146建立不可堆疊物件，159呼叫storeItem。`L1PcInventory.lambda$storeItem$16`從既有剩餘秒數、時間表h×3600或MaxUseTime取得期限，同時設remaining與_time。`CharItemsTable.storeTimedItem`先寫主表(offset81)，再確認期限(offset96)，後者採owner/template匹配的INSERT SELECT並讀回；不宣稱跨表原子性。
2. **正常進旅館：** `NpcTeleportExecutor.selectByRequiredItem`是持有數量檢查，歷史enter費用為0；不是消耗鑰匙。不能把正常進出本身當作此次消失原因。
3. **登入：** 已核對 `C_LoginToServer.items → CharacterTable.restoreInventory → L1PcInventory.loadItems` 及 `S_InvList`。這些方法沒有直接以Timestamp.before判斷到期後remove/delete的分支。登入會啟動剩餘時間計時入口；但 `L1ItemInstance.startEquipmentTimerInternal` offset1–12在_time非null時立即返回，**具有絕對期限的鑰匙不會因此啟動相對倒數**。此負證據限於已核對鏈，不是全服所有登入擴充的窮舉。
4. **絕對到期：** `ServerItemUserTimer`每60000ms掃描，以deadline.before(now)判斷。線上重新檢查World、owner、inventory與期限後removeItem；離線走deleteExpiredOfflineItem。這條線上到期分支不自行將remainingTime歸零。到期與消失區間相容，但缺少當次執行緒與物件級刪除證據。
5. **一般整件移除：** 實際 `L1PcInventory.lambda$deleteItem$31` 三個分支均以remainingTime>0才清限時表（讀值offset129/176/399，清理142/189/412），不檢查_time。若已有time row而remaining≤0，主列刪除可能留下附表；但沒有10326歷史remaining_time值，不能指定它就是此例。正常時間表h>0的新建鑰匙通常會先取得正數remaining，且上述絕對計時器不會將其歸零。
6. **分開清理的失敗窗口：** 離線 `CharItemsTable.deleteItem`先刪主列(offset44)，再無條件清限時表(103)。`CharItemsTimeTable.delete`會捕捉Exception、只記錄error而返回(offset1呼叫、7進catch、16記錄、21返回)。中斷或附表SQL失敗可能留下孤兒；memory-first早期清理失敗或跳過後，autosave只處理主表pending delete，不會補清附表。這些是可證實的條件性風險，尚無本次失敗證據；SQL後close例外也不必然代表DELETE未生效。
7. **再啟動：** `GameServer.initialize`先載主表(offset1114)，再載限時表(1166)，一般Server timer到1446才啟動。限時load讀全部列，沒有「已過期就刪除」分支；offset146找World物件、151–218驗證，失敗後828–839包成IllegalStateException拒絕開服。當次before-server已無主列，因此這次載入不是已證實的主列刪除來源。

## 來源與JAR一致程度

20個class直接從指定SHA JAR取樣雜湊，使用javap -c -p -l取得實際方法；沒有執行Server main或任何遊戲類別。52項外部來源pins已核對，32個關鍵方法的指令條件檢查通過。

關鍵刪除條件、例外處理、計時分流與載入拒絕的來源邏輯與實際JAR吻合，但**不是整份來源碼等同實際JAR**。已找到明確差異：現存NpcMakingExecutor以CraftNotice承載通知，實際JAR的storeCraftResult是List<String>並加入getLogName；建立／store呼叫仍一致。JAR LineNumberTable是編譯時行號，並不自動證明現在檔案每行相同。報告以實際bytecode為準，無可重現編譯或整類別等價宣稱。所有offset都是各Java方法內offset，不是JAR File Offset／RVA／VA。

## 剩餘證據與主管最小查詢

`minimal-select.sql`由本工作者提出，**主管已審閱並執行其中8個SELECT，本工作者未連線或執行**。`lifecycle-select-results.json`的SQL SHA與交付檔完全相符；本地摘要在 `supervisor-select-evidence.json`。新收據補齊以下現態：兩表ENGINE=MyISAM，主表PRIMARY(id)、限時表PRIMARY(itemr_obj_id)；10326孤兒仍存在；該角色目前沒有49290或49900；兩模板期限h=2；製作1237 room與1238 hall仍為100金幣換一把鑰匙；兩表trigger metadata為0。`timed-counts.json`全表total=1、distinct=1、orphan=1、null-time=0、nonpositive-id=0。

時區查詢的session_time_zone為SYSTEM，system_time_zone文字亂碼，不能宣稱已解出時區名稱。server_now與收據UTC差約+08，與本報告Asia/Taipei呈現相容；不能把這個當作歷史Java／DB時區完全相同的額外證明。目前h=2支持兩小時期限配置，仍不恢復10326歷史remaining_time或刪除呼叫者。此有限診斷目前沒有尚待主管執行的SELECT。

若後續要將具體原因提升為已證實，仍需要保留下來的物件級刪除／SQL錯誤／日誌或當時remaining_time與分支證據。現有資料不足以在「條件漏清、附表失敗、分開寫入被中斷或其他未留存操作」中唯一選定。兩表已由當前SELECT確認MyISAM，任何後續方案都不可假設rollback可復原；本交付不提出或執行修復步驟。

## 重跑與驗收

在本目錄依序執行（只寫本目錄）：

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\collect.py
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\analyze.py
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\report.py
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 .\verify_seal.py
```

collect使用javap離線讀取JAR，已存在且hash／命令相符的類別不重跑。交付包含result.json、class-evidence.json、method-evidence.json、source-pins.json、historical-evidence.json、因果表、查詢建議、static-verification.json、verification.json與artifact-manifest.json。驗收為靜態因果診斷與證據完整性，不是重現故障、資料修復或搬移後煙霧通過。封存後停寫，等待主管新派工。
