# Issue #805 T342：R7-C 戒指與盟屋拍賣

依據：`docs/reference/gap-matrix.md` 第 15.6 節 R7-C（T339／#800）；#736、#761 報告；PAK§4；第 1 節 op 122、0；FU3§4.1；WIN2§5.2–5.3。

## 做了什麼

### 1. 戒指開窗不查限制地圖（op 122）

- `AbilityStatus.cs` `TryOpenRingWindow`：拿掉開窗前的限制地圖檢查。戴傳送控制戒指施放 5／69 時，地圖 5、6、83、84、≥0x4000 也照樣開書籤窗，不顯示 276。
  - 依據 #736：`sub_E08A20` 從 `0xE08B22` 起的開窗路徑不查限制地圖；276 只出現在直接送 C6 的路徑（`0xE08BA9`）。
  - `RingWindowRules.TeleportBlocked`／`TeleportBlockedMessage` 保留為規則常數。直接施放路徑不在本組檔案，註解已寫明。
  - 順手更新過時註解：妖精 Lv≥50 分支已在 `RingWindows.cs`（R5-D）。
- 妖精選項 `byte_13A931C` 的 UI：依原表要等 Q7，**本組沒做**，仍只有 `WorldSession.ElfBookmarkTeleportOption` 屬性。

### 2. 盟屋拍賣（1:0、AgitAuctionUI）

- **選盟屋送 C125 `select`**（`NativeUi/AgitAuctionPanel.cs`、`RingWindows.cs`）
  - 新介面 `IAgitAuctionHost`。拍賣清單窗（`Agitmap_Window`）點清單列或盟屋鈕時，選取該盟屋並呼叫 `Host.Select(shown, houseId)`。
  - 送出端 `LoginConnection.SendAgitAuctionSelect`，封包 `C125 D(S156 看板 id) S("select") S("%u" 盟屋 id)`，和 674 鈕相同（FU3§4.1，已驗證）。
  - 以下情況不送：S156 已被新的取代、盟屋不在目前清單、互動送出中、移動中、動作鎖定、交易中。
  - 沒有宿主時（離線）照舊記一次 `select 尚未接上`。
- **AuctionInfo_Window**（新面板 `AgitAuctionInfoPanel`，`[UiPanel(184)]`，`IUiClosablePanel`）
  - S39 加觀察者（`[WorldReceive(39, Observe=true)]`）。NPC 對話照舊處理同一個封包，觀察者只認 `agsel`，格式 `C D S("agsel") S(house) H(n) S×n`，取自 server-protocol.md 的 S_AuctionBoardRead。解析成功就送出開窗請求 `AgitAuctionInfoWindow`。
  - 只在拍賣清單窗開著、而且是同一個看板時才開窗。清單窗關掉、Esc、離開世界時一起關。
  - `Info_Label`（MultiLabel）＝ agsel 模板（`dialogs/templates.json`，和 NPC 對話同一份）帶入伺服器送來的字串，再經 `ResolveNpcText` 解 `$編號`。沒有模板就不畫，並記一次 log。
  - `Bid_Button`（`Bid_AuctionWin`）→ `SendAgitAuctionApply`，送 `C125 D(board) S("apply") S(house)`。3.81 `Npc_AuctionBoard` 的 `apply` 分支回 `S_ApplyAuction`（S136 agapply），或 518／519／521 訊息。
- **Bid_Window**（新面板 `AgitBidPanel`，`[UiPanel(189)]`，`IUiClosablePanel`／`IUiMovablePanel`／`IUiTextInput`）
  - 收到 S136 `agapply` 時開窗。WIN2§5.3 已驗證：id 是 agapply 時原版交給 CAgitAuctionUI。資料取自 `AmountPrompt.cs` 的目前詢問，由宿主提供。
  - `CurPrice_Label` 顯示詢問的下限，即 3.81 送的 minimum。
  - `Price_Edit` 的初值是建議值。只收數字（WM_CHAR），長度上限是 LimitText，Backspace 刪字。
  - Ok 或 Enter：數值在下限與上限之間時，經既有的 `SendAmountReply` 回 C11（一次性票不變）。
  - Cancel、Close、Esc：關窗，不送任何封包。同一筆詢問不重開，下一筆 agapply 才開。
  - 標題列 20 px 可以拖曳。
- **`AmountPrompt.cs`**：原生 Bid_Window 正在顯示同一筆 agapply 時，WinForms 數字列不顯示，避免兩個輸入框；`--classic-ui` 時不受影響。
- **`Agit.cs:111`**：快照 `ui` 說明從「data only (XML missing)」改成目前的原生面板狀態，檔頭註解也一併更新。
- 宿主 `ClientForm.AgitAuctionHost`（`RingWindows.cs`）在 `SyncRingWindows` 時掛到三個面板。`--classic-ui` 不建立原生 UI，維持原狀：S156 只存資料，agsel／agapply 走 NPC 對話頁與 WinForms 數字框。

### 3. 測試

- 新檔 `engine/source/RingAgitFollowupTests.cs`，`[WorldModuleTest("ring-agit-followup")]`，自動發現。只用合成封包、自寫的小版面與單色圖。涵蓋：
  - 戒指在 5 種限制地圖都開窗，且不出現 276。
  - `apply`／`select` 封包的 hex 內容。
  - agsel 解析：其他頁、看板 0、截斷、空封包都回 null，不丟例外。
  - 觀察者已註冊；Reset 會清除請求。
  - select／apply 送出端：目前請求送一次；以下情況不送——不在清單、動作鎖定、agsel 已被取代、別的看板、非數字編號、Reset 之後。
  - 清單窗：點列或盟屋鈕 → 宿主收到 select；沒有宿主只記 log。
  - 資訊窗：位置、大小、Bid → apply、Esc、看板不同不開、清單窗關閉時一起關、缺模板、缺 MultiLabel、缺 X、沒有 UiResources。
  - 出價窗：位置、大小、缺圖、字型顏色、只收 agapply、數字輸入與上限、低於下限不送、宿主不能送時不送、Ok／Enter、Plus 只記 log、Cancel／Esc 不重開、詢問消失就關、拖曳、Close、無法作答的詢問、缺 Edit、缺底圖時用子元件定大小。
  - 自動發現註冊與 `Agit.cs` 快照字串。
- **範圍外但必要的改動（之一，既有 bug）**：`engine/source/NpcInteraction.cs` 第 119 行（R7-D／#806 已合併，目前沒有其他組在用這個檔）。
  - 3.81 `S_AuctionBoardRead.java`（SHA-256 前 16 碼 `0bc92c0a00302b8e`，只讀）寫的是 `C D S("agsel") S(盟屋編號) H(9) S×9`，`agsel` 後面沒有 flag。
  - 一般 NPC 對話的解析是照 `S(html) H(flag) H(count)` 讀，會把盟屋編號字串當成 flag，所以**每一個真的 agsel 都會丟 `ProtocolException("dialog arguments")`**。
  - 674 鈕（#550）上線後就一直有這個問題，PR #829 第一輪 CI 也因此失敗。本 PR 加入清單窗送 select 之後，這個問題更容易碰到。
  - 修正：html 是 `agsel` 時，先讀掉盟屋編號，再讀數量；其他頁的讀法不變。修正後 agsel 頁照常交給 NPC 對話顯示。
- **範圍外但必要的改動（之二）**：`engine/source/RingWindowsTests.cs` 第 80–84 行原本斷言「限制地圖顯示 276、不開窗」，這正是本任務要改掉的行為。已改成「照樣開窗、不顯示 276」，並把窗關掉，讓後續步驟維持原狀。

## 怎麼驗證

- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t805/build`，**0 錯誤**。2 個警告在 `MagicUi.cs`，原本就有。
  - 本機 `dotnet restore`／`rebuild.py --build` 仍有 #761 記過的 NETSDK1130（WebView2 winmd）問題，和本次改動無關。
  - 處理方式同 #761：把主專案 `engine/source/obj` 的 `project.assets.json` 與 `nuget.g.*` 讀出來，路徑改成 worktree 後寫進 worktree 的 `obj/`（git 忽略），再用 `--no-restore` 建置。主專案只讀、沒有寫入。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- XML 核對：讀既有的 kor25 解密檔（task-629 worktree 的 scratch，只讀），SHA-256 `3869aeac…284d1e6d` 與 PAK§4 相符。Bid_Window／AuctionInfo_Window 的元素與屬性照這份實作。資源缺漏沿用 #761 的 PakViewer 全 pak 核對：Bid_Window 的 6239／6247／6248 本機沒有、kor25 有；6125／6126／6240／6241／6244–6246 本機在 `LiTo\LTsurf.pak`；png 2972–2974 本機沒有、kor25 有。本任務沒有新增任何「缺資源」的宣稱。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式或測試**；`ring-agit-followup` 與既有各組交給 PR 的 GitHub Actions CI。
- `rebuild-lock.json` 沒有改動；沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`。

## 未驗證什麼（待驗證／推測）

- 沒有開遊戲、伺服器或 DB；全部畫面與送包沒有真機驗證。
- **推測**：
  - agsel 在原版是否也交給 CAgitAuctionUI（BIN 的轉交沒追）。目前原生資訊窗和 NPC 對話頁（HyperText 或 WinForms）會**同時**顯示同一頁。
  - Bid_Button 送 `apply`：伺服器只認這個動作名，但原版鈕實際送什麼沒追。
  - `CurPrice_Label` 的意義（下限＝目前價格）。
  - Bid_Window 停在 XML 的 (0,0)；原版可能置中。
  - AuctionInfo_Window 沒有宣告大小，用子元件的聯集定大小。
  - MultiLabel 逐字折行。
- 沒套用：`Modal="1"`（不攔其他窗的滑鼠）、`DarkBack="1"`（XML 沒有顏色或透明度，同 ResetStatPanel 的處理）、`Activate`。
- Minus／Plus 鈕：步進值沒查，按下只記一次 log，不改數值（不自己編步進值）。
- 原生 UI 下 agapply 仍會在 HyperText 窗出現頁內數字框（`NpcUi.cs` 不在本組範圍），所以和 Bid_Window 會有兩個輸入。兩邊都走同一個一次性 C11 票，不會重送。
- 真機步驟（交給 Codex／dot，P24Lab 測試角色、王族盟主 Lv≥15、沒有盟屋）：
  1. 原生 UI 開著、kor25 在預設路徑時，點拍賣公告板，開出拍賣窗。
  2. 點一列 → log 有送出 C125 select，開出資訊窗，內容是 agsel 模板。
  3. 按 Bid → 收到 S136 agapply，開出出價窗；輸入金額後按 Ok → C11。
  4. 加 `--classic-ui` 時應維持原狀。
  5. 戴傳送控制戒指，在限制地圖施放指定傳送 → 開書籤窗，不出現 276；選定後由伺服器判斷。

## 產出路徑

- `engine/source/AbilityStatus.cs`、`RingWindows.cs`、`NativeUi/AgitAuctionPanel.cs`、`AmountPrompt.cs`、`Agit.cs`
- 新檔 `engine/source/RingAgitFollowupTests.cs`
- 範圍外必要改動：`engine/source/NpcInteraction.cs`（agsel 解析）、`engine/source/RingWindowsTests.cs`
- 本報告 `docs/reports/issue-805.md`；分支 `task/805`

## 給下一棒的提示

- 其他伺服器自訂的 S39 頁可能也不是 `S(html) H(flag) H(count)` 格式，`agsel` 就是一例。建議找一張任務，把 server-protocol.md 裡所有 `S_OPCODE_SHOWHTML` 的寫法和 `NpcInteraction.cs:119` 逐一比對。

- `NpcUi.cs` 擁有者：原生 Bid_Window 可用時（`ClientForm.NativeAgitBidShowing(offer)`），`HyperTextView` 的 `number` 對 agapply 應改傳 null，才能消除兩個輸入框。agsel 頁是否也從 HyperText 移走，要等 BIN 查證。
- 若要查原版細節，從 `CAgitAuctionUI`（`sub_546800` 附近，`0x5448F0`）追：agsel 的轉交、Bid 鈕送的動作、Minus／Plus 的步進值、Bid_Window 的位置（是否置中）。
- 妖精選項 `byte_13A931C` 的名稱與預設值等 Q7；查到後接 `WorldSession.ElfBookmarkTeleportOption`（`RingWindows.cs:40`）。
- R7-N（參考客戶端字串表）合併後，`PriceTitle_Label`（String 4029）與 List_Button（4391）可以經 `TextResolver` 畫出來。
