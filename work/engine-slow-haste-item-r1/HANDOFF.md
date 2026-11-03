# ENGINE-SLOW-HASTE-ITEM-IMPL-R1 — r32 緩速普通加速水候選

候選已完成，最終 377 項離線自測通過，封存後停寫，等待主管派獨立覆核。沒有共享合入、發布或正常產品／原生驗收，不代表 P24 完成。

## 產品差異

只改 `source/Inventory.cs` 與 `source/WorldEntry.cs`。Speed2/Brave0 限既有 exact SupportedHastePotion 單次 C164；正常0/1既有用品保全，未知速度與非零Brave明確保守拒絕。Count/Speed/Brave/HP/MP仍只由原回包決定，delay0+100ms本機guard不變。

送出前重驗快照與所有跨動作busy；送出後success/error/finally只以session/world/actor ID/lease判定歸屬，容許合法S255/S67/S24/S242/S57先到。WorldEntry在世界／actor身份改變時清理舊租約，避免嚴格finally留下永久忙碌。舊回呼不能發ItemSent、延長或清掉新owner，真正同歸屬錯誤仍故障。底層transport全域失敗政策不變。

其他175來源逐檔不變，包括r31動作timing、高水位、flight30/60、effect191、ground、NPC。NPC direct/pointer舊差異明列保留；來源utility scheduler仍未知，不能稱0ms。

## 驗證與身份

| 項目 | 結果 |
|---|---|
| 最終候選 helper | 377 checks 全過，真 Environment.TickCount64 |
| product/helper Release build | 0 warnings、0 errors |
| stage基底 | 原177檔全部SHA不變；candidate為獨立副本 |
| 最終r31綁定 | 177來源及四產品身份相同，release seal/acceptance核對通過 |
| 候選product DLL | `c64bf0069722e28fc174d70d261b7540da6e3f74ee422d4874cbffb77149364f` |
| audio DLL | `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4` |

候選result只計最後377，不加第一輪353、前階段36或獨立準備90。測試使用真DLL、公開API及packet入口，配合合成角色／背包與記憶體sender；公開producer與人工注入的證據分列。詳見 VALIDATION-NOTES.md、SOURCE-CONTRACT.md、evidence/candidate-result.json。沒有重跑全部3171，待整合者後續同版完整回歸。

source-before.json固定接受stage，source-manifest.json完整177檔均指向本封存candidate；schema engine-r29-source-pins-v1 是legacy名稱，revision為r32。changes.diff/changed-files.json/delta列最小差異。result.json 使用 engine-candidate-result-v1；delivery-manifest.json 使用 engine-candidate-seal-v1，包含result/source-manifest及採用產物，不含自己與assets/cache/obj/bin。

最終r31追加綁定：final-base-binding SHA `d6946c449f35b1d26804bd3fd4878bf24bdb0f74d66a302765dfd91058010fa4`；release seal `1d5b1c149495cb2b44f50daf2c3361eb15e8000ba27bf133d1029735f10cfd90`。原stage baseline SHA `16ab9dbb534e20e03e622777ab530065e1bc97363c08d8b765e2cb880115bb87`未改，final-base-binding-pins.json與evidence/final-base-equivalence.json保留本次核對。主管14329 release檔稽核只引用主管證據，此worker未冒充全量重驗。

## 重跑方式

不可在封存目錄原地重跑。在覆核者已獲授權scratch複製 candidate/source、candidate/audio、candidate/web、checks、vendor、NuGet.Config、run.py；略過obj/bin與assets junction。scratch下建 evidence/tmp/cli-home/nuget，assets建read-only consumer junction指向固定releases/r28/assets，保留candidate/build與build/checks相對層級。

```powershell
$python = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe'
& $python -B -X utf8 (Join-Path $scratch 'run.py') checks
```

run.py 使用offline vendor、各cache/TEMP皆在scratch、DOTNET_GENERATE_ASPNET_CERTIFICATE=false；不必重跑setup/finalize或執行原BIN/JAR。verify_binding.py供另獲准scratch核對外部最終基底時參考。

真Goal實讀blocked，本輪沒有新建／恢復／修改；沒有新chat/subagent、GUI/socket/服務/DB/SQL操作。封存後等待指定review，不寫共享engine或其他work。
