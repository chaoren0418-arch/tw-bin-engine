# T129 變身封包（#263）

2026-11-07；以 `docs/reference/server-protocol.md` 的 S_ChangeShape 與 `server-rules.md` R11／R25 為基礎，補充唯讀來源查核。沒有啟動伺服器、讀取設定或查詢 DB。

## 已驗證的 wire 格式

欄位採 little-endian，C／H／D 分別為 1／2／4 bytes；S 是 CP950、NUL 結尾字串。以下含 opcode，不含傳輸長度，尾端依既有 LoginReader 規則補零至 8 的倍數。

| 封包 | 有序欄位 | 處理 |
|---|---|---|
| S76 一般變身 | C(76), D(objectId), H(gfx), H(weaponTakeoff ? 0 : 29) | 更新當前 gfx；0 將武器姿勢設為 0，29 保留現值，不把 29 當 action。 |
| S76 自動刷新／解除 | 與一般變身相同 | 直接採伺服器送來的 gfx，包含合法的 gfx 0；不以本地職業或到期時間恢復。 |
| S76 NPC 外觀（寵物／迷魅 overload） | C(76), D(npcId), D(pcId), H(gfx), S(pcName) | 只更新 npcId 的 gfx。完整解析 owner ID／名稱，不拿 owner ID 當受變身物件、不憑此封包改寫既有所有權或名稱。 |
| S230 SPOLY | 本次沒有可驗證 serializer | 保持未處理；不能當成 S76。 |

前三個 Java 公開建構子均委派同一 CDHH serializer，第四個使用 CDDHS。本模組沒有新增 C 封包。

一般格式為 9 bytes 加 7 bytes 零；短名稱的 NPC 格式也可能補成 16 bytes。因此不能只比較封包長度。先辨識「16 bytes 且 File Offset 9..15 全零」的一般布局，再檢查尾欄只能是 0／29；其餘按 NPC 布局完整解析並驗證尾端 padding。NPC owner 必須非零、名稱非空且沒有控制字元；這是引擎對有效角色名稱的接收限制。所有欄位解析成功後才變更世界狀態。

S76 不含移速、勇水速度、攻速或動作編號。Speed／Brave 及既有 MotionSpeed／MotionBrave 不因變身而改寫；S87、S255、S67 的值保持權威。S119 繼續更新武器姿勢，S158／S30 繼續更新攻擊動作。清除舊外觀的進行中非死亡動畫，後續動作沿用既有、以當前 gfx／weapon 選取的已驗證動作資料；不另設變身倍率或猜測速度。

## 來源核驗與 SPOLY 結論

來源根目錄為 `D:\lineage開發\1-伺服器\src\com\lineage\server`，只記錄摘要，未複製來源。

| 相對路徑 | SHA-256 | 核驗點 |
|---|---|---|
| `serverpackets/S_ChangeShape.java` | `b23ec55370d70c3536d1d1aaa0b73921b9c1a83b533bcdbc695e32cb9846794d` | 21／29／40／45／59 行建構子及 serializer，與既有協定表一致。 |
| `serverpackets/OpcodesServer.java` | `9044cb98cf8a0548a421fbb05e6cdcef26147b2a2b85a1ef896fde8a8016f152` | POLY=76；SPOLY=230；常數表沒有另一個值 230 的 S opcode。 |
| `model/L1PolyMorph.java` | `8d19b2262ac1cf2ffb8a79b6157cf16ddc05a42dc2171f6739bdc81f198926b4` | doPoly 路徑依 weaponTakeoff 送 S76，武器更新另送 S119；undoPolyLocked 送角色 classId／怪物原 gfx。 |

搜尋 `server` 下 Java 的 `S_OPCODE_SPOLY` 只命中宣告；搜尋 `serverpackets` 下 `230` 也只命中該宣告。沒有同值別名與可啟用的 serializer，因此本次沒有新增 S230 case。這是目前來源快照的結論，不推論其他版本或部署 JAR。

## 狀態與範圍

- 當前外觀直接保存在現有 `OwnActor.Gfx/Weapon`、`WorldObject.Gfx/Action`；繪圖、動作節奏與送出檢查原本均使用這些值，沒有另建可能跨世界洩漏的 gfx 快取。
- 未知物件只增加既有 unknown-update 計數；S120、S206、Reset、後續 S87 自然淘汰舊外觀。保留死亡狀態，不以變身復活物件。
- 自身 AppearanceRevision、目標 TargetRevision 使舊外觀下排隊的操作失效。未更改選取的物件身分。
- `WorldSession.ReceiveCore` 只新增一個 S76 case。為使該 case 可達，移除 `WorldObjects.ReceiveObjectDelta` 原本提前攔截 S76 的兩行待更新邏輯；沒有改寫其他 delta。無須新增繪圖覆寫點或改動 `ObjectRendering.cs`。
- 缺少已驗證 profile／sprite 的 gfx 仍遵守既有不支援保護。封包狀態更新通過，不代表所有變身素材已可顯示；真實圖片與節奏留待 M6 smoke。
