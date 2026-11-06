# M6 開發批次契約（#186）

日期：2026-11-06。主控：雲端 Claude。本文件是 M6 各 code 任務的**共同契約**。每張任務只依賴本契約，任務之間互不依賴，可以平行派工。

## 共同規則
1. **伺服器格式一律查表**：`docs/reference/server-protocol.md`、`docs/reference/server-rules.md`（#132）。表上沒有的，**唯讀**查閱 `D:\lineage開發\1-伺服器`，結論補進 `docs/protocol/<模組>.md`（用附加段落的方式寫，避免衝突），不複製原始碼。
2. **只動自己模組的檔案**（見下表）。必須修改共用分派點時，只在下列位置加最小的掛接，不要重排或改寫既有分支：
   - 接收：`WorldSession.cs` 的 `ReceiveCore` switch，**一個 opcode 加一個 case**，內容只呼叫自己模組的方法。
   - 送出：`LoginConnection.cs` 只加一個呼叫自己模組編碼器的方法。
3. **驗收**：編譯通過（`rebuild.py --build`；改了程式就先 `--update-lock`）、自己模組的離線單元測試綠燈（合成封包，放在 `engine/_integration/dev/<模組>-tests/` 或既有 demo-tests 新增一個檔案）、`check-no-game-assets.py` 通過、CI 綠燈、短報告。**不需要真機**，真機集中在 M6 smoke。
4. **在 worktree 中進行**：`scratch/wt/task-<本號>`。dev lock 衝突交給調度器的同步輪處理，**不要手動拼接 hash**。
5. **禁止**：不啟動遊戲、伺服器或 DB；不複製伺服器原始碼或遊戲資源；不改 releases 與封存 lock；任何刪除都不可遞迴。

## 模組歸屬（第一波）
| 任務 | 模組 | 可以修改的檔案（另加自己的測試與 `docs/protocol/<模組>.md`） | 依據（#132 差異清單編號） |
|---|---|---|---|
| T100 | 技能施放通用化 | `MagicSpells.cs`、`Magic.cs` 的送出端（`MagicRules.Request` 一帶）、新檔 `SkillCast.cs` | 3；B01 的基礎 |
| T101 | 法術與箭矢回包分派 | `Magic.cs` 的接收端（`ReceiveMagic`）、`Combat.cs` 的 `ReceiveCombat`、新檔 `AttackSkillPackets.cs` | 4、5 |
| T102 | NPC 出售與私商回購 | `NpcServices.cs`、`NpcUi.cs`、`PrivateShop.cs`、`PrivateShopUi.cs` | B05 |
| T103 | 地面物品通用化 | `GroundCatalog.cs`、`Ground.cs`、`GroundSpriteReader.cs`、`GroundUi.cs` | 9；B06 |
| T104 | 緩速時的操作政策 | `OnlineMovement.cs` 的 `SlowActionsBlocked`／`SlowActionNotice` 及其使用處 | 18；B02 |
| T105 | 音效與座標特效封包 | 新檔 `SoundPackets.cs`、`MapEffects.cs`、`MagicSpells.AttachedEffect` 的擴充點 | 13、14；B04 的一部分 |
| T106 | 多頻道聊天 | `PlayerChat.cs`、`PlayerChatKeyboardFlow.cs`、`PlayerHudChat.cs` | 11 |

T100 與 T101 都會碰 `Magic.cs`，但分別是送出端與接收端，**不要動對方的區段**。

## 第二波（佇列少於 5 張時由主控補開）
| 任務 | 內容 | 依據 |
|---|---|---|
| T107 | 角色建立、刪除、同連線切角 | 10 |
| T108 | buff／狀態圖示 | 15 |
| T109 | 通用道具 use_type 尾段 | 8 |
| T110 | 傳送 recovery wrapper（N+8 bytes）相容 | 1 |
| T111 | 登入密碼字元規則對齊 | 6 |
| T113 起 | **各職業基本技能**，每職業一張：在 T100 的通用施放之上，補該職業基本技能的目標、效果與狀態 | B01 |
| T112 | **M6 smoke**（dot，真機一次） | — |

各職業的張數，要等 T100 從伺服器技能定義查出職業清單後才決定（不確定項 U8）。
