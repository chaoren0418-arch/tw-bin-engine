# P30 executor r2：最小環境修復準備

**已完成獨立副本、最小diff與唯讀差異驗證；本回不build。** 唯一語意變更：固定加入 `PROGRAMFILES(X86)=C:\Program Files (x86)`。其餘parent environment仍依原allowlist丟棄，不新增PROGRAMDATA／PROGRAMFILES繼承、不加feed、不更改系統或SDK。

派工ENGINE-P30-ENVIRONMENT-REPAIR-R2，SHA `44751d44a1abff0aef270dce056082e8c37483fd2f08763f9358eb35d55dc30a`。唯一新寫入root為本executor-r2；future-stage-02未建立。主管01a185b4-ecf6-705c-90e6-83b9eb1ba02f，整合者01a17a22-17e4-70c0-9792-166ddb86ea42。Goal實讀null，未建立／修改；沒有新增代理／對話。

## 精確交付

|檔案|SHA-256|
|---|---|
|execution-contract.json|3f4bf1a3e6ec5f9db90ed8583f14d0f53869ad0c2e98ad3fc20ba6427d86b75d|
|gate.py|df9ca456744f8f33d50da3f4b48518342644e8e2cabdfa18864a9c3d223db8ab|
|executor.py（與r1逐byte相同）|4bd924cfa769d6ec4146ff0086e444d79f00e4dd3ed37b2b5a827ff457b8e6d0|
|win_process.py（與r1逐byte相同）|37f21ac740a11f780a937f085598bbb3dbbe928a65fa8d19d6c6d0f39e0ef0dc|
|product-materialization.json|b5895020b5416b704b46348a05818154deb4c67759c922e78af2a466149858da|
|build-dependency-pins.json（與r1逐byte相同）|58fd3d9a4de20417a2336c00f99494a7e4874352c470676cdbdfefa3763b116b|
|invocation-schema.json|48f5b368f5e195dfc379a084a42bdfb10886fbee3499f17f64b05730aabb7880|

## 變更與根因證據

gate-r1-to-r2.patch與source-diff-ledger.json逐項列exact before/after。8項機械替換：future02、r2 dispatch/sha、r2 receipt、派工immutable/failure/diagnostic pin欄位、184目的地、build argv與cache環境路徑；另1項固定PROGRAMFILES(X86)語意新增。runtime主executor與Windows Job controller無任何byte差異。工具鏈4350 pins內容不變；source/vendor pins、181來源與product/audio expected SHA不變。

P29單變因原raw已按本派工核SHA並複製reference/diagnosis：baseline失敗、PROGRAMDATA-only失敗、PROGRAMFILES(X86)-only初始化成功；installed NuGet IL顯示讀PROGRAMFILES(X86)並有PROGRAMFILES fallback。這些是P29歷史診斷證據，本回沒有重跑，也不把其成功當成產品build成功。

固定 `C:\Program Files (x86)` 目錄存在；`C:\Program Files (x86)\NuGet\NuGetDefaults.Config` **不存在**；`C:\Program Files (x86)\NuGet\Config` **不存在**，故沒有現存config檔可列內容SHA。nuget-system-path-state.json保留exact存在狀態；沒有dump設定／secret、建立系統目錄、改ACL或新增feed。下次若系統路徑狀態改變，應先核對此快照與新版收據適用性。

## 本回驗證範圍

**23項差異檢查通過**：只增加指定env值、parent同名值不能覆蓋固定值、沒有其他env繼承、184列destination-only映射與source pins不變、argv只改fresh root、diagnostic參數不變、三個unchanged檔比對、expected DLL不變、全部其他contract欄位無語意差異、拒絕r1 contract／failed root／old destination／old receipt、系統路徑狀態與new readonly validator。

真實唯讀CLI argv/cwd/exit0與raw stdout/stderr在validate.command.json／validate.stdout／validate.stderr。只執行一次新的readonly validator；原53項、Job合成程序、NuGet診斷與候選suite皆未重跑。product build=0，runtime=0，future-stage-02不存在。

獨立保存並再核：r1 executor62檔、原planning581檔、failed stage01全部202檔不變；正式入口與project-manifest仍r33。final-preservation.json列原seal及數量。舊failure沒有改成pass、沒有重用／刪除／覆寫。

## 審查後的下一步

新receipt必須放 `D:\lineage開發\TW1608262200研究\主管\P25-P27-新主管接管-20261029\P30-product-only-execution-acceptance-r2.json`，由主管提供外部exact SHA。invocation-schema只提供required constants／模板，不是acceptance receipt；本回未建立新receipt。expected product仍 `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`，audio仍 `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`；若實際重建不符，仍停止而不改expected。

唯讀重驗：

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' '-B' '-X' 'utf8' 'D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-executor-r2\executor.py' 'validate' '--contract-sha256' '3f4bf1a3e6ec5f9db90ed8583f14d0f53869ad0c2e98ad3fc20ba6427d86b75d'
```

待主管／P29接受最小diff並另給新receipt後，才能使用一次fresh retry模板：

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' '-B' '-X' 'utf8' 'D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-executor-r2\executor.py' 'execute' '--contract-sha256' '3f4bf1a3e6ec5f9db90ed8583f14d0f53869ad0c2e98ad3fc20ba6427d86b75d' '--receipt' 'D:\lineage開發\TW1608262200研究\主管\P25-P27-新主管接管-20261029\P30-product-only-execution-acceptance-r2.json' '--receipt-sha256' '<SUPERVISOR_SUPPLIED_EXACT_SHA>'
```

fresh root：`D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-stage-r1\future-stage-02`。184來源／配置／vendor獨立副本；單次dotnet build、raw/binlog、精確identity、失敗保存與existing-root拒絕、600秒timeout／owned job控制沿用r1。唯一新環境值與機械根路徑已明列contract供接受。沒有44步通用executor、shared/release／入口check、DB／服務／原生／GUI／network操作。實際MSBuild與product/audio同identity仍待下一次授權執行證明。
