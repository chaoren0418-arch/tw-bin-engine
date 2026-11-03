# timed-item-repair-review-r1 交接

**審查完成；不批准任何原資料修復或再次開服。** 現在唯一有充分證據的動作是保存原狀、維持異常阻擋，進入另行核准的最小隔離重現。保留原活動列、原JAR、完整載入且不補物件的前提下，10326仍必然觸發守門；沒有可誠實稱為安全修復的刪列或expired旁路。

## 邊界與Goal

唯一寫入：`D:/lineage開發/TW1608262200研究/work/timed-item-repair-review-r1`。已讀專案根AGENTS.md；D:/lineage開發/AGENTS.md不存在，未虛構其內容。本worker真正get_goal回null，依根規則建立本次窄範圍Goal；主管收據中的paused是另一Goal，本worker沒有resume、重建或修改主管Goal／平台存檔。

基準封存releases/r28；實讀 environments/p24/server/Server_Game.jar SHA256仍為 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。無DB／socket／UI／服務啟停、沒有Java執行／編譯／新javap、沒有共享區寫入／環境複製／子代理／聊天派工。Python全用-B；讀取JAR內一個class僅計算hash，沒有落地候選。另一worker的完整生命週期調查不由本worker重做。

## 獨立核對結果

- `server.stdout.log`（SHA256 `f0bb99dd4ccb95ee2609213974146a370409c5811c756b27dd5f4e7efb7aaba0`）含例外原文 `java.lang.IllegalStateException: character_items_time 未完整載入，拒絕開服；保留原始資料供核對`，及原因 `Caused by: java.sql.SQLException: Timed item missing/invalid; preserve original row itemObjId=10326`。以原文與hash定位；主管預審後用Select-String核對分別為實體第238、243行，取代先前錯誤行號。JMX warning不是這個主失敗原因。
- 既存javap `load()` 方法內bytecode offsets138–220檢查有效item參照，221–257檢查重複；288–407再驗身份／舊時間引用；408–638發布與後驗；641–787條件回復並保留suppressed錯誤；788–790才設loaded=true。這些是**JVM方法內offset，不是BIN VA/RVA/File Offset**。現存Java第51–58、63–92行與此結構相符，不宣稱整份來源和JAR完全一致。
- 10326在初始驗證就失敗，該次load尚未進入timestamp發布；不能推論整個GameServer啟動沒有其他副作用。前後9角色／58道具列／14技能列獨立比較，唯一差異是1900249001 online1→0；不把這份有界帳本當成全DB不變證明。
- 904檔是主管preflight記錄的啟動前冷備份比對；本worker核對其904個唯一manifest條目與4個MyISAM資料／索引pin的一致性，**未重hash正在運行DB，也未重新解壓整備份**。位元一致不代表邏輯健康。
- 追加 `timed-counts.json` 的2026-10-29 02:56:29+08聚合為total1/distinct1/orphans1/nullTimes0/nonpositiveIds0，補足原LIMIT100清單的範圍限制；`timed-engines.json` 以SQL證實兩表MyISAM，rowsEstimate不充當精確count。DB UUID／port與原收據一致。均為主管既存只讀收據，本worker未查DB。
- `key10326-timeline.json` 及其中3份原始收據已另核hash：2026-10-28 05:31:39+08的DB仍有10326、template49290、owner1900249001；05:33:40.861的第一個InWorld快照已無，05:50:55 DB也無。預定05:33:31到期在區間內，**只能說相容，不能確證刪除原因**。Offline/Connecting空背包不是刪除證據。可確認主列消失早於搬移，不能據此斷定孤兒由哪個程式路徑造成。
- 當前owner=null是LEFT JOIN主表缺列造成；限時表只有itemr_obj_id／usertime，並非保存了一個「原owner=null」的事件。歷史owner已知仍不足以證明終止原因、正確歸屬及ID從未重用。

## 交付與下一步

1. `OPTIONS.md`：A保存原狀、B修真實漏載、C更早一致檢查點回復、D正式終止事件模型、E上游生命週期修正。逐項列保全、證據、風險和回復門檻；B/C/D目前均不具實施前提，E不會修好現有孤兒。
2. `ISOLATED-TEST-DESIGN.md`：L0檔案核對／合成政策；L1固定class隔離loader16類案例；L2 MyISAM故障點與重跑／結果未知／補償失敗；L3父對話批准後的回復及正常煙霧。L1/L2/L3均未執行。
3. `evidence-review.json`、`source-pins.json`：19個來源pin，8個主管原pin一致，3份時間線原始收據一致，JAR及classhash、有界帳本差異、聚合與engine證據。
4. `review_gate_checks.py`／`review-gate-results.json`：18項新建合成審查政策檢查通過；非原服、非修復候選、不證明MyISAM原子性或JAR重現。首次因腳本名遮蔽標準庫的工具錯誤已修正並重跑，詳設計文件。

**最小後續建議：** 主管彙合生命週期worker的有界來源證據，再向父對話提出是否授權L1／L2及哪個具體修復選項；在此之前不刪／移孤兒列、不補鑰匙、不改期限、不跳過載入檢查，也不再次整服嘗試。若採任何原資料變更，必先完成可審精確差異、獨立保全、MyISAM回復演練及父對話決策。

驗證命令（本目錄）：`python -B -X utf8 verify.py`。需重新生成證據時才執行collect_evidence.py／review_gate_checks.py；封存後勿覆寫。正常DB與遊戲煙霧仍未通過；本輪Goal完成只指審查交付。完成即停寫。
