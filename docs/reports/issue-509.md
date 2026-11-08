# Issue #509 T203：M7-A 接收與快照擴充點

## 做了什麼
- 新檔 `engine/source/WorldExtensions.cs`：WorldSession 的模組註冊。新模組只要寫 `partial class WorldSession` 新檔並加屬性，就會自動註冊，**不必再改 `WorldSession.cs`／`Program.cs`**。
  - `[WorldReceive(op)]` `bool M(byte[] plain)`：消化型處理器。`Subtype=n` 比對 `plain[1]`，用於 S250 子碼；`-1`（預設）不限子碼。分派時先找完全符合子碼的處理器，找不到再找不限子碼的。回 `true` 表示已消化，回 `false` 仍算未處理。
  - `[WorldReceive(op,Observe=true)]` `void M(byte[] plain)`：只觀察，排在主分派最前面，所有封包都會先經過，不影響原本的處理。
  - `[WorldReset]` `void M()`：在 `Reset()` 結尾呼叫。`[WorldSnapshot("key")]` `object? M()`：放進 `Snapshot().extensions["key"]`。
  - `[WorldModuleTest("name")]` `static void M(WorldModuleTestContext t)`：寫在組件內任何型別都可以，`--module-tests` 會自動找到並執行。
  - 不合規的註冊不會生效，只記進 `WorldExtensionRegistry.Errors`，產品不會因此崩潰；`world-extensions` 測試要求 Default 的 Errors 為空，等於替後續各組的新檔把關。不合規包括：同一 (opcode, 子碼) 重複、簽章錯誤、子碼超出範圍、快照 key 重複、實例方法沒有宣告在 WorldSession 上。
  - 註冊依方法名稱 ordinal 排序，結果可重現。
- `WorldSession.cs`（只改 3 處）：
  - `ReceiveCore` 開頭加 `ObserveExtensions`。
  - `default:` 先交給 `ReceiveExtension`；沒被消化的照舊 `Unhandled(opcode)`，另記 S250 子碼。
  - `Reset()` 結尾加 `ResetExtensions()`；`Snapshot()` 新增 `unhandledSubtypes` 與 `extensions` 兩個欄位，既有欄位與 `unhandled` 欄位、型別都沒動。
- **設計重點**：擴充處理器只接「主分派沒有任何 case 處理」的封包，也就是原本會記成 unhandled 的那些，所以搶不到既有模組的封包，既有行為不變。
- `Program.cs`：加 `--module-tests <輸出.json> [--module a,b]` 入口（`Assets` 載入前，不需素材），並加進啟動錯誤時不跳 MessageBox 的排除清單。結束碼：0＝全部通過；1＝有失敗、找不到指定名稱或沒有任何測試。
- 新檔 `engine/source/WorldExtensionsTests.cs`：`world-extensions` 離線測試共 39 項，只用合成封包與測試夾具登錄表。
- `.github/workflows/build.yml`（Windows job）：產品建置後新增一步「World module extension tests」，跑 `P11.ClientEngine.exe --module-tests`。之後各組的 `[WorldModuleTest]` 會自動進 CI，不必再改 workflow。
- `engine/_integration/dev/rebuild-lock.json`：以 `--update-lock` 重產（233 個來源、242 個鎖定檔）。

**範圍外的改動**：Issue 列的可改檔案是 `WorldSession.cs`、`Program.cs`、`WorldExtensions.cs`。測試新檔 `WorldExtensionsTests.cs`、`build.yml` 一步、dev lock 是「測試提交進 repo、接上測試入口、改程式先 update-lock」的要求所需。

## 怎麼驗證（本機 Windows）
- `rebuild.py --update-lock` → `--build` PASS。因 worktree 沒有 `engine/vendor`，以 `--configfile scratch/issue-509/NuGet.Config` 指向原專案 vendor（唯讀）。productDll SHA-256 `cdb58f7595b7561cf2bd08d104b6977553037f6b8d14acf0a8c18258f08b31cc`。
- `P11.ClientEngine.exe --module-tests …`：結束碼 0，`world-extensions` 39 項通過。反例：`--module no-such` 時結束碼為 1，`missing=[no-such]`。
- 測試涵蓋：
  - 消化與回 false 的計數、S250 子碼優先與不限子碼的備援、未處理子碼計數。
  - 觀察者先於主分派執行，且 S123 仍照原處理設定 ServerTime。
  - 處理器的 ProtocolException 照常往外拋。
  - Reset hook 與計數清除、快照 `extensions.<key>`。
  - 空登錄表時行為與擴充前相同。
  - 既有 S250/41 不會落到擴充點；session 之間登錄表隔離。
  - 7 種不合規註冊都被擋下，Default 登錄表無錯誤。
- 既有回歸（CI Windows job 的群組）全部 exit 0、status=pass：DemoChecks 的 `map-continuity`、`combat-admission`、`item-status`、`native-ui-resources`、`afflictions`、`resurrection`、`polymorph-cadence`、`buddies`、`item-transfer`、`doors`、`display-notices`、`timed-buffs`；另有 `MailChecks`、`WeaponRepairChecks`。
- `check-no-game-assets.py`：pass，0 錯誤。

## 沒驗什麼
- 遠端 CI 結果以 PR 頁面為準，本報告不預寫。
- 沒跑 CI 上的 sound-rules／sound-packets、combat-sfx、player-chat、WebView2 頁面檢查，也沒跑 Linux job；本次沒碰那些模組，交給 CI。
- 沒啟動遊戲、伺服器或 DB，沒做真機驗收。本任務只有協定擴充點，沒有畫面。
- 測試夾具用的 opcode（252、175、49、S250/0xEC–0xEE）是用本機探測選出「目前會落到 default」的值，不代表它們在原版的協定意義。

## 產出路徑
- `engine/source/WorldExtensions.cs`、`engine/source/WorldExtensionsTests.cs`、`engine/source/WorldSession.cs`、`engine/source/Program.cs`、`.github/workflows/build.yml`、`engine/_integration/dev/rebuild-lock.json`、本報告。
- 分支 `task/509`。

## 給下一棒的提示（M7-B／C／D／E／G／H／W）
- **注意 opcode 編號**：gap-matrix 第 1 節的「op N」不一定是線上 opcode。例：op 122 在引擎裡是 S122 heading（`WorldObjects.cs` 已經處理）。請以 SP〈S opcode 全表〉的線上值為準。如果那個 opcode 已經被主分派消化，擴充處理器收不到，這時要用 `Observe=true`，或改由擁有該檔的任務處理。
- 封包是 8 byte 對齊的。用 `new LoginReader(plain)` 讀完後呼叫 `Finish()` 驗證 padding；合成測試封包要補 0 到 8 的倍數。
- 測試寫法：在自己的 `XxxTests.cs` 加 `[WorldModuleTest("xxx")] static void Run(WorldModuleTestContext t)`；用 `new WorldSession()` 餵合成封包，以 `WorldModuleTestContext.SnapshotJson(s)` 讀 `extensions.<key>`。本機可用 `--module-tests out.json --module xxx` 只跑自己那組；CI 會自動跑全部，不必改 workflow。
- 改了程式先 `rebuild.py --update-lock` 再 `--build`。
