# Issue #259 — T125 組隊（code，第 2 次補正）

## 結論／做了什麼

已補上前次審查指出的產品接收整合缺漏。本輪依退回補正要求，將 `WorldSession.ReceiveCore` 的既有 `opcode==219` 限制移除，透過同一個前置呼叫接收隊伍 S250/104/105/106/110、S39/party、S71/414–426；未重排其他模組或新增其他共用分支。S237 的原有戰鬥 HP 分派、非隊伍 NPC 對話、Yes-No 與伺服器訊息均保留。

同一前置呼叫會在 EnterGame 設定 Entered 前檢查隊伍狀態，修正相同角色重新登入、期間未讀離線快照時殘留的名單、隊長與邀請。原有邀請／接受／拒絕／離隊／踢除與 HUD 名稱、HP 比例實作維持不變；隊伍聊天未修改。

`party-tests/PartyChecks.cs` 已移除 `PartyFeed` 反射捷徑，所有合成伺服器封包都走 `Feed` → `WorldSession.Receive`。新增共用分派、踢除／離隊回覆與同角色重登回歸；測試仍由既有 demo-tests 入口及 Windows CI 執行。

## 已驗證／怎麼驗證

- 先只改測試為實際 Receive，舊產品在第一個 S250/105 隊員斷言失敗（空名單），證明測試能抓到前次缺漏。
- 先執行 `rebuild.py --update-lock`，再執行 `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build`：SDK 10.0.300，產品及 launcher 建置通過；產品只有原有 `MagicUi.cs:20` 的 CS8600／CS8602 兩個警告，launcher 無警告。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-259/party-checks.json party`：**79 項通過**。包含本模組所有 C／S 格式、錯誤封包原子性、多隊員與隊長更新、HP、接受／拒絕、踢除／離隊回覆、舊快照拒送、逾時、取消並行傳送、同角色完整接收重登流程與原有共用分派。測試使用合成封包、記憶體 sender 與 SocketGuard，未連線。
- 同一測試入口 `combat-admission`：**291 項通過**；`map-continuity`：**207 項通過**，兩者各含上述 79 項組隊檢查，不能視為互不重複的總數。
- `check-no-game-assets.py`：通過，trackedFiles=3250、errors=0；`git -c core.whitespace=cr-at-eol diff --check` 通過。
- dev 來源核驗：175 個來源／184 個鎖定檔；只更新 dev lock，未更動封存 lock。
- 最終本機產品 DLL SHA-256：`b04f302d4782abc7f642a6189e72335426e83c66342deb0af0e57bbaca52f630`。
- CI：推送補正後另行核對 PR #269 最新 head 的檢查結果，再補記於本報告。

本機 Python 使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`。NuGet 暫存 config 唯讀引用原專案 `engine/vendor`；套件、建置與暫存均在本 worktree。沙箱無法覆寫前次 obj 且 GitHub 認證失敗，改於沙箱外執行已授權的建置／測試與 GitHub 操作；未更動任何認證設定。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB，未連正式服務，未做原生畫面與兩角色連線 smoke；離線通過不等於真機驗收。
- `S_PacketBoxParty/104` 排除新加入者與 `L1Party.removeMember` 的伺服器來源疑點，仍須由 dot smoke 確認部署行為；客戶端提供 C43/S39 更新名單，未修改伺服器。來源身份與衍生規格見 `docs/protocol/party.md`。

## 產出路徑／給下一棒的提示

- 本輪追蹤產出：`engine/source/WorldSession.cs`、`engine/source/Party.cs`、`engine/_integration/dev/party-tests/PartyChecks.cs`、`engine/_integration/dev/rebuild-lock.json`、`docs/protocol/party.md`、本報告。
- 本機證據：`scratch/issue-259/build/`、`scratch/issue-259/party-checks.json`、`scratch/issue-259/combat-checks.json`、`scratch/issue-259/map-checks.json`（不追蹤）；測試原始碼已提交，不以 scratch 輸出代替測試交付。
- 分支維持 `task/259`；沿用開啟中的 [PR #269](https://github.com/chaoren0418-arch/tw-bin-engine/pull/269)。本輪查得遠端 `isDraft=false`，更正前次報告所述草稿狀態。
- 請 Claude／codex-review 審查這次一行掛接及 79 項接收鏈測試，CI 綠燈後由使用者合併。dot 於 M6 smoke 核對邀請接受、隊員 HP、離隊／踢除、同角色重登；本輪沒有要求使用者操作真機或 DB。
