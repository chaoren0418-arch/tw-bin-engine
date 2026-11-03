# ENGINE-SLOW-ACTIONS-REVIEW-R1：獨立來源與驗收契約

這是候選封存前建立的獨立準備。基底為主管指定 r29，未讀實作者未封存的程式或預期。範圍僅 Speed2／Brave0、既有角色／武器及技能 1／3／4／8；未知速度、Brave、道具、伺服器規則不擴張。Speed0／1 保全既有行為，不藉本輪重寫 haste 戰鬥。

## 固定身份與來源強度

- r29 seal `d3ac187e7fc44745864640e4bcc72fefbaeeaa0b0e9d63de7c0c3cfa8e46273d`：`releases/r29/_integration/r29/release-seal.json`。此輪核對 seal 本文，不宣稱重新核驗全部 release 素材。
- r29 DLL `8ccfa4e3b5d253206779a5d4b1e59682263ffbddf5dab43fe15b0aa1f86a7397`。共享 source/audio/web 176 檔逐一符合主管 baseline。素材 integrity `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`。
- 固定 BIN `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`；本文件 VA 均以 image base `0x400000`，RVA=VA−base，File Offset 依 PE 映射。10 個必要函式的 chunks 逐 byte 核對，見 native-byte-audit.json。
- 固定 JAR `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；沿用前次已封存純函式 108 項 oracle。本輪沒有再啟動 JAR。L1SkillUse／ClientActionTimeline 原 class 與固定 JAR entry 逐 byte 相同，才用既存 javap 作靜態旁證。
- 原來源分別在 P21/reference/ranged、P22/reference/magic、已封存 work/engine-slow-review-r1。舊腳本未直接執行；所有新程式只寫本 work。10 個原函式的偽碼／instructions 副本在 native/，原 bytes 是核對基準，偽碼只作導覽。

## action1／18／19 適用鏈

1. `A467A0` 的 action1 先依武器 posture 選攻擊動作，action18 選指定施法（原碼另有隨機31分支），action19 走一般動作路徑。不能把 wire action1 等同所有武器都用 metadata action1；武器變體透過原選擇器選 profile。本輪不開 action31。
2. 同函式初始化 frame index，再呼叫 `A43560`；它推進 frame，呼叫 `A3EF40`；後者以目前 metadata action／frame 呼叫 `A407D0` 取得 frame 時間。
3. `A407D0` 的 speed-family switch 在共用後段。Speed2 是 frame 基底乘2，與 action1／18／19 無互斥。其他狀態倍率必須中和，Brave0 本身不等於其他所有 buff 旗標皆零。
4. 本輪在固定原 x86 指令上，以 action1／18／19、frame0／1／7、10 種基底、speed0／1／2 執行 **270 組**。metadata lookup 與無關狀態 callee 為明示 stub；有核對 action 確實送到原 lookup。這證明共同逐幀算術，不是完整原角色、self-state 解密或原遊戲實測。

原 actor scheduler 存在 frame 初始化／先遞增以及 `duration+1` 排程；不能把本輪純算術說成已重現原生所有 wall-clock 邊界。候選的當步快照／高水位是安全的引擎政策，不宣稱完全複製原 BIN 每次 tick 重讀速度的歷史行為。

## 必須分離的時間

| 項目 | Speed2 契約 | 不可混入 |
|---|---|---|
| actor 動畫 frame | 已證原 frame time ×2；保持幀順序／事件相位 | server cadence、reuse、Windows margin |
| server ATTACK interval | 原 profile ServerActionMs.attack ×2 | 畫面總長、弓的32ms本機餘裕 |
| server SPELL action interval | 原 spellDir／spellNoDir 基底 ×2 | skill reuse |
| 下一次施法可送下限 | `max(2×serverActionBase, reuseMs) + 100ms`，另尊重其他動作互斥及已准入 deadline | 不可 `2×max(base,reuse)+200` |
| 弓本機輸入餘裕 | 仍32ms，加在放慢後 server base 之外 | 不乘成64ms |
| actor→箭／光箭的 release 等待 | 來自 actor marker 前綴時間；放慢 actor 前段，相對倍率2 | 飛行週期、純特效週期 |
| 箭飛行 | 立即首步，之後30ms | 不變60ms |
| 光箭飛行 | 立即首步，之後60ms | 不變120ms |
| 11722／221／2165等附著特效及219 impact | 獨立 effect actor 自己的序列 | 不因施法者 Speed2 全部×2 |

固定 L1SkillUse bytecode 的施法預約先 `getActionInterval`→`tryReserveSkillAction`，另從 `getReuseDelay`→`L1SkillDelay.tryReserveSkillUse`；兩個條件各自保留，不相加也不將 reuse 當 actor duration。四個既有技能的封存 catalog reuse 均為10ms，另有 synthetic reuse=4000 邊界向量防止錯誤公式被小值掩蓋。

gfx0 的 server 例子：空手800→1600、劍960→1920、弓1200→2400（另32）、匕首840→1680、雙手1120→2240。技能4 action18 基底1320→2640（另100）；技能1／3／8 action19 基底1480→2960（另100）。其他角色從各自 profile 讀取，不硬編碼 gfx0。

## 素材與 release 證據界線

42 個已封存 profile、126 個 action rows 已獨立讀取並核對 profile SHA；action-metadata-vectors.json 同時保存 server base、r29 動畫值、原 ticks 的整數逐幀值、marker 前綴。Direction0 為代表，沒有重解所有原 PAK／八方向。

發現三個 **既有** gfx0 攻擊 profile 正規化差異：weapon0 的 r29 動畫800／原逐幀832；weapon4 為960／997；weapon50 為1120／1163。其餘本輪126列的動畫總長吻合。這不是本輪候選缺陷。Speed0／1 不擅改；候選須清楚說明 Speed2 沿用 r29 profile 的增量相位或採原逐幀新慢速路徑，不能把1600／1920／2240自稱原 native 動畫總長。初始 helper 的 renderer 斷言使用 retained profile，相較原逐幀正確實作須依明示策略適配，不能把測試值當來源。

release 鏈：`A467A0` 檢 metadata frame event bit1（`!`）→`A46240` 清掉待發物件並呼叫其 virtual+24；箭 `7C3950`／光箭 `8211E0` 取目標位置並開始飛行。`7C3AB0` 明確 schedule30；`821360` 明確 schedule60，並非從施法者速度計算。`8ABC10` 是獨立純特效 frame 迴圈。這些原 bytes 均已核對。

r29 gfx0 弓 marker 基準791、spellDir916；本次增量可檢查相對放慢至1582／1832以及飛行不變。但本輪沒有完整執行原 actor scheduler／metadata parser，**不額外認證這兩個絕對 marker 起算值與原 native 完全一致**（原 frame0／先遞增及+1排程需要分清）。若候選宣稱重建絕對 native release 而超出沿用 r29 基準，最低補證 supplier 為實作者：原 parser frame index 對照、A467A0→A43560→後續tick的事件 trace／原碼 byte pin、實際 profile marker索引與回包時間原點。此局部證據缺口不得阻擋已證明的近戰／非飛行技能；必要時只保留未證投射分支的明示拒絕，不能全域回退慢速動作。

## 候選必須驗證的生命週期

- 用真正 Attack／CastSelectedMagic 及 sender admit callback 驗 C229／C123／C6，不只 Available bool。選取技能／目標不能自行送包。未知speed／Brave、道具／裝備／拾取／丟棄維持既有拒絕。
- 0↔2／1↔2／同值／0→2→0：queued 未准入請求以 revision 取消或等價重新驗證；transient slow 不能因最後狀態恢復而遺失已送動作的較長 deadline。
- 已准入、async 尚未完成：速度回報或 ack 延長的高水位不得被 completion 短間隔覆寫；取消新session後的 completion 不得污染新角色。送出失敗也不能清掉另一動作／舊有效預約。
- 同一 actor 當步的速度、Brave、外觀／武器、duration、release 必須有一致快照。速度回包不回捲動畫、提前結束或提前 release。下一動作採新狀態。server ack 若替換預測動畫，不能用新的快速度替原已准入慢速動作縮短；須分清新的合法動作與同次 ack。
- own／remote 狀態互不污染；unknown current仍拒絕，恢復supported後不能留下永久不可見快照（r29 F1修正保全）。未知object不創造；死亡不因晚到hit／callback復活。
- map/session/appearance/weapon/target revision、位置校正、移除及死亡在最後admit及晚到回呼重驗。自我技能不因無關target變更被錯誤判成目標技能；真正目標技能必須拒絕 stale target。
- move↔attack↔spell，以及與item／ground的互斥，必須尊重已准入較長窗。不能只讓自己的同類型next*At增加，而讓另一動作入口繞過。精確邊界−1/0/+1與本機餘裕另測。
- release前死亡／map切換取消待發視覺；release取目標位置一次，飛行中不追蹤新座標；30／60ms及impact獨立。實際效果／HP／MP仍依原回包，不本地預扣或自造成功。

race-vectors.json 列明「已執行的準備」與「候選封存後才能完整跑的計畫」，後者不計入已通過數。

## 本輪實際與未做

r29-probe.json 為實際 r29 DLL／renderer，加明示合成actor／packet／in-memory sender；多項 false 是已知新功能缺口。尚未見／審查封存候選，因此沒有候選接受或拒絕結論。初版 helper 誤把遠端 ObjectVisual.StepMs 當 combat duration，已根據原欄位用途改以真實幀與動作終點驗證；只改本區 helper，不改產品。

本輪不啟動正常產品／GUI／socket／服務／DB，不附加原遊戲；get_goal=null，未建立或修改 Goal。準備檢查點不是整體覆核完成；收到主管精確候選後在新子目錄續跑到 accept/reject。
