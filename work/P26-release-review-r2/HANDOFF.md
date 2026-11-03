# P26 RG1 r2 獨立覆核

狀態：accepted-exact-release-tool-only。releaseToolAccepted=true；精確package plan及實際release均未接受。

最終正式核對已完成：617 artifact files、40 tool-source files與8 external dependencies逐檔相符；9個來源檔與獨立測試快照完全一致，因此56個有效案例未重跑。tool archive選取40檔＋tool-source seal，另加source-archive.json；新result_evidence.py已列入runtime required set及archive來源。作者690-pin closure報告由seal驗真，未冒稱本review重跑canonical。

Artifact seal：`30655ae3c7ce2b8efb82a2f7bfb35adc0f962a0f0162a0d323c29e231577fe13`
Tool source seal：`5bbde28bc03eef1e99453bedd92893ec7e5a4c4545023a9e0bbaef92dd391ec6`

本輪只寫 `work/P26-release-review-r2`，r1歷史覆核保持不變。已於自己的snapshot-01保存授權暫審來源及SHA；正式接受以主管exact artifact/tool-source pins及與測試快照一致為依據。

## 暫審結論

RG1引用核驗新增於completed_context回傳前，並於generate的archive/inventory之前再驗一次。generator實際差異只有completed_context、generate與package_inventory：後者沿用已驗證historical closure pins並重新核對檔案，避免重新取現檔hash掩蓋漂移。package controller、原coverage predicates及PE resource helper與r1 bytes相同。

獨立56項synthetic案例符合預期：原bytes正例；inner attempt/stdout/stderr/probe/PNG缺失或變動；必填pin/operation缺失、重複／未知；failed/partial/錯identity、argv、cwd、decoder、probe集合；nested command/raw缺失／變動、順序、集合、失敗語意；inventory沿用原pin正例與核驗後再漂移拒絕。案例完全由本review建立，未沿用作者fixture；沒有產品、完整generator或archive執行。輸出中數量不是產品斷言。

`Evidence.walk`驗證目前result/probe/marker值中的記錄pin，不把archive中的歷史before-state當現況。PNG bytes由既有probe/copy marker引用固定；pixel-evidence.json額外做目前rows/accounting檢查，不宣稱該衍生JSON已獲歷史hash錨定。原PowerShell grandchildren仍共用parent streams，不虛構child receipt。這些界線不擴大為產品或原生驗收。

## 正式接受所需核對

主管exact seals到達後，逐檔核對artifact/tool-source及external dependencies；比對9個快照來源；確認result_evidence.py在required runtime seal與archive selection內；確認policy/scriptRoot指向r2且原PREPARATION_ROOT不變。若最終source不同，先審diff並對新快照重跑必要測試，不能把暫審結果套到未知版本。

平台Goal在本次續作開頭實讀blocked；工具不能resume，未新建替代Goal或改內部儲存。本次仍依主管明確續作授權完成工作。精確package plan是下一個gate；工具接受不表示canonical重跑或release成功。

## 重跑

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-release-review-r2\run_independent.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-release-review-r2\finalize_review.py"
```

測試只使用本root已保存的快照，且每次另建synthetic子目錄。artifact manifest保留腳本、snapshot、結果與正式核對；synthetic子目錄是明示的非產品測試工作檔，未納入交付manifest，避免把它們誤當canonical provenance或正式payload。可由本腳本完整重建案例。
