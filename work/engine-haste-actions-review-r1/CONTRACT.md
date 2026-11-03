# ENGINE-HASTE-ACTIONS-REVIEW-R1：獨立 Speed1 契約

此契約在未讀取實作者未封存程式或預期的準備階段建立。基底是主管指定且已接受離線整合的 r30；本輪僅開發既有角色／武器、Speed1／Brave0 攻擊與技能1／3／4／8。Speed0／2、未知Speed／非零Brave的拒絕及原緩速非戰鬥規則須保全。本準備未開啟候選內容，沒有候選接受或拒絕判決。

## 身份與來源強度

- 基底baseline SHA：`1329700b9fabf59e7406d62790ea8a32758ee9771963d0dcd49c7cde85708a66`；共享 source/audio/web 177 pins 已逐一核對。
- r30 seal：`199ce6fe81130138e9fedf98e688928ce44b0a45c2aebd1b92fdd9519b8225dc`；DLL：`1abc1eed02917d14fd775be60920fe74efb9b102bc53eea5874fc0128e5247ef`；audio：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。
- 原BIN：`e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`；image base `0x400000`，計時函式 VA `0xA407D0`、RVA `0x6407D0`。File Offset 與分塊位址沿用已封存 native-byte-audit.json，15 chunks重新對固定BIN byte SHA核對；未重新執行原BIN。
- 原JAR：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；三個ActionIntervalCalculator純計算class entry SHA核對既有jar-oracle.json。不初始化伺服器類別、不新執行JAR。
- 素材固定 `releases/r28/assets`，integrity SHA `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`；42個profile本文SHA逐一核對，沒有解PAK或修改素材。

已封存native270組包含action1／18／19、frame0／1／7與Speed0／1／2，其中90組Speed1；本次只重新核對算術與身份，沒有把它們列成新執行。固定JAR108次純計算亦只沿用。主管提供的八方向1008序列一致性與126組取整差異是靜態補充，與本次實際DLL檢查分列。

## action1／18／19 的 Speed1 適用鏈

原A467A0依武器posture選攻擊動作；action18為指定施法，action19為一般動作分支，經A43560→A3EF40→A407D0進入共同逐幀計時。Speed1分支取 `(3*t)>>2`，非整個動畫長度最後才乘0.75。原action18另有action31分支，不在本增量開放。

原oracle明示使用synthetic metadata lookup與中性callee／actor flags；Brave0不表示其他buff、特殊frame速率、actor倍速旗標自然全為中性。已證的是固定中性條件下共同逐幀Speed1分支，並非完整self-state解密、所有buff或原生scheduler。

原native函式輸入0可輸出0；輸入1在Speed1亦輸出0。素材126個action中44組含零時間幀。不得使用只接受basis>0的步行helper，也不得強制每幀至少1ms；顯示應在有限序列中跳過零長幀，保持後續幀順序與marker相位。

## 分離的時間與取整

令原每幀毫秒為 `t_i`（已存在milliseconds，否則 `floor(ticks*1000/24)`），第一個 `!` marker 的index為k。本輪沿用r30的metadata與marker前綴定義：

| 分量 | Speed1 要求 | 保全項目 |
|---|---|---|
| actor每幀 | `h_i=(3*t_i)>>2` | 先逐幀整數截斷，零值保留 |
| actor總動畫 | `sum(h_i)` | 不用 `(3*sum(t_i))>>2` 或 elapsed/0.75代替 |
| actor marker等待 | `sum(h_i, i<k)` | 不把完整marker前綴最後才乘0.75 |
| server interval | `trunc(serverBase*0.755)` | 原JAR double乘法後轉整數；與native0.75不同 |
| reuse | 原值 | 不乘0.75／0.755，不與server間隔相加 |
| 本機margin | 弓32ms；施法100ms | 放在max外，不隨速度縮放 |
| Speed1輸入guard | `max(serverInterval, nativeAnimation, reuse)+margin` | 延續已接受的動畫互斥政策，並非新伺服器公式 |
| Speed0輸入guard | `max(serverBase,reuse)+margin` | 保持r30正常節奏與legacy動畫，不藉本輪重寫 |
| Speed2輸入guard | `max(2*serverBase,2*nativeFramesTotal,reuse)+margin` | 保持r30慢速策略及動畫／release |
| 箭／光箭飛行 | 即時首步，之後30／60ms | 不因Speed1加速飛行 |
| 附著／impact | 保留各自effect序列 | 不把11722／221／2165／219一起縮短 |

126組素材全部有 `sum(h_i)` 與 `(3*sum(t_i))>>2` 的差異；marker prefix亦126組有差異。例如gfx61/weapon0：attack=621而非624；spellDir=962而非967。實際每幀、key、marker index、server、reuse及guard在haste-metadata-vectors.json，完全由封存素材與固定來源推導。

`sum(h_i)`是本輪新的相對Speed1相位；r30原marker索引與At原點保持不變。原frame初始化／先遞增／duration+1排程尚未完整重建，所以不能把本輪毫秒值稱為已證原native絕對release時點。原A467A0→A46240的marker啟動、7C3AB0的30ms與821360的60ms來源沿用；不重開絕對scheduler研究。

## 快照、高水位與生命週期

sender admit建立當步動畫／release快照。0↔1、1↔2及同值回報不重新起算；1→0→1與1→2→1的較慢有效輸入窗不得丟失。已准入快照不因當前速度回包縮短或回捲；下一個動作才採新速度。

deadline以原admit時間與各速度已接受的guard取得Math.Max高水位。r30正常速率不含新增native動畫guard，因此從Speed1變回0的高水位使用既有normal guard；從任何supported速度遇2使用既有slow guard。async成功completion可能依既有收到完成時間另延長，但不得覆寫較長有效預約。有效攻擊ack仍保留既有收到回包再保留輸入窗的政策；顯示時間與輸入等待是不同的量。

未准入請求要驗speed revision，即使同值或1→0→1也拒絕舊請求。own／remote不互改；相同預測動作的首個ack保留原At／Timing，不因目前速度建立新快照；真正晚到ack的第一次renderer呼叫就應按實際當前時間判斷已過期，不可先倒回過去prime飛行快取。

admit及晚到success／error須保全session、world/map、actor、appearance/weapon、correction、target/input、lease、death防護。失效callback不改新狀態deadline、不發Sent、不讓舊錯誤取消新世界；finally只處理自己的lease。自我技能不依賴無關target。未知速度→supported恢復後不能留下永久不可見快照（既有F1）。

move／attack／spell／item／ground各消費端都遵守同一有效guard。前三者使用相同可控ActionClock／MovementClock驗deadline−1／exact／+1；item／ground最後准入仍直接讀Environment.TickCount64，故兩個注入clock都回傳同一真實時鐘，實際等待來源推導的guard，分別驗before與at-or-after並記錄觀測時刻，不冒稱精確命中1ms。准入前其他類型busy亦不可繞過。釋放前死亡／map／appearance／weapon取消視覺，釋放時取目標位置一次，之後飛行不追蹤新座標。HP／MP只依原回包，不本地預扣或自造成功。

## 舊976的適配與本輪界線

原正式slow actions976的Program.cs與Extra.cs SHA在test-adaptations.json，舊封存全體artifact已只讀核對。逐項列出156個預期替換：25個request-Speed1、126個metadata-Speed1、5個transition-1-0；其餘820個預期保持。新Haste.cs另外加入精確快節奏送出及邊界、八方向逐幀採樣（按action聚合）、reuse、速度暫態、實際renderer／ack與Speed1舊callback。

不能宣稱原976未修改全過；舊request只驗下限，r30較慢送出可能仍滿足新的較小下限，因此必須用新exact-gate案例直接暴露缺口。舊server-action-vectors的Speed1明示保留舊速率，只有其來源／baseline意義，不能直接當本輪正確預期。

準備執行對象為真r30 DLL，packet／state／clock／sender為明示離線替身。沒有啟動正常產品／GUI／socket／原遊戲／服務／DB。Goal實讀null，不建立、恢復或修改。準備檢查點candidateReviewed=false、reviewComplete=false；收到主管精確candidate seal後，僅在新candidate-review-r1子目錄執行正式accept／reject。
