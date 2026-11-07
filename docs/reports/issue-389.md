# Issue #389：T149 隱身

## 做了什麼

- 新增 `engine/source/Invisibility.cs`：解析 S171/type 0、1，驗證後更新既有 S87 Flags 的隱身位元；只接受伺服器狀態，不判斷看穿隱身的能力。
- `WorldSession.cs` 僅增加一個 S171 case；`ObjectRendering.cs` 僅增加一個透明度覆寫查詢點。自身與伺服器仍提供的隱身物件以既有半透明介面繪製，S120 移除的物件隱藏。未修改 `OpacityRendering.cs` 內部。
- 沿用既有 S87／S120／S206／Reset 生命週期，避免同 ID 重用殘留；未知物件更新不建立物件。
- 測試提交於 `engine/_integration/dev/invisibility-tests/InvisibilityChecks.cs`，接上 DemoChecks 的 `invisibility`、預設入口及現有 CI 執行的 `map-continuity` 群組。
- 以 `rebuild.py --update-lock` 更新 dev lock，未更動封存 lock。

## 怎麼驗證（已驗證）

- 唯讀核對伺服器封包與發送點；格式、可見性處理、來源 SHA-256 詳見 [隱身協定](../protocol/invisibility.md)。
- .NET SDK 10.0.300：dev product、audio、launcher 建置通過；產品有既有 `MagicUi.cs:20` CS8600／CS8602 兩個警告，0 錯誤。未執行產品入口。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-389/invisibility.json invisibility`：70 項斷言通過。涵蓋 S171 兩種 type、little endian、非法格式、完整包／移動／移除／換圖／重連、其他旗標保留與既有 blitter 的 RGB555 半透明像素／點選結果；本模組沒有新增 C 格式。
- 同一入口的 `map-continuity` 群組：428 項斷言通過（含新隱身測試），確認既有 CI 入口會實際執行本模組。
- `check-no-game-assets.py`：通過，3306 個追蹤檔、0 錯誤、4 個 embedded resource 宣告；保留來源既有 CRLF，以 `git -c core.whitespace=cr-at-eol diff --cached --check` 檢查變更。
- 本機 NuGet 線上還原遇到 NU1301 TLS 錯誤，改用 scratch 中的 NuGet.Config 唯讀引用原專案 `engine/vendor`，套件快取與輸出均放目前 worktree；未複製遊戲資源。

## 未驗證什麼

- 未啟動遊戲、伺服器、DB，未做登入、真機畫面、部署 JAR 行為或技能成功率驗收。合成封包與像素僅為離線測試，不能當作真機證據。
- CI 結果以此分支 PR 的 checks 為準；PR 綠燈後交 Claude 審查、使用者合併。

## 產出路徑

- 模組：`engine/source/Invisibility.cs`
- 測試：`engine/_integration/dev/invisibility-tests/InvisibilityChecks.cs`，既有入口 `engine/_integration/dev/demo-tests/`
- 協定：`docs/protocol/invisibility.md`
- 報告：`docs/reports/issue-389.md`
- 本機未追蹤證據：`scratch/issue-389/build/`、`scratch/issue-389/invisibility.json`、`scratch/issue-389/map-continuity.json`
- 分支：`task/389`

## 給下一棒的提示

由 Claude 審查上述伺服器語義與最小掛接；dev lock 若衝突交調度器同步輪重新產生，勿手拼 hash。由 dot 在 M6 smoke 檢查本人半透明→解除、旁觀者 S120 消失→S87 重現、隱身中換圖與重連；若部署來源行為不同，先核對封包證據，不加客戶端權限推測。worktree 交調度器清理，本任務未刪除目錄或建立 junction。
