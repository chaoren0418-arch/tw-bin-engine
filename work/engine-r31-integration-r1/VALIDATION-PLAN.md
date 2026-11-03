# r31 後續驗收計畫；本輪未執行完整套件

目標只涵蓋 Speed1/Brave0 的既有合格 profile／weapon 攻擊與技能 1/3/4/8。r30 ActionCadence 的 Slow 路徑已接受，Speed1 的 action cadence／animation 尚未加速。新功能、測試適配與正式建置須等候 accepted haste candidate、獨立覆核、主管精確收據及新派工。

固定來源：BIN SHA256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，image base `0x400000`，VA `0xA407D0`；Speed1 逐 frame `(3*t)>>2`。固定 JAR SHA256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`，ActionIntervalCalculator haste=0.755。Brave0 本身不證明其他 buff 中和，適用前提須逐一明列。

只重用已封存證據：slow-actions-review-r1 的 CONTRACT、native-action-oracle（270 vectors 含 Speed1）、native-byte-audit、action-metadata-vectors、server-action-vectors 與其 source-pins 連到的 slow-review-r1/jar-oracle（既有純函式108項）。本輪核對其 artifact manifest/pins，未重新啟動 JAR、原生客戶端或做反組譯研究。全部路徑／SHA 見 reference-pins.json。主管兩個補充 JSON 是靜態 metadata 推導，不能提升為 native scheduler 驗收。

逐 frame 縮放後加總和整段加總後縮放，在既有126個 action 全部不同；gfx61/weapon0 attack 為621對624、spellDir為962對967。須驗 prefix 與 frame 邊界，包括零時間 frame；不得強制至少1ms，或無條件套用只容許正基底的 walking helper。方向一致性是42×3×8=1008筆靜態比較，不是1008次 native 執行。

原 r30 suite 的151／976／1201／112只是來源套件的已接受計數，不代表可原封不動宣稱 r31 全通過。基礎151 wiring 可保留；只有在新最終DLL上實跑才是 r31 結果。其餘按具體適配與保留項報告實際計數，不能湊舊數量。

封存測試根為 `work/engine-r30-integration-r1/r30-runs/accepted-actions-r1-01/stage-work`；以下檔案五個精確 SHA 由舊 stage seal 核對並列在 reference-pins.json。只能在未來新run複製後適配，不改原檔。

| 舊位置 | r31 待處理項 |
|---|---|
| actions/harness/Program.cs:33 | Requests 的 `(speed==2?basis*2:basis)+guard` 把 Speed1 當普通值；只針對接受後的 Speed1 cadence 版本化，Speed0/2 保留。 |
| actions/harness/Program.cs:57 | 目前 renderer 正向案例只測 Speed0/2；新 Speed1 frame／prefix／動畫案例必須補齊，不能把舊976視為已涵蓋。 |
| actions/harness/Extra.cs:27–28 | Metadata 的 expectedWait 與 AnimationMs 仍以 Speed1 的 server/native/legacy 未縮放值判定；按已接受算法適配。 |
| actions/harness/Extra.cs:38 | TransitionMatrix 的 deadline 只考慮任一側 Speed2；涉及 Speed1 的 deadline／highwater 需版本化。既有 queued revision 失效要求保留。 |
| versioned-slow/harness/Program.cs:64 | packet-transition-0 與 packet-transition-2 各重複兩次，使用名稱＋出現序號；不能 set(name) 當全部斷言。 |
| versioned-slow/harness/Extra.cs:18 | 已接受的 walking Speed1 interval483 與 animation480 差異保留，不以 action 規則覆蓋。 |
| versioned-slow/harness/Extra.cs:57 | r30 Slow attack1746／skill4 2848 的正向 deadline 與動畫互斥保留。 |
| f1/harness/Program.cs | 112項保護是回歸來源；origin candidateId 舊標籤不構成 r31 身份，實際最終 DLL SHA 必須核對。 |

對帳必須保留每次 occurrence；r30 versioned-slow 為1201次／1199個不同名稱，舊r29為1199次／1197名，舊替換是兩次換成四次。後續 adapter 要保存原件、精確 patch、每次保留／移除／新增以及理由，不改保留斷言的運算式；同名重排另需明確 mapping。

後續最終DLL驗收分開以下證據，不把一層倍率套到整個 max/reuse/margin：

- 逐 frame 動畫、prefix rounding、native marker 基底、server0.755、reuse下限、bow32／spell100 margin，以及既有動畫互斥 guard 各自比對。flight30/60ms、attached／impact effects 不縮放。
- Speed0/2、unknown Speed／非零 Brave guards、slow item／equipment／ground／talk 拒絕保持。無新技能、buff、素材、道具或server規則。
- 速度0↔1、1↔2、same、1→0→1、1→2→1：queued revision 必須失效；已接納 snapshot 不縮短／倒退；慢速轉換或完成後 deadline highwater 持續；下一動作才採新狀態。
- own／remote／prediction→same ack／late ack，完成後清乾淨 renderer；move／attack／spell／item／ground busy 互斥。
- session／map／appearance／weapon／correction／target／input／lease／death 的 admission 與 late success/error；保留 queue/lifecycle/F1 全部保護。
- 所有 helper 引用同一最終 product DLL，記錄 SHA；audio／manifest／launcher 與來源鏈對齊。先處理最小失敗原因，不無理由重跑整套。

native absolute release origin、frame初始化與 +1 scheduler 仍未認證。維持已接受 marker 基底與明確的相對策略；離線結果不得宣稱 live／native 或整個 P24 完成。
