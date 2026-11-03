# 緩速動作來源與實作界線

候選基於共享 r29 的 176 個 source/audio/web pins；新增 `ActionCadence.cs`，不修改素材、伺服器、Brave 規則或技能清單。僅開放既有角色／武器的 Speed2、Brave0 單次攻擊及技能 1/3/4/8。未知 Speed、非零 Brave、緩速道具／裝備／拾取／丟棄及滑鼠對話仍拒絕。

## 來源強度

原 BIN SHA256 為 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。VA 以 ImageBase `0x400000` 為基準；每個核對 chunk 的 RVA、File Offset、大小及 SHA 均在 `source-evidence/native-callers-byte-audit.json`。偽碼用於導覽，原 bytes 才是核對依據。

已核對 A467A0 動作／武器選擇 → A43560 frame 推進 → A3EF40 metadata lookup → A407D0 frame 計時共用後段。新增 18 組固定 x86 執行：action1/18/19、frame index0/1/2、Speed0/2，輸入基底83/125/208。結果為原逐幀值乘2。原 lookup／其他狀態 callees 使用明示中性 stub、synthetic metadata；這不是完整原生 scheduler、self-state 解密或原遊戲執行。詳見 `action_evidence.py`、`source-evidence/action-frame-oracle.json`。前輪既有 Speed/JAR oracle 僅引用，不將其重新算成新測試。

原 JAR SHA256 為 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。本輪只抽出固定 class 並執行 javap，不初始化或執行伺服器類別。`L1SkillUse.reserveCastLocksHeld` 先由 getActionInterval / tryReserveSkillAction 預約動作，再由 getReuseDelay / L1SkillDelay.tryReserveSkillUse 保留技能 reuse。兩項各自計時，不相加或整段乘2。class entry pins、javap 本文在 `source-evidence`。

## 分離的時間

| 量 | Speed2 策略 | Speed0/1 |
|---|---|---|
| server 動作基底 | profile ServerActionMs ×2 | 保留 r29 基底 |
| actor 幀／動畫 | 每幀原 milliseconds ×2；缺欄位以 ticks×1000/24 整數截斷後×2 | 原 ObjectAppearance.At 與 actionMs |
| marker 等待 | r29 同一序列 `!` 前綴 ×2 | 相同前綴 |
| 技能 reuse | 原值，與動作取 max | 原值 |
| 本機 margin | 弓32ms、施法100ms，其他既有值；不乘2 | 原值 |
| 箭／光箭飛行 | 立即首步，其後30／60ms | 原值 |
| 附著特效／impact | 11722、221、2165、219 各自原序列 | 原值 |

候選 Speed2 輸入等待採 `max(2×serverBase, 2×nativeFramesTotal, reuse) + margin`。額外 native animation guard 是保守的引擎互斥政策，讓下一動作不截斷已准入慢動作；不是新的伺服器公式。Speed0/1 仍採 `max(serverBase,reuse)+margin`。42 profiles／126 action rows 的分量在 `profile-action-timings.json`，profile 本文 SHA 逐一核對。

gfx0 攻擊 weapon0/4/50 的舊 actionMs 為800/960/1120，但原逐幀和為832/997/1163。因此慢速使用1664/1994/2326，正常路徑保持舊值。weapon46 原逐幀873、server840；弓原1249、server1200、marker791；技能4 原1374、server1320、marker916。慢速弓動畫2498／釋放1582；技能4 動畫2748／釋放1832。這些是保留素材上的相對策略。

## release 與回報原點

A467A0 檢查 event bit1 後進 A46240，啟動待發物件；7C3950／8211E0 鎖定目標一次，7C3AB0／821360 分別使用30／60ms。8ABC10 為獨立純特效週期。沒有把飛行、impact 或所有效果隨施法者乘2。

**本輪沒有完整執行原 actor scheduler 或重建 metadata parser，不能認證原 native 絕對 release 起點。** 原程式的 frame 初始化、先遞增、duration+1 排程仍是完整原生時序的未驗證部分。本輪沿用 r29 的 marker 索引／前綴定義，僅證明共用逐幀放慢及相對倍率；不把1582／1832稱為已復原原生絕對 wall-clock 時點。

候選在 sender admit 當刻建立動作快照。相同 action、target、world、appearance、correction 的首個回報沿用已准入 At、duration、release；只將 Predicted 設 false。這適用正常及慢速，避免回報途中改速重新計時。無匹配預測時以回報時鐘建立新動作。這是引擎預測／確認政策，不是上述尚未驗證的原 scheduler 絕對起點。晚到回報不重播已過期動畫或飛行；攻擊維持既有「收到攻擊回報再保留輸入窗」政策，施法回報維持不額外預約；顯示結束與輸入保守等待是分開的量。

## 生命週期政策

- queued 請求在最後 admit 核對 speed revision、世界、外觀、校正、目標等；即使0→2→0也取消舊請求。
- 當步快照不隨速度回報回捲。已准入動作遇 Speed2 時，用原 admit 時點延長 next*At，之後恢復0/1不能縮短；同值回報不重新起算。
- attack、spell、movement、item、ground 共用互斥下限；技能及攻擊不預扣 HP/MP，不自造伺服器成功。
- await sender 完成與失敗都重驗 generation、lease、world、actor、appearance、weapon、correction、死亡、輸入取消及目標有效性。合法的速度變化不使 completion 失效，Math.Max 保留已延長高水位。失效 completion 不改 deadline、不發 Sent、不讓舊例外取消新世界；finally 只釋放自己仍持有的 lease。
- 自我技能不依賴無關目標選取。釋放前死亡／外觀／武器／世界變更取消待發視覺；釋放時取目標位置一次，之後不追蹤。

候選 helper 使用真實 DLL、封包入口、sender admit、實際 renderer，搭配 synthetic actor、clock、in-memory sender。原素材來源核對、固定原碼算術、離線候選測試三者分列；任何一者都不等於原生正常產品或獨立覆核接受。
