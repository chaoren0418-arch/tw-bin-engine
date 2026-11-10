# Issue #764 T326：P-D 原生登入畫面（參考客戶端 LoginUI.xml）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK）§4.1 `LoginUI.xml` 列、§4.2 第 4:LoginUI 列、§6 P-D。P-A（#757，`RefClientXmlSource`）與 P-B（#760，`RefClientSurfFallback`）已在 main。版面與繪圖規則沿用 #761（`AgitAuctionPanel`）的做法。

## 做了什麼

- **新檔 `engine/source/NativeUi/LoginPanel.cs`**（`PanelId="LoginUI"`）
  - **註冊與載入**：
    - 用 `[UiPanel(190)]` 自動發現，實作 `IUiClosablePanel`、`IUiTextInput`，圖層是 `Float`（登入畫面在最上層）。
    - 宿主版面用 `uistatus86`，跟 Option／AgitAuction 相同。`Load` 內才讀 `LoginUI`：主 Tile.pak 沒有，就讀參考客戶端（預設 kor25），SHA-256 由 P-A 寫進 `ui-xml-ref` log。
    - surf 走 `SurfCatalog.Get`（本機 → P-B 參考備援，log 前綴 `surf-ref`，含 SHA-256）。**不複製任何檔案進 repo。**
    - 缺的圖不畫，列在 `Missing`，並寫一行 `login-ui 缺圖不畫` log。不用替代圖。
  - **Login_Window（版面照 XML）**：
    - 視窗位置取 X/Y（缺就丟例外，不自己編）。XML 沒有 BaseSurf，也沒有宣告大小，所以 `Bounds` 是已建元素的聯集。聯集以外的世界照常可點，例如離線預覽。
    - `Back_Image` 沒有 Surf（背景是執行期由程式另外畫的），列在 `Skipped`，不補畫。
    - 有 Surf 的 Image 全畫。Button 用 Base／Highlight／Push 三態：滑鼠移上去畫 Highlight，按住畫 Push。
    - 兩個 InputBox：
      - `ID_Input`、`Password_Input` 取 X/Y/Width/Height 與 `LimitText`（XML 是 14／16）。
      - `Password="1"` 那格畫成 `*`。文字白色、靠左上，聚焦時畫游標，太長時只顯示尾段。
  - **登入**：
    - 按 `Login_Button`（`Login_LoginWin`），或在密碼框按 Enter，就呼叫 `LoginControl.NativeEntry.StartNative(帳號, 密碼)`。
    - 這和 WinForms 列、網頁走的是**同一個有防護的 `LoginControl.Start`**，endpoint 用正常入口的 `ProductEntry.Profile`／`Port`。送出後清掉密碼。
    - 帳密格式、連線中、版本號這些檢查都沿用 `LoginControl`，不另寫一套。
  - **鍵盤**：
    - 畫面出現時，焦點放在帳號框（**推測**）。
    - Tab 切換兩格。帳號框按 Enter 跳到密碼框（**推測**）。Backspace 刪字。Esc 離開輸入框，按鍵交回遊戲（`ReturnGameFocus`）。
    - 輸入框有焦點時，按鍵由本面板用掉：用掉的鍵不會產生 WM_CHAR，所以依 US 配置自己轉成字元（含 Shift、Caps Lock）。方向鍵也吃掉，避免打字時離線預覽在走。
    - F 鍵與 Ctrl／Alt 組合鍵照常交給客戶端。
    - `IUiTextInput.Char` 只收可列印 ASCII（XML `English="1"`）。
  - **Message_Window（Modal）**：
    - 以下兩種情況會開：
      - 原生登入被 `LoginControl` 拒絕，例如格式錯或已在連線中。內容是它回的訊息。
      - 原生登入之後，連線停在 `Rejected`／`Disconnected`／`Faulted`。內容是 `ProductEntry.StateName`。
    - 視窗大小取 BaseSurf 7092 的圖。XML 沒有宣告大小，所以 7092 缺時不開訊息窗，只記 log。
    - `Message_Label`（MultiLabel）在 Width/Height 內逐字換行，垂直置中。
    - Ok／Cancel 鈕、Enter／Esc 都能關。開著時 `Bounds` 是整個畫布，吃掉所有輸入（Modal）。
  - **何時顯示**：
    - 顯示條件：不在世界中，而且狀態是 Offline／Connecting／AwaitSeed／Authenticating／AwaitList／ReadingCharacters／Rejected／Disconnected／Cancelled／Faulted。
    - 收到角色清單（CharacterList 以後）就收起，交給既有的選角介面。
    - `Close()` 會收起，直到登入狀態改變才再出現。
  - **繪圖順序**（**推測**，同 #761）：同一視窗內依 XML **反向**順序畫。兩個視窗的 `Back_Image` 都列在最後，但它們必須在最底層。點擊判定順序與繪圖相反。
- **`engine/source/LoginControl.cs`（只加原生分流）**：
  - 主建構子改成一般建構子，參數、欄位與行為都不變。建構時把自己登記成 `LoginControl.NativeEntry`，用 WeakReference 存，不會造成記憶體洩漏。
  - 新增 `SessionState` 與 `StartNative(account,password)`。後者就是 `Start(ProductEntry.Profile, ProductEntry.Port, …, 目前的 generation/revision)`。
  - `ClientLogin.cs`、`UiClientBridge.cs`、註冊表都沒改。
- **新檔 `engine/source/NativeUi/LoginPanelTests.cs`**：`[WorldModuleTest("login-ui")]`。全部用合成資料：自寫的同結構小版面、純色圖、不連網的假 `ILoginSession`，不讀任何客戶端。涵蓋：
  - 版面：位置、輸入框幾何、LimitText、Password 旗標、按鈕三態、反向繪圖順序、Missing／Skipped、聯集範圍、訊息窗大小取 BaseSurf、`id;flag` 解析。
  - 壞版面：
    - 會丟例外的：沒有 Login_Window、輸入框重複、密碼框不是 Password、缺 LimitText、缺大小、圖號壞、缺 X、沒有輸入框。
    - 不丟例外、要照規則處理的：訊息窗底圖缺（仍可登入）、全部圖缺（只剩輸入框，不補圖）、沒有 UiResources。
  - 顯示：各 LoginState 下顯示或收起、在世界中收起、Close 後狀態改變再出現、沒有 LoginControl 時不顯示。
  - 打字：大小寫、Shift、Caps Lock、數字、數字鍵盤、LimitText、Backspace、Tab、Esc 交回焦點、F 鍵與 Ctrl 不吃、WM_CHAR 只收 ASCII、沒焦點時不吃鍵。
  - 登入流程：
    - Enter 送出。endpoint、帳密、`beforeStart` 都核對過，送出後密碼清空。
    - 送出後收到 Rejected、Faulted、Disconnected，各自跳出訊息窗。訊息窗是 Modal，Enter、Esc、Ok、Cancel 都能關。
    - 被 `LoginControl` 拒絕（連線中、帳號格式錯）時，訊息窗顯示它回的訊息。
    - 使用者取消時不跳訊息。訊息窗不能用時只記 log。進入角色清單後畫面收起。
  - 像素層級的繪圖：Image、輸入框底圖、Exit 在 Multi 之上、Highlight、Push、放開在鈕外不觸發、點輸入框取得焦點、訊息窗底圖、Back_Image、Ok 鈕、Modal 時登入窗不可點。
  - 其他：`KeyChar` 對照表、`Wrap`、`NativeEntry` 登記與 `StartNative`、自動發現。

> 任務說「測試用 `[CheckGroup]`」：`engine/source` 內的測試一律用 `[WorldModuleTest]` 自動發現，與 #757／#761／#768 相同；`[CheckGroup]` 只用在 `_integration/dev`。測試檔依任務指示用英文類名 `LoginPanelTests`。

## 既有 WinForms 入口

- `--classic-ui`（或 `native-ui.json` 的 `"nativeUi":false`）時，原生 UI 根本不會建立，不會讀 `LoginControl.NativeEntry`。登入照舊走 WinForms 列（開發工具的「登入／選角」頁）與網頁。
- 原生 UI 開著，但面板不能用時，面板不顯示，其餘原生 UI 照常，登入一樣走既有入口。不能用的情況有：沒有參考客戶端、XML 壞、`"refClient":false`、Login_Window 沒有可畫的元素。
- 原生登入畫面開著時，既有入口仍然可用，因為兩邊共用同一個 `LoginControl`，狀態一致。

## 資源核對（PakViewer CLI，唯讀，輸出只在 worktree 的 `scratch/t764/`，沒有進 git）

- **XML**：`pakviewer-cli pak extract <client>\Tile.idx LoginUI.xml`，再 `xml decrypt`。
  - kor25：原檔 SHA-256 `c79d636c56750fc5381cdcd18fc9104a9fb1d3eec9a65c8a0a4fb2783c821a61`，解密後 `dfefdd0dac096ff9c83840fdf9b865e73ee7e8c577d5f97cd6d27066b2c8d58f`，兩者都與 PAK§4.1 相符。
  - t89 版本不同：原檔 `6e47c21c…d057ad`，解密 `59d7cbbb…53dfa4`。差別是 Account／Password 圖拆成 `_Screen`＋`_Multi`（7000–7003）。
  - 執行期預設讀 kor25。
- **為了確認缺圖，查過的 pak**：用 `docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt -Out scratch\t764\pak-index` 列出清單，再用 `scratch/t764/find_login_surfs.py` 逐一比對。查法是 `<id>c.img`、`<id>.img`、`.tbt`、`.png` 四種檔名。
  - `0-客戶端` 全部 24 個 idx：
    - `Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`
    - `LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile.idx`
    - `launcher\TW13081901.pak` 沒有 idx，打不開。
  - kor25、t89、ly 各 19 個：`Tile`、`Text`、`Sprite`、`Sprite00`–`Sprite15.idx`。
- **結果（44 個 surf：6993–6997、7000、7002、7004–7031、7091–7098、7231）**：
  - **本機 `0-客戶端` 44 個全缺**（24 個 idx 都沒有）。
  - kor25：44/44 有 `.img`。7093–7098 是 `c.img`。
  - PAK§4.1 寫 kor25「42/44」。這次逐檔核對，kor25 其實是 44 個全有。差異可能來自當時沒算 `c.img` 變體（**推測**）。
  - 另用 `scratch/t764/check_hash_order.py` 照 P-B `RefClientSurfFallback.Candidates` 的順序重算一次：先 `<F>c.img` 再 `<F>.img`，各自先查 `Sprite{NameHash}` 再查 `Sprite`。kor25 照這個順序也是 **44/44 命中**，所以預設路徑下登入畫面**沒有缺圖**。
  - 本機 `SurfList.xml` 只有這些 id 的 `filedesc`（例如 `6995 Login_Off`），沒有 `file` 屬性，所以檔名用 id 本身，依 #760 的 `%d.img` 規則。
- 參考客戶端不在時，連 XML 都讀不到，登入畫面停用，不會畫半套。

## 怎麼驗證

- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t764/build` 結果 **0 錯誤**。2 個警告在 `MagicUi.cs`，原本就有。
  - 本機沙箱還原的老問題（#761 已記）：直接 `dotnet build` 會出現 WebView2 的 NETSDK1130。
  - 處理方式：把主專案的 `obj/project.assets.json`、`nuget.g.props`、`nuget.g.targets` 讀出來，專案路徑改成本 worktree、套件資料夾仍指向主專案的 `engine/.nuget`，放進 worktree 的 `obj/`，再用 `--no-restore` 建置。主專案只讀、沒有寫入。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`login-ui` 與既有測試交給 PR 的 GitHub Actions CI。CI 結果見 PR。
- `rebuild-lock.json` 沒有改動。

## 未驗證什麼（待驗證）

- 本機不能跑程式，所以沒有實測真實 kor25 的 `LoginUI.xml` 經本面板解析後能不能用（`Unavailable` 是否為 null）。只做了靜態比對：元素結構與合成版面相同，用到的屬性都有處理。
- 以下都是**推測**：
  - 反向繪圖順序。
  - `;n` 旗標（1／3／4）沒有解讀，`;3` 的 Multi／Shadow 圖可能原本是半透明混色，現在當一般色鍵圖畫。
  - 文字白色、靠左上。
  - 開畫面時焦點在帳號框、帳號框 Enter 跳密碼框。
  - 訊息窗觸發的時機與內容（原版是哪些伺服器回應會開，沒有反組譯）。
  - US 鍵盤配置。
- 沒套用的屬性：`FontSize="14"`（用既有 `UiText` 字型）、`DarkBack`（背景變暗）、各種音效（`OverSound`／`PushSound`／`FocusSound`）。
- 登入背景：`Back_Image` 沒有 Surf，原版在執行期另畫（可能是動畫），現在背後是離線預覽的世界或黑底。
- 參考客戶端的版面是否與 TW16 原版相同：**推測**（PAK§7）。
- 真機（交給 Codex／dot，P24Lab 測試角色）：
  1. 原生 UI 開著、kor25 在預設路徑時啟動，log 應出現 `ui-xml-ref LoginUI.xml` 與多行 `surf-ref …`，畫面中下方出現天堂標誌、帳密框與登入鈕，右下有 4 顆鈕。
  2. 打帳號、Tab 或 Enter 到密碼框、打密碼、Enter，應進到既有的選角介面，登入畫面收起。
  3. 打錯密碼，應跳出訊息窗（「登入遭拒絕」），按 Ok 關掉後可以重打。
  4. 加 `--classic-ui`，應完全是原本的介面。

## 沒有做的部分（待做）

- `Notice_Window`（S48 公告）：仍是既有的 `MessageBox`（`ClientLogin.cs` 不在本任務可改的範圍）。
- `Credit_Window`（TextScroller `credits`）。
- `ChangePass_Button`／`OTP_Button`／`Credit_Button`／`Exit_Button`：點了只記一次 `login-ui … 尚未接上`。
  - Exit 原版是結束遊戲，但 `IUiCommands` 沒有這個動作。
- 登入背景、音效、`;n` 混色。
- 選角畫面（`SelectUI.xml`）是另一張任務（PAK§4.2 4:SelectUI）。

## 產出路徑

- `engine/source/NativeUi/LoginPanel.cs`（新）
- `engine/source/NativeUi/LoginPanelTests.cs`（新）
- `engine/source/LoginControl.cs`（只加原生分流）
- 本報告 `docs/reports/issue-764.md`
- 分支 `task/764`

## 給下一棒的提示

- 選角（SelectUI）可以照同一個模式做：
  - 在 `LoginControl` 已有的 `NativeEntry` 上加 `EnterNative(name)`，包住 `Enter(name, generation, revision)`。
  - 面板在 `CharacterList` 狀態顯示，正好接在本面板收起之後。
- 接 Exit：要在 `IUiCommands`（或新的 `IUiLoginCommands`）加「結束客戶端」，由 `UiClientBridge.NativeUiCommands` 呼叫 `Close()`。這需要改 `UiClientBridge.cs`。
- 若真機看到繪圖順序、`;3` 混色或焦點行為不對：
  - 繪圖順序：改 `LoginPanel.Window` 的反向迴圈。
  - `;3` 混色：在 `PaintWindow` 依 `LoginImageRef.Flag` 分流。
  - 焦點：改 `Update` 中的 `Focus=IdInput`。
- PAK§4.1 的 kor25「42/44」應更正為 44/44（本報告「資源核對」）；`docs/reference` 不在本任務範圍，沒改。
