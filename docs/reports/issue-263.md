# Issue #263 — T129：變身

分支：`task/263`。階段：code。日期：2026-11-07。

## 做了什麼

- 新增 `Polymorph.cs`，完整解析 S76 一般 CDHH 與 NPC CDDHS，更新既有當前 gfx／武器姿勢；解除直接採用伺服器送回的原 gfx。清除舊外觀非死亡動畫，保留後續 S119／S158／S30 的權威更新。
- 不從變身推算速度或期限；保留 S87／S255／S67 速度。保留死亡狀態、處理目標操作世代、未知 ID 不建立物件，沒有獨立 gfx 快取殘留。
- 唯讀確認 S230 SPOLY 沒有同值別名或 serializer，維持未處理。來源 SHA-256 與欄位詳見 [協定補充](../protocol/polymorph.md)。
- `WorldSession.cs` 僅加一個 S76 case；`WorldObjects.cs` 僅移除原先提前攔截 S76 的兩行，使新 case 可達。既有 Gfx／Weapon 已是繪圖和動作共用查詢值，無須再改 `ObjectRendering.cs`。
- 測試提交於 `engine/_integration/dev/polymorph-tests/`，編入既有 DemoChecks，所有既有群組會先執行基本變身檢查；既有 Windows CI job 加入獨立的 `polymorph-cadence` 步驟。dev lock 由 `--update-lock` 產生。

## 怎麼驗證（已驗證）

- .NET SDK 10.0.300，`rebuild.py --update-lock`：同步 main 後 175 個來源、184 個鎖定輸入。
- `rebuild.py --build --configfile scratch/issue-263/NuGet.Config --output scratch/issue-263/build`：產品與 launcher 建置通過；產品有既有 `MagicUi.cs:20` CS8600／CS8602 兩個警告、零錯誤，launcher 零警告／零錯誤。
- 首次 nuget.org restore 因 Windows TLS 認證錯誤失敗；改用 scratch 內設定唯讀指向原專案 `engine/vendor`。套件快取、暫存與建置輸出皆留在本 worktree，沒有修改原專案資源。
- `DemoChecks ... polymorph-cadence`：123 項檢查通過，含 90 項基本變身檢查與 33 項合成動作節奏檢查。直接執行建出的產品 DLL 收包路徑；涵蓋所有 S76 serializer 格式、錯誤包不改狀態、S230、速度與物件生命週期，以及實際 S30／S158 路徑採用新 gfx／武器節奏。
- 既有群組回歸：`map-continuity` 218 項、`combat-admission` 302 項通過（各包含基本變身 90 項）。
- `check-no-game-assets.py`：3,261 個追蹤檔、4 個 EmbeddedResource 宣告，零錯誤；`git diff --check` 按既有 CRLF 格式檢查通過。
- 第一輪遠端 CI 的 Windows 變身／地圖／戰鬥檢查通過；Linux 編譯成功後，因 main 新增 `StatusIcons.cs` 與本分支的 dev lock 自動合併留下 sourceCount=174（實際 175），來源核驗失敗。已無衝突同步 `origin/main`（272667d），由 `--update-lock` 完整重產，未手拼 hash；重新建置及上述三組測試、素材檢查均通過。
- 遠端最終結果見 [PR #278 Checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/278/checks)。

## 未驗證什麼

- 未啟動遊戲、伺服器、DB，未執行原生登入、實際變身圖片、音效或攻擊 frame timing 驗收。
- 本次測試不讀遊戲素材；節奏測試使用自行撰寫的合成 metadata，不代表原始 sprite 的 frame timing。既有 profile／sprite 缺漏的 gfx 仍會被既有保護阻擋，不能宣稱所有變身外觀均已可顯示。
- 沒有驗證部署 JAR 的 SPOLY 行為；本次結論限於已雜湊的伺服器來源。沒有替 SPOLY 猜測格式。

## 產出路徑

- `engine/source/Polymorph.cs`
- `engine/source/WorldSession.cs`、`engine/source/WorldObjects.cs`（最小接點）
- `engine/_integration/dev/polymorph-tests/`、`engine/_integration/dev/demo-tests/`（測試與接點）
- `engine/_integration/dev/rebuild-lock.json`
- `.github/workflows/build.yml`（既有 Windows job 的測試接點）
- `docs/protocol/polymorph.md`、`docs/reports/issue-263.md`
- 本機可重建證據：`scratch/issue-263/build/`、`scratch/issue-263/polymorph-checks.json`、`scratch/issue-263/polymorph-cadence-checks.json`、`scratch/issue-263/map-continuity.json`、`scratch/issue-263/combat-admission.json`（不提交大型暫存）。

## 第 2 次嘗試：僅同步 main

- 合併 `origin/main`（最終 `9e979df0b20889b7ff402c5d22ff010d471d9e93`）至既有 `task/263`；保留遠端兩筆同步提交，未強制推送。dev lock 使用 `rebuild.py --update-lock` 重產，沒有手拼 hash。
- main 的道具轉移測試與變身測試在 `build.yml`、`DemoChecks.csproj`、`Program.cs` 發生入口衝突，已保留兩邊的測試與 CI 掛接。`docs/REPORT.md` 完全採 main 版本；未新增功能修改。
- 已驗證：177 個來源、186 個鎖定輸入；沿用上述本機唯讀 NuGet 設定完成產品與 launcher 建置。產品仍有既有 `MagicUi.cs:20` CS8600／CS8602 兩個警告、零錯誤；launcher 零警告／零錯誤。
- 既有離線回歸全數通過：`polymorph-cadence` 123 項、`map-continuity` 218 項、`combat-admission` 302 項、`item-transfer` 163 項（各組包含基本變身檢查）。素材拒收與差異空白檢查結果見本輪 PR 留言。
- 證據沿用 `scratch/issue-263/build/`，測試結果為 `scratch/issue-263/polymorph-cadence.json`、`map-continuity.json`、`combat-admission.json`、`item-transfer.json`。遠端新 HEAD 結果見既有 [PR #278 Checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/278/checks)。
- 未驗證：原生遊戲、伺服器、DB、實際外觀與音效；本輪均未啟動。下一棒由 Claude／調度器確認新 HEAD CI 後審查，合併依既有分工處理。
## 本輪補同步：佈告欄合入後

- 先快轉保留遠端 `task/263` 的 `ea8d77d`，再合併 `origin/main`（`bbaac76b815c9b08c51188e63a5cc627724ce646`）。僅解決 `WorldSession.cs` 與 DemoChecks 入口衝突，保留變身及佈告欄兩邊的既有分派與測試；沒有新增功能。`docs/REPORT.md` 與 main 完全一致。
- dev lock 由 `rebuild.py --update-lock` 重產並核驗：179 個來源、188 個鎖定輸入。產品與 launcher 建置通過；產品仍有上述兩個既有 nullable 警告，launcher 與 DemoChecks 零警告、零錯誤。
- 既有離線測試通過：`polymorph-cadence` 123 項、`map-continuity` 448 項（已包含 main 的佈告欄回歸）、`combat-admission` 302 項、`item-transfer` 163 項、`board` 320 項。
- `check-no-game-assets.py`：3,293 個追蹤檔、4 個 EmbeddedResource 宣告、零錯誤；`git -c core.whitespace=cr-at-eol diff --check` 通過。
- 本機證據沿用 `scratch/issue-263/build/` 與上述群組同名 JSON。新 HEAD 的遠端 CI 結果另記於 PR #278；本輪未啟動遊戲、伺服器或 DB，原生外觀與音效仍待 M6 smoke。

## 給下一棒的提示

本輪同步 `origin/main` 至 `ccaa3e02caf1c0cc2c0696683e243c1c7577df4e`：僅 dev lock 發生衝突，已用 `--update-lock` 重產為 180 個來源、189 個鎖定輸入；main 的受擊抑制與本分支的變身、佈告欄、道具轉移測試入口均保留，未追加功能修改。`docs/REPORT.md` 與 main 完全一致。

本輪產品、launcher、DemoChecks 建置通過；產品僅有既有 `MagicUi.cs:20` 的 CS8600／CS8602，其他建置零警告。離線回歸全綠：`polymorph-cadence` 123 項、`map-continuity` 448 項、`combat-admission` 368 項、`item-transfer` 163 項、`board` 320 項、`hit-reaction-suppress` 156 項（各組含基本變身檢查）。素材拒收檢查 3,289 個追蹤檔、4 個 EmbeddedResource 宣告、零錯誤；dev lock 唯讀核驗與差異空白檢查通過。證據沿用 `scratch/issue-263/build/` 與群組同名 JSON；最終提交 CI 結果記於 PR #278 本輪 RESULT 留言。未啟動遊戲、伺服器或 DB，原生驗收仍待 M6 smoke。

Claude 審查 PR；合併由使用者處理。調度器同步輪若遇 dev lock 衝突，依契約重新產生，不手拼 hash。M6 smoke 由 dot 依既有授權測試：自身／其他角色變身、保留／卸下武器、攻擊與行走、S119 更新、解除、同圖重設；若指定 gfx 缺素材，另開 resource 任務取得經驗證 profile，不以假圖補足。本次未改 `docs/REPORT.md`。
