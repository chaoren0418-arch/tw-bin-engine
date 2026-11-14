# Issue #1271：f1247-ime-path：聊天列與建角名稱欄的 IME 組字

結論：線索 1（「WinForms 把焦點視窗的 HIMC 解除關聯」）在 **ClientForm 本身握有焦點**時不成立。WinForms 原始碼保證焦點在 Form 時一定有 input context（行號見下）。靜態分析另外找到一個會直接讓組字失效的程式路徑：`VK_PROCESSKEY` 可能在 `ProcessCmdKey` 被原生面板吃掉，`TranslateMessage` 因此不會執行，`WM_IME_*` 也就不會產生。這張 PR 補上：`VK_PROCESSKEY` 放行、組字窗與候選窗放在輸入列的游標位置（依 viewport 換算，DPI 96／192 都對）、`WM_IME_CHAR` 只交一次、聊天列離開時還原 IME 狀態、IME 診斷 log，以及離線檢查。I01／I02 真正的現象是焦點 HWND 不是 Form、還是測試工具的限制，要靠新 log 在重測時判定（見「待驗證」）。

## 做了什麼
1. 讀 #1247 證據（`client-A/ime-*.json`、`client-D/ime96-diagnostic.json`、`client-A/jobs/04~09-*.ps1`）。工具送鍵用真正的 `SendInput`（`work/f1118-uia-path/UiaGdi.ps1:675`、`:756`），不是 `PostMessage`；open status 是用 `ImmGetDefaultIMEWnd` ＋ `WM_IME_CONTROL`（`IMC_GETOPENSTATUS` 5／`IMC_SETOPENSTATUS` 6）跨程序讀寫的。
2. 靜態核對 dotnet/winforms `release/10.0`（commit `a501342c1d246f58f1100b9b595fed1891496a2d`，原始檔放在 `scratch/issue-1271/winforms/`，不進 git）。
3. 用 pefile／Capstone 唯讀分析原版 BIN 的 IME 處理（腳本：`scratch/issue-1271/imm_scan.py`、`imm_scan2.py`、`imm_dis.py`、`imm_xref.py`，不進 git）。
4. 程式：新增 `engine/source/NativeUiIme.cs`（ClientForm partial）、`engine/source/NativeUi/UiIme.cs`（純規則）；聊天列（`ChatPanel`）和建角名稱欄（`CreateCharPanel`）提供游標座標；`UiClientBridge.InitNativeUi` 加一行 `InitNativeIme()`。
5. 檢查：`engine/_integration/dev/ime-tests/ImeChecks.cs`（`[CheckGroup("ime")]`＋Default，自動發現）；`.github/workflows/build.yml` 加一步跑 `ime` 群組。

## 根因核對

### 1. 焦點 HWND 與 WinForms IME 狀態（線索 1）
- 原生 UI 模式下，鍵盤焦點的設計位置是 **ClientForm 本體**：`ReturnGameFocus()` 做 `ActiveControl=null;Activate();Focus()`（`engine/source/WebPanel.cs:31`），聊天列開啟（`NativeUiCommands.BeginChatInput`，`UiClientBridge.cs`）不會改焦點，字元靠 `KeyPreview=true`（`Program.cs:102`）＋`KeyPress`（`UiClientBridge.cs:26`）進來。引擎沒有任何地方設定 `ImeMode`。
- WinForms（release/10.0，`src/System.Windows.Forms/System/Windows/Forms/`）：
  - `Form.cs:876`：`protected override ImeMode DefaultImeMode => ImeMode.NoControl;`，所以 `ImeSupported`（`Control.Ime.cs:212`，`DefaultImeMode != Disable`）是 true。
  - `Control.Ime.cs:317`／`:333` `UpdateImeContextMode`（每次 WM_SETFOCUS 都由 `Control.cs:12055-12057` `WmSetFocus → WmImeSetFocus`（`Control.Ime.cs:610`）呼叫）：Form 的 `CachedImeMode` 是 NoControl，所以 `newImeContextMode = PropagatingImeMode`。
  - `Control.Ime.cs:293-310`：`PropagatingImeMode` 的 setter 不接受 Disable／NoControl，所以傳下來的值永遠不會是 Disable。
  - `Control.Ime.cs:353` → `ImeContext.SetImeStatus`。只要模式不是 Disable，就會走到 `Enable`（`Control.Ime.cs:708-739`）：沒有 context 就重新關聯（`ImmAssociateContext`），而且會打開 IME。真正會解除關聯的只有 `Disable`（`Control.Ime.cs:684`、`:698` `ImmAssociateContext(handle, NULL)`）。
  - zh-TW（LANGID 0x0404）對到 Chinese 表（`Control.Ime.cs:1048`），所以上面這套機制在本機的注音環境下會運作。
  - **結論（已驗證，靜態）**：焦點在 ClientForm 時，WinForms 不會讓它沒有 HIMC。所以 #1247 那種「`IMC_SETOPENSTATUS` 後 open 仍然是 0」的現象，只可能是三種原因之一：(a) Win32 焦點其實在某個子控制項，而那個控制項的 `DefaultImeMode` 是 Disable（例如按鈕類；WinForms 的 Form 在啟用時，`ActiveControl` 為 null 會選第一個可選的子控制項——這一點是推測，沒有逐行核對 Form.Active）；(b) 跨程序 `WM_IME_CONTROL` 對 TSF（微軟注音，CUAS）沒有作用或讀不準；(c) 兩者都有。靜態分析分不出是哪一種。新的 `ime-focus` log 會記錄焦點 HWND 是不是 Form、有沒有 context、open、conversion，重測時一次就能判定。
- 補強：文字輸入開始時，如果焦點在 ClientForm 的子控制項上（`ContainsFocus&&!Focused`），引擎會把焦點拉回 Form 並記錄 `ime-action focus-form`；如果 Form 沒有 context，會用 `ImmAssociateContextEx(IACE_DEFAULT)` 關聯預設 context（`ime-action Associate`）。這兩個動作只在原生文字輸入開始時做一次。

### 2. 靜態找到的組字阻斷路徑（新發現）
- `ClientForm.ProcessCmdKey`（`Program.cs:120-127`）第一步就呼叫 `NativeUiKey(keyData,…)`，鍵會交給所有原生面板。WinForms 的 `PreProcessMessage` 只要 `ProcessCmdKey` 回傳 true（`Control.cs:8652`），就回報 `MessageProcessed`（`Control.cs:8761`）；接著 `PreTranslateMessage` 回傳 true（`Application.ThreadContext.cs:946`／`:955`），訊息迴圈就**不呼叫 `TranslateMessage`／`DispatchMessage`**（`Application.LightThreadContext.cs:106-108`）。IME 已經處理過的鍵是 `VK_PROCESSKEY`（0xE5），必須經過 `TranslateMessage`（→ `ImmTranslateMessage`）才會產生 `WM_IME_STARTCOMPOSITION`／`COMPOSITION`。
- 例如 `CreateCharPanel.Key` 是模態的：打字狀態下 Ctrl／Alt 組合鍵回傳 true；不在打字狀態時，所有按下的鍵都回傳 true（`CreateCharPanel.cs:606`）。`ChatPanel.Key` 對 `ProcessKey` 回傳 false（`ChatPanel.cs:318`），但 `UiInput.Key` 會把鍵依序交給所有可用的面板，只要其中任何一個吃掉 `ProcessKey`，組字就不會發生。
- 修正：`ClientForm.PreProcessMessage`（`NativeUiIme.cs:97`）在原生文字輸入接收 IME 時，遇到 `VK_PROCESSKEY` 直接回傳 false，不走 `ProcessCmdKey`。其他鍵（W/A/S/D、方向鍵、F 鍵、Enter、Esc……）照原路徑（`UiImeKeys.PassToIme`，有離線檢查）。

### 3. 原版 BIN（TW1608262200.bin，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，ImageBase 0x400000）
| 項目 | VA | RVA | File Offset | 內容 |
|---|---|---|---|---|
| imm32 IAT | 0x114B0FC–0x114B130 | 0xD4B0FC– | 0xD49CFC– | 匯入 ImmGetContext、ReleaseContext、Get/SetConversionStatus、GetProperty、GetCompositionStringA/W、GetOpenStatus、GetCandidateListW、SetCompositionWindow、SetCandidateWindow、SetCompositionStringA、GetIMEFileNameW、IsIME。**沒有** ImmAssociateContext(Ex)、ImmSetOpenStatus、ImmNotifyIME |
| imm32 jmp stub 表 | 0xF81E92–0xF81EE0 | 0xB81E92– | 0xB81292– | `jmp [IAT]` |
| 編輯框 wndproc | 0xA38B80 | 0x638B80 | 0x637F80 | `GetWindowLongA(hwnd,0)` 取物件；`WM_IME_STARTCOMPOSITION`（cmp 0x10D @0xA38BC2）→ 0xA38ED6：旗標 `[obj+0x70]&2` 時交給 DefWindowProcA（系統組字 UI），否則設 `[obj+0x2F4]|=1`，自己畫 |
| 組字窗位置 | 0xA37E30（呼叫點 0xA37E79，FO 0x637279） | 0x637E30 | 0x637230 | `ImmSetCompositionWindow`：`dwStyle=2`（CFS_POINT），`ptCurrentPos=([obj+0x3C],[obj+0x40])`（編輯框游標）；由 0xA332BA 呼叫 |
| 候選窗位置 | 0xA35950（呼叫點 0xA359EE，FO 0x634DEE） | 0x635950 | 0x634D50 | `WM_IME_NOTIFY`：`IMN_OPENCANDIDATE`(5) 且 lParam&1 → `ImmSetCandidateWindow`：`dwStyle=0x40`（CFS_CANDIDATEPOS），點在 `(x, y+[obj+0x48])`（游標下方一行）；只在 `[0x13CE82C]<6`（推測：OS 主版本 < 6）時才做；IMN_SETOPENSTATUS(8) → 0xA37F20 |
| 模擬按鍵輔助 | 0x8886E0（FO 0x487AE0） | 0x4886E0 | 0x487AE0 | `ImmGetConversionStatus`：如果是 NATIVE（bit0）且沒有 bit 0x10，就先 `ImmSetConversionStatus(0,0)`，再還原原值，然後才 `keybd_event` |
- 原版的文字輸入是**獨立的編輯框 HWND**（物件 `+0x2D8` 是 hwnd）。組字窗放在游標，候選窗放在游標下方，用的是系統 IME UI（除非設了自己畫的旗標）。BIN 裡沒有解除 context 或關閉 IME 的呼叫，遊戲熱鍵怎麼避開 IME 沒有直接證據；0x8886E0 會先把 native 模式切回英數再送鍵，這是旁證（推測）。

## 修改前後
| 項目 | 修改前 | 修改後 |
|---|---|---|
| `VK_PROCESSKEY`（原生文字輸入時） | 經過 `ProcessCmdKey` → `NativeUiKey`，任何一個面板回傳 true 就不會 `TranslateMessage` | `PreProcessMessage` 放行，一定會 `TranslateMessage` |
| 組字窗／候選窗 | 引擎從不設定，系統放在預設位置 | `WM_IME_STARTCOMPOSITION`、`WM_IME_COMPOSITION`、`IMN_OPENCANDIDATE`／`IMN_CHANGECANDIDATE` 時：組字 `CFS_POINT` 放在游標，候選 `CFS_EXCLUDE` 避開輸入列（類似原版 CFS_POINT／游標下方）；組字字串仍由系統 IME UI 畫 |
| 位置換算 | — | 邏輯 800×600 → `UiPresenter` viewport（PerMonitorV2＋AutoScaleMode.None，client 是實體像素），夾在 client 範圍內；DPI 96（s=1）、192（s=2）、置中黑邊、fill 小數比例都有檢查 |
| IME 結果字 | WinForms：`WM_IME_CHAR` → KeyPress（`ImeWmCharsToIgnore` 防重） | 原生 UI 啟用時，`WM_IME_CHAR` 直接交 `NativeUiChar` 一次，**不交** DefWindowProc（所以不會再產生 WM_CHAR）；如果 IME 另外送了同字的 WM_CHAR，在下一個真正的按鍵之前丟掉一次（`UiImeCharGate`） |
| 離開文字輸入（回到遊戲） | — | 取消沒打完的組字；conversion mode 和 open status 還原成進入前的值（原本是英數或關閉的，就還原成英數或關閉，熱鍵不會被 IME 吃掉）；引擎自己關聯的 context 解除。遊戲焦點下什麼都不動 |
| 焦點 | — | 文字輸入開始時，焦點如果在 Form 的子控制項上，就拉回 Form（只做一次，有 log） |
| `--classic-ui`／nativeUi:false | — | `InitNativeIme` 不會掛上；`WndProc`／`PreProcessMessage` 在 `!NativeUiActive` 時直接交給 base，TextBox 路徑不變 |

## IME log 欄位（`web-events.jsonl`，透過既有的 `WebLog`；只記長度和次數，不記文字）
| kind | 時機 | 欄位 |
|---|---|---|
| `ime-focus` | 文字輸入 enter／leave、Form 的 WM_SETFOCUS／WM_KILLFOCUS | `phase`(enter/leave/set-focus/kill-focus)、`why`、`form`（Form HWND）、`focus`（`GetFocus()` HWND）、`FocusIsForm`、`focusControl`（白名單類別標籤）、`context`（ImmGetContext(Form) 是否非 0）、`open`、`conversion`（hex）、`layout`（HKL）、`session`、`target`（面板 Id）、`imeWindow`（ImmGetDefaultIMEWnd） |
| `ime-action` | 引擎改動 IME 狀態時 | `action`（focus-form／Associate／RestoreDetached／Close／CancelComposition／RestoreConversion）、`why`、`ok` |
| `ime-start` | 收到 WM_IME_STARTCOMPOSITION | `target`、`dpi`（DeviceDpi）、`client`、`composition`（client 像素點）、`exclude`（候選避開矩形） |
| `ime-comp` | 收到 WM_IME_COMPOSITION | `flags`（lParam hex）、`compLength`（GCS_COMPSTR 字數，-1＝這次沒有）、`context` |
| `ime-result` | WM_IME_COMPOSITION 帶 GCS_RESULTSTR | `chars`（結果字數） |
| `ime-char` | 收到 WM_IME_CHAR，或丟掉 echo 的 WM_CHAR | `source`、`used`（是否有原生文字輸入接收）／`echoDropped` |
| `ime-end` | 收到 WM_IME_ENDCOMPOSITION | `delivered`（累計交給 Char() 的 IME 字數）、`echoDropped` |
| `ime-status` | WM_IME_NOTIFY：開啟狀態、conversion、候選窗開／關 | `notify`、`context`、`open`、`conversion` |
每一行都是收到真實訊息時才寫，沒有合成。判讀方式：`ime-start`→`ime-comp`→`ime-result chars=N`→`ime-char used=true`×N→`ime-end delivered` 增加 N，就是組字真的發生過、而且字進了輸入列的客觀證據。

## 怎麼驗證
- 已驗證：
  - `dotnet build engine/source/OfflineClient.csproj -c Release`：0 個錯誤，新檔案沒有新的警告。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 個錯誤。
  - 依本機限制，沒有執行任何編譯出來的程式；`ime` 檢查群組交給 PR 的 CI（workflow 新增「Native UI IME placement…」步驟）。
  - WinForms 行號（上表）：直接讀 pinned commit 的原始檔確認。
  - BIN 地址：pefile／Capstone 唯讀反組譯確認。
- 離線檢查內容（`ImeChecks.cs`，CI 跑）：游標 → 組字位置換算（DPI 96、192、置中黑邊、fill 1.5、右下界、負值、行底超出、空 client、高度 0）；`中文` 兩個 `WM_IME_CHAR` 各進 `Char()` 一次、echo WM_CHAR 各丟一次、真正的 WM_KEYDOWN 會清掉 echo、VK_PROCESSKEY 不清、沒人接收的字不留 echo；`中文` 的 CP950 是 `A4A4A4E5`（和 board C141 向量同樣的位元組）；遊戲熱鍵（W/A/S/D、方向鍵、F1、F12、Space、Enter、Esc、數字、M、Tab、Back）不放行給 IME；session 進入／離開的動作；目標面板查找（第一個可用、跳過不可用、丟例外的 caret 會略過）；`ChatPanel`、`CreateCharPanel` 都有實作 `IUiImeCaret`。

## 推測
- 焦點落在子控制項：Form 啟用時，`ActiveControl==null` 可能選第一個可選的子控制項。這一點沒有逐行核對 Form.Active，要靠 `ime-focus` 的 `FocusIsForm` 實測。
- 離開聊天列時還原 open／conversion：原版 BIN 沒有對應的 API 呼叫，原版靠獨立的編輯框 HWND。這是引擎自己的作法，用來確保「回到遊戲時 IME 不吃熱鍵」。
- 原版候選窗定位只在 OS 主版本 < 6 時做（`[0x13CE82C]<6`）。`0x13CE82C` 是 OS 版本，這一點是推測。引擎在所有版本都設定候選窗位置。
- 同時打開多個原生文字輸入時，IME 跟著 `HitOrder` 第一個有游標的面板。

## 待驗證（交 `f1247-ime-retest`）
- I01：P24Lab 42061，原生 UI 進世界 → Enter 開聊天列 → 切換到注音 → 打 `su3` → 空白鍵或 Down：要看到組字列在聊天輸入列游標附近、候選窗在輸入列上方或下方而且不蓋住輸入列；選字後輸入列出現中文，送出後聊天回顯正確（CP950）。log 要有 `ime-focus phase=enter FocusIsForm=true context=true`、`ime-start`、`ime-comp`、`ime-result chars=1`、`ime-char used=true`、`ime-end`。
- I02：建角能力頁 → 點名稱欄 → 同樣操作 → 名稱欄出現中文，log 的 `target=CreateUI`。
- DPI 192 下同樣兩項：`ime-start` 的 `dpi=192`，`composition` 落在 2 倍座標上。
- 回到遊戲後，W/A/S/D、方向鍵、F 鍵正常（`ime-action RestoreConversion／Close` 視情況出現）。
- C01 回歸：DPI192 清空＋聊天送出（C136、C129）。
- 如果重測 log 顯示 `FocusIsForm=false`，看 `focusControl`；如果 `context=true open=true`、但 `SendInput` 仍然沒有產生 `ime-start`，就是工具端限制，標 `needs:user` 交真機。

## 第 5 點：如果原因在工具端，工具還能試的方法（不改系統設定、不注入產品）
1. 先讀引擎的 `ime-focus` log，取代跨程序的 `WM_IME_CONTROL` 讀值：引擎在自己的執行緒讀到的才是準的。
2. 用 `GetGUIThreadInfo(targetThread)` 讀 `hwndFocus`，和 log 的 `form`／`focus` 對照（唯讀）。
3. 微軟注音（TSF）的中英切換預設是 Shift，開關 IME 是 Ctrl+Space（依使用者設定而定）。兩種都試，而且 Shift 要單獨按放、不要夾在其他鍵中間（`New-ClientKeyInput 0x10` 按下／放開之間不要有其他鍵）；切換後等 `ime-status notify=conversion` 出現再打字。
4. 不要再用 `IMC_SETOPENSTATUS` 跨程序強制打開：#1247 已記錄它沒有效果；新版注音的狀態以 TSF compartment 為準。
5. 自動化還是叫不出候選時，交使用者真機手動輸入（needs:user），以引擎 log 當客觀證據。

## 產出
- `engine/source/NativeUiIme.cs`（新，ClientForm partial：WndProc、PreProcessMessage、Imm P/Invoke、log）
- `engine/source/NativeUi/UiIme.cs`（新，純規則：`IUiImeCaret`、`UiImePlacement`、`UiImeKeys`、`UiImeCharGate`、`UiImeSession`、`UiImeTargets`）
- `engine/source/NativeUi/ChatPanel.cs`、`engine/source/NativeUi/CreateCharPanel.cs`（游標提供端）
- `engine/source/NativeUi/UiClientBridge.cs`（`InitNativeIme()` 一行、註解）
- `engine/_integration/dev/ime-tests/ImeChecks.cs`（新）
- `.github/workflows/build.yml`（新增一步 `ime` 群組）
- `docs/reports/issue-1271.md`（本檔）
- 沒有改 `Program.cs`、`DemoChecks.csproj`、各註冊表、`rebuild-lock.json`。

## 給下一棒的提示
- `f1247-ime-retest` 要等本 PR 和 PR #1268 都合進 main 才能開跑。log 在 `<stage>/runs/interactive-<pid>/web-events.jsonl`（有 `--evidence` 時在 evidence 目錄），`grep '"kind":"ime-'`。
- 第一個要看的是 `ime-focus phase=enter` 的 `FocusIsForm`／`context`／`open`／`conversion`，它會直接分辨出是程式端問題（焦點在子控制項、沒有 context）還是工具端問題。
- 其他原生文字輸入（信件、公告板發文、盟屋拍賣、私人商店等）還沒有實作 `IUiImeCaret`：這些面板不會啟動 IME session，所以 `VK_PROCESSKEY` 的放行、組字窗定位、離開時的還原都不會作用（組字可能和修改前一樣被 `ProcessCmdKey` 擋住）。組字結果如果真的產生，`WM_IME_CHAR` 仍然會交給第一個接收的 `IUiTextInput`。要支援的話，照 `ChatPanel` 的做法補 `ImeCaret` 就好，每個面板只要幾行。
