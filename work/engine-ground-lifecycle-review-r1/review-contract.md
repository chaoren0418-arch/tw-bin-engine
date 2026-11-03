# ENGINE-GROUND-LIFECYCLE-REVIEW-PREP-R1：獨立覆核契約

此為準備封存，candidateReviewed=false。未讀未封存的 engine-ground-lifecycle-r1，未審查任何 r33 候選。預期由主管任務、已接受 r32 原碼與既有封存來源推導，不能依未來作者測試結果改寫成通過。

## 基準與範圍

基準為正式接受 r32；實際執行使用 byte-identical accepted stage DLL。baseline-binding.json 保留原 stage baseline、final baseline、等價綁定與四身份。原 baseline.json 未改。stage/final/r31 各 177 份來源均核 SHA；r31→r32 只有 Inventory.cs、WorldEntry.cs 變更，其餘 175 份一致。未重驗全部 release 14,342 檔。

本增量限 Speed0/1、Brave0、既有肉/箭單次拾取之 session/world/actor/lease 生命週期。SendGround 共用修補自然涵蓋的手動 drop 需小範圍回歸，不能藉此增加新 drop 支援。預期主要涉及 Ground.cs、WorldEntry.cs；實際最小 diff 須等候指定封存候選，不能預先認定其他變更安全。

## 必須成立的所有權

1. 每次 SendGround 的 ownership 至少識別 session generation、WorldEpoch、Actor.ObjectId 與 ground lease。排隊 final admission、success/false/error completion、finally 均不能以只有 session 相同取代完整 owner 判斷。
2. 真正 LoginConnection.ApplyWorldPacket 引發 world 或 actor identity 切換時，釋放舊 groundSending、nextGroundAt 與舊 pendingPickup 所有權；更新 lease。正常同 actor S87、S67、S255、數量與 selection 回報不是 owner 更換。r32 ResetItemSend 行為須保留。
3. 新世界完成 S206→S87、Ready=true、合法地面/背包已恢復後，新 public API 必須在舊 sender 尚未完成時即能 admit。測試不能用 NotReady、HP0、空選取、失效封包或仍未恢復世界取代真正新 owner。
4. 新 owner 已 admit 且 sender 被 hold 時，舊 success / false / error / finally 必須不回報成功、不發 GroundSent、不清新 busy、不改新 deadline/lease、不改新 pendingPickup、不 Cancel/Fault 新 session、不改 GroundNotice、Detail、狀態與事件計數。以 old callback 前後完整 snapshot 比較，並確認新請求最終成功且只發一次新事件。
5. world route 使用實際 wrapper 解碼 S206/S87。same-world actor route 的 matching Status.ObjectId 注入是明示的 synthetic prerequisite；原 S8 不允許現有 status identity 中途換 ID。兩種證據不能互相冒稱可由相同正常 wire 流程到達。
6. baseline 新 owner 無法 admit 的案例只能記 candidate pending。本輪 24 個 ready 案例全部被舊 ground ownership 擋住；不能把「沒有新 owner 可破壞」算 stale-owner 保護成功。原 140 的 24 個 wrapper lifecycle 觀察仍保持觀察分類。

## Admission 與 completion 分開

Final admission 須重新查 actor/lease，及 magicSending、nextMagicAt；保留既有 session/world/input/speed revision/selection/ground or inventory record/count/correction/coordinates/death/HP/Ready/action-lock/trade/other-action busy/cooldown/pointer-current 檢查。不可用會包含本請求自身 groundSending 的 GroundBusy 或 MovementBusy 直接否決自己的 final admission；須證明正常 controls 仍可送出。

公開 API 互斥與注入狀態各自立案。既有 ground public API 在進入 sender 前即建立 groundSending，因此另一個合法 public magic/item/movement/attack 在初始 gate 會被擋；這不能證明 final predicate 已檢查 magicSending/nextMagicAt。獨立注入用來測 final fence，明示不是公開可達並行軌跡。本輪實際 public producer controls 全成立，仍保留四個 isolated gate gaps。

Completion 只查 owner，不複製 admission snapshot。已 admit 後，合法 S158/action15、同 actor S87、S67/S255 speed、S15/S24/S57 數量/移除、selection 變化先於 sender completion，不得讓正常成功變 false 或漏事件。success 正常回傳 true、GroundSent 一次；current false 不發事件並正常釋放 busy；current error 仍走既有 Cancel/Faulted，不得為抑制舊 error 而吞掉真正本請求錯誤。舊 callback 無權回寫 notice；本請求合理回包可更新 notice。

## 必須保留的規則

- C112 pickup / C25 drop，16-byte envelope；pickup 用 ground xy/id/count，drop 用 actor xy / owned inventory ID / count。不得預扣地面或背包，不本地製造拾取動畫。
- 正常 Speed0/1 Brave0，肉 gfx36、箭 gfx66，部分與全量，PickupSelected / PointerPickup / PlayerPointerController.PickupAsync 三路均成功。無自動追物、重送或 auto pickup。
- 距離 3 格可拾、4 格拒絕；count0/超庫存/超 int 拒絕，既有 exact ground envelope、meat/arrow drop eligibility 不擴張。
- 900ms click guard、3000ms pending timeout fence 保留。3 秒未確認仍 pending/不偽造成功；S87 數量不能冒充 S158/action15 ack。
- Slow pickup/drop gate、pointer focus/walking/visibility/stamp/current guards 不擴張。unknown Speed3 或 Brave1 在 direct/low 的 r31/r32 既有放行只是觀察，不是本增量新增允許政策；pointer 既有拒絕仍保留。
- r32 exact haste potion 的 Slow exception、unknown/Brave 拒絕、item owner、合法 early replies、其他動作 busy/cooldown 與所有道具範圍均保留。
- 不新增動畫、assets、NPC、Slow/unknown/Brave 支援或伺服器 delay 語意。

## 封存來源證據邊界

只 pin 已接受的 pickup source seal 8b1110e77614fbe8f9cb9b68718348c71f75d04110451cbca3c90699114125e1 與舊 review seal 29fb10cb6919a9ea07c13185af3ffc0eab2b8be4944c18093cbf84cb00326095 的選定 material，且逐項核對其 manifest。既有結論是：JAR S158/action15 只有 actor，沒有 item count/request correlation；900ms 是引擎本地政策；原 BIN 分支與 action15 default setter/frame advance 並未構成可用完整 pickup animation/timing；42 metadata profiles 無可用完整 pickup frame sequence。該來源 qualification 未通過，不能用此次生命週期修補升格。

本輪未重跑 native chunks、JAR classes、metadata、3602 suite、正式入口、GUI、socket、DB/SQL/服務；未執行原 BIN 或 JAR handler。此 probe 是真 r32 DLL 加 synthetic ready fixture、memory sender 與真 packet wrapper，排除完整接收 socket/pump/GUI/native 同版驗收。S158 沒有 request ID，不能聲稱能識別同 actor 的所有延遲舊 packet；本契約處理 transport callback 與 pending owner，沒有發明 wire correlation。
