# Issue #390 — T150：小地圖

分支：`task/390`。基底：`9f62553cf8e1b1f3c8d26cb0c48e0dfd551d19ba`。階段：code。

## 做了什麼

- 新增 `engine/source/Minimap.cs`：唯讀擷取既有 `OriginalMapCache.Scene`，從 RGB555 像素與既有繪圖順序產生等角縮圖，最長邊上限 512 像素；不建立全解析度地圖副本、不額外載入區塊、不寫入影像或遊戲資源。
- 新增 `engine/source/MinimapUi.cs`：場景右上角小地圖、HUD 按鈕及 F5 開關、按鈕／滾輪 1×／2×／4× 縮放，放大後跟隨自己的位置；以白點標自己、金點標 NPC／非玩家物件。每 200 ms 讀取鎖定的世界快照，只有快取實例、revision、epoch 或地圖改變才重建影像。
- `PlayerHudView.cs` 僅新增一行掛載；`KeyboardNavigation.cs` 僅新增一行 F5 分派。F5 受遊戲輸入、焦點與修飾鍵限制，長按不重複開關。
- 同圖重設、傳送等待、資源失敗、登出、關閉或控制項釋放時清掉縮圖及標記。沒有新的網路分派、C 封包或伺服器操作。
- 新增可追蹤的 `engine/_integration/dev/minimap-tests/`，接到既有 DemoChecks 的 `minimap`、`map-continuity` 與預設入口；現有 Windows CI 會執行。以工具更新 dev lock，未改封存 lock。

## 格式依據與限制

已查閱 `docs/design/m6-contract.md`、`docs/reference/server-protocol.md` 常用封包表及 `server-rules.md` R09–R11、R26、R34–R35。

- 使用既有 S87 自身／物件狀態；S10 為來源座標加 heading，沿用解析後終點。S120 物件移除、S206（含同圖）及 S149 傳送依既有世界生命週期清除。S122 面向、S158 死亡亦納入測試；S76 依 #263 合併後的語意是原地換外觀，NPC 標記保留在原位。未新增封包格式，所以沒有新增 C 格式需要編碼測試。
- 目前基底沒有 `Party.cs`／`Party` 介面，T125 仍為 review；依任務允許先不顯示隊友，不從姓名或 gfx 猜隊友。
- S87 沒有 NPC 模板或種族欄位。金點依非 PC 旗標的已知物件顯示，包含怪物及場景物件，不能宣稱是友善 NPC 的精確分類；帶 PC 旗標的變形 NPC 不會被猜成一般 NPC。地面物品已由既有 Ground 分流，另排除死亡及快取範圍外物件；`AppearancePending` 判斷沿用全專案慣例保留，但 #263 後已沒有封包路徑會把物件設為待更新。
- 快取是移動式 2×2 區塊，不包含整張地圖。介面明示「已載入區域」，不補造未載入地形；不累積舊圖或已離開視野的 NPC。自身位置沿用場景 Actor（可含既有移動預測），並接受 S87／S10 校正。
- 縮圖使用靜態快取像素；沒有複製完整場景的透明度動畫、船底圖或動態特效。船圖的透明地面不補造像素。

## 怎麼驗證（已驗證）

1. `rebuild.py --update-lock`：186 個來源、195 個鎖定檔核驗通過。
2. `rebuild.py --build --configfile scratch/issue-390/nuget.config --output scratch/issue-390/build --write-receipt`：主程式、audio 與 launcher 成功；只唯讀使用原專案 vendor 的 WebView2 套件，NuGet 與暫存輸出隔離在本 worktree。主程式 0 錯誤，有 `MagicUi.cs:20` 既有 CS8600／CS8602 兩項警告。
3. DemoChecks `minimap`：**51 項通過**；合成封包、快取更新／失效、縮放、逐像素取樣、快捷鍵焦點與真實控制項離屏繪製。沒有啟動產品入口或顯示視窗。
4. DemoChecks `map-continuity`：**409 項通過**，包含既有跨圖／公告板與新增小地圖檢查。
5. `check-no-game-assets.py`：通過，0 錯誤，4 個核准的 EmbeddedResource 宣告。
6. `git -c core.whitespace=cr-at-eol diff --check`：通過；保留原檔的換行格式。

產品 DLL SHA-256：`7b9c38a5ef538e18a2847eeca53ef9f11e2ca301707655d635de13c6a670ab1d`。

來源 SHA-256：

| 檔案 | SHA-256 |
|---|---|
| `engine/source/Minimap.cs` | `ccf546f43aec96f4d0fffa11fbfabdd524eba1509a7a58889e5bfa3b8915ede7` |
| `engine/source/MinimapUi.cs` | `72ae3d2581351108fc5e6a0bb86d569e64a257e814ff3633f22961677c241dbb` |

## 同步輪（第 2 輪，claude-local）

依調度器指示合併 `origin/main`（落後 47 筆；同步期間 main 又前進，合到 `065e81e`，含 #324 角色快捷設定）。上方第 1～6 項與產品 DLL SHA 是第 1 輪紀錄，以本節為最新結果。

- 衝突在 `DemoChecks.csproj`（兩邊各加測試連結，全部保留）、`Program.cs`（`map-continuity` 用 main 的新位置並接上 `MinimapChecks()`，保留 `minimap` 群組，預設入口保留兩邊的呼叫）與 `rebuild-lock.json`。lock 以 `--update-lock` 重產（189 個來源、198 個鎖定檔），未手動拼接。
- F5 與 #324 不衝突：快捷欄用 F1～F4，`engine/source` 沒有其他 F5 綁定。
- **測試語意調整**：#263 把 S76 改成權威變身封包後，第 1 輪的「S76 待更新會隱藏 NPC 標記」不再成立，合併後這項失敗。遠端物件收到 S76 只換 gfx、物件仍在，標記應保留，所以測試改為驗證同一標記留在原位且 gfx 已更新。產品程式碼未改，`Minimap.cs`／`MinimapUi.cs` 與第 1 輪相同。
- 建置：`rebuild.py --build` 退出 0。產品 DLL SHA-256：`f93ecee4370458c9cc476016d7eb1a964e5b0997fe16514905c20d8e8143a26a`。
- DemoChecks 全部通過：`minimap` 141、`map-continuity` 20083、`character-config` 183、`combat-admission` 368、`polymorph-cadence` 123、`buddies` 259、`item-transfer` 163。項數比第 1 輪多，是因為 main 新增的 POLY 檢查在群組分派前就會執行，`map-continuity` 另有 main 新增的群組內檢查，不代表小地圖測試變多。
- `check-no-game-assets.py` 通過（3320 個追蹤檔、0 錯誤）。
- **預設入口未通過（本機環境限制）**：在合併 #324 之前實測，不帶群組的 DemoChecks 跑完 `MinimapChecks` 後，在 `Movement123` 讀取 `engine/assets/terrain0.bin` 時失敗，因為本 worktree 沒有 `engine/assets` junction。與小地圖無關，CI 也不跑預設入口；本輪未建立 junction，所以預設入口其餘項目未驗證。

## 未驗證什麼

未啟動遊戲、伺服器、DB；未做真實地圖畫面、不同 DPI／視窗大小、實際鍵鼠焦點及長時間換區效能的實機驗收。合成快取與離屏繪製不是原生實機證據。CI 執行結果以本 PR 的 checks 為準。

## 產出路徑

- 產品：`engine/source/Minimap.cs`、`MinimapUi.cs` 與兩個單行掛接。
- 測試：`engine/_integration/dev/minimap-tests/MinimapChecks.cs`、`README.md`，以及 `demo-tests` 的編譯連結／入口。
- 本報告：`docs/reports/issue-390.md`。
- 本機證據（不追蹤）：`scratch/issue-390/minimap.json`、`map-continuity.json`、`build/result.json` 與建置日誌。未新增遊戲資源。

## 給下一棒的提示

閘門與 CI 通過後由調度器合併；dev lock 衝突依常設授權用 `--update-lock` 重產，不手動拼接 hash。M6 smoke 由 dot 在已授權的 P24Lab 測試角色驗證：F5 開關與長按、聊天輸入不觸發、三段縮放、走出快取區、NPC 移動／消失、同圖傳送／跨圖／登出清除，以及 HUD／服務面板交疊。T125 合併後，再依正式 Party 介面擴充隊友標記。
