# 好友與封鎖協定（T126／#260）

先核對 `docs/reference/server-protocol.md` 與 `server-rules.md`，再唯讀查閱本機 `D:\lineage開發\1-伺服器\src\com\lineage\server`。以下為欄位摘要，沒有複製伺服器原始碼。`C/H/D` 為 1/2/4 bytes，整數 little-endian；`S` 為 Big5（CP950）NUL 結尾；客戶端沿用 `LoginPackets.Pad` 補零至 8 bytes。

| 方向 | 格式 | 行為 |
|---|---|---|
| C4 | `C4` | 查詢目前角色好友，回 S39 buddy。 |
| C207 | `C207 S(name)` | 新增好友；名稱不分大小寫。非同步處理，成功不回確認封包；找不到角色回 S71/109。 |
| C202 | `C202 S(name)` | 刪除好友；非同步處理，沒有成功確認回包。 |
| C171 | `C171 S(name)` | 切換拒絕名單，不是獨立的增加／刪除命令。 |
| S39 | `C39 D(owner) S("buddy") H2 H2 S(all) S(online)` | 兩欄皆是空格分隔名稱，可空字串，通常帶尾端空格。第二欄表示這次查詢的在線名稱。 |
| S250/17 | `C250 C17 C(count) S(name)...` | 拒絕名單快照，取代本機清單；格式由 PacketBox 建構子確認，未證明每次登入必送。 |
| S250/18 | `C250 C18 S(name)` | 伺服器確認加入拒絕名單。 |
| S250/19 | `C250 C19 S(name)` | 伺服器確認移除拒絕名單。 |
| S71/472 | `C71 H472 C0` | 拒絕名單人數已滿；仍交既有訊息處理器顯示。 |

`C_Exclude.start` 先檢查名單是否已有 16 人，才檢查移除／新增，因此滿 16 人時連解除封鎖也會收到 472；客戶端不得假裝已解除。名稱以不分大小寫方式比對。沒有獨立查詢拒絕名單的 C171 空字串形式：空名稱直接返回。

好友變更後不立即自動查詢，避免 C4 超過伺服器的非同步增刪工作；保留上次清單並標示待確認，使用者稍後明確按「查詢好友」。S39 的 owner 必須等於目前角色，flag/count 必須恰為 2/2；不能交給一般 NPC HTML 的 flag 0/1 解碼器。其他 S39 與 PacketBox 子碼應保留既有路由。

封鎖切換只送一次，匹配回覆或明確拒絕到達前停用下一次切換；不因本機逾時自動重送，避免反向解除。未回覆時維持待確認，重新連線清除本機狀態。角色重置清空兩份清單；一般換地圖保留清單，但舊世界排隊的操作必須失效。UI 名稱 52 字與禁止空白／控制字元是本機輸入政策，不宣稱為伺服器創角限制。

## 聊天過濾

`C_Chat.chatType_0`（約 682–715 行）在送給可見玩家前檢查接收方拒絕名單；`C_ChatWhisper.start`（約 160 行）也檢查接收方拒絕名單，拒絕時回 117。因此此版本伺服器已負責一般聊天與密語過濾。

客戶端另以 `PlayerChat.cs` 的單一查詢點防止已知封鎖者顯示於新收聊天：S73 使用獨立的來源名稱；S81/type0 依 `S_Chat` 的 `name + ": " + body` 格式比對完整名稱，涵蓋隱形角色 objectId=0。不以訊息本文子字串判斷、不遮蔽送出密語回顯、不改其他頻道。特殊 NPC／GM 未使用此名稱格式的訊息仍由伺服器規則處理。既有聊天歷史不回溯刪除。

## 唯讀來源識別

| 相對 server 路徑 | SHA-256 |
|---|---|
| clientpackets/C_Buddy.java | `8b8b6e9533f4d7064d03d1bf0e144d26d9cadd75fcf1186c0023fa41e21fd519` |
| clientpackets/C_AddBuddy.java | `7ddae0a303a2114d5299dcfb06c127dcd53b8472374e67699c3f821c12573484` |
| clientpackets/C_DelBuddy.java | `a841bcc49ef05dd2c287afa55f78f2dca3abb742864c67ae35dd34902c0421ea` |
| clientpackets/C_Exclude.java | `35bb5412e2d0d77cf92360b9f2d3a62aa120de2da68d3b6b676ba609cc87f8ed` |
| serverpackets/S_Buddy.java | `0fd54bd7ca3ec58459c28abb2a1d4d972b4c0c704725ebf1b8dfe2ebd6bfe4a9` |
| serverpackets/S_PacketBox.java | `817ba5fd5b8ada480c6cef6d0529b9cb8ac8c6a33d84af550f8479a31fcce977` |
| model/L1ExcludingList.java | `7ec15a8874d04859bf29068fa1119e9f1e45894c230acba6025a872eb6d2e27e` |
| clientpackets/C_Chat.java | `72257d44684061e922f79dcd8f7b5ed4604ccaffd875506d2457a00a1873f4b2` |
| clientpackets/C_ChatWhisper.java | `44d67c3dccbdbe6842ec3a59b6c7c3dcb512e98c8182d79ae563a31d5b87f8ff` |
| serverpackets/S_Chat.java | `f1aa6a354745fe0f29b4d9154ea2c0b757e3aa264f943d1993d23ee4e6006f6b` |
