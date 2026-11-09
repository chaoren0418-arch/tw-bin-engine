# Issue #645 T270：測試與註冊表改為自動發現

分支：`task/645`（已同步 origin/main 到 `54316e9d`，含 #473 狀態圖示、#646 lock 政策）。

## 結論
- DemoChecks 的測試群組、DemoChecks 的原始檔清單、原生 UI 面板註冊表、巨集測試的面板篩選，全部改成**自動發現**。以後新增模組只要在**自己的檔案**加 attribute，不必再改 `demo-tests/Program.cs`、`DemoChecks.csproj`、`UiPanelRegistry.cs`、`MacroChecks.cs`。
- 現有註冊**全部遷移**，行為不變：67 個 DemoChecks 入口中，66 個在改動前後的結束碼與檢查數**完全相同**，唯一的差別是新增的 `auto-discovery` 群組。`--module-tests` 原有的 28 項測試數也一樣，只多了新的 `auto-discovery`（+28 項檢查）。
- `docs/design/m6-contract.md` 加了「自動發現」一節，並明定不要再改上述共用檔。

## 做了什麼
1. **DemoChecks 測試群組**（`engine/_integration/dev/demo-tests/AutoDiscovery.cs`，新檔）
   - `[CheckGroup("名稱",Order=n)]` 標在 static `void`／`Task` 方法上，一個方法可以屬於多個群組。另有兩個特殊群組：`CheckGroups.Default` 是不帶群組時跑的全套，`CheckGroups.Preamble` 是每次都先跑的隊伍、寵物、變身基本組；寵物組失敗會延後回報，與原本相同。
   - `Program.cs` 的 `Main` 從 80 行的 `switch` 改成一行 `CheckGroups.Run(...)`。命令列不變：`<repo> <out.json> [群組]`；群組名不存在時一樣丟 `Unknown regression group`，結束碼 1。
   - 參數依名稱繫結：`repo`／`root` 對應 args[0]，`output` 對應 args[1]，`[CheckArg(n)]` 對應 args[n]，`Action<bool,string>` 對應 `Check`。原本 `switch` 呼叫的 76 個方法（含 main 新加的狀態圖示 2 個）各加一行 attribute，用 `Order` 保留每個群組原本的執行順序。`buddies-codec` 在 `BuddyChecks.cs` 加了一行包裝方法；`attack-skill-resources` 的兩個參數改標 `[CheckArg(3)]`、`[CheckArg(4)]`。
2. **DemoChecks.csproj**：拿掉 50 行手寫的 `<Compile>`，改成一條規則：`../*-tests/` 底下**沒有自己 `.csproj`** 的資料夾，裡面所有 `.cs` 都編進 DemoChecks。用 `dotnet msbuild -getItem:Compile` 比對，改前與改後都是同樣 62 個檔案、逐一相同；合併 main 之後，新加的 `BuffIconsChecks.cs` 會自動納入。
3. **原生 UI 面板**（`engine/source/AutoDiscovery.cs`，新檔）
   - `[UiPanel(順序)]` 標在 static 屬性、欄位或方法上，型別為 `UiPanelRegistration` 或 `IEnumerable<UiPanelRegistration>`。`UiPanelRegistry.Panels` 改由反射掃描產生，API 不變。
   - 原本 13 筆註冊（合併 main 後加上狀態圖示共 14 筆）各移到自己面板的檔案（各加一行），順序以 10 為間隔：左狀態 10 … 選單按鈕 130，main 新加的狀態圖示放在 140。好友三窗只在既有的 `Registrations` 加 attribute。`PersonalShopPanel.Registration` 原本就沒註冊，維持不加。
   - 不合規的註冊（Id 重複、型別錯、空白、取值丟例外）不會生效，會記在 `UiPanelRegistry.Errors`。
   - `[ExcludeFromDiscovery]` 讓測試夾具不被組件掃描到。
4. **巨集註冊表**（`MacroChecks.MacroRegistry`）：原本每加一個 uistatus86 面板就要改一次排除清單，現在改成用完整合成版面**逐一試建**，`UiLayoutException` 的就排除。結果第一次算出來後快取，partial 與真客戶端的建構都沿用同一組。
5. **測試**
   - `engine/source/AutoDiscoveryTests.cs`（`[WorldModuleTest("auto-discovery")]`，28 項）：註冊無錯誤、Id 唯一、遷移前的 16 個面板 Id各只註冊一次且相對順序不變、掃描結果與反射列舉順序無關、夾具驗證排序與 5 種錯誤。
   - `demo-tests/AutoDiscoveryTests.cs`（群組 `auto-discovery`，扣掉每次都跑的 327 項 Preamble 後是 114 項）：64 個舊群組名都還在、各群組舊有的呼叫順序不變、Preamble 正確且寵物組延後回報；夾具驗證參數繫結、排序、Preamble 不重跑、未知群組、簽章錯誤；也驗證巨集探測選出的面板與舊排除清單一致。
6. **文件**：`docs/design/m6-contract.md` 新增「自動發現」一節（註冊方式對照表、DemoChecks 群組規則、面板順序表、仍未自動化的地方），第 2 條註明 #645 起改用自動發現；lock 的說法依 #646 的政策（PR 不改 lock）。

## 怎麼驗證（本機 Windows，皆為合成資料，不啟動遊戲、伺服器、DB）
- `dotnet build OfflineClient.csproj`：0 錯誤。`rebuild.py --build`：**PASS**（依 #646 使用暫時 lock，`rebuild-lock.json` 與 main 相同、未修改）。
- `check-no-game-assets.py`：pass（3913 個追蹤檔，0 錯誤）。
- `--module-tests`：main 為 28 個測試、1452 項檢查，本分支為 **29 個測試、1480 項檢查**，全部通過；原有 28 個測試的逐項檢查數完全相同。
- DemoChecks 全部入口：用 `git archive origin/main` 解到 scratch，建出 main 版本當基準，與本分支逐群組執行比對（腳本 `scratch/issue-645/run-groups.ps1`）。

| 群組 | main（改動前） | 本分支（改動後） |
|---|---|---|
| (預設全套) | 失敗(exit 1)※ | 失敗(exit 1)※ |
| timed-buffs | 645 | 645 |
| afflictions | 580 | 580 |
| resurrection | 553 | 553 |
| char-reset | 488 | 488 |
| clan | 446 | 446 |
| clan-emblem | 363 | 363 |
| clan-war | 530 | 530 |
| portals | 427 | 427 |
| doors | 648 | 648 |
| pickup | 369 | 369 |
| door-approach | 361 | 361 |
| party | 327 | 327 |
| pets | 327 | 327 |
| display-notices | 失敗※（CI 環境：388） | 失敗※（CI 環境：388） |
| name-display | 414 | 414 |
| map-continuity | 失敗※（CI 環境：33314） | 失敗※（CI 環境：33314） |
| marriage | 463 | 463 |
| yes-no | 452 | 452 |
| trade-yes-no | 354 | 354 |
| ghost-mode | 721 | 721 |
| ghost-limits | 362 | 362 |
| fishing | 366 | 366 |
| location-share | 462 | 462 |
| player-query | 1058 | 1058 |
| hypertext | 357 | 357 |
| character-config | 455 | 455 |
| hotbar | 569 | 569 |
| polymorph | 327 | 327 |
| polymorph-cadence | 380 | 380 |
| buddies | 496 | 496 |
| buddies-codec | 442 | 442 |
| minimap | 398 | 398 |
| ambience | 9881 | 9881 |
| map-ambient | 372 | 372 |
| background-music | 434 | 434 |
| invisibility | 397 | 397 |
| bookmarks | 774 | 774 |
| board | 585 | 585 |
| item-transfer | 400 | 400 |
| combat-loop | 391 | 391 |
| cast-cadence | 349 | 349 |
| combat-admission | 1750 | 1750 |
| title-emote | 553 | 553 |
| title-emote-assets | 492 | 492 |
| title-emote-compose | 341 | 341 |
| duel | 441 | 441 |
| attack-skills | 1110 | 1110 |
| attack-skill-resources | 失敗※※ | 失敗※※ |
| hit-reaction-suppress | 393 | 393 |
| slow-actions | 691 | 691 |
| item-status | 409 | 409 |
| native-ui-resources | 539 | 539 |
| native-ui-canvas | 429 | 429 |
| native-ui-left-status | 370 | 370 |
| native-ui-macro | 403（真客戶端：405） | 403（真客戶端：405） |
| native-ui-hpmp | 397 | 397 |
| native-ui-party | 396 | 396 |
| native-ui-chat-window | 446 | 446 |
| native-ui-menu-buttons | 355 | 355 |
| native-ui-minimap | 402 | 402 |
| native-ui-buff-icons | 381 | 381 |
| keyboard | 487 | 487 |
| busy | 377 | 377 |
| world | 333 | 333 |
| auto-discovery（新） | 不存在 | 441 |
| no-such-group | Unknown regression group | Unknown regression group |

※ 本機有原客戶端（`D:\lineage開發\0-客戶端`），字串表讀得到，`notice S245 unknown message stays labeled` 這項會失敗；main 也一樣，CI 沒有客戶端所以是綠的。把 `TW_CLIENT_ROOT` 指向空資料夾模擬 CI 後，display-notices（388）與 map-continuity（33314，CI 會跑）兩邊都通過且數字相同。預設全套在兩種環境下，兩邊都卡在同一個環境問題（有客戶端時是 S245；指向空資料夾時，選用的真客戶端檢查會開 Tile 而失敗）。它的組成另由 `auto-discovery` 驗證：預設 49 項、map-continuity 28 項，成員與順序都和舊的 `switch` 一致。
※※ 需要命令列第 3、4 個引數（真客戶端、素材路徑）；沒給時兩邊都失敗，與原本相同。
- 每個群組的檢查數都包含每次都跑的 Preamble 327 項（隊伍、寵物、變身基本組）。

## 未驗證什麼
- CI（GitHub Actions）結果要等 PR 開出後確認。
- 預設全套在本機環境無法完整跑完（前後相同，原因如上），未能取得完整總數。
- 沒有啟動產品視窗看原生 UI 的實際畫面（不在範圍；面板清單與順序已用測試核對）。

## 產出路徑
- 新檔：`engine/source/AutoDiscovery.cs`、`engine/source/AutoDiscoveryTests.cs`、`engine/_integration/dev/demo-tests/AutoDiscovery.cs`、`engine/_integration/dev/demo-tests/AutoDiscoveryTests.cs`
- 改動：`demo-tests/Program.cs`、`demo-tests/DemoChecks.csproj`、`NativeUi/UiPanelRegistry.cs`、`native-ui-tests/MacroChecks.cs`；各模組只加 attribute（59 個測試檔、13 個面板檔）；`docs/design/m6-contract.md`
- 本機證據（不入 git）：`scratch/issue-645/`（before2／after2 的 summary.json、compare2.json、module-tests JSON）

## 給下一棒的提示
- **請排在合併佇列第一**：這張 PR 每個測試檔、面板檔都加了一行。其他 PR 同步 main 時：
  - `Program.cs`、`DemoChecks.csproj`、`UiPanelRegistry.cs` 衝突時一律**取 main 版本**，再把自己的新增改成 attribute：測試用 `[CheckGroup]`，面板用 `[UiPanel(順序)]`，寫法見 m6-contract「自動發現」。
  - 只加了 `<Compile Include>` 的 csproj 衝突可以直接丟掉自己那邊，萬用字元會自動納入。
- 進行中的 PR 如果新增了 DemoChecks 群組（例如 `case "xxx":`），合併後要在方法上補 `[CheckGroup("xxx")]`；要進預設全套或 map-continuity，就加 `CheckGroups.Default`／`"map-continuity"` 並挑空號 `Order`。沒補的話，該群組名會變成 `Unknown regression group`，CI 若有跑那個群組就會擋下。
- 新面板沒加 `[UiPanel]` 就不會出現在產品裡；Id 重複時，`--module-tests` 的 `auto-discovery` 會失敗。
- 還沒自動化的地方：`UiClientBridge.cs` 的 `NativeUiGameState()` 狀態包裝鏈（新的 `IUiGameState` 裝飾層仍要改這裡，#473 就改了它）、`.github/workflows/build.yml` 的群組步驟、產品 `Program.cs` 舊的 `--xxx-tests` 診斷旗標。建議另開一張任務，把狀態包裝鏈也改成註冊式。
- `docs/design/native-ui.md` §5.1 與 `docs/reference/gap-matrix.md` 仍寫著「`UiPanelRegistry.cs` 加一行」，已過時（不在本任務可改範圍），建議下次改文件時一併更新。
- rebuild-lock 依 #646 由調度器在合併後重產；本 PR 新增 2 個、修改 14 個 `engine/source` 檔。
