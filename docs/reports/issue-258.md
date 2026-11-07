# Issue #258：T124 記憶座標

## 做了什麼

本輪為同步輪，不新增功能。`task/258`（同步前 `1f2ed29aac29bc8fc6f57d3179f437181e1f6a92`）合併 `origin/main`，同步期間 main 持續前進，共合併四次（`cb74af4`、`82d3a45`、`20b4a0a`、`76ac0fb`）。程式與 lock 的同步 head 為 `3f4c7cc04e50f7df5e840345c3d57bcd7cd3471d`，沿用 [PR #271](https://github.com/chaoren0418-arch/tw-bin-engine/pull/271)，未強制推送。

- 衝突僅在共用分派點與 lock：`WorldSession.cs`（`Reset()` 同時保留 `ResetBookmarks()` 與 `ResetBuddies()`；S92 與 S76、武器修理的 `case 83 when` 都保留）、`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`（書籤與 main 的測試入口都保留，`map-continuity` 與預設全跑仍執行 `BookmarkChecks`）。
- `engine/_integration/dev/rebuild-lock.json` 每次都先取 main 版本，再執行 `--update-lock`，未手動拼接 hash。
- `docs/REPORT.md` 與 main 相同，未編輯。
- **同步輪唯一的程式修改（超出「只做同步」字面範圍，為了能編譯）**：main 的 `BuddyUi.cs` 也覆寫 `ClientForm.OnLoad`，合併後出現 CS0111。依「其他模組用 main 內容」不動 `BuddyUi.cs`，改把本模組 `BookmarkUi.cs` 的掛接點換成 `OnCreateControl`。`OnShown`、`OnActivated` 已分別被 `LocationShare.cs`、`PetUi.cs` 使用。面板內容與行為未改。
- 既有功能不變：列表、目前位置新增 C165、按名稱刪除 C3、S64／0x2A 清單與 S92 更新，以及 `World.Bookmarks.TryGet(id)`。刪除無確認回包，保留待確認列並停用查詢，重登清單才調和；角色重設清空，S206 跨圖保留。

## 怎麼驗證（已驗證）

下表皆在最後一次合併後、`3f4c7cc` 的內容上執行。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，201 個 source／210 個 pinned inputs |
| `rebuild.py --build --configfile scratch/issue-258/NuGet.Config --output scratch/issue-258/build` | 產品、audio、launcher 建置通過，未執行遊戲 |
| `rebuild.py`（唯讀核驗） | 通過 |
| demo-tests `bookmarks` | 640 項通過 |
| demo-tests `map-continuity` | 21847 項通過 |
| demo-tests `combat-admission`／`item-transfer` | 1326／266 項通過 |
| demo-tests `buddies`／`board`／`location-share` | 362／423／292 項通過 |
| demo-tests `player-query`／`character-config` | 894／286 項通過 |
| demo-tests `polymorph`／`polymorph-cadence` | 193／226 項通過 |
| demo-tests `ambience`／`invisibility`／`duel` | 549／263／307 項通過 |
| demo-tests `pets`／`fishing` | 193／232 項通過 |
| `item-use-tests`／`character-management-tests`／`weapon-repair-tests` | 426／218／98 項通過 |
| `engine/tools/check-no-game-assets.py` | 通過，3373 tracked files、0 errors |

demo-tests 每個群組的數字都包含入口固定執行的共用檢查，所以不等於該模組自己的項數。

`3f4c7cc` 的 CI 全綠：[build #595](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42338138269)（dotnet／map-continuity）與 [npc-offline #464](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42338138279) 均 completed／success；當時 PR 為 CLEAN／MERGEABLE、落後 main 0 個提交。本報告以後續純文件提交補記，最終 head 與 CI 另記於 PR 留言，避免自我引用。

`OnCreateControl` 的時機用 `scratch/issue-258/lifecycle-probe`（空白 Form，與遊戲無關）實測，順序為 `OnLoad > OnCreateControl > OnShown`；`RecreateHandle` 會再觸發一次 `OnCreateControl`，既有的 `bookmarkPanel!=null` 判斷會擋掉重複初始化。`normalPanels` 與 `timer` 由建構子完成，觸發時已就緒。

環境：Windows x64、SDK 10.0.300、Python 3.12。產品 DLL SHA-256：`822d3adb2b5023551207d0e487da820d5fe984b14540ad51b474e7e301cef893`。產品有兩個既有 `MagicUi.cs:20` nullable 警告（CS8600／CS8602）、0 錯誤；未修改該檔。

## 未驗證什麼

- **換掛接點後，「記憶座標」分頁實際出現在真實 `ClientForm`，目前沒有任何自動測試覆蓋**：離線測試不經過 `ClientForm` 生命週期，上述只驗到空白 Form 的框架順序與靜態閱讀。需由 dot smoke 確認。
- 分頁順序現在排在「好友／封鎖」之後；依賴名稱開啟分頁的 `PlayerHudServices` 不受順序影響，但畫面未看過。
- 未啟動遊戲、伺服器或 DB；未驗證原生登入／畫面、書籤新增刪除與重登持久化。
- 離線測試使用產品 DLL、合成封包、記憶體 sender 與獨立控制項；未做真實 socket EOF、網路握手或重連。這些結果不代表真機驗收。
- 未驗證技能 5／69 施放；由法師技能任務處理。

## 產出路徑

- 本輪同步與驗證：`engine/source/BookmarkUi.cs`（掛接點）、`engine/source/WorldSession.cs`、`engine/_integration/dev/demo-tests/`、`engine/_integration/dev/rebuild-lock.json`、本報告；其餘差異均為 main 既有內容。
- 既有功能：`engine/source/Bookmarks.cs`、`BookmarkUi.cs`、`WorldSession.cs`；測試位於 `engine/_integration/dev/bookmarks-tests/`，接上 `demo-tests/` 入口。
- 封包格式、伺服器來源 SHA-256 與技能交接：[docs/protocol/bookmarks.md](../protocol/bookmarks.md)。
- 未追蹤本機證據：`scratch/issue-258/` 下的 `build/`、`lifecycle-probe/`、`verify-all.ps1` 與各群組 JSON。

## 給下一棒的提示

1. Claude／codex-review 審查 PR #271 本輪同步（重點：`BookmarkUi.cs` 的 `OnCreateControl`）與 CI，再依流程交使用者合併；worktree 由調度器清理。
2. main 合併頻繁，本輪就追了四次；建議本 PR 審查通過後盡快合併。之後再有模組覆寫 `ClientForm` 的生命週期方法會再撞 CS0111，新模組請先查已被占用的 `On*`，或把掛接集中到單一入口。
3. 法師技能任務用 `World.Bookmarks.TryGet(id)` 查詢，null 表示不存在或刪除待確認。依既有協定報告，伺服器 `BookmarkService.getBookMark(pc, i)` 將技能參數拆成 X/Y；應核對 C6 消費端，不能直接把清單 ID 當傳送參數。
4. dot 集中 smoke 覆蓋「更多」→「記憶座標」（確認分頁確實存在）、Big5 名稱、重名／容量／不可標記地點、刪除待確認、重登清單、切角隔離與跨圖保留。
