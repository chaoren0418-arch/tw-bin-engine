# Issue #216：T121 物品 status blob 語義化

## 第 3 次：只做同步（2026-11-07）

- 先快轉至調度器的遠端提交 `f46f9a9`，再合入 `origin/main` 的 `6024daf`。僅解決合併衝突：CI 同時保留 item-status 與 item-transfer 入口；背包同時保留 tooltip 釋放與 main 的 `InitItemTransfer()`。未改其他模組功能或 `SupportedUse`；`docs/REPORT.md` 完全採用 main。
- 上輪 [CI 失敗紀錄](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42314535616/job/128223814782) 為 `sourceCount 175 != pinned source files 176`。本輪以 `rebuild.py --update-lock` 重產 dev lock，**177 個來源／186 個 pin** 核驗成功，未手動拼接 hash。
- 完整 `rebuild.py --build --configfile scratch/issue-216/NuGet.Config --output scratch/issue-216/build` 通過；產品仍有既有 `MagicUi.cs:20` 兩個 nullable 警告，launcher 無警告。唯讀使用主專案 vendor；快取與暫存留在 worktree。
- 既有 DemoChecks：item-status **82 項**、map-continuity **128 項**、combat-admission **212 項**通過。`check-no-game-assets.py`：**3284 個追蹤檔，0 errors**；相對 main 的 diff 檢查通過。
- **未完成：整體 CI 綠燈。** main 新增的 `item-transfer` 測試在 `engine/_integration/dev/item-transfer-tests/ItemTransferChecks.cs:68` 失敗：`equipped item cannot be deleted`。該案例只送 S15 名稱 `劍 ($9)`，未送 S64；與 T121「名稱不得建立裝備狀態」契約衝突。現有產品轉移閘門會查 `World.ItemEquipped` 與 `EquipmentSlots`，不應恢復名稱猜測來迎合舊測試。本輪「只做同步、不要改其他內容」未授權修改此測試，保留原樣交下一棒。
- 本機證據：`scratch/issue-216/build/result.json` 與 `scratch/issue-216/{item-status,map-continuity,combat-admission}-checks.json`；產品 DLL SHA-256：`21d47a8318fdd53bc012ff78e513bdfcb53fad1bef2eb875b0a8cc8fa7b65bf2`。本輪 PR CI 結果另回報於 PR #242，以下綠燈紀錄僅屬第 2 次歷史證據。
- 未啟動遊戲、伺服器或 DB；真實回包及 tooltip 畫面仍待 dot 的 M6 smoke。

## 做了什麼

- 新增 `engine/source/ItemStatus.cs`：解析常見武器／防具／一般物品欄位、強化、損傷度、能力、元素及狀態抗性、Big5 說明；保留原始 hex、未解析尾段及錯誤位置。
- `InventoryItem.StatusHex` 更新同步重建結構化屬性，涵蓋既有 S5／S15／S24／S242。可交易等資訊讀取背包旗標，沒有杜撰交易 tag。
- `InventoryItem.Equipped` 綁定 session 權威 S64 槽位查詢，不再利用名稱後綴；`Equipment.cs` 僅新增讀取介面。原生背包 tooltip 顯示屬性、裝備槽位／待同步與旗標。
- 新增並提交 `demo-tests/ItemStatusChecks.cs`，接入既有測試入口與 CI。沒有更動 SupportedUse 區段、共用封包分派、正式入口、封存或 `docs/REPORT.md`。
- 第 2 次執行：在原 `task/216` 合入 main `28a8cab`，以既有 `--update-lock` 重新產生完整整合來源的 dev lock，修正 PR 合併版來源計數；沒有手動拼接 hash，也未修改其他模組功能。沿用 PR #242。

## 怎麼驗證（已驗證）

1. `rebuild.py --update-lock` 及來源核驗：整合 main 後 **174 個來源／183 個 pin** 通過，只更新 dev lock。相對 Git 自動合併結果，腳本只將 `sourceCount` 由 173 改為 174。
2. `rebuild.py --build --configfile scratch/issue-216/NuGet.Config --output scratch/issue-216/build`：產品與 launcher 編譯成功；產品 0 錯誤，既有 `MagicUi.cs:20` 兩個 nullable 警告。初次 nuget.org restore 因本機 TLS 認證失敗，改為唯讀使用主專案 `engine/vendor` 的現存 WebView2 nupkg；NuGet 快取、暫存與產物均留在 worktree。
3. `DemoChecks ... item-status`：**82 項通過**。涵蓋 signed C/H、複合欄位、0x13 內嵌 0x27／FF 標記、MR 與六種耐性、四元素抗性、Big5 重複說明、未知／截斷 tag、空 blob、S24／S242 更新、旗標、名稱偽造、槽位先到、全清單替換、絕對穿卸、reset、外層截斷不部分修改，以及 tooltip 文字。
4. 既有離線回歸：map-continuity 128 項、combat-admission 212 項通過。
5. `check-no-game-assets.py`：整合後 3249 個追蹤檔、0 errors；本任務相對 main 的 `git diff origin/main --check` 通過。未整理 main 既有的其他模組行尾差異。
6. 第 2 次在完整整合來源重新執行建置、item-status 82 項、map-continuity 128 項及 combat-admission 212 項，全部通過。Python 使用現有 `D:/dev-orchestrator/tools/python/python.exe`；未安裝新工具。

## CI 退回原因與修正

[Linux CI](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42304858601/job/128191513298) 的 Windows 目標編譯與資源拒收成功，但 canonical 核驗失敗：`REFUSED: sourceCount 173 != pinned source files 174`。GitHub 測試的是 merge commit `590e619`（本分支 `a1e704c` 合入當時 main `e67198f`），不是本機已通過的分支來源。並行 main 已納入 `SkillCast.cs` 等變更，合併後 lock 的來源項增加、計數未同步；比較 API 已確認該新增檔案來自 main。

本輪依調度器退回要求，在同分支同步 main，並由標準腳本重新掃描全部來源。同步後先以唯讀核驗重現相同的 173／174 錯誤，再執行 `--update-lock` 修正，完整建置及離線回歸均通過。修正提交：`f59b237`。

整合提交 `f59b237` 的 CI **全部綠燈**：[build](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42308374042) 的 Linux canonical／資源／封包追蹤與 Windows 物品／地圖／戰鬥／WebView2 全部通過；[NPC 離線 CI](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42308374285) 通過。前次 CI 閘門退回條件已解除。

本輪本機產品 DLL SHA-256：`c15e3dc8ae40b03dfa9812a1250bd9e745fdd635db2d2e55bad38be5162de015`。

## 未驗證什麼

- 未啟動遊戲、登入、伺服器或 DB；未驗證真實滑鼠 tooltip 排版與原服即時回包。
- 沒有從名稱推算元素強化種類／階級；來源沒有獨立 tag。大數能力與自由文字說明保留為文字，不假裝數值解碼完成。
- 空 status 不推定零能力；未知 tag 後的內容保留未解析，避免錯位。
- 歷史 `engine/source/EquipmentTests.cs` 仍有名稱後綴的舊斷言，且依賴未追蹤 fixtures／assets，不在本次執行入口與允許修改範圍；本次由已提交、接入 CI 的權威槽位測試取代該項證據。

## 產出路徑

- 協定與來源 SHA：`docs/protocol/item-status.md`。
- 實作：`engine/source/ItemStatus.cs`、`Inventory.cs`、`Equipment.cs`、`InventoryUi.cs`。
- 測試：`engine/_integration/dev/demo-tests/ItemStatusChecks.cs`、`Program.cs`；CI：`.github/workflows/build.yml`。
- 本機可重現執行紀錄：`scratch/issue-216/build/`、`scratch/issue-216/item-status-checks.json`（未追蹤）；測試原始碼已提交，不以 scratch 代替測試交付。

## 給下一棒的提示

第 3 次已修正 dev lock 並同步 main；請調度器允許或另派 #273 測試維護：將 `ItemTransferChecks.cs:68` 的裝備前置改為合成 S64 權威槽位封包，再跑 item-transfer 與 CI。CI 尚未全綠前不要合併。通過後交 Claude 審查、使用者合併；dot 的 M6 smoke 核對 tooltip 與穿卸／重新登入槽位。`Equipped` 現為即時權威查詢；需要歷史快照的呼叫端應保存 bool? 值。歷史依名稱推算的測試及其他 HUD／Web tooltip 可在後續整理。
