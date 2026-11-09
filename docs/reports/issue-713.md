# Issue #713 — T301：R4-J 原生書籤窗

分支 `task/713`，PR #726。依據 `docs/reference/gap-matrix.md` 13.5 節 R4-J、`ui-layout.md` 第 6 節（UI§6）、`followups-3.md` 第 3 節（FU3§3）。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/BookmarkPanel.cs`（新） | `BookmarkWindowPanel`：`[UiPanel(178)]`、版面 `BookmarkUI`（`Bookmark_Window`），實作 `IUiMovablePanel`＋`IUiClosablePanel`；宿主介面 `IBookmarkWindowHost` |
| `engine/source/RingWindows.cs` | `BookmarkWindowHost`（ClientForm 轉接）；選目的地改走原生窗；WinForms 備援時真的叫出視窗 |
| `engine/source/BookmarkUi.cs` | 只改註解（說明原生窗接手、WinForms 分頁變成備援） |
| `engine/source/BookmarkPanelTests.cs`（新） | `[WorldModuleTest("bookmark-window-panel")]` 離線測試 |

類別名稱用 `BookmarkWindowPanel`，因為 `P06.BookmarkPanel` 已經是 WinForms 分頁的類別名稱；檔名照任務規定的 `NativeUi/BookmarkPanel.cs`。

### 原生窗（全部在執行期從 Tile.pak 讀 `BookmarkUI.xml`，沒有把 XML 或圖複製進 repo）
- **已驗證（XML，UI§6）**：框 FrameSurf 4313（窗大小）、底 4312 (6,9)、`Exit_Button` (249,15)、`MemoryPlace_List` (22,162) 200×204、12 列（列高 17，選取圖 PushSurf 4336）、捲軸（`WheelControl` (23,163) 內 `ScrollBar` (218,−5) 的上、下鈕、底圖、拉桿）、`Confirm_Button` (67,395)／`Cancel_Button` (134,395)（確認鈕停用時畫 DisableSurf）、`BookmarkCount_Label`（n/上限）。可拖曳，標題列高度用 `Moveable=20`。
- **已驗證（BIN，FU3§3.1）**：
  - 選目的地模式（`sub_BDFE90(1)`）：S36 type 1＋技能 5／69 的請求（`AbilityStatus.cs` 既有的 `TeleportDestinationRequest`）一出現就開窗。確認鈕經宿主呼叫既有 `LoginConnection.SendTeleportDestination` 送 C6 **HD**（書籤地圖、書籤 id，3.81 格式，FU3§3.2），本面板不組封包、不碰 `LoginConnection`。
  - ESC（`sub_BDAA10`）關窗。關閉、取消、ESC、4 層視窗堆疊擠出，在選目的地模式下都是「取消請求、不送封包」（`CloseTeleportDestination`）。同一個請求取消後不會再開。
  - 單筆書籤收包後開窗（TW16 op 2 `sub_931D60`；3.81 是 S92）。面板用 `Bookmarks.ReceiveOne` 設的提示字串分辨單筆與整份清單（`Bookmarks.cs` 不在可改範圍，所以不加新旗標）。整份清單（S64）、第一次看到這個角色、換角色時都不開。
- **推測（程式註解也有標）**：框先畫、再畫底；列文字白色、左邊留 3 px、垂直置中；選取圖畫在文字下面；刪除待確認的列文字灰色；一般模式的確認鈕＝關窗；選目的地請求結束（送出成功）時窗一起關；拉桿位置＝ScrollStart＋(ScrollHeight−拉桿高)×捲動/最大捲動；上下鍵移動選取（`MemoryPlaceKeyBoard` 的內容沒追）；清單順序沿用書籤資料的順序。
- 缺必要的圖（框、底、關閉／確認／取消鈕、選取圖）→ 只停用本面板並記一行日誌；缺捲軸零件只是不畫那一件。XML 缺必要節點 → 丟 `UiLayoutException`（照 `IUiPanel.Load` 契約，不自己編座標）。

### WinForms 備援與「真的叫出視窗」
- `RingWindows.SyncRingWindows`：原生 UI 開啟、原生書籤窗可用（圖齊全、沒有因例外被停用）時，選目的地交給原生窗，WinForms 分頁**不**進入該模式（避免兩邊同時有窗）。
- 原生 UI 關閉、或原生窗不可用時，維持 PR #700 的 WinForms 選目的地模式。新請求時除了切到「記憶座標」分頁，還會把分頁所在的視窗叫出來：玩家 HUD 模式用 `ShowPlayerService("記憶座標")`，其他情況用 `ShowDevTools(normalPanels)`。PR #700 的 `RingWindows.cs:72` 只切分頁，這個問題已經修好。
- 新增／刪除書籤目前**只有 WinForms 分頁有入口**（原生窗沒做輸入框，也沒做 op 106＋846 的刪除確認）；原生 UI 開啟時仍可從開發工具的「服務 → 記憶座標」使用。

### 沒做（待驗證或之後的任務）
- 快速移動清單 `QuickMove_List`（含它的標題圖）、區域篩選 `ZoneFilter_Combobox`、隨機傳送、排序、13 色調色盤、`CloningMemoryPlace_List`：3.81 有沒有對應的協定沒查，所以**不畫**這些（規則 5：不畫不能用的鈕）。
- ESC 關窗時「必要時」顯示的 2847「座標記憶結束」：條件沒追。
- 一般模式的開窗熱鍵或選單入口：面板有 `Toggle()`（XML `Toggle="true"`），但熱鍵與選單鈕屬於 R4-A 的檔案（`MenuButtonsPanel.cs` 等），本任務不改。目前一般模式只有在單筆書籤收包時才會自動開窗。
- 本任務沒有標 Q4-n 的項目。

## 怎麼驗證
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "errors": 0}`。
- 離線測試 `bookmark-window-panel`（合成 XML 與單色 surf，不讀真實客戶端）：註冊一次＋介面；UI§6 座標（窗原點改成 (10,20) 檢查偏移）；框、底、按鈕、選取列、停用確認鈕、拉桿的像素；S92 單筆收包會開窗，整份清單／換角色／同一個 revision 不會開；也用真的 `WorldSession` 收 S92，確認提示字串對得上；選目的地流程（沒選不送、待刪除列不能用、送一次、送出中停用、請求結束關窗）；ESC／取消／關閉／`IUiClosablePanel.Close`／離開世界都會取消請求，同一請求不重開，下一個請求重開；捲動（上下鈕、滾輪、方向鍵、上限）；拖曳；缺圖停用；缺節點丟例外；沒有宿主時不開窗。
- **CI（PR #726，run 42693944085）**：`dotnet`、`map-continuity`、`npc-offline` 都通過；`--module-tests` 全部組 `failed: 0`，其中 `bookmark-window-panel`（`P06.BookmarkPanelTests.Run`）61 項檢查全過。
- 編譯與測試以 CI 為準。本機 `dotnet build` 和 `rebuild.py --build --configfile <CI nuget 設定>` 都卡在 `NETSDK1130: Microsoft.Web.WebView2.Core.winmd 無法參照`，在 Csc 之前就停了（worktree 沒有 `engine/vendor`，是本機套件解析的問題，與本次改動無關）。
- 依本機執行限制，沒有在本機執行任何編譯出來的程式。

## 未驗證什麼（待驗證，交給 Codex／dot 真機）
- 原生窗在真實 `BookmarkUI.xml`／Sprite 下的畫面：框和底的疊法、文字位置、捲軸零件的位置（巢狀座標是推測），尤其要確認啟用原生 UI 後**整個 UI 沒有因為本面板的 `Load` 例外而退回舊介面**。`UiRoot.Build` 是全有或全無；我已經對照本機解出的 XML 核對過每個必要節點與屬性。
- 傳送控制戒指＋指定傳送／集體傳送：原生窗開窗、選書籤後實際傳送、ESC 取消。
- 原生 UI 關閉時，WinForms 分頁是否真的會跳出來（玩家 HUD 模式和一般模式各一次）。
- 3.81 伺服器登入時會不會逐筆送 S92（會的話，登入時原生窗會自動開；原版 TW16 遇到 op 2 也會開窗，所以這是照原版）。

## 產出路徑
- `engine/source/NativeUi/BookmarkPanel.cs`、`engine/source/BookmarkPanelTests.cs`、`engine/source/RingWindows.cs`、`engine/source/BookmarkUi.cs`
- 本報告 `docs/reports/issue-713.md`

## 給下一棒的提示
- R4-A（選單鈕／熱鍵）如果要加一般模式的書籤窗入口：`NativeUiRoot.Find(BookmarkWindowPanel.PanelId)` 取得面板後呼叫 `Toggle()`。原版書籤窗的熱鍵還沒查。
- 原生的新增／刪除要做的話：新增要接 `IUiTextInput`（參考 `PledgePanel`），刪除要先跳 846 的確認（可以用 `YesNoPanel`），送包沿用既有的 `LoginConnection.SendBookmark`。
- `BookmarkMemoryWindowUI`（gap-matrix 13.4，開窗子碼 141 是推測）可以接在本面板之後做。
- 如果之後改得到 `Bookmarks.cs`：建議加一個「單筆收包」的 revision，取代現在比對提示字串的做法（`BookmarkWindowPanel.SingleBookmarkNotice`）。
