# Issue #260／T126：好友與封鎖名單

## 結論

**第 2 次嘗試仍未完成驗收，PR #275 請勿合併。** 四種 C 封包、S39 buddy 與 S250/17–19 解碼器、清單模型、傳送入口及原生服務 UI 已實作。既有檔案依原任務限制只增加 S39 分支與聊天封鎖查詢點；**S250／S71 觀察及角色重置尚未掛入，封鎖功能目前不能交付使用**。GitHub 的 PR 狀態已非 draft，但不代表驗收通過。

阻塞來自本 Issue 的明確範圍：「`WorldSession.cs` 在 S39 既有分派只加一個 S_Buddy 分支」。實際 S39 的 NPC 解碼在 `NpcInteraction.cs`，本次已在 `WorldSession` 的接收鏈前截取 buddy，沒有修改 NPC 檔。封鎖回覆實際是 S250，還必須在角色重置時清空名單。已在對話提出範圍確認，尚未取得答覆，未擅自擴張。

## 第 2 次嘗試的核對與補件（2026-11-07）

- 已讀根目錄 `AGENTS.md`、`docs/REPORT.md`、M6 契約及協定／規則參考，維持 `task/260`，沒有切換或建立分支。
- 查得前次 [build run 42310617554](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42310617554)：Linux `dotnet` 成功；Windows `map-continuity` 失敗步驟是 `Buddy and exclude contracts (no game or network)`。完整日誌下載 API 回 403，故以工作／步驟狀態與本機重現核對，不宣稱已讀完整 CI 日誌。
- 本機完整 `buddies` 測試重現 `S250/17 snapshot and case-insensitive lookup` 失敗。未移除、略過或改弱該斷言。
- 新增 S250 三種回包的中文解碼、空快照、16 人邊界及去重測試；`buddies-codec` 現為 **115 項通過**。
- 增補完整測試的錯誤快照原子性、S71/109 保留原訊息且不能解除封鎖等待、錯誤 S71/472 不能解除等待、快照解除等待；斷線測試先建立非空好友與封鎖清單。這些後段斷言因前面的實際路由失敗，**尚未執行到，不宣稱通過**。
- 重跑 `--update-lock`（lock 無差異）與 canonical `--build --write-receipt` 成功；地圖回歸 **128 項**、戰鬥 admission 回歸 **212 項**、`check-no-game-assets.py`（3251 tracked files、0 errors）及 `git diff --check` 通過。
- 本輪已再次提出文末兩處具體掛接的範圍確認，尚未收到答覆；不能將「要求 CI 綠燈」自行當作取消同一份任務明列的修改限制。

## 做了什麼

- `Buddies.cs`：C4／207／202／171 編碼；S39 的 `buddy`、flag=2、count=2 與兩個名稱字串；S250/17 快照、18 加入、19 移除；Big5、完整封包長度、owner 與名單數量驗證。
- 好友新增／刪除不樂觀更新、不自動立即查詢；提示稍後手動查詢。封鎖切換等待對應回包，避免重送變成反向操作。
- 送出採既有記憶體可測的 sender gate，驗證連線世代、世界 epoch、角色 ID、清單 revision，避免舊 UI 操作跨角色／場景送出。
- `BuddyUi.cs`：原生「好友／封鎖」服務頁，透過新檔內的 WinForms `OnLoad` 掛接；查詢、增刪好友、封鎖／解除與在線狀態。不自動送網路封包。
- `PlayerChat.cs` 增加一個封鎖查詢點；一般聊天比對來源名稱、密語比對獨立發送者欄位，不過濾本文子字串或送出密語回顯。此版伺服器已過濾一般聊天及密語，本機僅額外防護。
- 合成測試放在 `engine/_integration/dev/buddies-tests/`，接入既有 DemoChecks 的 `buddies`／`buddies-codec` 入口與 Windows CI。完整測試刻意保留實際路由斷言，不把未接通的功能當作成功。
- 協定與唯讀來源 SHA-256：`docs/protocol/buddies.md`。

## 已驗證

- .NET SDK 10.0.300；先執行 `rebuild.py --update-lock`，再執行 canonical `--build --write-receipt`：product、audio、launcher 建置成功。產品有既存 `MagicUi.cs` 的兩個 nullable 警告，本模組沒有新增編譯警告。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-260/buddies-codec.json buddies-codec`：**115 項通過**，涵蓋四種 C 格式、Big5、空／重複名單、S39／S250 回覆格式、錯誤 header、數量、截斷、尾端多餘 bytes、錯誤輸入。
- 完整 `buddies` 入口已執行：S39 路由與 owner 防護通過，**停在 `S250/17 snapshot and case-insensitive lookup`**，符合尚未允許掛入 S250 的已知限制；不宣稱完整模組綠燈。
- `check-no-game-assets.py`：pass，0 errors，4 個 EmbeddedResource declarations。
- 沒有啟動遊戲、伺服器、DB 或連正式服務；沒有複製遊戲資源／伺服器程式，沒有修改封存、硬連結、原始專案或 `docs/REPORT.md`，沒有遞迴刪除。

本機 NuGet 線上來源遇到 TLS 認證錯誤，改用 `scratch/issue-260/NuGet.Config` 唯讀引用原專案 `engine/vendor` 的既有 WebView2 套件；快取、TEMP、DOTNET_CLI_HOME 及所有建置輸出都位於此 worktree。未修改 repository 的 NuGet 設定。

## 尚未驗證／尚未完成

- S250／S71 實際路由及 Reset 掛接，因此完整模組離線測試與 CI 綠燈尚未達成。
- 原生 UI 顯示、實際登入、好友資料持久化、在線變化、封鎖聊天雙角色驗收；本任務禁止啟動遊戲，留給獲授權的 dot smoke。
- S250/17 格式有伺服器建構子依據，但未宣稱每次登入必定收到完整封鎖快照。

## 產出路徑

- 程式：`engine/source/Buddies.cs`、`BuddyUi.cs`；既有 `WorldSession.cs`、`PlayerChat.cs` 各一行掛接。
- 測試：`engine/_integration/dev/buddies-tests/BuddyChecks.cs`；DemoChecks 專案及入口；`.github/workflows/build.yml`。
- 鎖定：`engine/_integration/dev/rebuild-lock.json`（工具重新產生，沒有手拼 hash）。
- 協定：`docs/protocol/buddies.md`；本報告：`docs/reports/issue-260.md`。
- 本機證據（不提交）：`scratch/issue-260/build/`、`scratch/issue-260/buddies-codec.json`。

## 給下一棒的提示

請主控／使用者確認以下**僅兩處**額外掛接是否允許；不必修改 `NpcInteraction.cs` 或額外 UI 檔案：

1. `WorldSession.Reset()` 加 `ResetBuddies()`，避免同一連線切角／重連殘留。
2. 將本次新增的 `if(opcode==39&&ReceiveBuddies(opcode,plain))return;` 改為 `if(ReceiveBuddies(opcode,plain))return;`。模組只消費 S39 buddy 與 S250/17–19；S71/109、472 只觀察，仍回 false 交既有訊息分派。

取得範圍確認後，由 codex-local 套入兩處、更新 dev lock、重新建置，執行完整 `buddies` 測試並修正其餘實際失敗，再確認最新 PR head 的 CI 綠燈，更新本報告及同一 PR #275。目前 PR 不得作為 T126 完成證據；請主控先明確修訂允許範圍，避免第 3 次派工仍帶相同限制。
