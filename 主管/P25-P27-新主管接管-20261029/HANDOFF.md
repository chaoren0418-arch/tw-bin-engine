# P25–P27 新主管接管與階段派工

唯一主管：01a185b4-ecf6-705c-90e6-83b9eb1ba02f。使用者直接要求新建主管與階段工作對話；舊主管保持退役。本文件只記錄接管與派工，不代表全部階段完成。

正式入口仍為 r33。已獨立核對 r33 接受證據、目前引擎身份、r34 計畫封存與入口等 38 個 pins，全部符合；這不是重跑原生驗收。r34 隔離 stage 已接受，正式整合尚未執行。其餘 41 個 own profiles 只有規劃及 416 圖像／2688 待測映射準備，尚不是擴展實作。

| 階段 | 實際工作 | 專屬 owner / thread | 交付與驗收 | 相依 |
|---|---|---|---|---|
| P25 | 現行架構、狀態所有權、r28矩陣逐列對帳至r33與有限待辦 | 01a185b7-ddd1-79f0-98fe-fa456008cbd3 | module-map、architecture、completion-matrix、finite-backlog；逐項來源及SHA、歷史native版本分列 | 不等r34 |
| P26 | r34拾取計畫獨立覆核、正式整合接續與後續41 profiles精確基準 | 01a185b7-ea67-7053-9bfc-d9d90c1cff8f | review與finding；新版gate封存後再覆核，主管接受後才單一合入 | 原始plan已封存；接管gate需最小新版 |
| P27 | UI／輸入路由與可重現缺陷的隔離修復、原生驗收準備 | 01a185b7-f732-7f60-8716-a944d67724c2 | source-pinned routing/gap evidence；有確切bug才修復和回歸，不把未驗當bug | 無需等r34；避開Ground/WorldEntry |
| 唯一整合者 | P26接管gate與plan獨立新版本補件；本輪未授共享寫入 | 01a17a22-17e4-70c0-9792-166ddb86ea42 | 新版seal、old→new pins、舊主管被拒／新主管合法形狀／錯pin被拒 | 獨立覆核後才能核發shared receipt |

三個新階段已有真實回合並完成首批讀檔，詳 dispatch-receipts.json；create成功與running分列。整合者派工另見integrator-rebind-dispatch.json。只有主管可接回交付、更新本目錄、接受或退修；工作者只寫各自work。

更新：P25 架構與責任基線已產出並經主管接受，見 [architecture.md](../../work/P25-architecture-baseline-r1/architecture.md) 與 [完成矩陣](../../work/P25-architecture-baseline-r1/completion-matrix.json)。11模組、32主列、16補充紀錄、12有限待辦、120歷史引用；523來源pins與177正式來源核對通過。主管另獨立比較32列原文全部保留。P27 bounded review與12個native計畫已接受，171來源pins／19交付／45路由錨點獨立核對；沒有確證bug，沒有產品patch，native仍未執行。接受範圍詳p25-p27-root-acceptance.json。

P26原plan覆核已接受其退修結論；整合者已完成新authority與timeout partial raw保存補件，50檔seal 867c75d72137b18becc8138576bc0b2e8b6e529fabcd51482b485caa17419ad6，plan 6a9d7d6d57b74e7de93bad32aeba82d6ccdf504efd641daad718a53cc7b14761。已實際派回P26進行第二輪獨立覆核，turn 01a185c4-5572-7692-afd3-ddfd8ee8a29c 已執行唯讀核驗；尚未核發shared receipt。這是已交付→退修→接回review的實際接續，不是等待使用者回覆。

P24遺留仍包含同版全流程、完整原生畫面／滑鼠、map4／6016場景驗收與來源未知邊界，不能以r版號或斷言數宣稱接近完成。P25負責把全部舊row分配到明確階段/待驗責任；新階段數字不消除歷史未驗項。

平台Goal：主管目前工具清單沒有get_goal/create_goal，未讀、未建立，實際狀態未知。不得把文件當成Goal；子對話依自己的可用工具記錄。

接回機制：核對每個真實turn及progress；交付先review，不以completed回合當accepted。P26原plan完成review後接新版rebind覆核；P27有候選交由獨立review，P25則由主管逐項檢查矩陣分母。僅最終精確候選和基準被接受後，原整合者取得單一shared派工。用戶未回覆不停止其餘可獨立工作。

禁止DB／服務／原生遊戲操作，封存及共享硬連結保持不可變。不喚醒任何退役主管，不改模型或平台儲存。README舊r28段落與consolidation報告保留歷史性質，project-manifest仍是已pin的r33產品基準，本輪不為更換主管改它而破壞r34基準。
