# Issue #761 T323：P-D 原生盟屋拍賣窗（參考客戶端 AgitAuctionUI.xml）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK）§4 第 1:0／4:AgitAuctionUI 列、§6 P-D；P-A（#757，`RefClientXmlSource`）與 P-B（#760，`RefClientSurfFallback`）已在 main。Png 圖沿用 #768 的 `OptionPngSource`。

## 做了什麼

- **新檔 `engine/source/NativeUi/AgitAuctionPanel.cs`**
  - **S156 接線（同檔，不改 `Agit.cs`）**：
    - 在 `partial class WorldSession` 加 `[WorldReceive(156, Observe=true)]` 觀察者。S156 仍由 `Agit.cs` 的 `ReceiveAgitHouseList` 消化，資料與 `agit` 快照都不變。
    - 觀察者解析成功後，送一筆開窗請求到新的靜態 `AgitAuctionWindow`（帶遞增 Revision）。面板在每格 `Update` 讀取，做法同 `AgitMapWindow`／`AgitMapPanel`。
    - 封包格式錯時不送請求，`Agit.cs` 照舊丟 `ProtocolException`。
    - `[WorldReset]` 會清掉請求。另加 `[WorldSnapshot("agitAuctionWindow")]`，快照內容是 revision、boardId、筆數。
  - **面板 `AgitAuctionPanel`**（`PanelId="AgitAuctionUI"`）：
    - 註冊用 `[UiPanel(174)]` 自動發現，實作 `IUiClosablePanel`、`IUiMovablePanel`。宿主版面用 `uistatus86`，跟 Option／GameRanking 一樣。`Load` 內才讀 `AgitAuctionUI`：主 Tile.pak 沒有，就讀參考客戶端（預設 kor25），SHA-256 由 P-A 寫進 `ui-xml-ref` log。
    - surf 走 `SurfCatalog.Get`（本機 → P-B 參考備援）。Png 走 `OptionPngSource`（本機 Sprite → 參考 Sprite，log 前綴 `option-png`）。
    - 缺的圖不畫，列在 `Missing`，並寫一行 `agit-auction-ui 缺圖不畫` log。不用替代圖，也不複製任何檔案。
  - **版面照 XML，只做 `Agitmap_Window`**：
    - 視窗：位置取 X/Y（缺 X/Y 就丟例外，不自己編）。大小取宣告的 Width/Height（550×316），沒有宣告才用 BaseSurf 圖的大小；兩者都沒有時標 Unavailable。
    - BaseSurf 3961 與 FrameSurf 2828 畫在原點。`Moveable=20` 是可拖曳的標題列高度。
    - Exit_Button（`Exit_AgitWin`）關窗，並把焦點交回遊戲。
    - `Auction_List`：
      - 欄位標題鈕。
      - 10 列 CheckButton，依 Height＋Gap 往下排。每列的 `Name_Label`／`Price_Label`／`Deadline_Label` 填 S156 的名稱、價格、截止日。格式是**推測**：價格照伺服器送的有號整數，截止日寫成「月/日」。文字依 Align 對齊，白色。
      - 捲軸 Up/Down 鈕與滑鼠滾輪，一次捲一列；滑塊依 `ScrollStart`／`ScrollHeight` 定位。
    - `MapImage_Control`：4 個城鎮 Control 中只顯示清單盟屋所在的城鎮。對照規則是**推測**：
      - 區域 = `houseId >> 16`：8 古魯丁、4 奇岩、5 海音、7 亞丁。
      - `Agithouse<n>_Image` = 編號 n+1。
      - 依據：XML 的按鈕數 6／45／11／67，與 BIN `AgitMapWindow` 標記表的筆數 `AgitMapLayout.MarkerCount` 完全相同；3.81 盟屋 id 是「區域<<16 | 編號」。
    - 點清單列或盟屋鈕 → 選取該盟屋，該列與該盟屋鈕改畫 Push 圖。滑鼠移上盟屋鈕時畫 Highlight 圖。
    - `List_Button`（`List_AgitWin`）切換清單顯示／隱藏（**推測**）：
      - 依據：清單底圖 png 3055 是 197×144，放在 (2,149)，會蓋住奇岩／古魯丁的部分盟屋鈕，所以應能收起清單看地圖。
      - 每次收到新的 S156，清單都會重新顯示。
      - 畫在按鈕上方的圖也會擋住滑鼠，所以清單底下的盟屋鈕點不到。
    - 繪圖順序（**推測**，綁定函式沒有反組譯）：
      - 同一容器內依 XML **反向**順序畫。XML 把 Back_Image、ExitBack_Image、Back_Control 放在最後，但它們必須在按鈕與文字底下。
      - 按鈕自己的 Label 緊接在按鈕之後畫。
      - 點擊判定順序與繪圖相反，越上層越先判定。
  - **開窗與關窗**：
    - 每個新 Revision 的 S156 都會開窗（或更新已開的窗），清單捲回頂端、清掉選取。
    - 關窗後，同一筆請求不會再開；下一個 S156 才會再開。
    - `Toggle()`／`Open()` 重新打開上一份清單。
    - 離開世界或 `WorldSession.Reset` 時關窗。
- **新檔 `engine/source/NativeUi/AgitAuctionPanelTests.cs`**：`[WorldModuleTest("agit-auction-ui")]`。全部用合成資料：自寫的同結構小版面、純色圖、合成 S156 封包，不讀任何客戶端。涵蓋：
  - 版面：位置與大小、列堆疊、儲存格座標、捲軸、城鎮對照、盟屋編號、List_Button 的 String id、Missing／Skipped、反向繪圖順序。
  - 壞版面：沒有 Agitmap_Window、圖號壞、列沒有 Height、沒有清單列、城鎮重複、缺 X。另測兩種缺底圖：沒宣告大小時標 Unavailable；有宣告大小時列入 Missing 但照常開窗。另測沒有 UiResources。
  - 開窗：S156 開窗、換清單、換城鎮、沒有對應城鎮的區域、空清單、關窗後不重開、Toggle、離開世界、請求被清掉。
  - 捲動與選取，以及價格、截止日、名稱的格式。
  - 像素層級的繪圖：底圖／框、Exit 在 ExitBack 之上、標題鈕在清單底圖之上、只畫目前城鎮、選取的盟屋畫 Push 圖、hover 畫 Highlight 圖、滑塊位置、清單底圖擋住點擊、List_Button 收起與再展開。
  - 拖曳、Exit 交回焦點、放開在按鈕外不觸發、IUiClosablePanel。
  - 觀察者：已註冊、S156 仍被 Agit.cs 消化、送出開窗請求、快照、格式錯不送、預設來源、Reset 清除。
  - 自動發現。

> 任務說「測試用 `[CheckGroup]`」：`engine/source` 內的測試一律用 `[WorldModuleTest]` 自動發現，與 #757／#768／#769 相同；`[CheckGroup]` 只用在 `_integration/dev`。測試檔依任務指示用英文類名 `AgitAuctionPanelTests`。

## 既有 WinForms 入口

- S156 在本任務之前**沒有任何畫面**，只有資料：`Agit.cs` 的 `AgitHouseListReceived` 沒有訂閱者，快照的 `ui` 欄寫「data only」。所以 `--classic-ui` 或面板不能用（沒有參考客戶端、XML 壞、`"refClient":false`）時，維持原狀：只存資料，不開窗。
- 本任務只能改兩個新檔，`Agit.cs` 快照的 `ui` 說明字串沒有更新，仍寫「data only (XML missing)」；實際狀態看新的 `agitAuctionWindow` 快照。

## 資源核對（PakViewer CLI，唯讀，輸出只在 worktree 的 `scratch/t761/`，沒有進 git）

- XML：`pakviewer-cli pak extract "D:\381懶人包kor25(原)\Tile.idx" AgitAuctionUI.xml` 後再 `xml decrypt`。
  - 原檔 28,580 B，SHA-256 `2a3cd82581a99efabbdb39cc1705a9b1ceebe7d1cb2aac21caa4d7050208e737`。
  - 解密後 SHA-256 `3869aeacdbaa6ddd38289b98f9737845f8c4c91caac07fedb78420fd284d1e6d`。
  - 兩者都與 PAK§4 相符。
- 為了確認缺圖，用 `docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt -Out scratch\t761\pak-index` 列出以下清單，再用 `scratch/t761/find_images.py` 逐一比對：
  - `0-客戶端` 全部 24 個 idx：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile.idx`。`launcher\TW13081901.pak` 沒有 idx，打不開。
  - kor25、t89、ly 的 `Tile`／`Text`／`Sprite*.idx`。
- **`Agitmap_Window` 用到的圖**：

  | 圖 | 本機 `0-客戶端` | kor25 |
  |---|---|---|
  | surf 3961（BaseSurf） | `Sprite01` `3961c.img` | `Sprite01` `3961c.img`、`Sprite14` `3961.img` |
  | surf 2828（FrameSurf） | `Sprite15` `2828.img`、`Sprite12` `2828.tbt` | 同 |
  | png 2975–2980（ExitBack、Exit、盟屋鈕 3 態） | **全部沒有** | 全有（`Sprite10`–`14`、`06`） |
  | png 3055–3063、3065–3070（清單底圖、標題鈕、List 鈕、捲軸） | **全部沒有** | 全有（`2975.png`→Sprite10 等，位置符合 `SurfCatalog.NameHash` 規則） |

  - kor25 在預設路徑時，本視窗**沒有缺圖**。抽出 5 張 png 核對尺寸：3055 是 197×144、2978 是 16×19、3062 是 105×22、3056c 是 77×18、3065 是 11×13，都與 XML 座標吻合。
  - 參考客戶端不在時，連 XML 都讀不到，視窗停用，不會畫半套。
- **全 XML（含沒做的兩個窗）**：
  - surf 6239、6247、6248（Bid_Window 的 OK 鈕 Highlight 圖、底圖、框）本機沒有，kor25 有。
  - 6125／6126／6240／6241／6244–6246 只在本機 `LiTo\LTsurf.pak`，kor25 的 Sprite 也有。
  - png 2972–2974（AuctionInfo 的 Bid 鈕）本機沒有，kor25 有。
- **字串**：List_Button 的 `String="4391;1"` 是參考客戶端字串表的 id，不一定對得上本機 `string-c.tbl`，所以跟 OptionPanel 一樣只經 `TextResolver` 畫；產品中這個值是 null，按鈕上不畫字。

## 怎麼驗證

- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t761/build` 結果 **0 錯誤**。2 個警告在 `MagicUi.cs`，原本就有。
  - 本機沙箱裡的 `dotnet restore` 列不到 WebView2 套件內的 `.dll`／`.targets`，產生不完整的 `project.assets.json`，結果是 NETSDK1130 錯誤。這與本次改動無關。
  - 處理方式：把主專案 11/4 正常還原的 `obj/project.assets.json`、`nuget.g.props`、`nuget.g.targets` 讀出來，路徑改成本 worktree 後放進 worktree 的 `obj/`，再用 `--no-restore` 建置。主專案只讀、沒有寫入。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`agit-auction-ui` 與既有測試交給 PR 的 GitHub Actions CI。
- `rebuild-lock.json` 沒有改動。開始前已 merge `origin/main`（含 #767）。

## 未驗證什麼（待驗證）

- 本機不能跑程式，所以沒有實測真實 kor25 的 `AgitAuctionUI.xml` 經本面板解析後能不能用（Unavailable 是否為 null）。只做了靜態比對：元素結構與合成版面相同，用到的屬性都有處理。
- 以下都是**推測**：
  - 反向繪圖順序。
  - 城鎮與盟屋編號的對照。
  - List_Button 的切換行為，以及新 S156 時清單預設顯示。
  - 價格與截止日的顯示格式，文字白色。
  - 地圖本身是不是只靠 BaseSurf 3961：XML 的城鎮 Control 沒有圖，原版可能在執行期另畫城鎮地圖。
- 參考客戶端的版面是否與 TW16 原版相同：**推測**（PAK§7）。
- 真機（交給 Codex／dot，P24Lab 測試角色）：
  - 在盟屋拍賣公告板觸發 S156 後，是否開出原生拍賣窗，城鎮、盟屋位置、清單文字是否正確。
  - 收起清單後地圖是否正常。
  - `--classic-ui` 下是否維持原狀。

## 沒有做的部分（待做）

- **送出 C125 `select`**：選盟屋後查拍賣資訊（伺服器回 `agsel` 頁）還沒接上，點選時記一次 `agit-auction-ui select 尚未接上`。
  - 原因：既有的送出端 `SendAgitSelect`（`RingWindows.cs`，由 `ClientForm.login` 呼叫）只接受「目前這筆 S187」，且宿主只掛在 `AgitMapPanel`。要接上必須改 `RingWindows.cs`，不在本任務可改的檔案範圍內。
- `Bid_Window`（出價，原版由 S136 `agapply` 開，現由 `AmountPrompt` 的 HTML 頁加數字框處理）與 `AuctionInfo_Window`（MultiLabel＋Bid 鈕）：沒有建。
- 欄位排序鈕（`Name_`／`Price_`／`Deadline_AuctionList`）、捲軸滑塊拖曳、`Back_Control`（`MapClick_AgitWin`）：點擊時記 log，不動作。

## 產出路徑

- `engine/source/NativeUi/AgitAuctionPanel.cs`（新）
- `engine/source/NativeUi/AgitAuctionPanelTests.cs`（新）
- 本報告 `docs/reports/issue-761.md`
- 分支 `task/761`

## 給下一棒的提示

- 接 `select`：建議開一張小任務，可改 `RingWindows.cs`。
  - 做法一：新增 `SendAgitAuctionSelect(uint boardId, uint houseId)`，條件比照 `SendAgitSelect`，但驗證對象改成 `World.AgitHouseList` 的 `BoardId` 與 house id。
  - 做法二：讓 `AgitAuctionPanel` 吃一個 `IAgitAuctionHost`，比照 `IAgitMapHost`。
  - 封包沿用 `AgitSelectPackets.Select(boardId, houseId)`；`houseId` 本身就是「區域<<16|編號」。
- 真機驗收步驟：
  1. 原生 UI 開著、kor25 在預設路徑時，點盟屋拍賣公告板，log 應出現 `ui-xml-ref AgitAuctionUI.xml` 與多行 `option-png …`，並開出 550×316 的拍賣窗。
  2. 加 `--classic-ui`，或在 `native-ui.json` 設 `"refClient":false`，應不開窗（原狀）。
- 若真機看到繪圖順序或城鎮對照不對，只要改 `AgitAuctionPanel.Builder.Container`（順序）或 `TownRegions`（對照）。
