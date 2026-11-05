# Issue #67 — T050：CI 改用 Linux 交叉建置

## 結論

已完成程式與 workflow 修改，分支 `task/67`。本機 Windows 建置、交叉建置參數分支檢查、receipt 核驗與 PacketTraceChecks 通過。**尚未取得 ubuntu-latest CI 全綠證據，因此驗收仍待 CI 實跑**；任務已告知私有儲存庫 Actions 額度耗盡，須由使用者處理額度後再驗。

## 做了什麼

- `.github/workflows/build.yml` 改為 `ubuntu-latest`，安裝 Python 3.12；產品 restore／build 同時指定 `EnableWindowsTargeting=true`、`win-x64`、`SelfContained=false`。
- .NET 指令從 `engine/` 執行，使 `engine/global.json` 的 SDK 10.0.300 固定版本真正參與選版。
- `engine/_integration/dev/rebuild.py` 解除非 Windows 建置禁令；非 Windows 的產品與 launcher restore／build 使用相同 Windows 目標參數，檢查兩個 `.exe` 啟動檔存在，輸出目標建置成功訊息。Windows 原生建置參數維持原狀。
- 核驗成功後一律輸出 `dev-inputs-verified`，包含 `--build`／`--verify`；保留實際建置、manifest SHA-256 核驗、receipt 與非零退出碼檢查。
- PacketTraceChecks 保留原生 `net10.0` 執行，不套用 Windows RID；新增使用 CI NuGet 設定的明確 restore，執行時 `--no-restore`。
- workflow 明列 Windows 專屬項目的 `SKIP` 與原因；既有 receipt／失敗證據上傳流程保留。未加入忽略測試失敗的設定。
- 未改產品程式、lock、assets junction、封存或 `docs/REPORT.md`；未啟動遊戲、launcher、正式服務或 DB。

`EnableWindowsTargeting` 用於下載 Windows 目標參考套件；另指定 RID 以產出 Windows 啟動檔，並不代表可在 Linux 執行 WinForms。參考 [Microsoft NETSDK1100 說明](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100) 與 [RuntimeIdentifier 設定](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#runtimeidentifier)。

## 怎麼驗證（已驗證）

環境：Windows、.NET SDK 10.0.300、Python 3.12；實際輸入複製至本 worktree 的 `scratch/issue-67/fixture/`，保留原始位元組與鎖定檔。原始專案目錄未寫入。

| 檢查 | 結果 |
| --- | --- |
| worktree 執行 `rebuild.py` 唯讀核驗 | 退出 0；`dev-inputs-verified`；190 個來源檔、199 個鎖定檔；`writes: false` |
| actionlint v1.7.12 檢查 `.github/workflows/build.yml` | 退出 0，無診斷 |
| 隔離副本原生 `rebuild.py --build --write-receipt` | 產品、launcher restore／build 均退出 0；主程式 0 警告、0 錯誤 |
| 非 Windows 分支檢查 | 僅將 Python `platform.system()` 分支選擇模擬為 Linux；**編譯器、檔案系統與作業系統仍是 Windows，不算 Linux 執行證據**。四個實際 restore／build 指令都帶 Windows 目標參數並成功；主程式 0 警告、0 錯誤 |
| Windows 啟動檔格式 | 實際讀取產品與 launcher 的 PE 標頭，均為 MZ／PE、Machine `0x8664`（x64），未執行啟動檔 |
| 隔離副本 `rebuild.py --verify` | 退出 0；`pass-dev-verify`，receipt 與 manifest 核驗通過 |
| PacketTraceChecks（Windows） | 退出 0；7 個案例、36 個斷言通過；`gameStarted: false`、`network: false` |
| `git diff --check` | 通過 |

首次 NuGet 還原遇到沙箱 TLS `SEC_E_NO_CREDENTIALS`；經允許的非沙箱重試後完成實際還原與建置。沒有把失敗結果計為通過。

非 Windows 參數分支在 Windows 上產出的 SHA-256（僅作本次本機證據）：

| 產物 | SHA-256 |
| --- | --- |
| `P11.ClientEngine.dll` | `d032f3078ba0b3096ea247f106a1ef821d46f42fbd357559b0ec1c76467dbd19` |
| `P24.Audio.dll` | `4d4f38d2757b9bee38e731e16161d811eab4c5885273ba21e602b06d82f2e8e9` |
| `P24.Launcher.dll` | `8ba120fe1a9448f9fa03e2e8357ba477aa24797088dfd2a211754a2cc7305993` |

## 未驗證什麼／與原 windows-latest 的差異

- 本機沒有可用 Linux／WSL，尚未驗證 Ubuntu 上的還原、跨平台 SDK 行為、大小寫敏感路徑、Linux PacketTraceChecks 及 Actions artifact 上傳。上述相容性預期仍需 CI 證實。
- 原 workflow 也只建置產品與 launcher，沒有執行 WinForms／WebView2 或登入遊戲；本次明確揭露這項限制，不能將 CI 綠燈視為實機驗收。
- 原 Windows runner 執行 PacketTraceChecks 時建立 NTFS junction；Linux 執行既有符號連結分支，會少驗 Windows junction／Windows 檔案系統語義。Linux 該案例預期少一個 junction 建立斷言（35 對 Windows 36），不是刪除測試。
- Windows 啟動檔可執行性、WinForms／WebView2 執行期、原生畫面／輸入／音訊、遊戲登入與操作，交由本機 Windows 檢查及使用者／具既有授權的 dot 真機驗收。本任務未做這些操作。
- 不保證 Linux 與 Windows 建置的 DLL／receipt 位元組相同；receipt 代表當次實際目標建置，未變更 lock 或歷史 release。

## 產出路徑

- 交付：`.github/workflows/build.yml`、`engine/_integration/dev/rebuild.py`、本報告 `docs/reports/issue-67.md`。
- 本機暫存驗證腳本：`scratch/check_issue67.py`（不入 git）。
- 本機證據：`scratch/issue-67/native/`、`scratch/issue-67/cross-branch/`、`scratch/issue-67/verify/`，以及 `scratch/issue-67/packet-checks.stdout`（不入 git）。
- CI 預定證據：artifact `dev-receipt`，包含 `engine/_integration/dev/receipt.json` 與 `engine/_integration/evidence/ci-dev/`。

## 給下一棒的提示

1. 調度器／Claude 審查本 PR；由使用者處理 Actions 額度或儲存庫可見性決策，本任務不改費用或公開設定。
2. 額度解除後由調度器重跑 PR 的 `build / dotnet`，確認 runner 為 `ubuntu-latest`，log 有 `dev-inputs-verified`、`Windows target win-x64 build succeeded`、`PASS dev`、PacketTraceChecks `status: pass` 及明確的 `SKIP` 理由；檢查 artifact 內 `result.json` 為 `pass-dev-build`。
3. 若失敗，依實際 stdout／stderr 修正；不得以本次 Windows 分支檢查代替 Ubuntu 全綠驗收。
4. 本機執行器維持 Windows 自動檢查；使用者／具既有授權的 dot 接手真機驗收。Claude 審查、CI 通過後由使用者合併，本任務不合併。
