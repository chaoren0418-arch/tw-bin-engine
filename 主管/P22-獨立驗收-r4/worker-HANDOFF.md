# P22 r4 交驗：有限可見目標準備與最新保存續跑

r4 修復依據 `correction-authorization-r4.json`。實際 goal 維持 active，等待主管功能驗收，沒有清理授權；修復期間沒有任何角色登入或服務重啟。

## 本次失敗與修正

主管 r3 乾淨 build 的 18 項、96 原 frame／81 heading／895 step 通過。實際 UI 續跑用完既有 6 瓶藥、11 次單次治癒，26.03 秒達 HP500／MP83；44 次正常跨區移動符合原地形。實際焦點成功，選取狼因兩秒內無可見像素而正常停止，未死亡。最新完整保存為 HP521／MP83、Food40、32595/32948、0 藥水。原 r3 失敗與所有原封包均保留，尚未完成完整 kill／reward／refusal／relogin。

`MagicTargetPreparation` 僅是測試 controller。候選必須當下存活、距離≤10、被原觀察者接收且有正的原 renderer EntityOwners 像素；UI 額外用既有 VisiblePoint 驗證實際 viewport。每輪與施法前重判；沒有可見候選才準備有限正常移動。最多 3 個計畫目的地、兩角色合計 60 次正常 Move、累積 45 秒準備時間；每次等待≤2 秒、50ms檢查兩角色原 HP／死亡／連線，HP下降（包含自然恢復後再下降）或低於150立刻保留失敗。原 focus／selection 兩秒護欄不變。

目的地來自 `reference/magic/visibility-waypoints.json` 的原資產多姿態離線遮擋規劃，只取原 spawn32598/32948、歐氏半徑2內目標位置；路徑由目前角色與占位加原地形 BFS，觀察者先正常跟隨。原 NpcMove 的超半徑 teleport-home 規則不變，規劃不保證 NPC 真走到預想位置；只有當下實際可見像素能決定選取。沒有改 renderer／depth／opacity／AI／SQL／產品選取與技能規則，沒有 raw move 或 hidden target C6。既有負面協定 probes 繼續保留原語意。

`fixtures/magic-r3-hidden-final.json` 從真失敗擷取 server packets 與最終狀態，記錄原來源 hash。`--magic-target-tests` 重播876主端原包與觀察者原包，明示補上自己的最終預測位置；證明真狼2000042184在32597/32949dir5有0可選像素，因此不選取、不送C6。另明示衍生的可見位置只作離線原 compositor／pixel ownership／原 cast gate 回歸，非新 live 成功。最終 DLL 跑18必要回歸＋此額外回歸，見 `correction-r4-validation.json`。

`history/r3` 封存6份原交驗 envelopes與全部既有變更前檔；未變來源按 r3 freeze hash 在原位可重現。r1/r2/r3及所有失敗不覆寫。新 UI 續跑唯一 proof 為主管/P22-獨立驗收-r3/world-evidence/magic-supervisor-ui-r3/db-proof.json，SHA256 13ee8ce8ac10d6c461dc774298c79ede96e45fd99e236b00bc5e45a20adcfd4a；CLI仍是原 HP624／MP2 proof 5b575926170a6329ed20ccd1ac6ff2243e6986eba5bd4ea21f8b0695c8e73b07。所有角色 Fresh=false；首seed前guarded fullSELECT精確比對，不能再用原UI HP1／MP115 town資料。

以下 r3/r2 說明是歷史修訂；當前狀態、保存值與最新驗證以本節及 r4 ready 為準。

## r3 必要回歸與證據封存修正

主管r2乾淨重建18組中17通過，ObjectRegression仍用局部SourceIndex7斷言global serial owner，故明確退修（correction-authorization-r3.json）。本次只修測試資料與斷言：entity ID7與global map serial7真碰撞；map local source index42不同於serial；兩map同local42、global serial7/100007不同；每次map覆蓋均清EntityOwners，保留uint.MaxValue與self sentinel測試。產品Renderer全域serial修正維持不變。

完整原BIN證據reference/dual-block/bin不再被一般bin排除，僅此精確目錄例外；generated bin/obj仍排除。主管r2沒有新live登入。history/r2保留全部6交驗收據、兩份r2報告及每個既有變更前檔；其他未變來源依原freeze hash在原位可重現。r1完整archive及r2所有失敗仍保留。

本次最終DLL以run_complete_regressions.py執行完整18項（含object/world/movement/dual-block/完整--test及其他13），correction-r3-validation.json記錄確切DLL與run。r2十項／雙區與舊compositor證據仍為歷史，不冒充r3。

## 修正原因與歷史

主管首輪 CLI 已通過；UI 在第三次遊戲焦點等待時無法取得實際前景，等待期間被怪物打死，原服正常保存 HP1／MP115／Food40／32580,32974 map0。此位置超出 r1 單區塊，不能直接重跑或 raw 移動繞過產品門檻。依 `correction-authorization-r2.json` 具體交還完成本次修正。

`history/r1` 封存 2102 個 r1 原來源／資產／收據檔（含原 HANDOFF、PROTOCOL、result、ready、source/environment manifests），有逐檔 SHA-256。所有工人與主管歷史失敗保留。r1 工人完整 CLI/UI 與視覺補驗、主管 fresh CLI 均屬 r1 DLL 證據；不冒充 r2 live 結果。原主管副本未修改。未定案的早期跨區排序輸出保存在 `history/r2-unverified-order`。

## 產品與原始依據

完整載入 7ffd8002＋7ffd8003，X32576–32639／Y32896–33023，4608×2328 畫布、26960 筆原繪製命令，全部原越界物件像素保留。Coordinates、資產邊界、地形 stride、背景畫布、攝影機、Web 原狀態、深度與選取／彈道／魔法均使用同一雙區範圍。地形保留所有8192原bytes；原區塊 commands、tile pixels、前4096 terrain bytes 不變。

跨區 equal-z 有實際相異像素交疊。固定 BIN（base00400000，SHA e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2）F09C70 初載3→2→1→0，F0B570南先插北後插，F0ED00 equal-z insert-before，故同cell/z北先畫南後畫。主管另外隔離執行原x86核42鍵。`reference/dual-block/bin/manifest.json` 保存11原chunk、9資料表的VA/RVA/FileOffset與原檔逐byte比對；主管獨立原像素／排序證據唯讀複製在同目錄。

這是完整兩區常駐，採已證原冷載順序。不是原2×2動態cache或全圖streaming。F116D0在Ybase32896時actorY32984南移會卸掉北區並載入未支援8004；本產品兩區持續常駐。水平重插、cache歷史相關equal-z、觸發opacity/group及portal仍未實作，後續完整引擎必補，不能以本次常駐取代。`reference/dual-block/resident-scope.json` 記錄可觀測差異。

## 測試護欄及有限恢復

所有以下自動驅動僅是測試控制器，沒有增加產品自動施法／恢復功能。

- GameFocusReady最多2次真實activate／總2秒、50ms巡檢，要求產品GameInputAllowed及GetForegroundWindow==Handle。HP下降、HP<150、死亡或斷線立刻失敗並走正常Cancel/logout；不fake focus、不AttachThreadInput、不Web fallback假通過。
- 可見目標等待最多2秒並監看HP下降。已送出施法的原回包、busy、自然MP與有限heal等待持續監看死亡／斷線／低HP；不因正常一次受擊就宣稱技能失敗。進戰場前在安全區先確認真焦點。
- `ResumeRecovery=true` UI在當前原保存位置（r4為32595/32948、521HP83MP、0藥水），使用既有native藥水button、Web單次治癒及原自然恢復；最多6瓶現有藥水／30次單次治癒／600秒。所有扣量、HP與MP只讀原回包，HP≥500且MP≥20才正常走回；不足或受傷則有限停止。Food40滿足原自然恢復必要food≥3條件，還檢查實包weight<120；沒有吃肉支援或SQL補資源。CLI保存HP624／MP2只待原自然MP達20。
- 原死亡HP0→保存HP1由真正 `com.lineage.data.npc.event.QuitGame`、L1PcInstance死亡setter保護、CharacterTable保存clamp1與load復活鏈解釋；引用主管 `P22-原死亡HP0保存HP1獨立核對.json`，不是同名echo.QuitGame，也不是SQL修復。

## 驗證與重跑

`correction-r4-validation.json` 列出最新DLL完整18項加目標準備回歸證據；`correction-r2-validation.json` 為封存前一版。十項magic/ranged/combat/inventory/ground/equipment回歸、雙區原像素與動態離線測試、完整既有離線compositor均pass。雙區測試含10727424像素及mask完全相同、固定原JAR65536方向／384雙向接縫、正常產品Move跨界及640ms節奏、五區位置的原角色／點選遮罩／自癒／光箭／弓箭／epoch清除。主管獨立golden與5539南區原tile逐欄相等。

修復過程的離線失敗全部保留：雙向Move測試起初少了明示合成time reply；完整compositor發現局部SourceIndex重複，改為全域serial追蹤遮擋。這些失敗不是live失敗，也未修改原服。離線PNG均標示synthetic原資產情境，不能冒充新live畫面。

在獨立真副本根目錄：

```powershell
.\build.ps1 -Clean
python -B -X utf8 run_complete_regressions.py
```

主管下一輪必須 `Fresh=false, ResumeRecovery=true`。全部四個主管角色都已被原r1驗收消耗，不能再稱fresh。`ready-for-supervisor.json` 記錄兩份原db-proof的path/hash及guarded SELECT完整after相等，包含角色、items、skills。`magic_integration.py` 支援 `--resume-from <原db-proof.json>`，在首seed前獨立capture，要求exact saved state及原服務PID/UUID一致，修復期間禁止任何角色登入，工人禁止使用主管角色。

原服務：JAR58508／launcher59016；DB80716／parent5808；UUID2d062607-d05f-11f1-9855-4cedfb679789。game42053／DB43322，42054/42055不啟動。保持原wildcard game listener與原peer guard；曾有隔離security probe availability fail，不能對正式本輪listener重做LAN拒絕探測。

主管完成新乾淨重建、18項回歸＋目標準備回歸及雙區獨立核對後，以原保存UI續跑完整native/Web施法、擊殺、收益、拒絕、自然恢復、保存重登及實際圖片；必要時同原CLI繼續驗證全域場景修改。未通過前不清理。只有具體 `functional-acceptance-passed-handback-for-cleanup` 收據才允許 `cleanup_p22.py` 正常shutdown0／同DB SHUTDOWN，最後完整R/LAB manifest（只排除根delivery-manifest自身）與實際goal complete。
