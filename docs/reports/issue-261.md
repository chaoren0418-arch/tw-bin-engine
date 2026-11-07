# Issue #261：T127 寵物與召喚物（第三輪）

## 結論

第 1～3 項在前一輪已完成，本輪（同步輪）在分支上確認仍然有效；缺口是分支落後 main 且有衝突。已同步 `origin/main`（`d604050` 以後），解掉 4 個檔案的衝突與 1 個合併後才出現的編譯衝突，重產 dev lock，重新建置並跑完下表的離線回歸，全部通過、未跳過。PR #276 維持非草稿；新 head 的 CI 以推送後的 PR checks 為準，不引用前輪狀態。

## 做了什麼

- `WorldSession.cs` 新增單一 S39 分支呼叫 `Pets.cs` 的 `ReceivePetDialog`；anicom／moncom 使用寵物 C旗標格式，其他保留原 reader 與 NPC／RaiseAttr 路徑。既有 S83／S176 掛接保留，`NpcUi.cs` 未修改。
- 接通 C34 詳細資訊查詢與 C104 成功後 S39 回覆。沿用寵物控制面板、背包、世界物件與 HP；依原服格式，C103 是背包查詢、控制指令走 C125。
- `PetChecks.cs` 經真正 `WorldSession.Receive` 驗證 anicom／moncom、數值更新、無效封包原子性、防具與武器 C104 → S250/37 → S39 → C103 → S176，以及一般 NPC、相似模板名稱、開啟中的 NPC 頁面、空模板關閉與 RaiseAttr。
- `Program.cs` 捕捉 Pets 例外後仍執行後續群組；最後寫 fail、退出 1，保留錯誤，不誤報 pass。
- merge 保留 main 的好友 S39／S250／S71 分派與測試，並保留寵物 S39／S83／S176。測試專案與入口保留雙方群組；dev lock 衝突先取 main，再用 `--update-lock` 重新產生，未手動拼接 hash。`docs/REPORT.md` 僅隨 main 合併，本任務未編輯索引。
- main 的 `BuddyUi.cs` 與寵物原本同時覆寫 `ClientForm.OnLoad`，造成 CS0111。只改 `PetUi.cs`，移到 `OnShown` 並防止重複掛接；新增 3 項無視窗回歸，驗證缺少面板、保留既有頁面及重複初始化。未修改好友模組。

### 本輪同步（第 4 項）

- `WorldSession.cs`：main 新增 `case 39:if(ReceivePlayerQueryHtml(plain))return;break;`，與寵物分支是重複的 case 標籤，所以併成同一行 `case 39:if(ReceivePetDialog(plain)||ReceivePlayerQueryHtml(plain))return;break;`。兩者都只認自己的 htmlid（anicom／moncom；y_who／pc_show／排行模板），其他回傳 false 繼續走原本的 NPC／RaiseAttr 路徑。main 的天氣、光線、隱身、位置分享、角色設定分派全部保留。
- `PetUi.cs`：main 的 `LocationShare.cs` 也覆寫了 `OnShown`，再度造成 CS0111（`OnLoad` 已屬 `BuddyUi`）。改用 `OnActivated` 掛接，不動其他模組；掛接本身冪等，玩家開面板前視窗必定已啟用。`PetChecks.cs` 的生命週期檢查同步改呼叫 `OnActivated`。
- `Program.cs`：Pets 的 try／catch 保留，之後接 main 的 `PolymorphChecks()` 與新群組；`DemoChecks.csproj` 保留雙方測試檔。
- `rebuild-lock.json`：衝突取 main 版本後用 `--update-lock` 重新產生，未手動拼 hash。

## 怎麼驗證（已驗證）

快取、TEMP 與 CLI HOME 均在 worktree；scratch NuGet 設定唯讀引用原專案 `engine/vendor`。Python 使用本機 Python312/python.exe。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` 與唯讀核驗 | 194 來源／203 鎖定輸入通過 |
| `rebuild.py --build --configfile scratch/pets-nuget.config --output scratch/pets-build` | 產品、audio、launcher 成功；產品僅既有 MagicUi.cs 兩則 nullable 警告 |
| DemoChecks `pets` | 191 項通過（Pets 101 項＋每次都會跑的 Polymorph 90 項） |
| DemoChecks `map-continuity` | 21359 項通過 |
| DemoChecks `combat-admission` | 1210 項通過 |
| DemoChecks `polymorph-cadence` | 224 項通過 |
| DemoChecks `buddies` | 360 項通過 |
| DemoChecks `item-transfer` | 264 項通過 |
| DemoChecks `player-query`（驗證合併後的 S39 分派） | 892 項通過 |
| DemoChecks `character-config`／`location-share`／`ambience`／`invisibility` | 284／290／547／261 項通過 |
| NpcChecks | 165 項通過 |
| Pets 失敗仍執行後續群組 | 本輪暫時在 `Pets()` 開頭注入例外實測：後續 Polymorph 與 `board` 跑完共 320 項，JSON 為 fail、退出碼 1；注入已還原，`pets` 重跑通過 |
| `check-no-game-assets.py` | pass，3354 追蹤檔案、0 錯誤 |

入口：`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/pets-pets.json pets`；其他群組替換末尾參數及 JSON 路徑。SocketGuard 禁止建立網路連線。

來源 SHA-256 由 dev lock 鎖定；產品 DLL SHA-256：`a6b6308b8d77ca7d71c80e40a3cbb6651e99b3dd0b31879b03e60d3bbc75be80`。

## 未驗證與已知限制

- 未啟動遊戲、伺服器或 DB；未驗證原生 UI、部署 JAR、實機操作與音效。
- `OnActivated` 掛接只用無視窗 fixture 驗過，寵物分頁實際出現在真視窗的時機待 dot 的 smoke 確認。
- DemoChecks 不帶群組參數的完整跑法未跑完：worktree 沒有 `engine/assets`（`terrain0.bin`），`Movement123` 讀檔失敗；這條路徑不在 CI，也未為此建立 junction。
- main 既有 `GroundRules.IsGround` 存在格式重疊：owner 空白、HP255、朝向0且其餘可變欄位全零的 follower，會被視為零數量地面物件而移除。原服格式允許這些值；共用渲染／分類不在本輪授權範圍，沒有修改 Ground.cs。Follower 測試使用同樣合法的朝向3，驗證世界物件、隱藏HP及不可控制；不能據此宣稱所有 follower 顯示情況已解決。

## 產出路徑

- `engine/source/WorldSession.cs`、`PetUi.cs`；既有 `Pets.cs`
- `engine/_integration/dev/pets-tests/PetChecks.cs`、`README.md`
- `engine/_integration/dev/demo-tests/Program.cs`、`DemoChecks.csproj`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/pets.md`、本報告
- 不追蹤本機證據：`scratch/pets-build/`、`scratch/pets-<群組>.json`、`scratch/pets-npc-checks.json`、`scratch/pets-failure-continuation.json`

## 給下一棒的提示

`ClientForm` 的 `OnLoad`／`OnShown` 已被其他模組占用，且 override 只能有一個；之後新增面板的任務若再用這些掛接點會撞 CS0111，建議另開任務提供共用的面板註冊點。PR #276 依新 head checks 處理；原生 UI／登入／裝備與控制結果交 dot 的集中 smoke。調度器另評估 follower 與地面物件格式重疊的修復任務，不要把本張離線控制與資料測試當成全體世界顯示驗收。保留 worktree，由調度器關閉任務後清理。
