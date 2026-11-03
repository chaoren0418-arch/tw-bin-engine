# 隔離重現與故障注入設計

## 本輪已做與未做

已做：唯讀指定JSON／Java來源／留存javap／JAR；ZIP只讀取得單一class雜湊，未落地或執行class；核對3個追加時間線原始收據、8個主管既有pins、保留帳本差異。`review_gate_checks.py` 是**本輪新建的合成審查政策模型**，18例純記憶體判斷，非原服函式、非Java載入器重現、非MySQL持久化測試，不構成修復驗收。其owner／日誌條件是審查門檻，不冒稱目前facade已實作。

未做：DB/socket/UI、Java啟動、編譯、javap重跑、完整環境複製、JAR候選、原SQL修改。下面所有L1/L2/L3都是後續須另行授權的設計；本輪只完成L0。

## 最小複本及身份門檻

L0目錄就是本派工唯一寫入目錄，先保留小型原始證據pin與合成fixture，不搬完整環境。未來若核准L1，可在根內專屬新目錄只複製所需class／測試來源與小fixture。

任何候選複製均須：解析來源、目的實體路徑；目的必須在主管指定隔離目錄、不可是junction指回environments／releases；以新檔exclusive create方式複製本文，不採hardlink、不先改來源。複製後檢查 `os.path.samefile(source,dest)==False`，記錄Windows volume/file identity（或Python st_dev/st_ino）、目的st_nlink=1、SHA/size相等；資訊不可取得則不原地改寫。掃描候選檔之間也不能共享身份。核對完才允許改候選。原JAR及原SQL的多個硬連結位置一律不改。

本輪未複製任何候選，故不存在已通過候選file-identity驗收的宣稱；交付新檔由verify.py檢查link count。

## L1：原固定class的小型隔離loader重現（未執行）

只有核准後才選擇能隔離靜態相依的既有測試工具；不因缺工具啟動全服。實際d287 class若無法在無DB、無全域executor、無服務註冊情況載入，就先停止並提交精確相依清單，不能把手寫模型改稱實際class測試。若採測試替身提供World／persistence資料，要逐項明示替身，證明沒有真實socket或檔案持久化副作用；獨立子程序的安全限制也需先審查。

保留固定JAR作baseline，若有候選另建獨立檔；同一組輸入對baseline與candidate輸出比較，候選只能更完整維護一致性，不能因忽略失敗而較容易ready。

| Case | 注入／資料 | 必須觀察的結果 |
|---|---|---|
| L01 | 只有10326期限、World查無對應物件；重跑兩次 | 兩次皆拒絕，loaded=false；零timestamp發布、零持久寫入、原列不變 |
| L02 | 有效物件A＋孤兒10326，兩種列順序 | 全部驗證前不發布；A原時間引用不變，不能部分成功 |
| L03 | 主表尚未完整載入／讀取errors非空 | 明確失敗；不得把缺物件分類為已消耗 |
| L04 | ID<=0、NULL/畸形期限、重複ID；日期轉換錯誤 | parser或facade失敗；保留完整輸入，不刪無法解析的列 |
| L05 | object不是item、template=null、item ID不符 | 都失敗；無替身item／模板補填 |
| L06 | 未到期限的有效物件 | 按原期限發布、loaded=true；不延長期限、不變owner/count |
| L07 | 已到期限但物件仍有效存在 | load本身仍走正常發布；不能在load先刪物件。到期處理由另案生命週期測試驗證 |
| L08 | 過期孤兒、未過期孤兒、時區不明孤兒 | 全部仍拒絕；expired不是旁路 |
| L09 | 全驗證後、第一個set_time前替換World物件／舊時間引用 | pre/during publication守門失敗；不寫到替換物件 |
| L10 | 第一個set_time後、第二個set_time前拋Exception與Error各一次 | 反向核對所有已改項；能確認才恢復自己的舊引用，loaded=false；錯誤向上傳 |
| L11 | set_time先改值再拋出；最後一項後／後驗證時拋出 | 不能只依「方法拋出」判斷未寫入；檢查已發布集合與補償結果 |
| L12 | 發布中另一方更換物件或timestamp引用；值相等但不同引用 | 尊重現有引用identity條件；不覆寫另一方，標回復不明，不得ready |
| L13 | rollback set_time本身拋RuntimeException／Error | retained load附加suppressed錯誤、loaded=false；另行核對仍殘留的時間，不宣稱全還原 |
| L14 | 全部發布後CharItemsTable.requireLoaded失敗 | 走補償，檢查每項；沒有loaded=true |
| L15 | 正常載入兩次／失敗後乾淨重建World再試 | 不續期、不累計副作用；失敗不使用污染的World直接繼續；依版本重新快照 |
| L16 | owner不存在、owner錯誤、同ID被重用或來自錯誤庫 | 本facade沒有owner資格檢查，不能虛構baseline會拒絕；在候選的前置完整身份核對必須拒絕。記錄baseline差異並交生命周期worker解釋 |

## L2：持久化協定／MyISAM故障測試（未執行）

這一層必須由主管另行批准隔離DB，使用明確合成資料與獨立端口／UUID／datadir；不得連既有43324或讀寫原資料，不能把H2/SQLite/InnoDB通過當MyISAM等價。先確認真實DDL、SQL mode、時區、索引與交易能力，保留最小fixture；不需要先複製904個DB檔。

至少涵蓋：讀取前失敗、寫意圖前、意圖持久後但資料前、第一個持久變更後第二個前、第二個後但讀回前、讀回後但完成標記前、完成標記後但回應前；每點各測程序終止、SQL明確失敗、執行結果未知。MyISAM不假設ROLLBACK，逐表讀回並保留原表原列證據。

每個故障點必測：相同操作重跑、同ID不同期限重跑、錯owner、未知owner、來源在檢查後變更、archive/journal損壞或缺失、補償失敗與再次啟動。不完整pending只能進入對帳，不能猜失敗後重送。原始快照、預期前狀態、實際後狀態均需留存；沒有持久證據就維持拒絕。

若研究D選項，必須證明「所有列=完整有效集合＋經逐筆證實的終止集合」，未知集合非空就拒絕；分類指紋變更、重用ID或終止證據與有效物件同時存在皆拒絕。這是候選契約評估，**不是允許現在把10326分類成正常到期**。

## L3：資料回復與原服煙霧（未執行）

父對話接受具體選項、保全清單、精確差異與回復代價後才可排程。不能把已失敗啟動前備份直接稱為健康回復點。保留原環境、原表原列、原JAR及候選；資料表物理檔只有在確認停寫／關閉後整組處理，避免混用MYD/MYI或跨時點表。

驗收順序：檔案身份與hash→schema／全表關聯→全部角色/物品/貨幣/技能及期限對帳→有效限時物件正常存取與到期→正常全服啟動→正常登入／保存／重登入→服務正常停寫與可回復證據。新增異常、回復不明、資料差異未解一律停止，不放行開發；不得只以不再拋原例外作為通過。

## L0重跑

`python -B -X utf8 collect_evidence.py` 重建本目錄的唯讀證據摘要；`python -B -X utf8 review_gate_checks.py` 跑18個合成審查政策案例。封存後優先 `python -B -X utf8 verify.py`，不覆寫已交付輸出。第一次模型執行因本地腳本名inspect.py遮蔽Python標準庫而失敗，已在本目錄更名collect_evidence.py後重跑通過；未因此執行Java或接觸DB。
