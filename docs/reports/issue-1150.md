# Issue #1150：UIA＋GDI 客戶端重測

**結論：部分通過（✅ 5／❌ 4／⚠️ 1）。** 計數為下表 G01–G07、F01、P01、R07 共 10 個主項，沿用項與 R07 子項不重複計數。登入、兩視窗 GDI／UIA 讀取通過；單鍵 F12、分頁 SelectionItemPattern、建角對話框 UIA 路徑失敗。沒有證據顯示七職業能力值規則有缺陷：本輪卡在可及性，七職業尚未分配能力值，也未真正送出／刪角。

2026-11-14 約 07:29–07:42（台北）完成建置、測試及程序收尾；後續整理報告。分支 `task/1150`，測試來源 `6f7051fb27f60b32b2cd3eca7b008d6e5fcf9e64`，開始時等於 fetch 後的 `origin/main`。未修改引擎或測試 helper 程式，只更新本報告、[精簡原始證據](issue-1150-evidence.json) 與 [操作 README](../../work/f1118-uia-path/README.md)。

## 逐項結果

下列證據若未另註，均相對 `D:\lineage開發\TW1608262200研究\scratch\wt\task-1150\scratch\issue-1150\`。JSON 樹、錯誤、量測、鍵盤輸入紀錄及 SHA-256 另收錄於 Git 的 `issue-1150-evidence.json`；PNG／完整 log 不加入 Git。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| G01 | ✅ 通過 | p24svc01 到 CharacterList，已解析 3/3；DB 同帳號 3 個角色、4 槽中餘 1，全部 GM0 | 本輪建置、指定 real-interactive／classic-ui 參數啟動，讀登入狀態及 UIA／GDI | `login-keyboard-tree.json`、`after-cancel-tree.json`、`after-cancel.png`、`characters-before.tsv` |
| G02 | ✅ 通過 | 主窗 PrintWindow 1206×871，非全黑白；目視可讀「等待真實世界初始化」 | `Save-UiaGdiScreenshot $main`，讀矩形／DPI／最小化，目視 PNG | `main-initial.png`、`main-initial-capture.json`、`g02-window-info.json` |
| G03 | ✅ 通過 | 主窗 UIA 樹可讀，包含 owned 開發工具；畫布本身沒有場景文字 UIA 節點 | 對唯一主窗執行 `Get-UiaTree` | `g03-main-tree.json` |
| G04 | ❌ 失敗 | `Send-UiaF12` 前景核對成功、未丟錯，但沒有 manual PNG；補充的 Ctrl+Shift+F12 產生 3 張新檔，不抵銷指定單鍵失敗 | CharacterList 主窗 → Send-UiaF12 → 查新檔；另試來源現有的 Ctrl+Shift+F12 | `g-tests.json`、`p01-corrected-shortcut.json`、`client/manual-*.png`、Git 證據 `guardedKeyboardInputs` |
| G05 | ✅ 通過 | 開發工具 PrintWindow 826×711，非全黑白；可讀分頁與按鈕 | 由主窗尋 owned Window → GDI 截圖並目視 | `dev-initial.png`、`dev-initial-capture.json`、`dev-info.json` |
| G06 | ✅ 通過 | 開發工具的當前分頁樹與 pattern 清單可讀；不代表所有分頁節點完整／正確 | `Get-UiaTree $dev` | `g06-dev-tree.json`、`login-keyboard-tree.json` |
| G07 | ❌ 失敗 | 初次 Select「登入／選角」實際切到「控制」；新樹找「服務」及「登入／選角」各 found 0，未完成指定來回切頁 | 精確名稱 Find-UiaControl → SelectionItemPattern.Select → 重讀樹；再找服務／登入分頁 | `window-2425582.json`、`login-tree.json`、`g-tests.json`、`dev-initial.png` |
| F01 | ❌ 失敗 | Invoke 確實開啟建角，但回報 0x80131505；GDI 截圖成功，對話框子樹在開啟期間不返回；未切職業。核對同 PID 前景後以 Escape 取消 | 先用 UIA 聚焦 Tab＋Left 回登入頁 → Invoke 建立角色 → 從觀察到的前景 HWND 截圖／讀樹 → Escape | `create-dialog.png`、`create-capture.json`、`dialog-errors.json`、`dialog-foreground.json`、`cancel-key.json`、`after-cancel.png` |
| P01 | ❌ 失敗 | README 已逐項回寫可用方法與錯誤；找窗／截圖／讀樹／鍵盤替代可用，但原分頁方法、單鍵 F12、對話框操作未全通過；職業／能力值未實證 | 依 README 的各方法在同一客戶端執行並保存後態 | `g-tests.json`、`f01-result.json`、`dialog-errors.json`、`p01-corrected-shortcut.json`；`work/f1118-uia-path/README.md` |
| #1108 R07 | ⚠️ 無法判定 | GUI 可建立 0/7、送出 0 次；被 F01 可及性阻塞，不能歸因於職業規則或原服拒絕 | 對話框開啟後讀取職業／能力值控制項時阻塞；不改用座標操作、不直接重放 C84 | `create-dialog.png`、`dialog-errors.json`、`characters-final.tsv` |

R07 指定操作逐項列明：

| 子項 | 結果 | 實際觀察／重現 | 證據 |
|---|---|---|---|
| 王族 | ⚠️ | 預設畫面可見 13／10／10／11／13／10、剩餘 8，名稱空白；沒有取得 Edit／ComboBox，未達「可以建立」 | `create-dialog.png`、`dialog-errors.json` |
| 騎士 | ⚠️ | 同一對話框 UIA 列舉阻塞，未切換／分配／截可建立圖 | `dialog-errors.json` |
| 妖精（GUI 字串為精靈） | ⚠️ | 同上，未執行；GUI 字串對應僅為來源靜態核對 | 同上；`engine/source/CharacterManagement.cs:10` |
| 法師 | ⚠️ | 同一 UIA 阻塞，未切換／分配／截可建立圖 | `dialog-errors.json` |
| 黑暗妖精（GUI 字串為黑暗精靈） | ⚠️ | 同上，未執行；GUI 字串對應僅為來源靜態核對 | 同上；`engine/source/CharacterManagement.cs:10` |
| 龍騎士 | ⚠️ | 同一 UIA 阻塞，未切換／分配／截可建立圖 | `dialog-errors.json` |
| 幻術師 | ⚠️ | 同一 UIA 阻塞，未切換／分配／截可建立圖 | `dialog-errors.json` |
| 真正送出／S98／S127／新 objid | ⚠️ | 沒有送出，不存在本輪 S98=2／新角色證據；不能以 #1108 協定建角替代本輪 GUI 送出 | `characters-before.tsv`、`characters-final.tsv` |
| GUI 刪除本輪角色 | ⚠️ 跳過 | 本輪未建立角色，所以沒有可授權刪除的目標；未碰任何既有角色 | 同上 |

## 已驗證的錯誤與邊界

### 找窗、分頁與快捷鍵

主窗 HWND 2425582，矩形 `(1317,604,1206,871)`；開發工具 HWND 788790，矩形 `(98,98,826,711)`；建角 HWND 854508，矩形 `(1687,709,466,661)`。三窗 PID 6408、DPI 192、未最小化，GDI 均為 PrintWindow，無需 WGC。

`Get-UiaWindow` 只列桌面 Children；開發工具在主窗 descendants 中，頂層找窗原文：

```text
Expected one window for PID 6408 title '開發工具（過渡操作介面） · Ctrl+Shift+F9'; found 0
Expected one control name='服務' id='' type='TabItem'; found 0
Expected one control name='登入／選角' id='' type='TabItem'; found 0
```

UIA 初始列出登入分頁，Select 後目視為控制頁；其後樹中 TabItem 名稱／矩形也與可見分頁不一致。額外 RawViewWalker 未補回缺失分頁。這是**已觀察的可及性問題**，尚未判定來源是 WinForms provider、UIAutomationClient 相容性或 helper 列舉方式。沒有修程式、沒有座標點擊。為繼續獨立的 F01，UIA 聚焦唯一 Tab 並確認 HasKeyboardFocus，再送 Left；此時登入控制項與 CharacterList 文字可讀。

單鍵 F12 於 `23:37:12.074Z` 送出後無新檔，直到下一個明確不同的快捷鍵才產生證據。Ctrl+Shift+F12 的 SendInput 每批返回數量等於要求數量；三檔 UTC 修改時間為 `23:37:59.035Z`（client）、`23:37:59.019Z`（logical）、`23:37:59.071Z`（screen），均晚於該次送鍵起點 `23:37:58.792Z`。三圖用途為主窗證據，不作建角成功證據。靜態對應：`Program.cs:131` 直接攔下 F1–F12，`DevToolsForm.cs:106` 則處理 Ctrl+Shift+F12；這支持快捷鍵契約已與 README 的單鍵範例不符。

### 建角對話框

初次在錯誤分頁找入口為 `Expected one control name='建立角色' id='' type='Button'; found 0`。回到正確分頁後 Invoke 產生真實對話框，但呼叫退出 1：

```text
Exception calling "Invoke" with "0" argument(s): "Operation timed out. (Exception from HRESULT: 0x80131505)"
```

未因逾時重送 Invoke。由同 PID 前景 HWND 確認標題「建立角色」，GDI 可用。對話框 `Get-UiaTree`／FindAll Descendants 與另一個 Children 讀取均未在開啟期間返回；Escape 於 `23:41:02.7689939Z` 送出並取消後，前者才退出 1：

```text
Get-UiaTree : The property 'ProgrammaticName' cannot be found on this object. Verify that the property exists.
```

Children 呼叫取消後只回一個 Pane，不能當成對話框開啟期間的有效職業／能力值樹。`dialog-errors.json` 保存工具回覆原文及這項限制，沒有偽造完整樹。由對話框圖讀到「名稱包含禁止字元或為空白。」；對應 `CharacterManagementPackets.Validate` 的空名稱檢查（`CharacterManagement.cs:20`），是尚未填名稱的正常初始狀態。**未驗證合法配置後是否通過，不列為職業程式缺陷。** Escape 是 `CharacterCreationDialog.CancelButton` 的正常取消路徑；取消後目視與 UIA 都回 CharacterList 3/3。

## 環境、備份與收尾

- 已讀 AGENTS.md、docs/REPORT.md、指定 README／前置結果及 #1108 的唯讀啟動資料。#1149 S02／S02-GDI／S02-CLOSE 均 ✅，未重跑記事本。開始時 `query session` 為 `>console CHAOREN 6 Active`（完整輸出存 `session.txt`）。lease holder 為 `chaoren0418-arch/tw-bin-engine#1150`，exp `1794618126.9398184`＝`2026-11-14T01:02:06.939Z`，與派工一致至秒，測試期間有效。
- `engine/build-product.ps1 -ConfigFile .github/nuget.ci.config` 成功。資源 junction 接好後增量再建成功；使用 temporary lock，沒有寫 rebuild-lock。product DLL SHA-256：`1642ea357d5300fcdac8f5b40e6595ba3504c17aa10b830d8fd6816041726e93`。DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false、NOLOGO=1；TEMP／TMP 在 worktree scratch。
- **依原專案只讀要求，本輪是目前持久資料的隔離副本測試。** 原 `environments/p24/db-data` 在兩埠空閒時，以禁止其他寫入的 ibdata1 handle 保護，完整冷備份至本 worktree `scratch/issue-1150/backup-before/db-data`，再複製成執行副本；1,722 檔、279,502,537 bytes，兩份逐檔 SHA-256 相同。manifest SHA-256：`87a96204075351c1fd9ff00b9cf09f5f5e566e8d6a7de793708256c6e3083bf5`。不是較舊 task-1098 或 #1108 baseline 副本。
- MySQL 明確讀回 `@@port=43324`、`@@datadir=D:\BinEngine\scratch\wt\task-1150\scratch\issue-1150\db-data\`；原服 JAR／config／jar／maps 沿用 P24Lab，執行目錄與 log 在本 worktree。第一次 mysqld 以中文 datadir 參數啟動，原文 `Failed to set datadir to 'D:\lineage\' (OS errno: 2 - No such file or directory)`，立即自行退出；改用既有 `D:\BinEngine` junction 的 ASCII 別名成功，未改 ini／機密設定。初次失敗 log 保留為 `mysql-first.stderr.log`。
- 沒有人工 DB DML、補物品、GM 變更、血盟／寵物準備或 GUI 建角送出。前後角色 SELECT 逐字相同，17 角色全 AccessLevel=0／OnlineStatus=0，p24svc01 仍為 3/4；不表示伺服器啟動／登入對隔離副本的所有表都沒有自動寫入。**無人工資料修改可提供反向 SQL，也未執行還原 SQL**；如需回到副本啟動前的完整狀態，使用上述冷備份，不能對有後續寫入的持久環境重放 SQL。本輪不需要還原原持久 DB，收尾重新核對其全部 1,722 檔雜湊均未變。
- 自啟客戶端 6408 以 WM_CLOSE 正常退出，WebView 3176 已結束；Java 17468 按自啟 PID／名稱／父 PID 記錄核對後停止；mysqld 11200／子 PID 9232 由指向 127.0.0.1:43324 的 mysqladmin shutdown 正常結束。`cleanup.json` 最後 RemainingOwned=[]、Listeners=[]，42061／43324 無監聽。沒有啟動記事本、碰正式 2000／3306、原 BIN、系統設定或既有遊戲程序；未移除 worktree、拆 junction 或遞迴刪目錄。

## 沿用（不列入本輪計數）

| 編號 | 原結果與處理 |
|---|---|
| #1149 S02／S02-GDI／S02-CLOSE | ✅ 沿用記事本輸入讀回、GDI、不要儲存後正常關閉。 |
| #1118 S01 | ✅ 沿用互動 session；本輪只補 query session 前置。 |
| #1126 D01 | ✅ 沿用唯讀診斷：WGC `FrameArrived timed out`、GTX1080／32.0.15.6094、3840×2160／DPI192、session6；未重試 WGC。 |
| #1126 C01 | ✅ 沿用該輪收尾結果；本輪另正常收尾，不重測歷史程序。 |
| #1108 B01、R05、R08、R09、R10、R11、R12、R13、R14、R15、C01、C02、V01 | ✅ 全數沿用；不重放資料準備、物品、血盟或寵物操作。 |
| #1108 R07 的七職業協定建角部分 | ✅ 沿用原服 C84／S98=2／S127／DB 7/7；GUI 整體 R07 仍未完成。 |

## 做了什麼／未驗證／給下一棒的提示

已驗證建置、登入、GDI／當前分頁 UIA、替代快捷鍵、新舊資料邊界與自啟程序收尾。報告／JSON／README 以 JSON 解析、表格計數、來源 SHA-256 與 `git diff --check` 核對；沒有修改 `docs/REPORT.md` 或 lock。推測只有 UIA provider／相容性可能造成分頁錯位與 modal 列舉阻塞，尚未證明確切根因。

1. **交調度器／本機工具維護者分流 G04、G07、F01、P01；R07 等可及性可用再補。** G04 已有 Ctrl+Shift+F12 可用證據，可先修正測試契約／README 呼叫約定；不要要求再修 WGC。G07／F01 須處理 TabItem 名稱／選取不一致、modal Invoke 逾時與列舉阻塞，不能把一般 UIA 讀樹成功當全部操作成功。
2. 之後只重測上述 ❌／⚠️：七職業各到「可以建立」、一次 GUI 真正送出、讀回及刪除本輪新角色仍未完成；不能稱 R07 通過。G01–G03、G05–G06 不需原樣重跑。SM08／SM09／SM48 未執行。
3. PNG、完整 log、一次性腳本與完整冷備份在此 worktree 的 scratch；本輪 Git 證據保存小型原始值與雜湊。**調度器清理前先保存所需 PNG／備份**，不可遞迴刪除含 junction 的目錄。這次隔離副本結果不宣稱持久 DB 接受／刪除了新角色。
