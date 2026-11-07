# T146 血盟戰爭封包（Issue #378）

以 `docs/reference/server-protocol.md` 為基礎，唯讀核對 `D:\lineage開發\1-伺服器\src\com\lineage\server`，並以 `javap -p -c` 核對 `environments/p24/server/Server_Game.jar`（SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`）。沒有執行 JAR、沒有解壓或複製伺服器程式碼。下文 JVM 位移只是方法內 bytecode offset，不是 File Offset／RVA／VA。D／H 為 little endian，S 為 Big5 NUL 字串，傳輸沿用 8-byte 零填補。

## 格式

| 方向 | 格式 | 語義與 d287 核對 |
|---|---|---|
| C227 | `C(type) S(血盟名)` | type：0 宣戰、2 投降、3 結束。`C_War.start` 位移 201／207 依序 `readC`、`readS`。名稱 `" "` 是城堡路徑，本模組不送。 |
| S219／217 | `H(0) D(0) H(217) S(宣戰血盟) S(宣戰者)` | `C_War` 位移 850 呼叫 `S_Message_YN(int,String,String)`。 |
| S219／221、222 | `H(0) D(0) H(訊息) S(血盟)` | 投降、結束提案。位移 879、908 呼叫 `S_Message_YN(int,String)`。 |
| C121 | `H(0) D(0) H(訊息) C(1 接受／0 拒絕)` | `C_Attr.start` 位移 72／83／88 讀 H、D、H，switch 217／221／222 同跳 1149，位移 1150 `readC`。D 讀取後不用於本分支，對方由 `pc.getTempID()` 決定。 |
| S84 | `C(type) S(血盟1) S(血盟2)` | `S_War` 的 opcode 為 84。 |

d287 的 `S_Message_YN` 沒有 `confirmation(...)` 與 requestId 建構子（見 `docs/reference/source-vs-jar.md`）。本模組的 D 只接受 0，非零視為協定錯誤，不走 requestId 路徑。

## S84 子型別與狀態

| type | 文字（來源為 `S_War.java` 註解的訊息 226 等） | 本地狀態 |
|---|---|---|
| 1 | %0 血盟向 %1 血盟宣戰。 | 若涉及本身血盟，加入交戰配對 |
| 2 | %0 血盟向 %1 血盟投降了。 | 移除配對 |
| 3 | %0 血盟與 %1 血盟之間的戰爭結束了。 | 移除配對 |
| 4 | %0 血盟贏了對 %1 血盟的戰爭。 | 移除配對 |
| 6／7 | 同盟成立／解除 | 只更新提示 |
| 8 | 目前你的血盟與 %0 血盟交戰當中。 | 加入配對；封包實際帶 `(本身血盟, 敵方血盟)` |

其他 type 計入 `unhandled`，不改狀態。

- **結束事件**：`L1War.requestSimWar` 投降時依序送 2、4、3，結束時送 3；攻城 `requestCastleWar` 投降只送 2、4，沒有 3。所以 2、3、4 都視為該配對結束，重複移除無害。
- **配對不分先後**：宣戰方與被宣戰方都可能在第一欄。
- **只保留本身血盟的配對**：攻城 S84 是全服廣播，第三方戰爭只更新提示，不佔清單。上限 256 筆。
- **`S_War(1, 血盟, "NPC")`**：無主城堡的假血盟名，沒有對應結束訊息，不建立配對。
- 比對血盟名不分大小寫，與伺服器的 `equalsIgnoreCase` 相同。

## 生命週期

- 戰爭狀態**不隨 WorldEpoch 清除**：S84／8 只在登入時送，切地圖後不會重送。切圖時 `Actor` 會短暫為 null，因此模組保留本身血盟名。
- `Reset` 或登出後清除。`C_LoginToServer` 的順序是 `getFocus`（S8、S87）在前，`war`（S84／8）在後，所以 `Status==null` 可以當成清除條件，不會吃掉登入的 S84／8。
- 本身血盟變更時清除配對與待回答確認；重新加入也不會恢復舊配對。
- S84／8 在伺服器端遇到第一場戰爭就 `break`，同時多場戰爭時登入只會回報一場。

## 確認對話

S219 是共用的單一槽位，伺服器端共用 `TempID`。所有 S219 都先經過 `ReceiveClanWarYesNo`（`Trade.cs` 單一分支）：

- 非 217／221／222：作廢待回答的戰爭確認，交回原路徑。
- 217／221／222：作廢舊的求婚、交易邀請、入盟申請。Party（953）與 Duel（630）在 Trade 之前就被處理，因此改由 `CheckClanWar` 觀察：有存活的組隊或決鬥邀請時，代表它比戰爭確認新，作廢戰爭確認。
- 伺服器沒有此確認的逾時，本地也不設時限。世界變更、NPC 互動變更（可能改寫 `TempID`）、死亡、血盟變更時作廢。排隊中的回答在送出 gate 重新核對。

回答或送出都不預先修改交戰狀態，一律等 S84。

## 客戶端前置檢查

沒有血盟、對象是本身血盟時，伺服器會無聲丟棄，所以客戶端直接給提示、不送出。君主資格、等級、是否在戰爭中、對方是否在線，全部交給伺服器，結果走既有 S71（218、232、234、236、237、272、474–478、812）。「攻城戰期間，暫停血盟宣戰」走 `S_HelpMessage`，本任務未處理。

## 唯讀來源 SHA-256

路徑相對於 `1-伺服器/src/com/lineage/server`。

| 路徑 | SHA-256 |
|---|---|
| clientpackets/C_War.java | 733980808f998f818a52802b3cb8d36c6e556d7f115c2b84814040e43906fed0 |
| clientpackets/C_Attr.java | 793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23 |
| clientpackets/C_LoginToServer.java | c2eedcb6d78d9da7f33af485c596d393d49818a0a05bc5a37cbedb79bcfc9de4 |
| serverpackets/S_War.java | a9eea0aa678c021127f29bdb53cb52f7e04b6243cc46a9e7ad322a5e4812b348 |
| serverpackets/S_Message_YN.java | f5c6bad673e5af10c851fd6206624597c2514fb88b98fc1f3e574e935c6d0c45 |
| model/L1War.java | a6cbe96d34c5dcd4a14c8cf5dfc3b347ddac2e63fcd4a468cc83f1d7a5741c6a |

## 已驗證／推測／待驗證

- **已驗證**：上表格式與 d287 位元碼相符；合成封包離線測試。
- **推測**：S84 的文字取自伺服器註解，未與 `Text.pak` 逐字比對；type 8 的 `%0` 依語義顯示敵方血盟。
- **待驗證**：原生客戶端對 S84 的實際顯示位置、敵對名稱的畫面效果、實際雙人宣戰往返。
