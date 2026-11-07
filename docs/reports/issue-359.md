# Issue #359 — T142 能力值重置

## 做了什麼

- 新增 `engine/source/CharReset.cs`：C98 出生配點、逐級／十級提升、完成、萬能藥絕對能力值編碼；S64/01–04 嚴格解析、伺服器回包驅動的重置階段與送出保護。
- 新增 `engine/source/CharResetUi.cs`：獨立能力重置視窗，顯示各階段六能力、等級、HP/MP/AC、剩餘點數、出生加點資訊與確認操作。出生／萬能藥數字可編輯；升級能力單選；關閉提示中斷連線，沒有虛構取消封包。
- `WorldSession.cs` 僅新增一個限制 S64/01–04 的 case；本 worktree 基底尚無書籤 case，其他 S64 子型仍走原有路徑。`BonusStats.cs` 未修改。
- 防止重複確認、錯階段、錯點數、超過最大等級、跨 50 級批次提升、過期快照與排隊期間換圖／斷線。逾時與傳送失敗不重送。原生 NPC 的「先啟動傳送、後送 S64/1」已納入流程。
- 測試提交於 `engine/_integration/dev/char-reset-tests/CharResetChecks.cs`，透過既有 DemoChecks 的 `char-reset`、預設組與 CI 使用的 `map-continuity` 入口執行，無須改 CI workflow。
- 更新 dev 滾動 lock，沒有改封存 lock、遊戲資源、DB、伺服器配置、`docs/REPORT.md`。

## 怎麼驗證

已驗證（合成封包、記憶體 sender、編譯；非真機）：

1. `rebuild.py --update-lock` 後，`rebuild.py --build --configfile D:/lineage開發/TW1608262200研究/engine/NuGet.Config --output scratch/char-reset-build`：產品與 launcher 通過，SDK 10.0.300。原始專案 NuGet.Config/vendor 僅讀取，套件快取與建置輸出均在本 worktree。產品有既有 `MagicUi.cs` 兩項 nullable 警告，零錯誤。
2. `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/char-reset-checks.json char-reset`：161 項通過。涵蓋所有本模組 C/S 格式、七職業初始點數、負 AC、半位元組解碼、每個截斷長度、尾端污染、外部 S64 子型隔離、完整兩段配點、freeze、重複與不相符回包、逾時、同角色重登入隔離、並行送出、傳送失敗。
3. 同一測試入口 `map-continuity`：519 項通過，包含本模組與原有地圖／公告板回歸。runner 的 SocketGuard 禁止網路連線，沒有執行遊戲入口或顯示視窗。
4. `engine/tools/check-no-game-assets.py`：通過；`git -c core.whitespace=cr-at-eol diff --check`：通過（既有 WorldSession 為 CRLF 且 `.gitattributes` 設定 `-text`，保留原始行尾）。
5. CI 使用 PR 的 `build` workflow 實際結果；Windows `map-continuity` 會執行以上模組測試，Linux 交叉建置不算原生驗收。

本機產品 DLL SHA-256：`c1ae1a0670fb5a0291098e188d5afa5db9cd8ebdf72830bcacc403bbbd555421`。

## 未驗證什麼

- 未啟動遊戲／伺服器／DB，未登入、操作原生視窗或讀寫 DB；沒有原生畫面驗收。
- 未確認部署 JAR 使用原生 C98 重置或 NPC 自訂 HTML 方案，未實測蠟燭消耗、HP/MP 隨機成長、DB 保存與重新登入後能力。
- C98 handler 對配點缺少完整防護；本機採七職業總和 75、51 級起逐級一點、能力上限 35 的保守檢查。來源規則與本機政策分別記在協定文件；不能把寬鬆 handler 當成任意配點授權。
- 保存後沒有專用成功 ACK；不以一般血量／能力更新宣稱保存成功。原服換圖會退休視窗，保存結果留待真機核對。

## 產出路徑

- 產品：`engine/source/CharReset.cs`、`engine/source/CharResetUi.cs`、`engine/source/WorldSession.cs`。
- 測試：`engine/_integration/dev/char-reset-tests/CharResetChecks.cs` 與 DemoChecks 兩個入口檔案。
- 契約與來源 SHA：`docs/protocol/char-reset.md`。
- 本報告：`docs/reports/issue-359.md`。
- 未追蹤的本機證據：`scratch/char-reset-build/`、`scratch/char-reset-checks.json`、`scratch/char-reset-entry-checks.json`。

## 給下一棒的提示

由 Claude 審查配點語義與單一 S64 分支，再由使用者合併；dev lock 衝突依契約交調度器重新產生，不手拼 hash。書籤後續合併仍須依子型別分派，不能加入無條件 S64 case 蓋過別人的分支。

由 dot 依既有授權在 P24Lab 測試角色驗收原生重置：確認部署採原生方案、NPC 傳送後視窗仍可操作，測 50→51 等級邊界、十級提升、無萬能藥／有萬能藥、關閉重登入，以及完成後的能力、蠟燭與保存結果。若是自訂 HTML 方案，另立任務，不混用 C98。

## 同步紀錄

上一輪同步紀錄原文寫入時編碼損毀（中文變成 `?`），以下依 Issue #359 留言重寫，原文無法逐字還原。

### 第 1 輪（2026-11-07，同步到 `9f62553`，commit `100ba30`）

完整建置通過；重置 161 項、整合 519 項、好友 169 項測試與資源檢查通過。PR #369 三項 CI 全綠。

### 第 2 輪（2026-11-07，同步到 `1fd46dc`）

先合 `119439e`（落後 123 個 commit），推送後 main 又合入 #387，再合一次，以下是最終結果。

只做同步，沒有改 T142 功能。

- 衝突四處，都是兩邊各自新增的掛接，全部保留：
  - `WorldSession.cs`：S64/01–04 的 case 與 main 的環境、隱身、位置分享、角色設定分派並存。全程式只有這一個 `case 64`。
  - `DemoChecks.csproj`：兩邊的測試檔都保留。
  - `demo-tests/Program.cs`：採 main 的 case 組，加回 `char-reset` 入口；`CharResetChecks` 掛回 `map-continuity` 與預設組；兩邊重複的 `buddies` case 去重。
  - `rebuild-lock.json`：取 main 版後以 `--update-lock` 重產（194 來源檔、203 pinned），沒有手拼。
- `docs/REPORT.md` 自動合入 main 版，沒有另外修改。

已驗證（合成封包、記憶體 sender，非真機）：

1. `rebuild.py --build`：產品與 launcher 通過，產品 DLL SHA-256 `89e6f978ec2318ef613f92552a6f4e86a51a00b9b694a46f12fb110c3caba619`（取代上方本機 DLL 值）。
2. DemoChecks 各組全數通過（數字含每次都先跑的 `PolymorphChecks`）：`char-reset` 251、`map-continuity` 21419、`combat-admission` 1109、`polymorph-cadence` 123、`buddies` 259、`item-transfer` 163、`invisibility` 160、`location-share` 189、`player-query` 791、`character-config` 183、`ambience` 446。
3. `check-no-game-assets.py`：通過（3353 個追蹤檔、0 錯誤）。

未驗證：

- DemoChecks 無參數的預設組**失敗、沒有跑完**：`Movement123` 讀 `engine/assets/terrain0.bin` 時找不到路徑，因為本 worktree 沒有 assets junction，CI 也不跑這組。為避免寫到原始專案或封存，沒有把 `args[0]` 指向原始專案，也沒有建 junction。`CharResetChecks` 已由上述兩組涵蓋，但預設組中其他需要真實資源的測試本輪沒有執行。
- 原生畫面、蠟燭消耗與保存結果仍未驗證，見上方「未驗證什麼」。

給下一棒的補充：部分 DemoChecks 會寫到 `<args[0]>/scratch/`（例如 `CombatAdmissionChecks`），在 worktree 跑預設組時不要把 `args[0]` 指向原始專案。

### 第 3 輪（2026-11-07，同步到 `17e3dec`）

落後 main 84 個 commit。只做同步，沒有改 T142 功能。

- 衝突四處，都是兩邊各自新增的掛接，全部保留：
  - `WorldSession.cs`：S64/01–04 的 case 與 main 的寵物清單、寵物背包分派並存。全程式仍只有這一個 `case 64`，main 這批也沒有新增其他 S64 分派。
  - `DemoChecks.csproj`：`CharResetChecks` 與 main 新增的 ghost-mode、doors、fishing、pets、duel 測試檔都保留。
  - `demo-tests/Program.cs`：採 main 的 case 組，加回 `char-reset` 入口；`CharResetChecks` 掛回 `map-continuity` 與預設組。
  - `rebuild-lock.json`：取 main 版後以 `--update-lock` 重產（204 來源檔、213 pinned），沒有手拼。
- `docs/REPORT.md` 沒有另外修改。

已驗證（合成封包、記憶體 sender，非真機）：

1. `rebuild.py --build`：產品與 launcher 通過，產品 DLL SHA-256 `b7b9e7f48c770208366a49e7445e2e2028473705d00e79c2f7c6bbc9dec32129`（取代第 2 輪的值）。
2. DemoChecks 各組全數通過。數字含每次都先跑的 baseline 193 項，`char-reset` 354 項扣掉後就是本模組 161 項：`char-reset` 354、`map-continuity` 22020、`combat-admission` 1326、`polymorph-cadence` 226、`buddies` 362、`item-transfer` 266、`doors` 514、`portals` 293、`pets` 193、`ghost-mode` 552、`fishing` 232、`duel` 307、`location-share` 292、`player-query` 894、`character-config` 286、`ambience` 549、`invisibility` 263。
3. `WeaponRepairChecks`：98 項通過。
4. `check-no-game-assets.py`：通過（3388 個追蹤檔、0 錯誤）。

未驗證：

- DemoChecks 無參數的預設組**失敗、沒有跑完**：與第 2 輪相同，`engine/assets/terrain0.bin` 找不到路徑（本 worktree 沒有 assets junction，CI 也不跑這組）。`CharResetChecks` 已由 `char-reset`、`map-continuity` 涵蓋，但預設組中其他需要真實資源的測試本輪沒有執行。
- `web-resource`、`ResourceChecks`、`effect6016`、`packet-trace` 等其他 CI 項目本機沒有跑，以 PR 的 CI 結果為準。
- 原生畫面、蠟燭消耗與保存結果仍未驗證，見上方「未驗證什麼」。

### 第 4 輪（2026-11-07，同步到 `3630dcf`）

只做同步，沒有改 T142 功能。main 這批合入血盟（#262）與信件（#267）。

- 衝突三處，都是兩邊各自新增的掛接，全部保留：
  - `DemoChecks.csproj`：`CharResetChecks` 與 `ClanChecks` 都保留。
  - `demo-tests/Program.cs`：採 main 的 case 組，加回 `char-reset` 入口；`map-continuity` 與預設組同時跑 `ClanChecks`、`CharResetChecks`。
  - `rebuild-lock.json`：取 main 版後以 `--update-lock` 重產（208 來源檔、217 pinned），沒有手拼。
- `WorldSession.cs` 自動合併，全程式仍只有這一個 `case 64`。
- `docs/REPORT.md` 沒有另外修改。

已驗證（合成封包、記憶體 sender，非真機）：

1. `rebuild.py --build`：產品與 launcher 通過，產品 DLL SHA-256 `41fe3de71d6c9a27bb80dbfe9f89dd32f90f1c58743eadde2d7a68eeb938ab8e`（取代第 3 輪的值）。
2. DemoChecks 各組全數通過（數字含每次都先跑的 baseline 193 項）：`char-reset` 354、`map-continuity` 22139、`clan` 312、`combat-admission` 1326、`polymorph-cadence` 226、`buddies` 362、`item-transfer` 266、`doors` 514、`portals` 293、`pets` 193、`ghost-mode` 552、`fishing` 232、`duel` 307、`location-share` 292、`player-query` 894、`character-config` 286、`ambience` 549、`invisibility` 263。
3. `MailChecks` 119 項、`WeaponRepairChecks` 98 項通過。
4. `check-no-game-assets.py`：通過（3394 個追蹤檔、0 錯誤）。

未驗證：

- DemoChecks 無參數的預設組本輪沒有跑：本 worktree 沒有 assets junction，前兩輪都因讀不到 `engine/assets/terrain0.bin` 而失敗，CI 也不跑這組。`CharResetChecks` 已由 `char-reset`、`map-continuity` 涵蓋，組內其他需要真實資源的測試未執行。
- `web-resource`、`ResourceChecks`、`effect6016`、`packet-trace` 本機沒有跑，以 PR 的 CI 結果為準。
- 原生畫面、蠟燭消耗與保存結果仍未驗證，見上方「未驗證什麼」。

給下一棒的補充：本機 PATH 沒有 `python`，要用 `py -3.12`。`dotnet run` 加 `--no-build` 再加 `-p:` 時，DemoChecks 每組都報 `Unknown regression group`，原因未查；照 CI 不加 `--no-build` 就正常。
