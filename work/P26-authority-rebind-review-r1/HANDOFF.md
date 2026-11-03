# P26 authority rebind 獨立覆核

結論：**accepted-for-supervisor-shared-gate**。本結論只供現任主管決定是否另發 exact shared receipt；本review不是receipt、沒有授權或執行共享整合。

## 精確接受對象

- Root：`work/P26-r34-authority-rebind-r1`
- 50檔seal：`867c75d72137b18becc8138576bc0b2e8b6e529fabcd51482b485caa17419ad6`
- Plan：`6a9d7d6d57b74e7de93bad32aeba82d6ccdf504efd641daad718a53cc7b14761`
- Controller：`257533f50b13c4483c337791006483e07ceac7a90be4749cb66058e36dfb7ba3`

F1已解決：新shared gate只接受現任主管；歷史stage仍要求舊簽署者，未改舊證據。PREPARATION_ROOT/HERE仍在原R2，SCRIPT_ROOT與common實際載入新版controller/execution_support。16個獨立記憶體authority cases與真實唯讀takeover/policy binding核驗通過；未建立receipt，未呼叫完整validate_plan。

獨立source/diff/AST核對最小修訂；install/collect/main未改。44外層命令只更換wrapper路徑；inner recipes JSON值不變（序列化bytes可不同）；138 mappings相同bytes/hash、只1個planning-source前綴更換；180來源與四identity不變。

原181 support pins完整保留＋43新pins＝224。每個command包含新runtime dependencies；product archive選取43個新root support files＋exact plan＝44檔，包含authority-policy/shared_authority、controller、execution_support、context、maps與timeout證據。此為靜態archive選取/依賴閉包驗證，沒有建立workflow。artifact-seal/result/verification等7個最終作者metadata不在自動archive選取；不是runtime漏件，但未來release驗收須另外保留exact source seal與主管/review接受證據。release/package gate未在此授權。

## 必須保留的失敗界線

F3修正在execute_once邊界有效：自己的5組fixtures覆蓋非UTF8 bytes、只有stdout、只有stderr、都不可得及可得空stream；核對raw bytes、availability、decoder replacement/hash、timeout/exitCode=null/completed=false、重新拋出、collect拒絕與重跑在process前拒絕。作者現有測試另唯讀重跑通過，不取代獨立fixtures。

**不能宣稱F3所有層級都修好**：原sealed rebuild.run內部child timeout沒有修改。獨立抽取該函式以合成timeout證明仍沒有child command/partial raw檔；外層無法恢復只在child程序內捕獲的bytes。遇此失敗必須保留現有證據、停止交主管，不能collect或重跑。完整nested診斷保存需另授權overlay改版；本次bounded gate接受不冒稱它已完成。

F2仍保留：product收集若在bindings保存後、completed/product之前中斷，不能刪bindings、改紀錄或重跑build/probe；停下交現任主管另定精確恢復版本。

F4歷史AGENTS漂移已由先前byte還原與主管接受解釋；本輪現行policy/takeover/dispatch pins相符。原sealed prep完整verifier FAIL不改寫，41 profiles後續契約才重綁政策與final canonical base。

## 主管下一步

`supervisor-gate-contract-inputs.json`給出精確plan/controller/seal、receipt欄位要求、四identity、舊stage acceptance SHA及新receipt唯一位置。主管若採納，另發真正shared receipt，唯一整合者先用新controller與實際receipt執行完整唯讀validate_plan，再按新派工逐步執行。失敗停止規則必須隨契約保留。新review未寫shared receipt/workflow、未install/build/release/entry；產品build/assertions/native均0，4521預期斷言不列本輪執行。

## 可重跑命令

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-r34-authority-rebind-r1\verify_rebind.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-r34-authority-rebind-r1\test_timeout_capture.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-authority-rebind-review-r1\review_rebind.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-authority-rebind-review-r1\finalize.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-authority-rebind-review-r1\verify_delivery.py"
```

前兩項不加--report或--create-evidence；新review腳本只寫自己的root，合成subprocess均mock、實際process為0。若之後主管已建立receipt/workflow，準備期absence檢查會正確失敗，不能把它當允許繞過gate。
