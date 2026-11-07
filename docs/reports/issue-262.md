# Issue #262／T128：血盟

2026-11-07 第三輪，分支 `task/262`，PR #282。本輪已依主控裁定完成入盟確認與成員列表；盟徽實際查詢及 ID 對應拆至 T135（#316）。

## 同步輪（2026-11-07，合併 `origin/main` `4e62637`）

- 分支落後 main 116 個 commit。`WorldSession.cs`、`DemoChecks.csproj`、`Program.cs` 的衝突都是雙方新增，全部保留（血盟與變身、玩家查詢、角色設定、位置分享）；dev lock 取 main 版後用 `--update-lock` 重建。
- **唯一的產品程式修改**：main 的 `LocationShare.cs` 也覆寫 `ClientForm.OnShown`，與 `ClanUi.cs` 重複而編譯失敗（CS0111），這個衝突 git 看不出來。`OnLoad` 已被 `BuddyUi.cs` 使用，因此 `ClanUi.cs` 改為覆寫 `OnCreateControl`，訂閱 `Shown` 事件呼叫 `InitClanUi()`。初始化時機與原本相同，沒有動其他模組的檔案。
- 掛接行為：以 scratch 的 WinForms 小程式驗證同類別另有 `OnLoad`／`OnShown` 覆寫時，初始化只跑一次，重建 handle 也不重複。該程式跑在 Windows PowerShell 5.1（.NET Framework），不是 .NET 10，也不是實際的 `ClientForm`。
- 測試結果（`-p:UseSharedCompilation=false`，全部通過）：`clan` 209、`map-continuity` 20951、`combat-admission` 1109、`polymorph-cadence` 123、`polymorph` 90、`buddies` 259、`item-transfer` 163、`location-share` 189、`player-query` 791、`character-config` 183。各群組都含 main 新增的 90 項共用檢查（`polymorph` 群組只跑這部分），血盟本身仍是 119 項。
- `rebuild.py --build`：product／audio／launcher 通過，0 錯誤，2 個既有 `MagicUi.cs` 警告；`check-no-game-assets.py` 通過（0 錯誤、4 個核准 EmbeddedResource）。產品 DLL SHA-256：`4c6aaa60dd457fad6d54d7e64c0245e7350cce00d694cf4e275663dd02d3544a`。
- **未通過**：無群組的預設入口在本 worktree 執行失敗（`Movement123` 讀不到 `engine/assets/terrain0.bin`，exit 1），因為 worktree 沒有 `engine/assets` junction，而本任務不新建 junction。失敗點在血盟測試之前，所以預設入口內的血盟測試本輪沒有跑到。CI 不跑此入口；血盟測試由 `clan` 與 `map-continuity` 群組覆蓋。
- 下方各節保留第三輪紀錄，其中的測試數字與 DLL SHA 以本節為準。

## 做了什麼

- 前次同步曾合併 `origin/main`（`9f62553cf8e1b1f3c8d26cb0c48e0dfd551d19ba`），保留好友、公告欄、道具轉移、角色管理及其他既有分派／測試。測試專案與 Program 入口衝突保留雙方群組；dev lock 取 main 後由 `--update-lock` 重建。另併入遠端 task/262 的 main 同步提交，保留遠端歷史。
- 保留建立、申請加入、退出／解散、驅逐、成員查詢及已有伺服器盟徽顯示。S200 僅為關注血盟。
- `Trade.cs` 只加一個 message=97 轉交分支。`ClanUi` 收到邀請會開啟血盟視窗，提供接受／拒絕；C121 固定使用 d287 的 H(0)、D(0)、H(97)、C(1/0)，不使用 requestId／raw confirmation。
- 邀請以本地 revision 防止重複回答及排隊期間被新邀請取代。對方移除／離線、五分鐘逾時、拒絕、取消相關伺服器訊息、換盟、切圖、斷線或其他對話／確認操作會清除或失效；不預先修改伺服器血盟狀態。
- `WorldSession.cs` 在原有 S72／S118／S200 外，加 S167、S170 各一個 case，以及只匹配 S250/sub171 的 case。現有 S250 狀態分派在前置模組鏈內，保留其行為，不修改 MagicStatus.cs。
- S167 解析盟名、盟主、emblem ID、建立時間與 478-byte 公告；S170 解析姓名、階級、等級、62-byte 備註、角色 ID、職業；S250/sub171 解析 H 人數及逐筆 S 姓名＋C(0)。介面列出名稱、階級及上線／離線／未知，取得上線快照前不猜離線。
- 成員快照依自身血盟及 WorldEpoch 失效；完整解析成功後才替換狀態。S167 的 emblem ID 本輪只解析保留，不建立映射、不自動發 C72。

## 已驗證／怎麼驗證

- `rebuild.py --update-lock`：186 個來源、195 個鎖定輸入通過。
- `rebuild.py --build --configfile scratch/clan-nuget.config --write-receipt --output scratch/clan-build`：Windows product／audio／launcher 全部通過，0 錯誤；產品保留既有 MagicUi.cs 兩個 nullable 警告。
- 正式 `demo-tests/DemoChecks.csproj` 的 `clan` 群組 **119 項通過**；CI 使用的 `map-continuity` 群組 **477 項通過**，包含血盟及合併後既有測試。預設無群組入口也接入血盟測試。
- 測試以 `-p:UseSharedCompilation=false` 編譯，本次全部直接通過；另跑 main 新增的 `buddies` 群組 **169 項通過**，確認共用接收掛接保留。
- 測試透過實際產品 DLL 的 Receive 與記憶體 sender 執行，SocketGuard 禁止 socket；涵蓋全部本模組 C／S 格式、接受與拒絕的精確 C121 bytes、九種失效／取代情境、重複提交、錯誤／截斷封包不破壞成員快照、既有 message252 交易及 S250 狀態處理。
- `check-no-game-assets.py` 通過，0 錯誤、4 個核准 EmbeddedResource。最新 head 的 GitHub CI 結果記錄於 PR checks 及本輪 RESULT；離線測試不代表真機驗收。
- 本機 Python 使用已安裝的 Python312 絕對路徑；NuGet 從原專案 vendor 唯讀還原，快取、暫存與建置輸出皆在本 worktree。沒有複製遊戲資源。

產品 DLL SHA-256：`5fa24b9139415c29c74e9afa81661f0485cbb88391b9eafc437685a86cc1e53d`。
固定 JAR SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。第三輪實作時已唯讀核對 JAR 身分及 UiOperationContext 的五分鐘 TTL；本次 main 同步沿用該協定證據。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB；原生 UI、真實入盟往返、成員名單與實際盟徽 payload 尚待 dot 真機驗收。同步輪改過的 `ClanUi` 掛接沒有在實際 `ClientForm` 上執行，真機時請確認血盟頁籤與收到邀請時的自動開窗。
- d287 的確認封包沒有請求 token，也沒有本輪可使用的專用取消通知。客戶端收到可觀測的對象移除、對話取代／一般結果訊息時保守清除，無訊息的伺服器內部失效最遲於本地逾時清除；實際接受仍由伺服器判定。
- 盟徽查詢與 clan ID／emblem ID 映射交 T135（#316）。本輪僅顯示已存在且可對應的 S118 資料；沒有資料不顯示。BMP 支援的合成驗證不保證實際伺服器所有圖像格式均支援。
- `docs/REPORT.md` 只有合併 main 帶入既有變更，本任務沒有編輯；未改機密設定、封存 lock、releases 或硬連結，未遞迴刪除、未清理 worktree。

## 產出路徑

- `engine/source/Clan.cs`、`ClanUi.cs`；`Trade.cs`、`WorldSession.cs` 最小分派；原有 `ObjectRendering.cs` 單一繪製掛載。
- `engine/_integration/dev/clan-tests/ClanChecks.cs`、`README.md`；既有 `demo-tests/DemoChecks.csproj`、`Program.cs`。
- `engine/_integration/dev/rebuild-lock.json`、`docs/protocol/clan.md`、本報告。
- 未追蹤證據：`scratch/clan-checks.json`、`scratch/clan-map-checks.json`、`scratch/clan-buddy-checks.json`、`scratch/clan-build/`；產品在 `engine/build/client-product-dev/`。

## 給下一棒的提示

Claude 審查 PR #282 的本輪範圍與 CI，再由使用者合併；dot 在 P24Lab 以測試角色驗證雙方接受／拒絕、離線與逾時、成員查詢及有／無盟徽畫面。T135 執行器另處理查詢與 ID 對應，不要把 S167 emblem ID 或 S72 reserved 當作 S87 clan ID，也不要實作 d287 不存在的 S_Classic*。
