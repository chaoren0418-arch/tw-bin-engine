# Issue #258：T124 記憶座標

## 做了什麼

本輪僅同步，不新增功能。在既有 `task/258`（同步前 `7ca5c6caca9bb5317cf97fe865a0a6fb903baa86`）fetch 並 merge `origin/main` 的 `5fece0032aef4ed8b772671f4a425c0f1d4044e8`。同步提交為 `b2df607b987e5525fd01ddea3116174560d2587d`，沿用 [PR #271](https://github.com/chaoren0418-arch/tw-bin-engine/pull/271)。

- 唯一衝突是 `engine/_integration/dev/rebuild-lock.json`；先取 main 版本，再執行官方 `--update-lock`，未手動拼接 hash。
- 書籤模組、離線測試與掛接、`WorldSession.cs`、`MagicUi.cs` 均與同步前相同。S64／S92 與 `ResetBookmarks()` 保留，既有其他分派及 reset 呼叫保留。
- main 的通用道具使用模組與測試完整納入。`docs/REPORT.md` 與同步的 main 完全一致，未另行編輯。
- 既有功能包含列表、目前位置新增 C165、按名稱刪除 C3、S64／0x2A 清單與 S92 更新，以及 `World.Bookmarks.TryGet(id)`。刪除無確認回包，保留待確認列並停用查詢，重登清單才調和；角色重設清空，S206 跨圖保留。

## 怎麼驗證（已驗證）

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，182 個 source／191 個 pinned inputs |
| `rebuild.py --build --configfile scratch/issue-258/NuGet.Config --output scratch/issue-258/build` | 產品、audio、launcher 建置通過，未執行遊戲 |
| demo-tests `bookmarks` | 447 項通過 |
| demo-tests `map-continuity` | 805 項通過，包含書籤與 main 的佈告欄測試 |
| demo-tests `item-transfer` | 73 項通過 |
| demo-tests `combat-admission` | 278 項通過，包含受擊反應抑制測試 |
| main 的 `item-use-tests/ItemUseChecks.csproj` | 426 項通過 |
| `engine/tools/check-no-game-assets.py` | 通過，3297 tracked files、0 errors |
| `git -c core.whitespace=cr-at-eol diff --cached --check` | 通過，保留既有 CRLF |

同步 head `b2df607b987e5525fd01ddea3116174560d2587d` 的 CI 全綠：[build #485](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42322545445)（dotnet／map-continuity）與 [npc-offline #354](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42322545442) 均 completed／success。此報告以後續純文件提交補記已完成的 CI，程式與 lock 與同步 head 相同；文件提交的最終 head 與 CI 另於 PR 描述記錄，避免自我引用。

環境：Windows x64、SDK 10.0.300、Python 3.12.10；Python 實際路徑為 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`。沿用 scratch 的 NuGet 設定，唯讀引用原專案 `engine/vendor`；套件快取、dotnet 與暫存均寫在此 worktree。Git 僅更新此 worktree 所需的共用中繼資料。

產品 DLL SHA-256：`a16f40ffd8f5be8fdaae3746c7209bcd5c9c6ff4a42394794b0177dc9ca415ab`。產品有兩個既有 `MagicUi.cs:20` nullable 警告（CS8600／CS8602）、0 錯誤；未修改該檔。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB；未驗證原生登入／畫面、書籤新增刪除與重登持久化。
- 離線測試使用產品 DLL、合成封包、記憶體 sender 與獨立控制項；未做真實 socket EOF、網路握手或重連。這些結果不代表真機驗收。
- 未驗證技能 5／69 施放；由法師技能任務處理。

## 產出路徑

- 本輪同步與驗證：`engine/_integration/dev/rebuild-lock.json`、本報告；其餘差異均為 main 既有內容。
- 既有功能：`engine/source/Bookmarks.cs`、`BookmarkUi.cs`、`WorldSession.cs`；測試位於 `engine/_integration/dev/bookmarks-tests/`，接上 `demo-tests/` 入口。
- 封包格式、伺服器來源 SHA-256 與技能交接：[docs/protocol/bookmarks.md](../protocol/bookmarks.md)。
- 未追蹤本機證據：`scratch/issue-258/build/`、`bookmarks.json`、`map-continuity.json`、`item-transfer.json`、`combat-admission.json`、`item-use.json`（JSON 均位於 `scratch/issue-258/`）。

## 給下一棒的提示

1. Claude／codex-review 審查 PR #271 本輪同步與 CI，再依流程交使用者合併；worktree 由調度器清理。
2. 法師技能任務用 `World.Bookmarks.TryGet(id)` 查詢，null 表示不存在或刪除待確認。依既有協定報告，伺服器 `BookmarkService.getBookMark(pc, i)` 將技能參數拆成 X/Y；應核對 C6 消費端，不能直接把清單 ID 當傳送參數。
3. dot 集中 smoke 覆蓋「更多」→「記憶座標」、Big5 名稱、重名／容量／不可標記地點、刪除待確認、重登清單、切角隔離與跨圖保留。
