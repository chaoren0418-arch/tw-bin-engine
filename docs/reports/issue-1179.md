# Issue #1179：G06／N01 指定重測與 G05／G07 回歸

**結論：部分通過（✅ 3／❌ 1／⚠️ 0）。** 計數為 G05、G06、G07、N01 四項；G01 前置成功另列，沿用項目不計入。G06 三次讀樹皆無 Error。N01 在進入世界時遇到協定錯誤，未達 InWorld，不能驗證世界中的服務分頁。

2026-11-14 11:05–11:12（台北）完成 main 建置、隔離環境、實機與收尾。來源 main `7d55ee6c07d6ed24371da3884717ab1447a33b00`，包含 #1178；提交前 fetch 仍相同。只新增報告與證據，沒有修改引擎、helper、lock、README 或 docs/REPORT.md。

## 已驗證：逐項結果

JSON／log 名稱相對本 worktree 的 `scratch/issue-1179/`；原始 UIA 樹、狀態、建置 log、SELECT 與收尾已收進 [Git 證據](issue-1179-evidence.json)，五張 GDI 圖收進 [圖片目錄](issue-1179-images/)。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| G01（前置） | ✅ 通過 | p24svc01 到 CharacterList，3/3 角色、4 欄位，三角色 Gm=0 | 指定啟動參數與既有測試帳密環境變數；讀 login-state | `g01-login-state.json`、`g07-login-tree.json`、[登入圖](issue-1179-images/g07-login.png) |
| G05 | ✅ 通過 | PrintWindow 826×711、DPI192、非空白；目視網頁面板可辨識 | IncludeOwned 找開發工具 → Save-UiaGdiScreenshot → 目視 | `dev-info.json`、`g05-capture.json`、[GDI 圖](issue-1179-images/g05-dev.png) |
| G06 | ✅ 通過 | 連續三次各 183 節點，Error 各為 0；原壞位置現為 Text／尚未進入世界 | CharacterList 預設網頁面板 → Get-UiaTree 連讀三次 | `g06-tree-1/2/3.json`、Git 證據的 `g06-result`、[同頁圖](issue-1179-images/g05-dev.png) |
| G07 | ✅ 通過 | 服務／登入雙向回傳同名、IsSelected=true、唯一 TabSelection；圖一致 | Find-UiaControl -Type Tab -Scope Children → Select-UiaTab 服務 → 重新找外層 → 登入／選角 | `g07-result.json`、`g07-*-tree.json`、[服務圖](issue-1179-images/g07-service.png)、[登入圖](issue-1179-images/g07-login.png) |
| N01 | ❌ 失敗 | CharacterList 服務 Tab Enabled=false 符合對照預期；既有資源法師進世界後 Faulted：unsupported status snapshot size，未達 InWorld | 讀停用狀態 → 登入頁讀回既有角色資源法師 → UIA Invoke「進入世界」一次 → 讀 login-state／樹／GDI | `n01-disabled.json`、`n01-enter.json`、`n01-world-login-state.json`、`n01-fault-tree.json`、[錯誤圖](issue-1179-images/n01-fault.png)、[主窗圖](issue-1179-images/n01-main.png) |

G07 兩次 RequestedName＝TabItem.Name＝TabSelection 唯一元素，IsSelected=true，兩種 Error 欄位均 null。登入頁按鈕文字有既有裁切，UIA 可讀其精確名稱；不影響本張外層分頁判定。

### G06 三次 sceneStatus 對應節點

| 次數 | 節點數 | Error | Type | Name | UIA Id |
|---|---|---|---|---|---|
| 1 | 183 | 0 | ControlType.Text | 尚未進入世界 | 空字串 |
| 2 | 183 | 0 | ControlType.Text | 尚未進入世界 | 空字串 |
| 3 | 183 | 0 | ControlType.Text | 尚未進入世界 | 空字串 |

三次均是零起算第 54 節點，矩形 `161,1789,84,19`，位於 Image／sceneImage 後、StatusBar／incomingAttack 前。對照 `engine/web/index.html:16`，可定位為 sceneStatus 的文字內容；Chromium 沒有在此文字節點輸出 DOM Id，**不宣稱 UIA Id=sceneStatus**。該節點 Offscreen=true；GDI 是同一頁上方可見區域，沒有捲動或重測。一次性摘要最初按 Id 篩選為空，交付摘要改由已存的三棵樹擷取鄰接節點，未再讀 UI 或修改 helper。

### N01 失敗界線

「開發工具服務分頁」在 CharacterList 的 Type=ControlType.Tab、Enabled=false、矩形 `119,200,784,588`，是預期對照，不是失敗原因。未對停用子頁呼叫 Select。

登入頁既有 ComboBox 名稱與 ValuePattern 讀回皆為「資源法師」（p24svc01、objid=10334、GM0），以唯一「進入世界」Button 的 InvokePattern 送出一次。之後 login-state：

```text
state: Faulted
detail: ProtocolException: unsupported status snapshot size
world.ready: false
world.coreComplete: false
world.entered: false
```

UIA Text 的 Name 也完整讀到同一錯誤，GDI 可見錯誤前段（畫面寬度裁切）；主窗仍為「等待真實世界初始化」。未取得 InWorld，故未列世界中的全部 TabItem、未驗證其 Enabled=true、未切聲音／血盟；這些子條件仍待驗證，不能把 CharacterList 的標籤集合當世界驗收。整項 N01 以進世界失敗列 ❌，沒有宣稱 Selection API 壞掉，也沒有換角色重試。

一次性 enter-world 腳本最初沒有 UTF-8 BOM，Windows PowerShell 5.1 誤讀中文標題，找窗即失敗、未 Invoke；補本張 scratch 腳本 BOM 後才完成上述唯一一次 Invoke。未改專案 helper 或產品程式。

## 已驗證：環境、備份與收尾

- 開始 `query session`：console／CHAOREN／6／Active。lease holder=`chaoren0418-arch/tw-bin-engine#1179`、exp=`1794630852.2951803`（2026-11-14T04:34:12Z），全程在期限內。
- 初次 43324 被非本張 MySQL PID18856 佔用，prepare 在複製前停止；後續該 PID 與監聽已消失，未由本張關閉。兩埠空閒後才完成冷備份。此暫時狀況已解除，非最終阻塞；原持久資料不變的核對基線為下列冷備份時點。
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File engine/build-product.ps1 -ConfigFile .github/nuget.ci.config`：product／launcher restore、build 全 0、PASS dev。temporary lock、不寫 committed lock。DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false、DOTNET_NOLOGO=1，TEMP／TMP 都在本張 scratch；執行政策只作用於新行程，未改系統設定。
- product DLL SHA-256：`998cc2d0983635a888bfde4f04f83408f2410ee84852acee10d6bf385d82ca95`。panel.js SHA：`2e9eecb027c5b4725023d81cffeca9c9264fefdfd5dec3b1f75a3f11fcf7eff4`；helper SHA：`64feb3eb1ca9922b70afce2819d2bb72497e90ab005f7ad7b3c208a8650afc89`。
- 冷備份 03:08:35Z：原 `environments/p24/db-data` 以拒絕其他寫入的 ibdata1 handle 保護，完整複製至 `backup-before/db-data`，再建立執行副本；1,722 檔、279,504,637 bytes，三份逐檔 SHA 相同。manifest SHA=`15cf9077329b82c4d1b57ea73c84455696cf45e145786d447ea966ce899db5f2`。D:/BinEngine 是既有專案別名，沒有建立或改動該別名。
- SQL 確認 port=43324、datadir=`D:/BinEngine/scratch/wt/task-1179/scratch/issue-1179/db-data/`。Java 工作目錄／home／tmp／log 在本張 scratch；原 P24Lab config／jar／maps 為唯讀使用的 junction。未改 server.properties，未複製 pak／map／sprite。
- 客戶端參數：`--assets <worktree>/engine/assets --login-port 42061 --login-demo real-interactive --classic-ui --evidence <worktree>/scratch/issue-1179/client`，TW_CLIENT_ROOT=`D:/lineage開發/0-客戶端`。測試帳密僅從既有 private 檔讀至環境變數，未輸出或提交。
- 客戶端 PID19920 CloseMainWindow=true、Exited=true；Java4080 核對執行檔後停止；MySQL 父20388／子4452 使用指定 43324 的 mysqladmin shutdown。最終 RemainingOwned=[]、Listeners=[]，兩個測試埠無監聽。
- 最後完整 SELECT 17 角色全部 AccessLevel=0、OnlineStatus=0，指定六欄前後逐字相同。03:12:08Z 原持久 DB 1,722 檔 SHA 相對冷備份基線不變、無新增檔。沒有手動 DML、建角、刪角、補物品、改 GM；遊戲自動寫入只落隔離副本。
- 未連正式 2000／3306，未修改原 BIN、正式客戶端、封存、登錄檔、系統設定或服務；沒有拆 junction、遞迴刪除或移除 worktree。僅操作 UIA helper 與 GDI，未使用 WGC、@oai/sky、座標點擊或鍵盤切頁。

還原 SQL：無需人工 DML，原持久資料未改。若要還原隔離副本的自動寫入，在程序全停後由 `backup-before/db-data` 建立新副本並核 SHA，不覆寫持久 DB。唯讀核對 SQL（僅本張 43324）：

```sql
SELECT @@port, @@datadir;
SELECT account_name,char_name,objid,Type,AccessLevel,OnlineStatus
FROM p24_accounts.characters ORDER BY objid;
```

## 沿用，不重測、不計入本輪四項

| 編號 | 結果 | 實際觀察／重現步驟／證據 |
|---|---|---|
| #1173 P01 | ✅ 沿用 | 沿用 README 切分頁用法；本輪 G07 通過，不改判；[原結果與步驟](https://github.com/chaoren0418-arch/tw-bin-engine/blob/cf5470ff/docs/reports/issue-1173.md) |
| #1165 G04 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1165 F01 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R07 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1150 G02 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1150 G03 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1149 S02 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1149 S02-GDI | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1149 S02-CLOSE | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1118 S01 | ✅ 沿用 | 沿用，本輪開始只記 query session；[原結果與步驟](issue-1165.md) |
| #1126 D01 | ✅ 沿用 | 沿用既有診斷：WGC 不可用，沒有重試；[原結果與步驟](issue-1165.md) |
| #1126 C01 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 B01 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R05 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R08 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R09 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R10 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R11 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R12 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R13 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R14 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 R15 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 C01 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 C02 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |
| #1108 V01 | ✅ 沿用 | 本輪不操作，沿用指定基準；[原結果與步驟](issue-1165.md) |

## 推測、待驗證與給下一棒的提示

- **交調度器／Claude 分流 N01 的進世界協定錯誤，修正後只重測 N01**。`engine/source/MagicStatus.cs:52` 有相同錯誤字串；這只定位到可能的解析檢查，尚未查明實際封包欄位或伺服器相容性原因，不能宣稱已找到根因。G05／G06／G07 本輪已通過，P01 與其餘指定項目沿用。
- 世界中的實際 TabItem 數量、名稱／矩形、Enabled=true、聲音／血盟選取與 GDI 一致性均待驗證。沒有測服務子頁功能、SM08／SM09／SM48、移動、戰鬥、交易或對話。
- 本張沒有修程式。產出為本報告、`issue-1179-evidence.json` 與五張 `issue-1179-images/*.png`；完整 log、一次性腳本、冷備份與副本留在本張 `scratch/issue-1179/`。後續保全所需備份，不對含 junction 的 worktree 遞迴刪除。
