# Issue #12 — T014 rebuild.py 允許 engine/assets junction

階段：code。執行時間：2026-11-04（雲端 Cursor）。

## 做了什麼

- 已讀 AGENTS.md 與 docs/REPORT.md。未碰機密設定、releases/、封存、硬連結、遊戲程序或資料庫。
- 原因對應 Issue #7 真機完整建置最後一步：`REFUSED: input path escapes root: assets/integrity.json`，退出 2。`engine/assets` 是指向 `releases/r28` 的 junction；`resolve()` 用 `p.resolve().is_relative_to(ROOT.resolve())` 跟隨 junction 後跳出 `engine/`。
- 只改 `engine/_integration/r35/rebuild.py` 的容器檢查：改為 `os.path.normpath` 後的**字面（lexical）** `is_relative_to(ROOT)`。既有「非絕對路徑」與 `'..' not in parts` 仍保留。未改 lock、未改 r33/r34、未改其他引擎檔。
- 新增 `engine/_integration/r35/test_rebuild_paths.py`：模擬 `assets` 為指向 ROOT 外的 symlink 時，`resolve('assets/integrity.json')` 不拋錯；`'../x'` 與絕對路徑仍拒絕。

## 怎麼驗證／已驗證

工作目錄：雲端 checkout `/agent/repos/tw-bin-engine`（Linux）。本環境沒有 `engine/assets` junction（`releases/` 不進 git）。

```text
python3 -B -X utf8 engine/_integration/r35/test_rebuild_paths.py -v
python3 -B -X utf8 engine/_integration/r35/rebuild.py
```

- 路徑單元測試：**通過**，退出碼 **0**（4 tests）。symlink 出 ROOT 的 `assets/integrity.json` 不拋錯；`'../x'`、`assets/../../secret`、絕對路徑仍為 `RuntimeError: input path escapes root: ...`。
- 預設核 pin（無 `--build`）：**通過**，退出碼 **0**。Linux 上來源鎖定仍可核驗。

### 單元測試完整標準輸出（stdout）

```text
test_absolute_path_rejected (__main__.ResolvePathTests.test_absolute_path_rejected) ... ok
test_assets_symlink_outside_root_is_allowed (__main__.ResolvePathTests.test_assets_symlink_outside_root_is_allowed) ... ok
test_nested_parent_traversal_rejected (__main__.ResolvePathTests.test_nested_parent_traversal_rejected) ... ok
test_parent_traversal_rejected (__main__.ResolvePathTests.test_parent_traversal_rejected) ... ok

----------------------------------------------------------------------
Ran 4 tests in 0.001s

OK
```

### 核 pin 完整標準輸出（stdout）

```text
{"status": "r35-inputs-verified", "sourceFiles": 181, "pinnedFiles": 190, "writes": false}
```

### 完整標準錯誤（stderr）

空（兩條命令皆 0 bytes）。

### 來源識別（本輪變更後）

- `engine/_integration/r35/rebuild.py`：`resolve()` 不再呼叫 `Path.resolve()` 做容器檢查。
- `engine/_integration/r35/rebuild-lock.json`：未改動。

## 未驗證什麼／推測／待驗證

- **未驗證**：Windows 真機完整建置（`--build --write-receipt`）與 `啟動引擎.cmd --check-only`。Issue #7 的失敗發生在編譯 0 警告之後、核對 product manifest 的 `assets/integrity.json` 時；本環境無 junction、非 Windows，無法重跑那一步。
- **未驗證**：GitHub Actions windows-latest 的 `--build`（PR CI）。CI checkout 通常也沒有 `engine/assets` junction，故此修補在 CI 上多半走不到該路徑；預期核 pin / 建置不因本次改動變紅。
- **推測**：真機有 junction 時，lexical 檢查會讓 `assets/integrity.json` 通過容器關，後續 `pin()` 仍會讀 junction 目標並核 size/sha256。需本機 T015 重跑 Issue #7 流程確認。
- 未啟動遊戲、未連正式服務或 DB、未複製 pak/map/sprite、未修改硬連結或封存。

## 產出路徑

- `engine/_integration/r35/rebuild.py`
- `engine/_integration/r35/test_rebuild_paths.py`
- `docs/reports/issue-12.md`（本報告）
- `docs/REPORT.md`（本輪變更與下一步各一條 Issue #12）

## 給下一棒的提示

請 Claude 審查 PR（分支 `task/12`），確認 lexical 檢查與測試範圍。合併後由本機執行器重跑 Issue #7／T015：`python -B -X utf8 engine/_integration/r35/rebuild.py --build --write-receipt`，預期不再出現 `REFUSED: input path escapes root: assets/integrity.json`。原生畫面／登入仍由使用者執行。
