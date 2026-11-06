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

## 原任務檔案範圍的缺口

箭矢命中在 `ProjectileRendering.cs/ComposeProjectile` 直接建立 action=2，繞過 `AnimateCombat`；`WorldSession.Reset` 也沒有清單清理掛接點。這兩個檔案不在 #215 指定範圍，已提出各一處最小掛接的範圍確認，未取得確認前不修改。

因此目前清單隨每次合法 sub2（含空清單）更新且各 WorldSession 獨立，但同一 WorldSession 的登出／切角 Reset 不清空欄位；若後續登入沒有送新清單，舊值可能沿用。跨圖應保留清單；不得無條件接到也會在 S206 執行的 ResetCombat。

上述兩項是已確認的實作限制，不能以合成測試通過視為完整受擊路徑或連線生命週期已完成。現行 JAR 的實際發送、玩家變身語義與畫面效果仍交 M6 smoke 核對。
