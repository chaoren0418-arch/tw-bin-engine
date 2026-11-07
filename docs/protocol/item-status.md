# T121 物品 status 與裝備槽位契約（#216）

## 來源與範圍

依 #132 差異 17、`docs/reference/server-protocol.md` 的 S5／S15／S24／S242、S64，以及 `server-rules.md` R26。2026-11-07 唯讀核對下列 `D:\lineage開發\1-伺服器\src\com\lineage\server\` 來源；未複製來源碼、資源，未連線服務或資料庫。

| 相對來源 | SHA-256 |
|---|---|
| `model/Instance/L1ItemStatus.java` | `bed29c5ca1ea4ea5990d2545b8dd7c702bc40f90e492077b6a76d99efc854d00` |
| `serverpackets/S_ItemStatus.java` | `6e69ff83cbbe91f7a8914a2e43bda498eb0d2fc5808e5218edea5cfe1d513eac` |
| `serverpackets/S_EquipmentWindow.java` | `8cc712915a3aa7675b0c0a81b17e2aa0aeaaf5edbdcf14b9983919d6a7e6db08` |
| `serverpackets/S_InvList.java` | `cd3c0c20a7285b0ef32d9305e0559cf7ea02030c2ff0665363ffad91521f2820` |

以下位置是 Java **來源行號**，不是 File Offset／RVA／VA。主要依據：L1ItemStatus 的 98–127（有號 HP/SP）、154–170（重量與入口）、315–382（箭、食物、照明、防具）、435–574（能力與抗性）、1038–1253（武器）、1339–1474（一般物品、寵物）、2250–2274（元素強化名稱）；S_ItemStatus 27–121、S_EquipmentWindow 43–67、S_InvList 40–61。

## status 解碼

C／H／D 各為 1／2／4 bytes，H／D 為 little-endian；S 是 CP950／Big5、NUL 結尾。以下長度不包含 tag。

| Tag | 尾段 | 結構化語義 |
|---|---|---|
| `01` | C C C D | 小型傷害、大型傷害、材質、重量 |
| `02` | signed C | 強化值；缺少時保留未知，不從名稱猜測 |
| `03` | unsigned C | 損傷度；不是剩餘耐久百分比 |
| `04` | 無 | 雙手武器 |
| `05`／`06` | signed C | 命中／額外傷害（寵物格式） |
| `07` | unsigned C | 職業 bit mask |
| `08`–`0D` | signed C | STR、DEX、CON、WIS、INT、CHA |
| `0E` | signed H | HP 上限加成 |
| `0F` | signed H，可能後接 `21 C` | 未加限定碼或限定碼 D6＝MR；01–06＝寒冰、石化、睡眠、暗黑、昏迷、支撐耐性 |
| `11` | signed C | SP |
| `12` | 無 | 加速效果 |
| `13` | C C C D | 基礎防禦幅度、材質、格式標記、重量；標記 27 或寵物 FF **不是**獨立 tag |
| `15`／`16` | H C D | 營養／照明範圍、材質、重量 |
| `17` | C D | 一般物品材質、重量 |
| `1B`–`1E` | signed C | 火、水、風、地抗性 |
| `20` | signed C | MP 上限加成 |
| `27` | S | 伺服器說明文字，保留重複項與原文；顯示時去除顏色控制碼 |

`ItemStatus.Attributes` 保留順序、tag、語義 key、數值或文字。`Enchantment`、`Damage` 提供常用讀取，其他數值可透過 `Number(key)` 取得。`InventoryItem.StatusHex` 的 init setter 在 S24／S242 的 `with` 更新時重新解析，名稱、數量、顏色更新不會產生過期的屬性快取。

未知 tag 無通用長度，必須停止，不掃描猜測下一個 tag。截斷數值、缺少文字終止符、無效 Big5、未知抗性限定碼同樣停止，保留已完成欄位、`ErrorOffset`（blob 內 byte offset）、錯誤種類與 `UnparsedHex`。封包外層截斷仍依既有規則拒絕，不能部分寫入背包。完整 `StatusHex` 始終保留。

空 blob 不表示零能力，也不單獨證明未鑑定；可能尚待長狀態回包或 legacy inline 溢位。超過 signed HP／MP 等範圍時伺服器可改送文字，本次保留文字，不將自由文字強行轉為數值。元素強化種類／階級目前出現在名稱組合，沒有本次核對可用的獨立 tag；不從名稱反推。四元素抗性與元素強化不可混用。

## 背包旗標與裝備權威

S5／S15 的 flags：01＝已鑑定，02＝不可交易，04＝不可刪除，08＝不可強化，20＝封印。`InventoryItem` 暴露已鑑定、模板可交易、可刪除、封印欄位；封印也讀 Bless 的 80 bit。可交易欄位是最近背包旗標提供的資訊，不代表繞過伺服器的即時交易限制。S24 沒有 flags，不能憑 status 推算更新旗標。

裝備權威沿用 S64：subtype 65 的完整清單（C count，逐筆 D objectId、H slot、H reserved）；subtype 66 的絕對更新（D objectId、C slot、C equipped）。`Equipment.cs` 僅新增讀取介面，沒有改動解析或傳送邏輯。

`ReadItem` 將每筆物品綁定所屬 session 的權威查詢；`InventoryItem.Equipped` 不再讀名稱。完整清單尚未到達且該 ID 沒有絕對更新時回傳 null；完整清單未列出或收到卸下為 false；穿上為 true；非裝備為 null。既有物品參考會即時讀取槽位，`with` 更新保持綁定；若需要歷史裝備快照，請儲存讀出的 bool? 值，不要保留物品參考當作歷史狀態。session Reset 清除權威狀態。裝備先於物品到達也可正確查詢。

## 離線驗證入口

測試在 `engine/_integration/dev/demo-tests/ItemStatusChecks.cs`，接入 `DemoChecks` 的 `item-status` 群組及預設入口，CI Windows job 明確執行。測試用合成封包、實際產品 DLL、SocketGuard，沒有啟動遊戲、視窗、伺服器或 DB。

```text
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-216/item-status-checks.json item-status
```

原生 ListBox tooltip 顯示 slot／待同步、旗標及已解碼屬性，停留期間也會刷新 S24／S242／S64 變化。實際滑鼠停留與畫面排版留待 dot 的 M6 smoke。
