# #1049 T464 移除已知例外：OriginalExpTable.cs 與 ground-catalog.json

日期：2026-11-13。依據：[`rule-tables.md`](../reference/asset-recipes/rule-tables.md)（T446）第 3、4 節；使用者裁定 **L1＝A**、**L2＝X**（T476／#1072，寫在 [`asset-recipe.md`](../design/asset-recipe.md) 第 7 節）。

## 結論

| 檔案 | 處理 | B15 例外 |
|---|---|---|
| `engine/source/Resources/ground-catalog.json` | **已移除**（L2＝X）：拿掉可丟棄白名單，能不能丟由伺服器決定；4 筆 SPR pin 搬進程式；丟棄前一律確認 | 拿掉 |
| `engine/source/OriginalExpTable.cs` | **保留為已知例外**（L1＝A，T446 判 blocked）：內容與 SHA 都沒動，理由寫進 allowlist | 保留 |

B15 例外數：這張開工時是 7 列，拿掉 ground-catalog 後是 6 列；同步 main 時 T463（#1048）已經拿掉 pickup 2 列，所以合併後是 **4 列**：`OriginalExpTable.cs`（已知限制 L1）與測試向量 3 列（T465，待處理）。`check-no-game-assets.py` 的輸出是 `"b15Exceptions": 4`，跟這裡的數字一致。

## 做了什麼

### 1. ground-catalog.json（L2＝X）

- **產品**（`engine/source/GroundCatalog.cs`）
  - `GroundDefinition` 只剩 `Id`、`GroundGfx`、`Sprite`。`TemplateId`、`NameToken`、`InventoryIcon`、`InventoryDesc`、`WireUseType`、`Stackable`、`InventoryBlesses`、`Source` 這些伺服器模板欄位全部拿掉，沒有搬進程式常數。
  - 4 筆 SPR pin（gfx 7、36、45、66）改放在 `GroundCatalog.Pinned`。每筆有 entry、Sprite.idx 筆號、PAK File Offset、大小、payload SHA-256、frame header offset（SPR payload 內的 File Offset）、bounds、RGBA SHA-256，出處 `docs/reports/issue-48.md`，都是從客戶端 `Sprite.pak` 量出來的雜湊與位置，不含像素。靜態建構子會檢查欄位完整。
  - `Find(gfx)`：有 pin 的 gfx 回傳 pin，其他回傳通用描述（`Sprite=null`）。原本的 22、42、46 三筆本來就沒有 pin，行為沒變。
  - `CanDrop` 拿掉 `Match(i)`、`InventoryBlesses`、`Stackable`，只留和模板無關的本機條件：物件 ID 不是 0、數量大於 0、沒有充能數、沒裝備、不是武器或防具、`Bless < 128`、旗標只有 bit0。
  - `Rows`、`Match`、`IndexSha256`，以及模板列的載入與檢查都已刪除。
- **丟棄前確認**（`GroundUi.cs`，新增 `ConfirmedDrop`）：原產品其實**沒有**丟到地上的確認視窗，只有給予（MessageBox）與刪除（聊天列 Y/N）有。裁定要求「所有物品一律經過確認」，所以這張補上：
  - WinForms 的「手動丟棄選取數量」鈕與 Web 面板的「手動丟棄」，都改走 `ConfirmedDrop`。
  - 數量合法、選取還有效、物品可丟時，跳出「確認丟棄物品」（`MessageBoxDefaultButton.Button2`，預設是「否」）。選「否」不送出，提示「未送出：已取消丟棄」。
  - 數量不合法或選取已過期時不跳視窗，直接交給既有的 `SendGroundQuantity` 顯示原本的錯誤訊息。
  - 判斷邏輯放在 `LoginConnection.NeedsDropConfirm`／`CancelDrop`，文字在 `GroundCatalog.DropConfirmPrompt`，這三個都能離線測。
  - 自動化腳本（`GroundDemo`）用 `dropConfirmAnswer` 接縫自動回答「是」，同時把確認文字記進 log。
- **伺服器拒絕**：沿用既有的 S71 訊息顯示（例如 125「你不能夠放棄此樣物品。」），沒有另外寫文字。
- **移除**：`git rm engine/source/Resources/ground-catalog.json`；`OfflineClient.csproj` 拿掉對應的 `EmbeddedResource`；`check-no-game-assets.py` 的 `RESOURCES` 與 `tools/ResourceChecks` 的核准清單同步拿掉 `P06.Resources.GroundCatalog.json`。
- **實機情境腳本**（`GroundIntegration.cs`、`GroundTransfer.cs`）：以前用「可丟棄」來找出那疊肉，白名單拿掉後這招不能用了，改成 `GroundScenario.ScenarioItem`：可丟棄，且名稱是 `$23`（`desc-c.tbl` 的名稱代碼）。只有真機時才用得到。

### 2. OriginalExpTable.cs（L1＝A，blocked）

- 程式不改，SHA `325384a1…bc198b1` 不變。
- `game-assets-allowlist.json` 新增 `b15ExceptionReasons`，這一列的理由是：伺服器規則值；客戶端的表在加殼執行檔的 `.data`，不在任何 pak；伺服器不送門檻。並指向 `asset-recipe.md` 第 7 節 L1 與 `rule-tables.md` 第 3 節。
- 沒有用常數或編碼字串把表藏起來。

### 3. CI 閘門（`engine/tools/check-no-game-assets.py`）

- 每個 `b15Exceptions` 都要在 `b15ExceptionReasons` 有理由，否則報錯。反過來，有理由但已經不是例外的路徑也報錯。
- 執行時逐列印出 `B15-EXCEPTION: <路徑>: <理由>`，輸出 JSON 增加 `b15Exceptions` 筆數。

### 4. 測試（合成資料）

- **新增** `engine/_integration/dev/ground-drop-tests/GroundDropChecks.cs`：`[CheckGroup("ground-drop")]`，同時掛在 CI 已經在跑的 `[CheckGroup("pickup")]`。涵蓋：
  - 4 筆 pin 的範圍與欄位，`GroundDefinition` 不含模板欄位。
  - 任意說明編號、圖示、名稱、bless 都可以丟；武器、防具、`Bless ≥ 128`、數量 0、有充能數、旗標異常、物件 ID 0 仍然擋。
  - 確認的判斷：數量合法才問；不合法、過期、沒有選取都不問；取消不送且保留選取。
  - 確認後送出的 C25 封包（物件 ID、數量）、送出不預扣、S71/125 拒絕後背包不變。
  - 武器類不問、也不送。
- **改用合成物品**：
  - `demo-tests` 新增 `SyntheticGroundItem.Stack`（`$9001`、gfx 9001、icon 9002、desc 9003、use 0），取代 `Program.cs`、`CombatLoopChecks.cs`、`HitReactionSuppressChecks.cs`、`pickup-tests/PickupChecks.cs` 裡的 `GroundCatalog.Rows.Single(r=>r.Id=="meat")`。
  - `engine/source/PointerFlowTests.cs` 也改成合成的 gfx 與名稱。
- **npc-tests**：肉改用合成值。金幣維持產品已經認得的協定識別（desc 0、icon 5、`$4`，同 `Castle.cs:159`），不是模板列。
- **ground-tests**（需要真客戶端，不在 CI 跑）：
  - 改用 `GroundTestItem`。
  - 白名單相關的斷言反過來寫：圖示、說明編號、名稱不同的物品也要能丟。
  - pin 測試改成走 `GroundCatalog.Pinned`。
  - `verify-sources.py` 改成解析 `GroundCatalog.cs` 的 4 筆 pin，逐欄和 `issue-48.md` 比對，並確認程式裡沒有 `templateId`、`etcitem`、`rowSha256`、`wireUseType`、`inventoryBlesses`。

## 怎麼驗證（已驗證）

- `dotnet build engine/source/OfflineClient.csproj -c Release`：0 警告、0 錯誤。
- 只編譯、沒有執行：`DemoChecks`（0 錯誤；3 個警告都在既有的 `DisplayNoticeChecks.cs`、`MacroChecks.cs`，不是這次改的檔）、`NpcChecks`、`GroundChecks`、`ResourceChecks` 都 0 錯誤。
- `python -B -X utf8 engine/tools/check-no-game-assets.py` → `status: pass`、`errors: 0`、`embeddedResourceDeclarations: 1`（只剩 `P24.Audio` 的 combat-sfx-pins；產品 DLL 已經沒有內嵌資源，`ResourceChecks` 的核准清單對應改成空集合）、`b15Exceptions: 4`，4 列理由逐列印出（同步 main 之後的結果）。
- `python -B -X utf8 engine/_integration/dev/ground-tests/verify-sources.py` → `PASS: 4 in-code SPR pins match #48, no server template fields in code, byte-exact #49 fixtures`。
- **CI 結果**（PR #1087，commit `f87f8061`）：`dotnet`、`map-continuity`、`npc-offline` 全部通過。`map-continuity` 這個 job 會跑 DemoChecks 的 `pickup` 群組，新增的 `ground-drop` 測試也在裡面。第一輪 `npc-offline` 失敗，原因是 `SellChecks.cs` 寫死了舊的肉圖示 124；改成合成物品的 9002 後通過。
- `rebuild-lock.json` 沒有改。

## 沒有驗證的（待驗證）

- **沒有在本機執行**任何測試程式（依本機規定）。`ground-drop`、`pickup`、`item-transfer` 等群組只靠 CI 跑。
- `ground-tests`（需要 `TW_CLIENT_ROOT` 真客戶端與 #49 fixture）不在 CI 跑，這輪也沒有跑。它驗的是 4 筆 pin 對真實 `Sprite.pak` 的解碼，以及 Web 與 renderer 的路徑。交給 Codex（本機 Windows）跑：`dotnet run --project engine/_integration/dev/ground-tests/GroundChecks.csproj -- <repo> <output> <client-root> <assets-root>`。
- **真機（T474）**：
  - 丟棄一般物品時要先出現確認視窗，按「是」才送。
  - 丟棄伺服器不允許的物品（例如不可交易或限時物品）時，畫面要顯示伺服器回的拒絕訊息。
  - 按「否」不送。
  - EXP% 顯示照舊（L1）。
- 原版客戶端在送出丟棄封包前，本機到底檢查什麼，仍然沒有反組譯（`rule-tables.md` 4.3），還是**推測**。

## 產出路徑

- 產品：`engine/source/GroundCatalog.cs`、`GroundUi.cs`、`GroundIntegration.cs`、`GroundTransfer.cs`、`PointerFlowTests.cs`、`OfflineClient.csproj`；刪除 `engine/source/Resources/ground-catalog.json`。
- 工具：`engine/tools/check-no-game-assets.py`、`game-assets-allowlist.json`、`ResourceChecks/Program.cs`。
- 測試：
  - 新增 `engine/_integration/dev/ground-drop-tests/GroundDropChecks.cs`、`demo-tests/SyntheticGroundItem.cs`。
  - 修改 `demo-tests/{Program,CombatLoopChecks,HitReactionSuppressChecks}.cs`、`pickup-tests/PickupChecks.cs`、`npc-tests/Program.cs`、`ground-tests/{Program,GenericGroundChecks}.cs`、`ground-tests/verify-sources.py`。
- 文件：`docs/design/asset-recipe.md`（第 7 節 L1、L2 的實作註記、B15 現況）、本報告。

## 給下一棒的提示

- **T471**：例外目標是 1 列（`OriginalExpTable.cs`）。`b15ExceptionReasons` 的機制已經在了（T463 的兩列理由在同步時已經刪掉）；T465 完成後，記得把對應的理由一起刪掉，否則腳本會報「reason recorded for a path that is not a B15 exception」。T471 要加的「新增任何例外即失敗」自我測試，可以直接比對 `b15Exceptions` 的鍵集合。
- **T479**（D1 三層驗證）：`GroundCatalog.Pinned` 的 pin 綁的是 `Sprite.pak` 的 PAK File Offset 與 idx 筆號（`GroundSpriteReader.Read` 只在第一個 archive 核對）。改成三層驗證時，這 4 筆的 offset／record 檢查要一起決定要不要放寬。
- **T473 README**：已知限制寫「能不能丟由伺服器決定，每次丟棄都會先確認」。
- **T474 真機**：回歸項目照上面「待驗證」的真機清單。
