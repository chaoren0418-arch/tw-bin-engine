# Issue #1125：P24Lab 重測與 M7 smoke

**結論：部分通過（✅ 7／❌ 0／⚠️ 59）。第一段七項通過；SM01 的原生 UI 已 active，但 GUI 擷取重設後仍失敗，依任務閘門停止。SM02–SM59 未執行，屬環境前置阻塞，不是58個引擎缺陷。交 `needs:local`。**

2026-11-14 05:40–05:49（台北）執行與收尾；來源 `47ac3ef06d5cb82f19838840d6c69d80adcd410b`，既有分支 `task/1125`。未改引擎程式、dev lock、docs/REPORT.md、設定、JAR、原BIN或封存；未建角、設GM或直接DML。只連 P24Lab 42061／43324。

## 逐項結果

以下證據路徑相對於主專案 `environments/p24/backups/issue-1125-evidence/`；摘要隨Git保存於 [issue-1125-evidence.json](issue-1125-evidence.json)。66列計數只計本張指定的7個R項與59個SM項；沿用與收尾輔助檢查不重複計入。沒有成功的 `m7-SM01-1.png`，也沒有以其他圖片代替。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑／分類 |
|---|---|---|---|---|
| R03 | ✅ | 原 JAR PID6836 監聽42061，log 開服完成，無拒絕開服。 | 沿用 #1107 持久 server 工作目錄、JDK17 啟動。 | listeners.json；server.stdout.log |
| R04 | ✅ | p24svc01 收 S233=0、S178=3/4、三筆 S93 角色清單。 | 連127.0.0.1:42061，以既有測試憑證登入至選角後關閉。 | login.json；packets.json |
| R05 | ✅ | 服務驗證與透明王子同時在線 map4，相距1格；雙方收到對方S87、SQL OnlineStatus均1。 | 兩個不同帳號登入、C137進世界、C95探測，保持連線讀回。 | first-segment.json；dual-online.tsv；dual-service-packets.jsonl；dual-prince-packets.jsonl |
| R06 | ✅ | A進世界後，同帳號第二次登入回S233=22；該帳號新舊連線皆EOF，另一帳號保持在線。 | 維持R05兩session，再以p24svc01開第三session，觀察3秒。 | first-segment.json；same-account-second-packets.jsonl；server.stdout.log |
| R12 | ✅ | 法師LV52／GM0／MP276、技能51、項圈與牧羊關聯成立，笛9、寶石57。 | 只讀查角色、技能、背包、寵物關聯；本列驗準備條件，不施法或消耗物品。 | conditions.json |
| R13 | ✅ | 血盟10171、服務驗證盟主rank4／透明王子rank8、盟倉治癒藥水1件。 | 只讀查血盟、兩成員及盟倉；不重建血盟或重存物品。 | conditions.json |
| R17 | ✅ | 本輪R03／R04／R05／R06／R12／R13與沿用前置成立，P24Lab可交SM；後續GUI擷取另列SM01。 | 核對本輪回包、資料條件與沿用 #1124／#1119 結果。 | first-segment.json；conditions.json；login.json |
| SM01 | ⚠️ | 事件 verifying→active，載入63面板含SPELL_WINDOW；擷取兩次逾時，無PNG、核對中畫面或可接受耗時判定。 | main產品build→check-only→正常launcher帶evidence；get_window_state失敗，reset／重選窗後再失敗，依閘門停止。 | smoke-A/window-events.json；smoke-A/native-ui.log；capture-errors.json；環境 |
| SM02 | ⚠️ | 參考客戶端資源：依SM01擷取環境閘門未執行；#475「參考XML載入」沿用，其餘子步驟未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM03 | ⚠️ | 縮放 1×／2×：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM04 | ⚠️ | 登入畫面輸入：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM05 | ⚠️ | 登入背景動畫：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM06 | ⚠️ | 登入公告 S48：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM07 | ⚠️ | 選角畫面：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM08 | ⚠️ | 建角與中文名稱：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM09 | ⚠️ | 建立、刪除、切換角色：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM10 | ⚠️ | 常駐 HUD 外觀：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM11 | ⚠️ | 名稱、頭上條、身體中線：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM12 | ⚠️ | 聊天窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM13 | ⚠️ | 狀態圖示：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM14 | ⚠️ | 紅字、中央綠字、鑑定訊息：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM15 | ⚠️ | WidgetSlot：依SM01擷取環境閘門未執行；#475「參考XML載入」沿用，其餘子步驟未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM16 | ⚠️ | 巨集 F5–F12 與選單按鈕：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM17 | ⚠️ | 小地圖與 Field_Map：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM18 | ⚠️ | 背包窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM19 | ⚠️ | 倉庫窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM20 | ⚠️ | 魔法窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM21 | ⚠️ | 商店、修理、學魔法：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM22 | ⚠️ | 兩個角色交易：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM23 | ⚠️ | 私商與個人商店：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM24 | ⚠️ | 血盟窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM25 | ⚠️ | 寵物、召喚、盟屋地圖：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM26 | ⚠️ | 料理窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM27 | ⚠️ | 公告板：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM28 | ⚠️ | 能力值重置窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM29 | ⚠️ | 盟屋拍賣：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM30 | ⚠️ | 釣魚：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM31 | ⚠️ | 排名窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM32 | ⚠️ | 動作窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM33 | ⚠️ | HyperText 顏色：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM34 | ⚠️ | 好友與封鎖：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM35 | ⚠️ | 隊伍窗與名稱旁圖示：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM36 | ⚠️ | 召喚窗停用格：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM37 | ⚠️ | 血盟倉庫紀錄窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM38 | ⚠️ | 書籤窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM39 | ⚠️ | 近戰擊殺與 EXP：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM40 | ⚠️ | 弓箭：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM41 | ⚠️ | 時序：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM42 | ⚠️ | 特效：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM43 | ⚠️ | 盲目光圈：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM44 | ⚠️ | 死亡、QUIT_WINDOW、復活：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM45 | ⚠️ | 選目標游標：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM46 | ⚠️ | 丟撿與拾取更正：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM47 | ⚠️ | 指定目標道具與緩速：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM48 | ⚠️ | 各職業技能：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM49 | ⚠️ | 傳送控制戒指：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM50 | ⚠️ | 換圖：洞穴、船運、傳送：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM51 | ⚠️ | 聲音：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM52 | ⚠️ | NPC 衣物層：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM53 | ⚠️ | 聊天指令與伺服器回應：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM54 | ⚠️ | classic 模式的死亡備援窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM55 | ⚠️ | 切回舊介面：依SM01擷取環境閘門未執行；#475「兩種關閉方式與恢復」沿用，其餘子步驟未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM56 | ⚠️ | 登出：依SM01擷取環境閘門未執行；#475「五次closed事件」沿用，其餘子步驟未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM57 | ⚠️ | 選項窗：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM58 | ⚠️ | 走路動作 stride 與靜態外型 altdir：依SM01擷取環境閘門未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |
| SM59 | ⚠️ | classic 備援入口逐一點開：依SM01擷取環境閘門未執行；#475「classic登入頁焦點」沿用，其餘子步驟未執行。 | 先由needs:local恢復擷取，再按smoke-m7對應列執行。 | capture-errors.json；本報告SM01；環境／前置閘門 |

## 已驗證

- R01／R02 的流程沿用；實際啟動前另做完整冷備份到 `environments/p24/backups/issue-1125-before/db-data`，禁止寫入的 ibdata1 handle 保護下複製1,722檔、279,502,536 bytes，逐檔SHA相符。manifest SHA：`1917432faa7fad37a2272743102d7db80902a78b72e79106da0598a7f41d47e9`。R08–R11／R14／R15採 #1124 讀回結果，沒有重放補資料；R07採 #1119 七職業GUI結果，不重建角。
- `engine/build-product.ps1` 成功；初次編譯4個既有警告／0錯誤。為正常入口建立本worktree的 `engine/assets` 唯讀用途junction到既有r29，再重跑build產生含素材pin的manifest，增量0警告／0錯誤。沒有複製pak／map／sprite，沒有拆junction或遞迴刪除。第一次採證shell因原生stderr警告被PowerShell當作終止錯誤，改由Python直接收stdout／stderr後成功，未改產品腳本。
- #1093 merge `7c31e7ba`、#1097 merge `c8a73fb0` 均為HEAD祖先。產品DLL SHA-256：`1642ea357d5300fcdac8f5b40e6595ba3504c17aa10b830d8fd6816041726e93`；等於 #1109 `1642ea35…6e93`，不同於 #475 `7f861fce…2c77`。來源程式相同時SHA相同屬預期。
- §0.2正常入口check-only回 `status=checked-only`、`revision=dev`、`assetState=legacy-ready`，核對13,957個素材；.NET Desktop10.0.8、WebView2 157.0.4341.46。建置的TEMP、CLI home、NuGet快取皆在本worktree，同時設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`。
- SM01由正常launcher帶 `--evidence` 啟動，沒有診斷或自動登入參數。關閉後的 `window-events.json` 確認 `verifying → active`、63個面板含 `SPELL_WINDOW`；沒有 `state=failed`，native-ui.log沒有原生UI啟動失敗分類訊息。不能因此宣稱SM03縮放、SM20魔法窗或全smoke通過。
- `computer-use`／`@oai/sky`對主視窗723252擷取第一次逾時；`js_reset → initialize → list_windows → get_window → activate_window`後再試仍為 `FrameArrived timed out: timed out waiting on channel`。收尾用UIA索引39點關閉另遇 `coordinate input geometry is unavailable`；刷新UIA後用Alt+F4成功，留 `closed` PID12560事件。UIA能列樹不代表可擷取畫面或座標操作。
- R16照常收尾：客戶端12560、launcher1944、Java6836、MySQL父11256與其監聽子程序均結束，兩埠無監聽；mysqladmin正常shutdown=0。Java以自己持有的Process.Kill結束，不宣稱優雅停服。關服前17個角色 `AccessLevel=0`、`OnlineStatus=0`（final-gm.tsv）。沒有直接DML，因此無需逆向SQL；未執行備份還原。

## 寫入稽核

- API lease為 `chaoren0418-arch/tw-bin-engine#1125`，到期2026-11-13T23:10:13.846644Z，與派工一致且本輪有效；沒有修改調度器或鎖。
- mysql.ini、原JAR、server/config 共24檔SHA前後相同。主專案僅允許db-data／db-temp／db-files／db-logs、原JAR日誌／暫存及本張backups；腳本、建置、GUI證據工作副本與報告均在本worktree。
- C02按 #1133 K1：結果 `pass`；掃描1,456,337檔；分類數量 `{"excluded-worktree-git": 152, "jar-log-temp": 2, "mysql-runtime": 48, "task-backup-evidence": 20}`。已知拒讀11處只列出，新拒讀0處；`.git`與`scratch/wt/*`僅列出不判警告。詳見mtime-audit.json；JAR備份ZIP只含log的清單亦在其中。未調整目錄權限。
- mtime時窗只證明可觀察變動，不識別已刪除或保留原時間的副本；本張沒有刪除。掃描後新增的本報告／摘要與封裝manifest分別只在本worktree與核准證據目錄。

## 推測與待驗證

擷取錯誤與 #1118 同型，但本輪沒有證據足以指定DPI、驅動或工具宿主為根因。原生UI已active，可排除「本輪沒有active」這個閘門原因；實際畫面、首次／再次接手耗時仍未驗證。沒有成功截圖，不做畫面正確性判斷。第一段以真實伺服器回包與SQL驗條件，不冒充GUI遊戲操作；R12召喚、R13盟倉的GUI操作屬SM25／36／24／37待測。

## 給下一棒的提示

1. 本機對話／`needs:local`先恢復GUI擷取，至少成功擷取普通視窗及本客戶端，再續SM01的畫面／耗時與SM02–SM59；不必重跑本輪通過的七個R項或R07建角，啟動前後仍需必要的安全前置與收尾。
2. 沿用 #475 的 SM55兩種關閉與恢復、SM56五次closed、SM59 classic焦點、SM15／SM02參考XML載入，只補未測子步驟。本輪因閘門沒有執行任何SM02以後的操作。
3. 不修引擎來處理擷取工具故障；nativeUi是否預設、#475／T112能否結案由主控裁定。P24Lab已收尾，下張需重新啟動同一持久環境。
4. PNG／log／腳本與完整冷備份不進Git；持久證據不依賴worktree保留，清單 `evidence-manifest.json` SHA-256為 `8cbeca36a1a125433ebe2d88c680150529f137044d313ca31b03a2017d4d2db0`。本張只提交報告與摘要JSON。
