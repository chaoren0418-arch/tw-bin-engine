# Issue #765 T327：P-D 原生選角與建角畫面（參考客戶端 SelectUI.xml／CreateUI.xml）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK）§4 的 4:SelectUI／4:CreateUI 兩列、§6 P-D。P-A（#757，`RefClientXmlSource`）與 P-B（#760，`RefClientSurfFallback`）已在 main。做法沿用 #761／#763：執行期從參考客戶端讀 XML（SHA-256 由 P-A 寫進 `ui-xml-ref` log），不複製進 repo。

## 做了什麼

- **新檔 `engine/source/NativeUi/SelectCharPanel.cs`**（`PanelId="SelectUI"`）
  - 用 `[UiPanel(186)]` 自動發現，實作 `IUiClosablePanel`。宿主版面跟 #763 一樣是 `uistatus86`。`Load` 裡才讀 `SelectUI`。
  - 圖層是 `UiLayer.Float`：全畫面、modal，顯示時接收所有滑鼠與按鍵。
  - 只在登入狀態是 `CharacterList` 或 `ManagingCharacters` 時顯示。#764 的登入畫面只在選角之前的狀態顯示，兩個畫面剛好接上。
  - 新介面 `ICharSelectHost`（`Snapshot`／`Enter`／`Create`／`Delete`／`Logout`／`CreateFallback`）和資料快照 `CharSelectView`，兩個畫面共用。
  - 共用的版面建構器 `CharUiBuilder` 的規則：
    - 座標相對於父元素（Window／Control／Image／Button）。
    - `List` 依子元素的 Height（`Vertical="1"`）或 Width 排列。
    - 繪圖順序：同一層的兄弟元素依 XML 反序畫，父元素先畫、子元素後畫。點擊判定用相反的順序。
  - **版面照 XML**：
    - `Character_Select_Window`：大小取 BaseSurf 6421 的圖。
    - `BigSlot_List`：4 個 `Slot_Control` 就是畫面上的 4 列。
      - 有角色：畫 `Select_Button`。被伺服器標成七日待刪除的角色（class 32–38）改畫 `WaitDelete_Button`，再加 `WaitToDel_Image`。
      - 有空欄位：畫 `Create_Button`。
      - 超出欄位數：畫 `Slot_Lock_Back` 與 `Lock_Button`。買欄位在 3.81 沒有封包，所以停用。
    - 目前選取的角色用 PushSurf 畫。`Up`／`Down` 按鈕和滾輪一次捲一列。
    - `CharacterInfo_List` 顯示選取角色的名稱、等級、職業、血盟、正邪值，以及生日（S93 建立時間的原始數值）。
    - `Message_Window` 用來確認刪除、顯示被拒的原因，以及刪除後伺服器回的結果。
  - **操作**（呼叫的函式與 stamp 都跟 WinForms 相同）：
    - `Enter_Button`、Enter 鍵、在已選取的列上雙擊：`LoginControl.Enter`，進入世界。
    - `Delete_Button`：先開確認框，文字跟 WinForms MessageBox 相同，按 Ok 才呼叫 `LoginConnection.DeleteCharacter`。
    - `Back_Button`：`CancelLogin`，登出。
    - `Create_Button`：建角畫面可用時開原生建角畫面，不可用時開 WinForms `CharacterCreationDialog`。
    - 上下方向鍵：移動選取。
  - **不建的部分**（列在 `Skipped`）：
    - `Character_Ani`（Effect）、`DeleteDelay_Image`、`Back_Image`（兩者都沒有圖號）、所有 Tooltip。
    - `CharacterName_Change_Window`、`CharacterName_Merge_Window`、`PledgeName_Merge_Window`：3.81 沒有改名或合併的封包。
- **新檔 `engine/source/NativeUi/CreateCharPanel.cs`**（`PanelId="CreateUI"`）
  - 用 `[UiPanel(187)]` 自動發現，實作 `IUiClosablePanel`、`IUiTextInput`，圖層 Float，宿主版面 `uistatus86`。
  - 由選角畫面的 `Create_Button` 呼叫 `Begin` 開啟。沒有空欄位時不開。
  - **職業場景**（`Class_Select_Scene`）：
    - 7 個職業按鈕：Prince…Illusionist 對應 class 0…6。選中的職業用 PushSurf 畫。
    - `Warrior_Button` 不建：3.81 沒有戰士，7707／7708 也就不會去查。
    - 選擇性別。`Male_Back`／`Female_Back` 只畫選中的那一個。
    - `Next` 進入能力場景，`Prev` 回選角畫面。
  - **能力場景**（`Stat_Setting_Scene`）：
    - 六項能力的 Point 與 Up／Down 對應 CharacterCreation 的順序：STR、DEX、CON、WIS、CHA、INT。
    - 每項能力不能低於職業基礎值、不能高於基礎值加可分配點數；`Remain_Point` 顯示 75 減去總和。
    - `Reset_Button` 把能力回到職業基礎值。
    - `Username_Edit`：輸入的字元走 WM_CHAR，IME 組字完成後才收到。上限是 LimitText 的 12 字，Backspace 刪一字。
    - `OK_Button`：先用 `CharacterManagementPackets.Validate` 檢查（規則與 WinForms 對話框相同），通過才呼叫 `LoginConnection.CreateCharacter`。
      - 本機檢查失敗、送出被拒、伺服器拒絕：都在 `Message_Window` 顯示原因。
      - 伺服器確認、新角色出現在清單裡：關掉建角畫面，回到選角畫面。
    - 按鍵：Enter 等於 OK（職業場景是 Next），Esc 等於 Prev。
  - **不建或停用**：
    - `Customizing_Win`：衍生加成表，引擎沒有這些數值，跟 #763 相同。
    - `Gauge_Group`：沒有量表長度的公式。
    - 職業場景的 `Front_Image`／`Back_Image`：沒有圖號。
    - `StatType_Group` 只顯示目前職業的那組按鈕。引擎沒有自動配點的預設表，所以 Attack／Defense／Mix 與 `Customizing_Button` 都停用（待做）。
- **`engine/source/CharacterManagementUi.cs`（只加原生分流）**：
  - `AttachNativeCharacterScreens` 掛在 timer tick 上。原生 UI 可能在角色清單已經到了之後才啟用，所以不能只在登入事件時掛。
  - 條件：`NativeUiActive`、`SelectUI` 可用、面板沒有 Faulted。符合時掛上 `CharacterScreenHost`；建角畫面可用時一併接上，不可用時建角改走 WinForms。
  - 原本 WinForms「建立角色」按鈕的內容抽成 `OpenCharacterCreationDialog`（行為不變），供 `CreateFallback` 共用。
  - `--classic-ui`、沒有參考客戶端、XML 壞掉時，都不會掛 host，WinForms 路徑完全照舊：開發工具視窗的「登入／選角」頁與建角對話框。
- **新檔 `engine/source/NativeUi/SelectCharPanelTests.cs`**：`[WorldModuleTest("select-char-ui")]`、`[WorldModuleTest("create-char-ui")]`。全部用合成資料：同結構的小版面、純色圖、假 host。涵蓋：
  - 自動發現、圖層、在 root 上互相找到對方。
  - 版面解析：List 排列、巢狀座標、繪圖順序、略過的項目、Missing 的路徑。
  - 列的狀態：角色、七日待刪、空欄位、鎖住。
  - 互動：選取、捲動、雙擊、按鍵、刪除確認、伺服器結果訊息、被拒訊息、modal、generation 改變、進入世界後關閉。
  - 建角：職業與性別、配點上下限、輸入名稱、本機檢查、送出的 request、伺服器拒絕與確認、壞版面。

> 任務說「測試用 `[CheckGroup]`」：`engine/source` 內的測試一律用 `[WorldModuleTest]` 自動發現，與 #757／#761／#763／#764 相同；`[CheckGroup]` 只用在 `_integration/dev`。任務指定的檔名「選角與建角畫面Tests.cs」依指示改用英文類名 `SelectCharPanelTests`，兩個畫面共用一個檔。

## 資源核對（PakViewer CLI，唯讀，輸出只在 worktree 的 `scratch/t765/`，沒有進 git）

- **XML**：用 `pakviewer-cli pak extract` 從 kor25 `Tile.idx` 抽出，再 `xml decrypt`。四個雜湊都與 PAK§4.1 相符。

  | 檔案 | 原檔 SHA-256 | 解密後 SHA-256 |
  |---|---|---|
  | `SelectUI.xml` | `24661d17a8429adf6890bf83aaf1e3cd197b9a752759102fd6aded22a414af40` | `b4eb1fa7409611aa809c546aafb2615d901eaa74681ffaeae0a906e562bc46b9` |
  | `CreateUI.xml` | `c759172004f8c9b62a47c6cc906d033d1ae9cf5a07c478e7831a92f3a2d69f04` | `5277a2af96a418126401deb4100a382f377628629a414b3e770e2ce0e1a9d65f` |

- **查詢範圍**：
  - 用 `docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt -Out scratch\t765\pak-index` 列出清單：
    - `0-客戶端` 全部 24 個 idx：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile.idx`。
    - kor25、t89、ly 的 `Tile`／`Text`／`Sprite*.idx`。
  - 再用 `scratch/t765/find_surfs.py` 逐一比對每個 surf 的 `<id>c.img`、`<id>.img`、`<id>.tbt`、`<id>c.png`、`<id>.png`。XML 註解內的 surf 不算。
- **結果**：

  | XML | surf 數 | 不在本機釘選的 17 個 Sprite 檔 | 其中只在 `LiTo\LTsurf.pak` | kor25 |
  |---|---:|---|---|---|
  | `SelectUI.xml` | 50 | 15 個：6988、6989、7033–7038、7232–7238 | 無 | **15/15**，例如 `Sprite10` `6988.img`、`Sprite11` `7033c.img`、`Sprite02` `7238c.img` |
  | `CreateUI.xml` | 73 | 25 個：7099、7100、7128–7143、7156、7170、7171、7224、7225、7707、7708 | 7100、7130、7131、7156、7224、7225 | **25/25**，例如 `Sprite07` `7099c.img`、`Sprite03` `7707c.img`、`Sprite14` `7156.img` |

  - 每個 surf 在三套參考客戶端的位置，請看 `find_surfs.py` 的輸出。t89 缺 7224、7225、7707、7708；ly 和 kor25 一樣齊全。
  - **SelectUI 的 15 個缺圖都屬於改名／合併視窗**，本任務不建這幾個視窗，所以執行期不會去查這些圖。選角主窗與確認框用到的 surf，本機全部都有。
  - **更正 PAK§4.1**：
    - CreateUI 寫「還缺 2 個」，這兩個是 7707／7708（戰士）。它們在 kor25 與 ly 都是 `c.img`，舊表沒有把 `c` 名稱算進去。
    - kor25 實際能補齊全部 25 個；SelectUI 的 kor25 也是 15/15。
    - 跟 #763 一樣，舊表的「kor25 10/15、14/19」低估了實際數量。
  - 會實際缺圖的只有 7707／7708：參考客戶端是 t89 時才缺，而且戰士按鈕本來就不建，所以不會發生。kor25 在預設路徑時，兩個畫面都不缺圖。
  - 照 `SurfCatalog.NameHash` 的規則，上表的 Sprite 檔號應該都對得上，但這只做了清單比對，**沒有在本機實際解碼（待驗證）**。

## 怎麼驗證

- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t765/build`，**0 錯誤**。只有 2 個 `MagicUi.cs` 警告，原本就有。
  - 本機 restore 的方法跟 #761／#763 相同：讀主專案 `obj/` 的 assets 與 props，把專案路徑改成本 worktree，套件資料夾維持主專案的 `.nuget`，再放進 worktree 的 `obj/`。主專案只讀、沒有寫入。
- 已同步 `origin/main`（含 #764 登入畫面），沒有衝突，合併後重新編譯通過。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- `rebuild-lock.json` 沒有改動。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`select-char-ui`、`create-char-ui` 與既有測試都交給 PR 的 GitHub Actions CI，結果見 PR。

## 未驗證什麼（待驗證）

- 本機不能跑程式，所以沒有實測真實 kor25 的兩份 XML 經面板解析後是否 `Available`。只做了靜態比對：元素種類與合成版面相同，用到的屬性都有處理。
- 以下都是**推測**，SelectUI／CreateUI 的 binder 沒有反組譯：
  - 繪圖順序：兄弟元素反序，父元素先畫。
  - 選取的角色用 PushSurf 畫；雙擊進入世界。
  - 超出 Slots 的列顯示成鎖住。
  - 性別底圖只畫選中的那一個。
  - `ClassName_Screen` 7099;4 與 `ClassDesc_Screen` 7142;4 原樣畫一張圖，binder 是否依職業換圖待確認。
  - 生日欄直接顯示 S93 的原始數值，格式待確認。
  - 刪除完成後用 Message_Window 顯示伺服器回的結果。
  - `Message_Window` 的 `DarkBack`（變暗背景）沒有畫。
- 真機（交給 Codex／dot，P24Lab 測試角色）：
  1. 原生 UI 開啟、kor25 在預設路徑時登入，log 應出現 `ui-xml-ref SelectUI.xml`，並顯示全畫面選角畫面。
  2. 選角、進入世界。
  3. 建立角色：選職業與性別、配點、輸入中文名稱（IME）、送出。
  4. 刪除角色：七日待刪除的角色會顯示 `WaitToDel` 圖，再按一次刪除會取消排程。
  5. 按 Back 登出。
  6. 加 `--classic-ui` 時，應維持 WinForms 路徑（開發工具視窗的「登入／選角」頁）。

## 沒有做的部分（待做）

- 選角畫面右側的角色動畫 `Character_Ani`：需要依職業與性別決定 sprite，引擎還沒有對照。
- 建角畫面的職業立繪 `Front_Image`／`Back_Image`：沒有圖號。
- 自動配點：Attack／Defense／Mix 沒有預設表。
- `Customizing_Win` 加成表與 `Gauge_Group` 量表：引擎沒有數值。
- 音效：`OverSound`／`PushSound`／`FocusSound` 都沒有接，跟先前的 P-D 面板相同。
- 改名／合併視窗：3.81 沒有對應封包。

## 產出路徑

- `engine/source/NativeUi/SelectCharPanel.cs`（新）
- `engine/source/NativeUi/CreateCharPanel.cs`（新）
- `engine/source/NativeUi/SelectCharPanelTests.cs`（新）
- `engine/source/CharacterManagementUi.cs`（原生分流）
- 本報告 `docs/reports/issue-765.md`
- 分支 `task/765`

## 給下一棒的提示

- 真機若發現繪圖順序或選取顯示不對：
  - 繪圖順序：改 `CharUiBuilder.Walk`。
  - 列的狀態、選取的畫法：改 `SelectCharPanel.ShownPiece`／`PaintPiece`。
- 建角送出前的檢查只在 `CharacterManagementPackets.Validate` 一處，WinForms 與原生兩個入口共用，要改規則只改那裡。
- 之後若要補角色動畫或職業立繪，先在參考客戶端的 binder 確認 `Character_Ani`／`Front_Image` 怎麼選圖，再接到兩個面板，不要自己編圖號。
- 建議把 PAK§4.1 的 SelectUI／CreateUI 兩列更正為「kor25 15/15、25/25（含 `c.img`）」。本任務沒有改 PAK 文件。
