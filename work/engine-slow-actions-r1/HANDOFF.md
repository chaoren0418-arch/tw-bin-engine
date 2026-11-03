# ENGINE-SLOW-ACTIONS-IMPL-R1 — 緩速攻擊／施法候選

候選完成，等待獨立覆核；共享 r29 保持原樣，未合入、未發布、未進行原生遊戲驗收。封存後本目錄停寫。

## 交付內容

Speed2／Brave0 現在可走真正的單次攻擊與既有技能1／3／4／8送出流程。新增 ActionCadence 分離 server interval、原逐幀動畫、marker release、reuse 與本機 margin；其餘正常速率保持 r29。慢速動畫採原逐幀路徑，處理 gfx0 weapon0/4/50 的舊 normalized duration 差異。詳見 SOURCE-CONTRACT.md；不宣稱已重建原 native 絕對 scheduler/release 起點。

sender admit 建立當步快照；相同動作回報確認保留原時點，速度中途回報不回捲。queued speed revision 失效、0→2→0 deadline 高水位、move/attack/spell/item/ground 互斥均實測。弓／光箭保留30／60ms飛行，釋放時鎖定目標一次，附著與impact不因角色緩速加倍。

主管提出的 admit後同session換圖→舊sender完成 race 已修正：completion及exception重驗world、actor、appearance/weapon、correction、lease、death、input/target；失效回呼不延長新狀態deadline、不發Sent、不讓舊錯誤Cancel新世界。合法Speed變化仍保留有效預約的Math.Max高水位。finally僅清理自己的租約。helper包含成功／失敗回呼及新lease保全。

未知Speed、非零Brave與慢速道具／裝備／拾取／丟棄／滑鼠對話仍拒絕；pointer attack轉入實際AttackCore。HP/MP仍只依原回包，不本地扣除或偽造成功。

## 驗證

| 證據 | 結果／範圍 |
|---|---|
| r29 gap helper | 9 checks；原 r29 的緩速攻擊／施法確實拒絕 |
| 最終候選 helper | 916 checks 全過；真DLL、封包入口、admit、實際renderer，synthetic狀態與時鐘 |
| 本輪固定 x86 | 18組 action1/18/19 共用逐幀算術，明示metadata/callee替身 |
| profile證據 | 42個固定profile、126個action rows，SHA及分量核對 |
| 產品 Release 建置 | 0 warnings、0 errors；沒有啟動產品EXE |
| 共享保全 | 176 source/audio/web全數符合r29 baseline，原本文未修改 |

result.checks只計最終候選helper，不重複加基底gap、native算術或metadata rows。測試為實作者自行執行，不代表獨立接受。evidence含執行argv、exit code、stdout/stderr與實際載入DLL SHA。新原碼證據及class pins在source-evidence；SOURCE-CONTRACT.md明列已驗證、引擎政策與原生未驗證部分。

## 身份與整合

- CandidateId：ENGINE-SLOW-ACTIONS-IMPL-R1
- product DLL：`1abc1eed02917d14fd775be60920fe74efb9b102bc53eea5874fc0128e5247ef`
- audio DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`
- r29 seal：`d3ac187e7fc44745864640e4bcc72fefbaeeaa0b0e9d63de7c0c3cfa8e46273d`
- source-manifest：177檔完整source/audio/web，schema engine-r29-source-pins-v1；所有source均指向本候選獨立副本。
- 對r29差異：8檔（含新增ActionCadence.cs）；見changed-files.json、changes.diff、delta/source。base-manifest保留原176個pins。
- result採engine-candidate-result-v1；delivery-manifest採engine-candidate-seal-v1，包含result及source-manifest，seal不含自己。assets junction、暫存/cache/obj/bin不封存。

僅指定整合者在獨立覆核接受後，核對base pins再串行合入。未更改Goal；既有worker Goal為blocked，不新建、不繞過平台狀態。

## 重跑

不可在本封存目錄原地重跑。於覆核者獲准scratch複製candidate/source、candidate/audio、candidate/web、vendor、checks、baseline、run.py、NuGet.Config、input-pins.json；略過obj/bin與assets junction。建立evidence/tmp/cli-home/nuget及assets只讀consumer junction，指向固定releases/r29/assets。candidate/build與build/checks保持runner預期層級。檢查使用本地vendor且停用telemetry、開發憑證產生；只啟動dotnet建置與console helper，不需要正常產品／GUI／網路／資料庫。

```powershell
$python = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe'
& $python -B -X utf8 (Join-Path $scratch 'run.py') baseline
& $python -B -X utf8 (Join-Path $scratch 'run.py') checks
```

通常毋須重跑setup.py、implement.py、inspect_sources.py、action_evidence.py或finalize.py；前兩者為初始建置歷史，不是最終來源生成器。原碼算術若需獨立重驗，只在另外授權scratch執行action_evidence.py，並保留stub限制。完整正常遊戲、服務整合與獨立接受仍待主管另行安排。
