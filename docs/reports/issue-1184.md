# Issue #1184 f1155-capture-n03：登入畫面 Ctrl+P（#1155 N03 ❌）

階段 code，runner claude-local，第 1 次嘗試。分支 `task/1184`。

## 結論
- **採 B：不改引擎，改測試工具。** 原版登入畫面按 Ctrl+P **不會**截圖（已驗證，靜態），引擎現在的設計（R4-A／#705：選單熱鍵含 Ctrl+P 只在世界內有效）和原版一致。
- #1155 的 N03 失敗是因為測試把「有新檔」當成通過條件，和引擎、原版都相反。現在 N03 改成登入畫面的**反向核對**（`N03.Mode='login-negative'`），全部通過時腳本回 0 的分支可以走到。

## 原版查證（TW16，IDA 7.7 `idat -A` 唯讀批次）
IDB：從 `scratch/wt/task-629/scratch/rea/ida/tw16.idb` 複製到本 worktree 的 `scratch/t1184/ida/`（不進 git）。執行器沿用 `docs/reference/rea-scripts/t956/run_tw16.ps1`（subst `Q:`，結束時已解除），腳本沿用 `t742/ida_t742.py`，只做查詢。查詢清單存到 `docs/reference/rea-scripts/t1184/queries.txt`；反編譯輸出在 `scratch/t1184/ida/o1–o10/`（不進 git）。
TW16 是 VA＝映像基底 `0x400000`＋RVA；`.text` 段 FO＝VA−`0x400C00`（和 followups-4 的三組對照 `0x8C14F0/0x4C08F0`、`0x8CA740/0x4C9B40`、`0xAC22D0/0x6C16D0` 一致）。關鍵地址：`sub_8BD990` FO `0x4BCD90`、`sub_8CA5E0` FO `0x4C99E0`、`sub_EE2360` FO `0xAE1760`、`sub_EE8E00` FO `0xAE8200`、`sub_4FE4F0` FO `0xFD8F0`。其他只列 VA，FO／RVA 依同一換算。

**已驗證**
1. 主視窗程序 `sub_8BD990`（VA `0x8BD990`，RVA `0x4BD990`）處理 `WM_CHAR` 時一律呼叫函式指標 `off_136EB78(wParam, …)`（反編譯第 151 行），不是直接呼叫 `sub_8CA740`。
2. `off_136EB78` 是隨遊戲階段替換的 WM_CHAR 處理器（`gstores` 全程式掃描 18 處寫入）：
   - 靜態初值 `sub_8B2A40`（`return 0`，不處理任何鍵）。
   - **登入畫面**：XML 工廠 `sub_B56A40` 的 `strcmp("LoginUI")`（VA `0xB57D18`）建立 `CLoginUI`（建構子 `sub_EE8260`，vtable `??_7CLoginUI@@6B@`＝`0x11E3AD4`）。它的顯示函式（vtable 槽 `0x11E3B18`→`sub_EE8E00`）先呼叫 `sub_EE2360(a2)`；`a2` 為真時寫入 `off_136EB78 = sub_8CA5E0`。伺服器封包分派 `sub_947530` 的 case 61／77／209 呼叫 `sub_ECB0F0`，也寫入同一個 `sub_8CA5E0`。
   - `sub_8CA5E0`（VA `0x8CA5E0`，RVA `0x4CA5E0`）全文只有：交給 XML 視窗層 `sub_B557C0`，否則交給 `dword_13CCB24` 物件 vtable `+32`。**沒有任何 Ctrl+P（16）分支，也不呼叫截圖。**
   - **世界內**：`sub_8CA740`（含 Ctrl+P）只由 `sub_7466F0`（參數 0）、`sub_9CF250`、`sub_A51920`、`sub_EBDCC0`、`sub_EC5440`、`sub_EC7560` 寫入；其中 `sub_9CF250`／`sub_EC5440` 由伺服器封包分派 `sub_94E0E0` 呼叫，都是離開登入／選角類 UI 之後的處理。
3. 截圖函式 `sub_4FE4F0`（VA `0x4FE4F0`）＝`sub_4FE520("Saved current screen to %s", …)`，字串 `0x114F6C0` 只有這一個參照。它的 6 個呼叫點：
   - `0x8C17EC`：`sub_8C14F0`（`--v16` case 15＝WM_CHAR 16），只由 `sub_8CA740`（`0x8CAB26`）呼叫。
   - `0x8C245B`：`sub_8C2290` case 16。它只由 `sub_AC22D0`（`0xAC244F`）呼叫，而 `sub_AC22D0` 是 `sub_AC1170` 用 `SetWindowLongA(..., GWL_WNDPROC)` 掛在內嵌網頁的 **`Internet Explorer_Server`** 視窗上的子類別程序。它**不是**登入帳號／密碼框（followups-4 寫的「編輯框焦點版」，實際上指的是內嵌網頁視窗）。
   - `0x8C5675`：`sub_8C5380` case 16，由 `sub_AC8E10` 呼叫；`sub_AC8E10` 在 `0xAC…` 網頁元件類別的 vtable 裡（`0x119F038`／`0x119F120`）。
   - `0x8CA1F7`：`sub_8CA1A0`，要求 `dword_13CE3D8 == 3`（另一個訊息路徑）。
   - `0xF4159A`／`0xF42453`：`sub_F413E0`／`sub_F42420` 的定時連拍。
4. 所以焦點在登入帳號欄時，Ctrl+P 走 `sub_8BD990` → `off_136EB78`＝`sub_8CA5E0` → XML 視窗，不會到截圖。

**推測**
- `sub_8C5380`（網頁元件 vtable 內）與 `sub_8CA1A0` 不在登入畫面路徑上：依它們所屬的類別與 `dword_13CE3D8 == 3` 判斷，沒有逐一追到所有呼叫時機。
- `dword_13CE3D8 == 3` 代表世界內：依 `sub_8BD990` 多處以 3 為條件做遊戲畫面處理推測，沒有逐一核對階段值定義。
- 登入畫面若同時顯示內嵌網頁且焦點在網頁上，原版可能經由 `sub_AC22D0`→`sub_8C2290` 截圖；引擎沒有內嵌網頁登入公告，這個情境不適用 N03（帳號欄有焦點）。
- TW13（`sub_4D3910`／`sub_4D4510`）的對應分派本輪**沒查**（預算內 TW16 已足夠判定）。

## 改了哪些檔
- `work/f1118-uia-path/Test-NativeClient.ps1`
  - N02 結束時保留 `n02-after` 影格的複本（`$n02After`），在最外層 `finally` 釋放。
  - N03 改成 `login-negative`：記錄 `Mode`／`OriginalBasis`／`EngineBasis`；送 Ctrl+P 後**完整等 3 秒**（反向核對不提前結束）；記錄新檔（有的話含 SHA-256）；`ClientRunning`；擷取 `n03-after.png`，在帳號欄矩形內和 `n02-after` 比對（`Diff` 門檻 48，允許改變像素 `< 30·k²`，和 N02 同一單位：游標閃爍約 14 像素不算、多一個字元就算）。
  - **N03 新的通過條件**：Ctrl+P 送出無錯誤 **且** 沒有新的截圖檔 **且** 客戶端仍在執行 **且** 帳號欄矩形未變（`AccountBoxUnchanged`）。
  - 開頭說明加上 N03 是反向核對與依據。檔案維持純 ASCII。
  - 回 0 的條件（`$failures.Count -eq 0` 且 N01–N04 都 `Passed`）沒有改；現在 N03 在登入畫面可以為真，所以這個分支可以走到（靜態讀碼）。
- `work/f1118-uia-path/README.md`：「Ctrl+P 內建」那列改為已驗證並附原版地址；N03 步驟改寫為新的通過條件。
- `docs/reference/rea-scripts/t1184/queries.txt`：本輪 IDA 查詢清單（q1–q10）。
- 沒有改引擎、`Program.cs`、`DemoChecks.csproj`、註冊表或 `rebuild-lock.json`。

## 怎麼驗證（本機只做靜態）
- `[System.Management.Automation.Language.Parser]::ParseFile`：`Test-NativeClient.ps1` 0 個錯誤，`UiaGdi.ps1` 0 個錯誤。
- `git check-ignore -v work/f1118-uia-path/Test-NativeClient.ps1`：沒有輸出（exit 1），檔案仍被追蹤。
- `git grep -i -E "WGC|GraphicsCapture|password|passwd"`（`Test-NativeClient.ps1`）：只命中第 4 行原有說明「no password」，沒有 WGC 也沒有帳密字串。
- 腳本位元組檢查：非 ASCII 位元組 0。
- 依本機執行限制，沒有啟動客戶端、遊戲、伺服器、DemoChecks；也沒有執行修改後的腳本。CI 交給 PR。

## 未驗證（待驗證，交 `f1155-retest`）
- 實機：在登入畫面跑新版腳本，N03 應為 `Passed=true`、`NewFiles=[]`、`AccountBoxUnchanged=true`，整支回 0。
- `30·k²` 門檻在高 DPI（#1155 環境 DPI 192，k≈2）下，游標閃爍是否確實低於門檻；若誤判，看 `N03.Diff.BoundingBox` 與 `n03-after.png`。
- 世界內 Ctrl+P 產檔：由 `f1125-retest-3` 的 smoke-m7 SM15 實證。

## 產出路徑
- `docs/reports/issue-1184.md`（本檔）
- `work/f1118-uia-path/Test-NativeClient.ps1`、`work/f1118-uia-path/README.md`
- `docs/reference/rea-scripts/t1184/queries.txt`
- 本機（不進 git）：`scratch/t1184/ida/o1–o10/`

## 給下一棒的提示
- `f1155-retest`：照 #1155 的指令重跑（新的證據目錄），核對 `native-result.json` 的 `N03.Mode='login-negative'`、`NewFiles`、`ClientRunning`、`Diff.ChangedInsideAccountRect` 與 `AllowedInside`，並目視 `n02-after.png`／`n03-after.png` 的帳號欄都是 `abc`。
- followups-4 §1.1 把 `sub_8C2290` 稱為「編輯框焦點版」，實際上是內嵌網頁視窗（`Internet Explorer_Server`）的子類別路徑；之後修文件時可以更正。
