# r33 正式候選覆核計畫

目前 candidateReviewed=false；以下是待指定封存候選後的驗收門，不是候選已通過紀錄。

## 第一門：身份與 diff

主管須給 exact candidate root、seal、DLL 與 source pins；先核對 final r32 baseline SHA 0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7、release seal 46e9249224e768a1ac09fe99c08a9edf387833295022147aa046c206769fa507。逐檔差異須保留 r32 Inventory.cs owner/Slow haste fix。審查 Ground.cs/WorldEntry.cs 的 reset 呼叫是否置於真正 ApplyWorldPacket lifecycle，而不是只存在測試直接反射 reset。其他檔改動逐項核對範圍，不預准共享整合。

## 第二門：基線與正向 controls

| Family | 本輪證據 | 正式候選要求 |
|---|---|---|
| 原 140 | baseline-probe.json；61 baseline +24 future +55 observations | 不把55觀察改成全 pass；10 gaps 對應所有權修補應關閉 |
| 正常 pickup | extended pickup-positive 24 | Speed0/1 × meat/arrow × count1/3 ×3routes；精確 wire、一次事件、無預扣 |
| 手動 drop | 20 count/wire +8 eligibility +Slow gate +12 early replies | 同 C25/owned-ID/xy/count/900；無新 drop 能力 |
| public producers | 12 positives、48 admitted-busy、16 queued-owner | 每一 producer 正向可 admit；既有 move-turn/attack/magic/item/pickup/drop 互斥不靠 injected busy |
| r32 item | extended 14 + supplement4 crossguards | Slow exact haste 可用，unknown/Brave 拒絕，world/actor stale item callbacks 保護新 owner；ground 實際忙碌仍擋 item |
| legal pickup replies | 原12 +supplement12 | same-owner selection/count/S158/S87/S67/S255/S15/S24 先回仍可成功一次；false/error 保持語意 |

movement producer 使用 public Turn，共用 SendMovement/movementSending，避免地形可通性干擾；不能把它聲稱為完整移動或地形驗收。若正式候選實際觸及 move path，另補 Move 正向與同版地形前置條件。

## 第三門：真 wrapper owner 交錯

矩陣以 world 與 same-world actor 分開，Speed0/1、肉/箭、old pickup/drop、outcome success/false/error。既有 Extended.OwnerPreparation 的肉 24 例是準備入口；正式 review 擴充箭及 queued-before-admit，不能只跑舊矩陣後統計 pass。

順序必須：

1. 合法正向 fixture；舊 public request 實際進 sender。分為尚未 v()/admit 以及已 admit 後 hold 兩類。
2. world 使用 ApplyWorldPacket(S206), ApplyWorldPacket(S87 own), valid ground S87 + select；actor 使用 matching Status ID 明示注入後 ApplyWorldPacket(S87 new actor)，world 不變。記錄 session/world/actor 前後 identity。
3. 先核 Ready、GroundAvailable、selected record/count、HP、Speed/Brave，及 groundSending false、nextGroundAt=0、lease 已更新、pendingPickup 清除。不得等待900ms來掩蓋 reset 失敗。
4. 新 public request 必須在舊 callback 前進入 sender 並 admit；新 sender hold。補 pickup→drop / drop→pickup 共用 owner 交叉各一組，避免只證明同一 method。
5. 拍攝 snapshot：State、Detail、Revision、generation、world、actor、ground lease/busy/deadline、pending、GroundNotice、ItemNotice、ground/item event counts；再依序釋放舊 success、false 或 error。
6. 舊回傳 false、無舊事件，snapshot 必須完全不變；finally 不能清除新 busy。釋放新 sender 後應成功一次、只發一筆新 world event、正常解除其 own busy。仍待 S158 的新 pending 不可被舊 completion 清掉。

若第3或4步失敗，記實際阻礙與 candidate failure，不得把之後 unchanged 當保護通過。本輪 r32 的24例均在第4步受阻，因此 pass=null/candidatePending=true。舊140以實際等待900ms排除 cooldown後仍 blocked 的24筆觀察亦保留。

Session Cancel 及 lease synthetic takeover 另列：session cancel 後的清理狀態不可被舊 callback 重新 Fault/notice/event 修改；若還原 synthetic new session fixture，須明示不是 socket reconnect。lease-only mutation 用來隔離 owner predicate，不冒稱正常 public route。

## 第四門：final admission 狹義阻擋

重用原 Queue16，並保留 drop-final13。其中 actor/lease/magicSending/nextMagicAt 分別單因子隔離；所有其他 admission 條件保持合法，actor 注入時配對 Status 保持 Ready，以避免 NotReady 掩蓋缺失。magicSending=true 與 nextMagicAt=now+60000 分開，確實直到 v()執行時仍有效，時鐘為 Environment.TickCount64。

另檢 current pointer callback=false、selection即使同ID但revision變更、ground/owned item record替換、count/remove、input、coordinates/correction、session/world、death/HP、action lock/trade 與 movement/combat/item busy/cooldown 保留。正常 control 同時成功，避免直接使用 aggregate GroundBusy/MovementBusy 把自己的 request 拒絕。先驗 public producer 互斥，再看 injected-state final fence；兩種分類分開。

## 第五門：合法早到回包及副作用

所有 positive admitted operations 分別回傳 success/false/error；admit後再送正常 S158、ground S87 count change、same-actor S87、S67/S255、S15/S24、drop S57 removal、selection change，確認 owner 未變但 admission snapshot 已變。success 必須一次 GroundSent，不可要求 snapshot仍相同；current error 必須仍 Faulted。不得把 post-admit count/selection变化當 stale owner。

在 sender 未完成前核對 wire count、地面/背包無本地預扣、heading只由合格 S158處理、無本地 action15 animation。3000ms outcome timeout 與900ms guard維持；不得用手調時鐘或跳過 pending mask。

## 第六門：交付分類

正式 review 結果包含 exact candidate identity、diff、actual execution/fixture scope、baseline controls、future contract、observations 與未達 gate。原10gaps/55obs不能從報表消失。此準備未授權合入、部署、live/native驗收、DB或服務操作。最終是否接受由主管依正式 review 與整合證據決定。
