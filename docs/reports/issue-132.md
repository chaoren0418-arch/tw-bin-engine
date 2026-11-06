# Issue #132：主伺服器封包與規則對照

## 做了什麼

唯讀查核 `D:/lineage開發/1-伺服器/src/com/lineage`，產出 [封包表](../reference/server-protocol.md) 與 [規則表](../reference/server-rules.md)。封包表列出 107 個 C opcode 常數（106 個數值）、112 個 S opcode 常數（104 個數值），補上類別內 S242 的兩種子型；索引涵蓋 103 個 C_ 與 243 個 S_ 命名來源檔，包括未分派、停用及繼承類別。每個索引包含來源路徑、SHA-256、方法簽章、欄位型別／順序與條件，並提供常用封包布局及長度計算規則。規則表含 38 條來源可追溯的規則與登入／傳送狀態圖。

比對基準為引擎提交 `599e9e478d55e8030057b886a0df4e3d084eedac`。本次只新增三份文件，沒有修改程式碼、`docs/REPORT.md`、規範檔或鎖檔；不讀配置與 DB、不啟停服務、不啟動遊戲、不寫原專案或伺服器目錄、不複製 Java 本文進交付。

## 本專案目前猜錯或缺少的清單

依「中斷流程／狀態錯誤 → 功能缺口 → 顯示與通用性」排序，共 19 項。**已驗證差異**指程式靜態差異；不等同已在部署服重現。引擎目標目前是 P24Lab d287 client2，來源快照未證明與該 JAR 同版；標「相容性待驗證」的列不可直接當成 d287 回歸，也不應自動擴大主線。

| 優先序 | 分類／目前差異 | 引擎證據 | 伺服器依據與下一步 |
|---|---|---|---|
| 1 | **已驗證來源格式差異、部署待驗證：失敗傳送 recovery wrapper 增加 8 bytes。** wrapper 對已補齊 payload 呼叫 writeByte，追加 00 再補齊；引擎 Finish 只容許語義欄位到最近 8-byte 邊界，收到這種恢復包會拋錯，不能完成恢復。 | `LoginProtocol.cs:LoginReader.Finish`；`Teleport.cs:ReceiveTeleport`；`WorldSession.cs:ReceiveCore` | `Teleportation.S_TeleportRecovery`、`ServerBasePacket.writeByte/getBytes`；交 T071 先核對部署是否有此 wrapper，建立原包與 N+8 包離線向量，再決定明確相容策略。 |
| 2 | **已驗證缺少：死亡重生與復活回包路徑。** 有 OwnDead 鎖定，但線上路徑沒有 C177 發送或 S85 接收。S87 可重設 OwnDead，不能替代主動重生請求與 S85 語義。 | `Combat.cs:WorldSession.AnimateCombat`；`OnlineMovement.cs:ApplyAuthoritative`；`WorldSession.cs:ReceiveCore` | `C_Restart.start`、`L1PcInstance.restartLikeC_Restart`、`S_Resurrection`；交 T072 補離線死亡→重生／復活狀態測試。 |
| 3 | **已驗證缺少：完整技能施放。** 只接受 skillId 1／3／4／8，其他已學技能無相應施放格式；不能以既有 D／DHH 推廣所有技能。 | `MagicSpells.cs:MagicSpells`；`Magic.cs:MagicRules.Request` | `C_UseSkill.start`、`ClientPacketStructure.validateSkill`、`L1SkillUse.handleCommands`；T072 先實作任務需用技能，其餘依 B01。 |
| 4 | **已驗證缺少：S42 範圍法術。** 沒有多目標／傷害延伸欄位解碼，會落入 unhandled。 | `Magic.cs:ReceiveMagic`；`WorldSession.cs:ReceiveCore` | `S_RangeSkill`（RO/includeDamage 的逐目標延伸）；交 T072，需同時驗無延伸／有延伸。 |
| 5 | **已驗證限制：S30 法術只接 action18/gfx167/flight0；箭矢只接 action1/gfx66。** 伺服器 action 可變，其他合法法術／箭矢回包被忽略；部分 action18 的非預期布局亦可能拋錯。 | `Magic.cs:ReceiveMagic`；`Combat.cs:ReceiveCombat` | `S_UseAttackSkill.buildPacket`、`S_UseArrowSkill`（3860→21、2716→19 及 actionId overload）；T072 依格式分派，不以單一 gfx 當通用協議。 |
| 6 | **已驗證不一致：登入密碼字元檢查過寬。** 引擎接受全部 ASCII 33..126，來源只接受英數及 `!_=+-?.#`；可能讓使用者送出必拒絕字元。不是實際帳密錯誤證據。 | `LoginProtocol.cs:LoginPackets.Login` | `C_AuthLogin.start`；對齊來源 profile 的字元規則，帳號／密碼不應記錄在報告。 |
| 7 | **相容性待驗證：公版 profile 只在角色清單處理 HP/MP 欄寬。** 世界 S8／S225／S33 固定 D；若把 jar-d287-client1 用於世界流程，H/H 回包不符合目前解碼。既有 client2 目標不受此項推論直接定罪。 | `LoginProtocol.cs:LoginProfile/Character`；`WorldSession.cs:ReceiveCore` | `S_OwnCharStatus`、`S_HPUpdate.buildPacket`、`S_MPUpdate.buildPacket`；先明確 profile 適用範圍，再決定分支支援。 |
| 8 | **已驗證缺少：通用道具 use_type 尾段。** 支援特定補給與裝備，無完整的指定物品／座標／名字等尾段編碼；不能把所有 C164 都送物件 ID 即結案。 | `Inventory.cs:InventoryItem.SupportedUse`、`LoginConnection.UseSelectedItem` | `C_ItemUSe.useItemRequest`、`ClientPacketStructure.validateItemArguments`；按實際任務逐種支援，不能放寬白名單後仍送錯格式。 |
| 9 | **已驗證限制：地面物品只能走有限 catalog／sprite 對應。** 來源可對更多可交易物品丟撿，客戶端缺少通用地面分類／資源；反向也不能以本地 CanDrop 宣稱伺服器必允許。 | `GroundCatalog.cs:Find/Match/CanDrop`；`Ground.cs:GroundRules` | `C_DropItem.start`、`C_PickUpItem.start`、`S_NPCPack_Item`；保留 B06／B07／B14 邊界，先依 T072 驗任務物品。 |
| 10 | **已驗證缺少：角色建立、刪除與同連線切角。** 現有登入流程可選清單角色、取消／重連，沒有 C84／C162／C7 的完整 UI 與回包轉移。 | `LoginConnection.cs:Run`；`WorldSession.cs:SelectCharacter` | `C_CreateChar.start`、`C_DeleteChar.start`、`C_NewCharSelect.start`、`SessionPhaseGate`；交主控排期，勿把重連當作切角協議。 |
| 11 | **已驗證缺少：多頻道聊天。** 使用者聊天只送 C136/type0、收 S81/type0；沒有完整喊話、密語、血盟、隊伍及廣播聊天路徑。已有 S243/type9 系統訊息不等於全頻道支援。 | `PlayerChat.cs:PlayerChatProtocol.Encode/ReceivePlayerChat`；`Inventory.cs:ReceiveInventory` | `C_Chat.start`、`C_ChatGlobal.start`、`C_ChatWhisper.start` 與對應 S 類別；交 T073／主控界定頻道範圍。 |
| 12 | **已驗證缺少：S242/sub2 受擊抑制清單。** S242 只處理 sub1 長物品狀態；主伺服器來源登入會送 sub2，現有客戶端不保存 gfx 抑制清單。是否影響現行 JAR 須另核對。 | `Inventory.cs:ReceiveInventory case 242`；`Combat.cs:ReceiveCombat` | `S_HitReactionSuppressList`、`C_LoginToServer.start`、`HitReactionSuppressTable.resolveTargetObjId`；T072 對照受擊動畫政策。 |
| 13 | **已驗證缺少：明確 S22 音效。** 現有動作／sprite 音效機制不等於 S22 repeat/soundId 解碼，線上接收沒有該分派。 | `WorldSession.cs:ReceiveCore`；`ClientCombatSfx.cs` | `S_Sound.buildPacket`；交 T074，分清 soundId 與 gfxId，避免重複播音。 |
| 14 | **已驗證缺少：S106 座標特效及 S55 完整變體。** 附著特效只顯示有限 gfx；一般 S55 可被背包分支記錄但沒有通用效果顯示，帶 H(time) 的 16-byte 變體不處理。 | `Magic.cs:ReceiveMagic`；`MagicSpells.cs:AttachedEffect`；`Inventory.cs:ReceiveInventory case 55` | `S_EffectLocation`、`S_SkillSound(int,int,int)`；T071／T074 補格式與生命週期。 |
| 15 | **已驗證限制：buff／狀態圖示支援有限。** S216 只識別 icon5，S250/sub154 主要對應技能3／8；其他 packetbox 及技能圖示不能宣稱已支援。 | `MagicStatus.cs:ReceiveMagicStatus` | `S_SkillIconShield`、`S_PacketBoxIconAura`、`S_PacketBoxIcon1` 等完整索引；T072／T073 選定要顯示的狀態。 |
| 16 | **相容性待驗證：S206 的單參數 overload 帶非零尾段。** 世界解碼只讀 H(map),C(water) 後驗零 padding，若收到 `S_MapID(int)` 會拒絕。正常二參數 overload 已對齊，未證明單參數變體在目前部署被使用。 | `WorldSession.cs:ReceiveCore case 206`；`LoginProtocol.cs:LoginReader.Finish` | `S_MapID(int)` 與 `(int,boolean)`；T071 先查使用點與 profile，再補明確變體，不能任意吞掉所有尾端 bytes。 |
| 17 | **已驗證限制：物品 status blob 只保存 hex，未完整語義化。** 名稱後綴推算 Equipped 仍存在；雖已有獨立 EquipmentSlots，不能把 Name／StatusHex 視為完整屬性與裝備證據。 | `Inventory.cs:ReadItem/InventoryItem.Equipped`；`Equipment.cs` | `S_ItemStatus`、`L1ItemStatus.getStatusBytes`、`S_EquipmentWindow`；T073 優先用權威槽位與明確 status tag。 |
| 18 | **已驗證限制：緩速時主動停用部分合法操作。** SlowActionsBlocked 使道具、地面物品與交談不能送出；來源各 handler 仍按角色狀態／節奏自行驗證，沒有客戶端這個一刀切政策。這是已知縮小支援範圍，不是 opcode 錯誤。 | `OnlineMovement.cs:SlowActionsBlocked/SlowActionNotice` 及使用處 | `C_ItemUSe.useItemRequest`、`C_DropItem.start`、`C_PickUpItem.start`、`C_NPCTalk.start`；依 B02／主控排期。 |
| 19 | **相容性待驗證：來源版本未與 d287 完成對應。** 引擎 profile 命名與登入密碼／動作／packetbox 假設綁定歷史 JAR，現在來源新增的條件不宜直接替換所有既有規則。 | `LoginProtocol.cs:LoginProfile`；本報告固定引擎基準 commit | 參考表逐檔 SHA 與部署 JAR 是不同證據；後續本機執行器唯讀核對可用部署版本／建置來源，不讀機密配置、不連 DB。 |

已核對而**不列為缺陷**：C29 來源格、S10 來源格加方向、C229／C123 的 DHH、C25／C112 的 HHDD、S242/sub1 長物品狀態及 S113 低32位 EXP 處理均已有相應實作；不把 fixture `Protocol.cs:HeadingCodec` 的不同 opcode 誤報為線上協議錯誤。移動校正、NPC 買賣、個人倉庫、私商、交易已有程式路徑，這次沒有足夠證據把它們整體判成「未實作」。

## 怎麼驗證

- 僅使用檔案讀取與靜態檢查；去除 Java 註解後盤點常數、實際分派、欄位讀寫與方法，人工核對登入、移動、攻擊、技能、道具、聊天與傳送的主要條件分支。
- 完整索引逐檔核對 SHA-256、來源行號、欄位呼叫覆蓋；另查未在常數表出現的直接 opcode、空殼及父類別布局。C 欄位不含 opcode，S 欄位含 opcode，長度公式明確區分語義、padding 與 frame。
- 檢查三份文件的來源路徑、內部錨點／相對連結、必要章節、19 條差異上限、UTF-8、`git diff --check` 與變更範圍。文件任務未執行遊戲建置或新增程式測試。

## 未驗證什麼

未證明來源與 P24Lab d287／正式部署 JAR 位元對應；未讀取配置或資料表實際值、未驗證登入／操作／畫面／聲音、未執行 Java／C# 整合測試。欄位索引提供完整讀寫入口與條件定位，不把動態 item status、HTML 模板、各技能資料表或擴充 UI 語義冒充已完成客戶端支援。文件尚待 Claude 審查、使用者合併後才滿足「進 main」。

## 產出路徑與給下一棒的提示

- `docs/reference/server-protocol.md`
- `docs/reference/server-rules.md`
- `docs/reports/issue-132.md`

Claude／調度器先審查本分支；合併後 T070–T074 的 code 任務一律先查兩表及來源 SHA，選定正確 profile／分支再修改，T075 由 dot 集中真機驗收。清單是差異索引，不是本輪擴大主線或要求使用者逐項重測。來源更新時更新相同文件，以 git 保存歷史。

## 引擎比對來源 SHA-256

<!-- engine-manifest -->

| 來源 | SHA-256 |
|---|---|
| `engine/source/ClientCombatSfx.cs` | `dfd1a65c9bc579d6ab7759e078466d8f28ce630966992c26af834b00bf3fc1ff` |
| `engine/source/Combat.cs` | `fc640aaae1bef14cfa852ae5d286699194ebd1479b4c4f7b51f695ca8c5f3110` |
| `engine/source/Equipment.cs` | `54886622111435489a1bc1c36bcfafefa91278dd7b9ba3e2a352563cd8ab3c65` |
| `engine/source/Ground.cs` | `fd811e6c79571f01cbdd0081dbb8afe211c672a5e582e32663d850373484e30d` |
| `engine/source/GroundCatalog.cs` | `7dcac9e71b016005efc48d637417e17e960fef45cbbda90974465271f6794ec0` |
| `engine/source/Inventory.cs` | `0a01800bfa778ae5c1c8ec983ee2c2b307bf8b25a590cc08fd629fa427202015` |
| `engine/source/LoginConnection.cs` | `2239e0918b0c2ae94a29debcd0ca5428a44d1f95d128f469c9c1053616dd9cfe` |
| `engine/source/LoginProtocol.cs` | `c72b0d6a517949301be34458eb8ffa5166b876388a1b06680dc9739b381ce2be` |
| `engine/source/Magic.cs` | `cbe84b5d6c7f9515412c8702025fa8730be88b2935631d93580a29935e09b689` |
| `engine/source/MagicSpells.cs` | `20cc278da9390e90757349fb27080a258b412928ff1bd0e9a3bbabc232822de0` |
| `engine/source/MagicStatus.cs` | `5b8d0eeec6a7a45457f430ab587d833d8eea387e3ac94d2e3717c2e3f805a8ce` |
| `engine/source/OnlineMovement.cs` | `9539bef0b743895eecd7daefa7b912a47b38b25593c6f42a1fb8e4cc665ef502` |
| `engine/source/PlayerChat.cs` | `39d47921f20315167b2dc095d5475cd033ae5f5338fcfbc66d10b1b2526343d8` |
| `engine/source/Protocol.cs` | `9c51aad89c9bda86dcedcdaa46cf135b0f787bb8105aa3826c8d7759e176f3f8` |
| `engine/source/Teleport.cs` | `be65b1ed0776e8fcef557115525945ea49c8b3080c74386f20044426f5344e37` |
| `engine/source/WorldSession.cs` | `92bf845b68138cdcb1d85e675cd7697579815cea83d173a97ca913cc7f9baf0a` |
