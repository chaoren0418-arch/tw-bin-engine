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

## 給下一棒的提示

Claude 審查 PR；合併由使用者處理。調度器同步輪若遇 dev lock 衝突，依契約重新產生，不手拼 hash。M6 smoke 由 dot 依既有授權測試：自身／其他角色變身、保留／卸下武器、攻擊與行走、S119 更新、解除、同圖重設；若指定 gfx 缺素材，另開 resource 任務取得經驗證 profile，不以假圖補足。本次未改 `docs/REPORT.md`。
