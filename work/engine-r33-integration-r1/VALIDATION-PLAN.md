# r33 後續離線驗收計畫

本輪不建產品、不建 stage、不跑產品或完整 suite。未讀尚未封存的 engine-ground-lifecycle-r1／engine-ground-lifecycle-review-r1。以下是等待真實 accepted candidate／formal review 的驗收條件，不是本輪通過結論。

預期修復限正常 Speed0/1 肉／箭單次 pickup 的 session/world/actor/lease：初始與最後 admission 拒絕過期請求，world／actor 換置釋放舊 busy/deadline/pending，晚 success/error/finally 只操作所屬生命週期。共用 SendGround／ResetGroundSend 自然保護既有手動 drop，不新增 drop 能力。不開 Slow pickup、不新增素材，不改封包、既有入口、900ms／3000ms、Slow gate 或動畫。

來源參考僅為已封存 work/engine-slow-pickup-review-r1，seal `29fb10cb6919a9ea07c13185af3ffc0eab2b8be4944c18093cbf84cb00326095`。其基底 r31，140 rows＝61 baseline pass＋24 future contracts（14 met／10 舊基底差異）＋55 observations，不能當 final r32 或新候選正式 suite，也不加入正式 counts。其有限 source 結論仍 qualified=false：+64 queue/consumer/cancel、完整 native scheduler／輸入窗／pixel與八方向未資格化，900ms 為引擎政策。

未來同一最終 DLL 的驗收依序為 baseline151、accepted independent ground formal suite、utility431＋12 NPC observations、haste1707、versioned-slow1201、F1 112。既有回歸共3,602 assertions，ground 正式數量等 sealed review，絕不預先造數或拿作者自測代替。每套核真正載入的 DLL SHA、實際 build/run argv、exit code、probe、pending、warning；產品與 launcher 零警告／錯誤，其他 helpers 延續原0，utility inherited CS8602 一項明列。

所有 r32 final harness 原樣保存於 reference/r32-regression，原來已接受的 wrapper、原 probe／execution-result／occurrence、suite-adaptation 與 exact source-copy 收據亦保存。reference/r32-final/stage-work/run_final_suites.py 僅供接線參考，含 r32 身份，不直接執行。未來只在新 stage 建獨立副本，再以 r33 真實 identity 接線；不執行舊 setup/run/seal，不修改封存 harness。baseline151 的精確 check-template 支援 pins 在 baseline-lock。

逐 name/id＋一基 ordinal 對帳，重名不去重；versioned-slow 為1,201 occurrences／1,199不同名稱，F1 112與六案例保持。utility 443 rows 中431斷言、12既有NPC觀察按精確IDs分列，即使 observations 的 meetsContract=true 也不加總。r31 haste 歷史820原條件＋156來源支持更新＋577準備新增＋154正式新增映射照錄，不聲稱原976完全未變。

需維持的現有測試：

- utility 的13個 slow-start、24個真 ApplyWorldPacket wrapper ownership 交錯、合法先到回包、當前owner錯誤、queued/final busy、公開互斥與權威狀態。真 TickCount64、100ms guard 不縮放；不得讓 ground 修復削弱 r32 item admission/completion 歸屬。
- haste/harness/Extra.cs 的 slow-item、retained-slow 以及 versioned-slow/harness/Extra.cs 的 item fixtures 仍為 `$26` 紅水，非 exact haste；維持拒絕，不能改成加速水成功案例。Slow ground/pickup/drop/talk 拒絕維持。
- 20項 item/ground 真實時鐘邊界與 overshoot、45項受控−1/0/+1邊界分開；不可虛擬未來 clock 使 wall gate 提前。保留 walking483vs480、speed高水位／snapshot／frame、flight30/60ms、effect與F1。

ground 正式 suite 應由獨立 reviewer 定義並封存，至少覆蓋：

- Speed0/1 肉／箭普通成功與拒絕，準確 C147／既有封包、quantity/ObjectId、未送出前最後predicate；Slow與其他不合格狀態仍拒絕。
- 真 world wrapper 事件、actor替換（若需 synthetic Status 前提須明列）、session取消、新 lease：新world允許新請求後，舊callback不能改新deadline／事件／Faulted／busy。success、false、exception、finally 分別核對；不能只人工換lease冒稱公開新操作已admit。
- admission 防護與 completion 所有權分開。合法 S158/S87/S67/S15/S24 先到並改變資料，在同生命週期仍須有正確成功／當前owner錯誤處理；不可把所有 admission snapshots 照搬成 completion 拒絕，亦不可掩蓋真 transport 全域失敗。
- world／actor重置對 ground busy/deadline/pending 的釋放與對其他 producer 的影響；保留900ms／3000ms、互斥、正常drop原能力與r32 item保護。不得預扣／預增持有量或以本機事件假造權威更新。

先逐字使用舊 suites。若ground修復真的造成失敗，保留原命令／stdout/stderr／probe／SHA，分類為產品缺陷、fixture舊假設或接線問題；只能經另行來源支持與獨立覆核後，在 fresh stage 做最小適配，保留 before/after SHA、patch、逐occurrence理由。禁止降低斷言、刪案例、把觀察冒充assertion、用作者expected取代獨立期待或為通過而機械重跑。

未來共享整合須另派：先獨立備份，old shared source pins 如被接受改動，驗獨立backup舊SHA與現檔accepted SHA，不錯稱未變。外部原件先contains再relative_to，仍驗絕對path SHA／size。新release全SHA一次後才可另授權入口更新；不反覆掃舊assets，不修改舊seal。

native utility scheduler、frame初始化／+1排程／絕對release起點仍unknown；相對marker與額外guard維持引擎policy。離線套件及check-only不構成native/live或P24完成。
