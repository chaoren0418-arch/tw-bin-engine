# Issue #913 — T398：R10-N 選角／建角收尾（code）

依據：`docs/reference/gap-matrix.md` §15.12.6 的 R10-N 列與 §15.12 的 4:SelectUI、4:CreateUI 列；`docs/reports/issue-859.md` §1.2、§2。
分支：`task/913`。裁定（#880 第 1 點）：SelectUI、CreateUI 的小差異照原版做。

## 1. 做了什麼

### 1.1 選角：上／下鈕整頁翻頁（`NativeUi/SelectCharPanel.cs`）

- `this+352` 是**頁碼**。
  - `Up_Button`（`sub_EF13A0`）：頁碼 > 0 時減 1。
  - `Down_Button`（`sub_EECF90`）：頁碼 < 1 時加 1。
  - 兩個都在改完頁碼後呼叫 `sub_EF13F0` 重畫。
  - 一頁的列數是 `BigSlot_List` 的列數（kor25 是 4），第 r 列＝欄位序號＋列數×頁碼。
  - TW16 寫死只有第 0、1 兩頁。引擎照做；只有伺服器送來超過兩頁的列時才多開頁（3.81 是 8 格＝2×4，實際上不會發生）。
- **換圖**（`sub_EF13F0` 每次重畫都設定，蓋掉 XML 的 BaseSurf／HighlightSurf／PushSurf）：

  | 頁 | Up（一般／亮／按下） | Down（一般／亮／按下） |
  |---|---|---|
  | 0 | 6411／6411／6411 | 6413／6414／6414 |
  | ≥ 1 | 6411／6412／6412 | 6413／6413／6413 |

  （三態對應 `sub_AFE580`／`sub_AFF270`／`sub_AFFB20` → `sub_B0D810`）
- **鍵盤**（`sub_EEDF00`）：
  - 上／下方向鍵移動選取，頁碼跟著選取走（`sub_EEEE60`：頁＝序號÷4）。
  - PageUp／PageDown（`0x1021`／`0x1022`）的翻頁方式跟按鈕相同。
  - 引擎仍然只能選到角色；TW16 也能選空格。
- 滑鼠滾輪改成一次翻一頁。這是**推測**：CSelectUI 裡沒找到滾輪處理。
- 點欄位時選的列＝欄位＋4×頁（`sub_EEC3B0`）。這跟引擎的 `Scroll＋Slot` 一樣，因為現在 `Scroll` 一定是「頁×列數」。

### 1.2 建角：職業頁的圖片動畫（`NativeUi/CreateCharPanel.cs`）

IDA 查到的做法跟 gap-matrix 的寫法有一點不同：**一直在動的是 `Back_Image`（18 張 png 循環）**；`Front_Image` 只在換職業或換性別時用來交叉淡入。

- **圖片種類**：兩張都是 **Png**，不是 surf。
  - `XmlImage` 的 `this[83]`＝png 編號，`this[81]`＝png 模式，`this[82]`＝模式 2 的濃度（FU13§5；`sub_B07F90` 解析 `Png="id;mode;level"`）。
  - 基底編號＝`sub_EE3F40`＝**1573＋36×職業＋18×性別**（職業 7 用 1862／1880）。
- **循環**（`sub_EE3A00`，場景每幀更新）：
  - 條件：在職業場景、`Back_Image` 是模式 0、距離上次換圖超過 200 ms。
  - 動作：幀號＋1（到 18 歸 0），`Back_Image` png＝基底＋幀號。
  - 上次換圖的時間在重設後是 0，所以重設後的第一次更新會馬上換圖。
- **進職業場景**（`sub_EE6DD0` 場景 1 → `sub_EE5F50(職業, 0)`／`sub_EE6450(性別, 0)`）：`Back_Image` 模式 0、png＝基底，幀號與時間歸 0，`Front_Image` 隱藏，不淡入。
  - 適用時機：開建角畫面，以及從能力頁按「上一步」回來。
- **換職業**（`sub_EE3410` → `sub_EE5F50(…, 1)`）或**換性別**（`sub_EE3CF0`／`sub_EE47B0` → `sub_EE6450(…, 1)`），只在值真的改變時才做：
  1. 取消計時器 `sub_EE3BE0`，計數＝31。
  2. `Back_Image` 改成模式 2、濃度 31，png 保留目前那一張。
  3. `Front_Image` 改成模式 2、png＝新基底、濃度 0，並顯示。
  4. 每 19 ms 呼叫一次 `sub_EE3BE0`（`sub_A231C0` 是單次計時器，由回呼自己重排，所以下一次是「現在＋19」）：
     - 計數減 1；
     - 計數 > 0：Back 濃度＝計數，Front 濃度＝31−計數；
     - 計數到 0：Back 回模式 0，png 改成 Front 的那張，幀號與時間歸 0，Front 隱藏。
- **畫法**：模式 2 由 `sub_7D7740` 帶濃度畫（`XmlImage` 畫圖 `sub_AFB910` 的 case 2）。
  - 引擎用「濃度÷31」當不透明度。這是**推測**：開始淡入時舊圖是 31，淡入結束交回模式 0（不透明）。
  - 同一幀裡循環和計時器誰先跑沒查到，引擎是先循環再計時器（**推測**）。
- **圖從哪裡來**：0-客戶端沒有這些 png（見 §3），參考客戶端 kor25／ly 有。
  - 執行期經 `OptionPngSource` 讀（先本機，再參考客戶端，記 SHA-256），沒有複製進 repo。
  - 缺圖就不畫，同一張只查一次並記一行 log。
- 建版面時，`CharUiBuilder` 加了選填參數 `runtime`：職業頁沒有圖號的 `Front_Image`／`Back_Image` 會建出來（沒有 surface，執行期才給圖），不再列進 Skipped。只有 `CreateCharPanel` 有傳這個參數，選角畫面不受影響。
- 關閉畫面時取消淡入（`sub_EE3AE0`）。

### 1.3 測試

- 新檔 `NativeUi/CharScreenFollowup2Tests.cs`：群組 `char-screen-followup2`，用 `[WorldModuleTest]` 自動發現。全部是記憶體內的合成版面、圖片與時鐘。
  - 選角：
    - 6411–6414 的公式；
    - 翻頁與上下限，第 1 頁點欄位選到第 4＋n 列；
    - 換圖蓋過 XML 圖（一般／亮／按下，兩頁都測）；
    - 缺 6412 時不畫；
    - 方向鍵讓頁碼跟著選取走，PageUp／PageDown，滾輪；
    - 只有 4 格時第 1 頁全部鎖住；10 列時引擎多開第 3 頁。
  - 建角：
    - png 公式；
    - 進場、第一次更新馬上換圖、`> 200 ms` 的邊界、18 張循環、能力頁停止循環、「上一步」回來重設；
    - 淡入 31 步：19 ms 邊界、每次觸發只走一步、淡入中循環暫停、結束時交棒；
    - 換性別、同值不重來、淡入途中換職業會從 31 重來；
    - 關閉時取消；
    - 畫面像素：不透明 → 中途混色 → 結束換成新圖；缺圖不畫且只查一次；沒有 png 來源時不畫。
- **`NativeUi/SelectCharPanelTests.cs` 有改，超出任務列的檔案範圍**。原因：下面三處斷言寫的正是本張要改掉的舊行為，只能跟著改（#859 也是同樣處理）：
  1. 合成圖加入 6411、6413。Up／Down 改用 6411–6414 後，舊的合成圖號 411／413 不會再被畫出來。
  2. 「Down 一次捲一列」改成「Down 翻一頁」，並改成「停在 TW16 最後一頁」「Up 翻回第 0 頁」。
  3. 建角「`Front_Image`／`Back_Image` 沒有圖號、不建」改成「有建、執行期才給圖」。

  其他斷言沒動。
- 沒改：`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`、`docs/REPORT.md`。`--classic-ui` 不經過這兩個面板，WinForms 入口不變。

## 2. 依據（IDA 7.7 唯讀）

- IDB：P01 的 `03-ascii-isolated/TW1608262200.idb`，複製到 `scratch/t913/ida/tw16.idb`（SHA-256 `3A0BF206…BC17E82F`，與 FU5–FU13 相同）。
- 工具：`idat -A` 批次跑 `docs/reference/rea-scripts/t898/ida_t898.py`，只做查詢，沒有在副本上建函式或改名。
- 環境：`subst Q:` 只在每輪執行期間掛上，結束就解除（開始前 `subst` 沒有輸出，結束後也沒有）；IDA 的 `IDAUSR`／`APPDATA`／`TEMP` 都指到 scratch。
- 查詢清單與反編譯輸出：`scratch/t913/q1.txt`–`q8.txt`、`out1`–`out8`（不進 git）。

| 函式（VA） | 用到的事實 |
|---|---|
| `sub_EF13F0` | BigSlot 重畫：列＝i＋列數×`this+352`；Up／Down 依頁碼設定 6411–6414（見 §1.1 表） |
| `sub_EF13A0`／`sub_EECF90` | `UpSlot_SelectWin`／`DownSlot_SelectWin`（514）：頁碼 >0 減 1／<1 加 1，然後 `sub_EF13F0` |
| `sub_EEDF00` | 鍵盤：4134／4136 方向鍵移動選取（< 8）；4129／4130 翻頁；13 進入 |
| `sub_EEEE60` | 選取第 n 列時頁＝n/4，頁碼變了才重畫 |
| `sub_EEC3B0`／`sub_EEFF10`／`sub_EF1230` | 點欄位：序號＋4×頁 |
| `sub_EF0490` | 開畫面時頁＝選取÷列數（`0xEF060D`） |
| `sub_EE3F40` | 1573＋36c＋18s；職業 7：1862／1880 |
| `sub_EE3A00` | 職業頁每幀：`Back_Image` 模式 0 時，超過 200 ms 換下一張（18 張） |
| `sub_EE5F50`／`sub_EE6450` | 換職業／性別：a3＝1 淡入（計數 31、Back 模式 2 濃度 31、Front 模式 2 新圖濃度 0、19 ms 計時器）；a3＝0 直接換 |
| `sub_EE3BE0` | 計時回呼：計數減 1；>0 時 Back＝計數、Front＝31−計數；0 時 Back 接手 Front 的圖、Front 隱藏 |
| `sub_EE3410`／`sub_EE3CF0`／`sub_EE47B0` | 職業鈕 514（值改變才淡入）／515（不淡入，直接進能力頁）；性別鈕 |
| `sub_EE6DD0`／`sub_EE3AE0` | 進場景 1 的設定；關閉時取消計時器 |
| `sub_B07F90`／`sub_B0D100`／`sub_B0D0E0`／`sub_B0D0C0`／`sub_AFFA10`／`sub_AFF9D0` | Png 欄位 `[81]`／`[82]`／`[83]` 的解析、設定與讀取 |
| `sub_AFB910` | `XmlImage` 依 png 模式畫圖；模式 2 走 `sub_7D7740`，帶 `[82]` |
| `sub_A231C0`／`sub_A23B30` | 單次計時器（現在＋毫秒）／依回呼取消 |

物件位移以反組譯確認：`[this+0x168]`＝`this[90]`＝`Front_Image`，`[this+0x16C]`＝`this[91]`＝`Back_Image`（`0xEE3C1D`–`0xEE3CC0`、`0xEE628C`–`0xEE6368`）。

## 3. 資源查詢（PakViewer CLI，唯讀）

- 用 `docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt` 重新列出：
  - **0-客戶端全部 24 個 idx**：`Sprite`、`Sprite00`–`Sprite15`、`Text`、`Tile`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`。`launcher\TW13081901.pak` 沒有 idx，PakViewer 開不了。
  - 參考客戶端 kor25、t89、ly 的 `Tile`、`Text`、`Sprite`、`Sprite00`–`Sprite15`。
  - 清單放在 `scratch/t913/pak-index/`，比對腳本是 `scratch/t913/find.py`，結果在 `scratch/t913/png-query.txt`。
- **6411–6414**（`.img`）：0-客戶端 `Sprite07`／`08`／`09`／`10` 都有（kor25、ly、t89 也在同樣位置），所以按鈕用本機圖。
- **職業頁 png**（288 個編號：職業 0–6 × 性別 2 × 18 張＝1573–1824，加上職業 7 的 1862–1879、1880–1897）：
  - 0-客戶端：只有 `1888.png`–`1891.png`（`Sprite12`／`13`／`05`／`06`），屬於職業 7 女性，3.81 用不到；其他 284 個都沒有。
  - kor25、ly：288 個都有（分散在 `Sprite00`–`Sprite15`）。
  - t89：只有 74 個。
  - 執行期經 `OptionPngSource` 讀參考客戶端（T330 的路徑）。

## 4. 怎麼驗證

- 編譯：
  - restore：`dotnet restore engine/source/OfflineClient.csproj --source <主目錄 engine\vendor>`，套件放在 `scratch/t913/nuget`。
  - compile：`dotnet msbuild engine/source/OfflineClient.csproj -t:ResolveReferences;CoreCompile -p:Configuration=Release -p:EnableWindowsTargeting=true`。
  - 結果：**0 錯誤**，本次改的檔案沒有警告。
- `py -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（errors 0）。
- 依本機限制，單元測試不在本機執行。`char-screen-followup2`、`select-char-ui`、`create-char-ui`、`char-screen-followup` 等群組以 **PR 的 CI 結果為準**。

## 5. 未驗證什麼（待驗證）

- 真機畫面（M4，交給 Codex／dot）：
  - 選角上／下鈕在兩頁的外觀與按下效果；
  - 建角職業頁的循環速度與淡入的視覺效果，包括「濃度÷31」是否和 `sub_7D7740` 一致；
  - 參考客戶端的 png 是否都讀得到。
- 同一幀裡循環與淡入計時器的先後順序（推測）。
- 滾輪翻頁（推測，原版沒找到滾輪處理）。
- 沒做：
  - 職業鈕雙擊（`sub_EE3410` 515：不淡入，直接進能力頁）；
  - 選角時選到空格（TW16 可以，引擎只選角色）。

## 6. 產出路徑

- `engine/source/NativeUi/SelectCharPanel.cs`、`engine/source/NativeUi/CreateCharPanel.cs`
- `engine/source/NativeUi/CharScreenFollowup2Tests.cs`（新）、`engine/source/NativeUi/SelectCharPanelTests.cs`（三處舊斷言）
- `docs/reports/issue-913.md`（本檔）

## 7. 給下一棒的提示

- 真機時看 UI 紀錄：
  - `option-png … 1573.png` 這類行：參考客戶端的來源與 SHA-256；
  - `create-char-ui 缺圖不畫：png …`：缺了哪些圖。
- 如果要補職業鈕雙擊：只改 `CreateCharPanel.Fire`。`Mouse` 要把 `e.Clicks` 傳進去；`clicks>=2` 時直接換職業（不淡入）並 `EnterStatScene()`。
- gap-matrix 15.12 的 4:SelectUI／4:CreateUI 兩列可以考慮改成「已有」，只剩真機外觀（M4）。這要由主控裁定，本張沒有改 gap-matrix。
