# r29 已完成離線開發、合入與主管核驗

目前正式入口為專案根 `啟動引擎.cmd`，指向 `releases/r29`。Speed2／Brave0 步行、轉向與遠端物件顯示已完成；獨立覆核在 r1 發現 F1「恢復合法速度後仍持續不顯示」，原實作者以 r2 最小單檔修正，原覆核者親自複驗接受後才由唯一整合者合入。

主管已接受精確 r29 seal `d3ac187e7fc44745864640e4bcc72fefbaeeaa0b0e9d63de7c0c3cfa8e46273d`，產品 DLL `8ccfa4e3b5d253206779a5d4b1e59682263ffbddf5dab43fe15b0aa1f86a7397`。stage、共享 engine 重建及 release 身分一致。151 項既有回歸、1199 項獨立向量、112 項專項及另外六個最小案例通過；主管再獨立逐檔核對 14286 個發布本文、176 個來源與正常入口 check-only。r28 封存保全通過；三位工作者皆已完成回合並停寫。

當前決策見 `r29-supervisor-acceptance.json`，全檔核驗見 `r29-root-delivery-verification.json`，入口實讀見 `r29-root-entry-check-only.json`，更新佇列及矩陣為 `queue.json`、`completion-matrix-r29.json`。完整整合交接位於 `../../work/engine-next-integration-r1/HANDOFF-r29.md`。

此增量的已授權開發動作已完成。緩速攻擊／施法／道具等非步行動作仍未支援；原生連線流程未驗收，P24 未全部完成。資料庫及服務由使用者處理，本輪沒有查詢或操作。平台 Goal 實讀仍 paused，未更動平台狀態；不要重建 Goal、重啟 DB 診斷或重做已接受的 6016／本次緩速功能。其他來源受限範圍在新矩陣明列，歷史實機證據不升格為 r29。

## 下方保留最初派工紀錄（已由上述結果接續完成）

最新使用者指示將資料庫交由本人處理。停止 orphan10326、DB 診斷與隔離重現後續工作；既有證據保留，主管不再查詢或啟停服務。DB 煙霧只限制需要伺服器的整合驗收。

兩個原工作對話已接受派工且觀察到 active/inProgress：ENGINE-SLOW-IMPL-R1 實作 Speed2/Brave0 緩速步行與呈現；ENGINE-SLOW-REVIEW-R1 獨立核對固定 BIN/JAR 計算與引擎消費鏈。這是 NormalHaste.cs、OnlineMovement.cs、ObjectRendering.cs 及歷史 basic-potion-haste-r1 明列的未支援狀態，不重做既有滑鼠／6016／陰影功能。

原唯一整合者亦已派 ENGINE-NEXT-INTEGRATION-PREP-R1 並觀察 active/inProgress，在自己的 work 目錄準備下一版接受契約及建置入口。現行 run_stage.py 固定 r28 身份；新入口必須要求主管接受的候選 pins 與覆核收據，不得放鬆完整性驗證。共享合入仍等實作、覆核交付與主管核驗，未授權提早修改共享 engine。

dispatch.json 保存兩個功能工作完整派工；send-and-active-receipts.json 及 integrator-active-receipt.json 是實際送達與執行收據，queue.json 為當前佇列。baseline-source-pins.json 固定 140 個當前來源檔。三人專屬 work 子目錄互不競寫；releases/r28、assets junction 與共享硬連結保持不可變。

下一步：審核獨立契約與候選，必要時在原工作對話修正，證據足夠後由唯一整合者串行合入。離線候選不等於實機成功或 P24 完成。平台 Goal 實讀仍 paused，未恢復、重建或修改平台儲存；這不阻擋當前新授權工作。
