# Issue #960 — T418：R11-D 選角空格／建角雙擊（code）

依據：`docs/reference/gap-matrix.md` §15.13.5 的 4:SelectUI、4:CreateUI 列與 §15.13.6 的 R11-D 列；`docs/reports/issue-913.md` 第 144–145、158 行。
裁定（#952 第 1 點，沿用 #880）：照原版做，不算引擎取捨。分支：`task/960`。

## 1. 做了什麼

### 1.1 選角可選空格（`NativeUi/SelectCharPanel.cs`）

TW16 的選取 `this+344` 是**列號**，不是角色，所以空格也能選。#913 的 IDA 輸出已不在，本張重新反編譯查證（§2）。

| 動作 | TW16 | 引擎 |
|---|---|---|
| 點空格的 `Create_Button`（514） | `sub_EEC3B0`：列不同就 `sub_EF0060` 選它，**不開建角** | 選該列；不開建角（以前單擊就開） |
| 雙擊空格（515） | `sub_EEC4A0` 開建角 | 選該列並開建角（原生建角畫面，不可用時照舊開 WinForms 對話框）；`CanCreate` 為 false 時不開 |
| 上／下鍵（4134／4136） | `sub_EEDF00` ±1，`sub_EEEE60` 要 `< 格數`（另有 `< 8`）；頁＝列÷4 | 在 0 … 格數−1 之間移動，角色與空格都算；鎖住的列不選 |
| Enter（13） | 選到空格 → `sub_EEC4A0` 開建角；角色 → 進遊戲 | 同左 |
| 開畫面沒有選取 | `sub_EF0490`：選第 0 列 | 沒有角色時選第 0 列（空格） |
| 選到空格時的資訊欄 | `sub_EECB10`：六個標籤設成 `""`（`0x11E3C63`–`0x11E3C77` 都是空字串） | 資訊欄不畫（看起來一樣） |
| 選到空格時按 Ok／Delete | `sub_EEFDD0`→`sub_EED8A0`、`sub_EEC6B0`→`sub_EEC6D0`：職業 −1，什麼都不做 | 兩鈕停用（看起來一樣） |
| `Create_Button` 圖號 | `sub_EF13F0`：p＝XML PushSurf；選取列：一般＝亮＝p；否則一般 p−2、亮 p−1；按下一律 p | 同左（`CreateSurfs`）；XML 沒有 PushSurf 時照用 XML 圖；缺圖不畫 |

- 新增 `SelectedEmpty`（選到的空格列）與 `SelectedRow`（等於 TW16 的 `this+344`）。角色仍用名字記，避免清單變動時選錯人；空格用列號記。之後如果有角色補進那一列，選取就換成那個角色（TW16 的列號本來就指向它）；那一列變成鎖住時，改選第一個角色。
- `CharUiPiece` 加 `PushImage`（Button 的 XML PushSurf 參照），`CharUiBuilder` 建 Button 時填入。只有選角的 `Create_Button` 用到。
- `Create_Button` 只要畫面可操作（`Ready`）就啟用，因為單擊只是選取。開建角另外檢查 `CanCreate`。

### 1.2 建角職業鈕雙擊（`NativeUi/CreateCharPanel.cs`）

`sub_EE3410`（職業鈕）：
- 514：職業不同就 `sub_EE5F50(職業, 1)`（淡入）。這是既有行為，沒改。
- 515：職業不同就 `sub_EE5F50(職業, 0)`（**不淡入**），接著呼叫 vtable +196 帶 2，也就是**進能力頁**。職業相同也會進能力頁。
- 引擎做法：`Mouse` 把 `e.Clicks` 傳給 `Fire`。`clicks>=2` 時：
  - 職業不同：換職業、重設能力、`SetClassPicture()`（Back 模式 0、新基底 png、幀 0）；
  - 然後一律 `EnterStatScene()`。
- 真的雙擊時，第一次放開（514）已經換了職業並開始淡入，515 只負責換頁。淡入計時器在能力頁還會繼續數，但看不到；按「上一步」回職業頁時會重設。
- 雙擊另一個職業時，若淡入正在跑，引擎會停掉它。`sub_EE5F50` 帶 a3＝0 時會不會取消計時器沒查，這點是**推測**。

### 1.3 測試

- 新檔 `NativeUi/CharScreenFollowup3Tests.cs`：群組 `char-screen-followup3`，用 `[WorldModuleTest]` 自動發現。版面、圖片、時鐘全是記憶體內合成資料。
  - 選角：
    - `CreateSurfs` 公式；
    - 單擊空格只選不開；
    - 資訊欄、Ok／Delete 在空格時的狀態；
    - 點回角色；
    - 第 1 頁的欄位＝第 4＋n 列；
    - 鎖住的列不選；
    - 四種圖號狀態（一般、亮、按下、選取）與缺圖不畫；
    - 上／下鍵跨角色與空格、到格數上限停住、頁碼跟著走；
    - 沒有角色時選第 0 列；
    - 雙擊與 Enter 開建角（WinForms 備援與原生畫面兩種）；
    - `CanCreate` 為 false 時不開；忙碌時不選；
    - 清單更新時選取的去向，換代時重設。
  - 建角：
    - 雙擊直接進能力頁、不淡入、能力重設；
    - 雙擊目前的職業；
    - 真實雙擊順序（514 淡入，515 換頁）；
    - 單擊維持原樣；
    - 淡入途中雙擊另一個職業。
- **`NativeUi/SelectCharPanelTests.cs` 有改，超出任務列的檔案範圍**（#913 也這樣處理過）。原因：`SelectActions` 裡兩處寫的是「單擊 `Create_Button` 就開建角」，這正是本張依原版改掉的行為。這兩處改成先單擊、再雙擊，其他斷言都沒動。
- 沒改：`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`、`docs/REPORT.md`。`--classic-ui` 不經過這兩個面板，WinForms 入口不變。

## 2. 依據（IDA 7.7 唯讀）

- IDB：`P01-分析基準/ida/03-ascii-isolated/TW1608262200.idb` 複製到 `scratch/t960/ida/tw16.idb`，SHA-256 `3A0BF206…BC17E82F`，與 #913 相同。
- 執行器：`docs/reference/rea-scripts/t956/run_tw16.ps1` 加 `t742/ida_t742.py`。
  - 每輪只掛 `subst K:`，結束就解除；開始前 `subst` 沒有輸出。
  - 只做查詢，沒有建函式，也沒有改名。
- 查詢清單：`docs/reference/rea-scripts/t960/q1.txt`–`q5.txt`。反編譯輸出在 `scratch/t960/ida/out1`–`out5`，不進 git。

| 函式（VA） | 用到的事實 |
|---|---|
| `0xEEE3DB`–`0xEEE42E`（`sub_EEE280` 的登記） | `SelectChar_SelectWin`＝`sub_EEFF10`，`CreateChar_SelectWin`＝`sub_EEC3B0`，`DeleteChar_SelectWin`＝`sub_EEC6B0`，`SlotLock_SelectWin`＝`sub_EF1230`；`Ok_SelectWin`＝`sub_EEFDD0` |
| `sub_EEC3B0` | 列＝欄位＋4×頁；514：列不同就 `sub_EF0060(列)`；515：`sub_EEC4A0(列)` |
| `sub_EEC4A0` | 列 ≥ 格數時跳訊息 1450／1816（空格不會走到）；否則記 `this[87]`＝列，開建角 |
| `sub_EEDF00` | 13：選取 ≠ −1 時，職業 −1 → `sub_EEC4A0`，否則 `sub_EED8A0`；4134／4136：±1（≥0／<8）→ `sub_EEEE60` |
| `sub_EEEE60` | `0 ≤ n < byte_1542A97`（格數）才選；頁＝n/4 |
| `sub_EF0060` | `this+344`＝n，然後重畫列、動畫、`sub_EECB10` |
| `sub_EF0490` | 開畫面：`this+344`＝−1 時設 0；頁＝選取÷列數 |
| `sub_EF13F0` | 空格（職業 −1）：顯示 `Create_Button`；p＝`sub_AFFF30(sub_AFFB20)`（PushSurf）；選取列一般／亮＝p，否則 p−2／p−1 |
| `sub_EECB10` | 選取 < 0 或職業 −1：六個資訊標籤設成空字串 |
| `sub_EED8A0`、`sub_EEC6D0` | 進遊戲、刪除：職業 −1 時什麼都不做 |
| `sub_EE3410` | 514：值不同就 `sub_EE5F50(c, 1)`；515：值不同就 `sub_EE5F50(c, 0)`，然後 vtable +196 帶 2 |

## 3. 資源

本張沒有新增要讀的資源。`Create_Button` 的圖號由 XML PushSurf 推出（p−2／p−1／p），一律經 `SurfCatalog` 讀（先本機，再參考客戶端），缺圖就不畫。沒有複製任何資源，也沒有宣稱哪個資源不存在，所以這次沒有用 PakViewer 查。

## 4. 怎麼驗證

- restore：`dotnet restore engine/source/OfflineClient.csproj --source <主目錄 engine\vendor>`，套件放在 `scratch/t960/nuget`。
- compile：`dotnet msbuild engine/source/OfflineClient.csproj -t:ResolveReferences;CoreCompile -p:Configuration=Release -p:EnableWindowsTargeting=true`，結果 **0 錯誤**，本次改的檔案沒有警告。
- `py -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（errors 0）。
- 依本機限制，單元測試不在本機跑。`char-screen-followup3`、`select-char-ui`、`create-char-ui`、`char-screen-followup`、`char-screen-followup2` 都**以 PR 的 CI 結果為準**。

## 5. 未驗證什麼（待驗證，交給 Codex／dot，M4）

- 真機畫面：
  - 選到空格時 `Create_Button` 的樣子（kor25 的 PushSurf 減 2／減 1 對不對得上）；
  - 選到空格時資訊欄和角色動畫的樣子。動畫 `Character_Ani` 引擎本來就沒做。
- 雙擊的手感：Windows 雙擊時，第一次放開就已經淡入、換職業，第二下才換頁。
- 推測：
  - `sub_EE5F50` 帶 a3＝0 時，會不會取消正在跑的淡入計時器；
  - 刪除角色後 TW16 的 `this+344` 怎麼變。引擎是：那一列空出來時選第一個角色，沒有角色時選第 0 列。
- 沒做：
  - `sub_EEC4A0` 的 1450／1816 訊息。條件是列 ≥ 格數，選空格時不會發生；
  - `sub_EEFF10` 515 在另一個角色上「選取並進遊戲」。引擎維持「雙擊目前選取的角色才進」，真實雙擊時第一下已經選好，結果一樣。

## 6. 產出路徑

- `engine/source/NativeUi/SelectCharPanel.cs`、`engine/source/NativeUi/CreateCharPanel.cs`
- `engine/source/NativeUi/CharScreenFollowup3Tests.cs`（新）
- `engine/source/NativeUi/SelectCharPanelTests.cs`（兩處舊斷言，見 §1.3）
- `docs/reference/rea-scripts/t960/q1.txt`–`q5.txt`（IDA 查詢清單）
- `docs/reports/issue-960.md`（本檔）

## 7. 給下一棒的提示

- 真機看 UI 紀錄：
  - `select-char-ui 缺圖不畫：surf …` 會列出空格鈕推出來的圖號缺哪張（p−2／p−1）。
- gap-matrix 的 4:SelectUI、4:CreateUI 兩列在本張合併後可以改成「已有」，只剩真機外觀（M4）。要由主控裁定，本張沒有改 gap-matrix。
