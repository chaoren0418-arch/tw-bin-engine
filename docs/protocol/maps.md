# T071 換圖契約

本次唯讀查核 `D:\lineage開發\1-伺服器` 的 Java 與 `db/380_db.sql` 靜態檔，未連 DB。這是來源契約，不能證明 P24Lab 執行中的 JAR／資料表完全相同；來源 SHA-256 見文末。

| 流程 | 已核對契約 | 引擎對應 |
|---|---|---|
| 洞穴 | `C_MoveChar.start` 的 C29 踩格呼叫 `DungeonTable.dg`；C219 另讀 H(x),H(y)，只接受目前或相鄰合法入口。目的圖由伺服器決定。 | 既有 C29 步行觸發，不增加可任意指定目的地的 UI。 |
| 傳送 | S149,H(marker),D(portal)；C52 ACK 無額外資料。`C_Teleport` 以目前交易接受 ACK；`Teleportation.teleportation` 送 S206,H(map),C(water)，再 S87 自身位置並重建 AOI。S206 即使 map 相同仍是新場景。 | S149 清場、ACK 去重；S206 清 cache／物件／選取／魔法／遮擋／6016，S87 決定位置；非同步載入以 epoch／revision 隔離。 |
| 失敗恢復 | `Teleportation` 的 abort 送 S202(mode7)、S206、S87，再重建視野；mode7 本身不是抵達。 | 操作保持鎖定直到新地圖與自身包。 |
| 船運 | `DungeonTable.isTeleportableByTime` 使用遊戲秒，每天 86400 秒；說島登船 5400–8999，之後每 10800 秒；古魯丁首班 0–359，之後每 10800 秒起開放 3600 秒。`L1GameTime` 為現實時間六倍。 | 船圖5往 map4、船圖6往 map0；到港沿用既有原客戶端分鐘規則（0／90 mod180），以 S123 校時，不以本機時鐘授權下船。 |
| 下船 | C231,H(map),H(x),H(y)。`C_Ship` 依來源船圖消耗票、先送舊 S87，再 `L1Teleport.teleport`。`preparePcLanding` 可能校正落點，S206／新 S87 才是權威。handler 未檢查 consumeItem 回傳值，不能聲稱沒票必拒絕。 | 保留票與新鮮時間的客戶端檢查；不把送出或舊 S87 當抵達；接受目的圖上伺服器最終座標。 |
| 旅館 | `C_NPCAction` 路由 DB 驅動動作：`room` 製作鑰匙，`enter` 由 `NpcTeleportExecutor` 選持有資格符合的路線。`require_item_id` 是持有條件，不在 enter 消耗。 | 使用既有 C34／S39／C125 對話，鑰匙依背包回包更新；不自行扣款或建立房間。 |
| 地圖特效 | 伺服器換圖不傳本機 S32 特效序列；`OriginalMapCache` 從雜湊核驗的區塊與 effect manifest 載入。 | 每次場景重建重設效果起點；離圖退役 owner，回圖用新生命週期。合成像素測試不代表原素材畫面驗收。 |

靜態驗收路線與本機資源覆蓋另見 [issue-140.md](../reports/issue-140.md)。本次不改封存 lock、不複製 Java、SQL 或遊戲素材。

## 來源 SHA-256

路徑相對於唯讀伺服器根目錄；2026-11-06 查核，未複製檔案。

| 路徑 | SHA-256 |
|---|---|
| `src/com/lineage/server/clientpackets/C_MoveChar.java` | `b569737e5fcda200291f803f41f4f9bddaa6cc6986c3869274934bcbc48a12d2` |
| `src/com/lineage/server/clientpackets/C_EnterPortal.java` | `5ce4b59f857d121e54062fa6f4fa8b33ad878f2ee375dd953ca1bbd10feb1277` |
| `src/com/lineage/server/clientpackets/C_Teleport.java` | `0080261575be9445aedc3d98059937933a82ac4761fc2dbff79b87e499b5ef04` |
| `src/com/lineage/server/clientpackets/C_Ship.java` | `1be305fce6d3859627dc6ae189a6c97924997692a394eff81ca49c1e2faed0e2` |
| `src/com/lineage/server/clientpackets/C_NPCAction.java` | `280ef4bb7316ca61e199ae97a71405ec45affc37a8b4955c4881a891b298b0a1` |
| `src/com/lineage/server/datatables/DungeonTable.java` | `4831d6e6fc47eb0b380f35267919cf4feac2c2d16137c675f5da0dfba1c5b810` |
| `src/com/lineage/server/model/L1Teleport.java` | `fab5fb0e371317af631463c04f2fa6284f991227fa1a4b08b43424725e0c74e2` |
| `src/com/lineage/server/model/gametime/L1GameTime.java` | `6f9b22b839c7693d7d8ebe37faa56369a1e2d90de8f1c00a461777566551c749` |
| `src/com/lineage/server/model/npc/action/NpcTeleportExecutor.java` | `40f8be6b4c2f2226b5d4ba43392c2ee49b318a4513701aad4d0368bc29bceba6` |
| `src/com/lineage/server/serverpackets/S_MapID.java` | `e4f8e0f4d4b98bdc90333233a9daba53ffee547881905956ef2e1cb33e28fb68` |
| `src/com/lineage/server/serverpackets/S_Teleport.java` | `9080faaf3aaae8c2be0e3c63a15de74119f5831b40293af234b4f3b13e5b7f0c` |
| `src/com/lineage/server/serverpackets/S_Teleport2.java` | `4fdbeb813375129256f1fa5811896ae49834563dfd1a87698b6a27cc35219430` |
| `src/com/lineage/server/utils/Teleportation.java` | `2108857a47e620d35fa06b2b6dfc0ff867dcd97a913fcc57616565ba8a27e38b` |
| `db/380_db.sql` | `2afbc066fa7dbde391169050deeed206a20a39afbc0390378ddf2800cd3328b5` |
