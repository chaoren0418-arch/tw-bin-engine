# T143 幽靈模式契約

依據：[#132 協定表](../reference/server-protocol.md#s-owncharpack)、[規則表](../reference/server-rules.md)、[#226 版本對照](../reference/source-vs-jar.md)。本次唯讀核對 d287 JAR 的方法指令與來源；未執行 JAR、未讀設定或 DB，未複製伺服器原始碼／class。

## 已驗證的進入與離開路徑

- d287 **存在進入路徑**。`C_NPCAction.start` 的 `ent` 分支，NPC template ID 50038、50042、50029、50019、50062 會轉入 `watchUb`；其他 NPC 的 `ent` 不代表幽靈觀戰。
- `watchUb` 從 UBTable 取得觀戰落點，消耗 100 金幣（item 40308）並保存角色後，呼叫 `beginGhost(x,y,map,true)`；金幣不足送 S71 訊息 189。是否實際有該 NPC／對話／UB 資料，仍須真機確認。
- `beginGhost` 保存原座標、地圖、朝向，設定 ghost 與內部 canTalk，再呼叫傳送（朝向 5）。沒有額外的「進入幽靈」C opcode。本客戶端沿用原有 NPC 對話的 C125 `D(objectId), S("ent")`，不增加自製入場按鈕，也不預扣金幣。
- 全 JAR `.class` 的 `beginGhost` 名稱只出現在 `C_NPCAction` 與 `L1PcInstance`；逐方法檢視確認外部直接呼叫為上述觀戰路徑。**未找到死亡直接進入幽靈的呼叫路徑**；此結論不涵蓋未載入的外掛或反射呼叫。死亡（例如 S87 action 8）不會在本機自行轉成幽靈。
- C173 `C_ExitGhost` 不讀任何欄位。非幽靈直接返回；幽靈呼叫 `makeReadyEndGhost`，標記 reserve 並傳送回儲存位置。`utils.Teleportation` 在提交路徑呼叫 `endGhost`，再送自身 S87。本機送出 C173 不代表成功離開。

## Wire 格式與狀態權威

| 封包 | 欄位（不含補零） | 處理 |
|---|---|---|
| C173 | `AD` | 無欄位；沿既有 Pad 送 `AD 00 00 00 00 00 00 00` |
| C125（沿用） | `7D D(npc objectId) S(ent)` | 從伺服器對話選項發送，非 template ID |
| S87 自身 | `57 H(x) H(y) D(id) H(gfx) C(weapon/action) C(heading) C(light) C(speed) D(exp) H(lawful) S(name) S(title) C(flags) D(clanId) S(clan) S(owner) C(rank) C(hp%) C(brave) C(0) C(0) C(FF) C(FF)` | 完整解析、尾段／padding／自身名稱與朝向驗證後才更新 |

S87 `flags & 0x80` 是幽靈狀態；其他位元、其他物件的 S87、HP=0 或死亡動作都不能取代此判準。flags 位於兩個變長字串之後，不使用固定 offset。保留既有 N+8 recovery wrapper 支援。

外觀的 gfx、weapon/action、方向、光源、速度等沿用 S87；不捏造幽靈 gfx、不自行改透明度或復活。d287 的 `ghostCanTalk` 是伺服器內部欄位，S87 沒有該值，也沒有完整的可行動作 mask。此模組不以 ghost 旗標推定所有權限，既有動作限制與伺服器檢查仍有效；例如 C_NPCAction 自身會拒絕 ghost。HUD 顯示「行動由原服判定」。

## 客戶端生命週期

- 狀態直接取自當前自身 Actor，Reset、S206、S149 清除 Actor 時不保留舊幽靈資料。進出幽靈的 revision 配合 session generation、world epoch 與自身 ID，拒絕舊畫面／排隊中的離開請求，包括離開再進入的 ABA 情況。
- HUD 僅一個掛載點；顯示幽靈狀態並提供「離開幽靈」。按鈕不依賴可渲染的地圖或外觀，傳送未完成時禁止再送。
- C173 通過既有 socket send gate 最後一次驗證；排隊、送出後 15 秒內禁止重複請求。不預先解除旗標；15 秒後只提示結果未知並允許手動重試，沒有自動重送。這是本機 UI 防重政策，不是原服 ACK 或逾時保證。
- 當前連線傳送失敗沿用斷線處理；舊連線延遲失敗不影響新連線。原服訊息仍附在 HUD 提示內。

## 來源識別（SHA-256）

JAR：`D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。

以下為 JAR entry 的未壓縮 bytes 雜湊；路徑前綴為 `com/lineage/server/`。方法檢視使用 `javap -p -c`，未引用原生 File Offset／RVA／VA。

| entry | SHA-256 |
|---|---|
| `clientpackets/C_NPCAction.class` | `a852ccac8ea10a55b590c311c4cc646891b1bd7d7d108f7ddb199d6c55a0142c` |
| `clientpackets/C_ExitGhost.class` | `4a3857754db73019686d1a009161646229470c1a4a17426949f237484a4482c6` |
| `model/Instance/L1PcInstance.class` | `5cbacdbab9576d3c8367df09f71888b26d0edb33757a5f4367b3b8f9646bfd27` |
| `serverpackets/S_OwnCharPack.class` | `f985de3f8b097a0c10a654a36385acf9251f4d9a1e0510ee003e9fca402ec714` |
| `utils/Teleportation.class` | `855211bc6f8988a32ae678fddda0b9763614d79b04db8000ce9f3d5160756ad8` |

唯讀來源快照（`D:/lineage開發/1-伺服器/src/com/lineage/server/`）：

| Java 檔 | SHA-256 |
|---|---|
| `clientpackets/C_ExitGhost.java` | `5e7a14c59b100dd3f09384fac731a2e4668d06ab0e53b5c1f7308b25351f1839` |
| `clientpackets/C_NPCAction.java` | `280ef4bb7316ca61e199ae97a71405ec45affc37a8b4955c4881a891b298b0a1` |
| `model/Instance/L1PcInstance.java` | `0d1f267891c99e662bf0695d799a50e302f42a7f52a0440945372a443ac05c92` |
| `serverpackets/S_OwnCharPack.java` | `6925f701e7d0245e2ba41bf0e2892fa9d8a1c786d6ebe6d87363e77004020f26` |

測試入口與原生待驗事項見 [測試說明](../../engine/_integration/dev/ghost-mode-tests/README.md) 與 [任務報告](../reports/issue-360.md)。
