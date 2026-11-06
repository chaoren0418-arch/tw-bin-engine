# Issue #189／T102：NPC 出售與私商回購

## 做了什麼

基準 `69cd94a4` 已有 NPC 出售與私商回購的基本封包／UI 路徑；本次補齊出售清單驗證、權威裝備判斷、過期保護與操作提示，未將既有功能重算為新實作。

- NPC：在既有 `ReceiveNpcService` 掛點接收 S65 收購清單，保留物件 ID 與原服單價，從權威背包補名稱、圖示與可售數量。未持有／已裝備項目停用；多列出售檢查數量、重複 ID、總價與背包／裝備快照，報價 30 秒失效。沿用 C125 `sell` 與 C161/type1 的 D(物件 ID)、D(數量)。送出後不可重複提交，物品與金幣只由既有背包解碼器更新。
- 私商回購：沿用 C47/type1、S140/type1 與 C161/type1；保留一筆報價對應多個背包物件的處理。出售上限取持有量、原訂收購量、24 位數量上限、20 億總價上限的最小值；改採 `ItemEquipped` 的權威裝備狀態。查詢開始便保存快照，等待中改變背包／裝備時，舊回包不再成為可提交清單；沿用未結案查詢的連線隔離政策。
- UI：顯示「收購單價」、「向店主出售（查詢回購）」、「確認出售一次」與「預估收入」。私商清單數量標成「原訂收購量」，不宣稱為剩餘需求；未提供貨幣種類時仍明示未知。
- 生產程式只改指定四檔；沿用共用接收／送出掛點，未改 `WorldSession.cs`、`NpcInteraction.cs` 或網頁檔。更新的僅為 dev 滾動 lock。

## 怎麼驗證（已驗證）

先查 #132 的 `server-protocol.md`／`server-rules.md`，再唯讀核對 `C_Result.java`、`C_ShopList.java`、`S_PrivateShop.java`、`S_ShopBuyList.java`；四檔 SHA-256 均與表內相符。特別核對 `C_Result.mode_sellpc` 的 CH(數量)、C(order)、20 億總價檢查，以及 `S_PrivateShop` 的 H(報價數) 不等於實際輸出的背包匹配列數。未複製伺服器原始碼。

| 驗證 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 172 個來源、181 個鎖定輸入通過 |
| `rebuild.py --build --configfile scratch/issue-189/NuGet.Config --output scratch/issue-189/build` | product、audio、launcher 建置成功；product 0 錯誤，原有 `MagicUi.cs:20` 的 CS8600／CS8602 各一項；launcher 0 警告 |
| 原有 `npc-tests/NpcChecks.csproj` | 83 項斷言通過 |
| `scratch/issue-189/tests/SellChecks.csproj` | 最終 DLL 共 165 項斷言通過（原有 83＋本次 82），合成封包／記憶體 sender／禁止 socket 的 guard；未開 UI |
| `check-no-game-assets.py` | pass：3211 tracked files、0 errors、4 embedded declarations |
| `git -c core.whitespace=cr-at-eol diff --check` | 通過；保留原檔 CRLF／LF，未做全檔換行轉換 |

新增向量涵蓋 NPC 多列出售、私商多個持有堆疊、C125／C47／C161 位元組、零／超量／重複／不持有物件、裝備與數量競態、報價逾時、目標移除、畸形及晚到回包、24 位數量和非零收購序號、20 億總價上限、S57／S24 分別更新物品與收入。另驗證其他角色的外觀更新不會誤使出售清單失效。

最終本機 product DLL SHA-256：`4238dff0fdabaa32d851075cf2048c843211290a855678e84bcfb322fa5692ad`。CI 狀態以本 PR 的 GitHub Checks 為準，CI 不等於原生驗收。

## 未驗證什麼

未啟動遊戲、伺服器或 DB；未驗實機畫面、實際 NPC 合法報價、店主餘款／剩餘需求與成交後重登持久化。伺服器來源 SHA 不代表已核對部署 JAR。NPC 舊協議無 request ID，未宣稱能區分關閉後再次查詢同一 NPC 的所有晚到回包；30 秒報價期限是客戶端保守策略，並非伺服器承諾。

任務引用的 `docs/design/m6-contract.md` 在 worktree、原專案與當時的 origin/main 均不存在，GitHub 也無待合併 PR 可讀取；本次按 Issue #189 明列規則執行。未擴寫共用分派點或修改 `docs/REPORT.md`。

## 產出路徑／給下一棒的提示

- 程式：`engine/source/{NpcServices,NpcUi,PrivateShop,PrivateShopUi}.cs`；lock：`engine/_integration/dev/rebuild-lock.json`。
- 報告：`docs/reports/issue-189.md`。
- 本機證據與測試：`scratch/issue-189/{build/result.json,npc-checks.json,sell-checks.json,tests/}`。新增測試依四檔限制留在 scratch，未加入 git／CI；清理 worktree 前若需長期保留，請另授權納入既有模組測試目錄。
- 重跑擴充測試：先完成上述 dev 建置，再 `dotnet run --project scratch/issue-189/tests/SellChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- scratch/issue-189/sell-checks.json`。本機 Python 使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`；暫存、NuGet cache、CLI home 全放本 worktree 的 scratch，原專案 vendor 僅唯讀。
- Claude 審查本 PR；調度器補齊 M6 合約供後續對照。dot 在 M6 smoke 準備可收購物品的 NPC 與合法收購私商，依序驗「取得清單 → 選數量 → 提交一次 → 物品減少／收入增加 → 重登仍一致」，並驗裝備、超量、售罄與店主離場。
