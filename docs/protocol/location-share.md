# 座標分享協定（T144／#361）

## 2026-11-07 唯讀來源核對

依 `server-protocol.md` 的 C254／C_Windows 與 `server-rules.md` R29、R30 追查本機來源；以下只是欄位與行為摘要，沒有複製伺服器原始碼。

來源根目錄：`D:\lineage開發\1-伺服器\src\com\lineage`。

| 相對路徑 | SHA-256 |
|---|---|
| `server/clientpackets/C_Windows.java` | `2cb7e592dc2a449dc6421c2242d203ca11ca08ee63af8bb30e2d5cd323f299f0` |
| `server/clientpackets/ClientPacketStructure.java` | `ca88c05673188ec1b99a961be0d67561808f9bd42adf8a937b9ebda6767e96a9` |
| `server/serverpackets/S_PacketBoxLoc.java` | `1d8c641c6cd2c105cec528e2025507608f0c09efacc4e7d2ead49b2fbf238fad` |
| `server/serverpackets/S_ServerMessage.java` | `6f2ae74bc885da8d7807c816b1e139629cb14924ca38e176a44f031d26bcaf92` |
| `server/serverpackets/OpcodesServer.java` | `9044cb98cf8a0548a421fbb05e6cdcef26147b2a2b85a1ef896fde8a8016f152` |
| `echo/OpcodesClient.java` | `3b9df4518cad4ab3021d7a6454f88fe4513bd715eb3eb461ffaa873a64fe8931` |

## 已驗證的封包格式

`C`＝byte、`H`＝little-endian UInt16、`D`＝little-endian UInt32、`S`＝CP950 加 NUL。表格從明文 opcode 開始，不含線路長度及加密框架；引擎依既有契約補零至 8 bytes 倍數。

| 方向 | 明文欄位 | 行為 |
|---|---|---|
| C254 | `C254 C11 S(receiver) H(map) H(x) H(y) D(zone)` | `C_Windows.start` 的 `0x0b`；結構檢查為 `shhhd` |
| S250 | `C250 C111 S(sender) H(map) H(x) H(y) D(zone)` | 收件者收到 `S_PacketBoxLoc` |
| S71 | `C71 H1783 C1 S(receiver)` | 發送者收到「已發送座標位置給…」 |
| S71 | `C71 H1782 C0` | 發送者收到「無法找到該角色或角色不在線上」 |

本來源只按名稱找一位在線角色並轉送；沒有聊天頻道欄位，也沒有公開／血盟／隊伍廣播。伺服器直接轉送 client 傳入的 map/x/y/zone，不核對位置，也不把 zone 當作傳送授權或分流識別；接收內容不可當成經伺服器驗證的他人位置。

`zone` 的原生 UI 語意無法由以上轉送程式證明。引擎送中性 `0`，接收時完整保存 UInt32，不推導安全區、分流或可行走性；原客戶端互通仍待 smoke。

## 客戶端政策

- 聊天輸入 `/loc 角色名稱` 經既有 sender gate 呼叫 `SendLocation`；`/say /loc ...` 仍為一般文字。沒有選擇廣播頻道或允許手填任意座標。
- 使用當前 `World.MapId`／`World.Actor.X/Y`。既有移動沒有逐步 ACK，`Actor` 在移動後可能是本地預測；不能將它描述為最新伺服器確認位置，也不能使用過時的出生點替代目前位置。
- 收件名稱禁止空白、控制字及 CP950 無法編碼的字；API 最多 52 字，聊天 UI 仍受既有 52 字正文限制，因此 `/loc ` 入口最多輸入 47 字角色名。
- 共用聊天傳送中鎖與 1 秒本機節流；此間隔不是伺服器政策。送出前重驗 session、world、actor、可操作狀態及 map/x/y，排隊期間位置不同即取消。
- 只有伺服器回包才追加歷史；成功寫入 transport 只顯示「等待伺服器確認」。接收位置以型別欄位保存，不從普通聊天文字猜座標。
- S250 僅攔截 sub111；S71 僅攔截 1782／1783，其他 subtype／訊息保留原分派。全部欄位、參數數量與 padding 驗證成功後才修改歷史；歷史共用最多 100 筆及 reset。

## 尚待完成

`PlayerChat.cs` 只負責資料及文字格式；真正的 RichTextBox 與滑鼠事件在本任務允許範圍外的 `PlayerHudChat.cs`。目前已提供座標顯示文字及型別資料，尚未接上可點擊與位置標示。需核准最小 UI 掛接或另派 UI 任務；不可將文字顯示視為完整 T144 驗收。
