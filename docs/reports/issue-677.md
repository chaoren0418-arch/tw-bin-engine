# Issue #677 — T284：R3-I 視窗堆疊 4 層上限

分支：`task/677`（從 origin/main `508604c2` 建立）。依據：`docs/reference/gap-matrix.md` 第 12.5 節 R3-I。

## 做了什麼

### 1. 先查原版規則（已驗證，IDA 靜態反編譯）
用 `docs/reference/rea-scripts/t665/ida_t665.py`，在 scratch 複製的 IDB 上反編譯（輸出只在 `scratch/rea/`，未入 git）：

| 函式 | VA／FO | 行為 |
|---|---|---|
| `sub_A57C60` | `0xA57C60`／`0x657060` | 參數 `a1`：<br>• 不在 `dword_153D220[4]` 裡：如果 `[3]` 有窗，先關掉它（vtable+36）；接著 `[2]→[3]`、`[1]→[2]`、`[0]→[1]`，`a1` 放進 `[0]`，再開窗（vtable+32）。<br>• 已經在裡面：關掉它，並把後面的往前補（**是切換開關**）。<br>• `a1 == 0`：四個都關。<br>• 特例：`*dword_138BE60 == 509` 時拒絕推入 `dword_1541DF8`（`windows-2.md` 指出是 SpellWindow）。 |
| `sub_A58B50` | `0xA58B50` | 在陣列裡找 `a1`，回傳索引，找不到回傳 −1 |
| `sub_A58B10` | `0xA58B10` | 判斷 `a1` 是否在陣列裡 |
| `sub_A596B0` | `0xA596B0` | 移到最前面：先從原位置拿掉並補位，再推到 `[0]`；其他窗的相對順序不變 |
| `sub_A58A80` | `0xA58A80` | 清空陣列（不呼叫關窗） |
| `sub_8CA740`／`sub_8CAB40` | — | 鍵盤（+40）與滑鼠（+44）事件依 `[0]`→`[3]` 的順序分送，`[0]` 在最上層 |
| `sub_A59AB0`／`sub_A59AD0` | — | 包裝 `sub_A57C60`（`A59AD0` 另外播音效 237） |

結論：`[0]` 是最新或最上層，`[3]` 是最舊的。開第 5 個窗時，關掉的是 `[3]`，也就是**最底層、最久沒被拉到前面的那個**。

### 2. 引擎實作（`engine/source/NativeUi/UiRoot.cs`、`UiInput.cs`）
- 引擎的面板自己管理開關（`Visible` 是唯讀屬性，`IUiPanel` 沒有 Open／Close），所以 `UiRoot` 改用**觀察**的方式：Window 圖層的面板，只要 `Visible` 為真且 `Bounds` 不是空的，就算開著（`FieldMapPanel` 關閉時會保持 `Visible`，但 `Bounds` 是空的）。
- `UiRoot.SyncWindows()` 的行為：
  - 已關閉或已故障的窗離開堆疊。
  - 新開的窗放到 z-order 最上層。
  - 開著的窗超過 `MaxWindows = 4` 時，把最底層的窗**擠出去**。面板如果有實作新的 `IUiClosablePanel.Close()`，就呼叫它（對應 vtable+36）；沒有的話，把它標成 evicted：不畫、不能點，直到面板自己關閉為止。之後再開，會出現在最上層。
  - 擠出時寫入 `UiResourceLog`，並觸發 `WindowEvicted` 事件。
- 呼叫時機：`Update` 結束時、`PaintOrder`（`Compose`、`HitTest` 都會經過這裡），以及 `UiInput.Key` 一開始。這個函式可以重複呼叫，結果不變。
- `Usable()` 多加一條：被擠出的窗不算可用。所以 `UiInput` 原本的拖曳、capture、focus 檢查，會自動放掉被擠出的窗。
- `BringToFront` 的行為不變（等於 `sub_A596B0`：只把該窗移到最上層，其他窗順序不變）。之後再擠出時，挑的是「最底層」，不是「最早開的」，和原版一致。
- `WindowStack` 依原版陣列順序列出開著的窗（`[0]` 在最上層），測試和除錯用。
- `UiInput.Key`：被擠出的窗排在**最後**才收到按鍵。原因是引擎的開關熱鍵寫在面板裡（例如 Ctrl+M 在 `FieldMapPanel`），被擠出後仍要能用熱鍵關掉或重開。

### 3. 測試（`engine/source/NativeUi/WindowStackTests.cs`，`[WorldModuleTest("native-ui-window-stack")]`）
CI 的 `--module-tests` 步驟會自動跑所有 `[WorldModuleTest]`；DemoChecks 新群組不會被 CI 呼叫，所以測試改放產品端。共 28 項檢查，只用合成面板：
- 開到第 5 個窗時擠掉最舊的，順序是 E,D,C,B。被擠出的窗不畫、不能點，有寫記錄；重複同步結果不變。
- `BringToFront(B)` 後順序是 B,E,D,C。第 6 個窗擠掉的是 C，不是被拉到前面的 B。
- 窗自己關閉後，堆疊會往前補；被擠出的窗不會自動回來。被擠出的窗自己關掉後可以再開，會出現在最上層，並擠掉當時最舊的窗。
- 有實作 `IUiClosablePanel` 的窗會被呼叫 `Close()`，真的關掉。
- `Bounds` 為空的窗不佔位子；Bar 圖層不受上限影響。
- 沒呼叫 `Update` 時，第一次 hit test 也會同步，依註冊順序處理；故障的窗會離開堆疊。
- `UiInput`：點擊下層窗會把它拉到最上層；被擠出的窗仍會收到自己的開關熱鍵（類似 FieldMap 的寫法），關掉後再開會回到最上層；沒有面板處理的按鍵交回遊戲。

## 怎麼驗證
- **編譯（已驗證）**：在本機對 `engine/source/OfflineClient.csproj` 執行 `dotnet build -c Release`，結果 0 錯誤；改動的檔案沒有新警告（3 個既有警告都在 `MagicUi.cs`）。
  - 本機環境問題：這個 worktree 執行 restore 時，NuGet 看不到 WebView2 套件的 `.dll`／`.targets`，造成 NETSDK1130。編譯檢查時暫時加了只放在 scratch 的 `-p:CustomAfterMicrosoftCommonTargets=scratch/fix677.targets`，把 winmd 參考換成套件 `lib_manual` 裡的 DLL。這個檔案沒有入 git，產品專案沒有改動。
- `python engine/tools/check-no-game-assets.py`：pass（3976 個檔案，0 錯誤）。
- **單元測試與離線測試**：依本機執行限制，沒有在本機執行，交給 PR 的 CI（`--module-tests` 會跑 `native-ui-window-stack`，DemoChecks 的 `native-ui-canvas` 會跑既有的視窗順序測試）。

## 未驗證／限制
- **待驗證（CI）**：新測試與既有 `native-ui-canvas`、各面板測試在 CI 是否全綠，以 PR 的 CI 結果為準。
- **待驗證（真機，交給 Codex／dot）**：開 `--native-ui` 後，同時開 5 個視窗，看最舊的那個是否消失。
- **推測／差異**：
  - 原版第 5 窗會真的關掉最舊的窗。引擎目前的面板都還沒實作 `IUiClosablePanel`，所以只會隱藏，面板內部狀態仍是開著（例如交易窗的 `view` 還在）。要完全一致，需要各面板模組實作 `Close()`，這些檔案不在本任務範圍。
  - `FieldMapPanel` 被擠出後，按第一次 Ctrl+M 是「關掉」（看起來沒反應），第二次才會在最上層重新開啟。原版是透過 `sub_A57C60` 的切換邏輯直接處理。
  - 原版「再開一次已經開著的窗就是關掉」（切換）沒有做在 root 層，因為引擎的切換各自寫在面板裡。
  - 原版 SpellWindow 在狀態 509 時拒絕推入的特例沒有做，引擎還沒有對應的面板與狀態。
- `gap-matrix.md` 第 5 節與第 12.5 節的狀態沒有更新（不在可改檔案範圍），請下一棒更新。

## 產出路徑
- `engine/source/NativeUi/UiRoot.cs`（視窗堆疊、`IUiClosablePanel`）
- `engine/source/NativeUi/UiInput.cs`（按鍵分送）
- `engine/source/NativeUi/WindowStackTests.cs`（新增，`[WorldModuleTest]`）
- `docs/reports/issue-677.md`（本報告）

## 給下一棒的提示
- 各視窗模組（Trade、Warehouse、Pledge、Summon、Friend、Board…）如果實作 `IUiClosablePanel.Close()`，被擠出時就會真的關掉，和原版一致。這是各面板自己檔案的小改動，可以併進各自的後續任務。
- `gap-matrix.md` 第 5 節 `sub_A57C60` 可以改成「部分 → 大致一致（4 層上限、擠掉最舊、BringToFront；缺切換與 509 特例；預設關閉）」。
- 本機 restore 看不到 WebView2 `.targets` 的問題，會影響之後在 worktree 本機編譯；可以參考上面的暫時做法，或檢查 NuGet 快取與防毒設定。
