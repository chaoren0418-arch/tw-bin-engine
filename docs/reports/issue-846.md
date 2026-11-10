# Issue #846 — T362：R9-B 地圖窗右鍵選單與存點窗

依據：`docs/reference/followups-9.md` §7.2／§7.4／§5.3／§16.2（R9-B）、`gap-matrix.md` §15.11。分支 `task/846`。

## 做了什麼

| 檔案 | 改動 |
|---|---|
| `engine/source/NativeUi/FieldMapPanel.cs` | 右鍵**按下只記旗標**（`RightPressed`，原版 `byte_153F2F5`），**放開**時（在窗內、不在關閉／放大鈕上）在游標處叫出選單（`Popup.ShowMenu`）；選單開著時，在地圖窗內按左鍵或右鍵會先收起選單，再照常處理。新增 `MapPointAt`／`ToMapPoint`（`sub_A66F50`：游標 → 地圖座標，是 `ToPixel` 的反運算，截斷方式和原版一樣）。發送提示進行中時畫 surf 150 標記。`:10` 的 op 132 註解改成「開舊式 `MapWindow`，3.81 不送」（§5.3）。原本的「右鍵＝TransferMenu（推測）」改成已驗證的流程。 |
| `engine/source/NativeUi/MapCommunicationPanel.cs`（新） | `MapCommunicationSelectWindow`（Float 層）：鈕 0 用 2944／2946，在 (0,0)；鈕 1 用 2947／2949，在 (0, surf 128 的高)。位置是 (min(x,560)+1, min(y,440)+1)。左鍵放開在鈕上才觸發。鈕 0 走既有的 `MapSendEntry`（聊天輸入列「/loc 」），之後設提示旗標，標記畫在 (選單 x−4, y−4)。鈕 1 開存點窗。`IFieldMapPopup` 介面讓 Field_Map 不必認得面板。同檔另有 `MapSendPromptPanel`：把 **1785 畫在聊天輸入列上**（輸入文字右側，到輸入列右緣 −4 為止），滑鼠事件一律轉給聊天面板。 |
| `engine/source/NativeUi/MapLocationSavePanel.cs`（新） | `MapLocationSaveWindow`（Window 層，進視窗堆疊）：底圖 134 置中在 ((800−w)/2, (530−h)/2)。調色盤從 (11,43) 開始，每格 19、每列 8 格，點選範圍是 (11,43)–(w155+8, h155+40)，選取框 138。輸入框在 (12,108)，110×16，最多 12 字。確認鈕 2951／2953 在 (124,104)。Enter＝確認，ESC＝關閉，Backspace 刪字。`MapLocationBook`：每個角色一份本地清單，上限 30 筆；滿了窗不關，顯示 1786。 |
| `engine/source/NativeUi/LocationListPanel.cs` | 拿掉清單旁邊的 1785 提示列（`Prompting`／`PromptWidth`／`PromptRowText`／`PromptColor`），1785 改畫在聊天列。`MapSendEntry` 保留不動。 |
| `engine/source/MapPopupTests.cs`（新，`[WorldModuleTest("map-popup")]`） | 本組離線測試（見下）。 |
| `engine/source/MapSendTests.cs`（**不在原列範圍，必要的連帶修改**） | 舊測試寫死「右鍵按下就開提示」和「清單面板畫 1785 提示列」，行為改了之後這兩項一定會失敗。改成直接呼叫 `TransferMenu`，並檢查 `Entry.Prompting`；右鍵流程移到 `map-popup` 測。op 132 的標籤一併更正。 |

沒改：`WorldSession.cs`、`UiPanelRegistry.cs`、`UiRoot.cs`、`Program.cs`、`DemoChecks.csproj`、`ChatPanel.cs`、`rebuild-lock.json`、`docs/REPORT.md`。三個新面板都用 `[UiPanel(192／193／194)]` 自動發現，也都實作 `IUiClosablePanel`。

## 補查（IDA 靜態分析，已驗證）

TW16 IDB（`P01 ida/03-ascii-isolated/TW1608262200.idb`，SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`）複製到 `scratch/rea/ida/tw16.idb`，用 `docs/reference/rea-scripts/t160/run.ps1` 執行 `decomp.py` 和一次性的 `scratch/rea/r9b_vt.py`（只讀，不存 IDB）。

| 函式（VA） | 結論 |
|---|---|
| `sub_A62870` 選單建構 | 鈕 0 用 2944／2946，在 (0,0)；鈕 1 用 2947／2949，在 `sub_571E20(surf 128)`（128 的高）；回呼 `sub_A762A0` 帶 0／1 |
| `sub_A66950`（vtable `0x1190EAC`+32，顯示） | 位置是 `min(dword_13CF498,560)+1`、`min(dword_13CF49C,440)+1`（推測這兩個是滑鼠座標）；+8＝1（顯示），+9＝0 |
| `sub_A68770`（+36，隱藏） | +8＝0；+9 開著時呼叫 `sub_A77D20(0)` 與 `sub_7DCE00` |
| `sub_A6AA10`（+8，繪製） | 只在 **+9（發送提示中）** 為真時畫 surf 150，位置 (選單 x−4, y−4) |
| `sub_A762A0` | 先隱藏選單。a1≠0 → 存點窗 vtable+32；a1＝0 → `sub_AC98E0(sub_4EAFF0(1785), …, 回呼 sub_A770F0)`，接著 `sub_A77D20(1)` |
| `sub_A770F0` | `sub_7DCE00()`，接著 `sub_A77D20(0)`（提示結束，清掉旗標） |
| `sub_A6F0B0` Field_Map 滑鼠 | 先交給存點窗，再交給選單；選單開著、沒按到鈕時，513／516 會先收起選單，然後照常處理。516：只設旗標。517：旗標有設時算 `sub_A66F50`（結果只放在區域變數），再顯示選單。518：GM 送 op 226，否則 `sub_A77D80` 關地圖窗 |
| `sub_A62C40` 存點窗建構 | 底圖 134 置中在 (800,530) 內；確認鈕 2951／2953 在 (124,104)，回呼 `sub_A77080`；輸入框 `sub_AC3D90(x+12, y+108, 110, 16)`，`sub_703EA0(12)`，字色 `dword_1369D70[14]` |
| `sub_A6AA80` 繪製 | 134 用 `sub_7D7740(…, 23)` 畫成半透明；選取框 138 在 (11+19·(i%8), 43+19·(i/8)) |
| `sub_A6E710` 滑鼠 | 513 落在 (x+11, y+43)–(x+w155+8, y+h155+40) 內時，i＝(mx−x−11)/19＋8·((my−y−43)/19)。155 只用來量範圍，不會畫出來 |
| `sub_A6E6A0` 鍵盤 | 13 → `sub_A77080`，27 → 關窗，其他鍵交給輸入框 |
| `sub_A66A10`／`sub_A687B0` | 顯示時，已開著就不動，否則把焦點給輸入框；隱藏 |
| **`sub_A77080`** | **把輸入框讀進 25 byte 的區域緩衝區後就關窗，沒有存到任何地方** |

**存點在地圖上的畫法（任務要求先補查）**：舊式路徑（本機實際用的 `Field_Map`）**不會存點**。`sub_A77080` 把輸入丟掉，選單算出的地圖座標也只放在區域變數，所以 Field_Map 上**不會畫任何存點**。`.loc` 檔和 `ShowBookmark` 只出現在 MapUI 那條路徑，本機沒有 `MapUI.xml`（FU9 §7.1）。所以這次只做到存進本地清單，不在地圖上畫。

**FU9 §7.2 需要更正的地方**：原文「選單開著時另畫 surf 150」不對，應該是「鈕 0 的發送提示進行中才畫」，那時選單已經收起。這裡只記錄，文件不在本任務的可改範圍。

**資源**：用到的 surf 有 128、134、138、150、155、2944、2946、2947、2949、2951、2953，依 FU9 §7.2 都已確認本機有（T355 用 PakViewer 查過）。這次沒有宣稱任何資源缺少，所以沒有重查 pak。程式在缺圖時一律不畫：沒有鈕圖就不開選單；沒有 128，鈕 1 就沒有位置；沒有 134，存點窗就不開。

## 引擎的做法（推測，報告標明）

- **1785 的位置**：原版畫在聊天輸入列本身，`sub_7025D0(字串,14,14)`。引擎的 `ChatPanel` 不在可改範圍，所以改由 `MapSendPromptPanel` 畫在**輸入文字右側**，確切 x 是推測的。提示文字只用 1785 原文；沒有字串表時顯示 `〔訊息1785〕`。
- **存進本地**：原版舊式路徑不存。引擎在確認時存（地圖、座標、顏色、備註）到 `MapLocationBook`，上限 30 筆、滿了顯示 1786，這兩條規則取自 MapUI 路徑 `sub_8D40C0`。清單**只在記憶體裡**，不寫 `.loc` 檔。滿額時 1786 畫在輸入框上方 (12,90)，位置是推測的；這時窗不關（原版一律關窗）。
- **座標**：畫面上有 png 時，用游標下的地圖座標；沒有 png（地城外框、圖還沒載入）時，退回用玩家目前位置。
- **底圖不做半透明**：做法和 `ClanWarehouseHistoryPanel` 相同，因為引擎沒有透明度設定 `dword_1372264`。
- 右鍵按在選單上會直接收起選單，不會像原版那樣接著把旗標交給 Field_Map，所以要再右鍵一次才會重開。
- 518（右鍵雙擊：GM 送 op 226，其他人關地圖窗）**沒有做**。op 226 是 GM 功能；雙擊關窗留給後續。
- `--classic-ui`（WinForms）：沒有這個選單，也沒有存點窗；發送座標的入口仍然是聊天指令 `/loc`，這部分沒改。

## 怎麼驗證

- **本機編譯（通過，0 個錯誤）**：這台機器在 restore 時有 WebView2 的 NETSDK1130 問題，#817 已經遇過，和本次改動無關。只為了編譯，我在 `scratch/b846/` 做了一個暫時的 MSBuild hook（`fix.targets`），用 `-p:CustomAfterMicrosoftCommonTargets` 拿掉 `.winmd`，改參考 net462 的 `Microsoft.Web.WebView2.Core/WinForms.dll`。repo 裡的檔案都沒動。指令：
  `dotnet build source/OfflineClient.csproj -c Release --no-restore -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false -p:CustomAfterMicrosoftCommonTargets=<scratch>\fix.targets -o scratch\b846\client`
  結果 `0 個錯誤`；4 個警告都不在本次改動的檔案。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4325 檔，0 錯誤）。
- **單元／離線測試**：依本機執行限制，**沒有在本機執行**，交給 CI（`--module-tests`，包含 `map-popup`、`map-send`、`ui-stack-esc` 等）。結果見 PR 的 CI。
- `map-popup` 涵蓋：
  - 三個面板的註冊、圖層、`IUiClosablePanel`；
  - 右鍵按下只記旗標、放開才出選單（也測經過 `UiInput` 的 capture 路徑）；
  - 選單位置是游標＋1，並夾在 560×440 內；
  - 按下時先收起選單；
  - 鈕 0 → 聊天列「/loc 」、標記、1785 位置計算、ESC 結束提示；
  - 鈕 1 → 存點窗置中、調色盤 index、選取框、12 字上限、Enter／確認鈕存檔、每角色分開、30 筆上限與 1786、ESC；
  - 缺圖不畫；離開世界時全部收起；`ToMapPoint` 和 `ToPixel` 互逆。

## 未驗證什麼（待驗證，交給 Codex 真機）

- 真機上的選單位置、鈕圖外觀，以及 surf 150 標記是不是真的對準點選的地方。
- 1785 在聊天列上的位置與觀感（推測）。
- 存點窗的底圖、調色盤格數（surf 155 的實際大小）、輸入法輸入 12 字。
- `sub_7DCE00`、`dword_13CF498/49C` 的語意（推測分別是「收掉提示」和「滑鼠座標」）。
- `sub_565DE0` 回傳的 png 像素基準（`ToMapPoint` 的推測）。

## 產出路徑

- 程式：上表 6 個檔案。
- 報告：`docs/reports/issue-846.md`。
- 逆向的暫存輸出：`scratch/rea/out/r9b1`、`scratch/rea/out/r9b2`（不追蹤）。

## 給下一棒的提示

1. 要讓 1785 真的畫在輸入列本身（取代「/loc 」前綴的作法），要改 `ChatPanel`：加一個提示狀態，例如 `ChatInputLine.Hint`。之後 `MapSendPromptPanel` 可以拿掉，`LocationListPanel.MapSendEntry` 同時改用提示狀態。
2. 存點要寫檔的話，可以照 MapUI 路徑 `sub_8D40C0` 的 `<角色名>.loc` 格式（44 byte 一筆、XOR 校驗）實作 `MapLocationBook` 的讀寫。在地圖上畫存點要另外定規格，因為舊式路徑沒有這個功能。
3. 518（右鍵雙擊關地圖窗）要先確認宿主送進來的 `UiMouseEvent.Clicks` 會不會是 2。
4. FU9 §7.2「選單開著時畫 150」需要更正（見上）。
5. 本機 NETSDK1130 問題：可以用本報告的 `fix.targets` 作法在 scratch 編譯，但根本原因（restore 只解出 net462／winmd）還是要用 `needs:local` 處理。
