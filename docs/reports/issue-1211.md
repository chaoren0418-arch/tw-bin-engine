# Issue #1211：P24Lab 指定項目重測

結論：部分通過（✅ 2／❌ 4／⚠️ 41；本次範圍47列）。

**未執行（時間）：SM03、SM14、SM23、SM25、SM28、SM36、SM37、SM39、SM40、SM41、SM42、SM43、SM46、SM47、SM48、SM49、SM52。** 其餘 ⚠️ 列也有未完成子步驟，詳見「只補這些」欄；未完成不算通過。測試操作 UTC 08:35–09:20，09:21 已完成程序收尾；60分鐘時間盒包含建置、準備、整理與提交。

## 已驗證

- 在指定 worktree／task/1211 執行，沒有切分支、修引擎或更改 dev lock、docs/REPORT.md。來源 origin/main `51c11852`，開始 fetch 後與 HEAD 無差異。
- 實測開始時 main 缺 `docs/reports/issue-1186.md`、`work/f1186-smoke/fixtures.json` 與新版 SmokeTools；從 origin/task/1186、1209、1210 以 git show 唯讀取到 scratch。產品只用 main 重建，沒有把其他分支產品冒充 main。fixtures 的 readyForRetest=false 僅按任務分流使用其餘條件。
- `engine/build-product.ps1 -ConfigFile .github/nuget.ci.config` 成功；設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`，建置用暫存 lock；launcher `--check-only` 成功。開服與登入：前置成立。
- product DLL SHA-256 `e643bb88dac9f9e45f30fb22c126cd64707645a39304e8a8de46a40b9981a21f`，與 #1186 的 `58a01717…`、`d42e933f…` 均不同；audio `4c4aa97837e7c4e610d3e0b75eb2b619faf41508096435e37c635c2cec456473`；launcher `849ae3058ab773300691810dabc16b42db9e8d090d08f73c772ab20dda7488cb`。
- UI 輸入用專案 UIA／guarded SendInput，截圖用 GDI PrintWindow；無 WGC／computer-use、無反射或合成封包驗收。測試指令核對 `packet-trace.jsonl`（只有方向、opcode、長度、序號，沒有登入 payload）。
- A=原生 DPI192；D=僅該子程序 `DPIUNAWARE`、GetDpiForWindow=96，但物理桌面仍192，1600×1200 PrintWindow 有空黑區與邊界偏差，**不當成完整實體 DPI96 驗收**。沒有改登錄檔、系統顯示設定或權限。C=classic DPI192。
- SM22／SM35／SM51、SM27／SM29 已依 #1225／#1220 移出，不計47列分母。#1197 已有 M01⚠️（非❌），本次沒有以它阻擋SM19／SM50。

同步補驗：推送前 fast-forward 至 origin/main `1ef9039d6b6ef87151859457bdbd35b18d64724d`。新增15個提交僅涉及報告與測試工具，產品來源未變；重新建置與 check-only exit code 均0，product／audio／launcher DLL及manifest SHA均與實測產品一致（`sync-build-result.json`、`rebuild-after-sync.log`）。新版 UiaGdi 的差異為輸入日誌字典列舉；本次舊版按鍵名稱欄位不完整，須結合 jobs 結果及狀態／封包證據，不靠該欄單獨判定。沒有用同步後工具重算本次實測結果。

## 逐項結果

下列證據路徑皆相對本 worktree 的 `environments/p24/backups/issue-1211-evidence/`。同名 `*-state.json` 是原客戶端共享讀回；未執行列的 execution-window.json 只證明時間盒，不是功能證據。

|編號|結果|實際觀察／原因分類|重現／操作|證據路徑|只補這些|
|---|---|---|---|---|---|
|SM03|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|黑邊點擊與原版 800×600 並排；本張沒有啟動原 BIN。|
|SM04|✅|登入框帳號與遮蔽密碼文字大小、位置可讀，未見重疊。|原生登入頁輸入測試帳號及密碼。|`client-A/m7-SM04-1.png`|無；輸入、錯密碼與 Exit 沿用 #1186。|
|SM07|❌|引擎呈現：原生選角已掛載且上下鍵有效，但黑底中按鈕／背景位置出現人物卡片，資訊與圖卡交疊。|登入 p24svc01，在原生選角按 Up／Down。|`client-A/m7-SM07-1.png`；`client-A/m7-SM07-empty.png`；`client-D/m7-SM07-dpi96.png`|修復圖像呈現；DPI 96 只能取得相容模式圖。|
|SM08|❌|引擎呈現：七職頁與能力頁可進入，Unicode 名稱可輸入；部分按鈕仍被人物卡片取代。|空欄 Enter→職業頁逐職點選→Enter 能力頁→輸入煙測甲。|`client-A/m7-SM08-class-0.png`；`client-A/m7-SM08-stat-2.png`；`client-A/m7-SM08-name-focused.png`；`client-D/m7-SM08-stat-dpi96.png`|修復按鈕圖像；真正 IME 候選組字未驗（Unicode SendInput 不算 IME）。|
|SM09|⚠️|已在空欄建立煙測甲（3→4）、確認刪除本人新角（4→3），同客戶端登出後切換其他角色；環境：DPI 96 建刪圖未補齊。|建角→選新角→刪除確認 Enter→登出登入選另一角。|`client-A/m7-SM09-created-final.png`；`client-A/m7-SM09-delete-confirm.png`；`client-A/m7-SM09-deleted-state.json`|只補 DPI 96 證據；沒有刪除原有角色。|
|SM10|⚠️|世界 HUD 文字較 #1186 可讀；環境：未完整核對 Y/N 框及全部數值區。|進世界觀察 HUD；另取得原生死亡選單。|`client-A/m7-SM10-world.png`；`client-D/m7-SM10-dpi96.png`；`client-A/m7-SM44-death.png`|Y/N 框及完整 DPI 對照。|
|SM11|⚠️|取得站立、移動、1.725 縮放及低 HP 圖；環境：未完成條座標逐像素與連續閃爍核對。|普通移動、滾輪縮放；低 HP 時連續截圖。|`client-A/m7-SM11-standing.png`；`client-A/m7-SM11-noninteger.png`；`client-A/m7-SM11-lowhp-1.png`；`client-A/m7-SM11-lowhp-2.png`|低 HP 閃爍時序、非整數縮放座標及有效 DPI 96 對照。|
|SM12|⚠️|一般聊天 C136 已送且有本人文字回送；八分頁、五過濾與擴高已點核；環境：IME、各頻道未全做。|Send-SmokeCommand 一般訊息→八分頁→五個過濾按鈕。|`client-A/m7-SM12-general-send.json`；`client-A/m7-SM12-expanded.png`；`client-A/m7-SM12-tab-585.png`；`client-A/m7-SM12-filter-316.png`|真正 IME、全部頻道與 DPI 96。|
|SM13|⚠️|保護罩實際施放後出現盾牌圖示，讀回 Skill=3、Seconds=1800；環境：指定 6400／82／470 未觸發。|技能窗選保護罩→M。|`client-A/m7-SM13-shield-cast.png`；`client-A/m7-SM13-shield-cast-state.json`|其餘指定圖示與時間核對。|
|SM14|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|空盟倉紅字、鑑定與中央訊息。|
|SM16|⚠️|說明窗文字可讀；自寄煙法甲信件後扣 50 金幣，刪信後清單空且出現已取消刪除字樣（需另核文字）；巨集拖入未綁定。|Ctrl+H；信件填寫→寄出→選信刪除；道具拖巨集。|`client-A/m7-SM16-open.png`；`client-A/m7-SM16-mail-compose.png`；`client-A/m7-SM16-mail-sent.png`；`client-A/m7-SM16-mail-deleted.png`；`client-A/m7-SM16-macro-bind.png`|巨集八鍵／分頁、選項連結與信件提示字串；不要重寄刪已完成的流程。|
|SM17|⚠️|新增 Up／Right 移動圖及方向讀回；環境：未完成小地圖箭頭逐項比對與隊友點。|世界按 Up、Right。|`client-A/m7-SM17-heading-up.png`；`client-A/m7-SM17-heading-right.png`|箭頭核對；隊友條件仍受相鄰 PC 工具缺口影響。|
|SM18|⚠️|藥水雙擊 20→19、Tab／Ctrl+I／選單入口、分類及刪除取消已操作；環境：DPI 96 相容截圖邊界不完整。|背包雙擊藥水；三入口；分類；Delete→Escape。|`client-A/m7-SM18-potion-state.json`；`client-A/m7-SM18-delete-confirm-2.png`；`client-D/m7-SM18-dpi96.png`|只補有效 DPI 96 呈現證據及分類逐頁核對。|
|SM19|⚠️|map4 凱倫右鍵交談、存入銀箭 1、領回銀箭 1 完成；環境：DPI 96 圖未補。|4(32608,32788)→右鍵凱倫→存銀箭1→領銀箭1。|`client-A/m7-SM19-talk.png`；`client-A/m7-SM19-deposit-one.png`；`client-A/m7-SM19-withdraw-page2.png`；`client-A/m7-SM19-withdrawn-state.json`|只補 DPI 96；存領功能與 SEG 載入成功不必原樣重跑。|
|SM20|❌|引擎：龍騎士已學 181／182、幻術士已學 201／202，技能窗仍空白；妖精另有外型未支援遮斷世界。|登入煙龍甲、煙幻甲，各按 Ctrl+S，對照 learned 清單。|`client-A/m7-SM20-class-5.png`；`client-A/m7-SM20-class-5-state.json`；`client-A/m7-SM20-class-6.png`；`client-A/m7-SM20-class-6-state.json`|補 5／6 職業技能頁；map509 GM 瞬移未落地，拒開條件未測。|
|SM21|⚠️|潘朵拉買箭及弓成交（金幣 99950→99893）；賣頁回覆目前沒有要買的道具；環境：修理、學法未做。|靠近潘朵拉→右鍵→買→數量+1→Buy；再右鍵→賣。|`client-A/m7-SM21-talk.png`；`client-A/m7-SM21-buy-confirm.png`；`client-A/m7-SM21-bought-state.json`；`client-A/m7-SM21-sell-page.png`|可出售物品、修理及吉倫學法；不能由單次空賣頁推論伺服器不支援。|
|SM23|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|雙人私商買賣、價額確認與兩行訊息。|
|SM24|⚠️|/war 嘗試未觀察 C227，/focus 清空正向控制失敗而未送；環境：未開血盟備註／階級流程。|新指令工具執行 /focus、/war SM1211。|`client-A/m7-SM24-focus-send.json`；`client-A/m7-SM24-war-send.json`|血盟窗與指定階級、備註、城門及指令回包；不判伺服器不支援。|
|SM25|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|寵物取出、召喚控制戒指／技能51及盟屋頁。|
|SM26|⚠️|料理書雙擊後有獨立 WinForms 參數窗；確認使用後收到真實料理回包並開原生八格料理窗；環境：移走料理書測1167未做。|雙擊料理書→確認使用→原生 COOK_BOOK_WINDOW。|`client-A/m7-SM26-input-dialog.png`；`client-A/m7-SM26-native-book.png`；`client-A/m7-SM26-native-book-state.json`|捲動提示、移走書後製作與1167；先前只截主窗漏拍參數窗，不算引擎失敗。|
|SM28|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|真實 S43 kind1 重置與51級以上 Up／Esc。|
|SM30|⚠️|map5300 在32792,32784對32792,32781用短釣竿，真實動作71／fishing=true及原生釣魚窗；收竿後窗關閉；環境：線條與換圖殘留未核完。|雙擊釣竿→點水面→獨立窗確認使用→收竿。|`client-A/m7-SM30-target-dialog.png`；`client-A/m7-SM30-fishing.png`；`client-A/m7-SM30-fishing-state.json`；`client-A/m7-SM30-stopped.png`|逐格釣魚線、1516文字與換圖不殘留。|
|SM31|⚠️|C125 送出、S39 回包均確認；環境／待定位：截圖仍是世界，未取得可見排名窗，不能判伺服器未回。|工具清空後輸入 /rank，延遲後再截圖。|`client-A/m7-SM31-rank2-send.json`；`client-A/m7-SM31-delayed.png`|定位排名窗開關時序並完成可見排名頁。|
|SM32|⚠️|動作四分頁可見，另點行動頁第一格；環境：沒有逐一核對所有有路線動作。|動作按鈕→社群／行動／管理→第一行動格雙擊。|`client-A/m7-SM32-actions.png`；`client-A/m7-SM32-tab-425.png`；`client-A/m7-SM32-action-first.png`|只補尚未驗證的各動作路線。|
|SM33|⚠️|說明頁與潘朵拉頁取得滑入前後圖；環境：未取指定日期頁及 RGB 像素核對。|右鍵潘朵拉→滑入「知道夏洛伯嗎」。|`client-A/m7-SM33-npc-before.png`；`client-A/m7-SM33-npc-hover.png`|指定 NPC 日期、遊戲時間與色值；不是以本機說明頁代替指定頁。|
|SM34|⚠️|/buddy 重試 C4／S39 後出現空好友名單；封鎖／解除 C171 已送；環境：未核對名單回包最終狀態。|/buddy→/exclude 煙王甲→/include 煙王甲。|`client-A/m7-SM34-buddy2-send.json`；`client-A/m7-SM34-delayed.png`；`client-A/m7-SM34-exclude-send.json`；`client-A/m7-SM34-include-send.json`|名單新增刪除與封鎖解除讀回；首次清空失敗已保留，不當成送出。|
|SM36|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|召喚高等頁停用格滑入、點選與拖曳。|
|SM37|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|S_EVENT117 血盟倉庫紀錄窗。|
|SM38|⚠️|/bookmark SM1211、/qbookmark SM1211Q 的 C165 已送；未核得預期回包及可見書籤窗。|新工具逐條輸入新增與開窗指令。|`client-A/m7-SM38-add-send.json`；`client-A/m7-SM38-quick-send.json`；`client-A/m7-SM38-list.png`|新增讀回、刪除、19區域、IME與拖曳；預期 opcode 設定未另核，不能把工具未命中當成完全無回包。|
|SM39|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|普通近戰擊殺、EXP與掉落。|
|SM40|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|弓箭裝備／射擊／耗箭／其他箭矢；本次只從NPC買到弓箭。|
|SM41|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|攻速、動作競合及表情保留指定時序。|
|SM42|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|指定攻擊／座標／範圍特效；保護罩施法不替代本列。|
|SM43|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|妖精與黑妖各中盲目2的光圈。|
|SM44|⚠️|普通煙法甲被骷髏斧手擊殺，原生死亡窗取消、Ctrl+Q重開及重新開始回村均完成；環境：復活Y/N與Esc優先序未做。|map2普通角色受怪攻擊→死亡→取消→Ctrl+Q→重新開始。|`client-A/m7-SM44-death.png`；`client-A/m7-SM44-cancel.png`；`client-A/m7-SM44-reopen.png`；`client-A/m7-SM44-restart.png`|復活321／322、背包開啟時Esc優先序。|
|SM45|⚠️|光箭選取後 M 讀回「請選擇對象」，點地面取消已操作；環境：尚未完成對有效目標施放與游標形狀逐格核對。|選光箭→M→點地面。|`client-A/m7-SM45-picking-confirmed.png`；`client-A/m7-SM45-picking-confirmed-state.json`；`client-A/m7-SM45-cancel-confirmed.png`|有效目標施放與游標形狀、取消讀回。|
|SM46|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|肉丟1撿1／丟10撿10、錯誤數量更正及門遮擋。|
|SM47|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|指定目標道具、緩速時用物品／拾取／交談。|
|SM48|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|七職各施放基本技能、非1／3／4／8及範圍法術；只完成七職登入與技能清單。|
|SM49|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|傳送控制戒指、書籤選取及限制地圖。|
|SM50|⚠️|原 map4(33071,32800) 登入成功、renderable=true、原始區塊載入，未出現 SEG／壓縮錯誤；環境：洞穴步行、船運、失敗恢復未做。|GM僅準備位置並改回0，普通客戶端登入指定map4位置。|`client-A/m7-SM50-map4.png`；`client-A/m7-SM50-map4-state.json`|只補洞穴步行、船運、傳送失敗恢復；GM瞬移不算這些子項通過。|
|SM52|⚠️|環境／覆蓋：未執行（時間）；沒有功能判定。|未執行|`execution-window.json`|指定衣物層NPC、逐格順序與缺檔行為。|
|SM53|⚠️|checkpk／divorce／duel 已確認送出與回包，whisper off/on 已送；其他指令有前置清空失敗或未送出，未完成位置接收與踢除。|依 README 用工具逐條清空、送出、核對 trace。|`client-A/jobs/41-commands.ps1.result.json`；`client-A/m7-SM53-checkpk-send.json`；`client-A/m7-SM53-duel-send.json`|只補失敗或未核對子步驟；不把輸入文字等同C封包送出。|
|SM54|⚠️|普通煙王甲被怪擊殺，classic中央死亡窗出現；等待502763ms後Disconnected；環境：取消後死亡選單鈕／重新開始未操作。|classic登入map2→一般玩家受怪攻擊死亡→等501264ms。|`client-C/m7-SM54-dead.png`；`client-C/m7-SM54-after501264.png`；`client-C/m7-SM54-end-time.json`|只補classic取消後死亡選單鈕與重新開始；死亡及逾時已取得。|
|SM55|✅|--classic-ui 啟動並登入後，角色選單、建立／刪除與進入世界均維持WinForms控制項。|classic登入→UIA讀回與截圖角色選單。|`client-C/m7-SM55-selection.png`；`client-C/roles-tree.json`|無；另兩種切換沿用 #1186。|
|SM57|⚠️|常用選項有文字；三音量各自設定24／48／74，新客戶端讀回相同值；環境：DPI96只取得相容模式，實際聽感未驗。|選項環境頁分別改三滑桿→另開客戶端讀回。|`client-A/m7-SM57-volumes.png`；`client-A/m7-SM57-volume-file.json`；`client-D/m7-SM57-restart-dpi96.png`；`client-D/m7-SM57-restart-config.json`|有效DPI96呈現；聲音輸出工具依 #1225 分流，不在本張補錄。|
|SM58|❌|引擎：GM準備1062後普通客戶端顯示「尚未支援此外觀」，黑畫面且停止移動／戰鬥，stride無法驗；後續1461／1395指令未取得相應gfx讀回。|準備 .poly 煙法甲 1062→GM還原0→普通客戶端點地行走。|`client-A/m7-SM58-1062-walking.png`；`client-A/m7-SM58-1062-standing-state.json`；`client-A/m7-SM58-1395-state.json`；`prep-poly-1062-after.tsv`|修復1062可操作性；1461／1395條件未成立，不能把檔名當成變身成功。|

## 備份、寫入與收尾

- 原專案只讀。因本張明確禁止寫原專案，將停止狀態的 P24Lab DB 完整冷備份到本 worktree `scratch/issue-1211/backup-before/db-data`，再複製為 `scratch/issue-1211/db-data` 執行；1722檔、279511437 bytes，三份逐檔SHA一致。manifest SHA-256 `1fb267532c07870f087ce58d91fd820a855c189cfa74c0378e681c7609d224b4`，見 snapshot.json／backup-before-manifest.json。沒有覆寫原 DB。
- 本次只用 MySQL 127.0.0.1:43324、遊戲42061；所有資料庫查詢／GM設定皆核對 runtime datadir 含 task-1211。GM只用於位置／外型準備，實際遊戲操作以一般玩家執行。準備前後 `prep-*-before.tsv`／`prep-*-after.tsv`，每次均附 `prep-*-restore.sql` 且已執行還原GM0。未直接修改HP、技能、經驗；角色死亡掉級、交易／消耗等為伺服器正常遊戲結果。
- 24個角色最終GM=0、OnlineStatus=0（characters-final.tsv）；煙測甲已由GUI刪除，未刪原有角色。本次測試世界修改留在隔離DB，可保留它或改用冷備份；不需要把任何SQL施到原環境。
- 客戶端PID20036／19732／19964均取得 closed 並退出，Java20032核對啟動時間後關閉，MySQL以 mysqladmin TCP43324 shutdown；parent10300／child2280皆退出，42061／43324無監聽（cleanup.json）。沒有啟動登入器GUI；check-only已退出。
- C02依 #1133 K1 掃描主專案，排除 .git、scratch/wt、reparse point，不追入junction；11個既知拒讀歷史目錄只列不判。掃描987441檔，時間窗內主專案變動0、既知拒讀11、未知拒讀0；另列75個junction／reparse入口且未追入。原DB1722檔雜湊全部相同。依此界線C02通過，非對保留mtime或刪除的全域證明（mtime-audit.json）。

## 推測與待驗證

- 選角／建角的圖卡錯置可能涉及surf對應或素材來源，尚未定位；龍騎／幻術空白技能頁對照 main 的 SpellWindowPanel.ClassOf 只列0–4，屬可供修正者查核的線索，不代替實機證據。
- /rank有回包但沒取得排名頁，可能為開關／焦點時序；不宣稱伺服器不支援。工具 ResponseObserved=false 僅表示指定時間窗沒有命中配置的opcode，未全面解析的回包不得說成完全未回。
- 寄刪信、釣魚與料理已補出實際功能證據；獨立ItemUseDialog不在主窗PrintWindow內，先前漏拍不能直接定為引擎缺陷。初期同PID開發工具搶焦點／共享讀檔與太快連點造成的失敗也未當成引擎❌。
- 本次未修改程式、不跑替代性單元測試；未驗證事項逐列保留，不能據此裁定 nativeUi 預設或結案 #475／T112。

## 產出與給下一棒的提示

- 追蹤產出：`docs/reports/issue-1211.md`、`docs/reports/issue-1211-evidence.json`；PNG／log／原始state只在上述忽略的證據根目錄，manifest逐檔列SHA。
- 調度器先保全本 worktree 的證據目錄及 `scratch/issue-1211/backup-before`，再按原有流程處理worktree；本執行器沒有拆junction、遞迴刪除或移除worktree。
- code下一棒優先處理SM07／08錯圖、SM20龍騎與幻術技能空頁、SM58外型1062阻止操作；test下一棒只補表格最後欄與未執行列，已完成子步驟可沿用。
- DPI96仍需可用的有效截圖方法；主控裁定 nativeUi 預設、#475／T112與SM01歷史耗時。SM02／05／15／56／59及SM04／55非本次子項沿用 #1186，SM06未補公告，不重新計入本次。

證據封裝：657檔，manifest SHA-256 `04fcec308d38adcb51008ca1383f04b8067a8e03959b477c0948c51d5baf2bc9`。
