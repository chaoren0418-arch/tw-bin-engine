# Issue #1109：P24Lab 重測閘門

**結論：阻塞。R03／R04 尚未完成實機測試；本輪沒有任何功能驗收通過，不是伺服器程式故障的判定。**

2026-11-14（台北）在既有 `task/1109` 執行前置檢查。指定主環境的 42061／43324 沒有監聽；啟動主環境必須寫入其資料目錄，與本張「原始專案目錄……只讀取、不要在那裡寫檔」直接衝突。已提出單一範圍釐清，尚未取得答覆，因此沒有啟動主環境，也沒有改用副本冒充本次指定環境。

## 逐項結果

依本張「R03 或 R04 不通過 → 立刻停止，只交這兩列證據」閘門，只列以下兩項。⚠️ 表示未完成測試，不表示重現 #1098 的限時孤兒故障。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| R03 遊戲服 42061 開服 | ⚠️ | 03:49 查無 42061／43324 監聽；未啟動 MySQL 或 Java，主環境運行寫入範圍待釐清 | 查指定兩埠監聽，唯讀檢查 mysql.ini 的 datadir／日誌／暫存路徑 | `issue-1109-evidence.json`：`initialListeners`、`mysqlPathSettings`、`launchedRuntimePids` |
| R04 登入至選角 | ⚠️ | 03:53 對 127.0.0.1:42061 的 2 秒 TCP 探測逾時；未送帳密、未取得選角回包 | 執行 `python -B -X utf8 scratch/issue-1109/collect_gate.py` 的 TCP 前置探測 | `issue-1109-evidence.json`：`tcpProbes` |

證據 JSON 為 [issue-1109-evidence.json](issue-1109-evidence.json)。完整工作紀錄在本 worktree 的 `scratch/issue-1109/`；截圖與 `native-ui.log` 未產生，因為沒有啟動客戶端。前述 TCP 探測不是登入測試。

## 已驗證

- `git fetch origin` 後 HEAD 與 origin/main 均為 `50679105c5e9643fccc490e1728412730867b8aa`，沒有切換或建立分支。
- main 已包含 #1093（PR #1096，merge `7c31e7ba`）與 #1097（PR #1102，merge `c8a73fb0`）；目前來源可見 `LevelStrip`／`ListArea` 與 `DescribeStartFailure`。
- 03:49 的 `/api/state` 回應：lease 持有人 `chaoren0418-arch/tw-bin-engine#1109`，到期 `2026-11-13T21:19:05.664542Z`，當時有效。後續端點讀取未成功，沒有修改調度器；這不是停止原因。
- 只在 worktree 還原依賴及建置產品，`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o engine/build/client-product-dev` 成功，0 錯誤、4 個既有警告。restore 使用主專案 `engine/vendor` 唯讀來源；CLI home、NuGet 快取與 TEMP 均在 worktree，並設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`。
- 產品 DLL SHA-256：`1642ea357d5300fcdac8f5b40e6595ba3504c17aa10b830d8fd6816041726e93`，與任務提供的 #475 `7f861fce…2c77` 不同。建置成功不代表 check-only、登入或原生 UI 通過。
- 沒有啟動客戶端、登入器、Java 或 MySQL；沒有 DB 查詢／寫入、GM 變更、原專案運行寫入或正式服務連線。採證用的兩個逾時 PowerShell 子程序依自啟 PID 17288／13972 結束；後改用有限逾時的 Python 探測完成採證。沒有遊戲／DB 程序需要收尾。

## 推測／待驗證

- **推測**：本張指定主專案 P24Lab 並允許啟動，可能意在允許其必要運行寫入；但沒有把這項推測當成對明文只讀限制的豁免。
- **待驗證**：R03／R04；閘門後的 R05／R06／R07／R12／R13／R17、R08–R11／R14／R15 唯讀回歸、§0.2 check-only 與 SM01–SM59 均未執行。沒有重測已指定沿用的 R01／R02 或 #475 子步驟。
- **GM**：本輪未提升權限，因 DB 未啟動，不能宣稱本輪已讀回全角色 GM0；也未以 #1108 的結果冒充本輪查詢。

## 產出與給下一棒的提示

- 追蹤產出：本報告與 `docs/reports/issue-1109-evidence.json`。原始 build／restore log、採證腳本及 JSON 位於 `scratch/issue-1109/`，證據 JSON 保留 log SHA-256。
- 沒有修程式、改 `docs/REPORT.md` 或 dev lock，沒有刪除 worktree／junction，沒有重做備份。
- **本機主控／調度器**先釐清主專案 `environments/p24` 必要運行寫入是否為本張只讀限制的例外，再由持有有效 lease 的 codex-local 接續原定閘門。這不是需要 code 修正的結果，不應因此重做 #1107 孤兒處置或 #1108 補資料。
- nativeUi 預設值與 #475／T112 結案均未裁定，仍交主控。
