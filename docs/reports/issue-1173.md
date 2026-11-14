# Issue #1173：分頁 UIA 指定重測

**結論：部分通過（✅ 3／❌ 2／⚠️ 0）。** 本輪五項為 G05、G06、G07、N01、P01；G01 前置成功另列、不計入。G07／P01 外層切頁已通過；N01 名稱／矩形已正確，但聲音與血盟選取均仍停在背包；G06 預設網頁面板樹有一個 Name=null 錯誤。

2026-11-14 10:28–10:32（台北）完成建置、隔離環境、實機與收尾；其後整理報告。來源 main `88e23cc01e1807fc280897fed2b5ad6d96a3e2f1`，fetch 後與 task/1173 HEAD 相同。沒有修改引擎、helper、lock 或 docs/REPORT.md。

## 已驗證：逐項結果

下表 JSON／log 路徑相對本 worktree 的 `scratch/issue-1173/`。原始 UIA 樹、錯誤、讀回、SELECT、收尾與 SHA 已收進 [Git 證據](issue-1173-evidence.json)。六張 GDI PNG（合計約 150 KB）保存在 [圖片目錄](issue-1173-images/)，不依賴 scratch 留存。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| G01 | ✅ 沿用／前置成功 | p24svc01 到 CharacterList，3/3、4 欄位；三個角色 Gm=0 | 指定參數與測試帳密環境變數啟動，讀 login-state；G07 回登入頁核對 | `g01-login-state.json`、`g07-login-tree.json`、[登入圖](issue-1173-images/g07-login.png) |
| G05 | ✅ 通過 | 開發工具 PrintWindow 826×711、DPI192、非空白，頁面可辨識 | IncludeOwned 找窗 → Save-UiaGdiScreenshot → 目視 | `dev-info.json`、`g05-capture.json`、[GDI 圖](issue-1173-images/g05-dev.png) |
| G06 | ❌ 失敗 | 預設網頁面板 183 個樹節點中有 1 個 Name=null Error；其餘可讀 | 開發工具預設網頁面板 → Get-UiaTree | `g06-dev-tree.json`、[同頁 GDI 圖](issue-1173-images/g05-dev.png) |
| G07 | ✅ 通過 | 外層服務／登入雙向選取無例外、名稱一致；登入樹有建立角色 Button | Find-UiaControl -Type Tab -Scope Children → Select-UiaTab 服務 → 樹／圖 → 重新找外層 → 登入／選角 → 樹／圖 | `g07-result.json`、`g07-*-tree.json`、[服務圖](issue-1173-images/g07-service.png)、[登入圖](issue-1173-images/g07-login.png) |
| N01 | ❌ 失敗 | 16 個標籤名稱／矩形正確且含血盟；選聲音、血盟各一次均 IsSelected=false、TabSelection=背包，GDI 仍為背包 | 回服務 → 具名巢狀 Tab → 列樹／圖 → Select 聲音 → 樹／圖 → Select 血盟 → 樹／圖 | `n01-nested-tree.json`、`n01-result.json`、`n01-*-tree.json`、[標籤](issue-1173-images/n01-tabs.png)、[聲音失敗](issue-1173-images/n01-sound.png)、[血盟失敗](issue-1173-images/n01-clan.png) |
| P01 | ✅ 通過 | README 外層切分頁用法已實證並回寫；其餘用法沿用 #1165 | 使用 G07 同一輪結果驗證 Select-UiaTab＋Scope Children，未重跑已通過項目 | `g07-result.json`、[README](../../work/f1118-uia-path/README.md#切分頁最新實證1173p01) |

G07 兩次原文分別為「服務」「登入／選角」：RequestedName＝TabItem.Name＝TabSelection 唯一元素，IsSelected=true，TabItemError／TabSelectionError 均 null。登入圖部分按鈕文字受原有佈局裁切，建立角色的精確名稱與啟用狀態由 UIA 證實；沒有 Invoke。

### G06 錯誤與限制

```text
System.Management.Automation.RuntimeException: UIA property 'Name' is null
```

完整樹第 55 個節點（零起算 54）為 Error，前一節點是 Image／sceneImage「目前角色周圍的原場景」，後一節點是 StatusBar／incomingAttack。helper 錯誤物件未保留壞節點身分，因此不能判定哪個控制項有問題。初次 scratch 包裝腳本在已落盤樹上以 `.Type` 篩選時又因 Error 沒有 Type 中斷；僅修正一次性包裝腳本的輸出篩選，保留首次 G06 證據，不重測、不改 helper，繼續其他項目。

#1165 G06 基準是登入頁，本輪首次是預設網頁面板；不能據此宣稱 #1172 引入回歸。後續 G07 服務／登入與 N01 三棵服務樹均沒有 Error，但不覆蓋首次失敗。

### 全部 TabItem 與 GDI 對照

矩形為桌面實體像素 `x,y,w,h`；GDI 圖左上角為 `(441,441)`，圖內位置為矩形減此原點。本輪目視逐一比對文字及矩形落點，未用矩形點擊。

外層六個（服務／登入兩次完全相同）：

| 名稱 | 矩形 | 與圖一致 |
|---|---|---|
| 網頁面板 | `458,503,108,36` | ✅ |
| 服務 | `566,503,78,36` | ✅ |
| 登入／選角 | `644,503,132,36` | ✅ |
| 控制 | `776,503,78,36` | ✅ |
| 狀態列 | `854,503,84,36` | ✅ |
| Button | `938,503,90,36` | ✅ |

巢狀全部 16 個（本次 CharacterList 狀態實際可見的集合，不以離線 fixture 的 18 個當實機要求）：

| 名稱 | 矩形 | 與標籤圖一致 |
|---|---|---|
| 背包 | `466,583,78,36` | ✅ |
| 地面 | `544,583,78,36` | ✅ |
| 魔法 | `622,583,78,36` | ✅ |
| 角色 | `700,583,78,36` | ✅ |
| 能力點 | `778,583,84,36` | ✅ |
| 個人商店 | `862,583,108,36` | ✅ |
| NPC | `970,583,78,36` | ✅ |
| 交易 | `1048,583,78,36` | ✅ |
| 聲音 | `1126,583,78,36` | ✅ |
| 好友／封鎖 | `466,547,132,36` | ✅ |
| 決鬥 | `598,547,78,36` | ✅ |
| 求婚 | `676,547,78,36` | ✅ |
| 寵物 | `754,547,78,36` | ✅ |
| 血盟戰爭 | `832,547,108,36` | ✅ |
| 稱號／表情 | `940,547,132,36` | ✅ |
| 血盟 | `1072,547,78,36` | ✅ |

巢狀 Tab 具 SelectionPattern；16 個 TabItem 都具 SelectionItemPattern，沒有 ScrollItemPattern。名稱／矩形與「血盟存在」子條件通過。兩次選取錯誤原文：

```text
Select-UiaTab verification failed: {"RequestedName":"聲音","TabItem":{"Name":"聲音","IsSelected":false},"TabSelection":["背包"],"TabItemError":null,"TabSelectionError":null}
Select-UiaTab verification failed: {"RequestedName":"血盟","TabItem":{"Name":"血盟","IsSelected":false},"TabSelection":["背包"],"TabItemError":null,"TabSelectionError":null}
```

兩張失敗 GDI 圖相同，背包仍選取，內容是空白背包頁，沒有聲音或血盟頁內容。沒有用鍵盤、座標重試，也沒有按任何服務頁內按鈕。

## 環境、備份與收尾（已驗證）

- 開始只記 `query session`：console／CHAOREN／6／Active。lease holder=`chaoren0418-arch/tw-bin-engine#1173`、exp=`1794628686.1391792`（2026-11-14T03:58:06Z），測試在期限內。
- `engine/build-product.ps1 -ConfigFile .github/nuget.ci.config`：product／launcher restore、build 全 0，PASS dev。使用 temporary lock、不寫 rebuild-lock。DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false、DOTNET_NOLOGO=1；TEMP／TMP 在本張 scratch，新 PowerShell process-only Bypass，未改系統設定。
- product DLL SHA-256：`998cc2d0983635a888bfde4f04f83408f2410ee84852acee10d6bf385d82ca95`。helper 與兩個引擎來源 SHA 見 Git 證據。
- 原 `environments/p24/db-data` 兩埠空閒時，以禁止其他寫入的 ibdata1 handle 保護，完整冷備份，再建立執行副本；1,722 檔、279,502,566 bytes，備份與副本逐檔 SHA 一致。manifest SHA=`da7743d458bd0536766124694d034821da2e0d90f2033550fc78a2ca37c036db`。原路徑只讀；D:/BinEngine 是現有專案路徑別名。
- SQL 回報 port43324、datadir=`D:/BinEngine/scratch/wt/task-1173/scratch/issue-1173/db-data/`。Java 工作目錄／home／tmp／log 全在本張 scratch；config、jar、maps 使用原 P24Lab 唯讀 junction，未修改機密設定、未複製 pak／map／sprite。
- 啟動 `--assets <worktree>/engine/assets --login-port 42061 --login-demo real-interactive --classic-ui --evidence <worktree>/scratch/issue-1173/client`；TW_CLIENT_ROOT=`D:/lineage開發/0-客戶端`。帳密由既有 private 檔讀取至環境變數，未輸出或提交。
- 客戶端 PID3592 CloseMainWindow=true、Exited=true；Java12248 核對自啟 PID／執行檔後停止；MySQL 父19948／子16772 以指定 43324 的 mysqladmin shutdown 正常關閉。最終 RemainingOwned、RelatedProcesses、Listeners 均空，42061／43324 無監聽。
- 最後完整 SELECT 17 角色全部 AccessLevel=0、OnlineStatus=0；指定欄位前後逐字相同。原持久 DB 1,722 檔 SHA 不變、無新增檔。沒有人工 DB DML、建角／刪角、補物品、GM／血盟／寵物資料修改；登入造成的自動寫入僅限隔離副本。
- 未操作正式 2000／3306、原 BIN、正式客戶端、封存、登錄檔／系統設定／服務；未拆 junction、遞迴刪除或移除 worktree。

還原 SQL：無需 DML。原持久 DB 未改；若需回復隔離環境的登入自動寫入，在程序全停後以 `backup-before/db-data` 建立新副本並核 SHA，不覆寫持久資料。核對 SQL 僅限本張 43324：

```sql
SELECT @@port, @@datadir;
SELECT account_name,char_name,objid,Type,AccessLevel,OnlineStatus
FROM p24_accounts.characters ORDER BY objid;
```

## 沿用，不重測、不計入本輪五項

| 編號 | 原結果 | 本輪處理／證據 |
|---|---|---|
| #1165 G04 | ✅ 沿用 | 沿用 #1165 成功結果；[基準](issue-1165.md) |
| #1165 F01 | ✅ 沿用 | 沿用 #1165 成功結果；[基準](issue-1165.md) |
| #1108 R07 | ✅ 沿用 | 沿用 #1165 七職業 GUI、一次送出與 GUI 刪除；[基準](issue-1165.md) |
| #1150 G02 | ✅ 沿用 | 沿用主窗 GDI／UIA；[基準](issue-1165.md) |
| #1150 G03 | ✅ 沿用 | 沿用主窗 GDI／UIA；[基準](issue-1165.md) |
| #1149 S02 | ✅ 沿用 | 沿用記事本前置；[基準](issue-1165.md) |
| #1149 S02-GDI | ✅ 沿用 | 沿用記事本前置；[基準](issue-1165.md) |
| #1149 S02-CLOSE | ✅ 沿用 | 沿用記事本前置；[基準](issue-1165.md) |
| #1118 S01 | ✅ 沿用 | 沿用；本輪只記 query session；[基準](issue-1165.md) |
| #1126 D01 | ✅ 沿用 | 沿用既有診斷；D01 表示 WGC 不可用，不重試；[基準](issue-1165.md) |
| #1126 C01 | ✅ 沿用 | 沿用既有診斷；D01 表示 WGC 不可用，不重試；[基準](issue-1165.md) |
| #1108 B01 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R05 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R08 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R09 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R10 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R11 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R12 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R13 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R14 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 R15 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 C01 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 C02 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |
| #1108 V01 | ✅ 沿用 | 沿用 #1165 指定基準；[基準](issue-1165.md) |

## 推測、待驗證與給下一棒的提示

1. **交調度器分流 G06、N01，修正後只重測失敗項**；G05、G07、P01 與 G01 前置已有結果。G06 需先釐清預設 WebView 樹的 null Name 控制項，不假定是本次引擎變更造成。
2. N01 的名稱／矩形修正已驗證有效；SelectionItem.Select 為何未切頁仍未知，不能把「讀回 API 正確」推論為服務分頁選取可用。兩次具體失敗的樹、圖、原文均已保存。本張未修程式。
3. 未驗證任何服務子頁功能、盟屋動態頁、handle 重建、SM08／SM09／SM48、世界操作、原生輸入、WGC；不新增測試範圍。
4. 產出：本報告、`issue-1173-evidence.json`、六張 `issue-1173-images/*.png`、指定 README。完整 log、一次性腳本、冷備份留 `scratch/issue-1173/`；調度器收工前自行保存所需備份，不對含 junction 的目錄遞迴刪除。

文件交付檢查已通過：JSON 可解析、6／16 個標籤、兩次外層原文讀回、兩次巢狀失敗原文、唯一 G06 錯誤、六張 PNG SHA、PrintWindow 方法、角色前後讀值與完整收尾狀態一致；`git diff --check` 無錯。提交前再次 fetch，origin/main 仍是同一來源，無需同步或重建。
