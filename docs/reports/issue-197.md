# Issue #197／T109 通用道具 use_type 尾段

## 做了什麼

本輪依主控要求只做同步與重驗。原有 use_type 尾段編碼、目標選取 UI 與離線測試維持不變。

- 在既有 `task/197` 合併 `origin/main` 的 `25bc0f42c6eb9aae18db98771fe8b03c68710e77`，提交 `feec2eb`，無衝突。
- 納入遠端 `task/197` 已有的主線同步提交 `0ef08a3`，提交 `f0ba32b`，保留遠端歷史。
- 查閱 [CI 失敗紀錄](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42314502009/job/128223701087)，失敗步驟為 `dev canonical build + receipt`，訊息為 `sourceCount 175 != pinned source files 176`；本機更新前同樣重現。
- 執行 `rebuild.py --update-lock`，僅將來源計數由 175 修正為 176；沒有手動拼接 hash。
- `docs/REPORT.md` 與同步時的 main 完全一致；本輪未修改產品程式、測試或其他模組內容。

## 怎麼驗證（已驗證）

- SDK `10.0.300`。`rebuild.py --update-lock` 與隨後來源核驗通過：176 個來源、185 個 pinned 輸入。
- `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-197/build` 通過，包含原 CI 失敗的 canonical build。產品 0 錯誤、2 項既存 `MagicUi.cs` nullable 警告；launcher 0 錯誤、0 警告。
- `ItemUseChecks`：426 項通過，使用本輪產品 DLL、合成封包與記憶體 sender，沒有啟動遊戲或連線。
- `DemoChecks`：`slow-actions` 364 項、`combat-admission` 212 項、`map-continuity` 128 項皆通過。
- `check-no-game-assets.py`：3275 個追蹤檔案、0 errors。
- `git diff --check` 通過；本模組程式與同步前 `5a0dfd0` 無差異。
- 產品 DLL SHA-256：`af144b94318bf7b3181366135988dc5340fba3b4d5b274449cfb7498bd2ab3d1`。
- 推送後的最終 head 與 CI 結果記錄於既有 [PR #223](https://github.com/chaoren0418-arch/tw-bin-engine/pull/223) 的本輪驗收留言，避免報告自我引用提交雜湊。

驗證命令使用環境提供的 Python 3.12 執行檔；測試命令與環境設定見 `scratch/issue-197/verify.ps1`。NuGet、dotnet 與暫存寫入均位於本 worktree。

## 未驗證什麼

- 未做原生操作、視窗焦點、實際物品效果與原服拒絕回包驗收，留給 dot 的 M6 smoke。
- 未啟動遊戲、伺服器或 DB，未改動封存與原始專案檔案；Git 僅更新 worktree 必需的共用中繼資料。
- 原功能限制維持：傳送書籤使用手動 ID，料理與空白卷軸使用數字編號，信件使用代碼／收件人／主旨／本文欄位；書籤名稱列表與料理回包選單仍待相關 UI 模組接續。

## 產出路徑

- 本輪：`engine/_integration/dev/rebuild-lock.json`、`docs/reports/issue-197.md`。
- 既有功能：`engine/source/Inventory.cs`、`engine/source/InventoryUi.cs`、`engine/source/ItemUseArguments.cs`。
- 既有測試與協定：`engine/_integration/dev/item-use-tests/`、`docs/protocol/item-use.md`。
- 未追蹤證據：`scratch/issue-197/build/`、`item-use-checks.json`、`slow-actions.json`、`combat-admission.json`、`map-continuity.json`（後四者亦位於 `scratch/issue-197/`）。

## 給下一棒的提示

請 Claude／調度器核對 PR #223 最新 head 的 CI 後續審，再依既定流程交使用者合併。dot 的 M6 smoke 請覆蓋鑑定／武防強化選物品、魔杖或卷軸選世界目標、復活選屍體、釣魚選地面、變身名稱、傳送、取消／換圖後重試，以及緩速下使用帶參數道具；核對實際物品數量與效果回包。worktree 留待調度器清理。
