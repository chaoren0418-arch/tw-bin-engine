# Issue #323／T137：稱號與表情動作

## 做了什麼

- 新增 C90 稱號請求、S183 自身／其他物件稱號更新；在角色名稱上方畫出稱號。保留既有 S87 稱號欄位與伺服器權限判定。
- 新增 C120 四種表情選單與 S158/action66–69；原圖從外部客戶端唯讀載入，保留方向、幀時序、深度排序及像素 owner。自行送出只預測自身表情，名稱／稱號不預測。
- 玩家「更多」服務視窗新增「稱號／表情」頁：角色名、稱號輸入、表情選單、送出結果。舊世界請求、連點、截斷資料與傳送失敗均有防護。
- 產品只改 `TitleAndEmote.cs`、`WorldSession.cs` 的 S183 case 與 S158 共用 case 的一個子分支，以及 `ObjectRendering.cs` 一個完成合成掛載點。`PlayerChat.cs`、`docs/REPORT.md`、封存、原專案均未修改；沒有啟停遊戲、伺服器或 DB。

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

**同步輪（第 3 輪，2026-11-07）**：已依調度器指示 merge `origin/main` 兩次（先落後 99 個 commit；推送時 main 又前進 7 個，再同步一次），前述同步阻塞解除。解衝突只做聯集：`DemoChecks.csproj` 保留 title-emote 與 main 的 player-query／character-config／polymorph／buddies 引用；`Program.cs` 預設入口保留 `CharacterConfigChecks`、`TitleAndEmoteChecks` 與 `PlayerQueryChecks`；`WorldSession.cs` 保留 S183／S158 與 main 的 S250／S71 分派；`rebuild-lock.json` 以 main 為底跑 `--update-lock` 重產，未手動拼 hash；`docs/REPORT.md` 與 main 相同。未改其他程式內容。

同步後本機結果：`--update-lock`、`--build`、`--verify` 通過（189 個來源／198 個 pin），產品 DLL SHA-256 `fdc0bcd4891a1bd3cd926098e9ff9442b4d99d187673090e09aa7717c2f9dcb7`；DemoChecks 建置 0 warning／0 error。群組全過：`title-emote` 506、`title-emote-assets` 250、`title-emote-compose` 103、`combat-admission` 784、`map-continuity` 20733、`player-query` 791、`polymorph-cadence` 123、`buddies` 259、`item-transfer` 163；`character-management` 218；`check-no-game-assets.py` 通過（3331 個追蹤檔、0 錯誤）。title-emote 三組各比前輪多 90 項，是 main 在 `Main` 開頭加入、每個群組都會跑的 `PolymorphChecks()`，本模組測試本身未變。

**同步輪（第 4 輪，2026-11-07）**：PR #356 再度落後，依調度器指示合併 `origin/main`；同步期間 main 持續前進，共合併三次（落後 124 個 commit，之後再進來門與幽靈模式）。這輪除了文字衝突，還有三個 Git 沒標出、直接聯集會壞掉的語意衝突，**因此改動超出單純同步**，都限在本任務的檔案與 S158 共用 case：

- `WorldSession.cs`：main 的釣魚與門都用 S158，和本任務的 `case 158` 重複。現為 `case 158:ObserveFishing(plain);if(ReceiveDoorAction(plain)||ReceiveEmote(plain))return;break;`。順序不能換：門要處理自身 ID 的所有 action，含未知動作。
- `TitleAndEmote.cs` 合成：重新 Blit 時原本只用 `session.Opacity`，會讓畫面有稱號或表情時隱身角色變回不透明；改走 main 的 `InvisibilityOpacity`。
- `TitleAndEmote.cs` 分頁掛載：main 的 `LocationShare.cs` 已 override `OnShown`，造成 CS0111 編譯失敗；`OnLoad`、`OnVisibleChanged`、`OnActivated` 也都有模組使用，改掛 `OnCreateControl`（建構式未提早呼叫 `CreateControl`，已查過；第 5 輪又改掛，見下）。此掛載點只有編譯驗證，**分頁實際出現未驗證**，請 smoke 時確認。
- 測試：`TitleAndEmoteChecks.cs` 新增 S158 表情／釣魚、門物件優先、稱號合成保留隱身半透明三項回歸。其餘衝突（`DemoChecks.csproj`、`Program.cs`）取聯集；`rebuild-lock.json` 以 main 為底跑 `--update-lock` 重產；`docs/REPORT.md` 用 main 版本。

本輪最後一次完整驗證（基準 main `052f8c7`）：`--update-lock`、`--build`、唯讀核驗通過（202 個來源／211 個 pin），產品 0 error、2 個 `MagicUi.cs` 既有 warning，DemoChecks 0 warning／0 error，產品 DLL SHA-256 `22383f61b88c9a57176b71e9dc10b9ae48e737ec15173ad7a06c1a5926872e88`。群組全過：`title-emote` 613、`title-emote-assets` 353、`title-emote-compose` 206、`combat-admission` 1746、`map-continuity` 21759、`polymorph-cadence` 226、`buddies` 362、`item-transfer` 266、`player-query` 894、`fishing` 232、`doors` 514、`invisibility` 263、`duel` 307、`ambience` 549、`pets` 193、`location-share` 292、`attack-skills` 934、`character-config` 286、`board` 423、`ghost-mode` 552。`check-no-game-assets.py` 通過（3382 個追蹤檔、0 錯誤）。`title-emote-compose` 以原專案根目錄作 assets 唯讀定位，結果寫在 worktree，原專案未新增 `engine/build/offline-checks`。

**同步輪（第 5 輪，2026-11-07）**：PR #356 的 CI `dotnet` job 失敗，原因是 `REFUSED: sourceCount 202 != pinned source files 203`：head `b4a8297` 合入 main 帶進 `Portals.cs`，但沒有重產 dev lock。本輪合併 `origin/main`（`3630dcf`，再進來血盟與信件）並以 main 的 lock 為底跑 `--update-lock`。`DemoChecks.csproj`、`Program.cs` 取聯集，保留 `TitleAndEmoteChecks` 與 `ClanChecks`。

**本輪有一處改動超出單純同步，是編譯必要的**：main 的 `ClanUi.cs` 也 override `ClientForm.OnCreateControl`，與第 4 輪的分頁掛載點重複，Git 不會標出但會 CS0111。只改本任務的 `TitleAndEmote.cs`：改為 `OnHandleCreated` 掛一次 `Shown`（handle 可能重建，故有旗標防重複），分頁內容原樣搬進 `InitTitleEmoteUi`；`ClanUi.cs` 未動。分頁加入時機因此從建立控制項時延後到 `Shown`，與血盟分頁相同；`ShowPlayerService` 是用名稱動態查頁，不受影響。此掛載點同樣只有編譯驗證，**分頁實際出現未驗證**。

本輪驗證（基準 main `3630dcf`）：`--update-lock`、`--build`、唯讀核驗通過（207 個來源／216 個 pin），產品 DLL SHA-256 `0146647b1c5336616e95d0139969efc706b190d8c73174228da5d9f18441b836`；DemoChecks 0 warning／0 error。群組全過：`title-emote` 613、`title-emote-assets` 353、`title-emote-compose` 206、`combat-admission` 1746、`map-continuity` 21978、`polymorph-cadence` 226、`buddies` 362、`item-transfer` 266、`player-query` 894、`fishing` 232、`doors` 514、`invisibility` 263、`duel` 307、`ambience` 549、`pets` 193、`location-share` 292、`attack-skills` 934、`character-config` 286、`board` 423、`ghost-mode` 552、`portals` 293、`clan` 312；獨立專案 `MailChecks` 119、`WeaponRepairChecks` 98。`check-no-game-assets.py` 通過（3400 個追蹤檔、0 錯誤）。CI 的 `web-resource`、`ResourceChecks`、`check-local-resources`、`packet-trace`、`npc-checks` 本機未跑，以 CI 為準。本機 `python` 不在 PATH，改用 Python312 完整路徑。

**未通過／未驗證**：`combat-loop` 群組與 DemoChecks 無群組的預設全量入口需要 `engine/assets`，本 worktree 沒有 junction，沒有跑。`combat-loop` 退出 1，停在讀 `integrity.json`，不是斷言失敗；預設入口第 3 輪停在 `Movement123` 讀 `terrain0.bin`，本輪未重跑。測試碼有檔案寫入路徑，指向原專案會違反「原專案只讀」，故未這樣做。CI 不跑這兩個入口。CI 結果以 PR #356 為準，本機通過不能替代 CI 綠燈。

未做真機登入、角色權限與拒絕訊息、雙角色視野廣播、實際 UI 操作或音效驗收。未窮舉所有變身外觀；原圖缺件會顯示提示，沒有替代假圖。第一次使用封裝同步 SHA 核驗可能造成載入停頓；後續使用記憶體快取。表情音效與缺少的原生陰影不在此任務範圍。離線合成不是原生遊戲畫面驗收。

## 產出路徑／給下一棒的提示

- 產品：`engine/source/TitleAndEmote.cs`；最小掛接：`WorldSession.cs`、`ObjectRendering.cs`；dev lock 已由腳本重產。
- 契約：[title-emote.md](../protocol/title-emote.md)；測試：[README](../../engine/_integration/dev/title-emote-tests/README.md)。本機結果與建置紀錄：`scratch/issue-323/`。
- 分支 `task/323`，PR #356。同步已完成，下一棒：調度器確認兩個 CI job 綠燈後依閘門合併；若 main 再前進，重新同步並以 `--update-lock` 重產 dev lock。之後的 S158 新模組要接在同一個 `case 158`，表情必須留在門之後。
- 只把 main 合進來但不重產 lock（例如 GitHub 的 Update branch）會讓 CI 以 `sourceCount` 不符失敗；同步一定要跑 `--update-lock`。`ClientForm` 的 `OnLoad`、`OnShown`、`OnVisibleChanged`、`OnActivated`、`OnCreateControl`、`OnHandleCreated` 都已有模組使用，新模組不要再 override，可直接訂閱事件或另找掛載方式。
- dot 於 M6 smoke：先確認分頁在「更多」出現（第 4、5 輪都改了掛載點），再開「更多 → 稱號／表情」，分別核對隱身角色有稱號時仍為半透明、有權／無權稱號、自身與其他測試角色名稱上方的稱號更新、四種表情、移動／換圖中止，以及首次原圖核驗停頓。依既有 P24Lab 授權執行，不連正式服務。
