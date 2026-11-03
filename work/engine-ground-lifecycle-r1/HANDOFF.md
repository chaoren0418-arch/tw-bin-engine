# ENGINE-GROUND-LIFECYCLE-IMPL-R1 — r33 正常拾取生命週期候選

候選完成並封存，停止寫入，等待獨立review；不是正式r33 release、shared merge或P24完成。僅改Ground.cs及WorldEntry.cs，其他175來源SHA全不變。

## 修復與保全

最後准入補session/world/actor ID/lease、appearance與跨動作／deadline檢查。完成success/error/finally只核owner，容許合法S158、S87、S67/S255、inventory、選取與外觀回包先到。真ApplyWorldPacket在world/actor改變保留ResetItemSend並追加ResetGroundSend，同時清舊busy/deadline/pending，讓新owner可准入；舊回呼不發GroundSent、不Fault新世界、不清新busy。

Speed2拾取仍禁止；unknown/Brave舊入口政策未擴大。肉箭envelope、距離3、數量、C112/C25、900ms/3000ms、action15 heading與權威數量不變，沒有動畫/新資產。共用手動drop只有同一所有權防護；有限正常drop及早回包回歸通過。SOURCE-CONTRACT.md列完整條件與限制。

## 實證

- 原r32精確DLL baseline：36筆符合觀察預期，其中11缺陷重現、24合法early-reply controls、1無socket；不能稱36個安全契約通過。
- 候選：252+87=339次離線檢查全部通過，338個不同ID（兩個suite各有no-socket-events）；337功能斷言+2邊界檢查。baseline36、review140及歷史377/3602不累加。
- 真Environment.TickCount64，900/3000ms採實等；公開producer互斥與人工defensive注入分列。所有收包均走真ApplyWorldPacket。actor換置有人工matching Status前提。
- product/helper build零警告零錯誤，命令／exitCode／helper快照／逐row結果完整保存。無GUI/socket/native/JAR handler/DB/SQL/服務，未跑全3602或重掃14k素材。

候選DLL `8ed92b8be9e532198e0f5bc88e404b47a3601f0e98e0962a0e1820571090d0c2`；audio `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。candidate/build包含可供覆核的runtime；baseline-runtime保留原r32 runtime副本。沒有啟動正常產品。

## 基底與交付

原stage baseline SHA `015da2542257d3c124346dcdeb3f34528902b56e1913b5878f3bc995597093ff`保持不變；final r32 baseline `0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7`、binding `26cc55267f04896344333a6eda50d058d432aa4df1d2655875b6e9772eedff5e`、release seal `46e9249224e768a1ac09fe99c08a9edf387833295022147aa046c206769fa507`已核對。177來源與四身份實際逐項hash相等，不因純等價綁定重建候選。主管14342release稽核只引用，未冒充本worker重掃。

source-before.json為固定stage177檔；source-manifest.json為候選完整177檔（legacy schema engine-r29-source-pins-v1，revision=r33）；changed-files.json、changes.diff、delta保存最小兩檔差異。input-pins與final-base-binding-pins固定來源；preservation/result保存核對與候選狀態；delivery-manifest封存產物，排除自身與私有cache/obj/bin/tmp。

獨立r31review seal `29fb10cb6919a9ea07c13185af3ffc0eab2b8be4944c18093cbf84cb00326095`已pin且讀HANDOFF、findings、contract。原pickup source-only封存保持不變，qualified=false仍有效。這次修復不宣稱原生拾取輸入、佇列或完整utility timing資格。

## 重跑／後續

VALIDATION-NOTES.md提供在獲授權fresh scratch重跑baseline/candidate/extra的精確命令。run.py只使用offline vendor、私有cache/TEMP與DOTNET_GENERATE_ASPNET_CERTIFICATE=false，tag不可覆寫。不可原地重跑封存件；verify_delivery.py可唯讀核對本seal及固定來源。

真Goal於派工開始實讀blocked，未新建／恢復／修改，沒有新chat/subagent。共享engine/releases/assets/主管/其他work保持唯讀。由主管派獨立review及唯一整合者後續合入，本worker於封存後停寫。
