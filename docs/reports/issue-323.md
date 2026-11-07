# Issue #323／T137：稱號與表情動作

## 做了什麼

- 新增 C90 稱號請求、S183 自身／其他物件稱號更新；在角色名稱上方畫出稱號。保留既有 S87 稱號欄位與伺服器權限判定。
- 新增 C120 四種表情選單與 S158/action66–69；原圖從外部客戶端唯讀載入，保留方向、幀時序、深度排序及像素 owner。自行送出只預測自身表情，名稱／稱號不預測。
- 玩家「更多」服務視窗新增「稱號／表情」頁：角色名、稱號輸入、表情選單、送出結果。舊世界請求、連點、截斷資料與傳送失敗均有防護。
- 產品只改 `TitleAndEmote.cs`、`WorldSession.cs` 兩個最小分派 case，以及 `ObjectRendering.cs` 一個完成合成掛載點。`PlayerChat.cs`、`docs/REPORT.md`、封存、原專案均未修改；沒有啟停遊戲、伺服器或 DB。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock`：181 個來源、190 個 pin；其後 `--build` 產品與 launcher 通過。主程式 0 error；`MagicUi.cs:20` 原有 CS8600／CS8602 兩個 warning。
- 產品 DLL SHA-256：`8ef96520cd19f980c6836c847cdca9d00609d14563a89b5faa898c8a29ddba00`。
- `title-emote`：416 項通過，涵蓋每個 C／S 格式、CP950、容量邊界、錯誤與截斷原子性、未知物件、S158 商店原分派、移動／死亡／換圖／移除重建、sender admission、失敗斷線、SPR 合成解碼、字型標籤像素與選取 owner。
- `title-emote-assets`：160 項通過；四動作八方向 gfx0，加上現有另外 12 個角色 gfx 的方向 0 四動作。查驗原索引及各需要的封裝 SHA，素材只讀。
- `title-emote-compose`：13 項通過；實際原圖自身四動作替換／像素輸出，以及其他角色的表情 visual 與可選取 owner。只測函式，沒有啟動遊戲。
- 測試已提交於 `engine/_integration/dev/title-emote-tests/`，接入 DemoChecks 預設入口及現有 CI 的 `combat-admission` 群組。CI 以 PR 的最新 commit checks 為準。
- `check-no-game-assets.py`、`git -c core.whitespace=cr-at-eol diff --check` 通過。

本機線上 NuGet 遇到 TLS 認證錯誤，改用 `scratch/issue-323/NuGet.Config` 唯讀指向原專案 `engine/vendor`；所有還原快取、暫存、輸出均在 worktree。GitHub CLI 的既有 token 無效，改用已連接的 GitHub 帳號建立 PR／查 CI；Git 傳輸使用單次 `http.sslBackend=openssl` 且保留憑證驗證。

## 未驗證什麼／限制

未做真機登入、角色權限與拒絕訊息、雙角色視野廣播、實際 UI 操作或音效驗收。未窮舉所有變身外觀；原圖缺件會顯示提示，沒有替代假圖。第一次使用封裝同步 SHA 核驗可能造成載入停頓；後續使用記憶體快取。表情音效與缺少的原生陰影不在此任務範圍。離線合成不是原生遊戲畫面驗收。

## 產出路徑／給下一棒的提示

- 產品：`engine/source/TitleAndEmote.cs`；最小掛接：`WorldSession.cs`、`ObjectRendering.cs`；dev lock 已由腳本重產。
- 契約：[title-emote.md](../protocol/title-emote.md)；測試：[README](../../engine/_integration/dev/title-emote-tests/README.md)。本機結果與建置紀錄：`scratch/issue-323/`。
- 分支 `task/323`。由 Claude／調度器審查 PR 與最新 CI，使用者合併；dev lock 有同期衝突時依契約由同步輪重產，不拼接 SHA。
- dot 於 M6 smoke：開「更多 → 稱號／表情」，分別核對有權／無權稱號、自身與其他測試角色名稱上方的稱號更新、四種表情、移動／換圖中止，以及首次原圖核驗停頓。依既有 P24Lab 授權執行，不連正式服務。
