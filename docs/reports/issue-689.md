# Issue #689 報告：T288 Q3 補做——血盟倉庫紀錄原生窗、隱身只限同隊、酒醉註解

依據：`docs/reference/followups-4.md`（FU4）§2、§4、§5、§7；`docs/reference/followups-5.md`（FU5）§5（surf 9183 本機核對）。

## 做了什麼

### 1. 血盟倉庫紀錄（R3-C，Q3-6）
- **`ClanWarehouseHistory.cs`**
  - 類型註解改成已驗證：**0＝存放、1＝領出**（FU4§5）。
  - 新增 `ClanWarehouseHistoryText`：照 `sub_A55FE0` 每筆排出 3 行字＋1 行空白：
    1. `1880`「%s將道具 %s」／`1881`「%s將金幣 %s」（物品名稱等於 `$4`），參數是角色名稱，加上 `1883`「存放至血盟倉庫中。」（類型 0）或 `1882`「從血盟倉庫領出。」。
    2. `1884`「[%s]x(%d)」（`$編號` 展開後的名稱、數量），金幣用 `1885`「[%d] 金幣」。
    3. `1886`／`1887`／`1888`（分／小時／日），沒有紀錄時用 `1889`。
  - 字串從執行期 `string-c.tbl` 讀取（`ServerMessageText.Table`）；讀不到時改用內建原文（本機 `string-c.tbl` 1880–1889 的原文，decoded SHA-256 `4ebbc1a7…43e9`，在記憶體中讀出核對）。格式是 C 的 `%s`／`%d`，和 `sub_933850` 的 `%0` 不同，所以另外寫了 `CFormat`。
  - WinForms 暫行清單（原生 UI 沒開時用）的「類型」改成存放／領出，「距今」改用 1886–1888 的文字。
- **新檔 `NativeUi/ClanWarehouseHistoryPanel.cs`**（`[UiPanel(172)]`，id `CLAN_WAREHOUSE_HISTORY_WINDOW`，版面取自 uiextra86 的 `HYPER_TEXT_WINDOW` 段）
  - 依 XML：底圖 1968 (6,9)、框 1969 畫在窗原點、捲軸 1970 (217,51)、上鈕 2042 (217,37)、下鈕 2040 (217,275)。
  - 寫死的部分：關閉鈕 surf 9183 在 (215,9)；標題列 (1,1,239,20) 可以拖曳；一次顯示 16 行，第 r 行畫在 (21, 44+15·r)，白色（調色盤 14）；沒有紀錄時在 (21,73) 畫 1889。
  - 捲動支援滾輪和上下鈕，步長 1 行。關閉鈕關窗時不送封包；離開世界時窗會關閉；實作 `IUiClosablePanel`，視窗堆疊滿了會被擠掉。
  - **surf 9183 本機沒有**（FU5§5 已用 `Sprite*.idx`／`Tile.idx` 核對）→ **不畫關閉鈕**。點擊區仍在 (215,9)，大小取 XML `SURF_RENEWAL_BTN_QUIT_X` 1962 的圖（推測），只當點擊區，不畫出來。之後本機若有 9183，就改用它的圖與大小。
- **`ClanWarehouseHistoryUi.cs`**：原生 UI 開著時，收到 S250/117 非空清單就開原生窗（並移到最上層），同時把暫行清單收起來；原生 UI 沒開時照舊用暫行清單。

### 2. 隱身可見例外（R3-H，Q3-5）
- **`Invisibility.cs`**：`InvisiblePeerVisible` 拿掉血盟條件，只看對方在不在自己的隊伍名單裡（`dword_13DC4E4`＋`sub_956D30`，已驗證，FU4§4）。註解補上 `0x40` 分支在 3.81 永遠不成立的原因（需要 S36 類型 3，以及名稱含 `$431`「沙哈的戒指」的物品，FU5§4）。

### 3. 酒醉（R3-H，Q3-3）
- **`TimedBuffs.cs`**：只改註解。`sub_81B0E0`＝16 位元色彩模式，只有 32 位元路徑會把位移減半；引擎照 16 位元路徑走，固定全振幅。減半分支保留給測試用。

### 4. 聊天第二色（R3-G，Q3-9）
- 核對結果：R3-G **沒有畫第二色**。`ChatFormat.SecondColor` 只放進 `ChatRow.SecondColor` 當資料帶著，`ChatPanel` 和舊 HUD（`PlayerHudChat.cs`）都沒有讀它；`ChatState.cs`、`PlayerChat.cs` 的註解也已經寫明「原版不讀，不畫」。所以 `PlayerHudChat.cs`、`ChatState.cs` **沒有修改**。

### 5. 測試
- **新檔 `Q3FollowupTests.cs`**（`[WorldModuleTest("q3-followup")]`，自動發現）涵蓋：
  - 三行文字：類型 0／1、金幣與道具、分／小時／日的邊界（59／60／1439／1440）。
  - 從合成字串表取字、去掉顏色控制碼、表裡空白或太短時改用內建原文；`CFormat` 的 `%%`、缺參數、參數不再解析。
  - 面板：註冊、XML 座標、9183 缺圖時不畫只留點擊區、有 9183 時畫出來、Bounds、16 行與行位置、滾輪與上下鈕捲動及範圍、拖曳只在標題列、`MoveTo`、關閉、重開、離開世界、堆疊擠出、空清單。
  - 隱身只限同隊；酒醉產品路徑不減半。
- **任務檔案範圍以外，必要的最小修改**（既有測試的斷言和本任務查證的結果相反，不改 CI 會紅）：
  - `AmbienceWiringTests.cs` 的 `Invisible`：「同血盟可見」改成「同血盟但不同隊：不畫」。
  - `CookAndHistoryTests.cs` 的 `Rows`：類型文字改成存放／領出，時間改成 1886 的文字，並固定不用字串表（`UseTable(null)`）。

## 怎麼驗證
- **編譯**：本機 `dotnet build`／`rebuild.py --build` 卡在既有的環境問題：WebView2 的 `.winmd` 參考觸發 NETSDK1130，main 上一樣會發生，和本任務無關；它在編譯之前就擋下了。所以我改用 `dotnet msbuild -t:ResolveReferences;CoreCompile` 單獨跑 C# 編譯：只出現 `WebPanel.cs` 3 個 WebView2 型別錯誤（同一個環境問題），**本任務改到或新增的檔案沒有任何錯誤或警告**。完整建置以 CI（windows-latest、乾淨的套件快取）為準。
- `py -3 engine/tools/check-no-game-assets.py`：`pass`（4042 個檔、0 錯誤）。
- 依本機執行限制，**沒有在本機執行**任何編譯出來的程式或測試；單元測試與 `q3-followup` 交給 PR 的 CI。
- 沒有改 `rebuild-lock.json`（`--build` 用臨時 lock，`writes: false`）。

## 未驗證什麼
- **CI 結果**：push 後看 PR 的 CI（module tests 裡的 `q3-followup`、`cook-and-history`、`ambience-wiring`）。
- **推測、尚未證實的部分**：
  - 窗原點用 XML 的 (0,0)，建構子實際怎麼擺沒有追。
  - 標題列 (1,1,239,20) 當成左、上、右、下來解讀。
  - 9183 缺圖時，點擊區大小借用 1962。
  - 捲動範圍限在 0..(行數−16)；原版 `LScrollBar` 是 0–299，有沒有隨筆數調整沒追（FU4§9）。
  - 拉桿 2044 沒有座標，所以不畫。
  - 底圖不做半透明（引擎沒有 `dword_1372264` 這個設定）。
  - 類型值不是 0 或 1 時一律當領出；小時、日用整數除法。
  - 本窗搶在一般熱鍵之前吃哪些鍵（`sub_8CA740` 呼叫的 vtable +40）沒有展開，所以 `Key` 一律不吃。
- **真機**：要開原生 UI（`--native-ui`），讓 3.81 伺服器送 S250/117（血盟倉庫紀錄），看窗的位置、字型、行距與實際畫面——交給 Codex／dot 在 P24Lab 驗收。

## 產出路徑
- `engine/source/NativeUi/ClanWarehouseHistoryPanel.cs`（新）
- `engine/source/Q3FollowupTests.cs`（新）
- `engine/source/ClanWarehouseHistory.cs`、`ClanWarehouseHistoryUi.cs`、`Invisibility.cs`、`TimedBuffs.cs`
- `engine/source/AmbienceWiringTests.cs`、`CookAndHistoryTests.cs`（只改與查證結果相反的斷言）
- `docs/reports/issue-689.md`（本檔）

## 給下一棒的提示
- 真機核對清單：
  - 收到非空 S250/117 時原生窗會開；文字共三行，第 1 行是「某某將道具 存放至血盟倉庫中。」這類格式。
  - 關閉鈕的位置 (215,9) 看不到圖（本機缺 9183），點下去要能關窗。
  - 按住標題列可以拖曳；超過 16 行時，滾輪和上下鈕可以捲動。
- 本機若出現 9183（例如換了客戶端），面板會自動改用它的圖與大小，不必改程式。
- 本機建置環境：使用者的 NuGet 快取 `C:\Users\…\.nuget\packages\microsoft.web.webview2\1.0.4258.31` 只有 `lib\net462`，不完整；就算改用新下載的完整套件，NETSDK1130 仍然存在。建議另開 `needs:local` 查本機 WebView2 參考的問題（CI 正常）。
