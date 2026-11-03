# P36/P37 r2：P37-F1 最小修正交接

本輪僅依唯一主管 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f` 派工修正獨立審查 `P37-F1`，唯一寫根為本 r2 目錄。真 Goal 讀回 null，未建立或修改。r1 與 review-r1 原封存不變；完整獨立副本分別留在 `reference-r1` 與 `review-r1-reference`。

唯一 runtime 語意修改位於 `bound_inventory.inventory`：

```diff
- bundle();protected();ledger=source_state('after')
+ _,ledger=validate(plan_sha,contract_sha,initial=False,dependencies=False)
```

這使 P37 在使用 protected/source/output metadata 前，先走原契約的全部 `inputDocuments` pin 迴圈，再執行未變的 bundle、protected 與 after181 source guards。`initial=False` 保留安裝後模式；`dependencies=False` 不重算整套外部 SDK pins。新的 final/authority receipts、material、command、四身份與 inventory-only 限制均未移除。

其餘12個 runtime/reference 檔案與 r1 位元組相同，包括 common、gate、executor、win_process、build props/targets 及 P35 references。7份文件僅有 r2 路徑、版本標記及受影響 size/SHA 的機械重綁；完整逐欄 before/after 在 `delta.json`，runtime diff 在 `P37-F1-runtime.patch`，copy-before-edit 在同名 JSON。`verify_delta.py` 逐項核對，禁止未列出的語意／文件差異。

沿用原 execution root `work/P36-canonical-integration-execution-r1`、fresh `engine/build/client-product-p36-r1`、`engine/build/product-p36-r1-manifest.json` 及 `P36-execution-acceptance-r1.json` 路徑；目前三個輸出位置仍不存在。完整181來源、四替換一新增、single build、expected product/audio DLL、環境、安全回復、全部禁寫區不變。P37 工具版本為 `P37-inventory-only-r2`，沒有虛增遊戲版本或正式發布資格。

本輪只做兩項相關檢查與一次唯讀 preflight：

1. 將實際 protected-pins 獨立複製至 `negative-fixture`，刪除其中一列。執行從原 `gate.validate` AST 原樣取得的 `inputDocuments` pin 迴圈，僅將檔案路徑指向隔離副本、保留原契約 expected size/SHA；確認直接拒絕 `pin drift`。真契約及真 protected-pins 未改。
2. 靜態核對 P37 呼叫 `validate(...,initial=False,dependencies=False)` 且 pin 迴圈位於 metadata 消費前；直接執行未 monkeypatch 的真 gate.validate post-install 路徑，確認到達並拒絕 `exact canonical source set after`。現有 canonical 仍為 before180，所以這是預期拒絕。沒有跳過 inventory receipt prefix 或偽造主管成功收據。
3. r2 executor `validate` 唯讀 preflight 執行一次成功；沒有安裝、build、rollback、runtime 或成功 future receipt 路徑執行。

原 r1 的16個 bounded checks、347 stage pins／4,350 dependency pins 的原始準備證據保留在 reference-r1，明列為繼承，不當作新增案例；沒有重跑16案例或產品套件。一次 r2 preflight 依原 gate 規則驗證其真實相依，不增加產品測試數。

`commands/prepare_repair`、`minimal_checks`、`preflight`、`finalize_repair` 各含實際 argv/cwd/exit、原始 stdout/stderr 及 SHA。準備腳本一次性，封存後不要重跑。唯讀重驗命令：

```powershell
python -B 'D:\lineage開發\TW1608262200研究\work\P36-P37-integration-plan-r2\verify_delivery.py'
python -B 'D:\lineage開發\TW1608262200研究\work\P36-P37-integration-plan-r2\verify_delta.py'
```

`plan.json`、`execution-contract.json`、`tool-bundle/source-seal.json` 均已綁定新 r2 pins。主管需先取得獨立差異覆核，再核發綁定 **r2** plan/contract/tool/artifact seal 的 execution receipt；r1 review 的 changes-required 不能用作接受收據。P36 final acceptance 與 P37 inventory-only acceptance 仍為 pending；欄位需求見 `pending-receipt-requirements.json`，沒有製造成功收據。

414檔 r1 原交付及25檔 review-r1 證據的獨立副本只供追溯；其中原 result/commands/seals 是歷史紀錄，不代表 r2 新執行。r2 最新資格以根目錄 result、commands、外層 artifact manifest/seal 為準。

所有共享寫入、build、DB/SQL/服務/原生/GUI/遊戲/socket/network/憑證變更/永久刪除均為0。正式入口仍 r33，root entry 與 project-manifest 未寫入。完成最終新 seals 後停寫，交一次封存結果等待獨立差異覆核。

原 ENGINE-R34-ENTRY-ACTIVATE-P26-R1 的 send_message_to_thread automatic approval review denied 保留，未重試或改道，原文：

> 此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。
