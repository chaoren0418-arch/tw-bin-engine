# engine/_integration/dev — dev 滾動 lock 與建置

`rebuild.py` 核對 `rebuild-lock.json`（來源與建置輸入的 size／SHA-256）並建出 dev 產品。完整建置說明見 [docs/BUILD.md](../../../docs/BUILD.md)。

## lock 政策（#646）
- **PR 不修改 `rebuild-lock.json`。** 改了程式也不要跑 `--update-lock`；同步 main 時 lock 衝突一律取 main 版本。
- lock 在**合併進 main 之後**由調度器以 `rebuild.py --update-lock` 重產並 commit（調度器端需求見 #644）。

## 模式
| 指令 | lock 不一致時 | 用途 |
|---|---|---|
| `rebuild.py`（不帶參數） | 拒絕（exit 2） | 嚴格核驗；main CI 用 |
| `rebuild.py --verify` | 拒絕 | 嚴格核驗＋比對既有產物與 `receipt.json` |
| `rebuild.py --build` | 印 `WARN`，改用記憶體中的暫時 lock 繼續建置，**不寫回 lock**；`result.json` 記 `lock: temporary` 與 added／removed／changed | PR、閘門、本機開發 |
| `rebuild.py --build --strict` | 拒絕 | 舊行為；需要時在 main 用 |
| `--lock-out PATH` | 另外把目前輸入重掃成 lock 寫到 PATH（與 `--update-lock` 產出逐 byte 相同）；不能指向 `rebuild-lock.json` | CI artifact |
| `rebuild.py --update-lock` | 重寫 `rebuild-lock.json` 後嚴格核驗 | 只在合併後由調度器執行 |

缺少必要輸入檔、路徑逃出 `engine/` 時，任何模式都會拒絕。

## CI（`.github/workflows/build.yml`，`dotnet` job）
- PR 與 main 都用 `--build --lock-out engine/_integration/evidence/ci-dev/candidate-lock/rebuild-lock.json`（容許暫時 lock）。
- 只有 push 到 main 時跑嚴格核驗；lock 過時就失敗，並上傳 artifact `rebuild-lock-candidate`（不自動 commit）。
- 單元測試：`test_rebuild_paths.py`、`rebuild_tests.py`（在本目錄執行 `python -B -X utf8 -m unittest test_rebuild_paths rebuild_tests`）。
