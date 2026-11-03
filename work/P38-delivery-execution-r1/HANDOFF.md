# P38 實際隔離交付與 launcher check-only 交接

依唯一主管 ENGINE-P38-DELIVERY-R1-01 與 exact receipt SHA `11788b37e42a85f25d4a9cd9d8839a897d42fd16c1f98ed6e0d7322e454cc334`，唯一整合者以原封存 `work/P38-delivery-plan-r1/executor.py execute` 執行一次。執行前6項dispatch pins與1944-byte收據核對相符，candidate/execution roots確認不存在。真Goal讀回null，未建立或修改；沒有重試。

2026-10-29 18:04–18:10（Asia/Taipei）executor成功完成，外層exit0。`result.json`為原始executor結果，SHA `37655945b75dc0f86b6fe4153ba17779a186df571726f4ce162dcd8ee1807a2f`，交接整理未改寫。候選為 `work/P38-delivery-candidate-r1`，執行證據為本目錄。

## 實際交付

- 完成218個獨立nlink1副本、13,957個immutable素材硬連結、5個新launcher輸出及1份實際identity紀錄，共14,181檔。
- 181份source/audio/web、15個P36產品輸出原樣保留，沒有產品重編譯。候選包含額外41 own-pickup profiles（含control42，speed0/1、Brave0）的P36 DLL。
- 正常產品manifest是 `build/product-p36-delivery-manifest.json`，精確46列：15產品輸出＋30 web＋素材索引；不是P36 provenance manifest。
- 完成一次launcher build：exit0、0 warnings、0 errors，一個Csc編譯工作，真實binlog已保存。私有source/obj/temp/cache均在本execution root；未改plan source或共享engine。
- 候選check-only執行一次，exit0；確認revision p38-candidate、manifest/entry、46檔hash、13,957素材presence/size、.NET Core/Desktop 10.0.8 x64及WebView2 156.0.4314.50。回報ui/network/services/autoLogin均false。receipt中的arguments=[--normal-product]描述產品契約，實際命令為--check-only，沒有執行normal branch。
- 同一新launcher對immutable releases/r34執行一次check-only，exit1，stderr為「啟動檢查失敗：缺少 p38-candidate 產品 manifest；請使用完整交付目錄。」stdout為空，符合版本隔離拒絕。owned-job通用completed=false是expected exit1，不代表要重試。

四項實際identity：

| 身份 | SHA-256 |
|---|---|
| P36 product DLL | `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3` |
| audio DLL | `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4` |
| P38 normal manifest | `eb7bd750b534ed3190cb1b332bd3f0356721b85fbbec08f179ec0d407bdc56d2` |
| 新launcher DLL | `44361072a33d572e155d80af41bf6245cf80fe8770eb1b80bdae0958540053b6` |

## 證據與素材review界線

每個實際build/check命令的argv、cwd、exit、expectedExit、raw stdout/stderr、assigned/resumed與owned process-tree結束狀態在 `commands/<id>`；launcher binlog SHA `f1879400302f9f34a163e5dd806aef40b21cc6fc940c9572d8e64d069ee3d785`。`compiler-command-index.json`是原始compiler命令切片，供獨立source→launcher provenance審查。`outer-invocation.json`是工具完成轉錄，未冒稱另一份raw pipe capture。

`materialized-copies.json`記錄218份source/copy pins；`launcher-output-pins.json`記錄5份真實編譯輸出；`candidate-file-seal.json` SHA `3470b0ff44b49ae08c4751efe14ea2b8bd983437a8111c0512dc6c799d71e683`，記錄224份獨立檔案實際SHA並綁定素材metadata plan。`materialized-asset-identities.json`記錄全部13,957素材的物理身份、來源與expected SHA。

素材SHA期待值來自已pin的integrity索引，**作者未重新hash body**。executor實際確認samefile、volume/fileId/size/mtime及完整candidate集合；完整body內容SHA仍待獨立final reviewer在候選驗一次。新硬連結只增加link count；沒有內容、ReadOnly、ACL、mtime或其他屬性寫入。素材目錄不是junction；原engine/assets junction仍指原r28實體素材。

`preservation.json`回讀原plan/contract/source seal、33項protected pins及候選非素材證據。root入口和project-manifest仍r33、原engine/releases內容不變。首次子程序即certificate/global-tools PATH變更關閉、telemetryoff、offline NuGet、auditfalse/sharedcompilationfalse/nr:false；HOME/USERPROFILE/CODEX_HOME原值傳遞，未改父程序或系統環境。

沒有產品build、正常/noargs產品啟動、UI/native遊戲／角色、DB/SQL/service/socket/network、憑證操作、發布或永久刪除。WebView2只查installed版本，未啟動browser。沒有重跑已完成遊戲／4521／負向套件。r34舊context保留expected-refusal文字資格，未執行或改pins。

## 接回驗證

執行證據artifact manifest/seal封存本execution root全部檔案，外層兩個seal自排除避免循環；candidate保持停寫。下列只讀取封存與224個非asset-body實際pins，**不重新執行launcher、不hash13,957 body**：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P38-delivery-execution-r1\verify_delivery.py'
```

獨立final reviewer仍需驗候選所有素材body、candidate closure、launcher compiler provenance、兩個check-only raw結果與保護pins，然後主管才可給有限離線交付接受。**本次成功不是完整native、正常啟動或full-release接受，也不啟用正式入口。** r33是現行入口；舊r34不包含P36額外41組，批准舊r34也不等於啟用本候選。

原ENGINE-R34-ENTRY-ACTIVATE-P26-R1自動核准拒絕保留，未執行、未重試或改道：

> 此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。

此交付封存後停止所有候選／執行根寫入，等待獨立最終覆核。
