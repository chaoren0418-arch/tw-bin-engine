# Issue #259 — T125 組隊（code，第 2 次補正）

## 最新同步結果（2026-11-07，第五次同步，本輪只做同步）

依調度器要求，在既有 `task/259` 合併 `origin/main`。開工時落後 18 個提交（`3630dcf`，帶入信件 #267）；工作期間主線又進了求婚 #358（`52695e2`）與角色重置 #359（`659a98b`），也一併合入。除下面這 1 處防退步調整外，未新增或修改功能。

**超出純文字合併的產品調整（1 處，請審查）**：`Party.cs` 收到 S219/953 時呼叫既有的 `ReceiveMarriageYesNo`，作廢舊求婚邀請。main 上 953 會經 `ReceiveTrade` 走到這個函式；本分支的 Party 先攔下 953，不補的話舊求婚在逾時前都還能回答，而這個 Yes-No 在伺服器是共用的 TempID。同時在 `PartyChecks.cs` 補上 3 項雙向檢查（953 作廢舊求婚、654 作廢舊組隊邀請）。先只加測試，未修產品時停在 `party S219/953 expires older marriage invitation`，修正後通過。這項調整超出「不要改其他內容」的字面，但不修等於合併帶進退步，因此修在任務可改的檔案內。

- 衝突：`rebuild-lock.json`（三次）取 main 版後以 `--update-lock` 重產，未手動拼 hash；`DemoChecks.csproj` 保留組隊、求婚、角色重置的測試來源。`WorldSession.cs`、`PlayerHudView.cs`、`demo-tests/Program.cs` 自動合併。
- 語意檢查：S186（信件）、S64（角色重置）與組隊的 S237／S250／S39／S71／S219 不重疊；求婚的 S71（655–661、790）與組隊的 414–426 不重疊。信件與隊伍兩顆按鈕同在不換行的 `menu`，基本 7 顆加這兩顆約 602px，以程式碼推算放得下 900 寬；再加釣魚、鬼魂模式的按鈕時是否超出，沒有實測。
- `docs/REPORT.md` 與 main（`659a98b`）的 blob 相同；相對 main 仍只有原任務的 10 個差異檔。

已驗證（皆在最終合併內容上執行）：

- 來源核驗：**211 個來源／220 個鎖定檔通過**。
- `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build` 通過。產品 DLL SHA-256：`a24f2f4801e2ca687537ad62b331cc31246541b896f39c9ba9e7cec919f32f18`。
- demo-tests 入口全部通過：`party`／`pets` **276**、`duel` **390**、`clan` **395**、`marriage` **412**、`char-reset` **437**、`polymorph-cadence` **309**、`buddies` **445**、`item-transfer` **349**、`doors` **597**、`combat-admission` **1409**、`map-continuity` **22358**。每組都先跑共同檢查（276 項），各組數字不可加總。
- 獨立測試專案：`MailChecks` **119**、`CharacterManagementChecks` **218**、`NpcChecks` **165**、`SoundPacketChecks` **148**、`WeaponRepairChecks` **98**、`PacketTraceChecks` **3468** 項斷言通過。
- 更正：下方第四次報告的 `WeaponRepairChecks` **99** 是筆誤，當次原始 log 為 98 項，測試數量沒有減少。
- `check-no-game-assets.py` 通過（3403 個追蹤檔、0 錯誤）；demo-tests 與 `engine/source` 的 `git -c core.whitespace=cr-at-eol diff --check` 通過。
- 本機未跑 CI 中的 web-resource、ResourceChecks、Effect6016；以 PR #269 最新 head 的 CI 為準。測試證據在 `scratch/issue-259/sync5/`（不追蹤）。

未驗證：未啟動遊戲、伺服器或 DB，未做原生畫面或雙角色連線 smoke。S219 的取代行為只以合成封包驗證。第四次報告列出的組隊／決鬥／血盟交叉仍沒有提交進 repo 的測試；本輪只補了組隊／求婚這一對。

給下一棒：

- 調度器核對 PR #269 的 CI 後接續合併；dot 於 M6 smoke 驗收雙角色組隊、HP、離隊／踢除與重登，順便看 HUD 選單在小視窗是否完整。
- 建議另開小任務，把組隊／決鬥／血盟的 S219 交叉檢查補進 `party-tests`。
- main 既有缺口（不屬本任務）：`ReceiveDuel` 收到 S219/630 時 `return true`，到不了 `Trade.cs`，所以舊入盟申請、舊交易邀請，以及本輪新增的舊求婚邀請，都不會被決鬥邀請作廢。建議由決鬥模組的負責任務處理。
- 本機多個 worktree 共用 refs，`origin/main` 可能在工作中途被推進；核對差異時請用 `MERGE_HEAD` 或固定 hash。

## 前次同步結果（2026-11-07，第四次同步，本輪只做同步）

依調度器要求，在既有 `task/259` 合併 `origin/main`。開工時落後 84 個提交，期間主線又前進兩次（血盟 #262 與索引提交），都已合入。未新增組隊功能，但有 **2 處超出純文字合併的產品調整**，都是為了不讓合併造成行為退步，請審查：

1. `WorldSession.cs`：`ReceiveParty` 與 `ReceiveDuel` 都先跑完再判斷是否已處理，不是串成兩行短路。S219 是共用的 Yes-No 通道，兩者都靠看到對方的邀請來作廢自己的舊邀請；短路時不論誰在前，都有一邊會留下還能回答的舊邀請。兩者認領的封包不重疊（S250：5 對 104／105／106／110；S71：91／93／631／633／634 對 414–426）。
2. `Party.cs`：收到 S219/953 時一併作廢舊入盟申請。main 上 953 會走到 `Trade.cs` 的 `Unhandled(219)`，血盟靠它判定申請被取代；本分支的 Party 先攔下 953，不補的話舊申請最長可回答 5 分鐘。

其他衝突：`DemoChecks.csproj`、`demo-tests/Program.cs` 保留雙方測試來源與 case（含 `petsFailure` 結構），`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產，未手動拼 hash。`docs/REPORT.md` 與 main 的 blob 相同；相對 main 仍只有原任務的 10 個差異檔。

已驗證（最後一次功能性合併 `2899a69`；其後只合入改 `docs/REPORT.md` 的索引提交）：

- 來源核驗：**206 個來源／215 個鎖定檔通過**。
- `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build` 通過（產品與 launcher 退出 0）。產品 DLL SHA-256：`b5217fbe40f96886caffd4a9584862b2a961946c7a5e701addad04e8235e6284`。
- demo-tests 入口全部通過：`party`／`pets` **273**、`duel` **387**、`clan` **392**、`polymorph-cadence` **306**、`buddies` **442**、`item-transfer` **346**、`doors` **594**、`combat-admission` **1406**、`map-continuity` **22058**。每組都先跑共同檢查（273 項），各組數字不可加總。
- 獨立測試專案：`CharacterManagementChecks` **218**、`NpcChecks` **165**、`SoundPacketChecks` **148**、`WeaponRepairChecks` **99**（0 失敗）、`PacketTraceChecks` 退出 0。
- S219 三方交叉（組隊／決鬥／血盟，雙向）：**12 項暫時檢查通過**。回饋要求不改其他內容，所以這些檢查**沒有提交**，跑完已還原；repo 目前沒有測試守住這兩處調整。
- `check-no-game-assets.py` 通過（3386 個追蹤檔、0 錯誤）；demo-tests 與 `engine/source` 的 `git -c core.whitespace=cr-at-eol diff --check` 通過。
- 本機未跑 CI 中的 web-resource、ResourceChecks、Effect6016；以 PR #269 最新 head 的 CI 為準。測試證據在 `scratch/issue-259/`（不追蹤）。

未驗證：未啟動遊戲、伺服器或 DB，未做原生畫面或雙角色連線 smoke。S219 的取代行為只以合成封包驗證，未確認真實伺服器是否會在同一時間送出兩種邀請。

給下一棒：

- 調度器核對 PR #269 的 CI 後接續合併；dot 於 M6 smoke 驗收雙角色組隊、HP、離隊／踢除與重登。
- 建議另開小任務，把 S219 三方交叉檢查正式提交進 `party-tests`（測試內容見上方說明，共 4 個情境）。
- main 既有缺口（本輪未改，不屬本任務）：`ReceiveDuel` 收到 S219/630 時同樣 `return true`，到不了 `Trade.cs`，所以舊入盟申請與舊交易邀請不會被決鬥邀請作廢。建議由決鬥或血盟模組的負責任務處理。

## 前次同步結果（2026-11-07，第三次同步，本輪只做同步）

依調度器要求，在既有 `task/259` 合併 `origin/main`（`d604050`，隨後再合入只改 `docs/REPORT.md` 的索引提交 `1fd46dc`）。未新增或修改功能。

- 衝突 3 個，都是雙方各自新增：`DemoChecks.csproj` 保留組隊與主線的測試來源；`demo-tests/Program.cs` 保留 `PartyChecks()`、`PolymorphChecks()` 與雙方全部 case；`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產，未手動拼 hash。`WorldSession.cs`、`PlayerHudView.cs` 自動合併。
- `docs/REPORT.md` 與 main 的 blob 相同；相對 main 仍只有原任務的 10 個差異檔。
- 來源核驗：**194 個來源／203 個鎖定檔通過**。
- `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build` 通過（產品與 launcher 退出 0）。產品 DLL SHA-256：`219dec4d6211e02daeae95de20e155dda6934ee89ad53185ad8a212ae43d9407`。
- demo-tests 入口全部通過：`party`／`polymorph` **170**、`polymorph-cadence` **203**、`buddies` **339**、`item-transfer` **243**、`combat-admission` **1189**、`map-continuity` **21338**。每組都先跑組隊與變身的共同檢查（合計 170 項），各組數字不可加總。
- `CharacterManagementChecks` **218** 項、`NpcChecks` **165** 項通過。
- `check-no-game-assets.py` 通過（3345 個追蹤檔、0 錯誤）；demo-tests 與 `engine/source` 的 `git -c core.whitespace=cr-at-eol diff --check` 通過。
- 本機未跑 CI 中的 web-resource、ResourceChecks、Effect6016、packet-trace、其他測試專案；以 PR #269 最新 head 的 CI 為準。測試證據在 `scratch/issue-259/*-checks.json`（不追蹤）。

未驗證：未啟動遊戲、伺服器或 DB，未做原生畫面或雙角色連線 smoke。

給下一棒：調度器核對 PR #269 的 CI 後接續合併；dot 於 M6 smoke 驗收雙角色組隊、HP、離隊／踢除與重登。

## 前次同步結果（2026-11-07，第二次同步）

本輪依調度器要求，在既有 `task/259` 合併 `origin/main`（`9f62553`，原分支落後 22 個提交）。只處理同步：`WorldSession.cs` 保留組隊與好友接收掛接，demo-tests 專案及入口保留雙方測試，dev lock 由 `--update-lock` 重產，未新增或修改功能。`docs/REPORT.md` 與 main 完全一致。

- 來源核驗：**186 個來源／195 個鎖定檔通過**。
- `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build` 通過；產品僅有既有 `MagicUi.cs:20` CS8600／CS8602 警告，launcher 成功。
- 既有 demo-tests 入口：`party` **80**、`buddies` **249**、`item-transfer` **153**、`combat-admission` **358**、`map-continuity` **438** 項通過；後四組均包含 80 項組隊檢查，不加總為互不重複的測試。`CharacterManagementChecks` **218** 項通過。
- `check-no-game-assets.py` 通過（0 錯誤）；`git -c core.whitespace=cr-at-eol diff --check` 及 staged 檢查通過。
- 產品 DLL SHA-256：`3545f54fe52924c410bd383cc9c0a8361027df72de1db8c9c5dfd2b9193f1659`。
- 產出：本報告、合併衝突解決及 dev lock；相對 main 仍只有原任務的 10 個差異檔。測試證據為 `scratch/issue-259/{party,buddies,item-transfer,combat-admission,map-continuity,character-management}-checks.json`，建置紀錄為 `scratch/issue-259/build/`。
- 本機建置、測試、套件及暫存均位於此 worktree；唯讀引用原專案 vendor。Git 共用中繼資料與 GitHub 認證操作使用獲准的沙箱外命令，未修改認證設定。
- 沿用 [PR #269](https://github.com/chaoren0418-arch/tw-bin-engine/pull/269)；本輪最新 head 的 CI 結果見 PR checks 與同步留言。下方數據為前次紀錄，不能代替本輪驗證。

未驗證：未啟動遊戲、伺服器或 DB，未做原生畫面或雙角色連線 smoke。

給下一棒：調度器／Claude 審查同步提交及 CI，接續既有合併流程；dot 於 M6 smoke 驗收雙角色組隊、HP、離隊／踢除與重登。

## 前次同步紀錄（2026-11-07）

依調度器退回要求，於既有 `task/259` 合併 `origin/main`（`0380ae6`），並合併遠端 `task/259`（`4769046`）的同步提交，保留雙方歷史。合併均無衝突，未修改組隊或其他功能。

- 已確認 [前次 CI 失敗紀錄](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42324828531)：Linux canonical build 回報 `sourceCount 182 != pinned source files 184`，Windows map-continuity 與 npc-offline 已成功。
- 執行 `rebuild.py --update-lock` 後，唯一 lock 差異為 `sourceCount` 從 182 改為 184；184 個來源／193 個鎖定檔核驗通過，未手動修改 hash。
- `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build` 通過：產品及 launcher 建置成功；產品仍只有既有 `MagicUi.cs:20` CS8600／CS8602 兩項警告。
- 既有 demo-tests 入口重跑 `party`、`item-transfer`、`combat-admission`、`map-continuity`，分別 **80／153／358／438 項通過**。後三組包含組隊檢查，不加總為互不重複的檢查。
- 本次主線帶入的 `CharacterManagementChecks`：**218 項通過**，輸出 `scratch/issue-259/character-management-checks.json`。
- `check-no-game-assets.py`：**3300 個追蹤檔、0 錯誤**；`git -c core.whitespace=cr-at-eol diff --check` 通過。
- 產品 DLL SHA-256：`59609fb3f52ff3acda95f9157120f0249a6dadc15c76a7f4e9fe54645f17d072`。
- `docs/REPORT.md` 完全沿用 main，未編輯索引；本輪額外修改只有 dev lock 與本報告。建置、套件、測試及暫存輸出均位於本 worktree；NuGet config 唯讀引用原專案 vendor。
- 建置與離線測試在沙箱內完成；Git 共用中繼資料存取與 GitHub 認證需使用獲准的沙箱外命令，未變更認證設定。
- 本機證據在 `scratch/issue-259/build/`、`party-checks.json`、`item-transfer-checks.json`、`combat-admission-checks.json`、`map-continuity-checks.json`、`character-management-checks.json`（JSON 均在 `scratch/issue-259/`）。下方其他測試與 CI 數據為前次結果，不能代替本次驗證。
- 沿用 [PR #269](https://github.com/chaoren0418-arch/tw-bin-engine/pull/269)，最新提交 CI 以該 PR checks 與本輪交付摘要為準。

未驗證：未啟動遊戲、伺服器或 DB；未做原生畫面或雙角色連線 smoke。

給下一棒：請調度器／Claude 核對最新提交 CI 後接續審查合併；dot 依既有授權在 M6 smoke 驗收雙角色組隊、HP、離隊／踢除與重登。

## 結論／做了什麼

已補上前次審查指出的產品接收整合缺漏。本輪依退回補正要求，將 `WorldSession.ReceiveCore` 的既有 `opcode==219` 限制移除，透過同一個前置呼叫接收隊伍 S250/104/105/106/110、S39/party、S71/414–426；未重排其他模組或新增其他共用分支。S237 的原有戰鬥 HP 分派、非隊伍 NPC 對話、Yes-No 與伺服器訊息均保留。

同一前置呼叫會在 EnterGame 設定 Entered 前檢查隊伍狀態，修正相同角色重新登入、期間未讀離線快照時殘留的名單、隊長與邀請。原有邀請／接受／拒絕／離隊／踢除與 HUD 名稱、HP 比例實作維持不變；隊伍聊天未修改。

`party-tests/PartyChecks.cs` 已移除 `PartyFeed` 反射捷徑，所有合成伺服器封包都走 `Feed` → `WorldSession.Receive`。新增共用分派、踢除／離隊回覆與同角色重登回歸；測試仍由既有 demo-tests 入口及 Windows CI 執行。

## 已驗證／怎麼驗證

- 先只改測試為實際 Receive，舊產品在第一個 S250/105 隊員斷言失敗（空名單），證明測試能抓到前次缺漏。
- 先執行 `rebuild.py --update-lock`，再執行 `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build`：SDK 10.0.300，產品及 launcher 建置通過；產品只有原有 `MagicUi.cs:20` 的 CS8600／CS8602 兩個警告，launcher 無警告。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-259/party-checks.json party`：**80 項通過**。包含本模組所有 C／S 格式、錯誤封包原子性、多隊員與隊長更新、HP、接受／拒絕、踢除／離隊回覆、舊快照拒送、逾時、取消並行傳送、同角色完整接收重登流程與原有共用分派。測試使用合成封包、記憶體 sender 與 SocketGuard，未連線。
- 同一測試入口 `combat-admission`：**292 項通過**；`map-continuity`：**208 項通過**，兩者各含上述 80 項組隊檢查，不能視為互不重複的總數。
- `check-no-game-assets.py`：通過，trackedFiles=3279、errors=0；`git -c core.whitespace=cr-at-eol diff --check` 通過。
- dev 來源核驗：177 個來源／186 個鎖定檔；只更新 dev lock，未更動封存 lock。
- 最終本機產品 DLL SHA-256：`3341a5bbfb34c5f3543d368185a20a1325d37fd81ecd421ef800edefbc94ca36`。
- CI：來源提交 `c0c91ad1e757c40d8f94bb231011811f77ca6a3a` 全綠：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42315419034) 的 Linux dotnet 與 Windows map-continuity 均 SUCCESS，[npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42315418864) 亦 SUCCESS。Windows 已執行本輪組隊、地圖及戰鬥檢查；PR 衝突已解除，可送審。

本機 Python 使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`。NuGet 暫存 config 唯讀引用原專案 `engine/vendor`；套件、建置與暫存均在本 worktree。沙箱無法覆寫前次 obj 且 GitHub 認證失敗，改於沙箱外執行已授權的建置／測試與 GitHub 操作；未更動任何認證設定。

同步補充：首度推送後 PR 因主線前進回報 DIRTY、未啟動 CI，已合併 origin/main（25bc0f4）。僅 WorldSession 與 dev lock 衝突；保留雙方接收掛接，lock 以 --update-lock 重產，未手動拼接 hash。重新建置及執行上述全部回歸，另執行既有 sound-packets-tests：148 項通過。相對 main 仍只有原任務 10 個差異檔，docs/REPORT.md 與 main 相同。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB，未連正式服務，未做原生畫面與兩角色連線 smoke；離線通過不等於真機驗收。
- `S_PacketBoxParty/104` 排除新加入者與 `L1Party.removeMember` 的伺服器來源疑點，仍須由 dot smoke 確認部署行為；客戶端提供 C43/S39 更新名單，未修改伺服器。來源身份與衍生規格見 `docs/protocol/party.md`。

## 產出路徑／給下一棒的提示

- 本輪追蹤產出：`engine/source/WorldSession.cs`、`engine/source/Party.cs`、`engine/_integration/dev/party-tests/PartyChecks.cs`、`engine/_integration/dev/rebuild-lock.json`、`docs/protocol/party.md`、本報告。
- 本機證據：`scratch/issue-259/build/`、`scratch/issue-259/party-checks.json`、`scratch/issue-259/combat-checks.json`、`scratch/issue-259/map-checks.json`、`scratch/issue-259/sound-checks.json`（不追蹤）；測試原始碼已提交，不以 scratch 輸出代替測試交付。
- 分支維持 `task/259`；沿用開啟中的 [PR #269](https://github.com/chaoren0418-arch/tw-bin-engine/pull/269)。本輪查得遠端 `isDraft=false`，更正前次報告所述草稿狀態。
- 請 Claude／codex-review 審查這次一行掛接及 80 項接收鏈測試，CI 綠燈後由使用者合併。dot 於 M6 smoke 核對邀請接受、隊員 HP、離隊／踢除、同角色重登；本輪沒有要求使用者操作真機或 DB。
