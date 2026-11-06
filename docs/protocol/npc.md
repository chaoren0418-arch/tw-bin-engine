# NPC 服務協議與 T075 購肉路線（T070／#133）

本次唯讀核對 `D:\lineage開發\1-伺服器` 的 Java 原始碼與 `db/380_db.sql` 靜態定義；沒有連 DB、啟停服務或複製原始碼／遊戲素材。以下是來源契約與離線結果，**不等於目前 P24Lab 執行中資料或真機成交證據**。來源識別見文末 SHA-256。

## 封包與引擎對照

數字 opcode 均為十進位；C=1 byte、H=2 bytes、D=4 bytes，小端序；S=目前協議文字編碼的 NUL 結尾字串。以下省略 8-byte 對齊 padding。NPC template ID、世界 ObjectId、物品 ObjectId、商店 order 是不同身分。

| 服務 | Client → Server | Server → Client | 引擎 |
|---|---|---|---|
| 開啟／操作 NPC | C34 D(npcObjectId)；C125 D(npcObjectId) S(action) | S39 D(npcObjectId) S(htmlKey) H(flag) H(argumentCount) S(args)… | `NpcInteraction.cs`、`NpcTemplates.cs`、`NpcUi.cs` |
| NPC 購買 | action=`buy`；C161 D(npcObjectId) C(0) C(rowCount) C(0)，每列 D(order) D(quantity) | S70 D(npcObjectId) H(n)，每列 D(order) H(icon) D(price) S(name) C(statusLength) bytes；n>0 時尾 H(currency=7) | `NpcPackets.Shop`、`ValidateShop`、S70 parser |
| NPC 販售 | action=`sell`；C161 type=1，每列 D(inventoryObjectId) D(quantity) | S65 D(npcObjectId) H(n)，每列 D(inventoryObjectId) D(price) | 同上；名稱與數量取目前背包 |
| 個人倉庫存入 | `deposit` 開本地背包選單；C161 type=2，每列 D(inventoryObjectId) D(quantity) | 背包 S24／S57 等 | `NpcServices.cs`、`ServicePackets.Warehouse` |
| 個人倉庫領取 | C125 action=`retrieve`；選取後 C161 type=3，每列 D(warehouseObjectId) D(quantity) | S176 D(npcObjectId) H(n) C(3)，每列 D(itemObjectId) C(use) H(icon) C(bless) D(count) C(identified) S(name)，尾 D(fee) | `ReceiveNpcService`／`ValidateService` |
| 私商購買 | C47 C(0) D(ownerObjectId)；C161 type=0，每列 D(回包 C order) D(quantity) | S140 C(0) D(ownerObjectId) H(n)，每列 C(order) C(bless) H(icon) D(count) D(price) S(name) C(0) | `PrivateShop.cs`、`PrivateShopUi.cs` |
| 直接交易 | C2 邀請（無對象 ID）；C121 H(0) D(challenge) H(252) C(1/0) 接受／拒絕；C37 D(itemObjectId) D(count)；C71 確認；C86 取消 | S219 H(0) D(challenge) H(252) S(peerName)；S52 S(peerName) 開窗；S35 C(side) H(icon) S(numberedName) C(bless)；S112 C(0 完成／1 取消) | `Trade.cs`、`TradeUi.cs` |
| 普通肉丟／撿 | C25／C112 H(x) H(y) D(objectId) D(count) | S87 地面物件、S120 移除、S15／S24／S57 背包、S158/action15 拾取動作 | `Ground.cs`、`Inventory.cs` |

`C_NPCAction` 分派 buy/sell/retrieve；`C_Result` 依 resultType 分派 NPC、私商與倉庫。S70 的 order 是實際販售陣列序號，**不是** item_id 40056，也不保證等於 SQL 的 order_id。S65 沒有物品名稱欄位。倉庫 ObjectId 也不保證等於原背包 ID，取出後要重新選擇背包物品。

伺服器規則：

- `ShopTable` 依 `order_id,item_id` 排序；selling_price>0 才上架，pack_count=0 轉成 1；購買金額由 `L1Shop`、稅與倍率處理。實際單價以 S70 為準。沒有獨立「NPC 成交成功」回包，背包更新不能單獨當作整筆成交證明。
- 個人倉庫要求等級至少 5；角色須在可互動的倉庫 NPC 附近。存入免費，原服驗證可轉移、封印、時限、使用中寵物／娃娃及容量；領取每個選取列 30 金幣，**不是每個堆疊單位 30**。本次不擴充血盟、精靈等其他倉庫類型。
- `C_ShopList` 要求購買角色自己也站在 `isUsableShop` 區域，單純靠近店主不足。S140 不提供貨幣 item_id；店主庫存、價格與實際扣款由伺服器當下判定。PC 店主清單 H(n) 可能含未發出的庫存項，必須保留 C(order) 的缺號；本次每次只買一列。逾時未結案查詢沿用既有停用／重登保護。
- `FaceToFace` 以雙方相鄰座標及相反 heading 找角色；C2 不帶目標 ID。兩邊依 S52 開窗，S35 的 side=0 為自己、1 為對方，數量在顯示名稱內。`L1Trade` 兩方均確認才提交；確認後不能再加物品。取消與失敗返還以原服背包為準。

## 本次修正與離線證據

- S70／S65 只在該 NPC 互動仍為 `waiting` 且不是技能／倉庫專用等待時接收為可提交清單。已提交後的重複 S70 不再重新開放購買；S70 也不能取代等待中的倉庫清單。
- S112 success 必須本次已收到 S52 且自己的確認已送出，才可標為 completed。邀請階段或尚未確認時的 success 被忽略；取消回包仍可解除等待。
- 不預扣金幣、增加物品或假造完成。協議沒有 request/session ID，不能宣稱能區分所有跨次晚到回包；本修正縮小接收狀態範圍。
- `engine/_integration/dev/npc-tests/` 直接載入本次正式產品 DLL；記憶體 sender、合成封包、SocketGuard，83 項檢查。覆蓋購買與販售、存入與領取（每列費）、私商缺號購買、兩個離線客戶端直接交易、普通肉丟1／撿1／丟10／撿10；另測數量、重複提交、錯誤目標、晚到、逾時、取消、拒絕與送出前目標移除。
- 地面測試核對權威數量及送包；拾取動畫的回包階段刻意設為地圖未載入，不依賴遊戲素材，**未驗證動畫渲染**。合成兩端也未執行 Java 伺服器或持久化。

## 給 T075 的合法購肉路線

**NPC：潘朵拉，template 70014，說話之島 map 0，固定出生 `(32645,32951)`。** `npcaction` 指向 `pandora`／`pandora5`；`shop` 有普通肉 40056，基價 1、pack_count=0→1、daily limit=0。請使用回包中的「肉／$23」，不要以其他肉類替代。

起點為 map 0 `(32600,32940)`，終點 `(32644,32951)` 距 NPC 一格。以下每段內逐格前進，放開鍵後核對座標再走下一段；先將焦點還給遊戲。鍵位依 `InputState` 的斜視角映射，**不是把地圖 y+ 當成螢幕 Down 鍵**。長按可能越站，建議短按並以座標為準。

| 段 | 地圖方向／heading | 對應鍵 | 格數 | 到達座標 |
|---|---|---|---:|---|
| 1 | y+／4 | PageDown（或 NumPad3） | 6 | 32600,32946 |
| 2 | x+／2 | PageUp（或 NumPad9） | 22 | 32622,32946 |
| 3 | y+／4 | PageDown | 5 | 32622,32951 |
| 4 | x+／2 | PageUp | 9 | 32631,32951 |
| 5 | y+／4 | PageDown | 1 | 32631,32952 |
| 6 | x+／2 | PageUp | 13 | 32644,32952 |
| 7 | y−／0 | Home（或 NumPad7） | 1 | 32644,32951 |

**靜態已驗證**：57 步全部通過本機伺服器 map0 與引擎實際 maps/terrain/0.bin 的通行交集，避開靜態 NPC 出生格與門範圍；正式 `OriginalMapCache` 逐步 Passable＋Move 通過（0 次換區），終點可互動距離；本機 `pandora` 模板的 buy 選項可用。來源與可重跑資料在 `work/issue-133/route.json`。沒有把地圖複製進 repo。

**待真機驗證**：目前測服資料是否與此 SQL 相同、動態角色／怪物阻擋、實際鍵盤終點、購買與扣款。若出生點不同，先以正常遊戲操作到起點；也可用下方 checker 的 `--start X Y` 從當前合法座標產生新路線，再做快取核驗。不能用 GM 傳送、改 DB、補物品替代購買。

到店後：

1. 記錄金幣與普通肉數量；選取可見潘朵拉→交談→購買→普通肉數量 10→提交一次。核對金幣依 S70 單價減少、普通肉增加 10；若不符則保留原服訊息，不連點重送。
2. 關閉 NPC 選單。背包選普通肉，丟 1；確認背包減 1、地面增 1；選該地面物件撿 1，核對恢復。再完整做丟 10／撿 10。每次等權威更新再重新選取，不沿用已失效 ObjectId。
3. 留存每步截圖、金幣／肉前後數量及 C161、C25、C112 與相應回包時間。不要吃肉或拿任務肉替代。若做拾取數量更正，先輸入 0 確認未送包，再改 1 完成；不要把「送出成功」當成成交或拾取成功。
4. 倉庫另備等級≥5、可存的普通物品與至少30金幣，核對存前／存後／取後。私商需已有合法擺攤測試角色，購買者也要站在允許區；直接交易用兩個授權測試角色相鄰面對面，各自核對報價再確認。沒有第二角色或賣家時，交調度器備妥，不改資料製造條件。

重跑（於本 worktree，所有輸出留 scratch；不連線）：

```powershell
py -3.12 -B -X utf8 engine/_integration/dev/npc-tests/verify-route.py --server-root 'D:\lineage開發\1-伺服器' --assets-root 'D:\lineage開發\TW1608262200研究\engine\assets' --output scratch/route.json
dotnet run --project engine/_integration/dev/npc-tests/NpcChecks.csproj -c Release --no-restore -- --route 'D:\lineage開發\TW1608262200研究\engine\assets' scratch/route.json scratch/cache-route.json
```

先依建置文件建出本 worktree 的 `engine/build/client-product-dev/P11.ClientEngine.dll` 並 restore `NpcChecks.csproj`。路線核驗讀既有素材 junction，不建立或修改 junction。一般83項測試不帶 `--route`，不需要任何素材；CI 加入獨立 Windows `npc-offline` job，既有 Linux build 維持原流程。

## 來源 SHA-256

以下由本次實際讀取檔案計算。Java 路徑相對於 `D:\lineage開發\1-伺服器`；只記識別值與契約摘要，不收錄原始碼。這些來源未啟動，不能據此聲稱執行中 JAR／DB 一致。

| 來源 | SHA-256 |
|---|---|
| `src/com/lineage/echo/OpcodesClient.java` | `3b9df4518cad4ab3021d7a6454f88fe4513bd715eb3eb461ffaa873a64fe8931` |
| `src/com/lineage/server/serverpackets/OpcodesServer.java` | `9044cb98cf8a0548a421fbb05e6cdcef26147b2a2b85a1ef896fde8a8016f152` |
| `src/com/lineage/server/clientpackets/C_NPCAction.java` | `280ef4bb7316ca61e199ae97a71405ec45affc37a8b4955c4881a891b298b0a1` |
| `src/com/lineage/server/clientpackets/C_Result.java` | `4f616c8995cd1a43c6a1f4a133c98c68e6d686de0d89b05fcd910b86e155bae5` |
| `src/com/lineage/server/clientpackets/C_ShopList.java` | `1aef96d5889fa3590767e99287782e8f06cf7de496272b4da12e48d0197b37c5` |
| `src/com/lineage/server/clientpackets/C_Shop.java` | `cd94e74004c18471e01faf290e8f2320193fb2fcafadc94998fbcedc288b9bff` |
| `src/com/lineage/server/clientpackets/C_Trade.java` | `6466b1c760cbd96da6e1e417fd411557e2b3c781bfd7ff8408447822b4b529ab` |
| `src/com/lineage/server/clientpackets/C_TradeAddItem.java` | `668c3c26f33fa0b99d2bafe6dec17cf7a2a1a5ca39cf07c2dc2a4a3039825e71` |
| `src/com/lineage/server/clientpackets/C_TradeOK.java` | `af68f2ecc61d45b77b1579ad7325440f3d637cafd42a465bf6d22fdf74c5e749` |
| `src/com/lineage/server/clientpackets/C_TradeCancel.java` | `40c908a4a606c82917ee448e837abd8c0d55f06198e6b48bf42a2e77cee730a4` |
| `src/com/lineage/server/serverpackets/S_ShopSellList.java` | `5dfec708356a1c95755295393d4b9cd3a984bf26fa16660d6bf1de87f873a551` |
| `src/com/lineage/server/serverpackets/S_ShopBuyList.java` | `3c2d3d1a37644c9eabeeb221282009dd8d9199ed1f166563235c5e703e922ff8` |
| `src/com/lineage/server/serverpackets/S_RetrieveList.java` | `1bdf02501d592d5658ed3e5a3d677ae592ff488807470d3e1e7c6bb1c6779f57` |
| `src/com/lineage/server/serverpackets/S_PrivateShop.java` | `a4e31e41005c0e274cbaa4782a3509f6947be64b350369f1d9e8e6b6c52e2eff` |
| `src/com/lineage/server/serverpackets/S_TradeAddItem.java` | `0a04d8f3c942a160e0ebfbb95f82f6e68837bd4d105828ecc08d09c7a5a5a00d` |
| `src/com/lineage/server/serverpackets/S_TradeStatus.java` | `f793630e477aa9635d6467766babf3468addcc6e011554e98d674b23c0d5316e` |
| `src/com/lineage/server/datatables/ShopTable.java` | `d4679eaa30f041e0034d74581d50c91ac6b90fdf76c2f7819f9b7ac00ca12fad` |
| `src/com/lineage/server/model/L1Trade.java` | `1501fbfd18060776e1da322ca430f9838e42c1af8100f566b5e885ec11fb06c3` |
| `src/com/lineage/server/utils/FaceToFace.java` | `c6235d8150ee64fe890e8c5ad12c25623829cd8848e6efa1e40432db68ed7ce3` |
| `src/com/lineage/server/model/map/L1V1Map.java` | `855604d366edbe65dea4793d35c6535689e4b42f88c5fe4793084592a2429695` |
| `src/com/lineage/TextMapReader.java` | `d7f17ff3ef34162c13d126d37b2e3f2b0aec1a661aa4043a1143a900efb13059` |
| `db/380_db.sql` | `2afbc066fa7dbde391169050deeed206a20a39afbc0390378ddf2800cd3328b5` |
| `maps/0.txt` | `ece432f3af3cc20bcbf2722dea31a379a9e6ee2b05d7db8940f35ca52ec6bc44` |
