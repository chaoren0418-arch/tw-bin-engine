# Issue #259 — T125 組隊（code，第 1 次）

## 結論

**模組已實作並可建置，整合未完成，PR 應維持 draft。** 任務限定 `WorldSession.cs` 只能增加 S237 case 與 Yes-No 分支，但本分支沒有隊伍 S250、S39、S71 的掛接點。已提出一個前置 `ReceiveParty(opcode,plain)` 掛接的範圍確認，尚未收到確認，沒有越過限制修改共用接收鏈。模組測試通過不等於隊伍實際可用。

## 做了什麼

- `engine/source/Party.cs`：C230（0／1 模式＋目標）、C33、C43、C255、C121/953；隊員、HP、位置、隊長、邀請與錯誤解析；送出前的 session／epoch／revision 驗證，沒有預先宣告加入、離隊或踢除成功。
- `engine/source/PartyUi.cs`：原生隊伍面板、具名 HP 條、未知 HP、邀請選取玩家、分配模式、接受／拒絕、更新名單、離開、逐員踢除。`PlayerHudView.cs` 只新增一個掛載呼叫。
- `WorldSession.cs`：S237 在既有戰鬥接收器之前觀察，保留戰鬥模組原有 actor／target 更新；S219 僅接管 953 與清除被其他 Yes-No 取代的舊邀請。
- `engine/_integration/dev/party-tests/PartyChecks.cs`：合成封包與記憶體 transport 測試，編入既有 `demo-tests/DemoChecks.csproj`，既有測試入口每次都執行，另有 `party` 選擇器。既有 CI 的 map-continuity／combat-admission 會執行這組模組檢查。
- 封包與唯讀來源 SHA-256：`docs/protocol/party.md`。更新 dev lock，未改封存 lock。

## 已驗證／怎麼驗證

1. 使用 SDK 10.0.300；先 `rebuild.py --update-lock` 再 `rebuild.py --build --configfile scratch/issue-259/NuGet.Config --output scratch/issue-259/build`，產品與 launcher 建置通過。僅有 `MagicUi.cs:20` 原有的 CS8600／CS8602 兩個警告。
2. 本機 Python 使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`；PATH 無 python，py launcher 未列出安裝。NuGet HTTPS 在沙箱回報 SSL 認證失敗；暫存 config 唯讀指向原專案 `engine/vendor`，`NUGET_PACKAGES` 指向本 worktree `engine/.nuget`，沒有寫入原專案。
3. `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-259/party-checks.json party`：53 項檢查通過。S237／S219 走產品 `WorldSession.Receive`；S250／S39／S71 尚以反射直接呼叫模組解析器，未冒充接收鏈已完成。包含每個本模組 C／S 格式、Big5 名稱、零 HP／255、錯誤長度／padding／重複／超額名單、未知 ID、異世界名單、接受／拒絕／離隊／踢除、逾時、切圖、重播、並行送出與斷線取消排隊。
4. `check-no-game-assets.py` 通過；CRLF 來源依既有位元格式保留，以 `git -c core.whitespace=cr-at-eol diff --check` 檢查空白。
5. GitHub CI 狀態以 PR checks 為準；報告初稿時尚待推送後執行。

## 未驗證什麼

- S250／S39／S71 接入真實接收鏈尚未完成；不能將此版交給 dot 做功能驗收或視為 T125 完工。
- 同角色重新登入前未觀察離線狀態的隊伍資料清理，仍需接收鏈進入世界事件或明確 Reset 掛接；目前取消後讀取快照會清除。
- 未啟動遊戲、伺服器或 DB，未連正式服務；未作原生畫面、兩角色組隊、HP 同步、踢除／離隊及部署伺服器行為驗收。
- 104 排除新加入者與伺服器 removeMember 疑點，只是來源證據；部署實際影響待 dot smoke，詳見協議文件。

## 產出路徑／給下一棒的提示

追蹤產出：`Party.cs`、`PartyUi.cs`、兩個最小共用掛接、`party-tests/PartyChecks.cs`、demo-tests 入口、dev lock、`docs/protocol/party.md`、本報告。詳細本機 log 位於未追蹤的 `scratch/issue-259/`。

請主控／調度器裁定是否允許在 `WorldSession.ReceiveCore` 的既有接收鏈前，將目前 `if(opcode==219&&ReceiveParty(opcode,plain))return;` 改成 `if(ReceiveParty(opcode,plain))return;`。解析器自行辨識隊伍的 S250/104/105/106/110、S219/953、S39/party、S71/414–426；其他封包回傳 false，不消耗共用 reader；S71 保留原有訊息處理。前置呼叫也會在 EnterGame 之前清除上一個角色的殘留狀態。這是一行具體待審修改，不需要使用者進行真機或 DB 操作。

裁定後由 codex-local 套用掛接，把 `PartyFeed` 改成直接 `Feed`、補同角色重登／多隊員／共用分派回歸，更新 lock、重建與重跑測試，再解除 draft。Claude 審查、使用者合併；dot 於 M6 smoke 依既有授權完成實機驗收。
