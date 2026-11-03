# P18 地面物品與拾取交接

工作者已完成有限原肉的地面顯示、選取、單次拾取與手動丟棄，遊戲及本機 WebView2 均以原服回包更新背包與地面。CLI、完整 UI、原封包獨立重播及必要回歸通過。這是交審狀態，Goal 保持 active；主管 fresh 角色獨立測試與正式交還前不清理、不重啟、不再登入或改來源。

限制須一併驗收：第一輪 UI 的原始 8 肉最後存在於地面，後續正常重登、等待及步行未再可見，去向未證。完整 UI-r5 使用 CLI 的另一份有限原肉暫借，測後已正常歸還。保存總帳是 24 肉加 8 肉未證，不能說全環境 32 肉守恆；見 `VISIBILITY-NOTE.md` 與 `evidence/finite-ledger.json`。

## 建置與獨立重跑

工作目錄 `D:\lineage開發\TW1608262200研究\P18-地面物品與拾取`。PowerShell 執行：

```powershell
.\build.ps1 -Clean
python -B -X utf8 ground_integration.py cli supervisor
python -B -X utf8 verify_ground.py evidence/ground-supervisor-cli/result.json
python -B -X utf8 ground_integration.py ui supervisor
python -B -X utf8 verify_ground.py evidence/ground-supervisor-ui/result.json
```

每輪輸出資料夾必須不存在；腳本拒絕覆寫。失敗不能重設 fixture、SQL補物或直接再跑原路徑。正常服務已啟動，不重跑 prepare、initialize、guard 或 server start。工具憑證只讀 `D:\P18Lab-ClientEngine\private-tests.json`，不要把密碼寫進報告。主角色與旁觀依 supervisorCli / supervisorUi 配對，均尚未使用。

最後建置 0 errors / 0 warnings。`evidence/ground-regressions-final/run.json` 八組通過：地面 gate/392原朝向/晚到確認、背包、圖示、原序列化器及 combat/object/world/movement。後四組使用標明的 P17 raw fixture 重播，不冒稱 P18 新動態近戰。`evidence/tcp-summary.json` 是 P18 新端點16組 TCP/cipher/Netty；`evidence/web-regression` 是實際 WebView2 焦點、單次橋接及重開；`evidence/ground-layout-final` 是明示合成 UI、舊epoch拒絕與無新封包時的超時通知。`ground-final-regression` 等較早收據亦保留。

## 人工操作

建置後執行 `build\client\P11.ClientEngine.exe --login-port 42039`，選 P18 真實 profile，輸入保留測試角色的帳號／密碼並選角進世界。F9 開關本機面板，F10 遊戲控制。遊戲中選背包肉、數量後按「手動丟棄選取數量」；選地物原可見像素或地面清單，填數量後按「單次拾取」或回到遊戲焦點按 P。Web 有同步的選取、數量、丟棄與一次拾取。距離太遠須自行靠近，不自動走路。被建物或人物遮擋時可從清單選取，不會調高地物層級。

目前僅開放已核對的肉 desc10/icon124/use38/name$23/groundgfx36。零、過量、不支持物品、舊世代、失效選取、忙碌皆不送出；900ms 本機連點 guard 與 P held gate 不改原服規則。原拒絕以回包為準，無本地預扣。拾取逾3秒未確認會停止操作並保留原請求關聯；等待原服或正常取消重登。自身 S158/action15 確認後依原函式計算朝向；旁觀 S122 交叉核對，原拾取動畫尚未繪製。

## 真實證據與失敗歷史

|證據目錄（evidence下）|結果與意義|
|---|---|
|ground-worker-cli|fail；空背包原本無S5，尚未送地面請求，保存未變|
|ground-worker-cli-r2|pass；40雙端檢查點／1319欄位、10地面請求；部分合併、新背包、負例、正常保存重登|
|ground-worker-ui|fail；真UI直到最後全量丟棄均完成，最終像素被角色遮擋；原UI8肉最後在地面|
|ground-worker-ui-r2/r3/r4|fail保留；正常重登／等待／步行未再收到地物，未送C25/C112|
|finite-loan|pass；CLI現有8肉經原C25/C112交UI，兩端正常重登|
|ground-worker-ui-r5|pass；60雙端檢查點／1991欄位、10地面請求；真WinForms控制器、實際Web DOM、原像素選取、焦點負例、正常重登|
|finite-return|fail；接收角色占目的格，路徑不存在；未送地面請求，8肉仍在UI|
|finite-return-r2|pass；接收角色先正常讓位，原8肉object1900181014歸還CLI並正常重登|

每輪 `result.json` 保存首seed、完整server plainHex、sent world commands、兩端世代與sequence、applied邊界、前後snapshot；`db-proof.json` 保存 SELECT oracle、online歸零與同服務身分。`verify_ground.py` 重建完整成功流程；`audit_ground_history.py` 重建失敗與轉移，成功的一致性審計不會把原 fail 改 pass。主管另有自己的獨立原包稽核，不以本產品snapshot相互比較代替。

原素材／協議見 `PROTOCOL.md`、`reference/ground/manifest.json`、`reference/ground/assets/manifest.json` 及 `reference/ground/supervisor`。地面36-0.spr原244bytes／57opaque pixels，背包124.tbt分開；未用生成圖。螢幕證據以 `ground-worker-ui-r5/*-screen.png` 與獨立 `*-web.png` 為準；`*-client.png` 的 WinForms capture 不能合成 WebView2 子表面。UI自動化使用真WinForms控制器及WebView2 DOM，不是外部鍵鼠。完整CLI舊測試末次朝向1，修正可見點後UI與最終測試路線末次朝向2，均由自身S158及旁觀S122證明。

## 環境與凍結

只寫本階段與 `D:\P18Lab-ClientEngine`，真複製來源清單 `inheritance-manifest.json`、`copy-manifest.json`。原 JAR SHA256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf` 未改；五SQL、21表遷移、155表清理加四transit及盟屋／村長補項見 `initial-state-audit.json`。8個新合成角色，各主角初始8肉/6藥/3金/1卷，旁觀空背包；不是正式玩家資料。

JAR PID74244／launcher85972，game42039 原Netty wildcard bind，加獨立loopback peer guard；DB PID13288／launcher67088，loopback43318，UUID `e8ac0d64-d031-11f1-a13e-4cedfb679789`，datadir `D:\P18Lab-ClientEngine\db-data`。精確exe、建立時間、命令與port以 `ready-for-supervisor.json`、`server-ready.json`、`db-identity.json` 為準。已知LAN拒絕probe會使probe listener停止的原行為已保留，未對正常listener重做該測試。未連2000/3306。

交審後 `source-freeze.json`、`environment-freeze.json`、`delivery-manifest.json` 保存雜湊；`evidence/freeze-player-state.json` 核對所有角色離線、worker session/WebView釋放、reserved初始狀態及24肉保存數。環境manifest排除活躍datadir/log/tmp與私密憑證，不排除固定JAR/config/地圖等靜態輸入。服務保持同身分。

主管正式功能驗收及交還後才可執行 `python -B -X utf8 cleanup_final.py`：先核對主管擁有的交還收據與兩份freeze hash，原console shutdown 0，等JAR/launcher退出，再核對同DB後SQL SHUTDOWN。無強殺、重啟、補物或SQL搬位。完成後封存原freeze、更新最終manifest，再實際Goal complete；目前尚未執行。

不宣稱完整引擎、自然怪物掉落、原拾取動畫、全地圖／全物品、裝備／交易／商店／倉庫／製作或自動拾取已完成。P10舊EXE尚未驗收，依派工不阻擋本階段。
