# Issue #1165：#1150 指定項目重測

**結論：部分通過（✅ 5／❌ 2／⚠️ 0）。** 七職業 GUI 建角 R07 完成；G07／P01 仍失敗，原因是 helper 的 `GetCurrentSelection` 呼叫不存在。沒有修改引擎或 helper 程式。

2026-11-14 09:36–09:48（台北）完成準備、實機與收尾，之後整理文件。來源 `1f756d0966a35874f2cbdc93e0e77bf4f81c637f`（fetch 後 HEAD 與 origin/main 相同），分支 `task/1165`。所有本輪寫入均在 `D:\lineage開發\TW1608262200研究\scratch\wt\task-1165`。G01 是前置沿用，不計入七項重測；其餘歷史項目不重測。

## 逐項結果（已驗證）

證據路徑皆相對 `scratch/issue-1165/`。精簡原始 UIA 樹、SQL 讀值、封包 metadata、錯誤與圖片 SHA-256 見 [issue-1165-evidence.json](issue-1165-evidence.json)。PNG／完整 log／冷備份留在此 worktree，未放進 git。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| G01 | ✅ 沿用／前置成功 | p24svc01 到 CharacterList，3 角色／4 欄位，全 GM0；畫面「3/3」是已解析／預期角色數 | 以 real-interactive 與指定環境變數啟動 | `login-current-tree.json`、`client/login-state.json`、`characters-before.tsv` |
| G04 | ✅ 通過 | Ctrl+Shift+F12 產生 client／logical／screen 三圖，時間全部晚於送鍵 | 主窗 Send-UiaEvidenceKey → Get-UiaNewEvidence → 目視 | `client-input.jsonl`、`g04-files.json`、`client/manual-*.png` |
| G05 | ✅ 通過 | 開發工具 PrintWindow 826×711，非全黑白，分頁與 CharacterList 可讀 | IncludeOwned 找窗 → Save-UiaGdiScreenshot → 目視 | `dev-info.json`、`g05-dev.png`、`g05-capture.json` |
| G06 | ✅ 通過 | 當前分頁樹可讀，控制項與 pattern 清單無 Error；不宣稱巢狀分頁名稱正確 | Get-UiaTree 開發工具 | `g06-dev-tree.json` |
| G07 | ❌ 失敗 | 服務與登入頁實際都正確切換，外層六個 TabItem 完整；但 helper 的雙向驗證拋 API 方法不存在 | Select-UiaTab「服務」→ 存樹／圖 →「登入／選角」→ 存樹／圖 | `g07-service-tree.json`、`g07-service.png`、`g07-login-tree.json`、`g07-login.png`、`g07-login-error.txt` |
| F01 | ✅ 通過 | Invoke 約 17.20 ms 返回，無 0x80131505；modal 開啟期間讀到職業／性別 ComboBox、六項 Spinner／Edit、本機檢查；切騎士後 Invoke 取消 | 建立角色 → 讀樹／GDI → Expand 職業 → Select 騎士 → 取消 | `f01-invoke.json`、`f01-dialog-tree.json`、`f01-dialog.png`、`f01-knight.png`、`f01-result.json` |
| P01 | ❌ 失敗 | README 七種用法逐項實證並回寫；分頁 helper 失敗，其餘可用 | 找窗、切頁、Invoke、職業、配點、GDI、證據鍵 | 本表各列；`work/f1118-uia-path/README.md`（repo 相對路徑） |
| #1108 R07 | ✅ 通過 | 七職業 7/7 可建立且取消；GUI 真正建立一次，S98／S127、清單、DB 讀回一致，再 GUI 立即刪除 | 下表七組配點 → 另開騎士送出 → 選取新角色 → 刪除／是 | `r07-*.png/json`、`packets.jsonl`、`characters-after-submit.tsv`、`characters-final.tsv` |

### R07 七職業與一次送出

每組名稱均為 `Aurora1165`，男性；順序是力量／敏捷／體質／精神／魅力／智力。每個職業各開一次對話框、設定、讀回、截圖、取消，均未送出。

| 職業 | 結果 | 配點 | 實際觀察／取消 | 證據 |
|---|---|---|---|---|
| 0／王族 | ✅ | 18／10／13／11／13／10 | 剩餘 0、可以建立、IsEnabled=true；Invoke 取消 | `r07-0.png`、`r07-0.json`、`r07-0-cancel.json` |
| 1／騎士 | ✅ | 18／12／16／9／12／8 | 剩餘 0、可以建立、IsEnabled=true；Invoke 取消 | `r07-1.png`、`r07-1.json`、`r07-1-cancel.json` |
| 2／精靈 | ✅ | 18／12／12／12／9／12 | 剩餘 0、可以建立、IsEnabled=true；Invoke 取消 | `r07-2.png`、`r07-2.json`、`r07-2-cancel.json` |
| 3／法師 | ✅ | 8／7／16／18／8／18 | 剩餘 0、可以建立、IsEnabled=true；Invoke 取消 | `r07-3.png`、`r07-3.json`、`r07-3-cancel.json` |
| 4／黑暗精靈 | ✅ | 18／15／12／10／9／11 | 剩餘 0、可以建立、IsEnabled=true；Invoke 取消 | `r07-4.png`、`r07-4.json`、`r07-4-cancel.json` |
| 5／龍騎士 | ✅ | 18／11／15／12／8／11 | 剩餘 0、可以建立、IsEnabled=true；Invoke 取消 | `r07-5.png`、`r07-5.json`、`r07-5-cancel.json` |
| 6／幻術師 | ✅ | 11／10／16／12／8／18 | 剩餘 0、可以建立、IsEnabled=true；Invoke 取消 | `r07-6.png`、`r07-6.json`、`r07-6-cancel.json` |

| 子項 | 結果 | 實際觀察／重現 | 證據 |
|---|---|---|---|
| GUI 真正送出 | ✅ | 另開騎士，18／12／16／9／12／8，名稱 Aurora1165（送出前 SELECT 不存在），只 Invoke 一次建立 | `r07-submit-ready.png`、`r07-submit-invoke.json`、`characters-pre-submit.tsv` |
| 原服接受／S98／S127 | ✅ | 01:46:09.184Z C84 len24；.245Z S98 len8 seq7；.254Z S127 len40 seq8；GUI 顯示「角色已建立」 | `packets.jsonl`、`r07-after-submit-tree.json`、`r07-after-submit.png` |
| 清單／DB 讀回 | ✅ | 四個 UIA ListItem 含 Aurora1165；DB 新列 objid=10293、Type=1、AccessLevel=0、OnlineStatus=0 | `r07-character-list-tree.json`、`login-after-submit.json`、`characters-after-submit.tsv` |
| GUI 刪除本輪角色 | ✅ | Select Aurora1165 → 刪除角色 Invoke 約 248.43 ms → 確認文字只含此名稱 → Invoke「是(Y)」；C162／S6／C16／S178／三筆 S93，DB 立即無此列，非七日排程 | `r07-delete-confirm-tree.json`、`r07-delete-confirm.png`、`r07-delete-confirmed.json`、`r07-after-delete.png`、`login-after-delete.json`、`characters-after-delete.tsv` |

七張圖片已逐張目視「剩餘點數：0」「資料符合本機檢查，可以建立。」並以 UIA IsEnabled=true 交叉核對。`本機檢查` 的 UIA Name 是 AccessibleName，不含動態 notice 內容，因此以真實 GDI 圖讀文案，沒有把固定 Name 當成功訊息。PrintWindow 沒有畫出 ComboBox 獨立 popup；四筆選角清單以 UIA ListItem／login-state／DB 交叉證明，不以 popup 圖假稱四筆可見。

### G04 時間證據

`Send-UiaEvidenceKey` 只送一次。JSONL 的 Ctrl down 為 `01:41:14.9186420Z`，最後 Ctrl up 為 `01:41:14.9735258Z`；五筆呼叫共六個鍵盤事件，每次前景 PID=目標 PID=19632，SendInput 返回數等於要求數。

- logical：`01:41:14.9785095Z`
- client：`01:41:14.9954388Z`
- screen：`01:41:15.0269341Z`

初次腳本在後面的 G07 因找到兩個 Tab 中斷，未把函式回傳的 sentUtc 落盤。沒有重送已通過快捷鍵；改以已保存的最後一筆送鍵 UTC 呼叫 Get-UiaNewEvidence，仍找到三張。三圖時間晚於整個送鍵序列，是比「晚於送出前時間」更嚴格的證據；原始回傳時間未保留的限制照實列明。

### G07 原文、全部 TabItem 與界線

錯誤原文：

```text
Method invocation failed because [System.Windows.Automation.SelectionPattern] does not contain a method named 'GetCurrentSelection'.
```

`g07-login-error.txt` 同時保存 RequestedName=登入／選角、TabItem.Name=登入／選角、IsSelected=true、TabSelection=null 與完整例外。服務頁與登入頁各有樹／GDI，實際內容正確；登入頁讀到建立角色按鈕。本機反射證明 SelectionPattern 只有 Current／Cached，GetSelection 在 SelectionPatternInformation 上，見 `selection-api.json`。這是 **helper 錯誤已驗證**，不是再次觀察到外層頁面錯一格；本張不修程式。

外層六個 TabItem（兩頁一致；矩形為實體像素 x,y,w,h）：

| 名稱 | 矩形 |
|---|---|
| 網頁面板 | `409,454,108,36` |
| 服務 | `517,454,78,36` |
| 登入／選角 | `595,454,132,36` |
| 控制 | `727,454,78,36` |
| 狀態列 | `805,454,84,36` |
| Button | `889,454,90,36` |

服務頁樹另外列出的全部巢狀 TabItem（原樣保留，不把它們當外層選取目標）：

| 名稱 | 矩形 |
|---|---|
| 背包 | `495,534,78,36` |
| 地面 | `573,534,78,36` |
| 魔法 | `651,534,78,36` |
| 角色 | `729,534,84,36` |
| 能力點 | `813,534,108,36` |
| 個人商店 | `921,534,78,36` |
| NPC | `999,534,78,36` |
| 交易 | `1077,534,78,36` |
| 聲音 | `417,498,132,36` |
| 好友／封鎖 | `549,498,78,36` |
| 決鬥 | `627,498,78,36` |
| 求婚 | `705,498,78,36` |
| 寵物 | `783,498,108,36` |
| 血盟戰爭 | `891,498,132,36` |
| 稱號／表情 | `1023,498,78,36` |

服務頁可見外層與「開發工具服務分頁」兩個 Tab，因此對 dev 使用 `Find-UiaControl -Type Tab` 會得到 `found 2`；後續限定 dev 的唯一直接 Children Tab 才送回登入頁。這是測試腳本定位修正，沒有修 helper 或用座標。巢狀標籤仍有名稱／位置偏移、可見「血盟」未列於樹的現象；本張未切換任何服務子頁，不宣稱其通過，留給另行分流評估。

## 環境、建置、備份與收尾（已驗證）

- `query session`：console／CHAOREN／6／Active；不重測記事本／WGC。lease holder=`chaoren0418-arch/tw-bin-engine#1165`，exp=`1794625661.4167619`（2026-11-14T03:07:41Z，毫秒略），實機全程在期限內。
- `engine/build-product.ps1 -ConfigFile .github/nuget.ci.config` 成功，product DLL SHA-256=`8756a951c9386349678b51c6d7e52313a86e3f21eab7f58dbf68761dd156647f`。DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false、DOTNET_NOLOGO=1，TEMP／TMP 均在本張 scratch。首次直接呼叫被 shell execution policy 拒絕，改新 PowerShell 的 process-only Bypass；未改系統設定。建置使用 temporary lock，未修改 rebuild-lock。
- 原持久 `environments/p24/db-data` 在兩埠空閒時以禁止其他寫入的 ibdata1 handle 保護，完整冷備份後再複製成執行副本；1,722 檔、279,502,566 bytes，備份與副本逐檔 SHA 相符。manifest SHA-256=`da7743d458bd0536766124694d034821da2e0d90f2033550fc78a2ca37c036db`，見 `backup-before/manifest.json`、`snapshot.json`。未使用舊輪次 DB。
- DB 讀回 port=43324，datadir=`D:\BinEngine\scratch\wt\task-1165\scratch\issue-1165\db-data\`；Java 執行目錄、log、home／tmp 在本張 scratch，config／jar／maps 為原 P24Lab 唯讀引用，未修改機密設定。素材只用 engine/assets junction 與 TW_CLIENT_ROOT 指定的原客戶端；未複製 pak／map／sprite。
- 客戶端參數：`--assets <worktree>\engine\assets --login-port 42061 --login-demo real-interactive --classic-ui --evidence <本張 scratch/client>`，TW_CLIENT_ROOT=`D:\lineage開發\0-客戶端`。首輪 PID19632 正常 CloseMainWindow 後退出；為尚未送出的 R07 啟用既有 TW_PACKET_TRACE，再啟 PID4468／client-submit，沒有重跑已通過項目。追蹤只存 metadata，不含密碼／封包 payload。
- 無人工 SQL DML、GM 設定、補物品、血盟／寵物操作；只有 GUI 建立／刪除本輪角色。最後 17 角色全 AccessLevel=0、OnlineStatus=0，SELECT 與準備前逐字一致。這只證明所列欄位一致，不宣稱隔離副本其他表完全未變。
- 客戶端兩個 PID 均正常關閉，WebView 子程序結束；Java18420 核對自啟紀錄後按 PID 停止；mysqld16352／18328 以指定 43324 的 mysqladmin shutdown 正常關閉。最終 RemainingOwned=[]、RelatedProcesses=[]、Listeners=[]，42061／43324 無監聽。初次收尾查詢因 PowerShell 巢狀陣列產生無效 WMI filter，展平 PID 後完整重查，最終證據是 `cleanup.json`。
- 原持久 DB 收尾逐檔重算：1,722 檔全部不變、沒有新增檔，見 `original-db-final.json`。未碰正式 2000／3306、原 BIN、封存、系統設定、既有遊戲；未拆 junction、刪目錄或移除 worktree。

### 前後對照與還原

原持久 DB 不需還原。本輪新角色已由 GUI 刪除，**沒有待執行的角色還原 DML**；不得對持久 DB 重放 DELETE 或其他 SQL。若要把本輪隔離副本的所有自動寫入也回復，應在自啟程序全停後，從 `backup-before/db-data` 建立另一份新隔離副本並逐檔核對；不要覆寫原持久資料。SQL 只供核對，未執行任何反向 DML：

```sql
-- 僅限本張隔離副本的 127.0.0.1:43324；本輪收尾已停止，勿改連正式 DB。
SELECT @@port, @@datadir;
SELECT objid, char_name, Type, AccessLevel, OnlineStatus
FROM p24_accounts.characters WHERE char_name='Aurora1165'; -- 最終 0 rows
SELECT COUNT(*) AS total, SUM(AccessLevel<>0) AS gm, SUM(OnlineStatus<>0) AS online
FROM p24_accounts.characters; -- 最終 17 / 0 / 0（由完整 SELECT 核算）
-- 還原 SQL：無需 DML；GUI 已刪除本輪新角色，完整隔離狀態回復依冷備份。
```

## 沿用，不重測、不計入本輪七項

| 編號 | 結果 |
|---|---|
| #1150 G02、G03 | ✅ 沿用主視窗 GDI／UIA；G04 圖不另計為重測 G02。 |
| #1149 S02、S02-GDI、S02-CLOSE | ✅ 沿用。 |
| #1118 S01 | ✅ 沿用；本輪只有 query session 前置。 |
| #1126 D01、C01 | ✅ 沿用既有診斷與收尾；WGC 不可用、不重試。 |
| #1108 B01、R05、R08–R15、C01、C02、V01 | ✅ 全數沿用。 |
| #1108 R07 七職業協定建角部分 | ✅ 沿用；本輪另完成 GUI 部分，沒有重放七職業協定腳本。 |

## 推測、待驗證與給下一棒的提示

1. **交工具維護者修 G07／P01**：SelectionPattern 的讀回應使用實際 .NET API（本機反射為 `pattern.Current.GetSelection()`）；既有 fixture 未涵蓋真實 API 形狀。修正後只重測這兩項；G04、G05、G06、F01、R07 均可沿用，不需再建角色。
2. S98 reason=2 與 S6 result=5 符合已驗證的 GUI／DB 結果與來源路徑，屬**推論**；metadata 並未保存這兩個 payload 值，不以推論冒充原始封包讀值。
3. 服務巢狀 Tab 的名稱／矩形現象另供分流；未測其切換或任何遊戲服務，不擴充本張清單。SM08／SM09／SM48、原生畫布輸入、WGC、世界內操作均未測。
4. 只變更本報告、精簡 JSON、指定 README；docs/REPORT.md、lock、引擎與 helper 程式不變。JSON／七職業合計 75／圖片 hash／git diff --check 完成核對；不新增模擬測試冒充實機結果。
5. 調度器清理前請保存本張 scratch 的 PNG、log 與冷備份；Git 證據只有原始值／SHA，不能取代圖片本體。不得對含 junction 的 worktree 遞迴刪除。
