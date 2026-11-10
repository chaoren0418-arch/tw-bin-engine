# Issue #823 T359：R8-D 動作窗

依本機 `ActionUI-c.xml`＋`Actions.xml` 建原生動作窗（新面板，自動發現）；雙擊格子執行；Ctrl+D 開關。依據 FU8§4（`docs/reference/followups-8.md`）與 `gap-matrix.md` 15.10（Q6-4）。

## 做了什麼

- 新檔 `engine/source/NativeUi/ActionWindowPanel.cs`
  - `[UiPanel(191)]` 自動註冊，Id `Action_Window`。宿主版面用 `uistatus86`，`ActionUI`／`Actions` 在 `Load` 裡自己讀。讀不到或格式不對時只停用本窗（`Unavailable`），其他原生 UI 照常。
  - 實作 `IUiMovablePanel`、`IUiClosablePanel`（堆疊擠出和全域 ESC 都會關它）。
  - `ActionCatalog.Parse`：讀 Actions.xml 的 action／category／control／icon／target／domain。值不合法時丟 `UiLayoutException`。
  - 版面（XML 上看得到）：
    - 底圖 5141、外框 5142，Moveable 20；
    - 格子 (21,45)，4 列 × 6 欄，格子 32 px、間距 1；選取框 5333；
    - 四個分頁鈕 Total／Community／Action／Management；關閉鈕 (228,11)；捲軸（Up／Down／Move／底圖）。
  - 操作：
    - 單擊選取，**雙擊執行**；
    - 按下分頁鈕就換頁（捲動與選取歸零）；
    - Up／Down 鈕和滾輪一次捲一列；
    - 關閉鈕關窗；離開世界時關窗。
  - 執行順序：
    1. 先問指令物件的 `IUiActionCommands.RunAction`（新介面）；
    2. `Action_OpenBookmarkWin` → 用 `UiRoot.Toggle` 開關原生書籤窗；
    3. `Action_SocialAction1–4` → 開發工具「稱號／表情」分頁（過渡做法）；
    4. 其他動作不送任何封包，只記一次 log（`Unhandled`）。
- `engine/source/NativeUi/MenuButtonsPanel.cs`（只接 Ctrl+D）
  - `Key()` 收到 `ActionWindowPanel.Hotkey`（Ctrl+D）時，用 `ToggleWindow`（也就是 `UiRoot.Toggle`）開關動作窗。
  - 沒有可用的動作窗時不攔這個鍵，和以前一樣。
  - Ctrl+D 沒有放進 `CommandHotkeys`，因為 `MenuHotkeyTests` 檢查那張表要剛好 7 個鍵。
- 新檔 `engine/source/NativeUi/ActionWindowTests.cs`：模組測試 `action-window`（`[WorldModuleTest]` 自動發現）。只用合成版面、合成 surf 和合成圖示，不讀客戶端。

## 資源查證（PakViewer CLI，唯讀；輸出在 `scratch/issue-823/`，不進 git）

- 解出的版面檔（本機 `0-客戶端\Tile.idx`）：

  | 檔案 | 大小 | 解密後 SHA-256 前 16 碼 | 說明 |
  |---|---|---|---|
  | `ActionUI-c.xml` | 1,900 B | `ae467982a6880161` | 與 FU8§0 相同 |
  | `ActionUI.xml` | 2,181 B | `3387e7a16e417d69` | |
  | `Actions.xml` | 3,330 B | `20c180afe54e6e10` | 與 FU8§0 相同 |
  | `Actions-c.xml` | — | — | 不存在，引擎會退回讀 `Actions.xml`（會記 `ui-xml-fallback`） |

- 版面圖：用 `SurfList.xml` 對照 `Sprite.idx`、`Sprite00`–`Sprite15.idx` 共 17 個 idx 的 `pak list`，結果在 `scratch/issue-823/surf-check.tsv`。
  - 5141–5157、5333、2040–2045、1962／1963 都有 `.img`。
  - 5141c.img 與 5142c.img 都是 243×195；分頁鈕 50×23；選取框 34×34；關閉鈕 8×8。
- **動作圖示**：Actions.xml 的 25 個 icon 編號在本機都有 `<n>.tbt`（大約 1.9 KB），分散在 Sprite01–15。
  - 同號的 `.img` 大小不一（12×12、37×22、67×54、62×19、8×8……），看起來是別的 UI 圖。
  - 5363（Action_Loot 的圖示）沒有任何 `.img`：本機 24 個 idx 全部查過（Sprite、Sprite00–15、Text、Tile、LiTo\LT*），kor25 與雷雨天堂的 17 個 Sprite idx 也只有 `5363.tbt`／`5363c.tbt`。
  - 所以圖示照背包圖示的查法讀 `<icon>.tbt`（`ItemIconSource`）。這個對應是**推測**。
- TW16 用的 `BehaviourUI.xml` 本機沒有（FU8§4.1 已查過 24 個 idx）。本張不使用。

## 怎麼驗證

- 編譯：用 CI 的 `nuget.ci.config` restore，`NUGET_PACKAGES` 放在 `scratch/issue-823/packages`。
  - 先刪掉 `engine/source/obj/` 下 5 個 NuGet 輸出檔（逐一刪單檔，沒有遞迴刪除）。
  - 然後執行 `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o engine/build/task823-check`：**0 錯誤**，新檔和改過的檔都沒有警告（3 個警告是既有的 `MagicUi.cs`、`HtmlActionTests.cs`）。
  - 本機一開始的 restore 出現 NETSDK1130（WebView2 winmd），和程式無關，#676／#677 也遇過。
  - `rebuild.py --build` 在本機 restore 時失敗（離線 vendor 的環境問題），沒有改 lock。
- `py -3 engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。
- 離線測試 `action-window`：依規定**本機不執行**，交給 GitHub Actions CI 的 `--module-tests`。結果以 PR 的 CI 為準。
  - PR #843 第一次 CI：`dotnet`、`npc-offline` 綠；`map-continuity` 的模組測試 138 項中只有 `action-window` 失敗 1 個檢查。原因是測試本身的預期值寫錯：3 欄時捲一列後第一格是第 4 個動作，不是第 7 個。產品程式沒有改，只修了測試，修正後重跑 CI。
  - 測試涵蓋：
    - Actions 解析與錯誤值；
    - 版面座標（分頁、捲軸相對於格子、關閉鈕、開窗鈕資料）、缺圖清單；
    - 底圖與外框都缺時停用；沒有 UiResources 時不丟例外；
    - 分頁篩選、捲動上限、拇指位置；
    - 繪圖像素（底圖、分頁三種狀態、圖示、缺圖不畫、選取框、按下的狀態圖）；
    - 單擊選取、雙擊執行（橋接優先、拒絕時退回、經 `UiMacroCommands` 也找得到）；
    - Ctrl+D 經 `UiRoot.Toggle` 開關、放開鍵與連發鍵的處理、全域 ESC、窗不可用時不攔鍵；
    - 自動發現註冊。

## 未驗證什麼（待驗證／推測）

- **3.81 是否用 `ActionUI-c.xml`＋`Actions.xml`**：推測（FU8§4.1）。版面、開窗鍵都要真機再確認。
- **Ctrl+D**：TW16 用 Ctrl+D 開 BehaviourUI（FU4§1.1）。3.81 是否同一個鍵待驗證。
- **推測的版面規則**：
  - 視窗位置：水平置中、y＝150（同 QUIT_WINDOW 的規則）；
  - 捲軸的座標相對於格子：換算後 (223,53)–(235,189)，剛好落在 243×195 內；
  - 選取框畫在格子的 (−1,−1)；
  - 分頁 PushSurf 表示選中；
  - 開窗預設在 Total 頁；
  - 拇指依捲動比例移動；
  - 圖示來源是 `.tbt`。
- **只做資料、不畫也不收點擊**：
  - 常駐開窗鈕 `ActionUI_Button` (774,470) 5156／5157 與提示字串 3118（是否常駐待驗證）；
  - 格子的 Drag="1"（把動作拖到巨集列）；
  - toggle 類動作的勾選狀態；
  - XML 的 MaxValue／ScrollStart／ScrollHeight。
- **還沒做**：拖拇指、點軌道捲動、control="target" 的「指定對象」模式。
- **Esc**：ActionUI 的 KeyboardEvent 內容沒查，所以本窗不自己攔 Esc，交給全域 ESC（R7-H）關窗。
- **產品端的執行還沒接**：`NativeUiCommands`（`UiClientBridge.cs`）不在本張可以改的檔案裡，還沒實作 `IUiActionCommands`。所以在產品裡雙擊：
  - 只有書籤窗與表情（開到開發工具分頁）有反應；
  - 其他 21 個動作只記 log，不執行。
- 真機畫面（圖示是否就是 `.tbt`、位置、分頁圖）待 Codex／dot 驗收。

## 產出路徑

- `engine/source/NativeUi/ActionWindowPanel.cs`（新）
- `engine/source/NativeUi/MenuButtonsPanel.cs`（只接 Ctrl+D）
- `engine/source/NativeUi/ActionWindowTests.cs`（新）
- `docs/reports/issue-823.md`（本報告）
- 本機查證輸出（不進 git）：`scratch/issue-823/`（XML 解密檔、Sprite 清單、`surf-check.tsv`、圖檔標頭）

## 給下一棒的提示

1. **接橋接**（建議開新 code 任務，可以改 `NativeUi/UiClientBridge.cs`）：讓 `NativeUiCommands` 實作 `IUiActionCommands.RunAction`，依 FU8§4.3 對應：
   - `Action_SocialAction1–4` → `login.SendEmote`（`TitleAndEmote.cs`，item 0–3）；
   - `Action_Loot` → F4 拾取（R6-D）；
   - `Action_Trade` → `Trade.cs`；
   - `Action_OpenMacroWin`／`OpenPetManagementWin` → 既有視窗；
   - toggle 類動作 → 既有選項；
   - 接不到的回 `false`，面板會自己退回。
2. 真機（P24Lab）核對：
   - Ctrl+D 是否開這個窗；
   - 畫面位置；
   - 25 個圖示是否就是 `<icon>.tbt`；
   - `ActionUI_Button` 是否常駐在 (774,470)。
3. 若 3.81 其實用 BehaviourUI：版面檔要從 kor25 取（P-A／P-B），`ActionCatalog` 和執行邏輯可以沿用。
