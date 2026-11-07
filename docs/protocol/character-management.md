# T107 角色管理協議補充（#195）

依 #132 的 `server-protocol.md`、`server-rules.md` R01–R06，並於 2026-11-07 唯讀核對 `D:\lineage開發\1-伺服器/src`。以下是協議與規則摘要，未複製伺服器程式碼、配置或遊戲資源。

## 封包與階段

| 動作 | 請求（不含 8-byte 零填補） | 回包與完成條件 |
|---|---|---|
| 建立 | C84：S 名稱、C 職業、C 性別、六個 C：力量／敏捷／體質／精神／魅力／智力 | S98/C reason：2 接受、6 重名、9 名稱拒絕、21 欄位／能力／未開放職業；2 後仍須等待 S127 的角色明細才加入清單 |
| 刪除 | C162：S 角色名稱 | S6/C result：5 立即刪除；81 切換七日刪除排程。收到後送一次 C16，等待 S178＋完整 S93 清單，不樂觀移除角色 |
| 同連線切角 | C7：無欄位 | 等 S250/C42＋六個零，接著伺服器主動送 S178＋S93；不送 C16、不重新認證；完整清單後才允許 C137 |

S127 明細欄位與 S93 相同，含名稱、血盟、職業、性別、lawful、HP／MP、AC、等級、六能力、GM、建立日期、checkcode。HP／MP 依 client1 為 U16、client2 為 U32，其餘沿用 `LoginPackets.Character`。待刪角色職業為 32–38；再次 C162 可取消排程，刷新後回到 0–6。七日刪除門檻與開關由伺服器決定，客戶端不假定本機部署值。

C84／C162／C16 限 CHARACTER_SELECT。C7 限 IN_GAME；伺服器約延後 250 ms 進入 SWITCHING_CHARACTER，退役角色並完成 phase lease 後才送選角通知。客戶端以 `SwitchingCharacter`／`ManagingCharacters` 阻擋所有選角或世界請求；原 TCP、cipher、connection generation 與 sender 保留，WorldEpoch 及各動作 lease 更新。切角等待期間消耗但不套用舊世界封包，S227 仍視為失敗。

15 秒未收到完整結果會關閉連線並提示重新登入確認結果；不重送建立／刪除／切角請求，也不將這個失敗處理視為成功切角。主動取消／斷線撤銷等待與過期的排隊傳送。

## 初始能力與名稱

能力順序固定為力量、敏捷、體質、精神、魅力、智力。

| 職業 ID | 職業 | 基礎能力 | 可分配點 |
|---|---|---|---|
| 0 | 王族 | 13 / 10 / 10 / 11 / 13 / 10 | 8 |
| 1 | 騎士 | 16 / 12 / 14 / 9 / 12 / 8 | 4 |
| 2 | 精靈 | 11 / 12 / 12 / 12 / 9 / 12 | 7 |
| 3 | 法師 | 8 / 7 / 12 / 12 / 8 / 12 | 16 |
| 4 | 黑暗精靈 | 12 / 15 / 8 / 10 / 9 / 11 | 10 |
| 5 | 龍騎士 | 13 / 11 / 14 / 12 / 8 / 11 | 6 |
| 6 | 幻術師 | 11 / 10 / 12 / 12 / 8 / 12 | 10 |

性別 0／1。六能力總和必須等於 75，各欄不得低於職業基礎值，也不得高於該欄基礎值加可分配點數。龍騎士與幻術師另受部署配置開關限制；UI 顯示七職業，由 S98 拒絕結果告知部署限制。

名稱先移除 Java 預設 `\s` 對應的六種 ASCII 空白及全形空白，再以嚴格 Big5 編碼驗證：最多 12 bytes，bytes 減字元數最多 5。客戶端檢查 handler 的固定禁止符號與假名清單；伺服器的 ConfigBad 禁用詞與全帳號重名仍以回包為準。客戶端另外拒絕控制字元、不能編碼的字元與反斜線（伺服器 quoteReplacement 會改寫反斜線），避免送出與顯示名稱不同。

## 來源 SHA-256

路徑以下皆相對於 `D:\lineage開發\1-伺服器/src/com/lineage/`。

| 來源 | SHA-256 |
|---|---|
| server/clientpackets/C_CreateChar.java | `400c2c4d48b63d0c73fb07a736dde7a5c5b3a03159ed063b76aff3cbe09eae72` |
| server/clientpackets/C_DeleteChar.java | `a3024d7b58c34ef6f3e1c197b4824710d56ffe0cf387ec4220e937249de1a628` |
| server/clientpackets/C_NewCharSelect.java | `6154a69a9cd1b074667b98c077d4c349043b9cdc61e16fdb36a108b16bb5b1e3` |
| echo/SessionPhaseGate.java | `ea7e28fd276ba9b469222b3a655be279d729b69811f06978aaff74877b52c13a` |
| echo/SessionPacketPolicy.java | `e6b33150986f64550a1f02b148c68fc27f5085b41179d2796aac833a80dce8b2` |
| server/serverpackets/S_CharCreateStatus.java | `6a666e05144f0506857fd485bf0e98ef67aa2f1edd4cd15b00dd3895e7b7b182` |
| server/serverpackets/S_NewCharPacket.java | `89f9f783ec9e38f16fd21c30cbb32be4c86d021750b74083ed6a547fac08707e` |
| server/serverpackets/S_DeleteCharOK.java | `52405c3aeb5e1ef9a2d2a0e38d3cdf09694e4ec74a68183e88490fe8512dbb2a` |
| server/serverpackets/S_PacketBoxSelect.java | `21ca14f9f68de4a90b7229e26f5f0831c5344d6441c17b8b885bde836ba70496` |
| server/serverpackets/OpcodesServer.java | `9044cb98cf8a0548a421fbb05e6cdcef26147b2a2b85a1ef896fde8a8016f152` |
