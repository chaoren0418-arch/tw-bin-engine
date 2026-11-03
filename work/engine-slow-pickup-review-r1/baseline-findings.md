# r31 拾取基線發現

這是來源審查準備，非產品候選接受/拒絕。全部個案、wire與實際單調時鐘見baseline-probe.json。

**正常公開拾取路由可重現同session換世界舊回呼問題。** `wrapper-world-lifecycle-{0|1}-{36|66}-{success|false|error}`共12案使用公開PickupSelected及真ApplyWorldPacket S206/S87。新世界已ready、GroundAvailable=true、舊pending已清、真900ms已過；沒有反射改groundLease/groundSending。舊sender未完成時，新拾取全部無法admit，因舊groundSending仍true。這是in-memory延後sender模型，並非live socket時序實測。

- 舊success四案：GroundSent仍帶舊worldEpoch；deadline未延長（ground與item不同），finally清掉舊busy。等舊callback完成後，新操作可送且新busy/事件正常。
- 舊false四案：不發舊事件，但仍必須等舊callback釋放busy，新操作之後才可送。
- 舊exception四案：Cancel/Faulted作用到新世界，world再次變化，新操作無法送。
- **不能聲稱已在公開路由重現「舊finally清掉已admit新操作busy」**：r31在舊sender未完成前就阻擋新admit。本輪記錄了這個可達性限制；人工lease新owner另外分類，未冒充正常時序。

`wrapper-actor-lifecycle-*`另12案走真S87 wrapper，但先人工更新Status.ObjectId，屬same-world actor防禦性模型；原S8拒絕直接改已知Status身份，不能宣稱此前置可由普通伺服器包完成。Ready=true但舊pendingPickup仍留著；即使舊success/false返回，新拾取仍因pending fence拒絕。舊exception亦Faulted。pendingPickup目前不含actor ID且S87 actor替換不清它，應在未來所有權契約中考量，不能只加completion if。

合法回包12案 `legal-replies-before-completion-*` 全部符合保全條件：正常Speed0/1、肉/箭，admit後先經wrapper收S87 ground更新、同actor S87、S67、S158、S15及S24，再完成sender。成功保留一次GroundSent；false無成功事件；當前error仍Faulted。這些sampling/selection/count/revision變動不能被未來completion guard當成過期歸屬。

人工最後gate檢查另有4項差異：actor身份（Status同改維持Ready）、groundLease、magicSending、nextMagicAt未被r31最後predicate擋住。session/world/input/correction/position/selection/record/remove/death/speed同值與往返等負向控制均拒絕。這4項中reflection actor/lease與busy的正常API可達性未證，不稱live漏洞。

舊成功/error對world/actor/lease的6項隔離契約未滿足；session Cancel對照通過。加上前述4項，10個false row都是「擬議安全契約與舊基底差異」，不是本輪候選失敗數；公開world生命週期的12項另外以觀察保存，沒有重複灌入failure count。

基線功能：肉/箭精確envelope負向、count與距離、初始10種busy均符合舊邊界。C112不預扣地面/背包、不建立pickup動畫；action15只更新計數及本人pending朝向，地面數量仍由S87、背包由S15/S24處理。未知actor action15不消耗本人pending，重複action15不預扣物品。3100ms未回action15時顯示未確認、保留pending並拒絕再拾取；這是現有明示政策，不當成功或自動retry。

路由30筆觀察：Speed0/1+Brave0三入口都可送；Speed2全部拒絕；unknown Speed3或Brave1在direct/低階PointerPickup可送，但pointer controller拒絕。此差異需另決保守政策，本輪不改Slow或任何ground入口。

42個既有profile全缺pickup序列，原native selector/frame schedule仍未知。因此目前不能接受Slow pickup動畫或輸入parity；正常pickup生命週期問題則可提出獨立的最小修正候選，不依賴新動畫資產。是否派實作由主管另定。
