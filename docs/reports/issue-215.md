# Issue #215／T120：S242/sub2 受擊反應抑制清單

## 做了什麼

依主控第 3 輪裁定，補齊箭矢命中與 session 清理兩個退回缺口，沿用既有 `task/215` worktree 與 PR #241。

- `HitReactionSuppress.cs` 保存完整 gfx 清單：U16 little-endian、最多 512 筆、去重、整包驗證後替換、空清單清空；畸形封包保留前次有效清單。
- `Inventory.cs` 的 S242 只加 sub2 分派；`Combat.cs` 在共同 action=2 動畫路徑只查詢一次，涵蓋近戰 S30 與 S158。
- `ProjectileRendering.cs/ComposeProjectile` 的箭矢命中條件只增加一次相同的 `IsHitReactionSuppressed` 查詢；列入清單者不建立 action=2，既有飛行、傷害資料、HP 與死亡處理維持原樣。
- `WorldSession.Reset` 只新增 `ResetHitReactionSuppress()`，模組內執行集合 `Clear()`。取消／登出、重新登入前、同連線選角與斷線等既有路徑都會清理；S206 跨圖保留清單，未修改 `ResetCombat`。
- 補齊並提交箭矢與生命週期回歸，接續既有 `DemoChecks` 的模組、預設及 CI `combat-admission` 入口。

格式、來源 SHA-256 與語義見 [協定補充](../protocol/hit-reaction-suppress.md)。未修改 `docs/REPORT.md`、原專案資源、封存或正式設定。

## 怎麼驗證（已驗證）

| 檢查 | 本輪結果 |
|---|---|
| `rebuild.py --update-lock` 後完整 `--build` | product／audio／launcher 成功；0 錯誤，既有 `MagicUi.cs:20` CS8600／CS8602 共 2 個警告 |
| `hit-reaction-suppress` | 66 個斷言通過 |
| CI 相同入口 `combat-admission` | 278 個斷言通過 |
| `map-continuity` | 128 個斷言通過 |
| `check-no-game-assets.py` | 3236 個 tracked files，0 errors |
| dev lock 唯讀核驗 | 174 個來源、183 個 pinned 輸入通過 |
| `git -c core.whitespace=cr-at-eol diff --check` | 通過 |

測試直接載入本輪產品 DLL，使用合成封包與時序 metadata、記憶體 sender 及 SocketGuard。箭矢測試不呼叫 renderer 建構子、不開視窗、不讀 sprite；驗證命中前後 action=2、施法保留、攻擊／傷害欄、HP 與死亡行為。

生命週期測試涵蓋直接 Reset、Cancel、同連線選角；清理後重建相同 objectId／gfx 且不送 sub2，確認正常受擊，再確認新清單可以生效。跨圖測試送 S206 並重建物件，確認舊清單仍抑制受擊。既有解碼邊界、錯誤原子性、sub1 背包與近戰／S158 回歸均保留。

產品 DLL SHA-256：`8165466213125f258d869212c14d1f2298bf933cac938420f0ef030cedfb5539`。來源 SHA-256 見同提交的 dev lock，僅由 `--update-lock` 生成。

GitHub CI：推送後核對 [PR #241](https://github.com/chaoren0418-arch/tw-bin-engine/pull/241) 本輪 head 的 `dotnet`、`map-continuity`、`npc-offline`；舊 head 綠燈不作本輪證據。

本機 Python 3.12.10、SDK 10.0.300；NuGet config／vendor 唯讀引用原專案，cache、暫存與產物留在此 worktree。重跑方式（先把 TEMP、TMP、DOTNET_CLI_HOME、NuGet cache 指向 worktree）：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-215/build
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-215/hit-reaction-suppress.json hit-reaction-suppress
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-build -- . scratch/issue-215/combat-admission.json combat-admission
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-build -- . scratch/issue-215/map-continuity.json map-continuity
python -B -X utf8 engine/tools/check-no-game-assets.py
python -B -X utf8 engine/_integration/dev/rebuild.py
```

## 未驗證什麼

未啟動遊戲、伺服器或 DB。未驗證現行 JAR 實際發送、玩家變身、真機動畫／傷害數字畫面與音效；由 dot 在 M6 smoke 核對。離線測試使用合成封包，不代表真機功能驗收。

## 產出路徑

- 產品：`engine/source/HitReactionSuppress.cs`、`Inventory.cs`、`Combat.cs`、`ProjectileRendering.cs`、`WorldSession.cs`。
- 測試：`engine/_integration/dev/demo-tests/HitReactionSuppressChecks.cs`；既有 `Program.cs` 已接上入口。
- 鎖檔：`engine/_integration/dev/rebuild-lock.json`。
- 文件：`docs/protocol/hit-reaction-suppress.md`、`docs/reports/issue-215.md`。
- 本機未追蹤證據：`scratch/issue-215/build/`、`hit-reaction-suppress.json`、`combat-admission.json`、`map-continuity.json`。

## 給下一棒的提示

Claude／調度器審查 PR #241 的新 head 與 CI，再由使用者合併；dot 接 M6 smoke。兩個原退回缺口已修正，無須再次裁定範圍。跨圖不可清空抑制清單；dev lock 如與主線衝突，交同步輪重新生成，不手動拼接 hash。worktree 保留給調度器清理。
