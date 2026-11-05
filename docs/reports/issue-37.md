# Issue #37 — T042 dev 局部真機補測

本輪對應 2026-11-05 派工留言 `6649996007`。結論：**04、05、07 通過本輪範圍；06、15 證據不足；10 因產品不支援金幣丟棄而未通過**。已正常關閉本輪客戶端、P24Lab Java、MySQL。這份報告取代上一輪 ibdata1 不可寫的前置阻塞結論，不代表完整 N01 或全部遊戲功能驗收。

## 前置、執行模式與界線

- 執行前與發布前核對 #37 為 open、runner:dot、task:running；同 repo 的 task:running 僅 #37，最新派工後無其他 RESULT。#38 已準備來源與產品。
- 預設環境仍是 workspace-write／auto_review，未設定或聲稱整個工作階段已改成完全存取。依使用者明確放行授權，必要操作經 require_escalated 正式審查執行，帳戶核對為本機 CHAOREN；一般沙箱帳戶為 CodexSandboxOffline。本輪沒有再次出現 setup refresh 或 ibdata1 寫入阻塞，未修改 ACL、屬性、ExecutionPolicy 或其他安全設定。
- 唯讀 `git merge-base --is-ancestor 798bdf1 HEAD` exit 0、PacketTrace.cs 存在；check-only exit 0，revision=dev、uiStarted/networkStarted/servicesStarted=false。未 fetch、merge、checkout、commit、push，未重建產品。以本輪明確禁止 git 寫入為準，未遵循派工模板的分支／推送段落；沒有建立任務分支。
- DLL SHA-256：`5c45ebe0bd0d779b3ca0769d701a1c10486773569da1e5462e441a5f42eed0ae`，收尾重算相同。
- 搬移對照與既有 junction 已核對：舊 P24Lab 路徑已遷至本專案 `environments/p24`，經既有 `D:\BinEngine\environments\p24` 存取。連線後實際核對 port=43324、datadir 與 UUID，符合指定 P24Lab。未連線或查詢正式 MySQL。
- 使用原 P24Lab JAR（SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`）與原 server.policy（`13d92cb5ebf2721d320fcef2ade746580cc163aabed114962620d62b2dc7dfda`），只啟動實驗 42061／43324。未讀寫機密 server.properties、未更改正式 BIN、封存或 docs/REPORT.md。
- 六項各重新啟動客戶端，PID 依序為 24184、12152、23924、10332、20028、18924，均使用服務驗證測試角色（1900249001）。每項獨立 trace，沒有主角色操作。
- 只透過本機 .NET 工具擷取真實視窗／SendInput，未用 cua。每次輸入核對 exe、精確標題與前景 PID。工具為 scratch 內 console 專案；本輪修正 popup 主視窗辨識與動態 UIA 導致截圖中斷的問題，離線建置 0 警告、0 錯誤。未修改產品程式。

## 結果與封包

`C` 表示出站、`S` 表示入站，op 為十進位。以下統計使用各項 `run/NN-inputs.json` 的操作時間窗；10 未送動作，列整個登入工作階段。入站數含附近物件／定時回報，不能把時間相近的每筆封包都歸因於操作。完整分組摘要見 `scratch/issue-37/run/trace-summary.json`，不公開 raw trace。

|項目|結果|實際觀測|操作窗封包|
|---|---|---|---|
|04 八方向|✅|八方向各兩次；起終均 (32571,32957)，map0。起點由當時客戶端 state，終點由正常登出後角色儲存值核對。|C29×16、C95×2；入站99筆。|
|05 點地|✅|點 (720,300) 視窗相對位置，從 (32571,32957) 到 (32574,32957)，3格；正常登出後座標一致。|出站3筆：C29×3；入站23筆：S10×14、S87×3、S118×1、S119×1、S120×2、S158×1、S209×1。|
|06 近戰|⚠️|明確選取牛 2000036217，4次 G，之後目標離開可見範圍／選取消失便停止，未完成擊殺或15次。EXP 412855581→412855581，角色HP686不變；最後攻擊回報 DamageWord=0，不能推定怪物扣血。未取得怪物HP差值或死亡證據。|C229×4、C95×2；S30×4；其餘入站見摘要（全入站81筆），S237×1不能單憑op當作目標HP差。|
|07 光箭|✅|技能4、牛目標；只按一次 M。MP117→115，收到一次光箭效果 Gfx167／Action18／DamageWord15；真實截圖可見藍色效果。傷害欄不當作獨立HP差。|C6×1；S30×1、S33×1、S237×1，另S10×13、S87×1、S120×2，共19筆入站。|
|10 金幣|❌|金幣33400、selectedId=1900249101、droppable=false，丟棄按鈕停用；未丟出／拾取，沒有33400→33399→33400。來源 GroundRules.Droppable 只允許特定肉／箭；IsGround 亦只辨識對應地面物，未繞過、未改產品。|整段C25=0、C112=0；僅C95×5、C119×1、C137×1（7筆），入站222筆；不是金幣成功回包。|
|15 音訊|⚠️|音量50%，未靜音／靜音／取消靜音各按一次F3初級治癒術；三次皆有施法／效果回包。音訊host沒有播放證據，無法認定有聲對照成立。|C6×3、C95×2；S55×3、S71×3、S158×3、S174×3，其他見摘要；總入站85筆。|

04 截圖保留前後真實場景，沒有自行畫上座標；未取得完整16格的逐步伺服器確認，不能把 C29 當作逐步 ACK。05 相同，位置以登出後實際儲存核對。06 的最後截圖因動態 UIA ElementNotAvailableException 未產出，因此不列不存在的 06-after.png。前置點選與接近牛可能產生移動；06攻擊統計只截取實際攻擊窗。

07 的 MP 隨後可自然回復（登出時116），報告的115取自施法後即時state與截圖。15 施法前後MP依序117→114、114→111、111→109，含可能的自然回復，不以淨差反推每次固定成本。

## 音訊三階段明細

|階段|設定|最近host回覆|Playback|RemainingVoices|操作窗內新host事件|
|---|---|---|---|---|---|
|未靜音|gain0.5、muted=false、applied=true|ControlsApplied|null|0|0|
|靜音|gain0.5、muted=true、applied=true|DispatchSuppressed|null|0|0|
|取消靜音|gain0.5、muted=false、applied=true|ControlsApplied|null|0|0|

「最近host回覆」是該階段已知最後設定回覆，並非捏造每次施法有新增播放回覆。實際施法窗未新增host播放事件；未量測 Windows IAudioMeterInformation，也未取得可聽聲音證據。現有 host 記錄來自 ClientEffect6016；仍需開發確認一般攻擊／技能聲音與該host是否同一路徑，不宣稱全部音訊系統失效。

## 本機產出

- 本報告：`docs/reports/issue-37.md`。
- 每項trace：`scratch/packet-trace/04.jsonl`、`05.jsonl`、`06.jsonl`、`07.jsonl`、`10.jsonl`、`15.jsonl`。只記時間／方向／op／長度／序號。
- 證據摘要：`scratch/issue-37/run/trace-summary.json`、`evidence-manifest.json`（截圖與trace SHA-256）、`NN-inputs.json`、`NN-after-role.tsv`、`NN-after-inventory.tsv`、`06-attack-before/after.json`、`07-cast-before/after.json`、`10-blocked-state.json`、`15-phases.json`。
- app原始觀測：`engine/runs/interactive-<PID>/web-events.jsonl`；音訊 `interactive-18924/audio/host.jsonl`。此類state含完整診斷資料，留本機供審查，Issue僅摘要。
- 截圖目錄 `docs/native-baseline/dev-retest/`：
  - 04-before.png、04-after.png、04-start-diagnostics.png。
  - 05-before.png、05-after.png。
  - 06-before.png、06-approach.png、06-diagnostics.png（均非擊殺證明）。
  - 07-before.png、07-effect.png。
  - 10-gold-selected.png。
  - 15-controls.png、15-audible.png、15-muted.png、15-unmuted.png；另有15-volume50.png（切至聲音頁前）與15-closing.png。
- 本輪啟停證據一律在 `scratch/issue-37/run/`。scratch/issue-37 根目錄同名 mysql/preflight 舊檔屬前輪阻塞，不用來證明本輪成功。

## 收尾與未驗證

- 最後客戶端首次 CloseMainWindow 15秒內未退出，重新觀察仍是本輪PID18924，再送一次正常關閉後 exited=true；未強殺。最終角色資料於登出後重讀：HP686、MP114、EXP412855581、STR22、BonusStatus4、Food60、肉1、金幣33400。
- 核對本輪 Java PID7624 的路徑、啟動時間與專屬console後送 CTRL_C。server.log 在05:28:07 UTC記錄玩家／全域資料保存、連線池關閉及「伺服器關機程序安全完成」，Java已退出。
- 同一PowerShell脫離console後呼叫mysql曾遇 `控制代碼無效 0x6`，該mysql指令未執行；在新的shell重核對資料庫身分後執行正常 SHUTDOWN。MySQL於05:28:37 UTC記錄 Shutdown complete。
- 05:29:18 UTC 最終核對：43324／42061皆無LISTENING，本輪六個客戶端、Java及MySQL父子PID皆不存在。證據 `run/server-close.json`、`mysql-close.json`、`shutdown-verification.json`。
- 未手動UPDATE／DELETE資料、未再修10326；只有授權遊戲操作與正常開關服的自然儲存、身分及測試角色唯讀SELECT。未驗證整個datadir逐檔不變。
- 先前SDK初次初始化曾印出「安裝 ASP.NET 核心 HTTPS 開發憑證」，未執行trust；憑證儲存區是否新增仍未驗證。後續工具建置皆關閉該自動產生選項，沒有為本輪更改安全設定。
- 本輪只有上述局部測試，不延伸到正式環境、其他地圖或完整N01。06擊殺／怪物HP、10金幣流程、15實際聲音與峰值仍未驗證。

## 給下一棒

由主控安排開發釐清金幣丟棄／地面辨識支援、一般技能音訊路徑，及可穩定近戰的弱怪測試點，再派局部真機補測。另開資源任務提交現有截圖／trace摘要；本輪不提交git、不公開原始trace。避免直接重跑相同金幣或聲音驗收而沒有產品／測試前置變更。


發布回條：本輪 RESULT 已發表並回讀確認，留言 ID 6650577185，https://github.com/chaoren0418-arch/tw-bin-engine/issues/37#issuecomment-6650577185。
