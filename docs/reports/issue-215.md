# Issue #215／T120：S242/sub2 受擊反應抑制清單

## 做了什麼

在既有 `task/215` worktree 實作完整 gfx 清單解碼：U16 little-endian、最多 512 筆、去重、空清單清空、完整封包驗證後替換。`Inventory.cs` 只新增 sub2 分派；`Combat.cs` 只新增 action=2 的查詢點，涵蓋近戰 S30 與 S158。保留攻擊／傷害資料、HP 與死亡更新、既有 sub1 長物品狀態。

封包來源核對、來源 SHA-256 與語義見 [協定補充](../protocol/hit-reaction-suppress.md)。未修改 `docs/REPORT.md`、伺服器、資源、封存或原專案目錄。

## 怎麼驗證（已驗證）

- 先執行 `rebuild.py --update-lock`，再完整 `--build`：product、audio、launcher 成功；主程式 0 錯誤，既有 `MagicUi.cs:20` CS8600／CS8602 兩個警告。
- 新增並提交 `HitReactionSuppressChecks.cs`，接入 `DemoChecks` 的 `hit-reaction-suppress` 群組、預設入口與既有 CI 執行的 `combat-admission` 群組。測試直接呼叫產品 DLL，合成封包、合成時序 metadata、SocketGuard；不啟動遊戲或建立連線。
- 本模組 **36 個斷言通過**：初始空清單、一般／重複／0／65535 gfx、512 上限、513 拒收、截斷／padding／額外尾段、錯誤原子性、完整替換、空清單、未知 subtype、物件外觀更新、跨實例隔離、近戰與 S158、保留施法／死亡／HP／targetId／傷害，以及 sub1 回歸。
- CI 同一入口 `combat-admission` **248 個斷言通過**；`map-continuity` **128 個斷言通過**。
- `check-no-game-assets.py` 通過；`git -c core.whitespace=cr-at-eol diff --check` 通過；dev lock 為 174 個來源／183 個 pinned 輸入。
- GitHub CI：推送後核對 PR Checks，結果另以 PR 留言記錄，不以本機驗證代替 CI。

本次產品 DLL SHA-256：`62015881927aa80663d1708a1486c5ae55528387439dbe8bae7117bfb0effc14`。程式來源 SHA-256 見同提交的 dev lock。

本機使用既有 Python 3.12.10（完整路徑）與 SDK 10.0.300；套件來源唯讀引用原專案 `engine/NuGet.Config`／`vendor`，所有 cache／暫存／產物留在此 worktree。測試編譯首次遇到共享 compiler 寫入拒絕，改用 `-p:UseSharedCompilation=false` 後通過。

重跑指令（需先將暫存與 NuGet cache 指向 worktree 內）：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-215/build
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-215/hit-reaction-suppress.json hit-reaction-suppress
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-build -- . scratch/issue-215/combat-admission.json combat-admission
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-build -- . scratch/issue-215/map-continuity.json map-continuity
python -B -X utf8 engine/tools/check-no-game-assets.py
```

## 未驗證什麼／未完成事項

未啟動遊戲、伺服器或 DB；未做真機動畫／聲音、現行 JAR 實際登入封包或玩家變身驗收。

另有兩個已確認缺口超出指定產品檔案範圍：

1. `ProjectileRendering.cs` 直接產生箭矢命中受擊動畫，沒有走 `Combat.cs` 查詢點，因此箭矢尚未抑制。
2. `WorldSession.Reset` 未清空新清單；下一份合法 sub2 會完整替換，但若後續登入未發送 sub2，舊清單可能殘留。跨圖應保留清單。

已提出兩處各加一行的範圍確認；尚未取得回覆時維持原範圍。這不是完整驗收完成，PR 應保持草稿直到主控裁定並補齊。

## 產出路徑

- `engine/source/HitReactionSuppress.cs`、`Inventory.cs`、`Combat.cs`。
- `engine/_integration/dev/demo-tests/HitReactionSuppressChecks.cs`、`Program.cs`。
- `engine/_integration/dev/rebuild-lock.json`。
- `docs/protocol/hit-reaction-suppress.md`、本報告 `docs/reports/issue-215.md`。
- 本機未追蹤證據：`scratch/issue-215/build/`、`hit-reaction-suppress.json`、`combat-admission.json`、`map-continuity.json`。

## 給下一棒的提示

Claude／調度器先裁定增加 `ProjectileRendering.cs` 的抑制查詢與 `WorldSession.Reset` 的清理是否納入本任務；同意後由 codex-local 補實作及箭矢／Reset／跨圖保留測試，再重新生成 dev lock、建置、跑離線測試與 CI。不可把清單無條件接到 ResetCombat，因為跨圖也會呼叫它。lock 衝突交同步輪重新生成，不手動拼接 hash。合併與真機 M6 smoke 分別由使用者與 dot 接手。
