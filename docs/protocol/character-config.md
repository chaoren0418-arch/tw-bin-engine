# 角色快捷設定（T138／#324）

## 已核對的伺服器格式

依 `server-protocol.md`、`server-rules.md` 查表後，唯讀補查本機處理器及持久化服務。未執行伺服器、查詢 DB 或讀取機密設定。

| 封包 | 明文布局（不含 frame） |
|---|---|
| C244 | `C(244), D(N+3), bytes[N], zero padding to 8` |
| S250／41 | `C(250), C(41), D(N), stored bytes, C(0), zero padding to 8` |

整數為 little endian。C 的 preflight 要求宣告長度至少 3，且減 3 後不大於剩餘資料。處理器保存 `N = readD()-3`，`readByte()` 取得全部剩餘位元組，**包含 C 的補齊資料**。持久化層各自檢查 N 與資料長度不超過 65,535，沒有要求兩者相等。伺服器不解讀 blob 內容。

登入初始化的 `C_LoginToServer.start` 在自身狀態已送出後，查角色設定；存在時才送 S250／41，沒有設定則不送。`S_PacketBoxConfig` 的 `writeByte` 會再附加一個零。設定儲存沒有成功 ACK，因此送出成功只表示傳輸成功，不能宣稱已寫入資料庫。

引擎解碼接受兩種嚴格尾長：直接保存 N bytes 的回包，以及保存 C 補齊資料的回包。宣告 N 之後全部必須為零，拒絕截斷、額外區塊、非零尾段、超限／溢位。blob 內的零值不作字串終止符。

`LoginFrames` 最大 body 為 10,238 bytes。考慮 C 與 S 的補齊和額外終止零，客戶端送出的 N 最大 **10,219**，其 C body 為 10,224、回傳 S body 為 10,232。這是引擎的可往返限制，不是把伺服器的 65,535 上限改寫為相同數字。

## 引擎資料格式及接點

伺服器沒有快捷槽的欄位定義，本模組使用明確版本的引擎 blob，**未聲稱相容原 BIN 快捷設定布局**：

| 欄位 | 長度／意義 |
|---|---|
| magic | ASCII `TWCC`，4 bytes |
| version / count | `C(1), C(4)` |
| 四個槽 | 每槽 `C(kind), D(id)`；0＝空槽且 id＝0，1＝道具 object ID，2＝技能 ID |

完整 v1 長度 26 bytes。空 blob 代表清空四槽。未知版本、非本格式、無效 kind／ID 皆保留原始 blob，禁止 HUD 自動覆寫；UI 明確提示該情況的變更僅在本機有效。沒有把未知資料猜成已還原快捷列。

沿用現有 F1–F4 四格 HUD，指派／清除時送 C244，重登收到設定後還原。未擴充格數；題目只在沒有快捷列時要求最小十格。`KeyboardNavigation.cs` 不需變更，既有 F6–F12 的連線、診斷、焦點與截圖功能保持原有分派。

`WorldSession.ReceiveCore` 只增加 S250／41 一個條件 case，其他 S250 子碼仍走原有分派。`PlayerHudView.UpdateState` 只增加一個資料掛載呼叫。儲存走既有傳送鎖與有效性檢查，綁定 connection generation、actor、world epoch 與收到的設定版本；取消、切換世界或新設定抵達會使尚未送出的請求失效。世界資料的讀取按目前 generation／自身角色隔離；跨地圖保留設定，重登不採用舊本機槽快取。缺少或已移除的道具／技能保留 binding，既有 HUD 的 Resolve／Activate 防護不會執行不可用項目。

目前客戶端切角仍依既有登入流程重建連線世代；若未來加入同連線、同角色重入，應在該功能的 reset 接點明確清除模組快取，不只依 generation 與角色 ID。

## 唯讀來源識別

根目錄：`D:\lineage開發\1-伺服器\src\com\lineage\server`。以下是原始碼 SHA-256，非部署 JAR 或實機驗證；沒有引用二進位地址。

| 相對路徑 | SHA-256 |
|---|---|
| `clientpackets/C_CharcterConfig.java` | `4d935d45193e99d3598da1067dd2422c27194800d748bff0441c4d73cb01e555` |
| `clientpackets/ClientPacketStructure.java` | `ca88c05673188ec1b99a961be0d67561808f9bd42adf8a937b9ebda6767e96a9` |
| `clientpackets/ClientBasePacket.java` | `83b48cc84680c6238d2491c71a776ec5beb542c39b6232f3a1b4e620aaf77636` |
| `clientpackets/C_LoginToServer.java` | `c2eedcb6d78d9da7f33af485c596d393d49818a0a05bc5a37cbedb79bcfc9de4` |
| `serverpackets/S_PacketBoxConfig.java` | `2c94a77b314a0e38d01dbc0a47830913e52a7014502e543043bf5814ab3d0ba5` |
| `serverpackets/ServerBasePacket.java` | `3348c199e9694bd1eec050b130f28ab99590cae16f0047964c17b32df309a439` |
| `model/config/CharacterConfigService.java` | `dc63671d106cf7e6d77f01cbcf5b595c2f542dcd84bdabbc3c64348a20ba8b59` |
