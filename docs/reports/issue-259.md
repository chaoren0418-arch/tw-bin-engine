# Issue #259 — T125 組隊（code，第 2 次補正）

## 最新同步結果（2026-11-07）

本輪依調度器「只做同步」要求，先快轉至遠端 `task/259` 的 `ed4da1b`，再合併 `origin/main` 的 `ccaa3e0`（包含 #215 受擊抑制）。首度推送 `c6897d1` 時主線再前進，因此續合併 `44a2951`（#197 道具使用），並重跑以下建置及測試。未新增或修改組隊功能。

- 兩次合併都只有 dev lock 衝突，以 `rebuild.py --update-lock` 重新產生，最終 182 個來源／191 個鎖定檔核驗通過，沒有手動拼接 hash。
- `WorldSession.cs` 與 `Program.cs` 自動合併；核對後保留組隊接收掛接、主線受擊抑制重設及所有既有測試入口。
- `docs/REPORT.md` 完全沿用 main，沒有加入或編輯索引；本次相對 main 仍只有原任務 10 個差異檔。
- 重跑 `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build`：產品及 launcher 成功；產品仍僅有原有 `MagicUi.cs:20` 的 CS8600／CS8602，launcher 無警告。最新產品 DLL SHA-256：`e8e7429ff3993e33f202aaaec1b67b83b246b37c81d92dfc514b9edf8017c14f`。
- 同一 demo-tests 入口依序重跑 `party`、`item-transfer`、`combat-admission`、`map-continuity`，分別 **80／153／358／438 項通過**。後三組均含組隊 80 項，戰鬥組包含新增的受擊抑制 66 項，地圖組包含公告欄 230 項，不加總為互不重複的檢查。
- 另跑受 #197 影響的既有 `slow-actions`：444 項通過（含組隊 80 項）；`ItemUseChecks`：426 項通過。
- `check-no-game-assets.py` 通過（trackedFiles=3296、errors=0），`git -c core.whitespace=cr-at-eol diff --check` 通過。
- 本次產品建置在沙箱內完成；既有測試 obj 寫入遭拒，改以獲准的沙箱外命令執行離線測試，輸出仍限定本 worktree。Git fetch 需寫入共用 `.git/worktrees/task-259`，且沙箱內 `gh` 認證回覆 401，因此遠端操作亦使用獲准的沙箱外命令，未變更認證設定。
- 最新本機證據：`scratch/issue-259/build/`、`party-checks.json`、`item-transfer-checks.json`、`combat-admission-checks.json`、`map-continuity-checks.json`、`slow-actions-checks.json`、`item-use-checks.json`（六個 JSON 均在 `scratch/issue-259/`）。未重新跑音效 148 項；下方音效結果屬前次補正。
- 本次同步提交的 CI 以 [PR #269 的最新 checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/269/checks) 為準；下方 `c0c91ad` 的成功紀錄僅代表前次補正。

給下一棒：本次是合併衝突補正，請調度器核對最新 HEAD 的 CI 與合併狀態，再交既有審查／合併流程。雙角色真機流程仍由 dot 在 M6 smoke 驗收；未啟動遊戲、伺服器或 DB。

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
