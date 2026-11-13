# M6 開發批次契約（#186）

日期：2026-11-06。主控：雲端 Claude。本文件是 M6 各 code 任務的**共同契約**。每張任務只依賴本契約，任務之間互不依賴，可以平行派工。

## 共同規則
1. **伺服器格式一律查表**：`docs/reference/server-protocol.md`、`docs/reference/server-rules.md`（#132）。表上沒有的，**唯讀**查閱 `D:\lineage開發\1-伺服器`，結論補進 `docs/protocol/<模組>.md`（用附加段落的方式寫，避免衝突），不複製原始碼。
2. **只動自己模組的檔案**（見下表）。**#645 起**收封包、測試、面板一律用文末「自動發現」的 attribute 註冊，下面兩項只適用 M7-A（#509）之前的舊分支。必須修改共用分派點時，只在下列位置加最小的掛接，不要重排或改寫既有分支：
   - 接收：`WorldSession.cs` 的 `ReceiveCore` switch，**一個 opcode 加一個 case**，內容只呼叫自己模組的方法。
   - 送出：`LoginConnection.cs` 只加一個呼叫自己模組編碼器的方法。
3. **驗收**：編譯通過（`rebuild.py --build`）、自己模組的離線單元測試綠燈（合成封包，放在 `engine/_integration/dev/<模組>-tests/` 或既有 demo-tests 新增一個檔案）、`check-no-game-assets.py` 通過、CI 綠燈、短報告。**不需要真機**，真機集中在 M6 smoke。
4. **在 worktree 中進行**：`scratch/wt/task-<本號>`。**PR 不修改 `engine/_integration/dev/rebuild-lock.json`**（#646）：
   - 改了程式也**不要**跑 `--update-lock`，也不要 commit lock。`rebuild.py --build` 發現 lock 過時時會印 `WARN: rebuild-lock.json is stale…`，改用記憶體中的暫時 lock 繼續建置（`result.json` 的 `lock` 為 `temporary`，並列出 added／removed／changed），**不寫回 lock**。
   - 與 main 同步時若 lock 有衝突，一律取 main 版本（`git checkout --theirs`／`origin/main` 的那份），**不要手動拼接 hash**。
   - lock 在**合併進 main 之後**才由調度器用 `rebuild.py --update-lock` 重產並 commit（調度器端見 #644）。main 的 CI 會跑嚴格核驗（不帶參數的 `rebuild.py`）；lock 過時時這一步失敗，並把重產好的 lock 上傳成 artifact `rebuild-lock-candidate`，**CI 不自動 commit**。
   - 嚴格模式仍保留：不帶參數、`--verify`、`--build --strict` 遇到 lock 不一致一律拒絕。
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

## 自動發現（T270，#645）

自 #645 起，測試入口與註冊表都改成**自動發現**：新增模組只在**自己的檔案**裡加 attribute，**不要再改 `Program.cs`、`DemoChecks.csproj` 與註冊表**（`UiPanelRegistry.cs`、`MacroChecks.cs` 的 `MacroRegistry`、`WorldSession.cs` 的分派）。審查時看到 PR 為了註冊而改這些共用檔，請退回改用下列方式。

| 要註冊的東西 | 寫在自己的檔案裡 | 掃描者 |
|---|---|---|
| 產品內的模組離線測試 | `[WorldModuleTest("名稱")] static void Run(WorldModuleTestContext t)`（任何型別的 static 方法） | `WorldExtensions.cs`；`P11.ClientEngine.exe --module-tests <json> [--module 名稱,…]`，CI 會跑全部 |
| 封包接收／重設／快照 | `[WorldReceive(op)]`、`[WorldReset]`、`[WorldSnapshot("key")]`（`partial class WorldSession` 新檔） | `WorldExtensions.cs`（M7-A，#509） |
| DemoChecks 測試群組 | `[CheckGroup("群組名")]` 標在 `static void`／`static Task` 方法上；同時要進預設全套就再加 `[CheckGroup(CheckGroups.Default)]`；要進 CI 會跑的 `map-continuity`、`combat-admission` 等群組就加那個名稱 | `demo-tests/AutoDiscovery.cs` |
| DemoChecks 原始檔 | 放在 `engine/_integration/dev/<模組>-tests/`（**這個資料夾不可有自己的 `.csproj`**），所有 `.cs` 自動編進 DemoChecks | `DemoChecks.csproj` 萬用字元 |
| 原生 UI 面板 | `[UiPanel(順序)] internal static UiPanelRegistration Registration=>new(Id,"版面",root=>new XxxPanel());`（一個模組多個視窗：`IEnumerable<UiPanelRegistration>`） | `AutoDiscovery.cs`（`UiPanelDiscovery`），`UiPanelRegistry.Panels` |
| 本機素材準備器的類別（T451，#1036） | `[AssetRecipe] public sealed class XxxRecipe:IAssetRecipe{…}`（無參數建構式；`Id` 只用小寫英數與單一連字號，同時是 `assets/` 下的目錄名） | `AssetPrep/AssetPrepCore.cs`（`AssetRecipeDiscovery.Product`）；`asset-prep` 模組測試要求 `Errors` 為空 |

### DemoChecks 群組規則
- 命令列不變：`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- <repo> <out.json> [群組]`；不帶群組＝`CheckGroups.Default` 全套；群組名不存在仍是 `Unknown regression group`（結束碼 1）。
- 每次執行都先跑 `CheckGroups.Preamble`（隊伍、寵物、變身基本組，寵物失敗延後回報）；已在 Preamble 跑過的方法不會在群組裡再跑。
- 群組內順序：`Order` 小的先跑；沒寫 `Order` 的排在所有有寫的之後，再依型別全名、方法名稱（ordinal）。既有群組以 10 為間隔編號（例：`map-continuity` 10–280、預設 10–490；#473 的狀態圖示插在預設 125），新測試若要插在中間就挑空號，否則不寫。
- 參數依名稱／型別繫結：`string repo`（或 `root`）＝第 0 個引數（repo 根目錄）、`string output`＝第 1 個（結果 JSON）、`[CheckArg(n)] string x`＝第 n 個、`string[] args`＝全部、`Action<bool,string>`＝`Checks.Check`；有預設值的參數用預設值。其他參數或非 `void`／`Task` 回傳會被拒絕，整次執行不開始。
- 一個方法要用不同參數跑成兩個群組時，在自己的檔案加一行包裝方法（例：`[CheckGroup("buddies-codec")] static Task BuddyCodecChecks()=>BuddyChecks(true);`）。
- 測試夾具型別加 `[ExcludeFromDiscovery]`，組件掃描會略過（只在明確傳入 `Discover(types)` 時才掃描）。
- 新群組要進 CI，仍要在 `.github/workflows/build.yml` 加步驟；**更好的做法**是把方法加進 CI 已在跑的群組（`map-continuity`、`combat-admission`…）或改寫成產品內的 `[WorldModuleTest]`（CI 會自動跑全部）。

### 原生 UI 面板規則
- `[UiPanel(順序)]` 可標在 static 屬性、欄位或無參數方法上，型別為 `UiPanelRegistration` 或 `IEnumerable<UiPanelRegistration>`。
- 順序決定同一層內的先後（較晚註冊者畫在上面、先收到點擊）。既有面板：左狀態 10、聊天 20、召喚 30、HP/MP 40／50、小地圖 60、擴充巨集 70、巨集 80、隊伍 90／100、交易 110、好友三窗 120、選單按鈕 130、狀態圖示 140。新面板挑空號，例如 105、125、150。
- Id 重複、型別不對、Id／Layout 空白、取值丟例外的註冊不生效，記在 `UiPanelRegistry.Errors`；`--module-tests` 的 `auto-discovery` 測試要求它是空的（CI 會擋）。
- 宣告了但還不要上線的面板（例如 `PersonalShopPanel.Registration`），**不要**加 `[UiPanel]`。

### 仍然是共用檔、尚未自動化的地方
- `.github/workflows/build.yml`（新的 DemoChecks 群組步驟）。`engine/_integration/dev/rebuild-lock.json` 依 #646 PR 一律不改（見第 4 條）。
- `UiClientBridge.cs` 的 `NativeUiGameState()` 狀態包裝鏈與 `NativeUiCommands`：要新增 `IUiGameState` 裝飾層的面板仍需改這裡（後續任務）。
- 產品 `Program.cs` 既有的 `--xxx-tests` 舊診斷入口沒有遷移；新的產品內測試一律用 `[WorldModuleTest]`，不要再加新的命令列旗標。