# T145 踩點入口契約（Issue #377）

本次唯讀查閱 `docs/reference/server-protocol.md`、`server-rules.md` 與主伺服器處理器，未連線、未執行 Java、未讀機密設定或 DB。

## 伺服器與送包時機

- C219／0xDB：`C(opcode), H(x), H(y)`，H 為 little-endian，邏輯長度 5，沿用客戶端加密前補零至 8 bytes。沒有目的 map、入口 ID 或字串。例：`DB 34 12 CD AB 00 00 00` 表示入口 `(4660,43981)`。
- `C_EnterPortal.start` 拒絕未連線、無角色、鬼魂、死亡、傳送中；座標須距伺服器角色 Chebyshev ≤ 1，且 `DungeonTable.isPortal(x,y,currentMap)` 成立。`dg` 再決定目的地、船班時間與通行物品條件。
- `C_MoveChar.start` 已會對步行目標執行 `DungeonTable.dg`／`DungeonRTable.dg`。因此 C29 後可能先收到傳送通知；此時取消尚未送出的 C219。沒有從這些處理器找到下發入口清單的流程，亦不把每步 C29 後補 C219 當成協定要求。
- 客戶端策略：成功的一格步行 → 查目前 map/cache 的原始入口記錄 → 等待該步動畫剩餘時間 → 在 sender gate 再核對連線世代、world epoch、位置校正、movement lease、傳送世代、cache、角色 ID／座標與可移動狀態 → 送一次 C219。轉向、drift probe、失敗的 C29 不觸發。離開再踩入可以再次請求，沒有計時重送或本地傳送成功預測。
- S149／C52／S206／S87 與 S202 recovery 全部沿用既有流程。本模組沒有新 S 格式或接收分派，`Teleport.cs` 不變。入口資料與伺服器 `dungeon` 不同時由伺服器拒絕；沒有客戶端記錄的伺服器自訂入口仍保留原有 C29 路徑。

## 入口來源與座標

原始 S32 的 portal section 位於 ground、cover、attributes、object groups、opacity triggers、tile references 之後。格式為 `H(count)`，每筆 `C(nameLength), name bytes, C(localX), C(localY), H(target), D(id)`。只使用來源座標；不使用名稱、目的地或 ID 指揮傳送。SEG 使用既有 map decoder 已限定的單一 profile，section 前的 tile／attribute／reference 欄寬依該 profile 讀取。

`Portals.cs` 重新核對 `integrity.json` 中的 map manifest SHA-256，再用目前 `OriginalMapBlock` 的 SHA-256 核對來源 bytes。入口索引依 cache／block 物件生命週期保存，不共用不同地圖或退役區塊。不存在、截斷或雜湊不符時不送 C219。解析器只讀入口段，完整區塊／effect tail 仍由原有 map loader 負責。

查詢位置使用 `localX = 2 * worldX - 32768 - block.X`、`localY = worldY - block.Y`，僅接受該 128×64 區塊內的精確記錄；不把相鄰半格整除成同一格，不掃鄰格。

**不能用 S32 tile attribute 的 `0x0002` 位元遮罩當入口。** 唯讀原始檔證據如下，offset 均為 **File Offset**（十進位），沒有複製素材：

| 原客戶端相對路徑 | 入口記錄 offset | local／world 座標 | 該格 attribute | SHA-256 |
|---|---:|---|---:|---|
| `map/0/7ffb8001.s32` | 126122 | `(58,19)`／`(32477,32851)` | 9 | `c66daef38141ec5686c247463c5098d329b03d001abf7db6383d5b7044f5af48` |
| `map/0/7ffd8003.s32` | 81165 | `(110,23)`／`(32631,32983)` | 8 | `23ad0e1ef9001c6e331a3425e5a1faa410d3cf4a8e5d9425f38daa85c22e7e3a` |
| `map/1/7ffe8000.s32` | 214302 | `(58,34)`／`(32669,32802)` | 1 | `01fb3ecf024c48b97941961c6a5b7d6f0147d29f4d7265b3e8dc0ad76d5536fe` |
| `map/16384/7fff8000.s32` | 60979 | `(84,35)`／`(32746,32803)` | 4 | `17ad7591309d96e0afe80c0e0f9e80d730d60c493507a063d29367dc341e0d7b` |

這些區塊的 attribute `0x0002` 均沒有任何格設定。SEG loader 的人工標記不能反推 S32 有同一位元語義。以上只證明來源座標與格式，未驗證原 BIN 的 C219 發送時序；本次到達後發送策略仍待集中實機 smoke。

## 伺服器來源 SHA-256

2026-11-07 核對，路徑相對 `D:/lineage開發/1-伺服器`。

| 路徑 | SHA-256 |
|---|---|
| `src/com/lineage/server/clientpackets/C_EnterPortal.java` | `5ce4b59f857d121e54062fa6f4fa8b33ad878f2ee375dd953ca1bbd10feb1277` |
| `src/com/lineage/server/clientpackets/C_MoveChar.java` | `b569737e5fcda200291f803f41f4f9bddaa6cc6986c3869274934bcbc48a12d2` |
| `src/com/lineage/server/datatables/DungeonTable.java` | `4831d6e6fc47eb0b380f35267919cf4feac2c2d16137c675f5da0dfba1c5b810` |
