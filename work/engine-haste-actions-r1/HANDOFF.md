# ENGINE-HASTE-ACTIONS-IMPL-R1 — 加速攻擊／施法候選

候選已完成並封存，等待主管派獨立覆核；沒有共享合入、發布、正常產品／原生遊戲驗收。本目錄封存後停寫。

## 最終修改

只有 `source/ActionCadence.cs` 相較r30改動。Speed1／Brave0現在透過真正Attack/CastSelectedMagic准入、已存在renderer與release消費端使用加速快照。原每幀先算 `(3*t)>>2` 再加總動畫與marker前綴，保留零時間frame；server interval單獨採JAR的0.755、reuse與margin不縮放。新增NormalWaitMs讓1→0→1與1→2→1保留曾經較慢的有效deadline高水位，completion不縮短。

Speed0/2時長及既有API相容；Slow優先於Haste，舊SlowWaitMs保持作用。r30的Combat/Magic、renderer、NormalHaste/F1、地圖／角色／外觀／校正／目標／取消／lease／死亡回呼guard原文未改。弓30ms、光箭60ms、附著／impact週期不變，不加新技能、勇水、素材、道具或伺服器規則。HP/MP仍只依原回包。

時間分量、各來源適用條件及未認證的native絕對release起點見SOURCE-CONTRACT.md。舊server-action-vectors中的Speed1 gate是前一增量保留政策，不能拿來代替本次JAR haste公式。

## 實際驗證

| 項目 | 結果 |
|---|---|
| 固定r30 gap helper | 25 checks全過，真DLL/admit/renderer重現未加速 |
| 最終候選 helper | 1407 checks全過 |
| Release建置 | 0 warnings、0 errors，未啟動產品EXE |
| 固定來源核對 | BIN原15 chunks、JAR原3 class entries、42 profile SHA；沿用native270／Speed1 90向量與純JAR108結果，沒有新執行 |
| 逐幀metadata | 126 action rows皆可區分逐幀取整與總和乘倍率 |
| 共享保全 | 177 source/audio/web全部符合指定r30 baseline |

候選suite由封存r30 suite衍生，保留0/2檢查，將Speed1舊缺口預期改成獨立讀profile的逐幀／server值。新增0↔1／1↔2／同值、短暫較慢狀態、queued revision取消、own/remote、預測→ack、乾淨renderer真實晚到時點、完整late success/error生命週期、move/attack/spell/item/ground各消費端精確邊界及未知motion恢復。無本機HP/MP預扣或假伺服器成功。

result.checks只計最後一次候選helper，不重複加gap或來源引用數。此為實作者自測，不等於獨立接受。各argv、exit code、stdout/stderr、實際載入DLL SHA在evidence。VALIDATION-NOTES.md說明早期mixed-clock helper修正；未改產品其他時鐘。

## 封存身份

- CandidateId：ENGINE-HASTE-ACTIONS-IMPL-R1；revision r31（候選，不代表已發布）
- product DLL：`12ad5bbfac696033ee1f8d807346f401216138f4ce51cb58da9438e0b4ba5ec1`
- audio DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`
- r30 release seal：`199ce6fe81130138e9fedf98e688928ce44b0a45c2aebd1b92fdd9519b8225dc`
- r30 baseline SHA：`1329700b9fabf59e7406d62790ea8a32758ee9771963d0dcd49c7cde85708a66`
- source-before.json：原177 pins；changed-files.json／changes.diff／delta：唯一產品差異。
- source-manifest.json：完整177來源、R相對source指標、size及sha256；schema engine-r29-source-pins-v1為legacy名稱。
- result.json：engine-candidate-result-v1；delivery-manifest.json：engine-candidate-seal-v1，包含result/source-manifest及所有採用本文與實際產物，seal不列自己。

自己的真Goal實讀仍blocked，不新建／恢復／修改。沒有新chat／子代理、模型變更、DB／服務／socket操作。指定整合者須待獨立接受再核對base pins串行合入。

## 重跑

不可在此封存目錄原地重跑。於覆核者獲准scratch複製candidate/source、candidate/audio、candidate/web、vendor、baseline、checks、source-evidence、run.py、NuGet.Config、input-pins.json；略過obj/bin與assets junction。建立scratch下evidence/tmp/cli-home/nuget；assets建立唯讀consumer junction指向固定releases/r28/assets。保留candidate/build及build/checks的層級供素材相對路徑使用。

```powershell
$python = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe'
& $python -B -X utf8 (Join-Path $scratch 'run.py') baseline
& $python -B -X utf8 (Join-Path $scratch 'run.py') checks
```

runner使用本地vendor與隔離CLI/NuGet/TEMP，停用telemetry和開發憑證產生；僅dotnet離線建置及console helpers。一般毋須重跑setup、adapt_tests、source_evidence或finalize；如需核對測試衍生方式，adapt_tests.py及haste-tests.cs.inc已封存。原BIN/JAR算術不必重執行。原native absolute scheduler、完整同版遊戲流程與獨立接受仍待後續驗收。
