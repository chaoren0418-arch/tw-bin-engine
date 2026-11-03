# P36 canonical 單次執行交接

依主管 `ENGINE-P36-SERIAL-SOURCE-BUILD-R2-01`，唯一整合者 `01a17a22-17e4-70c0-9792-166ddb86ea42` 已執行已接受 r2 executor 的一次 `execute`。執行前讀回真 Goal=null，未建立／修改；核對7個 exact dispatch pins，確認 execution root、P36 build root、P36 product manifest 均不存在，才執行。沒有重試。

2026-10-29 16:57–16:58（Asia/Taipei）執行完成。實際 executor 退出碼0；單次 dotnet build 退出碼0、0 warnings、0 errors，owned process tree 正常結束。`result.json` 為 executor 原始結果，SHA `a86b0e4ea202d4dc02c71aec25a7c97e2609874a7b20662d839c0971509f0a8e`，交接整理未改寫此檔。

## 實際結果

- canonical source/audio/web 現有181檔，精確完成4替換＋1新增。全部 after pins、nlink與完整檔案集合已核對，四個歷史原件獨立保存在 `backup/source`，replace 前後日誌在 `journal`。未執行 rollback。
- build 真實輸入為 canonical `engine/source/OfflineClient.csproj`，audio 由原 ProjectReference 建置；私有 obj/temp/NuGet/cache 位於本 execution root。沒有將 accepted DLL 複製為新 build 結果。
- 新輸出 `engine/build/client-product-p36-r1` 共15檔，全部 size/SHA 與 P30 accepted stage 參照相同，見 `output-stage-comparison.json`。
- product DLL：`6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`，4,925,440 bytes。
- audio DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`，53,760 bytes。
- 新 product manifest：`engine/build/product-p36-r1-manifest.json`，SHA `d55389e048aee9eb2b64088ddfb0128de6166071e21969bd1dd57b9464d9ce75`。
- source manifest：SHA `f3275b4f202ee0d277e5749e85e2e10297396ce8f9bafa70696509a91027f95d`，181檔。

功能資格沿用已接受候選：額外41 exact own-pickup profiles、speed0/1、Brave0。這次只新增 canonical source-to-build 證據；相同DLL既有 runtime 證據不計新，未重跑遊戲套件或新增 runtime assertions。

## 命令與可追溯證據

主管收據：`主管/P25-P27-新主管接管-20261029/P36-execution-acceptance-r1.json`，SHA `4d5a0d73650ddc9bd6d9f121feb3f55fb084b742e45eb26a96430cea540e4032`。

r2 plan SHA `f67fa56815fbc133c1710c450efe1305d58da7c75566e02a9d909a24ca6059bd`；contract SHA `f439915614397edb8a478196375706aec1af68b670bd06c310d1ba3e251e8c30`。author seal `d72f2c75fc792df44a5e035651a82e5cb2244d944d42564e2231c7e15a1a30c7`；review seal `8cdfba1534056ca45d6b8fa36bb69c142051035dfea3e9be21cab1d522305bdb`。原 plan/review 封存全部成員已唯讀重驗，不再寫入。

實際外層 argv/工具完成退出資料在 `outer-invocation.json`，標明是工具轉錄，不冒稱另有 raw pipe capture。真正 build 的 argv/cwd/exit、原始 stdout/stderr、started/assigned/resumed、environment spec 和 binlog 在 `attempt/build-01`。

`compiler-command-index.json` 保存 raw stdout 的兩個 Csc task 起點與兩條原始 compiler 命令解碼切片；這是實際 log 索引，不代替獨立 binlog provenance 審查。binlog SHA `ddcc351fc4b28c936dd767ecb7af76245caf903f7e6d361292d794c948ebb882`，216,085 bytes。獨立覆核者應核對兩個編譯工作的 canonical project、實際 source/resource、引用相依、private obj PathMap、輸出與同一 source181 身份。

`evidence/tooling`、`evidence/authority` 保存 pin 完整的獨立工具／契約／收據副本；`evidence-copy-pins.json` 記錄來源與副本。這些是執行證據，不是正式 release tool archive 或 P37 inventory 執行。`commands/finalize-evidence` 保存交接整理命令的實際退出碼及原始輸出；此命令不執行 build 或產品。

`preservation.json` 回讀全部 source after 與 protected pins；`external-material-seal.json` 固定181個 canonical source、15個輸出及新 manifest。artifact manifest/seal 封存本 execution root 的全部檔案（含原始 log、obj/cache與備份），僅排除兩個外層 seal 檔本身，避免循環。

可唯讀重驗，不重跑 executor：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P36-canonical-integration-execution-r1\verify_delivery.py'
```

## 保護條件與尚待接受

root 入口仍指 r33；`啟動引擎.cmd` SHA `f189ee18ac2f3881f26246cb724772ac97ba11b977f40d74b0d9c1195795c27e`，`project-manifest.json` SHA `b61690a3a9269fd64b159ce32f1d686228fc26302f46bcec045e6aab551ceada` 均未變。原 r34 build/product manifest/launcher 與 protected r33/r34 release material 不變，assets junction target 不變。

首次 build 子程序即使用 certificate/global-tools-PATH 關閉、telemetry off、offline NuGet、NuGetAudit=false、UseSharedCompilation=false、nr:false、固定 PROGRAMFILES(X86)。HOME/USERPROFILE/CODEX_HOME 原值傳遞，不修改父程序或系統環境，保護值不另列出。沒有 DB/SQL/服務/native/GUI/遊戲/socket/憑證操作或永久刪除。

**此交付尚待獨立 execution provenance 覆核及主管 P36 final acceptance。P37 inventory 完全未執行。** 後續仍須另有 P36 final receipt 與 P37 authority，不能以這份交接冒充正式接受。沒有 release 生成、launcher 重建或入口啟用。舊 r34 completed_context 在 canonical source 更新後預期拒絕；本輪未執行它，也未修改舊 pins 使其通過。

原 ENGINE-R34-ENTRY-ACTIVATE-P26-R1 自動核准拒絕保留，未重試／改道：

> 此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。

交接封存後停寫；原結果、raw logs及成功輸出保留供獨立覆核。
