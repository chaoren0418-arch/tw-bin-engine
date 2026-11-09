# Issue #734 T307：R5-B 原生書籤窗收尾

依據：`docs/reference/gap-matrix.md` 第 14.6 節 R5-B；UI§6（`ui-layout.md` 第 6 節）、FU3§3.1（`followups-3.md`）；`BookmarkUI.xml`（本機 `Tile.pak` 解密版，只讀 scratch 內的解密檔核對元素名稱與座標，沒有複製進 repo）。

## 做了什麼

只改任務列出的三個檔案：`engine/source/NativeUi/BookmarkPanel.cs`、`engine/source/BookmarkUi.cs`、新檔 `engine/source/BookmarkPanelFollowupTests.cs`。沒有改 `RingWindows.cs`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

1. **刪除（op 106＋846）**
   - 依據（已驗證，FU3§3.1）：`sub_BDC1F0` 送 TW16 op 106 `csd`，送之前先跳字串 846「要刪除所選擇的項目？」。3.81 送的是 C3（書籤名稱），沿用既有的 `LoginConnection.SendBookmark`。
   - 怎麼觸發（推測，`sub_BDC1F0` 的呼叫點沒追）：選取一列後按 Delete 鍵，或在列上按右鍵，就會進入 846 確認。
   - 確認方式：Confirm_Button／Enter／Y＝是；Cancel_Button／ESC／N＝否。確認中按其他鍵都吃掉，避免換了選取。
   - 提示字畫在調色盤那一列 (22,372)。這是引擎自訂的畫法，原版 846 用的訊息窗樣子沒追。
2. **新增（op 84／C165）**
   - XML 裡沒有輸入框，原版從哪裡輸入名稱也沒追。
   - 引擎自訂的作法：按 Insert 鍵開始命名，字元走 `IUiTextInput`（WM_CHAR，含 IME 組字）。Enter 或確認鈕送出，ESC 或取消鈕放棄，Backspace 刪字。
   - 名稱規則和 WinForms 分頁相同（`BookmarkPackets.NameError`）。另外在本機擋同名（不分大小寫）和清單已滿。
3. **編輯宿主**：新介面 `IBookmarkEditHost`（`BookmarkPanel.cs`），和既有的 `IBookmarkWindowHost` 分開，所以 `RingWindows.cs` 不用改。
   - 實作是 `BookmarkUi.cs` 的 `BookmarkEditHost`：送出前呼叫 `ClearInput`，走路中不送，同時只送一筆。
   - 原生窗可用時，由計時器 `AttachBookmarkEditor()` 接上這個宿主。
   - 選目的地模式（傳送控制戒指）中不能新增或刪除。進入選目的地模式時，會清掉還沒回答的 846 確認與命名。
4. **排序 `Sorting_Button` (182,42)**：按一下在「資料順序」和「名稱順序（Ordinal，同名再比 id）」之間切換。切換時保留目前的選取。原版的排序依據沒追（推測）。
5. **區域篩選 `ZoneFilter_Combobox` (129,141)**
   - 照 XML 的部分：按 `Expand_Button` 展開 `Expand_List`（Y=19，每列 19，PageSize 7，背景 X=-2，選取圖 PushSurf）。第 0 項文字取 XML 的 `Caption="ALL"`。
   - 推測的部分：其他項目是書籤裡出現過的地圖編號，由小到大排。原版應該顯示區域名稱，但名稱表沒追。
   - 操作：選了之後清單只剩該地圖的書籤，筆數標籤仍顯示「總數/上限」。點清單外面或按 ESC 會先收起下拉。清單超過 7 項可以用滾輪捲動。篩選中的地圖已經沒有書籤時，自動回到 ALL。
   - 篩選和排序可以一起用。
6. **一般模式的開窗入口**：WinForms「記憶座標」分頁加了「原生書籤窗」鈕，按下呼叫 `ToggleNativeBookmarkWindow()` → `Toggle()`＋`BringToFront`。原生 UI 沒開時會顯示提示文字。原版的熱鍵或選單鈕沒查到，所以這是過渡入口（標明）。原生 UI 關閉時，WinForms 分頁本身的新增／刪除仍然可用。
7. **缺件處理**：排序鈕或下拉缺節點、缺圖時，只是不畫那一件，整個窗仍然可用（規則 5）。

## 沒做（待查證）

- **快速移動清單 `QuickMove_List`**（5 列，`String` 0–4）：資料來源沒追。照規則 5 不畫沒有資料的清單，標題也不畫。
- 隨機傳送 `RandomTeleport_Button`、13 色調色盤、`CloningMemoryPlace_List`、ESC 關窗時的 2847「座標記憶結束」：和 #713 一樣仍然不做。
- 下拉的 `Down_Scrollbar` 圖沒有畫，只支援滾輪捲動。

## 怎麼驗證

- **編譯（部分驗證）**：本機 `dotnet build engine/source/OfflineClient.csproj -c Release -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false`。
  - 本機 NuGet 快取裡的 WebView2 1.0.4258.31 套件缺 `build/*.targets`，所以會出 NETSDK1130 錯誤。
  - 改成 restore 到 `scratch/nuget-734`，再用 scratch 內的 targets 拿掉 `.winmd` 參考（只用於本機檢查，不進 repo）之後，剩下的錯誤**全部**在 `WebPanel.cs`／`WebLocalResource.cs`（WebView2 型別，和本次改動無關）。本次改的三個檔案沒有錯誤或警告。
  - 完整編譯以 CI（ubuntu 交叉建置）為準。
- **離線測試**：新增 `[WorldModuleTest("bookmark-window-followup")]`（`BookmarkPanelFollowupTests.cs`），用合成 XML 和 .img 驗：
  - 846 的是／否（鈕、Enter／Y、ESC／N、右鍵）；
  - 送出中、刪除待確認、CanEdit=false 時不能刪；
  - 命名、Backspace、同名或空名被擋、清單已滿；
  - 沒有編輯宿主或在選目的地模式時不能新增／刪除；
  - 排序切換；
  - 篩選的座標、繪製、選取、收起、捲動，以及篩選和排序一起用；
  - 缺件時降級；關窗時清掉暫態。
  
  依本機限制**沒有在本機執行**，交給 CI 跑。既有的 `bookmark-window-panel` 測試沒有改。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `status: pass`，0 錯誤。

## 未驗證

- CI 結果：push 後看 PR。
- 真機：原生 UI 開啟時的 Delete／Insert／右鍵、IME 輸入名稱、C165／C3 實際送出與伺服器回應、下拉蓋在清單上的畫面、846 提示的位置。這些交給 Codex／dot 依 #475 流程驗收。

## 產出路徑

- `engine/source/NativeUi/BookmarkPanel.cs`、`engine/source/BookmarkUi.cs`、`engine/source/BookmarkPanelFollowupTests.cs`
- 本報告 `docs/reports/issue-734.md`
- 分支 `task/734`

## 給下一棒的提示

- 開 Q 類查證任務（可以併進 14.7 建議的 resource 任務）：
  - `sub_BDC1F0` 的呼叫點，用來確認刪除的觸發鍵；
  - 新增書籤時名稱從哪裡輸入（`sub_4FE1D0`／`sub_502720` 的呼叫者）；
  - `Sorting` 的排序依據；
  - `ZoneFilterComboListItem` 的項目來源（區域名稱表）和 String 878；
  - `QuickList` 的資料來源；
  - 書籤窗原版的熱鍵或選單入口。
- 查到之後，只要改 `BookmarkWindowPanel` 的 `Key`／`Mouse` 觸發點和 `ZoneItems`／`Refresh`，宿主介面不用動。
- 真機 smoke 時請特別看：命名中打字是否會漏到聊天輸入列（Window 層在 HitOrder 前面，應該不會）。
