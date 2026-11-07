# Issue #357／T140 執行報告

## 做了什麼

完成可核定的 `/who 名字`／`/who`，送出 C206；S71／166 玩家資訊及 S39／y_who 九欄資訊顯示於聊天區。S161 文字回覆沿用現有資訊顯示。共享送出點僅改一行，WorldSession 僅新增 S39／S71 各一分支；新增模組、測試與 dev lock，未修改 docs/REPORT.md。

**本任務為部分完成，C99／排行榜仍待協定補正。** 唯讀核對來源與 d287 JAR 後確認：C99 的 D 是防外掛 checkType，沒有玩家資訊回包；C63 subtype 1 的 `C C S` 尾段會修改血盟階級，其他 subtype 也不是排行榜。`/check`、`/rank` 因此顯示限制且不送封包。完整差異、條件分支與 SHA-256 見 [協定](../protocol/player-query.md)。

## 怎麼驗證（已驗證）

- .NET SDK 10.0.300；先 `rebuild.py --update-lock`，再 `--build --configfile scratch/issue-357/NuGet.Config --output scratch/issue-357/build`：產品與 launcher 通過；183 個來源檔、192 個鎖定檔。產品 2 個既有 MagicUi.cs nullable 警告、0 錯誤；launcher 0 警告、0 錯誤。
- 玩家查詢離線測試：283 項通過；包含格式、截斷、padding、實際分派、NPC 隔離、錯誤身份、送出世代與節流、C99／C63 零送出。
- 接上既有 DemoChecks 的 `player-query`、預設全套與 CI `map-continuity` 入口；本機 `map-continuity` 共 641 項通過。
- 既有 PlayerChatChecks：107 項通過。
- `check-no-game-assets.py` 通過；提交前再檢查新增追蹤檔。WorldSession 原本使用 CRLF，使用 `git -c core.whitespace=cr-at-eol diff --check` 檢查。
- CI 狀態及 PR 連結以 PR checks／任務 RESULT 為準。

本機 NuGet 網路還原遇到 TLS 錯誤，改為 scratch 內臨時 NuGet.Config 唯讀引用原專案 `engine/vendor`；套件快取設為 worktree `engine/.nuget/packages`。Python launcher 找不到註冊版本，使用已安裝的 Python312/python.exe。離線測試加 `UseSharedCompilation=false` 避免共用編譯程序的寫入權限問題。以上未改產品設定或 CI。

產品 DLL SHA-256：`22ec2bab8d0d7f2245dd7b9d3dde8f3a828732c6df368ccbbe66cb8fbb445b1a`。

## 未驗證什麼

未啟動遊戲、伺服器、DB；未讀機密設定、查詢或改 DB；未動原專案、封存、硬連結或 junction。未驗證登入、實際畫面、伺服器設定分支、線上人數或排名真實值。合成封包僅驗證產品程式行為，不代表真機通過。未取得可用的物件資訊／排行榜協定，因此未實作 C99／C63 查詢送出。

## 產出路徑

- `engine/source/PlayerQuery.cs`、PlayerChat／WorldSession 最小掛接。
- `engine/_integration/dev/player-query-tests/`、demo-tests 入口、dev `rebuild-lock.json`。
- `docs/protocol/player-query.md`、`docs/reports/issue-357.md`。
- 本機暫存證據：`scratch/issue-357/build/`、`player-query.json`、`map-continuity.json`、`player-chat.json`（不入 git）。

## 給下一棒的提示

Claude／調度器審查 PR，先裁定 T140 的 C99／排行榜規格落差，提供 d287 可用入口與回包後另派 code 任務，不以本 PR 直接認定 T140 全部完成。合併由使用者處理。dot 後續 P24Lab smoke 核對 `/who` 空名、在線名字、查無名字、九欄顯示與一般聊天；不得將查無名字的線上資訊回覆誤判為離線通知。
