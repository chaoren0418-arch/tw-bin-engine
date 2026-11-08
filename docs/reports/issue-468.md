# Issue #468 ui-chat-window：原生 UI 聊天窗 CENTER_WINDOW

runner：claude-local（第 1 次嘗試）。分支 `task/468`。依據 `docs/design/native-ui.md` §5.3、`docs/reference/ui-layout.md` §3。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/ChatPanel.cs`（新） | `CENTER_WINDOW` 面板（`UiLayer.Bar`）。所有座標都在執行期從 `uistatus86(-c).xml` 讀取（視窗 x/y＋xRPos/yRPos，底圖用絕對 `yPos`）；缺節點丟 `UiLayoutException`，缺 surf 的元件不畫、也不能點，不畫替代圖 |
| `engine/source/NativeUi/State/ChatState.cs`（新） | 純邏輯：`ChatHeights`（伸縮階）、`ChatRules`（分頁／過濾對照、換行、文字區常數）、`ChatViewState`（分頁、過濾、分割、高度、捲動）、`ChatInputLine`（輸入列）、`IUiChatCommands`／`IUiTextInput` 介面、`UiChatGameState` 轉接（包裝既有 `PlayerChatView`，**不新增封包解析**） |
| `engine/_integration/dev/native-ui-tests/ChatWindowChecks.cs`（新） | 用程式合成的 XML 與 `.img` 做離線檢查（輸出只在 `scratch/issue-468/synthetic`）；選用的真實客戶端渲染（需 `TW_CLIENT_ROOT`，PNG 只寫 `scratch/issue-468/real`） |
| `UiPanelRegistry.cs` | 加一行 `new("CENTER_WINDOW","uistatus86",root=>new ChatPanel())` |
| `UiClientBridge.cs`（**範圍外，理由如下**） | 3 處小改：①`NativeUiGameState` 回傳 `UiChatGameState`（帶 `login.PlayerChatSnapshot()` 與 `gameFocus`）；②`NativeUiCommands` 改實作 `IUiChatCommands`：`BeginChatInput`＝`OpenPlayerChat` 的同一套 `PlayerChatFlowRules.CanOpen`＋`LeaveGameFocus`、`EndChatInput`＝`ReturnGameFocus`、`SendChat`＝核對 stamp 後呼叫既有 `login.SendPlayerChat`；③開關打開時掛 `KeyPress`，把 WM_CHAR（含 IME 組字後的字）交給 `IUiTextInput` 面板 |
| `DemoChecks.csproj`／`demo-tests/Program.cs` | 各加一行；新分組 `native-ui-chat-window`，並加進預設全組 |
| `rebuild-lock.json` | `rebuild.py --update-lock` 重產（再次同步 main（#557）後為 277 個來源檔／286 個鎖定檔），未手動改 hash |

**為何動 `UiClientBridge.cs`：** C2 的 `IUiGameState` 沒有聊天欄位、`IUiCommands` 沒有送出聊天，`UiKeyEvent` 也收不到字元。原生模式下舊 HUD 被隱藏，`ProcessPlayerChatKeyboard` 因 `playerHud.Visible==false` 直接略過，所以不接這座橋，聊天窗只能看不能打字。做法照 #464（ui-left-status）的先例：用裝飾器包 `IUiGameState`，只多一行；送出與焦點完全轉呼叫既有流程。開關關閉時這些程式都不會執行。

### 行為摘要

- **伸縮：** 高度階 = 130、166、202、238、240（最小 = `yPos 600 − y 470`，每階 `stepSize 36`，最後一階夾到底圖高 240）。視窗底邊固定在 600，往上長；`Bounds = (129, 600−h, 362, h)`。上下鈕同在 (345,115)：未達最大時顯示 `CHAT_UP`、點一下長一階；到最大顯示 `CHAT_DOWN`、點一下回到 130。
- **錨定：** 輸入列、上下鈕、8 個頻道分頁、捲動下鈕固定在底座（y=470 起算）；過濾圖示、外框、分割圖示、捲動上鈕跟著上緣移動。
- **底圖：** 3279 與外框 `CHAT_BORN` 3280（內部是透明色鍵，含標題列）從視窗上緣畫起，顯示上方 h 列。
- **頻道分頁（8 個，x=8、y=10–115、間距 15）：** 點選切換，選中的畫 `_ON`，其他畫 OFF。只改變顯示。
- **過濾圖示（y=5：179／198／221／240／258）：** 點一下切換開關，開＝`*_ON` 元素、關＝無後綴元素。只改變顯示；不送任何封包、不動聊天模型（有測試）。
- **左右分割：** 點 `CHAT_ICON_ACTIVE` (299,5) 切換；分割時畫 `LCHAT` 3288（寬 260）＋`RCHAT` 3290（x=260，真實寬 259），視窗寬 519；密語進右欄。
- **捲動與行數上限：** 最新一行在最底；滾輪 3 列、捲動鈕 1 列；捲到最上停在「總列數 − 可見列數」。往上捲時有新訊息，畫面停在原位；在最底時跟著新訊息走。最多保留 100 行（與 `WorldSession` 相同上限）；長行依寬度換行。可見列數：7／10／13／16／16（對應五階高度）。
- **輸入列（沿用 `PlayerChatKeyboardFlow`）：** 在世界中按 Enter →`BeginChatInput`（同 `OpenPlayerChat` 規則＋讓出遊戲焦點）；開啟用的 Enter 放開前不會送出（`PlayerChatEnterPress`）；之後按 Enter 只送合法內容（`PlayerChatProtocol` 解析＋編碼）；送出成功才清空，輸入列保持開啟（同舊 HUD）；Esc 關閉並還焦點；Backspace 刪字；最多 106 字；點輸入列也能開啟；焦點被別處拿回（例如點世界）或離開世界時自動關閉。可列印的鍵不在 `Key()` 吃掉，讓 WM_CHAR（含中文 IME）照常產生。
- 行的文字與顏色直接用共用的 `PlayerChatProtocol.FormatLine` 與 `PlayerHudChatPanel.ChannelColor`，ui-chat-format 改格式時新舊 UI 會一起變。

## 怎麼驗證

| 指令 | 結果 |
|---|---|
| `git merge origin/main`（帶入 #549 C26 聊天頻道開關，沒有衝突） | — |
| `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` | `dev-inputs-verified`，277／286 |
| `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config` | `PASS dev`，productDll `b1f067fa…69daf29`（同步 #557 後）（worktree 沒有 `engine/vendor`，照 #464 改用 CI NuGet 設定） |
| `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore --no-build -- . scratch/issue-468/demo.json native-ui-chat-window`（無 `TW_CLIENT_ROOT`） | **PASS 443** |
| 同上，`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`（唯讀） | **PASS 444**（多 1 項真實版面檢查） |
| `native-ui-canvas` | PASS 429 |
| `native-ui-resources`（無 `TW_CLIENT_ROOT`） | PASS 539 |
| `item-status` | PASS 409 |
| `player-chat-tests`（`PlayerChatChecks.csproj`） | PASS 138 |
| `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` | pass，errors 0（未新增 `.img`／UI `.xml`／`.png`） |

離線檢查涵蓋：五階高度的 `Bounds`、底圖與外框的位置與上緣列、固定／跟上緣元件的座標、每階可見列數、上下鈕循環；8 個分頁各自的位置、`_ON` 狀態與顯示內容；5 個過濾的開關與隱藏內容、點擊不送指令；分割的兩張底圖、寬度與右欄密語；滾輪／捲動鈕／上限／新訊息時的捲動行為；150 行只保留 100 行；換行；輸入列的開啟條件、Enter 按住、空白不送、送出成功／失敗、Esc、Backspace、106 字上限、焦點被拿回時關閉、離開世界；不同原點與 stepSize 從 XML 讀取；缺 surf 不畫也不能點；缺節點與 y 不合理時拒絕整個面板。

真實客戶端渲染（只在 `scratch/issue-468/real/`，未進 git）：`canvas-h130/166/202/238/240.png`、`canvas-split-h130.png`、`canvas-tab-blood.png`、`canvas-filter-general-off.png`、`chat-window.json`（元件座標表、繪製清單、surf 尺寸）。目視：標題列與 5 個過濾圖示、分割鈕對齊；文字在分頁右側；輸入列與上下鈕落在底部。真實 surf 尺寸：3279／3280 362×240、3288 260×240、3290 **259**×240、2273 346×16、2274／2277 10×14、頻道分頁 32×15、過濾圖示 18×18、外框 3285 123×20。

真實版面座標表（h=130，邏輯座標）：

| 元件 | 座標 |
|---|---|
| 視窗 `Bounds` | (129, 470, 362, 130)；h=240 時 (129, 360, 362, 240) |
| `CHAT_BAR` 2273 | (139, 584) |
| `CHAT_UP`／`CHAT_DOWN` | (474, 585) |
| 頻道分頁 GENERAL…UPPER | (137, 585)、(137, 570)、…、(137, 480) |
| 過濾圖示 一般／隊伍／血盟／交易／系統 | (308, 上緣+5)、(327, …)、(350, …)、(369, …)、(387, …) |
| `CHAT_ICON_FRAME` | (293, 上緣+4) |
| `CHAT_ICON_ACTIVE` | (428, 上緣+5) |
| 捲動上鈕／下鈕 | (475, 上緣+30)／(475, 577) |
| 文字區 | x 173–473（分頁 8＋32＋4），底列下緣 y=582，每列 12 px |

## 已驗證／推測／待驗證

- **已驗證（引用 T161 與 XML）：** 視窗 (129,470)、stepSize 36、底邊 600、最大高 240（surf 高）、上下鈕 (345,115)、輸入列 2273 (10,114)、8 個分頁座標、過濾圖示 x 與 y=5、LCHAT 3288 寬 260、RCHAT 3290 位於 x=260。
- **推測（集中在 `ChatState.cs` 常數或註解，查證後只改一處）：**
  - 第 5 階是 240（130＋36×3＝238 之後再夾到 240）還是停在 238。
  - 單一上下鈕的行為（長到最大後回到最小）。
  - 哪些元件跟上緣、哪些固定在底座。
  - 底圖與外框從視窗上緣畫起（依據：3280 的標題列在上方，過濾圖示在 y=5）。
  - 文字區（左界 = 分頁右緣＋4、右界 344、上 20、下 112、列高 12）、文字陰影、輸入字色、游標閃爍 500 ms。
  - 分頁的顯示對照（一般＝全部、血盟＝血盟＋血盟公告、隊伍＝隊伍＋聊天隊伍、全體、交易、同盟、上級＝盟主；「頻道」分頁在模型裡沒有對應類型，顯示空白，不猜）。
  - 過濾對照（一般＝一般／喊話／全體、隊伍、血盟＝血盟／公告／盟主／同盟、交易；密語不受過濾）。`*_ON` 元素名稱與 surfName 互相矛盾（`GENERAL_CHAT_ON` 的 surfName 是 `…_ICON`），這裡照元素名稱。
  - 分割規則（右欄＝密語、視窗變寬到 519）與觸發鈕 `CHAT_ICON_ACTIVE`。
  - 捲動上下鈕的 ON 狀態＝還能往該方向捲；捲軸鈕的位置。
- **待驗證／未做：**
  - **真機未驗證**：沒有啟動遊戲；原生模式下 Enter→打字（含中文 IME）→送出→Esc 的完整流程、`KeyPress` 在真實焦點狀態下是否都收得到，須由 ui-native-smoke（dot，P24Lab）驗收。
  - IME 組字視窗的位置沒有移到 `CHAT_BAR`（目前用系統預設位置）。
  - 輸入列只有尾端游標（不支援左右移動、選取、貼上）。
  - 沒畫的元件：捲動軌道 SCROLL1–4、`SHORT_CHAT_ICON`、`WEBBROWSER_ICON`、`LANGUAGE_ICON`（HANGUL/ENGLISH）、所有 `*_FOCUS`／`*_FOCUS_ON` 狀態、捲軸鈕的 `_ON`。
  - 系統訊息不在 `PlayerChat` 模型裡，所以「系統」過濾目前不影響任何一行。
  - 分割模式會蓋到 (485,475) 的 `BAR_485` 槽位（小地圖等），這也和 V5 提到的模式 3「聊天延伸」有關，待查證。
  - 預設全組 demo-tests、`world-ui-tests` 需要 `engine/assets` junction，worktree 沒有（與 #464 相同），沒有自己建立；所以只跑了上列各分組。

## 產出路徑

- 程式：`engine/source/NativeUi/ChatPanel.cs`、`engine/source/NativeUi/State/ChatState.cs`、`engine/source/NativeUi/UiClientBridge.cs`、`engine/source/NativeUi/UiPanelRegistry.cs`
- 測試：`engine/_integration/dev/native-ui-tests/ChatWindowChecks.cs`、`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`
- 鎖檔：`engine/_integration/dev/rebuild-lock.json`
- 報告：`docs/reports/issue-468.md`
- 本機證據（未進 git）：`scratch/issue-468/real/`

## 給下一棒的提示

1. **合併衝突預期：** `UiClientBridge.cs` 的 `NativeUiGameState()` 與 #464（`UiLeftStatusGameState`）、#465（HpMp）都改同一行。解法：把裝飾器串起來，例如 `new UiChatGameState(new UiLeftStatusGameState(basic,…),chat)`。但 `LeftStatusState.From(state)` 用 `state is ILeftStatusSource` 判斷，被外層包住後會失效。建議 C2 在 `IUiGameState` 加一個通用的 `T? Get<T>()`（或讓各裝飾器公開 `Inner` 並逐層找），一次解決。`UiChatGameState` 已經公開 `Inner`。`UiPanelRegistry.cs` 與 `demo-tests` 的衝突照規則保留兩邊的行。
2. ui-native-smoke 時請特別看：Enter 開啟、中文 IME 輸入、送出後原服回傳行出現在聊天窗、Esc 還焦點後 WASD／方向鍵恢復移動、五階伸縮與錨定、分頁／過濾只影響顯示。請截原版同一畫面對照「推測」各項（尤其是第 5 階 238／240、底圖上緣對齊、分頁是否顯示在文字區內）。
3. V4／V5 查證若確認元件錨定或分頁語義，只需要改 `ChatState.cs` 的 `ChatRules` 與 `ChatPanel.Load` 裡的 `Anchor` 指定。
4. main 已有 #549 的 `ChatChannelToggle`（C26，伺服器端全體／密語／買賣開關）。本任務的過濾圖示依設計**只影響顯示**，沒有接 C26。原版過濾圖示是否也會送 C26，目前未查證；如果查證後確認會送，要另開任務，由 `IUiChatCommands` 轉呼叫 `LoginConnection` 既有的 API。
