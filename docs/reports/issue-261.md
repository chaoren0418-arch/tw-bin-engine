# Issue #261：T127 寵物與召喚物（第三輪）

## 結論

已依 2026-11-07 主控擴充裁定接通 S39，指定離線回歸全部通過，未跳過。本輪同步 `origin/main`（`9f62553cf8e1b1f3c8d26cb0c48e0dfd551d19ba`），保留 main 的好友／封鎖模組與分派，解決合併衝突及面板生命週期編譯衝突，再重新建置與驗證。PR #276 維持非草稿；新 head 的 CI 結果於推送後以 PR checks 核對並更新 PR 描述，不引用前輪成功狀態。

## 做了什麼

- `WorldSession.cs` 新增單一 S39 分支呼叫 `Pets.cs` 的 `ReceivePetDialog`；anicom／moncom 使用寵物 C旗標格式，其他保留原 reader 與 NPC／RaiseAttr 路徑。既有 S83／S176 掛接保留，`NpcUi.cs` 未修改。
- 接通 C34 詳細資訊查詢與 C104 成功後 S39 回覆。沿用寵物控制面板、背包、世界物件與 HP；依原服格式，C103 是背包查詢、控制指令走 C125。
- `PetChecks.cs` 經真正 `WorldSession.Receive` 驗證 anicom／moncom、數值更新、無效封包原子性、防具與武器 C104 → S250/37 → S39 → C103 → S176，以及一般 NPC、相似模板名稱、開啟中的 NPC 頁面、空模板關閉與 RaiseAttr。
- `Program.cs` 捕捉 Pets 例外後仍執行後續群組；最後寫 fail、退出 1，保留錯誤，不誤報 pass。
- merge 保留 main 的好友 S39／S250／S71 分派與測試，並保留寵物 S39／S83／S176。測試專案與入口保留雙方群組；dev lock 衝突先取 main，再用 `--update-lock` 重新產生，未手動拼接 hash。`docs/REPORT.md` 僅隨 main 合併，本任務未編輯索引。
- main 的 `BuddyUi.cs` 與寵物原本同時覆寫 `ClientForm.OnLoad`，造成 CS0111。只改 `PetUi.cs`，移到 `OnShown` 並防止重複掛接；新增 3 項無視窗回歸，驗證缺少面板、保留既有頁面及重複初始化。未修改好友模組。

## 怎麼驗證（已驗證）

快取、TEMP 與 CLI HOME 均在 worktree；scratch NuGet 設定唯讀引用原專案 `engine/vendor`。Python 使用本機 Python312/python.exe。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` 與唯讀核驗 | 186 來源／195 鎖定輸入通過 |
| `rebuild.py --build --configfile scratch/pets-nuget.config --output scratch/pets-build` | 產品、audio、launcher 成功；產品僅既有 MagicUi.cs 兩則 nullable 警告 |
| DemoChecks `pets` | 101 項通過 |
| DemoChecks `map-continuity` | 459 項通過，包含 pets／map／board |
| DemoChecks `combat-admission` | 379 項通過，包含 pets 與 main 的命中反應回歸 |
| DemoChecks `item-transfer` | 174 項通過，包含 pets |
| DemoChecks `buddies` | 270 項通過，包含 pets 與 main 的好友／封鎖回歸 |
| Pets 失敗仍執行後續群組 | 本輪 fixture 一度誤查未建構 Form 的 IsHandleCreated 而拋例外，仍跑完 map／board 共 458 項；JSON 為 fail、退出碼 1。修正為檢查實際控制項後重跑全綠，沒有跳過案例 |
| `check-no-game-assets.py` | pass，最終索引 3308 追蹤檔案、0 錯誤 |

入口：`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/pets-checks.json pets`；其他群組替換末尾參數及 JSON 路徑。SocketGuard 禁止建立網路連線。

來源 SHA-256 由 dev lock 鎖定；產品 DLL SHA-256：`ac0c274ec3b019a571e5d5baf741b7f9ae6df0895443efc2dfd13536abbaaf23`。

## 未驗證與已知限制

- 未啟動遊戲、伺服器或 DB；未驗證原生 UI、部署 JAR、實機操作與音效。
- main 既有 `GroundRules.IsGround` 存在格式重疊：owner 空白、HP255、朝向0且其餘可變欄位全零的 follower，會被視為零數量地面物件而移除。原服格式允許這些值；共用渲染／分類不在本輪授權範圍，沒有修改 Ground.cs。Follower 測試使用同樣合法的朝向3，驗證世界物件、隱藏HP及不可控制；不能據此宣稱所有 follower 顯示情況已解決。

## 產出路徑

- `engine/source/WorldSession.cs`；既有 `Pets.cs`、`PetUi.cs`
- `engine/_integration/dev/pets-tests/PetChecks.cs`、`README.md`
- `engine/_integration/dev/demo-tests/Program.cs`、`DemoChecks.csproj`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/pets.md`、本報告
- 不追蹤本機證據：`scratch/pets-build/`、`scratch/pets-pets.json`、`scratch/pets-map-continuity.json`、`scratch/pets-combat-admission.json`、`scratch/pets-item-transfer.json`、`scratch/pets-buddies.json`、`scratch/pets-failure-continuation.json`

## 給下一棒的提示

Claude 審查 PR #276 與新 head checks，使用者合併；原生 UI／登入／裝備與控制結果交 dot 的集中 smoke。調度器另評估 follower 與地面物件格式重疊的修復任務，不要把本張離線控制與資料測試當成全體世界顯示驗收。保留 worktree，由調度器關閉任務後清理。
