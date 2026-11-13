# Issue #1050 — T465 移除已知例外：三個測試向量檔改為合成規則測試

分支：`task/1050`。依 `docs/reports/issue-102.md` 最後三列與 `docs/design/asset-recipe.md` 第 7 節（T465）。

## 做了什麼

1. **重新確認正常遊戲不讀這三個檔**（已驗證，靜態搜尋）
   - `engine/source`、`engine/audio`、`engine/web`、`engine/launcher` 與 dev 建置腳本（`rebuild.py`、`product-build.ps1`）沒有任何地方引用 `action-metadata-vectors.json`、`haste-metadata-vectors.json`、`expected-events.json`。
   - 產品 manifest 只收 `build/client-product-dev`、`web` 與 `assets/integrity.json`；`rebuild-lock.json` 也沒有這三個檔（不在 `source/audio/web` 底下）。
   - 唯一讀取者是 `engine/_integration/dev/combat-sfx-tests/Program.cs` 的 `issue47` 案例（讀 `expected-events.json`）。前兩個檔只有 r30–r33 封存的 `accepted-stage-seal.json` 與 `work/` 歷史紀錄提到，這些是封存的 SHA 清單，不會在執行時讀取，**沒有改**。
2. **CI 的合成排程規則測試**（新檔 `combat-sfx-tests/ScheduleRuleChecks.cs`，併進 `--selection` 模式，CI 的「Combat sound selection contracts」步驟會跑，不必改 `build.yml`）
   - 外觀 metadata 是測試裡編的（gfx 9001、聲音 id 個位數），執行時才寫到輸出目錄旁的 `synthetic/` 再用 `ObjectAppearance` 讀回。
   - 6 個案例：ticks 依 actionMs 比例分配、ticks 0 的格不進排程、`^` 格不播；全部有 milliseconds 時直接用毫秒、0ms 格略過、第 6 個候選丟掉；pure（index0 移到最後）／flight（60ms、循環）／breath 循環；慢速（整數 ms × 2）與加速（(3×ms)>>2）排程，並和 `ActionCadence.Frame`、`ObjectAppearance.At` 的畫面切換點對齊；cursor 每格只觸發一次、選中 0 不重抽；本機 oracle（見下）與 `SoundSchedule` 在合成資料上一致。
3. **需要真資料的對照改成選用**（`Program.cs` 完整模式）
   - 新檔 `combat-sfx-tests/LocalScheduleOracle.cs`：直接讀本機 player profile 的原始 JSON，自己掃 B07 聲音 token、自己累加時間，推導 expected；不經過被測的 `SoundSchedule`／`ObjectAppearance`。
   - `issue47` 案例改成：用 `PlayerProfiles.LoadManifest(engine/assets)` 列出本機所有 profile，逐個 attack／damage、8 方向，把 `SoundSchedule.Create` 和 oracle 逐幀比（index、時間、候選），並要求至少有一個有聲音的幀（避免空集合也通過）。原本固定的「62 個動作」數量改成以本機 manifest 為準。
   - `Case` 加上 `needsAssets`／`needsClient`：本機沒有 `engine/assets/players/manifest.json`＋`objects/manifest.json` 或沒有客戶端目錄時，該案例記為 `not-run`（附原因），輸出 JSON `status: "not-run"`、列出 `notRun`，主控台印 `NOT RUN …`，**結束碼 3**，不會顯示 PASS。
   - 原本在最上面直接 `new ObjectAssets(assets)`（沒有素材就整個崩潰）改成用到才載入。
4. **從 git 移除三個檔**，`engine/tools/game-assets-allowlist.json` 的 `b15Exceptions` 拿掉這三列（剩 `OriginalExpTable.cs`、`ground-catalog.json` 兩列；後者由 T464 處理）。
5. `run.ps1` 只改註解說明。

## 怎麼驗證

- `python -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "trackedFiles": 4712, "errors": 0, "embeddedResourceDeclarations": 2}`（已驗證，本機）。
- `dotnet build engine/source/OfflineClient.csproj -c Release`（輸出 `engine/build/client-product-dev`）與 `dotnet build CombatSfxChecks.csproj -c Release`：0 錯誤（已驗證，本機；只編譯，依規定沒有執行）。
- 合成測試的期望值是手算的（例：attack 0/75/225/450ms；慢速 0/166/498/998、總長 1330；加速 0/62/186/373、總長 497），實際通過與否**以 PR 的 CI 為準**。

## 未驗證什麼

- 合成測試在 CI 上是否全過：待 PR 的 CI 結果（本機不可執行測試程式）。
- 完整模式（有本機 assets 與客戶端）的 `issue47` 新比對：沒有在本機執行；要由 Codex（可執行測試的環境）以 `dotnet run --project engine/_integration/dev/combat-sfx-tests/CombatSfxChecks.csproj -c Release -- . <客戶端目錄> <輸出.json>` 跑一次，確認 PASS；也確認缺素材時印 `NOT RUN`、結束碼 3。
- 真機聽音（#52）不在本任務範圍。

## 產出路徑

- `engine/_integration/dev/combat-sfx-tests/ScheduleRuleChecks.cs`（新）
- `engine/_integration/dev/combat-sfx-tests/LocalScheduleOracle.cs`（新）
- `engine/_integration/dev/combat-sfx-tests/Program.cs`、`SelectionChecks.cs`、`run.ps1`（改）
- `engine/tools/game-assets-allowlist.json`（拿掉三列）
- 刪除：`engine/action-metadata-vectors.json`、`engine/haste-metadata-vectors.json`、`engine/_integration/dev/combat-sfx-tests/expected-events.json`
- 本報告：`docs/reports/issue-1050.md`

## 給下一棒的提示

- 合成資料只在執行時寫到輸出 JSON 所在目錄的 `synthetic/synthetic-appearance.json`（CI 是 `scratch/issue-433/synthetic/`），不進 git。
- 若 CI 合成案例失敗，先看是哪個 `Check` 名稱；期望值都在 `ScheduleRuleChecks.cs` 的註解裡有推算方式。
- 完整模式其他仍寫死的數值（技能 11722／221／167／2165 對應的聲音 id、weapon4 的 248／703／701、gfx1 的 1546／1547）不在這三個檔的範圍，沒有動；若之後要清，可比照 `LocalScheduleOracle` 改成本機即時推導。
- `docs/design/asset-recipe.md` 第 383 行的「7 列」現況與 `TASKS.md` 的 T465 狀態未改，留給維護 PLAN／TASKS 的 Claude 更新。
- r30–r33 `accepted-stage-seal.json` 仍列出兩個 vectors 檔名，那是封存 SHA 清單，不可改。
