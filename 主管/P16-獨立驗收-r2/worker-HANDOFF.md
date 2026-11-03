# P16 基礎近戰與原服傷害回饋

狀態：工作者必要驗證已通過；凍結收據以 ready-for-supervisor.json 為準，等待主管獨立驗收。固定原 JAR 與本階段 DB 保持同一程序，未重啟掩蓋失敗。完整引擎尚未完成。

## 使用

在本目錄 PowerShell 執行 `./build.ps1`，再 `python -B -X utf8 launch_real.py`。介面登入至本階段127.0.0.1:42035；憑證只在 `D:/P16Lab-ClientEngine/private-tests.json`，不放報告。選「整合王子乙」進入map0。滑鼠選可見存活物件，遊戲焦點按G或面板「單次攻擊」只送一次；按住不自動連擊。原服決定合法性、命中、傷害與死亡。移動保留方向鍵/WASD/數字鍵盤，Space清除輸入，F10返回遊戲焦點。輸入框打字不攻擊。

可見戰鬥區在原spawn東側。R2測試先正常C_Move至(32599,32948)，有界等狼人home(32598,32948)後重新點選；同Y向東逐格至32600、32601，各等原位置與旁觀確認，最後主角至(32600,32947)、狼人留(32600,32948)。原movement_distance=2使用歐氏距離；(32600,32947)若是狼人位置則√5>2會原回巢。不能SQL搬移，未支援產品追擊或自動移動。

## 重建與重跑

只在主管持有環境或明確交還修正後執行。freeze後工作者不再登入、改來源或停止服務。

```powershell
./build.ps1 -Clean
python -B -X utf8 lab.py verify
python -B -X utf8 server.py status
python -B -X utf8 combat_integration.py suite
python -B -X utf8 verify_combat.py evidence/combat-suite/result.json
python -B -X utf8 combat_integration.py ui
python -B -X utf8 verify_combat.py evidence/combat-ui/result.json
python -B -X utf8 verify_lure.py
python -B -X utf8 audit_lure_revision.py
python -B -X utf8 regression_verify.py
python -B -X utf8 ui_responsiveness.py
python -B -X utf8 object_integration.py suite
python -B -X utf8 replay_objects.py evidence/objects-suite/result.json
python -B -X utf8 object_integration.py ui
python -B -X utf8 movement_integration.py suite
python -B -X utf8 revision_verify.py
python -B -X utf8 movement_integration.py ui
python -B -X utf8 scripts/run_regressions.py
python -B -X utf8 scripts/run_tests.py
```

既有fixture禁止再次prepare。每個動態runner自動把既有同名證據移到history，保留全量wire與失敗；同帳號測試必須依序。自然回血正常走回安全點，原HP不足500才等待，最多900秒；前後各一段，wrapper2100秒。等待不改原HP、AI、裝備或數值。若再次失敗仍保存原capture，不重置角色或重啟原服。

## 實作與驗證

`source/Combat.cs`新增原近戰欄位、動畫狀態、生命/死亡防護與世代安全傳送；`OnlineInput.cs`、`WebPanel.cs`、`web/panel.js`提供單次鍵/按鈕；`ObjectAssets.cs`/`ObjectRendering.cs`沿用共用原素材遮擋。其他世界、物件、移動狀態共同處理原死亡/移除、取消與重連。

原JAR bytecode、來源Java、class hash與欄位來源見 `reference/combat/manifest.json`、`PROTOCOL.md`。原SPR增量72份/392frames逐像素獨立核對通過，完整資產216SPR/1496frames另由主管獨立核對。沒有用假動作/站立圖代替攻擊。

真實 `evidence/combat-suite/` 已證明一條原狼人死亡/移除鏈，原反擊造成自身HP下降、旁觀雙向可見、忙碌拒絕、原移動及重連無幽靈重送。獨立Python重播18快照/9800欄位通過。最新實際 UI 的全量 capture、獨立快照核對及原死亡鏈均見 `evidence/combat-ui/`；`independent-axis-lure.json` 額外核對同列引怪的三段雙連線原位置、C_Move、歐氏距離與後段原死亡終幀。UI屬真WinForms控制器與WebView2 DOM驅動，非外部鍵盤注入；截圖必須顯示主要圖形，未通過遮擋門檻的舊輪次不列成功。死亡終幀以原素材序列最後 key 為準，不假設其形狀必須平躺，也不更動原動畫/移除時序。

合成回放與真實TCP測試分開：combat/object/world/movement-gate負例、原JAR oracle及16TCP情境、離線渲染/heading/WebView回歸均保留獨立結果。合成負例不能當成真實原服戰鬥。

## 環境、fixture與失敗

新DB127.0.0.1:43316、UUID `4f47b7f8-d015-11f1-8f72-4cedfb679789`、datadir `D:/P16Lab-ClientEngine/db-data`，子PID82644。原JAR PID87612、game42035。實際父子、CreationDate、exe、hash在freeze身分收據；PID數字不能單獨作身份。21表/3685列遷移與獨立guard先完成，正常JAR未注入LAN拒絕probe。

第一個Lv10角色被原服擊敗，wire HP0/正常保存HP1已保留；1900120001沒有重置或再次選取。主管一次性允許新初始Lv60/HP700角色1900160001，其合法HP與Exp來源詳見fixture-revision-r2.json。初始SQL是明示合成角色/spawn，不是產品創角驗收。舊帳號只選另一個法師作不支援map/gfx回歸，敗戰角色前後row保持不變。

初期ED255解析錯誤、屋頂遮擋、超出狼人的原移動範圍及舊滿血等待失敗全部保留在history，沒有以SQL或重啟掩蓋。未受影響的P15既有證據只作有來源的繼承，不能算本輪新測試。

## 交接及清理

工作者完成後提供 `ready-for-supervisor.json`、`source-freeze.json`、`environment-freeze.json`，所有自身UI/WebView與合成連線先釋放，暫留同一JAR/DB供主管乾淨重建。此時Goal仍active，未宣稱最終完成。

只有主管明確交還後才執行 `cleanup_final.py`；該腳本要求主管交還JSON中的status與freeze hash，重新核對每個程序身份、帳號server_no與角色OnlineStatus，再原console shutdown 0，待JAR及launcher退出後，核對同DB後SQL SHUTDOWN。保留原GM_RESTART exit1/ShutdownControl警告，不強殺冒充正常。全部PID/端口消失且最終文件完成後才Goal complete。

日誌長時問題與復原見PROTOCOL。新負載回歸含完整程序/WebView退出；日誌197MB已獨立核對，不需要每次重跑。重跑可用 `build/client/P11.ClientEngine.exe --journal-test evidence/new-journal-folder`（必須新目錄）。95秒真實UI以 `python -B -X utf8 -c "from world_integration import ui; ui('long')"` 執行。

95秒原服UI已過：7次keepalive/time reply，2977 timer ticks、3944次繪圖、max gap78ms；398筆原server包獨立序列/HP/位置/時間核對通過。高頻UI合成負載為5006連續接收時間包、479 ticks/1212次繪圖、max gap336ms、取消6ms；尚未接收的已傳送資料因明示取消不列已套用，不裁改已接收capture。

最終受影響回歸：objects suite/UI皆pass，獨立28物件/756欄位與21物件/567欄位；movement suite/UI皆pass，16方向往返、原拒絕校正、保存重登及雙向原封包測量邊界通過。WebView20項含長戰鬥字串換行、焦點、白名單、三次重開通過；原生WebView預覽為evidence/web-regression/web-combat-wrap.png（明示合成字串排版負例，不當戰鬥證明）。

最新五個關鍵 checkpoint（自身攻擊、目標受擊、自身受擊、死亡與後段屍體）的可見率保存在 `result.json/visibleCombat`；數字為共用compositor的opaque pixel歸屬，並另作 PNG 目視。屋頂下的初始選取只作前置，不當清楚攻擊證明。

R1→R2 只修測試走位、距離參考、獨立驗證與交接；`evidence/lure-revision-scope.json` 逐檔核對原產品來源及759環境檔案未改。R1凍結與主管失敗副本在 `evidence/freeze-r1-lure-handback/`，所有本次失敗仍在 history。原未受影響回歸保留其來源與日期，不重新標成R2新跑。主管已獨立完成的R1乾淨四組回放、日誌及資產核對可在來源相同時沿用。

R2工作者封存：原UI pass、PID67792正常exit0；主capture3378筆、旁觀capture4580筆（含seed，原server包4579筆）。獨立16快照/7812欄位、一條死亡/移除鏈、三段軸向雙連線位置與C_Move通過，8次單擊的最短送出間隔859ms。原後段死亡為16:5，符合gfx1110方向0原序列終幀。自然回血約828秒至502HP，在原900秒內完成。最終driver已乾淨建置零警告/零錯誤，未再次登入；它與本輪實跑driver的三處等待條件差異及原assembly SHA已封存，主管將重跑最終版。
