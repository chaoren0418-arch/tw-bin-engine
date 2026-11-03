# P38 隔離交付準備：待獨立計畫覆核

本輪只寫 `work/P38-delivery-plan-r1`，沒有建立候選／執行根、build或執行launcher。真Goal讀回null，未改平台儲存、模型或新增代理／聊天。唯一主管為01a185b4-ecf6-705c-90e6-83b9eb1ba02f，唯一候選materialization整合者為01a17a22-17e4-70c0-9792-166ddb86ea42。

已核對P36/P37最終接受、原launcher source及既有正常manifest。有限契約位於 `plan.json`／`execution-contract.json`；`materialization.json`、`asset-materialization.json` 固定實際來源、目標、size/SHA及物理身份；`runtime-closure.json` 記錄執行所需集合與外部runtime條件。

## 有限交付內容

- 未來候選根固定 `work/P38-delivery-candidate-r1`，證據根固定 `work/P38-delivery-execution-r1`；目前均不存在。
- 218個獨立copy列：181 source/audio/web（含30 web）、15個P36原樣輸出、assets索引、新正常manifest、2個launcher source、3個重建支援檔、DELIVERY說明、14個真實來源／接受／build證據。每份獨立copy要求nlink1。
- 13,957個素材body：engine/assets junction已確認指向 `releases/r28/assets`；實體集合、大小、volume/fileId/mtime已核對，SHA期待值來自已pin的integrity索引。未對body重算SHA。候選只能建立相同實體的硬連結，所有目錄不得是junction/symlink。
- **不得修改素材內容、ReadOnly、ACL、mtime或其他共享屬性**；不可把設定ReadOnly當成保護措施。新連結只允許link count變化，原內容保持不變。獨立final reviewer對候選body完整hash一次即可，作者不再重做14k body hash。
- 未來再新增5個launcher編譯輸出及 `_delivery/identities.json`，完整候選白名單預計14,181檔。未知launcher size/SHA在契約中保持null，build成功後才記錄。

新manifest為 `build/product-p36-delivery-manifest.json`，SHA `eb7bd750b534ed3190cb1b332bd3f0356721b85fbbec08f179ec0d407bdc56d2`。它使用既有 `p24-normal-product-v1`，精確列15個P36 outputs＋30 web＋assets/integrity.json，共46列；profile、port42061、autoLogin=false與唯一`--normal-product`參數不變。P36原provenance manifest另外保存在候選evidence，不冒充正常manifest。

launcher由原r34 source獨立copy。`launcher.patch`與`launcher-delta.json`顯示唯一四類literal重綁：manifest hash、manifest path、3處P36產品路徑、5處候選版本／訊息標籤。反向literal正規化後整份Program.cs與原件相同，Launcher.csproj未變。原Verify、拒絕與check-only先於唯一Process.Start返回的控制流程保留，沒有擴參數或降低檢查。

## 未來執行只有三個命令

所有argv/cwd/timeout與environment在execution-contract中列全，executor的execute入口內先驗exact plan/contract、全部inputDocuments/script pins、新review與主管receipt，才建立fresh roots。

1. 一次launcher-only dotnet build：source為execution根的獨立launcher-source，輸出execution/launcher-build，private obj/cache/temp均在execution根。SDK10.0.300、明確offline NuGet、ImportDirectoryBuildProps/Targets=false、audit=false、UseSharedCompilation=false、nr:false；保存raw stdout/stderr、實際exit及binlog。要求0 warnings/errors、精確5個輸出，禁止產品重編譯。
2. 一次候選 `launcher/P24.Launcher.exe --check-only --root <候選絕對根>`：要求exit0、revision=p38-candidate、正確manifest/entry、46檔hash、13,957檔presence/size、x64 .NET10 Core/Desktop與非空WebView2版本、ui/network/services均false。只允許WebView2 loader的已安裝版本唯讀查詢，不啟動browser。
3. 一次同一新launcher `--check-only --root <releases/r34絕對根>`：新manifest在原r34不存在，應exit1且輸出缺manifest拒絕，無stdout正常收據，不到產品啟動分支。通用owned-job紀錄的completed可能因exit1為false；這是契約指定負向退出碼，不應誤當重試理由。

首次子程序即CERTIFICATE=false、ADD_GLOBAL_TOOLS_TO_PATH=false、telemetryoff、固定PROGRAMFILES(X86)；HOME/USERPROFILE/CODEX_HOME原值傳遞、不改、不記錄敏感值。父程序和系統環境不變。使用原樣owned Windows job helper，僅處理所建立的子程序，不觸碰其他程序。

任何materialization/build/check失敗即保留partial候選、links、logs與輸出並停寫；不清理、不刪除、不重試、不改expected、不裝runtime，也不回退到舊launcher。候選缺runtime只能回報。原件從未被替換，因此無共享還原操作；partial現場供新精確派工處理。

## 來源、重建與驗收界線

候選保留181sources、global.json、offline NuGet.Config/vendor及原始P36 compiler/binlog/source/dependency pins。`prepared/DELIVERY.md`說明完整資格與未來重建條件；舊r34 build-product.ps1不攜入，不能用它冒稱P36重建。歷史P36 contract中的絕對路徑只作證據；任何日後產品重建都須另有fresh-root精確派工，本輪禁止。

r33是正式入口；r34是舊封存baseline pickup產品，DLL c11678249221475fb749de41713f8ed75473980d8929d72a8cd04f2ca0b49aed；P36為已接受新增41組（含control42，speed0/1、Brave0）的DLL6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3；P38僅將P36形成隔離交付及新launcher資格。批准舊r34入口不等於啟用P36/P38，詳見主管獨立copy `version-boundaries.json`。

本輪只取得靜態literal與read-only pin/metadata準備結果，沒有新產品／launcher執行、正常啟動、UI/native、登入、服務、DB/SQL、socket/network、角色、憑證或完整regression驗收。未重跑4521、16負向或任何遊戲套件。未執行舊r34 completed_context，其expected-refusal資格與舊pins保持原狀。

待獨立plan review（schema `p38-delivery-plan-review-v1`、decision `accepted-for-p38-delivery-plan`）後，主管須另發 `P38-delivery-execution-acceptance-r1.json`，綁定本plan/contract/source/artifact seals及review pin、固定兩個根、1 launcher build、2 check-only、0 product build、禁止normal/shared/entry。`pending-acceptance.json`只是欄位需求，不是真收據。

## 證據與唯讀驗證

commands/prepare、assemble、validate_plan、finalize記錄實際argv/cwd/exit與原始stdout/stderr。只有一次唯讀計畫預檢，沒有新測試harness或案例矩陣。artifact manifest/seal涵蓋所有交付檔，僅外層兩個seal檔自排除以免循環。封存後停止寫入，交一次精確pins給主管覆核。

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P38-delivery-plan-r1\verify_delivery.py'
```

此命令只驗已保存的交付，不能執行未授權的build/check-only；準備腳本是一次性，不要重跑。

原ENGINE-R34-ENTRY-ACTIVATE-P26-R1的send_message_to_thread automatic approval review denied未執行、未重試或改道，原文：

> 此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。
