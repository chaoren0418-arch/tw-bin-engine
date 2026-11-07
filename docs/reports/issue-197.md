# Issue #197／T109 通用道具 use_type 尾段

## 做了什麼

本輪僅同步主線、重新產生 dev lock 與重驗，沒有新增功能或修改既有測試。

- 在既有 `task/197`（同步前 `a06a167`）執行 `git fetch origin`、合併 `origin/main` 的 `ccaa3e02caf1c0cc2c0696683e243c1c7577df4e`。
- 唯一衝突為 `engine/_integration/dev/rebuild-lock.json`，透過 `rebuild.py --update-lock` 重新產生，未手動拼接 hash；180 個來源、189 個 pinned 輸入。
- `Inventory.cs` 自動納入 main 的 S242 subtype 2 受擊反應分派，保留本分支完整道具使用流程；`WorldSession.cs` 保留 main 的公告板分派與狀態重設。其他模組與測試均使用 main 內容。
- `docs/REPORT.md` 與本次同步的 main 完全一致，未另行編輯。
- 核對 [原 CI 失敗 log](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42314502009/job/128223701087)：`dev canonical build + receipt` 因 `sourceCount 175 != pinned source files 176` 退出 2。上一輪已修正；本輪再次執行同一 canonical build 路徑通過。
- GitHub 查詢顯示既有 [PR #223](https://github.com/chaoren0418-arch/tw-bin-engine/pull/223) 仍開啟、尚未合併，因此推送原分支更新該 PR。

## 怎麼驗證（已驗證）

- SDK `10.0.300`；`rebuild.py --update-lock` 通過。
- `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-197/build` 通過。產品 0 錯誤、2 項既存 `MagicUi.cs` nullable 警告；launcher 0 錯誤、0 警告。
- `ItemUseChecks`：426 項通過。
- `DemoChecks`：`slow-actions` 364 項、`combat-admission` 278 項（含受擊反應抑制）、`map-continuity` 358 項（含公告板）、`item-transfer` 73 項皆通過。
- 上述測試使用本次產品 DLL、合成封包與記憶體 sender，並非真機證據。
- `check-no-game-assets.py`：3291 個追蹤檔案、0 errors。相對 `origin/main` 的 `git diff --cached --check` 通過；相對同步前 HEAD 的檢查會列出 main 既存 CRLF 空白警告，依同步範圍保留原樣。
- 核對 `InventoryUi.cs`、`ItemUseArguments.cs`、`LoginConnection.cs` 與 `item-use-tests/` 均與同步前一致；`Inventory.cs` 僅新增 main 的受擊反應分派；其他模組與 main 一致。
- 產品 DLL SHA-256：`9a57e612a39d3c76a62fd8d20a9a2a2fe7a3793524e346b0f6a41a81824d585c`。
- 推送後最新 head 與 CI 結果另記於 PR #223 本輪驗收留言，避免文件提交造成自我引用。

驗證使用環境提供的 Python 3.12 執行檔；測試命令與環境設定見 `scratch/issue-197/verify.ps1`。NuGet、dotnet 與暫存寫入均位於本 worktree。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB；未驗證原生操作、視窗焦點、實際物品效果與原服拒絕回包，留給 dot 的 M6 smoke。
- 原功能限制維持：傳送書籤使用手動 ID，料理與空白卷軸使用數字編號，信件使用代碼／收件人／主旨／本文欄位；書籤名稱列表與料理回包選單仍待相關 UI 模組接續。
- 未改動封存、原始專案內容或機密設定；Git 僅更新 worktree 必需的共用中繼資料。

## 產出路徑

- 同步產出：main 既有變更、`engine/_integration/dev/rebuild-lock.json`、`docs/reports/issue-197.md`。
- 未追蹤證據：`scratch/issue-197/build/`、`scratch/issue-197/item-use-checks.json`、`slow-actions.json`、`combat-admission.json`、`map-continuity.json`、`item-transfer.json`（後四者亦位於 `scratch/issue-197/`）。

## 給下一棒的提示

請 Claude／調度器核對 PR #223 最新 head 的 CI 並續審，再交使用者合併。dot 的 M6 smoke 請覆蓋鑑定／武防強化選物品、魔杖或卷軸選世界目標、復活選屍體、釣魚選地面、變身名稱、傳送、取消／換圖後重試，以及緩速下使用帶參數道具；核對實際物品數量與效果回包。worktree 留待調度器清理。
