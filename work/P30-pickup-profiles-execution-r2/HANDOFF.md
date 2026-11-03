# P30 fresh02 product/audio 單次隔離重建成功

**單次dotnet build成功，actual product/audio SHA與原核准candidate完全一致。** executor及內層dotnet均exit0，owned job已結束；0 warnings／0 errors。這是product-only里程碑，runtime／helper／launcher／入口check／44步回歸均0；沒有增加P28/P29歷史斷言計數。

dispatch ENGINE-P30-PRODUCT-ONLY-EXECUTE-R2；新receipt SHA `c6d74ab2020063d2ea432ca35efb3b5ebf42ce79c0296cc7bc050b363ecd2707`，contract SHA `3f4bf1a3e6ec5f9db90ed8583f14d0f53869ad0c2e98ad3fc20ba6427d86b75d`。完整外層argv/cwd/UTC/exit/raw在executor.command.json；內層完整command/environment/raw/binlog在stage-evidence/attempts/product/build-01。沒有第二次invocation或自行重試。

## Actual identities

|產物|bytes|SHA-256|
|---|---:|---|
|P11.ClientEngine.dll|4925440|6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3|
|P24.Audio.dll|53760|d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4|

181來源完整核對，184個materialized檔均獨立byte copy、st_nlink=1且與來源不同samefile。executor事後核4350相依pins；15個產品輸出與38個obj生成檔已核SHA。output-dependencies.json記錄實際deps libraries與全部輸出，7個managed/native DLL與封存P28對應輸出逐byte SHA一致。

## 真正的compiler與raw證據

diagnostic stdout共16291322 bytes，SHA `3f8a2b32d7e4e793d00b1a6d7143cafcef3f5302f282c3cc578faf5dadbe8d48`。binlog共212558 bytes，SHA `3d547e62e5f8aeb3dcebbc3dafb056eb301e1bb5dca5b5d1a9693161e53b166c`。完整MSBuild最終Project/Target/Task Performance與Build succeeded／0 Warning(s)／0 Error(s)保存在final-compiler-summary.txt。

實際Csc task共2筆：TaskId 167與231，分別完整保存開始至Done executing區塊，compiler-provenance.json連回原stdout的1-based行號與hash。這些是本次新編譯證據，不是歷史成功報告。stderr保留.NET SDK first-run banner497 bytes，非額外build；所有raw decoder replacementCount為0。

stage-files-seal.json封存fresh02全部347檔（含來源、build、obj、環境／restore產物與原始log），seal寫在本execution-r2，沒有另外修改stage。必要17檔證據獨立複本與copy pins另存；產品binary保持原stage。

## 保留與範圍

r1 failed stage01全部202檔保留不變；原planning581、executor-r1 62、executor-r2 37檔逐SHA核對不變。protected入口／project-manifest維持r33，沒有shared engine／releases／入口切換、DB／服務／原生／GUI／候選runtime操作。實際env只依已核准r2固定PROGRAMFILES(X86)；沒有再擴大parent env或調整expected。

本回新build=1成功1、runtime=0、新runtime assertions=0。Goal實讀null，未建立或修改。等待主管／P29對本次來源→實際編譯→DLL與binlog封存做獨立驗收；不代表後續完整回歸、native或全案完成。
