# P30 product-only executor 準備交付

**最小executor已實作，唯讀／有限合成驗證通過；本回未執行產品build或候選runtime。** 正式入口r33；future-stage-01仍不存在；既有planning581 artifacts再次逐SHA核對保持不變。本回只寫work/P30-pickup-profiles-executor-r1。

唯一主管01a185b4-ecf6-705c-90e6-83b9eb1ba02f；唯一整合者01a17a22-17e4-70c0-9792-166ddb86ea42。派工ENGINE-P30-PRODUCT-EXECUTOR-PREP-R1，SHA `4be1242a443975632a8f13ab23c528aec6eb2f536b144647e579175dcbb80568`。Goal實讀null，未建立／修改；未新增代理／對話。

## Exact pins

|檔案|SHA-256|
|---|---|
|execution-contract.json|37af732d7d30629d71cd3fb8e66bc18d77352bbef2ff3191de920743ced4a2d9|
|executor.py|4bd924cfa769d6ec4146ff0086e444d79f00e4dd3ed37b2b5a827ff457b8e6d0|
|gate.py|f4b14f7cb136c629b93f90d7807bf61bc9c95fbcfe66bf206b7d92079e8326b9|
|win_process.py|37f21ac740a11f780a937f085598bbb3dbbe928a65fa8d19d6c6d0f39e0ef0dc|
|product-materialization.json|63290a5c650dd992172d605210ca882b8212e1622f98ec000310f68e590c4f33|
|build-dependency-pins.json|58fd3d9a4de20417a2336c00f99494a7e4874352c470676cdbdfefa3763b116b|
|invocation-schema.json|45914acba14320056e31661cc557bf5912788afefc04b15915c8403902028391|

## 實作與邊界

`validate`預設唯讀，驗exact派工、planning封存與2635 input pins、184列獨立複製清單、4350工具鏈相依pins、來源／root／環境／祖先policy及runtime scripts。`execute`的mutation函式內再次要求完整validate及外部receipt，不依呼叫者傳入已驗證旗標。

只獨立複製184檔＝181 source/audio/web＋global.json＋NuGet.Config＋1 vendor套件；拒絕candidate DLL/EXE/PDB作為產品輸入。所有目的地需containment、無reparse、不得既存；copy以xb建立、fsync、SHA與st_nlink=1及不同samefile證明。任何既存future root均拒絕，不支援resume或auto retry。

組建前建立exclusive started.json及command-started.json。dotnet採CREATE_SUSPENDED，先配置獨立Windows Job並完成Assign，再resume其唯一主執行緒；hidden、stdin DEVNULL、無shell。Job不允許breakaway，KILL_ON_JOB_CLOSE；timeout600或中斷只終止本次owned job，不停止未知程序／服務。正常exit後最多2秒等待job accounting收斂，仍有child即標記失敗並清理；completed條件仍要求exit0、非timeout／非殘留、整個job已結束。

stdout／stderr直接stream成原始bytes，保留SHA／size／UTF-8-sig display replacement count；保留實際MSBuild binlog、diagnostic stdout供內部compiler/task provenance。成功後核對184輸入／完整181來源集合、工具相依與protected pins，封存所有product輸出及source/audio obj產物。實際P11 SHA必須 `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`；Audio必須 `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`，失配留存identities且停止，不改expected。MSBuild必須0 warnings／0 errors。

環境採allowlist重新建立，而不是只刪幾個已知變數；discarded僅記錄變數名稱，不記秘密值。PATH固定dotnet與Windows，所有cache/tmp/user profile/APPDATA都在future root，DOTNET_CLI_UI_LANGUAGE=en-US。祖先Directory.Build.props/targets/rsp、Directory.Packages.props、NuGet/global及MSBuild.rsp在preflight核對；本次祖先清單為空，新增或改動即拒絕。完整SDK10.0.300、host/fxr與.NET runtime檔亦納入pins。沒有dotnet --version、restore、build或任何product入口在本回執行。

不實作44步通用executor；不執行helper/runtime、音訊裝置、launcher/check-only、DB／服務／網路／GUI／原生、release或shared engine寫入。assets無須複製或連結。

## 唯讀與有限驗證證據

修正版 **53 checks通過**，包含真實CLI readonly validate與execute缺receipt拒絕、錯權威/plan/materialization/scriptPins/scope、越界／ADS／來源pin／既存copy拒絕、環境注入移除、獨立copy，以及4個小型Python合成程序（成功原始非法UTF-8 bytes、exit7、timeout含一個自建child、中斷）。不是產品測試；所有合成job結束。

首次synthetic-success的exit0與treeEnded=true被原runner誤記lingeringChildFailure；原因是proc.wait返回後Job accounting還沒歸零。validation原失敗raw／command／original-source皆保留；job-accounting-fix.patch只有正常exit後有上限的收尾等待，timeout仍立即處理，沒有降低completed或改預期。validation-r2為修正後新目錄。合成root程序共5次（含原失敗1次），另1個合成child；產品build=0、候選runtime=0。程序測試不等於真實MSBuild驗收，真實建置仍待下一exact授權。

## 收據與未來唯一命令

需要主管在 `D:\lineage開發\TW1608262200研究\主管\P25-P27-新主管接管-20261029\P30-product-only-execution-acceptance.json` 提供新收據，並從外部傳入其expected SHA。`invocation-schema.json`只是schema/required constants與命令模板，**不是acceptance receipt**；本回沒有建立任何acceptance receipt。需包含exact contract/materialization/planningPlan SHA、三個script pins、supervisor／owner、authorizedOutput、maxBuildInvocations=1及無shared/release/entry/runtime權限。P29覆核後由主管決定是否接受本contract。

唯讀重驗：

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' '-B' '-X' 'utf8' 'D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-executor-r1\executor.py' 'validate' '--contract-sha256' '37af732d7d30629d71cd3fb8e66bc18d77352bbef2ff3191de920743ced4a2d9'
```

待新receipt後才可使用（SHA placeholder不可自行替換為讀到的檔案hash以冒充授權）：

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' '-B' '-X' 'utf8' 'D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-executor-r1\executor.py' 'execute' '--contract-sha256' '37af732d7d30629d71cd3fb8e66bc18d77352bbef2ff3191de920743ced4a2d9' '--receipt' 'D:\lineage開發\TW1608262200研究\主管\P25-P27-新主管接管-20261029\P30-product-only-execution-acceptance.json' '--receipt-sha256' '<SUPERVISOR_SUPPLIED_EXACT_SHA>'
```

單次product build的完整argv（cwd=`D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-stage-r1\future-stage-01`）：

```json
[
  "C:\\Program Files\\dotnet\\dotnet.exe",
  "build",
  "D:\\lineage開發\\TW1608262200研究\\work\\P30-pickup-profiles-stage-r1\\future-stage-01\\source\\OfflineClient.csproj",
  "-o",
  "D:\\lineage開發\\TW1608262200研究\\work\\P30-pickup-profiles-stage-r1\\future-stage-01\\build\\client-product-r34",
  "-c",
  "Release",
  "--configfile",
  "D:\\lineage開發\\TW1608262200研究\\work\\P30-pickup-profiles-stage-r1\\future-stage-01\\NuGet.Config",
  "-p:NuGetAudit=false",
  "-p:UseSharedCompilation=false",
  "-nr:false",
  "--nologo",
  "-v:diag",
  "-bl:D:\\lineage開發\\TW1608262200研究\\work\\P30-pickup-profiles-stage-r1\\future-stage-01\\attempts\\product\\build-01\\msbuild.binlog;ProjectImports=None"
]
```

對planning原命令唯一新增兩個診斷參數：`-v:diag`、`-bl:.../msbuild.binlog;ProjectImports=None`。沒有更動功能性build property；新增環境白名單與記錄約束在contract明列供審核。

未來成功/失敗收據：future-stage-01/result.json；raw、command、binlog、post-run-pins、identities位於attempts/product/build-01。失敗會保存partial files及process outcome、停下並拒絕既存root重用。若controller本身被強制終止，Job handle關閉會終止其members，started marker仍阻止自動重試，須先人工唯讀查核。沒有delete/rollback覆寫功能。

下一步：主管／P29對封存來源及本contract覆核，再發exact product-only receipt並安排單次隔離build。入口仍待使用者決定；這不是入口切換重試。RG2與後續迴歸／native缺口不在本executor工作內解決。
