# #1186：M7 原生真機 smoke 重測

結論：部分通過（✅ 7／❌ 10／⚠️ 42）。這是實際完成程度；⚠️ 中有尚未執行的子項，沒有聲稱完整跑通 59 列。失敗程式未修改。

## 已驗證

- 前置 #1185（f1155-retest）已關閉且 N03／N04 ✅，採 #1184 B；N01／N02 沿用 #1155。開始時 `p24lab` lease holder 為 `chaoren0418-arch/tw-bin-engine#1186`、到期 `2026-11-14T07:20:25Z`（`preflight.json`）。
- 測試來源為 main `6db34a4aa66bde0eba965946dda3decb08cbc3d6`；`7c31e7ba`／`c8a73fb0` 都是祖先。`engine/build-product.ps1` 建置成功，check-only=`checked-only`／`dev`；沒有修改 rebuild-lock。
- product DLL SHA-256：`58a017171ccd02a63a5003bc6b1a8fc8d21d47e4dad7f287f062420de9fddd24`，不同於 #475 的 `7f861fce…2c77` 及 #1109／#1125 的 `1642ea35…6e93`，與 #1185 相同。audio=`4c4aa97837e7c4e610d3e0b75eb2b619faf41508096435e37c635c2cec456473`；launcher=`478fccf0f19db4579b3861a5b2e00b032966a7fc88f5c6063c44e30ae4591045`。
- 收尾依常設要求同步至 main `e783ff08`，已含 #1195（SEG 修正）；重新建置與 check-only 都成功，新 product SHA=`d42e933fe2e762c815c99fe6288d71e8b6fa879b1500463cbf33e36857aa9223`。本輪真機證據仍屬前述 `6db34a4a`／`58a01717…`；**未用新 DLL 重做真機 smoke**。SM19／SM50 涉及的 `4/80048001` 正是 #1195 已處理的區塊，下一棒承接既有修正重測，勿再開重複 code 任務。
- 前置成立：42061 監聽、JAR 開服完成；p24svc01 登入 S233=0。只作 SM 前置，不重新判 R03–R06／R12／R13／R17。其他 R 項沿用任務指定來源。
- 僅使用測試帳號 p24svc01／p241db6e、P24Lab 42061／43324；測試時 GM=0。未接觸正式 2000／3306，未補物品、未建同名角色、未改血盟或寵物資料。
- 使用 `Test-NativeClient.ps1` 同套 GDI／SendInput 函式；工作區 800×600、DPI 192。沒有使用 computer-use／WGC。開發工具 UIA 僅作繼續測試的替代入口，不能證明原生選角可用。

證據根目錄：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1186-evidence`。下表路徑皆相對此目錄；PNG／log 不進 git。完整證據 SHA 見 `evidence-manifest.json`，摘要見 [issue-1186-evidence.json](issue-1186-evidence.json)。

## 逐項結果

「環境／覆蓋不足」包含測試準備、工具操作或未完成子步驟，**不表示引擎故障，也不表示伺服器不支援**。全列 ✅ 只限本張明列的範圍；SM02／15／55／56／59 依本張要求補子項，沒有把 #475 其餘已得結果重測一遍。

|編號|結果|實際觀察／原因分類|重現／操作|證據路徑|
|---|---|---|---|---|
|SM01|⚠️|環境／判定未完：兩次取得核對中／接手後圖，6565／10137 ms；核對中只有離線場景，未見舊 HUD；耗時是否可接受待裁定。|正常入口啟動兩次，GDI 輪詢。|`smoke-A/m7-SM01-1.png`；`smoke-A/m7-SM01-2.png`；`smoke-A2/startup.json`|
|SM02|✅|本次指定子項：參考 XML 載入成立，含 WidgetSlot、LoginUI、OptionUI 的明文 SHA；未把載入成功當作所有視窗外觀通過。|查本輪 ui-xml-ref／ui-text-ref／surf-ref 日誌。|`smoke-A/native-ui.log`|
|SM03|⚠️|環境／覆蓋不足：取得 800×600 與 1600×1200 工作區；黑邊點擊及原版並排對照未完成。|UIA Resize 後 GDI 截圖。|`smoke-A/m7-SM03-1.png`；`smoke-A/m7-SM03-2.png`|
|SM04|✅|引擎：帳號可輸入、錯密碼出拒絕框、正確登入成功；HKL=0x4040404；登入頁 Exit 正常退出並寫 closed。|輸入測試帳號→錯密碼→正確登入→登出→Exit。|`smoke-A/m7-SM04-2.png`；`smoke-A3/SM04-keyboard-layout.json`；`smoke-A3/window-events.json`|
|SM05|✅|引擎：兩張背景有不同火光／粒子幀，動畫持續；未量測每一幀的精確週期。|登入頁停留並間隔約 400 ms 連拍。|`smoke-A/m7-SM05-1.png`；`smoke-A/m7-SM05-2.png`|
|SM06|⚠️|環境／未觸發：本次 news 空、newsPending=false，未收到可驗收的登入公告。|正常登入後讀回狀態。|`smoke-A3/normal-mage-ready-state.json`|
|SM07|❌|引擎：CharacterList 已有三角色，原生畫布仍黑底「等待真實世界初始化」，沒有選角列；開發工具可列出並進世界。|正常登入 p24svc01→等待選角→按上下鍵；開發工具對照。|`smoke-A/m7-SM07-1.png`；`smoke-A/m7-SM07-2.png`；`smoke-A/m7-SM07-3.png`|
|SM08|⚠️|引擎前置阻塞：SM07 沒有原生選角／Create 入口；職業頁、能力頁、IME 建角未執行。|由 SM07 進建角的入口缺失。|`smoke-A/m7-SM07-1.png`|
|SM09|⚠️|引擎前置阻塞：開發工具可切既有角色；未完成原生建立→刪除→切換全鏈，沒有新建角色。|以既有角色繼續其他測試，不把替代入口算通過。|`smoke-A2/prepare-character-list.png`；`smoke-A3/normal-mage-ready-state.json`|
|SM10|❌|引擎：map0 世界 HUD 可畫，但 HP／MP 數值、百分比、左狀態文字重疊；Y/N 框未取得。|普通角色進 map0，800×600 工作區、DPI 192。|`smoke-A3/normal-mage-ready.png`|
|SM11|❌|引擎：角色站立／移動時，頭上量表與角色身體明顯分離；非整數倍及低 HP 閃爍未完成。|map0 進世界→站立→走一步。|`smoke-A3/normal-mage-ready.png`；`smoke-A/m7-SM11-1.png`|
|SM12|❌|引擎：一般聊天有伺服器回送，但聊天列字高超出列距且底部裁切；八分頁、五過濾、IME 與所有頻道未完成。|一般頻道送 SM1186 map0 test 後看聊天欄。|`smoke-A/m7-SM12-3.png`；`smoke-A3/m7-SM31-4.png`|
|SM13|⚠️|環境／覆蓋不足：已有紫／綠圖示；未施放指定 buff、未取得 6400／82／470 觸發，不能判定對應及時間。|讀回普通角色狀態與圖示列。|`smoke-A3/normal-mage-ready.png`；`smoke-A3/normal-mage-ready-state.json`|
|SM14|⚠️|環境／未建立條件：未取得空血盟倉庫紀錄、鑑定與中央訊息所需回包；未改血盟資料。|本輪未執行指定觸發。|`smoke-A3/normal-mage-ready-state.json`|
|SM15|✅|本次指定子項：WidgetSlot.xml 載入；世界內 Ctrl+P 新增 PNG，檔名、時間與 SHA 已記；Alt+R 日誌出現模式 1→0。|世界內實按 Ctrl+P，再核對 Capture 新檔。|`smoke-A/SM15-capture.json`；`Capture/20261114-135835-970.png`；`smoke-A/native-ui.log`|
|SM16|❌|引擎：Ctrl+H 說明頁開啟但文字互相覆蓋，妨礙連結辨識；巨集八鍵、寄刪信及完整選項連結未完成。|進世界按 Ctrl+H。|`smoke-A/m7-SM16-help-1.png`；`smoke-A/m7-SM16-option-1.png`|
|SM17|⚠️|環境／覆蓋不足：已按 Ctrl+M 取圖；未完成方向箭頭與組隊隊友點核對。|map0 Ctrl+M 開關。|`smoke-A3/m7-SM17-1.png`|
|SM18|❌|引擎：Tab 可開背包，但圖示名稱／數量文字重疊；藥水雙擊、三入口、分類與刪除取消未全部完成。|普通法師 Tab 開背包。|`smoke-A3/m7-SM18-2.png`|
|SM19|⚠️|引擎前置阻塞／覆蓋不足：原法師 map4 區塊 4/80048001 不支援 SEG／壓縮格式；移至 map0 繼續，未到凱倫完成存領。|map4 載圖錯誤後，未執行倉庫存領。|`smoke-A/m7-SM10-1.png`|
|SM20|❌|引擎：法師 Ctrl+S 可開魔法窗，但「光箭」等字超出格子；七職與 map509 拒開未完成。|普通法師 Ctrl+S。|`smoke-A3/m7-SM45-1.png`；`smoke-A3/m7-SM45-2.png`|
|SM21|⚠️|環境／操作未完成：曾選到潘朵拉，但未取得商店買賣頁；取證鎖衝突另記，不當成商店引擎失敗；修理／學法未執行。|map0 選 NPC、嘗試對話；未成交。|`smoke-A3/m7-SM21-3.png`；`smoke-A3/004-pandora-result.json`；`smoke-A3/npc-service-tree.json`|
|SM22|⚠️|環境／操作未完成：雙帳號兩角色同時在世界，但未取得雙方交易窗，未移轉物品或金幣。|A、B 同 map0 相鄰；交易流程未完成。|`smoke-B/m7-SM22-B-1.png`；`smoke-A3/normal-mage-ready-state.json`|
|SM23|⚠️|環境／未建立條件：沒有成功開店與報價，不具備私商買賣／>99999／>8筆條件。|本輪未完成開店。|`smoke-A3/normal-mage-ready-state.json`|
|SM24|⚠️|環境／未觸發：未收到血盟窗、階級與戰爭流程所需回包；遵守不改血盟資料。|本輪未執行備註／宣戰等操作。|`smoke-A3/normal-mage-ready-state.json`|
|SM25|⚠️|環境／未建立條件：背包有既有寵物項圈，但未取出寵物、召喚控制戒指／技能51及盟屋頁條件未成立。|只讀現有背包，未改寵物資料。|`smoke-A3/normal-mage-ready-state.json`|
|SM26|⚠️|環境／未建立條件：已登入法師背包沒有料理書，未收到 S250/52；未重放補物品。|檢查實際背包後未執行製作。|`smoke-A3/normal-mage-ready-state.json`|
|SM27|⚠️|環境／未建立條件：未到公告板並取得文章；發文、刪文、等待期間防重入未執行。|本輪未觸發公告板。|`smoke-A3/normal-mage-ready-state.json`|
|SM28|⚠️|環境／未觸發：未收到 S43 kind1，未執行能力重置；不以合成封包驗收。|本輪沒有重置回包。|`smoke-A3/normal-mage-ready-state.json`|
|SM29|⚠️|環境／未觸發：未取得 S156 清單與拍賣出價頁，價格／關窗／4381 未驗。|本輪未到拍賣公告板。|`smoke-A3/normal-mage-ready-state.json`|
|SM30|⚠️|環境／未建立條件：未建立釣竿、餌與釣魚地點條件，未收到動作71。|未開始釣魚。|`smoke-A3/normal-mage-ready-state.json`|
|SM31|⚠️|環境／回應未確證：原生輸入列可見 /rank，但送出後未取得排名窗；尚未分清指令送出與伺服器回應，不能判不支援。|點輸入列→輸入 /rank→Enter。|`smoke-A3/m7-SM31-before.png`；`smoke-A3/m7-SM31-4.png`|
|SM32|⚠️|環境／覆蓋不足：Ctrl+D 動作窗可開、可觸發邀請選目標提示；未逐一驗完有路線動作。|Ctrl+D→動作圖示雙擊。|`smoke-A3/m7-SM32-open.png`；`smoke-A3/m7-SM35-selection.png`|
|SM33|❌|引擎：本機 HyperText 說明頁文字嚴重重疊；尚未取得指定 NPC 頁滑入前後色值與日期。|Ctrl+H 開 help HyperText。|`smoke-A/m7-SM16-help-1.png`|
|SM34|⚠️|環境／操作未完成：未取得 buddy 頁與封鎖／解除回包，不能以輸入過指令判成功。|先前 /buddy 嘗試後未完成名單操作。|`smoke-A/m7-SM34-1.png`|
|SM35|⚠️|環境／操作未完成：動作窗已提示選組隊對象，但沒有取得 B 的 Y/N 框或建立隊伍；離開／踢除／委任未驗。|動作窗邀請→嘗試選 B。|`smoke-A3/m7-SM35-selection.png`；`smoke-B/m7-SM35-B-1.png`|
|SM36|⚠️|環境／前置未成立：SM25 召喚窗未開，無高等級停用格可驗。|未執行停用格點／拖／提示。|`smoke-A3/normal-mage-ready-state.json`|
|SM37|⚠️|環境／未觸發：沒有 S_EVENT117 血盟倉庫紀錄窗，9183 真機畫面未驗。|不改血盟資料，未觸發紀錄。|`smoke-A3/normal-mage-ready-state.json`|
|SM38|⚠️|環境／操作未完成：未完成書籤新增刪除、IME、19區域與拖曳；不能用 XML 載入代替操作驗收。|原生輸入指令嘗試後未完成窗流程。|`smoke-A/m7-SM38-1.png`|
|SM39|⚠️|環境／未建立條件：未完成普通角色近戰擊殺，沒有前後 EXP／掉落證據。|未執行有效擊殺。|`smoke-A3/normal-mage-ready-state.json`|
|SM40|⚠️|環境／未建立條件：法師有銀箭但沒有可驗弓裝備；未重放補物品，弓箭全鏈未執行。|只讀現有背包。|`smoke-A3/normal-mage-ready-state.json`|
|SM41|⚠️|環境／未建立條件：移動已發生，但攻速、動作競合、表情保留等指定時序未完成。|未做所需連續動作計時。|`smoke-A/m7-SM11-1.png`|
|SM42|⚠️|環境／未建立條件：沒有指定攻擊／座標／範圍特效的完整施放與回包證據。|未以假特效替代實機。|`smoke-A3/normal-mage-ready-state.json`|
|SM43|⚠️|環境／未觸發：未觸發盲目狀態與光圈，無法判定。|本輪沒有盲目效果。|`smoke-A3/normal-mage-ready-state.json`|
|SM44|⚠️|環境／未觸發：沒有實際死亡、321／322 或復活；不得拿一般 Quit 窗代替。|本輪角色未死亡。|`smoke-A3/m7-SM56-2.png`|
|SM45|⚠️|環境／操作未完成：魔法窗雙擊嘗試後，未取得可確證的目標游標、選中及取消全流程。|Ctrl+S→雙擊技能→Escape。|`smoke-A3/m7-SM45-1.png`；`smoke-A3/m7-SM45-2.png`|
|SM46|⚠️|環境／未建立條件：未完成物品丟撿與關門遮擋測試，無前後數量核對。|本輪未執行丟棄／拾取。|`smoke-A3/normal-mage-ready-state.json`|
|SM47|⚠️|環境／未建立條件：有溶解劑但未完成指定目標道具及緩速施放／恢復。|未消耗現有道具。|`smoke-A3/normal-mage-ready-state.json`|
|SM48|⚠️|環境／未建立條件：只登入既有法師與王族；未準備所有職業、基本技能與範圍法術。|未建新同名角色，未重放補物品。|`smoke-A3/normal-mage-ready-state.json`；`smoke-B/m7-SM35-B-1-state.json`|
|SM49|⚠️|環境／未建立條件：已登入法師沒有傳送控制戒指；隨機／記憶傳送選單未驗。|檢查背包後未操作。|`smoke-A3/normal-mage-ready-state.json`|
|SM50|❌|引擎：實際登入 map4(33071,32800) 時區塊 4/80048001 報尚未支援 SEG／壓縮格式、世界不 renderable；洞穴、船運與失敗恢復未完成。|既有法師在原 map4 位置登入。GM 瞬移只作後續準備、不算本列通過。|`smoke-A/m7-SM10-1.png`|
|SM51|⚠️|環境／覆蓋不足：音訊快照 ready、串流 Failed=0，但未做 loopback 錄音／實聽／6016 對照，不能據此判有聲。|只讀世界音訊狀態。|`smoke-A3/normal-mage-ready-state.json`|
|SM52|⚠️|環境／覆蓋不足：看見 NPC 本體，未核對具指定衣物層 NPC、逐格層順序與缺檔行為。|map0 普通場景觀察。|`smoke-A3/m7-SM21-3.png`|
|SM53|⚠️|環境／操作未完成：部分指令輸入曾串接成普通聊天；未完成各指令、位置分享、決鬥及踢除回包判定，不宣稱伺服器不支援。|原生聊天列輸入；先前工具草稿清除不可靠。|`smoke-A/m7-SM53-receive-2.png`；`smoke-A3/m7-SM31-before.png`|
|SM54|⚠️|環境／未觸發：classic 啟動可見舊介面，未讓角色實際死亡及等候 501264 ms，死亡備援未驗。|只執行本張 classic 入口補測。|`smoke-C3/m7-SM55-classic-1.png`|
|SM55|✅|本次指定子項：--classic-ui 舊介面、nativeUi:false 舊介面、刪單一設定後恢復原生登入頁三者成立；既有基本操作依任務沿用。|C3→D→E，設定只寫 worktree。|`smoke-C3/m7-SM55-classic-1.png`；`smoke-D/m7-SM55-config-off-1.png`；`smoke-E/m7-SM55-restored-1.png`|
|SM56|✅|本次指定子項：A／A3／B／C3／D／E 六次正常 closed；A3 原生 Quit 的離開回登入，再按 Exit 關閉。兩次包裝失敗不算 closed。|正常關各測試窗，核對 window-events 的 closed。|`session-summary.json`；`smoke-A3/m7-SM56-2.png`；`smoke-A3/m7-SM56-exit-confirm.png`|
|SM57|❌|引擎：Ctrl+V 選項窗可開，但常用選項只畫勾選框，多個項目沒有名稱；音量獨立性與重啟保存未驗。|進世界 Ctrl+V，環境設定頁。|`smoke-A/m7-SM57-1.png`|
|SM58|⚠️|環境／未建立條件：未取得 1062／1461 stride 與 1395→1410 靜態外型條件；不能用普通走路當成此列通過。|本輪未準備指定外型。|`smoke-A3/normal-mage-ready-state.json`|
|SM59|✅|本次指定子項：classic 啟動自動選「登入／選角」；D 的前景屬自家客戶端、鍵盤焦點在該 Tab，沒有手動選頁。其餘 #475 已得子項依任務沿用。|啟動 C3／D，直接讀 UIA SelectionPattern 與焦點。|`smoke-C3/m7-SM59-1.png`；`smoke-D/m7-SM59-2.png`；`smoke-D/SM59-focus.json`|

## Ctrl+P、備份與收尾

- 世界內 Ctrl+P 新檔：`engine/runtime/Capture/20261114-135835-970.png`；修改時間 `2026-11-14T05:58:35.9805591Z`；SHA-256 `cc49263c2b3a0b11602ee77955410a087c97000943e53bef9f1b55f907d9c0cb`。當時 InWorld、HUD 可見，但 map4 區塊載入失敗；不把這張圖算成 map4 成功。
- `WidgetSlot.xml` 明文 SHA-256=`504ca99eb16d7127ad7a04087ec1824d1a3edd607408e84a2182572644381c54`，來源唯讀參考客戶端 Tile.pak；另有 LoginUI／OptionUI 雜湊日誌。
- 開服前冷備份 `backups/issue-1186-before/db-data`：1722 檔、279503832 bytes，逐檔 SHA 核對；manifest SHA=`996a39971570b7a2d8ab2547fea02e737a24c898f02b5f7f740135e6b3caf06b`。另於暫時 GM 前完整停庫冷備份 `backups/issue-1186-gm-cold/db-data`（`gm-cold-snapshot.json`）。mysqldump 不存在造成的空 `issue-1186-gm-before.sql` **不是有效備份**，未在該次失敗後直接寫 DB。
- 為避開 map4 載圖問題，暫設 objid 10334／1900249001／1900240001 為 GM=200，僅作 GM 瞬移準備，接著全部還原 0，再用 GUI 普通玩家測試。前後 TSV、準備封包與 `gm-restore.sql` 均在證據根；未把準備用協定客戶端當作引擎實測。
- 還原 SQL：`UPDATE p24_accounts.characters SET AccessLevel=0 WHERE objid IN (10334,1900249001,1900240001) AND @@port=43324;`。最終全庫 17 角色 AccessLevel=0、OnlineStatus=0（`characters-final.tsv`／`characters-final-summary.json`）。位置仍為本次測試位置，前位置見備份與前後對照，不宣稱整庫逐 byte 還原。
- Java 以自啟 PID 關閉；MySQL 用 mysqladmin 正常關閉，退出碼 0；42061／43324 無監聽。客戶端／launcher 已依 PID＋啟動時間查無本張仍存活程序（`final-processes.json`）。沒有停既有他人程序、沒有改服務或系統設定。
- C02 結果與寫入清單見 `mtime-audit.json`；保護檔 SHA 比對見 `protected-comparison.json`。只允許 DB runtime、JAR log／temp 與本號 backups；`scratch/wt/*`、`.git`、#1124 的 11 個拒讀歷史目錄依 #1133 K1 排除。mtime 稽核無法證明保留時間的寫入或刪除。
- **C02 未通過全域排他性判定**：除授權類別外，主專案 `scratch/nuget-packages/microsoft.web.webview2/1.0.4258.31/` 的 `.nupkg.metadata`、`microsoft.web.webview2.1.0.4258.31.nupkg`、`microsoft.web.webview2.1.0.4258.31.nupkg.sha512` 在約 14:06:31 有 mtime 變更。無程序寫入追蹤，不能歸屬本張或其他並行工作；不把它們擅自加入排除名單。已觀察授權範圍為 DB runtime 55 檔、JAR log/temp 3 檔、本號備份／證據 89 檔；冷備份保留原 mtime，故此數量不是本張所有新建檔總數。已知歷史拒讀 11 個只列出，未知拒讀 0；保護檔 SHA 全相同。請調度器釐清快取寫入歸屬。

## 推測與待驗證

文字重疊／頭上條偏移與 DPI 192 同時出現，**尚未證明根因是 DPI**。原生選角黑畫面、map4 區塊不支援及各窗文字問題需分流處理；未量測聲音、沒有死亡、沒有完成交易／戰鬥與全部職業流程，詳見 ⚠️ 列。

取證限制：一次使用 Copy-Item 讀取持續更新的 login-state.json，造成客戶端寫檔分享衝突並進 Faulted；已保留原錯誤，後續改用允許 ReadWrite／Delete 分享的讀取，不算 NPC 商店失敗。A2 的關窗包裝只關掉開發工具後強制收 client；C／C2 的啟動包裝未取得可見主窗而強制收自啟程序，均不算正常 closed。A3 寫出 closed 後，包裝 finally 與退出程序競爭而報 Kill Access is denied；最終 PID 稽核確認沒有殘留。以上只改 scratch 測試包裝，沒有修產品或追蹤內測試工具。

## 給下一棒的提示

主控先拆引擎 ❌ 修正與 ⚠️ 測試準備，C02 快取差異交調度器釐清；SM19／SM50 承接 #1195，避免重複開修正。不要重測本張已通過的指定子項，尤其 SM15 世界內 Ctrl+P、SM55 切換、SM56 六次 closed。先解原生選角與文字版面，再安排缺物品／地點／職業／死亡等條件及可靠的雙角色選取、聊天指令操作；沒有回應的項目不可直接標伺服器不支援。是否維持 nativeUi 預設、能否結案 #475／T112 由主控裁定。
