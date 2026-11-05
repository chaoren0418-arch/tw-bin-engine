# Issue #71：DEMO-2 真機驗收紀錄

結論：部分功能已驗證，完整 N01 流程未通過；含實機證據的提交暫留本機，尚未推送。逐項結果與截圖見 [展示頁](../demo/README.md)。

## 執行與版本

- 主機 DESKTOP-CV2L01A；2026-11-05 UTC 執行，2026-11-06 Asia/Taipei。開始前確認 Issue #71 為 runner:dot／task:running、無 RESULT，且無另一個實作工作。
- 從 task/70 的 `12352e0ed022c5f217c097ed3f30801840327655` 建立 task/71。`71ebe7e` 與 Issue 原列 `eab1c7d` 都是 HEAD 祖先；建置來源至 HEAD 的 engine 差異為空，兩項版本要求不衝突。
- 沿用 #70 的 dev 產物；check-only exit 0、revision dev，未同步或重建產品。DLL、receipt 的 SHA-256 見展示頁。
- 使用既有 native-ui helper 與本機工作階段，每次送出前核對本次 PID、標題；未使用 cua。
- 目前工作階段實際為 workspace-write／auto_review，無法驗證或宣稱已切換完全存取；需要的操作經工具核准執行，未改安全設定。未遇到 setup refresh had errors。

## 環境、備份與收尾

原 D:\P24Lab-ClientEngine 已移動；依整併紀錄核對目前實體目錄 environments/p24 與 D:\BinEngine junction，未以路徑名稱單獨判斷身分。

MySQL 啟動後三項相符：UUID `e3443e40-d090-11f1-8040-4cedfb679789`、port 43324、datadir `D:\BinEngine\environments\p24\db-data\`。Java policy 僅允許實驗埠。啟動前冷備份存於 `environments/p24/backups/issue-71-before-demo/db-data`，893 個檔案逐一 SHA-256 相符。未執行資料修改 SQL；只有身分及指定測試角色／背包 SELECT，遊戲操作正常保存資料。

使用者禁止的正式 2000／3306、既有遊戲程序、原 BIN、封存、config/server.properties 與 docs/REPORT.md 未修改或操作。未加能力點、未吃最後一塊肉、未刷怪或修改 NPC。

本次兩個客戶端 PID 8924／21144 均正常關窗。Java PID15388 正常關閉（19:40:22Z）。首次清理腳本釋放 console 後，MySQL 命令遇到「Win32 internal error 控制代碼無效 0x6」；確認 Java 已退出後，在新命令中重新核對 DB 身分並正常 SHUTDOWN。最終 `shutdown-verification.json`（19:41:21.8898403Z）記錄 ShutdownExit=0、RemainingOwnedProcesses=[]、LabListeners=[]、IdentityVerified=true。本次 MySQL PID24452 已不存在，43324／42061 均無監聽。

## 關鍵實測數值與限制

初始角色 Lv59，HP686/686、MP114/117、EXP412855581、Map0、座標32560/32955；最終與重登後均 HP686/686、MP117/117、EXP412855581、Map0、32562/32956。登出後與重登後逐項背包 id／item_id／count／is_equipped 完全一致。力量22、BonusStatus4、肉1保持不變；治癒藥水42減至41，金幣33400未變。

八方向各兩次按鍵僅產生13筆 C29，點地階段3筆；沒有將差額推定為已證實的碰撞。UI 未獨立取得精確座標，不能用最終 DB 座標替代點地終點的單步證據。

未發現固定練習目標，嘗試點附近牛並攻擊，沒有成功選中有效目標，未見 HP 減少／死亡；EXP 沒有增加。光箭同樣因未選對象未施放。保護罩與神聖武器的 Buff、MP 變化有證據；治癒術與喝水均在滿血狀態，不能證明回血量。MP 期間有自然恢復，不能把不同時間的差值當成精確消耗。

匕首卸裝後 UI 紀錄確實出現「未裝備；可單次穿上」，裝回後出現「已裝備；可單次卸下」，最終 DB 為已裝備。但 09-unequipped.png 拍得太早，仍是卸裝操作當下的舊畫面，因此展示頁保留部分驗證。06-exp-before.png 實際拍到能力點頁，不能作為 EXP 畫面證據；11-stats.png 使用同頁證據，未執行加點。

金幣第一次輸入1並送出 C25 後收到原服訊息125，未出現地面金幣或數量減少。輸入10的操作沒有第二筆 C25，不能宣稱10枚也被伺服器125拒絕。輸入0／33401後出現共用拒絕訊息，包含數量範圍與重新選取要求；選取有效性未隔離，尚不能單獨歸因於數量檢查。沒有拾取 C112 或完整丟撿循環。

音量50%／靜音／恢復的客戶端 session 峰值分別為0.27048927545547485／0／0.2572101354598999。三階段各有一次治癒術及攻擊嘗試；沒有有效攻擊目標，無法將峰值單獨對應每種事件。只量本次客戶端程序，不錄其他程序聲音；未安裝錄影工具。

聊天文字「DEMO71 本機驗收」的輸入與送出命令遭 auto-review 拒絕，理由原文：「命令會向遊戲伺服器送出聊天訊息『DEMO71 本機驗收』，這是對外溝通承諾且使用者未明確授權該精確內容；後續靜音操作不足以覆蓋此未授權副作用。」讀取使用者歷史未找到新增的精確授權，因此未重試、未繞過；命令未執行，聊天無回顯證據。獨立的聲音與收尾工作繼續完成。

## 產出與下一棒

交付候選為 docs/demo/README.md、docs/demo/*.png 與本報告。原始 trace、DB 查詢、UI 紀錄、音量樣本與關閉證據留在 `scratch/issue-71/`（trace 在 `scratch/packet-trace/`），未加入 git。誤拍到登入區的兩張圖已隔離至 scratch，不作交付。交付截圖逐張人工視覺核對無帳密明文；選角採拍攝時直接擷取局部，不合成畫面。

共37張 PNG，合計19,661,888 bytes，最大713,848 bytes，符合單檔2 MB／合計25 MB限制。文件相對連結及 git diff --check 通過；本輪只有文件與截圖，不需重建產品。19:51:09Z 再次唯讀確認兩埠無監聽、四個本次 PID 均已不存在。

收尾時 `gh repo view --json visibility` 回報 PUBLIC，與先前私人交付前提不一致。本任務不更改 repo 設定；先保留本機提交並將最小阻塞摘要交回 #71。這是未執行 push 的交付限制，不是已發生的 push 核准拒絕。

總控下一步先確認交付可見性與實機證據可否公開，再取本機 task/71 交付。後續開發優先核查金幣丟棄回應、選取有效性與目標選取；補測八方向／座標、近戰與 EXP、光箭、受傷後治癒、卸裝完成截圖、聊天及事件音效。不得把此次 RESULT 當作全功能通過，也不要為交付問題重啟已關閉的驗收環境。
