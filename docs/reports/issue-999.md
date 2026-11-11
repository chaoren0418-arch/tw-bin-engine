# Issue #999：dotnet 首次執行把各 worktree 的 `.dotnet\tools` 加進使用者 PATH

## 結論
所有**還在用、沒有封存**的 dotnet 入口，只要設了 `DOTNET_CLI_HOME`，都在同一處加上 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false` 與 `DOTNET_NOLOGO=1`。CLAUDE.md（並同步 AGENTS.md、`.cursor/rules/project.mdc`）「技術棧與建置」新增一條規則。有 SHA 封存的歷史腳本照規定不改，下面列出例外清單和理由。

## 做了什麼
### 已加上變數（`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`）
| 檔案 | 說明 |
|---|---|
| `engine/_integration/dev/rebuild.py` `build_env()` | 主要根因（原 :183）。`build-product.ps1` → `dev/product-build.ps1` → `rebuild.py --build` 的 dotnet 呼叫都用這組 env |
| `engine/tools/check-local-resources.py` | 本機資源檢查，會自己設 CLI home 並跑 dotnet |
| `engine/_integration/dev/ground-tests/legacy.py` | |
| `engine/_integration/dev/{combat-sfx,effect6016,location-share,mail,player-chat,world-ui}-tests/run.ps1` | 6 支測試腳本 |
| `engine/_integration/r29/rebuild.py`、`r34/rebuild.py`、`r35/rebuild.py` | 歷史腳本，但沒有被任何 lock／seal 鎖住，所以照改；它們呼叫的 `product-build.ps1` 會繼承這組 env |
| `engine/_integration/dev/{login,skill-cast,doors,sound-packets}-tests/README.md` | 文件裡的手動命令與說明一起補上 |

### 檢查過、不用改的入口
- `engine/build-product.ps1`、`engine/_integration/dev/product-build.ps1`：自己不呼叫 dotnet，只呼叫 `python rebuild.py --build`，env 由 rebuild.py 設定。兩支都被 dev lock 鎖住，PR 不改 lock（T271），所以也不改它們。
- `啟動引擎.cmd`：只啟動 `releases\r33\launcher\P24.Launcher.exe`，不呼叫 dotnet。
- `engine/launch-product.ps1`：不呼叫 dotnet。
- `engine/_integration/dev/rebuild_tests.py`、`test_rebuild_paths.py`：不跑 dotnet。
- `.github/workflows/*`：CI 跑 ubuntu／Linux，照 Issue 指示不改。

### 封存例外（有 SHA 鎖定，不改）
| 檔案 | 鎖在哪裡 |
|---|---|
| `engine/_integration/common.py` | r29–r34 `rebuild-lock.json`、`candidate-seal-manifest.json` |
| `engine/_integration/r29`–`r34/product-build.ps1` | 各版 `rebuild-lock.json` |
| `engine/_integration/r30`–`r33/rebuild.py` | 各版 `acceptance/overlay-manifest.json`、`accepted-stage-seal.json` |
| `engine/_integration/delta/build-product.ps1` | `candidate-seal-manifest.json` |
| `engine/_integration/r29/history-r28/{build-product.ps1,common.py}` | r29 `rebuild-lock.json` |
| `engine/_integration/changes.diff` | 歷史 diff 文字，不會執行 |

改這些檔案會讓 r29–r34 的核驗失敗，包括正式入口 r33。它們都從 `os.environ` 複製環境，所以只要呼叫端 shell 先設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false` 就不會寫 PATH。這點已寫進 CLAUDE.md 的新規則。調度器的執行者環境已經設了這個變數。

## 怎麼驗證（已驗證）
- 所有改過的 `.py` 都用 `ast.parse` 檢查過語法；6 支 `run.ps1` 用 PowerShell Parser 解析，錯誤 0。
- `python -B -X utf8 engine/_integration/dev/test_rebuild_paths.py`：7 個測試 OK。`rebuild_tests.py`：6 個測試 OK。兩者都不跑 dotnet。
- 逐檔 grep `DOTNET_CLI_HOME`（不含 bin/obj/build/vendor/assets）：除了上面的封存例外，每個檔案都有 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH`。
- 沒有改 `rebuild-lock.json`，也沒有改 `docs/REPORT.md`。

## 未驗證什麼（待驗證）
- 依本機限制，這次沒有在本機跑 `rebuild.py --build`。要等合併後的下一次建置確認使用者 PATH 不再新增 `...\.dotnet\.dotnet\tools`：建置前後各讀一次 HKCU `Path` 的項數來比對，由調度器的 PATH 長度監看負責。
- 已經寫進使用者 PATH 的約 440 項舊資料，這張不清理（不改登錄檔），交給本機調度器對話處理。

## 產出路徑
- 本報告：`docs/reports/issue-999.md`
- 分支：`task/999`

## 給下一棒的提示
- 新增任何會設 `DOTNET_CLI_HOME` 的腳本，都照 CLAUDE.md 新規則在同一處加 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`。審查時可以用 `grep -rln DOTNET_CLI_HOME engine/` 對照 `grep -rL DOTNET_ADD_GLOBAL_TOOLS_TO_PATH`。
- `dev/rebuild.py` 改了，但它本身不在 dev lock 裡（lock 只鎖 `dev/product-build.ps1` 等檔），所以合併後不一定要重產 lock，照調度器流程處理即可。
