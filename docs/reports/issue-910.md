# Issue #910 報告：T395／R10-K 拾取通行判斷

依據：`docs/reference/gap-matrix.md` 15.12.5（5:`sub_5001C0` 列）、15.12.6 R10-K；原文核對 `docs/reference/followups-8.md` 第 6 節（Q6-7，已驗證）與 `docs/reference/world-objects.md` 第 2 節。

## 做了什麼

`engine/source/Ground.cs`：

- `GroundRules.PickupStepClear(x,y,tx,ty,step)`：物品在腳下時直接通過（原版 `sub_F12490` 只在「不同格」時呼叫）；不同格時用 `PickupHeading`（對應 `sub_A40EC0` 的方向）查自己往物品那一步。
- `GroundRules.LootTarget` 加選用參數 `step`：被擋的物品跳過、繼續找下一個（WO§2：「第一個在 1 格內、類別 9、數量非 0、沒被牆擋的物品」）。不給 `step` 時行為與舊版相同。
- `WorldSession.PickupStep`／`PickupReachable`：引擎對應 `sub_F12490` bit 0＝「門覆蓋（S209 `doorBlockedTiles`）＋地圖屬性（`MapCache.Passable`，沒有 map cache 時 `MovementRules.Passable`）」。與 `ApproachWalk.RawStep` 同來源，但**不含**「有物件站在那格」這一項（原版判斷只讀格子旗標）。
- 套用位置：
  - 點擊拾取：`PickupArrived`（1 格內直接點，與走近到達後都經過這裡），在開數量提示**之前**檢查（WO§2 步驟 1 在步驟 3 之前）。
  - F4 鍵盤拾取：`LootNearby` 用帶 `step` 的 `LootTarget`；1 格內有物品但全部被擋時提示「被地形或門擋住」，而不是「1 格內沒有」。
  - 共用送出 `SendGround`（拾取）：距離檢查之後再查一次，涵蓋數量回答（`AnswerPickupPrompt`）與 `PickupSelected`。
- 被擋的提示：`LoginConnection.StepBlockedNotice`＝「未送出：往物品方向那一格被地形或門擋住」；被擋時不轉向、不送、不排隊、不動節流。
- 離線測試座：`WorldSession.PickupTerrain`（internal）；沒有 map cache 而且 `assets/terrain0.bin` 不存在（CI）時只套門覆蓋，避免既有拾取測試在 CI 讀檔失敗。產品一定帶 `terrain0.bin`，所以產品行為不受這個退路影響。

新檔 `engine/source/LootPassabilityTests.cs`（`[WorldModuleTest("loot-passability")]`，自動發現）：純規則（腳下不查、方向＝PickupHeading、跳過被擋的）、點擊（被擋不送不轉向、不開數量提示、數量回答也擋、腳下物品四周全擋仍可撿、`PickupSelected` 與 `PickupArrived` 同樣檢查）、F4（跳過被擋的撿下一個、全擋時的提示）、門（合成 S209 旗標 1 擋、旗標 0 放行、斜向相鄰格是門也擋）。

## 怎麼驗證

- `python engine/tools/check-no-game-assets.py`：pass（4454 個追蹤檔、0 錯誤）。
- 編譯與單元測試：交給 PR 的 GitHub Actions CI（`--module-tests` 會跑所有 `[WorldModuleTest]`，含本組）。依本機執行限制，沒有在本機執行任何編譯出來的程式。
- **本機編譯沒有成功，原因與本次改動無關**：本機 `dotnet build source/OfflineClient.csproj` 在 restore 後解析 `Microsoft.Web.WebView2 1.0.4258.31` 時退回 .NETFramework 資產（NU1701），然後 NETSDK1130（直接參考 `.winmd`）。改用 `.github/nuget.ci.config`、或還原到 `scratch/nuget-910` 的全新套件目錄，結果都一樣；`rebuild.py --build` 用離線 vendor 時 restore 也失敗。main 上同樣的專案檔在 CI（windows-latest）可以建置（例如 PR #928 的 map-continuity 通過），所以這是本機 SDK／環境問題，本 PR 不處理。

## 未驗證什麼

- 真機：門關／門開時撿門後物品、牆角斜向拾取。待驗證（交給 Codex 在 P24Lab 做）。
- 斜向（奇數方向）細節：原版 `sub_F12490` 斜走時查兩組相鄰格；引擎沿用 `MapCache.Passable`／`DoorBlocks` 既有的斜向規則（FU8§6 的「引擎對應」寫的就是用同一個入口），兩者是否逐格一致沒有另外比對。
- 數量回答（`sub_500A00`）原版是否在送出前再查一次牆：沒有原文。引擎是因為共用 `SendGround` 而一起擋（推測，偏保守）。
- 走近（模式 3）時，`ApproachRules.Plan` 可能停在「1 格內但往物品那一步被擋」的格子（例如隔牆）；這時到達後會被本檢查擋下並提示，不會改走別的格子。原版尋路是否會避開這種終點：待驗證。

## 產出路徑

- `engine/source/Ground.cs`
- `engine/source/LootPassabilityTests.cs`
- `docs/reports/issue-910.md`

## 給下一棒的提示

- gap-matrix 5:`sub_5001C0` 列在 CI 綠後可以從 K5 改成「已有（真機待驗證）」；真機項目建議加進 15.12.7 清單：「關著的門後放物品，點擊與 F4 都不撿；開門後可撿」。
- 本機 WebView2 建置問題若要處理，請另開 `needs:local`／resource 任務（檢查 SDK 10.0.300 對 `net10.0-windows` 的 WebView2 資產選擇），不要在功能 PR 裡改專案檔。
