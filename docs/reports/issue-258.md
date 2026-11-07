# Issue #258：T124 記憶座標

## 結論

本輪僅同步 `origin/main`（`ccaa3e02caf1c0cc2c0696683e243c1c7577df4e`），保留已審查的書籤功能與生命週期修正。同步提交：`89c996279fa3aa5f58496f6e18d6ae9b5d32136f`。產品建置、447 項書籤測試、805 項地圖整合檢查、73 項物品轉移檢查、278 項戰鬥入口檢查及資源檢查通過。

沿用分支 `task/258` 與 [PR #271](https://github.com/chaoren0418-arch/tw-bin-engine/pull/271)；PR 為非草稿，GitHub 已確認同步後可合併。同步提交的 build 與 npc-offline CI 全綠。此報告以後續純文件提交補記已完成的 CI，來源與 lock 與該同步提交相同。

## 做了什麼

本輪同步：處理 `WorldSession.cs`、共用測試專案及入口衝突，保留 main 的 S22／S55／S106、S68／S148 分派、物品轉移與佈告欄測試，並保留本分支 S64／S92、ResetBookmarks 及書籤測試掛接。dev lock 先取 main 版本，再用官方 `--update-lock` 產生；`docs/REPORT.md` 完整採用 main。書籤模組與測試本體未變更，其他模組沿用 main。以下功能與生命週期修正均為前輪既有內容。

等待 CI 期間 main 合併 #215，因此再次同步；共用 `Reset()` 同時保留 `ResetBookmarks()` 與 `ResetHitReactionSuppress()`，受擊反應分派及測試入口沿用 main。再次產生 lock、建置並重跑四組離線測試。

- 新增 `Bookmarks.cs`：S64／0x2A 清單原子替換、S92 單筆更新，保存 ID／名稱／地圖／XY，提供 `World.Bookmarks.TryGet(id)` 及 out overload。
- C165 只送目前位置的名稱請求；C3 依伺服器書籤名稱送出。新增等待 S92；刪除無確認回包，因此保留待確認列、停用再次刪除及技能查詢，重新登入清單才調和結果。
- 新增 `BookmarkUi.cs`：玩家「更多」→「記憶座標」分頁，列表、座標明細、新增及刪除；利用本檔 `ClientForm.OnLoad` 掛接，不改 `MagicUi.cs` 或共用 HUD。
- 送出前重驗 session／世界 epoch／清單 revision／角色／位置／動作狀態；等待 sender 時拒絕重複點擊。
- 新測試放 `engine/_integration/dev/bookmarks-tests/`，接上 demo-tests 的專用 `bookmarks`、預設及既有 CI `map-continuity` 入口。
- 前輪依審查修正要求，在既有 `WorldSession.Reset()` 增加唯一一個 `ResetBookmarks();` 呼叫；不重排分派或改其他模組。`Cancel()`、傳輸失敗、角色選取沿用既有 Reset 路徑；S206 跨圖不清除書籤。
- 補實際模型的 reset／取消斷線／切回離線／傳輸失敗／同角色重登／切角回歸，驗證普通項目與待刪狀態清空、舊 ID 的兩種查詢失效、容量與提示重設、revision 更新及舊操作失效。跨圖保留兩類項目；UI 測試改取實際取消後模型快照。
- 依官方 dev 腳本更新滾動 lock；未改封存 lock、`docs/REPORT.md`、原專案或遊戲資源。

## 怎麼驗證（已驗證）

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，181 個 source／190 個 pinned inputs |
| `rebuild.py --build --configfile scratch/issue-258/NuGet.Config --output scratch/issue-258/build` | 產品、audio、launcher 編譯通過；未執行遊戲 |
| demo-tests `bookmarks` | 447 項通過，合成封包／記憶體 sender／獨立控制項 |
| demo-tests `map-continuity` | 805 項通過，包含書籤 447 項與 main 的佈告欄 230 項測試 |
| demo-tests `item-transfer` | 73 項通過，保留 main 測試入口 |
| demo-tests `combat-admission` | 278 項通過，包含 main 的受擊反應抑制 66 項測試 |
| `engine/tools/check-no-game-assets.py` | 通過，3290 tracked files、0 errors |
| `git -c core.whitespace=cr-at-eol diff --check` | 通過；保留既有 CRLF |
| GitHub CI（`89c996279fa3aa5f58496f6e18d6ae9b5d32136f`） | [build #465](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42320466767) 與 [npc-offline #334](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42320466840) 均為 completed／success；build 包含 dotnet 與 map-continuity |

環境：Windows x64、SDK 10.0.300、Python 3.12.10。PATH 無可用 `python`，實際使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`。沿用前輪因 NuGet 線上還原 TLS 認證錯誤建立的 scratch 設定，唯讀引用原專案 `engine/vendor`；套件快取與暫存均落在本 worktree。

產品 DLL SHA-256：`54bd06a5aefa09d2cfd9aa60d7505963cd2c0af0b5521ccdac44964728fa31e1`。

產品建置有兩個既有 `MagicUi.cs:20` nullable 警告（CS8600／CS8602），沒有編譯錯誤；該檔未修改。

## 未驗證／未完成

- 未啟動遊戲、伺服器或 DB；未做登入後顯示、實機新增／刪除、重新登入持久化驗收。
- 斷線回歸以記憶體 sender 拋出 IOException 走實際取消／Faulted 路徑，另直接覆蓋共用 Reset；未開 socket 或實測遠端 EOF。重登／切角使用產品公開選角入口與合成清單，未做網路握手。
- 未驗證技能 5／69 施放；此張不負責技能串接。
- Git 以單次 `http.sslBackend=openssl` 操作；GitHub 狀態與 PR 描述透過 repo 擁有者的已連接帳號核對／更新。

## 產出路徑

- `engine/source/Bookmarks.cs`、`engine/source/BookmarkUi.cs`
- `engine/source/WorldSession.cs`：S92 case、S64 書籤子型別分派與前輪最小 reset 掛接
- `engine/_integration/dev/bookmarks-tests/BookmarkChecks.cs`、`README.md`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`Program.cs`：測試入口
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/bookmarks.md`：格式、伺服器來源 SHA-256、刪除語意及技能交接
- `scratch/issue-258/bookmarks.json`、`map-continuity.json`、`item-transfer.json`、`combat-admission.json`、`build/`：本機證據，未納入 git

## 給下一棒的提示

1. Claude／codex-review 審查本輪同步結果與 CI，依專案流程交使用者合併 PR #271；本輪未新增功能或擴大修正範圍。
2. 法師技能任務用 `World.Bookmarks.TryGet(id)` 取得資料；回傳 null 表示不存在或刪除待確認。唯讀查閱發現 `BookmarkService.getBookMark(pc, i)` 將技能參數拆成 X/Y；請核對實際 C6 消費端，不要直接把清單資料 ID 當傳送參數。
3. dot 在集中 smoke 驗證「更多」→「記憶座標」的 Big5 名稱、新增不可標記地點／重名／容量、刪除待確認、重登清單、切角隔離與跨圖保留。
