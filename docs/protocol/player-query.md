# 玩家查詢協定（Issue #357／T140）

2026-11-07；已唯讀核對 `docs/reference/server-protocol.md`、`server-rules.md`、本機來源及 P24Lab d287 JAR。以下是靜態協定證據，不代表原生驗收。

## 已核定格式與實作

欄位包含 opcode；C／H／D 為 1／2／4 bytes，H／D 小端序；S 為 CP950、NUL 結尾。客戶端使用既有 `LoginPackets.Pad` 補零至 8 bytes 倍數。

### 查詢玩家：`/who`

| 封包 | 格式 | 行為 |
|---|---|---|
| C206 `C_Who` | C206 S(name) | `/who 名字`；`/who` 送空 S，查詢線上資訊。查無名字也會走線上資訊分支，不能自行顯示「離線」。 |
| S71 `S_WhoCharinfo` | C71 H166 C1 S(text) | 伺服器已組合稱號、名字、善惡與血盟；聊天區顯示。 |
| S39 `S_NPCTalkReturn` | C39 D(self) S("y_who") H1 H9 S×9 | 依序為線上人數、經驗倍率、掉寶倍率、金錢倍率、手點能力上限、單項萬能藥上限、萬能藥總瓶數、目前時間、重啟時間；以九行純文字顯示。 |
| S161 `S_ServerMessage(String)` | C161 C0 D0 S(text) | C_Who 也送出此封包。沿用既有 Inventory／HUD 資訊顯示，不新增 S161 分派。 |

**S71／166 不等於玩家資訊。** 166 是伺服器的泛用「%0」訊息，來源有 352 處 `new S_ServerMessage(166, …)`，封包形狀與 `S_WhoCharinfo` 相同。`S_WhoCharinfo` 依 lawful 的三個區間一定帶 `($1501)`／`($1502)`／`($1503)` 其中之一，本模組只把含這些代碼的文字當玩家資訊；其他 166 文字，以及參數數量不是 1 的 166，一律交回既有 Inventory 分派。第一版把所有 166 都攔下並標成玩家資訊，參數數量不符時還會拋例外，本版已修正並加入測試。

### 排名：`/rank`（不需要 NPC）

| 封包 | 格式 | 行為 |
|---|---|---|
| C125 `C_NPCAction` | C125 D(self) S("lv_ranking") | 物件 ID 是玩家自己。`WorldInteractionGuard.npcAction` 允許 `target == pc`，接著進 `L1ActionPc.action`。 |
| S39 `S_NPCTalkReturn` | C39 D(self) S("y_h_2") H1 H11 S×11 | #0–9 為名次（空位是一個空白），#10 是「全職業」。顯示於聊天區，不開 NPC 對話。 |

這是全職業等級榜（`RankingHeroTimer.userNameAll`），伺服器每 10 分鐘更新。

### 風雲榜 NPC 回覆鏡射

四個 NPC 走既有 NPC 對話（C34 D(npc)、C125 D(npc) S(action)），本模組不改送出端，只把結果同步顯示到聊天區。只有在該 NPC 的對話正等待回覆時才鏡射；封包仍交給既有對話處理，所以對話視窗照常開啟。

| NPC（npcid） | 動作 | 回包（皆 `C39 D(npc) S(html) H1 H(n) S×n`） |
|---|---|---|
| 英雄風雲榜（80029） | `y_h_1` 選單；action `c/k/e/w/d/g/i/a` | `y_h_2`，n=11，#10 是職業標題 |
| 殺手風雲榜（80026） | `y_k_1` 選單；action `1`／`2` | `y_k_2`（殺人榜）／`y_k_3`（往生榜），n=10，名次為 `名字,次數` |
| 財富風雲榜（80027） | 對話即回覆 | `y_w_1`，n=10，只有名字 |
| 血盟風雲榜（80028） | 對話即回覆 | `y_c_1`，n=20，#0–9 為 `血盟(數值)`，#10–19 是宣戰選項 |

`y_c_1` 的 #10–19 不是名次，不顯示在聊天區。該頁的 action `0`–`9` 是**對血盟宣戰**，本模組不會送出；玩家只能在既有對話視窗自行點選。數值欄位的意義未核對，原樣顯示，不加單位。參數數量不符時不當成排名，交回一般對話。

### 查看玩家資訊：`/check`

先選取目標，再輸入 `/check`。伺服器是兩段式，第二段要等第一段確認：

| 順序 | 封包 | 格式 | 行為 |
|---|---|---|---|
| 1 | C125 `C_NPCAction` | C125 D(self) S("pc_list") | `PcScannerHandler` 進入選取模式。 |
| 2 | S236 `S_SelectTarget` | C236 D(self) C0 C0 C2 | 引擎沒有此 opcode 的分派，不處理。 |
| 3 | S71 `S_ServerMessage` | C71 H166 C1 S("請選擇要查詢的玩家。") | 排在 S236 之後，代表伺服器已進入選取模式；本模組收到才送下一步，且此提示不再往下分派。 |
| 4 | C223 `C_SelectTarget` | C223 D(self) C0 D(target) | 第一個 D 必須是自己，第二個 D 是目標 objid。 |
| 5 | S39 `S_NPCTalkReturn` | C39 D(self) S("pc_show") H1 H29 S×29 | #0 名字、#1 等級、#2 HP、#3 MP、#4 AC、#5 MR、#6–11 六圍（力敏體智精魅）；#12 武器、#13–20 頭盔／盔甲／內衣／斗篷／手套／長靴／盾牌／項鍊、#21–24 戒指、#25–26 耳環、#27 腰帶、#28 臂甲。空欄不顯示。 |

失敗回覆為 S71／166 的「目標不存在。」「只能對玩家查詢狀態。」「查詢失敗。」；會顯示在聊天區，也保留既有的伺服器訊息路徑。

本機逾時 3 秒（本機復原政策，不是伺服器規定）：沒收到提示就停止，不補送、不重送。提示收到時，世代、角色或選取目標已改變，也不送 C223。此時伺服器可能仍留在選取模式，下一個 C223（寵物指定目標）會被吃掉一次；這是伺服器行為，本模組無法清除。

### 共用規則

本機名字上限採 52 字，與密語對象輸入上限一致，這是客戶端政策而非聲稱伺服器上限。拒絕控制字元、名字內空白及 CP950 不可編碼字元。命令不分大小寫；`/rank`、`/check` 不接受參數；`/say /who 名字` 仍為一般聊天。

送出共用 `SendPlayerChat` 的世界就緒、角色存活、session／world／actor 世代、排隊再驗證與一秒節流；送出不生成成功結果。伺服器 `checkNpcActionSpam` 的 0.3 秒間隔由這個一秒節流涵蓋。共用來源僅修改 PlayerChat 的 `SendPlayerChat` 一行，以及 WorldSession 的 S39／S71 各一個分支。

自身 S39 只在世界就緒且收件物件為自身時顯示；其他 HTML（包含特殊 `RaiseAttr`）不動。先完整解析與檢查 padding，再追加聊天。沿用聊天的 100 行限制及重設機制。回包沒有 request ID，顯示的是目前連線收到的伺服器資訊，不能宣稱與某一筆請求一一配對。

## 任務描述與實際協定的落差

任務原文的兩個封包不是查詢。本模組不送它們，改用上面已核對的真實入口達成同一目標。

| 指定封包 | 本機來源及 d287 JAR 的實際含義 | 實際入口 |
|---|---|---|
| C99 `C_Check` | C99 D(checkType) + 剩餘 UTF-8 bytes；防外掛檢查，只作 DEBUG 記錄，沒有回包。D 不是目標 objid。 | `/check` 走 `pc_list` → C223 → `pc_show`。 |
| C63 `RESTARTMENU`／`RANK` | 同一 opcode 分派 `C_Rank`，別名並不代表兩種查詢子型別。 | `/rank` 走 `lv_ranking` → `y_h_2`。 |

C63 的兩種實際資料形狀：

- subtype 1：`C63 C1 C(rank) S(name)`，會修改線上或離線血盟成員階級，rank 接受 2–10；並非讀取排名。
- 其他 subtype：只讀 `C63 C(subtype)`。2／3／4 為未實作同盟分支；5 生存吶喊會改 HP／飽食，6 播效果；8 查詢地圖剩餘時間（S71／2535），9 為地圖計時資訊。0 的舊血盟查詢程式已註解；0／7／未知值走未處理分支。不得把 C63 當作 C177 重生，也不得送 subtype 1 當無副作用查詢。

`C_Windows` 的 0x65 會呼叫 `ClassicRankingService`，但 d287 JAR 沒有任何 `ClassicRanking*` 類別（見 `source-vs-jar.md`），不可使用。

`C_Who` 的線上資訊受 `ALT_WHO_COMMANDX`／`ALT_WHO_TYPE` 條件影響；未讀取實際設定。未啟用 HTML 時，不保證回傳線上人數；缺值不可自行補零。人數是伺服器計算（含倍率與虛擬人物）的顯示值，不推測為實際真人數。

## 待驗證

- **風雲榜 NPC 可能不在世界中**：`1-伺服器/db/380_db.sql` 有四個 NPC 的 `npc` 定義，`server_event` 的 `Ranking4` 為啟用，但沒有 80026–80029 的 `spawnlist*` 資料列。未查詢 P24Lab DB，實際是否生成未知。`/rank` 不依賴 NPC。
- `lv_ranking`、`pc_list` 是伺服器「小幫手」介面的動作，協定上用自身 objid 即可送出；`L1ActionPc` 在這些分支之前還有變身選單、GM 選單等前置處理，實際回應要靠真機確認。
- 來源的 `pc.isGhost()`、`isDead()`、`isTeleport()`、`isPrivateShop()` 會無回應丟棄 C125；`/rank` 此時沒有結果，`/check` 會走本機逾時。

## 來源識別

根目錄：`D:/lineage開發/1-伺服器/src/com/lineage/`。只讀取，未複製 Java 本文。

| 相對路徑 | SHA-256 |
|---|---|
| server/clientpackets/C_Who.java | `862ccf400889bba1d73d9ac956abd2eeffeb9db5ebf7bb807283b616aab575c0` |
| server/clientpackets/C_Check.java | `eeca0bd74d5bbcf6a50ac1b33855640984eca0ee626e0f237162fe871f938d34` |
| server/clientpackets/C_Rank.java | `0e284028348f160ed06a9241e0ec36ec5e4b0127f3f42dc2368773be76e17e14` |
| server/clientpackets/C_NPCAction.java | `280ef4bb7316ca61e199ae97a71405ec45affc37a8b4955c4881a891b298b0a1` |
| server/clientpackets/C_SelectTarget.java | `d6ac219ee87d0a89762db9efb799d23e22d65374bfbf82b91d245c91510ec141` |
| server/serverpackets/S_SelectTarget.java | `501d17863698d9d4773e3a9eb5ad3af73cf3b4dbe1ce20a237f1f81e3df3f76b` |
| server/serverpackets/S_WhoCharinfo.java | `31cc89b1056970582818228273883dfdeacdd57666fb7c688d96c38c2aee5f15` |
| server/serverpackets/S_NPCTalkReturn.java | `bfc54936df47f821b54a6acedbe273225e4d53a2b247f21ecf2ce35c653a5847` |
| server/serverpackets/S_ServerMessage.java | `6f2ae74bc885da8d7807c816b1e139629cb14924ca38e176a44f031d26bcaf92` |
| server/model/L1ActionPc.java | `3e0817f8d1ef7d5a044b79ef6a5f58f773f4dec7dcc665af8e2f327ca01162cd` |
| server/model/action/PcScannerHandler.java | `688b4d90e47e2ce1ad05ca33ace51318b66495eec609d72a669198757e415927` |
| data/npc/quest/Npc_HeroRanking.java | `4a2eb6bf26f207571cee86a8b9026e23398ae1ed0b1ae2aa5118599bd8af499e` |
| data/npc/quest/Npc_KillerRanking.java | `3c7417989ab76b7256134dc8c2f944b29e9e83cd0c7dc9826273f5d7b8b3926b` |
| data/npc/quest/Npc_WealthRanking.java | `34faa4e266bfd056204ca515288048e6d401df266b1228057d7519ca8f0ada73` |
| data/npc/quest/Npc_ClanRanking.java | `fcc9c92794cecdaa505e84dbc4d17be487e2ed3086d86c1c666e4c4acfa02205` |

`D:/lineage開發/1-伺服器/db/380_db.sql`，SHA-256 `2afbc066fa7dbde391169050deeed206a20a39afbc0390378ddf2800cd3328b5`：只讀取文字，未連線 DB。

JAR：`D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。以 `javap -p -c` 唯讀核對：C_Who、C_Check、C_Rank 的讀取／分支／回包；S_WhoCharinfo、S_ServerMessage、S_SelectTarget 的欄位序列；四個 `Npc_*Ranking` 的 htmlid、動作與陣列長度；`L1ActionPc` 的 `lv_ranking`；`PcScannerHandler` 的 `pc_list`、`pc_show`、29 欄；`C_SelectTarget` 的 D／C／D 與選取模式分支。提示與三個失敗字串以 UTF-8 比對 class 常數。未執行 JAR。S39 serializer 格式依來源與既有引擎 parser 交叉核對；不宣稱整份來源與 JAR 本文完全一致。

原生驗收由 dot 在獲授權的 P24Lab smoke 執行。
