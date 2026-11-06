# Issue #216：T121 物品 status blob 語義化

## 做了什麼

- 新增 `engine/source/ItemStatus.cs`：解析常見武器／防具／一般物品欄位、強化、損傷度、能力、元素及狀態抗性、Big5 說明；保留原始 hex、未解析尾段及錯誤位置。
- `InventoryItem.StatusHex` 更新同步重建結構化屬性，涵蓋既有 S5／S15／S24／S242。可交易等資訊讀取背包旗標，沒有杜撰交易 tag。
- `InventoryItem.Equipped` 綁定 session 權威 S64 槽位查詢，不再利用名稱後綴；`Equipment.cs` 僅新增讀取介面。原生背包 tooltip 顯示屬性、裝備槽位／待同步與旗標。
- 新增並提交 `demo-tests/ItemStatusChecks.cs`，接入既有測試入口與 CI。沒有更動 SupportedUse 區段、共用封包分派、正式入口、封存或 `docs/REPORT.md`。

## 怎麼驗證（已驗證）

1. `rebuild.py --update-lock` 及來源核驗：173 個來源／182 個 pin 通過，只更新 dev lock。
2. `rebuild.py --build --configfile scratch/issue-216/NuGet.Config --output scratch/issue-216/build`：產品與 launcher 編譯成功；產品 0 錯誤，既有 `MagicUi.cs:20` 兩個 nullable 警告。初次 nuget.org restore 因本機 TLS 認證失敗，改為唯讀使用主專案 `engine/vendor` 的現存 WebView2 nupkg；NuGet 快取、暫存與產物均留在 worktree。
3. `DemoChecks ... item-status`：**82 項通過**。涵蓋 signed C/H、複合欄位、0x13 內嵌 0x27／FF 標記、MR 與六種耐性、四元素抗性、Big5 重複說明、未知／截斷 tag、空 blob、S24／S242 更新、旗標、名稱偽造、槽位先到、全清單替換、絕對穿卸、reset、外層截斷不部分修改，以及 tooltip 文字。
4. `check-no-game-assets.py`：通過，0 errors；`git diff --check` 通過。
5. GitHub CI：PR 建立後核對，結果記錄於 PR 檢查。

本機產品 DLL SHA-256：`b228e5bfce92652046bad717437edf9869c354c658a0cb958ec8b3849791aeb7`。

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

由 Claude 審查 PR、使用者合併；dev lock 如有並行衝突由調度器同步輪重新產生。dot 的 M6 smoke 核對背包 tooltip 的強化、損傷、抗性與說明，以及穿上／卸下／重新登入後的槽位顯示。`Equipped` 現為即時權威查詢；需要歷史快照的呼叫端應保存 bool? 值。歷史依名稱推算的測試與不在本任務範圍的 HUD／Web 額外 tooltip，可在後續統一整理。
