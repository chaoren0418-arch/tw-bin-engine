# 門封包補充（Issue #322）

本頁是 2026-11-07 的唯讀來源核對，並非真機或部署 JAR 驗收。基礎參考為 `docs/reference/server-protocol.md` 的 C_Door、S_DoorPack、S_Door，以及 `server-rules.md` 的 R07–R11、R27。沒有複製伺服器原始碼或讀取 DB／機密設定。

## 已驗證的格式

| 封包 | payload（不含 opcode、含序列順序） | 含義 |
|---|---|---|
| C41 | **H(x)、H(y)、D(objid)** | `C_Door.start` 忽略兩個 H，再讀物件 ID。任務摘要及總表的 DHH 錯誤；參考文件的詳細欄位表本來就是 HHD，且 SHA 與目前來源相同。採用 HHD 才能讓處理器取得正確 ID。 |
| S87 DoorPack | HH 座標、D ID、H gfx、C action，後接 CHARPACK 固定欄位 | action：28 開、29 關、32–36 損傷、37 毀壞。heading/light/speed=0，exp=1，lawful=0，四個字串皆空，flags/clanId/rank/brave/level/unknown=0，HP/tail1/tail2=255。原始欄位共 37 bytes（含 opcode），補零至 40 bytes。 |
| S158 | D(objid)、C(action) | 門開／關及毀壞動作。只攔截已匹配 DoorPack 的當前物件；其他物件繼續既有分派。 |
| S209 | H(entranceX)、H(entranceY)、C(direction)、C(passable) | direction=0／1；passable=0 可通行、1 阻擋。沒有物件 ID，與外觀更新分開處理。 |

S87 的 heading 固定為 0，**不是門方向**。方向只能由 S209 取得；不把 S87 的 action、gfx 或發出的 C41 推算成通行狀態。

`L1DoorInstance.sendDoorPacket` 對寬門逐段送 S209。`L1V1Map.set_door_0/1` 將該入口及左右（方向 0）或上下（方向 1）鄰格加入／移除門阻擋層。本模組依這個已送達的狀態更新動態碰撞；斜走另檢查兩個正交鄰格，避免穿角。開門只撤去門層，仍須通過原有靜態地形檢查。

`WorldInteractionGuard.door` 允許目前可見門（含寬門可見端）操作，不是固定近戰兩格範圍；客戶端保留指標可見性、世界／連線世代、死亡、交易與忙碌閘門，實際鑰匙／盟屋／特殊門權限仍由伺服器決定。

## 生命週期及限制

- CHARPACK 沒有明確的型別 discriminator；本模組以完整 DoorPack 固定欄位及門動作集合識別，不能宣稱任何可能的未來共用封包永不撞型。
- 每個門物件保存當前不可變 `WorldObject` 實例；S120、重送完整資料、同 ID 換物件後舊請求失效。換圖、同圖 S206、傳送與 Reset 以 `WorldEpoch` 清除門資料。
- S209 不含物件 ID；S120 不會推算或清除其通行狀態。通行狀態保留至後續 S209 或世界重設。
- C41 只提出請求，不改 action／碰撞、不自動重送。發送閘門前再次驗證指標、物件實例及角色狀態。
- 本次限定的檔案範圍內，既有渲染器仍只支援已核驗的 gfx 2346 靜態 open／close 幀；其他 gfx、損傷／毀壞素材、動作推進與滑鼠開門通行仍需要擴充檔案範圍。詳見任務報告。

## 唯讀來源 SHA-256

路徑相對於 `D:\lineage開發\1-伺服器\src\com\lineage\server`。

| 路徑 | SHA-256 |
|---|---|
| `clientpackets/C_Door.java` | `34b3fc7271b49be0f5968acf3320c539fed750ccd23b8025de2aa27849f734a5` |
| `serverpackets/S_DoorPack.java` | `e1e26b0461705ece45a2d6c8d326d7023418a43fbd37b7048527de419bc37a48` |
| `serverpackets/S_Door.java` | `9533efa6d0724e7ea307e9e78e16ff335db5da315df2bb67b32e57cbaa703fa3` |
| `model/Instance/L1DoorInstance.java` | `a2620d562e27c1eb1098112fab0301a51a4ef0e294a0a26a090faef70413d6c9` |
| `model/map/L1V1Map.java` | `855604d366edbe65dea4793d35c6535689e4b42f88c5fe4793084592a2429695` |
| `utils/WorldInteractionGuard.java` | `a72159142ff5964e09b0cbe10b30420a947304de871d084c1d3ad63d1d2bbe8f` |
| `ActionCodes.java` | `4ce21418e157764592cd633ebe88ea93dd6e546264b573e866bfcf65120d236f` |
