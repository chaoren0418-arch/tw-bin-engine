# Issue #190／T103：地面物品通用化

## 做了什麼

S87 地面物品改依伺服器固定 envelope 接收，gfx／名稱／亮度不再受有限 catalog 限制；保留權威數量、C112、快照過期防護及物件生命週期。執行期從 `TW_CLIENT_ROOT` 查 SPR、解析 alias，核驗索引與封裝 SHA；終極治癒藥水（46）、自我加速藥水（42）、回家卷軸（22）可顯示及拾取。`CanDrop`／`Match` 四筆白名單及 B15 `ground-catalog.json` 逐 byte 不變；三種新增原圖不代表新增可丟資格。

只改指定四個產品檔、自己的測試、dev lock 與文件。沒有修改共用分派、`docs/REPORT.md`、封存或原專案；沒有複製遊戲資源或伺服器原始碼。

第 3 次嘗試依退回要求只同步：在既有 `task/190` 合併 `origin/main`（`aa20dfa890d0f6f02cd07edba7fffde2818bca87`）。唯一衝突為 dev lock，以 `--update-lock` 重產；`Ground.cs` 自動合入 #191 緩速政策，沒有新增功能或修改既有測試。`docs/REPORT.md` 保持 main 版本；Git 中繼資料由 worktree 的正常 merge／commit 流程更新。

## 怎麼驗證（已驗證）

- 同步後執行 `rebuild.py --update-lock`，再執行 `--build --configfile .github/nuget.ci.config --output scratch/issue-190/build`：PASS，173 個來源／182 個 pin，主程式 0 error；`MagicUi.cs:20` 既有 CS8600／CS8602 兩個 warning。產品 DLL SHA-256：`c04a5da634291ca4468ebaa50df4922f73c304bf4af5e16accee2bcb64f9a75b`。
- `GroundChecks`：**596 斷言／13 組通過**。合成 S87／C112、未知 gfx、亮度／充能／空名稱、count 上限、截斷原子性、分類取代、ABA、換圖／斷線；原有丟棄拒絕與快照測試保留；人工 SPR／IDX／PAK 損壞拒絕與核驗後唯讀鎖；三種 B06、3247 大寫 SPR、alias 及 16 個分卷樣本讀取通過。SocketGuard 未觀測到 socket。
- 三種 B06 的 C# bounds／RGBA 與既有 Python decoder 一致；正式 `ComposeGround`、像素 owner 與 Web Bitmap 路徑通過。`web-checks.cjs`：**72 項通過**；`verify-sources.py`：原 catalog／#49 fixture 完全不變。
- 同步後另執行既有 `DemoChecks` 的 `slow-actions` 群組：**364 項通過**，核對 #191 與地面操作整合；結果為 `scratch/issue-190/slow-actions-result.json`。
- `check-no-game-assets.py` 通過；差異檢查使用 `git -c core.whitespace=cr-at-eol diff --check`，保留原 `Ground.cs` 的 CRLF，不重寫其他任務也會修改的換行。
- CI 狀態於 PR 檢查欄確認；本機測試不是實機驗收。

重跑測試：建置後執行 `dotnet run --project engine/_integration/dev/ground-tests/GroundChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-190/ground-result.json "D:\lineage開發\0-客戶端" "D:\lineage開發\TW1608262200研究\engine"`（先用 CI NuGet.Config restore 測試專案）。最後一參數只供測試唯讀使用既有 assets，不建立 junction。Web：`node engine/_integration/dev/ground-tests/web-checks.cjs . scratch/issue-190/web-result.json`。

## 未驗證什麼

未啟動遊戲、伺服器或 DB，未證明真機外觀／實際拾取與部署規則；未窮舉全部 gfx。沿用 action 0／frame 0 靜態外觀，不宣稱任意地面動畫。首次使用某封裝會同步核對整檔 SHA，存在載入延遲；後續共用唯讀 handle。不同版本／缺件仍拒絕繪圖，但可從物品清單拾取。相同 wire envelope 無法證明原生物種類別，B07／B13 邊界見契約文件。

## 產出路徑／給下一棒的提示

- 產品：`engine/source/GroundCatalog.cs`、`Ground.cs`、`GroundSpriteReader.cs`、`GroundUi.cs`；測試：`engine/_integration/dev/ground-tests/`；契約與來源 SHA：[ground.md](../protocol/ground.md)。本機輸出：`scratch/issue-190/`（建置紀錄與兩份結果 JSON）。
- Claude／調度器審查既有 PR #207 的同步結果；#191 的 `GroundAvailable` 政策與 M6 契約已隨 main 合入，dev lock 已重產，不拼接 hash。本輪沒有另開 PR 或改動其他模組。
- dot 於 M6 smoke 驗證三種 B06 與一種 catalog 外物品的外觀、選取、拾取及首次載入延遲；真機物品由授權準備流程提供，不能以 CanDrop 或本次離線測試推定伺服器允許丟棄。
