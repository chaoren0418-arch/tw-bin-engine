# P30 隔離 stage 執行與回復契約（待審，不授權執行）

本派工只產生計畫與唯讀控制器。`stage_controller.py` 僅有 validate、commands、inspect-attempt；沒有 build、install、run、retry、collect-write 入口。reference/accepted-controller 是逐 byte 獨立複本，僅供覆核，不得直接執行，因其保留歷史 canonical 路徑與權威。

主管後續縮小第一個實際接受里程碑：只做181檔來源的fresh product/audio重建與exact identity比對，無須先重跑44操作或已通過的runtime套件。`product-only-spec.json` 與 `materialization-plan.json` 列最小argv／source／vendor／SDK／global／NuGet／environment／輸出與失敗收據。**可寫的product-only executor尚未實作**，須在後續任務實作、覆核並取得exact授權；本次只有唯讀planning controller。下文44操作、helper與asset條件均屬後續迴歸階段，不是product-only的前置執行條件。

新執行階段必須有主管新的 exact plan/controller/source/helper-diff 收據；只能指定本計畫的 fresh future-stage-01。本次不建立該目錄。181 個來源已在 prepared/source-tree 完成獨立副本；未來再次獨立複製，四替換的 180 基準原件保存在 reference/base-source。任何可寫目的地必須通過 resolved containment、無 reparse ancestor、st_nlink=1，拒絕覆蓋。

## 組建與實際 DLL 綁定

build-dependencies.json 記錄 SDK10.0.300、Roslyn、targeting packs、dotnet/python/node 與封存 vendor 的實際 SHA。新 stage 必須帶固定 global.json 與僅有本地 vendor 的 NuGet.Config。source/audio 的 Release PathMap 分別為 /_/p24/source、/_/p24/audio；Deterministic=true，保持 assembly name、target framework、references、resource logical names、generated AssemblyInfo 與編譯選項。

command-plan.json 的 product/rebuild 為完整 argv/cwd/timeout。它是直接 dotnet build，禁止沿用要求舊180來源及舊DLL的 canonical rebuild.py。只組建獨立 product/audio；不組建或啟動 launcher、入口、check-only。新 DLL 必須等於已核准 P28 的 6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3；audio 必須等於 d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4。

如果重建不等，立刻停下，保留 compile command、環境、compiler/SDK/refs/source/generated metadata、MVID、PDB與兩份 embedded resource 資料。即使差異看似僅路徑／metadata，也不能把 expected 改成 observed；須另行審查根因和 identity 決策。新 stage 的 runtime identity 尚不存在，不能用 candidate build 複本冒充新組建。

所有 helper 都從此新 DLL 編譯，不引用 source project 重建另一份產品。執行前確認輸出目錄中的 P11、P24.Audio、WebView2 Core/WinForms 等 copied dependency SHA 與 product 輸出相符；檢查 MSBuild 產出的 deps。P29 使用已修正 Private=true＋explicit Audio 的 project，不手寫 deps。執行時實際 Assembly.Location/SHA 必須入 raw result；禁止只寫預期 SHA。

## 順序、相依與斷言

舊八步為 product、baseline、ground、utility、haste、versioned-slow、f1、pickup；command-plan 保留44操作名稱、各內層 argv/cwd/probe 路徑，所有寫入重新指向 future-stage-01。archive/setup/collect 的目的及新 stage adapter 尚須依 harness-rebinding.json 逐項審查；不能直接啟動歷史 wrapper。

regression-inventory 保留歷史4521 assertions／93 observations，P28五套26953及baseline-control225另列，P29獨立5196另列；本次新執行全為0。原174+10拾取案例的三個舊拒絕向量現在合法，必須先審核 migration ledger、改用契約確定不允許的負例並加原向量正例；取消條件中的gfx/weapon變動仍應保留。不能只改assert expected、略過失敗、沿用歷史總數或把多family重複列加總。

P28 helper 的 `root` 參數使用本計畫根（仍為 project/work/child），其 helper-only AppContext 指向封存 r34 assets；P29 的 prep、assets 參數保持封存唯讀。一般舊 helper 依 stage/build/name/../../assets，因此未來 stage assets 使用已接受的13958 immutable hardlink策略並驗samefile；任何建立失敗即停，沒有全素材複製或junction fallback。禁止修改任何asset本文／屬性。

## Raw logs、timeout、interrupt與回復

每個外層操作在啟動前以 exclusive create 寫 started receipt，記錄 command ID、完整 argv/cwd、UTC、PID、parent PID、精確來源與組件pin、環境、600秒期限及預期輸出。外層、內層dotnet/helper、任何nested script都各自保存stdout/stderr原始bytes及command receipt；UTF-8-sig errors=replace僅供顯示，記錄replacement count、size、SHA，不修改raw。

exit非0、timeout、exception與interrupt一律保存部分probe、raw streams與started state。timeout receipt的exitCode=null、completed=false，記錄partial availability，不以空檔假稱沒有輸出。中斷後不能因缺final receipt就再次執行。先只讀檢查該attempt與其自建process tree是否終止、output與partial markers；不停止未知程序，不全域kill dotnet，不查DB/服務。

只在exact argv/cwd、實際DLL、每筆raw/nested log、probe集合及逐occurrence斷言全部驗證後標記completed；成功步驟不可重跑。單純collector失敗可提出新的、明確選定successful attempt pins的collect-only收據，由主管另行授權；不得透過collect-only重建或launch probe。產品／helper／probe需重試時須有根因、exact diff及新fresh attempt收據，舊失敗保留。不刪目錄、不覆寫、不從任意latest資料夾選證據。

`inspect-attempt` 僅讀取指定operation與record並檢查raw/probe pins，不寫入結果、不判定整個stage通過。未有真實成功證據前，所有stage completion欄位保持未執行。

## 邊界與未覆蓋

coverage-matrix.json 對P29全部14 families列歷史證據與待補。完整composite/occluder、真實S30 flight、完整lifecycle/transport/quantity/event counters、resource fault injection與方向1–7 active windows仍需新證據；既有P28部分證據只能引用，不能宣稱P30已補測。native same-version另案授權。

正式入口及project-manifest仍r33，沒有共享整合、release或入口切換權。本次不準備package execution；RG2一般generator漏product-manifest仍未修復，P26個別plan更正不等於generator修好。Goal實讀null，未建立或變更。
