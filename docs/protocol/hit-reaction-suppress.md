# S242/sub2 受擊反應抑制（#215／T120）

## 格式與來源

依 #132 `server-protocol.md` 的 S242／S_HitReactionSuppressList、`server-rules.md` R20，以及唯讀核對的主服來源：

| 欄位 | 型別 | 語義 |
|---|---|---|
| opcode | U8 | 242／0xF2 |
| subopcode | U8 | 2 |
| count | U16 little-endian | 0..512 |
| gfx | count × U16 little-endian | 完整抑制清單；合法值包括 0 與 65535 |
| padding | 0..7 個零位元組 | 沿用 LoginReader.Finish 的 8-byte 對齊規則 |

來源根目錄為 `D:\lineage開發\1-伺服器\src\com\lineage\server\`；只讀檔，不讀 DB／設定，不複製原始碼。

| 相對來源 | SHA-256 |
|---|---|
| `serverpackets/S_HitReactionSuppressList.java` | `f4c064afe45f593ee8cce910fd41b554df704f9a0a955d8998e3466d5fd3aa1c` |
| `clientpackets/C_LoginToServer.java` | `c2eedcb6d78d9da7f33af485c596d393d49818a0a05bc5a37cbedb79bcfc9de4` |
| `datatables/HitReactionSuppressTable.java` | `b68b67fe6b396b21d6500d161532c0881cd0803f68b9a48d4c360b47c5ea29a5` |

## 語義與實作

- `C_LoginToServer.start` 在 S_EnterGame 之後、背包同步之前，僅在 ClientLogin=2 發送此完整清單。空清單也發送。
- `resolveTargetObjId` 在 RO／ClientLogin=2 保留真實 targetId，由客戶端依 gfx 抑制；非 RO 的伺服器分支可對命中表內的 NPC 改送 targetId=0。不可把 targetId=0 解釋為不存在攻擊對象，也不可自行補造 targetId。
- 伺服器的非 RO `isSuppressed` 僅針對 NPC，但 sub2 本身沒有物件類別欄位；客戶端依當前 gfx 套用清單，不從 CHARPACK flags 推測種族。自身與其他物件使用相同 gfx 查詢規則。
- 接收時先讀入新集合、確認 count、所有欄位與 padding，再一次替換；重複 gfx 去重、空清單清空、畸形封包保留上次有效清單並拋出既有 ProtocolException。
- `Inventory.cs` 的 sub2 提前返回，避免背包 revision、道具狀態或交易確認等副作用；sub1 路徑不變。
- `Combat.cs/AnimateCombat` 僅對 action=2 查詢並返回，涵蓋近戰 S30 與 S158 的共同動畫路徑。攻擊者動畫、targetId、傷害欄、HP 與死亡依原流程處理。

## 箭矢與 session 生命週期（第 3 輪裁定後補齊）

依主控第 3 輪擴充範圍，`ProjectileRendering.cs/ComposeProjectile` 在箭矢命中建立 action=2 的條件只增加一次 `IsHitReactionSuppressed(p.Target)` 查詢，與 `Combat.cs` 共用同一函式。列入清單的目標不建立命中受擊動畫，未列入者維持原流程；飛行、傷害報告／數字資料及 HP／死亡更新不變。

`WorldSession.Reset` 呼叫 `ResetHitReactionSuppress`，由模組執行集合 `Clear()`。既有取消／登出、斷線／連線失敗、重新登入前及同連線選角的路徑都經過此 Reset，因此新登入即使未送 sub2 也不沿用舊清單。S206 跨圖只會呼叫 `ResetCombat` 等地圖重置，不清空此清單；同一 session 每次合法 sub2 仍完整替換它。

離線回歸直接執行產品 DLL：箭矢列入／未列入各一例，檢查命中動畫、施法保留、傷害欄與 HP／死亡；session Reset、Cancel 及記憶體 sender 的同連線選角後，重建相同 objectId／gfx 且不送 sub2，確認清單已清空；S206 後重建物件則確認抑制仍有效。測試已接入既有 `DemoChecks` 與 CI 的 `combat-admission` 入口。

現行 JAR 的實際發送、玩家變身語義與畫面效果仍交 M6 smoke 核對，離線合成測試不代表真機驗收。
