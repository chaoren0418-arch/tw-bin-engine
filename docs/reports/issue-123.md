# Issue #123／T064：DEMO-3 第 2c-b 產品修正

基線 origin/main `4b8ee9f8`；獨立工作樹 `scratch/wt/task-123`，分支 `task/123`。以下根因在修改前記錄，行號指基線。

## 已驗證

1. `WebPanel.cs:52,56,66–72` 同時對 p08.example 設定資料夾映射與 WebResourceRequested。映射直接從 web 讀檔，不經本機 icons handler；T035 已移除 web/icons，故 broken image。filter 是 All、host 及 stageRoot→engine/assets 正確，PNG MIME 正確；問題在路由優先權。參考 Microsoft WebView2 官方回報 https://github.com/MicrosoftEdge/WebView2Feedback/issues/4201 與 https://learn.microsoft.com/microsoft-edge/webview2/concepts/working-with-local-content 。
2. `CombatSfxScene.cs:71–97` 到達後只保留 impact，短距離／延遲 tick 可完全跳過 fly；`CombatSfx.cs:136–143` 自然結束也 Retire，`AudioCore.cs:151–157` 立即 Stop／Close。真機 PID24768 的 combat-sfx.jsonl：06:47:05.5714563 WAV97 StartedWav，06:47:05.6973357 retire（約126ms）；恢復階段 06:57:31.8062994 只有 magic/3/impact。WAV97 pin 存在，SHA-256 2e8661cc26238e422b3233509ba24ae11b3440d8c1d74666cb5c842e7de6f1a7，PCM mono16 22050Hz／23645 frames，時長1.072335601秒。非缺來源，不適用B07豁免。
3. `AudioCore.cs:63–65` 靜音會Gain(0)後Release；`DirectShowDriver.cs:91–96` Release最後graph／renderer；無持續音訊stream。正常播放完成也清理，故空閒／靜音沒有session，恢復設定本身不開stream。保留事件取消政策，另由combat AudioPlayer管理真正的零PCM常駐stream，讓世界期間session存在；零PCM不是事件音效或正峰值證據。
4. `World.cs:69–72` 八鍵對應完整；`KeyboardNavigation.cs:6–11` 統一焦點閘門；`OnlineMovement.cs:90` 靜態地形／載入邊界拒絕C29，已有MotionNotice。`OnlineInput.cs:33` 鍵盤忽略Move結果，沒有HUD回饋。歷史報告沒有payload／每鍵本地狀態，不能唯一判斷Up、End、Left當時是哪個guard；修補已確認的回饋缺失，不放寬地形或焦點。

## 推測

光箭被過早Stop足以造成輸出未及可聽；實際裝置延遲與歷史零峰值的完整因果仍需真機核對。三方向零C29可能受地形或焦點影響，不能當成已證實碰撞。

## 待真機驗證

本任務全程未啟動遊戲、P24Lab、DB或伺服器；下述原生測試均為獨立離線宿主，不是登入／實機驗收。

## 修正與離線回歸結果

| 項目 | 最終修正 | 已驗證範圍 |
|---|---|---|
| F9圖示 | 移除同host的資料夾映射，`WebLocalResource.Install`與`Read`統一路由；靜態頁面只允許原白名單，圖片維持assets/inventory與子路徑reparse限制，合法PNG回image/png，非法／不存在ID回404；no-store避免舊破圖快取。地面圖片保留既有已驗證生成路徑。 | 真正WebView2載入合成HTML／2×2 PNG；實際fetch bytes完全相同、MIME／cache／decode／404通過。另T035 URI與junction安全回歸23項通過（合成PNG，未用真素材）。 |
| 光箭 | `CombatSfxScene`保留飛行事件至impact視窗，零距離也保留第一次[N97]；飛行期重複幀仍依既有序列，impact219不新增假音效。`CombatSfxController`區分自然結束與取消；`AudioPlayer.FinishAsync`關閉該owner新派送，`AudioCore`讓已提交聲音至原生EC_COMPLETE再清理。尾音owner不立即重用；死亡、外觀失效、換圖、關閉仍取消，保留15秒最長清理界線。 | 實際產品S30解析、近距離略過release tick仍派送一次97、重繪不重播、自然完成不Stop、原生完成清理、死亡取消尾音、mute清理、新派送fence；原8,780項CombatSfx全部保留。另獨立原生宿主走合成S30→scene/controller→實際pin97 WAV→own-session量測，三階段通過。 |
| session | `SilentAudioSession`在combat transport啟用後建立真正的零PCM循環stream，靜音／恢復及聲音自然結束不重建，關窗shutdown釋放。既有遊戲事件靜音仍立即Gain(0)並取消，不在解除靜音時重播舊事件。零PCM不算事件音效。 | fake lifetime確認未啟用前不開裝置、四次切換只Start一次／shutdown一次；實際Windows own-session 60筆（有聲設定、靜音、恢復各20）全部存在且峰值0，instance ID完全一致。新增stream僅本程序；不調系統或別的程序音量。 |
| 三方向 | 八鍵映射、焦點保護與碰撞閘門不變。`OnlineInput`把拒絕結果送到HUD，地形阻擋後清除held輸入，避免每tick無聲重試；再次按鍵可重試。攻擊忙碌／角色不可移動也顯示既有具體原因；步行動畫與移動冷卻期間仍等候既有閘門。 | 八鍵逐一通過InputState、表單／非表單焦點、實際地形可走／不可走、記憶體sender核對C29 opcode及heading，共48項；沒有繞過碰撞或往伺服器送測試封包。歷史三鍵的唯一根因仍未確定，沒有「已證實非產品缺陷」的裁定。 |

全部建置0警告／0錯誤。最終dev `--update-lock`、`--build --write-receipt`、`--verify`皆exit0，171來源／180 pins；`engine\啟動新版客戶端.cmd --check-only`為revision=dev，uiStarted/networkStarted/servicesStarted=false。素材拒收檢查通過。

離線斷言：Demo157、CombatSfx8,794、Ground361、PacketTrace36、demo web16、ground web72，合計**9,436**（包含指定既有9,374）；再加T035安全23項，共**9,459**。WebView2原生宿主與音訊取樣另列，不灌入此斷言總數。

原生光箭宿主最終峰值：有聲 **0.2976982**、靜音 **0**、恢復 **0.2976982**；各40筆、共120筆皆找到本程序session。這是合成S30與真實音訊輸出，不是C6／原服MP／實際遊戲畫面或原BIN語義等價證明。

## 證據、重跑與產出

- [build.json](../../work/issue-123/build.json)：最終lock／dev建置命令、退出碼、stdout/stderr。
- [validation.json](../../work/issue-123/validation.json)：check-only、六組回歸、verify、拒收檢查及三個原生宿主命令的完整argv與結果。個別結果同目錄。
- [local-resource-validation.json](../../work/issue-123/local-resource-validation.json)：合成PNG的23項URI／junction安全回歸。
- [evidence.json](../../work/issue-123/evidence.json)：修正前基線、歷史combat log SHA及限定magic摘錄、WAV97 metadata與所有相關來源SHA。原log SHA為 `f0eab4aeb4090f623cdffd59aa60090e8ced831347a32f5d7e02f4ec3ed08f22`。
- 測試入口：`engine/_integration/dev/demo-tests`、`combat-sfx-tests`、`web-resource-tests`、`local-resource-tests`。WebView2測試自行在傳入的scratch目錄產生PNG與HTML；音訊測試的`--session`只送零PCM，`--native-bolt`唯讀已pin WAV97。禁止把這些合成封包結果當成真機證據。

重跑時先將TEMP、TMP、DOTNET_CLI_HOME、NUGET_PACKAGES、NUGET_HTTP_CACHE_PATH、NUGET_SCRATCH設為此worktree的scratch子目錄，停用telemetry／首次憑證生成；依validation.json的argv順序執行，每步非0即停止。SDK10.0.300，Python3.12，Node及本機WebView2。assets與vendor均使用本worktree新建的唯讀引用，沒有複製遊戲資源；產物只在engine/build、各專案obj／bin及既定launcher位置。

最終檔案SHA-256：

- `engine/build/client-product-dev/P11.ClientEngine.dll`：`9123df2a105424af14b53d94bb47f06dbd454dfba7e06537b31b9c6be96f7baa`。
- `engine/build/client-product-dev/P24.Audio.dll`：`f135b5080fb5acab27905d243e3a03fa06bfb7fc941bcf313f8839535abcd63d`。
- `engine/build/product-dev-manifest.json`：`3afa45385cdedd7f666760b388f0640e561508b49788af9df0937c77250ec03d`。
- `engine/_integration/dev/rebuild-lock.json`：`d0f15d92ebca58b221fa6177457fee76782ed1c0fcc61462b77abe927184fe4a`。


## 給 dot 第2d輪／下一棒的提示

1. Claude審查PR、使用者合併後，由本機執行器在互斥工作目錄重新建置dev、核對manifest／DLL及check-only；由dot依既有授權在P24Lab測試角色做真機驗收。本任務沒有改主目錄入口或啟動環境。
2. F9：開背包，核對紅藥／匕首等與原生圖示一致，沒有broken image或「原道具」替代字；關閉再重開，切背包項目及0／超額數量。仍破圖時保存web-events、請求URI與狀態、assets缺檔／拒絕證據，不清掉原件或複製素材。
3. 光箭：先選已學技能4、有效牛目標並停下，記MP與目標ID。收面板、F10後一次M（或手動單次施法）；分有聲、靜音、恢復各一個**有效C6＋S30**事件，完整涵蓋施法延遲與尾音的4秒以上量測窗，記C6／S30 UTC、MP差、gfx167、combat-sfx.jsonl的97 StartedWav／DispatchSuppressed／finish及own-session峰值。至少包含相鄰短距離和一個較遠合法目標，驗首幀不漏、聲音尾段不被impact切斷。
4. 靜音：先保存own PID與音訊session instance，切靜音後取樣確認session仍在且峰值0；送有效光箭或近戰，持續確認0。恢復後**未播放前session就應存在且ID不變**，下一有效事件應正峰值；null必須記缺session，不能換成0。安靜空閒的0PCM只證明session保活，不能替代有聲事件通過。正常關窗後確認本程序音訊資源已釋放；裝置拔除／預設裝置切換未測，不宣稱自動復原。
5. 方向鍵：關面板、F10、等可操作，每個鍵Home／Up／PageUp／Right／PageDown／Down／End／Left單按放開，間隔至少一個移動冷卻。記按鍵前後焦點、HUD拒絕文案、原服／預測座標、C29筆數；阻擋時應明示地形／邊界，另選已知可走空地再按。若明示忙碌則等候；若焦點在控制項，回F10。不可把本地預測當原服終點；需要時各段停點獨立重登取S87。仍有無文案且0封包的鍵，保留當刻證據再判斷焦點路徑。
6. 本張不提供肉供貨／購買前提；「替代物品丟撿循環」仍須沿T063交接由dot合法取得普通肉並驗1／10，不用金幣拒絕或本張圖示通過替代。M1尚不能標完成。

本機初期sandbox初始化失敗，改用經自動審查允許的提升模式後正常執行；沒有待處理的自動審查拒絕。初次新增移動測試誤選地圖外格，修正測試前置為地圖內樣本後通過；中途換行／程式變動造成的lock不符已由最終更新、重建、全套重跑解除。未修改docs/REPORT.md、DB／設定／帳密、封存release、既有junction／硬連結或r33／r34／r35 lock；未操作任何既有遊戲程序。

## GitHub交付

- 分支 `task/123`，產品修正提交 `cb32b4c12e81b5679e84bf9e65393f8ed58810dd`，已push。
- [PR #124](https://github.com/chaoren0418-arch/tw-bin-engine/pull/124) 已建立，未合併、未關閉Issue。
- 產品修正提交的 [CI run 42184918842](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42184918842) 已確認 completed／success；Ubuntu交叉建置不等於原生遊戲驗收。本節補件只改文件，不改上述已驗證產品來源。
- 納入本張新檔後再跑素材拒收檢查：3,082追蹤檔、0錯誤、4個EmbeddedResource宣告。
- 主專案仍是 `task/77`、追蹤檔乾淨；本工作樹僅有未提交的自生 `engine/_integration/dev/receipt.json`，刻意不stage。
- 審查者Claude，真機下一棒dot；使用者僅需原本的合併／驗收流程。
