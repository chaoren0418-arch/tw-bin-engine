# Issue #31 — T040 滾動式 dev lock

## 做了什麼

- 新增 `engine/_integration/dev/`：從 r35 複製 rebuild、product-build、pe_resource、launcher 來源與路徑測試；revision、manifest、產品目錄改為 dev。
- 新增 `--update-lock`：重新掃描來源及建置輸入，依路徑排序，以 UTF-8 無 BOM／LF 寫入 lock，再執行預設核驗。無時間戳、無本機絕對路徑，相同 tree 產生相同位元組；不需要舊 dev lock 即可產生。
- 預設核驗比對輸入清單、size 與 SHA-256，可拒絕未更新 lock 的新增、刪除與內容變更。掃描排除建置輸出，缺少必要輸入時不覆寫既有 lock。
- `engine/build-product.ps1` 改用 dev；CI 僅更改 canonical 步驟名稱、腳本與輸出目錄 `engine/_integration/evidence/ci-dev`，其他步驟未改。
- 更新 `docs/BUILD.md` 的開發建置路徑與「改程式之後」流程，寫明 r35 凍結與 M1 基線用途。

## 怎麼驗證（已驗證）

本機 Windows、Python 3.12。命令環境未提供 `python` 別名，使用同一安裝的 `py -3.12 -B -X utf8` 執行：

| 檢查 | 結果 |
|---|---|
| 修改前 `engine/_integration/r35/rebuild.py` | exit 0；181 來源／190 pinned 輸入 |
| `engine/_integration/dev/rebuild.py --update-lock` | exit 0；寫入後核驗成功，`writes: true` |
| `engine/_integration/dev/rebuild.py` | exit 0；181 來源／190 pinned 輸入，`writes: false` |
| `engine/_integration/dev/test_rebuild_paths.py` | 7 項通過：原有 4 項路徑測試，以及 lock 確定性／變更復原、增刪與排除、必要輸入遺失保護 |
| `engine/_integration/r35/test_rebuild_paths.py` | 原有 4 項通過 |
| r35／dev lock 逐項比對 | 181 個來源項目（路徑、大小、SHA-256）完全相同；190 個輸入路徑在 r35→dev 映射後完全一致 |
| 修改後 r35 lock 全部 190 個 pin 比對 | 唯一漂移為 `build-product.ps1`，其餘 189 個輸入一致 |
| 禁止修改範圍的 `git diff --exit-code` | exit 0；r33/r34/r35、source/audio/web、releases、project-manifest.json、根入口、docs/REPORT.md 皆未改 |
| `git diff --check` | 通過 |

dev lock SHA-256：`3d52ef60877d6a26cd1daa8d6e28cd613bd51c554b9ad4c5426395fbd8f761b2`。

測試的最小 fake tree 僅用於建置流程測試，存於本 worktree 的 `scratch/issue-31-rebuild-paths/` 並由測試清理，不是遊戲功能證據。另以 `scratch/issue-31-verify.py` 核對真實 lock 的來源、路徑與漂移。

## 驗收矛盾與未驗證什麼

1. **無法同時滿足「r35 不動」、「建置入口改 dev」及「本 PR tree 上 r35 核驗 exit 0」。** r35 lock 本來就釘住 `engine/build-product.ps1`（原 SHA-256 `ef5b0ee27b6ae43d1c050cd0171973b5eedbfb9184850ed27c78ebdfffbd2b3b`）。切換入口後，原封不動的 r35 腳本實際退出 2：`REFUSED: pin drift: .../engine/build-product.ps1`。沒有移除 pin、豁免核驗或改寫 r35；以修改前成功核驗、凍結目錄零差異、其餘 189 個 pin 一致證明基線未被修改。此項驗收條件需由 Claude 主控裁定修正。
2. CI 執行結果待 PR 建立後補記。
3. 依「其他步驟不動」限制，CI artifact 上傳仍讀 r35 receipt 與舊的 `evidence/ci/**`。dev receipt／ci-dev 證據不會由該步驟收集；建置成功仍可由 canonical 步驟日誌核對。建議主控另授權修正上傳路徑。
4. 未在本機執行完整 .NET 建置、launcher check-only 或原生遊戲流程；未操作 DB／服務，未讀寫機密設定、正式客戶端、封存或硬連結。原始專案目錄未寫入。

## 產出路徑

- `engine/_integration/dev/rebuild.py`、`rebuild-lock.json`、`product-build.ps1`、`pe_resource.py`、`test_rebuild_paths.py`
- `engine/_integration/dev/launcher-source/Launcher.csproj`、`Program.cs`
- `engine/build-product.ps1`
- `.github/workflows/build.yml`
- `docs/BUILD.md`
- `docs/reports/issue-31.md`

## 給下一棒的提示

- 分支 `task/31`；Claude 審查實作及上述 r35 驗收矛盾，由使用者合併。
- 後續改程式的 PR 應同步執行 dev `--update-lock` 並提交 lock，不要改 r35；下次真機驗收才從 dev 凍結新 rNN。
- r35 核驗應針對 M1 對應的來源 tree 執行，不應要求持續變動的 dev tree 仍符合 r35 pin。
- 不要把 lock 核驗、fake-tree 測試或 CI 編譯成功等同於原生畫面／遊戲功能驗收。
