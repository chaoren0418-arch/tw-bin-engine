# P32 拾取生命週期與跨方向時間窗口交接

本輪指定離線子集合通過：**196 個不同 case ID，196 pass／0 fail**。沒有確證產品缺陷，產品未修改或重建。另保留一項 SDK 首次啟動環境偏差，見下文；本交付待唯一主管接回，不代表全案、原生或正式入口驗收。

唯一工作根：`D:\lineage開發\TW1608262200研究\work\P32-pickup-lifecycle-review-r1`。主管：`01a185b4-ecf6-705c-90e6-83b9eb1ba02f`。沒有增開對話、子代理或改模型。

## 實際版本及 oracle

- accepted P30 r2 acceptance SHA：`b37e5993a5a2fd5c0aed6f10e5c1c313da6dbcaf5899fcb729fffd6d08c03cf1`。
- accepted stage seal SHA：`45bf24321192718e090deca882ce1ce884113cb9bfae817b73ab92892d3f5514`。
- 三次 runtime 實際載入 helper 的 CopyLocal `P11.ClientEngine.dll`，直接 Reference accepted stage build；SHA 均為 `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`。沒有 product ProjectReference 或 product build。
- copied／adjacent audio dependency SHA：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。這是 binding 檔案身份，不宣稱 audio assembly playback／device 執行。
- 獨立 prep 原 `artifact-manifest.json` SHA：`2f56ff408f7a2b96197d64313ec898f962ac1c53b49df8acc5b8a7b6cb316a66`。每次 build/runtime gate 都驗其原 SHA，並對 `active-window-slots.json`、`planned-mapping.json`、`test-families.json`、`profile-inputs.json` 驗原 manifest size/SHA。不是把當下雜湊當已封存 oracle。
- lifecycle expected 來自 accepted `PickupPresentation.cs`、`WorldSession.cs`、`WorldObjects.cs`、`NormalHaste.cs`、`Ground.cs` 等現行契約。frame/duration 來自上述獨立 prep，未由候選實測輸出建立 expected。

## 真正新增覆蓋

| 不重複情境鏈 | cases |
|---|---:|
| owner／事件取消、stale callback、合法新 receipt recovery | 39 |
| directions 1–7 active windows | 105 |
| directions 1–7、兩 speed 的 terminal replacement | 42 |
| 最新合法 receipt →不合法 receipt →恢復 | 1 |
| ground lease 舊成功／舊失敗完成隔離 | 2 |
| owner 不變的 guard 隔離 | 6 |
| Reset 後真正不同 S8/S87 actor identity 的新 receipt | 1 |
| **合計** | **196** |

首輪 `runtime-01`：188；`runtime-02 supplement`：7；`runtime-03 actor-reentry`：1。三次 exit0，ID 沒有重複。後兩次只執行新子集合，沒有重跑 188。helper-only build 共3次，全部0 warnings／0 errors；產品 build0。P29 歷史5196、P28歷史斷言、fixtures、樣本與 metadata 不累加。

105 個窗口情境各有 start、最後未到期整數、第一到期整數三個樣本，共315 samples，仍算105 cases。2796/0涵蓋 attack、damage、spellDir、spellNoDir，speed0/1，合法 captured/fallback 組合。另以0/0 damage fallback 的 **583.3333333333334ms** 驗583仍阻擋、584允許。這裡 double 指浮點 duration，並非把動畫延長兩倍；speed2不在本拾取支援範圍。

42個 terminal replacement 情境以0/0控制、2796/0特殊序列、6658/4一般延伸，遍歷方向1–7與speed0/1。實際時鐘取normal `0,41,42,208,209,375,376,959` 或 haste `0,30,31,155,156,280,281,864`，對照各幀key、index與due。terminal584最後1ms內由新receipt替換，舊serial在原finish邊界不清掉新動畫，再走新serial到終點；獨立結果驗證也對新動畫每個後續幀與finish做比較。

## owner、guard、lease 的邊界

`PickupOwner` 真正有12個欄位：Session、World、Map、Actor、Appearance、Correction、Speed、Gfx、Weapon、Direction、X、Y。每欄獨立注入一次最小狀態變化，實際呼叫產品 Validate／Advance；記錄改前與改後owner、serial、reason、frame、stale callback及新合法receipt。

舊規格22個分類不是22個owner欄位。`dimension-audit.json`逐項對帳，本輪執行20類；missingKey資源fault與newProjectileHit真S30不在本派工，未宣稱通過。額外實跑public `LoginConnection.Cancel`、public `WorldSession.Reset`、internal `ApplyWorldPacket`合成S206／S149／S87／S158等、internal Predict／PredictAction及public World.NetworkStep。封包為離線synthetic；不是原服或原生操作。

新actor重入從100切到101，S8/S87均真的經產品parser；新101 receipt建立較大serial，舊100 callback不改其frame。session Cancel也在never-connected LoginConnection上執行；未建立socket。

speed與brave封包會先增加SpeedRevision，故原生命週期案例取消原因是owner mismatch；新增六個guard案例保留owner不變，單獨驗 unsupported speed/brave、dead flag、status identity、HP percent。SelfAppearancePending使Renderable先變false，現行guard順序回報 `world-unavailable`；不偽稱走到後面的 `appearance-pending` 分支。

groundLease不屬於PickupOwner。實際 `SendGround` 加明示in-memory gate，先admit舊操作、收到receipt、ResetGroundSend換lease、admit新操作。舊完成true及舊IOException兩路均不能清新groundSending、發GroundSent或取消新動畫；新完成正常，GroundSent恰一次，已存在地面3個物品數量不變。這是有界completion隔離，不是900ms／3000ms時間驗收，也不是真transport。

## 敏感性與限制

`negative-controls.json`把相同實際結果交給相同判定器，分別故意錯置取消reason、截斷583.333…duration、把terminal key改成pickup frame1；三個baseline都pass，三個錯誤expected都fail。控制不改產品，不算額外產品case。原helper的diagnostic controls也原樣保留。

本輪附帶檢查PickupActions增量、部分inventory/ground數量及AttackReports/Projectiles快照不變；沒有全audio/effect/send event-counter instrumentation。不得宣稱完整event-inert、全權威數量流程或900/3000通過。沒有執行P31最終像素、resource faults、真S30飛行、GUI、原生、網路、DB、服務或launcher／ClientForm。

方向檢查是代表性profile，沒有宣稱42 profiles全方向全狀態cross-product。104 base資格仍historical-selected-payload-matched、312 pickup仍bounded-only，不升格BIN/native等價。

## SDK 環境偏差

build-01原始stderr包含 `Installed an ASP.NET Core HTTPS development certificate.`。P32沒有刻意執行憑證安裝或信任命令；目前僅能確證SDK輸出此訊息，沒有查詢憑證store，因此不斷言已新建哪一張或是否持久改變。**不能宣稱完全沒有可能的憑證store副作用。** 詳見`environment-deviation.json`與原始stderr。

原因是第一個SDK新HOME首次執行未設 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；`DOTNET_SKIP_FIRST_TIME_EXPERIENCE`未阻止它。build-02之前已增加false及停用global-tools PATH自動加入，後續build stderr皆空。未執行信任、移除、猜測清理或改安全設定，保留供主管判定。這不影響DLL／case結果，但列為環境範圍偏差。

## 交接與停止點

`result.json`、`distinct-cases.json`、`coverage-delta.json`、`verification.json`為彙總；`runs/runtime-*/runtime.json`保存逐案完整owner／serial／frame／callback；各run command.json與stdout.raw/stderr.raw保存實際argv、cwd、exit、環境覆寫與raw SHA；每run保存當次helper／oracle source snapshot。

`source-manifest.json`列初讀來源，`source-audit-addendum.json`列事後補讀來源，`asset-post-audit.json`只核實際用到的有限profile，不冒稱全資產audit。`artifact-manifest.json`封存交付，`verify-delivery.json`記錄驗封結果。重現見`REPRODUCE.md`。

正式入口仍r33。保留原 `ENGINE-R34-ENTRY-ACTIVATE-P26-R1` automatic approval review denied：該派工會持久修改正式入口與project-manifest，把r34升為現行產品，屬正式承諾；使用者未明示此次入口啟用。原動作未執行、未重試；本輪沒有代行或改道。唯一共享整合者仍01a17a22-17e4-70c0-9792-166ddb86ea42。

完成封存後停寫，等待唯一主管接回；未自行擴張派工。Goal真實完成狀態見`goal-readback.json`。
