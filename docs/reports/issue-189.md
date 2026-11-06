# Issue #189／T102：NPC 出售與私商回購

## 做了什麼

基準 `69cd94a4` 已有 NPC 出售與私商回購的基本封包／UI 路徑；本次補齊出售清單驗證、權威裝備判斷、過期保護與操作提示，未將既有功能重算為新實作。

- NPC：在既有 `ReceiveNpcService` 掛點接收 S65 收購清單，保留物件 ID 與原服單價，從權威背包補名稱、圖示與可售數量。未持有／已裝備項目停用；多列出售檢查數量、重複 ID、總價與背包／裝備快照，報價 30 秒失效。沿用 C125 `sell` 與 C161/type1 的 D(物件 ID)、D(數量)。送出後不可重複提交，物品與金幣只由既有背包解碼器更新。
- 私商回購：沿用 C47/type1、S140/type1 與 C161/type1；保留一筆報價對應多個背包物件的處理。出售上限取持有量、原訂收購量、24 位數量上限、20 億總價上限的最小值；改採 `ItemEquipped` 的權威裝備狀態。查詢開始便保存快照，等待中改變背包／裝備時，舊回包不再成為可提交清單；沿用未結案查詢的連線隔離政策。
- UI：顯示「收購單價」、「向店主出售（查詢回購）」、「確認出售一次」與「預估收入」。私商清單數量標成「原訂收購量」，不宣稱為剩餘需求；未提供貨幣種類時仍明示未知。
- 生產程式只改指定四檔；沿用共用接收／送出掛點，未改 `WorldSession.cs`、`NpcInteraction.cs` 或網頁檔。更新的僅為 dev 滾動 lock。
- 第二次補件：依主控與審查要求，以 `f98387a` 合併 `origin/main`（`1f2bc53`），納入 M6 契約。將前次 scratch 的 82 項新增斷言原樣整合至 `engine/_integration/dev/npc-tests/SellChecks.cs`；既有 `Program.cs` 改為 partial 並在預設入口呼叫 `SellTests()`。沿用 `NpcChecks.csproj`，不複製原有測試／另建重複專案。
- `.github/workflows/npc-checks.yml` 的 `npc-offline` 在每次 PR／main push 建置產品 DLL 後執行上述入口，任一斷言失敗即非零退出，並上傳含所有斷言名稱的 `engine/build/npc-checks.json`。原有 socket guard 也涵蓋新增測試。

## 怎麼驗證（已驗證）

第一輪先查 #132 的 `server-protocol.md`／`server-rules.md`，再唯讀核對 `C_Result.java`、`C_ShopList.java`、`S_PrivateShop.java`、`S_ShopBuyList.java`；四檔 SHA-256 均與表內相符。特別核對 `C_Result.mode_sellpc` 的 CH(數量)、C(order)、20 億總價檢查，以及 `S_PrivateShop` 的 H(報價數) 不等於實際輸出的背包匹配列數。未複製伺服器原始碼。第二輪重讀對照表，以下為合併 main 後重新執行的結果。

| 驗證 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 172 個來源、181 個鎖定輸入通過 |
| `rebuild.py --build --configfile scratch/issue-189/NuGet.Config --output scratch/issue-189/build` | product、audio、launcher 建置成功；product 0 錯誤，原有 `MagicUi.cs:20` 的 CS8600／CS8602 各一項；launcher 0 警告 |
| `engine/_integration/dev/npc-tests/NpcChecks.csproj` | 合併後 DLL 共 165 項斷言通過（原有 83＋本次 82）；使用已納入 git／CI 的測試入口，合成封包／記憶體 sender／禁止 socket 的 guard；未開 UI |
| `check-no-game-assets.py` | pass：3228 tracked files、0 errors、4 embedded declarations（已含新增測試） |
| `git -c core.whitespace=cr-at-eol diff --check` | 通過；保留原檔 CRLF／LF，未做全檔換行轉換 |

新增向量涵蓋 NPC 多列出售、私商多個持有堆疊、C125／C47／C161 位元組、零／超量／重複／不持有物件、裝備與數量競態、報價逾時、目標移除、畸形及晚到回包、24 位數量和非零收購序號、20 億總價上限、S57／S24 分別更新物品與收入。另驗證其他角色的外觀更新不會誤使出售清單失效。

本輪本機 product DLL SHA-256：`20d73f01f930feaac8bbf0a2da0b7f1cbf140c34ca10c89cbd8e77abd4c53cf6`。合併後重新執行 `--update-lock` 與核驗均通過，重新產生的 dev lock 與自動合併結果相同，未手動拼接 hash。CI 狀態以 PR #201 的最新 GitHub Checks 為準，CI 不等於原生驗收。

## 未驗證什麼

未啟動遊戲、伺服器或 DB；未驗實機畫面、實際 NPC 合法報價、店主餘款／剩餘需求與成交後重登持久化。伺服器來源 SHA 不代表已核對部署 JAR。NPC 舊協議無 request ID，未宣稱能區分關閉後再次查詢同一 NPC 的所有晚到回包；30 秒報價期限是客戶端保守策略，並非伺服器承諾。

歷史說明：第一輪開工時尚未取得 `docs/design/m6-contract.md`，因此新增測試曾只留在 scratch。契約已由 PR #194 合併；本輪同步 main 後已讀取並遵循第 3 點，把測試提交至既有模組目錄及回歸入口，不再有契約缺件。未擴寫共用分派點；`docs/REPORT.md` 僅承接 main 既有內容，本任務未編輯。

## 產出路徑／給下一棒的提示

- 程式：`engine/source/{NpcServices,NpcUi,PrivateShop,PrivateShopUi}.cs`；lock：`engine/_integration/dev/rebuild-lock.json`。
- 報告：`docs/reports/issue-189.md`。
- 已提交測試：`engine/_integration/dev/npc-tests/{Program.cs,SellChecks.cs}`，由既有 `NpcChecks.csproj` 編譯；CI：`.github/workflows/npc-checks.yml`。新增測試檔 SHA-256：`383f89b1891456cb815e95dccc49abab63703c57b93415099d95fa7de3dcf8ce`，與前次 scratch 版本逐 byte 相同。
- 本輪本機證據：`scratch/issue-189/build/result.json`、`scratch/issue-189/npc-checks.json`。舊 `scratch/issue-189/tests/` 與 `sell-checks.json` 僅為前輪證據，已不是重跑入口。
- 重跑：先完成上述 dev 建置，再 `dotnet restore engine/_integration/dev/npc-tests/NpcChecks.csproj --configfile scratch/issue-189/NuGet.Config`，接著 `dotnet run --project engine/_integration/dev/npc-tests/NpcChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- scratch/issue-189/npc-checks.json`。乾淨 checkout 可用 `.github/nuget.ci.config` 取代本機 vendor 設定，並以 `engine/build/npc-checks.json` 存結果，與 CI 相同。
- 本機 Python 使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`；暫存、NuGet cache、CLI home 全放本 worktree 的 scratch，原專案 vendor 僅唯讀。未拆除 worktree／junction，未執行遞迴刪除。
- Claude 審查 PR #201 的補件與 CI；dot 在 M6 smoke 準備可收購物品的 NPC 與合法收購私商，依序驗「取得清單 → 選數量 → 提交一次 → 物品減少／收入增加 → 重登仍一致」，並驗裝備、超量、售罄與店主離場。
