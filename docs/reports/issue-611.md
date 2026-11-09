# Issue #611 — T250：寵物 AC 與裝備欄（PBW-E，R2-P）

階段 code，第 1 次嘗試，分支 `task/611`。依據 `docs/reference/packetbox-windows.md` 第 2.1、6 節。

## 做了什麼

- 新檔 `engine/source/PetEquipment.cs`：
  - `[WorldReceive(250,Subtype=37)] ReceivePetEquipment`：解 3.81 `S_PetEquipment` `C(250) C(37) C(data) D(petId) C(listNo) C(petAc)`（與 TW16 `cdcc` 同寬；AC 以有號 byte 解讀，同 `PetBag.Ac`）。petId 0、長度不足、尾端非 0 丟 `ProtocolException`。
  - 只接自己的寵物／召喚物；他人或未知物件消化後忽略並計數（`ignored`）。
  - 存「最近一次裝備更新」：data、listNo、AC，以及收到當時已知背包該格的物品 id／名稱（背包未知或索引超出時為 null）。
  - **不回送 C99，也不自動送任何封包**（原版送的 C_CHECK_INVENTORY 對應 3.81 C99，伺服器不回應）。
  - `[WorldReceive(176,Observe=true)] ObservePetBagOrder`：只觀察 S176 類型 11 到達的先後，不消化、不影響 `Pets.cs`。
  - 讀取介面：`PetEquipmentState(id)`、`PetAc(id)`（S250/37 與背包尾端 AC 取較新的一份）、`PetBagStale(id)`（背包比最近一次 37 舊 → 裝備欄的 Equipped 位元可能過時，需使用者按既有「重新查詢」）、`PetEquipmentRevision`。
  - 地圖切換（WorldEpoch）與寵物離開時清掉；`[WorldReset]`、`[WorldSnapshot("petEquipment")]`。
- `engine/source/Pets.cs`：只加一個唯讀方法 `IsOwnedCompanion(uint id)`（沿用既有 `CheckPets`／`OwnedCompanion` 規則），不改任何狀態或既有行為。
- 新檔 `engine/source/PetEquipmentTests.cs`：`[WorldModuleTest("pet-equipment")]`，38 項檢查，接既有 `--module-tests` 入口。涵蓋註冊、位元組格式與解碼、錯誤長度、背包先到／後到的 AC 取捨、過時旗標、索引超出、未知 data、他人寵物、無背包、地圖切換、Reset，以及**全程送出封包數為 0**。
- 未改 `WorldSession.cs`、`rebuild-lock.json`（#646：PR 不改 lock）。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock`（本機建置用；commit 前已還原 lock）：`dev-inputs-verified`（323 個來源檔）。
- `engine/build-product.ps1`：`PASS dev`（product DLL `456aeb2d76949d4b4ad46665d959b60c6adf791aafa6dfe23718a4eba20ae9d0`）。
- `P11.ClientEngine.exe --module-tests`（照 CI 參數）：`pass`，36 組共 1776 項，0 失敗（pet-equipment 38）。
- `check-no-game-assets.py`：`pass`（3936 個檔案，0 錯誤）。
- CI：待 PR 跑完。

## 未驗證什麼

- 沒有實機觀察；沒有啟動遊戲、伺服器或 DB。
- 37 封包裡沒有「穿上／脫下」方向，裝備欄 Equipped 位元以 S176 重新查詢為準（本模組只標過時，不猜）。
- 原版 `dword_1656934` 是否就是寵物背包窗的「目前寵物」仍是**推定**（packetbox-windows.md §2.1）；本模組不開窗、不畫畫面。

## 產出路徑

- `engine/source/PetEquipment.cs`、`engine/source/PetEquipmentTests.cs`、`engine/source/Pets.cs`（唯讀介面）
- `docs/reports/issue-611.md`

## 給下一棒的提示

- 寵物面板（PetUi／原生寵物背包窗）若要顯示 AC，改用 `World.PetAc(id)`；`PetBagStale(id)` 為真時可提示「裝備已變更，請重新查詢」，但**不要自動送 C103／C99**。
- 合併後由調度器依 CI 的 candidate lock 提交 `rebuild-lock.json`（新增 2 個來源檔）。
- 本機 worktree 建置需要 `engine/vendor` junction（已建，指向原專案 `engine/vendor`；拆除只能 `cmd /c rmdir`，不可遞迴）。
