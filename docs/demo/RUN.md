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

## M5 smoke 準備（#175）

2026-11-06 的 [#175準備報告](../reports/issue-175.md)已核實：`服務驗證`暫設 AccessLevel200；同一既有帳號新增`服務交易`（一般角色、AccessLevel0），在 map0 `(32558,32975)`，與原角色 `(32557,32975)`相鄰。原角色背包已有弓172×1、普通箭40743×200、普通肉40056×20、治癒藥水40010×40、金幣40308×100000。沒有新增帳號或憑證，沒有啟動遊戲驗證。

GM只用來準備位置與條件，實際驗收仍採一般玩家操作；不能把GM效果或本張直接新增物品當成戰鬥／購買通過。smoke結束須將原角色AccessLevel還原0，SQL見報告；本張準備結束時按主控要求保留200。兩角色同帳號的同時登入與雙客戶端交易可用性尚未驗證，不修改登入政策繞過。

下表NPC位置由本次實驗資料唯讀核對；準備落點另核對伺服器與客戶端地形可通行，但**不保證無動態怪物或其他角色**。不要落在NPC本體格；先核對當下畫面與自身座標，再按正常玩家方式交談、學習、購買或進出地圖。本張只記錄指令，沒有送出。

| 測項 | 已核對目標 | 準備指令（一般聊天的GM指令） | 後續驗收 |
|---|---|---|---|
| NPC商店／普通肉購買 | 潘朵拉70014，map0 `(32645,32951)`；肉40056目前販售基價1、pack_count0→1 | `.tp 32644 32951 0` | 開實際購買清單，按回包單價成交；前後金幣／肉數量另記，不以本次背包20塊作購買證據。 |
| 倉庫 | 凱倫60007，map4 `(32608,32787)`，L1Dwarf | `.tp 32607 32787 4` | 一般個人倉庫存入再領出，至少留30金幣領取費。說島60001本次沒有固定出生列，勿假設它可用。 |
| 技能NPC | 吉倫70009，map0 `(32560,33086)`，NPC_Gereng；技能購買處理器包含此NPC | `.tp 32559 33086 0` | 依職業／等級／善惡值開清單，選尚未學會的支援技能；實際清單與價格待測。 |
| 洞穴 | map0入口 `(32477,32851)` → map1 `(32669,32802)` | `.tp 32477 32853 0` | 普通步行進洞並正常離洞；#164曾入洞後受攻擊死亡，須留正常離開時間，不以死亡恢復代替往返。 |
| 說島港口 | 港口主人70011，map0 `(32633,32968)`；登船點 `(32631,32983)` | `.tp 32632 32968 0` | 既有往古魯丁船票1張；按S123遊戲時間確認班次，正常登船／到港，不以GM直接到港代替船運。 |
| map4港口／6016 | 港口主人70022，map4 `(32560,32723)`；回程登船點 `(32543,32728)` | `.tp 32559 32723 4` | 分別核對場景6016進入有聲、離開停止；回程需正常買票／搭船。實際音效事件仍待smoke。 |

GM語法核對：目前指令表 `tp` 要求200、執行類別L1Move；聊天以`.`分派GM命令，參數順序為x、y、map，`.loc`可讀當下座標。`L1Move.java` SHA-256：`f2b1c5cf2b6a9d92b504bf2d57ecb8c89bc9d454b990d6656192967a8ec8819c`。伺服器來源及現有地形僅唯讀引用，沒有複製素材進repo；執行中JAR指令與實際服務仍待後續smoke確認。

原角色本次未補HP，準備後仍為67/686；應以既有藥水／正常遊戲操作恢復後再進危險區。直接交易前核對兩個授權測試角色、相鄰位置及相反面向；原角色目前heading4，不能把「已相鄰」當成「已面對面」。本張MySQL已正常關閉，後續需由正式派工取得有效排他使用後再啟動。
