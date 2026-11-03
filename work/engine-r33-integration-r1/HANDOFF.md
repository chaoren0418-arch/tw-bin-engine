# ENGINE-R33-INTEGRATION-PREP-R1

狀態 preparation-only。唯一寫入 work/engine-r33-integration-r1；candidateAccepted=false、groundLifecycleSuitePassed=false，沒有 build／stage／assets link/copy／shared merge／release／入口切換。Goal 實讀 null 未更改，未建立 chat／代理、未改模型或平台。交付後停寫，等待主管提供已封存 accepted ground candidate／獨立 formal review 及精確新派工；不是結束整個引擎工作。

已鎖 final r32 的177來源與26支援檔，共203 pins。原 stage baseline、final baseline、final-base-binding、主管最終接受與release seal皆保持原檔，按177個path/size/SHA與四身份確認等價。contract／receipt必須使用 final r32 baseline SHA，不能以原stage接受代替。

| 基底身份 | SHA256 |
|---|---|
| final r32 release seal | 46e9249224e768a1ac09fe99c08a9edf387833295022147aa046c206769fa507 |
| final r32 baseline | 0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7 |
| final-base-binding | 26cc55267f04896344333a6eda50d058d432aa4df1d2655875b6e9772eedff5e |
| 主管 r32 最終接受 | 5ecb6d4d9b09ce99febe4ba4a3e80c95fc57d740c01fbe25d3a7159fa35c6de9 |
| product DLL | c64bf0069722e28fc174d70d261b7540da6e3f74ee422d4874cbffb77149364f |
| audio DLL | d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4 |
| product manifest | 3bb6ee998c36084d420275e6b93d40346fe1fe0db5f285d3d22ad0789833b866 |
| launcher DLL | 042cf23807582c0b599103ffec8e8261b92e9e689e8328878124631aab17d3bb |

沿r32驗過的gate、151 baseline runner、rebuild overlay、schema及封裝來源準備r33工具，明確版本／schema／output為r33。原stage baseline來源是絕對路徑，現在依原逐檔路徑＋final SHA/size嚴格綁定；來源只可為canonical engine、sealed candidate或該精確original r32 stage，不能泛化為任意舊stage。外部唯讀evidence仍先驗SHA/size再contains分類，只有contains為真才relative_to，不跳過合法固定BIN/JAR，也不放寬candidate/output限制。將來保全被授權更新的shared兩檔，必須驗獨立backup舊SHA，而非誤稱現檔未變。

預設run_next_stage僅驗接受材料、不寫。未來--build必須新ENGINE-R33-INTEGRATE收據、四材料與prep seal、final基底、fresh未用output全部通過；本輪未呼叫--build。prepared/run_stage_r33.py直接執行先拒絕。canonical重建模板額外鎖精確canonical engine路徑，不只檢查目錄名字；sealed release不可build/verify產品，唯讀來源核對與verify_release全檔SHA分開。release-layout僅計畫，沒有發布功能。

76份獨立reference copies包括r32最終accepted harness、四套probe／occurrence／執行結果、utility warning／coverage、r31haste歷史映射、兩個oracle、兩個metadata vectors、r32已修runner與shared整合腳本。來源SHA與副本SHA列於reference-pins，不直接執行含r32身份的原runner。固定回歸為151＋431＋1707＋1201＋112＝3,602，另列12 NPC observations，作者377不加總；utility inherited CS8602一項保留，產品／launcher及其他helpers原0warning要求保持。13slow-start、24wrapper-owner、真TickCount64／100ms guard不放寬。紅水slow-item拒絕fixture保持，r31haste的820＋156＋577＋154映射不冒稱原976全未變。

ground formal suite/counts仍required且未知，未讀未封存候選／formal review，未造accepted樣本。預期僅Ground.cs／WorldEntry.cs正常Speed0/1肉箭單次pickup生命週期，保留900ms／3000ms／Slow gates／既有入口／封包／動畫與r32item防護；共用SendGround/ResetGroundSend自然保護既有手動drop，不新增drop或Slowpickup能力。最終來源數／變更集由接受manifest/review決定。

唯一引用的ground來源準備是sealed engine-slow-pickup-review-r1，seal 29fb10cb6919a9ea07c13185af3ffc0eab2b8be4944c18093cbf84cb00326095；r31的140rows＝61baselinepass＋24future（14met／10差異）＋55obs，絕不作新候選formal suite。詳細範圍、合法先到權威回包、late success/error/finally、公開新owner vs人工lease分類，見VALIDATION-PLAN。

14項小型gate/schema檢查一次通過，零失敗；另對新canonical精確路徑guard做4個AST expression案例，全部符合預期，未生成overlay或執行rebuild。schema使用本機jsonschema Draft202012Validator檢查；命令／stdout/stderr／結果在evidence。沒有產品建置、SDK或完整suite執行，未呼叫正常產品／GUI／socket／native／JAR／DB／SQL／服務。

可重跑的唯讀小檢查（不產生stage或產品）：

```powershell
python -B -X utf8 test_rejections.py
python -B -X utf8 test_canonical_guard.py
python -B -X utf8 verify_preparation.py --seal-sha256 <交付preparation-seal SHA>
```

run_checks.py與prepare.py是本次一次性產物生成入口，不在封存目錄重跑。preparation-seal固定tools／schemas／lock／plans／references／小型證據；result引用prep SHA，artifact-seal涵蓋完整交付。保全核驗只讀有限pins與索引，不重讀全部素材本文；r28～r32seal、r32prep／stage／shared、來源準備seal、根manifest／入口與所有引用原件保持不變。SYSTEMROOT大小寫無關，憑證產生false及私有cache接線保持。

native utility scheduler、frame初始化／+1 scheduler／絕對release起點仍unknown，維持相對marker與引擎policy標示，沒有native/live或P24完成宣稱。後續真正失敗必須留原證據分類；適配需另受審的來源支持，不得減斷言／刪案例或機械重跑。
