# P30 單次隔離product/audio build：失敗封存交接

**已依exact收據執行一次並停止。** executor exit2，內層dotnet build exit1；非timeout／非中斷，owned job processTreeEnded=true。原future-stage-01與所有失敗證據保留，沒有重試、改expected、改executor／planning或繼續44步。

授權dispatch ENGINE-P30-PRODUCT-ONLY-EXECUTE-R1。收據SHA `240ce19f1cd8f10bddff4a34366eacef31598d36b7bce0130a0cb83a431fe541`；contract SHA `37af732d7d30629d71cd3fb8e66bc18d77352bbef2ff3191de920743ced4a2d9`。完整外層argv/cwd/raw/exit在executor.command.json與executor.stdout/stderr；單次內層命令、原始diagnostic與binlog在stage-evidence/attempts/product/build-01。

## 真實結果

- 新dotnet build **1次嘗試、0次成功**；runtime／helper／launcher／entry-check／候選斷言均0。沒有把P28/P29或歷史斷言加進本回。
- final MSBuild summary：**Build FAILED，0 Warning(s)，2 Error(s)**。完整末段在final-msbuild-summary.txt，原始stdout共1561330 bytes，SHA `905c9bd7a1c08a0ddf2f35ee966b0304471feef076f5627118b2e78a4645b35f`。
- 錯誤在Restore的_GetRestoreSettings：`NuGet.Configuration.ConfigurationDefaults`型別初始化失敗，內層`ArgumentNullException (path1)`；stack顯示`Path.Combine`→`NuGetEnvironment.CalculateFolderPath`→`ConfigurationDefaults.InitializeInstance`。source與audio各報一次。沒有進入實際Csc task，沒有P11或P24.Audio DLL產物，故**actual identity比對未達成**，不宣稱重建成功或產品功能缺陷。
- 具體是哪個路徑／環境值缺少，尚未由本次證據確定；本回沒有修改環境、反覆啟動restore或診斷probe。交主管根據保存的environment、stack及binlog覆核。
- 184個獨立複本、完整181來源已事後重驗，st_nlink=1且不同samefile。executor事後核4350相依pins；outputFiles與generatedIntermediateFiles均0。既有planning581檔、executor62檔再次逐SHA吻合；protected入口／project-manifest不變，正式仍r33。

## 失敗證據封存

stage-files-seal.json在本execution目錄列future-stage所有 **202檔**及SHA；沒有為報告再寫入stage。必要13檔raw/command/environment/materialized/post-run/result/binlog另以獨立副本放stage-evidence並列copy-pins。

binlog：61492 bytes，SHA `90f866f0c82523d47e5a083789bfcac6e4bfc610cef5eb223152afff2ebaba5f`。

stage result SHA `c5fab50f21132bfeacfc89677f0748e1a859fd36dbdd835711e496786bf19ebb`。

外層stdout SHA `e31308cea1b417ec4d814ed6e2bb8b08e2b33249e6eec814809e812541876dd7`；外層stderr為0 bytes。內層stderr保留.NET first-run banner原始497 bytes，並非另一次build。所有raw顯示decode replacementCount=0。

本次來源與stage內容只讀封存；既有run不應重跑。後續若需修正executor環境及fresh root重試，須主管另給精確任務與收據，先保留此次失敗及原封存。正式入口／shared engine／releases／DB／服務／原生／GUI均未變更。Goal實讀null，沒有建立或修改。
