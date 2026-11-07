# 公告板協定（T130／#266）

2026-11-07：先查 `docs/reference/server-protocol.md`、`server-rules.md`，再唯讀查閱 `D:\lineage開發\1-伺服器` 的封包處理器、公告板腳本及既有 SQL 文字檔。未連資料庫、未複製伺服器原始碼。以下是本次來源快照的結論；不含 S156 拍賣板。

## 欄位

`C`／`H`／`D` 分別為 1／2／4 bytes，整數為 little endian；`S` 是 CP950／Big5、NUL 結尾。引擎沿用 LoginPackets 的 8-byte 零填充。

| 封包 | opcode 後的欄位 |
|---|---|
| C10 列表 | D(公告板物件 ID) |
| C23 下一頁 | D(公告板物件 ID)、D(目前清單最後一篇 ID) |
| C114 閱讀 | D(公告板物件 ID)、D(文章 ID) |
| C141 發文 | D(公告板物件 ID)、S(標題)、S(內容) |
| C153 刪文 | D(公告板物件 ID)、D(文章 ID) |
| S68 列表 | C(0)、D(公告板物件 ID)、D(0x7FFFFFFF)、H(篇數)、H(300)，接著每篇 D(ID)、S(作者)、S(日期)、S(標題) |
| S148 閱讀 | D(文章 ID)、S(作者)、S(日期)、S(標題)、S(內容) |

## 已核對的行為

- `BoardTable.getBoardPage`：最多 8 篇，ID 遞減。游標非零時包含 `ID <= 游標`，所以相鄰頁重疊一篇；C23 處理器亦要求游標文章存在。不能擅自把游標減一，否則遇到 ID 缺號可能不回包。首頁用 C10，上一頁以本機保存的游標重送 C10 或 C23。不到 8 篇即停用下一頁；游標已刪除而不回包時，15 秒後可手動刷新首頁。
- 發文標題最多 16 個 Java/C# UTF-16 字元，內容最多 1000 個；本機拒絕無法以 CP950 編碼的字元、NUL、空白全文及非必要控制字元，內容允許換行／Tab。空白全文限制是客戶端政策。
- `Npc_Board.writeTopic` 扣 300 金幣，持久化成功才保留文章；費用不足會回 S71 訊息。發文與刪文沒有專屬成功 ACK，也不保證回送列表，故不推算金幣、不插入／移除本機文章、不顯示成功，提交後停用再次異動，明確手動刷新才恢復操作。
- 刪文伺服器權限為作者或 GM；本次一般玩家 UI 只提供本人文章刪除，不推測 GM 身分。
- 伺服器檢查目前角色／世界綁定、原 NPC 腳本及距離不超過 5 格。引擎最終送出檢查保留 generation、world epoch、target revision、公告板 revision、角色存活、距離、交易／動作鎖、移動狀態及滑鼠請求有效性。
- S87 沒有 NPC template ID。唯讀 SQL 中一般公告板 gfx 為 1544、1546、1557、1559、2205、2207，名稱為 `$1385`，結婚板為 `$33 $1385`。拍賣板也用 gfx 1544，但名稱是 `$1383`。因此採 gfx＋精確名稱辨識；不單憑外觀、不將拍賣板列入支援。
- UI 使用獨立的原生 WinForms 擁有視窗，走既有 NPC／滑鼠交談入口。字串只顯示在 ListBox／TextBox，沒有 HTML 執行。換目標、離開範圍、死亡、切圖或斷線後舊視窗失效；關閉視窗也使排隊中的請求失效。

## 協定限制及待驗證

S68 沒有請求序號／回傳游標，S148 甚至沒有公告板 ID。本機只接受目前等待種類、公告板／文章身分與游標範圍符合的回包；關閉或逾時後不主動接受。若使用者重新請求同一公告板／同一文章，線上無法區別極晚到的相同回包，本機 revision 不能補出伺服器未提供的資訊。

實機點擊、視窗焦點／版面、不同解析度及原服持久化結果留待 dot 的集中 smoke；本次不啟動遊戲、伺服器或 DB。

## 唯讀來源 SHA-256

以下路徑相對於 `D:\lineage開發\1-伺服器`；只記錄來源識別與衍生結論。

| 路徑 | SHA-256 |
|---|---|
| `src/com/lineage/server/clientpackets/C_Board.java` | `20e3437630822a9b866d9e1a56865046477f4110451fb0c23d870defe5da8da5` |
| `src/com/lineage/server/clientpackets/C_BoardBack.java` | `96cebb5ea143b18b64852449fab085d1174b8484787db62c1f5842b8848d1d8b` |
| `src/com/lineage/server/clientpackets/C_BoardRead.java` | `8e99855f4bcd9abf356df2e4628b2a1c165c7b40e5271d2c277cb6c79d244f73` |
| `src/com/lineage/server/clientpackets/C_BoardWrite.java` | `d50fbae5dae1b86e75e163d8504a4ef5d3f5f582fe6fd2f80936b56fc4e82453` |
| `src/com/lineage/server/clientpackets/C_BoardDelete.java` | `411192fc2319fe4f8207de579123ae2a32ee688531815bc6f7d711bcac17be68` |
| `src/com/lineage/server/serverpackets/S_Board.java` | `d7e83c0bbf72d9084f267af9b22a47bc220db72d48e010708de4b58e21f42537` |
| `src/com/lineage/server/serverpackets/S_BoardRead.java` | `2a8da353ad4f0aa287b131ca823006c97edc2bea7b1f61ab1a623f0746930490` |
| `src/com/lineage/data/npc/other/Npc_Board.java` | `47c05a283f62b81da4797f81c9988ed29bcc2b46ab13537fa463ab20bed1ca2e` |
| `src/com/lineage/server/model/npc/BoardInteractionAccess.java` | `ad17689bd58247e2b79b23a9c6fd67ba0d6f03a0ecc2e23d4103b124a1fa268d` |
| `src/com/lineage/server/datatables/server/BoardTable.java` | `73b98f7895e2a6fa75c06d02478d18166d9e02641d4ed44c8473f70cf11c2544` |
| `src/com/lineage/server/model/BoardPersistenceService.java` | `766a6db6b34b40361344bacc4064f272b8f6e4f32562047d7094eb27b1680343` |
| `db/380_db.sql`（只讀 NPC 定義） | `2afbc066fa7dbde391169050deeed206a20a39afbc0390378ddf2800cd3328b5` |
