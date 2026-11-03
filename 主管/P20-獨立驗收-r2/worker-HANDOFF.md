# P20 原裝備穿脫與武器外觀

本階段在新副本實作31長劍、52雙手劍、20096環甲、20222木盾的單次穿脫、原服換裝與互斥、原背包裝備標記、S64裝備窗、S8屬性與S119武器型態。gfx0王族的徒手／單手4／雙手50皆使用原資源；主角與旁觀可顯示站立、行走、攻擊、受擊及死亡。這是四件已鑒定+0正常裝備的有限增量，不代表全物品或完整引擎。

交審狀態以ready-for-supervisor.json及result.json為準；沒有ready檔即尚未凍結。實際Goal保持active，同一JAR/DB保留，只有主管正式功能驗收並交還清理後才能正常收尾。

## 來源、邊界與狀態

- 唯一寫入區：本階段與D:/P20Lab-ClientEngine；原始資料、工具、P01–P19及主管目錄唯讀。bootstrap與prepare的真正副本清單、hash及路徑調整見inheritance-manifest.json、copy-manifest.json、adaptation-manifest.json。
- 固定原JAR SHA256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；未patch、重編或改裝備/戰鬥/AI/EXP/掉落規則。只啟動一次正常JAR。DB43320、game42045、合成42046/42047；不連正式2000/3306。
- 初始五SQL、21表3685行遷移、155表及四transit清理、盟屋/村長及保留表審查已在首次JAR前完成。8新角色及有限fixture見initial-fixtures.json。原始玩家資料不作產品來源。動態後只用受保護SELECT作獨立oracle，沒有補血、補物、SQL移動、reset或重啟。
- DB UUID `052f6955-d047-11f1-99cf-4cedfb679789`；freeze內記錄同一PID、父子、建立時間、exe、datadir及連接埠。原Netty wildcard與loopback peer guard保留；server-policy-probe.json如實記錄隔離通過但拒絕後listener不可用的既有限制，不對正常JAR再做LAN probe。

## 程式與原始證據

主要增量在source/Equipment.cs、Inventory.cs、InventoryUi.cs、EquipmentIntegration.cs、EquipmentUiTests.cs、ObjectAssets/ObjectRendering及CombatRules相關檔案。產品透過原C164請求，等原回包確認；不樂觀更新、不以SQL填UI、不由模板預算AC。Web同樣明確穿上/卸下，顯示裝備狀態及原角色屬性。未支援型態不以徒手替代。

協議欄寬、JVM bytecode offsets、原內部bless0與wire1的差別、原128/129拒絕及來源hash見PROTOCOL.md與reference/equipment。oracle.tsv的14組合成serializer向量SHA256 `e5ae3ac75c2dbfe4805dd93e4d246318ffa6b6b530b324d95b5c827638421296`。104實體SPR/816幀與狼40SPR/248幀經原Lin.Helper.Core.LoadRaw獨立逐像素/座標比較；圖示取原Sprite.idx/PAK。原action8誤解析修正在本階段留痕，不覆寫舊階段歷史。

## 實測與完整失敗鏈

CLI首輪在已完成原裝備/拒絕後，測試要求走回旁觀占用起點而失敗；r2原狼回家S120移除再S87出現，目標被原規則清除，0攻擊。控制器改成首次攻擊前等待同一原NPC原位置穩定再作新的明確選取，攻擊開始後不自動重選、不放寬死亡/移除停止。CLI r3 Fresh=false成功，106雙端快照、2862裝備/屬性欄位獨立一致，兩武器攻擊/受擊、原128/129、死亡/移除、EXP+20、同堆金3→47、重登保存均通過。藥6、卷1、肉8與四裝備仍在，旁觀沒有收益。

UI首輪第一次U沒有送出C164；r2增加等待仍因遊戲焦點未成立逾時；r3正常Activate後Focused=true，但ActiveForm仍false而逾時。三輪均保留0攻擊/0裝備請求證據。首登原服AC10→-1，不能宣稱whole before==after。r4控制器用正常BringToFront、Activate、SetForegroundWindow及ReturnGameFocus，實測ownHandle=foregroundHandle、ActiveForm==this、!web.ContainsFocus才按鍵；不偽造ActiveForm或跳過產品輸入隔離。各輪只接上一輪正常保存狀態，見resume.json與evidence/finite-ledger.json。

UI r4 Fresh=false成功：180雙端快照、4860裝備/屬性欄位獨立一致；3次明確單擊、兩武器攻擊與受擊、原128/129、原死亡/移除、EXP+20、同堆金3→45、正常重登保存。藥6、卷1、肉8及四裝備均保留，未補充也未消耗藥水，旁觀無收益。兩個成功主角最後在32591,32945、AC=-4、雙手劍與環甲已裝備，長劍/盾未裝備，SQL與原重登包一致。原HP保存CLI648/UI600，不重設為滿血。完整失敗鏈5輪均保留；CLI r3與UI r4都是續跑成功，不能稱fresh成功。

UI測試是WinForms controller/OnKeyDown與實際WebView2 DOM，不是外部OS鍵鼠。client.png為DrawToBitmap產品渲染，web.png為獨立WebView CapturePreview。screen.png可能捕捉其他前景程式，不能以screen-copy成功宣稱客戶端在前景；此類圖不作產品驗收證據。PNG須由實際view_image檢視並在visual-review.json記錄。

原向量、截短、重複絕對狀態、未知物件、取消/舊世代/忙碌/死亡/移除/選取改變均有明示合成測試。八項既有地物/背包/icon/combat/object/world/movement回歸與Web焦點/bridge/reopen測試見evidence。合成通過不冒充真實互斥分支。

## 重跑與主管驗收

2026-10-25主管v1驗收已完成真正副本乾淨重建零警告/錯誤、11項回歸與fresh CLI（102雙端快照、3原攻擊）通過。主管fresh UI在已完成128、穿雙手後、129前失去前景，SetForegroundWindow=false、ActiveForm=false，72雙端快照/0攻擊；原雙手+環甲AC=-4正常保存。這輪失敗位於主管/P20-獨立驗收/world-evidence/equipment-supervisor-ui，不得重設或稱fresh成功。

主管明確交還worker_revision後，v1 freeze/ready/result/交付說明/測試收據及原EquipmentUiTests已真正封存evidence/freeze-v1。v2只改EquipmentUiTests.GameFocusReady：最多30秒、最多15次、約每2秒正常BringToFront/Activate/SetForegroundWindow，等待真正GameInputAllowed，逐次記錄原生handle/ActiveForm/焦點；斷線或死亡立即停止，不偽造焦點、不改產品gate或CLI。Web事件日誌equipment-test-focus-wait可即時觀察等待。沒有重跑worker動態或消耗角色資料；僅乾淨重建及原向量/gate離線回歸。revision_v2.py驗證函式外原碼完全相同、其他既有source均未變、環境841檔hash與同服務一致，8角色保存狀態逐項等於各自最近db-proof。主管CLI已通過，v2主管UI須Fresh=false接原失敗保存狀態；新ready不再宣稱主管四角色fresh。

在本階段PowerShell執行 `./build.ps1 -Clean` 可從來源乾淨重建，輸出build/client/P11.ClientEngine.exe。`python -B -X utf8 run_equipment_tests.py` 執行原向量與gate；原像素比較使用verify_equipment_pixels.py與verify_combat_pixels.py。這些不登入或寫DB。原JAR/DB已在運行，禁止重跑prepare、initialize_fixtures、migrate或任何舊修復/SQL變更腳本。

v1首次freeze後的主管fresh命令為 `python -B -X utf8 equipment_integration.py cli supervisor` 及 `python -B -X utf8 equipment_integration.py ui supervisor`，目前均已執行，不可重跑fresh。四主管角色僅保留初始狀態至v1首次freeze；v2依上一段正常保存續跑。憑證只在LAB/private-tests.json，不在公開交付。每一成功/失敗result都分別執行 `python -B -X utf8 verify_equipment.py <result.json>` 及 `python -B -X utf8 verify_rewards.py <result.json>`。不可覆寫既有證據目錄，失敗需先核對raw與保存狀態。

source-freeze.json、environment-freeze.json與delivery-manifest.json分別鎖定可重建程式/資源/工具、靜態實驗環境與公開交付。排除build/cache/live datadir/log/private憑證的含義見manifest，不把私密憑證或舊build當可重用環境。freeze後工作者不改來源、不登入、不重啟、不清理。

只有主管所有獨立驗收通過、全部session/client/WebView釋放，並在主管目錄產出status為functional-acceptance-passed-handback-for-cleanup且hash匹配的收據後，才執行cleanup_final.py：原console shutdown 0→等JAR及launcher退出→驗同DB→SQL SHUTDOWN→等DB及launcher退出，無強殺。保留原exit1/ShutdownControl告警；最後實際Goal complete並idle。不自行P21。

## 限制

沒有魔法、遠程、商店、交易、倉庫、全地圖或產品自動戰鬥/追怪/補血/拾取。未聲稱自然地物拾取或真實升級。本階段原AutoLoot2自然同堆金收益不等於地物拾取。P18原UI8肉去向未證與未畫拾取動畫、P19無自然地物與真實升級、P10舊EXE未驗收等歷史仍保留。完整引擎總目標仍需後續整合驗收。
