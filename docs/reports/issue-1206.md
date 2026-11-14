# Issue #1206 — 原生 UI 在 CharacterList 時沒有選角畫面（#1186 SM07 ❌）

階段：code（claude-local，第 1 次嘗試）　分支：`task/1206`

## 結論（根因，已驗證：靜態＋資源比對）

選角面板**有掛上 Host、也判定為 Visible**，問題出在**視窗大小**：

1. `SelectUI.xml`（kor25 Tile.pak，plain sha256 `b4eb1fa7…`，與 #1186 log 的 `ui-xml-ref SelectUI.xml` 相同）的
   `Character_Select_Window` 是 `X="0" Y="0" BaseSurf="6421"`，**沒有 Width／Height**（第 9 行）。
2. 原本 `CharUiBuilder.Window`（`engine/source/NativeUi/SelectCharPanel.cs` 修改前第 159–160 行）的規則是「有宣告尺寸就用宣告的，否則用 BaseSurf 圖的大小」。
3. `SurfList.xml` 沒有 6400–6989 的列，surf 6421 依 `%d.img` 走本機 `Sprite08/6421.img`（#1186 `native-ui.log`：
   `surf 6421: SurfList.xml 無 file，依 %d.img 用本機 Sprite08/6421.img`）。這個檔是 **60×80 的職業肖像**（header `3c 00 50 00`，9,608 B = 8 + 60×80×2；
   0-客戶端 sha256 `4E2BC47C…`，kor25 同名檔 `1A5AB3F8…` 也是 60×80 肖像）。
4. 所以 `Main.Bounds = (0,0,60,80)`，而
   - `UiRoot.Compose`（`UiRoot.cs:221–225`）每個面板先 `canvas.Restrict(p.Bounds)` 再 `Paint` → 整個選角畫面被裁成左上 60×80，
     只看得到 6421 那張紅底肖像（就是 SM07 截圖左上那塊「紅色圖」）；其餘列表、名字、按鈕都畫在裁切範圍外。
   - 選角面板本身沒有畫底，世界層 `PaintScene`（`Program.cs:181–183`）的「等待真實世界初始化／可取消連線或回離線預覽。」就露出來了。
   - `UiRoot.HitTest`（`UiRoot.cs:73`）以 `Bounds.Contains` 判斷，60×80 以外的點擊都落到世界；上下鍵雖然送到面板（`UiInput.Key` 不看 Bounds），
     選取有變，但畫面被裁掉，看起來「沒有反應」。
5. 同理，`Message_Window`（刪除確認／結果通知）的 BaseSurf 6488 也是 60×80，視窗只有 60×80，
   `Message_Label`（394×190）與 Ok／Cancel（Y=237）都在範圍外 → SM09 刪角無法確認。

排除的假設（逐一查過）：
- Host 沒掛上：✗。`smoke-A/window-events.json` 在 `native-ui active` 之後有 `clear-input reason=native-character-select`，只有
  `AttachNativeCharacterScreens()`（`CharacterManagementUi.cs:143` 修改前）會送這個事件，代表 Host 已掛上。
- 面板 Faulted／載入停用：✗。`native-ui.log` 沒有任何 `panel … 失敗，已停用`、`select-char-ui …` 停用或缺圖行。
- View 沒更新：✗。`UiRoot.Update` 每次 OnPaint 都呼叫 `SelectCharPanel.Update → Sync`，State 來自同一個 `login`（`ClientLogin.cs:4` 唯一一個 LoginConnection）。
- 被 CreateUI／LoginUI 蓋掉：✗。`CreateCharPanel.Visible` 需要 `Begin` 開過；`LoginPanel.Update` 在 CharacterList 時 `Hide()`（`LoginPanel.cs:322`，`ShowsIn` 不含 CharacterList）。
- `NativeUiActive`／`normalProduct` 組合：✗。`native-ui active` 事件已出現，面板清單含 `SelectUI`、`CreateUI`。

截圖右下那個空框不是選角面板（被裁在 60×80 內不可能畫到那裡），是較低圖層的其他 HUD 視窗；修正後選角畫面以黑底蓋滿全畫面，下層（World／Bar／Window 層）視窗不會再露出。Float 層中註冊在 SelectUI 之後的面板會畫在它上面：已查 CreateUI（要 `Begin` 才顯示）、LoginUI（CharacterList 隱藏）、ActionUI_Button（Bar 層、只在世界中）；其餘（多為世界內提示框）在選角狀態不顯示屬推測，retest 截圖確認。

## 做了什麼

| 檔案 | 修改 | 行號（修改後） |
|---|---|---|
| `engine/source/NativeUi/SelectCharPanel.cs` | `Load`：主視窗沒有宣告 Width／Height → `FullScreen`（從原點蓋到 800×600）；`Message_Window` → `CoverPieces`（長大到包住自己的元件；本身沒有大小時維持空的 → 仍判為不可用，與原本一致） | 397–399、411–437 |
| 同上 | `Paint`：先把 `Main.Bounds` 塗黑再畫視窗（Back_Image 執行期才定圖、未建，見「推測」） | 543–546 |
| 同上 | 檔頭版面規則註解補上 #1206 | 28–29 |
| `engine/source/CharacterManagementUi.cs` | `WaitingTextShown(online, renderable, nativeSelectShown)`、`NativeSelectScreenShown()`（原生 UI 啟用且 SelectUI 在 root 上 Usable） | 86、88 |
| 同上 | 診斷：`TraceNativeSelect()` 每個 timer tick 比對一次，狀態有變才寫一行 `select-char-ui 狀態 …` 進 `native-ui.log` | 79、90–100 |
| `engine/source/Program.cs` | `PaintScene` 的等待字樣分支：原生選角畫面顯示中就不畫字（仍照舊 `return`，不畫世界） | 182 |
| `engine/source/NativeUi/SelectCharPanelTests.cs` | 兩個斷言由 `(0,0,400,300)` 改成 `(0,0,800,600)`（合成版面主視窗沒有宣告大小，依新規則是全畫面） | 163、189 |
| `engine/source/NativeUi/SelectScreenGeometryTests.cs`（新） | `[WorldModuleTest("select-char-geometry")]` | 全檔 |

`--classic-ui`／`nativeUi:false`：`NativeUiActive` 為 false，`AttachNativeCharacterScreens`、`TraceNativeSelect` 都直接 return，
`NativeSelectScreenShown()` 為 false，`WaitingTextShown` 的結果與修改前的 `OnlineMode&&!login.World.Renderable` 相同 → WinForms 選角路徑不變。
`CreateCharPanel` 的兩個場景在 CreateUI.xml 有宣告 800×600，不受 `FullScreen` 影響；`CharUiBuilder` 本身沒改。

## 怎麼驗證

- **已驗證（本機，只編譯）**：`dotnet build engine/source/OfflineClient.csproj -c Release`（`DOTNET_CLI_HOME` 指到 scratch，並設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`）→ 0 錯誤；警告都是既有的（HtmlActionTests、MagicUi、MapResources），新檔沒有警告。依規定**沒有執行**任何編出的程式。
- **已驗證（資源，唯讀）**：用 `D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe`（pak extract／xml decrypt／pak search）讀出，輸出只在 `scratch/issue-1206/`（不進 git）：
  - kor25 `Tile.idx`：`SelectUI.xml`（plain sha256 `B4EB1FA7…`，與 log 相同）、`CreateUI.xml`（`5277A2AF…`，兩個場景宣告 `Width="800" Height="600"`）、`SurfList.xml`；0-客戶端 `Tile.idx`：`SurfList.xml`（兩邊位元組相同，都沒有 6400–6989 的列）。
  - 0-客戶端 `Sprite00–15.idx` 的 6404–6417、6421–6432、6486–6494 `.img`：**全部是 60×80**（標頭與長度逐一列過）；6421、6410 解成 PNG 是職業肖像。kor25 `Sprite08/6421.img` 也是 60×80 肖像（內容不同）。
  - `pak search 6421` 在兩個客戶端的 Sprite、Sprite00–15 都只有 `6421.img` 與 `6421-0.spr`，沒有 `6421c.img` 或 png。
- **交給 CI**：`--module-tests` 會自動跑新的 `select-char-geometry`（合成版面照真實座標、64xx 全是 60×80 圖、假 `ICharSelectHost`、State=CharacterList、3 角色）：
  主視窗 800×600、訊息框 (171,153,423,317)；面板 Visible／Usable／未 Faulted；`UiRoot.HitTest` 在 60×80 以外也打到面板；
  `UiRoot.Compose` 以白色世界層模擬等待字樣，合成後空白處是黑色、第 0／2 列名字、空格的 Create_Button、Enter_Button、資訊欄都有畫出；
  上下鍵、Enter 進入、點選、Delete → Message_Window Ok 送出、空格雙擊 → `CreateFallback`；
  `WaitingTextShown` 在原生選角顯示時為 false、classic 時與原本相同；`UiOptions.Resolve(classic)` 關閉原生 UI；診斷行內容。
  也跑既有的 `select-char-ui`、`create-char-ui` 與 CharScreenFollowup 系列。

## 推測（沒有直接證據）

- TW16 的選角畫面是否真的是全畫面、空白處是否為黑色：CreateUI 兩個場景宣告 800×600、SelectUI 的 `Back_Image`（X=0,Y=0，無 Surf）跟 LoginUI 一樣是執行期才定圖，所以判斷選角也是全畫面場景；實際底圖是哪張沒有逆向，先塗黑（不放替代圖）。
- **64xx 圖是否是正確的選角美術**：本機 6404–6494 全是 60×80 的職業／人物肖像。6430＋8×class＋4×sex（R9-O 的職業圖）與 normal／highlight／push 三色邊框相符，這部分看起來合理；
  但 Enter／Up／Down／Back／Delete（6404–6417）、Slot_Back（6429）、Message_Window 底（6487／6488）也都是肖像，**很可能不是 TW16 該畫的按鈕圖**。kor25 的新版選角另外有 6991（`SelectUI_CharacterList_Back`，kor25 Sprite04，251 KB）、6992c 等，但那屬於 R9-O 記載的 Character_List 版面，不是這份 BigSlot SelectUI.xml。
  本 PR 不換圖（規則 5：不以替代圖冒充），所以修正後的選角畫面「可操作、文字正確」，但按鈕外觀可能是肖像 → 需要真機確認，並建議另開 resource 任務查 TW16 選角按鈕實際用的 surf。

## 待驗證（交 `f1186-retest`）

1. **SM07**：正常入口（不帶 `--classic-ui`）登入 p24svc01 → CharacterList：畫面不再是黑底＋等待字樣；看得到三列角色（Lv.／名字）、右下資訊欄文字；上下鍵改變選取、資訊欄跟著變；Enter 或雙擊進入世界。
2. **SM08**：空格單擊選取、雙擊 → 原生建角畫面（CreateUI 可用時）或 WinForms 建角對話框；建立後回到選角並出現新角色。
3. **SM09**：Delete → 確認框（中央，文字可讀，Ok／Cancel 可點）→ Ok 後伺服器結果通知；再建、刪、切角（世界內「切換角色」回到選角畫面要能再顯示）。
4. **SM55**：`--classic-ui` 的 WinForms 選角照舊（登入分頁的角色清單、建立／刪除按鈕、等待字樣仍在）。
5. 截圖對照：選角按鈕／底圖是否為肖像（見「推測」第 2 點），若是，記下來交 resource 任務。

retest 請收 `native-ui.log` 這幾行（新加的診斷，狀態有變才寫一行）：
```
select-char-ui 狀態 login=CharacterList available=True host=True view=CharacterList characters=3 faulted=False visible=True bounds=0,0,800x600 main=800x600 create-visible=False selected=…
```
若 `visible=False` 請看同一行的 `available`（括號內是停用原因）／`host`／`view`／`faulted`／`create-visible`；若 `bounds` 不是 `0,0,800x600` 代表版面仍不對。另外留意有沒有 `select-char-ui 缺圖不畫` 與 `panel SelectUI … 失敗，已停用`。

## 產出路徑

- 程式：`engine/source/NativeUi/SelectCharPanel.cs`、`engine/source/CharacterManagementUi.cs`、`engine/source/Program.cs`
- 測試：`engine/source/NativeUi/SelectScreenGeometryTests.cs`（新）、`engine/source/NativeUi/SelectCharPanelTests.cs`
- 報告：`docs/reports/issue-1206.md`
- 本機暫存（不進 git）：`scratch/issue-1206/`（解出的 XML、img、肖像 PNG、montage）

## 給下一棒的提示

- 測試用的是 `[WorldModuleTest]`，不是 `[CheckGroup]`：`SelectCharPanel.Load(UiLayout, Func<int,UiSurface?>)` 是 internal，產品組件沒有 `InternalsVisibleTo`，DemoChecks 看不到；既有選角／建角測試也都是 `[WorldModuleTest]`，同樣自動發現、CI 的 `--module-tests` 會跑。
- CreateUI 的 `Message_Window` 也是 BaseSurf 6488（60×80）→ 建角錯誤訊息框同樣會被裁成 60×80。不在本張檔案範圍內；修法相同：`CreateCharPanel.Load` 對 message 呼叫 `SelectCharPanel.CoverPieces`（一行）。建議併入 retest 後的修正或另開小任務。
- 若 retest 確認 64xx 按鈕是肖像：開 resource 任務，逆向 TW16 `CSelectUI`（R9-O／R10-N 已解的 sub_EF13F0 等）找 Enter／Delete／Back／Slot_Back 的實際 surf，或確認 TW16 用的 SelectUI 版面來源。
