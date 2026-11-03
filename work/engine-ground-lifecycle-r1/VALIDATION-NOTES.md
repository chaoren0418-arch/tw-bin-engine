# 離線驗證紀錄

所有command.json保存argv、cwd、exitCode、私有cache/TEMP與helper SHA；同目錄保留實際Program.cs快照、stdout/stderr、row-level result。未刪除失敗或更改既有斷言以求通過；本輪三次helper執行均首次通過，product/helper build均0 warnings/0 errors。

| 執行 | DLL | rows | 意義 |
|---|---|---:|---|
| baseline-01 | 原r32 c64bf006… | 36/36符合觀察預期 | 11缺口重現、24合法早回包控制、1無socket；非產品安全驗收pass數 |
| candidate-01 | 候選8ed92b8b… | 252/252 pass | 正常肉箭、partial/full、queued gates、所有權、真時鐘、drop保全 |
| extra-01 | 同一候選8ed92b8b… | 87/87 pass | 公開互斥、pointer、early false/error、world矩陣、item保全、action authority |

候選合計339次檢查執行、338個不同ID；兩個suite各有一次相同`no-socket-events`邊界檢查。337筆功能／狀態斷言加2筆無socket檢查。baseline36、獨立review140與歷史377/3602均不計入候選。extra只建helper，未重建或替換產品DLL，亦未重跑已通過252項；新增是為補足公開互斥與保全證據。

candidate-01細項包括：Speed0/1肉箭各partial/full精確C112及無本地扣量；24合法early replies；70個queued gate及10個initial defensive busy；world/actor/session/lease的true/false/error與舊先／新先完成；新owner准入、倒序finally、queued舊owner；current false/error；實際900ms click guard與3100ms pending timeout；8個正常drop/合法回包與2個drop stale；Slow與數量／距離／envelope邊界。

extra-01包含36個公開雙向producer互斥（ground與move/attack/magic/item/ground；queued及admitted）；10個pointer正常/取消/focus/visible/walking；16個current-owner在合法early reply後false/error；12個Speed0/1肉箭world true/false/error矩陣；8筆r32 item重設與stale保全；4筆未知／重複S158不改數量；1筆socket事件檢查。

世界更替使用真正ApplyWorldPacket→World.Receive，S206後S87恢復Ready；沒有只用底層World.Receive冒充wrapper修復。actor測試先反射更新匹配Status身份，再走S87 wrapper；這是防禦性所有權模型，不宣稱普通伺服器可達的Status替換。session取消走Cancel，lease-only走私有ResetGroundSend並明列為防禦性reset。

初始InWorld、Status/Entered/TimeReplies、CacheEnabled=false為合成準備；ground、inventory、own、speed等回包經真parser。只複製44份player metadata及1份magic catalog作helper依賴，沒有像素抽取、PAK重解或全素材稽核。SocketGuard只監測事件，禁止網路入口；未啟動正常產品視窗或使用任何服務。

## 在獲准fresh scratch重跑

不可原地執行封存工作。複製candidate/source、candidate/audio、candidate/web（排除obj/bin）、checks、vendor、NuGet.Config、run.py及本輪assets metadata subset，建立evidence/tmp/cli-home/nuget。目錄結構保留candidate與build為scratch子目錄；如需重現baseline，另複製baseline-runtime。

```powershell
$python = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe'
& $python -B -X utf8 (Join-Path $scratch 'run.py') baseline baseline-review
& $python -B -X utf8 (Join-Path $scratch 'run.py') candidate candidate-review
& $python -B -X utf8 (Join-Path $scratch 'run.py') extra extra-review
```

run.py要求每個evidence tag尚不存在，保留每次執行，不覆蓋舊紀錄。所有restore僅offline vendor，DOTNET_GENERATE_ASPNET_CERTIFICATE=false；DOTNET_CLI_HOME、NUGET_PACKAGES、TEMP/TMP皆在scratch。baseline判定是預期缺陷觀察；candidate才是修復契約。final-base-equivalence只核177來源／四身份與seal/acceptance，主管全release14342 SHA稽核僅引用，沒有冒稱worker重跑全量。
