# Issue #190／T103：地面物品通用化

## 做了什麼

S87 地面物品改依伺服器固定 envelope 接收，gfx／名稱／亮度不再受有限 catalog 限制；保留權威數量、C112、快照過期防護及物件生命週期。執行期從 `TW_CLIENT_ROOT` 查 SPR、解析 alias，核驗索引與封裝 SHA；終極治癒藥水（46）、自我加速藥水（42）、回家卷軸（22）可顯示及拾取。`CanDrop`／`Match` 四筆白名單及 B15 `ground-catalog.json` 逐 byte 不變；三種新增原圖不代表新增可丟資格。

只改指定四個產品檔、自己的測試、dev lock 與文件。沒有修改共用分派、`docs/REPORT.md`、封存或原專案；沒有複製遊戲資源或伺服器原始碼。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock` 後 `--build --configfile .github/nuget.ci.config --output scratch/issue-190/build`：PASS，172 個來源／181 個 pin，主程式 0 error；`MagicUi.cs:20` 既有 CS8600／CS8602 兩個 warning。產品 DLL SHA-256：`912e12ca39e7a56be64399434a8fc878f88c02ebbbe9985cf59a81a5925747a6`。
- `GroundChecks`：**596 斷言／13 組通過**。合成 S87／C112、未知 gfx、亮度／充能／空名稱、count 上限、截斷原子性、分類取代、ABA、換圖／斷線；原有丟棄拒絕與快照測試保留；人工 SPR／IDX／PAK 損壞拒絕與核驗後唯讀鎖；三種 B06、3247 大寫 SPR、alias 及 16 個分卷樣本讀取通過。SocketGuard 未觀測到 socket。
- 三種 B06 的 C# bounds／RGBA 與既有 Python decoder 一致；正式 `ComposeGround`、像素 owner 與 Web Bitmap 路徑通過。`web-checks.cjs`：**72 項通過**；`verify-sources.py`：原 catalog／#49 fixture 完全不變。
- `check-no-game-assets.py` 通過；差異檢查使用 `git -c core.whitespace=cr-at-eol diff --check`，保留原 `Ground.cs` 的 CRLF，不重寫其他任務也會修改的換行。
- CI 狀態於 PR 檢查欄確認；本機測試不是實機驗收。

重跑測試：建置後執行 `dotnet run --project engine/_integration/dev/ground-tests/GroundChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-190/ground-result.json "D:\lineage開發\0-客戶端" "D:\lineage開發\TW1608262200研究\engine"`（先用 CI NuGet.Config restore 測試專案）。最後一參數只供測試唯讀使用既有 assets，不建立 junction。Web：`node engine/_integration/dev/ground-tests/web-checks.cjs . scratch/issue-190/web-result.json`。

## 未驗證什麼

未啟動遊戲、伺服器或 DB，未證明真機外觀／實際拾取與部署規則；未窮舉全部 gfx。沿用 action 0／frame 0 靜態外觀，不宣稱任意地面動畫。首次使用某封裝會同步核對整檔 SHA，存在載入延遲；後續共用唯讀 handle。不同版本／缺件仍拒絕繪圖，但可從物品清單拾取。相同 wire envelope 無法證明原生物種類別，B07／B13 邊界見契約文件。

## 產出路徑／給下一棒的提示

- 產品：`engine/source/GroundCatalog.cs`、`Ground.cs`、`GroundSpriteReader.cs`、`GroundUi.cs`；測試：`engine/_integration/dev/ground-tests/`；契約與來源 SHA：[ground.md](../protocol/ground.md)。本機輸出：`scratch/issue-190/`（建置紀錄與兩份結果 JSON）。
- Claude／調度器審查 `task/190` PR，與 #191 的 `GroundAvailable` 政策分開整合；dev lock 由同步輪重產，不拼接 hash。M6 契約讀取 PR #194 提交 `6873e75`，本任務未合併它。
- dot 於 M6 smoke 驗證三種 B06 與一種 catalog 外物品的外觀、選取、拾取及首次載入延遲；真機物品由授權準備流程提供，不能以 CanDrop 或本次離線測試推定伺服器允許丟棄。
