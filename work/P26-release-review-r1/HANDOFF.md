# P26 release 獨立審核

本輪結論：**原版契約覆核完成；新版工具須 RG1 最小 generator 修訂，尚不接受工具／精確 package plan／實際 release。**

## 已核對的 exact 工具

- `work/P26-r34-release-gate-r1`
- artifact seal：`e8b88833ce71b783a7a57c82d4322b2306ea4cec9534e21bdfef7e9b6785ad3a`（20檔）
- tool-source seal：`40b32ea97bb80259c329ccb78001c71b0a5c9edbda160d66315d061fb5336c93`（14檔、6筆external dependencies）
- package_release.py：`6f71f11590b72df554563f39dc510f3a9e115bd4a87dc585864cc0787f444605`

逐檔hash封存相符。release authority/root最小diff與抽取coverage predicates保持原判斷；main除immutable verifier root alias外AST相同。新release shape拒絕舊主管、shared receipt、錯dispatch/plan等。局部依賴與archive選取完整：14檔＋tool-source-seal，另寫source-archive.json，未實際執行archive。

## RG1：需先補的 evidence closure

`completed_context`核對的是outer selected attempts的raw/probes與aggregate結果；沒有追查result內`innerEvidence.rawAttempt/stdout/stderr/probes`及product `canonicalNestedCommands`的舊reference pins。

自己的合成單元保留舊inner stdout pin，改變檔案bytes，再執行原inventory的workflow枚舉片段：inventory照常收錄目前新SHA，沒有拒絕它和原report pin不一致。這不是產品失敗或真canonical已漂移；只證明generator缺少這道一致性檢查。Full release SHA若只對新inventory做hash，也不會解決內部引用矛盾。

最小修訂是在**新版本**generator、任何archive/plan寫入之前，加read-only result evidence helper：驗每項inner attempt/raw/probes/相關PNG pins及success/identity/recipe argv/cwd/集合；驗4項nested child command/raw pins、membership、name/exit/argv/cwd。不得把舊reference pin改成現檔hash，不得重跑build/probe補證據。精確位置、觸發及獨立ret​​est條件見findings.json。此退修不影響原整合者正在做的canonical。

## 原immutable契約與限制

`original-contract-review.json`保留原版schema／門檻，不覆寫歷史。`release-contract.json`列14項coverage：全8 completed、四identity、180 source/resource、fullruntime/rebuild、10 helpers、stage/workflow/raw provenance、tool archive、containment/freshness、13958 immutable links、fail/no fallback與最終驗證。

原package gate來源數是>177而非exact180；新generator補exact180。原package gate只pin completed/result而不重驗所有suite語意；新generator補outer/suite/helper檢查，但inner closure仍缺。原readonly gate不hash全部素材body；本輪只核實13957筆inventory metadata＋integrity＝13958，body hash與samefile/link實證留實際package／full release verifier。原reserved-control檔名collision可能在mkdir後才失敗；新generator有RESERVED，精確plan須核其輸出。partial release保留，不自動覆寫／刪除／重跑。

原版63項isolated guards/helpers/evidence-loop單元、新版37項pure/outer-attempt/predicate單元都是synthetic，不是產品斷言。沒有真receipt或completed fixture冒充canonical，沒有呼叫完整package/generator/archiver，沒有新release或hardlink。未重跑原plan/source replay，也未重跑作者prep verifier去把已授權canonical變化誤報漂移。

## 三層接受與依賴

1. 工具：待主管提供修正RG1的新exact seals／generator pins。
2. 精確package plan：待真canonical完成與主管另綁exact package plan/completed pins；不可造completed。
3. 實際release：須另授權package後full seal/full SHA驗證，之後才可另訂入口切換。工具或plan接受均不是release成功。

Goal仍active，因上述後續精確依賴尚未齊備。本輪產品build/runtime/native皆0，不外推全案完成。

## 重跑

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-release-review-r1\review_original_release.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-release-review-r1\review_release_tools.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-release-review-r1\finalize_review.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-release-review-r1\verify_delivery.py"
```

所有輸出僅本root；不讀新版live/unsealed root。原contract與新tool結果分開保存。重跑不呼叫產品或package，不生成正式授權。
