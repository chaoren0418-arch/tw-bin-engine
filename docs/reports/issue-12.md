# Issue #12 — T014 rebuild.py 允許 engine/assets junction

階段：code。第 3 次嘗試。執行時間：2026-11-04（雲端 Cursor）。

## 做了什麼

- 已讀 AGENTS.md 與 docs/REPORT.md。未碰機密設定、releases/、封存、硬連結、遊戲程序或資料庫。**本輪不改 `docs/REPORT.md`。**
- 原因對應 Issue #7 真機完整建置最後一步：`REFUSED: input path escapes root: assets/integrity.json`，退出 2。`engine/assets` 是指向 `releases/r28` 的 junction；`resolve()` 用 `p.resolve().is_relative_to(ROOT.resolve())` 跟隨 junction 後跳出 `engine/`。
- 只改 `engine/_integration/r35/rebuild.py` 的容器檢查：改為 `os.path.normpath` 後的**字面（lexical）** `is_relative_to(ROOT)`。既有「非絕對路徑」與 `'..' not in parts` 仍保留。未改 lock、未改 r33/r34。
- 新增／修正 `engine/_integration/r35/test_rebuild_paths.py`：模擬 `assets` 為指向 ROOT 外的 symlink（Windows 則退回 `mklink /J`）時，`resolve('assets/integrity.json')` 不拋錯；`'../x'` 與絕對路徑仍拒絕。暫存目錄用 repo 內 `scratch/issue-12-rebuild-paths`（gitignore），**不寫系統 TEMP**。
- 第 2 次嘗試已合併 `origin/main`（衝突僅 REPORT.md，採 main）。第 3 次依審查退回修正測試暫存路徑與本報告的 stdout／stderr 紀錄。

## 怎麼驗證／已驗證

工作目錄：雲端 checkout `/agent/repos/tw-bin-engine`（Linux）。本環境沒有 `engine/assets` junction（`releases/` 不進 git）。

```text
python3 -B -X utf8 engine/_integration/r35/test_rebuild_paths.py -v
python3 -B -X utf8 engine/_integration/r35/rebuild.py
```

兩條命令分別重導 stdout／stderr 後量測（`unittest.main()` 預設把進度寫到 **stderr**）。

- 路徑單元測試：**通過**，退出碼 **0**（4 tests）。symlink 出 ROOT 的 `assets/integrity.json` 不拋錯；`'../x'`、`assets/../../secret`、絕對路徑仍為 `RuntimeError: input path escapes root: ...`。
- 預設核 pin（無 `--build`）：**通過**，退出碼 **0**。Linux 上來源鎖定仍可核驗。

### 單元測試標準輸出（stdout）

空（0 bytes）。`unittest` 的 `-v` 進度不寫 stdout。

### 單元測試標準錯誤（stderr）

```text
test_absolute_path_rejected (__main__.ResolvePathTests.test_absolute_path_rejected) ... ok
test_assets_symlink_outside_root_is_allowed (__main__.ResolvePathTests.test_assets_symlink_outside_root_is_allowed) ... ok
test_nested_parent_traversal_rejected (__main__.ResolvePathTests.test_nested_parent_traversal_rejected) ... ok
test_parent_traversal_rejected (__main__.ResolvePathTests.test_parent_traversal_rejected) ... ok

----------------------------------------------------------------------
Ran 4 tests in 0.001s

OK
```

退出碼：0。

### 核 pin 標準輸出（stdout）

```text
{"status": "r35-inputs-verified", "sourceFiles": 181, "pinnedFiles": 190, "writes": false}
```

### 核 pin 標準錯誤（stderr）

空（0 bytes）。退出碼：0。

### 來源識別（本輪變更後）

- `engine/_integration/r35/rebuild.py`：`resolve()` 不再呼叫 `Path.resolve()` 做容器檢查。
- `engine/_integration/r35/rebuild-lock.json`：未改動。
- `docs/REPORT.md`：未改動（與 main 相同；調度器合併後自動加索引）。

## 未驗證什麼／推測／待驗證

- **未驗證**：Windows 真機完整建置（`--build --write-receipt`）與 `啟動引擎.cmd --check-only`。Issue #7 的失敗發生在編譯 0 警告之後、核對 product manifest 的 `assets/integrity.json` 時；本環境無 junction、非 Windows，無法重跑那一步。標 `needs:local`。
- **未驗證**：本輪 push 之後的 GitHub Actions（windows-latest `--build`）。先前 head `d892cfb` 的 run 41811956930 為 SUCCESS。CI checkout 通常沒有 `engine/assets` junction，此修補在 CI 上多半走不到該路徑。
- **推測**：真機有 junction 時，lexical 檢查會讓 `assets/integrity.json` 通過容器關，後續 `pin()` 仍會讀 junction 目標並核 size/sha256。需本機 T015 重跑 Issue #7 流程確認。
- 未啟動遊戲、未連正式服務或 DB、未複製 pak/map/sprite、未修改硬連結或封存。

## 產出路徑

- `engine/_integration/r35/rebuild.py`
- `engine/_integration/r35/test_rebuild_paths.py`
- `docs/reports/issue-12.md`（本報告）
- 暫存（不進 git）：`scratch/issue-12/`、`scratch/issue-12-rebuild-paths/`

## 給下一棒的提示

請 Claude 再審 PR #14（分支 `task/12`）：審查退回三點已處理——測試暫存改 `scratch/`、報告正確分開 unittest 的 stderr 與核 pin 的 stdout、不再把 `docs/REPORT.md` 列為本 PR 產物。合併後由本機執行器（`needs:local`，T015／#13）重跑 `python -B -X utf8 engine/_integration/r35/rebuild.py --build --write-receipt`，預期不再出現 `REFUSED: input path escapes root: assets/integrity.json`。原生畫面／登入仍由使用者驗收（`needs:user`）。
