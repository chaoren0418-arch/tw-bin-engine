# Issue #115／T063：DEMO-3 目標選取、數量回饋與權威座標

基線：origin/main `0b963fcc`；工作樹 `scratch/wt/task-115`。本節先於程式修改寫下，後續補驗證結果。

## 已驗證

四項產品修正完成，最終 Windows dev 建置、check-only 與 **9,374 項離線斷言**通過。真機第2c輪仍待dot；第5項即時白名單／販售庫存受禁止讀DB限制，僅交付唯讀JAR條件、已有成功證據的普通肉候選及排除清單。

1. `OnlineInput.cs:9`：G 是攻擊鍵；`Program.cs:98`、`KeyboardNavigation.cs:6` 要求表單焦點。`PlayerPointerUi.cs:15–26` 依實際不透明像素命中選取，未命中會走地面。`Combat.cs:87–108` 不限制本地攻擊距離，但要求有效目標、存活、速度外觀可用、動作閘門與目標座標一致；目標移動於排隊期間會拒絕且未更新提示。`OnlineInput.cs:9` 不把攻擊結果帶入玩家 HUD。#77 截圖沒有已選取黃圈，不能只靠 opcode trace 判定當時是哪個 guard。修正方向：具體拒絕訊息與 HUD 回饋、選取成功提示；保留既有閘門，不盲目追擊。
2. `MagicTargetPreparation.cs:20` 明示 test controller only，與正式 UI 無關。`Magic.cs:22–25,65–77` 光箭自動要求敵方目標；`MagicUi.cs:18` 原生下拉僅切自己／目前對象，沒有可見敵人清單。`PlayerHudView.cs:146` 光箭的目標模式按鈕停用，未清楚指示先點場景目標。web 已有可見像素清單，但每次 HP／MP 變動會重建下拉。修正方向：原生可見目標下拉、明確目標提示、web 下拉只在選項變動時重建。
3. `GroundCatalog.cs:41–49` 拒絕數量只寫 GroundNotice；`GroundUi.cs:22` 丟棄後只刷新地面；`InventoryUi.cs:24` 背包仍讀 ItemNotice 與 LastServerMessage。故沒有 C25 但原生背包仍顯示舊125。修正方向：本機拒絕同步背包文案，清除舊伺服器訊息並刷新；web 本機錯誤保留到重新選取或下一次有效操作。
4. `panel.js:20,32` 顯示 Actor 的本地預測座標；`OnlineMovement.cs:19–27` 已有 AuthoritativeActor，但沒有單獨顯示，且自己 S10 被 `WorldObjects.cs:47` 當非自己物件處理。修正方向：獨立權威 map/x/y、回包種類／序號／預測後未確認標記；S87、自己的S10才更新，不從 Predict 更新。無逐步 ACK 時不能宣稱當前終點已確認。

## 推測

歷史零攻擊可能涉及未命中移動目標、焦點或忙碌；僅靠 opcode／截圖不足以唯一歸因。肉消失原因須以唯讀 JAR 條件與既有證據分析，不以假設當結論。

## 待真機驗證

修正後由 dot 進行第2c輪；本任務不啟動遊戲、伺服器或 DB。白名單實值與商店庫存若僅存於 DB，依禁止讀 DB 的限制保留未知。
## 修正結果與操作證據

上列行號記錄修正前基線；以下為本次交付行號（可由 Git 差異核對）。

| 項目 | 修正與證據 | 離線回歸 |
|---|---|---|
| 近戰 | `Combat.cs:87` 起維持原有 `AttackCore` 閘門，明確區分未選取、忙碌與排隊期間目標移動。`OnlineInput.cs` 的 G 將結果顯示到 HUD（先消化ClearInput產生的舊滑鼠取消提示，避免覆蓋本次結果；目標改選後不寫回舊結果）；`PlayerPointer.cs` 選取後顯示 ID 與操作方式，滑鼠拒絕也有訊息。G／web「單次攻擊」／雙擊或 Ctrl＋左鍵皆是單次攻擊；沒有自動追擊。程式沒有本地距離上限，是否命中仍由原服判斷，測試請先走近至相鄰格。 | 真正產品 DLL 的 Attack／PointerAttack 經記憶體 sender 產生 C229，目標 ID 正確；目標在最終送出檢查前移動時不送並顯示原因，再手動按一次使用新座標；冷卻、死亡／移除等保護不放寬。 |
| 光箭 | `Magic.cs` 的技能4本來就用目前目標，不是點「自己」模式切換。`MagicUi.cs:9` 起新增「可見施法目標」清單，和 web 使用相同實際像素可見性及存活／外觀檢查；清單開啟時不重建。`PlayerHudView.cs` 顯示先點場景敵人的說明；web 清單不再因 HP／MP／訊息變化重建選項。`MagicTargetPreparation.cs` 沒有修改，因為它是測試準備器。 | 真正 CastSelectedMagic 送 C6、skill4 編碼與敵方 ID／座標；移動時拒絕有訊息、手動重試成功、MP不預扣。另以實際 ComboBox 呼叫產品 `SyncMagicEnemies`，測空清單、可見目標、選項保留與死亡排除；未建構／顯示遊戲表單。 |
| 數量 | `GroundCatalog.cs` 新增共用拒絕回饋，清除 `LastServerMessage`／`LastServerReply`，丟棄錯誤同步寫入 `ItemNotice`；`GroundUi.cs` 丟棄後刷新背包。內容版本不變，錯誤不會讓物品快照失效。web 本機錯誤保留至重新選取或有效提交，不被忙碌狀態重繪覆蓋。 | 先餵 S71/125，再送0／超額／負數／溢位／空字串：0筆C25，舊125清空、背包及地面錯誤一致、物品數量／內容版本不變；實際 panel.js 重繪測試另驗畫面文字。 |
| 權威座標 | `OnlineMovement.cs:19` 起新增獨立 map／x／y、回包opcode、遞增序號與「移動後尚無位置回包」標記。只在自身S87／S10更新；S10使用該回包的來源格加方向推導伺服器目的格，不使用Actor預測座標。換圖／登出清空。`panel.js` 的頂部座標與世界區塊分開顯示原服座標和畫面預測；HUD標題也附權威字串，窄視窗以診斷面板完整字串為準。 | S87、自己S10八方向、其他角色S10、本地Predict、換圖、重登／重置；web狀態事件中故意讓預測999／999與權威32600／32940不同，畫面採權威值。 |

**歷史根因界線：**#77 的 opcode trace 不含 payload／選取狀態，不能重建當時是哪一道攻擊 guard。`demo3-06-selected.png` 未見黃色選取圈，下方仍是行走取消訊息，與「沒點中移動牛」相容，但只是推測。這次修的是已確認的缺少回饋與選目標可操作性，不宣稱已重現當時唯一原因，也不宣稱原攻擊／光箭 wire 本身錯誤。

## 唯讀 JAR 證據：可丟條件與清理

來源仍是 `environments/p24/server/Server_Game.jar`，SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`，與 #49／#77 相同。`javap -J-Dfile.encoding=UTF-8 -c -p -classpath <jar> <class>` 只解析類別，不執行 handler。七個 class SHA、javap輸出SHA、精選方法原文與摘錄SHA見 [jar-evidence.json](../../work/issue-115/jar-evidence.json)。以下數字均為 **JVM 方法內 bytecode offset**，不是 File Offset／RVA／VA。

- `C_DropItem.start` 5–26：C25依序讀x、y、背包ObjectId、signed count；47–56拒絕非正數；63–107拒絕ghost、非持有物品及數量≤0。
- 108–158：非GM時，若 `!ConfigAlt.DORP_ITEM && !ItemDropTable.isDroppable(templateId)`，回S71/125並返回。159–233另外要求可交易且非限時物品；234–434仍會拒絕使用中的寵物道具、娃娃、已裝備、封印等。白名單不會覆蓋後續限制。
- `ConfigAlt.load` 498–510：讀取 Properties 的 **`dorpitem`**，缺省字串 **`true`**，以 `Boolean.parseBoolean` 設 `DORP_ITEM`。這是程式缺省，不是本服目前實值；本輪完全沒有讀取任何 properties／帳密設定。
- `ItemDropTable.load` 25：白名單真正內容來源為 **`SELECT itemid FROM 道具_物品強制丟棄`**。65–93排除不存在的物品模板，94–108加入集合；`isDroppable` 0–13僅 `Set.contains(templateId)`。JAR沒有SQL種子檔或內建物品清單，**不能由JAR得出目前白名單全部列值**。
- `ShopTable.load` 40：販售內容來自 **`SELECT * FROM shop WHERE npc_id=? ORDER BY order_id,item_id`**；所以也不能從JAR硬編出現在某位NPC必賣哪些物品。只讀這段SQL常數，沒有執行SQL或讀DB檔。

地面規則：

1. `L1GroundInventory.storeTradeItem` 16–23將落地物品加入計時集合；`setTimer` 0–28排除模板40515及屋內地面，29–37加入 `ServerDeleteItemTimer`。
2. `ServerDeleteItemTimer.start/scheduleTimer` 以1000ms週期執行。`runCycle` 0–11以 `ALT_ITEM_DELETION_TIME * 60` 為週期，≤0停用；12–27累加**全服共同**計數器。29–94在剩30／15／5秒廣播提醒；97–107到期歸零，110–240巡集合刪除，243–255廣播「已完成清除地面道具。」。
3. `remove` 50–79確認仍在地面容器中，148–168移除整個現存堆疊。不是每件物品落地後保證存活一個完整週期；剛丟在掃除前也可能立刻消失。
4. 另一條路徑：`L1NpcInstance.searchTargetItem` 89–160從可見地面容器挑可加入背包的物品；`onTargetItem` 接近物品；`pickupTargetItem` 189–204把整堆移到NPC背包。這只能證明存在此路徑，不能證明當時是那隻牛撿走，也沒有查NPC模板或實際AI狀態。

**肉為何很快消失：**全服週期掃除是已核實、足以造成此現象的機制；#77 的數量錯誤截圖也可見「已完成清除地面道具。」廣播，證明該輪曾發生清理。但缺少肉移除當刻的回包payload與清理時間對齊，因此「當次一定被掃除」仍屬推測；其他玩家／NPC拾取也未排除。不得寫成客戶端把肉吃掉或確定NPC吃掉。

### 給第2c輪的候選清單與證據強度

| 物品 | 可疊加／客戶端支援 | 本服可丟證據 | 合法取得與裁定 |
|---|---|---|---|
| **普通肉40056**（token `$23`、背包icon124、地面gfx36） | #49模板證據及本次GroundCatalog；可疊加，已啟用 | **#77第2b輪實際丟1成功**，肉1→0且地面有肉 | **首選且唯一已有此服成功證據的候選**。dot先透過NPC購買清單尋找普通肉，以角色既有金幣購買至少11；必須以實際販售清單與背包回包確認，不指定未經驗證的NPC名稱。若商店未販售，回報取得受阻，不補DB或改白名單。 |
| 箭40743（token `$61`、icon7、gfx66） | 可疊加，客戶端支援 | `RangedIntegration.cs:21`及P21盤點已記原服125拒絕 | **排除本輪主候選**；即使商店可買也不能當作可丟替代。 |
| 治癒藥水40010 | 可疊加，客戶端支援 | #77第2b輪丟1／10均125，持有41不變 | **排除本輪主候選**，不得重複沿用#77第1輪的未確認推薦。 |
| 金幣40308 | 可疊加，客戶端支援 | #77／B14已拒絕 | 排除，不改伺服器繞過。 |
| 綠藥水40013／白藥水40012／回家卷軸40079 | #49有模板證據；本階段catalog缺地面pin的列停用 | 未確認 | 不是本輪可交付的丟撿候選；不擴張#109素材範圍。 |

**未完成界線：**在「不讀DB／設定、不啟動伺服器」條件下，不能提供即時白名單列值、保證目前NPC有貨，或第二個已證實可丟物品。上述是經證據篩選的候選清單，不是假裝已完成購買。此差額交dot透過既有合法NPC流程核對；若不存在合法取得路徑，由主控裁定替代驗收，不能要求AI改資料。

## 給 dot 第2c輪的操作步驟

1. Claude審查、使用者合併後，由本機執行器在與其他runner互斥的指定目錄重建dev，核對DLL／manifest與check-only。dot再依既有授權操作P24Lab測試角色；本任務不改主目錄入口、不啟動服務。
2. **近戰：**收起面板，F10回遊戲焦點。先靠近牛、停下等「可操作」，單擊牛身上可見像素，必須看見黃色圈與「已選取…#ID」。若點到地面會走路，不能當作已選目標。按一次G並放開，截HUD訊息及trace的C229（弓為C123）；也可雙擊／Ctrl＋左鍵，或F9診斷面板「單次攻擊」。若顯示目標移動／忙碌，重新靠近停下後手動按一次，不連打。送出只證明請求，還須取得S30、傷害／HP／死亡與EXP證據。
3. **光箭：**「服務」→「魔法」選已學技能4光箭，再從新增「可見施法目標」選牛的ID，核對對象文字，按「手動單次施法」。或HUD技能選光箭、收起抽屜、點中場景牛、確認黃圈、F10後按M。光箭不需要切到「自己」，停用的自己模式不是敵方清單。F9的魔法目標清單也可操作。清單空就表示沒有符合可見像素的有效目標，先調整站位；不繞過遮擋。取C6、原服光箭效果與MP差。
4. **數量：**背包選持有量N的物品，填0、N+1各按一次「手動丟棄選取數量」。背包應顯示「未送出：數量不合法…」，舊125消失，C25不增加，數量不變。F9 web面板另驗一次，等狀態重繪仍不能回到舊125。
5. **座標：**F9診斷面板上方及世界資訊讀「原服座標 map / x / y · S87或S10 #序號」，不要讀「畫面預測」或牛的座標。移動前截一次，八方向及點地各測完立即停下再截。若顯示「移動後尚無位置回包」，原服沒有提供新位置，**該截圖只能證明最後收到的權威值，不能驗收新終點**。必要時在每個待驗終點停止後正常登出／重登，取得新S87（每一段獨立做，不能以最後一次重登位置替代所有中間終點）。不加新查詢封包或用C29推測值冒充權威。
6. **丟撿：**首選普通肉40056，先經NPC購買清單取得至少11，記錄購買前後金幣及肉回包。觀察清理廣播，剛清理完再做，避開NPC／他人；丟1後立即雙擊同格肉拾1（或地面頁選取後填1按拾取），核對背包N→N−1→N；再做10。每次等前次回包／冷卻完成，保留C25、C112、S87／S120／背包數量證據。若掃除或被拾走，照實記錄，不補發。肉是食物，**不要按U／「單次使用」／肉快捷格**，那是吃肉。
7. 上述成功後再做非滿血回血與各事件有聲／靜音／恢復量測；本PR離線測試不算M1或第2c輪通過。

## 產出路徑與禁止事項核對

- 本報告、`docs/demo/RUN.md`新增第2c輪操作說明。
- 產品來源及唯一變更的lock：`engine/source/`、`engine/web/panel.js`、`engine/_integration/dev/rebuild-lock.json`。
- 可重跑回歸：`engine/_integration/dev/demo-tests/`；正式證據：`work/issue-115/`。
- 原始命令、javap與暫存：本工作樹 `scratch/issue-115/`；主程式及測試DLL在 `engine/build/`，中間產物在專案 `obj/`／`bin/`，launcher在既定 `engine/launcher/`。
- 主目錄分支維持 task/102；未checkout、commit或建置主目錄。只在新工作樹建立兩個先前不存在的assets／vendor引用；既有junction、原始素材、封存release與r33／r34／r35 lock未修改。一次junction自動審查因路徑判讀拒絕，提供絕對路徑及不存在證據後通過；無待處理拒絕。
- 未讀或改DB、設定／帳密，未啟動遊戲、P24Lab、MySQL、Java或服務，未操作既有程序；`docs/REPORT.md`、#109的 `engine/web/icons`／`ground`、`.gitignore`與CI拒收腳本均未修改。dev lock與#109日後合併可能需重新生成，不可手動拼接hash。

## 待真機驗證／給下一棒的提示

交Claude審查PR、使用者合併；本機執行器準備互斥建置，dot執行#77第2c輪。攻擊命中、MP／EXP與音效、原生面板實際排版／鼠標操作、肉購買及1／10完整丟撿都仍待真機驗證；離線合成封包、DOM替身、記憶體sender與未顯示的ComboBox不是實機證據。白名單內容、實際清理間隔與當時肉消失唯一原因仍未知。沒有需要使用者改設定或查DB的步驟。

## 驗證結果與重現

| 驗證 | 結果 |
|---|---|
| dev --update-lock／--build／--verify | 全部exit0，190來源／199 pins，PASS dev |
| 主程式／launcher／四個離線宿主建置 | 全部0警告、0錯誤 |
| engine\啟動新版客戶端.cmd --check-only | exit0，revision=dev，uiStarted/networkStarted/servicesStarted=false |
| DemoChecks | 109 項（含實際ComboBox方法） |
| DEMO panel.js | 16 項 |
| 原 GroundChecks／ground web | 361／72 項 |
| 原 CombatSfxChecks | 8,780 項／7組，不播放音效 |
| 原 PacketTraceChecks | 36 項 |
| 總計 | **9,374** 項 |
| git diff --check（cr-at-eol） | 通過 |

完整命令、exit code、stdout/stderr及check-only JSON見 [validation.json](../../work/issue-115/validation.json)；個別斷言結果同目錄。最終身分：

- `engine/build/client-product-dev/P11.ClientEngine.dll` SHA-256 `ff60a7facc7dd5fa6d227811dc46e00099d2348da631f4fc9edfbf3c8eaffc2f`。
- `engine/build/product-dev-manifest.json` SHA-256 `dab3590f974075fdaa7c389b6f4b42045a53782879b35afa4e93e8876e52e753`。
- `engine/_integration/dev/rebuild-lock.json` SHA-256 `49eb0eef7a1e726d8227eb50c9d73b70ec3c2b23f5a5b76545d21147dad7df14`。

重現時在本任務工作樹根目錄，沿用本機SDK10.0.300、Python3.12、Node。先將TEMP／TMP、DOTNET_CLI_HOME、NUGET_PACKAGES、NUGET_HTTP_CACHE_PATH、NUGET_SCRATCH設到該工作樹scratch子目錄，停用telemetry與首次certificate生成。素材／vendor使用工作樹的唯讀引用；不搬素材、不建置主目錄。以下每一步非0就停止：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-115/rebuild
& '.\engine\啟動新版客戶端.cmd' --check-only
dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -o engine/build/issue-115-demo --configfile engine/NuGet.Config
dotnet engine/build/issue-115-demo/DemoChecks.dll . work/issue-115/demo-result.json
node engine/_integration/dev/demo-tests/web-checks.cjs . work/issue-115/web-result.json
python -B -X utf8 engine/_integration/dev/rebuild.py --verify
```

其餘既有回歸命令完整保存在validation.json，所用DLL皆為本次最終建置；未以舊結果冒充。CI結果於PR完成後補記，Linux交叉建置不等於真機驗收。

## 第 2 次嘗試：審查退回根因（修正前記錄）

- `GroundCatalog.cs:34–36` 的 `RejectGroundQuantity` 僅更新提示與清除舊伺服器訊息，卻無條件增加 `GroundRevision`。`GroundCatalog.cs:39–42` 把該版號納入拾取快照相等檢查。
- `GroundUi.cs:31–33` 的 `SyncQuantitySelection` 只在 epoch／ID／selection 改變時更新快照；故同一物品輸入 0 後改為 1，仍持有舊 revision，被 `SendGroundQuantity` 拒絕。丟棄輸入錯誤也會誤使既有拾取快照過期。
- 修正方式：提示更新不增加地面內容版號；保留 `InventoryRevision` 的 UI 刷新及舊125清除。實際 S87 更新、移除、重新選取仍沿用原有版號保護，不自動換新快照繞過檢查。
- 新回歸先在 `c0cf9a9` 的產品 DLL（SHA-256 `5953dae58a5f0aad5b8c912d1bf663024380dcc8f9447003d0c62187187b663e`）重現失敗：`corrected pickup succeeds without reselection True0`，退出碼1。修正後同一路徑通過；新增38項斷言，DemoChecks共109項、全套共9,374項。

### 本次已驗證

- `GroundCatalog.cs:36–37` 僅移除錯誤提示對地面 revision 的遞增；UI 仍直接讀取 `GroundStatus`，舊125仍清除。
- `demo-tests/Program.cs:79–118`：實際呼叫原生 `SyncQuantitySelection`，先輸入0／超額，再用同一選取直接改成1；每例只送一次C112，核對ObjectId與數量且不預扣地面數量。另驗丟棄錯誤不影響已選地面物品。
- 數量改變、數量改回原值（ABA）、物品移除、重新選取與排隊期間S87更新，仍拒絕舊快照且0筆C112。
- 本次重跑四個離線宿主與兩組web檢查、dev lock／build／verify及check-only，全數通過。最新完整命令、身分SHA及結果已覆寫同一份 `work/issue-115/validation.json`；既有JAR唯讀證據未變更，本次未重新讀JAR。
- 沿用前次已隔離的 `scratch/wt/task-115`／`task/115`，fetch確認後保留既有修正提交；沒有切換主目錄或重設分支。原生實機未啟動；DB、設定、封存與#109範圍皆未變更。

### 推測／待真機驗證／給下一棒的提示

- 歷史真機問題及可購買肉的限制沿用上文，沒有新增真機結論。
- dot第2c輪追加：選一堆普通肉，在原生地面頁輸入0並按拾取，確認本機錯誤且0筆C112；**不要重新點物品**，直接改成1再按拾取，應有1筆C112。另以超額數量重做，並核對伺服器地面／背包回包；若測試期間物品被清除或數量改變，舊選取被拒絕是預期保護，須重新選取後再測。
- 交Claude重新審查既有PR #118；CI結果以該PR新提交的檢查與回覆為準，再由使用者合併、dot補測。
