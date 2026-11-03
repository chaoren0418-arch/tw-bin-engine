# ENGINE-HASTE-ACTIONS-REVIEW-R1：準備完成，待正式候選

已封存獨立Speed1契約、r30真DLL缺口證據與可重跑helper。狀態為 **preparation-ready-candidate-review-pending**；candidateReviewed=false、candidateAccepted=false、reviewComplete=false。未讀取實作者未封存內容，沒有候選接受或拒絕判決。

## 固定基底

- baseline SHA：`1329700b9fabf59e7406d62790ea8a32758ee9771963d0dcd49c7cde85708a66`
- r30 seal SHA：`199ce6fe81130138e9fedf98e688928ce44b0a45c2aebd1b92fdd9519b8225dc`
- 實際載入DLL SHA：`1abc1eed02917d14fd775be60920fe74efb9b102bc53eea5874fc0128e5247ef`
- audio SHA：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`

共享177個source/audio/web pins、259個本輪input pins及上一輪37個審查artifacts均核對未變。engine/assets仍唯讀指向releases/r28/assets。此輪核對r30 seal本文與必要來源，不重複宣稱新驗14296個release檔。

## 實際執行

建置0 warnings／0 errors；console helper執行 **1553 assertions：1057通過、496呈現r30已知Speed1缺口**。r30-probe.json保存逐項證據，commands.json與stdout/stderr保存argv及exit code；gap-analysis.json分類失敗及最小例子。

原976個assertion名稱全保留：156個Speed1預期依原來源明列替換，其餘820個斷言全部通過。另新增577項；不能稱為「原976原樣全過」。25個新的真送出精確時間案例全部重現未加速缺口，動作仍確實送包。126個聚合frame assertions包含20112個八方向幀邊界採樣，不把採樣再加算為assertion總數。

來源推導例子（ms）：

| 動作 | Speed1動畫 | server0.755 | 相對marker前綴 | 含reuse／margin的guard |
|---|---:|---:|---:|---:|
| gfx0／weapon20／action1 | 931 | 906 | 590 | 963 |
| gfx0／weapon0／action18 | 1024 | 996 | 682 | 1124 |
| gfx0／weapon0／action19 | 1149 | 1117 | 683 | 1249 |

r30弓攻擊實際guard仍1232，而新契約963；技能4仍1420，而新契約1124；技能1仍1580，而新契約1249。保留的較小「server下限」斷言會被r30較慢行為滿足，所以新增exact gate與−1／0／+1消費端案例，避免錯把慢速通過當加速完成。

時鐘修正：初版對item／ground也使用合成未來時刻，但兩者最後准入直接讀Environment.TickCount64，不能據此判缺陷。最終改成兩個注入clock均回傳同一真實時鐘，實際等到來源guard並記錄before／at-or-after採樣；保留實際busy／最後准入，不改產品。移動／攻擊／施法仍使用同一可控clock驗−1／0／+1。初版1563／506結果已由本最終1553／496取代，沒有將fixture失敗保留為產品缺陷。

已執行九種0/1/2轉換、同值與1→0→1／1→2→1、高水位、各消費端、Speed1舊成功／失敗callback與lease、乾淨renderer第一次晚到ack、remote未知→恢復，以及原Speed2取消／F1／拒絕規則。候選正式階段還要完成其他busy生產端、Speed1釋放前取消／目標鎖定及剩餘最後准入細項；race-vectors.json分開已執行與待驗，不將計畫計入通過。

## 原來源與策略

固定BIN原270組oracle（Speed1有90）與JAR108次純函式結果沿用，只重核必要身份、15個原byte chunks、class entry及算術。**新native執行0、新JAR執行0**。action1／18／19共同Speed1分支已具來源資格；Brave0不能代表其他buff中性，既有oracle的metadata/callee/actor替身限制完整保留。

126組action全部有逐幀取整與整段乘0.75的差異，126組marker prefix也有差異，44組含零時間幀。採sum((3*frameMs)>>2)，不強制至少1ms；server另取trunc(base*0.755)，reuse與32／100ms本機餘裕不縮放。Speed0／2保留r30策略，flight30／60及獨立特效不變。主管1008方向靜態補充已pin，不冒充native scheduler驗收。

完整契約見CONTRACT.md。原native絕對frame初始化／+1 scheduler／release起點仍未認證；本輪沿用r30 marker索引及At原點，只推導相對Speed1相位。不重開PAK或完整原碼研究。

## 封存、重現與下一步

本work完成後停寫，主管已續派精確候選資訊；本準備尚未開啟候選，完成此封存後即在新candidate-review-r1子目錄正式覆核，最終engine-independent-review-v1必須明確accepted/rejected與unresolvedFindings。不得將本準備result作接受收據，也不輪詢未封存候選。

本輪只寫指定work；沒有正常產品、GUI、socket、原遊戲、服務或DB操作。get_goal=null，未新建／恢復／修改Goal。

`python -B -X utf8 verify.py`為只讀封存檢查。不可在本封存目錄原地重跑prepare.py、run.py或seal-preparation.py。需要重現時，另獲准scratch複製harness、run.py、action-metadata-vectors.json及haste-metadata-vectors.json，修改runner的B/R與DLL路徑，維持helper輸出在scratch/helper/r30，並建立scratch/assets唯讀junction至固定素材；再執行 `python -B -X utf8 <scratch>/run.py`。prepare.py是初始複製與適配歷史，會重寫Program/Extra，不是最終helper重現入口。禁止改原候選或共享source以讓測試通過。
