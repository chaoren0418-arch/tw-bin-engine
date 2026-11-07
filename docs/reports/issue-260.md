# Issue #260／T126：好友與封鎖名單

## 結論

依 2026-11-07 主控第 3 輪裁定完成必要掛接；完整 buddies 離線測試 169 項通過，原第 63 行的 S250/17 快照與不分大小寫查詢斷言保留並通過。產品建置、相關回歸及素材檢查通過，修正提交的三項 CI 全綠。PR 沿用 [#275](https://github.com/chaoren0418-arch/tw-bin-engine/pull/275)，合併衝突已解除；原生實機不在此次驗收範圍。

## 做了什麼

- 先合併 `origin/main`（`5fece0032aef4ed8b772671f4a425c0f1d4044e8`）。解決五處衝突，保留 main 的音效分派、公告板與道具轉移測試；dev lock 取 main 版本，再由 `--update-lock` 產生，未手工拼接 hash。
- CI 等待期間 main 又合併角色管理，導致文件提交 `72e62f6` 的 PR 合成合併出現 `sourceCount 182 != pinned source files 184`。再次合併 `origin/main`（`0380ae6062804b976fe32e5ac2a20b0b611e42ac`），執行 `--update-lock` 並重做以下所有本機驗證；另加跑角色管理 218 項，確認新合併的切角模組回歸。
- `WorldSession.ReceiveCore` 保留 S39 buddy 分支，只將 S250/17–19 送入名單處理器；S71 僅 109／472 通知 Buddies，隨後繼續原訊息分派，其他訊息走原路徑。沒有改寫 Inventory 或其他模組處理器。
- `WorldSession.Reset()` 呼叫 `ResetBuddies()`，清除好友、封鎖、等待及過期 UI 狀態。未掛在跨圖會執行的 `ResetCombat`；跨圖保留清單及已送出的封鎖等待。
- 補強 S250/17 Big5 名稱與 revision、無關 S71 的參數與等待狀態、跨圖保留等待的測試。完整測試經既有 DemoChecks 與 Windows CI 入口執行，沒有跳過後段。
- 既有 `Buddies.cs`、`BuddyUi.cs`、`PlayerChat.cs` 提供 C4／207／202／171、好友與封鎖頁、權威回覆更新及一般聊天／密語的單一封鎖查詢點；協定及來源 SHA-256 見 `docs/protocol/buddies.md`。

## 怎麼驗證

本機 Windows、SDK 10.0.300、Python 3.12.10。建置與快取均位於本 worktree，NuGet 唯讀引用原專案 vendor。Python 命令使用本機 Python312 的完整執行檔路徑。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過：184 source files／193 pinned files |
| `rebuild.py --build --configfile scratch/issue-260/NuGet.Config --write-receipt --output scratch/issue-260/build` | product、audio、launcher 成功；0 錯誤，既有 `MagicUi.cs:20` 的 CS8600／CS8602 共 2 警告，launcher 0 警告 |
| DemoChecks `buddies` | **169 項全部通過**：全部 C 格式、S39／S250／S71、截斷與錯誤封包、聊天過濾、送出競態、Reset／Cancel、跨圖與 UI 綁定 |
| DemoChecks `map-continuity` | 358 項通過（含 main 的公告板測試） |
| DemoChecks `combat-admission` | 278 項通過 |
| DemoChecks `item-transfer` | 73 項通過 |
| CharacterManagementChecks | 218 項通過（合成資料，無遊戲／網路／DB） |
| `check-no-game-assets.py` | pass：3300 tracked files、0 errors、4 EmbeddedResource declarations |
| `git -c core.whitespace=cr-at-eol diff --check origin/main` | 通過 |

測試重現：先 restore `engine/_integration/dev/demo-tests/DemoChecks.csproj`，再執行 `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-260/buddies.json buddies`。測試使用產品 DLL、合成封包與記憶體 sender，沒有呼叫遊戲入口或顯示視窗。

最新產品 DLL SHA-256：`a8e021b87003c49c84d8777c5dfd8364fd5f18ab80343755da226a8d508c3148`。

CI：修正提交 `58bd1757881c6a1eae98bd1c4949675467b372a9` 的 [dotnet](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42324337305/job/128256032635)、[map-continuity](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42324337305/job/128256032403)（含完整 buddies 與 WebView2 回歸）、[npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42324337311/job/128256032411) 全部成功。後續 main 併入新模組造成的 lock 數量問題已如上重新生成；交付前另核對再次同步後最終 head 的三項 CI，結果與 SHA 記錄於 PR 描述及 [PR Checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/275/checks)，避免文件記錄自身 SHA 的循環。`gh` 認證回 401，GitHub 查核及 PR 更新使用 repo 擁有者已連接的 GitHub 帳號。

## 未驗證什麼

- 原生 UI 操作、登入、好友持久化／在線變化、雙角色封鎖聊天，交由獲授權的 dot 在後續 smoke 驗證。
- S250/17 有協定建構子依據，不宣稱每次登入一定收到完整封鎖快照。
- 合成封包及 UI 控制項建構通過不等於真機驗收。

未啟動遊戲、伺服器或 DB；未複製遊戲資源或伺服器原始碼；未修改封存、硬連結。`docs/REPORT.md` 僅隨 main 合併更新，本任務未另改；沒有遞迴刪除或移除 worktree。Git fetch／merge／commit 必須更新原專案 `.git/worktrees/task-260` 中繼資料，原專案工作檔未寫入。

## 產出路徑

- 程式：`engine/source/Buddies.cs`、`BuddyUi.cs`、`WorldSession.cs`、`PlayerChat.cs`。
- 測試：`engine/_integration/dev/buddies-tests/BuddyChecks.cs`；DemoChecks 與 `.github/workflows/build.yml` 入口。
- 文件：`docs/protocol/buddies.md`、`docs/reports/issue-260.md`。
- 本機證據（不提交）：`scratch/issue-260/build/`、`buddies.json`、`map-continuity.json`、`combat-admission.json`、`item-transfer.json`、`receipt.json`。

## 給下一棒的提示

Claude 審查 PR #275 與最終 head CI，使用者合併。主控範圍阻塞已解除，不需再派相同掛接修復。dot 的後續 smoke 檢查好友增刪後手動查詢、在線狀態、封鎖／解除的一般聊天與密語、跨圖保留及登出／切角清空；封鎖滿 16 人時依原服回 472，不能以本機 UI 推定解除成功。
