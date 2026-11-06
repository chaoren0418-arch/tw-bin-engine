# 寵物與召喚物封包補充（#261）

依據 `docs/reference/server-protocol.md`、`server-rules.md` 及 2026-11-07 唯讀查閱的伺服器處理器。沒有啟動服務或查詢 DB；以下是來源格式核對，尚非部署 JAR／真機驗證。

| 封包 | 已核對格式與用途（含 opcode） |
|---|---|
| S83 `S_PetList` | C83 D70 H列數，逐列 D項圈物件ID C數量；沒有寵物名稱、HP 或寵物世界ID。名稱只能從玩家背包對照。空清單通常不發包；解析器仍接受零列。S_FixWeaponList 使用相同格式但 D200，不能當成寵物。 |
| S176 `S_PetInventory` | C176 D寵物ID H列數 C11；逐列 D物件ID C分類 H圖示 C祝福 D數量 C鑑定／裝備旗標 S名稱，最後 C有號AC。分類 0 一般、2 防具、22 牙／武器；旗標 bit0 鑑定、bit1 裝備。與倉庫尾端 D費用不同，必須先依 type 分流。 |
| C103 `C_PetMenu` | C103 D寵物ID。實際用途是要求 S176 背包及裝備詳細資料，**不是**攻擊／防禦指令。伺服器核對玩家持有該寵物。 |
| C104 `C_UsePetItem` | C104 Cdata D寵物ID C從零開始的清單索引。索引依原服清單順序，不是物件ID；data 由原服回送給 S250/37。操作是裝備切換，原服依道具模板與寵物類型判斷允許。 |
| C125 `C_NPCAction` | C125 D夥伴ID S指令。`aggressive`、`defensive`、`stay`、`extend`、`alert`、`dismiss` 對應攻擊、防禦、停留、散開、警戒、解散；寵物與召喚物共用。原服檢查主人與可互動範圍。 |
| S87 | 沿用 `WorldObject` 解碼及渲染。`S_FollowerPack`、`S_NPCPack_Pet`、`S_NPCPack_Summon` 共用欄位；前者 owner 空白、HP 255，不能僅憑 S87 判定為可控制召喚物。寵物／召喚物的 owner 與玩家名稱相符且 HP 可見時，列入夥伴候選；最終權限仍由原服決定。 |
| S237 | D物件ID C百分比；沿用既有世界 HP 更新，255 為未知／隱藏，不解讀為死亡。 |
| S39 `S_PetMenuPacket` | C39 D夥伴ID S模板 **C0** H參數數量；`anicom` 10 個 S，`moncom` 6 個 S。前六項為狀態字串、HP、最大HP、MP、最大MP、等級；寵物另有名稱、飽食度字串、EXP百分比、善惡。與既有 NPC 的 **H旗標** 格式不同。 |

## 尚未掛接的必要分流

本張授權 `WorldSession.cs` 只加 S83／S176 分支，已遵守。`Pets.cs` 已有 `ReceivePetDialog` 與純解析器，但 S39 **尚未接入 ReceiveCore**；已在對話提出擴充範圍請求，未把未回覆當成同意。

目前 C104 成功後原服會送 S39，既有 NPC 解析器會把 C0 + H10 的前兩個 byte 當成 H旗標而拒絕。因此此版本只能作草稿審查，不能宣稱寵物裝備端到端完成或安排正式驗收。下一棒需獲准後，在既有模組前加單一 `case 39` 呼叫 `ReceivePetDialog`；非寵物模板回到原 NPC 路徑，並補真正 `WorldSession.Receive` 的 S39 回歸與明確查詢資訊入口。

## 來源 SHA-256

以下皆在 `D:\lineage開發\1-伺服器\src\com\lineage\server\`，只讀取，未複製原始碼。

| 相對路徑 | SHA-256 |
|---|---|
| `clientpackets/C_PetMenu.java` | `ee4e63ee86f0a0122a2cdc87e8f0c106be2e8e1a6ba4c1da894487e3adbc8c9f` |
| `clientpackets/C_UsePetItem.java` | `4f31c87a0ae456c320c12a99d67504bb44217d5c94c4d1bcfdc4f61f4c8cc1b1` |
| `model/L1ActionPet.java` | `bd93fb45ade3cb7ed7e7a44e1524b0071a2dc2f467d7380e6f07d88508b958fc` |
| `model/L1ActionSummon.java` | `748cf26087ae8b1ffe47aaa37534b82f02c4f541403716908051f48d557b613d` |
| `serverpackets/S_PetList.java` | `cd7e0f26c3809c98edae28887d545c19327c2a8ca1dcd3d82bd302e94c2e5528` |
| `serverpackets/S_PetInventory.java` | `ddc6e6f9bb6023ae58b38c3f28aac0890e610ab89642fdab9b9e5147660d67dd` |
| `serverpackets/S_PetMenuPacket.java` | `448af0d27a545bafc6978549304d5a9f87d8c1c9a2e4d5bf35d0b3ad89200d40` |
