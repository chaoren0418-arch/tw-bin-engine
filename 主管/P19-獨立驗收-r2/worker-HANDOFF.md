# P19 擊殺、經驗與原戰利品交接

第二次凍結交審（selection-race-v2）：工作者 CLI／UI 完整流程均通過，Goal 保持 active；只有主管明確功能驗收及清理交還後才正常關閉同一 JAR／DB。階段範圍是正常單次近戰、原 EXP／等級、AutoLoot2 自然背包、雙介面顯示與保存重登，不是完整引擎。

主管第一版 CLI 通過；UI 首輪點選成功後被原 NPC 回家 S120 移除，尚未攻擊，故首輪失敗。舊freeze/ready/docs已封存 `evidence/freeze-v1-before-selection-race`，主管失敗raw及診斷複製保存在evidence。固定JAR的NpcMove.setDirectionMove offsets308–402在移動後超原半徑則teleport；L1NpcInstance.teleportInternal offsets409–434送S55/169、S120及removeKnownObject。UI當時32599,32947向東到32600,32947，離home32598,32948為sqrt5>2，吻合此分支。

窄修正只在UI測試首次選取：最多8次，每次最多5秒等待同ID原pack重現，再重新點選可見像素；若40ms後同ID已不存在，記錄原移除後才重試。仍存在卻選取失敗、選到其他物件、斷線或死亡即失敗。首次選取完成後後續選取只有一次；不在攻擊開始後自動重選。產品選取、遮擋、死亡、忙碌與原AI均未改。原失敗raw回放驗證3次移除、2次重現，S120清目標而S87不自動重選，見selection-race-regression.json；此為原包回放加明示合成點選時機，不宣稱此次真實r2觸發重試分支。

工作者UI r2從原保存狀態續跑，8次攻擊、EXP416461902→416461922、金幣51→91（+40）、藥水仍6；58雙端快照獨立解析、保存重登與實際雙介面PNG通過，未發生選取重試。來源建置見build-selection-race-final.log。主管後續UI亦必須從其首輪失敗保存狀態續跑，Fresh=false、全新證據目錄，不可使用下方第一版fresh命令覆蓋首輪；具體第二版指示以ready-for-supervisor.json為準。

## 實際結果

| 路徑 | 初始→保存 EXP | 原金幣 | 攻擊 | 旁觀 | 藥水 |
|---|---|---|---|---|---|
| CLI 首輪，擊殺後返回失敗 | 416461882→416461902 | 3→26（+23） | 8 | 零 EXP／空背包 | 6→6 |
| CLI r5，完整成功 | 416461902→416461922 | 26→62（+36） | 7 | 零 EXP／空背包 | 5→5 |
| UI fresh，完整成功 | 416461882→416461902 | 3→51（+48） | 7 | 零 EXP／空背包 | 6→6 |
| UI r2，窄修正後續跑成功 | 416461902→416461922 | 51→91（+40） | 8 | 零 EXP／空背包 | 6→6 |

CLI 共五輪，前四輪失敗未覆寫。r2 原狼人占用路線，無攻擊；r3 固定軸向測試假設不符原 AI 斜行，送一次攻擊、保存 HP327；r4 原目標在送出前移動，原 gate 取消首擊，零攻擊，消耗一瓶原白色藥水 HP327→407。r5 從原已保存狀態續跑，不是新建角色起跑。原 NPC 的自然移動、回血與重生仍運作，沒有 SQL 補怪或重設。完整狀態鏈見 `evidence/finite-ledger.json`，失敗原因見 `evidence/development-failures.json`。

三次實際擊殺均為 S113 絕對 EXP +20；原 RO 通知為21。原82／Lv60懲罰8×原定時活動2=20.5，原入帳long截斷20、通知Math.round21。產品只採 S8/S113 原狀態；金幣只採原背包絕對數量。三次金幣均合併既有object，無其他隨機物品，無自然地物；不可宣稱完成自然地物拾取。來源與實際plainHex/順序見 PROTOCOL.md、`reference/rewards`、`evidence/reward-causal-summary.json`。

## 實作與證據

- 新增 `source/Rewards.cs`、`OriginalExpTable.cs`、Web rewardsPanel。依原 EXP 表可證級距顯示總值、等級及百分比，欄寬／級距不符明示未知；重複狀態不重複加值，重登清除舊變化。
- 原始來源依據為固定 JAR、原 Java/class/javap、原 SQL 表、實際回包，來源 hash 見 `reference/rewards/manifest.json`、`exp-table.json`、`sql-source.json`。原始資源與真正複製清單見 inheritance/adaptation/copy manifests；沒有替身圖或重編原 JAR。
- `evidence/reward-worker-cli-r5/result.json` 與 UI result 均從雙端第一個seed前擷取，保留兩世代完整前置／尾包、sent sequence及applied邊界。獨立小端CP950重建44／62雙端快照，死亡、EXP、背包合併、旁觀、正常離線保存SELECT、重登值一致。
- `verify_rewards.py` 另逐包核對 r4 的 S24減一→S55/197→S225正HP，HP增量80大於原自然回血保守上界22；不以產品快照自比代替原包。
- UI 使用真正 WinForms controller、可見像素選取及實際 WebView2 DOM；不是外部 OS 鍵鼠操作。G held/repeated、Web連點各一封；Web輸入G零封；死亡／移除目標不再攻擊、世代2沒有延後操作。
- 已檢視 UI `reward-natural-loot-screen.png`、`reward-natural-loot-web.png`、`reward-relogin-web.png`，雙邊 EXP416461902、金幣51一致，重登變化區清空。`*-client.png` 為 WinForms繪製，WebView另以實際CapturePreview與整體screen PNG核對。
- 原序列化器16EXP/status與7RO通知、重複／截短／欄寬／級距／reset測試通過：`evidence/reward-tests-final.json`。16個合成TCP端點測試通過：`tcp-summary.json`。8組既有模型回歸通過：`ground-regressions-final/run.json`，其中歷史P17 raw回放明示為回歸，非P19新動態。實際Web焦點／重開回歸 `web-regression/web-test-result.json` 通過。最終程式建置 `build-route-r5.log` 零警告錯誤。

## 操作與主管驗證

在本目錄以 PowerShell 執行 `./build.ps1 -Clean` 可乾淨重建，所有build/cache留在本目錄。執行 `build/client/P11.ClientEngine.exe`，選 `p19-real-jar-d287-client2`、port42041，正常登入並進入角色。方向鍵／WASD步行，點可見原怪物選取，G單次攻擊；Web有單次攻擊按鈕。原藥水選取後U／單次使用；不支援其他物品使用時明示。F9本機面板、F10返回遊戲焦點。帳密只在 `D:/P19Lab-ClientEngine/private-tests.json`，不要輸出密碼。

第一版交審時主管四角色 ID1900190003、1900190004、1900190007、1900190008 完整保留。第二版已核對其主管CLI成功與UI失敗的精確保存狀態，見freeze-v2-reserved-saved-baselines.json；下列為第一版fresh流程紀錄，第二版不能直接重跑：

```powershell
python -B -X utf8 reward_integration.py cli supervisor
python -B -X utf8 reward_integration.py ui supervisor
python -B -X utf8 verify_rewards.py evidence/reward-supervisor-cli/result.json
python -B -X utf8 verify_rewards.py evidence/reward-supervisor-ui/result.json
```

同一 JAR PID81072／launcher17028，DB14516／launcher26020。DB127.0.0.1:43319、UUID7f02dbea-d03b-11f1-ae5d-4cedfb679789、datadir D:/P19Lab-ClientEngine/db-data/。精確建立時間、exe、父子及端點在 server-start/ready、db-identity、freeze receipt中。原 Netty wildcard bind42041加loopback peer guard如實保留，不對正常listener做會關閉它的LAN probe。

freeze後工作者不再改來源、登入、重啟或清理。主管 handback 應提供 status=`functional-acceptance-passed-handback-for-cleanup`、frozen_source與environment兩份manifest SHA256。`cleanup_final.py`只在該明確交還後執行：核對全部連線釋放→原console shutdown0→等JAR/launcher退出→再驗同DB後SQL SHUTDOWN，不強殺。原exit1及ShutdownControl警告保留。凍結前版本會封存在 evidence/freeze-before-cleanup。

## 保留限制

P18原UI8肉去向未證、拾取動畫未繪製、P10舊EXE未驗收仍保持原狀；本階段新環境不改寫歷史。原 EXP 極端／升級邊界只有明示合成證據，這次真實角色維持Lv60。未知物品保留原欄位，沒有支援裝備、魔法、遠程、交易、商店、全物品、全地圖或產品自動戰鬥／追怪／補血。測試控制器的有限單次操作不作為產品功能。
