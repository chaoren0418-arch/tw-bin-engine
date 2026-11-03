# P30 隔離 stage 計畫交付

狀態：**planning完成，待主管審查；未執行stage**。下一接受里程碑僅為181檔來源的fresh product/audio重建與exact identity比對。**product-only executor尚未實作**；目前 `stage_controller.py` 只有validate／commands／inspect-attempt唯讀能力，不能建置或重試。需後續另案實作與審查executor，再發exact product-only授權。

唯一主管01a185b4-ecf6-705c-90e6-83b9eb1ba02f；唯一整合者01a17a22-17e4-70c0-9792-166ddb86ea42。本派工沒有共享寫入權。正式入口及project-manifest維持r33；r34僅封存離線基準。未執行DB／服務／GUI／原生遊戲／網路操作，未新增代理或對話。

## Exact交付識別

|檔案|SHA-256|
|---|---|
|P30 dispatch|b7ef761dd585c5594c38d870fb111294d4ffa519c31b4aeff3e83876dc5bbed0|
|P28/P29 acceptance|2537af60910b67dd6803b1ec8f5252823288e65936cd06c57d6197c5f1e05dd3|
|stage-plan.json|f58e0d6400d1715ad03ef32fc26486ac89a9b915824bbd7470a4ca6dff4f19f3|
|stage_controller.py|165a7fc19d2a07bf44089986dc06b5f3523619172fb2ff3d0968418765b81cde|
|product-only-spec.json|96732bd48ea76890ce5bf82bf5613a7f13b16c882997ae2fdcded5f674de3086|
|materialization-plan.json|de67b00de533abb45e196f3c0e97d245c36ed1e9c7312e9b8badf72fdcc64050|

## 已完成的規劃與唯讀驗證

- exact180個r34 source/audio/web基準＋1新增，四個replacement；完整181檔中151為source/audio、30為web。四替換：ObjectAssets.cs、OfflineClient.csproj、PickupAssets.cs、PickupPresentation.cs；新增Resources/pickup-own-base15-profile-extension.json。完整來源／來源SHA／原件／獨立prepared副本均在source-inventory.json，180原件另保留reference/base-source。
- P28全1207 artifacts、P29 runtime全243 artifacts、獨立profile oracle全451 artifacts逐SHA驗證。最終input-pins共2635筆，copy-pins共539個未改動獨立副本；另有3個明示helper差異，其原件、before/after SHA與patch分開保存。
- 唯讀控制器完整驗證通過；19項規劃檢查含權威錯配、越界、錯SHA、重複歷史執行狀態等拒絕條件。raw argv/cwd/stdout/stderr/exit在validation/attempt-02。這19項不是產品runtime斷言。
- 首次path validation拒絕了歷史relative workflowRoot造成的重複絕對前綴；validation/attempt-01保留錯誤plan與結構化finding。原工具輸出沒有另存raw檔，報告明示此限制；修正後成功驗證的raw已保存。未啟動build/probe。
- 舊8步／44操作完整保留為後續相依清單；新P28五套profile suites及P29已修正依賴的獨立helper有具體argv。它們不是第一個product-only里程碑的執行前提。
- 舊guard gfx61/weapon4/weapon46已合法：提出gfx62/weapon1/weapon2的新負例ID與對照ledger；原三組正例由完整42-pair suite承接。兩份helper快照共6遷移列；取消gfx/weapon的方法與assertion布林式未改。第三差異只將ground checker從舊r34 SHA綁到已核准candidate SHA。全部待審、尚未編譯。
- coverage-matrix對P29全部14 families映射P28及r34歷史實際row IDs。包含舊三個S30 projectile vectors、synthetic occlusion、控制transport/quantity/lifecycle與fallback部分；均標記舊DLL／0/0／合成fixture限制，不能宣稱新181-source或42-pair已補測。完整composite、所有方向/生命週期、事件計數、resource fault seam及native仍有缺口。

## 下一個product-only命令（只是計畫，現在不要執行）

cwd：`D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-stage-r1\future-stage-01`

完整argv：

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
  "--nologo"
]
```

必要輸入：materialization-plan中phase=product-only的184檔＝181來源＋global.json＋NuGet.Config＋1個vendor套件。global鎖SDK10.0.300；NuGet只有本地vendor。build-dependencies包含實際dotnet/python/node、Roslyn、targeting packs與WebView2 1.0.4258.31套件SHA。source/audio的Release PathMap分別/_/p24/source及/_/p24/audio，Deterministic=true。需要的環境override/remove/inherit清單完整寫在product-only-spec.environment；所有obj/bin/temp/cache/APPDATA都指向fresh stage，預檢拒絕未pin的祖先Directory.Build配置。SDK/tool/來源只讀。

輸出DLL：`D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-stage-r1\future-stage-01\build\client-product-r34\P11.ClientEngine.dll`；expected SHA `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`。

輸出Audio：`D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-stage-r1\future-stage-01\build\client-product-r34\P24.Audio.dll`；expected SHA `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。

raw與失敗收據根：`D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-stage-r1\future-stage-01\attempts\product\build-01`。executor必須exclusive started receipt→單次dotnet build→原始stdout/stderr及nested MSBuild來源→exit/timeout/interrupt→來源/依賴/actual DLL雙SHA驗證→product-only結果。timeout600秒，所有失敗與不確定結果保留，不自動retry；先查自建attempt/process outcome。不能以重算expected替代identity不符。完整要求見EXECUTION-CONTRACT與product-only-spec.requiredReceipts。

這一步不需要assets連結、helper build、runtime probe、launcher、check-only、完整44操作或正式入口切換。future-stage-01現在不存在。歷史controller不能替代缺少的executor，也不能直接指向共享engine執行。

## 唯讀重驗

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P30-pickup-profiles-stage-r1\stage_controller.py' validate --plan-sha256 f58e0d6400d1715ad03ef32fc26486ac89a9b915824bbd7470a4ca6dff4f19f3
```

不要重跑prepare_plan.py/refine_plan.py/complete_plan.py/validate_planning.py/seal_delivery.py，它們是本交付的產生歷史；封存後只使用唯讀控制器。artifact-seal列本交付所有檔（自身除外），artifact-manifest明示排除自身與seal。

本次產品build=0、helper build=0、runtime probe=0、新runtime assertions=0、shared/release/entry寫入=0。Goal實讀null，沒有建立或修改。RG2一般generator漏product-manifest仍未修復；本次未準備或執行package。整個引擎／原生驗收未完成。
