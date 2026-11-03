# P38 離線交付有限接受

P38 已完成並由主管接受，兩位原工作者均 completed／idle。沒有新增代理或 P39，沒有停在派工後等待使用者逐次驗收。此為 P36 既有遊戲功能的完整隔離材料與 launcher check-only 資格，不代表全引擎或原生遊戲驗收完成。

候選：`work/P38-delivery-candidate-r1`。執行證據：`work/P38-delivery-execution-r1`。獨立覆核：`work/P38-delivery-review-r1/final`。候選保持封存停寫，內部 pending-final 文字是執行時歷史狀態，以外置 `P38-final-acceptance.json` 為本次接受依據。

- 完整集合 14,181 檔，224 份獨立複本，13,957 份不可變素材硬連結。
- 素材全文 SHA 各驗一次，共 2,222,723,085 bytes，零差異；不修改硬連結內容、屬性或 ACL。
- 原樣包含 181 份 source/audio/web、15 份 P36 輸出；web 30 檔，新 normal manifest 46 列。產品重建 0 次。
- launcher build 1 次，0 warnings／errors；獨立 binlog 解析 3,703 events、1 個 Csc、178 個實際 SDK／參考輸入，來源與輸出匹配。
- check-only 2 次：候選 exit0，舊 r34 因缺新候選 manifest 拒絕 exit1。正常 branch、遊戲、UI、DB、服務皆未啟動；確認 Core/Desktop10.0.8、WebView2 156.0.4314.50。
- 作者證據 72 檔、獨立最終覆核 38 檔封存，主管已核對；33 個保護 pins 不變。

| 版本 | 真實範圍 |
|---|---|
| r33 | 現行正式啟動入口；未切換。 |
| r34 | 封存基礎 own-pickup 動畫，不含新增41組外觀。 |
| P36 | canonical 新增41組 exact own-pickup profiles，含control共42，speed0/1、Brave0。 |
| P38 | 原樣封裝P36產品及新綁定launcher，離線資格接受；未成為正式入口。 |

**批准舊 r34 入口不會啟用 P36/P38 的41組新成果。** 四項完整identity記於接受收據；P36產品DLL `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`，新launcher DLL `44361072a33d572e155d80af41bf6245cf80fe8770eb1b80bdae0958540053b6`。

剩餘工作與恢復條件：正常啟動／原生畫面滑鼠／固定版遊戲全流程、交易保存及6016音訊需明確原生操作授權與合法安全情境；需要服務的項目由使用者準備DB／服務。隔離原生驗收不必然要求切換正式入口。真實地圖effect→group淡出、其他token與缺素材／格式須精確權威來源，不能猜測或重複已無線索的研究。完整回歸與歷史證據界線維持，不捏造舊收據。

目前有限清單中已無具體、來源足夠且符合現有禁區的待實作／封裝項；不為保持活躍而增加階段或測試。細項見 `P38-convergence-delta.json` 及更新後 `current-finite-backlog.json`。不報全案完成率。

本輪覆核工具曾引用錯誤reader期待SHA，在使用前被assert阻擋；已由P36既有外層seal／manifest核實正確身份，保留首次失敗與更正證據。沒有產品修改、重建或檢查重跑。

主管Goal工具未提供；整合者實讀null、覆核者歷史I01 blocked，均未改動。文件不冒充平台Goal。

原 ENGINE-R34-ENTRY-ACTIVATE-P26-R1 向唯一整合者的正式入口派工遭自動核准審查拒絕：缺少將r34升為現行正式版本、持久修改入口與project-manifest的明確使用者授權。此動作未執行、未重試或改道，目前仍pending；入口r33不變。
