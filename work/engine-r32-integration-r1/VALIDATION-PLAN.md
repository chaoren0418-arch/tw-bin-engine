# r32 後續驗收計畫；本輪不執行產品套件

唯一例外為Speed2/Brave0既有exact SupportedHastePotion單次C164。保留ObjectId非零、Count>0、Charges0、Bless1、Flags高三位零、desc17、icon77、UseType246、Count1名稱恰`$234`／其餘恰`$234 (Count)`的完整envelope。40013是固定歷史template映射，不可當持有ObjectId。unknown Speed或非零Brave於ItemAvailable採明確保守拒絕，不宣稱原服必然禁止。

只引用已封存utility source/review及兩份主管rootverification。其原基底為r30，來源報告36項與獨立90筆觀察（18個基底差異）不等於r31或新候選通過；不能混入未來測試總數。source report只資格化一個有限請求，其他九類未資格化不等於原服禁止。source-contract/CONTRACT、HANDOFF及原manifest SHA都在reference-pins；不讀未封存實作或另一位r31-baseline-r1準備。

固定JAR `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`：StatusHaste.execute bytecode262/294/326清除29/76/152，360起回報mode1並建立haste。保留共同物品gate及stopPotion/decay698。固定BIN `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`、imagebase0x400000；VA0x94C530是速度接收，0x934690及0x8ABC10為獨立effect來源，不是utility輸入scheduler證明。只沿用封存證據，不新執行JAR/native或重新研究SQL。

未來最終DLL驗收順序為基礎151、接受後的新獨立utility suite、既有r31 haste1707、versioned-slow1201、F1 112。這些是來源套件規模，不硬湊3171或固定新總數。全部核真正載入DLL SHA，保留實際命令／exit code／probe；失敗先最小定位，不機械重跑。

已讀並由r31 stage seal核對9份既有C#測試來源，精確SHA見reference-pins。來源根：`work/engine-r31-integration-r1/r31-runs/accepted-haste-actions-r1-01/stage-work`。

| 位置 | 判定與後續處理 |
|---|---|
| haste/harness/Extra.cs:16、76 | ItemGround使用desc1342/icon80/use249、`$26 (3)`紅水；slow-item-ground-talk-rejected不是exact haste，必須保留拒絕。NPC direct/pointer舊差異不可被此名稱掩蓋或藉此擴大修改。 |
| haste/harness/Extra.cs:116–123 | retained-slow-nonwalk-rejection與queued案例使用同紅水、獨立equipment及drop fixture，維持保守拒絕及queued revision失效。 |
| versioned-slow/harness/Extra.cs:41、61 | item同樣是紅水，independent-slow-action-strictly-rejected-item不得因新加速水例外改為成功；pickup/drop保留。 |
| haste/harness/Haste.cs:63–82 | 20項item/ground真實TickCount64前／後邊界及45項可控−1/0/+1分開，保留最後predicate與overshoot，不用虛擬未來ActionClock提前開wall gate。 |
| haste/harness/Candidate.cs:9–30 | 公開producer互斥、人工最後busy/deadline與生命週期情境分列；utility共用流程修補後仍需保留。 |
| versioned-slow/harness/Program.cs:64 | packet-transition-0／2重名；1201次／1199名按name+occurrence對帳。 |
| versioned-slow/harness/Extra.cs:18、57 | walking483vs480、slow攻擊1746／skill4 2848與deadline互斥保留。 |
| f1/harness/Program.cs | 112項renderer恢復保護保留；舊candidateId僅來源標籤，驗真正最終DLL。 |

目前所讀既有slow-item必拒均非exact haste，尚無適配必要。若後續utility或其他封存測試確實對exact haste要求Slow必拒，只能在新stage副本依接受契約替換，留原SHA、最小patch、逐occurrence保留／替換／新增及理由；其他用品不可放寬。新unknown/Brave收緊政策亦須逐案分類，不能一律刪除舊斷言。r31 haste1707本身已有820原條件、156來源支持更新、577+154新增的歷史映射，不能稱原976原樣不變。

新utility suite須包含：

- 完整envelope的單欄變體與非exact物品，Speed0/1普通各類用品回歸，Speed2/Brave0局部例外，unknown/Brave入口及最後predicate均拒絕。
- 初始／最後admit的session、world、actor identity、selection、input、speedRevision、lease、death與busy；同值和往返revision使queued請求失效。保留magic/ground/MoveReadyAt及各deadline，忽略自己持有的itemSending，不能使自身永遠拒絕。
- move/attack/spell/item/ground的公開兩方向互斥；人工注入防禦測試與公開可達競態分開。兩個clock與最後gate同源真實TickCount64，本機0delay+100ms保持，不乘2或0.755；原服delay0不是native scheduler0ms。
- admit後success/error/finally只按session/world/actor/lease歸屬隔離。真S206同session換world、獨立actor替換、session取消、新lease後，舊回呼不得更新新deadline／ItemSent／Faulted或清除新owner。不能藉忽略真transport全域失敗掩蓋問題。
- 已admit後合法S255直接1或2→0→1、S67、S24/242數量變更或最後S57移除，即使先於sender完成，仍同world/actor/lease時成功須恰一次ItemSent。completion不得機械重驗speedRevision、selection、item record全部入場條件，也不得回寫舊Speed/Count。
- C164、sender成功、ItemSent、S55或timer都不預扣Count、改Speed/Brave或假解除Slow；只依權威封包。S67不是成功必要條件，不承諾完整全域回包次序，遠端actor回包不得改自己。
- 保全r31逐frame取整、速度高水位、snapshot、生命周期、F1、flight30/60ms、獨立effect191；沒有新道具、技能或倍率，ground/talk等不新增慢速開放。

原native utility輸入scheduler、攻擊frame初始化／+1 scheduler／絕對release起點仍未認證；保留相對marker策略與引擎policy標示。離線套件與check-only不構成native/live或P24完成。
