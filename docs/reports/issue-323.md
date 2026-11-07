# Issue #323／T137：稱號與表情動作

## 做了什麼

- 新增 C90 稱號請求、S183 自身／其他物件稱號更新；在角色名稱上方畫出稱號。保留既有 S87 稱號欄位與伺服器權限判定。
- 新增 C120 四種表情選單與 S158/action66–69；原圖從外部客戶端唯讀載入，保留方向、幀時序、深度排序及像素 owner。自行送出只預測自身表情，名稱／稱號不預測。
- 玩家「更多」服務視窗新增「稱號／表情」頁：角色名、稱號輸入、表情選單、送出結果。舊世界請求、連點、截斷資料與傳送失敗均有防護。
- 產品只改 `TitleAndEmote.cs`、`WorldSession.cs` 兩個最小分派 case，以及 `ObjectRendering.cs` 一個完成合成掛載點。`PlayerChat.cs`、`docs/REPORT.md`、封存、原專案均未修改；沒有啟停遊戲、伺服器或 DB。

## 怎麼驗證（已驗證）

- 第 2 次嘗試（2026-11-07）：在既有 `task/323` 重跑以下驗證；產品來源未改動，故沿用已重產且核驗通過的 dev lock。沒有建立／切換分支，沒有合併 main、清理 worktree 或遞迴刪除。
- `rebuild.py --update-lock`：181 個來源、190 個 pin；其後 `--build` 產品與 launcher 通過。主程式 0 error；`MagicUi.cs:20` 原有 CS8600／CS8602 兩個 warning。
- 本輪 `rebuild.py --build --configfile scratch/issue-323/NuGet.Config --output scratch/issue-323/build` 通過，181 個來源／190 個 pin 核驗成功；增量建置輸出為 0 warning／0 error，產品 DLL 與前輪相同。
- 產品 DLL SHA-256：`8ef96520cd19f980c6836c847cdca9d00609d14563a89b5faa898c8a29ddba00`。
- `title-emote`：416 項通過，涵蓋每個 C／S 格式、CP950、容量邊界、錯誤與截斷原子性、未知物件、S158 商店原分派、移動／死亡／換圖／移除重建、sender admission、失敗斷線、SPR 合成解碼、字型標籤像素與選取 owner。
- `title-emote-assets`：160 項通過；四動作八方向 gfx0，加上現有另外 12 個角色 gfx 的方向 0 四動作。查驗原索引及各需要的封裝 SHA，素材只讀。
- `title-emote-compose`：13 項通過；實際原圖自身四動作替換／像素輸出，以及其他角色的表情 visual 與可選取 owner。只測函式，沒有啟動遊戲。
- 測試已提交於 `engine/_integration/dev/title-emote-tests/`，接入 DemoChecks 預設入口及現有 CI 的 `combat-admission` 群組。
- `check-no-game-assets.py`、`git -c core.whitespace=cr-at-eol diff --check` 通過。
- 本輪三組測試重新得到 416／160／13 項通過；素材檢查為 3294 個追蹤檔、0 錯誤、4 個核准的 embedded resource 宣告。測試建置需加 `-p:UseSharedCompilation=false`，避免既有編譯服務對 worktree 輸出檔的存取拒絕。

本機線上 NuGet 遇到 TLS 認證錯誤，改用 `scratch/issue-323/NuGet.Config` 唯讀指向原專案 `engine/vendor`；所有還原快取、暫存、輸出均在 worktree。GitHub CLI 的既有 token 無效，改用已連接的 GitHub 帳號建立 PR／查 CI；Git 傳輸使用單次 `http.sslBackend=openssl` 且保留憑證驗證。

## 未驗證什麼／限制

**同步輪（第 3 輪，2026-11-07）**：已依調度器指示 merge `origin/main`（落後 99 個 commit），前述同步阻塞解除。解衝突只做聯集：`DemoChecks.csproj` 保留 title-emote 與 main 的 character-config／polymorph／buddies 引用；`Program.cs` 預設入口保留 `CharacterConfigChecks` 與 `TitleAndEmoteChecks`；`WorldSession.cs` 保留 S183／S158 與 main 的 S250／S71 分派；`rebuild-lock.json` 以 main 為底跑 `--update-lock` 重產，未手動拼 hash；`docs/REPORT.md` 與 main 相同。未改其他程式內容。

同步後本機結果：`--update-lock`、`--build`、`--verify` 通過（188 個來源／197 個 pin），產品 DLL SHA-256 `1e28aaa89676d163cfc60bc63ea768a420da4684e1dda2511cbb92a00fcb0da1`；DemoChecks 建置 0 warning／0 error。群組全過：`title-emote` 506、`title-emote-assets` 250、`title-emote-compose` 103、`combat-admission` 784、`map-continuity` 20032、`polymorph-cadence` 123、`buddies` 259、`item-transfer` 163；`character-management` 218；`check-no-game-assets.py` 通過（3328 個追蹤檔、0 錯誤）。title-emote 三組各比前輪多 90 項，是 main 在 `Main` 開頭加入、每個群組都會跑的 `PolymorphChecks()`，本模組測試本身未變。

**未通過／未驗證**：DemoChecks 無群組的預設全量入口在本 worktree 退出 1，停在 `Movement123` 讀 `engine/assets/terrain0.bin`（worktree 沒有 assets junction），不是斷言失敗；因此 `Movement123` 之後的預設項目本輪沒有跑。改指向原專案根目錄會讓測試寫入其 `scratch/`，違反「原專案只讀」，故未這樣做。CI 不跑此入口。CI 結果以 PR #356 為準，本機通過不能替代 CI 綠燈。

未做真機登入、角色權限與拒絕訊息、雙角色視野廣播、實際 UI 操作或音效驗收。未窮舉所有變身外觀；原圖缺件會顯示提示，沒有替代假圖。第一次使用封裝同步 SHA 核驗可能造成載入停頓；後續使用記憶體快取。表情音效與缺少的原生陰影不在此任務範圍。離線合成不是原生遊戲畫面驗收。

## 產出路徑／給下一棒的提示

- 產品：`engine/source/TitleAndEmote.cs`；最小掛接：`WorldSession.cs`、`ObjectRendering.cs`；dev lock 已由腳本重產。
- 契約：[title-emote.md](../protocol/title-emote.md)；測試：[README](../../engine/_integration/dev/title-emote-tests/README.md)。本機結果與建置紀錄：`scratch/issue-323/`。
- 分支 `task/323`，PR #356。同步已完成，下一棒：調度器確認兩個 CI job 綠燈後依閘門合併；若 main 再前進，重新同步並以 `--update-lock` 重產 dev lock。
- dot 於 M6 smoke：開「更多 → 稱號／表情」，分別核對有權／無權稱號、自身與其他測試角色名稱上方的稱號更新、四種表情、移動／換圖中止，以及首次原圖核驗停頓。依既有 P24Lab 授權執行，不連正式服務。
