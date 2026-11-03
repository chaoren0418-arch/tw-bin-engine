# P34 擴展拾取與權威事件交互覆核

**11 條不同離線事件鏈通過，0 產品失敗，0 確證產品缺陷。** 固定產品未修改或重建。交付待唯一主管接回；不代表 native、正式入口或全案完成。

主管為 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f`；本工作者只寫 `work/P34-pickup-authoritative-flow-review-r1`，未開代理／對話。唯一共享整合者維持 `01a17a22-17e4-70c0-9792-166ddb86ea42`。

## 固定基準與來源

- P30 acceptance SHA：`b37e5993a5a2fd5c0aed6f10e5c1c313da6dbcaf5899fcb729fffd6d08c03cf1`。
- P30 stage seal SHA：`45bf24321192718e090deca882ce1ce884113cb9bfae817b73ab92892d3f5514`；stage 為 `work/P30-pickup-profiles-stage-r1/future-stage-02`。
- 兩次 runtime 內部實際載入 `P11.ClientEngine.dll` SHA 均為 `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`，相鄰 audio SHA `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。helper 只有直接 Reference，沒有 ProjectReference。audio 僅驗身份，未作裝置／事件執行聲明。
- P31/P32 accepted receipt SHA：`217e44587a14ae2b65441aceb4f551cca1717d4b228616e51d5a2ec6a62acccf`。原 56 pixel cases、196 lifecycle cases 未重跑、未累加。
- frame expected 來自原獨立 prep `planned-mapping.json`，其原 manifest SHA 為 `2f56ff408f7a2b96197d64313ec898f962ac1c53b49df8acc5b8a7b6cb316a66`，讀取前核原 size/SHA；oracle 在 runtime 前凍結，沒有從產品 actual 產生 expected。
- 唯讀資產來自 `releases/r34/assets`，只核使用檔；其原 release seal SHA `955ea88173cb36cecaefb6d5e9bbce7d76dfd7bf39ccd7830e56a269ab3155c3`。程序內 AppContext base 改指該 release build 目錄，僅供產品既有 PlayerProfiles 靜態 asset 路徑尋址；實際 loaded product 仍是上述 P30 CopyLocal DLL，未執行 release 主程式。
- `source-manifest.json` 加 `source-audit-addendum.json` 共 58 筆唯讀輸入，對帳全部不變。`ground/36.json` 在首次 runtime 的 ComposeGround re-prime 路徑有使用，於事後依原 release seal 補驗，明列不是 runtime-01 的事前 gate；runtime-02 事前 gate 已包含。未全掃資產、重解 SPR 或掃 PAK。

## 本輪與既有覆蓋差異

P28 regression 的 early reply、900/3000 建構與 transport 故障案例是 0/0 控制；其 pending3000 是 deadline 數值，不是自然逾時演進。既有 `GroundTests.GroundRegression` 有 base fixture 真 3100ms pending 保留／heading，但沒有本輪擴展動畫、inventory、send await 交互。P29 明列完整 controlled transport／quantity adapter 未覆蓋。P32 lease 案例以 ResetGroundSend 切換 lease，沒有900/3000驗收。

| 新事件鏈 | cases | 實際序列與重點 |
|---|---:|---|
| early partial：0/0 s0、2796/20 s0、2796/20 s1 | 3 | C112 validation→admit→await 阻塞→S87地面3→2、S24背包5→6→S158→完成→動畫結束；新數量／frame 不被 completion 覆寫。2796 s0 加同選取 public rapid repeat。 |
| early deletion/add：6658/4 s1 | 1 | C112 count3→admit→S120刪除地面並清選取→S15新背包物件778/count3→S158→完成→finish。 |
| 承認前 selection／speed／appearance | 3 | 已有 extension 動畫→呼叫 public PickupSelected，gate 尚未 admit→三種各自 revision 改變→validation false／completion false；數量不變、無 pending、無 GroundSent；新合法 receipt 恢復。 |
| 承認後 revisions | 1 | admit→早到 receipt→選取、速度、S87外觀換6658/4→數量回包→新 receipt→舊 send完成；原 admission predicate 已 false，但 owner 有效，GroundSent一次，新 serial／數量保留。 |
| 真 S149 teleport cancellation | 1 | admit→早到 extension receipt→S149 清 world/actor/pending→S206/S8/S87/S123新世界→新資料／receipt→舊成功 completion；回 false，不發 GroundSent，不回復舊世界。 |
| 真3000 deadline／恢復 | 1 | admit→完成→跨900仍 pending→跨3000保持 Busy／timeout→public重複拒絕→晚到S158恢復→權威數量→動畫finish→明確新請求→Cancel清理。 |
| 只有900 guard的自然到期 | 1 | admit→早到S158清pending→完成→只有900 guard維持Busy並拒絕repeat→真deadline到期釋放Busy；無重送、無數量改變。 |
| **合計** | **11** | 每條完整鏈算一個case，狀態樣本、欄位、斷言不另計。 |

原先10條於 `runtime-01` 一次通過；對帳指出900與pending同時存在不足以獨立辨識guard，因此只加一條 `runtime-02 guard-only`，沒有重跑原10條。這是有界補足，不是失敗重試。

## 關鍵契約與實測

`Ground.cs:62` 的 SendGround 在初段檢查 MovementBusy；`OnlineMovement.cs:46` 明確包含 GroundBusy；GroundBusy 又包括 groundSending、AwaitingPickupAction、nextGroundAt。主管曾提出「第二次被拒仍先換lease」假設，隨後以完整getter鏈修正。本輪實跑 public PickupSelected：第一筆admit且await未完時，第二次立即false、沒有進gate、lease未變；第一筆成功completion仍發恰一次GroundSent。此假設沒有成立，沒有產品退修。

UI只作靜態來源核對：`GroundUi.cs:25` 按鈕使用 `!MovementBusy`，WebGround仍由同一 public API保護；未執行GUI，API安全不依賴UI必須先擋住。

承認前：`Ground.cs:75` 比較 selection、speed、appearance、input、correction、位置、gfx／weapon、數量等快照。本輪三個revision各自實跑拒絕；不宣稱所有 admission 欄位排列全測。

承認後：`Ground.cs:72,76` 只檢查 session、world、actor、lease。合法權威回包先改數量、選取或外觀／速度，不應把已承認的send當失敗；本輪驗到舊predicate false、completion仍成功，且不改新資料。S149使owner失效，舊completion不發GroundSent。P32 IOException舊lease失敗已接受，本輪不重跑。

`Ground.cs:25,32` 用 Environment.TickCount64 判定3000ms；timeout故意保留pending／Busy，要求等待原服或斷線重登。本輪第一鏈：before=33641203、click=33642103、until=33644203；跨click觀察超過37ms，跨pending超過31ms，總真elapsed3031ms。動畫 ActionClock 保持100000，沒有冒充ground時鐘，也沒有修改deadline。

獨立guard鏈：觀察時已完成send且pending=false，before=33769671、click=33770400；after=33770437，超過37ms後Busy=false。合計兩條少量真單調等待；沒有 private-deadline seam、沒有改產品clock。這些是Windows排程下的有限觀察，不是精密邊界延遲認證。

S158/action15只建立presentation及確認heading，不樂觀改數量。真正數量更新各自由合成S87／S24／S15／S120產品parser處理。動畫正常／加速的1→2→3→terminal→finish均對照sealed exact frame oracle；本輪不再次宣稱P31 final pixel驗收。

## 真實接點、seams 與限制

使用固定 LoginConnection、WorldSession、PickupSelected、實際內部ApplyWorldPacket與Renderer.BindPickupPresentation／AdvancePickupPresentation；Bootstrap沿用已讀P32的packet／空場景建構，不沿用P32 assertions。in-memory sendMovementGate保留實際產品validation／admit callbacks，由helper控制completion；沒有呼叫connect、socket或正式transport。S149透過ApplyWorldPacket不會執行network-loop teleport ACK，原始記錄有assert此seam沒有額外C52；不宣稱真網路ACK已驗。

可核實接點總數：116 synthetic packet feeds、12 gate-called、9 gate-admitted、8 GroundSent、73 observations。9次承認中1次是傳送取消後被丟棄的舊completion，因此只有8次GroundSent；其餘3次gate-called在承認前拒絕。這些是接點／觀察數，不是case數，也不是線上封包數。

全audio／effect事件counter未接點，不能用空快照宣稱全副作用inert。沒有原服、native、GUI、角色、DB、SQL、服務、下載、launcher／ClientForm主程式或憑證操作。104 base仍為historical-selected-payload-matched；312 pickup仍為bounded-slice-only，不升格BIN/native資格。未選profiles、其餘事件排列、真S30、resource faults、真transport仍在範圍外。

## 執行與封存

三次helper-only build全成功，0warnings／0errors；兩次runtime exit0；product build0。build-01尚未runtime時收到主管getter鏈修正，修正未執行的helper timeout-repeat預期並加入rapid-repeat斷言後build-02；原snapshot保留。build-03只增加guard-only入口。沒有產品失敗或改expected迎合實測。`pre-runtime-review.json`記錄原因。

每次首次及後續子程序均明設certificate=false、global-tools PATH=false、telemetryoff、固定PROGRAMFILES(X86)、NuGetAudit=false、UseSharedCompilation=false、nodeReuse=false與本work空offline feed。CLI-home/cache/temp/packages都在本work；HOME、USERPROFILE、CODEX_HOME保持繼承不改。首次build的SDK welcome原stderr保留，沒有憑證安裝訊息；沒有執行憑證命令或調查store。

每run `command.json` 保存argv/cwd/env/exit與raw stdout/stderr hash；同目錄保留helper、oracle、runner、source manifest snapshots。兩份runtime.json保留完整packet hex、gate階段、GroundSent payload、數量／revision／serial／deadline觀察。

`verify.py`讀原始觀察獨立驗92項條件，不只信任C# pass旗標；對actual深拷貝故意改數量、移除GroundSent、移除timeout pending，三種都被拒絕。這3個敏感性控制不是產品case。58 source pins、所有run receipts／snapshots與最終artifact manifest均可唯讀核驗。

重現命令見 `REPRODUCE.md`。交付 `result.json`、`verification.json`、`coverage-delta.json`、來源／artifact manifests，封存後停寫等待主管。

## 保留原拒絕

正式入口仍r33。向唯一整合者派發 `ENGINE-R34-ENTRY-ACTIVATE-P26-R1` 的 send_message_to_thread 曾被automatic approval review denied，未執行、未重試；批准仍待答。本輪不代行或改道。

原原因：「此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。」
