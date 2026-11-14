# Issue #1205 f1186-uitext-pixel：原生 UI 文字在 DPI 192 畫成兩倍大

階段 code、claude-local、第 1 次嘗試。分支 `task/1205`。

## 結論
根因確認是 **DPI**：原生 UI 字型以「點」定大小，而畫布 Bitmap 繼承了螢幕 DPI。已改成照原版以**像素**定字高（MingLiU 12 px、字元集 136、`GraphicsUnit.Pixel`），與畫布 DPI 無關；新增離線檢查 `native-ui-text-dpi`（也掛在 CI 會跑的 `native-ui-canvas` 群組）。本機只編譯、未執行；檢查結果以 PR 的 CI 為準，真機效果交 `f1186-retest`。

## 根因（已驗證，靜態讀碼）
1. `engine/source/Program.cs:15`：`Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`，所以程序是 DPI aware，GDI+ 拿到的是真實螢幕 DPI（#1186 的環境是 192）。
2. `engine/source/NativeUi/UiCanvas.cs:14-15`：畫布是 `new Bitmap(800,600,Format32bppPArgb)`＋`Graphics.FromImage`。沒有呼叫 `SetResolution`，所以 Bitmap 的解析度＝螢幕 DPI（192），`Graphics.DpiX/DpiY` 也是 192。HyperText 量字用的 `new Bitmap(1,1)`（`HyperTextPanel.cs:127`）也一樣。
3. 修改前 `engine/source/NativeUi/UiText.cs:12`：`new Font("新細明體",9f,FontStyle.Regular,GraphicsUnit.Point)`。GDI+ 把點數依 Graphics 的 DPI 換成像素：96 DPI → 9×96/72＝12 px；192 DPI → 24 px。800×600 畫布的版面（聊天列距 12、魔法列 16、背包格 33、HP 條高 14／15／20）全以 12 px 為準，因此所有經 `UiText` 畫的字變兩倍高，互相覆蓋或超出格子（SM10、SM12、SM16／SM33、SM18、SM20）。
4. 另外 `UiText.Font.Height`（`ItemWindowPanel.cs:384`、`WarehouseWindowPanel.cs:270`、`TransactionPanel.cs:298`、`LoginPanel.cs:434/462/469`）用的是點數字型在螢幕 DPI 下的行距，DPI 192 時約 29 px，讓背包數量的位置、登入框提示的行距也跟著錯。
5. 對照組：物件名稱（`ObjectRendering.cs:550` `CreateFontW(12,…)`、`:774` `GraphicsUnit.Pixel`）本來就是像素字高，所以 #1186 畫面上大小正常——與上述推論一致。

## 修改
| 檔案 | 修改前 | 修改後 |
|---|---|---|
| `engine/source/NativeUi/UiText.cs` | `新細明體` 9 pt、`GraphicsUnit.Point` | `MingLiU` **12 px**、`GraphicsUnit.Pixel`、GdiCharSet 136；新增 `PixelHeight=12`、`CharSet`、`FaceUsed`。字型不在時依序退到 `細明體`→`PMingLiU`→`新細明體`（找不到就交給 GDI+ 替代） |
| `ItemWindowPanel.cs`、`WarehouseWindowPanel.cs`、`TransactionPanel.cs`、`LoginPanel.cs` | `UiText.Font.Height` | `UiText.PixelHeight`（原版 cell 高 12） |
| `PledgePanel.cs:224` | `TextHeight=12`（註解寫「新細明體 9pt 推測」） | `TextHeight=UiText.PixelHeight`，註解更新 |
| `engine/_integration/dev/native-ui-tests/UiTextDpiChecks.cs`（新） | — | `[CheckGroup("native-ui-text-dpi")]`，同時掛 `native-ui-canvas`（Order 40）與 Default（Order 44） |

**字面改動的依據**：原版 `sub_8444A0` 是 `CreateFontA(12,0,0,0,400,0,0,0,136,0,0,0,0,"MingLiU")`（UI§1 V4；`ObjectRendering.cs:500-511` 的 `NativeNameFont`、`NativeFontTests.cs:38` 已用同一組參數）。`新細明體` 是 PMingLiU（比例字），原版用的是等寬的 MingLiU（細明體）。本機 `C:\Windows\Fonts\mingliu.ttc` 以 fontTools 唯讀讀出：MingLiU upm 1024、ascent 820、descent 204（＝1 em，internal leading 0，所以 CreateFont 高 12 ＝ em 12 px）、`A` 的 advance 512（半形 6 px）；PMingLiU 的 `A` 是 694（比例字）。版面裡「每 Big5 位元組 6 px」的寫法（`CookBookPanel.cs:52`、`NameLayout.GlyphWidth=6`）也是以 MingLiU 為前提。

`UiClientBridge.cs:96-100` 的 `DrawString(nativeUiError,Font,…)` 是畫在**視窗**上（不是 800×600 畫布）的啟動失敗紅字，用表單字型，應隨視窗 DPI 縮放，不屬本問題，未改。`engine/source/NativeUi/` 內其他地方沒有 `new Font`／直接 `DrawString`。

聊天列（SM12）：列距 `ChatRules.LineHeight=12`（`State/ChatState.cs:38`）沒有寫死成別的值，字高回到 12 px 後與列距一致；逐字量寬（`ChatPanel.cs:147`）在 MingLiU 下就是 6／12 px，與 `UiText.Measure` 相加一致。不需另改列距。

## DPI 96／192 量測表
本機規定不執行編譯出的程式，下表是**依字型度量推算**（推測，CI 會實測並以 `[native-ui-text-dpi]` 字樣印在 CI log）：

| 字串 | 修改前 DPI 96 | 修改前 DPI 192 | 修改後 DPI 96 | 修改後 DPI 192 |
|---|---|---|---|---|
| 字型 em 高 | 12 px | **24 px** | 12 px | 12 px |
| `A` 寬 | ≈ 8（PMingLiU 694/1024） | ≈ 16 | 6 | 6 |
| `國` 寬 | 12 | 24 | 12 | 12 |
| `32767/32767` 寬 | ≈ 66 | ≈ 132（超過 HP 條 85） | 66 | 66 |
| `光箭` 寬 | 24 | 48 | 24 | 24 |
| `99999` 寬 | ≈ 30 | ≈ 60（超過背包格 33） | 30 | 30 |

格子寬度來源（檢查裡用的數字）：
- HUD：`HpMpLayout.For` V1 表（`HpMpPanel.cs:20-51`，docs/reference/ui-layout.md §2 V1）；最窄：橫式一般 status 0 HP 85×20、MP 80×20；粗條高 14、直式高 15。
- 聊天：`ChatRules` 文字區 x 30–344（314 px）、列距 12（`State/ChatState.cs:38`）。
- 背包：`LegacyCellGrid.IconPitch=33`（`ItemIconSource.cs:91`；本機 uimagicitem.xml 推得 5×5 格，`ItemWindowPanel.cs:305`）。
- 魔法：列寬 145（uimagicitem.xml：底圖 x 6、等級條 14×212 在 x 12 → 列左緣 26，捲軸 x 171；`SpellWindowPanel.cs:206` 的檢查值）、列高 `RowPitch=16`。

## 檢查 `native-ui-text-dpi`（新增；CI 執行）
- 字型：`GraphicsUnit.Pixel`、Size 12、GdiCharSet 136、`FaceName=="MingLiU"`、`PixelHeight==ChatRules.LineHeight`。
- 對照：同一個 9 pt 字型在 `SetResolution(96)`／`SetResolution(192)` 的畫布上行高差 ≥1.9 倍（證明測試真的換了 DPI，也就是舊缺陷）。
- 11 個字串（含 HUD 數值、百分比、聊天一列、說明文字、背包數量、魔法名）：DPI 96 與 192 的 `UiText.Measure` 寬高相同、實際墨點框相同、墨點高 ≤ 12、墨點寬 ≤ 量測寬 +1。
- 有 MingLiU 時：半形 6、全形 12、逐字寬度相加＝整串寬（無 MingLiU 的機器只記錄實際字型，不比字寬）。
- 格子：12 種 HP/MP 條排法都放得下 `32767/32767` 與 `100%`、墨點高 ≤ 條高；聊天一列墨點高 ≤ 12、26 個全形字 ≤ 314；`99999` ≤ 33；`光箭`＋MP ≤ 145、+2 起畫不超出 16 px 列。

## 怎麼驗證
- 已驗證：本機 `dotnet build source/OfflineClient.csproj -c Release`、`dotnet build _integration/dev/demo-tests/DemoChecks.csproj -c Release` 皆 0 錯誤（設了 `DOTNET_CLI_HOME`＋`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`＋`DOTNET_NOLOGO=1`）。根因鏈（PerMonitorV2 → Bitmap DPI → 點數字型）為靜態讀碼確認。MingLiU 度量以 fontTools 唯讀讀 `C:\Windows\Fonts\mingliu.ttc`。
- 待 CI：`native-ui-canvas`（含新檢查）與 `--module-tests` 綠燈。
- 未做：沒有執行任何編譯出的程式、沒有看真機畫面（依 claude-local 限制）。

## 已驗證／推測／待驗證
- **已驗證**：根因位置（`Program.cs:15`、`UiCanvas.cs:14`、舊 `UiText.cs:12`）；原版字型參數（既有 `NativeNameFont` 註解與測試）；MingLiU 度量；編譯通過。
- **推測**：上表數值（由字型度量推算，CI 會實測）；GDI+ 畫 MingLiU 12 px 與原版 GDI TextOut 的像素是否逐點相同（只保證大小一致，未比對點陣）；`Font.Height` 改 `PixelHeight` 後背包數量、倉庫數量、交易數量、登入提示行距與原版一致（原版 cell 高 12）。
- **待驗證（交 `f1186-retest`，DPI 192、800×600）**：SM10 底列 HP／MP 數值、百分比、左側狀態字不重疊（注意：HP 條 status 1 時「百分比在左 +7」與「數值置中」在大數值下可能本來就靠很近，請截圖確認）；SM12 聊天列不裁切；SM16／SM33 Ctrl+H 說明頁不重疊；SM18 背包數量不重疊；SM20 魔法窗「光箭」不超格；SM04 登入框文字（提示行距、游標高度）回歸。

## 產出
- `engine/source/NativeUi/UiText.cs`、`ItemWindowPanel.cs`、`WarehouseWindowPanel.cs`、`TransactionPanel.cs`、`LoginPanel.cs`、`PledgePanel.cs`
- `engine/_integration/dev/native-ui-tests/UiTextDpiChecks.cs`
- 本報告 `docs/reports/issue-1205.md`

## 給下一棒的提示
- `f1186-retest`：用與 #1186 相同的 DPI 192 環境重拍 SM04、SM10、SM12、SM16、SM18、SM20、SM33；可順便拍一張 DPI 96 對照。若字仍偏大，先看 log 有沒有 `[native-ui-text-dpi] face used:` 的字型名稱（CI log 也有），確認是否落到 MingLiU。
- 如果 CI 機器沒有 MingLiU，MingLiU 專屬的字寬斷言會略過；使用者的繁中 Windows 有 `mingliu.ttc`，真機會走 MingLiU。
- 若之後要像物件名稱一樣改成 GDI 1× 光柵化（`NativeNameFont` 的做法）以逐點對齊原版，可另開任務；本張只修大小。
