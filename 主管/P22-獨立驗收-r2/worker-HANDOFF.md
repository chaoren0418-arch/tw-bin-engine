# P22 r2 交驗：原雙區塊常駐與原保存續跑

目前為退修完成、重新凍結交主管。實際 Codex goal 保持 active；沒有功能驗收通過、沒有清理授權。修復期間沒有登入任何角色，原 JAR／DB 仍是首次那一組。

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
- `ResumeRecovery=true` UI在原town保存位置，使用既有native藥水button、Web單次治癒及原自然恢復；最多6瓶現有藥水／30次單次治癒／600秒。所有扣量、HP與MP只讀原回包，HP≥500且MP≥20才正常走回；不足或受傷則有限停止。Food40滿足原自然恢復必要food≥3條件，還檢查實包weight<120；沒有吃肉支援或SQL補資源。CLI保存HP624／MP2只待原自然MP達20。
- 原死亡HP0→保存HP1由真正 `com.lineage.data.npc.event.QuitGame`、L1PcInstance死亡setter保護、CharacterTable保存clamp1與load復活鏈解釋；引用主管 `P22-原死亡HP0保存HP1獨立核對.json`，不是同名echo.QuitGame，也不是SQL修復。

## 驗證與重跑

`correction-r2-validation.json` 列出同一最新DLL的全部證據。十項magic/ranged/combat/inventory/ground/equipment回歸、雙區原像素與動態離線測試、完整既有離線compositor均pass。雙區測試含10727424像素及mask完全相同、固定原JAR65536方向／384雙向接縫、正常產品Move跨界及640ms節奏、五區位置的原角色／點選遮罩／自癒／光箭／弓箭／epoch清除。主管獨立golden與5539南區原tile逐欄相等。

修復過程的離線失敗全部保留：雙向Move測試起初少了明示合成time reply；完整compositor發現局部SourceIndex重複，改為全域serial追蹤遮擋。這些失敗不是live失敗，也未修改原服。離線PNG均標示synthetic原資產情境，不能冒充新live畫面。

在獨立真副本根目錄：

```powershell
.\build.ps1 -Clean
python -B run_magic_regressions.py
python -B run_ranged_regressions.py
python -B run_dual_regressions.py
```

主管下一輪必須 `Fresh=false, ResumeRecovery=true`。全部四個主管角色都已被原r1驗收消耗，不能再稱fresh。`ready-for-supervisor.json` 記錄兩份原db-proof的path/hash及guarded SELECT完整after相等，包含角色、items、skills。`magic_integration.py` 支援 `--resume-from <原db-proof.json>`，在首seed前獨立capture，要求exact saved state及原服務PID/UUID一致，修復期間禁止任何角色登入，工人禁止使用主管角色。

原服務：JAR58508／launcher59016；DB80716／parent5808；UUID2d062607-d05f-11f1-9855-4cedfb679789。game42053／DB43322，42054/42055不啟動。保持原wildcard game listener與原peer guard；曾有隔離security probe availability fail，不能對正式本輪listener重做LAN拒絕探測。

主管完成新乾淨重建、16項回歸＋雙區獨立核對後，以原保存UI續跑完整native/Web施法、擊殺、收益、拒絕、自然恢復、保存重登及實際圖片；必要時同原CLI繼續驗證全域場景修改。未通過前不清理。只有具體 `functional-acceptance-passed-handback-for-cleanup` 收據才允許 `cleanup_p22.py` 正常shutdown0／同DB SHUTDOWN，最後完整R/LAB manifest（只排除根delivery-manifest自身）與實際goal complete。
