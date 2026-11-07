# #324／T138：角色設定存取（快捷列）

## 做了什麼

- 新增 `engine/source/CharacterConfig.cs`：C244 編碼、S250／41 解碼、版本化四槽資料模型、傳送保護、HUD 資料同步。
- 依本機伺服器處理器、preflight、持久化服務與回包實作，確認 N+3 長度、65,535 bytes 儲存上限、雙重補齊及終止零；限制客戶端可安全往返的 N 為 10,219 bytes。
- 現有快捷列為 F1–F4 四格，所以只接資料，不另造十格 UI，也不改動 F5–F12 或其他既有按鍵。兩個共用產品檔案各只新增一行掛載／分派。
- 舊 BIN blob 的槽布局無來源可證，保留未知資料並阻止自動覆寫；TWCC v1 設定可以送出並在登入回包中還原。傳輸成功不標示為 DB 寫入成功。
- 已提交離線測試，接到既有 DemoChecks `character-config` 群組、預設群組與 CI 會執行的 `map-continuity` 群組；沒有新增工作流程。

## 怎麼驗證（已驗證）

1. `rebuild.py --update-lock`：同步 main 後 185 個來源、194 個鎖定輸入通過；只更新 dev lock。
2. `rebuild.py --build --configfile scratch/issue-324/NuGet.Config --output scratch/issue-324/build`：產品、audio、launcher 建置成功。主產品 0 錯誤，既有 `MagicUi.cs:20` 兩個 nullable 警告；launcher 0 警告／0 錯誤。
3. `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-324/checks.json character-config`：93 項檢查通過。測試執行產品 DLL，使用合成封包及記憶體 sender；SocketGuard 禁止網路。涵蓋 C／S 實際布局、八種補齊餘數、空／未知設定、畸形長度／尾段、frame 極限、資料複本隔離、其他 S250 子碼、HUD 還原／指派／清除、取消與新設定抵達時的排隊失效。
4. `check-no-game-assets.py`：同步後通過，3,314 個追蹤檔案、0 errors、4 個 embedded resource 宣告。`git -c core.whitespace=cr-at-eol diff --cached --check` 通過（保留既有 CRLF 檔案格式）。
5. PR CI 結果於推送後核對；Windows `map-continuity` 入口會實際執行本模組測試。

本機工具用 Python 3.12 的絕對路徑呼叫（PATH 沒有 python）；NuGet 網路來源因本機 TLS 認證失敗，改用 scratch 中的設定唯讀引用主專案 `engine/vendor`，套件解壓與所有產物留在 worktree。測試編譯停用共用 compiler server，避開既有 server 的輸出路徑存取失敗。

## 合併前同步（2026-11-07）

依 Issue 調度器的同步指示，將 `origin/main`（`9f62553`）合併至既有 `task/324`，沿用 PR #352。解決 `WorldSession.cs`、DemoChecks 專案及入口、dev lock 四處衝突：保留角色設定與好友模組的個別分派和測試入口，lock 由 `--update-lock` 重新產生，未手動拼接 hash。`docs/REPORT.md` 完全沿用 main，沒有新增或改寫索引。

同步後重跑完整建置、93 項 `character-config`、451 項 `map-continuity`（含角色設定與公告欄）、115 項 `buddies-codec` 與資源檢查，全部通過。暫存證據另含 `scratch/issue-324/map-checks.json`、`scratch/issue-324/buddies-checks.json`。本輪不改已審查的角色設定功能；推送後核對新提交的 CI 與 PR 可合併狀態。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB；未驗證原生 UI 畫面、真實登入還原或資料庫持久化。
- 未推測或還原原 BIN 的不透明快捷資料格式；若測試角色已有未知設定，會顯示保留訊息。
- 未聲稱保存其他尚未有模型的角色偏好；此次範圍只涵蓋既有四格道具／技能快捷列。
- 未改 `docs/REPORT.md`、正式設定、資源、release、封存 lock；沒有遞迴刪除或清理 worktree。

## 產出路徑

- `engine/source/CharacterConfig.cs`
- `engine/_integration/dev/character-config-tests/CharacterConfigChecks.cs`
- `docs/protocol/character-config.md`
- `docs/reports/issue-324.md`
- 暫存證據：`scratch/issue-324/checks.json`、`scratch/issue-324/build/`（不提交）

## 給下一棒的提示

Claude 審查協議與未知格式保護，使用者依流程合併。M6 smoke 由已授權的 dot 在 P24Lab 使用測試角色：指派／清除 F1–F4、登出重登、確認槽位與不可用道具處理。伺服器無儲存 ACK，須以重登回包確認持久化。若需要舊 BIN 設定遷移，另開唯讀格式研究任務，取得實證後再做轉換。未來同連線同角色重入功能需要補模組 reset 掛點，詳見協議文件。
