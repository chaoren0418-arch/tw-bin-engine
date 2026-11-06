# Issue #94／T080：交作業版架構與資料流

日期：2026-11-06；階段：design；第 1 次嘗試；分支：`task/94`。

## 做了什麼／結論

已新增 `docs/ARCHITECTURE.md`，共 199 行：一句話定位、Mermaid 元件圖、依實作分組的模組表、五條各 6–8 步的資料流、建置與可重現連結、Backlog 限制。內容以 `origin/main@31931256008607d9df21558d2fa26ed7e3d32bf9` 靜態閱讀為準；只新增架構頁與本報告，未改產品、lock、`engine/README.md` 或 `docs/REPORT.md`。

特別核對並明示：根入口仍指 r33、dev 有自己的入口；WinForms 負責場景而 WebView2 是面板；`engine/assets` 是既有 r28 衍生素材 junction，地面 SPR 與戰鬥 WAV 另直接讀使用者原客戶端；丟棄送出不代表數量已改變。`LoginTests.Vectors` 與 `MovementGateRegression` 是離線測試，`LoginTests.Probe` 會連測試端點，不混作產品功能或離線驗收。

## 隔離與規則

先讀主目錄 `AGENTS.md`、`docs/REPORT.md`，再 fetch；本任務具體 worktree 隔離要求優先於通用 checkout 前言，未在主目錄 checkout／reset。

```text
git fetch origin
git worktree add scratch/wt/task-94 -b task/94 origin/main
```

兩條命令 exit 0；工作目錄為 `D:\lineage開發\TW1608262200研究\scratch\wt\task-94`。主目錄仍為 `task/77@50d09756722961827411d2cca5d7900a1cf75f3b`；既有未追蹤截圖、receipt 與 runtime 未動。沙箱程序啟動前失敗，改用獲准的工作區命令執行；`rg` 不在 PATH，改用 Git／PowerShell 唯讀搜尋。

## 怎麼驗證／引用檔案清單

以下 PowerShell 在本 worktree 執行。所有 Markdown 檔案連結正規化為 repo 相對路徑；`git ls-files --with-tree` 核對追蹤路徑，另以 `git ls-tree` 排除只存在 index、卻不在 main 的可能，再以 `git diff` 確認引用檔與基準內容一致。

```powershell
$base = '31931256008607d9df21558d2fa26ed7e3d32bf9'
$doc = Get-Content docs/ARCHITECTURE.md -Raw -Encoding UTF8
$refs = [regex]::Matches($doc,'\]\(([^)]+)\)') | ForEach-Object {
    $rel = $_.Groups[1].Value.Split('#')[0]
    $abs = [IO.Path]::GetFullPath((Join-Path (Join-Path (Get-Location) 'docs') $rel))
    $abs.Substring((Get-Location).Path.Length+1).Replace('\','/')
} | Sort-Object -Unique
git ls-files --with-tree=$base --error-unmatch -- $refs
$mainPaths = @(git -c core.quotepath=false ls-tree -r --name-only $base -- $refs)
Compare-Object $refs $mainPaths
git diff $base -- $refs
```

`git ls-files` 原始 stdout（57 個唯一檔案，exit 0）：

```text
.github/workflows/build.yml
TASKS.md
docs/BUILD.md
docs/demo/README.md
docs/reports/issue-91.md
engine/_integration/dev/launcher-source/Launcher.csproj
engine/_integration/dev/launcher-source/Program.cs
engine/_integration/dev/rebuild-lock.json
engine/_integration/dev/rebuild.py
engine/audio/AudioAssets.cs
engine/audio/AudioPlayer.cs
engine/audio/DirectShowDriver.cs
engine/audio/P24.Audio.csproj
engine/source/Assets.cs
engine/source/AudioApiTransport.cs
engine/source/ClientCombatSfx.cs
engine/source/ClientEffect6016.cs
engine/source/ClientLogin.cs
engine/source/Combat.cs
engine/source/Equipment.cs
engine/source/EventJournal.cs
engine/source/Ground.cs
engine/source/GroundCatalog.cs
engine/source/GroundRendering.cs
engine/source/GroundSpriteReader.cs
engine/source/GroundUi.cs
engine/source/Inventory.cs
engine/source/LoginConnection.cs
engine/source/LoginControl.cs
engine/source/LoginProtocol.cs
engine/source/LoginTests.cs
engine/source/Magic.cs
engine/source/MapEffects.cs
engine/source/MapResources.cs
engine/source/MovementRegression.cs
engine/source/ObjectAssets.cs
engine/source/ObjectRendering.cs
engine/source/OfflineClient.csproj
engine/source/OnlineInput.cs
engine/source/OnlineMovement.cs
engine/source/OriginalMapCache.cs
engine/source/PacketTrace.cs
engine/source/PickupPresentation.cs
engine/source/PlayerHudView.cs
engine/source/PlayerPointer.cs
engine/source/ProductEntry.cs
engine/source/Program.cs
engine/source/Ranged.cs
engine/source/Renderer.cs
engine/source/WebPanel.cs
engine/source/WorldEffect6016.cs
engine/source/WorldEntry.cs
engine/source/WorldObjects.cs
engine/source/WorldSession.cs
engine/web/panel.js
engine/啟動新版客戶端.cmd
啟動引擎.cmd
```

`Compare-Object` 與 `git diff` 都無輸出；上述 57 檔全部存在於取樣的 main tree，且內容未改。
架構頁中的 `engine/assets`、素材相對名稱、pak／idx、產物與目錄標籤屬執行期契約，不冒充 Git 檔案；其存在約定由連結的讀取程式或 BUILD 支持，本輪未實際開啟素材。

文件驗證輸出：`PASS lines=199 refs=57 main-tree-missing=0 source-diff=0`。`git diff --cached --check` 無輸出、exit 0；暫存差異僅上述兩份新增文件。

## 五個資料流步驟抽查

行號均為上述 main 基準，為原始碼行號，不是 BIN 位址。以下各列對應架構頁一條流程。

| 流程／步驟 | 函式與核對內容 |
|---|---|
| 啟動 2–4 | launcher `Main`：先 `Verify`，check-only 返回在 `Process.Start` 之前。 |
| 登入 2 | `LoginConnection.Run`：收取 TCP bytes 後交給 `LoginFrames.Feed`。 |
| S87 3 | `WorldSession.ReceiveCore`：`case 87` 對非自身 ID 呼叫 `ReceiveObject`。 |
| 丟棄 3 | `SendGroundQuantity`：檢查數量與 `CurrentGroundQuantity`，通過才 `SendGround`。 |
| 素材 4 | `OriginalMapStore`／`Load`／`Verified`：使用素材根的 maps 目錄、驗 SHA 後解析區塊。 |

抽查方式：`Select-String -LiteralPath <下列檔案> -Pattern <函式名或分支>`；實際匹配輸出如下。

```text
engine/_integration/dev/launcher-source/Program.cs:12: static int Main(string[] args){
engine/_integration/dev/launcher-source/Program.cs:19: var receipt=Verify(root);
engine/_integration/dev/launcher-source/Program.cs:20: if(check){Console.WriteLine(JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));return 0;}
engine/_integration/dev/launcher-source/Program.cs:24: using var child=Process.Start(start)??throw new InvalidOperationException("無法啟動已驗證的客戶端。");
engine/source/LoginConnection.cs:26: public async Task Run(int port,string profileId,string account,string password,int timeoutMs=5000,CancellationToken token=default){
engine/source/LoginConnection.cs:54: foreach(byte[] body in frames.Feed(buffer.AsSpan(0,n).ToArray())){
engine/source/WorldSession.cs:30: void ReceiveCore(byte[] plain,string selected){
engine/source/WorldSession.cs:40: case 87:
engine/source/WorldSession.cs:42: if(Status==null||id!=Status.ObjectId){ReceiveObject(r,x,y,id);break;}
engine/source/GroundCatalog.cs:41: public Task<bool> SendGroundQuantity(bool pickup,string text,GroundQuantitySelection? selection){
engine/source/GroundCatalog.cs:46: if(!World.CurrentGroundQuantity(pickup,selection)){
engine/source/GroundCatalog.cs:49: return SendGround(pickup,count,()=>World.CurrentGroundQuantity(pickup,selection));
engine/source/OriginalMapCache.cs:16: public OriginalMapStore(string assetsRoot,int? mapId=null,CancellationToken token=default){loadToken=token;loadToken.ThrowIfCancellationRequested();
engine/source/OriginalMapCache.cs:30: byte[] Verified(string path,string hash){loadToken.ThrowIfCancellationRequested();var b=File.ReadAllBytes(path);loadToken.ThrowIfCancellationRequested();if(!Convert.ToHexString(SHA256.HashData(b)).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original cache source hash: "+path);return b;}
engine/source/OriginalMapCache.cs:34: public OriginalMapBlock Load(int bx,int by,int generation){loadToken.ThrowIfCancellationRequested();
```

上述五個檔案另以 `Get-FileHash -LiteralPath <檔案> -Algorithm SHA256` 識別：

| 檔案 | SHA-256 |
|---|---|
| `engine/_integration/dev/launcher-source/Program.cs` | `c804718a386f5d53e708695791b71704341496119fbc2ef45981286c7bc6de6d` |
| `engine/source/LoginConnection.cs` | `2239e0918b0c2ae94a29debcd0ca5428a44d1f95d128f469c9c1053616dd9cfe` |
| `engine/source/WorldSession.cs` | `92bf845b68138cdcb1d85e675cd7697579815cea83d173a97ca913cc7f9baf0a` |
| `engine/source/GroundCatalog.cs` | `89bed0d3164140d5ed6515a048c7bd4cd254804d54dd4b82e3324291633c135d` |
| `engine/source/OriginalMapCache.cs` | `8c7009fd8483196beda9108a07a01f54b1e02d4447ba6cb3c6b88fb78b15d084` |

## 未驗證什麼／推測界線

- 只有文件與靜態程式碼核對；未建置、未執行 check-only 或任何產品測試，未啟動遊戲／服務、未連 DB 或正式服務。
- 未碰機密設定、原 BIN、正式客戶端、封存目錄、硬連結，也未建立或變更素材 junction。
- Mermaid 僅檢查語法結構與依賴含義，未用瀏覽器渲染；五分鐘可讀性尚待評分者／審查者確認。
- #91 的可重現結論是引用既有報告，沒有在本輪重新執行；不宣稱跨 OS 或任意 SDK 產物相同。
- `docs/BUILD.md` 的 CI 舊段落與現行 YAML 不同；架構頁已指明以 YAML 的 Ubuntu 交叉建置為準，依任務範圍未修 BUILD。
- 未把 main 上既有實作、離線向量或合成傳送門檻當成真機成功；功能缺口與金幣限制引用 TASKS／Demo。

## 產出路徑與清理

提交僅包含：

- `docs/ARCHITECTURE.md`
- `docs/reports/issue-94.md`

一次性驗證腳本及 PR 文字僅放本 worktree 的 `scratch/issue-94/`，不提交；本任務未在 worktree 外產生暫存，沒有其他清理目標。保留 worktree 供審查。

## 給下一棒的提示

由 Claude／調度器審查 task/94 的兩份文件，核對架構內容及 main 基準；通過後交使用者合併並由主控更新 T080。此設計任務是現有產品導覽，無需拆後續實作任務，故不建立 `docs/plans/issue-94.json`。未做本輪真機驗收不是本文件交付阻塞；既有功能驗收仍依 TASKS 分派。
