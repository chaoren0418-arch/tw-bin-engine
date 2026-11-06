# 記憶座標（T124／#258）

## 封包與狀態

本模組依 `docs/reference/server-protocol.md`、`server-rules.md`，並唯讀核對本機伺服器來源。`C`／`H`／`D` 為 1／2／4 位元組，數值使用 little endian；`S` 為 Big5、NUL 結尾。產品沿用 8 位元組零填補規則。

| 封包 | opcode 後欄位 | 語意 |
|---|---|---|
| S64／S_BookmarkList | C(0x2A), H(0x80), C(2), 127 個 FF, H(容量), H(筆數), 每筆 D(id), S(name), H(map), H(x), H(y) | 完整替換書籤；其他 S64 子型別仍由既有模組處理 |
| S92／S_Bookmarks | S(name), H(map), H(x), H(y), D(id) | 按 ID 新增或更新 |
| C165／C_AddBookmark | S(name) | 伺服器自行取得當時地圖與座標；客戶端不提交座標 |
| C3／C_DeleteBookmark | S(name) | 按名稱刪除，無成功確認回包 |

清單解析完成且填補驗證通過才替換狀態；破損封包不套用部分資料。清單中的容量保留原值，預設容量依伺服器常數為 50；容量不是客戶端授權新增成功的證據。

新增按伺服器回包更新，客戶端不預填位置。刪除送出後保留可見的待確認列，停用重送並讓 `World.Bookmarks.TryGet(uint id)` 回傳 null；重新登入取得清單才確認結果。`TryGet(uint id, out Bookmark? bookmark)` 亦可用。地圖切換不清除書籤。

## 唯讀來源補充

來源基底：`D:\lineage開發\1-伺服器\src\com\lineage\server`。未複製任何伺服器程式碼、未查詢 DB。

| 相對來源 | SHA-256 |
|---|---|
| clientpackets/C_AddBookmark.java | 011bfc8e81c50067004becd19907e480b84a3afd91004f5de9fbe0d4e9f392c3 |
| clientpackets/C_DeleteBookmark.java | 1d322e0ee50f28c9a1765d5dbfbc24ebff8bc0aff441d629acff9f2759b6af19 |
| serverpackets/S_BookmarkList.java | f2421a346fc320248c166d7e2342c0374037b6652174beacd4cd9839a4556c00 |
| serverpackets/S_Bookmarks.java | 749e8529cb083c13715ccf7e8858775ce647bd223e365692c899a296ac76b0e9 |
| model/bookmark/BookmarkService.java | 2731266f9435849190cdce5e3a326c54d39be5d2c5441917b6dc53ea1b5398a2 |

- `C_AddBookmark.start` 檢查死亡／幽靈／傳送、可標記地圖、城戰／房屋／賭場限制；實際新增再經服務層驗證。
- `BookmarkService.addBookmark` 容量 50、名稱忽略大小寫去重；拒絕訊息為 214（地點）、676（容量）、327（同名）。接受後送 S92；非同步持久化可能失敗並回滾，S92 不能當作持久化驗收。
- `deleteBookmark` 按忽略大小寫的名稱找到第一筆，排入刪除工作後移除記憶體資料，沒有刪除確認封包；持久化失敗可回滾。
- 客戶端名稱輸入政策為非空白、無控制字元、Big5 可表示、最多 64 個 Big5 位元組；不是宣稱伺服器有相同字數限制。
- 給法師技能任務：本地查詢鍵是 S64／S92 的資料 ID，但此快照的 `BookmarkService.getBookMark(pc, i)` 將技能尾段參數拆成低 16 位 X、高 16 位 Y 尋找座標。請核對技能 5／69 的實際消費端，再決定 C6 的 D 內容；不要直接假設資料 ID 就是傳送參數。本張不改 `MagicUi.cs` 或施法編碼器。
