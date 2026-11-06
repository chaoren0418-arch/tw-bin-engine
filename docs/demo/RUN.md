# 自己跑一次 dev

適用 Windows x64 主專案工作目錄。先備妥 Python 3.12、.NET SDK `10.0.300`、.NET 10 Desktop Runtime、WebView2，以及現有唯讀素材 junction 與離線 NuGet 套件；詳見 [建置說明](../BUILD.md)。以下在專案根目錄的同一個 PowerShell 視窗依序執行，任一步失敗就停止。版本與本次證據見 [展示首頁](README.md) 及 [Issue #70 報告](../reports/issue-70.md)。

## 1. 準備本次命令環境

指令（使用已安裝的 Python；只調整目前視窗，不改全域設定）：

```powershell
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath 'engine/_integration/dev/rebuild.py')) { throw '請先切換至專案根目錄' }
$pythonExe = & py -3.12 -c "import sys; print(sys.executable)"
if ($LASTEXITCODE -ne 0) { throw '需要已安裝的 Python 3.12 與 py launcher' }
$env:PATH = (Split-Path -Parent $pythonExe) + ';' + $env:PATH
$env:NUGET_PACKAGES = Join-Path (Get-Location) 'engine/.nuget'
python --version
if ($LASTEXITCODE -ne 0) { throw 'Python 不可用' }
'PASS demo environment'
```

預期輸出：`Python 3.12.x`、`PASS demo environment`。若先備軟體或素材未就緒，交由本機執行器處理；不要複製素材或改 lock 來略過檢查。

## 2. 建置 dev

指令：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/demo-1/rebuild
if ($LASTEXITCODE -ne 0) { throw 'dev 建置失敗，請檢查 scratch/demo-1/rebuild' }
```

預期退出碼 `0`，依序包含以下輸出（路徑與 SHA-256 依此次建置而定）：

```text
sdk 0
product-restore 0
product-build 0
launcher-restore 0
launcher-build 0
receipt written <專案根>/engine/_integration/dev/receipt.json
PASS dev {"productDllSha256": "…", "audioDllSha256": "…", "productManifestSha256": "…", "launcherDllSha256": "…"}
```

主程式輸出在 `engine/build/client-product-dev/`，manifest 在 `engine/build/product-dev-manifest.json`，launcher 在既定 `engine/launcher/`，中間產物留在各專案 `obj/`／`bin/`。`scratch/demo-1/rebuild/` 存子命令紀錄與暫存；不需搬移產物。Receipt 含建置時間，重建後其檔案 SHA-256 會改變。

## 3. 檢查入口（不開遊戲）

指令：

```powershell
& '.\engine\啟動新版客戶端.cmd' --check-only
if ($LASTEXITCODE -ne 0) { throw '入口檢查失敗，請先處理錯誤' }
```

預期退出碼 `0`，JSON 至少包含以下欄位（其餘路徑、版本與檔案計數依環境而定）：

```json
{
  "status": "checked-only",
  "revision": "dev",
  "autoLogin": false,
  "uiStarted": false,
  "networkStarted": false,
  "servicesStarted": false
}
```

這一步只證明入口與環境檢查通過；登入、戰鬥、保存、聲音等仍須 DEMO-2 真機驗收。

## 4. 手動啟動與驗收（使用者操作／DEMO-2）

P24Lab 的啟停：**由 dot 執行，詳見 [AGENTS.md](../../AGENTS.md)**。待實驗環境就緒、check-only 通過後，使用者執行以下指令；DEMO-2 真機錄製依其任務授權執行。本次 DEMO-1 未執行這條指令。

```powershell
& '.\engine\啟動新版客戶端.cmd'
if ($LASTEXITCODE -ne 0) { throw 'dev 客戶端啟動失敗' }
```

預期 launcher 輸出 `已啟動 dev 客戶端；請在正常登入畫面手動登入。`，退出碼 `0`，並開啟登入畫面（畫面是否正常仍待真機驗證）。此處 launcher 的退出碼不代表遊戲流程驗收完成。使用者手動登入後，依 [展示首頁](README.md) 的功能表逐項觀測；DEMO-2 補上截圖、結果與限制。

## 5. DEMO-3 第2c輪：攻擊、光箭、數量與權威座標

本節由使用者或已有授權的 dot 操作。先完成合併後dev建置與check-only，完整核對清單及可丟物品限制見 [Issue #115](../reports/issue-115.md)。

- **攻擊**：F10回遊戲焦點，走近目標、停下等待可操作；點中敵人可見圖像，看到黃色圈及「已選取…#ID」後按一次G。雙擊／Ctrl＋左鍵及診斷面板「單次攻擊」也是單次請求。未命中圖像會走地面；目標移動或忙碌時讀訊息，停下再手動重試，沒有自動追擊。
- **技能4光箭**：「服務」→「魔法」選光箭，再選「可見施法目標」並按單次施法。也可選技能後點場景敵人、確認黃圈、F10後按M。光箭自動用敵方目標，不需切「自己」；清單空時先調整站位。
- **數量0／超額**：在背包輸入0或持有量+1按丟棄，應顯示本機「數量不合法」，清掉舊125且不增加C25；web重繪後也應保留本機錯誤。
- **自身座標**：F9診斷面板讀「原服座標 map / x / y · S87或S10 #序號」。另列的「畫面預測」不算權威證據。「移動後尚無位置回包」代表尚不能確認新終點；此協定無逐步成功ACK，必要時在該段終點正常登出／重登取得S87，不能拿最後一次重登驗所有中間點。
- **丟撿候選**：普通肉40056是#77已證實可丟的候選；先透過NPC實際販售清單合法買足至少11，再在清理廣播完成後立即做丟1撿1、丟10撿10。商店供應尚未驗證，若買不到則回報阻塞，不改DB。藥水／金幣及歷史箭已有拒絕證據，不當替代品。不要按U或肉快捷格，避免把肉吃掉。地面是全服週期清理，剛丟出的物品也可能很快被清掉。

- **拾取錯誤後更正（#115重審補測）**：選地面普通肉，輸入0或超過地面數量並按拾取，確認錯誤且沒有C112；不要重新點物品，直接改成1再按拾取，應只送一次C112。以回包確認數量；若地面物品已被清除或改量，須重新選取，不將過期保護判為同一缺陷。
