# ENGINE-SLOW-IMPL-R2 — F1 修正交付

R2 候選完成，等待同一覆核者複驗。r1 保持 rejected／封存；本次沒有共享引擎合入或原生驗收。

## 精確修改

相較 r1 **只有 `source/NormalHaste.cs` 一個產品來源檔修改**，見 `r1-to-r2.diff`、`r1-to-r2-changed-files.json`。遠端 S255／S67 更新後，當目前 Speed／Brave 已回復合法、舊 MotionSpeed／MotionBrave 卻不支援時，將 `MotionAt` 歸零，丟棄那個無法計時的步行。立即用權威終點顯示靜止角色，無須新 S10；舊未知速度欄位保留，不把它改成猜測倍率。

若目前狀態仍不支援，既有 renderer 拒絕不變；若當步快照原本合法，則不清除，保持該步原本的幀、時長及插值。下一個合法 S10 仍重新快照當前速度。沒有修改 ObjectRendering、步行冷卻或非步行動作閘門。緩速攻擊、施法、道具／裝備、拾取／丟棄及非行走滑鼠動作仍未支援。

對共享 r28 基底的完整差異仍為 9 個來源檔，見 `delta/source`、`changes.diff`、`changed-files.json`。`candidate/source`、`candidate/audio` 為獨立本文，不是硬連結。未沿 r1 assets junction 複製或寫入。

## 實測結果

| 項目 | 結果 |
|---|---|
| 相同覆核者最小 helper／r1 固定 DLL | 六組 F1 全部重現；恢復後 missing，再送合法 S10 才出現 |
| 相同 helper／R2 固定 DLL | 六組全部恢復正常，不需新 S10，當前不支援期間仍 missing |
| 本次 F1 專項 | 112 checks 全過：六恢復案例、立即與晚期呈現、權威終點、舊快照不猜速、下一步正確速度／幀／插值、Speed／Brave 雙重不支援的兩種回包順序、合法當步在暫時不支援後保留 |
| 覆核者原樣 suite 重跑 | 1199／1199 全過，原來六個 F1 assertion 現在通過 |
| r1 實作原樣 suite 對 R2 重跑 | 1371／1371 全過 |
| Release 產品建置 | 0 warnings、0 errors；沒有執行產品 EXE |

`checks=2682` 是專項、覆核者 suite 重跑、實作 suite 重跑三者的實際通過數相加，**不另加六個最小重現案例**。重跑覆核者的測試不代表已獲獨立接受。各結果、實際 argv、exit code 與載入 DLL SHA 均在 `evidence`。最小 helper 與兩套原 suite 本文從封存證據獨立複製，原樣保留；`run.py` 僅轉接本次可寫工作區、引用 DLL 與輸出位置。

## 身份與保全

- CandidateId：`ENGINE-SLOW-IMPL-R2`
- R2 product DLL：`8ccfa4e3b5d253206779a5d4b1e59682263ffbddf5dab43fe15b0aa1f86a7397`
- R2 audio DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`
- 原 r1 seal：`bf22ad09f0d2b2940dfb295680d363287b56a253249f128de393520b562dac61`
- 原 r1 DLL：`a595559878d3e24b447d8be3a5c1e8ce595ff3a47dceb6877b9256ba00f12472`
- r1 seal 296 個本文重新核對未變；共享 source/audio/web 共 176 檔對精確整合 baseline-lock 全部未變。
- `source-manifest.json` 採 `engine-r29-source-pins-v1`：完整涵蓋 source 140、audio 6、web 30。source/audio 指向本候選；未變 web 指向共享 engine/web 並核對 base SHA。
- `result.json` 採 `engine-candidate-result-v1`；`delivery-manifest.json` 採 `engine-candidate-seal-v1`，包含 source-manifest、result 與交付本文，seal 不列自己。
- 原 BIN／JAR、r28 與 oracle 只讀回核對 SHA，**沒有重跑原生模擬或 JAR 逆向**；來源連結見 `reused-evidence.json`、`input-pins.json`。
- 真 Goal 仍是舊任務 blocked，未新建、恢復或修改平台資料。沒有新代理、模型變更、DB／服務／正常產品／GUI／socket 操作。

## 重跑與接續

本目錄交付後停寫。覆核者在自己核准的新 scratch 複製本候選 source/audio、vendor、checks、harness、repro、f1、NuGet.Config、run.py，以及兩份 review oracle、native-speed-oracle.json、evidence/server 的 timing.tsv／speed-packets.tsv；略過 obj/bin 及 assets junction。建立 scratch 的 evidence/tmp/cli-home/nuget，以只讀使用 junction 指向 releases/r28/assets。若 scratch 不是 project/work 的直接子目錄，調整 runner 的 R 根解析。

```powershell
$python = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe'
& $python -B -X utf8 (Join-Path $scratch 'run.py') repro-r1
& $python -B -X utf8 (Join-Path $scratch 'run.py') r2
```

`run.py` 只啟動 .NET 離線建置及 console helpers，使用本地 vendor、隔離 CLI home/NuGet/TEMP，停用 telemetry 與開發憑證產生。不需執行 setup.py 或 finalize.py；不可原地改寫封存輸出。所有 tests 都是實際 DLL／renderer 配合合成狀態與封存原素材，不能代替 native live acceptance。指定整合者在覆核接受後，核對 base SHA 再串行合入。
