# Issue #1247：f1211-smoke-tooling 工具補強

結論：部分通過（✅ 8／❌ 0／⚠️ 2）。只做工具自測，沒有作任何 SM 功能判定；未執行（時間）0 項。

## 做了什麼

- 只改 `work/f1118-uia-path/` 與本報告。沒有改產品、dev lock、登錄檔、系統顯示設定、服務或權限；`docs/REPORT.md` 只接受 main 的同步內容。
- `SmokeRetest.ps1`：同執行緒暫切目標 DPI context → PrintWindow 全窗 → client 裁切 → finally 還原；另提供世界 Ctrl+P 畫布複製及 IME 執行緒診斷。
- `SmokeTools.ps1`：清空預設最多 3 次（可設 1–5）、主 HWND 前景核對、Backspace 分批等待、每次空白／W 正向控制／清除圖，以及失敗歷程；送出前置失敗不打字、不按 Enter，送出仍以 packet-trace 驗證。DPI 96 清空使用新擷取座標，不混用實體與虛擬像素。
- README 新增可直接執行的 `f1211-retest` 章節，列出每個新函式用法、DPI 證據界線與 IME 人工驗收步驟。`Test-SmokeRetest.ps1` 的 mock 明確只驗證工具契約。

## 逐項自測

操作時間為 2026-11-14 19:14–19:27（台北）；19:28:55 已完成所有自啟程序收尾。證據路徑皆相對本 worktree 的 `environments/p24/backups/issue-1247-evidence/`，另有工作原件 `scratch/issue-1247/`。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| B01 | ✅ | 本機產品建置退出 0；同步 main 後再建退出 0，product/audio/launcher SHA 均未變。 | 設定安全的 DOTNET／TEMP／NuGet 環境後執行 build-product.ps1，ConfigFile 指定 .github/nuget.ci.config。 | `build.log`、`sync-build.log`、`execution.json` |
| D01 | ✅ | 新 PrintWindow：192／96 均完整 800×600；同角色選角、同王族建角能力頁各 480,000 像素差異 **0**，四邊差異 0；context 均已還原。 | 正常程序及僅子程序 DPIUNAWARE，各進同頁呼叫 Get-SmokeDpiCapture。 | `client-A/dpi192-selection.png`、`client-D/dpi96-selection.png`、`client-A/dpi192-create.png`、`client-D/create96-stat.png`、各 `.json`、`dpi-comparison.json` |
| D02 | ✅ | 兩個 DPI 的 Ctrl+P 均產生完整 800×600 世界畫布；同 map0(32643,32951)，四邊無額外黑區或裁偏，底部 24 列逐像素相同；世界動畫、亮度及 HP 隨時間變化，未宣稱世界全圖相同。 | 使用服務驗證 GM0 進世界後 Save-SmokeCanvasCapture；對照新 PrintWindow 及 state。 | `client-A/dpi192-paired-world-canvas.png`、`client-D/dpi96-paired-world-canvas.png`、各 `-canvas.json`、`paired-world-state.json`、`dpi-comparison.json` |
| C01 | ✅ | DPI192 草稿清空、W 正向控制成功；一般聊天 C136、/focus C129、/checkpk C51、/whisper off/on C26、/buddy C4 均確認送出；各 1 次清空。 | 輸入 draft1247，再依 README 使用 Clear-SmokeChat／Send-SmokeCommand。 | `client-A/clear-draft-clear.json`、`chat-send-send.json`、`command-summary.json`、`command-*-send.json`、`packet-trace.jsonl` |
| C02 | ✅ | DPI96 草稿清空成功；/focus C129、最終版 /checkpk C51 均送出，空白圖與探針圖為 800×600，未使用舊 1600×1200 裁切判空。 | 輸入 DPI96draft，套用同輸入矩形清空；用新工具送兩指令。 | `client-D/clear96-draft-clear.json`、`command96-focus-send.json`、`final96-checkpk-send.json`、`packet-trace.jsonl` |
| C03 | ✅ | 6 個新增離線契約及既有 22 個均通過：失敗兩次後第 3 次恢復；耗盡時 typed=0、Enter=0，send.json 保留 2 次失敗；trace 不存在時不碰草稿。 | 執行 Test-SmokeRetest.ps1 與 Test-SmokeTools.ps1；失敗注入為明示 mock，不冒充真機自然重試。 | `contracts/results.json`、`contracts/exhausted-send.json`、`existing-contracts/results.json` |
| I01 | ⚠️ | 真正聊天 IME 未成立：Ctrl+Space、Shift、S/U/3/Down 只得到 su3；default IME open 請求後仍 0，沒有候選窗／選字。 | 先確定聊天空白與前景，再送 VK；另執行 Get-SmokeImeDiagnostic。 | `client-A/ime-compose-first.png`、`ime-shift-screen.png`、`ime-control-before.json`、`ime-control-open.json`、`ime-thread-diagnostic.json`、`jobs/04-*`、`jobs/06-*`、`jobs/07-*` |
| I02 | ⚠️ | 建角名稱欄也未取得組字或候選證據；沒有建立／刪除角色，不能用 Unicode 輸入代替。 | 建角能力頁點名稱欄，Ctrl+Space→S/U/3→Down，取消離開。 | `client-A/ime-name-composition.png`、`ime-name-candidate.png`、`jobs/09-ime-name.ps1`、`client-D/ime96-diagnostic.json` |
| S01 | ✅ | 客戶端均有 closed 並退出；自啟 Java／MySQL 與兩個 helper 均退出；42061／43324 無監聽。24 個角色最終 GM=0、OnlineStatus=0。 | 客戶端 Alt+F4；核對 Java PID＋啟動時間後關閉；mysqladmin 明確 TCP43324 shutdown；查核自啟 PID 與監聽。 | `cleanup.json`、`characters-before.tsv`、`characters-final.tsv`、`client-A/close.json`、`client-D/close.json` |
| S02 | ✅ | 原 DB 與冷備份 1,722 檔逐檔 SHA 仍相同；原專案唯讀，測試只使用 worktree 隔離副本。 | 開服前完整冷備份＋manifest，再複製為 runtime；收尾重算原 DB 與冷備份。 | `snapshot.json`、`backup-before-manifest.json`、`original-db-verify.json` |

C01 先以新增重試流程驗證 DPI192；隨後整合相同 DPI context 擷取至聊天，最終版以 C02 再驗 C51。沒有宣稱工具在一般桌面切換焦點或自然失敗後已實測第 2／3 次恢復；這部分由 C03 契約覆蓋。

## DPI 方法裁定與 IME 限制

- **f1211-retest 採用 D01**：登入、選角、建角及世界均可用 Get-SmokeDpiCapture；世界另可用 D02 Ctrl+P。舊 DPI96 PrintWindow 的實體 1600×1200 裁切不採用。新方法的 96 是目標視窗有效 DPI；實體桌面仍 192，沒有修改顯示縮放。若後續要求實體桌面 96，須由使用者另行裁定，不可由這份證據擴張推論。
- DPI96 初次點登入被自家 owned 開發工具遮住，工具正常拒點；明確關閉該 owned 視窗後完成。第一次建角快速連送 Enter 尚未轉頁，保留 `client-D/dpi96-create.png` 作未完成轉頁的原始圖，**不拿它當建角證據**；最後配對使用 `create96-stat.png`。這些是操作前置／等待問題，未改產品。
- **IME 尚不可自動驗收**：已載入 HKL 只有 `0x04040404`。同工具執行緒 ActivateKeyboardLayout(flags=0) 成功但 DidChange=false，前後／還原值相同，目標 HKL 未變，open status=0。此結果僅證明本次沒有取得可用方法，不證明引擎有缺陷或 Windows 絕對不能組字。
- [Microsoft IMM 執行緒規則](https://learn.microsoft.com/en-us/windows/win32/intl/developing-ime-aware-multiple-thread-applications) 要求不可存取其他執行緒建立的 HIMC；[ActivateKeyboardLayout](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-activatekeyboardlayout) 僅作用於呼叫執行緒／程序。因此沒有對產品注入程式或用外部 HIMC 呼叫 ImmSetOpenStatus。保留診斷函式，不發布假成功的 IME 輸入功能。SM08／SM12 真正候選組字交使用者驗收，步驟見 README；沒有選字證據就維持 ⚠️。

## 資源、備份及版本

- 起始 lease 確認持有人 `chaoren0418-arch/tw-bin-engine#1247`，到期 `2026-11-14T14:11:06Z`；只使用 P24Lab 42061／43324，未連正式 2000／3306。
- 冷備份在 `scratch/issue-1247/backup-before/db-data/`，279,511,437 bytes；manifest SHA `1fb267532c07870f087ce58d91fd820a855c189cfa74c0378e681c7609d224b4`。來源與備份與初始 runtime 三份逐檔相同。此輪沒有直接 DB 寫入、GM 提升或還原 SQL 需求；遊戲正常保存的 HP／位置等只在隔離 DB，原庫沒有套用任何 SQL。
- MySQL 首次以 Unicode 命令路徑啟動失敗，改用既有 `D:\BinEngine` alias 指向相同 worktree 後正常；沒有新增 alias、改設定或服務。伺服器正常開服一次，客戶端分別測 192／96。
- 實測產品來源 `1b165e2a0401a6932e1d69fc3edc1bf7220113d2`；最後 fast-forward `2e8baae7` 只同步 main 的報告索引，產品與工具來源未變，重建 SHA 一致。
- product DLL SHA `1cb04035a55c3a82bae1d813493a8c0473eec82428849452cbb7b52d9a066046`；audio `4c4aa97837e7c4e610d3e0b75eb2b619faf41508096435e37c635c2cec456473`；launcher `c1c11291e5e9785edbed3aea9df4fee8fc0fcf7377cc8ea8f81e11477348f295`。
- 自啟程序：client 9700／16796，helper 14996／3076，Java 17904，MySQL parent 4832／child 19080，收尾全退出。沒有停既有程序、遞迴刪除或移除 worktree。

## 證據 SHA-256

證據共 240 檔；`manifest.json` SHA `0796d5750002f220dbafe8044ce4d1adc0b87bcc5043fee4b4cc8818df1590af`。下表為主要證據，其餘逐檔 SHA 皆在 manifest。

| 路徑 | SHA-256 |
|---|---|
| `client-A/dpi192-selection.png` | `d7424db115a1e46d86225d7496a01e5af8e30b20e65e5f25225afcc62b9bb34f` |
| `client-D/dpi96-selection.png` | `0daa83e835a19ce726704d0263e17f116b4e9f14e5dc1985d57c8e2d14ded9d9` |
| `client-A/dpi192-create.png` | `39f21a9152877b871f088f53ff43e495f61a3889be00f391e1f91f1f1db4ab08` |
| `client-D/create96-stat.png` | `f939b18a96bef85ed7c4361e3c516213531c4b2bff5e11c0927f2598b4c8092f` |
| `client-A/dpi192-paired-world-canvas.png` | `f19ded150572828ffdb088bf88acff7cdd73a72a4d96513b3f25cbe0e14ec090` |
| `client-D/dpi96-paired-world-canvas.png` | `879961640dd49a9254eb058aa141303e230924b9d5d1fbfdbec37174de7a9ad8` |
| `client-D/final96-checkpk-send.json` | `6e0ded814bbdf940d02b4449c8e03ed77bd9b24ed60a1428323b0cf612584530` |
| `client-A/ime-thread-diagnostic.json` | `f982ec24cd33ffa17e633a8c385c3d51995f6bffe32b13e5823f013078e2c8a4` |
| `cleanup.json` | `89f3fb46fc151a3f9032939059e7d1e93bff977b0d0d3f45e57953bb0a9573b1` |

## 產出與給下一棒的提示

- 追蹤：本報告、README、SmokeTools.ps1、SmokeRetest.ps1、Test-SmokeRetest.ps1。PR 以最新 head 的 CI 為準；本報告不把本機建置替代 CI。
- 調度器先保全本 worktree 的 `environments/p24/backups/issue-1247-evidence/` 與 `scratch/issue-1247/backup-before/`，再依原流程清理；執行器沒有寫回原專案。
- `f1211-retest` 可直接使用新 DPI96 擷取與聊天重試，已通過工具列不需原樣重跑；真正 IME 只補 I01／I02，由使用者操作候選組字或裁定已知限制。這張沒有作 SM 通過／失敗判定，也沒有修改產品去修驗收現象。
