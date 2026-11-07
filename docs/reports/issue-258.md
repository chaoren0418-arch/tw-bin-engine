# Issue #258：T124 記憶座標

## 做了什麼

本輪為同步輪，不新增功能。`task/258`（同步前 `91a53fc52784b82a80d7ed596bb05e1877696e1f`）合併 `origin/main`，同步期間 main 持續前進，共合併四次（最後基準 `5555e4a`，涵蓋血盟、信件、求婚 #358、角色重置 #359）。程式與 lock 的同步 head 為 `6f827cb99b4d7c5abecc1fa497ae80ce372bc0d5`，沿用 [PR #271](https://github.com/chaoren0418-arch/tw-bin-engine/pull/271)，未強制推送。

- 衝突僅在共用分派點與 lock：`WorldSession.cs`（S92 與信件、血盟的 case 都保留）、`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`（書籤與 main 的測試入口都保留，`map-continuity` 與預設全跑仍執行 `BookmarkChecks`）。
- `engine/_integration/dev/rebuild-lock.json` 每次都先取 main 版本，再執行 `--update-lock`，未手動拼接 hash。
- `docs/REPORT.md` 與 main 相同，未編輯。

### 超出「只做同步」字面範圍的兩處修改（請審查）

兩處都只改本模組檔案，其他模組檔案維持 main 內容。

1. **`BookmarkUi.cs` 掛接點改為 `SetVisibleCore`（為了能編譯）**：main 的 `ClanUi.cs` 占用 `OnCreateControl`、`Marriage.cs` 占用 `OnHandleCreated`，合併後都會 CS0111。現在 `SetVisibleCore` 只訂閱一次 `Shown`，面板在 `Shown` 才建立（`InitBookmarkUi`）。面板內容與行為未改。
2. **`Bookmarks.cs` 的 S64 判定改為完整標頭（行為變更，為了讓 main 的測試通過）**：main 的 `CharResetChecks.cs:36` 送 `Pad([64,0x2a])`，要求不得出錯；本模組原本只看子型別 `0x2A` 就接管，標頭不符會丟 `ProtocolException`，造成 `char-reset` 與 `map-continuity` 失敗。現在只有完整標頭 `2A 80 00 02` 才算書籤清單。
   - 代價：標頭不完整或損毀的 S64/0x2A 從「拒絕」變成「當作其他子型別略過」，不會報錯，書籤狀態不變。
   - 不變：標頭之後的欄位槽、筆數、截斷、重複 ID 仍嚴格拒絕。
   - `BookmarkChecks.cs` 與 `docs/protocol/bookmarks.md` 已同步調整；新增標頭損毀與 `Pad([64,0x2A])` 不接管的檢查。
   - 另一個做法是改 `CharResetChecks.cs`，改送完整的書籤封包，可保留嚴格拒絕；因該檔不在可修改範圍且主控規定其他模組用 main 內容，本輪未採用。

既有功能不變：列表、目前位置新增 C165、按名稱刪除 C3、S64／0x2A 清單與 S92 更新，以及 `World.Bookmarks.TryGet(id)`。刪除無確認回包，保留待確認列並停用查詢，重登清單才調和；角色重設清空，S206 跨圖保留。

## 怎麼驗證（已驗證）

下表在基準 `5555e4a` 合併後的程式上執行（merge commit `9b69780`）。`6f827cb` 只再提交該次 `--update-lock` 產生的 lock，並同時通過表中的唯讀核驗。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，211 個 source／220 個 pinned inputs |
| `rebuild.py --build --configfile scratch/issue-258/NuGet.Config --output scratch/issue-258/build` | 產品、audio、launcher 建置通過，未執行遊戲 |
| `rebuild.py`（唯讀核驗） | 通過 |
| demo-tests `bookmarks` | 640 項通過 |
| demo-tests `map-continuity` | 22722 項通過 |
| demo-tests `clan`／`marriage`／`char-reset` | 312／329／354 項通過 |
| demo-tests `combat-admission`／`item-transfer` | 1326／266 項通過 |
| demo-tests `buddies`／`board`／`location-share` | 362／423／292 項通過 |
| demo-tests `player-query`／`character-config` | 894／286 項通過 |
| demo-tests `polymorph`／`polymorph-cadence` | 193／226 項通過 |
| demo-tests `ambience`／`invisibility`／`duel` | 549／263／307 項通過 |
| demo-tests `pets`／`fishing`／`doors`／`ghost-mode`／`portals` | 193／232／514／552／293 項通過 |
| `item-use-tests`／`character-management-tests`／`weapon-repair-tests`／`mail-tests` | 426／218／98／119 項通過 |
| `engine/tools/check-no-game-assets.py` | 通過，3404 tracked files、0 errors |

demo-tests 每個群組的數字都包含入口固定執行的共用檢查，所以不等於該模組自己的項數。`bookmarks` 與上輪同為 640 項是增減抵銷（放寬截斷與標頭 −4，新增標頭檢查 +4），不是測試未執行。

`6f827cb` 的 CI 全綠：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42342271883) 與 [npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42342271941) 均 completed／success；當時 PR 為 CLEAN／MERGEABLE、落後 main 0 個提交。本報告以後續純文件提交補記，最終 head 與 CI 另記於 PR 留言，避免自我引用。

`SetVisibleCore` 的時機用 `scratch/issue-258/lifecycle-probe`（空白 Form，與遊戲無關）實測：順序為 `SetVisibleCore > OnHandleCreated > OnLoad > OnCreateControl > Shown`。在建構子強制提早建立 handle 時，`OnHandleCreated` 當下面板尚未建立，而 `Shown` 時已就緒；兩種情況初始化都只執行一次，`RecreateHandle` 不會重複觸發。主視窗由 `Application.Run(form)` 顯示。

環境：Windows x64、SDK 10.0.300、Python 3.12。產品 DLL SHA-256：`f7a5f65ea074b8f3abcc3d4ec3a58b2b457ebbe63a32e1309e81ccfc690544f5`。產品有 2 個警告、0 錯誤（上輪記錄為既有 `MagicUi.cs:20` nullable 警告，本輪只核對數量）；未修改該檔。

## 未驗證什麼

- **換掛接點後，「記憶座標」分頁實際出現在真實 `ClientForm`，目前沒有任何自動測試覆蓋**：離線測試不經過 `ClientForm` 生命週期，上述只驗到空白 Form 的框架順序與靜態閱讀。需由 dot smoke 確認。
- 分頁順序現在由 `Shown` 訂閱順序決定，預期排在「好友／封鎖」之後、「血盟」之前；依賴名稱開啟分頁的 `PlayerHudServices` 不受順序影響，但畫面未看過。
- 真實伺服器的 S64/0x2A 是否一律符合完整標頭，只依本模組既有協定文件，未以真實封包驗證。若標頭不符，新判定會靜默略過，症狀是書籤清單空白而不是斷線。
- 未啟動遊戲、伺服器或 DB；未驗證原生登入／畫面、書籤新增刪除與重登持久化。
- 離線測試使用產品 DLL、合成封包、記憶體 sender 與獨立控制項；未做真實 socket EOF、網路握手或重連。這些結果不代表真機驗收。
- 未驗證技能 5／69 施放；由法師技能任務處理。

## 產出路徑

- 本輪修改：`engine/source/BookmarkUi.cs`（掛接點）、`engine/source/Bookmarks.cs`（S64 判定）、`engine/source/WorldSession.cs`、`engine/_integration/dev/bookmarks-tests/BookmarkChecks.cs`、`engine/_integration/dev/demo-tests/`、`engine/_integration/dev/rebuild-lock.json`、`docs/protocol/bookmarks.md`、本報告；其餘差異均為 main 既有內容。
- 既有功能：`engine/source/Bookmarks.cs`、`BookmarkUi.cs`、`WorldSession.cs`；測試位於 `engine/_integration/dev/bookmarks-tests/`，接上 `demo-tests/` 入口。
- 封包格式、伺服器來源 SHA-256 與技能交接：[docs/protocol/bookmarks.md](../protocol/bookmarks.md)。
- 未追蹤本機證據：`scratch/issue-258/` 下的 `build/`、`lifecycle-probe/`、`verify-all.ps1`、`verify-all.log` 與各群組 JSON。

## 給下一棒的提示

1. Claude／codex-review 審查 PR #271 本輪同步，重點是上述兩處修改，並裁定 S64 判定：接受現行的靜默略過，或改 `CharResetChecks.cs` 以保留嚴格拒絕。之後依流程交使用者合併；worktree 由調度器清理。
2. main 合併頻繁，本輪就追了四次；建議本 PR 審查通過後盡快合併。`ClientForm` 的生命週期方法已幾乎被各模組占滿（`OnLoad`、`OnCreateControl`、`OnHandleCreated`、`OnShown`、`OnActivated`、`OnVisibleChanged`，現在加上 `SetVisibleCore`），下一個模組會再撞 CS0111。建議開任務把掛接集中到單一入口。
3. `docs/protocol/char-reset.md` 的「本次基底尚無書籤 case」在本 PR 合併後會過時；屬其他模組文件，本輪未改。
4. 法師技能任務用 `World.Bookmarks.TryGet(id)` 查詢，null 表示不存在或刪除待確認。依既有協定報告，伺服器 `BookmarkService.getBookMark(pc, i)` 將技能參數拆成 X/Y；應核對 C6 消費端，不能直接把清單 ID 當傳送參數。
5. dot 集中 smoke 覆蓋「更多」→「記憶座標」（確認分頁確實存在）、登入後書籤清單確實載入（確認 S64 標頭判定）、Big5 名稱、重名／容量／不可標記地點、刪除待確認、重登清單、切角隔離與跨圖保留。
