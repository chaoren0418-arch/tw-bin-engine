# P26-INTEGRATION-AUTHORITY-REBIND-R1

本包是新主管接管相容性補件，供 P26 獨立覆核及新主管綁定 exact SHA。不是 r34 正式合入；本回合沒有 shared receipt、workflow、install、product/helper build、產品斷言、release 或入口變更。Goal 工具可用，實讀 null，沒有建立或修改 Goal；沒有新聊天／子代理，也沒有跨對話傳訊。

唯一新主管：`01a185b4-ecf6-705c-90e6-83b9eb1ba02f`。退役主管 `01a17e6a-9270-7cc2-a338-0120ed68d0ec` 只保留為歷史 stage evidence 的原簽署身分，不可再核發新的 shared receipt。

唯一寫入根：`D:\lineage開發\TW1608262200研究\work\P26-r34-authority-rebind-r1`。

## 精確修訂範圍

原封存 plan SHA：`d25c306fae5abee9209e46c557453f7b6a0a96645e5c052fbda70555f11f15ff`；原 seal：`faf5f661f5d569c950ff854dfa8bff6f1cf9d338d98e3bbf9006fd6555b3a2fe`。二者不改。新版 plan/seal SHA 見 result.json 及交付訊息。

| 部件 | 新版變更 | 保留的實質 gate |
|---|---|---|
| acceptance_gate.py | SCRIPT_ROOT 指新隔離根；PREPARATION_ROOT 固定原 R2；HERE 作歷史 helper alias；舊 SUPERVISOR 更名 STAGE_EVIDENCE_SUPERVISOR | 所有 source/base/path/hash/resource/delta 函式內容與判斷不變，歷史 stage receipt 的原簽署身分不變 |
| shared_authority.py | 新增 current shared identity 與 exact authority-policy 綁定；釘住真實 takeover 與舊 stage 接受證據 | 舊主管的新 shared receipt 拒絕；不冒名、不改全域常數、不替換來源或驗收結果 |
| serial_canonical.py | 原 HERE 語意明寫 PREPARATION_ROOT；原 shared identity 判斷改調用新 predicate；增加 authority-policy 與 historical stage identity gate | install、collect、main、source 180、四 identity、stage/preparation seal、strict serial、helper/command pins、失敗保留及不可重跑原樣保留 |
| common.py | controller import root 由原 preparationRoot 改為 exact scriptRoot | 原 raw-log adapter、授權、installed/prior/completed gate、獨立複製與回復規則不變 |
| execution_support.py | 主管追加授權：只新增明確 TimeoutExpired 分支，保存 partial raw bytes/decoder/hash，failed/unknown exit 後重新拋出 | private env、成功路徑、collect 拒絕失敗、同 attempt 不可重跑均不變 |
| pe_resource.py | 與原 R2 byte-identical independent copy | 原資源判定不變 |
| wrapper / collectors / recovery | 與原封存 plan byte-identical independent copy | build/probe 不重跑、最後 collector 獨立恢復、原 assertion/occurrence/pixel 判定不變 |

`diffs/*.patch` 提供最小 source diff；`initial-source-rebindings.json` 與 `rebindings.json` 提供 old→new size/SHA。JSON path/authority 差異見 `plan-delta.json`。原 plan 全部 support pins 與每個 command 原 script pins 都保留，再加入本包 pins。44 個 outer commands 只把 wrapper.py 路徑改為本包；內層 argv/cwd/probes 完全相同。10 helpers、138 copy mappings、51 fixture、預期 4521 assertions +93 observations 不變，這些是未來預期，不是本次實測。

Timeout 修復與 authority 接管分開列於 `timeout-fix.json`。原 R2 的 TimeoutExpired 落入通用 except，未保存 ex.stdout/ex.stderr；新版保存可取得的原始 bytes，未提供的 stream 以空 bytes 加 availability=false 表達，記錄 timeout/timeoutSeconds/error、completed=false、exitCode=null，然後重新拋出。沒有改成成功或自動重跑。原 sealed R2 不改。

舊 stage acceptance 的 path/SHA 保持：`主管/P24-權威拾取動畫-20261029/r34-stage-root-acceptance.json` / `d8923799b13660f4eab08ac52d45f27c183c3d0e01aed3d491a62b593e110308`。原 stage seal 保持 `9544f137a10914825b53080d4d390d17902cd40866467cebbf69578290240018`。overlay 內原接受資料也是封存的歷史來源證明，不會被冒充成本次新主管 shared 授權。

## 新授權契約與未來執行

新 shared receipt 唯一路徑：`D:\lineage開發\TW1608262200研究\主管\P25-P27-新主管接管-20261029\r34-shared-acceptance.json`。本包沒有建立它，也沒有保存合成 receipt fixture。

主管完成獨立覆核後，須另行核發 `engine-r34-shared-acceptance-v1` receipt，supervisor 必須為新主管，decision=`accepted-for-r34-serial-canonical`，authorization=`install-build-and-offline-suites`，dispatchId 前綴仍為 `ENGINE-R34-SHARED-INTEGRATE-`；planSha256 必須是本包新版 plan，並綁定同一 candidate、stage seal、原 stage acceptance SHA 及四 identity。沒有使用「舊主管身分、新 dispatch」的替代方式。

PREPARATION_ROOT 固定 `work/engine-r34-integration-r2`，所以既有 stage 與尚未建立的 workflowRoot `work/engine-r34-integration-r2/r34-shared/accepted-pickup-presentation-r1-01` 不會因腳本搬到 P26 目錄而漂移。canonicalEngine 仍是 exact `D:\lineage開發\TW1608262200研究\engine`。

只在新精確派工後，使用本包 `serial_canonical.py`（不能再使用封存舊 controller）先做無 --execute 的完整 validate_plan。正常 step 次序仍為 install → product → baseline → ground → utility → haste → versioned-slow → f1 → pickup，每次僅一個 step。命令格式：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-authority-rebind-r1\serial_canonical.py' --plan 'D:\lineage開發\TW1608262200研究\work\P26-r34-authority-rebind-r1\canonical-plan.json' --plan-sha256 <NEW_PLAN_SHA256> --receipt 'D:\lineage開發\TW1608262200研究\主管\P25-P27-新主管接管-20261029\r34-shared-acceptance.json' --receipt-sha256 <NEW_RECEIPT_SHA256>
```

以上只列未來格式，本輪未呼叫完整 validate_plan。未來 --execute 或 --collect-only 的原條件與順序均不變；recovery.py 仍只允許最後 collector 的 fresh attempt，不重跑成功 build/probe。product 第一步會獨立複製本包 pinned scripts/maps/歷史來源副本及 exact plan 至 workflow/evidence/canonical-plan；後續封裝證據來源保持在 engine/workflow/stage 允許邊界內。

此補件未改原 package_release 與 release authorization；release／入口不在本輪授權。未來若要發布，須另行審核接管後 release gate，而不能拿 shared receipt 當 release receipt。

## 可重跑驗證

以下是本回合實際執行的靜態／記憶體 gate 驗證，可唯讀重跑：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-authority-rebind-r1\verify_rebind.py'
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-authority-rebind-r1\test_timeout_capture.py'
```

`--report` 僅在本包內新增 verification.json；已有相同報告時只核對、不覆寫。驗證包含原封存 pins、stage non-assets、現行 engine baseline、最小 source transforms、44 operations／內層命令／138 mappings／raw-log adapter 不變，以及 11 個 bounded gate 案例。案例的 shared receipt 只在記憶體中：新主管合法形狀通過、舊主管新 shared receipt 被拒、plan SHA 被改拒絕、錯 schema/authorization/dispatch 被拒、歷史 stage 被改簽署者拒絕、錯 authority binding/receipt 目錄/來源 pin 被拒。結果只表示 gate predicate 相容性，不表示完整 shared gate、產品測試或正式接受。

另有 3 個 timeout 合成負例：以 mock subprocess.run 丟出含非 UTF-8 bytes 的 TimeoutExpired，不實際啟動子程序；驗證 stdout/stderr bytes 及 decoder/hash 完整保留、collect_attempts 拒絕失敗、同 attempt 重跑在呼叫 subprocess 前就拒絕。`synthetic-timeout/` 保存原失敗 attempt 和 raw streams，明確標示 synthetic/notProductAcceptance。上列第二個命令唯讀核對既有證據及拒絕條件；最初建立證據使用 `--create-evidence`，封存後不可重建或覆寫。原始 gate 常數沒有被 mock，僅測試中的 subprocess.run 使用 mock。

未執行 DB、服務、GUI、socket、native、角色或產品操作。原 r33 engine、assets/release、原 work、主管資料都保持唯讀。原 product collect 在 exclusive-save bindings 與 completed 之間中斷的恢復限制仍保留，原 sealed PowerShell 子命令共用 parent raw streams 的限制也保留；此回合未擴張成其他修正。交付後等待新主管精確整合派工。
