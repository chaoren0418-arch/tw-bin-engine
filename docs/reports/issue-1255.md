# Issue #1255：分流 #1246（f1211-retest-fixture）的 ⚠️ 項目

結論：#1246 是 fixture 準備任務，結果 ✅ 7／❌ 0／⚠️ 3。**三個 ⚠️ 都是伺服器資料不支援，不是程式缺陷，也不是可以靠準備補齊的環境問題**，所以列為已知限制。本張不開修正任務。
重測已經有 #1248（f1211-retest，`depends_on` 含 #1246、#1255），範圍涵蓋這些 SM 列，因此**不另開重測任務**（另開會跟 #1248 重複，等於原樣重跑）。我改了 #1248 的內文：只重測哪些項目、用哪個 DB 起點、這三條已知限制，都寫清楚了。`docs/plans/issue-1255.json` 是空陣列。

## 做了什麼
1. 讀了 #1246 的 RESULT 與留言，還有 `origin/task/1246:docs/reports/issue-1246.md`（`32bf867f`，PR #1254 尚未合併）和該分支的 `work/f1211-smoke/fixtures.json`。
2. 對照 `docs/testing/smoke-m7.md` SM13（`:180–184`、§14.2 `:767`）與 SM21（`:259–264`），確認每一列實際要驗什麼。
3. 靜態查了 470 的引擎覆蓋：`engine/source/Q8FollowupTests.cs:14–18` 的 `[WorldModuleTest("q8-vip470-icon")]` 已經驗證「vipFlag 0 → offIcon」（`BuffIconsPanel.IconFor`），由 CI 執行。
4. 查了 open issue 內文有提到 1246 的：只有 #1248 和本張（見下方第 5 點）。
5. 用 `gh issue edit 1248 --body-file` 改了 #1248：C 組 SM13、SM21 兩句換成縮小後的範圍，文末加上「#1255 分流更新」一節。前面的原文沒有改。

## 已通過（✅）的項目：之後不另外重測準備
SM13-82（碎片 49553 → S250/82）、SM20-map509（在線召回落點）、SM21-修理（損壞匕首 1900264602）、SM21-學法（煙騎甲／吉倫）、SM58-1461、SM58-1395、SM40（短弓＋兩種箭）。
這些是**資源條件**已經成立，不代表 SM 功能通過。功能判定照原本的規劃由 #1248 做，用 #1246 的 ready 快照當起點。本張沒有改任何程式，所以不需要附帶回歸。

## 每個 ⚠️ 的歸屬
|#1246 項目|分類|處理|理由與證據（相對 #1246 證據根目錄 E）|
|---|---|---|---|
|SM13-6400／PC咖啡|(c) 已知限制 K1|#1248 只測 82 的圖示與 tooltip|smoke 步驟原文是「到 6400 地圖（**或**有 PC 咖啡效果時）看 82」，6400 只是觸發 82 的其中一種方法。伺服器沒有 map6400（`maps/6400.txt` 不存在），也沒有 effect293 的來源（`catalog3.txt`、`none-time-callers.json`）。82 已經改用碎片 49553 穩定觸發，所以這一列要驗的東西不缺條件。|
|SM13-470|(c) 已知限制 K2|#1248 不測|skills、藥水、娃娃表都沒有 470；JAR 裡沒有正常送出這個 effect 的路徑（`icons.tsv`、`VIP.javap.txt`、`none-time-callers.json`）。合成封包冒充不行（規則 5）。引擎「470 畫 offIcon」的邏輯已由 module test `q8-vip470-icon` 覆蓋。|
|SM21-出售|(c) 已知限制 K3（只限成交）|#1248 仍要點「賣出」，記錄送出的 C 封包和伺服器實際的回應與畫面|全表 1631 筆 shop 的 `purchasing_price` 都是 −1，`ShopTable` 只把 ≥0 的列加進收購清單（`catalog5.txt`、`ShopTable.javap.txt`、`L1Shop.javap.txt`）。要讓它成交，就得改 shop 表，這超出 #175 授權的寫入範圍（GM、測試角色、測試物品），本張不開這個寫入任務。|

伺服器不支援的判定，依據是 #1246 讀伺服器 JAR 位元碼與 DB 的結果，不是猜測。

## 依賴任務的處理
- **#1248 f1211-retest**：不關閉，它涵蓋 45 列 SM，範圍比 #1246 大很多。`depends_on` 不改：沒有新的修正任務可以換；#1246 已關閉，#1255 會在本 PR 合併後關閉。內文補了三件事：(1) 前置：PR #1254 必須先合併，三個快照目錄必須已由調度器保全；(2) 一定要從 `issue-1246-ready` 開服；(3) K1–K3 不測，以及 #1246 各條件的用法（目標不要登出、`.poly` 目標必須在線、低 HP 先補血）。

## 怎麼驗證
- 用 Python 讀 #1248 原文：確認兩句被替換的字串各只出現一次，再寫回；`gh issue edit` 成功，回傳了 issue URL。
- `docs/plans/issue-1255.json` 是合法 JSON（空陣列）。

## 未驗證
- 沒有執行任何程式或測試。`q8-vip470-icon` 是否通過，以 CI 為準（推測：main 已經綠燈）。
- 潘朵拉在收購清單為空時，伺服器實際送什麼封包、引擎怎麼顯示，待 #1248 實機記錄。

## 產出路徑
- `docs/reports/issue-1255.md`（本檔）、`docs/plans/issue-1255.json`（`[]`）。
- GitHub：#1248 內文已更新。

## 給下一棒的提示
- **調度器（needs:orchestrator）**：#1246 已經結案，但它的 worktree `scratch\wt\task-1246\environments\p24\backups\` 裡的 `issue-1246-before`、`issue-1246-ready`、`issue-1246-evidence` 還沒保全到主專案（主專案目前沒有 `issue-1246-ready`）。**清理 task-1246 worktree 之前，請先保全這三個目錄**，並合併 PR #1254；#1248 的前置就是這兩件事。
- 如果使用者之後想驗出售成交，需要另外授權「在 P24Lab 隔離副本改 shop 表 1 列收購價」，再開一張 resource 任務。
