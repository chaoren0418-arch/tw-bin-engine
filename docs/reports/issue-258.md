# Issue #258：T124 記憶座標

## 做了什麼

本輪為同步輪，不新增功能。

### 2026-11-07 再同步（第三次）

`task/258`（同步前 `29b9c093`）再次落後 `origin/main` 25 個提交（基準 `4518ebc3`，含 minimap 模組），PR #271 顯示 CONFLICTING，依常設授權直接合併。

- 衝突兩處，都在共用測試入口：`demo-tests/DemoChecks.csproj`（書籤與 minimap 的 Compile 項都保留）、`demo-tests/Program.cs`（`map-continuity` 群組與預設路徑同時保留 `BookmarkChecks()` 與 `MinimapChecks()`；`minimap`、`bookmarks` 兩個 case 都在）。
- `rebuild-lock.json` 雖自動合併，仍先取 main 版本再 `--update-lock` 重產（216 source／225 pinned）。
- 本模組檔案、`WorldSession.cs`、`docs/REPORT.md` 本輪沒有變更。
- 驗證（合併後、提交前）：`rebuild.py --update-lock`、`--build` 通過（產品 DLL `b7153529…6141`）；demo-tests `bookmarks` 723、`minimap` 347、`map-continuity` 22876、`afflictions` 428、`party` 276、`combat-admission` 1409、`item-transfer` 349、`buddies` 445、`board` 506、`pets` 276、`doors` 597、`polymorph-cadence` 309 項全部通過；`check-no-game-assets.py` 通過（3447 tracked files、0 errors）。
- 新 head 與 CI 結果記在 PR #271 留言（報告本身不自我引用 head）。

以下為上一次同步（第二次）的紀錄：`task/258`（同步前 `208f9489`）合併 `origin/main`（基準 `dd640a75`，落後 44 個提交，含 afflictions、party 等模組），程式與 lock 的同步 head 為 `2bf2ff804bfcb18d64e2ceeb6f2a6dffcf76f487`，沿用 [PR #271](https://github.com/chaoren0418-arch/tw-bin-engine/pull/271)，未強制推送。

- 衝突只有兩處：`demo-tests/DemoChecks.csproj`（書籤與 main 的 afflictions、party 測試項都保留）、`engine/_integration/dev/rebuild-lock.json`（先取 main 版本，再執行 `--update-lock`，未手動拼接 hash）。
- `WorldSession.cs`、`demo-tests/Program.cs` 自動合併，S92 case 與 `BookmarkChecks` 入口都還在。
- `docs/REPORT.md` 與 main 相同，未編輯。
- 本模組檔案（`Bookmarks.cs`、`BookmarkUi.cs`、`bookmarks-tests/`、`docs/protocol/bookmarks.md`）本輪沒有變更。

### 上一輪同步留下、仍待審查的兩處修改（超出「只做同步」字面範圍）

兩處都在上一輪提交（基準 `5555e4a`），只改本模組檔案，其他模組檔案維持 main 內容。

1. **`BookmarkUi.cs` 掛接點改為 `SetVisibleCore`（為了能編譯）**：main 的 `ClanUi.cs` 占用 `OnCreateControl`、`Marriage.cs` 占用 `OnHandleCreated`，合併後都會 CS0111。現在 `SetVisibleCore` 只訂閱一次 `Shown`，面板在 `Shown` 才建立（`InitBookmarkUi`）。面板內容與行為未改。
2. **`Bookmarks.cs` 的 S64 判定改為完整標頭（行為變更，為了讓 main 的測試通過）**：main 的 `CharResetChecks.cs:36` 送 `Pad([64,0x2a])`，要求不得出錯；本模組原本只看子型別 `0x2A` 就接管，標頭不符會丟 `ProtocolException`，造成 `char-reset` 與 `map-continuity` 失敗。現在只有完整標頭 `2A 80 00 02` 才算書籤清單。
   - 代價：標頭不完整或損毀的 S64/0x2A 從「拒絕」變成「當作其他子型別略過」，不會報錯，書籤狀態不變。
   - 不變：標頭之後的欄位槽、筆數、截斷、重複 ID 仍嚴格拒絕。
   - `BookmarkChecks.cs` 與 `docs/protocol/bookmarks.md` 已同步調整；新增標頭損毀與 `Pad([64,0x2A])` 不接管的檢查。
   - 另一個做法是改 `CharResetChecks.cs`，改送完整的書籤封包，可保留嚴格拒絕；因該檔不在可修改範圍且主控規定其他模組用 main 內容，本輪未採用。

既有功能不變：列表、目前位置新增 C165、按名稱刪除 C3、S64／0x2A 清單與 S92 更新，以及 `World.Bookmarks.TryGet(id)`。刪除無確認回包，保留待確認列並停用查詢，重登清單才調和；角色重設清空，S206 跨圖保留。

## 怎麼驗證（已驗證）

下表在 `2bf2ff8` 的內容上執行（基準 `dd640a75` 合併後、提交前）。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，214 個 source／223 個 pinned inputs |
| `rebuild.py --build --configfile scratch/issue-258/NuGet.Config --output scratch/issue-258/build` | 產品、audio、launcher 建置通過，未執行遊戲 |
| `rebuild.py`（唯讀核驗） | 通過 |
| demo-tests `bookmarks` | 723 項通過 |
| demo-tests `afflictions`／`party` | 428／276 項通過 |
| demo-tests `map-continuity` | 22805 項通過 |
| demo-tests `clan`／`marriage`／`char-reset` | 395／412／437 項通過 |
| demo-tests `combat-admission`／`item-transfer` | 1409／349 項通過 |
| demo-tests `buddies`／`board`／`location-share` | 445／506／375 項通過 |
| demo-tests `player-query`／`character-config` | 977／369 項通過 |
| demo-tests `polymorph`／`polymorph-cadence` | 276／309 項通過 |
| demo-tests `ambience`／`invisibility`／`duel` | 632／346／390 項通過 |
| demo-tests `pets`／`fishing`／`doors`／`ghost-mode`／`portals` | 276／315／597／635／376 項通過 |
| `item-use-tests`／`character-management-tests`／`weapon-repair-tests`／`mail-tests` | 426／218／98／119 項通過 |
| `engine/tools/check-no-game-assets.py` | 通過，3413 tracked files、0 errors |

demo-tests 每個群組的數字都包含入口固定執行的共用檢查，所以不等於該模組自己的項數。各群組比上輪多 83 項（`bookmarks` 640→723、`pets` 193→276）來自 main 新增的共用檢查，不是書籤測試有變動。

`2bf2ff8` 的 CI 全綠：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42353770994) 與 [npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42353770898) 均 completed／success；當時 PR 為 CLEAN／MERGEABLE。本報告以後續純文件提交補記，最終 head 與 CI 另記於 PR 留言，避免自我引用。

`SetVisibleCore` 的時機（上一輪實測，本輪未重跑）用 `scratch/issue-258/lifecycle-probe`（空白 Form，與遊戲無關）實測：順序為 `SetVisibleCore > OnHandleCreated > OnLoad > OnCreateControl > Shown`。在建構子強制提早建立 handle 時，`OnHandleCreated` 當下面板尚未建立，而 `Shown` 時已就緒；兩種情況初始化都只執行一次，`RecreateHandle` 不會重複觸發。主視窗由 `Application.Run(form)` 顯示。

環境：Windows x64、SDK 10.0.300、Python 3.12。產品 DLL SHA-256：`f614f5f9c026eaf9565783dd67dd38c490c1e4d71adb0f9774502f1c7e3eaf46`。產品有 2 個警告、0 錯誤（上輪記錄為既有 `MagicUi.cs:20` nullable 警告，本輪只核對數量）；未修改該檔。

### 2026-11-07 同步輪

- `git merge origin/main`（main `4a962f3b`，含 #415 display-notices）。衝突兩處：`demo-tests/Program.cs` 的 `map-continuity` 群組兩邊都保留（`BookmarkChecks()` + `DisplayNoticeChecks()`），並保留 main 新增的 `display-notices` case；`rebuild-lock.json` 取 main 版本後以 `rebuild.py --update-lock` 重產。`WorldSession.cs` 自動合併，書籤 case 與 main 新 case 並存。
- 本機驗證：`rebuild.py --update-lock`（217 source／226 pinned）、`--build` PASS（產品 DLL `db360fd5495a46e12320ba4f7614dde18f7564603a4458a2c8a867096c6a95f1`）；`DemoChecks map-continuity` PASS 22934 checks（含書籤）；`check-no-game-assets.py` pass（3451 檔、0 錯誤）。
- 同步 head `43db979c2eaa46575a614e8239ace08361811637` 的 CI 全綠：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42362195138) 與 [npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42362195055) 均 completed／success；PR #271 為 CLEAN／MERGEABLE。本行以後續純文件提交補記。

### 2026-11-07 同步輪（timed-buffs）

- `git merge origin/main`（main `98bf2b76`，落後 7 個提交，含 #416 timed-buffs）。衝突三處：`demo-tests/DemoChecks.csproj`（書籤與 timed-buffs 的 Compile 項都保留）、`demo-tests/Program.cs`（`map-continuity` 群組同時保留 `BookmarkChecks()` 與 `TimedBuffChecks()`，預設路徑兩者都在，main 新增的 `timed-buffs` case 保留）、`rebuild-lock.json`（取 main 版本後 `--update-lock` 重產）。`WorldSession.cs` 自動合併，S92 書籤 case 與 main 新 case 並存。本模組檔案與 `docs/REPORT.md` 未編輯。
- 本機驗證：`rebuild.py --update-lock`（218 source／227 pinned）、`--build` PASS（產品 DLL `4bbaa80d6dee0bddc72dfc7bc6ae784096dd5b176f6f41e4c949346b7e37718e`）；`DemoChecks map-continuity` PASS 23218 checks（含書籤與 timed-buffs）；`check-no-game-assets.py` pass（3458 檔、0 錯誤）。
- 同步 head `e149a0ca82e65e1d11c7c7e4c906162685f3c9da` 的 CI 全綠：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42365318933) 與 [npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42365319092) 均 completed／success。本段以後續純文件提交補記。

### 2026-11-07 同步輪（item-status）

- `git merge origin/main`（main `2aa41def`，落後 45 個提交，含 #216 item-status）。衝突兩處：`demo-tests/Program.cs` 預設路徑同時保留 `BookmarkChecks()` 與 `ItemStatusChecks()`（main 新增的 `item-status` case 自動合併保留）；`rebuild-lock.json` 取 main 版本後 `--update-lock` 重產。`DemoChecks.csproj`、`WorldSession.cs` 自動合併。本模組檔案與 `docs/REPORT.md` 未編輯。
- 本機驗證：`rebuild.py --update-lock`（219 source／228 pinned）、`--build` PASS（產品 DLL `e03087fa0b6b06598d7307267a0f0d022c1b4dabc4d07b6f8e207ebe1f95eebc`）；DemoChecks `bookmarks` PASS 723、`item-status` PASS 358、`item-transfer` PASS 349、`map-continuity` PASS 23218；`check-no-game-assets.py` pass（3456 檔、0 錯誤）。
- 同步 head `2ebce98ac15d28e9061d3d68e2f58556d8ce43b9` 的 CI 全綠：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42368193291) 與 [npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42368193325) 均 completed／success（只有既有 `MagicUi.cs:20` 警告）。本行以後續純文件提交補記。

### 2026-11-07 同步輪（title-emote）

- `git merge origin/main`（main `9e6d7e8f`，含 title-emote 模組）。文字衝突兩處：`demo-tests/DemoChecks.csproj`（書籤與 title-emote 的 Compile 項都保留）、`rebuild-lock.json`（取 main 版本後 `--update-lock` 重產）。`demo-tests/Program.cs`、`WorldSession.cs` 自動合併，`bookmarks`／`title-emote` case 與 S92 書籤 case 都在。`docs/REPORT.md` 用 main 版本。
- **語意衝突（超出「只做同步」字面範圍，只改本模組檔案）**：main 的 `TitleAndEmote.cs:247` 也覆寫 `ClientForm.SetVisibleCore`，合併後 CS0111 無法編譯。`BookmarkUi.cs` 的掛接點改為 `OnLayout`（全專案原本無人覆寫），同樣只訂閱一次 `Shown`、面板在 `Shown` 才建立；面板內容與行為未改。`TitleAndEmote.cs` 未動。
  - 時機實測：`scratch/issue-258/layout-probe`（空白 Form，`Application.Run` 顯示，與遊戲無關），一般顯示與建構子提早建立 handle 兩種情況順序都是 `OnLoad > OnLayout > Shown`，初始化各只執行一次；`Shown` 內 `RecreateHandle` 不重複觸發。
- 本機驗證：`rebuild.py --update-lock`（220 source／229 pinned）、`--build` PASS（產品 DLL `dbe974e0c46817dde147f2e7e244f57505695f85085a2fef730c79e7cd551add`）；DemoChecks `bookmarks` 723、`title-emote` 704、`map-continuity` 23218、`combat-admission` 1837、`item-status` 358、`afflictions` 428、`polymorph-cadence` 309、`buddies` 445、`item-transfer` 349、`doors` 597 全部 PASS；`check-no-game-assets.py` pass（3463 檔、0 錯誤）。
- 同步 head `3484741ea9005d14f7f4e4c21e8cc5f6bdbc5f32` 的 CI 全綠：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42372762014) 與 [npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42372761954) 均 completed／success（只有既有 `MagicUi.cs:20` 警告）。本行以後續純文件提交補記。
## 未驗證什麼

- **換掛接點後，「記憶座標」分頁實際出現在真實 `ClientForm`，目前沒有任何自動測試覆蓋**：離線測試不經過 `ClientForm` 生命週期，上述只驗到空白 Form 的框架順序與靜態閱讀。需由 dot smoke 確認。
- 分頁順序現在由 `Shown` 訂閱順序決定，預期排在「好友／封鎖」之後、「血盟」之前；依賴名稱開啟分頁的 `PlayerHudServices` 不受順序影響，但畫面未看過。
- 真實伺服器的 S64/0x2A 是否一律符合完整標頭，只依本模組既有協定文件，未以真實封包驗證。若標頭不符，新判定會靜默略過，症狀是書籤清單空白而不是斷線。
- 未啟動遊戲、伺服器或 DB；未驗證原生登入／畫面、書籤新增刪除與重登持久化。
- 離線測試使用產品 DLL、合成封包、記憶體 sender 與獨立控制項；未做真實 socket EOF、網路握手或重連。這些結果不代表真機驗收。
- 未驗證技能 5／69 施放；由法師技能任務處理。

## 產出路徑

- 2026-11-07 同步輪（title-emote）修改：`demo-tests/DemoChecks.csproj`（解衝突）、`engine/source/BookmarkUi.cs`（掛接點 `SetVisibleCore`→`OnLayout`）、`rebuild-lock.json`（重產）、本報告。
- 2026-11-07 同步輪（item-status）修改：`demo-tests/Program.cs`（解衝突）、`rebuild-lock.json`（重產）、本報告。
- 2026-11-07 同步輪（timed-buffs）修改：`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`（解衝突）、`rebuild-lock.json`（重產）、本報告。
- 2026-11-07 同步輪修改：`engine/_integration/dev/demo-tests/Program.cs`（解衝突）、`rebuild-lock.json`（重產）、本報告。
- 前一輪修改：`engine/_integration/dev/demo-tests/DemoChecks.csproj`（解衝突）、`engine/_integration/dev/rebuild-lock.json`（重產）、本報告；其餘差異均為 main 既有內容。
- 既有功能：`engine/source/Bookmarks.cs`、`BookmarkUi.cs`、`WorldSession.cs`；測試位於 `engine/_integration/dev/bookmarks-tests/`，接上 `demo-tests/` 入口。
- 封包格式、伺服器來源 SHA-256 與技能交接：[docs/protocol/bookmarks.md](../protocol/bookmarks.md)。
- 未追蹤本機證據：`scratch/issue-258/` 下的 `build/`、`lifecycle-probe/`、`verify-all.ps1`、`verify-all.log` 與各群組 JSON。

## 給下一棒的提示

1. Claude／codex-review 審查 PR #271 本輪同步，重點是上述兩處修改，並裁定 S64 判定：接受現行的靜默略過，或改 `CharResetChecks.cs` 以保留嚴格拒絕。之後依流程交使用者合併；worktree 由調度器清理。
2. main 合併頻繁，上輪追了四次、本輪又落後 44 個提交；建議本 PR 審查通過後盡快合併。`ClientForm` 的生命週期方法已幾乎被各模組占滿（`OnLoad`、`OnCreateControl`、`OnHandleCreated`、`OnShown`、`OnActivated`、`OnVisibleChanged`、`SetVisibleCore`（已被 title-emote 占用，書籤因此改用 `OnLayout`）），下一個模組會再撞 CS0111。建議開任務把掛接集中到單一入口。
3. `docs/protocol/char-reset.md` 的「本次基底尚無書籤 case」在本 PR 合併後會過時；屬其他模組文件，本輪未改。
4. 法師技能任務用 `World.Bookmarks.TryGet(id)` 查詢，null 表示不存在或刪除待確認。依既有協定報告，伺服器 `BookmarkService.getBookMark(pc, i)` 將技能參數拆成 X/Y；應核對 C6 消費端，不能直接把清單 ID 當傳送參數。
5. dot 集中 smoke 覆蓋「更多」→「記憶座標」（確認分頁確實存在）、登入後書籤清單確實載入（確認 S64 標頭判定）、Big5 名稱、重名／容量／不可標記地點、刪除待確認、重登清單、切角隔離與跨圖保留。
