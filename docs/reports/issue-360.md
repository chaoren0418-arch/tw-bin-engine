# Issue #360／T143：幽靈模式

## 做了什麼

- 唯讀核對 d287 JAR：確認競技場 NPC `ent` → 扣 100 金幣 → `beginGhost` 觀戰入口；未找到死亡直接進入幽靈的呼叫路徑。完整欄位、方法與 SHA-256 見 [協定契約](../protocol/ghost-mode.md)。
- 新增 `GhostMode.cs`：自身 S87 `flags & 0x80` 狀態、HUD 標示與「離開幽靈」按鈕、C173 無欄位封包。保留原服 gfx／動作值，沿用既有 NPC 入場，不自行設定幽靈或預判離開成功。
- 送出前與 socket gate 內驗證 session／world／角色／ghost revision，阻擋重複與舊請求；15 秒後可手動重試，沒有自動重送。缺少可渲染的地圖或外觀時仍可離開。
- `WorldSession.cs` 只在既有 S87 自身分支加一個模組呼叫；`PlayerHudView.cs` 只加一個掛載點。測試提交於 `ghost-mode-tests`，接入 DemoChecks 預設入口與既有 Windows CI 的 `map-continuity` 群組。dev lock 由工具重新產生。

## 怎麼驗證（已驗證）

- `.NET SDK 10.0.300`；`rebuild.py --update-lock` 核驗 183 個來源／192 個鎖定輸入；`rebuild.py --build --write-receipt` 產品與 launcher 成功，沒有啟動產物。產品 0 錯誤、2 個既有 `MagicUi.cs` nullable 警告，launcher 0 警告／0 錯誤。
- 最終建置唯讀使用原專案 `engine/NuGet.Config` 及 vendor 套件；NuGet packages 與暫存均在本 worktree。nuget.org 本機 TLS 還原失敗後改用離線來源；未改原專案檔案或配置。
- `DemoChecks ... ghost-mode`：**359 項通過**。涵蓋 C173／沿用 C125、S87 全欄位與全部 flags、逐 byte 截斷、無效名稱／朝向／padding、recovery、死亡區分、其他角色隔離、非樂觀更新、防重、逾時、跨世代與延遲失敗。
- `DemoChecks ... map-continuity`：**717 項通過**，包含原有地圖／公告板與新增幽靈測試；SocketGuard 全程無網路。
- `check-no-game-assets.py` 通過；差異檢查使用 `git -c core.whitespace=cr-at-eol diff --check`，保留既有共用來源的 CRLF 原始位元組。
- 產品 DLL SHA-256：`798e4c596418420c0587c13ee497d8fc0640f4f7546f7dd2181bdc5bc7c2cae8`。
- CI 在本報告提交後由 PR 觸發，最終結果以 PR checks 與任務完成回報為準。

## 未驗證什麼

未啟動遊戲、伺服器或 DB；未驗證原生 HUD 排版、競技場 NPC／對話資料是否實際可用、觀戰入場／金幣扣款／各動作拒絕／離開回原地。合成封包只驗證產品邏輯，不冒充原生實機證據。

S87 沒有完整可行動作 mask，也沒有伺服器內部 `ghostCanTalk` 欄位；此模組不推定未傳送的權限、不新增本機幽靈特效、不把死亡一律當幽靈。實際行動仍依既有門檻與原服裁定。

## 產出路徑

- `engine/source/GhostMode.cs`，及上述兩個共用掛載點。
- `engine/_integration/dev/ghost-mode-tests/{GhostModeChecks.cs,README.md}`；DemoChecks 的編譯與執行掛接；`engine/_integration/dev/rebuild-lock.json`。
- `docs/protocol/ghost-mode.md`、`docs/reports/issue-360.md`。
- 未追蹤的本機證據：`scratch/issue-360/{ghost-mode.json,map-continuity.json,build/}`。建置 receipt 亦保留在此暫存區，不列入來源提交。

## 給下一棒的提示

Claude／調度器審查本 PR 與 CI 後交使用者合併；dev lock 若與其他分支衝突，由同步輪重新產生。dot 接 M6 smoke：透過原服觀戰 NPC 入場，確認 HUD／外觀及移動、攻擊、NPC 對話的原服裁定；點離開一次，確認回原位且 S87 清除 `0x80`，再確認普通死亡不顯示幽靈。未提供上述 NPC／UB 資料時應記為測試條件不足，不自行修改正式資料。

分支 `task/360`；未改 `docs/REPORT.md`、正式配置、封存或原始專案，沒有遞迴刪除或拆除 worktree。
