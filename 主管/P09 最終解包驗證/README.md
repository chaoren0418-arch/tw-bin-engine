# TW1608262200：從二進位證據到獨立展示客戶端

這份 P09 學期交付包含可重建 Windows 客戶端、真實資產、本機 WebView2 面板、離線報告及必要證據。先開啟 **report/semester-report.html** 閱讀報告；可修改來源是 **report/semester-report.md**。姓名、學號與班級請在報告首頁填寫。

## 三分鐘展示

1. 在 Windows 解壓全部檔案，路徑可含中文及空白。不要直接在 ZIP 預覽內啟動。
2. 確認既有環境有 .NET SDK **10.0.300**、WindowsDesktop .NET **10** runtime、WebView2 Runtime，以及 PATH 中的 `pwsh` 和 Python 3。此次實測 Python **3.12.10**、WindowsDesktop **10.0.8**、WebView2 **155.0.4283.62**；沒有捆綁或自動安裝全機 runtime。
3. 雙擊 `launch-offline.cmd`。首次缺少建置輸出時會呼叫 `build.ps1`，由本機 vendor 還原固定 WebView2 SDK **1.0.4258.31**。離線模式無需測試伺服器。
4. 若需通訊展示，關閉離線視窗後雙擊 `launch-test.cmd`。它只啟動自有 `127.0.0.1` 臨時埠 fixture；關閉客戶端後測試端正常結束。

網頁欄位是本機文字測試區。F11 聚焦欄位，輸入 WASD/數字/方向鍵不應移動角色。F10 回遊戲，WASD/方向鍵可離線移動；1–8 轉向，F9 開關面板，F2/F3 示範建物／樹木前後遮擋，F12 保存畫面與模型，Alt+F4 正常退出。通訊模式只做站立轉向，收到有效回覆才套用方向。網頁 D0–D7 按鈕也走相同流程。

## 明確的展示邊界

單 64×64 地圖區塊（含來源自帶外伸物件）、單 prince3225 的站立與行走八方向；未載入鄰圖。碰撞、尋路與動態群組仍 unknown，角色可穿水與建物。沒有完整速度、負重、影子、音效或原服登入。通訊只是預置 key/object/序號的 post-init 切片，不能當正式服相容或 ACK 協定。WebView2 是 Windows 內嵌面板，非整個遊戲瀏覽器化、非 Awesomium ABI 重建。資源/BIN 同版配對及原 runtime RGB565 模式未知。

## 可重跑命令

在解壓目錄開啟 PowerShell 7：

```powershell
pwsh -NoProfile -File .\build.ps1 -Clean
python -B .\scripts\integrity.py
python -B .\scripts\launch.py --offline
python -B .\scripts\launch.py
```

一般 server/launcher/integrity 只依賴 Python 標準庫。**驗證 oracle 才需要** `requirements-verification.txt` 中的 Unicorn 2.1.4；本套件未離線捆綁它。無此依賴仍可重建／展示，不能宣稱跑過 oracle。P09 使用既有安裝，未執行安裝。

```powershell
$env:PYTHONUTF8='1'
python -B .\scripts\verify.py --clean --output .\runs\review-regression-01
python -B .\scripts\verify_web.py --output .\runs\review-web-01
python -B .\scripts\render_report.py
```

每次驗證的 `--output` 必須是此套件下尚不存在的目錄。一般執行自動寫 `runs/UTC時間`，WebView2 UDF 寫 `runtime/session-PID`；`build/`、`source/bin`、`source/obj`、`.dotnet/`、`.nuget/`、`runs/`、`runtime/` 都是可重建產物，不屬 manifest。不要覆寫 `evidence/p09-*` 正式證據。`prepare.py` 未打包，沒有上游覆寫入口。

## 導讀與來源

- [學期報告](report/semester-report.html)／[Markdown](report/semester-report.md)：問題、方法、結果、圖解、限制。
- [需求—交付—證據矩陣](report/evidence-matrix.md)：逐項驗證入口與證據。
- [選擇性來源指紋](provenance/selected-sources.json)：上游相對路徑、大小與 SHA-256。
- [P08 accepted 決策](provenance/accepted/P08-驗收決策.md)及 `provenance/accepted/`：八階段驗收快照。
- [實作邊界](architecture-and-boundaries.md)、[依賴來源](dependencies.json)。後者是 P08 建置環境快照，P09 當次環境見 `evidence/p09-environment.json`。

`source/`、`assets/`、`web/`、`vendor/` 保留 accepted P08 bytes，因此程式標題、組件名稱 `P08.WebClient` 及虛擬主機 `p08.example` 保留原名；不是執行時依賴 P08 原目錄。原 BIN、原遊戲 DLL、IDA 與原 IDX/PAK 大包都不需要放在執行機。`evidence/static` 是四個純函式指令 oracle，不是可啟動的原遊戲。

歷史文件在 `provenance/` 為不改寫的證據快照，其中上游絕對路徑及未選入的歷史附件僅供來源追溯，不是本套件執行命令。正式報告與矩陣的相對連結均指向本包已收錄檔案。本機報告不需伺服器、網路字型或 CDN。
