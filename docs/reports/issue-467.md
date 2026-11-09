# Issue #467 ui-menu-buttons：巨集框選單按鈕（Map／Mail／Status／Spell／Item／Pledge／Option／Quit）

依據：`docs/design/native-ui.md` §5.3 ui-menu-buttons、`docs/reference/ui-layout.md` §1 V4 (b)、§2 `RIGHT_WINDOW`。
runner：claude-local（2026-11-08 由 codex-local 改派）。分支：`task/467`。

## 做了什麼

1. 新增 `engine/source/NativeUi/MenuButtonsPanel.cs`（`IUiPanel`，圖層 Bar）。
   - 執行期從 `uistatus86-c.xml` 讀 `RIGHT_WINDOW`（x, y）與 `BUTTON_2STAGE_LIST/IMAGE_BUTTON` 的 `title`／`action`／`surfNumber`／`xRPos`／`yRPos`，程式中沒有寫死座標。
   - 面板 Id 是 `RIGHT_WINDOW/BUTTON_2STAGE_LIST`，跟之後 ui-macro-bar 的 `RIGHT_WINDOW`（底圖 3176、巨集格）分開。`Bounds` 只取 8 顆按鈕矩形的聯集，不會擋到巨集格的點擊。
   - 命中矩形是 `(643+xRPos, 476+yRPos)` 加上一般狀態 surf 的尺寸。一般狀態 surf 缺少時，不畫，也不能點（不自行補命中框）。
   - 讀取規則很嚴格：遇到未知的 action、重複的 action、空清單或缺屬性，都會丟出 `UiLayoutException`，不會自己補按鈕。
   - 只在遊戲中（`IUiGameState.InWorld`）顯示；離開遊戲時，會放掉正在按住的按鈕。
   - 不處理任何按鍵。Ctrl＋M 和 Map 按鈕效果相同，但 Ctrl＋M 歸 ui-minimap 處理。
2. 在 `UiPanelRegistry.cs` 加一行註冊：`new(MenuButtonsPanel.PanelId,"uistatus86",_=>new MenuButtonsPanel())`。
3. 新增離線測試 `engine/_integration/dev/native-ui-tests/MenuButtonsChecks.cs`。設計 §5.2 允許在 `DemoChecks.csproj`、`demo-tests/Program.cs` 各加一行，這裡也照做：
   - `DemoChecks.csproj` 加入編譯。
   - `Program.cs` 新增 `native-ui-menu-buttons` 群組，並把測試接進無參數的完整回歸。
4. 用 `rebuild.py --update-lock` 重新產生 `rebuild-lock.json`。

### 按鈕狀態圖（V4 已完成，改照 V4 規則）

派工寫的是「V4 完成前只用第 0 張」。V4（#459，PR #498）已經合併，`ui-layout.md` §1 (b) 寫明 `BUTTON_2STAGE_LIST` 的 `IMAGE_BUTTON` 是 `3Stage(a, a+1)`：
- 一般狀態：`a`
- 滑過：`a+1`
- 按下：`a+1`

所以本任務改用 `a`／`a+1`。另外依 V4 的說明，`surfNumber` 跟 BIN 的 `atoi` 一樣只取第一個數字。真實 XML 的選單按鈕都只寫單一數字（例如 `3177`），沒有寫 `a~b` 範圍。
- **有畫：** 一般狀態（`a`），以及左鍵按住、游標還在按鈕上時的按下狀態（`a+1`）。
- **沒畫：** 滑過狀態（`a+1`）。C2 的 `UiInput` 不會在滑鼠離開面板時通知面板，所以畫了滑過圖之後可能一直卡在畫面上。這要等框架補上「滑鼠離開」事件後再加。
- 狀態切換規則都集中在 `MenuButtonsPanel.StateSurfs`。

### action 對應（目前是過渡做法，不是原版行為）

`IUiCommands` 目前只有 `ShowDevTools(page)`、`ReturnGameFocus()`、`Logout()`。本任務不可修改 `IUiCommands`，所以對應如下：

| 按鈕 | action | 原版 | 本引擎目前的做法 | 頁名提示對應的既有開發工具分頁 |
|---|---|---|---|---|
| Map | 13 | 開關野外／地監地圖窗＋音效 237（V5） | `ShowDevTools("小地圖")`，**過渡** | 無（舊 HUD 小地圖不是開發工具分頁） |
| Mail | 1 | 信件窗 | `ShowDevTools("信件")`，**過渡** | 無（舊 HUD 用的是 `MailDialog`） |
| Status | 2 | 角色狀態窗 | `ShowDevTools("角色")`，**過渡** | 「服務／角色」 |
| Spell | 3 | 魔法窗 | `ShowDevTools("魔法")`，**過渡** | 「服務／魔法」 |
| Item | 4 | 背包 | `ShowDevTools("背包")`，**過渡** | 「服務／背包」 |
| Pledge | 5 | 血盟窗 | `ShowDevTools("血盟")`，**過渡** | 「服務／血盟」 |
| Option | 6 | 選項窗 | `ShowDevTools("選項")`，**過渡** | 無 |
| Quit | 7 | 登出 | `Logout()`，跟舊 HUD「登出」按鈕走同一條路徑（`CancelLogin`） | — |

**要注意的兩件事：**
- C2 的 `NativeUiCommands.ShowDevTools(page)` 目前**會忽略 page 參數**，只把開發工具視窗打開，不會切換到指定分頁。所以現在 7 顆按鈕的效果都一樣：打開開發工具。
- 派工要求「Quit 走既有登出確認」。但查了既有程式，舊 HUD 的「登出」按鈕（`PlayerHudIntegration.cs:27`）**沒有確認對話框**，按下就直接 `CancelLogin`。本任務照用這條既有路徑，沒有另外做確認窗。原版有沒有確認窗，沒有查證。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` | 279 個來源、288 個鎖定輸入 |
| `rebuild.py --build` | PASS（product、audio、launcher 都建置成功） |
| `DemoChecks.dll . <out> native-ui-menu-buttons`（合成資料，沒有客戶端） | PASS |
| 完整回歸 `DemoChecks.dll . <out>`（`TW_CLIENT_ROOT` 指向空目錄，模擬 CI） | **PASS 14692 DEMO checks** |
| `engine/tools/check-no-game-assets.py` | pass，0 錯誤 |
| 本機選用檢查（`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`，唯讀） | PASS。PNG／JSON 只輸出到 `scratch/issue-467/real/` |

離線測試的內容。資料全部由程式合成，放在 `scratch/issue-467/synthetic/`：
- 註冊表只有一行，Id 跟 `RIGHT_WINDOW` 不同；V4 的 `a`／`a+1` 規則。
- 讀到 8 顆按鈕：action 依序是 13、1–7，標題相符，被註解掉的 Help 按鈕不會被讀進來。
- 命中矩形和 `Bounds` 聯集；按鈕之間的空隙點了沒有作用；矩形邊緣算在內。
- 不在遊戲中時，不顯示也不能點。
- 繪製：每顆按鈕以 1:1 畫一般狀態 surf；null surf 不畫；按鈕外不畫任何東西。
- 經過 `UiInput` 點擊的結果：前 6 顆 → `dev-tools:<頁>`；Option 合成資料缺 surf → 不觸發；Quit → `logout`。
- 右鍵不觸發；面板不接任何按鍵。
- 按住時畫 `a+1`，拖離按鈕變回 `a`，拖回來又是 `a+1`；在別顆按鈕上放開不觸發。缺按下圖時，按住期間什麼都不畫。
- 按住後離開遊戲：會放掉按住狀態，之後放開也不會登出。
- 未知 action、重複 action、空清單、缺 `xRPos`：都拒絕載入。

本機真實客戶端（`scratch/issue-467/real/menu-buttons.json`）：

| 按鈕 | action | surf（一般／按下） | 封存檔 | 邏輯矩形 |
|---|---|---|---|---|
| Map | 13 | 3202c／3203c | Sprite05 | (645, 567) 19×31 |
| Mail | 1 | 3177c／3178c | Sprite00 | (664, 567) 19×31 |
| Status | 2 | 3179c／3180c | Sprite02 | (683, 567) 19×31 |
| Spell | 3 | 3181c／3182c | Sprite11 | (702, 567) 19×31 |
| Item | 4 | 3183c／3184c | Sprite13 | (721, 567) 19×31 |
| Pledge | 5 | 3185c／3186c | Sprite15 | (740, 567) 19×31 |
| Option | 6 | 3187c／3188c | Sprite01 | (759, 567) 19×31 |
| Quit | 7 | 3189c／3190c | Sprite03 | (778, 567) 19×31 |

`Bounds` 是 (645, 567) 152×31，右緣 797、下緣 598，都在 `RIGHT_WINDOW` 底圖 157×124 的範圍內。8 顆按鈕寬 19，與間距 19 相同，所以中間沒有空隙。

目視比對：測試另外把 3176 底圖畫在 (643, 476)，只為了比對位置，本面板不畫底圖。截圖存在 `scratch/issue-467/real/right-window-crop.png`。目視確認：8 個圖示剛好落在底圖下緣那一排，左右都沒有超出底圖。本輪**沒有**另外跑 `ui_img_render.py` 做逐像素比對，底圖與按鈕都是同一套 `SurfCatalog` 解碼出來的。

## 已驗證／推測／待驗證

- **已驗證**
  - 8 顆按鈕的 XML 座標、action 與 surf，以及在 800×600 上的邏輯矩形（真實 XML 與 `.img` 標頭）。
  - 16 張狀態圖全部能從 Sprite*.pak 解出。
  - 離線測試涵蓋的所有邏輯。
- **推測**
  - TW16 的按鈕狀態與 TW13 相同（V4 也是這樣標）。
  - 只有在同一顆按鈕上放開才觸發點擊。V4 只寫「放開時清除並觸發」，沒寫要不要還在按鈕上。
- **待驗證**
  - 滑過圖要等框架補「滑鼠離開」事件才能畫。
  - 原版 Quit 有沒有確認窗。
  - Map 的音效 237 本任務沒有做。
  - 真機 smoke（ui-native-smoke）還沒跑。

## 未驗證／未做

- 沒有啟動遊戲，也沒有真機點擊。`nativeUi` 開關打開時的實際畫面與按鈕行為，要等 ui-native-smoke 驗收。
- 本機有客戶端時跑完整回歸，`DisplayNoticeChecks` 的「S245 unknown message stays labeled」會失敗。這是 #546 已記錄的既有問題：真實訊息表會把 id 999 解出文字。跟本任務無關，本任務也沒改。
- worktree 原本沒有 `engine/vendor` 和 `engine/assets`。照 #13／#109 的做法：
  - 把原專案的 WebView2 nupkg 複製到 worktree 的 `engine/vendor/`。
  - 建立 `engine/assets` junction，指向原專案的 `engine/assets`，只供唯讀。
  - 兩者都在 gitignore 範圍內，沒有進 commit。

## 第二輪：同步 origin/main（2026-11-09）

PR #596 落後 main 108 個 commit，有 4 個衝突：`UiPanelRegistry.cs`、`DemoChecks.csproj`、`Program.cs`、`rebuild-lock.json`。

- **解法：** 兩邊都保留。`rebuild-lock.json` 用 `rebuild.py --update-lock` 重新產生（302 個來源、311 個鎖定輸入）。
- **註冊順序（實際問題）：** main 已經有 ui-macro-bar 的 `MacroPanel`。它的 `Bounds` 是 3176 底圖加鎖定鈕，範圍蓋住選單按鈕所在的 y=567。兩個面板都在 Bar 圖層，`UiRoot` 依註冊順序畫圖，命中測試也只挑最上層那一個面板，不會往下傳。所以本面板那一行改排在註冊表最後，否則底圖會蓋住按鈕，也會吃掉按鈕的點擊。`MenuButtonsChecks` 加了一項檢查，確認順序。
- **驗證：**

| 項目 | 結果 |
|---|---|
| `rebuild.py --build` | PASS（product `38eba1f3…`） |
| `native-ui-menu-buttons`（沒有客戶端） | PASS |
| `native-ui-menu-buttons`（`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`，唯讀） | PASS |
| `build.yml` 的 15 個 CI 群組＋`native-ui-menu-buttons`（`TW_CLIENT_ROOT` 未設定） | 15 個 PASS。`map-continuity` 失敗在 `DisplayNoticeChecks` 的「S245 unknown message stays labeled」，就是 #546 記錄的本機問題（本機找得到真實訊息表），與本任務無關 |
| 完整回歸（不帶群組參數） | 第 353 行的原生 UI 群組全部通過，之後一樣停在 #546 那一項。改用空的客戶端目錄也跑不完：main 的 `NativeUiMacroRealClient` 只有在變數是空字串時才略過，指向空目錄會丟例外（屬於 #466 的測試，本任務沒改） |
| `check-no-game-assets.py` | pass |

- **未驗證：** 沒有在同一個 `UiRoot` 裡放 `MacroPanel`＋本面板做實際命中測試（要有完整巨集 XML 與巨集狀態），目前只檢查註冊順序。也沒有做真機 smoke。

## 產出路徑

- `engine/source/NativeUi/MenuButtonsPanel.cs`（新增）
- `engine/source/NativeUi/UiPanelRegistry.cs`（加一行）
- `engine/_integration/dev/native-ui-tests/MenuButtonsChecks.cs`（新增）
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`（各加一行，Program.cs 另外在完整回歸加一個呼叫）
- `engine/_integration/dev/rebuild-lock.json`（重新產生）
- `docs/reports/issue-467.md`（本檔）

## 給下一棒的提示

1. **註冊順序（已處理）：** 同步 main 後，`MenuButtonsPanel` 那一行已排在 `MacroPanel`（`RIGHT_WINDOW`）與 FriendPanels **之後**。之後新增其他 Bar 圖層面板時，如果和 (645, 567) 152×31 這塊區域重疊，要注意不要排在本面板後面。`MenuButtonsChecks` 會檢查這個順序。
2. **讓 page 參數生效（小任務，要改 C2 的 `UiClientBridge.cs`）：** 讓 `NativeUiCommands.ShowDevTools(page)` 依頁名選分頁，可以參考 `ShowPlayerService(name)` 的作法。另外：
   - Mail 改呼叫既有的 `OpenMail()`。
   - Map 改呼叫小地圖開關。
   - 建議在 `IUiCommands` 加 `OpenMail`、`ToggleMap`，讓本面板直接呼叫，不要再繞道開發工具。
3. **滑過圖：** 請 C2 在 `UiInput` 補一個「滑鼠離開面板」通知，例如 hit 的面板改變時，送一次 Move 到上一個面板。之後本面板只要在 Move 時記錄滑過的按鈕，就可以畫 `a+1`。
4. **原生視窗做好後：** ui-minimap 完成後，Map 直接開 `Field_Map`，並播放音效 237。信件、角色、魔法、背包、血盟、選項的原生視窗完成後，把 `MenuButtonsPanel.Actions` 對應改掉，並刪掉報告中「過渡」的說明。
5. **Quit 確認窗：** 如果查證後發現原版 Quit 有確認窗，需要另開任務；既有登出路徑目前沒有確認窗。
