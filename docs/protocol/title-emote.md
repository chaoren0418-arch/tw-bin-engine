# T137 稱號與表情契約（#323）

依 `docs/reference/server-protocol.md`、`server-rules.md` 與 2026-11-07 唯讀處理器核對補充。沒有查詢資料庫、讀取機密配置或複製伺服器原始碼。

## 封包

| 方向／opcode | 順序欄位（不含 opcode） | 行為 |
|---|---|---|
| C90 `C_Title` | S 角色名稱、S 稱號 | CP950、NUL 結尾、既有 8-byte padding；不預測更新稱號 |
| S183 `S_CharTitle` | D 物件 ID、S 稱號 | 更新自己或可見物件；空稱號可清除；未知 ID 不建立物件 |
| C120 `C_ExtraCommand` | C 動作 | 只接受 66 思考、67 挑釁、68 敬禮、69 歡呼 |
| S158 `S_DoActionGFX` | D 物件 ID、C 動作 | 66–69 交本模組；其他動作留給原有分派 |

解析先完成長度／padding 驗證才改模型。原有 S87 的 Title 欄位直接參與繪製，S183 更新相同欄位，完整 S87 仍能替換舊物件。

## 伺服器規則與本機限制

- `C_Title.start` 只要求角色名、稱號不是空字串；沒有額外 16 字上限或字元白名單。客戶端禁止內嵌 NUL、拒絕無法無損編碼的 CP950 字元，並限制整個未補齊內容不超過 10232 bytes（符合既有傳輸框架上限）。空格不會被偷偷移除。
- GM、血盟主、等級 10／40、`CLANTITLE` 等權限由原服裁決，不在本機猜測配置。不讀取配置值；請求送出不代表成功。S183 空字串是合法清除通知，但 C90 空稱號會被原服拒絕。
- 表情在死亡、鬼魂、傳送、隱身及多數變身時被原服拒絕；騎馬外觀 6080／6094 是原服變身例外。客戶端擋住已知死亡、操作忙碌、世界切換、交易與可知隱身狀態，其餘仍以伺服器為準。
- 原服 `broadcastPacketAll` 走可見玩家廣播，沒有 C120 自身 ACK。自身在 sender admission 後播放明示的本地預測；遠方角色只依 S158 播放。傳送失敗斷線，不重送。稱號永遠等 S183。
- 稱號繪製移除 legacy 顏色控制碼及控制字元；只為顯示截成 80 字加省略號，模型與送出的稱號不因此截斷。名稱在稱號下方。標籤不改物件選取 owner。

## 原圖與生命週期

`TitleAndEmote.cs` 從 `TW_CLIENT_ROOT`（預設 `D:\lineage開發\0-客戶端`）唯讀讀取 `TW13081901.txt`、`Sprite.idx/pak` 與分卷 `Sprite00`–`Sprite15`。索引、封裝、長度 SHA 沿用既有 `GroundSpriteReader` 核定值。解碼後只放記憶體，不產出或追蹤遊戲資源。

保留角色原始動作列、alias、各 frame 的 ticks 與當列 framerate，SPR 檔號加上方向 0–7。核定 metadata 的 gfx2796/action66 有雙左括號，已加入限定語法相容。gfx0/action66 在原檔被命名為 shop loop；仍依伺服器 action66 對應，沒有自製替代姿勢。

動作播放一次。換圖／斷線／新 S87／移動／轉向／死亡／其他權威模型更新會使舊角色參照失效；最長保留 10 秒。無原圖時停止該次表情並顯示錯誤，不使用無關姿勢。使用原 block Z、tile 排序和 object owner 合成，原快取未提供的表情陰影不杜撰。

限制：首次讀某封裝會同步核驗整檔 SHA，可能造成載入停頓；之後使用記憶體快取與已核驗唯讀 handle。動畫音效不在本任務範圍。未窮舉任意變身 gfx，未聲稱所有外觀均有表情。

## 唯讀來源身分

以下相對 `D:\lineage開發\1-伺服器\src\com\lineage\server`；使用 Java 行號／方法定位，沒有 BIN 地址。

| 路徑／方法 | SHA-256 |
|---|---|
| `clientpackets/C_Title.java`：`start`、`changeTitle` | `6ef78748cd0a63d1706eac0fa279c922c34cf19f1e5b66640d1cc9d6603d3b5b` |
| `clientpackets/C_ExtraCommand.java`：`start` | `5ac261f9461a33ccaa4f33e97e6f355408b59fb26fc8207852ec9b1e5f8dcc51` |
| `serverpackets/S_CharTitle.java` | `16ce2bc261cb0d9a4ae0ad5550172e483fa67fc09ab480d376f6f14e4df4e6c7` |
| `serverpackets/S_DoActionGFX.java` | `7d71e3bd0d6fa265058cd78223148e54e45106724c4eeb268f62fd90b136b6ba` |
| `ActionCodes.java`：66–69 | `4ce21418e157764592cd633ebe88ea93dd6e546264b573e866bfcf65120d236f` |
| `model/L1Character.java`：`broadcastPacketAll` | `efde278d67f29468b2581188c6966a6d6ee9e7f6a87cc2d5650b624700410fa3` |
| `world/WorldBroadcast.java`：`toVisiblePlayers` | `c37429e086de95ce21c67ff86687cca359adcf1150981087987e6694f70b2e17` |

原客戶端 `TW13081901.txt` SHA-256：`28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8`；封裝 pin 完整列在模組。SPR 索引 offset 是各 `.pak` 的 File Offset，沒有當成 RVA／VA。
