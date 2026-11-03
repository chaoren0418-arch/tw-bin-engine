# ENGINE-SLOW-REVIEW-R1 獨立契約

範圍：Speed2、Brave0；其他速度、Brave與額外技能倍率不擴張。固定BIN e12949cf…641a2、JAR d287bc4b…41cf；原動作基底仍來自各角色／武器profile。先建立契約，再看實作者封存候選；本契約未取用實作者的預期值或測試結果。

## 原始證據

| 路徑 | 已核對語意 | 驗證強度 |
|---|---|---|
| BIN VA0xA407D0，image base0x400000 | speed-family switch：0原值、1 `(3*t)>>2`、2 `2*t`；Brave另處理 | 既存functions的全部chunks逐byte與固定BIN核對；36組原x86指令Unicorn執行。metadata/status callees明示替身，沒有執行整個原客戶端 |
| BIN VA0x94C530 | d/c/h讀物件ID、mode、duration；更新actor+195，自己的mode2有獨立狀態圖示路徑 | 固定bytes與既存指令／偽碼靜態核對；沒有偽稱此收包函式已動態執行 |
| JAR ActionIntervalCalculator.calculate(int,ActionType,SpeedState)，方法內offset19–34 | slowed先於haste，乘2.0；適用MOVE／ATTACK／SPELL，再處理其他旗標 | 實際固定JAR純函式36組×3動作=108次核對，沒有建立L1Character、服務或DB |
| 同方法offset37–51 | haste乘0.755，與native動畫0.75分開 | 以7、333、641等非整齊基底檢查截斷，不把動畫值當發送值 |
| S_SkillHaste建構式offset9–28 | C255、D objectId、C mode、H seconds | class與固定JAR entry逐byte相同，既存javap核對；引擎以合成8byte封包通過實際WorldSession.Receive |

所有VA/RVA/fileOffset、chunks SHA及中和callee列表見native-oracle.json。固定JAR三個純計算class的SHA見jar-oracle.json。舊路徑透過根project-manifest.json解析；未直接執行舊腳本，也未搬／改原BIN、JAR、SQL。

## 時間契約（無額外倍率）

| speed | 動畫 | move最小送出間隔 |
|---|---|---|
| 0 | base | base |
| 1 | floor(3×base/4) | max(動畫, trunc(base×0.755)) |
| 2 | base×2 | base×2 |

gfx0步行base640：0=640/640，1=480/483，2=1280/1280。gfx0匕首attack基底840的slow要求1680；光箭spellDir1320的slow要求2640（本地既有安全餘裕另加，不乘進模板值）。這些值來自reviewer固定來源及oracle，不來自候選。

1. S255是狀態權威，顯示秒數到期不能本地把Speed2改0；mode0/1/2必須可互轉，mode2不能顯示「加速中」。未知mode與Brave≠0維持原有拒絕，不借此開放其他速度族。
2. 自己與其他物件的接收要分離。陌生objectId不能創造新物件；遠端speed變更不能改自己的speed/revision或取消自己的路徑。
3. 已送出一步的動畫採入場時鎖定的NetworkStepMs；中途狀態回包不得重啟當步、瞬移、倒退或讓步數多算。下一步採新狀態。這是保守引擎契約，沒有宣稱原BIN中途切速的完整政策已重現。
4. 最後實際送出時間不因速度回報、取消未送輸入或同值重報歸零。0→2時ready至少lastSent+slowInterval；2→0時已准入的慢步deadline仍須完成。既有max(nextMovementAt,lastMovementAt+currentInterval)是待保留的安全底線。
5. 佇列送出前mode／revision變化：取消舊請求或在真正admit時完整重算，不能拿舊快間隔送緩速動作。本review helper先驗既有保守取消政策；若候選採等價重驗政策，需補相應安全證據，不能單以案例名稱拒絕更安全的合法策略。
6. 同值S255重報可保守取消未送滑鼠路徑，但不能重置已送時鐘／續一整步動畫，也不能無限自動重試。顯示倒數可更新，遊戲狀態仍依回包。
7. Cancel／新session／重登入清除舊actor、倒數、lease和送出時間；舊queued callback不得作用於新角色。不連線也能測Cancel與重建fixture；完整登入仍非本輪實測。

## 消費鏈與風險門檻

- `OnlineMovement.cs:34,39–40,74–88`：MovementAvailable→SendMovement，lastMovementAt與speedRevision防線。`PlayerPointer.cs:40–41,49–66`：滑鼠route stamp包含speedRevision，每tick與最後admit檢查。開放Speed2後必須確保它能走路而非停在unavailable；不改pathfinder規則。
- `ClientLogin.cs:52`→`World.cs:24,43–54`：自己的NetworkStep以from.Speed建立固定當步時長；後續不要用新的速度改寫當步。整體WinForms播放未在本輪helper執行，候選需保留這段契約。
- **攻擊／施法連帶放行風險**：`Combat.cs:79,87–98`、`Magic.cs:63–73` 都以MovementAvailable為入口，基底duration目前未按Speed2調整，也未捕捉speedRevision。候選只改NormalSpeed.Supported就會把先前封鎖的攻擊／施法解鎖，這不等於安全。要求Speed2動作按固定JAR間隔／正確動畫處理，或清楚維持相應動作拒絕；不得以步行需求為由忽略提早送出。helper有慢速直接動作及0→2排隊途中切速兩類檢查。
- `Inventory.cs:107–117`：ItemAvailable本來不依MovementAvailable，但使用需要MovementBusy=false，最後送出又檢查MovementReadyAt／nextCombatAt。補血／回家不應套上2倍道具延遲；仍不得越過正在走的慢步或其他互斥動作。物品冷卻保持原模板，不重新設計藥水。
- **遠端物件動畫風險**：`ObjectRendering.cs:21–25` 以當前speed重算stepMs，moving進度直接取 `(now-MotionAt)/stepMs`。若同一步320ms時由640改1280，公式進度由0.5變0.25，有倒退風險；2→0也可能提前結束。這是目前程式公式的靜態推導，**未作renderer實測**。候選需鎖定當步時長或提供等價不倒退的時間重基準證據。此行同時控制物件是否unavailable，必須驗mode2不再整個隱藏。
- 遠端combat分支仍以a.Duration及原age取幀，自己combat/spell rendering另有消費鏈；若開放slow戰鬥，需同時核對時長／release與實際送出，不只改按鈕available。保留阻擋亦須說明功能範圍。

以上為候選驗收風險，**不是已審查候選的缺陷清單**。候選尚未封存時不宣稱接受或拒絕其實作。
