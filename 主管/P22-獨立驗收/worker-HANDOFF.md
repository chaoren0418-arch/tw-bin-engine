# P22 魔法清單與單次施法交接

本階段實作固定原服已學技能清單、初級治癒術1及光箭4的遊戲／本機Web選取與手動單次施放。工作者CLI及UI全流程通過，另完成有限可見特效補驗。此份為交主管的凍結交接，Goal仍active；**尚待主管同服務乾淨build及reserved CLI/UI功能驗收，尚未授權清理，並非整體引擎完成。**

## 執行邊界

只寫本P22目錄與 `D:/P22Lab-ClientEngine`。唯一初始化 `initialization-start/progress/complete.json` 記錄同一initializer59716的七步exit0；沒有重置資料庫、補HP／MP／物品／技能或更改原技能／傷害／自然恢復／掉落／AI。

第一次正常JAR PID58508／parent59016，DB80716／parent5808，DB UUID `2d062607-d05f-11f1-9855-4cedfb679789`。game42053、DB127.0.0.1:43322，合成42054/42055未占用。PID／建立時間／exe／listener／UUID/datadir由每次oracle守護；詳細以server-start、db-identity、各db-proof與freeze receipt為準。正常服務未重做會關閉listener的LAN測試。隔離probe原結果為 `isolation_pass_availability_fail`：拒絕非loopback正確，但原Netty拒絕後listener不保留，此既有可用性限制沒有隱藏。

四reserved角色 `supervisorCli/Ui` 與各Observer在ready之前未登入；主角初HP500、旁觀700、MP120，主角各有限40堆中的10堆及skill1/4，旁觀空包／無技能。freeze再次SELECT核對，不借用工作者保存角色給主管。

## 工作者證據

|目錄（均在evidence）|結果／意義|
|---|---|
|magic-worker-cli|全流程pass；103 checkpoints，雙端1085/720封包、48 C6；16201欄位獨立replay。首heal原HP500→503；4光箭kill EXP+20、金幣3→48；真MP不足278、自然0→1；兩世代保存重登|
|magic-worker-ui|第一次失敗保留：effect／MP先到，測試在HP commit回包前立即判斷。截止raw HP500／MP117，原保存HP507／MP117；不是用SQL回填raw。改為最多7秒等原HP回包|
|magic-worker-ui-r2|Fresh=false逐欄接續507/117；全流程pass，204 checkpoints，雙端1209/788封包、49 C6；31957欄位獨立replay。EXP+20、金幣3→30(+27)，原278／0→1／兩世代保存；全流程結束HP569／MP1|
|magic-worker-ui-review|有限補驗第一次失敗保留：自然MP1→6、一次Web自癒→3；目標沒有可見像素所以產品未送光箭。正常保存HP591／MP3、32598/32941。新版HP/MP與換行圖已取得|
|magic-worker-ui-review-r2|Fresh=false逐欄接上review保存；等原目標具可見像素再Web選取，僅1 C6光箭；可見原特效、回原位置、兩世代保存pass，2450欄位獨立replay。最後HP595／MP2、EXP416461902、金幣30|

每個目錄的 `result.json` 含首seed之前起的雙端所有世代raw、sent sequence、原server packet sequence及checkpoint applied邊界；`executed-build.json` 固定該輪實際EXE/DLL/PDB hash；`db-proof.json` 為受保護SELECT；`independent-magic.json` 是獨立Python LE/CP950解碼與逐欄核對。失敗的capture尾包和後續原保存值分開，沒有宣稱失敗截尾的HP等於SQL。

完整MP守恆包括每個S8及S33，不把effect視為資源已更新，也不在每個S8重設初值：CLI gen1 `120+8-127=1`；UI-r2 `117+9-125=1`；失敗review `1+5-3=3`；成功review-r2 `3+1-2=2`。各gen2保持當次保存MP。原S8扣魔後S33同值只是duplicate，不能再次扣除。治癒模板4/光箭3，原INT11減耗1後通常3/2；實際絕對更新為權威。

超距CLI實際距離12>10，原結果MP-2且無光箭，**不是不耗魔拒絕**。UI-r2靜態LOS probe在原JAR+原terrain判定阻擋且射程內，觀察窗無effect／MP變動；真目標在觀察窗有移動。只記錄原靜態阻擋與實際silent結果，不把未直接觀察的handler原因或動態門狀態宣稱已證實。非法221、未學2及無效target原結果完整保留，沒有捏造錯誤封包。

## 實際看圖結果

已實際view以下PNG；`*-screen.png`可能是其他前景，未用作產品驗收。`*-client.png`為自有WinForms DrawToBitmap；嵌入WebView是獨立原生視窗，不由此bitmap呈現，右邊底層初始化文字不是Web內容證據，Web另以 `*-web.png` CapturePreview核對。Observer圖為真實旁觀連線由同產品compositor渲染，**沒有獨立旁觀視窗**。

- `magic-worker-ui-r2/magic-self-heal-client.png`：原治癒像素與姿勢可見；相同web圖含原技能選單。
- `magic-worker-ui-r2/magic-kill-rewards-web.png`、`magic-original-MP-refusal-web.png`：EXP+20、金幣+27、原278可見；這些歷史圖有舊長notice橫向溢出，新版已修正。
- `magic-worker-ui-r2/magic-bolt-1-flight-{client,observer-renderer}.png`：受原屋頂遮住，不宣稱可見光箭。
- `magic-worker-ui-review/magic-self-heal-{client,web}.png`：新版兩面板原HP591/700、MP3/120清楚，治癒原像素可見，Web換行正常。
- `magic-worker-ui-review-r2/magic-bolt-review-flight-observer-renderer.png`：清楚的原gfx167飛行光箭。
- `magic-worker-ui-review-r2/magic-bolt-review-flight-client.png`：檔名雖有flight，實際稍晚的主視窗擷取是原gfx219命中藍環；其objects.json明記Phase=impact、gfx219，不以檔名冒充飛行。
- `magic-worker-ui-review-r2/magic-bolt-review-web.png`：HP591/700、MP1/120與傷害word16的限制說明，無橫向溢出。
- `magic-worker-ui-review-r2/magic-relogin-client.png`：原保存重登HP595/700、MP2/120與金幣30；沒有延遲施法。

## 原來源、回歸與最後build

詳見 `PROTOCOL.md`。固定JAR hash `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；原BIN hash `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。JAR profile opcode與原BIN native opcode不相同，已明確分開。

`reference/magic/manifest.json` 為原class/Java/javap；`bin-raw-manifest.json` 為46個含重複chunk、31036bytes的獨立PE原bytes核對，每個VA/RVA/FileOffset完整；`asset-records/asset-entries/cast-timing.json` 為原SPR／PAK／IDX／list與時序。`inheritance-manifest.json`及`inherited-build-removal.json`保留真複製及移除18個誤帶入舊compiler outputs的歷史，沒有hardlink/junction。

35個原JAR serializer向量含真正single1/4的40byte新增／刪除、合法32byte前綴、非零padding／截短／未知／重複。MagicRenderingTests涵蓋原純特效立即前進、terminal wrap、空影格、重疊、世代清除與原時間。原static LOS22390候選／4588阻擋，原完整map獨立核對由主管完成；不代表每個候選已真人走過。

最後版本另只補原HP/MP顯示、換行、native面板高度、無選取文字換行，以及test-only可見像素有界等待、finite-heal等MP真正settle的邊界。歷史動態每輪build hash保留；最終build由最新 `magic-regressions-*/run.json`及`ranged-regressions-*/run.json`固定，兩項魔法及八項弓箭／近戰／背包／地物／裝備回歸均須pass後才freeze。兩個早期繼承合成ID不一致失敗保留在 `pre-live-regression-index.json`，沒有改產品封包去迎合測試。

## 主管驗收與重跑入口

正常服務已啟動；**不要重跑 prepare/initialize_p22/lab init/import/server start**。離線可重跑 `build.ps1 -Clean`、`run_magic_regressions.py`、`run_ranged_regressions.py`，但freeze後工作者不再修改或自行執行；主管在自己的真正乾淨副本build。

原oracle可用 `run_magic_oracle.py`及`run_magic_los_oracle.py`（隔離oracle拒絕network/listen/exec），不呼叫原server main。原來源更新與生成工具須在非freeze工作區使用，不在凍結來源上改寫。

主管reserved全流程入口 `magic_integration.py cli supervisor 1`／`ui supervisor 1`；驗收獨立副本EXE時主管應在自己wrapper將module的 `APP` 指向該乾淨build，結果與執行hash仍獨立記錄。第一次reserved為Fresh=true；有失敗須留獨立receipt、原保存值、Fresh=false受保護續跑，不重置。密碼只在LAB/private-tests.json與private case，不寫報告。

`ready-for-supervisor.json`、`source-freeze-manifest.json`、`environment-freeze-manifest.json`產生後代表正式ready；source freeze包含global.json、全部必要scripts、source/assets/web/reference/vendor/fixtures，client/WebView/session釋放，同一原JAR/DB保留。Goal保持active。只有主管具體 `functional-acceptance-passed-handback-for-cleanup` 後，執行 `cleanup_p22.py <主管實際交還JSON>`：原console shutdown0→JAR及launcher退出→核同DB後SQL SHUTDOWN→四程序及四ports消失；不kill/restart。最後 `final_manifest.py` 包含所有LAB檔案hash及所有階段檔案，根delivery-manifest只排除自己，所有歷史nested manifest均納入。收尾結果另見cleanup-result、result與最終delivery-manifest。

## 未完成範圍

僅兩個原技能與原已學清單；技能學習、其他魔法、音效播放及完整引擎未宣稱完成。未直接證實動態門LOS的精確handler原因。P21原箭丟棄拒絕／LOS動態、P18舊UI8肉與拾取動畫、P19自然地物／真實升級、P10舊EXE0/11等歷史缺口保留；不自行進P23。
