# P36/P37 精確計畫獨立覆核

結論：changes-required-P37-F1。沒有出具 accepted-for-p36-serial-source-build 收據，不能把本報告用作共享執行許可。

主管轉交的五個 exact hashes 全部相符。作者 artifact-seal `a03d41a3f1966fe16967d3872e5acde7e5353c611c538c3e72e9d15ddd8fa878` 所綁定的414檔完整集合、13個 tool members 全部核對。plan `58d072aee47cc8f6c5a08071025dcd2c46ebebf2c3ef0e663605b5f2705302b5`、contract `7d7ff368af9a439c8bb65918766333479c493ca03a324f0ce96ac1278104b19a`、tool source seal `856fd8d0e76df91dd90c2fc5e655be306f3e6cc6b8fb44ec8c467921fd5088e1`。

## 唯一未通過項目 P37-F1

`bound_inventory.py:43` 目前直接執行 `bundle();protected();ledger=source_state('after')`。`bundle` 驗13個程式/reference檔；`protected`、`source_state` 和稍後的 expected-outputs 直接讀 JSON，沒有核對這些 JSON 在 exact contract 的 inputDocuments pins。P37 inventory 也沒有呼叫 gate.validate，因此完整的 inputDocuments pin loop 不在這條執行路徑上。

具體影響：真實主管 receipts 存在後，如果 protected-pins.json 少了一列而外層 plan/contract/tool/artifact-seal 檔未變，P37 會使用縮減後的保護清單，沒有文件自身 SHA drift 拒絕。receipt 比較 artifact-seal 檔案 SHA 並不等於重新核對 manifest 成員內容。這是已讀最終程式的靜態控制流程缺口；本覆核沒有偽造成功 receipts、修改作者清單或操作共享檔案以冒充正向完整執行。

最小建議（未修改作者檔案；見 suggested-minimal-change.diff）：

```python
_,ledger=validate(plan_sha,contract_sha,initial=False,dependencies=False)
```

以上取代目前 bundle/protected/source_state 一行，讓 P37 使用已修正的 after-state ledger 路徑並先驗 inputDocuments。保留既有 final/authority/material/output/command/四identity檢查。無須新產品套件。修後只需核新 exact diff/seals 及這一項 metadata pin gate，未改的基準與七項查核可沿用。

## 已通過及資格

P30 347 stage files、181來源與當前180來源已獨立對帳：四替換、一新增、176相同。完整 source ledger 與本review基準相符；五個允寫目標及新的 client-product-p36-r1/product-p36-r1-manifest 路徑避開入口、project-manifest、原 r34 build/manifest/launcher、所有 releases。

已讀 install/rollback：獨立備份與incoming、再核 before/nlink、同磁碟原子replace；rollback先核五個目標只能是before/after狀態，再以備份還原或把新增resource移到preservation；未知drift或缺原件拒絕自動回復，沒有永久刪除。這些是靜態審查，沒有實際安裝／回復。

canonical source one-build argv、private project obj/cache/temp、PathMap 邏輯對映、固定expected DLL、初次子程序即禁憑證及global-tools PATH／telemetry、離線NuGet、原值HOME/USERPROFILE/CODEX_HOME均核對。是否重現exact DLL與實際compiler provenance仍待未來合法單次build，不以本計畫審查預先接受。

作者提及的post-install copy-pin修正已確認：validate(initial=False)跳過original canonical copy來源pin，仍核獨立copy及prepared，使用after ledger。但目前P37入口未呼叫它，故尚不能解決P37-F1。

七項本review查核：真實唯讀preflight含4350 dependencies；缺execution receipt、舊r34 receipt路徑、錯plan SHA、錯contract SHA、P37錯plan SHA五項預期拒絕；初次child environment保留身份值一項。全部符合預期。沒有執行executor.execute、install/rollback/build、真P36/P37成功receipt路徑或任何產品套件。

P37新工具要求181-source final及inventory-only receipts，不借原180-source/8steps接受，不宣稱重跑4521。新launcher只繼承唯讀身份；不是launcher/package啟動驗收。

## 證據與重驗

baseline-review.json、protected-baseline.json、final-plan-verification.json、final-pins.json、suggested-minimal-change.diff、result.json構成精確覆核。commands/baseline-01、plan-review-01、finalize-01保存argv/cwd、stdout/stderr原始位元組與hash及exit。探索性Get-Content/rg輸出在對話工具紀錄，不捏造另存raw logs。

封存後只使用唯讀命令：

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P36-P37-integration-review-r1\verify_delivery.py"
```

一次性inspect_baseline/review_final_plan/finalize腳本不可在封存根重跑寫入。工具檢查的原始命令及結果均已保存；修後另開本review根內續作子目錄保存新證據，不改既有封存。source/artifact seal涵蓋本review交付，外層seal/manifest除外以免自循環。

歷史Goal實讀blocked，未新建替代或修改平台。只寫review根；共享寫入、build、DB/services/native、憑證、永久刪除、入口啟用均0。原入口維持r33。

保留原拒絕：ENGINE-R34-ENTRY-ACTIVATE-P26-R1 send_message_to_thread automatic approval review denied，未執行未重試。原文：「此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。」本輪不重試、不改道。
