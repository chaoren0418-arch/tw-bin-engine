# P17 背包與原道具使用

工作者功能驗證完成，準備凍結交主管獨立驗收；實際Goal仍active。最終是否已凍結以ready-for-supervisor.json存在為準。只寫本階段與D:/P17Lab-ClientEngine，沒有啟停舊服務或連2000/3306，沒有開始P18。

## 功能與證據

自主客戶端已由原服清單與更新建模背包，兩介面顯示原名稱／圖示／數量／選取／使用狀態；單次使用核對過的40012/140012恢復指紋，送出後由原服決定消耗、效果與HP。未知使用型別禁用，耗盡／取消／世代更換清選取，無本地假扣物或假回血。

- CLI：三capture鏈6→5→4→0，9個請求、6消耗、3原拒絕。28快照869道具欄位獨立重播通過；兩次失敗完整保留。第一輪冷卻邊界與第二輪動態占格，均未補物／重設／改原規則。
- UI：原7請求6消耗，遊戲U與真實WebView2 DOM各一次傷後恢復、正常耗盡保存重登；33快照1012欄位獨立通過。首個seed缺失仍明示，不能當完整seed驗證。修正掛接先於StartLogin後，無消耗review已完整seed／seq與可見背包pass，主管新保留角色負責修正版整輪覆核。
- CLI原HP藥效+65、另自然+4、第二次藥效+70；UI+80、+65。quantity→self gfx197→HP有完整原包，非只看HP上升。詳見PROTOCOL.md及兩份independent-inventory.json。
- 原JAR八種背包序列化／獨立165欄位，圖示1779像素；16TCP／cipher／Netty、離線render、WebView2、背包gate及combat/object/world/movement四組必要回歸通過。已驗證P16死亡／日誌／長接收讓出UI來源保持，沿用來源證據，未稱新跑。

較清楚的當前畫面：evidence/inventory-worker-review/inventory-saved-depleted-visible-screen.png與-web.png。早期UI後段PNG因焦點負例捲到下方而沒有露背包，原圖保留；目前Shot先scrollIntoView。初次review發現原名稱表延遲載入，已改在背包顯示前載入，最新真實review金幣／鑑定卷軸與原圖一致。

## 建置與使用

在本目錄 PowerShell執行 `./build.ps1 -Clean`，.NET10 SDK、離線vendor包與原JDK參考皆為已識別來源；build/cache/tmp都留階段內。EXE延續相容名build/client/P11.ClientEngine.exe，介面標P17。

同服務運行時，`python -B -X utf8 launch_real.py`開本階段worker帳號；選角色、進入世界，點選背包白色藥水後按U或單次使用，或本機Web背包選取後點單次使用。F9開關面板、F10回遊戲、F11輸入隔離；WASD／方向鍵步行、G既有單次近戰。兩worker藥水目前已耗盡，不能因此補發。Web一般操作不顯示template技術欄位。

主管只在freeze後使用獨立乾淨副本、同JAR/DB：`python -B -X utf8 inventory_integration.py cli supervisor`，再 `python -B -X utf8 inventory_integration.py ui supervisor`。每個預留角色各6瓶，只完整執行一次；driver從原保存位置行走，遇原動態占格有界等待。私密角色／憑證只讀D:/P17Lab-ClientEngine/private-tests.json，禁止將密碼寫回報告。不得跑bootstrap.py、prepare.py、initialize_fixtures.py或initial_state_audit.py重建環境。

獨立檢驗：`python -B -X utf8 verify_inventory.py evidence/inventory-worker-cli/result.json`（或UI結果）；純回歸 `python -B -X utf8 regression_verify.py`；`--inventory-regression evidence/new-regression.json`、`--inventory-icons evidence/new-icons.json`；合成布局 `--web-demo inventory-layout --evidence evidence/new-layout --error-log evidence/new-layout/error.json`，必須新目錄。synthetic明示不是原服效果。其餘沿用P16腳本為歷史參考，未納本次命令白名單，不盲目重跑。

## 環境與交接

原JAR SHA256 d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf；正常game42037 PID13880。DB43317 UUID04891658-d029-11f1-9406-4cedfb679789，datadir D:/P17Lab-ClientEngine/db-data。實際exe／父子／建立時間以ready-for-supervisor.json為準，不能只用PID。

全新5 SQL、21表3685列遷移；155表3343舊玩家狀態於正常JAR前清空，4transit補項、兩schema盟屋/村長皆0。只5個初始合成角色、4組各6瓶，共12堆疊；workerCli/Ui已耗盡，主管1900170003/4仍保留各6瓶且未登入。原狼45024新spawn400827，未改模板/AI/移動距離/HP/藥水/延遲。固定原Netty wildcard bind加loopback准入，獨立guard證據如實標示LAN拒絕會關probe listener，沒有打正常原服。

freeze後自身client/WebView全部釋放，同一JAR/DB保留；Goal active，不修改來源、不登入、不重啟、不清理。主管明確功能接受及交還後才能執行cleanup_final.py：核對交還JSON與source/environment hash、所有角色offline、精確程序身分，原console shutdown 0；JAR與launcher退出後核對同DB再SQL SHUTDOWN。不強殺，保留原exit1／ShutdownControl警告。完成最終清單與停止收據才Goal complete。

## 範圍限制

只一種恢復指紋的單次使用；非完整引擎。沒有裝備、交易、倉庫、商店、製作、丟棄、自動喝水或自動戰鬥。S55效果事件有解碼，原藥水動畫尚未渲染。原$238顯示白色藥水，DB模板名終極治癒藥水，各來源明確區分。實際UI測試為WinForms控制器／WebView2 DOM，非外部鍵鼠注入；原生停用按鈕沿系統暗字，啟用字可讀。完整原始包、失敗history與局限不刪改。
