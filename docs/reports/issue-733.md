# Issue #733 — T306：R5-A 原生 UI 開關收尾

分支：`task/733`（從 origin/main `632f98a0` 建立）。依據：`docs/reference/gap-matrix.md` 14.6 節 R5-A、14.2 第 4 點、第 5 節 `sub_A57C60`；`docs/reports/issue-677.md` 第 1 節。

## 做了什麼

### 1. 選單鈕 5／6（與 Ctrl+H／V／C）不再多開開發工具（`NativeUi/UiClientBridge.cs`）
- `ToggleMenuLocalPage` 原本在顯示本機 HTML 頁後**一律**呼叫 `ShowDevToolsPage("NPC")`（14.2 第 4 點）。
- 改成先問新的 `ClientForm.MenuLocalPageDevTools(bool)`：
  - 原生 `HYPER_TEXT_WINDOW` 可用時回 `null`，不開開發工具。可用的條件是 `NpcUi.cs` 既有的 `NativeHyperText()`：原生 UI 已啟用、面板已註冊、而且沒有故障。
  - 不可用時回 `"NPC"`，維持舊路徑。
- 頁面本身仍由既有的 `SyncNativeHyperText` 送進原生窗，這部分沒有改。

### 2. `sub_A57C60` 的 509 特例（`NativeUi/UiRoot.cs`）
- 原版：`*dword_138BE60 == 509` 時，拒絕把 `SpellWindow`（`dword_1541DF8`）推入堆疊（windows-2.md 2.1 第 4 點、#677 報告）。
- 新增 `UiRoot.CurrentMap`，由橋接層的 `ComposeNativeUi` 在每格 `Update` 前設定：在世界中是 `WorldSession.MapId`，不在世界中是 `null`。
- 新增 `UiRoot.RefusesPush(panel, map)`：只有在地圖 509 時，`SPELL_WINDOW` 才會被拒絕。
- `SyncWindows` 看到新打開的窗被拒絕時：
  - 可關的窗（`SpellWindowPanel` 屬於 `LegacyWindowPanel`，可關）：在同一次同步裡呼叫 `Close()` 關回去，所以不會被畫出來，也點不到。
  - 不可關的窗：藏起來，和被擠出的窗一樣處理，直到它自己關掉。
  - 兩種情況都會寫 `UiResourceLog`，並觸發 `WindowRefused` 事件。
- 已經在堆疊裡的窗不受影響。原版只在推入時判斷，所以在其他地圖打開魔法窗後再傳到 509，窗會保持開著。

### 3. 「再開一次＝關掉」移到 root 層（`UiRoot.Toggle`）
- 新增 `UiToggleResult Toggle(IUiPanel panel, Action? open = null)`，照 `sub_A57C60` 的單窗行為：
  - 窗在堆疊中：呼叫 `IUiClosablePanel.Close()`（vtable+36），堆疊往前補，回傳 `Closed`。
  - 窗不在堆疊中：
    - 先檢查 509 規則，被拒絕時回傳 `Refused`，`open` 不會被呼叫。
    - 窗其實還開著、只是被藏起來（被擠出或被拒絕過）：直接拉回最上層，不重開，必要時擠掉最舊的窗。這正好解決 #677 記下的 FieldMap 問題：被擠出後第一次按鍵看起來沒反應。
    - 其他情況：呼叫 `open` 打開窗，推到最上層（vtable+32），回傳 `Opened`。
  - 不是 Window 圖層、沒註冊、已故障、在堆疊中但不可關、或沒有提供 `open` 時：回傳 `Unavailable`。`open` 丟出例外時，該面板標成故障。
- 檔案範圍限制：既有面板各自的切換寫法（`LegacyWindowPanel.Toggle`、`FieldMapPanel` 的 Ctrl+M、`MenuButtonsPanel.Invoke`）在本任務可改的檔案之外，所以**這次沒有改成呼叫 `UiRoot.Toggle`**。
  - 背包、魔法、倉庫三個舊式窗可以被關掉，被擠出時本來就會真的關掉，所以它們面板內的 `Toggle` 已經等於原版的切換。
  - 不可關的窗（FieldMap 等）要改走 `UiRoot.Toggle`，才會和原版完全一致（見「給下一棒的提示」）。

### 4. 測試（新檔 `engine/source/NativeUi/UiStackFollowupTests.cs`，`[WorldModuleTest("native-ui-stack-followup")]`）
共 40 項檢查，只用合成面板，不建 `ClientForm`、不讀客戶端資源：
- 推入規則：地圖 509 只擋 `SPELL_WINDOW`；其他地圖、不在世界中、其他窗都照常推入。
- 地圖 509 自己打開的可關魔法窗：
  - 第一次 hit test 就被關回去，不進堆疊，`WindowRefused` 只觸發一次，有寫記錄。
  - 重複同步結果不變。
  - 已經開著的魔法窗傳到 509 後仍然開著。
- 不可關的窗被拒絕：
  - 被藏起來；在 509 時 `Toggle` 回傳 `Refused`，而且不會開窗。
  - 離開 509 後不會自己回來；`Toggle` 讓它直接回到最上層，不重開。
  - 自己關掉後，就不再算被拒絕。
- `Toggle` 的開、關、再開：
  - 堆疊會往前補；第 5 個窗會擠掉最舊的。
  - Bar 圖層、沒註冊的窗、沒有 `open`、`open` 丟例外：都回傳 `Unavailable`，丟例外的面板會標成故障。
- 被擠出、不可關的窗：`Toggle` 直接把它拉回最上層，並擠掉當時最舊的窗；它在堆疊中時不能被 `Toggle` 關掉。
- `ClientForm.MenuLocalPageDevTools`：原生頁窗可用時回 `null`，不可用時回 `"NPC"`。

## 怎麼驗證
- **編譯（已驗證）**：在本機對 `engine/source/OfflineClient.csproj` 執行 `dotnet build -c Release -r win-x64`，結果 **0 錯誤**；3 個警告都是既有的，不在這次改動的檔案裡。
  - 本機環境問題，與 #677 相同：使用者家目錄下的 NuGet 快取裡，WebView2 套件不完整（只有 winmd），會出現 NETSDK1130。
  - 編譯時暫時加上只放在 scratch 的 `-p:CustomAfterMicrosoftCommonTargets=scratch/fix733.targets`，改成參考原專案 `engine/.nuget` 完整解壓的 `lib_manual` DLL（只讀）。這個檔案沒有入 git。
  - restore 改用 `.github/nuget.ci.config`，因為本機離線 vendor restore 失敗（`rebuild.py --build` 回報 product-restore 失敗）。
- `py -3 engine/tools/check-no-game-assets.py`：pass（4077 個追蹤檔，0 錯誤）。
- **單元測試與離線測試**：依本機執行限制，沒有在本機執行，交給 PR 的 CI 跑：`--module-tests` 會跑新的 `native-ui-stack-followup`，以及既有的 `native-ui-window-stack`、`menu-hotkeys`、`hypertext` 等。
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`，也沒有碰 T290／T273 使用中的檔案。

## 未驗證／推測
- **待驗證（CI）**：新測試與既有測試在 CI 是否全綠，以 PR 的 CI 結果為準。
- **待驗證（真機，交給 Codex／dot）**：
  1. 開 `--native-ui`，按選單鈕 5／6：只出現原生 `HYPER_TEXT_WINDOW`，不再跳出開發工具視窗；同一顆鈕再按一次，頁面會關掉。
  2. 到地圖 509 按選單 3 或 Ctrl+S：魔法窗不出現；在其他地圖打開後再傳到 509，窗仍然開著。
- **推測**：
  - `*dword_138BE60` 取值後就是地圖編號，這點來自反編譯的型別推斷，以及 followups-3.md 3.1 傳送限制地圖讀的是同一個全域變數。
  - 引擎的 `SPELL_WINDOW`（uimagicitem 舊式版面）對應原版 `SpellWindow`。
  - 原版被拒絕時會不會顯示訊息沒有查證；引擎不顯示任何訊息。
- **差異**：
  - 不可關的窗被拒絕而藏起來時，不會收到按鍵。`UiInput` 只把按鍵送給被擠出的窗。目前只有 `SPELL_WINDOW` 會被拒絕，而且它可以關，所以實際上沒有影響。
  - 原版 `a1 == 0`（四個窗全關）沒有做：沒有找到呼叫點，也不在 R5-A 的範圍內。

## 產出路徑
- `engine/source/NativeUi/UiClientBridge.cs`
- `engine/source/NativeUi/UiRoot.cs`
- `engine/source/NativeUi/UiStackFollowupTests.cs`（新增）
- `docs/reports/issue-733.md`（本報告）

## 給下一棒的提示
- 不可關的窗（`FieldMapPanel`、`SummonPanel`、`QuitWindowPanel`、`PersonalShopPanel`、`LocationListPanel`、`FriendPanel`、`AgitMapPanel`、`BoardPanels`、`UserPlayStatsPanel`）的開關熱鍵，可以改成呼叫 `root.Toggle(this, open)`；要先實作 `IUiClosablePanel`，root 才能關掉在堆疊中的窗。
  - 這樣做之後，被擠出後按第一次鍵就會回到最上層，和原版一致。
  - 這些都是各面板自己檔案的小改動。
- `MenuButtonsPanel.Invoke` 的 `LegacyWindowPanel` 分支，可以改成 `root.Toggle(w, w.Open)`，讓 509 規則在開窗前就判斷，不必等同步時再關回去。結果相同，只是少開一次。
- `gap-matrix.md`：
  - 第 5 節 `sub_A57C60` 可以改成「4 層上限、擠掉最舊的窗、BringToFront、509 特例、root 層切換都已做；面板各自的熱鍵還沒改走 root；預設關閉」。
  - 4.1 MainButtonUI 的「5／6 多開開發工具」可以拿掉。
- 本機編譯如果再遇到 NETSDK1130，可以照本報告的暫時 targets 做法處理（改參考原專案 `engine/.nuget` 裡完整的 WebView2 解壓檔，只讀）。
