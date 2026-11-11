# Issue #957 報告：T415 R11-A 選目標模式共用游標

依據：`docs/reference/gap-matrix.md` 15.13.5（3:C_INVITE_PARTY 列）、15.13.6（R11-A）。分支 `task/957`。

## 做了什麼
1. **瞄準游標看「回呼不為空」**（`engine/source/DuelUi.cs`）
   - 新增純邏輯 `TargetPickCursor.Index(LoginConnection, hovered)`：施法選目標照舊依射程出 8／19（`SpellCursorRules`，回呼 type 4）；組隊選對象（`/invite` 不帶名稱）與通用選對象不是 type 4 → 8；不在任何模式 → 3（手形）。
   - `UpdateAttackCursor`／`SyncSpellCursor` 改看 `LoginConnection.TargetPicking`（施法、組隊邀請、通用選對象任一），選完或取消（滑鼠沒動）也由計時器還原成 3。原本只認 `SpellTargetPicking` 的缺口（#923 報告列的「未接」）補上。
2. **通用的選對象入口**（`engine/source/PlayerPointer.cs`，只加回呼，不動既有流程）
   - `ObjectPickRequest(Name, Session, Epoch, Pick, Cancelled)` 與 `LoginConnection.BeginObjectPick(name, pick, cancelled)`、`CancelObjectPick()`、`ObjectPicking`、`ObjectPickName`、`TargetPicking`。
   - `PlayerPointerController`：模式中左鍵 `Select`／攻擊路徑 `ActAsync(id,false)` 點物件＝取走回呼、交給 `Pick(id)`（不改選取目標、不攻擊，回 false），結果放 `LastObjectPick`、提示 `ObjectPickSentNotice/RefusedNotice(name)`；右鍵交談不走；點地面＝取消、不走路、通知 `Cancelled`。
   - 單一槽：開新回呼會取消舊回呼（通知）、施法選目標與組隊選對象；之後若施法或組隊選對象被點中，通用回呼丟掉（不通知）。換連線（`SessionGeneration`）或換世界（`WorldEpoch`）後失效。
   - 對象合不合格（玩家、距離、活著）由回呼自己判斷——給 R11-B ③（`Action_AddFriend`／`Action_Exclude`／`Action_InviteChatParty`）直接用。
3. **離線測試** `engine/source/TargetPickCursorTests.cs`（`[WorldModuleTest("target-pick-cursor")]` 自動發現）：組隊選對象游標 8 與還原 3、通用選對象的點人／拒絕／攻擊路徑／交談不走／點地面取消、單一槽、換世界失效、null 回呼。

沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`；沒有新面板。

## 怎麼驗證
- 已驗證：`dotnet build engine/source/OfflineClient.csproj -c Release` 成功（0 錯誤；3 個警告都在既有檔 `HtmlActionTests.cs`、`MagicUi.cs`，非本次）。
- 已驗證：`engine/tools/check-no-game-assets.py` → `status: pass`（4502 檔、0 錯誤）。
- 已驗證（CI，PR #971 第一個 commit，run 42948053882）：`dotnet`、`map-continuity`、`npc-offline` 三個 job 都綠；`map-continuity` 的 `--module-tests` 裡 `target-pick-cursor`（`P06.TargetPickCursorTests.Run`）pass、37 個檢查、0 失敗，既有 `party-invite-pick`、`spell-cursor` 等照舊通過。本機沒執行測試（依 2026-11-09 規定）。

## 未驗證／推測
- 組隊邀請回呼在原版的類型號沒查；推測不是 4（不看射程，固定 8）。
- 通用選對象「點中先結束模式再呼叫回呼」照 `sub_954FC0` 推測；多個模式同時存在時的先後（施法 > 組隊 > 通用）是引擎規則，原版只有一個槽。
- 游標實際外觀（BIN 讀出的游標 208／136）與切換時機：真機再確認。
- 沒有 R11-B 的實際呼叫端；入口目前只有測試在用。

## 產出路徑
- `engine/source/DuelUi.cs`、`engine/source/PlayerPointer.cs`、`engine/source/TargetPickCursorTests.cs`、本報告。

## 其他說明
- 本機建置時 NuGet 還原以 `NUGET_PACKAGES` 指到原始專案的 `engine\.nuget\packages`，因此在原始專案目錄下新建了這個 NuGet 快取資料夾（git 忽略、約 0.4 GB 的套件快取，跟 `engine\.nuget` 下既有快取同一類）。依規定不做遞迴刪除，所以留在原處；不需要的話可由調度器清理。

## 給下一棒的提示
- R11-B ③：在 `NativeUiCommands` 呼叫 `login.BeginObjectPick("加入好友", id => …送 C138／C_EXCLUDE…, null)` 即可；回呼裡自己檢查對象（可參考 `WorldObjectDrag.PlayerCandidate`），回 true＝已送出。游標與點地面取消不用另外處理。
- 若之後查到組隊邀請回呼類型或 `byte_139FBAF`／`byte_13CE3F8`，只要改 `TargetPickCursor.Index` 傳給 `SpellCursorRules.Index` 的參數。
