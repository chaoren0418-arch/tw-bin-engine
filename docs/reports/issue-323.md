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

**本輪有一處改動超出單純同步，是編譯必要的**：main 的 `ClanUi.cs` 也 override `ClientForm.OnCreateControl`，與第 4 輪的分頁掛載點重複，Git 不會標出但會 CS0111。只改本任務的 `TitleAndEmote.cs`：先改為 `OnHandleCreated`，推送後 CI 全綠（`8cfd75f`），但 main 接著進來的 `Marriage.cs` 也用 `OnHandleCreated`，所以再同步一次（`f8679a8`）時改掛 `SetVisibleCore`：首次顯示前訂閱一次 `Shown`，分頁內容原樣搬進 `InitTitleEmoteUi`；`ClanUi.cs`、`Marriage.cs` 未動。分頁加入時機因此從建立控制項時延後到 `Shown`，與血盟、結婚分頁相同；`ShowPlayerService` 是用名稱動態查頁，不受影響。此掛載點同樣只有編譯驗證，**分頁實際出現未驗證**。

之後 main 再進來角色重置（`5555e4a`），`WorldSession.cs` 的 S183 與 S64 相鄰衝突，兩個 case 都保留；測試接線取聯集。推送紀錄：`6804ff5` 是在這次合併衝突未解時，由未設條件的指令鏈推上去的，但推送只含已驗證的前一個 commit，不含衝突內容；該 head 因 PR 有衝突而沒有 CI，不算驗證。

本輪驗證（基準 main `5555e4a`）：`--update-lock`、`--build` 通過（210 個來源／219 個 pin），產品 DLL SHA-256 `9059664e77b26cafd2bba7b2115107f7ef8cf68961f6a6563e416efb9ba00122`；DemoChecks 0 warning／0 error。群組全過：`title-emote` 613、`title-emote-assets` 353、`title-emote-compose` 206、`combat-admission` 1746、`map-continuity` 22275、`marriage` 329、`char-reset` 354、`polymorph-cadence` 226、`buddies` 362、`item-transfer` 266、`player-query` 894、`fishing` 232、`doors` 514、`invisibility` 263、`duel` 307、`ambience` 549、`pets` 193、`location-share` 292、`attack-skills` 934、`character-config` 286、`board` 423、`ghost-mode` 552、`portals` 293、`clan` 312；獨立專案 `MailChecks` 119、`WeaponRepairChecks` 98。`check-no-game-assets.py` 通過（3411 個追蹤檔、0 錯誤）。CI 的 `web-resource`、`ResourceChecks`、`check-local-resources`、`packet-trace`、`npc-checks` 本機未跑，以 CI 為準。本機 `python` 不在 PATH，改用 Python312 完整路徑。

**同步輪（第 6 輪，2026-11-07）**：`fef78ff` 的 CI 三個 job 全綠，但 main 又前進 44 個 commit（狀態異常 #388、組隊 #259）。合併 `origin/main`（`dd640a7`）；`DemoChecks.csproj`、`WorldSession.cs` 取聯集（S183 與 S165／S202／S47 並存），`rebuild-lock.json` 以 main 為底跑 `--update-lock` 重產，`Program.cs` 自動合併。

**本輪有一處改動超出單純同步**：main 把物件合成改由 `ComposeAfflictionPatch` 負責，而 `ComposeTitlesAndEmotes` 原本自己重新 Blit，直接聯集會在畫面有稱號或表情時洗掉中毒／灰色染色與盲目遮罩。改法都限在本任務的掛載點與 `TitleAndEmote.cs`：

- `ObjectRendering.cs` 掛載點改為接收 `ComposeAfflictionPatch` 的結果；`ComposeTitlesAndEmotes` 需要重畫時也走同一個入口，不再自己 Blit。只有稱號且範圍沒變時直接沿用傳入的 patch，避免盲目時多一次全畫面合成。
- 盲目 1 不畫其他角色的稱號（否則黑畫面會洩漏對方位置），自身稱號保留；盲目 2 的稱號與場景一樣變暗。這是呈現政策，**未與原客戶端比對**，已寫進 `docs/protocol/title-emote.md`。
- 測試新增：稱號重畫保留中毒染色；盲目 1 不洩漏他人稱號、保留自身稱號、他人維持全黑；盲目 2 稱號變暗；解除後逐像素還原；盲目 1 自身表情仍可見。

本輪驗證（基準 main `dd640a7`）：`--update-lock`、`--build` 通過（213 個來源／222 個 pin），產品 DLL SHA-256 `af499b512c7c74cf19e806d12609873e25a445785770830c44d5dc0cba094a1b`；DemoChecks 0 warning／0 error。群組全過：`title-emote` 704、`title-emote-assets` 436、`title-emote-compose` 290、`afflictions` 428、`party` 276、`combat-admission` 1837、`map-continuity` 22358、`marriage` 412、`char-reset` 437、`polymorph-cadence` 309、`buddies` 445、`item-transfer` 349、`player-query` 977、`fishing` 315、`doors` 597、`invisibility` 346、`duel` 390、`ambience` 632、`pets` 276、`location-share` 375、`attack-skills` 1017、`character-config` 369、`board` 506、`ghost-mode` 635、`portals` 376、`clan` 395；獨立專案 `MailChecks` 119、`WeaponRepairChecks` 98。`check-no-game-assets.py` 通過（3420 個追蹤檔、0 錯誤）。各群組計數含 `Main` 開頭共用的檢查，所以比前輪多，不全是本模組新增。

**同步輪（第 7 輪，2026-11-07）**：main 又前進 25 個 commit（小地圖 #390、逆向資源盤點 #417）。合併 `origin/main`（`4518ebc`）：`DemoChecks.csproj` 取聯集（`TitleAndEmoteChecks` 與 `MinimapChecks` 並存）；`Program.cs` 預設入口用 main 版本並保留 `await TitleAndEmoteChecks();`，`minimap` 群組與 `map-continuity` 內的 `MinimapChecks()` 照 main；`rebuild-lock.json` 以 main 為底跑 `--update-lock` 重產；`docs/REPORT.md` 與 main 相同。本輪**沒有**超出同步的程式改動，`WorldSession.cs`、`ObjectRendering.cs`、`TitleAndEmote.cs` 都未動（main 這次沒有改到這三個檔，也沒有新的 `ClientForm` override 撞名，建置通過可證）。

本輪驗證（基準 main `4518ebc`）：`--update-lock`、`--build` 通過（215 個來源／224 個 pin），產品 DLL SHA-256 `0b25d4da7871e044183c020fd0d10dcfde6b68c3b25d8cd9cff9c0c7335be533`；DemoChecks 0 warning／0 error。群組全過：`title-emote` 704、`title-emote-assets` 436、`title-emote-compose` 290、`minimap` 347、`map-continuity` 22429、`afflictions` 428、`party` 276、`combat-admission` 1837、`marriage` 412、`char-reset` 437、`polymorph-cadence` 309、`buddies` 445、`item-transfer` 349、`player-query` 977、`fishing` 315、`doors` 597、`invisibility` 346、`duel` 390、`ambience` 632、`pets` 276、`location-share` 375、`attack-skills` 1017、`character-config` 369、`board` 506、`ghost-mode` 635、`portals` 376、`clan` 395。本模組三組與第 6 輪相同；`map-continuity` 多出的 71 項是 main 的小地圖檢查。`check-no-game-assets.py` 通過（3448 個追蹤檔、0 錯誤）。`diff --check` 只在 main 帶進的 `work/issue-417/rea-results.md` 有行尾空白，與本任務無關，未修改。獨立專案 `MailChecks`、`WeaponRepairChecks` 本輪未重跑（其來源未變）。結果在 `scratch/issue-323/sync8/`。

**同步輪（第 8 輪，2026-11-07）**：main 又前進（顯示通知 #415）。合併 `origin/main`（`4a962f3`）：`Program.cs` 預設入口取聯集（main 的 `DisplayNoticeChecks();` 與本任務的 `await TitleAndEmoteChecks();` 並存），`display-notices` 群組照 main；`rebuild-lock.json` 以 main 為底跑 `--update-lock` 重產；`WorldSession.cs`、`ObjectRendering.cs`、`DemoChecks.csproj` 自動合併；`docs/REPORT.md` 與 main 相同。沒有超出同步的程式改動。

本輪驗證（基準 main `4a962f3`）：`--update-lock`、`--build` 通過（216 個來源／225 個 pin），產品 DLL SHA-256 `225cbb3d4ff64434ae07b5db96fa7c77c3b16ca41c71f3006273237104efca9c`；DemoChecks 0 error。群組全過：`title-emote` 704、`title-emote-assets` 436、`title-emote-compose` 290（以原專案根目錄唯讀定位 assets，原專案未新增 `engine/build/offline-checks`）、`display-notices` 334、`map-continuity` 22487、`combat-admission` 1837、`afflictions` 428、`polymorph-cadence` 309、`buddies` 445、`item-transfer` 349、`doors` 597。本模組三組與第 7 輪相同。`check-no-game-assets.py` 通過（3446 個追蹤檔、0 錯誤）。其他群組本輪未重跑，以 CI 為準。結果在 `scratch/issue-323/sync9/`。

**同步輪（第 9 輪，2026-11-07）**：main 又前進（限時狀態 #416）。合併 `origin/main`（`98bf2b7`）：`DemoChecks.csproj` 取聯集（`TitleAndEmoteChecks` 與 `TimedBuffChecks` 並存）；`Program.cs` 預設入口取聯集（本任務的 `await TitleAndEmoteChecks();` 與 main 的 `TimedBuffChecks();` 並存），`timed-buffs` 群組照 main；`rebuild-lock.json` 以 main 為底跑 `--update-lock` 重產；`WorldSession.cs` 自動合併；`docs/REPORT.md` 與 main 相同。沒有超出同步的程式改動。

本輪驗證（基準 main `98bf2b7`）：`--update-lock`、`--build` 通過（217 個來源／226 個 pin），產品 DLL SHA-256 `0de88ecb3ba34d32371834608ba1afed93c3eb9703d8c23d149b5ac7c1510698`；DemoChecks 0 warning／0 error。群組全過：`title-emote` 704、`title-emote-assets` 436、`title-emote-compose` 290（以原專案根目錄唯讀定位 assets）、`timed-buffs` 560、`display-notices` 334、`map-continuity` 22771、`combat-admission` 1837、`afflictions` 428、`doors` 597。本模組三組與第 8 輪相同。`check-no-game-assets.py` 通過（3451 個追蹤檔、0 錯誤）。其他群組本輪未重跑，以 CI 為準。結果在 `scratch/issue-323/sync10/`。

**同步輪（第 10 輪，2026-11-07）**：遠端 `task/323` 被 GitHub 的「Update branch」合入 main（`8f7ff01`，帶進道具狀態 #216 的 `ItemStatus.cs`），但沒有重產 dev lock，CI `dotnet` job 以 `REFUSED: sourceCount 217 != pinned source files 218` 失敗。本輪先快轉到 `8f7ff01`，確認 `Program.cs` 自動合併保留 `await TitleAndEmoteChecks();` 與 main 的 `ItemStatusChecks();`，再跑 `--update-lock` 重產 lock。沒有其他改動。

本輪驗證（基準 main `2aa41de`）：`--update-lock`、`--build` 通過（218 個來源／227 個 pin），產品 DLL SHA-256 `14684f7e423c03e9b069340a3ec9e58fe2e5176e25e31f4ac340123fb511d4b5`。群組全過：`title-emote` 704、`combat-admission` 1837（CI 用的群組）、`item-status` 358。`check-no-game-assets.py` 通過（3455 個追蹤檔、0 錯誤）。`title-emote-assets`／`title-emote-compose` 本輪未重跑（本模組來源未變）。結果在 `scratch/issue-323/`。

**未通過／未驗證**：`combat-loop` 群組與 DemoChecks 無群組的預設全量入口需要 `engine/assets`，本 worktree 沒有 junction，沒有跑。`combat-loop` 退出 1，停在讀 `integrity.json`，不是斷言失敗；預設入口第 3 輪停在 `Movement123` 讀 `terrain0.bin`，本輪未重跑。測試碼有檔案寫入路徑，指向原專案會違反「原專案只讀」，故未這樣做。CI 不跑這兩個入口。CI 結果以 PR #356 為準，本機通過不能替代 CI 綠燈。

未做真機登入、角色權限與拒絕訊息、雙角色視野廣播、實際 UI 操作或音效驗收。未窮舉所有變身外觀；原圖缺件會顯示提示，沒有替代假圖。第一次使用封裝同步 SHA 核驗可能造成載入停頓；後續使用記憶體快取。表情音效與缺少的原生陰影不在此任務範圍。離線合成不是原生遊戲畫面驗收。

## 產出路徑／給下一棒的提示

- 產品：`engine/source/TitleAndEmote.cs`；最小掛接：`WorldSession.cs`、`ObjectRendering.cs`；dev lock 已由腳本重產。
- 契約：[title-emote.md](../protocol/title-emote.md)；測試：[README](../../engine/_integration/dev/title-emote-tests/README.md)。本機結果與建置紀錄：`scratch/issue-323/`。
- 分支 `task/323`，PR #356。同步已完成，下一棒：調度器確認兩個 CI job 綠燈後依閘門合併；若 main 再前進，重新同步並以 `--update-lock` 重產 dev lock。之後的 S158 新模組要接在同一個 `case 158`，表情必須留在門之後。物件合成的新效果請加在 `ComposeAfflictionPatch`，稱號與表情的重畫會自動帶上；不要在 `ComposeTitlesAndEmotes` 另寫 Blit。
- 只把 main 合進來但不重產 lock（例如 GitHub 的 Update branch）會讓 CI 以 `sourceCount` 不符失敗；同步一定要跑 `--update-lock`。`ClientForm` 的 `OnLoad`、`OnShown`、`OnVisibleChanged`、`OnActivated`、`OnCreateControl`、`OnHandleCreated`、`SetVisibleCore` 都已有模組使用；每個模組各佔一個 override 的做法已撞名多次，建議由調度器另開任務在 `ClientForm` 建一個共用的 UI 初始化事件。
- dot 於 M6 smoke：先確認分頁在「更多」出現（第 4、5 輪都改了掛載點），再開「更多 → 稱號／表情」，分別核對隱身角色有稱號時仍為半透明、中毒與盲目時的稱號和表情（盲目 1 看不到他人稱號，並觀察盲目時的流暢度）、有權／無權稱號、自身與其他測試角色名稱上方的稱號更新、四種表情、移動／換圖中止，以及首次原圖核驗停頓。依既有 P24Lab 授權執行，不連正式服務。
