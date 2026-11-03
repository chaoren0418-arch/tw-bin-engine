# P35 RG2 獨立覆核

結論：accepted-isolated-rg2-fix-only。RG2 在精確候選中已修正，可交主管接受隔離修補；正式工具尚未安裝。

候選 SHA-256：`4fbf32a4d1a5faa12e34631ad2c0e9ee02647ff51947128c69e1894690f13fa2`。作者 artifact-seal.json SHA-256：`664a0d7df517d7c9bb191c99cf94995843be882eae26302c8cf344998596156a`；它綁定 artifact-manifest.json `1dc485e7ed75c870d18cf9f5f28668040fe3a6b9878bf693b2cde252d51ddce6` 與 source-seal.json `0a9499f8e8290883cb71541af24d89792bc304405d65846970a21c8326de3cd3`。173 個 artifact 成員與完整檔案集合均獨立核對相符。

精確 AST 與 diff 僅 package_inventory 有新增；所有舊 statements 保留，其他函式未變。新增 manifest 本身透過原 add 收錄，因此仍受原 path、reserved target、collision、no_links、historical pins 約束。四身份後置檢查要求精確 runtime target/source、canonical hash 與實體 pin。authority、completed_context、RG1 evidence closure、generate/archive_tools 均未改。

十個本 reviewer 自建有限 synthetic 案例通過：manifest 正向收錄且原列不變 1；product/audio 有實體卻從 inventory 遺漏 2；四種錯 canonical identity 4；缺 canonical manifest identity 1；manifest 實體缺檔 1；casefold manifest collision 1。九個預期拒絕。測試資料不是 canonical，不執行產品。

原封存 completed_context 的真實唯讀驗證通過，完整沿用 authority、180 source、690 historical evidence pins 與原依賴，沒有 monkeypatch、假 receipt 或 root rebinding。原／候選 inventory 實測 2289→2290：全部原列不變，只加 runtime product manifest 7955 bytes、SHA-256 `3b3b5557372f325aeecbb30065ed389216e932478e34b5763a4beda031bf8c9d`，來源為 engine 同名檔案。候選全部 files 與精確已接受 PP2 相同。

未執行舊 RG1 56 案例、canonical 4521 assertions 或遊戲套件。未呼叫 generate/archive_tools/package_release；沒有正式 plan、release、receipt、共享安裝或入口寫入。指定原輸入 pins、舊 review、入口與 project-manifest 前後相同。正式入口保持 r33。這不代表遊戲產品新增驗收或正式工具安裝。

## 命令證據與重驗

commands/baseline-01 保留初次 reviewer assertion 失敗：它錯把補單新增 provenance metadata 視為違規。baseline-02 修正為核對所有原欄位不變且唯一新增 planCorrection，exit 0。final-01 執行上述十案例與真 inventory 比較，exit 0；finalize-01 核對完整集合及產生交付。各命令 argv/cwd、stdout/stderr 原始 bytes、SHA 與真實 exit 保存在 commands/<名稱>/receipt.json。最早探索讀取的紀錄保留於 conversation tool outputs，review-scope.json 有明示，沒有捏造完整 raw logs。

下列命令唯讀，不建立 pycache、不修改封存、不執行產品：

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P35-release-generator-rg2-review-r1\verify_delivery.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P35-release-generator-rg2-review-r1\verify_delivery.py" --cases
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P35-release-generator-rg2-review-r1\verify_delivery.py" --inventory
```

若真實輸入之後漂移，inventory 重驗應拒絕，不可重算既有 pins 消除錯誤。原一次性 inspect_baseline/run_review/finalize 命令不應在封存 review 根重跑；readonly verifier 使用已封存 fixtures 重現相同案例。source-seal 封存 reviewer 程式與精確輸入/差異；artifact-manifest 封存其餘完整交付及 fixtures，只排除自身及 artifact-seal。封存在 finalize command stdout/stderr/exit 落盤後建立。

平台歷史 Goal 實讀 blocked；工具無 resume API，沒有新建替代 Goal 或修改平台狀態。本次依新派工完成 P35，未把歷史 Goal 冒稱完成。

保留原拒絕：ENGINE-R34-ENTRY-ACTIVATE-P26-R1 的 send_message_to_thread 被 automatic approval review denied，未執行未重試。原因原文：「此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。」本輪不處理此批准、不改道。
