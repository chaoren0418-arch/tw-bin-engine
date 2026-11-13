# Issue #1118：GUI 擷取環境檢查

**結論：部分通過（✅ 2／❌ 1／⚠️ 8）。GUI 驗收仍阻塞，交 `needs:local`；驗收 (a)、(b) 均未成立。**

2026-11-14 03:57–03:59（台北），分支 `task/1118`，來源 HEAD `50679105c5e9643fccc490e1728412730867b8aa`。依任務第 2 步，無關系統視窗擷取在重設一次後仍失敗，因此停止後續 GUI 測試。沒有啟動客戶端、Java 或 MySQL，沒有登入、建角、查寫 DB、改程式或系統設定。

證據摘要隨 Git 保留於 [issue-1118-evidence.json](issue-1118-evidence.json)。以下 `scratch/issue-1118/` 路徑皆位於本 worktree，不在原始專案根目錄寫入。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| S01 互動桌面 | ✅ | 執行器在 console session 6／Active；輸入桌面為 Default，未見已中斷 RDP，未見 LockApp／LogonUI | `query session`、執行器 SessionId、唯讀 `OpenInputDesktop`／`GetUserObjectInformationW` | `scratch/issue-1118/session.json`；Git 證據 `session` |
| S02 系統視窗擷取與 Recovery | ❌ | 新開記事本兩次擷取皆 `FrameArrived timed out`；重設 JS、重新初始化及選窗後仍失敗 | `sky.list_apps` → `launch_app` → `list_windows` → `get_window` → `activate_window` → `get_window_state`；重設一次後重新選窗並重試 | Git 證據 `captureAttempts`、`notepad`；`scratch/issue-1118/notepad-first.json` |
| G01 登入到 CharacterList | ⚠️ | 未啟動，S02 觸發任務明定停止條件 | 系統視窗擷取恢復後，才使用 #1108 相同參數登入 p24svc01 | 本報告；Git 證據 `cleanup` |
| G02 主視窗截圖 | ⚠️ | 未執行；最小化、遮蔽、DPI 均未量測 | 到選角後對主視窗 `get_window_state` | 本報告 S02 |
| G03 主視窗 include_text | ⚠️ | 未執行 | 對主視窗讀 `include_text: true` | 本報告 S02 |
| G04 主視窗座標點擊 | ⚠️ | 未執行，沒有可用截圖幾何 | 用該次截圖選擇不改資料位置點擊並刷新 | 本報告 S02 |
| G05 開發工具視窗截圖 | ⚠️ | 未執行；最小化、遮蔽、DPI 均未量測 | 對「開發工具（過渡操作介面）」擷取 | 本報告 S02 |
| G06 開發工具 include_text | ⚠️ | 未執行 | 對開發工具讀 `include_text: true` | 本報告 S02 |
| G07 開發工具座標點擊 | ⚠️ | 未執行 | 以最新截圖點「登入／選角」等不改資料位置 | 本報告 S02 |
| F01 無座標替代路徑 | ⚠️ | 未驗證 `element_index`／`set_value`／`perform_secondary_action` 配合內建截圖可完成建角對話框；沒有宣稱可用 | 系統工具前置通過後，開啟建角對話框、切換職業、關閉，不送出 | 本報告 S02 |
| C01 收尾 | ✅ | 自啟記事本 PID 14492 已結束；沒有自啟客戶端／Java／MySQL；42061、43324 皆無監聽 | 核對記事本 PID、程序名及啟動時間後結束；查指定兩埠 LISTEN | `scratch/issue-1118/cleanup.json`；Git 證據 `cleanup` |

## 已驗證與原因界線

- 使用 `computer-use` 技能的 `@oai/sky`，與問題描述相同的 `get_window_state` 擷取路徑。兩次錯誤原文均為：`Error: FrameArrived timed out: timed out waiting on channel`。
- 對照視窗是 Windows `System32/notepad.exe`「未命名 - 記事本」，window ID 264538、PID 14492。第一次失敗後的唯讀量測：可見、未最小化，前景 HWND 同為 264538；矩形 `(891,56)–(1797,856)`，DPI 192（200%）。不是在背景或最小化狀態下測得的失敗；未做逐像素遮蔽分析。
- Recovery：`js_reset` 一次 → 重新 import `@oai/sky` → `list_windows` 重新唯一選取記事本 → `get_window` → `activate_window` → 重試擷取一次。兩次均在數秒內回報上述錯誤，沒有繼續重試或使用舊座標。
- **已證明擷取故障不限於 P11.ClientEngine.exe；尚未證明底層確切根因。** 本輪不能把 DPI、顯示驅動、擷取 helper 或桌面合成器判作原因。沒有成功截圖可交付，也沒有重現座標輸入錯誤；該錯誤仍僅有 #1108 證據。
- 03:58 的調度器 lease 為 `p24lab`、holder `chaoren0418-arch/tw-bin-engine#1118`，到期 `2026-11-13T21:27:14.704196Z`，與派工值一致（API 另含小數秒），當時有效；未更改 lease 或調度器。

## 做了什麼／怎麼驗證／未驗證什麼

讀取根目錄規則、`docs/REPORT.md`、#1116 分流，以及 task-1108 的報告與 `start_client.py`（只讀）；只執行互動桌面診斷、記事本擷取／一次 Recovery、程序與監聽收尾。原始工具錯誤、系統狀態與量測值收入 Git 證據；`git diff --check`、JSON 解析與結果計數核對通過。這是報告變更，未改引擎、dev lock 或 `docs/REPORT.md`，未重建引擎。

客戶端兩視窗測試、無座標替代方法、內建 `manual-screen`／`manual-client` 截圖，以及建角對話框開啟／切職業／關閉都尚未驗證。未重跑 #1108 其他已通過項目。

## 給下一棒的提示

1. **由本機對話（`needs:local`）處理 Windows 擷取工具環境。** 這次 session 可互動、桌面未鎖定；不應直接安排解鎖作為已確認修法。提供上述兩次完整錯誤及 200% DPI 的觀察，檢查工具宿主／擷取後端；本張未更動設定、登錄檔、權限、排程或服務。
2. 接手門檻：先以同一 `@oai/sky` 成功擷取普通記事本，再重測 G01–G07、F01。S01／C01 已通過，後續僅按新執行時段做必要前置與收尾；不要原樣重跑 #1108 全清單，也不要在此門檻通過前派 R07 建角重測空轉。
3. #1108 啟動參數為 `--assets D:\BinEngine\engine\assets --login-port 42061 --login-demo real-interactive --evidence <本次 scratch 路徑> --classic-ui`，`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`。測試憑證由原腳本讀取既有私有檔，不輸出到報告。本輪沒有執行該腳本。
4. 若普通視窗恢復但遊戲仍失敗，才依原任務第 4 步驗證無座標路徑：每次先刷新 UIA，依當次 `element_index` 開啟建角、操作職業控制項（只使用樹中列出的次要 action）、關閉；搭配新的客戶端內建截圖證明狀態。這是**待驗證步驟，不是已證實可行清單**；禁止送出或寫 DB。

產出：本報告、`docs/reports/issue-1118-evidence.json`；本機原始證據與診斷腳本在 `scratch/issue-1118/`。未刪除任何目錄或 junction。
