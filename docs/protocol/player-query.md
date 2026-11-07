# 玩家查詢協定（Issue #357／T140）

2026-11-07；已唯讀核對 `docs/reference/server-protocol.md`、`server-rules.md`、本機來源及 P24Lab d287 JAR。以下是靜態協定證據，不代表原生驗收。

## 已核定格式與實作

欄位包含 opcode；C／H／D 為 1／2／4 bytes，H／D 小端序；S 為 CP950、NUL 結尾。客戶端使用既有 `LoginPackets.Pad` 補零至 8 bytes。

| 封包 | 格式 | 行為 |
|---|---|---|
| C206 `C_Who` | C206 S(name) | `/who 名字`；`/who` 送空 S，查詢線上資訊。查無名字也會走線上資訊分支，不能自行顯示「離線」。 |
| S71 `S_WhoCharinfo` | C71 H166 C1 S(text) | 伺服器已組合稱號、名字、善惡與血盟；聊天區顯示。只解讀 `($1501)`／`($1502)`／`($1503)` 為正義／中立／邪惡，不推測等級、裝備或排名。 |
| S39 `S_NPCTalkReturn` | C39 D(self) S("y_who") H1 H9 S×9 | 依序為線上人數、經驗倍率、掉寶倍率、金錢倍率、手點能力上限、單項萬能藥上限、萬能藥總瓶數、目前時間、重啟時間；以九行純文字顯示，不依賴 NPC HTML 素材。 |
| S161 `S_ServerMessage(String)` | C161 C0 D0 S(text) | C_Who 也送出此封包。沿用既有 Inventory／HUD 資訊顯示，不新增 S161 分派；不把字串建構子誤當 S71。 |

本機名字上限採 52 字，與密語對象輸入上限一致，這是客戶端政策而非聲稱伺服器上限。拒絕控制字元、名字內空白及 CP950 不可編碼字元。命令不分大小寫；`/say /who 名字` 仍為一般聊天。

送出共用 `SendPlayerChat` 的世界就緒、角色存活、session／world／actor 世代、排隊再驗證與一秒節流；送出不生成成功結果。共用來源僅修改 PlayerChat 一個指令編碼分派點，以及 WorldSession 的 S39／S71 各一個分支。

S71 僅攔截訊息 166，避免玩家資訊誤觸既有 Inventory 的交易／NPC 回覆處理。其他訊息仍走原分派。S39 僅攔截 `y_who`，且世界就緒、收件物件為自身才顯示；其他 HTML（包含特殊 `RaiseAttr`）不動。先完整解析與檢查 padding，再追加聊天。沿用聊天的 100 行限制及重設機制。回包沒有 request ID，顯示的是目前連線收到的伺服器資訊，不能宣稱與某一筆請求一一配對或代表排名。

## 任務描述與實際協定的落差

| 指定封包 | 本機來源及 d287 JAR 的實際含義 | 本任務處理 |
|---|---|---|
| C99 `C_Check` | C99 D(checkType) + 剩餘 UTF-8 bytes；防外掛檢查，只作 DEBUG 記錄，沒有玩家資訊回包。D 不是目標 objid。 | 不實作為玩家查詢；`/check` 明確告知不支援，零封包。 |
| C63 `RESTARTMENU`／`RANK` | 同一 opcode 分派 `C_Rank`，別名並不代表兩種查詢子型別。 | 不臆造排行榜請求；`/rank` 明確告知尚無核定協定，零封包。 |

C63 的兩種實際資料形狀：

- subtype 1：`C63 C1 C(rank) S(name)`，會修改線上或離線血盟成員階級，rank 接受 2–10；並非讀取排名。
- 其他 subtype：只讀 `C63 C(subtype)`。2／3／4 為未實作同盟分支；5 生存吶喊會改 HP／飽食，6 播效果；8 查詢地圖剩餘時間（S71／2535），9 為地圖計時資訊。0 的舊血盟查詢程式已註解；0／7／未知值走未處理分支。不得把 C63 當作 C177 重生，也不得送 subtype 1 當無副作用查詢。

`C_Who` 的線上資訊受 `ALT_WHO_COMMANDX`／`ALT_WHO_TYPE` 條件影響；未讀取實際設定。未啟用 HTML 時，不保證回傳線上人數；缺值不可自行補零。人數是伺服器計算（含倍率與虛擬人物）的顯示值，不推測為實際真人數。

## 來源識別

根目錄：`D:/lineage開發/1-伺服器/src/com/lineage/server/`。只讀取，未複製 Java 本文。

| 相對路徑 | SHA-256 |
|---|---|
| clientpackets/C_Who.java | `862ccf400889bba1d73d9ac956abd2eeffeb9db5ebf7bb807283b616aab575c0` |
| clientpackets/C_Check.java | `eeca0bd74d5bbcf6a50ac1b33855640984eca0ee626e0f237162fe871f938d34` |
| clientpackets/C_Rank.java | `0e284028348f160ed06a9241e0ec36ec5e4b0127f3f42dc2368773be76e17e14` |
| serverpackets/S_WhoCharinfo.java | `31cc89b1056970582818228273883dfdeacdd57666fb7c688d96c38c2aee5f15` |
| serverpackets/S_NPCTalkReturn.java | `bfc54936df47f821b54a6acedbe273225e4d53a2b247f21ecf2ce35c653a5847` |
| serverpackets/S_ServerMessage.java | `6f2ae74bc885da8d7807c816b1e139629cb14924ca38e176a44f031d26bcaf92` |

JAR：`D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。以 JDK 17 `javap -p -c` 唯讀核對 C_Who、C_Check、C_Rank 的讀取／分支／回包，以及 S_WhoCharinfo、S_ServerMessage 的欄位序列。未執行 JAR。S39 serializer 格式依來源與既有引擎 parser 交叉核對；不宣稱整份來源與 JAR 本文完全一致。

## 待核定

由 Claude／調度器修正 C99 的任務敘述，提供 d287 實際可用的排行榜入口與回包後另行派工。此版本完成 `/who` 與已核定資訊顯示；不宣稱完成物件資訊／排行榜。原生驗收由 dot 在獲授權的 P24Lab smoke 執行。
